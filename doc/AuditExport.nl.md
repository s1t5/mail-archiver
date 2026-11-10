# 📤 Audit Data Export {#audit-data-export}

[← Terug naar Documentatie Index](index.md)

## 📋 Overzicht {#overview}

De audit data export maakt tabelvormige massadata-pakketten (ZIP met `INDEX.XML` + CSV-tabellen + index DTD) van het bestaande Archief. Deze pakketten volgen de vastgestelde indexstructuur voor de overdracht van datamedia en kunnen direct worden geïmporteerd door veelgebruikte externe audit- en analyse-tools die dit indexformaat ondersteunen.

De functie is beschikbaar als een **speciale pagina bereikbaar vanaf de Logs-pagina** en is beperkt tot admin-gebruikers. Elke exportrun wordt opgenomen in het access log, zodat de exportgeschiedenis revisiebestendig is.

## 🧭 Toegang {#access}

1. Log in als een admin-gebruiker
2. Open de **Logs**-pagina
3. Klik op de knop **Audit Data Export** in de rechterbovenhoek

Niet-admin gebruikers zien de knop niet en kunnen geen van de export-endpoints aanroepen (de server wijst deze af).

## 📦 Structuur van het Exportpakket {#export-package-structure}

Het gegenereerde ZIP-bestand heet `audit-export-<timestamp>.zip` en bevat:

| Bestand | Inhoud |
|---|---|
| `INDEX.XML` | Indexbestand dat de dataleverancier, het medium en alle tabel/kolomdefinities beschrijft |
| `index.dtd` | De DTD waarnaar het indexbestand verwijst; vereist door importtools voor validatie |
| `emails.csv` | Eén rij per gearchiveerde e-mail in de geselecteerde periode/Postbus |
| `attachments.csv` | Optioneel; één rij per Bijlage van de geëxporteerde e-mails |

### Tabel `emails.csv` {#table-emailscsv}

Kolommen (geen headerrij; de kolomnamen komen van `INDEX.XML`, zoals gebruikelijk voor dit formaat):

| Kolom | Beschrijving |
|---|---|
| Id | Interne e-mail ID (numeriek) |
| MessageId | Email Message-ID header |
| SentDate | Verzenddatum, ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`) |
| ReceivedDate | Ontvangstdatum, ISO 8601 UTC |
| IsOutgoing | `1` = uitgaand, `0` = inkomend |
| From | Afzender |
| To | Ontvangers |
| Cc | CC-ontvangers |
| Bcc | BCC-ontvangers |
| Subject | Onderwerp |
| FolderName | Map waarin de e-mail is gearchiveerd |
| HasAttachments | `1` = met Bijlagen, `0` = zonder |
| AttachmentCount | Aantal Bijlagen (numeriek) |
| AccountEmail | E-mailadres van de archiverende Postbus |

### Tabel `attachments.csv` {#table-attachmentscsv}

| Kolom | Beschrijving |
|---|---|
| EmailId | Interne ID van de e-mail waartoe de Bijlage behoort (join key naar `emails.csv`) |
| FileName | Bestandsnaam van de Bijlage |
| ContentType | MIME-type |
| Size | Grootte in bytes (numeriek) |
| Sha256 | SHA-256 hash van de inhoud (van de content-addressed Bijlage opslag) |

### CSV Conventies {#csv-conventions}

- Encoding: UTF-8 **zonder** BOM (gedeclareerd als `UTF8` in `INDEX.XML`)
- Kolomscheidingsteken: `;`
- Tekstomsluiting: `"` (verdubbeld voor escaping)
- Record scheidingsteken: `\n` (expliciet gedeclareerd in `INDEX.XML`)
- Datums: ISO 8601, tijden geconverteerd van de geconfigureerde weergave-tijdzone naar UTC
- Geen headerrij; geen gelokaliseerde getallen (numerieke kolommen bevatten gewone integers)

## 🖥️ Gebruik van de Exportpagina {#using-the-export-page}

### Formuliervelden {#form-fields}

1. **Van / Naar datum** - Periode om te exporteren (verplicht, maximale periode configureerbaar, standaard 10 jaar)
2. **Postbus** - Beperk de export tot een enkele Postbus of export alle Postbussen
3. **Bijlage metadata** - Wanneer aangevinkt, wordt `attachments.csv` toegevoegd aan het pakket
4. **Naam dataleverancier / locatie / commentaar** - Geschreven in het `DataSupplier` blok van `INDEX.XML`; vooraf ingevuld vanuit de `AuditExport` configuratiesectie en bewerkbaar per export

