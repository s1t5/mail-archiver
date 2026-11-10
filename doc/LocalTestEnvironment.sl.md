# 🧪 Lokalno testno okolje {#local-test-environment}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodič opisuje, kako zagnati testni niz, aplikacijo in IMAP cilj na razvojnem stroju. Namenjen je prispevniki: tehnološki sklad (stack), ki ga vzpostavi, je namerno nevaren in ne služi kot priročnik za implementacijo. Za implementacijo poglejte
[Setup Guide](Setup.md).

Vključena sta trije elementi:

| Element | Zagotavlja | Namen |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | Dve bazi, ena za `dotnet test` in druga za aplikacijo |
| Dovecot | `docker-compose.test.yml` | IMAP cilj, ki nadomešča mailcow poštni predal |
| Aplikacija | `dotnet run` na gostitelju | Hitrejše iteriranje kot pri ponovni gradnji slike kontejnerja |

## 🚀 Začetek strosa {#starting-the-stack}

```bash
docker compose -f docker-compose.test.yml up -d
```

To objavi vse le na loopback naslovu:

| Storitev | Host naslov | Opombe |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5433, ne 5432, da ne pride do konflikta z lokalnim strežnikom |
| Dovecot, plain | `127.0.0.1:1143` | Omogočena je avtentikacija v obliki besedila |
| Dovecot, TLS | `127.0.0.1:1993` | Samodejan podpisani certifikat slike |

