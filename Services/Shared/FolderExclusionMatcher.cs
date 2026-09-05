namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Decides whether a mail folder is excluded from synchronization, given the per-account
    /// exclusion list and the installation-wide one.
    ///
    /// The two lists are additive: a folder is excluded when it matches either. Neither list can
    /// re-include what the other excluded, which keeps the rule easy to reason about — adding an
    /// entry anywhere can only ever remove folders from the sync, never add them back.
    ///
    /// Both providers and both of their paths go through here — the IMAP sync, the Graph sync and
    /// the Graph retention deletion — so an entry means the same thing everywhere. Before this,
    /// IMAP had three matching rules while Graph compared exact strings inline, and Graph's
    /// deletion path looked only at the folder's own name, so an entry written as a path excluded a
    /// folder from the sync but not from the deletion.
    ///
    /// The matching rules are the ones the per-account list has always used, kept in one place so
    /// the sources cannot drift apart into different algorithms:
    ///
    /// <list type="number">
    /// <item>exact match against the folder's full path;</item>
    /// <item>exact match against the folder's own name, which catches an entry typed as the short
    /// name when the server reports a prefixed path;</item>
    /// <item>suffix match, which catches IMAP path separator variations such as "Drafts" against
    /// "INBOX.Drafts" or "INBOX/Drafts", and Gmail-style names such as "[Gmail]/Drafts".</item>
    /// </list>
    ///
    /// All comparisons are case-insensitive. This class is pure (no I/O, no static state) so it can
    /// be unit-tested in isolation.
    /// </summary>
    public static class FolderExclusionMatcher
    {
        /// <summary>
        /// True when the folder matches any entry of either exclusion list.
        /// </summary>
        /// <param name="folderFullName">The folder's full path as the server reports it.</param>
        /// <param name="folderName">The folder's own name, without the path.</param>
        /// <param name="accountExclusions">The account's own exclusion list. May be null or empty.</param>
        /// <param name="globalExclusions">
        /// The installation-wide list from <c>MailSync:GlobalExcludedFolders</c>. Empty by default,
        /// so an installation that never configures it behaves exactly as before.
        /// </param>
        public static bool IsExcluded(
            string? folderFullName,
            string? folderName,
            IEnumerable<string>? accountExclusions,
            IEnumerable<string>? globalExclusions)
        {
            var effective = CombineExclusions(accountExclusions, globalExclusions);
            if (effective.Count == 0)
                return false;

            var fullName = folderFullName ?? string.Empty;
            var name = folderName ?? string.Empty;

            // 1) Exact match against FullName (most common case)
            if (effective.Any(f => f.Equals(fullName, StringComparison.OrdinalIgnoreCase)))
                return true;

            // 2) Exact match against Name/DisplayName (catches cases where the user
            //    entered the short folder name but the server prefixes it)
            if (!string.IsNullOrEmpty(name) &&
                effective.Any(f => f.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return true;

            // 3) Suffix match: if the excluded entry matches the trailing part of FullName,
            //    this catches IMAP path prefix variations (e.g. "Drafts" matches "INBOX.Drafts"
            //    or "INBOX/Sent" matches "Sent")
            foreach (var excludedName in effective)
            {
                if (fullName.EndsWith("." + excludedName, StringComparison.OrdinalIgnoreCase) ||
                    fullName.EndsWith("/" + excludedName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Also check if Name ends with the excluded entry (handles Gmail-style "[Gmail]/Drafts")
                if (!string.IsNullOrEmpty(name) &&
                    name.EndsWith(excludedName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The union of both lists, trimmed, without blanks and without case-insensitive
        /// duplicates. Blank entries are dropped deliberately: an empty string left behind in
        /// configuration would otherwise match through the suffix rule and silently exclude
        /// folders nobody named.
        /// </summary>
        private static List<string> CombineExclusions(
            IEnumerable<string>? accountExclusions,
            IEnumerable<string>? globalExclusions)
        {
            var combined = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Add(accountExclusions);
            Add(globalExclusions);

            return combined;

            void Add(IEnumerable<string>? source)
            {
                if (source == null)
                    return;

                foreach (var entry in source)
                {
                    if (string.IsNullOrWhiteSpace(entry))
                        continue;

                    var trimmed = entry.Trim();
                    if (seen.Add(trimmed))
                        combined.Add(trimmed);
                }
            }
        }
    }
}
