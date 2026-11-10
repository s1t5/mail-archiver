# MCP Server (Model Context Protocol) {#mcp-server-model-context-protocol}

A Mail Archiver egy opcionális **MCP (Model Context Protocol) szervert** szállít, amely a költöztesett leveleket a Streamable HTTP transzporton keresztül teszi elérhetővé az AI ügynökök számára. Ez tükrözi a [REST API](API.md)-t: ugyanazok az adatok, ugyanaz a felhasználónkénti jogosultsági modell, ugyanaz az hozzáférés-naplózás — csak MCP eszközökként (tools) csomagolva a REST végpontok helyett, így egy LLM ügynök közvetlenül felfedezheti és hívhatja őket.

A cél az, hogy az AI ügynök a költöztesett leveleket **anélkül olvashassa el, hogy a postafalak hitelesítő adatait megmutatná vagy kommunikálnia kellene a posta szolgáltatóval.** Egy körbe olytható, felhasználónkénti API kulcs a lehetőséget biztosítja a *költöztetett архів olvasására*, nem pedig a *postafalak helyettesítésére* — ez csak a tulajdonos postafalak-jogosultságait örökli, nem biztosít írási hozzáférést, és azonnal visszavonható anélkül, hogy az alapértelmezett posta fiókkal kellene foglalkozni.

