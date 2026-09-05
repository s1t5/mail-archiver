using System.Text;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;

namespace MailArchiver.Services.Providers.Graph
{
    /// <summary>
    /// JSON parse node factory for the Microsoft Graph client that sanitizes response
    /// payloads containing problematic escape sequences before they are handed to the
    /// Kiota JSON parser.
    ///
    /// Background: Exchange Online occasionally returns message bodies (large
    /// body.content strings) whose JSON string tokens cannot be safely decoded into
    /// .NET strings. Two corruption classes have been observed producing the identical
    /// production error:
    /// "The JSON value could not be converted to System.String. Path: $ | BytePositionInLine: ..."
    ///
    ///   1. Invalid UTF-8 byte sequences inside a JSON string token (e.g. raw 0xFF bytes).
    ///      JsonDocument.Parse accepts them during tokenization, but the later
    ///      JsonElement.Deserialize(string) call inside Kiota's GetStringValue() fails
    ///      when transcoding the bytes to UTF-16.
    ///
    ///   2. Unpaired Unicode surrogate escapes ("\ud83d" without its low-surrogate
    ///      partner). These are plain ASCII bytes in the JSON text, so UTF-8 validation
    ///      passes untouched, but JsonElement.Deserialize(string) still throws the
    ///      same exception.
    ///
    /// Because the exception is thrown in the middle of deserializing a message page,
    /// a single affected email makes the whole page (and effectively the folder sync)
    /// fail on every sync run.
    ///
    /// This factory sanitizes the raw response payload in two passes before parsing:
    /// invalid UTF-8 sequences are replaced with the Unicode replacement character
    /// (U+FFFD), and unpaired surrogate *escape* sequences are replaced with the
    /// equivalent "\ufffd" escape. The JSON structure and all valid characters remain
    /// untouched, so the affected email is still archived with a slightly sanitized
    /// body instead of breaking the entire sync.
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

            var sanitized = SanitizeUtf8(payload, out var utf8Replacements);
            var surrogateReplacements = SanitizeSurrogateEscapes(sanitized);

            if (utf8Replacements > 0)
            {
                _logger.LogWarning(
                    "Graph response payload contained {SequenceCount} invalid UTF-8 byte sequence(s); they were replaced with U+FFFD so the sync can continue. " +
                    "The affected message body is archived with replacement characters.",
                    utf8Replacements);
            }

