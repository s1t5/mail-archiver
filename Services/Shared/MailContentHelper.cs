using System.Text;
using System.Text.RegularExpressions;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Shared utility methods for email content cleaning, truncation, and inline-image processing.
    /// Used by both Graph and IMAP sync pipelines as well as the core email service.
    /// All methods are static and side-effect-free for easy unit testing.
    /// </summary>
    public static class MailContentHelper
    {
        /// <summary>
        /// Removes null characters and control characters from text, replacing them with spaces.
        /// </summary>
        public static string CleanText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = text.Replace("\0", "");

            var cleanedText = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (c == '\r' || c == '\n' || c == '\t' || c >= 32)
                {
                    cleanedText.Append(c);
                }
                else
                {
                    cleanedText.Append(' ');
                }
            }

            return cleanedText.ToString();
        }

        /// <summary>
        /// Removes null bytes (0x00) from a string. PostgreSQL does not allow null bytes in TEXT/VARCHAR columns.
        /// Returns null if input is null.
        /// </summary>
        public static string? RemoveNullBytes(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            if (!input.Contains('\0'))
            {
                return input;
            }

            return input.Replace("\0", "");
        }

        /// <summary>
        /// Truncates a single field to ensure it doesn't exceed tsvector limits.
        /// </summary>
        public static string TruncateFieldForTsvector(string? text, int maxSizeBytes)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (Encoding.UTF8.GetByteCount(text) <= maxSizeBytes)
                return text;

            int approximateCharPosition = Math.Min(maxSizeBytes, text.Length);

            while (approximateCharPosition > 0 && Encoding.UTF8.GetByteCount(text.Substring(0, approximateCharPosition)) > maxSizeBytes)
            {
                approximateCharPosition--;
            }

            int wordBoundarySearch = Math.Max(0, approximateCharPosition - 50);
            int lastSpaceIndex = text.LastIndexOf(' ', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);

            if (lastSpaceIndex > wordBoundarySearch)
            {
                approximateCharPosition = lastSpaceIndex;
            }

            return text.Substring(0, approximateCharPosition) + "...";
        }

        /// <summary>
        /// Truncates text content for storage, preserving word/sentence boundaries.
        /// </summary>
        public static string TruncateTextForStorage(string? text, int maxSizeBytes)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            const string textTruncationNotice = "\n\n[CONTENT TRUNCATED - This email contains very large text content that has been truncated for better performance. The complete original content has been saved as an attachment.]";

            int noticeOverhead = Encoding.UTF8.GetByteCount(textTruncationNotice);
            int maxContentSize = maxSizeBytes - noticeOverhead;

            if (maxContentSize <= 0)
                return textTruncationNotice;

            if (Encoding.UTF8.GetByteCount(text) <= maxSizeBytes)
                return text;

            int approximateCharPosition = Math.Min(maxContentSize, text.Length);

            while (approximateCharPosition > 0 && Encoding.UTF8.GetByteCount(text.Substring(0, approximateCharPosition)) > maxContentSize)
            {
                approximateCharPosition--;
            }

            int wordBoundarySearch = Math.Max(0, approximateCharPosition - 100);
            int lastSpaceIndex = text.LastIndexOf(' ', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);
            int lastNewlineIndex = text.LastIndexOf('\n', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);
            int lastPunctuationIndex = text.LastIndexOfAny(new char[] { '.', '!', '?', ';' }, approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);

            int breakPoint = Math.Max(Math.Max(lastSpaceIndex, lastNewlineIndex), lastPunctuationIndex);
            if (breakPoint > wordBoundarySearch)
            {
                approximateCharPosition = breakPoint + 1;
            }

            string truncatedContent = text.Substring(0, approximateCharPosition);
            while (Encoding.UTF8.GetByteCount(truncatedContent + textTruncationNotice) > maxSizeBytes && truncatedContent.Length > 0)
            {
                truncatedContent = truncatedContent.Substring(0, truncatedContent.Length - 1);
            }

            return truncatedContent + textTruncationNotice;
        }

        private const string HtmlTruncationNotice = @"
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

        private const int MaxHtmlSizeBytes = 1_000_000;

        /// <summary>
        /// Cleans and truncates HTML content for storage, preserving inline cid: images.
        /// </summary>
        public static string CleanHtmlForStorage(string? html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            if (html.Contains('\0'))
            {
                html = html.Replace("\0", "");
            }

            if (html.Length <= MaxHtmlSizeBytes)
                return html;

            int TruncationOverhead = Encoding.UTF8.GetByteCount(HtmlTruncationNotice + "</body></html>");
            int maxContentSize = MaxHtmlSizeBytes - TruncationOverhead;

            if (maxContentSize <= 0)
            {
                return $"<html><body>{HtmlTruncationNotice}</body></html>";
            }

            int truncatePosition = Math.Min(maxContentSize, html.Length);

            // Preserve inline images with cid: references
            var imgMatches = Regex.Matches(html, @"<img[^>]*src\s*=\s*[""']cid:[^""']+[""'][^>]*>", RegexOptions.IgnoreCase);

            foreach (Match match in imgMatches)
            {
                int imgEnd = match.Index + match.Length;
                if (imgEnd > truncatePosition && match.Index < truncatePosition && match.Index > maxContentSize / 2)
                {
                    truncatePosition = match.Index;
                    break;
                }
            }

            // Find safe truncation point that doesn't break HTML tags
            int lastLessThan = html.LastIndexOf('<', truncatePosition - 1);
            int lastGreaterThan = html.LastIndexOf('>', truncatePosition - 1);

            if (lastLessThan > lastGreaterThan && lastLessThan >= 0)
            {
                truncatePosition = lastLessThan;
            }
            else if (lastGreaterThan >= 0)
            {
                truncatePosition = lastGreaterThan + 1;
            }

            var result = new StringBuilder(truncatePosition + HtmlTruncationNotice.Length + 50);
            ReadOnlySpan<char> baseContent = html.AsSpan(0, truncatePosition);

            bool hasHtml = baseContent.Contains("<html".AsSpan(), StringComparison.OrdinalIgnoreCase);
            bool hasBody = baseContent.Contains("<body".AsSpan(), StringComparison.OrdinalIgnoreCase);

            if (!hasHtml)
            {
                result.Append("<html>");
            }

            if (!hasBody)
            {
                if (hasHtml)
                {
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

            result.Append(HtmlTruncationNotice);

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
        /// Determines if a Graph API FileAttachment is inline content (has Content-ID or is an image).
        /// </summary>
        public static bool IsGraphInlineContent(string? contentId, string? contentType, string? fileName)
        {
            // Check for Content-ID (the most important criterion for inline content)
            if (!string.IsNullOrEmpty(contentId))
                return true;

            // Fallback: Images with inline characteristics
            if (!string.IsNullOrEmpty(contentType) &&
                contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// Gets file extension based on content type.
        /// </summary>
        public static string GetExtensionFromContentType(string? contentType)
        {
            return contentType?.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/tiff" => ".tiff",
                "image/svg+xml" => ".svg",
                "image/webp" => ".webp",
                "text/html" => ".html",
                "text/plain" => ".txt",
                "application/pdf" => ".pdf",
                _ => ".dat"
            };
        }

        /// <summary>
        /// Resolves inline images in HTML by converting cid: references to data URLs.
        /// </summary>
        public static string ResolveInlineImagesInHtml(string htmlBody, List<Models.EmailAttachment> attachments)
        {
            if (string.IsNullOrEmpty(htmlBody) || attachments == null || !attachments.Any())
                return htmlBody;

            var resultHtml = htmlBody;

            var cidMatches = Regex.Matches(htmlBody,
                @"src\s*=\s*[""']cid:([^""']+)[""']",
                RegexOptions.IgnoreCase);

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
                    }
                    catch
                    {
                        // Ignore resolution failures for individual images
                    }
                }
            }

            return resultHtml;
        }

        /// <summary>
        /// Processes HTML body to ensure inline images are properly referenced with Content-ID.
        /// </summary>
        public static string ProcessHtmlBodyForInlineImages(string htmlBody, ICollection<Models.EmailAttachment> attachments)
        {
            if (string.IsNullOrEmpty(htmlBody) || attachments == null || !attachments.Any())
                return htmlBody;

            var resultHtml = htmlBody;

            try
            {
                var cidMatches = Regex.Matches(htmlBody,
                    @"src\s*=\s*[""']cid:([^""']+)[""']",
                    RegexOptions.IgnoreCase);

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

                    if (attachment != null)
                    {
                        if (string.IsNullOrEmpty(attachment.ContentId))
                        {
                            attachment.ContentId = $"<{Guid.NewGuid()}@mailarchiver>";
                        }
                        else if (!attachment.ContentId.StartsWith("<"))
                        {
                            attachment.ContentId = $"<{attachment.ContentId}>";
                        }

                        var formattedCid = attachment.ContentId.Trim('<', '>');
                        resultHtml = resultHtml.Replace(match.Groups[0].Value, $"src=\"cid:{formattedCid}\"");
                    }
                }
            }
            catch
            {
                return htmlBody;
            }

            return resultHtml;
        }
    }
}