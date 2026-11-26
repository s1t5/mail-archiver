# 🛠️ Guida alla configurazione di Mail Archiver {#mail-archiver-setup-guide}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida fornisce istruzioni dettagliate per la configurazione dell'applicazione Mail Archiver utilizzando Docker Compose.

## 🛠️ Prerequisiti {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Passaggi per l'installazione {#installation-steps}

1. Installa i prerequisiti sul tuo sistema.

2. Crea un file `docker-compose.yml` con il seguente contenuto:

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

3. Modifica la configurazione del database in `docker-compose.yml` e imposta una password sicura nella variabile `POSTGRES_PASSWORD` e in `ConnectionString`.

4. Se desideri utilizzare l'autenticazione (scelta fortemente raccomandata), definisci un `Authentication__Username` e `Authentication__Password` che viene utilizzato per l'utente admin.

5. Regola la variabile d'ambiente `TimeZone__DisplayTimeZoneId` per corrispondere al tuo fuso orario preferito (il valore predefinito è "Etc/UCT"). Puoi utilizzare qualsiasi identificatore di fuso orario IANA (ad es. "Europe/Berlin", "Asia/Tokyo").

6. Opzionalmente, configura le variabili d'ambiente `Logging__LogLevel` per controllare la verbosità dei log dell'applicazione. Consulta la sezione Logging Settings qui sotto per le opzioni disponibili.

7. Configura un reverse proxy a tua scelta con https e autenticazione per proteggere l'accesso all'applicazione. 

> ⚠️ **Attenzione**: L'applicazione stessa non fornisce l'accesso crittografato tramite https! Deve essere configurata tramite un reverse proxy!

8. Avvio iniziale dei container:
```bash
docker compose up -d
```

9. Riavvia i container:
```bash
docker compose restart
```

10. Accedi all'applicazione

11. Effettua il login con le tue credenziali definite e aggiungi il tuo primo Account email:
   - Naviga nella sezione "Email Accounts"
   - Clicca su "New Account"
   - Inserisci i dettagli del server e le credenziali
   - Salva e avvia l'Archivio!
   - Se desideri, crea altri utenti e assegna gli Account.

## 📚 Spiegazioni delle variabili d'ambiente {#environment-variable-explanations}

### 🗄️ Connessione al Database {#database-connection}
- `ConnectionStrings__DefaultConnection`: La stringa di connessione al database PostgreSQL. Modificare i valori `Host`, `Database`, `Username` e `Password` in base alle necessità.

