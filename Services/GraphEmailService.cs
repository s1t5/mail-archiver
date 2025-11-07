using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Utilities;
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
        private readonly DateTimeHelper _dateTimeHelper;

        public GraphEmailService(
            MailArchiverDbContext context,
            ILogger<GraphEmailService> logger,
            ISyncJobService syncJobService,
            IOptions<BatchOperationOptions> batchOptions,
            IOptions<MailSyncOptions> mailSyncOptions,
            DateTimeHelper dateTimeHelper)
        {
            _context = context;
            _logger = logger;
            _syncJobService = syncJobService;
            _batchOptions = batchOptions.Value;
            _mailSyncOptions = mailSyncOptions.Value;
            _dateTimeHelper = dateTimeHelper;
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
                        if (!string.IsNullOrEmpty(folder.DisplayName) && account.ExcludedFoldersList.Contains(folder.DisplayName))
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
                _logger.LogInformation("Starting to retrieve all mail folders for user: {UserPrincipalName}", userPrincipalName);

                // Get mail folders from the user's mailbox using application permissions with pagination
                var response = await graphClient.Users[userPrincipalName].MailFolders.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName", "parentFolderId", "childFolderCount", "totalItemCount" };
                    requestConfiguration.QueryParameters.Top = 100; // Increase from BatchSize to ensure we get all top-level folders
                });

                int folderCount = 0;
                int pageCount = 0;

                // Process all pages of top-level folders
                while (response?.Value != null)
                {
                    pageCount++;
                    var currentPageCount = response.Value.Count;
                    folderCount += currentPageCount;
                    
                    _logger.LogInformation("Processing folder page {PageNumber} with {FolderCount} folders (Total so far: {TotalFolders})",
                        pageCount, currentPageCount, folderCount);

                    folders.AddRange(response.Value);

                    // Log folder names for debugging
                    foreach (var folder in response.Value)
                    {
                        _logger.LogDebug("Found folder: '{FolderName}' (ID: {FolderId}, ChildCount: {ChildCount}, ItemCount: {ItemCount})",
                            folder.DisplayName, folder.Id, folder.ChildFolderCount, folder.TotalItemCount);
                    }

                    // Check for next page
                    if (!string.IsNullOrEmpty(response.OdataNextLink))
                    {
                        _logger.LogInformation("Fetching next page of folders...");
                        response = await graphClient.Users[userPrincipalName].MailFolders.WithUrl(response.OdataNextLink).GetAsync();
                    }
                    else
                    {
                        break;
                    }
                }

                _logger.LogInformation("Retrieved {TotalFolders} top-level folders across {PageCount} pages", folderCount, pageCount);

                // Now get child folders recursively for all top-level folders
                var topLevelFolders = folders.ToList(); // Create a copy to avoid modification during iteration
                foreach (var folder in topLevelFolders)
                {
                    if (folder.ChildFolderCount > 0)
                    {
                        _logger.LogDebug("Getting child folders for: '{FolderName}' (Expected children: {ChildCount})",
                            folder.DisplayName, folder.ChildFolderCount);
                        
                        var childFolders = await GetChildFoldersAsync(graphClient, userPrincipalName, folder.Id);
                        folders.AddRange(childFolders);
                        
                        _logger.LogDebug("Added {ChildFolderCount} child folders for '{FolderName}'",
                            childFolders.Count, folder.DisplayName);
                    }
                }

                _logger.LogInformation("Total folders retrieved (including children): {TotalFolders}", folders.Count);

                // Log all folder names for verification
                var folderNames = folders.Select(f => f.DisplayName).OrderBy(name => name).ToList();
                _logger.LogInformation("All folders found: {FolderNames}", string.Join(", ", folderNames));
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
                _logger.LogDebug("Starting to retrieve child folders for parent: {ParentFolderId}", parentFolderId);

                var response = await graphClient.Users[userPrincipalName].MailFolders[parentFolderId].ChildFolders.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName", "parentFolderId", "childFolderCount", "totalItemCount" };
                    requestConfiguration.QueryParameters.Top = 100; // Increase from BatchSize to ensure we get all child folders
                });

                int childFolderCount = 0;
                int pageCount = 0;

                // Process all pages of child folders
                while (response?.Value != null)
                {
                    pageCount++;
                    var currentPageCount = response.Value.Count;
                    childFolderCount += currentPageCount;
                    
                    _logger.LogDebug("Processing child folder page {PageNumber} with {FolderCount} folders for parent {ParentFolderId} (Total so far: {TotalFolders})",
                        pageCount, currentPageCount, parentFolderId, childFolderCount);

                    childFolders.AddRange(response.Value);

                    // Log child folder names for debugging
                    foreach (var folder in response.Value)
                    {
                        _logger.LogDebug("Found child folder: '{FolderName}' (ID: {FolderId}, Parent: {ParentFolderId}, ChildCount: {ChildCount}, ItemCount: {ItemCount})",
                            folder.DisplayName, folder.Id, folder.ParentFolderId, folder.ChildFolderCount, folder.TotalItemCount);
                    }

                    // Check for next page
                    if (!string.IsNullOrEmpty(response.OdataNextLink))
                    {
                        _logger.LogDebug("Fetching next page of child folders for parent {ParentFolderId}...", parentFolderId);
                        response = await graphClient.Users[userPrincipalName].MailFolders[parentFolderId].ChildFolders.WithUrl(response.OdataNextLink).GetAsync();
                    }
                    else
                    {
                        break;
                    }
                }

                _logger.LogDebug("Retrieved {TotalChildFolders} child folders across {PageCount} pages for parent {ParentFolderId}", 
                    childFolderCount, pageCount, parentFolderId);

                // Now recursively get grandchild folders for all child folders
                var currentChildFolders = childFolders.ToList(); // Create a copy to avoid modification during iteration
                foreach (var folder in currentChildFolders)
                {
                    if (folder.ChildFolderCount > 0)
                    {
                        _logger.LogDebug("Getting grandchild folders for: '{FolderName}' (Expected children: {ChildCount})",
                            folder.DisplayName, folder.ChildFolderCount);
                        
                        var grandChildFolders = await GetChildFoldersAsync(graphClient, userPrincipalName, folder.Id);
                        childFolders.AddRange(grandChildFolders);
                        
                        _logger.LogDebug("Added {GrandChildFolderCount} grandchild folders for '{FolderName}'",
                            grandChildFolders.Count, folder.DisplayName);
                    }
                }

                _logger.LogDebug("Total child and grandchild folders retrieved for parent {ParentFolderId}: {TotalFolders}", 
                    parentFolderId, childFolders.Count);
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
                
                // Subtract 12 hours from lastSync for the query, but only if it's not the Unix epoch (1/1/1970)
                if (lastSync != new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                {
                    lastSync = lastSync.AddHours(-12);
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
                        // If no messages returned with filter, try a diagnostic query to check if messages exist at all
                        _logger.LogWarning("No messages returned with date filter for folder {FolderName}. Performing diagnostic check...", folder.DisplayName);
                        
                        try
                        {
                            // Diagnostic query: Get total message count without filter
                            var diagnosticResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                            {
                                requestConfiguration.QueryParameters.Select = new string[] { "id" };
                                requestConfiguration.QueryParameters.Top = 1;
                            });
                            
                            _logger.LogWarning("Diagnostic check for folder {FolderName}: {MessageCount} messages found without filter. " +
                                             "This suggests the date filter might be too restrictive or there's a permission issue.",
                                folder.DisplayName, diagnosticResponse?.Value?.Count ?? 0);
                            
                            // If diagnostic query finds messages, try with a more permissive date filter
                            if (diagnosticResponse?.Value?.Count > 0)
                            {
                                var permissiveLastSync = DateTime.UtcNow.AddDays(-30); // Last 30 days
                                var permissiveFilter = $"lastModifiedDateTime ge {permissiveLastSync:yyyy-MM-ddTHH:mm:ssZ}";
                                
                                _logger.LogInformation("Trying permissive filter for folder {FolderName}: {Filter}", 
                                    folder.DisplayName, permissiveFilter);
                                
                                messagesResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.GetAsync((requestConfiguration) =>
                                {
                                    requestConfiguration.QueryParameters.Filter = permissiveFilter;
                                    requestConfiguration.QueryParameters.Select = new string[] 
                                    { 
                                        "id", "internetMessageId", "subject", "from", "sentDateTime", "receivedDateTime", "lastModifiedDateTime"
                                    };
                                    requestConfiguration.QueryParameters.Top = _batchOptions.BatchSize;
                                });
                                
                                _logger.LogInformation("Permissive filter returned {Count} messages for folder {FolderName}", 
                                    messagesResponse?.Value?.Count ?? 0, folder.DisplayName);
                            }
                        }
                        catch (Exception diagEx)
                        {
                            _logger.LogError(diagEx, "Diagnostic query failed for folder {FolderName}: {Error}", 
                                folder.DisplayName, diagEx.Message);
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
                    var filteredMessages = messagesResponse.Value
                        .Where(m => m.LastModifiedDateTime >= lastSync)
                        .ToList();
                        
                    _logger.LogInformation("Applying date filter. Before filter: {TotalCount}, After filter: {FilteredCount}", 
                        messagesResponse.Value.Count, filteredMessages.Count);

                    result.ProcessedEmails = filteredMessages.Count;
                    _logger.LogInformation("Processing page 1 with {Count} messages in folder {FolderName} for account: {AccountName}",
                        filteredMessages.Count, folder.DisplayName, account.Name);

                    // Process messages in batches for better memory management
                    for (int i = 0; i < filteredMessages.Count; i += _batchOptions.BatchSize)
                    {
                        var batch = filteredMessages.Skip(i).Take(_batchOptions.BatchSize).ToList();
                        _logger.LogInformation("Processing batch of {Count} messages (starting at {Start}) in folder {FolderName} via Graph API",
                            batch.Count, i, folder.DisplayName);

                        foreach (var message in batch)
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

                        // Log memory usage after each batch (including the last one)
                        _logger.LogInformation("Memory usage after processing batch {BatchNumber}: {MemoryUsage}",
                            (i / _batchOptions.BatchSize) + 1, MemoryMonitor.GetMemoryUsageFormatted());

                        // After processing each batch, perform comprehensive cleanup (except for the last batch)
                        if (i + _batchOptions.BatchSize < filteredMessages.Count)
                        {
                            // Clear Entity Framework Change Tracker to free memory
                            _context.ChangeTracker.Clear();
                            
                            // Use the configurable pause between batches
                            if (_batchOptions.PauseBetweenBatchesMs > 0)
                            {
                                await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                            }
                            
                            // Force garbage collection after each batch to free memory
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }

                    // Handle pagination if there are more messages
                    int paginationCount = 1; // Start with 1 since we already processed the first page
                    int maxPaginationPages = int.MaxValue; // Removed pagination limit
                    
                    while (!string.IsNullOrEmpty(messagesResponse.OdataNextLink) && paginationCount < maxPaginationPages)
                    {
                        paginationCount++;
                        
                        // Safety check: Stop if we've processed too many messages already
                        if (processedInThisFolder >= maxMessagesPerFolder)
                        {
                            _logger.LogWarning("Reached maximum message limit during pagination for folder {FolderName}, stopping at page {PageNumber}", folder.DisplayName, paginationCount);
                            break;
                        }

                        // Check if job has been cancelled
                        if (jobId != null)
                        {
                            var job = _syncJobService.GetJob(jobId);
                            if (job?.Status == SyncJobStatus.Cancelled)
                            {
                                _logger.LogInformation("Sync job {JobId} for account {AccountName} has been cancelled during pagination at page {PageNumber}", jobId, account.Name, paginationCount);
                                return result;
                            }
                        }

                        // Add a pause between batches to avoid overwhelming the API
                        if (_batchOptions.PauseBetweenBatchesMs > 0)
                        {
                            await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                        }

                        _logger.LogInformation("Fetching page {PageNumber} for folder {FolderName} (Total processed so far: {TotalProcessed})", 
                            paginationCount, folder.DisplayName, result.ProcessedEmails);

                        // Get next page
                        messagesResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.WithUrl(messagesResponse.OdataNextLink).GetAsync();

                        if (messagesResponse?.Value != null)
                        {
                            // Apply the same filtering logic for pagination results as for the main query
                            var filteredPaginationMessages = messagesResponse.Value
                                .Where(m => m.LastModifiedDateTime >= lastSync)
                                .ToList();

                            _logger.LogInformation("Processing page {PageNumber} with {Count} messages in folder {FolderName} for account: {AccountName}",
                                paginationCount, filteredPaginationMessages.Count, folder.DisplayName, account.Name);

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
                                
                                // Force garbage collection after each email to free memory
                                if (processedInThisFolder % 10 == 0)
                                {
                                    GC.Collect();
                                    GC.WaitForPendingFinalizers();
                                }
                            }

                            // Log memory usage after processing each pagination page
                            _logger.LogInformation("Memory usage after processing pagination page {PageNumber}: {MemoryUsage}",
                                paginationCount, MemoryMonitor.GetMemoryUsageFormatted());
                        }
                        
                        // Force garbage collection after each page to free memory
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }

                    if (paginationCount >= maxPaginationPages)
                    {
                        _logger.LogWarning("Reached maximum pagination limit ({MaxPages}) for folder {FolderName}, stopping for safety", 
                            maxPaginationPages, folder.DisplayName);
                    }
                    
                    // Log pagination completion summary
                    if (paginationCount > 1)
                    {
                        _logger.LogInformation("Completed pagination for folder {FolderName}. Total pages processed: {TotalPages}, Total messages: {TotalMessages}",
                            folder.DisplayName, paginationCount, result.ProcessedEmails);
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
            // Verbesserte MessageId-Generierung für M365 E-Mails ohne zuverlässige MessageIds
            var messageId = message.InternetMessageId ?? message.Id;
            
            // Wenn keine MessageId vorhanden, generiere Hash basierend auf E-Mail-Inhalt
            if (string.IsNullOrEmpty(messageId))
            {
                var from = message.From?.EmailAddress?.Address ?? "";
                var to = string.Join(",", message.ToRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>());
                var subject = message.Subject ?? "";
                var dateTicks = message.SentDateTime?.Ticks ?? DateTime.UtcNow.Ticks;
                
                var uniqueString = $"{from}|{to}|{subject}|{dateTicks}";
                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(uniqueString));
                    var hashString = Convert.ToBase64String(hashBytes).Replace("+", "-").Replace("/", "_").Substring(0, 16);
                    messageId = $"generated-{hashString}@mail-archiver.local";
                }
                
                _logger.LogDebug("Generated MessageId for M365 email without MessageId: {MessageId}", messageId);
            }
            
            _logger.LogDebug("Processing message {MessageId} for account {AccountName}, Subject: {Subject}", 
                messageId, account.Name, message.Subject ?? "No Subject");

            try
            {
                // ROBUSTE Duplikaterkennung - prüfe mehrere Kriterien  
                // M365 kann auch fehlende/duplizierte MessageIds haben
                var checkFrom = message.From?.EmailAddress?.Address ?? "";
                var checkTo = string.Join(",", message.ToRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>());
                var checkSubject = message.Subject ?? "(No Subject)";
                var checkDate = message.SentDateTime?.DateTime ?? DateTime.UtcNow;
                
                var existingEmail = await _context.ArchivedEmails
                    .Where(e => e.MailAccountId == account.Id)
                    .Where(e => 
                        e.MessageId == messageId ||
                        (e.From == checkFrom &&
                         e.To == checkTo &&
                         e.Subject == checkSubject &&
                         Math.Abs((e.SentDate - checkDate).TotalSeconds) < 2)
                    )
                    .FirstOrDefaultAsync();

                if (existingEmail != null)
                {
                    // E-Mail existiert bereits, prüfen ob der Ordner geändert wurde
                    var cleanFolderName = CleanText(folderName);
                    if (existingEmail.FolderName != cleanFolderName)
                    {
                        // Ordner hat sich geändert, aktualisieren
                        var oldFolder = existingEmail.FolderName;
                        existingEmail.FolderName = cleanFolderName;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Updated folder for existing email: {Subject} from '{OldFolder}' to '{NewFolder}'",
                            existingEmail.Subject, oldFolder, cleanFolderName);
                    }
                    _logger.LogInformation("Email already exists (duplicate) - MessageId: {MessageId}, Subject: {Subject}, From: {From}", 
                        messageId, checkSubject, checkFrom);
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
                
                // Convert timestamp to configured display timezone
                var convertedSentDate = message.SentDateTime.HasValue 
                    ? _dateTimeHelper.ConvertToDisplayTimeZone(message.SentDateTime.Value) 
                    : _dateTimeHelper.ConvertToDisplayTimeZone(DateTime.UtcNow);

                var subject = CleanText(message.Subject ?? "(No Subject)");
                var from = CleanText(message.From?.EmailAddress?.Address ?? string.Empty);
                var to = CleanText(string.Join(", ", message.ToRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>()));
                var cc = CleanText(string.Join(", ", message.CcRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>()));
                var bcc = CleanText(string.Join(", ", message.BccRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>()));

                // Extract body content
                var body = string.Empty;
                var htmlBody = string.Empty;
                var bodyUntruncatedText = string.Empty;
                var bodyUntruncatedHtml = string.Empty;
                var isHtmlTruncated = false;
                var isBodyTruncated = false;

                if (message.Body?.Content != null)
                {
                    if (message.Body.ContentType == BodyType.Html)
                    {
                        var cleanedHtmlBody = CleanText(message.Body.Content);
                        
                        // Check if HTML body needs truncation
                        if (Encoding.UTF8.GetByteCount(cleanedHtmlBody) > 1_000_000)
                        {
                            isHtmlTruncated = true;
                            // Store original in untruncated column
                            bodyUntruncatedHtml = cleanedHtmlBody;
                            // Store truncated version for search index
                            htmlBody = CleanHtmlForStorage(cleanedHtmlBody);
                        }
                        else
                        {
                            htmlBody = cleanedHtmlBody;
                        }

                        // Also extract text version if available
                        var bodyPreview = CleanText(message.BodyPreview ?? "");
                        // Set to 500KB to ensure total of all fields stays under 1MB tsvector limit
                        if (Encoding.UTF8.GetByteCount(bodyPreview) > 500_000)
                        {
                            isBodyTruncated = true;
                            // Store original in untruncated column
                            bodyUntruncatedText = bodyPreview;
                            // Store truncated version for search index
                            body = TruncateTextForStorage(bodyPreview, 500_000);
                        }
                        else
                        {
                            body = bodyPreview;
                        }
                    }
                    else
                    {
                        var cleanedTextBody = CleanText(message.Body.Content);
                        // Set to 500KB to ensure total of all fields stays under 1MB tsvector limit
                        if (Encoding.UTF8.GetByteCount(cleanedTextBody) > 500_000)
                        {
                            isBodyTruncated = true;
                            // Store original in untruncated column
                            bodyUntruncatedText = cleanedTextBody;
                            // Store truncated version for search index
                            body = TruncateTextForStorage(cleanedTextBody, 500_000);
                        }
                        else
                        {
                            body = cleanedTextBody;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(message.BodyPreview))
                {
                    var bodyPreview = CleanText(message.BodyPreview);
                    // Set to 500KB to ensure total of all fields stays under 1MB tsvector limit
                    if (Encoding.UTF8.GetByteCount(bodyPreview) > 500_000)
                    {
                        isBodyTruncated = true;
                        // Store original in untruncated column
                        bodyUntruncatedText = bodyPreview;
                        // Store truncated version for search index
                        body = TruncateTextForStorage(bodyPreview, 500_000);
                    }
                    else
                    {
                        body = bodyPreview;
                    }
                }

                var cleanMessageId = CleanText(messageId);
                var cleanFolderName = CleanText(folderName);

                // Ensure individual fields don't exceed reasonable limits for tsvector
                // This prevents tsvector size errors when all fields are concatenated
                subject = TruncateFieldForTsvector(subject, 50_000); // ~50KB for subject
                from = TruncateFieldForTsvector(from, 10_000); // ~10KB for from
                to = TruncateFieldForTsvector(to, 50_000); // ~50KB for to (can be many recipients)
                cc = TruncateFieldForTsvector(cc, 50_000); // ~50KB for cc
                bcc = TruncateFieldForTsvector(bcc, 50_000); // ~50KB for bcc
                // Body already truncated above to 500KB
                
                // Final safety check: ensure total size for tsvector doesn't exceed limit
                var totalTsvectorSize = Encoding.UTF8.GetByteCount(subject) +
                                       Encoding.UTF8.GetByteCount(body) +
                                       Encoding.UTF8.GetByteCount(from) +
                                       Encoding.UTF8.GetByteCount(to) +
                                       Encoding.UTF8.GetByteCount(cc) +
                                       Encoding.UTF8.GetByteCount(bcc);

                // PostgreSQL tsvector max is ~1MB (1048575 bytes), use 900KB as safe limit
                const int maxTsvectorSize = 900_000;
                if (totalTsvectorSize > maxTsvectorSize)
                {
                    _logger.LogWarning("Email fields exceed tsvector limit ({TotalSize} > {MaxSize}), truncating body further",
                        totalTsvectorSize, maxTsvectorSize);
                    
                    // Calculate how much we need to reduce the body
                    var otherFieldsSize = totalTsvectorSize - Encoding.UTF8.GetByteCount(body);
                    var maxBodySize = maxTsvectorSize - otherFieldsSize - 10_000; // 10KB safety buffer
                    
                    if (maxBodySize > 0 && Encoding.UTF8.GetByteCount(body) > maxBodySize)
                    {
                        isBodyTruncated = true;
                        body = TruncateTextForStorage(body, maxBodySize);
                    }
                    else if (maxBodySize <= 0)
                    {
                        // Other fields alone exceed limit, truncate body completely
                        _logger.LogError("Other email fields alone exceed tsvector limit, body will be saved as attachment only");
                        isBodyTruncated = true;
                        body = "[Body too large - saved as attachment]";
                    }
                }

                // Determine if the email is outgoing by comparing the From address with the account's email address
                bool isOutgoingEmail = !string.IsNullOrEmpty(from) && 
                                      !string.IsNullOrEmpty(account.EmailAddress) && 
                                      from.Equals(account.EmailAddress, StringComparison.OrdinalIgnoreCase);
                
                // Additionally check if the folder indicates outgoing mail
                bool isOutgoingFolder = IsOutgoingFolderByName(folderName);
                
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
                    SentDate = convertedSentDate,
                    ReceivedDate = DateTime.UtcNow,
                    IsOutgoing = (isOutgoingEmail || isOutgoingFolder) && !isDraftsFolder,
                    HasAttachments = false, // Will be set correctly after checking for attachments
                    Body = body,
                    HtmlBody = htmlBody,
                    BodyUntruncatedText = !string.IsNullOrEmpty(bodyUntruncatedText) ? bodyUntruncatedText : null,
                    BodyUntruncatedHtml = !string.IsNullOrEmpty(bodyUntruncatedHtml) ? bodyUntruncatedHtml : null,
                    FolderName = cleanFolderName,
                    Attachments = new List<EmailAttachment>() // Initialize collection for hash calculation
                };

                _context.ArchivedEmails.Add(archivedEmail);
                await _context.SaveChangesAsync();

                // Always try to get attachments, regardless of HasAttachments flag
                // This is important because inline images might not be reflected in HasAttachments
                var attachmentCount = await SaveGraphAttachmentsAsync(graphClient, message.Id, archivedEmail.Id, account.EmailAddress);
                
                // Update HasAttachments flag based on actual attachments found
                archivedEmail.HasAttachments = attachmentCount > 0;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Archived Graph API email: {Subject}, From: {From}, To: {To}, Account: {AccountName}, Truncated: {IsTruncated}",
                    archivedEmail.Subject, archivedEmail.From, archivedEmail.To, account.Name, 
                    isHtmlTruncated || isBodyTruncated ? "Yes" : "No");

                return true; // New email successfully archived
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving Graph API email: Subject={Subject}, From={From}, Error={Message}",
                    message.Subject, message.From?.EmailAddress?.Address, ex.Message);
                return false;
            }
        }

        private async Task<int> SaveGraphAttachmentsAsync(GraphServiceClient graphClient, string messageId, int archivedEmailId, string userPrincipalName)
        {
            var attachmentCount = 0;
            
            try
            {
                _logger.LogDebug("Getting attachments for Graph API message {MessageId}", messageId);
                
                var attachmentsResponse = await graphClient.Users[userPrincipalName].Messages[messageId].Attachments.GetAsync();

                if (attachmentsResponse?.Value != null)
                {
                    _logger.LogDebug("Found {Count} attachments for message {MessageId}", attachmentsResponse.Value.Count, messageId);
                    
                    foreach (var attachment in attachmentsResponse.Value)
                    {
                        try
                        {
                            if (attachment is FileAttachment fileAttachment && fileAttachment.ContentBytes != null)
                            {
                                var cleanFileName = CleanText(fileAttachment.Name ?? "attachment");
                                var contentType = CleanText(fileAttachment.ContentType ?? "application/octet-stream");
                                
                                // Normalize Content-ID by removing angle brackets for consistency with HTML cid: references
                                // In MIME headers: Content-ID: <part1.abc@xyz.com>
                                // In HTML: <img src="cid:part1.abc@xyz.com">
                                // We store without brackets to match HTML references
                                var contentId = !string.IsNullOrEmpty(fileAttachment.ContentId) 
                                    ? fileAttachment.ContentId.Trim().Trim('<', '>') 
                                    : null;
                                if (!string.IsNullOrEmpty(contentId))
                                {
                                    _logger.LogDebug("Found inline attachment with Content-ID: {ContentId}, FileName: {FileName}, ContentType: {ContentType}",
                                        contentId, cleanFileName, contentType);
                                }
                                
                                // Determine if this is inline content using the same logic as EmailService
                                bool isInlineContent = IsGraphInlineContent(fileAttachment);
                                
                                // For inline content without explicit filename, generate a descriptive name
                                if (isInlineContent && (string.IsNullOrEmpty(cleanFileName) || cleanFileName == "attachment"))
                                {
                                    var extension = GetExtensionFromContentType(contentType);
                                    if (!string.IsNullOrEmpty(contentId))
                                    {
                                        // Use Content-ID to create filename
                                        var cidPart = contentId.Trim('<', '>').Split('@')[0];
                                        cleanFileName = $"inline_{cidPart}{extension}";
                                    }
                                    else
                                    {
                                        // Generate filename for inline content without Content-ID
                                        cleanFileName = $"inline_image_{Guid.NewGuid().ToString("N")[..8]}{extension}";
                                    }
                                    _logger.LogDebug("Generated filename for inline content: {FileName}", cleanFileName);
                                }

                                var emailAttachment = new EmailAttachment
                                {
                                    ArchivedEmailId = archivedEmailId,
                                    FileName = cleanFileName,
                                    ContentType = contentType,
                                    Content = fileAttachment.ContentBytes,
                                    Size = fileAttachment.ContentBytes.Length,
                                    ContentId = contentId // Store Content-ID for inline images
                                };

                                _context.EmailAttachments.Add(emailAttachment);
                                attachmentCount++;
                                
                                _logger.LogInformation("Saved Graph API attachment: FileName={FileName}, ContentType={ContentType}, Size={Size}, ContentId={ContentId}, IsInline={IsInline}",
                                    cleanFileName, contentType, fileAttachment.ContentBytes.Length, contentId, isInlineContent);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process Graph API attachment: {AttachmentName}", attachment.Name);
                        }
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogDebug("Successfully saved {Count} attachments for message {MessageId}", attachmentCount, messageId);
                }
                else
                {
                    _logger.LogDebug("No attachments found for message {MessageId}", messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get attachments for Graph API message {MessageId}", messageId);
            }
            
            return attachmentCount;
        }

        /// <summary>
        /// Determines if a Graph API FileAttachment is inline content
        /// </summary>
        private bool IsGraphInlineContent(FileAttachment fileAttachment)
        {
            // Check for Content-ID (the most important criterion for inline content)
            // Content-ID is the standard indicator for inline content, referenced in HTML via cid:
            if (!string.IsNullOrEmpty(fileAttachment.ContentId))
            {
                _logger.LogDebug("Found inline content via Content-ID: {ContentId}, ContentType: {ContentType}, FileName: {FileName}",
                    fileAttachment.ContentId, fileAttachment.ContentType, fileAttachment.Name);
                return true;
            }

            // Fallback: Images with inline characteristics
            if (!string.IsNullOrEmpty(fileAttachment.ContentType) && 
                fileAttachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                // Images are often inline content, especially when they have a Content-ID
                _logger.LogDebug("Found potential inline image: {ContentType}, FileName: {FileName}, ContentId: {ContentId}",
                    fileAttachment.ContentType, fileAttachment.Name, fileAttachment.ContentId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets file extension based on content type
        /// </summary>
        private string GetExtensionFromContentType(string contentType)
        {
            return contentType?.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg", 
                "image/jpg" => ".jpg",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/tiff" => ".tiff",
                "image/svg+xml" => ".svg",
                "image/webp" => ".webp",
                "text/html" => ".html",
                "text/plain" => ".txt",
                "application/pdf" => ".pdf",
                _ => ".dat"
            };
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
                    
                    // Additional test: Try to access mail folders to ensure proper permissions
                    try
                    {
                        var foldersResponse = await graphClient.Users[account.EmailAddress].MailFolders.GetAsync((requestConfiguration) =>
                        {
                            requestConfiguration.QueryParameters.Select = new string[] { "id", "displayName" };
                            requestConfiguration.QueryParameters.Top = 1;
                        });
                        
                        _logger.LogInformation("Mail folder access test passed for account {Name}. Found {FolderCount} folders.",
                            account.Name, foldersResponse?.Value?.Count ?? 0);
                            
                        // Test message access in first available folder
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
                            requestConfiguration.QueryParameters.Top = _batchOptions.BatchSize;
                        });

                        var totalOldEmailsFound = 0;
                        var totalProcessedInFolder = 0;
                        var paginationCount = 0;

                        // Process the initial response and all paginated results
                        while (messagesResponse?.Value != null)
                        {
                            paginationCount++;
                            var currentPageSize = messagesResponse.Value.Count;
                            totalOldEmailsFound += currentPageSize;

                            _logger.LogInformation("Processing page {PageNumber} with {Count} old emails in folder {FolderName} for account {AccountName} (Total found so far: {TotalFound})",
                                paginationCount, currentPageSize, folder.DisplayName, account.Name, totalOldEmailsFound);

                            if (currentPageSize > 0)
                            {
                                // Process messages in current page
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
                                        _logger.LogDebug("Skipping deletion of email with Message-ID {MessageId} from folder {FolderName} (not archived). Account ID: {AccountId}",
                                            messageId, folder.DisplayName, account.Id);
                                    }
                                }

                                if (messageIdsToDelete.Count > 0)
                                {
                                    _logger.LogInformation("Attempting to delete {Count} emails from page {PageNumber} in folder {FolderName} for account {AccountName}",
                                        messageIdsToDelete.Count, paginationCount, folder.DisplayName, account.Name);

                                    // Delete messages using individual requests to avoid rate limiting
                                    foreach (var messageId in messageIdsToDelete)
                                    {
                                        try
                                        {
                                            await graphClient.Users[account.EmailAddress].Messages[messageId].DeleteAsync();
                                            deletedCount++;
                                            totalProcessedInFolder++;
                                            _logger.LogDebug("Successfully deleted email {MessageId} from folder {FolderName}",
                                                messageId, folder.DisplayName);
                                        }
                                        catch (Exception deleteEx)
                                        {
                                            _logger.LogError(deleteEx, "Error deleting email {MessageId} from folder {FolderName} for account {AccountName}",
                                                messageId, folder.DisplayName, account.Name);
                                        }
                                    }

                                    _logger.LogInformation("Successfully processed {Count} emails for deletion from page {PageNumber} in folder {FolderName} for account {AccountName}",
                                        messageIdsToDelete.Count, paginationCount, folder.DisplayName, account.Name);
                                }
                                else
                                {
                                    _logger.LogInformation("No archived emails to delete on page {PageNumber} in folder {FolderName} for account {AccountName}",
                                        paginationCount, folder.DisplayName, account.Name);
                                }
                            }

                            // Check for next page using OdataNextLink
                            if (!string.IsNullOrEmpty(messagesResponse.OdataNextLink))
                            {
                                _logger.LogInformation("Fetching next page of old emails for folder {FolderName} (page {NextPage})", 
                                    folder.DisplayName, paginationCount + 1);

                                // Add a pause between pages to avoid overwhelming the API
                                if (_batchOptions.PauseBetweenBatchesMs > 0)
                                {
                                    await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                                }

                                // Get next page
                                messagesResponse = await graphClient.Users[account.EmailAddress].MailFolders[folder.Id].Messages.WithUrl(messagesResponse.OdataNextLink).GetAsync();
                            }
                            else
                            {
                                // No more pages
                                break;
                            }
                        }

                        _logger.LogInformation("Completed deletion processing for folder {FolderName}. Total emails found: {TotalFound}, Total pages processed: {PagesProcessed}, Emails deleted: {DeletedInFolder}",
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

                // Process the HTML body to ensure inline images are properly referenced
                var processedHtmlBody = email.HtmlBody;
                if (!string.IsNullOrEmpty(email.HtmlBody) && email.Attachments != null && email.Attachments.Any(a => !string.IsNullOrEmpty(a.ContentId)))
                {
                    processedHtmlBody = ProcessHtmlBodyForInlineImages(email.HtmlBody, email.Attachments);
                }

                // Create the message to restore - focus on preserving content first
                var message = new Message
                {
                    Subject = email.Subject ?? "(No Subject)",
                    Body = new ItemBody
                    {
                        ContentType = !string.IsNullOrEmpty(processedHtmlBody) ? BodyType.Html : BodyType.Text,
                        Content = !string.IsNullOrEmpty(processedHtmlBody) ? processedHtmlBody : (email.Body ?? "(No Content)")
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
                // Separate inline attachments from regular attachments
                var inlineAttachments = email.Attachments.Where(a => !string.IsNullOrEmpty(a.ContentId)).ToList();
                var regularAttachments = email.Attachments.Where(a => string.IsNullOrEmpty(a.ContentId)).ToList();
                
                // Add inline attachments first so they can be referenced in the HTML body
                foreach (var attachment in inlineAttachments)
                {
                    try
                    {
                        using var attachmentStream = new MemoryStream(attachment.Content);
                        var mimePart = builder.LinkedResources.Add(attachment.FileName, attachmentStream, 
                            MimeKit.ContentType.Parse(attachment.ContentType ?? "application/octet-stream"));
                        mimePart.ContentId = attachment.ContentId;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to add inline attachment {AttachmentName} to MIME message", attachment.FileName);
                    }
                }
                
                // Add regular attachments
                foreach (var attachment in regularAttachments)
                {
                    try
                    {
                        using var attachmentStream = new MemoryStream(attachment.Content);
                        builder.Attachments.Add(attachment.FileName, attachmentStream, 
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

                        // For inline attachments, ensure ContentId is properly set
                        if (!string.IsNullOrEmpty(attachment.ContentId))
                        {
                            // Ensure Content-ID is properly formatted for Graph API
                            var contentId = attachment.ContentId;
                            if (contentId.StartsWith("<") && contentId.EndsWith(">"))
                            {
                                contentId = contentId.Trim('<', '>');
                            }
                            fileAttachment.ContentId = contentId;
                        }

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

        /// <summary>
        /// Checks if a folder name indicates outgoing mail based on its name in multiple languages
        /// </summary>
        /// <param name="folderName">The folder name to check</param>
        /// <returns>True if the folder name indicates outgoing mail, false otherwise</returns>
        private bool IsOutgoingFolderByName(string folderName)
        {
            var outgoingFolderNames = new[]
            {
                // English
                "outgoing", "sent", "sent items", "sent mail", "outbox",

                // German
                "gesendet", "gesendete objekte", "gesendete nachrichten", "postausgang",

                // French
                "envoyé", "éléments envoyés", "boîte d'envoi", "messages envoyés",

                // Spanish
                "enviados", "elementos enviados", "correo enviado", "bandeja de salida",

                // Italian
                "inviato", "posta inviata", "elementi inviati", "posta in uscita",

                // Dutch
                "verzonden", "verzonden items", "verzonden e-mail", "postvak uit",

                // Russian
                "исходящие", "отправленные", "исходящая почта",

                // Chinese
                "已发送", "发件箱", "已传送",

                // Japanese
                "送信済み", "送信済メール", "送信メール", "送信トレイ",

                // Portuguese
                "enviados", "itens enviados", "mensagens enviadas", "caixa de saída",

                // Arabic
                "الصادر", "المرسلة", "بريد الصادر",

                // Other common variations
                "out", "send"
            };

            string folderNameLower = folderName?.ToLowerInvariant() ?? "";
            return outgoingFolderNames.Any(name => folderNameLower.Contains(name));
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

        /// <summary>
        /// Truncates a single field to ensure it doesn't exceed tsvector limits
        /// </summary>
        /// <param name="text">The field text to truncate</param>
        /// <param name="maxSizeBytes">Maximum size in bytes</param>
        /// <returns>Truncated text</returns>
        private string TruncateFieldForTsvector(string text, int maxSizeBytes)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Check if truncation is needed
            if (Encoding.UTF8.GetByteCount(text) <= maxSizeBytes)
            {
                return text; // No truncation needed
            }

            // Find a safe truncation point
            int approximateCharPosition = Math.Min(maxSizeBytes, text.Length);

            // Work backwards to ensure we don't exceed byte limit
            while (approximateCharPosition > 0 && Encoding.UTF8.GetByteCount(text.Substring(0, approximateCharPosition)) > maxSizeBytes)
            {
                approximateCharPosition--;
            }

            // Try to find a word boundary to avoid breaking in the middle of a word
            int wordBoundarySearch = Math.Max(0, approximateCharPosition - 50);
            int lastSpaceIndex = text.LastIndexOf(' ', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);

            if (lastSpaceIndex > wordBoundarySearch)
            {
                approximateCharPosition = lastSpaceIndex;
            }

            return text.Substring(0, approximateCharPosition) + "...";
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

            // IMPORTANT: Preserve inline images with cid: references
            var imgMatches = System.Text.RegularExpressions.Regex.Matches(html, @"<img[^>]*src\s*=\s*[""']cid:[^""']+[""'][^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            foreach (System.Text.RegularExpressions.Match match in imgMatches)
            {
                int imgEnd = match.Index + match.Length;
                if (imgEnd > truncatePosition && match.Index < truncatePosition && match.Index > maxContentSize / 2)
                {
                    truncatePosition = match.Index;
                    _logger.LogDebug("Adjusted truncation to preserve inline images, truncating before <img> tag at position {Position}", match.Index);
                    break;
                }
            }

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
                // Get the email's attachments to resolve inline images
                var email = await _context.ArchivedEmails
                    .Include(e => e.Attachments)
                    .FirstOrDefaultAsync(e => e.Id == archivedEmailId);

                if (email != null && email.Attachments != null && email.Attachments.Any())
                {
                    // Resolve inline images by converting cid: references to data URLs
                    originalHtml = ResolveInlineImagesInHtml(originalHtml, email.Attachments.ToList());
                }

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

                _logger.LogInformation("Successfully saved original HTML content as attachment for email {EmailId} with {ImageCount} inline images resolved",
                    archivedEmailId, email?.Attachments?.Count(a => !string.IsNullOrEmpty(a.ContentId)) ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save original HTML content as attachment for email {EmailId}", archivedEmailId);
            }
        }

        /// <summary>
        /// Resolves inline images in HTML by converting cid: references to data URLs
        /// </summary>
        private string ResolveInlineImagesInHtml(string htmlBody, List<EmailAttachment> attachments)
        {
            if (string.IsNullOrEmpty(htmlBody) || attachments == null || !attachments.Any())
                return htmlBody;

            var resultHtml = htmlBody;

            // Find all cid: references in the HTML
            var cidMatches = System.Text.RegularExpressions.Regex.Matches(htmlBody, 
                @"src\s*=\s*[""']cid:([^""']+)[""']", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in cidMatches)
            {
                var cid = match.Groups[1].Value;

                // Find the corresponding attachment - try multiple matching strategies
                var attachment = attachments.FirstOrDefault(a => 
                    !string.IsNullOrEmpty(a.ContentId) && 
                    (a.ContentId.Equals($"<{cid}>", StringComparison.OrdinalIgnoreCase) ||
                     a.ContentId.Equals(cid, StringComparison.OrdinalIgnoreCase)));

                // If no attachment with ContentId found, try matching by filename
                if (attachment == null)
                {
                    attachment = attachments.FirstOrDefault(a => 
                        !string.IsNullOrEmpty(a.FileName) && 
                        (a.FileName.Equals($"inline_{cid}", StringComparison.OrdinalIgnoreCase) ||
                         a.FileName.StartsWith($"inline_{cid}.", StringComparison.OrdinalIgnoreCase) ||
                         a.FileName.Contains($"_{cid}")));
                }

                if (attachment != null && attachment.Content != null && attachment.Content.Length > 0)
                {
                    try
                    {
                        // Create a data URL for the inline image
                        var base64Content = Convert.ToBase64String(attachment.Content);
                        var dataUrl = $"data:{attachment.ContentType ?? "image/png"};base64,{base64Content}";
                        
                        // Replace the cid: reference with the data URL
                        resultHtml = resultHtml.Replace(match.Groups[0].Value, $"src=\"{dataUrl}\"");
                        
                        _logger.LogDebug("Resolved inline image with CID: {Cid} to data URL", cid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to resolve inline image with CID: {Cid}", cid);
                    }
                }
                else
                {
                    _logger.LogWarning("Could not find attachment for CID: {Cid}", cid);
                }
            }

            return resultHtml;
        }

        /// <summary>
        /// Processes HTML body to ensure inline images are properly referenced with Content-ID
        /// </summary>
        /// <param name="htmlBody">The HTML body content</param>
        /// <param name="attachments">List of email attachments</param>
        /// <returns>Processed HTML body with proper Content-ID references</returns>
        private string ProcessHtmlBodyForInlineImages(string htmlBody, ICollection<EmailAttachment> attachments)
        {
            if (string.IsNullOrEmpty(htmlBody) || attachments == null || !attachments.Any())
                return htmlBody;

            var resultHtml = htmlBody;

            try
            {
                // Find all cid: references in the HTML
                var cidMatches = System.Text.RegularExpressions.Regex.Matches(htmlBody, 
                    @"src\s*=\s*[""']cid:([^""']+)[""']", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                foreach (System.Text.RegularExpressions.Match match in cidMatches)
                {
                    var cid = match.Groups[1].Value;

                    // Find the corresponding attachment by ContentId
                    var attachment = attachments.FirstOrDefault(a => 
                        !string.IsNullOrEmpty(a.ContentId) && 
                        (a.ContentId.Equals($"<{cid}>", StringComparison.OrdinalIgnoreCase) ||
                         a.ContentId.Equals(cid, StringComparison.OrdinalIgnoreCase)));

                    // If no attachment found by ContentId, try to match by filename
                    if (attachment == null)
                    {
                        attachment = attachments.FirstOrDefault(a => 
                            !string.IsNullOrEmpty(a.FileName) && 
                            (a.FileName.Equals($"inline_{cid}", StringComparison.OrdinalIgnoreCase) ||
                             a.FileName.StartsWith($"inline_{cid}.", StringComparison.OrdinalIgnoreCase) ||
                             a.FileName.Contains($"_{cid}")));
                    }

                    if (attachment != null)
                    {
                        // Ensure the attachment has a proper Content-ID format for referencing
                        if (string.IsNullOrEmpty(attachment.ContentId))
                        {
                            // Generate a Content-ID if not present
                            attachment.ContentId = $"<{Guid.NewGuid()}@mailarchiver>";
                        }
                        else if (!attachment.ContentId.StartsWith("<"))
                        {
                            // Ensure Content-ID is properly formatted
                            attachment.ContentId = $"<{attachment.ContentId}>";
                        }
                        
                        // Update the HTML to reference the attachment by its Content-ID
                        var formattedCid = attachment.ContentId.Trim('<', '>');
                        resultHtml = resultHtml.Replace(match.Groups[0].Value, $"src=\"cid:{formattedCid}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing HTML body for inline images. Returning original HTML.");
                return htmlBody;
            }

            return resultHtml;
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
