# 📧 Original Message Storage {#original-message-storage}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Standardmäßig bewahrt das Archiv die **analysierten Teile** einer E-Mail auf: Header, Text- und HTML-Inhalte, Anhänge. Ein exportiertes `.eml` wird aus diesen Feldern wiederaufgebaut.

Dies reicht aus, um eine E-Mail zu lesen, aber es ist nicht die Nachricht **so wie sie empfangen wurde**. Transfer-Kodierungen, MIME-Grenzen und Header-Folding unterscheiden sich vom Original, sodass eine wiederaufgebaute Nachricht ihre DKIM-Signatur nicht mehr verifiziert und Spam-Scanner sie anders bewerten.

Wenn die Speicherung der Originalnachricht aktiviert ist, bewahrt das Archiv die vollständige Nachricht so auf, wie sie eingegangen ist, zusätzlich zu den analysierten Feldern. Das gespeicherte Original ist das, was das Archiv als zugestellt nachweisen kann.

| | Nur analysierte Felder | Mit gespeichertem Original |
| --- | --- | --- |
| Header, Inhalt, Anhänge | ✅ | ✅ |
| Byte-exakte Nachricht | ❌ | ✅ |
| DKIM-Signatur verifiziert | ❌ | ✅ |
| Forensische Nutzung (Gericht, Audit) | eingeschränkt | ✅ |

## 🎯 Wo das Original bereitgestellt wird {#where-the-original-is-served}

Sobald es gespeichert ist, wird das Original überall zurückgegeben, wo eine vollständige Nachricht erwartet wird:

