# MCP Server (Model Context Protocol) {#mcp-server-model-context-protocol}

Mail Archiver wird mit einem optionalen **MCP (Model Context Protocol) Server** ausgeliefert, der die archivierte E-Mail über den Streamable HTTP-Transport für KI-Agenten bereitstellt. Er spiegelt die schreibgeschützte [REST API](API.md) wider: gleiche Daten, gleiches pro Benutzerbasiertes Berechtigungmodell, gleiche Zugriffsprotokollierung — nur verpackt als MCP-Tools anstelle von REST-Endpunkten, sodass ein LLM-Agent diese direkt entdecken und aufrufen kann.

Der Zweck besteht darin, einem KI-Agenten das Lesen archivierter E-Mails zu ermöglichen, **ohne jemals Postfach-Anmeldedaten preiszugeben oder mit dem E-Mail-Anbieter zu kommunizieren.** Ein eingeschränkter API-Schlüssel pro Benutzer ist eine Berechtigung, um das *Archiv zu lesen*, nicht eine Anmeldedaten, um *das Postfach zu sein* — er erbt nur die Postfach-Berechtigungen seines Besitzers, gewährt keinen Schreibzugriff und kann sofort widerrufen werden, ohne das zugrunde liegende E-Mail-Konto zu beeinflussen.

