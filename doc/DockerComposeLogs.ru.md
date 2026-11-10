# 🐳 Docker Compose Logs Guide {#docker-compose-logs-guide}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

Это руководство объясняет, как просматривать и настраивать логи при запуске приложения Mail Archiver с использованием Docker Compose. Правильное управление логами имеет важное значение для мониторинга состояния приложения, устранения неполадок и понимания поведения системы.

## 📖 Viewing Docker Compose Logs {#viewing-docker-compose-logs}

### Basic Log Commands {#basic-log-commands}

Чтобы просмотреть логи всех сервисов в вашей конфигурации Docker Compose:

```bash
docker compose logs
```

Чтобы просмотреть логи конкретного сервиса (например, приложения Mail Archiver):

```bash
docker compose logs mailarchive-app
```

### Real-time Log Streaming {#real-time-log-streaming}

Чтобы отслеживать логи в режиме реального времени (аналогично `tail -f`):

```bash
docker compose logs -f
```

Чтобы отслеживать логи конкретного сервиса:

```bash
docker compose logs -f mailarchive-app
```

### Log Filtering and Options {#log-filtering-and-options}

Показать последние N строк логов:
```bash
docker compose logs --tail=100
```

Показать логи с определенного времени:
```bash
docker compose logs --since=1h
```

Показать логи до определенного времени:
```bash
docker compose logs --until=2025-01-01T12:00:00
```

Показать временные метки в логах:
```bash
docker compose logs -t
```

## 📝 Log Level Configuration {#log-level-configuration}

Приложение Mail Archiver поддерживает различные уровни логирования, которые управляют детализацией вывода. Вы можете настроить эти уровни с помощью переменных окружения в вашем файле `docker-compose.yml`.

### Available Log Levels {#available-log-levels}

Доступны следующие уровни логирования, от самого подробного к наименее подробному:

- **`Trace`** - Самое подробное логирование, включая внутренние сообщения фреймворка
- **`Debug`** - Подробная отладочная информация для разработки и устранения неполадок
- **`Information`** - Общая информация о работе приложения (уровень по умолчанию)
- **`Warning`** - Предупреждающие сообщения о потенциальных проблемах
- **`Error`** - Сообщения об ошибках при сбоях операций
- **`Critical`** - Критические сообщения об ошибках, требующие немедленного внимания
- **`None`** - Отключение логирования

### Default Log Configuration {#default-log-configuration}

По умолчанию приложение использует следующую конфигурацию уровней логирования:
- **Default**: `Information`
- **Microsoft.AspNetCore**: `Warning` (снижает уровень шума фреймворка)
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning` (снижает уровень шума команд базы данных)

### Configuring Log Levels in Docker Compose {#configuring-log-levels-in-docker-compose}

Чтобы настроить уровни логирования, добавьте следующие переменные окружения в ваш файл `docker-compose.yml` в разделе сервиса `mailarchive-app`:

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

### Example: Debug Configuration {#example-debug-configuration}

Для целей устранения неполадок вы можете захотеть включить более подробное логирование:

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

### Example: Production Configuration {#example-production-configuration}

Для производственных сред вы можете захотеть снизить детализацию логов, чтобы повысить производительность и уменьшить объем логов:

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

## 🔍 Understanding Log Output {#understanding-log-output}

### Log Format {#log-format}

Логи следуют этому общему формату:
```
timestamp [log-level] category - message
```

Пример:
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### Common Log Categories {#common-log-categories}

- **`MailArchiver.Services.*`** - Логи сервисов приложения
- **`MailArchiver.Controllers.*`** - Логи контроллеров Web API
- **`Microsoft.AspNetCore`** - Логи фреймворка ASP.NET Core
- **`Microsoft.EntityFrameworkCore`** - Логи Entity Framework
- **`Microsoft.Extensions.Hosting`** - Логи, связанные с хостингом

## 🛠️ Troubleshooting Tips {#troubleshooting-tips}

### 1. Finding Error Messages {#1-finding-error-messages}

Чтобы быстро найти сообщения об ошибках в логах:
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. Monitoring Specific Operations {#2-monitoring-specific-operations}

Чтобы отслеживать синхронизацию электронной почты:
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. Checking for Warnings {#3-checking-for-warnings}

Чтобы проверить наличие предупреждающих сообщений:
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 Log Management Best Practices {#log-management-best-practices}

### For Development {#for-development}
- Используйте уровни `Debug` или `Information` для детального устранения неполадок
- Включите потоковую передачу логов в реальном времени во время разработки
- Непрерывно отслеживайте логи при тестировании новых функций

### For Production {#for-production}
- Используйте уровни `Warning` или `Error`, чтобы уменьшить объем логов
- Внедрите ротацию логов, чтобы предотвратить проблемы с дисковым пространством
- Рассмотрите решения для централизованного логирования для конфигураций с несколькими контейнерами
- Регулярно проверяйте логи на наличие событий безопасности и проблем с производительностью

## 📚 Related Documentation {#related-documentation}

- [Installation and Setup Guide](Setup.md) - Полные инструкции по установке
- [Access Logging](Logs.md) - Логирование доступа на уровне приложения
- [Backup and Restore Guide](BackupRestore.md) - Процедуры защиты и восстановления данных
