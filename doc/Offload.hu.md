# 📤 Date-Windowed Offload {#date-windowed-offload}

[← Vissz a dokumentáció indexre](index.md)

## 📋 Áttekintés {#overview}

Az offload egy olyan folyamat, amely egy meghatározott időpontnál (cutoff) újabb archivált leveleket rendel más postafiókhoz, miközben az archivában hagy minden régebbit. Migrációk esetén használható, amikor az új postafiókban csak a legfrissebb leveleket szeretnénk tárolni, a teljes előzmény pedig elérhető marad a Mail Archiveren keresztül.

Öt módon tér el a [Copy All Emails to Another Mailbox](MailboxMigration.md) funkciótól:

| | Copy All Emails | Offload |
|---|---|---|
| Dátumszűrő | nincs, minden vagy sem | cutoff, opcionális felső határértékkel |
| Ismételhető | nem, mindent újra hozzárendel | igen, kihagyja azt, ami már a célban van |
| Mappák kizárása | nem | igen, konfigurálható |
| Mappák átnevezése | nem | igen, konfigurálható |
| Előnézet | nem | dry run jelentés mappánként írás nélkül |

Az offload csak hozzáad (append) a célhoz. Soha nem töröl az archívából, és soha nem érinti a forrás postafiókot.

## 🔁 Az ismételt futtatás biztonságos {#repeating-a-run-is-safe}

Az első hozzáadás előtt a teljes célpostafiókot indexelik, és minden üzenetet ellenőriznek hozzájuk viszonyítva. Így egy ismételt futtatás nem rendel hozzá semmit, ami már megérkezett, hanem azt jelent, hogy az üzenet már jelen van. Ennek három gyakorlati következménye van:

- Egy megszakadt futtatást egyszerűen újra elindítva helyre lehet állítani.
- Egy befejezett futtatást ismételhetjük ellenőrző futtatásként. A helyes eredmény az, ha semmi nem kerül hozzárendelésre, és minden már jelen van.
- A célpostafiókban törölt üzenetek a következő futtatás során visszakerülnek, mert már nincsenek az indexben.

Két szempont dönt el, hogy egy üzenet már jelen van-e, az alábbi sorrendben:

1. **Message-ID**, normalizálva. Ez teszi el a munka nagy részét.
2. **Ugyanazon ujjlenyomat (fingerprint)** a feladó, címzettek, tárgy és az üzenet beküldésének időpontja (két másodperces toleranciával). Ez az olyan üzeneteket fedezi le, amelyeknél a tárolt Message-ID hiányzik vagy hibás. Az ilyen üzenetek az eredeti Message-ID-jukkal egyáltalán nem állíthatóak vissza, így e második szempont nélkül minden egyes futtatás során duplikálódnának.

A feladat külön jelzi, hány találat érkezett az ujjlenyomat alapján, mivel ez az a szempont, amely csendben elhibázódhat.

## ⚙️ Konfiguráció {#configuration}

Egy új `Offload` szakasz, amely alapértelmezett beállításokkal reproduálja a korábbi viselkedést:

```json
"Offload": {
  "MaxConcurrentJobs": 1,
  "PrefetchMaxMessages": 500000,
  "ExcludedSourceFolders": [],
  "FolderRenameMap": {},
  "MarkAsSeen": true
}
```

| Kulcs | Alapértelmezett | Cél |
|---|---|---|
| `MaxConcurrentJobs` | `1` | Hány restore vagy offload feladat futhat egyszerre. Az egy érték fenntartja a feladatsoros (serial) viselkedést, ami korábban volt a feladatmenetben. A célpostafiókonként legfeljebb egy feladat fut le, erre nem tekinthető tekintettel erre az értékre. |
| `PrefetchMaxMessages` | `500000` | Felső határ arra, hogy hány üzenet kerül indexelésre egy célpostafiókból. E felett az ismétlésellenőrzés egyetlen mappára szűkül, és logolja ezt. |
| `ExcludedSourceFolders` | empty | Forrásmappák, amik sosem kerülnek offload alá. A megnevezés előtt kerül ellenőrzésre, és magában foglalja az almappákat is. |
| `FolderRenameMap` | empty | Átnévzi a forrásmappa elérési útja első részeit. |
| `MarkAsSeen` | `true` | Ez határozza meg, hogy a hozzárendelt levelek olvashatónak (read) vannak-e jelölve. |

