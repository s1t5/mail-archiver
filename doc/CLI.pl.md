# 🖥️ CLI Commands {#cli-commands}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niektóre czynności nie powinny być wykonywane w interfejsie webowym: importowanie istniejącego Archiwum z lokalnego systemu plików lub przenoszenie przechowywanych oryginalnych wiadomości między backendami pamięci masowej. Mail Archiver przyjmuje oba te procesy jako polecenia w linii komend.

| Polecenie | Cel |
|----------|---------|
| `--import-mbox` | Import pliku MBox z zamontowanego katalogu |
| `--import-eml` | Import Archiwum ZIP z plikami EML z zamontowanego katalogu |
| `--migrate-raw-storage` | Przenoszenie przechowywanych oryginalnych wiadomości między bazą danych a systemem plików |

Wszystkie polecenia są obsługiwane przed uruchomieniem potoku webowego i kończą działanie po zakończeniu pracy, więc ich uruchamianie z `docker compose exec` podczas pracy Archiwum jest bezpieczne.

## ⚠️ Model bezpieczeństwa {#security-model}

Polecenia importu są dostępne **wyłącznie przez CLI** – nie ma punktu końcowego (endpointu) w aplikacji webowej do przeglądania lub importowania lokalnych plików. Musisz posiadać dostęp do hosta Docker (`docker exec`), aby móc je uruchomić.

**Biała lista ścieżek** (`LocalImport:AllowedPaths`) kontroluje, z których katalogów importer może odczytywać dane. Pliki znajdujące się poza tymi ścieżkami są odrzucane.

`--migrate-raw-storage` nie podlega tej białej liście: odczytuje ona tylko pliki wiadomości, które samo Archiwum zapisało w `RawMessages:FileSystem:BasePath`.

## 🛠️ Konfiguracja {#setup}

### 1. Konfiguracja dozwolonych ścieżek {#1-configure-allowed-paths}

