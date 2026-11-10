# 🧪 Helyi tesztkörnyezet {#local-test-environment}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Ez az útmutató leírja, hogyan futtatható le a tesztsuite, az alkalmazás és egy IMAP célpont egy fejlesztői gépen. Célközvetlenül a hozzájárulóknak (contributors) szól: az általa felállított technológiai stack szándékosan biztonság nélküli, és nem szolgál üzembe helyezési útmutatóként. Üzembe helyezéshez lásd a
[Setup Guide](Setup.md) dokumentációt.

Három komponens vesz részt a folyamatban:

| Komponens | Biztosítja | Célja |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | Két adatbázis, egy a `dotnet test`-nek és egy az alkalmazásnak |
| Dovecot | `docker-compose.test.yml` | Egy IMAP célpont, amely egy mailcow postafiókot helyettesíti |
| Az alkalmazás | `dotnet run` a gazdagépön | Gyorsabb iteráció, mint a konténerkép újraépítése |

## 🚀 A stack elindítása {#starting-the-stack}

```bash
docker compose -f docker-compose.test.yml up -d
```

Ez mindent csak loopback interfészen tesz közzé:

| Szolgáltatás | Host cím | Megjegyzések |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5433, nem 5432, hogy ne ütközön helyi szerverrel |
| Dovecot, plain | `127.0.0.1:1143` | A plain text hitelesítés engedélyezve van |
| Dovecot, TLS | `127.0.0.1:1993` | A kép saját aláírású tanúsítványa |

