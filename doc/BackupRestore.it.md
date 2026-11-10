# 💾 Guida al Backup e al Ripristino {#backup-and-restore-guide}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida fornisce istruzioni dettagliate per eseguire il backup e il ripristino del tuo ambiente Mail Archiver Docker. I backup regolari sono essenziali per proteggere le tue email archiviate e i dati di configurazione.

## ⚠️ Disclaimer Importante {#important-disclaimer}

**Tutti gli script e i comandi forniti in questa guida sono solo esempi.** Non si assume alcuna garanzia o responsabilità per la loro funzionalità o idoneità per il tuo ambiente specifico. Sempre:
- Testare le procedure di backup e ripristino in un ambiente non di produzione prima di procedere
- Verificare che i backup siano completi e ripristinabili
- Adattare script e comandi alla propria configurazione e ai propri requisiti specifici
- Conservare più copie di backup in posizioni diverse
- Testare regolarmente le procedure di disaster recovery

**Utilizzare a proprio rischio.**

## 🎯 Raccomandazioni per il Backup {#backup-recommendations}

### Backup Offline (Altamente Raccomandati) {#offline-backups-highly-recommended}
Per la massima coerenza e integrità dei dati, **eseguire sempre i backup con l'applicazione e il database fermi**. Ciò garantisce:
- Nessuna transazione di database attiva durante il backup
- Completa coerenza dei dati tra tutti i file
- Nessun rischio di corruzione dei file dovuto a scritture concorrenti
- Capacità di ripristino affidabile

Sebbene i backup a caldo (Metodo 2 e 3) siano convenienti per ridurre al minimo i tempi di inattività, comportano un rischio maggiore di incongruenze. Per gli ambienti di produzione critici, pianificare backup offline regolari durante le finestre di manutenzione.

### Backup a Livello di Sistema {#system-level-backups}
Oltre ai backup specifici dell'applicazione, si **raccomanda vivamente** di creare backup dell'intera infrastruttura di sistema:
- **Backup del Container LXC**: Se eseguito in un container LXC, creare snapshot offline dell'intero container
- **Backup di Macchine Virtuali**: Per le implementazioni VM, creare snapshot completi della VM
- **Backup del Sistema Host**: Includere i volumi Docker, le configurazioni dei container e le impostazioni di sistema
- **File di Configurazione**: Effettuare il backup separatamente di tutti i file di configurazione Docker e di sistema

I backup a livello di sistema forniscono un ulteriore livello di sicurezza e consentono un disaster recovery completo, inclusa la possibilità di ripristinare l'intero ambiente su un host diverso, se necessario.

## 🛠️ Prerequisiti {#prerequisites}

- Setup Mail Archiver Docker funzionante (vedere [Guida al Setup](Setup.md))
- Accesso al sistema host Docker
- Spazio di archiviazione sufficiente per i backup

## 📦 Cosa fare il Backup {#what-to-backup}

L'ambiente Mail Archiver Docker è composto da diversi volumi di dati importanti che devono essere sottoposti a backup:

### 1. Database PostgreSQL {#1-postgresql-database}
- **Volume**: `./postgres-data`
- **Contiene**: Tutte le email archiviate, gli Account utente, le Caselle di posta e i dati dell'applicazione
- **Importanza**: ⭐⭐⭐ Critica - Questo è il tuo archivio dati principale

### 2. Chiavi di Protezione dei Dati {#2-data-protection-keys}
- **Volume**: `./data-protection-keys`
- **Contiene**: Chiavi ASP.NET Core Data Protection per la crittografia dei cookie e l'autenticazione
- **Importanza**: ⭐⭐ Importante - Necessaria per le sessioni utente esistenti e l'autenticazione sicura

