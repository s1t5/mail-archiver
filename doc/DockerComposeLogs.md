# 🐳 Docker Compose Logs Guide

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide explains how to view and configure logs when running the Mail Archiver application using Docker Compose. Proper log management is essential for monitoring application health, troubleshooting issues, and understanding system behavior.

## 📖 Viewing Docker Compose Logs

### Basic Log Commands

To view logs from all services in your Docker Compose setup:

```bash
docker compose logs
```

To view logs for a specific service (e.g., the Mail Archiver application):

```bash
docker compose logs mailarchive-app
```

### Real-time Log Streaming

To follow logs in real-time (similar to `tail -f`):

```bash
docker compose logs -f
```

To follow logs for a specific service:

```bash
docker compose logs -f mailarchive-app
```

### Log Filtering and Options

Show last N lines of logs:
```bash
docker compose logs --tail=100
```

Show logs since a specific time:
```bash
docker compose logs --since=1h
```

Show logs until a specific time:
```bash
docker compose logs --until=2025-01-01T12:00:00
```

Show timestamps in logs:
```bash
docker compose logs -t
```

## 📝 Log Level Configuration

The Mail Archiver application supports various log levels that control the verbosity of the output. You can configure these levels using environment variables in your `docker-compose.yml` file.

### Available Log Levels

The following log levels are available, ordered from most verbose to least verbose:

- **`Trace`** - Most detailed logging, including internal framework messages
- **`Debug`** - Detailed debugging information for development and troubleshooting
- **`Information`** - General information about application flow (default level)
- **`Warning`** - Warning messages about potential issues
- **`Error`** - Error messages about failed operations
- **`Critical`** - Critical error messages that require immediate attention
- **`None`** - No logging output

### Default Log Configuration

By default, the application uses the following log level configuration:
- **Default**: `Information`
- **Microsoft.AspNetCore**: `Warning` (reduces framework noise)
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning` (reduces database command noise)

### Configuring Log Levels in Docker Compose

To customize log levels, add the following environment variables to your `docker-compose.yml` file under the `mailarchive-app` service:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Logging Settings (Optional)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning
```

### Example: Debug Configuration

For troubleshooting purposes, you might want to enable more verbose logging:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Verbose Logging for Debugging
      - Logging__LogLevel__Default=Debug
      - Logging__LogLevel__Microsoft_AspNetCore=Information
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Debug
```

### Example: Production Configuration

For production environments, you might want to reduce log verbosity to improve performance and reduce log volume:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Production Logging (Reduced Verbosity)
      - Logging__LogLevel__Default=Warning
      - Logging__LogLevel__Microsoft_AspNetCore=Error
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Error
```

## 🔍 Understanding Log Output

### Log Format

Logs follow this general format:
```
timestamp [log-level] category - message
```

Example:
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### Common Log Categories

- **`MailArchiver.Services.*`** - Application service logs
- **`MailArchiver.Controllers.*`** - Web API controller logs
- **`Microsoft.AspNetCore`** - ASP.NET Core framework logs
- **`Microsoft.EntityFrameworkCore`** - Entity Framework logs
- **`Microsoft.Extensions.Hosting`** - Hosting-related logs

## 🛠️ Troubleshooting Tips

### 1. Finding Error Messages

To quickly find error messages in the logs:
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. Monitoring Specific Operations

To monitor email synchronization:
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. Checking for Warnings

To check for warning messages:
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 Log Management Best Practices

### For Development
- Use `Debug` or `Information` level for detailed troubleshooting
- Enable real-time log streaming during development
- Monitor logs continuously while testing new features

### For Production
- Use `Warning` or `Error` level to reduce log volume
- Implement log rotation to prevent disk space issues
- Consider centralized logging solutions for multi-container setups
- Regularly review logs for security events and performance issues

## 📚 Related Documentation

- [Installation and Setup Guide](Setup.md) - Complete setup instructions
- [Access Logging](Logs.md) - Application-level access logging
- [Backup and Restore Guide](BackupRestore.md) - Data protection procedures
