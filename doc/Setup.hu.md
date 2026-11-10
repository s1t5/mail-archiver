# 🛠️ Mail Archiver beállítási útmutató {#mail-archiver-setup-guide}

[← Vissza a dokumentációs indexhez (index.md)](index.md)

## 📋 Áttekintés {#overview}

Ez az útmutató részletes utasításokat tartalmaz a Mail Archiver alkalmazás felállításához Docker Compose használatával.

## 🛠️ Előfeltételek {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Telepítési lépések {#installation-steps}

1. Telepítse a szükséges előfeltételeket a rendszerére.

2. Hozzon létre egy `docker-compose.yml` fájlt a következő tartalommal:

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

3. Szerkesztse az adatbázis konfigurációt a `docker-compose.yml` fájlban, és állítson be egy biztonságos jelszót a `POSTGRES_PASSWORD` változóban és a `ConnectionString` területen.

4. Amennyiben hitelesítést szeretne használni (amit erősen javasolunk), határozzon meg egy `Authentication__Username` és `Authentication__Password` értéket, amely az admin felhasználóhoz tartozik.

5. A `TimeZone__DisplayTimeZoneId` környezeti változót az Ön által preferált időzónához igazítsa (alapértelmezett: "Etc/UCT"). Bármely IANA időzónévidentifikátort használhat (pl. "Europe/Berlin", "Asia/Tokyo").

6. Opcionálisan konfigurálhatja a `Logging__LogLevel` környezeti változókat az alkalmazási logok részletességének szabályozására. Az elérhető lehetőségekért lásd az alábbi Logging Settings szekciót.

7. Konfiguráljon egy Ön által választott reverse proxy-t https és hitelesítéssel az alkalmazásos hozzáférés biztonságosításához.

> ⚠️ **Figyelem**: Mag本身 az alkalmazás nem biztosít titkosított hozzáférést https keresztül! Ezt egy reverse proxy segítségével kell beállítani!

8. A konténerek kezdeti indítása:
```bash
docker compose up -d
```

9. Konténerek újraindítása:
```bash
docker compose restart
```

10. Az alkalmazás elérése

11. Lépjen be a meghatározott hitelesítő adatokkal, és adja hozzá az első e-mail fiókját:
   - Navigáljon az "Email Accounts" szekcióba
   - Kattintson a "New Account" gombra
   - Adja meg a szerver részleteit és a hitelesítő adatokat
   - Mentés és az archiválás megkezdése!
   - Ha szeretné, hozzon létre további felhasználókat és rendeljen hozzájuk fiókokat.

## 📚 Környezeti változók magyarázata {#environment-variable-explanations}

### 🗄️ Adatbázis-kapcsolat {#database-connection}
- `ConnectionStrings__DefaultConnection`: A PostgreSQL adatbázishoz való csatlakozási karakterlánc. Szükség esetén módosítsa a `Host`, `Database`, `Username`, és `Password` értékeket.

