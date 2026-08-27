// Services/Shared/FolderMapper.cs
namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Turns a stored source folder path into the folder an offload should append into.
    /// <para>
    /// The order of the three steps is not interchangeable. Exclusion is matched against the
    /// source name, because an exclusion on "Deleted Items" would never fire if the rename to
    /// "Trash" had already happened. Only then is the rename applied, and only then is the
    /// target path built.
    /// </para>
    /// <para>
    /// <see cref="ArchivedEmail.FolderName"/> holds the full source path, so the rename is a
    /// rewrite of the longest matching path prefix along segment boundaries rather than a match
    /// on the whole string: renaming "Sent Items" to "Sent" also has to turn "Sent Items/2019"
    /// into "Sent/2019", while leaving "Sent Items Archive" alone.
    /// </para>
    /// </summary>
    public static class FolderMapper
    {
        // The existing folder creation in ImapMailRestorer splits on both separators, so the
        // same set is used here to decide where a segment boundary is.
        private static readonly char[] Separators = { '/', '\\' };

        /// <summary>
        /// Whether the source folder is excluded, either by an exact match or by being a
        /// descendant of an excluded folder. Case insensitive. Evaluated before any rename.
        /// </summary>
        public static bool IsExcluded(string? sourcePath, IEnumerable<string>? excludedFolders)
        {
            if (excludedFolders == null) return false;

            var segments = Split(sourcePath);
            if (segments.Count == 0) return false;

            foreach (var excluded in excludedFolders)
            {
                var excludedSegments = Split(excluded);
                if (excludedSegments.Count == 0) continue;
                if (StartsWithSegments(segments, excludedSegments)) return true;
            }

            return false;
        }

        /// <summary>
        /// Rewrites the longest matching prefix of the source path using the map. Matching is
        /// case insensitive and happens on segment boundaries only.
        /// </summary>
        public static string ApplyRenameMap(string? sourcePath, IDictionary<string, string>? map)
        {
            var segments = Split(sourcePath);
            if (segments.Count == 0) return string.Empty;
            if (map == null || map.Count == 0) return string.Join("/", segments);

            List<string>? bestReplacement = null;
            var bestLength = 0;

            foreach (var entry in map)
            {
                var fromSegments = Split(entry.Key);
                if (fromSegments.Count == 0) continue;
                if (fromSegments.Count <= bestLength) continue;
                if (!StartsWithSegments(segments, fromSegments)) continue;

                bestLength = fromSegments.Count;
                bestReplacement = Split(entry.Value);
            }

            if (bestReplacement == null) return string.Join("/", segments);

            var rewritten = new List<string>(bestReplacement);
            rewritten.AddRange(segments.Skip(bestLength));
            return string.Join("/", rewritten);
        }

        /// <summary>
        /// Builds the target folder path, reproducing the rule the restore path already uses:
        /// when the target root is INBOX the renamed source path is used as it is, otherwise it
        /// is placed below the root. Without structure preservation everything lands in the root.
        /// </summary>
        public static string ResolveTargetPath(string? renamedPath, string? targetRoot, bool preserveStructure)
        {
            var root = string.IsNullOrWhiteSpace(targetRoot) ? "INBOX" : targetRoot.Trim();

            if (!preserveStructure) return root;

            var segments = Split(renamedPath);
            if (segments.Count == 0) return root;

            var renamed = string.Join("/", segments);

            return string.Equals(root, "INBOX", StringComparison.OrdinalIgnoreCase)
                ? renamed
                : $"{root}/{renamed}";
        }

        /// <summary>
        /// The full pipeline: exclusion, then rename, then target path. Returns null when the
        /// source folder is excluded.
        /// </summary>
        public static string? Resolve(
            string? sourcePath,
            string? targetRoot,
            bool preserveStructure,
            IEnumerable<string>? excludedFolders,
            IDictionary<string, string>? renameMap)
        {
            if (IsExcluded(sourcePath, excludedFolders)) return null;
            var renamed = ApplyRenameMap(sourcePath, renameMap);
            return ResolveTargetPath(renamed, targetRoot, preserveStructure);
        }

        private static List<string> Split(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return new List<string>();
            return path
                .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        private static bool StartsWithSegments(List<string> segments, List<string> prefix)
        {
            if (prefix.Count > segments.Count) return false;
            for (var i = 0; i < prefix.Count; i++)
            {
                if (!string.Equals(segments[i], prefix[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
    }
}
