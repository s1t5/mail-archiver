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
        public string? DisplayName { get; set; }
        public string[]? ClientScopes { get; set; }

        /// <summary>
        /// Response mode of the authorization request: <c>query</c>, <c>fragment</c> or
        /// <c>form_post</c>. Leave empty to keep the framework default (<c>form_post</c>).
        /// </summary>
        public string? ResponseMode { get; set; }

        public bool DisablePasswordLogin { get; set; }
        public bool AutoRedirect { get; set; }
        public bool AutoApproveUsers { get; set; }
        public string[]? AdminEmails { get; set; }
    }
}