### 🔐 Hitelesítési beállítások {#authentication-settings}
- `Authentication__Username`: A admin fiók felhasználóneve.
- `Authentication__Password`: A admin fiók jelszava.
- `Authentication__SessionTimeoutMinutes`: A munkamenet lejárati ideje perces egységben. Ez a nem tartós (non-persistent) bejelentkezési cookie-ra vonatkozik; azok a felhasználók, akik a bejelentkezési űrlapon bepipálják a **Remember Me** opciót, helyette egy fix 7 napig érvényes, tartós (persistent) cookie-t kapnak. See [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: A hitelesítési cookie neve.
- `Authentication__CookieSameSite`: A hitelesítési, munkamenet (session) és CSRF védelem miatti cookie-k SameSite attribútumának konfigurálása. Érvényes értékek:
  - `Strict` (alapértelmezett): Maximális biztonság. A cookie-k csak ugyanazon a webhelyen (same-site) történő kérésekkel küldenek. Ez problémákat okozhat, ha külső linkről navigálunk az alkalmazáshoz (például egy másik weboldalról kattintunk egy linkre), mivel a meglévő munkamenet nem lesz felismerhető.
  - `Lax`: Ajánlott reverse proxy használata esetén. A cookie-k a legfelső szintű navigációk (top-level navigations) és ugyanazon a webhelyen történő kérések során is elküldésre kerülnek, lehetővé téve a felhasználók számára, hogy külső linkeken keresztül navigáljanak az alkalmazáshoz, miközben megmarad a CSRF védelem a POST kérésekhez.
  - `None`: A cookie-k minden kérésrel küldésre kerülnek. HTTPS és a `Secure` attribútum szükséges. Csak akkor használja, ha specifikus cross-site igényei vannak, és érti a biztonsági következményeket.

### 🔌 REST API Beállítások {#rest-api-settings}
Az opcionális, csak olvasható REST API alapértelmezetten **ki van kapcsolva**. A teljes útmutatóért lásd a [REST API guide](API.md) dokumentációt.
- `Api__Enabled`: A csak olvasható REST API fő kapcsolója (alapértelmezett `false`). Ha `false`, minden `/api/*` útvonal `404` választódást ad vissza.
- `Api__AllowAttachmentDownloads`: Engedélyezi a csatolmány byte-ok letöltését az API keresztül (alapértelmezett `true`). Ha `false`, a csatolmány végpont `403` választódást ad vissza.
- `Api__AllowMessageDownloads`: Engedélyezi a teljes üzenetek letöltését az API keresztül, mind az újrabszerkesztett `/eml`, mind az eredeti `/raw` formátumban (alapértelmezett `true`). Ha `false`, mindkét végpont `403` választódást ad vissza. Egy teljes üzenet tartalmazza a csatolmányait, ezért **mind** ez a kapcsoló, **mind** a `Api__AllowAttachmentDownloads` kapcsolónak `true` kell lennie; bármelyik kikapcsolása mindkét végpontot elérészíti.
- `Api__EnableSwaggerUi`: A Swagger UI elérhető `/apidocs` helyen, az OpenAPI dokumentum pedig `/apidocs/spec/v1.json` helyen (alapértelmezett `true`). Mindkéthez bejelentkezett böngésző-sesszió szükséges.
- `Api__DefaultPageSize`: Az alapértelmezett oldalméret a listázó végpontokhoz, ha nincs megadva (alapértelmezett `20`).
- `Api__MaxPageSize`: A maximálisan megengedett oldalméret; a nagyobb kéréseket korlátozzák (alapértelmezett `100`).
- `Api__RateLimitPerMinute`: Fix ablakos kérések költségvetése API-kulcsonként percenként (alapértelmezett `120`).

### 🤖 MCP Server Beállítások {#mcp-server-settings}
Az opcionális MCP (Model Context Protocol) szerver ugyanazt a csak-olvasási levél- hozzáférést biztosítja az AI ügynökök számára a Streamable HTTP transzpor través, ugyanúgy, mint a REST API. Alapértelmezés szerint **le van kapcsolva**. A teljes útmutatóért lásd az [MCP guide](MCP.md) dokumentációt. Az MCP ugyanazokat az API kulcsokat használja, mint a REST API (`ma_...` bearer tokenek), és ugyanazt a felhasználónkénti fiók-Hozzáférési korlátozást (scoping).
- `Mcp__Enabled`: A fő kapcsoló az MCP szerverhez (alapértelmezett `false`). Ha `false`, a `/mcp` végpont `404`-öt ad vissza.
- `Mcp__AllowAttachmentDownloads`: Engedélyezi, hogy a `get_attachment` eszköz csatolt fájlok (attachment) byteit visszaadja (alapértelmezett `true`). Ha `false`, az eszköz hibát ad vissza.
- `Mcp__DefaultPageSize`: Az `search_emails` által használt alapértelmezett oldalméret, ha a hívó elmaradta a `pageSize` paramétert (alapértelmezett `20`).
- `Mcp__MaxResults`: A `search_emails` oldalméretének felső határa; a nagyobb értékek korlátozásra kerülnek (alapértelmezett `100`).
- `Mcp__MaxAttachmentBytes`: A maximális csatolt fájl mérete (byte-okban), amelyet a `get_attachment` eszköz inline formátumban, base64 kódolással ad vissza; a nagyobb csatolmányokat elutasítjuk (alapértelmezett `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Engedélyezi, hogy a `get_email_eml` és a `get_email_raw` eszközök teljes üzeneteket adjanak vissza (alapértelmezett `true`). Ha `false`, az eszközök hibát adnak vissza. Egy teljes üzenet tartalmazza a csatolmányait, ezért **mind** ez a kapcsoló, **mind** az `Mcp__AllowAttachmentDownloads` értéknek `true` kell lennie; bármelyik kikapcsolása mindkét eszközt letiltja.
- `Mcp__MaxMessageBytes`: A maximális üzenetméret (byte-okban), amelyet a `get_email_eml` és a `get_email_raw` eszköz inline formátumban ad vissza; a nagyobb üzeneteket elutasítjuk (alapértelmezett `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Fix ablakos kérés-keret (request budget) API kulcsonként percenként (alapértelmezett `120`).

### 📨 MailSync Beállítások {#mailsync-settings}
- `MailSync__IntervalMinutes`: Az e-mail szinkronizálás közötti intervallum percenként. Ez a globális alapértelmezett; minden fiók egyen individually felülírhatja azt a Create/Edit oldalon (hagyja üresen a használatához).
- `MailSync__FullSyncIntervalHours`: Opcionális globális alapértelmezett az automatikus teljes újra szinkronizálásra, órákban megadva. Ha nincs beállítva (az alapértelmezett), nem fut automatikus teljes szinkronizálás, hacsak egy fiókra vonatkozó `FullSyncIntervalHours` érték nincs megadva a Create/Edit oldalon. A fiókonkénti értékek felülírják ezt a globális alapértelmezettet.
- `MailSync__TimeoutMinutes`: Fiókonkénti szinkronizálási timeout percenként. A szinkronizálás, amely tovább tart, a következő üzenet határonál megáll, megőrzi a checkpointjeit, és folytatja a következő futtatáskor; a feladatot `Timed Out` jelenti, és a `LastSync` nem halad előre. Csak ütemezett szinkronizálásokra vonatkozik; a felületről elindított szinkronizálás (manuális szinkronizálás vagy teljes újra szinkronizálás) timeout nélkül fut. A `0` (alapértelmezett) vagy bármely nem pozitív érték azt jelenti, hogy nincs timeout. A nagy levelezési adatbázisok szinkronizálása előtt ellenőrizze az értéket. Lásd: [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Az IMAP kapcsolatok MTU-timeoutja másodpercekben.
- `MailSync__CommandTimeoutSeconds`: Az IMAP parancsok command timeoutja másodpercekben.
- `MailSync__AlwaysForceFullSync`: Hogy mindig kényszerítsen teljes szinkronizálást (true/false).
- `MailSync__IgnoreSelfSignedCert`: Hogy figyeljen-e el tovább a self-signed certifikátokat (true/false).
- `MailSync__MaxConcurrentSyncs`: Mennyi fiók szinkronizálása futhat egyszerre. Alapértelmezett `1` (szekvenciális, hátralévő kompatibilitás). Az ükemező olyan helyeket tölti fel, amint azok szabadon állnak, ahelyett, hogy egy egész batch-re várt volna, így egy lassabb mailbox már nem tartja vissza a többi folyamatot. Növelje a több fiók egyidejű szinkronizálásához — vegye figyelembe a szolgáltató sebességhatárait és a helyi erőforrás-használatot. Lásd: [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Opcionális csúszó (stagger) késleltetés másodpercekben, amely minden fiók szinkronizálási feladat végén alkalmazandó. Alapértelmezett `0` (nincs késleltetés). Hasznos a hirtelen kezdések (burst-starts) elkerülésére, amikor `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Hány problémát emlékszik meg a szinkronizálási feladat a fiók oldalán egyes típusok szerint. A sikertelen mappák, hiányzó mappák és sikertelen üzenetek külön költségvetéssel rendelkeznek, így egy típusból érkező tömeg nem tudja eltávolítani a többi típust a nézetből; a költségvetésen túlmutató bármi csak számlálódik, nem pedig tárolódik. Alapértelmezett `20`. A `0` kikapcsolja a részleteket, és csak a számlálókat hagyja maga után. Lásd: [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Mely mappák kerülnek kivetve a szinkronizálásból **minden** fiók esetében, a fiók saját kizárási listáján felül. Alapértelmezés szerint üres, így a meglévő beállítások nem érintettek. A két lista **kiegészítő** jellegű — egy mappát akkor is kihagy, ha bármelyik listában szerepel — és mindkettő ugyanazokat a szabályokat alkalmazza: pontos egyezés a teljes útvonalon, pontos egyezés a mappa nevén, útvonali suffix egyezés (így a `Drafts` a `INBOX/Drafts` és `INBOX.Drafts` esetében is egyezik), és minden, ami a mappa alatt található, amikor a `MailSync__ExcludeSubfolders` be van kapcsolva. Az egyezés érzékeny a kis- és nagybetűre. Hasznos sok mailbox importálásakor ugyanazon a szerverről, ahol a másik lehetőség minden fiókra azonos kizárási lista fenntartása. Alapértelmezés szerint nincs kivetett mappa; hogy mely nevek érdemesek felsorolni, az a szerver és a nyelve határozza meg. Példa egy mailbox fa-struktúrára, amely tartalmaz kalendár és névjegy mappákat is:
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
- `MailSync__ExcludeSubfolders`: A kizárási bejegyzés lefed-e egy olyan mappát is, amely az említett mappa alatt található. Ez egyaránt vonatkozik a fiókonkénti listára és a telepítés egészére vonatkozó listára, valamint mindkét szolgáltatóra. Alapértelmezett `false`, ami megegyezik a beállítás létezése előtti viselkedéssel: egy bejegyzés `Deleted Items` csak azt a mappát fede le, és egy fa-struktúra megtartása a tárolásnál a gyökér és minden alatta lévő mappa megnevezésével. Ha azt szeretné, hogy egy bejegyzés a teljes fa-struktúrát is lefedje, állítsa `true` értékre — egy `Deleted Items` bejegyzés ilyenkor a `Deleted Items/2024` és a maradék ág részeket is kivételi az archiválásból. A szabály az útvonal-szeparátorra rögzül, így egész mappákat fed le, és soha nem egy név részét: `Archive` lefedélyezi a `Archive` és a `Archive/Team` esetét, és magát a `Team Archive Review` nevű levelezési mappát is elhagyja. A `.` szintén számlál úgy, ahogy a path-suffix szabály is kezeli.

### 📧 Eredeti üzenetek tárolási beállításai {#original-message-storage-settings}

Alapértelmezés szerint az archiválás csak az e-mailek feldolgozott részeit tárolja, így egy exportált `.eml` egy rekonstruált változat: a transfer kódolások, a MIME határok és a header törés eltérhetnek a megérkezett üzenettől, a DKIM aláírások pedig már nem igazolhatók. `RawMessages:Enabled` esetén az eredeti teljes üzenet is tárolásra kerül.

| Paraméter | Alapértelmezett | Cél |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Minden új archivált e-mail eredeti változatának tárolása. |
| `RawMessages__Storage` | `postgres` | Ahely, ahol a byte-ok találhatóak: `postgres` vagy `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | A nagyobb üzenetek az eredeti változat nélkül kerülnek archiválásra. `0` = nincs limit. |
| `RawMessages__IncludeGraph` | `true` | Microsoft 365 (Graph) fiókok esetén is tárolja az eredetiket. |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Az üzenetfájlok mappája. Dockerben mountolt volumenre kell mutatnia. |
| `RawMessages__FileSystem__ShardDepth` | `2` | A mappaszintek, amelyeken az e-mail id el van osztva. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | SHA-256 ellenőrzése minden letöltésnél. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Az olyan üzenetfájlok eltávolítása, amelyekhez már nincs e-mail hivatkozás. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Gyakرتség, amennyivel ez a tisztítás fut le. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Az ilyennél fiatalabb fájlok soha nem kerülnek eltávolításra. |

!!! warning "A fájlrendszer háttérnek szüksége van egy mountolt volumenre"
    `RawMessages__Storage=filesystem` esetén az üzenetfájlok aຖານ adatbázison kívül élnek.
    Bind mount nélkül a konténer írható rétegében vannak, és a következő konténer újrakreáláskor
    elveszítődnek, míg az adatbázis sorai megmaradnak. A mappát az adatbázis mentéssel együtt
    mentse vissza.

Lásd a **[Original Message Storage](RawMessages.md)** dokumentációt a teljes leírásért: hová
szolgáltatja az eredetit, a tárolási háttértechnológiák, hogyan lehet a meglévő üzeneteket
mozgatni közöttük a `--migrate-raw-storage` segítségével, és mit kell menteni.

### 📤 BatchRestore Beállítások {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Az e-mailek száma, amely aszinkron feldolgozást indít.
- `BatchRestore__MaxSyncEmails`: A szinkron feldolgozáshoz szükséges e-mailek maximális száma.
- `BatchRestore__MaxAsyncEmails`: Az aszinkron feldolgozáshoz szükséges e-mailek maximális száma.
- `BatchRestore__SessionTimeoutMinutes`: A batch restore session timeout percenként.
- `BatchRestore__DefaultBatchSize`: Az e-mail műveletek alapértelmezett batch mérete.

### 🏢 Bérlőkezelési beállítások {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: A single Tenant Management művelet során hozzáadható postafiókok maximális száma. Az alapértelmezett érték `1000`. Növelje ezt nagyon nagy bérlők esetén, vagy csökkentse a véletlen tömeges importok megelőzésére. Ha a limite meghaladódik, a művelet hibával utasítva kerül elutasításra, és nincs létrehozott fiók. Részletekért lásd a [M365 Tenant Import Guide](M365TenantImport.md) dokumentációt.

### 🐄 Mailcow beállítások {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: A single Mailcow aktiválási futtatás során létrehozható postafiókok maximális száma. Az alapértelmezett érték `1000`. Csökkentse az értéket a véletlen tömeges létrehozás megelőzésére, vagy emelje azt nagy példányok esetén. Ha a limit túllépésre kerül, nem kerülnek létre fiókok. Részletekért lásd a [Mailcow Instances](Mailcow.md) részt.
- A Mailcow integráció emellett `Security__CredentialEncryptionKey`-t igényel (lásd a [Security Settings](#-security-settings) részt). Egy példány csatlakoztatása és a postafiókok aktiválása is elutasításra kerül, amíg a hitelesítő adatok tit加密 (credential encryption) kikapcsolva van, így az API kulcs és a generált alkalmazási jelszavak soha nem tárolhatók szöveges formátumban.
- Külső hozzáférés szükséges a Mailcow HTTPS API-hoz, valamint az IMAP hosthoz TLS felett (általában `993` porton).

### 📦 BatchOperation Beállítások {#batchoperation-settings}
- `BatchOperation__BatchSize`: Az e-mail műveletek batch mérete.
- `BatchOperation__PauseBetweenEmailsMs`: A szünet az egyes e-mailek között millisekundokban kifejezve.
- `BatchOperation__PauseBetweenBatchesMs`: A szünet a batch-ek között millisekundokban kifejezve.

### 📤 Offload Beállítások {#offload-settings}
A [date-windowed offload](Offload.md) beállításai. Az alapértelmezett értékek visszaállítják az alkalmazás korábbi viselkedését, így a telepítés, amely nem konfigurálja ezt a szekciót, nem érintett.
- `Offload__MaxConcurrentJobs`: Hány visszaállítási vagy offload feladat futhat egyszerre. Az alapértelmezett érték `1`, amely megőrzi a feladat soros (serial) feldolgozását, amelyet a feladatlista eddig mindig alkalmazott. Ebben az értékben függetlenül legfeljebb egy feladat futhat céllevelezőbõlcsenként.
- `Offload__PrefetchMaxMessages`: A felső határ arra vonatkozóan, hogy hány üzenet indexelhető egy céllevelezőbőlből a duplikáció detektálása érdekében. Az alapértelmezett érték `500000`. E felett a ellenőrzés egyetlen mappára szűkül le, és logolja, hogy a kör csökkentve lett.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Forrásmappák, amelyek soha nem kerülnek offload alá, átnevezés előtt kerülnek megkeresésre és tartalmazzák a mappalábrendezéseket. Alapértelmezés szerint üres.
- `Offload__FolderRenameMap__<SourceFolder>`: Egy forrásmappa elérési út első szegmensének átírása, például `Offload__FolderRenameMap__Sent Items=Sent`. Alapértelmezés szerint üres.
- `Offload__MarkAsSeen`: Ez határozza meg, hogy a hozzáadott leveleket olvasottnak jelöli-e a rendszer. Az alapértelmezett érték `true`, amely megegyezik a meglévő visszaállítási viselkedéssel.

Mindkét mappa beállítás szándékosan üres: a mappák átírása vagy törlése kérés nélkül meglepetés lenne mindenkinek, aki már használja a visszaállítási folyamatot. Ezért egy konfiguráció nélküli futtatás mindent migrál (beleértve a spam mappákat is), és létrehoz egy második speciális mappák készletet a célmappók mellett.

### 📊 Bandwidth Tracking Beállítások {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Engedélyezi vagy letiltja a sávszélesség ny tracking funkcióját az IMAP sebességkorlátozás kezeléséhez (true/false). Az alapértelmezett érték `false`. Ha engedélyezve van, a rendszer fiókonként követi a sávszélesség felhasználását, és szinkronizálást állíthat le, ha a szolgáltató korlátai elérhetővé st. Lásd a [Rate Limit Handling](RateLimitHandling.md) rész a részletes információkért.
- `BandwidthTracking__DailyLimitMb`: Napi letöltési limit megabajtókban, fiókonként. Az alapértelmezett érték `25000` (25 GB). A sávszélesség-korláttal rendelkező szolgáltatóknál állítsa ezt az értékét a sebességkorlátozásuknak megfelelően (például `2500` a ~2500 MB napi limitrel rendelkező szolgáltatók esetén). A rendszer szinkronizálást állít le, ha ez a limit elérhetővé válik.
- `BandwidthTracking__WarningThresholdPercent`: A napi limit százalékos értéke, amelynél figyelmeztető üzenetek vannak naplózva. Az alapértelmezett érték `80`. Amikor a sávszélesség felhasználása eléri ezt a százalékot, figyelmeztetések kerülnek naplózásra, hogy segítsenek a közelgő korlátok monitorozásában.
- `BandwidthTracking__PauseHoursOnLimit`: Az órák száma, amennyi ideig a szinkronizálás szünetel, ha a napi limit elérhető. Az alapértelmezett érték `24`. Ezen időszak után a limit jelző automatikusan törlődik, és a szinkronizálás folytatódik.
- `BandwidthTracking__TrackUploadBytes`: Követi-e a feltöltési sávszélességet is (true/false). Az alapértelmezett érték `false`. A legtöbb IMAP szolgáltató csak a letöltést korlátozza, így ez általában nem szükséges.

### 🎯 Kiválasztási beállítások {#selection-settings}
- `Selection__MaxSelectableEmails`: Az e-mailek maximális száma, amely egyszerre kiválasztható.

### 👁️ Megtekintési beállítások (Adatvédelem és megjelenítés) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Kontrollálja az e-mailek alapértelmezett megtekintési módját a magánélet és a nyomtatás megelőzése érdekében (true/false). Az alapértelmezett érték `false`.
  - `true` beállítás esetén: Az e-mailek alapértelmezés szerint plain-text módban nyílnak meg, megakadályozva a tracking pixel, külső képek és web-beaconok automatikus betöltését. Ez ajánlott az e-mail nyomtatás (tracking) és az adatvédelem iránt érdeklődő felhasználóknak.
  - `false` beállítás esetén: Az e-mailek alapértelmezés szerint HTML módban nyílnak meg, megjelenítve a formázott e-mailt képekkel és stílusokkal.
  - A felhasználók az e-mail részleteinek oldalán található gombbal váltogathatnak az HTML és a plain-text nézetek között (csak akkor látható, ha mindkét formátum elérhető).
  - A "Full View" link figyelembe veszi a jelenleg kiválasztott megtekintési módot.

- `View__BlockExternalResources`: Blokkolja a külső erőforrásokat (távoli képek, külső CSS, külső szkriptek, webes betűtípusok stb.) az HTML e-mail nézetekben a nyomtatás megelőzése és a magánélet javítása érdekében (true/false). Az alapértelmezett érték `false`.
  - `true` beállítás esetén: A külső erőforrásokat kiszűrik az HTML e-mailek megjelenítésekor. Csak az inline tartalmak és a data URI-k (beleértve a `cid:` hivatkozáson keresztül elérhető inline csatolmányokat) jelennek meg.
  - `false` beállítás esetén: Az HTML e-mailek az összes eredeti külső erőforrásukkal együtt jelennek meg.
  - **Fontos**: Ez a beállítás csak az e-mail **megjelenítésére** hatással van. A archivált e-mailek változatlanul, az összes eredeti tartalommal megőrizve tárolódnak az adatbázisban.
  - A blokkolt erőforrások közé tartozik:
    - Távoli képek (tracking pixelek, külső szervereken tárolt képek)
    - Külső CSS stílus táblázatok
    - Külső betűtípusok @font-face használatával
    - Külső CSS importálások @import használatával
    - Külső háttérképek
  - Engedélyezett erőforrások:
    - Beágyazott képek data: URI formátumban
    - Beágyazott csatolmányok cid: URI hivatkozással
    - Inline CSS stílusok és stílus tagek
  - Ez a beállítás függetlenül működik a `DefaultToPlainText` beállítástól, és additional adatvédelmi védelmi réteget biztosít HTML e-mailek megtekintésekor.

### 📊 Dashboard Settings {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Azzal kapcsolatos, hogy a számláló kártyák tartalmazzák-e a beérkező és kimenő részeket, az обліves kártya pedig tartalmazza-e a különböző domainok számát. Az alapértelmezett érték `true`. Ha `false` értékre állítjuk, ezek a számok nem számolódnak ki, hanem egyszerűen megjelennek: a számlálók egyszerű számot mutatnak, a csatolmányok számlálója pedig nem kapcsolódik hozzá a levélhez, amire rögzítve van, ami az egyetlen lekérdezés, amit ezek a részek hozzátesznek.
- `Dashboard__SelectablePeriods`: Azzal kapcsolatos, hogy a dashboard diagramok kínálnak-e fel felbontást, egy időszakot és nyilvántartást a mozgatáshoz. Az alapértelmezett érték `true`. Ha `false` értékre állítjuk, a diagramok az utolsó tizenkét hónapot mutatják havi bontásban és az egész archívum küldőit; a diagram végpontja úgy válaszol, mintha a végpont nem is létezne, és a leghivatalosabb küldési dátumot nem keresi meg.
- `Dashboard__CacheSeconds`: Mennyi ideig tárolják a kiszámított dashboard statisztikákat (összesítések a beérkező és kimenő bontással, fiókonkénti számlálók, a diagram sorozatok, leggyakoribb küldők, legutóbbi e-mailek, adatbázis mérete) a szerver memóriában tárolt gyorsítótárában (in-memory cache). Az alapértelmezett érték `60` másodperc. Ha `0` értékre állítjuk, a gyorsítótárazás kikapcsolódik, és a statisztikák mindig újra számolódnak. A felbontás és az időszak diagramonként külön kerül gyorsítótárazásra, így egy olyan dashboard, amely vált a kivitelezések között, minden kivitelezéshez egyetlen bejegyzést tart fenn. A nagyobb értékek csökkentik az adatbázis terhelését nagy környezetekben, de cserébe elavultabb számokat adhatnak. A szinkronization állapot jelzők, a tárolási értékek és a legutóbbi e-mailek listája mindig élő adatokból kerülnek lekérdezésre, és nem befolyásolja őket ez a gyorsítótár.
- `Dashboard__RefreshIntervalMinutes`: Milyen gyakran számolja újra a `DashboardStatsRefreshService` háttérszolgáltatás a költséges dashboard összesítéseket (összesített számlálók a beérkező és kimenő bontással, fiókonkénti számlálók, a fiókok domainjeinek száma, az alapértelmezett diagram sorozat a leggyakoribb küldőkkel, adatbázis mérete) a `DashboardStatsCache` adatbázis táblázatba. Több millió e-mailt tartalmazó telepítéseknél a dashboard ezeket az előkészített értékeket olvassa ki a teljes archívum lekérdezés közbeni összesítése helyett, ami a ~10 másodperces első betöltést millisekundok gyorsaságára gyorsítja. A diagram végpontja a megadott alapértelmezett kivitelezésre (havi bontás egy évre, beérkező küldők, a jelenlegi időszak) ugyanabból az oszlopból válaszol, így visszatérve az alapértelmezett nézetre az első megjelenítés számaiban látjuk az adatokat; minden egyéb kivitelezést a lekérdezéskor számolják ki, és a fenti memóriában tárolt gyorsítótárban tárolják. Az alapértelmezett érték `15` perc. Ha `0` értékre állítjuk, a háttérben történő frissítés kikapcsolódik; ilyenkor a dashboard újra élő adatokból számolja ki a statisztikákat (a fenti memóriában tárolt gyorsítótár továbbra is érvényes). A megjelenített értékek akár a beállított intervallummal elmaradhatnak a tényleges archívum állapotától. Egy olyan sor, amely más `ShowDirectionSplits` vagy `SelectablePeriods` értékekkel készült, mint amilyenek a jelenlegiek, nem kerül felhasználásra; a dashboard élő módon számol tovább a következő frissítésig.

### 🔄 Jobs Settings {#jobs-settings}
- `Jobs__RefreshSeconds`: Mennyire gyakran töltődik újra magától a háttérfolyamatok (background jobs) oldala, amíg egy böngészőfül nyitva van rá. Az alapértelmezett érték `30` másodpercenként van beállítva. A kézi frissítéshez állítsa `0` értékre az automatikus újratöltés kikapcsolásához. Minden újratöltés az összes nyolc forrásból építi újra az oldalt, így sok fiókot tartalmazó telepítéseknél a hosszabb intervallum csökkenti a terhelést, amelyet minden nyitott böngészőfül szorzóként befolyásol.

### 🗃️ Npgsql Beállítások {#npgsql-settings}
- `Npgsql__CommandTimeout`: Az adatbázzis parancsok időtúllépési ideje másodpercekenként.

### 📥 Feltöltési beállítások {#upload-settings}
- `Upload__MaxFileSizeGB`: A feltöltések maximális fájlmérete GB-ban.
- `Upload__KeepAliveTimeoutHours`: A feltöltések élettartam (keep alive) timeout ideje órában.
- `Upload__RequestHeadersTimeoutHours`: A kérések fejléceinek (request headers) timeout ideje órában.

### 📂 Helyi importálási beállítások {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, stb.: A helyi directory-ek fehérlistája, amelyből a CLI import parancsok (`--import-mbox`, `--import-eml`) fájlokat olvashatnak. Minden bejegyzés egy path a container binnen. A Docker volume-ök segítségével kell a beimportálandó fájlokat valamelyik ezek közül egy directory-be mountolnia. Alapértelmezett: `/data/import` (automatikusan `/app/uploads` értékre állítva fallbackként).
  - Amikor `docker exec`-ot használunk az import parancs futtatásához, a `--file` által megadott fájl elérési út must (must) belül kell legyen az egyik engedélyezett path között.
  - Ez a biztonsági intézkedés megakadályozza a tetszőleges fájlrendszer hozzáférést a CLI parancsokból.
  - Különböző import forrásokhoz több path is konfigurálható.
  - Részletes használati útmutatóhoz tekintse meg a [CLI commands](CLI.md) részt.

### 📄 CSV Import Beállítások {#csv-import-settings}
- `CsvImport__MaxRows`: A single tömegimportálás során feldolgozott CSV sorok (postafiókok) maximum száma. Az alapértelmezett érték `5000`. Növelje ezt a értéket nagy implementációk esetén; csökkentse, hogy korlátozza egyetlen import futás hatását az adatbázz terhelésére.
- `CsvImport__MaxFileSizeBytes`: A feltöltött CSV fájl maximum engedélyezett mérete (байtban). Az alapértelmezett érték `10000000` (10 MB). Szükség esetén állítsa ezt az értéket az Ön feltöltési limiteihez.
- Látogasson el a [Account Import Guide](Account-Import.md) dokumentációhoz a CSV útján történő tömeg IMAP fiók importálásával kapcsolatos részletes használati útmutatóért.

### 📤 Audit Export Beállítások {#audit-export-settings}
A audit adatok exportálási oldal beállításai (csak adminisztrátor számára, a Logs oldalról elérhető). Az export egy ZIP csomagot generál a meglévő archivumból származó táblázatos tömegadatokkal (INDEX.XML + CSV táblák + DTD) külső audit eszközök számára. A használati részletekért lásd a [Audit Data Export Guide](AuditExport.md) dokumentációt.
- `AuditExport__DataSupplierName`: Az "Data supplier name" űrlapmez défaut értéke, amely az exportált index fájlban azonosítja az szervezetet. Alapértelmezetten: üres.
- `AuditExport__DataSupplierLocation`: Az "Data supplier location" űrlapmez défaut értéke (pl. cég székhely). Alapértelmezetten: üres.
- `AuditExport__Comment`: Az "Comment" űrlapmez défaut értéke, amely szabad szöveges formátumban szerepel az exportált index fájlban. Alapértelmezetten: üres.
- `AuditExport__OutputDirectory`: A generált ZIP fájlok tárolási könyvtára. A relatív útvonalakat az app content root alapján oldja fel. Alapértelmezetten: `exports/audit`.
- `AuditExport__RetentionDays`: A napok száma, amely után a befejezett export fájlokat a napi tisztítás törli. Alapértelmezetten: `30`.
- `AuditExport__MaxRangeYears`: A megengedett maximum intervallum az exportalapozott időszak kezdete és vége között. Alapértelmezetten: `10`.
- A űrlapmezeket ezek a défaut értékek töltik meg előre, de az egyes exportok során módosíthatók.
- Minden export egy kezdő és egy eredmény tételt ír a hozzáférési naplóba (típus: "Audit Data Export"), így a történet revision-biztos az adatbázis-félema módosítás nélkül is.

### 🔒 Törlési szabályzat beállítások {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Ez szabályozza, hogy a archivált e-mailek manuális törlése engedélyezett-e (true/false). Az alapértelmezett érték `true`. Ha `false` értékre van állítva:
  - Minden archivált e-mail le van zárva (`IsLocked = true`) az indításkor a database compliance trigger segítségével, megakadályozva a módosítást vagy törlést a database szintjén.
  - A manuális törlés (egyes vagy tömeges) az alkalmazás szintjén hibaüzenettel blokkolva van.
  - Az oszlop alapértelmezett értékét úgy állítják át, hogy az újból importált e-mailek is le legyenek zárva.
  - A jelenlegi szabályzat állapota minden indításkor a logolásra kerül az AccessLogs táblába (a Logs oldalon "Deletion Policy" bejegyzésekként látható) a módszertani ellenőrzhetőség érdekében.
  - A helyi tárolási időhöz (retention) kapcsolódó törlés mentesül: azok az e-mailek, amelyek egy konfigurált tárolási idő alá esnek, továbbra is törlődnek (a tárolási folyamat során a törlés előtt közvetlenül feloldják őket).
- `DeletionPolicy__MaxBulkDeleteEmails`: Felső határ egy szűrőalapú tömeges törléshez (*Delete all matches*). Az alapértelmezett érték `100000`. Ha egy szűrő több e-maillel megegyezik, mint a határ, a rendszer elutasítja azt egy üzenettel, amely kéri a szűrő szűkítését; a folyamat soha nem vágja le csendben a tömeget. A tárolási szabályzat által lezárt e-maileket kihagyja és a folyamat státuszában jelzi, nem oldja fel őket. A folyamat újra ellenőrzi ezt a limitet és a `DeletionPolicy__DeletionAllowed` értékét indításkor, így ha egy szabályzatváltozás előtt bekeresztelt folyamat áll meg, nem fut tovább a régi beállítással.
- **Változatlansági védelem:** Amikor `IsLocked = true`, a database compliance trigger (`prevent_locked_email_changes`) megakadályoz ANY módosítást az e-mail sor bármilyen módosítását — minden oszlop védett, nem csak egy fix mezolistát. Az egyetlen mentesült oszlop maga a `IsLocked` (hogy a tárolási törléshez és az indítási szabályzat alkalmazásához szükséges feloldás lehetséges maradjon), valamint a `FolderName` (hogy az IMAP szinkronizáció frissíthesse a mappát, ha egy e-mail a szerver oldalon mozog). A védelem oszlopfüggetlen (JSONB-alapú összehasonlítás), és automatikusan leкрыt minden későbbi schéma bővítést.

### 🕐 Időzóna beállítások {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Az időzóna, amely az e-mailek időbélyegűinek megjelenítéséhez használatos a felületen. IANA időzóna azonosítókat használ (pl. "Europe/Berlin", "Asia/Tokyo"). Az alapértelmezett érték a "Etc/UCT" a háttatólagos kompatibilitás érdekében. Az e-mailek importálásakor az időbélyegeket erre az időzónára konvertálja a megjelenítés céljából.

### 🎉 ReleaseNotes Beállítások (Verziófrissítési felugró ablak) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: Az az verziónév, amelyet ez az példány jelent. Ezt a GitHub release tagekkel hasonlítják össze, hogy eldöntsék, létezik-e újabb kiadás. Ha üres, a changelog a fordított assembly verzióra tér vissza, és a frissítési jelzés kikapcsol.
- `ReleaseNotes__Enabled`: A verziófrissítési felugró ablak engedélyezése vagy tiltása (true/false). Az alapértelmezett érték `true`. Ha engedélyezve van, az adminisztrátorok egy egyszeri changelog modalt fognak látni az alkalmazás frissítése után, amely a jelenlegi verzióhoz a GitHub Releases oldalról lekelt release note-okat mutatja. Minden adminisztrátor el tudja zárni a modalt, és ez csak egy újabb verzió esetén jelenik meg újra. Állítsa `false` értékre a funkció teljes kikapcsolásához, beleértve a frissítési jelzést is.
- `ReleaseNotes__UpdateHintEnabled`: Mutasson-e egy kis toast értesítést az adminisztrátorok számára az alsó jobb sarokban, ha a GitHub egy újabb stabil kiadást tesz közzé, mint a telepített verzió (true/false). Az alapértelmezett érték `true`. A toast linkel a megfelelő kiadáshoz, és a böngészőben verziónként egyszer zárható le. A tervezetek (drafts) és előkiadások (prereleases) figyelmen kívül maradnak. A `ReleaseNotes__AppVersion` szükséges hozzá; nélküle nem jelenik meg a jelzés.
- `ReleaseNotes__UpdateCheckIntervalHours`: Mennyi gyakرتartományban frissüljön a GitHub kiadási lista, órában megadva. Az alapértelmezett érték `6`. A rövidebb intervallum miatt egy folyamatosan futó példányban hamarabb láthatóvá válik az új kiadás, a hosszabb pedig csökkenti a GitHub API hívások számát. A minimum `1`.

### 🔧 Database Maintenance Settings {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Az automatikus napi adatbázis karbantartás engedélyezése vagy letiltása (true/false). Az alapértelmezett érték `false`. Ha engedélyezve van, a rendszer automatikusan futtat VACUUM ANALYZE műveleteket az adatbázis teljesítményének optimalizálása és a túlterjesztés (bloat) megelőzése érdekében. További részletekért olvassa el a [Database Maintenance Guide](DatabaseMaintenance.md) dokumentációt.
- `DatabaseMaintenance__DailyExecutionTime`: Az az időpont, amikor az adatbázis karbantartásnak futnia kell, 24 órás formátumban (HH:mm). Az alapértelmezett érték `02:00`. Válasszon egy olyan időpontot, amikor alacsony a rendszer aktivitás.
- `DatabaseMaintenance__TimeoutMinutes`: A karbantartási műveletekhez megengedett maximum idő perces értékben. Az alapértelmezett érték `30`. Növelje ezt az értéket nagyobb adatbázázok esetén.

### 🧬 Melléklet de Duplikáció Beállítások {#attachment-deduplication-settings}
A melléklet de duplikáció minden egyedi melléklet-payloadt csak egyszer tárol (SHA-256 alapú tartalom-címzés alapján), és ez egy **alapvető funkció, amely mindig engedélyezve van** – szándékosan nincs be/ki kapcsoló. Csak az alábbi batch/ütemezési paraméterek állíthatók be. A teljes részletekért lásd a [Attachment Deduplication Guide](AttachmentDeduplication.md) dokumentációt.

- `AttachmentDeduplication__BatchSize`: Az egyszeri háttér migráció során tranzakciónként migrálandó meglévő mellékletek száma a meglévő adatok migráálásakor. Az alapértelmezett érték `200`. A nagyobb értékek gyorsabban migrálnak, de több memóriát/DB-terhelést igényelnek batchonként.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Opcionális szünet (millisekunderben) a migrációs batchek között a busy rendszereken tapasztalható adatbázis-terhelés korlátozására. Az alapértelmezett érték `0` (nincs szünet).
- `AttachmentDeduplication__StartupDelaySeconds`: Késleltetés (másodpercekben) az alkalmazás indítása után, mielőtt a háttér migráció megkezdődne, hogy a schema migrációnak legyen ideje befejeződni. Az alapértelmezett érték `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Az állandóan futó garbage collection intervalluma (órákban), amely eltávolítja azokat a melléklet-payloadokat, amelyekre már nincs hivatkozás semmilyen e-mailből. Az alapértelmezett érték `12`. Ez függetlenül fut `DatabaseMaintenance__Enabled` beállítástól.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Adatbázis parancs timeout (másodpercekben) a migrációs batch műveletekhez (INSERT SHA-256 hash-eléssel és UPDATE). Az alapértelmezett érték `300` (5 perc). Növelje ezt az értéket nagyon nagy adatbázok vagy mellékletek esetén, vagy csökkentse, ha gyorsabb hibaérzékelést szeretne. Ha egy batch továbbra is timeoutot okoz, a szolgáltatás automatikusan megpróbálkozik a fel fél méretű batch-szel.

### 💾 Fiók tárolási beállítások {#account-storage-settings}
A fiók alanyi tárolási megjelenítése a megadott adatbázis tárolási felhasználását (összes e-mail mez + csatolások) mutatja a Dashboard "Account Overview" táblázatban és a MailAccounts "Show All" táblázatban. Egy autark háttérszolgáltatás (`AccountStorageRefreshService` kiszámítja az értékeket az egyes oszlopok PostgreSQL `pg_column_size` összeadásával (minden e-mail mezét lefedve anélkül, hogy az e-mail testét olvassák), majd elmenti őket a `AccountStorageCache` táblázatba. Ez a szolgáltatás függetlenül fut a `DatabaseMaintenance__Enabled`-tól.

- `AccountStorage__Enabled`: A tárolási frissítési szolgáltatás engedélyezése vagy tiltása (true/false). Az alapértelmezett érték `true`. Ha engedélyezve van, a szolgáltatás indításkor a függőben lévő fiókok újrabetöltését (resumable backfill) végzi el (`AccountStorageBackfillState` segítségével a hibatűrés biztosított), majd ezután napi teljes frissítést végez. Ha letiltva van, a tárolási értékek csak az e-mailek szinkronizálásakor, importálásakor vagy törléténél frissülnek, de a megjelenített értékek idővel elavultակ lehetnek.
- `AccountStorage__DailyExecutionTime`: A nap folyamán megadott időpont (24 órás formátum `HH:mm`) az összes fiók napi teljes frissítéséhez. Az alapértelmezett érték `02:30`. Egy olyan időpontot válasszon, amikor a rendszer aktivitása alacsony.
- `AccountStorage__BackfillDelayMs`: Késleltetés (milliszekundokban) a fiókok között az indítási elsődleges újrabetöltés (backfill) során. Az alapértelmezett érték `5000`. Az alacsonyabb értékek felgyorsítják az újrabetöltést, de növelik az adatbázis terhelését; nagyon nagy archiváknál emelje ezt az értéket, hogy elkerülje az adatbázis túlterhelését.
- `AccountStorage__RefreshBatchDelayMs`: Késleltetés (milliszekundokban) a fiókok között a napi teljes frissítés során. Az alapértelmezett érték `1000`. Az alacsonyabb értékek felgyorsítják a frissítést, de növelik az adatbázis terhelését; nagyon nagy archiváknál emelje ezt az értéket.
- `AccountStorage__CommandTimeoutSeconds`: Adatbázis parancs timeout (másodpercekben) a fiók alanyi tárolási számításhoz (`pg_column_size` egy fiók összes során). Az alapértelmezett érték `300` (5 perc). A számítás minden e-mail sort olvas be, beleértve a TOAST adatokat is, ami nagyon nagy archiváknál (250k+ e-mail) akár több percet is igénybe vehethet; a korábbi 30 másodperces Npgsql alapértelmezett érték miatt a frissítés néma hibával leállt, és a dashboard elavult értékeket mutatott. Emelje ezt az értéket, ha a számítás még mindig időtúlsúlya az Ön archivájánál.

> 💡 **Megjegyzés**: A tárolási értékek azonnal frissülnek minden e-mail szinkronizálás, importálás vagy megőrzési (retention) törlés után, így a megjelenített értékek a napi frissítés nélkül is aktuálisak maradnak. A napi frissítés egy biztonsági háló, amely az esetleges különleges eseteket (pl. közvetlen adatbázis-módosításokat) kezeli.

### 📝 Naplózási beállítások {#logging-settings}
- `Logging__LogLevel__Default`: Az alkalmazás alapértelmezett naplózási szintje. A rendelkezésre álló szintek: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Az alapértelmezett: `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Naplózási szint az ASP.NET Core framework üzenetekhez. Az alapértelmezett: `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Naplózási szint az Entity Framework adatbázis parancsokhoz. Az alapértelmezett: `Warning`.

### 🛡️ Biztonsági beállítások {#security-settings}
- `AllowedHosts`: A alkalmazás által kiszolgálható host nevek pontosvesszővel tagolt listája. Ez segít megelőzni az HTTP Host header támadásokat. Példa: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Fontos**: Ne használja a `*` értéket termelési környezetben, mivel ez kikapcsolja a host header validációt.

#### 🔐 Hitelesítő adatok titkosítása tároláskor (At Rest) {#credential-encryption-at-rest}
A levelezési fiók hitelesítő adatai (IMAP jelszó, M365 client secret és MSA OAuth tokenek) az adatbázisban titkosítva lehetnek. A titkosítási kulcsot a adatbázis **kívül** tartjuk ( környezeti változó / Docker secret), így az adatbázis mentése (dump) önmagában sem elég a hitelesítő adatok kiolvasásához.

- `Security__CredentialEncryptionKey`: Base64 kódolt 256 bites (32 byte) kulcs (key encryption key). Generáljon ilyen kulcsot a következő paranccsal:
  ```bash
  openssl rand -base64 32
  ```
  A értéket környezeti változóként állítsa be (ajánlott: `Security__CredentialEncryptionKey` a compose fájlban vagy a konténer környezetében). Az alkalmazás nem támogatja a `_FILE` stílusú Docker secreteket — ha Docker secretet használ, mountolja azt, és töltse be a tartalmát a környezeti változóba az entrypoint során. Soha ne committeleyin kulcsot a `appsettings.json`-be. Amikor egy kulcs konfigurálva van, a hitelesítő adatokat íráskor titkosítják, egy háttérszolgáltatás pedig egyszer le titkosítja a meglévő plain text hitelesítő adatokat.
- `Security__EncryptAccountCredentials`: Az új/frissített hitelesítő értékek titkosításának engedélyezése vagy tiltása (true/false). Az alapértelmezett érték a `true`. Ha nincs konfigurálva kulcs, a hitelesítő adatokat plain text formátumban tárolják, és a folyamatindításkor figyelmeztetés kerül a logokba — a meglévő telepítések nem érintettek, amíg kulcs nem érkezik meg.
- `Security__CredentialEncryptionBackfill`: A háttérszolgáltat undergoes-on a folyamatindításkor a meglévő plain text hitelesítő adatok titkosítása (true/false). Az alapértelmezett érték a `true`. Ha nincs konfigurálva kulcs, a művelet semmi hatással nem bír.
- `Security__CredentialEncryptionKeyPrevious`: Opcionális korábbi kulcs, amely csak az azzal titkosított értékek visszaoldásához használható (kulcscserekeléshez). Az új értékek mindig a `Security__CredentialEncryptionKey` kulccsal kerülnek titkosításra.

  > ⚠️ **Kulcscsere (Key rotation)**: a csere után tartsa konfigurálva a `Security__CredentialEncryptionKeyPrevious` értéket **amíg minden hitelesítő adat újrétேகésre nem került** (például a fiók szerkesztésével vagy egy automatikus OAuth token frissítéssel) — az óriás kulccsal titkosított meglévő értékek *nem* kerülnek automatikusan újratitkosításra az új kulccsal. Amint eltávolítja a korábbi kulcsot, minden olyan hitelesítő adat, amely még ezzel van titkosítva, olvashatatlanná válik, és újra kell megadni. Amíg konfigurálva van, olyan biztonságosan kell kezelni, mint a jelenlegi kulcsot.

> ⚠️ **Tartsa biztonságban a kulcsot**: A kulcs nélkül a titkosított hitelesítő adatok nem állíthatók vissza, és újra kell őket adni. Készítsen róla biztonsági mentést az adatbázis mentéseivel együtt (de külön megtartva).
>
> 💡 **Meglévő telepítések**: Frissítsen biztonságosan — amíg nincs beállítva `Security__CredentialEncryptionKey`, semmi sem változik. Csak akkor állítsa be a kulcsot, amikor készen áll; a meglévő plain text hitelesítő adatok olvashatóak maradnak, és a háttérben titkosításra kerülnek.

### 🔐 OIDC Konfiguráció {#oidc-configuration}

Az OpenID Connect hitelesítés részletes beállítási útmutatóért lásd a [OIDC Implementation Guide](OIDC_Implementation.md) dokumentációt.

#### Alapvető OIDC Beállítások {#basic-oidc-settings}
- `OAuth__Enabled`: Az OIDC hitelesítés engedélyezése vagy tiltása (true/false)
- `OAuth__Authority`: Az OpenID Connect authority URL (pl. https://sts.windows.net/{TENANT-ID}/ az Azure AD esetén)
- `OAuth__ClientId`: Az identitásszolgáltató által kiosztott client ID
- `OAuth__ClientSecret`: Az identitásszolgáltató által kiosztott client secret
- `OAuth__DisplayName`: Opcionális megjelenítési név, amely az OIDC bejelentkezési gombon és az automatikus átirányítási oldalon látható (pl. `PocketID SSO`). Ha elhagyja, az általános "Login with OAuth" felirat kerül alkalmazásra.
- `OAuth__ClientScopes__0`: Az első kért scope az identitásszolgáltatótól (openid)
- `OAuth__ClientScopes__1`: A második kért scope az identitásszolgáltatótól (profile)
- `OAuth__ClientScopes__2`: A harmadik kért scope az identitásszolgáltatótól (email)
- `OAuth__ResponseMode`: Az autorizációs kérés opcionális response mode (`query`, `fragment` vagy `form_post`). Az alapértelmezett `form_post`, az ASP.NET Core alapértelmezett értéke. Állítsa `query` értékre olyan szolgáltatók esetén, amelyek nem támogatják a `form_post` funkciót, mint például a Synology SSO Server. Lásd a [Provider Compatibility](OIDC_Implementation.md#provider-compatibility) részt.
- `OAuth__AutoApproveUsers`: Az új OIDC felhasználók automatikus jóváhagyása manuális admin jóváhagyás nélkül (true/false). Az alapértelmezett érték `false`.

#### Felhasználói Provisioning Beállítások {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Az új OIDC felhasználók automatikus jóváhagyása manuális admin jóváhagyás nélkül (true/false). Az alapértelmezett érték `false`. Ha engedélyezve van, az OIDC szolgáltatón keresztül hitelesített felhasználók azonnal aktívvá válnak, és hozzáférhetnek az alkalmazáshoz. Ha letiltva van (alapértelmezett), az új OIDC felhasználók inaktív állapotban kerülnek létrevetésre, és adminisztrátori manuális aktiválásra szólnak szükség. Részletes információért lásd az [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users) részt.
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, stb.: E-mail címek, amelyek automatikusan adminisztrátorként kerülnek provisionálásra. Azok a felhasználók, akik ezekkel az e-mail címekkel rendelkeznek, az első OAuth bejelentkezéskor aktív adminisztrátorként kerülnek létrevetésre, megkerülve a normál jóváhagyási folyamatot. Az e-mail címek összevetéseesés nem érzékeny.

#### Jelszó nélküli Bejelentkezési Beállítások {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: A felhasználónév/jelszó bejelentkezés letiltása (true/false). Az alapértelmezett érték `false`. Ha engedélyezve van, a jelszó űrlap el van rejtve, **és** a szerver minden jelszó POST kérést elutasít, így csak az OAuth bejelentkezés áll rendelkezésre. Ez `OAuth__Enabled=true` beállítást igényel. Ha az OAuth letiltva van, ez a beállítás nem hatással van.
- `OAuth__AutoRedirect`: Felhasználók automatikus átirányítása az OAuth szolgáltatóhoz (true/false). Az alapértelmezett érték `false`. Ez `OAuth__DisablePasswordLogin` értékének `true` állapotát igényli. A felhasználók egy rövid betöltési képernyőt fognak látni az átirányítás előtt.

#### Példa: Teljes OIDC-First Konfiguráció {#example-full-oidc-first-configuration}
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

## 🔐 Kestrel HTTPS konfiguráció (Opcionális) {#kestrel-https-configuration-optional}

Bár az alkalmazáshoz HTTPS-sel ellátott reverse proxy keresztül való hozzáférés a cél, a Kestrel web szervert SSL/TLS tanúsítványok használatára is konfigurálhatja. Ez végső-végig (end-to-end) titkosítást biztosít a reverse proxy és az alkalmazás konténer között.

### Konfigurációs lépések {#configuration-steps}

1. **Generáljon vagy szerezzen be egy SSL tanúsítványt** PFX formátumban (pl. `localhost.pfx`)

2. **Adja meg a következő környezeti változókat** a `docker-compose.yml` `mailarchive-app` szolgáltatáshoz:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Frissítse a port mappolást** a `mailarchive-app` szolgáltatásban:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Adjon hozzá egy volume mappolást** a tanúsítványhoz:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Helyezze el a tanúsítvány fájlát** (pl. `localhost.pfx`) a gazdagép rendszerének `./certs` könyvtárában.

### Környezeti változók magyarázata {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: HTTP végpont URL (alapértelmezett: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: HTTPS végpont URL (alapértelmezett: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: A PFX tanúsítvány fájl útvonala a konténeren belül
- `Kestrel__Endpoints__Https__Certificate__Password`: A PFX tanúsítvány fájl jelszava

> 💡 **Megjegyzés**: Ez a konfiguráció opcionális. Ha HTTPS-sel ellátott reverse proxyt használ (ajánlott), a reverse proxy és az alkalmazás közötti kommunikáció maradhat HTTP. Azonban érzékeny környezetben a maximális biztonság érdekében érdemes lehet a Kestrel HTTPS-t is engedélyezni az egész kommunikációs útvonal titkosítása érdekében.

## 🔑 Titkos szavak kezelése term production környezetben {#secrets-management-for-production}

A szenzitív adatok, például az adatbázis jelszavak, admin hitelesítési adatok és OAuth client secret értékek közvetlen hardkodálása a `docker-compose.yml` fájlba biztonsági kockázatot jelent, különösen, ha a fájlt a verziókontrollba teszik be. A javasolt megközelítés ezek az értékek külsőre helyezése egy olyan `.env` fájlba, amelyet a Docker Compose automatikusan betölt.

### 📄 A `.env` fájl {#the-env-file}

Hozzon létre egy `.env` fájlt a `docker-compose.yml` fájllal azonos mappában a következő tartalommal:

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

**Fontos**: A Docker Compose automatikusan beolvassa a `.env` fájlt ugyanabból a mappából — nem kell manuálisan hivatkozni rá. A `.env` fájlban definiált összes változó elérhető a `docker-compose.yml` környezetben a `${VARIABLE_NAME}` szintaxis segítségével.

### 🐳 Adaptált `docker-compose.yml` {#adapted-docker-composeyml}

A telepítési lépésekből származó `docker-compose.yml` részt úgy kell adaptálni, hogy a hardkodált titkos szavak helyett helyőrzőket használjon:

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

### 📋 `.env.example` Sablon adminisztrátoroknak {#envexample-template-for-administrators}

Hozzon létre egy `.env.example` fájlt a repellitójában, amely sablonként szolgál más adminisztrátorok számára. Ez a fájl helyőrző értékeket és dokumentációt tartalmaz, de **nem tartalmaz valódi titkos szavakat**:

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

- **Használjon `.env.example`-ot sablonként**
- **Állítson be szigorú fájlengedélyeket** — Korlátozza a hozzáférést a `.env` fájlhoz:
  ```bash
  chmod 600 .env
  ```
- **Használjon erős jelszavakat** — Generáljon hosszú, véletlenszerű jelszavakat (legalább 16 karakter), betűkkel, számokkal és speciális karakterekkel kevert formában.
- **Korlátozza a `.env` fájl elérését** — Csak a Docker Compose-t futtató felhasználónak should legyen olvasási engedélye a `.env` fájlhoz.

## 🔒 Biztonsági megjegyzések {#security-notes}

- Használjon erős jelszavakat és değiştirje az alapértelmezett hitelesítő adatokat. A jelszavak legalább 12 karakter hosszúak powin быть, és tartalmazniuk kell nagybetűket, kisbetűket, számokat és speciális karaktereket. Kerülje a gyakori szavak vagy könnyen leleplezhető információk használatát.
- Termelési környezetben fontoljon meg HTTPS implementálását reverse proxy használatával
- A PostgreSQL adatbázis rendszeres mentése javasolt. A részletes mentési és visszaállítási folyamatokért olvassa el a [Backup and Restore Guide](BackupRestore.md) dokumentációt.
