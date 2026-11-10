# 📤 Date-Windowed Offload {#date-windowed-offload}

[← Terug naar de documentatie-index](index.md)

## 📋 Overzicht {#overview}

Een offload voegt gearchiveerde e-mails toe die nieuwer zijn dan een afkapdatum aan een andere Postbus, en laat alles wat ouder is in het Archief. Het is bedoeld voor migraties waarbij de nieuwe Postbus alleen recente e-mail moet bevatten, terwijl de volledige geschiedenis bereikbaar blijft via Mail Archiver.

Het verschilt op vijf manieren van [Copy All Emails to Another Mailbox](MailboxMigration.md):

| | Copy All Emails | Offload |
|---|---|---|
| Datumfilter | geen, alles of niets | afkapdatum, met een optionele bovengrens |
| Herhaalbaar | nee, voeg alles opnieuw toe | ja, sla over wat de doelmap al bevat |
| Map uitsluitingen | nee | ja, configureerbaar |
| Map hernoemen | nee | ja, configureerbaar |
| Preview | nee | dry run rapportage per map zonder te schrijven |

De offload voegt alleen maar toe aan het doel. Het verwijdert nooit iets uit het Archief en raakt de bron-Postbus nooit aan.

## 🔁 Een run herhalen is veilig {#repeating-a-run-is-safe}

Vóór de eerste toevoeging wordt de volledige doel-Postbus geïndexeerd en wordt elk bericht hieraan getoetst. Een herhaalde run voegt daarom niets toe dat al is gearriveerd, en rapporteert dit in plaats daarvan als reeds aanwezig. Dat heeft drie praktische gevolgen:

- Een onderbroken run kan worden hersteld door deze simpelweg opnieuw uit te voeren.
- Een voltooide run kan worden herhaald als een verificatiepass. Een correct resultaat is dat er niets is toegevoegd en alles al aanwezig is.
- Berichten die op het doel zijn verwijderd, komen bij de volgende run terug, omdat ze niet meer in de index staan.

Twee criteria bepalen of een bericht al aanwezig is, in deze volgorde:

1. **Message-ID**, genormaliseerd. Dit doet bijna al het werk.
2. **Een fingerprint** op basis van afzender, ontvangers, onderwerp en verzendtijd binnen twee seconden. Dit dekt de berichten waarvan de opgeslagen Message-ID ontbreekt of defect is. Dergelijke berichten kunnen helemaal niet worden hersteld met hun oorspronkelijke Message-ID, dus zonder dit tweede criterium zouden ze bij elke run duplicaten worden.

De taak rapporteert apart hoeveel overeenkomsten voortkomen uit de fingerprint, omdat dit het criterium is dat stilzwijgend kan falen.

## ⚙️ Configuratie {#configuration}

Een nieuwe `Offload` sectie, met standaardwaarden die het vorige gedrag reproduceren:

```json
"Offload": {
  "MaxConcurrentJobs": 1,
  "PrefetchMaxMessages": 500000,
  "ExcludedSourceFolders": [],
  "FolderRenameMap": {},
  "MarkAsSeen": true
}
```

| Key | Default | Doel |
|---|---|---|
| `MaxConcurrentJobs` | `1` | Hoeveel herstel- of offload-taken tegelijk mogen draaien. Eén houdt het strikt seriële gedrag aan dat de taakwachtrij altijd had. Er draait maximaal één taak per doel-Postbus, ongeacht deze waarde. |
| `PrefetchMaxMessages` | `500000` | Bovengrens voor het aantal berichten dat wordt geïndexeerd uit een doel-Postbus. Daarboven wordt de duplicatiecontrole beperkt tot een enkele map en wordt dit gelogd. |
| `ExcludedSourceFolders` | leeg | Bronmappen die nooit worden geoffload. Wordt gecontroleerd vóór het hernoemen en omvat submappen. |
| `FolderRenameMap` | leeg | Overschrijft de beginsegmenten van een pad naar een bronmap. |
| `MarkAsSeen` | `true` | Of toegevoegde e-mail wordt gemarkeerd als gelezen. |

Beide mapinstellingen zijn opzettelijk leeg. Het herschrijven of laten vallen van mappen zonder dat daarom is gevraagd, zou iedereen die al de herstelroute gebruikt verrassen; een run zonder configuratie migreert alles, inclusief spammappen, en maakt een tweede set speciale mappen aan naast die van het doel.

### Map uitsluitingen en hernoemen {#folder-exclusions-and-renaming}

Voor een migratie van Exchange naar Dovecot zien de twee instellingen er meestal zo uit:

```json
"ExcludedSourceFolders": ["Junk E-Mail", "Deleted Items"],
"FolderRenameMap": {
  "Sent Items": "Sent",
  "Deleted Items": "Trash",
  "Junk E-Mail": "Junk"
}
```

Zonder de rename map eindigt elke gemigreerde Postbus met zowel `Sent` als `Sent Items`, omdat Dovecot zijn eigen speciale mappen aanmaakt.

Vier zaken over de toepassing hiervan zijn het waard om te weten, omdat elk ervan een manier is om verrast te worden:

