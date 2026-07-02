namespace MailArchiver.Services
{
    /// <summary>
    /// Berechnet und cached den Speicherverbrauch pro Mail-Account (Mail-Body + Anhaenge).
    /// </summary>
    public interface IAccountStorageService
    {
        /// <summary>
        /// Liest die gecachten Speicherwerte (formatiert) fuer die uebergebenen Accounts.
        /// Accounts ohne Cache-Eintrag erhalten "0 B".
        /// </summary>
        Task<Dictionary<int, string>> GetStorageForAccountsAsync(IEnumerable<int> accountIds);

        /// <summary>
        /// Berechnet den Speicherverbrauch fuer einen einzelnen Account neu und
        /// aktualisiert den Cache (UPSERT). Sofort-Refresh nach Sync/Import/Deletion.
        /// </summary>
        Task RefreshAccountStorageAsync(int mailAccountId);

        /// <summary>
        /// Berechnet den Speicherverbrauch fuer alle Accounts neu (Full-Refresh).
        /// Wird durch den taeglichen Refresh-Service ausgefuehrt.
        /// </summary>
        Task RefreshAllAccountStorageAsync(CancellationToken ct = default);

        /// <summary>
        /// Erstellt Backfill-State-Eintraege fuer alle Accounts ohne solchen (beim Start).
        /// </summary>
        Task EnsureBackfillStatesAsync();
    }
}
