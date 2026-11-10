# 📥 Levél szerinti levél beimportálása a Web UI-ban (EML / MBox) {#importing-mail-in-the-web-ui-eml-mbox}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

A [parancssori felület](CLI.md) és a [CSV fiók importálás](Account-Import.md) mellett a Mail Archiver képes meglévő levél archivumok **webes felületen keresztül** is importálni. Két feltöltési űrlap áll rendelkezésre:

| Űrlap | Elfogadott fájl | Eredmény |
|------|---------------|--------|
| **Import MBox** | `.mbox`, `.mbx` | A fájl minden üzenete **egyetlen**, Ön által választott mappába kerül beimportálásra |
| **Import EML** | `.zip`, amely tartalmaz `.eml` fájlokat | A **ZIP fájlon belüli mappastruktúra** határozza meg, hogy az egyes üzenetek hová kerülnek |

Mindkét űrlap egy meglévő Mail Archiver fiókba archiválja az üzeneteket, mindkét folyamat **háttérfolyamatként** fut, és mindkét folyamat **automatikusan kihagyja a duplikáltakat** — a ugyanaz fájl kétszeres feltöltése nem létrehoz második példányt.

> 💡 **Tipp**: Ha az archivum már fájlként van a szerveren, a [CLI](CLI.md) használata javasolt. Ez elkerüli a böngészős feltöltést, és jobb választás több gigabájtos archivumok esetén.

## 🧭 Elérhetőség {#access}

1. Nyissa meg a **Mail Accounts** részt a főmenüben.
2. Használja a fiókok felett található **Import** legördülő menüt, és válassza az **Import MBox** vagy az **Import EML** lehetőséget.

