using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Globalization;
using System.Net.Sockets;
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

        public EmailService(
            MailArchiverDbContext context,
            ILogger<EmailService> logger,
            ISyncJobService syncJobService,
            IOptions<BatchOperationOptions> batchOptions)
        {
            _context = context;
            _logger = logger;
            _syncJobService = syncJobService;
            _batchOptions = batchOptions.Value;
        }

        // SyncMailAccountAsync Methode
        public async Task SyncMailAccountAsync(MailAccount account, string? jobId = null)
        {
            _logger.LogInformation("Starting sync for account: {AccountName}", account.Name);

            using var client = new ImapClient();
            client.Timeout = 180000; // 3 Minuten - Reduced timeout to prevent server disconnections

            var processedFolders = 0;
            var processedEmails = 0;
            var newEmails = 0;
            var failedEmails = 0;
            var deletedEmails = 0; // Counter for deleted emails

            try
            {
                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSSL);
                await client.AuthenticateAsync(account.Username, account.Password);
                _logger.LogInformation("Connected to IMAP server for {AccountName}", account.Name);

                // Prepare a list to store all folders
                var allFolders = new List<IMailFolder>();

                // Get all folders by starting from the root and getting all subfolders
                var rootFolder = client.GetFolder(client.PersonalNamespaces[0]);
                await AddSubfoldersRecursively(rootFolder, allFolders);

                // Also add the root folder itself if it's selectable
                if (!rootFolder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                    !rootFolder.Attributes.HasFlag(FolderAttributes.NoSelect))
                {
                    allFolders.Add(rootFolder);
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

                // Delete old emails if configured
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
                    await client.AuthenticateAsync(account.Username, account.Password);
                }

                // Ensure folder is open
                if (!folder.IsOpen)
                {
                    await folder.OpenAsync(FolderAccess.ReadOnly);
                }

                bool isOutgoing = IsOutgoingFolder(folder);
                var lastSync = account.LastSync;
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
                        await client.AuthenticateAsync(account.Username, account.Password);
                    }

                    if (!folder.IsOpen)
                    {
                        await folder.OpenAsync(FolderAccess.ReadOnly);
                    }

                    var uids = await folder.SearchAsync(query);
                    _logger.LogInformation("Found {Count} new messages in folder {FolderName} for account: {AccountName}",
                        uids.Count, folder.FullName, account.Name);

                    result.ProcessedEmails = uids.Count;

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
                        _logger.LogInformation("Processing batch of {Count} messages (starting at {Start}) in folder {FolderName}",
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

                            MimeMessage message = null;
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
                                    await client.AuthenticateAsync(account.Username, account.Password);
                                }
                                else if (folder.IsOpen == false)
                                {
                                    _logger.LogWarning("Folder is not open, attempting to re-open...");
                                    await folder.OpenAsync(FolderAccess.ReadOnly);
                                }

                                message = await folder.GetMessageAsync(uid);
                                var isNew = await ArchiveEmailAsync(account, message, isOutgoing, folder.FullName);
                                if (isNew)
                                {
                                    result.NewEmails++;
                                }
                            }
                            catch (Exception ex)
                            {
                                var subject = message?.Subject ?? "Unknown";
                                var date = message?.Date.DateTime.ToString() ?? "Unknown";
                                _logger.LogError(ex, "Error archiving message {MessageNumber} from folder {FolderName}. Subject: {Subject}, Date: {Date}, Message: {Message}",
                                    uid, folder.FullName, subject, date, ex.Message);
                                result.FailedEmails++;
                            }
                        }

                        if (i + _batchOptions.BatchSize < uids.Count)
                        {
                            // Use the configurable pause between batches
                            if (_batchOptions.PauseBetweenBatchesMs > 0)
                            {
                                await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                            }
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

            var emailExists = await _context.ArchivedEmails
                .AnyAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);

            if (emailExists)
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
                var isHtmlTruncated = false;

                if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    // Check if HTML body will be truncated
                    isHtmlTruncated = message.HtmlBody.Length > 1_000_000;
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
                    HasAttachments = allAttachments.Any() || isHtmlTruncated, // Set to true if there are attachments or HTML was truncated
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

                    // If HTML was truncated, save the original HTML as an attachment
                    if (isHtmlTruncated)
                    {
                        await SaveTruncatedHtmlAsAttachment(message.HtmlBody, archivedEmail.Id);
                    }

                    _logger.LogInformation(
                        "Archived email: {Subject}, From: {From}, To: {To}, Account: {AccountName}, Total Attachments: {AttachmentCount}",
                        archivedEmail.Subject, archivedEmail.From, archivedEmail.To, account.Name, allAttachments.Count + (isHtmlTruncated ? 1 : 0));

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

        // Methode zum Speichern des ursprünglichen HTML-Codes als Anhang, wenn er gekürzt wurde
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
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null)
        {
            var startTime = DateTime.UtcNow;
            
            // Validate pagination parameters to prevent excessive data loading
            if (take > 1000) take = 1000; // Limit maximum page size
            if (skip < 0) skip = 0;

            try
            {
                // Use optimized raw SQL query for better performance
                return await SearchEmailsOptimizedAsync(searchTerm, fromDate, toDate, accountId, isOutgoing, skip, take, allowedAccountIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Optimized search failed, falling back to Entity Framework search");
                
                // Fallback to original Entity Framework approach
                return await SearchEmailsEFAsync(searchTerm, fromDate, toDate, accountId, isOutgoing, skip, take, allowedAccountIds);
            }
        }

        private async Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsOptimizedAsync(
            string searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null)
        {
            var startTime = DateTime.UtcNow;
            
            // Build the raw SQL query that directly uses the GIN index
            var whereConditions = new List<string>();
            var parameters = new List<Npgsql.NpgsqlParameter>();
            var paramCounter = 0;

            // Full-text search condition (this will use the GIN index)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var sanitizedSearchTerm = SanitizeSearchTermForTsQuery(searchTerm);
                if (!string.IsNullOrEmpty(sanitizedSearchTerm))
                {
                    whereConditions.Add($@"
                        to_tsvector('simple', 
                            COALESCE(""Subject"", '') || ' ' || 
                            COALESCE(""Body"", '') || ' ' || 
                            COALESCE(""From"", '') || ' ' || 
                            COALESCE(""To"", '') || ' ' || 
                            COALESCE(""Cc"", '') || ' ' || 
                            COALESCE(""Bcc"", '')) 
                        @@ to_tsquery('simple', @param{paramCounter})");
                    parameters.Add(new Npgsql.NpgsqlParameter($"@param{paramCounter}", sanitizedSearchTerm));
                    paramCounter++;
                    _logger.LogInformation("Using optimized full-text search for term: {SearchTerm}", searchTerm);
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

            // Data query (optimized)
            var dataSql = $@"
                SELECT e.""Id"", e.""MailAccountId"", e.""MessageId"", e.""Subject"", e.""Body"", e.""HtmlBody"",
                       e.""From"", e.""To"", e.""Cc"", e.""Bcc"", e.""SentDate"", e.""ReceivedDate"",
                       e.""IsOutgoing"", e.""HasAttachments"", e.""FolderName"",
                       ma.""Id"" as ""AccountId"", ma.""Name"" as ""AccountName"", ma.""EmailAddress"" as ""AccountEmail""
                FROM mail_archiver.""ArchivedEmails"" e
                INNER JOIN mail_archiver.""MailAccounts"" ma ON e.""MailAccountId"" = ma.""Id""
                {whereClause}
                ORDER BY e.""SentDate"" DESC
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

        private string SanitizeSearchTermForTsQuery(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return null;

            _logger.LogDebug("Sanitizing search term for tsquery: '{SearchTerm}'", searchTerm);

            // Remove special PostgreSQL tsquery operators and characters that could break the query
            var sanitized = System.Text.RegularExpressions.Regex.Replace(searchTerm, @"[&|!():\*]", " ", System.Text.RegularExpressions.RegexOptions.None);
            
            // Remove extra whitespace
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\s+", " ", System.Text.RegularExpressions.RegexOptions.None).Trim();
            
            if (string.IsNullOrEmpty(sanitized))
                return null;

            // Split into terms and join with & (AND operator)
            var terms = sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!terms.Any())
                return null;

            // Escape single quotes and join with AND
            var escapedTerms = terms.Select(t => t.Replace("'", "''"));
            var result = string.Join(" & ", escapedTerms);
            
            _logger.LogDebug("Sanitized search term result: '{Result}'", result);
            return result;
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

            IQueryable<ArchivedEmail> searchQuery = baseQuery;
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var escapedSearchTerm = searchTerm.Replace("'", "''");
                searchQuery = baseQuery.Where(e =>
                    EF.Functions.ILike(e.Subject, $"%{escapedSearchTerm}%") ||
                    EF.Functions.ILike(e.From, $"%{escapedSearchTerm}%") ||
                    EF.Functions.ILike(e.To, $"%{escapedSearchTerm}%") ||
                    EF.Functions.ILike(e.Body, $"%{escapedSearchTerm}%")
                );
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

                // Get all folders by starting from the root and getting all subfolders
                var rootFolder = client.GetFolder(client.PersonalNamespaces[0]);
                await AddSubfolderNamesRecursively(rootFolder, allFolders);

                // Also add the root folder itself if it's selectable
                if (!rootFolder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                    !rootFolder.Attributes.HasFlag(FolderAttributes.NoSelect))
                {
                    allFolders.Add(rootFolder.FullName);
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
                // Prepare a list to store all folders
                var allFolders = new List<IMailFolder>();

                // Get all folders by starting from the root and getting all subfolders
                var rootFolder = client.GetFolder(client.PersonalNamespaces[0]);
                await AddSubfoldersRecursively(rootFolder, allFolders);

                // Also add the root folder itself if it's selectable
                if (!rootFolder.Attributes.HasFlag(FolderAttributes.NonExistent) &&
                    !rootFolder.Attributes.HasFlag(FolderAttributes.NoSelect))
                {
                    allFolders.Add(rootFolder);
                }

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
                            await client.AuthenticateAsync(account.Username, account.Password);
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
                await client.ConnectAsync(account.ImapServer, account.ImapPort, account.UseSSL);
                await client.AuthenticateAsync(account.Username, account.Password);
                _logger.LogInformation("Successfully reconnected to IMAP server for account {AccountName}", account.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect to IMAP server for account {AccountName}", account.Name);
                throw new InvalidOperationException("Failed to reconnect to IMAP server", ex);
            }
        }
    }
}
