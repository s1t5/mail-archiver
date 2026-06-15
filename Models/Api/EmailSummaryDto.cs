using MailArchiver.Models;

namespace MailArchiver.Models.Api;

public class EmailSummaryDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public DateTime SentDate { get; set; }
    public bool IsOutgoing { get; set; }
    public bool HasAttachments { get; set; }
    public string FolderName { get; set; } = string.Empty;

    public static EmailSummaryDto FromEntity(ArchivedEmail e)
    {
        return new EmailSummaryDto
        {
            Id = e.Id,
            AccountId = e.MailAccountId,
            Subject = e.Subject,
            From = e.From,
            To = e.To,
            SentDate = e.SentDate,
            IsOutgoing = e.IsOutgoing,
            HasAttachments = e.HasAttachments,
            FolderName = e.FolderName
        };
    }
}
