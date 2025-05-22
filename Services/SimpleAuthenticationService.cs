using MailArchiver.Models;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace MailArchiver.Services
{
    public class SimpleAuthenticationService : IAuthenticationService
    {
        private readonly AuthenticationOptions _authOptions;
        private readonly ILogger<SimpleAuthenticationService> _logger;

        public SimpleAuthenticationService(IOptions<AuthenticationOptions> authOptions, ILogger<SimpleAuthenticationService> logger)
        {
            _authOptions = authOptions.Value;
            _logger = logger;
        }

        public bool IsAuthenticationRequired()
        {
            return _authOptions.Enabled;
        }

        public bool ValidateCredentials(string username, string password)
        {
            if (!_authOptions.Enabled)
                return true;

            var isValid = string.Equals(username, _authOptions.Username, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(password, _authOptions.Password, StringComparison.Ordinal);

            _logger.LogInformation("Authentication attempt for user '{Username}': {Result}", 
                username, isValid ? "Success" : "Failed");

            return isValid;
        }

        public void SignIn(HttpContext context, string username)
        {
            if (!_authOptions.Enabled)
                return;

            var token = GenerateSecureToken(username);
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddMinutes(_authOptions.SessionTimeoutMinutes)
            };

            context.Response.Cookies.Append(_authOptions.CookieName, token, cookieOptions);
            
            // Store username in session for display purposes
            context.Session.SetString("Username", username);
            context.Session.SetString("LoginTime", DateTimeOffset.UtcNow.ToString());

            _logger.LogInformation("User '{Username}' signed in successfully", username);
        }

        public void SignOut(HttpContext context)
        {
            if (!_authOptions.Enabled)
                return;

            context.Response.Cookies.Delete(_authOptions.CookieName);
            context.Session.Clear();

            _logger.LogInformation("User signed out");
        }

        public bool IsAuthenticated(HttpContext context)
        {
            if (!_authOptions.Enabled)
                return true;

            var token = context.Request.Cookies[_authOptions.CookieName];
            if (string.IsNullOrEmpty(token))
                return false;

            // Simple token validation - in production, use more sophisticated validation
            return ValidateToken(token);
        }

        public string GetCurrentUser(HttpContext context)
        {
            if (!_authOptions.Enabled)
                return "System";

            return context.Session.GetString("Username") ?? "Unknown";
        }

        private string GenerateSecureToken(string username)
        {
            var data = $"{username}:{DateTimeOffset.UtcNow:yyyy-MM-dd-HH-mm}:{Guid.NewGuid()}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }

        private bool ValidateToken(string token)
        {
            // Simple validation - token exists and is not corrupted
            try
            {
                var bytes = Convert.FromBase64String(token);
                return bytes.Length == 32; // SHA256 hash length
            }
            catch
            {
                return false;
            }
        }
    }
}