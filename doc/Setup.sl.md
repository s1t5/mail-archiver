# 🛠️ Navodila za namestitev Mail Archiverja {#mail-archiver-setup-guide}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodič nudi podrobna navodila za namestitev aplikacije Mail Archiver z uporabo Docker Compose.

## 🛠️ Predposemni pogoji {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Koraki namestitve {#installation-steps}

1. Na sistem namestite predposegnięte programske oprem same.

2. Ustvarite datoteko `docker-compose.yml` z naslednjim vsebino:

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

3. Uredite konfiguracijo baze podatkov v `docker-compose.yml` in nastavite varno geslo v spremenljivki `POSTGRES_PASSWORD` in `ConnectionString`.

4. Če želite uporabiti avtentikacijo (kar se močno priporoča), definirajte `Authentication__Username` in `Authentication__Password`, ki se uporabljata za uporabnika administratorja.

5. Prilagodite spremenljivo okolja `TimeZone__DisplayTimeZoneId`, da ustreza vašemu izbranemu časovnemu pasu (privzeto je "Etc/UCT"). Lahko uporabite katerikoli identifikator časovnega pasu IANA (npr. "Europe/Berlin", "Asia/Tokyo").

6. Po želji konfigurirajte spremenljivke okolja `Logging__LogLevel` za nadzor podrobnosti dnevnišnih zapisov aplikacije. Za razpoložljive možnosti poglejte spodaj razdelek Logging Settings.

7. Konfigurirajte obratni proxy po svoji izbiri z https in avtentikacijo za zagotovitev varnega dostopa do aplikacije.

> ⚠️ **Pozor**: Sama aplikacija ne zagotavlja zaščitenega dostopa prek https! Ta mora biti nastavljen prek obratnega proxyja!

8. Prvi zagon kontejnerjev:
```bash
docker compose up -d
```

9. Ponovni zagon kontejnerjev:
```bash
docker compose restart
```

10. Dostop do aplikacije

11. Prijavite se s svojimi definiranimi podatki in dodajte svoje prvo e-poštno račun:
   - Pojdite v razdelek "Email Accounts"
   - Kliknite "New Account"
   - Vnesite podrobnosti o strežniku in svoje legitimacije
   - Shranite in začnite z arhiviranjem!
   - Če želite, ustvarite druge uporabnike in jim dodelite račune.

## 📚 Razlaganja okoljskih spremenljivk {#environment-variable-explanations}

### 🗄️ Povezava z bazo podatkov {#database-connection}
- `ConnectionStrings__DefaultConnection`: Povezovalni niz do PostgreSQL baze podatkov. Po potrebi spremenite vrednosti `Host`, `Database`, `Username` in `Password`.

