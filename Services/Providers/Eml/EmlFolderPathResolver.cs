namespace MailArchiver.Services.Providers.Eml
{
    /// <summary>
    /// Derives the archive folder path for a .eml entry inside a ZIP import from the
    /// entry's full path. The complete directory hierarchy is preserved (e.g.
    /// "Test Folder/Lectures/_AD 2006") instead of collapsing every level into flat
    /// top-level folders. The archive side already treats FolderName as a hierarchical
    /// path: the folder tree builds nested nodes from "/" separators and folder
    /// filtering matches descendants via path-prefix boundaries.
    /// </summary>
    public static class EmlFolderPathResolver
    {
        /// <summary>Fallback folder when the entry has no directory component.</summary>
        public const string DefaultFolder = "INBOX";

        // The folder tree validation rejects folder names longer than 500 characters
        // (and any containing ".."), so cap the resolved path below that limit.
        private const int MaxFolderPathLength = 490;

        private static readonly char[] Separators = { '/', '\\' };

        /// <summary>
        /// Resolves the target folder path for a ZIP entry path. The directory part of
        /// the entry path becomes the folder path, normalized to "/" separators.
        /// Entries in the ZIP root resolve to <see cref="DefaultFolder"/>.
        /// </summary>
        public static string Resolve(string? entryFullName)
        {
            var path = entryFullName?.Replace('\\', '/') ?? string.Empty;

            // Cut off the file name: last segment after the final slash. A trailing
            // slash (directory entry) yields an empty last part and is kept whole.
            var lastSlash = path.LastIndexOf('/');
            var directory = lastSlash >= 0 ? path[..lastSlash] : string.Empty;

            var segments = directory
                .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0 && s != "." && s != "..")
                .ToList();

            if (segments.Count == 0) return DefaultFolder;

            var joined = string.Join("/", segments);
            if (joined.Length <= MaxFolderPathLength) return joined;

            // Keep the deepest segments — the leaf carries the mail; the tail (root
            // levels) is truncated until the path fits.
            while (segments.Count > 1)
            {
                segments.RemoveAt(0);
                joined = string.Join("/", segments);
                if (joined.Length <= MaxFolderPathLength) return joined;
            }

            return joined[..MaxFolderPathLength];
        }
    }
}