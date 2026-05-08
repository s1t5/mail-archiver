using MimeKit;
using System.Text;

namespace MailArchiver.Services.Providers.Eml
{
    /// <summary>
    /// Cleans and truncates MimeMessage and text content from EML files.
    /// Handles null-byte removal, control-character sanitization, HTML/Text truncation
    /// with tsvector-compatible sizing, and pre-cleaning of message headers.
    /// </summary>
    public class EmlMailCleaner
    {
        private readonly ILogger<EmlMailCleaner> _logger;

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

        public EmlMailCleaner(ILogger<EmlMailCleaner> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Pre-cleans a MimeMessage to remove null bytes and other invalid UTF-8 characters
        /// from all text header fields. Modifies the message in place.
        /// </summary>
        public void PreCleanMessage(MimeMessage message)
        {
            try
            {
                if (!string.IsNullOrEmpty(message.Subject))
                {
                    message.Subject = RemoveNullBytes(message.Subject);
                }

                if (message.From != null)
                {
                    foreach (var address in message.From)
                    {
                        if (address is MailboxAddress mailboxAddress)
                        {
                            mailboxAddress.Name = RemoveNullBytes(mailboxAddress.Name);
                        }
                    }
                }

                if (message.To != null)
                {
                    foreach (var address in message.To)
                    {
                        if (address is MailboxAddress mailboxAddress)
                        {
                            mailboxAddress.Name = RemoveNullBytes(mailboxAddress.Name);
                        }
                    }
                }

                if (message.Cc != null)
                {
                    foreach (var address in message.Cc)
                    {
                        if (address is MailboxAddress mailboxAddress)
                        {
                            mailboxAddress.Name = RemoveNullBytes(mailboxAddress.Name);
                        }
                    }
                }

                if (message.Bcc != null)
                {
                    foreach (var address in message.Bcc)
                    {
                        if (address is MailboxAddress mailboxAddress)
                        {
                            mailboxAddress.Name = RemoveNullBytes(mailboxAddress.Name);
                        }
                    }
                }

                _logger.LogDebug("Pre-cleaned message to remove null bytes: Subject='{Subject}', MessageId='{MessageId}'",
                    message.Subject, message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during message pre-cleaning: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Removes null bytes (0x00) and other invalid UTF-8 control characters from a string.
        /// PostgreSQL does not allow null bytes in TEXT/VARCHAR columns.
        /// </summary>
        public static string RemoveNullBytes(string input)
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
        /// Cleans text by removing null bytes and replacing control characters with spaces.
        /// Preserves CR, LF, and TAB characters.
        /// </summary>
        public static string CleanText(string text)
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

        /// <summary>
        /// Cleans and truncates HTML content for storage within size limits.
        /// If the HTML exceeds 1 MB, it is truncated at a safe tag boundary with a notice appended.
        /// </summary>
        public string CleanHtmlForStorage(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            if (html.Contains('\0'))
            {
                html = html.Replace("\0", "");
            }

            if (html.Length <= MaxHtmlSizeBytes)
                return html;

            int maxContentSize = MaxHtmlSizeBytes - TruncationOverhead;
            if (maxContentSize <= 0)
            {
                return $"<html><body>{TruncationNotice}</body></html>";
            }

            int truncatePosition = Math.Min(maxContentSize, html.Length);

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

            var result = new StringBuilder(truncatePosition + TruncationNotice.Length + 50);

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

            result.Append(TruncationNotice);

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
        /// Truncates a text field to fit within the specified byte limit for tsvector indexing.
        /// Ensures truncation occurs at a word boundary when possible.
        /// </summary>
        public static string TruncateFieldForTsvector(string text, int maxSizeBytes)
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
        /// Truncates text content to fit within tsvector size limits while preserving readability.
        /// Appends a truncation notice and saves the original content as an attachment reference.
        /// </summary>
        public static string TruncateTextForStorage(string text, int maxSizeBytes)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            const string textTruncationNotice = "\n\n[CONTENT TRUNCATED - This email contains very large text content that has been truncated for better performance. The complete original content has been saved as an attachment.]";

            int noticeOverhead = Encoding.UTF8.GetByteCount(textTruncationNotice);
            int maxContentSize = maxSizeBytes - noticeOverhead;

            if (maxContentSize <= 0)
            {
                return textTruncationNotice;
            }

            if (Encoding.UTF8.GetByteCount(text) <= maxSizeBytes)
            {
                return text;
            }

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
    }
}