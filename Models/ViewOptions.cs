namespace MailArchiver.Models
{
    /// <summary>
    /// Configuration options for email viewing behavior.
    /// </summary>
    public class ViewOptions
    {
        /// <summary>
        /// When true, emails will default to plain text view to prevent loading
        /// tracking pixels and external resources. Users can still switch to HTML view.
        /// Default: false (show HTML by default for backward compatibility)
        /// </summary>
        public bool DefaultToPlainText { get; set; } = false;
        
        /// <summary>
        /// When true, external resources (remote images, external CSS, external scripts, etc.)
        /// will be blocked in HTML emails to prevent tracking and improve privacy.
        /// Only data: URIs and inline content will be allowed.
        /// Default: false (allow external resources for backward compatibility)
        /// </summary>
        public bool BlockExternalResources { get; set; } = false;
    }
}
