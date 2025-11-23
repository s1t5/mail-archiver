using MailArchiver.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MailArchiver.Auth.Services
{
    public class AuthenticationService : MailArchiver.Services.IAuthenticationService
    {
        private readonly ILogger<AuthenticationService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthenticationService(
            ILogger<AuthenticationService> logger
            , IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetCurrentUserDisplayName(HttpContext context)
        {
            // Get username from claims
            var username = _httpContextAccessor.HttpContext!.User?.Identity?.Name;
            
            // If we don't have a username from claims, return null
            return username;
        }

        public bool IsAuthenticated(HttpContext context)
        {
            // Check if user is in 2FA verification process
            var twoFactorUsername = _httpContextAccessor.HttpContext!.Session.GetString("TwoFactorUsername");
            if (!string.IsNullOrEmpty(twoFactorUsername))
            {
                // User is in 2FA process, not fully authenticated yet
                return false;
            }

            // Check if the user is authenticated through the framework
            return _httpContextAccessor.HttpContext!.User?.Identity?.IsAuthenticated ?? false;
        }

        public bool IsCurrentUserAdmin(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public bool IsCurrentUserSelfManager(HttpContext context)
        {
            throw new NotImplementedException();
        }

        public async Task StartUserSessionAsync(
            HttpContext context
            , string authenticationSchema
            , string username
            , bool rememberMe = false)
        {
            throw new NotImplementedException();
        }

        public void SignOut(HttpContext context)
        {
            var username = GetCurrentUserDisplayName(context);
            context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).Wait();

            if (!string.IsNullOrEmpty(username))
            {
                _logger.LogInformation("User '{Username}' signed out", username);
            }
        }
        
        // TODO: move this into UserService
        public bool ValidateCredentials(string username, string password)
        {
            throw new NotImplementedException();
        }
    }
}