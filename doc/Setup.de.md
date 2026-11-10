# 🛠️ Mail Archiver Setup Guide {#mail-archiver-setup-guide}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden bietet detaillierte Anweisungen zur Einrichtung der Mail Archiver Anwendung unter Verwendung von Docker Compose.

## 🛠️ Voraussetzungen {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Installationsschritte {#installation-steps}

1. Installieren Sie die Voraussetzungen auf Ihrem System.

2. Erstellen Sie eine `docker-compose.yml`-Datei mit dem folgenden Inhalt:

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

3. Bearbeiten Sie die Datenbankkonfiguration in der `docker-compose.yml` und legen Sie ein sicheres Passwort in der Variable `POSTGRES_PASSWORD` und in der `ConnectionString` fest.

4. Wenn Sie Authentifizierung verwenden möchten (was dringend empfohlen wird), definieren Sie ein `Authentication__Username` und `Authentication__Password`, das für den Admin-Benutzer verwendet wird.

5. Passen Sie die Umgebungsvariable `TimeZone__DisplayTimeZoneId` an, damit sie Ihrer bevorzugten Zeitzone entspricht (Standard ist "Etc/UCT"). Sie können jeden IANA-Zeitzonenbezeichner verwenden (z. B. "Europe/Berlin", "Asia/Tokyo").

6. Konfigurieren Sie optional die Umgebungsvariablen `Logging__LogLevel`, um die Detailgenauigkeit der Anwendungsprotokolle zu steuern. Weitere Optionen finden Sie im Abschnitt „Logging Settings“ unten.

7. Konfigurieren Sie einen Reverse-Proxy Ihrer Wahl mit https und Authentifizierung, um den Zugriff auf die Anwendung abzusichern.

> ⚠️ **Achtung**: Die Anwendung selbst bietet keinen verschlüsselten Zugriff über https! Dies muss über einen Reverse-Proxy eingerichtet werden!

8. Erster Start der Container:
```bash
docker compose up -d
```

9. Container neu starten:
```bash
docker compose restart
```

10. Zugriff auf die Anwendung

11. Melden Sie sich mit Ihren definierten Anmeldedaten an und fügen Sie Ihr erstes E-Mail-Konto hinzu:
   - Navigieren Sie zum Abschnitt "Email Accounts"
   - Klicken Sie auf "New Account"
   - Geben Sie Ihre Serverdetails und Anmeldedaten ein
   - Speichern und Archivierung starten!
   - Wenn Sie möchten, erstellen Sie weitere Benutzer und weisen Sie Konten zu.

## 📚 Erklärungen zu Umgebungsvariablen {#environment-variable-explanations}

### 🗄️ Datenbankverbindung {#database-connection}
- `ConnectionStrings__DefaultConnection`: Die Verbindungszeichenfolge zur PostgreSQL-Datenbank. Ändern Sie die Werte `Host`, `Database`, `Username` und `Password` nach Bedarf.

