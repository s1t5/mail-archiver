using MailArchiver.Models;

namespace MailArchiver.Models.Api;

public class EmailDetailDto
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
    public string Cc { get; set; } = string.Empty;
    public string Bcc { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }
    public List<AttachmentDto> Attachments { get; set; } = new();

    public static EmailDetailDto FromEntity(ArchivedEmail e)
    {
        return new EmailDetailDto
        {
            Id = e.Id,
            AccountId = e.MailAccountId,
            Subject = e.Subject,
            From = e.From,
            To = e.To,
            SentDate = e.SentDate,
            IsOutgoing = e.IsOutgoing,
            HasAttachments = e.HasAttachments,
            FolderName = e.FolderName,
            Cc = e.Cc,
            Bcc = e.Bcc,
            ReceivedDate = e.ReceivedDate,
            MessageId = e.MessageId,
            HtmlBody = e.OriginalBodyHtml != null
                ? System.Text.Encoding.UTF8.GetString(e.OriginalBodyHtml)
                : (!string.IsNullOrEmpty(e.BodyUntruncatedHtml) ? e.BodyUntruncatedHtml : e.HtmlBody),
            TextBody = e.OriginalBodyText != null
                ? System.Text.Encoding.UTF8.GetString(e.OriginalBodyText)
                : (!string.IsNullOrEmpty(e.BodyUntruncatedText) ? e.BodyUntruncatedText : e.Body),
            Attachments = e.Attachments.Select(AttachmentDto.FromEntity).ToList()
        };
    }
}
