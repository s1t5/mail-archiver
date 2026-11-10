# 📤 Exportieren eines Mail-Kontos (EML / MBox) {#exporting-a-mail-account-eml-mbox}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Der Kontoexport schreibt **jede Nachricht eines Kontos** zurück in eine
herunterladbare ZIP-Datei. Er ist das Gegenstück zum
[Web-Import](Import-EML-MBox.md): Sie erhalten ein portables Archiv des Kontos in einem
Format, das jeder Mail-Client oder jedes Migrationswerkzeug lesen kann.

Es werden zwei Formate angeboten:

| Format | Was Sie erhalten | Typische Verwendung |
|--------|--------------|-------------|
| **EML** | Einzelne `.eml`-Dateien, organisiert in Ordnern | Re-Import in einen Mail-Client, Verarbeitung pro Nachricht, langfristige Lesbarkeit |
| **MBox** | Traditionelle Postfach-Dateien — **eine Datei pro Ordner** | Tools und Clients, die MBox lesen, Massenverarbeitung von Text |

In beiden Fällen werden die Nachrichten als rohe RFC-822-Nachrichten geschrieben, sodass **Anhänge
im Export enthalten sind**.

> 💡 **Tipp**: Um nur eine Auswahl von Nachrichten statt eines gesamten Kontos zu exportieren,
> verwenden Sie die Massenaktionen in der [Mail-Suche](Search.md#-acting-on-many-results-at-once).

## 🧭 Zugriff {#access}

1. Öffnen Sie **Mail Accounts** im Hauptmenü.
2. Klicken Sie auf die Schaltfläche **⋮** (weitere Aktionen) in der Zeile des Kontos und wählen Sie
   **Export**. Derselbe Eintrag befindet sich auf der **Details**-Seite des Kontos.

Der Export erfordert die Rolle **admin** oder **self manager** — Standardbenutzer sehen
den Eintrag, können ihn jedoch nicht verwenden. Der Export umfasst immer das gesamte Konto; es gibt
keinen Datums- oder Ordnerfilter.

## 🖥️ Verwendung der Export-Seite {#using-the-export-page}

Die Seite zeigt die **Gesamtanzahl**, die **eingehenden** und die **ausgehenden** Nachrichten des Kontos an, sodass Sie die Größe des Auftrags sehen können, bevor Sie ihn starten. Gehen Sie dann wie folgt vor:

1. Wählen Sie das Format — **EML** oder **MBox**.
2. Drücken Sie auf **Start Export**.

Der Export läuft **im Hintergrund**. Sie werden auf eine Statusseite weitergeleitet, die die Job-ID, das gewählte Format, die Zeitstempel und die Anzahl der verarbeiteten/insgesamt vorhandenen Nachrichten anzeigt.

### Job-Status {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

Wenn der Auftrag den Status **Completed** erreicht, erscheint ein **Download-Button** auf der Statusseite. Derselbe Auftrag wird auch auf der [Jobs](BackgroundJobs.md)-Seite aufgeführt.

> ⚠️ **Exportdateien werden automatisch bereinigt.** Das ZIP wird **nach einem erfolgreichen Download** und in jedem Fall **nach 7 Tagen** gelöscht. Laden Sie die Datei herunter, solange sie noch angeboten wird — das Archiv selbst ist davon unberührt, Sie können später einfach einen neuen Export starten.

## ⚠️ Was Sie beachten sollten {#things-to-watch-out-for}

- **Große Konten benötigen Zeit.** Ein großes Postfach benötigt möglicherweise mehrere Minuten und die ZIP-Datei kann je nach Nachrichtenvolumen und Anhängen sehr groß werden. Da der Job im Hintergrund läuft, können Sie den Browser schließen und später zur Statusseite zurückkehren.
- **Server-Festplattenspeicher.** Die ZIP-Datei wird auf dem Server zusammengestellt, bevor Sie sie herunterladen, stellen Sie also sicher, dass neben dem Archiv genügend freier Speicherplatz vorhanden ist.
- **"Export file not found"** — der Job ist noch nicht abgeschlossen oder die Datei wurde bereits heruntergeladen oder durch die 7-Tage-Bereinigung entfernt. Starten Sie einen neuen Export.
- **Keine Filteroptionen** — der Export erfolgt pro Konto entweder vollständig oder gar nicht. Verwenden Sie den Export der Suchseite für eine Teilmenge.
- **Kein Compliance-Export.** Für eine revisionssichere Datenmedien-Übergabe mit Indexdatei verwenden Sie stattdessen den [Audit Data Export](AuditExport.md) — dieser erstellt ein Paket im tabellarischen Massendatenformat, das Audit-Tools erwarten.

## 🔗 Verwandte Dokumentation {#related-documentation}

- [Importing Mail in the Web UI](Import-EML-MBox.md) — die entgegengesetzte Richtung
- [Mail search](Search.md) — Exportieren einer Auswahl von Nachrichten
- [Background Jobs](BackgroundJobs.md) — Jobliste, Fortschritt und Abbrechen
- [Audit Data Export](AuditExport.md) — revisionssichere Exportpakete
- [CLI commands](CLI.md) — Importieren aus einem gemounteten Verzeichnis
