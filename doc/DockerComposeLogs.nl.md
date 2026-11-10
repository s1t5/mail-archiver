# 🐳 Docker Compose Logs Guide {#docker-compose-logs-guide}

[← Terug naar de Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Deze gids legt uit hoe u logs kunt bekijken en configureren wanneer u de Mail Archiver-applicatie uitvoert met Docker Compose. Goed logbeheer is essentieel voor het monitoren van de gezondheid van de applicatie, het oplossen van problemen en het begrijpen van het systeemgedrag.

## 📖 Docker Compose Logs Bekijken {#viewing-docker-compose-logs}

### Basis Log-commando's {#basic-log-commands}

Om logs van alle services in uw Docker Compose-opstelling te bekijken:

```bash
docker compose logs
```

Om logs voor een specifieke service te bekijken (bijv. de Mail Archiver-applicatie):

```bash
docker compose logs mailarchive-app
```

### Real-time Log Streaming {#real-time-log-streaming}

Om logs in real-time te volgen (vergelijkbaar met `tail -f`):

```bash
docker compose logs -f
```

Om logs voor een specifieke service te volgen:

```bash
docker compose logs -f mailarchive-app
```

### Log Filtering en Opties {#log-filtering-and-options}

Toon de laatste N regels logs:
```bash
docker compose logs --tail=100
```

Toon logs sinds een specifieke tijd:
```bash
docker compose logs --since=1h
```

Toon logs tot een specifieke tijd:
```bash
docker compose logs --until=2025-01-01T12:00:00
```

Toon tijdstempels in logs:
```bash
docker compose logs -t
```

## 📝 Log Level Configuratie {#log-level-configuration}

De Mail Archiver-applicatie ondersteunt verschillende log levels die de mate van details in de output bepalen. U kunt deze levels configureren met behulp van omgevingsvariabelen in uw `docker-compose.yml` bestand.

### Beschikbare Log Levels {#available-log-levels}

De volgende log levels zijn beschikbaar, gerangschikt van meest gedetailleerd naar minst gedetailleerd:

- **`Trace`** - Meest gedetailleerde logging, inclusief interne framework-berichten
- **`Debug`** - Gedetailleerde debugging-informatie voor ontwikkeling en probleemoplossing
- **`Information`** - Algemene informatie over de applicatieflow (standaard level)
- **`Warning`** - Waarschuwingsberichten over potentiële problemen
- **`Error`** - Foutmeldingen over mislukte operaties
- **`Critical`** - Kritieke foutmeldingen die onmiddellijke aandacht vereisen
- **`None`** - Geen logging output

### Standaard Log Configuratie {#default-log-configuration}

Standaard gebruikt de applicatie de volgende log level configuratie:
- **Standaard**: `Information`
- **Microsoft.AspNetCore**: `Warning` (vermindert framework-ruis)
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning` (vermindert database-opdrachtruis)

### Log Levels Configureren in Docker Compose {#configuring-log-levels-in-docker-compose}

Om log levels aan te passen, voegt u de volgende omgevingsvariabelen toe aan uw `docker-compose.yml` bestand onder de `mailarchive-app` service:

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

### Voorbeeld: Debug Configuratie {#example-debug-configuration}

Voor het oplossen van problemen wilt u mogelijk meer gedetailleerde logging inschakelen:

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

### Voorbeeld: Productie Configuratie {#example-production-configuration}

Voor productieomgevingen wilt u mogelijk de log-details verminderen om de prestaties te verbeteren en het logvolume te verkleinen:

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

## 🔍 Log Output Begrijpen {#understanding-log-output}

### Log Formaat {#log-format}

Logs volgen dit algemene formaat:
```
timestamp [log-level] category - message
```

Voorbeeld:
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### Veelvoorkomende Log Categorieën {#common-log-categories}

- **`MailArchiver.Services.*`** - Applicatie service logs
- **`MailArchiver.Controllers.*`** - Web API controller logs
- **`Microsoft.AspNetCore`** - ASP.NET Core framework logs
- **`Microsoft.EntityFrameworkCore`** - Entity Framework logs
- **`Microsoft.Extensions.Hosting`** - Hosting-gerelateerde logs

## 🛠️ Tips voor Probleemoplossing {#troubleshooting-tips}

### 1. Foutmeldingen Vinden {#1-finding-error-messages}

Om snel foutmeldingen in de logs te vinden:
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. Specifieke Operaties Monitoren {#2-monitoring-specific-operations}

Om e-mail synchronisatie te monitoren:
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. Controleren op Waarschuwingen {#3-checking-for-warnings}

Om te controleren op waarschuwingsberichten:
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 Best Practices voor Logbeheer {#log-management-best-practices}

### Voor Ontwikkeling {#for-development}
- Gebruik `Debug` of `Information` level voor gedetailleerde probleemoplossing
- Schakel real-time log streaming in tijdens de ontwikkeling
- Monitor logs continu tijdens het testen van nieuwe functies

### Voor Productie {#for-production}
- Gebruik `Warning` of `Error` level om het logvolume te verminderen
- Implementeer logrotatie om problemen met de schijfruimte te voorkomen
- Overweeg gecentraliseerde logging-oplossingen voor multi-container opstellingen
- Bekijk logs regelmatig op beveiligingsgebeurtenissen en prestatieproblemen

## 📚 Gerelateerde Documentatie {#related-documentation}

- [Installation and Setup Guide](Setup.md) - Volledige instructies voor installatie en configuratie
- [Access Logging](Logs.md) - Toegang-logging op applicatieniveau
- [Backup and Restore Guide](BackupRestore.md) - Procedures voor gegevensbescherming
