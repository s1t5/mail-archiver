namespace MailArchiver.Models
{
    public class ArchivedEmail
    {
        public int Id { get; set; }
        public int MailAccountId { get; set; }
        public string MessageId { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string HtmlBody { get; set; }
        public string? BodyUntruncatedText { get; set; }
        public string? BodyUntruncatedHtml { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Cc { get; set; }
        public string Bcc { get; set; }
        public DateTime SentDate { get; set; } = DateTime.UtcNow;
        public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
        public bool IsOutgoing { get; set; }
        public bool HasAttachments { get; set; }
        public string FolderName { get; set; }

        // Compliance fields for integrity and immutability
        public string? ContentHash { get; set; }
        public DateTime? HashCreatedAt { get; set; }
        public bool IsLocked { get; set; }

        public virtual MailAccount MailAccount { get; set; }
        public virtual ICollection<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
    }
}
