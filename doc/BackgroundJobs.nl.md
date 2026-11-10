# ⚙️ Background Jobs {#background-jobs}

[← Terug naar de Documentatie Index](index.md)

## 📋 Overzicht {#overview}

De meeste werkzaamheden in Mail Archiver vinden plaats terwijl u niet wacht. Het synchroniseren van een
Postbus, het importeren van een MBOX-bestand, het herstellen van duizenden e-mails of het exporteren van een
Archief kan minuten tot uren duren. Deze taken draaien als **background jobs** — u
start ze, en ze blijven draaien, zelfs als u de browser sluit.

De **Background Jobs** pagina laat zien wat er momenteel draait, wat is voltooid en
wat er mis is gegaan. Dit is de plek om te kijken wanneer iets langer duurt dan
verwacht of wanneer een synchronisatie eindigt met fouten.

> **ℹ️ Administrator pagina.** Background Jobs vereist administratorrechten en
> is bereikbaar vanuit het hoofdmenu. Reguliere gebruikers zien dit niet — zij kunnen nog steeds
> jobs starten (bijvoorbeeld een batch herstel), maar de monitoring vindt hier plaats.

## 🗂️ Job Types {#job-types}

De pagina groepeert jobs per type. Elke sectie toont maximaal de **20 meest recente**
jobs, waarbij lopende en in de wachtrij staande jobs bovenaan staan — zodat een oude job niet
uit het zicht wordt gedrukt door nieuwere jobs.

