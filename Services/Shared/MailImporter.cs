using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers.Eml;
using MailArchiver.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Text;

namespace MailArchiver.Services.Shared
{
    public class MailImporter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MailImporter> _logger;
        private readonly EmlMailCleaner _mailCleaner;
        private readonly EmlAttachmentCollector _attachmentCollector;

        public MailImporter(IServiceProvider serviceProvider, ILogger<MailImporter> logger,
            EmlMailCleaner mailCleaner, EmlAttachmentCollector attachmentCollector)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _mailCleaner = mailCleaner;
            _attachmentCollector = attachmentCollector;
        }

        public async Task<ImportResult> ImportEmailToDatabase(MimeMessage message, MailAccount account, string jobId, string targetFolder)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

                var messageId = GenerateMessageId(message, jobId);

                var checkFrom = string.Join(",", message.From.Mailboxes.Select(m => m.Address));
                var checkTo = string.Join(",", message.To.Mailboxes.Select(m => m.Address));
                var checkSubject = message.Subject ?? "(No Subject)";

                var existing = await context.ArchivedEmails
                    .Where(e => e.MailAccountId == account.Id)
                    .Where(e =>
                        e.MessageId == messageId ||
                        (e.From == checkFrom && e.To == checkTo && e.Subject == checkSubject &&
                         Math.Abs((e.SentDate - message.Date.DateTime).TotalSeconds) < 2))
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    _logger.LogInformation(
                        "Job {JobId}: Skipping duplicate email. Subject: '{Subject}', From: '{From}', MessageId: '{MessageId}'",
                        jobId, checkSubject, checkFrom, messageId);
                    return ImportResult.CreateAlreadyExists();
                }

                var rawTextBody = message.TextBody;
                var rawHtmlBody = message.HtmlBody;
                var hasNullBytesInText = !string.IsNullOrEmpty(rawTextBody) && rawTextBody.Contains('\0');
                var hasNullBytesInHtml = !string.IsNullOrEmpty(rawHtmlBody) && rawHtmlBody.Contains('\0');
                var originalTextBody = !string.IsNullOrEmpty(rawTextBody) ? EmlMailCleaner.CleanText(rawTextBody) : null;
                var originalHtmlBody = !string.IsNullOrEmpty(rawHtmlBody) ? EmlMailCleaner.CleanText(rawHtmlBody) : null;

                var body = string.Empty;
                if (!string.IsNullOrEmpty(message.TextBody))
                {
                    var cleaned = EmlMailCleaner.CleanText(message.TextBody);
                    body = Encoding.UTF8.GetByteCount(cleaned) > 500_000
                        ? EmlMailCleaner.TruncateTextForStorage(cleaned, 500_000) : cleaned;
                }
                else if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    var cleaned = EmlMailCleaner.CleanText(message.HtmlBody);
                    if (Encoding.UTF8.GetByteCount(cleaned) > 500_000)
                    {
                        originalTextBody = message.HtmlBody;
                        body = EmlMailCleaner.TruncateTextForStorage(cleaned, 500_000);
                    }
                    else body = cleaned;
                }

                var htmlBody = string.Empty;
                if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    var cleaned = EmlMailCleaner.CleanText(message.HtmlBody);
                    htmlBody = Encoding.UTF8.GetByteCount(cleaned) > 1_000_000
                        ? _mailCleaner.CleanHtmlForStorage(cleaned) : cleaned;
                }

                var allAttachments = new List<MimePart>();
                _attachmentCollector.CollectAllAttachments(message.Body, allAttachments);

                var dateTimeHelper = scope.ServiceProvider.GetRequiredService<DateTimeHelper>();
                var convertedSentDate = dateTimeHelper.ConvertToDisplayTimeZone(message.Date);

                var subject = EmlMailCleaner.TruncateFieldForTsvector(
                    EmlMailCleaner.CleanText(message.Subject ?? "(No Subject)"), 50_000);
                var from = EmlMailCleaner.TruncateFieldForTsvector(
                    EmlMailCleaner.CleanText(string.Join(", ", message.From.Mailboxes.Select(m => m.Address))), 10_000);
                var to = EmlMailCleaner.TruncateFieldForTsvector(
                    EmlMailCleaner.CleanText(string.Join(", ", message.To.Mailboxes.Select(m => m.Address))), 50_000);
                var cc = EmlMailCleaner.TruncateFieldForTsvector(
                    EmlMailCleaner.CleanText(string.Join(", ", message.Cc?.Mailboxes.Select(m => m.Address) ?? Enumerable.Empty<string>())), 50_000);
                var bcc = EmlMailCleaner.TruncateFieldForTsvector(
                    EmlMailCleaner.CleanText(string.Join(", ", message.Bcc?.Mailboxes.Select(m => m.Address) ?? Enumerable.Empty<string>())), 50_000);

                var totalTsvectorSize = Encoding.UTF8.GetByteCount(subject + body + from + to + cc + bcc);
                if (totalTsvectorSize > 900_000)
                {
                    var otherFieldsSize = totalTsvectorSize - Encoding.UTF8.GetByteCount(body);
                    var maxBodySize = 900_000 - otherFieldsSize - 10_000;
                    if (maxBodySize > 0 && Encoding.UTF8.GetByteCount(body) > maxBodySize)
                        body = EmlMailCleaner.TruncateTextForStorage(body, maxBodySize);
                    else if (maxBodySize <= 0)
                        body = "[Body too large - saved as attachment]";
                }

                var rawHeaders = ExtractRawHeaders(message);
                if (!string.IsNullOrEmpty(rawHeaders))
                    rawHeaders = EmlMailCleaner.CleanText(rawHeaders);

                var archivedEmail = new ArchivedEmail
                {
                    MailAccountId = account.Id, MessageId = messageId,
                    Subject = subject, From = from, To = to, Cc = cc, Bcc = bcc,
                    SentDate = convertedSentDate, ReceivedDate = DateTime.UtcNow,
                    IsOutgoing = DetermineIfOutgoing(message, account, targetFolder),
                    HasAttachments = allAttachments.Any(), Body = body, HtmlBody = htmlBody,
                    BodyUntruncatedText = null, BodyUntruncatedHtml = null,
                    OriginalBodyText = (hasNullBytesInText || (!string.IsNullOrEmpty(originalTextBody) && originalTextBody != body))
                        ? Encoding.UTF8.GetBytes(hasNullBytesInText ? rawTextBody! : originalTextBody!) : null,
                    OriginalBodyHtml = (hasNullBytesInHtml || (!string.IsNullOrEmpty(originalHtmlBody) && originalHtmlBody != htmlBody))
                        ? Encoding.UTF8.GetBytes(hasNullBytesInHtml ? rawHtmlBody! : originalHtmlBody!) : null,
                    FolderName = targetFolder, RawHeaders = rawHeaders,
                    Attachments = new List<EmailAttachment>()
                };

                if (allAttachments.Any())
                {
                    foreach (var attachment in allAttachments)
                    {
                        try
                        {
                            using var ms = new MemoryStream();
                            await attachment.Content.DecodeToAsync(ms);
                            var fileName = GetAttachmentFileName(attachment);
                            archivedEmail.Attachments.Add(new EmailAttachment
                            {
                                FileName = EmlMailCleaner.CleanText(fileName),
                                ContentType = EmlMailCleaner.CleanText(attachment.ContentType?.MimeType ?? "application/octet-stream"),
                                ContentId = !string.IsNullOrEmpty(attachment.ContentId) ? EmlMailCleaner.CleanText(attachment.ContentId) : null,
                                Content = ms.ToArray(), Size = ms.Length
                            });
                        }
                        catch (Exception ex) { _logger.LogWarning(ex, "Job {JobId}: Failed to process attachment", jobId); }
                    }
                }

                context.ArchivedEmails.Add(archivedEmail);
                await context.SaveChangesAsync();
                return ImportResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId}: Failed to import email", jobId);
                return ImportResult.CreateFailed(ex.Message);
            }
        }

        private string GenerateMessageId(MimeMessage message, string jobId)
        {
            var messageId = message.MessageId;
            if (!string.IsNullOrEmpty(messageId)) return messageId;

            var uniqueString = $"{string.Join(",", message.From.Mailboxes.Select(m => m.Address))}|{string.Join(",", message.To.Mailboxes.Select(m => m.Address))}|{message.Subject ?? ""}|{message.Date.Ticks}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(uniqueString));
            var hashString = Convert.ToBase64String(hashBytes).Replace("+", "-").Replace("/", "_").Substring(0, 16);
            return $"generated-{hashString}@mail-archiver.local";
        }

        private static string GetAttachmentFileName(MimePart attachment)
        {
            if (!string.IsNullOrEmpty(attachment.FileName)) return attachment.FileName;
            var ext = EmlAttachmentCollector.GetFileExtensionFromContentType(attachment.ContentType?.MimeType);
            return !string.IsNullOrEmpty(attachment.ContentId)
                ? $"inline_{attachment.ContentId.Trim('<', '>')}{ext}"
                : $"attachment_{Guid.NewGuid().ToString("N").Substring(0, 8)}{ext}";
        }

        public static string? ExtractRawHeaders(MimeMessage message)
        {
            try
            {
                if (message.Headers == null || !message.Headers.Any()) return null;
                var sb = new StringBuilder();
                foreach (var h in message.Headers) sb.AppendLine($"{h.Field}: {h.Value}");
                var raw = sb.ToString();
                return raw.Length > 100_000 ? raw.Substring(0, 100_000) + "\r\n[...truncated...]" : raw;
            }
            catch { return null; }
        }

        public static bool DetermineIfOutgoing(MimeMessage message, MailAccount account, string folderName)
        {
            var fromAddr = message.From.Mailboxes.FirstOrDefault()?.Address;
            bool isOutgoingEmail = !string.IsNullOrEmpty(fromAddr) &&
                fromAddr.Equals(account.EmailAddress, StringComparison.OrdinalIgnoreCase);
            bool isOutgoingFolder = IsOutgoingFolderByName(folderName);
            bool isDrafts = IsDraftsFolder(folderName);
            return (isOutgoingEmail || isOutgoingFolder) && !isDrafts;
        }

        public static bool IsOutgoingFolderByName(string folderName)
        {
            var names = new[] { "sent", "gesendet", "enviado", "inviato", "verzonden", "envoye",
                "wyslane", "skickat", "trimise", "elkuldott", "odeslane", "poslano" };
            var lower = folderName?.ToLowerInvariant() ?? "";
            return names.Any(n => lower.Contains(n));
        }

        public static bool IsDraftsFolder(string folderName)
        {
            var names = new[] { "drafts", "entwurfe", "brouillons", "bozze", "draft" };
            var lower = folderName?.ToLowerInvariant() ?? "";
            return names.Any(n => lower.Contains(n));
        }
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public bool AlreadyExists { get; set; }
        public string? Error { get; set; }

        public static ImportResult CreateSuccess() => new ImportResult { Success = true };
        public static ImportResult CreateAlreadyExists() => new ImportResult { AlreadyExists = true };
        public static ImportResult CreateFailed(string error) => new ImportResult { Error = error };
    }
}
