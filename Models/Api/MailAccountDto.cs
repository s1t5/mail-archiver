namespace MailArchiver.Models.Api;

public class MailAccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime LastSync { get; set; }

    public static MailAccountDto FromEntity(MailAccount a)
    {
        return new MailAccountDto
        {
            Id = a.Id,
            Name = a.Name,
            EmailAddress = a.EmailAddress,
            Provider = a.Provider.ToString(),
            IsEnabled = a.IsEnabled,
            LastSync = a.LastSync
        };
    }
}
