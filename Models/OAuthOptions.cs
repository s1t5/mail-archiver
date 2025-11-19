namespace MailArchiver.Models
{
    public class OAuthOptions
    {
        public const string OAuth = "OAuth";

        public bool Enabled { get; set; }
        public string? Authority { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string[]? ClientScopes { get; set; }
    }
}
