namespace MailArchiver.Models.ViewModels
{
    /// <summary>
    /// Represents a node in the folder tree hierarchy
    /// </summary>
    public class FolderTreeNode
    {
        /// <summary>
        /// Display name of the folder (last part of the path)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full path of the folder (e.g., "INBOX/Work/2024")
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Number of emails in this folder
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Number of unread emails in this folder (if tracking is enabled)
        /// </summary>
        public int UnreadCount { get; set; }

        /// <summary>
        /// Nesting level (0 for root folders)
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Whether this folder has children
        /// </summary>
        public bool HasChildren => Children != null && Children.Any();

        /// <summary>
        /// Child folders
        /// </summary>
        public List<FolderTreeNode> Children { get; set; } = new List<FolderTreeNode>();

        /// <summary>
        /// Whether this folder is currently selected
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Whether this folder's children are expanded
        /// </summary>
        public bool IsExpanded { get; set; }

    }
}
