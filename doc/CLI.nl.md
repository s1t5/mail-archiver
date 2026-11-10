# 🖥️ CLI Commands {#cli-commands}

[← Terug naar de Documentatie-index](index.md)

## 📋 Overzicht {#overview}

Sommige taken horen niet thuis in de web UI: het importeren van een bestaand Archief vanaf het lokale bestandssysteem, of het verplaatsen van opgeslagen originele berichten tussen opslag-backends. Mail Archiver accepteert beide als commando's op de command line.

| Commando | Doel |
|----------|---------|
| `--import-mbox` | Importeer een MBox-bestand vanuit een gemounte directory |
| `--import-eml` | Importeer een ZIP-archief van EML-bestanden vanuit een gemounte directory |
| `--migrate-raw-storage` | Verplaats opgeslagen originele berichten tussen de database en het bestandssysteem |

Alle commando's worden afgehandeld voordat de web pipeline start en stoppen wanneer ze klaar zijn, dus ze kunnen veilig worden uitgevoerd met `docker compose exec` terwijl het Archief draait.

## ⚠️ Beveiligingsmodel {#security-model}

De importcommando's zijn **alleen via de CLI** – er is geen web-endpoint voor het browsen of importeren van lokale bestanden. U moet toegang hebben tot de Docker host (`docker exec`) om ze uit te voeren.

Een **path whitelist** (`LocalImport:AllowedPaths`) bepaalt welke directory's de importer kan lezen. Bestanden buiten deze paden worden geweigerd.

`--migrate-raw-storage` is niet onderworpen aan die whitelist: het leest alleen de berichtbestanden die het Archief zelf heeft geschreven onder `RawMessages:FileSystem:BasePath`.

## 🛠️ Setup {#setup}

### 1. Configureer toegestane paden {#1-configure-allowed-paths}

