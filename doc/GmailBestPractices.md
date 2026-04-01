# 📧 Gmail Best Practices

[← Back to Documentation Index](Index.md)

When using Gmail accounts with the Mail Archiver application, there are several specific configurations and best practices that should be followed to ensure optimal performance and avoid potential issues.

## 🔐 IMAP Configuration with App Passwords

Gmail accounts can be integrated with the Mail Archiver application using IMAP with App Passwords:

1. Enable 2-Factor Authentication on your Gmail account
2. Generate an App Password specifically for the Mail Archiver application
3. Use the App Password instead of your regular Gmail password when configuring the account in Mail Archiver

## 🏷️ Gmail Label Settings

It is strongly recommended to adjust your Gmail label settings to prevent the "All Mail" label from being exposed via IMAP:

1. Go to your Gmail webinterface and open the settings (little gear icon at the top) → See all settings → Labels tab
2. Find the "All Mail" label in the list
3. Uncheck the "Show in IMAP" option for "All Mail"

### 📚 Background on Gmail Label Implementation

Gmail's label system works differently from traditional email providers. In Gmail:
- All emails exist in a single "All Mail" repository
- Labels act as tags rather than folders
- The "All Mail" label contains every email in your account
- When an email is labeled, it appears in both the original folder and the labeled section. So all Mails in your inbox appear in the all Mail label too.

### ⚠️ Issues with IMAP Retrieval

When "All Mail" is exposed via IMAP, it can cause several problems:

1. **Duplicate Processing**: The archiver may process the same email multiple times since it appears in multiple folders/labels
2. **Performance Impact**: Retrieving "All Mail" can be extremely slow for large accounts with thousands of emails
4. **Synchronization Issues**: Conflicts can arise when trying to determine which folder an email actually belongs to

By disabling IMAP access to "All Mail", you ensure that emails are only retrieved once from their primary location, improving both performance and accuracy.

## 🗑️ Retention Policy and Expunge Settings

As mentioned in the main README, Gmail accounts require special attention regarding retention policies:

- Gmail's Auto-Expunge feature must be disabled in Gmail settings under the "Forwarding and POP/IMAP" tab
- This setting is crucial for retention policies to work correctly with Gmail accounts

To configure this setting:
1. Go to Gmail Settings → See all settings → Forwarding and POP/IMAP tab
2. In the "IMAP Access" section, select "Auto-Expunge off - Wait for the client to update the server." and "Move the message to the Bin"

## ⚠️ Gmail IMAP Rate Limiting

Gmail imposes a daily rate limit of 2500MB for IMAP retrieval. When archiving large volumes of emails, be aware that you may hit this limit, which could temporarily pause email retrieval until the next day.

### 📊 Rate Limit Handling

Mail Archiver provides built-in rate limit handling to manage Gmail's IMAP bandwidth limits:

- **Automatic bandwidth tracking**: Monitors downloaded data per account per day
- **Configurable limits**: Set `BandwidthTracking__DailyLimitMb=2500` for Gmail
- **Automatic pause**: Sync pauses when limit is reached and resumes after reset
- **Checkpoint system**: Sync progress is saved and resumed from the last position

For detailed configuration and usage, see the **[Rate Limit Handling Guide](RateLimitHandling.md)**.

### 🔧 Gmail Configuration Example

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 Large Mailbox Considerations

For large Gmail mailboxes (e.g., 14 GB), the initial sync will take multiple days:

| Mailbox Size | Days to Complete (at 2500 MB/day) |
|--------------|-----------------------------------|
| 5 GB | ~2 days |
| 10 GB | ~4 days |
| 14 GB | ~6 days |
| 20 GB | ~8 days |

**Tip**: Enable bandwidth tracking **before** adding large Gmail accounts to ensure proper rate limit handling from the start.

## ✅ Best Practices Summary

Following these best practices will help ensure smooth operation of the Mail Archiver with your Gmail accounts while maintaining data integrity and optimal performance.
