# 🛠️ Mail Archiver Beállítási Útmutató {#mail-archiver-setup-guide}

[← Vissza a Dokumentáció Indexhez](index.md)

## 📋 Áttekintés {#overview}

Ez az útmutató részletes utasításokat nyújt a Mail Archiver alkalmazás Docker Compose segítségével történő beállításához.

## 🛠️ Előfeltételek {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Telepítési lépések {#installation-steps}

1. Telepítse a rendszer előfeltételeit.

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

3. Szerkesztse az adatbázis konfigurációját a `docker-compose.yml` fájlban, és állítson be egy biztonságos jelszót a `POSTGRES_PASSWORD` változóba és a `ConnectionString` beállításba.

4. Ha hitelesítést szeretne használni (amit erősen javasolunk), határozzon meg egy `Authentication__Username` és `Authentication__Password`, amelyet az admin felhasználó használ.

5. A `TimeZone__DisplayTimeZoneId` környezeti változót az Ön által preferált időzónának megfelelően állítsa be (az alapértelmezett az "Etc/UCT"). Bármely IANA időzónamező azonosítót használhat (például: "Europe/Berlin", "Asia/Tokyo").

6. Opcionálisan konfigurálhatja a `Logging__LogLevel` környezeti változókat az alkalmazás naplóinak részletességének szabályozására. Az elérhető lehetőségekért tekintse meg alább a Logging Settings szakaszt.

7. Konfiguráljon egy a választásuk szerinti reverse proxy-t https és hitelesítés használatával az alkalmazás hozzáférésének biztonságosításához.

> ⚠️ **Figyelem**: Maga az alkalmazás nem biztosít titkosított hozzáférést https keresztül! Ezt egy reverse proxy segítségével kell beállítani!

8. A konténerek első indítása:
```bash
docker compose up -d
```

9. Konténerek újraindítása:
```bash
docker compose restart
```

10. Az alkalmazás megtekintése

11. Lépjen be a meghatározott hitelesítő adatokkal, és adja hozzá az első e-mail fiókját:
   - Navigáljon az "Email Accounts" szekcióba
   - Kattintson a "New Account" gombra
   - Adja meg a szerver részleteit és a hitelesítő adatokat
   - Mentés és az archiválás megkezdése!
   - Ha szeretné, hozzon létre egyéb felhasználókat és rendeljen hozzá fiókokat.

## 📚 Környezeti változók magyarázata {#environment-variable-explanations}

### 🗄️ Adatbázis-kapcsolat {#database-connection}
- `ConnectionStrings__DefaultConnection`: A PostgreSQL adatbázishoz való csatlakozási karakterlánc. Szükség esetén módosítsa a `Host`, `Database`, `Username`, és `Password` értékeket.

