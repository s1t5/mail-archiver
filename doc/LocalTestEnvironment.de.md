# 🧪 Lokale Testumgebung {#local-test-environment}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Diese Anleitung beschreibt, wie Sie die Testsuite, die Anwendung und ein IMAP-Ziel auf einer Entwicklungsmaschine ausführen. Sie richtet sich an Mitwirkende: Der bereitgestellte Stack ist absichtlich unsicher und ist kein Leitfaden für eine Bereitstellung. Informationen zur Bereitstellung finden Sie im [Setup Guide](Setup.md).

Drei Komponenten sind beteiligt:

| Komponente | Bereitgestellt von | Zweck |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | Zwei Datenbanken, eine für `dotnet test` und eine für die Anwendung |
| Dovecot | `docker-compose.test.yml` | Ein IMAP-Ziel, das stellvertretend für ein mailcow Postfach dient |
| Die Anwendung | `dotnet run` auf dem Host | Schnellerer Iterationszyklus als durch das Neuerstellen des Container-Images |

## 🚀 Starten des Stacks {#starting-the-stack}

```bash
docker compose -f docker-compose.test.yml up -d
```

Dies veröffentlicht alles nur auf Loopback:

| Service | Host-Adresse | Anmerkungen |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5433, nicht 5432, damit es nicht mit einem lokalen Server kollidiert |
| Dovecot, plain | `127.0.0.1:1143` | Plaintext-Authentifizierung ist aktiviert |
| Dovecot, TLS | `127.0.0.1:1993` | Das selbstsignierte Zertifikat des Images |

