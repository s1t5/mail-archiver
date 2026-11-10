# Read-Only REST API (v1) {#read-only-rest-api-v1}

Mail Archiver wordt geleverd met een optionele, **read-only** REST API voor programmeerbare toegang
tot gearchiveerde e-mail. Het is ontworpen voor automatisering en integratiegebruikssituaties
(rapportage, e-discovery exports, monitoring) zonder de web UI te gebruiken.

Het doel is om een script of AI-agent gearchiveerde e-mail te laten lezen **zonder ooit
de inloggegevens van de Postbus te onthullen of te communiceren met de e-mailprovider.** Een gescoped
per-gebruiker API-sleutel is een mogelijkheid om *het Archief te lezen*, geen inloggegevens om
*de Postbus te zijn* — het erft alleen de Postbus-machtigingen van de eigenaar, verleent geen schrijfrechten,
en kan onmiddellijk worden ingetrokken zonder het onderliggende e-mailaccount aan te raken.

> ⚠️ **De API is standaard uitgeschakeld.** Deze moet expliciet worden ingeschakeld in de
> configuratie (zie [Enabling the API](#enabling-the-api)). Wanneer uitgeschakeld,
> geeft elke `/api/*` route `404 Not Found` terug.

De API is **read-only door ontwerp**: het biedt toegang tot accounts, mappen, berichtenzoekopdrachten,
berichtdetails en downloads van Bijlagen. Het maakt nooit gegevens aan, wijzigt ze niet of
verwijdert ze niet.

## Inhoudsopgave {#table-of-contents}

- [De API inschakelen](#enabling-the-api)
- [Authenticatie](#authentication)
  - [API key formaat](#api-key-format)
  - [Key levenscyclus](#key-lifecycle)
  - [Machtigingsmodel](#permission-model)
  - [Beveiligingsratio](#security-rationale)
- [Conventies](#conventions)
  - [Base URL en versiering](#base-url-and-versioning)
  - [Paginering](#pagination)
  - [Fouten](#errors)
  - [Rate limiting](#rate-limiting)
- [Endpoints](#endpoints)
  - [Lijst met accounts](#list-accounts)
  - [Lijst met mappen](#list-folders)
  - [Berichten zoeken](#search-messages)
  - [Bericht ophalen](#get-message)
  - [Bijlage downloaden](#download-attachment)
  - [Bericht (.eml) downloaden](#download-message-eml)
  - [Origineel bericht downloaden](#download-original-message)
  - [Statistieken](#stats)
- [Datatypen](#data-types)
- [Toegang logging](#access-logging)
- [OpenAPI en Swagger UI](#openapi-and-swagger-ui)
- [API Referentie](API-Reference.md) — gegenereerde endpoint en data-type referentie

## De API inschakelen {#enabling-the-api}

De API is geconfigureerd onder de `Api` sectie van `appsettings.json` (of de
vergelijkbare omgevingsvariabelen, bijv. `Api__Enabled=true`):

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


### Key lifecycle {#key-lifecycle}

1. **Creëren** — een gebruiker creëert een sleutel met een beschrijvende `Name` en een optionele
   verloopdatum. De respons toont de plaintext sleutel één keer.
2. **Gebruiken** — stuur de sleutel als bearer token. Elke geaccepteerde aanvraag ververst de
   `LastUsedAt` van de sleutel (schrijfacties worden beperkt tot maximaal één keer per ~5 minuten om
   een schrijfactie bij elke aanroep te voorkomen).
3. **Vervallen** — als `ExpiresAt` is ingesteld en in het verleden ligt, wordt de sleutel geweigerd.
4. **Intrekken** — de eigenaar van de sleutel of een administrator trekt de sleutel
   in (`RevokedAt` is ingesteld); deze wordt onmiddellijk en permanent geweigerd.

Een sleutel is **actief** wanneer deze niet is ingetrokken, niet is verlopen, **en** de eigenaar
nog actief is. Een sleutel die om een van deze redenen inactief is, wordt geweigerd met `401`.

### Machtigingsmodel {#permission-model}

Een API-sleutel erft exact dezelfde machtigingen van de gebruiker die de eigenaar is. De
geauthenticeerde principal draagt dezelfde claims als een normale weblogin, dus
alle bestaande autorisatie

| Instelling | Standaard | Beschrijving |
| --- | --- | --- |
| `Enabled` | `false` | Master switch. Wanneer `false`, geven alle `/api/*` routes `404` terug en zijn het OpenAPI-document en Swagger UI niet in kaart gebracht. |
| `AllowAttachmentDownloads` | `true` | Wanneer `false`, geeft het attachment download endpoint `403` terug. Metadata over bijlagen wordt nog steeds geretourneerd door het message detail endpoint. |
| `AllowMessageDownloads` | `true` | Wanneer `false`, geven de complete-message endpoints (`/eml` en `/raw`) `403` terug. Een complete message bevat de bijlagen, dus **zowel** deze switch als `AllowAttachmentDownloads` moeten `true` zijn; het uitschakelen van een van beide schakelt beide endpoints uit. |
| `EnableSwaggerUi` | `true` | Wanneer `true` (en `Enabled` is `true`), monteert Swagger UI op `/apidocs` en het OpenAPI-document op `/apidocs/spec/v1.json`. Beiden vereisen een geauthenticeerde **web (cookie) sessie** — ze maken geen deel uit van `/api/` en zijn niet bereikbaar met een API-sleutel. |
| `DefaultPageSize` | `20` | Pagina-grootte die wordt gebruikt wanneer een aanvraag `pageSize` weglaat. |
| `MaxPageSize` | `100` | Bovengrens; grotere `pageSize` waarden worden afgekapt naar deze waarde. |
| `RateLimitPerMinute` | `120` | Fixed-window request budget per API-sleutel per minuut (zie [Rate limiting](#rate-limiting)). |

## Authenticatie {#authentication}

De API authenticeert met **per-gebruiker API-sleutels** die worden verzonden als een bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` accepteert **alleen** API-sleutels. Browsercookies worden niet erkend, waardoor de
API geen CSRF-oppervlak heeft. Authenticatiefouten geven `401` terug met een
`WWW-Authenticate: Bearer` header en een [problem+json](#errors) body — de API
wordt **nooit** doorgeleid naar de inlogpagina.

Sleutels worden via self-service beheerd in de web UI onder **API Keys** (elke gebruiker
beheert zijn eigen sleutels; beheerders kunnen de sleutels van elke gebruiker inzien en intrekken).

### API-sleutelformaat {#api-key-format}

Een sleutel is de string `ma_` gevolgd door 43 URL-veilige base64-tekens die 32
willekeurige bytes coderen (256 bits entropie):

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Opslag:

- **`KeyPrefix`** — de eerste ~11 tekens (bijv. `ma_8Kf3pQ2`), opgeslagen in
  plaintext. Wordt gebruikt om een sleutel te identificeren in de UI en in logs. Geen geheim.
- **`KeyHash`** — de hex-gecodeerde SHA-256 van de volledige sleutel, opgeslagen onder een
  **unieke index**. De plaintext sleutel wordt nooit opgeslagen.

De volledige sleutel wordt de gebruiker **exact één keer** getoond, op het moment van aanmaak. Deze kan daarna niet
meer worden opgehaald; een verloren sleutel moet worden ingetrokken en vervangen.

### Levenscyclus van de sleutel {#key-lifecycle}

1. **Aanmaken** — een gebruiker maakt een sleutel aan met een beschrijvende `Name` en een optionele
   verloopdatum. De respons toont de plaintext sleutel één keer.
2. **Gebruiken** — verzend de sleutel als een bearer token. Elke geaccepteerde aanvraag ververst de
   `LastUsedAt` van de sleutel (schrijfacties worden beperkt tot maximaal één keer per ~5 minuten om
   een schrijfactie bij elke aanroep te voorkomen).
3. **Vervallen** — als `ExpiresAt` is ingesteld en in het verleden ligt, wordt de sleutel geweigerd.
4. **Intrekken** — de eigenaar van de sleutel of een beheerder trekt de sleutel in
   (`RevokedAt` is ingesteld); deze wordt onmiddellijk en permanent geweigerd.

Een sleutel is **actief** wanneer deze niet is ingetrokken, niet is verlopen, **en** de eigenaar
nog actief is. Een sleutel die om een van deze redenen inactief is, wordt geweigerd met `401`.

### Machtigingsmodel {#permission-model}

Een API-sleutel erft exact dezelfde machtigingen van de gebruiker die er eigenaar van is. De
geauthenticeerde principal draagt dezelfde claims als een normale web-login, dus
alle bestaande autorisatielogica is ongewijzigd van toepassing:

- **Beheerders** (`IsAdmin`) zien alle mail accounts en alle berichten.
- **Reguliere gebruikers** zien alleen de mail accounts die aan hen zijn toegewezen
  (`UserMailAccount`). Accounts en berichten buiten die set worden behandeld als
  **niet-bestaand**: aanvragen hiervoor geven `404` terug, niet `403`, dus de API
  lekt de aanwezigheid van accounts niet die een aanroeper niet kan zien.

### Beveiligingsratio {#security-rationale}

- **2FA bypass.** API-sleutels omzeilen ontworpen gezien twee-factor authenticatie — dit is
  inherent aan niet-interactieve tokens (hetzelfde geldt voor personal access tokens
  op GitHub/GitLab). Dit wordt beperkt door: sleutels zijn **standaard uitgeschakeld**,
  intrekbaar, optioneel verlopend, beperkt tot de machtigingen van de gebruiker, en elke
  toegang wordt geregistreerd in het [access log](#access-logging).
- **Geen cookies op `/api`.** Cookie-authenticatie wordt niet geaccepteerd op `/api`
  routes, wat CSRF-zorgen voor de API wegneemt.
- **Transport.** Serveer de API in productie altijd over HTTPS zodat bearer tokens
  niet op de lijn worden blootgesteld.

## Conventies {#conventions}

### Base URL en versiering {#base-url-and-versioning}

Alle endpoints bevinden zich onder `/api/v1`. De versie zit in het pad, zodat toekomstige
breaking changes als `/api/v2` kunnen worden uitgebracht zonder bestaande clients te verstoren.

Responses zijn `application/json` tenzij anders vermeld (bijlage downloads retourneren de
eigen content type van de bijlage). Alle tijdstempels zijn UTC, ISO 8601
(`2026-06-11T09:30:00Z`).

### Paginering {#pagination}

List endpoints retourneren een pagineringsenvelop:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` is 1-gebaseerd. Pagina's buiten het bereik retourneren een lege `items` array (geen
  fout).
- `pageSize` staat standaard op `Api:DefaultPageSize` en wordt begrensd door
  `[1, Api:MaxPageSize]`.

### Fouten {#errors}

Fouten gebruiken [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
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

| Status | Wanneer |
| --- | --- |
| `400 Bad Request` | Ongeldige parameters (bijv. niet-parseerbare datum, ongeldige `sortBy`). |
| `401 Unauthorized` | Ontbrekende, ongeldige, verlopen, ingetrokken of anderszins ongeldige sleutel. Inclusief `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Bijlage downloads aangevraagd tijdens `AllowAttachmentDownloads=false`, of bericht downloads tijdens `AllowMessageDownloads=false`. |
| `404 Not Found` | Resource bestaat niet **of** valt buiten de toegestane accounts van de aanroeper. Wordt ook geretourneerd voor elke `/api/*` route wanneer `Api:Enabled=false`. |
| `429 Too Many Requests` | Rate limit overschreden. |
| `500 Internal Server Error` | Onverwachte serverfout (problem+json, nooit de HTML foutpagina). |

### Rate limiting {#rate-limiting}

Verzoeken worden beperkt door een fixed-window beleid van `Api:RateLimitPerMinute`
verzoeken per minuut (standaard `120`), verdeeld per API key prefix (valt
terug op client IP wanneer er geen sleutel aanwezig is). Het overschrijden van het budget retourneert `429`.

## Endpoints {#endpoints}

> Alle voorbeelden gaan ervan uit dat de API is ingeschakeld en dat `$KEY` een geldige sleutel bevat.

### Accounts opsommen {#list-accounts}

```
GET /api/v1/accounts
```

Geeft de mail accounts terug die zichtbaar zijn voor de aanroeper. Credential velden
(IMAP/OAuth username, password, client secret, …) worden **nooit** meegeleverd.

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

### Mappen opsommen {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

Geeft de mappenboom voor één Account terug als een geneste structuur. Geeft `404` terug als de Account niet bestaat of niet zichtbaar is voor de aanroeper.

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

### Berichten zoeken {#search-messages}

```
GET /api/v1/emails
```

Zoekt gearchiveerde berichten met dezelfde mogelijkheden als de web UI.

| Query parameter | Type | Beschrijving |
| --- | --- | --- |
| `q` | string | Full-text query over onderwerp, body, van, naar, cc, bcc. Ondersteunt veldsyntaxis, bijv. `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Vroegst verzonden datum (inclusief), `YYYY-MM-DD`. |
| `to` | date | Laatst verzonden datum (inclusief), `YYYY-MM-DD`. |
| `accountId` | int | Beperken tot één Account (moet zichtbaar zijn voor de aanroeper). |
| `folder` | string | Beperken tot één Map via het volledige pad. |
| `direction` | string | `incoming` of `outgoing`. |
| `page` | int | Paginanummer op basis van 1 (standaard `1`). |
| `pageSize` | int | Resultaten per pagina (standaard `Api:DefaultPageSize`, begrensd tot `Api:MaxPageSize`). |
| `sortBy` | string | Een van `sentDate` (standaard), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` of `desc` (standaard `desc`). |

Geeft een [paged envelope](#pagination) van [`EmailSummaryDto`](#emailsummarydto) terug.
Logt een [access log](#access-logging) vermelding van type `Search`.

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

### Bericht ophalen {#get-message}

```
GET /api/v1/emails/{id}
```

Geeft het volledige bericht inclusief de body's terug. De body gebruikt dezelfde fallback-keten als de web UI: `OriginalBodyHtml` (ruwe bytes) → `BodyUntruncatedHtml` →
`HtmlBody`, en het equivalent voor de tekstbody. Geeft `404` terug als het bericht niet bestaat of toebehoort aan een account buiten de machtigingen van de aanroeper. Logt een toegangseintree van het type `Open`.

Geeft een [`EmailDetailDto`](#emaildetaildto) terug.

### Bijlage downloaden {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Streamt de bytes van de bijlage met de opgeslagen content type en bestandsnaam. Geeft
`404` terug als het bericht of de bijlage niet bestaat of niet zichtbaar is voor de
aanroeper. Geeft `403` (problem+json) terug wanneer `Api:AllowAttachmentDownloads=false`.
Logt een toegangseintree van het type `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Bericht downloaden (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Geeft het bericht terug als `message/rfc822`, opnieuw opgebouwd uit de gearchiveerde velden:
adressen, de opgeslagen ruwe headers (`Received`, `Authentication-Results`,
`X-Spam-*`, …), bodies en bijlagen. Werkt voor elke gearchiveerde e-mail. Het is
dezelfde reconstructie die de account export van de web UI produceert, dus het is **niet**
byte-identiek aan wat is ontvangen: DKIM-handtekeningen zullen hier niet tegen verifiëren.
Gebruik [`/raw`](#download-original-message) hiervoor.

Geeft `404` terug als het bericht niet bestaat of niet zichtbaar is voor de aanroeper, en
`403` (problem+json) wanneer `Api:AllowMessageDownloads=false`. Logt een toegangsprotocol-invoer
van het type `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Origineel bericht downloaden {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Geeft het originele bericht terug zoals ontvangen (`message/rfc822`), voor spamanalyse,
DKIM-verificatie of forensische controles. Het origineel wordt alleen opgeslagen wanneer
`RawMessages:Enabled` was ingesteld op het moment dat de e-mail werd gearchiveerd; zie
[Instellingen voor opslag origineel bericht](Setup.md#-original-message-storage-settings).
Of een e-mail een origineel heeft, wordt aangegeven door `hasRawMessage` in
[`EmailDetailDto`](#emaildetaildto).

| Response header | Betekenis |
| --- | --- |
| `X-Raw-Message-Sha256` | SHA-256 van de geretourneerde bytes, geregistreerd op het moment dat het bericht werd opgeslagen |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (exacte kopieën) of `serialized` (opnieuw geschreven door de parser, regelafsluitingen genormaliseerd naar CRLF) |
| `X-Raw-Message-Storage` | `postgres` of `filesystem` — waar de bytes vandaan zijn gelezen (zie [Storage backend](RawMessages.md#storage-backend)) |

De bytes kunnen in de database staan of in een bestand, afhankelijk van
`RawMessages:Storage` op het moment dat het bericht werd gearchiveerd; het eindpunt
lost dit per bericht op, zodat beide soorten op dezelfde manier worden geserveerd. Met
`RawMessages:FileSystem:VerifyChecksumOnRead` (standaard `true`) wordt het bestand
opnieuw gehasht voordat het wordt verzonden, en een bericht waarvan de inhoud niet langer overeenkomt met de
geregistreerde checksum wordt **niet** geserveerd.

Geeft `404` terug als het bericht niet bestaat of niet zichtbaar is voor de aanroeper,
`404` met de probleemtitel `No original message is stored for this email.`
wanneer het wel bestaat maar zonder origineel, en `500` met de titel
`Stored original message failed its checksum.` wanneer de opgeslagen inhoud niet langer
overeenkomt met de checksum die bij de archivering is geregistreerd. Geeft `403` (problem+json) terug wanneer
`Api:AllowMessageDownloads=false`. Logt een toegangseintree van het type `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -o 9876.eml \
  https://host/api/v1/emails/9876/raw
sha256sum 9876.eml          # matches X-Raw-Message-Sha256
spamassassin -t < 9876.eml  # or: rspamc < 9876.eml
```

### Statistieken {#stats}

```
GET /api/v1/stats
```

Geeft geaggregeerde Archiefstatistieken terug die zichtbaar zijn voor de aanroeper: totaal aantal gearchiveerde e-mails, zichtbare mail accounts, bijlagen bij die e-mails, en de totale PostgreSQL databasegrootte in megabytes. Voor niet-beheerders zijn de tellingen beperkt tot de mail accounts die aan hen zijn toegewezen (dezelfde afbakening die het webdashboard toepast); beheerders zien globale totalen.

Voor API-sleutels zonder beheerdersrechten is `databaseSizeInMB` een lege string: de databasegrootte beschrijft de gehele instantie, niet de scope van de sleutel. E-mails, Accounts en Bijlagen zijn altijd beperkt tot de accounts waartoe de sleutel toegang heeft.

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

| Veld | Type | Opmerkingen |
| --- | --- | --- |
| `emails` | int | Gearchiveerde berichten zichtbaar voor de aanroeper |
| `accounts` | int | Mail accounts zichtbaar voor de aanroeper |
| `attachments` | int | Bijlagen op die zichtbare berichten |
| `databaseSizeInMB` | string | Totale PostgreSQL database grootte in MB; leeg voor niet-admin keys |

## Datatypen {#data-types}

### MailAccountDto {#mailaccountdto}

| Veld | Type | Opmerkingen |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

Er worden nooit velden met inloggegevens geserialiseerd.

### FolderNodeDto {#foldernodedto}

| Veld | Type | Opmerkingen |
| --- | --- | --- |
| `name` | string | Leaf name |
| `fullPath` | string | Volledig pad |
| `totalCount` | int | Berichten in Map |
| `level` | int | Nesting diepte (0 = root) |
| `children` | FolderNodeDto[] | Sub-mappen |

### EmailSummaryDto {#emailsummarydto}

| Veld | Type |
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

Alles van [`EmailSummaryDto`](#emailsummarydto) plus:

| Veld | Type | Opmerkingen |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Opgelost via de body fallback chain |
| `htmlBody` | string | Opgelost via de body fallback chain |
| `attachments` | AttachmentDto[] | Alleen metadata |
| `hasRawMessage` | bool | `true` wanneer het originele bericht is opgeslagen en [`/raw`](#download-original-message) het teruggeeft |

### AttachmentDto {#attachmentdto}

| Veld | Type | Opmerkingen |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bytes |

Bijlage bytes worden verkregen van de [download endpoint](#download-attachment),
niet van deze DTO.

## Toegangslogging {#access-logging}

Elke gegevenstoegang via de API wordt geregistreerd in hetzelfde toegangslogboek dat wordt gebruikt door de web UI (zichtbaar onder **Logs**), toegewezen aan de eigenaar van de sleutel:

| Endpoint | Gelogd type |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI en Swagger UI {#openapi-and-swagger-ui}

Wanneer de API is ingeschakeld en `Api:EnableSwaggerUi=true`:

- Het OpenAPI v1-document wordt geserveerd op **`/apidocs/spec/v1.json`**.
- Swagger UI wordt geserveerd op **`/apidocs`**.

Beide paden bevinden zich buiten `/api/` en worden daarom afgeschermd door de normale web(cookie) authenticatie — een ingelogde browser sessie is vereist om ze te bekijken, en ze kunnen niet worden bereikt met een API-sleutel.

### Endpoint referentie op deze site {#endpoint-reference-on-this-site}

De endpoints en datatypen worden hier ook gepubliceerd als een gegenereerde pagina:
**[API Reference](API-Reference.md)**. Deze wordt gerenderd vanuit hetzelfde OpenAPI-document — een kopie wordt bewaard bij `doc/assets/openapi-v1.json` — door
`tools/gen_api_reference.py`. CI faalt wanneer de pagina en de specificatie niet meer in synchronisatie zijn, dus de referentie beschrijft altijd de API die daadwerkelijk wordt geleverd.

!!! note "Regenereren"
    De pagina is gegenereerde output. Wijzig de API (of de generator), voer dan
    `python3 tools/gen_api_reference.py` uit en commit het resultaat.

    Het verversen van de meegeleverde specificatie zelf betekent het opnieuw uitvoeren van de documentgeneratie tijdens de build, zoals beschreven in `tools/README.md`.
