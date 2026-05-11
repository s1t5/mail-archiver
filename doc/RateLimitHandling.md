# 📊 Rate Limit Handling

[← Back to Documentation Index](Index.md)

## 📋 Overview

Many email providers impose bandwidth limits on IMAP access. These limits typically range from a few hundred megabytes to several gigabytes per 24-hour period. When syncing large mailboxes that exceed these limits, the sync process would normally fail or be incomplete.

Mail Archiver includes a built-in rate limit handling system that automatically manages these limitations, ensuring reliable synchronization even for large mailboxes.

## ⚙️ How It Works

### Automatic Bandwidth Tracking

The system continuously monitors the amount of data downloaded during IMAP synchronization. When the configured bandwidth limit is reached:

1. **Sync Pauses Automatically**: The current sync operation stops gracefully
2. **Progress is Saved**: Checkpoints store the exact position where syncing stopped
3. **Status Updates**: The account shows "Rate-Limited" status in the dashboard
4. **Automatic Resume**: When the limit resets (typically after 24 hours), syncing resumes from the saved checkpoint

### Example: 14 GB Mailbox with 2.5 GB Daily Limit

```
Day 1:  Sync starts → ~2.5 GB downloaded → Rate-Limited
        Status: Rate-Limited, Checkpoint saved

Day 2:  Limit still active → Sync skipped

Day 3:  Limit resets → Sync resumes from checkpoint
        → Additional ~2.5 GB → Rate-Limited again

Day 4-6: Continues...

Day N:   All 14 GB synced → Status: Completed
```

## 🔧 Configuration

### Enabling Bandwidth Tracking

Add the following section to your `appsettings.json`:

```json
{
  "BandwidthTracking": {
    "Enabled": true,
    "DailyLimitMb": 2500,
    "WarningThresholdPercent": 80,
    "PauseHoursOnLimit": 24
  }
}
```

### Configuration Options

| Option | Description | Default | Example |
|--------|-------------|---------|---------|
| `Enabled` | Enable/disable bandwidth tracking | `false` | `true` |
| `DailyLimitMb` | Daily download limit in megabytes | `2500` | `2500` |
| `WarningThresholdPercent` | Percentage of limit to trigger warnings in logs | `80` | `80` |
| `PauseHoursOnLimit` | Hours to pause when limit is reached | `24` | `24` |

### Setting the Correct Limit

To determine the appropriate `DailyLimitMb` value:

1. Check your email provider's documentation or terms of service
2. Look for IMAP bandwidth limits or download quotas
3. Contact your email administrator if using a corporate email system
4. Start with a conservative value if unsure (e.g., 2000 MB)
5. Monitor logs for rate limit warnings during initial syncs

## 👤 User Experience

### Dashboard Indicators

When rate limiting is active, the account displays:

- **Status**: "Rate-Limited" 
- **Info Banner**: Explains the situation and estimated reset time
- **Sync Progress**: Shows emails processed before limit was reached

### Automatic Behavior

1. **No Manual Intervention Required**: The system handles everything automatically
2. **Application Restarts**: If the application restarts, the rate limit state is preserved in the database
3. **Multiple Accounts**: Each account tracks its bandwidth independently

### What Gets Saved

When a rate limit occurs, the system saves:

- **Sync Checkpoints**: Exact position in each folder where syncing stopped
- **Bandwidth Usage**: Total bytes downloaded for the current day
- **Limit State**: Whether limit is reached and when it will reset

## 🗄️ Database Maintenance

The `DatabaseMaintenanceService` automatically cleans up old data:

- **Bandwidth Records**: Deleted after 7 days
- **Sync Checkpoints**: Completed checkpoints deleted after 30 days
- **Incomplete Checkpoints**: Preserved until sync completes

## ✅ Best Practices

### For Large Mailboxes

1. **Enable Bandwidth Tracking**: Essential for mailboxes larger than the daily limit
2. **Plan for Initial Sync**: A large mailbox may take several days to fully sync
3. **Monitor Progress**: Check the dashboard for rate-limit status
4. **Don't Interrupt**: Let the automatic resume handle continuation

### For Administrators

1. **Set Appropriate Limits**: Match the `DailyLimitMb` to your email provider's actual limits
2. **Monitor Logs**: Warning messages appear at 80% of the limit
3. **Database Space**: Bandwidth tracking uses minimal additional storage
4. **Backup Before Changes**: Always backup before modifying rate limit settings

### Performance Considerations

- ✅ **Memory Impact**: Negligible - only tracking bytes, not storing emails
- ✅ **Database Impact**: Minimal - small records cleaned up automatically
- ✅ **Sync Speed**: No performance impact during active syncing

## 🛡️ Transient FETCH Errors / Server-Side Throttling

In addition to the byte-based bandwidth tracking described above, Mail Archiver
also detects and reacts to **per-message throttling** signaled by the IMAP
server itself. Some hosted IMAP providers apply per-account or per-IP throttling
that is **independent of the configured `BatchOperation` pauses**, because the
throttle is enforced on the server side based on overall request volume during
the current session.

### Symptoms

The server returns a `NO` (or `BAD`) response to a single `FETCH` command for a
message body, with messages such as:

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- responses containing `throttle`, `rate limit` or similar markers

Authentication, folder listing and the message search itself usually still
succeed — only the actual full-message retrieval fails.

### Automatic Handling

When such a transient error is detected, Mail Archiver applies a per-message
retry strategy:

1. **Retry with exponential backoff**: Up to **3 retries** for the same
   message, with delays of **5 s → 15 s → 60 s**.
2. **Connection recovery**: Before each retry, the connection, authentication
   and folder selection state are validated and restored if necessary.
3. **Reset on success**: A successful FETCH resets the consecutive-failure
   counter.
4. **Graceful sync pause**: If **10 consecutive messages** still fail with
   transient errors after all retries, the current sync run is paused
   gracefully — the same mechanism used by the bandwidth-limit pause:
   - The `LastSync` timestamp is **not** updated.
   - If `BandwidthTracking` is enabled, checkpoints are preserved so the next
     sync run resumes from the last successfully processed message.
   - The job is marked as rate-limited.
   - The next scheduled sync will resume automatically.

### Mitigation

If you regularly hit server-side throttling, lower the request rate by
adjusting your `BatchOperation` settings:

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

Note that some providers throttle based on **total bytes** or **total FETCHs
per time window** rather than per-second rate. In those cases:

- Enable `BandwidthTracking` (see above) and configure `DailyLimitMb` to match
  the provider's documented limit. Sync will then pause cleanly when the daily
  budget is exhausted and resume automatically the next day.
- Initial syncs of very large mailboxes may take several days, which is the
  intended behavior.

### What Counts as a Transient Error

Only IMAP `NO`/`BAD` responses with throttling-related text or response codes
are treated as transient. Other errors (malformed messages, UTF-8 decoding
issues, etc.) are still counted as `FailedEmails` immediately and do **not**
trigger the retry/pause logic.

## 📚 Related Documentation

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Database cleanup procedures
- [GmailBestPractices.md](GmailBestPractices.md) - Gmail-specific configuration

