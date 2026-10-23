using MailArchiver.Models;

namespace MailArchiver.Models.ViewModels
{
    public class ApiKeyViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public bool IsActive { get; set; }
        public string? OwnerUsername { get; set; }

        public static ApiKeyViewModel FromEntity(ApiKey k, bool includeOwner)
        {
            return new ApiKeyViewModel
            {
                Id = k.Id,
                Name = k.Name,
                KeyPrefix = k.KeyPrefix,
                CreatedAt = k.CreatedAt,
                LastUsedAt = k.LastUsedAt,
                ExpiresAt = k.ExpiresAt,
                RevokedAt = k.RevokedAt,
                IsActive = k.IsActive,
                OwnerUsername = includeOwner ? k.User?.Username : null
            };
        }
    }
}
