# 📤 Een Mail Account exporteren (EML / MBox) {#exporting-a-mail-account-eml-mbox}

[← Terug naar de Documentatie-index](index.md)

## 📋 Overzicht {#overview}

De accountexport schrijft **elke bericht van één account** weg naar een
downloadbaar ZIP-bestand. Het is het tegenovergestelde van de
[web import](Import-EML-MBox.md): je krijgt een draagbaar Archief van het account in een
formaat dat elke mailclient of migratietool kan lezen.

Er worden twee formaten aangeboden:

| Formaat | Wat je krijgt | Typisch gebruik |
|--------|--------------|-------------|
| **EML** | Individuele `.eml`-bestanden, georganiseerd in mappen | Opnieuw importeren in een mailclient, verwerking per bericht, leesbaarheid op lange termijn |
| **MBox** | Traditionele Postbus-bestanden — **één bestand per map** | Tools en clients die MBox lezen, bulk tekstverwerking |

In beide gevallen worden de berichten geschreven als ruwe RFC-822-berichten, dus **bijlagen
zijn inbegrepen** in de export.

> 💡 **Tip**: Om alleen een selectie van berichten te exporteren in plaats van een heel account,
> gebruik je de bulkacties in [Mail search](Search.md#-acting-on-many-results-at-once).

## 🧭 Toegang {#access}

1. Open **Mail Accounts** in het hoofdmenu.
2. Klik op de **⋮** (meer acties) knop op de accountrij en kies
   **Export**. Dezelfde optie staat op de **Details**-pagina van het account.

Exporteren vereist de **admin** of **self manager** rol — standaardgebruikers zien
de optie wel, maar kunnen deze niet gebruiken. De export omvat altijd het hele account; er is
geen datum- of mapfilter.

## 🖥️ De Export-pagina gebruiken {#using-the-export-page}

De pagina toont het **totaal**, **inkomend** en **uitgaand** aantal berichten van het account,
zodat je de omvang van de taak kunt zien voordat je deze start. Daarna:

1. Kies het formaat — **EML** of **MBox**.
2. Druk op **Start Export**.

De export draait **op de achtergrond**. Je wordt doorgeleid naar een statuspagina met de
job ID, het gekozen formaat, de tijdstempels en de verwerkte/totale berichtenaantallen.

### Jobstatussen {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

Wanneer de taak de status **Completed** bereikt, verschijnt er een **downloadknop** op de statuspagina. Dezelfde taak staat ook vermeld op de [Jobs](BackgroundJobs.md) pagina.

> ⚠️ **Exportbestanden worden automatisch opgeruimd.** De ZIP wordt verwijderd **na een
> succesvolle download**, en in ieder geval **na 7 dagen**. Download het bestand terwijl
> het nog wordt aangeboden — het Archief zelf blijft ongewijzigd, je kunt later gewoon
> een nieuwe export starten.

## ⚠️ Aandachtspunten {#things-to-watch-out-for}

- **Grote accounts duren even.** Een grote Postbus kan enkele minuten in beslag nemen en de
  ZIP kan erg groot worden, afhankelijk van het berichtvolume en de bijlagen.
  Omdat de taak op de achtergrond draait, kun je de browser sluiten en later
  terugkeren naar de statuspagina.
- **Schijfruimte op de server.** De ZIP wordt op de server samengesteld voordat je deze downloadt,
  zorg er dus voor dat er voldoende vrije ruimte beschikbaar is naast het Archief.
- **"Export file not found"** — de taak is nog niet voltooid, of het bestand is
  al gedownload of verwijderd door de 7-daagse opschoonactie. Start een nieuwe export.
- **Geen filteropties** — de export is alles-of-niets per account. Gebruik de
  export van de zoekpagina voor een subset.
- **Geen compliance-export.** Gebruik voor een audit-gereed data-media overdracht met een
  indexbestand de [Audit Data Export](AuditExport.md) — deze produceert een
  pakket in het tabelvormige massadataformaat dat audittools verwachten.

## 🔗 Gerelateerde Documentatie {#related-documentation}

- [Importing Mail in the Web UI](Import-EML-MBox.md) — de omgekeerde richting
- [Mail search](Search.md) — het exporteren van een selectie berichten
- [Background Jobs](BackgroundJobs.md) — taaklijst, voortgang en annuleren
- [Audit Data Export](AuditExport.md) — audit-gereed exportpakketten
- [CLI commands](CLI.md) — importeren vanuit een gemounte directory
