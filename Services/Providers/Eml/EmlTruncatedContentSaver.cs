using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Shared;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

namespace MailArchiver.Services.Providers.Eml
{
    /// <summary>
    /// Saves truncated HTML or text email content as attachments.
    /// Resolves inline images (cid: references) to data URLs for standalone HTML export.
    /// </summary>
    public class EmlTruncatedContentSaver
    {
        private readonly ILogger<EmlTruncatedContentSaver> _logger;

        public EmlTruncatedContentSaver(ILogger<EmlTruncatedContentSaver> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Saves the original HTML content as an attachment when it was truncated.
        /// Resolves inline images by converting cid: references to data URLs.
        /// </summary>
        public async Task SaveTruncatedHtmlAsAttachment(MailArchiverDbContext context, string originalHtml, int archivedEmailId, string jobId, string messageId)
        {
            try
            {
                var email = await context.ArchivedEmails
                    .Include(e => e.Attachments)
                    .FirstOrDefaultAsync(e => e.Id == archivedEmailId);

                if (email != null && email.Attachments != null && email.Attachments.Any())
                {
                    originalHtml = ResolveInlineImagesInHtml(originalHtml, email.Attachments.ToList(), jobId);
                }

                var cleanFileName = MailContentHelper.CleanText($"original_content_{DateTime.UtcNow:yyyyMMddHHmmss}.html");
                var contentType = "text/html";

                var emailAttachment = new EmailAttachment
                {
                    ArchivedEmailId = archivedEmailId,
                    FileName = cleanFileName,
                    ContentType = contentType,
                    Content = Encoding.UTF8.GetBytes(originalHtml),
                    Size = Encoding.UTF8.GetByteCount(originalHtml)
                };

                context.EmailAttachments.Add(emailAttachment);
                await context.SaveChangesAsync();

                _logger.LogInformation("Job {JobId}: Saved original HTML content as attachment for email {MessageId}",
                    jobId, messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId}: Failed to save original HTML as attachment for {MessageId}", jobId, messageId);
            }
        }

        /// <summary>
        /// Saves the original text content as an attachment when it was truncated.
        /// </summary>
        public async Task SaveTruncatedTextAsAttachment(MailArchiverDbContext context, string originalText, int archivedEmailId, string jobId, string messageId)
        {
            try
            {
                var cleanFileName = MailContentHelper.CleanText($"original_text_content_{DateTime.UtcNow:yyyyMMddHHmmss}.txt");
                var contentType = "text/plain";

                var emailAttachment = new EmailAttachment
                {
                    ArchivedEmailId = archivedEmailId,
                    FileName = cleanFileName,
                    ContentType = contentType,
                    Content = Encoding.UTF8.GetBytes(originalText),
                    Size = Encoding.UTF8.GetByteCount(originalText)
                };

                context.EmailAttachments.Add(emailAttachment);
                await context.SaveChangesAsync();

                _logger.LogInformation("Job {JobId}: Saved original text content as attachment for email {MessageId}",
                    jobId, messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId}: Failed to save original text as attachment for {MessageId}", jobId, messageId);
            }
        }

        /// <summary>
        /// Resolves inline images in HTML by converting cid: references to data URLs.
        /// Searches for matching attachments by Content-ID or filename pattern.
        /// </summary>
        public string ResolveInlineImagesInHtml(string htmlBody, List<EmailAttachment> attachments, string jobId)
        {
            if (string.IsNullOrEmpty(htmlBody) || attachments == null || !attachments.Any())
                return htmlBody;

            var resultHtml = htmlBody;

            var cidMatches = Regex.Matches(htmlBody, @"src\s*=\s*[""']cid:([^""']+)[""']", RegexOptions.IgnoreCase);

            foreach (Match match in cidMatches)
            {
                var cid = match.Groups[1].Value;

                var attachment = attachments.FirstOrDefault(a =>
                    !string.IsNullOrEmpty(a.ContentId) &&
                    (a.ContentId.Equals($"<{cid}>", StringComparison.OrdinalIgnoreCase) ||
                     a.ContentId.Equals(cid, StringComparison.OrdinalIgnoreCase)));

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
                        var base64Content = Convert.ToBase64String(attachment.Content);
                        var dataUrl = $"data:{attachment.ContentType ?? "image/png"};base64,{base64Content}";
                        resultHtml = resultHtml.Replace(match.Groups[0].Value, $"src=\"{dataUrl}\"");
                        _logger.LogDebug("Job {JobId}: Resolved inline image CID: {Cid}", jobId, cid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Job {JobId}: Failed to resolve inline image CID: {Cid}", jobId, cid);
                    }
                }
                else
                {
                    _logger.LogWarning("Job {JobId}: Could not find attachment for CID: {Cid}", jobId, cid);
                }
            }

            return resultHtml;
        }
    }
}