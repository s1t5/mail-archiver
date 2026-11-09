namespace MailArchiver.Auth.Helpers
{
    /// <summary>
    /// Claim types used by the cookie authentication scheme. They are read back by
    /// <see cref="MailArchiver.Auth.Services.SecurityStampValidator"/> and by the
    /// authentication service, so keep the literals in one place.
    /// </summary>
    public static class AuthClaimTypes
    {
        public const string UserId = "UserId";
        public const string SecurityStamp = "SecurityStamp";
        public const string RememberMe = "RememberMe";
    }
}
