# MCP strežnik (Model Context Protocol) {#mcp-server-model-context-protocol}

Mail Archiver priloži neobvezen **MCP (Model Context Protocol) strežnik**, ki razpoloaga arhivirano pošto AI agentom prek Streamable HTTP prenosa. Zrcali samozaščitni [REST API](API.md): isti podatki, isti model dovoljenj na uporabnika, enaka beleženja dostopa — le da so zapakirani kot MCP orodja namesto REST končnic, tako da jih lahko LLM agent neposredno odkrije in uporabi.

Namen je omogočiti AI agentu branje arhivirane pošte **brez izpostavljanja poveril za dostop do poštnega predala ali komuniciranja s ponudnikom pošte.** Omejen API ključ na uporabnika je sposobnost *branja arhiva*, ne pa poveril za *predstavljanje poštnega predala* — deduje le dovoljenja za poštno predal lastnika, ne omogoča pisanja in ga je mogoče takoj preklicati, ne da bi prišli v poseg v osnovni račun pošte.

> ⚠️ **MCP strežnik je privzeto onemogočen.** Izrecno ga je treba omogočiti v konfiguraciji (poglejte [Omogočanje MCP strežnika](#enabling-the-mcp-server)). Ko je onemogočen, `/mcp` končnica vrne `404 Not Found`.

MCP strežnik je **zasnovan kot samozaščitni (read-only)**: razpoložljiva orodja so za izpis računov, izpis map, iskanje sporočil, branje sporočila in prenos priloge. Nikoli ne ustvarja, ne spreminja in ne briše podatkov.

## Kazalo vsebine {#table-of-contents}

- [Omogočanje MCP strežnika](#enabling-the-mcp-server)
- [Avtentikacija](#authentication)
- [Transport in endpoint](#transport-and-endpoint)
- [Orodja](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
  - [get_email_eml](#get_email_eml)
  - [get_email_raw](#get_email_raw)
- [Omejitev števila zahtev](#rate-limiting)
- [Zapisovanje dostopa](#access-logging)
- [Povezovanje agenta](#connecting-an-agent)
- [Vodnik za varnost pri izpostavljenosti na internet](#security-guidance-for-internet-exposure)

## Omogočanje MCP strežnika {#enabling-the-mcp-server}

MCP strežnik je konfiguriran v razdelku `Mcp` v `appsettings.json` (ali v ustreznih okolenskih spremenljivkah, npr. `Mcp__Enabled=true`):

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

| Nastavitev | Z умолчанию | Opis |
| --- | --- | --- |
| `Enabled` | `false` | Glavno stikalo. Ko je `false`, endpoint `/mcp` vrne `404`. |
| `AllowAttachmentDownloads` | `true` | Ko je `false`, `get_attachment` namesto bajt prilog vrne napako. Metapodatki o prilogah jih še vedno vrne `get_email`. |
| `DefaultPageSize` | `20` | Velikost strani, ki jo uporabi `search_emails`, ko klicatelj izpusti `pageSize`. |
| `MaxResults` | `100` | Zgornja meja za velikost strani `search_emails`; večje vrednosti so omejene. |
| `MaxAttachmentBytes` | `10000000` | Maksimalna velikost priloge (v bajtih), ki jo bo `get_attachment` vrnil neposredno kot base64; večje priloge so zavrnjene. |
| `AllowMessageDownloads` | `true` | Ko je `false`, `get_email_eml` in `get_email_raw` namesto sporočila vrneta napako. Celotno sporočilo vsebuje svoje priloge, zato morata biti **tako** to stikalo kot `AllowAttachmentDownloads` nastavljena na `true`; izklop bilo katerega od obeh onemogoči oba orodja. |
| `MaxMessageBytes` | `10000000` | Maksimalna velikost sporočila (v bajtih), ki jo bosta `get_email_eml` in `get_email_raw` vrnila neposredno; večja sporočila so zavržena. |
| `RateLimitPerMinute` | `120` | Proračun zahtev s fiksnim oknom na ključ API na minuto (poglejte [Rate limiting](#rate-limiting)). |

## Avtentikacija {#authentication}

MCP strežnik se avtenticira z **istimi API ključi za posameznika kot REST API**, ki se pošljejo kot bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

API ključi se ustvarjajo in upravljajo v spletnem vmesniku pod **API Keys**. Ključ deduje točno tista dovoljenja, ki jih ima uporabnik, ki ga je lastnik (upravnik vidi vse račune; omejen uporabnik vidi le svoje dodeljene račune). Ključi lahko neposredno prekličemo ali zastarejo neodvisno od pripadajočih e-poštnih računov. Za celoten življenjski cikel ključa in model dovoljenj si oglejte [razdelek o avtentikaciji REST API](API.md#authentication) — MCP ključi niso ločen repozitorij za poverilnice.

Ko je MCP strežnik onemogočen ali pa ni poslodžen/je poslodžen neveljavni bearer token, končnica vrne ustrezno `404` / `401` (nikoli ne preusmerja na stran za prijavo).

## Transport in endpoint {#transport-and-endpoint}

MCP strežnik uporablja **Streamable HTTP** transport in je montiran na:

```
POST /mcp
```

Deluje v **stateless** načinu (ena JSON-RPC zahteva na en HTTP zahtev), kar je
priporočeni način za strežnike, ki ne potrebujejo zahtev od strežnika do klijenta,
kot so sampling ali elicitation. Orodja se odkrivajo prek standardne MCP
`tools/list` zahteve in se kličejo prek `tools/call`.

## Orodja {#tools}

### list_accounts {#list_accounts}

Izlista poštne račune, katerim je trenutnemu API ključu dovoljen dostop. Vrne
`id`, `name`, `emailAddress`, `provider`, `isEnabled` in `lastSync`.

**Parameterji:** none.

**Rezultat:** array objektov računov. Vrnjeni `id` uporabite s `list_folders`
in `search_emails`.

### list_folders {#list_folders}

Izlisti drevo map enega e-poštnega računa s številom e-poštnih sporočil po vsaki mapi.

**Parameterji:**

| Ime | Tip | Obvezno | Opis |
| --- | --- | --- | --- |
| `accountId` | integer | da | ID e-poštnega računa (iz `list_accounts`). |

**Rezultat:** niz objektov map, od katerih je vsak z `name`, `fullPath`, `totalCount`,
`level` in rekurzivnim `children`. Če račun ne obstaja ali ključ za njega ni pooblaščen, vrne prazen seznam.

### search_emails {#search_emails}

Išče arhivirana e-poštna sporočila s pomočjo poizvedbe besedila, časovnega razpona, računa, mapi in smeri.

**Parameterji:**

| Ime | Tip | Obvezno | Opis |
| --- | --- | --- | --- |
| `q` | string | ne | Poizvedba za iskanje besedila (zadeva, vsebina, od/do). |
| `from` | string (ISO-8601) | ne | Filter za datum/čas v UTC, poslano na ali po tem. |
| `to` | string (ISO-8601) | ne | Filter za datum/čas v UTC, poslano pred tem. |
| `accountId` | integer | ne | Omeji na eno identifikacijsko številko (id) e-poštnega računa. |
| `folder` | string | ne | Omeji na polno potjo ene mape (npr. `INBOX`). |
| `direction` | string | ne | `incoming` ali `outgoing`. |
| `page` | integer | ne | Številka strani, ki se začne s 1 (privzeto `1`). |
| `pageSize` | integer | ne | Velikost strani; omejena na `[1, MaxResults]`. `0` uporablja `DefaultPageSize`. |
| `sortBy` | string | ne | `sentdate` (privzeto), `receiveddate`, `subject`, `from`, `to`. |
| `sortOrder` | string | ne | `asc` ali `desc` (privzeto `desc`). |

**Rezultat:** stranica z objektom, ki vsebuje `items` (niz povzetkov e-poštnih sporočil), `page`,
`pageSize`, `totalItems`, `totalPages`. Vsak povzetek vsebuje `id`, `accountId`,
`subject`, `from`, `to`, `sentDate`, `isOutgoing`, `hasAttachments` in
`folderName`. Za celotno vsebino in seznam prilog predložite vrnjeni `id` funkci `get_email`.

### get_email {#get_email}

Pridobi celotno vsebino enega arhiviranega e-poštnega sporočila.

**Parameterji:**

| Ime | Tip | Obvezno | Opis |
| --- | --- | --- | --- |
| `id` | integer | da | ID arhiviranega e-poštnega sporočila (iz `search_emails`). |

**Rezultat:** en sam objekt e-poštnega sporočila z vsemi polji povzetka, vključno s `cc`, `bcc`,
`receivedDate`, `messageId`, `textBody`, `htmlBody` in `attachments` (niz
`{ id, fileName, contentType, size }`) ter `hasRawMessage` (ali lahko
`get_email_raw` vrne original). Vrne `null`, če e-poštno sporočilo ne
obstaja ali če ključ ni pooblaščen za njegov račun.

### get_attachment {#get_attachment}

Prenese eno prilogo e-pošte kot base64 blob.

**Parameterji:**

| Ime | Tip | Obvezno | Opis |
| --- | --- | --- | --- |
| `id` | integer | yes | Arhivirana ID e-pošte, kateri pripada priloga. |
| `attachmentId` | integer | yes | ID priloge (iz `attachments` od `get_email`). |

**Rezultat:** objekt z `id`, `emailId`, `fileName`, `contentType`, `size`
in `base64Data` (surovi bajti priloge, kodirani v base64). Orodje vrne
napako, ko je `Mcp:AllowAttachmentDownloads=false`, ko priloga presega
`Mcp:MaxAttachmentBytes`, ali ko ključ ni pooblaščen za račun e-pošte.

### get_email_eml {#get_email_eml}

Vrne e-pošto kot celotno sporočilo RFC 5322 (`.eml`), zgrajeno ponovno iz
arhiviranych polj: shranjenih zaglavkov (`Received`, `Authentication-Results`,
`X-Spam-*`, …), telesa in prilog. Deluje za vsako arhivirano e-pošto, vendar
rezultat ni bajtno identičen tistemu, ki je bil prejet, zato se DKIM podpisi
ne bodo uveljavljali proti njemu — za to uporabite [`get_email_raw`](#get_email_raw). Zrcali
`GET /api/v1/emails/{id}/eml`.

**Parameterji:**

| Ime | Tip | Obvezno | Opis |
| --- | --- | --- | --- |
| `id` | integer | da | Arhivirana ID e-pošte (iz `search_emails`). |

**Rezultat:** objekt z `emailId`, `kind` (`eml`), `source` (`null`),
`sha256`, `size`, `encoding` in `content`. `content` je besedilo sporočila, ko je
`encoding` `utf-8`, in besedilo sporočila v formatu base64, ko je `encoding`
`base64` (sporočila, ki niso veljavna v UTF-8, zato noben bajt ni spremenjen).
Orodje vrne napako, ko je `Mcp:AllowMessageDownloads=false`, ko sporočilo
prehiti `Mcp:MaxMessageBytes`, ali ko ključ ni pooblaščen za
račun e-pošte.

### get_email_raw {#get_email_raw}

Vrne izvirno sporočilo točno tako, kot je bilo prejeto, za analizo nezaželene pošte, preverjanje DKIM/SPF/ARC ali forenziko. Na voljo je le, če je bilo izvirno sporočilo shranjeno ob архівiranju (`RawMessages:Enabled`, poglejte [Original Message Storage Settings](Setup.md#-original-message-storage-settings)); `get_email` ga poroča kot `hasRawMessage`. Odraža `GET /api/v1/emails/{id}/raw`.

**Parameterji:**

| Ime | Tip | Obvezno | Opis |
| --- | --- | --- | --- |
| `id` | integer | da | Arhiviran id e-pošte (iz `search_emails`). |

**Rezultat:** isti objekt kot `get_email_eml` s `kind` `raw`, `source` nastavljen na način, kako je bilo izvirno sporočilo zajeto (`imap`, `graph`, `eml-file` — natančne kopije — ali `serialized`, ponovno zapisano s parsirnikom, kjer so končnice vrnic normalizirane na CRLF) in `sha256` kontrolna vrednost (checksum), zabeležena ob архівiranju. Vrne iste napake kot `get_email_eml`, napako pa vrne, če za e-pošto ni shranjeno izvirno sporočilo.

Bajti se berejo od kjer koli so bili shranjeni — iz baze podatkov ali datoteke, odvisno od `RawMessages:Storage` ob архівiranju (poglejte [Storage backend](RawMessages.md#storage-backend)). S `RawMessages:FileSystem:VerifyChecksumOnRead` (privzeto `true`) se datoteka ponovno izračuna (re-hashed) pred vrnitvijo; sporočilo, č waarvan vsebina ne ustreza zabeleženi kontrolni vrednosti, bo zavrnjeno z napako namesto vrnitev.

## Omejitev števila zahtev (Rate limiting) {#rate-limiting}

MCP strežnik ima lastno politiko omejevanja števila zahtev s fiknim oknom (`"Mcp"`), razdeljeno po nezaščitenem prefiksu API-ključa (prvih 11 znakov tokena `ma_...`), pri odsotnosti nosilnega tokena pa se preklopi na IP naslov odjemalca. Budžet je konfiguriran prek `Mcp:RateLimitPerMinute` (privzeto `120` na minuto). Ko je budžet presežen, vrne endpoint `429 Too Many Requests` s celovito glavo `Retry-After`. To je neodvisno od politike omejitve števila zahtev REST API.

## Beleženje dostopa {#access-logging}

Vsak poziv orodja se beleži skozi isti `IAccessLogService` kot REST API,
z uporabniškim imenom lastnika in ustreznim tipom dostopa (`Search`, `Open`,
`Download`). Dejavnost MCP je zato vidna na strani **Logs** skupaj z
dejavnostjo REST API in web-UI, za enoten revizijski слід.

## Povezovanje agenta {#connecting-an-agent}

MCP strežnik uporablja standardni MCP Streamable HTTP transport, zato se lahko poveže vsak MCP klijent, ki podpira HTTP. URL je osnovna URL aplikacije plus `/mcp`, npr. `http://localhost:5179/mcp`, ključ API pa je zagotovljen prek glave `Authorization: Bearer ma_...`.

Za agente, konfigurirane prek standardnega MCP klijentskega JSON-a, je vnos v naslednjem oblikovan:

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

## Varnostna navodila za izpostavitev na internet {#security-guidance-for-internet-exposure}

MCP strežnik je zasnovan tako, da s privzetimi nastavitvami in spodnjo utrnitvijo arhivirana pošta ne more zapustiti sistema brez veljavnega API ključa za posameznika, ki je bil eksplicitno izdajen za določeno račun. Spodnje točke so **obvezno branje pred izpostavitvijo `/mcp` na internet**.

### Obvezno (preprečevanje izrabljanja / izteka podatkov) {#mandatory-block-exploitation-data-exfiltration}

1. **Le HTTPS.** TLS naj se zaključi na robu (reverse proxy). Bearer API ključ preko običajnega HTTP protokola je mogoče prislušati in ponovno uporabiti.
2. **Obdržite `Mcp:Enabled=false`, razen če ga dejansko uporabljate.** Ko je onemogočen, `/mcp` vrne `404` in nobena pot kodnega izvajanja ni dosegljiva; to je varna privzeta nastavitev in varno stanje za implementacije, ki ne potrebujejo dostopa AI agentov.
3. **Omejitev hitrosti (rate limiting) je prisiljena na končališču.** Politika `"Mcp"` (`Mcp:RateLimitPerMinute`, privzeto `120`) se uporablja na zmapiranem končališču `/mcp`. Razdeljena je po ne-secret predponu API ključa (prvih 11 znakov `ma_...` tokena) ali IP naslovu odjemalca, če bearer token ni prisoten. **Zmanjšajte `RateLimitPerMinute`** za izpostavitev na internet (npr. `30`–`60`) — klici MCP orodij so zahtevnejši od REST klikov in končališča ne bi smelo ostati z visokim proračunom, ko mu lahko dostopajo ne zaupanih odjemalci.
4. **S `X-Forwarded-For` reverse-proxyja morate previdno zaupati.** Omejevalnik hitrosti se preklopi na `RemoteIpAddress`, kar je pod proxyjem IP naslov proxyja, razen če je v `UseForwardedHeaders` poslodan pravi IP odjemalca. Zagotovite, da vaš proxy pravilno postavlja `X-Forwarded-For` in da ga lahko postavlja le vaš proxy; v nasprotnem primeru lahko napadalec lažno simulira razdelitev omejitve hitrosti.
5. **Ne postavljajte `/mcp` za CDN predvsem (cache).** MCP odgovori so JSON-RPC po zahtevi in jih nikoli ne smemo predvsem; predvsevanje bi tako prekinilo protokol in razkalo rezultat enega uporabnika drugemu. Označite `/mcp` kot nepredvsešljivo na robu (`Cache-Control: no-store`).

### Higiena API ključev (preprečevanje nepooblaščenega dostopa) {#api-key-hygiene-block-unauthorized-access}

6. **Izdajajte namenske, kratkotrajne ključe za MCP agente.** En `ma_...` ključ je veljaven tako za `/api` kot za `/mcp`; danes ni razgranitve po protokolih. Zato:
   - Ustvarite **poseben ključ za vsakega agenta** (ne uporabljajte ključa za skripto za poročanje za zunanega LLM agenta).
   - Vedno nastavite kratek `ExpiresAt` in ga rotirajte; ga takoj prekličite, ko je agent razstavljen.
   - Prednost dajte ključem, ki jih ima **omejen (ne-admin) uporabnik**, katerevi dodeljeni računi so le tisti, ki jih agent potrebuje. Admin ključ omogoča vpogled v *vse* e-poštne račune; omejen ključ omejuje obseg škode.
7. **S ključem API ravnavajte kot s geslo.** Gre za bearer凭证 (identifikacijsko sredstvo). Nikoli ga ne vključujte v kodo na strani odjemalca, ga nikoli ne beležite v dnevnike in ga nikoli ne oddajte v repozitorij. Shranite ga v MCP klijentovo skladišče skrivnosti in ga rotirajte ob sumu na kompromitacijo.
8. **Pregledajte dnevnike na strani Logs.** Vsak klic orodja je zabeležen v `AccessLogs` (`Search`, `Open`, `Download`) pod uporabnikom, ki ga lasti. Občasno pregledujte MCP dejavnost za nepričakovane vzorce (skokov volumena, branja izven delovnega časa, masovno prenosanje podatkov).

### Model zaupanja za vrnjene podatke (preprečevanje škode v nadaljnji fazi) {#trust-model-for-returned-data-block-downstream-harm}

9. **Vsebina e-pošte in priloge so ne zaupanja vredne vsebine**, točno tako, kot so prišle v predalo. Vsebujejo lahko złoživo programsko opremo, phishing povezave ali aktivni HTML/JavaScript.
   - `get_email` vrne `htmlBody` **raw in neizčisteno**. Ne prikazujte tega človeku brez čiščenja (DOMPurify / `HtmlSanitizer` / sandbox). Prikazovanje surovega arhiviranega HTML-ja je vektor za stored-XSS.
   - `get_attachment` vrne surove bajte z originalnim `contentType`. Ne odpirajte in ne izvajajte prilog samodejno; polje `contentType` obravnavajte kot ne zaupanja vredne metapodatke, ne kot varno navodilo.
   - `get_email_eml` in `get_email_raw` vrneta celotno sporočilo vključno z vsako prilogo. Slednja pravila veljajo za vsak njegov del.
10. **Ne dovolite agentu izvajanja zapisovalnih dejanj na podlagi vsebine e-pošte brez potrditve.** Złošivo e-poštno sporočilo lahko vsebuje besedilo za prompt-injection, namenjeno porabnemu LLM-ju. V agentov lastni logiki zahtevajte dovoljenja in potrditve — sistemski prompt ni meja varnosti.

### Priporočila za obrambo v več slojih (Defense-in-depth) {#defense-in-depth-recommendations}

11. **Omejite `/mcp` na mrežni plast**, ko je to izvedljivo (npr. dovolite le IP naslov / VPN obseg gostitelja agenta na reverse proxy), poleg avtentikacije z API ključem. To je najbolj učinkovita nadzorna medida za izpostavitev na internet.
12. **Zmanjšajte `Mcp:AllowAttachmentDownloads` na `false`**, če vaši agenti ne potrebujejo bajt prilog — iztek prilog je pot z največjim volumnom podatkov. Metapodatki (ime datoteke, tip vsebine, velikost) se še vedno vrnejo prek `get_email`.
13. **Zmanjšajte `Mcp:MaxAttachmentBytes`** na najmanjšo vrednost, ki jo vaši agenti dejansko potrebujejo (privzeto `10000000` ≈ 10 MB). To omejuje največji posamezen odgovor, ki ga lahko API ključ izvzame po zahtevi. Isto velja za `Mcp:MaxMessageBytes`, ali pa nastavite `Mcp:AllowMessageDownloads` na `false`, ko agenti ne potrebujejo celotnih sporočil.
14. **Zmanjšajte `Mcp:MaxResults` in `Mcp:DefaultPageSize`**, da omejite največji rezultat iskanja, ki ga lahko API ključ izvzame po zahtevi (privzeto `100` / `20`).
15. **Operativno spremljajte `AccessLogs` in zavračanja zaradi omejitve hitrosti** (`429`). Nenaden skok pri enem predponu ključa je signal za preklic tega ključa.
