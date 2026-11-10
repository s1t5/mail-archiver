# 🧬 Mellékjegyzék de Duplikáció (Attachment Deduplication) {#attachment-deduplication}

[← Vissza a Dokumentáció Indexhez](index.md)

## 📋 Áttekintés {#overview}

A mail mellékletek bináris adatként (payload) tárolódnak az adatbázisban. A ** ugyanaz a fájl** nagyon gyakran több e-mailhez is mellékelve van (célogos aláírások, továbbított PDF-ek, hírlevelek, szerződések, ...). De Duplikáció nélkül minden melléklet saját privát másolatot tárol ezekről a byteokról, ami hatalmas mennyiségű adatbázis-helyet foglal le.

A **mellékletek de duplikációja** minden *egyedi* tartalmat pontosan egyszer tárol egy tartalom alapú táblázatban (amelynek kulcsa az egyes byteok SHA-256 hash-értéke), és lehetővé teszi, hogy minden azonos melléklet erre az egyetlen közös másolatra mutasson. Ez általában jelentősen csökkenti a sok mellékletet tartalmazó archivumok tárolási helyigényét.

## ⚙️ Hogyan működik {#how-it-works}

### Új mellékletek {#new-attachments}
Amikor egy új e-mail archiválódik (IMAP, Microsoft 365 / Graph, EML importálás, MBOX importálás, ...), a melléklet tartalma hash-eltésre kerül, és egyszeren tárolódik. Ha ugyanaz a tartalom már létezik, az új melléklet egyszerűen a meglévő tartalomsorra hivatkozik. Ez átláthatóan történik az adatrétegen, így minden archiválási útvonal automatikusan le van fedve.

### Mevexistső adatok (migráció) {#existing-data-migration}
Olyan telepítéseknél, amelyek már ezen funkció bevezetését **előtt** archivált mellékleteket tartalmaznak, egy **robusztus, félismétetleníthető háttérben futó migráció** alakítja át a meglévő inline tartalmakat közös tartalomsorokká:

- **Batchal, azaz csoportosan dolgozik**, ahol minden csoport egy saját tranzakcióban fut.
- A haladást egy dedikált állapot-táblázatban tárolja, így a migráció **túlél egy alkalmazás-újraindítást**, és egyszerűen ott folytatja, ahol abbahagyta.
- **Idempotens** – a már migrált mellékletekkel többször nem nyúlunk, még egy összeomlás vagy újraindítás után sem.
- Háttérben fut, és nem blokkolja az alkalmazás normál használatát.

Amikor a migráció befejeződik, egy befejelezési üzenetet ír ki, pl.:

```
Attachment Deduplication migration COMPLETED. 36199 attachments processed in 1.6 minutes.
```

A következő indításkor a rendszer felismeri, hogy nincs több teendő, és nyúgó állapotban marad. Ha bármilyen de-duplikálatlan melléklet maradna hátra (például egy régebbi mentés visszaállítása után), a rendszer automatikusan folytatja és migrálja őket — a rendszer mindig egy teljes de-duplikált állapotba konvergál.

### Garbage collection (mindig aktív) {#garbage-collection-always-on}
Amikor e-maileket (és így mellékleteket) törölnek, egy közös tartalom nem lehet tovább hivatkozott. Beépített garbage collection (szemétgyűjtés) eltávolítja ezeket az elárulmányos (orphan) tartalomsorokat.

> 🧹 **A tisztítás SEMLÉG SEMNYEGYESEN fut**, függetlenül a
> `DatabaseMaintenance` funkciótól. A migráció után egyszer fut le, majd
> időnként (minden `OrphanCleanupIntervalHours`, alapértelmezett 12 h). Amikor
> `DatabaseMaintenance__Enabled=true`, a tisztítás továbbá fut le a napi `VACUUM` előtt is, így a felszabadított hely fizikailag visszanyerhető.

### Tárhelyhasználat a migráció után (fontos) {#disk-usage-after-the-migration-important}

> 📈 **A "Storage" érték a dashboardon rövid időre FEL AKAR HATALMASODNI a migráció után** (pl. 3.5 GB → 5.3 GB). Ez várható, és nem hiba.
>
> A dashboard a **fizikai** adatbázis méretét jelenti (`pg_database_size`). Amikor a migráció egy tartalmat közös tárolóba mozgat, a korábbi inline másolatot `NULL` állapotba állítja, de a PostgreSQL (MVCC) megőrzi ezeket a régi byteokat *halott tömböként* (dead tuples), amíg a táblatájékszót nem írják át — így egy pillanatra az adatbázis tartalmazza mind a régi, mind az új másolatot.
>
> Az automatikus javítás érdekében az alkalmazás egy **egyszeri**
> `VACUUM FULL` futtatást a melléklet táblán **miután a migráció befejeződött és amíg nincs futó szinkronizációs feladat**. Ez átírja a táblát és visszaküldi a felszabadított helyet az operációs rendszernek; ezután a jelentett méret a původi méret **alattra** csökken. Pontosan egyszer fut le (egy `ReclaimedAt` jelző segítségével nyomon követve), újraindítás-biztos, és addig halasztódik, amíg szinkronizálás folyamatban van (automatikusan megpróbálja újra, ha az rendszer elengedte).