### 2a. File dei Messaggi Originali (solo con `RawMessages.Storage=filesystem`) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **Volume**: `./raw-messages` (il lato host di `/app/RawMessages`)
- **Contiene**: L'originale `.eml` di ogni email, compresso in gzip, scritto quando `RawMessages:Enabled` è impostato e il backend di archiviazione è `filesystem`
- **Importanza**: ⭐⭐⭐ Critica **quando in uso** - il database conserva solo il checksum, la dimensione e il percorso relativo, quindi un ripristino senza questa directory lascia gli originali che non possono essere serviti
- Effettuare il backup di questa directory e del dump del database **insieme**. Il ripristino del database senza i file non è un errore che l'Archivio può riparare: i download risponderanno `404` e i checksum non possono essere ricalcolati da nient'altro. Il ripristino dei file senza il database è altrettanto inutile, perché nulla vi fa riferimento.
- Se utilizzi `RawMessages.Storage=postgres` (il predefinito), questa directory non esiste e gli originali si trovano all'interno di `./postgres-data` come in precedenza.
- Consultare [Archiviazione Messaggi Originali](RawMessages.md) per la funzionalità stessa e per come cambiare i backend.

### 3. Configurazione Docker Compose {#3-docker-compose-configuration}
- **File**: `docker-compose.yml`
- **Contiene**: Configurazione del container, variabili d'ambiente e impostazioni
- **Importanza**: ⭐⭐ Importante - Contiene la configurazione della tua applicazione

## 🔄 Procedure di Backup {#backup-procedures}

### Metodo 1: Backup Completo (Raccomandato) {#method-1-complete-backup-recommended}

Questo metodo esegue il backup di tutti i dati mentre i container sono fermi per garantire la coerenza dei dati.

```bash
# 1. Stop the containers
docker compose down

# 2. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 3. Copy all data volumes
cp -r postgres-data "$BACKUP_DIR/"
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Copy docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

# 7. Start the containers again
docker compose up -d

echo "Backup completed: $BACKUP_DIR.tar.gz"
```

### Metodo 2: Hot Backup (Tempo di inattività minimo) {#method-2-hot-backup-minimal-downtime}

Questo metodo crea un backup completo mentre il sistema è in funzione con un tempo di inattività minimo.

