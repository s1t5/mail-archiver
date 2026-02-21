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
        
        /// <summary>
        /// Configures the SameSite attribute for authentication and session cookies.
        /// Valid values: "Strict", "Lax", "None".
        /// Default is "Strict" for maximum security (original behavior).
        /// Use "Lax" to allow navigation from external links while maintaining CSRF protection.
        /// </summary>
        public string CookieSameSite { get; set; } = "Strict";
    }
}
