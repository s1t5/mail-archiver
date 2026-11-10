# 🧪 Lokale Testomgeving {#local-test-environment}

[← Terug naar Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Deze gids beschrijft hoe u de testsuite, de applicatie en een IMAP-doel op een ontwikkelmachine kunt uitvoeren. Het is gericht op bijdragers: de stack die wordt opgestart is opzettelijk onveilig en is geen handleiding voor implementatie. Raadpleeg voor implementatie de
[Setup Guide](Setup.md).

Drie onderdelen zijn betrokken:

| Onderdeel | Geleverd door | Doel |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | Twee databases, één voor `dotnet test` en één voor de applicatie |
| Dovecot | `docker-compose.test.yml` | Een IMAP-doel dat dient als vervanging voor een mailcow Postbus |
| De applicatie | `dotnet run` op de host | Sneller itereren dan het opnieuw opbouwen van de container image |

## 🚀 De stack starten {#starting-the-stack}

```bash
docker compose -f docker-compose.test.yml up -d
```

Dit publiceert alles alleen op loopback:

| Service | Hostadres | Opmerkingen |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5433, niet 5432, zodat het niet botst met een lokale server |
| Dovecot, plain | `127.0.0.1:1143` | Plaintext-authenticatie is ingeschakeld |
| Dovecot, TLS | `127.0.0.1:1993` | Het zelfondertekende certificaat van de image |

Om het te stoppen en alle databases en opgeslagen e-mail te verwijderen:

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ De twee databases {#the-two-databases}

De testsuite laat fixture-rijen achter: na een volledige run bevat de testdatabase enkele tientallen mail accounts en gebruikers. Als de applicatie die database zou delen, zou de achtergrond synchronisatie deze fixtures oppikken en herhaaldelijk proberen verbinding te maken met servers die niet bestaan. Daarom maakt de postgres container twee databases aan bij de eerste initialisatie
(`tests/postgres/10-create-dev-db.sql`):

| Database | Gebruikt door | Geconfigureerd in |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

Beide configuratiebestanden zijn gitignored en moeten lokaal worden aangemaakt.

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

`appsettings.Development.json` in de repository root:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` is van belang buiten de container: het staat standaard op het absolute pad
`/app/DataProtection-Keys`, dat niet bestaat op een development machine, en zonder dit bestand mislukt elke
aanvraag bij het lezen van de key ring. `IgnoreSelfSignedCert` is wat de applicatie in staat stelt
het Dovecot-certificaat op poort 1993 te accepteren.

## ✅ De tests uitvoeren {#running-the-tests}

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

Migraties worden automatisch toegepast door `TestDbFixture` bij de eerste uitvoering. De meeste tests hebben helemaal geen database nodig; de rest gebruikt PostgreSQL, dus ze falen met
`Name or service not known` als de stack niet draait.

Een connection string kan ook worden meegegeven via de omgeving, wat elke
configuratiebestand overschrijft:

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ Het uitvoeren van de applicatie {#running-the-application}

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` stelt al `ASPNETCORE_ENVIRONMENT=Development` in voor
`dotnet run`, dus een gewone `dotnet run --project MailArchiver.csproj` werkt ook en bedient
http://localhost:5179 (het `http` profiel). `ASPNETCORE_URLS` hierboven kiest in plaats daarvan alleen de container-
poort 5000.

Openstaande migraties worden bij het opstarten toegepast op `MailArchiverDev`. Meld u aan met de inloggegevens uit `appsettings.json`
(`Authentication:Username` en `Authentication:Password`).

## 📬 Gebruik van de Dovecot target {#using-the-dovecot-target}

Dovecot accepteert **elke gebruikersnaam** met het wachtwoord `pass`, dus een nieuwe target Postbus heeft geen provisioning nodig: kies een naam en log in. Voeg deze toe onder Mail Accounts met:

| Field | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | anything, e.g. `offload-target@example.com` | same |
| Password | `pass` | same |

De configuratie in `tests/dovecot/dovecot.conf` is gebaseerd op de standaardinstellingen van de image, met
drie bewuste verschillen die ervoor zorgen dat deze zich gedraagt als een mailcow target:

- **Maildir storage** in plaats van de sdbox van de image, wat is wat mailcow gebruikt.
- **`separator = /`**, overeenkomend met mailcow. Map paden worden gesplitst op `/` en `\` wanneer een herstel
  een map hiërarchie opnieuw aanmaakt, dus een server die `.` gebruikt zou anders reageren.
- **De special-use mappen `Drafts`, `Junk`, `Sent` en `Trash` zijn vooraf aangemaakt**, zoals mailcow
  ze vooraf aanmaakt. Dit is van belang bij het testen van een migratie vanuit Exchange: een bronmap genaamd
  `Sent Items` komt naast de bestaande `Sent` te staan in plaats van ermee samen te voegen, wat de
  situatie is waar map hernoeming mee te maken krijgt.

Plaintext authenticatie is ingeschakeld op poort 1143 omdat de applicatie zonder enige
transportbeveiliging verbinding maakt wanneer een Account SSL uitgeschakeld heeft.

## 🌱 Het vullen van een Archief voor tests {#seeding-an-archive-to-test-against}

Een leeg Archief is niet erg nuttig voor het testen van een Herstel of een migratie, dus
`tests/seed/seed.sh` bouwt er een:

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```

Het maakt twee Accounts aan, importeert de fixtures en print wat er is geland. Beide Accounts worden
aangemaakt met een synchronisatie-interval per Account van een jaar, en de bron is aanvullend uitgeschakeld,
zodat de achtergrond Synchronisatie-service deze negeert: de server bestaat niet, en het Dovecot-doel
hoeft alleen maar aangevuld te worden. Eén kanttekening: de planningsstatus leeft in het geheugen, dus bij elke
herstart van de applicatie wordt het doel één keer direct ingepland. Het doel sluit daarom ook de Dovecot special-use mappen (`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`) uit, zodat
één synchronisatie bij herstart niets heeft om terug te archiveren. Het script twee keer draaien is onschadelijk;
de tweede run rapporteert elke melding als reeds aanwezig.

De fixtures zijn ingecheckt onder `tests/seed/fixtures`, één mbox per bronmap, met
`manifest.tsv` die elk bestand koppelt aan de doelmap. Ze worden geïmporteerd via de
eigen `--import-mbox` CLI van de applicatie, en dat detail is bewust: de CLI routeert via
`MailImporter.ImportEmailToDatabase`, dezelfde importer die de IMAP-synchronisatie gebruikt, zodat de rijen landen in
precies het formaat dat een echt Archief bevat -- adressen samengevoegd met `", "`, onderwerpen doorgegeven via
`CleanText`, `SentDate` omgezet naar de weergave-tijdzone, `RawHeaders` geëxtraheerd en afgekort.
Het direct invoegen van rijen in de database zou dit allemaal overslaan en fixtures produceren die
stilletjes afwijken van productiedata.

Elke fixture-melding bevat een `X-Fixture-Purpose` header die aangeeft waarom deze bestaat, zodat de bestanden
zichzelf documenteren:

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```

Datums zijn vast in plaats van relatief aan de huidige dag. Een migratie-cutoff wordt omgezet naar een
absolute datum voordat een taak wordt uitgevoerd, zodat een vaste fixture-set en een vaste cutoff hetzelfde testen op een
voorspelbaardere manier dan een verschuivende set. De meldingen bestrijken de periode van 2012 tot 2026.

De mapstructuur weerspiegelt een Exchange Postbus, en elke map bestaat om iets te testen:

| Bronmap | Waarom deze aanwezig is |
|---|---|
| `INBOX` | datums verspreid over vijftien jaar, plus de edge-case meldingen hieronder |
| `Sent Items` | botst met de bestaande special-use `Sent` van het Dovecot-doel |
| `Sent Items/2019` | een hernoeming moet alleen het voorste padsegment herschrijven |
| `Sent Items Archive` | deelt een prefix als een substring maar niet op een segmentgrens, dus deze mag niet herschreven worden |
| `Deleted Items` | mapuitsluiting |
| `Deleted Items/Old` | een uitsluiting op een ouder moet ook de afstammelingen beslaan |
| `Junk E-Mail` | de naam van Exchange voor een map die Dovecot `Junk` noemt |
| `Archive/Projects/2020` | drie niveaus van nesting |

De individuele meldingen bevatten een melding zonder `Message-ID` header (die de deterministische
fallback krijgt), één waarvan de `Message-ID` geen `@` heeft (die een Herstel niet intact overleeft), een onderwerp
met een besturingskarakter, een melding met een `Received`-keten van drie stappen en één zonder,
meerdere met twee `To` ontvangers, en twee bijna identieke paren met één seconde verschil.

### Een opmerking over de exit code {#a-note-on-the-exit-code}

`tests/seed/seed.sh` controleert de aantallen mislukte en ongeldige meldingen uit de import-output in plaats van
de exit code van het proces. `MBoxImportService` rapporteert `CompletedWithErrors` wanneer duplicaten zijn overgeslagen, en de CLI koppelt elke status behalve `Completed` aan exit 1, dus het opnieuw draaien van een
import resulteert in een non-zero exit, zelfs als er niets mis is gegaan. Het opnieuw draaien van het seed-script is daarom veilig
en rapporteert elke melding als reeds aanwezig.

## ⚠️ Niet voor productie {#not-for-production}

- Elke gebruikersnaam authenticeert tegen Dovecot met één vast wachtwoord.
- Plaintext-authenticatie is ingeschakeld.
- Database-gegevens zijn de gedocumenteerde standaardwaarden.
- De status van de mail en de database staat in Docker-volumes die `down -v` verwijdert.

## 🔧 Probleemoplossing {#troubleshooting}

**Tests falen met `Name or service not known`** — de stack draait niet, of
`appsettings.Test.json` ontbreekt, waardoor de connection string terugvalt op de container-standaard
`Host=postgres`.

**`NETSDK1226` bij build** — onvolledige workload-gegevens in de lokale .NET SDK, niet gerelateerd aan deze
repository. Voer ofwel `dotnet workload repair` uit, of build met
`-p:AllowMissingPrunePackageData=true`.

**Dovecot start in een loop opnieuw op** — een configuratiefout. `docker compose -f
docker-compose.test.yml logs dovecot` rapporteert de foutieve regel. Let op dat Dovecot
meerdere instellingen op één regel binnen een blok niet accepteert.

**De applicatie geeft HTTP 500 op elke aanvraag terug** — meestal de data protection key ring.
Controleer of `DataProtection:KeyPath` naar een beschrijfbare directory wijst.
