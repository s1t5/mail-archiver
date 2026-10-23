namespace MailArchiver.Models
{
    public enum ProviderType
    {
        IMAP,
        M365, // Microsoft 365 (Exchange Online, client credentials)
        IMPORT,
        MSA   // Microsoft personal account (Outlook.com, OAuth2 auth code flow)
    }
}
