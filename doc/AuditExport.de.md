# 📤 Audit Data Export {#audit-data-export}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Der Audit Data Export erstellt tabellarische Massendatenpakete (ZIP mit `INDEX.XML` + CSV-Tabellen + Index-DTD) aus dem bestehenden Archiv. Diese Pakete folgen der etablierten Indexstruktur für die Übergabe von Datenträgern und können direkt von gängigen externen Audit- und Analyse-Tools importiert werden, die dieses Indexformat unterstützen.

Die Funktion ist als **dedizierte Seite erreichbar von der Logs-Seite** verfügbar und ist auf Admin-Benutzer beschränkt. Jeder Exportlauf wird im Zugriffsprotokoll erfasst, sodass der Exportverlauf revisionssicher ist.

## 🧭 Zugriff {#access}

1. Melden Sie sich als Admin-Benutzer an
2. Öffnen Sie die **Logs**-Seite
3. Klicken Sie auf die Schaltfläche **Audit Data Export** oben rechts

Nicht-Admin-Benutzer sehen die Schaltfläche nicht und können keine der Export-Endpunkte aufrufen (der Server lehnt diese ab).

## 📦 Struktur des Exportpakets {#export-package-structure}

Die generierte ZIP-Datei heißt `audit-export-<timestamp>.zip` und enthält:

| Datei | Inhalt |
|---|---|
| `INDEX.XML` | Indexdatei, die den Datenlieferanten, das Medium und alle Tabellen-/Spaltendefinitionen beschreibt |
| `index.dtd` | Das DTD, auf das sich die Indexdatei bezieht; erforderlich für Validierungen durch Import-Tools |
| `emails.csv` | Eine Zeile pro archivierter E-Mail im ausgewählten Zeitraum/Postfach |
| `attachments.csv` | Optional; eine Zeile pro Anhang der exportierten E-Mails |

### Tabelle `emails.csv` {#table-emailscsv}

Spalten (keine Kopfzeile; die Spaltennamen stammen wie üblich für dieses Format aus `INDEX.XML`):

| Spalte | Beschreibung |
|---|---|
| Id | Interne E-Mail-ID (numerisch) |
| MessageId | E-Mail Message-ID Header |
| SentDate | Sendedatum, ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`) |
| ReceivedDate | Empfangsdatum, ISO 8601 UTC |
| IsOutgoing | `1` = ausgehend, `0` = eingehend |
| From | Absender |
| To | Empfänger |
| Cc | CC-Empfänger |
| Bcc | BCC-Empfänger |
| Subject | Betreff |
| FolderName | Ordner, in dem die E-Mail archiviert ist |
| HasAttachments | `1` = mit Anhängen, `0` = ohne |
| AttachmentCount | Anzahl der Anhänge (numerisch) |
| AccountEmail | E-Mail-Adresse des Archivierungs-Postfachs |

### Tabelle `attachments.csv` {#table-attachmentscsv}

| Spalte | Beschreibung |
|---|---|
| EmailId | Interne ID der E-Mail, zu der der Anhang gehört (Join-Key zu `emails.csv`) |
| FileName | Dateiname des Anhangs |
| ContentType | MIME-Typ |
| Size | Größe in Bytes (numerisch) |
| Sha256 | SHA-256 Hash des Inhalts (aus dem content-addressed Attachment-Speicher) |

### CSV-Konventionen {#csv-conventions}

- Kodierung: UTF-8 **ohne** BOM (deklariert als `UTF8` in `INDEX.XML`)
- Spaltentrennzeichen: `;`
- Text-Einschließung: `"` (verdoppelt zum Escaping)
- Datensatz-Trennzeichen: `\n` (explizit deklariert in `INDEX.XML`)
- Daten: ISO 8601, Zeiten werden von der konfigurierten Anzeigenzeitzone in UTC konvertiert
- Keine Kopfzeile; keine lokalisierten Zahlen (numerische Spalten enthalten einfache Ganzzahlen)

## 🖥️ Verwendung der Export-Seite {#using-the-export-page}

### Formularfelder {#form-fields}

1. **Von / Bis Datum** - Zeitraum für den Export (erforderlich, maximaler Zeitraum konfigurierbar, Standard 10 Jahre)
2. **Postfach** - Export auf ein einzelnes Postfach beschränken oder alle Postfächer exportieren
3. **Anhang-Metadaten** - Wenn aktiviert, wird `attachments.csv` dem Paket hinzugefügt
4. **Name des Datenlieferanten / Standort / Kommentar** - Wird in den `DataSupplier`-Block von `INDEX.XML` geschrieben; vorbefüllt aus dem `AuditExport`-Konfigurationsabschnitt und pro Export editierbar

### Lebenszyklus eines Auftrags {#job-lifecycle}

