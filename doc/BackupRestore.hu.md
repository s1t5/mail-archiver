# 💾 Backup and Restore Guide {#backup-and-restore-guide}

[← Vissz a dokumentáció indexre](index.md)

## 📋 Áttekintés {#overview}

Ez a útmutató részletes utasításokat nyújt a Mail Archiver Docker környezetének mentéséhez (backup) és helyreállításához (restore). A rendszeres mentések elengedhetetlen az archívált e-mailek és a konfigurációs adatok védelme érdekében.

## ⚠️ Fontos nyilatkozat {#important-disclaimer}

**Ebben az útmutatóban megadott összes szkript és parancs csak példák.** Nem vállalunk garanciát vagy felelősséget azok működéséért vagy az Ön specifikus környezetében való alkalmasságukért. Mindig:
- Tesztelje a mentési és helyreállítási folyamatokat először nem-termelési környezetben
- Ellenőrizze, hogy a mentések teljes értékűek-e és visszaállíthatók-e
- Alkalmazza a szkripteket és parancsokat a saját beállításaihoz és követelményeihez
- Tartson meg több mentési másolatot különböző helyeken
- Rendszeresen tesztelje a katasztrófahelyzeti helyreállítási folyamatait

**A használat saját felelősségére történik.**

## 🎯 Mentési ajánlások {#backup-recommendations}

### Offline mentések (Erősen ajánlott) {#offline-backups-highly-recommended}
A maximális adatkonzisztencia és integritás érdekében **mindig az alkalmazás és az adatbázis leállítása után végezzen mentést**. Ez biztosítja:
- Nincs aktív adatbázis-tranzakció a mentés alatt
- Teljes adatkonzisztencia az összes fájl között
- Nincs a fájlok sérülésének kockázata a folyamatos írás miatt
- Megbízható visszaállítási képesség

Bár a "hot backup" módszerek (2. és 3. módszer) kényelmesek a leállási idő minimalizálásához, nagyobb az inkonszitencia kockázata. Kritikus termelési környezetek esetén ütemezze a rendszeres offline mentéseket karbantartási ablakokban.

### Rendszer szintű mentések {#system-level-backups}
Az alkalmazásonkénti mentéseken túl **erősen javasolt** az egész rendszerinfrastruktúra mentése is:
- **LXC Container mentések**: Ha LXC konténerben fut, készítsen offline snapshotokat az egész konténerről
- **Virtuális gép mentések**: VM telepítéseknél készítsen teljes VM snapshotokat
- **Host rendszer mentések**: Tartalmazza a Docker volume-okat, a konténerkonfigurációkat és a rendszerbeállításokat
- **Konfigurációs fájlok**: Mentse külön a Docker és a rendszer konfigurációs fájljait

A rendszer szintű mentések extra biztonsági réteget biztosítanak, és lehetővé teszik a teljes katasztrófahelyzeti helyreállítást, beleértve azt is, hogy szükség esetén az egész környezet egy másik hostra is visszaállítható legyen.

## 🛠️ Előfeltételek {#prerequisites}

- Futó Mail Archiver Docker telepítés (lásd: [Setup Guide](Setup.md))
- Hozzáférés a Docker host rendszerhez
- Eléglelt tárhely a mentésekhez

## 📦 Mit kell menteni {#what-to-backup}

A Mail Archiver Docker környezet több fontos adatvolume-ból áll, amelyeket menteni kell:

### 1. PostgreSQL adatbázis {#1-postgresql-database}
- **Volume**: `./postgres-data`
- **Tartalmaz**: Minden archívált e-mailt, felhasználói fiókot, posta fiókot és alkalmazásadatot
- **Fontossága**: ⭐⭐⭐ Kritikus - Ez a fő adatbázisod

### 2. Data Protection kulcsok {#2-data-protection-keys}
- **Volume**: `./data-protection-keys`
- **Tartalmaz**: ASP.NET Core Data Protection kulcsokat a cookie titkosításhoz és hitelesítéshez
- **Fontossága**: ⭐⭐ Fontos - Szükséges a meglévő felhasználói munkamenetekhez és a biztonságos hitelesítéshez

### 2a. Eredeti üzenet fájlok (csak `RawMessages.Storage=filesystem` esetén) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **Volume**: `./raw-messages` (a `/app/RawMessages` host oldala)
- **Tartalmaz**: Minden e-mail eredeti `.eml`-ét, gzip-komprimálva, amelyet akkor ír a rendszer, ha a `RawMessages:Enabled` be van állítva és a tároló backend `filesystem`
- **Fontossága**: ⭐⭐⭐ Kritikus **használat esetén** - az adatbázis csak a checksumot, a méretet és a relatív elérési utat tárolja, így egy ezen iránytár nélkül végzett visszaállítás olyan eredeti fájlokat hagy maga után, amelyeket nem lehet kiszolgálni
- Ezt a könyvtárat és az adatbázis mentését (dump) **együtt** mentse! Az adatbázis visszaállítása a fájlok nélkül nem olyan hiba, amit az archívum képes kijavítani: a letöltések `404`-öt várják, a checksumokat pedig semmilyen másból nem lehet újra előállítani. A fájlok visszaállítása az adatbázis nélkül ugyanilyen hiábavaló, mert semmi sem hivatkozik rájuk.
- Ha a `RawMessages.Storage=postgres` (alapértelmezett) opciót használja, ez a könyvtár nem létezik, és az eredetiek korábban, mint `./postgres-data`-ben vannak.
- Lásd a [Original Message Storage](RawMessages.md) részt a funkcióról és a backend váltásáról.

