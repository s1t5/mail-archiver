# 📧 Original Message Storage {#original-message-storage}

[← Terug naar de Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Standaard bewaart het Archief de **geanalyseerde onderdelen** van een e-mail: headers, tekst- en HTML-body's, Bijlagen. Een geëxporteerde `.eml` wordt opgebouwd uit deze velden.

Dat is voldoende om een e-mail te lezen, maar het is niet het bericht **zoals ontvangen**. Transfer-encodings, MIME-grenzen en header folding verschillen van het origineel, waardoor een opnieuw opgebouwd bericht de DKIM-handtekening niet langer verifieert en spamscanners het anders beoordelen.

Wanneer de opslag van het originele bericht is ingeschakeld, bewaart het Archief het volledige bericht zoals het is aangekomen, naast de geanalyseerde velden. Het opgeslagen origineel is wat het Archief kan bewijzen dat is afgeleverd.

| | Alleen geanalyseerde velden | Met opgeslagen origineel |
| --- | --- | --- |
| Headers, body, Bijlagen | ✅ | ✅ |
| Byte-exact bericht | ❌ | ✅ |
| DKIM-handtekening verifieert | ❌ | ✅ |
| Forensisch gebruik (rechtbank, audit) | beperkt | ✅ |

## 🎯 Waar het origineel wordt geserveerd {#where-the-original-is-served}

Eenmaal opgeslagen, wordt het origineel geretourneerd overal waar een volledige berichten wordt verwacht:

| Plaats | Wat er gebeurt |
| --- | --- |
| **Export EML** knop op de e-mail detailpagina | Serveert het opgeslagen origineel wanneer een dergelijk origineel bestaat, valt terug op de reconstructie voor e-mails die zonder origineel zijn gearchiveerd. |
| **Herstel naar een Postbus** | Voegt het opgeslagen origineel toe wanneer een dergelijk origineel bestaat, valt terug op de reconstructie voor e-mails die zonder origineel zijn gearchiveerd. |
| `GET /api/v1/emails/{id}/raw` | REST API download, zie de [REST API guide](API.md#download-original-message). |
| `get_email_raw` | MCP tool, zie de [MCP guide](MCP.md#get_email_raw). |

De respons vertelt je welke vorm je hebt ontvangen, zodat een aanroeper een echt origineel kan onderscheiden van een reconstructie zonder de bytes te inspecteren:

| Signaal | Betekenis |
| --- | --- |
| `X-Raw-Message-Source: imap` | Exacte kopie van de IMAP server (`BODY[]` zoals geleverd). |
| `X-Raw-Message-Source: graph` | Exacte kopie, de MIME-inhoud die Exchange retourneerde. |
| `X-Raw-Message-Source: eml-file` | Exacte kopie, de inhoud van het geïmporteerde `.eml` bestand. |
| `X-Raw-Message-Source: serialized` | Geparseerd bericht dat opnieuw is weggeschreven. Identiek behalve dat de regeleinden CRLF zijn (MBox import, IMAP recovery fetch). |
| `X-Raw-Message-Sha256` | SHA-256 van het opgeslagen origineel. |
| `X-Raw-Message-Reconstructed: true` | Geen origineel opgeslagen; het bericht is opnieuw opgebouwd. Geen checksum header. |
| `X-Raw-Message-Storage` | Backend waaruit het origineel is gelezen (`postgres` of `filesystem`). |

De bestandsnaam zegt het ook: `email-{id}-original-...eml` voor een opgeslagen origineel,
`email-{id}-...eml` voor een reconstructie.

Wanneer een opgeslagen origineel **faalt op zijn checksum**, worden de export en het herstel geweigerd
met een foutmelding. Het teruggeven van de reconstructie in plaats daarvan zou verbergen dat de gearchiveerde
kopie is gewijzigd, wat precies is wat een Archief nooit mag doen.

Een herstel leest het opgeslagen origineel en voegt het toe via de client van de provider, zodat de
Postbus de originele headers en MIME-structuur ontvangt. Op IMAP serialiseert de client het
bericht opnieuw, waardoor de exacte byte stream op de lijn niet gegarandeerd is. Op Microsoft 365 worden de
gearchiveerde bytes ongewijzigd verzonden, omdat Graph het bericht in MIME-vorm accepteert. De
gearchiveerde kopie zelf wordt nooit gewijzigd.

## ⚙️ Instellingen {#settings}

Alle instellingen bevinden zich in de `RawMessages` sectie, of als omgevingsvariabelen met het `RawMessages__` prefix.

| Instelling | Standaard | Beschrijving |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Sla het originele bericht op van elke nieuw gearchiveerde e-mail. E-mails die zijn gearchiveerd voordat deze is ingeschakeld, hebben geen origineel. |
| `RawMessages:MaxMessageSizeMb` | `50` | Berichten groter dan dit worden gearchiveerd zonder hun origineel. `0` = geen limiet. |
| `RawMessages:IncludeGraph` | `true` | Sla ook het origineel op voor Microsoft 365 (Graph) accounts. Kost één extra Graph-verzoek per bericht, wat meetelt voor het throttling-budget. |
| `RawMessages:Storage` | `postgres` | Waar de berichtbytes worden bewaard: `postgres` (in de database) of `filesystem` (gzip-bestand naast het Archief). Zie [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Directory waarnaar de berichtbestanden worden geschreven, relatief aan de content root van de applicatie. Wijs dit naar een gemount volume in Docker. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Aantal directory-niveaus van twee tekens waarover de e-mail-id wordt verspreid. `2` levert 10.000 directory's op, `0` is plat. Houdt het aantal bestanden per directory laag bij grote Archiefs. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Bereken bij elke download opnieuw de SHA-256 van het bestand en vergelijk deze met de checksum die op het moment van Archiveren is opgenomen. Een mismatch laat de download mislukken met HTTP 500 in plaats van gewijzigde bytes te serveren. |

### Dimensionering {#sizing}

Opslag groeit met ongeveer de grootte van elke mail inclusief de Bijlagen, gzip-gecomprimeerd. Bijlagen worden **niet** gededuplicateerd binnen het opgeslagen origineel, dus een e-mail met een grote Bijlage kost die Bijlage twee keer: één keer in de gededupliceerde Bijlage-opslag en één keer in het origineel. Reken op ongeveer **twee keer** de databasegrootte van een Archief zonder originelen.

Het inschakelen van de instelling vult bestaande mail niet bij. Alleen nieuw gearchiveerde e-mails krijgen een origineel.

## 💾 Storage backend {#storage-backend}

De `Storage` instelling bepaalt waar de bytes worden opgeslagen. De **metadata-rij blijft altijd in
de database**, ongeacht welke backend is geconfigureerd, en bevat de SHA-256, grootte, capture
source en (voor bestanden) het relatieve pad. Die rij is wat het Archief bewijst wat een
opgeslagen bericht moet bevatten.

### postgres (default) {#postgres-default}

Het gecomprimeerde bericht staat in de `Content` kolom van `mail_archiver."RawMessages"`.
Dit is het gedrag dat het Archief altijd heeft gehad en geen extra volume vereist.

Kies dit wanneer de database al je Back-up eenheid is en je één ding wilt
backuppen en herstellen.

### filesystem {#filesystem}

Het bericht wordt geschreven naar `{BasePath}/{xx}/{yy}/{id}.eml.gz`, waarbij `xx`/`yy` worden afgeleid
van de email id. De database-rij bevat alleen de metadata.

Kies dit wanneer het Archief groot genoeg is dat originelen de databasegrootte zouden domineren,
of wanneer je de berichtbestanden op goedkopere of aparte opslag wilt plaatsen.

!!! danger "Mount een volume in Docker"
    De standaard `BasePath` (`RawMessages`) wordt opgelost naar `/app/RawMessages` **binnen
    de container**. Zonder een bind mount behoort die directory tot de writable
    layer van de container, dus elke opnieuw aangemaakte container - inclusief `docker compose down && docker compose
    up -d`, een image update, of een host reboot met een ephemeral layer - neemt elke opgeslagen
    origineel mee terwijl de database-rijen overleven.

    Mount de directory altijd naar de host wanneer je de filesystem backend gebruikt:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Het host pad (`./raw-messages` hier) is wat je backuppt. Het container pad moet
    overeenkomen met `RawMessages:FileSystem:BasePath`, of stel `RawMessages:FileSystem:BasePath` in op
    het mount punt dat je hebt gekozen. Als je de mount verplaatst, houd dan `BasePath` wijzend naar die locatie: de
    opgeslagen `StoragePath` waarden zijn relatief, dus ze blijven correct worden opgelost.

### Switching backends {#switching-backends}

Leesacties volgen de opslag die op elke rij is geregistreerd, niet de geconfigureerde instelling. Nadat je
`Storage` hebt gewijzigd, blijven eerder gearchiveerde berichten downloadbaar vanaf de plek waar ze staan.
De instelling heeft alleen invloed op **nieuw** gearchiveerde mail.

Om wat al is opgeslagen te verplaatsen, voer je de storage migratie uit. Raadpleeg de
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

- Elk bericht wordt geverifieerd tegen de geregistreerde checksum voordat het wordt gekopieerd. Een bericht
  dat faalt, blijft staan waar het is en wordt gerapporteerd: een corrupt origineel wordt nooit voortgeplant en
  de enige resterende kopie wordt nooit verwijderd.
- Een bestand wordt pas verwijderd nadat de database transactie die het verplaatst heeft is gecommit, zodat een
  onderbroken run niet de enige kopie kan verliezen.
- De run is hervatbaar. Werk wordt geselecteerd door de `StorageType` kolom, dus het opnieuw starten van de
  command gaat verder met wat nog openstaat.
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
    niet kan worden geserveerd. De download geeft dan HTTP 404 terug in plaats van iets
    onjuists te retourneren, en de e-mails zelf blijven leesbaar vanuit hun geparseerde velden.

## 🗑️ Een e-mail verwijderen {#deleting-an-email}

Het verwijderen van een e-mail verwijdert de originele met het mee: regels voor de Bewaring, de handmatige enkelvoudige en bulkverwijdering, en het verwijderen van een heel e-mail Account, verwijderen het bestand als onderdeel van dezelfde operatie. Met de `postgres` backend is er niets extra's te doen, omdat de bytes binnen de rij reizen die de cascade al verwijdert.

Het bestand wordt verwijderd **nadat** de e-mailrij is gecommit, nooit eerder. De twee stappen kunnen niet één transactie zijn, omdat een bestand geen deel uitmaakt van de database-transactie, dus moet een van beide als tweede komen. Deze volgorde is de veilige: een verwijdering die mislukt of terugdraait, bijvoorbeeld op een vergrendeld record, laat het origineel onaangetast, en een crash tussen de twee stappen laat een bestand zonder rij achter, die de sweep hieronder opeist. De andere volgorde zou het origineel van een e-mail vernietigen die nog bestaat.

**Orphaned bestanden worden verzameld.** Alles wat erdoorheen is geglipt, inclusief bestanden die zijn achtergelaten door verwijderingen van voordat deze opschoning bestond, wordt verwijderd door een periodieke sweep van `BasePath` die elk bestand vergelijkt met de rijen en de bestanden verwijdert waar geen enkele e-mail naar verwijst:

| Instelling | Standaard | Betekenis |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Zet de sweep aan of uit |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Hoe vaak deze wordt uitgevoerd |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Bestanden jonger dan dit worden nooit aangeraakt |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Vertraging voor de eerste uitvoering, zodat een lopende opslagmigratie eerst wordt afgerond |

De gratieperiode is wat de sweep veilig maakt: een import schrijft een bestand voordat de rij die ernaar verwijst wordt gecommit, en een bestand dat enkele momenten geleden is geschreven, mag niet worden aangezien voor een orphan. Half geschreven `.tmp` bestanden van een onderbroken schrijfactie worden onder dezelfde regel verzameld. Met de `postgres` backend doet de sweep niets en doorloopt zelfs de directory niet.

## 🔍 Het verifiëren van een Archief {#verifying-an-archive}

De checksum die op het moment van Archief is vastgelegd, maakt verificatie mogelijk.

- **Bij download** (`VerifyChecksumOnRead=true`, de standaard) wordt het bestand gehasht en
  vergeleken. Bij een mismatch wordt de download geweigerd met HTTP 500.
- **Bij de opslagmigratie** wordt elk bericht geverifieerd voordat het wordt gekopieerd, in beide
  richtingen.

Omdat de metadataregel in de database staat en de bytes op de schijf kunnen staan, is een checksum-
vergelijking de enige manier om te merken dat een bestand buiten de applicatie is gewijzigd.

## ⚠️ Zaken om te weten {#things-to-know}

- **Inschakelen vult niet achteraf aan.** Alleen e-mail die na de overschakeling wordt gearchiveerd, krijgt een origineel.
- **Een ontbrekend origineel wordt als een fout gelogd.** Wanneer de capture wordt geweigerd (zie bericht boven `MaxMessageSizeMb`, geen bruikbare capture) of wanneer het schrijven naar de geconfigureerde backend mislukt, bevat het logboek een fout met de e-mail id en de reden. De e-mail blijft in het Archief zonder het origineel; er is geen automatische achterafvulling en geen herpoging.
- **De detailpagina markeert een ontbrekend origineel.** Met `RawMessages:Enabled` toont de detailpagina een melding boven de headers. Het vraagt de storage backend, dus een bestand in het bestandssysteem dat is verdwenen, wordt ook gemarkeerd. De e-maillijst heeft geen badge per rij; dit zou te veel ruimte innemen in de statuskolom.
- **De reconstructie is niet het bericht.** Voor e-mails zonder opgeslagen origineel geeft elk downloadpad een opgebouwd bericht terug en vermeldt dit via `X-Raw-Message-Reconstructed: true`.
- **Bestandssysteem betekent twee Back-up eenheden.** Database en berichtmap moeten samen worden hersteld, anders overleven de rijen zonder hun inhoud.
- **`VerifyChecksumOnRead` kost CPU.** Elke download van een groot bericht wordt gedecomprimeerd en gehasht. Laat het aan staan, tenzij u een probleem heeft gemeten; het is de enige bescherming tegen stille wijziging op de schijf.
- **Het opgeslagen origineel wordt niet onderworpen aan Deduplicatie.** Een Bijlage die in veel e-mails voorkomt, wordt één keer opgeslagen in de bijlageopslag, maar één keer per e-mail binnen het origineel.
- **Verwijderen verwijdert ook het origineel.** Een e-mail die door Bewaring of handmatig wordt verwijderd, neemt het opgeslagen origineel mee, zowel het bestand als de rij. Niets wordt bewaard dat het Archief niet langer vermeldt.

## 🔗 Zie ook {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - de volledige opslagmigratie
- [Setup and Parameters](Setup.md) - alle configuratieparameters
- [REST API](API.md#download-original-message) - de `/raw` endpoint
- [MCP Server](MCP.md#get_email_raw) - de `get_email_raw` tool
- [Backup and Restore](BackupRestore.md) - wat u moet back-uppen
- [Attachment Deduplication](AttachmentDeduplication.md) - hoe bijlagen worden opgeslagen
