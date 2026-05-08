using MailArchiver.Services.Shared;
using MimeKit;

namespace MailArchiver.Services.Providers.Eml
{
    /// <summary>
    /// Pre-cleans MimeMessage objects from EML/MBOX/IMAP imports to remove null bytes
    /// from text header fields before database storage.
    /// All text cleaning and truncation is delegated to <see cref="MailContentHelper"/>.
    /// </summary>
    public class EmlMailCleaner
    {
        private readonly ILogger<EmlMailCleaner> _logger;

        public EmlMailCleaner(ILogger<EmlMailCleaner> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Pre-cleans a MimeMessage to remove null bytes from all text header fields.
        /// Modifies the message in place. 
        /// Uses <see cref="MailContentHelper.RemoveNullBytes"/> for null-byte removal.
        /// </summary>
        public void PreCleanMessage(MimeMessage message)
        {
            try
            {
                if (!string.IsNullOrEmpty(message.Subject))
                {
                    message.Subject = MailContentHelper.RemoveNullBytes(message.Subject) ?? "";
                }

                CleanAddressNames(message.From);
                CleanAddressNames(message.To);
                CleanAddressNames(message.Cc);
                CleanAddressNames(message.Bcc);

                _logger.LogDebug("Pre-cleaned message to remove null bytes: Subject='{Subject}', MessageId='{MessageId}'",
                    message.Subject, message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during message pre-cleaning: {Message}", ex.Message);
            }
        }

        private static void CleanAddressNames(InternetAddressList? addresses)
        {
            if (addresses == null) return;

            foreach (var address in addresses)
            {
                if (address is MailboxAddress mailboxAddress)
                {
                    mailboxAddress.Name = MailContentHelper.RemoveNullBytes(mailboxAddress.Name);
                }
            }
        }
    }
}