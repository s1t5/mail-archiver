# 🛠️ Navodila za nastavitev Mail Archiverja {#mail-archiver-setup-guide}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodič nudi podrobna navodila za nastavitev aplikacije Mail Archiver z uporabo Docker Compose.

## 🛠️ Predpogoj {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Koraki namestitve {#installation-steps}

1. Namestite predposegnięte zahteve na svoj sistem.

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

4. Če želite uporabljati avtentikacijo (kar se močno priporoča), določite `Authentication__Username` in `Authentication__Password`, ki se uporabljata za uporabnika administratorja.

5. Prilagodite spremenljivo okolja `TimeZone__DisplayTimeZoneId`, da ujemate vaš izbran časovni pas (privzeto je "Etc/UCT"). Lahko uporabite katerikoli identifikator časovnega pasu IANA (npr. "Europe/Berlin", "Asia/Tokyo").

6. Po potrebi nastavite spremenljive okolja `Logging__LogLevel` za nadzorovanje podrobnosti dnevnišnih zapisov aplikacije. Za razpoložljive možnosti poglejte spodnji razdelek Nastavitve dnevnišnih zapisov.

7. Konfigurirajte obratni proxy po svoji izbiri z https in avtentikacijo za zagotovitev varne dostopnosti do aplikacije.

> ⚠️ **Pozor**: Sama aplikacija ne zagotavlja zaščitene povezave prek https! Mora biti nastavljena preko obturnega proxyja!

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
   - Navigacija do razdelka "Email Accounts"
   - Kliknite "New Account"
   - Vnesite podrobnosti o strežniku in podatke za prijavo
   - Shrani in začni z arhiviranjem!
   - Če želite, ustvarite druge uporabnike in dodelite račune.

## 📚 Razlagi okoljskih spremenljivk {#environment-variable-explanations}

### 🗄️ Povezava z bazo podatkov {#database-connection}
- `ConnectionStrings__DefaultConnection`: Vezni niz za PostgreSQL bazo podatkov. Po potrebi prilagodite vrednosti `Host`, `Database`, `Username` in `Password`.

