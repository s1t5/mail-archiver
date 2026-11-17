# 🛠️ Przewodnik konfiguracji Mail Archiver {#mail-archiver-setup-guide}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik zawiera szczegółowe instrukcje dotyczące konfiguracji aplikacji Mail Archiver przy użyciu Docker Compose.

## 🛠️ Wymagania wstępne {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Kroki instalacji {#installation-steps}

1. Zainstaluj wymagania wstępne w swoim systemie.

2. Utwórz plik `docker-compose.yml` o następującej zawartości:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey;

      # Authentication Settings
      - Authentication__Username=admin
      - Authentication__Password=secure123!
      - Authentication__SessionTimeoutMinutes=60
      - Authentication__CookieName=MailArchiverAuth
      - Authentication__CookieSameSite=Lax

      # REST API Settings (read-only, disabled by default)
      - Api__Enabled=false
      - Api__AllowAttachmentDownloads=true
      - Api__AllowMessageDownloads=true
      - Api__EnableSwaggerUi=true
      - Api__DefaultPageSize=20
      - Api__MaxPageSize=100
      - Api__RateLimitPerMinute=120

      # MCP Server Settings (Model Context Protocol for AI agents, read-only, disabled by default)
      - Mcp__Enabled=false
      - Mcp__AllowAttachmentDownloads=true
      - Mcp__DefaultPageSize=20
      - Mcp__MaxResults=100
      - Mcp__MaxAttachmentBytes=10000000
      - Mcp__AllowMessageDownloads=true
      - Mcp__MaxMessageBytes=10000000
      - Mcp__RateLimitPerMinute=120

      # MailSync Settings
      - MailSync__IntervalMinutes=15
      - MailSync__TimeoutMinutes=0
      - MailSync__ConnectionTimeoutSeconds=180
      - MailSync__CommandTimeoutSeconds=300
      - MailSync__AlwaysForceFullSync=false
      - MailSync__IgnoreSelfSignedCert=false
      - MailSync__MaxConcurrentSyncs=1
      - MailSync__InterAccountDelaySeconds=0
      - MailSync__MaxIssuesPerKind=20
      - MailSync__FullSyncIntervalHours=24
      - MailSync__ExcludeSubfolders=false
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Contacts

      # Original Message Storage (complete .eml per email, disabled by default)
      - RawMessages__Enabled=false
      - RawMessages__Storage=postgres
      - RawMessages__MaxMessageSizeMb=50
      - RawMessages__IncludeGraph=true
      - RawMessages__FileSystem__BasePath=RawMessages
      - RawMessages__FileSystem__ShardDepth=2
      - RawMessages__FileSystem__VerifyChecksumOnRead=true

      # BatchRestore Settings
      - BatchRestore__AsyncThreshold=50
      - BatchRestore__MaxSyncEmails=150
      - BatchRestore__MaxAsyncEmails=50000
      - BatchRestore__SessionTimeoutMinutes=30
      - BatchRestore__DefaultBatchSize=50

      # Tenant Management Settings
      - TenantManagement__MaxSelectedMailboxes=1000

      # Mailcow Settings
      - Mailcow__MaxSelectedMailboxes=1000

      # BatchOperation Settings
      - BatchOperation__BatchSize=50
      - BatchOperation__PauseBetweenEmailsMs=50
      - BatchOperation__PauseBetweenBatchesMs=250

      # Bandwidth Tracking Settings (for IMAP rate limit handling)
      - BandwidthTracking__Enabled=false
      - BandwidthTracking__DailyLimitMb=25000
      - BandwidthTracking__WarningThresholdPercent=80
      - BandwidthTracking__PauseHoursOnLimit=24
      - BandwidthTracking__TrackUploadBytes=false

      # Selection Settings
      - Selection__MaxSelectableEmails=250

      # View Settings (Privacy & Display)
      - View__DefaultToPlainText=true
      - View__BlockExternalResources=false

      # Dashboard Settings (statistics cache, counter parts, chart periods)
      - Dashboard__CacheSeconds=60
      - Dashboard__RefreshIntervalMinutes=15
      - Dashboard__ShowDirectionSplits=true
      - Dashboard__SelectablePeriods=true

      # Jobs Settings (background jobs page)
      - Jobs__RefreshSeconds=30

      # Npgsql Settings
      - Npgsql__CommandTimeout=900

      # Upload Settings for MBox and EML files
      - Upload__MaxFileSizeGB=10
      - Upload__KeepAliveTimeoutHours=4
      - Upload__RequestHeadersTimeoutHours=2

      # Local Import Settings (for CLI imports from mounted volumes)
      - LocalImport__AllowedPaths__0=/data/import

      # CSV Import Settings (for bulk IMAP account import)
      - CsvImport__MaxRows=5000
      - CsvImport__MaxFileSizeBytes=10000000

      # Deletion Policy Settings (Optional - controls whether email deletion is allowed)
      - DeletionPolicy__DeletionAllowed=true

      # TimeZone Settings
      - TimeZone__DisplayTimeZoneId=Etc/UCT

      # Database Maintenance Settings (Optional)
      - DatabaseMaintenance__Enabled=false
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30

      # Attachment Deduplication Settings (Optional - feature is always on)
      - AttachmentDeduplication__BatchSize=200
      - AttachmentDeduplication__DelayBetweenBatchesMs=0
      - AttachmentDeduplication__StartupDelaySeconds=20
      - AttachmentDeduplication__OrphanCleanupIntervalHours=12
      - AttachmentDeduplication__CommandTimeoutSeconds=300

      # Account Storage Settings (Optional - per-account storage display)
      - AccountStorage__Enabled=true
      - AccountStorage__DailyExecutionTime=02:30
      - AccountStorage__BackfillDelayMs=5000
      - AccountStorage__RefreshBatchDelayMs=1000
      - AccountStorage__CommandTimeoutSeconds=300

      # ReleaseNotes Settings (Version Update Splash Screen)
      - ReleaseNotes__Enabled=true
      - ReleaseNotes__UpdateHintEnabled=true
      - ReleaseNotes__UpdateCheckIntervalHours=6

      # Logging Settings (Optional - defaults to Information level)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning

      # Security Settings
      - AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com

      # OIDC Configuration (see OIDC_Implementation.md for detailed setup)
      - OAuth__Enabled=true
      - OAuth__Authority=https://example.com
      - OAuth__ClientId=YOUR-CLIENT-ID
      - OAuth__ClientSecret=YOUR-CLIENT-SECRET
      - OAuth__DisplayName=PocketID SSO
      - OAuth__ClientScopes__0=openid
      - OAuth__ClientScopes__1=profile
      - OAuth__ClientScopes__2=email
      - OAuth__DisablePasswordLogin=false
      - OAuth__AutoRedirect=false
      - OAuth__AutoApproveUsers=false
      - OAuth__AdminEmails__0=admin@example.com
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      # Uncomment the following line to mount a directory for local file imports
      # (also configure LocalImport__AllowedPaths__0=/data/import in environment variables)
      # - /path/to/your/mbox/files:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
      # Only with RawMessages__Storage=filesystem: keep the original messages outside the
      # container, otherwise they are lost when the container is recreated.
      # - ./raw-messages:/app/RawMessages
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

networks:
  postgres:
