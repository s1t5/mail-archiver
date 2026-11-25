# 🛠️ Navodila za namestitev Mail Archiverja {#mail-archiver-setup-guide}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta priročnik nudi podrobna navodila za namestitev aplikacije Mail Archiver z uporabo Docker Compose.

## 🛠️ Predpogoj {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Koraki za namestitev {#installation-steps}

1. Namestite predpogoje na svoj sistem.

2. Ustvarite datoteko `docker-compose.yml` z naslednjo vsebino:

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

3. Uredite konfiguracijo baze podatkov v `docker-compose.yml` in nastavite varno geslo v spremenljivki `POSTGRES_PASSWORD` in v `ConnectionString`.

4. Če želite uporabljati avtentikacijo (kar se močno priporoča), določite `Authentication__Username` in `Authentication__Password`, ki se uporablja za administrativno uporabnika.

5. Prilagodite spremenljivo okolja `TimeZone__DisplayTimeZoneId`, da ustreza vašemu izbranemu časovnem pasu (privzeto je "Etc/UCT"). Lahko uporabite katerikoli identifikator časovnega pasu IANA (npr. "Europe/Berlin", "Asia/Tokyo").

6. Po želji konfigurirajte spremenljive okolja `Logging__LogLevel` za nadzor podrobnosti logov aplikacije. Za razpoložljive možnosti poglejte spodaj razdelek Logging Settings.

7. Konfigurirajte reverse proxy po svoji izbiri s https in avtentikacijo, da zagotovite varnostni dostop do aplikacije. 

> ⚠️ **Pozor**: Sama aplikacija ne zagotavlja zaščitenga dostopa prek https! Nastaviti jo je treba preko reverse proxy!

8. Začetni zagon kontejnerjev:
```bash
docker compose up -d
```

9. Ponovni zagon kontejnerjev:
```bash
docker compose restart
```

10. Dostop do aplikacije

11. Prijavite se s svojimi določenimi podatki za prijavo in dodajte svoje prvo e-poštno račun:
   - Pojdite v razdelek "Email Accounts"
   - Kliknite "New Account"
   - Vnesite podrobnosti o svojem strežniku in podatke za prijavo
   - Shranite in začnite z arhiviranjem!
   - Če želite, ustvarite druge uporabnike in dodelite račune.

## 📚 Razlagitev spremenljivih okolja {#environment-variable-explanations}

### 🗄️ Povezava z bazo podatkov {#database-connection}
- `ConnectionStrings__DefaultConnection`: Povezovalni niz do PostgreSQL baze podatkov. Po potrebi spremenite vrednosti `Host`, `Database`, `Username` in `Password`.

