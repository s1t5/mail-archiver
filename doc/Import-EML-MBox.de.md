# 📥 Importieren von E-Mails in der Web-UI (EML / MBox) {#importing-mail-in-the-web-ui-eml-mbox}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Neben der [Befehlszeilenschnittstelle](CLI.md) und dem
[CSV-Kontoimport](Account-Import.md) kann Mail Archiver ein bestehendes
Mail-Archiv **über die Weboberfläche** importieren. Es gibt zwei Upload-Formulare:

| Formular | Akzeptierte Datei | Ergebnis |
|----------|-------------------|----------|
| **MBox importieren** | `.mbox`, `.mbx` | Jede Nachricht in der Datei wird in **einen** von Ihnen gewählten Ordner importiert |
| **EML importieren** | `.zip` mit `.eml` Dateien | Die **Ordnerstruktur innerhalb der ZIP-Datei** bestimmt, wo jede Nachricht landet |

Beide Formulare archivieren die Nachrichten in ein bestehendes Mail Archiver Konto, beide laufen
als **Hintergrundaufträge** und beide **überspringen Duplikate automatisch** — das zweimalige Importieren
derselben Datei erstellt keine zweite Kopie.

> 💡 **Tipp**: Wenn Ihr Archiv bereits als Datei auf dem Server liegt, bevorzugen Sie die
> [CLI](CLI.md). Sie vermeidet den Browser-Upload und ist die
> bessere Wahl für Multi-Gigabyte-Archive.

## 🧭 Zugriff {#access}

1. Öffnen Sie **Mail Accounts** im Hauptmenü.
2. Verwenden Sie das Dropdown-Menü **Import** oberhalb der Kontoliste und wählen Sie
   **Import MBox** oder **Import EML**.

