using MailArchiver.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace MailArchiver.Services.Providers.Graph
{
    /// <summary>
    /// Interface for Microsoft Graph folder operations.
    /// </summary>
    public interface IGraphFolderService
    {
        /// <summary>
        /// Gets all mail folder paths for the specified account.
        /// </summary>
        Task<List<string>> GetMailFoldersAsync(MailAccount account);

        /// <summary>
        /// Retrieves all mail folders (including child folders) for a user.
        /// </summary>
        Task<List<MailFolder>> GetAllMailFoldersAsync(GraphServiceClient graphClient, string userPrincipalName);

        /// <summary>
        /// Builds a dictionary mapping folder IDs to their full hierarchical path.
        /// </summary>
        Dictionary<string, string> BuildFolderPathDictionary(List<MailFolder> folders);

        /// <summary>
        /// Resolves and optionally creates an entire folder hierarchy in an M365 mailbox.
        /// </summary>
        Task<MailFolder?> EnsureFolderPathAsync(
            GraphServiceClient graphClient,
            string userPrincipalName,
            string folderPath,
            List<MailFolder> existingFolders,
            bool createIfMissing);

        /// <summary>
        /// Returns the mailbox Inbox folder using the Graph well-known name "inbox".
        /// </summary>
        Task<MailFolder?> GetWellKnownInboxAsync(
            GraphServiceClient graphClient,
            string userPrincipalName,
            List<MailFolder> existingFolders);

        /// <summary>
        /// Determines if a folder is an outgoing (sent) folder.
        /// </summary>
        bool IsOutgoingFolder(MailFolder folder);

        /// <summary>
        /// Checks if a folder name indicates a drafts folder.
        /// </summary>
        bool IsDraftsFolder(string? folderName);

        /// <summary>
        /// Checks if a folder name indicates outgoing mail based on its name in multiple languages.
        /// </summary>
        bool IsOutgoingFolderByName(string? folderName);
    }
}