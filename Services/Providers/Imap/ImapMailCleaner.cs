using MimeKit;

namespace MailArchiver.Services.Providers.Imap
{
    /// <summary>
    /// Pre-cleans MimeMessages retrieved via IMAP to remove null bytes and other invalid
    /// UTF-8 characters that would cause PostgreSQL encoding errors (0x00 is invalid in UTF-8).
    /// </summary>
    public class ImapMailCleaner
    {
        private readonly ILogger<ImapMailCleaner> _logger;

        public ImapMailCleaner(ILogger<ImapMailCleaner> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Pre-cleans a MimeMessage to remove null bytes and other invalid UTF-8 characters
        /// from all text header fields. Modifies the message in place.
        /// </summary>
        public void PreCleanMessage(MimeMessage message)
        {
            try
            {
                // Clean Subject
                if (!string.IsNullOrEmpty(message.Subject))
                {
                    message.Subject = RemoveNullBytes(message.Subject);
                }

                // Clean From addresses
                if (message.From != null)
                {
                    foreach (var address in message.From)
                    {
                        if (address is MailboxAddress mailboxAddress)
                        {
                            mailboxAddress.Name = RemoveNullBytes(mailboxAddress.Name);
                        }
                    }
                }

                // Clean To addresses
                if (message.To != null)
                {
                    foreach (var address in message.To)
                    {
                        if (address is MailboxAddress mailboxAddress)
                        {
                            mailboxAddress.Name = RemoveNullBytes(mailboxAddress.Name);
                        }
                    }
                }

                // Clean Cc addresses
                if (message.Cc != null)
                {
                    foreach (var address in message.Cc)
                    {
                        if (address is MailboxAddress mailboxAddress)
                        {
                            mailboxAddress.Name = RemoveNullBytes(mailboxAddress.Name);
                        }
                    }
                }

                // Clean Bcc addresses
                if (message.Bcc != null)
                {
                    foreach (var address in message.Bcc)
                    {
                        if (address is MailboxAddress mailboxAddress)
                        {
                            mailboxAddress.Name = RemoveNullBytes(mailboxAddress.Name);
                        }
                    }
                }

                _logger.LogDebug("Pre-cleaned message to remove null bytes: Subject='{Subject}', MessageId='{MessageId}'",
                    message.Subject, message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during message pre-cleaning: {Message}", ex.Message);
                // Continue even if cleaning fails - the archive process will handle it
            }
        }

        /// <summary>
        /// Removes null bytes (0x00) and other invalid UTF-8 control characters from a string.
        /// PostgreSQL does not allow null bytes in TEXT/VARCHAR columns.
        /// </summary>
        public static string RemoveNullBytes(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Check if there are any null bytes first (optimization)
            if (!input.Contains('\0'))
            {
                return input;
            }

            // Remove null bytes
            return input.Replace("\0", "");
        }
    }
}