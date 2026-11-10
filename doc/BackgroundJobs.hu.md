# ⚙️ Háttérfolyamok {#background-jobs}

[← Vissza a dokumentáció indexhez](index.md)

## 📋 Áttekintés {#overview}

A Mail Archiverben a legtöbb munka nem a várakozás alatt történik. Egy postalakás szinkronizálása, egy MBOX fájl importálása, több ezer e-mail visszaállítása vagy egy archívum exportálása percekig vagy akár órákig is eltarthat. Ezek a feladatok **háttérfolyamokként** futnak — elindítja őket, és akkor is futnak tovább, ha bezárja a böngészőt.

A **Háttérfolyamok** oldal mutatja, mi fut éppen, mi fejeződött be, és mi ment el rosszul. Ez az a hely, ahová érdemes nézni, ha valami tovább tart, mint várható, vagy ha egy szinkron hibákkal zárul.

> **ℹ️ Adminisztrátori oldal.** A Háttérfolyamok adminisztrátori jogokat igényel, és a főmenüből érhető el. A sima felhasználók nem látják — továbbra is elindíthatnak folyamatokat (például tömeges visszaállítást), de a monitorozás itt történik.

## 🗂️ Folyamattípusok {#job-types}

Az oldal típusok szerint csoportosítja a folyamatokat. Minden szakasz a **legutóbbi 20** folyamatot jeleníti meg, a futó és a sorban álló folyamatokat pedig a tetejére sorolja — így egy régi folyamat nem kerül el a szemünk elől az újabbak miatt.

