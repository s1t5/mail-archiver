namespace MailArchiver.Models
{
    /// <summary>
    /// Configuration options for the encryption of stored mail account credentials
    /// (IMAP password, M365 client secret and MSA OAuth tokens).
    ///
    /// The encryption key (KEK) is supplied from outside the database, e.g. via the
    /// environment variable <c>Security__CredentialEncryptionKey</c>. When no key is
    /// configured, credentials are stored as plain text (the pre-encryption behavior)
    /// and a warning is logged at startup; existing plain text remains readable.
    /// </summary>
    public class SecurityOptions
    {
        public const string SectionName = "Security";

        /// <summary>
        /// Base64 encoded 256-bit (32 byte) key encryption key. Recommended to be set
        /// via the environment (Docker secret / .env), never committed to appsettings.json.
        /// Generate with: <c>openssl rand -base64 32</c>.
        /// </summary>
        public string? CredentialEncryptionKey { get; set; }

        /// <summary>
        /// Optional previous key, only used to decrypt values that were encrypted with it
        /// (e.g. during key rotation). New values are always encrypted with
        /// <see cref="CredentialEncryptionKey"/>.
        /// </summary>
        public string? CredentialEncryptionKeyPrevious { get; set; }

        /// <summary>
        /// When true (default) new/updated credential values are encrypted as soon as a
        /// valid key is configured. When false, new values are kept as plain text while
        /// already encrypted values remain readable.
        /// </summary>
        public bool EncryptAccountCredentials { get; set; } = true;

        /// <summary>
        /// When true (default) a background service encrypts existing plain text
        /// credentials once a valid key is configured. No-op without a key.
        /// </summary>
        public bool CredentialEncryptionBackfill { get; set; } = true;
    }
}
