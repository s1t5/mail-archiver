namespace MailArchiver.ViewModels
{
    public class MsaDeviceCodeViewModel
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string UserCode { get; set; } = string.Empty;
        public string VerificationUri { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public int PollIntervalSeconds { get; set; }
    }
}
