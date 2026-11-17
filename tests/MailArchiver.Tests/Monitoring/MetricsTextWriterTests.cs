using MailArchiver.Services.Monitoring;

namespace MailArchiver.Tests.Monitoring;

public class MetricsTextWriterTests
{
    [Fact]
    public void WithoutSnapshot_ReportsNotReadyAndZeroCounters()
    {
        var text = MetricsTextWriter.Write(
            null, databaseReachable: false, new MetricsCounterSnapshot(0, 0, 0, 0, 0, 0));

        Assert.Contains("mailarchiver_up 1", text);
        Assert.Contains("mailarchiver_metrics_ready 0", text);
        Assert.Contains("mailarchiver_db_up 0", text);
        Assert.Contains("mailarchiver_raw_message_failures_total{reason=\"size\"} 0", text);
        Assert.Contains("mailarchiver_sync_failures_total{reason=\"timeout\"} 0", text);
    }

    [Fact]
    public void WithSnapshot_WritesCountsAndAccountSeries()
    {
        var lastSync = DateTime.UtcNow.AddMinutes(-10);
        var snapshot = new MetricsSnapshot(
            Emails: 3,
            EnabledAccounts: 1,
            DisabledAccounts: 1,
            RawMessages: 2,
            MissingOriginals: 1,
            RawMessagesEnabled: true,
            Accounts: new[]
            {
                new AccountSyncSnapshot(1, "Archiv \"A\"", lastSync, true),
                new AccountSyncSnapshot(2, "Alt", default, false)
            },
            RefreshedAtUtc: DateTime.UtcNow);

        var text = MetricsTextWriter.Write(
            snapshot, databaseReachable: true,
            new MetricsCounterSnapshot(1, 2, 3, 4, 5, 6));

        Assert.Contains("mailarchiver_emails 3", text);
        Assert.Contains("mailarchiver_accounts{state=\"enabled\"} 1", text);
        Assert.Contains("mailarchiver_accounts{state=\"disabled\"} 1", text);
        Assert.Contains("mailarchiver_raw_messages 2", text);
        Assert.Contains("mailarchiver_raw_messages_enabled 1", text);
        Assert.Contains("mailarchiver_raw_messages_missing 1", text);
        Assert.Contains(
            "mailarchiver_account_never_synced{account_id=\"2\",account=\"Alt\",enabled=\"false\"} 1",
            text);
        Assert.Contains(
            "mailarchiver_account_last_sync_timestamp_seconds{account_id=\"1\",account=\"Archiv \\\"A\\\"\",enabled=\"true\"}",
            text);
        Assert.DoesNotContain(
            "mailarchiver_account_last_sync_timestamp_seconds{account_id=\"2\"", text);
        Assert.Contains("mailarchiver_raw_message_failures_total{reason=\"write\"} 2", text);
        Assert.Contains("mailarchiver_sync_failures_total{reason=\"rate_limited\"} 5", text);
    }

    [Fact]
    public void CounterFamilyHeaderIsWrittenOnce()
    {
        var text = MetricsTextWriter.Write(
            null, databaseReachable: true, new MetricsCounterSnapshot(0, 0, 0, 0, 0, 0));

        Assert.Equal(1, CountOccurrences(text, "# TYPE mailarchiver_raw_message_failures_total"));
        Assert.Equal(1, CountOccurrences(text, "# TYPE mailarchiver_sync_failures_total"));
    }

    [Fact]
    public void EmptyAccountList_StillWritesFamilyHeaders()
    {
        var snapshot = new MetricsSnapshot(
            0, 0, 0, 0, 0, false,
            Array.Empty<AccountSyncSnapshot>(),
            DateTime.UtcNow);

        var text = MetricsTextWriter.Write(
            snapshot, databaseReachable: true, new MetricsCounterSnapshot(0, 0, 0, 0, 0, 0));

        Assert.Contains("# TYPE mailarchiver_account_never_synced gauge", text);
        Assert.Contains("# TYPE mailarchiver_account_sync_age_seconds gauge", text);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
