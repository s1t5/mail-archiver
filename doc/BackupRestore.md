# 💾 Backup and Restore Guide

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide provides detailed instructions for backing up and restoring your Mail Archiver Docker environment. Regular backups are essential to protect your archived emails and configuration data.

## ⚠️ Important Disclaimer

**All scripts and commands provided in this guide are examples only.** No guarantee or liability is assumed for their functionality or suitability for your specific environment. Always:
- Test backup and restore procedures in a non-production environment first
- Verify backups are complete and restorable
- Adapt scripts and commands to your specific setup and requirements
- Keep multiple backup copies in different locations
- Regularly test your disaster recovery procedures

**Use at your own risk.**

## 🎯 Backup Recommendations

### Offline Backups (Highly Recommended)
For maximum data consistency and integrity, **always perform backups with the application and database stopped**. This ensures:
- No active database transactions during backup
- Complete data consistency across all files
- No risk of file corruption from concurrent writes
- Reliable restore capability

While hot backups (Method 2 and 3) are convenient for minimizing downtime, they carry a higher risk of inconsistencies. For critical production environments, schedule regular offline backups during maintenance windows.

### System-Level Backups
In addition to application-specific backups, it is **strongly recommended** to create backups of the entire system infrastructure:
- **LXC Container Backups**: If running in an LXC container, create offline snapshots of the entire container
- **Virtual Machine Backups**: For VM deployments, create full VM snapshots
- **Host System Backups**: Include Docker volumes, container configurations, and system settings
- **Configuration Files**: Back up all Docker and system configuration files separately

System-level backups provide an additional safety layer and enable complete disaster recovery, including the ability to restore the entire environment to a different host if needed.

## 🛠️ Prerequisites

- Running Mail Archiver Docker setup (see [Setup Guide](Setup.md))
- Access to the Docker host system
- Sufficient storage space for backups

## 📦 What to Backup

The Mail Archiver Docker environment consists of several important data volumes that need to be backed up:

### 1. PostgreSQL Database
- **Volume**: `./postgres-data`
- **Contains**: All archived emails, user accounts, mail accounts, and application data
- **Importance**: ⭐⭐⭐ Critical - This is your main data store

### 2. Data Protection Keys
- **Volume**: `./data-protection-keys`
- **Contains**: ASP.NET Core Data Protection keys for cookie encryption and authentication
- **Importance**: ⭐⭐ Important - Required for existing user sessions and secure authentication

### 3. Docker Compose Configuration
- **File**: `docker-compose.yml`
- **Contains**: Container configuration, environment variables, and settings
- **Importance**: ⭐⭐ Important - Contains your application configuration

## 🔄 Backup Procedures

### Method 1: Complete Backup (Recommended)

This method backs up all data while the containers are stopped to ensure data consistency.

```bash
# 1. Stop the containers
docker compose down

# 2. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 3. Copy all data volumes
cp -r postgres-data "$BACKUP_DIR/"
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Copy docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

# 7. Start the containers again
docker compose up -d

echo "Backup completed: $BACKUP_DIR.tar.gz"
```

### Method 2: Hot Backup (Minimal Downtime)

This method creates a complete backup while the system is running with minimal downtime.

**Note:** Replace `mailuser` and `MailArchiver` with your actual database credentials from `docker-compose.yml` (check `POSTGRES_USER` and `POSTGRES_DB` environment variables).

```bash
# 1. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 2. Backup the database
# Replace 'mailuser' and 'MailArchiver' with your actual POSTGRES_USER and POSTGRES_DB
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > "$BACKUP_DIR/database.sql"

# 3. Backup data protection keys (these change rarely, can be done while running)
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Backup docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

echo "Hot backup completed: $BACKUP_DIR.tar.gz"
```

### Method 3: Automated Backup Script

For regular automated backups, we provide a ready-to-use backup script. The script is available in [backup.sh](attachments/backup.sh).

**Features:**
- ✅ Automatic database credential detection from docker-compose.yml
- ✅ Hot backup (minimal downtime)
- ✅ Lock file mechanism to prevent concurrent backups
- ✅ Disk space check before backup
- ✅ Automatic cleanup of old backups

**Configuration:**

Before using the script, edit the configuration section at the top of the file:

```bash
# Required settings:
MAILARCHIVER_DIR="/path/to/your/mailarchiver"   # Mail Archiver installation directory
BACKUP_BASE_DIR="/path/to/your/backups"         # Where to store backups

# Optional settings (with defaults):
RETENTION_DAYS=30                               # How many days to keep backups
LOG_FILE="/var/log/mailarchiver-backup.log"     # Log file location
MIN_DISK_SPACE_MB=1000                          # Minimum free disk space required
LOCK_FILE="/var/lock/mailarchiver-backup.lock"  # Lock file to prevent concurrent runs
```

**Setup:**

1. **Copy the script to your desired location:**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **Edit the configuration:**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # Adjust the settings
   ```

3. **Make the script executable:**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **Test the script manually:**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **Verify the backup:**
   - Check the backup directory for the created `.tar.gz` file
   - Review the log file for any errors or warnings

**Scheduling Automated Backups:**

To schedule automatic backups using cron:

```bash
# Edit crontab
crontab -e

# Add a line to run backup daily at 2 AM
0 2 * * * /usr/local/bin/mailarchiver-backup.sh

# Or weekly on Sunday at 3 AM
0 3 * * 0 /usr/local/bin/mailarchiver-backup.sh

