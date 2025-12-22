using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MailArchiver.Services
{
    public class EmailDeletionService : BackgroundService, IEmailDeletionService
    {
        private readonly ILogger<EmailDeletionService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ConcurrentDictionary<string, EmailDeletionJob> _jobs = new();
        private readonly SemaphoreSlim _jobSemaphore = new(1, 1);
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
        private readonly TimeSpan _completedJobRetention = TimeSpan.FromDays(7);
        private const int BatchSize = 1000;

        public EmailDeletionService(
            ILogger<EmailDeletionService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public string QueueJob(EmailDeletionJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            job.JobId = Guid.NewGuid().ToString();
            job.Status = EmailDeletionJobStatus.Queued;
            job.Created = DateTime.UtcNow;
            job.TotalEmails = job.EmailIds.Count;

            _jobs.TryAdd(job.JobId, job);
            _logger.LogInformation("Email deletion job {JobId} queued with {Count} emails", 
                job.JobId, job.EmailIds.Count);

            return job.JobId;
        }

        public EmailDeletionJob? GetJob(string jobId)
        {
            _jobs.TryGetValue(jobId, out var job);
            return job;
        }

        public List<EmailDeletionJob> GetAllJobs()
        {
            return _jobs.Values.OrderByDescending(j => j.Created).ToList();
        }

        public List<EmailDeletionJob> GetActiveJobs()
        {
            return _jobs.Values
                .Where(j => j.Status == EmailDeletionJobStatus.Queued || 
                           j.Status == EmailDeletionJobStatus.Running)
                .OrderBy(j => j.Created)
                .ToList();
        }

        public bool CancelJob(string jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return false;

            if (job.Status == EmailDeletionJobStatus.Completed || 
                job.Status == EmailDeletionJobStatus.Failed || 
                job.Status == EmailDeletionJobStatus.Cancelled)
            {
                return false;
            }

            job.Status = EmailDeletionJobStatus.Cancelled;
            job.CancellationTokenSource.Cancel();
            job.Completed = DateTime.UtcNow;

            _logger.LogInformation("Email deletion job {JobId} cancelled", jobId);
            return true;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email Deletion Service started");

            var cleanupTimer = DateTime.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Process queued jobs
                    var queuedJob = _jobs.Values
                        .Where(j => j.Status == EmailDeletionJobStatus.Queued)
                        .OrderBy(j => j.Created)
                        .FirstOrDefault();

                    if (queuedJob != null)
                    {
                        await ProcessJobAsync(queuedJob, stoppingToken);
                    }

                    // Periodic cleanup
                    if (DateTime.UtcNow - cleanupTimer > _cleanupInterval)
                    {
                        CleanupOldJobs();
                        cleanupTimer = DateTime.UtcNow;
                    }

                    // Wait before checking for next job
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Email Deletion Service stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Email Deletion Service main loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("Email Deletion Service stopped");
        }

        private async Task ProcessJobAsync(EmailDeletionJob job, CancellationToken stoppingToken)
        {
            await _jobSemaphore.WaitAsync(stoppingToken);

            try
            {
                job.Status = EmailDeletionJobStatus.Running;
                job.Started = DateTime.UtcNow;
                job.CurrentPhase = "Starting deletion process";

                _logger.LogInformation("Starting email deletion job {JobId} with {Count} emails",
                    job.JobId, job.EmailIds.Count);

                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                var accessLogService = scope.ServiceProvider.GetService<IAccessLogService>();

                var combinedToken = CancellationTokenSource
                    .CreateLinkedTokenSource(stoppingToken, job.CancellationTokenSource.Token)
                    .Token;

                // Phase 1: Count attachments
                job.CurrentPhase = "Counting attachments";
                job.TotalAttachments = await context.EmailAttachments
                    .Where(a => job.EmailIds.Contains(a.ArchivedEmailId))
                    .CountAsync(combinedToken);

                _logger.LogInformation("Job {JobId}: Found {Count} attachments to delete", 
                    job.JobId, job.TotalAttachments);

                // Phase 2: Delete attachments in batches
                job.CurrentPhase = "Deleting attachments";
                var remainingAttachments = job.TotalAttachments;
                
                while (remainingAttachments > 0)
                {
                    combinedToken.ThrowIfCancellationRequested();

                    var attachmentsToDelete = await context.EmailAttachments
                        .Where(a => job.EmailIds.Contains(a.ArchivedEmailId))
                        .Take(BatchSize)
                        .ToListAsync(combinedToken);

                    if (!attachmentsToDelete.Any())
                        break;

                    context.EmailAttachments.RemoveRange(attachmentsToDelete);
                    await context.SaveChangesAsync(combinedToken);

                    job.DeletedAttachments += attachmentsToDelete.Count;
                    remainingAttachments -= attachmentsToDelete.Count;

                    _logger.LogDebug("Job {JobId}: Deleted {Count} attachments, {Remaining} remaining", 
                        job.JobId, attachmentsToDelete.Count, remainingAttachments);
                }

                // Phase 3: Delete emails in batches
                job.CurrentPhase = "Deleting emails";
                var remainingEmails = job.EmailIds.Count;
                
                while (remainingEmails > 0)
                {
                    combinedToken.ThrowIfCancellationRequested();

                    var emailsToDelete = await context.ArchivedEmails
                        .Where(e => job.EmailIds.Contains(e.Id))
                        .Take(BatchSize)
                        .ToListAsync(combinedToken);

                    if (!emailsToDelete.Any())
                        break;

                    // Log each deletion if access log service is available
                    if (accessLogService != null)
                    {
                        foreach (var email in emailsToDelete)
                        {
                            try
                            {
                                await accessLogService.LogAccessAsync(
                                    job.UserId,
                                    AccessLogType.Deletion,
                                    emailId: email.Id,
                                    emailSubject: email.Subject?.Length > 255 
                                        ? email.Subject.Substring(0, 255) 
                                        : email.Subject,
                                    emailFrom: email.From?.Length > 255 
                                        ? email.From.Substring(0, 255) 
                                        : email.From,
                                    mailAccountId: email.MailAccountId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to log deletion of email {EmailId}", email.Id);
                            }
                        }
                    }

                    context.ArchivedEmails.RemoveRange(emailsToDelete);
                    await context.SaveChangesAsync(combinedToken);

                    job.DeletedEmails += emailsToDelete.Count;
                    remainingEmails -= emailsToDelete.Count;

                    _logger.LogDebug("Job {JobId}: Deleted {Count} emails, {Remaining} remaining", 
                        job.JobId, emailsToDelete.Count, remainingEmails);
                }

                // Phase 5: Complete successfully
                job.CurrentPhase = "Completed";
                job.Status = EmailDeletionJobStatus.Completed;
                job.Completed = DateTime.UtcNow;

                _logger.LogInformation(
                    "Email deletion job {JobId} completed successfully. Deleted {EmailCount} emails and {AttachmentCount} attachments",
                    job.JobId, job.DeletedEmails, job.DeletedAttachments);
            }
            catch (OperationCanceledException)
            {
                job.Status = EmailDeletionJobStatus.Cancelled;
                job.CurrentPhase = "Cancelled";
                job.Completed = DateTime.UtcNow;
                _logger.LogInformation("Email deletion job {JobId} was cancelled", job.JobId);
            }
            catch (Exception ex)
            {
                job.Status = EmailDeletionJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.Completed = DateTime.UtcNow;
                job.CurrentPhase = "Failed";
                _logger.LogError(ex, "Email deletion job {JobId} failed", job.JobId);
            }
            finally
            {
                _jobSemaphore.Release();
            }
        }

        private void CleanupOldJobs()
        {
            try
            {
                var cutoffDate = DateTime.UtcNow - _completedJobRetention;
                var jobsToRemove = _jobs.Values
                    .Where(j => j.IsCompleted && j.Completed.HasValue && j.Completed.Value < cutoffDate)
                    .Select(j => j.JobId)
                    .ToList();

                foreach (var jobId in jobsToRemove)
                {
                    _jobs.TryRemove(jobId, out _);
                    _logger.LogDebug("Removed old email deletion job {JobId}", jobId);
                }

                if (jobsToRemove.Any())
                {
                    _logger.LogInformation("Cleaned up {Count} old email deletion jobs", jobsToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old email deletion jobs");
            }
        }
    }
}