Mindkét mappa-beállítás szándékosan üresen érkezik. A mappák átírása vagy elvetése kérés nélkül meglephetné azokat, akik már a restore útvonalat használják, ezért egy konfiguráció nélküli futtatás mindent migrál (a spam mappákat is), és a célpostafiók saját speciális mappái mellett létrehoz egy második készet is.

### Mappa-kizárás és átnevezés {#folder-exclusions-and-renaming}

Exchange to Dovecot migrációnál a két beállítás általában így néz ki:

```json
"ExcludedSourceFolders": ["Junk E-Mail", "Deleted Items"],
"FolderRenameMap": {
  "Sent Items": "Sent",
  "Deleted Items": "Trash",
  "Junk E-Mail": "Junk"
}
```

A mappa-átnevezési mappa (rename map) nélkül minden migrált postafiók tartalmazza mind a `Sent`, mind a `Sent Items` mappákat, mivel a Dovecot előre létrehozza a saját speciális mappáit.

Négy dolog érdemes tudni arról, hogyan alkalmazzák ezeket, mert mindegyik meglepetést okozhat:

1. **A kizárásokat először ellenőrzik** a forrás név alapján. Ha az átnevezés előbb történne meg, akkor a `Deleted Items`-re vonatkozó kizárás soha nem aktiválódna, miután az már `Trash`-vé vált.
2. **Az átnevezés a leghosszabb egyező útvonali előtagot írja át**, a szegmens határainál. Ha a `Sent Items` `Sent`-vé válik, az a `Sent Items/2019`-at is `Sent/2019`-vé alakítja, de a `Sent Items Archive`-at érintetlen hagyja.
3. **A kapcsolódás nem érzékeny a kis- és nagybetűre**, és a `/`, valamint a `\` egyaránt elválasztónak számít.
4. **Két forrásmappa összeolvadhat egy célmappává.** Ha a `Sent Items` és a `Sent` mind `Sent`-vé válik, az rendben van, és nem nyitja meg a célmappát kétszer.

A két futtatás közötti átnevezési mappa módosítása nem duplikál semmit, mert az ismétlésellenőrzés az egész célpostafiókot lefedi, nem csak egyetlen feloldott mappát.

## 🖥️ Futtatás a felületről {#running-it-from-the-interface}

A fiók részletei oldalán a **Offload to Another Mailbox** egy űrlapot nyit meg, amely tartalmazza a célpostafiókot, a cél gyökérmappát, a mappastruktúra megőrzését, a dátumablakot, egy dry run kapcsolót, valamint azt, hogy a hozzárendelt leveleket olvashatónak kell-e jelölni. A konfigurált kizárások és az átnevezési mappa csak olvasható módban jelennek meg, így a futtatás sosem váratlan.

A feladat sorba kerül a menetrendben, fejlődése és mappánkénti jelentése a feladatállapot oldalon és a **Jobs** menüpont alatt jelenik meg. A mappánkénti jelentés megnevezi a forrásmappákat, ezért csak annak a felhasználónak és az adminisztrátoroknak látható, aki elindította a feladatot.

A célpostafiók lehet egy letiltott IMAP fiók is, például migrációs cél létrehozásához, mielőtt az élő üzembe kerülne; a futtatás hibát jelent a csatlakozáskor, ha a cél nem elérhető. A Microsoft 365 fiókok nem lehetnek offload célok; a Graph restore útvonal változatlan marad.

### Ki futtathatja {#who-may-run-it}

Az adminisztrátorok bármilyen két fiók között futtathatnak offload-ot. Egy self-manager **csak a saját számára hozzárendelt fiókok között** futtathatja, mind a két végpontnál: a forrásnál, mivel a fiókoldal már így szűkített a körét, és a célnál is, mivel a céllista ugyanarra a készletre szűkül, és a kérést újra ellenőrzik ezzel szembe a feladat elindításakor. Egyetlen hozzárendelt fiókkal rendelkező self-manager tehát nem talál érvényes célt, és erről tájékoztatást kap.

A körök meghatározása a `IAccountAccessResolver` függvénytől függ, amely ugyanaz a feloldó (resolver), amelyet a REST API és az MCP szerver is használ, így létezik egyetlen definíció arról, hogy ki használhat melyik postafiókot. Maga a döntés a `OffloadTargetEligibility` függvényben él, amelyet mind a céllista, mind a kérelem-ellenőrzés meghív, így a űrlap kínálata és a szerver elfogadása nem térhet el egymástól.

## ⌨️ Futtatás parancssorból {#running-it-from-the-command-line}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll \
  --offload --source-account-id 3 --target-account-id 7 \
  --since 2025-08-01 \
  [--until 2026-08-01] [--target-folder INBOX] \
  [--preserve-folders] [--dry-run] [--no-mark-seen]
```

