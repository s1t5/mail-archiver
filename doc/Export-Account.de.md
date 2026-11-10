# 📤 Exportieren eines Mail-Kontos (EML / MBox) {#exporting-a-mail-account-eml-mbox}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Der Kontoexport schreibt **jede Nachricht eines Kontos** in eine herunterladbare ZIP-Datei zurück. Er ist das Gegenstück zum [Web-Import](Import-EML-MBox.md): Sie erhalten ein portables Archiv des Kontos in einem Format, das jeder Mail-Client oder jedes Migrationswerkzeug lesen kann.

Es werden zwei Formate angeboten:

| Format | Was Sie erhalten | Typische Verwendung |
|--------|--------------|-------------|
| **EML** | Einzelne `.eml`-Dateien, organisiert in Ordnern | Re-Import in einen Mail-Client, Verarbeitung einzelner Nachrichten, langfristige Lesbarkeit |
| **MBox** | Traditionelle Postfach-Dateien — **eine Datei pro Ordner** | Tools und Clients, die MBox lesen, Massenverarbeitung von Texten |

In beiden Fällen werden die Nachrichten als reine RFC-822-Nachrichten geschrieben, sodass **Anhänge im Export enthalten** sind.

> 💡 **Tipp**: Um nur eine Auswahl an Nachrichten statt eines ganzen Kontos zu exportieren, verwenden Sie die Massenaktionen in der [Mail-Suche](Search.md#-acting-on-many-results-at-once).

## 🧭 Zugriff {#access}

1. Öffnen Sie **Mail-Konten** im Hauptmenü.
2. Klicken Sie auf die Schaltfläche **⋮** (weitere Aktionen) in der Kontenzeile und wählen Sie **Exportieren**. Derselbe Eintrag befindet sich auf der **Details**-Seite des Kontos.

Der Export erfordert die Rolle **admin** oder **self manager** — Standardbenutzer sehen den Eintrag, können ihn aber nicht verwenden. Der Export umfasst immer das gesamte Konto; es gibt keinen Datums- oder Ordnerfilter.

## 🖥️ Verwendung der Export-Seite {#using-the-export-page}

Die Seite zeigt die **Gesamtanzahl**, die **eingehenden** und die **ausgehenden** Nachrichten des Kontos an, sodass Sie die Größe des Vorgangs sehen können, bevor Sie ihn starten. Dann:

1. Wählen Sie das Format — **EML** oder **MBox**.
2. Drücken Sie auf **Export starten**.

Der Export läuft **im Hintergrund**. Sie werden auf eine Statusseite weitergeleitet, die die Job-ID, das gewählte Format, die Zeitstempel und die Anzahl der verarbeiteten/insgesamt vorhandenen Nachrichten anzeigt.

### Job-Status {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

Wenn der Job den Status **Abgeschlossen** erreicht, erscheint ein **Download-Button** auf der Statusseite. Derselbe Job wird auch auf der [Jobs](BackgroundJobs.md)-Seite aufgeführt.

> ⚠️ **Exportdateien werden automatisch bereinigt.** Die ZIP-Datei wird **nach einem erfolgreichen Download** und in jedem Fall **nach 7 Tagen** gelöscht. Laden Sie die Datei herunter, solange sie noch angeboten wird — das Archiv selbst ist davon unberührt, Sie können später einfach einen neuen Export starten.

## ⚠️ Was Sie beachten sollten {#things-to-watch-out-for}

- **Große Konten benötigen Zeit.** Ein großes Postfach kann mehrere Minuten in Anspruch nehmen, und die ZIP-Datei kann je nach Nachrichtenvolumen und Anhängen sehr groß werden. Da der Job im Hintergrund läuft, können Sie den Browser schließen und später zur Statusseite zurückkehren.
- **Server-Festplattenspeicher.** Die ZIP-Datei wird auf dem Server zusammengestellt, bevor Sie sie herunterladen, stellen Sie also sicher, dass neben dem Archiv genügend freier Speicherplatz vorhanden ist.
- **"Exportdatei nicht gefunden"** — der Job ist noch nicht abgeschlossen oder die Datei wurde bereits heruntergeladen oder durch die 7-Tage-Bereinigung entfernt. Starten Sie einen neuen Export.
- **Keine Filteroptionen** — der Export erfolgt pro Konto nach dem Prinzip "Alles oder Nichts". Verwenden Sie den Export der Suchseite für eine Teilmenge.
- **Kein Compliance-Export.** Für eine revisionssichere Datenmedien-Übergabe mit einer Indexdatei verwenden Sie stattdessen den [Audit Data Export](AuditExport.md) — dieser erstellt ein Paket im tabellarischen Massendatenformat, das von Audit-Tools erwartet wird.

## 🔗 Verwandte Dokumentation {#related-documentation}

- [Importieren von Mails in der Web-UI](Import-EML-MBox.md) — die umgekehrte Richtung
- [Mail-Suche](Search.md) — Exportieren einer Auswahl von Nachrichten
- [Hintergrund-Jobs](BackgroundJobs.md) — Jobliste, Fortschritt und Abbrechen
- [Audit Data Export](AuditExport.md) — revisionssichere Exportpakete
- [CLI-Befehle](CLI.md) — Importieren aus einem gemounteten Verzeichnis
