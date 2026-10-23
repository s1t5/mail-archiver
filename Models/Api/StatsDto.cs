namespace MailArchiver.Models.Api;

public class StatsDto
{
    public int Emails { get; set; }
    public int Accounts { get; set; }
    public int Attachments { get; set; }
    public string DatabaseSizeInMB { get; set; } = string.Empty;
}