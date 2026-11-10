# 📤 Audit Data Export {#audit-data-export}

[← Visszamenut a dokumentáció indexhez](index.md)

## 📋 Áttekintés {#overview}

Az audit adatexport táblázatos tömegadat-csomagokat (ZIP `INDEX.XML`-val + CSV táblákkal + index DTD-vel) hoz létre a meglévő archívumból. Ezek a csomagok a kialakított indexstruktúrát követik az adati média átadásához, és közvetlenül importálhatók a gyakori külső audit- és elemző eszközök számára, amelyek támogatják ezt az indexformátumot.

A funkció egy **külön oldalként érhető el a Logs oldalon**, és kizárólag admin felhasználók számára rendelkezésre áll. Minden exportfutást rögzít a hozzáférési naplóban, így az exporttörténet revízióálló.

## 🧭Hozzáférés {#access}

1. Jelentkezzen be admin felhasználóként
2. Nyissa meg a **Logs** oldalt
3. Kattintson a jobb felső sarokban található **Audit Data Export** gombra

A nem admin felhasználók nem látják a gombot, és nem tudják meghívni egyik export végpontot sem (a szerver elutasítja őket).

## 📦 Export csomag struktúra {#export-package-structure}

A generált ZIP fájl neve `audit-export-<timestamp>.zip`, és a következőket tartalmazza:

| Fájl | Tartalom |
|---|---|
| `INDEX.XML` | Index fájl, amely leírja az adatellátót, a médiát és az összes táblázat/oszlop definíciót |
| `index.dtd` | A DTD, amire az index fájl hivatkozik; az importáló eszközök igénylik a validáláshoz |
| `emails.csv` | Egy sor minden archivált e-mailhez a kiválasztott időszakban/postafiókban |
| `attachments.csv` | Opcionális; egy sor minden exportált e-mail csatolmányához |

### `emails.csv` tábla {#table-emailscsv}

Oszlopok (nincs fejléc sor; az oszlopnevek a `INDEX.XML`-ből származnak, mint ez a formátumnál szokásos):

| Oszlop | Leírás |
|---|---|
| Id | Belső e-mail ID (numerikus) |
| MessageId | E-mail Message-ID header |
| SentDate | Küldési dátum, ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`) |
| ReceivedDate | Beérkezési dátum, ISO 8601 UTC |
| IsOutgoing | `1` = kimenő, `0` = beérkező |
| From | Feladó |
| To | Címzett(ek) |
| Cc | CC címzett(ek) |
| Bcc | BCC címzett(ek) |
| Subject | Tárgy |
| FolderName | Melyik mappába lett archiválva az e-mail |
| HasAttachments | `1` = csatolásokkal, `0` = csatolások nélkül |
| AttachmentCount | Csatolmányok száma (numerikus) |
| AccountEmail | Az archiváló postafiók e-mail címe |

### `attachments.csv` tábla {#table-attachmentscsv}

| Oszlop | Leírás |
|---|---|
| EmailId | Azon e-mail belső ID, amelyhez a csatolmány tartozik (kapcsolódási kulcs a `emails.csv`-hez) |
| FileName | Csatolmány fájlneve |
| ContentType | MIME típus |
| Size | Méret byte-okban (numerikus) |
| Sha256 | A tartalom SHA-256 hash értéke (a tartalom-adressált csatolás tárolóból) |

### CSV konvenciók {#csv-conventions}

- Kódolás: UTF-8 **BOM nélkül** (`UTF8`-ként deklarálva a `INDEX.XML`-ban)
- Oszlopodozó: `;`
| Textüdvözlő: `"` (duplázva az escapeléshez)
- Sorodozó: `\n` (explicit módon deklarálva a `INDEX.XML`-ban)
- Dátumok: ISO 8601, az időpontok a konfigurált megjelenítési időzónából UTC-be konvertálva
- Nincs fejléc sor; nincs lokalizált számérték (a numerikus oszlopok egyszerű egész számokat tartalmaznak)

## 🖥️ Az Export oldal használata {#using-the-export-page}

### Formás mezők {#form-fields}

1. **From / To date** - Exportálandó időszak (kötelező, a maximális intervallum konfigurálható, alapértelmezett 10 év)
2. **Mailbox** - A export korlátozása egyetlen postafiókra, vagy az összes postafiók exportálása
3. **Attachment metadata** - Ha be van pipálva, a `attachments.csv` hozzáadódik a csomaghoz
4. **Data supplier name / location / comment** - Beírás a `INDEX.XML` `DataSupplier` blokkjába; az `AuditExport` konfigurációs részből előre kitöltött, de exportonként szerkeszthető

### Feladat életciklusa {#job-lifecycle}

