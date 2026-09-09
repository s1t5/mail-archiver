using MailArchiver.Models;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Decides, for the exclusion editor's folder picker, which folders the installation-wide list
    /// already covers. Separate from the endpoint that serves them so the rule can be tested without
    /// a provider connection, and so the two decisions it encodes stay explicit.
    ///
    /// First: the account's own list is deliberately not consulted. The picker is where that list is
    /// being edited, so a folder the user just ticked would otherwise come back struck through and
    /// unselectable, and there would be no way to untick it. Only the global list is out of reach
    /// from this page, so only the global list may disable a choice.
    ///
    /// Second: the name comes from the provider rather than from cutting the path at its last
    /// separator. The matcher compares the folder's own name as well as its path, and it does so
    /// with an unanchored suffix rule, so a global entry "Kontakte" also covers "Vorgeschlagene
    /// Kontakte". Guessing the name from a delimiter the server chooses would get exactly those
    /// cases wrong.
    /// </summary>
    public static class FolderExclusionMarking
    {
        public static void MarkGloballyExcluded(
            IEnumerable<MailFolderInfo> folders,
            IEnumerable<string>? globalExclusions)
        {
            if (folders == null)
                return;

            foreach (var folder in folders)
            {
                folder.GloballyExcluded = FolderExclusionMatcher.IsExcluded(
                    folder.FullName, folder.Name, null, globalExclusions);
            }
        }
    }
}
