namespace MailArchiver.Models
{
    /// <summary>
    /// Backfill-Status pro Account fuer die erstmalige Speicherverbrauchs-Berechnung.
    /// Crash-safe und resumable (analog SyncCheckpoints).
    /// </summary>
    public class AccountStorageBackfillState
    {
        public int MailAccountId { get; set; }

        /// <summary>Status: 'Pending' oder 'Done'.</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>Zeitpunkt der Fertigstellung des Backfills fuer diesen Account.</summary>
        public DateTime? CompletedAt { get; set; }

        public virtual MailAccount MailAccount { get; set; }
    }
}
