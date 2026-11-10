# 💾 Przewodnik po Kopiach Zapasowych i Przywracaniu {#backup-and-restore-guide}

[← Powrót do Indeksu Dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik zawiera szczegółowe instrukcje dotyczące tworzenia kopii zapasowych i przywracania środowiska Mail Archiver Docker. Regularne kopie zapasowe są niezbędne do ochrony zarchiwizowanych wiadomości e-mail oraz danych konfiguracyjnych.

## ⚠️ Ważne zastrzeżenie {#important-disclaimer}

**Wszystkie skrypty i polecenia zawarte w tym przewodniku służą wyłącznie jako przykłady.** Nie przyjmuje się żadnej odpowiedzialności ani gwarancji co do ich funkcjonalności lub przydatności dla Twojego konkretnego środowiska. Zawsze:
- Najpierw przetestuj procedury kopii zapasowej i przywracania w środowisku nieprodukcyjnym
- Zweryfikuj, czy kopie zapasowe są kompletne i możliwe do przywrócenia
- Dostosuj skrypty i polecenia do swojej specyficznej konfiguracji i wymagań
- Przechowuj wiele kopii zapasowych w różnych lokalizacjach
- Regularnie testuj swoje procedury odzyskiwania danych po awarii

**Stosuj na własne ryzyko.**

## 🎯 Zalecenia dotyczące Kopii Zapasowych {#backup-recommendations}

### Kopie zapasowe offline (Wysoce zalecane) {#offline-backups-highly-recommended}
Dla zapewnienia maksymalnej spójności i integralności danych, **zawsze wykonuj kopie zapasowe przy zatrzymanej aplikacji i bazie danych**. Zapewnia to:
- Brak aktywnych transakcji bazy danych podczas tworzenia kopii zapasowej
- Pełną spójność danych we wszystkich plikach
- Brak ryzyka uszkodzenia plików spowodowanego współbieżnym zapisem
- Niezawodną możliwość przywracania

Chociaż kopie zapasowe typu "hot" (Metoda 2 i 3) są wygodne ze względu na minimalizację przestojów, niosą ze sobą wyższe ryzyko niespójności. W krytycznych środowiskach produkcyjnych należy zaplanować regularne kopie zapasowe offline w oknach serwisowych.

### Kopie zapasowe na poziomie systemu {#system-level-backups}
Oprócz kopii zapasowych specyficznych dla aplikacji, **zdecydowanie zaleca się** tworzenie kopii zapasowych całej infrastruktury systemu:
- **Kopie zapasowe kontenerów LXC**: Jeśli środowisko działa w kontenerze LXC, utwórz migawki (snapshots) całego kontenera w trybie offline
- **Kopie zapasowe maszyn wirtualnych**: W przypadku wdrożeń VM, twórz pełne migawki VM
- **Kopie zapasowe systemu hosta**: Obejmują wolumeny Docker, konfiguracje kontenerów i ustawienia systemu
- **Pliki konfiguracyjne**: Wykonaj oddzielną kopię zapasową wszystkich plików konfiguracyjnych Docker i systemu

Kopie zapasowe na poziomie systemu stanowią dodatkową warstwę bezpieczeństwa i umożliwiają pełne odzyskiwanie danych, w tym możliwość przywrócenia całego środowiska na innym hoście, jeśli zajdzie taka potrzeba.

## 🛠️ Wymagania wstępne {#prerequisites}

- Działająca instalacja Mail Archiver Docker (patrz [Przewodnik instalacji](Setup.md))
- Dostęp do systemu hosta Docker
- Wystarczająca przestrzeń dyskowa na kopie zapasowe

## 📦 Co kopiować {#what-to-backup}

Środowisko Mail Archiver Docker składa się z kilku ważnych wolumenów danych, które muszą zostać objęte kopią zapasową:

### 1. Baza danych PostgreSQL {#1-postgresql-database}
- **Wolumen**: `./postgres-data`
- **Zawiera**: Wszystkie zarchiwizowane e-maile, konta użytkowników, konta pocztowe i dane aplikacji
- **Ważność**: ⭐⭐⭐ Krytyczne - To jest Twoje główne repozytorium danych

### 2. Klucze ochrony danych (Data Protection Keys) {#2-data-protection-keys}
- **Wolumen**: `./data-protection-keys`
- **Zawiera**: Klucze ASP.NET Core Data Protection do szyfrowania ciasteczek i uwierzytelniania
- **Ważność**: ⭐⭐ Ważne - Wymagane dla istniejących sesji użytkowników i bezpiecznego uwierzytelniania

### 2a. Oryginalne pliki wiadomości (tylko z `RawMessages.Storage=filesystem`) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **Wolumen**: `./raw-messages` (strona hosta dla `/app/RawMessages`)
- **Zawiera**: Oryginalne `.eml` każdej wiadomości e-mail, skompresowane gzip, zapisywane gdy `RawMessages:Enabled` jest ustawione, a backendem składowania jest `filesystem`
- **Ważność**: ⭐⭐⭐ Krytyczne **podczas użytkowania** - baza danych przechowuje tylko sumę kontrolną, rozmiar i ścieżkę relatywną, więc przywrócenie bez tego katalogu pozostawi oryginały, których nie można będzie udostępnić
- Kopię zapasową tego katalogu i zrzutu bazy danych należy wykonać **razem**. Przywrócenie bazy danych bez plików nie jest błędem, który Archiwum może naprawić: pobrania odpowiedzą `404`, a sum kontrolnych nie można odtworzyć z niczego innego. Przywrócenie plików bez bazy danych jest równie bezużyteczne, ponieważ nic się do nich nie odnosi.
- Jeśli używasz `RawMessages.Storage=postgres` (domyślne), katalog ten nie istnieje, a oryginały znajdują się wewnątrz `./postgres-data` jak wcześniej.
- Więcej informacji na temat tej funkcji oraz sposobu zmiany backendu znajduje się w sekcji [Przechowywanie oryginalnych wiadomości](RawMessages.md).

### 3. Konfiguracja Docker Compose {#3-docker-compose-configuration}
- **Plik**: `docker-compose.yml`
- **Zawiera**: Konfigurację kontenerów, zmienne środowiskowe i ustawienia
- **Ważność**: ⭐⭐ Ważne - Zawiera konfigurację Twojej aplikacji

## 🔄 Procedury Kopii Zapasowej {#backup-procedures}

### Metoda 1: Pełna kopia zapasowa (Zalecana) {#method-1-complete-backup-recommended}

Ta metoda tworzy kopię zapasową wszystkich danych podczas zatrzymanych kontenerów, aby zapewnić spójność danych.

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

### Metoda 2: Gorąca kopia zapasowa (Minimalny czas przestoju) {#method-2-hot-backup-minimal-downtime}

Ta metoda tworzy pełną kopię zapasową podczas pracy systemu, minimalizując czas przestoju.

**Uwaga:** Zastąp `mailuser` i `MailArchiver` swoimi rzeczywistymi danymi uwierzytelniającymi do bazy danych z `docker-compose.yml` (sprawdź zmienne środowiskowe `POSTGRES_USER` i `POSTGRES_DB`).

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

### Metoda 3: Zautomatyzowany skrypt kopii zapasowej {#method-3-automated-backup-script}

Dla regularnych, zautomatyzowanych kopii zapasowych udostępniamy gotowy do użycia skrypt. Skrypt jest dostępny w [backup.sh](attachments/backup.sh).

**Funkcje:**
- ✅ Automatyczne wykrywanie danych uwierzytelniających bazy danych z docker-compose.yml
- ✅ Gorąca kopia zapasowa (minimalny czas przestoju)
- ✅ Mechanizm pliku blokady (lock file), aby zapobiec współbieżnym kopiom zapasowym
- ✅ Sprawdzanie miejsca na dysku przed wykonaniem kopii
- ✅ Automatyczne czyszczenie starych kopii zapasowych

**Konfiguracja:**

Przed użyciem skryptu edytuj sekcję konfiguracji na górze pliku:

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

**Instalacja:**

1. **Skopiuj skrypt do wybranej lokalizacji:**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **Edytuj konfigurację:**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # Dostosuj ustawienia
   ```

3. **Nadaj skryptowi uprawnienia do wykonywania:**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **Przetestuj skrypt ręcznie:**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **Zweryfikuj kopię zapasową:**
   - Sprawdź katalog kopii zapasowej pod kątem utworzonego pliku `.tar.gz`
   - Przejrzyj plik logów w poszukiwaniu błędów lub ostrzeżeń

**Planowanie automatycznych kopii zapasowych:**

Aby zaplanować automatyczne kopie zapasowe za pomocą cron:

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

## 🔙 Procedury Przywracania {#restore-procedures}

Procedury przywracania zostały zaprojektowane tak, aby odpowiadały metodom tworzenia kopii zapasowych. Wybierz odpowiednią metodę przywracania na podstawie użytej metody kopii zapasowej:

- **Metoda 1 (Pełna kopia zapasowa)**: Użyj procedury "Full Restore from Complete Backup". Metoda ta jest stosowana, gdy posiadasz pełną kopię zapasową zawierającą katalog `postgres-data`.
- **Metoda 2 (Gorąca kopia zapasowa)** lub **Metoda 3 (Zautomatyzowany skrypt kopii zapasowej)**: Użyj procedury "Restore from SQL Dump" (obsługuje klucze ochrony danych i bazę danych). Metoda ta jest stosowana, gdy posiadasz gorącą kopię zapasową zawierającą zrzut SQL bazy danych i klucze ochrony danych.

Dla wygody udostępniliśmy również zautomatyzowany skrypt przywracania, który może obsłużyć wszystkie procedury automatycznie. Możesz użyć skryptu zamiast ręcznego wykonywania poniższych kroków.

### Zautomatyzowany skrypt przywracania {#automated-restore-script}

Skrypt przywracania jest dostępny w pliku [restore.sh](attachments/restore.sh). Zapewni on prowadzenie przez proces przywracania i zapyta o metodę kopii zapasowej, ścieżkę do pliku tar z kopią zapasową oraz katalog instalacyjny, a następnie automatycznie przeprowadzi proces przywracania.

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

Skrypt przeprowadzi Cię przez proces przywracania i automatycznie obsłuży wszystkie kroki na podstawie użytej metody kopii zapasowej.

### Pełne przywracanie z pełnej kopii zapasowej (Metoda ręczna) {#full-restore-from-complete-backup-manual-method}

Przywraca to wszystkie dane z kompletnego archiwum kopii zapasowej. Metodę tę należy stosować, gdy posiadasz pełną kopię zapasową zawierającą katalog `postgres-data` (utworzony przez metodę pierwszą).

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

### Przywracanie ze zrzutu SQL (Metoda ręczna) {#restore-from-sql-dump-manual-method}

Przywraca to bazę danych i klucze ochrony danych z gorącej kopii zapasowej (Metoda druga i trzecia).

**Uwaga:** Poniższe polecenia używają przykładowych danych uwierzytelniających (`mailuser` i `MailArchiver`). Musisz zastąpić je rzeczywistymi wartościami z Twojego pliku `docker-compose.yml`. Sprawdź zmienne środowiskowe `POSTGRES_USER` i `POSTGRES_DB` w usłudze `postgres`.

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


## ⚠️ Ważne uwagi {#important-notes}

### Ustawienia połączenia z bazą danych {#database-connection-settings}
Upewnij się, że ustawienia połączenia z bazą danych w `docker-compose.yml` są zgodne podczas kopii zapasowej i przywracania:
- `POSTGRES_USER` (domyślne: mailuser)
- `POSTGRES_PASSWORD` (domyślne: masterkey)
- `POSTGRES_DB` (domyślne: MailArchiver)

### Klucze ochrony danych {#data-protection-keys}
Katalog `data-protection-keys` jest używany do:
- Uwierzytelniania użytkowników i zarządzania sesjami
- Szyfrowania ciasteczek
- Bezpiecznego przetwarzania danych

Bez tych kluczy istniejące sesje użytkowników zostaną unieważnione i użytkownicy będą musieli zalogować się ponownie.

### Zmienne środowiskowe {#environment-variables}
Wszystkie ustawienia aplikacji są przechowywane w zmiennych środowiskowych w `docker-compose.yml`. Upewnij się, że:
- Wykonujesz kopię zapasową pliku `docker-compose.yml`
- Dokumentujesz wszelkie niestandardowe ustawienia
- Używasz tej samej konfiguracji podczas przywracania

## 🔒 Najlepsze praktyki bezpieczeństwa {#security-best-practices}

1. **Szyfruj kopie zapasowe**: Rozważ szyfrowanie archiwów kopii zapasowych, zwłaszcza jeśli zawierają wrażliwe dane e-mail.

2. **Bezpieczne przechowywanie**: Przechowuj kopie zapasowe w bezpiecznym miejscu:
   - Używaj oddzielnego serwera lub systemu składowania
   - Wdróż mechanizmy kontroli dostępu
   - Rozważ przechowywanie poza lokalizacją (off-site) lub w chmurze

3. **Regularne testowanie**: Regularnie testuj swoje procedury kopii zapasowej i przywracania, aby upewnić się, że działają poprawnie.

4. **Monitoruj rozmiar kopii zapasowych**: Śledź rozmiary kopii zapasowych, aby upewnić się, że masz wystarczającą przestrzeń dyskową.

5. **Przechowywanie kopii zapasowych (Retention)**: Zdefiniuj i wdróż politykę Przechowywania kopii zapasowych (np. przechowuj codzienne kopie przez 7 dni, tygodniowe przez 4 tygodnie, miesięczne przez 12 miesięcy).

6. **Zmień domyślne hasła**: Domyślne hasła w konfiguracji są niebezpieczne i powinny zostać zmienione do użytku produkcyjnego.


## 🖥️ Proxmox Backup {#proxmox-backup}

Podczas uruchamiania środowiska Mail Archiver Docker w kontenerze Proxmox LXC, zaleca się używanie trybu "stop" do tworzenia kopii zapasowej całego kontenera, aby zapewnić spójność danych.

### Dlaczego zalecany jest tryb Stop {#why-stop-mode-is-recommended}

1. **Spójność danych**: Zatrzymanie kontenera zapewnia, że wszystkie transakcje bazy danych zostaną zakończone, a dane zapisane na dysku przed wykonaniem kopii zapasowej.
2. **Zapobieganie uszkodzeniu danych**: Wykonywanie kopii zapasowych podczas aktywnego kontenera może prowadzić do uszkodzenia danych, szczególnie w operacjach na bazie danych.
3. **Integralność systemu plików**: Zatrzymanie kontenera zapewnia, że wszystkie operacje systemu plików zostaną zakończone, zapobiegając częściowemu zapisowi podczas tworzenia kopii zapasowej.

### Uwagi dotyczące Proxmox Backup {#proxmox-backup-notes}

- Zawsze upewnij się, że masz wystarczającą przestrzeń dyskową na kopie zapasowe.
- Regularnie testuj procedurę przywracania, aby upewnić się, że kopie zapasowe są poprawne.
- Rozważ ustawienie harmonogramu kopii zapasowych w interfejsie webowym Proxmox dla zautomatyzowanych kopii zapasowych.
- Monitoruj logi kopii zapasowych pod kątem błędów lub problemów.