| Argumentum | Jelentés |
|---|---|
| `--source-account-id` | Fiók, amelyről az archivált leveleket kell olvasni. Kötelező. |
| `--target-account-id` | IMAP fiók, ahová hozzá kell rendelni. Kötelező, és meg kell különböznie a forrástól. |
| `--since` | Az elküldési dátum bezględelt alsó határa, `YYYY-MM-DD`. Kötelező. |
| `--until` | Opcionális, bezględelt felső határ. |
| `--target-folder` | Gyökérmappa a célpostafiókban. Alapértelmezetten `INBOX`. |
| `--preserve-folders` | A forrás mappastruktúra újra létrehozása a gyökér alatt. |
| `--dry-run` | Jelenti, mi történne, de semmit nem rendel hozzá. |
| `--no-mark-seen` | Hozzárendelés Seen flag (olvasható jelzés) nélkül. |

Kilépési kódok:

| Kód | Jelentés |
|---|---|
| `0` | Minden hozzá került vagy már jelen volt. |
| `1` | Legalább egy üzenet elbukott, vagy maga a futtatás hibásodott el. |
| `2` | Hibás hívás: rossz vagy hiányzó argumentumok, ismeretlen fiók, a cél nem IMAP, a forrás megegyezik a céllal. |

Egy hívás egy postafiókot kezel, ami lehetővé teszi, hogy egy CSV fájl (forrás, cél és cutoff alapján) segítségével egy egész fiókkal rendelkező flottát automatizáljunk.

> **Több folyamat egyidejű futtatása.** A "egy feladat minden célpostafiókhoz" szabály csak egyetlen folyamaton belül érvényes, így két egyidejű parancssori hívás ugyanazon a célpostafiókon történő futtatás esetén mindkettő le tudja különíteni az ismétlés-indexét, mielőtt a másik elkezdené az írást, és ezáltal mindkettő ugyanazokat a leveleket adhatja hozzá. A munkát úgy kell felosztani, hogy minden egyidejű hívás különböző célt érjen el, ami a normális eset, ha egy CSV sorában minden egyes fiók más.

## 🗓️ Hogyan értelmeződik a cutoff {#how-the-cutoff-is-interpreted}

- A szűrő a **küldési dátumon** van, nem pedig azon, hogy mikor archiválták az üzenetet. Az archiválás időbélyeg (archived-at timestamp) az archiválási futtatás ideje, és nincs jelentősége a cutoff-nak.
- Egy relatív ablak, mint például "az elmúlt tizenkét hónap", **fix dátumra kerül megoldásra, amikor a feladat létrehozódik**. Egy feladat hosszú ideig futhat és később ismétlődhet, és minden alkalommal ugyanazokat a leveleket kellene kiválasztania; egy tárolt relatív ablak eltolódna az időben.
- A cutoff-okat a **konfigurált megjelenítési időzónában** értelmezik, ugyanabban, amit a keresőképernyő is mutat, és az alsó határ az adott nap 00:00-tól kezdődően bezględetlen. A felső határ az egész napon belül érvényes. A relatív ablakok ("az elmúlt N hónap") szintén ebben a megjelenítési időzónában kerülnek megoldásra, ugyanúgy, ahogy a `SentDate` is tárolódik.

