# 🖥️ CLI-Befehle {#cli-commands}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Einige Aufgaben gehören nicht in die Web-UI: das Importieren eines vorhandenen Archivs vom lokalen Dateisystem oder das Verschieben gespeicherter Originalnachrichten zwischen Speicher-Backends. Mail Archiver akzeptiert beides als Befehle auf der Befehlszeile.

| Befehl | Zweck |
|----------|---------|
| `--import-mbox` | Importiert eine MBox-Datei aus einem gemounteten Verzeichnis |
| `--import-eml` | Importiert ein ZIP-Archiv von EML-Dateien aus einem gemounteten Verzeichnis |
| `--migrate-raw-storage` | Verschiebt gespeicherte Originalnachrichten zwischen der Datenbank und dem Dateisystem |

Alle Befehle werden verarbeitet, bevor die Web-Pipeline startet, und beenden sich, wenn sie fertig sind. Daher können sie sicher mit `docker compose exec` ausgeführt werden, während das Archiv läuft.

## ⚠️ Sicherheitsmodell {#security-model}

Die Import-Befehle sind **nur über das CLI** verfügbar – es gibt keinen Web-Endpunkt zum Durchsuchen oder Importieren lokaler Dateien. Sie müssen Zugriff auf den Docker-Host (`docker exec`) haben, um sie auszuführen.

Eine **Pfad-Whitelist** (`LocalImport:AllowedPaths`) steuert, aus welchen Verzeichnissen der Importer lesen kann. Dateien außerhalb dieser Pfade werden abgelehnt.

`--migrate-raw-storage` unterliegt dieser Whitelist nicht: Es liest nur die Nachrichten-Dateien, die das Archiv selbst unter `RawMessages:FileSystem:BasePath` geschrieben hat.

## 🛠️ Setup {#setup}

### 1. Erlaubte Pfade konfigurieren {#1-configure-allowed-paths}

