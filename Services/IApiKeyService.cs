using MailArchiver.Models;

namespace MailArchiver.Services
{
    /// <summary>
    /// Lifecycle and validation for per-user API keys (read-only REST API).
    /// </summary>
    public interface IApiKeyService
    {
        /// <summary>
        /// Creates a new key for the user. Returns the persisted entity and the
        /// plaintext key — the plaintext is shown to the user once and is never
        /// recoverable afterwards.
        /// </summary>
        Task<(ApiKey Entity, string PlaintextKey)> CreateAsync(int userId, string name, DateTime? expiresAt);

        /// <summary>
        /// Validates a presented plaintext key. Returns the owning <see cref="User"/>
        /// when the key is active (not revoked, not expired) and the user is active;
        /// otherwise null. Refreshes LastUsedAt at most once per ~5 minutes.
        /// </summary>
        Task<User?> ValidateAsync(string plaintextKey);

        /// <summary>
        /// Revokes a key. A non-admin may only revoke their own keys. Returns false
        /// if the key does not exist or the caller is not permitted to revoke it.
        /// </summary>
        Task<bool> RevokeAsync(int keyId, int requestingUserId, bool isAdmin);

        /// <summary>All keys owned by a user, newest first.</summary>
        Task<List<ApiKey>> GetKeysForUserAsync(int userId);

        /// <summary>All keys across all users (admin oversight), newest first.</summary>
        Task<List<ApiKey>> GetAllKeysAsync();
    }
}