1. Durch Klicken auf **Start Export** wird der Zeitraum und das Postfach validiert, ein Auftrag erstellt und zur Seite zurückgekehrt
2. Der Auftrag läuft im Hintergrund (wartende Exporte werden nacheinander verarbeitet); die Auftragstabelle zeigt den Live-Fortschritt (verarbeitete/Gesamt-E-Mails mit einem Fortschrittsbalken) via automatischem Polling an
3. Wenn der Export abgeschlossen ist, erscheint die Schaltfläche **Download** in der Auftragzeile
4. Laufende oder wartende Aufträge können **abgebrochen** werden

### Exportverlauf {#export-history}

Die Tabelle unter dem Formular listet die neuesten Exporte mit Zeitstempel, Benutzer, Zeitraum, Postfach, E-Mail-Anzahl, Status und Dateigröße auf. Der Verlauf wird **in der Datenbank persistiert** (Tabelle `AuditExportJobs`), sodass er Anwendungsneustarts übersteht und revisionssicher ist.

Download-Dateien werden für ein konfigurierbares Aufbewahrungsfenster aufbewahrt (Standard 30 Tage, siehe `AuditExport__RetentionDays`); danach werden die ZIP-Dateien durch die tägliche Bereinigung gelöscht, während die Verlaufszeilen erhalten bleiben.

> 📝 **Hinweis**: Exportdateien werden innerhalb des Anwendungscontainers gespeichert. Wenn Sie diese langfristig behalten müssen, laden Sie sie herunter und speichern Sie sie in Ihrem Repository für Audit-Nachweise.

## 🔐 Auditierung {#auditing}

Jeder Export erzeugt zwei Einträge im Zugriffsprotokoll des neuen Typs **Audit Data Export**:

1. **Start-Eintrag** - wenn der Export in die Warteschlange gestellt wird (mit Job-ID, Zeitraum, Postfach, Optionen als JSON)
2. **Ergebnis-Eintrag** - wenn der Export abgeschlossen ist, fehlschlägt oder abgebrochen wird (mit Job-ID, Zeitraum, Postfach, E-Mail-Anzahl, Ergebnis und Paketgröße als JSON)

Die Einträge erscheinen auf der Logs-Seite wie alle anderen Einträge im Zugriffsprotokoll und können nach dem neuen Typ gefiltert werden. Das Dropdown-Menü für den Typ-Filter erkennt diese automatisch.

## ⚙️ Konfiguration {#configuration}

Der Abschnitt `AuditExport` in `appsettings.json` (Umgebungsvariablen verwenden die Double-Underscore-Syntax, z. B. `AuditExport__RetentionDays`):

| Einstellung | Beschreibung | Standard |
|---|---|---|
| `DataSupplierName` | Standardwert für das Formularfeld "Name des Datenlieferanten" | leer |
| `DataSupplierLocation` | Standardwert für das Formularfeld "Standort des Datenlieferanten" | leer |
| `Comment` | Standardwert für das Formularfeld "Kommentar" | leer |
| `OutputDirectory` | Verzeichnis für die generierten ZIP-Dateien (relative Pfade beziehen sich auf den App-Content-Root) | `exports/audit` |
| `RetentionDays` | Tage, bis abgeschlossene Exportdateien gelöscht werden | `30` |
| `MaxRangeYears` | Maximal zulässige Spanne zwischen Start- und Enddatum des Zeitraums | `10` |

## 🗄️ Datenbank {#database}

Der Exportverlauf wird in der Tabelle `mail_archiver."AuditExportJobs"` gespeichert (erstellt durch Migration `MigrateV2609_1`, automatisch beim Start angewendet). Bestehende Tabellen werden durch diese Funktion nicht verändert.

## 🔍 Fehlerbehebung {#troubleshooting}

| Problem | Ursache / Lösung |
|---|---|
| Download-Schaltfläche nach Neustart fehlt | Exportdateien liegen im Container; wenn der Container neu erstellt wurde oder das Aufbewahrungsfenster abgelaufen ist, ist die ZIP-Datei weg, während die Verlaufszeile bleibt. Führen Sie den Export erneut aus. |
| "Der Zeitraum darf X Jahre nicht überschreiten" | Verringern Sie den Zeitraum oder erhöhen Sie `AuditExport__MaxRangeYears`. |
| Auftrag bleibt lange auf *Running* | Große Archive werden in Batches von 1000 E-Mails gestreamt; der Fortschritt wird kontinuierlich aktualisiert. Sehr große Zeiträume können eine Weile dauern — prüfen Sie den Zähler verarbeitet/gesamt. |
| Import-Tool meldet Kodierungsprobleme | Das Paket ist UTF-8 ohne BOM mit deklariertem `UTF8`-Flag und explizitem Datensatz-Trennzeichen; stellen Sie sicher, dass das Tool die `INDEX.XML` neben den Datendateien liest (entpacken Sie das gesamte ZIP, öffnen Sie Dateien nicht einzeln aus dem ZIP heraus). |

## 🔗 Verwandte Dokumentation {#related-documentation}

- [Access Logging](Logs.md)
- [Installation, Setup und Parameter](Setup.md)