**Nota:** Sostituire `mailuser` e `MailArchiver` con le tue credenziali reali del database da `docker-compose.yml` (controllare le variabili d'ambiente `POSTGRES_USER` e `POSTGRES_DB`).

```bash
# 1. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 2. Backup the database
# Replace 'mailuser' and 'MailArchiver' with your actual POSTGRES_USER and POSTGRES_DB
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > "$BACKUP_DIR/database.sql"

# 3. Backup data protection keys (these change rarely, can be done while running)
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Backup docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

echo "Hot backup completed: $BACKUP_DIR.tar.gz"
```

### Metodo 3: Script di Backup Automatizzato {#method-3-automated-backup-script}

Per i backup automatizzati regolari, forniamo uno script di backup pronto all'uso. Lo script è disponibile in [backup.sh](attachments/backup.sh).

**Caratteristiche:**
- ✅ Rilevamento automatico delle credenziali del database da docker-compose.yml
- ✅ Hot backup (tempo di inattività minimo)
- ✅ Meccanismo di lock file per prevenire backup concorrenti
- ✅ Controllo dello spazio su disco prima del backup
- ✅ Pulizia automatica dei vecchi backup

**Configurazione:**

Prima di utilizzare lo script, modificare la sezione di configurazione all'inizio del file:

```bash
# Required settings:
MAILARCHIVER_DIR="/path/to/your/mailarchiver"   # Mail Archiver installation directory
BACKUP_BASE_DIR="/path/to/your/backups"         # Where to store backups

# Optional settings (with defaults):
RETENTION_DAYS=30                               # How many days to keep backups
LOG_FILE="/var/log/mailarchiver-backup.log"     # Log file location
MIN_DISK_SPACE_MB=1000                          # Minimum free disk space required
LOCK_FILE="/var/lock/mailarchiver-backup.lock"  # Lock file to prevent concurrent runs
```

**Setup:**

1. **Copiare lo script nella posizione desiderata:**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **Modificare la configurazione:**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # Regolare le impostazioni
   ```

3. **Rendere lo script eseguibile:**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **Testare lo script manualmente:**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **Verificare il backup:**
   - Controllare la directory di backup per il file `.tar.gz` creato
   - Esaminare il file di log per eventuali errori o avvisi

**Pianificazione dei Backup Automatizzati:**

Per pianificare i backup automatici utilizzando cron:

```bash
# Edit crontab
crontab -e

# Add a line to run backup daily at 2 AM
0 2 * * * /usr/local/bin/mailarchiver-backup.sh

# Or weekly on Sunday at 3 AM
0 3 * * 0 /usr/local/bin/mailarchiver-backup.sh

# Or multiple times (e.g., daily at 2 AM and 2 PM)
0 2,14 * * * /usr/local/bin/mailarchiver-backup.sh
```

## 🔙 Procedure di Ripristino {#restore-procedures}

Le procedure di ripristino sono progettate per corrispondere ai metodi di backup. Scegliere il metodo di ripristino appropriato in base al metodo di backup utilizzato:

- **Metodo 1 (Backup Completo)**: Utilizzare la procedura "Full Restore from Complete Backup". Questo metodo si usa quando si dispone di un backup completo che include la directory `postgres-data`.
- **Metodo 2 (Hot Backup)** o **Metodo 3 (Script di Backup Automatizzato)**: Utilizzare la procedura "Restore from SQL Dump" (gestisce le chiavi di protezione dei dati e il database). Questo metodo si usa quando si dispone di un hot backup che include il dump SQL del database e le chiavi di protezione dei dati.

Per comodità, abbiamo fornito anche uno script di ripristino automatizzato che può gestire tutte le procedure di ripristino automaticamente. È possibile utilizzare lo script invece di seguire manualmente i passaggi riportati di seguito.

### Script di Ripristino Automatizzato {#automated-restore-script}

Lo script di ripristino è disponibile nel file [restore.sh](attachments/restore.sh). Fornirà un ripristino guidato e chiederà il metodo di backup, il percorso del file tar di backup e la directory di installazione, quindi eseguirà il processo di ripristino automaticamente.

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

Lo script ti guiderà attraverso il processo di ripristino e gestirà automaticamente tutti i passaggi in base al metodo di backup utilizzato.

### Full Restore from Complete Backup (Metodo Manuale) {#full-restore-from-complete-backup-manual-method}

Questo ripristina tutti i dati da un Archivio di backup completo. Questo metodo deve essere utilizzato quando si dispone di un backup completo che include la directory `postgres-data` (creata dal metodo uno).

```bash
# 1. Stop the containers
docker compose down

# 2. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 3. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 4. Remove current data (make sure to backup first if needed!)
rm -rf postgres-data
rm -rf data-protection-keys

# 5. Restore data volumes
cp -r "$BACKUP_PATH/postgres-data" ./
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Start the containers
docker compose up -d

echo "Restore completed from: $BACKUP_FILE"
```

### Restore from SQL Dump (Metodo Manuale) {#restore-from-sql-dump-manual-method}

Questo ripristina il database e le chiavi di protezione dei dati da un hot backup (Metodo due e tre).

**Nota:** I comandi seguenti utilizzano credenziali di esempio (`mailuser` e `MailArchiver`). È necessario sostituirle con i valori reali del tuo file `docker-compose.yml`. Controllare le variabili d'ambiente `POSTGRES_USER` e `POSTGRES_DB` sotto il servizio `postgres`.

```bash
# 1. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 2. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 3. Stop the containers
docker compose down

# 4. Backup current data protection keys (optional, for safety)
# cp -r data-protection-keys data-protection-keys.backup

# 5. Restore data protection keys
rm -rf data-protection-keys
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Extract database credentials from docker-compose.yml
# Check your docker-compose.yml for the actual values and replace in the commands below:
# - POSTGRES_USER (default: mailuser)
# - POSTGRES_DB (default: MailArchiver)
DB_USER="mailuser"  # Replace with your POSTGRES_USER
DB_NAME="MailArchiver"  # Replace with your POSTGRES_DB

# 8. Start the database container
docker compose up -d postgres

# 9. Wait for database to be ready
echo "Waiting for database to be ready..."
sleep 10

# 10. Verify database is ready (adjust -U and -d parameters if needed)
docker compose exec -T postgres pg_isready -U "$DB_USER" -d "$DB_NAME"

# 11. Drop and recreate the database (WARNING: This deletes all current data!)
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "DROP DATABASE IF EXISTS \"$DB_NAME\";"
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "CREATE DATABASE \"$DB_NAME\";"

# 12. Restore the database from SQL dump
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" < "$BACKUP_PATH/database.sql"

# 13. Start all containers
docker compose up -d

# 14. Clean up extracted backup
rm -rf "$BACKUP_PATH"

echo "Restore completed from: $BACKUP_FILE"
```


## ⚠️ Note Importanti {#important-notes}

### Impostazioni di Connessione al Database {#database-connection-settings}
Assicurarsi che le impostazioni di connessione al database in `docker-compose.yml` corrispondano tra backup e ripristino:
- `POSTGRES_USER` (predefinito: mailuser)
- `POSTGRES_PASSWORD` (predefinito: masterkey)
- `POSTGRES_DB` (predefinito: MailArchiver)

### Chiavi di Protezione dei Dati {#data-protection-keys}
La directory `data-protection-keys` viene utilizzata per:
- Autenticazione utente e gestione delle sessioni
- Crittografia dei cookie
- Gestione sicura dei dati

Senza queste chiavi, le sessioni utente esistenti saranno invalidate e gli utenti dovranno effettuare nuovamente l'accesso.

### Variabili d'Ambiente {#environment-variables}
Tutte le impostazioni dell'applicazione sono memorizzate in variabili d'ambiente in `docker-compose.yml`. Assicurarsi di:
- Effettuare il Backup del file `docker-compose.yml`
- Documentare eventuali impostazioni personalizzate
- Utilizzare la stessa configurazione durante il ripristino

## 🔒 Best Practice di Sicurezza {#security-best-practices}

1. **Crittografare i Backup**: Considerare la crittografia degli Archivio di backup, specialmente se contengono dati email sensibili.

2. **Archiviazione Sicura**: Conservare i backup in una posizione sicura:
   - Utilizzare un server o un sistema di archiviazione separato
   - Implementare controlli di accesso
   - Considerare l'archiviazione off-site o in cloud

3. **Test Regolari**: Testare regolarmente le procedure di backup e ripristino per garantire che funzionino correttamente

4. **Monitorare la Dimensione del Backup**: Tenere traccia delle dimensioni dei backup per garantire di avere spazio di archiviazione adeguato

5. **Conservazione dei Backup**: Definire e implementare una policy di Conservazione dei backup (ad es. conservare i backup giornalieri per 7 giorni, i backup settimanali per 4 settimane, i backup mensili per 12 mesi)

6. **Cambiare le Password Predefinite**: Le password predefinite nella configurazione non sono sicure e dovrebbero essere cambiate per l'uso in produzione.


## 🖥️ Proxmox Backup {#proxmox-backup}

Quando si esegue l'ambiente Mail Archiver Docker in un container Proxmox LXC, si raccomanda di utilizzare la modalità "stop" per eseguire il backup dell'intero container per garantire la coerenza dei dati.

### Perché la Modalità Stop è Raccomandata {#why-stop-mode-is-recommended}

1. **Coerenza dei Dati**: Arrestare il container garantisce che tutte le transazioni del database siano completate e che i dati siano scritti su disco prima che venga eseguito il backup.
2. **Prevenire la Corruzione dei Dati**: Eseguire i backup mentre il container è attivo può portare alla corruzione dei dati, specialmente con le operazioni del database.
3. **Integrità del File System**: Arrestare il container garantisce che tutte le operazioni del file system siano completate, evitando scritture parziali durante il backup.

### Note sul Backup Proxmox {#proxmox-backup-notes}

- Assicurarsi sempre di avere spazio di archiviazione sufficiente per i backup.
- Testare regolarmente la procedura di ripristino per garantire che i backup siano validi.
- Considerare la configurazione di una pianificazione dei backup nell'interfaccia web di Proxmox per i backup automatizzati.
- Monitorare i log di backup per eventuali errori o problemi.
