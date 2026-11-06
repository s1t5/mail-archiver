namespace MailArchiver.Auth.Exceptions
{
    /// <summary>
    /// Raised when the user info returned by the identity provider does not contain a
    /// claim that is required to sign the user in.
    /// </summary>
    public class MissingClaimException : OidcAuthenticationException
    {
        public string ClaimType { get; private set; }

        public MissingClaimException(string claimType)
            : base("OidcMissingClaim", claimType)
        {
            ClaimType = claimType;
        }
    }
}