Die Formulare stehen **Admins** (alle Konten) und **Self Managern**
(den ihnen zugewiesenen Konten) zur Verfügung; siehe
[User Management](UserManagement.md#-user-account-permissions). Nur **aktivierte**
Konten werden als Importziel angeboten — ein deaktiviertes Konto kann nicht ausgewählt werden.

## 📨 MBox importieren {#import-mbox}

MBox ist das Format, das von den meisten Mail-Clients für exportierte Ordner und für Sicherungen im `mbox`-Stil verwendet wird.

| Feld | Bedeutung |
|-------|---------|
| **MBox-Datei** | Die hochzuladende `.mbox` / `.mbx`-Datei |
| **Ziel-Konto** | Das Mail Archiver Konto, das die Nachrichten empfängt |
| **Ziel-Ordner** | Der Ordner, in den die Nachrichten importiert werden. Wählen Sie einen aus der bestehenden Ordnerliste des Kontos aus oder geben Sie einen neuen Namen ein — ein Ordner, der noch nicht existiert, wird während des Imports **automatisch erstellt**. Standardmäßig `INBOX`. |

Alle Nachrichten in der Datei werden in diesen einzelnen Zielordner importiert. Die MBox-Datei selbst enthält keine Ordnerinformationen, daher wird die Struktur des ursprünglichen Postfachs nicht rekonstruiert — wenn Sie dies benötigen, verwenden Sie den EML/ZIP-Weg.

## 🗂️ EML importieren {#import-eml}

Der EML-Import erwartet ein **ZIP-Archiv**, dessen Einträge `.eml`-Dateien sind.

- `.eml`-Dateien im **Stammverzeichnis** des ZIP werden in `INBOX` importiert.
- `.eml`-Dateien in einem **Unterordner** werden in einen Ordner mit dem **gleichen
  Namen** wie dieser Unterordner importiert.

Ein ZIP mit folgendem Aufbau:

```
mailbox.zip
├── 2024-invoice.eml            → INBOX
├── Archive/
│   └── old-message.eml         → Archive
└── Projects/
    └── 2026/
        └── kickoff.eml         → Projects/2026
```

stellt die Ordnerstruktur zusammen mit den Nachrichten wieder her. Einzelne `.eml`-Dateien können
hochgeladen werden, indem sie zuerst in ein ZIP gepackt werden (`zip emails.zip *.eml`).

## ⚙️ Gemeinsames Verhalten {#shared-behaviour}

Beide Formulare verhalten sich gleich, sobald Sie auf **Start Import** klicken:

- **Dateigrößenlimit** — konfiguriert mit `Upload__MaxFileSizeGB` (Standard **10 GB**).
  Das Formular zeigt das aktuelle Limit neben dem Dateiauswahldialog an.
- **Hintergrundverarbeitung** — der Upload wird als Job in eine Warteschlange gestellt; Sie müssen
  den Browser nicht offen halten.
- **Umgang mit Duplikaten** — Nachrichten, die bereits im Archiv vorhanden sind, werden übersprungen
  und separat gezählt. Duplikate werden anhand des `Message-ID` Headers abgeglichen.
- **Fehlerhafte Eingaben** — unlesbare Dateien werden einzeln übersprungen und auf der
  Statusseite gemeldet, anstatt den gesamten Import abzubrechen.
- **Fortschritt** — Sie werden direkt zur Job-Statusseite weitergeleitet, die die
  Job-ID, den Dateinamen, die Dateigröße, den Status, die verarbeiteten/insgesamt Nachrichten und die
  laufenden Zähler anzeigt. Derselbe Job erscheint auch auf der [Jobs](BackgroundJobs.md)-Seite.
- **Abbrechen** — ein laufender Import kann von seiner Statusseite aus abgebrochen werden. Die
  bis zu diesem Zeitpunkt importierten Nachrichten bleiben im Archiv.
- **Audit-Trail** — der Import wird im [access log](Logs.md) protokolliert, und der
  Speicherwert pro Konto wird anschließend aktualisiert (siehe
  [Per-Account Storage](AccountStorage.md)).

### Job-Status {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled`

Ein abgeschlossener Job meldet, wie viele Nachrichten importiert wurden, wie viele fehlgeschlagen sind und wie
viele als Duplikate übersprungen wurden — zum Beispiel
*"1420 importiert, 3 fehlgeschlagen, 87 Duplikate"*. Bei einem erfolgreichen Import wird die hochgeladene
Datei vom Server gelöscht; fehlgeschlagene Einträge werden für die Lebensdauer des
Jobs aufbewahrt, damit Sie sehen können, was abgelehnt wurde.

## ⚠️ Was Sie beachten sollten {#things-to-watch-out-for}

- **Falscher Dateityp** — der Upload wird abgelehnt, es sei denn, die Dateiendung ist eine von
  `.mbox`, `.eml` oder `.zip`. Der Picker des EML-Formulars akzeptiert nur `.zip`, der des MBox-
  Formulars nur `.mbox` und `.mbx`.
- **"0 importiert, N Duplikate"** — dies ist kein Fehler. Die Nachrichten befinden sich bereits
  im Archiv, höchstwahrscheinlich durch einen früheren Import oder eine Synchronisierung der gleichen Quelle.
- **Sehr große Archive** — eine MBox-Datei wird Nachricht für Nachricht gelesen, daher dauert eine
  mehrgigabytegroße Datei selbst im Hintergrund eine Weile. Überprüfen Sie den Fortschritt auf
  der Statusseite, anstatt den Upload erneut zu starten.
- **Freier Festplattenspeicher** — die hochgeladene Datei wird vorübergehend auf dem Server gespeichert und
  benötigt Platz neben Ihrer Archiv-Datenbank.
- **Ordnernamen mit Sonderzeichen** — Ordnernamen werden aus den ZIP-Pfaden oder Ihrer Eingabe übernommen;
  überprüfen Sie die Ordnerliste des Kontos nach dem Import, falls Sie ungewöhnliche Zeichen verwendet haben.

## 🔗 Verwandte Dokumentation {#related-documentation}

- [CLI commands](CLI.md) — Import aus einem gemounteten Verzeichnis
- [Account Import (CSV)](Account-Import.md) — Hinzufügen vieler Konten gleichzeitig
- [Background Jobs](BackgroundJobs.md) — Überwachen und Abbrechen von Import-Jobs
- [Exporting a Mail Account](Export-Account.md) — die umgekehrte Richtung
- [Setup and Parameters](Setup.md) — `Upload__*` Einstellungen