A leállításhoz, valamint az összes adatbázis és tárolt levél törléséhez:

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ A két adatbázis {#the-two-databases}

A tesztsuite fixture sorokat hagy maga után: egy teljes futás után a tesztadatbázis több tucat posta-fiókot és felhasználót tartalmaz. Ha az alkalmazás megosztaná azt az adatbázist, a háttérszinkronizáció felismerné ezeket a fixtureket, és ismételten megpróbálna elérni olyan szervereket, amelyek nem léteznek. Ezért a postgres konténer az első inicializáláskor két adatbázist hoz létre (`tests/postgres/10-create-dev-db.sql`):

| Adatbázis | Használja | Konfigurálva itt |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

Mindkét konfigurációs fájl gitignored, így őket helyileg kell létrehozni.

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

`appsettings.Development.json` a repository gyökerében:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` számít a konténeren kívül is: alapértelmezés szerint az abszolút útvonalat `/app/DataProtection-Keys` állítja be, amely nem létezik fejlesztői gépen, és nélküle minden kérés elbukik a key ring olvasása közben. `IgnoreSelfSignedCert` teszi lehetővé az alkalmazás számára, hogy elfogadja a Dovecot tanúsítványt az 1993-as porton.

## ✅ Tesztek futtatása {#running-the-tests}

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

A migrációkat az első futtatáskor automatikusan az `TestDbFixture` végzi el. A legtöbb teszthez
egyáltalán nincs szükség adatbázisra; a többi PostgreSQL-t használ, így
`Name or service not known` hibát fognak jelezni, ha a stack nincs futva.

A connection string is átadható környezetváltozókon keresztül, ami felülír minden
konfigurációs fájlt:

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ Az alkalmazás futtatása {#running-the-application}

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` már beállítja a `ASPNETCORE_ENVIRONMENT=Development`-t a
`dotnet run` számára, így egy egyszerű `dotnet run --project MailArchiver.csproj` is működik, és a
http://localhost:5179 (a `http` profil) címet szolgálja. A fenti `ASPNETCORE_URLS` viszont csak a konténer
5000-es portját választja be.

A függőben lévő migrációk a `MailArchiverDev` helyen kerülnek alkalmazásra a folyamat elindításakor. Jelentkezzen be a `appsettings.json` hiteles adatokkal (`Authentication:Username` és `Authentication:Password`).

## 📬 A Dovecot célpont használata {#using-the-dovecot-target}

A Dovecot **bármilyen felhasználónév** elfogadása `pass` jelszóval, így az új célposta nem igényel előre beállítást: válasszon egy nevet, és jelentkezzen be. Adja hozzá a Mail Accounts alatt a következő adatokkal:

| Mező | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | bármi, pl. `offload-target@example.com` | ugyanaz |
| Password | `pass` | ugyanaz |

A `tests/dovecot/dovecot.conf` fájlban található konfiguráció az image saját alapértelmezett beállításaiban készült, három szándékos különbséggel, amelyek miatt úgy viselkedik, mint egy mailcow célpont:

- **Maildir tárolás** az image sdbox helyett, ahogy azt a mailcow is használja.
- **`separator = /`**, megfelel a mailcow-nak. A mappák elérési útja `/` és `\` alapján van szétválasztva, amikor egy visszaállítás újraciptál egy mappahierarchiát, így egy `.` használatára épülő szerver másképp viselkedne.
- **A `Drafts`, `Junk`, `Sent` és `Trash` speciális célfolyamban (special-use folders) lévő mappák előre létre vannak hozva**, ahogy a mailcow is teszi. Ez fontos az Exchange-ről történő migráció tesztelésekor: egy `Sent Items` nevű forrásmappa a meglévő `Sent` mellett jelenik meg, ahelyett, hogy összeolvadna vele, ami éppen az a helyzet, amellyel a mappanevezések átnevezésének meg kell foglalkoznia.

A Plaintext hitelesítés engedélyezve van az 1143-as porton, mert az alkalmazás szállítási biztonsági protokoll nélkül csatlakozik, ha egy fiók SSL kikapcsolva van.

## 🌱 Archívum adatok létrehozása a tesztekhez {#seeding-an-archive-to-test-against}

Egy üres archívum nem túl hasznos a visszaállítás vagy a migráció teszteléséhez, ezért
`tests/seed/seed.sh` létrehoz egyet:

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```

Ez létrehoz két fiókot, importálja a fixture adatokat, majd kiírja, mi került be. Mindkét fiókot
egy éves, fiókonkénti szinkronizational állítják be, a forrást pedig további biztonság érdekében
letiltják, így a háttérszinkronizáló szolgáltatás figyelmen kívül hagyja: a szerverét nem éri, és a
Dovecot célpontba csak hozzáférési (append) művelet szükséges. Egy apró hátrány: az ütemezési
állapot az emlékben (in-memory) él, így az alkalmazás minden újraindítása azonnal elindít egy
szinkronizálást a célpontra. Ezért a célpont tartalmazza a Dovecot speciális használatú mappáit is
(`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`), hogy az első újraindítási szinkron ne találjon semmit
archíválható adatként. A szkript kétszer történő futtatása ártatlan; a második futtatás minden
üzenetet már meglévőnek jelent.

A fixture adatokat a `tests/seed/fixtures` alatt tárolják, forrásmappánként egy mbox formátumban, ahol a
`manifest.tsv` térképezi le a fájlokat a célmappjukhoz. Ezek az alkalmazás saját `--import-mbox` CLI-jén keresztül
kerülnek importálásra, és ez a részlet szándékos: a CLI a `MailImporter.ImportEmailToDatabase`-en keresztül介vez, ami ugyanaz az
importer, amelyet az IMAP szinkronizálás is használ, így az adatok pontosan olyan formátumに
érkeznek, ahogyan egy valódi archívum tárolja őket – az címzettek `", "` segítségével egyesülnek,
a tárgyak (subject) átmennek az `CleanText`-on keresztül, az `SentDate`-et a megjelenítési időzónába
konvertálják, az `RawHeaders`-öt pedig kinyerik és levágják. Ha az adatokat közvetlenül az adatbázisba
helyeznék, ez minden átalakítást kihagy, és olyan fixture adatokat hozna létre, amelyek csendben
eltérnek a termelési adatoktól.

Minden fixture üzenet egy `X-Fixture-Purpose` fejlécet tartalmaz, amely elmondja, miért létezik, így a fájlok
saját magukat dokumentálják:

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```

A dátumok fixek, nem pedig a jelenlegi naphoz viszonyítva. A migrációs lezárási időpontot (cutoff)
abszolút dátummá oldják fel az feladat futása előtt, így egy fix fixture készlet és egy fix
lezárási időpont kiszámíthatóbban teszteli ugyanazt, mint egy változó. Az üzenetek 2012 és 2026
között időzülnek.

A mappastruktúra egy Exchange postafiókot tükröz, és minden mappa azért létezik, hogy valamit
teszteljen:

| Forrásmappa | Miért van ott |
|---|---|
| `INBOX` | tizenöt évet lefedő dátumok, valamint az alábbi szempontok szerinti határesetek |
| `Sent Items` | ütközik a Dovecot célpont meglévő speciális használatú `Sent` mappájával |
| `Sent Items/2019` | egy átnevezítésnek csak az első útvonalrészt kell átírnia |
| `Sent Items Archive` | egy részét megosztja egy prefixszel, de nem egy mappa határon, ezért nem szabad
átírni |
| `Deleted Items` | mappa kizárás |
| `Deleted Items/Old` | egy szülő mappa kizárásának lefednie kell az utódokat is |
| `Junk E-Mail` | az Exchange neve egy mappának, amelyet a Dovecot `Junk` néven hív |
| `Archive/Projects/2020` | három szintű egymásba ágyazás |

Az egyes üzenetek egy olyan üzenetet tartalmaznak, amelynek nincs `Message-ID` fejlécével (amely
determinisztikus alapértelmezést kap), egyet, amelynek a `Message-ID` nem tartalmaz `@`-et (ami nem
túlél egy visszaállítást), egy tárgyat, amely tartalmaz egy vezérlő karaktert, egy üzenetet
három ugrásos `Received` lánccal és egyet anélkül, többet két `To` címzettel, valamint két
egymáshoz nagyon hasonló üzenetet egy másodperccel egymás után.

### Megjegyzés a kilépési kódról {#a-note-on-the-exit-code}

`tests/seed/seed.sh` az import kimenetéből ellenőrzi a hibás és rosszul formázott adatok számát, nem pedig a
folyamat kilépési kódját. `MBoxImportService` azt jelzi, ha `CompletedWithErrors`, amikor duplikált adatokat hagyott ki,
és a CLI minden olyan státuszt, amely nem `Completed`, 1-es kilépési kóddal kezel, így az import
újraindítása nem nulla kilépéssel zárul, még akkor is, ha nem történt hiba. Ezért a seed
skript újraindítása biztonságos, és minden üzenetet már meglévőnek jelent.

## ⚠️ Nem érdemes terméküzembe helyezni {#not-for-production}

- Bármilyen felhasználónév a Dovecot hitelesítését egyetlen fix jelszóval végzi el.
- A plaintext hitelesítés engedélyezve van.
- Az adatbázis hitelesítési adatai a dokumentált alapértelmezettek.
- A levelek és az adatbázis állapota olyan Docker volume-ekben tárolódik, amelyeket `down -v` töröl.

## 🔧 Hibaelégítés {#troubleshooting}

**A tesztek `Name or service not known` hibával leállnak** — a stack nincs futó állapotban, vagy
hiányzik a `appsettings.Test.json`, így a connection string visszavált a konténer alapértelmezett
`Host=postgres` értékére.

**`NETSDK1226` a build során** — hiányos munkaterhelési adatok a helyi .NET SDK-ban, amik nem kapcsolódnak ehhez a
tárgyhoz. Vagy futtassa le a `dotnet workload repair` parancsot, vagy építse le a
`-p:AllowMissingPrunePackageData=true` használatával.

**Dovecot folyamatos újraindítási ciklusban van** — konfigurációs hiba. A `docker compose -f
docker-compose.test.yml logs dovecot` megjelenti a hibás sort. Figyelem: a Dovecot nem
fogad el több beállítást egyetlen soron egy blokkon belül.

**Az alkalmazás minden kérésre HTTP 500 hibát ad vissza** — ez általában a data protection key ring hibája.
Ellenőrizze, hogy a `DataProtection:KeyPath` egy írható könyvtárra mutat-e.