> ⚠️ **Der MCP-Server ist standardmäßig deaktiviert.** Er muss explizit in der Konfiguration aktiviert werden (siehe [Enabling the MCP server](#enabling-the-mcp-server)). Wenn er deaktiviert ist, gibt der `/mcp`-Endpunkt `404 Not Found` zurück.

Der MCP-Server ist **von Grund auf schreibgeschützt**: Er stellt Tools bereit, um Konten aufzulisten, Ordner aufzulisten, Nachrichten zu suchen, eine Nachricht zu lesen und einen Anhang herunterzuladen. Er erstellt, modifiziert oder löscht niemals Daten.

## Inhaltsverzeichnis {#table-of-contents}

- [Aktivieren des MCP-Servers](#enabling-the-mcp-server)
- [Authentifizierung](#authentication)
- [Transport und Endpunkt](#transport-and-endpoint)
- [Tools](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
  - [get_email_eml](#get_email_eml)
  - [get_email_raw](#get_email_raw)
- [Rate Limiting](#rate-limiting)
- [Zugriffsprotokollierung](#access-logging)
- [Verbinden eines Agenten](#connecting-an-agent)
- [Sicherheitshinweise für die Internetexposition](#security-guidance-for-internet-exposure)

## Aktivieren des MCP-Servers {#enabling-the-mcp-server}

Der MCP-Server wird im Abschnitt `Mcp` von `appsettings.json` (oder den entsprechenden Umgebungsvariablen, z. B. `Mcp__Enabled=true`) konfiguriert:

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

| Einstellung | Standard | Beschreibung |
| --- | --- | --- |
| `Enabled` | `false` | Hauptschalter. Wenn `false`, gibt der `/mcp`-Endpunkt `404` zurück. |
| `AllowAttachmentDownloads` | `true` | Wenn `false`, gibt `get_attachment` einen Fehler anstelle von Anhang-Bytes zurück. Metadaten über Anhänge werden weiterhin von `get_email` zurückgegeben. |
| `DefaultPageSize` | `20` | Seitengröße, die von `search_emails` verwendet wird, wenn der Aufrufer `pageSize` weglässt. |
| `MaxResults` | `100` | Obergrenze für die `search_emails`-Seitengröße; größere Werte werden begrenzt. |
| `MaxAttachmentBytes` | `10000000` | Maximale Anhanggröße (in Bytes), die `get_attachment` inline als base64 zurückgibt; größere Anhänge werden abgelehnt. |
| `AllowMessageDownloads` | `true` | Wenn `false`, geben `get_email_eml` und `get_email_raw` einen Fehler anstelle der Nachricht zurück. Eine vollständige Nachricht enthält ihre Anhänge, daher müssen **sowohl** dieser Schalter als auch `AllowAttachmentDownloads` auf `true` stehen; das Deaktivieren eines von beiden deaktiviert beide Tools. |
| `MaxMessageBytes` | `10000000` | Maximale Nachrichtengröße (in Bytes), die `get_email_eml` und `get_email_raw` inline zurückgeben; größere Nachrichten werden abgelehnt. |
| `RateLimitPerMinute` | `120` | Festes Zeitfenster-Anfragebudget pro API-Schlüssel pro Minute (siehe [Rate limiting](#rate-limiting)). |

## Authentifizierung {#authentication}

Der MCP-Server authentifiziert sich mit den **gleichen pro Benutzer gültigen API-Schlüsseln wie die REST-API**, die als Bearer-Token gesendet werden:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

API-Schlüssel werden in der Web-UI unter **API Keys** erstellt und verwaltet. Ein Schlüssel erbt die exakten Berechtigungen des Benutzers, dem er gehört (ein Admin sieht alle Konten; ein eingeschränkter Benutzer sieht nur seine zugewiesenen Konten). Schlüssel können unabhängig von den zugrunde liegenden E-Mail-Konten widerrufen werden oder ablaufen. Weitere Informationen zum vollständigen Schlüssel-Lebenszyklus und zum Berechtigungsmodell finden Sie im [Abschnitt zur REST-API-Authentifizierung](API.md#authentication) — MCP-Schlüssel sind kein separater Anmeldedaten-Speicher.

Wenn der MCP-Server deaktiviert ist oder kein/ungültiger Bearer-Token übermittelt wird, gibt der Endpunkt `404` bzw. `401` zurück (er leitet niemals auf die Login-Seite weiter).

## Transport und Endpunkt {#transport-and-endpoint}

Der MCP-Server verwendet den **Streamable HTTP**-Transport und ist unter folgendem Pfad gemountet:

```
POST /mcp
```

Er läuft im **stateless**-Modus (eine JSON-RPC-Anfrage pro HTTP-Anfrage), was der empfohlene Modus für Server ist, die keine Server-zu-Client-Anfragen wie Sampling oder Elicitation benötigen. Tools werden über die standardmäßige MCP `tools/list`-Anfrage entdeckt und über `tools/call` aufgerufen.

## Tools {#tools}

### list_accounts {#list_accounts}

Listet die E-Mail-Konten auf, auf die der aktuelle API-Schlüssel zugreifen darf. Gibt `id`, `name`, `emailAddress`, `provider`, `isEnabled` und `lastSync` zurück.

**Parameter:** keine.

**Ergebnis:** Array von Konto-Objekten. Verwenden Sie das zurückgegebene `id` mit `list_folders` und `search_emails`.

### list_folders {#list_folders}

Listet den Ordnerbaum eines einzelnen E-Mail-Kontos mit E-Mail-Zahlen pro Ordner auf.

**Parameter:**

| Name | Typ | Erforderlich | Beschreibung |
| --- | --- | --- | --- |
| `accountId` | integer | ja | Die E-Mail-Konto-ID (aus `list_accounts`). |

**Ergebnis:** Array von Ordner-Objekten, jedes mit `name`, `fullPath`, `totalCount`, `level` und rekursivem `children`. Gibt eine leere Liste zurück, wenn das Konto nicht existiert oder der Schlüssel nicht dafür autorisiert ist.

### search_emails {#search_emails}

Durchsucht archivierte E-Mails mittels Volltextabfrage, Datumsbereich, Konto, Ordner und Richtung.

**Parameter:**

| Name | Typ | Erforderlich | Beschreibung |
| --- | --- | --- | --- |
| `q` | string | nein | Volltext-Suchabfrage (Betreff, Textkörper, Von/An). |
| `from` | string (ISO-8601) | nein | UTC-Datum/Zeit-Filter "gesendet am oder nach". |
| `to` | string (ISO-8601) | nein | UTC-Datum/Zeit-Filter "gesendet am oder vor". |
| `accountId` | integer | nein | Beschränkung auf eine einzige E-Mail-Konto-ID. |
| `folder` | string | nein | Beschränkung auf einen vollständigen Ordnerpfad (z. B. `INBOX`). |
| `direction` | string | nein | `incoming` oder `outgoing`. |
| `page` | integer | nein | Seitenzahl (beginnend bei 1, Standard `1`). |
| `pageSize` | integer | nein | Seitengröße; begrenzt auf `[1, MaxResults]`. `0` verwendet `DefaultPageSize`. |
| `sortBy` | string | nein | `sentdate` (Standard), `receiveddate`, `subject`, `from`, `to`. |
| `sortOrder` | string | nein | `asc` oder `desc` (Standard `desc`). |

**Ergebnis:** ein paginiertes Objekt mit `items` (Array von E-Mail-Zusammenfassungen), `page`, `pageSize`, `totalItems`, `totalPages`. Jede Zusammenfassung enthält `id`, `accountId`, `subject`, `from`, `to`, `sentDate`, `isOutgoing`, `hasAttachments` und `folderName`. Übergeben Sie ein zurückgegebenes `id` an `get_email` für den vollständigen Textkörper und die Liste der Anhänge.

### get_email {#get_email}

Ruft den vollständigen Inhalt einer einzelnen archivierten E-Mail ab.

**Parameter:**

| Name | Typ | Erforderlich | Beschreibung |
| --- | --- | --- | --- |
| `id` | integer | ja | Die archivierte E-Mail-ID (aus `search_emails`). |

**Ergebnis:** ein einzelnes E-Mail-Objekt mit allen Zusammenfassungsfeldern plus `cc`, `bcc`, `receivedDate`, `messageId`, `textBody`, `htmlBody` und `attachments` (Array von `{ id, fileName, contentType, size }`) sowie `hasRawMessage` (ob `get_email_raw` das Original zurückgeben kann). Gibt `null` zurück, wenn die E-Mail nicht existiert oder der Schlüssel nicht für das Konto autorisiert ist.

### get_attachment {#get_attachment}

Lädt einen einzelnen E-Mail-Anhang als Base64-Blob herunter.

**Parameter:**

| Name | Typ | Erforderlich | Beschreibung |
| --- | --- | --- | --- |
| `id` | integer | ja | Die archivierte E-Mail-ID, zu der der Anhang gehört. |
| `attachmentId` | integer | ja | Die Anhang-ID (aus `get_email`'s `attachments`). |

**Ergebnis:** ein Objekt mit `id`, `emailId`, `fileName`, `contentType`, `size` und `base64Data` (die rohen Anhang-Bytes, base64-kodiert). Das Tool gibt einen Fehler zurück, wenn `Mcp:AllowAttachmentDownloads=false`, wenn der Anhang `Mcp:MaxAttachmentBytes` überschreitet oder wenn der Schlüssel nicht für das Konto der E-Mail autorisiert ist.

### get_email_eml {#get_email_eml}

Gibt eine E-Mail als vollständige RFC 5322-Nachricht (`.eml`) zurück, die aus den archivierten Feldern neu aufgebaut wurde: die gespeicherten Header (`Received`, `Authentication-Results`, `X-Spam-*`, …), Textkörper und Anhänge. Funktioniert für jede archivierte E-Mail, aber das Ergebnis ist nicht byteidentisch mit dem Empfangenen, sodass DKIM-Signaturen nicht verifiziert werden können — verwenden Sie [`get_email_raw`](#get_email_raw) dafür. Spiegelt `GET /api/v1/emails/{id}/eml` wider.

**Parameter:**

| Name | Typ | Erforderlich | Beschreibung |
| --- | --- | --- | --- |
| `id` | integer | ja | Die archivierte E-Mail-ID (aus `search_emails`). |

**Ergebnis:** ein Objekt mit `emailId`, `kind` (`eml`), `source` (`null`), `sha256`, `size`, `encoding` und `content`. `content` ist der Nachrichtentext, wenn `encoding` `utf-8` ist, und die Nachricht ist base64-kodiert, wenn `encoding` `base64` ist (Nachrichten, die kein gültiges UTF-8 sind, sodass kein Byte verändert wird). Das Tool gibt einen Fehler zurück, wenn `Mcp:AllowMessageDownloads=false`, wenn die Nachricht `Mcp:MaxMessageBytes` überschreitet oder wenn der Schlüssel nicht für das Konto der E-Mail autorisiert ist.

### get_email_raw {#get_email_raw}

Gibt die Originalnachricht exakt so zurück, wie sie empfangen wurde, für Spam-Analysen, DKIM/SPF/ARC-Prüfungen oder Forensik. Nur verfügbar, wenn das Original zum Zeitpunkt des Archivs gespeichert wurde (`RawMessages:Enabled`, siehe [Original Message Storage Settings](Setup.md#-original-message-storage-settings)); `get_email` meldet dies als `hasRawMessage`. Spiegelt `GET /api/v1/emails/{id}/raw` wider.

**Parameter:**

| Name | Typ | Erforderlich | Beschreibung |
| --- | --- | --- | --- |
| `id` | integer | ja | Die archivierte E-Mail-ID (aus `search_emails`). |

**Ergebnis:** dasselbe Objekt wie `get_email_eml` mit `kind` `raw`, wobei `source` so gesetzt ist, wie das Original erfasst wurde (`imap`, `graph`, `eml-file` — exakte Kopien — oder `serialized`, vom Parser neu geschrieben mit normalisierten Zeilenendungen auf CRLF) und `sha256` der zum Zeitpunkt des Archivs aufgezeichnete Prüfsummenwert. Gibt dieselben Fehler wie `get_email_eml` zurück sowie einen Fehler, wenn kein Original für die E-Mail gespeichert ist.

Die Bytes werden von dort gelesen, wo sie gespeichert wurden — der Datenbank oder einer Datei, abhängig von `RawMessages:Storage` zum Zeitpunkt des Archivs (siehe [Storage backend](RawMessages.md#storage-backend)). Mit `RawMessages:FileSystem:VerifyChecksumOnRead` (Standard `true`) wird die Datei vor der Rückgabe erneut gehasht; eine Nachricht, deren Inhalt nicht mehr mit der aufgezeichneten Prüfsumme übereinstimmt, wird mit einem Fehler abgelehnt, anstatt zurückgegeben zu werden.

## Rate limiting {#rate-limiting}

Der MCP-Server verfügt über eine eigene Fixed-Window-Rate-Limit-Policy (`"Mcp"`), die nach dem nicht-geheimen API-Key-Präfix (die ersten 11 Zeichen des `ma_...`-Tokens) partitioniert ist und auf die Client-IP zurückgreift, wenn kein Bearer-Token vorhanden ist. Das Budget wird über `Mcp:RateLimitPerMinute` konfiguriert (Standard `120` pro Minute). Wenn das Limit überschritten wird, gibt der Endpunkt `429 Too Many Requests` mit einem `Retry-After`-Header zurück. Dies ist unabhängig von der Rate-Limit-Policy der REST-API.

## Zugriffsprotokollierung {#access-logging}

Jeder Tool-Aufruf wird über dasselbe `IAccessLogService` wie die REST API protokolliert,
mit dem Besitzer-Benutzernamen und dem entsprechenden Zugriffstyp (`Search`, `Open`,
`Download`). MCP-Aktivitäten sind daher auf der Seite **Logs** zusammen mit
REST API- und Web-UI-Aktivitäten sichtbar, für ein einzelnes Audit-Protokoll.

## Verbindung eines Agenten {#connecting-an-agent}

Der MCP-Server nutzt den standardmäßigen MCP Streamable HTTP-Transport, sodass jeder MCP-Client, der HTTP unterstützt, eine Verbindung herstellen kann. Die URL ist die Basis-URL der Anwendung plus `/mcp`, z. B. `http://localhost:5179/mcp`, und der API-Schlüssel wird über den `Authorization: Bearer ma_...`-Header bereitgestellt.

Für Agenten, die über das standardmäßige MCP-Client-JSON konfiguriert sind, sieht ein Eintrag wie folgt aus:

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

## Sicherheitsrichtlinien für die Internetexposition {#security-guidance-for-internet-exposure}

Der MCP-Server ist so konzipiert, dass mit den Standardeinstellungen und der unten aufgeführten Härtung archivierte E-Mails das System nicht verlassen können, ohne dass ein gültiger, pro Benutzer ausgestellter API-Key vorliegt, der explizit einem Konto zugewiesen wurde. Die folgenden Punkte sind **zwingend vor der Exposition von `/mcp` gegenüber dem Internet zu lesen**.

### Obligatorisch (Blockierung von Exploitation / Datenexfiltration) {#mandatory-block-exploitation-data-exfiltration}

1. **Nur HTTPS.** Terminieren Sie TLS am Edge (Reverse Proxy). Ein Bearer-API-Key über unverschlüsseltes HTTP kann abgefangen und wiederverwendet werden.
2. **Behalten Sie `Mcp:Enabled=false` bei, es sei denn, Sie verwenden es tatsächlich.** Wenn es deaktiviert ist, gibt `/mcp` `404` zurück und kein Code-Pfad ist erreichbar; dies ist der sichere Standard und der sichere Zustand für Bereitstellungen, die keinen Zugriff durch KI-Agenten benötigen.
3. **Rate Limiting wird am Endpunkt erzwungen.** Die `"Mcp"`-Richtlinie (`Mcp:RateLimitPerMinute`, Standard `120`) wird auf den gemappten `/mcp`-Endpunkt angewendet. Sie ist nach dem nicht-geheimen API-Key-Präfix (die ersten 11 Zeichen des `ma_...`-Tokens) oder der Client-IP partitioniert, wenn kein Bearer-Token vorhanden ist. **Stellen Sie `RateLimitPerMinute` niedriger ein** für die Internetexposition (z. B. `30`–`60`) — MCP-Tool-Aufrufe sind schwerfälliger als REST-Aufrufe, und der Endpunkt sollte nicht mit einem hohen Budget belassen werden, wenn nicht vertrauenswürdige Clients darauf zugreifen können.
4. **Reverse-Proxy `X-Forwarded-For` muss sorgfältig geprüft werden.** Der Rate Limiter fällt auf `RemoteIpAddress` zurück, was unter einem Proxy die Proxy-IP ist, es sei denn, `UseForwardedHeaders` erhält die echte Client-IP. Stellen Sie sicher, dass Ihr Proxy `X-Forwarded-For` korrekt setzt und nur Ihr Proxy dies tun kann; andernfalls kann ein Angreifer die Rate-Limit-Partition fälschen.
5. **Platzieren Sie `/mcp` nicht hinter einem CDN-Cache.** MCP-Antworten sind pro Anfrage JSON-RPC und dürfen niemals gecacht werden — Caching würde sowohl das Protokoll unterbrechen als auch das Ergebnis eines Benutzers an einen anderen durchsickern lassen. Markieren Sie `/mcp` am Edge als nicht cachebar (`Cache-Control: no-store`).

### API-Key-Hygiene (Blockierung unbefugter Zugriffe) {#api-key-hygiene-block-unauthorized-access}

6. **Stellen Sie dedizierte, kurzlebige Keys für MCP-Agenten aus.** Ein einzelner `ma_...`-Key ist sowohl für `/api` als auch für `/mcp` gültig; es gibt heute keine protokollspezifische Scope-Trennung. Daher:
   - Erstellen Sie einen **separaten Key pro Agent** (verwenden Sie keinen Key eines Reporting-Skripts für einen externen LLM-Agenten wieder).
   - Setzen Sie immer eine kurze `ExpiresAt` und rotieren Sie diese; widerrufen Sie den Key sofort, wenn der Agent außer Betrieb genommen wird.
   - Bevorzugen Sie Keys, die einem **eingeschränkten (Nicht-Admin-) Benutzer** gehören, dessen einzige zugewiesenen Konten diejenigen sind, die der Agent benötigt. Ein Admin-Key gewährt Sichtbarkeit auf *alle* E-Mail-Konten; ein eingeschränkter Key begrenzt den Schadensradius.
7. **Behandeln Sie den API-Key wie ein Passwort.** Er ist eine Bearer-Anmeldeinformation. Betten Sie ihn niemals in clientseitigen Code ein, protokollieren Sie ihn niemals und committen Sie ihn niemals in ein Repo. Speichern Sie ihn im Secret Store des MCP-Clients und rotieren Sie ihn bei Verdacht auf Kompromittierung.
8. **Audit auf der Logs-Seite.** Jeder Tool-Aufruf wird in `AccessLogs` (`Search`, `Open`, `Download`) unter dem Besitzer-Benutzer aufgezeichnet. Überprüfen Sie regelmäßig die MCP-Aktivität auf unerwartete Muster (Volumenspitzen, Lesezugriffe außerhalb der Geschäftszeiten, Massen-Downloads).

### Vertrauensmodell für zurückgegebene Daten (Blockierung von Folgeschäden) {#trust-model-for-returned-data-block-downstream-harm}

9. **E-Mail-Bodies und Anhänge sind nicht vertrauenswürdige Inhalte**, genau wie sie waren, als sie im Postfach ankamen. Sie können Malware, Phishing-Links oder aktives HTML/JavaScript enthalten.
   - `get_email` gibt `htmlBody` **roh und unsanitized** zurück. Rendern Sie dies **nicht** für einen Menschen ohne Bereinigung (DOMPurify / `HtmlSanitizer` / eine Sandbox). Das Rendern von rohem archiviertem HTML ist ein Stored-XSS-Vektor.
   - `get_attachment` gibt die Rohbytes mit dem ursprünglichen `contentType` zurück. Öffnen oder führen Sie Anhänge **nicht** automatisch aus; behandeln Sie das `contentType`-Feld als nicht vertrauenswürdige Metadaten, nicht als sichere Anweisung.
   - `get_email_eml` und `get_email_raw` geben die vollständige Nachricht inklusive jedes Anhangs zurück. Dieselben Regeln gelten für jeden ihrer Teile.
10. **Lassen Sie den Agenten keine Schreibaktionen basierend auf E-Mail-Inhalten ohne Bestätigung durchführen.** Eine bösartige E-Mail kann Prompt-Injection-Text enthalten, der auf das konsumierende LLM abzielt. Erzwingen Sie Berechtigungen und Bestätigungen in der eigenen Logik des Agenten — der System-Prompt ist keine Sicherheitsgrenze.

### Defense-in-Depth-Empfehlungen {#defense-in-depth-recommendations}

11. **Beschränken Sie `/mcp` auf der Netzwerkschicht**, sofern möglich (z. B. nur die IP / den VPN-Bereich des Agent-Hosts am Reverse Proxy zulassen), zusätzlich zur API-Key-Authentifizierung. Dies ist die effektivste Einzelmaßnahme für die Internetexposition.
12. **Senken Sie `Mcp:AllowAttachmentDownloads` auf `false`**, wenn Ihre Agenten keine Anhang-Bytes benötigen — die Exfiltration von Anhängen ist der datenintensivste Pfad. Metadaten (Dateiname, Inhaltstyp, Größe) werden weiterhin über `get_email` zurückgegeben.
13. **Senken Sie `Mcp:MaxAttachmentBytes`** auf den kleinsten Wert, den Ihre Agenten tatsächlich benötigen (Standard `10000000` ≈ 10 MB). Dies begrenzt die größte einzelne Antwort, die ein API-Key pro Anfrage abrufen kann. Dasselbe gilt für `Mcp:MaxMessageBytes`, oder setzen Sie `Mcp:AllowMessageDownloads` auf `false`, wenn Agenten keine vollständigen Nachrichten benötigen.
14. **Senken Sie `Mcp:MaxResults` und `Mcp:DefaultPageSize`**, um das größte Suchergebnis zu begrenzen, das ein API-Key pro Anfrage abrufen kann (Standard `100` / `20`).
15. **Überwachen Sie `AccessLogs` und Rate-Limit-Ablehnungen** (`429`) im Betrieb. Ein plötzlicher Anstieg von einem einzelnen Key-Präfix ist ein Signal, diesen Key zu widerrufen.
