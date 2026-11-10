# 📊 Rate Limit Handling {#rate-limit-handling}

[← Vissz a dokumentáció indexre](index.md)

## 📋 Áttekintés {#overview}

Sok e-mail szolgáltató korlátozza az IMAP-el való hozzáférést a sávszélesség alapján. Ezek a korlátok jellemzően néhány száz megabytumtól néhány gigabájtig terjednek 24 óránként. Amikor a korlátokat meghaladó nagy postafiókok szinkronizálásáról van szó, a szinkronizálási folyamat normál esetben elhibásodik vagy hiányossá válik.

A Mail Archiver beépített sebességkorlátoлást kezelő rendszert tartalmaz, amely automatikusan kezeli ezeket a korlátozásokat, biztosítva a megbízható szinkronizálást még a nagy postafiókok esetében is.

## ⚙️ Hogyan működik {#how-it-works}

### Automatikus sávszélesség-követés {#automatic-bandwidth-tracking}

A rendszer folyamatosan figyeli az IMAP szinkronizálás során letöltött adatok mennyiségét. Amikor a konfigurált sávszélesség-limit elérhetővé válik:

1. **A szinkron automatikusan szünetel**: A folyamatban lévő szinkron művelet rendesen leáll.
2. **A haladás mentésre kerül**: Checkpointek tárolják a pontos helyet, ahol a szinkron megállt.
3. **Státuszfrissítés**: A fiók a dashboardon a "Rate-Limited" állapotot mutatja.
4. **Automatikus folytatás**: Amikor a limit visszaáll (általában 24 órával később), a szinkron a mentett checkpointtől folytatódik.

### Példa: 14 GB-os postafiók 2,5 GB-os napi limittel {#example-14-gb-mailbox-with-25-gb-daily-limit}

```
Day 1:  Sync starts → ~2.5 GB downloaded → Rate-Limited
        Status: Rate-Limited, Checkpoint saved

Day 2:  Limit still active → Sync skipped

Day 3:  Limit resets → Sync resumes from checkpoint
        → Additional ~2.5 GB → Rate-Limited again

Day 4-6: Continues...

Day N:   All 14 GB synced → Status: Completed
```

## 🔧 Konfiguráció {#configuration}

### A sávszélesség-követés engedélyezése {#enabling-bandwidth-tracking}

Adja hozzá a következő szakaszt a `appsettings.json` fájljához:

```json
{
  "BandwidthTracking": {
    "Enabled": true,
    "DailyLimitMb": 2500,
    "WarningThresholdPercent": 80,
    "PauseHoursOnLimit": 24
  }
}
```

### Konfigurációs opciók {#configuration-options}

| Opció | Leírás | Alapértelmezett | Példa |
|--------|-------------|---------|---------|
| `Enabled` | Sávszélesség-követés engedélyezése/tiltása | `false` | `true` |
| `DailyLimitMb` | Napi letöltési limit megabytenben | `2500` | `2500` |
| `WarningThresholdPercent` | A limit százaléka, amelynél log figyelmeztetést váltanak | `80` | `80` |
| `PauseHoursOnLimit` | Szünetelési órák, ha elérhető a limit | `24` | `24` |

### A megfelelő limit beállítása {#setting-the-correct-limit}

A megfelelő `DailyLimitMb` érték meghatározásához:

1. Ellenőrizze e-mail szolgáltatója dokumentációját vagy szolgáltatási feltételeit.
2. Keressen IMAP sávszélesség-limiteket vagy letöltési kvótákat.
3. Vegyen fel kapcsolat az e-mail adminisztrátorral, ha vállalati e-mail rendszert használ.
4. Ha nem biztos, kezdjen egy óvatosabb értékkel (pl. 2000 MB).
5. Figyelje a logokat a sebességkorlátozással kapcsolatos figyelmeztetések miatt az elsődleges szinkronizálás során.

## 👤 Felhasználói élmény {#user-experience}

### Dashboard indikátorok {#dashboard-indicators}

Amikor a sebességkorlátozás aktív, a fiók az alábbiakat jeleníti meg:

- **Status**: "Rate-Limited" 
- **Info Banner**: Elmagyarázza a helyzetet és a becsült visszaállási időt.
- **Sync Progress**: Megmutatja, hány e-mailt feldolgozott, mielőtt elérte a limitet.

### Automatikus viselkedés {#automatic-behavior}

1. **Nincs szükség manuális beavatkozásra**: A rendszer mindent automatikusan kezel.
2. **Alkalmazás újraindítása**: Ha az alkalmazás újraindul, a sebességkorlátozási állapot megmarad az adatbázisban.
3. **Több fiók**: Minden fiók függetlenül követi a saját sávszélességét.

### Mit mentünk el {#what-gets-saved}

Sebességkorlátozás esetén a rendszer menti:

- **Sync Checkpoints**: A pontos pozíció minden mappában, ahol a szinkron megállt.
- **Bandwidth Usage**: Az aktuális nap összesen letöltött byte mennyisége.
- **Limit State**: Történik-e limit elérési, és mikor áll vissza.

## 🗄️ Adatbázis karbantartás {#database-maintenance}

A `DatabaseMaintenanceService` automatikusan kitakarítja a régi adatokat:

- **Bandwidth Records**: 7 nap után törlődnek.
- **Sync Checkpoints**: A befejezett checkpointek 30 nap után törlődnek.
- **Incomplete Checkpoints**: Megmaradnak a szinkron befejeződéséig.

## ✅ Best Practices {#best-practices}

### Nagy postafiókok esetén {#for-large-mailboxes}

