# 🔄 Przewodnik po aktualizacji głównej wersji PostgreSQL {#postgresql-major-version-upgrade-guide}

[← Powrót do Indeksu Dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik wyjaśnia, jak zaktualizować bazę danych PostgreSQL z jednej głównej wersji do drugiej w środowisku Mail Archiver Docker Compose. Aktualizacje głównej wersji są niezbędne, aby korzystać z nowych funkcji PostgreSQL, ulepszeń wydajności oraz aktualizacji bezpieczeństwa.

## ⚠️ Ważne zastrzeżenie {#important-disclaimer}
> **Uwaga**: Ta procedura aktualizacji jest ogólnym procesem migracji bazy danych PostgreSQL i nie jest specyficzna dla Mail Archiver. Jest ona dostarczana jako zalecenie oparte na najlepszych praktykach PostgreSQL. Zawsze najpierw przetestuj procedurę w środowisku nieprodukcyjnym i upewnij się, że posiadasz odpowiednie kopie zapasowe przed kontynuowaniem.


**Wszystkie procedury opisane w tym przewodniku powinny zostać najpierw przetestowane w środowisku nieprodukcyjnym.** Aktualizacje głównej wersji obejmują migrację danych i niosą ze sobą ryzyko utraty lub uszkodzenia danych. Zawsze:
- Utwórz pełną kopię zapasową przed rozpoczęciem procesu aktualizacji
- Przetestuj procedurę aktualizacji w środowisku deweloperskim
- Zweryfikuj całą funkcjonalność aplikacji po aktualizacji
- Przygotuj plan wycofania zmian (rollback) na wypadek problemów
- Zaplanuj aktualizacje w oknach serwisowych

**Stosuj na własną odpowiedzialność.**

## 🎯 Kiedy aktualizować {#when-to-upgrade}

Rozważ aktualizację PostgreSQL, gdy:
- Twoja obecna wersja zbliża się do końca wsparcia (end-of-life)
- Aktualizacje bezpieczeństwa wymagają nowszej wersji
- Planujesz główne okno serwisowe systemu

## 🛠️ Wymagania wstępne {#prerequisites}

- Działająca konfiguracja Mail Archiver Docker (patrz [Setup Guide](Setup.md))
- Wystarczająca ilość miejsca na dysku (co najmniej 1,5x aktualny rozmiar bazy danych)
- Dostęp do systemu hosta Docker
- Pełna kopia zapasowa aktualnej bazy danych (patrz [Backup and Restore Guide](BackupRestore.md))
- Okno serwisowe na przestój aplikacji

## 📋 Aktualna konfiguracja {#current-configuration}

Twoja obecna konfiguracja Mail Archiver używa:
- **Nazwa bazy danych**: MailArchiver
- **Użytkownik bazy danych**: mailuser
- **Wolumen danych**: `./postgres-data` mapowany do `/var/lib/postgresql/data`

W razie potrzeby należy to dostosować w poniższych przykładowych poleceniach i skryptach.

## 🔄 Procedury aktualizacji {#upgrade-procedures}

Ta metoda tworzy logiczną kopię zapasową Twoich danych i przywraca ją do nowej wersji PostgreSQL. Jest to najbezpieczniejsze podejście, które działa we wszystkich wersjach PostgreSQL.

> **Alternatywne metody**: PostgreSQL oferuje również inne metody aktualizacji, takie jak `pg_upgrade` dla szybszych aktualizacji. Aby uzyskać więcej informacji na temat alternatywnych podejść do aktualizacji, zapoznaj się z [oficjalną dokumentacją aktualizacji PostgreSQL](https://www.postgresql.org/docs/current/upgrading.html).

#### Krok 1: Przygotowanie środowiska {#step-1-prepare-the-environment}

```bash
# 1. Stop the application container (keep database running for backup)
docker compose stop mailarchive-app

# 2. Create a backup directory
mkdir -p postgres-backup

# 3. Create a database dump
echo "Export DB content (this can take some time)"
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > postgres-backup/database_dump.sql

# 4. Verify the dump was created successfully
ls -la postgres-backup/database_dump.sql

# 5. Stop the database container
docker compose down
```

#### Krok 2: Aktualizacja konfiguracji Docker Compose {#step-2-update-docker-compose-configuration}

Edytuj swój plik `docker-compose.yml`, aby używać nowej wersji PostgreSQL:

```yaml
  postgres:
    image: postgres:18  # Changed from old version
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data-new:/var/lib/postgresql  # New data directory
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
```

**Ważne zmiany:**
- Zaktualizowano `image` z `postgres:17` na `postgres:18` (lub Twoją docelową wersję)
- Zmieniono mapowanie wolumenu z `./postgres-data:/var/lib/postgresql/data` na `./postgres-data-new:/var/lib/postgresql` (zmiana ścieżki wewnętrznej jest wymagana od wersji 18 i nowszych postgres)

#### Krok 3: Uruchomienie nowej instancji PostgreSQL {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### Krok 4: Przywracanie danych do nowej bazy danych {#step-4-restore-data-to-new-database}

Przywróć zrzut bazy danych do nowej instancji PostgreSQL:

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
Może to zająć trochę czasu w zależności od rozmiaru bazy danych
Monitoruj wszelkie komunikaty o błędach podczas przywracania

#### Krok 5: Testowanie i zakończenie migracji {#step-5-test-and-complete-migration}
Uruchom aplikację, aby przetestować połączenie:

```bash
docker compose up -d
```

Przetestuj funkcjonalność aplikacji:
- Zaloguj się do interfejsu webowego
- Sprawdź, czy wyszukiwanie e-maili działa
- Sprawdź, czy istniejące dane są dostępne
- Przetestuj funkcjonalność kopia zapasowa/przywracanie

Jeśli wszystko działa poprawnie, wyczyść stare dane:

Zatrzymaj kontenery
```bash
docker compose down
```
Usuń stary katalog danych (tylko po potwierdzeniu, że nowa konfiguracja działa)
```bash
rm -rf postgres-data
```
Usuń katalog kopii zapasowej
```bash
rm -rf postgres-backup
```

Zmień nazwę nowego katalogu danych na standardową
```bash
mv postgres-data-new postgres-data
```

Zaktualizuj docker-compose.yml, aby używać standardowej nazwy wolumenu:
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Uruchom kompletny system
```bash
docker compose up -d
```

## ⚠️ Ważne uwagi {#important-considerations}

### Planowanie przestoju {#downtime-planning}

- W zależności od systemu i rozmiaru bazy danych, procedura ta może zająć dużo czasu.
- Zaplanuj działania w oknach serwisowych

### Wymagania dotyczące miejsca na dysku {#disk-space-requirements}

Upewnij się, że masz wystarczającą ilość miejsca na dysku (ok. dwukrotność rozmiaru Twojej bazy danych)

Sprawdź dostępną przestrzeń:
```bash
df -h
du -sh postgres-data/
```

### Plan wycofania zmian (Rollback) {#rollback-plan}

Zawsze przygotuj się na wycofanie zmian:
1. Zachowaj stary katalog danych do czasu zweryfikowania aktualizacji
2. Zachowaj starą kopię zapasową `docker-compose.yml`
3. Najpierw przetestuj procedurę wycofania zmian w środowisku deweloperskim

### Weryfikacja po aktualizacji {#post-upgrade-verification}

Po aktualizacji zweryfikuj:
- Połączenie aplikacji z bazą danych
- Funkcjonalność wyszukiwania i pobierania e-maili
- Uwierzytelnianie użytkowników i zarządzanie sesjami
- Funkcjonalność kopia zapasowa i przywracanie
- Metryki wydajności bazy danych

## 📚 Powiązana dokumentacja {#related-documentation}

- [Backup and Restore Guide](BackupRestore.md) - Niezbędny do wykonania kopii zapasowych przed aktualizacją
- [Database Maintenance Guide](DatabaseMaintenance.md) - Konserwacja po aktualizacji
- [Setup Guide](Setup.md) - Konfiguracja Docker Compose
- [PostgreSQL Release Notes](https://www.postgresql.org/docs/release/) - Oficjalna dokumentacja wydania
