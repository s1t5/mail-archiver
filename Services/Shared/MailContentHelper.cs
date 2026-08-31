using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MimeKit;

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
        /// Normalizes a Message-ID header value to its canonical form: the bare
        /// <c>local-part@domain</c> token without any surrounding angle brackets,
        /// whitespace, or stray bracket characters. Repeatedly strips leading
        /// <c>&lt;</c> and trailing <c>&gt;</c> (plus whitespace) until no outer
        /// bracket characters remain, so that malformed values such as
        /// <c>&lt;id@host&gt;&gt;</c>, <c>id@host&gt;</c>, <c>&lt;id@host</c>,
        /// <c>&lt;&lt;id@host&gt;&gt;</c>, and <c>"  id@host  "</c> all reduce to
        /// <c>id@host</c>. Returns <see cref="string.Empty"/> for null/empty/whitespace-only input.
        /// Does NOT validate the inner token — it is returned verbatim so that
        /// genuinely unknown identifiers still produce a stable, comparable key.
        /// </summary>
        /// <param name="raw">The raw Message-ID header value as read from IMAP/Graph.</param>
        /// <returns>The normalized, bracket-free identifier; never null.</returns>
        public static string NormalizeMessageId(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var value = raw.Trim();

            // Iteratively strip a leading '<' and/or a trailing '>', trimming
            // whitespace after each step. This handles arbitrary combinations
            // such as "<<id>>", "id>>", "<id", "<id> ", etc., in a single pass
            // per bracket character.
            bool changed;
            do
            {
                changed = false;

                if (value.Length > 0 && value[0] == '<')
                {
                    value = value.Substring(1);
                    changed = true;
                }

                if (value.Length > 0 && value[^1] == '>')
                {
                    value = value.Substring(0, value.Length - 1);
                    changed = true;
                }

                if (changed)
                {
                    var trimmed = value.Trim();
                    if (trimmed.Length != value.Length)
                    {
                        value = trimmed;
                    }
                }
            } while (changed);

            return value;
        }

        /// <summary>
        /// Sets <see cref="MimeMessage.MessageId"/> only when the stored value yields a usable
        /// restorable identifier. Mirrors the guard in
        /// <c>ImapMailRestorer.CreateMimeMessageFromArchivedEmailAsync</c> so the export paths
        /// cannot throw on rows whose stored Message-ID reduces to empty (MailKit 4.17.0 rejects
        /// an empty string with ArgumentException) or to a token without a domain, which the
        /// append path drops as well so MimeKit generates a fresh id (M3).
        /// </summary>
        public static void ApplyRestorableMessageId(MimeMessage message, string? storedMessageId)
        {
            var restorable = NormalizeMessageId(storedMessageId);
            if (!string.IsNullOrEmpty(restorable) && restorable.Contains('@'))
            {
                message.MessageId = restorable;
            }
        }

        /// <summary>
        /// Generates a deterministic fallback Message-ID for messages that have no
        /// Message-ID header. The ID is a SHA-256 hash over the pipe-joined components
        /// <c>from|to|subject|dateTicks</c> (plus <paramref name="canonicalHeaders"/> when
        /// supplied), formatted as <c>generated-{16 Base64URL chars}@mail-archiver.local</c>.
        /// The same algorithm is used by all archiving pipelines (IMAP, EML/MBOX import,
        /// M365/Graph) so identical messages produce identical keys everywhere.
        /// </summary>
        /// <param name="from">Comma-separated bare sender addresses (or null).</param>
        /// <param name="to">Comma-separated bare recipient addresses (or null).</param>
        /// <param name="subject">The message subject (null is treated as empty).</param>
        /// <param name="dateTicks">The message date in ticks (0 when unknown).</param>
        /// <param name="canonicalHeaders">
        /// Optional canonical header block (see <see cref="BuildCanonicalHeaders"/>). The
        /// IMAP pipeline includes it so that distinct deliveries whose From/To/Subject/Date
        /// are all identical (very old mail without Message-ID and Subject) still produce
        /// distinct keys via their differing Received chains. Import/Graph omit it.
        /// </param>
        public static string GenerateFallbackMessageId(string? from, string? to, string? subject, long dateTicks, string? canonicalHeaders = null)
        {
            var uniqueString = $"{from ?? string.Empty}|{to ?? string.Empty}|{subject ?? string.Empty}|{dateTicks}";
            if (!string.IsNullOrEmpty(canonicalHeaders))
            {
                uniqueString += $"|{canonicalHeaders}";
            }

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(uniqueString));
            var hashString = Convert.ToBase64String(hashBytes).Replace("+", "-").Replace("/", "_").Substring(0, 16);
            return $"generated-{hashString}@mail-archiver.local";
        }

        /// <summary>
        /// Builds a canonical string representation of a header list: one
        /// <c>Field: Value</c> line per header, in original order, joined by newlines.
        /// Used as a per-delivery discriminator for the fallback Message-ID (the Received
        /// chain differs between deliveries even when all other headers are identical).
        /// The same canonicalization is applied by the archiving and the retention-deletion
        /// path so both compute matching keys.
        /// </summary>
        public static string BuildCanonicalHeaders(IEnumerable<Header>? headers)
        {
            if (headers == null)
                return string.Empty;

            return string.Join("\n", headers.Select(h => $"{h.Field}: {h.Value}"));
        }

        /// <summary>
        /// Extracts the message date with fallback handling for missing or malformed Date
        /// headers. Tries the Date header first, then falls back to the Received headers
        /// (oldest hop first), then to Resent-Date, and finally returns
        /// <see cref="DateTimeOffset.MinValue"/>. Shared by the archiving pipeline and the
        /// retention-deletion pipeline so both compute identical dates (and therefore
        /// identical fallback Message-IDs) for the same message.
        /// </summary>
        /// <param name="dateHeader">
        /// The parsed Date header value. Pass <c>default</c> (<see cref="DateTimeOffset.MinValue"/>)
        /// when the message has no parsable Date header.
        /// </param>
        /// <param name="headers">The message headers used for the Received/Resent-Date fallbacks.</param>
        /// <returns>A DateTimeOffset representing the email's date.</returns>
        public static DateTimeOffset ExtractEmailDate(DateTimeOffset dateHeader, HeaderList? headers)
        {
            if (dateHeader != default)
                return dateHeader;

            if (headers != null)
            {
                // Fallback 1: Try to extract date from Received headers. Iterate from the end
                // of the list (oldest hop in the chain, i.e. when the mail was originally
                // received) towards the front.
                try
                {
                    var receivedHeaders = headers.Where(h => h.Id == HeaderId.Received).ToList();
                    for (int i = receivedHeaders.Count - 1; i >= 0; i--)
                    {
                        var dateFromReceived = ExtractDateFromReceivedHeader(receivedHeaders[i].Value);
                        if (dateFromReceived.HasValue)
                            return dateFromReceived.Value;
                    }
                }
                catch
                {
                    // Ignore malformed Received headers and fall through to the next fallback.
                }

                // Fallback 2: Try Resent-Date header
                try
                {
                    var resentDateHeader = headers.FirstOrDefault(h => h.Id == HeaderId.ResentDate);
                    if (resentDateHeader != null)
                    {
                        var resentDate = ParseDateHeaderValue(resentDateHeader.Value);
                        if (resentDate.HasValue)
                            return resentDate.Value;
                    }
                }
                catch
                {
                    // Ignore malformed Resent-Date headers and fall through.
                }
            }

            // Fallback 3: Use MinValue to indicate an unknown date
            return DateTimeOffset.MinValue;
        }

        /// <summary>
        /// Extracts a date from a Received header value. Received headers typically end with
        /// a date in a format like <c>; Sat, 16 Dec 2000 08:45:05 +0100 (CET)</c>.
        /// </summary>
        /// <param name="receivedHeader">The Received header value.</param>
        /// <returns>A DateTimeOffset if parsing was successful, null otherwise.</returns>
        public static DateTimeOffset? ExtractDateFromReceivedHeader(string receivedHeader)
        {
            if (string.IsNullOrEmpty(receivedHeader))
                return null;

            // Find the semicolon that precedes the date
            var lastSemicolon = receivedHeader.LastIndexOf(';');
            if (lastSemicolon < 0 || lastSemicolon >= receivedHeader.Length - 1)
                return null;

            var datePart = receivedHeader.Substring(lastSemicolon + 1).Trim();

            return ParseDateHeaderValue(datePart);
        }

        /// <summary>
        /// Parses a date string from a header value, handling various formats gracefully.
        /// </summary>
        /// <param name="dateString">The date string to parse.</param>
        /// <returns>A DateTimeOffset if parsing was successful, null otherwise.</returns>
        public static DateTimeOffset? ParseDateHeaderValue(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return null;

            // Remove any trailing comments in parentheses like (CET) or (GMT)
            var parenIndex = dateString.IndexOf('(');
            if (parenIndex > 0)
            {
                dateString = dateString.Substring(0, parenIndex).Trim();
            }

            // Try various date formats
            var formats = new[]
            {
                "ddd, d MMM yyyy H:mm:ss zzz",
                "ddd, d MMM yyyy HH:mm:ss zzz",
                "ddd, d MMM yyyy H:mm:ss",
                "ddd, d MMM yyyy HH:mm:ss",
                "d MMM yyyy H:mm:ss zzz",
                "d MMM yyyy HH:mm:ss zzz",
                "d MMM yyyy H:mm:ss",
                "d MMM yyyy HH:mm:ss",
                "ddd, d MMM yy H:mm:ss zzz",
                "ddd, d MMM yy HH:mm:ss zzz",
                "d MMM yy H:mm:ss zzz",
                "d MMM yy HH:mm:ss zzz"
            };

            foreach (var format in formats)
            {
                if (DateTimeOffset.TryParseExact(dateString, format, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var result))
                {
                    return result;
                }
            }

            // Try the standard RFC 2822 date parsing as a fallback
            if (DateTimeOffset.TryParse(dateString, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var parsedDate))
            {
                return parsedDate;
            }

            return null;
        }

        /// <summary>
        /// Builds the complete set of fallback Message-ID candidate keys under which a message
        /// without a Message-ID header may have been stored in the archive, depending on which
        /// pipeline and which application version archived it:
        /// <list type="number">
        /// <item>Current IMAP archiving format: hash over from|to|subject|dateTicks plus the
        /// canonical header block (date from the full fallback chain of <see cref="ExtractEmailDate"/>).</item>
        /// <item>EML/MBOX import format: hash over from|to|subject|dateTicks without canonical
        /// headers, using only the Date header (ticks 0 when missing).</item>
        /// <item>Legacy IMAP archiving format (before the deterministic generator existed):
        /// <c>{From}-{To}-{Subject}-{dateTicks}</c> (date from the full fallback chain).</item>
        /// </list>
        /// The retention-deletion path matches all candidates so messages archived by any of
        /// these pipelines/versions are recognized as archived and can be deleted.
        /// </summary>
        /// <param name="from">Comma-separated bare sender addresses.</param>
        /// <param name="to">Comma-separated bare recipient addresses.</param>
        /// <param name="subject">The message subject (null is treated as empty).</param>
        /// <param name="dateHeader">
        /// The parsed Date header value; <c>default</c> (<see cref="DateTimeOffset.MinValue"/>) when missing.
        /// </param>
        /// <param name="headers">The message headers (for the canonical header block and date fallbacks).</param>
        /// <param name="legacyFromText">
        /// Formatted sender address list (<c>InternetAddressList.ToString()</c> semantics) used by the
        /// legacy key; falls back to the bare addresses when null.
        /// </param>
        /// <param name="legacyToText">
        /// Formatted recipient address list (<c>InternetAddressList.ToString()</c> semantics) used by the
        /// legacy key; falls back to the bare addresses when null.
        /// </param>
        /// <returns>The distinct candidate keys.</returns>
        public static List<string> GenerateFallbackMessageIdCandidates(
            string? from, string? to, string? subject,
            DateTimeOffset dateHeader,
            HeaderList? headers,
            string? legacyFromText, string? legacyToText)
        {
            var extractedTicks = ExtractEmailDate(dateHeader, headers).Ticks;

            var candidates = new List<string>(3)
            {
                // Current IMAP archiving format (with canonical headers)
                GenerateFallbackMessageId(from, to, subject, extractedTicks, BuildCanonicalHeaders(headers)),
                // EML/MBOX import format (no canonical headers, Date header only - 0 ticks when missing)
                GenerateFallbackMessageId(from, to, subject, dateHeader.Ticks),
                // Legacy IMAP archiving format
                $"{legacyFromText ?? from ?? string.Empty}-{legacyToText ?? to ?? string.Empty}-{subject}-{extractedTicks}"
            };

            return candidates.Distinct().ToList();
        }

        /// <summary>
        /// Determines whether the supplied "text" content is actually HTML markup rather than genuine plain text.
        /// This happens when an email was archived without a real text/plain part: the archiving fallback stores
        /// the raw HTML in the Body field. Emitting such content as a text/plain MIME part would be incorrect.
        /// </summary>
        /// <param name="text">The candidate plain-text content (e.g. the Body field).</param>
        /// <param name="htmlBody">The HTML body of the same email, used for an equality check.</param>
        public static bool IsHtmlContent(string? text, string? htmlBody)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // If the "text" is identical to the HTML body, it is clearly HTML stored as text.
            if (!string.IsNullOrEmpty(htmlBody) && string.Equals(text, htmlBody, StringComparison.Ordinal))
                return true;

            // Heuristic: content that begins with an HTML document/markup marker is HTML, not plain text.
            var trimmed = text.TrimStart();
            if (trimmed.Length == 0)
                return false;

            return trimmed.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("<head", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("<body", StringComparison.OrdinalIgnoreCase);
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

        /// <summary>
        /// Splits whitespace-delimited tokens longer than <paramref name="maxTokenLength"/> by inserting
        /// a space every <paramref name="maxTokenLength"/> characters. Prevents PostgreSQL tsvector
        /// "word is too long to be indexed" warnings and avoids per-row re-tokenization cost for
        /// inline Base64/Hex/minified blobs. Prose is never affected (ordinary words are far shorter).
        /// Returns null when <paramref name="text"/> is null, and empty string for empty input.
        /// </summary>
        public static string? SanitizeLongTokens(string? text, int maxTokenLength = 2047)
        {
            if (text is null) return null;
            if (text.Length == 0) return string.Empty;
            if (maxTokenLength <= 0) return text;

            int longestToken = 0;
            int i = 0;
            while (i < text.Length)
            {
                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                int len = i - start;
                if (len > longestToken) longestToken = len;
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            }

            if (longestToken <= maxTokenLength) return text;

            var sb = new StringBuilder(text.Length + 32);
            i = 0;
            while (i < text.Length)
            {
                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                int len = i - start;
                if (len <= maxTokenLength)
                {
                    sb.Append(text, start, len);
                }
                else
                {
                    int written = 0;
                    while (written < len)
                    {
                        int chunk = Math.Min(maxTokenLength, len - written);
                        sb.Append(text, start + written, chunk);
                        written += chunk;
                        if (written < len) sb.Append(' ');
                    }
                }
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    sb.Append(text[i]);
                    i++;
                }
            }

            return sb.ToString();
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
        /// Applies display names from a comma-separated string to a parsed InternetAddressList.
        /// If the number of parsed names does not match the number of addresses, no names are
        /// applied (safe fallback to preserve the bare addresses without false assignments).
        /// </summary>
        public static void ApplyDisplayNames(InternetAddressList? addresses, string? displayNamesCsv)
        {
            if (string.IsNullOrEmpty(displayNamesCsv) || addresses == null || addresses.Count == 0)
                return;

            var names = displayNamesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim())
                                       .ToArray();

            if (names.Length != addresses.Count)
                return;

            for (int i = 0; i < addresses.Count; i++)
            {
                if (addresses[i] is MailboxAddress mb && !string.IsNullOrEmpty(names[i]))
                    mb.Name = names[i];
            }
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