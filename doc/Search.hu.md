# 🔍 Mail Search {#mail-search}

[← Vissza a dokumentáció indexhez](index.md)

## 📋 Áttekintés {#overview}

A keresés az e-mail archívum szíve. A Mail Archiver a levelezett levelezése **teljes szövegét** — tárgy, tartalom, feladó és címzettek — keresi az összes, amelyhez hozzáférése van, és millisekunder alatti választ ad, még több millió üzenetből álló archívumok esetén is.

Ez az útmutató le covers a keresőfelületet, a lekérdezési szintaktikát, a szűrőket, a rendezést, az oldaltűzést, valamint azt, mit tegyen, ha egy eredménykészletre cselekednie kell.

> **ℹ️ A kereső csak azt látja, amit Ön is lát.** Az eredményeket a felhasználói jogosultságok szűrik. A nem adminisztrátor felhasználó csak olyan leveleket talál, amelyek az Ön számára hozzárendelt postafiókokból származnak — a korlátozást magában a lekérdezésben alkalmazza a rendszer, nem pedig a felületen.

## 🚀 A keresés elérése {#accessing-search}

1. Jelentkezzen be, és válassza az **Archive** lehetőséget a főmenüben
2. A keresősáv a találati lista felett helyezkedik el

A keresőkifejezés nélküli Archívum megnyitása a legfrissebb e-maileket listázza, a legújabb elsőként. Innen böngészhet, vagy szűrőkkel szűkítheti a találatokat.

## 🎯 A keresőfelület {#the-search-interface}

