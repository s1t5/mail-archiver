using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Me.MailFolders.Item.Messages;
using System.Text;
using System.Text.Json;
using MimeKit;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Microsoft.Kiota.Abstractions;

namespace MailArchiver.Services
{
    /// <summary>
    /// Service for Microsoft Graph email operations for M365 accounts
    /// </summary>
    public class GraphEmailService : IGraphEmailService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<GraphEmailService> _logger;
        private readonly ISyncJobService _syncJobService;
        private readonly BatchOperationOptions _batchOptions;
        private readonly MailSyncOptions _mailSyncOptions;

        public GraphEmailService(
            MailArchiverDbContext context,
            ILogger<GraphEmailService> logger,
            ISyncJobService syncJobService,
            IOptions<BatchOperationOptions> batchOptions,
            IOptions<MailSyncOptions> mailSyncOptions)
        {
            _context = context;
            _logger = logger;
            _syncJobService = syncJobService;
            _batchOptions = batchOptions.Value;
            _mailSyncOptions = mailSyncOptions.Value;
        }

        /// <summary>
        /// Creates a GraphServiceClient for the specified M365 account using client credentials flow
        /// </summary>
        /// <param name="account">The M365 mail account</param>
        /// <returns>Configured GraphServiceClient</returns>
        private async Task<GraphServiceClient> CreateGraphClientAsync(MailAccount account)
        {
            if (string.IsNullOrEmpty(account.ClientId) || string.IsNullOrEmpty(account.ClientSecret))
            {
                throw new InvalidOperationException($"M365 account '{account.Name}' requires ClientId and ClientSecret for OAuth authentication");
            }

            var accessToken = await GetAccessTokenAsync(account);

            // Create GraphServiceClient with the access token using a custom auth provider
            var authProvider = new TokenAuthenticationProvider(accessToken);
            var requestAdapter = new HttpClientRequestAdapter(authProvider);
            var graphServiceClient = new GraphServiceClient(requestAdapter);

            return graphServiceClient;
        }

        /// <summary>
        /// Gets an OAuth access token for Microsoft Graph API
        /// </summary>
        /// <param name="account">The M365 mail account</param>
        /// <returns>Access token string</returns>
        private async Task<string> GetAccessTokenAsync(MailAccount account)
        {
            try
            {
                string tenantId = !string.IsNullOrEmpty(account.TenantId) ? account.TenantId : "common";

                _logger.LogDebug("Getting Graph API access token for M365 account: {AccountName} with tenant: {TenantId}", account.Name, tenantId);

                var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

                var requestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", account.ClientId),
                    new KeyValuePair<string, string>("client_secret", account.ClientSecret),
                    new KeyValuePair<string, string>("scope", "https://graph.microsoft.com/.default"),
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(60);

                var response = await httpClient.PostAsync(tokenEndpoint, requestBody);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get Graph API access token for M365 account {AccountName}. Status: {StatusCode}, Response: {Response}",
                        account.Name, response.StatusCode, responseContent);
                    throw new InvalidOperationException($"Failed to get Graph API access token: {response.StatusCode} - {responseContent}");
                }

                var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (!tokenResponse.TryGetProperty("access_token", out var accessTokenElement))
                {
                    throw new InvalidOperationException("Graph API OAuth response does not contain access_token");
                }

