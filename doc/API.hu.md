# Csak olvasható REST API (v1) {#read-only-rest-api-v1}

A Mail Archiver egy opcionális, **csak olvasható** REST API-t biztosít a programatikus hozzáféréshez a archivált levelekhez. Az automatizálási és integrációs felhasználási esetekre (jelentéskészítés, e-discovery exportálás, monitorozás) tervezték, a webes felületen való navigálás nélkül.

A cél az, hogy egy szkript vagy AI ügynök olvashasson az archivált levelekről anélkül, hogy **kihívná a postafiók hitelesítő adatait vagy kommunikálnia kellene a levél szolgáltatóval.** Egy felhasználónként meghatározott, korlátozott jogosultságú API-kulcs olyan képességet tesz lehetővé, hogy *olvasson az archivumból*, de nem egy olyan hitelesítő adat, amely lehetővé teszi, hogy *ön maga legyen a postafiók* — ez csak a tulajdonos postafiók jogosultságait örökli, nem biztosít írási hozzáférést, és azonnal visszavonható anélkül, hogy az alapértelmezett levélfiókot érintetné.

> ⚠️ **Az API alapértelmezés szerint le van engedélyezve.** Explicit módon engedélyezni kell a konfigurációban (lásd: [Enabling the API](#enabling-the-api)). Ha le van engedélyelve, minden `/api/*` útvonalat `404 Not Found` eredményt ad vissza.

Az API **csak olvasható kialakítású**: hozzáférést biztosít fiókokhoz, mappákhoz, üzenőkereséshez, üzenet részleteihez és csatolmány letöltéséhez. Soha nem hoz létre, nem módosít és nem töröl adatokat.

## Tartalomjegyzék {#table-of-contents}

- [Az API engedélyezése](#enabling-the-api)
- [Hitelesítés](#authentication)
  - [API kulcs formátum](#api-key-format)
  - [Kulcs élettartama](#key-lifecycle)
  - [Jogosultsági modell](#permission-model)
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
  - [Melléklet letöltése](#download-attachment)
  - [Üzenet letöltése (.eml)](#download-message-eml)
  - [Eredeti üzenet letöltése](#download-original-message)
  - [Statisztikák](#stats)
- [Adattípusok](#data-types)
- [Hozzáférési naplózás](#access-logging)
- [OpenAPI és Swagger UI](#openapi-and-swagger-ui)
- [API Referencia](API-Reference.md) — generált végpont és adattípus referencia

## Az API engedélyezése {#enabling-the-api}

Az API a `appsettings.json` `Api` szekciója (vagy az azonos környezeti változók, pl. `Api__Enabled=true`) alatt van konfigurálva:

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

1. **Létrehozás** — a felhasználó létrehoz egy kulcsot leíró `Name` értékkel és opcionális
   lejáró háttérrel. A válasz egyszer mutatja meg a kulcsot szöveges formátumban.
2. **Használat** — küldje a kulcsot bearer tokenként. Minden elfogadott kérés frissíti a
   kulcs `LastUsedAt` értékét (a írási műveleteket legfeljebb ~5 percenként egyszeri sebességet begróznak, hogy elkerüljék a folyamatos írást).
3. **Lejárat** — ha a `ExpiresAt` be van állítva és a múltban van, a kulcs elutasításra kerül.
4. **Visszavonás** — a kulcs tulajdonosa vagy egy adminisztrátor visszavonja a kulcsot
   (a `RevokedAt` be van állítva); a kulcs azonnal és véglegesen elutasításra kerül.

A kulcs akkor **aktív**, ha nem vonult vissza, nem járt le, **és** a tulajdonos felhasználó
jeszt is aktív. Ha a kulcs bármely fent említett okból inaktív, a `401` hibával kapja elutasítást.

### Jogosultsági modell {#permission-model}

Egy API-kulcs pontosan ugyanazokat a jogosultságokat örökli, amelyek a tulajdonos felhasználónak vannak. A
hitelesített alany ugyanazokat a claimeket hordozza, mint egy normál webes bejelentkezés, így minden meglévő autoriz

| Beállítás | Alapértelmezett | Leírás |
| --- | --- | --- |
| `Enabled` | `false` | Fő kapcsoló. Ha `false`, minden `/api/*` útvonal `404`-at ad terug, az OpenAPI dokumentum és a Swagger UI pedig nincs feltérképezve. |
| `AllowAttachmentDownloads` | `true` | Ha `false`, a csatolt fájlok letöltése végpontja `403`-et ad terug. A csatolt fájlokról szóló metaadatokat továbbra is visszaküldi az üzenet részleteit ismertető végpont. |
| `AllowMessageDownloads` | `true` | Ha `false`, a teljes üzenet végpontok (`/eml` és `/raw`) `403`-at adnak terug. Egy teljes üzeset tartalmazza a csatolt fájljait, ezért **mind** ez a kapcsoló, **mind** a `AllowAttachmentDownloads` értékének `true` kell lennie; bármelyik kikapcsolása mindkét végpontot letiltja. |
| `EnableSwaggerUi` | `true` | Ha `true` (és a `Enabled` `true`), a Swagger UI a `/apidocs` helyen, az OpenAPI dokumentum pedig a `/apidocs/spec/v1.json` helyen érhető el. Mindkettő hitelesített **web (cookie) munkamenetet** igényel — nem része a `/api/`-nak, és nem érhető el API kulccsal. |
| `DefaultPageSize` | `20` | Az oldal mérete, amelyet akkor használ, ha a kérés kihagyja a `pageSize` paramétert. |
| `MaxPageSize` | `100` | Felső határ; a nagyobb `pageSize` értékeknek ez az üst limit. |
| `RateLimitPerMinute` | `120` | Fix ablakos kérés-keret az API kulcsonként percenként (lásd: [Rate limiting](#rate-limiting)). |

## Hitelesítés {#authentication}

Az API a **felhasználónkénti API-kulcsokkal** hitelesít, amelyeket bearer tokenként küldenek:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` **csak** API-kulcsokat fogad el. A böngésző cookie-it nem veszi figyelembe, így az API nem rendelkezik CSRF felülettel. A hitelesítés sikertelen esete esetén a rendszer `401`-at ad vissza `WWW-Authenticate: Bearer` headerrel és [problem+json](#errors) törzsadatт — az API **soha** nem redirectál a bejelentkezési oldalra.

A kulcsok az önkiszolgáló felületen, a web UI alatt, a **API Keys** menüpont alatt kezelhetők (minden felhasználó a saját kulcsait kezeli; adminisztrátorok bármely felhasználó kulcsait megtekinthetik és visszavonhatják).

### API-kulcs formátum {#api-key-format}

A kulcs a `ma_` string, amelyet 43 URL-biztos base64 karakter követ, ami 32 véletlenszerű byte (256 bit entropia) kódolását tartalmaz:

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Tárolás:

- **`KeyPrefix`** — az első ~11 karakter (pl. `ma_8Kf3pQ2`), amely plaintext formátumban tárolódik. A UI-ben és a logokban a kulcs azonosítására szolgál. Nem titkos adat.
- **`KeyHash`** — a teljes kulcs hex-kódolt SHA-256 értéke, amely **egyedi index** alatt tárolódik. A plaintext kulcsot soha nem tároljuk.

A teljes kulcsot a felhasználónak **pontosan egyszer** mutatjuk meg a létrehozáskor. Utánuk már nem tekinthető vissza; elveszett kulcs visszavonása és új kulcs létrehozása szükséges.

### Kulcs életköre {#key-lifecycle}

1. **Létrehozás (Create)** — a felhasználó létrehoz egy kulcsot leíró `Name` és opcionális lejárati idővel. A válasz egyszer mutatja meg a plaintext kulcsot.
2. **Használat (Use)** — küldje a kulcsot bearer tokenként. Minden elfogadott kérés frissíti a kulcs `LastUsedAt` értékét (a írási műveleteket legfeljebb ~5 percenként egyszer korlátozzuk, hogy ne történjen írás minden egyes hívásnál).
3. **Lejárvány (Expire)** — ha a `ExpiresAt` be van állítva és a múltban van, a kulcs elutasításra kerül.
4. **Visszavonás (Revoke)** — a kulcs tulajdonos felhasználó vagy egy adminisztrátor visszavonja a kulcsot (a `RevokedAt` be van állítva); a kulcs azonnal és véglegesen elutasításra kerül.

A kulcs **aktív**, amennyiben nem vonható vissza, nem lejárt, **és** a tulajdonos felhasználó még aktív. Bármely okból inaktív kulcs `401`-al kerül elutasításra.

### Jogosultsági modell {#permission-model}

Egy API-kulcs pontosan ugyanazokat a jogosultságokat örökli, amelyek a tulajdonos felhasználóhoz tartoznak. A hitelesített entitás ugyanazokat a claim-eket hordozza, mint egy normál webes bejelentkezés, így az összes meglévő autorizációs logika változatlanul alkalmazható:

- **Adminisztrátorok** (`IsAdmin`) minden levelezési fiókot és minden üzenetet látnak.
- **Általános felhasználók** csak nekik hozzárendelt levelezési fiókat látnak (`UserMailAccount`). A meghatározott készleten kívüli fiókok és üzenetek olyan, mintha **nem léteznének**: a hozzájuk való kérések nem `403`-et, hanem `404`-ot adnak vissza, így az API nem szivárogja ki a hozzászólók számára láthatatlan fiókok meglétét.

### Biztonsági indoklás {#security-rationale}

- **2FA megkerülés.** Az API-kulcsok tervezés szerint megkerülik a kétfaktoros hitelesítést — ez a nem interaktív tokenek tulajdonosára jellem (ugyan ez igaz a GitHub/GitLab személyes hozzáférési tokenére is). Ezt a következőkel enyhítjük: a kulcsok **alapértelmezés szerint kikapcsoltak**, visszavonhatóak, opcionálisan lejárhatók, a felhasználói jogosultságokhoz kötöttek, és minden hozzáférést rögzít az [access log](#access-logging)-ban.
- **Nincs cookie `/api` esetén.** A cookie hitelesítés nem elfogadott a `/api` útvonalakon, így az API CSRF problémája elenyésző.
- **Transport.** A termelésben mindig HTTPS-en keresztül tegye elérhetővé az API-t, hogy a bearer tokenek ne kerüljenek kiszolgáltatottságba a hálózatban.

## Konvenciók {#conventions}

### Alap URL és verziózás {#base-url-and-versioning}

Minden végpont a `/api/v1` alatt található. A verzió a útvonalban szerepel, így a jövőbeli törő változások `/api/v2` formájában is megjelenhetnek a meglévő kliensek zavartalan működtetése mellett.

A válaszok `application/json`, hacsak nincs másképp megjelölve (csatolmány letöltése esetén a csatolmány saját content type-ja tér vissza). Minden időbélyeg UTC, ISO 8601 (`2026-06-11T09:30:00Z`) formátumú.

### Oldalazás (Pagination) {#pagination}

A listás végpontok egy oldazási borítékot (pagination envelope) adnak vissza:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- A `page` 1-al indul. A tartományon kívüli oldalak egy üres `items` tömböt adnak vissza (nem hiba).
- Az `pageSize` alapértelmezett értéke `Api:DefaultPageSize`, és a `[1, Api:MaxPageSize]` határain belül marad.

### Hibák {#errors}

A hibák az [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) szabványt használják:
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

| Status | Mikor |
| --- | --- |
| `400 Bad Request` | Hibás paraméterek (pl. nem értelmezhető dátum, érvénytelen `sortBy`). |
| `401 Unauthorized` | Hiányzó, hibás, lejárt, visszavonott vagy egyébként érvénytelen kulcs. Tartalmazza a `WWW-Authenticate: Bearer` értéket is. |
| `403 Forbidden` | Csatolmány letöltése kért `AllowAttachmentDownloads=false` állapot esetén, vagy üzenet letöltése `AllowMessageDownloads=false` állapot esetén. |
| `404 Not Found` | A erőforrás nem létezik, **vagy** kívül esik a hívó engedélyezett számláin. Szintén visszatér minden `/api/*` útvonal esetén, ha `Api:Enabled=false`. |
| `429 Too Many Requests` | Túllépte a sebességkorlást (Rate limit exceeded). |
| `500 Internal Server Error` | Várandó szerverhiba (problem+json, soha nem HTML hibaoldal). |

### Sebességkorlátozás (Rate limiting) {#rate-limiting}

A kéréseket egy fix ablakos (fixed-window) politika korlátozza, amely `Api:RateLimitPerMinute` kérés percenként (alapértelmezett `120`), az API kulcs előtagja szerint csoportosítva (ha nincs kulcs, akkor a kliens IP-címe alapján). A keret túllépése esetén `429` érkezik.

## Endpontok {#endpoints}

> Minden példa feltételezi, hogy az API engedélyezve van, és a `$KEY` érvényes kulcsot tartalmaz.

### List accounts {#list-accounts}

```
GET /api/v1/accounts
```

Visszaadja a hívó számára látható posta-fiókokat. A hitelesítő adatokat tartalmazó mezők
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

| Lekérdezési paraméter | Típus | Leírás |
| --- | --- | --- |
| `q` | string | Teljes szöveges lekérdezés a tárgy, a levél tartalma, a küldő, a címzett, a másolat (cc) és a titkos másolat (bcc) mezőkben. Támogatja a mező szintaktikát, pl. `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Legkorábbi küldési dátum (beleértve), `YYYY-MM-DD`. |
| `to` | date | Legutóbbi küldési dátum (beleértve), `YYYY-MM-DD`. |
| `accountId` | int | Korlátozás egyetlen fiókra (a hívónak látható아야). |
| `folder` | string | Korlátozás egyetlen mappára a teljes elérési út alapján. |
| `direction` | string | `incoming` vagy `outgoing`. |
| `page` | int | 1-al kezdődő oldalszám (alapértelmezett `1`). |
| `pageSize` | int | Eredmények oldalonként (alapértelmezett `Api:DefaultPageSize`, maximum `Api:MaxPageSize`). |
| `sortBy` | string | Az alábbiak egyike: `sentDate` (alapértelmezett), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` vagy `desc` (alapértelmezett `desc`). |

[`EmailSummaryDto`](#emailsummarydto) [oldaltörlős csomagot](#pagination) ad vissza.
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

### Get message {#get-message}

```
GET /api/v1/emails/{id}
```

Visszaadja a teljes üzenetet a törzstextekkel együtt. A törzstext ugyanazt a visszalépési láncot használja, mint a webes felület: `OriginalBodyHtml` (nyers bytes) → `BodyUntruncatedHtml` →
`HtmlBody`, és az erre vonatkozó megfelelőket a szöveges törzstexthez. `404`-et ad vissza, ha az üzenet nem létezik, vagy olyan fiókhoz tartozik, amely kívül esik a hívó jogosultságain. `Open` típusú hozzáférési naplózási bejegyzést generál.

Egy [`EmailDetailDto`](#emaildetaildto)-t ad vissza.

### Melléklet letöltése {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

A melléklet bytes-ét a tárolt content type és filename adatokkal együtt streameli. `404`-t ad vissza, ha az üzenet vagy a melléklet nem létezik, vagy nem látható a hívó számára. `403` (problem+json) értéket ad vissza, ha `Api:AllowAttachmentDownloads=false`.
A `Download` típusú hozzáférési naplót (access log) rögzíti.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Üzenet letöltése (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Az üzenetet `message/rfc822` formátumban adja vissza, az archivált mezőkből:
címzettek, a tárolt nyers fejléc-adatok (`Received`, `Authentication-Results`,
`X-Spam-*`, …), testrészek és csatolmányok újraépítve. Minden archivált e-mailnél működik.
Ez ugyanaz az rekonstrukció, amelyet a web UI fiókexportálása végez, így **nem**
azonos a megérkezett üzenet byte-szintű másolatával: a DKIM aláírások nem fogják
ellenőrizni vele. Ehhez használja a [`/raw`](#download-original-message) funkciót.

`404`-et ad vissza, ha az üzenet nem létezik vagy nem látható a hívó számára, és
`403` (problem+json) értéket, ha `Api:AllowMessageDownloads=false`. Rögzíti a hozzáférést a `Download` típusú
naplófájlban.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Eredeti üzenet letöltése {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Az eredeti üzenetet visszaadja úgy, ahogy azt megkaptuk (`message/rfc822`), spamszűrés,
DKIM-ellenőrzés vagy informatikai vizsgálatokhoz. Az eredeti üzenet csak akkor tárolódik, ha
az e-mail archiválásakor a `RawMessages:Enabled` be volt állítva; lásd:
[Original Message Storage Settings](Setup.md#-original-message-storage-settings).
Azt, hogy egy e-mail rendelkezik-e ilyen üzenettel, a `hasRawMessage` jelzi a
[`EmailDetailDto`](#emaildetaildto) felületen.

| Response header | Jelentése |
| --- | --- |
| `X-Raw-Message-Sha256` | Az visszaadott byteok SHA-256 értéke, amelyet az üzenet tárolásakor rögzítettek |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (pontos másolatok) vagy `serialized` (az elemző által újrakeretbe foglalva, a sorvégek CRLF formátumúak) |
| `X-Raw-Message-Storage` | `postgres` vagy `filesystem` — honnan olvasták ki a byteokat (lásd: [Storage backend](RawMessages.md#storage-backend)) |

A byteok az adatbázisban vagy egy fájlban lehetnek, attól függően, hogy az üzenet archiválásakor milyen `RawMessages:Storage` volt; az endpoint üzenetenként megoldja ezt, így mindkét típus azonos módon érhető el. A `RawMessages:FileSystem:VerifyChecksumOnRead` (alapértelmezett `true`) beállítás mellett a fájlt
újra lefásozás (re-hash) folyamatnak vetik alá, mielőtt elküldik, és az olyan üzenet, amelynek tartalma már nem egyezik a
rögzített ellenőrzősummával, **nem** kerül tovább.

`404`-at ad vissza, ha az üzenet nem létezik vagy nem látható a hívó számára,
`404`-et a `No original message is stored for this email.` hibaírással, ha az üzenet létezik eredeti változat nélkül, és `500`-et a
`Stored original message failed its checksum.` címmel, ha a tárolt tartalom már nem egyezik az archiváláskor
rögzített ellenőrzősummával. `403` (problem+json) értéket ad vissza, ha
`Api:AllowMessageDownloads=false`. A `Download` típusú hozzáférési naplót (access log) rögzít.

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

Visszaadja az hívó számára látható összesített archívum statisztikákat: összes archivált
e-mailt, látható posta-fiókokat, az e-mailekhez tartozó csatolmányokat, valamint a
PostgreSQL adatbázis teljes méretét megabajtában. Admin nem lévő felhasználók esetén a számok
csak a hozzájuk assigned posta-fiókokra korlátozódnak (ugyanúgy, ahogy a web
dashboard is alkalmazza); a rendszergazdák a globális összesítéseket látják.

Admin nem lévő API kulcsok esetén `databaseSizeInMB` egy üres karakterlánc: az adatbázis
mérete az egész instanciát írja le, nem pedig a kulcs körét. Az e-mailek, a Fiókok és a Csatolmányok
mindig a kulcs által hozzáférhető fiókok köré szűkülnek.

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

## Adattípusok {#data-types}

### MailAccountDto {#mailaccountdto}

| Mező | Típus | Megjegyzések |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

A hitelesítő adatokat tartalmazó mezők soha nem kerülnek szerializálásra.

### FolderNodeDto {#foldernodedto}

| Mező | Típus | Megjegyzések |
| --- | --- | --- |
| `name` | string | Levél neve |
| `fullPath` | string | Teljes elérési út |
| `totalCount` | int | Üzenetek a mappában |
| `level` | int | Belső elhelyezkedési mélység (0 = gyökér) |
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

Minden olyan adat, ami a [`EmailSummaryDto`](#emailsummarydto)-ben található, plus:

| Mező | Típus | Megjegyzések |
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

| Mező | Típus | Megjegyzések |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bytek |

A csatolmány byteit a [download endpoint](#download-attachment)-ból kell lekérni, nem pedig ebből a DTO-ból.

## Access logging {#access-logging}

Minden API-n keresztül történő adat-hozzáférés ugyanabban az hozzáférési naplóban kerül rögzítésre, amelyet a web UI is használ (a **Logs** menüpont alatt látható), a kulcs tulajdonos felhasználóhoz társítva:

| Endpoint | Logged type |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI és Swagger UI {#openapi-and-swagger-ui}

Amikor az API engedélyezve van és `Api:EnableSwaggerUi=true`:

- Az OpenAPI v1 dokumentum a **`/apidocs/spec/v1.json`** elérési helyen érhető el.
- A Swagger UI a **`/apidocs`** elérési helyen érhető el.

Mindkét elérési út kívül esik a `/api/` területén, ezért a normál webes (cookie) hitelesítés védi őket — a megtekintésükhöz bejelentkezett böngésző-azonnyalátt (session) szükséges, és nem érhetők el API kulccal.

### Endpoint referencia ezen az oldalon {#endpoint-reference-on-this-site}

A végpontok (endpoints) és az adattípusok itt is közzétételre kerülnek, mint egy generált oldal:
**[API Reference](API-Reference.md)**. Ezt ugyanabból az OpenAPI dokumentumból rendereli `tools/gen_api_reference.py` — amely másolatát a `doc/assets/openapi-v1.json` tárolja. A CI hibát jelez, ha az oldal és a specifikáció elcsúszik egymástól, így a referencia mindig azt az API-t írja le, amely ténylegesen kerül kiállításra.

!!! note "Regenerálás"
    Az oldal generált kimenet. Ha megváltoztatja az API-t (vagy a generátort), futtassa le a `python3 tools/gen_api_reference.py` parancsot, majd commitelje az eredményt.

    Magának a beágyazott specifikációnak a frissítése a `tools/README.md` részben leírt építési idejű (build-time) dokumentumgenerálás újraindítását jelenti.
