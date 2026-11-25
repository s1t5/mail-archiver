# 🛠️ Mail Archiver beállítási útmutató {#mail-archiver-setup-guide}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Ez az útmutató részletes utasításokat tartalmaz a Mail Archiver alkalmazás Docker Compose segítségével történő beállításához.

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

3. Szerkesztse az adatbázis konfigurációját a `docker-compose.yml` fájlban, és állítson be biztonságos jelszót a `POSTGRES_PASSWORD` változóba és a `ConnectionString` beállításba.

4. Ha hitelesítést szeretne használni (ami erősen javasolt), határozzon meg egy `Authentication__Username` és `Authentication__Password` értéket, amelyet a rendszergazda felhasználóhoz használ.

5. A `TimeZone__DisplayTimeZoneId` környezeti változót az Ön által preferált időzónához igazítsa (az alapértelmezett az "Etc/UCT"). Bármely IANA időzóna azonosítót használhat (pl. "Europe/Berlin", "Asia/Tokyo").

6. Opcionálisan konfigurálhatja a `Logging__LogLevel` környezeti változókat az alkalmazás logok részletességének szabályozására. Az elérhető lehetőségekért tekintse meg az alábbi Logging Settings részt.

7. Konfiguráljon egy a választásában álló reverse proxy-t https és hitelesítés mellett az alkalmazás biztonságos hozzáféréséhez.

> ⚠️ **Figyelem**: Magán az alkalmazás nem biztosít titkosított hozzáférést https keresztül! Ez egy reverse proxy prostředvén kell beállítani!

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
   - Navigáljon az "Email Accounts" részhez
   - Kattintson a "New Account" gombra
   - Adja meg a szerver adatait és a hitelesítő adatokat
   - Mentés és az archiválás megkezdése!
   - Ha szeretné, hozzon létre egyéb felhasználókat, és rendeljen hozzájuk fiókokat.

## 📚 Környezeti változók magyarázata {#environment-variable-explanations}

### 🗄️ Adatbázis-kapcsolat {#database-connection}
- `ConnectionStrings__DefaultConnection`: A PostgreSQL adatbázishoz való csatlakozási karakterlánc. A szükséges esetén módosítsa a `Host`, `Database`, `Username`, és `Password` értékeket.

