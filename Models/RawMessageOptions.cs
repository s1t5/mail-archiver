namespace MailArchiver.Models
{
    /// <summary>
    /// Storage of the original message (the complete .eml) next to the parsed fields,
    /// bound from the "RawMessages" section. Off by default: it roughly doubles the
    /// storage per email, because attachments are kept a second time inside the message.
    /// </summary>
    public class RawMessageOptions
    {
        public const string RawMessages = "RawMessages";

        /// <summary>Master switch. When false, no original messages are stored.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Where the message bytes are kept: <see cref="RawMessageStorage.Postgres"/> (default,
        /// in the database) or <see cref="RawMessageStorage.FileSystem"/> (a file next to the
        /// archive). The metadata row - checksum, size, capture source - stays in the database
        /// either way, so the archive can always be verified against it.
        /// <para>
        /// Changing this does not move existing messages; use the storage migration
        /// (<c>--migrate-raw-storage</c> or the admin endpoint) for that.
        /// </para>
        /// </summary>
        public string Storage { get; set; } = RawMessageStorage.Postgres;

        /// <summary>
        /// Messages larger than this (uncompressed, in MB) are archived without their
        /// original. 0 = no limit.
        /// </summary>
        public int MaxMessageSizeMb { get; set; } = 50;

        /// <summary>
        /// Whether Microsoft Graph (M365) accounts store the original too. Costs one
        /// additional Graph request per message, which counts against the throttling budget.
        /// </summary>
        public bool IncludeGraph { get; set; } = true;

        /// <summary>Settings of the filesystem backend, used when <see cref="Storage"/> is filesystem.</summary>
        public RawMessageFileSystemOptions FileSystem { get; set; } = new();

        /// <summary>The normalized backend the archive writes to.</summary>
        public string EffectiveStorage => RawMessageStorage.Normalize(Storage);

        /// <summary>Whether messages are written to the filesystem.</summary>
        public bool WritesToFileSystem => EffectiveStorage == RawMessageStorage.FileSystem;
    }

    /// <summary>
    /// Settings of the filesystem storage backend ("RawMessages:FileSystem").
    /// </summary>
    public class RawMessageFileSystemOptions
    {
        /// <summary>
        /// Directory the message files are written to. Relative paths resolve against the
        /// application's content root, so the default works both in Docker (mounted volume)
        /// and when running from the repository.
        /// </summary>
        public string BasePath { get; set; } = "RawMessages";

        /// <summary>
        /// Number of two-character directory levels the message id is spread over
        /// (0 = flat directory, 2 = <c>ab/cd/1234.eml.gz</c>). Keeps the per-directory file
        /// count low on large archives; 2 gives 10,000 directories.
        /// </summary>
        public int ShardDepth { get; set; } = 2;

        /// <summary>
        /// Whether a download re-computes the SHA-256 of the file and compares it with the
        /// checksum recorded at archive time. On by default: a file outside the database can be
        /// changed without the archive noticing, and a mismatch makes the download fail instead
        /// of serving altered bytes.
        /// </summary>
        public bool VerifyChecksumOnRead { get; set; } = true;
    }
}
