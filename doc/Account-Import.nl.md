# Account Import {#account-import}

[← Terug naar Documentatie Index](index.md)

## Overzicht {#overview}

Mail Archiver ondersteunt het importeren van meerdere e-mailaccounts tegelijk, zodat het onboarden van een groot aantal postbussen niet vereist dat elk account afzonderlijk wordt aangemaakt.

Er zijn twee paden voor bulkimport, afhankelijk van de mailprovider:

| Provider | Bulk Import Methode | Documentatie |
|---|---|---|
| **Microsoft 365 (tenant)** | Tenant discovery via Microsoft Graph — één app-registratie heeft toegang tot elke postbus in de tenant | [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) |
| **IMAP** | CSV bulkimport — upload een CSV-bestand met één rij per postbus | Deze pagina (zie hieronder) |

> **Let op:** Microsoft persoonlijke accounts (Outlook.com / M365 Family, provider **MSA**) gebruiken een interactieve device-code autorisatieflow en kunnen niet via bulkimport worden geïmporteerd. Ze moeten één voor één worden aangemaakt en geautoriseerd. Zie [MSA Outlook Setup](MSA_Outlook_Setup.md).

---

## IMAP CSV Bulkimport {#imap-csv-bulk-import}

### Wat het doet {#what-it-does}

De CSV bulkimport stelt een administrator in staat om honderden IMAP mailaccounts in één enkele operatie aan te maken door een CSV-bestand te uploaden. Elke rij in het bestand komt overeen met één postbus. Gemeenschappelijke serverinstellingen (IMAP host, poort, SSL) kunnen eenmalig in het uploadformulier worden opgegeven en zijn van toepassing op elke rij, met optionele overschrijvingen per rij.

De import is **alleen voor de administrator**. Deze is toegankelijk vanaf de **Mail Accounts** pagina via het dropdownmenu **Import** → **CSV Bulk Import**.

### CSV Bestandsformaat {#csv-file-format}

Het bestand moet een UTF-8 gecodeerde CSV zijn met een **headerrij** en een **komma (`,`) als scheidingsteken**. Velden die komma's of aanhalingstekens bevatten, moeten tussen dubbele aanhalingstekens staan (`"..."`).

#### Kolommen {#columns}

| Kolom | Vereist | Beschrijving |
|---|---|---|
| `email` | Ja | Postbusadres. Wordt ook gebruikt als de standaard login gebruikersnaam wanneer `username` niet wordt opgegeven. |
| `password` | Ja | IMAP inlogwachtwoord. Wordt opgeslagen zoals opgegeven — voorafgaande of achterafgaande spaties blijven behouden. |
| `name` | Nee | Weergavenaam voor het account. Als dit wordt weggelaten, is de standaardwaarde `{prefix} - <{email}>`. |
| `username` | Nee | Login gebruikersnaam als deze afwijkt van het e-mailadres. |
| `imap_server` | Nee | Per rij IMAP host. Overschrijft de gemeenschappelijke server uit het uploadformulier. |
| `imap_port` | Nee | Per rij IMAP poort (1–65535). Overschrijft de gemeenschappelijke poort. |
| `use_ssl` | Nee | Per rij SSL toggle (`true` of `false`). Overschrijft de gemeenschappelijke instelling. |

#### Voorbeeld {#example}

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- Rij 1 gebruikt de gemeenschappelijke server/poort/SSL van het uploadformulier.
- Rij 2 stelt een aangepaste gebruikersnaam in, maar gebruikt verder de gemeenschappelijke instellingen.
- Rij 3 overschrijft server, poort en SSL.

Een downloadbaar voorbeeldbestand is beschikbaar op de importpagina via de knop **Download example CSV**.

### Uploadformulier {#upload-form}

| Veld | Doel |
|---|---|
| **CSV file** | Het te uploaden bestand. |
| **IMAP server** | Gemeenschappelijke host voor alle rijen zonder een `imap_server` overschrijving. |
| **IMAP port** | Gemeenschappelijke poort (standaard 993). |
| **Use SSL** | Gemeenschappelijke SSL toggle (standaard aan). |
| **Name prefix** | Prefix voor automatisch gegenereerde accountnamen. Standaard: `IMAP`. |
| **Skip existing mailboxes** | Wanneer ingeschakeld, worden rijen waarvan het e-mailadres al als IMAP-account bestaat overgeslagen (geteld als "geslagen", niet als fouten). Wanneer uitgeschakeld, worden ze behandeld als mislukkingen. |
| **Account enabled** | Of de aangemaakte accounts geactiveerd moeten worden voor achtergrond synchronisatie. |
| **Delete After Days / Local Retention Days** | Optionele instellingen voor Bewaring die worden toegepast op elk aangemaakt account. Zie [Retention Policies](RetentionPolicies.md). |

### Verwerking {#processing}

1. Het CSV-bestand wordt geparsed en elke rij wordt gevalideerd (e-mailformaat, aanwezigheid wachtwoord, poortbereik, SSL-waarde).
2. Dubbele e-mails binnen het bestand worden verwijderd (de eerste instantie wint).
3. Bestaande IMAP-accounts met hetzelfde e-mailadres worden gedetecteerd; afhankelijk van de **Skip existing** toggle worden ze ofwel overgeslagen of gemeld als mislukkingen.
4. Alle nieuwe accounts worden in een enkele database batch ingevoegd.
5. Er wordt een logboekvermelding geschreven die de uitvoering samenvat.

Er wordt geen IMAP-verbindingstest uitgevoerd tijdens de import — accounts worden onmiddellijk opgeslagen. Authenticatieproblemen worden pas zichtbaar tijdens de eerste achtergrond synchronisatie. Dit houdt de import snel, zelfs voor honderden postbussen.

### Resultatenpagina {#result-page}

Na voltooiing van de import toont een resultatenpagina drie tabellen:

- **Created** — e-mail en naam van elk account dat is aangemaakt.
- **Skipped** — e-mails die duplicaten waren (in het bestand of al bestaand) wanneer **Skip existing** is ingeschakeld.
- **Failed** — rijen die niet konden worden verwerkt, met het lijnnummer en de reden (ontbrekend e-mailadres, ontbrekend wachtwoord, ongeldige poort, geen IMAP-server, etc.).

Een samenvattingsbanner toont de aantallen: *{created} aangemaakt, {skipped} overgeslagen, {failed} mislukt*.

### Limieten {#limits}

De volgende limieten kunnen worden geconfigureerd via `appsettings.json` / omgevingsvariabelen (zie [Setup Guide](Setup.md) → *CSV Import Settings*):

| Instelling | Standaard | Beschrijving |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | Maximaal aantal verwerkte rijen per upload. |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | Maximale grootte van het geüploade bestand. |

### Beveiligingsnotitie {#security-note}

IMAP-wachtwoorden worden in de database op dezelfde manier opgeslagen als voor afzonderlijk aangemaakte IMAP-accounts. Houd er rekening mee dat het CSV-bestand alle IMAP-wachtwoorden in platte tekst bevat — verwijder het van de client machine na een succesvolle import.

---

## Microsoft 365 Tenant Import {#microsoft-365-tenant-import}

Voor Microsoft 365 (Exchange Online) vereist de bulkimport geen CSV-bestand. Een enkele Azure AD app-registratie met de juiste Graph-machtigingen kan toegang krijgen tot elke postbus in de tenant, zodat Mail Archiver de postbussen automatisch kan ontdekken.

Zie de speciale gids: **[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**.
