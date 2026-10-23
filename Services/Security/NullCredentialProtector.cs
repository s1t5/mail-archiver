namespace MailArchiver.Services.Security
{
    /// <summary>
    /// No-op <see cref="ICredentialProtector"/> used by design-time/test contexts that are
    /// constructed without a configured key. Values pass through unchanged.
    /// </summary>
    public sealed class NullCredentialProtector : ICredentialProtector
    {
        public static readonly NullCredentialProtector Instance = new();

        private NullCredentialProtector()
        {
        }

        public bool IsEnabled => false;

        public bool IsProtected(string? value) => false;

        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
