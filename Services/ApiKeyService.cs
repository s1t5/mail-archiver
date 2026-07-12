using System.Security.Cryptography;
using System.Text;
using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services
{
    public class ApiKeyService : IApiKeyService
    {
        // Key format: "ma_" + base64url(32 random bytes) = 256 bits of entropy.
        public const string KeyPrefixMarker = "ma_";
        public const int RandomByteCount = 32;
        // Characters of the full key retained in plaintext for identification.
        public const int StoredPrefixLength = 11;

        private readonly MailArchiverDbContext _context;

        public ApiKeyService(MailArchiverDbContext context)
        {
            _context = context;
        }

        // ---- Pure, testable helpers -------------------------------------------------

        /// <summary>Generates a new plaintext key: "ma_" + 43 url-safe base64 chars.</summary>
        public static string GenerateKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(RandomByteCount);
            var body = Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
            return KeyPrefixMarker + body;
        }

        /// <summary>Hex-encoded SHA-256 of the full key (lowercase).</summary>
        public static string ComputeHash(string plaintextKey)
        {
            var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plaintextKey));
            return Convert.ToHexStringLower(hash);
        }

        /// <summary>True when the key and its owner are both currently usable.</summary>
        public static bool IsUsable(ApiKey key, User user) =>
            key.IsActive && user.IsActive;

        // ---- Service operations (implemented by the delegated TDD slice) ------------

        public async Task<(ApiKey Entity, string PlaintextKey)> CreateAsync(int userId, string name, DateTime? expiresAt)
        {
            var plaintextKey = GenerateKey();
            var entity = new ApiKey
            {
                KeyHash = ComputeHash(plaintextKey),
                KeyPrefix = plaintextKey.Substring(0, StoredPrefixLength),
                Name = name,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt
            };

            _context.ApiKeys.Add(entity);
            await _context.SaveChangesAsync();

            return (entity, plaintextKey);
        }

        public async Task<User?> ValidateAsync(string plaintextKey)
        {
            if (string.IsNullOrEmpty(plaintextKey) || !plaintextKey.StartsWith(KeyPrefixMarker))
            {
                return null;
            }

            var hash = ComputeHash(plaintextKey);
            var key = await _context.ApiKeys
                .Include(k => k.User)
                .FirstOrDefaultAsync(k => k.KeyHash == hash);

            if (key == null)
            {
                return null;
            }

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(key.KeyHash),
                    Encoding.UTF8.GetBytes(hash)))
            {
                return null;
            }

            if (!IsUsable(key, key.User))
            {
                return null;
            }

            var now = DateTime.UtcNow;
            if (key.LastUsedAt == null || key.LastUsedAt < now.AddMinutes(-5))
            {
                key.LastUsedAt = now;
                await _context.SaveChangesAsync();
            }

            return key.User;
        }

        public async Task<bool> RevokeAsync(int keyId, int requestingUserId, bool isAdmin)
        {
            var key = await _context.ApiKeys.FindAsync(keyId);

            if (key == null)
            {
                return false;
            }

            if (!isAdmin && key.UserId != requestingUserId)
            {
                return false;
            }

            if (key.RevokedAt == null)
            {
                key.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public Task<List<ApiKey>> GetKeysForUserAsync(int userId)
            => _context.ApiKeys
                .Where(k => k.UserId == userId)
                .OrderByDescending(k => k.CreatedAt)
                .ToListAsync();

        public Task<List<ApiKey>> GetAllKeysAsync()
            => _context.ApiKeys
                .Include(k => k.User)
                .OrderByDescending(k => k.CreatedAt)
                .ToListAsync();
    }
}
