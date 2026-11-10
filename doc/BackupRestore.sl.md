# 💾 Vodnik za varnostno kopiranje in obnavljanje {#backup-and-restore-guide}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodnik nudi podrobna navodila za varnostno kopiranje in obnavljanje vašega Mail Archiver Docker okolja. Redno varnostno kopiranje je ključno za zaščito vaših arhiviranih e-poštnih sporočil in konfiguracijskih podatkov.

## ⚠️ Pomembno izjava o omejitvi odgovornosti {#important-disclaimer}

**Vsi skripti in ukazi, navedeni v tem vodniku, so le primeri.** Ne prevzamamo nobene odgovornosti za njihovo funkcionalnost ali primernost za vaše specifično okolje. Vedno:
- Najprej preizkusite postopke varnostnega kopiranja in obnavljanja v neprodukcijskem okolju
- Preverite, ali so varnostne kopije popolne in zmožne obnavitve
- Prilagodite skripte in ukaze svoji specifični postavitvi in zahtevam
- Shranjujte več kopij varnostnih kopij na različnih lokacijah
- Redno preizkušajte svoje postopke za okrevanje po nesrečah

**Uporabljate na lastno odgovornost.**

## 🎯 Priporočila za varnostno kopiranje {#backup-recommendations}

### Brezžično varnostno kopiranje (možno priporočljivo) {#offline-backups-highly-recommended}
Za maksimalno doslednost in integriteto podatkov **vedno izvedite varnostno kopiranje, ko sta aplikacija in baza podatkov ustavljeni**. To zagotavlja:
- Brez aktivnih transakcij baze podatkov med kopiranjem
- Popolno doslednost podatkov v vseh datotekah
- Brez tveganja za poškodbo datotek zaradi hkrnih zapisov
- Zanesljivo sposobnost obnove

Čeprav so "hot" varnostne kopije (Metoda 2 in 3) priročne za zmanjšanje nedolga, nosijo večje tveganje za neskladnosti. Za kritična produkcijska okolja načrtujte redno brezžično varnostno kopiranje med okno za vzdrževanje.

### Varnostno kopiranje na ravni sistema {#system-level-backups}
Poleg varnostnih kopiranj specifičnih za aplikacijo je **možno priporočljivo** ustvariti varnostne kopije celotne sistemske infrastrukture:
- **Varnostne kopije LXC kontejnerjev**: Če delujete v LXC kontejnerju, naredite brezžično zajetje (snapshot) celotnega kontejnerja
- **Varnostne kopije virtualnih strojev**: Za VM postavitve naredite popolne zajetja (snapshots) VM
- **Varnostne kopije gostiteljskega sistema**: Vključite Docker volumne, konfiguracije kontejnerjev in sistemske nastavitve
- **Konfiguracijske datoteke**: Posebej varnostno kopirajte vse Docker in sistemske konfiguracijske datoteke

Varnostno kopiranje na ravni sistema zagotavlja dodatni sloj varnosti in omogoča popolno okrevanje po nesrečah, vključno s sposobnostjo obnovitve celotnega okolja na drugega gostitelja, če je to potrebno.

## 🛠️ Predpogoj {#prerequisites}

- Delujoča Mail Archiver Docker postavitev (poglejte [Vodnik za postavitev](Setup.md))
- Dostop do Docker gostiteljskega sistema
- Zadostno prostora za shranjevanje varnostnih kopij

## 📦 Kaj kopirati {#what-to-backup}

Mail Archiver Docker okolje se sestoji iz več pomembnih volumov podatkov, ki jih je treba varnostno kopirati:

### 1. PostgreSQL baza podatkov {#1-postgresql-database}
- **Volume**: `./postgres-data`
- **Vsebuje**: Vsa arhivirana e-poštna sporočila, uporabniške račune, e-poštne račune in podatke aplikacije
- **Pomembnost**: ⭐⭐⭐ Kritično - To je vaše glavno shreno mesto za podatke

### 2. Ključi za zaščito podatkov {#2-data-protection-keys}
- **Volume**: `./data-protection-keys`
- **Vsebuje**: ASP.NET Core Data Protection ključe za šifriranje piškotkov in avtentikacijo
- **Pomembnost**: ⭐⭐ Pomembno - Zahtevano za obstoječe uporabniške seje in varno avtentikacijo

