namespace MailArchiver.Models
{
    /// <summary>
    /// Cache-Tabelle: Speicherverbrauch pro Account (alle Mail-Felder + Anhaenge).
    /// Wird durch AccountStorageRefreshService und nach Sync/Import befuellt.
    /// </summary>
    public class AccountStorageCache
    {
        public int MailAccountId { get; set; }

        /// <summary>Speicherverbrauch aller Felder einer Mail in Bytes (Summe von pg_column_size je Spalte).</summary>
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
