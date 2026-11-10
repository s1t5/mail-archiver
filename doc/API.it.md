# Read-Only REST API (v1) {#read-only-rest-api-v1}

Mail Archiver include un'opzionale REST API **read-only** per l'accesso programmatico alla posta archiviata. È progettata per casi d'uso di automazione e integrazione (reporting, esportazioni e-discovery, monitoraggio) senza passare attraverso l'interfaccia web.

L'obiettivo è consentire a uno script o a un agente AI di leggere la posta archiviata **senza mai esporre le credenziali della Casella di posta o comunicare con il provider di posta.** Una chiave API per utente con ambito limitato è una capacità per *leggere l'Archivio*, non una credenziale per *essere la Casella di posta* — eredita solo le Autorizzazione della Casella di posta del proprietario, non concede percorsi di scrittura e può essere revocata istantaneamente senza toccare l'Account di posta sottostante.

> ⚠️ **L'API è disabilitata per impostazione predefinita.** Deve essere esplicitamente abilitata nella configurazione (vedere [Enabling the API](#enabling-the-api)). Quando è disabilitata, ogni percorso `/api/*` restituisce `404 Not Found`.

L'API è **read-only per progettazione**: espone Account, Cartella, ricerca messaggi, dettagli del messaggio e download di Allegato. Non crea, modifica o elimina mai dati.

## Indice {#table-of-contents}