W swoim `docker-compose.yml` ustaw dozwolone ścieżki importu:

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. Montowanie plików {#2-mount-your-files}

Zamontuj katalog zawierający pliki MBox/EML do kontenera:

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

Zastąp `/home/user/mbox-archives` właściwą ścieżką na hoście Docker.

### 3. Restart kontenera {#3-restart-the-container}

```bash
docker compose down && docker compose up -d
```

## 📝 Użycie {#usage}

### Znajdź ID docelowego Konta {#find-your-target-account-id}

Przed importowaniem musisz znać identyfikator bazy danych docelowego konta pocztowego. Możesz go znaleźć w Web UI:

Przejdź do "Email Accounts", otwórz szczegóły docelowego konta; ID konta jest wyświetlane na liście kont lub w adresie URL podczas przeglądania konta.

### Importuj plik MBox {#import-an-mbox-file}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**Parametry:**
| Parametr | Wymagany | Opis |
|-----------|----------|-------------|
| `--import-mbox` | Tak | Sygnalizuje operację importu MBox |
| `--file <path>` | Tak | Ścieżka do pliku MBox wewnątrz kontenera |
| `--account-id <id>` | Tak | Identyfikator bazy danych docelowego konta pocztowego |
| `--folder <name>` | Nie | Nazwa docelowego Folderu (domyślnie: `INBOX`) |

### Importuj pliki EML (Archiwum ZIP) {#import-eml-files-zip-archive}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**Parametry:**
| Parametr | Wymagany | Opis |
|-----------|----------|-------------|
| `--import-eml` | Tak | Sygnalizuje operację importu EML |
| `--file <path>` | Tak | Ścieżka do **Archiwum ZIP** zawierającego pliki `.eml` wewnątrz kontenera |
| `--account-id <id>` | Tak | Identyfikator bazy danych docelowego konta pocztowego |

> **Ważne:** `--import-eml` oczekuje **Archiwum ZIP** zawierające pliki `.eml` — **nie** akceptuje pojedynczych plików `.eml`. Aby zaimportować pojedyncze pliki EML, najpierw umieść je w Archiwum ZIP:
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **Uwaga dla `dotnet run`:** Podczas używania `dotnet run` (zamiast opublikowanej biblioteki DLL), oddziel argumenty dotnet od argumentów aplikacji za pomocą `--`:
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> `--` informuje `dotnet run`, że wszystkie następujące argumenty należą do Twojej aplikacji, a nie do kompilatora .NET.

## 📊 Przykładowy wynik wyjściowy {#example-output}

```
Target account: user@example.com (ID: 1)

=== Local MBox Import ===
File: /data/import/myarchive.mbox
Size: 245.32 MB
Target Account ID: 1
Target Folder: INBOX

[Importing... progress logged to container logs]

=== Import Results ===
Status: Completed
Total Emails: 15234
Imported Successfully: 15120
Failed: 42
Skipped (malformed): 28
Skipped (duplicates): 44
Duration: 00:12:45
```

## 🔍 Monitorowanie {#monitoring}

Podczas importu szczegółowy postęp jest zapisywany w logach kontenera:

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ Referencja konfiguracji {#configuration-reference}

| Zmienna środowiskowa | Domyślna | Opis |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | Pierwsza dozwolona ścieżka dla lokalnych importów |
| `LocalImport__AllowedPaths__1` | (empty) | Druga dozwolona ścieżka (opcjonalna) |

Dodaj więcej ścieżek, zwiększając indeks (`__2`, `__3` itd.).

## 🚫 Obsługa błędów {#error-handling}

| Błąd | Przyczyna | Rozwiązanie |
|-------|-------|----------|
| `File not found` | Ścieżka `--file` nie istnieje w kontenerze | Zweryfikuj ścieżkę do pliku i montowanie wolumenu |
| `File path is not in an allowed import directory` | Plik znajduje się poza ścieżkami na liście dozwolonych | Przenieś plik do dozwolonej ścieżki lub dodaj jego katalog do `LocalImport:AllowedPaths` |
| `Mail account with ID X not found` | Nieprawidłowe ID Konto | Zweryfikuj ID Konto za pomocą Web UI lub zapytania do bazy danych |
| `Invalid account-id` | Wartość niebędąca liczbą dla `--account-id` | Użyj numerycznego ID bazy danych |

## 🔒 Najlepsze praktyki bezpieczeństwa {#security-best-practices}

1. **Używaj montowań tylko do odczytu**, gdy to możliwe:
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **Ogranicz AllowedPaths** tylko do katalogów, których faktycznie używasz.

3. **Usuń montowania wolumenów** po zakończeniu importu, aby zachować czystą powierzchnię bezpieczeństwa.

4. **Nigdy nie udostępniaj** katalogu importu poprzez serwer WWW – nie ma punktu końcowego WWW dla lokalnych importów.

## 📦 Formaty importu w pigułce {#import-formats-at-a-glance}

| CLI Flag | Format wejściowy | Typowe rozszerzenie pliku | Pojedyncza wiadomość e-mail | Masowo |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | plik MBox | `.mbox`, `.mbx` | ❌ (zawsze masowo) | ✅ |
| `--import-eml` | Archiwum ZIP plików EML | `.zip` | ❌ (wymagany ZIP) | ✅ |
| `--migrate-raw-storage` | Przechowywane oryginalne wiadomości | — | — | ✅ |

*Pojedyncze pliki `.eml` nie są bezpośrednio obsługiwane. Użyj `zip`, aby je spakować, lub zaimportuj poprzez [przesyłanie przez przeglądarkę](Import-EML-MBox.md).*

## 🔧 Komendy konserwacyjne {#maintenance-commands}

### Przenoszenie przechowywanych oryginalnych wiadomości {#moving-stored-original-messages}

Gdy włączone jest `RawMessages:Enabled`, oryginalna wiadomość każdego e-maila jest przechowywana w bazie danych lub w plikach, w zależności od `RawMessages:Storage`. Ta komenda przenosi już zapisane oryginały z jednego backendu do drugiego; konfigurację opisano w sekcji [Original Message Storage](RawMessages.md#storage-backend).

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

| Argument | Domyślny | Opis |
|----------|---------|-------------|
| `--from` | skonfigurowany `RawMessages:Storage` | Backend, z którego są przenoszone wiadomości. |
| `--to` | skonfigurowany `RawMessages:Storage` | Backend, do którego są przenoszone wiadomości. |
| `--batch` | `200` | Liczba wiadomości na jedną transakcję bazy danych. |

W przeciwieństwie do komend importu, ta nie jest ograniczona przez `LocalImport:AllowedPaths`: odczytuje ona tylko te pliki wiadomości, które samo Archiwum zapisało w `RawMessages:FileSystem:BasePath`.

**Co gwarantuje ta komenda:**

- Każda wiadomość jest weryfikowana pod kątem sumy kontrolnej zarejestrowanej w czasie archiwizacji przed jej skopiowaniem. Wiadomość, która nie przejdzie weryfikacji, zostaje dokładnie tam, gdzie była, i zostaje zgłoszona — uszkodzony oryginał nigdy nie zostaje powielony, a jego jedyna pozostała kopia nigdy nie zostaje usunięta.
- Plik jest usuwany dopiero po zatwierdzeniu transakcji bazy danych, która go przeniosła, dzięki czemu przerwanie działania nie spowoduje utraty jedynej kopii.
- Proces można wznowić: praca jest wybierana na podstawie miejsca przechowywania zarejestrowanego dla każdej wiadomości, więc ponowne uruchomienie komendy kontynuuje zadania, które wciąż pozostają do wykonania.
- Jedna błędna wiadomość nie przerywa całego procesu. Kod wyjścia to `1`, jeśli jakakolwiek wiadomość zakończyła się niepowodzeniem, w przeciwnym razie `0`.

**Przykład wyjścia:**

```text
Batch: 200 moved, 0 skipped, 0 failed
Batch: 200 moved, 0 skipped, 0 failed
Batch: 42 moved, 0 skipped, 1 failed
Done: 442 moved, 1 failed.
  Email 12345: checksum mismatch (recorded 4f1c..., file has 9ab2...)
```

Operacje odczytu zawsze podążają za backendem zarejestrowanym dla każdej wiadomości, a nie za skonfigurowanym ustawieniem, dzięki czemu pobieranie działa nieprzerwanie w trakcie i po migracji. Zmiana `RawMessages:Storage` wpływa tylko na nowo archiwizowane pocztę — uruchom tę komendę, aby przenieść to, co jest już zapisane.

> **Kopia zapasowa:** w przypadku backendu opartego na systemie plików pliki wiadomości znajdują się poza bazą danych. Wykonaj kopię zapasową `RawMessages:FileSystem:BasePath` wraz z zrzutem bazy danych; przywracanie bez plików pozostawi oryginały, których nie można będzie obsłużyć.

## ❓ FAQ {#faq}

**P: Dlaczego `--import-eml` wymaga pliku ZIP?**
O: Podstawowa usługa importu EML (`EmlImportService`) przetwarza Archiwum ZIP wewnętrznie za pomocą API `ZipFile` systemu .NET. Taka konstrukcja umożliwia wydajny masowy import. W przypadku pojedynczych plików `.eml` należy umieścić je w Archiwum ZIP (np. `zip emails.zip *.eml`) lub skorzystać z [przesyłania przez przeglądarkę](Import-EML-MBox.md).

**P: Dlaczego nie mogę przeglądać plików z interfejsu Web UI?**
O: Jest to celowy projekt bezpieczeństwa. Funkcja ta została celowo zaimplementowana za pomocą CLI dla interakcji na poziomie systemu, ponieważ zwykli użytkownicy nie mają bezpośredniego dostępu do poziomu systemu operacyjnego; wchodzą oni w interakcję wyłącznie z Web UI.

Aby zapobiec ryzyku bezpieczeństwa, takiemu jak ataki typu path traversal oraz nieautoryzowany dostęp do systemu plików, zwykli użytkownicy mają ograniczone możliwości korzystania z Web UI w celu przesyłania i importowania plików. Zapewnia to, że nie mogą oni ominąć zamierzonego interfejsu, aby uzyskać dostęp do wewnętrznego systemu plików kontenera.
