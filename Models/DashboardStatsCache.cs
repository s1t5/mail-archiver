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

        public long TotalAttachments { get; set; }

        public int TotalAccounts { get; set; }

        public long TotalDatabaseSizeBytes { get; set; }

        /// <summary>Top-Senders als serialisierte Liste (jsonb).</summary>
        public string? TopSendersJson { get; set; }

        /// <summary>Monats-Histogramm als serialisierte Liste (jsonb).</summary>
        public string? EmailsByMonthJson { get; set; }

        /// <summary>Account-Panel als serialisierte Liste (jsonb).</summary>
        public string? EmailsPerAccountJson { get; set; }

        public DateTime ComputedAtUtc { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        [NotMapped]
        public List<EmailCountByAddress>? TopSenders =>
            Deserialize(TopSendersJson, () => new List<EmailCountByAddress>());

        [JsonIgnore]
        [NotMapped]
        public List<EmailCountByPeriod>? EmailsByMonth =>
            Deserialize(EmailsByMonthJson, () => new List<EmailCountByPeriod>());

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