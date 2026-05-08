using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Text;

namespace MailArchiver.Services.Providers.Graph
{
    /// <summary>
    /// Archives a single Microsoft Graph email into the database
    /// body cleaning/truncation, duplicate detection, attachment processing, and memory cleanup.
    /// </summary>
    public class GraphMailArchiver
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<GraphMailArchiver> _logger;
        private readonly DateTimeHelper _dateTimeHelper;

        public GraphMailArchiver(
            MailArchiverDbContext context,
            ILogger<GraphMailArchiver> logger,
            DateTimeHelper dateTimeHelper)
        {
            _context = context;
            _logger = logger;
            _dateTimeHelper = dateTimeHelper;
        }

        /// <summary>
        /// Archives a single Graph API message. Returns true if the email was newly archived.
        /// </summary>
        /// <param name="graphClient">Authenticated Graph client (used only for attachment loading).</param>
        /// <param name="account">The mail account.</param>
        /// <param name="message">The Graph message to archive.</param>
        /// <param name="isOutgoing">Whether the folder indicates outgoing mail.</param>
        /// <param name="folderName">The folder path for storage.</param>
        /// <returns>True if a new email was archived, false if it already existed.</returns>
        public async Task<bool> ArchiveGraphEmailAsync(
            GraphServiceClient graphClient,
            MailAccount account,
            Message message,
            bool isOutgoing,
            string folderName)
        {
            var messageId = message.InternetMessageId ?? message.Id;

            // Generate a deterministic MessageId when Graph doesn't provide one.
            if (string.IsNullOrEmpty(messageId))
            {
                messageId = GenerateMessageId(message);
                _logger.LogDebug("Generated MessageId for M365 email without MessageId: {MessageId}", messageId);
            }

            _logger.LogDebug("Processing message {MessageId} for account {AccountName}, Subject: {Subject}",
                messageId, account.Name, message.Subject ?? "No Subject");

            // Duplicate check
            var isDuplicate = await IsDuplicateAsync(account.Id, messageId, message, folderName);
            if (isDuplicate)
                return false;

            // Build and persist the archived email
            try
            {
                _logger.LogDebug("Archiving new email {MessageId} for account {AccountName}", messageId, account.Name);

                var archivedEmail = await BuildArchivedEmailAsync(graphClient, account, message, messageId, isOutgoing, folderName);

                _context.ArchivedEmails.Add(archivedEmail);
                await _context.SaveChangesAsync();

                var attachmentCount = archivedEmail.Attachments?.Count ?? 0;

                // Release large payloads to avoid LOH fragmentation.
                ReleaseLargePayloads(archivedEmail);
                _context.ChangeTracker.Clear();

                _logger.LogInformation("Archived Graph API email: {Subject}, From: {From}, To: {To}, Account: {AccountName}, Attachments: {AttachmentCount}",
                    archivedEmail.Subject, archivedEmail.From, archivedEmail.To, account.Name, attachmentCount);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving Graph API email: Subject={Subject}, From={From}, Error={Message}",
                    message.Subject, message.From?.EmailAddress?.Address, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Generates a deterministic MessageId using SHA-256 over From|To|Subject|Ticks.
        /// </summary>
        public static string GenerateMessageId(Message message)
        {
            var from = message.From?.EmailAddress?.Address ?? "";
            var to = string.Join(",", message.ToRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>());
            var subject = message.Subject ?? "";
            var dateTicks = message.SentDateTime?.Ticks ?? 0L;

            var uniqueString = $"{from}|{to}|{subject}|{dateTicks}";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(uniqueString));
                var hashString = Convert.ToBase64String(hashBytes).Replace("+", "-").Replace("/", "_").Substring(0, 16);
                return $"generated-{hashString}@mail-archiver.local";
            }
        }