1. **Uitsluitingen worden eerst gecontroleerd**, tegen de bronnaam. Als hernoemen eerst zou gebeuren, zou een uitsluiting op `Deleted Items` nooit worden geactiveerd zodra deze al `Trash` is geworden.
2. **Hernoemen herschrijft het langste overeenkomstige padprefix**, op grenzen van segmenten. `Sent Items` dat `Sent` wordt, maakt ook `Sent Items/2019` tot `Sent/2019`, en laat `Sent Items Archive` ongemoeid.
3. **Matching is niet hoofdlettergevoelig**, en zowel `/` als `\` tellen als scheidingstekens.
4. **Twee bronmappen kunnen samenvallen in één doelmap.** `Sent Items` en `Sent` die beide `Sent` worden is prima en opent de doelmap niet dubbel.

Het wijzigen van de rename map tussen twee runs dupliceert niets, omdat de duplicatiecontrole de volledige doel-Postbus beslaat in plaats van één opgeloste map.

## 🖥️ Uitvoeren via de interface {#running-it-from-the-interface}

Op de detailpagina van een Account opent **Offload to Another Mailbox** een formulier met de doel-Postbus, de doel-hoofdmap, of de mappenstructuur behouden moet blijven, het datumbereik, een dry run-schakelaar, en of toegevoegde e-mail als gelezen moet worden gemarkeerd. De geconfigureerde uitsluitingen en de rename map worden alleen-lezen getoond, zodat een run nooit een verrassing is.

De taak wordt in de wachtrij geplaatst en de voortgang en het rapport per map verschijnen op de statuspagina van de taak en onder **Jobs**. Het rapport per map noemt de bronmappen, dus het wordt alleen getoond aan de gebruiker die de taak heeft gestart en aan beheerders.

De doel-Postbus kan een uitgeschakeld IMAP-Account zijn, bijvoorbeeld om een migratiedoel te voorzien voordat het live gaat; de run faalt met een duidelijke foutmelding bij de verbinding als het doel onbereikbaar is. Microsoft 365-Accounts kunnen geen offload-doelen zijn; de Graph herstelroute blijft ongewijzigd.

### Wie mag het uitvoeren {#who-may-run-it}

Beheerders kunnen offload uitvoeren tussen willekeurige twee Accounts. Een zelfbeheerder kan **alleen offload uitvoeren tussen de Accounts die aan hem zijn toegewezen**, aan beide kanten: de bron, omdat de Accountpagina al op die manier is ingeperkt, en het doel, omdat de doelmaplijst is beperkt tot dezelfde set en het verzoek opnieuw wordt gecontroleerd wanneer de taak wordt gestart. Een zelfbeheerder met een enkel toegewezen Account heeft daarom geen geldig doel en krijgt dat te horen.

De beperking komt voort uit `IAccountAccessResolver`, dezelfde resolver die de REST API en de MCP-server gebruiken, dus er is één definitie van wie welke Postbus mag gebruiken. De beslissing zelf bevindt zich in `OffloadTargetEligibility`, die zowel de doelmaplijst als de controle van het verzoek aanroept, zodat wat het formulier aanbiedt en wat de server accepteert, niet uit elkaar kan lopen.

## ⌨️ Uitvoeren via de opdrachtregel {#running-it-from-the-command-line}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll \
  --offload --source-account-id 3 --target-account-id 7 \
  --since 2025-08-01 \
  [--until 2026-08-01] [--target-folder INBOX] \
  [--preserve-folders] [--dry-run] [--no-mark-seen]
```

| Argument | Betekenis |
|---|---|
| `--source-account-id` | Account van waaruit gearchiveerde e-mail wordt gelezen. Verplicht. |
| `--target-account-id` | IMAP-Account waaraan wordt toegevoegd. Verplicht, en moet verschillen van de bron. |
| `--since` | Inclusieve ondergrens van de verzenddatum, `YYYY-MM-DD`. Verplicht. |
| `--until` | Optionele inclusieve bovengrens. |
| `--target-folder` | Hoofdmap in de doel-Postbus. Standaard `INBOX`. |
| `--preserve-folders` | Maak de bronmapstructuur onder de hoofdmap opnieuw aan. |
| `--dry-run` | Rapporteer wat er zou gebeuren, voeg niets toe. |
| `--no-mark-seen` | Toevoegen zonder de Seen-vlag. |

Exitcodes:

| Code | Betekenis |
|---|---|
| `0` | Alles is toegevoegd of al aanwezig. |
| `1` | Ten minste één bericht is mislukt, of de run zelf is mislukt. |
| `2` | De aanroep was onjuist: foutieve of ontbrekende argumenten, onbekend Account, doel is geen IMAP, bron is gelijk aan doel. |

Eén aanroep verwerkt één Postbus, wat een vloot aan Postbussen scriptbaar maakt vanaf een CSV met bron, doel en afkapdatum.

