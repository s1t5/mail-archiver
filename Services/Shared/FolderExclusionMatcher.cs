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
    /// "INBOX.Drafts" or "INBOX/Drafts", and Gmail-style names such as "[Gmail]/Drafts";</item>
    /// <item>an entry also covers what lies underneath the folder it names, unless
    /// <c>MailSync:ExcludeSubfolders</c> is switched off. "Kalender" therefore also covers
    /// "Kalender/KfW" and "INBOX.Kalender.KfW".</item>
    /// </list>
    ///
    /// The fourth rule anchors on the path separator, so it takes whole folders and never parts of
    /// a name: "Kalender" covers "Kalender/KfW" but not "SDA DO-Kalender Performance", which is an
    /// ordinary mail folder that happens to carry the word. Anchoring is what separates the two,
    /// and a substring rule would quietly take the second one out of the archive.
    ///
    /// Two properties of that rule are worth stating, because both are visible in real mailbox
    /// trees:
    ///
    /// <list type="bullet">
    /// <item>Ancestors are compared by path only, not by the unanchored name rule. A folder that is
    /// excluded solely because its own name ends with an entry does not pass that on to its
    /// children. The alternative would mean cutting an ancestor's name out of the path at a
    /// delimiter the server chooses, which is exactly the guessing
    /// <see cref="MailArchiver.Models.MailFolderInfo"/> avoids.</item>
    /// <item>"." counts as a separator wherever it appears, the same way rule 3 already treats it.
    /// On a server that delimits with "/", a top-level folder literally named "Kalender.ics" is
    /// therefore read as lying underneath "Kalender". The two cannot be told apart from the path
    /// alone, and for an exclusion list taking the folder is the safe direction.</item>
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
        /// <param name="excludeSubfolders">
        /// <c>MailSync:ExcludeSubfolders</c>. When true, an entry covers the folders underneath the
        /// one it names as well. Passed in rather than read here so this class stays free of
        /// configuration and every caller has to decide what it means.
        /// </param>
        public static bool IsExcluded(
            string? folderFullName,
            string? folderName,
            IEnumerable<string>? accountExclusions,
            IEnumerable<string>? globalExclusions,
            bool excludeSubfolders)
            => FindCoveringEntry(
                   folderFullName, folderName, accountExclusions, globalExclusions, excludeSubfolders)
               != null;

        /// <summary>
        /// The entry that excludes this folder, or null when none does. Same decision as
        /// <see cref="IsExcluded"/>, and it exists so the exclusion editor can say which entry is
        /// responsible instead of only that something is: with rule 4 the entry is often not the
        /// folder's own name, and "already excluded" on its own would leave the user hunting.
        ///
        /// Entries are tested one after the other, each against all four rules. The order differs
        /// from testing rule by rule across all entries, but the answer cannot: the decision is a
        /// disjunction over every (entry, rule) pair either way. Which entry is reported when
        /// several match is the list's own order, account entries first.
        /// </summary>
        public static string? FindCoveringEntry(
            string? folderFullName,
            string? folderName,
            IEnumerable<string>? accountExclusions,
            IEnumerable<string>? globalExclusions,
            bool excludeSubfolders)
        {
            var effective = CombineExclusions(accountExclusions, globalExclusions);
            if (effective.Count == 0)
                return null;

            var fullName = folderFullName ?? string.Empty;
            var name = folderName ?? string.Empty;

            foreach (var entry in effective)
            {
                // 1) and 3) — the full path is the entry, or ends at it on a separator.
                if (PathIsOrEndsAt(fullName, entry))
                    return entry;

                // 2) exact match against Name/DisplayName (catches cases where the user
                //    entered the short folder name but the server prefixes it)
                if (!string.IsNullOrEmpty(name) &&
                    entry.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }

                // 3b) Name ends with the excluded entry (handles Gmail-style "[Gmail]/Drafts").
                //     Deliberately unanchored, which is why "Kontakte" also takes "Vorgeschlagene
                //     Kontakte". Pre-existing and kept: for an exclusion list, matching more is the
                //     safe direction, and installations rely on it.
                if (!string.IsNullOrEmpty(name) &&
                    name.EndsWith(entry, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }

                // 4) the folder lies underneath one the entry names
                if (excludeSubfolders && IsUnderneath(fullName, entry))
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// True when the path names exactly the folder the entry names: either it is the entry, or
        /// it ends at the entry on a separator ("Drafts" against "INBOX.Drafts" or "INBOX/Drafts").
        /// This is what an ancestor has to satisfy for rule 4 as well.
        /// </summary>
        private static bool PathIsOrEndsAt(string path, string entry)
            => path.Equals(entry, StringComparison.OrdinalIgnoreCase)
               || path.EndsWith("." + entry, StringComparison.OrdinalIgnoreCase)
               || path.EndsWith("/" + entry, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True when some ancestor of the path is the folder the entry names. Every separator in the
        /// path opens one ancestor, and each is held against the same test the folder itself gets,
        /// so the rule inherits the anchoring instead of restating it. A separator with nothing
        /// behind it opens no ancestor: the folder would be its own.
        /// </summary>
        private static bool IsUnderneath(string path, string entry)
        {
            for (var i = 1; i < path.Length - 1; i++)
            {
                if (path[i] != '/' && path[i] != '.')
                    continue;

                if (PathIsOrEndsAt(path.Substring(0, i), entry))
                    return true;
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