### 🔐 Impostazioni di Autenticazione {#authentication-settings}
- `Authentication__Username`: Lo username per l'Account admin.
- `Authentication__Password`: La password per l'Account admin.
- `Authentication__SessionTimeoutMinutes`: Il timeout della sessione in minuti. Questo si applica al cookie di login non persistente; gli utenti che selezionano **Remember Me** nel modulo di login ottengono invece un cookie persistente valido per un periodo fisso di 7 giorni. Vedere [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: Il nome del cookie di autenticazione.
- `Authentication__CookieSameSite`: Configura l'attributo SameSite per i cookie di autenticazione, sessione e protezione CSRF. I valori validi sono:
  - `Strict` (default): Massima sicurezza. I cookie vengono inviati solo con richieste same-site. Ciò potrebbe causare problemi quando si naviga verso l'applicazione da link esterni (ad esempio, cliccando un link da un altro sito web), poiché la sessione esistente non verrà riconosciuta.
  - `Lax`: Raccomandato quando si utilizza un reverse proxy. I cookie vengono inviati con le navigazioni top-level e le richieste same-site, consentendo agli utenti di seguire link esterni verso l'applicazione mantenendo la protezione CSRF per le richieste POST.
  - `None`: I cookie vengono inviati con tutte le richieste. Richiede HTTPS e l'attributo `Secure`. Utilizzare questa opzione solo se si hanno requisiti cross-site specifici e si comprendono le implicazioni sulla sicurezza.

### 🔌 Impostazioni REST API {#rest-api-settings}

La REST API opzionale in sola lettura è **disabilitata per impostazione predefinita**. Consultare la [guida REST API](API.md) per il riferimento completo.
- `Api__Enabled`: Interruttore principale per la REST API in sola lettura (predefinito `false`). Quando `false`, tutte le rotte `/api/*` restituiscono `404`.
- `Api__AllowAttachmentDownloads`: Consente il download dei byte dell'Allegato tramite l'API (predefinito `true`). Quando `false`, l'endpoint dell'Allegato restituisce `403`.
- `Api__AllowMessageDownloads`: Consente il download di messaggi completi tramite l'API, sia il `/eml` ricostruito che l'originale `/raw` (predefinito `true`). Quando `false`, entrambi gli endpoint restituiscono `403`. Un messaggio completo porta con sé i propri Allegati, quindi **entrambi** questo interruttore e `Api__AllowAttachmentDownloads` devono essere `true`; disattivando uno dei due, entrambi gli endpoint vengono disabilitati.
- `Api__EnableSwaggerUi`: Espone Swagger UI a `/apidocs` e il documento OpenAPI a `/apidocs/spec/v1.json` (predefinito `true`). Entrambi richiedono una sessione browser effettuata con l'accesso.
- `Api__DefaultPageSize`: Dimensione predefinita della pagina per gli endpoint di elenco quando non specificata (predefinito `20`).
- `Api__MaxPageSize`: Dimensione massima della pagina consentita; le richieste più grandi vengono limitate (predefinito `100`).
- `Api__RateLimitPerMinute`: Budget di richiesta a finestra fissa per chiave API al minuto (predefinito `120`).

### 🤖 Impostazioni del server MCP {#mcp-server-settings}
Il server MCP (Model Context Protocol) opzionale espone lo stesso accesso alle email in sola lettura della REST API agli agenti AI tramite il trasporto Streamable HTTP. È **disabilitato per impostazione predefinita**. Consulta la [guida MCP](MCP.md) per il riferimento completo. MCP riutilizza le stesse identiche chiavi API della REST API (token bearer `ma_...`) e lo stesso scoping dell'accesso all'Account per utente.
- `Mcp__Enabled`: Interruttore principale per il server MCP (predefinito `false`). Quando `false`, l'endpoint `/mcp` restituisce `404`.
- `Mcp__AllowAttachmentDownloads`: Consente allo strumento `get_attachment` di restituire i byte dell'Allegato (predefinito `true`). Quando `false`, lo strumento restituisce un errore.
- `Mcp__DefaultPageSize`: Dimensione predefinita della pagina utilizzata da `search_emails` quando il chiamante omette `pageSize` (predefinito `20`).
- `Mcp__MaxResults`: Limite superiore per la dimensione della pagina di `search_emails`; i valori più grandi vengono limitati (predefinito `100`).
- `Mcp__MaxAttachmentBytes`: Dimensione massima dell'Allegato (in byte) che lo strumento `get_attachment` restituirà inline come base64; gli Allegati più grandi vengono rifiutati (predefinito `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Consente agli strumenti `get_email_eml` e `get_email_raw` di restituire messaggi completi (predefinito `true`). Quando `false`, gli strumenti restituiscono un errore. Un messaggio completo porta con sé i suoi Allegati, quindi **entrambi** questo interruttore e `Mcp__AllowAttachmentDownloads` devono essere `true`; disattivando uno dei due, entrambi gli strumenti vengono disabilitati.
- `Mcp__MaxMessageBytes`: Dimensione massima del messaggio (in byte) che gli strumenti `get_email_eml` e `get_email_raw` restituiranno inline; i messaggi più grandi vengono rifiutati (predefinito `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Budget di richiesta a finestra fissa per chiave API al minuto (predefinito `120`).

### 📨 Impostazioni MailSync {#mailsync-settings}
- `MailSync__IntervalMinutes`: L'intervallo in minuti tra le Sincronizzazione delle email. Questo è il valore predefinito globale; ogni Account può sovrascriverlo individualmente dalla pagina Crea/Modifica (lasciare vuoto per utilizzare questo valore predefinito).
- `MailSync__FullSyncIntervalHours`: Opzione globale predefinita per i riavvii completi automatici, in ore. Quando non impostato (il valore predefinito), non viene eseguita alcuna Sincronizzazione completa automatica a meno che non venga impostato un valore `FullSyncIntervalHours` per singolo Account nella pagina Crea/Modifica. I valori per singolo Account sovrascrivono questo valore predefinito globale.
- `MailSync__TimeoutMinutes`: Timeout di Sincronizzazione per singolo Account in minuti. Una Sincronizzazione che dura più a lungo si interrompe al successivo confine di messaggio, mantiene i suoi checkpoint e riprende alla successiva esecuzione; il lavoro viene segnalato come `Timed Out` e `LastSync` non viene avanzato. Si applica solo alle Sincronizzazioni pianificate; una Sincronizzazione avviata dall'interfaccia utente (Sincronizzazione manuale o riavvio completo) viene eseguita senza timeout. `0` (il valore predefinito) o qualsiasi valore non positivo significa nessun timeout. Controllare il valore prima dell'aggiornamento se si sincronizzano Caselle di posta di grandi dimensioni. Vedere [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Il timeout di connessione per le connessioni IMAP in secondi.
- `MailSync__CommandTimeoutSeconds`: Il timeout del comando per i comandi IMAP in secondi.
- `MailSync__AlwaysForceFullSync`: Se forzare sempre una Sincronizzazione completa (true/false).
- `MailSync__IgnoreSelfSignedCert`: Se ignorare i certificati auto-firmati (true/false).
- `MailSync__MaxConcurrentSyncs`: Quante Sincronizzazioni di Account possono essere eseguite contemporaneamente. Predefinito `1` (sequenziale, retrocompatibile). Lo scheduler riempie gli slot man mano che si liberano invece di attendere un intero batch, quindi una Casella di posta lenta non blocca più le altre. Aumentare per sincronizzare più Account concorrentemente — tenere conto dei limiti di frequenza del provider e dell'uso delle risorse locali. Vedere [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Ritardo di scaglionamento opzionale in secondi applicato alla fine di ogni attività di Sincronizzazione dell'Account. Predefinito `0` (nessun ritardo). Utile per evitare avvii a raffica quando `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Quanti problemi di ogni tipo un lavoro di Sincronizzazione ricorda per la pagina dell'Account. Cartelle fallite, cartelle mancanti e messaggi falliti sono preventivati separatamente, quindi un'ondata di un tipo non può espellere gli altri dalla visualizzazione; qualsiasi cosa oltre il budget viene conteggiata invece di essere conservata. Predefinito `20`. `0` disattiva il dettaglio e lascia solo i contatori. Vedere [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Cartelle escluse dalla Sincronizzazione per **ogni** Account, oltre alla lista delle cartelle escluse di ogni singolo Account. Vuoto per impostazione predefinita, quindi le configurazioni esistenti non ne sono influenzate. Le due liste sono **additive** — una cartella viene saltata se corrisponde a una delle due — e entrambe utilizzano le stesse regole di corrispondenza: corrispondenza esatta sul percorso completo, corrispondenza esatta sul nome della Cartella, una corrispondenza del suffisso del percorso (quindi `Drafts` corrisponde anche a `INBOX/Drafts` e `INBOX.Drafts`), e tutto ciò che sta sotto la cartella che un elemento nomina quando `MailSync__ExcludeSubfolders` è attivato. La corrispondenza non fa distinzione tra maiuscole e minuscole. Utile quando si importano molte Caselle di posta dallo stesso server, dove l'alternativa è mantenere una lista di esclusione identica su ogni Account. Nessuna Cartella è esclusa per impostazione predefinita; quali nomi valga la pena elencare dipende dal server e dalla sua lingua. Esempio per un albero di Caselle di posta che contiene anche cartelle di calendario e contatti:
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
- `MailSync__ExcludeSubfolders`: Se un elemento di esclusione copre anche le cartelle sotto quella che nomina. Si applica sia alla lista per singolo Account che a quella globale dell'installazione, e a entrambi i provider. Predefinito `false`, che corrisponde al comportamento precedente all'esistenza dell'opzione: un elemento `Deleted Items` corrisponde solo a quella Cartella, e un albero viene mantenuto fuori dall'Archivio nominando la sua radice e ogni Cartella sottostante. Impostare a `true` per far sì che un elemento includa anche l'albero sottostante — un elemento `Deleted Items` manterrà quindi anche `Deleted Items/2024` e il resto di quel ramo fuori dall'Archivio. La regola si ancora sul separatore di percorso, quindi prende intere Cartelle e mai parte di un nome: `Archive` copre `Archive` e `Archive/Team`, e lascia sola una cartella di posta chiamata `Team Archive Review`. Anche un `.` conta come separatore, nello stesso modo in cui la regola del suffisso del percorso lo tratta.

### 📧 Impostazioni di archiviazione dei messaggi originali {#original-message-storage-settings}

Per impostazione predefinita l'Archivio conserva solo le parti analizzate di un'email, quindi un `.eml` esportato è una ricostruzione: le codifiche di trasferimento, i confini MIME e il ripiegamento degli header differiscono dal messaggio ricevuto, e le firme DKIM non sono più verificabili. Con `RawMessages:Enabled` viene memorizzato anche l'originale completo.

| Parametro | Predefinito | Scopo |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Memorizza l'originale di ogni email appena archiviata. |
| `RawMessages__Storage` | `postgres` | Dove risiedono i byte: `postgres` o `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | I messaggi più grandi vengono archiviati senza il loro originale. `0` = nessun limite. |
| `RawMessages__IncludeGraph` | `true` | Memorizza gli originali anche per gli Account Microsoft 365 (Graph). |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Directory per i file dei messaggi. Deve puntare a un volume montato in Docker. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Livelli di directory su cui è distribuito l'id dell'email. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Verifica lo SHA-256 su ogni download. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Rimuove i file dei messaggi a cui nessuna email fa riferimento. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Frequenza con cui viene eseguita quella pulizia. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | I file più recenti di questo valore non vengono mai rimossi. |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Ritardo prima della prima pulizia dopo l'avvio, in modo che un import che sta ancora scrivendo non venga raccolto. |

!!! warning "Il backend del filesystem richiede un volume montato"
    Con `RawMessages__Storage=filesystem` i file dei messaggi risiedono all'esterno del database.
    Senza un bind mount, essi risiedono nello strato scrivibile del container e vengono persi alla successiva ricreazione del container, mentre le righe del database sopravvivono. Esegui il Backup della directory insieme al dump del database.

Vedi **[Original Message Storage](RawMessages.md)** per la descrizione completa: dove viene servito l'originale, i backend di archiviazione, come spostare i messaggi esistenti tra di essi con `--migrate-raw-storage` e cosa eseguire il Backup.

### 📈 Impostazioni di Monitoraggio {#monitoring-settings}

| Parametro | Predefinito | Scopo |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | Gli Account abilitati più vecchi di questo generano il report `Degraded` di `/api/v1/health`. `0` disabilita il controllo. |
| `Metrics__Enabled` | `false` | Abilita l'endpoint delle metriche di Prometheus e il relativo loop di aggiornamento. |
| `Metrics__RefreshMinutes` | `5` | Frequenza con cui viene ricostruito lo snapshot delle metriche. I valori inferiori a 1 vengono elevati a 1. |

Gli endpoint di health e metrics richiedono una chiave API admin e `Api__Enabled=true`. Il controllo di liveness del container stesso non richiede autenticazione e non necessita di configurazione. Consultare **[Health and Metrics](Monitoring.md)** per gli endpoint, i nomi delle metriche, la configurazione di Prometheus e il Docker health check.

### 📤 Impostazioni BatchRestore {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Il numero di email che attiva l'elaborazione asincrona.
- `BatchRestore__MaxSyncEmails`: Il numero massimo di email per l'elaborazione sincrona.
- `BatchRestore__MaxAsyncEmails`: Il numero massimo di email per l'elaborazione asincrona.
- `BatchRestore__SessionTimeoutMinutes`: Il timeout della sessione per il batch restore in minuti.
- `BatchRestore__DefaultBatchSize`: La dimensione del batch predefinita per le operazioni email.

### 🏢 Impostazioni di gestione del Tenant {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Numero massimo di Casella di posta che possono essere aggiunte in una singola operazione di gestione del Tenant. Il valore predefinito è `1000`. Aumentare questo valore per Tenant molto grandi, o diminuirlo per prevenire importazioni di massa accidentali. Quando il limite viene superato, l'operazione viene rifiutata con un errore di validazione e nessun Account viene creato. Consultare la [M365 Tenant Import Guide](M365TenantImport.md) per i dettagli.

### 🐄 Impostazioni Mailcow {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Numero massimo di Caselle di posta che possono essere configurate in una singola esecuzione di attivazione Mailcow. Il valore predefinito è `1000`. Riduci il valore per prevenire l'attivazione massiva accidentale, o aumentalo per istanze di grandi dimensioni. Quando il limite viene superato, non vengono creati Account. Vedere [Mailcow Instances](Mailcow.md) per i dettagli.
- L'integrazione con Mailcow richiede inoltre `Security__CredentialEncryptionKey` (vedere [Security Settings](#-security-settings)). La connessione di un'istanza e l'attivazione delle Caselle di posta vengono rifiutate se la crittografia delle credenziali è disabilitata, pertanto la chiave API e le app password generate non possono mai essere memorizzate in chiaro.
- È richiesto l'accesso in uscita all'API HTTPS di Mailcow e all'host IMAP tramite TLS (normalmente porta `993`).

### 📦 Impostazioni BatchOperation {#batchoperation-settings}
- `BatchOperation__BatchSize`: La dimensione del batch per le operazioni di email.
- `BatchOperation__PauseBetweenEmailsMs`: La pausa tra le singole email in millisecondi.
- `BatchOperation__PauseBetweenBatchesMs`: La pausa tra i batch in millisecondi.

### 📤 Impostazioni di Offload {#offload-settings}

Impostazioni per il [date-windowed offload](Offload.md). Tutti i valori predefiniti riproducono il comportamento che l'applicazione aveva prima dell'esistenza di questa funzione, quindi un'installazione che non configura questa sezione non ne risentirà.
- `Offload__MaxConcurrentJobs`: Quanti job di ripristino o offload possono essere eseguiti contemporaneamente. Il valore predefinito è `1`, che mantiene l'elaborazione strettamente seriale che la coda dei job ha sempre utilizzato. Al massimo un job per ogni Casella di posta di destinazione viene eseguito indipendentemente da questo valore.
- `Offload__PrefetchMaxMessages`: Limite superiore al numero di messaggi indicizzati da una Casella di posta di destinazione per la rilevazione dei duplicati. Il valore predefinito è `500000`. Al di sopra di questo valore, il controllo si restringe a una singola Cartella e registra che l'ambito è stato ridotto.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Cartelle sorgente che non vengono mai sottoposte a offload, corrispondenti prima della rinomina e che includono le sottocartelle. Vuote per impostazione predefinita.
- `Offload__FolderRenameMap__<SourceFolder>`: Riscrive i segmenti iniziali di un percorso di una cartella sorgente, ad esempio `Offload__FolderRenameMap__Sent Items=Sent`. Vuoto per impostazione predefinita.
- `Offload__MarkAsSeen`: Se la posta aggiunta viene contrassegnata come letta. Il valore predefinito è `true`, in linea con l'attuale comportamento di ripristino.

Entrambe le impostazioni della Cartella vengono fornite vuote appositamente: riscrivere o eliminare cartelle senza che venga richiesto sorprenderebbe chiunque stia già utilizzando il percorso di ripristino. Un'esecuzione senza configurazione, pertanto, migra tutto, incluse le cartelle spam, e crea un secondo set di cartelle speciali accanto a quelle della destinazione.

### 📊 Impostazioni di monitoraggio della larghezza di banda {#bandwidth-tracking-settings}

- `BandwidthTracking__Enabled`: Abilita o disabilita il monitoraggio della larghezza di banda per la gestione del limite di velocità IMAP (true/false). Il valore predefinito è `false`. Quando abilitato, il sistema monitora l'uso della larghezza di banda per Account e può mettere in pausa la Sincronizzazione quando vengono raggiunti i limiti del provider. Consultare [Rate Limit Handling](RateLimitHandling.md) per informazioni dettagliate.
- `BandwidthTracking__DailyLimitMb`: Limite di download giornaliero in megabyte per Account. Il valore predefinito è `25000` (25 GB). Per i provider con limiti di larghezza di banda, impostare questo valore in modo che corrisponda al loro limite di velocità (ad es. `2500` per i provider con limiti giornalieri di circa ~2500 MB). Il sistema metterà in pausa la sincronizzazione quando viene raggiunto questo limite.
- `BandwidthTracking__WarningThresholdPercent`: Percentuale del limite giornaliero alla quale vengono registrati i messaggi di avviso. Il valore predefinito è `80`. Quando l'uso della larghezza di banda raggiunge questa percentuale, vengono registrati avvisi per aiutare a monitorare l'avvicinamento ai limiti.
- `BandwidthTracking__PauseHoursOnLimit`: Numero di ore per mettere in pausa la Sincronizzazione quando viene raggiunto il limite giornaliero. Il valore predefinito è `24`. Dopo questo periodo, il flag del limite viene rimosso automaticamente e la sincronizzazione riprende.
- `BandwidthTracking__TrackUploadBytes`: Se monitorare anche la larghezza di banda in upload (true/false). Il valore predefinito è `false`. La maggior parte dei provider IMAP limita solo i download, quindi solitamente questo non è necessario.

### 🎯 Impostazioni di Selezione {#selection-settings}
- `Selection__MaxSelectableEmails`: Il numero massimo di email che possono essere selezionate contemporaneamente.

### 👁️ Visualizza Impostazioni (Privacy e Visualizzazione) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Controlla la modalità di visualizzazione predefinita delle email per la privacy e la prevenzione del tracciamento (true/false). Il valore predefinito è `false`.
  - Quando impostato su `true`: Le email si aprono in modalità testo semplice per impostazione predefinita, impedendo il caricamento automatico di pixel di tracciamento, immagini esterne e web beacon. Questo è consigliato per gli utenti preoccupati per il tracciamento delle email e la privacy.
  - Quando impostato su `false`: Le email si aprono in modalità HTML per impostazione predefinita, visualizzando l'email formattata con immagini e stile.
  - Gli utenti possono passare tra le visualizzazioni HTML e testo semplice utilizzando un pulsante nella pagina dei dettagli dell'email (visibile solo quando entrambi i formati sono disponibili).
  - Il link "Full View" rispetta la modalità di visualizzazione attualmente selezionata.

- `View__BlockExternalResources`: Blocca le risorse esterne (immagini remote, CSS esterni, script esterni, font web, ecc.) nelle visualizzazioni delle email HTML per prevenire il tracciamento e migliorare la privacy (true/false). Il valore predefinito è `false`.
  - Quando impostato su `true`: Le risorse esterne vengono filtrate durante la visualizzazione delle email HTML. Vengono visualizzati solo i contenuti inline e i data URI (inclusi gli Allegati inline tramite riferimenti `cid:`).
  - Quando impostato su `false`: Le email HTML vengono visualizzate con tutte le loro risorse esterne originali.
  - **Importante**: Questa impostazione influisce solo sulla **visualizzazione** delle email. Le email archiviate sono memorizzate completamente invariate nel database con tutto il contenuto originale preservato.
  - Le risorse bloccate includono:
    - Immagini remote (pixel di tracciamento, immagini esterne ospitate su server)
    - Fogli di stile CSS esterni
    - Font esterni tramite @font-face
    - Import CSS esterni tramite @import
    - Immagini di sfondo esterne
  - Risorse consentite:
    - Immagini inline incorporate come data: URIs
    - Allegati inline referenziati tramite cid: URIs
    - Stili CSS inline e tag style
  - Questa impostazione funziona indipendentemente da `DefaultToPlainText` e fornisce un ulteriore livello di protezione della privacy durante la visualizzazione delle email HTML.

### 📊 Impostazioni della Dashboard {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Se le schede dei contatori includono le loro parti in entrata e in uscita, e la scheda dell'Account il numero di domini distinti. Il valore predefinito è `true`. Impostato a `false`, tali numeri non vengono calcolati anziché calcolati e nascosti: i contatori leggono un conteggio semplice e il conteggio degli Allegati smette di unirsi alla mail a cui è collegato, che è l'unica query che queste parti aggiungono.
- `Dashboard__SelectablePeriods`: Se i grafici della dashboard offrono una risoluzione, un periodo e delle frecce per spostarlo. Il valore predefinito è `true`. Impostato a `false`, i grafici mostrano gli ultimi dodici mesi per mese e i mittenti dell'intero Archivio, l'endpoint del grafico risponde come se non esistesse e la data di invio più vecchia non viene cercata.
- `Dashboard__CacheSeconds`: Per quanto tempo le statistiche della dashboard calcolate (totali con le loro suddivisioni in entrata e in uscita, conteggi per Account, le serie del grafico, i principali mittenti, le email recenti, la dimensione del database) sono mantenute nella cache in memoria del server. Il valore predefinito è `60` secondi. Impostato a `0` per disabilitare la cache e ricalcolare sempre le statistiche. Ogni selezione del grafico per risoluzione e periodo è memorizzata singolarmente, quindi una dashboard che viene commutata tra le selezioni mantiene una voce per ogni selezione. Valori più elevati riducono il carico del database in ambienti ampi a costo di numeri meno aggiornati. I badge dello stato di Sincronizzazione, i valori di archiviazione e l'elenco delle email recenti vengono sempre recuperati in tempo reale e non sono influenzati da questa cache.
- `Dashboard__RefreshIntervalMinutes`: Quanto spesso il servizio in background `DashboardStatsRefreshService` ricalcola gli aggregati costosi della dashboard (conteggi totali con le loro suddivisioni in entrata e in uscita, conteggi per Account, il numero di domini dell'Account, la serie di grafici predefinita con i suoi principali mittenti, la dimensione del database) nella tabella del database `DashboardStatsCache`. Nelle installazioni con diversi milioni di email, la dashboard legge questi valori preparati invece di aggregare l'intero Archivio nel percorso della richiesta, il che trasforma un primo caricamento di circa 10 secondi in millisecondi. L'endpoint del grafico risponde alla selezione predefinita (mesi su un anno, mittenti in entrata, il periodo corrente) dalla stessa riga, quindi tornare ad essa mostra i numeri del primo caricamento; ogni altra selezione viene calcolata su richiesta e mantenuta nella cache in memoria sopra descritta. Il valore predefinito è `15` minuti. Impostato a `0` per disabilitare l'aggiornamento in background; la dashboard calcolerà quindi nuovamente le statistiche in tempo reale (con la cache in memoria sopra ancora applicata). I valori mostrati possono essere in ritardo fino all'intervallo configurato rispetto allo stato effettivo dell'Archivio. Una riga calcolata con valori `ShowDirectionSplits` o `SelectablePeriods` diversi da quelli attuali non viene utilizzata; la dashboard calcola in tempo reale fino al prossimo aggiornamento.

### 🔄 Impostazioni dei Job {#jobs-settings}

- `Jobs__RefreshSeconds`: Con quale frequenza la pagina dei job in background si ricarica automaticamente mentre una scheda del browser la tiene aperta. Il valore predefinito è `30` secondi. Impostare a `0` per disattivare il ricaricamento automatico e aggiornare manualmente. Ogni ricaricamento ricostruisce la pagina da tutte le otto sorgenti dei job, quindi nelle installazioni con molti Account un intervallo più lungo mantiene basso il carico, moltiplicato per ogni scheda aperta sulla pagina.

### 🗃️ Impostazioni Npgsql {#npgsql-settings}
- `Npgsql__CommandTimeout`: Il timeout per i comandi del database in secondi.

### 📥 Impostazioni di caricamento {#upload-settings}
- `Upload__MaxFileSizeGB`: La dimensione massima del file per i caricamenti in GB.
- `Upload__KeepAliveTimeoutHours`: Il timeout di keep alive per i caricamenti in ore.
- `Upload__RequestHeadersTimeoutHours`: Il timeout per gli header della richiesta in ore.

### 📂 Impostazioni di Importazione Locale {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, ecc.: White list di directory locali dalle quali i comandi di importazione CLI (`--import-mbox`, `--import-eml`) sono autorizzati a leggere i file. Ogni voce è un percorso all'interno del container. È necessario montare i file di importazione in una di queste directory utilizzando i volumi Docker. Predefinito: `/data/import` (impostato automaticamente su `/app/uploads` come fallback).
  - Quando si utilizza `docker exec` per eseguire il comando di importazione, il percorso del file fornito tramite `--file` deve trovarsi all'interno di uno di questi percorsi consentiti.
  - Questa misura di sicurezza impedisce l'accesso arbitrario al file system dai comandi CLI.
  - È possibile configurare percorsi multipli per diverse sorgenti di importazione.
  - Consultare i [comandi CLI](CLI.md) per istruzioni dettagliate sull'uso.

### 📄 Impostazioni Importazione CSV {#csv-import-settings}
- `CsvImport__MaxRows`: Numero massimo di righe CSV (Caselle di posta) elaborate in un singolo importazione massiva. Il valore predefinito è `5000`. Aumentare questo valore per implementazioni di grandi dimensioni; diminuirlo per limitare l'impatto di un singolo ciclo di importazione sul carico del database.
- `CsvImport__MaxFileSizeBytes`: Dimensione massima consentita (in byte) del file CSV caricato. Il valore predefinito è `10000000` (10 MB). Regolare questo valore per corrispondere ai propri limiti di caricamento, se necessario.
- Consultare la [Guida all'importazione dell'Account](Account-Import.md) per istruzioni dettagliate sull'uso dell'importazione massiva di Account IMAP tramite CSV.

### 📤 Impostazioni di esportazione del Registro di controllo {#audit-export-settings}
Impostazioni per la pagina di esportazione dei dati del Registro di controllo (solo admin, raggiungibile dalla pagina Logs). L'esportazione genera un pacchetto ZIP con dati massivi tabulari (INDEX.XML + tabelle CSV + DTD) dall'Archivio esistente per strumenti di audit esterni. Consultare la [Guida all'esportazione dei dati di audit](AuditExport.md) per i dettagli sull'uso.
- `AuditExport__DataSupplierName`: Valore predefinito per il campo del modulo "Nome del fornitore dati" che identifica la vostra organizzazione nel file di indice esportato. Predefinito: vuoto.
- `AuditExport__DataSupplierLocation`: Valore predefinito per il campo del modulo "Località del fornitore dati" (es. sede della società). Predefinito: vuoto.
- `AuditExport__Comment`: Valore predefinito per il campo del modulo "Commento", incluso come testo libero nel file di indice esportato. Predefinito: vuoto.
- `AuditExport__OutputDirectory`: Directory in cui vengono memorizzati i file ZIP generati. I percorsi relativi sono risolti rispetto alla root del contenuto dell'app. Predefinito: `exports/audit`.
- `AuditExport__RetentionDays`: Numero di giorni dopo i quali i file di esportazione completati vengono eliminati dalla pulizia giornaliera. Predefinito: `30`.
- `AuditExport__MaxRangeYears`: Intervallo massimo consentito tra la data di inizio e la data di fine del periodo di esportazione. Predefinito: `10`.
- I campi del modulo sono precompilati con questi valori predefiniti ma possono essere modificati per ogni esportazione.
- Ogni esportazione scrive una voce di inizio e una voce di risultato nel registro di accesso (tipo "Audit Data Export"), in modo che la cronologia sia sicura rispetto alle revisioni senza alcuna modifica allo schema del DB.

### 🔒 Impostazioni della Policy di Eliminazione {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Controlla se l'eliminazione manuale delle email archiviate è consentita (true/false). Il valore predefinito è `true`. Quando impostato su `false`:
  - Tutte le email archiviate sono bloccate (`IsLocked = true`) all'avvio tramite il database compliance trigger, impedendo qualsiasi modifica o eliminazione a livello di database.
  - L'eliminazione manuale (singola e massiva) è bloccata a livello di applicazione con un messaggio di errore.
  - Il valore predefinito della colonna viene regolato in modo che anche le email importate di recente siano bloccate.
  - Lo stato attuale della policy viene registrato nella tabella AccessLogs ad ogni avvio (visibile nella pagina Logs come voci "Deletion Policy") per fini di audit.
  - L'eliminazione per la Conservazione locale è esente: le email che rientrano in un periodo di Conservazione configurato vengono comunque eliminate (vengono sbloccate immediatamente prima dell'eliminazione all'interno del processo di Conservazione).
- `DeletionPolicy__MaxBulkDeleteEmails`: Limite superiore per un'eliminazione massiva basata su un filtro (*Delete all matches*). Il valore predefinito è `100000`. Un filtro che corrisponde a più email viene rifiutato con un messaggio che richiede di restringere i criteri; il job non tronca mai silenziosamente i risultati. Le email bloccate dalla policy di Conservazione vengono saltate e segnalate nello stato del job, non sbloccate. Il job ricontrolla questo limite e `DeletionPolicy__DeletionAllowed` all'avvio, quindi un job in coda prima di un cambiamento della policy si interrompe invece di essere eseguito con l'impostazione precedente.
- **Protezione dell'immutabilità:** Quando `IsLocked = true`, il database compliance trigger (`prevent_locked_email_changes`) blocca QUALSIASI modifica alla riga dell'email — tutte le colonne sono protette, non solo un elenco di campi fissi. Le uniche colonne esenti sono `IsLocked` stessa (per consentire lo sblocco per l'eliminazione per Conservazione e l'applicazione della policy all'avvio) e `FolderName` (per consentire la Sincronizzazione IMAP di aggiornare la Cartella quando un'email viene spostata lato server). La protezione è indipendente dalle colonne (confronto basato su JSONB) e copre automaticamente futuri inserimenti nello schema.

### 🕐 Impostazioni TimeZone {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: La zona temporale utilizzata per visualizzare i timestamp delle email nell'interfaccia utente. Utilizza gli identificatori della zona temporale IANA (ad es. "Europe/Berlin", "Asia/Tokyo"). Il valore predefinito è "Etc/UCT" per la compatibilità con le versioni precedenti. Durante l'importazione, i timestamp delle email verranno convertiti in questa zona temporale per fini di visualizzazione.

### 🎉 Impostazioni ReleaseNotes (Schermata di aggiornamento versione) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: La stringa della versione che questa istanza riporta. Viene confrontata con i tag di rilascio di GitHub e decide se esiste un rilascio più recente. Quando è vuota, il changelog ripiega sulla versione dell'assembly compilato e l'indicazione di aggiornamento rimane disattivata.
- `ReleaseNotes__Enabled`: Abilita o disabilita la schermata di aggiornamento della versione (true/false). Il valore predefinito è `true`. Quando abilitata, gli amministratori vedranno una finestra modale del changelog una tantum dopo un aggiornamento dell'applicazione, mostrando le note di rilascio recuperate da GitHub Releases per la versione corrente. Ogni amministratore può chiudere la modale, e questa riapparirà solo per una nuova versione. Imposta a `false` per disabilitare completamente questa funzione, inclusa l'indicazione di aggiornamento.
- `ReleaseNotes__UpdateHintEnabled`: Mostra agli amministratori un piccolo toast nell'angolo in basso a destra quando GitHub pubblica un rilascio stabile più recente rispetto alla versione installata (true/false). Il valore predefinito è `true`. Il toast rimanda a quel rilascio ed è rimosso per versione nel browser. Draft e prerelease vengono ignorati. Richiede `ReleaseNotes__AppVersion`; senza di esso non viene mostrata alcuna indicazione.
- `ReleaseNotes__UpdateCheckIntervalHours`: Ogni quanto viene aggiornata la lista dei rilasci di GitHub, in ore. Il valore predefinito è `6`. Un intervallo più breve rende un nuovo rilascio visibile prima in un'istanza a lunga durata, uno più lungo riduce le chiamate alle API di GitHub. Il minimo è `1`.
- `ReleaseNotes__RequestTimeoutSeconds`: Timeout per una richiesta API di GitHub, in secondi. Il valore predefinito è `10`. Quando scade, il changelog e l'indicazione di aggiornamento rimangono disattivati per quel tentativo invece di mantenere aperta la richiesta; il controllo successivo riprova.

### 🔧 Impostazioni di Manutenzione del Database {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Abilita o disabilita la manutenzione automatica giornaliera del database (true/false). Il valore predefinito è `false`. Quando abilitata, il sistema eseguirà automaticamente le operazioni VACUUM ANALYZE per ottimizzare le prestazioni del database e prevenire il bloat. Consulta la [Guida alla Manutenzione del Database](DatabaseMaintenance.md) per ulteriori dettagli.
- `DatabaseMaintenance__DailyExecutionTime`: L'ora del giorno in cui deve essere eseguita la manutenzione del database, in formato 24 ore (HH:mm). Il valore predefinito è `02:00`. Scegli un orario durante i periodi di bassa attività del sistema.
- `DatabaseMaintenance__TimeoutMinutes`: Tempo massimo consentito per le operazioni di manutenzione in minuti. Il valore predefinito è `30`. Aumenta questo valore per database più grandi.

### 🧬 Impostazioni di Deduplicazione degli Allegati {#attachment-deduplication-settings}
La Deduplicazione degli Allegati memorizza ogni payload di Allegato univoco una sola volta (indirizzato tramite contenuto tramite SHA-256) ed è una **funzionalità principale che è sempre abilitata** – non esiste intenzionalmente un interruttore on/off. Solo i parametri di batch/pianificazione sottostanti possono essere regolati. Consultare la [Guida alla Deduplicazione degli Allegati](AttachmentDeduplication.md) per tutti i dettagli.

- `AttachmentDeduplication__BatchSize`: Numero di Allegati esistenti migrati per transazione durante la migrazione in background una tantum dei dati preesistenti. Il valore predefinito è `200`. Valori più elevati migrano più velocemente ma utilizzano più memoria/carico DB per batch.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Pausa opzionale (in millisecondi) tra i batch di migrazione per limitare il carico sul database su sistemi occupati. Il valore predefinito è `0` (nessuna pausa).
- `AttachmentDeduplication__StartupDelaySeconds`: Ritardo (in secondi) dopo l'avvio dell'applicazione prima che inizi la migrazione in background, per dare tempo alla migrazione dello schema di completarsi. Il valore predefinito è `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Intervallo (in ore) della garbage collection sempre attiva che rimuove i payload degli Allegati non più referenziati da alcuna email. Il valore predefinito è `12`. Questa operazione viene eseguita indipendentemente da `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Timeout del comando del database (in secondi) per le operazioni di batch di migrazione (INSERT con hashing SHA-256 e UPDATE). Il valore predefinito è `300` (5 minuti). Aumentare questo valore per database o Allegati molto grandi, o diminuirlo se si desidera un rilevamento dei guasti più rapido. Se un batch va ancora in timeout, il servizio riprova automaticamente con la metà della dimensione del batch.

### 💾 Impostazioni di archiviazione dell'Account {#account-storage-settings}

La visualizzazione dell'archiviazione per Account mostra l'utilizzo dell'archiviazione del database (tutti i campi mail + Allegati) per ogni Account nella tabella "Account Overview" della Dashboard e nella tabella "Show All" di MailAccounts. Un servizio di background autarke (`AccountStorageRefreshService`) calcola i valori sommando i PostgreSQL `pg_column_size` per colonna (coprendo tutti i campi di una mail senza leggere i corpi delle mail) e li memorizza nella tabella `AccountStorageCache`. Questo servizio viene eseguito indipendentemente da `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Abilita o disabilita il servizio di aggiornamento dell'archiviazione (true/false). Il valore predefinito è `true`. Quando abilitato, il servizio esegue un backfill riprendibile degli Account in sospeso all'avvio (sicuro in caso di crash tramite `AccountStorageBackfillState`) e un aggiornamento completo giornaliero successivamente. Quando disabilitato, i valori di archiviazione vengono aggiornati solo quando le email vengono sottoposte a Sincronizzazione, importazione o eliminazione, ma i valori visualizzati potrebbero diventare obsoleti nel tempo.
- `AccountStorage__DailyExecutionTime`: Orario del giorno (formato 24 ore `HH:mm`) per l'aggiornamento completo giornaliero di tutti gli Account. Il valore predefinito è `02:30`. Scegli un orario durante i periodi di bassa attività del sistema.
- `AccountStorage__BackfillDelayMs`: Ritardo (in millisecondi) tra gli Account durante il backfill iniziale all'avvio. Il valore predefinito è `5000`. Valori più bassi velocizzano il backfill ma aumentano il carico del database; aumenta questo valore per Archivio molto grandi per evitare di sovraccaricare il database.
- `AccountStorage__RefreshBatchDelayMs`: Ritardo (in millisecondi) tra gli Account durante l'aggiornamento completo giornaliero. Il valore predefinito è `1000`. Valori più bassi velocizzano l'aggiornamento ma aumentano il carico del database; aumenta questo valore per Archivio molto grandi.
- `AccountStorage__CommandTimeoutSeconds`: Timeout del comando del database (in secondi) per il calcolo dell'archiviazione per Account (`pg_column_size` su tutte le righe di un Account). Il valore predefinito è `300` (5 minuti). Il calcolo legge ogni riga mail inclusi i dati TOAST, il che può richiedere molto più di un minuto su Archivio molto grandi (oltre 250k email); il precedente valore predefinito Npgsql di 30 secondi causava il fallimento silenzioso dell'aggiornamento e lasciava la dashboard con valori obsoleti. Aumenta questo valore se il calcolo va ancora in timeout sul tuo Archivio.

> 💡 **Nota**: I valori di archiviazione vengono aggiornati immediatamente dopo ogni Sincronizzazione, importazione o eliminazione per Conservazione delle mail, quindi i valori visualizzati rimangono aggiornati anche senza l'aggiornamento giornaliero. L'aggiornamento giornaliero è una rete di sicurezza che cattura i casi limite (ad es. modifiche dirette al database).

### 📝 Impostazioni di Logging {#logging-settings}
- `Logging__LogLevel__Default`: Il livello di log predefinito per l'applicazione. I livelli disponibili sono: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Il valore predefinito è `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Livello di log per i messaggi del framework ASP.NET Core. Il valore predefinito è `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Livello di log per i comandi del database Entity Framework. Il valore predefinito è `Warning`.

### 🛡️ Impostazioni di Sicurezza {#security-settings}
- `AllowedHosts`: Un elenco separato da punti e virgola di nomi host che l'applicazione è autorizzata a servire. Questo aiuta a prevenire attacchi HTTP Host header. Esempio: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Importante**: Non utilizzare `*` in ambienti di produzione poiché disabilita la validazione dell'host header.

#### 🔐 Crittografia delle Credenziali a Riposo {#credential-encryption-at-rest}
Le credenziali dell'Account Mail (password IMAP, client secret M365 e token MSA OAuth) possono essere crittografate nel database. La chiave di crittografia è conservata **all'esterno** del database (variabile d'ambiente / Docker secret), pertanto un dump del database da solo non è sufficiente per leggere le credenziali.

- `Security__CredentialEncryptionKey`: Chiave di crittografia a 256 bit (32 byte) codificata in Base64. Generane una con:
  ```bash
  openssl rand -base64 32
  ```
  Imposta il valore come variabile d'ambiente (consigliato: `Security__CredentialEncryptionKey` nel tuo file compose o nell'ambiente del container). L'applicazione non supporta i Docker secret in stile `_FILE` — se utilizzi un Docker secret, montalo e carica il suo contenuto nella variabile d'ambiente nel tuo entrypoint. Non caricare mai la chiave su `appsettings.json`. Quando una chiave è configurata, le credenziali vengono crittografate in fase di scrittura e un servizio in background crittografa una sola volta le credenziali esistenti in chiaro.
- `Security__EncryptAccountCredentials`: Abilita o disabilita la crittografia dei nuovi/aggiornati valori delle credenziali (true/false). Il valore predefinito è `true`. Quando non è configurata alcuna chiave, le credenziali sono memorizzate in chiaro e viene registrato un avviso all'avvio — le installazioni esistenti non vengono influenzate finché non viene fornita una chiave.
- `Security__CredentialEncryptionBackfill`: Se il servizio in background debba crittografare le credenziali esistenti in chiaro all'avvio (true/false). Il valore predefinito è `true`. Nessuna operazione senza una chiave configurata.
- `Security__CredentialEncryptionKeyPrevious`: Chiave precedente opzionale, utilizzata solo per decriptare i valori crittografati con essa (per la rotazione delle chiavi). I nuovi valori sono sempre crittografati con `Security__CredentialEncryptionKey`.

  > ⚠️ **Rotazione delle chiavi**: dopo la rotazione, mantieni `Security__CredentialEncryptionKeyPrevious` configurata **finché ogni credenziale non è stata riscritta** (ad esempio modificando l'Account o tramite un aggiornamento automatico del token OAuth) — i valori esistenti crittografati con la vecchia chiave *non* vengono ricriptati automaticamente con la nuova chiave. Una volta rimossa la chiave precedente, qualsiasi credenziale ancora crittografata con essa diventa illeggibile e deve essere inserita nuovamente. Finché è configurata, deve essere mantenuta sicura quanto la chiave corrente.

> ⚠️ **Mantieni la chiave al sicuro**: Senza la chiave, le credenziali crittografate non possono essere recuperate e devono essere inserite nuovamente. Effettua un Backup insieme (ma separatamente dal) Backup del tuo database.
>
> 💡 **Installazioni esistenti**: Aggiorna in sicurezza — finché non viene impostata alcuna `Security__CredentialEncryptionKey`, nulla cambia. Imposta la chiave quando sei pronto; le credenziali esistenti in chiaro rimangono leggibili e vengono crittografate in background.

### 🔐 Configurazione OIDC {#oidc-configuration}

Per istruzioni dettagliate sulla configurazione dell'autenticazione OpenID Connect, consultare la [Guida all'implementazione OIDC](OIDC_Implementation.md).

#### Impostazioni OIDC di base {#basic-oidc-settings}
- `OAuth__Enabled`: Abilita o disabilita l'autenticazione OIDC (true/false)
- `OAuth__Authority`: L'URL dell'autorità OpenID Connect (ad es. https://sts.windows.net/{TENANT-ID}/ per Azure AD)
- `OAuth__ClientId`: Il client ID assegnato dal proprio identity provider
- `OAuth__ClientSecret`: Il client secret assegnato dal proprio identity provider
- `OAuth__DisplayName`: Nome visualizzato opzionale mostrato sul pulsante di login OIDC e sulla pagina di reindirizzamento automatico (ad es. `PocketID SSO`). Se omesso, viene utilizzata l'etichetta generica "Login with OAuth".
- `OAuth__ClientScopes__0`: Primo scope richiesto all'identity provider (openid)
- `OAuth__ClientScopes__1`: Secondo scope richiesto all'identity provider (profile)
- `OAuth__ClientScopes__2`: Terzo scope richiesto all'identity provider (email)
- `OAuth__ResponseMode`: Modalità di risposta opzionale della richiesta di autorizzazione (`query`, `fragment` o `form_post`). Il valore predefinito è `form_post`, il default di ASP.NET Core. Impostare su `query` per i provider che non supportano `form_post`, come Synology SSO Server. Consultare [Compatibilità del provider](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Approva automaticamente i nuovi utenti OIDC senza richiedere l'approvazione manuale dell'amministratore (true/false). Il valore predefinito è `false`.

#### Impostazioni di provisioning degli utenti {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Approva automaticamente i nuovi utenti OIDC senza richiedere l'approvazione manuale dell'amministratore (true/false). Il valore predefinito è `false`. Quando abilitato, gli utenti che si autenticano tramite il provider OIDC vengono immediatamente attivati e possono accedere all'applicazione. Quando disabilitato (predefinito), i nuovi utenti OIDC vengono creati come inattivi e richiedono l'attivazione manuale da parte di un amministratore. Consultare [Approvazione automatica utenti OIDC](OIDC_Implementation.md#auto-approve-oidc-users) per informazioni dettagliate.
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, ecc.: Indirizzi email che dovrebbero essere configurati automaticamente come amministratori. Gli utenti con questi indirizzi email verranno creati come amministratori attivi al primo login OAuth, bypassando il normale processo di approvazione. Il confronto degli indirizzi email non è case-sensitive.

#### Impostazioni di login senza password {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Disabilita il login con username/password (true/false). Il valore predefinito è `false`. Quando abilitato, il modulo della password viene nascosto **e** il server rifiuta ogni POST della password, quindi è disponibile solo il login OAuth. Richiede `OAuth__Enabled=true`. Se OAuth è disabilitato, questa impostazione non ha effetto.
- `OAuth__AutoRedirect`: Reindirizza automaticamente gli utenti al provider OAuth (true/false). Il valore predefinito è `false`. Richiede che `OAuth__DisablePasswordLogin` sia impostato su `true`. Gli utenti vedranno una breve schermata di caricamento prima di essere reindirizzati.

#### Esempio: Configurazione completa "OIDC-First" {#example-full-oidc-first-configuration}
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

## 🔐 Configurazione HTTPS di Kestrel (Opzionale) {#kestrel-https-configuration-optional}

Sebbene l'applicazione sia pensata per essere accessata tramite un reverse proxy con HTTPS, è possibile configurare anche il web server Kestrel per utilizzare certificati SSL/TLS. Ciò fornisce una crittografia end-to-end tra il reverse proxy e il container dell'applicazione.

### Passaggi di Configurazione {#configuration-steps}

1. **Generare o ottenere un certificato SSL** in formato PFX (ad es. `localhost.pfx`)

2. **Aggiungere le seguenti variabili d'ambiente** al proprio `docker-compose.yml` per il servizio `mailarchive-app`:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Aggiornare la mappatura delle porte** nel servizio `mailarchive-app`:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Aggiungere una mappatura di un volume** per il certificato:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Posizionare il file del certificato** (ad es. `localhost.pfx`) nella directory `./certs` sul proprio sistema host.

### Spiegazione delle Variabili d'Ambiente {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: URL dell'endpoint HTTP (predefinito: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: URL dell'endpoint HTTPS (predefinito: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Percorso al file del certificato PFX all'interno del container
- `Kestrel__Endpoints__Https__Certificate__Password`: Password per il file del certificato PFX

> 💡 **Nota**: Questa configurazione è opzionale. Se si utilizza un reverse proxy con HTTPS (scelta raccomandata), la comunicazione tra il reverse proxy e l'applicazione può rimanere in HTTP. Tuttavia, per la massima sicurezza in ambienti sensibili, è possibile abilitare HTTPS anche su Kestrel per crittografare l'intero percorso di comunicazione.

## 🔑 Gestione dei Segreti per la Produzione {#secrets-management-for-production}

Inserire dati sensibili come password del database, credenziali admin e client secret OAuth direttamente in `docker-compose.yml` è un rischio per la sicurezza, specialmente quando il file viene inserito nel controllo versione. L'approccio raccomandato è quello di esternalizzare questi valori in un file `.env` che Docker Compose carica automaticamente.

### 📄 Il File `.env` {#the-env-file}

Crea un file `.env` nella stessa directory del tuo `docker-compose.yml` con il seguente contenuto:

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

**Importante**: Docker Compose legge automaticamente il file `.env` dalla stessa directory — non è necessario fare riferimento ad esso manualmente. Tutte le variabili definite in `.env` sono disponibili in `docker-compose.yml` tramite la sintassi `${VARIABLE_NAME}`.

### 🐳 `docker-compose.yml` Adattato {#adapted-docker-composeyml}

Il `docker-compose.yml` dei passaggi di installazione deve essere adattato per utilizzare segnaposto invece di segreti codificati:

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

### 📋 Template `.env.example` per gli Amministratori {#envexample-template-for-administrators}

Crea un file `.env.example` nel tuo repository che funga da template per altri amministratori. Contiene valori segnaposto e documentazione ma **nessun segreto reale**:

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

- **Usa `.env.example` come template**
- **Imposta Autorizzazioni file rigorose** — Limita l'accesso al file `.env`:
  ```bash
  chmod 600 .env
  ```
- **Usa password forti** — Genera password lunghe e casuali (almeno 16 caratteri) con un mix di lettere, numeri e caratteri speciali.
- **Limita l'accesso al file `.env`** — Solo l'utente che esegue Docker Compose deve avere le Autorizzazioni di lettura per il file `.env`.

## 🔒 Note sulla sicurezza {#security-notes}

- Utilizzare password forti e cambiare le credenziali predefinite. Le password dovrebbero avere una lunghezza di almeno 12 caratteri e includere un mix di lettere maiuscole, lettere minuscole, numeri e caratteri speciali. Evitare l'uso di parole comuni o informazioni facilmente indovinabili.
- Considerare l'implementazione di HTTPS con un reverse proxy in produzione
- Si raccomandano Backup regolari del database PostgreSQL. Per procedure dettagliate di Backup e Ripristino, consultare la [Guida al Backup e al Ripristino](BackupRestore.md).