Az űrlapok **adminisztorok** (összes fiók esetén) és **önkezelők** (az ő nevükre másolt fiókok esetén) számára érhetőekek el; lássee [User Management](UserManagement.md#-user-account-permissions). Csak a **kijelölt (enabled)** fiókok jelenhetnek meg import célként — a kikapcsolt (disabled) fiókot nem lehet kiválasztani.

## 📨 Import MBox {#import-mbox}

Az MBox a legtöbb e-mail kliens által használt formátum az exportált mappákhoz, valamint a `mbox` stílusú mentésekhez.

| Mez | Jelentés |
|-------|---------|
| **MBox File** | A feltöltendő `.mbox` / `.mbx` fájl |
| **Target Account** | A Mail Archiver fiók, amely receives az üzeneteket |
| **Target Folder** | A mappa, amelybe az üzenetek kerülnek. Válasszon egyet a fiók meglévő mappalista feltárából, vagy írjon egy új nevet — a még nem létező mappát a folyamat során **automatikusan létrehozzuk**. Alapértelmezés szerint: `INBOX`. |

A fájl összes üzenete ebbe a single célmappába kerül beimportálásra. Maga az MBox fájl nem hordoz mappainformációt, így az eredeti postafiók struktúrája nem rekonstruálódik — ha erre van szüksége, használja az EML/ZIP módszert.

## 🗂️ Import EML {#import-eml}

Az EML importálás egy olyan **ZIP архівet** vár, amelynek tartalma `.eml` fájlokból áll.

- A ZIP **gyökérk directoryjében** található `.eml` fájlok a `INBOX` mappába kerülnek beimportálásra.
- A **alsó mappában** lévő `.eml` fájlok olyan mappába kerülnek beimportálásra, amelynek **ugyano a neve**, mint az alsó mappáé.

Így egy ZIP felépítése:

```
mailbox.zip
├── 2024-invoice.eml            → INBOX
├── Archive/
│   └── old-message.eml         → Archive
└── Projects/
    └── 2026/
        └── kickoff.eml         → Projects/2026
```


visszaállítja a mappafát az üzenetekkel együtt. Az egyedi `.eml` fájlok feltöltéséhez először egy ZIP fájlba kell csomagolni őket (`zip emails.zip *.eml`).

## ⚙️ Közös műköndás {#shared-behaviour}

Mindkét űrlap ugyanúgy viselkedik, miután megnyomta a **Start Import** gombot:

- **Fájlméret korlát** — `Upload__MaxFileSizeGB` beállítással konfigurálva (alapértelmezés szerint **10 GB**). Az űrlap a fájlválasztó mellett mutatja a jelenlegi limitet.
- **Háttérfeldolgozás** — a feltöltés egy feladatként kerül a sorba; nem kell nyitva tartania a böngészőt.
- **Duplikát kezelés** — az már az archivumban szereplő üzenetek kihagyásra kerülnek és külön számolódnak. A duplikáltak az `Message-ID` fejléc alapján kerülnek felismerésre.
- **Hibás bemenet** — a olvashatatlan fájlok egyenként kihagyásra kerülnek, és a státuszoldalon jelentkeznek, ahelyett, hogy leállítanák az egész importálást.
- **Progresszivitás** — közvetlenül a feladat státuszoldalára kerül, amely mutatja a feladat ID-ját, a fájl nevét, a fájl méretét, a státuszt, a feldolgozott/összesen üzenet számát és a futó számlálókat. Ugyanez a feladat a [Jobs](BackgroundJobs.md) oldalon is megjelenik.
- **Mégsemelés** — a folyamatban lévő import a státuszoldalról leállítható. Az addigi beimportált üzenetek az archivumban maradnak.
- **Audit trail** — az importálás bekerül az [access log](Logs.md) fájlba, és a fiókonkénti tárolási érték később frissül (lássee [Per-Account Storage](AccountStorage.md)).

### Feladat státuszok {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled`

A befejezett feladat jelzi, hány üzenet került beimportálásra, hányhiba történt és hányat hagyott ki a duplikáltak miatt — például:
*"1420 beimportálva, 3 hiba, 87 duplikált"*. A sikeres importálás esetén a feltöltött fájl törlődik a szerverről; a sikertelen bejegyzéseket a feladat élettartamáig megtartjuk, hogy lássa, mi volt elutasítva.

## ⚠️ Figyelni kell a következőkre {#things-to-watch-out-for}

- **Rossz fájltípus** — a feltöltés elutasításra kerül, hacsak a kiterjesztés nem az egyik: `.mbox`, `.eml` vagy `.zip`. Az EML űrlap kiválasztója csak `.zip`-öt fogad el, a MBox űrlap pedig csak a `.mbox` és `.mbx` típusokat.
- **"0 beimportálva, N duplikált"** — ez nem hiba. Az üzenetek már az archivumban vannak, valószínűleg egy korábbi import vagy ugyanazon forrás szinkronizálása miatt.
- **Nagyon nagy archivumok** — az MBox fájlt üzenetenként olvassa ki, ezért egy több gigabájtos fájl hosszú ideig eltarthat még a háttérben is. Figyelje a folyamatot a státuszoldalon ahelyett, hogy újra feltöltené.
- **Szabad disktér** — a feltöltött fájlt ideiglenesen a szerveren tárolják, és helyre van szükség az archivum adatbázis mellett.
- **Speciális karaktereket tartalmazó mappanevek** — a mappanevek a ZIP útvonalakból vagy az Ön bemenetéből származnak; a feltöltés után ellenőrizze a fiók mappalista tartalmát, ha szokatlan karaktereket használt.

## 🔗 Kapcsolódó dokumentáció {#related-documentation}

- [CLI commands](CLI.md) — importálás mountolt könyvtárból
- [Account Import (CSV)](Account-Import.md) — sok fiók egyszeri hozzáadása
- [Background Jobs](BackgroundJobs.md) — import folyamatok monitorozása és leállítása
- [Exporting a Mail Account](Export-Account.md) — az ellenkező irány
- [Setup and Parameters](Setup.md) — `Upload__*` beállítások
