using MailArchiver.Services.Shared;
using MimeKit;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MailArchiver.Services.Providers.Eml
{
    /// <summary>
    /// Categorizes the outcome of a header-recovery attempt on a malformed EML.
    /// </summary>
    public enum HeaderRecoveryCategory
    {
        /// <summary>No recovery was necessary or applied.</summary>
        None,
        /// <summary>Leftover mbox "From " marker line.</summary>
        MboxFromLine,
        /// <summary>Non-standard banner line before the RFC822 headers.</summary>
        BannerLine,
        /// <summary>Unparseable junk lines before the first real header.</summary>
        JunkBeforeHeaders,
        /// <summary>Blank line(s) splitting the header block into two parts.</summary>
        SplitHeaderBlock,
        /// <summary>No parseable headers could be recovered at all.</summary>
        Unrecoverable
    }

    /// <summary>Result of a tolerant header-recovery attempt.</summary>
    public record HeaderRecoveryResult(MimeMessage? Message, HeaderRecoveryCategory Category);

    /// <summary>
    /// Pre-cleans MimeMessage objects from EML/MBOX/IMAP imports to remove null bytes
    /// from text header fields before database storage.
    /// All text cleaning and truncation is delegated to <see cref="MailContentHelper"/>.
    /// </summary>
    public class EmlMailCleaner
    {
        private readonly ILogger<EmlMailCleaner> _logger;

        public EmlMailCleaner(ILogger<EmlMailCleaner> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Pre-cleans a MimeMessage to remove null bytes from all text header fields.
        /// Modifies the message in place. 
        /// Uses <see cref="MailContentHelper.RemoveNullBytes"/> for null-byte removal.
        /// </summary>
        public void PreCleanMessage(MimeMessage message)
        {
            try
            {
                if (!string.IsNullOrEmpty(message.Subject))
                {
                    message.Subject = MailContentHelper.RemoveNullBytes(message.Subject) ?? "";
                }

                CleanAddressNames(message.From);
                CleanAddressNames(message.To);
                CleanAddressNames(message.Cc);
                CleanAddressNames(message.Bcc);

                _logger.LogDebug("Pre-cleaned message to remove null bytes: Subject='{Subject}', MessageId='{MessageId}'",
                    message.Subject, message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during message pre-cleaning: {Message}", ex.Message);
            }
        }

        private static void CleanAddressNames(InternetAddressList? addresses)
        {
            if (addresses == null) return;

            foreach (var address in addresses)
            {
                if (address is MailboxAddress mailboxAddress)
                {
                    mailboxAddress.Name = MailContentHelper.RemoveNullBytes(mailboxAddress.Name);
                }
            }
        }

        /// <summary>
        /// Attempts to parse a MIME message from a stream that begins with a
        /// corrupted or leftover mbox "From " line 
        /// exports with leading whitespace, or Eudora "&gt;&gt;From" artifacts).
        /// <para>
        /// Strategy: if the first line looks like an mbox marker, try
        /// <see cref="MimeFormat.Mbox"/> first (which natively skips the marker).
        /// If that still fails, re-parse with the first line physically removed.
        /// Returns <c>null</c> when all strategies fail. The caller's stream is
        /// left at position 0 so it can be retried / discarded.
        /// </para>
        /// </summary>
        /// <summary>
        /// Legacy wrapper kept for backwards compatibility: strict mbox-artifact
        /// semantics — returns <c>null</c> unless the first line looks like a
        /// leftover mbox "From " marker. Callers that want the full tolerant
        /// recovery (banner lines, junk lines, split header blocks) should use
        /// <see cref="TryRecoverHeadersAsync"/> instead.
        /// </summary>
        public async Task<MimeMessage?> TryParseMessageFromCorruptedMboxAsync(
            Stream stream, CancellationToken ct = default)
        {
            if (!stream.CanSeek) return null;
            if (stream.Position != 0) stream.Position = 0;

            var firstLine = await ReadFirstLineAsync(stream, ct);
            stream.Position = 0;

            if (!LooksLikeMboxFromLine(firstLine))
                return null; // not an mbox artifact — caller handles the original error

            var result = await TryRecoverHeadersAsync(stream, ct);
            return result.Message;
        }

        /// <summary>
        /// Tolerant header recovery for streams whose header block cannot be parsed
        /// as-is. Covers, in order:
        /// <list type="number">
        /// <item>mbox "From " marker lines (incl. Thunderbird/Eudora variants)</item>
        /// <item>non-standard banner lines before the RFC822 headers</item>
        /// <item>any junk lines before the first real header</item>
        /// <item>blank lines splitting the header block into two parts (#499 pattern)</item>
        /// </list>
        /// Clean RFC-5322 messages never reach the recovery logic — callers invoke it
        /// only after the plain entity parse threw a FormatException.
        /// Returns the recovered message (or <c>null</c>) plus a category describing
        /// which recovery strategy succeeded. The caller's stream is left at position 0.
        /// </summary>
        public async Task<HeaderRecoveryResult> TryRecoverHeadersAsync(
            Stream stream, CancellationToken ct = default)
        {
            if (!stream.CanSeek) return new HeaderRecoveryResult(null, HeaderRecoveryCategory.Unrecoverable);
            if (stream.Position != 0) stream.Position = 0;

            // Detect a leading mbox-style From-line (max ~200 bytes).
            // Covers: "From x@y ...", "From - Tue Nov 17 ...",
            // " Aug 22 15:03:38 2008" (Thunderbird) and ">>From - ..." (Eudora).
            var firstLine = await ReadFirstLineAsync(stream, ct);
            stream.Position = 0;

            if (LooksLikeMboxFromLine(firstLine))
            {
                // Strategy 1: MimeFormat.Mbox natively consumes the marker.
                try
                {
                    stream.Position = 0;
                    var parser = new MimeParser(stream, MimeFormat.Mbox);
                    var message = await parser.ParseMessageAsync(ct);
                    _logger.LogInformation("Recovered message using Mbox parser. Subject='{Subject}'", message.Subject);
                    return new HeaderRecoveryResult(message, HeaderRecoveryCategory.MboxFromLine);
                }
                catch (FormatException)
                {
                    // fall through to strategy 2
                }

                // Strategy 2: physically strip the first line, then parse as entity.
                try
                {
                    using var stripped = StripFirstLine(stream);
                    stripped.Position = 0;
                    var parser = new MimeParser(stripped, MimeFormat.Entity);
                    var message = await parser.ParseMessageAsync(ct);
                    _logger.LogInformation("Recovered message after stripping mbox From-line. Subject='{Subject}'", message.Subject);
                    return new HeaderRecoveryResult(message, HeaderRecoveryCategory.MboxFromLine);
                }
                catch (FormatException ex)
                {
                    _logger.LogWarning(ex, "Message is unrecoverable even after mbox From-line removal");
                    return new HeaderRecoveryResult(null, HeaderRecoveryCategory.Unrecoverable);
                }
                finally
                {
                    if (stream.CanSeek) stream.Position = 0;
                }
            }

            // Strategy 3: generic header-block rebuild — handles banner lines arbitrary junk
            // before the first header, and blank lines splitting the header block.
            try
            {
                using var rebuilt = await RebuildHeaderBlockAsync(stream, ct);
                if (rebuilt.Length > 0)
                {
                    rebuilt.Position = 0;
                    var parser = new MimeParser(rebuilt, MimeFormat.Entity);
                    var message = await parser.ParseMessageAsync(ct);
                    var category = ClassifyRebuiltHeaderBlock(firstLine);
                    _logger.LogInformation("Recovered message via header-block rebuild ({Category}). Subject='{Subject}'",
                        category, message.Subject);
                    return new HeaderRecoveryResult(message, category);
                }
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "Header-block rebuild could not recover the message");
            }
            finally
            {
                if (stream.CanSeek) stream.Position = 0;
            }

            return new HeaderRecoveryResult(null, HeaderRecoveryCategory.Unrecoverable);
        }

        /// <summary>
        /// Classifies what the header-block rebuild actually removed, based on the
        /// first (discarded) line of the original stream.
        /// </summary>
        private static HeaderRecoveryCategory ClassifyRebuiltHeaderBlock(string firstLine)
        {
            if (string.IsNullOrWhiteSpace(firstLine)) return HeaderRecoveryCategory.SplitHeaderBlock;
            var trimmed = firstLine.TrimStart();
            if (trimmed.StartsWith("Microsoft Mail Internet Headers", StringComparison.OrdinalIgnoreCase))
                return HeaderRecoveryCategory.BannerLine;
            if (IsHeaderLine(trimmed)) return HeaderRecoveryCategory.SplitHeaderBlock;
            return HeaderRecoveryCategory.JunkBeforeHeaders;
        }

        /// <summary>
        /// Rebuilds a message stream whose header block is unparsable: skips junk
        /// lines before the first real header, drops blank lines that split the
        /// header block, and re-emits everything from the first valid header on
        /// (headers, blank separator, body) unchanged.
        /// </summary>
        private static async Task<MemoryStream> RebuildHeaderBlockAsync(Stream source, CancellationToken ct)
        {
            source.Position = 0;
            var output = new MemoryStream();
            using var reader = new StreamReader(source, Encoding.Latin1, detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096, leaveOpen: true);

            bool headersStarted = false;
            bool headerBlockClosed = false;
            bool pendingBlank = false;
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                ct.ThrowIfCancellationRequested();

                if (headerBlockClosed)
                {
                    await WriteLineAsync(output, line);
                    continue;
                }

                var isBlank = line.Length == 0 || line.All(c => c == ' ' || c == '\t');
                var isContinuation = !isBlank && (line[0] == ' ' || line[0] == '\t');

                if (isBlank)
                {
                    if (headersStarted)
                    {
                        // Defer the decision: if another real header follows, the blank
                        // line wrongly split the header block (#499 pattern) and is dropped;
                        // otherwise this blank is the header/body separator and is kept.
                        pendingBlank = true;
                    }
                    // Blanks before any header are junk — drop.
                    continue;
                }

                if (isContinuation)
                {
                    if (headersStarted)
                    {
                        await WriteLineAsync(output, line);
                        pendingBlank = false;
                    }
                    // Continuation before any header is junk — drop.
                    continue;
                }

                if (IsHeaderLine(line))
                {
                    if (pendingBlank)
                    {
                        // A real header follows the blank — the blank was a mid-block
                        // split, not the header/body separator. Drop it.
                        pendingBlank = false;
                    }
                    headersStarted = true;
                    await WriteLineAsync(output, line);
                    continue;
                }

                // Junk line. If headers already started this terminates the header
                // block (it becomes the first body line); otherwise it is skipped.
                if (headersStarted)
                {
                    if (pendingBlank)
                    {
                        // The blank was the header/body separator — emit it, then the
                        // rest of the stream (starting with this junk/body line).
                        pendingBlank = false;
                        await WriteLineAsync(output, string.Empty);
                    }
                    else
                    {
                        // Ensure a blank separator exists between headers and body.
                        await WriteLineAsync(output, string.Empty);
                    }
                    headerBlockClosed = true;
                    await WriteLineAsync(output, line);
                }
            }

            // Headers parsed but no explicit terminator reached (e.g. stripped banner
            // left no body) — append the blank header/body separator MimeKit needs.
            if (headersStarted && !headerBlockClosed)
                await WriteLineAsync(output, string.Empty);

            output.Position = 0;
            return output;
        }

        /// <summary>
        /// Heuristic: does this line look like a valid RFC-5322 header field line?
        /// "Name: value" where Name is printable ASCII, ':' at position 1..78.
        /// </summary>
        private static bool IsHeaderLine(string line)
        {
            var idx = line.IndexOf(':');
            if (idx < 1 || idx > 78) return false;
            for (var i = 0; i < idx; i++)
            {
                var c = line[i];
                if (c <= 32 || c > 126) return false;
            }
            return true;
        }

        private static async Task WriteLineAsync(MemoryStream stream, string line)
        {
            var bytes = Encoding.Latin1.GetBytes(line + "\r\n");
            await stream.WriteAsync(bytes);
        }

        /// <summary>Reads the first line of a text stream (up to 512 bytes) as Latin-1.</summary>
        private static async Task<string> ReadFirstLineAsync(Stream stream, CancellationToken ct)
        {
            var buffer = new byte[512];
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0) return string.Empty;
            var slice = buffer.AsSpan(0, read);
            var nl = slice.IndexOf((byte)'\n');
            if (nl >= 0) slice = slice[..nl];
            return Encoding.Latin1.GetString(slice).TrimEnd('\r');
        }

        /// <summary>Heuristic: does this line look like an mbox From-marker?</summary>
        internal static bool LooksLikeMboxFromLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            // Standard mbox marker
            if (line.StartsWith("From ", StringComparison.Ordinal)) return true;
            // Corrupted variants with extra '>' or leading whitespace (Thunderbird/Eudora/Apple Mail)
            var trimmed = line.TrimStart('>', ' ', '\t');
            if (trimmed.StartsWith("From ", StringComparison.Ordinal) ||
                trimmed.StartsWith("From -", StringComparison.Ordinal)) return true;
            // Bare timestamp line: "Aug 22 15:03:38 2008" (month name followed by day/time/year)
            if (trimmed.Length >= 20 &&
                (trimmed.StartsWith("Jan ") || trimmed.StartsWith("Feb ") || trimmed.StartsWith("Mar ") ||
                 trimmed.StartsWith("Apr ") || trimmed.StartsWith("May ") || trimmed.StartsWith("Jun ") ||
                 trimmed.StartsWith("Jul ") || trimmed.StartsWith("Aug ") || trimmed.StartsWith("Sep ") ||
                 trimmed.StartsWith("Oct ") || trimmed.StartsWith("Nov ") || trimmed.StartsWith("Dec ")))
            {
                return char.IsDigit(trimmed[4]) || trimmed[4] == ' '; // day with or without leading space
            }
            return false;
        }

        /// <summary>Returns a new MemoryStream containing the source minus its first line.</summary>
        private static MemoryStream StripFirstLine(Stream source)
        {
            source.Position = 0;
            var ms = new MemoryStream((int)Math.Min(source.Length, int.MaxValue));
            int b;
            bool pastFirstLine = false;
            while ((b = source.ReadByte()) != -1)
            {
                if (!pastFirstLine)
                {
                    if (b == '\n') pastFirstLine = true; // skip everything incl. the LF
                    continue;
                }
                ms.WriteByte((byte)b);
            }
            return ms;
        }
    }
}