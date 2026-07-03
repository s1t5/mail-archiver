namespace MailArchiver.Models
{
    /// <summary>
    /// Cache-Tabelle: Speicherverbrauch pro Account (Mail-Body + Anhaenge).
    /// Wird durch AccountStorageRefreshService und nach Sync/Import befuellt.
    /// </summary>
    public class AccountStorageCache
    {
        public int MailAccountId { get; set; }

        /// <summary>Speicherverbrauch der Mail-Body-Spalten in Bytes (pg_column_size).</summary>
        public long MailBytes { get; set; }

        /// <summary>Logische Summe der Anhangsgroessen in Bytes (Sum EmailAttachment.Size).</summary>
        public long AttachmentBytes { get; set; }

        /// <summary>Gesamtgroesse in Bytes (MailBytes + AttachmentBytes).</summary>
        public long TotalBytes { get; set; }

        /// <summary>Zeitpunkt der letzten Aktualisierung.</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual MailAccount MailAccount { get; set; }
    }
}
