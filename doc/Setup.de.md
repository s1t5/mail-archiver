# 🛠️ Mail Archiver Setup Guide {#mail-archiver-setup-guide}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden bietet detaillierte Anweisungen zur Einrichtung der Mail Archiver Anwendung unter Verwendung von Docker Compose.

## 🛠️ Voraussetzungen {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Installationsschritte {#installation-steps}

1. Installieren Sie die Voraussetzungen auf Ihrem System.

2. Erstellen Sie eine `docker-compose.yml`-Datei mit folgendem Inhalt:

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

3. Bearbeiten Sie die Datenbankkonfiguration in der `docker-compose.yml` und legen Sie ein sicheres Passwort in der `POSTGRES_PASSWORD`-Variable und der `ConnectionString` fest.

4. Wenn Sie Authentifizierung verwenden möchten (was dringend empfohlen wird), definieren Sie einen `Authentication__Username` und `Authentication__Password`, der für den Admin-Benutzer verwendet wird.

5. Passen Sie die `TimeZone__DisplayTimeZoneId`-Umgebungsvariable an Ihre bevorzugte Zeitzone an (Standard ist "Etc/UCT"). Sie können jeden IANA-Zeitzonenbezeichner verwenden (z. B. "Europe/Berlin", "Asia/Tokyo").

6. Konfigurieren Sie optional die `Logging__LogLevel`-Umgebungsvariablen, um die Detailgenauigkeit der Anwendungsprotokolle zu steuern. Weitere Informationen zu den verfügbaren Optionen finden Sie im Abschnitt „Logging Settings“ unten.

7. Konfigurieren Sie einen Reverse Proxy Ihrer Wahl mit https und Authentifizierung, um den Zugriff auf die Anwendung abzusichern.

> ⚠️ **Achtung**: Die Anwendung selbst bietet keinen verschlüsselten Zugriff über https! Dies muss über einen Reverse Proxy eingerichtet werden!

8. Erstmaliger Start der Container:
```bash
docker compose up -d
```

9. Container neu starten:
```bash
docker compose restart
```

10. Zugriff auf die Anwendung

11. Melden Sie sich mit Ihren definierten Zugangsdaten an und fügen Sie Ihr erstes E-Mail-Konto hinzu:
   - Navigieren Sie zum Abschnitt „Email Accounts“
   - Klicken Sie auf „New Account“
   - Geben Sie Ihre Serverdetails und Zugangsdaten ein
   - Speichern und mit der Archivierung beginnen!
   - Wenn Sie möchten, erstellen Sie andere Benutzer und weisen Sie Konten zu.

## 📚 Erklärungen zu Umgebungsvariablen {#environment-variable-explanations}

### 🗄️ Datenbankverbindung {#database-connection}
- `ConnectionStrings__DefaultConnection`: Der Verbindungsstring zur PostgreSQL-Datenbank. Ändern Sie die Werte `Host`, `Database`, `Username` und `Password` nach Bedarf.

### 🔐 Authentifizierungseinstellungen {#authentication-settings}