Legen Sie in Ihrem `docker-compose.yml` die erlaubten Importpfade fest:

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. Dateien mounten {#2-mount-your-files}

Mounten Sie das Verzeichnis, das Ihre MBox/EML-Dateien enthält, in den Container:

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

Ersetzen Sie `/home/user/mbox-archives` durch den tatsächlichen Pfad auf Ihrem Docker-Host.

### 3. Container neu starten {#3-restart-the-container}

```bash
docker compose down && docker compose up -d
```

## 📝 Verwendung {#usage}

### Finden Sie Ihre Ziel-Konto-ID {#find-your-target-account-id}

Bevor Sie den Import starten, benötigen Sie die Datenbank-ID des Ziel-Mail-Kontos. Sie können diese über die Web-UI finden:

Navigieren Sie zu „Email Accounts“, öffnen Sie die Details des Zielkontos; die Konto-ID wird in der Liste der Konten oder in der URL angezeigt, wenn ein Konto betrachtet wird.

### Eine MBox-Datei importieren {#import-an-mbox-file}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**Parameter:**
| Parameter | Erforderlich | Beschreibung |
|-----------|----------|-------------|
| `--import-mbox` | Ja | Signalisiert eine MBox-Importoperation |
| `--file <path>` | Ja | Pfad zur MBox-Datei innerhalb des Containers |
| `--account-id <id>` | Ja | Datenbank-ID des Ziel-Mail-Kontos |
| `--folder <name>` | Nein | Name des Zielordners (Standard: `INBOX`) |

### EML-Dateien importieren (ZIP-Archiv) {#import-eml-files-zip-archive}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**Parameter:**
| Parameter | Erforderlich | Beschreibung |
|-----------|----------|-------------|
| `--import-eml` | Ja | Signalisiert eine EML-Importoperation |
| `--file <path>` | Ja | Pfad zu einem **ZIP-Archiv**, das `.eml` Dateien innerhalb des Containers enthält |
| `--account-id <id>` | Ja | Datenbank-ID des Ziel-Mail-Kontos |

> **Wichtig:** `--import-eml` erwartet ein **ZIP-Archiv**, das `.eml` Dateien enthält — es akzeptiert **keine** einzelnen `.eml` Dateien. Um einzelne EML-Dateien zu importieren, packen Sie diese zuerst in ein ZIP-Archiv ein:
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **Hinweis für `dotnet run`:** Wenn Sie `dotnet run` verwenden (anstelle der veröffentlichten DLL), trennen Sie die Argumente von dotnet von den Argumenten der Anwendung mit `--`:
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> `--` teilt `dotnet run` mit, dass alle folgenden Argumente zu Ihrer Anwendung gehören und nicht zum .NET-Compiler.

## 📊 Beispielhafte Ausgabe {#example-output}

```
Target account: user@example.com (ID: 1)

=== Local MBox Import ===
File: /data/import/myarchive.mbox
Size: 245.32 MB
Target Account ID: 1
Target Folder: INBOX

[Importing... progress logged to container logs]

=== Import Results ===
Status: Completed
Total Emails: 15234
Imported Successfully: 15120
Failed: 42
Skipped (malformed): 28
Skipped (duplicates): 44
Duration: 00:12:45
```

## 🔍 Überwachung {#monitoring}

Während des Imports werden detaillierte Fortschrittsinformationen in die Container-Logs geschrieben:

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ Konfigurationsreferenz {#configuration-reference}

| Umgebungsvariable | Standard | Beschreibung |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | Erster erlaubter Pfad für lokale Importe |
| `LocalImport__AllowedPaths__1` | (leer) | Zweiter erlaubter Pfad (optional) |

Fügen Sie weitere Pfade hinzu, indem Sie den Index erhöhen (`__2`, `__3`, etc.).

## 🚫 Fehlerbehandlung {#error-handling}

| Fehler | Ursache | Lösung |
|-------|-------|----------|
| `File not found` | Der Pfad `--file` existiert nicht im Container | Überprüfen Sie den Dateipfad und den Volume-Mount |
| `File path is not in an allowed import directory` | Die Datei liegt außerhalb der Whitelist-Pfade | Verschieben Sie die Datei in einen zulässigen Pfad oder fügen Sie das Verzeichnis zu `LocalImport:AllowedPaths` hinzu |
| `Mail account with ID X not found` | Falsche Konto-ID | Überprüfen Sie die Konto-ID über das Web-UI oder eine Datenbankabfrage |
| `Invalid account-id` | Nicht-numerischer Wert für `--account-id` | Verwenden Sie eine numerische Datenbank-ID |

## 🔒 Best Practices für die Sicherheit {#security-best-practices}

1. **Verwenden Sie nach Möglichkeit Read-Only-Mounts**:
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **Beschränken Sie AllowedPaths** auf die Verzeichnisse, die Sie tatsächlich verwenden.

3. **Entfernen Sie Volume-Mounts**, nachdem der Import abgeschlossen ist, um eine saubere Sicherheitsfläche aufrechtzuerhalten.

4. **Exponieren Sie das Import-Verzeichnis niemals** über den Webserver – es gibt keinen Web-Endpunkt für lokale Importe.

## 📦 Importformate auf einen Blick {#import-formats-at-a-glance}

| CLI Flag | Eingabeformat | Typische Dateiendung | Einzelne E-Mail | Massenimport |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | MBox-Datei | `.mbox`, `.mbx` | ❌ (immer Massenimport) | ✅ |
| `--import-eml` | ZIP-Archiv von EMLs | `.zip` | ❌ (ZIP erforderlich) | ✅ |
| `--migrate-raw-storage` | Gespeicherte Originalnachrichten | — | — | ✅ |

*Einzelne `.eml`-Dateien werden nicht direkt unterstützt. Verwenden Sie `zip`, um sie zu kapseln, oder importieren Sie diese über den [Web-Upload](Import-EML-MBox.md).*

## 🔧 Wartungsbefehle {#maintenance-commands}

### Verschieben gespeicherter Originalnachrichten {#moving-stored-original-messages}

Wenn `RawMessages:Enabled` aktiviert ist, wird die Originalnachricht jeder E-Mail entweder in der Datenbank oder in Dateien gespeichert, abhängig von `RawMessages:Storage`. Dieser Befehl verschiebt die bereits gespeicherten Originale von einem Backend in ein anderes; siehe [Original Message Storage](RawMessages.md#storage-backend) für die Konfiguration.

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

| Argument | Standard | Beschreibung |
|----------|---------|-------------|
| `--from` | konfigurierter `RawMessages:Storage` | Backend, aus dem die Nachrichten verschoben werden. |
| `--to` | konfigurierter `RawMessages:Storage` | Backend, in das die Nachrichten verschoben werden. |
| `--batch` | `200` | Nachrichten pro Datenbanktransaktion. |

Im Gegensatz zu den Import-Befehlen ist dieser nicht durch `LocalImport:AllowedPaths` eingeschränkt: Er liest nur die Nachrichten-Dateien, die das Archiv selbst unter `RawMessages:FileSystem:BasePath` geschrieben hat.

**Was er garantiert:**

- Jede Nachricht wird vor dem Kopieren gegen die zum Zeitpunkt der Archivierung aufgezeichnete Prüfsumme verifiziert. Eine Nachricht, die fehlschlägt, bleibt genau dort, wo sie ist, und wird gemeldet — ein beschädigtes Original wird niemals weitergegeben und seine einzige verbleibende Kopie wird niemals gelöscht.
- Eine Datei wird erst nach Abschluss der Datenbanktransaktion gelöscht, die sie verschoben hat, sodass ein unterbrochener Durchlauf nicht die einzige Kopie verlieren kann.
- Der Durchlauf ist fortsetzbar: Die Arbeit wird anhand des bei jeder Nachricht aufgezeichneten Speichers ausgewählt, sodass ein erneuter Start des Befehls mit den noch ausstehenden Aufgaben fortfährt.
- Eine fehlerhafte Nachricht bricht den Durchlauf nicht ab. Der Exit-Code ist `1`, falls eine Nachricht fehlgeschlagen ist, andernfalls `0`.

**Beispielhafte Ausgabe:**

```text
Batch: 200 moved, 0 skipped, 0 failed
Batch: 200 moved, 0 skipped, 0 failed
Batch: 42 moved, 0 skipped, 1 failed
Done: 442 moved, 1 failed.
  Email 12345: checksum mismatch (recorded 4f1c..., file has 9ab2...)
```

Lesezugriffe folgen immer dem bei jeder Nachricht aufgezeichneten Backend, nicht der konfigurierten Einstellung, sodass ein Download während und nach einer Migration weiterhin funktioniert. Das Ändern von `RawMessages:Storage` wirkt sich nur auf neu archivierte E-Mails aus — führen Sie diesen Befehl aus, um das bereits Gespeicherte zu verschieben.

> **Sicherung:** Beim Filesystem-Backend liegen die Nachrichten-Dateien außerhalb der Datenbank. Sichern Sie `RawMessages:FileSystem:BasePath` zusammen mit dem Datenbank-Dump; eine Wiederherstellung ohne die Dateien hinterlässt Originale, die nicht bereitgestellt werden können.

## ❓ FAQ {#faq}

**F: Warum benötigt `--import-eml` eine ZIP-Datei?**
A: Der zugrunde liegende EML-Importdienst (`EmlImportService`) verarbeitet ZIP-Archive intern über die `ZipFile`-API von .NET. Dieses Design unterstützt Massenimporte effizient. Für einzelne `.eml`-Dateien packen Sie diese bitte in ein ZIP-Archiv (z. B. `zip emails.zip *.eml`) oder nutzen Sie den [Web-Upload](Import-EML-MBox.md).

**F: Warum kann ich Dateien nicht über die Web-UI durchsuchen?**
A: Dies ist ein bewusst gewähltes Sicherheitsdesign. Diese Funktion wurde absichtlich über das CLI für Interaktionen auf Systemebene implementiert, da normale Benutzer keinen direkten Zugriff auf die Betriebssystemebene haben; sie interagieren nur mit der Web-UI.

Um Sicherheitsrisiken wie Path-Traversal-Angriffe und unbefugte Offenlegung des Dateisystems zu verhindern, sind reguläre Benutzer darauf beschränkt, die Web-UI für Datei-Uploads und Importe zu verwenden. Dies stellt sicher, dass sie die vorgesehene Schnittstelle nicht umgehen können, um auf das zugrunde liegende Container-Dateisystem zuzugreifen.
