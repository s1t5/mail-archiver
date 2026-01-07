# 🚨 Emergency Account Recovery

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide provides detailed instructions for recovering access to the Mail Archiver application when you've forgotten the administrator password. This emergency procedure allows you to regain access to your system without losing any data.

> 📝 **Version Requirement**: This recovery method is available in Mail Archiver version 2601.1 and later.

## ⚠️ Important Security Notice

This recovery method should only be used in emergency situations when you cannot access your administrator account. It requires temporary modification of your application configuration and should be reversed immediately after password recovery.

## 🛠️ Recovery Steps

### 1. Create Emergency Administrator Account

Modify your `appsettings.json` or Docker environment variables to create a temporary emergency administrator account:

**For Docker Compose (Recommended):**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Emergency admin account
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempPass123!
      # ... other settings remain unchanged
```

**For direct appsettings.json modification:**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. Restart the Application/Container

After modifying the configuration, restart your application:

**For Docker Compose:**
```bash
docker compose down
docker compose up -d
```

**For direct application:**
```bash
# Restart your application service
```

### 3. Login with Emergency Account

1. Access the Mail Archiver web interface
2. Login using the emergency credentials:
   - **Username**: `emergency_admin` (or whatever you set)
   - **Password**: `TempPass123!` (or whatever you set)

### 4. Reset Original Administrator Password

1. Navigate to the "Users" section from the main menu
2. Find your original administrator account in the user list
3. Click "Edit" for that user
4. Change the password to a new secure password
5. Click "Save Changes"

### 5. Restore Original Configuration

Revert your configuration changes back to the original administrator username:

**For Docker Compose:**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Original admin account
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword
      # ... other settings
```

**For direct appsettings.json:**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. Restart Application Again

Restart the application with the restored configuration:

**For Docker Compose:**
```bash
docker compose down
docker compose up -d
```

## 🎯 Example: Complete Docker Compose Recovery Process

Here's a complete example of the emergency recovery process using Docker Compose:

### Step 1: Emergency Configuration
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # EMERGENCY ADMIN ACCOUNT
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempEmergencyPass2026!
      
      # Other settings remain unchanged...
      - MailSync__IntervalMinutes=15
      - TimeZone__DisplayTimeZoneId=Europe/Berlin
      # ... rest of configuration
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Step 2: Restart and Login
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### Step 3: Reset Original Password
1. Login to web interface with emergency credentials
2. Go to Users section
3. Edit original admin user and set new password
4. Save changes

### Step 4: Restore Normal Configuration
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # RESTORED ORIGINAL ADMIN ACCOUNT
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword2026!
      
      # ... rest of configuration unchanged
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Step 5: Final Restart
```bash
docker compose down
docker compose up -d
```

## 🔒 Security Best Practices

1. **Use Strong Temporary Passwords**: Make your emergency password complex and unique
2. **Change It Immediately**: Reset the emergency password as soon as you regain access