> **Meerdere tegelijk uitvoeren.** De regel van één taak per doel bestaat alleen binnen een enkel proces, dus twee gelijktijdige aanroepen via de opdrachtregel tegen *dezelfde* doel-Postbus zouden elk hun duplicatie-index kunnen pakken voordat de ander begint met schrijven, en zouden dan allebei dezelfde e-mail kunnen toevoegen. Verdeel het werk zodat elke gelijktijdige aanroep een ander doel heeft, wat de normale situatie is wanneer elke rij in een CSV een andere Postbus is.

## 🗓️ Hoe de afkapdatum wordt geïnterpreteerd {#how-the-cutoff-is-interpreted}

- De filter is gebaseerd op **de verzenddatum**, niet op wanneer een bericht is gearchiveerd. De tijdstempel van het archiveren is het tijdstip van de archiveringsrun en heeft geen betekenis voor een afkapdatum.
- Een relatief venster zoals "de laatste twaalf maanden" wordt **omgezet naar een vaste datum wanneer de taak wordt aangemaakt**. Een taak kan lang draaien en daarna worden herhaald, en moet elke keer dezelfde e-mail selecteren; een opgeslagen relatief venster zou verschuiven.
- Afkapdata worden geïnterpreteerd in de **geconfigureerde weergave-tijdzone**, dezelfde als die in het zoekscherm wordt getoond, en de ondergrens is inclusief vanaf 00:00 van die dag. Een bovengrens omvat de gehele dag. Relatieve vensters ("de laatste N maanden") worden eveneens omgezet in die weergave-tijdzone, overeenkomstig hoe `SentDate` wordt opgeslagen.

### Bekende beperking {#known-limitation}

- Wanneer de duplicatie-index `Offload:PrefetchMaxMessages` bereikt, wordt de scope verkleind en gelogd in plaats van opnieuw te proberen tegen een enkele map. De `restrictToFolder` fallback die de indexbouwer biedt, wordt momenteel niet gebruikt door de offload-route; het verhogen van de limiet is de ondersteunde oplossing.

## 📋 Een migratie, van begin tot eind {#a-migration-end-to-end}

1. Maak de doel-Postbussen aan. Zie voor veel tegelijk de CSV bulkimport in [Account Import](Account-Import.md).
2. Configureer `Offload:ExcludedSourceFolders` en `Offload:FolderRenameMap`. Beide zijn standaard leeg, dus het overslaan hiervan migreert spammappen en maakt dubbele speciale mappen aan.
3. Bevestig dat elke bron-Postbus volledig is gearchiveerd voordat deze wordt geoffload.
4. **Voer een dry run uit voor alles.** Controleer de aantallen en de opgeloste doelmapnamen voordat er iets wordt geschreven.
5. Echte run. Begin met `MaxConcurrentJobs` op 2 en verhoog dit pas nadat je hebt gezien hoe de doelserver ermee omgaat; Dovecot beperkt het aantal gelijktijdige verbindingen per gebruiker en per IP.
6. Herhaal de run om te verifiëren. Verwacht dat er niets is toegevoegd en dat alles al aanwezig is.
7. Schakel de bron-Accounts uit.

De doorvoersnelheid wordt beperkt door `BatchOperation:PauseBetweenEmailsMs`, wat standaard 50 ms is en dus maximaal twintig toevoegingen per seconde toestaat voordat de serverlatentie wordt meegeteld.

## 🔍 Wat de tellers betekenen {#what-the-counters-mean}

| Teller | Betekenis |
|---|---|
| Appended | Geschreven naar het doel. Bij een dry run: wat er geschreven zou zijn. |
| Already present | Het doel bevatte het al, op basis van een van de twee criteria. |
| of those by fingerprint | Hoeveel werden herkend door het tweede criterium in plaats van door Message-ID. |
| Excluded folder | Overgeslagen omdat de bronmap is uitgesloten. |
| Failed | Kon niet worden toegevoegd. |

Er wordt een waarschuwing gerapporteerd als de duplicatie-index niet de volledige doel-Postbus besloeg, wat gebeurt boven `PrefetchMaxMessages` of wanneer een map niet kon worden gelezen. In die staat kan een herhaalde run e-mail toevoegen die al aanwezig is in de delen die niet zijn geïndexeerd.

## ⚠️ Notities en limieten {#notes-and-limits}

- Gearchiveerde e-mail bevat nu de **oorspronkelijke leveringstijd** als de IMAP interne datum, overgenomen uit de `Received` keten van het bericht en met de verzenddatum als fallback. Voorheen bevatte het de tijd van de archiveringsrun, wat sorteren aan de serverzijde op aankomst en elke op leeftijd gebaseerde regel op het doel verstoorde.
- Omdat een herhaalde run overslaat wat al aanwezig is, kan een foutieve interne datum niet worden gecorrigeerd door opnieuw uit te voeren: de berichten worden herkend en overgeslagen. Zorg dat dit correct is voor een productie-run, of verwijder de betreffende berichten op het doel en voer de run opnieuw uit.
- Microsoft 365 wordt niet ondersteund als offload-doel.
- Duplicatiedetectie kan alleen herkennen wat het kan zien in de doel-Postbus. E-mail die het doel via een andere route heeft bereikt, wordt alleen gevangen voor zover de twee criteria toevallig overeenkomen.
