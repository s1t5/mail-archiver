#!/bin/bash
# Mail Archiver Automated Backup Script
# This script performs automated hot backups of the Mail Archiver Docker environment

set -euo pipefail  # Exit on error, undefined variables, and pipe failures

# ================================
# CONFIGURATION - ADJUST THESE PATHS
# ================================
# Path to the Mail Archiver installation directory
MAILARCHIVER_DIR="/path/to/your/mailarchiver"
# Backup storage directory
BACKUP_BASE_DIR="/path/to/your/backups"
# Number of days to keep backups
RETENTION_DAYS=30
# Log file location
LOG_FILE="/var/log/mailarchiver-backup.log"
# Minimum free disk space in MB (adjust as needed)
MIN_DISK_SPACE_MB=1000
# Lock file to prevent concurrent backups
LOCK_FILE="/var/lock/mailarchiver-backup.lock"
# ================================

# Function to log messages
log() {
    local log_dir=$(dirname "$LOG_FILE")
    # Create log directory if it doesn't exist
    if [ ! -d "$log_dir" ]; then
        mkdir -p "$log_dir" 2>/dev/null || {
            echo "$(date '+%Y-%m-%d %H:%M:%S') - WARNING: Cannot create log directory, logging to stdout only"
            echo "$(date '+%Y-%m-%d %H:%M:%S') - $1"
            return
        }
    fi
    echo "$(date '+%Y-%m-%d %H:%M:%S') - $1" | tee -a "$LOG_FILE"
}

# Function to handle errors and cleanup
error_exit() {
    log "ERROR: $1"
    cleanup
    exit 1
}

# Function to cleanup on exit
cleanup() {
    # Remove lock file
    if [ -f "$LOCK_FILE" ]; then
        rm -f "$LOCK_FILE"
        log "Lock file removed"
    fi
}

# Set trap to cleanup on exit
trap cleanup EXIT INT TERM

# Check for lock file (prevent concurrent backups)
if [ -f "$LOCK_FILE" ]; then
    log "ERROR: Another backup is already running (lock file exists: $LOCK_FILE)"
    exit 1
fi

# Create lock file
touch "$LOCK_FILE" || error_exit "Failed to create lock file"
log "Lock file created"

# Start backup process
log "Starting Mail Archiver backup process"

# Check if required directories exist
if [ ! -d "$MAILARCHIVER_DIR" ]; then
    error_exit "Mail Archiver directory not found: $MAILARCHIVER_DIR"
fi

if [ ! -d "$BACKUP_BASE_DIR" ]; then
    error_exit "Backup directory not found: $BACKUP_BASE_DIR"
fi

# Check available disk space
AVAILABLE_SPACE=$(df -m "$BACKUP_BASE_DIR" | tail -1 | awk '{print $4}')
if [ "$AVAILABLE_SPACE" -lt "$MIN_DISK_SPACE_MB" ]; then
    error_exit "Insufficient disk space. Available: ${AVAILABLE_SPACE}MB, Required: ${MIN_DISK_SPACE_MB}MB"
fi
log "Disk space check passed: ${AVAILABLE_SPACE}MB available"

# Create backup directory
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_DIR="$BACKUP_BASE_DIR/mailarchiver-backup-$TIMESTAMP"
mkdir -p "$BACKUP_DIR" || error_exit "Failed to create backup directory"

# Change to Mail Archiver directory
cd "$MAILARCHIVER_DIR" || error_exit "Failed to change to Mail Archiver directory"

# Check if docker-compose.yml exists
if [ ! -f "docker-compose.yml" ]; then
    error_exit "docker-compose.yml not found in $MAILARCHIVER_DIR"
fi

# Check if docker-compose is running
if ! docker compose ps >/dev/null 2>&1; then
    error_exit "Docker Compose is not running or not accessible"
fi

# Extract database credentials from docker-compose.yml
DB_USER=$(grep -A 10 "postgres:" docker-compose.yml | grep "POSTGRES_USER:" | sed 's/.*POSTGRES_USER:[[:space:]]*//' | tr -d '[:space:]')
DB_NAME=$(grep -A 10 "postgres:" docker-compose.yml | grep "POSTGRES_DB:" | sed 's/.*POSTGRES_DB:[[:space:]]*//' | tr -d '[:space:]')

# Set defaults if not found
DB_USER=${DB_USER:-mailuser}
DB_NAME=${DB_NAME:-MailArchiver}

log "Using database credentials: User=$DB_USER, Database=$DB_NAME"

# Hot backup of database
log "Starting database backup..."
if docker compose exec -T postgres pg_dump -U "$DB_USER" -d "$DB_NAME" > "$BACKUP_DIR/database.sql" 2>/dev/null; then
    # Verify backup file is not empty
    SQL_SIZE=$(stat -f%z "$BACKUP_DIR/database.sql" 2>/dev/null || stat -c%s "$BACKUP_DIR/database.sql" 2>/dev/null)
    if [ "$SQL_SIZE" -lt 100 ]; then
        error_exit "Database backup appears to be empty or too small (${SQL_SIZE} bytes)"
    fi
    log "Database backup completed successfully (Size: $(numfmt --to=iec-i --suffix=B $SQL_SIZE 2>/dev/null || echo ${SQL_SIZE} bytes))"
else
    error_exit "Failed to backup database"
fi

# Backup data protection keys
log "Backing up data protection keys..."
if [ ! -d "data-protection-keys" ]; then
    log "WARNING: data-protection-keys directory not found, skipping"
else
    cp -r data-protection-keys "$BACKUP_DIR/" || error_exit "Failed to backup data protection keys"
    log "Data protection keys backup completed successfully"
fi

# Backup docker-compose configuration
log "Backing up docker-compose configuration..."
cp docker-compose.yml "$BACKUP_DIR/" || error_exit "Failed to backup docker-compose.yml"

# Create final compressed archive
log "Creating final compressed archive..."
tar -czf "$BACKUP_DIR.tar.gz" -C "$BACKUP_BASE_DIR" "$(basename $BACKUP_DIR)" || error_exit "Failed to create final compressed archive"
log "Final archive created successfully"

# Remove uncompressed backup directory
log "Cleaning up temporary files..."
rm -rf "$BACKUP_DIR" || error_exit "Failed to remove temporary backup directory"

# Remove old backups (older than RETENTION_DAYS)
log "Removing old backups (older than $RETENTION_DAYS days)..."
OLD_BACKUPS=$(find "$BACKUP_BASE_DIR" -name "mailarchiver-backup-*.tar.gz" -mtime +$RETENTION_DAYS)
if [ -n "$OLD_BACKUPS" ]; then
    echo "$OLD_BACKUPS" | while read -r old_backup; do
        rm -f "$old_backup"
        log "Removed old backup: $(basename $old_backup)"
    done
else
    log "No old backups to remove"
fi

# Log backup size
BACKUP_SIZE=$(du -h "$BACKUP_DIR.tar.gz" | cut -f1)
log "Backup size: $BACKUP_SIZE"

log "Backup process completed successfully"
echo "Backup completed: $BACKUP_DIR.tar.gz"

exit 0
