# 📧 Gmail Best Practices {#gmail-best-practices}

[← Visszamenet a Dokumentációs Indexhez](index.md)

Amikor a Mail Archiver alkalmazással Gmail fiókokat használ, számos olyan specifikus konfigurációt és legjobb gyakorlatot kell követni, hogy biztosítsuk az optimális teljesítményt és elkerüljük a potenciális problémákat.

## 🔐 IMAP konfiguráció App Password-szemel {#imap-configuration-with-app-passwords}

A Gmail fiókok az IMAP protokoll és App Password használatával integrálhatók a Mail Archiver alkalmazásba:

1. Engedélyezze a 2-szoros hitelesítést (2-Factor Authentication) a Gmail fiókjában
2. Generáljon egy kifejezetten a Mail Archiver alkalmazáshoz tartozó App Password-t
3. A Mail Archiver-ben történő fiókkonfiguráció során az App Password-t használja a reguláris Gmail jelszavával szemben

## 🏷️ Gmail Label beállítások {#gmail-label-settings}

Erősen javasolt a Gmail címke (label) beállítások módosítása annak érdekében, hogy elkerülje az "All Mail" címke IMAP-on keresztüli látszódását:

1. Nyissa meg a Gmail webfelületét, és nyissa meg a beállításokat (a kis fogaskerék ikon a tetején) → See all settings → Labels fül
2. Keresse meg az "All Mail" címkét a listában
3. Eltörölje az "Show in IMAP" opciót az "All Mail" esetében

### 📚 Háttérinformáció a Gmail Label implementációról {#background-on-gmail-label-implementation}

A Gmail címke-rendszere eltér a hagyományos e-mail szolgáltatóktól. A Gmailben:
- Minden e-mail egyetlen "All Mail" tárhelyben található meg
- A címkék (labels) inkább tagekként működnek, mint mappaként
- Az "All Mail" címke tartalmaz minden e-mailt a fiókjából
- Amikor egy e-mailt címkéznek, az megjelenik az eredeti mappában és a címkézett szekcióban is. Így a beérkező postában (inbox) lévő összes e-mail az "All Mail" címkében is szerepel.

### ⚠️ Problémák az IMAP lekéréssel {#issues-with-imap-retrieval}

Ha az "All Mail" látható az IMAP-on keresztül, az több problémát is okozhat:

1. **Duplikált feldolgozás**: Az archiver többször is feldolgozhatja ugyanazt az e-mailt, mivel az több mappában/címkében is szerepel
2. **Teljesítményhatás**: A "All Mail" lekérése rendkívül lassú lehet több ezer e-maillel rendelkező nagy fiókok esetén
4. **Szinkronizációs problémák**: Konfliktusok alakulhatnak fel, amikor meg kell határozni, hogy egy e-mail valójában melyik mappába tartozik

Az "All Mail" IMAP-os hozzáférésének kikapcsolásával biztosítja, hogy az e-maileket csak egyszer lekérjék az elsődleges helyükről, javítva ezzel a teljesítményt és a pontosságot is.

## 🗑️ Retention Policy és Expunge beállítások {#retention-policy-and-expunge-settings}

Ahogy azt a fő README fájlban említettük, a Gmail fiókok különleges figyelmet igényelnek a megőrzési irányelvek (retention policies) tekintetében:

- A Gmail "Auto-Expunge" funkcióját ki kell kapcsolni a Gmail beállításokban a "Forwarding and POP/IMAP" fül alatt
- Ez a beállítás döntő fontosságú ahhoz, hogy a megőrzési irányelvek megfelelően működjenek a Gmail fiókokkal

A beállítás konfigurálásához:
1. Menjen a Gmail Settings → See all settings → Forwarding and POP/IMAP fülre
2. Az "IMAP Access" szekcióban válassza az "Auto-Expunge off - Wait for the client to update the server." és a "Move the message to the Bin" opciókat

## ⚠️ Gmail IMAP Rate Limiting {#gmail-imap-rate-limiting}

A Gmail napi 2500MB-os limitet áp a IMAP lekérésekre. Nagy mennyiségű e-mail archiválásakor tudatában kell lennie annak, hogy elérheti ezt a limitet, ami ideiglenesen felfüggesztheti az e-mail lekérést a következő napig.

### 📊 Rate Limit kezelés {#rate-limit-handling}

A Mail Archiver beépített rate limit kezeléssel rendelkezik a Gmail IMAP sávszélességi korlátaihoz:

- **Automatikus sávszélesség figyelés**: Figyeli a fiókonkénti, napi szintű letöltött adatmennyiséget
- **Konfigurálható limitek**: Állítson be `BandwidthTracking__DailyLimitMb=2500` a Gmail számára
- **Automatikus szüneteltetés**: A szinkronization szünetel, amikor elérhető a limit, és folytatódik a reset után
- **Checkpoint rendszer**: A szinkronizációs folyamat mentésre kerül, és az utolsó pozícióból tud folytatódni

A részletes konfigurációért és használatért lásd a **[Rate Limit Handling Guide](RateLimitHandling.md)** dokumentációt.

### 🔧 Gmail Configuration Example {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 Nagy Postafiókoknál teendők {#large-mailbox-considerations}

Nagy Gmail postafiókok esetén (pl. 14 GB), az első szinkronization több napot vesz igénybe:

| Mailbox Size | Days to Complete (at 2500 MB/day) |
|--------------|-----------------------------------|
| 5 GB | ~2 days |
| 10 GB | ~4 days |
| 14 GB | ~6 days |
| 20 GB | ~8 days |

**Tipp**: Kapcsolja be a sávszélesség figyelést (bandwidth tracking) **mielőtt** nagy Gmail fiókokat ad hozzá, hogy az elejétől kezdve megfelelően kezelje a rate limitet.

## ✅ Best Practices Summary {#best-practices-summary}

Ezen legjobb gyakorlatok betartása segít biztosítani a Mail Archiver zökkenőmentes működését a Gmail fiókaival, miközben megőrzi az adatok integritását és az optimális teljesítményt.
