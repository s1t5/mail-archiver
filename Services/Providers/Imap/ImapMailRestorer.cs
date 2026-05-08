using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Utilities;
using MailKit;
using MailKit.Net.Imap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MailArchiver.Services.Providers.Imap
{
    /// <summary>
    /// Restores archived emails back to an IMAP mailbox.
    /// Handles single-email restore, batch restore with shared connection and retry logic,
    /// and folder-structure-preserving batch restore.
    /// </summary>
    public class ImapMailRestorer
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<ImapMailRestorer> _logger;
        private readonly ImapConnectionFactory _connectionFactory;
        private readonly DateTimeHelper _dateTimeHelper;
        private readonly BatchOperationOptions _batchOptions;

        public ImapMailRestorer(
            MailArchiverDbContext context,
            ILogger<ImapMailRestorer> logger,
            ImapConnectionFactory connectionFactory,
            DateTimeHelper dateTimeHelper,
            IOptions<BatchOperationOptions> batchOptions)
        {
            _context = context;
            _logger = logger;
            _connectionFactory = connectionFactory;
            _dateTimeHelper = dateTimeHelper;
            _batchOptions = batchOptions.Value;
        }

        /// <summary>
        /// Restores a single archived email to an IMAP folder.
        /// </summary>
        public async Task<bool> RestoreEmailToFolderAsync(int emailId, int targetAccountId, string folderName, bool preserveFolderStructure = false)
        {
            _logger.LogInformation("RestoreEmailToFolderAsync called with parameters: emailId={EmailId}, targetAccountId={TargetAccountId}, folderName={FolderName}, preserveFolderStructure={Preserve}",
                emailId, targetAccountId, folderName, preserveFolderStructure);

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

                _logger.LogInformation("Found email: Subject='{Subject}', From='{From}', Attachments={AttachmentCount}, OriginalFolder={OriginalFolder}",
                    email.Subject, email.From, email.Attachments.Count, email.FolderName);

                var targetAccount = await _context.MailAccounts.FindAsync(targetAccountId);
                if (targetAccount == null)
                {
                    _logger.LogError("Target account with ID {AccountId} not found", targetAccountId);
                    return false;
                }

                _logger.LogInformation("Found target account: {AccountName}, {EmailAddress}",
                    targetAccount.Name, targetAccount.EmailAddress);

                var message = await CreateMimeMessageFromArchivedEmailAsync(email, targetAccount.Name);
                if (message == null)
                    return false;

                try
                {
                    using var client = _connectionFactory.CreateImapClient(targetAccount.Name);
                    client.Timeout = 180000;
                    client.ServerCertificateValidationCallback = _connectionFactory.ServerCertificateValidationCallback;
                    _logger.LogInformation("Connecting to IMAP server {Server}:{Port} for account {AccountName}",
                        targetAccount.ImapServer, targetAccount.ImapPort, targetAccount.Name);

                    await _connectionFactory.ConnectWithFallbackAsync(client, targetAccount.ImapServer, targetAccount.ImapPort ?? 993, targetAccount.UseSSL, targetAccount.Name);
                    _logger.LogInformation("Connected to IMAP server, authenticating using {Provider} authentication", targetAccount.Provider);

                    await _connectionFactory.AuthenticateClientAsync(client, targetAccount);

                    // Determine the actual target folder - with or without structure preservation
                    string actualTargetFolder = folderName;
                    if (preserveFolderStructure)
                    {
                        var originalFolder = email.FolderName ?? "INBOX";
                        if (string.Equals(folderName, "INBOX", StringComparison.OrdinalIgnoreCase))
                        {
                            actualTargetFolder = originalFolder;
                        }
                        else
                        {
                            actualTargetFolder = $"{folderName}/{originalFolder}";
                        }
                        _logger.LogInformation("PreserveFolderStructure enabled: original folder '{OriginalFolder}' -> target '{TargetFolder}'",
                            originalFolder, actualTargetFolder);
                    }

                    _logger.LogInformation("Looking for folder: {FolderName}", actualTargetFolder);

                    IMailFolder folder;
                    try
                    {
                        folder = await client.GetFolderAsync(actualTargetFolder);
                        _logger.LogInformation("Found folder: {FolderName}", folder.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not find folder '{FolderName}', trying to create it", actualTargetFolder);

                        try
                        {
                            var folderParts = actualTargetFolder.Split('/', '\\');
                            var parentFolder = client.Inbox;

                            if (!string.Equals(folderParts[0], "INBOX", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    parentFolder = await client.GetFolderAsync(folderParts[0]);
                                }
                                catch
                                {
                                    parentFolder = await client.Inbox.CreateAsync(folderParts[0], true);
                                }
                            }

                            for (int i = 1; i < folderParts.Length; i++)
                            {
                                if (string.IsNullOrWhiteSpace(folderParts[i])) continue;

                                try
                                {
                                    var existingFolder = await client.GetFolderAsync($"{parentFolder.FullName}/{folderParts[i]}");
                                    parentFolder = existingFolder;
                                }
                                catch
                                {
                                    parentFolder = await parentFolder.CreateAsync(folderParts[i], true);
                                }
                            }

                            folder = parentFolder;
                            _logger.LogInformation("Created folder hierarchy: {FolderName}", folder.FullName);
                        }
                        catch (Exception createEx)
                        {
                            _logger.LogWarning(createEx, "Could not create folder '{FolderName}', falling back to INBOX", actualTargetFolder);
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
                        var receivedSource = email.ReceivedDate == default ? email.SentDate : email.ReceivedDate;
                        var internalDateUtc = _dateTimeHelper.ConvertFromDisplayTimeZoneToUtc(receivedSource);
                        var internalDate = new DateTimeOffset(internalDateUtc, TimeSpan.Zero);
                        await folder.AppendAsync(message, MessageFlags.Seen, internalDate);
                        _logger.LogInformation("Message successfully appended to folder {FolderName} with INTERNALDATE {InternalDate}",
                            folder.FullName, internalDate);
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

        /// <summary>
        /// Restores multiple emails using a shared IMAP connection with progress tracking and retry logic.
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
                client = _connectionFactory.CreateImapClient(targetAccount.Name);
                var connectionResult = await EstablishImapConnectionAsync(client, targetAccount, folderName);
                if (!connectionResult.Success)
                {
                    _logger.LogError("Failed to establish initial IMAP connection: {Error}", connectionResult.ErrorMessage);
                    return (0, emailIds.Count);
                }
                targetFolder = connectionResult.Folder;

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
                                    await Task.Delay(1000 * retryCount, cancellationToken);

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

                        var totalProcessed = successCount + failCount;
                        progressCallback?.Invoke(totalProcessed, successCount, failCount);

                        if (_batchOptions.PauseBetweenEmailsMs > 0)
                        {
                            await Task.Delay(_batchOptions.PauseBetweenEmailsMs, cancellationToken);
                        }
                    }

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
                failCount = emailIds.Count - successCount;
            }
            finally
            {
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
        /// Restores multiple emails while preserving the original folder structure.
        /// Creates the original folder hierarchy under the target base folder.
        /// </summary>
        public async Task<(int Successful, int Failed)> RestoreMultipleEmailsWithFolderStructureAsync(
            List<int> emailIds,
            int targetAccountId,
            string baseFolderName,
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

            _logger.LogInformation("Restoring {Count} emails with folder structure preservation to account {AccountName}, base folder: {BaseFolder}",
                emailIds.Count, targetAccount.Name, baseFolderName);

            var emailsWithFolders = await _context.ArchivedEmails
                .Where(e => emailIds.Contains(e.Id))
                .Select(e => new { e.Id, e.FolderName })
                .ToListAsync(cancellationToken);

            var emailsByFolder = emailsWithFolders
                .GroupBy(e => e.FolderName ?? "INBOX")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Id).ToList());

            _logger.LogInformation("Emails grouped into {FolderCount} distinct folders", emailsByFolder.Count);

            ImapClient client = null;

            try
            {
                client = _connectionFactory.CreateImapClient(targetAccount.Name);
                client.Timeout = 180000;
                client.ServerCertificateValidationCallback = _connectionFactory.ServerCertificateValidationCallback;

                await _connectionFactory.ConnectWithFallbackAsync(client, targetAccount.ImapServer, targetAccount.ImapPort ?? 993, targetAccount.UseSSL, targetAccount.Name);
                await _connectionFactory.AuthenticateClientAsync(client, targetAccount);

                IMailFolder baseFolder;
                try
                {
                    baseFolder = await client.GetFolderAsync(baseFolderName);
                    _logger.LogInformation("Base folder '{BaseFolder}' exists at path: {FullName}", baseFolderName, baseFolder.FullName);
                }
                catch
                {
                    try
                    {
                        baseFolder = await client.Inbox.CreateAsync(baseFolderName, true);
                        _logger.LogInformation("Created base folder '{BaseFolder}' at path: {FullName}", baseFolderName, baseFolder.FullName);
                    }
                    catch (Exception createEx)
                    {
                        _logger.LogWarning(createEx, "Could not create base folder '{BaseFolder}', using INBOX", baseFolderName);
                        baseFolder = client.Inbox;
                        baseFolderName = "INBOX";
                    }
                }

                foreach (var folderGroup in emailsByFolder)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var originalFolderName = folderGroup.Key;
                    var folderEmailIds = folderGroup.Value;

                    _logger.LogInformation("Processing {Count} emails from original folder '{OriginalFolder}'",
                        folderEmailIds.Count, originalFolderName);

                    IMailFolder targetFolder = null;
                    try
                    {
                        char separator;
                        string relativePath;

                        if (originalFolderName.StartsWith("INBOX/", StringComparison.OrdinalIgnoreCase))
                        {
                            separator = '/';
                            relativePath = originalFolderName.Substring(6);
                        }
                        else if (originalFolderName.StartsWith("INBOX\\", StringComparison.OrdinalIgnoreCase))
                        {
                            separator = '\\';
                            relativePath = originalFolderName.Substring(6);
                        }
                        else if (originalFolderName.StartsWith("INBOX.", StringComparison.OrdinalIgnoreCase))
                        {
                            separator = '.';
                            relativePath = originalFolderName.Substring(6);
                        }
                        else
                        {
                            if (originalFolderName.Contains('/'))
                                separator = '/';
                            else if (originalFolderName.Contains('\\'))
                                separator = '\\';
                            else if (originalFolderName.Contains('.'))
                                separator = '.';
                            else
                                separator = '/';

                            relativePath = originalFolderName;
                        }

                        var folderParts = relativePath.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);

                        _logger.LogDebug("Creating folder hierarchy under '{BaseFolder}' using separator '{Separator}': {Parts}",
                            baseFolderName, separator, string.Join(" " + separator + " ", folderParts));

                        var currentFolder = baseFolder;

                        foreach (var part in folderParts)
                        {
                            if (string.IsNullOrWhiteSpace(part)) continue;

                            IMailFolder nextFolder = null;

                            try
                            {
                                var searchPath = currentFolder == baseFolder
                                    ? (string.Equals(baseFolderName, "INBOX", StringComparison.OrdinalIgnoreCase)
                                        ? part
                                        : $"{baseFolderName}{separator}{part}")
                                    : $"{currentFolder.FullName}{separator}{part}";

                                nextFolder = await client.GetFolderAsync(searchPath);
                                _logger.LogDebug("Found existing folder: {FolderPath}", searchPath);
                            }
                            catch
                            {
                                try
                                {
                                    _logger.LogInformation("Creating subfolder '{Part}' under '{ParentFolder}'", part, currentFolder.FullName);
                                    nextFolder = await currentFolder.CreateAsync(part, true);
                                    _logger.LogInformation("Created folder: {FullName}", nextFolder.FullName);
                                }
                                catch (Exception createEx)
                                {
                                    _logger.LogWarning(createEx, "Failed to create folder '{Part}' under '{ParentFolder}', using parent",
                                        part, currentFolder.FullName);
                                    nextFolder = currentFolder;
                                    break;
                                }
                            }

                            currentFolder = nextFolder;
                        }

                        targetFolder = currentFolder;
                        _logger.LogInformation("Target folder resolved to: {FullName}", targetFolder.FullName);
                    }
                    catch (Exception folderEx)
                    {
                        _logger.LogError(folderEx, "Error creating folder hierarchy for '{OriginalFolder}' under '{BaseFolder}', using base folder",
                            originalFolderName, baseFolderName);
                        targetFolder = baseFolder;
                    }

                    try
                    {
                        await targetFolder.OpenAsync(FolderAccess.ReadWrite);
                        _logger.LogDebug("Opened folder {FolderName} for writing", targetFolder.FullName);
                    }
                    catch (Exception openEx)
                    {
                        _logger.LogWarning(openEx, "Could not open folder {FolderName} with ReadWrite, trying ReadOnly", targetFolder.FullName);
                        try
                        {
                            await targetFolder.OpenAsync(FolderAccess.ReadOnly);
                        }
                        catch
                        {
                            _logger.LogError("Could not open folder {FolderName} at all, skipping {Count} emails", targetFolder.FullName, folderEmailIds.Count);
                            failCount += folderEmailIds.Count;
                            progressCallback?.Invoke(successCount + failCount, successCount, failCount);
                            continue;
                        }
                    }

                    foreach (var emailId in folderEmailIds)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            var result = await RestoreEmailWithSharedConnectionAsync(emailId, client, targetFolder, targetAccount.Name);
                            if (result)
                            {
                                successCount++;
                                _logger.LogDebug("Successfully restored email {EmailId} to folder {FolderName}", emailId, targetFolder.FullName);
                            }
                            else
                            {
                                failCount++;
                                _logger.LogWarning("Failed to restore email {EmailId} to folder {FolderName}", emailId, targetFolder.FullName);
                            }
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            _logger.LogError(ex, "Error restoring email {EmailId} to folder {FolderName}: {Message}", emailId, targetFolder.FullName, ex.Message);
                        }

                        progressCallback?.Invoke(successCount + failCount, successCount, failCount);

                        if (_batchOptions.PauseBetweenEmailsMs > 0)
                        {
                            await Task.Delay(_batchOptions.PauseBetweenEmailsMs, cancellationToken);
                        }
                    }

                    if (_batchOptions.PauseBetweenBatchesMs > 0)
                    {
                        await Task.Delay(_batchOptions.PauseBetweenBatchesMs, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Folder structure restore was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during folder structure restore: {Message}", ex.Message);
                failCount = emailIds.Count - successCount;
            }
            finally
            {
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

            _logger.LogInformation("Folder structure restore completed. Success: {SuccessCount}, Failed: {FailCount}", successCount, failCount);
            return (successCount, failCount);
        }

        /// <summary>
        /// Restores a single email using an existing IMAP connection.
        /// </summary>
        public async Task<bool> RestoreEmailWithSharedConnectionAsync(int emailId, ImapClient client, IMailFolder targetFolder, string accountName)
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

                var message = await CreateMimeMessageFromArchivedEmailAsync(email, accountName);
                if (message == null)
                {
                    return false;
                }

                var receivedSource = email.ReceivedDate == default ? email.SentDate : email.ReceivedDate;
                var internalDateUtc = _dateTimeHelper.ConvertFromDisplayTimeZoneToUtc(receivedSource);
                var internalDate = new DateTimeOffset(internalDateUtc, TimeSpan.Zero);
                await targetFolder.AppendAsync(message, MessageFlags.Seen, internalDate);
                _logger.LogDebug("Email {EmailId} successfully appended to folder {FolderName} with INTERNALDATE {InternalDate}",
                    emailId, targetFolder.FullName, internalDate);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring email {EmailId} with shared connection: {Message}", emailId, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Creates a MimeMessage from an ArchivedEmail entity, including body, attachments, and inline images.
        /// </summary>
        public async Task<MimeMessage> CreateMimeMessageFromArchivedEmailAsync(ArchivedEmail email, string accountName)
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

                // Use original body if available (preserves null bytes), otherwise use untruncated or regular body
                var htmlBodyToRestore = email.OriginalBodyHtml != null
                    ? System.Text.Encoding.UTF8.GetString(email.OriginalBodyHtml)
                    : (!string.IsNullOrEmpty(email.BodyUntruncatedHtml)
                        ? email.BodyUntruncatedHtml
                        : email.HtmlBody);

                var textBodyToRestore = email.OriginalBodyText != null
                    ? System.Text.Encoding.UTF8.GetString(email.OriginalBodyText)
                    : (!string.IsNullOrEmpty(email.BodyUntruncatedText)
                        ? email.BodyUntruncatedText
                        : email.Body);

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

                    // Add inline attachments first so they can be referenced in the HTML body
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
                var sentUtc = _dateTimeHelper.ConvertFromDisplayTimeZoneToUtc(email.SentDate);
                message.Date = new DateTimeOffset(sentUtc, TimeSpan.Zero);
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

        /// <summary>
        /// Establishes an IMAP connection and opens the target folder.
        /// </summary>
        private async Task<(bool Success, ImapClient Client, IMailFolder Folder, string ErrorMessage)> EstablishImapConnectionAsync(
            ImapClient client, MailAccount targetAccount, string folderName)
        {
            try
            {
                client.Timeout = 180000;
                client.ServerCertificateValidationCallback = _connectionFactory.ServerCertificateValidationCallback;

                _logger.LogDebug("Connecting to IMAP server {Server}:{Port} for account {AccountName}",
                    targetAccount.ImapServer, targetAccount.ImapPort, targetAccount.Name);

                await _connectionFactory.ConnectWithFallbackAsync(client, targetAccount.ImapServer, targetAccount.ImapPort ?? 993, targetAccount.UseSSL, targetAccount.Name);

                _logger.LogDebug("Authenticating with {Provider} authentication", targetAccount.Provider);
                await _connectionFactory.AuthenticateClientAsync(client, targetAccount);

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
        /// Restores the IMAP connection by disposing the old client and creating a new one.
        /// </summary>
        private async Task<(bool Success, ImapClient Client, IMailFolder Folder, string ErrorMessage)> RestoreImapConnectionAsync(
            ImapClient existingClient, MailAccount targetAccount, string folderName)
        {
            _logger.LogInformation("Attempting to restore IMAP connection for account {AccountName}", targetAccount.Name);

            try
            {
                if (existingClient != null)
                {
                    try
                    {
                        if (existingClient.IsConnected)
                        {
                            await existingClient.DisconnectAsync(false);
                        }
                        existingClient.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error cleaning up existing client during reconnection");
                    }
                }

                var newClient = _connectionFactory.CreateImapClient(targetAccount.Name);
                return await EstablishImapConnectionAsync(newClient, targetAccount, folderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore IMAP connection: {Message}", ex.Message);
                return (false, null, null, ex.Message);
            }
        }

        /// <summary>
        /// Checks if the IMAP connection and folder are healthy.
        /// </summary>
        private static bool IsConnectionHealthy(ImapClient client, IMailFolder folder)
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
    }
}