### 3. Docker Compose konfiguráció {#3-docker-compose-configuration}
- **File**: `docker-compose.yml`
- **Tartalmaz**: Konténerkonfigurációt, környezeti változókat és beállításokat
- **Fontossága**: ⭐⭐ Fontos - Tartalmazza az alkalmazás konfigurációját

## 🔄 Mentési folyamatok {#backup-procedures}

### 1. módszer: Teljes mentés (Ajánlott) {#method-1-complete-backup-recommended}

Ez a módszer minden adatot ment meg, miközben a konténerek le vannak állítva, biztosítva az adatkonzisztenciát.

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

### 2. módszer: Hot Backup (Minimális leállási idő) {#method-2-hot-backup-minimal-downtime}

Ez a módszer teljes mentést készít, miközben a rendszer fut, minimális leállási idővel.

**Megjegyzés:** Az `mailuser` és `MailArchiver` helyére az `docker-compose.yml` fájlból származó tényleges adatbázis hitelesítő adatait kell írni (ellenőrizze az `POSTGRES_USER` és `POSTGRES_DB` környezeti változókat).

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

### 3. módszer: Automatizált mentési szkript {#method-3-automated-backup-script}

Rendszeres, automatizált mentésekhez egy készen használható mentési szkriptet biztosítunk. A szkript elérhető a [backup.sh](attachments/backup.sh) fájlban.

**Funkciók:**
- ✅ Automatikus adatbázis hitelesítő adat felismerés a docker-compose.yml fájlból
- ✅ Hot backup (minimális leállási idő)
- ✅ Lock file mechanizmus az egyszerre futó mentések megakadályozására
- ✅ Tárhely ellenőrzése a mentés előtt
- ✅ Régi mentések automatikus törlése

**Konfiguráció:**

A szkript használata előtt szerkeszd a fájl tetején található konfigurációs részt:

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

**Beállítás:**

