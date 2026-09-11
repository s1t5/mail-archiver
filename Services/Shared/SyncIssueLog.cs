using MailArchiver.Models;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// The bounded list of things that went wrong during one sync run.
    ///
    /// Bounded per kind rather than overall, which is the whole point: an account with a thousand
    /// unreadable messages would otherwise fill the list before the sixteen folder problems — the
    /// ones that actually explain why the account is stuck — ever got in. Each kind keeps its own
    /// budget and its own count of what was dropped, so the UI can say "and 984 more" per group and
    /// stay honest about it.
    ///
    /// Sync jobs live for 24 hours in memory and, at 75 mailboxes on a quarter-hour interval, there
    /// are on the order of 7000 of them at any moment. That is affordable only because almost all of
    /// them stay empty: entries appear only for accounts that have trouble. The cap then bounds the
    /// worst case per job at three times the budget, however broken a mailbox is.
    ///
    /// Written by the sync task and read by the UI at the same time, hence the lock. The counters on
    /// SyncJob next to it are plain ints written the same way and have always been racy; a list is
    /// the one structure where that would actually throw.
    /// </summary>
    public sealed class SyncIssueLog
    {
        private readonly object _gate = new();
        private readonly List<SyncIssue> _issues = new();
        private readonly Dictionary<SyncIssueKind, int> _kept = new();
        private readonly Dictionary<SyncIssueKind, int> _dropped = new();
        private readonly int _maxPerKind;

        /// <param name="maxPerKind">
        /// Budget per kind. Zero or less switches the log off entirely, which is the honest reading
        /// of "keep none" and keeps the option usable as a way out if it ever costs too much.
        /// </param>
        public SyncIssueLog(int maxPerKind)
        {
            _maxPerKind = maxPerKind;
        }

        public void Add(SyncIssue issue)
        {
            if (issue == null) return;

            lock (_gate)
            {
                var kept = _kept.TryGetValue(issue.Kind, out var k) ? k : 0;

                if (_maxPerKind <= 0 || kept >= _maxPerKind)
                {
                    _dropped[issue.Kind] = (_dropped.TryGetValue(issue.Kind, out var d) ? d : 0) + 1;
                    return;
                }

                _issues.Add(issue);
                _kept[issue.Kind] = kept + 1;
            }
        }

        /// <summary>Everything kept, in the order it happened.</summary>
        public IReadOnlyList<SyncIssue> Issues
        {
            get { lock (_gate) { return _issues.ToList(); } }
        }

        /// <summary>What was kept for one kind.</summary>
        public int CountFor(SyncIssueKind kind)
        {
            lock (_gate) { return _kept.TryGetValue(kind, out var k) ? k : 0; }
        }

        /// <summary>What was dropped for one kind because the budget was spent.</summary>
        public int DroppedFor(SyncIssueKind kind)
        {
            lock (_gate) { return _dropped.TryGetValue(kind, out var d) ? d : 0; }
        }

        public bool IsEmpty
        {
            get { lock (_gate) { return _issues.Count == 0; } }
        }
    }
}
