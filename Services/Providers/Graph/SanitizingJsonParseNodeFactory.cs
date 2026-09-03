using System.Text;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace MailArchiver.Services.Providers.Graph
{
    /// <summary>
    /// JSON parse node factory for the Microsoft Graph client that sanitizes response
    /// payloads containing invalid UTF-8 byte sequences before they are handed to the
    /// Kiota JSON parser.
    ///
    /// Background: Exchange Online occasionally returns message bodies (large
    /// body.content strings) that contain invalid UTF-8 bytes. The System.Text.Json
    /// tokenizer accepts those payloads during JsonDocument.ParseAsync, but the later
    /// JsonElement.Deserialize(string) call inside Kiota's GetStringValue() then fails with:
    ///   "The JSON value could not be converted to System.String. Path: $ | LineNumber: 0 | BytePositionInLine: ..."
    /// Because the exception is thrown in the middle of deserializing a message page,
    /// a single affected email makes the whole page (and effectively the folder sync)
    /// fail on every sync run.
    ///
    /// This factory scans the raw response payload for invalid UTF-8 sequences and replaces
    /// them with the Unicode replacement character (U+FFFD) before parsing. The JSON
    /// structure is untouched (invalid bytes only ever appear inside string payloads),
    /// so the affected email is still archived with a slightly sanitized body instead of
    /// breaking the entire sync.
    /// </summary>
    public class SanitizingJsonParseNodeFactory : IParseNodeFactory
    {
        private readonly JsonParseNodeFactory _innerFactory;
        private readonly ILogger<SanitizingJsonParseNodeFactory> _logger;

        public SanitizingJsonParseNodeFactory(ILogger<SanitizingJsonParseNodeFactory> logger)
        {
            _innerFactory = new JsonParseNodeFactory();
            _logger = logger;
        }

        /// <inheritdoc/>
        public string ValidContentType => _innerFactory.ValidContentType;

        /// <inheritdoc/>
        public async Task<IParseNode> GetRootParseNodeAsync(
            string contentType,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(contentType))
                throw new ArgumentNullException(nameof(contentType));

            if (!ValidContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentOutOfRangeException(nameof(contentType), $"expected a {ValidContentType} content type");

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            if (!content.CanSeek)
            {
                // Kiota passes buffered, seekable streams. If a non-seekable stream ever
                // shows up we cannot validate upfront, so fall through to the regular
                // parser behavior (defensive branch).
                return await _innerFactory.GetRootParseNodeAsync(contentType, content, cancellationToken);
            }

            var payload = new byte[content.Length];
            await content.ReadExactlyAsync(payload, cancellationToken);

            var sanitized = SanitizeUtf8(payload, out var replacements);
            if (replacements > 0)
            {
                _logger.LogWarning(
                    "Graph response payload contained {SequenceCount} invalid UTF-8 byte sequence(s); they were replaced with U+FFFD so the sync can continue. " +
                    "The affected message body is archived with replacement characters.",
                    replacements);
            }

            var sanitizedStream = new MemoryStream(sanitized, writable: false);
            return await _innerFactory.GetRootParseNodeAsync(contentType, sanitizedStream, cancellationToken);
        }

        /// <summary>
        /// Returns a UTF-8 payload in which every invalid byte sequence has been replaced
        /// by the Unicode replacement character (U+FFFD). When the input is already
        /// valid UTF-8 the input array is returned unchanged.
        /// </summary>
        /// <param name="payload">Raw JSON payload bytes.</param>
        /// <param name="replacements">Number of replaced sequences (0 when the input was valid).</param>
        internal static byte[] SanitizeUtf8(byte[] payload, out int replacements)
        {
            replacements = 0;

            var (isValid, firstInvalidIndex) = ValidateUtf8(payload);
            if (isValid)
                return payload;

            var validPrefix = payload.AsSpan(0, firstInvalidIndex);
            var validPrefixText = Encoding.UTF8.GetString(validPrefix);
            var replacedCount = 0;

            var result = new StringBuilder(payload.Length);
            result.Append(validPrefixText);

            var i = firstInvalidIndex;
            while (i < payload.Length)
            {
                var b = payload[i];
                if (b < 0x80)
                {
                    result.Append((char)b);
                    i++;
                    continue;
                }

                var sequenceLength = b switch
                {
                    >= 0xC2 and < 0xE0 => 2,
                    >= 0xE0 and < 0xF0 => 3,
                    >= 0xF0 and <= 0xF4 => 4,
                    _ => 0
                };

                if (sequenceLength == 0 || i + sequenceLength > payload.Length)
                {
                    result.Append('\uFFFD');
                    replacedCount++;
                    i++;
                    continue;
                }

                var sequence = payload.AsSpan(i, sequenceLength);
                var valid = true;
                for (var j = 1; j < sequenceLength; j++)
                {
                    if ((sequence[j] & 0xC0) != 0x80)
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid)
                {
                    result.Append('\uFFFD');
                    replacedCount++;
                    i++;
                    continue;
                }

                var codePoint = DecodeCodePoint(sequence);
                if (codePoint > 0x10FFFF ||
                    (codePoint >= 0xD800 && codePoint <= 0xDFFF) ||
                    (sequenceLength == 2 && codePoint < 0x80) ||
                    (sequenceLength == 3 && codePoint < 0x800) ||
                    (sequenceLength == 4 && codePoint < 0x10000))
                {
                    result.Append('\uFFFD');
                    replacedCount++;
                    i++;
                    continue;
                }

                var decoded = Encoding.UTF8.GetString(sequence.ToArray());
                result.Append(decoded);
                i += sequenceLength;
            }

            replacements = replacedCount;
            return Encoding.UTF8.GetBytes(result.ToString());
        }

        /// <summary>
        /// Fast strict UTF-8 validation. Returns (true, -1) for valid payloads,
        /// otherwise (false, index of the first invalid byte).
        /// </summary>
        internal static (bool IsValid, int FirstInvalidIndex) ValidateUtf8(ReadOnlySpan<byte> bytes)
        {
            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                if (b < 0x80)
                    continue;

                var sequenceLength = b switch
                {
                    >= 0xC2 and < 0xE0 => 2,
                    >= 0xE0 and < 0xF0 => 3,
                    >= 0xF0 and <= 0xF4 => 4,
                    _ => 0
                };

                if (sequenceLength == 0 || i + sequenceLength > bytes.Length)
                    return (false, i);

                var sequence = bytes.Slice(i, sequenceLength);
                var valid = true;
                for (var j = 1; j < sequenceLength; j++)
                {
                    if ((sequence[j] & 0xC0) != 0x80)
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid)
                    return (false, i);

                var codePoint = DecodeCodePoint(sequence);
                if (codePoint > 0x10FFFF ||
                    (codePoint >= 0xD800 && codePoint <= 0xDFFF) ||
                    (sequenceLength == 2 && codePoint < 0x80) ||
                    (sequenceLength == 3 && codePoint < 0x800) ||
                    (sequenceLength == 4 && codePoint < 0x10000))
                {
                    return (false, i);
                }

                i += sequenceLength - 1;
            }

            return (true, -1);
        }

        private static int DecodeCodePoint(ReadOnlySpan<byte> sequence)
        {
            return sequence[0] switch
            {
                >= 0xC2 and < 0xE0 => ((sequence[0] & 0x1F) << 6) | (sequence[1] & 0x3F),
                >= 0xE0 and < 0xF0 => ((sequence[0] & 0x0F) << 12) | ((sequence[1] & 0x3F) << 6) | (sequence[2] & 0x3F),
                >= 0xF0 and <= 0xF4 => ((sequence[0] & 0x07) << 18) | ((sequence[1] & 0x3F) << 12) | ((sequence[2] & 0x3F) << 6) | (sequence[3] & 0x3F),
                _ => -1
            };
        }

        private static int CountReplacements(string lossyText)
        {
            var count = 0;
            var idx = 0;
            while ((idx = lossyText.IndexOf('\uFFFD', idx)) >= 0)
            {
                count++;
                idx++;
            }

            return count;
        }
    }
}