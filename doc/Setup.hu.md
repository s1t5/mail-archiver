# 🛠️ Mail Archiver beállítási útmutató {#mail-archiver-setup-guide}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Ez az útmutató részletes utasításokat nyújt a Mail Archiver alkalmazás Docker Compose használatával történő beállításához.

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

3. Szerkesztse át az adatbázis konfigurációját a `docker-compose.yml` fájlban, és állítson be biztonságos jelszót a `POSTGRES_PASSWORD` változóban és a `ConnectionString` területen.

4. Ha hitelesítést szeretne használni (ami erősen ajánlott), határozzon meg egy `Authentication__Username` és `Authentication__Password` értéket, amelyet a rendszergazda felhasználó használ.

5. A `TimeZone__DisplayTimeZoneId` környezeti változót állítsa be a preferred időzónájának megfelelően (alapértelmezetten az "Etc/UCT"). Bármilyen IANA időzóna azonosítót használhat (pl. "Europe/Berlin", "Asia/Tokyo").

6. Opcionálisan konfigurálhatja a `Logging__LogLevel` környezeti változókat az alkalmazási naplók részletességének szabályozására. Tekintse meg az alábbi Logging Settings részt az elérhető lehetőségekért.

7. Konfiguráljon egy a választásának megfelelő reverse proxy-t https és hitelesítés használatával az alkalmazáshoz való hozzáférés biztonságosításához.

> ⚠️ **Figyelem**: Maga az alkalmazás nem biztosít titkosított hozzáférést https keresztül! Ezt egy reverse proxy segítségével kell beállítani!

8. A konténerek első indítása:
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
   - Ha szeretné, hozzon létre más felhasználókat, és rendeljen hozzájuk fiókokat.

## 📚 Környezeti változók magyarázata {#environment-variable-explanations}

### 🗄️ Adatbázis-kapcsolat {#database-connection}
- `ConnectionStrings__DefaultConnection`: A PostgreSQL adatbázishoz való csatlakozási karakterlánc. A `Host`, `Database`, `Username`, és `Password` értékeket szükség szerint módosítsa.

