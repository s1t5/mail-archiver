using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace MailArchiver.Services
{
    public class CookieAuthenticationService : IAuthenticationService
    {
        private readonly MailArchiver.Models.AuthenticationOptions _authOptions;
        private readonly IUserService _userService;
        private readonly MailArchiverDbContext _dbContext;
        private readonly ILogger<CookieAuthenticationService> _logger;

        public CookieAuthenticationService(
            IOptions<MailArchiver.Models.AuthenticationOptions> authOptions,
            IUserService userService,
            MailArchiverDbContext dbContext,
            ILogger<CookieAuthenticationService> logger)
        {
            _authOptions = authOptions.Value;
            _userService = userService;
            _dbContext = dbContext;
            _logger = logger;
        }

        public bool IsAuthenticationRequired()
        {
            return true; // Always require authentication
        }

        public bool ValidateCredentials(string username, string password)
        {
            // All authentication should go through the database user system
            return _userService.AuthenticateUserAsync(username, password).Result;
        }

        public void SignIn(HttpContext context, string username, bool rememberMe = false)
        {
            // Get the user to build claims
            var user = _userService.GetUserByUsernameAsync(username).Result;
            if (user == null)
            {
                _logger.LogWarning("Attempt to sign in non-existent user: {Username}", username);
                return;
            }

            // Create claims for the user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("UserId", user.Id.ToString())
            };

            // Add role claims
            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            if (user.IsSelfManager)
            {
                claims.Add(new Claim(ClaimTypes.Role, "SelfManager"));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            // Configure authentication properties
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddMinutes(_authOptions.SessionTimeoutMinutes),
                IssuedUtc = DateTimeOffset.UtcNow
            };

            // Sign in the user using ASP.NET Core authentication
            context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties).Wait();

            // Update user's last login time
            UpdateUserLastLogin(username);

            _logger.LogInformation("User '{Username}' signed in successfully", username);
        }

        public void SignOut(HttpContext context)
        {
            var username = GetCurrentUser(context);
            context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).Wait();

            if (!string.IsNullOrEmpty(username))
            {
                _logger.LogInformation("User '{Username}' signed out", username);
            }
        }

        public bool IsAuthenticated(HttpContext context)
        {
            // Check if user is in 2FA verification process
            var twoFactorUsername = context.Session.GetString("TwoFactorUsername");
            if (!string.IsNullOrEmpty(twoFactorUsername))
            {
                // User is in 2FA process, not fully authenticated yet
                return false;
            }

            // Check if the user is authenticated through the framework
            return context.User?.Identity?.IsAuthenticated ?? false;
        }

        public string GetCurrentUser(HttpContext context)
        {
            // Get username from claims
            var username = context.User?.Identity?.Name;
            
            // If we don't have a username from claims, return null
            return username;
        }

        public bool IsCurrentUserAdmin(HttpContext context)
        {
            var username = GetCurrentUser(context);
            _logger.LogDebug("Checking if user '{Username}' is admin", username);

            if (string.IsNullOrEmpty(username))
            {
                _logger.LogDebug("Username is null or empty, returning false for admin check");
                return false;
            }

            // Check if user has admin role in claims
            var isAdmin = context.User?.IsInRole("Admin") ?? false;
            _logger.LogDebug("User '{Username}' claims admin status: {IsAdmin}", username, isAdmin);
            return isAdmin;
        }

        public bool IsCurrentUserSelfManager(HttpContext context)
        {
            var username = GetCurrentUser(context);
            _logger.LogDebug("Checking if user '{Username}' is self-manager", username);

            if (string.IsNullOrEmpty(username))
            {
                _logger.LogDebug("Username is null or empty, returning false for self-manager check");
                return false;
            }

            // Check if user has self-manager role in claims
            var isSelfManager = context.User?.IsInRole("SelfManager") ?? false;
            _logger.LogDebug("User '{Username}' claims self-manager status: {IsSelfManager}", username, isSelfManager);
            return isSelfManager;
        }

        private void UpdateUserLastLogin(string username)
        {
            try
            {
                // Try to find the user in the new user system first
                var user = _userService.GetUserByUsernameAsync(username).Result;
                if (user != null)
                {
                    user.LastLoginAt = DateTime.UtcNow;
                    _userService.UpdateUserAsync(user).Wait();
                    _logger.LogInformation("Updated last login time for user '{Username}'", username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last login time for user '{Username}'", username);
            }
        }
    }
}