Da ga ustavite in zavrnete vse baze podatkov ter shranjeno pošto:

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ Dve bazi podatkov {#the-two-databases}

Testni suite pusti za sebo vrstice v okvirih (fixtures): po celotnem teku testna baza vsebuje več deset e-poštnih računov in uporabnikov. Če bi aplikacija uporabljala isto bazo, bi njen proces sinhronizacije v ozadju te okvirne podatke prenalagal in ponovljeno poskušal doseči strežnike, ki ne obstajajo. Zato postgres kontejner ob prvi inicializaciji ustvari dve bazi podatkov
(`tests/postgres/10-create-dev-db.sql`):

| Baza podatkov | Uporablja jo | Konfigurirano v |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

Oba konfiguracijskega datoteke sta v .gitignore in ju je treba ustvariti lokalno.

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

`appsettings.Development.json` v korenini repozitorija:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` je pomembno zunaj kontejnerja: privzeto je nastavljena absolutna pot
`/app/DataProtection-Keys`, ki na razvojnem stroju ne obstaja, brez nje pa vsaka zahteva ne uspe, ko brlja ključno veriga (key ring). `IgnoreSelfSignedCert` omogoča aplikaciji,
da sprejme Dovecot certifikat na portu 1993.

## ✅ Zagon testov {#running-the-tests}

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

Migracije jih `TestDbFixture` uporabi samodejno ob prvem zagonu. Večina testov sploh ne potrebuje
baze podatkov; preostali uporabljajo PostgreSQL, zato v primeru, da stek je ni v teku,
poročajo napako z `Name or service not known`.

Niz povezave (connection string) je mogoče posloditi tudi prek okolja, kar prehiti vsako
namestitveno datoteko:

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ Zagnem aplikacijo {#running-the-application}

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` že nastavi `ASPNETCORE_ENVIRONMENT=Development` za
`dotnet run`, zato deluje tudi navaden `dotnet run --project MailArchiver.csproj` in streži
http://localhost:5179 (profil `http`). `ASPNETCORE_URLS` zgoraj pa namesto tega izbere namenski port
v kontejnerju 5000.

Preostale migracije se ob zagonu uporabijo na `MailArchiverDev`. Prijavite se s podatki iz `appsettings.json`
(`Authentication:Username` in `Authentication:Password`).

## 📬 Uporaba cilja Dovecot {#using-the-dovecot-target}

Dovecot sprejme **kateri koli uporabniški z Brez_vsebine_`pass`**, zato nova ciljna poštarica ne zahteva zagotavljanja (provisioning): izberite ime in se prijavite. Dodajte jo pod Mail Accounts z:

| Polje | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | karkoli, npr. `offload-target@example.com` | isto |
| Password | `pass` | isto |

Konfiguracija v `tests/dovecot/dovecot.conf` je modelirana na privzeto nastavitev same slike, s tremi namernimi razlikami, zaradi katerih se obnaša kot cilj mailcow:

- **Maildir shranjevanje** namesto sdbox slike, kar uporablja mailcow.
- **`separator = /`**, kar ustreza mailcow. Poti map se razdelijo na `/` in `\`, ko obnavljanje (restore) ponovno ustvari hierarhijo map, zato bi strežnik, ki uporablja `.`, obnašal drugače.
- **Posebne mape `Drafts`, `Junk`, `Sent` in `Trash` so že predhodno ustvarjene**, saj jih mailcow ustvari vnaprej. To je pomembno pri testiranju migracije iz Exchange: izvorna mapa z imenom `Sent Items` se postavi poleg obstoječe `Sent` namesto da bi se združila z njo, kar je situacija, s katero se mora ukrajati preimenovanje map.

Avtentikacija v obliki besedila (plaintext) je omogočena na portu 1143, ker se aplikacija povezuje brez kakršne koli transportne varnosti, ko je za račun SSL onemogočen.

## 🌱 Seeding an archive to test against {#seeding-an-archive-to-test-against}

Prazna arhiva ni za kaj je uporabna pri testiranju obnovitve ali migracije, zato `tests/seed/seed.sh` ustvari eno:

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```

Ustvari dva računa, uvozi podatke (fixtures) in izpiše, kaj je bilo uvoženo. Oba računa sta ustvarjena z letnim intervalom sinhronizacije na račun, vir pa je dodatno onemogočen, zato ga storitev sinhronizacije v ozadju ignorira: njegov strežnik ne obstaja, cilj v Dovecotu pa je treba le dodajati nove podatke. Ena opozorilna stvar: stanje načrtovanja živi v spominu, zato vsak ponovni zagon aplikacije takoj načrtuje cilj enkrat. Cilj zato izključuje tudi Dovecotove mape posebnih namenskih uporab (`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`), tako da ima sinhronizacija ob prvem ponovnem zagonu ničesar za arhiviranje. Dvakratno zagnanje skripti je neškodljivo; drugi zagon prijavi, da je vsaka sporočilo že prisotno.

Podatki (fixtures) so shranjeni pod `tests/seed/fixtures`, en mbox na mapo vira, pri čemer `manifest.tsv` vsako datoteko presloji v ciljno mapo. Uvoženi so prek lastnega `--import-mbox` CLI aplikacije in ta podrobnost je namerna: CLI potuje prek `MailImporter.ImportEmailToDatabase`, istega uvožnika, ki ga uporablja IMAP sinhronizacija, zato podatki pristanejo v točno takšnem formatu, kot ga ima prava arhiva -- naslovi združeni s `", "`, zadeve posredovane skozi `CleanText`, `SentDate` pretvorjene v časovni zonu za prikaz, `RawHeaders` izlučene in skrajšane. Direktno vstavanje vrstkov v bazo podatkov bi črtalo vse to in proizvedlo podatke, ki se tiho razlikujejo od produkcijskih podatkov.

Vsako sporočilo v podatkih vsebuje `X-Fixture-Purpose` glavo, ki pojasnjuje, zakaj obstaja, zato datoteke same dokumentirajo svojo vsebino:

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```

Datumi so fiksni namesto relativnih glede na trenutni dan. Meja migracije se pred zagonom opravila razreši v absolutni datum, zato fiksni niz podatkov in fiksna meja testirata isto stvar bolj predvidljivo kot premikajoča se meja. Sporočila pokrivajo obdobje od leta 2012 do 2026.

Postavitev map odraža Exchange poštni predal, vsaka mapa pa obstaja, da preizkusi določeno funkcijo:

| Mapa vira | Zakaj je tam |
|---|---|
| `INBOX` | datumi, ki pokrivajo petnajst let, plus spodnja sporočila s specifičnimi primerji |
| `Sent Items` | kolidira z obstoječim Dovecotovim posebnim `Sent` ciljem |
| `Sent Items/2019` | preimenovanje mora prepisati le začetni segment poti |
| `Sent Items Archive` | deli prefiks kot podniz, vendar ne na meji segmenta, zato ga ne sme prepisati |
| `Deleted Items` | izključitev mape |
| `Deleted Items/Old` | izključitev na starševski mapi mora pokriti tudi podmape |
| `Junk E-Mail` | Exchangeov naziv za mapo, ki jo Dovecot kliče `Junk` |
| `Archive/Projects/2020` | trije nivoji vstavljanja |

Posamezna sporočila vključujejo sporočilo brez `Message-ID` glave (ki dobi deterministično nadomestno vrednost), sporočilo, čeprav ima `Message-ID` brez `@` (ki se ob obnovitvi ne obnovi v celoti), zadevo, ki vsebuje kontrolni znak, sporočilo s tri-korakno `Received` verigo in sporočilo brez nje, več sporočil z dvema `To` prejemnikoma ter dve skoraj identični pari z enim sekundo razlike.

### Opomba o izstopnem kodu {#a-note-on-the-exit-code}

`tests/seed/seed.sh` preveri število neuspešnih in neveljavnih uvozov iz izhoda uvoza namesto izstopnega koda procesa. `MBoxImportService` prijavi `CompletedWithErrors`, ko je preskočil podvojene podatke, CLI pa vsak status zraven `Completed` presloži v izstop 1, zato ponovni zagon uvoza konča z nenulno vrednostjo, tudi če nič ni šlo narobe. Ponovni zagon skripte za seme je zato varen in prijavi vsako sporočilo kot že prisotno.

## ⚠️ Ni za produkcijo {#not-for-production}

- Vsak uporabniški naziv se avtentira v Dovecot z enim fiksno geslom.
- Avtentacija v obliki čistega besedila (plaintext) je omogočena.
- Podatkovne prednosti so dokumentirane privzete vrednosti.
- Stanje pošte in baze podatkov se nahaja v Docker volumih, ki jih `down -v` izbriše.

## 🔧 Reševanje težav {#troubleshooting}

**Testi ne uspejo z `Name or service not known`** — sklad je ni v teku, ali pa
manjka `appsettings.Test.json`, zato se povezovalna niza vrne na privzeto vrednostovno nastavitvo posode
`Host=postgres`.

**`NETSDK1226` pri gradnji** — nepopolni podatki o delovnem pogojevanju v lokalnem .NET SDK, kar ni povezano s tem
repozitorijem. Po ali zaženite `dotnet workload repair`, ali gradite z
`-p:AllowMissingPrunePackageData=true`.

**Dovecot se ponovno zagona v zanko** — napaka v konfiguraciji. `docker compose -f
docker-compose.test.yml logs dovecot` prijavi napredenjo vrstico. Upoštevajte, da Dovecot ne
sprejema več nastavitev v eni vrstici znotraj bloka.

**Aplikacija pri vsaki zahtevi vrne HTTP 500** — običajno gre za zbornik ključev za zaščito podatkov (data protection key ring).
Preverite, ali `DataProtection:KeyPath` kaže na zapisljivo direktorij.
