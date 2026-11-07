using MailArchiver.Models;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Result of reading an original message back: the bytes plus the metadata the archive
    /// verified them against.
    /// </summary>
    public sealed record RawMessageContent(byte[] Bytes, string Sha256, string Source, string StorageType);

    /// <summary>
    /// Thrown when a stored original cannot be served because it no longer matches the
    /// checksum recorded at archive time. Deliberately an exception rather than a silent
    /// return: altered content must never reach a caller that asked for forensically
    /// exact bytes.
    /// </summary>
    public class RawMessageIntegrityException : Exception
    {
        public RawMessageIntegrityException(int emailId, string expected, string actual)
            : base($"The stored original of email {emailId} does not match its recorded checksum " +
                   $"(expected {expected}, got {actual}). The file was modified after archiving.")
        {
            EmailId = emailId;
            Expected = expected;
            Actual = actual;
        }

        public int EmailId { get; }
        public string Expected { get; }
        public string Actual { get; }
    }

    /// <summary>
    /// Storage backend for original messages. The metadata row always lives in the database;
    /// an implementation decides only where the bytes are kept.
    /// </summary>
    public interface IRawMessageStore
    {
        /// <summary>Backend name, one of <see cref="RawMessageStorage"/>.</summary>
        string StorageType { get; }

        /// <summary>
        /// Stores the capture for the given email and returns the row to attach to it, or null
        /// when the capture is not to be stored (disabled, empty, too large).
        /// </summary>
        RawMessage? CreateRow(int emailId, RawMessageCapture? capture);

        /// <summary>
        /// Persists bytes that a row refers to but that are not part of the row itself
        /// (filesystem backend). Called after the email row has been saved, because the email id
        /// is the key. A no-op for Postgres, where the bytes travel inside the row.
        /// </summary>
        Task PersistAsync(RawMessage row, RawMessageCapture? capture);

        /// <summary>Whether an original is stored for the email.</summary>
        Task<bool> ExistsAsync(int emailId);

        /// <summary>
        /// Returns the original bytes of an email, or null when none is stored. Throws
        /// <see cref="RawMessageIntegrityException"/> when the content no longer matches the
        /// recorded checksum.
        /// </summary>
        Task<RawMessageContent?> ReadAsync(int emailId);

        /// <summary>
        /// Verifies the stored content against the recorded checksum without returning it.
        /// Returns the mismatch description, or null when the content is intact or absent.
        /// </summary>
        Task<string?> VerifyAsync(int emailId);

        /// <summary>
        /// Removes the original of an email: the stored bytes and the metadata row. Called
        /// before the email row is deleted, because a cascade removes the metadata row but
        /// cannot reach bytes kept outside the database.
        /// <para>
        /// A missing file is not an error; it is logged and the row is removed anyway, so a
        /// half-deleted original can always be cleaned up by calling this again.
        /// </para>
        /// </summary>
        Task DeleteAsync(int emailId);
    }
}
