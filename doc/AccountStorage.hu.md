# 💾 Egyenkénti tárhely megjelenítése {#per-account-storage-display}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

A Dashboard "Account Overview" táblázata és a MailAccounts "Show All" táblázata tartalmaz egy **Storage** oszlopot. Ez az oszlop mutatja, mennyi adatbázis-helyet használ minden egyes fiók — beleértve **minden archivált e-mail minden mezőjét**, valamint **minden csatolmányt**.

## ⚙️ Hogyan működik {#how-it-works}

A tárhelyértékek a `AccountStorageCache` adatbázis-táblázatban vannak **tárolva (cached)**. Ez azt jelenti, hogy a weboldalak előre kiszámolt értékeket olvasnak, ahelyett, hogy minden alkalommal drága lekérdezést futtatnának, amikor megnyitja a Dashboardot vagy a fióklistát. A gyorsítótárat egy független háttérszolgáltatás (`AccountStorageRefreshService`) tartja naprakész állapotban.

Fiókonként a szolgáltatás megkéri a PostgreSQL-t, hogy összeadja a `pg_column_size`-t a fiók sorainak minden oszlopának tekintetében a `ArchivedEmails` táblázatban. Ez lefedi **egy e-mail minden mezőjét** (Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash, stb.), beleértve a PostgreSQL tömörítését és a táblazón kívüli tárolást (TOAST). A táblazón kívüli érték mérete a TOAST mutatóból kerül kiolvasásra, így a számítás csak magát a táblázatot szkenneli, és soha nem tölti be az e-mail testekét. A csatolmányok részét külön veszik hozzá, mint a `EmailAttachment.Size` logikai összege.

A frissítő szolgáltatás három helyzetben frissíti a gyorsítótárat:

1. **Startup backfill** — Egy új telepítés után vagy az ezzel a funkcióval érkező verzióra való frissítés után minden fiók nulla gyorsítótárazott értékkel indul. A szolgáltatás egymás után végigjár minden fiókot, és kiszámolja az első értéket.
2. **Daily refresh** — Naponta egyszer, a konfigurált időpontban (alapértelmezett: `02:30`), a szolgáltatás biztonsági hálóként újraszámolja az összes fiókot.
3. **Immediate refresh** — E-mail szinkronizálás, EML importálás, MBOX importálás vagy megőrzési szabályok (retention) törlése után az érintett fiókot azonnal újraszámolja, hogy a megjelenített érték naprakész maradjon.

## 🤔 Miért jelenik meg először "0 B" {#why-0-b-is-shown-at-first}

Amikor a funkciót először engedélyezik, vagy amikor új fiókot hoznak létre, az adott fiókhoz tartozó gyorsítótárazott sor még nem létezik. A frissítő szolgáltatás a háttérben fut, és a fiókokat **egymás után** számolja ki, kis késéssel minden fiók között, hogy a nagyon nagy архівök ne terheljék túl az adatbázist.
Amíg a szolgáltatás el nem dolgozik egy fiókot, a felület a helyőrzőként a **"0 B"** értéket mutatja. Ez teljesen normális. Amint a háttérszolgáltatás befejezi azt a fiókot, a valódi érték a következő oldalbetöltés vagy frissítéskor megjelenik.
A meglévő nagy архів esetén a backfill több percet vagy annál többet is igénybe vehet. A szolgáltatás a haladását egy külön táblázatban követi (`AccountStorageBackfillState`), így ha az alkalmazást a backfill folyamata közepén újraindítják, egyszerűen onnan folytatja, ahol abbahagyta — egyetlen fiókot sem számol ki kétszer.

## 🔧 Konfiguráció {#configuration}

A funkció `appsettings.json` vagy környezeti változókon keresztül finomítható. Tekintse meg a [Setup.md](Setup.md) dokumentáció **Account Storage Settings** részét az összes elérhető opcióért, beleértve:

- `AccountStorage__Enabled`: A frissítő szolgáltatás be- vagy kikapcsolása (alapértelmezett: `true`).
- `AccountStorage__DailyExecutionTime`: Időpont a napi teljes frissítéshez (alapértelmezett: `02:30`).
- `AccountStorage__BackfillDelayMs`: Szünet a fiókok között a startup backfill során (alapértelmezett: `5000` ms).
- `AccountStorage__RefreshBatchDelayMs`: Szünet a fiókok között a napi frissítés során (alapértelmezett: `1000` ms).
- `AccountStorage__CommandTimeoutSeconds`: Adatbázis parancs timeout az egyenkénti tárhelyszámításhoz (alapértelmezett: `300` másodperc). Növelje ezt a nagyon nagy архівek esetén, ahol a `pg_column_size` szkennelés hosszabb ideig tart.

> 💡 **Tipp**: A funkció működéséhez **nem** szükséges engedélyezni a `DatabaseMaintenance__Enabled`-et. A tárhelyfrissítő szolgáltatás függetlenül fut.
