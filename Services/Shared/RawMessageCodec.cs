using MailArchiver.Models;
using MimeKit;
using System.IO.Compression;
using System.Security.Cryptography;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Turns original message bytes into a <see cref="RawMessage"/> row and back.
    /// </summary>
    public static class RawMessageCodec
    {
        /// <summary>
        /// Writes a parsed message back out as bytes with CRLF line endings, the wire format.
        /// MimeKit re-emits the raw header values and the still-encoded body parts of an
        /// unmodified message, so the result matches the original up to line endings.
        /// Must be called before the message is modified (e.g. by PreCleanMessage).
        /// </summary>
        public static byte[] Serialize(MimeMessage message)
        {
            var options = FormatOptions.Default.Clone();
            options.NewLineFormat = NewLineFormat.Dos;

            using var ms = new MemoryStream();
            message.WriteTo(options, ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Builds the row for an email, or returns null when storage is disabled, nothing was
        /// captured, or the message exceeds the configured size limit.
        /// </summary>
        public static RawMessage? Create(RawMessageCapture? capture, RawMessageOptions options)
        {
            if (!options.Enabled || capture == null || capture.Bytes.Length == 0)
            {
                return null;
            }

            if (options.MaxMessageSizeMb > 0 && capture.Bytes.LongLength > options.MaxMessageSizeMb * 1024L * 1024L)
            {
                return null;
            }

            using var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(capture.Bytes, 0, capture.Bytes.Length);
            }

            return new RawMessage
            {
                Content = compressed.ToArray(),
                Sha256 = Convert.ToHexString(SHA256.HashData(capture.Bytes)).ToLowerInvariant(),
                Size = capture.Bytes.LongLength,
                Source = capture.Source,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>Returns the uncompressed message bytes of a stored row.</summary>
        public static byte[] Decompress(RawMessage raw)
        {
            using var input = new MemoryStream(raw.Content);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream(raw.Size > 0 && raw.Size < int.MaxValue ? (int)raw.Size : 0);
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }
}
