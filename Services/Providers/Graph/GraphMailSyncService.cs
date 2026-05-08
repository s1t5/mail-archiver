using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace MailArchiver.Services.Providers.Graph
{
    /// <summary>
    /// Orchestrates the Microsoft Graph email sync pipeline: folder iteration, message batching,
    /// pagination, memory management, delete-after-days retention, and resync.
    /// </summary>
    public class GraphMailSyncService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<GraphMailSyncService> _logger;
        private readonly ISyncJobService _syncJobService;
        private readonly GraphAuthClientFactory _authFactory;
        private readonly IGraphFolderService _folderService;
        private readonly GraphMailArchiver _archiver;
        private readonly BatchOperationOptions _batchOptions;
        private readonly MailSyncOptions _mailSyncOptions;
        private readonly DateTimeHelper _dateTimeHelper;

        public GraphMailSyncService(
            MailArchiverDbContext context,
            ILogger<GraphMailSyncService> logger,
            ISyncJobService syncJobService,
            GraphAuthClientFactory authFactory,
            IGraphFolderService folderService,
            GraphMailArchiver archiver,
            IOptions<BatchOperationOptions> batchOptions,
            IOptions<MailSyncOptions> mailSyncOptions,
            DateTimeHelper dateTimeHelper)
        {
            _context = context;
            _logger = logger;
            _syncJobService = syncJobService;
            _authFactory = authFactory;
            _folderService = folderService;
            _archiver = archiver;
            _batchOptions = batchOptions.Value;
            _mailSyncOptions = mailSyncOptions.Value;
            _dateTimeHelper = dateTimeHelper;
        }

        /// <summary>
        /// Syncs all emails from the M365 mailbox for the specified account.
        /// </summary>
        public async Task SyncMailAccountAsync(MailAccount account, string? jobId = null)
        {
            _logger.LogInformation("Starting Graph API sync for M365 account: {AccountName}", account.Name);

            try
            {
                var graphClient = _authFactory.CreateGraphClient(account);

                var processedFolders = 0;
                var processedEmails = 0;
                var newEmails = 0;
                var failedEmails = 0;

                var folders = await _folderService.GetAllMailFoldersAsync(graphClient, account.EmailAddress);

                if (jobId != null)
                {
                    _syncJobService.UpdateJobProgress(jobId, job =>
                    {
                        job.TotalFolders = folders.Count;
                    });
                }

                _logger.LogInformation("Found {Count} folders for M365 account: {AccountName}", folders.Count, account.Name);

                var folderPaths = _folderService.BuildFolderPathDictionary(folders);

                foreach (var folder in folders)
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
                        var fullFolderPath = folderPaths.TryGetValue(folder.Id!, out var path) ? path : folder.DisplayName;

                        if (!string.IsNullOrEmpty(folder.DisplayName) &&
                            (account.ExcludedFoldersList.Any(f => f.Equals(fullFolderPath, StringComparison.OrdinalIgnoreCase)) ||
                             account.ExcludedFoldersList.Any(f => f.Equals(folder.DisplayName, StringComparison.OrdinalIgnoreCase))))
                        {
                            _logger.LogInformation("Skipping excluded folder: {FolderName} (full path: {FullPath}) for account: {AccountName}",
                                folder.DisplayName, fullFolderPath, account.Name);
                            processedFolders++;
                            continue;
                        }

                        if (jobId != null)
                        {
                            _syncJobService.UpdateJobProgress(jobId, job =>
                            {
                                job.CurrentFolder = fullFolderPath;
                                job.ProcessedFolders = processedFolders;
                            });
                        }

                        var folderResult = await SyncFolderAsync(graphClient, folder, account, jobId, fullFolderPath);
                        processedEmails += folderResult.ProcessedEmails;
                        newEmails += folderResult.NewEmails;
                        failedEmails += folderResult.FailedEmails;

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
                            folder.DisplayName, account.Name, ex.Message);
                        failedEmails++;
                    }
                }

                // Delete old emails if configured
                var deletedEmails = 0;
                if (account.DeleteAfterDays.HasValue && account.DeleteAfterDays.Value > 0)
                {
                    deletedEmails = await DeleteOldEmailsAsync(account);
                }

                if (failedEmails == 0)
                {
                    var trackedAccount = await _context.MailAccounts.FindAsync(account.Id);
                    if (trackedAccount != null)
                    {
                        trackedAccount.LastSync = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    _logger.LogWarning("Not updating LastSync for account {AccountName} due to {FailedCount} failed emails",
                        account.Name, failedEmails);
                }

                _logger.LogInformation("Graph API sync completed for account: {AccountName}. New: {New}, Failed: {Failed}, Deleted: {Deleted}",
                    account.Name, newEmails, failedEmails, deletedEmails);

                if (jobId != null)
                {
                    _syncJobService.CompleteJob(jobId, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Graph API sync for account {AccountName}: {Message}",
                    account.Name, ex.Message);

                if (jobId != null)
                {
                    _syncJobService.CompleteJob(jobId, false, ex.Message);
                }
                throw;
            }
        }

        /// <summary>
        /// Tests the connection to Microsoft Graph API for the specified account.
        /// </summary>
        public async Task<bool> TestConnectionAsync(MailAccount account)
        {
            try
            {
                _logger.LogInformation("Testing Graph API connection for M365 account {Name} ({Email})",
                    account.Name, account.EmailAddress);

                var graphClient = _authFactory.CreateGraphClient(account);
                var user = await graphClient.Users[account.EmailAddress].GetAsync();

                if (user != null)
                {
                    _logger.LogInformation("Graph API connection test passed for account {Name}. User: {UserPrincipalName}",
                        account.Name, user.UserPrincipalName);

                    try
                    {
                        var foldersResponse = await graphClient.Users[account.EmailAddress].MailFolders.GetAsync((requestConfiguration) =>
                        {
                            requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName" };
                            requestConfiguration.QueryParameters.Top = 1;
                        });

                        _logger.LogInformation("Mail folder access test passed for account {Name}. Found {FolderCount} folders.",
                            account.Name, foldersResponse?.Value?.Count ?? 0);

                        if (foldersResponse?.Value?.Count > 0)
                        {
                            var firstFolder = foldersResponse.Value.First();
                            try
                            {
                                var messagesResponse = await graphClient.Users[account.EmailAddress].MailFolders[firstFolder.Id].Messages.GetAsync((requestConfiguration) =>
                                {
                                    requestConfiguration.QueryParameters.Select = new string[] { "id" };
                                    requestConfiguration.QueryParameters.Top = 1;
                                });

                                _logger.LogInformation("Message access test passed for account {Name} in folder {FolderName}. Found {MessageCount} messages.",
                                    account.Name, firstFolder.DisplayName, messagesResponse?.Value?.Count ?? 0);
                            }
                            catch (Exception msgEx)
                            {
                                _logger.LogWarning(msgEx, "Message access test failed for account {Name} in folder {FolderName}: {Message}",
                                    account.Name, firstFolder.DisplayName, msgEx.Message);
                            }
                        }
                    }
                    catch (Exception folderEx)
                    {
                        _logger.LogWarning(folderEx, "Mail folder access test failed for account {Name}: {Message}. " +
                                             "This may indicate insufficient permissions (Mail.Read or Mail.ReadWrite required).",
                            account.Name, folderEx.Message);
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Graph API connection test failed for account {AccountName}: {Message}",
                    account.Name, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Performs a full resync of an account by resetting LastSync to Unix epoch.
        /// </summary>
        public async Task<bool> ResyncAccountAsync(int accountId)
        {
            try
            {
                var account = await _context.MailAccounts.FindAsync(accountId);
                if (account == null)
                    return false;

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
                _logger.LogError(ex, "Error during resync for Graph API account {AccountId}", accountId);
                return false;
            }
        }

        /// <summary>
        /// Syncs a single mail folder: fetches messages with filter fallback, processes them in batches,
        /// and handles pagination with memory optimization.
        /// </summary>
        private async Task<SyncFolderResult> SyncFolderAsync(
            GraphServiceClient graphClient,
            MailFolder folder,
            MailAccount account,
            string? jobId,
            string? fullFolderPath)
        {
            var result = new SyncFolderResult();
            var folderNameForStorage = fullFolderPath ?? folder.DisplayName;

            _logger.LogInformation("Syncing Graph API folder: {FolderName} (full path: {FullPath}) for account: {AccountName}",
                folder.DisplayName, folderNameForStorage, account.Name);

            try
            {
                bool isOutgoing = _folderService.IsOutgoingFolder(folder);
                var lastSync = account.LastSync;

                if (lastSync != new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                {
                    lastSync = lastSync.AddHours(-12);
                }

                _logger.LogInformation("Syncing folder {FolderName} for account {AccountName} since {LastSync} (UTC)",
                    folder.DisplayName, account.Name, lastSync);

                // Fetch first page of messages with filter fallback
                var messagesResponse = await FetchMessagesWithFallbackAsync(graphClient, account, folder, lastSync);

                int pageNumber = 0;
                int totalMessagesFound = 0;

                // Paginate through all pages
                while (messagesResponse?.Value != null)
                {
                    pageNumber++;
                    var currentPageMessages = messagesResponse.Value;

                    if (currentPageMessages.Count == 0)
                        break;

                    totalMessagesFound += currentPageMessages.Count;
                    result.ProcessedEmails += currentPageMessages.Count;

                    _logger.LogInformation("Processing page {PageNumber} with {Count} messages in folder {FolderName} (Total found so far: {TotalFound})",
                        pageNumber, currentPageMessages.Count, folder.DisplayName, totalMessagesFound);

                    await ProcessMessagePageAsync(graphClient, account, folder, currentPageMessages, lastSync,
                        folderNameForStorage, isOutgoing, jobId, result, pageNumber);

                    // Check for cancellation
                    if (jobId != null)
                    {
                        var job = _syncJobService.GetJob(jobId);
                        if (job?.Status == SyncJobStatus.Cancelled)
                        {
                            _logger.LogInformation("Sync job {JobId} for account {AccountName} has been cancelled during folder sync", jobId, account.Name);
                            return result;
                        }
                    }

                    // Follow OData nextLink for pagination
                    if (!string.IsNullOrEmpty(messagesResponse.OdataNextLink))
                    {
                        if (_batchOptions.PauseBetweenBatchesMs > 0)
                        {
                            await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                        }

                        _logger.LogDebug("Fetching next page of messages for folder {FolderName}...", folder.DisplayName);
                        messagesResponse = await graphClient.Users[account.EmailAddress]
                            .MailFolders[folder.Id]
                            .Messages
                            .WithUrl(messagesResponse.OdataNextLink)
                            .GetAsync();
                    }
                    else
                    {
                        break;
                    }
                }

                _logger.LogInformation("Completed syncing folder {FolderName}. Total pages: {TotalPages}, Total messages: {TotalMessages}",
                    folder.DisplayName, pageNumber, totalMessagesFound);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing Graph API folder {FolderName}: {Message}",
                    folder.DisplayName, ex.Message);
                result.FailedEmails = result.ProcessedEmails;
            }

            return result;
        }

        /// <summary>
        /// Fetches messages from a folder with a progressive fallback strategy for OData filter complexity errors.
        /// </summary>
        private async Task<MessageCollectionResponse?> FetchMessagesWithFallbackAsync(
            GraphServiceClient graphClient,
            MailAccount account,
            MailFolder folder,
            DateTime lastSync)
        {
            // Use receivedDateTime instead of lastModifiedDateTime to mirror the IMAP sync behavior.
            // lastModifiedDateTime is touched by Exchange Online for many background operations
            // (re-indexing, flag changes, moves, …) and would cause the full mailbox to be
            // re-fetched on every sync. receivedDateTime only changes when a new message arrives,
            // matching the IMAP DeliveredAfter semantics.
            var filter = $"receivedDateTime ge {lastSync:yyyy-MM-ddTHH:mm:ssZ}";

            try
            {
                // Attempt 1: Full select with filter
                _logger.LogInformation("Attempting Graph API query with filter for folder {FolderName}: {Filter}",
                    folder.DisplayName, filter);

                var response = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = filter;
                    requestConfiguration.QueryParameters.Select = new string[]
                    {
                        "id", "internetMessageId", "subject", "from", "toRecipients", "ccRecipients", "bccRecipients",
                        "sentDateTime", "receivedDateTime", "hasAttachments", "body", "bodyPreview", "lastModifiedDateTime"
                    };
                    requestConfiguration.QueryParameters.Top = _batchOptions.BatchSize;
                });

                _logger.LogInformation("Graph API response for folder {FolderName}: {MessageCount} messages returned (filter attempt), has nextLink: {HasNextLink}",
                    folder.DisplayName, response?.Value?.Count ?? 0, !string.IsNullOrEmpty(response?.OdataNextLink));

                // NOTE: Previously, when the filter returned 0 messages we ran a "permissive
                // fallback" that re-queried the last 30 days. That caused the entire mailbox
                // (or at least the last 30 days) to be re-fetched on every incremental sync,
                // because 0 results is the *expected* outcome of an incremental sync that
                // simply has no new mail. The fallback has been removed – 0 results is a valid
                // and final answer here.
                return response;
            }
            catch (ODataError ex) when (ex.Error?.Code == "ErrorInvalidRestriction" || ex.Message.Contains("too complex"))
            {
                _logger.LogWarning("Complex filter failed for folder {FolderName}, trying simpler approach: {Error}",
                    folder.DisplayName, ex.Message);

                try
                {
                    // Attempt 2: Reduced select fields with filter
                    var response = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Filter = filter;
                        requestConfiguration.QueryParameters.Select = new string[]
                        {
                            "id", "internetMessageId", "subject", "from", "sentDateTime", "receivedDateTime", "lastModifiedDateTime"
                        };
                        requestConfiguration.QueryParameters.Top = _batchOptions.BatchSize;
                    });

                    _logger.LogInformation("Second attempt returned {Count} messages for folder {FolderName}, has nextLink: {HasNextLink}",
                        response?.Value?.Count ?? 0, folder.DisplayName, !string.IsNullOrEmpty(response?.OdataNextLink));

                    return response;
                }
                catch (ODataError ex2) when (ex2.Error?.Code == "ErrorInvalidRestriction" || ex2.Message.Contains("too complex"))
                {
                    _logger.LogWarning("Filtered query still too complex for folder {FolderName}, falling back to basic query: {Error}",
                        folder.DisplayName, ex2.Message);

                    // Attempt 3: No filter
                    var response = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Select = new string[]
                        {
                            "id", "internetMessageId", "subject", "from", "sentDateTime", "receivedDateTime", "lastModifiedDateTime"
                        };
                        requestConfiguration.QueryParameters.Top = _batchOptions.BatchSize;
                    });

                    _logger.LogDebug("Third attempt (basic query) succeeded for folder {FolderName}, has nextLink: {HasNextLink}",
                        folder.DisplayName, !string.IsNullOrEmpty(response?.OdataNextLink));

                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Graph API query for folder {FolderName}: {Error}",
                    folder.DisplayName, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Filters messages by lastModifiedDateTime client-side if server-side filtering wasn't possible.
        /// </summary>
        private static List<Message>? FilterMessagesByDate(MessageCollectionResponse? response, DateTime lastSync)
        {
            if (response?.Value == null)
                return null;

            var filtered = response.Value
                .Where(m => m.LastModifiedDateTime >= lastSync)
                .ToList();

            return filtered;
        }

        /// <summary>
        /// Processes a page of messages: enriches with full details, archives each message, and manages memory.
        /// </summary>
        private async Task ProcessMessagePageAsync(
            GraphServiceClient graphClient,
            MailAccount account,
            MailFolder folder,
            List<Message> messages,
            DateTime lastSync,
            string folderNameForStorage,
            bool isOutgoing,
            string? jobId,
            SyncFolderResult result,
            int pageNumber)
        {
            _logger.LogInformation("Processing page {PageNumber} with {Count} messages in folder {FolderName} for account: {AccountName}",
                pageNumber, messages.Count, folder.DisplayName, account.Name);

            int processedInBatch = 0;

            for (int i = 0; i < messages.Count; i++)
            {
                if (jobId != null)
                {
                    var job = _syncJobService.GetJob(jobId);
                    if (job?.Status == SyncJobStatus.Cancelled)
                    {
                        _logger.LogInformation("Sync job {JobId} for account {AccountName} has been cancelled during message processing",
                            jobId, account.Name);
                        return;
                    }
                }

                try
                {
                    var message = messages[i];

                    // Enrich with full details if needed
                    Message fullMessage = message;
                    if (message.Body?.Content == null || message.ToRecipients == null || message.CcRecipients == null)
                    {
                        try
                        {
                            fullMessage = await graphClient.Users[account.EmailAddress].Messages[message.Id].GetAsync((requestConfiguration) =>
                            {
                                requestConfiguration.QueryParameters.Select = new string[]
                                {
                                    "id", "internetMessageId", "subject", "from", "toRecipients", "ccRecipients", "bccRecipients",
                                    "sentDateTime", "receivedDateTime", "hasAttachments", "body", "bodyPreview", "lastModifiedDateTime"
                                };
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to get full message details for {MessageId}, using limited data", message.Id);
                        }
                    }

                    var isNew = await _archiver.ArchiveGraphEmailAsync(graphClient, account, fullMessage, isOutgoing, folderNameForStorage);
                    if (isNew)
                        result.NewEmails++;

                    processedInBatch++;
                }
                catch (Exception ex)
                {
                    var subject = messages[i].Subject ?? "Unknown";
                    var date = messages[i].SentDateTime?.ToString() ?? "Unknown";
                    _logger.LogError(ex, "Error archiving Graph API message {MessageId} from folder {FolderName}. Subject: {Subject}, Date: {Date}, Message: {Message}",
                        messages[i].Id, folderNameForStorage, subject, date, ex.Message);
                    result.FailedEmails++;
                    processedInBatch++;
                }

                // Memory cleanup after each message
                if (processedInBatch % 10 == 0)
                {
                    _context.ChangeTracker.Clear();
                }
            }

            _logger.LogInformation("Memory usage after processing page {PageNumber}: {MemoryUsage}",
                pageNumber, MemoryMonitor.GetMemoryUsageFormatted());
        }

        /// <summary>
        /// Deletes old emails from the M365 mailbox based on the account's retention policy.
        /// </summary>
        private async Task<int> DeleteOldEmailsAsync(MailAccount account)
        {
            if (!account.DeleteAfterDays.HasValue || account.DeleteAfterDays.Value <= 0)
                return 0;

            var deletedCount = 0;
            var cutoffDate = DateTime.UtcNow.AddDays(-account.DeleteAfterDays.Value);

            _logger.LogInformation("Starting deletion of emails older than {Days} days (before {CutoffDate}) for M365 account {AccountName}",
                account.DeleteAfterDays.Value, cutoffDate, account.Name);

            try
            {
                var graphClient = _authFactory.CreateGraphClient(account);
                var folders = await _folderService.GetAllMailFoldersAsync(graphClient, account.EmailAddress);

                _logger.LogInformation("Found {Count} folders for M365 account: {AccountName}", folders.Count, account.Name);

                foreach (var folder in folders)
                {
                    if (account.ExcludedFoldersList.Any(f => f.Equals(folder.DisplayName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogInformation("Skipping excluded folder for deletion: {FolderName} for account: {AccountName}",
                            folder.DisplayName, account.Name);
                        continue;
                    }

                    try
                    {
                        _logger.LogInformation("Processing folder {FolderName} for email deletion for account: {AccountName}",
                            folder.DisplayName, account.Name);

                        var filter = $"receivedDateTime lt {cutoffDate:yyyy-MM-ddTHH:mm:ssZ}";

                        var messagesResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                        {
                            requestConfiguration.QueryParameters.Filter = filter;
                            requestConfiguration.QueryParameters.Select = new string[] { "id", "internetMessageId", "subject", "receivedDateTime" };
                            requestConfiguration.QueryParameters.Top = _batchOptions.BatchSize;
                        });

                        int totalOldEmailsFound = 0;
                        int totalProcessedInFolder = 0;
                        int paginationCount = 0;

                        while (messagesResponse?.Value != null)
                        {
                            paginationCount++;
                            var currentPageSize = messagesResponse.Value.Count;
                            totalOldEmailsFound += currentPageSize;

                            _logger.LogInformation("Processing page {PageNumber} with {Count} old emails in folder {FolderName} for account {AccountName} (Total found so far: {TotalFound})",
                                paginationCount, currentPageSize, folder.DisplayName, account.Name, totalOldEmailsFound);

                            if (currentPageSize > 0)
                            {
                                var messageIdsToDelete = new List<string>();

                                foreach (var message in messagesResponse.Value)
                                {
                                    var messageId = message.InternetMessageId ?? message.Id;

                                    var archivedEmail = await _context.ArchivedEmails
                                        .Where(e => e.MailAccountId == account.Id)
                                        .Where(e => e.MessageId == messageId)
                                        .FirstOrDefaultAsync();

                                    if (archivedEmail == null && !string.IsNullOrEmpty(messageId) && !messageId.StartsWith("<"))
                                    {
                                        var messageIdWithBrackets = $"<{messageId}>";
                                        archivedEmail = await _context.ArchivedEmails
                                            .Where(e => e.MailAccountId == account.Id)
                                            .Where(e => e.MessageId == messageIdWithBrackets)
                                            .FirstOrDefaultAsync();
                                    }
                                    else if (archivedEmail == null && !string.IsNullOrEmpty(messageId) && messageId.StartsWith("<") && messageId.EndsWith(">"))
                                    {
                                        var messageIdWithoutBrackets = messageId.Substring(1, messageId.Length - 2);
                                        archivedEmail = await _context.ArchivedEmails
                                            .Where(e => e.MailAccountId == account.Id)
                                            .Where(e => e.MessageId == messageIdWithoutBrackets)
                                            .FirstOrDefaultAsync();
                                    }

                                    if (archivedEmail != null)
                                    {
                                        messageIdsToDelete.Add(message.Id!);
                                            _logger.LogDebug("Marking email with Message-ID {MessageId} for deletion from folder {FolderName}",
                                                messageId, folder.DisplayName);
                                    }
                                    else
                                    {
                                        _logger.LogDebug("Skipping deletion of email with Message-ID {MessageId} from folder {FolderName} (not archived).",
                                            messageId, folder.DisplayName);
                                    }
                                }

                                foreach (var msgId in messageIdsToDelete)
                                {
                                    try
                                    {
                                        await graphClient.Users[account.EmailAddress].Messages[msgId].DeleteAsync();
                                        deletedCount++;
                                        totalProcessedInFolder++;
                                        _logger.LogDebug("Successfully deleted email {MessageId} from folder {FolderName}", msgId, folder.DisplayName);
                                    }
                                    catch (Exception deleteEx)
                                    {
                                        _logger.LogError(deleteEx, "Error deleting email {MessageId} from folder {FolderName} for account {AccountName}",
                                            msgId, folder.DisplayName, account.Name);
                                    }
                                }

                                _logger.LogInformation("Successfully processed {Count} emails for deletion from page {PageNumber} in folder {FolderName}",
                                    messageIdsToDelete.Count, paginationCount, folder.DisplayName);
                            }

                            if (!string.IsNullOrEmpty(messagesResponse.OdataNextLink))
                            {
                                if (_batchOptions.PauseBetweenBatchesMs > 0)
                                {
                                    await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                                }
                                messagesResponse = await graphClient.Users[account.EmailAddress]
                                    .MailFolders[folder.Id]
                                    .Messages
                                    .WithUrl(messagesResponse.OdataNextLink)
                                    .GetAsync();
                            }
                            else
                            {
                                break;
                            }
                        }

                        _logger.LogInformation("Completed deletion processing for folder {FolderName}. Total found: {TotalFound}, Pages: {PagesProcessed}, Deleted: {DeletedInFolder}",
                            folder.DisplayName, totalOldEmailsFound, paginationCount, totalProcessedInFolder);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing folder {FolderName} for email deletion for account {AccountName}: {Message}",
                            folder.DisplayName, account.Name, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email deletion for M365 account {AccountName}: {Message}",
                    account.Name, ex.Message);
            }

            _logger.LogInformation("Completed deletion process for M365 account {AccountName}. Deleted {Count} emails",
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
        }
    }
}