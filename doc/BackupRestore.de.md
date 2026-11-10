# 💾 Backup und Wiederherstellung Leitfaden {#backup-and-restore-guide}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden bietet detaillierte Anweisungen für die Sicherung und Wiederherstellung Ihrer Mail Archiver Docker-Umgebung. Regelmäßige Sicherungen sind unerlässlich, um Ihre archivierten E-Mails und Konfigurationsdaten zu schützen.

## ⚠️ Wichtiger Haftungsausschluss {#important-disclaimer}

**Alle in diesem Leitfaden bereitgestellten Skripte und Befehle dienen nur als Beispiele.** Es wird keine Garantie oder Haftung für deren Funktionalität oder Eignung für Ihre spezifische Umgebung übernommen. Tun Sie immer Folgendes:
- Testen Sie Sicherungs- und Wiederherstellungsverfahren zuerst in einer Nicht-Produktionsumgebung
- Überprüfen Sie, ob Sicherungen vollständig und wiederherstellbar sind
- Passen Sie Skripte und Befehle an Ihr spezifisches Setup und Ihre Anforderungen an
- Bewahren Sie mehrere Sicherungskopien an verschiedenen Orten auf
- Testen Sie regelmäßig Ihre Disaster-Recovery-Verfahren

**Die Verwendung erfolgt auf eigene Gefahr.**

## 🎯 Backup-Empfehlungen {#backup-recommendations}

### Offline-Sicherungen (Dringend empfohlen) {#offline-backups-highly-recommended}
Um maximale Datenkonsistenz und Integrität zu gewährleisten, **führen Sie Sicherungen immer bei gestoppter Anwendung und Datenbank durch**. Dies stellt Folgendes sicher:
- Keine aktiven Datenbanktransaktionen während der Sicherung
- Vollständige Datenkonsistenz über alle Dateien hinweg
- Kein Risiko von Dateikorruption durch gleichzeitige Schreibvorgänge
- Zuverlässige Wiederherstellungsmöglichkeit

Während Hot-Backups (Methode 2 und 3) praktisch sind, um Ausfallzeiten zu minimieren, bergen sie ein höheres Risiko für Inkonsistenzen. Planen Sie für kritische Produktionsumgebungen regelmäßige Offline-Sicherungen während Wartungsfenstern ein.

### Sicherungen auf Systemebene {#system-level-backups}
Zusätzlich zu anwendungsspezifischen Sicherungen wird **dringend empfohlen**, Sicherungen der gesamten Systeminfrastruktur zu erstellen:
- **LXC-Container-Sicherungen**: Wenn der Betrieb in einem LXC-Container erfolgt, erstellen Sie Offline-Snapshots des gesamten Containers
- **Virtuelle Maschinen-Sicherungen**: Erstellen Sie bei VM-Bereitstellungen vollständige VM-Snapshots
- **Host-System-Sicherungen**: Beziehen Sie Docker-Volumes, Container-Konfigurationen und Systemeinstellungen ein
- **Konfigurationsdateien**: Sichern Sie alle Docker- und Systemkonfigurationsdateien separat

Sicherungen auf Systemebene bieten eine zusätzliche Sicherheitsebene und ermöglichen eine vollständige Disaster Recovery, einschließlich der Möglichkeit, die gesamte Umgebung bei Bedarf auf einen anderen Host wiederherzustellen.

## 🛠️ Voraussetzungen {#prerequisites}

- Laufendes Mail Archiver Docker-Setup (siehe [Setup Guide](Setup.md))
- Zugriff auf das Docker-Hostsystem
- Ausreichend Speicherplatz für Sicherungen

## 📦 Was Sie sichern sollten {#what-to-backup}

Die Mail Archiver Docker-Umgebung besteht aus mehreren wichtigen Datenvolumina, die gesichert werden müssen:

### 1. PostgreSQL-Datenbank {#1-postgresql-database}
- **Volume**: `./postgres-data`
- **Enthält**: Alle archivierten E-Mails, Benutzerkonten, E-Mail-Konten und Anwendungsdaten
- **Wichtigkeit**: ⭐⭐⭐ Kritisch - Dies ist Ihr Hauptdatenspeicher