- [Abilitazione dell'API](#enabling-the-api)
- [Autenticazione](#authentication)
  - [Formato della chiave API](#api-key-format)
  - [Ciclo di vita della chiave](#key-lifecycle)
  - [Modello di Autorizzazione](#permission-model)
  - [Razionale di sicurezza](#security-rationale)
- [Convenzioni](#conventions)
  - [URL di base e versionamento](#base-url-and-versioning)
  - [Paginazione](#pagination)
  - [Errori](#errors)
  - [Limitazione del tasso](#rate-limiting)
- [Endpoint](#endpoints)
  - [Elenca Account](#list-accounts)
  - [Elenca Cartelle](#list-folders)
  - [Cerca messaggi](#search-messages)
  - [Ottieni messaggio](#get-message)
  - [Scarica Allegato](#download-attachment)
  - [Scarica messaggio (.eml)](#download-message-eml)
  - [Scarica messaggio originale](#download-original-message)
  - [Statistiche](#stats)
- [Tipi di dati](#data-types)
- [Registrazione dell'accesso](#access-logging)
- [OpenAPI e Swagger UI](#openapi-and-swagger-ui)
- [Riferimento API](API-Reference.md) — riferimento generato per endpoint e tipi di dati

## Abilitazione dell'API {#enabling-the-api}

L'API è configurata sotto la sezione `Api` di `appsettings.json` (o le
variabili d'ambiente equivalenti, ad es. `Api__Enabled=true`):

```json
"Api": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "AllowMessageDownloads": true,
  "EnableSwaggerUi": true,
  "DefaultPageSize": 20,
  "MaxPageSize": 100,
  "RateLimitPerMinute": 120
}
```


### Ciclo di vita della chiave {#key-lifecycle}

1. **Creazione** — un utente crea una chiave con un `Name` descrittivo e una scadenza
   opzionale. La risposta mostra la chiave in chiaro una sola volta.
2. **Utilizzo** — invia la chiave come bearer token. Ogni richiesta accettata aggiorna il `LastUsedAt` della
   chiave (le scritture sono limitate a al massimo una volta ogni ~5 minuti per
   evitare una scrittura ad ogni chiamata).
3. **Scadenza** — se `ExpiresAt` è impostato e nel passato, la chiave viene rifiutata.
4. **Revoca** — l'utente proprietario o un amministratore revoca la chiave
   (`RevokedAt` è impostato); viene immediatamente e permanentemente rifiutata.

Una chiave è **attiva** quando non è revocata, non è scaduta **e** il suo utente proprietario
è ancora attivo. Una chiave inattiva per uno qualsiasi di questi motivi viene rifiutata con `401`.

### Modello di Autorizzazione {#permission-model}

Una chiave API eredita le esatte Autorizzazione dell'utente che la possiede. Il
principale autenticato trasporta gli stessi claim che un normale login web emette, quindi
tutte le esistenti autorizz
| Impostazione | Predefinito | Descrizione |
| --- | --- | --- |
| `Enabled` | `false` | Interruttore principale. Quando `false`, tutte le rotte `/api/*` restituiscono `404` e il documento OpenAPI / Swagger UI non sono mappati. |
| `AllowAttachmentDownloads` | `true` | Quando `false`, l'endpoint di download dell'Allegato restituisce `403`. I metadati relativi agli Allegati sono ancora restituiti dall'endpoint dei dettagli del messaggio. |
| `AllowMessageDownloads` | `true` | Quando `false`, gli endpoint complete-message (`/eml` e `/raw`) restituiscono `403`. Un messaggio completo trasporta i suoi Allegati, quindi **sia** questo interruttore che `AllowAttachmentDownloads` devono essere `true`; disattivando uno dei due si disattivano entrambi gli endpoint. |
| `EnableSwaggerUi` | `true` | Quando `true` (e `Enabled` è `true`), monta Swagger UI su `/apidocs` e il documento OpenAPI su `/apidocs/spec/v1.json`. Entrambi richiedono una **sessione web (cookie)** autenticata — non fanno parte di `/api/` e non sono raggiungibili con una chiave API. |
| `DefaultPageSize` | `20` | Dimensione della pagina utilizzata quando una richiesta omette `pageSize`. |
| `MaxPageSize` | `100` | Limite superiore; i valori `pageSize` più grandi vengono limitati a questo valore. |
| `RateLimitPerMinute` | `120` | Budget di richiesta a finestra fissa per chiave API al minuto (vedi [Rate limiting](#rate-limiting)). |

## Autenticazione {#authentication}

L'API si autentica tramite **chiavi API per utente** inviate come bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` accetta **solo** chiavi API. I cookie del browser non vengono onorati, quindi l'API non ha una superficie CSRF. I fallimenti di autenticazione restituiscono `401` con un header `WWW-Authenticate: Bearer` e un corpo [problem+json](#errors) — l'API **non** reindirizza mai alla pagina di login.

Le chiavi sono gestite in self-service nell'interfaccia web sotto **API Keys** (ogni utente gestisce le proprie chiavi; gli amministratori possono visualizzare e revocare le chiavi di qualsiasi utente).

### Formato della chiave API {#api-key-format}

Una chiave è la stringa `ma_` seguita da 43 caratteri base64 sicuri per URL che codificano 32 byte casuali (256 bit di entropia):

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Archiviazione:

- **`KeyPrefix`** — i primi ~11 caratteri (es. `ma_8Kf3pQ2`), memorizzati in chiaro. Utilizzati per identificare una chiave nell'interfaccia utente e nei log. Non è un segreto.
- **`KeyHash`** — lo SHA-256 codificato in esadecimale della chiave completa, memorizzato sotto un **indice unico**. La chiave in chiaro non viene mai memorizzata.

La chiave completa viene mostrata all'utente **esattamente una volta**, al momento della creazione. Non può essere recuperata successivamente; una chiave persa deve essere revocata e sostituita.

### Ciclo di vita della chiave {#key-lifecycle}

1. **Crea** — un utente crea una chiave con un `Name` descrittivo e una scadenza opzionale. La risposta mostra la chiave in chiaro una sola volta.
2. **Usa** — invia la chiave come bearer token. Ogni richiesta accettata aggiorna l'`LastUsedAt` della chiave (le scritture sono limitate a al massimo una volta ogni ~5 minuti per evitare una scrittura ad ogni chiamata).
3. **Scadenza** — se `ExpiresAt` è impostato ed è nel passato, la chiave viene rifiutata.
4. **Revoca** — l'utente proprietario o un amministratore revoca la chiave (`RevokedAt` è impostato); viene immediatamente e permanentemente rifiutata.

Una chiave è **attiva** quando non è revocata, non è scaduta **e** il suo utente proprietario è ancora attivo. Una chiave inattiva per uno qualsiasi di questi motivi viene rifiutata con `401`.

### Modello di Autorizzazione {#permission-model}

Una chiave API eredita le esatte autorizzazioni dell'utente che ne è proprietario. Il principale autenticato porta gli stessi claim di un normale login web, quindi tutta l'esistente logica di autorizzazione si applica invariata:

- **Amministratori** (`IsAdmin`) vedono tutti gli account di posta e tutti i messaggi.
- **Utenti regolari** vedono solo gli account di posta assegnati loro (`UserMailAccount`). Gli account e i messaggi al di fuori di quel set sono trattati come **non esistenti**: le richieste per essi restituiscono `404`, non `403`, quindi l'API non rivela l'esistenza di account che un chiamante non può vedere.

### Razionale di sicurezza {#security-rationale}

- **Bypass 2FA.** Le chiavi API bypassano l'autenticazione a due fattori per progettazione — questo è inerente ai token non interattivi (lo stesso vale per i personal access token su GitHub/GitLab). È mitigato dal fatto che le chiavi sono **disabilitate di default**, revocabili, opzionalmente scadenti, limitate alle autorizzazioni dell'utente e ogni accesso viene registrato nel [access log](#access-logging).
- **Nessun cookie su `/api`.** L'autenticazione tramite cookie non è accettata sulle rotte `/api`, eliminando le preoccupazioni CSRF per l'API.
- **Trasporto.** Servire sempre l'API tramite HTTPS in produzione in modo che i bearer token non siano esposti sul cavo.

## Convenzioni {#conventions}

### URL di base e versionamento {#base-url-and-versioning}

Tutti gli endpoint si trovano sotto `/api/v1`. La versione è nel percorso in modo che futuri cambiamenti critici possano essere rilasciati come `/api/v2` senza disturbare i client esistenti.

Le risposte sono `application/json` a meno che non sia diversamente indicato (i download di Allegato restituiscono il content type dell'Allegato stesso). Tutti i timestamp sono UTC, ISO 8601 (`2026-06-11T09:30:00Z`).

### Paginazione {#pagination}

Gli endpoint di elenco restituiscono una busta di paginazione:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` è basato su 1. Le pagine fuori intervallo restituiscono un array `items` vuoto (non un errore).
- `pageSize` ha come valore predefinito `Api:DefaultPageSize` ed è limitato a `[1, Api:MaxPageSize]`.

### Errori {#errors}

Gli errori utilizzano [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
`application/problem+json`:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Unknown sortBy value 'foo'.",
  "instance": "/api/v1/emails"
}
```

| Status | Quando |
| --- | --- |
| `400 Bad Request` | Parametri malformati (ad es. data non analizzabile, `sortBy` non valido). |
| `401 Unauthorized` | Chiave mancante, malformata, scaduta, revocata o altrimenti non valida. Include `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Download di Allegato richiesti durante `AllowAttachmentDownloads=false`, o download di messaggi durante `AllowMessageDownloads=false`. |
| `404 Not Found` | La risorsa non esiste **o** è al di fuori degli Account autorizzati del chiamante. Restituito anche per ogni percorso `/api/*` quando `Api:Enabled=false`. |
| `429 Too Many Requests` | Limite di frequenza superato. |
| `500 Internal Server Error` | Errore del server imprevisto (problem+json, mai la pagina di errore HTML). |

### Rate limiting {#rate-limiting}

Le richieste sono limitate da una policy a finestra fissa di `Api:RateLimitPerMinute` richieste al minuto (predefinito `120`), partizionate per prefisso della chiave API (con fallback all'IP del client quando non è presente alcuna chiave). Il superamento del budget restituisce `429`.

## Endpoints {#endpoints}

> Tutti gli esempi presuppongono che l'API sia abilitata e che `$KEY` contenga una chiave valida.

### Elenca account {#list-accounts}

```
GET /api/v1/accounts
```

Restituisce gli account di posta visibili al chiamante. I campi delle credenziali
(IMAP/OAuth username, password, client secret, …) **non** sono **mai** inclusi.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/accounts
```

```json
[
  {
    "id": 1,
    "name": "Support Mailbox",
    "emailAddress": "support@example.com",
    "provider": "IMAP",
    "isEnabled": true,
    "lastSync": "2026-06-11T08:15:00Z"
  }
]
```

### Elenca le cartelle {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

Restituisce l'albero delle cartelle per un Account come struttura nidificata. Restituisce `404` se
l'Account non esiste o non è visibile al chiamante.

```json
[
  {
    "name": "INBOX",
    "fullPath": "INBOX",
    "totalCount": 4213,
    "level": 0,
    "children": [
      { "name": "Work", "fullPath": "INBOX/Work", "totalCount": 512, "level": 1, "children": [] }
    ]
  }
]
```

### Cerca messaggi {#search-messages}

```
GET /api/v1/emails
```

Cerca i messaggi nell'Archivio con le stesse funzionalità dell'interfaccia web.

| Parametro di query | Tipo | Descrizione |
| --- | --- | --- |
| `q` | string | Query full-text su subject, body, from, to, cc, bcc. Supporta la sintassi dei campi, ad es. `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Data di invio più recente (inclusa), `YYYY-MM-DD`. |
| `to` | date | Data di invio più tardiva (inclusa), `YYYY-MM-DD`. |
| `accountId` | int | Restringe a un Account (deve essere visibile al chiamante). |
| `folder` | string | Restringe a una Cartella tramite percorso completo. |
| `direction` | string | `incoming` o `outgoing`. |
| `page` | int | Numero di pagina basato su 1 (predefinito `1`). |
| `pageSize` | int | Risultati per pagina (predefinito `Api:DefaultPageSize`, limitato a `Api:MaxPageSize`). |
| `sortBy` | string | Uno tra `sentDate` (predefinito), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` o `desc` (predefinito `desc`). |

Restituisce una [paged envelope](#pagination) di [`EmailSummaryDto`](#emailsummarydto).
Registra una voce nel [Registro di controllo](#access-logging) di tipo `Search`.

```bash
curl -H "Authorization: Bearer $KEY" \
  "https://host/api/v1/emails?q=subject:invoice&from=2026-01-01&pageSize=50"
```

```json
{
  "items": [
    {
      "id": 9876,
      "accountId": 1,
      "subject": "Invoice #4711",
      "from": "billing@vendor.com",
      "to": "support@example.com",
      "sentDate": "2026-03-02T11:04:00Z",
      "isOutgoing": false,
      "hasAttachments": true,
      "folderName": "INBOX"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalItems": 3,
  "totalPages": 1
}
```

### Ottenere il messaggio {#get-message}

```
GET /api/v1/emails/{id}
```

Restituisce il messaggio completo inclusi i corpi. Il corpo utilizza la stessa catena di fallback dell'interfaccia web: `OriginalBodyHtml` (byte grezzi) → `BodyUntruncatedHtml` →
`HtmlBody`, e l'equivalente per il corpo del testo. Restituisce `404` se il messaggio
non esiste o appartiene a un account al di fuori delle autorizzazioni del chiamante. Registra una voce nel registro di controllo di tipo `Open`.

Restituisce un [`EmailDetailDto`](#emaildetaildto).

### Scarica Allegato {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Trasmette i byte dell'Allegato con il relativo tipo di contenuto e nome file memorizzati. Restituisce
`404` se il messaggio o l'Allegato non esiste o non è visibile al chiamante. Restituisce `403` (problem+json) quando `Api:AllowAttachmentDownloads=false`.
Registra una voce nel registro di controllo di tipo `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Download message (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Restituisce il messaggio come `message/rfc822`, ricostruito dai campi archiviati:
indirizzi, gli header raw memorizzati (`Received`, `Authentication-Results`,
`X-Spam-*`, …), corpi e allegati. Funziona per ogni email archiviata. È la
stessa ricostruzione che l'esportazione dell'Account della web UI produce, quindi **non**
è identico a livello di byte rispetto a quanto ricevuto: le firme DKIM non verranno verificate.
Utilizzare [`/raw`](#download-original-message) per questo scopo.

Restituisce `404` se il messaggio non esiste o non è visibile al chiamante, e
`403` (problem+json) quando `Api:AllowMessageDownloads=false`. Registra una voce nel registro di controllo
di tipo `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Scarica il messaggio originale {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Restituisce il messaggio originale così come ricevuto (`message/rfc822`), per l'analisi dello spam,
la verifica DKIM o controlli forensi. L'originale viene archiviato solo quando
`RawMessages:Enabled` era impostato al momento in cui l'e-mail è stata archiviata; vedere
[Original Message Storage Settings](Setup.md#-original-message-storage-settings).
La presenza di un messaggio originale è indicata da `hasRawMessage` in
[`EmailDetailDto`](#emaildetaildto).

| Intestazione di risposta | Significato |
| --- | --- |
| `X-Raw-Message-Sha256` | SHA-256 dei byte restituiti, registrato quando il messaggio è stato archiviato |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (copie esatte) o `serialized` (riscritti dal parser, finali di riga normalizzati in CRLF) |
| `X-Raw-Message-Storage` | `postgres` o `filesystem` — da dove sono stati letti i byte (vedere [Storage backend](RawMessages.md#storage-backend)) |

I byte possono risiedere nel database o in un file, a seconda di
`RawMessages:Storage` al momento in cui il messaggio è stato archiviato; l'endpoint
risolve la questione per ogni singolo messaggio, quindi entrambi i tipi vengono serviti allo stesso modo. Con
`RawMessages:FileSystem:VerifyChecksumOnRead` (default `true`) il file viene
ricalcolato l'hash prima di essere inviato, e un messaggio il cui contenuto non corrisponde più all'
checksum registrato **non** viene servito.

Restituisce `404` se il messaggio non esiste o non è visibile al chiamante,
`404` con il titolo del problema `No original message is stored for this email.`
quando esiste senza un originale, e `500` con il titolo
`Stored original message failed its checksum.` quando il contenuto archiviato non corrisponde più al
checksum registrato al momento dell'archiviazione. Restituisce `403` (problem+json) quando
`Api:AllowMessageDownloads=false`. Registra una voce nel registro di controllo di tipo `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -o 9876.eml \
  https://host/api/v1/emails/9876/raw
sha256sum 9876.eml          # matches X-Raw-Message-Sha256
spamassassin -t < 9876.eml  # or: rspamc < 9876.eml
```

### Statistiche {#stats}

```
GET /api/v1/stats
```

Restituisce statistiche aggregate dell'Archivio visibili al chiamante: numero totale di email archiviate, Account di posta visibili, Allegati su tali email e la dimensione totale del database PostgreSQL in megabyte. Per gli utenti non amministratori, i conteggi sono limitati agli Account di posta assegnati loro (lo stesso ambito applicato alla dashboard web); gli amministratori vedono i totali globali.

Per le chiavi API non amministrative, `databaseSizeInMB` è una stringa vuota: la dimensione del database descrive l'intera istanza, non l'ambito della chiave. Email, Account e Allegati sono sempre limitati agli account a cui la chiave può accedere.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/stats
```

```json
{
  "emails": 160132,
  "accounts": 4,
  "attachments": 43540,
  "databaseSizeInMB": "5432"
}
```

### StatsDto {#statsdto}

| Campo | Tipo | Note |
| --- | --- | --- |
| `emails` | int | Messaggi archiviati visibili al chiamante |
| `accounts` | int | Account di posta visibili al chiamante |
| `attachments` | int | Allegati su quei messaggi visibili |
| `databaseSizeInMB` | string | Dimensione totale del database PostgreSQL in MB; vuoto per chiavi non admin |

## Tipi di dati {#data-types}

### MailAccountDto {#mailaccountdto}

| Campo | Tipo | Note |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

Nessun campo relativo alle credenziali viene mai serializzato.

### FolderNodeDto {#foldernodedto}

| Campo | Tipo | Note |
| --- | --- | --- |
| `name` | string | Nome della foglia |
| `fullPath` | string | Percorso completo |
| `totalCount` | int | Messaggi nella cartella |
| `level` | int | Profondità di annidamento (0 = root) |
| `children` | FolderNodeDto[] | Sotto-cartelle |

### EmailSummaryDto {#emailsummarydto}

| Campo | Tipo |
| --- | --- |
| `id` | int |
| `accountId` | int |
| `subject` | string |
| `from` | string |
| `to` | string |
| `sentDate` | datetime |
| `isOutgoing` | bool |
| `hasAttachments` | bool |
| `folderName` | string |

### EmailDetailDto {#emaildetaildto}

Tutti i campi di [`EmailSummaryDto`](#emailsummarydto) più:

| Campo | Tipo | Note |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Risolto tramite la catena di fallback del corpo |
| `htmlBody` | string | Risolto tramite la catena di fallback del corpo |
| `attachments` | AttachmentDto[] | Solo metadati |
| `hasRawMessage` | bool | `true` quando il messaggio originale è memorizzato e [`/raw`](#download-original-message) lo restituisce |

### AttachmentDto {#attachmentdto}

| Campo | Tipo | Note |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Byte |

I byte dell'Allegato vengono ottenuti dall' [endpoint di download](#download-attachment),
non da questo DTO.

## Access logging {#access-logging}

Ogni accesso ai dati tramite l'API viene registrato nello stesso registro di accesso utilizzato dalla web UI (visibile sotto **Logs**), attribuito all'utente proprietario della chiave:

| Endpoint | Tipo registrato |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI e Swagger UI {#openapi-and-swagger-ui}

Quando l'API è abilitata e `Api:EnableSwaggerUi=true`:

- Il documento OpenAPI v1 è servito a **`/apidocs/spec/v1.json`**.
- Swagger UI è servito a **`/apidocs`**.

Entrambi i percorsi si trovano al di fuori di `/api/` e sono quindi protetti dalla normale autenticazione web (cookie) — è richiesta una sessione del browser effettuata con l'accesso per visualizzarli, e non possono essere raggiunti con una API key.

### Riferimento endpoint su questo sito {#endpoint-reference-on-this-site}

Gli endpoint e i tipi di dati sono pubblicati anche qui come pagina generata:
**[API Reference](API-Reference.md)**. Viene renderizzata dallo stesso documento OpenAPI — una copia è mantenuta in `doc/assets/openapi-v1.json` — tramite `tools/gen_api_reference.py`. La CI fallisce quando la pagina e la specifica vanno fuori Sincronizzazione, quindi il riferimento descrive sempre l'API effettivamente distribuita.

!!! note "Rigenerazione"
    La pagina è un output generato. Modifica l'API (o il generatore), quindi esegui
    `python3 tools/gen_api_reference.py` e conferma il risultato.

    Aggiornare la specifica fornita significa rieseguire la generazione del documento
    al tempo di build descritta in `tools/README.md`.
