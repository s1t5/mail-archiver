using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace MailArchiver.Services
{
    public class MailAccountDeletionService : BackgroundService, IMailAccountDeletionService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MailAccountDeletionService> _logger;
        private readonly BatchOperationOptions _batchOptions;
        private readonly ConcurrentQueue<MailAccountDeletionJob> _jobQueue = new();
        private readonly ConcurrentDictionary<string, MailAccountDeletionJob> _allJobs = new();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource? _currentJobCancellation;
        private readonly ISyncJobService _syncJobService;

        public MailAccountDeletionService(
            IServiceProvider serviceProvider,
            ILogger<MailAccountDeletionService> logger,
            IOptions<BatchOperationOptions> batchOptions,
            ISyncJobService syncJobService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _batchOptions = batchOptions.Value;
            _syncJobService = syncJobService;

            // Cleanup timer: Remove old completed jobs every 24 hours
            _cleanupTimer = new Timer(
                callback: _ => CleanupOldJobs(),
                state: null,
                dueTime: TimeSpan.FromHours(24),
                period: TimeSpan.FromHours(24)
            );
        }

        public string QueueDeletion(int mailAccountId, string mailAccountName, string userId)
        {
            var job = new MailAccountDeletionJob
            {
                MailAccountId = mailAccountId,
                MailAccountName = mailAccountName,
                UserId = userId,
                Status = MailAccountDeletionJobStatus.Queued
            };

            _allJobs[job.JobId] = job;
            _jobQueue.Enqueue(job);
            
            _logger.LogInformation("Queued deletion job {JobId} for account {AccountName} (ID: {AccountId})",
                job.JobId, job.MailAccountName, job.MailAccountId);
            
            return job.JobId;
        }

        public MailAccountDeletionJob? GetJob(string jobId)
        {
            return _allJobs.TryGetValue(jobId, out var job) ? job : null;
        }

        public bool CancelJob(string jobId)
        {
            if (_allJobs.TryGetValue(jobId, out var job))
            {
                if (job.Status == MailAccountDeletionJobStatus.Queued)
                {
                    job.Status = MailAccountDeletionJobStatus.Cancelled;
                    job.Completed = DateTime.UtcNow;
                    _logger.LogInformation("Cancelled queued deletion job {JobId}", jobId);
                    return true;
                }
                else if (job.Status == MailAccountDeletionJobStatus.Running)
                {
                    job.Status = MailAccountDeletionJobStatus.Cancelled;
                    _currentJobCancellation?.Cancel();
                    _logger.LogInformation("Requested cancellation of running deletion job {JobId}", jobId);
                    return true;
                }
            }
            return false;
        }

        public IEnumerable<MailAccountDeletionJob> GetAllJobs()
        {
            return _allJobs.Values
                .OrderByDescending(j => j.Status == MailAccountDeletionJobStatus.Queued || 
                                       j.Status == MailAccountDeletionJobStatus.Running)
                .ThenByDescending(j => j.Created)
                .ToList();
        }

        private void CleanupOldJobs()
        {
            var cutoffTime = DateTime.UtcNow.AddDays(-7);
            var toRemove = _allJobs.Values
                .Where(j => j.Completed.HasValue && j.Completed < cutoffTime)
                .ToList();

            foreach (var job in toRemove)
            {
                _allJobs.TryRemove(job.JobId, out _);
            }

            if (toRemove.Any())
            {
                _logger.LogInformation("Cleaned up {Count} old deletion jobs", toRemove.Count);
            }
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Mail Account Deletion Service is starting.");
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Mail Account Deletion Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_jobQueue.TryDequeue(out var job))
                    {
                        if (job.Status == MailAccountDeletionJobStatus.Cancelled)
                        {
                            _logger.LogInformation("Skipping cancelled deletion job {JobId}", job.JobId);
                            continue;
                        }

                        await ProcessDeletionJob(job, stoppingToken);
                    }
                    else
                    {
                        await Task.Delay(100, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Mail Account Deletion Service stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Mail Account Deletion Service");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Mail Account Deletion Service is stopping.");
            return base.StopAsync(cancellationToken);
        }

        private async Task ProcessDeletionJob(MailAccountDeletionJob job, CancellationToken stoppingToken)
        {
            _currentJobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var cancellationToken = _currentJobCancellation.Token;

            try
            {
                job.Status = MailAccountDeletionJobStatus.Running;
                job.Started = DateTime.UtcNow;

                _logger.LogInformation("Starting deletion job {JobId} for account {AccountName} (ID: {AccountId})",
                    job.JobId, job.MailAccountName, job.MailAccountId);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

                // Phase 1: Cancel any running sync jobs
                job.CurrentPhase = "Cancelling sync jobs";
                _syncJobService.CancelJobsForAccount(job.MailAccountId);
                _logger.LogInformation("Job {JobId}: Cancelled any running sync jobs for account {AccountId}",
                    job.JobId, job.MailAccountId);

                // Phase 2: Unlock all emails (required for compliance mode)
                job.CurrentPhase = "Unlocking emails";
                var lockedEmails = await context.ArchivedEmails
                    .Where(e => e.MailAccountId == job.MailAccountId && e.IsLocked)
                    .ToListAsync(cancellationToken);

                if (lockedEmails.Any())
                {
                    _logger.LogInformation("Job {JobId}: Unlocking {Count} locked emails for account {AccountId}",
                        job.JobId, lockedEmails.Count, job.MailAccountId);

                    foreach (var email in lockedEmails)
                    {
                        email.IsLocked = false;
                    }
                    await context.SaveChangesAsync(cancellationToken);
                }

                // Phase 3: Count items to delete
                job.CurrentPhase = "Counting items";
                var emailIds = await context.ArchivedEmails
                    .Where(e => e.MailAccountId == job.MailAccountId)
                    .Select(e => e.Id)
                    .ToListAsync(cancellationToken);

                job.TotalEmails = emailIds.Count;
                job.TotalAttachments = await context.EmailAttachments
                    .Where(a => emailIds.Contains(a.ArchivedEmailId))
                    .CountAsync(cancellationToken);

                _logger.LogInformation("Job {JobId}: Found {EmailCount} emails and {AttachmentCount} attachments to delete",
                    job.JobId, job.TotalEmails, job.TotalAttachments);

                // Phase 4: Delete attachments in batches
                job.CurrentPhase = "Deleting attachments";
                await DeleteAttachmentsInBatches(job, context, emailIds, cancellationToken);

                // Phase 5: Delete emails in batches
                job.CurrentPhase = "Deleting emails";
                await DeleteEmailsInBatches(job, context, emailIds, cancellationToken);

                // Phase 6: Delete the mail account
                job.CurrentPhase = "Deleting account";
                var account = await context.MailAccounts.FindAsync(new object[] { job.MailAccountId }, cancellationToken);
                if (account != null)
                {
                    context.MailAccounts.Remove(account);
                    await context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Job {JobId}: Deleted mail account {AccountName} (ID: {AccountId})",
                        job.JobId, job.MailAccountName, job.MailAccountId);
                }

                if (job.Status != MailAccountDeletionJobStatus.Cancelled)
                {
                    job.Status = MailAccountDeletionJobStatus.Completed;
                    job.Completed = DateTime.UtcNow;
                    job.CurrentPhase = "Completed";
                    
                    _logger.LogInformation("Completed deletion job {JobId}. Deleted: {DeletedEmails} emails, {DeletedAttachments} attachments",
                        job.JobId, job.DeletedEmails, job.DeletedAttachments);
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = MailAccountDeletionJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                job.CurrentPhase = "Cancelled";
                _logger.LogInformation("Deletion job {JobId} was cancelled", job.JobId);
            }
            catch (Exception ex)
            {
                job.Status = MailAccountDeletionJobStatus.Failed;
                job.Completed = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
                job.CurrentPhase = "Failed";
                _logger.LogError(ex, "Deletion job {JobId} failed", job.JobId);
            }
            finally
            {
                _currentJobCancellation?.Dispose();
                _currentJobCancellation = null;
            }
        }

        private async Task DeleteAttachmentsInBatches(
            MailAccountDeletionJob job, 
            MailArchiverDbContext context, 
            List<int> emailIds, 
            CancellationToken cancellationToken)
        {
            const int batchSize = 1000;
            var processedCount = 0;

            while (processedCount < job.TotalAttachments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = await context.EmailAttachments
                    .Where(a => emailIds.Contains(a.ArchivedEmailId))
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (!batch.Any())
                    break;

                context.EmailAttachments.RemoveRange(batch);
                await context.SaveChangesAsync(cancellationToken);

                job.DeletedAttachments += batch.Count;
                processedCount += batch.Count;

                // Log progress every 5000 attachments
                if (job.DeletedAttachments % 5000 == 0 || job.DeletedAttachments == job.TotalAttachments)
                {
                    var progressPercent = job.TotalAttachments > 0 
                        ? (job.DeletedAttachments * 100.0 / job.TotalAttachments) 
                        : 0;
                    _logger.LogInformation("Job {JobId}: Deleted {Deleted}/{Total} attachments ({Progress:F1}%)",
                        job.JobId, job.DeletedAttachments, job.TotalAttachments, progressPercent);
                }

                // Small pause between batches to reduce database load
                if (_batchOptions.PauseBetweenEmailsMs > 0)
                {
                    await Task.Delay(_batchOptions.PauseBetweenEmailsMs, cancellationToken);
                }
            }
        }

        private async Task DeleteEmailsInBatches(
            MailAccountDeletionJob job, 
            MailArchiverDbContext context, 
            List<int> emailIds, 
            CancellationToken cancellationToken)
        {
            const int batchSize = 1000;
            var processedCount = 0;

            while (processedCount < job.TotalEmails)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = await context.ArchivedEmails
                    .Where(e => e.MailAccountId == job.MailAccountId)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (!batch.Any())
                    break;

                context.ArchivedEmails.RemoveRange(batch);
                await context.SaveChangesAsync(cancellationToken);

                job.DeletedEmails += batch.Count;
                processedCount += batch.Count;

                // Log progress every 5000 emails
                if (job.DeletedEmails % 5000 == 0 || job.DeletedEmails == job.TotalEmails)
                {
                    var progressPercent = job.TotalEmails > 0 
                        ? (job.DeletedEmails * 100.0 / job.TotalEmails) 
                        : 0;
                    _logger.LogInformation("Job {JobId}: Deleted {Deleted}/{Total} emails ({Progress:F1}%)",
                        job.JobId, job.DeletedEmails, job.TotalEmails, progressPercent);
                }

                // Small pause between batches to reduce database load
                if (_batchOptions.PauseBetweenEmailsMs > 0)
                {
                    await Task.Delay(_batchOptions.PauseBetweenEmailsMs, cancellationToken);
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
