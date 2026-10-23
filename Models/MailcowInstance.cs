namespace MailArchiver.Models;

public class MailcowInstance
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ImapHost { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
}

// Kept after an archive account is deleted so its remote credential can still be revoked.
public class MailcowMailbox
{
    public int Id { get; set; }
    public int InstanceId { get; set; }
    public MailcowInstance Instance { get; set; } = null!;
    public string EmailAddress { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string? AppPasswordId { get; set; }
    public int? MailAccountId { get; set; }
    public MailAccount? MailAccount { get; set; }
    public bool IsRevoked { get; set; }
}