        /// <summary>
        /// Checks whether an email with the given criteria already exists in the archive.
        /// Returns true if a duplicate was found (and optionally updates the folder name).
        /// </summary>
        private async Task<bool> IsDuplicateAsync(int accountId, string messageId, Message message, string folderName)
        {
            try
            {
                var checkFrom = message.From?.EmailAddress?.Address ?? "";
                var checkTo = string.Join(",", message.ToRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>());
                var checkSubject = message.Subject ?? "(No Subject)";
                var checkDate = message.SentDateTime?.DateTime ?? DateTime.UtcNow;

                var existingInfo = await _context.ArchivedEmails
                    .AsNoTracking()
                    .Where(e => e.MailAccountId == accountId)
                    .Where(e =>
                        e.MessageId == messageId ||
                        (e.From == checkFrom &&
                         e.To == checkTo &&
                         e.Subject == checkSubject &&
                         Math.Abs((e.SentDate - checkDate).TotalSeconds) < 2)
                    )
                    .Select(e => new { e.Id, e.FolderName, e.Subject })
                    .FirstOrDefaultAsync();

                if (existingInfo != null)
                {
                    var cleanFolderName = MailContentHelper.CleanText(folderName);
                    if (existingInfo.FolderName != cleanFolderName)
                    {
                        var existingEmail = await _context.ArchivedEmails.FindAsync(existingInfo.Id);
                        if (existingEmail != null)
                        {
                            var oldFolder = existingEmail.FolderName;
                            existingEmail.FolderName = cleanFolderName;
                            await _context.SaveChangesAsync();
                            _logger.LogDebug("Updated folder for existing email: {Subject} from '{OldFolder}' to '{NewFolder}'",
                                existingEmail.Subject, oldFolder, cleanFolderName);
                            _context.ChangeTracker.Clear();
                        }
                    }
                    _logger.LogDebug("Email already exists (duplicate) - MessageId: {MessageId}, Subject: {Subject}, From: {From}",
                        messageId, checkSubject, checkFrom);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if email {MessageId} exists for account {AccountName}: {Message}",
                    messageId, accountId, ex.Message);
                return true; // Treat as duplicate to be safe
            }

            return false;
        }

