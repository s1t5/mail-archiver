using MailArchiver.Models;

namespace MailArchiver.Services
{
    /// <summary>
    /// Lifecycle and validation for per-user API keys (REST API).
    /// </summary>
    public interface IApiKeyService
    {
        /// <summary>
        /// Creates a new key for the user. Returns the persisted entity and the
        /// plaintext key — the plaintext is shown to the user once and is never
        /// recoverable afterwards. <paramref name="scopes"/> defaults to
        /// <c>read</c>, so a key created without an explicit scope stays
        /// read-only.
        /// </summary>
        Task<(ApiKey Entity, string PlaintextKey)> CreateAsync(
            int userId, string name, DateTime? expiresAt, IEnumerable<string>? scopes = null);

        /// <summary>
        /// Validates a presented plaintext key. Returns the owning <see cref="User"/>
        /// when the key is active (not revoked, not expired) and the user is active;
        /// otherwise null. Refreshes LastUsedAt at most once per ~5 minutes.
        /// </summary>
        Task<User?> ValidateAsync(string plaintextKey);

        /// <summary>
        /// Same validation as <see cref="ValidateAsync"/>, but also returns the key
        /// itself so the authentication handler can read its scopes.
        /// </summary>
        Task<ApiKeyValidation?> ValidateKeyAsync(string plaintextKey);


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

    /// <summary>
    /// Result of a successful API-key validation: the owning user plus the key
    /// itself (whose <see cref="ApiKey.Scopes"/> the caller needs).
    /// </summary>
    public sealed record ApiKeyValidation(User User, ApiKey Key);
}
