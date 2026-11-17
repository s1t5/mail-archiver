# Csak olvasható REST API (v1) {#read-only-rest-api-v1}

A Mail Archiver egy opcionális, **csak olvasható** REST API-t biztosít a meg archivált levelek programatikus eléréséhez. A megoldás automatizálási és integrációs felhasználási esetekre (jelentések készítése, e-discovery exportok, monitorozás) készült, a webes felület használata nélkül.

A cél az, hogy egy szkript vagy AI ügynök olvashassa a meg archivált leveleket anélkül, hogy **soha ki téne a postafalak hitelesítő adatait, vagy kommunikálnia kellene a levélszerverrel.** A felhasználónként korlátozott API-kulcs lehetőséget biztosít az *archívum olvasására*, de nem egy olyan hitelesítő adat, amellyel a felhasználó *magát a postafalaként* reprezentálhat — ez csak a tulajdonos postafalak-jogosultságait örökli, nem biztosít írási hozzáférést, és azonnal visszavonható anélkül, hogy az alapértelmezett levelszámlát érinteni kellene.

> ⚠️ **Az API alapértelmezés szerint le van tiltva.** A konfigurációban kifejezetten engedélyezni kell (lás: [Enabling the API](#enabling-the-api)). Ha le van tiltva, minden `/api/*` útvonal `404 Not Found`-et ritornul.

Az API **alapvetően csak olvasható**: fiókokat, mappákat, üzenət keresést, üzenet részleteket és csatolások letöltését teszi elérhetővé. Soha nem hoz létre, nem módosít és nem töröl adatokat.

## Tartalomjegyzék {#table-of-contents}

- [Az API engedélyezése](#enabling-the-api)
- [Hitelesítés](#authentication)
  - [API kulcs formátum](#api-key-format)
  - [Kulcs életciklus](#key-lifecycle)
  - [Engedélyezési modell](#permission-model)
  - [Biztonsági indoklás](#security-rationale)
- [Konvenciók](#conventions)
  - [Alap URL és verziózás](#base-url-and-versioning)
  - [Oldalazás](#pagination)
  - [Hibák](#errors)
  - [Sebességkorlátozás](#rate-limiting)
- [Végpontok](#endpoints)
  - [Fiókok listázása](#list-accounts)
  - [Mappák listázása](#list-folders)
  - [Üzenetek keresése](#search-messages)
  - [Üzenet lekérése](#get-message)
  - [Csatolmány letöltése](#download-attachment)
  - [Üzenet letöltése (.eml)](#download-message-eml)
  - [Eredeti üzenet letöltése](#download-original-message)
  - [Statisztikák](#stats)
  - [Egészségállapot](#health)
  - [Métricasok](#metrics)
- [Adattípusok](#data-types)
- [Hozzáférési naplózás](#access-logging)
- [OpenAPI és Swagger UI](#openapi-and-swagger-ui)
- [API referencia](API-Reference.md) — generált végpont- és adattípus referencia

## Az API engrenélése {#enabling-the-api}

Az API a `appsettings.json` `Api` szekciója alatt (vagy az azonos környezeti változók alatt, pl. `Api__Enabled=true`) konfigurálható:

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


### Kulcs élettartama {#key-lifecycle}

1. **Létrehozás** — a felhasználó létrehoz egy kulcsot leíró `Name` segítségével, opcionális lejárattal. A válasz csak egyszer mutatja meg a kulcsot szöveges formátumban.
2. **Használat** — küldje a kulcsot bearer tokenként. Minden elfogadott kérés frissíti a kulcs `LastUsedAt` értékét (a írási műveleteket legfeljebb ~5 percenként egyszer korlátozzuk, hogy ne történjen írás minden hívásnál).
3. **Lejáratság** — ha a `ExpiresAt` be van állítva és a múltban van, a kulcs elutasításra kerül.
4. **Visszavonás** — a tulajdonos felhasználó vagy egy adminisztrátor visszavonja a kulcsot (be van állítva a `RevokedAt`); a kulcs azonnal és véglegesen elutasításra kerül.

A kulcs akkor **aktív**, ha nem volt visszavonva, nem járt le, **és** a tulajdonos felhasználó továbbra is aktív. Ha a kulcs bármely okból inaktív, a `401` hiba kódjával utasítják el.

### Engedélyezési modell {#permission-model}

Egy API kulcs pontosan ugyanazokat a jogosultságokat örökli, amelyek a tulajdonos felhasználót illetik. Az autentikált entitás ugyanazokat a claimeket hordozza, amelyeket egy normál webes bejelentkezés is, így minden meglévő auctoriz...

| Beállítás | Alapértelmezett | Leírás |
| --- | --- | --- |
| `Enabled` | `false` | Fő kapcsoló. Ha `false`, minden `/api/*` útvonal `404`-at ad vissza, az OpenAPI dokumentum és a Swagger UI pedig nem érhető el. |
| `AllowAttachmentDownloads` | `true` | Ha `false`, a csatolmány letöltési végpont `403`-et ad vissza. A csatolmányokról szóló metaadatokat továbbra is visszaadja az üzenet részleteit lekérdező végpont. |
| `AllowMessageDownloads` | `true` | Ha `false`, a teljes üzenet végpontok (`/eml` és `/raw`) `403`-at adnak vissza. Egy teljes üzenet tartalmazza a csatolmányait is, ezért **mind** ez a kapcsoló, **mind a** `AllowAttachmentDownloads` értékének `true` kell lennie; bármelyik kikapcsolása mindkét végpontot elérhetővé teszi. |
| `EnableSwaggerUi` | `true` | Ha `true` (és a `Enabled` `true`), a Swagger UI a `/apidocs` helyen, az OpenAPI dokumentum pedig a `/apidocs/spec/v1.json` helyen lesz elérhető. Mindkettőhez autentikált **webes (cookie) munkamenetre** van szükség — nem részei a `/api/`-nak, és nem érhetőek el API kulccsal. |
| `DefaultPageSize` | `20` | Oldalméret, amelyet akkor alkalmaznak, ha a kérés elhagyja a `pageSize` paramétert. |
| `MaxPageSize` | `100` | Felső korlát; a nagyobb `pageSize` értékeket erre korlátozzák. |
| `RateLimitPerMinute` | `120` | Fix ablakos kéréskeret API kulcsonként, percenként (lásd: [Rate limiting](#rate-limiting)). |

## Hitelesítés {#authentication}

Az API **felhasználónkénti API kulcsokkal** hitelesít, amelyeket bearer tokenként küldenek:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` **kizárólag** API kulcsokat fogad el. A böngésző cookie-it nem veszi figyelembe, így az API nem rendelkezik CSRF felülettel. Az hitelesítési hiba esetén a rendszer `401`-at ritornál `WWW-Authenticate: Bearer` headerrel és [problem+json](#errors) törzsadatral — az API **soha** nem redirectál a bejelentkezési oldalra.

A kulcsok önkiszolgáló módon kezelhetők a web felületen az **API Keys** menüpont alatt (minden felhasználó a saját kulcsait kezeli; a rendszergazdák bármely felhasználó kulcsait megtekinthetik és visszavonhatják).

### API kulcs formátum {#api-key-format}

A kulcs egy `ma_` karakterlánc, amelyet 43 URL-biztos base64 karakter követ, amelyek 32 véletlen byte-ot kódolnak (256 bit entropia):

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Tárolás:

- **`KeyPrefix`** — az első ~11 karakter (pl. `ma_8Kf3pQ2`), szöveges formátumban tárolva. A UI-ben és a naplókban a kulcs azonosítására szolgál. Nem titkos adat.
- **`KeyHash`** — a teljes kulcs hex kódolt SHA-256 értékét tárolja egy **egyedi index** alatt. A kulcsot szöveges formátumban soha nem tároljuk.

A teljes kulcsot a felhasználónak **mindössze egyszer** mutatjuk meg, a létrehozás pillanatában. Utasítással nem kérhető vissza; elveszett kulcs esetén az új kulcsot kell létrehozni és a régit visszavonni.

### Kulcs élettartama {#key-lifecycle}

1. **Létrehozás (Create)** — a felhasználó létrehoz egy kulcsot leíró `Name` értékkel és opcionális lejárati idővel. A válasz egyszer mutatja meg a kulcsot szöveges formátumban.
2. **Használat (Use)** — küldje a kulcsot bearer tokenként. Minden elfogadott kérés frissíti a kulcs `LastUsedAt` értékét (a írási műveleteket legfeljebb ~5 percenként egyszer korlátozzuk, hogy ne kelljen minden hívásnál írniuk).
3. **Lejárat (Expire)** — ha az `ExpiresAt` be van állítva és a múltban van, a kulcs elutasításra kerül.
4. **Visszavonás (Revoke)** — a tulajdonos felhasználó vagy egy rendszergazda visszavonja a kulcsot (az `RevokedAt` be van állítva); a kulcs azonnal és véglegesen elutasításra kerül.

A kulcs akkor **aktív**, ha nem volt visszavonva, nem járt le, **és** a tulajdonos felhasználó még aktív. Ha bármely okból a kulcs nem aktív, `401`-al veszik el.

### Jogosultsági modell {#permission-model}

Egy API kulcs pontosan ugyanazokat a jogosultságokat örökli, amelyek a kulcs tulajdonosa rendelkezésére állnak. A hitelesített entitás ugyanazokat a claimeket hordozza, mint egy normál webes bejelentkezés, így az összes meglévő engedélyezési logika változatlanul alkalmazható:

- **Rendszergazdák** (`IsAdmin`) minden posta fiókot és minden üzenetet látnak.
- **Rendszeres felhasználók** csak nekik hozzárendelt posta fiókat látják (`UserMailAccount`). A hozzájuk nem tartozó fiókok és üzenetek úgy kezelésre kerülnek, mint amik **nincsenek**: a kérések ezekre `404`-ot ritornálnak, nem pedig `403`-et, így az API nem szivárogtatja a felhasználók számára nem elérhető fiókok meglétét.

### Biztonsági indoklás {#security-rationale}

- **2FA megkerülés.** Az API kulcsok tervezés szerinti működésük miatt megkerülik a kétfaktoros hitelesítést — ez a nem interaktív tokenek tulajdonsága (ugyanaz igaz a GitHub/GitLab személyes hozzáférési tokenjeire is). Ezt a következő módon enyhítjük: a kulcsok **alapértelmezés szerint ki vannak kapcsolva**, visszavonhatók, opcionálisan lejárhatók, a felhasználó jogosultságaihoz kötöttek, és minden hozzáférést rögzítünk az [access log](#access-logging) fájlban.
- **Nincsenek cookie-k a `/api` útvonalakon.** A cookie hitelesítés nem elfogadott a `/api` útvonalakon, így az API-t CSRF kockázatoktól mentesítjük.
- **Transzport.** A termékverzióban az API-t mindig HTTPS protokollon keresztül biztosítsuk, hogy a bearer tokenek ne kerüljenek lehallgatásra a hálózatban.

## Konvenciók {#conventions}

### Alap URL és verziózás {#base-url-and-versioning}

Minden végpont a `/api/v1` alatt található. A verzió a elérési útvonalban szerepel, így a jövőbeli, tördelő változások `/api/v2` formában is megjelenhetnek anélkül, hogy zavarnák a meglévő klienseket.

A válaszok `application/json`, hacsak nincs mástól jelezve (a csatolmány letöltésekor a csatolmány saját content type-ja érkezik vissza). Minden időbélyeg UTC, ISO 8601 (`2026-06-11T09:30:00Z`) formátumú.

### Oldalazás {#pagination}

A listás végpontok egy oldalasítást tartalmazó borítékot (pagination envelope) adnak vissza:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- A `page` 1-al indul. A határéttől távol lévő oldalak üres `items` tömböt vrátanak (nem hiba esetén).
- Az `pageSize` alapértelmezett értéke `Api:DefaultPageSize`, és a `[1, Api:MaxPageSize]` értékhez van igazítva.

### Hibák {#errors}

A hibák az [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) `application/problem+json` szabványt használják:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Unknown sortBy value 'foo'.",
  "instance": "/api/v1/emails"
}
```

| Status | Mikor |
| --- | --- |
| `400 Bad Request` | Rosszul formázott paraméterek (pl. nem értelmezhető dátum, érvénytelen `sortBy`). |
| `401 Unauthorized` | Hiányzó, rosszul formázott, lejárt, visszavonott vagy egyébként érvénytelen kulcs. Tartalmazza a `WWW-Authenticate: Bearer` értéket is. |
| `403 Forbidden` | Csatolmány letöltése kért, miközben `AllowAttachmentDownloads=false`, vagy üzenet letöltése kért, miközben `AllowMessageDownloads=false`. |
| `404 Not Found` | A forrás nem létezik, **vagy** kívül esik a hívó engedélyezett fiókjai között. Minden `/api/*` útvonal esetén is visszatér, ha `Api:Enabled=false`. |
| `429 Too Many Requests` | Túllépett sebességkorlát (Rate limit exceeded). |
| `500 Internal Server Error` | Váratlan szerverhiba (problem+json, soha nem HTML hibaoldal). |

### Sebességkorlátozás (Rate limiting) {#rate-limiting}

A kéréseket egy fix ablakos (fixed-window) szabály határozza meg, amely percenként `Api:RateLimitPerMinute` kérést tesz lehetővé (alapértelmezett `120`), az API kulcs előtagja szerint csoportosítva (ha nincs kulcs, akkor a kliens IP-címe alapján). A keret túllépése esetén `429` érkezik vissza.

## Endpointok {#endpoints}

> Minden példa feltételezi, hogy az API engedélyezve van, és a `$KEY` érvényes kulcsot tartalmaz.

### List accounts {#list-accounts}

```
GET /api/v1/accounts
```

Visszaadja a hívó számára látható postafiókokat. A hitelesítő adatokat tartalmazó mezők
(IMAP/OAuth username, password, client secret, …) **soha** nem kerülnek bele.

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

### List folders {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

Egyetlen fiók mappatréndszerét adja vissza egymásba ágyazott struktúrában. `404`-t ad vissza, ha a fiók nem létezik, vagy nem látható a hívó számára.

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

### Üzenetek keresése {#search-messages}

```
GET /api/v1/emails
```

Archivált üzenetek keresése ugyanazokkal a funkciókkal, mint a webes felület.

| Kérdés (Query) paraméter | Típus | Leírás |
| --- | --- | --- |
| `q` | string | Teljes szöveges keresés a tárgy, a levél tartalma, a küldő, a címzett, a másolat (cc) és a titkos másolat (bcc) mezőkben. Támogatja a mező szintaxisát, pl. `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Legkorábbi küldési dátum (beleértve), `YYYY-MM-DD`. |
| `to` | date | Legutóbbi küldési dátum (beleértve), `YYYY-MM-DD`. |
| `accountId` | int | Korlátozás egyetlen fiókra (a hívónak látatnasına kell lennie). |
| `folder` | string | Korlátozás egyetlen mappára a teljes elérési út alapján. |
| `direction` | string | `incoming` vagy `outgoing`. |
| `page` | int | 1-al kezdődő oldalszám (alapértelmezett: `1`). |
| `pageSize` | int | Eredmények oldalonként (alapértelmezett: `Api:DefaultPageSize`, maximum `Api:MaxPageSize`). |
| `sortBy` | string | Az alábbiak közül: `sentDate` (alapértelmezett), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` vagy `desc` (alapértelmezett: `desc`). |

[Lapozott borítékot](#pagination) ad vissza, amely tartalmaz [`EmailSummaryDto`](#emailsummarydto)-t.
Egy `Search` típusú [hozzáférési napló](#access-logging) bejegyzést rögzít.

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

### Üzenet lekérése {#get-message}

```
GET /api/v1/emails/{id}
```

A teljes üzenetet visszatéríti a testekkel együtt. A test ugyanazt a visszalépési láncot használja, mint a webes felület: `OriginalBodyHtml` (nyers bájtok) → `BodyUntruncatedHtml` →
`HtmlBody`, és az erre vonatkozó megfelelőket a szöveges testhez. `404`-et ad vissza, ha az üzenet
nem létezik, vagy olyan fiókhoz tartozik, amely kívül esik a hívó jogosultságain. `Open` típusú hozzáférési naplózási bejegyzést generál.

Egy [`EmailDetailDto`](#emaildetaildto)-t ritorál.

### Melléklet letöltése {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

A melléklet bytesezeit a tárolt content type és filename adatokkal együtt streameli. `404`-t ritorna, ha az üzenet vagy a melléklet nem létezik, vagy nem látható a hívó számára. `403` (problem+json) értéket ritornal, ha `Api:AllowAttachmentDownloads=false`.
Egy `Download` típusú hozzáférési naplót (access log) rögzít.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Üzenet letöltése (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Az üzenetet `message/rfc822` formátumban adja vissza, amelyet a mentett mezőkből építت fel:
címzettek, a tárolt nyers fejlécadatok (`Received`, `Authentication-Results`,
`X-Spam-*`, …), törzstextek és csatolmányok. Minden archivált e-mailnél működik.
Ez ugyanaz az rekonstrukció, amelyet a webes felület fiókexportálása végez, így nem
**azonos** byte-szinten az eredetivel: a DKIM aláírások nem fogják hitelesíteni.
Ehhez használja a [`/raw`](#download-original-message) funkciót.

`404`-et ad vissza, ha az üzenet nem létezik vagy nem látható a hívó számára, és
`403` (problem+json) értéket, ha `Api:AllowMessageDownloads=false`. A hozzáférésről `Download` típusú
naplózási bejegyzést készít.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Eredeti üzenet letöltése {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Az eredeti üzenetet adja vissza úgy, ahogy azt megkapta (`message/rfc822`), spamszűréshez,
DKIM-ellenőrzéshez vagy informatikai vizsgálatokhoz. Az eredeti üzenet csak akkor tárolódik, ha
az e-mail archiválásakor az `RawMessages:Enabled` be volt állítva; lásd:
[Original Message Storage Settings](Setup.md#-original-message-storage-settings).
Azt, hogy egy e-mail rendelkezik-e ilyen üzenettel, az `hasRawMessage` mutatja a
[`EmailDetailDto`](#emaildetaildto) felületen.

| Response header | Jelentése |
| --- | --- |
| `X-Raw-Message-Sha256` | Az visszaadott bájtok SHA-256 értékének ellenőrzősumája, amelyet az üzenet tárolásakor rögzítettek |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (pontos másolatok) vagy `serialized` (a parser által újranyírva, a sorvégek CRLF formátumúak) |
| `X-Raw-Message-Storage` | `postgres` vagy `filesystem` — honnan olvasták ki a bájtokat (lásd: [Storage backend](RawMessages.md#storage-backend)) |

A bájtok az adatbázisban vagy egy fájlban tárolódhatnak, attól függően, hogy az üzenet archiválásakor milyen `RawMessages:Storage` volt érvényben; az endpoint üzenetenként megoldja ezt, így mindkét típus ugyanúgy érhető el. A `RawMessages:FileSystem:VerifyChecksumOnRead` (alapértelmezett `true`) beállítás mellett a fájlt újra-hasholják a küldés előtt, és az olyan üzenet, amelynek tartalma már nem egyezik a
rögzített ellenőrzősumával, **nem** kerül kiadatra.

`404`-at ad vissza, ha az üzenet nem létezik vagy nem látható a hívó számára,
`404`-et az `No original message is stored for this email.` hibatíttal, ha az üzenet létezik, de nincs eredeti változat,
és `500`-et az `Stored original message failed its checksum.` címmel, ha a tárolt tartalom már nem egyezik az
archiváláskor rögzített ellenőrzősumával. `403` (problem+json) értéket ad vissza, ha
`Api:AllowMessageDownloads=false`. Rögzít egy `Download` típusú hozzáférési naplót.

```bash
curl -H "Authorization: Bearer $KEY" -o 9876.eml \
  https://host/api/v1/emails/9876/raw
sha256sum 9876.eml          # matches X-Raw-Message-Sha256
spamassassin -t < 9876.eml  # or: rspamc < 9876.eml
```

### Stats {#stats}

```
GET /api/v1/stats
```

Visszaadja az hívó számára látható összesített archívum statisztikákat: összes archivált e-mailt, látható postafiókokat, az e-mailekhez tartozó csatolmányokat, valamint a PostgreSQL adatbázis teljes méretét megabájtban. Nem adminisztrátor felhasználók esetén a számok csak az őt megadott postafiókokra korlátozódnak (ugyanúgy, ahogy a webes műszerfal is); az adminisztrátorok a globális összesítéseket látják.

Nem adminisztrátor API kulcsok esetén a `databaseSizeInMB` egy üres 문자열: az adatbázis mérete az egész példányt írja le, nem pedig a kulcs körét. Az Emails, Accounts és Attachments értékek mindig a kulcs által elérhető fiókokra korlátozódnak.

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

| Mező | Típus | Megjegyzések |
| --- | --- | --- |
| `emails` | int | Az hívó számára látható archivált üzenetek |
| `accounts` | int | Az hívó számára látható postafalak |
| `attachments` | int | A látható üzenetek csatolmányai |
| `databaseSizeInMB` | string | A teljes PostgreSQL adatbázis mérete MB-ban; üres nem-admin kulcsok esetén |

### Health {#health}

```
GET /api/v1/health/live
GET /api/v1/health/ready
GET /api/v1/health
```

Mindháromhoz admin API kulcs szükséges. `health/live` 200-as választ ad bármilyen függőség megérintése nélkül. `health/ready` ellenőrzi a PostgreSQL-t és az archívéma-szerkezetet, és 503-as választ ad, ha bármelyik elérhetetlen. `health` futtatja továbbá a szinkronizationi frissességellenőrzést is, és `Degraded` (HTTP 200) választ ad, ha egy engedélyezett fiók nem szinkronizált `Health:SyncStaleMinutes` alatt.

```json
{"status":"Healthy","checks":{"database":"Healthy","sync":"Healthy"}}
```

A teljes leírás, beleértve a 401/403/404 viselkedést és a Docker health checket, a **[Health and Metrics](Monitoring.md)** dokumentációban található.

### Metrics {#metrics}

```
GET /api/v1/metrics
```

Prometheus szöveges kiállítási formátumot adор. Admin API kulcsot és `Metrics:Enabled=true`-et igényel; különben az endpoint 404-et adор. A metrikanevészetek, címkék és riasztási példák a **[Health and Metrics](Monitoring.md)** dokumentációban találhatók.

## Adattípusok {#data-types}

### MailAccountDto {#mailaccountdto}

| Mező | Típus | Megjegyzés |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

A hitelesítő mezők soha nem kerülnek szerializálásra.

### FolderNodeDto {#foldernodedto}

| Mező | Típus | Megjegyzés |
| --- | --- | --- |
| `name` | string | Levél neve |
| `fullPath` | string | Teljes elérési út |
| `totalCount` | int | Üzenetek a mappában |
| `level` | int | Beágyazottsági mélység (0 = gyökér) |
| `children` | FolderNodeDto[] | Almapák |

### EmailSummaryDto {#emailsummarydto}

| Mező | Típus |
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

Minden olyan, ami a [`EmailSummaryDto`](#emailsummarydto)-ben van, plus:

| Mező | Típus | Megjegyzés |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | A body fallback láncon keresztül kerül feloldásra |
| `htmlBody` | string | A body fallback láncon keresztül kerül feloldásra |
| `attachments` | AttachmentDto[] | Csak metaadatok |
| `hasRawMessage` | bool | `true`, amikor az eredeti üzenet tárolásra kerül, és a [`/raw`](#download-original-message) visszaadja azt |

### AttachmentDto {#attachmentdto}

| Mező | Típus | Megjegyzés |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bytek |

A csatolmány bytek a [download endpoint](#download-attachment) segítségével érhetők el,
nem pedig ebből a DTO-ból.

## Hozzáféréli naplózás {#access-logging}

Minden API-n keresztül történő adat-hozzáférés ugyanabban az hozzáférési naplóban kerül rögzítésre, amelyet a web UI is használ (a **Logs** menüpont alatt látható), a kulcs tulajdonos felhasználóhozապróblálva:

| Endpoint | Naplózott típus |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI és Swagger UI {#openapi-and-swagger-ui}

Amikor az API engedélyezve van és `Api:EnableSwaggerUi=true`:

- Az OpenAPI v1 dokumentum a **`/apidocs/spec/v1.json`** címen érhető el.
- A Swagger UI a **`/apidocs`** címen érhető el.

Mindkét elérési út `/api/` kívül esik, ezért a normál web (cookie) hitelesítés korlátozza őket — a megtekintésükhöz bejelentkezett böngésző-sesszióra van szükség, és nem érhetők el API kulccal.

### Endpoint referencia ezen az oldalon {#endpoint-reference-on-this-site}

A végpontok és az adattípusok itt is közzétételre kerülnek, mint egy generált oldal:
**[API Reference](API-Reference.md)**. Ezt ugyanabból az OpenAPI dokumentumból — amelynek másolata `doc/assets/openapi-v1.json` helyen található — generálja a `tools/gen_api_reference.py`. A CI hibát jelez, ha a oldal és a specifikáció ellentétes állapotba kerül, így a referencia mindig azt az API-t írja le, amely ténylegesen kerül kiállításra.

!!! note "Regenerálás"
    Az oldal generált kimenet. Ha megváltoztatja az API-t (vagy a generátort), akkor futtassa le a `python3 tools/gen_api_reference.py` parancsot, és commitelje az eredményt.

    A mellékelt specifikáció frissítése magában foglalja a `tools/README.md` részben leírt építési idejű dokumentumgenerálás újraindítását.
