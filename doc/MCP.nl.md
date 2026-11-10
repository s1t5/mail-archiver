# MCP Server (Model Context Protocol) {#mcp-server-model-context-protocol}

Mail Archiver wordt geleverd met een optionele **MCP (Model Context Protocol) server** die de gearchiveerde mail beschikbaar stelt voor AI-agenten via de Streamable HTTP-transport. Het spiegelt de read-only [REST API](API.md): dezelfde data, hetzelfde per-user machtigingsmodel, dezelfde toegangslogging — alleen verpakt als MCP-tools in plaats van REST-endpoints, zodat een LLM-agent deze direct kan ontdekken en aanroepen.

Het doel is om een AI-agent gearchiveerde mail te laten lezen **zonder ooit de mailbox-credentials te onthullen of met de mailprovider te communiceren.** Een gescoopte per-user API-key is een mogelijkheid om het *Archief te lezen*, geen credential om *de Postbus te zijn* — het erft alleen de mailbox-machtigingen van de eigenaar, verleent geen schrijfweg en kan onmiddellijk worden ingetrokken zonder het onderliggende mailaccount aan te passen.

> ⚠️ **De MCP server is standaard uitgeschakeld.** Deze moet expliciet worden ingeschakeld in de configuratie (zie [Enabling the MCP server](#enabling-the-mcp-server)). Wanneer uitgeschakeld, geeft de `/mcp` endpoint `404 Not Found` terug.

De MCP server is **by design read-only**: het biedt tools om accounts op te sommen, mappen op te sommen, berichten te doorzoeken, een bericht te lezen en een bijlage te downloaden. Het maakt nooit gegevens aan, wijzigt ze niet of verwijdert ze niet.

## Inhoudsopgave {#table-of-contents}

- [Het inschakelen van de MCP-server](#enabling-the-mcp-server)
- [Authenticatie](#authentication)
- [Transport en endpoint](#transport-and-endpoint)
- [Tools](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
  - [get_email_eml](#get_email_eml)
  - [get_email_raw](#get_email_raw)
- [Rate limiting](#rate-limiting)
- [Toegangslogging](#access-logging)
- [Een agent verbinden](#connecting-an-agent)
- [Beveiligingsrichtlijnen voor internettoegang](#security-guidance-for-internet-exposure)

## Het inschakelen van de MCP server {#enabling-the-mcp-server}

De MCP server wordt geconfigureerd onder de `Mcp` sectie van `appsettings.json` (of de equivalente omgevingsvariabelen, bijv. `Mcp__Enabled=true`):

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

| Instelling | Standaard | Beschrijving |
| --- | --- | --- |
| `Enabled` | `false` | Hoofdschakelaar. Wanneer `false`, geeft het `/mcp` endpoint `404` terug. |
| `AllowAttachmentDownloads` | `true` | Wanneer `false`, geeft `get_attachment` een foutmelding terug in plaats van bijlage bytes. Metadata over bijlagen wordt nog steeds geretourneerd door `get_email`. |
| `DefaultPageSize` | `20` | Paginagrootte gebruikt door `search_emails` wanneer de aanroeper `pageSize` weglaat. |
| `MaxResults` | `100` | Bovengrens voor de `search_emails` paginagrootte; grotere waarden worden begrensd. |
| `MaxAttachmentBytes` | `10000000` | Maximale bijlage grootte (in bytes) die `get_attachment` inline als base64 zal retourneren; grotere bijlagen worden geweigerd. |
| `AllowMessageDownloads` | `true` | Wanneer `false`, geven `get_email_eml` en `get_email_raw` een foutmelding terug in plaats van het bericht. Een volledig bericht bevat zijn bijlagen, dus **zowel** deze schakelaar als `AllowAttachmentDownloads` moeten `true` zijn; het uitschakelen van een van beide schakelt beide tools uit. |
| `MaxMessageBytes` | `10000000` | Maximale berichtgrootte (in bytes) die `get_email_eml` en `get_email_raw` inline zullen retourneren; grotere berichten worden geweigerd. |
| `RateLimitPerMinute` | `120` | Fixed-window request budget per API key per minuut (zie [Rate limiting](#rate-limiting)). |

## Authenticatie {#authentication}

De MCP-server authenticeert met **dezelfde per-gebruiker API-sleutels als de REST
API**, verzonden als een bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

API-sleutels worden aangemaakt en beheerd in de web UI onder **API Keys**. Een sleutel
erft exact de machtigingen van de gebruiker die de eigenaar is (een admin ziet alle
accounts; een beperkte gebruiker ziet alleen de toegewezen accounts). Sleutels kunnen
worden ingetrokken of verlopen onafhankelijk van de onderliggende mail-accounts. Zie de
[REST API authenticatie sectie](API.md#authentication) voor de volledige levenscyclus van de sleutel
en het machtigingsmodel — MCP-sleutels zijn geen aparte credential store.

Wanneer de MCP-server is uitgeschakeld, of er geen/ongeldig bearer token wordt gepresenteerd,
geeft de endpoint respectievelijk `404` / `401` terug (het wordt nooit doorverwezen naar de login-
pagina).

## Transport en endpoint {#transport-and-endpoint}

De MCP-server gebruikt de **Streamable HTTP**-transport en is gemount op:

```
POST /mcp
```

De server draait in **stateless**-modus (één JSON-RPC-verzoek per HTTP-verzoek), wat de aanbevolen modus is voor servers die geen server-naar-client-verzoeken nodig hebben zoals sampling of elicitation. Tools worden ontdekt via de standaard MCP `tools/list`-verzoek en aangeroepen via `tools/call`.

## Tools {#tools}

### list_accounts {#list_accounts}

Lijst de mailaccounts waar de huidige API-sleutel toegang toe heeft. Geeft
`id`, `name`, `emailAddress`, `provider`, `isEnabled` en `lastSync` terug.

**Parameters:** geen.

**Resultaat:** array van account objecten. Gebruik de geretourneerde `id` met `list_folders`
en `search_emails`.

### list_folders {#list_folders}

Lijst de mappenboom van een enkel mail Account, met e-mailaantallen per Map.

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `accountId` | integer | yes | De mail Account id (van `list_accounts`). |

**Result:** array van folder objecten, elk met `name`, `fullPath`, `totalCount`,
`level` en recursieve `children`. Geeft een lege lijst terug als het Account niet
bestaat of de sleutel hiervoor niet is geautoriseerd.

### search_emails {#search_emails}

Zoekt gearchiveerde e-mails via een full-text query, datumbereik, Account, Map en richting.

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `q` | string | no | Full-text zoekopdracht (onderwerp, body, van/naar). |
| `from` | string (ISO-8601) | no | Verzonden op of na UTC datum/tijd filter. |
| `to` | string (ISO-8601) | no | Verzonden op of voor UTC datum/tijd filter. |
| `accountId` | integer | no | Beperken tot een enkele mail Account id. |
| `folder` | string | no | Beperken tot een enkel volledig Map pad (bijv. `INBOX`). |
| `direction` | string | no | `incoming` of `outgoing`. |
| `page` | integer | no | Paginanummer beginnend bij 1 (standaard `1`). |
| `pageSize` | integer | no | Paginaformaat; begrensd tot `[1, MaxResults]`. `0` gebruikt `DefaultPageSize`. |
| `sortBy` | string | no | `sentdate` (standaard), `receiveddate`, `subject`, `from`, `to`. |
| `sortOrder` | string | no | `asc` of `desc` (standaard `desc`). |

**Resultaat:** een gepageerd object met `items` (array van e-mail samenvattingen), `page`,
`pageSize`, `totalItems`, `totalPages`. Elke samenvatting bevat `id`, `accountId`,
`subject`, `from`, `to`, `sentDate`, `isOutgoing`, `hasAttachments` en
`folderName`. Geef een geretourneerde `id` door aan `get_email` voor de volledige body en
Bijlage lijst.

### get_email {#get_email}

Haalt de volledige inhoud van een enkele gearchiveerde e-mail op.

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | De gearchiveerde e-mail id (vanuit `search_emails`). |

**Resultaat:** een enkel e-mailobject met alle samenvattingsvelden plus `cc`, `bcc`,
`receivedDate`, `messageId`, `textBody`, `htmlBody` en `attachments` (array van
`{ id, fileName, contentType, size }`) en `hasRawMessage` (of
`get_email_raw` de originele kan retourneren). Retourneert `null` als de e-mail niet
bestaat of de sleutel niet is geautoriseerd voor het bijbehorende Account.

### get_attachment {#get_attachment}

Downloadt een enkele e-mailbijlage als een base64-blob.

**Parameters:**

| Naam | Type | Vereist | Beschrijving |
| --- | --- | --- | --- |
| `id` | integer | ja | Het id van de gearchiveerde e-mail waartoe de bijlage behoort. |
| `attachmentId` | integer | ja | Het id van de bijlage (uit de `attachments` van `get_email`). |

**Resultaat:** een object met `id`, `emailId`, `fileName`, `contentType`, `size`
en `base64Data` (de ruwe bijlage-bytes, base64-gecodeerd). De tool geeft een
foutmelding wanneer `Mcp:AllowAttachmentDownloads=false`, wanneer de bijlage `Mcp:MaxAttachmentBytes` overschrijdt,
of wanneer de sleutel geen machtiging heeft voor het Account van de e-mail.

### get_email_eml {#get_email_eml}

Geeft een e-mail terug als een volledige RFC 5322-bericht (`.eml`), opnieuw opgebouwd uit de
gearchiveerde velden: de opgeslagen headers (`Received`, `Authentication-Results`,
`X-Spam-*`, …), bodies en bijlagen. Werkt voor elke gearchiveerde e-mail, maar het
resultaat is niet byte-identiek aan wat is ontvangen, dus DKIM-handtekeningen zullen niet
verifiëren tegen dit bericht — gebruik [`get_email_raw`](#get_email_raw) hiervoor. Spiegelt
`GET /api/v1/emails/{id}/eml`.

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | De gearchiveerde e-mail id (vanuit `search_emails`). |

**Resultaat:** een object met `emailId`, `kind` (`eml`), `source` (`null`),
`sha256`, `size`, `encoding` en `content`. `content` is de berichttekst wanneer
`encoding` `utf-8` is, en het bericht base64-gecodeerd wanneer `encoding`
`base64` is (berichten die geen geldige UTF-8 zijn, zodat geen enkele byte wordt gewijzigd). De tool
geeft een foutmelding wanneer `Mcp:AllowMessageDownloads=false`, wanneer het bericht
`Mcp:MaxMessageBytes` overschrijdt, of wanneer de sleutel niet is geautoriseerd voor het
account van de e-mail.

### get_email_raw {#get_email_raw}

Geeft het originele bericht exact terug zoals ontvangen, voor spam-analyse,
DKIM/SPF/ARC-controles of forensisch onderzoek. Alleen beschikbaar wanneer het origineel werd opgeslagen op het moment van
Archief (`RawMessages:Enabled`, zie
[Original Message Storage Settings](Setup.md#-original-message-storage-settings));
`get_email` rapporteert het als `hasRawMessage`. Spiegelt
`GET /api/v1/emails/{id}/raw`.

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | De gearchiveerde e-mail id (van `search_emails`). |

**Result:** hetzelfde object als `get_email_eml` met `kind` `raw`, `source` ingesteld
op hoe het origineel is vastgelegd (`imap`, `graph`, `eml-file` — exacte kopieën —
of `serialized`, herschreven door de parser met regelafsluitingen genormaliseerd naar CRLF)
en `sha256` de checksum die is opgenomen op het moment van Archief. Geeft dezelfde fouten terug als
`get_email_eml`, en een fout wanneer er geen origineel is opgeslagen voor de e-mail.

De bytes worden gelezen van waar ze ook zijn opgeslagen — de database of een bestand,
afhankelijk van `RawMessages:Storage` op het moment van Archief (zie
[Storage backend](RawMessages.md#storage-backend)). Met
`RawMessages:FileSystem:VerifyChecksumOnRead` (standaard `true`) wordt het bestand
opnieuw gehasht voordat het wordt geretourneerd; een bericht waarvan de inhoud niet meer overeenkomt met de
opgenomen checksum wordt geweigerd met een fout in plaats van geretourneerd.

## Rate limiting {#rate-limiting}

De MCP server heeft een eigen fixed-window rate-limit beleid (`"Mcp"`), gesegmenteerd
op basis van de niet-geheime API-key prefix (de eerste 11 tekens van de `ma_...` token),
waarbij wordt teruggevallen op het client IP wanneer er geen bearer token aanwezig is. Het budget wordt
geconfigureerd via `Mcp:RateLimitPerMinute` (standaard `120` per minuut). Wanneer dit
wordt overschreden, geeft de endpoint `429 Too Many Requests` terug met een `Retry-After`
header. Dit staat los van het rate-limit beleid van de REST API.

## Toegangslogging {#access-logging}

Elke tool-aanroep wordt gelogd via hetzelfde `IAccessLogService` als de REST API,
met de eigenaar-gebruikersnaam en het bijbehorende toegangstype (`Search`, `Open`,
`Download`). MCP-activiteit is daarom zichtbaar op de **Logs** pagina naast
REST API- en web-UI-activiteit, voor een enkel audit trail.

## Het verbinden van een agent {#connecting-an-agent}

De MCP server spreekt de standaard MCP Streamable HTTP transport, dus elke MCP
client die HTTP ondersteunt, kan verbinding maken. De URL is de base URL van de applicatie plus
`/mcp`, bijv. `http://localhost:5179/mcp`, en de API-sleutel wordt geleverd via de
`Authorization: Bearer ma_...` header.

Voor agents die zijn geconfigureerd via de standaard MCP client JSON, ziet een vermelding er als volgt uit:

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

## Beveiligingsrichtlijnen voor blootstelling aan het internet {#security-guidance-for-internet-exposure}

De MCP-server is zo ontworpen dat, met de standaardinstellingen en de hieronder beschreven hardening, gearchiveerde e-mail het systeem niet kan verlaten zonder een geldige per-gebruiker API-key die expliciet aan een Account is uitgegeven. De onderstaande punten zijn **verplichte kost voordat u `/mcp` blootstelt aan het internet**.

### Verplicht (blokkeren van exploitatie / data-exfiltratie) {#mandatory-block-exploitation-data-exfiltration}

1. **Alleen HTTPS.** Beëindig TLS aan de rand (reverse proxy). Een bearer API-key via gewone HTTP kan worden onderschept en hergebruikt.
2. **Houd `Mcp:Enabled=false` ingeschakeld, tenzij u het daadwerkelijk gebruikt.** Wanneer uitgeschakeld, geeft `/mcp` `404` terug en is er geen code-pad bereikbaar; dit is de veilige standaard en de veilige staat voor implementaties die geen toegang door AI-agenten nodig hebben.
3. **Rate limiting wordt afgedwongen op het eindpunt.** Het `"Mcp"`-beleid (`Mcp:RateLimitPerMinute`, standaard `120`) wordt toegepast op het gekoppelde `/mcp`-eindpunt. Het is gepartitioneerd op basis van het niet-geheime API-key-prefix (de eerste 11 tekens van de `ma_...`-token) of het client-IP wanneer er geen bearer token aanwezig is. **Verlaag `RateLimitPerMinute`** voor blootstelling aan het internet (bijv. `30`–`60`) — MCP tool-aanroepen zijn zwaarder dan REST-aanroepen en het eindpunt moet niet op een hoog budget worden gelaten wanneer onbetrouwbare clients erbij kunnen.
4. **Reverse-proxy `X-Forwarded-For` moet zorgvuldig worden vertrouwd.** De rate limiter valt terug op `RemoteIpAddress`, wat onder een proxy het proxy-IP is, tenzij `UseForwardedHeaders` het echte client-IP krijgt gevoed. Zorg ervoor dat uw proxy `X-Forwarded-For` correct instelt en dat alleen uw proxy dit kan instellen; anders kan een aanvaller de rate-limit-partitie vervalsen.
5. **Plaats `/mcp` niet achter een CDN-cache.** MCP-responses zijn per verzoek JSON-RPC en mogen nooit worden gecached — caching zou zowel het protocol verbreken als het resultaat van de ene gebruiker lekken naar een andere. Markeer `/mcp` als niet-cachebaar aan de rand (`Cache-Control: no-store`).

### API-key hygiëne (blokkeren van ongeautoriseerde toegang) {#api-key-hygiene-block-unauthorized-access}

6. **Geef toegewijde, kortstondige keys uit voor MCP-agenten.** Een enkele `ma_...`-key is geldig voor zowel `/api` als `/mcp`; er is momenteel geen scope per protocol. Daarom:
   - Maak een **aparte key per agent** aan (hergebruik geen key van een rapportagescript voor een externe LLM-agent).
   - Stel altijd een korte `ExpiresAt` in en roteer deze; trek de key onmiddellijk in wanneer de agent wordt uitgefaseerd.
   - Geef de voorkeur aan keys die eigendom zijn van een **beperkte (niet-admin) gebruiker** wiens enige toegewezen Accounts de Accounts zijn die de agent nodig heeft. Een admin-key geeft zicht op *alle* e-mailaccounts; een beperkte key beperkt de impact bij een eventueel lek.
7. **Behandel de API-key als een wachtwoord.** Het is een bearer-credential. Embed deze nooit in client-side code, log deze nooit en commit deze nooit naar een repo. Plaats deze in de secret store van de MCP-client en roteer bij vermoeden van compromittering.
8. **Audit op de Logs-pagina.** Elke tool-aanroep wordt geregistreerd in `AccessLogs` (`Search`, `Open`, `Download`) onder de eigenaar-gebruiker. Bekijk periodiek de MCP-activiteit op onverwachte patronen (volume-pieken, lezen buiten kantoortijden, massale downloads).

### Vertrouwensmodel voor geretourneerde gegevens (blokkeren van schade aan downstream-systemen) {#trust-model-for-returned-data-block-downstream-harm}

9. **E-mailberichten en Bijlagen zijn onbetrouwbare inhoud**, precies zoals ze arriveerden in de Postbus. Ze kunnen malware, phishing-links of actieve HTML/JavaScript bevatten.
   - `get_email` geeft `htmlBody` terug, **ruw en ongesanitiseerd**. Render dit **niet** voor een mens zonder sanitization (DOMPurify / `HtmlSanitizer` / een sandbox). Het renderen van ruwe gearchiveerde HTML is een stored-XSS-vector.
   - `get_attachment` geeft de ruwe bytes terug met de originele `contentType`. Open of voer Bijlagen **niet** automatisch uit; behandel het `contentType`-veld als onbetrouwbare metadata, niet als een veilige instructie.
   - `get_email_eml` en `get_email_raw` geven het volledige bericht terug inclusief elke Bijlage. Dezelfde regels gelden voor elk van de onderdelen.
10. **Laat de agent geen schrijfacties uitvoeren op basis van e-mailinhoud zonder bevestiging.** Een kwaadaardige e-mail kan prompt-injection-tekst bevatten die gericht is op de consumerende LLM. Dwing machtigingen en bevestiging af in de eigen logica van de agent — de system prompt is geen beveiligingsgrens.

### Aanbevelingen voor defense-in-depth {#defense-in-depth-recommendations}

11. **Beperk `/mcp` op de netwerklaag** waar mogelijk (bijv. sta alleen het IP / VPN-bereik van de agent-host toe bij de reverse proxy), naast API-key-authenticatie. Dit is de meest effectieve controle voor blootstelling aan het internet.
12. **Verlaag `Mcp:AllowAttachmentDownloads` naar `false`** als uw agenten geen bytes van Bijlagen nodig hebben — exfiltratie van Bijlagen is het datapad met het hoogste volume. Metadata (bestandsnaam, content type, grootte) wordt nog steeds geretourneerd via `get_email`.
13. **Verlaag `Mcp:MaxAttachmentBytes`** naar de kleinste waarde die uw agenten daadwerkelijk nodig hebben (standaard `10000000` ≈ 10 MB). Dit begrenst de grootste enkele response die een API-key per verzoek kan ophalen. Hetzelfde geldt voor `Mcp:MaxMessageBytes`, of stel `Mcp:AllowMessageDownloads` in op `false` wanneer agenten geen volledige berichten nodig hebben.
14. **Verlaag `Mcp:MaxResults` en `Mcp:DefaultPageSize`** om het grootste zoekresultaat te begrenzen dat een API-key per verzoek kan ophalen (standaard `100` / `20`).
15. **Monitor `AccessLogs` en rate-limit afwijzingen** (`429`) operationeel. Een plotselinge piek vanaf een enkel key-prefix is een signaal om die key in te trekken.