### 🔐 Authentifizierungseinstellungen {#authentication-settings}
- `Authentication__Username`: Der Benutzername für das Admin-Konto.
- `Authentication__Password`: Das Passwort für das Admin-Konto.
- `Authentication__SessionTimeoutMinutes`: Der Sitzungs-Timeout in Minuten. Dies gilt für das nicht-persistente Login-Cookie; Benutzer, die im Login-Formular **Remember Me** auswählen, erhalten stattdessen ein persistentes Cookie, das für fest 7 Tage gültig ist. Siehe [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: Der Name des Authentifizierungs-Cookies.
- `Authentication__CookieSameSite`: Konfiguriert das SameSite-Attribut für Authentifizierungs-, Sitzungs- und CSRF-Schutz-Cookies. Gültige Werte sind:
  - `Strict` (Standard): Maximale Sicherheit. Cookies werden nur bei Same-Site-Anfragen gesendet. Dies kann Probleme verursachen, wenn man von externen Links zur Anwendung navigiert (z. B. durch Klicken auf einen Link von einer anderen Website), da die bestehende Sitzung nicht erkannt wird.
  - `Lax`: Empfohlen bei der Verwendung eines Reverse Proxy. Cookies werden bei Top-Level-Navigationen und Same-Site-Anfragen gesendet, sodass Benutzer externen Links zur Anwendung folgen können, während der CSRF-Schutz für POST-Anfragen erhalten bleibt.
  - `None`: Cookies werden mit allen Anfragen gesendet. Erfordert HTTPS und das `Secure`-Attribut. Verwenden Sie dies nur, wenn Sie spezifische Cross-Site-Anforderungen haben und die Sicherheitsimplikationen verstehen.

### 🔌 REST API Einstellungen {#rest-api-settings}
Die optionale schreibgeschützte REST API ist **standardmäßig deaktiviert**. Siehe den [REST API guide](API.md) für die vollständige Referenz.
- `Api__Enabled`: Hauptschalter für die schreibgeschützte REST API (Standard `false`). Wenn `false`, geben alle `/api/*`-Routen `404` zurück.
- `Api__AllowAttachmentDownloads`: Erlaubt das Herunterladen von Anhang-Bytes über die API (Standard `true`). Wenn `false`, gibt der Anhang-Endpunkt `403` zurück.
- `Api__AllowMessageDownloads`: Erlaubt das Herunterladen vollständiger Nachrichten über die API, sowohl der wiederaufgebauten `/eml` als auch der ursprünglichen `/raw` (Standard `true`). Wenn `false`, geben beide Endpunkte `403` zurück. Eine vollständige Nachricht enthält ihre Anhänge, daher müssen **sowohl** dieser Schalter als auch `Api__AllowAttachmentDownloads` auf `true` stehen; das Deaktivieren eines der beiden deaktiviert beide Endpunkte.
- `Api__EnableSwaggerUi`: Stellt die Swagger UI unter `/apidocs` und das OpenAPI-Dokument unter `/apidocs/spec/v1.json` bereit (Standard `true`). Beide erfordern eine angemeldete Browser-Sitzung.
- `Api__DefaultPageSize`: Standardmäßige Seitengröße für List-Endpunkte, wenn nicht angegeben (Standard `20`).
- `Api__MaxPageSize`: Maximal zulässige Seitengröße; größere Anfragen werden begrenzt (Standard `100`).
- `Api__RateLimitPerMinute`: Festes Zeitfenster-Anfragebudget pro API-Key pro Minute (Standard `120`).

### 🤖 MCP Server Einstellungen {#mcp-server-settings}
Der optionale MCP (Model Context Protocol) Server stellt KI-Agenten über den Streamable HTTP Transport denselben schreibgeschützten E-Mail-Zugriff wie die REST API bereit. Er ist **standardmäßig deaktiviert**. Weitere Informationen finden Sie im [MCP guide](MCP.md). MCP verwendet exakt dieselben API-Schlüssel wie die REST API (`ma_...` Bearer-Token) und dieselbe pro Benutzer geltende Konto-Zugriffsbeschränkung.
- `Mcp__Enabled`: Hauptschalter für den MCP Server (Standard `false`). Wenn `false`, gibt der `/mcp` Endpunkt `404` zurück.
- `Mcp__AllowAttachmentDownloads`: Erlaubt dem `get_attachment` Tool, Anhang-Bytes zurückzugeben (Standard `true`). Wenn `false`, gibt das Tool einen Fehler zurück.
- `Mcp__DefaultPageSize`: Standardmäßige Seitengröße, die von `search_emails` verwendet wird, wenn der Aufrufer `pageSize` weglässt (Standard `20`).
- `Mcp__MaxResults`: Obergrenze für die Seitengröße von `search_emails`; größere Werte werden begrenzt (Standard `100`).
- `Mcp__MaxAttachmentBytes`: Maximale Anhanggröße (in Bytes), die das `get_attachment` Tool inline als base64 zurückgibt; größere Anhänge werden abgelehnt (Standard `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Erlaubt den `get_email_eml` und `get_email_raw` Tools, vollständige Nachrichten zurückzugeben (Standard `true`). Wenn `false`, geben die Tools einen Fehler zurück. Eine vollständige Nachricht enthält ihre Anhänge, daher müssen **sowohl** dieser Schalter als auch `Mcp__AllowAttachmentDownloads` auf `true` stehen; das Deaktivieren eines der beiden deaktiviert beide Tools.
- `Mcp__MaxMessageBytes`: Maximale Nachrichtengröße (in Bytes), die die `get_email_eml` und `get_email_raw` Tools inline zurückgeben; größere Nachrichten werden abgelehnt (Standard `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Festes Zeitfenster-Anfragebudget pro API-Schlüssel pro Minute (Standard `120`).

### 📨 MailSync Einstellungen {#mailsync-settings}
- `MailSync__IntervalMinutes`: Das Intervall in Minuten zwischen der E-Mail-Synchronisierung. Dies ist der globale Standardwert; jedes Konto kann diesen individuell auf der Erstellen/Bearbeiten-Seite überschreiben (leer lassen, um diesen Standardwert zu verwenden).
- `MailSync__FullSyncIntervalHours`: Optionaler globaler Standardwert für automatische vollständige Resynchronisierungen, in Stunden. Wenn nicht festgelegt (Standard), wird keine automatische vollständige Synchronisierung ausgeführt, es sei denn, es ist ein `FullSyncIntervalHours`-Wert pro Konto auf der Erstellen/Bearbeiten-Seite eingestellt. Werte pro Konto überschreiben diesen globalen Standardwert.
- `MailSync__TimeoutMinutes`: Zeitüberschreitung der Synchronisierung pro Konto in Minuten. Eine Synchronisierung, die länger läuft, stoppt an der nächsten Nachrichten-Grenze, behält ihre Checkpoints bei und wird beim nächsten Durchlauf fortgesetzt; der Auftrag wird als `Timed Out` gemeldet und `LastSync` wird nicht fortgeschritten. Gilt nur für geplante Synchronisierungen; eine über die Benutzeroberfläche gestartete Synchronisierung (manuelle Synchronisierung oder vollständige Resynchronisierung) läuft ohne Zeitüberschreitung. `0` (Standard) oder jeder nicht-positive Wert bedeutet keine Zeitüberschreitung. Überprüfen Sie den Wert vor einem Upgrade, wenn Sie große Postfächer synchronisieren. Siehe [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Die Verbindungszeitüberschreitung für IMAP-Verbindungen in Sekunden.
- `MailSync__CommandTimeoutSeconds`: Die Befehlszeitüberschreitung für IMAP-Befehle in Sekunden.
- `MailSync__AlwaysForceFullSync`: Ob immer eine vollständige Synchronisierung erzwungen werden soll (true/false).
- `MailSync__IgnoreSelfSignedCert`: Ob selbstsignierte Zertifikate zu ignorieren sind (true/false).
- `MailSync__MaxConcurrentSyncs`: Wie viele Konto-Synchronisierungen gleichzeitig laufen dürfen. Standard `1` (sequenziell, abwärtskompatibel). Der Scheduler füllt Slots auf, sobald sie frei werden, anstatt auf einen kompletten Batch zu warten, sodass ein langsames Postfach nicht mehr die anderen aufhält. Erhöhen Sie diesen Wert, um mehrere Konten gleichzeitig zu synchronisieren — beachten Sie dabei die Rate-Limits der Anbieter und die lokale Ressourcennutzung. Siehe [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Optionale zeitliche Verzögerung in Sekunden, die am Ende jeder Konto-Synchronisierungsaufgabe angewendet wird. Standard `0` (keine Verzögerung). Nützlich, um Burst-Starts zu vermeiden, wenn `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Wie viele Probleme jeder Art ein Synchronisierungsauftrag für die Kontoseite speichert. Fehlgeschlagene Ordner, fehlende Ordner und fehlgeschlagene Nachrichten werden separat budgetiert, sodass eine Flut einer Art nicht die anderen aus der Ansicht verdrängen kann; alles, was über das Budget hinausgeht, wird gezählt, anstatt gespeichert zu werden. Standard `20`. `0` schaltet die Details aus und lässt nur die Zähler übrig. Siehe [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Ordner, die für **jedes** Konto von der Synchronisierung ausgeschlossen sind, zusätzlich zur jeweiligen Liste der ausgeschlossenen Ordner des Kontos. Standardmäßig leer, sodass bestehende Setups unbeeinflusst bleiben. Die beiden Listen sind **additiv** — ein Ordner wird übersprungen, wenn er mit einer der Listen übereinstimmt — und beide verwenden dieselben Matching-Regeln: exakte Übereinstimmung des vollständigen Pfads, exakte Übereinstimmung des Ordnernamens, eine Pfad-Suffix-Übereinstimmung (so passt `Drafts` auch auf `INBOX/Drafts` und `INBOX.Drafts`) und alles unterhalb des Ordners, den ein Eintrag benennt, wenn `MailSync__ExcludeSubfolders` eingeschaltet ist. Der Abgleich erfolgt nicht case-sensitiv. Nützlich beim Importieren vieler Postfächer vom selben Server, wo die Alternative darin bestünde, auf jedem Konto eine identische Ausschlussliste zu pflegen. Standardmäßig wird kein Ordner ausgeschlossen; welche Namen es wert sind, aufgelistet zu werden, hängt vom Server und dessen Sprache ab. Beispiel für einen Postfachbaum, der auch Kalender- und Kontaktordner enthält:
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
- `MailSync__ExcludeSubfolders`: Ob ein Ausschluss-Eintrag auch die Ordner unterhalb des benannten Ordners abdeckt. Dies gilt gleichermaßen für die Liste pro Konto und die installationsweite Liste sowie für beide Anbieter. Standard `false`, was dem Verhalten entspricht, bevor die Option existierte: Ein Eintrag `Deleted Items` passt nur auf diesen Ordner, und ein Baum wird vom Archiv ausgeschlossen, indem man dessen Wurzel und jeden darunter liegenden Ordner benennt. Setzen Sie dies auf `true`, damit ein Eintrag auch den darunter liegenden Baum übernimmt — ein Eintrag `Deleted Items` hält dann auch `Deleted Items/2024` und den Rest dieses Zweiges aus dem Archiv fern. Die Regel verankert sich am Pfadtrennzeichen, sodass sie ganze Ordner erfasst und niemals Teile eines Namens: `Archive` deckt `Archive` und `Archive/Team` ab und lässt einen Mail-Ordner namens `Team Archive Review` unberührt. Ein `.` zählt ebenfalls als Trennzeichen, so wie die Pfad-Suffix-Regel ihn behandelt.

### 📧 Einstellungen zur Speicherung der Originalnachricht {#original-message-storage-settings}

Standardmäßig bewahrt das Archiv nur die analysierten Teile einer E-Mail auf, sodass ein exportiertes `.eml` eine Rekonstruktion ist: Transfer-Kodierungen, MIME-Grenzen und Header-Folding unterscheiden sich von der empfangenen Nachricht, und DKIM-Signaturen lassen sich nicht mehr verifizieren. Mit `RawMessages:Enabled` wird das vollständige Original ebenfalls gespeichert.

| Parameter | Standard | Zweck |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Speichert das Original jeder neu archivierten E-Mail. |
| `RawMessages__Storage` | `postgres` | Wo die Bytes liegen: `postgres` oder `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Größere Nachrichten werden ohne ihr Original archiviert. `0` = kein Limit. |
| `RawMessages__IncludeGraph` | `true` | Speichert Originale auch für Microsoft 365 (Graph) Konten. |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Verzeichnis für die Nachrichten-Dateien. Muss auf ein gemountetes Volume in Docker verweisen. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Verzeichnisebenen, über die die E-Mail-ID verteilt wird. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Überprüft den SHA-256 bei jedem Download. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Entfernt Nachrichten-Dateien, auf die keine E-Mail verweist. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Wie oft dieser Bereinigungsprozess läuft. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Dateien, die jünger als dieser Wert sind, werden niemals entfernt. |

!!! warning "Filesystem backend benötigt ein gemountetes Volume"
    Mit `RawMessages__Storage=filesystem` liegen die Nachrichten-Dateien außerhalb der Datenbank.
    Ohne einen Bind-Mount liegen sie in der beschreibbaren Ebene des Containers und gehen bei der
    nächsten Container-Rekonstruktion verloren, während die Datenbankzeilen erhalten bleiben. Sichern Sie das Verzeichnis
    zusammen mit dem Datenbank-Dump.

Siehe **[Original Message Storage](RawMessages.md)** für die vollständige Beschreibung: wo das
Original bereitgestellt wird, die Storage-Backends, wie man bestehende Nachrichten mit
`--migrate-raw-storage` zwischen ihnen verschiebt und was gesichert werden muss.

### 📤 BatchRestore Einstellungen {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Die Anzahl der E-Mails, die eine asynchrone Verarbeitung auslöst.
- `BatchRestore__MaxSyncEmails`: Die maximale Anzahl an E-Mails für die synchrone Verarbeitung.
- `BatchRestore__MaxAsyncEmails`: Die maximale Anzahl an E-Mails für die asynchrone Verarbeitung.
- `BatchRestore__SessionTimeoutMinutes`: Das Sitzungs-Timeout für die Batch-Wiederherstellung in Minuten.
- `BatchRestore__DefaultBatchSize`: Die Standard-Batch-Größe für E-Mail-Operationen.

### 🏢 Tenant Management Einstellungen {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Maximale Anzahl an Postfächern, die in einem einzigen Tenant Management Vorgang hinzugefügt werden können. Standardwert ist `1000`. Erhöhen Sie diesen Wert für sehr große Mandanten oder senken Sie ihn, um versehentliche Massenimporte zu verhindern. Wenn das Limit überschritten wird, wird der Vorgang mit einem Validierungsfehler abgelehnt und es werden keine Konten erstellt. Weitere Details finden Sie im [M365 Tenant Import Guide](M365TenantImport.md).

### 🐄 Mailcow-Einstellungen {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Maximale Anzahl an Postfächern, die in einem einzigen Mailcow-Aktivierungslauf bereitgestellt werden können. Standard ist `1000`. Verringern Sie diesen Wert, um eine versehentliche Massenbereitstellung zu verhindern, oder erhöhen Sie ihn für große Instanzen. Wenn das Limit überschritten wird, werden keine Konten erstellt. Weitere Details finden Sie unter [Mailcow Instances](Mailcow.md).
- Die Mailcow-Integration erfordert zusätzlich `Security__CredentialEncryptionKey` (siehe [Security Settings](#-security-settings)). Das Verbinden einer Instanz und das Aktivieren von Postfächern werden beides abgelehnt, solange die Verschlüsselung der Anmeldedaten deaktiviert ist, sodass der API-Key und die generierten App-Passwörter niemals im Klartext gespeichert werden können.
- Ein ausgehender Zugriff auf die Mailcow HTTPS API und auf den IMAP-Host über TLS (normalerweise Port `993`) ist erforderlich.

### 📦 BatchOperation Einstellungen {#batchoperation-settings}
- `BatchOperation__BatchSize`: Die Batch-Größe für E-Mail-Operationen.
- `BatchOperation__PauseBetweenEmailsMs`: Die Pause zwischen einzelnen E-Mails in Millisekunden.
- `BatchOperation__PauseBetweenBatchesMs`: Die Pause zwischen Batches in Millisekunden.

### 📤 Offload-Einstellungen {#offload-settings}
Einstellungen für den [date-windowed offload](Offload.md). Alle Standardwerte reproduzieren das Verhalten, das die Anwendung vor der Existenz dieser Funktion hatte, sodass eine Installation, die diesen Abschnitt nicht konfiguriert, nicht beeinträchtigt wird.
- `Offload__MaxConcurrentJobs`: Wie viele restore- oder offload-Jobs gleichzeitig laufen dürfen. Standard ist `1`, was die strikt serielle Verarbeitung beibehält, die die Job-Warteschlange schon immer verwendet hat. Unabhängig von diesem Wert läuft maximal ein Job pro Ziel-Postfach.
- `Offload__PrefetchMaxMessages`: Obergrenze für die Anzahl der Nachrichten, die aus einem Ziel-Postfach zur Deduplizierung indiziert werden. Standard ist `500000`. Oberhalb dieses Wertes wird die Prüfung auf einen einzelnen Ordner eingegrenzt und protokolliert, dass der Umfang reduziert wurde.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Quell-Ordner, die niemals offloaded werden, vor dem Umbenennen abgeglichen werden und Unterordner abdecken. Standardmäßig leer.
- `Offload__FolderRenameMap__<SourceFolder>`: Schreibt die führenden Segmente eines Quell-Ordnerpfads um, zum Beispiel `Offload__FolderRenameMap__Sent Items=Sent`. Standardmäßig leer.
- `Offload__MarkAsSeen`: Ob angehängte E-Mails als gelesen markiert werden. Standard ist `true`, was dem bestehenden restore-Verhalten entspricht.

Beide Ordner-Einstellungen werden absichtlich leer ausgeliefert: Das Umschreiben oder Löschen von Ordnern ohne Aufforderung würde jeden überraschen, der bereits den restore-Pfad nutzt. Ein Durchlauf ohne Konfiguration migriert daher alles, einschließlich Spam-Ordner, und erstellt einen zweiten Satz spezieller Ordner neben den eigenen des Ziels.

### 📊 Bandwidth Tracking Settings {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Aktivieren oder deaktivieren Sie das Bandbreiten-Tracking für das IMAP Rate Limit Handling (true/false). Standard ist `false`. Wenn aktiviert, verfolgt das System die Bandbreitennutzung pro Konto und kann die Synchronisierung pausieren, wenn die Limits des Anbieters erreicht werden. Weitere Informationen finden Sie unter [Rate Limit Handling](RateLimitHandling.md).
- `BandwidthTracking__DailyLimitMb`: Tägliches Download-Limit in Megabyte pro Konto. Standard ist `25000` (25 GB). Bei Anbietern mit Bandbreitenbeschränkungen stellen Sie diesen Wert so ein, dass er deren Rate Limit entspricht (z. B. `2500` für Anbieter mit ~2500 MB täglichen Limits). Das System pausiert die Synchronisierung, wenn dieses Limit erreicht wird.
- `BandwidthTracking__WarningThresholdPercent`: Prozentsatz des täglichen Limits, bei dem Warnmeldungen protokolliert werden. Standard ist `80`. Wenn die Bandbreitennutzung diesen Prozentsatz erreicht, werden Warnungen protokolliert, um die Annäherung an die Limits zu überwachen.
- `BandwidthTracking__PauseHoursOnLimit`: Anzahl der Stunden, um die Synchronisierung zu pausieren, wenn das tägliche Limit erreicht ist. Standard ist `24`. Nach diesem Zeitraum wird das Limit-Flag automatisch gelöscht und die Synchronisierung wird fortgesetzt.
- `BandwidthTracking__TrackUploadBytes`: Ob auch die Upload-Bandbreite verfolgt werden soll (true/false). Standard ist `false`. Die meisten IMAP-Anbieter beschränken nur Downloads, daher ist dies in der Regel nicht erforderlich.

### 🎯 Auswahl-Einstellungen {#selection-settings}
- `Selection__MaxSelectableEmails`: Die maximale Anzahl an E-Mails, die gleichzeitig ausgewählt werden können.

### 👁️ Ansichtseinstellungen (Datenschutz & Anzeige) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Steuert den Standard-E-Mail-Ansichtsmodus für Datenschutz und Tracking-Prävention (true/false). Standard ist `false`.
  - Wenn auf `true` gesetzt: E-Mails werden standardmäßig in der Nur-Text-Ansicht geöffnet, was das automatische Laden von Tracking-Pixeln, externen Bildern und Web-Beacons verhindert. Dies wird für Benutzer empfohlen, die Wert auf E-Mail-Tracking und Datenschutz legen.
  - Wenn auf `false` gesetzt: E-Mails werden standardmäßig in der HTML-Ansicht geöffnet, wobei die formatierte E-Mail mit Bildern und Styling angezeigt wird.
  - Benutzer können über eine Schaltfläche auf der E-Mail-Detailseite zwischen HTML- und Nur-Text-Ansichten umschalten (nur sichtbar, wenn beide Formate verfügbar sind).
  - Der Link „Vollansicht“ berücksichtigt den aktuell ausgewählten Ansichtsmodus.

- `View__BlockExternalResources`: Blockiert externe Ressourcen (Remote-Bilder, externes CSS, externe Skripte, Web-Fonts usw.) in HTML-E-Mail-Ansichten, um Tracking zu verhindern und den Datenschutz zu verbessern (true/false). Standard ist `false`.
  - Wenn auf `true` gesetzt: Externe Ressourcen werden beim Anzeigen von HTML-E-Mails herausgefiltert. Es werden nur Inline-Inhalte und Data-URIs (einschließlich Inline-Anhänge über `cid:`-Referenzen) angezeigt.
  - Wenn auf `false` gesetzt: HTML-E-Mails werden mit all ihren ursprünglichen externen Ressourcen angezeigt.
  - **Wichtig**: Diese Einstellung beeinflusst nur die E-Mail-**Anzeige**. Archivierte E-Mails werden vollständig unverändert in der Datenbank gespeichert, wobei alle ursprünglichen Inhalte erhalten bleiben.
  - Zu den blockierten Ressourcen gehören:
    - Remote-Bilder (Tracking-Pixel, auf Servern gehostete externe Bilder)
    - Externe CSS-Stylesheets
    - Externe Schriftarten über @font-face
    - Externe CSS-Imports über @import
    - Externe Hintergrundbilder
  - Zulässige Ressourcen:
    - Eingebettete Inline-Bilder als data: URIs
    - Inline-Anhänge, die über cid: URIs referenziert werden
    - Inline-CSS-Styles und Style-Tags
  - Diese Einstellung arbeitet unabhängig von `DefaultToPlainText` und bietet eine zusätzliche Ebene des Datenschutzes beim Betrachten von HTML-E-Mails.

### 📊 Dashboard-Einstellungen {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Ob die Counter-Karten ihre eingehenden und ausgehenden Teile enthalten und die Konto-Karte die Anzahl der unterschiedlichen Domains. Standard ist `true`. Setzen Sie dies auf `false`, dann werden diese Zahlen nicht berechnet, sondern berechnet und ausgeblendet: Die Counter lesen eine einfache Anzahl, und die Anzahl der Anhänge wird nicht mehr mit der Mail verknüpft, an der sie hängt, was die einzige Abfrage ist, die diese Teile hinzufügen.
- `Dashboard__SelectablePeriods`: Ob die Dashboard-Diagramme eine Auflösung, einen Zeitraum und Pfeile zum Bewegen anbieten. Standard ist `true`. Setzen Sie dies auf `false`, dann zeigen die Diagramme die letzten zwölf Monate nach Monat und die Absender des gesamten Archivs; der Chart-Endpoint antwortet so, als ob er nicht existierte, und das älteste Sendedatum wird nicht nachgeschlagen.
- `Dashboard__CacheSeconds`: Wie lange berechnete Dashboard-Statistiken (Gesamtsummen mit ihren eingehenden und ausgehenden Aufteilungen, pro Konto gezählte Werte, die Chart-Serien, Top-Absender, aktuelle E-Mails, Datenbankgröße) im In-Memory-Cache des Servers behalten werden. Standard ist `60` Sekunden. Setzen Sie dies auf `0`, um das Caching zu deaktivieren und die Statistiken immer neu zu berechnen. Jede Chart-Auswahl von Auflösung und Zeitraum wird einzeln gecacht, sodass ein Dashboard, das zwischen Auswahlen wechselt, einen Eintrag pro Auswahl hält. Höhere Werte reduzieren die Datenbanklast in großen Umgebungen auf Kosten veralteter Zahlen. Sync-Status-Badges, Speicherwerte und die Liste der aktuellen E-Mails werden immer live abgerufen und sind von diesem Cache nicht betroffen.
- `Dashboard__RefreshIntervalMinutes`: Wie oft der `DashboardStatsRefreshService` Hintergrunddienst die aufwendigen Dashboard-Aggregate (Gesamtzahlen mit ihren eingehenden und ausgehenden Aufteilungen, pro Konto gezählte Werte, die Anzahl der Konto-Domains, die Standard-Chart-Serie mit ihren Top-Absendern, Datenbankgröße) in die `DashboardStatsCache` Datenbanktabelle neu berechnet. Bei Installationen mit mehreren Millionen Mails liest das Dashboard diese vorbereiteten Werte, anstatt das gesamte Archiv im Request-Pfad zu aggregieren, was einen ~10-sekündigen ersten Ladevorgang in Millisekunden verwandelt. Der Chart-Endpoint beantwortet die Standardauswahl (Monate über ein Jahr, eingehende Absender, der aktuelle Zeitraum) aus derselben Zeile, sodass die Rückkehr dazu die Zahlen des ersten Paints anzeigt; jede andere Auswahl wird bei Anfrage berechnet und im oben genannten In-Memory-Cache behalten. Standard ist `15` Minuten. Setzen Sie dies auf `0`, um die Hintergrundaktualisierung zu deaktivieren; das Dashboard berechnet die Statistiken dann wieder live (wobei der oben genannte In-Memory-Cache weiterhin Anwendung findet). Die angezeigten Werte können dem tatsächlichen Archivzustand bis zum konfigurierten Intervall hinterherhinken. Eine Zeile, die mit anderen `ShowDirectionSplits` oder `SelectablePeriods` Werten als den aktuellen berechnet wurde, wird nicht verwendet; das Dashboard berechnet live bis zur nächsten Aktualisierung.

### 🔄 Jobs Settings {#jobs-settings}
- `Jobs__RefreshSeconds`: Wie oft die Seite für Hintergrund-Jobs neu geladen wird, während ein Browser-Tab sie geöffnet hat. Standard ist `30` Sekunden. Setzen Sie den Wert auf `0`, um das automatische Neuladen zu deaktivieren und manuell zu aktualisieren. Jeder Reload baut die Seite aus allen acht Job-Quellen neu auf, daher hält ein längeres Intervall bei Installationen mit vielen Konten die Last niedrig, multipliziert mit jedem Tab, der auf der Seite geöffnet ist.

### 🗃️ Npgsql Einstellungen {#npgsql-settings}
- `Npgsql__CommandTimeout`: Das Timeout für Datenbankbefehle in Sekunden.

### 📥 Upload-Einstellungen {#upload-settings}
- `Upload__MaxFileSizeGB`: Die maximale Dateigröße für Uploads in GB.
- `Upload__KeepAliveTimeoutHours`: Das Keep-Alive-Timeout für Uploads in Stunden.
- `Upload__RequestHeadersTimeoutHours`: Das Timeout für Request-Header in Stunden.

### 📂 Lokale Import-Einstellungen {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, etc.: Whitelist lokaler Verzeichnisse, aus denen die CLI-Importbefehle (`--import-mbox`, `--import-eml`) Dateien lesen dürfen. Jeder Eintrag ist ein Pfad innerhalb des Containers. Sie müssen Ihre Importdateien mithilfe von Docker-Volumes in eines dieser Verzeichnisse mounten. Standard: `/data/import` (automatisch auf `/app/uploads` als Fallback gesetzt).
  - Wenn Sie `docker exec` verwenden, um den Importbefehl auszuführen, muss der über `--file` angegebene Dateipfad innerhalb eines dieser zulässigen Pfade liegen.
  - Diese Sicherheitsmaßnahme verhindert den beliebigen Dateisystemzugriff durch CLI-Befehle.
  - Es können mehrere Pfade für verschiedene Importquellen konfiguriert werden.
  - Weitere Informationen zur Verwendung finden Sie unter [CLI commands](CLI.md).

### 📄 CSV-Import-Einstellungen {#csv-import-settings}
- `CsvImport__MaxRows`: Maximale Anzahl der in einem einzelnen Massenimport verarbeiteten CSV-Zeilen (Postfächer). Standard ist `5000`. Erhöhen Sie diesen Wert für große Implementierungen; senken Sie ihn, um die Auswirkungen eines einzelnen Importlaufs auf die Datenbanklast zu begrenzen.
- `CsvImport__MaxFileSizeBytes`: Maximale erlaubte Größe (in Bytes) der hochgeladenen CSV-Datei. Standard ist `10000000` (10 MB). Passen Sie diesen Wert bei Bedarf an Ihre Upload-Limits an.
- Weitere detaillierte Anweisungen zur Verwendung des Massenimports von IMAP-Konten via CSV finden Sie im [Account Import Guide](Account-Import.md).

### 📤 Audit Export Settings {#audit-export-settings}
Einstellungen für die Seite zum Export der Audit-Daten (nur für Administratoren, erreichbar von der Logs-Seite). Der Export generiert ein ZIP-Paket mit tabellarischen Massendaten (INDEX.XML + CSV-Tabellen + DTD) aus dem bestehenden Archiv für externe Audit-Tools. Weitere Details zur Verwendung finden Sie im [Audit Data Export Guide](AuditExport.md).
- `AuditExport__DataSupplierName`: Standardwert für das Formularfeld "Data supplier name", das Ihre Organisation in der exportierten Indexdatei identifiziert. Standard: leer.
- `AuditExport__DataSupplierLocation`: Standardwert für das Formularfeld "Data supplier location" (z. B. Unternehmenssitz). Standard: leer.
- `AuditExport__Comment`: Standardwert für das Formularfeld "Comment", das als Freitext in der exportierten Indexdatei enthalten ist. Standard: leer.
- `AuditExport__OutputDirectory`: Verzeichnis, in dem die generierten ZIP-Dateien gespeichert werden. Relative Pfade werden relativ zum App-Content-Root aufgelöst. Standard: `exports/audit`.
- `AuditExport__RetentionDays`: Anzahl der Tage, nach denen abgeschlossene Exportdateien durch die tägliche Bereinigung gelöscht werden. Standard: `30`.
- `AuditExport__MaxRangeYears`: Maximal zulässiger Zeitraum zwischen dem Start- und Enddatum des Exportzeitraums. Standard: `10`.
- Die Formularfelder sind mit diesen Standardwerten vorausgefüllt, können aber pro Export bearbeitet werden.
- Jeder Export schreibt einen Start-Eintrag und einen Ergebnis-Eintrag in das Audit-Protokoll (Typ "Audit Data Export"), sodass der Verlauf ohne Änderungen am DB-Schema revisionssicher ist.

### 🔒 Deletion Policy Einstellungen {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Steuert, ob das manuelle Löschen von archivierten E-Mails erlaubt ist (true/false). Standard ist `true`. Wenn auf `false` gesetzt:
  - Alle archivierten E-Mails werden beim Start über den Datenbank-Compliance-Trigger gesperrt (`IsLocked = true`), was jegliche Änderung oder Löschung auf Datenbankebene verhindert.
  - Das manuelle Löschen (einzeln und in Massen) wird auf Anwendungsebene mit einer Fehlermeldung blockiert.
  - Der Spalten-Standardwert wird so angepasst, dass auch neu importierte E-Mails gesperrt werden.
  - Der aktuelle Richtlinienstatus wird bei jedem Start zur Revisionssicherheit in der AccessLogs-Tabelle protokolliert (auf der Logs-Seite als „Deletion Policy“-Einträge sichtbar).
  - Die lokale Löschung aufgrund der Aufbewahrung ist ausgenommen: E-Mails, die unter einen konfigurierten Aufbewahrungszeitraum fallen, werden weiterhin gelöscht (sie werden innerhalb des Aufbewahrungsprozesses unmittelbar vor dem Löschen entsperrt).
- `DeletionPolicy__MaxBulkDeleteEmails`: Obergrenze für eine filterbasierte Massenlöschung (*Delete all matches*). Standard ist `100000`. Ein Filter, der auf mehr E-Mails zutrifft, wird mit einer Meldung abgelehnt, die dazu auffordert, den Filter zu verfeinern; der Job wird niemals stillschweigend gekürzt. E-Mails, die durch die Aufbewahrungsrichtlinie gesperrt sind, werden übersprungen und im Job-Status gemeldet, anstatt sie zu entsperren. Der Job prüft diesen Grenzwert und `DeletionPolicy__DeletionAllowed` erneut, wenn er startet, sodass ein vor einer Richtlinienänderung in die Warteschlange gestellter Job stoppt, anstatt mit der alten Einstellung zu laufen.
- **Immutability-Schutz:** Wenn `IsLocked = true`, blockiert der Datenbank-Compliance-Trigger (`prevent_locked_email_changes`) JEDE Änderung an der E-Mail-Zeile — alle Spalten sind geschützt, nicht nur eine feste Liste von Feldern. Die einzigen ausgenommenen Spalten sind `IsLocked` selbst (damit das Entsperren für die Löschung aufgrund der Aufbewahrung und die Anwendung der Richtlinie beim Start weiterhin möglich bleibt) und `FolderName` (damit die IMAP-Synchronisierung den Ordner aktualisieren kann, wenn eine E-Mail serverseitig verschoben wird). Der Schutz ist spaltenunabhängig (JSONB-basierter Vergleich) und deckt automatisch zukünftige Schema-Erweiterungen ab.

### 🕐 Zeitzonen-Einstellungen {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Die Zeitzone, die zur Anzeige von E-Mail-Zeitstempeln in der Benutzeroberfläche verwendet wird. Verwendet IANA-Zeitzonenbezeichner (z. B. "Europe/Berlin", "Asia/Tokyo"). Standard ist "Etc/UCT" zur Abwärtskompatibilität. Beim Importieren werden die Zeitstempel der E-Mails für die Anzeige in diese Zeitzone konvertiert.

### 🎉 ReleaseNotes Settings (Version Update Splash Screen) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: Der Versionsstring, den diese Instanz meldet. Er wird mit GitHub-Release-Tags abgeglichen und entscheidet, ob ein neueres Release existiert. Wenn dieser leer ist, fällt das Changelog auf die kompilierte Assembly-Version zurück und der Update-Hinweis bleibt deaktiviert.
- `ReleaseNotes__Enabled`: Aktiviert oder deaktiviert den Version Update Splash Screen (true/false). Standard ist `true`. Wenn aktiviert, sehen Administratoren nach einem Anwendungsupdate ein einmaliges Changelog-Modal, das die aus GitHub Releases abgerufenen Release Notes für die aktuelle Version anzeigt. Jeder Administrator kann das Modal schließen, und es erscheint nur bei einer neuen Version erneut. Setzen Sie dies auf `false`, um diese Funktion, einschließlich des Update-Hinweises, vollständig zu deaktivieren.
- `ReleaseNotes__UpdateHintEnabled`: Zeigt Administratoren einen kleinen Toast in der unteren rechten Ecke an, wenn GitHub ein neueres stabiles Release veröffentlicht als die installierte Version (true/false). Standard ist `true`. Der Toast verlinkt auf dieses Release und wird pro Version im Browser geschlossen. Entwürfe und Pre-Releases werden ignoriert. Erfordert `ReleaseNotes__AppVersion`; ohne diesen wird kein Hinweis angezeigt.
- `ReleaseNotes__UpdateCheckIntervalHours`: Wie oft die GitHub-Release-Liste aktualisiert wird, in Stunden. Standard ist `6`. Ein kürzeres Intervall macht ein neues Release in einer laufenden Instanz schneller sichtbar, ein längeres Intervall reduziert die GitHub-API-Aufrufe. Das Minimum ist `1`.

### 🔧 Datenbank-Wartungseinstellungen {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Automatische tägliche Datenbankwartung aktivieren oder deaktivieren (true/false). Standard ist `false`. Wenn aktiviert, führt das System automatisch VACUUM ANALYZE-Operationen aus, um die Datenbankleistung zu optimieren und Bloat zu verhindern. Weitere Details finden Sie im [Database Maintenance Guide](DatabaseMaintenance.md).
- `DatabaseMaintenance__DailyExecutionTime`: Die Tageszeit, zu der die Datenbankwartung ausgeführt werden soll, im 24-Stunden-Format (HH:mm). Standard ist `02:00`. Wählen Sie eine Zeit während geringer Systemaktivität.
- `DatabaseMaintenance__TimeoutMinutes`: Maximal zulässige Zeit für Wartungsoperationen in Minuten. Standard ist `30`. Erhöhen Sie diesen Wert für größere Datenbanken.

### 🧬 Attachment Deduplication Settings {#attachment-deduplication-settings}
Die Anhang-Deduplizierung speichert jede eindeutige Anhang-Nutzlast nur einmal (inhaltsadressiert durch SHA-256) und ist ein **Kernfeature, das immer aktiviert ist** – es gibt absichtlich keinen Ein/Aus-Schalter. Nur die unten aufgeführten Batch-/Zeitplanparameter können angepasst werden. Weitere Details finden Sie im [Attachment Deduplication Guide](AttachmentDeduplication.md).

- `AttachmentDeduplication__BatchSize`: Anzahl der pro Transaktion migrierten vorhandenen Anhänge während der einmaligen Hintergrundmigration bereits existierender Daten. Standard ist `200`. Größere Werte migrieren schneller, verursachen aber pro Batch mehr Arbeitsspeicher-/DB-Last.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Optionale Pause (in Millisekunden) zwischen Migrations-Batches, um die Datenbanklast auf ausgelasteten Systemen zu drosseln. Standard ist `0` (keine Pause).
- `AttachmentDeduplication__StartupDelaySeconds`: Verzögerung (in Sekunden) nach Anwendungsstart, bevor die Hintergrundmigration beginnt, damit die Schema-Migration abgeschlossen werden kann. Standard ist `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Intervall (in Stunden) der permanent laufenden Garbage Collection, die Anhang-Nutzlasten entfernt, die nicht mehr von einer E-Mail referenziert werden. Standard ist `12`. Dies läuft unabhängig von `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Datenbank-Befehls-Timeout (in Sekunden) für die Migrations-Batch-Operationen (INSERT mit SHA-256-Hashing und UPDATE). Standard ist `300` (5 Minuten). Erhöhen Sie diesen Wert für sehr große Datenbanken oder Anhänge, oder senken Sie ihn, wenn Sie eine schnellere Fehlererkennung wünschen. Wenn ein Batch weiterhin ein Timeout verursacht, versucht der Dienst den Vorgang automatisch mit der halben Batch-Größe erneut.

### 💾 Account-Speichereinstellungen {#account-storage-settings}
Die Anzeige des Speichers pro Konto zeigt die Datenbank-Speichernutzung (alle Mail-Felder + Anhänge) für jedes Konto in der Dashboard-Tabelle „Account Overview“ und der MailAccounts-Tabelle „Show All“ an. Ein autarker Hintergrunddienst (`AccountStorageRefreshService`) berechnet die Werte, indem er die PostgreSQL `pg_column_size` pro Spalte addiert (was alle Felder einer Mail abdeckt, ohne die Mail-Bodies zu lesen) und diese in der `AccountStorageCache`-Tabelle zwischenspeichert. Dieser Dienst läuft unabhängig von `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Aktivieren oder deaktivieren des Speicher-Refresh-Dienstes (true/false). Standard ist `true`. Wenn aktiviert, führt der Dienst beim Start einen fortsetzbaren Backfill der ausstehenden Konten durch (crash-sicher via `AccountStorageBackfillState`) und danach einen täglichen vollständigen Refresh. Wenn deaktiviert, werden die Speicherwerte nur aktualisiert, wenn E-Mails synchronisiert, importiert oder gelöscht werden, aber die angezeigten Werte können mit der Zeit veralten.
- `AccountStorage__DailyExecutionTime`: Tageszeit (24-Stunden-Format `HH:mm`) für den täglichen vollständigen Refresh aller Konten. Standard ist `02:30`. Wählen Sie eine Zeit während geringer Systemaktivität.
- `AccountStorage__BackfillDelayMs`: Verzögerung (in Millisekunden) zwischen den Konten während des initialen Backfills beim Start. Standard ist `5000`. Niedrigere Werte beschleunigen den Backfill, erhöhen aber die Datenbanklast; erhöhen Sie diesen Wert bei sehr großen Archiven, um eine Überlastung der Datenbank zu vermeiden.
- `AccountStorage__RefreshBatchDelayMs`: Verzögerung (in Millisekunden) zwischen den Konten während des täglichen vollständigen Refreshes. Standard ist `1000`. Niedrigere Werte beschleunigen den Refresh, erhöhen aber die Datenbanklast; erhöhen Sie diesen Wert bei sehr großen Archiven.
- `AccountStorage__CommandTimeoutSeconds`: Datenbank-Befehls-Timeout (in Sekunden) für die Berechnung des Speichers pro Konto (`pg_column_size` über alle Zeilen eines Kontos). Standard ist `300` (5 Minuten). Die Berechnung liest jede Mail-Zeile einschließlich TOAST-Daten, was bei sehr großen Archiven (250k+ E-Mails) weit über eine Minute dauern kann; der vorherige Npgsql-Standard von 30 Sekunden führte dazu, dass der Refresh lautlos fehlschlug und das Dashboard veraltete Werte anzeigte. Erhöhen Sie diesen Wert, wenn die Berechnung bei Ihrem Archiv immer noch in einen Timeout läuft.

> 💡 **Hinweis**: Speicherwerte werden sofort nach jeder Mail-Synchronisierung, jedem Import oder jeder Löschung aufgrund der Aufbewahrung aktualisiert, sodass die angezeigten Werte auch ohne den täglichen Refresh aktuell bleiben. Der tägliche Refresh dient als Sicherheitsnetz, um Grenzfälle (z. B. direkte Datenbankänderungen) abzufangen.

### 📝 Logging-Einstellungen {#logging-settings}
- `Logging__LogLevel__Default`: Die Standard-Log-Level für die Anwendung. Verfügbare Level sind: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Standard ist `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Log-Level für ASP.NET Core framework Nachrichten. Standard ist `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Log-Level für Entity Framework Datenbankbefehle. Standard ist `Warning`.

### 🛡️ Sicherheitseinstellungen {#security-settings}
- `AllowedHosts`: Eine durch Semikolon getrennte Liste von Hostnamen, die die Anwendung bedienen darf. Dies hilft, HTTP Host-Header-Angriffe zu verhindern. Beispiel: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Wichtig**: Verwenden Sie `*` nicht in Produktionsumgebungen, da dies die Host-Header-Validierung deaktiviert.

#### 🔐 Verschlüsselung von Anmeldedaten im Ruhezustand {#credential-encryption-at-rest}
Anmeldedaten für das Mail-Konto (IMAP-Passwort, M365 Client Secret und MSA OAuth-Token) können in der Datenbank verschlüsselt werden. Der Verschlüsselungsschlüssel wird **außerhalb** der Datenbank aufbewahrt (Umgebungsvariable / Docker Secret), sodass ein Datenbank-Dump allein nicht ausreicht, um die Anmeldedaten zu lesen.

- `Security__CredentialEncryptionKey`: Base64-kodierter 256-Bit (32 Byte) Key Encryption Key. Erzeugen Sie einen mit:
  ```bash
  openssl rand -base64 32
  ```
  Setzen Sie den Wert als Umgebungsvariable (empfohlen: `Security__CredentialEncryptionKey` in Ihrer Compose-Datei oder Container-Umgebung). Die Anwendung unterstützt keine Docker Secrets im `_FILE`-Stil — wenn Sie ein Docker Secret verwenden, mounten Sie es und laden Sie dessen Inhalt in die Umgebungsvariable in Ihrem Entrypoint. Committen Sie den Schlüssel niemals in `appsettings.json`. Wenn ein Schlüssel konfiguriert ist, werden Anmeldedaten beim Schreiben verschlüsselt, und ein Hintergrunddienst verschlüsselt einmalig vorhandene Anmeldedaten im Klartext.
- `Security__EncryptAccountCredentials`: Aktiviert oder deaktiviert die Verschlüsselung neuer/aktualisierter Anmeldedatenwerte (true/false). Standard ist `true`. Wenn kein Schlüssel konfiguriert ist, werden Anmeldedaten als Klartext gespeichert und beim Start eine Warnung protokolliert — bestehende Installationen sind nicht betroffen, bis ein Schlüssel bereitgestellt wird.
- `Security__CredentialEncryptionBackfill`: Ob der Hintergrunddienst beim Start vorhandene Anmeldedaten im Klartext verschlüsseln soll (true/false). Standard ist `true`. Ohne konfigurierten Schlüssel erfolgt keine Aktion.
- `Security__CredentialEncryptionKeyPrevious`: Optionaler vorheriger Schlüssel, der nur verwendet wird, um mit ihm verschlüsselte Werte zu entschlüsseln (für die Schlüsselrotation). Neue Werte werden immer mit `Security__CredentialEncryptionKey` verschlüsselt.

  > ⚠️ **Schlüsselrotation**: Bewahren Sie nach der Rotation `Security__CredentialEncryptionKeyPrevious` so lange auf, **bis jedes Anmeldedatum neu geschrieben wurde** (z. B. durch Bearbeiten des Kontos oder eine automatische OAuth-Token-Aktualisierung) — bestehende mit dem alten Schlüssel verschlüsselte Werte werden *nicht* automatisch mit dem neuen Schlüssel neu verschlüsselt. Sobald Sie den vorherigen Schlüssel entfernen, werden alle Anmeldedaten, die noch mit ihm verschlüsselt sind, unlesbar und müssen erneut eingegeben werden. Solange er konfiguriert ist, muss er genauso sicher wie der aktuelle Schlüssel aufbewahrt werden.

> ⚠️ **Bewahren Sie den Schlüssel sicher auf**: Ohne den Schlüssel können verschlüsselte Anmeldedaten nicht wiederhergestellt werden und müssen erneut eingegeben werden. Sichern Sie ihn zusammen mit (aber getrennt von) Ihren Datenbank-Sicherungen.
>
> 💡 **Bestehende Installationen**: Aktualisieren Sie sicher — solange kein `Security__CredentialEncryptionKey` gesetzt ist, ändert sich nichts. Setzen Sie den Schlüssel, wenn Sie bereit sind; bestehende Anmeldedaten im Klartext bleiben lesbar und werden im Hintergrund verschlüsselt.

### 🔐 OIDC-Konfiguration {#oidc-configuration}

Detaillierte Anweisungen zur Einrichtung der OpenID Connect-Authentifizierung finden Sie im [OIDC Implementation Guide](OIDC_Implementation.md).

#### Grundlegende OIDC-Einstellungen {#basic-oidc-settings}
- `OAuth__Enabled`: OIDC-Authentifizierung aktivieren oder deaktivieren (true/false)
- `OAuth__Authority`: Die OpenID Connect Authority-URL (z. B. https://sts.windows.net/{TENANT-ID}/ für Azure AD)
- `OAuth__ClientId`: Die vom Identity Provider zugewiesene Client ID
- `OAuth__ClientSecret`: Das vom Identity Provider zugewiesene Client Secret
- `OAuth__DisplayName`: Optionaler Anzeigename, der auf der OIDC-Login-Schaltfläche und der Auto-Redirect-Seite angezeigt wird (z. B. `PocketID SSO`). Wenn dieser weggelassen wird, wird die generische Bezeichnung „Login with OAuth“ verwendet.
- `OAuth__ClientScopes__0`: Erster vom Identity Provider angeforderter Scope (openid)
- `OAuth__ClientScopes__1`: Zweiter vom Identity Provider angeforderter Scope (profile)
- `OAuth__ClientScopes__2`: Dritter vom Identity Provider angeforderter Scope (email)
- `OAuth__ResponseMode`: Optionaler Response Mode der Autorisierungsanfrage (`query`, `fragment` oder `form_post`). Standard ist `form_post`, der ASP.NET Core Standard. Setzen Sie dies auf `query` für Anbieter, die `form_post` nicht unterstützen, wie z. B. den Synology SSO Server. Siehe [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Neue OIDC-Benutzer automatisch genehmigen, ohne dass eine manuelle Admin-Genehmigung erforderlich ist (true/false). Standard ist `false`.

#### Einstellungen zur Benutzerbereitstellung {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Neue OIDC-Benutzer automatisch genehmigen, ohne dass eine manuelle Admin-Genehmigung erforderlich ist (true/false). Standard ist `false`. Wenn aktiviert, werden Benutzer, die sich über den OIDC-Provider authentifizieren, sofort aktiviert und können auf die Anwendung zugreifen. Wenn deaktiviert (Standard), werden neue OIDC-Benutzer als inaktiv erstellt und erfordern eine manuelle Aktivierung durch einen Administrator. Weitere Informationen finden Sie unter [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users).
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, etc.: E-Mail-Adressen, die automatisch als Administratoren bereitgestellt werden sollen. Benutzer mit diesen E-Mail-Adressen werden beim ersten OAuth-Login als aktive Administratoren erstellt und umgehen den normalen Genehmigungsprozess. Der E-Mail-Abgleich erfolgt nicht case-sensitiv.

#### Einstellungen für passwortlosen Login {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Login mit Benutzername/Passwort deaktivieren (true/false). Standard ist `false`. Wenn aktiviert, wird das Passwortformular ausgeblendet **und** der Server lehnt jeden Passwort-POST ab, sodass nur der OAuth-Login verfügbar ist. Erfordert `OAuth__Enabled=true`. Wenn OAuth deaktiviert ist, hat diese Einstellung keine Auswirkung.
- `OAuth__AutoRedirect`: Benutzer automatisch zum OAuth-Provider weiterleiten (true/false). Standard ist `false`. Erfordert, dass `OAuth__DisablePasswordLogin` auf `true` gesetzt ist. Benutzer sehen einen kurzen Ladebildschirm, bevor sie weitergeleitet werden.

#### Beispiel: Vollständige OIDC-First-Konfiguration {#example-full-oidc-first-configuration}
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

## 🔐 Kestrel HTTPS-Konfiguration (Optional) {#kestrel-https-configuration-optional}

Obwohl die Anwendung über einen Reverse Proxy mit HTTPS aufgerufen werden soll, können Sie auch den Kestrel Webserver so konfigurieren, dass er SSL/TLS-Zertifikate verwendet. Dies bietet eine Ende-zu-Ende-Verschlüsselung zwischen dem Reverse Proxy und dem Anwendungscontainer.

### Konfigurationsschritte {#configuration-steps}

1. **Generieren oder beschaffen Sie ein SSL-Zertifikat** im PFX-Format (z. B. `localhost.pfx`)

2. **Fügen Sie die folgenden Umgebungsvariablen** zu Ihrem `docker-compose.yml` für den `mailarchive-app`-Dienst hinzu:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Aktualisieren Sie das Port-Mapping** im `mailarchive-app`-Dienst:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Fügen Sie ein Volume-Mapping** für das Zertifikat hinzu:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Platzieren Sie Ihre Zertifikatsdatei** (z. B. `localhost.pfx`) im Verzeichnis `./certs` auf Ihrem Host-System.

### Erklärungen der Umgebungsvariablen {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: HTTP-Endpunkt-URL (Standard: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: HTTPS-Endpunkt-URL (Standard: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Pfad zur PFX-Zertifikatsdatei innerhalb des Containers
- `Kestrel__Endpoints__Https__Certificate__Password`: Passwort für die PFX-Zertifikatsdatei

> 💡 **Hinweis**: Diese Konfiguration ist optional. Wenn Sie einen Reverse Proxy mit HTTPS verwenden (empfohlen), kann die Kommunikation zwischen dem Reverse Proxy und der Anwendung per HTTP erfolgen. Für maximale Sicherheit in sensiblen Umgebungen möchten Sie jedoch möglicherweise auch HTTPS auf Kestrel aktivieren, um den gesamten Kommunikationsweg zu verschlüsseln.

## 🔑 Secrets Management für die Produktion {#secrets-management-for-production}

Das Hardcodieren sensibler Daten wie Datenbankpasswörter, Admin-Anmeldedaten und OAuth-Client-Secrets direkt in `docker-compose.yml` stellt ein Sicherheitsrisiko dar, insbesondere wenn die Datei in die Versionsverwaltung übernommen wird. Der empfohlene Ansatz besteht darin, diese Werte in eine `.env`-Datei auszulagern, die von Docker Compose automatisch geladen wird.

### 📄 Die `.env`-Datei {#the-env-file}

Erstellen Sie eine `.env`-Datei im selben Verzeichnis wie Ihre `docker-compose.yml` mit dem folgenden Inhalt:

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

**Wichtig**: Docker Compose liest die `.env`-Datei automatisch aus demselben Verzeichnis – Sie müssen sie **nicht** manuell referenzieren. Alle in `.env` definierten Variablen sind in `docker-compose.yml` über die `${VARIABLE_NAME}`-Syntax verfügbar.

### 🐳 Angepasste `docker-compose.yml` {#adapted-docker-composeyml}

Die `docker-compose.yml` aus den Installationsschritten sollte so angepasst werden, dass Platzhalter anstelle von hardcodierten Secrets verwendet werden:

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

### 📋 `.env.example`-Vorlage für Administratoren {#envexample-template-for-administrators}

Erstellen Sie eine `.env.example`-Datei in Ihrem Repository, die als Vorlage für andere Administratoren dient. Sie enthält Platzhalterwerte und Dokumentation, aber **keine echten Secrets**:

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

- **Verwenden Sie `.env.example` als Vorlage**
- **Setzen Sie strikte Dateiberechtigungen** — Beschränken Sie den Zugriff auf die `.env`-Datei:
  ```bash
  chmod 600 .env
  ```
- **Verwenden Sie starke Passwörter** — Generieren Sie lange, zufällige Passwörter (mindestens 16 Zeichen) mit einer Mischung aus Buchstaben, Zahlen und Sonderzeichen.
- **Beschränken Sie den Zugriff auf die `.env`-Datei** — Nur der Benutzer, der Docker Compose ausführt, sollte Leseberechtigungen für die `.env`-Datei haben.

## 🔒 Sicherheitshinweise {#security-notes}

- Verwenden Sie starke Passwörter und ändern Sie Standard-Anmeldedaten. Passwörter sollten mindestens 12 Zeichen lang sein und eine Mischung aus Großbuchstaben, Kleinbuchstaben, Zahlen und Sonderzeichen enthalten. Vermeiden Sie die Verwendung von gängigen Wörtern oder leicht erratbaren Informationen.
- Erwägen Sie die Implementierung von HTTPS mit einem Reverse Proxy in der Produktionsumgebung.
- Regelmäßige Sicherungen der PostgreSQL-Datenbank werden empfohlen. Detaillierte Verfahren zur Sicherung und Wiederherstellung finden Sie im [Backup and Restore Guide](BackupRestore.md).