### 2. Datenschutzschlüssel {#2-data-protection-keys}
- **Volume**: `./data-protection-keys`
- **Enthält**: ASP.NET Core Data Protection-Schlüssel für die Cookie-Verschlüsselung und Authentifizierung
- **Wichtigkeit**: ⭐⭐ Wichtig - Erforderlich für bestehende Benutzersitzungen und sichere Authentifizierung

### 2a. Originale Nachrichten-Dateien (nur mit `RawMessages.Storage=filesystem`) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **Volume**: `./raw-messages` (die Host-Seite von `/app/RawMessages`)
- **Enthält**: Die originale `.eml` jeder E-Mail, gzip-komprimiert, geschrieben, wenn `RawMessages:Enabled` gesetzt ist und das Storage-Backend `filesystem` ist
- **Wichtigkeit**: ⭐⭐⭐ Kritisch **bei Verwendung** - die Datenbank speichert nur die Prüfsumme, die Größe und den relativen Pfad; eine Wiederherstellung ohne dieses Verzeichnis hinterlässt Originale, die nicht bereitgestellt werden können
- Sichern Sie dieses Verzeichnis und den Datenbank-Dump **zusammen**. Eine Wiederherstellung der Datenbank ohne die Dateien ist ein Fehler, den das Archiv nicht beheben kann: Die Downloads antworten auf `404` und die Prüfsummen können aus nichts anderem wieder abgeleitet werden. Eine Wiederherstellung der Dateien ohne die Datenbank ist ebenso nutzlos, da nichts auf sie verweist.
- Wenn Sie `RawMessages.Storage=postgres` (Standard) verwenden, existiert dieses Verzeichnis nicht und die Originale befinden sich wie zuvor in `./postgres-data`.
- Weitere Informationen zur Funktion finden Sie unter [Original Message Storage](RawMessages.md) sowie zur Anleitung zum Wechseln der Backends.

### 3. Docker Compose Konfiguration {#3-docker-compose-configuration}
- **Datei**: `docker-compose.yml`
- **Enthält**: Container-Konfiguration, Umgebungsvariablen und Einstellungen
- **Wichtigkeit**: ⭐⭐ Wichtig - Enthält Ihre Anwendungskonfiguration

## 🔄 Sicherungsverfahren {#backup-procedures}

### Methode 1: Vollständige Sicherung (Empfohlen) {#method-1-complete-backup-recommended}

Diese Methode sichert alle Daten, während die Container gestoppt sind, um die Datenkonsistenz zu gewährleisten.

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

### Methode 2: Hot Backup (Minimale Ausfallzeit) {#method-2-hot-backup-minimal-downtime}

Diese Methode erstellt eine vollständige Sicherung, während das System läuft, bei minimaler Ausfallzeit.

**Hinweis:** Ersetzen Sie `mailuser` und `MailArchiver` durch Ihre tatsächlichen Datenbank-Anmeldedaten aus `docker-compose.yml` (prüfen Sie die Umgebungsvariablen `POSTGRES_USER` und `POSTGRES_DB`).

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

### Methode 3: Automatisiertes Sicherungsskript {#method-3-automated-backup-script}

Für regelmäßige automatisierte Sicherungen stellen wir ein einsatzbereites Sicherungsskript zur Verfügung. Das Skript ist unter [backup.sh](attachments/backup.sh) verfügbar.

**Funktionen:**
- ✅ Automatische Erkennung der Datenbank-Anmeldedaten aus docker-compose.yml
- ✅ Hot Backup (minimale Ausfallzeit)
- ✅ Lock-File-Mechanismus zur Vermeidung gleichzeitiger Sicherungen
- ✅ Prüfung des Festplattenspeichers vor der Sicherung
- ✅ Automatische Bereinigung alter Sicherungen

**Konfiguration:**