### Ismert korlátozás {#known-limitation}

- Amikor az ismétlés-index eléri a `Offload:PrefetchMaxMessages` értéket, a körét csökkentik és logolják, ahelyett, hogy egyetlen mappával próbálkoznának újra. Az indexépítő által kínált `restrictToFolder` visszahajtási lehetőség jelenleg nem kerül alkalmazásra az offload útvonalon; a limit növelése a támogatott megoldás.

## 📋 Migráció, lépésről lépésre {#a-migration-end-to-end}

1. Hozzák létre a célpostafiókokat. Több egyszerre az [Account Import](Account-Import.md) dokumentációban található CSV tömegimport segítségével.
2. Konfigurálják a `Offload:ExcludedSourceFolders` és `Offload:FolderRenameMap` beállításokat. Mindkettő alapértelmezetten üres, így az elhagyásuk migrálja a spam mappákat is, és duplikált speciális mappákat hoz létre.
3. Ellenőrizzék, hogy minden forráspostafiók teljes körűen archivált-e, mielőtt elindítják az offload-ot.
4. **Mindenről dry run.** Ellenőrizzék a darabszámokat és a megoldott célmappaneveket, mielőtt bármit leírnának.
5. Valódi futtatás. Kezdje a `MaxConcurrentJobs` értékkel 2-nél, és csak azután emelje, ha látja, hogyan bírja a cél szerver a terhelést; a Dovecot korlátozza az egy felhasználóra és egy IP-re vonatkozó egyidejű csatlakozásokat.
6. Ismételje a futtatást a ellenőrzéshez. Olyan eredményt várjunk, hogy semmi nem került hozzá, és minden már jelen volt.
7. Tiltson le a forrásfiókokat.

A sebességet a `BatchOperation:PauseBetweenEmailsMs` korlátozza, amely alapértelmezetten 50 ms, így legfeljebb tíz hozzáadást tesz lehetővé másodpercenként, mielőtt a szerver válaszidejét (latency) számolni kellene.

## 🔍 Mit jelentenek a számlálók {#what-the-counters-mean}

| Számláló | Jelentés |
|---|---|
| Appended | A célba írt üzenetek száma. Dry run esetén azok, amik írásba kerültek volna. |
| Already present | A célpostafiókban már létező üzenetek száma valamelyik szempont alapján. |
| of those by fingerprint | Hányat ismertek fel az ujjlenyomat alapján a Message-ID helyett. |
| Excluded folder | Azok a mappák, amikről kihagyták az üzeneteket, mert a forrásmappa kizárt. |
| Failed | Azok, amelyek nem adhatók hozzá. |

Figyelmeztetés jelenik meg, ha az ismétlés-index nem fedte le a teljes célpostafiókot, ami akkor fordul elő, ha a limit `PrefetchMaxMessages` felett van, vagy ha egy mappát nem lehet elolvasni. Ebben az állapotban egy ismételt futtatás hozzáadhat olyan leveleket is, amelyek már jelen vannak azokban a részekben, amiket nem indexeltek.

## ⚠️ Megjegyzések és korlátok {#notes-and-limits}

- A visszaállított levelek most már az **eredeti kézbesítési idejüket** viselik IMAP belső dátumként, amit a üzenet `Received` lánca alapján vesznek fel, vagy visszamenőleg a küldési dátumot használják. Korábban az archiválás időpontját vették, ami megszakította a szerver oldali érkezési sorrendet és minden korábban alapuló szabályt a célpostafiókban.
- Mivel az ismételt futtatás kihagyja azt, ami már jelen van, egy hibás belső dátum nem javítható újra futtatással: az üzeneteket felismeri és kihagyja. Ezt a gyártási futtatás (production run) előtt állítsa helyre, vagy törölje az érintett üzeneteket a célpostafiókban, és futtassa újra a folyamatot.
- A Microsoft 365 nem támogatott offload célként.
- Az ismétlés-érzékelés csak azt ismerheti fel, amit a célpostafiókban lát. Az olyan levelek, amelyek más úton értek el a célhoz, csak olyasmiként kerülnek elkapásra, amennyiben a két szempont véletlenül egyezik meg velük.
