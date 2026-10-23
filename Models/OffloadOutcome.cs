// Models/OffloadOutcome.cs
using System.Text;

namespace MailArchiver.Models
{
    /// <summary>
    /// What an offload actually did, per target folder.
    /// </summary>
    public class OffloadFolderOutcome
    {
        public int Appended { get; set; }
        public int SkippedAlreadyPresent { get; set; }
        public int MatchedByFingerprint { get; set; }
        public int Failed { get; set; }

        public int Total => Appended + SkippedAlreadyPresent + Failed;
    }

    /// <summary>
    /// Totals and a per-folder breakdown for one offload job.
    /// <para>
    /// The vocabulary follows the import job, which already reports a skipped-already-exists
    /// count, so that repeating a finished job is a legitimate verification pass: a correct
    /// implementation reports nothing appended and everything already present.
    /// </para>
    /// </summary>
    public class OffloadOutcome
    {
        /// <summary>Messages appended to the target mailbox.</summary>
        public int Appended { get; set; }

        /// <summary>Messages the target mailbox already held, by either criterion.</summary>
        public int SkippedAlreadyPresent { get; set; }

        /// <summary>
        /// How many of <see cref="SkippedAlreadyPresent"/> were recognised by the fingerprint
        /// rather than by Message-ID. Reported separately because it is the criterion that can
        /// fail silently, so a plausible number here is evidence that it works.
        /// </summary>
        public int MatchedByFingerprint { get; set; }

        /// <summary>Messages skipped because their source folder is excluded.</summary>
        public int SkippedExcludedFolder { get; set; }

        /// <summary>Messages that could not be appended.</summary>
        public int Failed { get; set; }

        /// <summary>True when nothing was actually written.</summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// True when the duplicate index covered less than the whole target mailbox, so a
        /// repeated run could append mail that is already present somewhere unindexed.
        /// </summary>
        public bool DuplicateScopeReduced { get; set; }

        public Dictionary<string, OffloadFolderOutcome> PerFolder { get; } = new();

        public int Considered => Appended + SkippedAlreadyPresent + SkippedExcludedFolder + Failed;

        public OffloadFolderOutcome Folder(string path)
        {
            if (!PerFolder.TryGetValue(path, out var outcome))
            {
                outcome = new OffloadFolderOutcome();
                PerFolder[path] = outcome;
            }
            return outcome;
        }

        /// <summary>Legacy shape, for the call sites that still expect a success/fail pair.</summary>
        public (int Successful, int Failed) AsTuple() => (Appended, Failed);

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine(DryRun ? "=== Offload dry run ===" : "=== Offload ===");
            sb.AppendLine($"Considered:            {Considered}");
            sb.AppendLine(DryRun
                ? $"Would append:          {Appended}"
                : $"Appended:              {Appended}");
            sb.AppendLine($"Already present:       {SkippedAlreadyPresent}");
            sb.AppendLine($"  of those by fingerprint: {MatchedByFingerprint}");
            sb.AppendLine($"Excluded folder:       {SkippedExcludedFolder}");
            sb.AppendLine($"Failed:                {Failed}");

            if (DuplicateScopeReduced)
            {
                sb.AppendLine("WARNING: the duplicate index did not cover the whole target mailbox.");
            }

            if (PerFolder.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Per target folder:");
                foreach (var entry in PerFolder.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var f = entry.Value;
                    sb.AppendLine($"  {entry.Key}");
                    sb.AppendLine($"      appended {f.Appended}, already present {f.SkippedAlreadyPresent} " +
                                  $"(fingerprint {f.MatchedByFingerprint}), failed {f.Failed}");
                }
            }

            return sb.ToString();
        }
    }
}
