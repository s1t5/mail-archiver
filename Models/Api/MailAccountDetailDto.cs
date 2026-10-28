namespace MailArchiver.Models.Api;

/// <summary>
/// Full account representation for <c>GET /api/v1/accounts/{id}</c> and the
/// write endpoints. Credentials are never returned — only flags that say
/// whether a secret is stored.
/// </summary>
public class MailAccountDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime LastSync { get; set; }
    public DateTime? LastFullSync { get; set; }

    // IMAP settings
    public string? ImapServer { get; set; }
    public int? ImapPort { get; set; }
    public bool UseSsl { get; set; }
    public string? Username { get; set; }

    // OAuth settings (M365 / MSA)
    public string? ClientId { get; set; }
    public string? TenantId { get; set; }

    /// <summary>True when an IMAP password is stored. The value itself is never exposed.</summary>
    public bool HasPassword { get; set; }

    /// <summary>True when an OAuth client secret is stored. The value itself is never exposed.</summary>
    public bool HasClientSecret { get; set; }

    public List<string> ExcludedFolders { get; set; } = new();
    public int? DeleteAfterDays { get; set; }
    public int? LocalRetentionDays { get; set; }
    public int? SyncIntervalMinutes { get; set; }
    public int? FullSyncIntervalHours { get; set; }

    public static MailAccountDetailDto FromEntity(MailAccount a)
    {
        return new MailAccountDetailDto
        {
            Id = a.Id,
            Name = a.Name,
            EmailAddress = a.EmailAddress,
            Provider = a.Provider.ToString(),
            IsEnabled = a.IsEnabled,
            LastSync = a.LastSync,
            LastFullSync = a.LastFullSync,
            ImapServer = a.ImapServer,
            ImapPort = a.ImapPort,
            UseSsl = a.UseSSL,
            Username = a.Username,
            ClientId = a.ClientId,
            TenantId = a.TenantId,
            HasPassword = !string.IsNullOrEmpty(a.Password),
            HasClientSecret = !string.IsNullOrEmpty(a.ClientSecret),
            ExcludedFolders = a.ExcludedFoldersList,
            DeleteAfterDays = a.DeleteAfterDays,
            LocalRetentionDays = a.LocalRetentionDays,
            SyncIntervalMinutes = a.SyncIntervalMinutes,
            FullSyncIntervalHours = a.FullSyncIntervalHours
        };
    }
}
