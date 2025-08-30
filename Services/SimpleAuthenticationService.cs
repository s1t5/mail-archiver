using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MailArchiver.Services
{
    public class SimpleAuthenticationService : IAuthenticationService
    {
        private readonly AuthenticationOptions _authOptions;
        private readonly IUserService _userService;
        private readonly MailArchiverDbContext _dbContext;
        private readonly ILogger<SimpleAuthenticationService> _logger;

        public SimpleAuthenticationService(
            IOptions<AuthenticationOptions> authOptions, 
            IUserService userService,
            MailArchiverDbContext dbContext,
            ILogger<SimpleAuthenticationService> logger)
        {
            _authOptions = authOptions.Value;
            _userService = userService;
            _dbContext = dbContext;
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

            // First try the legacy admin user
            var isValid = string.Equals(username, _authOptions.Username, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(password, _authOptions.Password, StringComparison.Ordinal);

            if (isValid)
                return true;

            // Then try the new user system
            return _userService.AuthenticateUserAsync(username, password).Result;
        }

        public void SignIn(HttpContext context, string username, bool rememberMe = false)
        {
            if (!_authOptions.Enabled)
                return;

            var token = GenerateSecureToken();
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddMinutes(rememberMe ? 10080 : _authOptions.SessionTimeoutMinutes) // 7 days or normal timeout
            };

            context.Response.Cookies.Append(_authOptions.CookieName, token, cookieOptions);
            
            // Store username in session for display purposes
            context.Session.SetString("Username", username);
            context.Session.SetString("LoginTime", DateTimeOffset.UtcNow.ToString());
            
            // Store token-to-username mapping in database
            StoreTokenMapping(token, username, rememberMe);

            _logger.LogInformation("User '{Username}' signed in successfully", username);
        }

        public void SignOut(HttpContext context)
        {
            if (!_authOptions.Enabled)
                return;

            // Get the token from the cookie
            var token = context.Request.Cookies[_authOptions.CookieName];
            
            // Remove the session from the database if token exists
            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    var userSession = _dbContext.UserSessions
                        .Where(us => us.Token == token)
                        .FirstOrDefault();
                    
                    if (userSession != null)
                    {
                        _dbContext.UserSessions.Remove(userSession);
                        _dbContext.SaveChanges();
                        _logger.LogInformation("User session removed from database for token");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing user session from database");
                }
            }

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
            var isValid = ValidateToken(token);
            
            // If token is valid but session data is missing, try to restore it
            if (isValid && string.IsNullOrEmpty(context.Session.GetString("Username")))
            {
                RestoreSessionFromToken(context, token);
            }
            
            return isValid;
        }

        public string GetCurrentUser(HttpContext context)
        {
            if (!_authOptions.Enabled)
                return "System";

            var username = context.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                // Try to restore from cookie if session is lost
                var token = context.Request.Cookies[_authOptions.CookieName];
                if (!string.IsNullOrEmpty(token) && ValidateToken(token))
                {
                    RestoreSessionFromToken(context, token);
                    username = context.Session.GetString("Username");
                }
            }
            
            return username ?? "Unknown";
        }

        public bool IsCurrentUserAdmin(HttpContext context)
        {
            if (!_authOptions.Enabled)
            {
                _logger.LogDebug("Authentication not enabled, returning admin");
                return true;
            }

            var username = GetCurrentUser(context);
            _logger.LogDebug("Checking if user '{Username}' is admin", username);
            
            // Check if it's the legacy admin user from appsettings
            if (string.Equals(username, _authOptions.Username, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("User '{Username}' is legacy admin user", username);
                return true;
            }

            // Check if it's a database user with admin privileges
            var user = _userService.GetUserByUsernameAsync(username).Result;
            var isAdmin = user?.IsAdmin ?? false;
            _logger.LogDebug("User '{Username}' database admin status: {IsAdmin}", username, isAdmin);
            return isAdmin;
        }

        public bool IsCurrentUserSelfManager(HttpContext context)
        {
            if (!_authOptions.Enabled)
            {
                _logger.LogDebug("Authentication not enabled, returning false for self-manager check");
                return false;
            }

            var username = GetCurrentUser(context);
            _logger.LogDebug("Checking if user '{Username}' is self-manager", username);

            // Legacy admin user is not considered a self-manager
            if (string.Equals(username, _authOptions.Username, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("User '{Username}' is legacy admin user, not self-manager", username);
                return false;
            }

            // Check if it's a database user with self-manager privileges
            var user = _userService.GetUserByUsernameAsync(username).Result;
            var isSelfManager = user?.IsSelfManager ?? false;
            _logger.LogDebug("User '{Username}' database self-manager status: {IsSelfManager}", username, isSelfManager);
            return isSelfManager;
        }

        private string GenerateSecureToken()
        {
            // Generate a random token
            var data = $"{Guid.NewGuid()}:{DateTimeOffset.UtcNow:yyyy-MM-dd-HH-mm}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            // Encode both the original data and hash for verification
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(data)) + "." + Convert.ToBase64String(hash);
        }


        private void StoreTokenMapping(string token, string username, bool rememberMe = false)
        {
            try
            {
                // Clean up expired sessions first
                CleanupExpiredSessions();
                
                var userSession = new UserSession
                {
                    Token = token,
                    Username = username,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(rememberMe ? 10080 : _authOptions.SessionTimeoutMinutes) // 7 days or normal timeout
                };
                
                _dbContext.UserSessions.Add(userSession);
                _dbContext.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing token mapping for user '{Username}'", username);
            }
        }
        
        private string GetUsernameFromToken(string token)
        {
            try
            {
                var userSession = _dbContext.UserSessions
                    .Where(us => us.Token == token && (us.ExpiresAt == null || us.ExpiresAt > DateTime.UtcNow))
                    .FirstOrDefault();
                    
                return userSession?.Username;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving username from token");
                return null;
            }
        }

        private void RestoreSessionFromToken(HttpContext context, string token)
        {
            try
            {
                // Get username from token mapping in database
                var username = GetUsernameFromToken(token);
                if (!string.IsNullOrEmpty(username))
                {
                    context.Session.SetString("Username", username);
                    context.Session.SetString("LoginTime", DateTimeOffset.UtcNow.ToString());
                    _logger.LogInformation("Session restored for user '{Username}' from token mapping", username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring session from token");
            }
        }

        private void CleanupExpiredSessions()
        {
            try
            {
                var expiredSessions = _dbContext.UserSessions
                    .Where(us => us.ExpiresAt != null && us.ExpiresAt < DateTime.UtcNow)
                    .ToList();
                
                if (expiredSessions.Any())
                {
                    _dbContext.UserSessions.RemoveRange(expiredSessions);
                    _dbContext.SaveChanges();
                    _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired sessions");
            }
        }

        private bool ValidateToken(string token)
        {
            // Simple validation - token exists and is not corrupted
            try
            {
                // Split token into data and hash parts
                var parts = token.Split('.');
                if (parts.Length != 2)
                    return false;

                // Decode the data part
                var dataBytes = Convert.FromBase64String(parts[0]);
                var data = Encoding.UTF8.GetString(dataBytes);
                
                // Decode the hash part
                var hashBytes = Convert.FromBase64String(parts[1]);
                
                // Verify hash length (SHA256)
                if (hashBytes.Length != 32)
                    return false;
                
                // Recompute hash to verify integrity
                using var sha256 = SHA256.Create();
                var computedHash = sha256.ComputeHash(dataBytes);
                
                // Compare hashes
                return computedHash.SequenceEqual(hashBytes);
            }
            catch
            {
                return false;
            }
        }
    }
}