        /// <summary>
        /// Builds an ArchivedEmail entity from a Graph message, including attachments
        /// </summary>
        private async Task<ArchivedEmail> BuildArchivedEmailAsync(
            GraphServiceClient graphClient,
            MailAccount account,
            Message message,
            string messageId,
            bool isOutgoing,
            string folderName)
        {
            var convertedSentDate = message.SentDateTime.HasValue
                ? _dateTimeHelper.ConvertToDisplayTimeZone(message.SentDateTime.Value)
                : _dateTimeHelper.ConvertToDisplayTimeZone(DateTime.UtcNow);

            var subject = MailContentHelper.CleanText(message.Subject ?? "(No Subject)");
            var from = MailContentHelper.CleanText(message.From?.EmailAddress?.Address ?? string.Empty);
            var to = MailContentHelper.CleanText(string.Join(", ", message.ToRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>()));
            var cc = MailContentHelper.CleanText(string.Join(", ", message.CcRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>()));
            var bcc = MailContentHelper.CleanText(string.Join(", ", message.BccRecipients?.Select(r => r.EmailAddress?.Address) ?? new List<string>()));

            var (body, htmlBody) = ExtractBody(message);
            var originalTextBody = message.Body?.ContentType == BodyType.Text ? message.Body?.Content : (message.BodyPreview ?? "");
            var originalHtmlBody = message.Body?.ContentType == BodyType.Html ? message.Body?.Content : null;

            var hasNullBytesInText = !string.IsNullOrEmpty(originalTextBody) && originalTextBody.Contains('\0');
            var hasNullBytesInHtml = !string.IsNullOrEmpty(originalHtmlBody) && originalHtmlBody.Contains('\0');

            var cleanMessageId = MailContentHelper.CleanText(messageId);
            var cleanFolderName = MailContentHelper.CleanText(folderName);

            // Truncate individual fields for tsvector safety
            subject = MailContentHelper.TruncateFieldForTsvector(subject, 50_000);
            from = MailContentHelper.TruncateFieldForTsvector(from, 10_000);
            to = MailContentHelper.TruncateFieldForTsvector(to, 50_000);
            cc = MailContentHelper.TruncateFieldForTsvector(cc, 50_000);
            bcc = MailContentHelper.TruncateFieldForTsvector(bcc, 50_000);

            // Final safety check: ensure total tsvector size doesn't exceed limit
            var totalTsvectorSize = Encoding.UTF8.GetByteCount(subject) +
                                   Encoding.UTF8.GetByteCount(body) +
                                   Encoding.UTF8.GetByteCount(from) +
                                   Encoding.UTF8.GetByteCount(to) +
                                   Encoding.UTF8.GetByteCount(cc) +
                                   Encoding.UTF8.GetByteCount(bcc);

            const int maxTsvectorSize = 900_000;
            if (totalTsvectorSize > maxTsvectorSize)
            {
                _logger.LogWarning("Email fields exceed tsvector limit ({TotalSize} > {MaxSize}), truncating body further",
                    totalTsvectorSize, maxTsvectorSize);

                var otherFieldsSize = totalTsvectorSize - Encoding.UTF8.GetByteCount(body);
                var maxBodySize = maxTsvectorSize - otherFieldsSize - 10_000;

                if (maxBodySize > 0 && Encoding.UTF8.GetByteCount(body) > maxBodySize)
                {
                    body = MailContentHelper.TruncateTextForStorage(body, maxBodySize);
                }
                else if (maxBodySize <= 0)
                {
                    _logger.LogError("Other email fields alone exceed tsvector limit, body will be saved as attachment only");
                    body = "[Body too large - saved as attachment]";
                }
            }

            // Determine if the email is outgoing
            bool isOutgoingEmail = !string.IsNullOrEmpty(from) &&
                                  !string.IsNullOrEmpty(account.EmailAddress) &&
                                  from.Equals(account.EmailAddress, StringComparison.OrdinalIgnoreCase);

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
                IsOutgoing = isOutgoingEmail || isOutgoing,
                HasAttachments = false, // Set after attachment loading
                Body = body,
                HtmlBody = htmlBody,
                BodyUntruncatedText = null,
                BodyUntruncatedHtml = null,
                OriginalBodyText = (hasNullBytesInText || (!string.IsNullOrEmpty(originalTextBody) && originalTextBody != body))
                    ? Encoding.UTF8.GetBytes(originalTextBody!)
                    : null,
                OriginalBodyHtml = (hasNullBytesInHtml || (!string.IsNullOrEmpty(originalHtmlBody) && originalHtmlBody != htmlBody))
                    ? Encoding.UTF8.GetBytes(originalHtmlBody!)
                    : null,
                FolderName = cleanFolderName,
                Attachments = new List<EmailAttachment>()
            };

            // Load attachments before hash calculation
            await LoadAttachmentsAsync(graphClient, account.EmailAddress, message.Id, archivedEmail);

            archivedEmail.HasAttachments = archivedEmail.Attachments != null && archivedEmail.Attachments.Count > 0;