| Ort | Was passiert |
| --- | --- |
| **Export EML** Schaltfläche auf der E-Mail-Detailseite | Stellt das gespeicherte Original bereit, sofern eines existiert; greift auf die Rekonstruktion zurück, falls E-Mails ohne Original archiviert wurden. |
| **Restore to a mailbox** | Fügt das gespeicherte Original an, sofern eines existiert; greift auf die Rekonstruktion zurück, falls E-Mails ohne Original archiviert wurden. |
| `GET /api/v1/emails/{id}/raw` | REST API Download, siehe den [REST API guide](API.md#download-original-message). |
| `get_email_raw` | MCP tool, siehe den [MCP guide](MCP.md#get_email_raw). |

Die Antwort teilt Ihnen mit, welche Form Sie erhalten haben, sodass ein Aufrufer ein echtes Original von einer Rekonstruktion unterscheiden kann, ohne die Bytes zu inspizieren:

| Signal | Bedeutung |
| --- | --- |
| `X-Raw-Message-Source: imap` | Exakte Kopie vom IMAP-Server (`BODY[]` wie zugestellt). |
| `X-Raw-Message-Source: graph` | Exakte Kopie, der vom Exchange zurückgegebene MIME-Inhalt. |
| `X-Raw-Message-Source: eml-file` | Exakte Kopie, der Inhalt der importierten `.eml` Datei. |
| `X-Raw-Message-Source: serialized` | Parsed Nachricht, die wieder ausgegeben wurde. Identisch, außer dass die Zeilenendungen CRLF sind (MBox Import, IMAP recovery fetch). |
| `X-Raw-Message-Sha256` | SHA-256 des gespeicherten Originals. |
| `X-Raw-Message-Reconstructed: true` | Kein Original gespeichert; die Nachricht wurde neu aufgebaut. Kein Checksum-Header. |
| `X-Raw-Message-Storage` | Backend, aus dem das Original gelesen wurde (`postgres` oder `filesystem`). |

Der Dateiname sagt es ebenfalls: `email-{id}-original-...eml` für ein gespeichertes Original,
`email-{id}-...eml` für eine Rekonstruktion.

Wenn ein gespeichertes Original die **Prüfsumme nicht besteht**, werden der Export und die Wiederherstellung mit einer Fehlermeldung abgelehnt. Die Rückgabe der Rekonstruktion anstelle des Originals würde verschleiern, dass die archivierte Kopie verändert wurde, was genau das ist, was ein Archiv niemals tun darf.

Eine Wiederherstellung liest das gespeicherte Original und fügt es über den Client des Providers an, sodass das Postfach die originalen Header und die MIME-Struktur erhält. Der exakte Byte-Stream ist bei der Übertragung nicht garantiert, da der Client die Nachricht erneut serialisiert; die archivierte Kopie selbst bleibt unverändert.

## ⚙️ Einstellungen {#settings}

Alle Einstellungen befinden sich im Abschnitt `RawMessages` oder als Umgebungsvariablen mit dem Präfix `RawMessages__`.

| Einstellung | Standard | Beschreibung |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Speichert die Originalnachricht jeder neu archivierten E-Mail. E-Mails, die vor der Aktivierung archiviert wurden, haben kein Original. |
| `RawMessages:MaxMessageSizeMb` | `50` | Nachrichten, die größer als dieser Wert sind, werden ohne ihr Original archiviert. `0` = kein Limit. |
| `RawMessages:IncludeGraph` | `true` | Speichert das Original auch für Microsoft 365 (Graph) Konten. Dies verursacht eine zusätzliche Graph-Anfrage pro Nachricht, die auf das Throttling-Budget angerechnet wird. |
| `RawMessages:Storage` | `postgres` | Wo die Nachricht-Bytes aufbewahrt werden: `postgres` (in der Datenbank) oder `filesystem` (gzip-Datei neben dem Archiv). Siehe [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Verzeichnis, in das die Nachrichtendateien geschrieben werden, relativ zum Content-Root der Anwendung. Verweisen Sie dies in Docker auf ein gemountetes Volume. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Anzahl der zweistelligen Verzeichnis-Ebenen, über die die E-Mail-ID verteilt wird. `2` ergibt 10.000 Verzeichnisse, `0` ist flach. Hält die Anzahl der Dateien pro Verzeichnis in großen Archiven niedrig. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Berechnet bei jedem Download den SHA-256 der Datei neu und vergleicht ihn mit der zum Zeitpunkt der Archivierung aufgezeichneten Prüfsumme. Bei einer Abweichung schlägt der Download mit HTTP 500 fehl, anstatt veränderte Bytes auszuliefern. |

### Dimensionierung {#sizing}

Der Speicherplatz wächst etwa um die Größe jeder Mail inklusive ihrer Anhänge, gzip-komprimiert. Anhänge werden innerhalb des gespeicherten Originals **nicht** dedupliziert, daher kostet eine E-Mail mit einem großen Anhang diesen Anhang doppelt: einmal im deduplizierten Anhang-Speicher und einmal innerhalb des Originals. Planen Sie mit etwa dem **doppelten** Datenbankvolumen eines Archivs ohne Originale.

Das Aktivieren der Einstellung führt zu keiner nachträglichen Befüllung bestehender Mails. Nur neu archivierte E-Mails erhalten ein Original.

## 💾 Storage backend {#storage-backend}

Die Einstellung `Storage` entscheidet darüber, wo die Bytes liegen. Die **Metadaten-Zeile bleibt immer in
der Datenbank**, egal welches Backend konfiguriert ist, und enthält den SHA-256, die Größe, die Erfassungsquelle und (bei Dateien) den relativen Pfad. Diese Zeile ist es, die das Archiv beweisen lässt, was eine
gespeicherte Nachricht enthalten sollte.

### postgres (default) {#postgres-default}

Die komprimierte Nachricht liegt in der Spalte `Content` von `mail_archiver."RawMessages"`.
Dies ist das Verhalten, das das Archiv schon immer hatte und keinen zusätzlichen Speicherplatz benötigt.

Wählen Sie dies, wenn die Datenbank bereits Ihre Sicherungseinheit ist und Sie nur eine Sache
sichern und wiederherstellen möchten.

### filesystem {#filesystem}

Die Nachricht wird nach `{BasePath}/{xx}/{yy}/{id}.eml.gz` geschrieben, wobei `xx`/`yy` aus der
E-Mail-ID abgeleitet werden. Die Datenbankzeile behält nur die Metadaten.

Wählen Sie dies, wenn das Archiv so groß ist, dass die Originale die Datenbankgröße dominieren würden, oder wenn Sie die Nachrichten-Dateien auf günstigerem oder separatem Speicher ablegen möchten.

!!! danger "Mount a volume in Docker"
    Das Standardverzeichnis `BasePath` (`RawMessages`) wird zu `/app/RawMessages` **innerhalb des
    Containers** aufgelöst. Ohne einen Bind-Mount gehört dieses Verzeichnis zur beschreibbaren
    Schicht des Containers, sodass jeder neu erstellte Container – einschließlich `docker compose down && docker compose
    up -d`, ein Image-Update oder ein Host-Reboot mit einer ephemeren Schicht – jedes gespeicherte
    Original mit sich nimmt, während die Datenbankzeilen überleben.

    Mounten Sie das Verzeichnis beim Verwenden des filesystem-Backends immer auf den Host:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Der Host-Pfad (hier `./raw-messages`) ist das, was Sie sichern. Der Container-Pfad muss
    mit `RawMessages:FileSystem:BasePath` übereinstimmen oder Sie setzen `RawMessages:FileSystem:BasePath` auf den
    gewählten Mount-Punkt. Wenn Sie den Mount verschieben, lassen Sie `BasePath` darauf zeigen: Die
    gespeicherten `StoragePath`-Werte sind relativ, sodass sie weiterhin aufgelöst werden können.

### Switching backends {#switching-backends}

Lesevorgänge folgen dem in jeder Zeile aufgezeichneten Speicherort, nicht der konfigurierten Einstellung. Nachdem Sie
`Storage` geändert haben, bleiben früher archivierte Nachrichten von dort aus herunterladbar, wo sie liegen.
Die Einstellung wirkt sich nur auf **neu** archivierte E-Mails aus.

Um das bereits Gespeicherte zu verschieben, führen Sie die Speicher-Migration aus. Siehe die
[CLI commands](CLI.md#moving-stored-original-messages) für die vollständige Referenz:

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Was die Migration garantiert:

- Jede Nachricht wird vor dem Kopieren gegen ihren aufgezeichneten Prüfsummenwert verifiziert. Eine Nachricht,
  die fehlschlägt, bleibt dort, wo sie ist, und wird gemeldet: Ein beschädigtes Original wird niemals weitergegeben und
  seine einzige verbleibende Kopie wird niemals gelöscht.
- Eine Datei wird erst nach Abschluss der Datenbanktransaktion, die sie verschoben hat, gelöscht, sodass ein
  unterbrochener Durchlauf nicht die einzige Kopie verlieren kann.
- Der Durchlauf ist fortsetzbar. Die Arbeit wird durch die Spalte `StorageType` ausgewählt, sodass das erneute
  Starten des Befehls mit dem fortfährt, was noch ausstehend ist.
- Der Exit-Code ist `1`, falls eine Nachricht fehlgeschlagen ist, `0` andernfalls.

Eine Migration ist nicht erforderlich, damit das Archiv funktioniert. Sie wird nur benötigt, um vorhandene
Bytes in das andere Backend zu verschieben.

## 🛟 Sicherung und Wiederherstellung {#backup-and-restore}

=== "postgres backend"
    Nichts Besonderes. Die Originale sind Teil der Datenbank und sind durch den normalen
    Dump abgedeckt, siehe [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Sichern Sie das `BasePath`-Verzeichnis **zusammen mit** dem Datenbank-Dump.

    Eine Datenbank-Wiederherstellung ohne die Dateien hinterlässt das Archiv mit Zeilen, deren Inhalt
    nicht bereitgestellt werden kann. Der Download antwortet dann mit HTTP 404, anstatt etwas
    Falsches zurückzugeben, und die E-Mails selbst bleiben aus ihren geparsten Feldern lesbar.

## 🗑️ Löschen einer E-Mail {#deleting-an-email}

Das Löschen einer E-Mail entfernt mit ihr auch das Original: Aufbewahrungsregeln, das manuelle Einzel- und Massenlöschen sowie das Löschen eines gesamten E-Mail-Kontos entfernen die Datei als Teil desselben Vorgangs. Mit dem `postgres`-Backend ist nichts weiter zu tun, da die Bytes innerhalb der Zeile übertragen werden, die die Kaskadierung bereits entfernt.

Die Datei wird **nach** dem Commit der E-Mail-Zeile gelöscht, niemals davor. Die beiden Schritte können nicht in einer einzigen Transaktion erfolgen, da eine Datei nicht Teil der Datenbanktransaktion ist, sodass einer der beiden Schritte als zweiter erfolgen muss. Diese Reihenfolge ist die sichere: Eine Löschung, die fehlschlägt oder zurückgerollt wird (zum Beispiel bei einem gesperrten Datensatz), lässt das Original unberührt, und ein Absturz zwischen den beiden Schritten hinterlässt eine Datei ohne Zeile, die durch den unten beschriebenen Sweep zurückgewonnen wird. Die andere Reihenfolge würde das Original einer E-Mail zerstören, die noch existiert.

**Verwaiste Dateien werden gesammelt.** Alles, was durchgerutscht ist, einschließlich Dateien, die durch Löschungen hinterlassen wurden, bevor diese Bereinigung existierte, wird durch einen periodischen Sweep von `BasePath` entfernt, der jede Datei mit den Zeilen vergleicht und diejenigen löscht, auf die sich keine E-Mail bezieht:

| Einstellung | Standard | Bedeutung |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Den Sweep ein- oder ausschalten |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Wie oft er ausgeführt wird |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Dateien, die jünger als dieser Wert sind, werden nie angefasst |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Verzögerung vor dem ersten Durchlauf, damit eine ausstehende Speicher-Migration zuerst abgeschlossen wird |

Die Schonfrist macht den Sweep sicher: Ein Import schreibt eine Datei, bevor er die Zeile committet, die auf sie verweist, und eine vor wenigen Augenblicken geschriebene Datei darf nicht mit einer verwaisten Datei verwechselt werden. Halb geschriebene `.tmp`-Dateien aus einem unterbrochenen Schreibvorgang werden nach derselben Regel gesammelt. Mit dem `postgres`-Backend bewirkt der Sweep nichts und durchläuft nicht einmal das Verzeichnis.

## 🔍 Überprüfung eines Archivs {#verifying-an-archive}

Die zum Zeitpunkt der Archivierung aufgezeichnete Prüfsumme ermöglicht die Verifizierung.

- **Beim Download** (`VerifyChecksumOnRead=true`, der Standard) wird die Datei gehasht und
  verglichen. Bei einer Abweichung wird der Download mit HTTP 500 abgelehnt.
- **Bei der Speicher-Migration** wird jede Nachricht vor dem Kopieren verifiziert, in
  beide Richtungen.

Da sich die Metadaten-Zeile in der Datenbank befindet und die Bytes auf der Festplatte liegen können, ist ein Prüfsummenvergleich der einzige Weg, um festzustellen, dass eine Datei außerhalb der Anwendung geändert wurde.

## ⚠️ Wichtige Hinweise {#things-to-know}

- **Aktivierung erfolgt ohne Backfill.** Nur E-Mails, die nach der Umstellung im Archiv abgelegt werden, erhalten ein Original.
- **Die Rekonstruktion ist nicht die Nachricht.** Bei E-Mails ohne gespeichertes Original liefert jeder Download-Pfad eine neu zusammengesetzte Nachricht zurück und weist dies via `X-Raw-Message-Reconstructed: true` aus.
- **Dateisystem bedeutet zwei Sicherungseinheiten.** Datenbank und Nachrichtenverzeichnis müssen gemeinsam wiederhergestellt werden, andernfalls bleiben die Zeilen ohne ihren Inhalt bestehen.
- **`VerifyChecksumOnRead` kostet CPU.** Jedes Herunterladen einer großen Nachricht führt zu einer Dekomprimierung und einem Hashing. Lassen Sie diese Funktion aktiviert, es sei denn, Sie haben ein Problem gemessen; sie ist der einzige Schutz gegen stille Veränderungen auf der Festplatte.
- **Das gespeicherte Original wird nicht dedupliziert.** Ein Anhang, der in vielen E-Mails vorkommt, wird einmal im Anhang-Speicher abgelegt, aber einmal pro E-Mail innerhalb des Originals gespeichert.
- **Löschen entfernt auch das Original.** Eine E-Mail, die durch Aufbewahrung oder manuell gelöscht wird, nimmt ihr gespeichertes Original mit sich, sowohl die Datei als auch die Zeile. Es wird nichts behalten, das das Archiv nicht mehr auflistet.

## 🔗 Siehe auch {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - die vollständige Speicher-Migration
- [Setup and Parameters](Setup.md) - alle Konfigurationsparameter
- [REST API](API.md#download-original-message) - der `/raw`-Endpunkt
- [MCP Server](MCP.md#get_email_raw) - das `get_email_raw`-Tool
- [Backup and Restore](BackupRestore.md) - was gesichert werden sollte
- [Attachment Deduplication](AttachmentDeduplication.md) - wie Anhänge gespeichert werden