> ⚠️ **Az MCP szerver alapértelmezés szerint letiltva van.** A konfigurációban kifejezetten engedélyezni kell (lás: [Enabling the MCP server](#enabling-the-mcp-server)). Letiltva esetén a `/mcp` végpont `404 Not Found`-et térít vissza.

Az MCP szerver **alapvetően csak olvasható (read-only)**: eszközöket biztosít fiókok felsorolásához, mappák felsorolásához, üzenetek kereséséhez, üzenetek olvasásához és csatolmányok letöltéséhez. Soha nem hoz létre, nem módosít és nem töröl adatokat.

## Tartalomjegyzék {#table-of-contents}

- [Az MCP szerver engedélyezése](#enabling-the-mcp-server)
- [Hitelesítés](#authentication)
- [Transport és endpoint](#transport-and-endpoint)
- [Eszközök](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
  - [get_email_eml](#get_email_eml)
  - [get_email_raw](#get_email_raw)
- [Sebességkorlátozás](#rate-limiting)
- [Hozzáférési naplózás](#access-logging)
- [Agent csatlakoztatása](#connecting-an-agent)
- [Biztonsági útmutató az internetes kitettséghez](#security-guidance-for-internet-exposure)

## Az MCP szerver engrenedése {#enabling-the-mcp-server}

Az MCP szerver a `appsettings.json` `Mcp` szekciója alatt van konfigurálva (vagy az azonos környezeti változók alatt, pl. `Mcp__Enabled=true`):

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

| Beállítás | Alapértelmezett | Leírás |
| --- | --- | --- |
| `Enabled` | `false` | Fő kapcsoló. Ha `false`, akkor a `/mcp` végpont `404`-at ad vissza. |
| `AllowAttachmentDownloads` | `true` | Ha `false`, akkor a `get_attachment` hibát ad vissza a csatolási bájtok helyett. A csatolásokról szóló metaadatokat továbbra is visszatéríti a `get_email`. |
| `DefaultPageSize` | `20` | Oldalméret, amelyet a `search_emails` használ, ha a hívó elhagyja a `pageSize` paramétert. |
| `MaxResults` | `100` | A `search_emails` oldalméret felső határa; a nagyobb értékek korlátozásra kerülnek. |
| `MaxAttachmentBytes` | `10000000` | A maximális csatolás mérete (bájtban), amelyet a `get_attachment` base64 formátumban ad vissza közvetlenül; a nagyobb csatolásokat elutasítjuk. |
| `AllowMessageDownloads` | `true` | Ha `false`, akkor a `get_email_eml` és a `get_email_raw` hibát ad vissza az üzenet helyett. Egy teljes üzenet magával viszi a csatolásait, ezért **mind** ezen a kapcsolón, **mind** a `AllowAttachmentDownloads` beállítón `true`-nak kell lenni; ha bármelyiket kikapcsoljuk, mindkét eszköz használhatatlanná válik. |
| `MaxMessageBytes` | `10000000` | A maximális üzenetméret (bájtban), amelyet a `get_email_eml` és a `get_email_raw` közvetlenül ad vissza; a nagyobb üzeneteket elutasítjuk. |
| `RateLimitPerMinute` | `120` | Fix ablakos kérés-keret (request budget) API kulcsonként percenként (lásd: [Rate limiting](#rate-limiting)). |

## Hitelesítés {#authentication}

Az MCP szerver ugyanazokkal a **felhasználónkénti API kulcsokkal hitelesít, mint a REST API**, amelyeket bearer tokenként küldenek:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

Az API kulcsok a web felületen, az **API Keys** menüpont alatt jönnek létre és kezelhetők. Egy kulcs pontosan azt a jogosultságot örökli, amely a tulajdonos felhasználót illeti (a rendszergazda minden fiókot lát; egy korlátozott jogosultságú felhasználó csak a hozzá rendelt fiókat lát). A kulcsok az alapvető e-mail fióktól függetlenül visszavonhatók vagy lejárhatók. Lásd a [REST API authentication section](API.md#authentication) részletet a teljes kulcs-életciklusról és jogosultságmodellről — az MCP kulcsok nem stanál külön hitelesítő tárolóban.

Ha az MCP szerver le van engedélyezve, vagy nem található/érvénytelen bearer token, az endpoint megfelelően `404` / `401` választást ad vissza (soha nem irányít át a bejelentkezési oldalra).

## Transport és végpont {#transport-and-endpoint}

Az MCP szerver a **Streamable HTTP** transportot használ, és a következő helyen van rögzítve:

```
POST /mcp
```

**Stateless** módban fut (egy JSON-RPC kérés minden HTTP kéréshez), ami a javasolt mód olyan szerverek számára, amelyek nem igényelnek szerverről a kliens felé irányuló kéréseket, például sampling vagy elicitation esetén. A eszközök a standard MCP `tools/list` kérés segítségével fedezhetők fel, és a `tools/call` prostředvén hívhatók meg.

## Eszközök {#tools}

### list_accounts {#list_accounts}

Listázza azokat a posta-fiókokat, amelyekhez a jelenlegi API kulcs hozzáféréssel rendelkezik. `id`, `name`, `emailAddress`, `provider`, `isEnabled` és `lastSync` értékeket ad vissza.

**Paraméterek:** nincs.

**Eredmény:** fiók-objektusok tömbje. Használja a visszaadott `id` értéket a `list_folders` és `search_emails` funkciókkal együtt.

### list_folders {#list_folders}

Listázza egyetlen e-mail fiók mappatrécét, mappánkénti e-mail számlálókkal.

**Paraméterek:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `accountId` | integer | yes | Az e-mail fiók id (a `list_accounts` alapján). |

**Eredmény:** mappatermékek tömbje, amelyek mindegyike tartalmaz `name`, `fullPath`, `totalCount`,
`level` és rekurzív `children` mezőket. Üres listát adod vissza, ha a fiók nem
létezik, vagy a kulcs nincs jogosult hozzá.

### search_emails {#search_emails}

Archív e-maileket keres feladat szöveges lekérdezés, dátumtartomány, fiók, mappa és irány alapján.

**Paraméterek:**

| Név | Típus | Kötelező | Leírás |
| --- | --- | --- | --- |
| `q` | string | nem | Teljes szöveges keresési lekérdezés (tárgy, tartalom, küldő/cél). |
| `from` | string (ISO-8601) | nem | UTC dátum/idő szűrő (küldés ideje: nem korábban, mint `from`). |
| `to` | string (ISO-8601) | nem | UTC dátum/idő szűrő (küldés ideje: nem később, mint `to`). |
| `accountId` | integer | nem | Korlátozás egyetlen e-mail fiók idójára. |
| `folder` | string | nem | Korlátozás egyetlen mappa teljes elérési útjára (pl. `INBOX`). |
| `direction` | string | nem | `incoming` vagy `outgoing`. |
| `page` | integer | nem | 1-al kezdődő oldalszám (alapértelmezett `1`). |
| `pageSize` | integer | nem | Oldalméret; maximum `[1, MaxResults]`. `0` használja a `DefaultPageSize` értéket. |
| `sortBy` | string | nem | `sentdate` (alapértelmezett), `receiveddate`, `subject`, `from`, `to`. |
| `sortOrder` | string | nem | `asc` vagy `desc` (alapértelmezett `desc`). |

**Eredmény:** egy oldaltáblázatos objektum `items` (e-mail összefoglalók tömbje), `page`,
`pageSize`, `totalItems`, `totalPages` részekkel. Minden összefoglaló tartalmaz `id`, `accountId`,
`subject`, `from`, `to`, `sentDate`, `isOutgoing`, `hasAttachments` és
`folderName` értékeket. A visszakapott `id`-et adja át a `get_email` függvénynek a teljes tartalom és
csatolmánylista lekérése érdekében.

### get_email {#get_email}

Egyetlen archivált e-mail teljes tartalmát teszi lehetővé lekérdezni.

**Paraméterek:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | Az archivált e-mail azonosító (a `search_emails` alapján). |

**Eredmény:** egyetlen e-mail objektum minden összefoglaló mezőjével, valamint `cc`, `bcc`,
`receivedDate`, `messageId`, `textBody`, `htmlBody` és `attachments` (`{ id, fileName, contentType, size }` tömb) és `hasRawMessage` (annak jele, hogy
`get_email_raw` vissza tudja-e adni az eredetit). `null`-t ad vissza, ha az e-mail nem
létezik, vagy a kulcs nem jogosult a hozzá tartozó fiókhoz.

### get_attachment {#get_attachment}

Letölt egyetlen e-mail csatolmányt base64 blob formátumban.

**Paraméterek:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | Az archivált e-mail azonosító, amelyhez a csatolmány tartozik. |
| `attachmentId` | integer | yes | A csatolmány azonosítója (a `get_email` `attachments` alapján). |

**Eredmény:** egy objektum `id`, `emailId`, `fileName`, `contentType`, `size`
és `base64Data` (a nyers csatolmányбайt, base64-kódolva) értékekkel. A függvény hibát ritorz, ha `Mcp:AllowAttachmentDownloads=false`, ha a csatolmány meghaladja a
`Mcp:MaxAttachmentBytes` méretet, vagy ha a kulcs nem jogosult az e-mail
fiókjához.

### get_email_eml {#get_email_eml}

Egy teljes RFC 5322 üzenetet (`.eml`) ad vissza, amelyet a mentett mezőkből építت fel újra: a tárolt fejléc-adatok (`Received`, `Authentication-Results`, `X-Spam-*`, …), a törzsszövegek és csatolmányok. Minden archivált e-mailnél működik, de az eredmény nem byte-identikus az eredetivel, így a DKIM aláírások nem tudják hitelesíteni — ehhez használja a [`get_email_raw`](#get_email_raw) funkciót. Tükrözi az `GET /api/v1/emails/{id}/eml` értéket.

**Paraméterek:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | Az archivált e-mail az (az `search_emails` alapján). |

**Eredmény:** egy objektum `emailId`, `kind` (`eml`), `source` (`null`), `sha256`, `size`, `encoding` és `content` értékekkel. Az `content` az üzenet szövege, ha az `encoding` értéke `utf-8`, és az üzenet base64-kódolva, ha az `encoding` értéke `base64` (az olyan üzeneteknél, amelyek nem érvényes UTF-8, így egyetlen byte sem változik meg). Az eszköz hibát ritornul, ha `Mcp:AllowMessageDownloads=false`, ha az üzenet meghaladja a `Mcp:MaxMessageBytes` méretet, vagy ha a kulcs nem jogosult az e-mail fiókjához.

### get_email_raw {#get_email_raw}

A beantított eredeti üzenetet adja vissza pontosan úgy, ahogy azt megkapta, spamszűréshez, DKIM/SPF/ARC ellenőrzéshez vagy informatikai vizsgálatokhoz. Csak akkor érhető el, ha az eredeti üzenetet elmentették az archiváláskor (`RawMessages:Enabled`, lásd a [Original Message Storage Settings](Setup.md#-original-message-storage-settings) beállításokat); `get_email` azt jelzi, hogy `hasRawMessage`. Tükrözi a `GET /api/v1/emails/{id}/raw`-at.

**Paraméterek:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | Az archivált e-mail azonosító (a `search_emails` alapján). |

**Eredmény:** ugyanaz az objektum, mint a `get_email_eml`, `kind` `raw`, `source` beállítva ahhoz, ahogyan az eredetit rögzítették (`imap`, `graph`, `eml-file` — pontos másolatok — vagy `serialized`, amelyet a parser írt át, a sorvégek CRLF formátumra normalizálva), és `sha256` az archiváláskor rögzített ellenőrző összeg (checksum). Ugyanazokat a hibákat téríti vissza, mint a `get_email_eml`, és hibát jelez, ha az e-mailhez nem tároltak eredeti példányt.

A bájtok onnan kerülnek ki, ahol tárolták őket — az adatbázisból vagy egy fájlból, az archiváláskor alkalmazott `RawMessages:Storage` függvényében (lásd a [Storage backend](RawMessages.md#storage-backend) részt). A `RawMessages:FileSystem:VerifyChecksumOnRead` (alapértelmezett `true`) esetén a fájlt újra ellenőrizik (re-hash), mielőtt visszaadják; ha egy üzenet tartalma már nem egyezik a rögzített ellenőrző összeggel, akkor nem kapjuk vissza, hanem hibával elutasítják.

## Sebességkorlátozás {#rate-limiting}

Az MCP szerver saját, fix ablakos (fixed-window) sebességkorlátozási szabályzatával (`"Mcp"`) rendelkezik, amelyet a nem titkos API-kulcs előtag (a `ma_...` token első 11 karaktere) szerint osztanak fel, és amely a bearer token hiányában az ügyfél IP-címére tér vissza. A keret `Mcp:RateLimitPerMinute` segítségével konfigurálható (alapértelmezés szerint `120` percenként). Túllépés esetén az végpont `429 Too Many Requests` választాన్ని küld vissza `Retry-After` fejléc mellett. Ez független a REST API sebességkorlátozási szabályzatától.

## Naplózás {#access-logging}

Minden eszközhívás ugyanazon `IAccessLogService` keresztül kerül naplózásra, amely a REST API-t,
a tulajdonos felhasználónevével és a hozzá tartozó hozzáférési típussal (`Search`, `Open`,
`Download`) együtt. Az MCP tevékenység tehát a **Logs** oldalon látható a
REST API és a web-UI tevékenység mellett, egyetlen audit nyomonkövetés érdekében.

## Agent csatlakoztatása {#connecting-an-agent}

Az MCP szerver a szabványos MCP Streamable HTTP transzportot használ, így bármely HTTP-t támogató MCP kliens csatlakozhat hozzá. A URL az alkalmazás alap URL-je, plusz `/mcp`, pl. `http://localhost:5179/mcp`, az API kulcsot pedig a `Authorization: Bearer ma_...` header segítségével kell megadni.

A szabványos MCP kliens JSON-en keresztül konfigurált agenteknél egy bejegyzés így néz ki:

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

## Biztonsági útmutató az internetes kitettséghez {#security-guidance-for-internet-exposure}

Az MCP szerver úgy lett tervezve, hogy a beállított alapértelmezettekkel és az alábbi szigorításokkal az archivált levelek nem távozhatnak el a rendszerből anélkül, hogy egy érvényes, felhasználónkénti API-kulcs lenne hozzájuk rendelve, amelyet kifejezetten egy fiókhoz kiállítottak. Az alábbi pontok **kötelező olvasnivalók, mielőtt `/mcp`-t internetre terítik**.

### Kötelező (kiv exploitation / adatszivárgás megakadályozása) {#mandatory-block-exploitation-data-exfiltration}

1. **Csak HTTPS.** A TLS-végpontot a peremhatáron (reverse proxy) kezelje. A plain HTTP feletti bearer API-kulcs lehallgatható és újrafelhasználható.
2. **Tartsa fenn `Mcp:Enabled=false`-et, hacsak nem ténylegesen használja.** Ha letiltva van, `/mcp` `404`-at ad vissza, és semmilyen kódút nem érhető el; ez a biztonságos alapértelmezett és a biztonságos állapot azoknak a telepítéseknek, amelyek nem igényelnek AI-agent hozzáférést.
3. **A végponton sebességkorlátozás (rate limiting) van érvényesítve.** A `"Mcp"` szabályzat (`Mcp:RateLimitPerMinute`, alapértelmezett `120`) a térképezett `/mcp` végpontra kerül alkalmazásra. Ezt a nem titkos API-kulcs előtag (az `ma_...` token első 11 karaktere) vagy a kliens IP alapján különítik el, ha nincs jelen bearer token. **Csökkentse a `RateLimitPerMinute` értékét** az internetes kitettség esetén (pl. `30`–`60`) — az MCP eszközhívások nehezebbek, mint a REST hívások, és a végpontnak nem szabad magas keretedben maradnia, ha nem megbízható kliensek érhetik el.
4. **A reverse-proxy `X-Forwarded-For`-t óvatosan kell bízni.** A sebességkorlátozó a `RemoteIpAddress` értékre tér vissza, ami proxy esetén a proxy IP-je, hacsak nem táplálják a `UseForwardedHeaders`-nek a valódi kliens IP-jét. Győződjön meg róla, hogy a proxije helyesen állítja be a `X-Forwarded-For`-öt, és hogy csak az Ön proxije állhatja be azt; különben egy támadó hamisíthatja a sebességkorlátozási partíciót.
5. **Ne helyezzen `/mcp`-ot CDN cache mögé.** Az MCP válaszok per-request JSON-RPC típusúak, és soha nem szabad őket cache-elni — a cache-elés megszakítaná a protokoll működését, és egy felhasználó eredményét kisziválogná egy másikéhoz. Jelölje meg a `/mcp`-et cache-elhetetlenként a peremhatáron (`Cache-Control: no-store`).

### API-kulcs higiénia (nem engedélyezett hozzáférés megakadályozása) {#api-key-hygiene-block-unauthorized-access}

6. **Kifejezetten MCP agentekhez rendeljen rövid élettartamú kulcsokat.** Egyetlen `ma_...` kulcs érvényes mind a `/api`, mind a `/mcp` esetében; ma még nincs protokollonkénti körkör (scope). Ezért:
   - Hozzon létre **külön kulcsot minden agentehez** (ne használjon jelentőscript kulcsot külső LLM agentekhez).
   - Mindig állítson be rövid `ExpiresAt`-t, és rotálja azt; vonja vissza azonnal, ha az agentet leállítják.
   - Preferálja a **korlátozott (nem admin) felhasználóhoz** tartozó kulcsokat, amelyeknek az egyetlen hozzárendelt fiókjaik azok, amikre az agentnek szüksége van. Egy admin kulcs minden levelezési fiókhoz hozzáférést biztosít; egy korlátozott kulcs csökkenti a károk körét (blast radius).
7. **Kezelje az API-kulcsot úgy, mint egy jelszót.** Ez egy bearer hitelesítő adat. Soha ne ágyazza be kliensoldali kódba, soha ne logolja, és soha ne committeleyin egy repo-ba. Tegye a kulcsot az MCP kliens titkos tárhelyébe (secret store), és rotálja, ha gyanús merénylet merül fel.
8. **Audit a Logs oldalon.** Minden eszközhívás rögzítve van a `AccessLogs`-ban (`Search`, `Open`, `Download`) a tulajdonos felhasználó alatt. Időszorosan ellenőrizze az MCP aktivitást a váratlan mintázatok tekintetében (mennyiségi ingadozások, munkaidőn kívüli olvasások, tömeges letöltések).

### Visszatérő adatok bizalmi modellje (folyamatban történő kár megelőzése) {#trust-model-for-returned-data-block-downstream-harm}

9. **Az e-mail testek és csatolmányok megbízhatatlan tartalmak**, pontosan ugyanazok, amilyenek voltak, amikor megérkeztek a postafiókba. Tartalmazhatnak kártékony szoftvereket, phishing linkeket vagy aktív HTML/JavaScript-et.
   - A `get_email` `htmlBody` formátumot **nyers és tisztázatlan (unsanitized)** állapotban ad vissza. **Ne** jelenítse megjelenítve ember számára tisztítás nélkül (DOMPurify / `HtmlSanitizer` / sandbox). A nyers archivált HTML megjelenítése tárolt XSS (stored-XSS) vektor.
   - A `get_attachment` a nyers byteket az eredeti `contentType`-lel együtt adja vissza. **Ne** nyissa meg vagy futtassa ki automatikusan a csatolmányokat; kezelje a `contentType` mezőt megbízhatatlan metadatként, ne pedig biztonságos utasításként.
   - A `get_email_eml` és a `get_email_raw` a teljes üzenetet tartalmazza minden csatolmányával együtt. Ugyanezek a szabályok érvényesek minden egyes részre.
10. **Ne hagyja, hogy az agent írási műveleteket végezzen az e-mail tartalma alapján megerősítés nélkül.** Egy rosszindulatú e-mail prompt-injection szöveget tartalmazhat, amely a fogyasztó LLM felé irányul. Kényszerítse a jogosultságokat és a megerősítést az agent saját logikájában — a rendszer prompt nem biztonsági határvonal.

### Mélységben kialakított védelem (Defense-in-depth) ajánlások {#defense-in-depth-recommendations}

11. **Korlátozza a `/mcp`-öt a hálózati rétegen**, ahol lehetséges (pl. csak az agent host IP-jét / VPN tartományát engedélyezze a reverse proxy esetén), az API-kulcs hitelesítés mellett. Ez a leghatékonyabb egyetlen kontroll az internetes kitettség esetén.
12. **Csökkentse a `Mcp:AllowAttachmentDownloads` értékét `false`-re**, ha az agenteknek nincs szükségük a csatolmány bytekra — a csatolmányok kiszivárgása a legnagyobb mennyiségű adatátvitelű útvonal. A metaadatok (fájlnév, típus, méret) továbbra is visszatérnek a `get_email` prostředén keresztül.
13. **Csökkentse a `Mcp:MaxAttachmentBytes` értékét** a legkisebb értékre, amire az agentek ténylegesen szükségük van (alapértelmezett `10000000` ≈ 10 MB). Ez korlátozza a legnagyobb egyes választ, amelyet egy API-kulcs egy kérésre lekérhet. Ugyanez vonatkozik a `Mcp:MaxMessageBytes`-re is, vagy állítsa a `Mcp:AllowMessageDownloads` értékét `false`-ra, ha az agenteknek nincs szükségük a teljes üzenetekre.
14. **Csökkentse a `Mcp:MaxResults` és `Mcp:DefaultPageSize` értékeket**, hogy korlátozza a legnagyobb keresési eredményt, amelyet egy API-kulcs egy kérésre lekérhet (alapértelmezett `100` / `20`).
15. **Operatív módon figyelje a `AccessLogs`-at és a sebességkorlátozási elutasításokat** (`429`). Egyetlen kulcs előtag hirtelen növekedése jelez, hogy vonja vissza azt a kulcsot.
