# 🧪 Lokalne środowisko testowe {#local-test-environment}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik opisuje, jak uruchomić zestaw testowy, aplikację oraz cel IMAP na maszynie deweloperskiej. Jest on skierowany do kontrybutorów: stos, który jest uruchamiany, jest celowo niebezpieczny i nie stanowi instrukcji wdrożenia. Aby dowiedzieć się więcej o wdrożeniu, zapoznaj się z
[Instrukcją konfiguracji](Setup.md).

Zaangażowane są trzy elementy:

| Element | Dostarczany przez | Cel |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | Dwie bazy danych, jedna dla `dotnet test` i jedna dla aplikacji |
| Dovecot | `docker-compose.test.yml` | Cel IMAP zastępujący Skrzynka pocztowa mailcow |
| Aplikacja | `dotnet run` na hoście | Szybsza iteracja niż przebudowywanie obrazu kontenera |

## 🚀 Uruchamianie stosu {#starting-the-stack}

```bash
docker compose -f docker-compose.test.yml up -d
```

To publikuje wszystko wyłącznie na loopback:

| Usługa | Adres hosta | Uwagi |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5433, a nie 5432, aby nie kolidowało z lokalnym serwerem |
| Dovecot, plain | `127.0.0.1:1143` | Uwierzytelnianie tekstowe jest włączone |
| Dovecot, TLS | `127.0.0.1:1993` | Samopodpisany certyfikat obrazu |

