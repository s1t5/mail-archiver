# Read-Only REST API (v1) {#read-only-rest-api-v1}

Mail Archiver prinaša neobvezen, **read-only** REST API za programsko dostop do arhivirane pošte. Zasnovan je za primere uporabe pri avtomatizaciji in integraciji (poročanje, izvozi e-discovery, spremljanje) brez uporabe spletnega vmesnika (web UI).

Namen je omogočiti skripti ali AI agentu branje arhivirane pošte **brez izpostavljanja poveril za poštni predal ali komunikacije s ponudnikom pošte.** Omejen API ključ na uporabnika je funkcija za *branje arhive*, ne pa poverilo za *identiteto poštnega predala* — dedišči le dovoljenja poštnega predala svojega lastnika, ne omogoča pisanja in ga je mogoče takoj preklicati brez poseganja v osnovni poštni račun.

> ⚠️ **API je privzeto onemogočen.** Vsak `/api/*` pot mora biti izrecno omogočen v konfiguraciji (poglejte [Enabling the API](#enabling-the-api)). Ko je onemogočen, vsaka `/api/*` pot vrne `404 Not Found`.

API je **zasnovan kot read-only**: omogoča dostop do računov, map, iskanja sporočil, podrobnosti sporočil in prenosov prilog. Nikoli ne ustvarja, ne spreminja in ne briše podatkov.

## Kazalo vsebine {#table-of-contents}

- [Omogočanje API](#enabling-the-api)
- [Avtentikacija](#authentication)
  - [Format API ključa](#api-key-format)
  - [Življenjska doba ključa](#key-lifecycle)
  - [Model dovoljenj](#permission-model)
  - [Razlog za varnost](#security-rationale)
- [Konvencije](#conventions)
  - [Osnovna URL in različica](#base-url-and-versioning)
  - [Straničenje](#pagination)
  - [Napake](#errors)
  - [Omejitev števila zahtev](#rate-limiting)
- [Končališča](#endpoints)
  - [Seznam računov](#list-accounts)
  - [Seznam map](#list-folders)
  - [Iskanje sporočil](#search-messages)
  - [Pridobivanje sporočila](#get-message)
  - [Prenos priloge](#download-attachment)
  - [Prenos sporočila (.eml)](#download-message-eml)
  - [Prenos originalnega sporočila](#download-original-message)
  - [Statistika](#stats)
- [Vrste podatkov](#data-types)
- [Zapisovanje dostopa](#access-logging)
- [OpenAPI in Swagger UI](#openapi-and-swagger-ui)
- [Referenčna dokumentacija API](API-Reference.md) — generiran seznam končališč in vrst podatkov

## Omogočanje API-ja {#enabling-the-api}

API je konfiguriran pod razdelkom `Api` v `appsettings.json` (ali ustreznimi okoljskimi variablami, npr. `Api__Enabled=true`):

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

1. **Ustvarjanje** — uporabnik ustvari ključ z opisnim `Name` in izbirnim
   časom poteka. Odziv prikaže ključ v besedilni obliki le enkrat.
2. **Uporaba** — pošljite ključ kot bearer token. Vsak sprejet zahtev iz tazne
   `LastUsedAt` (pisalni procesi so omejeni na največ enkrat vsakih ~5 minut, da se
   prepreči pisanje ob vsaki அழைvi).
3. **Potek** — če je `ExpiresAt` nastavljen in je v preteklosti, je ključ zavrnjen.
4 **Prevzem** — lastnik ključa ali administrator ključ prekliče
   (`RevokedAt` je nastavljen); ključ je takoj in trajno zavrnjen.

Ključ je **aktiven**, ko ni prekličen, ni iztekel **in** je njegov lastnik še vedno aktiven. Ključ, ki je zaradi katerega koli od teh razlogov neaktiven, je zavrnjen z `401`.

### Model dovolil {#permission-model}

API ključ dedišči točna dovoljenja uporabnika, ki ga lastnikuje.
Avtentificirani princip ima iste zahteve (claims) kot običen prijavični proces na spletu, zato so vsi obstoječi avtoriz

| Nastavitev | Privzeto | Opis |
| --- | --- | --- |
| `Enabled` | `false` | Glavno stikalo. Ko je `false`, vsi `/api/*` poti vrnejo `404` in dokument OpenAPI / Swagger UI nista preslikana. |
| `AllowAttachmentDownloads` | `true` | Ko je `false`, endpoint za prenos priloge vrne `403`. Metapodatki o prilogah jih še vedno vrača endpoint za podrobnosti sporočila. |
| `AllowMessageDownloads` | `true` | Ko je `false`, endpointi za celotna sporočila (`/eml` in `/raw`) vrnejo `403`. Celotno sporočilo vsebuje svoje priloge, zato morata biti **obe** stikalo in `AllowAttachmentDownloads` nastavljena na `true`; izklop bilo katerega od obeh onemogoči oba endpointa. |
| `EnableSwaggerUi` | `true` | Ko je `true` (in `Enabled` je `true`), montira Swagger UI na `/apidocs` in dokument OpenAPI na `/apidocs/spec/v1.json`. Oba zahtevata avtentificirano **web (cookie) sesijo** — nista del `/api/` in nista dosežljiva z API ključem. |
| `DefaultPageSize` | `20` | Velikost strani, ko zahteva ne vsebuje `pageSize`. |
| `MaxPageSize` | `100` | Zgornja meja; večje vrednosti `pageSize` so omejene na to. |
| `RateLimitPerMinute` | `120` | Proračun zahtev v fiksnem oknu na minuto na API ključ (glejte [Omejitev hitrosti](#rate-limiting)). |

## Avtentikacija {#authentication}

API se avtentikira z **API ključi za posameznega uporabnika**, ki se pošljejo kot bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` sprejema **le** API ključe. Spletni piškotki niso upoštevani, zato API nima CSRF površine. Napake pri avtentikaciji vrnejo `401` s
povstetjem `WWW-Authenticate: Bearer` in telesom [problem+json](#errors) — API se **nikoli** ne preusmeri na stran za prijavo.

Ključe se upravlja prek samostojnega servisa v spletnem vmesniku pod **API Keys** (vsak uporabnik upravlja svoje ključe; administratorji lahko vidijo in prekličejo ključe katerega koli uporabnika).

### Oblika API ključa {#api-key-format}

Ključ je nizek `ma_`, nato pa 43 URL-varnih base64 znakov, ki kodirajo 32
naključnih bitov (256 bitov entropije):

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Shranjevanje:

- **`KeyPrefix`** — prvih ~11 znakov (npr. `ma_8Kf3pQ2`), shranjenih v
  besedilu. Uporablja se za identifikacijo ključa v vmesniku in v dnevnikih. Ni tajna.
- **`KeyHash`** — SHA-256 celotnega ključa, kodiran v hex, shranjen pod
  **unikatnim indeksom**. Ključ v besedilu nikoli ni shranjen.

Celoten ključ je uporabniku prikazan **točno enkrat** ob ustvarjanju. Pozneje ga ni mogoče
priti nazaj; izgubljen ključ je treba preklicati in zamenjati.

### Življenjska doba ključa {#key-lifecycle}

1. **Ustvari** — uporabnik ustvari ključ z opisnim `Name` in možnostno
   rokom uporabnosti. Odziv prikaže ključ v besedilu enkrat.
2. **Uporabi** — pošljite ključ kot bearer token. Vsak sprejet zahteva osveži
   `LastUsedAt` ključa (pisanje je omejeno na največ enkrat vsakih ~5 minut, da se
   izogiba pisanju ob vsakem klicu).
3. **Preteče** — če je `ExpiresAt` nastavljen in je v preteklosti, je ključ zavrnjen.
4. **Prekliči** — lastnik ključa ali administrator ključ prekliče
   (`RevokedAt` je nastavljen); ključ je takoj in trajno zavrnjen.

Ključ je **aktiven**, ko ni prekličen, rok uporabnosti ni pretekel **in** je njegov lastnik še vedno aktiven. Ključ, ki je neaktiven iz katerega koli od teh razlogov, je zavrnjen z `401`.

### Model dovolil {#permission-model}

API ključ dedišči točna dovoljenja uporabnika, ki ga je lastnik.
Avtentikacija nosi tiste zahteve (claims), ki jih izdaja običajna prijava v splet, zato se vsa obstoječa logika avtorizacije uporablja neposredno:

- **Administratorji** (`IsAdmin`) vidijo vse e-poštne račune in vse sporočilo.
- **Običajni uporabniki** vidijo le tiste e-poštne račune, ki so jim dodeljeni
  (`UserMailAccount`). Računi in sporočila zunaj tega nabora se obravnavajo kot
  **ne obstajajoči**: zahtevki za njih vrnejo `404`, ne `403`, zato API ne
  razkriva obstoja računov, ki jih klicatelj ne more videti.

### Razlog za varnost {#security-rationale}

- **Obzijavanje 2FA.** API ključi po zasnovi obijejo dvostopeno avtentikacijo — to je
  naravno za neinteraktivne tokene (isto velja za osebne dostopne tokene na
  GitHub/GitLab). To se ublaža z tem, da so ključi **privzeto izključeni**,
  možno jih je preklicati, imajo lahko rok uporabnosti, so omejeni na uporabnikova dovoljenja in se vsak
  dejanja zabeleži v [access log](#access-logging).
- **Brez piškotkov na `/api`.** Avtentikacija s piškotki ni sprejeta na routah `/api`,
  kar odpravlja concerns glede CSRF za API.
- **Prenos.** V produkciji vedno strežite API prek HTTPS, da bearer tokeni
  niso izpostavljeni na vezju.

## Konvencije {#conventions}

### Osnovna URL in različica {#base-url-and-versioning}

Vsi končnimi točkami (endpoints) so pod `/api/v1`. Različica je v poti, da lahko bodo prihodnje spremembe, ki vključujejo nekompatibilnost, izvedene kot `/api/v2`, ne da bi pri tem motili obstoječe odjemalce.

Odgovori so `application/json`, razen če je drugače navedeno (prenos prilog vrača lastno zawetje tipa vsebine priloge). Vsi časovni žetki so v UTC, ISO 8601 (`2026-06-11T09:30:00Z`).

### Paginacija {#pagination}

Končnimi točkami za sezname se vrne ovojnica paginacije:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` je 1-zacijena. Strani izven obsega vrnejo prazno polje `items` (ne napako).
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
| `400 Bad Request` | Malformirani parametri (npr. neanalizirabilen datum, neveljavni `sortBy`). |
| `401 Unauthorized` | Manjka, malformiran, zastarel, odvzet ali na drug način neveljav ključ. Vključuje `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Zahtevano prenosanje prilog med `AllowAttachmentDownloads=false` ali prenosovanje sporočil med `AllowMessageDownloads=false`. |
| `404 Not Found` | Vir ne obstaja **ali** je izven dovoljenih računov klicatelja. Prav tako se vrne za vsako pot `/api/*`, ko je `Api:Enabled=false`. |
| `429 Too Many Requests` | Prekrocjen limit hitrosti (Rate limit). |
| `500 Internal Server Error` | Nepričakovana napaka strežnika (problem+json, nikoli HTML strani z napako). |

### Omejitev hitrosti (Rate limiting) {#rate-limiting}

Zahtevki so omejeni s politiko fiksnega okna `Api:RateLimitPerMinute` zahtev na minuto (privzeto `120`), razdeljenih glede na predpono API ključa (če ključa ni prisoten, se uporabi IP stranke). Prekorašitev proračuna vrne `429`.

## Končne točke {#endpoints}

> Vsi primeri predpostavljajo, da je API omogočen in da `$KEY` vsebuje veljavni ključ.

### Seznam računov {#list-accounts}

```
GET /api/v1/accounts
```

Vrne poštovne račune, ki so vidni povabilcu. Polja s pooblaščenimi podatki
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

Vrneča drevo map za en račun kot ujemljeno strukturo. Če računa ne obstaja ali pa ga povodec ne more videti, vrne `404`.

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

Iskanje arhiviranih sporočil z enako zmožnostjo kot v spletnem vmesniku.

| Parameter poševanja | Tip | Opis |
| --- | --- | --- |
| `q` | string | Poiskavo celotnega besedila po temah, vsebini, od, do, cc, bcc. Podpira sintakso polj, npr. `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Najprejja poslana datum (vključno), `YYYY-MM-DD`. |
| `to` | date | Najpoznejši poslana datum (vključno), `YYYY-MM-DD`. |
| `accountId` | int | Omejitev na en račun (mora biti viden povabilcu). |
| `folder` | string | Omejitev na eno mapo s polno potjo. |
| `direction` | string | `incoming` ali `outgoing`. |
| `page` | int | Številka strani na podlagi 1-osnove (default `1`). |
| `pageSize` | int | Rezultatov na stran (default `Api:DefaultPageSize`, omejeno na `Api:MaxPageSize`). |
| `sortBy` | string | Ena od `sentDate` (default), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` ali `desc` (default `desc`). |

Vrne [stranico z ovojnico](#pagination) za [`EmailSummaryDto`](#emailsummarydto).
Zapiše vpis v [dnevnik dostopa](#access-logging) tipa `Search`.

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

### Pridobite sporočilo {#get-message}

```
GET /api/v1/emails/{id}
```

Vrne polno sporočilo, vključno s telesom. Za telo se uporablja ista veriga zamenjave kot v spletnem vmesniku: `OriginalBodyHtml` (sistemski bajti) → `BodyUntruncatedHtml` →
`HtmlBody`, in ustrezno za besedilno telo. Če sporočilo ne obstaja ali pripada računu z zunaj klicateljevovega dovoljenja, vrne `404`. V zapis nesledilnega dnevnika zabeleži vpis vrste `Open`.

Vrne [`EmailDetailDto`](#emaildetaildto).

### Prenos priloge {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Prejavi bajte priloge s pripadajočim shranjenim tipom vsebine in imenom datoteke. Vrne
`404`, če sporočilo ali priloga ne obstaja ali pa ni vidna povzivalcu. Vrne `403` (problem+json), ko `Api:AllowAttachmentDownloads=false`.
Zapiše v dostopni dnevnik z vrsto `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Prenos sporočila (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Vrne sporočilo kot `message/rfc822`, ponovo sestavljeno iz arhiviranih polj:
naslovov, shranjenih nedoločenih zaglavljih (`Received`, `Authentication-Results`,
`X-Spam-*`, …), telesa in prilog. Deluje za vsako arhivirano e-pošto. Gre za
isto rekonstrukcijo, ki jo ustvari izvoz računa v spletnem uporabniškem vmesniku, zato **ni**
identična na bajt ravni tistemu, ki je bilo prejeto: DKIM podpisi se proti njej ne bodo preverili.
Za to uporabite [`/raw`](#download-original-message).

Vrne `404`, če sporočilo ne obstaja ali pa ga povodec ne vidi, in
`403` (problem+json), ko `Api:AllowMessageDownloads=false`. V dnevnik pristopov zabeleži
prispevek tipa `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Prenos originalne sporočile {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Vrne originalno sporočilo, kot je bilo prejeto (`message/rfc822`), za analizo nezaželjenih vsebin (spam),
verifikacijo DKIM ali forenzične preverbe. Original je shranjen le, če je bila takrat
nastavljena `RawMessages:Enabled`, ko je bilo e-poštno sporočilo arhivirano; poglejte
[Original Message Storage Settings](Setup.md#-original-message-storage-settings).
Ali ima e-poštno sporočilo original, je prikazano z `hasRawMessage` v
[`EmailDetailDto`](#emaildetaildto).

| Glava odzivnega besedila | Pomen |
| --- | --- |
| `X-Raw-Message-Sha256` | SHA-256 vrnjenih bajt, zabeležen ob shranitvi sporočila |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (točne kopije) ali `serialized` (prepisano s strani parsatorja, končnice vrsti z normalizacijo na CRLF) |
| `X-Raw-Message-Storage` | `postgres` ali `filesystem` — od kod so bile prebrani bajti (poglejte [Storage backend](RawMessages.md#storage-backend)) |

Bajtov lahko ne le v bazi podatkov, temveč tudi v datoteki, odvisno od
`RawMessages:Storage` ob času arhiviranja sporočila; končališče to razredi za vsako sporočilo, zato sta oba načina obdelovana na enak način. S
`RawMessages:FileSystem:VerifyChecksumOnRead` (privzeto `true`) se datoteka
ponovno izračuna (re-hash), preden je poslana, sporočilo, čebel którego vsebina ne ustrezal več
z zabeleženim kontrolnim zbirnikom, se **ne** posluša.

Vrne `404`, če sporočilo ne obstaja ali pa ni vidno povzivalcu,
`404` s naslovom težave `No original message is stored for this email.`,
če obstaja brez originala, in `500` z naslovom
`Stored original message failed its checksum.`, ko shranjena vsebina ne ustrezal več
kontrolnemu zbirku, zabeleženem ob arhiviranju. Vrne `403` (problem+json), ko je
`Api:AllowMessageDownloads=false`. Zabeleži v dostopni dnevnikju (access log) z vrsto `Download`.

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

Vrne agregirane statistike arhiva, ki so vidne povozovalcu: skupno število arhiviranih
e-poštnih sporočil, vidnih e-poštnih računov, prilog na teh sporočilih in skupno
veliko PostgreSQL baze podatkov v megabajtih. Za uporabnike brez upravljavskih pravic je
številka omejena na e-poštne račune, dodeljene njim (isto omejevanje, ki ga uporablja
spletni nadzorni panel); upravljavci vidijo globalne celote.

Za API ključe brez upravljavskih pravic je `databaseSizeInMB` prazen niz: velikost baze
podatkov opisuje celotno instanco, ne pa obseg ključa. E-poštna sporočila, računi in priloge
so vedno omejeni na račune, ki jih lahko ključ dostopa.

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
| `emails` | int | Arhivirana sporočila, vidna povabilcu |
| `accounts` | int | Poštni računi, vidni povabilcu |
| `attachments` | int | Priloge pri teh vidnih sporočilih |
| `databaseSizeInMB` | string | Celotna velikost PostgreSQL baze podatkov v MB; prazno za ključe brez admin pravic |

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

Polja za凭证 (credentials) nikoli niso serijalizirana.

### FolderNodeDto {#foldernodedto}

| Polje | Tip | Opombe |
| --- | --- | --- |
| `name` | string | Ime končnega elementa (Leaf name) |
| `fullPath` | string | Celotna pot |
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
| `textBody` | string | Razvedeno preko verige nadomeščenih teles (body fallback chain) |
| `htmlBody` | string | Razvedeno preko verige nadomeščenih teles (body fallback chain) |
| `attachments` | AttachmentDto[] | Le metapodatki |
| `hasRawMessage` | bool | `true` ko je originalno sporočilo shranjeno in [`/raw`](#download-original-message) ga vrne |

### AttachmentDto {#attachmentdto}

| Polje | Tip | Opombe |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Biti |

Biti priloge se pridobijo preko [download endpoint](#download-attachment),
ne iz tega DTOja.

## Beleženje dostopa {#access-logging}

Vsak dostop do podatkov prek API se zabeleži v isti dnevniku dostopa, ki ga uporablja spletni vmesnik (vidno pod **Logs**), in je pripisana uporabniku, ki je lastnik ključa:

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
- Swagger UI je na voljujem na **`/apidocs`**.

Obesje sta zunaj `/api/`, zato ju omejuje običajna spletna (preko piškotkov) avtentikacija — za ogled zahtevana je prijavljena sesija v brskalniku in ni mogoče dostopati z API ključem.

### Referenčni kontejner končinskih točk na tej strani {#endpoint-reference-on-this-site}

Končnišča in tipi podatkov so tukaj objavljeni tudi kot generirana stran:
**[API Reference](API-Reference.md)**. Generira jo `tools/gen_api_reference.py` iz tega samega OpenAPI dokumenta — kopija je shranjena na `doc/assets/openapi-v1.json`. CI ne uspe, če stran in specifikacija niso usklajene, zato referenca vedno opisuje API, ki je dejansko v uporabi.

!!! note "Regeneracija"
    Stran je generiran izhod. Če spremenite API (ali generator), nato zomežite `python3 tools/gen_api_reference.py` in oddate rezultat (commit).

    Osveževanje same priložene specifikacije pomeni ponovno zomejitev generiranja dokumentacije v času gradnje, ki je opisano v `tools/README.md`.