### 🔐 Hitelesítési beállítások {#authentication-settings}
- `Authentication__Username`: A rendszergazda fiók felhasználóneve.
- `Authentication__Password`: A rendszergazda fiók jelszava.
- `Authentication__SessionTimeoutMinutes`: A munkamenet lejárati ideje perces egységben. Ez a nem tartós (non-persistent) bejelentkezési cookie-ra vonatkozik; azok a felhasználók, akik bejelölték a **Remember Me** opciót a bejelentkezési űrlapon, helyette egy fix 7 napig érvényes, tartós (persistent) cookie-t kapnak. See [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: A hitelesítési cookie neve.
- `Authentication__CookieSameSite`: A hitelesítési, munkamenet (session) és CSRF védelem cookie-k SameSite attribútumának konfigurálása. Az érvényes értékek:
  - `Strict` (alapértelmezett): Maximális biztonság. A cookie-k csak azonos oldalbeli (same-site) kérések esetén küldődnek. Ez problémákat okozhat, ha külső linkekről navigálnak az alkalmazáshoz (pl. egy másik weboldalról kattintanak egy linkre), mivel a meglévő munkamenet nem lesz felismerhető.
  - `Lax`: Javasolt reverse proxy használata esetén. A cookie-k a legfelső szintű navigációk (top-level navigations) és az azonos oldalbeli kérések esetén is küldődnek, lehetővé téve a felhasználók számára, hogy külső linkről az alkalmazáshoz navigáljanak, miközben megmarad a CSRF védelem a POST kéréseknél.
  - `None`: A cookie-k minden kérésrel küldődnek. HTTPS és a `Secure` attribútum szükséges hozzá. Csak akkor használja, ha specifikus, oldalközi (cross-site) igényei vannak, és érti a biztonsági következményeket.

### 🔌 REST API Beállítások {#rest-api-settings}
Az opcionális, olvasható REST API **alapértelmezés szerint le van engedélyezve**. A teljes referenciához tekintse meg a [REST API guide](API.md) dokumentációt.
- `Api__Enabled`: A master kapcsoló a olvasható REST API-hoz (alapértelmezett `false`). Ha `false`, minden `/api/*` útvonal `404` értéket ad vissza.
- `Api__AllowAttachmentDownloads`: Engedélyezi a csatolmányбайtok letöltését az API keresztül (alapértelmezett `true`). Ha `false`, a csatolmány végpont `403` értéket ad vissza.
- `Api__AllowMessageDownloads`: Engedélyezi a teljes üzenetek letöltését az API keresztül, mind az újból felépített `/eml`, mind az eredeti `/raw` formátumban (alapértelmezett `true`). Ha `false`, mindkét végpont `403` értéket ad vissza. Egy teljes üzenet magával viszi a csatolmányait, ezért **mind** ennek a kapcsolónak, mind a `Api__AllowAttachmentDownloads` beállításnak `true` kell lennie; bármelyik kikapcsolása mindkét végpont leállítását eredményezi.
- `Api__EnableSwaggerUi`: A Swagger UI megjelenítése a `/apidocs` helyen, és az OpenAPI dokumentum a `/apidocs/spec/v1.json` helyen (alapértelmezett `true`). Mindkettőhez bejelentkezett böngésző-sesszió szükséges.
- `Api__DefaultPageSize`: Az alapértelmezett oldalkelmézet a listázó végpontokhoz, ha nincs megadva (alapértelmezett `20`).
- `Api__MaxPageSize`: A maximum megengedett oldalkelmézet; a nagyobb kéréseket korlátozni fogja (alapértelmezett `100`).
- `Api__RateLimitPerMinute`: Fix ablakos kéresterjesztési keret API-kulcsonként percenként (alapértelmezett `120`).

### 🤖 MCP Server Beállítások {#mcp-server-settings}
Az opcionális MCP (Model Context Protocol) szerver ugyanazt az olvasási joggal ellátott e-mail hozzáférést biztosítja az AI ügynökök számára a Streamable HTTP transzporthon keresztül, mint a REST API. Ez **alapértelmezetten ki van kapcsolva**. A teljes útmutatóért lásd az [MCP guide](MCP.md) dokumentációt. Az MCP ugyanazokat az API kulcsokat használja újra, mint a REST API (`ma_...` bearer tokenek), és ugyanazt a felhasználónkénti fiókalapú hozzáférést (per-user account-access scoping) biztosítja.
- `Mcp__Enabled`: A MCP szerver fő kapcsolója (alapértelmezetten `false`). Ha `false`, a `/mcp` endpoint `404` értéket ad vissza.
- `Mcp__AllowAttachmentDownloads`: Engedélyezi, hogy a `get_attachment` eszköz csatolmányбайt (attachment bytes) adjon vissza (alapértelmezetten `true`). Ha `false`, az eszköz hibát ad vissza.
- `Mcp__DefaultPageSize`: Az `search_emails` által használt alapértelmezett oldalszín (page size), ha a hívó elhagyja a `pageSize` paramétert (alapértelmezetten `20`).
- `Mcp__MaxResults`: A `search_emails` oldalszínének felső határa; a nagyobb értékek korlátozásra kerülnek (alapértelmezetten `100`).
- `Mcp__MaxAttachmentBytes`: A maximális csatolt fájl mérete (byte-okban), amelyet a `get_attachment` eszköz base64 formátumban ad vissza inline módon; a nagyobb csatlakozmányokat elutasít (alapértelmezetten `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Engedélyezi a `get_email_eml` és a `get_email_raw` eszközök számára, hogy teljes üzeneteket adjanak vissza (alapértelmezetten `true`). Ha `false`, az eszközök hibát adnak vissza. Egy teljes üzenet tartalmazza a csatolt fájljait is, ezért **mind** ez a kapcsoló, mind a `Mcp__AllowAttachmentDownloads` értéknek `true` kell lennie; bármelyik egyikének kikapcsolása mindkét eszközt letiltja.
- `Mcp__MaxMessageBytes`: A maximális üzenetméret (byte-okban), amelyet a `get_email_eml` és a `get_email_raw` eszköz inline módon ad vissza; a nagyobb üzeneteket elutasít (alapértelmezetten `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Fix ablakos (fixed-window) kérést budzsét API kulcsonként percenként (alapértelmezetten `120`).

### 📨 MailSync Beállítások {#mailsync-settings}
- `MailSync__IntervalMinutes`: Az e-mail szinkronizálás közötti intervallum percében. Ez a globális alapértelmezett; minden egyes fiók ezt egyenként felülbírálhatja a Create/Edit oldalon (hagyja üresen a megmaradt alapértelmezettnek).
- `MailSync__FullSyncIntervalHours`: Opcionális globális alapértelmezett az automatikus teljes újraszinkronizálásokhoz, órában. Ha nincs beállítva (az alapértelmezett), nem fut automatikus teljes szinkronizálás, hac Unless egy fiókra vonatkozó `FullSyncIntervalHours` érték van megadva a Create/Edit oldalon. A fiókonkénti értékek felülírják ezt a globális alapértelmezettet.
- `MailSync__TimeoutMinutes`: Fiókonkénti szinkronizálási timeout minutespercében. Egy szinkronizálás, amely tovább tart, a következő üzenet határonál megáll, megőrzi a checkpointjeit, és a következő futtatáskor folytatja; a feladatot `Timed Out` jelzi, és a `LastSync` nem halad előre. Csak a tervezett szinkronizálásokra vonatkozik; a felületről indított szinkronizálás (manuális szinkronizálás vagy teljes újraszinkronizálás) timeout nélkül fut. A `0` (az alapértelmezett) vagy bármely nem pozitív érték nem jelent timeoutot. Frissítés előtt ellenőrizze az értéket, ha nagy postafiókokat szinkronizál. Lásd: [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Az IMAP kapcsolatoknak megfelelő kapcsolat megszakítási idő (connection timeout) másodpercekben.
- `MailSync__CommandTimeoutSeconds`: Az IMAP parancsoknak megfelelő parancsmegszakítási idő (command timeout) másodpercekben.
- `MailSync__AlwaysForceFullSync`: Az, hogy mindig kényszerítsen teljes szinkronizálást (true/false).
- `MailSync__IgnoreSelfSignedCert`: way, hogy figyeljen-e az önmagában aláírt tanúsítványokra (true/false).
- `MailSync__MaxConcurrentSyncs`: Hány fiók szinkronizálása futhat egyszerre. Alapértelmezett `1` (soros, háttatocompatibilis). Az ükemény (scheduler) akkor tölti vissza a helyeket, amint azok szabadon szólnak, nem pedig egy egész batch megvárására, így egy lassú postafiók már nem tartja vissza a többit. Növelje a több fiók egyidejű szinkronizálásához — vegye figyelembe a szolgáltató sebességkorlátait és a helyi erőforrás-használatot. Lásd: [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Opcionális csúszó késés másodpercekben, amelyet minden fiók szinkronizálási feladat végén alkalmaznak. Alapértelmezett `0` (nincs késés). Hasznos a hirtelen kezdetek elkerülésére, amikor `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Hány olyan problémát emlékezik meg egy szinkronizálási feladat minden típusból a fiók oldalán. A hibás mappák, a hiányzó mappák és a hibás üzenetek külön költségvetésűek, így egy típusú tömeges hiba nem tudja eltávolítani a többi fajta hibát a nézetből; a keret túlmutató bármit számlál, nem pedig tárol. Alapértelmezett `20`. A `0` kikapcsolja a részletezést, és csak a számlálókat hagyja meg. Lásd: [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Mely mappák kerülnek ki исклюva a szinkronizálásból **minden** fiók esetében, a fiókonkénti kizárási lista mellett is. Alapértelmezett üres, így a meglévő beállítások nem változnak. A két lista **kiegészíti egymást** — egy mappa akkor kerül át跳, ha bármelyik lista alapján megfelel — és mindkettő ugyanazokat a megfeleltetési szabályokat használja: pontos egyezés a teljes útvonalon, pontos egyezés a mappa neven, útvonali suffix egyezés (így `Drafts` is megfelel a `INBOX/Drafts` és `INBOX.Drafts` értékeknek), és minden, ami a mappa alatt található, amennyiben `MailSync__ExcludeSubfolders` be van kapcsolva. A megfeleltetés nem figyeli a kisbetű/nagybetű különbséget. Hasznos, ha sok postafiókot importál ugyanazon a szerveren, ahol az alternatíva minden fiókra azonos kizárási lista fenntartása lenne. Alapértelmezett nincs kivételes mappa; hogy mely neveket érdemes felsorolni, az a szerverről és annak nyelvéről függ. Példa egy postafiók-fánra, amely tartalmaz kalendárium és névjegyzék mappákat is:
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
- `MailSync__ExcludeSubfolders`: Azt, hogy egy kizárási bejegyzés a megnevezett mappa alatti mappákat is lefedi-e. Egyaránt vonatkozik a fiókonkénti listára és a telepítés-szintű listára is, valamint mindkét szolgáltatóra. Alapértelmezett `false`, ami a beállítás létezése előtti viselkedést követi: egy `Deleted Items` bejegyzés csak azt a mappát jelöli meg, és egy faveret a gyökér és minden alatta lévő mappa megnevezésével lehet távol tartani az archívumtól. Állítsa `true` értékre, hogy egy bejegyzés a maga alatt lévő fát is lefedje — egy `Deleted Items` bejegyzés ilyenkor a `Deleted Items/2024` és az ág többi részét is távol tartja az archívumtól. A szabály az útvonalszeparátorhoz rögzül, így egész mappákat vesz igénybe, és soha egy név részét: `Archive` lefedi a `Archive` és a `Archive/Team` elemeket, és érintetlenül hagy egy `Team Archive Review` nevű mappát. A `.` is szeparátorként számít, ugyanúgy, ahogy a suffix szabály is kezeli.

### 📧 Eredeti üzenetek tárolási beállításai {#original-message-storage-settings}

Alapértelmezés szerint az archívum csak az e-mail feldolgozott részeit őrzi meg, így egy exportált `.eml` egy rekonstrukció: az átviteli kódolások (transfer encodings), a MIME határértékek (MIME boundaries) és a fejlécösszezasztások (header folding) eltérhetnek a megkapott üzenettől, a DKIM aláírások pedig már nem igazolhatóak. `RawMessages:Enabled` esetén az eredeti üzenet teljes formájában is tárolásra kerül.

| Paraméter | Alapértelmezett | Cél |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Minden új archivált e-mail eredeti változatának tárolása. |
| `RawMessages__Storage` | `postgres` | A byte-ok tárolási helye: `postgres` vagy `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | A nagyobb üzenetek az eredeti változat nélkül kerülnek archiválásra. `0` = nincs limit. |
| `RawMessages__IncludeGraph` | `true` | Microsoft 365 (Graph) fiókok esetén is az eredeti üzenetek tárolása. |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Az üzenetfájlok könyvtára. Dockerben futó futtatás esetén egy felszerelt (mounted) volument kell指 指向. |
| `RawMessages__FileSystem__ShardDepth` | `2` | A könyvtár mélysége, amelyre az e-mail azonosító kiterjed. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | A SHA-256 ellenőrzése minden letöltésnél. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Az olyan üzenetfájlok törlése, amelyekre már nincs hivatkozás. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Ezen tisztító futtatás gyakorisága. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Ebbennél fiatalabb fájlok soha nem kerülnek törlésre. |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Indítás utáni első tisztító futtatás késleltetése, hogy az éppen írás alatt lévő importálás ne kerüljön törlésre. |

!!! warning "A fájlrendszer háttérnek (filesystem backend) szükség van egy felszerelt volumentre"
    `RawMessages__Storage=filesystem` esetén az üzenetfájlok a tároló adatbázison kívül élnek.
    Bind mount nélkül a konténer írható rétegében (writable layer) vannak, és a következő konténer újrakreációkor elvesznek, míg az adatbázis sorai megmaradnak. A könyvtárat az adatbázis mentésével (database dump) együtt biztonságba mentse.

Részletes leírást itt talál: **[Original Message Storage](RawMessages.md)**: hol érhető el az eredeti üzenet, a tárolási háttértechnológiák, hogyan lehet a meglévő üzeneteket mozgatni közöttük a `--migrate-raw-storage` segítségével, és mit érdemes menteni.

### 📈 📈 Monitoring Beállítások {#monitoring-settings}

| Paraméter | Alapértelmezett | Cél |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | Epektől idősebb engedélyezett fiókok esetén a `/api/v1/health` jelent `Degraded`. A `0` kikapcsolja a ellenőrzést. |
| `Metrics__Enabled` | `false` | Engedélyezi a Prometheus metrics endpointet és annak frissítési ciklusát. |
| `Metrics__RefreshMinutes` | `5` | Mennyire gyakran épül fel újra a metrics snapshot. 1 alatti értékek 1-re kerülnek emelve. |

Az egészségi (health) és metrics endpointekhez admin API key és `Api__Enabled=true` szükséges. Lásd a
**[Health and Metrics](Monitoring.md)** részt az endpointekről, a metrik nevékről, a Prometheus
konfigurációról és a Docker health checkről.

### 📤 BatchRestore Beállítások {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Az e-mailek száma, amely aszinkron feldolgozást vált ki.
- `BatchRestore__MaxSyncEmails`: A szinkron feldolgozáshoz használható e-mailek maximális száma.
- `BatchRestore__MaxAsyncEmails`: Az aszinkron feldolgozáshoz használható e-mailek maximális száma.
- `BatchRestore__SessionTimeoutMinutes`: A batch restore session timeout percenként.
- `BatchRestore__DefaultBatchSize`: Az e-mail műveletek alapértelmezett batch mérete.

### 🏢 Bérlőkezelési beállítások {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: A single Tenant Management művelet során hozzáadható mailboxok maximális száma. Az alapértő érték `1000`. Növelje ezt nagyon nagy bérlők esetén, vagy csökkentse, hogy megakadályozza a véletlenszerű tömeges importálást. Ha a limit túllépésre kerül, a művelet elutasításra kerül validációs hibával, és nem kerülnek létre fiókok. Részletekért lásd a [M365 Tenant Import Guide](M365TenantImport.md) dokumentációt.

### 🐄 Mailcow beállítások {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: A Mailcow egyetlen aktiválási futtatás során létrehozható mailboxok maximális száma. Az alapértelmezett érték `1000`. Csökkentse az értékét, hogy megakadályozza a véletlen tömeges létrehozást, vagy növelje a nagyobb példányok esetén. Ha a limite vượtılır, nem készülnek létre fiókok. Részletekért lásd a [Mailcow Instances](Mailcow.md) oldalt.
- A Mailcow integráció emellett megköveteli a `Security__CredentialEncryptionKey` használatát (lásd a [Security Settings](#-security-settings) részt). Egy példány csatlakoztatása és a mailboxok aktiválása is elutasításra kerül, amíg a hitelesítő adatok titkosítása ki van kapcsolva, így az API key és a generált alkalmazási jelszavak soha nem tárolhatók szöveges formátumban.
- Kibocsátási hozzáférés szükséges a Mailcow HTTPS API és az IMAP host felé TLS felett (általában a `993` porton).

### 📦 BatchOperation Beállítások {#batchoperation-settings}
- `BatchOperation__BatchSize`: Az e-mail műveletek batch mérete.
- `BatchOperation__PauseBetweenEmailsMs`: Az egyes e-mailek közötti szünet millisekundyban.
- `BatchOperation__PauseBetweenBatchesMs`: A batchok közötti szünet millisekundyban.

### 📤 Offload Beállítások {#offload-settings}
A [date-windowed offload](Offload.md) beállításai. Az alapértelmezett értékek visszaállítják az alkalmazás korábbi viselkedését, így az olyan telepítések sem érintettek, amelyek nem konfigurálják ezt a szakaszt.
- `Offload__MaxConcurrentJobs`: Hány visszaállítási vagy offload feladat futhat egyszerre. Az alapértelmezett érték `1`, ami megőrzi a feladatों sorosított feldolgozását, ahogyan azt a feladatlista eddig is alkalmazta. A megadott értéktől függetlenül maximum egy feladat futhat céllevan postaládánként.
- `Offload__PrefetchMaxMessages`: A felső határ arra, hogy hány üzenet indexelődik egy cél postaládából a duplikáció detektálásához. Az alapértelmezett érték `500000`. E felett a ellenőrzés egyetlen mappára szűkül le, és logolja, hogy a lefedettség csökkentve lett.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Forrásmappák, amelyek soha nem kerülnek offload alá, az átnevezés előtt kerülnek összevetésre, és tartalmazzák a mappalábrécҳои is. Alapértelmezés szerint üresek.
- `Offload__FolderRenameMap__<SourceFolder>`: A forrásmappa elérési út első részeit írja át, például `Offload__FolderRenameMap__Sent Items=Sent`. Alapértelmezés szerint üres.
- `Offload__MarkAsSeen`: A hozzáadott levelek jelölése olvashatónak. Az alapértelmezett érték `true`, ami megegyezik a meglévő visszaállítási viselkedéssel.

Mindkét mappa beállítás szándékosan üres: a mappák átírása vagy törlése kérés nélkül meglepetés lenne minden olyan felhasználónak, aki már a visszaállítási útvonalat használja. Ezért egy konfiguráció nélküli futtatás mindent migrál, a spam mappákat is beleértve, és a cél mappa saját mappái mellé létrehoz egy második speciális mappák készletet.

### 📊 Sávszélesség Nyomon követési Beállítások {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Az IMAP sebességkorlátozás kezeléséhez használt sávszélesség nyomon követésének engedélyezése vagy letiltása (true/false). Az alapértelmezett érték `false`. Ha engedélyezve van, a rendszer fiókonként követi a sávszélesség használatát, és szinkronizálást állít meg, amikor a szolgáltató korlátai elérhetők. Részletes információkért lásd a [Rate Limit Handling](RateLimitHandling.md) részt.
- `BandwidthTracking__DailyLimitMb`: Napi letöltési limit megabajtékban fiókonként. Az alapértelmezett érték `25000` (25 GB). A sávszélesség-korláttal rendelkező szolgáltatóknál állítsa ezt a sebességkorlátozásuknak megfelelően (például `2500` a ~2500 MB napi limitrel rendelkező szolgáltatóknál). A rendszer szinkronizálást állít meg, amikor ez a limit elérhető.
- `BandwidthTracking__WarningThresholdPercent`: A napi limit százalékos értéke, amelynél figyelmeztető üzenetek kerülnek a logokba. Az alapértelmezett érték `80`. Amikor a sávszélesség használata eléri ezt a százalékot, figyelmeztetések kerülnek a logokba, hogy segítsenek a közelítő limitek nyomon követésében.
- `BandwidthTracking__PauseHoursOnLimit`: Órák száma, amíg a szinkronizálás szünetel, ha a napi limit elérhető. Az alapértelmezett érték `24`. Ezen időszak után a limit jelző automatikusan törlődik, és a szinkronizálás folytatódik.
- `BandwidthTracking__TrackUploadBytes`: A feltöltési sávszélesség nyomon követésének is engedélyezése (true/false). Az alapértelmezett érték `false`. A legtöbb IMAP szolgáltató csak a letöltéseket korlátozza, így ez általában nem szükséges.

### 🎯 Kiválasztási beállítások {#selection-settings}
- `Selection__MaxSelectableEmails`: Az e-mailek maximális száma, amelyeket egyszerre lehet kiválasztani.

### 👁️ Nézet beállítások (Adatvédelem és Megjelenítés) {#view-settings-privacy-display}

- `View__DefaultToPlainText`: A megrendelélt alapértelmezett e-mail nézet módját szabályozza az adatvédelmi és nyomon követési megelőzés érdekében (true/false). Az alapértelmezett érték `false`.
  - Ha `true` értékre van állítva: Az e-mailek alapértelmezés szerint plain-text nézetben nyílnak meg, megakadályozva a nyomon követési pixel, külső képek és web beaconok automatikus betöltését. Ez az e-mail nyomon követéséről és az adatvédelemről concernsált felhasználóknak ajánlott.
  - Ha `false` értékre van állítva: Az e-mailek alapértelmezés szerint HTML nézetben nyílnak meg, megjelenítve a formázott e-mailt képekkel és stílusokkal.
  - A felhasználók az e-mail részleteinek oldalán található gombbal váltogathatnak az HTML és a plain-text nézetek között (csak akkor látható, ha mindkét formátum elérhető).
  - A "Full View" link figyeli a jelenleg kiválasztott nézet módot.

- `View__BlockExternalResources`: Megblokálja a külső erőforrásokat (távoli képek, külső CSS, külső szkriptek, webes betűtípusok stb.) az HTML e-mail nézetekben a nyomon követés megakadályozása és az adatvédelem javítása érdekében (true/false). Az alapértelmezett érték `false`.
  - Ha `true` értékre van állítva: A külső erőforrásokat kiszűrjük az HTML e-mailek megjelenítésekor. Csak az inline tartalmak és a data URI-k (beleértve a `cid:` hivatkozásokon keresztüli inline csatolmányokat) jelennek meg.
  - Ha `false` értékre van állítva: Az HTML e-mailek az összes eredeti külső erőforrásukkal együtt jelennek meg.
  - **Fontos**: Ez a beállítás csak az e-mail **megjelenítésére** hatással van. Az archivált e-maileket változatlanul tároljuk az adatbázisban, az összes eredeti tartalom megőrzésével.
  - A blokkolt erőforrások közé tartozik:
    - Távoli képek (nyomon követési pixel, szervereken tárolt külső képek)
    - Külső CSS stíluslapok
    - Külső betűtípusok @font-face hivatkozáson keresztül
    - Külső CSS importok @import hivatkozáson keresztül
    - Külső háttérképek
  - Engedélyezett erőforrások:
    - Inline képek, amelyek data: URI-ként vannak beágyazva
    - cid: URI-kon keresztül hivatkozott inline csatolmányok
    - Inline CSS stílusok és style tagek
  - Ez a beállítás függetlenül működik a `DefaultToPlainText` beállítástól, és további adatvédelmi réteget biztosít az HTML e-mailek megtekintésekor.

### 📊 Dashboard Settings {#dashboard-settings}

- `Dashboard__ShowDirectionSplits`: Arról, hogy a számláló kártyák tartalmazzák-e a bejövő és kimenő részeket, illetve a fiók kártya tartalmazza-e a különböző domainok számát. Az alapértelmezett érték `true`. Ha `false` értékre állítjuk, ezek a számok nem számolódnak ki, hanem egyszerűen nem jelennek meg: a számlálók egyszerű számot mutatnak, és a csatolmányok számlálása abbahagyja a levélhez való csatolódást, ami az egyetlen lekérdezés, amit ezek a részek tesznek}`}>
- `Dashboard__SelectablePeriods`: Arról, hogy a dashboard diagramok kínálnak-e fel feloldási módot (resolution), egy időszakot és nyilasokat a mozgatáshoz. Az alapértelmezett érték `true`. Ha `false` értékre állítjuk, a diagramok az utolsó tizenkét hónapot mutatják havi bontásban és az egész archívum küldőit; a diagram végpont (chart endpoint) olyan választ ad, mintha nem is létezne, és a leghatárértékű küldési dátumot nem keresi fel.
- `Dashboard__CacheSeconds`: Mennyi ideig tárolja a kiszámolt dashboard statisztikákat (összesített számok a bejövő és kimenő bontással együtt, fiókonkénti számok, a diagram sorozatok, top küldők, legutóbbi e-mailek, adatbázis mérete) a szerver belső memóriájában tárolt gyorsítértékében (in-memory cache). Az alapértelmezett érték `60` másodperc. Ha `0` értékre állítjuk, a gyorsítértékelés kikapcsolódik, és a statisztikák mindig újra számolódnak. Minden egyes feloldási mód és időszak diagramválasztás saját külön gyorsítértékben kerül tárolásra, így egy olyan dashboard, amely váltogat a választások között, minden egyes választáshoz egyetlen bejegyzést tárol. A magasabb értékek csökkentik az adatbázis terhelését nagy környezetekben, de cserébe aktualitástalanabb számokat adhatnak. A szinkronizálási állapot jelzések, a tárolási értékek és a legutóbbi e-mailek listája mindig élőben kerülnek lekérdezésre, és nem érinti őket ez a gyorsítérték.
- `Dashboard__RefreshIntervalMinutes`: Mennyire gyakran számolja újra a `DashboardStatsRefreshService` háttérszolgáltatás a költséges dashboard aggregációkat (összesített számok a bejövő és kimenő bontással együtt, fiókonkénti számok, a fiók domainjeinek száma, az alapértelmezett diagram sorozat a legnépszerűbb küldőkkel együtt, adatbázis mérete) a `DashboardStatsCache` adatbázis táblába. Több millió e-mailet tartalmazó telepítéseknél a dashboard ezeket a készített értékeket olvassa ki a teljes archívum aggregálása helyett a lekérdezési útvonalon, ami a ~10 másodperces első betöltést millisekundyá (\< 1 másodperc) gyorsítja. A diagram végpont a megadott alapértelmezett választást (egy év összes hónapja, bejövő küldők, az aktuális időszak) ugyanabból az egyes sorból válaszolja meg, így visszatéréskor az első megjelenítés száma kerül megjelenésre; minden egyéb választás a lekérdezéskor számolódik ki, és az előbb említett belső memóriában tárolt gyorsítértékben marad meg. Az alapértelmezett érték `15` perc. Ha `0` értékre állítjuk, a háttérfrissítés kikapcsolódik; ilyenkor a dashboard újra élőben számolja ki a statisztikákat (az előbb említett belső memóriában tárolt gyorsítérték továbbra is alkalmazódik). A megjelenített értékek akár a konfigurált intervallummal el is maradhatnak a tényleges archív állapot mögött. Egy olyan sor, amelyet más `ShowDirectionSplits` vagy `SelectablePeriods` értékekkel számoltak ki, mint az aktuálisak, nem kerül felhasználásra; a dashboard addig számol élőben, amíg a következő frissítés nem történik meg.

### 🔄 Munka folyamat beállításai {#jobs-settings}
- `Jobs__RefreshSeconds`: Mennyire gyakran tölti újra magát a háttérfolyamatok oldala, amíg egy böngésző fül nyitva van rajta. Az alapértelmezett `30` másodperc. Állítsa `0` értékre az automatikus újratöltés kikapcsolásához, hogy kézzel frissíthesseni. Minden újratöltés az összes egyes támféle munkaforrásból építi újra az oldalt, ezért sok fiókot tartalmazó telepítéseken a hosszabb intervallum csökkenti a terhelést, amelyet minden megnyitott fülben meg kell szorozni.

### 🗃️ Npgsql Beállítások {#npgsql-settings}
- `Npgsql__CommandTimeout`: Az adatbázis-parancsok timeout értéke másodpercekben.

### 📥 Feltöltési beállítások {#upload-settings}
- `Upload__MaxFileSizeGB`: A feltöltések maximális fájlmérete GB-ban.
- `Upload__KeepAliveTimeoutHours`: A feltöltések élettartamának timeout értéke órában.
- `Upload__RequestHeadersTimeoutHours`: A kérések fejléceinek (request headers) timeout értéke órában.

### 📂 Helyi importálási beállítások {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, stb.: A helyi könyvtárak fehérlistája, amelyeket a CLI importálási parancsnak (`--import-mbox`, `--import-eml`) engedélyezett beolvasni. Minden egyes bejegyzés egy elérési út a konténeren belül. A beimportálni kívánt fájlokat az egyik ilyen könyvtárba kell csatolnia (mount) a Docker volume-ök segítségével. Alapértelmezett: `/data/import` (automatikusan `/app/uploads` értékre állítva, ha nincs megadva).
  - Amikor a `docker exec` eszközt használja az importálási parancs futtatásához, a `--file` paraméteren keresztül megadott fájl elérési útának az egyik engedélyezett útvonal része kell, hogy legyen.
  - Ez a biztonsági intézkedés megakadályozza a CLI parancsok által történő tetszőleges fájlrendszer-Hozzáférés lehetőséget.
  - Különféle import forrásokhoz több útvonal is konfigurálható.
  - Részletes használati utasításért tekintse meg a [CLI parancsokat](CLI.md).

### 📄 CSV Import Beállítások {#csv-import-settings}
- `CsvImport__MaxRows`: Egyetlen tömeges import során feldolgozott CSV sorok (postafiókok) maximális száma. Az alapértelmezett érték `5000`. Növelje ezt az értéket nagy implementációk esetén; csökkentse a hatást a single import futtatásának az adatbázterhelésre nézve.
- `CsvImport__MaxFileSizeBytes`: A feltöltött CSV fájl maximális engedélyezett mérete (байtban). Az alapértelmezett érték `10000000` (10 MB). Szükség esetén állítsa ezt az értékét az upload limiteknek megfelelően.
- A részletes használati útmutatóért tekintse meg a [Account Import Guide](Account-Import.md) dokumentációt a CSV útján végzett tömeges IMAP fiók importálásához.

### 📤 Audit Export Beállítások {#audit-export-settings}
A felauditálási adatok exportálási oldalának beállításai (csak admin számára, a Logs oldalról elérhető). Az export egy ZIP csomagot generál a meglévő archívumból táblázatos tömegadatokkal (INDEX.XML + CSV táblák + DTD) külső auditálási eszközök számára. A használati részletekért lásd az [Audit Data Export Guide](AuditExport.md) dokumentációt.
- `AuditExport__DataSupplierName`: Az "Data supplier name" űrlapmez défautértéke, amely az exportált index fájlban azonosítja az Ön szervezetét. Alapértelmezett: üres.
- `AuditExport__DataSupplierLocation`: Az "Data supplier location" űrlapmez défautértéke (pl. céghelyszín). Alapértelmezett: üres.
- `AuditExport__Comment`: A "Comment" űrlapmez défautértéke, amely szabad szöveges formátumban szerepel az exportált index fájlban. Alapértelmezett: üres.
- `AuditExport__OutputDirectory`: A generált ZIP fájlok tárolására szolgáló könyvtár. A relatív útvonalak az alkalmazás tartalom gyökérpontjához (app content root) viszonyítva kerülnek ki_'rés_'űltetés_'re_' (resolved). Alapértelmezett: `exports/audit`.
- `AuditExport__RetentionDays`: A napok száma, amely után a befejezett export fájlokat a napi tisztító folyamat törli. Alapértelmezett: `30`.
- `AuditExport__MaxRangeYears`: A megengedett maximális időtartam az export időszak kezdő és vége között. Alapértelmezett: `10`.
- Az űrlapmezeket ezek a alapértelmezett értékek töltik meg előre, de az exportonként szerte módosíthatók.
- Minden export egy kezdési és egy eredmény bejegyzést ír az hozzáférés naplóba (típus: "Audit Data Export"), így a történet módosításbiztos (revision-safe) bármilyen DB schéma módosítás nélkül.

### 🔒 Törlési szabályzat beállításai {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Ez szabályozza, hogy az archivált e-mailek manuális törlése engedélyezett-e (true/false). Az alapértő érték `true`. Ha `false` értékre van állítva:
  - Minden archivált e-mail le van zárva (`IsLocked = true`) az indításkor az adatbázis-megfelelőségi trigger (database compliance trigger) segítségével, megakadályozva a módosítást vagy törlést az adatbázis szintjén.
  - A manuális törlés (egyedi és tömeges) az alkalmazás szintjén blokkolva van egy hibavisszajelzésrel.
  - Az oszlop alapértelmezett értékét úgy állítják át, hogy az úján importált e-mailek is le legyenek zárva.
  - A jelenlegi szabályzat állapota minden indításkor a megjegyzhetőség érdekében a megjegyzésbe (AccessLogs table) kerül feljegyzésre (a Logs oldalon "Deletion Policy" bejegyzésekként látható).
- `DeletionPolicy__MaxBulkDeleteEmails`: A felső határ egy szűrőalapú tömeges törléshez (*Delete all matches*). Az alapértő érték `100000`. Ha egy szűrő több e-maillel is egyezik, a rendszer elutasítja azt egy üzenettel, amely kéri a szűrő szűkítését; a folyamat soha nem vágja le csendben a találatokat. A tárolási idő politikáján (retention policy) kereszt be zárt e-maileket kihagyja és a folyamat állapotsávában jelzi őket, nem pedig feloldja a zártságukat. A folyamat újra ellenőrzi ezt a limiteket és a `DeletionPolicy__DeletionAllowed` értékét indításkor, így a szabályzatváltozás előtt sorba került feladat leáll, ahelyett hogy a régi beállítással futtatná le.
- **Változatlanság védelem (Immutability protection):** Amikor `IsLocked = true`, az adatbázis-megfelelőségi trigger (`prevent_locked_email_changes`) megakadályoz ANY módosítást az e-mail soron — minden oszlop védve van, nem csak egy fix mezőlista. Az egyetlen kivételt képező oszlopok magát a `IsLocked` (hogy a tárolási időhöz kapcsolódó törléshez és az indítási szabályzat alkalmazásához szükséges feloldás lehetséges maradjon), valamint a `FolderName` (hogy az IMAP szinkronizáció frissíthesse a mappát, ha egy e-mailt a szerver oldalon mozgatnak). A védelem oszlopfüggetlen (JSONB-alapú összehasonlítás), és automatikusan lefedi a jövőbeni sémakiterjesztéseket is.

### 🕐 Időzóna beállítások {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Az időzóna, amely az e-mail időbélyeggének megjelenítéséhez használható a UI-ban. IANA időzóna azonosítókat használ (pl. "Europe/Berlin", "Asia/Tokyo"). Az alapértelmezett érték az "Etc/UCT" a háttatartozó kompatibilitás érdekében. E-mailek importálásakor az időbélyegeket erre az időzónára konvertáljuk a megjelenítés céljából.

### 🎉 ReleaseNotes Settings (Verziófrissítési Splash Screen) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: A verzió karakterlánc, amelyet ez az példány jelent meg. Ezt a GitHub release tagekkel hasonlítják össze, hogy eldönthessék, létezik-e újabb kiadás. Ha üres, a changelog a fordított assembly verzióra tér vissza, és a frissítés jelzése (update hint) kikapcsolt marad.
- `ReleaseNotes__Enabled`: A verziófrissítési splash screen engedélyezése vagy letiltása (true/false). Az alapértelmezett `true`. Ha engedélyezve van, a rendszergazdák egy egyszeri changelog modált fognak látni az alkalmazás frissítése után, amely a GitHub Releases oldalról lekelt verziójegyzéket mutatja az aktuális verzióhoz. Minden rendszergazda el tudja tüntetni a modalt, és ez csak egy újabb verzió esetén jelenik meg újra. Állítsa `false` értékre a funkció teljes kikapcsolásához, beleértve a frissítés jelzését is.
- `ReleaseNotes__UpdateHintEnabled`: Mutasson-e egy kis toast értesítést a rendszergazdáknak a jobb alsó sarokban, ha a GitHub újabb stabil kiadást tesz közz, mint amelyik verzió telepítve van (true/false). Az alapértelmezett `true`. A toast linkel a megfelelő kiadáshoz, és a böngészőben verzióonként egyszer tekinthető elavultnak. A tervezetek (drafts) és előkiadások (prereleases) figyelmen kívül maradnak. Requires `ReleaseNotes__AppVersion`; nélküle semmilyen jelzés nem jelenik meg.
- `ReleaseNotes__UpdateCheckIntervalHours`: Hányszor frissüljen a GitHub kiadási lista, órában megadva. Az alapértelmezett `6`. Egy rövidebb intervallum lehetővé teszi, hogy egy folyamatosan futó példányban a újabb kiadás hamarabb láthatóvá váljon, egy hosszabb pedig csökkenti a GitHub API hívások számát. A minimum `1`.
- `ReleaseNotes__RequestTimeoutSeconds`: Egyetlen GitHub API kérés timeout értéke, másodpercekben. Az alapértelmezett `10`. Ha lejár az idő, a changelog és a frissítés jelzése az adott kísérlet esetében kikapcsolt marad, ahelyett hogy nyitva tartanák a kérést; a következő ellenőrzés megkísérli újra.

### 🔧 Adatbázis karbantartási beállítások {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Az automatikus napi adatbázis karbantartás engedélyezése vagy kikapcsolása (true/false). Az alapértelmezett `false`. Ha engedélyezve van, a rendszer automatikusan futtat VACUUM ANALYZE műveleteket az adatbázis teljesítményének optimalizálása és a felesleges adatok (bloat) megelőzése érdekében. További részletekért lásd a [Database Maintenance Guide](DatabaseMaintenance.md) dokumentációt.
- `DatabaseMaintenance__DailyExecutionTime`: Az az időpont a nap folyamán, amikor az adatbázis karbantartásnak futnia kell, 24 órás formátumban (HH:mm). Az alapértelmezett `02:00`. Egy olyan időpontot válasszon, amikor a rendszer aktivitása alacsony.
- `DatabaseMaintenance__TimeoutMinutes`: A karbantartási műveletekhez megengedett maximális idő perces értékben. Az alapértelmezett `30`. Növelje ezt az értéket nagyobb adatbázusok esetén.

### 🧬 Melléklet de Duplikáció Beállításai {#attachment-deduplication-settings}
A melléklet de duplikáció minden egyes egyedi melléklet-payloadot csak egyszer tárol (tartalom alapú azonosítás SHA-256 segítségével), és ez egy **alapvető funkció, amely mindig engedélyezve van** – szándékosan nincs be/ki kapcsoló. Kizárólag az alábbi tömeges/ütemezési paraméterek állíthatók be. A teljes részletekért lásd a [Melléklet De Duplikáció Útmutatót](AttachmentDeduplication.md).

- `AttachmentDeduplication__BatchSize`: Az egyszeri háttér migráció során egy tranzakciónként migrált meglévő mellékletek száma a korábbi adatok esetén. Az alapértelmezett érték `200`. A nagyobb értékek gyorsabban migráznak, de több memóriát/adatbázis-terhelést igényelnek batchelésごとに.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Opcionális szünet (milliszekundokban) a migrációs batch-ek között a busy rendszereken tapasztalható adatbázis-terhelés korlátozására. Az alapértelmezett érték `0` (nincs szünet).
- `AttachmentDeduplication__StartupDelaySeconds`: Késleltetés (másodpercekben) az alkalmazás indítása után, mielőtt a háttér migráció megkezdődne, hogy a schéma migrációnak legyen ideje befejeződni. Az alapértelmezett érték `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Az állandóan futó „garbage collection” intervalluma (órákban), amely eltávolítja azokat a melléklet-payloadokat, amelyekre már egyetlen e-mail sem hivatkozik. Az alapértelmezett érték `12`. Ez `DatabaseMaintenance__Enabled` értékétől függetlenül fut.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Adatbázis parancs timeout (másodpercekben) a migrációs batch műveletekhez (INSERT SHA-256 hash-sel és UPDATE). Az alapértelmezett érték `300` (5 perc). Növelje ezt az értéket nagyon nagy adatbázusok vagy mellékletek esetén, vagy csökkentse, ha gyorsabb hibaérzékelést szeretne. Ha egy batch még mindig timeout-ot kap, a szolgáltatás automatikusan megpróbálkozik a batch méretének felével.

### 💾 Fiók tárolási beállítások {#account-storage-settings}

A fiók alanyi tárolási megjelenítése megmutatja az adatbázis tárolási felhasználását (összes e-mail mez + csatolások) minden egyes fiók esetében a Dashboard "Account Overview" táblázatban és a MailAccounts "Show All" táblázatban. Egy autark háttérszolgáltatás (`AccountStorageRefreshService` kiszámítja az értékeket az egyes oszlopok PostgreSQL `pg_column_size` összeadása詈al (minden e-mail mez lefedésével anélkül, hogy az e-mail testét beolvasná), majd elmenti őket a `AccountStorageCache` táblázatba. Ez a szolgáltatás függetlenül fut a `DatabaseMaintenance__Enabled`-tól.

- `AccountStorage__Enabled`: A tárolási frissítés szolgáltatásos aktiválása vagy deaktiválása (true/false). Az alapértelmezett érték `true`. Ha engedélyezve van, a szolgáltatás a folyamat megindulásakor felfalható backfill eljárással végzi a függőben lévő fiókok feltöltését (hibatűrő a `AccountStorageBackfillState` révén), ezt követően pedig napi teljes frissítést végez. Ha deaktiválva van, a tárolási értékek csak akkor frissülnek, ha e-mailek szinkronizálódnak, importálódnak vagy törlődnek, de a megjelenített értékek az idővel a tócska lehetnek.
- `AccountStorage__DailyExecutionTime`: A nap folyamán megadott idő (24 órás formátum `HH:mm`) az összes fiók napi teljes frissítéséhez. Az alapértelmezett érték `02:30`. Egy olyan időpontot válasszon, amikor alacsony a rendszer aktivitása.
- `AccountStorage__BackfillDelayMs`: Késleltetés (milliszékonként) a fiókok között a folyamat megindulásakor végzett kezdeti backfill során. Az alapértelmezett érték `5000`. Az alacsonyabb értékek felgyorsítják a backfill folyamatot, de növelik az adatbázis terhelését; nagyon nagy archivák esetén emelje ezt az értéket az adatbázis túlterhelésének elkerülése érdekében.
- `AccountStorage__RefreshBatchDelayMs`: Késleltetés (milliszékonként) a fiókok között a napi teljes frissítés során. Az alapértelmezett érték `1000`. Az alacsonyabb értékek felgyorsítják a frissítést, de növelik az adatbázis terhelését; nagyon nagy archivák esetén emelje ezt az értéket.
- `AccountStorage__CommandTimeoutSeconds`: Adatbázis parancs timeout (másodpercekben) a fiók alanyi tárolási számításához (`pg_column_size` egy fiók összes során). Az alapértelmezett érték `300` (5 perc). A számítás minden e-mail sort beolvas, beleértve a TOAST adatokat is, ami nagyon nagy archiváknál (250k+ e-mail) jóval egy percnél töled is eltarthat; a korábbi 30 másodperces Npgsql alapértelmezett értéke miatt a frissítés csendben meghiúsult, és a dashboard régi értékeket mutatott. Emelje ezt az értéket, ha a számítás továbbra is időtúllépés (timeout) hibát idéz a saját archivájánál.

> 💡 **Megjegyzés**: A tárolási értékek azonnal frissülnek minden e-mail szinkronizálás, import vagy megőrzési törlés után, így a megjelenített értékek a napi frissítés nélkül is naprakészek maradnak. A napi frissítés egy biztonsági háló, amely az extrém eseteket (pl. közvetlen adatbázis módosítások) kezeli.

### 📝 Naplózási beállítások {#logging-settings}
- `Logging__LogLevel__Default`: Az alkalmazás alapértelmezett naplózási szintje. A rendelkezésre álló szintek: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Az alapértelmezett érték: `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: A naplózási szint az ASP.NET Core framework üzenetekhez. Alapértelmezett: `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: A naplózási szint az Entity Framework adatbázis parancsokhoz. Alapértelmezett: `Warning`.

### 🛡️ Biztonsági beállítások {#security-settings}
- `AllowedHosts`: Pontáltal különített listája azoknak a host nevének, amelyeket az alkalmazásnak szolgáltatni szabad. Ez segít megelőzni az HTTP Host header támadásokat. Példa: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Fontos**: Ne használja a `*`-t termelési környezetben, mivel az kikapcsolja a host header validációt.

#### 🔐 Hitelesítő adatok tárolás alatti titkosítása {#credential-encryption-at-rest}
A levelezési fiók hitelesítő adatai (IMAP jelszó, M365 client secret és MSA OAuth tokenek) titkosíthatók az adatbázisban. A titkosítási kulcsot **kívül** tárolják az adatbázis mellett (környezeti változó / Docker secret), így az adatbázis mentése önmagában nem elegendő a hitelesítő adatok olvasásához.

- `Security__CredentialEncryptionKey`: Base64 kódolt 256 bites (32 byte) kulcs-titkosítási kulcs. Generáljon egyet a következővel:
  ```bash
  openssl rand -base64 32
  ```
  A değeri beállítása környezeti változóként történik (ajánlott: `Security__CredentialEncryptionKey` a compose fájlban vagy a konténer környezetében). Az alkalmazás nem támogatja a `_FILE` stílusú Docker secreteket — ha Docker secretet használ, futtassa be (mount) azt, és töltse be a tartalmát a környezeti változóba az entrypointnél. Soha ne committeleyin kulcsot a `appsettings.json`-hoz. Ha egy kulcs konfigurálva van, a hitelesítő adatokat íráskor titkosítják, és egy háttérszolgáltatás egyszer le titkosítja a meglévő plain text hitelesítő adatokat.
- `Security__EncryptAccountCredentials`: Az új/frissített hitelesítő adatértékek titkosításának engedélyezése vagy tiltása (true/false). Az alapértelmezett érték a `true`. Ha nincs konfigurálva kulcs, a hitelesítő adatok plain text formátumban kerülnek tárolásra, és a folyamat indításakor figyelmeztetés kerül a logokba — a meglévő telepítések nem érintettek, amíg kulcs nem érkezik meg.
- `Security__CredentialEncryptionBackfill`: Arról, hogy a háttérszolgáltatás a folyamat indításakor le kell-e titkosítania a meglévő plain text hitelesítő adatokat (true/false). Az alapértelmezett érték a `true`. Nincs hatása, ha nincs konfigurálva kulcs.
- `Security__CredentialEncryptionKeyPrevious`: Opcionális korábbi kulcs, amely csak az azzal titkosított értékek visszaoldásához használható (kulcscsere esetén). Az új értékeket mindig a `Security__CredentialEncryptionKey` kulccal titkosítják.

  > ⚠️ **Kulcscsere**: a csere után tartsa konfigurálva a `Security__CredentialEncryptionKeyPrevious`-at **amíg minden hitelesítő adat újrán nem íródódtik fel** (például a fiók szerkesztésével vagy egy automatikus OAuth token frissítéssel) — azంతో titkosított meglévő értékek *nem* kerülnek automatikusan az új kulccsal titkosítás alá. Amint eltávolítja a korábbi kulcsot, minden olyan hitelesítő adat, amely még azzal van titkosítva, olvashatatlan lesz, és újra kell adni. Amíg konfigurálva van, olyan biztonságosan kell kezelni, mint a jelenlegi kulcsot.

> ⚠️ **Tartsa biztonságban a kulcsot**: A kulcs nélkül a titkosított hitelesítő adatok nem állíthatók vissza, és újra kell őket adni. Mentse el a kulcsot az adatbázis mentéseivel együtt (de külön is) együtt.
>
> 💡 **Meglévő telepítések**: Frissítsen biztonságosan — amíg nincs beállítva `Security__CredentialEncryptionKey`, semmi sem változik. A kulcsot akkor állítsa be, amikor készen áll; a meglévő plain text hitelesítő adatok olvashatóak maradnak, és a háttérben titkosítás alá kerülnek.

### 🔐 OIDC Konfiguráció {#oidc-configuration}

Az OpenID Connect hitelesítés részletes beállítási útmutatójához tekintse meg az [OIDC Implementation Guide](OIDC_Implementation.md) dokumentációt.

#### Alapvető OIDC Beállítások {#basic-oidc-settings}
- `OAuth__Enabled`: Az OIDC hitelesítés engedélyezése vagy tiltása (true/false)
- `OAuth__Authority`: Az OpenID Connect authority URL (pl. https://sts.windows.net/{TENANT-ID}/ az Azure AD esetén)
- `OAuth__ClientId`: Az identitásszolgáltató által kiosztott client ID
- `OAuth__ClientSecret`: Az identitásszolgáltató által kiosztott client secret
- `OAuth__DisplayName`: Opcionális megjelenítendő név az OIDC bejelentkezési gombon és az automatikus átirányítási oldalon (pl. `PocketID SSO`). Ha elhagyja, a generikus "Login with OAuth" felirat kerül használatra.
- `OAuth__ClientScopes__0`: Az első kért scope az identitásszolgáltatótól (openid)
- `OAuth__ClientScopes__1`: A második kért scope az identitásszolgáltatótól (profile)
- `OAuth__ClientScopes__2`: A harmadik kért scope az identitásszolgáltatótól (email)
- `OAuth__ResponseMode`: Az autorizációs kérés opcionális response mode-ja (`query`, `fragment` vagy `form_post`). Az alapértelmezett az `form_post`, az ASP.NET Core alapértelmezett értéke. Állítsa `query` értékre olyan szolgáltatók esetén, amelyek nem támogatják a `form_post`-ot, mint például a Synology SSO Server. Lásd: [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Az új OIDC felhasználók automatikus jóváhagyása manuális admin jóváhagyás nélkül (true/false). Az alapértelmezett az `false`.

#### Felhasználói Provisioning Beállítások {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Az új OIDC felhasználók automatikus jóváhagyása manuális admin jóváhagyás nélkül (true/false). Az alapértelmezett az `false`. Ha engedélyezve van, az OIDC szolgáltatón keresztül hitelesített felhasználók azonnal aktiválódnak és hozzáférhetnek az alkalmazáshoz. Ha letiltva van (alapértelmezett), az új OIDC felhasználók inaktív állapotban kerülnek létre, és adminisztrátori manuális aktiválást igényelnek. Részletes információkért lásd: [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users).
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, stb.: E-mail címek, amelyek automatikusan adminisztrátorként kerülnek létre (provisioning). Az ezekkel az e-mail címekkel rendelkező felhasználók az első OAuth bejelentkezés során aktív adminisztrátorként kerülnek létre, megkerülve a normál jóváhagyási folyamatot. Az e-mail címek összehasonlítása nem érzékeny a kis- és nagybetűzésre.

#### Jelszó nélküli Bejelentkezési Beállítások {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: A felhasználónév/jelszó bejelentkezés letiltása (true/false). Az alapértelmezett az `false`. Ha engedélyezve van, a jelszó űrlap el van rejtve, **és** a szerver minden jelszó POST kérést elutasít, így csak az OAuth bejelentkezés áll rendelkezésre. `OAuth__Enabled=true` szükséges hozzá. Ha az OAuth le van tiltva, ez a beállítás nem hatással bír.
- `OAuth__AutoRedirect`: A felhasználók automatikus átirányítása az OAuth szolgáltatóhoz (true/false). Az alapértelmezett az `false`. A `OAuth__DisablePasswordLogin` értékének `true` állapotに kell lennie a `OAuth__DisablePasswordLogin` használatához. A felhasználók egy rövid betöltési képernyőt fognak látni, mielőtt átirányítanak.

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

Bár az alkalmazáshoz HTTPS-sel ellátott reverse proxy keresztül kell valódi hozzáférést biztosítani, a Kestrel web szervert úgy is konfigurálhatja, hogy SSL/TLS tanúsítványokat használjon. Ez végrehajtott titkosítást biztosít a reverse proxy és az alkalmazás konténer között.

### Konfigurációs lépések {#configuration-steps}

1. **Generáljon vagy szerezzen egy SSL tanúsítványt** PFX formátumban (pl. `localhost.pfx`)

2. **Adja meg a következő környezeti változókat** a `docker-compose.yml` fájlhoz a `mailarchive-app` szolgáltatáshoz:

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

5. **Helyezze el a tanúsítvány fájlát** (pl. `localhost.pfx`) a gazdán futó rendszer `./certs` könyvtárában.

### Környezeti változók magyarázata {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: HTTP végpont URL (alapértelmezett: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: HTTPS végpont URL (alapértelmezett: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: A PFX tanúsítvány fájlja elérési útja a konténeren belül
- `Kestrel__Endpoints__Https__Certificate__Password`: Jelszó a PFX tanúsítvány fájljához

> 💡 **Megjegyzés**: Ez a konfiguráció opcionális. Ha HTTPS-sel ellátott reverse proxy-t használ (ajánlott), a reverse proxy és az alkalmazás közötti kommunikáció maradhat HTTP. Azonban érzékeny környezetben a maximális biztonság érdekében érdemes lehet a Kestrelen is engedélyezni a HTTPS-t, hogy az egész kommunikációs útvonalat titkosítsuk.

## 🔑 Titkos szavak kezelése a termeléshez {#secrets-management-for-production}

A szenet sensitive adatok, például az adatbázis jelszavak, admin hitelesítő adatok és OAuth client secret-ek közvetlen hardkodálása a `docker-compose.yml` fájlba biztonsági kockázatot jelent, különösen, ha a fájlt a verziókezelőbe (version control) beküldik. A javasolt megközelítés ezek az értékek externalizálása egy `.env` fájlba, amelyet a Docker Compose automatikusan betölt.

### 📄 A `.env` fájl {#the-env-file}

Hozzon létre egy `.env` fájlt a `docker-compose.yml` fájllal azonos könyvtárban a következő tartalommal:

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

**Fontos**: A Docker Compose automatikusan olvassa a `.env` fájlt ugyanabból a könyvtárból — nem kell manuálisan hivatkozni rá. A `.env` fájlban definiált összes változó elérhető a `docker-compose.yml` környezetben a `${VARIABLE_NAME}` szintaxis segítségével.

### 🐳 Adaptált `docker-compose.yml` {#adapted-docker-composeyml}

A telepítési lépésekben szereplő `docker-compose.yml`-at úgy kell adaptálni, hogy a hardkodált titkos szavak helyett helyőrzőket (placeholders) használjon:

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

### 📋 `.env.example` sablon adminisztrátorok számára {#envexample-template-for-administrators}

Hozzon létre egy `.env.example` fájlt a repository-ban, amely sablonként szolgál más adminisztrátorok számára. Ez a fájl helyőrző értékeket és dokumentációt tartalmaz, de **nem tartalmaz valódi titkos szavakat**:

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
- **Használjon erős jelszavakat** — Generáljon hosszú, véletlenszerű jelszavakat (legalább 16 karakterrel), betűkkel, számokkal és speciális karakterekkel kevert formában.
- **Korlátozza a `.env` fájlhoz való hozzáférést** — Csak a Docker Compose-t futtató felhasználónak kell legyen olvasási jogosultsága a `.env` fájlhoz.

## 🔒 Biztonsági megjegyzések {#security-notes}

- Használjon erős jelszavakat, és változtassa meg az alapértelmezett hitelesítő adatokat. A jelszavak legalább 12 karakter hosszúak powin быть, és tartalmazniuk kell nagybetűket, kisbetűket, számokat és speciális karaktereket. Kerülje a gyakori szavak vagy könnyen megக்erülhető információk használatát.
- Üretelmi környezetben fontoljon fel HTTPS implementálását reverse proxy segítségével.
- A PostgreSQL adatbázis rendszeres mentése javasolt. A részletes mentési és visszaállítási folyamatokért lászd a [Backup and Restore Guide](BackupRestore.md) dokumentációt.
