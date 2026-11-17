# Read-Only REST API (v1) {#read-only-rest-api-v1}

Mail Archiver prinaša opcionalni, **read-only** REST API za programsko dostop do arhivirane pošte. Zasnovan je za primere uporabe pri avtomatizaciji in integraciji (poročanje, izvozi za e-discovery, spremljanje) brez uporabe spletnega uporabniškega vmesnika.

Namen je omogočiti skripti ali AI agentu branje arhivirane pošte **brez izpostavljanja poveril za dostop do poštnega predala ali komunikacije s ponudnikom e-pošte.** Omejen API ključ za posameznika je funkcija za *branje arhive*, ne pa poverilo za *predstavljanje poštnega predala* — deduje le dovoljenja poštnega predala svojega lastnika, ne omogoča pisanja in ga je mogoče takoj preklicati brez poseganja v osnovni e-poštni račun.

> ⚠️ **API je privzeto onemogočen.** V konfiguraciji ga je treba izrecno omogočiti (poglejte [Enabling the API](#enabling-the-api)). Ko je onemogočen, vsaka `/api/*` ruta vrne `404 Not Found`.

API je **zasnovan kot read-only**: omogoča dostop do računov, map, iskanja sporočil, podrobnosti sporočil in prenos prilog. Nikoli ne ustvarja, ne spreminja in ne briše podatkov.

## Kazalo vsebine {#table-of-contents}

- [Omogočanje API](#enabling-the-api)
- [Avtentikacija](#authentication)
  - [Format API ključa](#api-key-format)
  - [Življenjski cikel ključa](#key-lifecycle)
  - [Model dovoljenj](#permission-model)
  - [Razlog za varnost](#security-rationale)
- [Konvencije](#conventions)
  - [Osnovna URL in različice](#base-url-and-versioning)
  - [Straničenje](#pagination)
  - [Napake](#errors)
  - [Omejitev števila zahtev](#rate-limiting)
- [Končne točke](#endpoints)
  - [Seznam računov](#list-accounts)
  - [Seznam map](#list-folders)
  - [Iskanje sporočil](#search-messages)
  - [Pridobivanje sporočila](#get-message)
  - [Prenos priloge](#download-attachment)
  - [Prenos sporočila (.eml)](#download-message-eml)
  - [Prenos izvirnega sporočila](#download-original-message)
  - [Statistika](#stats)
  - [Zdravje sistema](#health)
  - [Metrike](#metrics)
- [Vrste podatkov](#data-types)
- [Zapisovanje dostopa](#access-logging)
- [OpenAPI in Swagger UI](#openapi-and-swagger-ui)
- [Referenčna dokumentacija API](API-Reference.md) — generirana referenca končnih točk in vrst podatkov

## Omogočanje API {#enabling-the-api}

API je konfiguriran v razdelku `Api` v `appsettings.json` (ali prek ustreznih okoljskih spremenljivk, npr. `Api__Enabled=true`):

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


### Življenjska doba ključa {#key-lifecycle}

1. **Ustvari** — uporabnik ustvari ključ z opisnim `Name` in po želji s časovnim omejitevjo veljavnosti. Odziv pri tem enkrat prikaže ključ v obliki nešifriranega besedila.
2. **Uporabi** — ključ pošljite kot bearer token. Vsaka sprejeta zahteva osveži `LastUsedAt` ključa (zapisovanje je omejeno na največ enkrat vsakih ~5 minut, da se prepreči zapis pri vsakem klicu).
3. **Poteče** — če je `ExpiresAt` nastavljeno in je datum v preteklosti, bo ključ zavrnjen.
4. **Preneha veljati** — lastnik ključa ali administrator ključ prekliče (nastavi `RevokedAt`); ključ bo takoj in trajno zavrnjen.

Ključ je **aktiven**, ko ni preklican, ni iztekli in je njegov lastnik še vedno aktiven. Ključ, ki je zaradi katerega od teh razlogov neaktiven, bo zavrnjen s kodo `401`.

### Model dovoljenj {#permission-model}

API ključ deduje natančna dovoljenja uporabnika, ki je njegov lastnik. Avtentificirani princip ima iste zahteve (claims) kot obična prijava na spletno mesto, zato so vsa obstoječa dovoljenja...

| Nastavitev | Z умолчанию | Opis |
| --- | --- | --- |
| `Enabled` | `false` | Glavno stikalo. Ko je `false`, vsi `/api/*` poti vrnejo `404`, dokument OpenAPI in Swagger UI pa niso preslikani. |
| `AllowAttachmentDownloads` | `true` | Ko je `false`, endpoint za prenos prilog vrne `403`. Metadata o prilogah pa jih še vedno vrne endpoint za podrobnosti sporočila. |
| `AllowMessageDownloads` | `true` | Ko je `false`, endpointi za celotna sporočila (`/eml` in `/raw`) vrnejo `403`. Celotno sporočilo vsebuje svoje priloge, zato morata biti **tako** to stikalo kot `AllowAttachmentDownloads` nastavljena na `true`; izklop bilo katerega od njiju onemogoči oba endpointa. |
| `EnableSwaggerUi` | `true` | Ko je `true` (in je `Enabled` nastavljeno na `true`), montira Swagger UI na `/apidocs` in dokument OpenAPI na `/apidocs/spec/v1.json`. Oba zahtevata avtentificirano **spletno (cookie) sesijo** — nista del `/api/` in nista dostopna z API ključem. |
| `DefaultPageSize` | `20` | Velikost strani, ki se uporabi, ko zahteva izpusti `pageSize`. |
| `MaxPageSize` | `100` | Zgornja meja; večje vrednosti `pageSize` se omejijo na to vrednost. |
| `RateLimitPerMinute` | `120` | Proračun zahtev v fiknem oknu na minuto na API ključ (poglejte [Rate limiting](#rate-limiting)). |

## Avtentikacija {#authentication}

API se avtentikira s **API ključi za posameznika**, ki se pošljejo kot bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` sprejema **le** API ključe. Piškotki brskalnika niso veljavni, zato
API nima CSRF površine. Napake pri avtentikaciji vrnejo `401` s
glavnikom `WWW-Authenticate: Bearer` in telesom [problem+json](#errors) — API se
**nikoli** ne preusmeri na stran za prijavo.

Ključi se upravljajo prek samostojnega storitvene uporabniškega vmesnika v spletnem vmesniku pod **API Keys** (vsak uporabnik upravlja s svojimi ključi; administratorji lahko vidijo in prekličejo ključe katerega koli uporabnika).

### Oblika API ključa {#api-key-format}

Ključ je niz `ma_`, ki mu sledi 43 URL-varovnih base64 znakov, ki kodirajo 32
naključnih bajtov (256 bitov entropije):

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Shranjevanje:

- **`KeyPrefix`** — prvih ~11 znakov (npr. `ma_8Kf3pQ2`), shranjenih v
  prostekstni obliki. Uporablja se za identifikacijo ključa v vmesniku in v dnevnikih. Ni skrivnost.
- **`KeyHash`** — SHA-256 celotnega ključa v hex-kodiranem formatu, shranjen pod
  **unikatnim indeksom**. Prostekstni ključ nikoli ni shranjen.

Celoten ključ je uporabniku prikazan **točno enkrat**, ob ustvarjanju. Ne more
biti pozneje pridobil; izgubljen ključ je treba preklicati in zamenjati.

### Življenjska doba ključa {#key-lifecycle}

1. **Ustvari** — uporabnik ustvari ključ z opisnim `Name` in po želji določenim
   rokom veljavnosti. Odziv prikaze prostekstni ključ enkrat.
2. **Uporabi** — pošlji ključ kot bearer token. Vsaka sprejeta zahteva osveži
   `LastUsedAt` ključa (zapisovanje je omejeno na največ enkrat vsakih ~5 minut, da se
   prepreči zapis pri vsakem klicu).
3. **Poteče** — če je `ExpiresAt` nastavljen in je v preteklosti, je ključ zavrnjen.
4. **Prekliči** — lastnik ključa ali administrator ključ prekliče
   (nastavi se `RevokedAt`); ključ je takoj in trajno zavrnjen.

Ključ je **aktiven**, ko ni preklican, ni zastarel **in** je njegov lastnik
še vedno aktiven. Ključ, ki je zaradi katerega od teh razlogov neaktiven, je zavrnjen s `401`.

### Model dovoljenj {#permission-model}

API ključ deduje točno tista dovoljenja, ki jih ima uporabnik, ki ga je lastnik.
Avtentikovan princip ima iste zahteve (claims), kot jih izdaja običajna spletna prijava, zato
se vsa obstoječa logika avtorizacije uporablja nepreobstočeno:

- **Administratorji** (`IsAdmin`) vidijo vse e-poštne račune in vse sporočilo.
- **Običajni uporabniki** vidijo le tiste e-poštne račune, ki so jim dodeljeni
  (`UserMailAccount`). Računi in sporočila zunaj tega nabora se obravnavajo kot
  **neobstojajoči**: zahtevki za njih vrnejo `404`, ne `403`, zato API
  ne razkriva obstoja računov, ki jih klicatelj ne more videti.

### Razlog za varnost {#security-rationale}

- **Obzijanje 2FA.** API ključi po zasnovi obijejo dvostopenjsko avtentikacijo — to je
  neodločno pri netektičih tokenih (isto velja za osebne dostopne tokene
  na GitHub/GitLab). To se ublaža z tem, da so ključi **privzeto izključeni**,
  preklicljivi, lahko opционално zastarejo, so omejeni na uporabnikova dovoljenja in se
  vsak dostop zabeleži v [access log](#access-logging).
- **Brez piškotkov na `/api`.** Avtentikacija s piškotki ni sprejeta na `/api`
  potih, kar odpravlja concerns glede CSRF za API.
- **Prenos.** V produkciji vedno uporabljajte HTTPS za API, da bearer tokeni
  niso izpostavljeni v omrežju.

## Konvencije {#conventions}

### Osnovna URL in različica {#base-url-and-versioning}

Vse končnice so pod `/api/v1`. Različica je v poti, da lahko bodo prihodnje prekinitevne spremembe izdane kot `/api/v2`, ne da bi motili obstoječe odjemalce.

Odgovori so `application/json`, razen če ni navedeno drugače (prenosi prilog vrne lastno vrsto vsebine priloge). Vsi časovni žigri so v UTC, ISO 8601 (`2026-06-11T09:30:00Z`).

### Straniciranje {#pagination}

Končnice za sezname vrnejo ovojnico za straničenje:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` je zasnovano na osnovi številke 1. Stranice izven obsega vrnejo prazno polje `items` (ne napako).
- `pageSize` je privzeto `Api:DefaultPageSize` in je omejeno na `[1, Api:MaxPageSize]`.

### Napake {#errors}

Napake uporabljajo [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
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

| Status | Kdaj |
| --- | --- |
| `400 Bad Request` | Malformirani parametri (npr. neanalizirabilen datum, neveljavna `sortBy`). |
| `401 Unauthorized` | Manjkajoč, malformiran, zastarel, preklican ali na kakšen drug način neveljavni ključ. Vključuje `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Zahtevani prenosi prilog med `AllowAttachmentDownloads=false` ali prenosi sporočil med `AllowMessageDownloads=false`. |
| `404 Not Found` | Vir ne obstaja **ali** je izven dovoljenih računov uporabnika. Vrne se tudi za vsak `/api/*` pot, ko je `Api:Enabled=false`. |
| `429 Too Many Requests` | Prekrocena omejitev števila zahtev. |
| `500 Internal Server Error` | Nepričakovana napaka strežnika (problem+json, nikoli HTML strani z napako). |

### Omejitev števila zahtev {#rate-limiting}

Zahtev je omejenih s politiko fikznega okna `Api:RateLimitPerMinute`
zahtev na minuto (privzeto `120`), razdeljenih glede na prefiks API ključa (v primeru, ko ključa ni, se uporabi IP naslov odjemalca). Prekoračitev proračuna vrne `429`.

## Endpoints {#endpoints}

> Vsi primeri predpostavljajo, da je API omogočen in da `$KEY` vsebuje veljavni ključ.

### Seznam računov {#list-accounts}

```
GET /api/v1/accounts
```

Vrne e-poštne račune, ki so vidni klicatelju. Polja s pooblaščenimi podatki
(IMAP/OAuth username, password, client secret, …) **nikoli** niso vključena.

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

### Seznam map {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

Vrne drevo map za en račun kot ugnezdeno strukturo. Vrne `404`, če
račun ne obstaja ali pa ni viden povsilovalcu.

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

### Iskanje sporočil {#search-messages}

```
GET /api/v1/emails
```

Iskanje arhiviranih sporočil z enajmo zmožnostjo kot je spletni uporabniški vmesnik.

| Vrstni parameter | Tip | Opis |
| --- | --- | --- |
| `q` | string | Poiskavo besedila po temelih: subject, body, from, to, cc, bcc. Podpira sintakso polj, npr. `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Najprejši datum pošiljanja (vključno), `YYYY-MM-DD`. |
| `to` | date | Najpozdnejejš datum pošiljanja (vključno), `YYYY-MM-DD`. |
| `accountId` | int | Omejitev na en račun (mora biti vidno povzpovedniku). |
| `folder` | string | Omejitev na eno mapo s polno potjo. |
| `direction` | string | `incoming` ali `outgoing`. |
| `page` | int | Številka strani od 1 naprej (privzeto `1`). |
| `pageSize` | int | Število rezultatov na stran (privzeto `Api:DefaultPageSize`, omejeno na `Api:MaxPageSize`). |
| `sortBy` | string | Ena od `sentDate` (privzeto), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` ali `desc` (privzeto `desc`). |

Vrne [paged envelope](#pagination) za [`EmailSummaryDto`](#emailsummarydto).
Zapiše v [access log](#access-logging) vstop tipa `Search`.

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

Vrne celotno sporočilo, vključno z vsebino (bodies). Za vsebino se uporablja ista veriga zamenjave (fallback chain) kot v spletnem uporabniškem vmesniku: `OriginalBodyHtml` (raw bytes) → `BodyUntruncatedHtml` →
`HtmlBody`, in ustrezno za besedilno vsebino. Vrne `404`, če sporočilo ne obstaja ali pripada računu izven권权限 (permissions) klicatelja. Zabeleži v dnevnik dostopa z vrsto `Open`.

Vrne [`EmailDetailDto`](#emaildetaildto).

### Prenos priloge {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Preteka bajte priloge s pripadajočim shranjenim tipom vsebine in imenom datoteke. Vrne
`404`, če sporočilo ali priloga ne obstaja ali pa ni vidna povratniku. Vrne `403` (problem+json), ko `Api:AllowAttachmentDownloads=false`.
Zapiše v dnevnik dostopa vstop z vrsto `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Prenos sporočila (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Vrne sporočilo kot `message/rfc822`, zgrajeno iz arhiviranih polj:
naslovi, shranjeni surovih zaglavkov (`Received`, `Authentication-Results`,
`X-Spam-*`, …), telesa in prilog. Deluje za vsako arhivirano e-pošto. Gre za
isto rekonstrukcijo, ki jo ustvari izvoz računa v spletnem vmesniku, zato ni
**identična** na ravni bajtov tistemu, ki je bilo prejeto: DKIM podpisi se z njo ne bodo potrdili.
Za to uporabite [`/raw`](#download-original-message).

Vrne `404`, če sporočilo ne obstaja ali pa ni vidno povlečevalcu, in
`403` (problem+json), ko `Api:AllowMessageDownloads=false`. Zabeleži v dnevnik dostopa
z vrsto `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Prenos originalne sporočile {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Vrne originalno sporočilo, kot je bilo prejeto (`message/rfc822`), za analizo nezaželene pošte,
preverjanje DKIM ali forenzične preiskave. Original se shrani le, ko je bila
`RawMessages:Enabled` nastavljena v času arhiviranja e-pošte; poglejte
[Original Message Storage Settings](Setup.md#-original-message-storage-settings).
Podatki o tem, ali ima e-pošta originalno sporočilo, so prikazani v `hasRawMessage` v
[`EmailDetailDto`](#emaildetaildto).

| Glava odziva | Pom meaning |
| --- | --- |
| `X-Raw-Message-Sha256` | SHA-256 vrnjenih bajtov, zabeležen ob shranovanju sporočila |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (točne kopije) ali `serialized` (ponovno zapisano s strani解析nika, končnice vrnic normalizirane na CRLF) |
| `X-Raw-Message-Storage` | `postgres` ali `filesystem` — od kod so bili bajti prebrani (poglejte [Storage backend](RawMessages.md#storage-backend)) |

Bajte lahko shranjuje baza podatkov ali datoteka, odvisno od
`RawMessages:Storage` v času arhiviranja sporočila; konališče (endpoint)
to razreši posamezno za vsako sporočilo, zato sta oba tipa obdeljena na enak način. S
`RawMessages:FileSystem:VerifyChecksumOnRead` (privzeto `true`) se datoteka
ponovno izračuna (re-hash), preden je poslana, sporočilo, čije vsebina ne ustreza več
zabeleženemu kontrolnemu zmecu, **ne** bo poslano.

Vrne `404`, če sporočilo ne obstaja ali ni vidno povlečevalcu,
`404` s naslovom težave `No original message is stored for this email.`,
ko sporočilo obstaja brez originala, in `500` z naslovom
`Stored original message failed its checksum.`, ko shranjena vsebina ne ustreza več
kontrolnemu zmecu, zabeleženemu ob arhiviranju. Vrne `403` (problem+json), ko je
`Api:AllowMessageDownloads=false`. Zabeleži vstop v dnevnik dostopa tipa `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -o 9876.eml \
  https://host/api/v1/emails/9876/raw
sha256sum 9876.eml          # matches X-Raw-Message-Sha256
spamassassin -t < 9876.eml  # or: rspamc < 9876.eml
```

### Statistika {#stats}

```
GET /api/v1/stats
```

Vrne agregirane statistike arhive, ki so vidne povratniku: skupno število arhiviranih
e-poštnih sporočil, vidnih poštnih računov, prilog v teh sporočilih in skupno
velikost PostgreSQL baze podatkov v megabitih. Za uporabnike brez administratorskih
pravic so številke omejene na poštne račune, dodeljene njim (isto omejevanje,
ki ga uporablja spletni nadzorni panel); administratorji vidijo globalne skupne vrednosti.

Za API ključe brez administratorskih pravic je `databaseSizeInMB` prazn niz: velikost
baze podatkov opisuje celotno instanco, ne obseg ključa. Emails, Accounts in
Attachments so vedno omejeni na račune, ki jih lahko ključ dostopa.

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

| Polje | Tip | Opombe |
| --- | --- | --- |
| `emails` | int | Arhivirana sporočila, ki so vidna povabilcu |
| `accounts` | int | Poštni računi, ki so vidni povabilcu |
| `attachments` | int | Priloge pri teh vidnih sporočilih |
| `databaseSizeInMB` | string | Celotna velikost PostgreSQL baze v MB; prazno za ključe brez admin pravic |

### Health {#health}

```
GET /api/v1/health/live
GET /api/v1/health/ready
GET /api/v1/health
```

Vsi trije zahtevajo admin API ključ. `health/live` odgovori s 200 brez dotika katere koli
odvisnosti. `health/ready` preveri PostgreSQL in shemo arhiva ter vrne 503, ko
katera od njiju ni na voljo. `health` prav tako izvaja preverjanje svežine sinhronizacije in vrne `Degraded`
(HTTP 200), ko omogočeni račun ni sinhroniziran v okviru `Health:SyncStaleMinutes`.

```json
{"status":"Healthy","checks":{"database":"Healthy","sync":"Healthy"}}
```

Celoten opis, vključno s vedenjem 401/403/404 in Docker preverjanjem zdravja, je v
**[Health and Metrics](Monitoring.md)**.

### Metrike {#metrics}

```
GET /api/v1/metrics
```

Vrne v formatu tekstovne izpostavitve Prometheus. Zahteva admin API ključ in
`Metrics:Enabled=true`; v nasprotnem primeru bo končnica vrnila 404. Imena metrik, oznake in
primeri alarmov so v razdelku **[Health and Metrics](Monitoring.md)**.

## Vrsti podatkov {#data-types}

### MailAccountDto {#mailaccountdto}

| Polje | Tip | Opombe |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

Polja s pooblaščenimi podatki (credentials) niso nikoli serijalizirana.

### FolderNodeDto {#foldernodedto}

| Polje | Tip | Opombe |
| --- | --- | --- |
| `name` | string | Ime končnega elementa (Leaf name) |
| `fullPath` | string | Polna pot |
| `totalCount` | int | Sporočila v mapi |
| `level` | int | Globina vstavljanja (0 = korenina) |
| `children` | FolderNodeDto[] | Podmapi |

### EmailSummaryDto {#emailsummarydto}

| Polje | Tip |
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

Vse iz [`EmailSummaryDto`](#emailsummarydto) plus:

| Polje | Tip | Opombe |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Razžeže preko verige ponovnih možnosti v telesu (body fallback chain) |
| `htmlBody` | string | Razžeže preko verige ponovnih možnosti v telesu (body fallback chain) |
| `attachments` | AttachmentDto[] | Le metapodatki |
| `hasRawMessage` | bool | `true` ko je originalno sporočilo shranjeno in [`/raw`](#download-original-message) ga vrne |

### AttachmentDto {#attachmentdto}

| Polje | Tip | Opombe |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bajti |

Bajti priloge so pridobljeni prek [download endpoint](#download-attachment),
ne iz tega DTO.

## Beleženje dostopa {#access-logging}

Vsak dostop do podatkov prek API se zabeleži v istem dnevniku dostopa, ki ga uporablja spletni vmesnik (vidno pod **Logs**), in se pripiše uporabniku, ki je lastnik ključa:

| Endpoint | Vrsta zabeleženega dogodka |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI in Swagger UI {#openapi-and-swagger-ui}

Ko je API omogočen in `Api:EnableSwaggerUi=true`:

- Dokument OpenAPI v1 je na voljo na **`/apidocs/spec/v1.json`**.
- Swagger UI je na voljo na **`/apidocs`**.

Oba poti sta zunaj `/api/` in zato zahtevata običajno spletno (s piškotki) avtentikacijo — za ogled zahtevana je prijavljena sesija v brskalniku in do njima ni mogoče dostopati z API ključem.

### Referenca na končne točke na tej strani {#endpoint-reference-on-this-site}

Končne točke in tipi podatkov so tukaj objavljeni tudi kot generirana stran:
**[API Reference](API-Reference.md)**. Generira jo `tools/gen_api_reference.py` iz tega istega OpenAPI dokumenta — kopija je shranjena na `doc/assets/openapi-v1.json`. CI ne uspe, če stran in specifikacija nista v sinkronizaciji, zato referenca vedno opisuje dejansko verzijo API, ki se distribuira.

!!! note "Regeneracija"
    Stran je generiran izhod. Če spremenite API (ali generator), nato zaženite
    `python3 tools/gen_api_reference.py` in predložite (commit) rezultat.

    Osvežitev same priložene specifikacije pomeni ponovno zagon
    generiranja dokumentacije v času gradnje, ki je opisano v `tools/README.md`.