            if (surrogateReplacements > 0)
            {
                _logger.LogWarning(
                    "Graph response payload contained {SequenceCount} unpaired Unicode surrogate escape(s); they were replaced with U+FFFD so the sync can continue. " +
                    "The affected message body is archived with replacement characters.",
                    surrogateReplacements);
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

        /// <summary>
        /// Replaces unpaired Unicode surrogate escape sequences ("\ud8xx" without a
        /// matching adjacent low surrogate, or lone low surrogates) with the "\ufffd"
        /// escape. Both sequences are exactly 6 bytes, so replacements are applied
        /// in place without changing offsets or payload length.
        ///
        /// The scanner tracks the JSON string-token state and the escape state so that
        /// a literal "\ud800" produced by an escaped backslash ("\\ud800") is never
        /// modified, while the escape after a literal backslash ("\\\ud800") is.
        /// </summary>
        /// <param name="payload">UTF-8 payload (already sanitized from invalid byte sequences).
        /// Mutated in place and returned.</param>
        /// <returns>Number of unpaired surrogate escapes replaced.</returns>
        internal static int SanitizeSurrogateEscapes(byte[] payload)
        {
            var replacements = 0;

            // Single forward pass, tracking whether we are inside a JSON string token and
            // whether the previous byte was an unescaped backslash (an escape opener).
            // Bytes >= 0x80 belong to (already validated) multi-byte UTF-8 sequences and
            // can never be ASCII structural characters, so they never flip the state.
            var inString = false;
            var inEscape = false;
            var i = 0;

            while (i < payload.Length)
            {
                var b = payload[i];

                if (b >= 0x80)
                {
                    i++;
                    continue;
                }

                if (!inString)
                {
                    if (b == (byte)'"')
                        inString = true;
                    i++;
                    continue;
                }

                if (inEscape)
                {
                    // We are inside a backslash escape sequence within a string; payload[i-1] is
                    // the backslash and payload[i] is the escape character.
                    if (b == (byte)'u' && i + 4 < payload.Length)
                    {
                        // \uXXXX escape - read the 4 hex digits following 'u'.
                        if (TryReadHex4(payload, i + 1, out var codePoint))
                        {
                            // The current escape spans indices (i-1) .. (i+4); the next
                            // adjacent escape - if any - starts at (i+5).
                            var adjacentEscapeStart = i + 5;

                            if (IsHighSurrogate(codePoint))
                            {
                                // A high surrogate is only valid when directly followed
                                // by an adjacent low surrogate escape.
                                if (TryReadLowSurrogatePair(payload, adjacentEscapeStart))
                                {
                                    // Valid pair: skip past both escapes.
                                    i += 11;
                                    inEscape = false;
                                    continue;
                                }

                                // Unpaired high surrogate -> replace with \ufffd (same 6 bytes).
                                WriteReplacementEscape(payload, i - 1);
                                replacements++;
                                i += 5;
                                inEscape = false;
                                continue;
                            }

                            if (IsLowSurrogate(codePoint))
                            {
                                // Lone low surrogate -> replace with \ufffd.
                                WriteReplacementEscape(payload, i - 1);
                                replacements++;
                                i += 5;
                                inEscape = false;
                                continue;
                            }

                            // Regular \uXXXX escape - skip past it.
                            i += 5;
                            inEscape = false;
                            continue;
                        }

                        // Malformed \u escape (non-hex digits) - leave it alone; the JSON
                        // parser will report it as a JsonReaderException downstream.
                        inEscape = false;
                        i += 2;
                        continue;
                    }

                    // Any other escape character (\, ", /, b, f, n, r, t, ...) consumes
                    // exactly one byte after the backslash.
                    inEscape = false;
                    i++;
                    continue;
                }

                if (b == (byte)'\\')
                {
                    inEscape = true;
                    i++;
                    continue;
                }

                if (b == (byte)'"')
                {
                    inString = false;
                    i++;
                    continue;
                }

                i++;
            }

            return replacements;
        }

        private static bool IsHighSurrogate(int codePoint) =>
            codePoint >= 0xD800 && codePoint <= 0xDBFF;

        private static bool IsLowSurrogate(int codePoint) =>
            codePoint >= 0xDC00 && codePoint <= 0xDFFF;

        /// <summary>
        /// Reads 4 hex digits at <paramref name="index"/> from <paramref name="payload"/>.
        /// Returns false when the digits are missing or not hexadecimal.
        /// </summary>
        private static bool TryReadHex4(byte[] payload, int index, out int value)
        {
            value = 0;
            if (index + 4 > payload.Length)
                return false;

            for (var j = 0; j < 4; j++)
            {
                var ch = payload[index + j];
                if (!IsHex(ch))
                    return false;
                value = (value << 4) | HexValue(ch);
            }

            return true;
        }

        private static bool IsHex(byte ch) =>
            (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');

        private static int HexValue(byte ch) =>
            ch switch
            {
                >= (byte)'0' and <= (byte)'9' => ch - (byte)'0',
                >= (byte)'a' and <= (byte)'f' => ch - (byte)'a' + 10,
                >= (byte)'A' and <= (byte)'F' => ch - (byte)'A' + 10,
                _ => 0
            };

        /// <summary>
        /// Returns true when the payload at <paramref name="index"/> directly holds a low
        /// surrogate escape "\udcXX" - i.e. the adjacent partner of a high surrogate.
        /// </summary>
        private static bool TryReadLowSurrogatePair(byte[] payload, int index)
        {
            if (index + 6 > payload.Length)
                return false;

            if (payload[index] != (byte)'\\' || payload[index + 1] != (byte)'u')
                return false;

            return TryReadHex4(payload, index + 2, out var codePoint) && IsLowSurrogate(codePoint);
        }

        /// <summary>
        /// Overwrites the 6-byte "\uXXXX" escape starting at <paramref name="startIndex"/>
        /// with "\ufffd" (same length, in-place).
        /// </summary>
        private static void WriteReplacementEscape(byte[] payload, int startIndex)
        {
            // startIndex points at the backslash of the escape.
            payload[startIndex] = (byte)'\\';
            payload[startIndex + 1] = (byte)'u';
            payload[startIndex + 2] = (byte)'f';
            payload[startIndex + 3] = (byte)'f';
            payload[startIndex + 4] = (byte)'f';
            payload[startIndex + 5] = (byte)'d';
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
    }
}