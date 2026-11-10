# 📧 Original Message Storage {#original-message-storage}

[← Terug naar de Documentatie-index](index.md)

## 📋 Overzicht {#overview}

Standaard bewaart het Archief de **geanalyseerde onderdelen** van een e-mail: headers, tekst- en HTML-body's, Bijlagen. Een geëxporteerde `.eml` wordt opgebouwd uit deze velden.

Dat is voldoende om een e-mail te lezen, maar het is niet het bericht **zoals ontvangen**. Transfer-encodings, MIME-grenzen en header folding verschillen van het origineel, waardoor een opnieuw opgebouwd bericht de DKIM-handtekening niet meer verifieert en spamscanners het anders beoordelen.

Wanneer de opslag van het originele bericht is ingeschakeld, bewaart het Archief het volledige bericht zoals het is aangekomen, naast de geanalyseerde velden. Het opgeslagen origineel is wat het Archief kan bewijzen dat is afgeleverd.

| | Alleen geanalyseerde velden | Met opgeslagen origineel |
| --- | --- | --- |
| Headers, body, Bijlagen | ✅ | ✅ |
| Byte-exact bericht | ❌ | ✅ |
| DKIM-handtekening verifieert | ❌ | ✅ |
| Forensisch gebruik (rechtbank, audit) | beperkt | ✅ |

## 🎯 Waar het origineel wordt geserveerd {#where-the-original-is-served}

Zodra het is opgeslagen, wordt het origineel geretourneerd waar een volledig bericht wordt verwacht:

