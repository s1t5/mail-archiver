namespace MailArchiver.Models
{
    public class AuthenticationOptions
    {
        public const string Authentication = "Authentication";

        public bool Enabled { get; set; } = true; // Always true now
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "password";
        public int SessionTimeoutMinutes { get; set; } = 60;
        public string CookieName { get; set; } = "MailArchiverAuth";
    }
}
