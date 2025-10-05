#!/bin/bash
# Mail Archiver Restore Script
# This script provides a guided restore process for Mail Archiver backups

set -e  # Exit on any error

# ================================
# Color definitions for output
# ================================
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# ================================
# Functions
# ================================

# Print colored messages
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Error exit function
error_exit() {
    print_error "$1"
    exit 1
}

# Ask yes/no question
ask_yes_no() {
    local question="$1"
    local answer
    while true; do
        read -p "$question (y/n): " answer
        case $answer in
            [Yy]* ) return 0;;
            [Nn]* ) return 1;;
            * ) echo "Please answer yes (y) or no (n).";;
        esac
    done
}

# Validate directory exists
validate_directory() {
    local dir="$1"
    if [ ! -d "$dir" ]; then
        error_exit "Directory not found: $dir"
    fi
}

# Validate file exists
validate_file() {
    local file="$1"
    if [ ! -f "$file" ]; then
        error_exit "File not found: $file"
    fi
}

# Extract database credentials from docker-compose.yml
extract_db_credentials() {
    local compose_file="$1"
    
    if [ ! -f "$compose_file" ]; then
        error_exit "docker-compose.yml not found: $compose_file"
    fi
    
    # Extract POSTGRES_USER
    DB_USER=$(grep -A 10 "postgres:" "$compose_file" | grep "POSTGRES_USER:" | sed 's/.*POSTGRES_USER:[[:space:]]*//' | tr -d '[:space:]')
    if [ -z "$DB_USER" ]; then
        print_warning "POSTGRES_USER not found in docker-compose.yml, using default: mailuser"
        DB_USER="mailuser"
    fi
    
    # Extract POSTGRES_PASSWORD
    DB_PASSWORD=$(grep -A 10 "postgres:" "$compose_file" | grep "POSTGRES_PASSWORD:" | sed 's/.*POSTGRES_PASSWORD:[[:space:]]*//' | tr -d '[:space:]')
    if [ -z "$DB_PASSWORD" ]; then
        print_warning "POSTGRES_PASSWORD not found in docker-compose.yml, using default: masterkey"
        DB_PASSWORD="masterkey"
    fi
    
    # Extract POSTGRES_DB
    DB_NAME=$(grep -A 10 "postgres:" "$compose_file" | grep "POSTGRES_DB:" | sed 's/.*POSTGRES_DB:[[:space:]]*//' | tr -d '[:space:]')
    if [ -z "$DB_NAME" ]; then
        print_warning "POSTGRES_DB not found in docker-compose.yml, using default: MailArchiver"
        DB_NAME="MailArchiver"
    fi
    
    print_success "Database credentials extracted: User=$DB_USER, Database=$DB_NAME"
}

# ================================
# Main Script
# ================================

echo ""
echo "=========================================="
echo "  Mail Archiver Restore Script"
echo "=========================================="
echo ""

# Check if running as root (optional warning)
if [ "$EUID" -eq 0 ]; then 
    print_warning "Running as root. Make sure this is intended."
fi

# ================================
# Step 1: Determine backup method
# ================================

echo ""
print_info "Which backup method was used?"
echo "  1) Complete Backup (includes postgres-data directory)"
echo "  2) Hot Backup / Automated Script (includes database.sql dump)"
echo ""

while true; do
    read -p "Enter your choice (1 or 2): " backup_method
    case $backup_method in
        1)
            RESTORE_METHOD="complete"
            print_success "Selected: Complete Backup restore method"
            break
            ;;
        2)
            RESTORE_METHOD="sql_dump"
            print_success "Selected: SQL Dump restore method"
            break
            ;;
        *)
            print_error "Invalid choice. Please enter 1 or 2."
            ;;
    esac
done

# ================================
# Step 2: Get backup file path
# ================================

echo ""
print_info "Please provide the path to your backup tar.gz file"
read -p "Backup file path: " BACKUP_FILE

# Validate backup file
validate_file "$BACKUP_FILE"
print_success "Backup file found: $BACKUP_FILE"

# ================================
# Step 3: Get installation directory
# ================================

echo ""
print_info "Please provide the path to your Mail Archiver installation directory"
print_info "(This is the directory containing docker-compose.yml)"
read -p "Installation directory: " INSTALL_DIR

# Validate installation directory
validate_directory "$INSTALL_DIR"
validate_file "$INSTALL_DIR/docker-compose.yml"
print_success "Installation directory found: $INSTALL_DIR"

# ================================
# Step 4: Confirm restore
# ================================

echo ""
print_warning "=========================================="
print_warning "  WARNING: DATA WILL BE OVERWRITTEN"
print_warning "=========================================="
echo ""
print_warning "This restore process will:"
echo "  - Stop all running containers"
echo "  - Delete existing data"
echo "  - Restore data from backup"
echo ""
print_warning "Current data will be PERMANENTLY LOST!"
echo ""

if ! ask_yes_no "Are you sure you want to proceed with the restore?"; then
    print_info "Restore cancelled by user."
    exit 0
fi

# ================================
# Step 5: Perform restore
# ================================

echo ""
print_info "Starting restore process..."

# Change to installation directory
cd "$INSTALL_DIR" || error_exit "Failed to change to installation directory"

# Create temporary directory for extraction
TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

print_info "Extracting backup archive..."
tar -xzf "$BACKUP_FILE" -C "$TEMP_DIR" || error_exit "Failed to extract backup archive"

# Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="$TEMP_DIR/$BACKUP_DIR"

validate_directory "$BACKUP_PATH"
print_success "Backup extracted to: $BACKUP_PATH"