### 🔐 Nastavitve avtentikacije {#authentication-settings}
- `Authentication__Username`: Uporabniško ime za administrativni račun.
- `Authentication__Password`: Geslo za administrativni račun.
- `Authentication__SessionTimeoutMinutes`: Čas izteka seance v minutah. To velja za kovček za prijavo, ki ni trajen; uporabniki, ki ob prijavi označijo **Remember Me**, namesto tega prejmejo trajen kovček, ki velja natančno 7 dni. Več na strani [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: Ime kovčka za avtentikacijo.
- `Authentication__CookieSameSite`: Konfigurira atribut SameSite za kovčke za avtentikacijo, seanco in CSRF zaščito. Veljavne vrednosti so:
  - `Strict` (privzeto): Največja varnost. Kovčki se pošljejo le pri zahtevah iste strani (same-site). To lahko povzroči težave pri navigaciji v aplikacijo iz zunanjih povezav (npr. klik na povezavo z druge spletne strani), saj obstoječa seanca ne bo prepoznana.
  - `Lax`: Priporočljivo pri uporabi obratnega proxyja. Kovčki se pošljejo pri navigacijah na najvišji ravni in zahtevah iste strani, kar uporabnikom omogoča sledenje zunanjim povezavam do aplikacije, hkrati pa ohranja CSRF zaščito za POST zahteve.
  - `None`: Kovčki se pošljejo pri vseh zahtevah. Zahteva HTTPS in atribut `Secure`. Uporabite to le, če imate specizne zahteve glede medstranskega (cross-site) komuniciranja in razumete varnostne posledice.

### 🔌 Nastavitve REST API {#rest-api-settings}
Neobvezen REST API v okolju samo za branje je **s privzetim nastavljen kot onemogočen**. Za celoten pregled poglejte [vodnik za REST API](API.md).
- `Api__Enabled`: Glavno stikalo za REST API v okolju samo za branje (privzeto `false`). Ko je `false`, vsi `/api/*` poti vrnejo `404`.
- `Api__AllowAttachmentDownloads`: Dovoli prenos bajt prilog skozi API (privzeto `true`). Ko je `false`, končnica za priloge vrne `403`.
- `Api__AllowMessageDownloads`: Dovoli prenos celotnih sporočil skozi API, tako z obnovljeno `/eml` kot z originalno `/raw` (privzeto `true`). Ko je `false`, obe končnici vrneta `403`. Celotno sporočilo vsebuje svoje priloge, zato morata biti **obe** to stikalo in `Api__AllowAttachmentDownloads` nastavljena na `true`; izklop bilo katerega od obeh onemogoči obe končnici.
- `Api__EnableSwaggerUi`: Razpoložljivost Swagger UI na `/apidocs` in OpenAPI dokumenta na `/apidocs/spec/v1.json` (privzeto `true`). Oba zahtevata prijavljeno sesijo v brskalniku.
- `Api__DefaultPageSize`: Privzeta velikost strani za končnice za sezname, če ni navedena (privzeto `20`).
- `Api__MaxPageSize`: Največja dovoljena velikost strani; večji zahtevki bodo omejeni (privzeto `100`).
- `Api__RateLimitPerMinute`: Budžet zahtevov z določenim oknom na minuto na ključ API (privzeto `120`).

### 🤖 Nastavitve MCP strežnika {#mcp-server-settings}
Neobvezen MCP (Model Context Protocol) strežnik omogoča isti dostop do e-pošte v samo branju kot REST API za AI agente prek Streamable HTTP prenosa. **Privzeto je onemogočen**. Za celoten pregled poglejte [vodnik MCP](MCP.md). MCP ponovno uporablja prav iste API ključe kot REST API (`ma_...` bearer tokeni) in isto omejevanje dostopa do uporabniških računov.
- `Mcp__Enabled`: Glavno stičalo za MCP strežnik (privzeto `false`). Ko je `false`, endpoint `/mcp` vrne `404`.
- `Mcp__AllowAttachmentDownloads`: Dovoli orodju `get_attachment`, da vrne bajte prilog (privzeto `true`). Ko je `false`, orodje vrne napako.
- `Mcp__DefaultPageSize`: Privzete število vrsti (page size), ki ga uporabi `search_emails`, ko klicatelj izpustijo `pageSize` (privzeto `20`).
- `Mcp__MaxResults`: Zgornja meja števila vrsti za `search_emails`; večje vrednosti bodo omejene (privzeto `100`).
- `Mcp__MaxAttachmentBytes`: Maksimalna velikost priloge (v bajtih), ki jo bo orodje `get_attachment` vrnilo neposredno kot base64; večje priloge bodo zavrnjene (privzeto `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Dovoli orodjem `get_email_eml` in `get_email_raw`, da vrneta celotna sporočila (privzeto `true`). Ko je `false`, orodja vrnejo napako. Celotno sporočilo vsebuje svoje priloge, zato morata biti **obe** to stičalo in `Mcp__AllowAttachmentDownloads` nastavljena na `true`; izklop katerega od obeh onemogoči oba orodja.
- `Mcp__MaxMessageBytes`: Maksimalna velikost sporočila (v bajtih), ki jo bodo orodja `get_email_eml` in `get_email_raw` vrnila neposredno; večja sporočila bodo zavržena (privzeto `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Budžet zahtevov v fiknem oknu na ključ API na minuto (privzeto `120`).

### 📨 Nastavitve MailSync {#mailsync-settings}
- `MailSync__IntervalMinutes`: Interval v minutah med sinhronizacijo e-pošte. To je globalni privzeto; vsak račun lahko to spremeni individualno na strani Ustvari/Uredi (pustite prazno, da uporabite to privzeto vrednost).
- `MailSync__FullSyncIntervalHours`: Neobvezna globalna privzeta vrednost za samodejne popolne ponovne sinhronizacije v urah. Ko ni nastavljeno (privzeto), se ne izvaja nobena samodejna popolna sinhronizacija, razen če je na strani Ustvari/Uredi nastavljena vrednost `FullSyncIntervalHours` za posamezen račun. Vrednosti za posamezen račun preprečijo to globalno privzeto vrednost.
- `MailSync__TimeoutMinutes`: Čas izteka sinhronizacije (timeout) za posamezen račun v minutah. Sinhronizacija, ki traja dlje, se ustavi pri naslednji meji sporočila, ohrani svoje kontrolne točke in nadaljuje pri naslednjem zagonu; naloga je poročena kot `Timed Out` in `LastSync` se ne napreduje. Velja le za načrtovane sinhronizacije; sinhronizacija, začeta iz uporabniškega vmesnika (ročna sinhronizacija ali popolna ponovna sinhronizacija), poteka brez časovnega omejitev. `0` (privzeto) ali katera koli nepozitivna vrednost pomeni brez časovne omejitve. Pred nadgradnjo preglejte vrednost, če sinhronizirate velike nabiralnike. Sie [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Čas izteka povezave za IMAP povezave v sekundah.
- `MailSync__CommandTimeoutSeconds`: Čas izteka ukaz za IMAP ukaze v sekundah.
- `MailSync__AlwaysForceFullSync`: Ali vedno prisilno izvajati popolno sinhronizacijo (true/false).
- `MailSync__IgnoreSelfSignedCert`: Ali ignorirati samodejavne certifikate (true/false).
- `MailSync__MaxConcurrentSyncs`: Koliko sinhronizacij računov lahko teče hkrati. Privzeto `1` (sekvencialno, navzdolno zd compatible). Programator polnila mesta, ko postanejo prosti, namesto čakanja na celotno serijo, zato ena počasna posta ne zadržuje več ostalih. Povečajte za hkratično sinhronizacijo več računov — upoštevajte omejitve hitrosti ponudnika in uporabo lokalnih virov. Sie [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Neobvezno zakasnjanje (stagger delay) v sekundah, ki se uporabi na koncu vsake naloge sinhronizacije računa. Privzeto `0` (brez zakasnitve). Uporabno za izogibanje kratkim udarcem ob začetku, ko je `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Koliko težav posamezne vrste si naloga sinhronizacije zapomni za stran računa. Neuspeli mapi, manjkajoče mape in neuspela sporočila imajo svojega proračun, zato množica ene vrste ne lahko iztisne drugih iz pogleda; vse zunaj proračuna se šteje namesto shranjevanja. Privzeto `20`. `0` izklopi podrobnosti in pusti le številke. Sie [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Mape, izključene iz sinhronizacije za **vsak** račun, poleg seznama izločenih map posameznega računa. Privzeto prazno, zato obstoječe nastavitve niso ov prisite. Oba seznama sta **dodajanja** — mapa se preskoči, če ustreva kateremu izmed njima — in oba uporabljata iste pravila ujemanja: natančno ujemanje na celotno pot, natančno ujemanje na ime mape, ujemanje z končnico poti (tako `Drafts` ujema tudi `INBOX/Drafts` in `INBOX.Drafts`) in vse pod mapo, ki jo vnos imenuje, ko je `MailSync__ExcludeSubfolders` vklopljeno. Ujemanje je neobčudno glede velikih vsakih čustev (case-insensitive). Uporabno pri uvozu številnih nabiralnikov iz istega strežnika, kjer je alternativa vzdrževanje identičnega seznama izključitev na vsakem računu. Nobena mapa ni privzeto izključena; katera imena je smiselno navesti, je odvisno od strežnika in njegovega jezika. Primer za drevo nabiralnika, ki vsebuje tudi mape koledarja in stikov:
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
- `MailSync__ExcludeSubfolders`: Ali izključni vnos pokriva tudi mape pod tisto, ki jo imenuje. Velja tako za seznam za posamezen račun kot za tistega za celo instalacijo, in za oba ponudnika. Privzeto `false`, kar ustreza vedenju, preden je možnost obstajala: vnos `Deleted Items` ujema le to mapo, drevo pa je iz arhive izločeno z navajanjem njegovega korena in vsake mape pod njim. Nastavite na `true`, da vnos zajame tudi drevo pod njim — vnos `Deleted Items` nato tudi izključi `Deleted Items/2024` in preostalo vejo iz arhive. Pravilo se navezuje na ločitev poti, zato prevzame celotne mape in nikoli dela imena: `Archive` pokrije `Archive` in `Archive/Team`, in pusti mapo z imenom `Team Archive Review` brez sprememb. Tudi `.` šteje kot ločitev, na enak način kot pravilo končnice poti obravnava to.

### 📧 Nastavitve shiranja originalnih sporočil {#original-message-storage-settings}

Privzeto arhiv ohranja le razčlenjene dele e-pošte, zato je izvožen `.eml`
rekonstrukcija: prenosi kodiranja, MIME meje in prepogibanje zaglavij se razlikujejo od
prejetega sporočila, DKIM podpisi pa ne delujejo več pravilno. Z `RawMessages:Enabled` se
hraniti posčuvano tudi celotno originalno sporočilo.

| Parameter | Default | Namen |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Shranjuj original vsake novo arhivirane e-pošte. |
| `RawMessages__Storage` | `postgres` | Kje se nahajajo bajti: `postgres` ali `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Večja sporočila se arhivirajo brez njihovega originala. `0` = ni omejitve. |
| `RawMessages__IncludeGraph` | `true` | Prav tako shranjuj originale za Microsoft 365 (Graph) račune. |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Mreža za datoteke sporočil. Mora kazati na zmontiran volumen v Dockerju. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Stopnje direktorijev, na katere je razdeljena ID-ja e-pošte. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Preveri SHA-256 ob vsakem prenosu. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Odstrani datoteke sporočil, na katere se nobena e-pošta ne nanaša. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Kako pogosto se izvede ta čistilni proces. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Datoteke, nav younger kot to, se nikoli ne odstranijo. |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Zamika pred prvim čistilnim procesom po zagonu, da se uvoz, ki še vedno piše, ne zbere. |

!!! warning "Namestitev na datoteko potrebuje zmontiran volumen"
    Z `RawMessages__Storage=filesystem` datoteke sporočil zunaj baze podatkov.
    Brez bind mounta bodo le v zapisljivem sloju kontejnerja in bodo izgubljeni ob
    naslednji ponovni ustavitvi kontejnerja, medtem ko vrstice v bazi podatkov preživijo.
    Zasem direktori s kopijo baze podatkov.

Za celoten opis poglejte **[Original Message Storage](RawMessages.md)**: kje se poslužuje
original, namesti za shranjevanje, kako premakniti obstoječa sporočila med njimi z
`--migrate-raw-storage` in kaj je treba varnostno zamejati.

### 📈 Nastavitve spremljanja {#monitoring-settings}

| Parameter | Default | Namen |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | Aktivirani računi, starejši od tega, povzročijo poročilo `/api/v1/health` `Degraded`. `0` onemogoči preverjanje. |
| `Metrics__Enabled` | `false` | Aktiviraj končnico (endpoint) za Prometheus metrike in njen zbirčni zpet (refresh loop). |
| `Metrics__RefreshMinutes` | `5` | Kako pogosto se znova zgradi posnetek (snapshot) metrik. Vrednosti pod 1 se dvignejo na 1. |

Končnici za stanje (health) in metrike zahtevajo admin API ključ in `Api__Enabled=true`. Za končnice, imena metrik, konfiguracijo Prometheus in Docker preverjanje zdravja (health check) poglejte
**[Health and Metrics](Monitoring.md)**.

### 📤 Nastavitve BatchRestore {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Število e-poštnih sporočil, ki sproži asinkrono obdelavo.
- `BatchRestore__MaxSyncEmails`: Največ number e-poštnih sporočil za sinhrono obdelavo.
- `BatchRestore__MaxAsyncEmails`: Največ number e-poštnih sporočil za asinkrono obdelavo.
- `BatchRestore__SessionTimeoutMinutes`: Čas izteka seance za batch restore v minutah.
- `BatchRestore__DefaultBatchSize`: Privzeta velikost paketa (batch size) za operacije z e-pošto.

### 🏢 Nastavitve upravljanja strank (Tenant Management) {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Največja števila poštovnih predalov, ki jih je mogoče dodati v eni operaciji upravljanja strank. Privzeto je `1000`. Povečajte to za zelo velike stranke ali to znižajte, da preprečite nenamerno masovno uvoz. Ko je limit presežen, operacija zavrne z napako pri validaciji in nobenega računa ni ustvarjeno. Za podrobnosti poglejte [M365 Tenant Import Guide](M365TenantImport.md).

### 🐄 Nastavitve Mailcow {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Največja številka poštovnih predalov, ki jih je mogoče pripraviti v enem samem procesnem ciklu Mailcow aktivacije. Privzeto je `1000`. Zmanjšajte to vrednost, da preprečite nenamerno masovno pripravljanje, ali jo povečajte za velike primere. Ko je limit presežen, se računi ne ustvarijo. Za podrobnosti poglejte [Mailcow Instances](Mailcow.md).
- Integracija Mailcow dodatno zahteva `Security__CredentialEncryptionKey` (glejte [Security Settings](#-security-settings)). Povezovanje instance in aktivacija poštovnih predalov sta zavrnjena, če je šifriranje poveril onemogočeno, zato API ključ in generirana gesla za aplikacije nikoli ne smata biti shranjena kot nešifrirana besedila.
- Za odhajajoč dostop je potreben dostop do Mailcow HTTPS API in do IMAP gostitelja prek TLS (običajno port `993`).

### 📦 Nastavitve BatchOperation {#batchoperation-settings}
- `BatchOperation__BatchSize`: Velikost paketa (batch size) za operacije z e-pošto.
- `BatchOperation__PauseBetweenEmailsMs`: Premor med posameznimi e-poštnimi sporočili v milisekundah.
- `BatchOperation__PauseBetweenBatchesMs`: Premor med paketi v milisekundah.

### 📤 Nastavitve za Offload {#offload-settings}
Nastavitve za [date-windowed offload](Offload.md). Vse privzeto vrednosti ponovijo vedenje, ki ga je aplikacija imela, preden je ta funkcija obstajala, zato namestitev, ki ne konfigurira tega dela, ni ovljena.
- `Offload__MaxConcurrentJobs`: Koliko oprav (jobs) za ponovno odpiranje (restore) ali offload lahko teče hkrati. Privzeto je `1`, kar ohranja strogo serijsko obdelavo, ki jo je vrstica oprav (job queue) vedno uporabljala. Ne glede na to vrednost teče največ ena oprav na ciljno poštovno predal (mailbox).
- `Offload__PrefetchMaxMessages`: Zgornja meja tem, koliko sporočil je indeksiranih iz ciljnega poštovnega predala za zaznavanje podvojenih sporočil. Privzeto je `500000`. Nad tem se preverjanje zozi na eno mapo in zabeleži, da je obseg bil zmanjšan.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Mapove izvora, ki se nikoli ne offloadajo, ki se ujamejo pred preimenovanjem in pokrivajo podmape. Privzeto so prazne.
- `Offload__FolderRenameMap__<SourceFolder>`: Prepiše začetne dele poti mape izvora, na primer `Offload__FolderRenameMap__Sent Items=Sent`. Privzeto je prazno.
- `Offload__MarkAsSeen`: Ali je dodana pošta označena kot prebrana. Privzeto je `true`, kar ustreza obstoječemu vedenju pri ponovnem odpiranju (restore).

Obி obe nastavitvi map sta namerno prazni: prepisovanje ali odstranjevanje map brez odpiranja bi presenetilo vsakogar, ki že uporablja pot za ponovno odpiranje (restore). Zato zagon brez konfiguracije migrirata vse, vključno s spam mapami, in ustvari drugo nabor posebnih map poleg map samega cilja.

### 📊 Nastavitve za sledenje pasovni širini {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Omogoči ali onemogoči sledenje pasovne širine za obdelavo omejitev hitrosti IMAP (true/false). Privzeto je `false`. Ko je funkcija omogočena, sistem sledi uporabi pasovne širine na posamezen račun in lahko ustavi sinhronizacijo, ko so dosežene omejitve ponudnika. Za podrobne informacije poglejte [Obdelava omejitev hitrosti](RateLimitHandling.md).
- `BandwidthTracking__DailyLimitMb`: Dnevna omejitev prenosov v megabajtih na račun. Privzeto je `25000` (25 GB). Za ponudnike z omejitvami pasovne širine nastavite to vrednost tako, da ustreza njihovi omejitvi hitrosti (npr. `2500` za ponudnike z dnevno omejitvijo okoli ~2500 MB). Sistem bo ustavil sinhronizacijo, ko bo dosežena ta omejitev.
- `BandwidthTracking__WarningThresholdPercent`: Delež dnevne omejitve, pri katerem se zabeležejo opozorilna sporočila. Privzeto je `80`. Ko uporaba pasovne širine doseže ta odstotek, se zabeležejo opozorila, da bi pomagali spremljati približevanje omejitev.
- `BandwidthTracking__PauseHoursOnLimit`: Število ur, za obdobje, ko je sinhronizacija ustavljena, ko je dosežena dnevna omejitev. Privzeto je `24`. Po tem obdobju se zastavica za omejitev samodejno izbriše in sinhronizacija nadaljuje.
- `BandwidthTracking__TrackUploadBytes`: Ali slediti tudi pasovni širini naložbe (true/false). Privzeto je `false`. Večina IMAP ponudnikov omejuje le prenose, zato to običajno ni potrebno.

### 🎯 Nastavitve izbire {#selection-settings}
- `Selection__MaxSelectableEmails`: Največ number e-poštnih sporočil, ki jih je lahko hkrati izbrati.

### 👁️ Nastavitve ogleda (Zasebnost in prikaz) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Nadzoruje privzeto vrsto pogleda e-pošte za preprečevanje sledenja in zaščito zasebnosti (true/false). Privzeto je `false`.
  - Ko je nastavljeno na `true`: E-pošta se privzeto odpre v načalu besedil (plain-text), kar preprečuje samodejno nalaganje sledenja pikselov, zunanjih slik in spletnih žarnikov. To je priporočljivo za uporabnike, ki skrbi za zasebnost in sledenje e-pošti.
  - Ko je nastavljeno na `false`: E-pošta se privzeto odpre v HTML načelu, s prikazom oblikovane e-pošte z slikami in slogi.
  - Uporabniki lahko med HTML in besedilnim prikazom preklapljajo z uporabo gumba na strani z podrobnostiami e-pošte (vidno le, ko sta na voljo oba formata).
  - Povezava "Full View" upošteva trenutno izbran način ogleda.

- `View__BlockExternalResources`: Blokira zunanje vire (oddaljene slike, zunanje CSS slogovnike, zunanje skripte, spletne police itd.) v HTML prikazih e-pošte, da prepreči sledenje in izboljša zasebnost (true/false). Privzeto je `false`.
  - Ko je nastavljeno na `true`: Zunanji viri so filtrirani pri prikazovanju HTML e-pošte. Prikazane so le vključene vsebine in data URIs (vključno z vključnimi prilogami prek `cid:` referenc).
  - Ko je nastavljeno na `false`: HTML e-pošta je prikazana z vsemi svojimi prvotnimi zunanjimi viri.
  - **Pomembno**: Ta nastavitev vpliva le na **prikaz** e-pošte. Arhivirana e-pošta je v bazi podatkov shranjena popolnoma nespremenjena, z vsemi prvotnimi vsebami.
  - Blokirani viri vključujejo:
    - Oddaljene slike (sledenje pikseli, zunanje slike, ki so shranjene na strežnikih)
    - Zunanje CSS slogovnike
    - Zunanje police prek @font-face
    - Zanje CSS uvoze prek @import
    - Zunanje slike ozadja
  - dovoljeni viri:
    - Vključene slike, vključene kot data: URIs
    - Vključene priloge, ki se nanašajo na cid: URIs
    - Vključeni CSS slogi in slogovni oznaki (style tags)
  - Ta nastavitev deluje neodvisno od `DefaultToPlainText` in zagotavlja dodatno plast zaščite zasebnosti pri ogledu HTML e-pošte.

### 📊 Nastavitve nadzornih plošč {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Ali prikazujejo kartice številčnikov svoje sestavne dele za prihajajoča in odhajajoča sporočila, kartica računa pa število različnih domen. Privzeto je `true`. Če nastavite na `false`, se te številke ne izračunajo, temveč se ne izračunajo in ne skrijejo: številčniki prikazujejo samo preprost števec, število prilog pa ne združuje več z e-pošto, na katero se nanaša, kar je ta enega od poizpitov, ki jih ti deli dodajo.
- `Dashboard__SelectablePeriods`: Ali nadzorni grafi ponujajo razpoložljivost, obdobje in puščice za njihovo premikanje. Privzeto je `true`. Če nastavite na `false`, grafi prikazujejo zadnjih dvanajst mesecev po mesecih in pošiljatelje celotnega arhive, končnica grafa odgovarja, kot da ne bi obstajala, in najstarej datum pošiljanja ni iskan.
- `Dashboard__CacheSeconds`: Kako dolgo so izračunana statistična podatki nadzorne plošče (skupni podatki s pripadajočimi razdelitvami na prihajajoča in odhajajoča sporočila, številke na račun, serije grafov, najboljši pošiljatelji, nedavne e-pošte, velikost baze podatkov) shranjeni v strežniščenem pom 기억u (in-memory cache). Privzeto je `60` sekund. Nastavite na `0`, da onemogočite predračunovanje in vedno izračunate statistiko na novo. Vsaka izbira razpoložljivosti in obdobja grafa je shranjena posebej, zato nadzorna plošča, ki se preključa med izbira, hrani en vnos za vsako izbiro. Višje vrednosti zmanjšajo obremenitev baze podatkov v velikih okoljih na račun zastarelih številk. Badgi za status sinhronizacije, vrednosti shranitve in seznam nedavnih e-pošt se vedno preizpolnjajo v živo in niso odvisni od tega predpomnilnika.
- `Dashboard__RefreshIntervalMinutes`: Kako pogosto ozadinska storitev `DashboardStatsRefreshService` ponovno izračuna zahtevne agregacije nadzorne plošče (skupni številki s pripadajočimi razdelitvami na prihajajoča in odhajajoča sporočila, številke na račun, število domen računov, privzeto serije grafov z njihovimi najboljšimi pošiljatelji, velikost baze podatkov) v tabelo baze podatkov `DashboardStatsCache`. Pri namestitvah z več milijoni e-poštnih sporočil nadzorna plošča bere te pripravljene vrednosti namesto agregacije celotne arhive v poti za zahtevo, kar prvi nalog iz ~10 sekund spremeni v milisekunde. Končnica grafa odgovarja na privzeto izbiro (meseci v obdobju enega leta, prihajajoči pošiljatelji, trenutno obdobje) iz istega vrstca, zato se ob povračitku na to izbiro prikažejo številke ob prvem risanju; vsaka druga izbira se izračuna ob zahtevi in se hrani v zgorenjem pom 기억u (in-memory cache). Privzeto je `15` minut. Nastavite na `0`, da onemogočite ozadinsko osveževanje; nadzorna plošča nato znova izračunava statistiko v živo (ob upoštevanju zgornjega pom 기억a v pom 기억u). Prikazane vrednosti lahko začenjajo za največ konfigurirovani interval zaostajati za dejansim stanjem arhive. Vrstica, izračunana z drugimi vrednostmi `ShowDirectionSplits` ali `SelectablePeriods` kot so trenutne, se ne uporabi; nadzorna plošča izračunava podatke v živo do naslednjega osvežitve.

### 🔄 Nastavitve oprav (Jobs Settings) {#jobs-settings}
- `Jobs__RefreshSeconds`: Kako pogosto se stran s strežnimi opravami (background jobs) samodejno osveži, ko je odprta v zavijdniku brskalnika. Privzeto je `30` sekund. Nastavite na `0`, da izklopite samodejno osveževanje in ga izvajali ročno. Vsako osveževanje ponovno zgradi stran iz vseh osmih virov oprav, zato pri namestitvah z veliko računov daljši interval zmanjša obremenitev, ki se мноži z vsako odprto zavihkom na strani.

### 🗃️ Nastavitve Npgsql {#npgsql-settings}
- `Npgsql__CommandTimeout`: Čas izteka za ukaze baze podatkov v sekundah.

### 📥 Nastavitve nalaganja {#upload-settings}
- `Upload__MaxFileSizeGB`: Maksimalna velikost datoteke za nalaganje v GB.
- `Upload__KeepAliveTimeoutHours`: Čas izteka (keep alive timeout) za nalaganje v urah.
- `Upload__RequestHeadersTimeoutHours`: Čas izteka za zagoglje zahtevka (request headers) v urah.

### 📂 Nastavitve lokalnega uvoza {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, itd.: Seznam dovoljenih lokalnih direktorijev, iz katerih lahko povelavke za uvoz CLI (`--import-mbox`, `--import-eml`) brljejo datoteke. Vsak vnos je pot v okviru kontejnerja. Datoteke za uvoz morate z uporabo Docker volumnov uvoziti v eno izmed teh direktorijev. Privzeto: `/data/import` (samodejno nastaveno na `/app/uploads` kot nadomeščenec).
  - Pri uporabi `docker exec` za izvedbo povelave za uvoz mora biti pot do datoteke, posredovana prek `--file`, v okviru ene izmed teh dovoljenih poti.
  - Ta varnostni ukrep preprečuje naključen dostop do datoteke sistema iz povelav CLI.
  - Za različne vire uvoza je lahko konfiguriranih več poti.
  - Za podrobna navodila za uporabo poglejte [CLI commands](CLI.md).

### 📄 Nastavitve uvoza CSV {#csv-import-settings}
- `CsvImport__MaxRows`: Največja številka vrstic CSV-ja (poštovnih predalov), obdelanih v enem paketnem uvozu. Privzeto je `5000`. Za velike namene povečajte to vrednost; znižajte jo, da omejite vpliv enega paketnega uvoza na obremenitev baze podatkov.
- `CsvImport__MaxFileSizeBytes`: Največja dovoljena velikost (v bajtih) naložene datoteke CSV. Privzeto je `10000000` (10 MB). Če je potrebno, prilagodite to vrednost svojim omejitvam nalaganja.
- Za podrobna navodila za uporabo paketnega uvoza IMAP računov prek CSV poglejte [Account Import Guide](Account-Import.md).

### 📤 Nastavitve izvoza revizijskega poročila {#audit-export-settings}
Nastavitve za stran za izvoz revizijskih podatkov (le za administratorje, dostopno s strani Logs). Izvoz generira ZIP paket z tabelnimi masovnimi podatki (INDEX.XML + CSV tabele + DTD) iz obstoječega arhive za zunanje revizijske orodje. Za podrobnosti o uporabi si oglejte [Audit Data Export Guide](AuditExport.md).
- `AuditExport__DataSupplierName`: Default vrednost za polje v obrazcu "Data supplier name", ki identificira vašo organizacijo v izvoženem indeksnem datoteki. Default: prazno.
- `AuditExport__DataSupplierLocation`: Default vrednost za polje v obrazcu "Data supplier location" (npr. sedež podjetja). Default: prazno.
- `AuditExport__Comment`: Default vrednost za polje v obrazcu "Comment", ki je vključeno kot prost tekst v izvoženi indeksni datoteki. Default: prazno.
- `AuditExport__OutputDirectory`: Direktorij, kjer so shranjene generirane ZIP datoteke. Relativne poti se razširijo glede na korenino vsebine aplikacije. Default: `exports/audit`.
- `AuditExport__RetentionDays`: Število dni, po katerih dnevno čiščenje izbriše izvožene datoteke. Default: `30`.
- `AuditExport__MaxRangeYears`: Največ dovoljen razмах med začetnim in končnim datumom obdobja izvoza. Default: `10`.
- Polja v obrazcu so predpolnila s temi predvzglavnimi vrednostmi, vendar jih je lahko treba urediti za vsak izvoz posebej.
- Vsak izvoz v dnevnik dostopa zapiše vnos o začetku in vnos o rezultatu (tip "Audit Data Export"), tako da je zgodovina varna pred revizijami brez kakršnih koli sprememb sheme DB.

### 🔒 Nastavitve politike brisanja {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Nadzoruje, ali je dovoljeno ročno brisanje arhiviranih e-poštnih sporočil (true/false). Privzeto je `true`. Ko je nastaveno na `false`:
  - Ob zagonu so vse arhivirane e-poštne pošiljke zaklenjene (`IsLocked = true`) prek sprožitev skladnosti v bazi podatkov, kar preprečuje kakršno koli spremembo ali brisanje na ravni baze podatkov.
  - Ročno brisanje (posamezno in množično) je blokirano na ravni aplikacije z besedilom o napaki.
  - Privzete vrednosti v stolpcih so prilagojene tako, da so tudi nedavno uvozenih e-poštnih sporočil zaklenjena.
  - Trenutno stanje politike se ob vsakem zagonu zabeleži v tabelo AccessLogs (na strani Logs vidno kot zapisi "Deletion Policy") za potrebe revizije.
  - Brisanje glede lokalnega obdobja shranjevanja je iz exceptions: e-poštna sporočila, ki spadajo pod konfigurirano obdobje shranjevanja, se še vedno izbrišejo (pred brisanjem v okviru postopka shranjevanja so takoj odblokirana).
- `DeletionPolicy__MaxBulkDeleteEmails`: Zgornja meja za množično brisanje na podlagi enega filtra (*Delete all matches*). Privzeto je `100000`. Filter, ki ustreza več e-poštnih sporočil, bo zavrnjen z sporočilom, ki zahteva zožanje filtra; naloga nikoli ne krati števila sporočil tiho. E-poštna sporočila, ki so zaklenjena zaradi politike shranjevanja, so preskočena in prijavljena v statusu naloge, ne odblokirana. Naloga ob začetku ponovno preveri to omejitev in `DeletionPolicy__DeletionAllowed`, zato naloga, ki je bila v vrstici pred spremembo politike, ne nadaljuje delo, temveč se ustavi, namesto da bi delala z starimi nastavitvami.
- **Zaščita nemembrabilnosti:** Ko je `IsLocked = true`, sprožitev skladnosti baze podatkov (`prevent_locked_email_changes`) blokira KAKŠNO KOLI spremembo vrstice e-poštnega sporočila — zaščitenih je vseh stolpcev, ne le določenega seznama polj. Edini iz exceptions stolpci sta `IsLocked` sama (da je odblokavanje za brisanje zaradi shranjevanja in uporabo politike ob zagonu še vedno mogoče) in `FolderName` (da lahko IMAP sinhronizacija posodobi mapo, ko je e-poštno sporočilo premeščeno na strani strežnika). Zaščita je neodvisna od stolpcev (primerjava na podlagi JSONB) in samodejno zajema prihodnje dodajanja v shemo.

### 🕐 Nastavitve časovne cone {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Časovna zona, ki se uporablja za prikaz tismov e-pošte v uporabniškem vmesniku. Uporablja identifikatorje časovnih con IANA (npr. "Europe/Berlin", "Asia/Tokyo"). Privzeto je "Etc/UCT" za združljivost z starejšimi različicami. Ob uvozu e-pošte bodo časovni žigovi pretvorjeni v to časovno cono za namene prikaza.

### 🎉 ReleaseNotes Settings (Splash zaslon za posodobitev različice) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: String različice, ki ga ta instanca prijavlja. Primerjava s GitHub release oznakami (tags) določa, ali obstaja novejša različica. Če je prazno, se changelog kot nadomestek uporabi različica kompajlirane assemblies in se opozorilo o posodobitvi izklopi.
- `ReleaseNotes__Enabled`: Omogoči ali onemogoči splash zaslon za posodobitev različice (true/false). Privzeto je `true`. Ko je omogočeno, bodo administratorji po posodobitvi aplikacije videli enkratni modalni okenj za changelog, ki prikazuje zapiske o izdaji (release notes), pridobljene s GitHub Releases za trenutno različico. Vsak administrator lahko okno zapre in ponovno se bo pojavilo le za novo različico. Nastavite na `false`, da popolnoma onemogočite to funkcijo, vključno z opozorilom o posodobitvi.
- `ReleaseNotes__UpdateHintEnabled`: Administratorjem prikaži majhno obvestilo (toast) v spodnjem desnem kotu, ko GitHub objavi novejšo stabilno različico kot je nameščena različica (true/false). Privzeto je `true`. Obvestilo je povezano z to izdajo in se v brskovalniku zapre za vsako različico posebej. Predzadki (drafts) in predrelease se prezrejo. Zahteva `ReleaseNotes__AppVersion`; brez tega se nobeno opozorilo ne prikaže.
- `ReleaseNotes__UpdateCheckIntervalHours`: Nameščeno v urah, kako pogosto se osveži seznam GitHub izdaj. Privzeto je `6`. Krajši interval povzroči, da je nova izdaja v dolgotrajno delujoči instanci hitreje vidna, daljši interval pa zmanjša število pozivov GitHub API. Minimum je `1`.
- `ReleaseNotes__RequestTimeoutSeconds`: Čas izteka (timeout) za en poziv GitHub API, v sekundah. Privzeto je `10`. Ko čas poteče, changelog in opozorilo o posodobitvi ostata izključena za ta poskus namesto tega, da bi poziv obdržali odprt; naslednji preklop bo ponovil preverjanje.

### 🔧 Nastavitve vzdrževanja baze podatkov {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Omogoči ali onemogoči samodejno dnevno vzdrževanje baze podatkov (true/false). Privzeto je `false`. Ko je omogočeno, bo sistem samodejno izvajal operacije VACUUM ANALYZE za optimizacijo zmogljivosti baze podatkov in preprečevanje zameglitve (bloat). Za več podrobnosti poglejte [Priročnik za vzdrževanje baze podatkov](DatabaseMaintenance.md).
- `DatabaseMaintenance__DailyExecutionTime`: Čas v dnevu, ko se mora začeti vzdrževanje baze podatkov, v 24-urnem formatu (HH:mm). Privzeto je `02:00`. Izberite čas med nizko aktivnostjo sistema.
- `DatabaseMaintenance__TimeoutMinutes`: Največ dovoljen čas za operacije vzdrževanja v minutah. Privzeto je `30`. Za večje baze podatkov povečajte to vrednost.

### 🧬 Nastavitve deduplikacije prilog {#attachment-deduplication-settings}
Deduplikacija prilog shrani vsako edinstveno vsebino priloge le enkrat (identificirano s SHA-256) in je **ključna funkcija, ki je vedno vključena** – namerno ni vklopljen ali izklopljen stik. Prilagoditi lahko lahko le spodnje parametre pakiranja/urnikovanja. Za celovite podrobnosti si oglejte [Attachment Deduplication Guide](AttachmentDeduplication.md).

- `AttachmentDeduplication__BatchSize`: Število obstoječih prilog, prepeljenih po transakciji med enkratno ozadinsko migracijo obstoječih podatkov. Z умолчанию je `200`. Večje vrednosti omogočajo hitrejše prepelovanje, vendar povečajo porabo pomnilnika/obremenitev baze podatkov na paketo.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Ob optionalni pavzi (v milisekundah) med paketi migracije za omejitev obremenitve baze podatkov na obremenjenih sistemih. Z умолчанию je `0` (brez pavze).
- `AttachmentDeduplication__StartupDelaySeconds`: Zamuditev (v sekundah) po zagonu aplikacije, preden se začne ozadinska migracija, da se migraciji sheme zmeni čas za zaključek. Z умолчанию je `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Interval (v urah) stalno delujočega čiščenja (garbage collection), ki odstranjuje vsebine prilog, ki jih nobena e-pošta več ne povezuje. Z умолчанию je `12`. To teče neodvisno od `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Čas izteka ukazov baze podatkov (v sekundah) za operacije pakiranja migracije (INSERT s SHA-256 hashiranjem in UPDATE). Z умолчанию je `300` (5 minut). Povečajte to vrednost za zelo velike baze podatkov ali priloge, ali jo znižajte, če želite hitrejše zaznavanje napak. Če pakete še vedno izteče čas, storitev samodejno poskuša ponovno z polovico velikosti paketa.

### 💾 Nastavitve shrambe za posamezna računa {#account-storage-settings}

Prikaz shrambe za posamezen račun prikazuje porabo baze podatkov (vsi polja e-pošte + priloge) za vsak račun v tabeli "Account Overview" v nadzornem panelu in v tabeli "Show All" v MailAccounts. Samostojna servisna procesja v ozadju (`AccountStorageRefreshService`) izračuna vrednosti tako, da izračuna PostgreSQL `pg_column_size` po vsaki koloni (pokriva vsa polja e-pošte brez branja telesa e-pošte) in jih pospravi v tabeli `AccountStorageCache`. Ta servis deluje neodvisno od `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Omogoči ali onemogoči servis za osveževanje shrambe (true/false). Privzeto je `true`. Ko je omogočen, servis ob zagonu opravi ponovljivo popolnilni proces (backfill) za preostala računa (zaščiteno pred okvarami preko `AccountStorageBackfillState`), nato pa izvaja dnevno popolno osveževanje. Ko je onemogočen, se vrednosti shrambe posodobijo le, ko so e-poštne ladje sinhronizirane, uvožene ali izbrisane, vendar se prikazane vrednosti lahko čez čas neujestremejo.
- `AccountStorage__DailyExecutionTime`: Čas v dnevu (24-urnski format `HH:mm`) za dnevno popolno osveževanje vseh računov. Privzeto je `02:30`. Izberite čas med nizko sistemsko aktivnostjo.
- `AccountStorage__BackfillDelayMs`: Zamuditev (v milisekundah) med računi med začetnim popolnilnim procesom ob zagonu. Privzeto je `5000`. Nižje vrednosti pospešijo popolnilni proces, vendar povečajo obremenitev baze podatkov; za zelo velike arhive povečajte to vrednost, da preprečite preobremenitev baze podatkov.
- `AccountStorage__RefreshBatchDelayMs`: Zamuditev (v milisekundah) med računi med dnevnim popolnim osveževanjem. Privzeto je `1000`. Nižje vrednosti pospešijo osveževanje, vendar povečajo obremenitev baze podatkov; za zelo velike arhive povečajte to vrednost.
- `AccountStorage__CommandTimeoutSeconds`: Čas izteka ukaza baze podatkov (v sekundah) za izračun shrambe za posamezen račun (`pg_column_size` nad vsemi vrsticami računa). Privzeto je `300` (5 minut). Izračun bere vsako vrstico e-pošte, vključno s TOAST podatki, kar lahko traja preštirik vrtic na zelo velikih arhivih (več kot 250k e-poštnih sporočil); prejšnja 30-sekundna privzeta vrednost Npgsql je povzročala tiho neuspšno osveževanje in povzročala, da je nadzorni panel prikazoval zastarele vrednosti. Če se izračun še vedno izteče v vašem arhivu, povečajte to vrednost.

> 💡 **Opomba**: Vrednosti shrambe se osvežijo takoj po vsaki sinhronizaciji, uvozu ali izbrisu glede na politiko ohranjanja podatkov, zato prikazane vrednosti ostanejo aktualne tudi brez dnevnega osveževanja. Dnevno osveževanje je varnostna mreža, ki zajame izjemne primere (npr. neposredne spremembe v bazi podatkov).

### 📝 Nastavitve beleženja {#logging-settings}
- `Logging__LogLevel__Default`: Privzeto raven beleženja za aplikacijo. Razpoložljive ravni so: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Privzeto je `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Raven beleženja za sporočila ok框架a ASP.NET Core. Privzeto je `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Raven beleženja za ukazov baze podatkov Entity Framework. Privzeto je `Warning`.

### 🛡️ Varnostne nastavitve {#security-settings}
- `AllowedHosts`: Seznam gostiteljskih imen, ločenih s poglavki, ki jih aplikacija lahko poslužuje. To pomaga preprečiti napade na HTTP Host glave. Primer: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Pomembno**: V produkcijskih okoljih ne uporabljajte `*`, saj to onemogoča preverjanje glave gostitelja.

#### 🔐 Šifriranje poveril ob mirovanju {#credential-encryption-at-rest}
Poverja za e-poštne račune (IMAP geslo, M365 client secret in MSA OAuth tokeni) je mogoče zašifrirati v bazi podatkov. Ključ za šifriranje se shranjuje **zunaj** baze podatkov (okoljska spremenljivka / Docker secret), zato sama kopija baze podatkov ni dovolj za branje poveril.

- `Security__CredentialEncryptionKey`: Base64 kodiran 256-bitni (32 bajta) ključ za šifriranje. Generirajte ga z:
  ```bash
  openssl rand -base64 32
  ```
  Vrednost nastavite kot okolensko spremenljivko (priporočeno: `Security__CredentialEncryptionKey` v vašem compose datoteki ali okolju kontejnerja). Aplikacija ne podpira Docker secret v slogu `_FILE` — če uporabljate Docker secret, ga montirajte in njegovo vsebino naložite v okoljsko spremenljivko v vašem entrypointu. Ključa nikoli ne objavljajte v `appsettings.json`. Ko je ključ konfiguriran, se poverila zašifrirajo ob zapisu, v ozadja proces pa enkrat zašifrirajo obstoječa poverila v obliki čistega besedila.
- `Security__EncryptAccountCredentials`: Omogoči ali onemogoči šifriranje novih/posodobljenih vrednosti poveril (true/false). Privzeto je `true`. Ko ključ ni konfiguriran, se poverila shranjujejo kot čist tekst in se ob zagonu zabeleži opozorilo — obstoječe namestitve niso ov prisilno, dokler ni zagotovljen ključ.
- `Security__CredentialEncryptionBackfill`: Ali naj bi storitev v ozadju ob zagonu zašifrirala obstoječa poverila v obliki čistega besedila (true/false). Privzeto je `true`. Če ključ ni konfiguriran, se ne izvede nothing.
- `Security__CredentialEncryptionKeyPrevious`: Ob optionalni prejšnji ključ, uporablja se le za dešifriranje vrednosti, ki so bile zašifrirane z njim (za rotacijo ključev). Nove vrednosti so vedno zašifrirane z `Security__CredentialEncryptionKey`.

  > ⚠️ **Rotacija ključev**: po rotaciji ohranite konfiguriran `Security__CredentialEncryptionKeyPrevious` **takrat, ko je vsako poverilo ponovno predelano** (npr. z urejanjem računa ali samodečno osvežitvijo OAuth tokena) — obstoječe vrednosti, zašifrirane s starim ključem, *niso* samodejno ponovno zašifrirane z novim ključem. Ko odstranite prejšnji ključ, postane vsako poverilo, ki je še zašifrirano z njim, nečitljivo in ga je treba vnesti ponovno. Dokler je konfiguriran, ga je treba skrbeti tako varno kot trenutni ključ.

> ⚠️ **Ključ shranjujte varno**: Brez ključa zašifriranih poveril ni mogoče povrniti in jih je treba vnesti ponovno. Zupam ga skupaj z (vendar ločeno od) vašimi varnostnimi kopijami baze podatkov.
>
> 💡 **Obstoječe namestitve**: Nadgradite varno — dokler ni nastavljen `Security__CredentialEncryptionKey`, se nič ne spremeni. Ključ nastavite, ko ste pripravljeni; obstoječa poverila v obliki čistega besedila ostanejo čitljiva in se bodo zašifrirala v ozadju.

### 🔐 OIDC konfiguracija {#oidc-configuration}

Za podrobna navodila za nastavitev avtentikacije OpenID Connect poglejte [OIDC Implementation Guide](OIDC_Implementation.md).

#### Osnovne OIDC nastavitve {#basic-oidc-settings}
- `OAuth__Enabled`: Omogoči ali onemogoči OIDC avtentikacijo (true/false)
- `OAuth__Authority`: URL avtoritete OpenID Connect (npr. https://sts.windows.net/{TENANT-ID}/ za Azure AD)
- `OAuth__ClientId`: ID klijenta, ki ga je dodelil vaš ponudnik identitete
- `OAuth__ClientSecret`: Tajna klijenta (client secret), ki jo je dodelil vaš ponudnik identitete
- `OAuth__DisplayName`: Izbirni izbrisni naziv, ki se prikaže na gumbu za OIDC prijavo in strani za samodejno preusmerjanje (npr. `PocketID SSO`). Če je izpuščen, se uporabi splošni oznaka "Login with OAuth".
- `OAuth__ClientScopes__0`: Prvi zahtevan scope iz ponudnika identitete (openid)
- `OAuth__ClientScopes__1`: Drugi zahtevan scope iz ponudnika identitete (profile)
- `OAuth__ClientScopes__2`: Tretji zahtevan scope iz ponudnika identitete (email)
- `OAuth__ResponseMode`: Izbirni način odziva (response mode) za zahtev za avtorizacijo (`query`, `fragment` ali `form_post`). Privzeto je `form_post`, privzeto za ASP.NET Core. Nastavite na `query` za ponudnike, ki ne podpirajo `form_post`, kot je Synology SSO Server. Poglejte [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Samodejno odobrite nove OIDC uporabnike brez potrebe po ročnem odobritvi administratorja (true/false). Privzeto je `false`.

#### Nastavitve za zagotavljanje uporabnikov (User Provisioning) {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Samodejno odobrite nove OIDC uporabnike brez potrebe po ročnem odobritvi administratorja (true/false). Privzeto je `false`. Ko je omogočeno, so uporabniki, ki se avtenticirajo prek OIDC ponudnika, takoj aktivirani in lahko dostopajo do aplikacije. Ko je onemogočeno (privzeto), so novi OIDC uporabniki ustvarjeni kot neaktivni in zahtevajo ročno aktivacijo s strani administratorja. Za podrobne informacije poglejte [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users).
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, itd.: E-poštni naslovi, ki bi morali biti samodejno zagotovljeni kot administratorji. Uporabniki z temi e-poštnimi naslovi bodo ob prvem OAuth prijavi ustvarjeni kot aktivni administratorji, s čimer ob bypassing normalnega postopka odobritve. Primerjava e-poštnih naslovov ni razlikovalna glede na velike/male črke.

#### Nastavitve za prijavo brez gesla {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Onemogoči prijavo z uporabniškim imenom/geslom (true/false). Privzeto je `false`. Ko je omogočeno, je obrazec za geslo skrit **in** strežnik zavrne vsak POST za geslo, zato je na voljo le OAuth prijava. Zahteva `OAuth__Enabled=true`. Če je OAuth onemogočen, ta nastavitev nima učinka.
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

## 🔐 Kestrel HTTPS konfiguracija (neobavezno) {#kestrel-https-configuration-optional}

Čeprav je namen aplikacije dostop do nje preko obverse proxyja z HTTPS, lahko Kestrel spletni strežnik konfigurirate tudi tako, da uporablja SSL/TLS certifikate. To zagotavlja končno šifriranje (end-to-end) med obversnim proxyjem in posameznim posode (container) aplikacije.

### Koraki konfiguracije {#configuration-steps}

1. **Generirajte ali pridobite SSL certifikat** v PFX formatu (npr. `localhost.pfx`)

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

4. **Dodajte preslikavo volumnov** za certifikat:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Namestite datoteko certifikata** (npr. `localhost.pfx`) v direktorij `./certs` na vašem gostiteljskem sistemu.

### Razlaga okoljskih spremenljivk {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: URL končnice HTTP (privzeto: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: URL končnice HTTPS (privzeto: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Pot do datoteke PFX certifikata znotraj posode
- `Kestrel__Endpoints__Https__Certificate__Password`: Geslo za datoteko PFX certifikata

> 💡 **Opomba**: Ta konfiguracija je neobavezna. Če uporabljate obversni proxy z HTTPS (priporočeno), lahko komunikacija med obversnim proxyjem in aplikacijo ostane na HTTP. Vendar pa za maksimalno varnost v občutljivih okoljih morda želite omogočiti tudi HTTPS na Kestrelu, da zašifrirate celotno pot komunikacije.

## 🔑 Upravljanje tajnosti za produkcijo {#secrets-management-for-production}

Neposredno v `docker-compose.yml` zapisovanje občutljivih podatkov, kot so gesla za baze podatkov, administratorjeva poverilnica in OAuth client secrets, predstavlja varnostno tveganje, še posebej, če je datoteka shranjena v sistemu za nadzor različic (version control). Priporočeni pristop je izpeljave teh vrednosti v datoteko `.env`, ki jo Docker Compose samodejno naloži.

### 📄 Datoteka `.env` {#the-env-file}

V isti direktorij kot vaša `docker-compose.yml` ustvarite datoteko `.env` z naslednjim vsebino:

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

**Pomembno**: Docker Compose samodejno bere datoteko `.env` iz istega direktorija — datoteke ne morate navajati ročno. Vse spremenljivke, določene v `.env`, so na voljo v `docker-compose.yml` prek sintakse `${VARIABLE_NAME}`.

### 🐳 Prilagojena `docker-compose.yml` {#adapted-docker-composeyml}

`docker-compose.yml` iz korakov za namestitev je treba prilagoditi tako, da namesto trdno zapisanih tajnosti uporabite meto (placeholders):

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

### 📋 `.env.example` predloga za administratorje {#envexample-template-for-administrators}

V svojem repozitoriju ustvarite datoteko `.env.example`, ki služi kot predloga za druge administratorje. Vsebuje vrednosti met (placeholders) in dokumentacijo, vendar **brez dejanskih tajnosti**:

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
- **Nastavite stroge dovolilniki datotek** — Omejite dostop do datoteke `.env`:
  ```bash
  chmod 600 .env
  ```
- **Uporabite močna gesla** — Generirajte dolga, naključna gesla (vsaj 16 znakov) z mešanico črn, številk in posebnih znakov.
- **Omejite dostop do datoteke `.env`** — Le uporabnik, ki izvaja Docker Compose, mora imeti dovoljilnika za branje datoteke `.env`.

## 🔒 Varnostni opomini {#security-notes}

- Uporabite močna gesla in spremenite privzete poverilnice. Gesla morajo biti dolga vsaj 12 znakov in vključevati mešanico velikih in malih črk, številk ter posebnih znakov. Izogibajte se uporabi pogostih besed ali podatkov, ki jih je enostavno ugotoviti.
- Za produkcijo upoštevajte implementacijo HTTPS z obratnim posrednikom (reverse proxy).
- Priporočljivo je redno opravljanje varnostnih kopij PostgreSQL baze podatkov. Za podrobne postopke varnostnega kopiranja in obujave poglejte [Backup and Restore Guide](BackupRestore.md).