| Plaats | Wat er gebeurt |
| --- | --- |
| **Export EML** knop op de e-mail detailpagina | Serveert het opgeslagen origineel wanneer dit bestaat, valt terug op de reconstructie voor e-mails die zonder origineel zijn gearchiveerd. |
| **Restore to a mailbox** | Voegt het opgeslagen origineel toe wanneer dit bestaat, valt terug op de reconstructie voor e-mails die zonder origineel zijn gearchiveerd. |
| `GET /api/v1/emails/{id}/raw` | REST API download, zie de [REST API guide](API.md#download-original-message). |
| `get_email_raw` | MCP tool, zie de [MCP guide](MCP.md#get_email_raw). |

De respons vertelt je welke vorm je hebt ontvangen, zodat een aanroeper een echt origineel kan onderscheiden van een reconstructie zonder de bytes te inspecteren:

| Signaal | Betekenis |
| --- | --- |
| `X-Raw-Message-Source: imap` | Exacte kopie van de IMAP server (`BODY[]` zoals geleverd). |
| `X-Raw-Message-Source: graph` | Exacte kopie, de MIME content die Exchange retourneerde. |
| `X-Raw-Message-Source: eml-file` | Exacte kopie, de inhoud van het geïmporteerde `.eml` bestand. |
| `X-Raw-Message-Source: serialized` | Geparseerd bericht dat opnieuw is weggeschreven. Identiek behalve dat de regeleinden CRLF zijn (MBox import, IMAP recovery fetch). |
| `X-Raw-Message-Sha256` | SHA-256 van het opgeslagen origineel. |
| `X-Raw-Message-Reconstructed: true` | Geen origineel opgeslagen; het bericht is opnieuw opgebouwd. Geen checksum header. |
| `X-Raw-Message-Storage` | Backend waaruit het origineel is gelezen (`postgres` of `filesystem`). |

De bestandsnaam zegt het ook: `email-{id}-original-...eml` voor een opgeslagen origineel,
`email-{id}-...eml` voor een reconstructie.

Wanneer een opgeslagen origineel **faalt bij de checksum**, worden de export en de restore geweigerd
met een foutmelding. Het retourneren van de reconstructie in plaats daarvan zou verbergen dat de gearchiveerde
kopie is gewijzigd, wat precies is wat een Archief nooit mag doen.

Een restore leest het opgeslagen origineel en voegt het toe via de client van de provider, zodat de
Postbus de originele headers en MIME-structuur ontvangt. De exacte byte-stroom is niet
gegarandeerd over de lijn omdat de client het bericht opnieuw serialiseert; de gearchiveerde
kopie zelf blijft ongewijzigd.

## ⚙️ Instellingen {#settings}

Alle instellingen bevinden zich in de `RawMessages`-sectie, of als omgevingsvariabelen met het `RawMessages__`-prefix.

| Instelling | Standaard | Beschrijving |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Sla het originele bericht op van elke nieuw gearchiveerde e-mail. E-mails die zijn gearchiveerd voordat deze functie is ingeschakeld, hebben geen origineel. |
| `RawMessages:MaxMessageSizeMb` | `50` | Berichten groter dan deze waarde worden gearchiveerd zonder hun origineel. `0` = geen limiet. |
| `RawMessages:IncludeGraph` | `true` | Sla ook het origineel op voor Microsoft 365 (Graph) accounts. Dit kost één extra Graph-verzoek per bericht, wat meetelt voor het throttling-budget. |
| `RawMessages:Storage` | `postgres` | Waar de berichtbytes worden bewaard: `postgres` (in de database) of `filesystem` (gzip-bestand naast het Archief). Zie [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Directory waarnaar de berichtbestanden worden geschreven, relatief aan de content root van de applicatie. Wijs dit naar een gemounte volume in Docker. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Aantal directory-niveaus van twee tekens waarover de e-mail-id wordt verspreid. `2` geeft 10.000 directory's, `0` is plat. Houdt het aantal bestanden per directory laag bij grote Archieven. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Bereken bij elke download de SHA-256 van het bestand opnieuw en vergelijk deze met de checksum die tijdens het archiveren is vastgelegd. Bij een mismatch mislukt de download met HTTP 500 in plaats van gewijzigde bytes te serveren. |

### Dimensionering {#sizing}

Opslag groeit met ongeveer de grootte van elke e-mail inclusief de Bijlagen, gzip-gecomprimeerd. Bijlagen worden **niet** onderworpen aan Deduplicatie binnen het opgeslagen origineel, dus een e-mail met een grote Bijlage kost die Bijlage dubbel: één keer in de gededupliceerde Bijlage-opslag en één keer in het origineel. Plan voor ongeveer **twee keer** de databasegrootte van een Archief zonder originelen.

Het inschakelen van de instelling vult bestaande e-mail niet bij. Alleen nieuw gearchiveerde e-mails krijgen een origineel.

## 💾 Storage backend {#storage-backend}

De `Storage`-instelling bepaalt waar de bytes worden opgeslagen. De **metadata-rij blijft altijd in
de database**, ongeacht welke backend is geconfigureerd, en bevat de SHA-256, grootte, de bron van de
vastlegging en (voor bestanden) het relatieve pad. Die rij is wat het Archief bewijst wat een
opgeslagen bericht moet bevatten.

### postgres (default) {#postgres-default}

Het gecomprimeerde bericht staat in de `Content`-kolom van `mail_archiver."RawMessages"`.
Dit is het gedrag dat het Archief altijd heeft gehad en heeft geen extra volume nodig.

Kies dit wanneer de database al je Back-up unit is en je één ding wilt
back-uppen en herstellen.

### filesystem {#filesystem}

Het bericht wordt geschreven naar `{BasePath}/{xx}/{yy}/{id}.eml.gz`, waarbij `xx`/`yy` worden afgeleid
van de e-mail id. De database-rij houdt alleen de metadata bij.

Kies dit wanneer het Archief groot genoeg is dat originelen de databasegrootte zouden domineren,
of wanneer je de berichtbestanden op goedkopere of aparte opslag wilt plaatsen.

!!! danger "Mount een volume in Docker"
    De standaard `BasePath` (`RawMessages`) wordt opgelost naar `/app/RawMessages` **binnen
    de container**. Zonder een bind mount behoort die directory tot de beschrijfbare laag van de container,
    dus elke opnieuw aangemaakte container - inclusief `docker compose down && docker compose
    up -d`, een image-update, of een host-reboot met een efemere laag - neemt elk opgeslagen
    origineel mee, terwijl de database-rijen blijven bestaan.

    Mount de directory altijd naar de host wanneer je de filesystem backend gebruikt:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Het host-pad (`./raw-messages` hier) is wat je back-upt. Het container-pad moet
    overeenkomen met `RawMessages:FileSystem:BasePath`, of stel `RawMessages:FileSystem:BasePath` in op
    het mountpunt dat je hebt gekozen. Als je de mount verplaatst, zorg dan dat `BasePath` ernaar blijft wijzen: de
    opgeslagen `StoragePath`-waarden zijn relatief, dus ze blijven correct worden opgezocht.

### Switching backends {#switching-backends}

Lezingen volgen de opslag die op elke rij is vastgelegd, niet de geconfigureerde instelling. Nadat je
`Storage` hebt gewijzigd, blijven eerder gearchiveerde berichten downloadbaar vanaf de plek waar ze staan.
De instelling heeft alleen invloed op **nieuw** gearchiveerde e-mail.

Om te verplaatsen wat al is opgeslagen, voer je de storage migratie uit. Zie de
[CLI commands](CLI.md#moving-stored-original-messages) voor de volledige referentie:

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Wat de migratie garandeert:

- Elk bericht wordt geverifieerd tegen de opgenomen checksum voordat het wordt gekopieerd. Een bericht
  dat faalt, blijft staan waar het is en wordt gerapporteerd: een corrupt origineel wordt nooit doorgegeven en
  de enige resterende kopie wordt nooit verwijderd.
- Een bestand wordt pas verwijderd nadat de database-transactie die het heeft verplaatst is bevestigd (committed), zodat een
  onderbroken run de enige kopie niet kan verliezen.
- De run is hervatbaar. Werk wordt geselecteerd door de `StorageType`-kolom, dus het opnieuw starten van de
  opdracht gaat verder met wat nog openstaat.
- De exit code is `1` als een bericht is mislukt, `0` anders.

Een migratie is niet vereist om het Archief te laten werken. Het is alleen nodig om bestaande
bytes naar de andere backend te verplaatsen.

## 🛟 Back-up en herstel {#backup-and-restore}

=== "postgres backend"
    Niets extra's. De originelen maken deel uit van de database en worden gedekt door de normale
    dump, zie [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Maak een Back-up van de `BasePath` directory **samen met** de database dump.

    Een database herstel zonder de bestanden laat het Archief achter met rijen waarvan de inhoud
    niet kan worden geleverd. De download geeft dan een HTTP 404 in plaats van iets
    foutiefs terug, en de e-mails zelf blijven leesbaar via hun geparseerde velden.

## 🗑️ Een e-mail verwijderen {#deleting-an-email}

Het verwijderen van een e-mail verwijdert ook het origineel: de Bewaring-regels, de handmatige individuele en bulkverwijdering, en het verwijderen van een heel mail Account verwijderen het bestand als onderdeel van dezelfde operatie. Met de `postgres` backend is er niets extra's te doen, omdat de bytes meereizen binnen de rij die de cascade al verwijdert.

Het bestand wordt verwijderd **nadat** de e-mailrij is gecommit, nooit daarvoor. De twee stappen kunnen niet één transactie zijn, omdat een bestand geen deel uitmaakt van de database-transactie, dus moet een van de twee als tweede komen. Deze volgorde is de veilige: een verwijdering die mislukt of wordt teruggedraaid, bijvoorbeeld bij een vergrendeld record, laat het origineel ongemoeid, en een crash tussen de twee stappen laat een bestand zonder rij achter, wat de sweep hieronder terugwint. De andere volgorde zou het origineel van een e-mail vernietigen die nog wel bestaat.

**Verweesde bestanden worden verzameld.** Alles wat erdoorheen is geglipt, inclusief bestanden die zijn achtergelaten door verwijderingen van voordat deze opschoning bestond, wordt verwijderd door een periodieke sweep van `BasePath` die elk bestand vergelijkt met de rijen en de bestanden verwijdert waar geen enkele e-mail naar verwijst:

| Instelling | Standaard | Betekenis |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Zet de sweep aan of uit |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Hoe vaak deze wordt uitgevoerd |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Bestanden jonger dan dit worden nooit aangeraakt |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Vertraging voor de eerste uitvoering, zodat een lopende opslagmigratie eerst wordt afgerond |

De coulanceperiode is wat de sweep veilig maakt: een import schrijft een bestand voordat de rij die ernaar verwijst wordt gecommit, en een bestand dat enkele momenten geleden is geschreven, mag niet worden aangezien voor een weeskind. Half geschreven `.tmp` bestanden van een onderbroken schrijfactie worden onder dezelfde regel verzameld. Met de `postgres` backend doet de sweep niets en doorloopt zelfs niet de directory.

## 🔍 Het verifiëren van een Archief {#verifying-an-archive}

De checksum die op het moment van archiveren is vastgelegd, maakt verificatie mogelijk.

- **Bij download** (`VerifyChecksumOnRead=true`, de standaardinstelling) wordt het bestand gehasht en
  vergeleken. Bij een mismatch wordt de download geweigerd met HTTP 500.
- **Tijdens de opslagmigratie** wordt elk bericht geverifieerd voordat het wordt gekopieerd, in beide
  richtingen.

Omdat de metadataregel in de database staat en de bytes op de schijf kunnen staan, is een checksum-
vergelijking de enige manier om te merken dat een bestand buiten de applicatie om is gewijzigd.

## ⚠️ Het is belangrijk om te weten {#things-to-know}

- **Inschakelen zorgt niet voor een backfill.** Alleen e-mails die na de overschakeling worden gearchiveerd, krijgen een origineel.
- **De reconstructie is niet het bericht zelf.** Voor e-mails zonder opgeslagen origineel geeft elke
  downloadpath een opgebouwd bericht terug en geeft dit aan via
  `X-Raw-Message-Reconstructed: true`.
- **Filesystem betekent twee back-up eenheden.** Database en message directory moeten samen worden hersteld,
  anders blijven de rijen bestaan zonder hun inhoud.
- **`VerifyChecksumOnRead` kost CPU.** Elke download van een groot bericht wordt gedecodeerd
  en gehasht. Laat het aan staan, tenzij u een probleem heeft vastgesteld; het is de enige bescherming
  tegen stille wijzigingen op de schijf.
- **Het opgeslagen origineel wordt niet onderworpen aan deduplicatie.** Een bijlage die in veel e-mails voorkomt,
  wordt één keer opgeslagen in de attachment store, maar één keer per e-mail binnen het origineel.
- **Verwijderen verwijdert ook het origineel.** Een e-mail die wordt verwijderd door bewaring of handmatig, neemt
  het opgeslagen origineel mee, zowel het bestand als de rij. Niets wordt bewaard dat het Archief niet
  meer vermeldt.

## 🔗 Zie ook {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - de volledige opslagmigratie
- [Setup and Parameters](Setup.md) - alle configuratieparameters
- [REST API](API.md#download-original-message) - de `/raw` endpoint
- [MCP Server](MCP.md#get_email_raw) - de `get_email_raw` tool
- [Backup and Restore](BackupRestore.md) - wat u moet back-uppen
- [Attachment Deduplication](AttachmentDeduplication.md) - hoe bijlagen worden opgeslagen
