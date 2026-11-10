# 🐳 Docker Compose Logs Guide {#docker-compose-logs-guide}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden erklärt, wie Sie Protokolle (Logs) anzeigen und konfigurieren, wenn Sie die Mail Archiver Anwendung mit Docker Compose ausführen. Ein ordnungsgemäßes Log-Management ist entscheidend für die Überwachung der Anwendungsgesundheit, die Fehlerbehebung und das Verständnis des Systemverhaltens.

## 📖 Anzeigen von Docker Compose Logs {#viewing-docker-compose-logs}

### Grundlegende Log-Befehle {#basic-log-commands}

Um die Logs aller Dienste in Ihrem Docker Compose Setup anzuzeigen:

```bash
docker compose logs
```

Um die Logs für einen bestimmten Dienst anzuzeigen (z. B. die Mail Archiver Anwendung):

```bash
docker compose logs mailarchive-app
```

### Echtzeit-Log-Streaming {#real-time-log-streaming}

Um die Logs in Echtzeit zu verfolgen (ähnlich wie `tail -f`):

```bash
docker compose logs -f
```

Um die Logs für einen bestimmten Dienst zu verfolgen:

```bash
docker compose logs -f mailarchive-app
```

### Log-Filterung und Optionen {#log-filtering-and-options}

Zeige die letzten N Zeilen der Logs an:
```bash
docker compose logs --tail=100
```

Zeige Logs seit einem bestimmten Zeitpunkt an:
```bash
docker compose logs --since=1h
```

Zeige Logs bis zu einem bestimmten Zeitpunkt an:
```bash
docker compose logs --until=2025-01-01T12:00:00
```

Zeige Zeitstempel in den Logs an:
```bash
docker compose logs -t
```

## 📝 Konfiguration des Log-Levels {#log-level-configuration}

Die Mail Archiver Anwendung unterstützt verschiedene Log-Levels, die die Detailgenauigkeit der Ausgabe steuern. Sie können diese Levels über Umgebungsvariablen in Ihrer `docker-compose.yml` Datei konfigurieren.

### Verfügbare Log-Levels {#available-log-levels}

Die folgenden Log-Levels sind verfügbar, sortiert von der höchsten zur niedrigsten Detailgenauigkeit:

- **`Trace`** - Detaillierteste Protokollierung, einschließlich interner Framework-Nachrichten
- **`Debug`** - Detaillierte Debugging-Informationen für Entwicklung und Fehlerbehebung
- **`Information`** - Allgemeine Informationen über den Anwendungsablauf (Standard-Level)
- **`Warning`** - Warnmeldungen über potenzielle Probleme
- **`Error`** - Fehlermeldungen über fehlgeschlagene Operationen
- **`Critical`** - Kritische Fehlermeldungen, die sofortige Aufmerksamkeit erfordern
- **`None`** - Keine Protokollierung der Ausgabe

### Standardmäßige Log-Konfiguration {#default-log-configuration}

Standardmäßig verwendet die Anwendung die folgende Log-Level-Konfiguration:
- **Standard**: `Information`
- **Microsoft.AspNetCore**: `Warning` (reduziert Framework-Rauschen)
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning` (reduziert Datenbank-Befehls-Rauschen)

### Konfigurieren von Log-Levels in Docker Compose {#configuring-log-levels-in-docker-compose}

Um die Log-Levels anzupassen, fügen Sie die folgenden Umgebungsvariablen in Ihrer `docker-compose.yml` Datei unter dem Dienst `mailarchive-app` hinzu:

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

### Beispiel: Debug-Konfiguration {#example-debug-configuration}

Für Zwecke der Fehlerbehebung möchten Sie möglicherweise eine detailliertere Protokollierung aktivieren:

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

### Beispiel: Produktions-Konfiguration {#example-production-configuration}

Für Produktionsumgebungen möchten Sie die Log-Detailgenauigkeit möglicherweise reduzieren, um die Leistung zu verbessern und das Log-Volumen zu verringern:

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

## 🔍 Verständnis der Log-Ausgabe {#understanding-log-output}

### Log-Format {#log-format}

Die Logs folgen diesem allgemeinen Format:
```
timestamp [log-level] category - message
```

Beispiel:
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### Häufige Log-Kategorien {#common-log-categories}

- **`MailArchiver.Services.*`** - Anwendungsdienst-Logs
- **`MailArchiver.Controllers.*`** - Web API Controller Logs
- **`Microsoft.AspNetCore`** - ASP.NET Core Framework Logs
- **`Microsoft.EntityFrameworkCore`** - Entity Framework Logs
- **`Microsoft.Extensions.Hosting`** - Hosting-bezogene Logs

## 🛠️ Tipps zur Fehlerbehebung {#troubleshooting-tips}

### 1. Finden von Fehlermeldungen {#1-finding-error-messages}

Um Fehlermeldungen schnell in den Logs zu finden:
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. Überwachen spezifischer Operationen {#2-monitoring-specific-operations}

Um die E-Mail-Synchronisierung zu überwachen:
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. Überprüfen auf Warnungen {#3-checking-for-warnings}

Um nach Warnmeldungen zu suchen:
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 Best Practices für das Log-Management {#log-management-best-practices}

### Für die Entwicklung {#for-development}
- Verwenden Sie das Level `Debug` oder `Information` für detaillierte Fehlerbehebung
- Aktivieren Sie das Echtzeit-Log-Streaming während der Entwicklung
- Überwachen Sie die Logs kontinuierlich, während Sie neue Funktionen testen

### Für die Produktion {#for-production}
- Verwenden Sie das Level `Warning` oder `Error`, um das Log-Volumen zu reduzieren
- Implementieren Sie eine Log-Rotation, um Probleme mit dem Speicherplatz zu vermeiden
- Erwägen Sie zentralisierte Logging-Lösungen für Multi-Container-Setups
- Überprüfen Sie regelmäßig die Logs auf Sicherheitsereignisse und Leistungsprobleme

## 📚 Verwandte Dokumentation {#related-documentation}

- [Installations- und Setup-Leitfaden](Setup.md) - Vollständige Anweisungen zum Setup
- [Zugriffsprotokollierung](Logs.md) - Zugriffsprotokollierung auf Anwendungsebene
- [Sicherungs- und Wiederherstellungsleitfaden](BackupRestore.md) - Verfahren zum Datenschutz
