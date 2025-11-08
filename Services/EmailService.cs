using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.Utilities;
using MailArchiver.ViewModels;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MailArchiver.Services
{
    public class EmailService : IEmailService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<EmailService> _logger;
        private readonly ISyncJobService _syncJobService;
        private readonly BatchOperationOptions _batchOptions;
        private readonly MailSyncOptions _mailSyncOptions;
        private readonly IGraphEmailService _graphEmailService;
        private readonly DateTimeHelper _dateTimeHelper;

        public EmailService(
            MailArchiverDbContext context,
            ILogger<EmailService> logger,
            ISyncJobService syncJobService,
            IOptions<BatchOperationOptions> batchOptions,
            IOptions<MailSyncOptions> mailSyncOptions,
            IGraphEmailService graphEmailService,
            DateTimeHelper dateTimeHelper)
        {
            _context = context;
            _logger = logger;
            _syncJobService = syncJobService;
            _batchOptions = batchOptions.Value;
            _mailSyncOptions = mailSyncOptions.Value;
            _graphEmailService = graphEmailService;
            _dateTimeHelper = dateTimeHelper;
        }

        /// <summary>
        /// Gets the appropriate username for authentication.
        /// Uses Username if provided, otherwise falls back to EmailAddress.
        /// Note: M365 accounts no longer supported via IMAP - use GraphEmailService.
        /// </summary>
        /// <param name="account">The mail account</param>
        /// <returns>The username to use for authentication</returns>
        private string GetAuthenticationUsername(MailAccount account)
        {
            // Use Username if provided, otherwise fall back to EmailAddress
            return account.Username ?? account.EmailAddress;
        }

        /// <summary>
        /// Authenticates the IMAP client using a fallback authentication strategy.
        /// Note: M365 accounts should use GraphEmailService, not IMAP.
        /// For other providers, tries SASL PLAIN first (for Exchange compatibility), 
        /// then falls back to auto-negotiation if PLAIN fails (for T-Online and others).
        /// </summary>
        /// <param name="client">The IMAP client to authenticate</param>
        /// <param name="account">The mail account with authentication details</param>
        /// <returns>Task</returns>
        private async Task AuthenticateClientAsync(ImapClient client, MailAccount account)
        {
            // Remove GSSAPI and NEGOTIATE mechanisms to prevent Kerberos authentication attempts
            // which can fail in containerized environments due to missing libraries
            client.AuthenticationMechanisms.Remove("GSSAPI");
            client.AuthenticationMechanisms.Remove("NEGOTIATE");
            
            var username = GetAuthenticationUsername(account);
            var password = account.Password;
            
            // Try SASL PLAIN first (preferred for Exchange 2019 and similar servers)
            if (client.AuthenticationMechanisms.Contains("PLAIN"))
            {
                try
                {
                    _logger.LogDebug("Attempting SASL PLAIN authentication for account {AccountName}", account.Name);
                    var credentials = new NetworkCredential(username, password);
                    var saslPlain = new SaslMechanismPlain(credentials);
                    await client.AuthenticateAsync(saslPlain);
                    _logger.LogDebug("SASL PLAIN authentication successful for account {AccountName}", account.Name);
                    return;
                }
                catch (MailKit.Security.AuthenticationException ex)
                {
                    _logger.LogInformation("SASL PLAIN authentication failed for account {AccountName}, trying fallback: {Message}", 
                        account.Name, ex.Message);
                    // Continue to fallback authentication
                }
            }
            else
            {
                _logger.LogInformation("SASL PLAIN not available for account {AccountName}, using fallback authentication", account.Name);
            }
            
            // Fallback: Let MailKit auto-negotiate the best available mechanism
            // This works for T-Online and other providers that don't support PLAIN
            _logger.LogDebug("Using auto-negotiated authentication for account {AccountName}", account.Name);
            await client.AuthenticateAsync(username, password);
        }

        // SyncMailAccountAsync Methode
        public async Task SyncMailAccountAsync(MailAccount account, string? jobId = null)
        {
            _logger.LogInformation("Starting sync for account: {AccountName} (Provider: {Provider})", account.Name, account.Provider);

            using var client = CreateImapClient(account.Name);
            // Increased timeout for M365 connections (5 minutes)
            client.Timeout = 300000;
            client.ServerCertificateValidationCallback = ServerCertificateValidationCallback;

            var processedFolders = 0;
            var processedEmails = 0;
            var newEmails = 0;
            var failedEmails = 0;
            var deletedEmails = 0; // Counter for deleted emails

            try
            {
                await ConnectWithFallbackAsync(client, account.ImapServer, account.ImapPort ?? 993, account.UseSSL, account.Name);
                await AuthenticateClientAsync(client, account);
                _logger.LogInformation("Connected to IMAP server for {AccountName}", account.Name);

                // Use the centralized folder retrieval method
                var allFolders = await GetAllFoldersAsync(client, account.Name);

                if (jobId != null)
                {
                    _syncJobService.UpdateJobProgress(jobId, job =>
                    {
                        job.TotalFolders = allFolders.Count;
                    });
                }

                _logger.LogInformation("Found {Count} folders for account: {AccountName}",
                    allFolders.Count, account.Name);

                // Process each folder
                foreach (var folder in allFolders)
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
                        if (account.ExcludedFoldersList.Contains(folder.FullName))
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

                // Delete old emails from mail server if configured
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

                // Delete old emails from local archive if configured
                if (account.LocalRetentionDays.HasValue && account.LocalRetentionDays.Value > 0)
                {
                    var localDeletedCount = await DeleteOldLocalEmailsAsync(account, jobId);
                    _logger.LogInformation("Deleted {Count} emails from local archive for account {AccountName} based on local retention policy",
                        localDeletedCount, account.Name);
                }

                // Update lastSync only if no individual email failed
                if (failedEmails == 0)
                {
                    account.LastSync = DateTime.UtcNow;
                    _context.Entry(account).Property(a => a.LastSync).IsModified = true;
                    await _context.SaveChangesAsync();
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
                _logger.LogError(ex, "Error during sync for account {AccountName}: {Message}",
                    account.Name, ex.Message);

                if (jobId != null)
                {
                    _syncJobService.CompleteJob(jobId, false, ex.Message);
                }
                throw;
            }
        }

        // NEUE ResyncAccountAsync Methode
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

                _logger.LogInformation("Starting full resync for account {AccountName} (Provider: {Provider})", 
                    account.Name, account.Provider);

                // Reset LastSync to Unix Epoch to force full resync
                account.LastSync = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                _context.Entry(account).Property(a => a.LastSync).IsModified = true;
                await _context.SaveChangesAsync();

                // Start sync job
                var jobId = _syncJobService.StartSync(account.Id, account.Name, account.LastSync);

                // Route to appropriate service based on provider type
                if (account.Provider == ProviderType.M365)
                {
                    _logger.LogInformation("Using Microsoft Graph API for M365 account resync: {AccountName}", account.Name);
                    await _graphEmailService.SyncMailAccountAsync(account, jobId);
                }
                else
                {
                    _logger.LogInformation("Using IMAP for account resync: {AccountName}", account.Name);
                    await SyncMailAccountAsync(account, jobId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during resync for account {AccountId}", accountId);
                return false;
            }
        }

        // NEUE GetEmailCountByAccountAsync Methode
        public async Task<int> GetEmailCountByAccountAsync(int accountId)
        {
            return await _context.ArchivedEmails.CountAsync(e => e.MailAccountId == accountId);
        }

        // MODIFIZIERTE SyncFolderAsync Methode mit Rückgabewerten
        private async Task<SyncFolderResult> SyncFolderAsync(IMailFolder folder, MailAccount account, ImapClient client, string? jobId = null)
        {
            var result = new SyncFolderResult();

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

                // Ensure connection is still active before opening folder
                if (!client.IsConnected)
                {
                    _logger.LogWarning("Client disconnected during sync, attempting to reconnect...");
                    await ReconnectClientAsync(client, account);
                }
                else if (!client.IsAuthenticated)
                {
                    _logger.LogWarning("Client not authenticated, attempting to re-authenticate...");
                    await AuthenticateClientAsync(client, account);
                }

                // Ensure folder is open
                if (!folder.IsOpen)
                {
                    await folder.OpenAsync(FolderAccess.ReadOnly);
                }

                bool isOutgoing = IsOutgoingFolder(folder);
                var lastSync = account.LastSync;
                
                // Subtract 12 hours from lastSync for the query, but only if it's not the Unix epoch (1/1/1970)
                if (lastSync != new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                {
                    lastSync = lastSync.AddHours(-12);
                }
                
                var query = SearchQuery.DeliveredAfter(lastSync);

                try
                {
                    // Ensure connection is still active before searching
                    if (!client.IsConnected)
                    {
                        _logger.LogWarning("Client disconnected during sync, attempting to reconnect...");
                        await ReconnectClientAsync(client, account);
                    }
                    else if (!client.IsAuthenticated)
                    {
                        _logger.LogWarning("Client not authenticated, attempting to re-authenticate...");
                        await AuthenticateClientAsync(client, account);
                    }

                    if (!folder.IsOpen)
                    {
                        await folder.OpenAsync(FolderAccess.ReadOnly);
                    }

                    IList<UniqueId> uids;
                    try
                    {
                        // Try the standard DeliveredAfter query first
                        uids = await folder.SearchAsync(query);
                        _logger.LogDebug("DeliveredAfter search found {Count} messages in folder {FolderName}", 
                            uids.Count, folder.FullName);
                        
                        // Check if this is a full sync (LastSync is Unix Epoch) and the search returned 0 results
                        // but the folder actually contains messages. This indicates the server doesn't support
                        // DeliveredAfter properly
                        if (uids.Count == 0 && folder.Count > 0 && 
                            account.LastSync == new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                        {
                            _logger.LogWarning("DeliveredAfter returned 0 results but folder contains {Count} messages during full sync for {FolderName}. Server may not support DeliveredAfter. Using SearchQuery.All instead.", 
                                folder.Count, folder.FullName);
                            uids = await folder.SearchAsync(SearchQuery.All);
                            _logger.LogInformation("SearchQuery.All found {Count} messages in folder {FolderName}", 
                                uids.Count, folder.FullName);
                        }
                    }
                    catch (Exception searchEx)
                    {
                        // Some servers may not support DeliveredAfter properly
                        _logger.LogWarning(searchEx, "DeliveredAfter search failed for folder {FolderName}, falling back to SentSince", 
                            folder.FullName);
                        
                        try
                        {
                            // Fallback: Try SentSince instead
                            uids = await folder.SearchAsync(SearchQuery.SentSince(lastSync.Date));
                            _logger.LogDebug("SentSince search found {Count} messages in folder {FolderName}", 
                                uids.Count, folder.FullName);
                        }
                        catch (Exception fallbackEx)
                        {
                            // If both fail, get all UIDs and filter client-side
                            _logger.LogWarning(fallbackEx, "SentSince also failed for folder {FolderName}, using All query", 
                                folder.FullName);
                            uids = await folder.SearchAsync(SearchQuery.All);
                            _logger.LogInformation("All query found {Count} total messages in folder {FolderName}, will filter by date client-side", 
                                uids.Count, folder.FullName);
                        }
                    }
                    
                    _logger.LogInformation("Found {Count} messages to process in folder {FolderName} for account: {AccountName}",
                        uids.Count, folder.FullName, account.Name);

                    result.ProcessedEmails = uids.Count;

                    // Process emails in smaller chunks to reduce memory usage
                    for (int i = 0; i < uids.Count; i += _batchOptions.BatchSize)
                    {
                        // Check if job has been cancelled
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

                            // Use using statement to ensure proper disposal of MimeMessage
                            try
                            {
                                // Ensure connection is still active before getting message
                                if (!client.IsConnected)
                                {
                                    _logger.LogWarning("Client disconnected during sync, attempting to reconnect...");
                                    await ReconnectClientAsync(client, account);
                                    // Re-open the folder after reconnection
                                    await folder.OpenAsync(FolderAccess.ReadOnly);
                                }
                                else if (!client.IsAuthenticated)
                                {
                                    _logger.LogWarning("Client not authenticated, attempting to re-authenticate...");
                                    await AuthenticateClientAsync(client, account);
                                }
                                else if (folder.IsOpen == false)
                                {
                                    _logger.LogWarning("Folder is not open, attempting to re-open...");
                                    await folder.OpenAsync(FolderAccess.ReadOnly);
                                }

                                using var message = await folder.GetMessageAsync(uid);
                                var isNew = await ArchiveEmailAsync(account, message, isOutgoing, folder.FullName);
                                if (isNew)
                                {
                                    result.NewEmails++;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error archiving message {MessageNumber} from folder {FolderName}. Message: {Message}",
                                    uid, folder.FullName, ex.Message);
                                result.FailedEmails++;
                            }
                        }

                        // After processing each batch, perform comprehensive cleanup
                        if (i + _batchOptions.BatchSize < uids.Count)
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
                            
                            // Log memory usage after each batch
                            _logger.LogInformation("Memory usage after processing batch {BatchNumber}: {MemoryUsage}",
                                (i / _batchOptions.BatchSize) + 1, MemoryMonitor.GetMemoryUsageFormatted());
                        }
                        else if (_batchOptions.PauseBetweenEmailsMs > 0 && batch.Count > 1)
                        {
                            // Add a small delay between individual emails within the last batch to avoid overwhelming the server
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

        // Helper class für SyncFolderResult
        private class SyncFolderResult
        {
            public int ProcessedEmails { get; set; }
            public int NewEmails { get; set; }
            public int FailedEmails { get; set; }
        }

        private async Task<bool> ArchiveEmailAsync(MailAccount account, MimeMessage message, bool isOutgoing, string? folderName = null)
        {
            // Check if this email is already archived
            var messageId = message.MessageId ??
                $"{message.From}-{message.To}-{message.Subject}-{message.Date.Ticks}";

            var existingEmail = await _context.ArchivedEmails
                .FirstOrDefaultAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);

            if (existingEmail != null)
            {
                // E-Mail existiert bereits, prüfen ob der Ordner geändert wurde
                var cleanFolderName = CleanText(folderName ?? string.Empty);
                if (existingEmail.FolderName != cleanFolderName)
                {
                    // Ordner hat sich geändert, aktualisieren
                    var oldFolder = existingEmail.FolderName;
                    existingEmail.FolderName = cleanFolderName;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated folder for existing email: {Subject} from '{OldFolder}' to '{NewFolder}'",
                        existingEmail.Subject, oldFolder, cleanFolderName);
                }
                return false; // E-Mail existiert bereits
            }

            try
            {
                // Convert timestamp to configured display timezone
                var convertedSentDate = _dateTimeHelper.ConvertToDisplayTimeZone(message.Date);
                var subject = CleanText(message.Subject ?? "(No Subject)");
                // Extract email address from From field
                var fromAddress = message.From?.FirstOrDefault() as MailboxAddress;
                var from = CleanText(fromAddress?.Address ?? string.Empty);
                // Extract email addresses from To field
                var toAddresses = message.To?.Select(m => m as MailboxAddress).Where(m => m != null).Select(m => m.Address) ?? new List<string>();
                var to = CleanText(string.Join(", ", toAddresses));
                // Extract email addresses from Cc field
                var ccAddresses = message.Cc?.Select(m => m as MailboxAddress).Where(m => m != null).Select(m => m.Address) ?? new List<string>();
                var cc = CleanText(string.Join(", ", ccAddresses));
                // Extract email addresses from Bcc field
                var bccAddresses = message.Bcc?.Select(m => m as MailboxAddress).Where(m => m != null).Select(m => m.Address) ?? new List<string>();
                var bcc = CleanText(string.Join(", ", bccAddresses));

                // Extract text and HTML body preserving original encoding
                var body = string.Empty;
                var htmlBody = string.Empty;
                var isHtmlTruncated = false;
                var isBodyTruncated = false;

                // Handle text body - use original content directly to preserve encoding
                if (!string.IsNullOrEmpty(message.TextBody))
                {
                    var cleanedTextBody = CleanText(message.TextBody);
                    // Check if text body needs truncation for tsvector compatibility
                    // Set to 500KB to ensure total of all fields stays under 1MB tsvector limit
                    if (Encoding.UTF8.GetByteCount(cleanedTextBody) > 500_000)
                    {
                        isBodyTruncated = true;
                        body = TruncateTextForStorage(cleanedTextBody, 500_000);
                    }
                    else
                    {
                        body = cleanedTextBody;
                    }
                }
                else if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    // If no TextBody, try to extract text from HTML body
                    var cleanedHtmlAsText = CleanText(message.HtmlBody);
                    // Check if HTML-as-text body needs truncation for tsvector compatibility
                    // Set to 500KB to ensure total of all fields stays under 1MB tsvector limit
                    if (Encoding.UTF8.GetByteCount(cleanedHtmlAsText) > 500_000)
                    {
                        isBodyTruncated = true;
                        body = TruncateTextForStorage(cleanedHtmlAsText, 500_000);
                    }
                    else
                    {
                        body = cleanedHtmlAsText;
                    }
                }

                // Handle HTML body - preserve original encoding (keep cid: references for inline images)
                if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    // Keep the original HTML body with cid: references
                    htmlBody = CleanText(message.HtmlBody);
                    
                    // Check if HTML body will be truncated
                    isHtmlTruncated = htmlBody.Length > 1_000_000;
                    if (isHtmlTruncated)
                    {
                        htmlBody = CleanHtmlForStorage(htmlBody);
                    }
                }

                var cleanMessageId = CleanText(messageId);
                var cleanFolderName = CleanText(folderName ?? string.Empty);

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

                // Sammle ALLE Anhänge einschließlich inline Images
                var allAttachments = new List<MimePart>();
                CollectAllAttachments(message.Body, allAttachments);

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
                    BodyUntruncatedText = isBodyTruncated ? (!string.IsNullOrEmpty(message.TextBody) ? message.TextBody : message.HtmlBody) : null,
                    BodyUntruncatedHtml = isHtmlTruncated ? message.HtmlBody : null,
                    FolderName = cleanFolderName,
                    Attachments = new List<EmailAttachment>() // Initialize collection for hash calculation
                };

                try
                {
                    _context.ArchivedEmails.Add(archivedEmail);
                    await _context.SaveChangesAsync();

                    // Speichere ALLE Anhänge als normale Attachments (including inline images)
                    if (allAttachments.Any())
                    {
                        await SaveAllAttachments(allAttachments, archivedEmail.Id);
                    }

                    _logger.LogInformation(
                        "Archived email: {Subject}, From: {From}, To: {To}, Account: {AccountName}, Attachments: {AttachmentCount}, Truncated: {IsTruncated}",
                        archivedEmail.Subject, archivedEmail.From, archivedEmail.To, account.Name, allAttachments.Count, 
                        isHtmlTruncated || isBodyTruncated ? "Yes" : "No");

                    return true; // Neue E-Mail erfolgreich archiviert
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving archived email to database: {Subject}, {Message}", subject, ex.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving email: Subject={Subject}, From={From}, Error={Message}",
                    message.Subject, message.From, ex.Message);
                return false;
            }
        }

        // Neue Methode zum rekursiven Sammeln ALLER Anhänge
        private void CollectAllAttachments(MimeEntity entity, List<MimePart> attachments)
        {
            if (entity is MimePart mimePart)
            {
                // Sammle normale Attachments
                if (mimePart.IsAttachment)
                {
                    attachments.Add(mimePart);
                    _logger.LogDebug("Found attachment: FileName={FileName}, ContentType={ContentType}",
                        mimePart.FileName, mimePart.ContentType?.MimeType);
                }
                // Sammle inline Images und andere inline Content
                else if (IsInlineContent(mimePart))
                {
                    attachments.Add(mimePart);
                    _logger.LogDebug("Found inline content: ContentId={ContentId}, ContentType={ContentType}, FileName={FileName}",
                        mimePart.ContentId, mimePart.ContentType?.MimeType, mimePart.FileName);
                }
            }
            else if (entity is Multipart multipart)
            {
                // Rekursiv durch alle Teile einer Multipart-Nachricht gehen
                foreach (var child in multipart)
                {
                    CollectAllAttachments(child, attachments);
                }
            }
            else if (entity is MessagePart messagePart)
            {
                // Auch in eingebetteten Nachrichten suchen
                CollectAllAttachments(messagePart.Message.Body, attachments);
            }
        }

        // Hilfsmethode um inline Content zu identifizieren
        private bool IsInlineContent(MimePart mimePart)
        {
            // Prüfe Content-Disposition auf "inline"
            if (mimePart.ContentDisposition?.Disposition?.Equals("inline", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogDebug("Found inline content via Content-Disposition: inline - {ContentType}, ContentId: {ContentId}", 
                    mimePart.ContentType?.MimeType, mimePart.ContentId);
                return true;
            }

            // Prüfe auf Content-ID (das wichtigste Kriterium für inline Images)
            // Content-ID ist der Standard-Indikator für inline Content, der in HTML via cid: referenziert wird
            if (!string.IsNullOrEmpty(mimePart.ContentId))
            {
                _logger.LogDebug("Found inline content via Content-ID: {ContentId}, ContentType: {ContentType}, FileName: {FileName}", 
                    mimePart.ContentId, mimePart.ContentType?.MimeType, mimePart.FileName);
                return true;
            }

            // Fallback: Images ohne Content-ID aber mit inline disposition (falls Content-ID fehlt)
            if (mimePart.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true &&
                mimePart.ContentDisposition?.Disposition?.Equals("attachment", StringComparison.OrdinalIgnoreCase) != true)
            {
                // Nur wenn es nicht explizit als attachment markiert ist
                _logger.LogDebug("Found potential inline image without Content-ID: {ContentType}, FileName: {FileName}", 
                    mimePart.ContentType?.MimeType, mimePart.FileName);
                return true;
            }

            return false;
        }

        // Methode zum Speichern aller Anhänge als normale Attachments
        private async Task SaveAllAttachments(List<MimePart> attachments, int archivedEmailId)
        {
            var emailAttachments = new List<EmailAttachment>();
            
            foreach (var attachment in attachments)
            {
                try
                {
                    // Use using statement for proper MemoryStream disposal
                    using var ms = new MemoryStream();
                    await attachment.Content.DecodeToAsync(ms);

                    // Bestimme den Dateinamen
                    var fileName = attachment.FileName;
                    if (string.IsNullOrEmpty(fileName))
                    {
                        // Generiere Dateinamen für inline Content ohne Namen
                        if (!string.IsNullOrEmpty(attachment.ContentId))
                        {
                            var extension = GetFileExtensionFromContentType(attachment.ContentType?.MimeType);
                            var cleanContentId = attachment.ContentId.Trim('<', '>');
                            fileName = $"inline_{cleanContentId}{extension}";
                        }
                        else if (attachment.ContentType?.MediaType?.StartsWith("image/") == true)
                        {
                            var extension = GetFileExtensionFromContentType(attachment.ContentType.MimeType);
                            fileName = $"inline_image_{Guid.NewGuid().ToString("N")[..8]}{extension}";
                        }
                        else
                        {
                            var extension = GetFileExtensionFromContentType(attachment.ContentType?.MimeType);
                            fileName = $"attachment_{Guid.NewGuid().ToString("N")[..8]}{extension}";
                        }
                    }

                    var cleanFileName = CleanText(fileName);
                    var contentType = CleanText(attachment.ContentType?.MimeType ?? "application/octet-stream");
                    
                    // Preserve Content-ID exactly as it comes from the email (including angle brackets if present)
                    // This is important for inline images referenced via cid: in HTML
                    var contentId = !string.IsNullOrEmpty(attachment.ContentId) ? attachment.ContentId.Trim() : null;

                    var emailAttachment = new EmailAttachment
                    {
                        ArchivedEmailId = archivedEmailId,
                        FileName = cleanFileName,
                        ContentType = contentType,
                        ContentId = contentId,
                        Content = ms.ToArray(),
                        Size = ms.Length
                    };

                    emailAttachments.Add(emailAttachment);

                    _logger.LogDebug("Prepared attachment for saving: FileName={FileName}, Size={Size}, ContentType={ContentType}, ContentId={ContentId}",
                        cleanFileName, ms.Length, contentType, contentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process attachment: FileName={FileName}, ContentType={ContentType}, ContentId={ContentId}",
                        attachment.FileName, attachment.ContentType?.MimeType, attachment.ContentId);
                }
            }

            // Add all attachments in one batch to reduce database round trips
            if (emailAttachments.Any())
            {
                try
                {
                    _context.EmailAttachments.AddRange(emailAttachments);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Successfully saved {Count} attachments for email {EmailId}",
                        emailAttachments.Count, archivedEmailId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save attachments batch for email {EmailId}", archivedEmailId);
                }
                finally
                {
                    // Clear the list to help GC
                    emailAttachments.Clear();
                }
            }
        }

        // Methode zum Speichern des ursprünglichen HTML-Codes als Anhang, wenn er gekürzt wurde
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
            var cidMatches = Regex.Matches(htmlBody, @"src\s*=\s*[""']cid:([^""']+)[""']", RegexOptions.IgnoreCase);

            foreach (Match match in cidMatches)
            {
                var cid = match.Groups[1].Value;

                // Find the corresponding attachment
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

        // Hilfsmethode für Dateierweiterungen
        private string GetFileExtensionFromContentType(string? contentType)
        {
            return contentType?.ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                "image/tiff" => ".tiff",
                "image/ico" or "image/x-icon" => ".ico",
                "text/html" => ".html",
                "text/plain" => ".txt",
                "text/css" => ".css",
                "application/pdf" => ".pdf",
                "application/zip" => ".zip",
                "application/json" => ".json",
                "application/xml" => ".xml",
                _ => ".dat"
            };
        }
        private bool IsOutgoingFolder(IMailFolder folder)
        {
            var sentFolderNames = new[]
            {
                // Arabic
                "المرسلة", "البريد المرسل",

                // Bulgarian
                "изпратени", "изпратена поща",

                // Chinese (Simplified)
                "已发送", "已传送",

                // Croatian
                "poslano", "poslana pošta",

                // Czech
                "odeslané", "odeslaná pošta",

                // Danish
                "sendt", "sendte elementer",

                // Dutch
                "verzonden", "verzonden items", "verzonden e-mail",

                // English
                "sent", "sent items", "sent mail",

                // Estonian
                "saadetud", "saadetud kirjad",

                // Finnish
                "lähetetyt", "lähetetyt kohteet",

                // French
                "envoyé", "éléments envoyés", "mail envoyé",

                // German
                "gesendet", "gesendete objekte", "gesendete",

                // Greek
                "απεσταλμένα", "σταλμένα", "σταλμένα μηνύματα",

                // Hebrew
                "נשלחו", "דואר יוצא",

                // Hungarian
                "elküldött", "elküldött elemek",

                // Irish
                "seolta", "r-phost seolta",

                // Italian
                "inviato", "posta inviata", "elementi inviati",

                // Japanese
                "送信済み", "送信済メール", "送信メール",

                // Korean
                "보낸편지함", "발신함", "보낸메일",

                // Latvian
                "nosūtītie", "nosūtītās vēstules",

                // Lithuanian
                "išsiųsta", "išsiųsti laiškai",

                // Maltese
                "mibgħuta", "posta mibgħuta",

                // Norwegian
                "sendt", "sendte elementer",

                // Polish
                "wysłane", "elementy wysłane",

                // Portuguese
                "enviados", "itens enviados", "mensagens enviadas",

                // Romanian
                "trimise", "elemente trimise", "mail trimis",

                // Russian
                "отправленные", "исходящие", "отправлено",

                // Slovak
                "odoslané", "odoslaná pošta",

                // Slovenian
                "poslano", "poslana pošta",

                // Spanish
                "enviado", "elementos enviados", "correo enviado",

                // Swedish
                "skickat", "skickade objekt",

                // Turkish
                "gönderilen", "gönderilmiş öğeler"
            };

            string folderNameLower = folder.Name.ToLowerInvariant();
            return sentFolderNames.Any(name => folderNameLower.Contains(name)) ||
                   folder.Attributes.HasFlag(FolderAttributes.Sent);
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

        /// <summary>
        /// Retrieves all folders from an IMAP account using a robust fallback strategy.
        /// This method uses the most reliable approach for retrieving folders across different IMAP server implementations.
        /// </summary>
        /// <param name="client">The connected and authenticated IMAP client</param>
        /// <param name="accountName">The account name for logging purposes</param>
        /// <returns>A list of all selectable folders</returns>
        private async Task<List<IMailFolder>> GetAllFoldersAsync(ImapClient client, string accountName)
        {
            var allFolders = new List<IMailFolder>();

            try
            {
                _logger.LogInformation("Retrieving all folders from IMAP server for account: {AccountName}", accountName);

                // IMPORTANT: First always try to add INBOX explicitly
                // Some IMAP servers have INBOX as a special folder
                try
                {
                    var inbox = client.Inbox;
                    if (inbox != null)
                    {
                        _logger.LogInformation("Adding INBOX explicitly: {FullName}", inbox.FullName);
                        if (!inbox.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                            !inbox.Attributes.HasFlag(FolderAttributes.NoSelect))
                        {
                            allFolders.Add(inbox);
                        }
                    }
                }
                catch (Exception inboxEx)
                {
                    _logger.LogWarning(inboxEx, "Could not access INBOX for {AccountName}", accountName);
                }

                if (client.PersonalNamespaces != null && client.PersonalNamespaces.Count > 0)
                {
                    var ns = client.PersonalNamespaces[0];
                    _logger.LogDebug("Using PersonalNamespace: {Path}", ns.Path);

                    // New Hybrid folder retrieval
                    try
                    {
                        // Recursive fetch - explicitly include non-subscribed folders
                        var rootFolders = await client.GetFoldersAsync(ns, StatusItems.None, subscribedOnly: false);
                        _logger.LogInformation("GetFoldersAsync(including non-subscribed) returned {Count} folders", rootFolders.Count);

                        foreach (var folder in rootFolders)
                        {
                            _logger.LogDebug("Found folder: Name={Name}, FullName={FullName}, Attributes={Attributes}",
                                folder.Name ?? "NULL", folder.FullName ?? "NULL", folder.Attributes);

                            if (!folder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                                !folder.Attributes.HasFlag(FolderAttributes.NoSelect) &&
                                !allFolders.Any(f => f.FullName == folder.FullName))
                            {
                                allFolders.Add(folder);
                            }
                        }
                    }
                    catch (Exception getFoldersEx)
                    {
                        _logger.LogWarning(getFoldersEx, "GetFoldersAsync(recursive) failed for {AccountName}, trying non-recursive fallback", accountName);

                        try
                        {
                            // Fallback non-recursive
                            var toProcess = new Queue<IMailFolder>();

                            // Top-level folder retrieval - explicitly include non-subscribed folders
                            var topFolders = await client.GetFoldersAsync(ns, StatusItems.None, subscribedOnly: false);
                            _logger.LogInformation("Fallback: got {Count} top-level folders (including non-subscribed)", topFolders.Count);

                            foreach (var topFolder in topFolders)
                            {
                                _logger.LogDebug("Found top-level folder: Name={Name}, FullName={FullName}, Attributes={Attributes}",
                                    topFolder.Name ?? "NULL", topFolder.FullName ?? "NULL", topFolder.Attributes);

                                if (!topFolder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                                    !topFolder.Attributes.HasFlag(FolderAttributes.NoSelect) &&
                                    !allFolders.Any(f => f.FullName == topFolder.FullName))
                                {
                                    allFolders.Add(topFolder);
                                    toProcess.Enqueue(topFolder);
                                }
                            }

                            while (toProcess.Count > 0)
                            {
                                var currentFolder = toProcess.Dequeue();
                                try
                                {
                                    // Fetch subfolders without recursion
                                    var subFolders = await currentFolder.GetSubfoldersAsync(false);
                                    foreach (var subFolder in subFolders)
                                    {
                                        _logger.LogDebug("Found subfolder: Name={Name}, FullName={FullName}, Attributes={Attributes}",
                                            subFolder.Name ?? "NULL", subFolder.FullName ?? "NULL", subFolder.Attributes);

                                        if (!subFolder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                                            !subFolder.Attributes.HasFlag(FolderAttributes.NoSelect) &&
                                            !allFolders.Any(f => f.FullName == subFolder.FullName))
                                        {
                                            allFolders.Add(subFolder);
                                            toProcess.Enqueue(subFolder); // Add to queue
                                        }
                                    }
                                }
                                catch (Exception subEx)
                                {
                                    _logger.LogWarning(subEx, "Could not get subfolders for {Folder}", currentFolder.FullName);
                                }
                            }
                        }
                        catch (Exception fallbackEx)
                        {
                            _logger.LogError(fallbackEx, "Fallback also failed for {AccountName}", accountName);
                        }
                    }

                    // If GetFoldersAsync returned 0 folders, use alternative method
                    if (allFolders.Count <= 1) // Only INBOX or less
                    {
                        _logger.LogInformation("Few folders found via GetFoldersAsync, trying alternative folder discovery method for {AccountName}", accountName);

                        try
                        {
                            // Get the root folder from the namespace path
                            var rootFolder = await client.GetFolderAsync(ns.Path ?? string.Empty);
                            _logger.LogDebug("Got root folder: {FullName}", rootFolder.FullName);

                            // Recursively get all subfolders using simple method
                            await AddSubfoldersRecursivelySimple(rootFolder, allFolders);
                            _logger.LogInformation("Alternative method found {Count} additional folders", allFolders.Count - 1);
                        }
                        catch (Exception altEx)
                        {
                            _logger.LogWarning(altEx, "Alternative folder discovery method also failed for {AccountName}", accountName);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No PersonalNamespaces available for account {AccountName}", accountName);
                }

                _logger.LogInformation("Total selectable folders found for {AccountName}: {Count}", accountName, allFolders.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving folders for {AccountName}: {Message}", accountName, ex.Message);
            }

            return allFolders;
        }

        /// <summary>
        /// Simple recursive helper method for retrieving subfolders.
        /// Used as a last resort when modern IMAP methods fail.
        /// </summary>
        private async Task AddSubfoldersRecursivelySimple(IMailFolder folder, List<IMailFolder> allFolders)
        {
            try
            {
                var subfolders = folder.GetSubfolders(false);
                foreach (var subfolder in subfolders)
                {
                    if (!subfolder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                        !subfolder.Attributes.HasFlag(FolderAttributes.NoSelect))
                    {
                        allFolders.Add(subfolder);
                    }
                    await AddSubfoldersRecursivelySimple(subfolder, allFolders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving subfolders for {FolderName}: {Message}",
                    folder.FullName, ex.Message);
            }
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Remove null characters
            text = text.Replace("\0", "");

            // Use a more encoding-safe approach to remove control characters
            // Only remove control characters except for common whitespace characters
            // This preserves extended ASCII and Unicode characters
            var cleanedText = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                // Keep common whitespace characters and anything above the control character range
                if (c == '\r' || c == '\n' || c == '\t' || c >= 32)
                {
                    cleanedText.Append(c);
                }
                else
                {
                    // Replace other control characters with space
                    cleanedText.Append(' ');
                }
            }

            return cleanedText.ToString();
        }

        // Constants for HTML truncation - calculated once to avoid repeated computations
        private static readonly string TruncationNotice = @"
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

        private static readonly int TruncationOverhead = Encoding.UTF8.GetByteCount(TruncationNotice + "</body></html>");
        private const int MaxHtmlSizeBytes = 1_000_000;

        private string CleanHtmlForStorage(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Remove null characters efficiently - only if they exist
            if (html.Contains('\0'))
            {
                html = html.Replace("\0", "");
            }

            // Early return for small HTML content
            if (html.Length <= MaxHtmlSizeBytes)
                return html;

            // Calculate safe truncation position
            int maxContentSize = MaxHtmlSizeBytes - TruncationOverhead;
            if (maxContentSize <= 0)
            {
                // Fallback for edge case - return minimal valid HTML
                return $"<html><body>{TruncationNotice}</body></html>";
            }

            int truncatePosition = Math.Min(maxContentSize, html.Length);

            // IMPORTANT: Preserve inline images with cid: references
            // Find all <img> tags with cid: references and ensure they're included
            var imgMatches = Regex.Matches(html, @"<img[^>]*src\s*=\s*[""']cid:[^""']+[""'][^>]*>", RegexOptions.IgnoreCase);
            
            // Find the last complete <img> tag with cid: before truncation point
            int lastCompleteImgEnd = -1;
            foreach (Match match in imgMatches)
            {
                int imgEnd = match.Index + match.Length;
                if (imgEnd <= truncatePosition)
                {
                    lastCompleteImgEnd = imgEnd;
                }
                else
                {
                    // This image would be cut off, adjust truncation point if possible
                    if (match.Index < truncatePosition && match.Index > maxContentSize / 2)
                    {
                        // If the image starts before truncation but ends after, try to truncate before it
                        truncatePosition = match.Index;
                        _logger.LogDebug("Adjusted truncation to preserve inline images, truncating before <img> tag at position {Position}", match.Index);
                    }
                    break;
                }
            }

            // Find safe truncation point that doesn't break HTML tags
            int lastLessThan = html.LastIndexOf('<', truncatePosition - 1);
            int lastGreaterThan = html.LastIndexOf('>', truncatePosition - 1);

            // If we're inside a tag, truncate before it starts
            if (lastLessThan > lastGreaterThan && lastLessThan >= 0)
            {
                truncatePosition = lastLessThan;
            }
            else if (lastGreaterThan >= 0)
            {
                // Otherwise, truncate after the last complete tag
                truncatePosition = lastGreaterThan + 1;
            }

            // Use StringBuilder for efficient string building
            var result = new StringBuilder(truncatePosition + TruncationNotice.Length + 50);

            // Get base content as span for better performance
            ReadOnlySpan<char> baseContent = html.AsSpan(0, truncatePosition);

            // Check for HTML structure efficiently
            bool hasHtml = baseContent.Contains("<html".AsSpan(), StringComparison.OrdinalIgnoreCase);
            bool hasBody = baseContent.Contains("<body".AsSpan(), StringComparison.OrdinalIgnoreCase);

            // Build the result efficiently
            if (!hasHtml)
            {
                result.Append("<html>");
            }

            if (!hasBody)
            {
                if (hasHtml)
                {
                    // Find where to insert <body> tag efficiently
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

            // Add truncation notice
            result.Append(TruncationNotice);

            // Close tags efficiently
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

        /// <summary>
        /// Truncates text content to fit within tsvector size limits while preserving readability
        /// </summary>
        /// <param name="text">The text to truncate</param>
        /// <param name="maxSizeBytes">Maximum size in bytes</param>
        /// <returns>Truncated text with notice appended</returns>
        private string TruncateTextForStorage(string text, int maxSizeBytes)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            const string textTruncationNotice = "\n\n[CONTENT TRUNCATED - This email contains very large text content that has been truncated for better performance. The complete original content has been saved as an attachment.]";

            // Calculate overhead for the truncation notice
            int noticeOverhead = Encoding.UTF8.GetByteCount(textTruncationNotice);
            int maxContentSize = maxSizeBytes - noticeOverhead;

            if (maxContentSize <= 0)
            {
                // Edge case - just return the notice
                return textTruncationNotice;
            }

            // Check if we need to truncate
            if (Encoding.UTF8.GetByteCount(text) <= maxSizeBytes)
            {
                return text; // No truncation needed
            }

            // Find a safe truncation point that doesn't break in the middle of a word
            int approximateCharPosition = Math.Min(maxContentSize, text.Length);

            // Work backwards to find a word boundary or reasonable break point
            while (approximateCharPosition > 0 && Encoding.UTF8.GetByteCount(text.Substring(0, approximateCharPosition)) > maxContentSize)
            {
                approximateCharPosition--;
            }

            // Try to find a word boundary within the last 100 characters to avoid breaking words
            int wordBoundarySearch = Math.Max(0, approximateCharPosition - 100);
            int lastSpaceIndex = text.LastIndexOf(' ', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);
            int lastNewlineIndex = text.LastIndexOf('\n', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);
            int lastPunctuationIndex = text.LastIndexOfAny(new char[] { '.', '!', '?', ';' }, approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);

            // Use the best break point found
            int breakPoint = Math.Max(Math.Max(lastSpaceIndex, lastNewlineIndex), lastPunctuationIndex);
            if (breakPoint > wordBoundarySearch)
            {
                approximateCharPosition = breakPoint + 1; // Include the break character
            }

            // Final safety check to ensure we don't exceed byte limit
            string truncatedContent = text.Substring(0, approximateCharPosition);
            while (Encoding.UTF8.GetByteCount(truncatedContent + textTruncationNotice) > maxSizeBytes && truncatedContent.Length > 0)
            {
                truncatedContent = truncatedContent.Substring(0, truncatedContent.Length - 1);
            }

            return truncatedContent + textTruncationNotice;
        }

        /// <summary>
        /// Saves the original text content as an attachment when it was truncated
        /// </summary>
        /// <param name="originalText">The original text content</param>
        /// <param name="archivedEmailId">The email ID to attach to</param>
        /// <returns>Task</returns>
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

        /// <summary>
        /// Sanitizes search terms for PostgreSQL full-text search
        /// Converts user input to a valid tsquery format
        /// </summary>
        /// <param name="searchTerm">User input search term</param>
        /// <returns>Sanitized tsquery string or null if invalid</returns>
        private string SanitizeSearchTerm(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                _logger.LogDebug("Search term is null or whitespace, returning null");
                return null;
            }

            _logger.LogDebug("Sanitizing search term: '{SearchTerm}'", searchTerm);

            // Remove or escape special characters that could break tsquery
            // Keep alphanumeric, spaces, and common punctuation that's safe
            var sanitized = System.Text.RegularExpressions.Regex.Replace(searchTerm, @"[^\w\s\u00C0-\u017F\-']", " ", RegexOptions.None);
            _logger.LogDebug("After regex replacement: '{Sanitized}'", sanitized);

            // Trim extra whitespace
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\s+", " ", RegexOptions.None).Trim();
            _logger.LogDebug("After whitespace trim: '{Sanitized}'", sanitized);

            if (string.IsNullOrEmpty(sanitized))
            {
                _logger.LogDebug("Sanitized term is empty, returning null");
                return null;
            }

            // Convert to tsquery format:
            // 1. Split by spaces
            // 2. Join with & (AND operator)
            // 3. Escape single quotes
            var terms = sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _logger.LogDebug("Split into {Count} terms: {Terms}", terms.Length, string.Join(", ", terms));

            if (terms.Length == 0)
            {
                _logger.LogDebug("No terms after split, returning null");
                return null;
            }

            // For single term, just escape quotes
            if (terms.Length == 1)
            {
                var singleResult = terms[0].Replace("'", "''");
                _logger.LogDebug("Single term result: '{Result}'", singleResult);
                return singleResult;
            }

            // For multiple terms, join with & (AND)
            var escapedTerms = terms.Select(t => t.Replace("'", "''"));
            var multiResult = string.Join(" & ", escapedTerms);
            _logger.LogDebug("Multiple terms result: '{Result}'", multiResult);
            return multiResult;
        }

        public async Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsAsync(
            string searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            string folderName,
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null,
            string sortBy = "SentDate",
            string sortOrder = "desc")
        {
            var startTime = DateTime.UtcNow;

            // Validate pagination parameters to prevent excessive data loading
            if (take > 1000) take = 1000; // Limit maximum page size
            if (skip < 0) skip = 0;

            try
            {
                // Use optimized raw SQL query for better performance
                return await SearchEmailsOptimizedAsync(searchTerm, fromDate, toDate, accountId, folderName, isOutgoing, skip, take, allowedAccountIds, sortBy, sortOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Optimized search failed, falling back to Entity Framework search");

                // Fallback to original Entity Framework approach
                return await SearchEmailsEFAsync(searchTerm, fromDate, toDate, accountId, folderName, isOutgoing, skip, take, allowedAccountIds);
            }
        }

        private async Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsOptimizedAsync(
            string searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            string folderName,
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null,
            string sortBy = "SentDate",
            string sortOrder = "desc")
        {
            var startTime = DateTime.UtcNow;

            // Build the raw SQL query that directly uses the GIN index
            var whereConditions = new List<string>();
            var parameters = new List<Npgsql.NpgsqlParameter>();
            var paramCounter = 0;

            // Full-text search condition (handles individual words, quoted phrases, and field-specific searches)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var (tsQuery, phrases, fieldSearches, fieldPhrases) = ParseSearchTermForTsQuery(searchTerm);
                var searchConditions = new List<string>();

                // Handle individual words with tsquery (uses GIN index for global search)
                if (!string.IsNullOrEmpty(tsQuery))
                {
                    searchConditions.Add($@"
                        to_tsvector('simple', 
                            COALESCE(""Subject"", '') || ' ' || 
                            COALESCE(""Body"", '') || ' ' || 
                            COALESCE(""From"", '') || ' ' || 
                            COALESCE(""To"", '') || ' ' || 
                            COALESCE(""Cc"", '') || ' ' || 
                            COALESCE(""Bcc"", '')) 
                        @@ to_tsquery('simple', @param{paramCounter})");
                    parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", tsQuery));
                    paramCounter++;
                    _logger.LogDebug("Added tsquery condition for individual words: {TsQuery}", tsQuery);
                }

                // Handle exact phrases with POSITION (exact string matching across all fields)
                foreach (var phrase in phrases)
                {
                    searchConditions.Add($@"(
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Subject"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Body"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""From"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""To"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Cc"", ''))) > 0 OR
                        POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""Bcc"", ''))) > 0
                    )");
                    parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", phrase));
                    paramCounter++;
                    _logger.LogDebug("Added exact phrase condition for: '{Phrase}'", phrase);
                }

                // Handle field-specific word searches
                foreach (var fieldSearch in fieldSearches)
                {
                    var field = fieldSearch.Key;
                    var terms = fieldSearch.Value;
                    var columnName = GetColumnNameForField(field);

                    if (!string.IsNullOrEmpty(columnName))
                    {
                        foreach (var term in terms)
                        {
                            searchConditions.Add($@"
                                to_tsvector('simple', COALESCE(""{columnName}"", '')) 
                                @@ to_tsquery('simple', @param{paramCounter})");
                            parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", term.Replace("'", "''")));
                            paramCounter++;
                            _logger.LogDebug("Added field-specific tsquery condition for {Field}: {Term}", field, term);
                        }
                    }
                }

                // Handle field-specific phrase searches  
                foreach (var fieldPhrase in fieldPhrases)
                {
                    var field = fieldPhrase.Key;
                    var currentFieldPhrases = fieldPhrase.Value;
                    var columnName = GetColumnNameForField(field);

                    if (!string.IsNullOrEmpty(columnName))
                    {
                        foreach (var phrase in currentFieldPhrases)
                        {
                            searchConditions.Add($@"
                                POSITION(LOWER(@param{paramCounter}) IN LOWER(COALESCE(""{columnName}"", ''))) > 0");
                            parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", phrase));
                            paramCounter++;
                            _logger.LogDebug("Added field-specific phrase condition for {Field}: '{Phrase}'", field, phrase);
                        }
                    }
                }

                if (searchConditions.Any())
                {
                    whereConditions.Add($"({string.Join(" AND ", searchConditions)})");
                    _logger.LogInformation("Using optimized search for term: {SearchTerm} (individual words: {HasWords}, phrases: {PhraseCount}, field searches: {FieldSearches}, field phrases: {FieldPhrases})",
                        searchTerm, !string.IsNullOrEmpty(tsQuery), phrases.Count, fieldSearches.Count, fieldPhrases.Count);
                }
            }

            // Account filtering
            if (allowedAccountIds != null)
            {
                if (allowedAccountIds.Any())
                {
                    whereConditions.Add($@"""MailAccountId"" = ANY(@param{paramCounter})");
                    parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", allowedAccountIds.ToArray()));
                    paramCounter++;
                }
                else
                {
                    // User has no access to any accounts
                    return (new List<ArchivedEmail>(), 0);
                }
            }
            else if (accountId.HasValue)
            {
                // Additional check to ensure user has access to the requested account
                if (allowedAccountIds != null && allowedAccountIds.Any() && !allowedAccountIds.Contains(accountId.Value))
                {
                    // User doesn't have access to this account, return empty results
                    return (new List<ArchivedEmail>(), 0);
                }
                whereConditions.Add($@"""MailAccountId"" = @param{paramCounter}");
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", accountId.Value));
                paramCounter++;
            }

            // Date filtering (these will use the composite indexes)
            if (fromDate.HasValue)
            {
                whereConditions.Add($@"""SentDate"" >= @param{paramCounter}");
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", fromDate.Value));
                paramCounter++;
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.AddDays(1).AddSeconds(-1);
                whereConditions.Add($@"""SentDate"" <= @param{paramCounter}");
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", endDate));
                paramCounter++;
            }

            if (isOutgoing.HasValue)
            {
                whereConditions.Add($@"""IsOutgoing"" = @param{paramCounter}");
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", isOutgoing.Value));
                paramCounter++;
            }

            // Folder filtering
            if (!string.IsNullOrEmpty(folderName))
            {
                whereConditions.Add($@"""FolderName"" = @param{paramCounter}");
                parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", folderName));
                paramCounter++;
            }

            var whereClause = whereConditions.Any() ? "WHERE " + string.Join(" AND ", whereConditions) : "";

            // Count query (optimized)
            var countSql = $@"
                SELECT COUNT(*)
                FROM mail_archiver.""ArchivedEmails""
                {whereClause}";

            var countStartTime = DateTime.UtcNow;
            var totalCount = await ExecuteScalarQueryAsync<int>(countSql, CloneParameters(parameters));
            var countDuration = DateTime.UtcNow - countStartTime;
            _logger.LogInformation("Optimized count query took {Duration}ms for {Count} matching records",
                countDuration.TotalMilliseconds, totalCount);

            // Build dynamic ORDER BY clause based on sortBy and sortOrder parameters
            var orderByClause = GetOrderByClause(sortBy, sortOrder);

            // Data query (optimized)
            var dataSql = $@"
                SELECT e.""Id"", e.""MailAccountId"", e.""MessageId"", e.""Subject"", e.""Body"", e.""HtmlBody"",
                       e.""From"", e.""To"", e.""Cc"", e.""Bcc"", e.""SentDate"", e.""ReceivedDate"",
                       e.""IsOutgoing"", e.""HasAttachments"", e.""FolderName"",
                       ma.""Id"" as ""AccountId"", ma.""Name"" as ""AccountName"", ma.""EmailAddress"" as ""AccountEmail""
                FROM mail_archiver.""ArchivedEmails"" e
                INNER JOIN mail_archiver.""MailAccounts"" ma ON e.""MailAccountId"" = ma.""Id""
                {whereClause}
                {orderByClause}
                LIMIT {take} OFFSET {skip}";

            var dataStartTime = DateTime.UtcNow;
            var emails = await ExecuteDataQueryAsync(dataSql, CloneParameters(parameters));
            var dataDuration = DateTime.UtcNow - dataStartTime;
            _logger.LogInformation("Optimized data query took {Duration}ms for {Count} records",
                dataDuration.TotalMilliseconds, emails.Count);

            var totalDuration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Total optimized search operation took {Duration}ms", totalDuration.TotalMilliseconds);

            return (emails, totalCount);
        }

        private async Task<T> ExecuteScalarQueryAsync<T>(string sql, List<Npgsql.NpgsqlParameter> parameters)
        {
            using var connection = new Npgsql.NpgsqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            using var command = new Npgsql.NpgsqlCommand(sql, connection);
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            var result = await command.ExecuteScalarAsync();
            return (T)Convert.ChangeType(result, typeof(T));
        }

        private async Task<List<ArchivedEmail>> ExecuteDataQueryAsync(string sql, List<Npgsql.NpgsqlParameter> parameters)
        {
            var emails = new List<ArchivedEmail>();

            using var connection = new Npgsql.NpgsqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            using var command = new Npgsql.NpgsqlCommand(sql, connection);
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var email = new ArchivedEmail
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    MailAccountId = reader.GetInt32(reader.GetOrdinal("MailAccountId")),
                    MessageId = reader.IsDBNull(reader.GetOrdinal("MessageId")) ? "" : reader.GetString(reader.GetOrdinal("MessageId")),
                    Subject = reader.IsDBNull(reader.GetOrdinal("Subject")) ? "" : reader.GetString(reader.GetOrdinal("Subject")),
                    Body = reader.IsDBNull(reader.GetOrdinal("Body")) ? "" : reader.GetString(reader.GetOrdinal("Body")),
                    HtmlBody = reader.IsDBNull(reader.GetOrdinal("HtmlBody")) ? "" : reader.GetString(reader.GetOrdinal("HtmlBody")),
                    From = reader.IsDBNull(reader.GetOrdinal("From")) ? "" : reader.GetString(reader.GetOrdinal("From")),
                    To = reader.IsDBNull(reader.GetOrdinal("To")) ? "" : reader.GetString(reader.GetOrdinal("To")),
                    Cc = reader.IsDBNull(reader.GetOrdinal("Cc")) ? "" : reader.GetString(reader.GetOrdinal("Cc")),
                    Bcc = reader.IsDBNull(reader.GetOrdinal("Bcc")) ? "" : reader.GetString(reader.GetOrdinal("Bcc")),
                    SentDate = reader.GetDateTime(reader.GetOrdinal("SentDate")),
                    ReceivedDate = reader.GetDateTime(reader.GetOrdinal("ReceivedDate")),
                    IsOutgoing = reader.GetBoolean(reader.GetOrdinal("IsOutgoing")),
                    HasAttachments = reader.GetBoolean(reader.GetOrdinal("HasAttachments")),
                    FolderName = reader.IsDBNull(reader.GetOrdinal("FolderName")) ? "" : reader.GetString(reader.GetOrdinal("FolderName")),
                    MailAccount = new MailAccount
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("AccountId")),
                        Name = reader.IsDBNull(reader.GetOrdinal("AccountName")) ? "" : reader.GetString(reader.GetOrdinal("AccountName")),
                        EmailAddress = reader.IsDBNull(reader.GetOrdinal("AccountEmail")) ? "" : reader.GetString(reader.GetOrdinal("AccountEmail"))
                    }
                };
                emails.Add(email);
            }

            return emails;
        }

        private (string tsQuery, List<string> phrases, Dictionary<string, List<string>> fieldSearches, Dictionary<string, List<string>> fieldPhrases) ParseSearchTermForTsQuery(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return (null, new List<string>(), new Dictionary<string, List<string>>(), new Dictionary<string, List<string>>());

            _logger.LogDebug("Parsing search term for tsquery: '{SearchTerm}'", searchTerm);

            var phrases = new List<string>();
            var individualWords = new List<string>();
            var fieldSearches = new Dictionary<string, List<string>>();
            var fieldPhrases = new Dictionary<string, List<string>>();

            // Supported fields for field-specific search
            var validFields = new HashSet<string> { "subject", "body", "from", "to" };

            // Parse quoted phrases, field-specific terms, and individual words
            // Enhanced regex to capture: "quoted phrases", field:term, field:"quoted phrase", and individual words
            var regex = new Regex(@"""([^""]*)""|(\w+):(""([^""]*)""|(\S+))|(\S+)", RegexOptions.None);
            var matches = regex.Matches(searchTerm);

            foreach (Match match in matches)
            {
                if (match.Groups[1].Success) // Quoted phrase (not field-specific)
                {
                    var phrase = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(phrase))
                    {
                        phrases.Add(phrase);
                    }
                }
                else if (match.Groups[2].Success) // Field-specific search
                {
                    var field = match.Groups[2].Value.ToLower().Trim();
                    if (validFields.Contains(field))
                    {
                        if (match.Groups[4].Success) // field:"quoted phrase"
                        {
                            var fieldPhrase = match.Groups[4].Value.Trim();
                            if (!string.IsNullOrEmpty(fieldPhrase))
                            {
                                if (!fieldPhrases.ContainsKey(field))
                                    fieldPhrases[field] = new List<string>();
                                fieldPhrases[field].Add(fieldPhrase);
                            }
                        }
                        else if (match.Groups[5].Success) // field:term
                        {
                            var fieldTerm = match.Groups[5].Value.Trim();
                            if (!string.IsNullOrEmpty(fieldTerm))
                            {
                                // Remove special PostgreSQL tsquery operators
                                var sanitized = Regex.Replace(fieldTerm, @"[&|!():\*]", "", RegexOptions.None);
                                if (!string.IsNullOrEmpty(sanitized))
                                {
                                    if (!fieldSearches.ContainsKey(field))
                                        fieldSearches[field] = new List<string>();
                                    fieldSearches[field].Add(sanitized);
                                }
                            }
                        }
                    }
                }
                else if (match.Groups[6].Success) // Individual word (not field-specific)
                {
                    var word = match.Groups[6].Value.Trim();
                    if (!string.IsNullOrEmpty(word))
                    {
                        // Remove special PostgreSQL tsquery operators and characters that could break the query
                        var sanitized = Regex.Replace(word, @"[&|!():\*]", "", RegexOptions.None);
                        if (!string.IsNullOrEmpty(sanitized))
                        {
                            individualWords.Add(sanitized);
                        }
                    }
                }
            }

            // Build tsquery for individual words (non-field-specific)
            string tsQuery = null;
            if (individualWords.Any())
            {
                // Escape single quotes and join with AND
                var escapedTerms = individualWords.Select(t => t.Replace("'", "''"));
                tsQuery = string.Join(" & ", escapedTerms);
            }

            _logger.LogDebug("Parsed search - tsQuery: '{TsQuery}', phrases: [{Phrases}], fieldSearches: [{FieldSearches}], fieldPhrases: [{FieldPhrases}]",
                tsQuery,
                string.Join(", ", phrases.Select(p => $"'{p}'")),
                string.Join(", ", fieldSearches.Select(kvp => $"{kvp.Key}:[{string.Join(",", kvp.Value)}]")),
                string.Join(", ", fieldPhrases.Select(kvp => $"{kvp.Key}:[{string.Join(",", kvp.Value.Select(p => $"'{p}'"))}]")));

            return (tsQuery, phrases, fieldSearches, fieldPhrases);
        }

        /// <summary>
        /// Maps field names to their corresponding database column names
        /// </summary>
        /// <param name="fieldName">The field name from the search term (subject, body, from, to)</param>
        /// <returns>The corresponding database column name or null if invalid</returns>
        private string GetColumnNameForField(string fieldName)
        {
            return fieldName.ToLower() switch
            {
                "subject" => "Subject",
                "body" => "Body",
                "from" => "From",
                "to" => "To",
                _ => null
            };
        }

        /// <summary>
        /// Generates the ORDER BY clause for SQL query based on sortBy and sortOrder parameters
        /// </summary>
        /// <param name="sortBy">The column to sort by</param>
        /// <param name="sortOrder">The sort direction (asc or desc)</param>
        /// <returns>SQL ORDER BY clause</returns>
        private string GetOrderByClause(string sortBy, string sortOrder)
        {
            // Validate and sanitize sortBy parameter
            var columnName = sortBy?.ToLower() switch
            {
                "subject" => "Subject",
                "from" => "From",
                "to" => "To",
                "sentdate" => "SentDate",
                "receiveddate" => "ReceivedDate",
                _ => "SentDate" // Default to SentDate
            };

            // Validate and sanitize sortOrder parameter
            var direction = sortOrder?.ToLower() == "asc" ? "ASC" : "DESC";

            return $@"ORDER BY e.""{columnName}"" {direction}";
        }

        private List<Npgsql.NpgsqlParameter> CloneParameters(List<Npgsql.NpgsqlParameter> parameters)
        {
            var clonedParameters = new List<Npgsql.NpgsqlParameter>();
            foreach (var param in parameters)
            {
                clonedParameters.Add(new Npgsql.NpgsqlParameter(param.ParameterName, param.Value));
            }
            return clonedParameters;
        }

        private async Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsEFAsync(
            string searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            string folderName,
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null)
        {
            // Original Entity Framework implementation as fallback
            var baseQuery = _context.ArchivedEmails.AsNoTracking().AsQueryable();

            // Filter by allowed account IDs if provided (for non-admin users)
            if (allowedAccountIds != null)
            {
                if (allowedAccountIds.Any())
                {
                    baseQuery = baseQuery.Where(e => allowedAccountIds.Contains(e.MailAccountId));
                }
                else
                {
                    // User has no access to any accounts, return no results
                    baseQuery = baseQuery.Where(e => false);
                }
            }

            if (accountId.HasValue)
            {
                // Additional check to ensure user has access to the requested account
                if (allowedAccountIds != null && allowedAccountIds.Any() && !allowedAccountIds.Contains(accountId.Value))
                {
                    // User doesn't have access to this account, return empty results
                    return (new List<ArchivedEmail>(), 0);
                }
                baseQuery = baseQuery.Where(e => e.MailAccountId == accountId.Value);
            }

            if (fromDate.HasValue)
                baseQuery = baseQuery.Where(e => e.SentDate >= fromDate.Value);

            if (toDate.HasValue)
                baseQuery = baseQuery.Where(e => e.SentDate <= toDate.Value.AddDays(1).AddSeconds(-1));

            if (isOutgoing.HasValue)
                baseQuery = baseQuery.Where(e => e.IsOutgoing == isOutgoing.Value);

            // Folder filtering
            if (!string.IsNullOrEmpty(folderName))
                baseQuery = baseQuery.Where(e => e.FolderName == folderName);

            IQueryable<ArchivedEmail> searchQuery = baseQuery;
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var (tsQuery, phrases, fieldSearches, fieldPhrases) = ParseSearchTermForTsQuery(searchTerm);

                // Start with the base query
                searchQuery = baseQuery;

                // Handle individual words (global search)
                if (!string.IsNullOrEmpty(tsQuery))
                {
                    var words = tsQuery.Split('&', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(w => w.Trim().Replace("''", "'"))
                                      .ToList();

                    foreach (var word in words)
                    {
                        var escapedWord = word.Replace("'", "''");
                        searchQuery = searchQuery.Where(e =>
                            EF.Functions.ILike(e.Subject, $"%{escapedWord}%") ||
                            EF.Functions.ILike(e.From, $"%{escapedWord}%") ||
                            EF.Functions.ILike(e.To, $"%{escapedWord}%") ||
                            EF.Functions.ILike(e.Body, $"%{escapedWord}%") ||
                            EF.Functions.ILike(e.Cc, $"%{escapedWord}%") ||
                            EF.Functions.ILike(e.Bcc, $"%{escapedWord}%")
                        );
                    }
                }

                // Handle quoted phrases with exact matching (global search)
                foreach (var phrase in phrases)
                {
                    searchQuery = searchQuery.Where(e =>
                        (e.Subject != null && e.Subject.ToLower().Contains(phrase.ToLower())) ||
                        (e.From != null && e.From.ToLower().Contains(phrase.ToLower())) ||
                        (e.To != null && e.To.ToLower().Contains(phrase.ToLower())) ||
                        (e.Body != null && e.Body.ToLower().Contains(phrase.ToLower())) ||
                        (e.Cc != null && e.Cc.ToLower().Contains(phrase.ToLower())) ||
                        (e.Bcc != null && e.Bcc.ToLower().Contains(phrase.ToLower()))
                    );
                }

                // Handle field-specific word searches
                foreach (var fieldSearch in fieldSearches)
                {
                    var field = fieldSearch.Key;
                    var terms = fieldSearch.Value;

                    foreach (var term in terms)
                    {
                        var escapedTerm = term.Replace("'", "''");

                        switch (field.ToLower())
                        {
                            case "subject":
                                searchQuery = searchQuery.Where(e => e.Subject != null && EF.Functions.ILike(e.Subject, $"%{escapedTerm}%"));
                                break;
                            case "body":
                                searchQuery = searchQuery.Where(e => e.Body != null && EF.Functions.ILike(e.Body, $"%{escapedTerm}%"));
                                break;
                            case "from":
                                searchQuery = searchQuery.Where(e => e.From != null && EF.Functions.ILike(e.From, $"%{escapedTerm}%"));
                                break;
                            case "to":
                                searchQuery = searchQuery.Where(e => e.To != null && EF.Functions.ILike(e.To, $"%{escapedTerm}%"));
                                break;
                        }
                    }
                }

                // Handle field-specific phrase searches
                foreach (var fieldPhrase in fieldPhrases)
                {
                    var field = fieldPhrase.Key;
                    var fieldPhrasesList = fieldPhrase.Value;

                    foreach (var phrase in fieldPhrasesList)
                    {
                        switch (field.ToLower())
                        {
                            case "subject":
                                searchQuery = searchQuery.Where(e => e.Subject != null && e.Subject.ToLower().Contains(phrase.ToLower()));
                                break;
                            case "body":
                                searchQuery = searchQuery.Where(e => e.Body != null && e.Body.ToLower().Contains(phrase.ToLower()));
                                break;
                            case "from":
                                searchQuery = searchQuery.Where(e => e.From != null && e.From.ToLower().Contains(phrase.ToLower()));
                                break;
                            case "to":
                                searchQuery = searchQuery.Where(e => e.To != null && e.To.ToLower().Contains(phrase.ToLower()));
                                break;
                        }
                    }
                }
            }

            var totalCount = await searchQuery.CountAsync();
            var emails = await searchQuery
                .Include(e => e.MailAccount)
                .OrderByDescending(e => e.SentDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return (emails, totalCount);
        }

        public async Task<byte[]> ExportEmailsAsync(ExportViewModel parameters, List<int> allowedAccountIds = null)
        {
            using var ms = new MemoryStream();

            // Prüfe ob es sich um einen Single-Email Export handelt
            if (parameters.EmailId.HasValue)
            {
                var email = await _context.ArchivedEmails
                    .Include(e => e.MailAccount)
                    .Include(e => e.Attachments)
                    .FirstOrDefaultAsync(e => e.Id == parameters.EmailId.Value);

                if (email == null)
                {
                    throw new InvalidOperationException($"Email with ID {parameters.EmailId.Value} not found");
                }

                switch (parameters.Format)
                {
                    case ExportFormat.Csv:
                        await ExportSingleEmailAsCsv(email, ms);
                        break;
                    case ExportFormat.Json:
                        await ExportSingleEmailAsJson(email, ms);
                        break;
                    case ExportFormat.Eml:
                        await ExportSingleEmailAsEml(email, ms);
                        break;
                }
            }
            else
            {
                // Multi-Email Export (bestehender Code)
                var searchTerm = parameters.SearchTerm ?? string.Empty;
                var (emails, _) = await SearchEmailsAsync(
                    searchTerm,
                    parameters.FromDate,
                    parameters.ToDate,
                    parameters.SelectedAccountId,
                    null, // folderName
                    parameters.IsOutgoing,
                    0,
                    10000,
                    allowedAccountIds);

                switch (parameters.Format)
                {
                    case ExportFormat.Csv:
                        await ExportMultipleEmailsAsCsv(emails, ms);
                        break;
                    case ExportFormat.Json:
                        await ExportMultipleEmailsAsJson(emails, ms);
                        break;
                }
            }

            ms.Position = 0;
            return ms.ToArray();
        }

        private async Task ExportSingleEmailAsCsv(ArchivedEmail email, MemoryStream ms)
        {
            using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
            writer.WriteLine("Subject;From;To;Date;Account;Direction;Message Text");

            var subject = email.Subject.Replace("\"", "\"\"").Replace(";", ",");
            var from = email.From.Replace("\"", "\"\"").Replace(";", ",");
            var to = email.To.Replace("\"", "\"\"").Replace(";", ",");
            var sentDate = email.SentDate.ToString("yyyy-MM-dd HH:mm:ss");
            var account = email.MailAccount?.Name.Replace("\"", "\"\"").Replace(";", ",") ?? "Unknown";
            var direction = email.IsOutgoing ? "Outgoing" : "Incoming";
            var body = email.Body?.Replace("\r", " ").Replace("\n", " ")
                .Replace("\"", "\"\"").Replace(";", ",") ?? "";

            writer.WriteLine($"\"{subject}\";\"{from}\";\"{to}\";\"{sentDate}\";\"{account}\";\"{direction}\";\"{body}\"");
            await writer.FlushAsync();
        }

        private async Task ExportSingleEmailAsJson(ArchivedEmail email, MemoryStream ms)
        {
            var exportData = new
            {
                Id = email.Id,
                Subject = email.Subject,
                From = email.From,
                To = email.To,
                Cc = email.Cc,
                Bcc = email.Bcc,
                SentDate = email.SentDate,
                ReceivedDate = email.ReceivedDate,
                AccountName = email.MailAccount?.Name,
                FolderName = email.FolderName,
                IsOutgoing = email.IsOutgoing,
                HasAttachments = email.HasAttachments,
                MessageId = email.MessageId,
                Body = email.Body,
                HtmlBody = email.HtmlBody,
                Attachments = email.Attachments?.Select(a => new
                {
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    Size = a.Size
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            await JsonSerializer.SerializeAsync(ms, exportData, options);
        }

        private async Task ExportSingleEmailAsEml(ArchivedEmail email, MemoryStream ms)
        {
            var message = new MimeMessage();
            message.Subject = email.Subject;

            try { message.From.Add(InternetAddress.Parse(email.From)); }
            catch { message.From.Add(new MailboxAddress("Sender", "sender@example.com")); }

            foreach (var to in email.To.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                try { message.To.Add(InternetAddress.Parse(to.Trim())); }
                catch { continue; }
            }

            if (!string.IsNullOrEmpty(email.Cc))
            {
                foreach (var cc in email.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    try { message.Cc.Add(InternetAddress.Parse(cc.Trim())); }
                    catch { continue; }
                }
            }

            // Use untruncated body if available for compliance
            var htmlBodyToExport = !string.IsNullOrEmpty(email.BodyUntruncatedHtml) 
                ? email.BodyUntruncatedHtml 
                : email.HtmlBody;
            
            var textBodyToExport = !string.IsNullOrEmpty(email.BodyUntruncatedText) 
                ? email.BodyUntruncatedText 
                : email.Body;

            var body = !string.IsNullOrEmpty(htmlBodyToExport)
                ? new TextPart("html") { Text = htmlBodyToExport }
                : new TextPart("plain") { Text = textBodyToExport };

            if (email.Attachments.Any())
            {
                // Create a multipart/mixed as the base
                var multipart = new Multipart("mixed");
                
                // Check if we have inline images that should be in a related part
                var inlineAttachments = email.Attachments.Where(a => !string.IsNullOrEmpty(a.ContentId)).ToList();
                var regularAttachments = email.Attachments.Where(a => string.IsNullOrEmpty(a.ContentId)).ToList();
                
                if (inlineAttachments.Any() && !string.IsNullOrEmpty(email.HtmlBody))
                {
                    // Create multipart/related for HTML body and inline images
                    var related = new Multipart("related");
                    related.Add(body);
                    
                    // Add inline attachments with proper Content-Disposition
                    foreach (var attachment in inlineAttachments)
                    {
                        var mimePart = new MimePart(attachment.ContentType)
                        {
                            Content = new MimeContent(new MemoryStream(attachment.Content)),
                            ContentId = attachment.ContentId,
                            ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                            ContentTransferEncoding = ContentEncoding.Base64,
                            FileName = attachment.FileName
                        };
                        related.Add(mimePart);
                    }
                    
                    // Add the related part to the main multipart
                    multipart.Add(related);
                }
                else
                {
                    // No inline images, just add the body directly
                    multipart.Add(body);
                }
                
                // Add regular attachments
                foreach (var attachment in regularAttachments)
                {
                    var mimePart = new MimePart(attachment.ContentType)
                    {
                        Content = new MimeContent(new MemoryStream(attachment.Content)),
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        FileName = attachment.FileName
                    };
                    multipart.Add(mimePart);
                }
                
                message.Body = multipart;
            }
            else
            {
                message.Body = body;
            }

            message.Date = email.SentDate;
            message.MessageId = email.MessageId;

            await Task.Run(() => message.WriteTo(ms));
        }

        private async Task ExportMultipleEmailsAsCsv(List<ArchivedEmail> emails, MemoryStream ms)
        {
            using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
            writer.WriteLine("Subject;From;To;Date;Account;Direction;Message Text");

            foreach (var email in emails)
            {
                var subject = email.Subject.Replace("\"", "\"\"").Replace(";", ",");
                var from = email.From.Replace("\"", "\"\"").Replace(";", ",");
                var to = email.To.Replace("\"", "\"\"").Replace(";", ",");
                var sentDate = email.SentDate.ToString("yyyy-MM-dd HH:mm:ss");
                var account = email.MailAccount?.Name.Replace("\"", "\"\"").Replace(";", ",") ?? "Unknown";
                var direction = email.IsOutgoing ? "Outgoing" : "Incoming";
                var body = email.Body?.Replace("\r", " ").Replace("\n", " ")
                    .Replace("\"", "\"\"").Replace(";", ",") ?? "";
                writer.WriteLine($"\"{subject}\";\"{from}\";\"{to}\";\"{sentDate}\";\"{account}\";\"{direction}\";\"{body}\"");
            }
            await writer.FlushAsync();
        }

        private async Task ExportMultipleEmailsAsJson(List<ArchivedEmail> emails, MemoryStream ms)
        {
            var exportData = emails.Select(e => new
            {
                Subject = e.Subject,
                From = e.From,
                To = e.To,
                SentDate = e.SentDate,
                AccountName = e.MailAccount?.Name,
                IsOutgoing = e.IsOutgoing,
                Body = e.Body
            }).ToList();

            var options = new JsonSerializerOptions { WriteIndented = true };
            await JsonSerializer.SerializeAsync(ms, exportData, options);
        }

        public async Task<DashboardViewModel> GetDashboardStatisticsAsync()
        {
            var model = new DashboardViewModel();

            model.TotalEmails = await _context.ArchivedEmails.CountAsync();
            model.TotalAccounts = await _context.MailAccounts.CountAsync();
            model.TotalAttachments = await _context.EmailAttachments.CountAsync();

            var totalDatabaseSizeBytes = await GetDatabaseSizeAsync();
            model.TotalStorageUsed = FormatFileSize(totalDatabaseSizeBytes);

            model.EmailsPerAccount = await _context.MailAccounts
                .Select(a => new AccountStatistics
                {
                    AccountName = a.Name,
                    EmailAddress = a.EmailAddress,
                    EmailCount = a.ArchivedEmails.Count,
                    LastSyncTime = a.LastSync,
                    IsEnabled = a.IsEnabled
                })
                .ToListAsync();

            var now = DateTime.UtcNow;
            var startDate = now.AddMonths(-11).Date;
            startDate = new DateTime(startDate.Year, startDate.Month, 1); // First day of the month
            var months = new List<EmailCountByPeriod>();
            for (int i = 0; i < 12; i++)
            {
                var currentMonth = startDate.AddMonths(i);
                var nextMonth = currentMonth.AddMonths(1);

                int count;
                if (i == 11) // Current month
                {
                    // For the current month, count all emails up to now
                    count = await _context.ArchivedEmails
                        .Where(e => e.SentDate >= currentMonth && e.SentDate <= now)
                        .CountAsync();
                }
                else
                {
                    // For past months, use the standard range
                    count = await _context.ArchivedEmails
                        .Where(e => e.SentDate >= currentMonth && e.SentDate < nextMonth)
                        .CountAsync();
                }

                months.Add(new EmailCountByPeriod
                {
                    Period = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(currentMonth.Month)} {currentMonth.Year}",
                    Count = count
                });
            }
            model.EmailsByMonth = months;

            model.TopSenders = await _context.ArchivedEmails
                .Where(e => !e.IsOutgoing)
                .GroupBy(e => e.From)
                .Select(g => new EmailCountByAddress
                {
                    EmailAddress = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(e => e.Count)
                .Take(10)
                .ToListAsync();

            model.RecentEmails = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .OrderByDescending(e => e.SentDate)
                .Take(10)
                .ToListAsync();

            return model;
        }

        /// <summary>
        /// Gets the total size of the PostgreSQL database in bytes
        /// </summary>
        /// <returns>Database size in bytes</returns>
        private async Task<long> GetDatabaseSizeAsync()
        {
            try
            {
                using var connection = new Npgsql.NpgsqlConnection(_context.Database.GetConnectionString());
                await connection.OpenAsync();

                // Query to get the total size of the current database
                var sql = "SELECT pg_database_size(current_database())";
                
                using var command = new Npgsql.NpgsqlCommand(sql, connection);
                var result = await command.ExecuteScalarAsync();
                
                return Convert.ToInt64(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database size: {Message}", ex.Message);
                // Fallback to attachment size if database size query fails
                return await _context.EmailAttachments.SumAsync(a => (long)a.Size);
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        public async Task<bool> TestConnectionAsync(MailAccount account)
        {
            try
            {
                _logger.LogInformation("Testing connection to IMAP server {Server}:{Port} for account {Name} ({Email})",
                    account.ImapServer, account.ImapPort, account.Name, account.EmailAddress);

                using var client = CreateImapClient(account.Name);
                client.Timeout = 30000;
                client.ServerCertificateValidationCallback = ServerCertificateValidationCallback;

                _logger.LogDebug("Connecting to {Server}:{Port}, SSL: {UseSSL}",
                    account.ImapServer, account.ImapPort, account.UseSSL);

                await ConnectWithFallbackAsync(client, account.ImapServer, account.ImapPort ?? 993, account.UseSSL, account.Name);
                _logger.LogDebug("Connection established, authenticating using {Provider} authentication", account.Provider);

        await AuthenticateClientAsync(client, account);
        _logger.LogInformation("Authentication successful for {Email}", account.EmailAddress);

        // Try to verify folder access, but don't fail the test if this doesn't work
        // Some servers have non-standard IMAP implementations
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
                
                // Try to verify namespace access without opening folders
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
            // Folder access failed, but connection and authentication worked
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
                else if (ex is SocketException socketEx)
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
        

        // RestoreEmailToFolderAsync und andere Methoden bleiben unverändert...
        public async Task<bool> RestoreEmailToFolderAsync(int emailId, int targetAccountId, string folderName)
        {
            _logger.LogInformation("RestoreEmailToFolderAsync called with parameters: emailId={EmailId}, targetAccountId={TargetAccountId}, folderName={FolderName}",
                emailId, targetAccountId, folderName);

            try
            {
                var email = await _context.ArchivedEmails
                    .Include(e => e.Attachments)
                    .FirstOrDefaultAsync(e => e.Id == emailId);

                if (email == null)
                {
                    _logger.LogError("Email with ID {EmailId} not found", emailId);
                    return false;
                }

                _logger.LogInformation("Found email: Subject='{Subject}', From='{From}', Attachments={AttachmentCount}",
                    email.Subject, email.From, email.Attachments.Count);

                var targetAccount = await _context.MailAccounts.FindAsync(targetAccountId);
                if (targetAccount == null)
                {
                    _logger.LogError("Target account with ID {AccountId} not found", targetAccountId);
                    return false;
                }

                _logger.LogInformation("Found target account: {AccountName}, {EmailAddress}",
                    targetAccount.Name, targetAccount.EmailAddress);

                MimeMessage message = null;
                try
                {
                    message = new MimeMessage();
                    message.Subject = email.Subject;

                    try
                    {
                        var fromAddresses = InternetAddressList.Parse(email.From);
                        foreach (var address in fromAddresses)
                        {
                            message.From.Add(address);
                        }
                        if (message.From.Count == 0)
                        {
                            throw new FormatException("No valid From addresses");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing From address: {From}, using fallback", email.From);
                        message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
                    }

                    if (!string.IsNullOrEmpty(email.To))
                    {
                        try
                        {
                            var toAddresses = InternetAddressList.Parse(email.To);
                            foreach (var address in toAddresses)
                            {
                                message.To.Add(address);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing To addresses: {To}, using placeholder", email.To);
                            message.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
                        }
                    }
                    else
                    {
                        message.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
                    }

                    if (!string.IsNullOrEmpty(email.Cc))
                    {
                        try
                        {
                            var ccAddresses = InternetAddressList.Parse(email.Cc);
                            foreach (var address in ccAddresses)
                            {
                                message.Cc.Add(address);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing Cc addresses: {Cc}, ignoring", email.Cc);
                        }
                    }

                    // Use untruncated body if available for compliance
                    var htmlBodyToRestore = !string.IsNullOrEmpty(email.BodyUntruncatedHtml) 
                        ? email.BodyUntruncatedHtml 
                        : email.HtmlBody;
                    
                    var textBodyToRestore = !string.IsNullOrEmpty(email.BodyUntruncatedText) 
                        ? email.BodyUntruncatedText 
                        : email.Body;

                    var bodyBuilder = new BodyBuilder();
                    if (!string.IsNullOrEmpty(htmlBodyToRestore))
                    {
                        bodyBuilder.HtmlBody = htmlBodyToRestore;
                    }
                    if (!string.IsNullOrEmpty(textBodyToRestore))
                    {
                        bodyBuilder.TextBody = textBodyToRestore;
                    }

                    // Add attachments
                    if (email.Attachments?.Any() == true)
                    {
                        // Separate inline attachments from regular attachments
                        var inlineAttachments = email.Attachments.Where(a => !string.IsNullOrEmpty(a.ContentId)).ToList();
                        var regularAttachments = email.Attachments.Where(a => string.IsNullOrEmpty(a.ContentId)).ToList();
                        
                        // Add inline attachments first so they can be referenced in the HTML body
                        foreach (var attachment in inlineAttachments)
                        {
                            try
                            {
                                var contentType = ContentType.Parse(attachment.ContentType);
                                var mimePart = bodyBuilder.LinkedResources.Add(attachment.FileName, attachment.Content, contentType);
                                mimePart.ContentId = attachment.ContentId;
                                _logger.LogInformation("Added inline attachment: {FileName} with Content-ID: {ContentId}", attachment.FileName, attachment.ContentId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error adding inline attachment {FileName}", attachment.FileName);
                            }
                        }
                        
                        // Add regular attachments
                        foreach (var attachment in regularAttachments)
                        {
                            try
                            {
                                bodyBuilder.Attachments.Add(attachment.FileName,
                                                           attachment.Content,
                                                           ContentType.Parse(attachment.ContentType));
                                _logger.LogInformation("Added attachment: {FileName}", attachment.FileName);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error adding attachment {FileName}", attachment.FileName);
                            }
                        }
                    }

                    message.Body = bodyBuilder.ToMessageBody();
                    message.Date = email.SentDate;
                    if (!string.IsNullOrEmpty(email.MessageId) && email.MessageId.Contains('@'))
                    {
                        message.MessageId = email.MessageId;
                    }

                    _logger.LogInformation("Successfully created MimeMessage for email ID {EmailId}", emailId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating MimeMessage for email ID {EmailId}", emailId);
                    return false;
                }

                try
                {
                    using var client = CreateImapClient(targetAccount.Name);
                    client.Timeout = 180000;
                    client.ServerCertificateValidationCallback = ServerCertificateValidationCallback;
                    _logger.LogInformation("Connecting to IMAP server {Server}:{Port} for account {AccountName}",
                        targetAccount.ImapServer, targetAccount.ImapPort, targetAccount.Name);

                    await ConnectWithFallbackAsync(client, targetAccount.ImapServer, targetAccount.ImapPort ?? 993, targetAccount.UseSSL, targetAccount.Name);
                    _logger.LogInformation("Connected to IMAP server, authenticating using {Provider} authentication", targetAccount.Provider);

                    await AuthenticateClientAsync(client, targetAccount);
                    _logger.LogInformation("Authenticated successfully, looking for folder: {FolderName}", folderName);

                    IMailFolder folder;
                    try
                    {
                        folder = await client.GetFolderAsync(folderName);
                        _logger.LogInformation("Found folder: {FolderName}", folder.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not find folder '{FolderName}', trying INBOX instead", folderName);
                        try
                        {
                            folder = client.Inbox;
                            folderName = "INBOX";
                            _logger.LogInformation("Using INBOX as fallback");
                        }
                        catch (Exception inboxEx)
                        {
                            _logger.LogError(inboxEx, "Could not access INBOX folder either");
                            return false;
                        }
                    }

                    try
                    {
                        _logger.LogInformation("Opening folder {FolderName} with ReadWrite access", folder.FullName);
                        await folder.OpenAsync(FolderAccess.ReadWrite);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error opening folder {FolderName} with ReadWrite access", folder.FullName);
                        try
                        {
                            await folder.OpenAsync(FolderAccess.ReadOnly);
                            _logger.LogInformation("Opened folder {FolderName} with ReadOnly access", folder.FullName);
                        }
                        catch (Exception readEx)
                        {
                            _logger.LogError(readEx, "Could not open folder {FolderName} at all", folder.FullName);
                            return false;
                        }
                    }

                    try
                    {
                        _logger.LogInformation("Appending message to folder {FolderName}", folder.FullName);
                        await folder.AppendAsync(message, MessageFlags.Seen);
                        _logger.LogInformation("Message successfully appended to folder {FolderName}", folder.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error appending message to folder {FolderName}: {ErrorMessage}",
                            folder.FullName, ex.Message);
                        return false;
                    }

                    await client.DisconnectAsync(true);
                    _logger.LogInformation("Successfully disconnected from IMAP server");

                    _logger.LogInformation("Email with ID {EmailId} successfully copied to folder '{FolderName}' of account {AccountName}",
                        emailId, folderName, targetAccount.Name);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during IMAP operations: {Message}", ex.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in RestoreEmailToFolderAsync: {Message}", ex.Message);
                return false;
            }
        }

        public async Task<(int Successful, int Failed)> RestoreMultipleEmailsWithProgressAsync(
            List<int> emailIds,
            int targetAccountId,
            string folderName,
            Action<int, int, int> progressCallback,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting batch restore with progress tracking of {Count} emails to account {AccountId}, folder {Folder}",
                emailIds.Count, targetAccountId, folderName);

            // Use optimized batch restore with shared IMAP connection and progress tracking
            return await RestoreMultipleEmailsWithSharedConnectionAndProgressAsync(emailIds, targetAccountId, folderName, progressCallback, cancellationToken);
        }

        /// <summary>
        /// Restores multiple emails using a shared IMAP connection with progress tracking
        /// </summary>
        public async Task<(int Successful, int Failed)> RestoreMultipleEmailsWithSharedConnectionAndProgressAsync(
            List<int> emailIds,
            int targetAccountId,
            string folderName,
            Action<int, int, int> progressCallback,
            CancellationToken cancellationToken = default)
        {
            int successCount = 0;
            int failCount = 0;

            var targetAccount = await _context.MailAccounts.FindAsync(targetAccountId, cancellationToken);
            if (targetAccount == null)
            {
                _logger.LogError("Target account with ID {AccountId} not found", targetAccountId);
                return (0, emailIds.Count);
            }

            _logger.LogInformation("Using shared IMAP connection with progress tracking for batch restore of {Count} emails to account {AccountName}",
                emailIds.Count, targetAccount.Name);

            ImapClient client = null;
            IMailFolder targetFolder = null;
            
            try
            {
                // Initialize connection
                client = CreateImapClient(targetAccount.Name);
                var connectionResult = await EstablishImapConnectionAsync(client, targetAccount, folderName);
                if (!connectionResult.Success)
                {
                    _logger.LogError("Failed to establish initial IMAP connection: {Error}", connectionResult.ErrorMessage);
                    return (0, emailIds.Count);
                }
                targetFolder = connectionResult.Folder;

                // Process emails in batches
                var batchSize = _batchOptions.BatchSize;
                for (int i = 0; i < emailIds.Count; i += batchSize)
                {
                    var batch = emailIds.Skip(i).Take(batchSize).ToList();
                    _logger.LogInformation("Processing batch {BatchNumber}/{TotalBatches} with {BatchSize} emails",
                        (i / batchSize) + 1, (emailIds.Count + batchSize - 1) / batchSize, batch.Count);

                    foreach (var emailId in batch)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var maxRetries = 3;
                        var retryCount = 0;
                        bool emailRestored = false;

                        while (retryCount < maxRetries && !emailRestored)
                        {
                            try
                            {
                                // Verify connection health before each email
                                if (!IsConnectionHealthy(client, targetFolder))
                                {
                                    _logger.LogWarning("Connection unhealthy, attempting to restore connection for email {EmailId}", emailId);
                                    var reconnectResult = await RestoreImapConnectionAsync(client, targetAccount, folderName);
                                    if (!reconnectResult.Success)
                                    {
                                        _logger.LogError("Failed to restore connection: {Error}", reconnectResult.ErrorMessage);
                                        throw new InvalidOperationException($"Failed to restore IMAP connection: {reconnectResult.ErrorMessage}");
                                    }
                                    client = reconnectResult.Client;
                                    targetFolder = reconnectResult.Folder;
                                }

                                // Restore the email using the shared connection
                                var result = await RestoreEmailWithSharedConnectionAsync(emailId, client, targetFolder, targetAccount.Name);
                                if (result)
                                {
                                    successCount++;
                                    emailRestored = true;
                                    _logger.LogDebug("Successfully restored email {EmailId} (attempt {Attempt})", emailId, retryCount + 1);
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to restore email {EmailId} (attempt {Attempt})", emailId, retryCount + 1);
                                    retryCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                retryCount++;
                                _logger.LogError(ex, "Error restoring email {EmailId} (attempt {Attempt}/{MaxRetries}): {Message}", 
                                    emailId, retryCount, maxRetries, ex.Message);

                                if (retryCount < maxRetries)
                                {
                                    // Wait before retry
                                    await Task.Delay(1000 * retryCount, cancellationToken); // Progressive delay
                                    
                                    // Try to restore connection for next attempt
                                    try
                                    {
                                        var reconnectResult = await RestoreImapConnectionAsync(client, targetAccount, folderName);
                                        if (reconnectResult.Success)
                                        {
                                            client = reconnectResult.Client;
                                            targetFolder = reconnectResult.Folder;
                                        }
                                    }
                                    catch (Exception reconnectEx)
                                    {
                                        _logger.LogError(reconnectEx, "Failed to reconnect during retry for email {EmailId}", emailId);
                                    }
                                }
                            }
                        }

                        if (!emailRestored)
                        {
                            failCount++;
                            _logger.LogError("Failed to restore email {EmailId} after {MaxRetries} attempts", emailId, maxRetries);
                        }

                        // Update progress after each email
                        var totalProcessed = successCount + failCount;
                        progressCallback?.Invoke(totalProcessed, successCount, failCount);

                        // Small delay between emails
                        if (_batchOptions.PauseBetweenEmailsMs > 0)
                        {
                            await Task.Delay(_batchOptions.PauseBetweenEmailsMs, cancellationToken);
                        }
                    }

                    // Pause between batches
                    if (i + batchSize < emailIds.Count && _batchOptions.PauseBetweenBatchesMs > 0)
                    {
                        _logger.LogDebug("Pausing {Ms}ms between batches", _batchOptions.PauseBetweenBatchesMs);
                        await Task.Delay(_batchOptions.PauseBetweenBatchesMs, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Batch restore with progress tracking was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during batch restore with progress tracking: {Message}", ex.Message);
                // Mark all unprocessed emails as failed
                failCount = emailIds.Count - successCount;
            }
            finally
            {
                // Clean up connection
                if (client != null)
                {
                    try
                    {
                        if (client.IsConnected)
                        {
                            await client.DisconnectAsync(true);
                        }
                        client.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error during IMAP client cleanup");
                    }
                }
            }

            _logger.LogInformation("Batch restore with progress tracking completed. Success: {SuccessCount}, Failed: {FailCount}",
                successCount, failCount);

            return (successCount, failCount);
        }

        /// <summary>
        /// Establishes IMAP connection and opens target folder
        /// </summary>
        private async Task<(bool Success, ImapClient Client, IMailFolder Folder, string ErrorMessage)> EstablishImapConnectionAsync(
            ImapClient client, MailAccount targetAccount, string folderName)
        {
            try
            {
                client.Timeout = 180000; // 3 minutes timeout
                client.ServerCertificateValidationCallback = ServerCertificateValidationCallback;
                
                _logger.LogDebug("Connecting to IMAP server {Server}:{Port} for account {AccountName}",
                    targetAccount.ImapServer, targetAccount.ImapPort, targetAccount.Name);

                await ConnectWithFallbackAsync(client, targetAccount.ImapServer, targetAccount.ImapPort ?? 993, targetAccount.UseSSL, targetAccount.Name);

                _logger.LogDebug("Authenticating with {Provider} authentication", targetAccount.Provider);
                await AuthenticateClientAsync(client, targetAccount);

                _logger.LogDebug("Opening folder: {FolderName}", folderName);
                IMailFolder folder;
                try
                {
                    folder = await client.GetFolderAsync(folderName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not find folder '{FolderName}', using INBOX instead", folderName);
                    folder = client.Inbox;
                    folderName = "INBOX";
                }

                await folder.OpenAsync(FolderAccess.ReadWrite);
                _logger.LogInformation("Successfully established IMAP connection and opened folder {FolderName}", folder.FullName);

                return (true, client, folder, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish IMAP connection: {Message}", ex.Message);
                return (false, client, null, ex.Message);
            }
        }

        /// <summary>
        /// Restores IMAP connection when it's broken
        /// </summary>
        private async Task<(bool Success, ImapClient Client, IMailFolder Folder, string ErrorMessage)> RestoreImapConnectionAsync(
            ImapClient existingClient, MailAccount targetAccount, string folderName)
        {
            _logger.LogInformation("Attempting to restore IMAP connection for account {AccountName}", targetAccount.Name);

            try
            {
                // Clean up existing client
                if (existingClient != null)
                {
                    try
                    {
                        if (existingClient.IsConnected)
                        {
                            await existingClient.DisconnectAsync(false); // Quick disconnect
                        }
                        existingClient.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error cleaning up existing client during reconnection");
                    }
                }

                // Create new client and establish connection
                var newClient = CreateImapClient(targetAccount.Name);
                return await EstablishImapConnectionAsync(newClient, targetAccount, folderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore IMAP connection: {Message}", ex.Message);
                return (false, null, null, ex.Message);
            }
        }

        /// <summary>
        /// Checks if IMAP connection and folder are healthy
        /// </summary>
        private bool IsConnectionHealthy(ImapClient client, IMailFolder folder)
        {
            try
            {
                return client != null && 
                       client.IsConnected && 
                       client.IsAuthenticated && 
                       folder != null && 
                       folder.IsOpen;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Restores a single email using an existing IMAP connection
        /// </summary>
        private async Task<bool> RestoreEmailWithSharedConnectionAsync(int emailId, ImapClient client, IMailFolder targetFolder, string accountName)
        {
            try
            {
                var email = await _context.ArchivedEmails
                    .Include(e => e.Attachments)
                    .FirstOrDefaultAsync(e => e.Id == emailId);

                if (email == null)
                {
                    _logger.LogError("Email with ID {EmailId} not found", emailId);
                    return false;
                }

                // Create MimeMessage (same logic as in RestoreEmailToFolderAsync)
                var message = await CreateMimeMessageFromArchivedEmailAsync(email, accountName);
                if (message == null)
                {
                    return false;
                }

                // Append message to folder using existing connection
                await targetFolder.AppendAsync(message, MessageFlags.Seen);
                _logger.LogDebug("Email {EmailId} successfully appended to folder {FolderName}", emailId, targetFolder.FullName);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring email {EmailId} with shared connection: {Message}", emailId, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Creates a MimeMessage from an ArchivedEmail (extracted from RestoreEmailToFolderAsync)
        /// </summary>
        private async Task<MimeMessage> CreateMimeMessageFromArchivedEmailAsync(ArchivedEmail email, string accountName)
        {
            try
            {
                var message = new MimeMessage();
                message.Subject = email.Subject;

                // Set From address
                try
                {
                    var fromAddresses = InternetAddressList.Parse(email.From);
                    foreach (var address in fromAddresses)
                    {
                        message.From.Add(address);
                    }
                    if (message.From.Count == 0)
                    {
                        throw new FormatException("No valid From addresses");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing From address: {From}, using fallback", email.From);
                    message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
                }

                // Set To addresses
                if (!string.IsNullOrEmpty(email.To))
                {
                    try
                    {
                        var toAddresses = InternetAddressList.Parse(email.To);
                        foreach (var address in toAddresses)
                        {
                            message.To.Add(address);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing To addresses: {To}, using placeholder", email.To);
                        message.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
                    }
                }
                else
                {
                    message.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
                }

                // Set Cc addresses
                if (!string.IsNullOrEmpty(email.Cc))
                {
                    try
                    {
                        var ccAddresses = InternetAddressList.Parse(email.Cc);
                        foreach (var address in ccAddresses)
                        {
                            message.Cc.Add(address);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing Cc addresses: {Cc}, ignoring", email.Cc);
                    }
                }

                // Use untruncated body if available for compliance
                var htmlBodyToRestore = !string.IsNullOrEmpty(email.BodyUntruncatedHtml) 
                    ? email.BodyUntruncatedHtml 
                    : email.HtmlBody;
                
                var textBodyToRestore = !string.IsNullOrEmpty(email.BodyUntruncatedText) 
                    ? email.BodyUntruncatedText 
                    : email.Body;

                // Create body with attachments
                var bodyBuilder = new BodyBuilder();
                if (!string.IsNullOrEmpty(htmlBodyToRestore))
                {
                    bodyBuilder.HtmlBody = htmlBodyToRestore;
                }
                if (!string.IsNullOrEmpty(textBodyToRestore))
                {
                    bodyBuilder.TextBody = textBodyToRestore;
                }

                // Add attachments
                if (email.Attachments?.Any() == true)
                {
                    var inlineAttachments = email.Attachments.Where(a => !string.IsNullOrEmpty(a.ContentId)).ToList();
                    var regularAttachments = email.Attachments.Where(a => string.IsNullOrEmpty(a.ContentId)).ToList();
                    
                    // Add inline attachments
                    foreach (var attachment in inlineAttachments)
                    {
                        try
                        {
                            var contentType = ContentType.Parse(attachment.ContentType);
                            var mimePart = bodyBuilder.LinkedResources.Add(attachment.FileName, attachment.Content, contentType);
                            mimePart.ContentId = attachment.ContentId;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error adding inline attachment {FileName}", attachment.FileName);
                        }
                    }
                    
                    // Add regular attachments
                    foreach (var attachment in regularAttachments)
                    {
                        try
                        {
                            bodyBuilder.Attachments.Add(attachment.FileName,
                                                       attachment.Content,
                                                       ContentType.Parse(attachment.ContentType));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error adding attachment {FileName}", attachment.FileName);
                        }
                    }
                }

                message.Body = bodyBuilder.ToMessageBody();
                message.Date = email.SentDate;
                if (!string.IsNullOrEmpty(email.MessageId) && email.MessageId.Contains('@'))
                {
                    message.MessageId = email.MessageId;
                }

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating MimeMessage for email ID {EmailId}", email.Id);
                return null;
            }
        }

        public async Task<List<string>> GetMailFoldersAsync(int accountId)
        {
            var account = await _context.MailAccounts.FindAsync(accountId);
            if (account == null)
            {
                _logger.LogError("Account with ID {AccountId} not found", accountId);
                return new List<string>();
            }

            // Check if this is an import-only account (provider is IMPORT)
            if (account.Provider == ProviderType.IMPORT)
            {
                _logger.LogInformation("Account {AccountId} is an import-only account, returning 'Import' folder", accountId);
                return new List<string> { "Import" };
            }

            // Check if the account has the required IMAP server configuration
            if (string.IsNullOrEmpty(account.ImapServer))
            {
                _logger.LogInformation("Account {AccountId} ({Name}) has no IMAP server configured", accountId, account.Name);
                return new List<string> { "INBOX" }; // Return default folder instead of throwing error
            }

            try
            {
                using var client = CreateImapClient(account.Name);
                client.Timeout = 60000;
                client.ServerCertificateValidationCallback = ServerCertificateValidationCallback;
                await ConnectWithFallbackAsync(client, account.ImapServer, account.ImapPort ?? 993, account.UseSSL, account.Name);
                await AuthenticateClientAsync(client, account);

                // Use the centralized robust folder retrieval method
                var allFolders = await GetAllFoldersAsync(client, account.Name);

                await client.DisconnectAsync(true);
                
                // Convert IMailFolder list to string list and sort
                return allFolders.Select(f => f.FullName).OrderBy(f => f).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving folders for account {AccountId}: {Message}", accountId, ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Deletes old emails from the local archive based on LocalRetentionDays setting
        /// </summary>
        private async Task<int> DeleteOldLocalEmailsAsync(MailAccount account, string? jobId = null)
        {
            if (!account.LocalRetentionDays.HasValue || account.LocalRetentionDays.Value <= 0)
            {
                return 0;
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-account.LocalRetentionDays.Value);

            _logger.LogInformation("Starting deletion of local archived emails older than {Days} days (before {CutoffDate}) for account {AccountName}",
                account.LocalRetentionDays.Value, cutoffDate, account.Name);

            try
            {
                // Find all emails older than the cutoff date for this account
                var emailsToDelete = await _context.ArchivedEmails
                    .Where(e => e.MailAccountId == account.Id && e.SentDate < cutoffDate)
                    .Select(e => new { e.Id, e.Subject, e.From, e.SentDate })
                    .ToListAsync();

                if (emailsToDelete.Count == 0)
                {
                    _logger.LogInformation("No emails found to delete from local archive for account {AccountName}", account.Name);
                    return 0;
                }

                _logger.LogInformation("Found {Count} emails to delete from local archive for account {AccountName}",
                    emailsToDelete.Count, account.Name);

                // Delete in batches to avoid memory issues
                var batchSize = _batchOptions.BatchSize;
                var deletedCount = 0;

                for (int i = 0; i < emailsToDelete.Count; i += batchSize)
                {
                    var batch = emailsToDelete.Skip(i).Take(batchSize).ToList();
                    var batchIds = batch.Select(e => e.Id).ToList();

                    try
                    {
                        // Delete attachments first (cascade delete should handle this, but being explicit)
                        var attachmentsDeleted = await _context.EmailAttachments
                            .Where(a => batchIds.Contains(a.ArchivedEmailId))
                            .ExecuteDeleteAsync();

                        // Delete emails
                        var emailsDeleted = await _context.ArchivedEmails
                            .Where(e => batchIds.Contains(e.Id))
                            .ExecuteDeleteAsync();

                        deletedCount += emailsDeleted;

                        _logger.LogDebug("Deleted batch of {Count} emails ({Attachments} attachments) from local archive",
                            emailsDeleted, attachmentsDeleted);

                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting batch from local archive for account {AccountName}: {Message}",
                            account.Name, ex.Message);
                    }

                    // Add a small delay between batches
                    if (i + batchSize < emailsToDelete.Count && _batchOptions.PauseBetweenBatchesMs > 0)
                    {
                        await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                    }
                }

                _logger.LogInformation("Completed local archive deletion for account {AccountName}. Deleted {Count} emails",
                    account.Name, deletedCount);

                // Log the deletion summary to AccessLogs
                if (deletedCount > 0)
                {
                    try
                    {
                        var accessLog = new AccessLog
                        {
                            Username = "System",
                            Type = AccessLogType.Deletion,
                            Timestamp = DateTime.UtcNow,
                            SearchParameters = $"Local retention: Deleted {deletedCount} emails older than {account.LocalRetentionDays} days from local archive",
                            MailAccountId = account.Id
                        };

                        _context.AccessLogs.Add(accessLog);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception logEx)
                    {
                        _logger.LogWarning(logEx, "Failed to log local retention deletion summary to AccessLogs");
                    }
                }

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during local archive deletion for account {AccountName}: {Message}",
                    account.Name, ex.Message);
                return 0;
            }
        }

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
                // Use the centralized robust folder retrieval method
                var allFolders = await GetAllFoldersAsync(client, account.Name);

                // Process each folder for deletion
                foreach (var folder in allFolders)
                {
                    // Skip empty or invalid folder names
                    if (folder == null || string.IsNullOrEmpty(folder.FullName) ||
                        folder.Attributes.HasFlag(FolderAttributes.NonExistent) ||
                        folder.Attributes.HasFlag(FolderAttributes.NoSelect))
                    {
                        _logger.LogInformation("Skipping folder {FolderName} (null, empty name, non-existent or non-selectable) for deletion",
                            folder?.FullName ?? "NULL");
                        continue;
                    }

                    // Skip excluded folders
                    if (account.ExcludedFoldersList.Contains(folder.FullName))
                    {
                        _logger.LogInformation("Skipping excluded folder for deletion: {FolderName} for account: {AccountName}",
                            folder.FullName, account.Name);
                        continue;
                    }

                    try
                    {
                        // Ensure connection is still active before opening folder
                        if (!client.IsConnected)
                        {
                            _logger.LogWarning("Client disconnected during deletion, attempting to reconnect...");
                            await ReconnectClientAsync(client, account);
                        }
                        else if (!client.IsAuthenticated)
                        {
                            _logger.LogWarning("Client not authenticated during deletion, attempting to re-authenticate...");
                            await AuthenticateClientAsync(client, account);
                        }

                        // Ensure folder is open with read-write access
                        if (!folder.IsOpen || folder.Access != FolderAccess.ReadWrite)
                        {
                            await folder.OpenAsync(FolderAccess.ReadWrite);
                        }

                        // First try using SearchQuery.SentBefore for efficiency
                        var uids = await folder.SearchAsync(SearchQuery.SentBefore(cutoffDate));

                        // Log the results of the search query for debugging
                        _logger.LogInformation("SearchQuery.SentBefore found {Count} emails in folder {FolderName} for account {AccountName}",
                            uids.Count, folder.FullName, account.Name);

                        // If we found emails, let's log some details about them for debugging
                        if (uids.Any())
                        {
                            // Fetch envelopes for the found emails to check their actual dates
                            var summaries = await folder.FetchAsync(uids.Take(10).ToList(), MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.InternalDate);

                            foreach (var summary in summaries)
                            {
                                // Use only the envelope date (sent date) of the mail
                                DateTime? emailDate = null;

                                if (summary.Envelope?.Date.HasValue == true)
                                {
                                    emailDate = DateTime.SpecifyKind(summary.Envelope.Date.Value.DateTime, DateTimeKind.Utc);
                                }

                                // Log the email date for debugging
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

                        // Process in batches to avoid memory issues and server timeouts
                        var batchSize = _batchOptions.BatchSize;
                        for (int i = 0; i < uids.Count; i += batchSize)
                        {
                            var batch = uids.Skip(i).Take(batchSize).ToList();

                            // Get message IDs for this batch to check if they're archived
                            var messageSummaries = await folder.FetchAsync(batch, MessageSummaryItems.UniqueId | MessageSummaryItems.Headers);

                            // Collect UIDs of emails that are archived and can be deleted
                            var uidsToDelete = new List<UniqueId>();

                            foreach (var summary in messageSummaries)
                            {
                                var messageId = summary.Headers["Message-ID"];

                                // Log the raw Message-ID for debugging
                                _logger.LogDebug("Raw Message-ID from IMAP: {RawMessageId}", messageId ?? "NULL");

                                // If there's no Message-ID header, construct it the same way as in ArchiveEmailAsync
                                if (string.IsNullOrEmpty(messageId))
                                {
                                    var from = summary.Envelope?.From?.ToString() ?? string.Empty;
                                    var to = summary.Envelope?.To?.ToString() ?? string.Empty;
                                    var subject = summary.Envelope?.Subject ?? string.Empty;
                                    var dateTicks = summary.InternalDate?.Ticks ?? 0;

                                    messageId = $"{from}-{to}-{subject}-{dateTicks}";
                                    _logger.LogDebug("Constructed Message-ID: {ConstructedMessageId}", messageId);
                                }

                                // Check if this email is already archived
                                var isArchived = await _context.ArchivedEmails
                                    .AnyAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);

                                // Also check with angle brackets if not already found
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
                                // Also check without angle brackets if not already found
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
                                    // Log additional info for debugging
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

                                    // Delete the emails by adding the Deleted flag
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

                                    // Try to reconnect and retry once
                                    try
                                    {
                                        _logger.LogInformation("Attempting to reconnect and retry deletion...");
                                        await ReconnectClientAsync(client, account);
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

                            // Add a small delay between batches to avoid overwhelming the server
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

        private async Task ReconnectClientAsync(ImapClient client, MailAccount account)
        {
            try
            {
                if (client.IsConnected)
                {
                    await client.DisconnectAsync(true);
                }

                // Use the configurable pause between batches as reconnection delay
                if (_batchOptions.PauseBetweenBatchesMs > 0)
                {
                    await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                }

                _logger.LogInformation("Reconnecting to IMAP server for account {AccountName}", account.Name);
                await ConnectWithFallbackAsync(client, account.ImapServer, account.ImapPort ?? 993, account.UseSSL, account.Name);
                client.ServerCertificateValidationCallback = ServerCertificateValidationCallback;
                await AuthenticateClientAsync(client, account);
                _logger.LogInformation("Successfully reconnected to IMAP server for account {AccountName}", account.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect to IMAP server for account {AccountName}", account.Name);
                throw new InvalidOperationException("Failed to reconnect to IMAP server", ex);
            }
        }

        /// <summary>
        /// Validates the server certificate based on the IgnoreSelfSignedCert setting
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="certificate">The certificate</param>
        /// <param name="chain">The certificate chain</param>
        /// <param name="sslPolicyErrors">The SSL policy errors</param>
        /// <returns>True if the certificate is valid or should be accepted, false otherwise</returns>
        private bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // If there are no SSL policy errors, the certificate is valid
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // If we're configured to ignore self-signed certificates and the only error is
            // that the certificate is untrusted (which is typical for self-signed certs),
            // then accept the certificate
            if (_mailSyncOptions.IgnoreSelfSignedCert &&
                (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors ||
                 sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch))
            {
                // Additional check: if it's a chain error, verify it's specifically a self-signed certificate
                if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors && chain.ChainStatus.Length > 0)
                {
                    // Check if the chain status indicates a self-signed certificate
                    bool isSelfSigned = chain.ChainStatus.All(status =>
                        status.Status == X509ChainStatusFlags.UntrustedRoot ||
                        status.Status == X509ChainStatusFlags.PartialChain ||
                        status.Status == X509ChainStatusFlags.RevocationStatusUnknown);

                    if (isSelfSigned)
                    {
                        _logger.LogDebug("Accepting self-signed certificate for IMAP server");
                        return true;
                    }
                }
                else if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch)
                {
                    _logger.LogDebug("Accepting certificate with name mismatch for IMAP server (IgnoreSelfSignedCert=true)");
                    return true;
                }
            }

            // Log the certificate validation error
            _logger.LogWarning("Certificate validation failed for IMAP server: {SslPolicyErrors}", sslPolicyErrors);
            return false;
        }


        /// <summary>
        /// Connects to an IMAP server with fallback from SSL to STARTTLS if needed
        /// </summary>
        /// <param name="client">The IMAP client to connect</param>
        /// <param name="server">The server hostname</param>
        /// <param name="port">The server port</param>
        /// <param name="useSSL">Whether to attempt SSL connection</param>
        /// <param name="accountName">The account name for logging</param>
        /// <returns>Task</returns>
        private async Task ConnectWithFallbackAsync(ImapClient client, string server, int port, bool useSSL, string accountName)
        {
            if (!useSSL)
            {
                _logger.LogDebug("Connecting to {Server}:{Port} with no security for account {AccountName}", 
                    server, port, accountName);
                await client.ConnectAsync(server, port, SecureSocketOptions.None);
                return;
            }

            // First try: SSL/TLS directly
            try
            {
                _logger.LogDebug("Connecting to {Server}:{Port} with SSL/TLS for account {AccountName}", 
                    server, port, accountName);
                await client.ConnectAsync(server, port, SecureSocketOptions.SslOnConnect);
                _logger.LogDebug("Successfully connected using SSL/TLS for account {AccountName}", accountName);
            }
            catch (MailKit.Security.SslHandshakeException sslEx)
            {
                _logger.LogDebug("SSL/TLS connection failed for account {AccountName}, trying STARTTLS: {Message}", 
                    accountName, sslEx.Message);
                
                // Fallback: STARTTLS
                try
                {
                    await client.ConnectAsync(server, port, SecureSocketOptions.StartTls);
                    _logger.LogInformation("Successfully connected using STARTTLS for account {AccountName} on {Server}:{Port}", 
                        accountName, server, port);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "STARTTLS fallback also failed for account {AccountName}", accountName);
                    throw new AggregateException("Both SSL/TLS and STARTTLS connection attempts failed", sslEx, fallbackEx);
                }
            }
        }

        // Create an ImapClient without protocol logging
        private ImapClient CreateImapClient(string accountName)
        {
            // Return ImapClient without ProtocolLogger to suppress IMAP negotiation logging
            return new ImapClient();
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "account";
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