                var accessToken = accessTokenElement.GetString();

                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new InvalidOperationException("Received empty access token from Microsoft Graph API");
                }

                _logger.LogDebug("Successfully obtained Graph API access token for M365 account: {AccountName}", account.Name);
                return accessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Graph API access token for M365 account {AccountName}: {Message}", account.Name, ex.Message);
                throw;
            }
        }

        public async Task SyncMailAccountAsync(MailAccount account, string? jobId = null)
        {
            _logger.LogInformation("Starting Graph API sync for M365 account: {AccountName}", account.Name);

            try
            {
                var graphClient = await CreateGraphClientAsync(account);
                
                var processedFolders = 0;
                var processedEmails = 0;
                var newEmails = 0;
                var failedEmails = 0;

                // Get all mail folders
                var folders = await GetAllMailFoldersAsync(graphClient, account.EmailAddress);

                if (jobId != null)
                {
                    _syncJobService.UpdateJobProgress(jobId, job =>
                    {
                        job.TotalFolders = folders.Count;
                    });
                }

                _logger.LogInformation("Found {Count} folders for M365 account: {AccountName}", folders.Count, account.Name);

                // Process each folder
                foreach (var folder in folders)
                {
                    // Check if job has been cancelled
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
                        // Skip excluded folders
                        if (account.ExcludedFoldersList.Contains(folder.DisplayName))
                        {
                            _logger.LogInformation("Skipping excluded folder: {FolderName} for account: {AccountName}",
                                folder.DisplayName, account.Name);
                            processedFolders++;
                            continue;
                        }

                        if (jobId != null)
                        {
                            _syncJobService.UpdateJobProgress(jobId, job =>
                            {
                                job.CurrentFolder = folder.DisplayName;
                                job.ProcessedFolders = processedFolders;
                            });
                        }

                        var folderResult = await SyncFolderAsync(graphClient, folder, account, jobId);
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

                // Update lastSync only if no individual email failed
                if (failedEmails == 0)
                {
                    account.LastSync = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
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

        private async Task<List<MailFolder>> GetAllMailFoldersAsync(GraphServiceClient graphClient, string userPrincipalName)
        {
            var folders = new List<MailFolder>();

            try
            {
                // Get mail folders from the user's mailbox using application permissions
                var response = await graphClient.Users[userPrincipalName].MailFolders.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName", "parentFolderId", "childFolderCount" };
                    requestConfiguration.QueryParameters.Top = int.MaxValue; // Removed folder limit
                });

                if (response?.Value != null)
                {
                    folders.AddRange(response.Value);

                    // Get child folders recursively
                    foreach (var folder in response.Value.ToList())
                    {
                        if (folder.ChildFolderCount > 0)
                        {
                            var childFolders = await GetChildFoldersAsync(graphClient, userPrincipalName, folder.Id);
                            folders.AddRange(childFolders);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mail folders from Graph API: {Message}", ex.Message);
                throw;
            }

            return folders;
        }

        private async Task<List<MailFolder>> GetChildFoldersAsync(GraphServiceClient graphClient, string userPrincipalName, string parentFolderId)
        {
            var childFolders = new List<MailFolder>();

            try
            {
                var response = await graphClient.Users[userPrincipalName].MailFolders[parentFolderId].ChildFolders.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName", "parentFolderId", "childFolderCount" };
                    requestConfiguration.QueryParameters.Top = int.MaxValue; // Removed folder limit
                });

                if (response?.Value != null)
                {
                    childFolders.AddRange(response.Value);

                    // Recursively get child folders
                    foreach (var folder in response.Value.ToList())
                    {
                        if (folder.ChildFolderCount > 0)
                        {
                            var grandChildFolders = await GetChildFoldersAsync(graphClient, userPrincipalName, folder.Id);
                            childFolders.AddRange(grandChildFolders);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting child folders for parent {ParentFolderId}: {Message}", parentFolderId, ex.Message);
            }

            return childFolders;
        }

        private async Task<SyncFolderResult> SyncFolderAsync(GraphServiceClient graphClient, MailFolder folder, MailAccount account, string? jobId = null)
        {
            var result = new SyncFolderResult();

            _logger.LogInformation("Syncing Graph API folder: {FolderName} for account: {AccountName}",
                folder.DisplayName, account.Name);

            try
            {
                bool isOutgoing = IsOutgoingFolder(folder);
                var lastSync = account.LastSync;

                // Safety check: If lastSync is too old or null, use a reasonable default
                bool isFirstSync = false;
                if (lastSync == null || lastSync < DateTime.UtcNow.AddYears(-1))
                {
                    lastSync = DateTime.UtcNow.AddDays(-365); // Start with 1 year back for initial sync
                    isFirstSync = true;
                    _logger.LogInformation("LastSync is null or too old for account {AccountName}, using {DefaultDate} as starting point (first sync)", 
                        account.Name, lastSync);
                }
                else if (lastSync > DateTime.UtcNow.AddDays(-1))
                {
                    // If lastSync is very recent (within 24 hours), extend it back to capture more emails
                    lastSync = DateTime.UtcNow.AddDays(-7);
                    isFirstSync = true;
                    _logger.LogInformation("LastSync is too recent for account {AccountName}, extending back to {DefaultDate} to capture more emails", 
                        account.Name, lastSync);
                }
                
                // For debugging: Log the filter criteria being used
                _logger.LogInformation("Syncing folder {FolderName} for account {AccountName} since {LastSync} (UTC)", 
                    folder.DisplayName, account.Name, lastSync);

                _logger.LogDebug("Syncing folder {FolderName} for account {AccountName} since {LastSync}", 
                    folder.DisplayName, account.Name, lastSync);

                // Try different approaches to avoid "too complex" errors
                Microsoft.Graph.Models.MessageCollectionResponse? messagesResponse = null;
                int maxMessagesPerFolder = int.MaxValue; // Removed limit
                int processedInThisFolder = 0;
                
                try
                {
                    // First attempt: Simple filter without orderby
                    var filter = $"lastModifiedDateTime ge {lastSync:yyyy-MM-ddTHH:mm:ssZ}";
                    _logger.LogInformation("Attempting Graph API query with filter for folder {FolderName}: {Filter}", 
                        folder.DisplayName, filter);
                    
                    messagesResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                    {
                        requestConfiguration.QueryParameters.Filter = filter;
                        requestConfiguration.QueryParameters.Select = new string[] 
                        { 
                            "id", "internetMessageId", "subject", "from", "toRecipients", "ccRecipients", "bccRecipients",
                            "sentDateTime", "receivedDateTime", "hasAttachments", "body", "bodyPreview", "lastModifiedDateTime"
                        };
                        requestConfiguration.QueryParameters.Top = _batchOptions.BatchSize;
                    });
                    
                    _logger.LogInformation("Graph API response for folder {FolderName}: {MessageCount} messages returned (filter attempt)", 
                        folder.DisplayName, messagesResponse?.Value?.Count ?? 0);
                    
                    if (messagesResponse?.Value?.Count == 0)
                    {
                        // This is a normal case when there are no new messages since last sync
                        // Only do additional checks if we have reason to believe there might be an issue
                        _logger.LogDebug("No messages returned with date filter for folder {FolderName}. This is normal if there are no new messages.", folder.DisplayName);
                        
                        // For first sync or when we suspect an issue with the date filter, check if there are any messages at all
                        if (isFirstSync)
                        {
                            _logger.LogDebug("First sync detected for folder {FolderName}. Checking if any messages exist in folder.", folder.DisplayName);
                            
                            // Try without filter to see if there are ANY messages
                            var testResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                            {
                                requestConfiguration.QueryParameters.Select = new string[] { "id", "lastModifiedDateTime" };
                                requestConfiguration.QueryParameters.Top = 1;
                            });
                            
                            if (testResponse?.Value?.Count > 0)
                            {
                                _logger.LogInformation("Folder {FolderName} contains messages but none match date filter. Using fallback query for first sync.", folder.DisplayName);
                                
                                // For first sync, get messages without date filter
                                int fallbackLimit = int.MaxValue;
                                messagesResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                                {
                                    requestConfiguration.QueryParameters.Select = new string[] 
                                    { 
                                        "id", "internetMessageId", "subject", "from", "toRecipients", "ccRecipients", "bccRecipients",
                                        "sentDateTime", "receivedDateTime", "hasAttachments", "body", "bodyPreview", "lastModifiedDateTime"
                                    };
                                    requestConfiguration.QueryParameters.Top = fallbackLimit;
                                });
                                
                                _logger.LogInformation("Fallback query without filter returned {Count} messages for folder {FolderName} (limit: {Limit})", 
                                    messagesResponse?.Value?.Count ?? 0, folder.DisplayName, fallbackLimit);
                            }
                            else
                            {
                                _logger.LogInformation("Folder {FolderName} contains no messages at all.", folder.DisplayName);
                            }
                        }
                    }
                }
                catch (ODataError ex) when (ex.Error?.Code == "ErrorInvalidRestriction" || ex.Message.Contains("too complex"))
                {
                    _logger.LogWarning("Complex filter failed for folder {FolderName}, trying simpler approach: {Error}", 
                        folder.DisplayName, ex.Message);
                    
                    try
                    {
                        // Second attempt: Reduced select fields with filter
                        var filter = $"lastModifiedDateTime ge {lastSync:yyyy-MM-ddTHH:mm:ssZ}";
                        messagesResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                        {
                            requestConfiguration.QueryParameters.Filter = filter;
                            requestConfiguration.QueryParameters.Select = new string[] 
                            { 
                                "id", "internetMessageId", "subject", "from", "sentDateTime", "receivedDateTime", "lastModifiedDateTime"
                            };
                            requestConfiguration.QueryParameters.Top = _batchOptions.BatchSize;
                        });
                        
                        _logger.LogInformation("Second attempt returned {Count} messages for folder {FolderName}", 
                            messagesResponse?.Value?.Count ?? 0, folder.DisplayName);
                    }
                    catch (ODataError ex2) when (ex2.Error?.Code == "ErrorInvalidRestriction" || ex2.Message.Contains("too complex"))
                    {
                        _logger.LogWarning("Filtered query still too complex for folder {FolderName}, falling back to basic query: {Error}", 
                            folder.DisplayName, ex2.Message);
                        
                        try
                        {
                            // Third attempt: No filter, no orderby - simplest possible query
                            messagesResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                            {
                                requestConfiguration.QueryParameters.Select = new string[] 
                                { 
                                    "id", "internetMessageId", "subject", "from", "sentDateTime", "receivedDateTime", "lastModifiedDateTime"
                                };
                                requestConfiguration.QueryParameters.Top = _batchOptions.BatchSize;
                            });
                            
                            _logger.LogDebug("Third attempt (basic query) succeeded for folder {FolderName}", folder.DisplayName);
                        }
                        catch (Exception ex3)
                        {
                            _logger.LogError(ex3, "All query attempts failed for folder {FolderName}: {Error}", 
                                folder.DisplayName, ex3.Message);
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during Graph API query for folder {FolderName}: {Error}", 
                        folder.DisplayName, ex.Message);
                    throw;
                }

                if (messagesResponse?.Value != null)
                {
                    // Filter messages by lastModifiedDateTime if we couldn't filter on the server
                    // For first sync or fallback queries, be more lenient with filtering
                    var filteredMessages = messagesResponse.Value.ToList();
                    
                    if (!isFirstSync)
                    {
                        // Only apply strict filtering for subsequent syncs
                        filteredMessages = messagesResponse.Value
                            .Where(m => m.LastModifiedDateTime >= lastSync)
                            .ToList();
                            
                        _logger.LogInformation("Applying strict date filter for subsequent sync. Before filter: {TotalCount}, After filter: {FilteredCount}", 
                            messagesResponse.Value.Count, filteredMessages.Count);
                    }
                    else
                    {
                        _logger.LogInformation("First sync detected - processing all messages without strict date filtering. Total messages: {Count}", 
                            filteredMessages.Count);
                    }

                    result.ProcessedEmails = filteredMessages.Count;
                    _logger.LogInformation("Found {Count} new messages in Graph API folder {FolderName} for account: {AccountName}",
                        filteredMessages.Count, folder.DisplayName, account.Name);

                    foreach (var message in filteredMessages)
                    {
                        // Safety check: Limit messages per folder to prevent infinite loops
                        if (processedInThisFolder >= maxMessagesPerFolder)
                        {
                            _logger.LogWarning("Reached maximum message limit ({MaxMessages}) for folder {FolderName}, stopping processing for safety", 
                                maxMessagesPerFolder, folder.DisplayName);
                            break;
                        }

                        // Check if job has been cancelled
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
                            // For messages with limited fields, we need to get the full message details
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
                                    fullMessage = message; // Use what we have
                                }
                            }

                            var isNew = await ArchiveGraphEmailAsync(graphClient, account, fullMessage, isOutgoing, folder.DisplayName);
                            if (isNew)
                            {
                                result.NewEmails++;
                            }
                            
                            processedInThisFolder++;
                        }
                        catch (Exception ex)
                        {
                            var subject = message.Subject ?? "Unknown";
                            var date = message.SentDateTime?.ToString() ?? "Unknown";
                            _logger.LogError(ex, "Error archiving Graph API message {MessageId} from folder {FolderName}. Subject: {Subject}, Date: {Date}, Message: {Message}",
                                message.Id, folder.DisplayName, subject, date, ex.Message);
                            result.FailedEmails++;
                            processedInThisFolder++;
                        }
                    }

                    // Handle pagination if there are more messages
                    int paginationCount = 0;
                    int maxPaginationPages = int.MaxValue; // Removed pagination limit
                    
                    while (!string.IsNullOrEmpty(messagesResponse.OdataNextLink) && paginationCount < maxPaginationPages)
                    {
                        paginationCount++;
                        
                        // Safety check: Stop if we've processed too many messages already
                        if (processedInThisFolder >= maxMessagesPerFolder)
                        {
                            _logger.LogWarning("Reached maximum message limit during pagination for folder {FolderName}, stopping", folder.DisplayName);
                            break;
                        }

                        // Check if job has been cancelled
                        if (jobId != null)
                        {
                            var job = _syncJobService.GetJob(jobId);
                            if (job?.Status == SyncJobStatus.Cancelled)
                            {
                                _logger.LogInformation("Sync job {JobId} for account {AccountName} has been cancelled during pagination", jobId, account.Name);
                                return result;
                            }
                        }

                        // Add a pause between batches to avoid overwhelming the API
                        if (_batchOptions.PauseBetweenBatchesMs > 0)
                        {
                            await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                        }

                        _logger.LogDebug("Processing pagination page {PageNumber} for folder {FolderName}", paginationCount, folder.DisplayName);

                        // Get next page
                        messagesResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.WithUrl(messagesResponse.OdataNextLink).GetAsync();

                        if (messagesResponse?.Value != null)
                        {
                            // Apply the same filtering logic for pagination results as for the main query
                            var filteredPaginationMessages = messagesResponse.Value.ToList();
                            
                            if (!isFirstSync)
                            {
                                // Only apply strict filtering for subsequent syncs
                                filteredPaginationMessages = messagesResponse.Value
                                    .Where(m => m.LastModifiedDateTime >= lastSync)
                                    .ToList();
                            }

                            result.ProcessedEmails += filteredPaginationMessages.Count;

                            foreach (var message in filteredPaginationMessages)
                            {
                                // Safety check again during pagination
                                if (processedInThisFolder >= maxMessagesPerFolder)
                                {
                                    _logger.LogWarning("Reached maximum message limit during pagination message processing for folder {FolderName}", folder.DisplayName);
                                    break;
                                }

                                try
                                {
                                    // For messages with limited fields, we need to get the full message details
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
                                            _logger.LogWarning(ex, "Failed to get full message details for {MessageId} in pagination, using limited data", message.Id);
                                            fullMessage = message; // Use what we have
                                        }
                                    }

                                    var isNew = await ArchiveGraphEmailAsync(graphClient, account, fullMessage, isOutgoing, folder.DisplayName);
                                    if (isNew)
                                    {
                                        result.NewEmails++;
                                    }
                                    
                                    processedInThisFolder++;
                                }
                                catch (Exception ex)
                                {
                                    var subject = message.Subject ?? "Unknown";
                                    var date = message.SentDateTime?.ToString() ?? "Unknown";
                                    _logger.LogError(ex, "Error archiving Graph API message {MessageId} from folder {FolderName}. Subject: {Subject}, Date: {Date}, Message: {Message}",
                                        message.Id, folder.DisplayName, subject, date, ex.Message);
                                    result.FailedEmails++;
                                    processedInThisFolder++;
                                }
                            }
                        }
                    }

                    if (paginationCount >= maxPaginationPages)
                    {
                        _logger.LogWarning("Reached maximum pagination limit ({MaxPages}) for folder {FolderName}, stopping for safety", 
                            maxPaginationPages, folder.DisplayName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing Graph API folder {FolderName}: {Message}",
                    folder.DisplayName, ex.Message);
                result.FailedEmails = result.ProcessedEmails;
            }

            return result;
        }

        private async Task<bool> ArchiveGraphEmailAsync(GraphServiceClient graphClient, MailAccount account, Message message, bool isOutgoing, string folderName)
        {
            // Check if this email is already archived
            var messageId = message.InternetMessageId ?? message.Id;
            
            _logger.LogDebug("Processing message {MessageId} for account {AccountName}, Subject: {Subject}", 
                messageId, account.Name, message.Subject ?? "No Subject");

            try
            {
                var emailExists = await _context.ArchivedEmails
                    .AnyAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);

                if (emailExists)
                {
                    _logger.LogDebug("Email {MessageId} already exists in database for account {AccountName}", 
                        messageId, account.Name);
                    return false; // Email already exists
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if email {MessageId} exists for account {AccountName}: {Message}", 
                    messageId, account.Name, ex.Message);
                return false;
            }

            try
            {
                _logger.LogDebug("Archiving new email {MessageId} for account {AccountName}", messageId, account.Name);
                
                DateTime sentDate = message.SentDateTime?.DateTime ?? DateTime.UtcNow;
                if (sentDate.Kind != DateTimeKind.Utc)
                {
                    sentDate = DateTime.SpecifyKind(sentDate, DateTimeKind.Utc);
                }

                var subject = CleanText(message.Subject ?? "(No Subject)");
                var from = CleanText(message.From?.EmailAddress?.Address ?? string.Empty);
                var to = CleanText(string.Join(", ", message.ToRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>()));
                var cc = CleanText(string.Join(", ", message.CcRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>()));
                var bcc = CleanText(string.Join(", ", message.BccRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>()));

                // Extract body content
                var body = string.Empty;
                var htmlBody = string.Empty;
                var isHtmlTruncated = false;
                var isBodyTruncated = false;

                if (message.Body?.Content != null)
                {
                    if (message.Body.ContentType == BodyType.Html)
                    {
                        var cleanedHtmlBody = CleanText(message.Body.Content);
                        if (Encoding.UTF8.GetByteCount(cleanedHtmlBody) > 1_000_000)
                        {
                            isHtmlTruncated = true;
                            htmlBody = CleanHtmlForStorage(cleanedHtmlBody);
                        }
                        else
                        {
                            htmlBody = cleanedHtmlBody;
                        }

                        // Also extract text version if available
                        body = CleanText(message.BodyPreview ?? "");
                    }
                    else
                    {
                        var cleanedTextBody = CleanText(message.Body.Content);
                        if (Encoding.UTF8.GetByteCount(cleanedTextBody) > 800_000)
                        {
                            isBodyTruncated = true;
                            body = TruncateTextForStorage(cleanedTextBody, 800_000);
                        }
                        else
                        {
                            body = cleanedTextBody;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(message.BodyPreview))
                {
                    body = CleanText(message.BodyPreview);
                }

                var cleanMessageId = CleanText(messageId);
                var cleanFolderName = CleanText(folderName);

                // Determine if the email is outgoing by comparing the From address with the account's email address
                bool isOutgoingEmail = !string.IsNullOrEmpty(from) && 
                                      !string.IsNullOrEmpty(account.EmailAddress) && 
                                      from.Equals(account.EmailAddress, StringComparison.OrdinalIgnoreCase);
                
                // Additionally check if the folder is a drafts folder to exclude it from outgoing emails
                bool isDraftsFolder = IsDraftsFolder(folderName);

                var archivedEmail = new ArchivedEmail
                {
                    MailAccountId = account.Id,
                    MessageId = cleanMessageId,
                    Subject = subject,
                    From = from,
                    To = to,
                    Cc = cc,
                    Bcc = bcc,
                    SentDate = sentDate,
                    ReceivedDate = DateTime.UtcNow,
                    IsOutgoing = isOutgoingEmail && !isDraftsFolder,
                    HasAttachments = message.HasAttachments ?? false || isHtmlTruncated || isBodyTruncated,
                    Body = body,
                    HtmlBody = htmlBody,
                    FolderName = cleanFolderName
                };

                _context.ArchivedEmails.Add(archivedEmail);
                await _context.SaveChangesAsync();

                // Get and save attachments if any
                if (message.HasAttachments == true)
                {
                    await SaveGraphAttachmentsAsync(graphClient, message.Id, archivedEmail.Id, account.EmailAddress);
                }

                // Save truncated content as attachments if needed
                if (isHtmlTruncated && message.Body?.Content != null)
                {
                    await SaveTruncatedHtmlAsAttachment(message.Body.Content, archivedEmail.Id);
                }

                if (isBodyTruncated && message.Body?.Content != null)
                {
                    await SaveTruncatedTextAsAttachment(message.Body.Content, archivedEmail.Id);
                }

                _logger.LogInformation("Archived Graph API email: {Subject}, From: {From}, To: {To}, Account: {AccountName}",
                    archivedEmail.Subject, archivedEmail.From, archivedEmail.To, account.Name);

                return true; // New email successfully archived
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving Graph API email: Subject={Subject}, From={From}, Error={Message}",
                    message.Subject, message.From?.EmailAddress?.Address, ex.Message);
                return false;
            }
        }

        private async Task SaveGraphAttachmentsAsync(GraphServiceClient graphClient, string messageId, int archivedEmailId, string userPrincipalName)
        {
            try
            {
                var attachmentsResponse = await graphClient.Users[userPrincipalName].Messages[messageId].Attachments.GetAsync();

                if (attachmentsResponse?.Value != null)
                {
                    foreach (var attachment in attachmentsResponse.Value)
                    {
                        try
                        {
                            if (attachment is FileAttachment fileAttachment && fileAttachment.ContentBytes != null)
                            {
                                var cleanFileName = CleanText(fileAttachment.Name ?? "attachment");
                                var contentType = CleanText(fileAttachment.ContentType ?? "application/octet-stream");

                                var emailAttachment = new EmailAttachment
                                {
                                    ArchivedEmailId = archivedEmailId,
                                    FileName = cleanFileName,
                                    ContentType = contentType,
                                    Content = fileAttachment.ContentBytes,
                                    Size = fileAttachment.ContentBytes.Length
                                };

                                _context.EmailAttachments.Add(emailAttachment);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process Graph API attachment: {AttachmentName}", attachment.Name);
                        }
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get attachments for Graph API message {MessageId}", messageId);
            }
        }

        public async Task<bool> TestConnectionAsync(MailAccount account)
        {
            try
            {
                _logger.LogInformation("Testing Graph API connection for M365 account {Name} ({Email})",
                    account.Name, account.EmailAddress);

                var graphClient = await CreateGraphClientAsync(account);

                // Test connection by getting user profile using application permissions
                var user = await graphClient.Users[account.EmailAddress].GetAsync();

                if (user != null)
                {
                    _logger.LogInformation("Graph API connection test passed for account {Name}. User: {UserPrincipalName}",
                        account.Name, user.UserPrincipalName);
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

        public async Task<List<string>> GetMailFoldersAsync(MailAccount account)
        {
            try
            {
                var graphClient = await CreateGraphClientAsync(account);
                var folders = await GetAllMailFoldersAsync(graphClient, account.EmailAddress);

                return folders.Select(f => f.DisplayName).OrderBy(f => f).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Graph API folders for account {AccountId}: {Message}", account.Id, ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Deletes old emails from the M365 mailbox based on the retention policy
        /// </summary>
        /// <param name="account">The M365 mail account</param>
        /// <returns>Number of deleted emails</returns>
        private async Task<int> DeleteOldEmailsAsync(MailAccount account)
        {
            if (!account.DeleteAfterDays.HasValue || account.DeleteAfterDays.Value <= 0)
            {
                return 0;
            }

            var deletedCount = 0;
            var cutoffDate = DateTime.UtcNow.AddDays(-account.DeleteAfterDays.Value);

            _logger.LogInformation("Starting deletion of emails older than {Days} days (before {CutoffDate}) for M365 account {AccountName}",
                account.DeleteAfterDays.Value, cutoffDate, account.Name);

            try
            {
                var graphClient = await CreateGraphClientAsync(account);

                // Get all mail folders
                var folders = await GetAllMailFoldersAsync(graphClient, account.EmailAddress);

                _logger.LogInformation("Found {Count} folders for M365 account: {AccountName}", folders.Count, account.Name);

                // Process each folder
                foreach (var folder in folders)
                {
                    // Skip excluded folders
                    if (account.ExcludedFoldersList.Contains(folder.DisplayName))
                    {
                        _logger.LogInformation("Skipping excluded folder for deletion: {FolderName} for account: {AccountName}",
                            folder.DisplayName, account.Name);
                        continue;
                    }

                    try
                    {
                        _logger.LogInformation("Processing folder {FolderName} for email deletion for account: {AccountName}",
                            folder.DisplayName, account.Name);

                        // Get messages older than cutoff date using Graph API filter
                        var filter = $"receivedDateTime lt {cutoffDate:yyyy-MM-ddTHH:mm:ssZ}";
                        
                        var messagesResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                        {
                            requestConfiguration.QueryParameters.Filter = filter;
                            requestConfiguration.QueryParameters.Select = new string[] { "id", "internetMessageId", "subject", "receivedDateTime" };
                            requestConfiguration.QueryParameters.Top = int.MaxValue; // Process in smaller batches
                        });

                        if (messagesResponse?.Value != null && messagesResponse.Value.Count > 0)
                        {
                            _logger.LogInformation("Found {Count} old emails in folder {FolderName} for account {AccountName}",
                                messagesResponse.Value.Count, folder.DisplayName, account.Name);

                            // Process messages in batches
                            var messageIdsToDelete = new List<string>();
                            
                            foreach (var message in messagesResponse.Value)
                            {
                                // Check if this email is already archived before deletion
                                var messageId = message.InternetMessageId ?? message.Id;
                                
                                var isArchived = await _context.ArchivedEmails
                                    .AnyAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);

                                // Also check with angle brackets if not already found
                                if (!isArchived && !string.IsNullOrEmpty(messageId) && !messageId.StartsWith("<"))
                                {
                                    var messageIdWithBrackets = $"<{messageId}>";
                                    isArchived = await _context.ArchivedEmails
                                        .AnyAsync(e => e.MessageId == messageIdWithBrackets && e.MailAccountId == account.Id);
                                }
                                // Also check without angle brackets if not already found
                                else if (!isArchived && !string.IsNullOrEmpty(messageId) && messageId.StartsWith("<") && messageId.EndsWith(">"))
                                {
                                    var messageIdWithoutBrackets = messageId.Substring(1, messageId.Length - 2);
                                    isArchived = await _context.ArchivedEmails
                                        .AnyAsync(e => e.MessageId == messageIdWithoutBrackets && e.MailAccountId == account.Id);
                                }

                                if (isArchived)
                                {
                                    messageIdsToDelete.Add(message.Id);
                                    _logger.LogDebug("Marking email with Message-ID {MessageId} for deletion from folder {FolderName}",
                                        messageId, folder.DisplayName);
                                }
                                else
                                {
                                    _logger.LogInformation("Skipping deletion of email with Message-ID {MessageId} from folder {FolderName} (not archived). Account ID: {AccountId}",
                                        messageId, folder.DisplayName, account.Id);
                                }
                            }

                            if (messageIdsToDelete.Count > 0)
                            {
                                _logger.LogInformation("Attempting to delete {Count} emails from folder {FolderName} for account {AccountName}",
                                    messageIdsToDelete.Count, folder.DisplayName, account.Name);

                                // Delete messages using batch requests to avoid rate limiting
                                foreach (var messageId in messageIdsToDelete)
                                {
                                    try
                                    {
                                        await graphClient.Users[account.EmailAddress].Messages[messageId].DeleteAsync();
                                        deletedCount++;
                                        _logger.LogDebug("Successfully deleted email {MessageId} from folder {FolderName}",
                                            messageId, folder.DisplayName);
                                    }
                                    catch (Exception deleteEx)
                                    {
                                        _logger.LogError(deleteEx, "Error deleting email {MessageId} from folder {FolderName} for account {AccountName}",
                                            messageId, folder.DisplayName, account.Name);
                                    }
                                }

                                _logger.LogInformation("Successfully processed {Count} emails for deletion from folder {FolderName} for account {AccountName}",
                                    messageIdsToDelete.Count, folder.DisplayName, account.Name);
                            }
                            else
                            {
                                _logger.LogInformation("No archived emails to delete in folder {FolderName} for account {AccountName}",
                                    folder.DisplayName, account.Name);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("No old emails found in folder {FolderName} for account {AccountName}",
                                folder.DisplayName, account.Name);
                        }
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

        public async Task<bool> RestoreEmailToFolderAsync(ArchivedEmail email, MailAccount targetAccount, string folderName)
        {
            _logger.LogInformation("RestoreEmailToFolderAsync via Graph API called for email {EmailId} to folder {FolderName}",
                email.Id, folderName);

            try
            {
                var graphClient = await CreateGraphClientAsync(targetAccount);

                // Find the target folder
                var folders = await GetAllMailFoldersAsync(graphClient, targetAccount.EmailAddress);
                var targetFolder = folders.FirstOrDefault(f => f.DisplayName.Equals(folderName, StringComparison.OrdinalIgnoreCase));

                if (targetFolder == null)
                {
                    // Default to Inbox if folder not found
                    targetFolder = folders.FirstOrDefault(f => f.DisplayName.Equals("Inbox", StringComparison.OrdinalIgnoreCase));
                    if (targetFolder == null)
                    {
                        _logger.LogError("Could not find target folder {FolderName} or Inbox for account {AccountName}", folderName, targetAccount.Name);
                        return false;
                    }
                    _logger.LogWarning("Target folder {FolderName} not found, using Inbox instead", folderName);
                }

                // Create the message to restore - focus on preserving content first
                var message = new Message
                {
                    Subject = email.Subject ?? "(No Subject)",
                    Body = new ItemBody
                    {
                        ContentType = !string.IsNullOrEmpty(email.HtmlBody) ? BodyType.Html : BodyType.Text,
                        Content = !string.IsNullOrEmpty(email.HtmlBody) ? email.HtmlBody : (email.Body ?? "(No Content)")
                    },
                    From = new Recipient
                    {
                        EmailAddress = new Microsoft.Graph.Models.EmailAddress
                        {
                            Address = email.From ?? "unknown@unknown.com",
                            Name = email.From ?? "Unknown Sender"
                        }
                    },
                    ToRecipients = ParseEmailAddresses(email.To),
                    CcRecipients = ParseEmailAddresses(email.Cc),
                    BccRecipients = ParseEmailAddresses(email.Bcc),
                    SentDateTime = email.SentDate,
                    ReceivedDateTime = email.ReceivedDate,
                    InternetMessageId = email.MessageId,
                    // Set properties that might help with proper display
                    IsRead = false, // Mark as unread
                    Importance = Importance.Normal,
                    InferenceClassification = InferenceClassificationType.Focused,
                    // CRITICAL: Set extended property to prevent draft behavior
                    // PidTagMessageFlags (0x0E07) = 3591 with value 1 prevents the message from appearing as a draft
                    SingleValueExtendedProperties = new List<SingleValueLegacyExtendedProperty>
                    {
                        new SingleValueLegacyExtendedProperty
                        {
                            Id = "Integer 0x0E07", // PidTagMessageFlags
                            Value = "1" // Mark as not a draft (MSGFLAG_READ)
                        }
                    }
                };

                _logger.LogDebug("Creating message with Subject: {Subject}, From: {From}, To: {To}, Body length: {BodyLength}", 
                    message.Subject, message.From?.EmailAddress?.Address, 
                    string.Join(", ", message.ToRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>()),
                    message.Body?.Content?.Length ?? 0);

                // Create the message in the target folder
                var createdMessage = await graphClient.Users[targetAccount.EmailAddress].MailFolders[targetFolder.Id].Messages.PostAsync(message);

                if (createdMessage != null)
                {
                    _logger.LogInformation("Message created successfully with ID: {MessageId}", createdMessage.Id);
                    
                    // Handle attachments restoration if needed
                    if (email.Attachments != null && email.Attachments.Any())
                    {
                        await RestoreAttachmentsAsync(graphClient, email, targetAccount, createdMessage.Id);
                    }
                    
                    _logger.LogInformation("Successfully restored email {EmailId} to folder {FolderName} via Graph API. Extended property set to prevent draft behavior.", 
                        email.Id, folderName);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring email {EmailId} to folder {FolderName} via Graph API: {Message}",
                    email.Id, folderName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Builds a proper MIME message from archived email data to preserve original headers
        /// </summary>
        private async Task<MimeMessage> BuildMimeMessageFromArchivedEmailAsync(ArchivedEmail email)
        {
            var message = new MimeMessage();

            // Set the Message-ID to preserve original identity
            if (!string.IsNullOrEmpty(email.MessageId))
            {
                // Ensure Message-ID has proper format
                var messageId = email.MessageId.Trim();
                if (!messageId.StartsWith("<"))
                    messageId = "<" + messageId;
                if (!messageId.EndsWith(">"))
                    messageId = messageId + ">";
                
                message.MessageId = messageId;
            }

            // Set subject
            message.Subject = email.Subject ?? "(No Subject)";

            // Parse and set From address
            if (!string.IsNullOrEmpty(email.From))
            {
                try
                {
                    message.From.Add(MailboxAddress.Parse(email.From));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse From address '{From}', using fallback", email.From);
                    message.From.Add(new MailboxAddress("Restored Email", email.From));
                }
            }

            // Parse and set To addresses
            if (!string.IsNullOrEmpty(email.To))
            {
                foreach (var address in ParseAddressesForMime(email.To))
                {
                    message.To.Add(address);
                }
            }

            // Parse and set CC addresses  
            if (!string.IsNullOrEmpty(email.Cc))
            {
                foreach (var address in ParseAddressesForMime(email.Cc))
                {
                    message.Cc.Add(address);
                }
            }

            // Parse and set BCC addresses
            if (!string.IsNullOrEmpty(email.Bcc))
            {
                foreach (var address in ParseAddressesForMime(email.Bcc))
                {
                    message.Bcc.Add(address);
                }
            }

            // Set dates
            message.Date = email.SentDate;

            // Create message body
            var builder = new BodyBuilder();

            if (!string.IsNullOrEmpty(email.Body))
            {
                builder.TextBody = email.Body;
            }

            if (!string.IsNullOrEmpty(email.HtmlBody))
            {
                builder.HtmlBody = email.HtmlBody;
            }

            // Add attachments if any
            if (email.Attachments != null && email.Attachments.Any())
            {
                foreach (var attachment in email.Attachments)
                {
                    try
                    {
                        using var attachmentStream = new MemoryStream(attachment.Content);
                        await builder.Attachments.AddAsync(attachment.FileName, attachmentStream, 
                            MimeKit.ContentType.Parse(attachment.ContentType ?? "application/octet-stream"));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to add attachment {AttachmentName} to MIME message", attachment.FileName);
                    }
                }
            }

            message.Body = builder.ToMessageBody();

            // Set additional headers to ensure proper restoration
            message.Headers.Add("X-MS-Exchange-Organization-OriginalArrivalTime", email.ReceivedDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            message.Headers.Add("X-Restored-From-Archive", "true");

            return message;
        }

        /// <summary>
        /// Parses email addresses from a comma-separated string for MimeKit
        /// </summary>
        private List<MailboxAddress> ParseAddressesForMime(string addresses)
        {
            var result = new List<MailboxAddress>();

            if (string.IsNullOrEmpty(addresses))
                return result;

            var addressList = addresses.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var address in addressList)
            {
                try
                {
                    var trimmedAddress = address.Trim();
                    if (!string.IsNullOrEmpty(trimmedAddress))
                    {
                        result.Add(MailboxAddress.Parse(trimmedAddress));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse email address '{Address}', using fallback", address);
                    // Create a fallback mailbox address
                    var cleanAddress = address.Trim();
                    if (cleanAddress.Contains("@"))
                    {
                        result.Add(new MailboxAddress("Restored Contact", cleanAddress));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a MimeKit MimeMessage to a Microsoft Graph Message object
        /// while preserving original sender/recipient information and avoiding draft status
        /// </summary>
        private async Task<Message> ConvertMimeMessageToGraphMessage(MimeMessage mimeMessage)
        {
            var message = new Message
            {
                Subject = mimeMessage.Subject,
                SentDateTime = mimeMessage.Date,
                // Do not set IsDraft explicitly - let Exchange handle it properly based on headers
                IsRead = false, // New messages should appear as unread
                Importance = Importance.Normal
            };

            // Convert From address
            if (mimeMessage.From.Any())
            {
                var fromAddress = mimeMessage.From.First() as MailboxAddress;
                if (fromAddress != null)
                {
                    message.From = new Recipient
                    {
                        EmailAddress = new Microsoft.Graph.Models.EmailAddress
                        {
                            Address = fromAddress.Address,
                            Name = fromAddress.Name ?? fromAddress.Address
                        }
                    };
                }
            }

            // Convert To addresses
            message.ToRecipients = new List<Recipient>();
            foreach (var toAddress in mimeMessage.To.OfType<MailboxAddress>())
            {
                message.ToRecipients.Add(new Recipient
                {
                    EmailAddress = new Microsoft.Graph.Models.EmailAddress
                    {
                        Address = toAddress.Address,
                        Name = toAddress.Name ?? toAddress.Address
                    }
                });
            }

            // Convert CC addresses
            message.CcRecipients = new List<Recipient>();
            foreach (var ccAddress in mimeMessage.Cc.OfType<MailboxAddress>())
            {
                message.CcRecipients.Add(new Recipient
                {
                    EmailAddress = new Microsoft.Graph.Models.EmailAddress
                    {
                        Address = ccAddress.Address,
                        Name = ccAddress.Name ?? ccAddress.Address
                    }
                });
            }

            // Convert BCC addresses  
            message.BccRecipients = new List<Recipient>();
            foreach (var bccAddress in mimeMessage.Bcc.OfType<MailboxAddress>())
            {
                message.BccRecipients.Add(new Recipient
                {
                    EmailAddress = new Microsoft.Graph.Models.EmailAddress
                    {
                        Address = bccAddress.Address,
                        Name = bccAddress.Name ?? bccAddress.Address
                    }
                });
            }

            // Convert message body
            if (mimeMessage.Body is Multipart multipart)
            {
                // Handle multipart messages
                var textPart = multipart.OfType<TextPart>().FirstOrDefault(p => p.ContentType.MediaSubtype.Equals("plain", StringComparison.OrdinalIgnoreCase));
                var htmlPart = multipart.OfType<TextPart>().FirstOrDefault(p => p.ContentType.MediaSubtype.Equals("html", StringComparison.OrdinalIgnoreCase));

                if (htmlPart != null)
                {
                    message.Body = new ItemBody
                    {
                        ContentType = Microsoft.Graph.Models.BodyType.Html,
                        Content = htmlPart.Text
                    };
                }
                else if (textPart != null)
                {
                    message.Body = new ItemBody
                    {
                        ContentType = Microsoft.Graph.Models.BodyType.Text,
                        Content = textPart.Text
                    };
                }

                // Handle attachments from multipart
                var attachmentParts = multipart.OfType<MimePart>().Where(p => p.IsAttachment);
                if (attachmentParts.Any())
                {
                    message.HasAttachments = true;
                    // Note: Attachments will need to be added separately after message creation
                }
            }
            else if (mimeMessage.Body is TextPart textPart)
            {
                // Handle simple text message
                message.Body = new ItemBody
                {
                    ContentType = textPart.ContentType.MediaSubtype.Equals("html", StringComparison.OrdinalIgnoreCase) 
                        ? Microsoft.Graph.Models.BodyType.Html 
                        : Microsoft.Graph.Models.BodyType.Text,
                    Content = textPart.Text
                };
            }

            // Set Internet Message ID to preserve original identity
            if (!string.IsNullOrEmpty(mimeMessage.MessageId))
            {
                message.InternetMessageId = mimeMessage.MessageId;
            }

            // Set additional properties to ensure proper message handling
            message.ReceivedDateTime = DateTime.UtcNow;

            return message;
        }

        private async Task RestoreAttachmentsAsync(GraphServiceClient graphClient, ArchivedEmail email, MailAccount targetAccount, string messageId)
        {
            try
            {
                foreach (var attachment in email.Attachments)
                {
                    try
                    {
                        var fileAttachment = new Microsoft.Graph.Models.FileAttachment
                        {
                            OdataType = "#microsoft.graph.fileAttachment",
                            Name = attachment.FileName,
                            ContentType = attachment.ContentType,
                            ContentBytes = attachment.Content
                        };

                        await graphClient.Users[targetAccount.EmailAddress].Messages[messageId].Attachments.PostAsync(fileAttachment);
                        _logger.LogInformation("Successfully restored attachment {AttachmentName} for email {EmailId}", attachment.FileName, email.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error restoring attachment {AttachmentName} for email {EmailId}", attachment.FileName, email.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring attachments for email {EmailId}", email.Id);
            }
        }

        private List<Recipient> ParseEmailAddresses(string emailAddresses)
        {
            var recipients = new List<Recipient>();

            if (string.IsNullOrEmpty(emailAddresses))
                return recipients;

            var addresses = emailAddresses.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var address in addresses)
            {
                var cleanAddress = address.Trim();
                if (!string.IsNullOrEmpty(cleanAddress))
                {
                    recipients.Add(new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = cleanAddress,
                            Name = cleanAddress
                        }
                    });
                }
            }

            return recipients;
        }

        private bool IsOutgoingFolder(MailFolder folder)
        {
            var sentFolderNames = new[]
            {
                "sent", "sent items", "sent mail", "outbox",
                "gesendet", "gesendete objekte", "postausgang",
                "envoyé", "éléments envoyés", "boîte d'envoi",
                "inviato", "posta inviata", "posta in uscita"
            };

            string folderNameLower = folder.DisplayName?.ToLowerInvariant() ?? "";
            return sentFolderNames.Any(name => folderNameLower.Contains(name));
        }

        private bool IsDraftsFolder(string folderName)
        {
            var draftsFolderNames = new[]
            {
                "drafts", "entwürfe", "brouillons", "bozze"
            };

            string folderNameLower = folderName?.ToLowerInvariant() ?? "";
            return draftsFolderNames.Any(name => folderNameLower.Contains(name));
        }

        // Helper classes and methods
        private class SyncFolderResult
        {
            public int ProcessedEmails { get; set; }
            public int NewEmails { get; set; }
            public int FailedEmails { get; set; }
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Remove null characters
            text = text.Replace("\0", "");

            var cleanedText = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (c == '\r' || c == '\n' || c == '\t' || c >= 32)
                {
                    cleanedText.Append(c);
                }
                else
                {
                    cleanedText.Append(' ');
                }
            }

            return cleanedText.ToString();
        }

        private string CleanHtmlForStorage(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            if (html.Contains('\0'))
            {
                html = html.Replace("\0", "");
            }

            const int MaxHtmlSizeBytes = 1_000_000;

            if (html.Length <= MaxHtmlSizeBytes)
                return html;

            const string TruncationNotice = @"
                    <div style='background-color: #f8f9fa; border: 1px solid #dee2e6; border-radius: 5px; padding: 15px; margin: 10px 0; font-family: Arial, sans-serif;'>
                        <h4 style='color: #495057; margin-top: 0;'>📎 Email content has been truncated</h4>
                        <p style='color: #6c757d; margin-bottom: 10px;'>
                            This email contains very large HTML content (over 1 MB) that has been truncated for better performance.
                        </p>
                        <p style='color: #6c757d; margin-bottom: 0;'>
                            <strong>The complete original HTML content has been saved as an attachment.</strong><br>
                            Look for a file named 'original_content_*.html' in the attachments.
                        </p>
                    </div>";

            int TruncationOverhead = Encoding.UTF8.GetByteCount(TruncationNotice + "</body></html>");
            int maxContentSize = MaxHtmlSizeBytes - TruncationOverhead;
            
            if (maxContentSize <= 0)
            {
                return $"<html><body>{TruncationNotice}</body></html>";
            }

            int truncatePosition = Math.Min(maxContentSize, html.Length);

            // Find safe truncation point that doesn't break HTML tags
            int lastLessThan = html.LastIndexOf('<', truncatePosition - 1);
            int lastGreaterThan = html.LastIndexOf('>', truncatePosition - 1);

            if (lastLessThan > lastGreaterThan && lastLessThan >= 0)
            {
                truncatePosition = lastLessThan;
            }
            else if (lastGreaterThan >= 0)
            {
                truncatePosition = lastGreaterThan + 1;
            }

            var result = new StringBuilder(truncatePosition + TruncationNotice.Length + 50);
            ReadOnlySpan<char> baseContent = html.AsSpan(0, truncatePosition);

            bool hasHtml = baseContent.Contains("<html".AsSpan(), StringComparison.OrdinalIgnoreCase);
            bool hasBody = baseContent.Contains("<body".AsSpan(), StringComparison.OrdinalIgnoreCase);

            if (!hasHtml)
            {
                result.Append("<html>");
            }

            if (!hasBody)
            {
                if (hasHtml)
                {
                    string contentStr = baseContent.ToString();
                    int htmlStart = contentStr.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
                    if (htmlStart >= 0)
                    {
                        int htmlTagEnd = contentStr.IndexOf('>', htmlStart);
                        if (htmlTagEnd >= 0)
                        {
                            result.Append(baseContent.Slice(0, htmlTagEnd + 1));
                            result.Append("<body>");
                            result.Append(baseContent.Slice(htmlTagEnd + 1));
                        }
                        else
                        {
                            result.Append("<body>");
                            result.Append(baseContent);
                        }
                    }
                    else
                    {
                        result.Append("<body>");
                        result.Append(baseContent);
                    }
                }
                else
                {
                    result.Append("<body>");
                    result.Append(baseContent);
                }
            }
            else
            {
                result.Append(baseContent);
            }

            result.Append(TruncationNotice);

            string resultStr = result.ToString();
            if (!resultStr.EndsWith("</body>", StringComparison.OrdinalIgnoreCase))
            {
                result.Append("</body>");
            }
            if (!resultStr.EndsWith("</html>", StringComparison.OrdinalIgnoreCase))
            {
                result.Append("</html>");
            }

            return result.ToString();
        }

        private string TruncateTextForStorage(string text, int maxSizeBytes)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            const string textTruncationNotice = "\n\n[CONTENT TRUNCATED - This email contains very large text content that has been truncated for better performance. The complete original content has been saved as an attachment.]";

            int noticeOverhead = Encoding.UTF8.GetByteCount(textTruncationNotice);
            int maxContentSize = maxSizeBytes - noticeOverhead;

            if (maxContentSize <= 0)
            {
                return textTruncationNotice;
            }

            if (Encoding.UTF8.GetByteCount(text) <= maxSizeBytes)
            {
                return text;
            }

            int approximateCharPosition = Math.Min(maxContentSize, text.Length);

            while (approximateCharPosition > 0 && Encoding.UTF8.GetByteCount(text.Substring(0, approximateCharPosition)) > maxContentSize)
            {
                approximateCharPosition--;
            }

            int wordBoundarySearch = Math.Max(0, approximateCharPosition - 100);
            int lastSpaceIndex = text.LastIndexOf(' ', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);
            int lastNewlineIndex = text.LastIndexOf('\n', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);
            int lastPunctuationIndex = text.LastIndexOfAny(new char[] { '.', '!', '?', ';' }, approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);

            int breakPoint = Math.Max(Math.Max(lastSpaceIndex, lastNewlineIndex), lastPunctuationIndex);
            if (breakPoint > wordBoundarySearch)
            {
                approximateCharPosition = breakPoint + 1;
            }

            string truncatedContent = text.Substring(0, approximateCharPosition);
            while (Encoding.UTF8.GetByteCount(truncatedContent + textTruncationNotice) > maxSizeBytes && truncatedContent.Length > 0)
            {
                truncatedContent = truncatedContent.Substring(0, truncatedContent.Length - 1);
            }

            return truncatedContent + textTruncationNotice;
        }

        private async Task SaveTruncatedHtmlAsAttachment(string originalHtml, int archivedEmailId)
        {
            try
            {
                var cleanFileName = CleanText($"original_content_{DateTime.UtcNow:yyyyMMddHHmmss}.html");
                var contentType = "text/html";

                var emailAttachment = new EmailAttachment
                {
                    ArchivedEmailId = archivedEmailId,
                    FileName = cleanFileName,
                    ContentType = contentType,
                    Content = Encoding.UTF8.GetBytes(originalHtml),
                    Size = Encoding.UTF8.GetByteCount(originalHtml)
                };

                _context.EmailAttachments.Add(emailAttachment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully saved original HTML content as attachment for email {EmailId}",
                    archivedEmailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save original HTML content as attachment for email {EmailId}", archivedEmailId);
            }
        }

        private async Task SaveTruncatedTextAsAttachment(string originalText, int archivedEmailId)
        {
            try
            {
                var cleanFileName = CleanText($"original_text_content_{DateTime.UtcNow:yyyyMMddHHmmss}.txt");
                var contentType = "text/plain";

                var emailAttachment = new EmailAttachment
                {
                    ArchivedEmailId = archivedEmailId,
                    FileName = cleanFileName,
                    ContentType = contentType,
                    Content = Encoding.UTF8.GetBytes(originalText),
                    Size = Encoding.UTF8.GetByteCount(originalText)
                };

                _context.EmailAttachments.Add(emailAttachment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully saved original text content as attachment for email {EmailId}",
                    archivedEmailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save original text content as attachment for email {EmailId}", archivedEmailId);
            }
        }
    }

    /// <summary>
    /// Simple token authentication provider for Microsoft Graph
    /// </summary>
    public class TokenAuthenticationProvider : IAuthenticationProvider
    {
        private readonly string _accessToken;

        public TokenAuthenticationProvider(string accessToken)
        {
            _accessToken = accessToken;
        }

        public Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");
            return Task.CompletedTask;
        }
    }
}
