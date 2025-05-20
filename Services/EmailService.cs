using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MailArchiver.Services
{
    public class EmailService : IEmailService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<EmailService> _logger;

        public EmailService(MailArchiverDbContext context, ILogger<EmailService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SyncMailAccountAsync(MailAccount account)
        {
            _logger.LogInformation("Starting sync for account: {AccountName}", account.Name);

            using var client = new ImapClient();
            client.Timeout = 900000; // 15 Minuten
            //client.ServerCertificateValidationCallback = (s, c, h, e) => true;



            try
            {
                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSSL);
                await client.AuthenticateAsync(account.Username, account.Password);

                _logger.LogInformation("Connected to IMAP server for {AccountName}", account.Name);

                // Prepare a list to store all folders
                var allFolders = new List<IMailFolder>();

                // Get personal namespace folders - main folders like Inbox, Sent, etc.
                foreach (var folder in client.GetFolders(client.PersonalNamespaces[0]))
                {
                    allFolders.Add(folder);

                    // Recursively add all subfolders
                    await AddSubfoldersRecursively(folder, allFolders);
                }

                _logger.LogInformation("Found {Count} folders for account: {AccountName}",
                    allFolders.Count, account.Name);

                // Process each folder
                foreach (var folder in allFolders)
                {
                    try
                    {
                        await SyncFolderAsync(folder, account);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing folder {FolderName} for account {AccountName}: {Message}",
                            folder.FullName, account.Name, ex.Message);
                    }
                }

                // Update lastSync
                account.LastSync = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await client.DisconnectAsync(true);

                _logger.LogInformation("Sync completed for account: {AccountName}", account.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sync for account {AccountName}: {Message}",
                    account.Name, ex.Message);
                throw;
            }
        }

        // Services/EmailService.cs - Neue Methode hinzufügen, um alle Ordner eines Kontos zu holen
        public async Task<List<string>> GetMailFoldersAsync(int accountId)
        {
            var account = await _context.MailAccounts.FindAsync(accountId);
            if (account == null)
            {
                _logger.LogError("Account with ID {AccountId} not found", accountId);
                return new List<string>();
            }

            try
            {
                using var client = new ImapClient();
                client.Timeout = 60000; // 1 Minute
                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSSL);
                await client.AuthenticateAsync(account.Username, account.Password);

                var allFolders = new List<string>();

                // Get all personal namespace folders
                foreach (var folder in client.GetFolders(client.PersonalNamespaces[0]))
                {
                    // Add the main folder
                    allFolders.Add(folder.FullName);

                    // Add all subfolders recursively
                    await AddSubfolderNamesRecursively(folder, allFolders);
                }

                await client.DisconnectAsync(true);

                return allFolders.OrderBy(f => f).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving folders for account {AccountId}: {Message}", accountId, ex.Message);
                return new List<string>();
            }
        }

        // Helper method to add subfolder names recursively
        private async Task AddSubfolderNamesRecursively(IMailFolder folder, List<string> folderNames)
        {
            try
            {
                // Get all subfolders of the current folder
                var subfolders = folder.GetSubfolders(false);
                foreach (var subfolder in subfolders)
                {
                    folderNames.Add(subfolder.FullName);
                    // Recursively add this folder's subfolders
                    await AddSubfolderNamesRecursively(subfolder, folderNames);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving subfolders for {FolderName}: {Message}",
                    folder.FullName, ex.Message);
            }
        }

        // Services/EmailService.cs - Korrigierte Methode

        public async Task<bool> RestoreEmailToFolderAsync(int emailId, int targetAccountId, string folderName)
        {
            _logger.LogInformation("RestoreEmailToFolderAsync called with parameters: emailId={EmailId}, targetAccountId={TargetAccountId}, folderName={FolderName}",
                emailId, targetAccountId, folderName);

            try
            {
                // 1. Load email with attachments from the archive
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

                // 2. Load the target account
                var targetAccount = await _context.MailAccounts.FindAsync(targetAccountId);
                if (targetAccount == null)
                {
                    _logger.LogError("Target account with ID {AccountId} not found", targetAccountId);
                    return false;
                }

                _logger.LogInformation("Found target account: {AccountName}, {EmailAddress}",
                    targetAccount.Name, targetAccount.EmailAddress);

                // 3. Try to construct a valid MimeMessage
                MimeMessage message = null;
                try
                {
                    message = new MimeMessage();
                    message.Subject = email.Subject;

                    // Parse From address
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

                    // Parse To addresses
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
                            // Add a placeholder address if parsing fails
                            message.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
                        }
                    }
                    else
                    {
                        // Ensure we have at least one recipient for the message to be valid
                        message.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
                    }

                    // Parse CC addresses if any
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
                            // Ignoring CC if parsing fails is acceptable
                        }
                    }

                    // Set message body
                    var bodyBuilder = new BodyBuilder();

                    if (!string.IsNullOrEmpty(email.HtmlBody))
                    {
                        bodyBuilder.HtmlBody = email.HtmlBody;
                    }

                    if (!string.IsNullOrEmpty(email.Body))
                    {
                        bodyBuilder.TextBody = email.Body;
                    }

                    // Add attachments if any
                    foreach (var attachment in email.Attachments)
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

                    // Set the message body
                    message.Body = bodyBuilder.ToMessageBody();

                    // Set other message properties
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

                // 4. Connect to the IMAP server and send the message
                try
                {
                    using var client = new ImapClient();
                    client.Timeout = 180000; // 3 Minuten

                    _logger.LogInformation("Connecting to IMAP server {Server}:{Port} for account {AccountName}",
                        targetAccount.ImapServer, targetAccount.ImapPort, targetAccount.Name);

                    await client.ConnectAsync(targetAccount.ImapServer, targetAccount.ImapPort, targetAccount.UseSSL);

                    _logger.LogInformation("Connected to IMAP server, authenticating as {Username}", targetAccount.Username);

                    await client.AuthenticateAsync(targetAccount.Username, targetAccount.Password);

                    _logger.LogInformation("Authenticated successfully, looking for folder: {FolderName}", folderName);

                    // Try to get the specified folder
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

                    // Open the folder in write mode
                    try
                    {
                        _logger.LogInformation("Opening folder {FolderName} with ReadWrite access", folder.FullName);
                        await folder.OpenAsync(FolderAccess.ReadWrite);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error opening folder {FolderName} with ReadWrite access", folder.FullName);

                        // Try with read-only access and then append
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

                    // Append the message to the folder
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

                    // Disconnect
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

        public async Task<(int Successful, int Failed)> RestoreMultipleEmailsAsync(
            List<int> emailIds,
            int targetAccountId,
            string folderName)
        {
            int successCount = 0;
            int failCount = 0;

            _logger.LogInformation("Starting batch restore of {Count} emails to account {AccountId}, folder {Folder}",
                emailIds.Count, targetAccountId, folderName);

            foreach (var emailId in emailIds)
            {
                try
                {
                    var result = await RestoreEmailToFolderAsync(emailId, targetAccountId, folderName);
                    if (result)
                    {
                        successCount++;
                        _logger.LogInformation("Successfully restored email {EmailId}", emailId);
                    }
                    else
                    {
                        failCount++;
                        _logger.LogWarning("Failed to restore email {EmailId}", emailId);
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger.LogError(ex, "Error restoring email {EmailId}", emailId);
                }
            }

            _logger.LogInformation("Batch restore completed. Success: {SuccessCount}, Failed: {FailCount}",
                successCount, failCount);

            return (successCount, failCount);
        }

        private async Task SyncFolderAsync(IMailFolder folder, MailAccount account)
        {
            _logger.LogInformation("Syncing folder: {FolderName} for account: {AccountName}",
                folder.FullName, account.Name);
            try
            {
                // Skip some special system folders that might cause issues
                if (folder.Attributes.HasFlag(FolderAttributes.NonExistent) ||
                    folder.Attributes.HasFlag(FolderAttributes.NoSelect))
                {
                    _logger.LogInformation("Skipping folder {FolderName} (non-existent or non-selectable)",
                        folder.FullName);
                    return;
                }

                await folder.OpenAsync(FolderAccess.ReadOnly);

                // Determine if this is an outgoing mail folder (like Sent Items)
                bool isOutgoing = IsOutgoingFolder(folder);

                // Get messages newer than last sync
                var lastSync = account.LastSync;
                var query = SearchQuery.DeliveredAfter(lastSync);

                try
                {
                    var uids = await folder.SearchAsync(query);
                    _logger.LogInformation("Found {Count} new messages in folder {FolderName} for account: {AccountName}",
                        uids.Count, folder.FullName, account.Name);

                    // Batch-Verarbeitung: Maximal 20 E-Mails auf einmal verarbeiten
                    const int batchSize = 20;
                    for (int i = 0; i < uids.Count; i += batchSize)
                    {
                        var batch = uids.Skip(i).Take(batchSize).ToList();
                        _logger.LogInformation("Processing batch of {Count} messages (starting at {Start}) in folder {FolderName}",
                            batch.Count, i, folder.FullName);

                        foreach (var uid in batch)
                        {
                            try
                            {
                                var message = await folder.GetMessageAsync(uid);
                                await ArchiveEmailAsync(account, message, isOutgoing, folder.FullName);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error archiving message {MessageNumber} from folder {FolderName}: {Message}",
                                    uid, folder.FullName, ex.Message);
                            }
                        }

                        // Kurze Pause zwischen Batches, um Ressourcen zu schonen
                        if (i + batchSize < uids.Count)
                        {
                            await Task.Delay(1000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error searching messages in folder {FolderName}: {Message}",
                        folder.FullName, ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing folder {FolderName}: {Message}",
                    folder.FullName, ex.Message);
            }
        }

        private bool IsOutgoingFolder(IMailFolder folder)
        {
            // Common names for sent folders in different languages and email systems
            var sentFolderNames = new[]
            {
        "sent", "sent items", "sent mail", "gesendet", "gesendete objekte",
        "gesendete", "enviado", "envoyé", "inviato", "verzonden"
    };

            string folderNameLower = folder.Name.ToLowerInvariant();

            // Check if folder name contains any of the common sent folder names
            return sentFolderNames.Any(name => folderNameLower.Contains(name)) ||
                   // Or check if the folder has the Sent flag
                   folder.Attributes.HasFlag(FolderAttributes.Sent);
        }

        private async Task AddSubfoldersRecursively(IMailFolder folder, List<IMailFolder> allFolders)
        {
            try
            {
                // Alle Unterordner des aktuellen Ordners abrufen
                var subfolders = folder.GetSubfolders(false);
                foreach (var subfolder in subfolders)
                {
                    // Nur nicht-auswählbare oder nicht-existierende Ordner überspringen
                    if (!subfolder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                        !subfolder.Attributes.HasFlag(FolderAttributes.NoSelect))
                    {
                        allFolders.Add(subfolder);
                    }

                    // Rekursiv die Unterordner dieses Ordners hinzufügen
                    await AddSubfoldersRecursively(subfolder, allFolders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving subfolders for {FolderName}: {Message}",
                    folder.FullName, ex.Message);
            }
        }

        private async Task<IMailFolder> GetSentFolderAsync(ImapClient client)
        {
            // Versuchen, den Ordner für gesendete Nachrichten zu finden
            // Verschiedene E-Mail-Anbieter verwenden unterschiedliche Namen
            string[] possibleNames = { "Sent", "Sent Items", "Gesendet", "Gesendete Objekte", "Sent Messages" };

            foreach (var name in possibleNames)
            {
                try
                {
                    var folder = await client.GetFolderAsync(name);
                    if (folder != null)
                        return folder;
                }
                catch
                {
                    // Ignorieren, wenn Ordner nicht existiert
                }
            }

            // Versuch, im Namespace nach Sent-Ordnern zu suchen
            foreach (var folder in client.GetFolders(client.PersonalNamespaces[0]))
            {
                if (folder.Name.ToLower().Contains("sent") || folder.Name.ToLower().Contains("gesend"))
                {
                    return folder;
                }
            }

            return null;
        }

        private async Task ArchiveEmailAsync(MailAccount account, MimeMessage message, bool isOutgoing, string? folderName = null)
        {
            // Check if this email is already archived
            var messageId = message.MessageId ??
                $"{message.From}-{message.To}-{message.Subject}-{message.Date.Ticks}";

            var existingEmail = await _context.ArchivedEmails
                .FirstOrDefaultAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);

            if (existingEmail != null)
                return;

            try
            {
                // Make sure the DateTime is in UTC
                DateTime sentDate = DateTime.SpecifyKind(message.Date.DateTime, DateTimeKind.Utc);

                // Reinige die Textdaten von Null-Bytes und anderen ungültigen UTF-8-Zeichen
                var subject = CleanText(message.Subject ?? "(No Subject)");
                var from = CleanText(message.From.ToString());
                var to = CleanText(message.To.ToString());
                var cc = CleanText(message.Cc?.ToString() ?? string.Empty);
                var bcc = CleanText(message.Bcc?.ToString() ?? string.Empty);

                // Speichere den Body immer als Text, aber HTML könnte zu lang sein für die Indizierung
                var body = CleanText(message.TextBody ?? string.Empty);

                // Speichere HTML separat nach spezieller Bereinigung
                var htmlBody = string.Empty;
                if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    htmlBody = CleanHtmlForStorage(message.HtmlBody);
                }

                var cleanMessageId = CleanText(messageId);
                var cleanFolderName = CleanText(folderName ?? string.Empty);

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
                    IsOutgoing = isOutgoing,
                    HasAttachments = message.Attachments.Any(),
                    Body = body,
                    HtmlBody = htmlBody,
                    FolderName = cleanFolderName
                };

                // Speichern in einem Try-Block
                try
                {
                    _context.ArchivedEmails.Add(archivedEmail);
                    await _context.SaveChangesAsync();

                    // Anhänge speichern
                    if (message.Attachments.Any())
                    {
                        foreach (var attachment in message.Attachments)
                        {
                            if (attachment is MimePart mimePart)
                            {
                                try
                                {
                                    using var ms = new MemoryStream();
                                    await mimePart.Content.DecodeToAsync(ms);

                                    // Saubere Dateinamen für Anhänge
                                    var fileName = CleanText(mimePart.FileName ?? "attachment.dat");
                                    var contentType = CleanText(mimePart.ContentType?.MimeType ?? "application/octet-stream");

                                    var emailAttachment = new EmailAttachment
                                    {
                                        ArchivedEmailId = archivedEmail.Id,
                                        FileName = fileName,
                                        ContentType = contentType,
                                        Content = ms.ToArray(),
                                        Size = ms.Length
                                    };

                                    _context.EmailAttachments.Add(emailAttachment);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error saving attachment: {FileName}", mimePart.FileName);
                                }
                            }
                        }

                        // Speichern der Anhänge in einem separaten Try-Block
                        try
                        {
                            await _context.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error saving attachments for email: {Subject}", subject);
                        }
                    }

                    _logger.LogInformation(
                        "Archived email: {Subject}, From: {From}, To: {To}, Account: {AccountName}",
                        archivedEmail.Subject, archivedEmail.From, archivedEmail.To, account.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving archived email to database: {Subject}, {Message}", subject, ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving email: Subject={Subject}, From={From}, Error={Message}",
                    message.Subject, message.From, ex.Message);
            }
        }

        // Verbesserte Methode zur Textbereinigung
        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Entferne Null-Bytes
            text = text.Replace("\0", "");

            // Ersetze ungültige Zeichen durch Fragezeichen oder entferne sie komplett
            var cleanedText = new StringBuilder();

            foreach (var c in text)
            {
                // Prüfe, ob das Zeichen ein gültiges UTF-8-Zeichen ist
                if (c < 32 && c != '\r' && c != '\n' && c != '\t')
                {
                    // Steuerzeichen außer CR, LF und Tab durch Leerzeichen ersetzen
                    cleanedText.Append(' ');
                }
                else
                {
                    cleanedText.Append(c);
                }
            }

            return cleanedText.ToString();
        }

        // Spezielle Methode für HTML-Bereinigung
        private string CleanHtmlForStorage(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Entferne Null-Bytes
            html = html.Replace("\0", "");

            // Für sehr große HTML-Inhalte (>1MB) speichern wir nur einen Hinweistext
            if (html.Length > 1_000_000)
            {
                return "<html><body><p>Diese E-Mail enthält sehr großen HTML-Inhalt, der gekürzt wurde.</p></body></html>";
            }

            // Wir könnten hier auch HTML bereinigen (scripts entfernen etc.)
            // aber das wichtigste ist die Entfernung von Null-Bytes

            return html;
        }

        public async Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsAsync(
    string searchTerm,
    DateTime? fromDate,
    DateTime? toDate,
    int? accountId,
    bool? isOutgoing,
    int skip,
    int take)
        {
            // Optimierte Grundabfrage, die nur sichere Filterbedingungen verwendet
            var query = _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .AsQueryable();

            // Filtere nach den indizierten Feldern zuerst
            if (accountId.HasValue)
                query = query.Where(e => e.MailAccountId == accountId.Value);

            if (fromDate.HasValue)
                query = query.Where(e => e.SentDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.SentDate <= toDate.Value.AddDays(1).AddSeconds(-1)); // Bis Ende des Tages

            if (isOutgoing.HasValue)
                query = query.Where(e => e.IsOutgoing == isOutgoing.Value);

            // Wenn ein Suchbegriff vorhanden ist, optimiere die Suche für PostgreSQL
            // mit speziellen Textsuchefunktionen
            if (!string.IsNullOrEmpty(searchTerm))
            {
                // Wichtig: Begrenze die maximale Anzahl der E-Mails, die durchsucht werden
                // PostgreSQL hat eine optimierte Textsuche, aber wir sollten die Menge begrenzen
                searchTerm = searchTerm.Replace("'", "''"); // SQL-Injektion verhindern

                // Verwende PostgreSQL's interne Funktionen für Textsuche mit ILIKE
                // Dies ist effizienter als EF Core's Contains für große Datenmengen
                query = query.Where(e =>
                    EF.Functions.ILike(e.Subject, $"%{searchTerm}%") ||
                    EF.Functions.ILike(e.From, $"%{searchTerm}%") ||
                    EF.Functions.ILike(e.To, $"%{searchTerm}%") ||
                    EF.Functions.ILike(e.Body, $"%{searchTerm}%")
                );
            }

            // Zähle die Gesamtmenge
            var totalCount = await query.CountAsync();

            // Hol die Ergebnisse sortiert und paginiert
            var emails = await query
                .OrderByDescending(e => e.SentDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return (emails, totalCount);
        }

        // In EmailService.cs, look for the ExportEmailsAsync method
        public async Task<byte[]> ExportEmailsAsync(ExportViewModel parameters)
        {
            var (emails, _) = await SearchEmailsAsync(
                parameters.SearchTerm,
                parameters.FromDate,
                parameters.ToDate,
                parameters.SelectedAccountId,
                parameters.IsOutgoing,
                0,
                10000); // Limit to 10,000 emails

            using var ms = new MemoryStream();

            switch (parameters.Format)
            {
                case ExportFormat.Csv:
                    using (var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true))
                    {
                        // CSV header
                        writer.WriteLine("Subject;From;To;Date;Account;Direction;Message Text");

                        // CSV data
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

                        writer.Flush(); // Make sure all data is written
                    }
                    break;

                case ExportFormat.Json:
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
                    await ms.FlushAsync(); // Make sure all data is written
                    break;

                case ExportFormat.Eml:
                    // Diese Implementierung ist für einzelne E-Mails
                    if (parameters.EmailId.HasValue)
                    {
                        var email = await _context.ArchivedEmails
                            .Include(e => e.Attachments)
                            .FirstOrDefaultAsync(e => e.Id == parameters.EmailId.Value);

                        if (email != null)
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

                            var body = !string.IsNullOrEmpty(email.HtmlBody)
                                ? new TextPart("html") { Text = email.HtmlBody }
                                : new TextPart("plain") { Text = email.Body };

                            if (email.Attachments.Any())
                            {
                                var multipart = new Multipart("mixed");
                                multipart.Add(body);

                                foreach (var attachment in email.Attachments)
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

                            message.WriteTo(ms);
                        }
                    }
                    break;
            }

            ms.Position = 0;
            return ms.ToArray();
        }

        public async Task<DashboardViewModel> GetDashboardStatisticsAsync()
        {
            var model = new DashboardViewModel();

            // Basic statistics
            model.TotalEmails = await _context.ArchivedEmails.CountAsync();
            model.TotalAccounts = await _context.MailAccounts.CountAsync();
            model.TotalAttachments = await _context.EmailAttachments.CountAsync();

            var totalSizeBytes = await _context.EmailAttachments.SumAsync(a => (long)a.Size);
            model.TotalStorageUsed = FormatFileSize(totalSizeBytes);

            // Statistics per account
            model.EmailsPerAccount = await _context.MailAccounts
                .Select(a => new AccountStatistics
                {
                    AccountName = a.Name,
                    EmailAddress = a.EmailAddress,
                    EmailCount = a.ArchivedEmails.Count,
                    LastSyncTime = a.LastSync
                })
                .ToListAsync();

            // Emails per month (last 12 months)
            var startDate = DateTime.UtcNow.AddMonths(-11).Date;
            var months = new List<EmailCountByPeriod>();

            for (int i = 0; i < 12; i++)
            {
                var currentMonth = startDate.AddMonths(i);
                var nextMonth = currentMonth.AddMonths(1);

                // FIXED APPROACH - Use EF Core's built-in query instead of raw SQL
                var count = await _context.ArchivedEmails
                    .Where(e => e.SentDate >= currentMonth && e.SentDate < nextMonth)
                    .CountAsync();

                months.Add(new EmailCountByPeriod
                {
                    Period = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(currentMonth.Month)} {currentMonth.Year}",
                    Count = count
                });
            }

            model.EmailsByMonth = months;

            // Top senders
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

            // Recent emails
            model.RecentEmails = await _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .OrderByDescending(e => e.ReceivedDate)
                .Take(10)
                .ToListAsync();

            return model;
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
                using var client = new ImapClient();
                client.Timeout = 60000; // 1 Minute
                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSSL);
                await client.AuthenticateAsync(account.Username, account.Password);
                await client.DisconnectAsync(true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection test failed for account {AccountName}: {Message}",
                    account.Name, ex.Message);
                return false;
            }
        }
    }
}