### 2a. Originalne datoteke sporočil (le z `RawMessages.Storage=filesystem`) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **Volume**: `./raw-messages` (gostiteljska stran `/app/RawMessages`)
- **Vsebuje**: Originalne `.eml` vsakega e-poštnega sporočila, v obliki gzip, zapisane, ko je `RawMessages:Enabled` nastavljeno in je storage backend `filesystem`
- **Pomembnost**: ⭐⭐⭐ Kritično **ko se uporablja** - baza podatkov shranjuje le kontrolno vrednost (checksum), velikost in relativno pot, zato ob obnovi brez te direktorija ostanejo originali, ki jih ni mogoče servirati
- Varnostno kopirajte to direktorij in izvoz baze podatkov **skupaj**. Obnova baze podatkov brez datotek ni napaka, ki bi jo lahko arhiv popravil: prenos (download) bo odgovoril na `404` in kontrolnih vrednosti ni mogoče ponovno izvedeti iz ničesar drugega. Obnova datotek brez baze podatkov je prav tako neuporabna, saj jih nič ne povezuje.
- Če uporabljate `RawMessages.Storage=postgres` (privzeto), ta direktorij ne obstaja in se originali nahajajo v `./postgres-data` kot prej.
- Za samo funkcijo in način menjave backendov poglejte [Shranjevanje originalnih sporočil](RawMessages.md).

### 3. Docker Compose konfiguracija {#3-docker-compose-configuration}
- **Datoteka**: `docker-compose.yml`
- **Vsebuje**: Konfiguracijo kontejnerjev, okoljske spremenljivke in nastavitve
- **Pomembnost**: ⭐⭐ Pomembno - Vsebuje vašo konfiguracijo aplikacije

## 🔄 Postopki varnostnega kopiranja {#backup-procedures}

### Metoda 1: Popolno varnostno kopiranje (Priporočeno) {#method-1-complete-backup-recommended}

Ta metoda varnostno kopira vse podatke, medtem ko so kontejnerji ustavljeni, da zagotovi doslednost podatkov.

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

### Metoda 2: Hot Backup (Minimalen nedolga) {#method-2-hot-backup-minimal-downtime}

Ta metoda ustvari popolno varnostno kopijo, medtem ko sistem deluje, z minimalno nedolgo.

**Opomba:** Zamenjajte `mailuser` in `MailArchiver` s svojimi dejanskimi podatki za dostop do baze iz `docker-compose.yml` (preverite okoljske spremenljivke `POSTGRES_USER` in `POSTGRES_DB`).

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

### Metoda 3: Avtomatiziran skript za varnostno kopiranje {#method-3-automated-backup-script}

Za redno avtomatizirano varnostno kopiranje vam na voljo imamo skript, ki je pripravljen za uporabo. Skript je na voljo v [backup.sh](attachments/backup.sh).

**Funkcije:**
- ✅ Samodejno zaznavanje poveril baze podatkov iz docker-compose.yml
- ✅ Hot backup (minimalna nedolga)
- ✅ Mehanizem datoteke za zaklepanje (lock file), ki preprečuje hkrne varnostne kopije
- ✅ Preverjanje prostora na disku pred kopiranjem
- ✅ Samodejno čiščenje starih varnostnih kopij

**Konfiguracija:**

Pred uporabo skripta uredite razdelek za konfiguracijo na vrhu datoteke:

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

**Postavitev:**