### Levenscyclus van de taak {#job-lifecycle}

1. Door op **Start Export** te klikken, wordt de periode en de Postbus gevalideerd, wordt een taak aangemaakt en keert de pagina terug naar de startpagina
2. De taak draait op de achtergrond (gequeueerde exports worden één voor één verwerkt); de taastabel toont live voortgang (verwerkte/totaal aantal e-mails met een voortgangsbalk) via automatische polling
3. Wanneer de export klaar is, verschijnt de knop **Download** in de rij van de taak
4. Lopende of gequeueerde taken kunnen worden **geannuleerd**

### Exportgeschiedenis {#export-history}

De tabel onder het formulier toont de meest recente exports met tijdstempel, gebruiker, periode, Postbus, aantal e-mails, status en bestandsgrootte. De geschiedenis wordt **bewaard in de database** (`AuditExportJobs` tabel), waardoor deze een app-herstart overleeft en revisiebestendig is.

Gedownloadde bestanden worden bewaard voor een configureerbare Bewaring-venster (standaard 30 dagen, zie `AuditExport__RetentionDays`); daarna worden de ZIP-bestanden verwijderd door de dagelijkse opschoning, terwijl de rijen in de geschiedenis blijven staan.

> 📝 **Opmerking**: Exportbestanden worden opgeslagen in de applicatiecontainer. Als u deze op de lange termijn wilt bewaren, download ze dan en sla ze op in uw audit-bewijslast repository.

## 🔐 Auditing {#auditing}

Elke export produceert twee toegangseintredes van het nieuwe type **Audit Data Export** in het access log:

1. **Start-entry** - wanneer de export in de wachtrij staat (met job ID, periode, Postbus, opties als JSON)
2. **Resultaat-entry** - wanneer de export voltooid is, is mislukt of is geannuleerd (met job ID, periode, Postbus, aantal e-mails, resultaat en pakketgrootte als JSON)

De entries verschijnen op de Logs-pagina zoals alle andere toegangseintredes in het access log en kunnen worden gefilterd op het nieuwe type. De type-filter dropdown pakt deze automatisch op.

## ⚙️ Configuratie {#configuration}

De `AuditExport` sectie in `appsettings.json` (omgevingsvariabelen gebruiken de dubbele underscore syntaxis, bijv. `AuditExport__RetentionDays`):

| Instelling | Beschrijving | Standaard |
|---|---|---|
| `DataSupplierName` | Standaardwaarde voor het veld naam dataleverancier | leeg |
| `DataSupplierLocation` | Standaardwaarde voor het veld locatie dataleverancier | leeg |
| `Comment` | Standaardwaarde voor het veld commentaar | leeg |
| `OutputDirectory` | Map voor de gegenereerde ZIP-bestanden (relatieve paden worden opgelost tegen de app content root) | `exports/audit` |
| `RetentionDays` | Dagen totdat voltooide exportbestanden worden verwijderd | `30` |
| `MaxRangeYears` | Maximale toegestane span tussen startdatum en einddatum van de periode | `10` |

## 🗄️ Database {#database}

De exportgeschiedenis wordt opgeslagen in de `mail_archiver."AuditExportJobs"` tabel (gemaakt door migratie `MigrateV2609_1`, automatisch toegepast bij opstarten). Bestaande tabellen worden niet gewijzigd door deze functie.

## 🔍 Problemen oplossen {#troubleshooting}

| Probleem | Oorzaak / Oplossing |
|---|---|
| Downloadknop ontbreekt na herstart | Exportbestanden leven in de container; als de container opnieuw is aangemaakt of het Bewaring-venster is verstreken, is de ZIP weg terwijl de geschiedenisrij blijft staan. Voer de export opnieuw uit. |
| "De periode mag niet langer zijn dan X jaar" | Verkort de periode of verhoog `AuditExport__MaxRangeYears`. |
| Taak blijft lange tijd op *Running* staan | Grote archieven worden gestreamd in batches van 1000 e-mails; voortgang wordt continu bijgewerkt. Zeer grote periodes kunnen even duren — controleer de teller van verwerkt/totaal. |
| Importtool rapporteert encodingproblemen | Het pakket is UTF-8 zonder BOM met een gedeclareerde `UTF8` flag en expliciet record scheidingsteken; zorg ervoor dat de tool de `INDEX.XML` leest naast de databestanden (extracteer de hele ZIP, open niet de bestanden afzonderlijk vanuit de ZIP). |

## 🔗 Gerelateerde Documentatie {#related-documentation}

- [Access Logging](Logs.md)
- [Installatie, Setup en Parameters](Setup.md)
