# Read-Only REST API (v1) {#read-only-rest-api-v1}

Mail Archiver liefert eine optionale, **schreibgeschützte** REST API für den programmatischen Zugriff auf archivierte E-Mails mit. Sie ist für Automatisierungs- und Integrationsanwendungsfälle (Reporting, E-Discovery-Exporte, Monitoring) konzipiert, ohne die Web-UI nutzen zu müssen.

Das Ziel ist es, einem Skript oder KI-Agenten das Lesen archivierter E-Mails zu ermöglichen, **ohne jemals die Zugangsdaten des Postfachs preiszugeben oder mit dem E-Mail-Anbieter zu kommunizieren.** Ein eingeschränkter API-Schlüssel pro Benutzer ist eine Funktion, um *das Archiv zu lesen*, nicht ein Zugangsdatum, um *das Postfach zu sein* — er erbt nur die Postfach-Berechtigungen seines Besitzers, gewährt keinen Schreibpfad und kann sofort widerrufen werden, ohne das zugrunde liegende E-Mail-Konto zu beeinflussen.

> ⚠️ **Die API ist standardmäßig deaktiviert.** Sie muss explizit in der Konfiguration aktiviert werden (siehe [Enabling the API](#enabling-the-api)). Wenn sie deaktiviert ist, gibt jede `/api/*`-Route `404 Not Found` zurück.

Die API ist **designbedingt schreibgeschützt**: Sie bietet Zugriff auf Konten, Ordner, Nachrichtensuche, Nachrichtendetails und Downloads von Anhängen. Sie erstellt, modifiziert oder löscht niemals Daten.

## Inhaltsverzeichnis {#table-of-contents}

- [Aktivierung der API](#enabling-the-api)
- [Authentifizierung](#authentication)
  - [API-Key-Format](#api-key-format)
  - [Key-Lebenszyklus](#key-lifecycle)
  - [Berechtigungsmodell](#permission-model)
  - [Sicherheitsbegründung](#security-rationale)
- [Konventionen](#conventions)
  - [Base URL und Versionierung](#base-url-and-versioning)
  - [Pagination](#pagination)
  - [Fehler](#errors)
  - [Rate Limiting](#rate-limiting)
- [Endpunkte](#endpoints)
  - [Konten auflisten](#list-accounts)
  - [Ordner auflisten](#list-folders)
  - [Nachrichten suchen](#search-messages)
  - [Nachricht abrufen](#get-message)
  - [Anhang herunterladen](#download-attachment)
  - [Nachricht (.eml) herunterladen](#download-message-eml)
  - [Originalnachricht herunterladen](#download-original-message)
  - [Statistiken](#stats)
- [Datentypen](#data-types)
- [Zugriffsprotokollierung](#access-logging)
- [OpenAPI und Swagger UI](#openapi-and-swagger-ui)
- [API-Referenz](API-Reference.md) — generierte Endpunkt- und Datentyp-Referenz

## Aktivierung der API {#enabling-the-api}

Die API wird unter dem Abschnitt `Api` von `appsettings.json` (oder den entsprechenden Umgebungsvariablen, z. B. `Api__Enabled=true`) konfiguriert:

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


### Lebenszyklus der Schlüssel {#key-lifecycle}

1. **Erstellen** — ein Benutzer erstellt einen Schlüssel mit einem beschreibenden `Name` und einem optionalen Ablaufdatum. Die Antwort zeigt den Schlüssel im Klartext nur einmal an.
2. **Verwenden** — senden Sie den Schlüssel als Bearer-Token. Jede akzeptierte Anfrage aktualisiert den `LastUsedAt` des Schlüssels (Schreibvorgänge werden auf maximal einmal alle ~5 Minuten gedrosselt, um einen Schreibvorgang bei jedem Aufruf zu vermeiden).
3. **Ablaufen** — wenn `ExpiresAt` gesetzt ist und in der Vergangenheit liegt, wird der Schlüssel abgelehnt.
4. **Widerrufen** — der Besitzer oder ein Administrator widerruft den Schlüssel (`RevokedAt` ist gesetzt); er wird sofort und dauerhaft abgelehnt.

Ein Schlüssel ist **aktiv**, wenn er nicht widerrufen, nicht abgelaufen ist **und** sein Besitzer noch aktiv ist. Ein Schlüssel, der aus einem dieser Gründe inaktiv ist, wird mit `401` abgelehnt.

### Berechtigungsmodell {#permission-model}

Ein API-Schlüssel erbt exakt die Berechtigungen des Benutzers, dem er gehört. Das authentifizierte Subjekt trägt dieselben Claims, die ein normaler Web-Login ausgibt, sodass alle bestehenden Autorisierungen...

| Einstellung | Standard | Beschreibung |
| --- | --- | --- |
| `Enabled` | `false` | Hauptschalter. Wenn `false`, geben alle `/api/*`-Routen `404` zurück und das OpenAPI-Dokument / Swagger UI werden nicht gemappt. |
| `AllowAttachmentDownloads` | `true` | Wenn `false`, gibt der Endpunkt für den Anhang-Download `403` zurück. Metadaten über Anhänge werden weiterhin vom Nachrichtendetail-Endpunkt zurückgegeben. |
| `AllowMessageDownloads` | `true` | Wenn `false`, geben die Complete-Message-Endpunkte (`/eml` und `/raw`) `403` zurück. Eine vollständige Nachricht enthält ihre Anhänge, daher müssen **sowohl** dieser Schalter als auch `AllowAttachmentDownloads` auf `true` stehen; das Deaktivieren eines der beiden deaktiviert beide Endpunkte. |
| `EnableSwaggerUi` | `true` | Wenn `true` (und `Enabled` auf `true` steht), wird Swagger UI unter `/apidocs` und das OpenAPI-Dokument unter `/apidocs/spec/v1.json` bereitgestellt. Beides erfordert eine authentifizierte **Web-(Cookie)-Sitzung** — sie sind nicht Teil von `/api/` und sind mit einem API-Schlüssel nicht erreichbar. |
| `DefaultPageSize` | `20` | Seitengröße, die verwendet wird, wenn eine Anfrage `pageSize` weglässt. |
| `MaxPageSize` | `100` | Obergrenze; größere `pageSize`-Werte werden auf diesen Wert begrenzt. |
| `RateLimitPerMinute` | `120` | Festes Zeitfenster-Anfragebudget pro API-Schlüssel pro Minute (siehe [Rate limiting](#rate-limiting)). |

## Authentifizierung {#authentication}

Die API authentifiziert sich mit **pro-Benutzer-API-Schlüsseln**, die als Bearer-Token gesendet werden:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` akzeptiert **nur** API-Schlüssel. Browser-Cookies werden nicht berücksichtigt, sodass die API keine CSRF-Angriffsfläche besitzt. Authentifizierungsfehler geben `401` mit einem `WWW-Authenticate: Bearer`-Header und einem [problem+json](#errors)-Body zurück — die API leitet **niemals** auf die Login-Seite weiter.

Schlüssel werden im Web-UI unter **API Keys** im Self-Service verwaltet (jeder Benutzer verwaltet seine eigenen Schlüssel; Administratoren können die Schlüssel aller Benutzer einsehen und widerrufen).

### API-Schlüssel-Format {#api-key-format}

Ein Schlüssel ist der String `ma_` gefolgt von 43 URL-sicheren Base64-Zeichen, die 32 Zufallsbytes (256 Bit Entropie) kodieren:

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Speicherung:

- **`KeyPrefix`** — die ersten ~11 Zeichen (z. B. `ma_8Kf3pQ2`), im Klartext gespeichert. Wird verwendet, um einen Schlüssel in der UI und in den Protokollen zu identifizieren. Kein Geheimnis.
- **`KeyHash`** — das hex-kodierte SHA-256 des vollständigen Schlüssels, gespeichert unter einem **eindeutigen Index**. Der Schlüssel im Klartext wird niemals gespeichert.

Der vollständige Schlüssel wird dem Benutzer **genau einmal** zum Zeitpunkt der Erstellung angezeigt. Er kann danach nicht mehr abgerufen werden; ein verlorener Schlüssel muss widerrufen und ersetzt werden.

### Lebenszyklus des Schlüssels {#key-lifecycle}

1. **Erstellen** — ein Benutzer erstellt einen Schlüssel mit einer beschreibenden `Name` und einem optionalen Ablaufdatum. Die Antwort zeigt den Schlüssel im Klartext einmal an.
2. **Verwenden** — senden Sie den Schlüssel als Bearer-Token. Jede akzeptierte Anfrage aktualisiert die `LastUsedAt` des Schlüssels (Schreibvorgänge werden auf maximal einmal alle ~5 Minuten gedrosselt, um einen Schreibvorgang bei jedem Aufruf zu vermeiden).
3. **Ablaufen** — wenn `ExpiresAt` gesetzt ist und in der Vergangenheit liegt, wird der Schlüssel abgelehnt.
4. **Widerrufen** — der Besitzer des Schlüssels oder ein Administrator widerruft den Schlüssel (`RevokedAt` ist gesetzt); er wird sofort und dauerhaft abgelehnt.

Ein Schlüssel ist **aktiv**, wenn er nicht widerrufen, nicht abgelaufen ist **und** der Besitzer des Schlüssels noch aktiv ist. Ein Schlüssel, der aus einem dieser Gründe inaktiv ist, wird mit `401` abgelehnt.

### Berechtigungsmodell {#permission-model}

Ein API-Schlüssel erbt die exakten Berechtigungen des Benutzers, dem er gehört. Das authentifizierte Principal trägt dieselben Claims, die ein normaler Web-Login ausgibt, sodass die gesamte bestehende Autorisierungslogik unverändert gilt:

- **Administratoren** (`IsAdmin`) sehen alle Mail-Konten und alle Nachrichten.
- **Reguläre Benutzer** sehen nur die Mail-Konten, die ihnen zugewiesen sind (`UserMailAccount`). Konten und Nachrichten außerhalb dieses Sets werden als **nicht existent** behandelt: Anfragen dafür geben `404` zurück, nicht `403`, sodass die API die Existenz von Konten, die ein Aufrufer nicht sehen kann, nicht preisgibt.

### Sicherheitsbegründung {#security-rationale}

- **2FA-Bypass.** API-Schlüssel umgehen per Design die Zwei-Faktor-Authentifizierung — dies ist inhärent bei nicht-interaktiven Token (dasselbe gilt für Personal Access Tokens auf GitHub/GitLab). Dies wird dadurch gemildert, dass Schlüssel **standardmäßig deaktiviert**, widerrufbar, optional zeitlich begrenzt, auf die Berechtigungen des Benutzers beschränkt sind und jeder Zugriff im [Access Log](#access-logging) aufgezeichnet wird.
- **Keine Cookies auf `/api`.** Cookie-Authentifizierung wird auf `/api`-Routen nicht akzeptiert, was CSRF-Bedenken für die API eliminiert.
- **Transport.** Betreiben Sie die API in der Produktion immer über HTTPS, damit Bearer-Token nicht im Netzwerk exponiert werden.

## Konventionen {#conventions}

### Base URL und Versionierung {#base-url-and-versioning}

Alle Endpunkte befinden sich unter `/api/v1`. Die Version ist im Pfad enthalten, sodass zukünftige
bruchbedingte Änderungen als `/api/v2` veröffentlicht werden können, ohne bestehende Clients zu stören.

Antworten sind `application/json`, sofern nicht anders angegeben (Downloads von Anhängen geben den
eigenen Content-Type des Anhangs zurück). Alle Zeitstempel sind UTC, ISO 8601
(`2026-06-11T09:30:00Z`).

### Paginierung {#pagination}

List-Endpunkte geben ein Paginierungs-Envelope zurück:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` ist 1-basiert. Seiten außerhalb des Bereichs geben ein leeres `items`-Array zurück (kein
  Fehler).
- `pageSize` ist standardmäßig `Api:DefaultPageSize` und ist auf `[1, Api:MaxPageSize]` begrenzt.

### Fehler {#errors}

Fehler verwenden [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
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

| Status | Wann |
| --- | --- |
| `400 Bad Request` | Fehlerhafte Parameter (z. B. nicht parsbares Datum, ungültiger `sortBy`). |
| `401 Unauthorized` | Fehlender, fehlerhafter, abgelaufener, widerrufener oder ansonsten ungültiger Schlüssel. Beinhaltet `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Anhang-Downloads angefordert, während `AllowAttachmentDownloads=false`, oder Nachrichten-Downloads, während `AllowMessageDownloads=false`. |
| `404 Not Found` | Ressource existiert nicht **oder** liegt außerhalb der zulässigen Konten des Aufrufers. Wird auch für jede `/api/*`-Route zurückgegeben, wenn `Api:Enabled=false`. |
| `429 Too Many Requests` | Rate Limit überschritten. |
| `500 Internal Server Error` | Unerwarteter Serverfehler (problem+json, niemals die HTML-Fehlerseite). |

### Rate Limiting {#rate-limiting}

Anfragen werden durch eine Fixed-Window-Policy von `Api:RateLimitPerMinute`
Anfragen pro Minute (Standard `120`) begrenzt, partitioniert nach API-Key-Präfix (Rückfall auf Client-IP, wenn kein Key vorhanden ist). Das Überschreiten des Budgets liefert `429` zurück.

## Endpoints {#endpoints}

> Alle Beispiele setzen voraus, dass die API aktiviert ist und `$KEY` einen gültigen Schlüssel enthält.

### Konten auflisten {#list-accounts}

```
GET /api/v1/accounts
```

Gibt die für den Aufrufer sichtbaren Mail-Konten zurück. Felder für Anmeldedaten
(IMAP/OAuth Benutzername, Passwort, Client Secret, …) werden **niemals** enthalten.

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

### Ordner auflisten {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

Gibt den Ordnerbaum für ein Konto als verschachtelte Struktur zurück. Gibt `404` zurück, wenn das Konto nicht existiert oder für den Aufrufer nicht sichtbar ist.

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

### Nachrichten suchen {#search-messages}

```
GET /api/v1/emails
```

Durchsucht archivierte Nachrichten mit denselben Funktionen wie die Web-UI.

| Query-Parameter | Typ | Beschreibung |
| --- | --- | --- |
| `q` | string | Volltextsuche über Betreff, Body, von, an, cc, bcc. Unterstützt Feld-Syntax, z. B. `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Frühestes Sendedatum (inklusiv), `YYYY-MM-DD`. |
| `to` | date | Spätestes Sendedatum (inklusiv), `YYYY-MM-DD`. |
| `accountId` | int | Beschränkung auf ein Konto (muss für den Aufrufer sichtbar sein). |
| `folder` | string | Beschränkung auf einen Ordner über den vollständigen Pfad. |
| `direction` | string | `incoming` oder `outgoing`. |
| `page` | int | 1-basierte Seitennummer (Standard `1`). |
| `pageSize` | int | Ergebnisse pro Seite (Standard `Api:DefaultPageSize`, begrenzt auf `Api:MaxPageSize`). |
| `sortBy` | string | Einer von `sentDate` (Standard), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` oder `desc` (Standard `desc`). |

Gibt ein [paged envelope](#pagination) von [`EmailSummaryDto`](#emailsummarydto) zurück.
Protokolliert einen [access log](#access-logging) Eintrag des Typs `Search`.

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

### Nachricht abrufen {#get-message}

```
GET /api/v1/emails/{id}
```

Gibt die vollständige Nachricht inklusive der Inhalte zurück. Der Inhalt verwendet dieselbe Fallback-Kette wie die Web-UI: `OriginalBodyHtml` (Rohbytes) → `BodyUntruncatedHtml` →
`HtmlBody`, und das Äquivalent für den Textinhalt. Gibt `404` zurück, wenn die Nachricht nicht existiert oder zu einem Konto gehört, für das der Aufrufer keine Berechtigungen hat. Erstellt einen Zugriffseintrag des Typs `Open` im Audit-Protokoll.

Gibt einen [`EmailDetailDto`](#emaildetaildto) zurück.

### Anhang herunterladen {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Streamt die Anhang-Bytes mit dem gespeicherten Content-Type und Dateinamen. Gibt
`404` zurück, wenn die Nachricht oder der Anhang nicht existiert oder für den
Aufrufer nicht sichtbar ist. Gibt `403` (problem+json) zurück, wenn `Api:AllowAttachmentDownloads=false`.
Erstellt einen Zugriffseintrag vom Typ `Download` im Audit-Protokoll.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Nachricht (.eml) herunterladen {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Gibt die Nachricht als `message/rfc822` zurück, die aus den archivierten Feldern neu aufgebaut wurde:
Adressen, die gespeicherten Raw-Header (`Received`, `Authentication-Results`,
`X-Spam-*`, …), Bodies und Anhänge. Funktioniert für jede archivierte E-Mail. Es ist
dieselbe Rekonstruktion, die der Konto-Export der Web-UI erzeugt, daher ist sie **nicht**
byteidentisch mit dem, was empfangen wurde: DKIM-Signaturen werden dagegen nicht verifiziert werden können.
Verwenden Sie [`/raw`](#download-original-message) dafür.

Gibt `404` zurück, wenn die Nachricht nicht existiert oder für den Aufrufer nicht sichtbar ist, und
`403` (problem+json), wenn `Api:AllowMessageDownloads=false`. Erstellt einen Eintrag im Audit-Protokoll
vom Typ `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Originalnachricht herunterladen {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Gibt die Originalnachricht so zurück, wie sie empfangen wurde (`message/rfc822`), zur Spam-Analyse,
DKIM-Verifizierung oder forensischen Prüfungen. Das Original wird nur gespeichert, wenn
zum Zeitpunkt der Archivierung der E-Mail `RawMessages:Enabled` gesetzt war; siehe
[Original Message Storage Settings](Setup.md#-original-message-storage-settings).
Ob eine E-Mail eine Originalnachricht besitzt, wird durch `hasRawMessage` in
[`EmailDetailDto`](#emaildetaildto) angezeigt.

| Response header | Bedeutung |
| --- | --- |
| `X-Raw-Message-Sha256` | SHA-256 der zurückgegebenen Bytes, aufgezeichnet als die Nachricht gespeichert wurde |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (exakte Kopien) oder `serialized` (vom Parser neu geschrieben, Zeilenenden auf CRLF normalisiert) |
| `X-Raw-Message-Storage` | `postgres` oder `filesystem` — woraus die Bytes gelesen wurden (siehe [Storage backend](RawMessages.md#storage-backend)) |

Die Bytes können in der Datenbank oder in einer Datei liegen, abhängig von
`RawMessages:Storage` zum Zeitpunkt der Archivierung der Nachricht; der Endpunkt
löst dies pro Nachricht auf, sodass beide Arten auf die gleiche Weise bedient werden. Mit
`RawMessages:FileSystem:VerifyChecksumOnRead` (Standard `true`) wird die Datei vor dem Senden
neu gehasht, und eine Nachricht, deren Inhalt nicht mehr mit dem
aufgezeichneten Prüfsummenwert übereinstimmt, wird **nicht** ausgeliefert.

Gibt `404` zurück, wenn die Nachricht nicht existiert oder für den Aufrufer nicht sichtbar ist,
`404` mit dem Fehlertitel `No original message is stored for this email.`,
wenn sie existiert, aber kein Original vorhanden ist, und `500` mit dem Titel
`Stored original message failed its checksum.`, wenn der gespeicherte Inhalt nicht mehr
mit der zum Zeitpunkt der Archivierung aufgezeichneten Prüfsumme übereinstimmt. Gibt `403` (problem+json) zurück, wenn
`Api:AllowMessageDownloads=false`. Erstellt einen Zugriffsprotokolleintrag des Typs `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -o 9876.eml \
  https://host/api/v1/emails/9876/raw
sha256sum 9876.eml          # matches X-Raw-Message-Sha256
spamassassin -t < 9876.eml  # or: rspamc < 9876.eml
```

### Statistiken {#stats}

```
GET /api/v1/stats
```

Gibt aggregierte Archiv-Statistiken zurück, die für den Aufrufer sichtbar sind: insgesamt archivierte
E-Mails, sichtbare Mail-Konten, Anhänge zu diesen E-Mails und die gesamte
PostgreSQL-Datenbankgröße in Megabytes. Für Nicht-Admin-Benutzer sind die Zählungen auf die den ihnen zugewiesenen Mail-Konten beschränkt (dieselbe Einschränkung, die auch für das Web-Dashboard gilt); Administratoren sehen die globalen Gesamtsummen.

Für Nicht-Admin-API-Schlüssel ist `databaseSizeInMB` ein leerer String: Die Datenbankgröße beschreibt die gesamte Instanz, nicht den Umfang des Schlüssels. E-Mails, Konten und Anhänge sind immer auf die Konten beschränkt, auf die der Schlüssel zugreifen kann.

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

| Feld | Typ | Anmerkungen |
| --- | --- | --- |
| `emails` | int | Archivierte Nachrichten, die für den Aufrufer sichtbar sind |
| `accounts` | int | Postfach-Konten, die für den Aufrufer sichtbar sind |
| `attachments` | int | Anhänge dieser sichtbaren Nachrichten |
| `databaseSizeInMB` | string | Gesamte PostgreSQL-Datenbankgröße in MB; leer für Nicht-Admin-Schlüssel |

## Datentypen {#data-types}

### MailAccountDto {#mailaccountdto}

| Feld | Typ | Anmerkungen |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

Es werden niemals Felder für Anmeldedaten serialisiert.

### FolderNodeDto {#foldernodedto}

| Feld | Typ | Anmerkungen |
| --- | --- | --- |
| `name` | string | Blattname |
| `fullPath` | string | Vollständiger Pfad |
| `totalCount` | int | Nachrichten im Ordner |
| `level` | int | Verschachtelungstiefe (0 = Stammverzeichnis) |
| `children` | FolderNodeDto[] | Unterordner |

### EmailSummaryDto {#emailsummarydto}

| Feld | Typ |
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

Alles von [`EmailSummaryDto`](#emailsummarydto) plus:

| Feld | Typ | Anmerkungen |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Aufgelöst über die Body-Fallback-Kette |
| `htmlBody` | string | Aufgelöst über die Body-Fallback-Kette |
| `attachments` | AttachmentDto[] | Nur Metadaten |
| `hasRawMessage` | bool | `true`, wenn die ursprüngliche Nachricht gespeichert ist und [`/raw`](#download-original-message) sie zurückgibt |

### AttachmentDto {#attachmentdto}

| Feld | Typ | Anmerkungen |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bytes |

Anhang-Bytes werden über den [Download-Endpunkt](#download-attachment) bezogen,
nicht über dieses DTO.

## Zugriffsprotokollierung {#access-logging}

Jeder Datenzugriff über die API wird im selben Zugriffsprotokoll erfasst, das auch von der Web-UI verwendet wird (unter **Logs** sichtbar), und dem Besitzer des Schlüssels zugeordnet:

| Endpoint | Protokollierter Typ |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI und Swagger UI {#openapi-and-swagger-ui}

Wenn die API aktiviert ist und `Api:EnableSwaggerUi=true`:

- Das OpenAPI v1 Dokument wird unter **`/apidocs/spec/v1.json`** bereitgestellt.
- Swagger UI wird unter **`/apidocs`** bereitgestellt.

Beide Pfade liegen außerhalb von `/api/` und sind daher durch die normale Web-(Cookie)-Authentifizierung geschützt — eine angemeldete Browser-Sitzung ist erforderlich, um sie anzuzeigen, und sie können nicht mit einem API-Schlüssel aufgerufen werden.

### Endpoint-Referenz auf dieser Seite {#endpoint-reference-on-this-site}

Die Endpunkte und Datentypen werden hier ebenfalls als generierte Seite veröffentlicht:
**[API Reference](API-Reference.md)**. Diese wird aus demselben OpenAPI-Dokument gerendert — eine Kopie wird unter `doc/assets/openapi-v1.json` aufbewahrt — durch `tools/gen_api_reference.py`. Die CI schlägt fehl, wenn die Seite und die Spezifikation nicht mehr synchronisiert sind, sodass die Referenz immer die API beschreibt, die tatsächlich ausgeliefert wird.

!!! note "Regenerierung"
    Die Seite ist ein generierter Output. Ändern Sie die API (oder den Generator), führen Sie dann `python3 tools/gen_api_reference.py` aus und committen Sie das Ergebnis.

    Das Aktualisieren der mitgelieferten Spezifikation selbst bedeutet das erneute Ausführen der zur Build-Zeit beschriebenen Dokumentgenerierung in `tools/README.md`.