- `Authentication__Username`: Der Benutzername für das Admin-Konto.
- `Authentication__Password`: Das Passwort für das Admin-Konto.
- `Authentication__SessionTimeoutMinutes`: Der Sitzungs-Timeout in Minuten. Dies gilt für das nicht-persistente Login-Cookie; Benutzer, die im Login-Formular **Remember Me** auswählen, erhalten stattdessen ein persistentes Cookie, das für fest 7 Tage gültig ist. Siehe [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: Der Name des Authentifizierungs-Cookies.
- `Authentication__CookieSameSite`: Konfiguriert das SameSite-Attribut für Authentifizierungs-, Sitzungs- und CSRF-Schutz-Cookies. Gültige Werte sind:
  - `Strict` (Standard): Maximale Sicherheit. Cookies werden nur bei Same-Site-Anfragen gesendet. Dies kann Probleme verursachen, wenn man über externe Links zur Anwendung navigiert (z. B. durch Klicken auf einen Link von einer anderen Website), da die bestehende Sitzung nicht erkannt wird.
  - `Lax`: Empfohlen bei der Verwendung eines Reverse Proxys. Cookies werden bei Top-Level-Navigationen und Same-Site-Anfragen gesendet, sodass Benutzer externen Links zur Anwendung folgen können, während der CSRF-Schutz für POST-Anfragen aufrechterhalten wird.
  - `None`: Cookies werden mit allen Anfragen gesendet. Erfordert HTTPS und das `Secure`-Attribut. Verwenden Sie dies nur, wenn Sie spezifische Cross-Site-Anforderungen haben und die Sicherheitsimplikationen verstehen.

### 🔌 REST API Einstellungen {#rest-api-settings}
Die optionale schreibgeschützte REST API ist **standardmäßig deaktiviert**. Siehe den [REST API Guide](API.md) für die vollständige Referenz.
- `Api__Enabled`: Hauptschalter für die schreibgeschützte REST API (Standard `false`). Wenn `false`, geben alle `/api/*`-Routen `404` zurück.
- `Api__AllowAttachmentDownloads`: Erlaubt das Herunterladen von Anhang-Bytes über die API (Standard `true`). Wenn `false`, gibt der Anhang-Endpunkt `403` zurück.
- `Api__AllowMessageDownloads`: Erlaubt das Herunterladen vollständiger Nachrichten über die API, sowohl der neu aufgebauten `/eml` als auch der ursprünglichen `/raw` (Standard `true`). Wenn `false`, geben beide Endpunkte `403` zurück. Eine vollständige Nachricht enthält ihre Anhänge, daher müssen **sowohl** dieser Schalter als auch `Api__AllowAttachmentDownloads` auf `true` stehen; das Deaktivieren eines der beiden deaktiviert beide Endpunkte.
- `Api__EnableSwaggerUi`: Stellt die Swagger UI unter `/apidocs` und das OpenAPI-Dokument unter `/apidocs/spec/v1.json` bereit (Standard `true`). Beide erfordern eine angemeldete Browser-Sitzung.
- `Api__DefaultPageSize`: Standardmäßige Seitengröße für Listen-Endpunkte, wenn nicht angegeben (Standard `20`).
- `Api__MaxPageSize`: Maximal zulässige Seitengröße; größere Anfragen werden begrenzt (Standard `100`).
- `Api__RateLimitPerMinute`: Festes Zeitfenster-Anfragebudget pro API-Schlüssel pro Minute (Standard `120`).

### 🤖 MCP Server Einstellungen {#mcp-server-settings}

Der optionale MCP (Model Context Protocol) Server stellt KI-Agenten über den Streamable HTTP-Transport denselben schreibgeschützten E-Mail-Zugriff wie die REST API zur Verfügung. Er ist **standardmäßig deaktiviert**. Weitere Informationen finden Sie im [MCP Guide](MCP.md). MCP verwendet exakt dieselben API-Keys wie die REST API (`ma_...` Bearer-Token) und dieselbe pro Benutzer geltende Konto-Zugriffsbeschränkung.
- `Mcp__Enabled`: Hauptschalter für den MCP Server (Standard `false`). Wenn `false`, gibt der `/mcp` Endpunkt `404` zurück.
- `Mcp__AllowAttachmentDownloads`: Erlaubt dem `get_attachment` Tool, Anhang-Bytes zurückzugeben (Standard `true`). Wenn `false`, gibt das Tool einen Fehler zurück.
- `Mcp__DefaultPageSize`: Standardmäßige Seitengröße, die von `search_emails` verwendet wird, wenn der Aufrufer `pageSize` weglässt (Standard `20`).
- `Mcp__MaxResults`: Obergrenze für die Seitengröße von `search_emails`; größere Werte werden begrenzt (Standard `100`).
- `Mcp__MaxAttachmentBytes`: Maximale Anhanggröße (in Bytes), die das `get_attachment` Tool inline als Base64 zurückgibt; größere Anhänge werden abgelehnt (Standard `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Erlaubt den `get_email_eml` und `get_email_raw` Tools, vollständige Nachrichten zurückzugeben (Standard `true`). Wenn `false`, geben die Tools einen Fehler zurück. Eine vollständige Nachricht enthält ihre Anhänge, daher müssen **sowohl** dieser Schalter als auch `Mcp__AllowAttachmentDownloads` auf `true` stehen; das Deaktivieren eines der beiden deaktiviert beide Tools.
- `Mcp__MaxMessageBytes`: Maximale Nachrichtengröße (in Bytes), die die `get_email_eml` und `get_email_raw` Tools inline zurückgeben; größere Nachrichten werden abgelehnt (Standard `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Festes Zeitfenster-Anfragebudget pro API-Key pro Minute (Standard `120`).

### 📨 MailSync Einstellungen {#mailsync-settings}
- `MailSync__IntervalMinutes`: Das Intervall in Minuten zwischen der E-Mail-Synchronisierung. Dies ist der globale Standardwert; jedes Konto kann ihn individuell von der Erstellen/Bearbeiten-Seite aus überschreiben (leer lassen, um diesen Standardwert zu verwenden).
- `MailSync__FullSyncIntervalHours`: Optionaler globaler Standardwert für automatische vollständige Resyncs in Stunden. Wenn nicht festgelegt (der Standard), wird keine automatische vollständige Synchronisierung ausgeführt, es sei denn, ein pro Konto festgelegter `FullSyncIntervalHours`-Wert ist auf der Erstellen/Bearbeiten-Seite eingestellt. Pro-Konto-Werte überschreiben diesen globalen Standardwert.
- `MailSync__TimeoutMinutes`: Zeitüberschreitung der Synchronisierung pro Konto in Minuten. Eine Synchronisierung, die länger läuft, stoppt an der nächsten Nachrichten-Grenze, behält ihre Checkpoints bei und wird beim nächsten Durchlauf fortgesetzt; der Job wird als `Timed Out` gemeldet und `LastSync` wird nicht fortgeschritten. Gilt nur für geplante Synchronisierungen; eine über die UI gestartete Synchronisierung (manuelle Synchronisierung oder vollständige Resync) läuft ohne Zeitüberschreitung. `0` (der Standard) oder jeder nicht-positive Wert bedeutet keine Zeitüberschreitung. Überprüfen Sie den Wert vor einem Upgrade, wenn Sie große Postfächer synchronisieren. Siehe [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Die Verbindungszeitüberschreitung für IMAP-Verbindungen in Sekunden.
- `MailSync__CommandTimeoutSeconds`: Die Befehlszeitüberschreitung für IMAP-Befehle in Sekunden.
- `MailSync__AlwaysForceFullSync`: Ob immer eine vollständige Synchronisierung erzwungen werden soll (true/false).
- `MailSync__IgnoreSelfSignedCert`: Ob selbstsignierte Zertifikate zu ignorieren sind (true/false).
- `MailSync__MaxConcurrentSyncs`: Wie viele Konto-Synchronisierungen gleichzeitig laufen dürfen. Standard `1` (sequenziell, abwärtskompatibel). Der Scheduler füllt Slots auf, sobald sie frei werden, anstatt auf einen ganzen Batch zu warten, sodass ein langsames Postfach nicht mehr die anderen aufhält. Erhöhen Sie den Wert, um mehrere Konten gleichzeitig zu synchronisieren — beachten Sie dabei die Ratenbegrenzungen der Anbieter und die lokale Ressourcennutzung. Siehe [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Optionale Stufenverzögerung in Sekunden, die am Ende jeder Konto-Synchronisierungsaufgabe angewendet wird. Standard `0` (keine Verzögerung). Nützlich, um Burst-Starts zu vermeiden, wenn `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Wie viele Probleme jeder Art ein Synchronisierungsjob für die Kontoseite speichert. Fehlgeschlagene Ordner, fehlende Ordner und fehlgeschlagene Nachrichten werden separat budgetiert, sodass eine Flut einer Art die anderen nicht aus der Ansicht drängen kann; alles, was über das Budget hinausgeht, wird gezählt, anstatt gespeichert zu werden. Standard `20`. `0` schaltet die Details aus und lässt nur die Zähler übrig. Siehe [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Ordner, die für **jedes** Konto von der Synchronisierung ausgeschlossen sind, zusätzlich zur jeweiligen Liste der ausgeschlossenen Ordner des Kontos. Standardmäßig leer, sodass bestehende Setups unbeeinflusst bleiben. Die beiden Listen sind **additiv** — ein Ordner wird übersprungen, wenn er mit einer der beiden übereinstimmt — und beide verwenden dieselben Matching-Regeln: exakte Übereinstimmung auf dem vollständigen Pfad, exakte Übereinstimmung auf dem Ordnernamen, eine Pfad-Suffix-Übereinstimmung (sodass `Drafts` auch mit `INBOX/Drafts` und `INBOX.Drafts` übereinstimmt) und alles unterhalb des Ordners, den ein Eintrag benennt, wenn `MailSync__ExcludeSubfolders` aktiviert ist. Das Matching ist nicht fallsensitiv. Nützlich beim Importieren vieler Postfächer vom selben Server, wo die Alternative darin besteht, auf jedem Konto eine identische Ausschlussliste zu pflegen. Standardmäßig ist kein Ordner ausgeschlossen; welche Namen es wert sind, aufgelistet zu werden, hängt vom Server und seiner Sprache ab. Beispiel für einen Postfachbaum, der auch Kalender- und Kontaktordner enthält:
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
- `MailSync__ExcludeSubfolders`: Ob ein Ausschluss-Eintrag auch die Ordner unterhalb des benannten Ordners abdeckt. Gilt gleichermaßen für die pro Konto festgelegte Liste und die installationsweite Liste sowie für beide Anbieter. Standard `false`, was dem Verhalten entspricht, bevor die Option existierte: Ein Eintrag `Deleted Items` passt nur zu diesem Ordner, und ein Baum wird aus dem Archiv ferngehalten, indem man dessen Wurzel und jeden darunter liegenden Ordner benennt. Setzen Sie `true`, um einen Eintrag den darunter liegenden Baum mit einzuschließen — ein Eintrag `Deleted Items` hält dann auch `Deleted Items/2024` und den Rest dieses Zweigs aus dem Archiv fern. Die Regel verankert am Pfadtrennzeichen, sodass sie ganze Ordner erfasst und niemals Teile eines Namens: `Archive` deckt `Archive` und `Archive/Team` ab und lässt einen Mail-Ordner namens `Team Archive Review` unberührt. Ein `.` zählt ebenfalls als Trennzeichen, genau so, wie die Pfad-Suffix-Regel ihn behandelt.

### 📧 Einstellungen zur Speicherung der Originalnachricht {#original-message-storage-settings}

Standardmäßig bewahrt das Archiv nur die analysierten Teile einer E-Mail auf, sodass ein exportiertes `.eml` eine Rekonstruktion ist: Transfer-Kodierungen, MIME-Grenzen und Header-Folding unterscheiden sich von der empfangenen Nachricht, und DKIM-Signaturen lassen sich nicht mehr verifizieren. Mit `RawMessages:Enabled` wird auch das vollständige Original gespeichert.

| Parameter | Standard | Zweck |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Speichert das Original jeder neu archivierten E-Mail. |
| `RawMessages__Storage` | `postgres` | Wo die Bytes liegen: `postgres` oder `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Größere Nachrichten werden ohne ihr Original archiviert. `0` = kein Limit. |
| `RawMessages__IncludeGraph` | `true` | Speichert auch Originale für Microsoft 365 (Graph) Konten. |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Verzeichnis für die Nachrichten-Dateien. Muss auf ein gemountetes Volume in Docker verweisen. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Verzeichnisebenen, über die die E-Mail-ID verteilt ist. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Verifiziert das SHA-256 bei jedem Download. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Entfernt Nachrichten-Dateien, auf die keine E-Mail verweist. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Wie oft dieser Sweep ausgeführt wird. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Dateien, die jünger als dies sind, werden niemals entfernt. |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Verzögerung vor dem ersten Sweep nach dem Start, damit ein Import, der noch schreibt, nicht bereinigt wird. |

!!! warning "Filesystem-Backend benötigt ein gemountetes Volume"
    Mit `RawMessages__Storage=filesystem` liegen die Nachrichten-Dateien außerhalb der Datenbank.
    Ohne einen Bind Mount liegen sie in der beschreibbaren Schicht des Containers und gehen bei der
    nächsten Container-Rekonstruktion verloren, während die Datenbankzeilen überleben. Sichern Sie das Verzeichnis
    zusammen mit dem Datenbank-Dump.

Siehe **[Original Message Storage](RawMessages.md)** für die vollständige Beschreibung: wo das
Original bereitgestellt wird, die Storage-Backends, wie man bestehende Nachrichten mit
`--migrate-raw-storage` zwischen ihnen verschiebt und was man sichern muss.

### 📈 Monitoring-Einstellungen {#monitoring-settings}

| Parameter | Standard | Zweck |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | Aktivierte Konten, die älter als dies sind, lassen `/api/v1/health` Bericht `Degraded` erstellen. `0` deaktiviert die Prüfung. |
| `Metrics__Enabled` | `false` | Aktiviert den Prometheus-Metriken-Endpunkt und dessen Refresh-Loop. |
| `Metrics__RefreshMinutes` | `5` | Wie oft der Metriken-Snapshot neu aufgebaut wird. Werte unter 1 werden auf 1 angehoben. |

Die Health- und Metriken-Endpunkte erfordern einen Admin-API-Schlüssel und `Api__Enabled=true`. Siehe
**[Health and Metrics](Monitoring.md)** für die Endpunkte, Metrikennamen, Prometheus-
Konfiguration und den Docker-Health-Check.

### 📤 BatchRestore Einstellungen {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Die Anzahl der E-Mails, die die asynchrone Verarbeitung auslöst.
- `BatchRestore__MaxSyncEmails`: Die maximale Anzahl von E-Mails für die synchrone Verarbeitung.
- `BatchRestore__MaxAsyncEmails`: Die maximale Anzahl von E-Mails für die asynchrone Verarbeitung.
- `BatchRestore__SessionTimeoutMinutes`: Das Sitzungs-Timeout für die Batch-Wiederherstellung in Minuten.
- `BatchRestore__DefaultBatchSize`: Die Standard-Batch-Größe für E-Mail-Operationen.

### 🏢 Tenant-Management-Einstellungen {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Maximale Anzahl an Postfächern, die in einem einzigen Tenant-Management-Vorgang hinzugefügt werden können. Standardwert ist `1000`. Erhöhen Sie diesen Wert für sehr große Mandanten oder senken Sie ihn, um versehentliche Massenimporte zu verhindern. Wenn das Limit überschritten wird, wird der Vorgang mit einem Validierungsfehler abgelehnt und es werden keine Konten erstellt. Weitere Details finden Sie im [M365 Tenant Import Guide](M365TenantImport.md).

### 🐄 Mailcow-Einstellungen {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Maximale Anzahl an Postfächern, die in einem einzigen Mailcow-Aktivierungslauf bereitgestellt werden können. Standard ist `1000`. Verringern Sie diesen Wert, um eine versehentliche Massenbereitstellung zu verhindern, oder erhöhen Sie ihn für große Instanzen. Wenn das Limit überschritten wird, werden keine Konten erstellt. Weitere Details finden Sie unter [Mailcow Instances](Mailcow.md).
- Die Mailcow-Integration erfordert zusätzlich `Security__CredentialEncryptionKey` (siehe [Security Settings](#-security-settings)). Das Verbinden einer Instanz und das Aktivieren von Postfächern werden beide abgelehnt, solange die Verschlüsselung der Anmeldedaten deaktiviert ist, sodass der API-Schlüssel und die generierten App-Passwörter niemals im Klartext gespeichert werden können.
- Ein ausgehender Zugriff auf die Mailcow HTTPS API und auf den IMAP-Host über TLS (normalerweise Port `993`) ist erforderlich.

### 📦 `BatchOperation__BatchSize` BatchOperation Einstellungen {#batchoperation-settings}
- `BatchOperation__BatchSize`: Die Batch-Größe für E-Mail-Operationen.
- `BatchOperation__PauseBetweenEmailsMs`: Die Pause zwischen einzelnen E-Mails in Millisekunden.
- `BatchOperation__PauseBetweenBatchesMs`: Die Pause zwischen Batches in Millisekunden.

### 📤 Offload-Einstellungen {#offload-settings}

Einstellungen für das [date-windowed offload](Offload.md). Alle Standardwerte reproduzieren das Verhalten, das die Anwendung hatte, bevor die Funktion existierte, sodass eine Installation, die diesen Abschnitt nicht konfiguriert, nicht beeinträchtigt wird.
- `Offload__MaxConcurrentJobs`: Wie viele Wiederherstellungs- oder Offload-Jobs gleichzeitig ausgeführt werden dürfen. Standard ist `1`, was die strikt serielle Verarbeitung beibehält, die die Warteschlange bisher immer verwendet hat. Unabhängig von diesem Wert wird maximal ein Job pro Ziel-Postfach ausgeführt.
- `Offload__PrefetchMaxMessages`: Obergrenze für die Anzahl der Nachrichten, die aus einem Ziel-Postfach zur Deduplizierung indiziert werden. Standard ist `500000`. Oberhalb dieses Wertes wird die Prüfung auf einen einzelnen Ordner eingegrenzt und protokolliert, dass der Umfang reduziert wurde.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Quellordner, die niemals offgeladen werden, vor dem Umbenennen abgeglichen werden und Unterordner abdecken. Standardmäßig leer.
- `Offload__FolderRenameMap__<SourceFolder>`: Schreibt die führenden Segmente eines Quellordnerpfads um, zum Beispiel `Offload__FolderRenameMap__Sent Items=Sent`. Standardmäßig leer.
- `Offload__MarkAsSeen`: Ob angehängte E-Mails als gelesen markiert werden. Standard ist `true`, was dem bestehenden Wiederherstellungsverhalten entspricht.

Beide Ordner-Einstellungen werden absichtlich leer ausgeliefert: Das Umschreiben oder Löschen von Ordnern ohne Aufforderung würde jeden überraschen, der bereits den Wiederherstellungspfad nutzt. Ein Durchlauf ohne Konfiguration migriert daher alles, einschließlich Spam-Ordner, und erstellt neben den eigenen Ordnern des Ziels einen zweiten Satz spezieller Ordner.

### 📊 Bandbreiten-Tracking-Einstellungen {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Aktivieren oder deaktivieren Sie das Bandbreiten-Tracking für die Handhabung von IMAP-Rate-Limits (true/false). Standard ist `false`. Wenn aktiviert, verfolgt das System die Bandbreitennutzung pro Konto und kann die Synchronisierung pausieren, wenn die Limits des Anbieters erreicht werden. Weitere Informationen finden Sie unter [Rate Limit Handling](RateLimitHandling.md).
- `BandwidthTracking__DailyLimitMb`: Tägliches Download-Limit in Megabyte pro Konto. Standard ist `25000` (25 GB). Bei Anbietern mit Bandbreitenbeschränkungen setzen Sie diesen Wert passend zu deren Rate Limit (z. B. `2500` für Anbieter mit ~2500 MB täglichen Limits). Das System pausiert die Synchronisierung, wenn dieses Limit erreicht ist.
- `BandwidthTracking__WarningThresholdPercent`: Prozentsatz des täglichen Limits, bei dem Warnmeldungen protokolliert werden. Standard ist `80`. Wenn die Bandbreitennutzung diesen Prozentsatz erreicht, werden Warnungen protokolliert, um die Annäherung an die Limits zu überwachen.
- `BandwidthTracking__PauseHoursOnLimit`: Anzahl der Stunden, um die Synchronisierung zu pausieren, wenn das tägliche Limit erreicht ist. Standard ist `24`. Nach diesem Zeitraum wird das Limit-Flag automatisch gelöscht und die Synchronisierung wird fortgesetzt.
- `BandwidthTracking__TrackUploadBytes`: Ob auch die Upload-Bandbreite verfolgt werden soll (true/false). Standard ist `false`. Die meisten IMAP-Anbieter beschränken nur Downloads, daher ist dies in der Regel nicht erforderlich.

### 🎯 Auswahl-Einstellungen {#selection-settings}
- `Selection__MaxSelectableEmails`: Die maximale Anzahl an E-Mails, die gleichzeitig ausgewählt werden können.

### 👁️ Einstellungen anzeigen (Datenschutz & Anzeige) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Steuert den Standard-E-Mail-Ansichtsmodus für Datenschutz und Tracking-Prävention (true/false). Standard ist `false`.
  - Wenn auf `true` gesetzt: E-Mails werden standardmäßig in der Nur-Text-Ansicht geöffnet, was das automatische Laden von Tracking-Pixeln, externen Bildern und Web-Beacons verhindert. Dies wird für Benutzer empfohlen, die sich um E-Mail-Tracking und Datenschutz sorgen.
  - Wenn auf `false` gesetzt: E-Mails werden standardmäßig in der HTML-Ansicht geöffnet und zeigen die formatierte E-Mail mit Bildern und Styling an.
  - Benutzer können zwischen HTML- und Nur-Text-Ansichten über eine Schaltfläche auf der E-Mail-Detailseite umschalten (nur sichtbar, wenn beide Formate verfügbar sind).
  - Der Link „Vollständige Ansicht“ berücksichtigt den aktuell ausgewählten Ansichtsmodus.

- `View__BlockExternalResources`: Blockiert externe Ressourcen (Remote-Bilder, externe CSS, externe Skripte, Web-Fonts usw.) in HTML-E-Mail-Ansichten, um Tracking zu verhindern und den Datenschutz zu verbessern (true/false). Standard ist `false`.
  - Wenn auf `true` gesetzt: Externe Ressourcen werden beim Anzeigen von HTML-E-Mails herausgefiltert. Es werden nur Inline-Inhalte und Data-URIs (einschließlich Inline-Anhänge über `cid:`-Referenzen) angezeigt.
  - Wenn auf `false` gesetzt: HTML-E-Mails werden mit all ihren ursprünglichen externen Ressourcen angezeigt.
  - **Wichtig**: Diese Einstellung betrifft nur die E-Mail-**Anzeige**. Archivierte E-Mails werden vollständig unverändert in der Datenbank gespeichert, wobei alle ursprünglichen Inhalte erhalten bleiben.
  - Blockierte Ressourcen umfassen:
    - Remote-Bilder (Tracking-Pixel, auf Servern gehostete externe Bilder)
    - Externe CSS-Stylesheets
    - Externe Schriftarten via @font-face
    - Externe CSS-Imports via @import
    - Externe Hintergrundbilder
  - Zulässige Ressourcen:
    - Eingebettete Inline-Bilder als data: URIs
    - Inline-Anhänge, die über cid: URIs referenziert werden
    - Inline-CSS-Styles und Style-Tags
  - Diese Einstellung arbeitet unabhängig von `DefaultToPlainText` und bietet eine zusätzliche Ebene des Datenschutzschutzes beim Betrachten von HTML-E-Mails.

### 📊 Dashboard-Einstellungen {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Ob die Counter-Karten ihre eingehenden und ausgehenden Anteile sowie die Konto-Karte die Anzahl der unterschiedlichen Domains enthalten. Standard ist `true`. Setzen Sie dies auf `false`, und diese Zahlen werden nicht berechnet, anstatt berechnet und ausgeblendet zu werden: Die Counter lesen eine einfache Anzahl, und die Anzahl der Anhänge hört auf, sich mit der Mail zu verknüpfen, an der sie hängt, was die einzige Abfrage ist, die diese Teile hinzufügen.
- `Dashboard__SelectablePeriods`: Ob die Dashboard-Diagramme eine Auflösung, einen Zeitraum und Pfeile zur Steuerung anbieten. Standard ist `true`. Setzen Sie dies auf `false`, und die Diagramme zeigen die letzten zwölf Monate nach Monat und die Absender des gesamten Archivs; der Chart-Endpunkt antwortet, als ob er nicht existierte, und das älteste Versanddatum wird nicht nachgeschlagen.
- `Dashboard__CacheSeconds`: Wie lange berechnete Dashboard-Statistiken (Gesamtsummen mit ihren eingehenden und ausgehenden Aufteilungen, pro Konto gezählte Werte, die Chart-Serien, Top-Absender, aktuelle E-Mails, Datenbankgröße) im In-Memory-Cache des Servers gespeichert werden. Standard ist `60` Sekunden. Setzen Sie dies auf `0`, um das Caching zu deaktivieren und die Statistiken immer neu zu berechnen. Jede Auswahl der Auflösung und des Zeitraums im Diagramm wird separat gecacht, sodass ein Dashboard, zwischen dem Auswahlmöglichkeiten gewechselt werden, pro Auswahl einen Eintrag speichert. Höhere Werte reduzieren die Datenbanklast in großen Umgebungen auf Kosten veralteter Zahlen. Sync-Status-Badges, Speicherwerte und die Liste der aktuellen E-Mails werden immer live abgerufen und sind von diesem Cache nicht betroffen.
- `Dashboard__RefreshIntervalMinutes`: Wie oft der `DashboardStatsRefreshService` Hintergrunddienst die aufwendigen Dashboard-Aggregate (Gesamtzahlen mit ihren eingehenden und ausgehenden Aufteilungen, pro Konto gezählte Werte, die Anzahl der Konto-Domains, die Standard-Chart-Serie mit ihren Top-Absendern, Datenbankgröße) in die `DashboardStatsCache` Datenbanktabelle neu berechnet. Bei Installationen mit mehreren Millionen Mails liest das Dashboard diese vorbereiteten Werte, anstatt das gesamte Archiv im Anforderungspfad zu aggregieren, was eine ~10-sekündige Erstladung in Millisekunden verwandelt. Der Chart-Endpunkt beantwortet die Standardauswahl (Monate über ein Jahr, eingehende Absender, der aktuelle Zeitraum) aus derselben Zeile, sodass die Rückkehr dazu die Zahlen des ersten Paints anzeigt; jede andere Auswahl wird bei Anforderung berechnet und im oben genannten In-Memory-Cache gespeichert. Standard ist `15` Minuten. Setzen Sie dies auf `0`, um die Hintergrundaktualisierung zu deaktivieren; das Dashboard berechnet die Statistiken dann wieder live (wobei der oben genannte In-Memory-Cache weiterhin Anwendung findet). Die angezeigten Werte können dem tatsächlichen Archivzustand bis zum konfigurierten Intervall hinterherhinken. Eine Zeile, die mit anderen `ShowDirectionSplits` oder `SelectablePeriods` Werten als den aktuellen berechnet wurde, wird nicht verwendet; das Dashboard berechnet live bis zur nächsten Aktualisierung.

### 🔄 Einstellungen für Jobs {#jobs-settings}
- `Jobs__RefreshSeconds`: Wie oft sich die Hintergrund-Jobs-Seite selbst neu lädt, während ein Browser-Tab sie geöffnet hat. Standard ist `30` Sekunden. Setzen Sie den Wert auf `0`, um das automatische Neuladen zu deaktivieren und manuell zu aktualisieren. Jeder Reload baut die Seite aus allen acht Job-Quellen neu auf, daher hält ein längeres Intervall bei Installationen mit vielen Konten die Last niedrig, multipliziert mit jedem Tab, der auf der Seite geöffnet ist.

### 🗃️ Npgsql-Einstellungen {#npgsql-settings}
- `Npgsql__CommandTimeout`: Der Timeout für Datenbankbefehle in Sekunden.

### 📥 Upload-Einstellungen {#upload-settings}
- `Upload__MaxFileSizeGB`: Die maximale Dateigröße für Uploads in GB.
- `Upload__KeepAliveTimeoutHours`: Der Keep-Alive-Timeout für Uploads in Stunden.
- `Upload__RequestHeadersTimeoutHours`: Der Timeout für Request-Header in Stunden.

### 📂 Lokale Import-Einstellungen {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, etc.: Whitelist lokaler Verzeichnisse, aus denen die CLI-Importbefehle (`--import-mbox`, `--import-eml`) Dateien lesen dürfen. Jeder Eintrag ist ein Pfad innerhalb des Containers. Sie müssen Ihre Importdateien mithilfe von Docker-Volumes in eines dieser Verzeichnisse mounten. Standard: `/data/import` (automatisch auf `/app/uploads` als Fallback gesetzt).
  - Wenn Sie `docker exec` verwenden, um den Importbefehl auszuführen, muss der über `--file` angegebene Dateipfad innerhalb eines dieser zulässigen Pfade liegen.
  - Diese Sicherheitsmaßnahme verhindert den beliebigen Dateisystemzugriff durch CLI-Befehle.
  - Es können mehrere Pfade für verschiedene Importquellen konfiguriert werden.
  - Weitere Informationen finden Sie in den [CLI-Befehlen](CLI.md).

### 📄 CSV-Import-Einstellungen {#csv-import-settings}
- `CsvImport__MaxRows`: Maximale Anzahl der in einem einzelnen Massenimport verarbeiteten CSV-Zeilen (Postfächer). Standardwert ist `5000`. Erhöhen Sie diesen Wert für große Bereitstellungen; senken Sie ihn, um die Auswirkungen eines einzelnen Importlaufs auf die Datenbanklast zu begrenzen.
- `CsvImport__MaxFileSizeBytes`: Maximale zulässige Größe (in Bytes) der hochgeladenen CSV-Datei. Standardwert ist `10000000` (10 MB). Passen Sie diesen Wert bei Bedarf an Ihre Upload-Limits an.
- Siehe [Account Import Guide](Account-Import.md) für detaillierte Gebrauchsanweisungen zum Massenimport von IMAP-Konten via CSV.

### 📤 Audit Export Einstellungen {#audit-export-settings}

Einstellungen für die Seite zum Export der Audit-Daten (nur für Administratoren, erreichbar von der Logs-Seite). Der Export generiert ein ZIP-Paket mit tabellarischen Massendaten (INDEX.XML + CSV-Tabellen + DTD) aus dem bestehenden Archiv für externe Audit-Tools. Weitere Details zur Verwendung finden Sie im [Audit Data Export Guide](AuditExport.md).

- `AuditExport__DataSupplierName`: Standardwert für das Formularfeld „Data supplier name“, das Ihre Organisation in der exportierten Indexdatei identifiziert. Standard: leer.
- `AuditExport__DataSupplierLocation`: Standardwert für das Formularfeld „Data supplier location“ (z. B. Firmensitz). Standard: leer.
- `AuditExport__Comment`: Standardwert für das Formularfeld „Comment“, das als Freitext in der exportierten Indexdatei enthalten ist. Standard: leer.
- `AuditExport__OutputDirectory`: Verzeichnis, in dem die generierten ZIP-Dateien gespeichert werden. Relative Pfade werden relativ zum App-Content-Root aufgelöst. Standard: `exports/audit`.
- `AuditExport__RetentionDays`: Anzahl der Tage, nach denen abgeschlossene Exportdateien durch die tägliche Bereinigung gelöscht werden. Standard: `30`.
- `AuditExport__MaxRangeYears`: Maximal zulässiger Zeitraum zwischen dem Start- und Enddatum des Exportzeitraums. Standard: `10`.
- Die Formularfelder sind mit diesen Standardwerten vorbefüllt, können aber pro Export bearbeitet werden.
- Jeder Export schreibt einen Start-Eintrag und einen Ergebnis-Eintrag in das Zugriffsprotokoll (Typ „Audit Data Export“), sodass der Verlauf ohne jegliche Änderung am DB-Schema revisionssicher ist.

### 🔒 Einstellungen für die Löschrichtlinie {#deletion-policy-settings}

- `DeletionPolicy__DeletionAllowed`: Steuert, ob das manuelle Löschen von archivierten E-Mails zulässig ist (true/false). Standard ist `true`. Wenn auf `false` gesetzt:
  - Alle archivierten E-Mails werden beim Start über den Datenbank-Compliance-Trigger gesperrt (`IsLocked = true`), was jegliche Änderung oder Löschung auf Datenbankebene verhindert.
  - Manuelles Löschen (einzeln und gesammelt) wird auf Anwendungsebene mit einer Fehlermeldung blockiert.
  - Der Spalten-Standardwert wird so angepasst, dass auch neu importierte E-Mails gesperrt werden.
  - Der aktuelle Richtlinienstatus wird bei jedem Start in die Tabelle AccessLogs protokolliert (auf der Logs-Seite als „Deletion Policy“-Einträge sichtbar) zur Auditierbarkeit.
  - Die lokale Löschung gemäß Aufbewahrung ist ausgenommen: E-Mails, die unter einen konfigurierten Aufbewahrungszeitraum fallen, werden weiterhin gelöscht (sie werden unmittelbar vor der Löschung innerhalb des Aufbewahrungsprozesses entsperrt).
- `DeletionPolicy__MaxBulkDeleteEmails`: Obergrenze für eine filterbasierte Massenlöschung (*Delete all matches*). Standard ist `100000`. Ein Filter, der mehr E-Mails trifft, wird mit einer Meldung abgelehnt, die dazu auffordert, den Filter zu verengen; der Job wird niemals stillschweigend gekürzt. E-Mails, die durch die Aufbewahrungsrichtlinie gesperrt sind, werden übersprungen und im Job-Status gemeldet, anstatt sie zu entsperren. Der Job überprüft dieses Limit und `DeletionPolicy__DeletionAllowed` beim Start erneut, sodass ein Job, der vor einer Richtlinienänderung in die Warteschlange gestellt wurde, stoppt, anstatt mit der alten Einstellung weiterzulaufen.
- **Unveränderlichkeitsschutz:** Wenn `IsLocked = true`, blockiert der Datenbank-Compliance-Trigger (`prevent_locked_email_changes`) JEDE Änderung an der E-Mail-Zeile — alle Spalten sind geschützt, nicht nur eine feste Liste von Feldern. Die einzigen ausgenommenen Spalten sind `IsLocked` selbst (damit das Entsperren für die Löschung gemäß Aufbewahrung und die Anwendung der Startrichtlinie weiterhin möglich bleibt) und `FolderName` (damit die IMAP-Synchronisierung den Ordner aktualisieren kann, wenn eine E-Mail serverseitig verschoben wird). Der Schutz ist spaltenunabhängig (JSONB-basierter Vergleich) und deckt automatisch zukünftige Schema-Erweiterungen ab.

### 🕐 Zeitzoneneinstellungen {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Die Zeitzone, die zur Anzeige von E-Mail-Zeitstempeln in der Benutzeroberfläche verwendet wird. Verwendet IANA-Zeitzonenbezeichner (z. B. "Europe/Berlin", "Asia/Tokyo"). Der Standardwert ist "Etc/UCT" zur Abwärtskompatibilität. Beim Importieren von E-Mails werden die Zeitstempel zu dieser Zeitzone für die Anzeige konvertiert.

### 🎉 ReleaseNotes Settings (Version Update Splash Screen) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: Der Versionsstring, den diese Instanz meldet. Er wird mit GitHub-Release-Tags abgeglichen und entscheidet, ob eine neuere Version existiert. Wenn er leer ist, greift das Changelog auf die kompilierte Assembly-Version zurück und der Update-Hinweis bleibt deaktiviert.
- `ReleaseNotes__Enabled`: Aktiviert oder deaktiviert den Splash-Screen für Versions-Updates (true/false). Standard ist `true`. Wenn aktiviert, sehen Administratoren nach einem Anwendungsupdate einmalig ein Changelog-Modal, das die aus GitHub Releases für die aktuelle Version abgerufenen Release Notes anzeigt. Jeder Administrator kann das Modal schließen, und es erscheint nur bei einer neuen Version erneut. Setzen Sie `false`, um diese Funktion einschließlich des Update-Hinweises vollständig zu deaktivieren.
- `ReleaseNotes__UpdateHintEnabled`: Zeigt Administratoren einen kleinen Toast in der unteren rechten Ecke an, wenn GitHub eine neuere stabile Version als die installierte Version veröffentlicht (true/false). Standard ist `true`. Der Toast verlinkt auf dieses Release und wird pro Version im Browser geschlossen. Entwürfe und Pre-Releases werden ignoriert. Erfordert `ReleaseNotes__AppVersion`; ohne diesen wird kein Hinweis angezeigt.
- `ReleaseNotes__UpdateCheckIntervalHours`: Wie oft die GitHub-Release-Liste aktualisiert wird, in Stunden. Standard ist `6`. Ein kürzeres Intervall macht ein neues Release in einer lang laufenden Instanz früher sichtbar, ein längeres reduziert die GitHub-API-Aufrufe. Minimum ist `1`.
- `ReleaseNotes__RequestTimeoutSeconds`: Timeout für eine GitHub-API-Anfrage, in Sekunden. Standard ist `10`. Wenn die Zeit abläuft, bleiben das Changelog und der Update-Hinweis für diesen Versuch deaktiviert, anstatt die Anfrage offen zu halten; der nächste Check führt einen erneuten Versuch durch.

### 🔧 Einstellungen zur Datenbankwartung {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Automatische tägliche Datenbankwartung aktivieren oder deaktivieren (true/false). Standard ist `false`. Wenn aktiviert, führt das System automatisch VACUUM ANALYZE-Operationen aus, um die Datenbankleistung zu optimieren und Bloat zu verhindern. Weitere Details finden Sie im [Datenbankwartungsleitfaden](DatabaseMaintenance.md).
- `DatabaseMaintenance__DailyExecutionTime`: Die Tageszeit, zu der die Datenbankwartung ausgeführt werden soll, im 24-Stunden-Format (HH:mm). Standard ist `02:00`. Wählen Sie eine Zeit während geringer Systemaktivität.
- `DatabaseMaintenance__TimeoutMinutes`: Maximale Zeit in Minuten, die für Wartungsoperationen zulässig ist. Standard ist `30`. Erhöhen Sie diesen Wert für größere Datenbanken.

### 🧬 Attachment Deduplication Settings {#attachment-deduplication-settings}

Die Deduplizierung von Anhängen speichert jede eindeutige Anhang-Payload nur einmal (inhaltsadressiert durch SHA-256) und ist ein **Kernfeature, das immer aktiviert ist** – es gibt absichtlich keinen Ein/Aus-Schalter. Nur die unten aufgeführten Batch-/Zeitplanparameter können angepasst werden. Weitere Details finden Sie im [Attachment Deduplication Guide](AttachmentDeduplication.md).

- `AttachmentDeduplication__BatchSize`: Anzahl der pro Transaktion migrierten vorhandenen Anhänge während der einmaligen Hintergrundmigration bereits vorhandener Daten. Standard ist `200`. Größere Werte migrieren schneller, verursachen aber pro Batch eine höhere Speicher-/DB-Last.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Optionale Pause (in Millisekunden) zwischen Migrations-Batches, um die Datenbanklast auf ausgelasteten Systemen zu drosseln. Standard ist `0` (keine Pause).
- `AttachmentDeduplication__StartupDelaySeconds`: Verzögerung (in Sekunden) nach dem Anwendungsstart, bevor die Hintergrundmigration beginnt, um der Schema-Migration Zeit zur Fertigstellung zu geben. Standard ist `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Intervall (in Stunden) der permanent laufenden Garbage Collection, die Anhang-Payloads entfernt, die nicht mehr von einer E-Mail referenziert werden. Standard ist `12`. Dies läuft unabhängig von `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Datenbank-Befehls-Timeout (in Sekunden) für die Migrations-Batch-Operationen (INSERT mit SHA-256 Hashing und UPDATE). Standard ist `300` (5 Minuten). Erhöhen Sie diesen Wert für sehr große Datenbanken oder Anhänge, oder senken Sie ihn, wenn Sie eine schnellere Fehlererkennung wünschen. Wenn ein Batch immer noch ein Timeout verursacht, versucht der Dienst es automatisch mit der halben Batch-Größe erneut.

### 💾 Konto-Speichereinstellungen {#account-storage-settings}

Die Anzeige des Speichers pro Konto zeigt die Datenbank-Speichernutzung (alle Mail-Felder + Anhänge) für jedes Konto in der Dashboard „Account Overview“-Tabelle und der MailAccounts „Show All“-Tabelle an. Ein autarker Hintergrunddienst (`AccountStorageRefreshService`) berechnet die Werte, indem er die PostgreSQL `pg_column_size` pro Spalte aufsummiert (alle Felder einer Mail abdeckend, ohne die Mail-Bodies zu lesen) und diese in der `AccountStorageCache`-Tabelle zwischenspeichert. Dieser Dienst läuft unabhängig von `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Aktivieren oder deaktivieren des Speichereffizienzhilfe-Dienstes (true/false). Standard ist `true`. Wenn aktiviert, führt der Dienst beim Start eine fortsetzbare Nachfüllung (backfill) ausstehender Konten durch (absturzsicher via `AccountStorageBackfillState`) und danach eine tägliche vollständige Aktualisierung. Wenn deaktiviert, werden die Speicherwerte nur aktualisiert, wenn E-Mails synchronisiert, importiert oder gelöscht werden, aber die angezeigten Werte können im Laufe der Zeit veraltet sein.
- `AccountStorage__DailyExecutionTime`: Tageszeit (24-Stunden-Format `HH:mm`) für die tägliche vollständige Aktualisierung aller Konten. Standard ist `02:30`. Wählen Sie eine Zeit während geringer Systemaktivität.
- `AccountStorage__BackfillDelayMs`: Verzögerung (in Millisekunden) zwischen den Konten während der initialen Nachfüllung beim Start. Standard ist `5000`. Niedrigere Werte beschleunigen die Nachfüllung, erhöhen aber die Datenbanklast; erhöhen Sie diesen Wert für sehr große Archive, um eine Überlastung der Datenbank zu vermeiden.
- `AccountStorage__RefreshBatchDelayMs`: Verzögerung (in Millisekunden) zwischen den Konten während der täglichen vollständigen Aktualisierung. Standard ist `1000`. Niedrigere Werte beschleunigen die Aktualisierung, erhöhen aber die Datenbanklast; erhöhen Sie diesen Wert für sehr große Archive.
- `AccountStorage__CommandTimeoutSeconds`: Datenbank-Befehl-Timeout (in Sekunden) für die Berechnung des Speichers pro Konto (`pg_column_size` über alle Zeilen eines Kontos). Standard ist `300` (5 Minuten). Die Berechnung liest jede Mail-Zeile einschließlich TOAST-Daten, was bei sehr großen Archiven (250k+ E-Mails) weit über eine Minute dauern kann; der vorherige 30-Sekunden-Npgsql-Standard führte dazu, dass die Aktualisierung lautlos fehlschlug und das Dashboard veraltete Werte anzeigte. Erhöhen Sie diesen Wert, wenn die Berechnung bei Ihrem Archiv immer noch einen Timeout verursacht.

> 💡 **Hinweis**: Speicherwerte werden unmittelbar nach jeder Mail-Synchronisierung, jedem Import oder jeder Löschung aufgrund der Aufbewahrung aktualisiert, sodass die angezeigten Werte auch ohne die tägliche Aktualisierung aktuell bleiben. Die tägliche Aktualisierung dient als Sicherheitsnetz, das Grenzfälle (z. B. direkte Datenbankänderungen) abfängt.

### 📝 Logging-Einstellungen {#logging-settings}
- `Logging__LogLevel__Default`: Die Standard-Log-Ebene für die Anwendung. Verfügbare Ebenen sind: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Standard ist `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Log-Ebene für ASP.NET Core Framework-Nachrichten. Standard ist `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Log-Ebene für Entity Framework Datenbankbefehle. Standard ist `Warning`.

### 🛡️ Sicherheitseinstellungen {#security-settings}
- `AllowedHosts`: Eine durch Semikolon getrennte Liste von Hostnamen, die die Anwendung bedienen darf. Dies hilft, HTTP Host-Header-Angriffe zu verhindern. Beispiel: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Wichtig**: Verwenden Sie `*` nicht in Produktionsumgebungen, da dies die Host-Header-Validierung deaktiviert.

#### 🔐 Verschlüsselung von Anmeldedaten im Ruhezustand {#credential-encryption-at-rest}
Die Anmeldedaten des Mail-Kontos (IMAP-Passwort, M365 Client Secret und MSA OAuth-Token) können in der Datenbank verschlüsselt werden. Der Verschlüsselungsschlüssel wird **außerhalb** der Datenbank aufbewahrt (Umgebungsvariable / Docker Secret), sodass ein Datenbank-Dump allein nicht ausreicht, um die Anmeldedaten zu lesen.

- `Security__CredentialEncryptionKey`: Base64-codierter 256-Bit (32 Byte) Key-Encryption-Key. Erzeugen Sie einen mit:
  ```bash
  openssl rand -base64 32
  ```
  Setzen Sie den Wert als Umgebungsvariable (empfohlen: `Security__CredentialEncryptionKey` in Ihrer Compose-Datei oder Container-Umgebung). Die Anwendung unterstützt keine Docker-Secrets im `_FILE`-Stil — wenn Sie ein Docker-Secret verwenden, mounten Sie es und laden Sie dessen Inhalt in die Umgebungsvariable in Ihrem Entrypoint. Committen Sie den Schlüssel niemals in `appsettings.json`. Wenn ein Schlüssel konfiguriert ist, werden die Anmeldedaten beim Schreiben verschlüsselt, und ein Hintergrunddienst verschlüsselt einmalig vorhandene Klartext-Anmeldedaten.
- `Security__EncryptAccountCredentials`: Aktiviert oder deaktiviert die Verschlüsselung neuer/aktualisierter Anmeldewert-Einträge (true/false). Standard ist `true`. Wenn kein Schlüssel konfiguriert ist, werden die Anmeldedaten als Klartext gespeichert und beim Start eine Warnung protokolliert — bestehende Installationen sind nicht betroffen, bis ein Schlüssel bereitgestellt wird.
- `Security__CredentialEncryptionBackfill`: Ob der Hintergrunddienst beim Start vorhandene Klartext-Anmeldedaten verschlüsseln soll (true/false). Standard ist `true`. Keine Auswirkung ohne konfigurierten Schlüssel.
- `Security__CredentialEncryptionKeyPrevious`: Optionaler vorheriger Schlüssel, der nur verwendet wird, um mit ihm verschlüsselte Werte zu entschlüsseln (für Key Rotation). Neue Werte werden immer mit `Security__CredentialEncryptionKey` verschlüsselt.

  > ⚠️ **Key Rotation**: Nach der Rotation halten Sie `Security__CredentialEncryptionKeyPrevious` konfiguriert, **bis jedes Anmeldedatum neu geschrieben wurde** (z. B. durch Bearbeiten des Kontos oder ein automatisches OAuth-Token-Refresh) — vorhandene mit dem alten Schlüssel verschlüsselte Werte werden *nicht* automatisch mit dem neuen Schlüssel neu verschlüsselt. Sobald Sie den vorherigen Schlüssel entfernen, wird jedes Anmeldedatum, das noch mit ihm verschlüsselt ist, unlesbar und muss erneut eingegeben werden. Solange er konfiguriert ist, muss er genauso sicher wie der aktuelle Schlüssel aufbewahrt werden.

> ⚠️ **Halten Sie den Schlüssel sicher**: Ohne den Schlüssel können verschlüsselte Anmeldedaten nicht wiederhergestellt werden und müssen erneut eingegeben werden. Sichern Sie ihn zusammen mit (aber getrennt von) Ihren Datenbank-Sicherungen.
>
> 💡 **Bestehende Installationen**: Upgrade sicher durchführen — solange kein `Security__CredentialEncryptionKey` gesetzt ist, ändert sich nichts. Setzen Sie den Schlüssel, wenn Sie bereit sind; vorhandene Klartext-Anmeldedaten bleiben lesbar und werden im Hintergrund verschlüsselt.

### 🔐 OIDC-Konfiguration {#oidc-configuration}

Für detaillierte Einrichtungshinweise zur OpenID Connect-Authentifizierung lesen Sie bitte den [OIDC Implementation Guide](OIDC_Implementation.md).

#### Grundlegende OIDC-Einstellungen {#basic-oidc-settings}
- `OAuth__Enabled`: OIDC-Authentifizierung aktivieren oder deaktivieren (true/false)
- `OAuth__Authority`: Die OpenID Connect Authority-URL (z. B. https://sts.windows.net/{TENANT-ID}/ für Azure AD)
- `OAuth__ClientId`: Die von Ihrem Identity Provider zugewiesene Client ID
- `OAuth__ClientSecret`: Das von Ihrem Identity Provider zugewiesene Client Secret
- `OAuth__DisplayName`: Optionaler Anzeigename, der auf der OIDC-Login-Schaltfläche und der Auto-Redirect-Seite angezeigt wird (z. B. `PocketID SSO`). Wenn dieser weggelassen wird, wird die generische Bezeichnung „Login mit OAuth“ verwendet.
- `OAuth__ClientScopes__0`: Erster vom Identity Provider angeforderter Scope (openid)
- `OAuth__ClientScopes__1`: Zweiter vom Identity Provider angeforderter Scope (profile)
- `OAuth__ClientScopes__2`: Dritter vom Identity Provider angeforderter Scope (email)
- `OAuth__ResponseMode`: Optionaler Response-Modus der Autorisierungsanfrage (`query`, `fragment` oder `form_post`). Standard ist `form_post`, der ASP.NET Core Standardwert. Setzen Sie dies auf `query` für Anbieter, die `form_post` nicht unterstützen, wie z. B. den Synology SSO Server. Siehe [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Neue OIDC-Benutzer automatisch genehmigen, ohne dass eine manuelle Administrator-Genehmigung erforderlich ist (true/false). Standard ist `false`.

#### Einstellungen zur Benutzerbereitstellung {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Neue OIDC-Benutzer automatisch genehmigen, ohne dass eine manuelle Administrator-Genehmigung erforderlich ist (true/false). Standard ist `false`. Wenn aktiviert, werden Benutzer, die sich über den OIDC-Provider authentifizieren, sofort aktiviert und können auf die Anwendung zugreifen. Wenn deaktiviert (Standard), werden neue OIDC-Benutzer als inaktiv erstellt und erfordern eine manuelle Aktivierung durch einen Administrator. Weitere Informationen finden Sie unter [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users).
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, etc.: E-Mail-Adressen, die automatisch als Administratoren bereitgestellt werden sollen. Benutzer mit diesen E-Mail-Adressen werden beim ersten OAuth-Login als aktive Administratoren erstellt und umgehen den normalen Genehmigungsprozess. Der E-Mail-Abgleich erfolgt unabhängig von der Groß-/Kleinschreibung.

#### Einstellungen für passwortlosen Login {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Login mit Benutzername/Passwort deaktivieren (true/false). Standard ist `false`. Wenn aktiviert, wird das Passwortformular ausgeblendet **und** der Server lehnt jeden Passwort-POST ab, sodass nur der OAuth-Login verfügbar ist. Erfordert `OAuth__Enabled=true`. Wenn OAuth deaktiviert ist, hat diese Einstellung keine Auswirkung.
- `OAuth__AutoRedirect`: Benutzer automatisch zum OAuth-Provider umleiten (true/false). Standard ist `false`. Erfordert, dass `OAuth__DisablePasswordLogin` auf `true` gesetzt ist. Benutzer sehen einen kurzen Ladebildschirm, bevor sie umgeleitet werden.

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

Obwohl die Anwendung über einen Reverse Proxy mit HTTPS aufgerufen werden soll, können Sie auch den Kestrel-Webserver so konfigurieren, dass er SSL/TLS-Zertifikate verwendet. Dies bietet eine End-to-End-Verschlüsselung zwischen dem Reverse Proxy und dem Anwendungscontainer.

### Konfigurationsschritte {#configuration-steps}

1. **Generieren oder erhalten Sie ein SSL-Zertifikat** im PFX-Format (z. B. `localhost.pfx`)

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

5. **Platzieren Sie Ihre Zertifikatsdatei** (z. B. `localhost.pfx`) im `./certs`-Verzeichnis auf Ihrem Host-System.

### Erklärungen zu den Umgebungsvariablen {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: HTTP-Endpunkt-URL (Standard: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: HTTPS-Endpunkt-URL (Standard: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Pfad zur PFX-Zertifikatsdatei innerhalb des Containers
- `Kestrel__Endpoints__Https__Certificate__Password`: Passwort für die PFX-Zertifikatsdatei

> 💡 **Hinweis**: Diese Konfiguration ist optional. Wenn Sie einen Reverse Proxy mit HTTPS verwenden (empfohlen), kann die Kommunikation zwischen dem Reverse Proxy und der Anwendung auf HTTP bleiben. Für maximale Sicherheit in sensiblen Umgebungen möchten Sie jedoch möglicherweise auch HTTPS auf Kestrel aktivieren, um den gesamten Kommunikationspfad zu verschlüsseln.

## 🔑 Secrets Management für die Produktion {#secrets-management-for-production}

Das direkte Hardcodieren sensibler Daten wie Datenbankpasswörter, Admin-Anmeldedaten und OAuth-Client-Secrets in `docker-compose.yml` stellt ein Sicherheitsrisiko dar, insbesondere wenn die Datei in die Versionskontrolle übernommen wird. Der empfohlene Ansatz ist es, diese Werte in eine `.env`-Datei auszulagern, die Docker Compose automatisch lädt.

### 📄 Die `.env`-Datei {#the-env-file}

Erstellen Sie eine `.env`-Datei im selben Verzeichnis wie Ihre `docker-compose.yml` mit folgendem Inhalt:

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

**Wichtig**: Docker Compose liest die `.env`-Datei automatisch aus demselben Verzeichnis — Sie müssen sie **nicht** manuell referenzieren. Alle in `.env` definierten Variablen sind in `docker-compose.yml` über die `${VARIABLE_NAME}`-Syntax verfügbar.

### 🐳 Angepasste `docker-compose.yml` {#adapted-docker-composeyml}

Die `docker-compose.yml` aus den Installationsschritten sollte so angepasst werden, dass sie Platzhalter anstelle von hardcodierten Secrets verwendet:

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

- Verwenden Sie starke Passwörter und ändern Sie Standard-Anmeldedaten. Passwörter sollten mindestens 12 Zeichen lang sein und eine Mischung aus Großbuchstaben, Kleinbuchstaben, Zahlen und Sonderzeichen enthalten. Vermeiden Sie die Verwendung von gängigen Wörtern oder leicht zu erratenden Informationen.
- Erwägen Sie die Implementierung von HTTPS mit einem Reverse Proxy in der Produktionsumgebung.
- Regelmäßige Sicherungen der PostgreSQL-Datenbank werden empfohlen. Detaillierte Verfahren zur Sicherung und Wiederherstellung finden Sie im [Backup and Restore Guide](BackupRestore.md).
