using MailArchiver.Models;
using System.ComponentModel.DataAnnotations.Schema;

public class MailAccount
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string EmailAddress { get; set; }
    public string? ImapServer { get; set; }
    public int? ImapPort { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSSL { get; set; }
    public DateTime LastSync { get; set; }
    public bool IsEnabled { get; set; } = true;
    
    
    // Folder exclusion functionality
    public string ExcludedFolders { get; set; } = string.Empty;
    
    // Email deletion functionality
    public int? DeleteAfterDays { get; set; }
    
    // Local archive retention functionality
    public int? LocalRetentionDays { get; set; }
    
    // Provider field for account type
    public ProviderType Provider { get; set; } = ProviderType.IMAP;
    
    // Microsoft 365 OAuth2 fields (M365 and MSA)
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? TenantId { get; set; }

    // MSA OAuth2 token storage (personal Microsoft accounts)
    public string? OAuthRefreshToken { get; set; }
    public string? OAuthAccessToken { get; set; }
    public DateTime? OAuthTokenExpiry { get; set; }

    // Per-account sync scheduling
    public int? SyncIntervalMinutes { get; set; }
    public int? FullSyncIntervalHours { get; set; }
    public DateTime? LastFullSync { get; set; }
    
    [NotMapped]
    public List<string> ExcludedFoldersList
    {
        get
        {
            return string.IsNullOrEmpty(ExcludedFolders) 
                ? new List<string>() 
                : ExcludedFolders.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .Where(f => f.Length > 0)
                    .ToList();
        }
    }
    
    public virtual ICollection<ArchivedEmail> ArchivedEmails { get; set; } = new List<ArchivedEmail>();
    
    // Navigation properties for multi-user functionality
    public virtual ICollection<UserMailAccount> UserMailAccounts { get; set; } = new List<UserMailAccount>();
}
