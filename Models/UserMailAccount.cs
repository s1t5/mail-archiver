using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models
{
    public class UserMailAccount
    {
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int MailAccountId { get; set; }
        
        // Navigation properties
        public virtual User User { get; set; }
        public virtual MailAccount MailAccount { get; set; }
    }
}
