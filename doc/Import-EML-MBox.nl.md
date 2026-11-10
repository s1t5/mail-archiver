# 📥 Mail importeren in de Web UI (EML / MBox) {#importing-mail-in-the-web-ui-eml-mbox}

[← Terug naar de Documentatie-index](index.md)

## 📋 Overzicht {#overview}

Naast de [command line interface](CLI.md) en de
[CSV account import](Account-Import.md), kan Mail Archiver een bestaand
mail-Archief **via de webinterface** importeren. Er zijn twee uploadformulieren:

| Formulier | Geaccepteerd bestand | Resultaat |
|------|---------------|--------|
| **Import MBox** | `.mbox`, `.mbx` | Elke bericht in het bestand wordt geïmporteerd in **één** Map die u kiest |
| **Import EML** | `.zip` met `.eml` bestanden | De **mapstructuur binnen de ZIP** bepaalt waar elk bericht terechtkomt |

Beide formulieren archiveren de berichten in een bestaand Mail Archiver Account, beide draaien als
**background jobs**, en beide **overslaan duplicaten automatisch** — het twee keer importeren
van hetzelfde bestand maakt geen tweede kopie aan.

> 💡 **Tip**: Als uw Archief al als bestand op de server staat, heeft de
> [CLI](CLI.md) de voorkeur. Dit voorkomt de upload via de browser en is de
> betere keuze voor Archieven van meerdere gigabytes.

## 🧭 Toegang {#access}

1. Open **Mail Accounts** in het hoofdmenu.
2. Gebruik het **Import** dropdownmenu boven de accountlijst en kies
   **Import MBox** of **Import EML**.

De formulieren zijn beschikbaar voor **admins** (alle accounts) en **self managers**
(de accounts die aan hen zijn toegewezen); zie
[User Management](UserManagement.md#-user-account-permissions). Alleen **ingeschakelde**
accounts worden aangeboden als importdoelen — een uitgeschakeld account kan niet worden geselecteerd.

## 📨 Import MBox {#import-mbox}

MBox is het formaat dat door de meeste mailclients wordt gebruikt voor geëxporteerde mappen, en door
`mbox`-stijl Back-ups.

| Veld | Betekenis |
|-------|---------|
| **MBox File** | Het `.mbox` / `.mbx` bestand om te uploaden |
| **Target Account** | Het Mail Archiver Account dat de berichten ontvangt |
| **Target Folder** | De Map waarin de berichten worden geïmporteerd. Kies één uit de bestaande mappenlijst van het account, of typ een nieuwe naam — een Map die nog niet bestaat, wordt **automatisch aangemaakt** tijdens de import. Standaard `INBOX`. |

Alle berichten in het bestand worden geïmporteerd in die enkele doelmap. Het MBox-
bestand zelf bevat geen mapinformatie, dus de structuur van de oorspronkelijke
Postbus wordt niet gereconstrueerd — als u dat nodig heeft, gebruik dan de EML/ZIP-route.

## 🗂️ Import EML {#import-eml}

EML-import verwacht een **ZIP-archief** waarvan de items `.eml` bestanden zijn.

- `.eml` bestanden in de **root** van de ZIP worden geïmporteerd in `INBOX`.
- `.eml` bestanden in een **submap** worden geïmporteerd in een Map met de **dezelfde
  naam** als die submap.

Dus een ZIP die als volgt is opgebouwd:

```
mailbox.zip
├── 2024-invoice.eml            → INBOX
├── Archive/
│   └── old-message.eml         → Archive
└── Projects/
    └── 2026/
        └── kickoff.eml         → Projects/2026
```

herstelt de mappenboom samen met de berichten. Individuele `.eml` bestanden kunnen
worden geüpload door ze eerst in een ZIP te verpakken (`zip emails.zip *.eml`).

## ⚙️ Gedeeld gedrag {#shared-behaviour}

Beide formulieren gedragen zich op dezelfde manier zodra u op **Start Import** drukt:

- **Bestandsgrootte limiet** — geconfigureerd met `Upload__MaxFileSizeGB` (standaard **10 GB**).
  Het formulier toont de huidige limiet naast de bestandsselectie.
- **Achtergrondverwerking** — de upload wordt in de wachtrij geplaatst als een job; u hoeft
  de browser niet open te houden.
- **Afhandeling van duplicaten** — berichten die al in het Archief staan, worden overgeslagen
  en apart geteld. Duplicaten worden gematcht op de `Message-ID` header.
- **Ongeldige invoer** — onleesbare bestanden worden individueel overgeslagen en gerapporteerd op
  de statuspagina in plaats van de hele import af te breken.
- **Voortgang** — u wordt direct naar de jobstatuspagina geleid, die de
  job ID, bestandsnaam, bestandsgrootte, status, verwerkte/totale berichten en de lopende
  tellingen toont. Dezelfde job verschijnt ook op de [Jobs](BackgroundJobs.md) pagina.
- **Annuleren** — een lopende import kan worden geannuleerd vanaf de statuspagina. De
  berichten die tot dat moment zijn geïmporteerd, blijven in het Archief.
- **Audit trail** — de import wordt geschreven naar het [access log](Logs.md), en de
  opslagwaarde per account wordt daarna bijgewerkt (zie
  [Per-Account Storage](AccountStorage.md)).

### Jobstatussen {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled`

Een voltooide job rapporteert hoeveel berichten zijn geïmporteerd, hoeveel zijn mislukt en hoeveel
zijn overgeslagen als duplicaten — bijvoorbeeld
*"1420 geïmporteerd, 3 mislukt, 87 duplicaten"*. Bij een succesvolle import wordt het geüploade
bestand van de server verwijderd; mislukte items worden bewaard voor de levensduur van de
job, zodat u kunt zien wat er is geweigerd.

## ⚠️ Aandachtspunten {#things-to-watch-out-for}

- **Verkeerd bestandstype** — de upload wordt geweigerd, tenzij de extensie een van
  `.mbox`, `.eml` of `.zip` is. De selector van het EML-formulier accepteert alleen `.zip`, de MBox-
  formulier alleen `.mbox` en `.mbx`.
- **"0 geïmporteerd, N duplicaten"** — dit is geen fout. De berichten staan al
  in het Archief, hoogstwaarschijnlijk door een eerdere import of Synchronisatie van dezelfde bron.
- **Zeer grote Archieven** — een MBox-bestand wordt bericht voor bericht gelezen, dus een
  bestand van meerdere gigabytes duurt even, zelfs op de achtergrond. Houd de voortgang in de
  gaten op de statuspagina in plaats van opnieuw te uploaden.
- **Beschikbare schijfruimte** — het geüploade bestand wordt tijdelijk op de server opgeslagen en
  heeft ruimte nodig naast uw Archief-database.
- **Mapnamen met speciale tekens** — mapnamen worden overgenomen uit de ZIP-
  paden of uw invoer; controleer de mappenlijst van het account na de import als u
  ongebruikelijke tekens heeft gebruikt.

## 🔗 Gerelateerde Documentatie {#related-documentation}

- [CLI commands](CLI.md) — importeren vanuit een gemounte directory
- [Account Import (CSV)](Account-Import.md) — veel accounts tegelijk toevoegen
- [Background Jobs](BackgroundJobs.md) — het monitoren en annuleren van importjobs
- [Exporting a Mail Account](Export-Account.md) — de omgekeerde richting
- [Setup and Parameters](Setup.md) — `Upload__*` instellingen
