# 🛠️ Mail Archiver Setup Guide

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide provides detailed instructions for setting up the Mail Archiver application using Docker Compose.

## 🛠️ Prerequisites

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Installation Steps

1. Install the prerequisites on your system.

2. Create a `docker-compose.yml` file with the following content:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey;

      # Authentication Settings
      - Authentication__Enabled=true
      - Authentication__Username=admin
      - Authentication__Password=secure123!
      - Authentication__SessionTimeoutMinutes=60
      - Authentication__CookieName=MailArchiverAuth

      # MailSync Settings
      - MailSync__IntervalMinutes=15
      - MailSync__TimeoutMinutes=60
      - MailSync__ConnectionTimeoutSeconds=180
      - MailSync__CommandTimeoutSeconds=300
      - MailSync__AlwaysForceFullSync=false
      - MailSync__IgnoreSelfSignedCert=false

      # BatchRestore Settings
      - BatchRestore__AsyncThreshold=50
      - BatchRestore__MaxSyncEmails=150
      - BatchRestore__MaxAsyncEmails=50000
      - BatchRestore__SessionTimeoutMinutes=30
      - BatchRestore__DefaultBatchSize=50

      # BatchOperation Settings
      - BatchOperation__BatchSize=50
      - BatchOperation__PauseBetweenEmailsMs=50
      - BatchOperation__PauseBetweenBatchesMs=250

      # Selection Settings
      - Selection__MaxSelectableEmails=250

      # Npgsql Settings
      - Npgsql__CommandTimeout=900

      # Upload Settings for MBox files
      - Upload__MaxFileSizeGB=10
      - Upload__KeepAliveTimeoutHours=4
      - Upload__RequestHeadersTimeoutHours=2
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
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

networks:
  postgres:
```

3. Edit the database configuration in the `docker-compose.yml` and set a secure password in the `POSTGRES_PASSWORD` variable and the `ConnectionString`.

4. If you want to use authentication (which is strongly recommended), define a `Authentication__Username` and `Authentication__Password` which is used for the admin user.

5. Configure a reverse proxy of your choice with https and authentication to secure access to the application. 

> ⚠️ **Attention**: The application itself does not provide encrypted access via https! It must be set up via a reverse proxy!

6. Initial start of the containers:
```bash
docker compose up -d
```

7. Restart containers:
```bash
docker compose restart
```

8. Access the application

9. Login with your defined credentials and add your first email account:
   - Navigate to "Email Accounts" section
   - Click "New Account"
   - Enter your server details and credentials
   - Save and start archiving!
   - If you want, create other users and assign accounts.

## 📚 Environment Variable Explanations

### 🗄️ Database Connection
- `ConnectionStrings__DefaultConnection`: The connection string to the PostgreSQL database. Modify the `Host`, `Database`, `Username`, and `Password` values as needed.

### 🔐 Authentication Settings
- `Authentication__Enabled`: Whether to enable authentication (true/false). Set to true to require login.
- `Authentication__Username`: The username for the admin account.
- `Authentication__Password`: The password for the admin account.
- `Authentication__SessionTimeoutMinutes`: The session timeout in minutes.
- `Authentication__CookieName`: The name of the authentication cookie.

### 📨 MailSync Settings
- `MailSync__IntervalMinutes`: The interval in minutes between email synchronization.
- `MailSync__TimeoutMinutes`: The timeout for the sync operation in minutes.
- `MailSync__ConnectionTimeoutSeconds`: The connection timeout for IMAP connections in seconds.
- `MailSync__CommandTimeoutSeconds`: The command timeout for IMAP commands in seconds.
- `MailSync__AlwaysForceFullSync`: Whether to always force a full sync (true/false).
- `MailSync__IgnoreSelfSignedCert`: Whether to ignore self-signed certificates (true/false).

### 📤 BatchRestore Settings
- `BatchRestore__AsyncThreshold`: The number of emails that triggers async processing.
- `BatchRestore__MaxSyncEmails`: The maximum number of emails for sync processing.
- `BatchRestore__MaxAsyncEmails`: The maximum number of emails for async processing.
- `BatchRestore__SessionTimeoutMinutes`: The session timeout for batch restore in minutes.
- `BatchRestore__DefaultBatchSize`: The default batch size for email operations.

### 📦 BatchOperation Settings
- `BatchOperation__BatchSize`: The batch size for email operations.
- `BatchOperation__PauseBetweenEmailsMs`: The pause between individual emails in milliseconds.
- `BatchOperation__PauseBetweenBatchesMs`: The pause between batches in milliseconds.

### 🎯 Selection Settings
- `Selection__MaxSelectableEmails`: The maximum number of emails that can be selected at once.

### 🗃️ Npgsql Settings
- `Npgsql__CommandTimeout`: The timeout for database commands in seconds.

### 📥 Upload Settings
- `Upload__MaxFileSizeGB`: The maximum file size for uploads in GB.
- `Upload__KeepAliveTimeoutHours`: The keep alive timeout for uploads in hours.
- `Upload__RequestHeadersTimeoutHours`: The timeout for request headers in hours.

## 🔒 Security Notes

- Use strong passwords and change default credentials. Passwords should be at least 12 characters long and include a mix of uppercase letters, lowercase letters, numbers, and special characters. Avoid using common words or easily guessable information.
- Consider implementing HTTPS with a reverse proxy in production
- Regular backups of the PostgreSQL database are recommended