Bevor Sie das Skript verwenden, bearbeiten Sie den Konfigurationsabschnitt am Anfang der Datei:

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

**Einrichtung:**

1. **Kopieren Sie das Skript an Ihren gewünschten Ort:**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **Bearbeiten Sie die Konfiguration:**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # Passen Sie die Einstellungen an
   ```

3. **Machen Sie das Skript ausführbar:**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **Testen Sie das Skript manuell:**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **Überprüfen Sie die Sicherung:**
   - Überprüfen Sie das Sicherungsverzeichnis auf die erstellte `.tar.gz`-Datei
   - Überprüfen Sie die Logdatei auf Fehler oder Warnungen

**Planung automatisierter Sicherungen:**

Um automatische Sicherungen mit cron zu planen:

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

## 🔙 Wiederherstellungsverfahren {#restore-procedures}

Die Wiederherstellungsverfahren sind so konzipiert, dass sie zu den Sicherungsmethoden passen. Wählen Sie die entsprechende Wiederherstellungsmethode basierend auf der von Ihnen verwendeten Sicherungsmethode:

- **Methode 1 (Vollständige Sicherung)**: Verwenden Sie das Verfahren „Vollständige Wiederherstellung aus vollständiger Sicherung“. Diese Methode wird verwendet, wenn Sie eine vollständige Sicherung haben, die das Verzeichnis `postgres-data` enthält.
- **Methode 2 (Hot Backup)** oder **Methode 3 (Automatisiertes Sicherungsskript)**: Verwenden Sie das Verfahren „Wiederherstellung aus SQL-Dump“ (behandelt die Datenschutzschlüssel und die Datenbank). Diese Methode wird verwendet, wenn Sie ein Hot Backup haben, das den SQL-Dump der Datenbank und die Datenschutzschlüssel enthält.

Zur Erleichterung haben wir auch ein automatisiertes Wiederherstellungsskript bereitgestellt, das alle Wiederherstellungsverfahren automatisch abwickeln kann. Sie können das Skript anstelle des manuellen Befolgens der unten aufgeführten Schritte verwenden.

### Automatisiertes Wiederherstellungsskript {#automated-restore-script}

Das Wiederherstellungsskript ist in der Datei [restore.sh](attachments/restore.sh) verfügbar. Es bietet eine geführte Wiederherstellung und fragt nach der Sicherungsmethode, dem Pfad zur Sicherungs-Tar-Datei und dem Installationsverzeichnis, und führt dann den Wiederherstellungsprozess automatisch durch.

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

Das Skript führt Sie durch den Wiederherstellungsprozess und wickelt automatisch alle Schritte basierend auf der von Ihnen verwendeten Sicherungsmethode ab.

### Vollständige Wiederherstellung aus vollständiger Sicherung (Manuelle Methode) {#full-restore-from-complete-backup-manual-method}

Dies stellt alle Daten aus einem vollständigen Sicherungsarchiv wieder her. Diese Methode sollte verwendet werden, wenn Sie eine vollständige Sicherung haben, die das Verzeichnis `postgres-data` enthält (erstellt durch Methode eins).

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

### Wiederherstellung aus SQL-Dump (Manuelle Methode) {#restore-from-sql-dump-manual-method}

Dies stellt die Datenbank und die Datenschutzschlüssel aus einem Hot Backup wieder her (Methode zwei und drei).

**Hinweis:** Die unten aufgeführten Befehle verwenden Beispiel-Anmeldedaten (`mailuser` und `MailArchiver`). Sie müssen diese durch die tatsächlichen Werte aus Ihrer `docker-compose.yml`-Datei ersetzen. Überprüfen Sie die Umgebungsvariablen `POSTGRES_USER` und `POSTGRES_DB` unter dem `postgres`-Dienst.

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

## ⚠️ Wichtige Hinweise {#important-notes}

### Datenbank-Verbindungseinstellungen {#database-connection-settings}
Stellen Sie sicher, dass Ihre Datenbank-Verbindungseinstellungen in `docker-compose.yml` bei der Sicherung und Wiederherstellung übereinstimmen:
- `POSTGRES_USER` (Standard: mailuser)
- `POSTGRES_PASSWORD` (Standard: masterkey)
- `POSTGRES_DB` (Standard: MailArchiver)

### Datenschutzschlüssel {#data-protection-keys}
Das Verzeichnis `data-protection-keys` wird verwendet für:
- Benutzerauthentifizierung und Sitzungsverwaltung
- Cookie-Verschlüsselung
- Sichere Datenverarbeitung

Ohne diese Schlüssel werden bestehende Benutzersitzungen ungültig und Benutzer müssen sich erneut anmelden.

### Umgebungsvariablen {#environment-variables}
Alle Anwendungseinstellungen werden in Umgebungsvariablen in `docker-compose.yml` gespeichert. Stellen Sie sicher, dass Sie:
- Ihre `docker-compose.yml` Datei sichern
- Alle benutzerdefinierten Einstellungen dokumentieren
- Dieselbe Konfiguration während der Wiederherstellung verwenden

## 🔒 Best Practices für die Sicherheit {#security-best-practices}

1. **Backups verschlüsseln**: Erwägen Sie die Verschlüsselung von Backup-Archiven, insbesondere wenn diese sensible E-Mail-Daten enthalten.

2. **Sichere Speicherung**: Speichern Sie Backups an einem sicheren Ort:
   - Verwenden Sie einen separaten Server oder ein separates Speichersystem
   - Implementieren Sie Zugriffskontrollen
   - Erwägen Sie Off-Site- oder Cloud-Speicherung

3. **Regelmäßige Tests**: Testen Sie regelmäßig Ihre Backup- und Wiederherstellungsprozesse, um sicherzustellen, dass sie korrekt funktionieren

4. **Backup-Größe überwachen**: Behalten Sie die Größe der Backups im Auge, um sicherzustellen, dass Sie über ausreichenden Speicherplatz verfügen

5. **Backup-Aufbewahrung**: Definieren und implementieren Sie eine Richtlinie zur Backup-Aufbewahrung (z. B. tägliche Backups für 7 Tage, wöchentliche Backups für 4 Wochen, monatliche Backups für 12 Monate aufbewahren)

6. **Standardpasswörter ändern**: Die Standardpasswörter in der Konfiguration sind unsicher und sollten für den Produktivbetrieb geändert werden.

## 🖥️ Proxmox Sicherung {#proxmox-backup}

Wenn Sie die Mail Archiver Docker-Umgebung in einem Proxmox LXC-Container ausführen, wird empfohlen, den Modus „stop“ für die Sicherung des gesamten Containers zu verwenden, um die Datenkonsistenz zu gewährleisten.

### Warum der Stop-Modus empfohlen wird {#why-stop-mode-is-recommended}

1. **Datenkonsistenz**: Das Stoppen des Containers stellt sicher, dass alle Datenbanktransaktionen abgeschlossen sind und die Daten auf die Festplatte geschrieben wurden, bevor die Sicherung erstellt wird.
2. **Datenkorruption verhindern**: Das Ausführen von Sicherungen, während der Container aktiv ist, kann zu Datenkorruption führen, insbesondere bei Datenbankoperationen.
3. **Dateisystemintegrität**: Das Stoppen des Containers stellt sicher, dass alle Dateisystemoperationen abgeschlossen sind, wodurch Teil-Schreibvorgänge während der Sicherung verhindert werden.

### Proxmox Sicherungsnotizen {#proxmox-backup-notes}

- Stellen Sie immer sicher, dass Sie über ausreichend Speicherplatz für Sicherungen verfügen.
- Testen Sie Ihr Wiederherstellungsverfahren regelmäßig, um sicherzustellen, dass die Sicherungen gültig sind.
- Erwägen Sie die Einrichtung eines Sicherungsplans in der Proxmox-Weboberfläche für automatisierte Sicherungen.
- Überwachen Sie die Sicherungsprotokolle auf Fehler oder Probleme.