1. **Kopirajte skript na želeno lokacijo:**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **Uredite konfiguracijo:**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # Prilagodite nastavitve
   ```

3. **Skript naredite izvršljivo:**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **Skript preizkusite ročno:**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **Preverite varnostno kopijo:**
   - Preverite direktorij za varnostno kopiranje za ustvarjeno `.tar.gz` datoteko
   - Preglejte datoteko dnevnika (log) za morebitne napake ali opozorila

**Načrtovanje avtomatiziranih varnostnih kopiranj:**

Za načrtovanje avtomatskih varnostnih kopiranj z uporabo cron:

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

## 🔙 Postopki obnove {#restore-procedures}

Postopki obnove so zasnovani tako, da ustrezajo metodam varnostnega kopiranja. Izberite ustrezno metodo obnove glede na to, katero metodo kopiranja ste uporabili:

- **Metoda 1 (Popolno varnostno kopiranje)**: Uporabite postopek "Full Restore from Complete Backup". To metodo uporabite, ko imate popolno varnostno kopijo, ki vključuje direktorij `postgres-data`.
- **Metoda 2 (Hot Backup)** ali **Metoda 3 (Avtomatiziran skript za varnostno kopiranje)**: Uporabite postopek "Restore from SQL Dump" (obdela ključe za zaščito podatkov in bazo podatkov). To metodo uporabite, ko imate hot backup, ki vključuje SQL izvoz baze podatkov in ključe za zaščito podatkov.

Za olajšanje smo pripravili tudi avtomatiziran skript za obnovo, ki lahko samodejno obdela vse postopke obnove. Namesto ročnega sledenja spodnjim korakom lahko uporabite skript.

### Avtomatiziran skript za obnovo {#automated-restore-script}

Skript za obnovo je na voljo v datoteki [restore.sh](attachments/restore.sh). Zagotavlja vodeno obnovo in vpraša za metodo varnostnega kopiranja, pot do tar datoteke varnostne kopije in direktorij namestitve, nato pa samodejno izvede postopek obnove.

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

Skript vas bo vodil skozi postopek obnove in samodejno obdelal vse korake na podlagi metode varnostnega kopiranja, ki ste jo uporabili.

### Popolna obnova iz popolne varnostne kopije (Ročna metoda) {#full-restore-from-complete-backup-manual-method}

To obnovi vse podatke iz arhivne popolne varnostne kopije. To metodo uporabite, ko imate popolno varnostno kopijo, ki vključuje direktorij `postgres-data` (ustvarjen z metodo številka ena).

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

### Obnova iz SQL izvoza (Ročna metoda) {#restore-from-sql-dump-manual-method}

To obnovi bazo podatkov in ključe za zaščito podatkov iz hot backup (Metoda dva in tri).

**Opomba:** Spodnji ukazi uporabljajo primer kredencialov (`mailuser` in `MailArchiver`). Te morate zamenjati z dejanskimi vrednostmi iz vaše datoteke `docker-compose.yml`. Preverite okoljske spremenljivke `POSTGRES_USER` in `POSTGRES_DB` pod storitvijo `postgres`.

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


## ⚠️ Pomembne opombe {#important-notes}

### Nastavitve povezave z bazo podatkov {#database-connection-settings}
Zagotovite, da se vaše nastavitve povezave z bazo podatkov v `docker-compose.yml` ujimajo med varnostnim kopiranjem in obnovo:
- `POSTGRES_USER` (privzeto: mailuser)
- `POSTGRES_PASSWORD` (privzeto: masterkey)
- `POSTGRES_DB` (privzeto: MailArchiver)

### Ključi za zaščito podatkov {#data-protection-keys}
Direktorij `data-protection-keys` se uporablja za:
- Avtentikacijo uporabnikov in upravljanje sej
- Šifriranje piškotkov
- Varno rokovanje z podatki

Brez teh ključev bodo obstoječe uporabniške seje veljavne in uporabniki se bodo morali ponovno prijaviti.

### Okoljske spremenljivke {#environment-variables}
Vse nastavitve aplikacije so shranjene v okoljskih spremenljivkah v `docker-compose.yml`. Poskrbite, da:
- Varnostno kopirate svojo datoteko `docker-compose.yml`
- Dokumentirate vse prilagojene nastavitve
- Med obnovo uporabite isto konfiguracijo

## 🔒 Najboljše prakse za varnost {#security-best-practices}

1. **Šifriranje varnostnih kopij**: Razmislite o šifriranju arhivov varnostnih kopij, še posebej, če vsebujejo občutljive e-poštne podatke.

2. **Varno shranjevanje**: Varnostne kopije shranjujte na varni lokaciji:
   - Uporabite ločen strežnik ali sistem za shranjevanje
   - Implementirajte nadzor dostopa
   - Razmislite o shranjevanju zunaj lokacije ali v oblaku

3. **Redno testiranje**: Redno preizkušajte svoje postopke varnostnega kopiranja in obnove, da zagotovite njihovo pravilno delovanje.

4. **Spremljanje velikosti varnostnih kopij**: Sledite velikosti varnostnih kopij, da zagotovite zadostno prostora za shranjevanje.

5. **Obdržavanje varnostnih kopij**: Določite in implementirajte politiko obdržavanja varnostnih kopij (npr. obdržati dnevne kopije za 7 dni, tedenske kopije za 4 tedne, mesečne kopije za 12 mesecev).

6. **Zamenjava privzetih gesel**: Privzeta gesla v konfiguraciji niso varna in jih je treba za produkcijo spremeniti.


## 🖥️ Proxmox Backup {#proxmox-backup}

Pri delovanju Mail Archiver Docker okolja v Proxmox LXC kontejnerju se priporoča uporaba načina "stop" za varnostno kopiranje celotnega kontejnerja, da zagotovite doslednost podatkov.

### Zakaj je priporočen način Stop {#why-stop-mode-is-recommended}

1. **Doslednost podatkov**: Ustavitev kontejnerja zagotovi, da so vse transakcije baze podatkov zaključene in so podatki zapisani na disk, preden se izvede varnostna kopija.
2. **Preprečevanje poškodbe podatkov**: Izvajanje varnostnih kopij, medtem ko je kontejner aktiven, lahko privede do poškodbe podatkov, zlasti pri operacijah baze podatkov.
3. **Integriteta datotečnega sistema**: Ustavitev kontejnerja zagotovi, da so vse operacije datotečnega sistema zaključene, kar preprečuje delne zapise med kopiranjem.

### Opombe za Proxmox Backup {#proxmox-backup-notes}

- Vedno zagotovite, da imate zadostno prostora za shranjevanje varnostnih kopij.
- Redno preizkušajte postopek obnove, da zagotovite veljavnost varnostnih kopij.
- Razmislite o nastavitvi načrta varnostnega kopiranja v Proxmox spletnem vmesniku za avtomatizirane kopije.
- Spremljajte dnevnike varnostnega kopiranja za morebitne napake ali težave.
