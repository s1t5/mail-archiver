# 🛠️ Mail Archiver Setup Guide {#mail-archiver-setup-guide}

[← Terug naar de Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Deze gids biedt gedetailleerde instructies voor het instellen van de Mail Archiver applicatie met behulp van Docker Compose.

## 🛠️ Vereisten {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Installatiestappen {#installation-steps}

1. Installeer de vereisten op uw systeem.

2. Maak een `docker-compose.yml` bestand aan met de volgende inhoud:

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
      - RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds=300

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
      - ReleaseNotes__RequestTimeoutSeconds=10

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

3. Bewerk de databaseconfiguratie in de `docker-compose.yml` en stel een veilig wachtwoord in de `POSTGRES_PASSWORD` variabele en de `ConnectionString` in.

4. Als u authenticatie wilt gebruiken (wat sterk wordt aanbevolen), definieer dan een `Authentication__Username` en `Authentication__Password` die wordt gebruikt voor de admin gebruiker.

5. Pas de `TimeZone__DisplayTimeZoneId` omgevingsvariabele aan zodat deze overeenkomt met uw voorkeurstijdzone (standaard is "Etc/UCT"). U kunt elke IANA tijdzone-identificator gebruiken (bijv. "Europe/Berlin", "Asia/Tokyo").

6. Configureer optioneel de `Logging__LogLevel` omgevingsvariabelen om de details van de applicatielogs te regelen. Zie de sectie Logging Settings hieronder voor beschikbare opties.

7. Configureer een reverse proxy naar keuze met https en authenticatie om de toegang tot de applicatie te beveiligen. 

> ⚠️ **Let op**: De applicatie zelf biedt geen versleutelde toegang via https! Dit moet worden ingesteld via een reverse proxy!

8. Eerste start van de containers:
```bash
docker compose up -d
```

9. Containers herstarten:
```bash
docker compose restart
```

10. Toegang tot de applicatie

11. Log in met uw gedefinieerde gegevens en voeg uw eerste e-mail Account toe:
   - Navigeer naar de "Email Accounts" sectie
   - Klik op "New Account"
   - Voer uw servergegevens en inloggegevens in
   - Opslaan en starten met archiveren!
   - Als u wilt, kunt u andere gebruikers aanmaken en Accounts toewijzen.

## 📚 Uitleg van omgevingsvariabelen {#environment-variable-explanations}

### 🗄️ Databaseverbinding {#database-connection}
- `ConnectionStrings__DefaultConnection`: De connection string naar de PostgreSQL database. Pas de `Host`, `Database`, `Username` en `Password` waarden aan indien nodig.

### 🔐 Authenticatie-instellingen {#authentication-settings}
- `Authentication__Username`: De gebruikersnaam voor het admin Account.
- `Authentication__Password`: Het wachtwoord voor het admin Account.
- `Authentication__SessionTimeoutMinutes`: De sessietimeout in minuten. Dit is van toepassing op de niet-persistente login cookie; gebruikers die **Remember Me** aanvinken op het inlogformulier, krijgen in plaats daarvan een persistente cookie die 7 dagen geldig is. Zie [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: De naam van de authenticatie cookie.
- `Authentication__CookieSameSite`: Configureert het SameSite attribuut voor authenticatie, sessie en CSRF-bescherming cookies. Geldige waarden zijn:
  - `Strict` (standaard): Maximale beveiliging. Cookies worden alleen verzonden bij same-site verzoeken. Dit kan problemen veroorzaken bij het navigeren naar de applicatie vanaf externe links (bijv. het klikken op een link van een andere website), omdat de bestaande sessie niet wordt herkend.
  - `Lax`: Aanbevolen bij het gebruik van een reverse proxy. Cookies worden verzonden bij top-level navigaties en same-site verzoeken, waardoor gebruikers externe links naar de applicatie kunnen volgen terwijl de CSRF-bescherming voor POST-verzoeken behouden blijft.
  - `None`: Cookies worden bij alle verzoeken verzonden. Vereist HTTPS en het `Secure` attribuut. Gebruik dit alleen als u specifieke cross-site vereisten heeft en de beveiligingsimplicaties begrijpt.

### 🔌 REST API Instellingen {#rest-api-settings}

De optionele read-only REST API is **standaard uitgeschakeld**. Raadpleeg de [REST API gids](API.md) voor de volledige referentie.
- `Api__Enabled`: Master switch voor de read-only REST API (standaard `false`). Wanneer `false`, retourneren alle `/api/*` routes `404`.
- `Api__AllowAttachmentDownloads`: Sta het downloaden van bijlage bytes toe via de API (standaard `true`). Wanneer `false`, retourneert het bijlage-endpoint `403`.
- `Api__AllowMessageDownloads`: Sta het downloaden van volledige berichten toe via de API, zowel de herbouwde `/eml` als het originele `/raw` (standaard `true`). Wanneer `false`, retourneren beide endpoints `403`. Een volledig bericht bevat zijn bijlagen, dus **zowel** deze switch als `Api__AllowAttachmentDownloads` moeten `true` zijn; het uitschakelen van een van beide schakelt beide endpoints uit.
- `Api__EnableSwaggerUi`: Stel Swagger UI bloot op `/apidocs` en het OpenAPI document op `/apidocs/spec/v1.json` (standaard `true`). Beide vereisen een ingelogde browsersessie.
- `Api__DefaultPageSize`: Standaard paginagrootte voor lijst-endpoints wanneer niet gespecificeerd (standaard `20`).
- `Api__MaxPageSize`: Maximale toegestane paginagrootte; grotere verzoeken worden beperkt (standaard `100`).
- `Api__RateLimitPerMinute`: Fixed-window request budget per API-sleutel per minuut (standaard `120`).

### 🤖 MCP Server Instellingen {#mcp-server-settings}

De optionele MCP (Model Context Protocol) server biedt AI-agenten via de Streamable HTTP-transport dezelfde alleen-lezen mailtoegang als de REST API. Het staat **standaard uit**. Raadpleeg de [MCP-gids](MCP.md) voor de volledige referentie. MCP hergebruikt exact dezelfde API-sleutels als de REST API (`ma_...` bearer tokens) en dezelfde per-gebruiker account-toegang scoping.
- `Mcp__Enabled`: Master switch voor de MCP server (standaard `false`). Wanneer `false`, geeft het `/mcp` endpoint `404` terug.
- `Mcp__AllowAttachmentDownloads`: Sta de `get_attachment` tool toe om bijlage bytes terug te geven (standaard `true`). Wanneer `false`, geeft de tool een foutmelding terug.
- `Mcp__DefaultPageSize`: Standaard paginagrootte gebruikt door `search_emails` wanneer de aanroeper `pageSize` weglaat (standaard `20`).
- `Mcp__MaxResults`: Bovengrens voor de paginagrootte van `search_emails`; grotere waarden worden afgekapt (standaard `100`).
- `Mcp__MaxAttachmentBytes`: Maximale bijlage grootte (in bytes) die de `get_attachment` tool inline als base64 zal teruggeven; grotere bijlagen worden geweigerd (standaard `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Sta de `get_email_eml` en `get_email_raw` tools toe om volledige berichten terug te geven (standaard `true`). Wanneer `false`, geven de tools een foutmelding terug. Een volledig bericht bevat zijn bijlagen, dus **zowel** deze switch als `Mcp__AllowAttachmentDownloads` moeten `true` zijn; het uitschakelen van een van beide schakelt beide tools uit.
- `Mcp__MaxMessageBytes`: Maximale bericht grootte (in bytes) die de `get_email_eml` en `get_email_raw` tools inline zullen teruggeven; grotere berichten worden geweigerd (standaard `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Fixed-window request budget per API-sleutel per minuut (standaard `120`).

### 📨 MailSync Instellingen {#mailsync-settings}
- `MailSync__IntervalMinutes`: Het interval in minuten tussen e-mail synchronisatie. Dit is de globale standaard; elk account kan dit individueel overschrijven vanaf de Create/Edit pagina (leeg laten om deze standaard te gebruiken).
- `MailSync__FullSyncIntervalHours`: Optionele globale standaard voor automatische volledige resyncs, in uren. Wanneer niet ingesteld (de standaard), wordt er geen automatische volledige sync uitgevoerd, tenzij een per-account `FullSyncIntervalHours` waarde is ingesteld op de Create/Edit pagina. Per-account waarden overschrijven deze globale standaard.
- `MailSync__TimeoutMinutes`: Per-account sync timeout in minuten. Een sync die langer duurt, stopt bij de volgende berichtgrens, behoudt zijn checkpoints en hervat bij de volgende run; de taak wordt gerapporteerd als `Timed Out` en `LastSync` wordt niet geavanceerd. Geldt alleen voor geplande syncs; een sync die gestart wordt vanuit de UI (handmatige sync of volledige resync) draait zonder timeout. `0` (de standaard) of elke niet-positieve waarde betekent geen timeout. Controleer de waarde voordat u upgradet als u grote postboxen synchroniseert. Zie [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: De verbindingstimesout voor IMAP-verbindingen in seconden.
- `MailSync__CommandTimeoutSeconds`: De command timeout voor IMAP-commando's in seconden.
- `MailSync__AlwaysForceFullSync`: Of er altijd een volledige sync moet worden afgedwongen (true/false).
- `MailSync__IgnoreSelfSignedCert`: Of zelfondertekende certificaten moeten worden genegeerd (true/false).
- `MailSync__MaxConcurrentSyncs`: Hoeveel account syncs tegelijkertijd mogen draaien. Standaard `1` (sequentieel, backwards-compatible). De scheduler vult slots aan zodra ze vrijkomen in plaats van te wachten op een hele batch, zodat één trage postbus niet langer de anderen ophoudt. Verhoog dit om meerdere accounts gelijktijdig te synchroniseren — houd rekening met de rate limits van de provider en het lokale resourcegebruik. Zie [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Optionele stagger delay in seconden toegepast aan het einde van elke account sync taak. Standaard `0` (geen delay). Nuttig om burst-starts te voorkomen wanneer `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Hoeveel problemen van elk type een sync taak onthoudt voor de accountpagina. Mislukte mappen, ontbrekende mappen en mislukte berichten worden apart begroot, zodat een vloedgolf van één type de andere niet uit het zicht kan duwen; alles voorbij het budget wordt geteld in plaats van bewaard. Standaard `20`. `0` schakelt de details uit en laat alleen de tellers over. Zie [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Mappen uitgesloten van synchronisatie voor **elk** account, bovenop de eigen uitgesloten-mappen lijst van elk account. Leeg per standaard, dus bestaande setups worden niet beïnvloed. De twee lijsten zijn **additief** — een map wordt overgeslagen wanneer deze aan één van beide voldoet — en beide gebruiken dezelfde matching regels: exacte match op het volledige pad, exacte match op de mapnaam, een pad-suffix match (dus `Drafts` matcht ook `INBOX/Drafts` en `INBOX.Drafts`), en alles onder de map die een entry benoemt wanneer `MailSync__ExcludeSubfolders` is ingeschakeld. Matching is niet hoofdlettergevoelig. Nuttig bij het importeren van veel postboxen van dezelfde server, waarbij het alternatief is om een identieke uitsluitingslijst op elk account te onderhouden. Geen enkele map is standaard uitgesloten; welke namen het waard zijn om te vermelden hangt af van de server en de taal ervan. Voorbeeld voor een postbus boom die ook kalender- en contactmappen bevat:
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
- `MailSync__ExcludeSubfolders`: Of een uitsluitingsentry ook de mappen onder de benoemde map beslaat. Geldt zowel voor de per-account lijst als voor de installatie-brede lijst, en voor beide providers. Standaard `false`, wat overeenkomt met het gedrag voordat de optie bestond: een entry `Deleted Items` matcht alleen die map, en een boom wordt uit het Archief gehouden door de root en elke map daaronder te benoemen. Stel `true` in om een entry de boom daaronder ook te laten omvatten — een entry `Deleted Items` houdt dan ook `Deleted Items/2024` en de rest van die tak uit het Archief. De regel verankert op de padscheider, dus het neemt hele mappen en nooit een deel van een naam: `Archive` beslaat `Archive` en `Archive/Team`, en laat een map genaamd `Team Archive Review` met rust. Een `.` telt ook als een scheider, op dezelfde manier als de pad-suffix regel deze behandelt.

### 📧 Instellingen voor opslag van originele berichten {#original-message-storage-settings}

Standaard bewaart het Archief alleen de geparseerde onderdelen van een e-mail, waardoor een geëxporteerde `.eml` een reconstructie is: transfer encodings, MIME boundaries en header folding verschillen van het ontvangen bericht, en DKIM-handtekeningen zijn niet langer geldig. Met `RawMessages:Enabled` wordt ook het volledige origineel opgeslagen.

| Parameter | Standaard | Doel |
|-----------|-----------|------|
| `RawMessages__Enabled` | `false` | Sla het origineel op van elke nieuw gearchiveerde e-mail. |
| `RawMessages__Storage` | `postgres` | Waar de bytes worden opgeslagen: `postgres` of `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Grotere berichten worden zonder hun origineel gearchiveerd. `0` = geen limiet. |
| `RawMessages__IncludeGraph` | `true` | Sla ook originelen op voor Microsoft 365 (Graph) accounts. |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Directory voor de berichtbestanden. Moet wijzen naar een gemount volume in Docker. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Directory-niveaus waaroover de e-mail-id is verspreid. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Verifieer de SHA-256 bij elke download. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Verwijder berichtbestanden waarnaar geen enkele e-mail verwijst. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Hoe vaak die sweep wordt uitgevoerd. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Bestanden die jonger zijn dan dit worden nooit verwijderd. |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Vertraging vóór de eerste sweep na de start, zodat een import die nog aan het schrijven is niet wordt opgehaald. |

!!! warning "Filesystem backend heeft een gemount volume nodig"
    Met `RawMessages__Storage=filesystem` bevinden de berichtbestanden zich buiten de database.
    Zonder een bind mount bevinden ze zich in de writable layer van de container en gaan ze verloren bij de
    volgende container-recreatie, terwijl de database rijen wel blijven bestaan. Maak een Back-up van de directory
    samen met de database dump.

Zie **[Original Message Storage](RawMessages.md)** voor de volledige beschrijving: waar het
origineel wordt geserveerd, de opslag backends, hoe u bestaande berichten tussen deze verplaatst met
`--migrate-raw-storage`, en wat u moet back-uppen.

### 📈 Monitoring Settings {#monitoring-settings}

| Parameter | Standaard | Doel |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | Ingeschakelde accounts ouder dan dit maken `/api/v1/health` rapport `Degraded`. `0` schakelt de controle uit. |
| `Metrics__Enabled` | `false` | Schakel de Prometheus metrics endpoint en de refresh loop in. |
| `Metrics__RefreshMinutes` | `5` | Hoe vaak de metrics snapshot wordt opgebouwd. Waarden onder 1 worden verhoogd naar 1. |

De health en metrics endpoints vereisen een admin API key en `Api__Enabled=true`. Raadpleeg
**[Health and Metrics](Monitoring.md)** voor de endpoints, metric namen, Prometheus
configuratie en de Docker health check.

### 📤 BatchRestore Instellingen {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Het aantal e-mails dat asynchrone verwerking triggert.
- `BatchRestore__MaxSyncEmails`: Het maximale aantal e-mails voor synchrone verwerking.
- `BatchRestore__MaxAsyncEmails`: Het maximale aantal e-mails voor asynchrone verwerking.
- `BatchRestore__SessionTimeoutMinutes`: De sessietimeout voor batch herstel in minuten.
- `BatchRestore__DefaultBatchSize`: De standaard batchgrootte voor e-mailoperaties.

### 🏢 Instellingen voor Tenantbeheer {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Maximaal aantal Postbussen dat in één Tenantbeheer-operatie kan worden toegevoegd. Standaard is `1000`. Verhoog dit voor zeer grote tenants, of verlaag het om onbedoelde massale imports te voorkomen. Wanneer het limiet wordt overschreden, wordt de operatie geweigerd met een validatiefout en worden er geen Accounts aangemaakt. Raadpleeg de [M365 Tenant Import Guide](M365TenantImport.md) voor details.

### 🐄 Mailcow Instellingen {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Maximum aantal Postbussen dat in een enkele Mailcow-activatierun kan worden aangemaakt. Standaard is `1000`. Verlaag dit om accidentele massale provisioning te voorkomen, of verhoog het voor grote instanties. Wanneer het limiet wordt overschreden, worden er geen Accounts aangemaakt. Zie [Mailcow Instances](Mailcow.md) voor details.
- Mailcow-integratie vereist daarnaast `Security__CredentialEncryptionKey` (zie [Security Settings](#-security-settings)). Het verbinden van een instantie en het activeren van Postbussen worden beide geweigerd zolang de versleuteling van de inloggegevens is uitgeschakeld, zodat de API-sleutel en gegenereerde app-wachtwoorden nooit als platte tekst kunnen worden opgeslagen.
- Uitgaande toegang is vereist tot de Mailcow HTTPS API en tot de IMAP-host via TLS (normaal gesproken poort `993`).

### 📦 BatchOperation Instellingen {#batchoperation-settings}
- `BatchOperation__BatchSize`: De batchgrootte voor e-mailoperaties.
- `BatchOperation__PauseBetweenEmailsMs`: De pauze tussen individuele e-mails in milliseconden.
- `BatchOperation__PauseBetweenBatchesMs`: De pauze tussen batches in milliseconden.

### 📤 Offload-instellingen {#offload-settings}

Instellingen voor de [date-windowed offload](Offload.md). Alle standaardwaarden reproduceren het gedrag dat de applicatie had voordat de functie bestond, dus een installatie die deze sectie niet configureert, wordt niet beïnvloed.
- `Offload__MaxConcurrentJobs`: Hoeveel herstel- of offload-taken tegelijkertijd mogen draaien. De standaardwaarde is `1`, wat de strikt seriële verwerking behoudt die de taakwachtrij altijd heeft gebruikt. Ongeacht deze waarde wordt er maximaal één taak per doel-Postbus uitgevoerd.
- `Offload__PrefetchMaxMessages`: Bovengrens voor het aantal berichten dat wordt geïndexeerd vanuit een doel-Postbus voor deduplicatie. De standaardwaarde is `500000`. Daarboven wordt de controle beperkt tot een enkele Map en wordt er gelogd dat de omvang is verkleind.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Bronmappen die nooit worden geoffload, gematcht vóór het hernoemen en inclusief submappen. Standaard leeg.
- `Offload__FolderRenameMap__<SourceFolder>`: Overschrijft de voorliggende segmenten van een bronmappad, bijvoorbeeld `Offload__FolderRenameMap__Sent Items=Sent`. Standaard leeg.
- `Offload__MarkAsSeen`: Of toegevoegde e-mail als gelezen wordt gemarkeerd. De standaardwaarde is `true`, wat overeenkomt met het bestaande herstelgedrag.

Beide Map-instellingen zijn opzettelijk leeg geleverd: het herschrijven of verwijderen van mappen zonder dat daarom is gevraagd, zou iedereen die al de herstelroute gebruikt verrassen. Een uitvoering zonder configuratie migreert daarom alles, inclusief spammappen, en maakt een tweede set speciale mappen aan naast de eigen mappen van het doel.

### 📊 Bandwidth Tracking Settings {#bandwidth-tracking-settings}

- `BandwidthTracking__Enabled`: Schakel bandbreedte-tracking in of uit voor IMAP rate limit afhandeling (true/false). Standaard is `false`. Wanneer ingeschakeld, houdt het systeem het bandbreedteverbruik per Account bij en kan het de Synchronisatie pauzeren wanneer de limieten van de provider zijn bereikt. Zie [Rate Limit Handling](RateLimitHandling.md) voor gedetailleerde informatie.
- `BandwidthTracking__DailyLimitMb`: Dagelijkse downloadlimiet in megabytes per Account. Standaard is `25000` (25 GB). Stel dit voor providers met bandbreedtelimieten in om overeen te komen met hun rate limit (bijv. `2500` voor providers met ~2500 MB dagelijkse limieten). Het systeem pauzeert de synchronisatie wanneer deze limiet is bereikt.
- `BandwidthTracking__WarningThresholdPercent`: Percentage van de dagelijkse limiet waarbij waarschuwingsberichten worden gelogd. Standaard is `80`. Wanneer het bandbreedteverbruik dit percentage bereikt, worden waarschuwingen gelogd om te helpen bij het monitoren van naderende limieten.
- `BandwidthTracking__PauseHoursOnLimit`: Aantal uren om de Synchronisatie te pauzeren wanneer de dagelijkse limiet is bereikt. Standaard is `24`. Na deze periode wordt de limietvlag automatisch gewist en wordt de synchronisatie hervat.
- `BandwidthTracking__TrackUploadBytes`: Of ook de uploadbandbreedte moet worden bijgehouden (true/false). Standaard is `false`. De meeste IMAP-providers beperken alleen downloads, dus dit is doorgaans niet nodig.

### 🎯 Selectie-instellingen {#selection-settings}
- `Selection__MaxSelectableEmails`: Het maximale aantal e-mails dat tegelijkertijd kan worden geselecteerd.

### 👁️ Weergave-instellingen (Privacy & Weergave) {#view-settings-privacy-display}

- `View__DefaultToPlainText`: Regelt de standaard e-mailweergavemodus voor privacy en het voorkomen van tracking (true/false). Standaard is `false`.
  - Wanneer ingesteld op `true`: E-mails worden standaard geopend in tekstweergave (plain-text), waardoor het automatisch laden van tracking pixels, externe afbeeldingen en web beacons wordt voorkomen. Dit wordt aanbevolen voor gebruikers die bezorgd zijn over e-mailtracking en privacy.
  - Wanneer ingesteld op `false`: E-mails worden standaard geopend in HTML-weergave, waarbij de geformatteerde e-mail met afbeeldingen en styling wordt getoond.
  - Gebruikers kunnen schakelen tussen HTML- en tekstweergave met behulp van een knop op de pagina met e-maildetails (alleen zichtbaar wanneer beide formaten beschikbaar zijn).
  - De link "Full View" respecteert de momenteel geselecteerde weergavemodus.

- `View__BlockExternalResources`: Blokkeert externe bronnen (remote afbeeldingen, externe CSS, externe scripts, webfonts, etc.) in HTML-e-mailweergaven om tracking te voorkomen en privacy te verbeteren (true/false). Standaard is `false`.
  - Wanneer ingesteld op `true`: Externe bronnen worden gefilterd bij het weergeven van HTML-e-mails. Alleen inline-inhoud en data URIs (inclusief inline bijlagen via `cid:`-verwijzingen) worden weergegeven.
  - Wanneer ingesteld op `false`: HTML-e-mails worden weergegeven met al hun originele externe bronnen.
  - **Belangrijk**: Deze instelling heeft alleen invloed op de **weergave** van e-mails. Gearchiveerde e-mails worden volledig ongewijzigd in de database opgeslagen, waarbij alle originele inhoud behouden blijft.
  - Geblokkeerde bronnen omvatten:
    - Remote afbeeldingen (tracking pixels, externe afbeeldingen die op servers worden gehost)
    - Externe CSS-stylesheets
    - Externe fonts via @font-face
    - Externe CSS-imports via @import
    - Externe achtergrondafbeeldingen
  - Toegestane bronnen:
    - Inline afbeeldingen ingebed als data: URIs
    - Inline bijlagen die worden aangeroepen via cid: URIs
    - Inline CSS-stijlen en style tags
  - Deze instelling werkt onafhankelijk van `DefaultToPlainText` en biedt een extra laag privacybescherming bij het bekijken van HTML-e-mails.

### 📊 Dashboard Instellingen {#dashboard-settings}

- `Dashboard__ShowDirectionSplits`: Of de tellerkaarten hun inkomende en uitgaande delen bevatten, en de Account kaart het aantal afzonderlijke domeinen. Standaard is `true`. Stel in op `false` en die getallen worden niet berekend maar verborgen: de tellers lezen een gewone telling, en de Bijlage telling stopt met het koppelen aan de mail waaraan deze hangt, wat de enige query is die deze delen toevoegen.
- `Dashboard__SelectablePeriods`: Of de dashboardgrafieken een resolutie, een periode en pijlen bieden om deze te verplaatsen. Standaard is `true`. Stel in op `false` en de grafieken tonen de laatste twaalf maanden per maand en de verzenders van het hele Archief, het chart endpoint antwoordt alsof het niet bestaat, en de oudste verzenddatum wordt niet opgezocht.
- `Dashboard__CacheSeconds`: Hoe lang berekende dashboardstatistieken (totalen met hun inkomende en uitgaande splits, per-Account tellingen, de chart series, top verzenders, recente e-mails, database grootte) in de in-memory cache van de server worden bewaard. Standaard is `60` seconden. Stel in op `0` om caching uit te schakelen en de statistieken altijd opnieuw te berekenen. Elke grafiekselectie van resolutie en periode wordt afzonderlijk gecached, dus een dashboard dat tussen selecties wisselt, bevat één vermelding per selectie. Hogere waarden verminderen de databasebelasting in grote omgevingen ten koste van meer verouderde getallen. Sync status badges, opslagwaarden en de recente-e-mails lijst worden altijd live opgehaald en worden niet beïnvloed door deze cache.
- `Dashboard__RefreshIntervalMinutes`: Hoe vaak de `DashboardStatsRefreshService` achtergrondservice de dure dashboard aggregaties (totale tellingen met hun inkomende en uitgaande splits, per-Account tellingen, het aantal Account domeinen, de standaard chart series met zijn top verzenders, database grootte) herberekenen in de `DashboardStatsCache` database tabel. Bij installaties met enkele miljoenen mails leest het dashboard deze voorbereide waarden in plaats van het hele Archief te aggregeren in het verzoekspad, wat een eerste laadtijd van ~10 seconden omzet naar milliseconden. Het chart endpoint beantwoordt de standaard selectie (maanden over één jaar, inkomende verzenders, de huidige periode) vanuit dezelfde rij, zodat het terugkeren naar de selectie de getallen van de eerste paint toont; elke andere selectie wordt op aanvraag berekend en bijgehouden in de in-memory cache hierboven. Standaard is `15` minuten. Stel in op `0` om de achtergrondverversing uit te schakelen; het dashboard berekent dan de statistieken weer live (waarbij de in-memory cache hierboven nog steeds van toepassing is). De getoonde waarden kunnen tot het geconfigureerde interval achterlopen op de werkelijke Archief status. Een rij die is berekend met andere `ShowDirectionSplits` of `SelectablePeriods` waarden dan de huidige, wordt niet gebruikt; het dashboard berekent live tot de volgende verversing.

### 🔄 Jobs Instellingen {#jobs-settings}
- `Jobs__RefreshSeconds`: Hoe vaak de achtergrondtakenpagina zichzelf ververst terwijl een browsertabblad deze open heeft staan. De standaardwaarde is `30` seconden. Stel `0` in om de automatische verversing uit te schakelen en handmatig te verversen. Elke verversing bouwt de pagina opnieuw op vanuit alle acht de jobbronnen, dus bij installaties met veel accounts houdt een langer interval de belasting laag, vermenigvuldigd met elk tabblad dat op de pagina open staat.

### 🗃️ Npgsql Instellingen {#npgsql-settings}
- `Npgsql__CommandTimeout`: De timeout voor databasecommando's in seconden.

### 📥 Uploadinstellingen {#upload-settings}
- `Upload__MaxFileSizeGB`: De maximale bestandsgrootte voor uploads in GB.
- `Upload__KeepAliveTimeoutHours`: De keep alive timeout voor uploads in uren.
- `Upload__RequestHeadersTimeoutHours`: De timeout voor request headers in uren.

### 📂 Lokale Importinstellingen {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, etc.: Whitelist van lokale mappen waaruit de CLI import-opdrachten (`--import-mbox`, `--import-eml`) bestanden mogen lezen. Elke vermelding is een pad binnen de container. U moet uw importbestanden in een van deze mappen mounten met behulp van Docker volumes. Standaard: `/data/import` (automatisch ingesteld op `/app/uploads` als fallback).
  - Bij het gebruik van `docker exec` om de importopdracht uit te voeren, moet het via `--file` opgegeven bestandspad binnen een van deze toegestane paden vallen.
  - Deze beveiligingsmaatregel voorkomt willekeurige toegang tot het bestandssysteem vanuit CLI-opdrachten.
  - Er kunnen meerdere paden worden geconfigureerd voor verschillende importbronnen.
  - Raadpleeg [CLI commands](CLI.md) voor gedetailleerde instructies voor gebruik.

### 📄 CSV Import Instellingen {#csv-import-settings}
- `CsvImport__MaxRows`: Maximaal aantal CSV-rijen (Postbussen) dat in één bulkimport wordt verwerkt. De standaardwaarde is `5000`. Verhoog deze waarde voor grote implementaties; verlaag deze om de impact van een enkele importrun op de databasebelasting te beperken.
- `CsvImport__MaxFileSizeBytes`: Maximale toegestane grootte (in bytes) van het geüploade CSV-bestand. De standaardwaarde is `10000000` (10 MB). Pas deze waarde aan om overeen te komen met uw uploadlimieten indien nodig.
- Raadpleeg de [Account Import Gids](Account-Import.md) voor gedetailleerde instructies over bulk IMAP accountimport via CSV.

### 📤 Audit Export Instellingen {#audit-export-settings}
Instellingen voor de exportpagina van de auditgegevens (alleen voor admin, bereikbaar vanaf de Logs pagina). De export genereert een ZIP-pakket met tabelvormige massadata (INDEX.XML + CSV-tabellen + DTD) uit het bestaande Archief voor externe audittools. Raadpleeg de [Audit Data Export Guide](AuditExport.md) voor details over het gebruik.
- `AuditExport__DataSupplierName`: Standaardwaarde voor het formulierveld "Data supplier name" dat uw organisatie identificeert in het geëxporteerde indexbestand. Standaard: leeg.
- `AuditExport__DataSupplierLocation`: Standaardwaarde voor het formulierveld "Data supplier location" (bijv. zetel van het bedrijf). Standaard: leeg.
- `AuditExport__Comment`: Standaardwaarde voor het formulierveld "Comment", opgenomen als vrije tekst in het geëxporteerde indexbestand. Standaard: leeg.
- `AuditExport__OutputDirectory`: Directory waar de gegenereerde ZIP-bestanden worden opgeslagen. Relatieve paden worden opgelost ten opzichte van de app content root. Standaard: `exports/audit`.
- `AuditExport__RetentionDays`: Aantal dagen waarna voltooide exportbestanden worden verwijderd door de dagelijkse opschoning. Standaard: `30`.
- `AuditExport__MaxRangeYears`: Maximale toegestane periode tussen de start- en einddatum van de exportperiode. Standaard: `10`.
- De formuliervelden worden vooraf ingevuld met deze standaardwaarden, maar kunnen per export worden bewerkt.
- Elke export schrijft een startvermelding en een resultaatvermelding naar het access log (type "Audit Data Export"), zodat de geschiedenis revisiebestendig is zonder wijziging in het DB-schema.

### 🔒 Instellingen voor Verwijderingsbeleid {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Bepaalt of handmatige verwijdering van gearchiveerde e-mails is toegestaan (true/false). Standaard is `true`. Wanneer ingesteld op `false`:
  - Alle gearchiveerde e-mails worden bij het opstarten vergrendeld (`IsLocked = true`) via de database compliance trigger, wat elke wijziging of verwijdering op databaseniveau voorkomt.
  - Handmatige verwijdering (individueel en in bulk) wordt geblokkeerd op applicatieniveau met een foutmelding.
  - De kolomstandaardwaarde wordt aangepast zodat ook nieuw geïmporteerde e-mails worden vergrendeld.
  - De huidige status van het beleid wordt bij elke opstarting gelogd in de AccessLogs tabel (zichtbaar op de Logs pagina als "Deletion Policy" vermeldingen) voor controleerbaarheid.
  - Lokale Bewaring verwijdering is vrijgesteld: e-mails die onder een geconfigureerde Bewaring periode vallen, worden nog steeds verwijderd (deze worden onmiddellijk vóór verwijdering binnen het bewaringstraject ontgrendeld).
- `DeletionPolicy__MaxBulkDeleteEmails`: Bovengrens voor één filtergebaseerde bulkverwijdering (*Delete all matches*). Standaard is `100000`. Een filter die meer e-mails vindt, wordt afgewezen met een bericht waarin wordt gevraagd de zoekopdracht te verfijnen; de taak wordt nooit stilzwijgend afgekort. E-mails die vergrendeld zijn door het bewaring beleid worden overgeslagen en gerapporteerd in de taakstatus, niet ontgrendeld. De taak controleert deze limiet en `DeletionPolicy__DeletionAllowed` opnieuw wanneer deze start, zodat een taak die in de wachtrij stond vóór een beleidswijziging stopt in plaats van te draaien met de oude instelling.
- **Onveranderlijkheidsbescherming:** Wanneer `IsLocked = true`, blokkeert de database compliance trigger (`prevent_locked_email_changes`) ELKE wijziging aan de e-mailrij — alle kolommen zijn beschermd, niet alleen een vaste lijst met velden. De enige vrijgestelde kolommen zijn `IsLocked` zelf (zodat ontgrendelen voor bewaring verwijdering en het toepassen van het beleid bij opstarten mogelijk blijft) en `FolderName` (zodat IMAP synchronisatie de Map kan bijwerken wanneer een e-mail aan de serverzijde wordt verplaatst). De bescherming is kolom-agnostisch (gebaseerd op JSONB-vergelijking) en dekt automatisch toekomstige schema-toevoegingen af.

### 🕐 Tijdzone-instellingen {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: De tijdzone die wordt gebruikt voor het weergeven van e-mail tijdstempels in de UI. Gebruikt IANA tijdzone-identificatoren (bijv. "Europe/Berlin", "Asia/Tokyo"). De standaardwaarde is "Etc/UCT" voor backward compatibility. Bij het importeren van e-mails worden de tijdstempels naar deze tijdzone omgezet voor weergave doeleinden.

### 🎉 ReleaseNotes Settings (Versie Update Splash Screen) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: De versiestring die deze instantie rapporteert. Deze wordt vergeleken met GitHub release tags en bepaalt of er een nieuwere release bestaat. Wanneer dit leeg is, valt de changelog terug op de versie van de gecompileerde assembly en blijft de update hint uitgeschakeld.
- `ReleaseNotes__Enabled`: Schakel de versie update splash screen in of uit (true/false). Standaard is `true`. Wanneer ingeschakeld, zien beheerders na een applicatie-update een eenmalige changelog modal, die de release notes toont die van GitHub Releases zijn opgehaald voor de huidige versie. Elke beheerder kan de modal sluiten, en deze verschijnt alleen opnieuw voor een nieuwe versie. Stel in op `false` om deze functie volledig uit te schakelen, inclusief de update hint.
- `ReleaseNotes__UpdateHintEnabled`: Toon beheerders een kleine toast in de rechterbenedenhoek wanneer GitHub een nieuwere stabiele release publiceert dan de geïnstalleerde versie (true/false). Standaard is `true`. De toast linkt naar die release en wordt per versie in de browser gesloten. Concepten en prereleases worden genegeerd. Vereist `ReleaseNotes__AppVersion`; zonder dit wordt er geen hint getoond.
- `ReleaseNotes__UpdateCheckIntervalHours`: Hoe vaak de GitHub release lijst wordt ververst, in uren. Standaard is `6`. Een korter interval maakt een nieuwe release sneller zichtbaar in een langlopende instantie, een langer interval vermindert het aantal GitHub API-aanroepen. Minimum is `1`.
- `ReleaseNotes__RequestTimeoutSeconds`: Timeout voor één GitHub API-verzoek, in seconden. Standaard is `10`. Wanneer deze verloopt, blijven de changelog en de update hint voor die poging uitgeschakeld in plaats van het verzoek open te houden; de volgende controle is een nieuwe poging.

### 🔧 Database Maintenance Instellingen {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Schakel automatische dagelijkse database onderhoud in of uit (true/false). Standaard is `false`. Wanneer ingeschakeld, voert het systeem automatisch VACUUM ANALYZE-bewerkingen uit om de databaseprestaties te optimaliseren en bloat te voorkomen. Raadpleeg de [Database Maintenance Guide](DatabaseMaintenance.md) voor meer details.
- `DatabaseMaintenance__DailyExecutionTime`: Het tijdstip van de dag waarop database onderhoud moet worden uitgevoerd, in 24-uursformaat (HH:mm). Standaard is `02:00`. Kies een tijdstip tijdens lage systeemactiviteit.
- `DatabaseMaintenance__TimeoutMinutes`: Maximale tijd toegestaan voor onderhoudsbewerkingen in minuten. Standaard is `30`. Verhoog deze waarde voor grotere databases.

### 🧬 Bijlage Deduplicatie Instellingen {#attachment-deduplication-settings}

Bijlage Deduplicatie slaat elke unieke bijlage-payload slechts één keer op (content-addressed via SHA-256) en is een **kernfunctie die altijd is ingeschakeld** – er is opzettelijk geen aan/uit-schakelaar. Alleen de onderstaande batch/planning parameters kunnen worden aangepast. Raadpleeg de [Attachment Deduplication Guide](AttachmentDeduplication.md) voor volledige details.

- `AttachmentDeduplication__BatchSize`: Aantal bestaande bijlagen dat per transactie wordt gemigreerd tijdens de eenmalige achtergrondmigratie van bestaande gegevens. De standaardwaarde is `200`. Grotere waarden migreren sneller, maar gebruiken meer geheugen/DB-belasting per batch.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Optionele pauze (in milliseconden) tussen migratiebatches om de databasebelasting op drukke systemen te beperken. De standaardwaarde is `0` (geen pauze).
- `AttachmentDeduplication__StartupDelaySeconds`: Vertraging (in seconden) na het opstarten van de applicatie voordat de achtergrondmigratie begint, zodat de schema-migratie de tijd krijgt om te voltooien. De standaardwaarde is `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Interval (in uren) van de altijd actieve garbage collection die bijlage-payloads verwijdert die niet langer worden gerefereerd door een e-mail. De standaardwaarde is `12`. Dit draait onafhankelijk van `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Database command timeout (in seconden) voor de migratiebatch-operaties (INSERT met SHA-256 hashing en UPDATE). De standaardwaarde is `300` (5 minuten). Verhoog deze waarde voor zeer grote databases of bijlagen, of verlaag deze als u snellere foutdetectie wilt. Als een batch nog steeds een timeout krijgt, probeert de service het automatisch opnieuw met de helft van de batchgrootte.

### 💾 Account Opslaginstellingen {#account-storage-settings}

De opslagweergave per Account toont het database-opslaggebruik (alle mailvelden + Bijlage) voor elk Account in de Dashboard "Account Overview" tabel en de MailAccounts "Show All" tabel. Een autarke achtergrondservice (`AccountStorageRefreshService`) berekent de waarden door PostgreSQL `pg_column_size` per kolom op te tellen (waarbij alle velden van een mail worden gedekt zonder de mailbodies te lezen) en cachet deze in de `AccountStorageCache` tabel. Deze service draait onafhankelijk van `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Schakel de opslagverversingsservice in of uit (true/false). Standaard is `true`. Wanneer ingeschakeld, voert de service bij het opstarten een hervatbare backfill uit van openstaande Accounts (crash-veilig via `AccountStorageBackfillState`) en daarna een dagelijkse volledige verversing. Wanneer uitgeschakeld, worden opslagwaarden alleen bijgewerkt wanneer e-mails worden gesynchroniseerd, geïmporteerd of verwijderd, maar de weergegeven waarden kunnen na verloop van tijd verouderd raken.
- `AccountStorage__DailyExecutionTime`: Tijdstip van de dag (24-uurs formaat `HH:mm`) voor de dagelijkse volledige verversing van alle Accounts. Standaard is `02:30`. Kies een tijdstip tijdens lage systeemactiviteit.
- `AccountStorage__BackfillDelayMs`: Vertraging (in milliseconden) tussen Accounts tijdens de initiële backfill bij het opstarten. Standaard is `5000`. Lagere waarden versnellen de backfill maar verhogen de databasebelasting; verhoog deze waarde voor zeer grote Archief-bestanden om overbelasting van de database te voorkomen.
- `AccountStorage__RefreshBatchDelayMs`: Vertraging (in milliseconden) tussen Accounts tijdens de dagelijkse volledige verversing. Standaard is `1000`. Lagere waarden versnellen de verversing maar verhogen de databasebelasting; verhoog deze waarde voor zeer grote Archief-bestanden.
- `AccountStorage__CommandTimeoutSeconds`: Database command timeout (in seconden) voor de opslagberekening per Account (`pg_column_size` over alle rijen van een Account). Standaard is `300` (5 minuten). De berekening leest elke mailrij inclusief TOAST-data, wat bij zeer grote Archief-bestanden (250k+ e-mails) ruim een minuut kan duren; de vorige Npgsql-standaard van 30 seconden zorgde ervoor dat de verversing stilzwijgend mislukte en het dashboard verouderde waarden bleef tonen. Verhoog deze waarde als de berekening nog steeds een timeout geeft op uw Archief.

> 💡 **Opmerking**: Opslagwaarden worden onmiddellijk ververst na elke mail Synchronisatie, import of Bewaring-verwijdering, zodat de weergegeven waarden actueel blijven, zelfs zonder de dagelijkse verversing. De dagelijkse verversing is een vangnet voor uitzonderlijke gevallen (bijv. directe databasewijzigingen).

### 📝 Logging Instellingen {#logging-settings}
- `Logging__LogLevel__Default`: Het standaard logniveau voor de applicatie. Beschikbare niveaus zijn: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Standaard is `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Logniveau voor ASP.NET Core framework berichten. Standaard is `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Logniveau voor Entity Framework database commando's. Standaard is `Warning`.

### 🛡️ Beveiligingsinstellingen {#security-settings}
- `AllowedHosts`: Een met puntkomma gescheiden lijst van hostnamen die de applicatie mag bedienen. Dit helpt HTTP Host header-aanvallen te voorkomen. Voorbeeld: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Belangrijk**: Gebruik `*` niet in productieomgevingen, omdat dit de validatie van de host header uitschakelt.

#### 🔐 Versleuteling van credentials bij opslag {#credential-encryption-at-rest}
De credentials van het mail Account (IMAP-wachtwoord, M365 client secret en MSA OAuth-tokens) kunnen in de database worden versleuteld. De versleutelingssleutel wordt **buiten** de database bewaard (omgevingsvariabele / Docker secret), zodat een database dump alleen niet voldoende is om de credentials te lezen.

- `Security__CredentialEncryptionKey`: Base64-gecodeerde 256-bit (32 byte) sleutel voor versleuteling. Genereer er een met:
  ```bash
  openssl rand -base64 32
  ```
  Stel de waarde in als een omgevingsvariabele (aanbevolen: `Security__CredentialEncryptionKey` in uw compose-bestand of containeromgeving). De applicatie ondersteunt geen Docker secrets in `_FILE`-stijl — als u een Docker secret gebruikt, monteer deze dan en laad de inhoud ervan in de omgevingsvariabele in uw entrypoint. Commit de sleutel nooit naar `appsettings.json`. Wanneer een sleutel is geconfigureerd, worden credentials versleuteld bij het schrijven, en een achtergrondservice versleutelt bestaande tekstuele credentials eenmalig.
- `Security__EncryptAccountCredentials`: Schakel versleuteling van nieuwe/bijgewerkte credential-waarden in of uit (true/false). De standaardwaarde is `true`. Wanneer er geen sleutel is geconfigureerd, worden credentials als platte tekst opgeslagen en wordt er bij het opstarten een waarschuwing in het logboek geregistreerd — bestaande installaties worden niet beïnvloed totdat er een sleutel wordt opgegeven.
- `Security__CredentialEncryptionBackfill`: Of de achtergrondservice bestaande platte tekst credentials bij het opstarten moet versleutelen (true/false). De standaardwaarde is `true`. Geen actie zonder een geconfigureerde sleutel.
- `Security__CredentialEncryptionKeyPrevious`: Optionele vorige sleutel, wordt alleen gebruikt om waarden te ontsleutelen die ermee zijn versleuteld (voor sleutelrotatie). Nieuwe waarden worden altijd versleuteld met `Security__CredentialEncryptionKey`.

  > ⚠️ **Sleutelrotatie**: bewaar na rotatie `Security__CredentialEncryptionKeyPrevious` geconfigureerd **totdat elke credential opnieuw is geschreven** (bijv. door het Account te bewerken of een automatische OAuth-tokenverversing) — bestaande waarden die met de oude sleutel zijn versleuteld, worden *niet* automatisch opnieuw versleuteld met de nieuwe sleutel. Zodra u de vorige sleutel verwijdert, wordt elke credential die nog steeds ermee is versleuteld onleesbaar en moet deze opnieuw worden ingevoerd. Zolang deze is geconfigureerd, moet deze even veilig worden bewaard als de huidige sleutel.

> ⚠️ **Houd de sleutel veilig**: Zonder de sleutel kunnen versleutelde credentials niet worden hersteld en moeten ze opnieuw worden ingevoerd. Maak er een Back-up van samen met (maar apart van) uw database Back-ups.
>
> 💡 **Bestaande installaties**: Upgrade veilig — zolang er geen `Security__CredentialEncryptionKey` is ingesteld, verandert er niets. Stel de sleutel in wanneer u er klaar voor bent; bestaande platte tekst credentials blijven leesbaar en worden op de achtergrond versleuteld.

### 🔐 OIDC Configuratie {#oidc-configuration}

Voor gedetailleerde instructies voor de configuratie van OpenID Connect-authenticatie, zie [OIDC Implementation Guide](OIDC_Implementation.md).

#### Basis OIDC Instellingen {#basic-oidc-settings}
- `OAuth__Enabled`: OIDC-authenticatie in- of uitschakelen (true/false)
- `OAuth__Authority`: De OpenID Connect authority URL (bijv. https://sts.windows.net/{TENANT-ID}/ voor Azure AD)
- `OAuth__ClientId`: De client ID toegewezen door uw identiteitsprovider
- `OAuth__ClientSecret`: Het client secret toegewezen door uw identiteitsprovider
- `OAuth__DisplayName`: Optionele weergavenaam die wordt getoond op de OIDC-inlogknop en de auto-redirect pagina (bijv. `PocketID SSO`). Als dit wordt weggelaten, wordt het generieke label "Login with OAuth" gebruikt.
- `OAuth__ClientScopes__0`: Eerste scope gevraagd aan de identiteitsprovider (openid)
- `OAuth__ClientScopes__1`: Tweede scope gevraagd aan de identiteitsprovider (profile)
- `OAuth__ClientScopes__2`: Derde scope gevraagd aan de identiteitsprovider (email)
- `OAuth__ResponseMode`: Optionele response mode van de autorisatieaanvraag (`query`, `fragment` of `form_post`). Standaard is `form_post`, de ASP.NET Core standaard. Stel in op `query` voor providers die `form_post` niet ondersteunen, zoals Synology SSO Server. Zie [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Nieuwe OIDC-gebruikers automatisch goedkeuren zonder dat handmatige goedkeuring door een admin vereist is (true/false). Standaard is `false`.

#### Gebruikersprovisionering Instellingen {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Nieuwe OIDC-gebruikers automatisch goedkeuren zonder dat handmatige goedkeuring door een admin vereist is (true/false). Standaard is `false`. Wanneer ingeschakeld, worden gebruikers die authenticeren via de OIDC-provider onmiddellijk geactiveerd en kunnen zij toegang krijgen tot de applicatie. Wanneer uitgeschakeld (standaard), worden nieuwe OIDC-gebruikers als inactief aangemaakt en is handmatige activering door een administrator vereist. Zie [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users) voor gedetailleerde informatie.
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, etc.: E-mailadressen die automatisch als administrators geprovisioneerd moeten worden. Gebruikers met deze e-mailadressen worden bij de eerste OAuth-login aangemaakt als actieve admins, waarbij het normale goedkeuringsproces wordt overgeslagen. E-mailovereenkomst is niet hoofdlettergevoelig.

#### Wachtwoordloze Inloginstellingen {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Inloggen met gebruikersnaam/wachtwoord uitschakelen (true/false). Standaard is `false`. Wanneer ingeschakeld, wordt het wachtwoordformulier verborgen **en** wijst de server elke wachtwoord POST af, zodat alleen de OAuth-login beschikbaar is. Vereist `OAuth__Enabled=true`. Als OAuth is uitgeschakeld, heeft deze instelling geen effect.
- `OAuth__AutoRedirect`: Gebruikers automatisch doorsturen naar de OAuth-provider (true/false). Standaard is `false`. Vereist dat `OAuth__DisablePasswordLogin` op `true` staat. Gebruikers zien een kort laadscherm voordat ze worden doorverwezen.

#### Voorbeeld: Volledige OIDC-First Configuratie {#example-full-oidc-first-configuration}
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

## 🔐 Kestrel HTTPS Configuratie (Optioneel) {#kestrel-https-configuration-optional}

Hoewel de applicatie bedoeld is om via een reverse proxy met HTTPS te worden toegewezen, kunt u ook de Kestrel webserver configureren om SSL/TLS-certificaten te gebruiken. Dit biedt end-to-end encryptie tussen de reverse proxy en de applicatiecontainer.

### Configuratiestappen {#configuration-steps}

1. **Genereer of verkrijg een SSL-certificaat** in PFX-formaat (bijv. `localhost.pfx`)

2. **Voeg de volgende omgevingsvariabelen toe** aan uw `docker-compose.yml` voor de `mailarchive-app` service:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Werk de poortmapping bij** in de `mailarchive-app` service:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Voeg een volumemapping toe** voor het certificaat:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Plaats uw certificaatbestand** (bijv. `localhost.pfx`) in de `./certs` directory op uw hostsystem.

### Uitleg van Omgevingsvariabelen {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: HTTP endpoint URL (standaard: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: HTTPS endpoint URL (standaard: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Pad naar het PFX-certificaatbestand binnen de container
- `Kestrel__Endpoints__Https__Certificate__Password`: Wachtwoord voor het PFX-certificaatbestand

> 💡 **Opmerking**: Deze configuratie is optioneel. Als u een reverse proxy met HTTPS gebruikt (aanbevolen), kan de communicatie tussen de reverse proxy en de applicatie HTTP blijven. Echter, voor maximale beveiliging in gevoelige omgevingen wilt u wellicht ook HTTPS op Kestrel inschakelen om het volledige communicatiepad te versleutelen.

## 🔑 Secrets Management voor Productie {#secrets-management-for-production}

Het hardcoderen van gevoelige gegevens zoals database wachtwoorden, admin-gegevens en OAuth client secrets direct in `docker-compose.yml` is een beveiligingsrisico, vooral wanneer het bestand wordt gecontroleerd in versiebeheer. De aanbevolen aanpak is om deze waarden te externaliseren naar een `.env` bestand dat Docker Compose automatisch laadt.

### 📄 Het `.env` Bestand {#the-env-file}

Maak een `.env` bestand aan in dezelfde directory als uw `docker-compose.yml` met de volgende inhoud:

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

**Belangrijk**: Docker Compose leest automatisch het `.env` bestand uit dezelfde directory — u hoeft dit **niet** handmatig te refereren. Alle variabelen die zijn gedefinieerd in `.env` zijn beschikbaar in `docker-compose.yml` via de `${VARIABLE_NAME}` syntaxis.

### 🐳 Aangepaste `docker-compose.yml` {#adapted-docker-composeyml}

De `docker-compose.yml` uit de installatiestappen moet worden aangepast om placeholders te gebruiken in plaats van hardcoded secrets:

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

### 📋 `.env.example` Template voor Beheerders {#envexample-template-for-administrators}

Maak een `.env.example` bestand aan in uw repository dat dient als template voor andere beheerders. Het bevat placeholder-waarden en documentatie, maar **geen echte secrets**:

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

### ✅ Best Practices {#best-practices}

- **Gebruik `.env.example` als template**
- **Stel strikte bestandsmachtigingen in** — Beperk de toegang tot het `.env` bestand:
  ```bash
  chmod 600 .env
  ```
- **Gebruik sterke wachtwoorden** — Genereer lange, willekeurige wachtwoorden (minimaal 16 tekens) met een mix van letters, cijfers en speciale tekens.
- **Beperk toegang tot het `.env` bestand** — Alleen de gebruiker die Docker Compose uitvoert, mag leesrechten hebben voor het `.env` bestand.

## 🔒 Beveiligingsnotities {#security-notes}

- Gebruik sterke wachtwoorden en wijzig standaardgegevens. Wachtwoorden moeten ten minste 12 tekens lang zijn en een mix van hoofdletters, kleine letters, cijfers en speciale tekens bevatten. Vermijd het gebruik van veelvoorkomende woorden of gemakkelijk te raden informatie.
- Overweeg het implementeren van HTTPS met een reverse proxy in productie
- Regelmatige Back-ups van de PostgreSQL-database worden aanbevolen. Raadpleeg voor gedetailleerde procedures voor back-up en herstel de [Backup and Restore Guide](BackupRestore.md).
