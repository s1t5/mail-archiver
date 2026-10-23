using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using MailArchiver.Models.ViewModels;

namespace MailArchiver.Models
{
    /// <summary>
    /// Cache-Tabelle: vorberechnete Dashboard-Statistiken (admin-Scope).
    /// Wird durch DashboardStatsRefreshService periodisch befuellt, damit der
    /// Dashboard-Aufruf auf Installationen mit Millionen Mails keine teuren
    /// Aggregate ueber den gesamten Datenbestand im Request-Pfad ausfuehrt.
    /// Die JSON-Spalten speichern die Listen serialisiert als jsonb.
    /// </summary>
    public class DashboardStatsCache
    {
        public string Key { get; set; } = "admin";

        public long TotalEmails { get; set; }

        public long IncomingEmails { get; set; }

        public long OutgoingEmails { get; set; }

        public long TotalAttachments { get; set; }

        public long IncomingAttachments { get; set; }

        public long OutgoingAttachments { get; set; }

        public int TotalAccounts { get; set; }

        public int AccountDomains { get; set; }

        public long TotalDatabaseSizeBytes { get; set; }

        /// <summary>
        /// Default-Diagrammauswahl (Monate, ein Jahr, eingehende Absender) samt
        /// Top-Senders als serialisierte DashboardSeries (jsonb).
        /// </summary>
        public string? DefaultSeriesJson { get; set; }

        /// <summary>Account-Panel als serialisierte Liste (jsonb).</summary>
        public string? EmailsPerAccountJson { get; set; }

        public DateTime ComputedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Dashboard:ShowDirectionSplits zum Zeitpunkt der Berechnung. Null bei
        /// Zeilen, die vor dieser Spalte entstanden sind; eine Zeile, die nicht
        /// zur aktuellen Einstellung passt, wird bis zum naechsten Lauf ignoriert.
        /// </summary>
        public bool? ComputedWithDirectionSplits { get; set; }

        /// <summary>Dashboard:SelectablePeriods zum Zeitpunkt der Berechnung (siehe oben).</summary>
        public bool? ComputedWithSelectablePeriods { get; set; }

        [JsonIgnore]
        [NotMapped]
        public DashboardSeries? DefaultSeries =>
            Deserialize(DefaultSeriesJson, () => new DashboardSeries());

        [JsonIgnore]
        [NotMapped]
        public List<AccountStatistics>? EmailsPerAccount =>
            Deserialize(EmailsPerAccountJson, () => new List<AccountStatistics>());

        private static T Deserialize<T>(string? json, Func<T> fallback) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                return fallback();

            try
            {
                return JsonSerializer.Deserialize<T>(json,
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                       ?? fallback();
            }
            catch (JsonException)
            {
                return fallback();
            }
        }
    }
}