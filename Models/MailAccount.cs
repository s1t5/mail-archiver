using MailArchiver.Models;

public class MailAccount
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string EmailAddress { get; set; }
    public string ImapServer { get; set; }
    public int ImapPort { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public bool UseSSL { get; set; }
    public DateTime LastSync { get; set; }
    public bool IsEnabled { get; set; } = true;
    public virtual ICollection<ArchivedEmail> ArchivedEmails { get; set; } = new List<ArchivedEmail>();
}