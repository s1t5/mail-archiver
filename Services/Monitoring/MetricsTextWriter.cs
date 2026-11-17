using System.Globalization;
using System.Text;

namespace MailArchiver.Services.Monitoring;

/// <summary>
/// Writes the Prometheus text exposition format (version 0.0.4) for the archive metrics.
/// No package dependency: the metric set is small and static, so the format is produced
/// directly and the escaping rules are kept in one place.
/// </summary>
public static class MetricsTextWriter
{
    public static string Write(
        MetricsSnapshot? snapshot,
        bool databaseReachable,
        MetricsCounterSnapshot counters)
    {
        var builder = new StringBuilder(4096);

        WriteFamily(builder, "mailarchiver_up", "gauge",
            "1 when the metrics endpoint responds.",
            new[] { ("", "1") });

        WriteFamily(builder, "mailarchiver_metrics_ready", "gauge",
            "1 when a metrics snapshot has been collected.",
            new[] { ("", snapshot is null ? "0" : "1") });

        WriteFamily(builder, "mailarchiver_db_up", "gauge",
            "1 when the last metrics refresh reached PostgreSQL.",
            new[] { ("", databaseReachable ? "1" : "0") });

        var refreshTimestamp = snapshot is null
            ? 0
            : new DateTimeOffset(DateTime.SpecifyKind(snapshot.RefreshedAtUtc, DateTimeKind.Utc))
                .ToUnixTimeSeconds();

        WriteFamily(builder, "mailarchiver_metrics_refresh_timestamp_seconds", "gauge",
            "Unix time of the last successful metrics refresh.",
            new[] { ("", Num(refreshTimestamp)) });

        if (snapshot is not null)
        {
            WriteFamily(builder, "mailarchiver_emails", "gauge",
                "Number of archived emails.",
                new[] { ("", Num(snapshot.Emails)) });

            WriteFamily(builder, "mailarchiver_accounts", "gauge",
                "Number of mail accounts by state.",
                new[]
                {
                    (Labels(("state", "enabled")), Num(snapshot.EnabledAccounts)),
                    (Labels(("state", "disabled")), Num(snapshot.DisabledAccounts))
                });

            WriteFamily(builder, "mailarchiver_raw_messages", "gauge",
                "Number of stored original messages.",
                new[] { ("", Num(snapshot.RawMessages)) });

            WriteFamily(builder, "mailarchiver_raw_messages_enabled", "gauge",
                "1 when original message storage is enabled.",
                new[] { ("", snapshot.RawMessagesEnabled ? "1" : "0") });

            WriteFamily(builder, "mailarchiver_raw_messages_missing", "gauge",
                "Number of archived emails without a stored original.",
                new[] { ("", Num(snapshot.MissingOriginals)) });

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var neverSynced = new List<(string Labels, string Value)>();
            var lastSync = new List<(string Labels, string Value)>();
            var syncAge = new List<(string Labels, string Value)>();

            foreach (var account in snapshot.Accounts)
            {
                var labels = Labels(
                    ("account_id", account.Id.ToString(CultureInfo.InvariantCulture)),
                    ("account", account.Name),
                    ("enabled", account.IsEnabled ? "true" : "false"));

                if (account.LastSync == default)
                {
                    neverSynced.Add((labels, "1"));
                    continue;
                }

                var timestamp = new DateTimeOffset(
                    DateTime.SpecifyKind(account.LastSync, DateTimeKind.Utc)).ToUnixTimeSeconds();

                lastSync.Add((labels, Num(timestamp)));
                syncAge.Add((labels, Num(Math.Max(0, now - timestamp))));
            }

            WriteFamily(builder, "mailarchiver_account_never_synced", "gauge",
                "1 when an account has never completed a sync.", neverSynced);

            WriteFamily(builder, "mailarchiver_account_last_sync_timestamp_seconds", "gauge",
                "Unix time of the account's last successful sync.", lastSync);

            WriteFamily(builder, "mailarchiver_account_sync_age_seconds", "gauge",
                "Seconds since the account's last successful sync.", syncAge);
        }

        WriteFamily(builder, "mailarchiver_raw_message_failures_total", "counter",
            "Original messages that could not be stored, by reason.",
            new[]
            {
                (Labels(("reason", MetricsReasons.Size)), Num(counters.RawMessageSize)),
                (Labels(("reason", MetricsReasons.Write)), Num(counters.RawMessageWrite)),
                (Labels(("reason", MetricsReasons.NoCapture)), Num(counters.RawMessageNoCapture))
            });

        WriteFamily(builder, "mailarchiver_sync_failures_total", "counter",
            "Sync runs that ended in failure, rate limiting or timeout, by reason.",
            new[]
            {
                (Labels(("reason", MetricsReasons.Failed)), Num(counters.SyncFailed)),
                (Labels(("reason", MetricsReasons.RateLimited)), Num(counters.SyncRateLimited)),
                (Labels(("reason", MetricsReasons.Timeout)), Num(counters.SyncTimeout))
            });

        return builder.ToString();
    }

    private static void WriteFamily(
        StringBuilder builder,
        string name,
        string type,
        string help,
        IEnumerable<(string Labels, string Value)> samples)
    {
        builder.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
        builder.Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');

        foreach (var (labels, value) in samples)
        {
            builder.Append(name).Append(labels).Append(' ').Append(value).Append('\n');
        }
    }

    private static string Labels(params (string Name, string Value)[] labels)
    {
        if (labels.Length == 0)
        {
            return string.Empty;
        }

        return "{" + string.Join(",", labels.Select(label =>
            $"{label.Name}=\"{Escape(label.Value)}\"")) + "}";
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n");

    private static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);
}
