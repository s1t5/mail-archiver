using MailArchiver.Auth.Options;
using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MailArchiver.Services
{
    public class UserService : IUserService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IOptions<OAuthOptions> _oAuthOptions;

        public UserService(
            MailArchiverDbContext context, 
            ILogger<UserService> logger,
            IStringLocalizer<SharedResource> localizer,
            IOptions<OAuthOptions> oAuthOptions)
        {
            _context = context;
            _logger = logger;
            _localizer = localizer;
            _oAuthOptions = oAuthOptions;
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            if (string.IsNullOrEmpty(username))
                return null;
                
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
                return null;
                
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<User> GetOrCreateUserFromRemoteIdentity(
            ClaimsIdentity remoteIdentity)
        {
            var userId = remoteIdentity.Claims
                .Where(c => c.Type == ClaimTypes.NameIdentifier)
                .FirstOrDefault()?
                .Value;

            var email = remoteIdentity.Claims
                .Where(c => c.Type == ClaimTypes.Email)
                .FirstOrDefault()?
                .Value;

            var displayName = remoteIdentity.Claims
                .Where(c => c.Type == ClaimTypes.Name)
                .FirstOrDefault()?
                .Value;

            // Try to find existing user by remote identity ID (already linked)
            var user = await _context.Users
                .Where(u => u.OAuthRemoteUserId == userId) 
                .FirstOrDefaultAsync();

            if (user != null)
            {
                _logger.LogInformation("Found existing OIDC user by remote ID: {RemoteId}, Username: {Username}", userId, user.Username);
                return user;
            }

            // SECURITY: Do NOT automatically link accounts based on email
            // Check if email already exists - if so, user must manually link their account
            var existingEmailUser = await _context.Users
                .Where(u => u.Email.ToLower() == email.ToLower())
                .FirstOrDefaultAsync();

            if (existingEmailUser != null)
            {
                _logger.LogWarning("OIDC login attempted with email {Email} that already exists for user {Username}. Automatic linking is disabled for security. Administrator must remove or modify the existing local account before using OIDC authentication.", 
                    email, existingEmailUser.Username);
                throw new InvalidOperationException(_localizer["OidcAccountAlreadyExists"]);
            }

            // Check if user email is in the admin emails list
            bool isAdminEmail = false;
            bool autoApprove = false;
            
            if (_oAuthOptions.Value.AdminEmails != null && _oAuthOptions.Value.AdminEmails.Length > 0)
            {
                isAdminEmail = _oAuthOptions.Value.AdminEmails.Any(adminEmail => 
                    string.Equals(adminEmail, email, StringComparison.OrdinalIgnoreCase));
                
                if (isAdminEmail)
                {
                    autoApprove = true;
                    _logger.LogInformation("Email {Email} found in AdminEmails configuration - user will be provisioned as admin", email);
                }
            }
            
            // Auto-approve all OIDC users if configured (for OIDC-first deployments where IdP controls access)
            if (!autoApprove && _oAuthOptions.Value.AutoApproveUsers)
            {
                autoApprove = true;
                _logger.LogInformation("AutoApproveUsers is enabled - OIDC user {Email} will be auto-approved", email);
            }
            
            // Create a new user
            _logger.LogInformation("Creating new OIDC user: Email={Email}, DisplayName={DisplayName}, RemoteId={RemoteId}, IsAdmin={IsAdmin}", 
                email, displayName, userId, isAdminEmail);
            
            user = new User()
            {
                Username = $"{displayName}_{userId.Substring(0, 8)}", // Add unique suffix to prevent username collisions
                Email = email,
                PasswordHash = null, // OIDC users don't have passwords
                IsAdmin = isAdminEmail,
                IsActive = autoApprove, // Auto-approve admins
                RequiresApproval = !autoApprove, // Admin emails don't require approval
                OAuthRemoteUserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            
            if (autoApprove)
            {
                _logger.LogInformation("New OIDC user created and auto-approved (IsAdmin={IsAdmin}): {Username} (ID: {UserId}, Email: {Email})", 
                    isAdminEmail, user.Username, user.Id, user.Email);
            }
            else
            {
                _logger.LogWarning("New OIDC user created and requires approval: {Username} (ID: {UserId}, Email: {Email})", 
                    user.Username, user.Id, user.Email);
            }
            
            return user;
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<User> CreateUserAsync(string username, string email, string password, bool isAdmin = false)
        {
            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = HashPassword(password),
                IsAdmin = isAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new user: {Username} (ID: {UserId})", username, user.Id);

            return user;
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated user: {Username} (ID: {UserId})", user.Username, user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {Username} (ID: {UserId})", user.Username, user.Id);
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return false;

            // Warn when deleting self-manager users but allow it
            if (user.IsSelfManager)
            {
                _logger.LogWarning("Deleting self-manager user {Username} (ID: {UserId})", user.Username, user.Id);
            }

            // Remove user's mail account associations
            var userMailAccounts = await _context.UserMailAccounts
                .Where(uma => uma.UserId == id)
                .ToListAsync();

            _context.UserMailAccounts.RemoveRange(userMailAccounts);

            // Remove the user
            _context.Users.Remove(user);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted user: {Username} (ID: {UserId})", user.Username, user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {Username} (ID: {UserId})", user.Username, user.Id);
                return false;
            }
        }

        public async Task<bool> AuthenticateUserAsync(string username, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() && u.IsActive);

            if (user == null)
                return false;

            // SECURITY: OIDC users cannot login with username/password
            if (!string.IsNullOrEmpty(user.OAuthRemoteUserId))
            {
                _logger.LogWarning("OIDC user {Username} (ID: {UserId}) attempted to login with password - denied", 
                    user.Username, user.Id);
                return false;
            }

            // SECURITY: Users without password (OIDC users) cannot login with password
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                _logger.LogWarning("User {Username} (ID: {UserId}) has no password but attempted password login - denied", 
                    user.Username, user.Id);
                return false;
            }

            return VerifyPassword(password, user.PasswordHash);
        }

        public async Task<bool> SetUserActiveStatusAsync(int id, bool isActive)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return false;

            user.IsActive = isActive;
            
            // SECURITY: When activating an OIDC user, also clear the RequiresApproval flag
            if (isActive && user.RequiresApproval)
            {
                user.RequiresApproval = false;
                _logger.LogInformation("Clearing RequiresApproval flag for activated OIDC user {Username} (ID: {UserId})",
                    user.Username, user.Id);
            }
            
            _context.Users.Update(user);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Set user {Username} (ID: {UserId}) active status to {IsActive}",
                    user.Username, user.Id, isActive);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting user active status: {Username} (ID: {UserId})", user.Username, user.Id);
                return false;
            }
        }

        public async Task<List<MailAccount>> GetUserMailAccountsAsync(int userId)
        {
            return await _context.UserMailAccounts
                .Where(uma => uma.UserId == userId)
                .Include(uma => uma.MailAccount)
                .Select(uma => uma.MailAccount)
                .ToListAsync();
        }

        public async Task<bool> AssignMailAccountToUserAsync(int userId, int mailAccountId)
        {
            // Check if association already exists
            var existing = await _context.UserMailAccounts
                .FirstOrDefaultAsync(uma => uma.UserId == userId && uma.MailAccountId == mailAccountId);

            if (existing != null)
                return true; // Already assigned

            var userMailAccount = new UserMailAccount
            {
                UserId = userId,
                MailAccountId = mailAccountId
            };

            _context.UserMailAccounts.Add(userMailAccount);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Assigned mail account {MailAccountId} to user {UserId}", mailAccountId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning mail account {MailAccountId} to user {UserId}", mailAccountId, userId);
                return false;
            }
        }

        public async Task<bool> RemoveMailAccountFromUserAsync(int userId, int mailAccountId)
        {
            var userMailAccount = await _context.UserMailAccounts
                .FirstOrDefaultAsync(uma => uma.UserId == userId && uma.MailAccountId == mailAccountId);

            if (userMailAccount == null)
                return false;

            _context.UserMailAccounts.Remove(userMailAccount);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Removed mail account {MailAccountId} from user {UserId}", mailAccountId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing mail account {MailAccountId} from user {UserId}", mailAccountId, userId);
                return false;
            }
        }

        public async Task<bool> IsUserAdminAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user?.IsAdmin ?? false;
        }

        public async Task<bool> IsUserAuthorizedForAccountAsync(int userId, int mailAccountId)
        {
            // Admin users have access to all accounts
            var isAdmin = await IsUserAdminAsync(userId);
            if (isAdmin)
            {
                _logger.LogInformation("User {UserId} is admin, granting access to account {MailAccountId}", userId, mailAccountId);
                return true;
            }

            // Check if user is a self-manager
            var user = await _context.Users.FindAsync(userId);
            if (user?.IsSelfManager == true)
            {
                _logger.LogInformation("User {UserId} is self-manager, granting access to account {MailAccountId}", userId, mailAccountId);
                return true;
            }

            // Check if user has direct access to the account
            var hasDirectAccess = await _context.UserMailAccounts
                .AnyAsync(uma => uma.UserId == userId && uma.MailAccountId == mailAccountId);

            _logger.LogInformation("User {UserId} access check for account {MailAccountId}: {HasAccess}",
                userId, mailAccountId, hasDirectAccess ? "Granted" : "Denied");

            return hasDirectAccess;
        }

        public async Task<int> GetAdminCountAsync()
        {
            return await _context.Users.CountAsync(u => u.IsAdmin && u.IsActive);
        }

        public string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private string HashBackupCode(string backupCode)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(backupCode));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        #region Password Hashing

        private bool VerifyPassword(string password, string hash)
        {
            var hashedInput = HashPassword(password);
            return hashedInput == hash;
        }

        #endregion

        #region Two-Factor Authentication

        public async Task<bool> SetTwoFactorEnabledAsync(int userId, bool enabled)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            // SECURITY: OIDC users cannot use 2FA (they authenticate via OIDC provider)
            if (!string.IsNullOrEmpty(user.OAuthRemoteUserId))
            {
                _logger.LogWarning("Attempted to set 2FA for OIDC user {Username} (ID: {UserId}) - denied", 
                    user.Username, user.Id);
                return false;
            }

            user.IsTwoFactorEnabled = enabled;
            _context.Users.Update(user);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Set 2FA status for user {Username} (ID: {UserId}) to {Enabled}",
                    user.Username, user.Id, enabled);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting 2FA status for user: {Username} (ID: {UserId})", user.Username, user.Id);
                return false;
            }
        }

        public async Task<bool> SetTwoFactorSecretAsync(int userId, string secret)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            user.TwoFactorSecret = secret;
            _context.Users.Update(user);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Set 2FA secret for user {Username} (ID: {UserId})", user.Username, user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting 2FA secret for user: {Username} (ID: {UserId})", user.Username, user.Id);
                return false;
            }
        }

        public async Task<bool> SetTwoFactorBackupCodesAsync(int userId, string backupCodes)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Hash the backup codes before storing them
            var hashedCodes = string.Join(";", backupCodes.Split(';').Select(code => HashBackupCode(code)));
            user.TwoFactorBackupCodes = hashedCodes;
            _context.Users.Update(user);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Set 2FA backup codes for user {Username} (ID: {UserId})", user.Username, user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting 2FA backup codes for user: {Username} (ID: {UserId})", user.Username, user.Id);
                return false;
            }
        }

        public async Task<string?> GetTwoFactorSecretAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user?.TwoFactorSecret;
        }

        public async Task<bool> VerifyTwoFactorBackupCodeAsync(int userId, string backupCode)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user?.TwoFactorBackupCodes == null)
                return false;

            var backupCodes = user.TwoFactorBackupCodes.Split(';');
            var hashedInputCode = HashBackupCode(backupCode);
            // Use case-insensitive comparison for backup codes
            return backupCodes.Contains(hashedInputCode, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<bool> RemoveUsedBackupCodeAsync(int userId, string usedCode)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user?.TwoFactorBackupCodes == null)
                return false;

            var backupCodes = user.TwoFactorBackupCodes.Split(';').ToList();
            var hashedUsedCode = HashBackupCode(usedCode);
            if (backupCodes.Remove(hashedUsedCode))
            {
                user.TwoFactorBackupCodes = string.Join(";", backupCodes);
                _context.Users.Update(user);

                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Removed used backup code for user {Username} (ID: {UserId})", user.Username, user.Id);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing used backup code for user: {Username} (ID: {UserId})", user.Username, user.Id);
                    return false;
                }
            }

            return false;
        }

        #endregion
    }
}
