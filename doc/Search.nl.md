# 🔍 Mail Search {#mail-search}

[← Terug naar Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Zoeken is het hart van een e-mailarchief. Mail Archiver doorzoekt de **volledige tekst**
van uw gearchiveerde e-mail — onderwerp, tekst, afzender en ontvangers — over alle
accounts waar u toegang toe heeft, en geeft resultaten terug in milliseconden, zelfs bij
archieven met miljoenen berichten.

Deze gids behandelt de zoekinterface, de query-syntaxis, filters, sorteren,
paginering en wat u moet doen wanneer u actie moet ondernemen op een resultatenlijst.

> **ℹ️ Zoeken ziet alleen wat u mag zien.** Resultaten worden gefilterd op basis van uw
> accountmachtigingen. Een niet-beheerder vindt alleen e-mail uit de
> postbussen die aan hem zijn toegewezen — de beperking wordt toegepast in de query zelf,
> niet in de interface.

## 🚀 Toegang tot Zoeken {#accessing-search}

1. Log in en kies **Archive** in het hoofdmenu
2. De zoekbalk bevindt zich boven de resultatenlijst

Het openen van het Archief zonder zoekterm toont uw meest recente e-mails, nieuwste
eerst. U kunt van daaruit bladeren of de resultaten verfijnen met de filters.

## 🎯 De Zoekinterface {#the-search-interface}

| Veld | Wat het doet |
|---|---|
| **Search Term** | Full-text query — zie [Query Syntax](#-query-syntax) hieronder |
| **From** | Alleen e-mails verzonden op of na deze datum |
| **To** | Alleen e-mails verzonden op of voor deze datum |
| **Direction** | `All`, `Incoming` of `Outgoing` |
| **Account** | Beperken tot één mailaccount |
| **Folders** | Beperken tot één map via de mappenboom |
| **Search** | Voer de query uit |
| **Reset** | Wis alle filters |

De zoekterm komt overeen met het **onderwerp, de tekst, de afzender en de ontvangers**
(`From`, `To`, `Cc`, `Bcc`).

### De mappenboom {#the-folder-tree}

Mappen worden aan de linkerkant weergegeven als een **hiërarchische boom**, die de mappenstructuur
van de bronpostbus weerspiegelt — inclusief geneste mappen. Klik op een map om de
zoekopdracht tot die map te beperken, en gebruik de in-/uitklapfunctie om de boom
in te klappen wanneer u de schermruimte nodig heeft.

Het selecteren van een map verandert uw zoekterm niet; de twee worden gecombineerd.

## 🧩 Query Syntax {#query-syntax}

De syntaxis is bewust beperkt. Alles hieronder is wat de applicatie
daadwerkelijk begrijpt.

### Gewone woorden — en waarom prefixes werken {#plain-words-and-why-prefixes-work}

Typ één of meer woorden; ze moeten allemaal aanwezig zijn (een **AND**-zoekopdracht):

```
invoice january
```

Vindt e-mails die zowel `invoice` als `january` bevatten in de gezochte velden.

> **💡 Gedeeltelijke woorden worden automatisch gematcht.** Elk woord wordt behandeld als een
> **prefix**, dus een zoekopdracht naar `isenb` vindt ook `isenboeck` en `isenböck`, en
> `rech` vindt zowel `Rechnung` als `Rechnungen`. U heeft geen wildcard-
> teken nodig — er is er geen om te typen. Dit betekent ook dat een zeer korte term
> veel kan opleveren; zie [Performance](#-performance-and-large-archives).

De zoekopdracht is **niet hoofdlettergevoelig**. `Invoice`, `invoice` en `INVOICE` zijn
dezelfde query.

### Exacte zinnen — aanhalingstekens {#exact-phrases-quotes}

Zet tekst tussen dubbele aanhalingstekens om de woorden **naast elkaar** te vereisen in die
volgorde:

```
"important meeting"
```

Zonder aanhalingstekens zou `important meeting` ook overeenkomen met een e-mail waarin de twee
woorden in niet-gerelateerde zinnen voorkomen. Met aanhalingstekens komt alleen de aangrenzende frase
overeen.

### Veldspecifiek zoeken {#field-specific-search}

Zet een prefix voor een term met een veldnaam en een dubbele punt om **alleen** in dat veld te zoeken:

| Syntaxis | Zoekt in |
|---|---|
| `subject:invoice` | Alleen onderwerp |
| `from:john` | Alleen afzender |
| `to:example.com` | Alleen ontvangers |
| `body:important` | Alleen tekst |

Veldaamnamen zijn niet hoofdlettergevoelig (`Subject:` werkt ook), en elk kan worden
gecombineerd met aanhalingstekens voor een frase:

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ Vier velden worden ondersteund:** `subject`, `body`, `from`, `to`. Het schrijven van
> `cc:someone` of `bcc:someone` is **geen** veldzoekopdracht — de term wordt behandeld
> als gewone tekst en door alle velden gezocht. Om een ontvanger te vinden die
> alleen in Cc stond, zoekt u naar het adres of de naam zonder prefix.

### Alles combineren {#combining-everything}

Filters en termen tellen bij elkaar op. Deze query vindt e-mails van elke verzekeringsafzender,
met "car insurance" als exacte frase in de tekst, en "invoice" in het
onderwerp:

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

Praktische combinaties:

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### Karakters die worden genegeerd {#characters-that-are-ignored}

De query-parser verwijdert de karakters `& | ! ( ) : *` van individuele termen,
omdat ze betekenis hebben in de onderliggende full-text engine. Als uw zoekopdracht
niets onverwachts oplevert, controleer dan of een speciaal karakter de reden is.

## 📊 Sorteren en Paginering {#sorting-and-paging}

### Sorteren {#sorting}

Klik op een kolomkop om te sorteren. Sorteerbare kolommen:

| Kolom | Sorteert op |
|---|---|
| **Subject** | Onderwerpregel, alfabetisch |
| **From** | Afzender |
| **To** | Ontvangers |
| **Date** | Verzenddatum |

Op dezelfde koptekst klikken keert de richting om. De standaard is **nieuwste
eerst**.

### Paginaformaat {#page-size}

Kies hoeveel resultaten er per pagina worden weergegeven:

**20** (standaard) · **50** · **75** · **100** · **150**

Een grotere pagina toont meer context maar duurt langer om te laden. Gebruik de
**Previous** / **Next** knoppen om door de resultatenpagina's te navigeren; het
totaal aantal resultaten vertelt u hoeveel e-mails er in totaal zijn gevonden.

> **ℹ️ Uw tijdzone is van toepassing op de getoonde datums.** Weergegeven tijden worden
> omgezet van UTC naar uw geconfigureerde tijdzone, zodat de datum die u ziet overeenkomt
> met uw lokale kalender — handig wanneer u rond middernacht zoekt.

## 👁️ Werken met Resultaten {#working-with-results}

Elke rij toont het account, de afzender, het onderwerp, de datum, en een **📎 paperclip-icoon**
wanneer de e-mail bijlagen heeft. De paperclip is slechts een indicator — het is geen
filter.

Klik op **Details** om een e-mail te openen:

- Volledige tekst (HTML of platte tekst)
- **Bijlagen**, opgelijst met de optie om elke bijlage te downloaden
- **Download as EML** — het volledige bericht, voor gebruik elders
- **Herstel** — kopieer de e-mail terug naar een actieve postbus

Zie [Mailbox Migration](MailboxMigration.md) voor het herstellen van e-mails naar een postbus.

## ✅ Actie ondernemen op veel resultaten tegelijk {#acting-on-many-results-at-once}

Resultaten kunnen in bulk worden geselecteerd en verwerkt. Schakel **Selection Mode** in om
selectievakjes te krijgen, en doe dan het volgende:

- **Select All** / **Deselect All** voor de huidige pagina
- **Select all {n} matching** — elk resultaat van de huidige zoekopdracht over alle
  pagina's (wordt getoond wanneer de resultaten meer dan één pagina beslaan)
- **Copy Selected** — herstellen naar een postbus
- **Export Selected** — als MBOX of EML
- **Delete Selected** — uit het Archief

De teller toont *"{selected} van {total} geselecteerd"* terwijl u werkt.

### De selectielimiet {#the-selection-limit}

> **⚠️ Er kunnen maximaal 250 e-mails tegelijk worden geselecteerd** (configureerbaar via
> `Selection:MaxSelectableEmails`). Daarboven geeft de interface aan: *"Maximum
> selections reached (250)"* en **Too Many — Split Required**.

Die limiet bestaat omdat elke operatie de betreffende berichten in het geheugen moet houden. De
melding *"Split larger operations into multiple batches"* is de beoogde oplossing: werk
de resultaten af in groepen van 250 of minder.

**Select all {n} matching** houdt rekening met dezelfde limiet. Wanneer de zoekopdracht meer
e-mails vindt dan de limiet toestaat, worden de eerste e-mails in de huidige sorteervolgorde
geselecteerd en wordt de melding *"Maximum selections reached"* getoond. Verfijn de zoekopdracht (datumreeks, map, account) om de rest te vinden.

Een poging om de limiet te overschrijden geeft aan: *"Too many emails selected. Maximum allowed is
250 emails. Please reduce your selection or split into multiple operations."*

### Elke match van een filter verwijderen {#deleting-every-match-of-a-filter}

De knop **Delete all matches** naast *Reset* verwijdert elke e-mail die
overeenkomt met de huidige zoekopdracht, niet alleen de huidige pagina of selectie. Deze is beschikbaar voor beheerders en zelfbeheerders en draait als een achtergrondtaak, dus
de selectielimiet van 250 e-mails is hier niet van toepassing.

Het bevestigingsvenster laat zien hoeveel e-mails verwijderbaar zijn en hoeveel er zijn vergrendeld door de bewaring. U moet dat aantal typen om de
bevestiging mogelijk te maken. De server telt opnieuw en breekt af als de resultatenlijst in de tussentijd is gewijzigd, bijvoorbeeld omdat een synchronisatie mail heeft toegevoegd.

Vergrendelde e-mails worden nooit verwijderd door deze actie; ze worden gemeld als overgeslagen in
de status van de taak. `DeletionPolicy:MaxBulkDeleteEmails` (standaard 100.000) beperkt één
taak. Een filter dat meer e-mails vindt, wordt geweigerd met een verzoek om de zoekopdracht te verfijnen.

De taak verschijnt op de [Background Jobs](BackgroundJobs.md) pagina en kan daar
worden geannuleerd.

### Directe verwerking vs. achtergrondtaak {#direct-processing-vs-background-job}

Voor **Copy** en **Export** kiest u hoe de taak wordt uitgevoerd:

| Optie | Gedrag |
|---|---|
| **Process on this page (wait for completion)** | De browser wacht; goed voor een handvol e-mails |
| **Process in background (monitor separately)** | Draait als een taak; u kunt de browser sluiten |

Exports draaien altijd op de achtergrond:

> *"De export wordt op de achtergrond uitgevoerd en u kunt het resultaat downloaden
> vanaf de Jobs-pagina zodra deze is voltooid."*

Vind lopende en voltooide taken op de [Background Jobs](BackgroundJobs.md) pagina.

> **⚠️ Verwijderen is definitief.** De bevestiging is expliciet: *"Weet u zeker dat u
> {n} geselecteerde e-mail(s) permanent wilt verwijderen? Deze actie kan niet worden ongedaan gemaakt
> en elke verwijdering wordt gelogd voor auditdoeleinden."* Verwijderingen worden vastgelegd
> in het toegangsprotocol.

## ⚡ Performance en grote Archieven {#performance-and-large-archives}

Zoeken wordt ondersteund door een PostgreSQL full-text index, dus het verschil tussen een
snelle en een langzame zoekopdracht is meestal hoe **selectief** de query is.

**Wel doen:**

- Voeg een **datumreeks** toe wanneer u ongeveer weet wanneer de e-mail is aangekomen — dit is
  de meest effectieve manier van verfijnen
- Beperk tot een **account** of **map** wanneer u weet waar de mail staat
- Gebruik **meerdere woorden** in plaats van één zeer kort woord
- Gebruik veldzoekopdrachten (`subject:`, `from:`) om niet-gerelateerde resultaten uit te sluiten

**Niet doen:**

- Een enkel kort prefix zoals `re` — als prefix-zoekopdracht komt dit overeen met een enorm
  deel van uw archief
- Zeer brede datumreeksen gecombineerd met een veelvoorkomend woord
- Door veel pagina's bladeren terwijl u de zoekopdracht ook had kunnen verfijnen

> **💡 Als een zoekopdracht traag aanvoelt, is de datumreeks meestal de oplossing.** Het beperken
> tot een maand verandert een zoekopdracht van meerdere seconden vaak in een directe actie, omdat de
> index het grootste deel van het archief kan overslaan.

## ❓ Probleemoplossing {#troubleshooting}

| Symptoom | Oorzaak | Wat te doen |
|---|---|---|
| Geen resultaten, maar u verwacht er wel | Zoekterm te specifiek, of een typefout | Probeer minder woorden; controleer spelling |
| `cc:name` vindt niets nuttigs | `cc` is geen ondersteund veld | Zoek de naam zonder prefix |
| Een speciaal karakter breekt de query | `& \| ! ( ) : *` worden verwijderd | Verwijder of vervang het karakter |
| Te veel resultaten | Veelvoorkomend woord of kort prefix | Voeg een datumreeks of veldzoekopdracht toe |
| *"Too Many — Split Required"* | Meer dan 250 e-mails geselecteerd | Verminder de selectie of splits de operatie |
| Een export ontbreekt | Het draait als een achtergrondtaak | Controleer [Background Jobs](BackgroundJobs.md) |
| Resultaten lijken te ontbreken | Accountmachtigingen | Controleer of de postbus aan uw gebruiker is toegewezen |
| Datums lijken verschoven | Tijdzoneconversie | Controleer uw tijdzone-instelling |

## 📚 Gerelateerde Documentatie {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — resultaten herstellen naar een postbus
- [Background Jobs](BackgroundJobs.md) — het monitoren van exports en herstelacties
- [Access Logging](Logs.md) — het auditlogboek van zoekopdrachten en verwijderingen
- [REST API](API.md) — hetzelfde archief via de API
- [MCP Server](MCP.md) — zoeken via een AI-agent
- [Retention Policies](RetentionPolicies.md) — wanneer e-mails het archief verlaten
