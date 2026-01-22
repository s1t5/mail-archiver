using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Core;
using MailArchiver.Services.Providers;
using MailArchiver.Utilities;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace MailArchiver.Services.Providers
{
    /// <summary>
    /// IMAP email provider service
    /// Handles IMAP-specific operations: sync, connection, folders, restore
    /// </summary>
    public class ImapEmailService : IProviderEmailService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<ImapEmailService> _logger;
        private readonly EmailCoreService _coreService;
        private readonly ISyncJobService _syncJobService;
        private readonly BatchOperationOptions _batchOptions;
        private readonly MailSyncOptions _mailSyncOptions;
        private readonly DateTimeHelper _dateTimeHelper;

        public ImapEmailService(
            MailArchiverDbContext context,
            ILogger<ImapEmailService> logger,
            EmailCoreService coreService,
            ISyncJobService syncJobService,
            IOptions<BatchOperationOptions> batchOptions,
            IOptions<MailSyncOptions> mailSyncOptions,
            DateTimeHelper dateTimeHelper)
        {
            _context = context;
            _logger = logger;
            _coreService = coreService;
            _syncJobService = syncJobService;
            _batchOptions = batchOptions.Value;
            _mailSyncOptions = mailSyncOptions.Value;
            _dateTimeHelper = dateTimeHelper;
        }

        #region Interface Implementation (IProviderEmailService)

        public async Task SyncMailAccountAsync(MailAccount account, string? jobId = null)
        {
            _logger.LogInformation("Starting IMAP sync for account: {AccountName}", account.Name);

            using var client = CreateImapClient(account.Name);
            client.Timeout = 300000;
            client.ServerCertificateValidationCallback = ServerCertificateValidationCallback;

            var processedFolders = 0;
            var processedEmails = 0;
            var newEmails = 0;
            var failedEmails = 0;
            var deletedEmails = 0;

            try
            {
                await ConnectWithFallbackAsync(client, account.ImapServer, account.ImapPort ?? 993, account.UseSSL, account.Name);
                await AuthenticateClientAsync(client, account);
                _logger.LogInformation("Connected to IMAP server for {AccountName}", account.Name);

                var allFolders = await GetAllFoldersAsync(client, account.Name);

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

                if (failedEmails == 0)
                {
                    // Update LastSync using a separate tracked entity to avoid tracking conflicts
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

                    // Use untruncated body if available
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

            return await RestoreMultipleEmailsWithSharedConnectionAndProgressAsync(emailIds, targetAccountId, folderName, progressCallback, cancellationToken);
        }

        public async Task<bool> ResyncAccountAsync(int accountId)
        {
            try
            {
                // First, update LastSync in the database using a tracked entity
                var account = await _context.MailAccounts.FindAsync(accountId);
                if (account == null)
                {
                    _logger.LogError("Account with ID {AccountId} not found for resync", accountId);
                    return false;
                }

                _logger.LogInformation("Starting full resync for IMAP account {AccountName}", account.Name);

                account.LastSync = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                await _context.SaveChangesAsync();

                // Now fetch the account again for the sync operation to avoid tracking conflicts
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

        public async Task<int> GetEmailCountByAccountAsync(int accountId)
        {
            return await _coreService.GetEmailCountByAccountAsync(accountId);
        }

        #endregion

        #region IMAP Connection Management

        private string GetAuthenticationUsername(MailAccount account)
        {
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

        #endregion

        #region IMAP Sync

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

                        // Some IMAP servers have limitations where SearchAsync returns a limited number
                        // of results even when more messages exist. Detect this by comparing search results to actual folder count.
                        // If search returned fewer results than the folder contains, fall back to fetching all UIDs by sequence.
                        if (folder.Count > 0 && uids.Count > 0 && uids.Count < folder.Count)
                        {
                            double percentageReturned = (double)uids.Count / folder.Count * 100;
                            
                            _logger.LogWarning("Search returned only {ReturnedCount} of {TotalCount} messages ({Percentage:F1}%) for {FolderName}. " +
                                "Server likely has SEARCH result limit. Fetching all UIDs by sequence instead.",
                                uids.Count, folder.Count, percentageReturned, folder.FullName);
                            
                            // Fetch all UIDs using sequence numbers (1:*)
                            var allUids = await folder.FetchAsync(0, -1, MessageSummaryItems.UniqueId);
                            uids = allUids.Select(msg => msg.UniqueId).ToList();
                            
                            _logger.LogInformation("Retrieved {Count} UIDs by sequence for folder {FolderName}",
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
                            
                            // Check for server search limit on fallback too
                            if (folder.Count > 0 && uids.Count > 0 && uids.Count < folder.Count)
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
                            // If both fail, get all UIDs and filter client-side
                            _logger.LogWarning(fallbackEx, "SentSince also failed for folder {FolderName}, using All query",
                                folder.FullName);
                            uids = await folder.SearchAsync(SearchQuery.All);
                            _logger.LogInformation("All query found {Count} total messages in folder {FolderName}, will filter by date client-side",
                                uids.Count, folder.FullName);
                            
                            // Check for server search limit on All query too
                            if (folder.Count > 0 && uids.Count > 0 && uids.Count < folder.Count)
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
                                var isNew = await _coreService.ArchiveEmailAsync(account, message, isOutgoing, folder.FullName);
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

        private class SyncFolderResult
        {
            public int ProcessedEmails { get; set; }
            public int NewEmails { get; set; }
            public int FailedEmails { get; set; }
        }

        #endregion

        #region IMAP Folders

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

        #endregion

        #region IMAP Restore

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

                // Use untruncated body if available
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

        #endregion

        #region IMAP Retention

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

        #endregion
    }
}
