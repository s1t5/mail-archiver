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

## 📚 Related Documentation

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Database cleanup procedures
- [GmailBestPractices.md](GmailBestPractices.md) - Gmail-specific configuration