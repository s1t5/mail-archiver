using MailKit;
using MailKit.Net.Imap;

namespace MailArchiver.Services.Providers.Imap
{
    /// <summary>
    /// Interface for IMAP folder operations.
    /// </summary>
    public interface IImapFolderService
    {
        /// <summary>
        /// Retrieves all selectable folders from an IMAP account using a robust fallback strategy.
        /// </summary>
        Task<List<IMailFolder>> GetAllFoldersAsync(ImapClient client, string accountName);

        /// <summary>
        /// Determines if a folder is an outgoing (sent) folder based on its name and attributes.
        /// </summary>
        bool IsOutgoingFolder(IMailFolder folder);
    }
}