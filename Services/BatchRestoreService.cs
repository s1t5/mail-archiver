// Services/BatchRestoreService.cs
using MailArchiver.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace MailArchiver.Services
{
public class BatchRestoreService : BackgroundService, IBatchRestoreService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BatchRestoreService> _logger;
        private readonly BatchOperationOptions _batchOptions;
        private readonly ConcurrentQueue<BatchRestoreJob> _jobQueue = new();
        private readonly ConcurrentDictionary<string, BatchRestoreJob> _allJobs = new();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource? _currentJobCancellation;

        public BatchRestoreService(IServiceProvider serviceProvider, ILogger<BatchRestoreService> logger, IOptions<BatchOperationOptions> batchOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _batchOptions = batchOptions.Value;
            
            // Cleanup-Timer: Jeden Stunde alte Jobs entfernen
            _cleanupTimer = new Timer(
                callback: _ => CleanupOldJobs(),
                state: null,
                dueTime: TimeSpan.FromMinutes(60),
                period: TimeSpan.FromMinutes(60)
            );
        }

        public string QueueJob(BatchRestoreJob job)
        {
            job.Status = BatchRestoreJobStatus.Queued;
            _allJobs[job.JobId] = job;
            _jobQueue.Enqueue(job);
            
            _logger.LogInformation("Queued batch restore job {JobId} with {Count} emails", 
                job.JobId, job.EmailIds.Count);
            
            return job.JobId;
        }

        public BatchRestoreJob? GetJob(string jobId)
        {
            return _allJobs.TryGetValue(jobId, out var job) ? job : null;
        }

        public List<BatchRestoreJob> GetActiveJobs()
        {
            return _allJobs.Values
                .Where(j => j.Status == BatchRestoreJobStatus.Queued || j.Status == BatchRestoreJobStatus.Running)
                .OrderBy(j => j.Created)
                .ToList();
        }

        public bool CancelJob(string jobId)
        {
            if (_allJobs.TryGetValue(jobId, out var job))
            {
                if (job.Status == BatchRestoreJobStatus.Queued)
                {
                    job.Status = BatchRestoreJobStatus.Cancelled;
                    _logger.LogInformation("Cancelled queued job {JobId}", jobId);
                    return true;
                }
                else if (job.Status == BatchRestoreJobStatus.Running)
                {
                    job.Status = BatchRestoreJobStatus.Cancelled;
                    _currentJobCancellation?.Cancel();
                    _logger.LogInformation("Requested cancellation of running job {JobId}", jobId);
                    return true;
                }
            }
            return false;
        }

        public void CleanupOldJobs()
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24); // Jobs älter als 24 Stunden entfernen
            var toRemove = _allJobs.Values
                .Where(j => j.Completed.HasValue && j.Completed < cutoffTime)
                .Select(j => j.JobId)
                .ToList();

            foreach (var jobId in toRemove)
            {
                _allJobs.TryRemove(jobId, out _);
            }

            if (toRemove.Any())
            {
                _logger.LogInformation("Cleaned up {Count} old batch restore jobs", toRemove.Count);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Batch Restore Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_jobQueue.TryDequeue(out var job))
                    {
                        // Prüfe ob Job bereits abgebrochen wurde
                        if (job.Status == BatchRestoreJobStatus.Cancelled)
                        {
                            _logger.LogInformation("Skipping cancelled job {JobId}", job.JobId);
                            continue;
                        }

                        await ProcessJob(job, stoppingToken);
                    }
                    else
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Batch Restore Background Service stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Batch Restore Background Service");
                    await Task.Delay(5000, stoppingToken); // Warte 5 Sekunden bei Fehlern
                }
            }
        }

        private async Task ProcessJob(BatchRestoreJob job, CancellationToken stoppingToken)
        {
            _currentJobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var cancellationToken = _currentJobCancellation.Token;

            try
            {
                job.Status = BatchRestoreJobStatus.Running;
                job.Started = DateTime.UtcNow;
                
                _logger.LogInformation("Starting batch restore job {JobId} with {Count} emails", 
                    job.JobId, job.EmailIds.Count);

                using var scope = _serviceProvider.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                // Verarbeite in Batches mit Progress-Updates
                await ProcessJobWithProgress(job, emailService, cancellationToken);

                if (job.Status != BatchRestoreJobStatus.Cancelled)
                {
                    job.Status = BatchRestoreJobStatus.Completed;
                    job.Completed = DateTime.UtcNow;
                    
                    _logger.LogInformation("Completed batch restore job {JobId}. Success: {Success}, Failed: {Failed}", 
                        job.JobId, job.SuccessCount, job.FailedCount);
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = BatchRestoreJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                _logger.LogInformation("Batch restore job {JobId} was cancelled", job.JobId);
            }
            catch (Exception ex)
            {
                job.Status = BatchRestoreJobStatus.Failed;
                job.Completed = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Batch restore job {JobId} failed", job.JobId);
            }
            finally
            {
                _currentJobCancellation?.Dispose();
                _currentJobCancellation = null;
            }
        }

private async Task ProcessJobWithProgress(BatchRestoreJob job, IEmailService emailService, CancellationToken cancellationToken)
        {
            var batchSize = _batchOptions.BatchSize;
            var totalEmails = job.EmailIds.Count;

            for (int i = 0; i < totalEmails; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = job.EmailIds.Skip(i).Take(batchSize).ToList();
                
                _logger.LogInformation("Job {JobId}: Processing batch {Current}/{Total} ({BatchStart}-{BatchEnd} of {Total})",
                    job.JobId,
                    (i / batchSize) + 1, 
                    (totalEmails + batchSize - 1) / batchSize,
                    i + 1, 
                    Math.Min(i + batchSize, totalEmails), 
                    totalEmails);

                foreach (var emailId in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var result = await emailService.RestoreEmailToFolderAsync(emailId, job.TargetAccountId, job.TargetFolder);
                        if (result)
                        {
                            job.SuccessCount++;
                        }
                        else
                        {
                            job.FailedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        job.FailedCount++;
                        _logger.LogWarning(ex, "Job {JobId}: Failed to restore email {EmailId}", job.JobId, emailId);
                    }

                    job.ProcessedCount++;

                    // Kleine Pause zwischen E-Mails
                    if (_batchOptions.PauseBetweenEmailsMs > 0)
                    {
                        await Task.Delay(_batchOptions.PauseBetweenEmailsMs, cancellationToken);
                    }
                }

                // Pause zwischen Batches
                if (i + batchSize < totalEmails && _batchOptions.PauseBetweenBatchesMs > 0)
                {
                    await Task.Delay(_batchOptions.PauseBetweenBatchesMs, cancellationToken);
                }
            }
        }

        public override void Dispose()
        {
            _cleanupTimer?.Dispose();
            _currentJobCancellation?.Dispose();
            base.Dispose();
        }
    }
}
