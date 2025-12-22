# 🔧 Database Maintenance Guide

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide explains how to configure and use the automatic database maintenance feature in Mail Archiver. The application includes a built-in background service that automatically performs PostgreSQL database maintenance operations to maintain optimal performance and prevent database bloat.

## ⚠️ Important Notice

**PostgreSQL databases do not automatically shrink when data is deleted.** When you delete emails or mailboxes, the database marks the space as available for reuse, but the actual database file size remains the same. This phenomenon is called "database bloat."

Mail Archiver includes an automatic maintenance service that runs daily VACUUM ANALYZE operations to:
- Reclaim storage for reuse within the database
- Update query statistics for optimal performance
- Prevent performance degradation over time
- Maintain database health

## 🚀 Automatic Maintenance (Recommended)

### Configuration

Enable automatic database maintenance in your `appsettings.json` or via environment variables in `docker-compose.yml`:

#### Option 1: Via appsettings.json

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### Option 2: Via docker-compose.yml (Recommended)

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Enable automatic database maintenance
      - DatabaseMaintenance__Enabled=true
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30
```

### Configuration Options

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Enable or disable automatic maintenance |
| `DailyExecutionTime` | string | `"02:00"` | Time of day to run maintenance (HH:mm format, 24-hour) |
| `TimeoutMinutes` | integer | `30` | Maximum time allowed for maintenance operations in minutes |

### How It Works

When enabled, the maintenance service:

1. **Starts automatically** with the application
2. **Waits** until the configured execution time
3. **Runs VACUUM ANALYZE** on the entire database
4. **Logs the operation** in the application's access log with duration
5. **Reschedules** for the next day

The maintenance operation:
- ✅ Runs without downtime
- ✅ Does not interfere with normal application operations
- ✅ Provides full logging and monitoring
- ✅ Reclaims space for reuse within the database
- ✅ Updates query statistics for better performance

### Choosing the Execution Time

Select a time when your system has the lowest activity:
- **Recommended**: Between 02:00 and 04:00 (night hours)
- **Consider**: Your timezone setting
- **Avoid**: Peak usage hours or backup windows

Example configurations:

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### Monitoring Automatic Maintenance

#### 1. Application Logs

Check the application logs for maintenance activities:

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. Access Log (UI)

All maintenance operations are logged in the application's access log:

1. Navigate to **Logs** → **Access Log** in the UI
2. Filter by type: **Database Maintenance**
3. View execution times, duration, and any errors

## 🔧 Manual Maintenance (Advanced)

### VACUUM FULL for Disk Space Recovery

**⚠️ Requires Application Downtime**

VACUUM FULL compacts the database and returns disk space to the operating system, but requires exclusive locks.

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**When to use:**
- After deleting large mailboxes
- To reclaim significant disk space
- During scheduled maintenance windows

**Important:**
- Requires free disk space equal to table size
- Can take hours on large databases
- Blocks all database operations
- Should be scheduled during off-peak hours

For detailed manual procedures, see [PostgreSQL VACUUM Documentation](https://www.postgresql.org/docs/current/sql-vacuum.html).

## 📚 Related Documentation

- [Setup Guide](Setup.md) - Initial configuration and environment variables
- [Backup and Restore Guide](BackupRestore.md) - Database backup procedures
- [PostgreSQL VACUUM Documentation](https://www.postgresql.org/docs/current/sql-vacuum.html) - Official PostgreSQL documentation

## 📝 Summary

✅ **Enable automatic maintenance** for hands-off database optimization
✅ **Monitor via access logs** to verify successful operations
✅ **Use manual VACUUM FULL** only when disk space recovery is needed
✅ **Schedule VACUUM FULL** during maintenance windows for large deletions
✅ **Keep backups current** before any manual maintenance operations

The automatic maintenance service handles daily database optimization, eliminating the need for manual intervention in most cases.
