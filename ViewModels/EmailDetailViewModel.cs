namespace MailArchiver.Models.ViewModels
{
    public class EmailDetailViewModel
    {
        public ArchivedEmail Email { get; set; }
        public string AccountName { get; set; }
        public string FormattedHtmlBody { get; set; } // Bereinigtes HTML für sichere Darstellung
        
        /// <summary>
        /// Plain text version of the email body for privacy-focused viewing
        /// </summary>
        public string PlainTextBody { get; set; }
        
        /// <summary>
        /// Whether to default to plain text view (based on configuration)
        /// </summary>
        public bool DefaultToPlainText { get; set; }
        
        /// <summary>
        /// Whether to block external resources (remote images, CSS, etc.) in HTML view
        /// </summary>
        public bool BlockExternalResources { get; set; }
        
        /// <summary>
        /// Indicates if the email has HTML content available
        /// </summary>
        public bool HasHtmlBody { get; set; }
        
        /// <summary>
        /// Indicates if the email has plain text content available
        /// </summary>
        public bool HasPlainTextBody { get; set; }

        /// <summary>
        /// Whether a stored original exists for this email. Resolved through IRawMessageStore,
        /// so a filesystem-backed row whose file vanished counts as missing.
        /// </summary>
        public bool HasRawMessage { get; set; }

        /// <summary>
        /// Whether RawMessages is enabled. The details page only shows the missing-original
        /// notice when it is.
        /// </summary>
        public bool RawMessagesEnabled { get; set; }
    }
}