# Or multiple times (e.g., daily at 2 AM and 2 PM)
0 2,14 * * * /usr/local/bin/mailarchiver-backup.sh
```

## 🔙 Restore Procedures

The restore procedures are designed to match the backup methods. Choose the appropriate restore method based on which backup method you used:

- **Method 1 (Complete Backup)**: Use the "Full Restore from Complete Backup" procedure. This method is used when you have a complete backup that includes the `postgres-data` directory.
- **Method 2 (Hot Backup)** or **Method 3 (Automated Backup Script)**: Use the "Restore from SQL Dump" procedure (handles the data protection keys and database). This method is used when you have a hot backup that includes the database SQL dump and data protection keys.

For convenience, we've also provided an automated restore script that can handle all the restore procedures automatically. You can use the script instead of manually following the steps below.

### Automated Restore Script

The restore script is available in the [restore.sh](attachments/restore.sh) file. It will provide a guided restore and ask for the backup method, the path to the backup tar file and the installation directory, then perform the restore process automatically.

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

The script will guide you through the restore process and automatically handle all the steps based on the backup method you used.

### Full Restore from Complete Backup (Manual Method)

This restores all data from a complete backup archive. This method should be used when you have a complete backup that includes the `postgres-data` directory (created by method one).

```bash
# 1. Stop the containers
docker compose down

# 2. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 3. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 4. Remove current data (make sure to backup first if needed!)
rm -rf postgres-data
rm -rf data-protection-keys

# 5. Restore data volumes
cp -r "$BACKUP_PATH/postgres-data" ./
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Start the containers
docker compose up -d

echo "Restore completed from: $BACKUP_FILE"
```

### Restore from SQL Dump (Manual Method)

This restores the database and data protection keys from a hot backup (Method two and three).

**Note:** The commands below use example credentials (`mailuser` and `MailArchiver`). You must replace these with the actual values from your `docker-compose.yml` file. Check the `POSTGRES_USER` and `POSTGRES_DB` environment variables under the `postgres` service.

```bash
# 1. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 2. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 3. Stop the containers
docker compose down

# 4. Backup current data protection keys (optional, for safety)
# cp -r data-protection-keys data-protection-keys.backup

# 5. Restore data protection keys
rm -rf data-protection-keys
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Extract database credentials from docker-compose.yml
# Check your docker-compose.yml for the actual values and replace in the commands below:
# - POSTGRES_USER (default: mailuser)
# - POSTGRES_DB (default: MailArchiver)
DB_USER="mailuser"  # Replace with your POSTGRES_USER
DB_NAME="MailArchiver"  # Replace with your POSTGRES_DB

# 8. Start the database container
docker compose up -d postgres

# 9. Wait for database to be ready
echo "Waiting for database to be ready..."
sleep 10

# 10. Verify database is ready (adjust -U and -d parameters if needed)
docker compose exec -T postgres pg_isready -U "$DB_USER" -d "$DB_NAME"

# 11. Drop and recreate the database (WARNING: This deletes all current data!)
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "DROP DATABASE IF EXISTS \"$DB_NAME\";"
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "CREATE DATABASE \"$DB_NAME\";"

# 12. Restore the database from SQL dump
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" < "$BACKUP_PATH/database.sql"

# 13. Start all containers
docker compose up -d

# 14. Clean up extracted backup
rm -rf "$BACKUP_PATH"

echo "Restore completed from: $BACKUP_FILE"
```


## ⚠️ Important Notes

### Database Connection Settings
Ensure your database connection settings in `docker-compose.yml` match across backup and restore:
- `POSTGRES_USER` (default: mailuser)
- `POSTGRES_PASSWORD` (default: masterkey)
- `POSTGRES_DB` (default: MailArchiver)

### Data Protection Keys
The `data-protection-keys` directory is used for:
- User authentication and session management
- Cookie encryption
- Secure data handling

Without these keys, existing user sessions will be invalidated and users must log in again.

### Environment Variables
All application settings are stored in environment variables in `docker-compose.yml`. Make sure to:
- Backup your `docker-compose.yml` file
- Document any custom settings
- Use the same configuration during restore

## 🔒 Security Best Practices

1. **Encrypt Backups**: Consider encrypting backup archives, especially if they contain sensitive email data.

2. **Secure Storage**: Store backups in a secure location:
   - Use a separate server or storage system
   - Implement access controls
   - Consider off-site or cloud storage

3. **Regular Testing**: Regularly test your backup and restore procedures to ensure they work correctly

4. **Monitor Backup Size**: Keep track of backup sizes to ensure you have adequate storage space

5. **Backup Retention**: Define and implement a backup retention policy (e.g., keep daily backups for 7 days, weekly backups for 4 weeks, monthly backups for 12 months)

6. **Change Default Passwords**: The default passwords in the configuration are insecure and should be changed for production use.


## 🖥️ Proxmox Backup

When running the Mail Archiver Docker environment in a Proxmox LXC container, it's recommended to use the "stop" mode for backing up the entire container to ensure data consistency.

### Why Stop Mode is Recommended

1. **Data Consistency**: Stopping the container ensures that all database transactions are completed and data is written to disk before the backup is taken.
2. **Prevent Data Corruption**: Running backups while the container is active can lead to data corruption, especially with database operations.
3. **File System Integrity**: Stopping the container ensures that all file system operations are complete, preventing partial writes during backup.

### Proxmox Backup Notes

- Always ensure you have sufficient storage space for backups.
- Test your restore procedure regularly to ensure backups are valid.
- Consider setting up a backup schedule in the Proxmox web interface for automated backups.
- Monitor the backup logs for any errors or issues.
