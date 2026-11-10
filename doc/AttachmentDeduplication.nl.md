# 🧬 Bijlage Deduplicatie {#attachment-deduplication}

[← Terug naar Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Bijlagen van e-mails worden opgeslagen als binaire payloads in de database. Het **zelfde bestand**
wordt heel vaak bij veel e-mails bijgevoegd (bedrijfslogo's in handtekeningen, doorgestuurde
PDF's, nieuwsbrieven, contracten, ...). Zonder deduplicatie houdt elke bijlage
zijn eigen privékopie van die bytes, wat een grote hoeveelheid databasegeschiktheid verspilt.

**Bijlage deduplicatie** slaat elke *unieke* payload precies één keer op in een
content-addressed tabel (gekeyd door de SHA-256 hash van de bytes) en laat alle
identieke bijlagen naar die enkele gedeelde kopie verwijzen. Dit vermindert doorgaans de
opslagvoetafdruk van bijlage-rijke Archieven aanzienlijk.

## ⚙️ Hoe het werkt {#how-it-works}

### Nieuwe bijlagen {#new-attachments}
Wanneer een nieuwe e-mail wordt gearchiveerd (IMAP, Microsoft 365 / Graph, EML import, MBOX
import, ...) wordt de bijlage-payload gehasht en één keer opgeslagen. Als dezelfde payload
al bestaat, verwijst de nieuwe bijlage simpelweg naar de bestaande content-rij. Dit gebeurt
transparant op de datalaag, zodat elk archiveringspad automatisch wordt gedekt.

### Bestaande data (migratie) {#existing-data-migration}
Voor installaties die al bijlagen bevatten die zijn gearchiveerd **voordat** deze functie
werd geïntroduceerd, zet een **robuuste, hervatbare achtergrondmigratie** de bestaande
inline payloads om in gedeelde content-rijen:

- Het verwerkt bijlagen **batch voor batch**, waarbij elke batch in een eigen transactie wordt verwerkt.
- De voortgang wordt bijgehouden in een speciale state-tabel, zodat de migratie een
  **applicatieherstart overleeft** en simpelweg verdergaat waar het gebleven was.
- Het is **idempotent** – reeds gemigreerde bijlagen worden nooit meer aangeraakt, zelfs
  niet na een crash of herstart.
- Het draait op de achtergrond en blokkeert het normale gebruik van de applicatie niet.

Wanneer de migratie klaar is, wordt er een voltooiingsbericht gelogd, bijv.:

```
Attachment Deduplication migration COMPLETED. 36199 attachments processed in 1.6 minutes.
```

Bij de volgende start detecteert het systeem dat er niets meer te doen is en blijft het in ruststand. Mocht er ooit een niet-gedupliceerde bijlage overblijven (bijvoorbeeld na het herstellen van een oudere
back-up), dan wordt deze automatisch hervat en gemigreerd — het systeem convergeert altijd
naar een volledig gedupliceerde staat.

### Garbage collection (altijd aan) {#garbage-collection-always-on}
Wanneer e-mails (en dus bijlagen) worden verwijderd, kan een gedeelde payload niet meer
worden geraadpleegd. Een ingebouwde garbage collection verwijdert deze orphaned content-rijen.

> 🧹 **De orphan cleanup draait ALTTIJD**, onafhankelijk van de
> `DatabaseMaintenance` feature. Het draait één keer direct na de migratie en daarna
> periodiek (elke `OrphanCleanupIntervalHours`, standaard 12 u). Wanneer
> `DatabaseMaintenance__Enabled=true`, draait de cleanup ook voor de dagelijkse `VACUUM` zodat vrijgekomen ruimte fysiek wordt teruggevorderd.

### Diskgebruik na de migratie (belangrijk) {#disk-usage-after-the-migration-important}

> 📈 **De "Storage" waarde op het dashboard kan kortstondig OMHOOG gaan direct na de
> migratie** (bijv. 3.5 GB → 5.3 GB). Dit is verwacht en is geen bug.
>
> Het dashboard rapporteert de **fysieke** databasegrootte (`pg_database_size`). Wanneer de
> migratie een payload naar gedeelde opslag verplaatst, wordt de oude inline kopie op `NULL` gezet,
> maar PostgreSQL (MVCC) houdt die oude bytes vast als *dead tuples* totdat de tabelbestand
> opnieuw wordt geschreven — dus voor een moment bevat de database zowel de oude als de nieuwe kopie.
>
> Om dit automatisch op te lossen, voert de applicatie een **eenmalige**
> `VACUUM FULL` uit op de bijlagen-tabel **zodra de migratie voltooid is en terwijl er geen
> sync jobs draaien**. Dit schrijft de tabel opnieuw en geeft de vrijgekomen ruimte terug aan het
> besturingssysteem; daarna daalt de gerapporteerde grootte **onder** de oorspronkelijke waarde. Het draait
> precies één keer (bijgehouden via een `ReclaimedAt` marker), is restart-veilig en wordt uitgesteld
> zolang een synchronisatie in uitvoering is (het probeert het automatisch opnieuw zodra het systeem in rust is).

>
> ⚠️ De `VACUUM FULL` blokkeert de bijlagen-tabel voor de duur ervan (geen bijlage
> lees/schrijf acties ondertussen) en heeft wat vrije diskruimte nodig voor de rewrite. Als je dit
> handmatig wilt triggeren, voer dan uit:
> ```sql
> VACUUM (FULL, ANALYZE) mail_archiver."EmailAttachments";
> ```

## 🔧 Configuratie {#configuration}


Deze instellingen zijn optioneel; de standaardwaarden zijn passend voor de meeste installaties. Zie ook
de [Setup Guide](Setup.md#-attachment-deduplication-settings).

| Instelling | Standaard | Beschrijving |
|---------|---------|-------------|
| `AttachmentDeduplication__BatchSize` | `200` | Aantal bestaande bijlagen dat per transactie wordt gemigreerd. Grotere waarden migreren sneller maar verhogen de geheugen/DB belasting per batch. |
| `AttachmentDeduplication__DelayBetweenBatchesMs` | `0` | Optionele pauze (ms) tussen migratie batches om de DB belasting op drukke systemen te beperken. |
| `AttachmentDeduplication__StartupDelaySeconds` | `20` | Vertraging (s) na app start voordat de migratie begint (laat de schema migratie eerst afvullen). |
| `AttachmentDeduplication__OrphanCleanupIntervalHours` | `12` | Interval (u) van de altijd aanstaande orphan garbage collection. |
| `AttachmentDeduplication__CommandTimeoutSeconds` | `300` | Database command timeout (in seconden) voor de migratie batch operaties. Standaard is 5 minuten. Als een batch nog steeds een timeout geeft, probeert de service automatisch opnieuw met de helft van de batch grootte. |

Voorbeeld (`docker-compose.yml`):

```yaml
environment:
  - AttachmentDeduplication__BatchSize=200
  - AttachmentDeduplication__DelayBetweenBatchesMs=0
  - AttachmentDeduplication__StartupDelaySeconds=20
  - AttachmentDeduplication__OrphanCleanupIntervalHours=12
  - AttachmentDeduplication__CommandTimeoutSeconds=300
```

## ❓ FAQ {#faq}

**Kan ik deduplicatie uitschakelen?**
Nee. Het is een kernfunctie van de opslag en is altijd actief. Je kunt alleen de batch
grootte en de scheduling parameters hierboven aanpassen.

**Zijn mijn gegevens veilig tijdens de migratie?**
Ja. Elke batch is een enkele transactie en het werk is idempotent. Een crash of
herstart tijdens de migratie draait de onvoltooide batch terug en deze wordt opnieuw verwerkt bij de
volgende start; er gaat geen data verloren en er is geen sprake van duplicaten.

**Vertraagt het de archivering?**
De overhead van de per-bijlage hashing is verwaarloosbaar. De eenmalige migratie van
bestaande data draait op de achtergrond en kan worden beperkt met
`DelayBetweenBatchesMs` indien nodig.

**De migratie blijft steeds timen en herstarten bij dezelfde cursor. Wat kan ik doen?**
Dit kan gebeuren op zeer grote databases (honderdduizenden e-mails) waar een enkele batch van 200 bijlagen met SHA-256 hashing de standaard command timeout overschrijdt. De service probeert het nu automatisch opnieuw met de helft van de batch grootte wanneer een timeout optreedt. Als je nog steeds timeouts ziet, kun je handmatig de batch grootte verlagen en/of de command timeout verhogen:

```yaml
environment:
  - AttachmentDeduplication__BatchSize=10
  - AttachmentDeduplication__CommandTimeoutSeconds=600
```

**Hoeveel ruimte zal ik besparen?**
Dat hangt er volledig van af hoeveel dubbele bijlagen je archief bevat.
Archieven met veel herhaalde handtekeningen, logo's of doorgestuurde bestanden profiteren het meest.

---
