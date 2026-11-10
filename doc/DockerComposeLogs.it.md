# 🐳 Guida ai Log di Docker Compose {#docker-compose-logs-guide}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida spiega come visualizzare e configurare i log durante l'esecuzione dell'applicazione Mail Archiver utilizzando Docker Compose. Una gestione adeguata dei log è essenziale per monitorare lo stato di salute dell'applicazione, risolvere problemi e comprendere il comportamento del sistema.

## 📖 Visualizzazione dei Log di Docker Compose {#viewing-docker-compose-logs}

### Comandi Log di Base {#basic-log-commands}

Per visualizzare i log di tutti i servizi nella tua configurazione Docker Compose:

```bash
docker compose logs
```

Per visualizzare i log di un servizio specifico (ad es. l'applicazione Mail Archiver):

```bash
docker compose logs mailarchive-app
```

### Streaming dei Log in Tempo Reale {#real-time-log-streaming}

Per seguire i log in tempo reale (simile a `tail -f`):

```bash
docker compose logs -f
```

Per seguire i log di un servizio specifico:

```bash
docker compose logs -f mailarchive-app
```

### Filtro e Opzioni dei Log {#log-filtering-and-options}

Mostra le ultime N righe di log:
```bash
docker compose logs --tail=100
```

Mostra i log da un momento specifico:
```bash
docker compose logs --since=1h
```

Mostra i log fino a un momento specifico:
```bash
docker compose logs --until=2025-01-01T12:00:00
```

Mostra i timestamp nei log:
```bash
docker compose logs -t
```

## 📝 Configurazione del Livello di Log {#log-level-configuration}

L'applicazione Mail Archiver supporta vari livelli di log che controllano la verbosità dell'output. È possibile configurare questi livelli utilizzando le variabili d'ambiente nel file `docker-compose.yml`.

### Livelli di Log Disponibili {#available-log-levels}

I seguenti livelli di log sono disponibili, ordinati dal più verboso al meno verboso:

- **`Trace`** - Logging più dettagliato, inclusi i messaggi interni del framework
- **`Debug`** - Informazioni di debugging dettagliate per lo sviluppo e la risoluzione dei problemi
- **`Information`** - Informazioni generali sul flusso dell'applicazione (livello predefinito)
- **`Warning`** - Messaggi di avviso relativi a potenziali problemi
- **`Error`** - Messaggi di errore relativi a operazioni fallite
- **`Critical`** - Messaggi di errore critici che richiedono attenzione immediata
- **`None`** - Nessun output di logging

### Configurazione Log Predefinita {#default-log-configuration}

Per impostazione predefinita, l'applicazione utilizza la seguente configurazione del livello di log:
- **Predefinito**: `Information`
- **Microsoft.AspNetCore**: `Warning` (riduce il rumore del framework)
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning` (riduce il rumore dei comandi del database)

### Configurare i Livelli di Log in Docker Compose {#configuring-log-levels-in-docker-compose}

Per personalizzare i livelli di log, aggiungi le seguenti variabili d'ambiente al file `docker-compose.yml` sotto il servizio `mailarchive-app`:

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

### Esempio: Configurazione Debug {#example-debug-configuration}

Per scopi di risoluzione dei problemi, potresti voler abilitare un logging più verboso:

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

### Esempio: Configurazione di Produzione {#example-production-configuration}

Per gli ambienti di produzione, potresti voler ridurre la verbosità dei log per migliorare le prestazioni e ridurre il volume dei log:

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

## 🔍 Comprendere l'Output dei Log {#understanding-log-output}

### Formato dei Log {#log-format}

I log seguono questo formato generale:
```
timestamp [log-level] category - message
```

Esempio:
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### Categorie di Log Comuni {#common-log-categories}

- **`MailArchiver.Services.*`** - Log del servizio applicativo
- **`MailArchiver.Controllers.*`** - Log del controller Web API
- **`Microsoft.AspNetCore`** - Log del framework ASP.NET Core
- **`Microsoft.EntityFrameworkCore`** - Log di Entity Framework
- **`Microsoft.Extensions.Hosting`** - Log relativi all'hosting

## 🛠️ Suggerimenti per la Risoluzione dei Problemi {#troubleshooting-tips}

### 1. Trovare Messaggi di Errore {#1-finding-error-messages}

Per trovare rapidamente i messaggi di errore nei log:
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. Monitorare Operazioni Specifiche {#2-monitoring-specific-operations}

Per monitorare la Sincronizzazione delle email:
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. Controllare gli Avvisi {#3-checking-for-warnings}

Per controllare i messaggi di avviso:
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 Best Practice per la Gestione dei Log {#log-management-best-practices}

### Per lo Sviluppo {#for-development}
- Utilizzare il livello `Debug` o `Information` per una risoluzione dei problemi dettagliata
- Abilitare lo streaming dei log in tempo reale durante lo sviluppo
- Monitorare i log continuamente durante il test di nuove funzionalità

### Per la Produzione {#for-production}
- Utilizzare il livello `Warning` o `Error` per ridurre il volume dei log
- Implementare la rotazione dei log per prevenire problemi di spazio su disco
- Considerare soluzioni di logging centralizzato per configurazioni multi-container
- Revisionare regolarmente i log per eventi di sicurezza e problemi di prestazioni

## 📚 Documentazione Correlata {#related-documentation}

- [Guida all'Installazione e Configurazione](Setup.md) - Istruzioni complete per la configurazione
- [Access Logging](Logs.md) - Logging degli accessi a livello applicativo
- [Guida al Backup e Ripristino](BackupRestore.md) - Procedure di protezione dei dati
