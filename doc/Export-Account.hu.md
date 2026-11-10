# 📤 Mail fiók exportálása (EML / MBox) {#exporting-a-mail-account-eml-mbox}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

A fiók exportálás minden egyes üzenetet egy letölthető ZIP fájlba ír vissza. Ez a [web import](Import-EML-MBox.md) megfelelője: a fiókról hordozható archívumot kap, olyan formátumban, amelyet bármilyen e-mail kliens vagy migrációs eszköz képes olvasni.

Két formátum érhető el:

| Formátum | Mit kapsz | Tipikus használat |
|--------|--------------|-------------|
| **EML** | Egyedi `.eml` fájlok, mappákba szervezve | Újratelepítés e-mail kliensbe, üzenetenkénti feldolgozás, hosszú távú olvashatóság |
| **MBox** | Hagyományos postafiók fájlok — **egy fájl másonként** | MBox-ot olvasó eszközök és kliensek, tömeges szöveges feldolgozás |

Mindkét esetben az üzenetek nyers RFC-822 üzenetként kerülnek leírásra, így a **csatolmányok is szerepelnek** az exportban.

> 💡 **Tipp**: Ha nem az egész fiókot, hanem csak egy kiválasztott üzenetcsoportot szeretné exportálni, használja a tömeges műveleteket a [Mail search](Search.md#-acting-on-many-results-at-once) funkcióban.

## 🧭 Elérhetőség {#access}

1. Nyissa meg a **Mail Accounts** menüpontot a főmenüben.
2. Kattintson a fiók során található **⋮** (további műveletek) gombra, és válassza az **Export** lehetőséget. Ugyanez a bejegyzés elérhető a fiók **Details** oldalán is.

Az exportáláshoz **admin** vagy **self manager** szerepkör szükséges — a standard felhasználók látják a menüpontot, de nem tudják használni. Az export mindig az egész fiókot lefedi; nincs dátum- vagy mappaszűrő.

## 🖥️ Az Export oldal használata {#using-the-export-page}

Az oldal megjeleníti a fiók **összes**, **beérkező** és **küldő** üzenetszámát, így a folyamat elindítása előtt láthatja a feladat méretét. Ezután:

1. Válassza ki a formátumot — **EML** vagy **MBox**.
2. Nyomja meg az **Start Export** gombot.

Az export **háttérben** fut. Egy állapotoldalra kerül, amely megjeleníti a feladat azonosítóját (job ID), a választott formátumot, az időbélyegeket, valamint a feldolgozott/összes üzenetszámot.

### Feladat állapotai {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

Amikor a feladat eléri a **Completed** állapotot, egy **letöltési gomb** jelenik meg az állapotoldalon. Ugyanez a feladat a [Jobs](BackgroundJobs.md) oldalon is szerepel.

> ⚠️ **Az export fájlok automatikusan törlődnek.** A ZIP-et a **sikeres letöltés után**, illetve minden esetben **7 nap után** törli a rendszer. A fájlt amíg még elérhető, töltse le — maga az archívum nem érintett, később egyszerűen indíthat új exportot.

## ⚠️ Amire figyelni kell {#things-to-watch-out-for}

- **A nagy fiókok időbe telhetnek.** Egy nagy postafiók több percet is igénybe vehet, és a ZIP fájl mérete is nagyon nagy lehet az üzenetek mennyisége és a csatolmányok függvényében. Mivel a feladat a háttérben fut, bezárhatja a böngészőt, és később visszatérhet az állapotoldalra.
- **Szerver tárhely.** A ZIP-et a szerveren állítja össze, mielőtt letölti, ezért győződjön meg arról, hogy elegendő szabad hely áll rendelkezésre az archívum mellett.
- **"Export file not found"** — a feladat még nem fejeződött be, vagy a fájlt már letöltötték, illetve a 7 napos törlési szabály miatt távolították el. Indítson új exportot.
- **Nincsenek szűrő opciók** — az export fiókonként „mind vagy sem” elve alapján működik. Egy részlet esetén használja a keresési oldal exportját.
- **Nem megfelel audit-szabályozási exportnak.** Audit-ready adat-média átadás esetén, index fájllal együtt, használja helyette az [Audit Data Export](AuditExport.md) funkciót — ez olyan csomagot generál táblázatos tömegadat formátumban, amelyet az audit eszközök várnak.

## 🔗 Kapcsolódó dokumentáció {#related-documentation}

- [Importing Mail in the Web UI](Import-EML-MBox.md) — az ellenkező irány
- [Mail search](Search.md) — üzenetválaszték exportálása
- [Background Jobs](BackgroundJobs.md) — feladatlista, folyamatが進és és lemondás
- [Audit Data Export](AuditExport.md) — audit-ready export csomagok
- [CLI commands](CLI.md) — importálás csatlakoztatott könyvtárból
