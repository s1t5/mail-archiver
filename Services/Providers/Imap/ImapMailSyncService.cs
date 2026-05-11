using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Core;
using MailArchiver.Services.Providers.Eml;
using MailArchiver.Utilities;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services.Providers.Imap
{
    /// <summary>
    /// Orchestrates the IMAP email sync pipeline: connection management, folder iteration,
    /// message search with progressive fallback strategies, bandwidth tracking with checkpoints,
    /// batch processing, memory management, and retention deletion.
    /// </summary>
    public class ImapMailSyncService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<ImapMailSyncService> _logger;
        private readonly EmailCoreService _coreService;
        private readonly ISyncJobService _syncJobService;
        private readonly IBandwidthService _bandwidthService;
        private readonly ImapConnectionFactory _connectionFactory;
        private readonly EmlMailCleaner _mailCleaner;
        private readonly IImapFolderService _folderService;
        private readonly DateTimeHelper _dateTimeHelper;
        private readonly BatchOperationOptions _batchOptions;
        private readonly MailSyncOptions _mailSyncOptions;
        private readonly BandwidthTrackingOptions _bandwidthOptions;

        public ImapMailSyncService(
            MailArchiverDbContext context,
            ILogger<ImapMailSyncService> logger,
            EmailCoreService coreService,
            ISyncJobService syncJobService,
            IBandwidthService bandwidthService,
            ImapConnectionFactory connectionFactory,
            EmlMailCleaner mailCleaner,
            IImapFolderService folderService,
            DateTimeHelper dateTimeHelper,
            IOptions<BatchOperationOptions> batchOptions,
            IOptions<MailSyncOptions> mailSyncOptions,
            IOptions<BandwidthTrackingOptions> bandwidthOptions)
        {
            _context = context;
            _logger = logger;
            _coreService = coreService;
            _syncJobService = syncJobService;
            _bandwidthService = bandwidthService;
            _connectionFactory = connectionFactory;
            _mailCleaner = mailCleaner;
            _folderService = folderService;
            _dateTimeHelper = dateTimeHelper;
            _batchOptions = batchOptions.Value;
            _mailSyncOptions = mailSyncOptions.Value;
            _bandwidthOptions = bandwidthOptions.Value;
        }

        /// <summary>
        /// Syncs all emails from the IMAP mailbox for the specified account.
        /// </summary>
        public async Task SyncMailAccountAsync(MailAccount account, string? jobId = null)
        {
            _logger.LogInformation("Starting IMAP sync for account: {AccountName}", account.Name);

            // Check bandwidth limit before starting sync
            if (_bandwidthOptions.Enabled)
            {
                var limitReached = await _bandwidthService.IsLimitReachedAsync(account.Id);
                if (limitReached)
                {
                    var status = await _bandwidthService.GetStatusAsync(account.Id);
                    _logger.LogWarning("Sync skipped for account {AccountName} - bandwidth limit reached. " +
                        "Downloaded: {DownloadedMB:F2} MB / {LimitMB:F2} MB. Reset at: {ResetTime}",
                        account.Name, status.BytesDownloaded / (1024.0 * 1024.0),
                        status.DailyLimitBytes / (1024.0 * 1024.0), status.ResetTime);

                    if (jobId != null)
                    {
                        _syncJobService.CompleteJobRateLimited(jobId, "Bandwidth limit reached - sync paused");
                    }
                    return;
                }
            }

            // Check for incomplete checkpoints (interrupted sync)
            if (_bandwidthOptions.Enabled)
            {
                var hasIncompleteCheckpoints = await _bandwidthService.HasIncompleteCheckpointsAsync(account.Id);
                if (hasIncompleteCheckpoints)
                {
                    _logger.LogInformation("Found incomplete checkpoints for account {AccountName} - resuming from last position", account.Name);
                }
            }

            using var client = _connectionFactory.CreateImapClient(account.Name);
            client.Timeout = 300000;
            client.ServerCertificateValidationCallback = _connectionFactory.ServerCertificateValidationCallback;

            var processedFolders = 0;
            var processedEmails = 0;
            var newEmails = 0;
            var failedEmails = 0;
            var deletedEmails = 0;
            var totalBytesDownloaded = 0L;
            var wasRateLimited = false;

            try
            {
                await _connectionFactory.ConnectWithFallbackAsync(client, account.ImapServer, account.ImapPort ?? 993, account.UseSSL, account.Name);
                await _connectionFactory.AuthenticateClientAsync(client, account);
                _logger.LogInformation("Connected to IMAP server for {AccountName}", account.Name);

                var allFolders = await _folderService.GetAllFoldersAsync(client, account.Name);

                if (jobId != null)
                {
                    _syncJobService.UpdateJobProgress(jobId, job =>
                    {
                        job.TotalFolders = allFolders.Count;
                    });
                }

                _logger.LogInformation("Found {Count} folders for account: {AccountName}", allFolders.Count, account.Name);

                foreach (var folder in allFolders)
                {
                    if (jobId != null)
                    {
                        var job = _syncJobService.GetJob(jobId);
                        if (job?.Status == SyncJobStatus.Cancelled)
                        {
                            _logger.LogInformation("Sync job {JobId} for account {AccountName} has been cancelled", jobId, account.Name);
                            _syncJobService.CompleteJob(jobId, false, "Job was cancelled");
                            return;
                        }
                    }

                    try
                    {
                        if (account.ExcludedFoldersList.Any(f => f.Equals(folder.FullName, StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogInformation("Skipping excluded folder: {FolderName} for account: {AccountName}",
                                folder.FullName, account.Name);
                            processedFolders++;
                            continue;
                        }

                        if (jobId != null)
                        {
                            _syncJobService.UpdateJobProgress(jobId, job =>
                            {
                                job.CurrentFolder = folder.FullName;
                                job.ProcessedFolders = processedFolders;
                            });
                        }

                        var folderResult = await SyncFolderAsync(folder, account, client, jobId);
                        processedEmails += folderResult.ProcessedEmails;
                        newEmails += folderResult.NewEmails;
                        failedEmails += folderResult.FailedEmails;

                        if (folderResult.WasRateLimited)
                        {
                            wasRateLimited = true;
                            _logger.LogWarning("Rate limit hit during folder {FolderName} for account {AccountName}. " +
                                "Stopping folder iteration to preserve bandwidth.", folder.FullName, account.Name);
                            break;
                        }

                        processedFolders++;

                        if (jobId != null)
                        {
                            _syncJobService.UpdateJobProgress(jobId, job =>
                            {
                                job.ProcessedFolders = processedFolders;
                                job.ProcessedEmails = processedEmails;
                                job.NewEmails = newEmails;
                                job.FailedEmails = failedEmails;
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing folder {FolderName} for account {AccountName}: {Message}",
                            folder.FullName, account.Name, ex.Message);
                        failedEmails++;
                    }
                }

                if (account.DeleteAfterDays.HasValue && account.DeleteAfterDays.Value > 0)
                {
                    deletedEmails = await DeleteOldEmailsAsync(account, client, jobId);

                    if (jobId != null)
                    {
                        _syncJobService.UpdateJobProgress(jobId, job =>
                        {
                            job.DeletedEmails = deletedEmails;
                        });
                    }
                }

                if (account.LocalRetentionDays.HasValue && account.LocalRetentionDays.Value > 0)
                {
                    var localDeletedCount = await _coreService.DeleteOldLocalEmailsAsync(account, jobId);
                    _logger.LogInformation("Deleted {Count} emails from local archive for account {AccountName} based on local retention policy",
                        localDeletedCount, account.Name);
                }

                if (wasRateLimited)
                {
                    _logger.LogWarning("Sync for account {AccountName} was rate-limited. Preserving checkpoints for resume. " +
                        "LastSync will NOT be updated. Processed: {Processed}, New: {New}",
                        account.Name, processedEmails, newEmails);

                    await client.DisconnectAsync(true);

                    if (jobId != null)
                    {
                        _syncJobService.CompleteJobRateLimited(jobId,
                            $"Bandwidth limit reached during sync. Processed: {processedEmails}, New: {newEmails}. Sync will resume from checkpoint.");
                    }
                    return;
                }

                if (failedEmails == 0)
                {
                    var trackedAccount = await _context.MailAccounts.FindAsync(account.Id);
                    if (trackedAccount != null)
                    {
                        trackedAccount.LastSync = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }

                    if (_bandwidthOptions.Enabled)
                    {
                        await _bandwidthService.ClearCheckpointsAsync(account.Id);
                        _logger.LogDebug("Cleared sync checkpoints for account {AccountName} after successful sync", account.Name);
                    }
                }
                else
                {
                    _logger.LogWarning("Not updating LastSync for account {AccountName} due to {FailedCount} failed emails",
                        account.Name, failedEmails);
                }

                await client.DisconnectAsync(true);
                _logger.LogInformation("Sync completed for account: {AccountName}. New: {New}, Failed: {Failed}, Deleted: {Deleted}",
                    account.Name, newEmails, failedEmails, deletedEmails);

                if (jobId != null)
                {
                    _syncJobService.CompleteJob(jobId, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sync for account {AccountName}: {Message}", account.Name, ex.Message);

                if (jobId != null)
                {
                    _syncJobService.CompleteJob(jobId, false, ex.Message);
                }
                throw;
            }
        }

        /// <summary>
        /// Tests the connection to an IMAP server.
        /// </summary>
        public async Task<bool> TestConnectionAsync(MailAccount account)
        {
            try
            {
                _logger.LogInformation("Testing connection to IMAP server {Server}:{Port} for account {Name} ({Email})",
                    account.ImapServer, account.ImapPort, account.Name, account.EmailAddress);

                using var client = _connectionFactory.CreateImapClient(account.Name);
                client.Timeout = 30000;
                client.ServerCertificateValidationCallback = _connectionFactory.ServerCertificateValidationCallback;

                _logger.LogDebug("Connecting to {Server}:{Port}, SSL: {UseSSL}",
                    account.ImapServer, account.ImapPort, account.UseSSL);

                await _connectionFactory.ConnectWithFallbackAsync(client, account.ImapServer, account.ImapPort ?? 993, account.UseSSL, account.Name);
                _logger.LogDebug("Connection established, authenticating using {Provider} authentication", account.Provider);

                await _connectionFactory.AuthenticateClientAsync(client, account);
                _logger.LogInformation("Authentication successful for {Email}", account.EmailAddress);

                try
                {
                    var inbox = client.Inbox;
                    if (inbox != null)
                    {
                        await inbox.OpenAsync(FolderAccess.ReadOnly);
                        _logger.LogInformation("INBOX opened successfully with {Count} messages", inbox.Count);
                    }
                    else
                    {
                        _logger.LogWarning("INBOX is null for account {Name} - server has non-standard IMAP implementation", account.Name);

                        if (client.PersonalNamespaces != null && client.PersonalNamespaces.Count > 0)
                        {
                            _logger.LogInformation("Personal namespace available for account {Name}: {Namespace}",
                                account.Name, client.PersonalNamespaces[0].Path);
                        }
                        else
                        {
                            _logger.LogWarning("No standard INBOX or personal namespace - server uses custom folder structure");
                        }
                    }
                }
                catch (Exception folderEx)
                {
                    _logger.LogWarning(folderEx, "Could not verify folder access for account {Name}, but connection and authentication succeeded. Error: {Message}",
                        account.Name, folderEx.Message);
                }

                await client.DisconnectAsync(true);
                _logger.LogInformation("Connection test passed for account {Name} - connection and authentication successful", account.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection test failed for account {AccountName}: {Message}",
                    account.Name, ex.Message);

                if (ex is ImapCommandException imapEx)
                {
                    _logger.LogError("IMAP command error: {Response}", imapEx.Response);
                }
                else if (ex is MailKit.Security.AuthenticationException)
                {
                    _logger.LogError("Authentication failed - incorrect username or password");
                }
                else if (ex is ImapProtocolException)
                {
                    _logger.LogError("IMAP protocol error - server responded unexpectedly");
                }
                else if (ex is System.Net.Sockets.SocketException socketEx)
                {
                    _logger.LogError("Socket error: {ErrorCode} - could not reach server", socketEx.ErrorCode);
                }
                else if (ex is TimeoutException)
                {
                    _logger.LogError("Connection timed out - server did not respond in time");
                }

                return false;
            }
        }

        /// <summary>
        /// Performs a full resync of an IMAP account by resetting LastSync to Unix epoch.
        /// </summary>
        public async Task<bool> ResyncAccountAsync(int accountId)
        {
            try
            {
                var account = await _context.MailAccounts.FindAsync(accountId);
                if (account == null)
                {
                    _logger.LogError("Account with ID {AccountId} not found for resync", accountId);
                    return false;
                }

                _logger.LogInformation("Starting full resync for IMAP account {AccountName}", account.Name);

                account.LastSync = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                await _context.SaveChangesAsync();

                var accountForSync = await _context.MailAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId);
                if (accountForSync == null)
                {
                    _logger.LogError("Account with ID {AccountId} not found after update", accountId);
                    return false;
                }

                var jobId = _syncJobService.StartSync(accountForSync.Id, accountForSync.Name, accountForSync.LastSync);
                await SyncMailAccountAsync(accountForSync, jobId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during resync for account {AccountId}", accountId);
                return false;
            }
        }

        /// <summary>
        /// Syncs a single IMAP folder: search with progressive fallback, bandwidth tracking,
        /// batch processing, and memory management.
        /// </summary>
        // Threshold for consecutive transient FETCH errors (server-side throttling)
        // before pausing the sync gracefully (analogous to the bandwidth-limit pause).
        private const int MaxConsecutiveTransientFetchFailures = 10;

        // Backoff delays (in milliseconds) for retrying a single message FETCH after a
        // transient server response such as "NO Service temporarily unavailable".
        private static readonly int[] TransientFetchRetryDelaysMs = new[] { 5_000, 15_000, 60_000 };

        /// <summary>
        /// Detects transient IMAP FETCH errors that indicate server-side throttling
        /// (e.g. "NO Service temporarily unavailable", over-quota, rate-limit responses).
        /// These errors are not caused by malformed messages and typically succeed when
        /// retried after a short backoff.
        /// </summary>
        private static bool IsTransientImapError(Exception ex)
        {
            if (ex == null) return false;

            var current = ex;
            while (current != null)
            {
                if (current is ImapCommandException imapEx)
                {
                    if (imapEx.Response == ImapCommandResponse.No || imapEx.Response == ImapCommandResponse.Bad)
                    {
                        var msg = imapEx.Message ?? string.Empty;
                        if (msg.IndexOf("temporarily unavailable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            msg.IndexOf("try again", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            msg.IndexOf("throttle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            msg.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            msg.IndexOf("over quota", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            msg.IndexOf("[LIMIT]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            msg.IndexOf("[OVERQUOTA]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            msg.IndexOf("[UNAVAILABLE]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            msg.IndexOf("[INUSE]", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                }

                current = current.InnerException;
            }

            return false;
        }

        private async Task<SyncFolderResult> SyncFolderAsync(IMailFolder folder, MailAccount account, ImapClient client, string? jobId = null)
        {
            var result = new SyncFolderResult();
            var totalBytesDownloaded = 0L;
            var consecutiveTransientFailures = 0;


            _logger.LogInformation("Syncing folder: {FolderName} for account: {AccountName}",
                folder.FullName, account.Name);

            try
            {
                if (string.IsNullOrEmpty(folder.FullName) ||
                    folder.Attributes.HasFlag(FolderAttributes.NonExistent) ||
                    folder.Attributes.HasFlag(FolderAttributes.NoSelect))
                {
                    _logger.LogInformation("Skipping folder {FolderName} (empty name, non-existent or non-selectable)",
                        folder.FullName);
                    return result;
                }

                if (!client.IsConnected)
                {
                    _logger.LogWarning("Client disconnected during sync, attempting to reconnect...");
                    await _connectionFactory.ReconnectClientAsync(client, account);
                }
                else if (!client.IsAuthenticated)
                {
                    _logger.LogWarning("Client not authenticated, attempting to re-authenticate...");
                    await _connectionFactory.AuthenticateClientAsync(client, account);
                }

                if (!folder.IsOpen)
                {
                    await folder.OpenAsync(FolderAccess.ReadOnly);
                }

                bool isOutgoing = _folderService.IsOutgoingFolder(folder);
                var lastSync = account.LastSync;
                bool isFullSync = account.LastSync == new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                // Resume from checkpoint if available for this folder
                if (_bandwidthOptions.Enabled)
                {
                    try
                    {
                        var checkpoints = await _bandwidthService.GetCheckpointsAsync(account.Id);
                        var folderCheckpoint = checkpoints.FirstOrDefault(c => c.FolderName == folder.FullName);
                        if (folderCheckpoint?.LastMessageDate.HasValue == true)
                        {
                            var checkpointDate = folderCheckpoint.LastMessageDate.Value;
                            if (checkpointDate > lastSync)
                            {
                                _logger.LogInformation("Resuming folder {FolderName} from checkpoint date {CheckpointDate} " +
                                    "(LastSync was {LastSync})", folder.FullName, checkpointDate, lastSync);
                                lastSync = checkpointDate;
                                isFullSync = false;
                            }
                        }
                    }
                    catch (Exception cpEx)
                    {
                        _logger.LogWarning(cpEx, "Error reading checkpoints for folder {FolderName}, using LastSync", folder.FullName);
                    }
                }

                if (!isFullSync)
                {
                    lastSync = lastSync.AddHours(-12);
                }

                var query = SearchQuery.DeliveredAfter(lastSync);

                try
                {
                    if (!client.IsConnected)
                    {
                        _logger.LogWarning("Client disconnected during sync, attempting to reconnect...");
                        await _connectionFactory.ReconnectClientAsync(client, account);
                    }
                    else if (!client.IsAuthenticated)
                    {
                        _logger.LogWarning("Client not authenticated, attempting to re-authenticate...");
                        await _connectionFactory.AuthenticateClientAsync(client, account);
                    }

                    if (!folder.IsOpen)
                    {
                        await folder.OpenAsync(FolderAccess.ReadOnly);
                    }

                    IList<UniqueId> uids;
                    try
                    {
                        uids = await folder.SearchAsync(query);
                        _logger.LogDebug("DeliveredAfter search found {Count} messages in folder {FolderName}",
                            uids.Count, folder.FullName);

                        if (uids.Count == 0 && folder.Count > 0 && isFullSync)
                        {
                            _logger.LogWarning("DeliveredAfter returned 0 results but folder contains {Count} messages during full sync for {FolderName}. Server may not support DeliveredAfter. Using SearchQuery.All instead.",
                                folder.Count, folder.FullName);
                            uids = await folder.SearchAsync(SearchQuery.All);
                            _logger.LogInformation("SearchQuery.All found {Count} messages in folder {FolderName}",
                                uids.Count, folder.FullName);
                        }

                        if (isFullSync && folder.Count > 0 && uids.Count > 0 && uids.Count < folder.Count)
                        {
                            double percentageReturned = (double)uids.Count / folder.Count * 100;

                            _logger.LogWarning("Search returned only {ReturnedCount} of {TotalCount} messages ({Percentage:F1}%) for {FolderName}. " +
                                "Server likely has SEARCH result limit. Fetching all UIDs by sequence instead.",
                                uids.Count, folder.Count, percentageReturned, folder.FullName);

                            var allUids = await folder.FetchAsync(0, -1, MessageSummaryItems.UniqueId);
                            uids = allUids.Select(msg => msg.UniqueId).ToList();

                            _logger.LogInformation("Retrieved {Count} UIDs by sequence for folder {FolderName}",
                                uids.Count, folder.FullName);
                        }
                    }
                    catch (Exception searchEx)
                    {
                        _logger.LogWarning(searchEx, "DeliveredAfter search failed for folder {FolderName}, falling back to SentSince",
                            folder.FullName);

                        try
                        {
                            uids = await folder.SearchAsync(SearchQuery.SentSince(lastSync.Date));
                            _logger.LogDebug("SentSince search found {Count} messages in folder {FolderName}",
                                uids.Count, folder.FullName);

                            if (isFullSync && folder.Count > 0 && uids.Count > 0 && uids.Count < folder.Count)
                            {
                                double percentageReturned = (double)uids.Count / folder.Count * 100;

                                _logger.LogWarning("SentSince returned only {ReturnedCount} of {TotalCount} messages ({Percentage:F1}%) for {FolderName}. " +
                                    "Fetching all UIDs by sequence instead.",
                                    uids.Count, folder.Count, percentageReturned, folder.FullName);

                                var allUids = await folder.FetchAsync(0, -1, MessageSummaryItems.UniqueId);
                                uids = allUids.Select(msg => msg.UniqueId).ToList();

                                _logger.LogInformation("Retrieved {Count} UIDs by sequence for folder {FolderName}",
                                    uids.Count, folder.FullName);
                            }
                        }
                        catch (Exception fallbackEx)
                        {
                            _logger.LogWarning(fallbackEx, "SentSince also failed for folder {FolderName}, using All query",
                                folder.FullName);
                            uids = await folder.SearchAsync(SearchQuery.All);
                            _logger.LogInformation("All query found {Count} total messages in folder {FolderName}, will filter by date client-side",
                                uids.Count, folder.FullName);

                            if (isFullSync && folder.Count > 0 && uids.Count > 0 && uids.Count < folder.Count)
                            {
                                double percentageReturned = (double)uids.Count / folder.Count * 100;

                                _logger.LogWarning("SearchQuery.All returned only {ReturnedCount} of {TotalCount} messages ({Percentage:F1}%) for {FolderName}. " +
                                    "Fetching all UIDs by sequence instead.",
                                    uids.Count, folder.Count, percentageReturned, folder.FullName);

                                var allUids = await folder.FetchAsync(0, -1, MessageSummaryItems.UniqueId);
                                uids = allUids.Select(msg => msg.UniqueId).ToList();

                                _logger.LogInformation("Retrieved {Count} UIDs by sequence for folder {FolderName}",
                                    uids.Count, folder.FullName);
                            }
                        }
                    }

                    _logger.LogInformation("Found {Count} messages to process in folder {FolderName} for account: {AccountName}",
                        uids.Count, folder.FullName, account.Name);

                    for (int i = 0; i < uids.Count; i += _batchOptions.BatchSize)
                    {
                        if (jobId != null)
                        {
                            var job = _syncJobService.GetJob(jobId);
                            if (job?.Status == SyncJobStatus.Cancelled)
                            {
                                _logger.LogInformation("Sync job {JobId} for account {AccountName} has been cancelled during folder sync", jobId, account.Name);
                                return result;
                            }
                        }

                        var batch = uids.Skip(i).Take(_batchOptions.BatchSize).ToList();
                        _logger.LogInformation("Processing batch of {Count} messages (starting at {Start}) in folder {FolderName} via IMAP",
                            batch.Count, i, folder.FullName);

                        foreach (var uid in batch)
                        {
                            if (jobId != null)
                            {
                                var job = _syncJobService.GetJob(jobId);
                                if (job?.Status == SyncJobStatus.Cancelled)
                                {
                                    _logger.LogInformation("Sync job {JobId} for account {AccountName} has been cancelled during message processing", jobId, account.Name);
                                    return result;
                                }
                            }

                            try
                            {
                                if (!client.IsConnected)
                                {
                                    _logger.LogWarning("Client disconnected during sync, attempting to reconnect...");
                                    await _connectionFactory.ReconnectClientAsync(client, account);
                                    await folder.OpenAsync(FolderAccess.ReadOnly);
                                }
                                else if (!client.IsAuthenticated)
                                {
                                    _logger.LogWarning("Client not authenticated, attempting to re-authenticate...");
                                    await _connectionFactory.AuthenticateClientAsync(client, account);
                                }
                                else if (folder.IsOpen == false)
                                {
                                    _logger.LogWarning("Folder is not open, attempting to re-open...");
                                    await folder.OpenAsync(FolderAccess.ReadOnly);
                                }

                                // Retry FETCH on transient server-side throttling responses
                                // (e.g. "NO Service temporarily unavailable", over-quota, rate-limit).
                                // Non-transient errors (malformed messages, UTF-8 issues, etc.) bubble
                                // up immediately to the outer catch and are counted as FailedEmails.
                                MimeKit.MimeMessage? message = null;
                                var maxAttempts = TransientFetchRetryDelaysMs.Length + 1;
                                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                                {
                                    try
                                    {
                                        message = await folder.GetMessageAsync(uid);
                                        // Success - reset the consecutive throttling counter
                                        consecutiveTransientFailures = 0;
                                        break;
                                    }
                                    catch (Exception fetchEx) when (IsTransientImapError(fetchEx) && attempt < maxAttempts)
                                    {
                                        var delayMs = TransientFetchRetryDelaysMs[attempt - 1];
                                        _logger.LogWarning(
                                            "Transient IMAP FETCH error for UID {Uid} in folder {FolderName} on attempt {Attempt}/{Max}. " +
                                            "Server response indicates throttling. Retrying after {DelayMs}ms. Inner: {Message}",
                                            uid, folder.FullName, attempt, maxAttempts, delayMs, fetchEx.Message);

                                        await Task.Delay(delayMs);

                                        // Restore connection / folder state before retrying
                                        try
                                        {
                                            if (!client.IsConnected)
                                            {
                                                await _connectionFactory.ReconnectClientAsync(client, account);
                                            }
                                            else if (!client.IsAuthenticated)
                                            {
                                                await _connectionFactory.AuthenticateClientAsync(client, account);
                                            }

                                            if (!folder.IsOpen)
                                            {
                                                await folder.OpenAsync(FolderAccess.ReadOnly);
                                            }
                                        }
                                        catch (Exception reconnectEx)
                                        {
                                            _logger.LogWarning(reconnectEx,
                                                "Reconnect/reopen before retry failed for UID {Uid} in folder {FolderName}",
                                                uid, folder.FullName);
                                        }
                                    }
                                    catch (Exception fetchEx) when (IsTransientImapError(fetchEx))
                                    {
                                        // Final retry attempt also failed with a transient throttling error.
                                        consecutiveTransientFailures++;

                                        _logger.LogWarning(fetchEx,
                                            "Transient IMAP FETCH error for UID {Uid} in folder {FolderName} persisted after {Attempts} attempts. " +
                                            "Consecutive transient failures: {Count}/{Threshold}.",
                                            uid, folder.FullName, maxAttempts,
                                            consecutiveTransientFailures, MaxConsecutiveTransientFetchFailures);

                                        if (consecutiveTransientFailures >= MaxConsecutiveTransientFetchFailures)
                                        {
                                            _logger.LogWarning(
                                                "Server-side throttling detected: {Count} consecutive transient FETCH failures in folder {FolderName} " +
                                                "for account {AccountName}. Pausing sync gracefully; will resume on the next sync run. " +
                                                "Processed: {Processed}, New: {New}",
                                                consecutiveTransientFailures, folder.FullName, account.Name,
                                                result.ProcessedEmails, result.NewEmails);

                                            result.WasRateLimited = true;
                                            return result;
                                        }

                                        // Re-throw so the outer catch records it as a normal failed email
                                        // (preserves existing detailed error logging and FailedEmails counter).
                                        throw;
                                    }
                                }

                                if (message == null)
                                {
                                    // Should not happen - either we set it or threw - defensive guard.
                                    throw new InvalidOperationException(
                                        $"FETCH for UID {uid} in folder {folder.FullName} returned no message and no exception.");
                                }

                                _mailCleaner.PreCleanMessage(message);


                                long messageSize = 0;
                                if (_bandwidthOptions.Enabled)
                                {
                                    try
                                    {
                                        var messageString = message.ToString();
                                        messageSize = System.Text.Encoding.UTF8.GetByteCount(messageString);

                                        var (_, limitReached) = await _bandwidthService.TrackUsageAndCheckLimitAsync(account.Id, messageSize);
                                        totalBytesDownloaded += messageSize;

                                        _logger.LogDebug("Tracked {Size} bytes for email in folder {FolderName}", messageSize, folder.FullName);

                                        if (limitReached)
                                        {
                                            _logger.LogWarning("Bandwidth limit reached during sync of folder {FolderName} for account {AccountName}. " +
                                                "Saving checkpoint and pausing sync. Processed: {Processed}, New: {New}",
                                                folder.FullName, account.Name, result.ProcessedEmails, result.NewEmails);

                                            await _bandwidthService.UpdateCheckpointAsync(
                                                account.Id, folder.FullName,
                                                message.Date.DateTime, message.MessageId,
                                                messageSize);

                                            result.WasRateLimited = true;
                                            return result;
                                        }
                                    }
                                    catch (Exception bwEx)
                                    {
                                        _logger.LogWarning(bwEx, "Error tracking bandwidth usage");
                                    }
                                }

                                var isNew = await _coreService.ArchiveEmailAsync(account, message, isOutgoing, folder.FullName);
                                if (isNew)
                                {
                                    result.NewEmails++;
                                }

                                result.ProcessedEmails++;

                                if (_bandwidthOptions.Enabled && messageSize > 0)
                                {
                                    try
                                    {
                                        await _bandwidthService.UpdateCheckpointAsync(
                                            account.Id, folder.FullName,
                                            message.Date.DateTime, message.MessageId,
                                            messageSize);
                                    }
                                    catch (Exception cpEx)
                                    {
                                        _logger.LogWarning(cpEx, "Error updating checkpoint");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                var emailSubject = "Unknown";
                                var emailFrom = "Unknown";
                                var emailDate = "Unknown";
                                var emailMessageId = "Unknown";

                                try
                                {
                                    if (client.IsConnected && folder.IsOpen)
                                    {
                                        try
                                        {
                                            var summary = await folder.FetchAsync(new[] { uid }, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);
                                            if (summary != null && summary.Count > 0)
                                            {
                                                var envelope = summary[0].Envelope;
                                                emailSubject = envelope?.Subject ?? "No Subject";
                                                emailFrom = envelope?.From?.ToString() ?? "Unknown Sender";
                                                emailDate = envelope?.Date?.ToString() ?? summary[0].InternalDate?.ToString() ?? "Unknown Date";
                                                emailMessageId = envelope?.MessageId ?? "No Message-ID";
                                            }
                                        }
                                        catch { }
                                    }
                                }
                                catch { }

                                var innermostEx = ex;
                                while (innermostEx.InnerException != null)
                                {
                                    innermostEx = innermostEx.InnerException;
                                }

                                var isUtf8Error = innermostEx.Message.Contains("UTF8") || innermostEx.Message.Contains("0x00") || innermostEx.Message.Contains("22021");

                                _logger.LogError(ex, "Error archiving message from folder {FolderName}. " +
                                    "Email Details - Subject: '{Subject}', From: '{From}', Date: '{Date}', Message-ID: '{MessageId}', UID: {Uid}. " +
                                    "IsUTF8Error: {IsUtf8Error}, InnermostError: {InnermostError}",
                                    folder.FullName, emailSubject, emailFrom, emailDate, emailMessageId, uid, isUtf8Error, innermostEx.Message);

                                result.FailedEmails++;
                            }
                        }

                        if (i + _batchOptions.BatchSize < uids.Count)
                        {
                            _context.ChangeTracker.Clear();

                            if (_batchOptions.PauseBetweenBatchesMs > 0)
                            {
                                await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                            }

                            GC.Collect();
                            GC.WaitForPendingFinalizers();

                            _logger.LogInformation("Memory usage after processing batch {BatchNumber}: {MemoryUsage}",
                                (i / _batchOptions.BatchSize) + 1, MemoryMonitor.GetMemoryUsageFormatted());
                        }
                        else if (_batchOptions.PauseBetweenEmailsMs > 0 && batch.Count > 1)
                        {
                            await Task.Delay(_batchOptions.PauseBetweenEmailsMs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error searching messages in folder {FolderName}: {Message}",
                        folder.FullName, ex.Message);
                    result.FailedEmails = result.ProcessedEmails;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing folder {FolderName}: {Message}",
                    folder.FullName, ex.Message);
                result.FailedEmails = result.ProcessedEmails;
            }

            return result;
        }

        /// <summary>
        /// Deletes old emails from the IMAP mailbox based on the account's retention policy.
        /// Only deletes emails that are already archived locally.
        /// </summary>
        private async Task<int> DeleteOldEmailsAsync(MailAccount account, ImapClient client, string? jobId = null)
        {
            if (!account.DeleteAfterDays.HasValue || account.DeleteAfterDays.Value <= 0)
            {
                return 0;
            }

            var deletedCount = 0;
            var cutoffDate = DateTime.UtcNow.AddDays(-account.DeleteAfterDays.Value);

            _logger.LogInformation("Starting deletion of emails older than {Days} days (before {CutoffDate}) for account {AccountName}",
                account.DeleteAfterDays.Value, cutoffDate, account.Name);

            try
            {
                var allFolders = await _folderService.GetAllFoldersAsync(client, account.Name);

                foreach (var folder in allFolders)
                {
                    if (folder == null || string.IsNullOrEmpty(folder.FullName) ||
                        folder.Attributes.HasFlag(FolderAttributes.NonExistent) ||
                        folder.Attributes.HasFlag(FolderAttributes.NoSelect))
                    {
                        _logger.LogInformation("Skipping folder {FolderName} (null, empty name, non-existent or non-selectable) for deletion",
                            folder?.FullName ?? "NULL");
                        continue;
                    }

                    if (account.ExcludedFoldersList.Any(f => f.Equals(folder.FullName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogInformation("Skipping excluded folder for deletion: {FolderName} for account: {AccountName}",
                            folder.FullName, account.Name);
                        continue;
                    }

                    try
                    {
                        if (!client.IsConnected)
                        {
                            _logger.LogWarning("Client disconnected during deletion, attempting to reconnect...");
                            await _connectionFactory.ReconnectClientAsync(client, account);
                        }
                        else if (!client.IsAuthenticated)
                        {
                            _logger.LogWarning("Client not authenticated during deletion, attempting to re-authenticate...");
                            await _connectionFactory.AuthenticateClientAsync(client, account);
                        }

                        if (!folder.IsOpen || folder.Access != FolderAccess.ReadWrite)
                        {
                            await folder.OpenAsync(FolderAccess.ReadWrite);
                        }

                        var uids = await folder.SearchAsync(SearchQuery.SentBefore(cutoffDate));

                        _logger.LogInformation("SearchQuery.SentBefore found {Count} emails in folder {FolderName} for account {AccountName}",
                            uids.Count, folder.FullName, account.Name);

                        if (uids.Any())
                        {
                            var summaries = await folder.FetchAsync(uids.Take(10).ToList(), MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.InternalDate);

                            foreach (var summary in summaries)
                            {
                                DateTime? emailDate = null;

                                if (summary.Envelope?.Date.HasValue == true)
                                {
                                    emailDate = DateTime.SpecifyKind(summary.Envelope.Date.Value.DateTime, DateTimeKind.Utc);
                                }

                                _logger.LogDebug("Email UID: {UniqueId}, Date: {EmailDate}, Cutoff: {CutoffDate}, IsOld: {IsOld}, Subject: {Subject}",
                                    summary.UniqueId, emailDate?.ToString() ?? "NULL", cutoffDate,
                                    emailDate.HasValue && emailDate.Value < cutoffDate,
                                    summary.Envelope?.Subject ?? "NULL");
                            }
                        }

                        if (uids.Count == 0)
                        {
                            _logger.LogInformation("No old emails found in folder {FolderName} for account {AccountName}",
                                folder.FullName, account.Name);
                            continue;
                        }

                        _logger.LogInformation("Found {Count} old emails in folder {FolderName} for account {AccountName}",
                            uids.Count, folder.FullName, account.Name);

                        var batchSize = _batchOptions.BatchSize;
                        for (int i = 0; i < uids.Count; i += batchSize)
                        {
                            var batch = uids.Skip(i).Take(batchSize).ToList();

                            var messageSummaries = await folder.FetchAsync(batch, MessageSummaryItems.UniqueId | MessageSummaryItems.Headers);

                            var uidsToDelete = new List<UniqueId>();

                            foreach (var summary in messageSummaries)
                            {
                                var messageId = summary.Headers["Message-ID"];

                                _logger.LogDebug("Raw Message-ID from IMAP: {RawMessageId}", messageId ?? "NULL");

                                if (string.IsNullOrEmpty(messageId))
                                {
                                    var from = summary.Envelope?.From?.ToString() ?? string.Empty;
                                    var to = summary.Envelope?.To?.ToString() ?? string.Empty;
                                    var subject = summary.Envelope?.Subject ?? string.Empty;
                                    var dateTicks = summary.InternalDate?.Ticks ?? 0;

                                    messageId = $"{from}-{to}-{subject}-{dateTicks}";
                                    _logger.LogDebug("Constructed Message-ID: {ConstructedMessageId}", messageId);
                                }

                                var isArchived = await _context.ArchivedEmails
                                    .AnyAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);

                                if (!isArchived && !string.IsNullOrEmpty(messageId) && !messageId.StartsWith("<"))
                                {
                                    var messageIdWithBrackets = $"<{messageId}>";
                                    isArchived = await _context.ArchivedEmails
                                        .AnyAsync(e => e.MessageId == messageIdWithBrackets && e.MailAccountId == account.Id);

                                    if (isArchived)
                                    {
                                        _logger.LogDebug("Found email with Message-ID {MessageId} when checking with angle brackets", messageIdWithBrackets);
                                    }
                                }
                                else if (!isArchived && !string.IsNullOrEmpty(messageId) && messageId.StartsWith("<") && messageId.EndsWith(">"))
                                {
                                    var messageIdWithoutBrackets = messageId.Substring(1, messageId.Length - 2);
                                    isArchived = await _context.ArchivedEmails
                                        .AnyAsync(e => e.MessageId == messageIdWithoutBrackets && e.MailAccountId == account.Id);

                                    if (isArchived)
                                    {
                                        _logger.LogDebug("Found email with Message-ID {MessageId} when checking without angle brackets", messageIdWithoutBrackets);
                                    }
                                }

                                if (isArchived)
                                {
                                    uidsToDelete.Add(summary.UniqueId);
                                    _logger.LogDebug("Marking email with Message-ID {MessageId} for deletion from folder {FolderName}",
                                        messageId, folder.FullName);
                                }
                                else
                                {
                                    _logger.LogInformation("Skipping deletion of email with Message-ID {MessageId} from folder {FolderName} (not archived). Account ID: {AccountId}",
                                        messageId, folder.FullName, account.Id);
                                }
                            }

                            if (uidsToDelete.Count > 0)
                            {
                                _logger.LogInformation("Attempting to delete {Count} emails from folder {FolderName} for account {AccountName}",
                                    uidsToDelete.Count, folder.FullName, account.Name);

                                try
                                {
                                    await folder.AddFlagsAsync(uidsToDelete, MessageFlags.Deleted, true);
                                    _logger.LogDebug("Added Deleted flag to {Count} emails in folder {FolderName}",
                                        uidsToDelete.Count, folder.FullName);

                                    await folder.ExpungeAsync(uidsToDelete);
                                    _logger.LogDebug("Expunged {Count} emails from folder {FolderName} (with UIDs)",
                                        uidsToDelete.Count, folder.FullName);

                                    deletedCount += uidsToDelete.Count;
                                    _logger.LogInformation("Successfully processed {Count} emails for deletion from folder {FolderName} for account {AccountName}",
                                        uidsToDelete.Count, folder.FullName, account.Name);
                                }
                                catch (Exception deleteEx)
                                {
                                    _logger.LogError(deleteEx, "Error deleting {Count} emails from folder {FolderName} for account {AccountName}",
                                        uidsToDelete.Count, folder.FullName, account.Name);

                                    try
                                    {
                                        _logger.LogInformation("Attempting to reconnect and retry deletion...");
                                        await _connectionFactory.ReconnectClientAsync(client, account);
                                        await folder.OpenAsync(FolderAccess.ReadWrite);

                                        await folder.AddFlagsAsync(uidsToDelete, MessageFlags.Deleted, true);
                                        await folder.ExpungeAsync(uidsToDelete);

                                        deletedCount += uidsToDelete.Count;
                                        _logger.LogInformation("Successfully processed {Count} emails for deletion on retry from folder {FolderName} for account {AccountName}",
                                            uidsToDelete.Count, folder.FullName, account.Name);
                                    }
                                    catch (Exception retryEx)
                                    {
                                        _logger.LogError(retryEx, "Retry deletion also failed for {Count} emails from folder {FolderName} for account {AccountName}",
                                            uidsToDelete.Count, folder.FullName, account.Name);
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogInformation("No archived emails to delete in current batch from folder {FolderName} for account {AccountName}",
                                    folder.FullName, account.Name);
                            }

                            if (i + batchSize < uids.Count && _batchOptions.PauseBetweenBatchesMs > 0)
                            {
                                await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing folder {FolderName} for email deletion for account {AccountName}: {Message}",
                            folder.FullName, account.Name, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email deletion for account {AccountName}: {Message}",
                    account.Name, ex.Message);
            }

            _logger.LogInformation("Completed deletion process for account {AccountName}. Deleted {Count} emails",
                account.Name, deletedCount);

            return deletedCount;
        }

        /// <summary>
        /// Internal result container for folder sync operations.
        /// </summary>
        private class SyncFolderResult
        {
            public int ProcessedEmails { get; set; }
            public int NewEmails { get; set; }
            public int FailedEmails { get; set; }
            public long BytesDownloaded { get; set; }
            public bool WasRateLimited { get; set; }
        }
    }
}