```

3. Edytuj konfigurację bazy danych w `docker-compose.yml` i ustaw bezpieczne hasło w zmiennej `POSTGRES_PASSWORD` oraz w `ConnectionString`.

4. Jeśli chcesz używać uwierzytelniania (co jest zdecydowanie zalecane), zdefiniuj `Authentication__Username` i `Authentication__Password`, które są używane dla użytkownika administratora.

5. Dostosuj zmienną środowiskową `TimeZone__DisplayTimeZoneId` tak, aby odpowiadała preferowanej strefie czasowej (domyślnie "Etc/UCT"). Możesz użyć dowolnego identyfikatora strefy czasowej IANA (np. "Europe/Berlin", "Asia/Tokyo").

6. Opcjonalnie skonfiguruj zmienne środowiskowe `Logging__LogLevel`, aby kontrolować szczegółowość logów aplikacji. Aby zobaczyć dostępne opcje, sprawdź sekcję Logging Settings poniżej.

7. Skonfiguruj wybrany przez siebie reverse proxy z https i uwierzytelnianiem, aby zabezpieczyć dostęp do aplikacji. 

> ⚠️ **Uwaga**: Sama aplikacja nie zapewnia szyfrowanego dostępu poprzez https! Musi on zostać skonfigurowany za pomocą reverse proxy!

8. Pierwsze uruchomienie kontenerów:
```bash
docker compose up -d
```

9. Restart kontenerów:
```bash
docker compose restart
```

10. Uzyskaj dostęp do aplikacji

11. Zaloguj się przy użyciu zdefiniowanych danych uwierzytelniających i dodaj swoje pierwsze konto e-mail:
   - Przejdź do sekcji "Email Accounts"
   - Kliknij "New Account"
   - Wprowadź szczegóły serwera i dane uwierzytelniające
   - Zapisz i rozpocznij archiwizację!
   - Jeśli chcesz, utwórz innych użytkowników i przypisz im konta.

## 📚 Wyjaśnienia zmiennych środowiskowych {#environment-variable-explanations}

### 🗄️ Połączenie z bazą danych {#database-connection}
- `ConnectionStrings__DefaultConnection`: Łączność z bazą danych PostgreSQL. Zmień wartości `Host`, `Database`, `Username` i `Password` w zależności od potrzeb.

### 🔐 Ustawienia uwierzytelniania {#authentication-settings}
- `Authentication__Username`: Nazwa użytkownika dla konta administratora.
- `Authentication__Password`: Hasło dla konta administratora.
- `Authentication__SessionTimeoutMinutes`: Czas wygaśnięcia sesji w minutach. Dotyczy to nieprzechowywalnego ciasteczka logowania; użytkownicy, którzy zaznaczą opcję **Remember Me** w formularzu logowania, otrzymują zamiast tego trwałe ciasteczko ważne przez stałe 7 dni. Patrz [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: Nazwa ciasteczka uwierzytelniającego.
- `Authentication__CookieSameSite`: Konfiguruje atrybut SameSite dla ciasteczek uwierzytelniania, sesji oraz ochrony CSRF. Dopuszczalne wartości to:
  - `Strict` (domyślna): Maksymalne bezpieczeństwo. Ciasteczka są wysyłane tylko przy żądaniach w obrębie tej samej witryny. Może to powodować problemy podczas przechodzenia do aplikacji z zewnętrznych linków (np. kliknięcia w link na innej stronie internetowej), ponieważ istniejąca sesja nie zostanie rozpoznana.
  - `Lax`: Zalecane przy użyciu reverse proxy. Ciasteczka są wysyłane podczas nawigacji na najwyższym poziomie oraz przy żądaniach w obrębie tej samej witryny, co pozwala użytkownikom przechodzić przez zewnętrzne linki do aplikacji przy jednoczesnym zachowaniu ochrony CSRF dla żądań POST.
  - `None`: Ciasteczka są wysyłane ze wszystkimi żądaniami. Wymaga HTTPS oraz atrybutu `Secure`. Używaj tego tylko wtedy, gdy masz specyficzne wymagania dotyczące komunikacji między witrynami i rozumiesz konsekwencje dla bezpieczeństwa.

### 🔌 Ustawienia REST API {#rest-api-settings}
Opcjonalne, tylko do odczytu REST API jest **domyślnie wyłączone**. Pełną dokumentację można znaleźć w [przewodniku REST API](API.md).
- `Api__Enabled`: Główny przełącznik dla tylko do odczytu REST API (domyślnie `false`). Gdy `false`, wszystkie trasy `/api/*` zwracają `404`.
- `Api__AllowAttachmentDownloads`: Pozwala na pobieranie bajtów załączników przez API (domyślnie `true`). Gdy `false`, punkt końcowy załącznika zwraca `403`.
- `Api__AllowMessageDownloads`: Pozwala na pobieranie kompletnych wiadomości przez API, zarówno odbudowanej `/eml`, jak i oryginalnej `/raw` (domyślnie `true`). Gdy `false`, oba punkty końcowe zwracają `403`. Kompletna wiadomość zawiera swoje załączniki, więc **zarówno** ten przełącznik, jak i `Api__AllowAttachmentDownloads` muszą być ustawione na `true`; wyłączenie któregokolwiek z nich wyłącza oba punkty końcowe.
- `Api__EnableSwaggerUi`: Udostępnia Swagger UI w `/apidocs` oraz dokument OpenAPI w `/apidocs/spec/v1.json` (domyślnie `true`). Oba wymagają zalogowanej sesji przeglądarki.
- `Api__DefaultPageSize`: Domyślny rozmiar strony dla punktów końcowych list, gdy nie został określony (domyślnie `20`).
- `Api__MaxPageSize`: Maksymalny dozwolony rozmiar strony; większe żądania są ograniczane (domyślnie `100`).
- `Api__RateLimitPerMinute`: Budżet żądań w oknie stałym na klucz API na minutę (domyślnie `120`).

### 🤖 Ustawienia serwera MCP {#mcp-server-settings}
Opcjonalny serwer MCP (Model Context Protocol) udostępnia agentom AI ten sam dostęp do poczty w trybie tylko do odczytu, co REST API, za pośrednictwem transportu Streamable HTTP. Jest on **domyślnie wyłączony**. Pełną dokumentację można znaleźć w [przewodniku MCP](MCP.md). MCP ponownie wykorzystuje dokładnie te same klucze API (tokeny `ma_...` bearer) oraz to samo ograniczanie dostępu do Konta na poziomie użytkownika.
- `Mcp__Enabled`: Główny przełącznik serwera MCP (domyślnie `false`). Gdy `false`, punkt końcowy `/mcp` zwraca `404`.
- `Mcp__AllowAttachmentDownloads`: Pozwala narzędziu `get_attachment` na zwracanie bajtów, które zawiera Załącznik (domyślnie `true`). Gdy `false`, narzędzie zwraca błąd.
- `Mcp__DefaultPageSize`: Domyślny rozmiar strony używany przez `search_emails`, gdy wywołujący pominie `pageSize` (domyślnie `20`).
- `Mcp__MaxResults`: Górna granica rozmiaru strony dla `search_emails`; większe wartości są ograniczane (domyślnie `100`).
- `Mcp__MaxAttachmentBytes`: Maksymalny rozmiar Załącznika (w bajtach), który narzędzie `get_attachment` zwróci wewnątrz wiadomości jako base64; większe załączniki są odrzucane (domyślnie `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Pozwala narzędziom `get_email_eml` i `get_email_raw` na zwracanie kompletnych wiadomości (domyślnie `true`). Gdy `false`, narzędzia zwracają błąd. Kompletna wiadomość zawiera swoje załączniki, więc **zarówno** ten przełącznik, jak i `Mcp__AllowAttachmentDownloads` muszą być ustawione na `true`; wyłączenie któregokolwiek z nich wyłącza oba narzędzia.
- `Mcp__MaxMessageBytes`: Maksymalny rozmiar wiadomości (w bajtach), który narzędzia `get_email_eml` i `get_email_raw` zwrócą wewnątrz wiadomości; większe wiadomości są odrzucane (domyślnie `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Budżet żądań w oknie stałym na klucz API na minutę (domyślnie `120`).

### 📨 MailSync Settings {#mailsync-settings}
- `MailSync__IntervalMinutes`: Interwał w minutach między synchronizacją e-maili. Jest to domyślna wartość globalna; każde Konto może ją nadpisać indywidualnie na stronie Tworzenie/Edycja (zostaw puste, aby użyć tej wartości domyślnej).
- `MailSync__FullSyncIntervalHours`: Opcjonalna globalna wartość domyślna dla automatycznych pełnych resynchronizacji, w godzinach. Gdy nie jest ustawiona (domyślnie), żadna automatyczna pełna synchronizacja nie zostanie uruchomiona, chyba że dla danego konta ustawiono wartość `FullSyncIntervalHours` na stronie Tworzenie/Edycja. Wartości na poziomie konta nadpisują tę globalną wartość domyślną.
- `MailSync__TimeoutMinutes`: Limit czasu synchronizacji na poziomie konta w minutach. Synchronizacja, która trwa zbyt długo, zatrzymuje się przy następnej granicy wiadomości, zachowuje swoje punkty kontrolne i wznawia pracę przy następnym uruchomieniu; zadanie jest raportowane jako `Timed Out`, a `LastSync` nie jest przesuwane. Dotyczy tylko zaplanowanych synchronizacji; synchronizacja uruchomiona z interfejsu użytkownika (manualna synchronizacja lub pełna resynchronizacja) działa bez limitu czasu. `0` (domyślnie) lub jakakolwiek wartość nie dodatnia oznacza brak limitu czasu. Przejrzyj tę wartość przed aktualizacją, jeśli synchronizujesz duże Skrzynki pocztowe. Zobacz [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Limit czasu połączenia dla połączeń IMAP w sekundach.
- `MailSync__CommandTimeoutSeconds`: Limit czasu polecenia dla poleceń IMAP w sekundach.
- `MailSync__AlwaysForceFullSync`: Czy zawsze wymuszać pełną synchronizację (true/false).
- `MailSync__IgnoreSelfSignedCert`: Czy ignorować certyfikaty samoasygnowane (true/false).
- `MailSync__MaxConcurrentSyncs`: Ile synchronizacji kont może odbywać się jednocześnie. Domyślnie `1` (sekwencyjnie, z zachowaniem kompatybilności wstecznej). Harmonogram uzupełnia wolne miejsca, gdy tylko stają się dostępne, zamiast czekać na całą partię, dzięki czemu jedna wolna Skrzynka pocztowa nie blokuje już pozostałych. Zwiększ tę wartość, aby synchronizować wiele kont jednocześnie — pamiętaj o limitach prędkości dostawcy i lokalnym zużyciu zasobów. Zobacz [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Opcjonalne opóźnienie przesunięcia w sekundach stosowane na końcu każdego zadania synchronizacji konta. Domyślnie `0` (brak opóźnienia). Przydatne, aby uniknąć nagłych skoków obciążenia, gdy `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Ile problemów każdego rodzaju zadanie synchronizacji zapamiętuje dla strony konta. Nieudane foldery, brakujące foldery i nieudane wiadomości są budżetowane oddzielnie, więc natłok jednego rodzaju nie może wyprzeć innych z widoku; wszystko powyżej budżetu jest liczone, a nie przechowywane. Domyślnie `20`. `0` wyłącza szczegóły, pozostawiając jedynie liczniki. Zobacz [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Foldery wykluczone z synchronizacji dla **każdego** konta, oprócz własnej listy wykluczonych folderów danego konta. Domyślnie puste, więc istniejące konfiguracje nie są dotknięte. Obie listy są **sumujące się** — folder jest pomijany, jeśli pasuje do którejkolwiek z nich — i obie używają tych samych reguł dopasowywania: dokładne dopasowanie pełnej ścieżki, dokładne dopasowanie nazwy folderu, dopasowanie przyrostka ścieżki (więc `Drafts` pasuje również do `INBOX/Drafts` i `INBOX.Drafts`) oraz wszystko poniżej folderu, który wskazuje wpis, gdy `MailSync__ExcludeSubfolders` jest włączone. Dopasowanie nie uwzględnia wielkości liter. Przydatne przy imporcie wielu Skrzynek pocztowych z tego samego serwera, gdzie alternatywą jest utrzymywanie identycznej listy wykluczeń dla każdego konta. Domyślnie żaden folder nie jest wykluczony; to, które nazwy warto wymienić, zależy od serwera i jego języka. Przykład dla drzewa Skrzynki pocztowej, które zawiera również foldery kalendarza i kontaktów:
  ```yaml
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Calendars
      - MailSync__GlobalExcludedFolders__2=Contacts
      - MailSync__GlobalExcludedFolders__3=AddressBook
      - MailSync__GlobalExcludedFolders__4=Tasks
      - MailSync__GlobalExcludedFolders__5=TaskList
      - MailSync__GlobalExcludedFolders__6=Notes
      - MailSync__GlobalExcludedFolders__7=NoteList
      - MailSync__GlobalExcludedFolders__8=Journal
  ```
- `MailSync__ExcludeSubfolders`: Czy wpis wykluczenia obejmuje również foldery znajdujące się poniżej wskazanego folderu. Dotyczy to zarówno listy na poziomie konta, jak i listy obejmującej całą instalację, oraz obu dostawców. Domyślnie `false`, co odpowiada zachowaniu sprzed wprowadzenia tej opcji: wpis `Deleted Items` pasuje tylko do tego folderu, a drzewo jest wyłączane z Archiwum poprzez wskazanie jego głównego folderu i każdego folderu poniżej niego. Ustaw na `true`, aby wpis obejmował również drzewo pod nim — wpis `Deleted Items` będzie wtedy również utrzymywał `Deleted Items/2024` i resztę tej gałęzi poza Archiwum. Reguła kotwiczy się na separatorze ścieżki, więc obejmuje całe foldery, a nigdy część nazwy: `Archive` obejmuje `Archive` i `Archive/Team`, a pozostawia folder pocztowy o nazwie `Team Archive Review` bez zmian. `.` również liczy się jako separator, w taki sam sposób, w jaki traktuje go reguła przyrostka ścieżki.

### 📧 Ustawienia przechowywania oryginalnych wiadomości {#original-message-storage-settings}

Domyślnie Archiwum przechowuje tylko przetworzone części wiadomości e-mail, więc wyeksportowany `.eml` jest
rekonstrukcją: kodowania transferu, granice MIME i zawijanie nagłówków różnią się od
wiadomości w formie otrzymanej, a podpisy DKIM nie przechodzą już weryfikacji. Dzięki `RawMessages:Enabled`
przechowywana jest również kompletna oryginalna wiadomość.

| Parametr | Domyślna wartość | Cel |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Przechowuj oryginał każdej nowo zarchiwizowanej wiadomości e-mail. |
| `RawMessages__Storage` | `postgres` | Miejsce przechowywania bajtów: `postgres` lub `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Większe wiadomości są archiwizowane bez ich oryginału. `0` = brak limitu. |
| `RawMessages__IncludeGraph` | `true` | Przechowuj również oryginały dla kont Microsoft 365 (Graph). |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Katalog dla plików wiadomości. Musi wskazywać na zamontowany wolumen w Docker. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Poziomy katalogów, w których rozproszone jest ID wiadomości e-mail. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Weryfikuj SHA-256 przy każdym pobieraniu. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Usuwaj pliki wiadomości, do których nie odnosi się żadna wiadomość e-mail. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Jak często uruchamiane jest to czyszczenie. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Pliki młodsze niż ta wartość nigdy nie są usuwane. |

!!! warning "Backend systemu plików wymaga zamontowanego wolumenu"
    Przy `RawMessages__Storage=filesystem` pliki wiadomości znajdują się poza bazą danych.
    Bez bind mount znajdują się one w warstwie zapisu kontenera i zostaną utracone przy
    następnej rekreacji kontenera, podczas gdy wiersze bazy danych przetrwają. Wykonaj Kopia zapasowa katalogu
    wraz z zrzutem bazy danych.

Zobacz **[Original Message Storage](RawMessages.md)**, aby uzyskać pełny opis: gdzie
serwowany jest oryginał, jakie są backendy przechowywania, jak przenosić istniejące wiadomości między nimi za pomocą
`--migrate-raw-storage` oraz co należy objąć procesem Kopia zapasowa.

### 📈 Ustawienia monitorowania {#monitoring-settings}

| Parametr | Domyślne | Cel |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | Włączone Konta starsze niż ta wartość powodują raport `Degraded` `/api/v1/health`. `0` wyłącza sprawdzanie. |
| `Metrics__Enabled` | `false` | Włącza punkt końcowy metryk Prometheus i jego pętlę odświeżania. |
| `Metrics__RefreshMinutes` | `5` | Jak często jest odbudowywany migawka metryk. Wartości poniżej 1 są podnoszone do 1. |

Punkty końcowe stanu zdrowia i metryk wymagają klucza API administratora oraz `Api__Enabled=true`. Aby uzyskać informacje o punktach końcowych, nazwach metryk, konfiguracji Prometheus oraz kontroli stanu Docker, zobacz
**[Health and Metrics](Monitoring.md)**.

### 📤 Ustawienia BatchRestore {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Liczba e-maili, która wyzwala przetwarzanie asynchroniczne.
- `BatchRestore__MaxSyncEmails`: Maksymalna liczba e-maili dla przetwarzania synchronicznego.
- `BatchRestore__MaxAsyncEmails`: Maksymalna liczba e-maili dla przetwarzania asynchronicznego.
- `BatchRestore__SessionTimeoutMinutes`: Timeout sesji dla operacji batch restore w minutach.
- `BatchRestore__DefaultBatchSize`: Domyślny rozmiar partii dla operacji e-mail.

### 🏢 Ustawienia zarządzania dzierżawą (Tenant Management) {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Maksymalna liczba skrzynek pocztowych, które można dodać w ramach jednej operacji zarządzania dzierżawą. Domyślnie wynosi `1000`. Zwiększ tę wartość dla bardzo dużych dzierżaw lub zmniejsz ją, aby zapobiec przypadkowym masowym importom. Po przekroczeniu limitu operacja zostanie odrzucona z błędem walidacji i żadne konta nie zostaną utworzone. Szczegóły znajdują się w [M365 Tenant Import Guide](M365TenantImport.md).

### 🐄 Ustawienia Mailcow {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Maksymalna liczba skrzynek pocztowych, które mogą zostać utworzone podczas jednego uruchomienia aktywacji Mailcow. Domyślnie wynosi `1000`. Zmniejsz tę wartość, aby zapobiec przypadkowemu masowemu tworzeniu skrzynek, lub zwiększ ją dla dużych instancji. Po przekroczeniu limitu żadne konta nie zostaną utworzone. Więcej szczegółów w sekcji [Mailcow Instances](Mailcow.md).
- Integracja Mailcow wymaga dodatkowo `Security__CredentialEncryptionKey` (patrz [Security Settings](#-security-settings)). Połączenie z instancją oraz aktywacja skrzynek pocztowych zostaną odrzucone, jeśli szyfrowanie poświadczeń jest wyłączone, dzięki czemu klucz API i wygenerowane hasła aplikacji nigdy nie zostaną zapisane jako tekst jawny.
- Wymagany jest dostęp wychodzący do Mailcow HTTPS API oraz do hosta IMAP przez TLS (zazwyczaj port `993`).

### 📦 Ustawienia BatchOperation {#batchoperation-settings}
- `BatchOperation__BatchSize`: Rozmiar partii dla operacji e-mail.
- `BatchOperation__PauseBetweenEmailsMs`: Przerwa między poszczególnymi e-mailami w milisekundach.
- `BatchOperation__PauseBetweenBatchesMs`: Przerwa między partiami w milisekundach.

### 📤 Ustawienia Offload {#offload-settings}
Ustawienia dla [date-windowed offload](Offload.md). Wszystkie wartości domyślne odtwarzają zachowanie aplikacji sprzed wprowadzenia tej funkcji, więc instalacja, która nie konfiguruje tej sekcji, nie ulega zmianie.
- `Offload__MaxConcurrentJobs`: Określa, ile zadań przywracanie lub offload może działać jednocześnie. Domyślnie jest to `1`, co zapewnia ściśle szeregowe przetwarzanie, z którego kolejka zadań korzystała zawsze. Niezależnie od tej wartości, uruchamiane jest maksymalnie jedno zadanie na docelową Skrzynka pocztowa.
- `Offload__PrefetchMaxMessages`: Górna granica liczby wiadomości indeksowanych ze Skrzynka pocztowa w celu wykrywania duplikatów. Domyślnie jest to `500000`. Powyżej tej wartości sprawdzanie zostaje ograniczone do pojedynczego Folder i loguje informację o zmniejszeniu zakresu.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Źródłowe Foldery, które nigdy nie są przesyłane (offload), dopasowywane przed zmianą nazwy i obejmujące podfoldery. Domyślnie puste.
- `Offload__FolderRenameMap__<SourceFolder>`: Przepisuje początkowe segmenty ścieżki źródłowego Folder, na przykład `Offload__FolderRenameMap__Sent Items=Sent`. Domyślnie puste.
- `Offload__MarkAsSeen`: Czy dopisana poczta jest oznaczana jako przeczytana. Domyślnie jest to `true`, co odpowiada istniejącemu zachowaniu przy operacji przywracanie.

Oba ustawienia dotyczące Folder są celowo puste: przepisanie lub usunięcie Folder bez wyraźnej prośby zaskoczyłoby każdego, kto korzysta już z ścieżki przywracanie. Uruchomienie bez konfiguracji migruje zatem wszystko, włączając Foldery ze spamem, i tworzy drugi zestaw specjalnych folderów obok folderów docelowych.

### 📊 Ustawienia śledzenia przepustowości {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Włącz lub wyłącz śledzenie przepustowości dla obsługi limitów prędkości IMAP (true/false). Domyślnie ustawione jest `false`. Gdy funkcja jest włączona, system śledzi zużycie przepustowości na każde Konto i może wstrzymać Synchronizacja, gdy zostaną osiągnięte limity dostawcy. Aby uzyskać szczegółowe informacje, zobacz [Rate Limit Handling](RateLimitHandling.md).
- `BandwidthTracking__DailyLimitMb`: Dzienny limit pobierania w megabajtach na każde Konto. Domyślnie ustawione jest `25000` (25 GB). W przypadku dostawców z limitami przepustowości, ustaw tę wartość tak, aby odpowiadała ich limitowi prędkości (np. `2500` dla dostawców z dziennymi limitami ok. ~2500 MB). System wstrzyma Synchronizacja po osiągnięciu tego limitu.
- `BandwidthTracking__WarningThresholdPercent`: Procent dziennego limitu, przy którym rejestrowane są komunikaty ostrzegawcze. Domyślnie ustawione jest `80`. Gdy zużycie przepustowości osiągnie ten procent, ostrzeżenia są zapisywane w celu monitorowania zbliżających się limitów.
- `BandwidthTracking__PauseHoursOnLimit`: Liczba godzin, na które Synchronizacja zostaje wstrzymana po osiągnięciu dziennego limitu. Domyślnie ustawione jest `24`. Po tym okresie flaga limitu jest automatycznie usuwana, a Synchronizacja zostaje wznowiona.
- `BandwidthTracking__TrackUploadBytes`: Czy również śledzić przepustowość przesyłania (true/false). Domyślnie ustawione jest `false`. Większość dostawców IMAP ogranicza tylko pobieranie, więc zazwyczaj nie jest to wymagane.

### 🎯 Ustawienia wyboru {#selection-settings}
- `Selection__MaxSelectableEmails`: Maksymalna liczba wiadomości e-mail, które mogą zostać wybrane jednocześnie.

### 👁️ View Settings (Privacy & Display) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Kontroluje domyślny tryb widoku e-mail w celu zapobiegania śledzeniu i ochrony prywatności (true/false). Domyślnie ustawione jest `false`.
  - Gdy ustawione na `true`: E-maile są domyślnie otwierane w widoku tekstowym (plain-text), co zapobiega automatycznemu ładowaniu pikseli śledzących, zewnętrznych obrazów i beaconów webowych. Jest to zalecane dla użytkowników dbających o prywatność i śledzenie e-maili.
  - Gdy ustawione na `false`: E-maile są domyślnie otwierane w widoku HTML, wyświetlając sformatowaną wiadomość wraz z obrazami i stylizacją.
  - Użytkownicy mogą przełączać się między widokiem HTML a tekstowym za pomocą przycisku na stronie szczegółów e-mail (widocznego tylko wtedy, gdy dostępne są oba formaty).
  - Link "Full View" respektuje aktualnie wybrany tryb widoku.

- `View__BlockExternalResources`: Blokuje zewnętrzne zasoby (zdjęcia zdalne, zewnętrzny CSS, zewnętrzne skrypty, czcionki webowe itp.) w widokach e-mail HTML, aby zapobiegać śledzeniu i zwiększyć prywatność (true/false). Domyślnie ustawione jest `false`.
  - Gdy ustawione na `true`: Zewnętrzne zasoby są filtrowane podczas wyświetlania wiadomości HTML. Wyświetlane są tylko treści inline oraz URI danych (w tym inline załączniki poprzez referencje `cid:`).
  - Gdy ustawione na `false`: Wiadomości HTML są wyświetlane wraz ze wszystkimi ich oryginalnymi zewnętrznymi zasobami.
  - **Ważne**: To ustawienie wpływa wyłącznie na **wyświetlanie** e-maili. Zarchiwizowane e-maile są przechowywane w bazie danych całkowicie bez zmian, z zachowaniem wszystkich oryginalnych treści.
  - Blokowane zasoby obejmują:
    - Zdjęcia zdalne (piksele śledzące, zewnętrzne obrazy hostowane na serwerach)
    - Zewnętrzne arkusze stylów CSS
    - Zewnętrzne czcionki za pomocą @font-face
    - Zewnętrzne importy CSS za pomocą @import
    - Zewnętrzne obrazy tła
  - Dozwolone zasoby:
    - Obrazy inline osadzone jako data: URIs
    - Załączniki inline referencjonowane poprzez cid: URIs
    - Style inline CSS i tagi style
  - Ustawienie to działa niezależnie od `DefaultToPlainText` i zapewnia dodatkową warstwę ochrony prywatności podczas przeglądania wiadomości HTML.

### 📊 Ustawienia pulpitu nawigacyjnego {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Określa, czy karty liczników zawierają swoje części przychodzące i wychodzące, a karta Konto liczbę odrębnych domen. Domyślnie ustawione jest na `true`. Ustawienie na `false` sprawia, że te liczby nie są obliczane, lecz zamiast obliczania są ukrywane: liczniki odczytują zwykłą sumę, a liczba Załącznik przestaje łączyć się z pocztą, do której jest przypisany, co stanowi zapytanie dodawane przez te części.
- `Dashboard__SelectablePeriods`: Określa, czy wykresy na pulpicie nawigacyjnym oferują rozdzielczość, okres oraz strzałki do ich zmiany. Domyślnie ustawione jest na `true`. Ustawienie na `false` sprawia, że wykresy pokazują ostatnie dwanaście miesięcy w podziale na miesiące oraz nadawców z całego Archiwum, punkt końcowy wykresu odpowiada tak, jakby nie istniał, a najstarsza data wysłania nie jest wyszukiwana.
- `Dashboard__CacheSeconds`: Określa, jak długo obliczane statystyki pulpitu nawigacyjnego (sumy wraz z podziałem na przychodzące i wychodzące, liczniki na każde Konto, serie wykresów, najczęstsi nadawcy, ostatnie e-maile, rozmiar bazy danych) są przechowywane w pamięci podręcznej serwera. Domyślnie `60` sekund. Ustawienie na `0` wyłącza buforowanie i zawsze przelicza statystyki na nowo. Każdy wybór rozdzielczości i okresu dla wykresu jest buforowany oddzielnie, więc pulpit przełączany między wyborami przechowuje jeden wpis na każdy wybór. Wyższe wartości zmniejszają obciążenie bazy danych w dużych środowiskach kosztem bardziej nieaktualnych danych. Odznaki statusu Synchronizacja, wartości składowania i lista ostatnich e-maili są zawsze pobierane na żywo i nie są objęte tym buforowaniem.
- `Dashboard__RefreshIntervalMinutes`: Określa, jak często usługa działająca w tle `DashboardStatsRefreshService` przelicza kosztowne agregaty pulpitu nawigacyjnego (całkowite sumy wraz z podziałem na przychodzące i wychodzące, liczniki na każde Konto, liczbę domen Kont, domyślną serię wykresów z jej najczęstszymi nadawcami, rozmiar bazy danych) do tabeli bazy danych `DashboardStatsCache`. W instalacjach z kilkoma milionami wiadomości pulpit nawigacyjny odczytuje te przygotowane wartości zamiast agregować całe Archiwum w ścieżce żądania, co skraca czas pierwszego ładowania z około 10 sekund do milisekund. Punkt końcowy wykresu odpowiada na domyślny wybór (miesiące w ciągu jednego roku, nadawcy przychodzący, bieżący okres) z tego samego wiersza, więc powrót do niego pokazuje liczby z pierwszego renderowania; każdy inny wybór jest obliczany na żądanie i przechowywany w powyższej pamięci podręcznej. Domyślnie `15` minut. Ustawienie na `0` wyłącza odświeżanie w tle; pulpit nawigacyjny ponownie oblicza statystyki na żywo (przy nadal obowiązującej powyższej pamięci podręcznej). Wyświetlane wartości mogą opóźniać się względem faktycznego stanu Archiwum o skonfigurowany interwał. Wiersz obliczony z innymi wartościami `ShowDirectionSplits` lub `SelectablePeriods` niż bieżące nie jest używany; pulpit nawigacyjny oblicza dane na żywo aż do następnego odświeżenia.

### 🔄 Jobs Settings {#jobs-settings}
- `Jobs__RefreshSeconds`: Jak często strona zadań w tle odświeża się sama, gdy karta przeglądarki jest otwarta. Domyślnie wynosi to `30` sekund. Ustaw `0`, aby wyłączyć automatyczne odświeżanie i odświeżać stronę ręcznie. Każde odświeżenie przebudowuje stronę ze wszystkich ośmiu źródeł zadań, więc w instalacjach z wieloma kontami dłuższy interwał zmniejsza obciążenie, mnożone przez każdą otwartą kartę na tej stronie.

### 🗃️ Ustawienia Npgsql {#npgsql-settings}
- `Npgsql__CommandTimeout`: Limit czasu oczekiwania dla poleceń bazy danych w sekundach.

### 📥 Ustawienia przesyłania {#upload-settings}
- `Upload__MaxFileSizeGB`: Maksymalny rozmiar pliku dla przesyłania w GB.
- `Upload__KeepAliveTimeoutHours`: Timeout keep alive dla przesyłania w godzinach.
- `Upload__RequestHeadersTimeoutHours`: Timeout dla nagłówków żądań w godzinach.

### 📂 Ustawienia lokalnego importu {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, itp.: Biała lista lokalnych katalogów, z których komendy importu CLI (`--import-mbox`, `--import-eml`) mogą odczytywać pliki. Każdy wpis to ścieżka wewnątrz kontenera. Musisz zamontować pliki do importu w jednym z tych katalogów, używając wolumenów Docker. Domyślnie: `/data/import` (automatycznie ustawiane na `/app/uploads` jako rozwiązanie awaryjne).
  - Podczas używania `docker exec` do uruchomienia komendy importu, ścieżka do pliku przekazana za pomocą `--file` musi znajdować się w obrębie jednej z tych dozwolonych ścieżek.
  - Ten środek bezpieczeństwa zapobiega dowolnemu dostępowi do systemu plików z komend CLI.
  - Można skonfigurować wiele ścieżek dla różnych źródeł importu.
  - Aby uzyskać szczegółowe instrukcje dotyczące użycia, zobacz [CLI commands](CLI.md).

### 📄 Ustawienia importu CSV {#csv-import-settings}
- `CsvImport__MaxRows`: Maksymalna liczba wierszy CSV (skrzynek pocztowych) przetwarzanych podczas jednego masowego importu. Domyślnie wynosi `5000`. Zwiększ tę wartość dla dużych wdrożeń; zmniejsz ją, aby ograniczyć wpływ pojedynczego przebiegu importu na obciążenie bazy danych.
- `CsvImport__MaxFileSizeBytes`: Maksymalna dozwolona wielkość (w bajtach) przesyłanego pliku CSV. Domyślnie wynosi `10000000` (10 MB). Dostosuj tę wartość do swoich limitów przesyłania, jeśli jest to konieczne.
- Aby uzyskać szczegółowe instrukcje dotyczące masowego importu kont IMAP za pomocą CSV, zobacz [Account Import Guide](Account-Import.md).

### 📤 Ustawienia eksportu dziennika audytu {#audit-export-settings}
Ustawienia strony eksportu danych audytowych (tylko dla administratorów, dostępne ze strony Logs). Eksport generuje pakiet ZIP z tabelarycznymi danymi masowymi (INDEX.XML + tabele CSV + DTD) z istniejącego Archiwum na potrzeby zewnętrznych narzędzi audytowych. Szczegóły dotyczące użytkowania znajdują się w [Audit Data Export Guide](AuditExport.md).
- `AuditExport__DataSupplierName`: Domyślna wartość pola formularza "Data supplier name", która identyfikuje Twoją organizację w wyeksportowanym pliku indeksu. Domyślnie: puste.
- `AuditExport__DataSupplierLocation`: Domyślna wartość pola formularza "Data supplier location" (np. siedziba firmy). Domyślnie: puste.
- `AuditExport__Comment`: Domyślna wartość pola formularza "Comment", dołączana jako tekst wolny w wyeksportowanym pliku indeksu. Domyślnie: puste.
- `AuditExport__OutputDirectory`: Katalog, w którym przechowywane są wygenerowane pliki ZIP. Ścieżki względne są rozwiązywane względem głównego katalogu aplikacji. Domyślnie: `exports/audit`.
- `AuditExport__RetentionDays`: Liczba dni, po których ukończone pliki eksportu są usuwane podczas codziennego czyszczenia. Domyślnie: `30`.
- `AuditExport__MaxRangeYears`: Maksymalny dozwolony zakres między datą rozpoczęcia a datą zakończenia okresu eksportu. Domyślnie: `10`.
- Pola formularza są wstępnie wypełnione tymi wartościami domyślnymi, ale mogą być edytowane dla każdego eksportu.
- Każdy eksport zapisuje wpis rozpoczęcia i wpis wyniku w dzienniku dostępu (typ "Audit Data Export"), dzięki czemu historia jest bezpieczna pod kątem rewizji bez wprowadzania jakichkolwiek zmian w schemacie bazy danych.

### 🔒 Ustawienia polityki usuwania {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Kontroluje, czy dozwolone jest ręczne usuwanie zarchiwizowanych wiadomości e-mail (true/false). Domyślnie ustawione jest na `true`. Gdy ustawione na `false`:
  - Wszystkie zarchiwizowane wiadomości e-mail są blokowane (`IsLocked = true`) podczas uruchamiania za pomocą wyzwalacza zgodności bazy danych, co zapobiega jakimkolwiek modyfikacjom lub usuwaniu na poziomie bazy danych.
  - Ręczne usuwanie (pojedyncze i masowe) jest blokowane na poziomie aplikacji wraz z komunikatem o błędzie.
  - Domyślna wartość kolumny jest dostosowywana tak, aby nowo importowane wiadomości e-mail również były blokowane.
  - Aktualny stan polityki jest logowany do tabeli AccessLogs przy każdym uruchomieniu (widoczny na stronie Logs jako wpisy "Deletion Policy") w celu zapewnienia możliwości przeprowadzenia audytu.
  - Usuwanie w ramach lokalnego Przechowywania jest wyłączone z tej zasady: wiadomości e-mail, które podlegają skonfigurowanemu okresowi Przechowywania, są nadal usuwane (są odblokowywane bezpośrednio przed usunięciem w ramach procesu Przechowywania).
- `DeletionPolicy__MaxBulkDeleteEmails`: Górna granica dla jednego masowego usuwania opartego na filtrze (*Delete all matches*). Domyślnie `100000`. Filtr pasujący do większej liczby wiadomości e-mail zostaje odrzucony z komunikatem z prośbą o zawężenie wyników; zadanie nigdy nie dokonuje cichego ucinania wyników. Wiadomości e-mail zablokowane przez politykę Przechowywania są pomijane i raportowane w statusie zadania, a nie odblokowywane. Zadanie ponownie sprawdza ten limit oraz `DeletionPolicy__DeletionAllowed` podczas uruchamiania, dzięki czemu zadanie ustawione w kolejce przed zmianą polityki zostaje zatrzymane, zamiast działać z poprzednim ustawieniem.
- **Ochrona niezmienności:** Gdy `IsLocked = true`, wyzwalacz zgodności bazy danych (`prevent_locked_email_changes`) blokuje WSZELKIE modyfikacje wiersza wiadomości e-mail — wszystkie kolumny są chronione, a nie tylko stała lista pól. Jedynymi wyłączonymi kolumnami są `IsLocked` samo w sobie (aby odblokowanie na potrzeby usuwania w ramach Przechowywania i zastosowania polityki przy uruchomieniu pozostało możliwe) oraz `FolderName` (aby Synchronizacja IMAP mogła zaktualizować Folder, gdy wiadomość e-mail zostanie przeniesiona po stronie serwera). Ochrona jest niezależna od kolumn (oparta na porównywaniu JSONB) i automatycznie obejmuje przyszłe rozszerzenia schematu.

### 🕐 Ustawienia TimeZone {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Strefa czasowa używana do wyświetlania znaczników czasu wiadomości e-mail w interfejsie użytkownika. Używa identyfikatorów stref czasowych IANA (np. "Europe/Berlin", "Asia/Tokyo"). Domyślną wartością jest "Etc/UCT" w celu zapewnienia wstecznej kompatybilności. Podczas importowania wiadomości e-mail znaczniki czasu zostaną skonwertowane na tę strefę czasową w celach wyświetlania.

### 🎉 Ustawienia ReleaseNotes (Ekran powitalny aktualizacji wersji) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: Ciąg znaków wersji, który zgłasza ta instancja. Jest on dopasowywany do tagów wersji w GitHub i decyduje o tym, czy istnieje nowsza wersja. Gdy jest pusty, dziennik zmian (changelog) powraca do wersji skompilowanego zestawu, a podpowiedź aktualizacji pozostaje wyłączona.
- `ReleaseNotes__Enabled`: Włącz lub wyłącz ekran powitalny aktualizacji wersji (true/false). Domyślnie ustawione jest `true`. Gdy funkcja jest włączona, administratorzy zobaczą jednorazowe okno modalne z dziennikiem zmian po aktualizacji aplikacji, pokazujące informacje o wydaniu pobrane z GitHub Releases dla bieżącej wersji. Każdy administrator może zamknąć to okno, a pojawi się ono ponownie dopiero przy nowej wersji. Ustaw na `false`, aby całkowicie wyłączyć tę funkcję, w tym podpowiedź aktualizacji.
- `ReleaseNotes__UpdateHintEnabled`: Wyświetl administratorom małe powiadomienie typu toast w prawym dolnym rogu, gdy GitHub opublikuje nowszą stabilną wersję niż zainstalowana (true/false). Domyślnie ustawione jest `true`. Powiadomienie zawiera link do danej wersji i jest zamykane dla każdej wersji z osobna w przeglądarce. Szkice i wersje przedpremierowe są ignorowane. Wymaga `ReleaseNotes__AppVersion`; bez tego żadna podpowiedź nie zostanie wyświetlona.
- `ReleaseNotes__UpdateCheckIntervalHours`: Jak często lista wersji GitHub jest odświeżana, w godzinach. Domyślnie ustawione jest `6`. Krótszy interwał sprawia, że nowa wersja staje się widoczna szybciej w działającej długotrwale instancji, dłuższy interwał zmniejsza liczbę wywołań GitHub API. Minimum to `1`.

### 🔧 Ustawienia konserwacji bazy danych {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Włącz lub wyłącz automatyczną codzienną konserwację bazy danych (true/false). Domyślnie ustawione jest `false`. Gdy funkcja jest włączona, system będzie automatycznie uruchamiał operacje VACUUM ANALYZE, aby zoptymalizować wydajność bazy danych i zapobiec jej rozrostowi. Więcej szczegółów znajduje się w [Database Maintenance Guide](DatabaseMaintenance.md).
- `DatabaseMaintenance__DailyExecutionTime`: Pora dnia, w której powinna odbywać się konserwacja bazy danych, w formacie 24-godzinnym (HH:mm). Domyślnie ustawione jest `02:00`. Wybierz czas o niskiej aktywności systemu.
- `DatabaseMaintenance__TimeoutMinutes`: Maksymalny dozwolony czas operacji konserwacyjnych w minutach. Domyślnie ustawione jest `30`. Zwiększ tę wartość dla większych baz danych.

### 🧬 Ustawienia Deduplikacja Załączników {#attachment-deduplication-settings}
Deduplikacja Załączników przechowuje każdą unikalną zawartość załącznika tylko raz (adresowaną przez SHA-256) i jest to **kluczowa funkcja, która jest zawsze włączona** – celowo nie przewidziano przełącznika włącz/wyłącz. Można dostosować jedynie poniższe parametry wsadowe/harmonogramu. Pełne szczegóły znajdują się w [Attachment Deduplication Guide](AttachmentDeduplication.md).

- `AttachmentDeduplication__BatchSize`: Liczba istniejących załączników migrowanych podczas jednej transakcji podczas jednorazowej migracji w tle danych istniejących. Domyślnie wynosi `200`. Większe wartości przyspieszają migrację, ale zużywają więcej pamięci/obciążają bazę danych w ramach jednej partii.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Opcjonalna przerwa (w milisekundach) między partiami migracji w celu ograniczenia obciążenia bazy danych w zajętych systemach. Domyślnie wynosi `0` (brak przerwy).
- `AttachmentDeduplication__StartupDelaySeconds`: Opóźnienie (w sekundach) po uruchomieniu aplikacji przed rozpoczęciem migracji w tle, co daje czas na zakończenie migracji schematu. Domyślnie wynosi `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Interwał (w godzinach) stale działającego procesu garbage collection, który usuwa zawartości załączników niebędące już powiązane z żadną wiadomością e-mail. Domyślnie wynosi `12`. Proces ten działa niezależnie od `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Limit czasu żądania do bazy danych (w sekundach) dla operacji wsadowych migracji (INSERT z haszowaniem SHA-256 oraz UPDATE). Domyślnie wynosi `300` (5 minut). Zwiększ tę wartość dla bardzo dużych baz danych lub załączników, lub zmniejsz ją, jeśli chcesz szybciej wykrywać błędy. Jeśli partia nadal przekroczy limit czasu, usługa automatycznie podejmie próbę ponownie z połową rozmiaru partii.

### 💾 Ustawienia przechowywania konta {#account-storage-settings}
Wyświetlanie przechowywania na konto pokazuje użycie pamięci bazy danych (wszystkie pola wiadomości + załączniki) dla każdego konta w tabeli „Account Overview” w Dashboardzie oraz w tabeli „Show All” w MailAccounts. Autarkiczna usługa działająca w tle (`AccountStorageRefreshService`) oblicza wartości, sumując PostgreSQL `pg_column_size` dla każdej kolumny (obejmując wszystkie pola wiadomości bez odczytywania treści wiadomości) i buforuje je w tabeli `AccountStorageCache`. Usługa ta działa niezależnie od `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Włącz lub wyłącz usługę odświeżania przechowywania (true/false). Domyślnie ustawione jest `true`. Gdy usługa jest włączona, podczas uruchamiania wykonuje wznowialne uzupełnianie danych (backfill) dla oczekujących kont (bezpieczne w przypadku awarii dzięki `AccountStorageBackfillState`), a następnie wykonuje codzienne pełne odświeżanie. Gdy usługa jest wyłączona, wartości przechowywania są aktualizowane tylko podczas synchronizacji, importowania lub usuwania wiadomości e-mail, ale wyświetlane wartości mogą z czasem stać się nieaktualne.
- `AccountStorage__DailyExecutionTime`: Godzina (format 24-godzinny `HH:mm`) codziennego pełnego odświeżania wszystkich kont. Domyślnie ustawione jest `02:30`. Wybierz godzinę podczas niskiej aktywności systemu.
- `AccountStorage__BackfillDelayMs`: Opóźnienie (w milisekundach) między kontami podczas początkowego uzupełniania danych przy uruchamianiu. Domyślnie ustawione jest `5000`. Niższe wartości przyspieszają uzupełnianie danych, ale zwiększają obciążenie bazy danych; zwiększ tę wartość dla bardzo dużych archiwów, aby uniknąć przeciążenia bazy danych.
- `AccountStorage__RefreshBatchDelayMs`: Opóźnienie (w milisekundach) między kontami podczas codziennego pełnego odświeżania. Domyślnie ustawione jest `1000`. Niższe wartości przyspieszają odświeżanie, ale zwiększają obciążenie bazy danych; zwiększ tę wartość dla bardzo dużych archiwów.
- `AccountStorage__CommandTimeoutSeconds`: Limit czasu polecenia bazy danych (w sekundach) dla obliczeń przechowywania na konto (`pg_column_size` dla wszystkich wierszy konta). Domyślnie ustawione jest `300` (5 minut). Obliczenie odczytuje każdy wiersz wiadomości, w tym dane TOAST, co może zająć znacznie więcej niż minutę w przypadku bardzo dużych archiwów (ponad 250 tys. wiadomości e-mail); poprzedni domyślny limit 30 sekund w Npgsql powodował ciche błędy odświeżania i sprawiał, że dashboard wyświetlał nieaktualne wartości. Zwiększ tę wartość, jeśli obliczenia wciąż przekraczają limit czasu w Twoim Archiwum.

> 💡 **Uwaga**: Wartości przechowywania są odświeżane natychmiast po każdej synchronizacji, imporcie lub usunięciu danych w ramach polityki Przechowywanie, dzięki czemu wyświetlane wartości pozostają aktualne nawet bez codziennego odświeżania. Codzienne odświeżanie stanowi siatkę bezpieczeństwa, która wyłapuje przypadki brzegowe (np. bezpośrednie zmiany w bazie danych).

### 📝 Ustawienia logowania {#logging-settings}
- `Logging__LogLevel__Default`: Domyślny poziom logowania dla aplikacji. Dostępne poziomy to: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Domyślnie ustawiono `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Poziom logowania dla komunikatów frameworka ASP.NET Core. Domyślnie ustawiono `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Poziom logowania dla poleceń bazy danych Entity Framework. Domyślnie ustawiono `Warning`.

### 🛡️ Ustawienia bezpieczeństwa {#security-settings}
- `AllowedHosts`: Lista nazw hostów oddzielonych średnikami, którym aplikacja może służyć. Pomaga to zapobiegać atakom na nagłówek HTTP Host. Przykład: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Ważne**: Nie używaj `*` w środowiskach produkcyjnych, ponieważ wyłącza to walidację nagłówka hosta.

#### 🔐 Szyfrowanie poświadczeń w spoczynku {#credential-encryption-at-rest}
Poświadczenia konta pocztowego (hasło IMAP, sekret klienta M365 i tokeny MSA OAuth) mogą być szyfrowane w bazie danych. Klucz szyfrujący jest przechowywany **poza** bazą danych (zmienna środowiskowa / Docker secret), więc sam zrzut bazy danych nie wystarczy do odczytania poświadczeń.

- `Security__CredentialEncryptionKey`: Zakodowany w Base64 256-bitowy (32 bajty) klucz szyfrujący. Wygeneruj go za pomocą:
  ```bash
  openssl rand -base64 32
  ```
  Ustaw tę wartość jako zmienną środowiskową (zalecane: `Security__CredentialEncryptionKey` w pliku compose lub środowisku kontenera). Aplikacja nie obsługuje sekretów Docker w stylu `_FILE` — jeśli używasz sekretu Docker, zamontuj go i załaduj jego zawartość do zmiennej środowiskowej w swoim entrypoint. Nigdy nie przesyłaj klucza do `appsettings.json`. Gdy klucz zostanie skonfigurowany, poświadczenia są szyfrowane podczas zapisu, a usługa działająca w tle szyfruje istniejące poświadczenia w formie czystego tekstu jednorazowo.
- `Security__EncryptAccountCredentials`: Włącz lub wyłącz szyfrowanie nowych/zaktualizowanych wartości poświadczeń (true/false). Domyślnie ustawione jest `true`. Gdy nie skonfigurowano klucza, poświadczenia są przechowywane jako czysty tekst, a podczas uruchamiania rejestrowane jest ostrzeżenie — istniejące instalacje nie są dotknięte, dopóki nie zostanie podany klucz.
- `Security__CredentialEncryptionBackfill`: Czy usługa działająca w tle powinna szyfrować istniejące poświadczenia w formie czystego tekstu podczas uruchamiania (true/false). Domyślnie ustawione jest `true`. Brak działania bez skonfigurowanego klucza.
- `Security__CredentialEncryptionKeyPrevious`: Opcjonalny poprzedni klucz, używany wyłącznie do deszyfrowania wartości zaszyfrowanych za jego pomocą (do rotacji kluczy). Nowe wartości są zawsze szyfrowane za pomocą `Security__CredentialEncryptionKey`.

  > ⚠️ **Rotacja kluczy**: po rotacji, pozostaw `Security__CredentialEncryptionKeyPrevious` skonfigurowany **dopóki każde poświadczenie nie zostanie nadpisane** (np. poprzez edycję konta lub automatyczne odświeżenie tokenu OAuth) — istniejące wartości zaszyfrowane starym kluczem *nie* są automatycznie szyfrowane nowym kluczem. Po usunięciu poprzedniego klucza wszelkie poświadczenia nadal nim zaszyfrowane staną się nieczytelne i będą musiały zostać wprowadzone ponownie. Dopóki jest on skonfigurowany, musi być chroniony tak samo jak obecny klucz.

> ⚠️ **Chroń klucz**: Bez klucza zaszyfrowanych poświadczeń nie można odzyskać i trzeba je wprowadzić ponownie. Wykonaj kopię zapasową klucza razem z (ale oddzielnie od) kopiami zapasowymi bazy danych.
>
> 💡 **Istniejące instalacje**: Aktualizuj bezpiecznie — dopóki nie zostanie ustawiony `Security__CredentialEncryptionKey`, nic się nie zmienia. Ustaw klucz, gdy będziesz gotowy; istniejące poświadczenia w czystym tekście pozostaną czytelne i zostaną zaszyfrowane w tle.

### 🔐 Konfiguracja OIDC {#oidc-configuration}

Szczegółowe instrukcje dotyczące konfiguracji uwierzytelniania OpenID Connect znajdują się w [OIDC Implementation Guide](OIDC_Implementation.md).

#### Podstawowe ustawienia OIDC {#basic-oidc-settings}
- `OAuth__Enabled`: Włącz lub wyłącz uwierzytelnianie OIDC (true/false)
- `OAuth__Authority`: Adres URL autorytetu OpenID Connect (np. https://sts.windows.net/{TENANT-ID}/ dla Azure AD)
- `OAuth__ClientId`: client ID przypisany przez dostawcę tożsamości
- `OAuth__ClientSecret`: client secret przypisany przez dostawcę tożsamości
- `OAuth__DisplayName`: Opcjonalna nazwa wyświetlana na przycisku logowania OIDC i stronie automatycznego przekierowania (np. `PocketID SSO`). Jeśli zostanie pominięta, używana jest ogólna etykieta "Login with OAuth".
- `OAuth__ClientScopes__0`: Pierwszy zakres (scope) żądany od dostawcy tożsamości (openid)
- `OAuth__ClientScopes__1`: Drugi zakres (scope) żądany od dostawcy tożsamości (profile)
- `OAuth__ClientScopes__2`: Trzeci zakres (scope) żądany od dostawcy tożsamości (email)
- `OAuth__ResponseMode`: Opcjonalny tryb odpowiedzi żądania autoryzacji (`query`, `fragment` lub `form_post`). Domyślnie ustawione jest `form_post`, domyślna wartość ASP.NET Core. Ustaw na `query` dla dostawców, którzy nie obsługują `form_post`, takich jak Synology SSO Server. Zobacz [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Automatycznie zatwierdzaj nowych użytkowników OIDC bez konieczności ręcznego zatwierdzenia przez administratora (true/false). Domyślnie `false`.

#### Ustawienia aprovisionowania użytkowników {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Automatycznie zatwierdzaj nowych użytkowników OIDC bez konieczności ręcznego zatwierdzenia przez administratora (true/false). Domyślnie `false`. Gdy funkcja jest włączona, użytkownicy uwierzytelniający przez dostawcę OIDC są natychmiast aktywowani i mogą uzyskać dostęp do aplikacji. Gdy funkcja jest wyłączona (domyślnie), nowi użytkownicy OIDC są tworzeni jako nieaktywni i wymagają ręcznej aktywacji przez administratora. Więcej informacji znajduje się w [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users).
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, itd.: Adresy e-mail, które powinny być automatycznie aprovisionowane jako administratorzy. Użytkownicy z tymi adresami e-mail zostaną utworzeni jako aktywni administratorzy przy pierwszym logowaniu OAuth, z pominięciem normalnego procesu zatwierdzania. Dopasowanie adresów e-mail nie uwzględnia wielkości liter.

#### Ustawienia logowania bezhasłowego {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Wyłącz logowanie nazwą użytkownika/hasłem (true/false). Domyślnie `false`. Gdy funkcja jest włączona, formularz hasła jest ukryty **oraz** serwer odrzuca każdy POST hasła, więc dostępna jest tylko metoda logowania OAuth. Wymaga `OAuth__Enabled=true`. Jeśli OAuth jest wyłączone, ustawienie to nie ma wpływu.
- `OAuth__AutoRedirect`: Automatycznie przekierowuj użytkowników do dostawcy OAuth (true/false). Domyślnie `false`. Wymaga, aby `OAuth__DisablePasswordLogin` było ustawione na `true`. Użytkownicy zobaczą krótki ekran ładowania przed przekierowaniem.

#### Przykład: Pełna konfiguracja typu OIDC-First {#example-full-oidc-first-configuration}
```yaml
environment:
  - OAuth__Enabled=true
  - OAuth__Authority=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0
  - OAuth__ClientId=your-client-id
  - OAuth__ClientSecret=your-client-secret
  - OAuth__DisplayName=PocketID SSO
  - OAuth__ClientScopes__0=openid
  - OAuth__ClientScopes__1=profile
  - OAuth__ClientScopes__2=email
  - OAuth__DisablePasswordLogin=true
  - OAuth__AutoRedirect=true
  - OAuth__AutoApproveUsers=true
  - OAuth__AdminEmails__0=admin@example.com
  - OAuth__AdminEmails__1=manager@example.com
```

## 🔐 Konfiguracja Kestrel HTTPS (Opcjonalnie) {#kestrel-https-configuration-optional}

Chociaż aplikacja ma być dostępna poprzez reverse proxy z HTTPS, można również skonfigurować serwer WWW Kestrel tak, aby używał certyfikatów SSL/TLS. Zapewnia to szyfrowanie end-to-end między reverse proxy a kontenerem aplikacji.

### Kroki konfiguracji {#configuration-steps}

1. **Wygeneruj lub zdobądź certyfikat SSL** w formacie PFX (np. `localhost.pfx`)

2. **Dodaj następujące zmienne środowiskowe** do swojego `docker-compose.yml` dla usługi `mailarchive-app`:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Zaktualizuj mapowanie portów** w usłudze `mailarchive-app`:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Dodaj mapowanie wolumenu** dla certyfikatu:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Umieść plik certyfikatu** (np. `localhost.pfx`) w katalogu `./certs` na systemie hosta.

### Wyjaśnienia zmiennych środowiskowych {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: URL punktu końcowego HTTP (domyślnie: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: URL punktu końcowego HTTPS (domyślnie: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Ścieżka do pliku certyfikatu PFX wewnątrz kontenera
- `Kestrel__Endpoints__Https__Certificate__Password`: Hasło do pliku certyfikatu PFX

> 💡 **Uwaga**: Ta konfiguracja jest opcjonalna. Jeśli używasz reverse proxy z HTTPS (zalecane), komunikacja między reverse proxy a aplikacją może pozostać w protokole HTTP. Jednak dla maksymalnego bezpieczeństwa w wrażliwych środowiskach możesz chcieć włączyć HTTPS również na Kestrel, aby zaszyfrować całą ścieżkę komunikacji.

## 🔑 Zarządzanie sekretami na produkcji {#secrets-management-for-production}

Umieszczanie wrażliwych danych, takich jak hasła do bazy danych, dane uwierzytelniające administratora i sekrety klienta OAuth, bezpośrednio w `docker-compose.yml` stanowi ryzyko bezpieczeństwa, zwłaszcza gdy plik zostaje dodany do kontroli wersji. Zalecanym podejściem jest wyprowadzenie tych wartości do pliku `.env`, który Docker Compose ładuje automatycznie.

### 📄 Plik `.env` {#the-env-file}

Utwórz plik `.env` w tym samym katalogu co `docker-compose.yml` o następującej zawartości:

```env
# Database
POSTGRES_PASSWORD=YourSecureDBPassword123!

# Admin Account
AUTH_USERNAME=admin
AUTH_PASSWORD=YourSecureAdminPassword456!

# OIDC / OAuth Secrets
OAUTH_CLIENT_SECRET=YourOAuthClientSecret

# Credential Encryption at Rest (generate with: openssl rand -base64 32)
CREDENTIAL_ENCRYPTION_KEY=YourBase64Encoded32ByteKey

# Kestrel HTTPS (optional)
KESTREL_CERT_PASSWORD=YourCertPassword
```

**Ważne**: Docker Compose automatycznie odczytuje plik `.env` z tego samego katalogu — **nie** musisz odwoływać się do niego ręcznie. Wszystkie zmienne zdefiniowane w `.env` są dostępne w `docker-compose.yml` za pomocą składni `${VARIABLE_NAME}`.

### 🐳 Dostosowany `docker-compose.yml` {#adapted-docker-composeyml}

`docker-compose.yml` z kroków instalacji należy dostosować tak, aby zamiast zaszytych na sztywno sekretów używać placeholderów:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=${POSTGRES_PASSWORD}

      # Authentication Settings
      - Authentication__Username=${AUTH_USERNAME}
      - Authentication__Password=${AUTH_PASSWORD}
      # ... other settings remain unchanged ...

      # OIDC Configuration
      - OAuth__ClientSecret=${OAUTH_CLIENT_SECRET}

      # Credential Encryption at Rest
      - Security__CredentialEncryptionKey=${CREDENTIAL_ENCRYPTION_KEY}
    # ... ports, volumes, networks ...

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    # ... volumes, networks ...
```

### 📋 Szablon `.env.example` dla administratorów {#envexample-template-for-administrators}

Utwórz plik `.env.example` w swoim repozytorium, który będzie służył jako szablon dla innych administratorów. Zawiera on wartości placeholderów oraz dokumentację, ale **nie zawiera prawdziwych sekretów**:

```env
# ============================================
# Mail Archiver - Environment Configuration
# ============================================
# Copy this file to .env and fill in your values.
# Never commit the actual .env file to version control!

# --- PostgreSQL ---
POSTGRES_PASSWORD=change_me_db_password

# --- Admin Account ---
AUTH_USERNAME=admin
AUTH_PASSWORD=change_me_admin_password

# --- OIDC / OAuth ---
OAUTH_CLIENT_SECRET=change_me_client_secret

# --- Credential Encryption at Rest ---
# Generate with: openssl rand -base64 32
CREDENTIAL_ENCRYPTION_KEY=change_me_base64_32_byte_key

#...and so on
```

### ✅ Najlepsze praktyki {#best-practices}

- **Używaj `.env.example` jako szablonu**
- **Ustaw rygorystyczne uprawnienia do plików** — Ogranicz dostęp do pliku `.env`:
  ```bash
  chmod 600 .env
  ```
- **Używaj silnych haseł** — Generuj długie, losowe hasła (co najmniej 16 znaków) zawierające mieszankę liter, cyfr i znaków specjalnych.
- **Ogranicz dostęp do pliku `.env`** — Tylko użytkownik uruchamiający Docker Compose powinien mieć uprawnienia do odczytu pliku `.env`.

## 🔒 Uwagi dotyczące bezpieczeństwa {#security-notes}

- Używaj silnych haseł i zmieniaj domyślne dane uwierzytelniające. Hasła powinny mieć co najmniej 12 znaków i zawierać mieszankę wielkich i małych liter, cyfr oraz znaków specjalnych. Unikaj używania powszechnych słów lub łatwych do odgadnięcia informacji.
- Rozważ wdrożenie HTTPS z reverse proxy w środowisku produkcyjnym
- Zaleca się regularne kopie zapasowe bazy danych PostgreSQL. Aby uzyskać szczegółowe procedury dotyczące operacji backup i restore, zobacz [Backup and Restore Guide](BackupRestore.md).
