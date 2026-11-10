# 💾 Opslagweergave per Account {#per-account-storage-display}

[← Terug naar Documentatie Index](index.md)

## 📋 Overzicht {#overview}

De "Account Overview" tabel van het Dashboard en de "Show All" tabel van de MailAccounts bevatten een kolom **Opslag**. Deze kolom toont hoeveel databaseruimte elk account gebruikt — inclusal **alle velden van elke gearchiveerde mail** plus **alle bijlagen**.

## ⚙️ Hoe het werkt {#how-it-works}

Opslagwaarden worden **gecached** in de `AccountStorageCache` database tabel. Dit betekent dat de webpagina's vooraf berekende waarden lezen in plaats van telkens een dure query uit te voeren wanneer u het Dashboard of de accountlijst opent. De cache wordt up-to-date gehouden door een onafhankelijke achtergrondservice (`AccountStorageRefreshService`).

Voor elk account vraagt de service aan PostgreSQL om `pg_column_size` op te tellen voor elke kolom van de rijen van het account in de `ArchivedEmails` tabel. Dit omvat **alle velden van een mail** (Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash, etc.), inclusief PostgreSQL compressie en out-of-line opslag (TOAST). De grootte van een out-of-line waarde wordt gelezen van de TOAST-pointer, zodat de berekening alleen de tabel zelf scant en nooit de mail bodies laadt. Het bijlage gedeelte wordt apart toegevoegd als de logische som van `EmailAttachment.Size`.

De refresh service werkt de cache bij in drie situaties:

1. **Startup backfill** — Na een nieuwe installatie of na het updaten naar de versie waarin deze functie is geïntroduceerd, begint elk account zonder gecachte waarde. De service loopt alle accounts één voor één af en berekent de eerste waarde.
2. **Dagelijkse refresh** — Eén keer per dag op het geconfigureerde tijdstip (standaard `02:30`) berekent de service alle accounts opnieuw als veiligheidsnet.
3. **Onmiddellijke refresh** — Na een mail synchronisatie, EML import, MBOX import, of een verwijdering door bewaring, wordt het betreffende account direct opnieuw berekend zodat de weergegeven waarde actueel blijft.

## 🤔 Waarom er eerst "0 B" wordt getoond {#why-0-b-is-shown-at-first}

Wanneer de functie voor het eerst wordt ingeschakeld, of wanneer een nieuw account wordt aangemaakt, bestaat de cache-rij voor dat account nog niet. De refresh service draait op de achtergrond en berekent accounts **één voor één**, met een kleine vertraging tussen elk account zodat zeer grote archieven de database niet overbelasten.
Totdat de service een account heeft verwerkt, toont de UI **"0 B"** als placeholder. Dit is volkomen normaal. Zodra de achtergrondservice dat account heeft voltooid, verschijnt de echte waarde de volgende keer dat de pagina wordt geladen of ververst.
Voor grote bestaande archieven kan de backfill enkele minuten of langer duren. De service houdt de voortgang bij in een speciale tabel (`AccountStorageBackfillState`), dus als de applicatie halverwege de backfill wordt herstart, gaat deze gewoon verder waar hij gebleven was — geen enkel account wordt twee keer berekend.

## 🔧 Configuratie {#configuration}

De functie kan worden aangepast in `appsettings.json` of via omgevingsvariabelen. Zie de sectie **Account Storage Settings** in [Setup.md](Setup.md) voor alle beschikbare opties, inclusief:

- `AccountStorage__Enabled`: Schakel de refresh service in of uit (standaard `true`).
- `AccountStorage__DailyExecutionTime`: Tijd voor de dagelijkse volledige refresh (standaard `02:30`).
- `AccountStorage__BackfillDelayMs`: Pauze tussen accounts tijdens de startup backfill (standaard `5000` ms).
- `AccountStorage__RefreshBatchDelayMs`: Pauze tussen accounts tijdens de dagelijkse refresh (standaard `1000` ms).
- `AccountStorage__CommandTimeoutSeconds`: Database command timeout voor de berekening van de opslag per account (standaard `300` seconden). Verhoog dit voor zeer grote archieven waarbij de `pg_column_size` scan langer duurt.

> 💡 **Tip**: U hoeft `DatabaseMaintenance__Enabled` **niet** in te schakelen om deze functie te laten werken. De opslag refresh service draait onafhankelijk.