Aby zatrzymać stos i usunąć wszystkie bazy danych oraz przechowywaną pocztę:

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ Dwie bazy danych {#the-two-databases}

Pakiet testowy pozostawia po sobie wiersze danych testowych (fixtures): po pełnym przebiegu baza danych testowych zawiera kilkanaście kont pocztowych i użytkowników. Gdyby aplikacja współdzieliła tę bazę danych, jej procesowa Synchronizacja w tle wykryłaby te dane testowe i wielokrotnie próbowałaby połączyć się z serwerami, które nie istnieją. Dlatego kontener postgres tworzy dwie bazy danych podczas pierwszej inicjalizacji
(`tests/postgres/10-create-dev-db.sql`):

| Baza danych | Używana przez | Skonfigurowana w |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

Oba pliki konfiguracyjne są pomijane przez git i muszą zostać utworzone lokalnie.

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

`appsettings.Development.json` w głównym katalogu repozytorium:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` ma znaczenie poza kontenerem: domyślnie ustawiana jest absolutna ścieżka
`/app/DataProtection-Keys`, która nie istnieje na maszynie deweloperskiej, a bez niej każde
żądanie kończy się niepowodzeniem podczas odczytu pierścienia kluczy (key ring). `IgnoreSelfSignedCert` pozwala aplikacji
zaakceptować certyfikat Dovecot na porcie 1993.

## ✅ Uruchamianie testów {#running-the-tests}

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

Migracje są automatycznie stosowane przez `TestDbFixture` podczas pierwszego uruchomienia. Większość testów w ogóle nie wymaga bazy danych; pozostałe korzystają z PostgreSQL, więc jeśli stos nie jest uruchomiony, zakończą się błędem `Name or service not known`.

Ciąg połączenia można również przekazać za pomocą zmiennej środowiskowej, co nadpisuje każdy plik konfiguracyjny:

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ Uruchamianie aplikacji {#running-the-application}

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` ustawia już `ASPNETCORE_ENVIRONMENT=Development` dla
`dotnet run`, więc zwykły `dotnet run --project MailArchiver.csproj` również działa i obsługuje
http://localhost:5179 (profil `http`). Powyższy `ASPNETCORE_URLS` zamiast tego wybiera port
kontenera 5000.

Oczekujące migracje są stosowane do `MailArchiverDev` podczas uruchamiania. Zaloguj się przy użyciu danych uwierzytelniających z `appsettings.json`
(`Authentication:Username` i `Authentication:Password`).

## 📬 Używanie celu Dovecot {#using-the-dovecot-target}

Dovecot akceptuje **dowolną nazwę użytkownika** z hasłem `pass`, więc nowa Skrzynka pocztowa celu nie wymaga aprovisionowania: wybierz nazwę i zaloguj się. Dodaj ją w sekcji Mail Accounts za pomocą:

| Pole | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | cokolwiek, np. `offload-target@example.com` | to samo |
| Password | `pass` | to samo |

Konfiguracja w `tests/dovecot/dovecot.conf` jest wzorowana na domyślnych ustawieniach obrazu, z trzema celowymi różnicami, które sprawiają, że zachowuje się ona jak cel mailcow:

- **Przechowywanie Maildir** zamiast sdbox obrazu, co jest wykorzystywane przez mailcow.
- **`separator = /`**, zgodnie z mailcow. Ścieżki Folderów są dzielone na `/` i `\`, gdy Przywracanie odtwarza hierarchię folderów, więc serwer używający `.` zachowywałby się inaczej.
- **Foldery special-use `Drafts`, `Junk`, `Sent` i `Trash` są wstępnie utworzone**, tak jak mailcow je wstępnie tworzy. Ma to znaczenie podczas testowania migracji z Exchange: źródłowy Folder o nazwie `Sent Items` ląduje obok istniejącego `Sent` zamiast z nim połączyć, co jest sytuacją, z którą musi poradzić sobie zmiana nazw folderów.

Uwierzytelnianie tekstowe (Plaintext) jest włączone na porcie 1143, ponieważ aplikacja łączy się bez żadnego zabezpieczenia transportu, gdy Konto ma wyłączone SSL.

## 🌱 Wypełnianie Archiwum danymi do testów {#seeding-an-archive-to-test-against}

Puste Archiwum nie nadaje się do testowania operacji takich jak Przywracanie czy migracja, dlatego
`tests/seed/seed.sh` tworzy jedno:

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```

Tworzy dwa Konta, importuje dane testowe (fixtures) i wypisuje to, co zostało zaimportowane. Oba Konta
zostały utworzone z interwałem Synchronizacji na poziomie konta wynoszącym rok, a źródło jest dodatkowo wyłączone,
więc usługa Synchronizacji działająca w tle je ignoruje: jego serwer nie istnieje, a cel w Dovecot
wymaga jedynie dopisywania nowych danych. Jedna uwaga: stan harmonogramu znajduje się w pamięci, więc każde
uruchomienie aplikacji planuje synchronizację celu raz, natychmiast. Cel wyklucza zatem specjalne Foldery Dovecot (`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`), aby
podczas pierwszej synchronizacji po restarcie nie było czego archiwizować. Dwukrotne uruchomienie skryptu jest nieszkodliwe;
drugie uruchomienie zgłasza każdą wiadomość jako już obecną.

Dane testowe znajdują się w `tests/seed/fixtures`, jeden plik mbox na każdy źródłowy Folder, z
`manifest.tsv` mapującym każdy plik na jego docelowy Folder. Są one importowane za pomocą
własnego CLI aplikacji `--import-mbox` i ten szczegół jest zamierzony: CLI kieruje ruch przez
`MailImporter.ImportEmailToDatabase`, ten sam importer, którego używa synchronizacja IMAP, dzięki czemu wiersze trafiają
dokładnie w takim formacie, w jakim przechowuje je prawdziwe Archiwum -- adresy połączone za pomocą `", "`, tematy przekazane przez
`CleanText`, `SentDate` skonwertowane na strefę czasową wyświetlania, `RawHeaders` wyodrębnione i skrócone.
Bezpośrednie wstawianie wierszy do bazy danych spowodowałoby pominięcie tych wszystkich kroków i stworzenie danych testowych, które
po cichu różniłyby się od danych produkcyjnych.

Każda wiadomość testowa posiada nagłówek `X-Fixture-Purpose` informujący o tym, dlaczego istnieje, dzięki czemu pliki
same siebie dokumentują:

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```

Daty są stałe, a nie relatywne względem bieżącego dnia. Granica migracji jest rozwiązywana do
bezwzględnej daty przed uruchomieniem zadania, dzięki czemu stały zestaw danych testowych i stała granica pozwalają testować to samo w bardziej przewidywalny sposób niż granica przesuwna. Wiadomości obejmują lata od 2012 do 2026.

Układ Folderów odzwierciedla Skrzynkę pocztową Exchange, a każdy Folder istnieje w celu przetestowania konkretnej funkcji:

| Źródłowy Folder | Dlaczego tam jest |
|---|---|
| `INBOX` | daty obejmujące piętnaście lat, plus poniższe wiadomości brzegowe |
| `Sent Items` | koliduje z istniejącym specjalnym `Sent` celu Dovecot |
| `Sent Items/2019` | zmiana nazwy musi nadpisać tylko początkowy segment ścieżki |
| `Sent Items Archive` | współdzieli prefiks jako podciąg, ale nie na granicy segmentu, więc nie może zostać zmieniony |
| `Deleted Items` | wykluczenie Folderu |
| `Deleted Items/Old` | wykluczenie na nadrzędnym elemencie musi obejmować potomków |
| `Junk E-Mail` | nazwa Exchange dla Folderu, który Dovecot nazywa `Junk` |
| `Archive/Projects/2020` | trzy poziomy zagnieżdżenia |

Poszczególne wiadomości obejmują wiadomość bez nagłówka `Message-ID` (której przypisany zostaje deterministyczny
fallback), wiadomość, której `Message-ID` nie ma `@` (która nie przetrwa nienaruszona operacji Przywracanie), temat zawierający znak sterujący, wiadomość z łańcuchem `Received` o trzech skokach oraz wiadomość bez żadnego skoku, kilka wiadomości z dwoma odbiorcami `To` oraz dwie pary niemal identycznych wiadomości oddzielonych o sekundę.

### Uwaga dotycząca kodu wyjścia {#a-note-on-the-exit-code}

`tests/seed/seed.sh` sprawdza liczbę błędów i nieprawidłowych formatów w wyjściu importu, zamiast
polegać na kodzie wyjścia procesu. `MBoxImportService` raportuje `CompletedWithErrors`, gdy pominięto
duplikaty, a CLI mapuje każdy status inny niż `Completed` na wyjście 1, więc ponowne uruchomienie
importu kończy się kodem niezerowym, nawet jeśli nic nie poszło nie tak. Ponowne uruchomienie skryptu wypełniającego jest zatem bezpieczne
i zgłasza każdą wiadomość jako już obecną.

## ⚠️ Nie do użytku produkcyjnego {#not-for-production}

- Każda nazwa użytkownika uwierzytelnia się w Dovecot za pomocą jednego stałego hasła.
- Uwierzytelnianie tekstem jawnym jest włączone.
- Dane uwierzytelniające bazy danych to udokumentowane wartości domyślne.
- Stan poczty i bazy danych znajduje się w wolumenach Docker, które `down -v` usuwa.

## 🔧 Rozwiązywanie problemów {#troubleshooting}

**Testy kończą się błędem `Name or service not known`** — stos nie jest uruchomiony lub
brakuje `appsettings.Test.json`, przez co ciąg połączenia powraca do domyślnej wartości kontenera
`Host=postgres`.

**`NETSDK1226` podczas budowania** — niekompletne dane obciążenia w lokalnym .NET SDK, niezwiązane z tym
repozytorium. Uruchom `dotnet workload repair` lub buduj za pomocą
`-p:AllowMissingPrunePackageData=true`.

**Dovecot restartuje się w pętli** — błąd konfiguracji. Polecenie `docker compose -f
docker-compose.test.yml logs dovecot` raportuje linię powodującą błąd. Należy pamiętać, że Dovecot nie
akceptuje kilku ustawień w jednej linii wewnątrz bloku.

**Aplikacja zwraca HTTP 500 przy każdym żądaniu** — zazwyczaj chodzi o pierścień kluczy ochrony danych.
Sprawdź, czy `DataProtection:KeyPath` wskazuje na katalog z uprawnieniem do zapisu.