### 🔐 Nastavitve avtentikacije {#authentication-settings}
- `Authentication__Username`: Uporabniško ime za administrativni račun.
- `Authentication__Password`: Geslo za administrativni račun.
- `Authentication__SessionTimeoutMinutes`: Čas izteka seance v minutah. To velja za ne-persistentni klepetalni datoteka (cookie) za prijavo; uporabniki, ki ob prijavi označijo **Remember Me**, prejmejo persistentni klepetalni datoteka, ki velja 7 dni. Več informacij na [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: Ime klepetalne datotke za avtentikacijo.
- `Authentication__CookieSameSite`: Konfigurira atribut SameSite za klepetalne datotke za avtentikacijo, seanco in zaščito pred CSRF. Veljavne vrednosti so:
  - `Strict` (default): Maksimalna varnost. Klepetalni datotke se pošljejo le ob zahtevah iste strani (same-site). To lahko povzroči težave pri navigaciji v aplikacijo iz zunanjih povezav (npr. klik na povezavo z druge spletne strani), saj obstoječa seanca ne bo prepoznana.
  - `Lax`: Priporočljivo ob uporabi obversenega proxyja (reverse proxy). Klepetalne datotke se pošljejo ob navigacijah na najvišji ravni (top-level) in zahtevah iste strani, kar uporabnikom omogoča sledenje zunanjim povezavam do aplikacije, hkrati pa ohranja zaščito pred CSRF za POST zahteve.
  - `None`: Klepetalne datotke se pošljejo pri vseh zahtevah. Zahteva HTTPS in atribut `Secure`. Uporabite to samo, če imate specifične zahteve glede medstranskega komuniciranja (cross-site) in razumete varnostne posledice.

### 🔌 Nastavitve REST API {#rest-api-settings}
Neobvezen REST API v okolju samo za branje je **privzeto onemogočen**. Za celoten priročnik poglejte [vodnik za REST API](API.md).
- `Api__Enabled`: Glavno stišilo za REST API v okolju samo za branje (privzeto `false`). Ko je `false`, vsi `/api/*` poti vrnejo `404`.
- `Api__AllowAttachmentDownloads`: Dovoli prenos bajt prilog prek API-ja (privzeto `true`). Ko je `false`, kontejner za priloge vrne `403`.
- `Api__AllowMessageDownloads`: Dovoli prenos celotnih sporočil prek API-ja, tako zgrajenega `/eml` kot izvirnega `/raw` (privzeto `true`). Ko je `false`, oba kontejnerja vrneta `403`. Celotno sporočilo vsebuje svoje priloge, zato morata biti **obe** to stišilo in `Api__AllowAttachmentDownloads` nastavljena na `true`; izklop enega od obeh onemogoči oba kontejnerja.
- `Api__EnableSwaggerUi`: Razpoložljivost Swagger UI na `/apidocs` in OpenAPI dokumenta na `/apidocs/spec/v1.json` (privzeto `true`). Oba zahtomega prijavljeno brzalno sesijo v brskalniku.
- `Api__DefaultPageSize`: Privzeta velikost strani za kontejnerje za sezname, ko ni navedena (privzeto `20`).
- `Api__MaxPageSize`: Največja dovoljena velikost strani; večji zahtevki bodo omejeni (privzeto `100`).
- `Api__RateLimitPerMinute`: Budžet zahtev v določenem oknu na ključa API na minuto (privzeto `120`).

### 🤖 Nastavitve MCP strežnika {#mcp-server-settings}
Neobvezni MCP (Model Context Protocol) strežnik omogoča AI agentom isti dostop do e-pošte v samo branju kot REST API prek Streamable HTTP prenosa. **Privedoma je onemogočen**. Za celoten pregled poglejte [vodnik MCP](MCP.md). MCP ponovno uporablja prav tiste API ključe kot REST API (`ma_...` nosilce žetonov) in isto omejevanje dostopa na uporabniško raven.
- `Mcp__Enabled`: Glavno stišilo za MCP strežnik (privzeto `false`). Ko je `false`, končnica `/mcp` vrne `404`.
- `Mcp__AllowAttachmentDownloads`: dovoli orodju `get_attachment` vrniti bajte priloge (privzeto `true`). Ko je `false`, orodje vrne napako.
- `Mcp__DefaultPageSize`: Privzeto število vrsti, ki ga uporablja `search_emails`, ko klicatelj izpusti `pageSize` (privzeto `20`).
- `Mcp__MaxResults`: Zgornja meja za število vrsti za `search_emails`; večje vrednosti bodo omejene (privzeto `100`).
- `Mcp__MaxAttachmentBytes`: Največja velikost priloge (v bajtih), ki jo bo orodje `get_attachment` vrnilo neposredno kot base64; večje priloge bodo zavrnjene (privzeto `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Dovoljaj orodjem `get_email_eml` in `get_email_raw`, da vrneta celotna sporočila (privzeto `true`). Ko je `false`, orodja vrnejo napako. Celovito sporočilo vsebuje svoje priloge, zato morata biti **obe** to stišilo in `Mcp__AllowAttachmentDownloads` nastavljena na `true`; če izklopite katero izmed njima, bosta oba orodja onemogočena.
- `Mcp__MaxMessageBytes`: Največja velikost sporočila (v bajtih), ki jo bosta orodja `get_email_eml` in `get_email_raw` vrnila neposredno; večja sporočila bodo zavrnjena (privzeto `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Budžet zahtevov v določenem časovnem oknu na API ključ na minuto (privzeto `120`).

### 📨 Nastavitve MailSync {#mailsync-settings}
- `MailSync__IntervalMinutes`: Interval v minutah med sinhronizacijo e-pošte. To je globalni privzeto; vsako račun lahko posamezno prekriva na strani Ustvari/Uredi (pustite prazno, da uporabite to privzeto vrednost).
- `MailSync__FullSyncIntervalHours`: Nepreobvezujoča globalna privzeta vrednost za samodejne popolne ponovne sinhronizacije v urah. Če ni nastavljena (privzeto), se ne izvaja nobena samodejna popolna sinhronizacija, razen če je na strani Ustvari/Uredi nastavljena vrednost `FullSyncIntervalHours` za posamezen račun. Vrednosti za posamezen račun prekrivajo to globalno privzeto vrednost.
- `MailSync__TimeoutMinutes`: Čas izteka sinhronizacije za posamezen račun v minutah. Sinhronizacija, ki teče dlje, se ustavi pri naslednji meji sporočila, ohrani svoje kontrolne točke in nadaljuje pri naslednjem zagonu; naloga je poročena kot `Timed Out` in `LastSync` se ne napreduje. Velja samo za načrtovane sinhronizacije; sinhronizacija, začeta iz uporabniškega vmesnika (ročna sinhronizacija ali popolna ponovna sinhronizacija), teče brez časovnega omejitve. `0` (privzeto) ali katera koli nepozitivna vrednost pomeni brez časovnega omejitve. Pred nadgradnjo preglejte vrednost, če sinhronizirate velike nabiralnike. Poglejte [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Čas izteka povezave za IMAP povezave v sekundah.
- `MailSync__CommandTimeoutSeconds`: Čas izteka ukaz za IMAP ukaze v sekundah.
- `MailSync__AlwaysForceFullSync`: Ali želite vedno prisiliti popolno sinhronizacijo (true/false).
- `MailSync__IgnoreSelfSignedCert`: Ali želite ignorirati samodejan podpisane certifikate (true/false).
- `MailSync__MaxConcurrentSyncs`: Koliko računov lahko teče hkrati. Privzeto `1` (zaporedno, primano nazaj). Programator ponovno napolni prosta mesta, ko ta postanejo prosti, namesto da bi čakal na celotno paket, zato en počasen nabiralnik ne zadržuje več ostalih. Povečajte za hkrati sinhronizacijo več računov — upravljajte z omejitvami števila zahtev ponudnika in lokalno porabo virov. Poglejte [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Nepreobvezujoča zakasnitve razmika v sekundah, ki se uporabi na koncu vsake naloge sinhronizacije računa. Privzeto `0` (brez zakasnitve). Uporabno za izogibanje kratkim udarom obremenitev, ko je `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Koliko težav vsakega tipo si naloga sinhronizacije zapomni za stran računa. Neuspešni mapi, manjkajoče mape in neuspešna sporočila imajo ločeno proračun, zato množica ene vrste ne more iztorniti drugih iz pregleda; vse zraven proračuna se šteje namesto tega, da bi se hranilo. Privzeto `20`. `0` izklopi podrobnosti in pusti le številce. Poglejte [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Mape, izključene iz sinhronizacije za **vsak** račun, poleg lastne seznama izločenih map vsakega računa. Privzeto prazno, zato obstoječe nastavitve niso vplivane. Oba seznama sta **dodajena** — mapa je preskočena, ko ustreza kateremu koli izmed njima — in oba uporabljata iste pravila ujemanja: natančno ujemanje celotne poti, natančno ujemanje imena mape, ujemanje z sufiksom poti (tako da `Drafts` ujema tudi `INBOX/Drafts` in `INBOX.Drafts`) in vse pod mapo, ki jo vnos navaja, ko je `MailSync__ExcludeSubfolders` vklopljen. Ujemanje je ne glede na velikost čust).(case-insensitive). Uporabno pri uvozu številnih nabiralnikov z istega strežnika, kjer je alternativa vzdrževanje identičnega seznama izključitev za vsak račun. Nobena mapa ni privzeto izključena; to, katere imenice je vredno navesti, je odvisno od strežnika in njegovega jezika. Primer za drevo nabiralnika, ki vsebuje tudi mape za kalendar in kontakte:
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
- `MailSync__ExcludeSubfolders`: Ali vnos za izključitev pokriva tudi mape pod tisto, ki jo navaja. To velja tako za seznam za posamezen račun kot za tistega po celotni namestitvi, in za oba ponudnika. Privzeto `false`, kar ustreza vedenju, preden je možnost obstajala: vnos `Deleted Items` ujema le to mapo, in drevo je iz arhive izključeno z navedbo njegove korenine in vsake mape pod njo. Nastavite na `true`, da vnos objame tudi drevo pod njim — vnos `Deleted Items` nato tudi zadrži `Deleted Items/2024` in preostalo vejo zunaj arhive. Pravilo se uskloni na ločilec poti, zato prevzame cele mape in nikoli dela imena: `Archive` pokrije `Archive` in `Archive/Team`, in pusti mapo e-pošte z imenom `Team Archive Review` brez sprememb. Tudi `.` šteje kot ločilnik, na enak način kot ga obravnava pravilo sufiksa poti.

### 📧 Nastavitve shराणjenja originalnih sporočil {#original-message-storage-settings}

Privzeto arhiv shrani le razdeljene dele e-pošte, zato je izvoženi `.eml`
rekonstrukcija: kodiranja prenosa, MIME meje in prepogib glavič (header folding) se razlikujejo od
prejetega sporočila, DKIM podpisi pa niso več validni. Z `RawMessages:Enabled` se
shrani tudi celotno originalno sporočilo.

| Parameter | Default | Namen |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Shranjuj original vsake nove arhivirane e-pošte. |
| `RawMessages__Storage` | `postgres` | Kje se bajti nahajajo: `postgres` ali `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Večja sporočila se arhivirajo brez njihovega originala. `0` = brez omejitve. |
| `RawMessages__IncludeGraph` | `true` | Shranjuj tudi originale za Microsoft 365 (Graph) račune. |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Mreža za datoteke sporočil. Mora kazati na montiran volumen v Dockeru. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Stopnje direktorijev, na katere je razdeljen id e-pošte. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Preveri SHA-256 ob vsakem prenosu. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Odstrani datoteke sporočil, ki jih nobena e-pošta ne povezuje. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Kako pogosto se izvaja čiščenje. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Datoteke, ki so mlajše od tega, se nikoli ne odstranijo. |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Zamuditev pred prvim čiščenjem po zagonu, da se uvoz, ki še teče, ne zbere. |

!!! warning "Filesystem backend potrebuje montiran volumen"
    Z `RawMessages__Storage=filesystem` datoteke sporočil živijo zunaj baze podatkov.
    Brez bind montaže bodo stale v zapisljivem sloju kontejnerja in bodo izgubljene ob
    naslednji ponovni izdelavi kontejnerja, medtem ko vrstki v bazi podatkov preživijo.
    Izvlečite varnostno kopijo direktorija skupaj z izvozom baze podatkov.

Za celoten opis poglejte **[Original Message Storage](RawMessages.md)**: kje se streže original,
hranilni sistemi, kako premakniti obstoječa sporočila med njimi z `--migrate-raw-storage`
in kaj je treba varnostno kopirati.

### 📈 Nastavitve spremljanja {#monitoring-settings}

| Parameter | Default | Namen |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | Aktivirani računi, ki so starejši od tega, povzročijo `/api/v1/health` poročilo `Degraded`. `0` onemogoči preverjanje. |
| `Metrics__Enabled` | `false` | Omogoči kontejner za metrike Prometheus in njegov zvočni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zmanjševalni zloop osveževanja. |
| `Metrics__RefreshMinutes` | `5` | Kako pogosto se znova zgradi posnetek metrik. Vrednosti pod 1 se dvignejo na 1. |

Kontejnerja za stanje (health) in metrike zahtevata API ključ administratorja in `Api__Enabled=true`. Lastna preverba življenja (liveness check) kontejnerja ne zahteva avtentikacije in ne potrebuje konfiguracije. Za kontejnerje, imena metrik, konfiguracijo Prometheus in Docker preverjanje zdravja poglejte **[Health and Metrics](Monitoring.md)**.

### 📤 Nastavitve BatchRestore {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Število e-poštnih sporočil, ki sproži asinkrono obdelavo.
- `BatchRestore__MaxSyncEmails`: Maksimalno število e-poštnih sporočil za sinhrono obdelavo.
- `BatchRestore__MaxAsyncEmails`: Maksimalno število e-poštnih sporočil za asinkrono obdelavo.
- `BatchRestore__SessionTimeoutMinutes`: Čas izteka sešnje za obnavljanje serije (batch restore) v minutah.
- `BatchRestore__DefaultBatchSize`: Privzeta velikost serije (batch size) za operacije z e-pošto.

### 🏢 Nastavitve upravljanja z zakupami (Tenant Management) {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Največje število poštovnih predalov, ki jih je mogoče dodati v enostopen operaciji upravljanja z zakupami. Z умолчанию je `1000`. Povečajte to vrednost za zelo velike zakupnike ali jo zmanjšajte, da preprečite nenamerne masovne uvoze. Ko je limit presežen, operacija zavrne z napako validacije in računi niso ustvarjeni. Za podrobnosti si oglejte [M365 Tenant Import Guide](M365TenantImport.md).

### 🐄 Nastavitve Mailcow {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Največja številka poštovnih predalov, ki jih je mogoče pripraviti v enem samotnem pogoju aktivacije Mailcow. Privzeto je `1000`. Zmanjšajte to vrednost, da preprečite nenamerno masovno pripravljanje, ali jo povečajte za velike primere. Ko je limit presežen, se računi ne ustvarijo. Za podrobnosti po see [Mailcow Instances](Mailcow.md).
- Integracija Mailcow dodatno zahteva `Security__CredentialEncryptionKey` (glejte [Security Settings](#-security-settings)). Povezovanje primera in aktivacija poštovnih predalov sta zavrnjenih, če je šifriranje poveril onemogočeno, zato se API ključ in generirana gesla aplikacij ne smeta nikoli shraniti kot besedilna.
- Za odhodni dostop je potreben Mailcow HTTPS API in IMAP gostitelj prek TLS (običajno port `993`).

### 📦 Nastavitve BatchOperation {#batchoperation-settings}
- `BatchOperation__BatchSize`: Velikost paketa (batch size) za operacije z e-pošto.
- `BatchOperation__PauseBetweenEmailsMs`: Premor med posameznimi e-poštnimi sporočili v milisekundah.
- `BatchOperation__PauseBetweenBatchesMs`: Premor med paketi v milisekundah.

### 📤 Nastavitve za Offload {#offload-settings}
Nastavitve za [date-windowed offload](Offload.md). Vse privzeto vrednosti ponovijo vedenje, ki ga je aplikacija imela, preden je ta funkcija obstajala, zato namestitev, ki ne konfigurira tega dela, ni vplivana.
- `Offload__MaxConcurrentJobs`: Koliko oprav za ponovno okrevanje (restore) ali prenos (offload) lahko teče hkrati. Privzeto je `1`, kar ohranja strogo serijsko obdelavo, ki jo je vrstica oprav vedno uporabljala. Ne glede na to vrednost teče največ ena oprav na ciljno poštovno predalo.
- `Offload__PrefetchMaxMessages`: Zgornja meja tem, koliko sporočil je indeksiranih iz ciljnega poštovnega predala za zaznavanje podvojžev. Privzeto je `500000`. Nad tem se preverjanje zožji na eno mapo in se zabeleži, da je obseg zmanjšan.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Mapice izvira, ki se nikoli ne prenesejo (offload), ki se ujamejo pred preimenovanjem in pokrivajo podmape. Privzeto so prazne.
- `Offload__FolderRenameMap__<SourceFolder>`: Prepiše začetne dele poti mapice izvira, na primer `Offload__FolderRenameMap__Sent Items=Sent`. Privzeto je prazno.
- `Offload__MarkAsSeen`: Ali je dodano pošto označeno kot prebrano. Privzeto je `true`, kar ustreza obstoječemu vedenju pri ponovnem okrevanju (restore).

Obema nastavitvama map je namerno prazni: prepisovanje ali izbrisovanje map brez povzdignja zahteve bi presenetilo vsakogar, ki že uporablja pot za ponovno okrevanje (restore). Zato zvoženje brez konfiguracije migrirá vse, vključno z mapami s smetmi (spam), in ustvari ikinci nabor posebnih map poleg tistih v ciljnem predalu.

### 📊 Nastavitve sledenja širini pasovnega hlaščanja {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Omogoči ali onemogoči sledenje širini pasovnega hlaščanja za obravnavo omejitev hitrosti IMAP (true/false). Privzeto je `false`. Ko je funkcija omogočena, sistem sledi uporabi širine pasovnega hlaščanja na račun in lahko ustavi sinhronizacijo, ko so dosežene omejitve ponudnika. Za podrobne informacije poglejte [Obravnavanje omejitev hitrosti](RateLimitHandling.md).
- `BandwidthTracking__DailyLimitMb`: Dnevna omejitev prenosov v megabitih na račun. Privzeto je `25000` (25 GB). Za ponudnike z omejitvami širine pasovnega hlaščanja to nastavite tako, da ustreza njihovi omejitvi hitrosti (npr. `2500` za ponudnike z dnevno omejitvijo okoli ~2500 MB). Sistem bo ustavil sinhronizacijo, ko bo dosežena ta omejitev.
- `BandwidthTracking__WarningThresholdPercent`: Delež dnevne omejitve, pri katerem se zabeležejo opozorilna sporočila. Privzeto je `80`. Ko uporaba širine pasovnega hlaščanja doseže ta odstotek, se zabeležijo opozorila, da bi pomagala spremljati približevanje omejitve.
- `BandwidthTracking__PauseHoursOnLimit`: Število ur za ustavitev sinhronizacije, ko je dosežena dnevna omejitev. Privzeto je `24`. Po tem obdobju se zastavica omejitve samodejno izbriše in sinhronizacija se nadaljuje.
- `BandwidthTracking__TrackUploadBytes`: Ali tudi slediti širini pasovnega hlaščanja za nalaganje (true/false). Privzeto je `false`. Večina IMAP ponudnikov omejuje le prenose (download), zato to običajno ni potrebno.

### 🎯 Nastavitve izbire {#selection-settings}
- `Selection__MaxSelectableEmails`: Najvišja števila e-poštnih sporočil, ki jih je mogoče izbrati hkrati.

### 👁️ Nastavitve ogleda (Zasebnost in prikaz) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Nadzoruje privzeto vrsto pogleda e-pošte za zasebnost in preprečevanje sledenja (true/false). Privzeto je `false`.
  - Ko je nastavljeno na `true`: E-pošta se privzeto odpre v besedilnem (plain-text) pogledu, kar preprečuje samodejno nalaganje sledenjskih pikselov, zunanjih slik in spletnih žarторов. To je priporočljivo za uporabnike, ki skrbi zasebnost in sledenje e-pošte.
  - Ko je nastavljeno na `false`: E-pošta se privzeto odpre v HTML pogledu, s prikazom oblikovane e-pošte z slikami in slogovanjem.
  - Uporabniki lahko med HTML in besedilnim pogledom preklapljajo z gumbom na strani s podrobnosti o e-pošti (vidno le, ko sta na voljo oba formata).
  - Povezava "Full View" upošteva trenutno izbran način pogleda.

- `View__BlockExternalResources`: Blokira zunanje vire (oddaljene slike, zunanje CSS datoteke, zunanje skripte, spletne police itd.) v HTML pregledih e-pošte, da bi preprečili sledenje in izboljšali zasebnost (true/false). Privzeto je `false`.
  - Ko je nastavljeno na `true`: Zunanji viri so pri prikazovanju HTML e-pošte filtrirani. Prikazane so le vključene (inline) vsebine in data URIs (vključno z vključnimi prilogami prek `cid:` referenc).
  - Ko je nastavljeno na `false`: HTML e-pošta se prikazuje z vsemi originalnimi zunanjimi viri.
  - **Pomembno**: Ta nastavitev vpliva le na **prikaz** e-pošte. Arhivirana e-pošta je v bazi podatkov shranjena popolnoma nespremenjena, z ohranjeno celotno originalno vsebino.
  - Blokirani viri vključujejo:
    - Oddaljene slike (sledenjski pikseli, zunanje slike, shranjene na strežnikih)
    - Zunanje CSS slogovnike
    - Zunanje police prek @font-face
    - Zanje CSS uvoze prek @import
    - Zunanje slike za ozadje
  - dovoljeni viri:
    - Vključene slike, vključene kot data: URIs
    - Vključene priloge, navedene prek cid: URIs
    - Vključeni CSS slogi in slogovni oznaki (style tags)
  - Ta nastavitev deluje neodvisno od `DefaultToPlainText` in zagotavlja dodatno plast zaščite zasebnosti pri ogledu HTML e-pošte.

### 📊 Nastavitve nadzornih plošč {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Ali kartice številnikov vključujejo svoje dele za prejem in pošiljanje ter ali kartica računa prikazuje število različnih domen. Privzeto je `true`. Če nastavite na `false`, se te številke ne izračunavajo, temveč so namesto izračunavanja skrite: številniki prikazujejo preprost znesek, število prilog pa ne več povezuje z e-pošto, pri kateri se nahaja, kar je edino poizvedbo, ki jo ti deli dodajo.
- `Dashboard__SelectablePeriods`: Ali nadzornim ploščam v grafikonih ponujajo ločljivost, obdobje in puščice za premikanje. Privzeto je `true`. Če nastavite na `false`, grafikon prikazuje zadnjih dvanajst mesecev po mesecih in pošiljatelje celotnega arhive; končna točka grafilika odgovarja, kot da ne bi obstajala, in najstarejši datum pošiljanja ni iskan.
- `Dashboard__CacheSeconds`: Kako dolgo so izračunani statistični podatki nadzorne plošče (skupni zneski s splitami na prejem in pošiljanje, število po vsakem računu, serije grafikonov, najpogostejši pošiljatelji, nedavne e-pošte, velikost baze podatkov) shranjeni v pomembni cache-u strežnika. Privzeto je `60` sekund. Nastavite na `0`, da onemogočite shranjevanje v cache-u in vedno ponovno izračunate statistiko. Vsaka izbira ločljivosti in obdobja grafilika je shranjena posebej, zato nadzorna plošča, ki se preklipa med izbiralniki, hrani en zapis za vsako izbiro. Višje vrednosti zmanjšajo obremenitev baze podatkov v velikih okoljih na račun bolj zastarelih številk. Pripadki statusa sinhronizacije, vrednosti shranjevanja in seznam nedavnih e-pošt se vedno pridobijo v živo in niso vplivni na ta cache.
- `Dashboard__RefreshIntervalMinutes`: Nameжко pogosto ponovno izračuna `DashboardStatsRefreshService` ozadja storitev dragocene agregacije nadzorne plošče (skupni zneski s spliti na prejem in pošiljanje, število po vsakem računu, število domen računov, privzeto serije grafikonov z najpogostejšimi pošiljatelji, velikost baze podatkov) v tabelo v bazi podatkov `DashboardStatsCache`. Pri namestitvah z več milijoni e-poštnih sporočil nadzorna plošča namesto agregacije celotnega arhive v poti zahteve bere te pripravljene vrednosti, kar prvi nalaganje iz ~10 sekund spremeni v milisekunde. Končna točka grafilika odgovarja na privzeto izbiro (meseci v obdobju enega leta, prejemniki, trenutno obdobje) iz iste vrstice, zato se ob povratku na to izbiro prikažejo številke ob prvem risanju; vsaka druga izbira se izračuna ob zahtevi in ostane v zglednem cache-u v pomembnosti. Privzeto je `15` minut. Nastavite na `0`, da onemogočite posodobitve v ozadju; nadzorna plošča nato ponovno izračunava statistiko v živo (z še vedno veljavnim cache-om v pomembnosti zgoraj). Prikazane vrednosti lahko za obdobje, ki je konfigurirano, zaostajajo za dejanim stanjem arhive. Vrstica, izračunana z drugimi vrednostmi `ShowDirectionSplits` ali `SelectablePeriods` kot so trenutne, se ne uporabi; nadzorna plošča izračunava v živo do naslednje osvežitve.

### 🔄 Nastavitve opravil {#jobs-settings}
- `Jobs__RefreshSeconds`: Nameščeno na to, kako pogosto se stran s pogosničnimi opravili samodejno osvežuje, ko je odprta v zavihku brskalnika. Privzeta vrednost je `30` sekund. Nastavite na `0`, da izklopite samodejno osveževanje in ga izvajali ročno. Vsako osveževanje ponovno zgradi stran iz vseh enajst virov opravil, zato na namestitvah z veliko računov daljši interval zmanjša obremenitev, ki se мноžimo z vsakim odprtim zavihkom na strani.

### 🗃️ Nastavitve Npgsql {#npgsql-settings}
- `Npgsql__CommandTimeout`: Čas izteka (timeout) za ukaze baze podatkov v sekundah.

### 📥 Nastavitve nalaganja {#upload-settings}
- `Upload__MaxFileSizeGB`: Maksimalna velikost datoteke za nalaganje v GB.
- `Upload__KeepAliveTimeoutHours`: Čas izteka (keep alive timeout) za nalaganje v urah.
- `Upload__RequestHeadersTimeoutHours`: Čas izteka za zaglavetke zahtevka v urah.

### 📂 Nastavitve lokalnega uvoza {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, itd.: Bela lista lokalnih direktorijev, iz katerih lahko CLI ukazani za uvoz (`--import-mbox`, `--import-eml`) brali datoteke. Vsak vpis je pot znotraj kontejnerja. Datoteke za uvoz morate z uporabo Docker volumnov uvoziti v enega izmed teh direktorijev. Privzeto: `/data/import` (samodejno nastavljeno na `/app/uploads` kot nadomeščavo).
  - Pri uporabi `docker exec` za izvedbo ukaza za uvoz mora biti pot do datoteke, posredovana prek `--file`, znotraj ene izmed teh dovoljenih poti.
  - Ta varnostni ukrep preprečuje nedovoljen dostop do naključnih datotekovnih sistemov iz ukazov CLI.
  - Za različne vire uvoza je lahko konfiguriranih več poti.
  - Za podrobna navodila za uporabo poglejte [CLI ukazi](CLI.md).

### 📄 Nastavitve uvoza CSV {#csv-import-settings}
- `CsvImport__MaxRows`: Največ number vrstic CSV-ja (poštetnih predal), obdelanih v enem posameznem massovnem uvozu. Privzeta vrednost je `5000`. Za velike razpoložljivosti povečajte to vrednost; znižajte jo, da omejite vpliv enega izvedenega uvoza na obremenitev baze podatkov.
- `CsvImport__MaxFileSizeBytes`: Največja dovoljena velikost (v bajtih) naložene datoteke CSV. Privzeta vrednost je `10000000` (10 MB). Če je potrebno, prilagodite to vrednost svojim omejitvam nalaganja.
- Za podrobne navodila za uporabo massovnega uvoza IMAP računov prek CSV poglejte [Account Import Guide](Account-Import.md).

### 📤 Nastavitve izvoza revizijskega poročila {#audit-export-settings}
Nastavitve za stran za izvoz revizijskih podatkov (samo za administratorje, dostopno s strani Logs). Izvoz generira ZIP paket z tabularnimi masovnimi podatki (INDEX.XML + CSV tabele + DTD) iz obstoječega arhiv za zunanje revizijske orodje. Za podrobnosti o uporabi poglejte [Priročnik za izvoz revizijskih podatkov](AuditExport.md).
- `AuditExport__DataSupplierName`: Privzeto vrednost za polje obrazca "Data supplier name", ki identificira vašo organizacijo v izvoženem indeksnem datoteki. Privzeto: prazno.
- `AuditExport__DataSupplierLocation`: Privzeto vrednost za polje obrazca "Data supplier location" (npr. sedež podjetja). Privzeto: prazno.
- `AuditExport__Comment`: Privzeto vrednost za polje obrazca "Comment", ki je vključeno kot prost tekst v izvoženo indeksno datoteko. Privzeto: prazno.
- `AuditExport__OutputDirectory`: Mesto (directory), kjer so shranjene generirane ZIP datoteke. Relativne poti se razrešujejo glede na korenino vsebine aplikacije. Privzeto: `exports/audit`.
- `AuditExport__RetentionDays`: Številčilo dni, po katerih dnevno čiščenje izbriše izvožene datoteke. Privzeto: `30`.
- `AuditExport__MaxRangeYears`: Največ dovoljen časovni razpon med začetnim in končnim datumom obdobja izvoza. Privzeto: `10`.
- Polja obrazca so predpolnjena s temi privzetimi vrednostmi, vendar jih je lahko treba urediti za vsak izvoz posebej.
- Vsak izvoz v dostopni dnevnik zapisuje vnos o začetku in vnos o rezultatu (tip "Audit Data Export"), tako da je zgodovina varno shranjena brez kakršnih koli sprememb sheme baze podatkov.

### 🔒 Nastavitve politike brisanja {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Nadzoruje, ali je dovoljeno ročno brisanje arhiviranih e-poštnih sporočil (true/false). Privzeto je `true`. Ko je nastavljeno na `false`:
  - Ob zagonu so vse arhivirane e-poštne pošte zaklenjene (`IsLocked = true`) prek sproževalca skladnosti baze podatkov (database compliance trigger), kar preprečuje kakršna koli spremembo ali brisanje na ravni baze podatkov.
  - Ročno brisanje (posamezno in množično) je blokirano na ravni aplikacije z besedilom o napaki.
  - Privzena vrednost v stolpcih je prilagojena tako, da so tudi novovhodne e-poštne pošte zaklenjene.
  - Trenutno stanje politike se ob vsakem zagonu zabeleži v tabeli AccessLogs (vidno na strani Logs kot zapisi "Deletion Policy") za potrebe revizije.
  - Brisanje glede lokalnega zadržanja (retention) je iz exceptions: e-poštna sporočila, ki spadajo pod nastavljeno obdobje zadržanja, se še vedno izbrišejo (pred brisanjem v procesu zadržanja so takoj odblokirana).
- `DeletionPolicy__MaxBulkDeleteEmails`: Zgornja meja za eno množično brisanje na podlagi filtra (*Delete all matches*). Privzeto je `100000`. Filter, ki ustreza več e-poštnih sporočil, bo zavrnjen z sporočilom, ki prosi za zoбščitev filtra; naloga nikoli ne obrezuje podatkov tiho. E-poštna sporočila, zaklenjena s politiko zadržanja, so preskočena in poročena v statusu naloge, ne pa odblokirana. Naloga ob zagonu ponovno preveri to omejitev in `DeletionPolicy__DeletionAllowed`, zato se naloga, ki je bila v vrsti še pred spremembo politike, ustavi namesto tega, da bi se izvedla s starimi nastavitvami.
- **Zaščita neupravičenega spreminjanja (Immutability protection):** Ko je `IsLocked = true`, sproživalec skladnosti baze podatkov (`prevent_locked_email_changes`) blokira KAKŠNO KOLI spremembo vrstice e-poštnega sporočila — zaščiteni so vsi stolpci, ne le seznam določenih polj. Edini iz exceptions stolpci sta `IsLocked` sama (tako da ostane mogoča odblokada za brisanje zaradi zadržanja in uporaba politike ob zagonu) in `FolderName` (tako da lahko IMAP sinhronizacija posodobi mapo, ko je e-poštno sporočilo premeščeno na strani strežnika). Zaščita je neodvisna od stolpcev (primerjava na podlagi JSONB) in samodejno zajema prihodnje dodajanja v shemo.

### 🕐 Nastavitve časovnih pasov {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Časovni pas, ki se uporablja za prikazovano časovnih žetonov e-pošte v uporabniškem vmesniku. Uporablja identifikatorje časovnih pasov IANA (npr. "Europe/Berlin", "Asia/Tokyo"). Privzeto je "Etc/UCT" zaradi združljivosti s starejšimi različicami. Ob uvozu e-pošte bodo časovni žetoni pret easy v ta časovni pas za namene prikazovanja.

### 🎉 Nastavitve ReleaseNotes (Splash zaslon za posodobitev različice) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: String različice, ki ga ta instanca prijavi. Primerjava se izvaja s GitHub oznakami izdaj (release tags) in določa, ali obstaja novejša izdaja. Če je prazno, se changelog neuspešno vrne na verzijo kompilirane assemblies in se opozorilo o posodobitvi izklopi.
- `ReleaseNotes__Enabled`: Omogoči ali onemogoči splash zaslon za posodobitev različice (true/false). Privzeto je `true`. Ko je omogočeno, bodo administratorji po posodobitvi aplikacije videli enkratni modalni okno s spremenami, ki prikazuje zapiski o izdaji, pridobljene s GitHub Releases za trenutno različico. Vsak administrator lahko okno zapre in ponovno se bo prikazalo le pri novi različici. Nastavite na `false`, da popolnoma onemogočite to funkcijo, vključno z opozorilom o posodobitvi.
- `ReleaseNotes__UpdateHintEnabled`: Administratorjem prikaži majhno obvestilo (toast) v spodnjem desnem kotu, ko GitHub objavi novejšo stabilno različico od nameščene različice (true/false). Privzeto je `true`. Obvestilo je povezano z to izdajo in se v brskalniku zapre za vsako različico posebej. Predzadki (drafts) in predizdaje (prereleases) so prezrti. Zahteva `ReleaseNotes__AppVersion`; brez tega se nobeno opozorilo ne prikaže.
- `ReleaseNotes__UpdateCheckIntervalHours`: Kako pogosto se osveži seznam izdaj GitHub, v urah. Privzeto je `6`. Krajši interval naredi novo izdajo hitreje vidno pri dolgotrajajoči instanci, daljši interval pa zmanjša število pozivov GitHub API. Minimum je `1`.
- `ReleaseNotes__RequestTimeoutSeconds`: Čas izteka (timeout) za en poziv GitHub API, v sekundah. Privzeto je `10`. Ko čas poteče, changelog in opozorilo o posodobitvi ostaneta izključena za ta poskus namesto tega, da bi poziv pustili odprt; naslednji preverjanje poskuša znova.

### 🔧 Nastavitve vzdrževanja baze podatkov {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Omogoči ali onemogoči samodejno dnevno vzdrževanje baze podatkov (true/false). Privzeto je `false`. Ko je funkcija omogočena, bo sistem samodejno izvajal operacije VACUUM ANALYZE, da bi optimiziral delovanje baze podatkov in preprečil zbranje nepotrebne zrasti (bloat). Za več podrobnosti poglejte [Database Maintenance Guide](DatabaseMaintenance.md).
- `DatabaseMaintenance__DailyExecutionTime`: Čas v dnevu, ko se mora izvesti vzdrževanje baze podatkov, v 24-urnem formatu (HH:mm). Privzeto je `02:00`. Izberite čas med nizko aktivnostjo sistema.
- `DatabaseMaintenance__TimeoutMinutes`: Največ dovoljen čas za operacije vzdrževanja v minutah. Privzeto je `30`. Za večje baze podatkov povečajte to vrednost.

### 🧬 Nastavitve deduplikacije prilog {#attachment-deduplication-settings}

Deduplikacija prilog shrani vsako edinstveno vsebino priloge le enkrat (identificirano s SHA-256) in je **ključna funkcija, ki je vedno vklopljena** – namerno ni stikala za vklop/izklop. Prilagoditi lahko lahko le spodnje parametre za obdelo/urnik. Za celotne podrobnosti si oglejte [Priročnik za deduplikacijo prilog](AttachmentDeduplication.md).

- `AttachmentDeduplication__BatchSize`: Število obstoječih prilog, migriranih po vsaki transakciji med enkratno migracijo obstoječih podatkov v ozadju. Privzeto je `200`. Večje vrednosti migrirajo hitreje, vendar uporabljajo več pomnilnika/obremena baze podatkov na vsako obdelo.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Ob optionalni pavzi (v milisekundah) med paketi migracije za omejitev obremena baze podatkov na obremenjenih sistemih. Privzeto je `0` (brez pavze).
- `AttachmentDeduplication__StartupDelaySeconds`: Zastavitev (v sekundah) po zagonu aplikacije, preden se začne migracija v ozadju, da se omogoči zaključek migracije sheme. Privzeto je `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Interval (v urah) stalnega čiščenja (garbage collection), ki odstranjuje vsebine prilog, ki jih več ne referencira nobena e-pošta. Privzeto je `12`. To teče neodvisno od `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Čas izteka ukazov baze podatkov (v sekundah) za operacije paketa migracije (INSERT s SHA-256 hashiranje in UPDATE). Privzeto je `300` (5 minut). Povečajte to vrednost za zelo velike baze podatkov ali priloge, ali jo zmanjšajte, če želite hitrejše zaznavanje napak. Če paket še vedno izteče, poslužba samodejno poskuša ponovno z polno zmanjšano velikostjo paketa.

### 💾 Nastavitve shrambe za posamezna računa {#account-storage-settings}
Prikaz shrambe za posamezen račun prikazuje porabo baze podatkov (vsi polja e-pošte + priloge) za vsak račun v tabeli "Account Overview" v nadzorni plošči in v tabeli "Show All" v MailAccounts. Avtonomna ozadinska storitev (`AccountStorageRefreshService`) izračuna vrednosti tako, da se izračuna PostgreSQL `pg_column_size` po vsaki koloni (pokriva vsa polja e-pošte brez branja telesov e-pošte) in jih pos缓存 v tabeli `AccountStorageCache`. Ta storitev deluje neodvisno od `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Omogoči ali onemogoči storitev osveževanja shrambe (true/false). Privzeto je `true`. Ko je storitev omogočena, ob zagonu opravi ponovni izpolnjitev (backfill) preostalih računov, ki je lahko prekinjena (prekinitev je varna zaradi `AccountStorageBackfillState`), nato pa izvaja dnevno popolno osveževanje. Ko je storitev onemogočena, se vrednosti shrambe posodobijo le, ko so e-pošte sinhronizirane, uvožene ali izbristane, vendar lahko prikazane vrednosti čez čas postanejo zastarele.
- `AccountStorage__DailyExecutionTime`: Čas dne (format 24 ur `HH:mm`) za dnevno popolno osveževanje vseh računov. Privzeto je `02:30`. Izberite čas med nizko aktivnostjo sistema.
- `AccountStorage__BackfillDelayMs`: Zamuditev (v milisekundah) med računi med začetnim ponovnim izpolnjevanjem ob zagonu. Privzeto je `5000`. Nižje vrednosti pospešijo ponovno izpolnjevanje, vendar povečajo obremenitev baze podatkov; povečajte to vrednost pri zelo velikih arhivih, da preprečite preobremenovanje baze podatkov.
- `AccountStorage__RefreshBatchDelayMs`: Zamuditev (v milisekundah) med računi med dnevnim popolnim osveževanjem. Privzeto je `1000`. Nižje vrednosti pospešijo osveževanje, vendar povečajo obremenitev baze podatkov; povečajte to vrednost pri zelo velikih arhivih.
- `AccountStorage__CommandTimeoutSeconds`: Čas izteka ukaza baze podatkov (v sekundah) za izračun shrambe posameznega računa (`pg_column_size` nad vsemi vrsticami računa). Privzeto je `300` (5 minut). Izračun bere vsako vrstico e-pošte, vključno s TOAST podatki, kar lahko pri zelo velikih arhivih (250k+ e-poštnih sporočil) traja več kot minuto; prejšnja 30-sekundna privzeta vrednost Npgsql je povzročala, da je osveževanje tiho neuspelo in nadzorna plošča je prikazovala zastarele vrednosti. Povečajte to vrednost, če izračun še vedno izteče v vašem arhivu.

> 💡 **Opomba**: Vrednosti shrambe se osvežijo takoj po vsaki sinhronizaciji, uvozu ali izbrisu zaradi politike o shranjevanju, zato ostanejo prikazane vrednosti aktualne tudi brez dnevnega osveževanja. Dnevno osveževanje je varnostna mreža, ki zajame izjemne primere (npr. neposredne spremembe v bazi podatkov).

### 📝 Nastavitve beleženja {#logging-settings}
- `Logging__LogLevel__Default`: Privzeto stopnje beleženja za aplikacijo. Razpoložljive stopnje so: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Privzeto je `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Stopnja beleženja za sporočila okvira ASP.NET Core. Privzeto je `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Stopnja beleženja za ukazov baze podatkov Entity Framework. Privzeto je `Warning`.

### 🛡️ Varnostne nastavitve {#security-settings}
- `AllowedHosts`: Seznam gostiteljev (host names), ločen s točkami, ki jih aplikacija lahko obavlja. To pomaga preprečiti napade na HTTP Host header. Primer: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Pomembno**: V produkcijskih okoljih ne uporabljajte `*`, saj to onemogoča validacijo host headerja.

#### 🔐 Šifriranje poveril v mirovanju {#credential-encryption-at-rest}
Poverila e-poštnih računov (IMAP geslo, M365 client secret in MSA OAuth tokeni) je mogoče šifrirati v bazi podatkov. Šifrirni ključ je shranjen **ven** iz baze podatkov (preimenovalnik okolja / Docker secret), zato sama kopija baze podatkov ni dovolj za branje poveril.

- `Security__CredentialEncryptionKey`: Base64 kodiran 256-bitni (32 bajta) šifrirni ključ. Generirajte ga z:
  ```bash
  openssl rand -base64 32
  ```
  Vrednost nastavite kot preimenovalnik okolja (priporočeno: `Security__CredentialEncryptionKey` v vašem compose datoteka ali okolju kontejnerja). Aplikacija ne podpira Docker secret v slogu `_FILE` — če uporabljate Docker secret, ga prepovejte (mount) in naložite njegovo vsebino v preimenovalnik okolja v vašem entrypointu. Ključa nikoli ne objavljajte v `appsettings.json`. Ko je ključ konfiguriran, se poverila šifrirajo ob zapisu, v ozadja storitev pa enkrat šifrirajo obstoječa poverila v besedilni obliki.
- `Security__EncryptAccountCredentials`: Omogoči ali onemogoči šifriranje novih/posodobljenih vrednosti poveril (true/false). Privzeto je `true`. Ko ključ ni konfiguriran, se poverila shranijo kot besedilo in ob zagonu se zabeleže opozorilo — obstoječe namestitve niso ov prisilno, dokler ni zagotovljen ključ.
- `Security__CredentialEncryptionBackfill`: Ali naj storitev v ozadju ob zagonu šifrirala obstoječa poverila v besedilni obliki (true/false). Privzeto je `true`. Brez konfiguriranega ključa operacija ne naredi ničesar.
- `Security__CredentialEncryptionKeyPrevious`: Ob optionalni prejšnji ključ, uporabljen le za dešifriranje vrednosti, ki so bile šifrirane s tem (za rotacijo ključev). Nove vrednosti so vedno šifrirane s `Security__CredentialEncryptionKey`.

  > ⚠️ **Rotacija ključev**: po rotaciji ohranite konfiguriran `Security__CredentialEncryptionKeyPrevious` **takrat, ko je vsako poverilo ponovno prepisano** (npr. z urejanjem računa ali z avtomatskim osvežitvanjem OAuth tokena) — obstoječe vrednosti, šifrirane s starim ključem, *niso* samodejno ponovno šifrirane z novim ključem. Ko odstranite prejšnji ključ, postane vsako poverilo, ki je še vedno šifrirano s tem, nečitljivo in ga je treba vnesti ponovno. Dokler je konfiguriran, ga morate skrbeti tako varno kot trenutni ključ.

> ⚠️ **Ključ varujte**: Brez ključa šifriranih poveril ni mogoče povrniti in jih je treba vnesti ponovno. Zbackajte ga skupaj z (vendar ločeno od) vašimi varnostnimi kopijami baze podatkov.
>
> 💡 **Obstoječe namestitve**: Nadgradite varno — dokler ni nastavljen `Security__CredentialEncryptionKey`, se nič ne spremeni. Ključ nastavite takrat, ko ste pripravljeni; obstoječa poverila v besedilni obliki ostanejo čitljiva in se šifrirajo v ozadju.

### 🔐 Konfiguracija OIDC {#oidc-configuration}

Za podrobna navodila za nastavitev avtentikacije OpenID Connect poglejte [OIDC Implementation Guide](OIDC_Implementation.md).

#### Osnovne OIDC nastavitve {#basic-oidc-settings}
- `OAuth__Enabled`: Omogoči ali onemogoči OIDC avtentikacijo (true/false)
- `OAuth__Authority`: URL avtoritete OpenID Connect (npr. https://sts.windows.net/{TENANT-ID}/ za Azure AD)
- `OAuth__ClientId`: Klientska identifikacija (client ID), ki jo je dodelil vaš ponudnik identitete
- `OAuth__ClientSecret`: Klientska skrivnost (client secret), ki jo je dodelil vaš ponudnik identitete
- `OAuth__DisplayName`: Nepreobvezujoče ime za prikaz na gumbu za OIDC prijavo in strani za samodejno preusmerjanje (npr. `PocketID SSO`). Če je izpuščeno, se uporabi splošni oznaka "Login with OAuth".
- `OAuth__ClientScopes__0`: Prva scope zahteva od ponudnika identitete (openid)
- `OAuth__ClientScopes__1`: Druga scope zahteva od ponudnika identitete (profile)
- `OAuth__ClientScopes__2`: Tretja scope zahteva od ponudnika identitete (email)
- `OAuth__ResponseMode`: Nepreobvezujoč način odgovora (response mode) za zahtevo za avtorizacijo (`query`, `fragment` ali `form_post`). Privzeto je `form_post`, privzeta vrednost ASP.NET Core. Nastavite na `query` za ponudnike, ki ne podpirajo `form_post`, kot je Synology SSO Server. Poglejte [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Samodejno odobrite nove OIDC uporabnike brez potrebe po ročnem odobritvi administratorja (true/false). Privzeto je `false`.

#### Nastavitve za zagotavljanje uporabnikov (User Provisioning) {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Samodejno odobrite nove OIDC uporabnike brez potrebe po ročnem odobritvi administratorja (true/false). Privzeto je `false`. Ko je funkcija omogočena, so uporabniki, ki se avtenticirajo preko OIDC ponudnika, takoj aktivirani in lahko dostopajo do aplikacije. Ko je onemogočena (privzeto), so novi OIDC uporabniki ustvarjeni kot neaktivni in zahtevajo ročno aktivacijo administratorja. Za podrobne informacije poglejte [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users).
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, itd.: E-poštni naslovi, ki bi morali biti samodejno zagotovljeni kot administratorji. Uporabniki z temi e-poštnimi naslovi bodo ob prvem OAuth prijavi ustvarjeni kot aktivni administratorji, s čimer bodo preskočili običajni postopek odobritve. Primerjava e-poštnih naslovov je neobazna na velike in male črke.

#### Nastavitve za prijavo brez gesla {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Onemogoči prijavo z uporabniškim imenom/geslom (true/false). Privzeto je `false`. Ko je funkcija omogočena, je obrazec za geslo skrit **in** strežnik zavrne vsak POST zahteven za geslo, zato je na voljo le OAuth prijava. Zahteva `OAuth__Enabled=true`. Če je OAuth onemogočen, ta nastavitva nima učinka.
- `OAuth__AutoRedirect`: Samodejno preusmeri uporabnike na OAuth ponudnika (true/false). Privzeto je `false`. Zahteva, da je `OAuth__DisablePasswordLogin` nastavljen na `true`. Uporabniki bodo pred preusmeritvijo videli kratek zaslon nalaganja.

#### Primer: Celotna konfiguracija s prednostno OIDC {#example-full-oidc-first-configuration}
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

## 🔐 Kestrel HTTPS konfiguracija (neobvezno) {#kestrel-https-configuration-optional}

Čeprav je namen aplikacije dostop preko obverse prostokratnega strežnika (reverse proxy) s HTTPS, lahko nastavite tudi spletni strežnik Kestrel za uporabo SSL/TLS certifikatov. To zagotavlja končano šifriranje (end-to-end encryption) med obversnim prostokratnim strežnikom in posameznim poslopom aplikacije (application container).

### Koraki konfiguracije {#configuration-steps}

1. **Generirajte ali pridobite SSL certifikat** v formatu PFX (npr. `localhost.pfx`)

2. **Dodajte naslednje spremenljivke okolja** v svoj `docker-compose.yml` za storitev `mailarchive-app`:

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

4. **Dodajte preslikavo volumna** za certifikat:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Postavite svojo datoteko certifikata** (npr. `localhost.pfx`) v direktorij `./certs` na svojem gostiteljskem sistemu.

### Razlagnja spremenljivk okolja {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: URL končnega mesta HTTP (privzeto: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: URL končnega mesta HTTPS (privzeto: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Pot do datoteke PFX certifikata znotraj poslopja
- `Kestrel__Endpoints__Https__Certificate__Password`: Geslo za datoteko PFX certifikata

> 💡 **Opomba**: Ta konfiguracija je neobvezna. Če uporabljate obversni prostokratni strežnik z HTTPS (priporočeno), lahko komunikacija med obversnim prostokratnim strežnikom in aplikacijo ostane preko HTTP. Vendar pa za maksimalno varnost v občutljivih okoljih morda boste želeli omogočiti tudi HTTPS na Kestrelu, da zašifrirate celotno pot komunikacije.

## 🔑 Upravljanje tajnosti za produkcijo {#secrets-management-for-production}

Neposredno v `docker-compose.yml` zapisovanje občutljivih podatkov, kot so gesla za bazo podatkov, administratorjeva poverilnika in OAuth client secrets, predstavlja varnostno tveganje, še posebej, če je datoteka shranjena v sistemu za nadzor različic. Priporočan pristop je izpeljave teh vrednosti v `.env` datoteko, ki jo Docker Compose samodejno naloži.

### 📄 Datoteka `.env` {#the-env-file}

V istem direktoriju kot vaša `docker-compose.yml` ustvarite `.env` datoteko z naslednjim vsebino:

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

**Pomembno**: Docker Compose samodejno bere `.env` datoteko iz istega direktorija — tega **ne potrebujete** navesti ročno. Vse spremenljivke, definirane v `.env`, so na voljo v `docker-compose.yml` prek sintakse `${VARIABLE_NAME}`.

### 🐳 Prilagojena `docker-compose.yml` {#adapted-docker-composeyml}

`docker-compose.yml` iz korakov namestitve je treba prilagoditi tako, da namesto trdno vpisanych tajnosti uporablja predstavilnike:

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

### 📋 `.env.example` predlogo za administratorje {#envexample-template-for-administrators}

V svojem repozitoriju ustvarite `.env.example` datoteko, ki služi kot predloga za druge administratorje. Vsebuje vrednosti predstavnikov in dokumentacijo, vendar **brez dejanskih tajnosti**:

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

- **Uporabite `.env.example` kot predlogo**
- **Nastavite stroge dovoljenja za datoteke** — Omejite dostop do datoteke `.env`:
  ```bash
  chmod 600 .env
  ```
- **Uporabite močna gesla** — Generirajte dolga, naključna gesla (vsaj 16 znakov) z mešanico črk, številk in posebnih znakov.
- **Omejite dostop do datoteke `.env`** — Le uporabnik, ki izvaja Docker Compose, bi moral imeti dovoljenja za branje datoteke `.env`.

## 🔒 Varnostni opomini {#security-notes}

- Uporabite močna gesla in spremenite privzete poverilnice. Gesla naj bodo dolga vsaj 12 znakov in naj vključujejo kombinacijo velikih in malih črk, številk ter posebnih znakov. Izogibajte se uporabi običajnih besed ali podatkov, ki jih je lahko enostavno ugotoviti.
- Za produkcijo upoštevajte implementacijo HTTPS z obratno namenskim posrednikom (reverse proxy).
- Priporočljivo je redno opravljanje varnih kopij PostgreSQL baze podatkov. Za podrobne postopke varnega kopiranja in obnavljanja poglejte [Backup and Restore Guide](BackupRestore.md).