### 🔐 Hitelesítési beállítások {#authentication-settings}
- `Authentication__Username`: A rendszergazda fiók felhasználóneve.
- `Authentication__Password`: A rendszergazda fiók jelszava.
- `Authentication__SessionTimeoutMinutes`: A munkamenet lejárati ideje perces egységben. Ez a nem tartós (non-persistent) bejelentkezési cookie-re vonatkozik; azok a felhasználók, akik a bejelentkezési űrlapon bepipálják az **Remember Me** lehetőséget, helyette egy fix 7 napig érvényes, tartós cookie-t kapnak. See [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: A hitelesítési cookie neve.
- `Authentication__CookieSameSite`: A hitelesítési, munkamenet (session) és CSRF védelem cookie-k SameSite attribútumának konfigurálása. Érvényes értékek:
  - `Strict` (alapértelmezett): Maximális biztonság. A cookie-k csak azonos oldalról érkező (same-site) kérésekkel küldődnek el. Ez problémákat okozhat, ha külső linkekről navigálnak az alkalmazásba (pl. egy másik weboldalról kattintanak egy linkre), mivel a meglévő munkamenet nem lesz felismerhető.
  - `Lax`: Javasolt reverse proxy használata esetén. A cookie-k a legfelső szintű navigációk (top-level navigations) és az azonos oldalról érkező kérések során is elküldődnek, lehetővé téve a felhasználók számára, hogy külső linkeken keresztül is elérjék az alkalmazást, miközben megmarad a CSRF védelem a POST kéréseknél.
  - `None`: A cookie-k minden kérés során elküldődnek. HTTPS-t és a `Secure` attribútumot igényel. Csak akkor használja, ha specifikus webhelyen keresztüli (cross-site) követelményei vannak, és értékeli a biztonsági következményeket.

### 🔌 REST API Beállítások {#rest-api-settings}
Az opcionális, olvasható REST API alapértelmezés szerint **letiltva van**. A teljes referenciához tekintse meg a [REST API guide](API.md) dokumentációt.
- `Api__Enabled`: A read-only REST API fő kapcsolója (alapértelmezett `false`). Ha `false`, minden `/api/*` útvonal `404` választófejet ad vissza.
- `Api__AllowAttachmentDownloads`: Engedélyezi a csatolmányok bytes adatainak letöltését az API-n keresztül (alapértelmezett `true`). Ha `false`, a csatolmány végpont `403`-at ad vissza.
- `Api__AllowMessageDownloads`: Engedélyezi a teljes üzenetek letöltését az API-n keresztül, mind az újraépített `/eml`, mind az eredeti `/raw` formátumot (alapértelmezett `true`). Ha `false`, mindkét végpont `403`-et ad vissza. Egy teljes üzenet tartalmazza a csatolmányait, ezért **mind** ez a kapcsoló, **mind az** `Api__AllowAttachmentDownloads` értéknek `true` kell lennie; bármelyik kikapcsolása mindkét végpontot letiltja.
- `Api__EnableSwaggerUi`: A Swagger UI elérhetősége `/apidocs`, valamint az OpenAPI dokumentum elérhetősége `/apidocs/spec/v1.json` (alapértelmezett `true`). Mindkettőhez bejelentkezett böngésző-sesszió szükséges.
- `Api__DefaultPageSize`: Alapértelmezett oldalszám a listázó végpontokhoz, ha nincs megadva (alapértelmezett `20`).
- `Api__MaxPageSize`: Maximális engedélyezett oldalszám; a nagyobb kéréseket korlátozzák (alapértelmezett `100`).
- `Api__RateLimitPerMinute`: Fix ablakos kérés-keret (request budget) API kulcsonként percenként (alapértelmezett `120`).

### 🤖 MCP Server Beállítások {#mcp-server-settings}
Az opcionális MCP (Model Context Protocol) szerver ugyanazt a csak-olvasási módbeli e-mail hozzáférést biztosítja az AI ügynökök számára a Streamable HTTP transzporton keresztül, точно olyan, mint a REST API. Ez **alapértelmezés szerint le van kapcsolva**. A teljes referenciához tekintse meg az [MCP guide](MCP.md) dokumentációt. Az MCP ugyanazokat az API kulcsokat használja, mint a REST API (`ma_...` bearer tokenek), és ugyanazt a felhasználónkénti fiók-hozzáférési korlátozást (scoping).
- `Mcp__Enabled`: A MCP szerver fő kapcsolója (alapértelmezett `false`). Ha `false`, akkor a `/mcp` végpont `404`-öt ad vissza.
- `Mcp__AllowAttachmentDownloads`: Engedélyezi, hogy a `get_attachment` eszköz csatolmány-байtokat adjon vissza (alapértelmezett `true`). Ha `false`, az eszköz hibát ad vissza.
- `Mcp__DefaultPageSize`: Az `search_emails` által használt alapértelmezett oldalméret, ha a hívó elhagyja a `pageSize` paramétert (alapértelmezett `20`).
- `Mcp__MaxResults`: A `search_emails` oldalméretének felső határa; a nagyobb értékeket korlátozzák (alapértelmezett `100`).
- `Mcp__MaxAttachmentBytes`: Maximális csatolmány méret (байtban), amelyet a `get_attachment` eszköz inline, base64 formátumban ad vissza; a nagyobb csatolmányokat elutasítjuk (alapértelmezett `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Engedélyezi, hogy a `get_email_eml` és `get_email_raw` eszközök teljes üzeneteket adjanak vissza (alapértelmezett `true`). Ha `false`, az eszközök hibát adnak vissza. Egy teljes üzenet magával viszi a csatolmányait, ezért **mind** ez a kapcsoló, **mind** az `Mcp__AllowAttachmentDownloads` értékének `true` kell lennie; bármelyik kikapcsolása mindkét eszközt devreztetési mentessé teszi.
- `Mcp__MaxMessageBytes`: Maximális üzenetméret (байtban), amelyet a `get_email_eml` és `get_email_raw` eszközök inline adnak vissza; a nagyobb üzeneteket elutasítjuk (alapértelmezett `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Fix ablakos kérés-keret (request budget) API kulcsonként percenként (alapértelmezett `120`).

### 📨 MailSync Beállítások {#mailsync-settings}
- `MailSync__IntervalMinutes`: Az e-mailek szinkronizálása közötti perces intervallum. Ez a globális alapértelmezett; minden fiók ezt egyenként felülírhatja a Create/Edit oldalon (hagyja üresen a globális alapértelmezett használatához).
- `MailSync__FullSyncIntervalHours`: Opcionális globális alapértelmezett az automatikus teljes újraszinkronizálásokhoz, órában kifejezve. Ha nincs beállítva (az alapértelmezett), nem fut automatikus teljes szinkronizálás, hacsak nem van beállítva egy fiókra vonatkozó `FullSyncIntervalHours` érték a Create/Edit oldalon. A fiókonkénti értékek felülírják ezt a globális alapértelmezettet.
- `MailSync__TimeoutMinutes`: Fiókonkénti szinkronizálási timeout perces értékben. Egy olyan szinkronizálás, amely túl hosszú ideig fut, a következő üzenet határonál megáll, megőrzi a checkpointjeit, és folytatja a következő futtatáskor; a feladatot `Timed Out` jelzi, és a `LastSync` nem halad előre. Csak a tervezett szinkronizálásokra vonatkozik; a felületről indított szinkronizálás (manuális szinkronizálás vagy teljes újraszinkronizálás) timeout nélkül fut. A `0` (alapértelmezett) vagy bármilyen nem pozitív érték azt jelenti, hogy nincs timeout. Frissítés előtt ellenőrizze az értéket, ha nagy postafiókokat szinkronizál. Lásd: [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Az IMAP kapcsolatoknak megfelelő csatlakozási timeout másodpercben.
- `MailSync__CommandTimeoutSeconds`: Az IMAP parancsoknak megfelelő parancscsoport-timeout másodpercben.
- `MailSync__AlwaysForceFullSync`: Azzal, hogy mindig kényszerítsen teljes szinkronizálást (true/false).
- `MailSync__IgnoreSelfSignedCert`: Azzal, hogy figyeljen-e az önmagában aláírt tanúsítványokat (true/false).
- `MailSync__MaxConcurrentSyncs`: Hány fiók szinkronizálása futhat egyszerre. Alapértelmezett: `1` (szekvenciális, háttátteljesíthető). A feladatozó új helyeket tölt be, amint azok felszabadulnak, ahelyett, hogy egy teljes csomakra várna, így egy lassú postafiók már nem tartja vissza a többiத். Növelje a több fiók egyidejű szinkronizálásához — vegye figyelembe a szolgáltatóak sebességkorlátait és a helyi erőforrás-használatot. Lásd: [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Opcionális, másodpercben megadott késleltetés, amely minden fiók szinkronizálási feladat végén alkalmazandó. Alapértelmezett: `0` (nincs késleltetés). Hasznos a robbanásszerű indítások elkerülésére, amikor `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Hány problémát emlékszik meg a szinkronizálási feladat minden típusból a fiók oldalon. A sikertelen mappákat, a hiányzó mappákat és a sikertelen üzeneteket külön költségvetés szerint kezelik, így egy típusú probléma tömege nem tünteti el a többiét; a kereteken túl lévő elemeket csak számolják, nem tárolják. Alapértelmezett: `20`. A `0` kikapcsolja a részletezést, és csak a számlálókat hagyja meg. Lásd: [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Mappák, amelyeket **minden** fiók számára kizár a szinkronizálásból, a fiókonkénti kizárt mappák listán felül. Alapértelmezettileg üres, így a meglévő beállításokat nem érinti. A két lista **összeadódik** — egy mappa akkor kerül kihagyásra, ha bármelyik listában szerepel — és mindkettő ugyanazokat a szabályokat használja: pontos egyezés a teljes útvonalon, pontos egyezés a mappa nevén, útvonal-suffix egyezés (így a `Drafts` is megfelel a `INBOX/Drafts` és `INBOX.Drafts` értékeknek), valamint minden mappán alul található elem, ha a `MailSync__ExcludeSubfolders` be van kapcsolva. Az egyezés nem érzékeny a kis- és nagybetűre. Hasznos, ha ugyanazon a szerveren több postafiókot importál, ahol az alternatíva minden fiókhoz azonos kizárási listát vezetni lenne. Alapértelmezettileg nincs kizárt mappa; hogy mely neveket érdemes felsorolni, az a szervertől és annak nyelvétől függ. Példa egy postafiók fなhérre, amely tartalmazza a naptár és a kapcsolat mappákat is:
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
- `MailSync__ExcludeSubfolders`: Azzal, hogy egy kizárási bejegyzés a megnevezett mappa alatti mappákat is lefedi-e. Ez ugyanúgy vonatkozik a fiókonkénti listára és a telepítésen keresztüli listára is, valamint mindkét szolgáltatóra. Alapértelmezett: `false`, amely megegyezik a beállítás létezése előtti viselkedéssel: egy `Deleted Items` bejegyzés csak arra a mappára vonatkozik, és egy faág kizárásához a gyökerét és minden alatta lévő mappát meg kell nevezni. Állítsa `true` értékre, hogy egy bejegyzés az alatta lévő teljes fát is lefedje — egy `Deleted Items` bejegyzés ilyenkor a `Deleted Items/2024` és az adott ág többi részét is archiván kívül tartja. A szabály az útvonal-szalagjhoz rögzül, így egész mappákon dolgozik, és soha nem egy név részét: a `Archive` lefedi a `Archive` és a `Archive/Team` elemeket, de hagyja érintetlenül a `Team Archive Review` nevű postafiókot. A `.` is úgy számít szalagjának, ahogy a path-suffix szabály is kezeli.

### 📧 Eredeti üzenetek tárolási beállításai {#original-message-storage-settings}

Alapértelmezés szerint az archívum csak az e-mailek elemzett részeit tárolja, így egy exportált `.eml` egy rekonstrukció: az átviteli kódolások, a MIME határértékek és a fejléc-összehajtások eltérhetnek a megérkezett üzenettől, a DKIM aláírások pedig már nem ellenőrizhetőek. `RawMessages:Enabled` beállítás esetén az eredeti, teljes üzenet is tárolásra kerül.

| Paraméter | Alapértelmezett | Cél |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Minden új archivált e-mail eredeti példányának tárolása. |
| `RawMessages__Storage` | `postgres` | Hol tárolódnak a byte-ok: `postgres` vagy `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | A nagyobb üzenetek az eredeti változat nélkül kerülnek archivásra. `0` = nincs korlát. |
| `RawMessages__IncludeGraph` | `true` | Microsoft 365 (Graph) fiókok eredeti üzeneteinek tárolása is. |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Az üzenetfájlok könyvtára. A Dockerben felszerelt (mounted) volumenre kell mutasson. |
| `RawMessages__FileSystem__ShardDepth` | `2` | A könyvtár mélysége, amelyre az e-mail id kiterjed. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | A SHA-256 ellenőrzése minden letöltéskor. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Azok üzenetfájlok törlése, amelyekhez már nincs e-mail hivatkozás. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | E tisztító folyamat gyakorisága. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Az ennél frissebb fájlok soha nem kerülnek törlésre. |

!!! warning "A fájlrendszer backendnek szüksége van felszerelt volumenre"
    `RawMessages__Storage=filesystem` esetén az üzenetfájlok aຖານadatbázison kívül tárolódnak.
    Bind mount nélkül a konténer írható rétegében fognak lenni, és a következő konténer újraindításakor elvesznek, míg az adatbázis sorai megmaradnak. A könyvtárat az adatbázis mentésével (dump) együtt biztonságba mentse!

Lásd a **[Original Message Storage](RawMessages.md)** részletes leírását: hol érhető el az eredeti üzenet, a tárolási backendek, hogyan lehet a meglévő üzeneteket `--migrate-raw-storage` segítségével mozgatni közöttük, és mit kell biztonságba menteni.

### 📈 Monitorozási beállítások {#monitoring-settings}

| Paraméter | Alapértelmezett | Cél |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | Eztől régebbi engedélyezett fiókok esetén a `/api/v1/health` jelent `Degraded`. A `0` kikapcsolja a ellenőrzést. |
| `Metrics__Enabled` | `false` | Engedélyezi a Prometheus metrik endpointet és annak frissítési ciklusát. |
| `Metrics__RefreshMinutes` | `5` | Mennyire gyakran készül fel a metrikák snapshotja. Az 1 alatti értékeket 1-re emeli. |

Az egészségügyi (health) és metrika endpointekhez admin API kulcs és `Api__Enabled=true` szükséges. Lásd:
**[Health and Metrics](Monitoring.md)** az endpointek, a metrika nevek, a Prometheus
konfiguráció és a Docker health check miatt.

### 📤 BatchRestore Beállítások {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Az e-mailek száma, amely aszinkron feldolgozást indít.
- `BatchRestore__MaxSyncEmails`: Az e-mailek maximális száma szinkron feldolgozáshoz.
- `BatchRestore__MaxAsyncEmails`: Az e-mailek maximális száma aszinkron feldolgozáshoz.
- `BatchRestore__SessionTimeoutMinutes`: A batch restore session timeout percenként.
- `BatchRestore__DefaultBatchSize`: Az e-mail műveletek alapértelmezett batch mérete.

### 🏢 Tenant Management beállítások {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: A legmagasabb elérhető számú mailbox, amely egyetlen Tenant Management művelet során hozzáadható. Az alapértelmezett érték `1000`. Növelje ezt nagyon nagy bérlőkhöz (tenants), vagy csökkentse a véletlen tömeges importok megelőzése érdekében. Ha a limite vượtılır, a művelet hibávalt ír vissza, és egyetlen fiókot sem hoz létre. Részletes információkért lásd a [M365 Tenant Import Guide](M365TenantImport.md) dokumentációt.

### 🐄 Mailcow beállítások {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: A Mailcow egyetlen aktiválási futtatása során létrehozható postafiókok maximális száma. Az alapértelmezett érték `1000`. Csökkentse ezt a véletlen tömeges létrehozás megelőzése érdekében, vagy növelje nagyobb példányok esetén. Ha a limite vượt wordt, nem kerülnek létre fiókok. Részletekért lásd a [Mailcow Instances](Mailcow.md) részt.
- A Mailcow integráció emellett megköveteli a `Security__CredentialEncryptionKey` használatát (lásd: [Security Settings](#-security-settings)). Egy példány csatlakoztatása és a postafiókok aktiválása is elutasításra kerül, amíg a hitelesítési adatok titkosítása (credential encryption) nincs engedélyezve, így az API kulcs és a generált alkalmazásjelszavak soha nem tárolhatók szöveges formátumban.
- Kibocsátó (outbound) hozzáférés szükséges a Mailcow HTTPS API-hoz és az IMAP hosthoz TLS felett (általában a `993` porton keresztül).

### 📦 BatchOperation Beállítások {#batchoperation-settings}
- `BatchOperation__BatchSize`: Az e-mail műveletek batch mérete.
- `BatchOperation__PauseBetweenEmailsMs`: Az egyedi e-mailek közötti szünet millisekundyban.
- `BatchOperation__PauseBetweenBatchesMs`: A batchok közötti szünet millisekundyban.

### 📤 Offload Beállítások {#offload-settings}
A [date-windowed offload](Offload.md) beállításai. Az alapértelmezett értékek visszaállítják az alkalmazás korábbi viselkedését, mielőtt ez a funkció létezett volna, így az olyan telepítések, amelyek nem konfigurálják ezt a szakaszt, nem érintettek.
- `Offload__MaxConcurrentJobs`: Hány visszaállítási (restore) vagy offload feladat futhat egyszerre. Az alapértelmezett érték `1`, ami megőrzi a feladatsoros (job queue) eddigi szigorúan szériális feldolgozását. A megadott értéktől függetlenül célként szolgáló postafiókonként maximum egy feladat futhat.
- `Offload__PrefetchMaxMessages`: A felső határ arra, hogy hány üzenet kerül indexelésre egy célpostafiókból a duplikáció-ellenőrzés érdekében. Az alapértelmezett érték `500000`. E fölött a ellenőrzés egyetlen mappára szűkül, és logolja, hogy a lefedettség csökkent meg.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Forrásmappák, amelyek soha nem kerülnek offload folyamatba; ezeket az újranevezés előtt keresik, és tartalmazzák a mappaszerkezet almappáit is. Alapértelmezés szerint üresek.
- `Offload__FolderRenameMap__<SourceFolder>`: Egy forrásmappa elérési útja első részeit írja át, például: `Offload__FolderRenameMap__Sent Items=Sent`. Alapértelmezés szerint üres.
- `Offload__MarkAsSeen`: A hozzáadott levelek jelölése olvashatónak-e. Az alapértelmezett érték `true`, ami megegyezik a meglévő visszaállítási viselkedéssel.

Mindkét mappa-beállítás szándékosan üres: a mappák átírása vagy törlése kérés nélkül meglepetés lenne azok számára, akik már használják a visszaállítási folyamatot. Így a konfiguráció nélküli futtatás mindent migráál (a spam mappákat is), és a célmappák mellett létrehoz egy második speciális mappák készletet.

### 📊 Sávszélesség-követési beállítások {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Engedélyezi vagy letiltja az IMAP sebességkorlátozás kezeléséhez használt sávszélesség-követést (true/false). Az alapértelmezett érték `false`. Ha engedélyezve van, a rendszer fiálonként követi a sávszélesség-használatot, és szüneteltetheti a szinkronizálást, ha elérhetővé stí a szolgáltató korlátai. További részletekért tekintse meg a [Rate Limit Handling](RateLimitHandling.md) részt.
- `BandwidthTracking__DailyLimitMb`: Napi letöltési limit megabyteben fiálonként. Az alapértelmezett érték `25000` (25 GB). A sávszélesség-korlátozással rendelkező szolgáltatóknál állítsa ezt a sebességkorlátozásuknak megfelelően (pl. `2500` azoknál a szolgáltatóknál, akiknek napi ~2500 MB limitük van). A rendszer szünetelteti a szinkronizálást, ha ez a limit elérhetővé stí.
- `BandwidthTracking__WarningThresholdPercent`: A napi limit százaléka, amelynél figyelmeztető üzenetek kerülnek naplózásra. Az alapértelmezett érték `80`. Amikor a sávszélesség-használat eléri ezt a százalékot, figyelmeztetések kerülnek a naplóba, hogy segítsen a közelgő korlátok nyomon követésében.
- `BandwidthTracking__PauseHoursOnLimit`: Az órák száma, amíg a szinkronizálás szünetel, ha elérhetővé stí a napi limit. Az alapértelmezett érték `24`. Ezen időszak után a limit jelző automatikusan törlődik, és a szinkronizálás folytatódik.
- `BandwidthTracking__TrackUploadBytes`: Követi-e továbbá a feltöltési sávszélességet is (true/false). Az alapértelmezett érték `false`. A legtöbb IMAP szolgáltató csak a letöltést korlátozza, így ez általában nem szükséges.

### 🎯 Kiválasztási beállítások {#selection-settings}
- `Selection__MaxSelectableEmails`: Az e-mailek maximális száma, amely egyszerre kiválasztható.

### 👁️ Nézet beállítások (Adatvédelem és megjelenítés) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: A beállítás szabályozza az e-mailek alapértelmezett megjelenítési módját az adatvédelem és a nyomon követés megelőzése érdekében (true/false). Az alapértelmezett érték: `false`.
  - Ha `true` értékre van állítva: Az e-mailek alapértelmezés szerint plain-text módban nyílnak meg, megakadályozva a nyomon követési pixel, a külső képek és a web beaconok automatikus betöltését. Ez az adatvédelem és az e-mail nyomon követése iránt érdeklődő felhasználóknak ajánlott.
  - Ha `false` értékre van állítva: Az e-mailek alapértelmezés szerint HTML módban nyílnak meg, megjelenítve a formázott e-mailt képekkel és stílusokkal.
  - A felhasználók az e-mail részleteinek oldalán található gombbal váltogathatnak az HTML és a plain-text nézetek között (csak akkor látható, ha mindkét formátum elérhető).
  - A "Full View" link tiszteledi a jelenleg kiválasztott megjelenítési módot.

- `View__BlockExternalResources`: Megblokkálja a külső erőforrásokat (távoli képek, külső CSS, külső szkriptek, web betűtípusok stb.) az HTML e-mail nézetekben a nyomon követés megelőzése és az adatvédelem javítása érdekében (true/false). Az alapértelmezett érték: `false`.
  - Ha `true` értékre van állítva: A külső erőforrásokat kiszűrik az HTML e-mailek megjelenítésekor. Csak az inline tartalom és a data URI-k (beleértve a `cid:` hivatkozásokon keresztül elérhető inline csatolmányokat) jelennek meg.
  - Ha `false` értékre van állítva: Az HTML e-mailek az összes eredeti külső erőforrásukkal jelennek meg.
  - **Fontos**: Ez a beállítás csak az e-mail **megjelenítésére** vonatkozik. A költözött (archived) e-maileket az adatbázisban teljesen változatlanul tároljuk, az összes eredeti tartalommal együtt.
  - A blokkolt erőforrások közé tartozik:
    - Távoli képek (nyomon követési pixel, szervereken tárolt külső képek)
    - Külső CSS stíluslapok
    - Külső betűtípusok @font-face használatával
    - Külső CSS importálások @import használatával
    - Külső háttérképek
  - Engedélyezett erőforrások:
    - data: URI-ként beágyazott inline képek
    - cid: URI-n keresztül hivatkozott inline csatolmányok
    - Inline CSS stílusok és style tagek
  - Ez a beállítás függetlenül működik a `DefaultToPlainText` beállítástól, és extra adatvédelmi réteget biztosít az HTML e-mailek megtekintésekor.

### 📊 Dashboard Settings {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Azzal kapcsolatos, hogy a számláló kártyák tartalmazzák-e a beérkező és kimenő részeket, az обліkártya pedig tartalmazza-e a különböző domainok számát. Az alapértelmezett érték `true`. Ha `false` értékre állítjuk, ezek a számok nem számolódnak ki, hanem egyszerűen nem jelennek meg: a számlálók egyszerű számot olvasnak ki, az csatolmányok számlálója pedig felhagy az ahhoz társított e-mailek egyesítésével, ami az egyetlen lekérdezés, amit ezek a részek tesznek szükségessé.
- `Dashboard__SelectablePeriods`: Azzal kapcsolatos, hogy a dashboard diagramok kínálnak-e fel felbontást, egy időszakot és nyilván másokat a mozgatáshoz. Az alapértelmezett érték `true`. Ha `false` értékre állítjuk, a diagramok az utolsó tizenkét hónapot mutatják havi bontásban és az egész archívum küldőit; a diagram végpontja úgy válaszol, mintha a beállítás nem létezne, és a leghitelesebb küldési dátumot nem keresi fel.
- `Dashboard__CacheSeconds`: Mennyi ideig tárolják a kiszámolt dashboard statisztikákat (összesítések a beérkező és kimenő bontással, fiókonkénti számlálók, diagram sorozatok, leggyakoribb küldők, legutóbbi e-mailek, adatbázis mérete) a szerver memóriában tárolt gyorsítótárában (in-memory cache). Az alapértelmezett érték `60` másodperc. Állítsa `0` értékre a gyorsítótárazás letiltásához, hogy a statisztikákat mindig újra számolják. A felbontás és az időszak diagramonként külön kerül gyorsítótárazásra, így egy olyan dashboard, amely vált a kiválasztások között, minden kiválasztáshoz egyetlen bejegyzést tart fenn. A nagyobb értékek csökkentik az adatbázis terhelését nagy környezetekben, de cserébe elavultabb számokat adhatnak. A szinkronization állapot jelzők, a tárolási értékek és a legutóbbi e-mailek listája mindig élő adatokból kerülnek lekérésre, és nem befolyásolja őket ez a gyorsítótár.
- `Dashboard__RefreshIntervalMinutes`: Milyen gyakran számolja újra a `DashboardStatsRefreshService` háttérszolgáltatás a költséges dashboard összesítéseket (összesített számlálók a beérkező és kimenő bontással, fiókonkénti számlálók, a fiókok domainjainak száma, az alapértelmezett diagram sorozat a leggyakoribb küldőkkel, adatbázis mérete) a `DashboardStatsCache` adatbázis táblázatba. Több millió e-mailt tartalmazó telepítéseknél a dashboard ezeket a előkészített értékeket olvassa ki a teljes archívum összesítése helyett a lekérdezési útvonalon, ami a ~10 másodperces első betöltést millisekundyra gyorsítja. A diagram végpontja az alapértelmezett kiválasztásra (egy év folyamán havi bontás, beérkező küldők, a jelenlegi időszak) ugyanabból az oszlopból válaszol, így az visszatéréskor az első megjelenítés számaiban látjuk az adatokat; minden egyéb kiválasztást a lekérdezéskor számolják ki, és a fent említett memóriában tárolt gyorsítótárban tárolják. Az alapértelmezett érték `15` perc. Állítsa `0` értékre a háttérfrissítés letiltásához; ilyenkor a dashboard újra élő módon számolja ki a statisztikákat (a fent említett memóriában tárolt gyorsítótár továbbra is érvényes). A megjelenített értékek akár a konfigurált intervallummal elmaradhatnak a tényleges archívum állapotától. Egy olyan sor, amelyet más `ShowDirectionSplits` vagy `SelectablePeriods` értékekkel számoltak ki, mint amelyek jelenleg érvényesek, nem kerül felhasználásra; a dashboard élő módon számol tovább a következő frissítésig.

### 🔄 Jobs Settings {#jobs-settings}
- `Jobs__RefreshSeconds`: Mennyire gyakran töltődik újra a háttérfolyamatok oldala, amíg egy böngészőfül nyitva tartja azt. Az alapértelmezett érték `30` másodperc. Állítsa `0` értékre az automatikus újratöltés kikapcsolásához, így kézzel is frissíthető lesz az oldal. Minden újratöltés az összes nyolc munkaforrásból építi újra az oldalt, ezért sok fiókot tartalmazó telepítéseknél a hosszabb intervallum csökkenti a terhelést, amit minden nyitott fül megszámlál.

### 🗃️ Npgsql beállítások {#npgsql-settings}
- `Npgsql__CommandTimeout`: Az adatbázis-parancsok időtúllépési ideje másodperceket számol.

### 📥 Feltöltési beállítások {#upload-settings}
- `Upload__MaxFileSizeGB`: A feltöltések maximális fájlmérete GB-ban.
- `Upload__KeepAliveTimeoutHours`: A feltöltések keep alive timeout értéke órában.
- `Upload__RequestHeadersTimeoutHours`: A request headers timeout értéke órában.

### 📂 Helyi importálási beállítások {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, stb.: A CLI importálási parancsok (`--import-mbox`, `--import-eml`) számára engedélyezett helyi könyvtárak fehérlistája. Minden egyes bejegyzés egy elérési út a konténeren belül. Az importálási fájlokat az egyik ilyen könyvtárba kell csatolnia a Docker volume-ok segítségével. Alapértelmezett: `/data/import` (visszaállítási értékként automatikusan a `/app/uploads` beállításra áll).
  - Amikor a `docker exec` parancsot használja az importálási parancs futtatásához, a `--file` paraméteren keresztül megadott fájl elérési útnak az egyik engedélyezett úton belül kell lennie.
  - Ez a biztonsági intézkedés megakadályozza a tetszőleges fájlrendszer-hozzáférést a CLI parancsokból.
  - Különböző import forrásokhoz több elérési út is konfigurálható.
  - Részletes használati útmutatóhoz tekintse meg a [CLI commands](CLI.md) részt.

### 📄 CSV Import beállítások {#csv-import-settings}
- `CsvImport__MaxRows`: A single tömeges import során feldolgozott CSV sorok (postafiókok) maximális száma. Az alapértelmezett érték `5000`. Növelje ezt az értéket nagy implementációk esetén; csökkentse a hatást aຖານadat terhelésre egyetlen import futtatás során.
- `CsvImport__MaxFileSizeBytes`: A feltöltött CSV fájl maximális engedélyezett mérete (byte-ban). Az alapértelmezett érték `10000000` (10 MB). Szükség esetén igazítsa ezt az értéket az upload limitekhez.
- Lásd a [Account Import Guide](Account-Import.md) dokumentációt a CSV útján történő tömeges IMAP fiók importálásával kapcsolatos részletes használati utasításokért.

### 📤 Audit Export Beállítások {#audit-export-settings}
A naplózási adatok exportálási oldal beállításai (csak admin számára, a Logs oldalról elérhető). Az export egy ZIP csomagot generál a meglévő archívából, amely táblázatos tömegadatokat (INDEX.XML + CSV táblák + DTD) tartalmaz külső audit eszközök számára. See a [Audit Data Export Guide](AuditExport.md) részletekért a használattal kapcsolatban.
- `AuditExport__DataSupplierName`: Az "Data supplier name" űrlapmező alapértelmezett értéke, amely az exportált indexfájlban azonosítja a szervezetet. Alapértelmezett: üres.
- `AuditExport__DataSupplierLocation`: Az "Data supplier location" űrlapmező alapértelmezett értéke (pl. székhely). Alapértelmezett: üres.
- `AuditExport__Comment`: Az "Comment" űrlapmező alapértelmezett értéke, amely szabad szövegként kerül bele az exportált indexfájlba. Alapértelmezett: üres.
- `AuditExport__OutputDirectory`: Katalógus, ahová a generált ZIP fájlok kerülnek. A relatív útvonalakat az alkalmazás tartalom gyökeréhez viszonyítva oldják fel. Alapértelmezett: `exports/audit`.
- `AuditExport__RetentionDays`: Napok száma, amely után a befejezett exportfájlokat a napi takarítás törli. Alapértelmezett: `30`.
- `AuditExport__MaxRangeYears`: Az export időszak kezdő- és befejező dátuma közötti maximális megengedett időtartam. Alapértelmezett: `10`.
- Az űrlapmezők ezekről az alapértelmezett értékekről vannak előre kitöltve, de minden exportnál szerkeszthetők.
- Minden export egy kezdő és egy eredmény bejegyzést ír a hozzáférési naplóba (típus: "Audit Data Export"), így a történet revision-biztos anélkül, hogy DB schéma változtatást igényelne.

### 🔒 Törlési szabályzat beállításai {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Meghatározza, hogy a költöztesett e-mailek manuális törlása engedélyezett-e (true/false). Az alapértelmezett érték `true`. Ha `false` értékre van állítva:
  - Minden archivált e-mail le van zárva (`IsLocked = true`) a rendszerindításkor az adatbázis-megfelelőségi trigger (compliance trigger) révén, megakadályozva a módosítást vagy törlést az adatbázis szintjén.
  - A manuális törlés (egyes vagy tömeges) az alkalmazás szintjén blokkolva van, hibaüzenettel együtt.
  - Az oszlop alapértelmezett értékét úgy módosítják, hogy az újonnan importált e-mailek is le vannak zárva.
  - A jelenlegi szabályzat állapota minden indításkor bejegyzésre kerül az AccessLogs táblába (a Logs oldalon "Deletion Policy" bejegyzésekként látható) a módszertani ellenőrizhetőség érdekében.
  - A helyi megőrzési (retention) törlés mentesül ettől: azok az e-mailek, amelyek egy konfigurált megőrzési időtartam alá esnek, továbbra is törlődnek (a megőrzési folyamat során a törlés előtt közvetlenül felengedik a zárásukat).
- `DeletionPolicy__MaxBulkDeleteEmails`: A felső határ egy szűrőalapú tömeges törléshez (*Delete all matches*). Az alapértelmezett érték `100000`. Ha egy szűrő több e-maillel érint, a rendszer elutasítja azt egy, a szűrő szűkítésére ösztönző üzenettel; a folyamat soha nem töröl el csendben rekordokat. A megőrzési szabályzat által lezárt e-maileket kihagyják és jelentik a folyamat állapotában, nem pedig felengedik a zárásukat. A folyamat újra ellenőrzi ezt a limiteket és `DeletionPolicy__DeletionAllowed` a kezdésekor, így a szabályzat módosítása előtt sorba állított folyamat leáll, ahelyett hogy a régi beállításokkal futna le.
- **變 Immutable védelem:** Amikor `IsLocked = true`, az adatbázis-megfelelőségi trigger (`prevent_locked_email_changes`) bármilyen módosítást blokkol az e-mail sorban — minden oszlop védett, nem csak egy fix mezőlista. Az egyetlen mentesült oszlop `IsLocked` maga (hogy a megőrzési törléshez és a rendszerindítási szabályzat alkalmazásához szükséges feloldás lehetséges marad), valamint `FolderName` (hogy az IMAP szinkronizálás frissíthesse a mappát, ha egy e-mailt a szerver oldalon mozgatnak). A védelem oszlopfüggőtől független (JSONB-alapú összehasonlítás), és automatikusan lefedi a jövőbeni schéma-bővítéseket.

### 🕐 Időzóna beállítások {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Az időzóna, amelyet az e-mailek időbélyegének megjelenítéséhez használnak a felületen. IANA időzóna-azonosítókat használ (pl. "Europe/Berlin", "Asia/Tokyo"). Az alapértelmezett érték az "Etc/UCT" a hátt兼容 (backward compatibility) érdekében. Az e-mailek importálásakor az időbélyegeket erre az időzónára konvertálják a megjelenítés érdekében.

### 🎉 ReleaseNotes beállítások (Verziófrissítési felugró ablak) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: Az az verzió-karakterlánc, amelyet ez az példány jelent. Ezt a GitHub release tagekkel hasonlítják össze, hogy eldönthessék, létezik-e újabb kiadás. Ha üres, a changelog a fordított assembly verzióra tér vissza, és a frissítési jelzés is kikapcsol.
- `ReleaseNotes__Enabled`: A verziófrissítési felugró ablak engedélyezése vagy kikapcsolása (true/false). Az alapértelmezett érték `true`. Ha engedélyezve van, az adminisztrátorok egyetlen alkalommal látni fognak egy changelog modalt az alkalmazás frissítése után, amely az aktuális verzióhoz a GitHub Releases oldalról lekombinált release note-okat mutatja. Minden adminisztrátor el tudja tüntetni a modalt, és ez csak egy új verzió esetén jelenik meg újra. Állítsa `false` értékre a funkció teljes kikapcsolásához, beleértve a frissítési jelzést is.
- `ReleaseNotes__UpdateHintEnabled`: Mutasson-e egy kis toast értesítést a jobb alsó sarokban az adminisztrátorok számára, ha a GitHub újabb stabil kiadást tesz közzé, mint ami telepítve van (true/false). Az alapértelmezett érték `true`. A toast linkel az adott kiadáshoz, és verziónként elrejthető a böngészőben. A tervezettel készült (drafts) és a korai kiadások (prereleases) figyelmen kívül maradnak. Erre a `ReleaseNotes__AppVersion` szükséges; nélküle nem jelenik meg semmilyen jelzés.
- `ReleaseNotes__UpdateCheckIntervalHours`: Milyen gyakran frissüljön a GitHub release lista, órában kifejezve. Az alapértelmezett érték `6`. Egy rövidebb intervallum gyorsabban láthatóvá teszi az új kiadást a folyamatosan futó példányokban, egy hosszabb pedig csökkenti a GitHub API hívások számát. A minimum `1`.

### 🔧 Adatbázis karbantartási beállítások {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Az automatikus napi adatbázis karbantartás engedélyezése vagy letiltása (true/false). Az alapértelmezett érték `false`. Ha engedélyezve van, a rendszer automatikusan futtat VACUUM ANALYZE műveleteket az adatbázis teljesítményének optimalizálása és a felesleges adatgyűjtés (bloat) megelőzése érdekében. További részletekért lásd a [Database Maintenance Guide](DatabaseMaintenance.md) dokumentációt.
- `DatabaseMaintenance__DailyExecutionTime`: Az az időpont a nap folyamán, amikor az adatbázis karbantartásnak futnia kell, 24 órás formátumban (HH:mm). Az alapértelmezett érték `02:00`. Válasszon egy olyan időpontot, amikor alacsony a rendszer aktivitása.
- `DatabaseMaintenance__TimeoutMinutes`: A karbantartási műveletekhez megengedett maximális időpercben. Az alapértelmezett érték `30`. Növelje ezt az értéket nagyobb adatbázázok esetén.

### 🧬 Melléklet de-duplikációs beállítások {#attachment-deduplication-settings}
A melléklet de-duplikáció minden egyes egyedi melléklet-payloadot csak egyszer tárol meg (SHA-256 alapú tartalom-adresszálás), és ez egy **alapvető funkció, amely mindig engedélyezve van** – szándékosan nincs be/ki kapcsoló. Csak az alábbi tömeges/ütemezési paraméterek állíthatók be. A teljes részletért lásd a [Attachment Deduplication Guide](AttachmentDeduplication.md) dokumentációt.

- `AttachmentDeduplication__BatchSize`: Az egyszeri háttérben futó, már meglévő adatok migrációja során egy tranzakcióban migrált meglévő mellékletek száma. Az alapértelmezett érték `200`. A nagyobb értékek gyorsabb migrációt tesznek lehetővé, de nagyobb memóriát/adatbázs-terhelést igényelnek batch esetén.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Opcionális szünet (millisekundyban) a migrációs batch-ek között a busy rendszereken tapasztalt adatbázs-terhelés korlátozására. Az alapértelmezett érték `0` (nincs szünet).
- `AttachmentDeduplication__StartupDelaySeconds`: Késleltetés (másodpercekben) az alkalmazás elindítása után, mielőtt a háttérben futó migráció megkezdődne, hogy a schema migrációnak legyen ideje befejeződni. Az alapértelmezett érték `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Az állandóan futó, szemétgyűjtés (garbage collection) intervalla (órákban), amely eltávolítja azokat a melléklet-payloadokat, amelyekre már egyetlen e-mail sem hivatkozik. Az alapértelmezett érték `12`. Ez függetlenül fut `DatabaseMaintenance__Enabled` folyamattól.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Adatbázis parancs timeout (másodpercekben) a migrációs batch műveletekre (INSERT SHA-256 hasholással és UPDATE). Az alapértelmezett érték `300` (5 perc). Emelje ezt az értéket nagyon nagy adatbázok vagy mellékletek esetén, vagy csökkentse, ha gyorsabb hibale discovery-t szeretne. Ha egy batch még mindig timeout-ot kap, a szolgáltatás automatikusan megpróbálkozik a batch méretének felétől.

### 💾 Fiók tárolási beállítások {#account-storage-settings}
A fiókonkénti tárolási megjelenítés a Dashboard "Account Overview" táblázatban és a MailAccounts "Show All" táblázatban minden egyes fiók adatbázis-tárolási használatát (összes e-mail mező + csatolások) mutatja. Egy autark háttérszolgáltatás (`AccountStorageRefreshService`) a PostgreSQL `pg_column_size` összeadásaával számolja ki az értékeket oszloponként (minden e-mail mezőjét lefedve anélkül, hogy az e-mail testekét kellene beolvasnia), majd elmenti azokat a `AccountStorageCache` táblázatba. Ez a szolgáltatás függetlenül fut a `DatabaseMaintenance__Enabled`-tól.

- `AccountStorage__Enabled`: A tárolási frissítő szolgáltatás engedélyezése vagy letiltása (true/false). Az alapértelmezett érték `true`. Ha engedélyezve van, a szolgáltatás indításkor folytatható módon feltölti a függőben lévő fiókok adatait (a `AccountStorageBackfillState` révén hibatűrő), majd ezután napi teljes frissítést végez. Ha letiltva van, a tárolási értékek csak akkor frissülnek, ha e-maileket szinkronizálnak, importálnak vagy törölnek, de az megjelenített értékek idővel elavulhatnak.
- `AccountStorage__DailyExecutionTime`: A napi teljes frissítés időpontja (24 órás formátum `HH:mm`) minden fiók számára. Az alapértelmezett érték `02:30`. Válasszon egy olyan időpontot, amikor alacsony a rendszertevékenység.
- `AccountStorage__BackfillDelayMs`: Késleltetés (millisekundumban) a fiókok között az indítási elsődleges feltöltés során. Az alapértelmezett érték `5000`. Az alacsonyabb értékek felgyorsítják a feltöltést, de növelik az adatbázis terhelését; nagyon nagy archívumok esetén emelje ezt az értéket, hogy elkerülje az adatbázis túlterhelését.
- `AccountStorage__RefreshBatchDelayMs`: Késleltetés (millisekundumban) a fiókok között a napi teljes frissítés során. Az alapértelmezett érték `1000`. Az alacsonyabb értékek felgyorsítják a frissítést, de növelik az adatbázis terhelését; nagyon nagy archívumok esetén emelje ezt az értéket.
- `AccountStorage__CommandTimeoutSeconds`: Adatbázis-parancs timeout (másodpercben) a fiókonkénti tárolás számításához (`pg_column_size` egy fiók összes során). Az alapértelmezett érték `300` (5 perc). A számítás minden e-mail sort beolvas, beleértve a TOAST adatokat is, ami nagyon nagy archívumok esetén (250k+ e-mail) jóval több mint egy percet vehet igénybe; a korábbi 30 másodperces Npgsql alapértelmezett érték miatt a frissítés csendben elbukott, és a dashboard elavult értékeket mutatott. Emelje ezt az értéket, ha a számítás még mindig timeoutot idéz az Ön archívumánál.

> 💡 **Megjegyzés**: A tárolási értékek azonnal frissülnek minden e-mail szinkronizálás, importálás vagy megőrzési (retention) törlés után, így az megjelenített értékek a napi frissítés nélkül is naprakész maradnak. A napi frissítés egy biztonsági háló, amely az átmeneti eseteket (pl. közvetlen adatbázis-módosítások) kezeli.

### 📝 Naplózási beállítások {#logging-settings}
- `Logging__LogLevel__Default`: Az alkalmazás alapértelmezett naplózási szintje. A rendelkezésre álló szintek: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Az alapértelmezett érték: `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: A naplózási szint az ASP.NET Core framework üzenetekhez. Alapértelmezett: `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: A naplózási szint az Entity Framework adatbázis parancsokhoz. Alapértelmezett: `Warning`.

### 🛡️ Biztonsági beállítások {#security-settings}
- `AllowedHosts`: A alkalmazás számára engedélyezett host nevek pontosvesszővel elválasztott listája. Ez segít megelőzni az HTTP Host header támadásokat. Példa: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Fontos**: Ne használja a `*`-t termelési környezetben, mivel az kikapcsolja a host header validációt.

#### 🔐 Hitelesítési adatok titkosítása tároláskor (at rest) {#credential-encryption-at-rest}
A levelezési fiókok hitelesítő adatai (IMAP jelszó, M365 client secret és MSA OAuth tokenek) titkosítva tárolhatók az adatbázisban. A titkosítási kulcs **az adatbázison kívül** marad (környezeti változó / Docker secret), így az adatbázis mentése (dump) önmagában sem elég a hitelesítési adatok kiolvasásához.

- `Security__CredentialEncryptionKey`: Base64 kódolt 256 bites (32 byte) kulcs (key encryption key). Generáljon ilyen kulcsot a következő paranccsal:
  ```bash
  openssl rand -base64 32
  ```
  A değeri állítsa be környezeti változóként (ajánlott: `Security__CredentialEncryptionKey` a compose fájlban vagy a konténer környezetében). Az alkalmazás nem támogatja a `_FILE` stílusú Docker secreteket — ha Docker secretet használ, csatolja (mount), és töltse be a tartalmát a környezeti változóba az entrypoint során. Soha ne committeleyin kulcsot a `appsettings.json`-ba. Ha kulcs van konfigurálva, a hitelesítő adatokat íráskor titkosítják, egy háttérszolgáltatás pedig egyszer lefutva titkosítja a meglévő plain text hitelesítő adatokat.
- `Security__EncryptAccountCredentials`: Az új/frissített hitelesítő adatok titkosításának engedélyezése vagy letiltása (true/false). Alapértelmezett: `true`. Ha nincs kulcs konfigurálva, a hitelesítő adatok plain text formátumban tárolódnak, és a folyamat megindításakor figyelmeztetés kerül a logokba — a meglévő telepítéseket nem érinti, amíg kulcs nem kerül megadásra.
- `Security__CredentialEncryptionBackfill`: A háttérszolgáltatás a folyamat megindításakor titkosítsa-e a meglévő plain text hitelesítő adatokat (true/false). Alapértelmezett: `true`. Ha nincs konfigurálva kulcs, a művelet semmi hatással nem rendelkezik.
- `Security__CredentialEncryptionKeyPrevious`: Opcionális korábbi kulcs, amelyet csak azzal titkosított értékek dekódolásához használ (kulcscsere esetén). Az új értékeket mindig a `Security__CredentialEncryptionKey` kulccsal titkosítják.

  > ⚠️ **Kulcscsere (Key rotation)**: a csere után tartsa konfigurálva a `Security__CredentialEncryptionKeyPrevious`-at **amíg minden hitelesítő adat nem íródik felül** (pl. a fiók szerkesztésével vagy egy automatikus OAuth token frissítéssel) — az óreg kulccsal titkosított meglévő értékeket *nem* titkosítják automatikusan az új kulccsal. Amint eltávolítja a korábbi kulcsot, minden olyan hitelesítő adat, amely még azzal titkosítva van, olvashatatlanná válik, és újra meg kell adni. Amíg konfigurálva van, olyan biztonságosan kell kezelni, mint a jelenlegi kulcsot.

> ⚠️ **Tartsa biztonságban a kulcsot**: A kulcs nélkül a titkosított hitelesítő adatok nem állíthatók vissza, és újra meg kell adni őket. Készítsen róla mentési másolatot az adatbázis mentéseivel együtt (de külön is).
>
> 💡 **Meglévő telepítések**: Frissítsen biztonságosan — amíg nincs beállítva `Security__CredentialEncryptionKey`, semmi nem változik. A kulcsot akkor állítsa be, amikor készen áll; a meglévő plain text hitelesítő adatok olvashatóak maradnak, és a háttérben titkosításra kerülnek.

### 🔐 OIDC Konfiguráció {#oidc-configuration}

A részletes beállítási útmutatóhoz az OpenID Connect hitelesítéshez, tekintse meg az [OIDC Implementation Guide](OIDC_Implementation.md) dokumentációt.

#### Alapvető OIDC Beállítások {#basic-oidc-settings}
- `OAuth__Enabled`: Az OIDC hitelesítés engedélyezése vagy letiltása (true/false)
- `OAuth__Authority`: Az OpenID Connect authority URL (pl. https://sts.windows.net/{TENANT-ID}/ az Azure AD esetén)
- `OAuth__ClientId`: Az identitásmező szolgáltató által kiosztott client ID
- `OAuth__ClientSecret`: Az identitásmező szolgáltató által kiosztott client secret
- `OAuth__DisplayName`: Opcionális megjelenítési név, amely az OIDC bejelentkezési gombon és az automatikus átirányítási oldalon jelenik meg (pl. `PocketID SSO`). Ha elhagyja, az általános "Login with OAuth" felirat kerül alkalmazásra.
- `OAuth__ClientScopes__0`: Az első kért scope az identitásmező szolgáltatótól (openid)
- `OAuth__ClientScopes__1`: A második kért scope az identitásmező szolgáltatótól (profile)
- `OAuth__ClientScopes__2`: A harmadik kért scope az identitásmező szolgáltatótól (email)
- `OAuth__ResponseMode`: Az engedélyezési kérés opcionális response mode (`query`, `fragment` vagy `form_post`). Az alapértelmezett érték `form_post`, az ASP.NET Core alapértelmezett beállítása. Állítsa `query` értékre olyan szolgáltatók esetén, amelyek nem támogatják az `form_post`-ot, mint például a Synology SSO Server. Tekintse meg a [Provider Compatibility](OIDC_Implementation.md#provider-compatibility) részt.
- `OAuth__AutoApproveUsers`: Az új OIDC felhasználók automatikus jóváhagyása manuális adminisztrátori jóváhagyás nélkül (true/false). Az alapértelmezett érték `false`.

#### Felhasználói Provisioning Beállítások {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Az új OIDC felhasználók automatikus jóváhagyása manuális adminisztrátori jóváhagyás nélkül (true/false). Az alapértelmezett érték `false`. Ha engedélyezve van, az OIDC szolgáltatón keresztül hiteles được felhasználók azonnal aktívvá válnak, és hozzáférhetnek az alkalmazáshoz. Ha letiltva van (alapértelmezett), az új OIDC felhasználók inaktív állapotban kerülnek létrevetésre, és adminisztrátori manuális aktiválást igényelnek. Részletes információért tekintse meg az [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users) részt.
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, stb.: E-mail címek, amelyek automatikusan adminisztrátorként kerülnek létrevetésre (provisioning). Azok a felhasználók, akik ezekkel az e-mail címekkel rendelkeznek, az első OAuth bejelentkezéskor aktív adminisztrátorokként kerülnek létrevetésre, megkerülve a normál jóváhagyási folyamatot. Az e-mail címek összehasonlítása nem érzékeny a kis- és nagybetűre.

#### Jelszó nélküli Bejelentkezési Beállítások {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: A felhasználónév/jelszó bejelentkezés letiltása (true/false). Az alapértelmezett érték `false`. Ha engedélyezve van, a jelszó mező elrejtésre kerül, **és** a szerver minden jelszó POST kérést elutasít, így csak az OAuth bejelentkezés áll rendelkezésre. `OAuth__Enabled=true` szükséges hozzá. Ha az OAuth letiltva van, ez a beállítás nem hatásos.
- `OAuth__AutoRedirect`: A felhasználók automatikus átirányítása az OAuth szolgáltatóhoz (true/false). Az alapértelmezett érték `false`. Ehhez szükséges, hogy a `OAuth__DisablePasswordLogin` értéke `true` legyen. A felhasználók egy rövid betöltési képernyőt fognak látni az átirányítás előtt.

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

Bár az alkalmazáshoz HTTPS-sel ellátott reverse proxy útján valóHozzáférés a cél, konfigurálhatja a Kestrel web szerverét SSL/TLS tanúsítványok használatára is. Ez végső-végig titkosítást biztosít a reverse proxy és az alkalmazás konténer között.

### Konfigurációs lépések {#configuration-steps}

1. **Generáljon vagy szerezzen be egy SSL tanúsítványt** PFX formátumban (pl. `localhost.pfx`)

2. **Adja hozzá az alábbi környezeti változókat** a `docker-compose.yml` fájljához a `mailarchive-app` szolgáltatáshoz:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Frissítse a portok térképezését** a `mailarchive-app` szolgáltatásban:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Adjon hozzá egy volume térképezést** a tanúsítványhoz:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Helyezze el a tanúsítvány fájlját** (pl. `localhost.pfx`) a gazdagélt rendszer `./certs` könyvtárában.

### Környezeti változók magyarázata {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: HTTP endpoint URL (alapértelmezett: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: HTTPS endpoint URL (alapértelmezett: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Az elérési út a PFX tanúsítvány fájljához a konténeren belül
- `Kestrel__Endpoints__Https__Certificate__Password`: Jelszó a PFX tanúsítvány fájljához

> 💡 **Megjegyzés**: Ez a konfiguráció opcionális. Ha HTTPS-sel ellátott reverse proxyt használ (ajánlott), akkor a reverse proxy és az alkalmazás közötti kommunikáció maradhat HTTP. Azonban érzékeny környezetben a maximális biztonság érdekében érdemes lehet a Kestrelen is engedélyezni a HTTPS-t az egész kommunikációs útvonal titkosítása érdekében.

## 🔑 Titkos adatok kezelése term उत्पादन környezetben {#secrets-management-for-production}

A biztonságérdekelésű adatok, például az adatbázis jelszavak, adminisztrátori hitelesítési adatok és az OAuth client secret értékek közvetlen hardkodálása a `docker-compose.yml` fájlban biztonsági kockázatot jelent, különösen, ha a fájl bekerül a verziókezelésbe. A javasolt megközelítés ezek az értékek külsőre helyezése egy `.env` fájlba, amelyet a Docker Compose automatikusan betölt.

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

**Fontos**: A Docker Compose automatikusan beolvassa a `.env` fájlt ugyanabból a mappából — **nem** szükséges manuálisan hivatkozni rá. A `.env` fájlban definiált összes változó elérhető a `docker-compose.yml` fájlban a `${VARIABLE_NAME}` szintaxis révén.

### 🐳 Alkalmazkodott `docker-compose.yml` {#adapted-docker-composeyml}

A telepítési lépésekben szereplő `docker-compose.yml`-at úgy kell módosítani, hogy ahelyett, hogy hardkodált titkos adatokat tartalmazna, helyőrzőket (placeholders) használjon:

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

Hozzon létre egy `.env.example` fájlt a tárhelyében (repository), amely sablonként szolgál más adminisztrátorok számára. Ez tartalmaz helyőrző értékeket és dokumentációt, de **nem tartalmaz valódi titkos adatokat**:

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
- **Használjon erős jelszavakat** — Generáljon hosszú, véletlenszerű jelszavakat (legalább 16 karakterrel), betűk, számok és speciális karakterek keverékével.
- **Korlátozza a `.env` fájl elérését** — Csak a Docker Compose-t futtató felhasználónak should legyen olvasási jogosultsága a `.env` fájlhoz.

## 🔒 Biztonsági megjegyzések {#security-notes}

- Használjon erős jelszavakat, és değiştirje a gyári hitelesítési adatokat. A jelszavak legalább 12 karakter hosszúak legyenek, és tartalmazzanak nagybetűket, kisbetűket, számokat és speciális karaktereket. Kerülje a gyakori szavak vagy könnyen megfogható információk használatát.
- Üret környékben fontoljanak meg HTTPS implementálását reverse proxy segítségével
- A PostgreSQL adatbázis rendszeres mentése javasolt. A részletes mentési és visszaállítási folyamatokért lásd a [Backup and Restore Guide](BackupRestore.md) dokumentációt.
