namespace MailArchiver.Models
{
    public class MailcowOptions
    {
        public const string Mailcow = "Mailcow";
        public int MaxSelectedMailboxes { get; set; } = 1000;
    }
}