>
> ⚠️ A `VACUUM FULL` lezárja a melléklet táblát a futás ideje alatt (közben nincs melléklet olvasás/írás) és némi szabad tárhelyre van szüksége az átírás a folyamathoz. Ha manuálisan szeretné elindítani, futtassa a következő parancsot:
> ```sql
> VACUUM (FULL, ANALYZE) mail_archiver."EmailAttachments";
> ```

## 🔧 Konfiguráció {#configuration}

E these beállítások opcionálisak; az alapértelmezettek megalgunak a legtöbb telepítéshez. Lásd tovább a [Setup Guide](Setup.md#-attachment-deduplication-settings) dokumentációt.

| Beállítás | Alapértelmezett | Leírás |
|---------|---------|-------------|
| `AttachmentDeduplication__BatchSize` | `200` | Egy tranzakció során migrált meglévő mellékletek száma. A nagyobb értékek gyorsabban migrálnak, de növelik az emlék/DB terhelést batchonként. |
| `AttachmentDeduplication__DelayBetweenBatchesMs` | `0` | Opcionális szünet (ms) a migrációs batchek között a DB terhelés korlátozására elfoglalt rendszereken. |
| `AttachmentDeduplication__StartupDelaySeconds` | `20` | Késleltetés (s) az alkalmazás indítása után, mielőtt a migráció elkezdődne (hogy először a schema migráció befejeződjön). |
| `AttachmentDeduplication__OrphanCleanupIntervalHours` | `12` | Időintervallum (h) az állandóan futó orphan garbage collection számára. |
| `AttachmentDeduplication__CommandTimeoutSeconds` | `300` | Adatbázis parancs timeout (másodpercekben) a migrációs batch műveletekhez. Az alapértelmezett 5 perc. Ha egy batch továbbra is timeoutot okoz, a szolgáltatás automatikusan megpróbálja újra a felét tartalmazó batch mérettel. |

Példa (`docker-compose.yml`):

```yaml
environment:
  - AttachmentDeduplication__BatchSize=200
  - AttachmentDeduplication__DelayBetweenBatchesMs=0
  - AttachmentDeduplication__StartupDelaySeconds=20
  - AttachmentDeduplication__OrphanCleanupIntervalHours=12
  - AttachmentDeduplication__CommandTimeoutSeconds=300
```

## ❓ GYIK {#faq}

**Ki tudom kapcsolni a de-duplikációt?**
Nem. Ez egy alapvető tárolási funkció, és mindig aktív. Csak a fenti batch méretet és ütemezési paramétereket tudja finomhangolni.

**Biztonságban vannak az adataim a migráció alatt?**
Igen. Minden batch egyetlen tranzakció, és a munka idempotens. Egy hibás leállás vagy újraindítás a migráció közbeni visszavonja a befejezetlen batchet, és a következő indításkor újra feldolgozza; nincs adatvesztés vagy duplikáció.

**Lassítja-e a archiválást?**
A mellékletenkénti hash-elés overhead negligészihető. A meglévő adatok egyszeri migrációja háttérben fut, és szükség esetén `DelayBetweenBatchesMs` segítségével is korlátozható.

**A migráció folyamatosan timeoutot jelez és ugyanazon a kurzornál indítja újra. Mit tehetek?**
Ez nagyon nagy adatbázisoknál (százezer e-mail) fordulhat elő, ahol egyetlen 200 mellékletet tartalmazó batch SHA-256 hash-eléssel meghaladja a parancsi timeout értéket. A szolgáltatás most már automatikusan megpróbálja újra a batch felével kisebb mérettel, ha timeout történik. Ha továbbra is tapasztal timeoutot, manuálisan csökkentheti a batch méretet és/vagy növelheti a parancs timeout idejét:

```yaml
environment:
  - AttachmentDeduplication__BatchSize=10
  - AttachmentDeduplication__CommandTimeoutSeconds=600
```

**Mennyi helyet fogok megtakarítani?**
Ez teljesen attól függ, hogy hány de Duplikált melléklet található az archivumban. A sok ismétlődő aláírást, logót vagy továbbított fájlt tartalmazó archivumokból kerül a legtöbb előny.
