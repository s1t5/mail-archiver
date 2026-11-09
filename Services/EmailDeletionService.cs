using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Core;
using MailArchiver.Services.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly int _batchSize;

        public EmailDeletionService(
            ILogger<EmailDeletionService> logger,
            IServiceScopeFactory serviceScopeFactory,
            int batchSize = 1000)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _batchSize = batchSize;
        }

        public string QueueJob(EmailDeletionJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            // Check deletion policy
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var deletionPolicy = scope.ServiceProvider.GetRequiredService<IOptions<DeletionPolicyOptions>>().Value;
                if (!deletionPolicy.DeletionAllowed)
                {
                    _logger.LogWarning("Email deletion job rejected: deletion is disabled by policy (DeletionPolicy:DeletionAllowed=false)");
                    job.JobId = Guid.NewGuid().ToString();
                    job.Status = EmailDeletionJobStatus.Failed;
                    job.Created = DateTime.UtcNow;
                    job.Completed = DateTime.UtcNow;
                    job.ErrorMessage = "Email deletion is disabled by configuration (DeletionPolicy:DeletionAllowed=false).";
                    _jobs.TryAdd(job.JobId, job);
                    return job.JobId;
                }
            }

            job.JobId = Guid.NewGuid().ToString();
            job.Status = EmailDeletionJobStatus.Queued;
            job.Created = DateTime.UtcNow;
            // Filter jobs bring their own count from the confirmation step; the
            // service recounts against the frozen upper bound when the job starts.
            if (job.Filter == null)
                job.TotalEmails = job.EmailIds.Count;

            _jobs.TryAdd(job.JobId, job);
            _logger.LogInformation("Email deletion job {JobId} queued with {Count} emails (filter: {IsFilterJob})",
                job.JobId, job.TotalEmails, job.Filter != null);

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
                    job.JobId, job.IsFilterJob ? job.TotalEmails : job.EmailIds.Count);

                using var scope = _serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                var accessLogService = scope.ServiceProvider.GetService<IAccessLogService>();
                var rawMessageStore = scope.ServiceProvider.GetService<IRawMessageStore>();
                var deletionPolicy = scope.ServiceProvider.GetService<IOptions<DeletionPolicyOptions>>()?.Value;

                // A job can wait in the queue while an administrator changes the policy.
                // Re-check at start so a queued job cannot outlive the setting that allowed it.
                if (deletionPolicy is { DeletionAllowed: false })
                {
                    throw new InvalidOperationException(
                        "Email deletion is disabled by configuration (DeletionPolicy:DeletionAllowed=false).");
                }

                var combinedToken = CancellationTokenSource
                    .CreateLinkedTokenSource(stoppingToken, job.CancellationTokenSource.Token)
                    .Token;

                var affectedAccountIds = job.Filter != null
                    ? await ProcessFilterJobAsync(job, context, accessLogService, rawMessageStore, deletionPolicy, combinedToken)
                    : await ProcessIdJobAsync(job, context, accessLogService, rawMessageStore, combinedToken);

                job.CurrentPhase = "Completed";
                job.Status = EmailDeletionJobStatus.Completed;
                job.Completed = DateTime.UtcNow;

                _logger.LogInformation(
                    "Email deletion job {JobId} completed successfully. Deleted {EmailCount} emails and {AttachmentCount} attachments",
                    job.JobId, job.DeletedEmails, job.DeletedAttachments);

                await RefreshStorageCachesAsync(affectedAccountIds);
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

        /// <summary>
        /// Deletes the explicit id list of a selection job and returns the accounts
        /// whose storage cache has to be refreshed.
        /// </summary>
        private async Task<HashSet<int>> ProcessIdJobAsync(
            EmailDeletionJob job,
            MailArchiverDbContext context,
            IAccessLogService? accessLogService,
            IRawMessageStore? rawMessageStore,
            CancellationToken combinedToken)
        {
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
                    .Take(_batchSize)
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
            var affectedAccountIds = new HashSet<int>();

            while (remainingEmails > 0)
            {
                combinedToken.ThrowIfCancellationRequested();

                var emailsToDelete = await context.ArchivedEmails
                    .Where(e => job.EmailIds.Contains(e.Id))
                    .Take(_batchSize)
                    .ToListAsync(combinedToken);

                if (!emailsToDelete.Any())
                    break;

                // Log each deletion if access log service is available. The entries are added
                // to the same context and saved together with the removal below: one round
                // trip per batch instead of one SaveChanges per email, and a deletion is never
                // committed without its log entry.
                // Note the scope of that guarantee: it holds per batch. Batches that were
                // already committed stay committed when a later batch fails, so a job that
                // dies halfway leaves the earlier batches deleted and logged and the failed
                // batch untouched - the job as a whole is not rolled back.
                if (accessLogService != null)
                {
                    var now = DateTime.UtcNow;
                    context.AccessLogs.AddRange(emailsToDelete.Select(email => new AccessLog
                    {
                        Username = job.UserId,
                        Type = AccessLogType.Deletion,
                        Timestamp = now,
                        EmailId = email.Id,
                        EmailSubject = email.Subject?.Length > 255
                            ? email.Subject.Substring(0, 255)
                            : email.Subject,
                        EmailFrom = email.From?.Length > 255
                            ? email.From.Substring(0, 255)
                            : email.From,
                        MailAccountId = email.MailAccountId
                    }));
                }

                context.ArchivedEmails.RemoveRange(emailsToDelete);
                await context.SaveChangesAsync(combinedToken);

                // The original files go after the rows are gone; see IRawMessageStore.DeleteAsync.
                if (rawMessageStore != null)
                {
                    try
                    {
                        await rawMessageStore.DeleteAsync(
                            emailsToDelete.Select(e => e.Id).ToList(), combinedToken);
                    }
                    catch (Exception rawEx)
                    {
                        _logger.LogError(rawEx,
                            "Job {JobId}: could not delete the stored originals of a batch", job.JobId);
                    }
                }

                foreach (var email in emailsToDelete)
                    affectedAccountIds.Add(email.MailAccountId);

                job.DeletedEmails += emailsToDelete.Count;
                remainingEmails -= emailsToDelete.Count;

                _logger.LogDebug("Job {JobId}: Deleted {Count} emails, {Remaining} remaining",
                    job.JobId, emailsToDelete.Count, remainingEmails);
            }

            return affectedAccountIds;
        }

        /// <summary>
        /// Deletes every email matching the job's filter, batch by batch. The filter is
        /// re-evaluated for each batch and capped at the id that existed when the job was
        /// confirmed, so mail arriving during the job is not covered by that confirmation.
        /// Attachments are removed by the ON DELETE CASCADE foreign key.
        /// </summary>
        private async Task<HashSet<int>> ProcessFilterJobAsync(
            EmailDeletionJob job,
            MailArchiverDbContext context,
            IAccessLogService? accessLogService,
            IRawMessageStore? rawMessageStore,
            DeletionPolicyOptions? deletionPolicy,
            CancellationToken combinedToken)
        {
            var filter = job.Filter!;

            // Freeze the set first (if the controller did not already do it), then count
            // against that same bound. Otherwise a mail arriving between count and start
            // would inflate the denominator without ever being deleted.
            if (filter.UpperBoundId is null)
            {
                var (_, _, maxId) = await EmailFilterQuery.CountAsync(context, filter, combinedToken);
                filter.UpperBoundId = maxId;
            }

            job.CurrentPhase = "Counting matching emails";
            var (deletable, locked, _) = await EmailFilterQuery.CountAsync(context, filter, combinedToken);
            job.SkippedLockedEmails = locked;
            job.TotalEmails = deletable;

            // The controller checks the cap before queueing. Count again here: emails can be
            // unlocked while the job waits, so the confirmed set is not necessarily the set
            // this job is about to delete.
            if (deletionPolicy != null && deletable > deletionPolicy.MaxBulkDeleteEmails)
            {
                throw new InvalidOperationException(
                    $"The filter matches {deletable:N0} emails, more than DeletionPolicy:MaxBulkDeleteEmails allows ({deletionPolicy.MaxBulkDeleteEmails:N0}). Narrow the filter.");
            }

            _logger.LogInformation(
                "Job {JobId}: filter matches {Deletable} deletable emails, {Locked} locked (bound {Bound})",
                job.JobId, deletable, locked, filter.UpperBoundId);

            var affectedAccountIds = new HashSet<int>();
            job.CurrentPhase = "Deleting emails";

            while (true)
            {
                combinedToken.ThrowIfCancellationRequested();

                var ids = await EmailFilterQuery.IdsBatchAsync(
                    context, filter, job.LastProcessedId, _batchSize, combinedToken);
                if (ids.Count == 0)
                    break;

                // Project only the fields the audit entry needs; loading Body/HtmlBody
                // for thousands of emails would be pure waste.
                var rows = await context.ArchivedEmails
                    .Where(e => ids.Contains(e.Id))
                    .Select(e => new { e.Id, e.Subject, e.From, e.MailAccountId })
                    .ToListAsync(combinedToken);

                if (rows.Count > 0)
                {
                    // The test fixture runs the job inside its own transaction; nesting
                    // one would fail. Only own (and commit) a transaction when there is
                    // none, so the audit rows and the deletes stay atomic.
                    var ownsTransaction = context.Database.CurrentTransaction == null;
                    await using var transaction = ownsTransaction
                        ? await context.Database.BeginTransactionAsync(combinedToken)
                        : null;

                    if (accessLogService != null)
                    {
                        var now = DateTime.UtcNow;
                        context.AccessLogs.AddRange(rows.Select(email => new AccessLog
                        {
                            Username = job.UserId,
                            Type = AccessLogType.Deletion,
                            Timestamp = now,
                            EmailId = email.Id,
                            EmailSubject = email.Subject?.Length > 255
                                ? email.Subject.Substring(0, 255)
                                : email.Subject,
                            EmailFrom = email.From?.Length > 255
                                ? email.From.Substring(0, 255)
                                : email.From,
                            MailAccountId = email.MailAccountId
                        }));
                        await context.SaveChangesAsync(combinedToken);
                    }

                    await context.ArchivedEmails
                        .Where(e => ids.Contains(e.Id))
                        .ExecuteDeleteAsync(combinedToken);

                    if (transaction != null)
                        await transaction.CommitAsync(combinedToken);

                    // The original files go after the rows are gone; see IRawMessageStore.DeleteAsync.
                    if (rawMessageStore != null)
                    {
                        try
                        {
                            await rawMessageStore.DeleteAsync(ids, combinedToken);
                        }
                        catch (Exception rawEx)
                        {
                            _logger.LogError(rawEx,
                                "Job {JobId}: could not delete the stored originals of a batch", job.JobId);
                        }
                    }

                    foreach (var row in rows)
                        affectedAccountIds.Add(row.MailAccountId);

                    job.DeletedEmails += rows.Count;
                }

                context.ChangeTracker.Clear();
                job.LastProcessedId = ids[^1];
                job.CurrentPhase = $"Deleting emails ({job.DeletedEmails:N0} / {job.TotalEmails:N0})";

                _logger.LogDebug("Job {JobId}: deleted {Count} emails, cursor {Cursor}",
                    job.JobId, job.DeletedEmails, job.LastProcessedId);
            }

            return affectedAccountIds;
        }

        private async Task RefreshStorageCachesAsync(HashSet<int> affectedAccountIds)
        {
            if (affectedAccountIds.Count == 0)
                return;

            try
            {
                using var storageScope = _serviceScopeFactory.CreateScope();
                var storageService = storageScope.ServiceProvider.GetRequiredService<IAccountStorageService>();
                foreach (var accountId in affectedAccountIds)
                {
                    try { await storageService.RefreshAccountStorageAsync(accountId); }
                    catch (Exception acctEx)
                    {
                        _logger.LogDebug(acctEx, "Storage cache refresh for account {AccountId} after deletion failed (non-fatal)", accountId);
                    }
                }
            }
            catch (Exception storageEx)
            {
                _logger.LogDebug(storageEx, "Storage cache refresh after deletion failed (non-fatal)");
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
