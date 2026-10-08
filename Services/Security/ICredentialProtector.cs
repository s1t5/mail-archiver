namespace MailArchiver.Services.Security
{
    /// <summary>
    /// Encrypts and decrypts individual mail account credential values (IMAP password,
    /// client secret, OAuth access/refresh tokens) before they are written to / after they
    /// are read from the database.
    ///
    /// Implementations must be safe to call for legacy plain text values: values that were
    /// stored before encryption was introduced are returned unchanged by
    /// <see cref="Unprotect(string?)"/>. Decryption failures never throw to the caller.
    /// </summary>
    public interface ICredentialProtector
    {
        /// <summary>
        /// True when encryption is active (a key is configured and encryption is enabled).
        /// When false, <see cref="Protect(string?)"/> leaves values unchanged.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// True when the value carries the encryption envelope prefix.
        /// </summary>
        bool IsProtected(string? value);

        /// <summary>
        /// Encrypts a plain text value. Null/empty values are returned unchanged. When
        /// encryption is disabled or no key is available the value is returned unchanged.
        /// </summary>
        string? Protect(string? value);

        /// <summary>
        /// Decrypts an encrypted value. Legacy plain text (no envelope prefix) is returned
        /// unchanged. On decryption failure a warning is logged and <c>null</c> is returned.
        /// </summary>
        string? Unprotect(string? value);
    }
}
