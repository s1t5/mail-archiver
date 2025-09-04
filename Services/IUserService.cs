using MailArchiver.Models;

namespace MailArchiver.Services
{
    public interface IUserService
    {
        Task<User?> GetUserByIdAsync(int id);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByEmailAsync(string email);
        Task<List<User>> GetAllUsersAsync();
        Task<User> CreateUserAsync(string username, string email, string password, bool isAdmin = false);
        Task<bool> UpdateUserAsync(User user);
        Task<bool> DeleteUserAsync(int id);
        Task<bool> AuthenticateUserAsync(string username, string password);
        Task<bool> SetUserActiveStatusAsync(int id, bool isActive);
        Task<List<MailAccount>> GetUserMailAccountsAsync(int userId);
        Task<bool> AssignMailAccountToUserAsync(int userId, int mailAccountId);
        Task<bool> RemoveMailAccountFromUserAsync(int userId, int mailAccountId);
        Task<bool> IsUserAdminAsync(int userId);
        Task<bool> IsUserAuthorizedForAccountAsync(int userId, int mailAccountId);
        Task<int> GetAdminCountAsync();
        string HashPassword(string password);

        // Two-Factor Authentication methods
        Task<bool> SetTwoFactorEnabledAsync(int userId, bool enabled);
        Task<bool> SetTwoFactorSecretAsync(int userId, string secret);
        Task<bool> SetTwoFactorBackupCodesAsync(int userId, string backupCodes);
        Task<string?> GetTwoFactorSecretAsync(int userId);
        Task<bool> VerifyTwoFactorBackupCodeAsync(int userId, string backupCode);
        Task<bool> RemoveUsedBackupCodeAsync(int userId, string usedCode);
    }
}
