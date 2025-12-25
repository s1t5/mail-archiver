# 🔄 PostgreSQL Major Version Upgrade Guide

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide explains how to upgrade the PostgreSQL database from one major version to another in your Mail Archiver Docker Compose environment. Major version upgrades are necessary to take advantage of new PostgreSQL features, performance improvements, and security updates.

## ⚠️ Important Disclaimer
> **Note**: This upgrade procedure is a general PostgreSQL database migration process and is not specific to Mail Archiver. It is provided as a recommendation based on PostgreSQL best practices. Always test the procedure in a non-production environment first and ensure you have proper backups before proceeding.


**All procedures described in this guide should be tested in a non-production environment first.** Major version upgrades involve data migration and carry risks of data loss or corruption. Always:
- Create a complete backup before starting the upgrade process
- Test the upgrade procedure in a development environment
- Verify all application functionality after the upgrade
- Have a rollback plan in case of issues
- Schedule upgrades during maintenance windows

**Use at your own risk.**

## 🎯 When to Upgrade

Consider upgrading PostgreSQL when:
- Your current version is approaching end-of-life
- Security updates require a newer version
- You're planning a major system maintenance window

## 🛠️ Prerequisites

- Running Mail Archiver Docker setup (see [Setup Guide](Setup.md))
- Sufficient disk space (at least 1.5x current database size)
- Access to the Docker host system
- Complete backup of current database (see [Backup and Restore Guide](BackupRestore.md))
- Maintenance window for application downtime

## 📋 Current Configuration

Your current Mail Archiver setup uses:
- **Database Name**: MailArchiver
- **Database User**: mailuser
- **Data Volume**: `./postgres-data` mapped to `/var/lib/postgresql/data`

If necessary, this must be adjusted in the following example commands and scripts.

## 🔄 Upgrade Procedures

This method creates a logical backup of your data and restores it to the new PostgreSQL version. It's the safest approach and works across any PostgreSQL versions.

> **Alternative Methods**: PostgreSQL also provides other upgrade methods such as `pg_upgrade` for faster upgrades. For more information about alternative upgrade approaches, please refer to the [official PostgreSQL upgrade documentation](https://www.postgresql.org/docs/current/upgrading.html).

#### Step 1: Prepare the Environment

```bash
# 1. Stop the application container (keep database running for backup)
docker compose stop mailarchive-app

# 2. Create a backup directory
mkdir -p postgres-backup

# 3. Create a database dump
echo "Export DB content (this can take some time)"
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > postgres-backup/database_dump.sql

# 4. Verify the dump was created successfully
ls -la postgres-backup/database_dump.sql

# 5. Stop the database container
docker compose down
```

#### Step 2: Update Docker Compose Configuration

Edit your `docker-compose.yml` file to use the new PostgreSQL version:

```yaml
  postgres:
    image: postgres:18  # Changed from old version
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data-new:/var/lib/postgresql  # New data directory
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
```

**Important Changes:**
- Updated `image` from `postgres:17` to `postgres:18` (or your target version)
- Changed volume mapping from `./postgres-data:/var/lib/postgresql/data` to `./postgres-data-new:/var/lib/postgresql` (the change of the internal path is required by version 18 and ongoind of postgres)

#### Step 3: Start New PostgreSQL Instance

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### Step 4: Restore Data to New Database

Restore the database dump to the new PostgreSQL instance:

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
This may take some time depending on database size
Watch for any error messages during restore

#### Step 5: Test and Complete Migration
Start the application to test connectivity:

```bash
docker compose up -d
```

Test application functionality:
- Log in to the web interface
- Verify email search works
- Check that existing data is accessible
- Test backup/restore functionality

If everything works correctly, clean up old data:

Stop containers
```bash
docker compose down
```
Remove old data directory (only after confirming new setup works)
```bash
rm -rf postgres-data
```
Remove the backup directory
```bash
rm -rf postgres-backup
```

Rename new data directory to standard name
```bash
mv postgres-data-new postgres-data
```

Update docker-compose.yml to use the standard volume name:
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Start the complete system
```bash
docker compose up -d
```

## ⚠️ Important Considerations

### Downtime Planning

- Depending on your system and database size, this procedure can take a long time.
- Schedule during maintenance windows

### Disk Space Requirements

Ensure adequate disk space (approx. tweo times your database size)

Check available space:
```bash
df -h
du -sh postgres-data/
```

### Rollback Plan

Always prepare for rollback:
1. Keep the old data directory until upgrade is verified
2. Maintain the old `docker-compose.yml` backup
3. Test rollback procedure in development first

### Post-Upgrade Verification

After upgrade, verify:
- Application connectivity to database
- Email search and retrieval functionality
- User authentication and session management
- Backup and restore functionality
- Database performance metrics

## 📚 Related Documentation

- [Backup and Restore Guide](BackupRestore.md) - Essential for pre-upgrade backups
- [Database Maintenance Guide](DatabaseMaintenance.md) - Post-upgrade maintenance
- [Setup Guide](Setup.md) - Docker Compose configuration
- [PostgreSQL Release Notes](https://www.postgresql.org/docs/release/) - Official release documentation
