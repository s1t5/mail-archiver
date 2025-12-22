namespace MailArchiver.Auth.Options
{
    public class OAuthOptions
    {
        public const string OAuth = "OAuth";
        public const string SignInScheme = "OidcCookie";

        public bool Enabled { get; set; }
        public string? Authority { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string[]? ClientScopes { get; set; }
        public bool DisablePasswordLogin { get; set; }
        public bool AutoRedirect { get; set; }
        public string[]? AdminEmails { get; set; }
    }
}
