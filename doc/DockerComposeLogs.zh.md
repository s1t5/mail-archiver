# 🐳 Docker Compose Logs Guide {#docker-compose-logs-guide}

[← Back to Documentation Index](index.md)

## 📋 概述 {#overview}

本指南介绍了在使用 Docker Compose 运行 Mail Archiver 应用程序时，如何查看和配置日志。合理的日志管理对于监控应用程序健康状况、排查问题以及理解系统行为至关重要。

## 📖 查看 Docker Compose 日志 {#viewing-docker-compose-logs}

### 基础日志命令 {#basic-log-commands}

要查看 Docker Compose 配置中所有服务的日志：

```bash
docker compose logs
```

要查看特定服务（例如 Mail Archiver 应用程序）的日志：

```bash
docker compose logs mailarchive-app
```

### 实时日志流 {#real-time-log-streaming}

要实时追踪日志（类似于 `tail -f`）：

```bash
docker compose logs -f
```

要追踪特定服务的日志：

```bash
docker compose logs -f mailarchive-app
```

### 日志过滤与选项 {#log-filtering-and-options}

显示最后 N 行日志：
```bash
docker compose logs --tail=100
```

显示自特定时间以来的日志：
```bash
docker compose logs --since=1h
```

显示到特定时间为止的日志：
```bash
docker compose logs --until=2025-01-01T12:00:00
```

在日志中显示时间戳：
```bash
docker compose logs -t
```

## 📝 日志级别配置 {#log-level-configuration}

Mail Archiver 应用程序支持多种日志级别，用于控制输出的详细程度。您可以通过在 `docker-compose.yml` 文件中使用环境变量来配置这些级别。

### 可用的日志级别 {#available-log-levels}

以下是可用的日志级别，按详细程度从高到低排序：

- **`Trace`** - 最详细的日志，包括内部框架消息
- **`Debug`** - 用于开发和故障排查的详细调试信息
- **`Information`** - 关于应用程序流程的一般信息（默认级别）
- **`Warning`** - 关于潜在问题的警告消息
- **`Error`** - 关于操作失败的错误消息
- **`Critical`** - 需要立即关注的关键错误消息
- **`None`** - 无日志输出

### 默认日志配置 {#default-log-configuration}

默认情况下，应用程序使用以下日志级别配置：
- **Default**: `Information`
- **Microsoft.AspNetCore**: `Warning`（减少框架噪音）
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning`（减少数据库命令噪音）

### 在 Docker Compose 中配置日志级别 {#configuring-log-levels-in-docker-compose}

要自定义日志级别，请在 `docker-compose.yml` 文件的 `mailarchive-app` 服务下添加以下环境变量：

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

### 示例：调试配置 {#example-debug-configuration}

出于故障排查目的，您可能希望启用更详细的日志：

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

### 示例：生产配置 {#example-production-configuration}

对于生产环境，您可能希望降低日志详细程度，以提高性能并减少日志量：

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

## 🔍 理解日志输出 {#understanding-log-output}

### 日志格式 {#log-format}

日志遵循以下通用格式：
```
timestamp [log-level] category - message
```

示例：
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### 常见日志类别 {#common-log-categories}

- **`MailArchiver.Services.*`** - 应用程序服务日志
- **`MailArchiver.Controllers.*`** - Web API 控制器日志
- **`Microsoft.AspNetCore`** - ASP.NET Core 框架日志
- **`Microsoft.EntityFrameworkCore`** - Entity Framework 日志
- **`Microsoft.Extensions.Hosting`** - 与托管相关的日志

## 🛠️ 故障排查技巧 {#troubleshooting-tips}

### 1. 查找错误消息 {#1-finding-error-messages}

快速在日志中查找错误消息：
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. 监控特定操作 {#2-monitoring-specific-operations}

监控电子邮件同步：
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. 检查警告 {#3-checking-for-warnings}

检查警告消息：
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 日志管理最佳实践 {#log-management-best-practices}

### 开发阶段 {#for-development}
- 使用 `Debug` 或 `Information` 级别进行详细的故障排查
- 在开发期间启用实时日志流
- 在测试新功能时持续监控日志

### 生产阶段 {#for-production}
- 使用 `Warning` 或 `Error` 级别以减少日志量
- 实现日志轮转（Log Rotation）以防止磁盘空间问题
- 对于多容器设置，考虑使用集中式日志解决方案
- 定期审查日志以发现安全事件和性能问题

## 📚 相关文档 {#related-documentation}

- [Installation and Setup Guide](Setup.md) - 完整的安装说明
- [Access Logging](Logs.md) - 应用程序级访问日志
- [Backup and Restore Guide](BackupRestore.md) - 数据保护程序
