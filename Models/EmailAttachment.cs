namespace MailArchiver.Models
{
    public class EmailAttachment
    {
        public int Id { get; set; }
        public int ArchivedEmailId { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public string? ContentId { get; set; }
        public byte[] Content { get; set; }
        public long Size { get; set; }
        
        public virtual ArchivedEmail ArchivedEmail { get; set; }
    }
}
