using MailArchiver.Services.Shared;
using MimeKit;
using System;
using System.IO;
using System.Text;

namespace MailArchiver.Services.Providers.Eml
{
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
        public async Task<MimeMessage?> TryParseMessageFromCorruptedMboxAsync(
            Stream stream, CancellationToken ct = default)
        {
            if (!stream.CanSeek) return null;
            if (stream.Position != 0) stream.Position = 0;

            // Detect a leading mbox-style From-line (max ~200 bytes).
            // Covers: "From x@y ...", "From - Tue Nov 17 ...",
            // " Aug 22 15:03:38 2008" (Thunderbird) and ">>From - ..." (Eudora).
            var firstLine = await ReadFirstLineAsync(stream, ct);
            stream.Position = 0;

            if (!LooksLikeMboxFromLine(firstLine))
                return null; // not an mbox artifact — caller handles the original error

            // Strategy 1: MimeFormat.Mbox natively consumes the marker.
            try
            {
                stream.Position = 0;
                var parser = new MimeParser(stream, MimeFormat.Mbox);
                var message = await parser.ParseMessageAsync(ct);
                _logger.LogInformation("Recovered message using Mbox parser. Subject='{Subject}'", message.Subject);
                return message;
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
                return message;
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "Message is unrecoverable even after mbox From-line removal");
                return null;
            }
            finally
            {
                if (stream.CanSeek) stream.Position = 0;
            }
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