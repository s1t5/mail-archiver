namespace MailArchiver.Models
{
    /// <summary>
    /// The original RFC 5322 message of an archived email, as it was received from the
    /// provider or read from the import file. Only written when RawMessages:Enabled is set.
    /// One row per email (the email id is the key); deleted together with the email.
    /// </summary>
    public class RawMessage
    {
        /// <summary>Primary key and foreign key to <see cref="ArchivedEmail"/>.</summary>
        public int ArchivedEmailId { get; set; }

        /// <summary>The message bytes, gzip-compressed.</summary>
        public byte[] Content { get; set; } = Array.Empty<byte>();

        /// <summary>SHA-256 of the uncompressed message as lowercase hex string (64 chars).</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>Size of the uncompressed message in bytes.</summary>
        public long Size { get; set; }

        /// <summary>
        /// How the bytes were obtained, see <see cref="RawMessageSource"/>. Tells an
        /// exact copy from the provider apart from a re-serialization of the parsed message.
        /// </summary>
        public string Source { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ArchivedEmail ArchivedEmail { get; set; } = null!;
    }

    /// <summary>Values of <see cref="RawMessage.Source"/>.</summary>
    public static class RawMessageSource
    {
        /// <summary>Byte-exact BODY[] fetched from the IMAP server.</summary>
        public const string Imap = "imap";

        /// <summary>Byte-exact MIME content fetched from Microsoft Graph.</summary>
        public const string Graph = "graph";

        /// <summary>Byte-exact content of the imported .eml file.</summary>
        public const string EmlFile = "eml-file";

        /// <summary>
        /// The parsed message written back out by MimeKit (used where the original bytes are
        /// not at hand, e.g. mbox import and IMAP recovery fetches). MimeKit keeps the raw
        /// headers and the encoded body parts, so this differs from the original at most in
        /// line endings, which are normalized to CRLF.
        /// </summary>
        public const string Serialized = "serialized";
    }

    /// <summary>The original bytes of a message, handed from the fetch to the archiving step.</summary>
    public sealed record RawMessageCapture(byte[] Bytes, string Source);
}