Um es zu stoppen und alle Datenbanken sowie gespeicherten E-Mails zu verwerfen:

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ Die zwei Datenbanken {#the-two-databases}

Die Testsuite hinterlässt Fixture-Zeilen: Nach einem vollständigen Durchlauf enthält die Testdatenbank mehrere Dutzend Mail-Konten und Benutzer. Wenn die Anwendung diese Datenbank teilen würde, würde ihre Hintergrund-Synchronisierung diese Fixtures erfassen und wiederholt versuchen, Server zu erreichen, die nicht existieren. Daher erstellt der postgres-Container bei der ersten Initialisierung zwei Datenbanken
(`tests/postgres/10-create-dev-db.sql`):

| Datenbank | Wird verwendet von | Konfiguriert in |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

Beide Konfigurationsdateien sind per gitignore ausgeschlossen und müssen lokal erstellt werden.

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

`appsettings.Development.json` im Repository-Root:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` ist außerhalb des Containers wichtig: Standardmäßig ist es der absolute Pfad
`/app/DataProtection-Keys`, der auf einer Entwicklungsmaschine nicht existiert, und ohne diesen schlägt jede
Anfrage beim Lesen des Keyrings fehl. `IgnoreSelfSignedCert` ermöglicht es der Anwendung,
das Dovecot-Zertifikat auf Port 1993 zu akzeptieren.

## ✅ Ausführen der Tests {#running-the-tests}

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

Migrationen werden beim ersten Durchlauf automatisch durch `TestDbFixture` angewendet. Die meisten Tests benötigen überhaupt keine Datenbank; der Rest verwendet PostgreSQL, sodass sie mit `Name or service not known` fehlschlagen, wenn der Stack nicht läuft.

Ein Connection String kann auch über die Umgebung bereitgestellt werden, welcher jede Konfigurationsdatei überschreibt:

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ Ausführen der Anwendung {#running-the-application}

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` setzt bereits `ASPNETCORE_ENVIRONMENT=Development` für
`dotnet run`, sodass auch ein einfacher `dotnet run --project MailArchiver.csproj` funktioniert und
http://localhost:5179 (das `http`-Profil) bedient. `ASPNETCORE_URLS` oben wählt stattdessen nur den Container-Port
5000 aus.

Ausstehende Migrationen werden beim Start auf `MailArchiverDev` angewendet. Melden Sie sich mit den Anmeldedaten aus `appsettings.json`
an (`Authentication:Username` und `Authentication:Password`).

## 📬 Verwendung des Dovecot-Ziels {#using-the-dovecot-target}

Dovecot akzeptiert **jeden Benutzernamen** mit dem Passwort `pass`, sodass für ein neues Ziel-Postfach keine Bereitstellung erforderlich ist: Wählen Sie einen Namen und melden Sie sich an. Fügen Sie es unter Mail Accounts hinzu mit:

| Feld | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | irgendetwas, z. B. `offload-target@example.com` | gleich |
| Password | `pass` | gleich |

Die Konfiguration in `tests/dovecot/dovecot.conf` ist dem Standard des Images nachempfunden, mit drei bewussten Unterschieden, die es wie ein mailcow-Ziel verhalten lassen:

- **Maildir-Speicherung** anstelle des sdbox des Images, was mailcow verwendet.
- **`separator = /`**, passend zu mailcow. Ordnerpfade werden bei einer Wiederherstellung, wenn eine Ordnerhierarchie neu erstellt wird, an `/` und `\` getrennt, daher würde ein Server, der `.` verwendet, sich anders verhalten.
- **Die Special-Use-Ordner `Drafts`, `Junk`, `Sent` und `Trash` sind vorab erstellt**, da mailcow sie ebenfalls vorab erstellt. Dies ist wichtig beim Testen einer Migration von Exchange: Ein Quellordner mit dem Namen `Sent Items` landet neben dem bestehenden `Sent`, anstatt mit ihm zu verschmelzen, was die Situation ist, mit der die Ordnerumbenennung umgehen muss.

Die Klartext-Authentifizierung ist auf Port 1143 aktiviert, da sich die Anwendung ohne jegliche Transportsicherheit verbindet, wenn für ein Konto SSL deaktiviert ist.

## 🌱 Ein Archiv zum Testen befüllen {#seeding-an-archive-to-test-against}

Ein leeres Archiv ist für das Testen einer Wiederherstellung oder einer Migration wenig nützlich, daher
erstellt `tests/seed/seed.sh` eines:

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```

Es erstellt zwei Konten, importiert die Fixtures und gibt aus, was gelandet ist. Beide Konten werden
mit einem pro Konto festgelegten Synchronisierungsintervall von einem Jahr erstellt, und die Quelle ist
zusätzlich deaktiviert, sodass der Hintergrund-Synchronisierungsdienst sie ignoriert: Ihr Server existiert nicht, und das Dovecot-Ziel muss lediglich angehängt werden. Ein Vorbehalt: Der Zeitplanungsstatus lebt im Speicher, sodass jeder
Anwendungsneustart das Ziel einmal sofort plant. Das Ziel schließt daher auch die Dovecot Special-Use-Ordner (`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`) aus, sodass
ein Neustart-Sync nichts zum Archivieren hat. Das zweimalige Ausführen des Skripts ist harmlos;
der zweite Durchlauf meldet jede Nachricht als bereits vorhanden.

Die Fixtures sind unter `tests/seed/fixtures` eingecheckt, ein mbox pro Quellordner, wobei
`manifest.tsv` jede Datei ihrem Zielordner zuordnet. Sie werden über das
eigene `--import-mbox` CLI der Anwendung importiert, und dieses Detail ist beabsichtigt: Das CLI leitet über
`MailImporter.ImportEmailToDatabase` weiter, denselben Importer, den die IMAP-Synchronisierung verwendet, sodass die Zeilen
genau in dem Format landen, in dem ein echtes Archiv sie hält -- Adressen verbunden mit `", "`, Betreffzeilen übermittelt durch
`CleanText`, `SentDate` in die Anzeigenzeitzone konvertiert, `RawHeaders` extrahiert und gekürzt.
Das direkte Einfügen von Zeilen in die Datenbank würde all das überspringen und Fixtures erzeugen, die
stillschweigend von den Produktionsdaten abweichen.

Jede Fixture-Nachricht trägt einen `X-Fixture-Purpose` Header, der erklärt, warum sie existiert, sodass die Dateien
sich selbst dokumentieren:

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```

Daten sind feststehend statt relativ zum aktuellen Tag. Ein Migrations-Stichtag wird vor dem Ausführen
eines Jobs in ein absolutes Datum aufgelöst, sodass ein fester Fixture-Satz und ein fester Stichtag dasselbe
vorhersehbarer testen als ein gleitender. Die Nachrichten erstrecken sich von 2012 bis 2026.

Das Ordnerlayout spiegelt ein Exchange-Postfach wider, und jeder Ordner existiert, um etwas zu testen:

| Quellordner | Warum er existiert |
|---|---|
| `INBOX` | Daten über fünfzehn Jahre verteilt, plus die unten genannten Randfall-Nachrichten |
| `Sent Items` | kollidiert mit dem vorhandenen Special-Use `Sent` des Dovecot-Ziels |
| `Sent Items/2019` | eine Umbenennung muss nur das führende Pfadsegment umschreiben |
| `Sent Items Archive` | teilt ein Präfix als Teilstring, aber nicht an einer Segmentgrenze, daher darf es nicht umgeschrieben werden |
| `Deleted Items` | Ordner-Ausschluss |
| `Deleted Items/Old` | ein Ausschluss auf einem übergeordneten Ordner muss auch die Unterordner abdecken |
| `Junk E-Mail` | Exchange-Name für einen Ordner, den Dovecot `Junk` nennt |
| `Archive/Projects/2020` | drei Ebenen Verschachtelung |

Die einzelnen Nachrichten decken eine Nachricht ohne `Message-ID` Header ab (der den deterministischen
Fallback erhält), eine, deren `Message-ID` keine `@` hat (die bei einer Wiederherstellung nicht intakt bleibt), einen Betreff mit einem Steuerzeichen, eine Nachricht mit einer dreistufigen `Received` Kette und eine ohne,
mehrere mit zwei `To` Empfängern und zwei fast identische Paare im Abstand von einer Sekunde.

### Ein Hinweis zum Exit-Code {#a-note-on-the-exit-code}

`tests/seed/seed.sh` prüft die Anzahl der fehlgeschlagenen und fehlerhaften Einträge aus der Import-Ausgabe statt
des Prozess-Exit-Codes. `MBoxImportService` meldet `CompletedWithErrors`, wenn Duplikate übersprungen wurden, und das CLI bildet jeden Status außer `Completed` auf exit 1 ab, sodass das erneute Ausführen eines
Imports mit einem Exit-Code ungleich Null endet, selbst wenn nichts schiefgelaufen ist. Das erneute Ausführen des Seed-Skripts ist daher sicher und meldet jede Nachricht als bereits vorhanden.

## ⚠️ Nicht für die Produktion {#not-for-production}

- Jeder Benutzername authentifiziert sich gegenüber Dovecot mit einem einzigen festen Passwort.
- Die Klartext-Authentifizierung ist aktiviert.
- Die Datenbank-Anmeldedaten entsprechen den dokumentierten Standardwerten.
- Der E-Mail- und Datenbankzustand befindet sich in Docker-Volumes, die `down -v` löscht.

## 🔧 Fehlerbehebung {#troubleshooting}

**Tests schlagen mit `Name or service not known` fehl** — der Stack läuft nicht, oder
`appsettings.Test.json` fehlt, sodass die Verbindungszeichenfolge auf den Container-Standard
`Host=postgres` zurückfällt.

**`NETSDK1226` beim Build** — unvollständige Workload-Daten im lokalen .NET SDK, die nicht mit diesem
Repository zusammenhängen. Führen Sie entweder `dotnet workload repair` aus oder bauen Sie mit
`-p:AllowMissingPrunePackageData=true`.

**Dovecot startet in einer Schleife neu** — ein Konfigurationsfehler. `docker compose -f
docker-compose.test.yml logs dovecot` meldet die fehlerhafte Zeile. Beachten Sie, dass Dovecot
mehrere Einstellungen in einer Zeile innerhalb eines Blocks nicht akzeptiert.

**Die Anwendung gibt bei jeder Anfrage HTTP 500 zurück** — normalerweise der Data Protection Key Ring.
Prüfen Sie, ob `DataProtection:KeyPath` auf ein beschreibbares Verzeichnis zeigt.
