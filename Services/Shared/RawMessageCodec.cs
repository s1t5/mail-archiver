using MailArchiver.Models;
using MimeKit;
using System.IO.Compression;
using System.Security.Cryptography;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Turns original message bytes into a <see cref="RawMessage"/> row and back, and does the
    /// compression/checksum work both storage backends share.
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

        /// <summary>SHA-256 of the message as lowercase hex (64 chars), the form stored in the row.</summary>
        public static string ComputeSha256(byte[] bytes)
            => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        /// <summary>Gzip-compresses the message bytes.</summary>
        public static byte[] Compress(byte[] bytes)
        {
            using var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(bytes, 0, bytes.Length);
            }
            return compressed.ToArray();
        }

        /// <summary>
        /// Decompresses stored message bytes. <paramref name="expectedSize"/> is only used to
        /// pre-size the output buffer; a wrong value costs a reallocation, not correctness.
        /// </summary>
        public static byte[] Decompress(byte[] compressed, long expectedSize = 0)
        {
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream(expectedSize > 0 && expectedSize < int.MaxValue ? (int)expectedSize : 0);
            gzip.CopyTo(output);
            return output.ToArray();
        }

        /// <summary>
        /// Whether the capture is to be stored at all: storage enabled, something captured, and
        /// within the configured size limit. Shared so both backends agree on what is skipped.
        /// </summary>
        public static bool ShouldStore(RawMessageCapture? capture, RawMessageOptions options)
        {
            if (!options.Enabled || capture == null || capture.Bytes.Length == 0)
            {
                return false;
            }

            return options.MaxMessageSizeMb <= 0
                || capture.Bytes.LongLength <= options.MaxMessageSizeMb * 1024L * 1024L;
        }

        /// <summary>
        /// Builds a Postgres-backed row: metadata plus the gzip-compressed bytes in
        /// <see cref="RawMessage.Content"/>. Returns null when the capture is not to be stored.
        /// </summary>
        public static RawMessage? Create(RawMessageCapture? capture, RawMessageOptions options)
        {
            if (!ShouldStore(capture, options))
            {
                return null;
            }

            return new RawMessage
            {
                Content = Compress(capture!.Bytes),
                Sha256 = ComputeSha256(capture.Bytes),
                Size = capture.Bytes.LongLength,
                Source = capture.Source,
                StorageType = RawMessageStorage.Postgres,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Builds a filesystem-backed row: the same metadata, but no content - the bytes go to
        /// the file named by <paramref name="relativePath"/>. Returns null when the capture is
        /// not to be stored.
        /// </summary>
        public static RawMessage? CreateFileSystemBacked(
            RawMessageCapture? capture, RawMessageOptions options, string relativePath)
        {
            if (!ShouldStore(capture, options))
            {
                return null;
            }

            return new RawMessage
            {
                Content = null,
                Sha256 = ComputeSha256(capture!.Bytes),
                Size = capture.Bytes.LongLength,
                Source = capture.Source,
                StorageType = RawMessageStorage.FileSystem,
                StoragePath = relativePath,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Returns the uncompressed message bytes of a row, taking them from the content column
        /// or - for a filesystem-backed row - from the supplied file bytes. The caller reads the
        /// file; this type does no I/O.
        /// </summary>
        public static byte[] Decompress(RawMessage raw, byte[]? fileContent = null)
        {
            var compressed = raw.IsFileSystemBacked
                ? fileContent ?? throw new InvalidOperationException(
                    $"Raw message {raw.ArchivedEmailId} is filesystem-backed; its file content must be supplied.")
                : raw.Content ?? throw new InvalidOperationException(
                    $"Raw message {raw.ArchivedEmailId} has no content and is not filesystem-backed.");

            return Decompress(compressed, raw.Size);
        }
    }
}