1. A **Start Export** megnyomásával ellenőrzésre kerül az időszak és a postafiók, létrehozódik egy feladat, majd visszatér a oldalra
2. A feladat a háttérben fut (a sorosított exportok egymás után kerülnek feldolgozásra); a feladat táblázat élő haladást mutat (feldolgozott/összes e-mail egy progress bar segítségével) automatikus felülemelés (polling) révén
3. Amikor az export befejeződik, a feladat sorában megjelenik a **Download** gomb
4. A futó vagy sorosított feladatok **lemondhatók**

### Export történet {#export-history}

A űrlap alatti történet táblázat a legutóbbi exportokat sorolja fel időbélyeggel, felhasználóval, időszakkal, postafiókkal, e-mail számmal, státusszal és fájlmérettel együtt. A történet **megmarad az adatbázisban** (`AuditExportJobs` tábla), így az alkalmazás újraindítása után is megmarad, és revízióálló.

A letöltési fájlokat egy konfigurálható megőrzési időszakra tartjuk fenn (alapértelmezetten 30 nap, lásd `AuditExport__RetentionDays`); ezután a ZIP fájlokat a napi tisztítás törli, de a történet sorok megmaradnak.

> 📝 **Megjegyzés**: Az export fájlok az alkalmazás konténerén belül tárolódnak. Ha hosszú távon meg akarja tartani őket, töltse le őket, és tárolja az audit bizonyítékok tárolójában.

## 🔐 Auditálás {#auditing}

Minden export két új típusú hozzáférési napló bejegyzést készít: **Audit Data Export**:

1. **Start entry** - amikor az export a sorba kerül (feladat ID, időszak, postafiók, opciók JSON formátumban)
2. **Result entry** - amikor az export befejeződik, elhibásodik vagy lemondásra kerül (feladat ID, időszak, postafiók, e-mail szám, eredmény és csomag mérete JSON formátumban)

A bejegyzések a Logs oldalon jelennek meg, mint minden egyéb hozzáférési napló bejegyzés, és az új típus alapján szűrhetők. A típus szűrő legördülő menüje automatikusan felismeri őket.

## ⚙️ Konfiguráció {#configuration}

A `AuditExport` rész a `appsettings.json`-ban (a környezeti változók a kettő aláhúzásos szintaxist használják, pl. `AuditExport__RetentionDays`):

| Beállítás | Leírás | Alapértelmezett |
|---|---|---|
| `DataSupplierName` | Alapértelmezett érték az adatellátó név mezőhöz | üres |
| `DataSupplierLocation` | Alapértelmezett érték az adatellátó hely mezőhöz | üres |
| `Comment` | Alapértelmezett érték a megjegyzés mezőhöz | üres |
| `OutputDirectory` | Címke a generált ZIP fájlok számára (relatív utak az app content root-hoz viszonyítva) | `exports/audit` |
| `RetentionDays` | Napok a befejezett export fájlok törléséig | `30` |
| `MaxRangeYears` | Maximális megengedett intervallum az időszak kezdő- és vég dátuma között | `10` |

## 🗄️ Adatbázis {#database}

Az export történet a `mail_archiver."AuditExportJobs"` táblában tárolódik (a `MigrateV2609_1` migráció által létrehozott, amely indításkor automatikusan alkalmazódik). E funkció nem módosít semmilyen meglévő táblát.

## 🔍 Hibaelégítés {#troubleshooting}

| Probléma | Ok / Megoldás |
|---|---|
| Hiányzó Download gomb újraindítás után | Az export fájlok a konténerben élnek; ha a konténer újracréálták vagy a megőrzési időszak lejárt, a ZIP eltűnik, de a történet sor megmarad. Futtassa újra az exportot. |
| "The period must not exceed X years" | Csökkentse az időszakot, vagy emelje a `AuditExport__MaxRangeYears` értékét. |
| A feladat hosszú ideig *Running* állapotban marad | A nagy archívumokat 1000 e-mailes csomagokban küldik ki; a haladást folyamatosan frissítik. A nagyon nagy időszakok eltarthatnak — ellenőrizze a feldolgozott/összes számlálót. |
| Az importáló eszköz kódolási problémákat jelez | A csomag UTF-8, BOM nélkül, deklarált `UTF8` flaggel és explicit sorodozóval rendelkezik; győződjön meg róla, hogy az eszköz olvassa a adatfájlok mellett található `INDEX.XML`-at (csomagolja ki a teljes ZIP-et, ne nyissa meg a fájlokat egyenként a ZIP-en belül). |

## 🔗 Kapcsolódó dokumentáció {#related-documentation}

- [Access Logging](Logs.md)
- [Installation, Setup and Parameters](Setup.md)