            return archivedEmail;
        }

        /// <summary>
        /// Extracts the text and HTML body from a Graph message, applying cleaning and truncation.
        /// </summary>
        private static (string body, string htmlBody) ExtractBody(Message message)
        {
            var body = string.Empty;
            var htmlBody = string.Empty;

            if (message.Body?.Content != null)
            {
                if (message.Body.ContentType == BodyType.Html)
                {
                    var cleanedHtmlBody = MailContentHelper.CleanText(message.Body.Content);

                    if (Encoding.UTF8.GetByteCount(cleanedHtmlBody) > 1_000_000)
                    {
                        htmlBody = MailContentHelper.CleanHtmlForStorage(cleanedHtmlBody);
                    }
                    else
                    {
                        htmlBody = cleanedHtmlBody;
                    }

                    var bodyPreview = MailContentHelper.CleanText(message.BodyPreview ?? "");
                    if (Encoding.UTF8.GetByteCount(bodyPreview) > 500_000)
                    {
                        body = MailContentHelper.TruncateTextForStorage(bodyPreview, 500_000);
                    }
                    else
                    {
                        body = bodyPreview;
                    }
                }
                else
                {
                    var cleanedTextBody = MailContentHelper.CleanText(message.Body.Content);
                    if (Encoding.UTF8.GetByteCount(cleanedTextBody) > 500_000)
                    {
                        body = MailContentHelper.TruncateTextForStorage(cleanedTextBody, 500_000);
                    }
                    else
                    {
                        body = cleanedTextBody;
                    }
                }
            }
            else if (!string.IsNullOrEmpty(message.BodyPreview))
            {
                var bodyPreview = MailContentHelper.CleanText(message.BodyPreview);
                if (Encoding.UTF8.GetByteCount(bodyPreview) > 500_000)
                {
                    body = MailContentHelper.TruncateTextForStorage(bodyPreview, 500_000);
                }
                else
                {
                    body = bodyPreview;
                }
            }

            return (body, htmlBody);
        }

        /// <summary>
        /// Loads all file attachments for a Graph message and adds them to the archived email.
        /// </summary>
        private async Task LoadAttachmentsAsync(
            GraphServiceClient graphClient,
            string userPrincipalName,
            string messageId,
            ArchivedEmail archivedEmail)
        {
            try
            {
                _logger.LogDebug("Loading attachments for Graph API message {MessageId} before hash calculation", messageId);

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
                                var cleanFileName = MailContentHelper.CleanText(fileAttachment.Name ?? "attachment");
                                var contentType = MailContentHelper.CleanText(fileAttachment.ContentType ?? "application/octet-stream");

                                var contentId = !string.IsNullOrEmpty(fileAttachment.ContentId)
                                    ? fileAttachment.ContentId.Trim().Trim('<', '>')
                                    : null;

                                bool isInlineContent = MailContentHelper.IsGraphInlineContent(
                                    fileAttachment.ContentId, fileAttachment.ContentType, fileAttachment.Name);

                                if (isInlineContent && (string.IsNullOrEmpty(cleanFileName) || cleanFileName == "attachment"))
                                {
                                    var extension = MailContentHelper.GetExtensionFromContentType(contentType);
                                    if (!string.IsNullOrEmpty(contentId))
                                    {
                                        var cidPart = contentId.Trim('<', '>').Split('@')[0];
                                        cleanFileName = $"inline_{cidPart}{extension}";
                                    }
                                    else
                                    {
                                        cleanFileName = $"inline_image_{Guid.NewGuid().ToString("N")[..8]}{extension}";
                                    }
                                }

                                var emailAttachment = new EmailAttachment
                                {
                                    FileName = cleanFileName,
                                    ContentType = contentType,
                                    Content = fileAttachment.ContentBytes,
                                    Size = fileAttachment.ContentBytes.Length,
                                    ContentId = contentId
                                };

                                archivedEmail.Attachments!.Add(emailAttachment);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process Graph API attachment: {AttachmentName}", attachment.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load attachments for Graph API message {MessageId} before hash calculation", messageId);
            }
        }

        /// <summary>
        /// Releases large payload references to mitigate Large Object Heap fragmentation.
        /// </summary>
        private static void ReleaseLargePayloads(ArchivedEmail archivedEmail)
        {
            if (archivedEmail.Attachments != null)
            {
                foreach (var att in archivedEmail.Attachments)
                {
                    att.Content = null!;
                }
                archivedEmail.Attachments = null!;
            }
            archivedEmail.Body = null!;
            archivedEmail.HtmlBody = null;
            archivedEmail.OriginalBodyText = null;
            archivedEmail.OriginalBodyHtml = null;
            archivedEmail.BodyUntruncatedText = null;
            archivedEmail.BodyUntruncatedHtml = null;
        }
    }
}