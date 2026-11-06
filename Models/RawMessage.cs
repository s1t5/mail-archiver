namespace MailArchiver.Models
{
    /// <summary>
    /// Metadata of the original RFC 5322 message of an archived email, as it was received
    /// from the provider or read from the import file. Only written when RawMessages:Enabled
    /// is set. One row per email (the email id is the key); deleted together with the email.
    /// <para>
    /// The row always carries the checksum, size and capture source, because those are what
    /// the archive is verified against. The bytes themselves live either in
    /// <see cref="Content"/> (Postgres storage) or in a file next to the archive
    /// (filesystem storage), see <see cref="RawMessageStorage"/>.
    /// </para>
    /// </summary>
    public class RawMessage
    {
        /// <summary>Primary key and foreign key to <see cref="ArchivedEmail"/>.</summary>
        public int ArchivedEmailId { get; set; }

        /// <summary>
        /// The message bytes, gzip-compressed. Null when <see cref="StorageType"/> is
        /// <see cref="RawMessageStorage.FileSystem"/> - the bytes are in the file named by
        /// <see cref="StoragePath"/> then.
        /// </summary>
        public byte[]? Content { get; set; }

        /// <summary>SHA-256 of the uncompressed message as lowercase hex string (64 chars).</summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>Size of the uncompressed message in bytes.</summary>
        public long Size { get; set; }

        /// <summary>
        /// How the bytes were obtained, see <see cref="RawMessageSource"/>. Tells an
        /// exact copy from the provider apart from a re-serialization of the parsed message.
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Where the bytes of this row live, see <see cref="RawMessageStorage"/>. Rows written
        /// before the column existed are Postgres rows and keep that value.
        /// </summary>
        public string StorageType { get; set; } = RawMessageStorage.Postgres;

        /// <summary>
        /// Path of the message file relative to <c>RawMessages:FileSystem:BasePath</c>, set only
        /// when <see cref="StorageType"/> is <see cref="RawMessageStorage.FileSystem"/>.
        /// Relative so the archive survives moving the base directory.
        /// </summary>
        public string? StoragePath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ArchivedEmail ArchivedEmail { get; set; } = null!;

        /// <summary>Whether this row's bytes are stored outside the database.</summary>
        public bool IsFileSystemBacked => StorageType == RawMessageStorage.FileSystem;
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

    /// <summary>Values of <see cref="RawMessage.StorageType"/> and <c>RawMessages:Storage</c>.</summary>
    public static class RawMessageStorage
    {
        /// <summary>Bytes in the <c>RawMessages.Content</c> column.</summary>
        public const string Postgres = "postgres";

        /// <summary>Bytes in a file under <c>RawMessages:FileSystem:BasePath</c>.</summary>
        public const string FileSystem = "filesystem";

        /// <summary>Whether the given setting names a known storage backend.</summary>
        public static bool IsKnown(string? value) =>
            string.Equals(value, Postgres, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, FileSystem, StringComparison.OrdinalIgnoreCase);

        /// <summary>Canonical (lowercase) form of a backend name.</summary>
        public static string Normalize(string? value) =>
            string.Equals(value, FileSystem, StringComparison.OrdinalIgnoreCase) ? FileSystem : Postgres;
    }

    /// <summary>The original bytes of a message, handed from the fetch to the archiving step.</summary>
    public sealed record RawMessageCapture(byte[] Bytes, string Source);
}