# Stop containers
print_info "Stopping Docker containers..."
docker compose down || error_exit "Failed to stop Docker containers"
print_success "Docker containers stopped"

if [ "$RESTORE_METHOD" = "complete" ]; then
    # ================================
    # Complete Backup Restore
    # ================================
    
    print_info "Performing complete backup restore..."
    
    # Verify postgres-data exists in backup
    if [ ! -d "$BACKUP_PATH/postgres-data" ]; then
        error_exit "postgres-data directory not found in backup. Did you mean to use SQL Dump restore method?"
    fi
    
    # Remove current data
    print_info "Removing current data directories..."
    rm -rf postgres-data
    rm -rf data-protection-keys
    print_success "Current data removed"
    
    # Restore data volumes
    print_info "Restoring postgres-data..."
    cp -r "$BACKUP_PATH/postgres-data" ./ || error_exit "Failed to restore postgres-data"
    print_success "postgres-data restored"
    
    print_info "Restoring data-protection-keys..."
    cp -r "$BACKUP_PATH/data-protection-keys" ./ || error_exit "Failed to restore data-protection-keys"
    print_success "data-protection-keys restored"
    
    # Ask about docker-compose.yml restore
    if [ -f "$BACKUP_PATH/docker-compose.yml" ]; then
        echo ""
        if ask_yes_no "Do you want to restore docker-compose.yml from backup? (Review recommended)"; then
            cp docker-compose.yml docker-compose.yml.backup
            cp "$BACKUP_PATH/docker-compose.yml" ./
            print_success "docker-compose.yml restored (backup saved as docker-compose.yml.backup)"
        else
            print_info "Keeping current docker-compose.yml"
        fi
    fi
    
    # Start containers
    print_info "Starting Docker containers..."
    docker compose up -d || error_exit "Failed to start Docker containers"
    print_success "Docker containers started"
    
elif [ "$RESTORE_METHOD" = "sql_dump" ]; then
    # ================================
    # SQL Dump Restore
    # ================================
    
    print_info "Performing SQL dump restore..."
    
    # Verify database.sql exists in backup
    if [ ! -f "$BACKUP_PATH/database.sql" ]; then
        error_exit "database.sql not found in backup. Did you mean to use Complete Backup restore method?"
    fi
    
    # Backup current data protection keys (optional safety)
    if [ -d "data-protection-keys" ]; then
        print_info "Backing up current data-protection-keys..."
        cp -r data-protection-keys data-protection-keys.backup.$(date +%Y%m%d-%H%M%S)
        print_success "Current data-protection-keys backed up"
    fi
    
    # Restore data protection keys
    print_info "Restoring data-protection-keys..."
    rm -rf data-protection-keys
    cp -r "$BACKUP_PATH/data-protection-keys" ./ || error_exit "Failed to restore data-protection-keys"
    print_success "data-protection-keys restored"
    
    # Ask about docker-compose.yml restore
    if [ -f "$BACKUP_PATH/docker-compose.yml" ]; then
        echo ""
        if ask_yes_no "Do you want to restore docker-compose.yml from backup? (Review recommended)"; then
            cp docker-compose.yml docker-compose.yml.backup
            cp "$BACKUP_PATH/docker-compose.yml" ./
            print_success "docker-compose.yml restored (backup saved as docker-compose.yml.backup)"
        else
            print_info "Keeping current docker-compose.yml"
        fi
    fi
    
    # Extract database credentials from docker-compose.yml
    print_info "Reading database credentials from docker-compose.yml..."
    extract_db_credentials "docker-compose.yml"
    
    # Start database container
    print_info "Starting PostgreSQL container..."
    docker compose up -d postgres || error_exit "Failed to start PostgreSQL container"
    print_success "PostgreSQL container started"
    
    # Wait for database to be ready
    print_info "Waiting for database to be ready..."
    sleep 10
    
    # Check if database is accepting connections
    MAX_RETRIES=30
    RETRY_COUNT=0
    while ! docker compose exec -T postgres pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1; do
        RETRY_COUNT=$((RETRY_COUNT + 1))
        if [ $RETRY_COUNT -ge $MAX_RETRIES ]; then
            error_exit "Database failed to become ready after $MAX_RETRIES attempts"
        fi
        print_info "Waiting for database... (attempt $RETRY_COUNT/$MAX_RETRIES)"
        sleep 2
    done
    print_success "Database is ready"
    
    # Drop and recreate database
    print_warning "Dropping and recreating $DB_NAME database..."
    docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "DROP DATABASE IF EXISTS \"$DB_NAME\";" || error_exit "Failed to drop database"
    docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "CREATE DATABASE \"$DB_NAME\";" || error_exit "Failed to create database"
    print_success "Database recreated"
    
    # Restore database from SQL dump
    print_info "Restoring database from SQL dump (this may take a while)..."
    docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" < "$BACKUP_PATH/database.sql" || error_exit "Failed to restore database"
    print_success "Database restored successfully"
    
    # Start all containers
    print_info "Starting all Docker containers..."
    docker compose up -d || error_exit "Failed to start all Docker containers"
    print_success "All Docker containers started"
fi

# ================================
# Final steps
# ================================

echo ""
print_success "=========================================="
print_success "  Restore completed successfully!"
print_success "=========================================="
echo ""
print_info "Next steps:"
echo "  1. Verify the application is running: docker compose ps"
echo "  2. Check the logs: docker compose logs -f"
echo "  3. Access the Mail Archiver web interface"
echo ""

if [ "$RESTORE_METHOD" = "sql_dump" ]; then
    print_info "Note: Existing user sessions have been invalidated."
    print_info "Users will need to log in again."
    echo ""
fi

print_success "Restore completed from: $BACKUP_FILE"
