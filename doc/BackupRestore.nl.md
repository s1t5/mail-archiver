# 💾 Backup and Restore Guide {#backup-and-restore-guide}

[← Terug naar de Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Deze gids biedt gedetailleerde instructies voor het maken van een back-up en het uitvoeren van een herstel van uw Mail Archiver Docker-omgeving. Regelmatige back-ups zijn essentieel om uw gearchiveerde e-mails en configuratiegegevens te beschermen.

## ⚠️ Belangrijke Disclaimer {#important-disclaimer}

**Alle scripts en commando's in deze gids zijn uitsluitend voorbeelden.** Er wordt geen garantie of aansprakelijkheid aanvaard voor de functionaliteit of geschiktheid ervan voor uw specifieke omgeving. Doe altijd het volgende:
- Test back-up en herstelprocedures eerst in een niet-productieomgeving
- Verifieer of back-ups volledig en herstelbaar zijn
- Pas scripts en commando's aan uw specifieke opstelling en vereisten aan
- Bewaar meerdere back-upkopieën op verschillende locaties
- Test regelmatig uw procedures voor rampenherstel

**Gebruik op eigen risico.**

## 🎯 Back-up Aanbevelingen {#backup-recommendations}

### Offline Back-ups (Sterk aanbevolen) {#offline-backups-highly-recommended}
Voor maximale dataconsistentie en integriteit, **voer altijd back-ups uit terwijl de applicatie en database zijn gestopt**. Dit zorgt voor:
- Geen actieve database-transacties tijdens de back-up
- Volledige dataconsistentie over alle bestanden
- Geen risico op bestandscorruptie door gelijktijdige schrijfacties
- Betrouwbare herstelmogelijkheid

Hoewel hot back-ups (Methode 2 en 3) handig zijn om downtime te minimaliseren, brengen ze een hoger risico op inconsistenties met zich mee. Plan voor kritieke productieomgevingen regelmatige offline back-ups tijdens onderhoudsvensters.

### Back-ups op Systeemniveau {#system-level-backups}
Naast applicatiespecifieke back-ups wordt het **sterk aanbevolen** om back-ups te maken van de gehele systeeminfrastructuur:
- **LXC Container Back-ups**: Als u in een LXC-container werkt, maak dan offline snapshots van de gehele container
- **Virtual Machine Back-ups**: Maak voor VM-implementaties volledige VM-snapshots
- **Host Systeem Back-ups**: Neem Docker volumes, containerconfiguraties en systeeminstellingen op
- **Configuratiebestanden**: Maak apart back-ups van alle Docker- en systeemconfiguratiebestanden

Back-ups op systeemniveau bieden een extra veiligheidslaag en maken volledig rampenherstel mogelijk, inclusief de mogelijkheid om de gehele omgeving naar een andere host te herstellen indien nodig.

## 🛠️ Vereisten {#prerequisites}

- Draaiende Mail Archiver Docker-opstelling (zie [Setup Guide](Setup.md))
- Toegang tot het Docker host-systeem
- Voldoende opslagruimte voor back-ups

## 📦 Wat te back-uppen {#what-to-backup}

De Mail Archiver Docker-omgeving bestaat uit verschillende belangrijke datavolumes die moeten worden geback-upt:

### 1. PostgreSQL Database {#1-postgresql-database}
- **Volume**: `./postgres-data`
- **Bevat**: Alle gearchiveerde e-mails, gebruikersaccounts, mailaccounts en applicatiegegevens
- **Belang**: ⭐⭐⭐ Kritiek - Dit is uw belangrijkste gegevensopslag

### 2. Data Protection Keys {#2-data-protection-keys}
- **Volume**: `./data-protection-keys`
- **Bevat**: ASP.NET Core Data Protection keys voor cookie-encryptie en authenticatie
- **Belang**: ⭐⭐ Belangrijk - Vereist voor bestaande gebruikerssessies en veilige authenticatie

### 2a. Originele Berichtbestanden (alleen met `RawMessages.Storage=filesystem`) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **Volume**: `./raw-messages` (de host-zijde van `/app/RawMessages`)
- **Bevat**: Het originele `.eml` van elke e-mail, gzip-gecomprimeerd, geschreven wanneer `RawMessages:Enabled` is ingesteld en de storage backend `filesystem` is
- **Belang**: ⭐⭐⭐ Kritiek **wanneer in gebruik** - de database houdt alleen de checksum, grootte en relatieve pad bij, dus een herstel zonder deze directory laat originelen achter die niet kunnen worden geserveerd
- Back-up deze directory en de database dump **samen**. Het herstellen van de database zonder de bestanden is geen fout die de archief kan repareren: de downloads beantwoorden `404` en de checksums kunnen niet uit iets anders worden afgeleid. Het herstellen van de bestanden zonder de database is even nutteloos, omdat niets naar hen verwijst.
- Als u `RawMessages.Storage=postgres` gebruikt (de standaard), bestaat deze directory niet en bevinden de originelen zich in `./postgres-data` zoals voorheen.
- Zie [Original Message Storage](RawMessages.md) voor de functie zelf en hoe u van backend kunt wisselen.

### 3. Docker Compose Configuratie {#3-docker-compose-configuration}
- **File**: `docker-compose.yml`
- **Bevat**: Containerconfiguratie, omgevingsvariabelen en instellingen
- **Belang**: ⭐⭐ Belangrijk - Bevat uw applicatieconfiguratie

## 🔄 Back-up Procedures {#backup-procedures}

### Methode 1: Volledige Back-up (Aanbevolen) {#method-1-complete-backup-recommended}

Deze methode maakt een back-up van alle gegevens terwijl de containers zijn gestopt om dataconsistentie te garanderen.

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

### Methode 2: Hot Back-up (Minimale Downtime) {#method-2-hot-backup-minimal-downtime}

Deze methode maakt een volledige back-up terwijl het systeem draait met minimale downtime.

**Let op:** Vervang `mailuser` en `MailArchiver` door uw werkelijke database-inloggegevens uit `docker-compose.yml` (controleer de `POSTGRES_USER` en `POSTGRES_DB` omgevingsvariabelen).

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

### Methode 3: Geautomatiseerd Back-up Script {#method-3-automated-backup-script}

Voor regelmatige geautomatiseerde back-ups bieden wij een direct bruikbaar back-up script aan. Het script is beschikbaar in [backup.sh](attachments/backup.sh).

**Kenmerken:**
- ✅ Automatische detectie van database-inloggegevens uit docker-compose.yml
- ✅ Hot back-up (minimale downtime)
- ✅ Lock file-mechanisme om gelijktijdige back-ups te voorkomen
- ✅ Controle van schijfruimte voor de back-up
- ✅ Automatische opschoning van oude back-ups

**Configuratie:**

Voordat u het script gebruikt, bewerkt u de configuratiesectie bovenaan het bestand:

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

1. **Kopieer het script naar de gewenste locatie:**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **Bewerk de configuratie:**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # Pas de instellingen aan
   ```

3. **Maak het script uitvoerbaar:**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **Test het script handmatig:**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **Verifieer de back-up:**
   - Controleer de back-up directory op het aangmaakte `.tar.gz` bestand
   - Bekijk het logbestand op eventuele fouten of waarschuwingen

**Plannen van Geautomatiseerde Back-ups:**

Om automatische back-ups te plannen met cron:

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

## 🔙 Herstel Procedures {#restore-procedures}

De herstelprocedures zijn ontworpen om overeen te komen met de back-upmethoden. Kies de juiste herstelmethode op basis van de back-upmethode die u heeft gebruikt:

- **Methode 1 (Volledige Back-up)**: Gebruik de procedure "Full Restore from Complete Backup". Deze methode wordt gebruikt wanneer u een volledige back-up heeft die de `postgres-data` directory bevat.
- **Methode 2 (Hot Back-up)** of **Methode 3 (Geautomatiseerd Back-up Script)**: Gebruik de procedure "Restore from SQL Dump" (behandelt de data protection keys en de database). Deze methode wordt gebruikt wanneer u een hot back-up heeft die de database SQL dump en data protection keys bevat.

Voor het gemak hebben we ook een geautomatiseerd herstelscript toegevoegd dat alle herstelprocedures automatisch kan afhandelen. U kunt het script gebruiken in plaats van handmatig de onderstaande stappen te volgen.

### Geautomatiseerd Herstel Script {#automated-restore-script}

Het herstelscript is beschikbaar in het [restore.sh](attachments/restore.sh) bestand. Het biedt een begeleid herstel en vraagt om de back-upmethode, het pad naar het back-up tar-bestand en de installatie-directory, en voert vervolgens het herstelproces automatisch uit.

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

Het script begeleidt u door het herstelproces en handelt automatisch alle stappen af op basis van de back-upmethode die u heeft gebruikt.

### Full Restore from Complete Backup (Handmatige Methode) {#full-restore-from-complete-backup-manual-method}

Dit herstelt alle gegevens uit een volledige back-up archief. Deze methode moet worden gebruikt wanneer u een volledige back-up heeft die de `postgres-data` directory bevat (gemaakt door methode één).

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

### Restore from SQL Dump (Handmatige Methode) {#restore-from-sql-dump-manual-method}

Dit herstelt de database en data protection keys vanuit een hot back-up (Methode twee en drie).

**Let op:** De onderstaande commando's gebruiken voorbeeld-inloggegevens (`mailuser` en `MailArchiver`). U moet deze vervangen door de werkelijke waarden uit uw `docker-compose.yml` bestand. Controleer de `POSTGRES_USER` en `POSTGRES_DB` omgevingsvariabelen onder de `postgres` service.

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


## ⚠️ Belangrijke Opmerkingen {#important-notes}

### Database Verbindingsinstellingen {#database-connection-settings}
Zorg ervoor dat uw database-verbindingsinstellingen in `docker-compose.yml` overeenkomen tussen back-up en herstel:
- `POSTGRES_USER` (standaard: mailuser)
- `POSTGRES_PASSWORD` (standaard: masterkey)
- `POSTGRES_DB` (standaard: MailArchiver)

### Data Protection Keys {#data-protection-keys}
De `data-protection-keys` directory wordt gebruikt voor:
- Gebruikersauthenticatie en sessiebeheer
- Cookie-encryptie
- Veilige gegevensverwerking

Zonder deze keys worden bestaande gebruikerssessies ongeldig gemaakt en moeten gebruikers opnieuw inloggen.

### Omgevingsvariabelen {#environment-variables}
Alle applicatie-instellingen worden opgeslagen in omgevingsvariabelen in `docker-compose.yml`. Zorg ervoor dat u:
- Uw `docker-compose.yml` bestand back-upt
- Eventuele aangepaste instellingen documenteert
- Dezelfde configuratie gebruikt tijdens het herstel

## 🔒 Best Practices voor Beveiliging {#security-best-practices}

1. **Back-ups Versleutelen**: Overweeg het versleutelen van back-up archieven, vooral als ze gevoelige e-mailgegevens bevatten.

2. **Veilige Opslag**: Bewaar back-ups op een veilige locatie:
   - Gebruik een aparte server of opslagsysteem
   - Implementeer toegangscontroles
   - Overweeg off-site of cloud-opslag

3. **Regelmatig Testen**: Test regelmatig uw back-up en herstelprocedures om te controleren of ze correct werken

4. **Monitor Back-up Grootte**: Houd de grootte van back-ups bij om er zeker van te zijn dat u voldoende opslagruimte heeft

5. **Back-up Bewaring**: Definieer en implementeer een beleid voor de Bewaring van back-ups (bijv. dagelijkse back-ups voor 7 dagen bewaren, wekelijkse back-ups voor 4 weken, maandelijkse back-ups voor 12 maanden)

6. **Standaard Wachtwoorden Wijzigen**: De standaard wachtwoorden in de configuratie zijn onveilig en moeten worden gewijzigd voor productiegebruik.


## 🖥️ Proxmox Back-up {#proxmox-backup}

Wanneer u de Mail Archiver Docker-omgeving in een Proxmox LXC-container draait, wordt het aanbevolen om de "stop" modus te gebruiken voor het back-uppen van de gehele container om dataconsistentie te garanderen.

### Waarom de Stop Modus wordt aanbevolen {#why-stop-mode-is-recommended}

1. **Dataconsistentie**: Het stoppen van de container zorgt ervoor dat alle database-transacties zijn voltooid en gegevens naar de schijf zijn geschreven voordat de back-up wordt gemaakt.
2. **Voorkom Datacorruptie**: Het uitvoeren van back-ups terwijl de container actief is, kan leiden tot datacorruptie, vooral bij database-operaties.
3. **Integriteit van het Bestandssysteem**: Het stoppen van de container zorgt ervoor dat alle bestandssysteem-operaties zijn voltooid, wat gedeeltelijke schrijfacties tijdens de back-up voorkomt.

### Proxmox Back-up Opmerkingen {#proxmox-backup-notes}

- Zorg er altijd voor dat u voldoende opslagruimte heeft voor back-ups.
- Test regelmatig uw herstelprocedure om te controleren of back-ups geldig zijn.
- Overweeg om een back-up schema in te stellen in de Proxmox webinterface voor geautomatiseerde back-ups.
- Monitor de back-up logs op eventuele fouten of problemen.
