# 🛠️ Vodnik za nastavitev Mail Archiverja {#mail-archiver-setup-guide}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodnik nudi podrobne navodila za nastavitev aplikacije Mail Archiver z uporabo Docker Compose.

## 🛠️ Predpogoj {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Koraki za namestitev {#installation-steps}

1. Na svoj sistem namestite predpogojne programske opreme.

2. Ustvarite `docker-compose.yml` datoteko z naslednjo vsebino:

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

3. Uredite konfiguracijo baze podatkov v `docker-compose.yml` in nastavite varno geslo v spremenljivki `POSTGRES_PASSWORD` in `ConnectionString`.

4. Če želite uporabljati avtentikacijo (kar se močno priporoča), določite `Authentication__Username` in `Authentication__Password`, ki se uporablja za uporabnika administratorja.

5. Prilagodite okoljsko spremenljivo `TimeZone__DisplayTimeZoneId`, da ustreza vaši izbrani časovni coni (privzeto je "Etc/UCT"). Uporabite lahko katerikoli identifikator časovne zone IANA (npr. "Europe/Berlin", "Asia/Tokyo").

6. Po želji konfigurirajte okoljske spremenljive `Logging__LogLevel` za nadzor podrobnosti dnevniškega zapisovanja aplikacije. Za razpoložljive možnosti si oglejte spodnji razdelek Logging Settings.

7. Konfigurirajte reverse proxy po vaši izbiri s https in avtentikacijo za zaščito dostopa do aplikacije. 

> ⚠️ **Pozor**: Sama aplikacija ne zagotavlja zaščenjenega dostopa prek https! Mora biti nameščena prek reverse proxy!

8. Začetni zagon kontejnerjev:
```bash
docker compose up -d
```

9. Ponovni zagon kontejnerjev:
```bash
docker compose restart
```

10. Dostop do aplikacije

11. Prijavite se s svojimi določenimi podatki in dodajte svoje prvo e-poštno račun:
   - Pojdite v razdelek "Email Accounts"
   - Kliknite "New Account"
   - Vnesite podrobnosti o strežniku in svoje podatke za prijavo
   - Shranite in začnite z arhiviranjem!
   - Če želite, ustvarite druge uporabnike in dodelite račune.

## 📚 Razlaga okoljskih spremenljivk {#environment-variable-explanations}

### 🗄️ Povezava z bazo podatkov {#database-connection}
- `ConnectionStrings__DefaultConnection`: Nize za povezavo s PostgreSQL bazo podatkov. Po potrebi spremenite vrednosti `Host`, `Database`, `Username` in `Password`.

