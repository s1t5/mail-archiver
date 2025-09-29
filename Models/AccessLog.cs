using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models
{
    public class AccessLog
    {
        public int Id { get; set; }
        
        [Required]
        public string Username { get; set; }
        
        [Required]
        public AccessLogType Type { get; set; }
        
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        // Optional email reference for actions related to specific emails
        public int? EmailId { get; set; }
        
        // Optional email subject for context
        public string? EmailSubject { get; set; }
        
        // Optional email sender for context
        public string? EmailFrom { get; set; }
        
        // Optional search parameters for search actions
        public string? SearchParameters { get; set; }
        
        // Optional email account for account-specific actions
        public int? MailAccountId { get; set; }
    }
    
    public enum AccessLogType
    {
        Login,
        Logout,
        Search,
        Open,
        Download,
        Restore,
        Account,
        Deletion
    }
}
