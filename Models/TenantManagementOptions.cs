namespace MailArchiver.Models
{
    public class TenantManagementOptions
    {
        public const string TenantManagement = "TenantManagement";
        public int MaxSelectedMailboxes { get; set; } = 1000;
    }
}