### 🔐 Hitelesítési beállítások {#authentication-settings}
- `Authentication__Username`: Az admin fiók felhasználóneve.
- `Authentication__Password`: Az admin fiók jelszava.
- `Authentication__SessionTimeoutMinutes`: A munkamenet lejárati ideje perces egységben. Ez a nem tartós (non-persistent) bejelentkezési cookie-ra vonatkozik; a felhasználók, akik a bejelentkezési űrlapon bepipálják a **Remember Me** opciót, helyette egy fix 7 napig érvényes, tartós cookie-t kapnak. See [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: A hitelesítési cookie neve.
- `Authentication__CookieSameSite`: A hitelesítési, munkamenet (session) és CSRF védelem cookie-k SameSite attribútumának konfigurálása. Érvényes értékek:
  - `Strict` (alapértelmezett): Maximális biztonság. A cookie-k csak azonos oldal (same-site) kérésekkel küldhetők. Ez problémákat okozhat, amikor külső linkekről navigálunk az alkalmazásba (például egy másik webhelyről kattintunk egy linkre), mivel a meglévő munkamenet nem lesz felismerhető.
  - `Lax`: Javasolt reverse proxy használata esetén. A cookie-k a legfelső szintű (top-level) navigációk és az azonos oldal (same-site) kérések során is küldésre kerülnek, lehetővé téve a felhasználók számára, hogy külső linkeken keresztül is az alkalmazáshoz navigáljanak a POST kérések CSRF védelmének megőrzése mellett.
  - `None`: A cookie-k minden kérésrel küldésre kerülnek. HTTPS és a `Secure` attribútum szükséges hozzá. Csak akkor használja, ha specifikus cross-site követelményei vannak, és megértette a biztonsági implikációkat.

### 🔌 REST API Beállítások {#rest-api-settings}
Az opcionális, csak olvasható REST API alapértelmezetten **ki van kapcsolva**. A teljes útmutatóért lásd a [REST API guide](API.md) dokumentációt.
- `Api__Enabled`: A csak olvasható REST API fő kapcsolója (alapértelmezetten `false`). Ha `false`, minden `/api/*` útvonal `404` választ ad vissza.
- `Api__AllowAttachmentDownloads`: Engedélyezi a csatolt fájlok (attachment) byte-ainak letöltését az API keresztül (alapértelmezetten `true`). Ha `false`, a csatolt fájl végpontja `403`-at ad vissza.
- `Api__AllowMessageDownloads`: Engedélyezi a teljes üzenetek letöltését az API keresztül, mind az újránépített `/eml`, mind az eredeti `/raw` formátumban (alapértelmezetten `true`). Ha `false`, mindkét végpont `403`-et ad vissza. Egy teljes üzenet tartalmazza a csatolt fájljait, ezért **mind** ez a kapcsoló, **mind** a `Api__AllowAttachmentDownloads` kapcsoló `true` állapotban kell lennie; bármelyik kikapcsolása mindkét végpontot elérési körét megszűri.
- `Api__EnableSwaggerUi`: A Swagger UI elérhető a `/apidocs` címen, az OpenAPI dokumentum pedig a `/apidocs/spec/v1.json` címen (alapértelmezetten `true`). Mindkeduanya bejelentkezett böngésző-űdzést igényel.
- `Api__DefaultPageSize`: A listázó végpontok alapértelmezett oldalmérete, ha nincs megadva (alapértelmezetten `20`).
- `Api__MaxPageSize`: A maximum megengedett oldalméret; a nagyobb kéréseket korlátozzuk (alapértelmezetten `100`).
- `Api__RateLimitPerMinute`: Fix ablakos kéresek költségvetése (request budget) API-kulcsonként percenként (alapértelmezetten `120`).

### 🤖 MCP Server Beállítások {#mcp-server-settings}
Az opcionális MCP (Model Context Protocol) szerver ugyanazt a csak olvasható levélalapú hozzáférést biztosítja az AI ügynökök számára a Streamable HTTP transzpor través, ugyanúgy, mint a REST API. Ez **alapértelmezés szerint le van kapcsolva**. A teljes útmutatóért lásd az [MCP guide](MCP.md) dokumentációt. Az MCP ugyanazokat az API kulcsokat használja, mint a REST API (`ma_...` bearer tokenek), és ugyanazt a felhasználónkénti számlahozzáfési korlátozást (scoping).
- `Mcp__Enabled`: A MCP szerver fő kapcsolója (alapértelmezett `false`). Ha `false`, a `/mcp` végpont `404` értéket ad vissza.
- `Mcp__AllowAttachmentDownloads`: Engedélyezi a `get_attachment` eszköz számára, hogy csatolt fájlok byteit adjon vissza (alapértelmezett `true`). Ha `false`, az eszköz hibát ad vissza.
- `Mcp__DefaultPageSize`: Az `search_emails` által használt alapértelmezett oldalméret, ha a hívó elhagyja a `pageSize` paramétert (alapértelmezett `20`).
- `Mcp__MaxResults`: A `search_emails` oldalméretének felső határa; a nagyobb értékek korlátozásra kerülnek (alapértelmezett `100`).
- `Mcp__MaxAttachmentBytes`: Maximális csatolt fájl mérete (byte-okban), amelyet a `get_attachment` eszköz base64 formátumban ad vissza inline; a nagyobb csatolmányokat elutasítjuk (alapértelmezett `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Engedélyezi a `get_email_eml` és a `get_email_raw` eszköz számára a teljes üzenetek visszatérítését (alapértelmezett `true`). Ha `false`, az eszközök hibát adnak vissza. Egy teljes üzenet tartalmazza a csatolt fájljait, ezért **mindhalkul** ennek a kapcsolónak és a `Mcp__AllowAttachmentDownloads`-nek is `true` állapotban kell lenniük; bármelyik kikapcsolása mindkét eszközt elérgésszé teszi.
- `Mcp__MaxMessageBytes`: Maximális üzenetméret (byte-okban), amelyet a `get_email_eml` és a `get_email_raw` eszköz inline ad vissza; a nagyobb üzeneteket elutasítjuk (alapértelmezett `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Fix ablakos kérelemkeret (request budget) API kulcsonként, percenként (alapértelmezett `120`).

### 📨 MailSync Beállítások {#mailsync-settings}
- `MailSync__IntervalMinutes`: Az e-mail szinkronizálás közötti intervallum perces egységben. Ez a globális alapértelmezett; minden egyes fiók ezt egyenként felülírhatja a Létrehozás/Szerkesztés oldalon (hagyja üresen a megmaradásához).
- `MailSync__FullSyncIntervalHours`: Opcionális globális alapértelmezett az automatikus teljes újraszinkronizálásokhoz, órában. Ha nincs beállítva (az alapértelmezett), nem fut automatikus teljes szinkronizálás, hacsak nincs elhelyezve egy fiókonkénti `FullSyncIntervalHours` érték a Létrehozás/Szerkesztés oldalon. A fiókonkénti értékek felülírják ezt a globális alapértelmezettet.
- `MailSync__TimeoutMinutes`: Fiókonkénti szinkronizálási timeout perces egységben. Egy szinkronizálás, amely tovább tart, mint amennyi az időkeret, megáll a következő üzenet határonál, megőrzi a checkpointjeit, és a következő futtatáskor folytatja; a feladatot `Timed Out` jelzi, és a `LastSync` nem halad előre. Csak időzített szinkronizálásokra vonatkozik; a felületről indított szinkronizálás (manuális szinkronizálás vagy teljes újraszinkronizálás) timeout nélkül fut. A `0` (az alapértelmezett) vagy bármely nem pozitív érték azt jelenti, hogy nincs timeout. Frissítés előtt ellenőrizze az értéket, ha nagy postafiókokat szinkronizál. Lásd: [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Az IMAP kapcsolatoknál alkalmazandó kapcsolattimeout másodpercekben.
- `MailSync__CommandTimeoutSeconds`: Az IMAP parancsoknál alkalmazandó parancscs timeout másodpercekben.
- `MailSync__AlwaysForceFullSync`: Arról, hogy mindig kényszerítsen-e teljes szinkronizálást (true/false).
- `MailSync__IgnoreSelfSignedCert`: Arról, hogy figyeljen-e el az önmagában aláíró certifikátusokat (true/false).
- `MailSync__MaxConcurrentSyncs`: Hány fiók szinkronizálása futhat egyszerre. Alapértelmezett: `1` (soros, visszafelé kompatibilis). Az ükemény (scheduler) akkor tölti vissza a helyeket, amint azok felszabadulnak, nem pedig egy egész batch befejezésére vár, így egy lassú postafiók már nem tartja vissza a többi folyamatot. Növelje a többszámú fiókok egyidejű szinkronizálásához — vegye figyelembe a szolgáltató sebességkorlátozását és a helyi erőforrás-használatot. Lásd: [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Opcionális, másodperces késleltetés (stagger delay), amely minden fiók szinkronizálási feladatának végén alkalmazható. Alapértelmezett: `0` (nincs késleltetés). Hasznos a hirtelen indulások elkerülésére, amikor `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Hány darab típusú probléma marad meg egy szinkronizálási feladatnál a fiókoldal megjelenítéséhez. A sikertelen mappák, a hiányzó mappák és a sikertelen üzenetek külön költségkeretedet (budget) kapnak, így egy típusból érkező tömeg nem tudja eltávolítani a többi fajt a nézetből; a keret túlsúlya esetén az értékek csak számolódnak, nem pedig tárolódnak. Alapértelmezett: `20`. A `0` kikapcsolja a részletezést, és csak a számlálókat hagyja meg. Lásd: [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Mappák, amelyek a szinkronizálásból kizárva vannak **minden** fiók esetében, a fiókonkénti kizárt mappák listáján felül. Alapértelmezett üres, így a meglévő beállítások nem változnak. A két lista **kiegészíti egymást** — egy mappa akkor kerül elkerülésre, ha bármelyik lista találatát idézi — és mindkét lista ugyanazokat a szabályokat használja: pontos egyezés a teljes útvonalon, pontos egyezés a mappa nevén, útvonali suffix egyezés (így a `Drafts` a `INBOX/Drafts` és a `INBOX.Drafts` értékeket is eléri), és minden, ami a mappa alatt található, amennyiben a `MailSync__ExcludeSubfolders` bekapcsolva van. Az egyezés nem figyeli az esetbet. Hasznos, ha sok postafiókot importál ugyanazon a szerveren, ahol az alternatíva minden fiókhoz azonos kizárási listát vezetni lenne. Alapértelmezett módon nincs kizárt mappa; az, hogy mely nevek érdemes listázni, a szervertől és annak nyelvétől függ. Példa egy postafiók-fáldrábra, amely tartalmaz kalendár- és névjegyzék-mappákat is:
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
- `MailSync__ExcludeSubfolders`: Arról, hogy egy kizárási bejegyzés tartalmazza-e a nevét viselő mappa alatti mappákat is. Ez egyszerre vonatkozik a fiókonkénti listára és a telepítés szintű listára, valamint mindkét szolgáltatóra. Alapértelmezett: `false`, ami a beállítás megjelenése előtti viselkedést követi: egy `Deleted Items` bejegyzés csak azt a mappát jelöli meg, és egy fa felidéret kerül a megőrzésből a gyökér és minden alatta lévő mappa megnevezésével. Állítsa `true` értékre, ha egy bejegyzés a maga alatt lévő fát is tartalmaz — egy `Deleted Items` bejegyzés ilyenkor a `Deleted Items/2024` és az ág többi részét is távol tartja az archívumból. A szabály az útvonal-szeparátorra támaszkodik, így egész mappákat vesz el, és soha nem egy név részét: a `Archive` lefed a `Archive` és a `Archive/Team` mappákat is, és magára hagy egy `Team Archive Review` nevű postafiók mappát. A `.` is úgy számít szeparátorként, ahogy a suffix szabály is kezeli.

### 📧 Eredeti üzenetek tárolási beállításai {#original-message-storage-settings}

Alapértelmezés szerint az archívum csak az e-mail feldolgozott részeit tárolja, így egy exportált `.eml` egy rekonstrukció: az átadási kódolások (transfer encodings), a MIME határértékek (boundaries) és a fejléc behajtása (header folding) eltérnek a megkapott üzenettől, a DKIM aláírások pedig már nem igazolhatóak. `RawMessages:Enabled` esetén az eredeti üzenet teljes változatát is tárolja.

| Paraméter | Alapértelmezett | Cél |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Minden újból archivált e-mail eredeti változatának tárolása. |
| `RawMessages__Storage` | `postgres` | A byte-ok tárolási helye: `postgres` vagy `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | A nagyobb üzenetek az eredeti változatuk nélkül kerülnek archiválásba. `0` = nincs limit. |
| `RawMessages__IncludeGraph` | `true` | Microsoft 365 (Graph) fiókok eredeti változatainak tárolása is. |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Az üzenetfájlok könyvtára. Egy Dockerben rákapcsolt (mounted) volument kell mutatnia. |
| `RawMessages__FileSystem__ShardDepth` | `2` | A könyvtár mélysége, amelyeken az e-mail azonosító el van osztva. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | A SHA-256 ellenőrzése minden letöltésnél. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Az olyan üzenetfájlok eltávolítása, amelyekhez egyetlen e-mail sem vonatkozik. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Mennyi gyakran fut ez a tisztító folyamat. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Ekkelnél fiatalabb fájlok soha nem kerülnek eltávolításra. |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Indítás utáni első tisztítás előtti késleltetés, hogy az olyan importálások, amelyek még írást végeznek, ne kerüljenek ki elliptetésre. |

!!! warning "A fájlrendszer háttérnek (backend) szükség van rákapcsolt volumenre"
    `RawMessages__Storage=filesystem` esetén az üzenetfájlok aຖານ adatbázison kívül élnek.
    Bind mount nélkül a konténer írható rétegében (writable layer) fognak lenni, és a következő konténer újrakreáláláskor elvesznek, míg az adatbázis sorai megmaradnak. A könyvtárat a database dump-al együtt biztonsárolja (backup).

Tekintse meg az **[Original Message Storage](RawMessages.md)** dokumentációt a teljes leírásért: hol érhető el az eredeti változat, a tárolási háttér megoldások, hogól lehet a meglévő üzeneteket `--migrate-raw-storage` segítségével mozgatni közöttük, és mit kell biztonsárolni.

### 📈 Monitor Beállítások {#monitoring-settings}

| Paraméter | Alapértelmezett | Cél |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | Ezzelnél régebbi engedélyezett fiókok esetén a `/api/v1/health` jelentés `Degraded`. A `0` kikapcsolja a vizsgálatot. |
| `Metrics__Enabled` | `false` | Engedélyezi a Prometheus metrikák végpontját és annak frissítési ciklusát. |
| `Metrics__RefreshMinutes` | `5` | Mennyi gyakorisággal épül újra a metrika snapshot. 1 alatti értékek 1-re kerülnek emelésre. |

Az egészségügyi és metrikák végpontjaihoz admin API kulcs és `Api__Enabled=true` szükséges. A konténer saját liveness ellenőrzése hitelesítés nélküli, és nincs szükség konfigurációra. A végpontok, a metrika nevek, a Prometheus konfiguráció és a Docker health check esetén lásd a **[Health and Metrics](Monitoring.md)** részt.

### 📤 BatchRestore Beállítások {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Az e-mailek száma, amely aszinkron feldolgozást vált ki.
- `BatchRestore__MaxSyncEmails`: A szinkron feldolgozáshoz alkalmazandó e-mailek maximális száma.
- `BatchRestore__MaxAsyncEmails`: Az aszinkron feldolgozáshoz alkalmazandó e-mailek maximális száma.
- `BatchRestore__SessionTimeoutMinutes`: A batch restore session timeout percenként.
- `BatchRestore__DefaultBatchSize`: Az e-mail műveletek alapértelmezett batch mérete.

### 🏢 Bárány (Tenant) kezelési beállítások {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: A single Tenant Management művelet során hozzáadható mailboxok maximális száma. Az alapértelmezett érték `1000`. Növelje ezt nagyon nagy bárányok esetén, vagy csökkentse azt, hogy megelőzze a véletlenszerű tömeges importálást. Ha a limitek túllépésre kerülnek, a művelet hibával elutasításra kerül, és egyik fiókot sem hoz létre. Részletekért lásd a [M365 Tenant Import Guide](M365TenantImport.md) dokumentációt.

### 🐄 Mailcow beállítások {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: A Mailcow egyetlen aktiválási futtatása során létrehozható mailboxok maximális száma. Az alapértelmezett érték `1000`. Csökkentse az értéket a véletlen tömeges létrehozás megelőzésére, vagy növelje a nagyobb példányokhoz. Ha a limit túllépésre kerül, nem készülnek létre fiókok. Részletekért lásd a [Mailcow Instances](Mailcow.md) részt.
- A Mailcow integráció emellett megköveteli a `Security__CredentialEncryptionKey` használatát (lásd a [Security Settings](#-security-settings) részt). Egy példány csatlakoztatása és a mailboxok aktiválása is elutasításra kerül, amíg a hitelesítő adatok titkosítása ki van kapcsolva, így az API key és a generált app passwordok soha nem tárolhatók tiszta szövegként.
- Külső hozzáférés szükséges a Mailcow HTTPS API és az IMAP host felé TLS felett (általában a `993` porton).

### 📦 `BatchOperation__BatchSize` BatchOperation Beállítások {#batchoperation-settings}
- `BatchOperation__BatchSize`: Az e-mail műveletek batch mérete.
- `BatchOperation__PauseBetweenEmailsMs`: Az egyes e-mailek közötti szünet millisekundyban.
- `BatchOperation__PauseBetweenBatchesMs`: A batchok közötti szünet millisekundyban.

### 📤 Offload beállítások {#offload-settings}
A [date-windowed offload](Offload.md) beállításai. Az alapértelmezett értékek visszaállítják az alkalmazás korábbi viselkedését, így az olyan telepítések sem érintettek, amelyek nem konfigurálják ezt a szakaszt.
- `Offload__MaxConcurrentJobs`: Hány visszaállítási vagy offload feladat futhat egyszerre. Az alapértelmezett érték `1`, amely megőrzi a feladatlánc által eddig használt szigorúan szériális feldolgozást. A megadott értéktől függetlenül legfeljebb egy feladat futhat céllevélbOxonként.
- `Offload__PrefetchMaxMessages`: A céllevélbOxból az indexelt üzenetek felső határa a duplikáció detektálásához. Az alapértelmezett érték `500000`. E felett a ellenőrzés egyetlen mappára szűkül le, és rögzíti, hogy a lefedettség csökkentve volt.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Forrásmappák, amelyeket soha nem vezetnek le (offload), az átnevezés előtt keresettek és tartalmazzák a mappalábrécéseket. Alapértelmezés szerint üres.
- `Offload__FolderRenameMap__<SourceFolder>`: A forrásmappa elérési út első részeit írja át, például: `Offload__FolderRenameMap__Sent Items=Sent`. Alapértelmezés szerint üres.
- `Offload__MarkAsSeen`: Ha a kitermelt levelek olvassottként van jelölve. Az alapértelmezett érték `true`, ami megfelel a meglévő visszaállítási viselkedésnek.

Mindkét mappa beállítás szándékosan üres: a mappák átírása vagy törlése utasítás nélkül meglepetés lenne azok számára, akik már használják a visszaállítási folyamatot. Ezért egy konfiguráció nélküli futtatás mindent migratál, a spam mappákat is, és a célmappák mellett létrehoz egy második speciális mappák készletet.

### 📊 Sávszélesség-követési beállítások {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Engedélyezi vagy letiltja a sávszélesség-követést az IMAP sebességkorlátozás kezeléséhez (true/false). Az alapértelmezett `false`. Engedélyezés esetén a rendszer fiókonként követi a sávszélesség-használatot, és szüneteltetheti a szinkronizálást, ha elérhetővé st az szolgáltató korlátai. Részletes információért lásd a [Rate Limit Handling](RateLimitHandling.md) részt.
- `BandwidthTracking__DailyLimitMb`: Napi letöltési limit megabایتban, fiókonként. Az alapértelmezett `25000` (25 GB). A sávszélesség-korláttal rendelkező szolgáltatók esetén állítsa ezt a sebességkorlátozásuknak megfelelően (például `2500` a ~2500 MB napi limitrel rendelkező szolgáltatók esetén). A rendszer szünetelteti a szinkronizálást, amint elérhető a limit.
- `BandwidthTracking__WarningThresholdPercent`: A napi limit százalékos értéke, amelynél figyelmeztető üzenetek kerülnek naplóba. Az alapértelmezett `80`. Amikor a sávszélesség-használat eléri ezt a százalékot, figyelmeztetések kerülnek a naplóba, hogy segítsenek a közelítő korlátok nyomon követésében.
- `BandwidthTracking__PauseHoursOnLimit`: Órák száma, amennyi ideig szüneteltetni kell a szinkronizálást, ha elérhető a napi limit. Az alapértelmezett `24`. Ezen időszak után a limit jelző automatikusan törlődik, és a szinkronizálás folytatódik.
- `BandwidthTracking__TrackUploadBytes`: Ez határozza meg, hogy a feltöltési sávszélességet is követni kell-e (true/false). Az alapértelmezett `false`. A legtöbb IMAP szolgáltató csak a letöltést korlátozza, így ez általában nem szükséges.

### 🎯 Kiválasztási beállítások {#selection-settings}
- `Selection__MaxSelectableEmails`: Az e-mailek maximális száma, amelyeket egyszerre lehet kiválasztani.

### 👁️ Megtekintési beállítások (Adatvédelem és megjelenítés) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: A megadott érték határozza meg az e-mailek alapértelmezett megtekintési módját az adatvédelem és a nyomon követés megakadályozása érdekében (true/false). Az alapértelmezett érték `false`.
  - Ha `true` értékre van állítva: Az e-mailek alapértelmezetten plain-text módban nyílnak meg, megakadályozva a tracking pixel, külső képek és web beaconok automatikus betöltését. Ez ajánlott az adatvédelmi és a nyomon követés miatti aggályos felhasználóknak.
  - Ha `false` értékre van állítva: Az e-mailek alapértelmezetten HTML módban nyílnak meg, megjelenítve a formázott e-mailt képekkel és stílusokkal együtt.
  - A felhasználók az e-mail részleteinek oldalán található gomb segítségével váltogathatnak a HTML és a plain-text nézetek között (csak akkor látszik, ha mindkét formátum elérhető).
  - A "Full View" link figyelembe veszi a jelenleg kiválasztott megtekintési módot.

- `View__BlockExternalResources`: Megblokálja a külső erőforrásokat (távoli képek, külső CSS, külső skriptт, web betűtípusok stb.) az HTML e-mail nézetekben a nyomon követés megakadályozása és a jobb adatvédelem érdekében (true/false). Az alapértelmezett érték `false`.
  - Ha `true` értékre van állítva: A külső erőforrásokat kiszűri az HTML e-mailek megjelenítésekor. Csak az inline tartalom és a data URI-k (beleértve a `cid:` hivatkozásokon keresztületi inline csatolmányokat) jelennek meg.
  - Ha `false` értékre van állítva: Az HTML e-mailek az összes eredeti külső erőforrásukkal együtt jelennek meg.
  - **Fontos**: Ez a beállítás csak az e-mail **megjelenítésére** hatással van. A archivált e-maileket az adatbázisban változatlanul tároljuk, az összes eredeti tartalommal együtt.
  - A blokkolt erőforrások közé tartozik:
    - Távoli képek (tracking pixelek, szervereken tárolt külső képek)
    - Külső CSS stíluslapok
    - Külső betűtípusok @font-face használatával
    - Külső CSS importálások @import segítségével
    - Külső háttérképek
  - Engedélyezett erőforrások:
    - Data: URI-ként beágyazott inline képek
    - cid: URI-n keresztül hivatkozott inline csatolmányok
    - Inline CSS stílusok és style tagek
  - Ez a beállítás függetlenül működik a `DefaultToPlainText` beállítástól, és extra adatvédelmi réteget biztosít az HTML e-mailek megtekintésekor.

### 📊 Dashboard Beállítások {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Arról, hogy a számláló kártyák tartalmazzák-e a bejövő és kimenő részeket, illetve hogy a fiókkártya tartalmazza-e a különböző domainok számát. Az alapértelmezett érték `true`. Ha `false` értékre állítva, ezek a számok nem számolódnak ki, hanem nem számoltak és nem is rejtettek: a számlálók egyszerű számot olvasnak le, és a csatolt fájlok számlálója abbahagyja a levélhez való csatolást, ami az egyetlen lekérdezés, amit ezek a részek tesznek extra.
- `Dashboard__SelectablePeriods`: Arról, hogy a dashboard diagramok kínálnak-e feloldást, egy időszakot és nyilasokat a mozgatáshoz. Az alapértelmezett érték `true`. Ha `false` értékre állítva, a diagramok a legutóbbi tizenkét hónapot havi bontozásban és az egész archivum küldőit mutatják, a diagram végpontja úgy válaszol, mintha a paraméter nem is létezne, és a leghatárolóbb küldési dátumot nem keresi fel.
- `Dashboard__CacheSeconds`: Mennyi ideig tárolja a kiszámított dashboard statisztikákat (összesítés a bejövő és kimenő bontozással együtt, fiókonkénti számok, diagram sorozatok, legnépszerűbb küldők, legutóbbi e-mailek, adatbázis méret) a szerver belső memóriában tárolható gyorsítmányában (in-memory cache). Az alapértelmezett érték `60` másodperc. Ha `0` értékre állítja, a gyorsítástás kikapcsolódik, és a statisztikákat mindig újra kell számolni. A feloldás és az időszak minden egyes diagramválasztása külön kerül gyorsítva, így egy dashboard, amely váltogat a választások között, minden egyes választáshoz egyetlen bejegyzést tart meg. A magasabb értékek csökkentik az adatbázis terhelését nagy környezetekben, de cserébe aktuálisabb számok helyett elavultabb adatokat adhatnak. A szinkronizálási állapot jelzések, a tárolási értékek és a legutóbbi e-mailek listája mindig élőben kerülnek lekérdezésre, és nem érinti őket ez a gyorsítás.
- `Dashboard__RefreshIntervalMinutes`: Milyen gyakran számolja újra a `DashboardStatsRefreshService` háttérszolgáltatás a költséges dashboard aggregációkat (összesítés a bejövő és kimenő bontozással együtt, fiókonkénti számok, a fiók domainjeinek száma, az alapértelmezett diagram sorozat a legnépszerűbb küldőkkel együtt, adatbázis méret) a `DashboardStatsCache` adatbázis táblába. Több millió e-mailet tartalmazó telepítéseknél a dashboard ezeket a készített értékeket olvassa ki a teljes archivum aggregálása helyett a lekérdezési útvonalon, ami az körülbelül 10 másodperces első betöltést millisekundok gyorsaságúra csökkenti. A diagram végpont a megadott alapértelmezett választásra (hónapok egy év alatt, bejövő küldők, a jelenlegi időszak) ugyanabból az oszlopból válaszol, így visszatérve az alapértelmezett nézetre az első megjelenítés száma látható; minden más választás a lekérdezéskor kerül kiszámításra, és a fentebb említett belső memória gyorsításban marad meg. Az alapértelmezett érték `15` perc. Ha `0` értékre állítja, a háttérfrissítés kikapcsolódik; ilyenkor a dashboard újra élőben számolja ki a statisztikákat (a fent említett belső memória gyorsítás továbbra is alkalmazható). A megjelenített értékek akár a konfigurált intervallummal elmaradhatnak a tényleges archiv állapot mögöttт. Egy olyan sor, amely más `ShowDirectionSplits` vagy `SelectablePeriods` értékekkel lett kiszámolva, mint amelyek jelenleg érvényesek, nem használható; a dashboard élőben számol tovább a következő frissítésig.

### 🔄 Feladat Beállítások {#jobs-settings}
- `Jobs__RefreshSeconds`: Mennyire gyakran töltődik újra magától a háttérben futó feladatok oldala, amíg egy böngészőfül nyitva tartja azt. Az alapértelmezett `30` másodpercig van beállítva. Állítsát l `0` értékre, hogy kikapcsolja az automatikus újratöltést, és kézzel frissíthessön. Minden egyes újratöltés az összes egyes feladatforrásból (job sources) újra építi fel az oldalt, így sok fiókot tartalmazó telepítéseknél a hosszabb intervallum csökkenti a terhelést, amelyet minden nyitott böngészőfül megismétel.

### 🗃️ Npgsql Beállítások {#npgsql-settings}
- `Npgsql__CommandTimeout`: Az adatbázis parancsainak timeout értéke másodpercekben.

### 📥 Feltöltési beállítások {#upload-settings}
- `Upload__MaxFileSizeGB`: A feltöltések maximum fájlmérete GB-ban.
- `Upload__KeepAliveTimeoutHours`: A feltöltések keep alive timeout értéke órában.
- `Upload__RequestHeadersTimeoutHours`: A request headers timeout értéke órában.

### 📂 Helyi importálási beállítások {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, stb.: A helyi könyvtárak fehérlistája, amelyeket a CLI importálási parancsnak (`--import-mbox`, `--import-eml`) szabad fájlokat olvasnia. Minden bejegyzés egy elérési út a konténeren belül. A beimportálandó fájlokat valamelyik ilyen könyvtárba kell csengősen (mount) rendelnie a Docker volume-ok segítségével. Alapértelmezett: `/data/import` (automatikusan `/app/uploads`-re állítva tartalékként).
  - Amikor a `docker exec` eszközt használja az importálási parancs futtatásához, a `--file` paraméteren keresztül megadott fájl elérési útának valamelyik engedélyezett úton belül kell lennie.
  - Ez a biztonsági intézkedés megakadályozza a tetszőleges fájlrendszer-Hozzáférés lehetőségét a CLI parancsokból.
  - Különféle import forrásokhoz több útvonal is konfigurálható.
  - Részletes használati útmutatóhoz tekintse meg a [CLI parancsokat](CLI.md).

### 📄 📄 CSV Import beállítások {#csv-import-settings}
- `CsvImport__MaxRows`: A single tömeges import során feldolgozandó CSV sorok (postafiókok) maximális száma. Az alapértelmezett érték `5000`. Növelje ezt az értéket nagy telepítéseknél; csökkentse a hatást a single import futtatás adatbázterhelésre.
- `CsvImport__MaxFileSizeBytes`: A feltöltött CSV fájl maximális engedélyezett mérete (byte-okban). Az alapértelmezett érték `10000000` (10 MB). Szükség esetén állítsa ezt az értéket az upload limitjeihez megfelelően.
- A részletes használati utasításokért a CSV útján történő tömeges IMAP fiók importálásról tekintse meg az [Account Import Guide](Account-Import.md) dokumentációt.

### 📤 Audit Export Settings {#audit-export-settings}

Beállítások az audit adatok exportálásának oldalán (csak adminisztrátor számára, a Logs oldalról elérhető). Az export egy ZIP csomagot generál a meglévő archívából táblázatos tömegadatokkal (INDEX.XML + CSV táblák + DTD) külső audit eszközök számára. A használati részletekért lásd az [Audit Data Export Guide](AuditExport.md) dokumentációt.

- `AuditExport__DataSupplierName`: Az "Data supplier name" űrlapmez défautértéke, amely azonosítja az organization nevét a letöltött index fájlban. Alapértelmezett: üres.
- `AuditExport__DataSupplierLocation`: Az "Data supplier location" űrlapmez défautértéke (pl. cég székhely). Alapértelmezett: üres.
- `AuditExport__Comment`: A "Comment" űrlapmez défautértéke, amely szabad szövegként szerepel a letöltött index fájlban. Alapértelmezett: üres.
- `AuditExport__OutputDirectory`: kịpönyű répertoire, ahol a generált ZIP fájlok tárolódnak. A relatív útvonalak az app content root alapján kerülnek feloldva. Alapértelmezett: `exports/audit`.
- `AuditExport__RetentionDays`: Napok száma, amely után a befejezett export fájlokat a napi tisztító folyamat törli. Alapértelmezett: `30`.
- `AuditExport__MaxRangeYears`: A megengedett maximális időtartam az export időszak kezdete és vége között. Alapértelmezett: `10`.
- Az űrlapmezűeket ezek a défautértékek töltik ki előre, de az egyes exportok esetén szerkeszthetők.
- Minden export egy kezdő és egy eredmény bejegyzést ír az access logba (típus: "Audit Data Export"), így a történet módosításálló (revision-safe) bármilyen DB schema változtatás nélkül.

### 🔒 Törlési szabályzat beállításai {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Ez határozza meg, hogy az archivált e-mailek kézi törlése engedélyezett-e (true/false). Az alapértelmezett érték `true`. Ha `false` értékre van állítva:
  - Minden archivált e-mail le van zárva (`IsLocked = true`) az indítás során az adatbázis-megfelelőségi trigger segítségével, megakadályozva bármilyen módosítást vagy törlést az adatbázis szintjén.
  - A manuális törlés (egyedi és tömeges) az alkalmazás szintjén blokkolva van egy hibaüzenettel.
  - Az oszlop alapértelmezett értékét úgy állítja át, hogy az újonnan importált e-mailek is le legyenek zárva.
  - A jelenlegi szabályzat állapota minden indításkor bekerül az AccessLogs táblába (a Logs oldalon "Deletion Policy" bejegyzésekként látható) a módon ellenőrizhetőség érdekében.
  - A helyi megőrzési törlés mentesül alatta: az e-mailek, amelyek egy konfigurált megőrzési időszakon belül esnek, továbbra is törlődnek (a megőrzési folyamat során a törlés előtt azonnal feloldják őket).
- `DeletionPolicy__MaxBulkDeleteEmails`: A felső határ egy szűrőalapú tömeges törléshez (*Delete all matches*). Az alapértelmezett érték `100000`. Ha egy szűrő több e-maillel érint, azt üzenettel visszaküldik, amely kéri a szűrő szűkítését; a folyamat soha nem vágja le csendben a listát. A megőrzési szabályzat által lezárt e-maileket kihagyja és a folyamat állapotsávában jelzi, nem pedig feloldja őket. A folyamat újra ellenőrzi ezt a limiteket és a `DeletionPolicy__DeletionAllowed`-ot indításkor, így a szabályzatváltozás előtt sorba került folyamat leáll, ahelyett hogy a régi beállításokkal futna le.
- **Változatlanság védelem (Immutability protection):** Amikor `IsLocked = true`, az adatbázis-megfelelőségi trigger (`prevent_locked_email_changes`) BLOKKOL MINDANYO MODOSÍTÁST az e-mail soron — minden oszlop védve van, nem csak egy fix mezolistát tartalmazó rész. Az egyetlen mentesült oszlop a `IsLocked` maga (hogy a megőrzési törléshez és az indítási szabályzat alkalmazásához szükséges feloldás lehetséges maradjon), valamint a `FolderName` (hogy az IMAP szinkronizálás frissíthesse a mappát, ha egy e-mail a szerver oldalon mozog). A védelem oszlopfüggenséget nem ismer (JSONB-alapú összehasonlítás), és automatikusan lefedi a későbbi sémakiterjesztéseket is.

### 🕐 Időzóna beállítások {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Az időzóna, amely az e-mailek időbélyegének megjelenítéséhez használja a felületen. IANA időzóna azonosítókat használ (pl. "Europe/Berlin", "Asia/Tokyo"). Az alapértelmezett az "Etc/UCT" a háttatólagos kompatibilitás érdekében. Az e-mailek importálásakor az időbélyegeket erre az időzónára konvertálja a megjelenítés céljából.

### 🎉 ReleaseNotes Beállítások (Verziófrissítési Splash Screen) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: Az az verziós zinó, amelyet ez az példány jelent. Ezt a GitHub release tagekkel hasonlítják össze, és ez dönt el, hogy létezik-e újabb kiadás. Ha üres, a changelog visszatér a fordított assembly verzióra, és a frissítési jelzés kikapcsolva marad.
- `ReleaseNotes__Enabled`: A verziófrissítési splash screen engedélyezése vagy tiltása (true/false). Az alapértelmezett `true`. Ha engedélyezve van, az adminisztrátorok egyetlen alkalommal látni fognak egy changelog modált az alkalmazás frissítése után, amely megmutatja a jelenlegi verzióhoz a GitHub Releases oldalról lekelt release note-okat. Minden adminisztrátor el tud zárni a modalt, és ez csak egy újabb verzió esetén jelenik meg újra. Állítsa `false` értékre a funkció teljes kikapcsolásához, beleértve a frissítési jelzést is.
- `ReleaseNotes__UpdateHintEnabled`: Mutasson-e egy kis toast üzenetet az adminisztrátoroknak a jobb alsó sarokban, ha a GitHub egy újabb, stabil kiadást tesz közzé, mint ami telepítve van (true/false). Az alapértelmezett `true`. A toast linkel a megfelelő kiadáshoz, és verzióonként elvação elhagyható a böngészőben. A tervezetek (drafts) és a előteszt verziók (prereleases) figyelmen kívül maradnak. `ReleaseNotes__AppVersion` igénylés; nélküle semmilyen jelzés nem jelenik meg.
- `ReleaseNotes__UpdateCheckIntervalHours`: Milyen gyakran frissüljön a GitHub release lista, órában megadva. Az alapértelmezett `6`. Egy rövidebb intervallum miatt egy futó példányban hamarabb láthatóvá válik az új kiadás, egy hosszabb pedig csökkenti a GitHub API hívások számát. A minimum `1`.
- `ReleaseNotes__RequestTimeoutSeconds`: Egy GitHub API kérés timeout értéke, másodperceken megadva. Az alapértelmezett `10`. Ha lejárt, a changelog és a frissítési jelzés kikapcsolva marad az adott kísérlet esetében ahelyett, hogy nyitva tartanák a kérést; a következő ellenőrzés megkísérli újra.

### 🔧 Adatbázis karbantartási beállítások {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Az automatikus napi adatbázis karbantartás engedélyezése vagy tiltása (true/false). Az alapértelmezett érték `false`. Ha engedélyezve van, a rendszer automatikusan futtat VACUUM ANALYZE műveleteket az adatbázis teljesítményének optimalizálása és a felesleges adatok (bloat) elkerülése érdekében. További részletekért lásd a [Database Maintenance Guide](DatabaseMaintenance.md) dokumentációt.
- `DatabaseMaintenance__DailyExecutionTime`: Az az időpont a nap folyamán, amikor az adatbázis karbantartásnak futnia kell, 24 órás formátumban (HH:mm). Az alapértelmezett érték `02:00`. Egy olyan időpontot válasszon, amikor alacsony a rendszer aktivitása.
- `DatabaseMaintenance__TimeoutMinutes`: A karbantartási műveletekhez megengedett maximális időpercben. Az alapértelmezett érték `30`. Növelje ezt az értéket nagyobb adatbázusok esetén.

### 🧬 Melléklet de Duplikáció Beállításai {#attachment-deduplication-settings}
A melléklet de duplikáció minden egyedi melléklet-payloadt csak egyszer tárol (SHA-256 alapú tartalom alapú azonosítás), és ez egy **alapvető funkció, amely mindig engedélyezve van** – szándékosan nincs rá kapcsoló. Csak az alábbi tömb/ütemezési paraméterek finකරímíthatók. A teljes részletért lásd a [Attachment Deduplication Guide](AttachmentDeduplication.md) dokumentációt.

- `AttachmentDeduplication__BatchSize`: Az meglévő adatok egyszeri háttérben történő migráciája során tranzakciónként migrált meglévő mellékletek száma. Az alapértelmezett érték `200`. A nagyobb értékek gyorsabban migráznak, de több memóriát/adatbázis-terhelést igényelnek batchelésenként.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Opcionális szünet (millisekundyékban) a migrációs batchek között a leterhelt rendszereken a database load szabályozására. Az alapértelmezett érték `0` (nincs szünet).
- `AttachmentDeduplication__StartupDelaySeconds`: Késleltetés (másodpercekben) az alkalmazás indítása után, mielőtt a háttérben futó migráció megkezdődne, hogy legyen ideje befejezni a schema migrációt. Az alapértelmezett érték `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Az állandóan futó garbage collection intervalluma (órákban), amely eltávolítja azokat a melléklet-payloadokat, amelyekre már nincs hivatkozás egyetlen e-mailből sem. Az alapértelmezett érték `12`. Ez függetlenül fut `DatabaseMaintenance__Enabled` értékétől.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Az adatbázis parancs timeout (másodpercekben) a migrációs batch műveletekhez (INSERT SHA-256 hasholással és UPDATE). Az alapértelmezett érték `300` (5 perc). Növelje ezt az értéket nagyon nagy adatbázok vagy mellékletek esetén, vagy csökkentse, ha gyorsabb hibale discovery-t szeretne. Ha egy batch még mindig timeout-ot kap, a szolgáltatás automatikusan megpróbálkozik a batch méretének felével.

### 💾 Fiók tárolási beállítások {#account-storage-settings}

A fiókonkénti tárolási megjelenítés a Dashboard "Account Overview" táblázatában és a MailAccounts "Show All" táblázatában minden egyes fiók adatbázis-tárolási felhasználását (összes e-mail mező + csatolmányok) mutatja. Egy autark háttérszolgáltatás (`AccountStorageRefreshService` kiszámítja az értékeket az egyes oszlopok PostgreSQL `pg_column_size` összeadása révén (minden e-mail mezőjét lefedve az e-mail testek beolvasása nélkül), majd elmenti őket a `AccountStorageCache` táblázatba. Ez a szolgáltatás függetlenül fut a `DatabaseMaintenance__Enabled`-tól.

- `AccountStorage__Enabled`: A tárolási frissítési szolgáltatás engenable vagy kikapcsolása (true/false). Az alapértelmezett érték `true`. Engedélyezés esetén a szolgáltatás indításkor felfüggeszthető backfill folyamatot futtat a függőben lévő fiókok számára (hibatűrő a `AccountStorageBackfillState` révén), ezt követően pedig napi teljes frissítést végez. Kikapcsolva a tárolási értékek csak az e-mailek szinkronizálása, importálása vagy törlésének során frissülnek, de a megjelenített értékek idővel elavultтá válhatnak.
- `AccountStorage__DailyExecutionTime`: A nap folyamán megadott időpont (24 órás formátum `HH:mm`) az összes fiók napi teljes frissítéséhez. Az alapértelmezett érték `02:30`. Válasszon egy olyan időpontot, amikor a rendszer aktivitása alacsony.
- `AccountStorage__BackfillDelayMs`: Késleltetés (millisekundyogban) a fiókok között az indítási kezdeti backfill során. Az alapértelmezett érték `5000`. Az alacsonyabb értékek felgyorsítják a backfill folyamatot, de növelik az adatbázis terhelését; nagyon nagy archívumok esetén emelje ezt az értéket az adatbázis túlterhelésének elkerülése érdekében.
- `AccountStorage__RefreshBatchDelayMs`: Késleltetés (millisekundyogban) a fiókok között a napi teljes frissítés során. Az alapértelmezett érték `1000`. Az alacsonyabb értékek felgyorsítják a frissítést, de növelik az adatbázis terhelését; nagyon nagy archívumok esetén emelje ezt az értéket.
- `AccountStorage__CommandTimeoutSeconds`: Adatbázis parancsbeküldési timeout (másodpercekben) a fiókonkénti tárolási számításhoz (`pg_column_size` egy fiók összes során). Az alapértelmezett érték `300` (5 perc). A számítás minden e-mail sort beolvas, beleértve a TOAST adatokat is, ami nagyon nagy archívumok esetén (250k+ e-mail) jóval egy percnél tövet is igénybe vehet; a korábbi 30 másodperces Npgsql alapértelmezett érték miatt a frissítés csendben elbukott, és a dashboard elavult értékeket mutatott. Ha a számítás még mindig timeoutot idéz az Ön archívumánál, emelje ezt az értéket.

> 💡 **Megjegyzés**: A tárolási értékek minden e-mail szinkronizálás, importálás vagy retention törlés után azonnal frissülnek, így a megjelenített értékek a napi frissítés nélkül is aktuálisak maradnak. A napi frissítés egy biztonsági háló, amely feldolgozza az extrém eseteket (pl. közvetlen adatbázis-módosítások).

### 📝 Naplózási beállítások {#logging-settings}
- `Logging__LogLevel__Default`: Az alkalmazás alapértelmezett naplózási szintje. A rendelkezésre álló szintek: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Az alapértelmezett `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: A naplózási szint az ASP.NET Core framework üzenetekhez. Az alapértelmezett `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: A naplózási szint az Entity Framework adatbázis parancsokhoz. Az alapértelmezett `Warning`.

### 🛡️ Biztonsági beállítások {#security-settings}
- `AllowedHosts`: A host nevek pontosvesszővel tagolt listája, amelyeket az alkalmazás szolgálhat. Ez segít megelőzni az HTTP Host header támadásokat. Példa: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Fontos**: Ne használja a `*` értéket termelési környezetben, mivel az kikapcsolja a host header validációt.

#### 🔐 Hitelesítő adatok tárolás alatti titkosítása {#credential-encryption-at-rest}
A levelezési fiókok hitelesítő adatai (IMAP jelszó, M365 client secret és MSA OAuth tokenek) titkosíthatók az adatbázisban. A titkosítási kulcsot **kívül** tartjuk az adatbázison (környezeti változó / Docker secret), így az adatbázis mentés (dump) önmagában sem elég a hitelesítő adatok elolvasásához.

- `Security__CredentialEncryptionKey`: Base64 kódolt 256 bites (32 byte) kulcs-titkosítási kulcs. Generáljon egyet a következővel:
  ```bash
  openssl rand -base64 32
  ```
  A değeri beállítása környezeti változóként (ajánlott: `Security__CredentialEncryptionKey` a compose fájlban vagy a konténer környezetében). Az alkalmazás nem támogatja a `_FILE` stílusú Docker secreteket — ha Docker secretet használ, mountolja azt, és töltse be a tartalmát a környezeti változóba az entrypointben. Soha ne committeleyin kulcsot a `appsettings.json`-be. Ha kulcs van konfigurálva, a hitelesítő adatokat íráskor titkosítják, és egy háttérszolgáltatás egyszer leírósítja a meglévő plain text hitelesítő adatokat.
- `Security__EncryptAccountCredentials`: Az új/frissített hitelesítő adatértékek titkosításának engedélyezése vagy tiltása (true/false). Az alapért값 `true`. Ha nincs konfigurált kulcs, a hitelesítő adatok plain text formátumban vannak tárolva, és a folyamat megindulásakor figyelmeztetés kerül a logokba — a meglévő telepítések nem érintettek, amíg kulcsot nem adnak meg.
- `Security__CredentialEncryptionBackfill`: Arról, hogy a háttérszolgáltatás a folyamat megindulásakor titkosítsa-e a meglévő plain text hitelesítő adatokat (true/false). Az alapért값 `true`. Ha nincs konfigurált kulcs, semmi nem történik.
- `Security__CredentialEncryptionKeyPrevious`: Opcionális korábbi kulcs, amely csak az azzal titkosított értékek visszaengedéséhez szolgál (kulcscsere esetén). Az új értékeket mindig a `Security__CredentialEncryptionKey` kulccsal titkosítják.

  > ⚠️ **Kulcscsere (Key rotation)**: a csere után tartsa konfigurálva a `Security__CredentialEncryptionKeyPrevious` értéket **amíg minden hitelesítő adat nem íródta át** (például a fiók szerkesztésével vagy egy automatikus OAuth token frissítéssel) — az ól kulccsal titkosított meglévő értékek *nem* kerülnek automatikusan az új kulccsal újratitkosításra. Amint eltávolítja a korábbi kulcsot, minden olyan hitelesítő adat, amely még ezzel van titkosítva, olvashatatlanná válik, és újra kell megadni. Amíg konfigurálva van, olyan biztonságosan kell kezelni, mint a jelenlegi kulcsot.

> ⚠️ **Tartja biztonságban a kulcsot**: A kulcs nélkül a titkosított hitelesítő adatok nem olvashatók vissza, és újra kell őket adni. Mentse el a kulcsot az adatbázis mentéseivel együtt (de különállóan)!
>
> 💡 **Meglévő telepítések**: Frissítsen biztonságosan — amíg nincs beállítva `Security__CredentialEncryptionKey`, semmi sem változik. Csak akkor állítsa be a kulcsot, amikor készen áll; a meglévő plain text hitelesítő adatok olvashatók maradnak, és a háttérben titkosításra kerülnek.

### 🔐 OIDC Konfiguráció {#oidc-configuration}

Az OpenID Connect hitelesítés részletes beállítási útmutatóért lásd a [OIDC Implementation Guide](OIDC_Implementation.md) dokumentációt.

#### Alapvető OIDC Beállítások {#basic-oidc-settings}
- `OAuth__Enabled`: Az OIDC hitelesítés engedélyezése vagy tiltása (true/false)
- `OAuth__Authority`: Az OpenID Connect authority URL (pl. https://sts.windows.net/{TENANT-ID}/ az Azure AD esetén)
- `OAuth__ClientId`: Az azonosító szolgáltató által kiosztott client ID
- `OAuth__ClientSecret`: Az azonosító szolgáltató által kiosztott client secret
- `OAuth__DisplayName`: Opcionális megjelenítendő név, amely az OIDC bejelentkezési gombon és az automatikus átirányítási oldalon látható (pl. `PocketID SSO`). Ha elhagyja, az általános "Login with OAuth" felirat kerül alkalmazásra.
- `OAuth__ClientScopes__0`: Az első kért scope az azonosító szolgáltatótól (openid)
- `OAuth__ClientScopes__1`: A második kért scope az azonosító szolgáltatótól (profile)
- `OAuth__ClientScopes__2`: A harmadik kért scope az azonosító szolgáltatótól (email)
- `OAuth__ResponseMode`: Az engedélyezési kérelem opcionális válaszformátuma (`query`, `fragment` vagy `form_post`). Az alapértő érték `form_post`, az ASP.NET Core alapértelmezettje. `query` értékre kell állítani az olyan szolgáltatók esetén, amelyek nem támogatják a `form_post`-ot, például a Synology SSO Server. Lásd: [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Az új OIDC felhasználók automatikus jóváhagyása manuális admin jóváhagyás nélkül (true/false). Az alapértő érték `false`.

#### Felhasználói Provisioning Beállítások {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Az új OIDC felhasználók automatikus jóváhagyása manuális admin jóváhagyás nélkül (true/false). Az alapértő érték `false`. Ha engedélyezve van, az OIDC szolgáltatón keresztül hitelesített felhasználók azonnal aktiválódnak, és hozzáférhetnek az alkalmazáshoz. Ha letiltva van (alapértő), az új OIDC felhasználók inaktív állapotban kerülnek létreállításra, és szükség van a rendszergazda által történő manuális aktiválásra. Részletes információért lásd az [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users) részt.
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, stb.: E-mail címek, amelyek automatikusan adminisztrátorként kerülnek provisionálva. Azok a felhasználók, akik ezekkel az e-mail címekkel rendelkeznek, az első OAuth bejelentkezéskor aktív adminisztrátorként kerülnek létreállításra, megkerülve a normál jóváhagyási folyamatot. Az e-mail címek összehasonlítása esetén nem számít a kis-/nagybetű.

#### Jelszó nélküli Bejelentkezési Beállítások {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: A felhasználónév/jelszó bejelentkezés letiltása (true/false). Az alapértő érték `false`. Ha engedélyezve van, a jelszó űrlap elrejtésre kerül, **és** a szerver minden jelszó POST kérést elutasít, így csak az OAuth bejelentkezés áll rendelkezésre. A `OAuth__Enabled=true` beállítása szükséges hozzá. Ha az OAuth letiltva van, ez a beállítás nem hatással rendelkezik.
- `OAuth__AutoRedirect`: A felhasználók automatikus átirányítása az OAuth szolgáltatóhoz (true/false). Az alapértő érték `false`. A működéshez szükséges, hogy a `OAuth__DisablePasswordLogin` értéke `true` legyen. A felhasználók egy rövid betöltési képernyőt fognak látni az átirányítás előtt.

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

Bár az alkalmazáshoz egy HTTPS-alapú reverse proxy keresztül valóHozzáférés a cél, a Kestrel web szervert úgy is konfigurálhatja, hogy SSL/TLS tanúsulványokat használjon. Ez végszélről végszélre védi a kommunikációt a reverse proxy és az alkalmazás konténer között.

### Konfigurációs lépések {#configuration-steps}

1. **Generáljon vagy szerezzen be egy SSL tanúsulványt** PFX formátumban (például: `localhost.pfx`)

2. **Adja meg a következő környezeti változókat** a `docker-compose.yml` fájlban a `mailarchive-app` szolgáltatáshoz:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Frissítse a portok mappolását** a `mailarchive-app` szolgáltatásban:

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

5. **Helyezze el a tanúsítvány fájlát** (például: `localhost.pfx`) a gazdagép rendszerének `./certs` könyvtárában.

### A környezeti változók magyarázata {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: HTTP végpont URL (alapértelmezett: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: HTTPS végpont URL (alapértelmezett: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: A PFX tanúsítvány fájlának elérési útja a konténeren belül
- `Kestrel__Endpoints__Https__Certificate__Password`: Jelszó a PFX tanúsítvány fájljához

> 💡 **Megjegyzés**: Ez a konfiguráció opcionális. Ha HTTPS-alapú reverse proxit használ (ajánlott), a reverse proxy és az alkalmazás közötti kommunikáció megmaradhat HTTP protokollon. Azonban érzékeny környezetekben a maximális biztonság érdekében érdemes lehet a Kestrelnél is engedélyezni a HTTPS-t az egész kommunikációs útvonal titkosításához érdekében.

## 🔑 Titkosítás kezelése a term production környezetben {#secrets-management-for-production}

A szenetit adatok, például az adatbázis jelszavak, admin hitelesítő adatok és OAuth client titkok közvetlen hardkodása a `docker-compose.yml` fájlban biztonsági kockázatot jelent, különösen, ha a fájl bekerül a verzióellenőrzésbe (version control). A javasolt megközelítés ezeknek az értékeknek a külső halegyítése egy `.env` fájlba, amelyet a Docker Compose automatikusan betölt.

### 📄 A `.env` fájl {#the-env-file}

Hozzon létre egy `.env` fájlt ugyanabban a mappában, ahol a `docker-compose.yml` található, a következő tartalommal:

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

**Fontos**: A Docker Compose automatikusan olvassa be a `.env` fájlt ugyanabból a mappából — nem kell manuálisan hivatkoznia rá. A `.env` fájlban definiált összes változó elérhető a `docker-compose.yml` számára a `${VARIABLE_NAME}` szintaxis segítségével.

### 🐳 Adaptált `docker-compose.yml` {#adapted-docker-composeyml}

A telepítési lépésekből származó `docker-compose.yml`-at úgy kell adaptálni, hogy a hardkodált titkok helyett helyőrzőket (placeholders) használjon:

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

### 📋 `.env.example` sablon adminisztrátoroknak {#envexample-template-for-administrators}

Hozzon létre egy `.env.example` fájlt a repellitoriumában, amely sablonként szolgál más adminisztrátorok számára. Ez tartalmaz helyőrző értékeket és dokumentációt, de **nem tartalmaz valódi titkokat**:

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

- **Használja a `.env.example`-ot sablonként**
- **Állítson be szigorú fájlengedélyeket** — Korlátozza a hozzáférést a `.env` fájlhoz:
  ```bash
  chmod 600 .env
  ```
- **Használjon erős jelszavakat** — Generáljon hosszú, véletlenszerű jelszavakat (legalább 16 karakter), betűkkel, számokkal és speciális karakterekkel keverve.
- **Korlátozza a `.env` fájlhoz való hozzáférést** — Csak a Docker Compose-t futtató felhasználónak kell legyen olvasási jogai a `.env` fájlhoz.

## 🔒 Biztonsági megjegyzések {#security-notes}

- Használjon erős jelszavakat, és változtassa meg a gyári hitelesítő adatokat. A jelszavak legalább 12 karakter hosszúak legyenek, és tartalmazzanak nagy és kisbetűket, számokat és speciális karaktereket. Kerülje a gyakori szavak vagy könnyen meg đoánható információk használatát.
- Üretőben fontos lehet HTTPS implementálása reverse proxy segítségével
- A PostgreSQL adatbázis rendszeres mentése javasolt. A részletes mentési és visszaállítási folyamatokért lásd a [Backup and Restore Guide](BackupRestore.md) dokumentációt.
