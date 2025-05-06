namespace MailArchiver.Models.ViewModels
{
    public class EmailDetailViewModel
    {
        public ArchivedEmail Email { get; set; }
        public string AccountName { get; set; }
        public string FormattedHtmlBody { get; set; } // Bereinigtes HTML für sichere Darstellung
    }
}