### 🔐 Nastavitve avtentikacije {#authentication-settings}
- `Authentication__Username`: Uporabniško ime za administrativni račun.
- `Authentication__Password`: Geslo za administrativni račun.
- `Authentication__SessionTimeoutMinutes`: Čas izteka seje v minutah. To velja za nepermanentni kledeč za prijavo; uporabniki, ki ob prijavi obdmarkirajo **Remember Me**, prejmejo permanentni kledeč, ki velja 7 dni. Več o tem na [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: Ime kledeča za avtentikacijo.
- `Authentication__CookieSameSite`: Konfigurira atribut SameSite za kledeče za avtentikacijo, seje in zaščito pred CSRF. Veljavne vrednosti so:
  - `Strict` (privzeto): Maksimalna varnost. Kledeči se pošljejo le pri zahtevah iste strani (same-site). To lahko povzroči težave pri navigaciji v aplikacijo iz zunanjih povezav (npr. klik na povezavo z drugega spletnega mesta), saj obstoječa seja ne bo prepoznana.
  - `Lax`: Priporočeno pri uporabi obversega proxyja. Kledeči se pošljejo pri navigacijah na najvišji ravni in zahtevah iste strani, kar uporabnikom omogoča sledenje zunanjim povezavam do aplikacije, hkrati pa ohranja zaščito pred CSRF za POST zahteve.
  - `None`: Kledeči se pošljejo pri vseh zahtevah. Zahteva HTTPS in atribut `Secure`. Uporabite to le, če imate specifične zahteve glede medstranskega komuniciranja in razumete varnostne posledice.

### 🔌 Nastavitve REST API {#rest-api-settings}
Neobvezni REST API v načinu samo branja je **samodejno onemogočen**. Za celoten pregled poglejte [vodnik za REST API](API.md).
- `Api__Enabled`: Glavno stikalo za REST API v načinu samo branja (osnovna vrednost `false`). Ko je `false`, vsi `/api/*` poti vrnejo `404`.
- `Api__AllowAttachmentDownloads`: Omogoči prenos bajtov prilog prek API-ja (osnovna vrednost `true`). Ko je `false`, endpoint za priloge vrne `403`.
- `Api__AllowMessageDownloads`: Omogoči prenos celotnih sporočil prek API-ja, tako z obnovljenim `/eml` kot z izvirnim `/raw` (osnovna vrednost `true`). Ko je `false`, oba endpointa vrneta `403`. Celotno sporočilo vsebuje svoje priloge, zato morata biti **tako** to stikalo kot `Api__AllowAttachmentDownloads` nastavljena na `true`; izklop bilo katerega od njima onemogoči oba endpointa.
- `Api__EnableSwaggerUi`: Razpoložljivost Swagger UI na `/apidocs` in OpenAPI dokumentacije na `/apidocs/spec/v1.json` (osnovna vrednost `true`). Oba zahtodata prijavljeno v brskalniku uporabniško nego.
- `Api__DefaultPageSize`: Osnovna velikost strani za listne endpointe, če ni navedena (osnovna vrednost `20`).
- `Api__MaxPageSize`: Maksimalna dovoljena velikost strani; večji zahtevi bodo omejeni (osnovna vrednost `100`).
- `Api__RateLimitPerMinute`: Proračun zahtev v fiksnem oknu na ključ API na minuto (osnovna vrednost `120`).

### 🤖 Nastavitve MCP strežnika {#mcp-server-settings}
Neobvezni MCP (Model Context Protocol) strežnik omogoča enak dostop do e-pošte v modi samo branje kot REST API AI agentom prek Streamable HTTP prenosa. **Privzeto je onemogočen**. Za celoten pregled poglejte [vodnik za MCP](MCP.md). MCP ponovno uporablja popolnoma iste API ključe kot REST API (`ma_...` nosilne žetonov) in isto omejevanje dostopa do računov na uporabnika.
- `Mcp__Enabled`: Glavno stikalo za MCP strežnik (privzeto `false`). Ko je `false`, končnica `/mcp` vrne `404`.
- `Mcp__AllowAttachmentDownloads`: Dovoli orodju `get_attachment`, da vrne bajte prilog (privzeto `true`). Ko je `false`, orodje vrne napako.
- `Mcp__DefaultPageSize`: Privzeta velikost strani, ki jo uporablja `search_emails`, ko klicatelj izpusti `pageSize` (privzeto `20`).
- `Mcp__MaxResults`: Zgornja meja velikosti strani za `search_emails`; večje vrednosti so omejene (privzeto `100`).
- `Mcp__MaxAttachmentBytes`: Maksimalna velikost priloge (v bajtih), ki jo bo orodje `get_attachment` vrnilo neposredno kot base64; večje priloge so zavrnjene (privzeto `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Dovoli orodju `get_email_eml` in `get_email_raw`, da vrneta celotna sporočila (privzeto `true`). Ko je `false`, orodja vrneta napako. Celotno sporočilo vsebuje svoje priloge, zato morata biti **tako** to stikalo kot `Mcp__AllowAttachmentDownloads` nastavljena na `true`; izklop bilo katerega od obeh onemogoči oba orodja.
- `Mcp__MaxMessageBytes`: Maksimalna velikost sporočila (v bajtih), ki jo bosta orodja `get_email_eml` in `get_email_raw` vrnila neposredno; večja sporočila so zavrnena (privzeto `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Proračun zahtev v fiksnem oknu na API ključ na minuto (privzeto `120`).

### 📨 Nastavitve MailSync {#mailsync-settings}
- `MailSync__IntervalMinutes`: Interval v minutah med sinhronizacijo e-pošte. To je globalni privzeto; vsak račun lahko to nastavoro prepiše posamezno na strani Create/Edit (pustite prazno, da uporabite to privzeto vrednost).
- `MailSync__FullSyncIntervalHours`: Nepričakovana globalna privzeta vrednost za samodejne popolne ponovne sinhronizacije v urah. Ko ni nastavljena (privzeto), se nočna samodejna popolna sinhronizacija ne izvaja, razen če je na strani Create/Edit nastavljena vrednost `FullSyncIntervalHours` za posamezen račun. Vrednosti za posamezne račune prepišejo to globalno privzeto vrednost.
- `MailSync__TimeoutMinutes`: Časovni omejitev (timeout) sinhronizacije za posamezen račun v minutah. Sinhronizacija, ki teče dlje, se ustavi pri naslednji meji sporočila, ohrani svoje kontrolne točke in nadaljuje pri naslednjem zagonu; delo se prijavi kot `Timed Out` in `LastSync` se ne napreduje. Velja le za načrtovane sinhronizacije; sinhronizacija, zagnana iz uporabniškega vmesnika (ročna sinhronizacija ali popolna ponovna sinhronizacija), teče brez časovne omejitve. `0` (privzeto) ali katera koli nepozitivna vrednost pomeni brez časovne omejitve. Pred nadgradnjo preglejte vrednost, če sinhronizirate velike predvalnike pošte. Več o tem v [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Časovna omejitev povezave za IMAP povezave v sekundah.
- `MailSync__CommandTimeoutSeconds`: Časovna omejitev ukazov za IMAP ukaze v sekundah.
- `MailSync__AlwaysForceFullSync`: Ali naj se vedno prisili popolna sinhronizacija (true/false).
- `MailSync__IgnoreSelfSignedCert`: Ali naj se ignorirajo samodejno podpisana certifikata (true/false).
- `MailSync__MaxConcurrentSyncs`: Koliko sinhronizacij računov lahko teče hkrati. Privzeto `1` (sekvencialno, združljivo navzoč). Razporejevalnik ponovno napolni razpoložljive mesta, ko postanejo prosti, namesto da bi čakal na celotno skupino, zato en počasen predvalnik ne zavira več ostalih. Povečajte za hkrati sinhronizirati več računov — upoštevajte omejitve števila zahtev ponudnika in lokalno porabo virov. Več o tem v [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Nepričakovana zakasnitev (stagger delay) v sekundah, ki se uporabi ob koncu vsake naloge sinhronizacije računa. Privzeto `0` (brez zakasnitve). Uporabno za preprečevanje kratkih udarjev ob zagonu, ko je `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Koliko težav vsake vrste si delo sinhronizacije zapomni za stran računa. Neuspešni mapi, manjkajoče mapi in neuspešna sporočila imajo ločen proračun, zato množica ene vrste ne more izbrisati drugih iz pregleda; vse, kar preseže proračun, se le šteje namesto shranjevanja. Privzeto `20`. `0` izključi podrobnosti in pusti le številke. Več o tem v [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Mapi, izključene iz sinhronizacije za **vsak** račun, poleg seznama izključene mapi za vsak posamezen račun. Privzeto prazno, zato obstoječe nastavitve niso vplivane. Oba seznama sta **dodajnilna** — mapa se preskoči, če ustreza kateremu izmed njima — in oba uporabljata iste pravila ujemanja: natančno ujemanje celotne poti, natančno ujemanje imena mapi, ujemanje z nastavkom poti (tako `Drafts` ustreza tudi `INBOX/Drafts` in `INBOX.Drafts`) in vse pod mapi, ki jo vnos navede, ko je `MailSync__ExcludeSubfolders` vklopljen. Ujemanje je neobčutno na velike in male črke. Uporabno pri uvozu številnih predvalnikov pošte z istega strežnika, kjer bi bila alternativa vzdrževanje identičnega seznama izključitev za vsak račun. Privzeto nobena mapa ni izključena; katera imena je smiselno navesti, je odvisno od strežnika in njegovega jezika. Primer za drevo predvalnika pošte, ki vsebuje tudi mapi za koledar in kontakte:
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
- `MailSync__ExcludeSubfolders`: Ali vnos za izključitev pokriva tudi mape pod tisto, ki jo navaja. Velja tako za seznam za posamezen račun kot za tistega za celotno namestitev, in za oba ponudnika. Privzeto `false`, kar ustreza vedenju, preden je ta možnost obstajala: vnos `Deleted Items` ustreza samo tej mapi, drevo pa ostane zunaj arhivacije z navajanjem njegove koreninske mapi in vsake mapi pod njo. Nastavite na `true`, da bo vnos zajel tudi celotno drevo pod njim — vnos `Deleted Items` nato tudi zadrži `Deleted Items/2024` in preostanek te veje zunaj arhivacije. Pravilo se navezuje na ločilnik poti, zato zajema celotne mape in nikoli dela imena: `Archive` pokriva `Archive` in `Archive/Team`, in pusti mapa z imenom `Team Archive Review` neopričmano. Tudi `.` šteje kot ločilnik, na enak način, kot ga obravnava pravilo nastavka poti.

### 📧 Nastavitve shranjevanja originalnih sporočil {#original-message-storage-settings}

Privzeto arhiv shranjuje le razčlenjene dele e-pošte, zato je izvoženi `.eml` rekonstrukcija: kodiranje prenosa, MIME meje in prepogibanje zaglavja se razlikujejo od prejetega sporočila, DKIM podpisi pa niso več veljavni. S `RawMessages:Enabled` se shrani tudi celotno originalno sporočilo.

| Parameter | Default | Namen |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Shranite original vsake novo arhivirane e-pošte. |
| `RawMessages__Storage` | `postgres` | Kje se nahajajo bajti: `postgres` ali `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Večja sporočila se arhivirajo brez njihovega originala. `0` = brez omejitve. |
| `RawMessages__IncludeGraph` | `true` | Shranjujte originale tudi za Microsoft 365 (Graph) račune. |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Direktorij za datoteke sporočil. Mora kazati na zmontiran volumen v Docker. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Stopnje direktorijev, na katerih je razpršen id e-pošte. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Preverite SHA-256 ob vsakem prenosu. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Odstranite datoteke sporočil, na katere se nobena e-pošta ne nanaša. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Kako pogosto se izvaja to čiščenje. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Datoteke, ki so mlajše od tega, se nikoli ne odstranijo. |

!!! warning "Backend datoteke potrebuje zmontiran volumen"
    S `RawMessages__Storage=filesystem` datoteke sporočil zunaj baze podatkov.
    Brez bind mounta bodo ostale v zapisljivem sloju posameznega kontejnerja in bodo izgubljene ob naslednjem ponovnem ustvarjanju kontejnerja, medtem ko vrstki v bazi podatkov ostanejo. Varnostno kopirajte direktorij skupaj s kopijo baze podatkov.

Za celoten opis si oglejte **[Original Message Storage](RawMessages.md)**: kje se poslužuje original, strežni sistemi za shranjevanje, kako premakniti obstoječa sporočila med njih z uporabo `--migrate-raw-storage` in kaj je treba varnostno kopirati.

### 📈 Nastavitve spremljanja {#monitoring-settings}

| Parameter | Default | Namen |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | Aktivirana računa, starejša od tega, povzročijo `/api/v1/health` poročilo `Degraded`. `0` onemogoči preverjanje. |
| `Metrics__Enabled` | `false` | Omogoči Prometheus metrics endpoint in njegov osveževalni z})} loop. |
| `Metrics__RefreshMinutes` | `5` | Kako pogosto se znova zgradi snapshot metrik. Vrednosti pod 1 se povečajo na 1. |

Endpoints za zdravje in metrike zahtevajo admin API ključ in `Api__Enabled=true`. Za iskalnike (endpoints), imena metrik, konfiguracijo Prometheus in Docker health check si oglejte
**[Health and Metrics](Monitoring.md)**.

### 📤 Nastavitve BatchRestore {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Število e-poštnih sporočil, ki sproži asinkrono obdelavo.
- `BatchRestore__MaxSyncEmails`: Najvišje število e-poštnih sporočil za sinhrono obdelavo.
- `BatchRestore__MaxAsyncEmails`: Najvišje število e-poštnih sporočil za asinkrono obdelavo.
- `BatchRestore__SessionTimeoutMinutes`: Čas izteka seje za batch restore v minutah.
- `BatchRestore__DefaultBatchSize`: Z умолчанию velikost paketa (batch size) za operacije z e-pošto.

### 🏢 Nastavitve upravljanja s strani (Tenant Management) {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Najvišji število poštovnih predalov, ki jih je mogoče dodati v eni operaciji upravljanja s strani. Z умолчанию je vrednost `1000`. To vrednost povečajte za zelo velike strani ali jo zmanjšajte, da preprečite naključne masovne uvoze. Ko je omejitev presežena, operacija zavrže z napako pri validaciji in računi niso ustvarjeni. Za podrobnosti si oglejte [M365 Tenant Import Guide](M365TenantImport.md).

### 🐄 Nastavitve Mailcow {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Najvišji število poštovnih predalov, ki jih je mogoče pripraviti v enem samostojnem Mailcow aktivacijskem koraku. Privzeto je `1000`. Zmanjšajte to vrednost, da preprečite accidentalno masovno pripravljanje, ali jo povečajte za velike instance. Ko je omejitev presežena, se računi ne ustvarijo. Za podrobnosti si oglejte [Mailcow Instances](Mailcow.md).
- Integracija Mailcow dodatno zahteva `Security__CredentialEncryptionKey` (poglejte [Security Settings](#-security-settings)). Povezovanje instance in aktivacija poštovnih predalov sta zavržena, dokler šifriranje poveril ni onemogočeno, zato API ključ in generirana gesla aplikacij ne moreta biti nikoli shranjena kot navadni besedilo.
- Za odhodni dostop je potreben dostop do Mailcow HTTPS API in do IMAP gostitelja prek TLS (običajno port `993`).

### 📦 Nastavitve BatchOperation {#batchoperation-settings}
- `BatchOperation__BatchSize`: Velikost paketa (batch size) za operacije z e-pošto.
- `BatchOperation__PauseBetweenEmailsMs`: Premor med posameznimi e-poštnimi sporočili v milisekundah.
- `BatchOperation__PauseBetweenBatchesMs`: Premor med paketi v milisekundah.

### 📤 Nastavitve za offload {#offload-settings}
Nastavitve za [date-windowed offload](Offload.md). Vse privzete vrednosti ponovijo vedenje, ki ga je aplikacija imela, preden je ta funkcija obstajala, zato namestitev, ki ne konfigurira tega razdelka, ni vplivana.
- `Offload__MaxConcurrentJobs`: Koliko oprav za obnovitev (restore) ali offload lahko teče hkrati. Privzena vrednost je `1`, kar ohranja strogo serijsko obdelavo, ki jo je vrstica oprav v vedno uporabljala. Ne glede na to vrednost teče največ ena oprav na ciljno poštno predalo.
- `Offload__PrefetchMaxMessages`: Zgornja meja števila sporočil, ki so indeksirana iz ciljnega poštnega predala za zaznavanje ponovitev. Privzena vrednost je `500000`. Nad to vrednostjo se preverjanje zoži na eno mapo in zabeleži, da je obseg bil zmanjšan.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Mapice izvora, ki se nikoli ne prenosijo (offload), ki se ujemajo pred preimenovanjem in pokrivajo podmapice. Privzeto so prazne.
- `Offload__FolderRenameMap__<SourceFolder>`: Prepiše začetne dele poti mapice izvora, na primer `Offload__FolderRenameMap__Sent Items=Sent`. Privzeto je prazno.
- `Offload__MarkAsSeen`: Ali je dodana pošta označena kot prebrana. Privzena vrednost je `true`, kar ustreza obstoječemu vedenju pri obnovitvi.

Obeh nastavitvah map je namerno pustilo praznih: prepisovanje ali izbrisovanje map brez izrecnega navodila bi presenetilo vsakogar, ki že uporablja pot za obnovitev. Zato z delovanjem brez konfiguracije migrirate vse, vključno s mapami z neželeno pošto, in ustvarite niz posebnih map poleg tistih v ciljni arhivi.

### 📊 Nastavitve sledenja širini h banda {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Omogoči ali onemogoči sledenje širini h banda za obdelavo omejitev hitrosti IMAP (true/false). Privzeto je `false`. Ko je funkcija omogočena, sistem sledi porabi širine h banda na posamezno konto in lahko ustavi sinhronizacijo, ko so dosežene omejitve ponudnika. Za podrobne informacije si oglejte [Rate Limit Handling](RateLimitHandling.md).
- `BandwidthTracking__DailyLimitMb`: Dnevna omejitev prenosov v megabitih na konto. Privzeto je `25000` (25 GB). Za ponudnike z omejitvami širine h banda to nastavite tako, da ustreza njihovi omejitvi hitrosti (npr. `2500` za ponudnike z dnevno omejitvijo okoli ~2500 MB). Sistem bo ustavil sinhronizacijo, ko bo dosežena ta omejitev.
- `BandwidthTracking__WarningThresholdPercent`: Delež dnevne omejitve, pri katerem se zabeležijo opozorilna sporočila. Privzeto je `80`. Ko poraba širine h banda doseže ta odstotek, se zabeležijo opozorila, da bi pomagali spremljati približevanje omejitve.
- `BandwidthTracking__PauseHoursOnLimit`: Število ur, za obdobje, ko se sinhronizacija ustavi, ko je dosežena dnevna omejitev. Privzeto je `24`. Po tem obdobju se zastavka za omejitev samodejno odstrani in sinhronizacija nadaljuje.
- `BandwidthTracking__TrackUploadBytes`: Ali želite slediti tudi širini h banda za prenese (upload) (true/false). Privzeto je `false`. Večina IMAP ponudnikov omejuje le prenose (download), zato to običajno ni potrebno.

### 🎯 Nastavitve izbire {#selection-settings}
- `Selection__MaxSelectableEmails`: Najvišja število e-poštnih sporočil, ki jih je mogoče izbrati hkrati.

### 👁️ Nastavitve ogleda (Zasebnost in prikaz) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Nadzoruje privzeto vrsto pogleda e-pošte za zasebnost in preprečevanje sledenja (true/false). Privzeto je `false`.
  - Ko je nastavljeno na `true`: E-pošte se privzeto odpirajo v obliki besedila (plain-text), kar preprečuje samodejno nalaganje pikselov za sledenje, zunanjih slik in spletnih žarometov (web beacons). To priporočamo uporabnikom, ki so zaskrbljeni glede sledenja e-pošte in zasebnosti.
  - Ko je nastavljeno na `false`: E-pošte se privzeto odpirajo v HTML obliki, kar prikazuje oblikovano e-pošto s slikami in slogovanjem.
  - Uporabniki lahko med HTML in besedilno (plain-text) vrsto preklapljajo z gumbom na strani s podrobnostmi e-pošte (vidno le, ko sta na voljo oba formata).
  - Povezava "Full View" upošteva trenutno izbrano vrsto pogleda.

- `View__BlockExternalResources`: Blokira zunanje vire (oddaljene slike, zunanje CSS, zunanje skripte, spletne police itd.) v HTML prikazih e-pošte, da prepreči sledenje in izboljša zasebnost (true/false). Privzeto je `false`.
  - Ko je nastavljeno na `true`: Zunanji viri so pri prikazovanju HTML e-pošte filtrirani. Prikazujejo se le vključeni (inline) vsebine in data URIs (vključno z vključenimi prilogami prek referenčnih oznak `cid:`).
  - Ko je nastavljeno na `false`: HTML e-pošta se prikazuje z vsemi njenimi prvotnimi zunanjimi viri.
  - **Pomembno**: Ta nastavitev vpliva le na **prikaz** e-pošte. Arhivirane e-pošte so v bazi podatkov shranjene popolnoma nespremenjene, pri čemer je ohranjena vsa prvotna vsebina.
  - Blokirani viri vključujejo:
    - Oddaljene slike (pikseli za sledenje, zunanje slike, shranjene na strežnikih)
    - Zunanje CSS slogovnike
    - Zunanje police prek @font-face
    - Zunanje CSS uvoze prek @import
    - Zunanje slike ozadja
  - dovoljeni viri:
    - Vključene slike, vključene kot data: URIs
    - Vključene priloge, ki se nanašajo na cid: URIs
    - Vključeni CSS slogi in oznake style
  - Ta nastavitev deluje neodvisno od `DefaultToPlainText` in zagotavlja dodatno plast zaščite zasebnosti pri ogledu HTML e-pošte.

### 📊 Nastavitve nadzorne plošče {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Ali kartice številnic vsebujejo svoje dele za prejemno in pošiljateljsko korespondenco, kartica računa pa število različnih domen. Privzeto je `true`. Če nastavite na `false`, se te številke ne izračunajo, temveč so namesto izračunavanja le skrite: številniki prikazujejo preprosto število, število prilog pa ne izvaja združevanja z e-pošto, na katero je navezano, kar predstavlja eno zapyt, ki ga ti deli dodajo.
- `Dashboard__SelectablePeriods`: Ali grafi na nadzorni plošči ponujajo ločljivost, obdobje in puščice za njihovo premikanje. Privzeto je `true`. Če nastavite na `false`, grafi prikazujejo zadnjih dvanajst mesecev po mesecih in pošiljatelje celotnega arhivščine; končnica grafa odgovarja, kot da ne bi obstajala, najstarejši datum pošiljanja pa ni iskan.
- `Dashboard__CacheSeconds`: Kako dolgo so izračunana statistična podatki nadzorne plošče (skupna števila s razdelitvijo na prejemno in pošiljateljsko korespondenco, števila na račun, serije grafov, najpogostejši pošiljatelji, nedavne e-pošte, velikost baze podatkov) shranjeni v predpomnilniku strežnika v spominu. Privzeto je `60` sekund. Nastavite na `0`, da onemogočite predpomnilnik in vedno ponovno izračunate statistiko. Vsaka izbira ločljivosti in obdobja grafa je predpomniljena posebej, zato nadzorna plošča, ki se preključa med izbira, shrani en zapis za vsako izbiro. Višje vrednosti zmanjšajo obremenitev baze podatkov v velikih okoljih na račun zastarelih številk. Pripadki statusa sinhronizacije, vrednosti shranjevanja in seznam nedavnih e-pošt se vedno pridobivajo v živo in niso vplivni na ta predpomnilnik.
- `Dashboard__RefreshIntervalMinutes`: Kako pogosto poslužba v ozadju `DashboardStatsRefreshService` ponovno izračuna zahtevne agregacije nadzorne plošče (skupna števila s razdelitvijo na prejemno in pošiljateljsko korespondenco, števila na račun, število domen računov, privzeto serijo grafov z najpogostejšimi pošiljatelji, velikost baze podatkov) v tabelo baze podatkov `DashboardStatsCache`. Pri namestitvah z več milijoni e-poštnih sporočil nadzorna plošča bere te pripravljene vrednosti namesto agregiranja celotne arhivščine v poti zahteve, kar skrajša prvi nalet iz približno 10 sekund na milisekunde. Končnica grafa odgovarja na privzeto izbiro (meseci v obdobju enega leta, prejemni pošiljatelji, trenutno obdobje) iz istega vrstice, zato se ob vrnitvi k njej prikažejo številke ob prvem prikazu; vsaka druga izbira se izračuna ob zahtevi in ohrani v zgornjem predpomnilniku v spominu. Privzeto je `15` minut. Nastavite na `0`, da onemogočite osveževanje v ozadju; nadzorna plošča nato ponovno izračuna statistiko v živo (ob še vedno veljavnem predpomnilniku v spominu, navedenem zgoraj). Prikazane vrednosti so lahko za največ konfigyurirani interval za zamudne za dejansko stanje arhivščine. Vrstica, izračunana z drugimi vrednostmi `ShowDirectionSplits` ali `SelectablePeriods` kot so trenutne, se ne uporabi; nadzorna plošča izračunava v živo do naslednjega osveževanja.

### 🔄 Nastavitve oprav (Jobs Settings) {#jobs-settings}
- `Jobs__RefreshSeconds`: Kako pogosto se stran s procesi v ozadju samodejno osvežuje, ko je zagnan zagnan tab v brskalniku. Privzeto je `30` sekund. Nastavite na `0`, da izklopite samodejno osveževanje in ga opravite ročno. Vsako osveževanje ponovno zgradi stran iz vseh osmih virov oprav, zato pri namestitvah z veliko računov daljši interval zmanjša obremenitev, ki se мноži z vsakim odprtim tabom na strani.

### 🗃️ Nastavitve Npgsql {#npgsql-settings}
- `Npgsql__CommandTimeout`: Čas izteka za bazične ukaze v sekundah.

### 📥 Nastavitve nalaganja {#upload-settings}
- `Upload__MaxFileSizeGB`: Maksimalna velikost datoteke za nalaganje v GB.
- `Upload__KeepAliveTimeoutHours`: Keep alive časovni zamik za nalaganje v urah.
- `Upload__RequestHeadersTimeoutHours`: Časovni zamik za glavice zahtevkov v urah.

### 📂 Nastavitve lokalnega uvoza {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1` itd.: Beli seznam lokalnih direktorij, iz katerih lahko ukazom za uvoz CLI (`--import-mbox`, `--import-eml`) dovolite branje datotek. Vsak vnos je pot znotraj posameznega kontejnerja. Datoteke za uvoz morate z uporabo Docker volumnov zmontirati v eno od teh direktorij. Z умолчанию: `/data/import` (samodejno nastavljeno na `/app/uploads` kot nadomestno).
  - Pri uporabi `docker exec` za zagon ukaza za uvoz mora biti pot do datoteke, ki jo je podana prek `--file`, v okviru ene od teh dovoljenih poti.
  - Ta varnostni ukrep preprečuje naključen dostop do sistemskih datotek prek ukazov CLI.
  - Za različne vire uvoza lahko nastavite več poti.
  - Za podrobne navodila za uporabo si oglejte [CLI commands](CLI.md).

### 📄 Nastavitve uvoza CSV {#csv-import-settings}
- `CsvImport__MaxRows`: Najvišja število vrstic CSV-ja (poštovnih predalov), obdelanih pri enem samem masovnem uvozu. Privzeto je `5000`. Za velike implementacije povečajte to vrednost; za omejitev vpliva posameznega uvoza na obremenitev baze podatkov jo znižajte.
- `CsvImport__MaxFileSizeBytes`: Najvišja dovoljena velikost (v bajtih) naložene CSV datoteke. Privzeto je `10000000` (10 MB). Če je potrebno, prilagodite to vrednost svojim omejitvam nalaganja.
- Za podrobne navoditve o uporabi masovnega uvoza IMAP računov prek CSV poglejte [Account Import Guide](Account-Import.md).

### 📤 Nastavitve izvoza revizijskega poročila {#audit-export-settings}
Nastavitve za stran za izvoz revizijskih podatkov (samo za administratorje, dostopna z strani Logs). Izvoz generira ZIP paket s tabelarnimi masovnimi podatki (INDEX.XML + CSV tabele + DTD) iz obstoječega arhivščnika za zunanje revizijske orodje. Za podrobnosti o uporabi si oglejte [Audit Data Export Guide](AuditExport.md).
- `AuditExport__DataSupplierName`: Z умолчанию vrednost za polje obrazca "Data supplier name", ki identificira vašo organizacijo v izvoženem indeksi datoteki. Z умолчанию: prazno.
- `AuditExport__DataSupplierLocation`: Z умолчанию vrednost za polje obrazca "Data supplier location" (npr. sedež podjetja). Z умолчанию: prazno.
- `AuditExport__Comment`: Z умолчанию vrednost za polje obrazca "Comment", ki je vključeno kot prost tekst v izvoženi indeksi datoteki. Z умолчанию: prazno.
- `AuditExport__OutputDirectory`: Nadполниčje, kjer so shranjene generirane ZIP datoteke. Relativne poti se razrešujejo glede na korenino vsebine aplikacije. Z умолчанию: `exports/audit`.
- `AuditExport__RetentionDays`: Število dni, po katerih dnevno čiščenje izbriše dokončane datoteke izvoza. Z умолчанию: `30`.
- `AuditExport__MaxRangeYears`: Najvišji dovoljeni razpon med začetnim in končnim datumom obdobja izvoza. Z умолчанию: `10`.
- Polja obrazca so predpolnjen s temi privzetimi vrednostmi, vendar jih je mogoče urediti za vsak izvoz posebej.
- Vsak izvoz v dostopni dnevnik zapiše vnos o začetku in vnos o rezultatu (tip "Audit Data Export"), zato je zgodovina varna pred spremembami brez kakršnihkoli sprememb sheme DB.

### 🔒 Nastavitve politike brisanja {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Nadzoruje, ali je dovoljeno ročno brisanje arhiviranih e-poštnih sporočil (true/false). Z умолчанию je `true`. Ko je nastavljeno na `false`:
  - Vse arhivirana e-poštna sporočila so ob zagonu zaklenjena (`IsLocked = true`) prek sprožitev skladnosti v bazi podatkov, kar preprečuje kakršno koli spremembo ali brisanje na ravni baze podatkov.
  - Ročno brisanje (posameznih in množičnih) je blokirano na ravni aplikacije z napako.
  - Privzeta vrednost stolpca je prilagojena tako, da so tudi novo uvožena e-poštna sporočila zaklenjena.
  - Trenutno stanje politike se ob vsakem zagonu zabeleži v tabeli AccessLogs (na strani Logs je vidno kot vpiši "Deletion Policy") za potrebe revizije.
  - Lokalno brisanje zaradi zadržanja (retention) je izjema: e-poštna sporočila, ki spadajo v konfigurirano obdobje zadržanja, se še vedno izbrišejo (pred brisanjem v procesu zadržanja so takoj odblokirana).
- `DeletionPolicy__MaxBulkDeleteEmails`: Zgornja meja za eno množično brisanje na podlagi filtra (*Delete all matches*). Z умолчанию je `100000`. Filter, ki ustreza več e-poštnih sporočil, bo zavrnitven z sporočilom, da ga treba zoščititi; delo nikoli ne odreže podatkov tiho. E-poštna sporočila, zaklenjena zaradi politike zadržanja, so preskočena in poročana v statusu dela, ne pa odblokirana. Delo ob zagonu ponovno preveri to omejitev in `DeletionPolicy__DeletionAllowed`, zato se delo, ki je bilo v vrstici pred spremembo politike, ustavi namesto da bi se izvedlo s staro nastavitvijo.
- **Zaščita neustreznosti (Immutability protection):** Ko je `IsLocked = true`, sprožitev skladnosti baze podatkov (`prevent_locked_email_changes`) blokira KAKOŠNO spremembo vrstice e-poštnega sporočila — zaščiteni so vsi stolpci, ne le seznam določenih polj. Izjeme so le stolpci `IsLocked` sama (tako da ostane mogoča odblokava za brisanje zaradi zadržanja in uporaba politike ob zagonu) in `FolderName` (tako da lahko IMAP sinhronizacija posodobi mapo, ko se e-poštno sporočilo premakne na strani strežnika). Zaščita je neodvisna od stolpcev (primerjava na podlagi JSONB) in samodejno zajema prihodnje dodajanja v shemo.

### 🕐 Nastavitve časovne zone {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Časovna zona, ki se uporablja za prikaz tidskih žigov e-pošte v uporabniškem vmesniku. Uporablja identifikatorje časovnih zon IANA (npr. "Europe/Berlin", "Asia/Tokyo"). Privzeta vrednost je "Etc/UCT" zaradi združljivosti s starejšimi različicami. Ob uvozu bodo tidski žigi e-pošte pret easykonvertirani v to časovno pono za namene prikaza.

### 🎉 Nastavitve ReleaseNotes (Splash zaslon za posodobitev različice) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: Nizek različice, ki ga prijavi ta instanca. Primerja se z GitHub oznakami izdaj (release tags) in določa, ali obstaja novejša različica. Če je prazno, se spremenki (changelog) zamenjajo z različico kompajlirane assemblies in opozorilo o posodobitvi ostane izklopljeno.
- `ReleaseNotes__Enabled`: Omogoči ali onemogoči splash zaslon za posodobitev različice (true/false). Privzeto je `true`. Ko je funkcija omogočena, bodo administratorji po posodobitvi aplikacije videli enkratni modalni okno s spremenki, ki prikazuje zapiski o izdaji, pridobljene iz GitHub Releases za trenutno različico. Vsak administrator lahko modalno okno zapre in ponovno se bo pojavilo le pri novi različici. Nastavite na `false`, da popolnoma onemogočite to funkcijo, vključno z opozorilom o posodobitvi.
- `ReleaseNotes__UpdateHintEnabled`: Administratorjem prikaži majhno obvestilo (toast) v spodnjem desnem kotu, ko GitHub objavi novejšo stabilno različico od nameščene različice (true/false). Privzeto je `true`. Obvestilo vodi do te izdaje in se v brskalniku zapre za vsako različico posebej. Osnutki (drafts) in predizdaje (prereleases) so prezrti. Zahteva `ReleaseNotes__AppVersion`; brez tega nobeno opozorilo ni prikazano.
- `ReleaseNotes__UpdateCheckIntervalHours`: Kako pogosto se osveži seznam GitHub izdaj, v urah. Privzeto je `6`. Krajši interval omogoča, da je nova različica v dolgotrajni instanci hitreje vidna, daljši interval pa zmanjša število pozivov GitHub API. Minimalna vrednost je `1`.

### 🔧 Nastavitve vzdrževanja baze podatkov {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Omogoči ali onemogoči samodejno dnevno vzdrževanje baze podatkov (true/false). Privzena vrednost je `false`. Ko je funkcija omogočena, bo sistem samodejno izvajal operacije VACUUM ANALYZE za optimizacijo zmogljivosti baze podatkov in preprečevanje zadeblanja (bloat). Za več informacij si oglejte [Database Maintenance Guide](DatabaseMaintenance.md).
- `DatabaseMaintenance__DailyExecutionTime`: Čas dne, ko se mora izvesti vzdrževanje baze podatkov, v 24-urnem formatu (HH:mm). Privzena vrednost je `02:00`. Izberite čas med nizko aktivnostjo sistema.
- `DatabaseMaintenance__TimeoutMinutes`: Najvišji dovoljeni čas za operacije vzdrževanja v minutah. Privzena vrednost je `30`. Za večje baze podatkov povečajte to vrednost.

### 🧬 Nastavitve za deduplikacijo prilog {#attachment-deduplication-settings}
Deduplikacija prilog shranjuje vsako unikatno vsebino priloge le enkrat (s poimenovanjem vsebine po SHA-256) in je **ključna funkcija, ki je vedno vklopljena** – namensko ni na izbira za vklopljanje/izklop. Lahko se prilagajajo le spodnji parametri za obdelavo v serijah/urniki. Za vse podrobnosti si oglejte [Attachment Deduplication Guide](AttachmentDeduplication.md).

- `AttachmentDeduplication__BatchSize`: Število obstoječih prilog, ki se migrirajo v vsaki transakcici med enkratno migracijo obstoječih podatkov v ozadju. Privzeto je `200`. Večje vrednosti omogočajo hitrejšo migracijo, vendar povečajo porabo pomnilnika/obremenitev baze pri vsaki seriji.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Nepriobvezni premor (v milisekundah) med serijami migracije za omejitev obremenitve baze na preobremenjenih sistemih. Privzeto je `0` (brez premora).
- `AttachmentDeduplication__StartupDelaySeconds`: Zavod (v sekundah) po zagonu aplikacije, preden se začne migracija v ozadju, da se dajemo čas za dokončanje migracije sheme. Privzeto je `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Interval (v urah) stalnega čiščenja (garbage collection), ki odstrani vsebine prilog, ki jih nobena e-pošta ne uporablja več. Privzeto je `12`. To teče neodvisno od `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Čas izteka ukazov baze (v sekundah) za operacije serij migracije (INSERT s hashiranjem SHA-256 in UPDATE). Privzeto je `300` (5 minut). Zvočite to vrednost za zelo velike baze ali priloge ali pa jo zmanjšajte, če želite hitrejšo zaznavo napak. Če serija še vedno izteče, storitev samodejno poskusi ponovno s polovico velikosti serije.

### 💾 Nastavitve shirvanja kontov {#account-storage-settings}
Prikaz shirvanja na konto prikazuje porabo shirvanja baze podatkov (vsa polja e-pošte + priloge) za vsak račun v tabeli "Account Overview" v nadzorni plošči in v tabeli "Show All" v MailAccounts. Samostojna ozadinska storitev (`AccountStorageRefreshService`) izračuna vrednosti tako, da sešteve PostgreSQL `pg_column_size` po vrstici (pokriva vsa polja e-pošte brez branja telesa e-pošte) in jih predrami v tabeli `AccountStorageCache`. Ta storitev deluje neodvisno od `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Omogoči ali onemogoči storitev osveževanja shirvanja (true/false). Z умолчанию je `true`. Ko je omogočena, storitev ob zagonu opravi obnavo (backfill) čakajočih računov, ki je lahko nadaljevanča (varen na okvare preko `AccountStorageBackfillState`), nato pa vsakodnevno popolno osveževanje. Ko je onemogočena, se vrednosti shirvanja posodobijo le, ko so e-poštna sporočila sinhronizirana, uvožena ali izbrisana, vendar se prikazane vrednosti lahko čezčas zastarejo.
- `AccountStorage__DailyExecutionTime`: Čas dneva (24-urnski format `HH:mm`) za vsakodnevno popolno osveževanje vseh računov. Z умолчанию je `02:30`. Izberite čas med nizko aktivnostjo sistema.
- `AccountStorage__BackfillDelayMs`: Zamika (v milisekundah) med računi med začetnim backfillom ob zagonu. Z умолчанию je `5000`. Nižje vrednosti pospešijo backfill, vendar povečajo obremenitev baze podatkov; za zelo velike arhive povečajte to vrednost, da preprečite preobremenitev baze podatkov.
- `AccountStorage__RefreshBatchDelayMs`: Zamika (v milisekundah) med računi med dnevnim popolnim osveževanjem. Z умолчанию je `1000`. Nižje vrednosti pospešijo osveževanje, vendar povečajo obremenitev baze podatkov; za zelo velike arhive povečajte to vrednost.
- `AccountStorage__CommandTimeoutSeconds`: Čas izteka ukaza baze podatkov (v sekundah) za izračun shirvanja na konto (`pg_column_size` nad vsemi vrsticami računa). Z умолчанию je `300` (5 minut). Izračun bere vsako vrstico e-pošte vključno s TOAST podatki, kar lahko pri zelo velikih arhivah (250k+ sporočil) traja več kot minuto; prejšnja 30-sekundna privzeta vrednost Npgsql je povzročala tiho neuspešno osveževanje in povzročala, da je nadzorna plošča prikazovala zastarele vrednosti. Povečajte to vrednost, če se izračun pri vašem arhivu še vedno izteče.

> 💡 **Opomba**: Vrednosti shirvanja se osvežijo takoj po vsaki sinhronizaciji, uvozu ali izbrisu zaradi politike zadržanja (retention), zato prikazane vrednosti ostanejo aktualne tudi brez dnevnega osveževanja. Dnevno osveževanje je varnostna mreža, ki zajame izjemne primere (npr. neposredne spremembe v bazi podatkov).

### 📝 Nastavitve beleženja {#logging-settings}
- `Logging__LogLevel__Default`: Privzeto raven beleženja za aplikacijo. Razpoložljive ravni so: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Privzeto je `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Raven beleženja za sporočila okvirja ASP.NET Core. Privzeto je `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Raven beleženja za ukaze baze podataka Entity Framework. Privzeto je `Warning`.

### 🛡️ Varnostne nastavitve {#security-settings}
- `AllowedHosts`: Seznam gostujočih imen, ločenih s poglavki, ki jih je aplikaciji dovoljeno postreči. To pomaga preprečiti napade na HTTP Host header. Primer: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Pomembno**: V produkcijskih okoljih ne uporabljajte `*`, saj to onemogoči preverjanje host headerja.

#### 🔐 Šifriranje poveril v stanju počitv {#credential-encryption-at-rest}
Poverila e-poštnih računov (IMAP geslo, M365 client secret in MSA OAuth tokeni) lahko zašifrirate v bazi podatkov. Ključ za šifriranje se shranjuje **zunaj** baze podatkov (okoljska spremenljivka / Docker secret), zato sama kopija baze podatkov ni dovolj za branje poveril.

- `Security__CredentialEncryptionKey`: Base64 kodiran 256-bitni (32 bajtov) ključ za šifriranje. Generirajte ga z:
  ```bash
  openssl rand -base64 32
  ```
  Vrednost nastavite kot okoljsko spremenljivko (priporočeno: `Security__CredentialEncryptionKey` v vašem compose文件中 ali okolju kontejnerja). Aplikacija ne podpira Docker secret v slogu `_FILE` — če uporabljate Docker secret, ga prepovejte in njegovo vsebino naložite v okoljsko spremenljivko v vašem entrypointu. Ključa nikoli ne oddajajte v `appsettings.json`. Ko je ključ konfiguriran, se poverila zašifrirajo ob zapisu, poslužba v ozadju pa enkrat zašifrir tudi obstoječa poverila v obliki nešifriranega besedila.
- `Security__EncryptAccountCredentials`: Omogoči ali onemogoči šifriranje novih/posodobljenih vrednosti poveril (true/false). Privzeto je `true`. Ko ključ ni konfiguriran, se poverila shranjujejo kot nešifrirano besedilo in ob zagonu se zabeleži opozorilo — obstoječe namestitve niso ovirane, dokler ni zagotovljen ključ.
- `Security__CredentialEncryptionBackfill`: Ali naj poslužba v ozadju ob zagonu zašifrir obstoječa poverila v obliki nešifriranega besedila (true/false). Privzeto je `true`. Brez konfiguriranega ključa ne bo opravilo ničesar.
- `Security__CredentialEncryptionKeyPrevious`: Neobvezni prejšnji ključ, uporablja se le za dešifriranje vrednosti, ki so bile zašifrirane s tem ključem (za rotacijo ključev). Nove vrednosti so vedno zašifrirane s `Security__CredentialEncryptionKey`.

  > ⚠️ **Rotacija ključev**: po rotaciji ohranite konfiguriran `Security__CredentialEncryptionKeyPrevious` **tako dolgo, dokler vsako poverilo ni ponovno zapisano** (npr. z urejanjem računa ali z avtomatskim osvežitvanjem OAuth tokena) — obstoječih vrednosti, zašifriranih s starim ključem, *ni* samodejno ponovno zašifrirano s novim ključem. Ko odstranite prejšnji ključ, postane vsako poverilo, ki je še vedno zašifrirano s tem ključem, nečitljivo in ga je treba ponovno vnesti. Dokler je konfiguriran, ga morate skrbi iti tako varno kot trenutni ključ.

> ⚠️ **Ključ shranjujte varno**: Brez ključa zašifriranih poveril ni mogoče obnoviti in jih je treba ponovno vnesti. Zabeležite ga skupaj z varnostnimi kopijami vaše baze podatkov (toda ločeno od njih).
>
> 💡 **Obstoječe namestitve**: Nadgradite varno — dokler ni nastavljen `Security__CredentialEncryptionKey`, se nič ne spremeni. Ključ nastavite, ko ste pripravljeni; obstoječa poverila v obliki nešifriranega besedila ostanejo čitljiva in se v ozadju zašifrirajo.

### 🔐 Konfiguracija OIDC {#oidc-configuration}

Za podrobne navodila za nastavitev avtentikacije OpenID Connect si oglejte [OIDC Implementation Guide](OIDC_Implementation.md).

#### Osnovne OIDC nastavitve {#basic-oidc-settings}
- `OAuth__Enabled`: Omogoči ali onemogoči OIDC avtentikacijo (true/false)
- `OAuth__Authority`: URL avtoritete OpenID Connect (npr. https://sts.windows.net/{TENANT-ID}/ za Azure AD)
- `OAuth__ClientId`: ID klijenta (client ID), ki ga je dodelila vaša ponudba identitete
- `OAuth__ClientSecret`: Tajna klijenta (client secret), ki jo je dodelala vaša ponudba identitete
- `OAuth__DisplayName`: Izbirno ime, ki se prikaže na gumbu za OIDC prijava in strani za samodejno preusmerjanje (npr. `PocketID SSO`). Če je izpuščeno, se uporabi splošni napis "Login with OAuth".
- `OAuth__ClientScopes__0`: Prva scope (obseg), zahtevana od ponudbe identitete (openid)
- `OAuth__ClientScopes__1`: Druga scope (obseg), zahtevana od ponudbe identitete (profile)
- `OAuth__ClientScopes__2`: Tretja scope (obseg), zahtevana od ponudbe identitete (email)
- `OAuth__ResponseMode`: Izbirni način odgovora (response mode) za zahtevo avtorizacije (`query`, `fragment` ali `form_post`). Privzeto je `form_post`, privzeto za ASP.NET Core. Nastavite na `query` za ponudnike, ki ne podpirajo `form_post`, kot je Synology SSO Server. Poglejte [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Samodejno odobrite nove OIDC uporabnike brez potrebe po ročnem admin odobritvi (true/false). Privzeto je `false`.

#### Nastavitve za zagotavljanje uporabnikov (User Provisioning) {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Samodejno odobrite nove OIDC uporabnike brez potrebe po ročnem admin odobritvi (true/false). Privzeto je `false`. Ko je funkcija omogočena, so uporabniki, ki se avtenticirajo prek OIDC ponudnika, takoj aktivirani in lahko dostopajo do aplikacije. Ko je onemogočena (privzeto), so novi OIDC uporabniki ustvarjeni kot neaktivni in zahtevajo ročno aktivacijo s strani administratorja. Za podrobne informacije si oglejte [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users).
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, itd.: E-poštni naslovi, ki bi morali biti samodejno zagotovljeni kot administratorji. Uporabniki s temi e-poštnimi naslovi bodo ob prvi OAuth prijavi ustvarjeni kot aktivni administratorji, s čimer se obide normalni postopek odobritve. Primerjava e-poštnih naslovov ni občutljiva na velike in male črke.

#### Nastavitve za prijavo brez gesla za dostop {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Onemogoči prijavo z uporabniškim imenom/geslom (true/false). Privzeto je `false`. Ko je funkcija omogočena, je obrazec za geslo skrit **in** strežnik zavrne vsak POST zahtev za geslo, zato je na voljo le OAuth prijava. Zahteva `OAuth__Enabled=true`. Če je OAuth onemogočen, ta nastavitev nima učinka.
- `OAuth__AutoRedirect`: Samodejno preusmeri uporabnike na OAuth ponudnika (true/false). Privzeto je `false`. Zahteva, da je `OAuth__DisablePasswordLogin` nastavljen na `true`. Uporabniki bodo pred preusmeritvijo videli kratek zaslon nalaganja.

#### Primer: Celotna konfiguracija s prednostjo OIDC {#example-full-oidc-first-configuration}
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

## 🔐 Konfiguracija Kestrel HTTPS (neobvezno) {#kestrel-https-configuration-optional}

Čeprav je aplikacija namenjena dostopu prek obversega proxyja z HTTPS, lahko nastavite tudi spletni strežnik Kestrel za uporabo SSL/TLS certifikatov. To zagotavlja končnu enkripcijo med obversem proxyjem in poslodovalnim kontejnerjem aplikacije.

### Koraki konfiguracije {#configuration-steps}

1. **Generirajte ali pridobite SSL certifikat** v formatu PFX (npr. `localhost.pfx`)

2. **Dodajte naslednje okoljske spremenljivke** v vaš `docker-compose.yml` za storitev `mailarchive-app`:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Posodobite preslikavo portov** v storitvi `mailarchive-app`:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Dodajte preslikavo volumov** za certifikat:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Namestite datoteko certifikata** (npr. `localhost.pfx`) v direktorij `./certs` na vašem gostiteljskem sistemu.

### Razlagava okoljskih spremenljivk {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: URL končnice HTTP (privzeto: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: URL končnice HTTPS (privzeto: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Pot do datoteke PFX certifikata znotraj kontejnerja
- `Kestrel__Endpoints__Https__Certificate__Password`: Geslo za datoteko PFX certifikata

> 💡 **Opomba**: Ta konfiguracija je neobvezna. Če uporabljate obversem proxy z HTTPS (priporočljivo), lahko komunikacija med obversem proxyjem in aplikacijo ostane na HTTP. Vendar pa za maksimalno varnost v občutljivih okoljih morda želite omogočiti tudi HTTPS na Kestrelu, da zašifrirate celotno pot komunikacije.

## 🔑 Upravljanje tajnosti za produkcijo {#secrets-management-for-production}

Neposredno vstavljanje občutljivih podatkov, kot so gesla za bazo podatkov, administratorjeva poverilnica in OAuth client secrets, v `docker-compose.yml` predstavlja varnostno tveganje, še posebej, ko je datoteka shranjena v sistemu za nadzor različic. Priporočan pristop je izpeljenje teh vrednosti v datoteko `.env`, ki jo Docker Compose samodejno naloži.

### 📄 Datoteka `.env` {#the-env-file}

V isti direktorij kot vaša `docker-compose.yml` ustvarite datoteko `.env` z naslednjo vsebino:

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

**Pomembno**: Docker Compose samodejno bere datoteko `.env` iz istega direktorija — datoteke **ne** morate navajati ročno. Vse spremenljivke, določene v `.env`, so na voljo v `docker-compose.yml` prek sintakse `${VARIABLE_NAME}`.

### 🐳 Prilagojena `docker-compose.yml` {#adapted-docker-composeyml}

`docker-compose.yml` iz korakov za namestitev je treba prilagoditi tako, da namesto trdno vstavljenih tajnosti uporabite vstavke (placeholders):

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

### 📋 Predloga `.env.example` za administratorje {#envexample-template-for-administrators}

V svojem repozitoriju ustvarite datoteko `.env.example`, ki služi kot predloga za druge administratorje. Vsežna vsebuje vstavke in dokumentacijo, vendar **brez dejanskih tajnosti**:

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

### ✅ Najboljše prakse {#best-practices}

- **Uporabite `.env.example` kot predlogo**
- **Nastavite stroge dovoljenja za datoteke** — Omejite dostop do datoteke `.env`:
  ```bash
  chmod 600 .env
  ```
- **Uporabljajte močna gesla** — Generirajte dolga, naključna gesla (vsaj 16 znakov) z mešanico črk, številk in posebnih znakov.
- **Omejite dostop do datoteke `.env`** — Le uporabnik, ki izvaja Docker Compose, mora imeti dovoljenja za branje datoteke `.env`.

## 🔒 Varnostni opomini {#security-notes}

- Uporabljajte močna gesla in spremenite privzete podatke za prijavo. Gesla morajo imeti vsaj 12 znakov in vključevati mešanico velikih in malih črk, številk ter posebnih znakov. Ne uporabljajte pogostih besed ali informacij, ki jih je lahko enostavno ugandeti.
- Za produkcijo razmislite o implementaciji HTTPS z obratnim posrednikom (reverse proxy).
- Priporočljive so redne varnostne kopije PostgreSQL baze podatkov. Podrobnejši postopki za varnostno kopiranje in obnovo so na strani [Backup and Restore Guide](BackupRestore.md).
