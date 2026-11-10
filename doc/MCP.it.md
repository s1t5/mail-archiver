# MCP Server (Model Context Protocol) {#mcp-server-model-context-protocol}

Mail Archiver include un **server MCP (Model Context Protocol)** opzionale che
espone la posta archiviata agli agenti AI tramite il trasporto Streamable HTTP. Esso
rispecchia la [REST API](API.md) in sola lettura: stessi dati, stesso modello di
Autorizzazione per utente, stesso registro di controllo degli accessi — solo impacchettati
come strumenti MCP invece che come endpoint REST, in modo che un agente LLM possa
scoprirli e chiamarli direttamente.

L'obiettivo è consentire a un agente AI di leggere la posta archiviata **senza mai esporre
le credenziali della Casella di posta o comunicare con il provider di posta.** Una chiave API
per utente limitata è una capacità di *leggere l'Archivio*, non una credenziale per
*essere la Casella di posta* — essa eredita solo le Autorizzazioni della Casella di posta
del proprietario, non concede alcun percorso di scrittura e può essere
revocata istantaneamente senza toccare l'Account di posta sottostante.

> ⚠️ **Il server MCP è disabilitato di default.** Deve essere esplicitamente abilitato nella
> configurazione (vedere [Enabling the MCP server](#enabling-the-mcp-server)). Quando
> disabilitato, l'endpoint `/mcp` restituisce `404 Not Found`.

Il server MCP è **progettato per la sola lettura**: espone strumenti per elencare gli Account,
elencare le Cartelle, cercare messaggi, leggere un messaggio e scaricare un Allegato. Non
crea, modifica o elimina mai dati.

## Sommario {#table-of-contents}

- [Abilitazione del server MCP](#enabling-the-mcp-server)
- [Autenticazione](#authentication)
- [Trasporto e endpoint](#transport-and-endpoint)
- [Tools](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
  - [get_email_eml](#get_email_eml)
  - [get_email_raw](#get_email_raw)
- [Rate limiting](#rate-limiting)
- [Access logging](#access-logging)
- [Connessione di un agent](#connecting-an-agent)
- [Linee guida sulla sicurezza per l'esposizione su internet](#security-guidance-for-internet-exposure)

## Abilitazione del server MCP {#enabling-the-mcp-server}

Il server MCP è configurato sotto la sezione `Mcp` di `appsettings.json` (o le corrispondenti variabili d'ambiente, ad esempio `Mcp__Enabled=true`):

```json
"Mcp": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "DefaultPageSize": 20,
  "MaxResults": 100,
  "MaxAttachmentBytes": 10000000,
  "AllowMessageDownloads": true,
  "MaxMessageBytes": 10000000,
  "RateLimitPerMinute": 120
}
```

| Setting | Default | Description |
| --- | --- | --- |
| `Enabled` | `false` | Interruttore principale. Quando `false`, l'endpoint `/mcp` restituisce `404`. |
| `AllowAttachmentDownloads` | `true` | Quando `false`, `get_attachment` restituisce un errore invece dei byte dell'allegato. I metadati relativi agli allegati vengono comunque restituiti da `get_email`. |
| `DefaultPageSize` | `20` | Dimensione della pagina utilizzata da `search_emails` quando il chiamante omette `pageSize`. |
| `MaxResults` | `100` | Limite superiore per la dimensione della pagina `search_emails`; i valori più grandi vengono limitati. |
| `MaxAttachmentBytes` | `10000000` | Dimensione massima dell'allegato (in byte) che `get_attachment` restituirà inline come base64; gli allegati più grandi vengono rifiutati. |
| `AllowMessageDownloads` | `true` | Quando `false`, `get_email_eml` e `get_email_raw` restituiscono un errore invece del messaggio. Un messaggio completo porta con sé i propri allegati, quindi **entrambi** questo interruttore e `AllowAttachmentDownloads` devono essere `true`; disattivandone uno, si disabilitano entrambi gli strumenti. |
| `MaxMessageBytes` | `10000000` | Dimensione massima del messaggio (in byte) che `get_email_eml` e `get_email_raw` restituiranno inline; i messaggi più grandi vengono rifiutati. |
| `RateLimitPerMinute` | `120` | Budget di richiesta a finestra fissa per chiave API al minuto (vedere [Rate limiting](#rate-limiting)). |

## Authentication {#authentication}

Il server MCP si autentica utilizzando le **stesse chiavi API per utente della REST API**, inviate come bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

Le chiavi API vengono create e gestite nell'interfaccia web sotto **API Keys**. Una chiave eredita le esatte autorizzazioni dell'utente che la possiede (un admin vede tutti gli account; un utente con restrizioni vede solo gli account assegnati). Le chiavi possono essere revocate o scadere indipendentemente dalle Caselle di posta sottostanti. Consultare la [REST API authentication section](API.md#authentication) per il ciclo di vita completo delle chiavi e il modello di autorizzazione — le chiavi MCP non sono un archivio di credenziali separato.

Quando il server MCP è disabilitato, o non viene presentato un bearer token valido/assente, l'endpoint restituisce rispettivamente `404` / `401` (non reindirizza mai alla pagina di login).

## Trasporto e endpoint {#transport-and-endpoint}

Il server MCP utilizza il trasporto **Streamable HTTP** ed è montato in:

```
POST /mcp
```

Esegue in modalità **stateless** (una richiesta JSON-RPC per ogni richiesta HTTP), che è la modalità raccomandata per i server che non necessitano di richieste server-client come sampling o elicitation. Gli strumenti vengono scoperti tramite la richiesta standard MCP `tools/list` e chiamati tramite `tools/call`.

## Strumenti {#tools}

### list_accounts {#list_accounts}

Elenca gli Account di posta a cui la chiave API corrente è autorizzata ad accedere. Restituisce
`id`, `name`, `emailAddress`, `provider`, `isEnabled` e `lastSync`.

**Parametri:** nessuno.

**Risultato:** array di oggetti Account. Utilizzare il `id` restituito con `list_folders`
e `search_emails`.

### list_folders {#list_folders}

Elenca l'albero delle Cartella di un singolo Account di posta, con i conteggi delle email per ogni Cartella.

**Parametri:**

| Nome | Tipo | Richiesto | Descrizione |
| --- | --- | --- | --- |
| `accountId` | integer | sì | L'id dell'Account di posta (da `list_accounts`). |

**Risultato:** array di oggetti Cartella, ciascuno con `name`, `fullPath`, `totalCount`,
`level` e `children` ricorsivo. Restituisce una lista vuota se l'Account non
esiste o se la chiave non ha l'Autorizzazione per esso.

### search_emails {#search_emails}

Cerca le email nell'Archivio tramite query full-text, intervallo di date, Account, Cartella e direzione.

**Parametri:**

| Nome | Tipo | Richiesto | Descrizione |
| --- | --- | --- | --- |
| `q` | string | no | Query di ricerca full-text (oggetto, corpo, da/a). |
| `from` | string (ISO-8601) | no | Filtro data/ora UTC inviato il o dopo. |
| `to` | string (ISO-8601) | no | Filtro data/ora UTC inviato il o prima. |
| `accountId` | integer | no | Restringe a un singolo id Account email. |
| `folder` | string | no | Restringe a un singolo percorso completo della Cartella (es. `INBOX`). |
| `direction` | string | no | `incoming` o `outgoing`. |
| `page` | integer | no | Numero di pagina basato su 1 (predefinito `1`). |
| `pageSize` | integer | no | Dimensione della pagina; limitata a `[1, MaxResults]`. `0` utilizza `DefaultPageSize`. |
| `sortBy` | string | no | `sentdate` (predefinito), `receiveddate`, `subject`, `from`, `to`. |
| `sortOrder` | string | no | `asc` o `desc` (predefinito `desc`). |

**Risultato:** un oggetto paginato con `items` (array di riepiloghi email), `page`,
`pageSize`, `totalItems`, `totalPages`. Ogni riepilogo contiene `id`, `accountId`,
`subject`, `from`, `to`, `sentDate`, `isOutgoing`, `hasAttachments` e
`folderName`. Passa un `id` restituito a `get_email` per il corpo completo e l'elenco degli Allegati.

### get_email {#get_email}

Recupera il contenuto completo di una singola email nell'Archivio.

**Parametri:**

| Nome | Tipo | Richiesto | Descrizione |
| --- | --- | --- | --- |
| `id` | integer | yes | L'id dell'email nell'Archivio (da `search_emails`). |

**Risultato:** un singolo oggetto email con tutti i campi di riepilogo più `cc`, `bcc`,
`receivedDate`, `messageId`, `textBody`, `htmlBody` e `attachments` (array di
`{ id, fileName, contentType, size }`) e `hasRawMessage` (se
`get_email_raw` può restituire l'originale). Restituisce `null` se l'email non
esiste o se la chiave non ha l'Autorizzazione per il suo Account.

### get_attachment {#get_attachment}

Scarica un singolo Allegato email come blob base64.

**Parametri:**

| Nome | Tipo | Richiesto | Descrizione |
| --- | --- | --- | --- |
| `id` | integer | yes | L'id dell'email nell'Archivio a cui appartiene l'Allegato. |
| `attachmentId` | integer | yes | L'id dell'Allegato (da `attachments` di `get_email`). |

**Risultato:** un oggetto con `id`, `emailId`, `fileName`, `contentType`, `size`
e `base64Data` (i byte grezzi dell'Allegato, codificati in base64). Lo strumento restituisce un
errore quando `Mcp:AllowAttachmentDownloads=false`, quando l'Allegato supera
`Mcp:MaxAttachmentBytes`, o quando la chiave non ha l'Autorizzazione per l'Account
dell'email.

### get_email_eml {#get_email_eml}

Restituisce un'e-mail come messaggio RFC 5322 completo (`.eml`), ricostruito dai campi nell'Archivio: gli header memorizzati (`Received`, `Authentication-Results`, `X-Spam-*`, …), i corpi e gli Allegati. Funziona per ogni e-mail archiviata, ma il risultato non è identico a livello di byte rispetto a quanto ricevuto, pertanto le firme DKIM non saranno verificabili — usa [`get_email_raw`](#get_email_raw) per questo scopo. Specchia `GET /api/v1/emails/{id}/eml`.

**Parametri:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | L'id dell'e-mail nell'Archivio (da `search_emails`). |

**Risultato:** un oggetto con `emailId`, `kind` (`eml`), `source` (`null`), `sha256`, `size`, `encoding` e `content`. `content` è il testo del messaggio quando `encoding` è `utf-8`, e il messaggio codificato in base64 quando `encoding` è `base64` (messaggi che non sono UTF-8 validi, quindi nessun byte viene alterato). Lo strumento restituisce un errore quando `Mcp:AllowMessageDownloads=false`, quando il messaggio supera `Mcp:MaxMessageBytes`, o quando la chiave non ha l'Autorizzazione per l'Account dell'e-mail.

### get_email_raw {#get_email_raw}

Restituisce il messaggio originale esattamente come ricevuto, per l'analisi dello spam, i controlli DKIM/SPF/ARC o l'attività forense. Disponibile solo quando l'originale è stato memorizzato al momento dell'Archivio (`RawMessages:Enabled`, vedi
[Original Message Storage Settings](Setup.md#-original-message-storage-settings));
`get_email` lo riporta come `hasRawMessage`. Specchio di
`GET /api/v1/emails/{id}/raw`.

**Parametri:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | L'id dell'email archiviata (da `search_emails`). |

**Risultato:** lo stesso oggetto di `get_email_eml` con `kind` `raw`, `source` impostato
su come l'originale è stato catturato (`imap`, `graph`, `eml-file` — copie esatte —
o `serialized`, riscritto dal parser con i finali di riga normalizzati in CRLF)
e `sha256` il checksum registrato al momento dell'Archivio. Restituisce gli stessi errori di
`get_email_eml`, e un errore quando non è memorizzato alcun originale per l'email.

I byte vengono letti da qualunque luogo siano stati memorizzati — il database o un file,
a seconda di `RawMessages:Storage` al momento dell'Archivio (vedi
[Storage backend](RawMessages.md#storage-backend)). Con
`RawMessages:FileSystem:VerifyChecksumOnRead` (default `true`) il file viene
ricalcolato tramite hash prima di essere restituito; un messaggio il cui contenuto non corrisponde più al
checksum registrato viene rifiutato con un errore invece di essere restituito.

## Rate limiting {#rate-limiting}

Il server MCP ha la propria policy di rate-limit a finestra fissa (`"Mcp"`), suddivisa
in base al prefisso non segreto della API-key (i primi 11 caratteri del token `ma_...`),
con fallback all'IP del client quando non è presente alcun bearer token. Il budget è
configurato tramite `Mcp:RateLimitPerMinute` (predefinito `120` al minuto). Quando
superato, l'endpoint restituisce `429 Too Many Requests` con un header `Retry-After`.
Questo è indipendente dalla policy di rate-limit della REST API.

## Access logging {#access-logging}

Ogni chiamata a uno strumento viene registrata attraverso lo stesso `IAccessLogService` delle REST API, con il nome utente proprietario e il corrispondente tipo di accesso (`Search`, `Open`, `Download`). L'attività MCP è quindi visibile sulla pagina **Logs** insieme all'attività delle REST API e della web-ui, per un unico registro di controllo.

## Connessione di un agente {#connecting-an-agent}

Il server MCP utilizza lo standard MCP Streamable HTTP transport, pertanto qualsiasi client MCP che supporti HTTP può connettersi. L'URL è l'URL base dell'applicazione più `/mcp`, ad esempio `http://localhost:5179/mcp`, e la API key viene fornita tramite l'header `Authorization: Bearer ma_...`.

Per gli agenti configurati tramite il JSON standard del client MCP, una voce appare come segue:

```json
{
  "mcpServers": {
    "mail-archiver": {
      "url": "https://archive.example.com/mcp",
      "headers": {
        "Authorization": "Bearer ma_<your-api-key>"
      }
    }
  }
}
```

## Linee guida sulla sicurezza per l'esposizione su Internet {#security-guidance-for-internet-exposure}

Il server MCP è progettato in modo che, con i valori predefiniti e l'indurimento (hardening) descritto di seguito, l'Archivio non possa lasciare il sistema senza una chiave API valida per utente che sia stata esplicitamente emessa per un Account. I punti seguenti sono una **lettura obbligatoria prima di esporre `/mcp` su Internet**.

### Obbligatorio (bloccare lo sfruttamento / l'esfiltrazione di dati) {#mandatory-block-exploitation-data-exfiltration}

1. **Solo HTTPS.** Termina il TLS all'estremità (reverse proxy). Una chiave API bearer su HTTP in chiaro può essere intercettata e riutilizzata.
2. **Mantieni `Mcp:Enabled=false` a meno che tu non lo utilizzi effettivamente.** Quando disabilitato, `/mcp` restituisce `404` e nessun percorso di codice è raggiungibile; questo è il valore predefinito sicuro e lo stato sicuro per le implementazioni che non necessitano dell'accesso di un agente AI.
3. **Il rate limiting è applicato sull'endpoint.** La policy `"Mcp"` (`Mcp:RateLimitPerMinute`, predefinito `120`) è applicata sull'endpoint `/mcp` mappato. È partizionata tramite il prefisso non segreto della chiave API (i primi 11 caratteri del token `ma_...`) o l'IP del client quando non è presente un bearer token. **Abbassa `RateLimitPerMinute`** per l'esposizione su Internet (ad es. `30`–`60`) — le chiamate agli strumenti MCP sono più pesanti delle chiamate REST e l'endpoint non deve essere lasciato con un budget elevato quando client non attendibili possono raggiungerlo.
4. **Il reverse-proxy `X-Forwarded-For` deve essere gestito con attenzione.** Il rate limiter ricorre a `RemoteIpAddress`, che sotto un proxy è l'IP del proxy, a meno che non venga fornito a `UseForwardedHeaders` il vero IP del client. Assicurati che il tuo proxy imposti correttamente `X-Forwarded-For` e che solo il tuo proxy possa impostarlo; altrimenti un attaccante può falsificare la partizione del rate-limit.
5. **Non posizionare `/mcp` dietro una cache CDN.** Le risposte MCP sono JSON-RPC per richiesta e non devono mai essere memorizzate nella cache — la cache interromperebbe il protocollo e farebbe trapelare il risultato di un utente a un altro. Segna `/mcp` come non memorizzabile all'estremità (`Cache-Control: no-store`).

### Igiene della chiave API (bloccare l'accesso non autorizzato) {#api-key-hygiene-block-unauthorized-access}

6. **Emetti chiavi dedicate e a breve durata per gli agenti MCP.** Una singola chiave `ma_...` è valida sia per `/api` che per `/mcp`; oggi non esiste uno scope per protocollo. Pertanto:
   - Crea una **chiave separata per ogni agente** (non riutilizzare una chiave di uno script di reporting per un agente LLM esterno).
   - Imposta sempre un breve `ExpiresAt` e ruotalo; revoca immediatamente quando l'agente viene dismesso.
   - Preferisci chiavi di proprietà di un **utente con privilegi limitati (non admin)** i cui unici Account assegnati siano quelli necessari all'agente. Una chiave admin garantisce la visibilità su *tutti* gli account email; una chiave limitata riduce il raggio d'azione di un eventuale attacco.
7. **Tratta la chiave API come una password.** È una credenziale bearer. Non incorporarla mai nel codice lato client, non registrarla mai nei log, non caricarla mai in un repository. Inseriscila nel secret store del client MCP e ruotala in caso di sospetta compromissione.
8. **Effettua l'audit sulla pagina dei Log.** Ogni chiamata agli strumenti è registrata in `AccessLogs` (`Search`, `Open`, `Download`) sotto l'utente proprietario. Revisiona periodicamente l'attività MCP per individuare pattern inaspettati (picchi di volume, letture fuori orario, download massivi).

### Modello di fiducia per i dati restituiti (bloccare danni a valle) {#trust-model-for-returned-data-block-downstream-harm}

9. **I corpi delle email e gli Allegati sono contenuti non attendibili**, esattamente come quando sono arrivati nella Casella di posta. Possono contenere malware, link di phishing o HTML/JavaScript attivi.
   - `get_email` restituisce `htmlBody` **grezzi e non sanificati**. **Non** renderizzarli a un essere umano senza sanificazione (DOMPurify / `HtmlSanitizer` / un sandbox). La renderizzazione di HTML archiviato grezzo è un vettore di stored-XSS.
   - `get_attachment` restituisce i byte grezzi con l'originale `contentType`. **Non** aprire o eseguire automaticamente gli Allegati; tratta il campo `contentType` come metadati non attendibili, non come un'istruzione sicura.
   - `get_email_eml` e `get_email_raw` restituiscono il messaggio completo inclusi tutti gli Allegati. Le stesse regole si applicano a ciascuna delle sue parti.
10. **Non permettere all'agente di compiere azioni di scrittura basate sul contenuto delle email senza conferma.** Un'email malevola può contenere testo di prompt-injection mirato all'LLM che la consuma. Applica le Autorizzazioni e la conferma nella logica stessa dell'agente — il system prompt non è un confine di sicurezza.

### Raccomandazioni di difesa in profondità {#defense-in-depth-recommendations}

11. **Limita `/mcp` a livello di rete** quando possibile (ad es. consente solo l'IP / l'intervallo VPN dell'host dell'agente al reverse proxy), oltre all'autenticazione tramite chiave API. Questo è il controllo singolo più efficace per l'esposizione su Internet.
12. **Abbassa `Mcp:AllowAttachmentDownloads` a `false`** se i tuoi agenti non necessitano dei byte degli Allegati — l'esfiltrazione degli Allegati è il percorso di dati a più alto volume. I metadati (nome file, tipo di contenuto, dimensione) vengono comunque restituiti tramite `get_email`.
13. **Abbassa `Mcp:MaxAttachmentBytes`** al valore minimo di cui i tuoi agenti hanno effettivamente bisogno (predefinito `10000000` ≈ 10 MB). Questo limita la dimensione della singola risposta più grande che una chiave API può estrarre per richiesta. Lo stesso vale per `Mcp:MaxMessageBytes`, o imposta `Mcp:AllowMessageDownloads` a `false` quando gli agenti non necessitano di messaggi completi.
14. **Abbassa `Mcp:MaxResults` e `Mcp:DefaultPageSize`** per limitare il risultato di ricerca più grande che una chiave API può estrarre per richiesta (predefinito `100` / `20`).
15. **Monitora `AccessLogs` e i rifiuti dovuti al rate limiting** (`429`) a livello operativo. Un picco improvviso da un singolo prefisso di chiave è un segnale per revocare quella chiave.
