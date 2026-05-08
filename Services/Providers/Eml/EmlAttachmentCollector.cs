using MailArchiver.Data;
using MailArchiver.Models;
using MimeKit;
using System.Text.RegularExpressions;

namespace MailArchiver.Services.Providers.Eml
{
    /// <summary>
    /// Collects all attachments and inline content from MIME entities in EML files.
    /// Handles recursive MIME traversal, inline content detection, and attachment persistence.
    /// </summary>
    public class EmlAttachmentCollector
    {
        private readonly ILogger<EmlAttachmentCollector> _logger;

        public EmlAttachmentCollector(ILogger<EmlAttachmentCollector> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Recursively collects all MimePart attachments and inline content from a MIME entity tree.
        /// </summary>
        public void CollectAllAttachments(MimeEntity entity, List<MimePart> attachments)
        {
            _logger.LogDebug("CollectAllAttachments: Processing entity type: {EntityType}", entity.GetType().Name);

            if (entity is MimePart mimePart)
            {
                _logger.LogDebug("Processing MimePart: ContentType={ContentType}, FileName={FileName}, ContentId={ContentId}, IsAttachment={IsAttachment}, ContentDisposition={ContentDisposition}",
                    mimePart.ContentType?.MimeType, mimePart.FileName, mimePart.ContentId, mimePart.IsAttachment, mimePart.ContentDisposition?.Disposition);

                if (mimePart.IsAttachment)
                {
                    attachments.Add(mimePart);
                    _logger.LogDebug("Found attachment: FileName={FileName}, ContentType={ContentType}",
                        mimePart.FileName, mimePart.ContentType?.MimeType);
                }
                else if (IsInlineContent(mimePart))
                {
                    attachments.Add(mimePart);
                    _logger.LogDebug("Found inline content: ContentId={ContentId}, ContentType={ContentType}, FileName={FileName}",
                        mimePart.ContentId, mimePart.ContentType?.MimeType, mimePart.FileName);
                }
                else
                {
                    _logger.LogDebug("Skipping MimePart: Not attachment and not inline content");
                }
            }
            else if (entity is Multipart multipart)
            {
                _logger.LogDebug("Processing Multipart with {Count} children", multipart.Count);
                foreach (var child in multipart)
                {
                    CollectAllAttachments(child, attachments);
                }
            }
            else if (entity is MessagePart messagePart)
            {
                _logger.LogDebug("Processing MessagePart");
                CollectAllAttachments(messagePart.Message.Body, attachments);
            }
            else
            {
                _logger.LogDebug("Skipping entity type: {EntityType}", entity.GetType().Name);
            }
        }

        /// <summary>
        /// Comprehensive detection of inline content for EML import.
        /// Checks Content-Disposition, Content-ID, image content type, and related content.
        /// </summary>
        public bool IsInlineContent(MimePart mimePart)
        {
            if (mimePart.ContentDisposition?.Disposition?.Equals("inline", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogDebug("Found inline content via Content-Disposition: ContentId={ContentId}, ContentType={ContentType}, FileName={FileName}",
                    mimePart.ContentId, mimePart.ContentType?.MimeType, mimePart.FileName);
                return true;
            }

            if (!string.IsNullOrEmpty(mimePart.ContentId))
            {
                _logger.LogDebug("Found inline content via Content-ID: ContentId={ContentId}, ContentType={ContentType}, FileName={FileName}",
                    mimePart.ContentId, mimePart.ContentType?.MimeType, mimePart.FileName);
                return true;
            }

            var contentType = mimePart.ContentType?.MimeType?.ToLowerInvariant() ?? "";
            var fileName = mimePart.FileName ?? "";

            if (contentType.StartsWith("image/"))
            {
                if (mimePart.ContentDisposition == null)
                {
                    _logger.LogDebug("Found potential inline image (no ContentDisposition): ContentType={ContentType}, FileName={FileName}",
                        mimePart.ContentType?.MimeType, fileName);
                    return true;
                }

                if (string.IsNullOrEmpty(fileName) ||
                    fileName.StartsWith("image", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("inline", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("embed", StringComparison.OrdinalIgnoreCase) ||
                    Regex.IsMatch(fileName, @"^(img|pic|photo)\d*\.", RegexOptions.IgnoreCase))
                {
                    _logger.LogDebug("Found potential inline image (generic filename): ContentType={ContentType}, FileName={FileName}",
                        mimePart.ContentType?.MimeType, fileName);
                    return true;
                }
            }

            if (contentType.StartsWith("text/") && contentType.Contains("related"))
            {
                _logger.LogDebug("Found potential inline content (related text): ContentType={ContentType}, FileName={FileName}",
                    mimePart.ContentType?.MimeType, fileName);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the file extension for a MIME content type.
        /// </summary>
        public static string GetFileExtensionFromContentType(string? contentType)
        {
            return contentType?.ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                _ => ".dat"
            };
        }

        /// <summary>
        /// Saves all collected attachments to the database for a given email ID.
        /// </summary>
        public async Task SaveAllAttachments(MailArchiverDbContext context, List<MimePart> attachments, int archivedEmailId)
        {
            foreach (var attachment in attachments)
            {
                try
                {
                    using var ms = new MemoryStream();
                    await attachment.Content.DecodeToAsync(ms);

                    var fileName = attachment.FileName;
                    if (string.IsNullOrEmpty(fileName))
                    {
                        if (!string.IsNullOrEmpty(attachment.ContentId))
                        {
                            var extension = GetFileExtensionFromContentType(attachment.ContentType?.MimeType);
                            var cleanContentId = attachment.ContentId.Trim('<', '>');
                            fileName = $"inline_{cleanContentId}{extension}";
                        }
                        else
                        {
                            var extension = GetFileExtensionFromContentType(attachment.ContentType?.MimeType);
                            fileName = $"attachment_{Guid.NewGuid().ToString("N")[..8]}{extension}";
                        }
                    }

                    var emailAttachment = new EmailAttachment
                    {
                        ArchivedEmailId = archivedEmailId,
                        FileName = EmlMailCleaner.CleanText(fileName),
                        ContentType = EmlMailCleaner.CleanText(attachment.ContentType?.MimeType ?? "application/octet-stream"),
                        ContentId = !string.IsNullOrEmpty(attachment.ContentId) ? EmlMailCleaner.CleanText(attachment.ContentId) : null,
                        Content = ms.ToArray(),
                        Size = ms.Length
                    };

                    context.EmailAttachments.Add(emailAttachment);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save attachment {FileName}", attachment.FileName);
                }
            }

            try
            {
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save attachments for email");
            }
        }
    }
}