1. **Engedélyezze a sávszélesség-követést**: Alapvető fontosságú a napi limitet meghaladó postafiókoknál.
2. **Tervezze meg az elsődleges szinkronizálást**: Egy nagy postafiók teljes szinkronizálása több napot is igénybe vehet.
3. **Monitorozza a haladást**: Ellenőrizze a dashboardot a sebességkorlátozási állapot miatt.
4. **Ne szakítsa meg**: Hagyja, hogy az automatikus folytatás kezelje a folyamatot.

### Adminisztrátoroknak {#for-administrators}

1. **Állítson be megfelelő limiteket**: Az `DailyLimitMb` érték legyen összhangban az e-mail szolgáltató tényleges limiteivel.
2. **Monitorozza a logokat**: A figyelmeztető üzenetek a limit 80%-ánál jelennek meg.
3. **Adatbázis hely**: A sávszélesség-követés minimális extra tárolóhelyet igényel.
4. **Mentés módosítás előtt**: Mindig készítsen mentést a sebességkorlátozási beállítások módosítása előtt.

### Teljesítményi szempontok {#performance-considerations}

- ✅ **Memóriahatás**: Elhanyagolható – csak byte-okat követ, nem ment e-maileket.
- ✅ **Adatbázis-hatás**: Minimális – a kis rekordok automatikusan kitakarítódnak.
- ✅ **Szinkron sebesség**: Nincs teljesítményromlás a folyamatos szinkronizálás alatt.

## 🛡️ Transient FETCH Errors / Server-Side Throttling {#transient-fetch-errors-server-side-throttling}

A fent leírt bytealapú sávszélesség-követés mellett a Mail Archiver
azonosítja és reagál az IMAP szerver által jelezett **üzenetenkénti korlátozásra** (throttling) is. Egyes tárhelyszolgáltatók alkalmaznak fiókonkénti vagy IP-alapú korlátozást, amely **független a konfigurált `BatchOperation` szüneteltetésektől**, mivel a korlátozást a szerver oldalon alkalmazzák a jelenlegi munkamenet során bekövetkező kérések összes volumenét alapul véve.

### Tünetek {#symptoms}

A szerver `NO` (vagy `BAD`) választ ad egyetlen `FETCH` parancsra egy üzenettesthez, az alábbi üzenetekkel:

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- olyan válaszok, amelyek `throttle`, `rate limit` vagy hasonló jelzőket tartalmaznak.

Az autentikáció, a mappalista és maga az üzenőkeresés általában sikeres marad — csak az egyes teljes üzenetek lekérése sikertelenül zárul le.

### Automatikus kezelés {#automatic-handling}

Ha ilyen ideiglenes (transient) hiba történik, a Mail Archiver üzenetenkénti újrapróbálási stratégiát alkalmaz:

1. **Exponential backoff újrapróbálással**: Legfeljebb **3 újrapróbálás** ugyanarra az üzenetre, a következő késleltetésekkel: **5 s → 15 s → 60 s**.
2. **Kapcsolat helyreállítása**: Minden újrapróbálás előtt ellenőrzik a kapcsolatot, az autentikációt és a mappa-választási állapotot, és szükség esetén visszaállítják őket.
3. **Sikeres elérés esetén alaphelyzetbeállítás**: Egy sikeres FETCH visszaállítja a folyamatos hibás kérések számoló (consecutive-failure) értékét.
4. **Rendes szinkron szüneteltetése**: Ha **10 egymást követő üzenet** továbbra is ideiglenes hibával akad el az összes újrapróbálás után, a folyamatban lévő szinkron rendesen szünetel — ugyanazzal a mechanizmussal, mint a sávszélesség-limit szüneteltetése:
   - A `LastSync` időbélyeg **nem** frissül.
   - Ha `BandwidthTracking` engedélyezve van, a checkpointek megmaradnak, így a következő szinkron futás az utolsó sikeresen feldolgozott üzenettől folytatódik.
   - A feladat "rate-limited" állapotba kerül.
   - A következő ütemezett szinkron automatikusan folytatódik.

### Enyhéسítés {#mitigation}

Ha rendszeresen beleüt a szerver oldali korlátozásba, csökkentse a kérések gyakoriságát a `BatchOperation` beállítások módosításával:

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

Fontos megjegyezni, hogy egyes szolgáltatók nem másodpercenkénti gyakoriság alapján, hanem a **teljes byte mennyiség** vagy az **időszak alatti összes FETCH** alapján korlátoznak. Ilyen esetén:

- Engedélyezze a `BandwidthTracking` funkciót (lásd fent), és konfigurálja a `DailyLimitMb` értéket úgy, hogy az megfeleljen a szolgáltató dokumentált limitjének. A szinkron ilyenkor tisztán szünetel, amikor a napi keret elfogy, és a következő napon automatikusan folytatódik.
- Nagyon nagy postafiókok elsődleges szinkronizálása több napot is igénybe vehet, ami a tervezett működés része.

### Mi számít ideiglenes (transient) hibának {#what-counts-as-a-transient-error}

Csak az IMAP `NO`/`BAD` válaszok számítanak ideiglenesnek, ha azok korlátozással kapcsolatos szöveget vagy válaszkódot tartalmaznak. Egyéb hibák (hibás üzenetek, UTF-8 dekódolási problémák stb.) továbbra is azonnal `FailedEmails` státuszot kapnak, és **nem** váltanak triggered újrapróbálási/szüneteltetési logikát.

## 📚 Kapcsolódó dokumentáció {#related-documentation}

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Adatbázis tisztítási folyamatok
- [GmailBestPractices.md](GmailBestPractices.md) - Gmail-specifikus konfiguráció