1. **Másolja a szkriptet a kívánt helyre:**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **Szerkeszd a konfigurációt:**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # Alkalmazza a beállításokat
   ```

3. **Tegyen futtathatóvá a szkriptet:**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **Tesztelje a szkriptet manuálisan:**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **Ellenőrizze a mentést:**
   - Ellenőrizze a mentési könyvtárat a létrehozott `.tar.gz` fájl tekintetében
   - Olvassa át a log fájlt a hibák vagy figyelmeztetések után

**Automatizált mentések ütemezése:**

Az automatikus mentések ütemezéséhez használja a cron-t:

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

## 🔙 Helyreállítási folyamatok {#restore-procedures}

A helyreállítási folyamatok úgy lettek kialakítva, hogy megfeleljenek a mentési módszereknek. Válasszon megfelelő helyreállítási módszert attól függően, hogy melyik mentési módszert használt:

- **1. módszer (Teljes mentés)**: Használja a "Full Restore from Complete Backup" folyamatot. Ezt akkor használja, ha rendelkezik olyan teljes mentéssel, amely tartalmazza a `postgres-data` könyvtárat is.
- **2. módszer (Hot Backup)** vagy **3. módszer (Automatizált mentési szkript)**: Használja a "Restore from SQL Dump" folyamatot (ez kezeli a data protection kulcsokat és az adatbázist). Ezt akkor használja, ha rendelkezik olyan hot backup-tal, amely tartalmazza az adatbázis SQL dump fájlt és a data protection kulcsokat.

A kényelem érdekében egy automatizált helyreállítási szkriptet is biztosítottunk, amely minden helyreállítási folyamatot automatikusan kezel. A szkriptet használhatja az alább leírt lépések manuális követése helyett.

### Automatizált helyreállítási szkript {#automated-restore-script}

A helyreállítási szkript elérhető a [restore.sh](attachments/restore.sh) fájlban. Ez vezetési útmutatót nyújt a helyreállításhoz, megkérdezi a mentési módszert, a mentési tar fájl elérési útját és a telepítési könyvtárat, majd automatikusan végrehajtja a helyreállítási folyamatot.

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

A szkript végigvezeti Önt a helyreállítási folyamaton, és az Ön által használt mentési módszer alapján automatikusan elvégzi az összes lépést.

### Teljes helyreállítás teljes mentésből (Manuális módszer) {#full-restore-from-complete-backup-manual-method}

Ez egy teljes mentési archívumból állítja vissza az összes adatot. Ezt a módszert akkor kell használni, ha rendelkezik olyan teljes mentéssel, amely tartalmazza a `postgres-data` könyvtárat is (amit az első módszer hozott létre).

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

### Helyreállítás SQL Dump-ból (Manuális módszer) {#restore-from-sql-dump-manual-method}

Ez az adatbázist és a data protection kulcsokat állítja vissza egy hot backup-ból (2. és 3. módszer).

**Megjegyzés:** Az alábbi parancsok példa hitelesítő adatokat használnak (`mailuser` és `MailArchiver`). Ezeket ki kell cserélnie a saját `docker-compose.yml` fájljában található tényleges értékekre. Ellenőrizze a `POSTGRES_USER` és `POSTGRES_DB` környezeti változókat a `postgres` szolgáltatás alatt.

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


## ⚠️ Fontos megjegyzések {#important-notes}

### Adatbázis-kapcsolati beállítások {#database-connection-settings}
Győződjön meg arról, hogy az `docker-compose.yml` fájlban megadott adatbázis-kapcsolati beállításai azonosak a mentés és a helyreállítás során:
- `POSTGRES_USER` (alapértelmezett: mailuser)
- `POSTGRES_PASSWORD` (alapértelmezett: masterkey)
- `POSTGRES_DB` (alapértelmezett: MailArchiver)

### Data Protection kulcsok {#data-protection-keys}
A `data-protection-keys` könyvtárat a következőkre használják:
- Felhasználói hitelesítés és munkamenetkezelés
- Cookie titkosítás
- Biztonságos adatkezelés

Ezek nélkül a meglévő felhasználói munkamenetek érvénytelenekké válnak, és a felhasználóknak újra be kell lépniük.

### Környezeti változók {#environment-variables}
Minden alkalmazásbeállítás az `docker-compose.yml` fájlban, környezeti változóként tárolódik. Gondoskodjon a következőkről:
- Mentse a `docker-compose.yml` fájlt
- Dokumentálja az összes egyedi beállítást
- Használja ugyanazt a konfigurációt a helyreállítás során

## 🔒 Biztonsági legjobb gyakorlatok {#security-best-practices}

1. **Mentések titkosítása**: Fontoljonാ növelni a mentési archívumok titkosítását, különösen, ha érzékeny e-mail adatokat tartalmaznak.

2. **Biztonságos tárolás**: A mentéseket biztonságos helyen tárolja:
   - Használjon külön szervert vagy tárolórendszert
   - Implementáljon hozzáférés-kezelést
   - Fontolóra vegye a hely خارج (off-site) vagy felhő alapú tárolást

3. **Rendszeres tesztelés**: Rendszeresen tesztelje a mentési és helyreállítási folyamatait annak biztosítása érdekében, hogy megfelelően működnek-e.

4. **A mentések méretének figyelése**: Kövesse nyomon a mentések méretét, hogy biztosítsa a megfelelő tárhely rendelkezését.

5. **Mentések megőrzési ideje**: Határozzon meg és alkalmazzon egy mentési megőrzési irányelveket (pl. napi mentések 7 napig, heti mentések 4 hétig, havi mentések 12 hónapig).

6. **Alapértelmezett jelszavak megváltoztatása**: A konfigurációban található alapértelmezett jelszavak nem biztonságosak, és termelési használat esetén meg kell változtatni őket.


## 🖥️ Proxmox Backup {#proxmox-backup}

Ha a Mail Archiver Docker környezet Proxmox LXC konténerben fut, javasolt a "stop" módot használni az egész konténer mentéséhez az adatkonzisztencia érdekében.

### Miért ajánlott a Stop mód? {#why-stop-mode-is-recommended}

1. **Adatkonzisztencia**: A konténer leállítása biztosítja, hogy az összes adatbázis-tranzakció befejeződjön, és az adatok leíródjanak a lemezre a mentés előtt.
2. **Adatkorrupció megelőzése**: A mentés futtatása közben, amíg a konténer aktív, adatkorrupcióhoz vezethet, különösen az adatbázis-műveletek esetén.
3. **Fájlrendszer integritás**: A konténer leállítása biztosítja, hogy minden fájlrendszeri művelet befejeződjön, megakadályozva a részleges írásokat a mentés alatt.

### Proxmox mentési megjegyzések {#proxmox-backup-notes}

- Mindig győződjön meg arról, hogy elegendő tárhelye van a mentésekhez.
- Rendszeresen tesztelje a helyreállítási folyamatát, hogy biztosítsa a mentések érvényességét.
- Fontolóra vegye a Proxmox web interfészben egy mentési ütemezés beállítását az automatizált mentések érdekében.
- Figyelje a mentési naplók (logs) hibáit vagy problémáit.
