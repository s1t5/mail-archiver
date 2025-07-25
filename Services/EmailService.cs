using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MailArchiver.Services
{
    public class EmailService : IEmailService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<EmailService> _logger;
        private readonly ISyncJobService _syncJobService;

        public EmailService(
            MailArchiverDbContext context,
            ILogger<EmailService> logger,
            ISyncJobService syncJobService)
        {
            _context = context;
            _logger = logger;
            _syncJobService = syncJobService;
        }

        // MODIFIZIERTE SyncMailAccountAsync Methode
        public async Task SyncMailAccountAsync(MailAccount account, string? jobId = null)
        {
            _logger.LogInformation("Starting sync for account: {AccountName}", account.Name);

            using var client = new ImapClient();
            client.Timeout = 900000; // 15 Minuten

            var processedFolders = 0;
            var processedEmails = 0;
            var newEmails = 0;
            var failedEmails = 0;

            try
            {
                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSSL);
                await client.AuthenticateAsync(account.Username, account.Password);
                _logger.LogInformation("Connected to IMAP server for {AccountName}", account.Name);

                // Prepare a list to store all folders
                var allFolders = new List<IMailFolder>();

                // Get personal namespace folders
                foreach (var folder in client.GetFolders(client.PersonalNamespaces[0]))
                {
                    allFolders.Add(folder);
                    await AddSubfoldersRecursively(folder, allFolders);
                }

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
                    try
                    {
                        if (jobId != null)
                        {
                            _syncJobService.UpdateJobProgress(jobId, job =>
                            {
                                job.CurrentFolder = folder.FullName;
                                job.ProcessedFolders = processedFolders;
                            });
                        }

                        var folderResult = await SyncFolderAsync(folder, account);
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

                await client.DisconnectAsync(true);
                _logger.LogInformation("Sync completed for account: {AccountName}. New: {New}, Failed: {Failed}",
                    account.Name, newEmails, failedEmails);

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

                _logger.LogInformation("Starting full resync for account {AccountName}", account.Name);

                // Reset LastSync to Unix Epoch to force full resync
                account.LastSync = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                await _context.SaveChangesAsync();

                // Start sync job
                var jobId = _syncJobService.StartSync(account.Id, account.Name, account.LastSync);

                // Perform the sync
                await SyncMailAccountAsync(account, jobId);

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
        private async Task<SyncFolderResult> SyncFolderAsync(IMailFolder folder, MailAccount account)
        {
            var result = new SyncFolderResult();

            _logger.LogInformation("Syncing folder: {FolderName} for account: {AccountName}",
                folder.FullName, account.Name);

            try
            {
                if (folder.Attributes.HasFlag(FolderAttributes.NonExistent) ||
                    folder.Attributes.HasFlag(FolderAttributes.NoSelect))
                {
                    _logger.LogInformation("Skipping folder {FolderName} (non-existent or non-selectable)",
                        folder.FullName);
                    return result;
                }

                await folder.OpenAsync(FolderAccess.ReadOnly);

                bool isOutgoing = IsOutgoingFolder(folder);
                var lastSync = account.LastSync;
                var query = SearchQuery.DeliveredAfter(lastSync);

                try
                {
                    var uids = await folder.SearchAsync(query);
                    _logger.LogInformation("Found {Count} new messages in folder {FolderName} for account: {AccountName}",
                        uids.Count, folder.FullName, account.Name);

                    result.ProcessedEmails = uids.Count;

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
                                var isNew = await ArchiveEmailAsync(account, message, isOutgoing, folder.FullName);
                                if (isNew)
                                {
                                    result.NewEmails++;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error archiving message {MessageNumber} from folder {FolderName}: {Message}",
                                    uid, folder.FullName, ex.Message);
                                result.FailedEmails++;
                            }
                        }

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
                return false; // E-Mail existiert bereits

            try
            {
                DateTime sentDate = DateTime.SpecifyKind(message.Date.DateTime, DateTimeKind.Utc);
                var subject = CleanText(message.Subject ?? "(No Subject)");
                var from = CleanText(message.From.ToString());
                var to = CleanText(message.To.ToString());
                var cc = CleanText(message.Cc?.ToString() ?? string.Empty);
                var bcc = CleanText(message.Bcc?.ToString() ?? string.Empty);
                var body = CleanText(message.TextBody ?? string.Empty);
                var htmlBody = string.Empty;

                if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    htmlBody = CleanHtmlForStorage(message.HtmlBody);
                }

                var cleanMessageId = CleanText(messageId);
                var cleanFolderName = CleanText(folderName ?? string.Empty);

                // Sammle ALLE Anhänge einschließlich inline Images
                var allAttachments = new List<MimePart>();
                CollectAllAttachments(message.Body, allAttachments);

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
                    HasAttachments = allAttachments.Any(),
                    Body = body,
                    HtmlBody = htmlBody,
                    FolderName = cleanFolderName
                };

                try
                {
                    _context.ArchivedEmails.Add(archivedEmail);
                    await _context.SaveChangesAsync();

                    // Speichere ALLE Anhänge als normale Attachments
                    if (allAttachments.Any())
                    {
                        await SaveAllAttachments(allAttachments, archivedEmail.Id);
                    }

                    _logger.LogInformation(
                        "Archived email: {Subject}, From: {From}, To: {To}, Account: {AccountName}, Total Attachments: {AttachmentCount}",
                        archivedEmail.Subject, archivedEmail.From, archivedEmail.To, account.Name, allAttachments.Count);

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
            // Prüfe Content-Disposition
            if (mimePart.ContentDisposition?.Disposition?.Equals("inline", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            // Prüfe auf Images mit Content-ID (typisch für inline Images)
            if (!string.IsNullOrEmpty(mimePart.ContentId) &&
                mimePart.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            // Prüfe auf andere inline Content-Typen
            if (!string.IsNullOrEmpty(mimePart.ContentId) &&
                (mimePart.ContentType?.MediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true ||
                 mimePart.ContentType?.MediaType?.StartsWith("application/", StringComparison.OrdinalIgnoreCase) == true))
            {
                return true;
            }

            return false;
        }

        // Methode zum Speichern aller Anhänge als normale Attachments
        private async Task SaveAllAttachments(List<MimePart> attachments, int archivedEmailId)
        {
            foreach (var attachment in attachments)
            {
                try
                {
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

                    var emailAttachment = new EmailAttachment
                    {
                        ArchivedEmailId = archivedEmailId,
                        FileName = cleanFileName,
                        ContentType = contentType,
                        Content = ms.ToArray(),
                        Size = ms.Length
                    };

                    _context.EmailAttachments.Add(emailAttachment);

                    _logger.LogDebug("Prepared attachment for saving: FileName={FileName}, Size={Size}, ContentType={ContentType}",
                        cleanFileName, ms.Length, contentType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process attachment: FileName={FileName}, ContentType={ContentType}, ContentId={ContentId}",
                        attachment.FileName, attachment.ContentType?.MimeType, attachment.ContentId);
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully saved {Count} attachments for email {EmailId}",
                    attachments.Count, archivedEmailId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save attachments batch for email {EmailId}", archivedEmailId);
            }
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
                "sent", "sent items", "sent mail", "gesendet", "gesendete objekte",
                "gesendete", "enviado", "envoyé", "inviato", "verzonden"
            };

            string folderNameLower = folder.Name.ToLowerInvariant();
            return sentFolderNames.Any(name => folderNameLower.Contains(name)) ||
                   folder.Attributes.HasFlag(FolderAttributes.Sent);
        }

        private async Task AddSubfoldersRecursively(IMailFolder folder, List<IMailFolder> allFolders)
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
                    await AddSubfoldersRecursively(subfolder, allFolders);
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

            text = text.Replace("\0", "");
            var cleanedText = new StringBuilder();
            foreach (var c in text)
            {
                if (c < 32 && c != '\r' && c != '\n' && c != '\t')
                {
                    cleanedText.Append(' ');
                }
                else
                {
                    cleanedText.Append(c);
                }
            }

            return cleanedText.ToString();
        }

        private string CleanHtmlForStorage(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            html = html.Replace("\0", "");
            if (html.Length > 1_000_000)
            {
                return "<html><body><p>Diese E-Mail enthält sehr großen HTML-Inhalt, der gekürzt wurde.</p></body></html>";
            }

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
            var query = _context.ArchivedEmails
                .Include(e => e.MailAccount)
                .AsQueryable();

            if (accountId.HasValue)
                query = query.Where(e => e.MailAccountId == accountId.Value);

            if (fromDate.HasValue)
                query = query.Where(e => e.SentDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.SentDate <= toDate.Value.AddDays(1).AddSeconds(-1));

            if (isOutgoing.HasValue)
                query = query.Where(e => e.IsOutgoing == isOutgoing.Value);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.Replace("'", "''");
                query = query.Where(e =>
                    EF.Functions.ILike(e.Subject, $"%{searchTerm}%") ||
                    EF.Functions.ILike(e.From, $"%{searchTerm}%") ||
                    EF.Functions.ILike(e.To, $"%{searchTerm}%") ||
                    EF.Functions.ILike(e.Body, $"%{searchTerm}%")
                );
            }

            var totalCount = await query.CountAsync();

            var emails = await query
                .OrderByDescending(e => e.SentDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return (emails, totalCount);
        }

        public async Task<byte[]> ExportEmailsAsync(ExportViewModel parameters)
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
                    parameters.IsOutgoing,
                    0,
                    10000);

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

            var totalSizeBytes = await _context.EmailAttachments.SumAsync(a => (long)a.Size);
            model.TotalStorageUsed = FormatFileSize(totalSizeBytes);

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

            var startDate = DateTime.UtcNow.AddMonths(-11).Date;
            var months = new List<EmailCountByPeriod>();
            for (int i = 0; i < 12; i++)
            {
                var currentMonth = startDate.AddMonths(i);
                var nextMonth = currentMonth.AddMonths(1);

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
                _logger.LogInformation("Testing connection to IMAP server {Server}:{Port} for account {Name} ({Email})",
                    account.ImapServer, account.ImapPort, account.Name, account.EmailAddress);

                using var client = new ImapClient();
                client.Timeout = 30000;
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                _logger.LogDebug("Connecting to {Server}:{Port}, SSL: {UseSSL}",
                    account.ImapServer, account.ImapPort, account.UseSSL);

                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSSL);
                _logger.LogDebug("Connection established, authenticating as {Username}", account.Username);

                await client.AuthenticateAsync(account.Username, account.Password);
                _logger.LogInformation("Authentication successful for {Email}", account.EmailAddress);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);
                _logger.LogInformation("INBOX opened successfully with {Count} messages", inbox.Count);

                await client.DisconnectAsync(true);
                _logger.LogInformation("Connection test passed for account {Name}", account.Name);
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

                    var bodyBuilder = new BodyBuilder();
                    if (!string.IsNullOrEmpty(email.HtmlBody))
                    {
                        bodyBuilder.HtmlBody = email.HtmlBody;
                    }
                    if (!string.IsNullOrEmpty(email.Body))
                    {
                        bodyBuilder.TextBody = email.Body;
                    }

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
                    using var client = new ImapClient();
                    client.Timeout = 180000;
                    _logger.LogInformation("Connecting to IMAP server {Server}:{Port} for account {AccountName}",
                        targetAccount.ImapServer, targetAccount.ImapPort, targetAccount.Name);

                    await client.ConnectAsync(targetAccount.ImapServer, targetAccount.ImapPort, targetAccount.UseSSL);
                    _logger.LogInformation("Connected to IMAP server, authenticating as {Username}", targetAccount.Username);

                    await client.AuthenticateAsync(targetAccount.Username, targetAccount.Password);
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
                client.Timeout = 60000;
                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSSL);
                await client.AuthenticateAsync(account.Username, account.Password);

                var allFolders = new List<string>();

                foreach (var folder in client.GetFolders(client.PersonalNamespaces[0]))
                {
                    allFolders.Add(folder.FullName);
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

        private async Task AddSubfolderNamesRecursively(IMailFolder folder, List<string> folderNames)
        {
            try
            {
                var subfolders = folder.GetSubfolders(false);
                foreach (var subfolder in subfolders)
                {
                    folderNames.Add(subfolder.FullName);
                    await AddSubfolderNamesRecursively(subfolder, folderNames);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving subfolders for {FolderName}: {Message}",
                    folder.FullName, ex.Message);
            }
        }
    }
}