### 🔐 Nastavitve avtentikacije {#authentication-settings}
- `Authentication__Username`: Uporabniško ime za administrativni račun.
- `Authentication__Password`: Geslo za administrativni račun.
- `Authentication__SessionTimeoutMinutes`: Čas izteka seance v minutah. To velja za piškot za prijavo brez trajnosti; uporabniki, ki ob prijavi označijo **Remember Me**, prejmejo trajen piškot, ki velja 7 dni. Več informacij na [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: Ime piškotka za avtentikacijo.
- `Authentication__CookieSameSite`: Konfigurira atribut SameSite za piškotke za avtentikacijo, seanso in zaščito pred CSRF. Veljavne vrednosti so:
  - `Strict` (default): Maksimalna varnost. Piškotki se pošljejo le pri zahtevah istega mesta (same-site). To lahko povzroči težave pri navigaciji v aplikacijo iz zunanjih povezav (npr. klik na povezavo z druge spletne strani), saj obstoječa seansa ne bo prepoznana.
  - `Lax`: Priporočeno pri uporabi obratnega proxyja (reverse proxy). Piškotki se pošljejo pri navigacijah na najvišji ravni in zahtevah istega mesta, kar uporabnikom omogoča sledenje zunanjim povezavam do aplikacije, hkrati pa ohranja zaščito pred CSRF za POST zahteve.
  - `None`: Piškotki se pošljejo pri vseh zahtevah. Zahteva HTTPS in atribut `Secure`. Uporabite to le, če imate specifične zahteve glede medstranskega komuniciranja (cross-site) in razumete varnostne posledice.

### 🔌 Nastavitve REST API {#rest-api-settings}
Neobvezni REST API v okolju samo za branje je **privzeto onemogočen**. Za celovit pregled poglejte [vodnik REST API](API.md).
- `Api__Enabled`: Glavno stikalo za REST API v okolju samo za branje (privzeto `false`). Ko je `false`, vsi `/api/*` poti vrnejo `404`.
- `Api__AllowAttachmentDownloads`: Omogoči prenos bajt prilog prek API-ja (privzeto `true`). Ko je `false`, končnica priloge vrne `403`.
- `Api__AllowMessageDownloads`: Omogoči prenos celotnih sporočil prek API-ja, tako zgrajenega `/eml` kot originalnega `/raw` (privzeto `true`). Ko je `false`, obe končici vrneta `403`. Celotno sporočilo vsebuje svoje priloge, zato morata biti **obe** to stikalo in `Api__AllowAttachmentDownloads` nastavljena na `true`; če izklopite katero koli od obeh, bosta obe končici onemogočeni.
- `Api__EnableSwaggerUi`: Razpoložljivost Swagger UI na `/apidocs` in OpenAPI dokumenta na `/apidocs/spec/v1.json` (privzeto `true`). Oba zahtomega prijavljeno brzalniško enoto (browser session).
- `Api__DefaultPageSize`: Privzeta velikost strani za končice seznamov, če ni navedena (privzeto `20`).
- `Api__MaxPageSize`: Največja dovoljena velikost strani; večji zahtevki bodo omejeni (privzeto `100`).
- `Api__RateLimitPerMinute`: Budžet zahtev v fiksnem oknu na ključ API na minuto (privzeto `120`).

### 🤖 Nastavitve MCP strežnika {#mcp-server-settings}
Neobvezen MCP (Model Context Protocol) strežnik omogoča isti dostop do poštnih ladij v načinu samo branje kot REST API AI agentom prek Streamable HTTP prenosa. **Privzeto je onemogočen**. Za celovito navodila si oglejte [vodnik MCP](MCP.md). MCP ponovno uporablja pravne ključe, istemore kot REST API (`ma_...` bearer tokens), in isto omejevanje dostopa na uporabniško raven.
- `Mcp__Enabled`: Glavno stikalno stikalo za MCP strežnik (privzeto `false`). Ko je `false`, kontejner `/mcp` vrne `404`.
- `Mcp__AllowAttachmentDownloads`: Do dovoli, da orodje `get_attachment` vrne bajte priloge (privzeto `true`). Ko je `false`, orodje vrne napako.
- `Mcp__DefaultPageSize`: Privzeta velikost strani, ki jo uporablja `search_emails`, ko klicatelj izpusti `pageSize` (privzeto `20`).
- `Mcp__MaxResults`: Zgornja meja za velikost strani za `search_emails`; večje vrednosti so omejene (privzeto `100`).
- `Mcp__MaxAttachmentBytes`: Maksimalna velikost priloge (v bajtih), ki jo bo orodje `get_attachment` vrnilo neposredno kot base64; večje priloge zavrni (privzeto `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Do dovoli, da orodja `get_email_eml` in `get_email_raw` vrneta celotna sporočila (privzeto `true`). Ko je `false`, orodja vrneta napako. Celotno sporočilo vsebuje svoje priloge, zato morata biti **tanto** to stikalo **kot** `Mcp__AllowAttachmentDownloads` nastavljena na `true`; če izklopite katero koli od obeh, bosta oba orodja onemogočena.
- `Mcp__MaxMessageBytes`: Maksimalna velikost sporočila (v bajtih), ki jo bosta orodja `get_email_eml` in `get_email_raw` vrnila neposredno; večja sporočila zavrne (privzeto `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Budžet zahtev v fiksnem oknu na ključ API na minuto (privzeto `120`).

### 📨 Nastavitve MailSync {#mailsync-settings}
- `MailSync__IntervalMinutes`: Interval v minutah med sinhronizacijo e-pošte. To je globalni privzetek; vsak račun lahko posamezno prekrides na strani Za ustvarjanje/urejanje (pustite prazno, da uporabite ta privzetek).
- `MailSync__FullSyncIntervalHours`: Nepreobvezilen globalni privzetek za samodejne popolne ponovne sinhronizacije v urah. Ko ni nastavljen (privzeto), se ne izvaja samodejna popolna sinhronizacija, razen če je na strani Za ustvarjanje/urejanje nastavljena vrednost `FullSyncIntervalHours` za posamezen račun. Vrednosti za posamezen račun prenašajo ta globalni privzetek.
- `MailSync__TimeoutMinutes`: Čas izteka sinhronizacije za posamezen račun v minutah. Sinhronizacija, ki traja dlje, se ustavi pri naslednji meji sporočila, ohrani svoje kontrolne točke in nadaljuje pri naslednjem zagonu; naloga je poročena kot `Timed Out` in `LastSync` se ne napreduje. Velja le za načrtovane sinhronizacije; sinhronizacija, začeta iz uporabniškega vmesnika (ročna sinhronizacija ali popolna ponovna sinhronizacija), poteka brez časovnega izteka. `0` (privzeto) ali katera koli nepozitivna vrednost pomeni brez časovnega izteka. Pred nadgradnjo preverite vrednost, če sinhronizirate velike poštne predalnike. Poglejte [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Čas izteka povezave za IMAP povezave v sekundah.
- `MailSync__CommandTimeoutSeconds`: Čas izteka ukaz za IMAP ukaze v sekundah.
- `MailSync__AlwaysForceFullSync`: Ali vedno prisilno izvedete popolno sinhronizacijo (true/false).
- `MailSync__IgnoreSelfSignedCert`: Ali ignorirate samodejno podpisana certifikata (true/false).
- `MailSync__MaxConcurrentSyncs`: Koliko računov se lahko sinhronizira hkrati. Privzeto `1` (sekvencialno, navpigжно kompatibilno). Razporejevalnik ponovno napolni prosta mesta, ko postanejo prosti, namesto da bi čakal na celotno paketo, zato ena počasna poštna nabiralnica ne zadržuje več ostalih. Povečajte za h konkurenčno sinhronizacijo več računov — upoštevajte omejitve hitrosti ponudnika in uporabo lokalnih virov. Poglejte [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__MaxIssuesPerKind`: Koliko težav vsakega tipo si naloga sinhronizacije zapomni za stran računa. Neuspešni nabralniki, manjkajoči nabralniki in neuspešna sporočila imajo ločene proračune, zato množica ene vrste ne lahko izloči drugih iz pogleda; vse zunaj proračuna se šteje namesto shranjivanja. Privzeto `20`. `0` izključi podrobnosti in pusti le številke. Poglejte [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Nabralniki, izključeni iz sinhronizacije za **vsak** račun, poleg lastnega seznama izločenih nabralnikov posameznega računa. Privzeto prazno, zato obstoječe nastavitve niso ovirane. Oba seznama sta **dodajena** — nabralnik se preskoči, če ustreza kateremu koli od obeh — in oba uporabljata iste pravila ujemanja: natančno ujemanje na celotno pot, natančno ujemanje na ime nabralnika, ujemanje z sufiksom poti (tako `Drafts` ustreza tudi `INBOX/Drafts` in `INBOX.Drafts`) in vse pod nabralnikom, ki ga vnos navaja, ko je `MailSync__ExcludeSubfolders` vklopljen. Ujemanje je neobčudljivostno glede na velikost črk. Uporabno pri uvozu številnih nabiralnikov z istega strežnika, kjer je alternativa vzdrževanje identičnega seznama izločenih na vsakem računu. Noben nabralnik ni izločen privzeto; to, katera imena je vredno navesti, je odvisno od strežnika in njegovega jezika. Primer za drevo nabiralnika, ki vsebuje tudi nabiralnike za koledar in kontakte:
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
- `MailSync__ExcludeSubfolders`: Ali vpis za izključitev pokriva tudi nabralnike pod tistim, ki ga navaja. Velja tako za seznam za posamezen račun kot za tistega za celotno namestitev, in za oba ponudnika. Privzeto `false`, kar ujemajo vedenje, preden je bila možnost obstajala: vnos `Deleted Items` ustreza samo temu nabralniku, drevo pa ostane zunaj arhive z navedbo njegovega korena in vsakega nabralnika pod njim. Nastavite na `true`, da bo vnos zajel tudi drevo pod njim — vnos `Deleted Items` nato tudi ohranja `Deleted Items/2024` in preostanek te veje zunaj arhive. Pravilo se navezuje na ločilec poti, zato prevzame celotne nabralnike in nikoli del imena: `Archive` pokriva `Archive` in `Archive/Team`, in pusti brez sprememb poštni nabralnik z imenom `Team Archive Review`. Tudi `.` šteje kot ločilec, na enak način kot ga obravnava pravilo sufiksa poti.
- `MailSync__InterAccountDelaySeconds`: Nepreobveziven zakasnitev (stagger delay) v sekundah, ki se uporabi ob koncu vsake naloge sinhronizacije računa. Privzeto `0` (brez zakasnitve). Uporabno za izogibanje kratkim udarcem ob zagonu, ko je `MaxConcurrentSyncs > 1`.

### 📧 Nastavitve shranjevanja originalnih sporočil {#original-message-storage-settings}

Privzeto arhiv shrani le razčlenjene dele e-pošte, zato je izvoženo `.eml`
rekonstrukcija: kodiranja prenosa, MIME meje in prepogib zaglavka se razlikujeta od
prejetega sporočila, prav tako pa DKIM podpisi ne več ne potrdijo. S `RawMessages:Enabled` se
shranijo tudi celotni original.

| Parameter | Default | Namen |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Shranjstvo originala vsakega novega arhiviranega e-maila. |
| `RawMessages__Storage` | `postgres` | Kje se nahajajo bajti: `postgres` ali `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Večja sporočila se arhivirajo brez svojega originala. `0` = ni omejitve. |
| `RawMessages__IncludeGraph` | `true` | Prav tako shranjuj originale za Microsoft 365 (Graph) račune. |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Mesto za datoteke sporočil. Mora kazati na montiran volumen v Dockerju. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Stopnje direktorijev, na katere je razporen id e-pošte. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Preverjanje SHA-256 ob vsakem prenosu. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Odstranjevanje datotek sporočil, na katere se nobena e-pošta ne nanaša. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Kako pogosto se izvede to čiščenje. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Datoteke, navzdol čisto mlajše od tega, se nikoli ne odstranijo. |

!!! warning "Namestitev na datoteko potrebuje montiran volumen"
    S `RawMessages__Storage=filesystem` datoteke sporočil zunaj baze podatkov.
    Brez bind mounta bodo le v zapisnem sloju kontejnerja in bodo izgubljeni ob
    naslednji ponovni izdelavi kontejnerja, medtem ko vrstice v bazi podatkov preživijo.
    Izvajaј varnostno kopiranje direktorija skupaj s kopijo baze podatkov.

Za celoten opis poglejte **[Original Message Storage](RawMessages.md)**: kje se poslužuje
original, skladiščni sistemi, kako premakniti obstoječa sporočila med njimi s
`--migrate-raw-storage` in kaj je treba varnostno kopirati.

### 📤 Nastavitve BatchRestore {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Število e-poštnih sporočil, ki sproži asinkrono obdelavo.
- `BatchRestore__MaxSyncEmails`: Največ number e-poštnih sporočil za sinhrono obdelavo.
- `BatchRestore__MaxAsyncEmails`: Največ number e-poštnih sporočil za asinkrono obdelavo.
- `BatchRestore__SessionTimeoutMinutes`: Čas izteka seance za obnavo serije (batch restore) v minutah.
- `BatchRestore__DefaultBatchSize`: Privzeta velikost serije za operacije z e-pošto.

### 🏢 Nastavitve upravljanja s strankami (Tenant Management) {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Največja številka poštovnih predalov, ki jih je mogoče dodati v eni operaciji upravljanja s strankami (Tenant Management). Privzeto je `1000`. Povečajte to vrednost za zelo velike stranke ali jo znižajte, da preprečite nenamerno masovno uvoz. Ko je omejitev presežena, operacija zavrne z napako pri validaciji in računi niso ustvarjeni. Za podrobnosti si oglejte [M365 Tenant Import Guide](M365TenantImport.md).

### 🐄 Nastavitve Mailcow {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Največja številka poštovnih predalov, ki jih je mogoče pripraviti v enem samotnem pogoju aktivacije Mailcow. Privzeto je `1000`. Zmanjšajte to vrednost, da preprečite nenamerno masovno pripravljanje, ali jo povečajte za velike primere. Ko je omejitev presežena, računi niso ustvarjeni. Za podrobnosti poglejte [Mailcow Instances](Mailcow.md).
- Integracija Mailcow dodatno zahteva `Security__CredentialEncryptionKey` (poglejte [Security Settings](#-security-settings)). Povezovanje instance in aktivacija poštovnih predalov sta zavrnjena, če je šifriranje poveril onemogočeno, zato API ključ in generirana gesla aplikacij ne smejo biti nikoli shranjeni kot nedovжно besedilo.
- Za odhodni dostop je potreben dostop do Mailcow HTTPS API in do IMAP gostitelja prek TLS (običajno port `993`).

### 📦 Nastavitve BatchOperation {#batchoperation-settings}
- `BatchOperation__BatchSize`: Velikost paketa (batch size) za operacije z e-pošto.
- `BatchOperation__PauseBetweenEmailsMs`: Pausa med posameznimi e-poštnimi sporočili v milisekundah.
- `BatchOperation__PauseBetweenBatchesMs`: Pausa med paketi v milisekundah.

### 📤 Nastavitve za prenos (Offload Settings) {#offload-settings}
Nastavitve za [date-windowed offload](Offload.md). Vse privzeto vrednosti ponovno odražajo vedenje, ki ga je aplikacija imela, preden je bila ta funkcija uvedena, zato namestitev, ki ne konfigurira tega dela, ni odobljena.
- `Offload__MaxConcurrentJobs`: Koliko oprav za ponovno okrevanje (restore) ali prenos (offload) lahko teče hkrati. Privzeto je `1`, kar ohranja strogo serijsko obdelavo, ki jo je vrstica opravila vedno uporabljala. Ne glede na to vrednost teče največ ena oprav na ciljno poštovno predal.
- `Offload__PrefetchMaxMessages`: Zgornja meja tem, koliko sporočil je indeksiranih iz ciljnega poštovnega predala za zaznavanje ponovitev. Privzeto je `500000`. Nad to vrednostjo se preverjanje zoži na eno mapo in zabeleži, da je bil obseg zmanjšan.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Mapove izvora, ki se nikoli ne prenesejo, ki se ujamejo pred preimenovanjem in pokrivajo podmape. Privzeto so prazne.
- `Offload__FolderRenameMap__<SourceFolder>`: Prepiše začetne segmente poti mape izvora, na primer `Offload__FolderRenameMap__Sent Items=Sent`. Privzeto je prazno.
- `Offload__MarkAsSeen`: Ali je dodana pošta označena kot prebrana. Privzeto je `true`, kar ujemajo obstoječemu vedenju pri ponovnem okrevanju.

Obstil nastavitve map sta namerno prazni: prepisovanje ali odstranjevanje map brez odobritve bi presenetilo vsakogar, ki že uporablja pot za ponovno okrevanje. Zato z voženjem brez konfiguracije migrirate vse, vključno s mapami za neželeno pošto, in ustvarite ikinci niz posebnih map poleg tistih v cilju.

### 📊 Nastavitve sledenja širini pasov {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Omogoči ali onemogoči sledenje širini pasov za obravnavo omejitev hitrosti IMAP (true/false). Privzeto je `false`. Ko je funkcija omogočena, sistem sledi uporabi širine pasov na račun in lahko ustavi sinhronizacijo, ko so dosežene omejitve ponudnika. Za podrobne informacije poglejte [Obravnavo omejitev hitrosti](RateLimitHandling.md).
- `BandwidthTracking__DailyLimitMb`: Dnevna omejitev prenosov v megabajtih na račun. Privzeto je `25000` (25 GB). Za ponudnike z omejitvami širine pasov nastavite to vrednost tako, da ustreza njihovi omejitvi hitrosti (npr. `2500` za ponudnike z dnevno omejitvijo okoli ~2500 MB). Sistem bo ustavil sinhronizacijo, ko je dosežena ta omejitev.
- `BandwidthTracking__WarningThresholdPercent`: Delež dnevne omejitve, pri katerem se zabeležijo opozorilna sporočila. Privzeto je `80`. Ko uporaba širine pasov doseže ta odstotek, se zabeležijo opozorila za spremljanje približevanja omejitve.
- `BandwidthTracking__PauseHoursOnLimit`: Število ur za ustavitev sinhronizacije, ko je dosežena dnevna omejitev. Privzeto je `24`. Po tem obdobju se zastavica za omejitev samodejno izbriše in sinhronizacija se nadaljuje.
- `BandwidthTracking__TrackUploadBytes`: Ali slediti tudi širini pasov za nalaganje (true/false). Privzeto je `false`. Večina IMAP ponudnikov omejuje le prenose, zato to običajno ni potrebno.

### 🎯 Nastavitve izbire {#selection-settings}
- `Selection__MaxSelectableEmails`: Največja števila e-poštnih sporočil, ki jih je mogoče izbrati hkrati.

### 👁️ Nastavitve ogleda (Zasebnost in prikaz) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Nadomešča privzeto vrsto pogleda e-pošte za zasebnost in preprečevanje sledenja (true/false). Privzeto je `false`.
  - Ko je nastavljeno na `true`: E-pošta se privzeto odpre v načinu besedilnega zapisa (plain-text), kar preprečuje samodejno nalaganje sledilnih pikselov, zunanjih slik in spletnih žarometov (web beacons). To se priporoča uporabnikom, ki skrbijo za sledenje e-pošte in zasebnost.
  - Ko je nastavljeno na `false`: E-pošta se privzeto odpre v HTML načinu, s prikazom oblikovane e-pošte z slikami in slogi.
  - Uporabniki lahko med HTML in besedilnim zapisom preklapljajo z gumbom na strani s podrobnosti e-pošte (vidno le, ko sta oba formata na voljo).
  - Povezava "Full View" upošteva trenutno izbran način ogleda.

- `View__BlockExternalResources`: Blokira zunanje vire (oddaljene slike, zunanje CSS, zunanje skripte, spletne police itd.) v HTML prikazih e-pošte za preprečevanje sledenja in izboljšanje zasebnosti (true/false). Privzeto je `false`.
  - Ko je nastavljeno na `true`: Zunanji viri so filtrirani pri prikazovanju HTML e-pošte. Prikazano je le vsebina v liniji (inline) in data URI (vključno z vključnimi prilogami prek `cid:` referenc).
  - Ko je nastavljeno na `false`: HTML e-pošta je prikazana z vsemi svojimi prvotnimi zunanjimi viri.
  - **Pomembno**: Ta nastavitev vpliva le na **prikaz** e-pošte. Arhivirana e-pošta je v bazi podatkov shranjena popolnoma nespremenjena, z ohranjeno vso prvotno vsebino.
  - Blokirani viri vključujejo:
    - Oddaljene slike (sledilni pixeli, zunanje slike, shranjene na strežnikih)
    - Zunanje CSS slogovnike
    - Zunanje police prek @font-face
    - Zunanje CSS uvoze prek @import
    - Zunanje slike ozadja
  - dovoljeni viri:
    - Vključene slike, vključene kot data: URI
    - Vključene priloge, na katere se nanaša preko cid: URI
    - Vključeni CSS slogi in oznake style
  - Ta nastavitev deluje neodvisno od `DefaultToPlainText` in zagotavlja dodatno plast zaščite zasebnosti pri ogledovanju HTML e-pošte.

### 📊 Nastavitve nadzorne plošče {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Ali kartice številnic vključujejo svoje dele za prejeto in poslano pošto, kartica računa pa število različnih domen. Privzeto je `true`. Če nastavite na `false`, se te številke ne izračunajo, temveč so namesto izračunavanja skrite: številci prikazujejo preprosto število, število prilog pa ne izvaja več združevanja z e-pošto, na katero se nanaša, kar je edini poiskovalni niz, ki ga ti deli dodajo.
- `Dashboard__SelectablePeriods`: Ali nadzorni grafi ponujajo ločljivost, obdobje in puščice za premikanje. Privzeto je `true`. Če nastavite na `false`, grafi prikazujejo zadnjih dvanajst mesecev po mesecih in pošiljatelje celotnega arhiv kind, končna točka grafa odgovarja, kot da ne bi obstajala, in najstarejši datum pošiljanja ni iskan.
- `Dashboard__CacheSeconds`: Kako dolgo so izračunani statistični podatki nadzorne plošče (skupni iznosi s splitom na prejeto in poslano pošto, številke na račun, serije grafov, najpogostejši pošiljatelji, nedavne e-pošte, velikost baze podatkov) shranjeni v pomembni cache-u strežnika. Privzeto je `60` sekund. Nastavite na `0`, da onemogočite cache in vedno ponovno izračunate statistiko. Vsaka izbira ločljivosti in obdobja grafa je shranjena posebej, zato nadzorna plošča, ki se preklapa med izbira, hrani en zapis za vsako izbiro. Višje vrednosti zmanjšajo obremenitev baze podatkov v velikih okoljih na račun zastarelih številk. Pripadki statusa sinhronizacije, vrednosti shrambe in seznam nedavnih e-pošt se vedno pridobivajo v živo in niso vplivni na ta cache.
- `Dashboard__RefreshIntervalMinutes`: Nameščena je pogostost, pri kateri `DashboardStatsRefreshService` servis v ozadju ponovno izračuna zahtevne agregacije nadzorne plošče (skupne številke s splitom na prejeto in poslano pošto, številke na račun, število domen računov, privzeto serije grafov z najpogostejšimi pošiljatelji, velikost baze podatkov) v tabelo baze podatkov `DashboardStatsCache`. Pri namestitvah z več milijoni e-poštnih sporočil nadzorna plošča bere te pripravljene vrednosti namesto agregacije celotnega arhiv v poti zahteve, kar skrajša prvi nalaganje iz približno 10 sekund na milisekunde. Končna točka grafa odgovarja na privzeto izbiro (meseci v obdobju ene leta, prejeti pošiljatelji, trenutno obdobje) iz istega vrstca, zato se ob vrnitvi prikazujejo številke ob prvem çizlenju; vsaka druga izbira se izračuna ob zahtevi in se hrani v zgornjem cache-u v pomembni pomnilnik. Privzeto je `15` minut. Nastavite na `0`, da onemogočite osveževanje v ozadju; nadzorna plošča nato ponovno izračuna statistiko v živo (ob še vedno veljavnem cache-u v pomembnem pomnilniku). Prikazane vrednosti lahko začasno zaostarijo za konfigurirano lahko v primeru dejanskega stanja arhivov. Vrstica, izračunana z drugimi vrednostmi `ShowDirectionSplits` ali `SelectablePeriods` kot so trenutne, se ne uporabi; nadzorna plošča izračuna podatke v živo do naslednjega osveževanja.

### 🔄 Nastavitve opravil {#jobs-settings}
- `Jobs__RefreshSeconds`: Kako pogosto se stran s podложnimi opravili samodejno osveži, medtem ko je odprta v zavijku brskalnika. Privzeto je `30` sekund. Nastavite na `0`, da izklopite samodejno osveževanje in osveževanje opravite ročno. Vsako osveževanje ponovno zgradi stran iz vseh osmih virov opravil, zato na namestitvah z veliko računov daljši interval zmanjša obremenitev, ki se многоžiči z vsakim odprtim zavijkom na strani.

### 🗃️ Nastavitve Npgsql {#npgsql-settings}
- `Npgsql__CommandTimeout`: Čas izteka za bazične ukaze v sekundah.

### 📥 Nastavitve nalaganja {#upload-settings}
- `Upload__MaxFileSizeGB`: Maksimalna velikost datoteke za nalaganje v GB.
- `Upload__KeepAliveTimeoutHours`: Čas preživetve (keep alive timeout) za nalaganje v urah.
- `Upload__RequestHeadersTimeoutHours`: Čas izteka (timeout) za zahteve zagotavljajočih glavice v urah.

### 📂 Nastavitve za lokalni uvoz {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, itd.: Seznam dovoljenih lokalnih direktorijev, iz katerih lahko CLI uvozne povelki (`--import-mbox`, `--import-eml`) berejo datoteke. Vsak vpis je pot znotraj kontejnerja. Datoteke za uvoz morate montirati v enega od teh direktorijev z uporabo Docker volumenov. Privzeto: `/data/import` (samodejno nastavljeno na `/app/uploads` kot nadomeščenč).
  - Pri uporabi `docker exec` za zagon uvozne povelke mora biti datoteka, navedena prek `--file`, znotraj ene od teh dovoljenih poti.
  - Ta varnostni ukrep preprečuje nedovoljen dostop do datoteka preko CLI povelkov.
  - Za različne vire uvoza je lahko konfiguriranih več poti.
  - Za podrobna navodila uporabe poglejte [CLI commands](CLI.md).

### 📄 Nastavitve uvoza CSV {#csv-import-settings}
- `CsvImport__MaxRows`: Največja številka vrstic CSV (poštovnih predalov), obdelanih pri enem massovnem uvozu. Privzeto je `5000`. Za velike namene povečajte to vrednost; znižajte jo, da omejite vpliv enega uvoza na obremenitev baze podatkov.
- `CsvImport__MaxFileSizeBytes`: Največja dovoljena velikost (v bajtih) naložene datoteke CSV. Privzeto je `10000000` (10 MB). Če je potrebno, prilagodite to vrednost svojim omejitvam nalaganja.
- Za podrobna navodila za uporabo massovnega uvoza IMAP računov prek CSV poglejte [Account Import Guide](Account-Import.md).

### 📤 Nastavitve izvoza revizijskega poročila {#audit-export-settings}
Nastavitve za stran za izvoz revizijskih podatkov (samo za administratorje, dostopno s strani Logs). Izvoz ustvari ZIP paket z tabelnimi masovnimi podatki (INDEX.XML + CSV tabele + DTD) iz obstoječega arhiv kind za zunanje revizijske orodje. Za podrobnosti o uporabi poglejte [Audit Data Export Guide](AuditExport.md).
- `AuditExport__DataSupplierName`: Privzena vrednost za polje v obrazcu "Data supplier name", ki identificira vašo organizacijo v izvoženem indeksnem 파일u. Privzeto: prazno.
- `AuditExport__DataSupplierLocation`: Privzena vrednost za polje v obrazcu "Data supplier location" (npr. sedež podjetja). Privzeto: prazno.
- `AuditExport__Comment`: Privzena vrednost za polje v obrazcu "Comment", ki je vključeno kot prost tekst v izvoženem indeksnem datoteki. Privzeto: prazno.
- `AuditExport__OutputDirectory`: Mesto (directory), kjer so shranjene ustvarjene ZIP datoteke. Relativne poti se razrešujejo glede na korenino vsebine aplikacije. Privzeto: `exports/audit`.
- `AuditExport__RetentionDays`: Število dni, po katerih dnevno čiščenje izbriše izvožene datoteke. Privzeto: `30`.
- `AuditExport__MaxRangeYears`: Največ dovoljenega razpona med datumom začetka in konca obdobja izvoza. Privzeto: `10`.
- Polja v obrazcu so predpolnjena s temi privzetimi vrednostmi, lahko pa jih uredite za vsak izvoz posebej.
- Vsak izvoz v dostopni dnevnik zapisuje vnos začetka in vnos rezultata (tip "Audit Data Export"), zato je zgodovina varna pred spremembami brez kakršnihkoli sprememb sheme DB.

### 🔒 Nastavitve politike brisanja {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Nadzoruje, ali je dovoljeno ročno brisanje arhiviranih e-poštnih sporočil (true/false). Privzeto je `true`. Ko je nastavljeno na `false`:
  - Ob zagonu so vse arhivirane e-poštne pošiljke zaklenjene (`IsLocked = true`) prek sprožilca skladnosti v bazi podatkov, kar preprečuje kakršna koli spremembo ali brisanje na ravni baze podatkov.
  - Ročno brisanje (posamezno in množinsko) je blokirano na ravni aplikacije z besedilom o napaki.
  - Privzete vrednosti v stolpcih so prilagojene tako, da so tudi nedavno uvozenih e-poštnih sporočil zaklenjena.
  - Trenutno stanje politike se ob vsakem zagonu zabeleži v tabeli AccessLogs (vidno na strani Logs kot vneski "Deletion Policy") za potrebe revizije.
  - Brisanje glede na lokalno politiko zadrževanja (retention) je iz exceptions: e-poštna sporočila, ki spadajo pod nastavljeno obdobje zadrževanja, se še vedno izbrišejo (pred brisanjem v okviru procesa zadrževanja so takoj odključen avtorizacijski zaklep).
- `DeletionPolicy__MaxBulkDeleteEmails`: Zgornja meja za množinsko brisanje na podlagi filtra (*Delete all matches*). Privzeto je `100000`. Filter, ki ustreza več e-poštnim sporočilom, je zavrnjen z sporočilom, ki zahteva zoбšo filtra; naloga nikoli ne skrajša števila nedolжно. E-poštna sporočila, zaklenjena zaradi politike zadrževanja, so preskočena in poročena v statusu naloge, ne odklenejo pa jih. Naloga ob zagonu ponovno preveri to omejitev in `DeletionPolicy__DeletionAllowed`, zato se naloga, postavljena v vrsto pred spremembo politike, ustavi namesto tega, da bi se izvedla s starimi nastavitvami.
- **Zaščita pred spremembami (Immutability protection):** Ko je `IsLocked = true`, sprožilec skladnosti baze podatkov (`prevent_locked_email_changes`) blokira KAKŠNO KOLI spremembo vrstice e-poštnega sporočila — zaščiteni so vsi stolpci, ne le seznam določenih polj. Edini iz exceptions stolpci so `IsLocked` sama (da ostane mogoča odključenje za brisanje zaradi zadrževanja in uporabo politike ob zagonu) in `FolderName` (da lahko IMAP sinhronizacija posodobi mapo, ko je e-poštno sporočilo premeščeno na strani strežnika). Zaščita je neodvisna od stolpcev (primerjava na podlagi JSONB) in samodejno zajema prihodnje dodajanja v shemo.

### 🕐 Nastavitve časovne cone {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Časovna zona, ki se uporablja za prikaz časovnih žigov e-pošte v uporabniškem vmesniku. Uporablja identifikatorje časovnih zon IANA (npr. "Europe/Berlin", "Asia/Tokyo"). Privzeta vrednost je "Etc/UCT" zaradi združljivosti s starejšimi različicami. Ob uvozu bodo časovni žigi e-pošte pretvorjeni v to časovno cono za namene prikaza.

### 🎉 Nastavitve ReleaseNotes (Splash zaslon za posodobitev različice) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: String različice, ki ga ta instanca prijavlja. Primerjava poteka s GitHub oznakami izdaje (release tags) in določa, ali obstaja novejša različica. Če je prazno, se changelog umakne na verzijo kompajlirane assemblies in se opozorilo o posodobitvi izklopi.
- `ReleaseNotes__Enabled`: Omogoči ali onemogoči splash zaslon za posodobitev različice (true/false). Privzeto je `true`. Ko je omogočeno, bodo administratorji po posodobitvi aplikacije videli enkratno pojavno okno s spremenami (changelog), ki prikazuje zapiski o izdaji, pridobljene s GitHub Releases za trenutno različico. Vsak administrator lahko okno zapre in ponovno se bo pojavilo le za novo različico. Nastavite na `false` za popolno onemogočitev te funkcije, vključno z opozorilom o posodobitvi.
- `ReleaseNotes__UpdateHintEnabled`: Administratorjem prikaži majhno obvestilo (toast) v spodnjem desnem kotu, ko GitHub objavi novejšo stabilno različico kot je nameščena različica (true/false). Privzeto je `true`. Obvestilo je povezano z to izdajo in se v brskovalniku ne prikazuje več za posojeno različico. Predogledi (drafts) in predizdaje (prereleases) so prezrti. Zahteva `ReleaseNotes__AppVersion`; brez tega se opozorilo ne prikaže.
- `ReleaseNotes__UpdateCheckIntervalHours`: Kako pogosto se osveži seznam GitHub izdaj, v urah. Privzeto je `6`. Krajši interval naredi novo izdajo hitreje vidno v dolgotrajni instanci, daljši interval pa zmanjša število pozivov GitHub API. Minimum je `1`.

### 🔧 Nastavitve vzdrževanja baze podatkov {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Omogoči ali onemogoči samodejno dnevno vzdrževanje baze podatkov (true/false). Privzeto je `false`. Ko je funkcija omogočena, bo sistem samodejno izvajal operacije VACUUM ANALYZE za optimizacijo zmogljivosti baze podatkov in preprečevanje zameglitve (bloat). Za več informacij si oglejte [Database Maintenance Guide](DatabaseMaintenance.md).
- `DatabaseMaintenance__DailyExecutionTime`: Čas dne, ko se mora izvesti vzdrževanje baze podatkov, v 24-urnem formatu (HH:mm). Privzeto je `02:00`. Izberite čas med nizko aktivnostjo sistema.
- `DatabaseMaintenance__TimeoutMinutes`: Največ dovoljen čas za operacije vzdrževanja v minutah. Privzeto je `30`. Za večje baze podatkov povečajte to vrednost.

### 🧬 Nastavitve za deduplikacijo prilog {#attachment-deduplication-settings}
Deduplikacija prilog vsako unikatno vsebino priloge shrani le enkrat (identifikacija vsebine s SHA-256) in je **ključna funkcija, ki je vedno vključena** – namerno ni vklopljenega stikala za vklapljanje/izklop. Prilagoditi se lahko le spodnjim parametrom za obdelavo serij in načrtovanje. Za celovite podrobnosti si oglejte [Attachment Deduplication Guide](AttachmentDeduplication.md).

- `AttachmentDeduplication__BatchSize`: Število obstoječih prilog, prenesenih v transakciji med enkratno dejansko migracijo obstoječih podatkov v ozadju. Privzeto je `200`. Večje vrednosti omogočajo hitrejše migracije, vendar povečajo porabo pomnilnika/obremenitev baze podatkov na serijo.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Nepreobvezna pavza (v milisekundah) med serijami migracije za omejitev obremenitve baze podatkov na obremenjenih sistemih. Privzeto je `0` (brez pavze).
- `AttachmentDeduplication__StartupDelaySeconds`: Zamika (v sekundah) po zagonu aplikacije, preden se začne migracija v ozadju, da se migracija sheme lahko zaključi. Privzeto je `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Interval (v urah) stalnega čiščenja (garbage collection), ki odstranjuje vsebine prilog, ki jih več ne povezuje noben e-poštni zapis. Privzeto je `12`. To teče neodvisno od `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Čas izteka ukaza baze podatkov (v sekundah) za operacije serije migracije (INSERT s SHA-256 hashiranje in UPDATE). Privzeto je `300` (5 minut). Povečajte to vrednost za zelo velike baze podatkov ali priloge ali pa jo znižajte, če želite hitrejše zaznavanje napak. Če serija še vedno izteče, servis samodejno poskusi ponovno z polovišnja vrednostjo serije.

### 💾 Nastavitve shrambe računov {#account-storage-settings}
Prikaz shrambe na račun prikazuje porabo prostora v bazi podatkov (vsi polja e-pošte + priloge) za vsak račun v tabeli "Account Overview" v nadzornom panelu in v tabeli "Show All" v MailAccounts. Avtonomna ozadinska storitev (`AccountStorageRefreshService`) izračuna vrednosti tako, da sečte PostgreSQL `pg_column_size` po stolpcih (pokriva vsa polja e-pošte brez branja telesa e-pošte) in jih predrami v tabeli `AccountStorageCache`. Ta storitev teče neodvisno od `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Omogoči ali onemogoči storitev osveževanja shrambe (true/false). Privzeto je `true`. Ko je omogočena, storitev ob zagonu opravi obновеljivo dopolnjevanje (backfill) preostalih računov (varno pri neskladu prek `AccountStorageBackfillState`) in nato dnevno popolno osveževanje. Ko je onemogočena, se vrednosti shrambe posodobijo le, ko so e-poštne ladje sinhronizirane, uvožene ali izbristane, vendar se prikazane vrednosti lahko čez čas zastarajo.
- `AccountStorage__DailyExecutionTime`: Čas dneva (24-urnski format `HH:mm`) za dnevno popolno osveževanje vseh računov. Privzeto je `02:30`. Izberite čas med nizko aktivnostjo sistema.
- `AccountStorage__BackfillDelayMs`: Zamuditev (v milisekundah) med računi med začetnim dopolnjevanjem ob zagonu. Privzeto je `5000`. Nižje vrednosti pospešijo dopolnjevanje, vendar povečajo obremenitev baze podatkov; povečajte to vrednost za zelo velike arhive, da preprečite preobremenitev baze podatkov.
- `AccountStorage__RefreshBatchDelayMs`: Zamuditev (v milisekundah) med računi med dnevnim popolnim osveževanjem. Privzeto je `1000`. Nižje vrednosti pospešijo osveževanje, vendar povečajo obremenitev baze podatkov; povečajte to vrednost za zelo velike arhive.
- `AccountStorage__CommandTimeoutSeconds`: Čas izteka ukaza baze podatkov (v sekundah) za izračun shrambe na račun (`pg_column_size` nad vsemi vrsticami računa). Privzeto je `300` (5 minut). Izračun bere vsako vrstico e-pošte, vključno s TOAST podatki, kar lahko traja precej več kot minuto pri zelo velikih arhivih (250k+ e-poštnih sporočil); prejšnja 30-sekundna privzeta vrednost Npgsql je povzročala tiho neuspelo osveževanje in povzročila, da je nadzorni panel prikazoval zastarele vrednosti. Povečajte to vrednost, če izračun še vedno izteče v vašem arhivu.

> 💡 **Opomba**: Vrednosti shrambe se osvežijo takoj po vsaki sinhronizaciji e-pošte, uvozu ali izbrisu glede na politiko zadrževanja, zato so prikazane vrednosti aktualne tudi brez dnevnega osveževanja. Dnevno osveževanje je varnostna mreža, ki zajame izjemne primere (npr. neposredne spremembe v bazi podatkov).

### 📝 Nastavitve beleženja {#logging-settings}
- `Logging__LogLevel__Default`: Privzeta stopnja beleženja za aplikacijo. Razpoložljive stopnje so: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Privzeta vrednost je `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Stopnja beleženja za sporočila okvira ASP.NET Core. Privzeta vrednost je `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Stopnja beleženja za bazične ukaze Entity Framework. Privzeta vrednost je `Warning`.

### 🛡️ Nastavitve varnosti {#security-settings}
- `AllowedHosts`: Seznam gostiteljev, ločen s semicolon, ki jih je aplikacija dovoljena strežti. To pomaga preprečiti napade na HTTP Host header. Primer: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Pomembno**: V produkcijskih okoljih ne uporabljajte `*`, saj to onemogoča preverjanje host headerja.

#### 🔐 Šifriranje poveril v mirovanju {#credential-encryption-at-rest}
Poverila e-poštnega računa (IMAP geslo, M365 client secret in MSA OAuth tokene) je mogoče zašifrirati v bazi podatkov. Ključ za šifriranje se shranjuje **zunaj** baze podatkov (poslovna spremenljiva / Docker secret), zato sam izvoz baze podatkov (database dump) ni dovolj za branje poveril.

- `Security__CredentialEncryptionKey`: Base64 kodiran 256-bitni (32 byte) ključ za šifriranje (key encryption key). Generirajte ga z:
  ```bash
  openssl rand -base64 32
  ```
  Vrednost nastavite kot poslovno spremenljivo (priporočeno: `Security__CredentialEncryptionKey` v vašem datoteki compose ali okolju kontejnerja). Aplikacija ne podpira Docker secret v slogu `_FILE` — če uporabljate Docker secret, ga montirajte in naložite njegovo vsebino v poslovno spremenljivo v vašem entrypointu. Ključa nikoli ne objavljajte v `appsettings.json`. Ko je ključ konfiguriran, se poverila zašifrirajo ob zapisu, servis v ozadju pa enkrat zašifrir tudi obstoječa poverila v obliki čistega besedila.
- `Security__EncryptAccountCredentials`: Omogoči ali onemogoči šifriranje novih/posodobljenih vrednosti poveril (true/false). Privzeto je `true`. Ko ključ ni konfiguriran, se poverila shranjujejo kot čisto besedilo in ob zagonu se zabeleži opozorilo — obstoječe namestitve niso prijavljene, dokler ni zagotovljen ključ.
- `Security__CredentialEncryptionBackfill`: Ali naj servis v ozadju ob zagonu zašifrir obstoječa poverila v obliki čistega besedila (true/false). Privzeto je `true`. Če ključ ni konfiguriran, se ne bo nič zgodilo.
- `Security__CredentialEncryptionKeyPrevious`: Neobvezen prejšnji ključ, uporablja se le za dešifriranje vrednosti, ki so bile zašifrirane s tem ključem (za rotacijo ključev). Nove vrednosti so vedno zašifrirane s `Security__CredentialEncryptionKey`.

  > ⚠️ **Rotacija ključev**: po rotaciji ohranite `Security__CredentialEncryptionKeyPrevious` konfiguriran **dokler vsako poverilo ni ponovno zapisano** (npr. z urejanjem računa ali samodejno osvežitvijo OAuth tokena) — obstoječe vrednosti, zašifrirane s starim ključem, *niso* samodejno ponovno zašifrirane s novim ključem. Ko odstranite prejšnji ključ, postane vsako poverilo, ki je še zašifrirano s tem, nečitljivo in ga je treba ponovno vnesti. Dokler je konfiguriran, ga je treba skrbeti tako varno kot trenutni ključ.

> ⚠️ **Ključ varujte**: Brez ključa zašifriranih poveril ni mogoče povrniti in treba jih je ponovno vnesti. Združite jih z varnostno kopijo baze podatkov (toda ločeno od nje).
>
> 💡 **Obstoječe namestitve**: Nadgradite varno — dokler ni nastavljen `Security__CredentialEncryptionKey`, se nič ne spremeni. Ključ nastavite, ko ste pripravljeni; obstoječa poverila v obliki čistega besedila ostanejo čitljiva in se zašifrirajojo v ozadju.

### 🔐 Konfiguracija OIDC {#oidc-configuration}

Za podrobna navodila za nastavitev avtentikacije OpenID Connect poglejte [OIDC Implementation Guide](OIDC_Implementation.md).

#### Osnovne OIDC nastavitve {#basic-oidc-settings}
- `OAuth__Enabled`: Omogoči ali onemogoči OIDC avtentikacijo (true/false)
- `OAuth__Authority`: URL avtoritete OpenID Connect (npr. https://sts.windows.net/{TENANT-ID}/ za Azure AD)
- `OAuth__ClientId`: Klijentska identifikacija (client ID), ki jo je dodelil vaš ponudnik identitete
- `OAuth__ClientSecret`: Klijentska skrivnost (client secret), ki jo je dodelil vaš ponudnik identitete
- `OAuth__DisplayName`: Neprijetno ime, ki se prikazuje na gumbu za OIDC prijava in strani za samodejno preusmerjanje (npr. `PocketID SSO`). Če je izpuščeno, se uporabi splošni oznaka "Login with OAuth".
- `OAuth__ClientScopes__0`: Prvi scope, zahtevan od ponudnika identitete (openid)
- `OAuth__ClientScopes__1`: Drugi scope, zahtevan od ponudnika identitete (profile)
- `OAuth__ClientScopes__2`: Tretji scope, zahtevan od ponudnika identitete (email)
- `OAuth__ResponseMode`: Neprijeten način odziva (response mode) zazahtev za avtorizacijo (`query`, `fragment` ali `form_post`). Privzeto je `form_post`, privzeto za ASP.NET Core. Nastavite na `query` za ponudnike, ki ne podpirajo `form_post`, kot je Synology SSO Server. Poglejte [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Samodejno odobri nove OIDC uporabnike brez potrebe po ročnem odobritvi administratorja (true/false). Privzeto je `false`.

#### Nastavitve za zagotavljanje uporabnikov (User Provisioning) {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Samodejno odobri nove OIDC uporabnike brez potrebe po ročnem odobritvi administratorja (true/false). Privzeto je `false`. Ko je omogočeno, so uporabniki, ki se avtentikacijajo preko OIDC ponudnika, takoj aktivirani in lahko dostopajo do aplikacije. Ko je onemogočeno (privzeto), so novi OIDC uporabniki ustvarjeni kot neaktivni in zahtevajo ročno aktivacijo administratorja. Za podrobne informacije poglejte [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users).
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, itd.: E-poštni naslovi, ki bi morali biti samodejno zagotavljeni kot administratorji. Uporabniki z temi e-poštnimi naslovi bodo ob prvih OAuth prijavi ustvarjeni kot aktivni administratorji, s čimer obtočijo normalni postopek odobritve. Primerjava e-poštnega naslova ni odvisna od velikih in malih črk.

#### Nastavitve za prijavo brez gesla {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Onemogoči prijavo z uporabniškim imenom/geslom (true/false). Privzeto je `false`. Ko je omogočeno, je obrazec za geslo skrit **in** strežnik zavrne vsak POST za geslo, zato je na voljo le OAuth prijava. Zahteva `OAuth__Enabled=true`. Če je OAuth onemogočen, ta nastavitev nima učinka.
- `OAuth__AutoRedirect`: Samodejno preusmeri uporabnike na OAuth ponudnika (true/false). Privzeto je `false`. Zahteva, da je `OAuth__DisablePasswordLogin` nastavljen na `true`. Uporabniki bodo pred preusmeritvijo videli kratek zaslon za nalaganje.

#### Primer: Celotna OIDC-First konfiguracija {#example-full-oidc-first-configuration}
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

Čeprav je aplikacija namenjena dostopu prek obversega proxyja s protokolom HTTPS, lahko nastavite tudi spletni strežnik Kestrel za uporabo SSL/TLS certifikatov. To zagotavlja končnu enkripcijo med obversem proxyjem in poslodovalnim kontejnerjem aplikacije.

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

5. **Postavite datoteko certifikata** (npr. `localhost.pfx`) v direktorij `./certs` na svojem gostiteljskem sistemu.

### Razlagava spremenljivk okolja {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: URL končnice HTTP (privzeto: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: URL končnice HTTPS (privzeto: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Pot do datoteke PFX certifikata znotraj kontejnerja
- `Kestrel__Endpoints__Https__Certificate__Password`: Geslo za datoteko PFX certifikata

> 💡 **Opomba**: Ta konfiguracija je neobvezna. Če uporabljate obversem proxy s protokolom HTTPS (priporočeno), lahko komunikacija med obversem proxyjem in aplikacijo ostane na protokolu HTTP. Vendar pa za maksimalno varnost v občutljivih okoljih morda želite omogočiti tudi HTTPS na Kestrelu, da zašifrirate celotno pot komunikacije.

## 🔑 Upravljanje tajnosti za produkcijo {#secrets-management-for-production}

Neposredno vnašanje občutljivih podatkov, kot so gesla za bazo podatkov, administratorjeva poverilila in OAuth client secrets, neposredno v `docker-compose.yml` predstavlja varnostno tveganje, še posebej, ko je datoteka shranjena v sistemu za nadzor različic. Priporočan pristop je izvajanje teh vrednosti v datoteko `.env`, ki jo Docker Compose samodejno naloži.

### 📄 Datoteka `.env` {#the-env-file}

V istem direktoriju kot vaša `docker-compose.yml` ustvarite `.env` datoteko z naslednjo vsebino:

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

**Pomembno**: Docker Compose samodejno bere `.env` datoteko iz istega direktorija — ni vam treba na njo referencirati ročno. Vse spremenljivke, definirane v `.env`, so na voljo v `docker-compose.yml` prek sintakse `${VARIABLE_NAME}`.

### 🐳 Prilagojena `docker-compose.yml` {#adapted-docker-composeyml}

`docker-compose.yml` iz korakov za namestitev je treba prilagoditi tako, da namesto trdno vnešenih tajnosti uporablja oklepce (placeholders):

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

### 📋 `.env.example` Predlogo za administratorje {#envexample-template-for-administrators}

V svojem repozitoriju ustvarite `.env.example` datoteko, ki služi kot predloga za druge administratorje. Vseže vse oklepce in dokumentacijo, vendar **brez dejanskih tajnosti**:

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
- **Nastavite stroge dovoliljenja za datoteke** — Omejite dostop do datoteke `.env`:
  ```bash
  chmod 600 .env
  ```
- **Uporabite močna gesla** — Generirajte dolga, naključna gesla (vsaj 16 znakov) z mešanico črk, številk in posebnih znakov.
- **Omejite dostop do datoteke `.env`** — Le uporabnik, ki izvaja Docker Compose, ima dovoljenja za branje datoteke `.env`.

## 🔒 Varnostni opomniki {#security-notes}

- Uporabite močna gesla in spremenite privzete podatke za prijavo. Gesla morajo biti dolga vsaj 12 znakov in vključevati kombinacijo velikih in malih črk, številk ter posebnih znakov. Izogibajte se uporabi pogostih besed ali podatkov, ki jih je lahko enostavno ugotoviti.
- Za produkcijo upoštejte implementacijo HTTPS z obratnim posrednikom (reverse proxy).
- Priporočljivo je redno opravljanje varnostnih kopij PostgreSQL baze podatkov. Za podrobne postopke kopiranja in obujave poglejte [Backup and Restore Guide](BackupRestore.md).