| Sectie | Wat het dekt | Gestart vanuit |
|---|---|---|
| **Synchronization Jobs** | Nieuwe mail ophalen van de mailserver | Automatisch schema, of *Sync now* op een Account |
| **MBox Import Jobs** | Importeren van `.mbox` bestanden | Mail Accounts → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **EML Import Jobs** | Importeren van `.eml` bestanden / ZIP-archieven | Mail Accounts → [Import EML](Import-EML-MBox.md#-import-eml) |
| **Batch Restore Jobs** | Geselecteerde e-mails terug kopiëren naar een Postbus | Archive → select emails → Batch Restore |
| **Account Export Jobs** | Exporteren van een volledige Account (mbox / EML) | Mail Accounts → [Export](Export-Account.md) |
| **Selected Emails Export Jobs** | Exporteren van een handgekozen set e-mails | Archive → select emails → Export |
| **Email Deletion Jobs** | E-mails verwijderen van server of Archief, inclusief bulkverwijdering op basis van filters | Bewaring-beleid, handmatige verwijdering, of Archive → *Delete all matches* |
| **Account Deletion Jobs** | Verwijderen van een volledige mail-Account | Mail Accounts → Delete |
| **Offload** | Mail met een specifieke datumperiode verplaatsen naar een andere Account | Mail Accounts → Offload |

## 📊 Een Job-rij lezen {#reading-a-job-row}

Elke rij bevat dezelfde set kolommen:

| Kolom | Betekenis |
|---|---|
| **Job ID** | Korte identificatie — vermeld deze bij het rapporteren van een probleem |
| **Account** | De Postbus waar de job op werkt |
| **Status** | Huidige staat, zie de onderstaande tabel |
| **Progress** | Hoe ver de job is gekomen, met verwerkt/totaal indien bekend |
| **Created** | Wanneer de job in de wachtrij is geplaatst |
| **Started** | Wanneer deze daadwerkelijk begon te draaien |
| **Completed** | Wanneer deze eindigde (leeg terwijl deze nog draait) |
| **Duration** | Verstreken tijd |

Sommige job types voegen tellers toe aan de progress-kolom:

- **Success** / **Failed** — e-mails die succesvol of niet succesvol zijn verwerkt
- **Skipped (duplicates)** — e-mails die al aanwezig zijn in het Archief, niet dubbel geïmporteerd
- **Recovered** — berichten die opnieuw zijn opgehaald nadat de server aangaf dat ze ontbraken
- **Malformed (skipped)** — berichten die niet geparsed konden worden en zijn overgeslagen

### Statuswaarden {#status-values}

| Status | Betekenis |
|---|---|
| **Queued** | Wacht op start — een andere job draait, of de worker is bezig |
| **Running** | Momenteel aan het uitvoeren |
| **Completed** | Succesvol voltooid |
| **CompletedWithErrors** | Voltooid, maar sommige items zijn mislukt (MBox imports) |
| **Failed** | Eindigde met een fout |
| **Cancelled** | Gestopt door een gebruiker |
| **Rate Limited** | *Sync gepauzeerd vanwege bandbreedte limiet. Wordt automatisch hervat.* |
| **Timed Out** | De synchronisatie overschreed de geconfigureerde tijdslimiet en werd gestopt |

> **ℹ️ "Rate Limited" en "Timed Out" zijn alleen voor synchronisatie-staten.** Ze lijken op
> fouten, maar dat zijn ze niet: een rate-limited synchronisatie hervat zichzelf zodra de bandbreedte
> quota het toelaat, en een timed-out synchronisatie gaat verder vanaf het checkpoint bij de volgende
> run. Zie [Rate Limit Handling](RateLimitHandling.md) en
> [Mail Synchronization](Synchronization.md).

### Waarom de voortgang op 0% kan blijven staan {#why-progress-can-sit-at-0}

Voortgang wordt gemeten in **voltooide mappen**, niet per individuele e-mail:

> *Voortgang wordt gemeten aan de hand van voltooide mappen. Een grote map (bijv. INBOX) houdt
> de balk op 0% totdat deze klaar is.*

Op een Postbus waar de INBOX de meeste mail bevat, kan de balk lange tijd op 0% blijven en dan verspringen. Dat is verwacht — de job werkt, hij heeft alleen zijn eerste grote map nog niet afgerond.

## 🔄 De pagina ververst zichzelf {#the-page-refreshes-itself}

Terwijl de pagina open is, wordt deze elke **30 seconden** opnieuw geladen, zodat u deze in een
tabblad kunt laten staan en de voortgang kunt volgen zonder ergens op te klikken. Het label laat zien wanneer de gegevens
voor het laatst zijn bijgewerkt.

Het herladen wordt geannuleerd op het moment dat u op een link of knop klikt — anders zou uw
klik terechtkomen op een pagina die onder uw klik wordt vervangen.

**Configureerbaar:** `Jobs:RefreshSeconds` in `appsettings.json` (standaard `30`).
Stel het in op `0` om automatisch herladen uit te schakelen. Elke herlaadbeurt bouwt de pagina
op uit alle job bronnen, dus een zeer kort interval op een installatie met veel
Accounts is een echte belasting — en die belasting vermenigvuldigt zich met elk open tabblad.

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 Een Job annuleren {#cancelling-a-job}

Jobs die annulering ondersteunen, tonen een **Cancel Job** knop, die vraagt:

> *Are you sure you want to cancel this job?*

Import jobs gebruiken de bewoording **Cancel Import** / *Are you sure you want to cancel
this import job?*

Annuleren is beschikbaar voor **synchronization, batch restore, selected-emails
export en email deletion** jobs. De job stopt zodra het huidige item is
afgerond; de status wordt **Cancelled**.

> **⚠️ Annuleren is geen rollback.** E-mails die al zijn geïmporteerd, hersteld,
> geëxporteerd of verwijderd, blijven in die staat. Annuleren zorgt ervoor dat de job geen *verdere* acties uitvoert —
> het ongedaan maken van wat er al is gedaan, gebeurt niet. Voor een verwijderingsjob betekent dit dat de e-mails
> die al zijn verwijderd, weg zijn.

Account en MBox import jobs kunnen niet vanaf deze pagina worden geannuleerd; stop ze in plaats daarvan
vanaf de eigen importpagina van de Account.

## ⚠️ Sync-fouten en "Acknowledge Failures" {#sync-failures-and-acknowledge-failures}

Wanneer een synchronisatie eindigt met de status **Failed**, biedt de job-rij de optie **Acknowledge
Failures**. Dit is de enige actie op de pagina die toekomstig gedrag verandert, dus
het is belangrijk om dit te begrijpen.

Elke synchronisatie-run start waar de vorige stopte, gebruikmakend van een **checkpoint**. Als een
synchronisatie faalt voor sommige e-mails, blijft het checkpoint achter die e-mails, zodat de volgende
run ze opnieuw probeert. Dat is de veilige standaard — er wordt niets stilletjes overgeslagen.

Maar als de fouten permanent zijn (een corrupt bericht, een map die de server
weigert te leveren), zal elke volgende synchronisatie ze opnieuw proberen en blijft de synchronisatie
op dezelfde plek falen.

**Acknowledge Failures** lost deze impasse op door het checkpoint voorbij
de gefaalde e-mails te verplaatsen. De bevestiging is expliciet over de gevolgen:

> *This will advance the sync timestamp to the completion time of this job, so
> the next sync will be a normal incremental sync. The failed emails will not be
> retried. Continue?*

Bij succes ziet u: *"Sync timestamp has been advanced. The next sync will be
incremental."*

> **⚠️ Bevestig pas nadat u naar de fouten heeft gekeken.** Na
> bevestiging worden die e-mails **niet** opnieuw geprobeerd — ze zullen ontbreken uit het
> Archief, tenzij u ze op een andere manier ophaalt. Controleer eerst de foutdetails, en
> als de oorzaak een misconfiguratie is, los dit dan op voordat u bevestigt. Zie
> [Mail Synchronization](Synchronization.md) voor het gedrag van het checkpoint in
> detail.

Het bevestigen wordt geregistreerd in het access log als *SyncAcknowledgeFailures*, met
het aantal gefaalde e-mails en de Account, zodat het later traceerbaar is.

## 👁️ Wat u hier niet kunt doen {#what-you-cannot-do-here}

- **Geen retry knop.** Een gefaalde job wordt niet herstart vanaf deze pagina — start de
  operatie opnieuw vanaf het punt waar u ermee begon (sync, import, export).
- **Geen bewerkingen.** Job parameters staan vast bij creatie.
- **Alleen de laatste 20 per type.** Oudere jobs worden niet vermeld. Voor langetermijn-
  gegevens zijn het access log en het application log de bronnen — zie
  [Access Logging](Logs.md) en [Docker Compose Logs](DockerComposeLogs.md).

## ❓ Probleemoplossing {#troubleshooting}

| Symptoom | Waarschijnlijke oorzaak | Wat te doen |
|---|---|---|
| Job blijft **Queued** | Een andere job van dezelfde worker draait | Wacht; controleer andere secties op een langlopende job |
| Voortgang blijft op 0% steken | Eén grote map wordt nog verwerkt | Normaal — zie "Waarom de voortgang op 0% kan blijven staan" |
| Sync herhaaldelijk **Failed** op dezelfde Account | Permanente fouten in individuele e-mails | Inspecteer de fouten, los de oorzaak op, en bevestig dan |
| Sync toont **Rate Limited** | Bandbreedte quota bereikt | Niets — het hervat automatisch |
| Sync toont **Timed Out** | Postbus groter dan de tijdslimiet | Het gaat verder vanaf het checkpoint bij de volgende run; zie [Synchronization](Synchronization.md) |
| **CompletedWithErrors** bij een MBox import | Sommige berichten zijn malformed | De tellers laten zien hoeveel; de rest is geïmporteerd |
| Job verdwijnt na herladen | Het is buiten het "laatste 20" venster gevallen | Controleer het application log voor de definitieve status |
| Pagina herlaadt terwijl u werkt | Automatisch verversen elke 30 s | Stel `Jobs:RefreshSeconds` in op `0` |

## 📚 Gerelateerde Documentatie {#related-documentation}

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — checkpoints, timed-out synchronisaties
- [Rate Limit Handling](RateLimitHandling.md) — bandbreedte quota's en automatische hervatting
- [Access Logging](Logs.md) — wie wat deed, inclusief job acties
- [Batch Restore](MailboxMigration.md) — e-mails verplaatsen tussen Postbussen
- [Date-Windowed Offload](Offload.md) — offload jobs
- [Docker Compose Logs](DockerComposeLogs.md) — het lezen van het application log
