using MailArchiver.Models;

namespace MailArchiver.Models.ViewModels
{
    public class UserMailAccountViewModel
    {
        public User User { get; set; }
        public List<MailAccount> AllMailAccounts { get; set; }
        public List<int> AssignedAccountIds { get; set; }
        
        public UserMailAccountViewModel()
        {
            User = new User();
            AllMailAccounts = new List<MailAccount>();
            AssignedAccountIds = new List<int>();
        }
    }
}