Stel in uw `docker-compose.yml` de toegestane importpaden in:

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. Mount uw bestanden {#2-mount-your-files}

Mount de directory die uw MBox/EML-bestanden bevat in de container:

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

Vervang `/home/user/mbox-archives` door het werkelijke pad op uw Docker host.

### 3. Herstart de container {#3-restart-the-container}

```bash
docker compose down && docker compose up -d
```

## 📝 Gebruik {#usage}

### Zoek de ID van je doel-Account {#find-your-target-account-id}

Voordat je importeert, heb je de database-ID van de doel-mailaccount nodig. Je kunt deze vinden via de Web UI:

Navigeer naar "Email Accounts", open de details van de doel-account; de account ID wordt weergegeven in de accountlijst of in de URL wanneer je een account bekijkt.

### Importeer een MBox-bestand {#import-an-mbox-file}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**Parameters:**
| Parameter | Vereist | Beschrijving |
|-----------|----------|-------------|
| `--import-mbox` | Ja | Signaleert een MBox-importoperatie |
| `--file <path>` | Ja | Pad naar het MBox-bestand binnen de container |
| `--account-id <id>` | Ja | Database ID van de doel-mailaccount |
| `--folder <name>` | Nee | Naam van de doel-Map (standaard: `INBOX`) |

### Importeer EML-bestanden (ZIP Archief) {#import-eml-files-zip-archive}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**Parameters:**
| Parameter | Vereist | Beschrijving |
|-----------|----------|-------------|
| `--import-eml` | Ja | Signaleert een EML-importoperatie |
| `--file <path>` | Ja | Pad naar een **ZIP Archief** met daarin `.eml` bestanden binnen de container |
| `--account-id <id>` | Ja | Database ID van de doel-mailaccount |

> **Belangrijk:** `--import-eml` verwacht een **ZIP Archief** met daarin `.eml` bestanden — het accepteert **geen** individuele `.eml` bestanden. Om afzonderlijke EML-bestanden te importeren, moet je ze eerst in een ZIP Archief plaatsen:
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **Opmerking voor `dotnet run`:** Bij het gebruik van `dotnet run` (in plaats van de gepubliceerde DLL), scheid de argumenten van dotnet van de argumenten van de applicatie met `--`:
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> De `--` vertelt `dotnet run` dat alle volgende argumenten bij jouw applicatie horen, en niet bij de .NET compiler.

## 📊 Voorbeeldoutput {#example-output}

```
Target account: user@example.com (ID: 1)

=== Local MBox Import ===
File: /data/import/myarchive.mbox
Size: 245.32 MB
Target Account ID: 1
Target Folder: INBOX

[Importing... progress logged to container logs]

=== Import Results ===
Status: Completed
Total Emails: 15234
Imported Successfully: 15120
Failed: 42
Skipped (malformed): 28
Skipped (duplicates): 44
Duration: 00:12:45
```

## 🔍 Monitoring {#monitoring}

Tijdens de import wordt gedetailleerde voortgang geschreven naar de containerlogs:

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ Configuration Reference {#configuration-reference}

| Omgevingsvariabele | Standaard | Beschrijving |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | Eerste toegestane pad voor lokale imports |
| `LocalImport__AllowedPaths__1` | (leeg) | Tweede toegestane pad (optioneel) |

Voeg meer paden toe door de index te verhogen (`__2`, `__3`, etc.).

## 🚫 Foutafhandeling {#error-handling}

| Fout | Oorzaak | Oplossing |
|-------|-------|----------|
| `File not found` | Het `--file` pad bestaat niet in de container | Controleer het bestandspad en de volume mount |
| `File path is not in an allowed import directory` | Het bestand bevindt zich buiten de whitelisted paden | Verplaats het bestand naar een toegestaan pad of voeg de directory toe aan `LocalImport:AllowedPaths` |
| `Mail account with ID X not found` | Verkeerd account ID | Controleer de account ID via de Web UI of een database query |
| `Invalid account-id` | Niet-numerieke waarde voor `--account-id` | Gebruik een numerieke database ID |

## 🔒 Best practices voor beveiliging {#security-best-practices}

1. **Gebruik read-only mounts** waar mogelijk:
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **Beperk AllowedPaths** tot alleen de mappen die u daadwerkelijk gebruikt.

3. **Verwijder volume mounts** nadat de import is voltooid om een schoon beveiligingsoppervlak te behouden.

4. **Exponeer nooit** de importmap via de webserver – er is geen web-endpoint voor lokale imports.

## 📦 Importformaten in één oogopslag {#import-formats-at-a-glance}

| CLI Flag | Inputformaat | Typische bestandsextensie | Enkele e-mail | Bulk |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | MBox-bestand | `.mbox`, `.mbx` | ❌ (altijd bulk) | ✅ |
| `--import-eml` | ZIP Archief van EML's | `.zip` | ❌ (ZIP vereist) | ✅ |
| `--migrate-raw-storage` | Opgeslagen originele berichten | — | — | ✅ |

*Enkele `.eml`-bestanden worden niet direct ondersteund. Gebruik `zip` om ze te bundelen, of importeer via de [web upload](Import-EML-MBox.md).*

## 🔧 Onderhoudscommando's {#maintenance-commands}

### Het verplaatsen van opgeslagen originele berichten {#moving-stored-original-messages}

Wanneer `RawMessages:Enabled` aan staat, wordt het originele bericht van elke e-mail bewaard in de database of in bestanden, afhankelijk van `RawMessages:Storage`. Dit commando verplaatst de reeds opgeslagen originelen van de ene backend naar de andere; zie [Original Message Storage](RawMessages.md#storage-backend) voor de configuratie.

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

| Argument | Standaard | Beschrijving |
|----------|---------|-------------|
| `--from` | geconfigureerde `RawMessages:Storage` | Backend waaruit de berichten worden verplaatst. |
| `--to` | geconfigureerde `RawMessages:Storage` | Backend waarnaar de berichten worden verplaatst. |
| `--batch` | `200` | Berichten per databasetransactie. |

In tegenstelling tot de importcommando's is dit commando niet beperkt door `LocalImport:AllowedPaths`: het leest alleen de berichtbestanden die het Archief zelf heeft geschreven onder `RawMessages:FileSystem:BasePath`.

**Wat het garandeert:**

- Elk bericht wordt gecontroleerd tegen de checksum die op het moment van archiveren is opgenomen voordat het wordt gekopieerd. Een bericht dat niet voldoet, blijft precies waar het is en wordt gerapporteerd — een corrupt origineel wordt nooit doorgegeven en de enige resterende kopie wordt nooit verwijderd.
- Een bestand wordt pas verwijderd nadat de databasetransactie die het heeft verplaatst is voltooid (committed), zodat een onderbroken uitvoering niet de enige kopie kan verliezen.
- De uitvoering is hervatbaar: het werk wordt geselecteerd op basis van de opslag die bij elk bericht is opgenomen, dus het opnieuw starten van het commando gaat verder met wat nog openstaat.
- Eén slecht bericht zorgt er niet voor dat de uitvoering wordt afgebroken. De exit code is `1` als een bericht is mislukt, `0` in andere gevallen.

**Voorbeeldoutput:**

```text
Batch: 200 moved, 0 skipped, 0 failed
Batch: 200 moved, 0 skipped, 0 failed
Batch: 42 moved, 0 skipped, 1 failed
Done: 442 moved, 1 failed.
  Email 12345: checksum mismatch (recorded 4f1c..., file has 9ab2...)
```

Lezingen volgen altijd de backend die bij elk bericht is opgenomen, niet de geconfigureerde instelling, zodat een download blijft werken tijdens en na een migratie. Het wijzigen van `RawMessages:Storage` heeft alleen invloed op nieuw gearchiveerde e-mail — voer dit commando uit om te verplaatsen wat al is opgeslagen.

> **Back-up:** bij de filesystem backend staan de berichtbestanden buiten de database. Maak een Back-up van `RawMessages:FileSystem:BasePath` samen met de databasedump; een Herstel zonder de bestanden laat originelen achter die niet kunnen worden geserveerd.

## ❓ FAQ {#faq}

**V: Waarom vereist `--import-eml` een ZIP-bestand?**
A: De onderliggende EML-importservice (`EmlImportService`) verwerkt ZIP-Archieven intern met behulp van de `ZipFile` API van .NET. Dit ontwerp ondersteunt bulk-imports efficiënt. Voor individuele `.eml`-bestanden moet u deze in een ZIP-Archief verpakken (bijv. `zip emails.zip *.eml`) of de [web upload](Import-EML-MBox.md) gebruiken.

**V: Waarom kan ik geen bestanden browsen vanuit de web UI?**
A: Dit is een bewust beveiligingsontwerp. Deze functie is opzettelijk via CLI geïmplementeerd voor interacties op systeemniveau, omdat normale gebruikers geen directe toegang hebben tot het OS-niveau; zij werken alleen met de Web UI.

Om beveiligingsrisico's zoals path traversal-aanvallen en ongeautoriseerde blootstelling van het bestandssysteem te voorkomen, zijn reguliere gebruikers beperkt tot het gebruik van de Web UI voor bestand-uploads en imports. Dit zorgt ervoor dat zij de beoogde interface niet kunnen omzeilen om toegang te krijgen tot het onderliggende container-bestandssysteem.