| Mező | Funkció |
|---|---|
| **Search Term** | Teljes szövegű lekérdezés — lásd alább a [Query Syntax](#-query-syntax) részt |
| **From** | Csak ezen a dátumon vagy utána érkezett e-mailek |
| **To** | Csak ezen a dátumon vagy előtte érkezett e-mailek |
| **Direction** | `All`, `Incoming` vagy `Outgoing` |
| **Account** | Korlátozás egyetlen e-mail fiókra |
| **Folders** | Korlátozás egyetlen mappára a mappafa segítségével |
| **Search** | A lekérdezés futtatása |
| **Reset** | Minden szűrő törlése |

A keresőkifejezés az **előre meghatározott mezőkön** (tárgy, tartalom, feladó és címzettek) (`From`, `To`, `Cc`, `Bcc`) keres.

### A mappafa {#the-folder-tree}

A mappák a bal oldalon **hierarchikus fa** formájában jelennek meg, tükrözve az eredeti postafiók mappastruktúráját — beleértve a beágyazott mappákat is. Kattintson egy mappára a keresés korlátozásához, és használja a össze-/szétfűzés funkciót a fa elrejtéséhez, ha szüksége van a képernyőtérre.

A mappa kiválasztása nem változtatja meg a keresőkifejezést; a kettő kombinálódik.

## 🧩 Query Syntax {#query-syntax}

A szintaxis tudatosan egyszerű. Az alábbiakban csak az szerepel, amit az alkalmazás ténylegesen ért.

### Egyszerű szavak — és miért működnek a prefixek {#plain-words-and-why-prefixes-work}

Írjon be egy vagy több szót; mindegyiknek jelen kell lennie (egy **AND** keresés):

```
invoice january
```

Talál olyan e-maileket, amelyekben a keresett mezőkben szerepel egyszerre a `invoice` és a `january`.

> **💡 A részleges szavak automatikusan találatosak.** Minden szót **prefixként** kezelünk, így a `isenb` keresés megtalálja a `isenboeck` és a `isenböck` kifejezéseket is, a `rech` pedig a `Rechnung` mellett a `Rechnungen` kifejezést is. Nem kell külön wildcards karaktert használnia — nincs is ilyen beírható. Ez azt is jelenti, hogy egy nagyon rövid kifejezés rengeteg találatot is hozhat; lásd a [Performance](#-performance-and-large-archives) részt.

A keresés **nem érzékeny a kis- és nagybetűre**. A `Invoice`, `invoice` és `INVOICE` azonos lekérdezés.

### Pontos kifejezések — idézőjelek {#exact-phrases-quotes}

Használjon dupla idézőjeleket a szöveg körül, ha azt szeretné, hogy a szavak **egymás mellett** álljanak, ebben a sorrendben:

```
"important meeting"
```

Idézőjelek nélkül a `important meeting` egy olyan e-mailre is rátalálna, ahol a két szó független mondatokban szerepel. Idézőjelek esetén csak a szomszédos kifejezés találatos.

### Mezőre specifikus keresés {#field-specific-search}

Egy kifejezés elé tegyen egy mezőnevest és egy kettőspontot, ha **csak** abban a mezőben keresni szeretne:

| Szintaxis | Keres ebben |
|---|---|
| `subject:invoice` | Csak a Subject mezőben |
| `from:john` | Csak a Sender mezőben |
| `to:example.com` | Csak a Recipients mezőben |
| `body:important` | Csak a Body mezőben |

A mezőnevek nem érzékenyek a kis- és nagybetűre (a `Subject:` is működik), és mindegyik kombinálható idézőjelekkel egy kifejezéshez:

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ Négy mező támogatott:** `subject`, `body`, `from`, `to`. A `cc:someone` vagy `bcc:someone` írása **nem** mezőre specifikus keresés — a kifejezést sima szövegként kezeli, és minden mezőben megkeresi. Ha egy olyan címzettet keres, aki csak a Cc-ben volt, keresse az e-mail címet vagy nevét prefix nélkül.

### Minden egyes 요소 kombinálása {#combining-everything}

A szűrők és a kifejezések összeadódnak. Ez a lekérdezés olyan e-maileket talál, amelyek bármely biztosítóvállalkozótól származnak, a tartalomban a "car insurance" pontos kifejezés szerepel, a tárgyban pedig az "invoice" szó.

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

Gyakori kombinációk:

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### Az elhanyagolt karakterek {#characters-that-are-ignored}

A lekérdezés elemzője eltávolítja a `& | ! ( ) : *` karaktereket az egyes kifejezésekből, mert ezek jelentéssel bírnak az alapvető teljes szövegkereső motorban. Ha a keresése nem hoz váratlan eredményt, ellenőrizze, hogy egy speciális karakter okozta-e a problémát.

## 📊 Rendezés és oldaltűzés {#sorting-and-paging}

### Rendezés {#sorting}

Kattintson egy oszlopfejre a rendezéshez. Rendezhető oszlopok:

| Oszlop | Rendezés alapján |
|---|---|
| **Subject** | Tárgy, ábabeti sorrendben |
| **From** | Feladó |
| **To** | Címzettek |
| **Date** | Küldés dátuma |

Ha ugyanarra a fejre kattint újra, a rendezés iránya megváltozik. Az alapértelmezett a **legújabb első**.

### Oldalméret {#page-size}

Válassza ki, hány találatot jelenítsen meg egy oldalon:

**20** (alapértelmezett) · **50** · **75** · **100** · **150**

Egy nagyobb oldal több kontextust mutat, de tovább tart a megjelenítése. Használja a **Previous** / **Next** vezérléseket az oldalak közötti navigációhoz; a találatok száma jelzi, hogy összesen hány e-mail felelt meg a keresésnek.

> **ℹ️ Az Ön időzónája érvényes a megjelenített dátumokra.** A megjelenített időpontokat az UTC-ről az Ön által beállított időzónába konvertáljuk, így a látott dátum megegyezik a helyi naptárával — ez hasznos, ha éjfél körüli időszakban keres.

## 👁️ Dimenziók a találatokkal {#working-with-results}

Minden sor mutatja a fiókot, a feladót, a tárgyat, a dátumot, és egy **📎 ceruza (paperclip) ikont**, ha az e-mail csatolmányokat tartalmaz. A ceruza csak egy jelző — nem pedig egy szűrő.

Kattintson a **Details** gombra egy e-mail megnyitásához:

- Teljes tartalom (HTML vagy plain text)
- **Attachments**, felsorolva a lehetőséggel, hogy mindegyiket letölthesse
- **Download as EML** — a teljes üzenet, más helyre való használatra
- **Restore** — az e-mail vissza Másolása egy élő postafiókba

Lásd a [Mailbox Migration](MailboxMigration.md) részt az e-mailek postafiókba való visszaállításához.

## ✅ Több találat egyidejű kezelése {#acting-on-many-results-at-once}

A találatok tömegesen kiválaszthatóak és feldolgozhatóak. Kapcsolja be a **Selection Mode**-ot a jelölőnégyzetek megjelenítéséhez, majd:

- **Select All** / **Deselect All** az aktuális oldalhoz
- **Select all {n} matching** — minden találat a jelenlegi keresésből az összes oldalon (csak akkor jelenik meg, ha a találatok több oldalon oszlanak)
- **Copy Selected** — visszaállítás egy postafiókba
| - **Export Selected** — MBOX vagy EML formátumban
- **Delete Selected** — törlés az archívumból

A számláló mutatja a *"{selected} of {total} selected"* értéket a munka során.

### A kiválasztási korlát {#the-selection-limit}

> **⚠️ Egyszerre maximum 250 e-mail választható ki** (ez `Selection:MaxSelectableEmails`-on keresztül konfigurálható). Ezen túl a felület azt jelzi: *"Maximum selections reached (250)"* és **Too Many — Split Required**.

Ez a korlát azért van, mert minden műveletnek tartalmaznia kell az érintett üzeneteket. A *"Split larger operations into multiple batches"* felugró üzenet a javasolt megoldás: dolgozzon a találatokon 250 vagy annál kevesebb csoportokban.

A **Select all {n} matching** is ugyanazt a korlátot tartja. Ha a keresés több e-maillel talál, mint amennyit a korlát megenged, az elsőket választja ki a jelenlegi rendezési sorrendben, és megjeleníti a *"Maximum selections reached"* üzenetet. Szűkítse a keresést (dátumtartomány, mappa, fiók), hogy célba érjen a maradékkal.

Ha megpróbálja túllépni, azt írja: *"Too many emails selected. Maximum allowed is 250 emails. Please reduce your selection or split into multiple operations."*

### Egy szűrő minden találatának törlése {#deleting-every-match-of-a-filter}

A *Reset* mellett található **Delete all matches** gomb minden olyan e-mailt töröl, amely megfelel a jelenlegi keresésnek, nem csak az aktuális oldalt vagy a kiválasztottakat. Ez csak adminisztrátorok és önkezelők számára elérhető, és háttérfolyamatként fut, így a 250 e-mailes kiválasztási korlát nem érvényes.

A megerősítő ablak mutatja, hány e-mail törölhető, és hányat zárolja a megőrzési szabályzat (retention policy). Be kell írnia ezt a számot a megerősítéshez. A szerver újra kiszámolja, és megszakítja a folyamatot, ha közben megváltozott a találati készlet, például egy szinkronizálás miatt.

A zárolt e-mailokat ez a művelet soha nem törli el; ezeket kihagyottként jelentik a folyamat státuszában. `DeletionPolicy:MaxBulkDeleteEmails` (alapértelmezett 100,000) korlátozza egy folyamatot. Ha egy szűrő több e-mailre talál, mint amennyit engedélyez, elutasítják és kérik a szűkítését.

A folyamat megjelenik a [Background Jobs](BackgroundJobs.md) oldalon, és ott törölhető le.

### Közvetlen feldolgozás vs. háttérfolyamat {#direct-processing-vs-background-job}

A **Copy** és **Export** műveleteknél választhatja ki, hogend fut a munka:

| Lehetőség | Viselkedés |
|---|---|
| **Process on this page (wait for completion)** | A böngésző vár; jó néhány e-mail esetén |
| **Process in background (monitor separately)** | Folyamatként fut; lezárhatja a böngészőt |

Az exportálások mindig háttérben futnak:

> *"Az exportálás a háttérben fog futni, és a folyamat befejeződését követően letöltheti az eredményt a Jobs oldalon."*

A futó és befejezett folyamatok megtalálhatók a [Background Jobs](BackgroundJobs.md) oldalon.

> **⚠️ A törlés végleges.** A megerősítés egyértelmű: *"Are you sure you want to permanently delete {n} selected email(s)? This action cannot be undone and each deletion will be logged for audit purposes."* A törléseket rögzítik a hozzáférési naplókban.

## ⚡ Teljesítmény és nagy archívumok {#performance-and-large-archives}

A keresést PostgreSQL teljes szövegkereső index támogatja, így a gyors és lassú keresés közötti különbség általában abban rejlik, mennyire **szűkítő** (selective) a lekérdezés.

**Teendők:**

- Adj meg egy **dátumtartományt**, ha körülbelül tudja, mikor érkezett az e-mail — ez a leghatékonyabb szűrés
- Korlátozd egy **fiókra** vagy **mappára**, ha tudja, hol található az e-mail
- Használj **több szót** egyetlen nagyon rövid szó helyett
- Használj mezőre specifikus keresést (`subject:`, `from:`), hogy kizárja a nem kapcsolódó találatokat

**Kerüld:**

- Egyetlen rövid prefixet, mint például a `re` — mint prefix keresés, ez az archívum hatalmas részét megtalálja
- Nagyon széles dátumtartományokat egy gyakori szóval kombinálva
- Túl sok oldal böngészését, amikor inkább szűkítenél a keresést

> **💡 Ha a keresés lassúnak tűnik, a válasz általában a dátumtartományban rejlik.** Egy hónapra való korlátozás gyakran egy több másodperces keresést pillanatnyiá tesz, mert az index átugorhatja az archívum nagy részét.

## ❓ Hibaelhárítás {#troubleshooting}

| Tünet | Ok | Teendő |
|---|---|---|
| Nincsenek találatok, pedig várnék | Túl specifikus keresőkifejezés vagy elírás | Próbáljon kevesebb szót használni; ellenőrizze a helyesírást |
| `cc:name` nem talál semmi hasznosat | `cc` nem támogatott mező | Keresse a nevet prefix nélkül |
| Egy speciális karakter megszakítja a lekérdezést | A `& \| ! ( ) : *` karaktereket eltávolítják | Távolítsa el vagy helyettesítse a karaktert |
| Túl sok találat | Gyakori szó vagy rövid prefix | Adj hozzá dátumtartományt vagy mezőre specifikus keresést |
| *"Too Many — Split Required"* | Több mint 250 e-mail van kiválasztva | Csökkentse a kiválasztást vagy bontsa meg a műveletet |
| Hiányoz egy export | Háttérfolyamatként fut | Ellenőrizze a [Background Jobs](BackgroundJobs.md) részt |
| A találatok úgy tűnnek, mintha hiányoznának | Felhasználói jogosultságok | Erősítse meg, hogy a postafiók hozzá van rendelve az Ön felhasználójához |
| A dátumok eltolódnak | Időzóna konverzió | Ellenőrizze az időzóna beállítását |

## 📚 Kapcsolódó dokumentáció {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — találatok visszaállítása postafiókba
- [Background Jobs](BackgroundJobs.md) — exportálások és visszaállítások figyelése
- [Access Logging](Logs.md) — a keresések és törlések audit nyomkövetése
- [REST API](API.md) — ugyanaz az archívum az API-n keresztül
- [MCP Server](MCP.md) — keresés AI ügynökkel
- [Retention Policies](RetentionPolicies.md) — amikor az e-mailek távoznak az archívumból