| Szakasz | Mit fed le | Honnan indított |
|---|---|---|
| **Szinkronizációs folyamatok** | Új levelek lekérése a mail szerverről | Automatikus ütemezés, vagy *Sync now* egy fiókon |
| **MBox Import folyamatok** | `.mbox` fájlok importálása | Mail Accounts → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **EML Import folyamatok** | `.eml` fájlok / ZIP archívumok importálása | Mail Accounts → [Import EML](Import-EML-MBox.md#-import-eml) |
| **Tömeges visszaállítási folyamatok** | Kiválasztott e-mailek visszamásolása egy postalakásba | Archive → select emails → Batch Restore |
| **Fiók export folyamatok** | Egy teljes fiók exportálása (mbox / EML) | Mail Accounts → [Export](Export-Account.md) |
| **Kiválasztott e-mailek export folyamatai** | Egy kézzel kiválasztott e-mail készlet exportálása | Archive → select emails → Export |
| **E-mail törlési folyamatok** | E-mailek törlése a szerverről vagy az archívumból, beleértve a szűrőalapú tömeges törlést is | Retention policy, manuális törlés, vagy Archive → *Delete all matches* |
| **Fiók törlési folyamatok** | Egy teljes e-mail fiók eltávolítása | Mail Accounts → Delete |
| **Offload** | Időintervallum alapú levelek mozgatása másik fiókba | Mail Accounts → Offload |

## 📊 Egy folyamat sorának olvasása {#reading-a-job-row}

Minden sor ugyanazt a oszlopkészletet tartalmazza:

| Oszlop | Jelentése |
|---|---|
| **Job ID** | Rövid azonosító — használja idézőjelben, ha problémát jelent |
| **Account** | A postalakás, amelyen a folyamat dolgozik |
| **Status** | Jelenlegi állapot, lásd az alábbi táblázatot |
| **Progress** | Mennyire haladt meg a folyamat, feldolgozott/összesen formátumban, ahol lehetséges |
| **Created** | Mikor került sorba a folyamat |
| **Started** | Mikor kezdődött el valójában a futás |
| **Completed** | Mikor fejeződött be (üres, amíg még fut) |
| **Duration** | Elült idő |

Néhány folyamattípus további számlálókat ad a progress oszlophoz:

- **Success** / **Failed** — sikeresen és sikertelenül feldolgozott e-mailek
- **Skipped (duplicates)** — az archívumban már meglévő e-mailek, nem kerülnek kétszer be |
- **Recovered** — a szerver hiánynak jelezte, majd újra lekért üzenetek
- **Malformed (skipped)** — olyan üzenetek, amelyeket nem lehetett értelmezni, és kihagyták őket

### Status értékek {#status-values}

| Status | Jelentése |
|---|---|
| **Queued** | Várakozás a kezdésre — egy másik folyamat fut, vagy a worker elfoglalt |
| **Running** | Jelenleg végrehajtás alatt |
| **Completed** | Sikeresen befejeződött |
| **CompletedWithErrors** | Befejeződött, de néhány elem elbukott (MBox importok esetén) |
| **Failed** | Hibával zárult |
| **Cancelled** | Felhasználó állította le |
| **Rate Limited** | *Szinkron felfüggesztve a sávszélesség korláta miatt. Automatikusan folytatódik.* |
| **Timed Out** | A szinkron túllépte a konfigurált időlimitet, és leállt |

> **ℹ️ A "Rate Limited" és "Timed Out" állapotok csak a szinkronnál fordulnak elő.** Úgy tűnhetnek hibáknak, de azok nem: a sávszélesség korlátozott szinkron önmagától folytatódik, amint a sávszélesség-kvóta engedi, a timeout szinkron pedig a következő futáskor a megállási pontjánál (checkpoint) folytatja magát. Lásd a [Rate Limit Handling](RateLimitHandling.md) és a [Mail Synchronization](Synchronization.md) részeket.

### Miért állhat a progress 0%-on {#why-progress-can-sit-at-0}

A haladást a **befejezett mappák száma** méri, nem az egyes e-maileket:

> *A haladást a befejezett mappák száma méri. Egy nagy mappa (pl. INBOX) addig tartja a sávot 0%-on, amíg nem fejeződik be.*

Egy olyan postalakásban, ahol az INBOX tartalmazza a legtöbb levelet, a sáv hosszú ideig 0%-on maradhat, majd hirtelen ugrani fog. Ez elvárható — a folyamat dolgozik, csak még nem fejezte be az első nagy mappát.

## 🔄 Az oldal önmegfrissül {#the-page-refreshes-itself}

Amíg az oldal nyitva van, minden **30 másodpercenként** újratöltődik, így hagyhatja egy fülre, és követheti a haladást kattintás nélkül is. A felirat jelzi, hogy mikor frissült utoljára az adatok.

Az újratöltés azonnal megromlik, amint rákattint egy linkre vagy gombra — különben a kattintás egy olyan oldalra történne, amely éppen helyét veszi át egy másiknak.

**Konfigurálható:** `Jobs:RefreshSeconds` a `appsettings.json`-ben (alapértelmezett `30`).
Állítsa `0`-ra az automatikus újratöltés kikapcsolásához. Minden újratöltés az összes folyamatforrásból újraépíti az oldalt, így egy sok fiókot tartalmazó telepítésnél a nagyon rövid intervallum valódi terhelést jelent — és ez a terhelés minden nyitott fül alatt megs倍ződik.

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 Folyamat leállítása {#cancelling-a-job}

Azok a folyamatok, amelyek támogatják a leállítást, egy **Cancel Job** gombot mutatnak, amely a következő kérdést teszi fel:

> *Biztosan le akarja állítani ezt a folyamatot?*

Az import folyamatok a **Cancel Import** / *Are you sure you want to cancel this import job?* szöveget használják.

A leállítás elérhető a **szinkronizációs, tömeges visszaállítási, kiválasztott e-mailek export és e-mail törlési** folyamatokhoz. A folyamat azonnal leáll, amint a jelenlegi elem befejeződött; a státusz pedig **Cancelled** lesz.

> **⚠️ A leállítás nem egy rollback.** Az már importált, visszaállított, exportált vagy törölt e-mailek azok maradnak. A leállítás csak azt akadályozza meg, hogy a folyamat *többet* csináljon — nem vezeti vissza azt, amit már megtett. Törlési folyamat esetén ez azt jelenti, hogy az már eltávolított e-mailek véglegesen eltűntek.

A fiók- és MBox import folyamatok nem állíthatók le erről az oldalról; ezeket a fiók saját importálási oldalán kell leállítani.

## ⚠️ Szinkronhibák és az "Acknowledge Failures" {#sync-failures-and-acknowledge-failures}

Amikor egy szinkron **Failed** státusszal zárul, a folyamat sor kínálja az **Acknowledge Failures** lehetőséget. Ez az egyetlen művelet az oldalon, amely megváltoztatja a jövőbeni viselkedést, ezért érdemes megérteni.

Minden szinkron futás ott folytatódik, ahol az előző megállt, egy **checkpoint** segítségével. Ha egy szinkron néhány e-mail miatt elbukik, a checkpoint azok mögött marad, így a következő futás újra megpróbálja őket. Ez a biztonságos alapértelmezett — semmi nem kerül csendben el a szem(?:esről).

De ha a hibák állandóak (egy sérült üzenet, egy mappa, amit a szerver nem szolgál ki), minden következő szinkron újra megpróbálja őket, és a szinkron ugyanazon a helyen fog újra elbukni.

Az **Acknowledge Failures** feloldja ezt a holtpontot azáltal, hogy a checkpointet a hibás e-maileken túl helyezheti el. A megerősítés egyértelműen jelzi a következményeket:

> *Ez eltoltatja a szinkron időbélyegzőt (timestamp) ennek a folyamat befejeződésének idejére, így a következő szinkron normál növekvő (incremental) szinkron lesz. A hibás e-maileket nem próbáljuk újra. Folytatja?*

Sikeres esetben ezt látja: *"Sync timestamp has been advanced. The next sync will be incremental."*

> **⚠️ Csak akkor erősítse meg, ha már megtekintette a hibákat.** Megerősítés után azokat az e-maileket **nem** próbálja újra — azok hiányozni fognak az archívumból, hacsak nem más módon szerez meg őket. Előbb ellenőrizze a hiba részleteit, és ha a hiba oka egy hibás konfiguráció, azt javítsa meg, mielőtt megerősítené. Lásd a [Mail Synchronization](Synchronization.md) részt a checkpoint működéséről részletesen.

Az elfogadás (acknowledging) be van jegyezve az hozzáférési naplóba (*SyncAcknowledgeFailures* formátumban), a hibás e-mailek számával és a fiókkal együtt, így később nyomon követhető.

## 👁️ Amit itt nem tehet {#what-you-cannot-do-here}

- **Nincs újratöltés (retry) gomb.** Egy elbukott folyamat nem indítható újra erről az oldalról — indítsa újra a műveletet onnan, ahol elindította (szinkron, import, export).
- **Nincs szerkesztés.** A folyamat paraméterei a létrehozáskor rögzítettek.
- **Csak a legutóbbi 20 típusonként.** A régebbi folyamatok nem szerepelnek listában. Hosszú távú naplózáshoz az hozzáférési napló és az alkalmazási napló a forrás — lásd a [Access Logging](Logs.md) és a [Docker Compose Logs](DockerComposeLogs.md) részeket.

## ❓ Hibaelégítés {#troubleshooting}

| Tünet | Valószínű ok | Mit tegyen |
|---|---|---|
| Folyamat **Queued** állapotban marad | Egy másik folyamat fut ugyanazon a workerön | Várjon; ellenőrizze a többi szakaszt egy hosszú ideig futó folyamat után |
| Progress 0%-on akad | Egy nagy mappa még feldolgozás alatt áll | Normális — lásd a "Miért állhat a progress 0%-on" részt |
| Szinkron ismételten **Failed** ugyanazon a fiókon | Állandó hibák az egyes e-maileknél | Vizsgálja meg a hibákat, javítsa meg az okot, majd erősítse meg |
| Szinkron **Rate Limited** állapotot mutat | Elért sávszélesség-kvótát | Semmit — automatikusan folytatódik |
| Szinkron **Timed Out** állapotot mutat | A postalakás nagyobb, mint az időlimit | A következő futás a checkpointnél folytatódik; lásd a [Synchronization](Synchronization.md) részt |
| **CompletedWithErrors** egy MBox importnál | Néhány üzenet rossz formátumú | A számlálók mutatják, hány darab; a 나머st beimportálták |
| Folyamat eltűnik az újratöltés után | Kilépett a "legutóbbi 20"-as ablakból | Ellenőrizze az alkalmazási naplót a végső állapotért |
| Az oldal újratöltődik munka közben | Automatikus frissítés 30 másodpercenként | Állítsa a `Jobs:RefreshSeconds` értéket `0`-ra |

## 📚 Kapcsolódó dokumentáció {#related-documentation}

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — checkpointek, timeout szinkronok
- [Rate Limit Handling](RateLimitHandling.md) — sávszélesség-kvóták és az automatikus folytatás
- [Access Logging](Logs.md) — ki mit csinált, beleértve a folyamat-műveleteket is
- [Batch Restore](MailboxMigration.md) — e-mailek mozgatása a postalakások között
- [Date-Windowed Offload](Offload.md) — offload folyamatok
- [Docker Compose Logs](DockerComposeLogs.md) — az alkalmazási napló olvasása
