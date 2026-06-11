using MailArchiver.Models;

namespace MailArchiver.Models.Api;

public class AttachmentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }

    public static AttachmentDto FromEntity(EmailAttachment a)
    {
        return new AttachmentDto
        {
            Id = a.Id,
            FileName = a.FileName,
            ContentType = a.ContentType,
            Size = a.Size
        };
    }
}
