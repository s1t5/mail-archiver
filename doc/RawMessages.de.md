# 📧 Original Message Storage {#original-message-storage}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Standardmäßig behält das Archiv die **analysierten Teile** einer E-Mail: Header, Text- und HTML-Body, Anhänge. Ein exportiertes `.eml` wird aus diesen Feldern neu aufgebaut.

Das reicht aus, um eine E-Mail zu lesen, aber es ist nicht die Nachricht **wie sie empfangen wurde**. Übertragungs-Encodings, MIME-Boundaries und Header-Folding unterscheiden sich vom Original, sodass eine neu aufgebaute Nachricht ihre DKIM-Signatur nicht mehr verifiziert und Spam-Scanner sie anders bewerten.

Wenn die Speicherung der Originalnachricht aktiviert ist, behält das Archiv die vollständige Nachricht, wie sie eingetroffen ist, neben den analysierten Feldern. Das gespeicherte Original ist das, was das Archiv als zugestellt nachweisen kann.

| | Nur analysierte Felder | Mit gespeicherter Originalnachricht |
| --- | --- | --- |
| Header, Body, Anhänge | ✅ | ✅ |
| Byte-exakte Nachricht | ❌ | ✅ |
| DKIM-Signatur verifiziert | ❌ | ✅ |
| Forensische Nutzung (Gericht, Audit) | eingeschränkt | ✅ |

## 🎯 Wo das Original bereitgestellt wird {#where-the-original-is-served}

Sobald es gespeichert ist, wird das Original überall zurückgegeben, wo eine vollständige Nachricht erwartet wird:

| Ort | Was passiert |
| --- | --- |
| **Export EML** Schaltfläche auf der E-Mail-Detailseite | Stellt das gespeicherte Original bereit, sofern eines existiert, andernfalls wird die Rekonstruktion für E-Mails verwendet, die ohne Original archiviert wurden. |
| **Restore to a mailbox** | Hängt das gespeicherte Original an, sofern eines existiert, andernfalls wird die Rekonstruktion für E-Mails verwendet, die ohne Original archiviert wurden. |
| `GET /api/v1/emails/{id}/raw` | REST API Download, siehe den [REST API guide](API.md#download-original-message). |
| `get_email_raw` | MCP Tool, siehe den [MCP guide](MCP.md#get_email_raw). |

Die Antwort teilt Ihnen mit, welche Form Sie erhalten haben, sodass ein Aufrufer ein echtes Original von einer Rekonstruktion unterscheiden kann, ohne die Bytes zu prüfen:

| Signal | Bedeutung |
| --- | --- |
| `X-Raw-Message-Source: imap` | Exakte Kopie vom IMAP-Server (`BODY[]` wie geliefert). |
| `X-Raw-Message-Source: graph` | Exakte Kopie, der vom Exchange zurückgegebene MIME-Inhalt. |
| `X-Raw-Message-Source: eml-file` | Exakte Kopie, der Inhalt der importierten `.eml` Datei. |
| `X-Raw-Message-Source: serialized` | Parsed Message, die wieder ausgegeben wurde. Identisch, außer dass die Zeilenendungen CRLF sind (MBox Import, IMAP Recovery Fetch). |
| `X-Raw-Message-Sha256` | SHA-256 des gespeicherten Originals. |
| `X-Raw-Message-Reconstructed: true` | Kein Original gespeichert; die Nachricht wurde neu aufgebaut. Kein Checksum-Header. |
| `X-Raw-Message-Storage` | Backend, aus dem das Original gelesen wurde (`postgres` oder `filesystem`). |

Der Dateiname sagt es ebenfalls: `email-{id}-original-...eml` für ein gespeichertes Original, `email-{id}-...eml` für eine Rekonstruktion.

Wenn ein gespeichertes Original die **Prüfsumme nicht besteht**, werden der Export und die Wiederherstellung mit einer Fehlermeldung abgelehnt. Die stattdessen die Rekonstruktion zurückzugeben, würde verschleiern, dass die archivierte Kopie verändert wurde, was genau das ist, was ein Archiv niemals tun darf.

Eine Wiederherstellung liest das gespeicherte Original und hängt es über den Client des Providers an, sodass das Postfach die Original-Header und die MIME-Struktur erhält. Bei IMAP serialisiert der Client die Nachricht erneut, sodass der exakte Byte-Stream auf der Leitung nicht garantiert werden kann. Bei Microsoft 365 werden die archivierten Bytes unverändert übertragen, da Graph die Nachricht in MIME-Form akzeptiert. Die archivierte Kopie selbst wird niemals modifiziert.

## ⚙️ Einstellungen {#settings}

Alle Einstellungen befinden sich im Abschnitt `RawMessages` oder als Umgebungsvariablen mit dem Präfix `RawMessages__`.

| Einstellung | Standard | Beschreibung |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Speichert die Originalnachricht jeder neu archivierten E-Mail. E-Mails, die vor der Aktivierung archiviert wurden, haben kein Original. |
| `RawMessages:MaxMessageSizeMb` | `50` | Nachrichten, die größer als dieser Wert sind, werden ohne ihr Original archiviert. `0` = kein Limit. |
| `RawMessages:IncludeGraph` | `true` | Speichert auch das Original für Microsoft 365 (Graph) Konten. Dies verursacht eine zusätzliche Graph-Anfrage pro Nachricht, die auf das Throttling-Budget angerechnet wird. |
| `RawMessages:Storage` | `postgres` | Ort, an dem die Nachrichtenbytes aufbewahrt werden: `postgres` (in der Datenbank) oder `filesystem` (gzip-Datei neben dem Archiv). Siehe [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Verzeichnis, in das die Nachrichtendateien geschrieben werden, relativ zum Content-Root der Anwendung. Verweisen Sie dies in Docker auf ein gemountetes Volume. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Anzahl der zweistelligen Verzeichnisebenen, über die die E-Mail-ID verteilt wird. `2` ergibt 10.000 Verzeichnisse, `0` ist flach. Hält die Anzahl der Dateien pro Verzeichnis bei großen Archiven niedrig. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Berechnet bei jedem Download den SHA-256 des Dateis, vergleicht ihn mit der zum Zeitpunkt der Archivierung aufgezeichneten Prüfsumme. Bei einer Abweichung schlägt der Download mit HTTP 500 fehl, anstatt geänderte Bytes auszuliefern. |

### Dimensionierung {#sizing}

Der Speicherplatz wächst etwa um die Größe jeder Mail inklusive ihrer Anhänge, gzip-komprimiert. Anhänge werden innerhalb des gespeicherten Originals **nicht** dedupliziert, daher kostet eine E-Mail mit einem großen Anhang diesen Anhang doppelt: einmal im deduplizierten Anhangsspeicher und einmal innerhalb des Originals. Planen Sie mit etwa dem **doppelten** Datenbankvolumen eines Archivs ohne Originale.

Das Aktivieren der Einstellung führt nicht zu einer nachträglichen Befüllung bestehender Mails. Nur neu archivierte E-Mails erhalten ein Original.

Seite: 📧 Original Message Storage

## 💾 Storage backend {#storage-backend}

Die Einstellung `Storage` entscheidet, wo die Bytes liegen. Die **Metadaten-Zeile verbleibt immer in
der Datenbank**, unabhängig davon, welches Backend konfiguriert ist, und enthält den SHA-256, die Größe, die Erfassungsquelle und (bei Dateien) den relativen Pfad. Diese Zeile ist es, die das Archiv beweist, was eine
gespeicherte Nachricht enthalten sollte.

### postgres (default) {#postgres-default}

Die komprimierte Nachricht liegt in der Spalte `Content` von `mail_archiver."RawMessages"`.
Dies ist das Verhalten, das das Archiv schon immer hatte und kein zusätzliches Volume benötigt.

Wählen Sie dies, wenn die Datenbank bereits Ihre Sicherungseinheit ist und Sie nur eine Sache
sichern und wiederherstellen möchten.

### filesystem {#filesystem}

Die Nachricht wird unter `{BasePath}/{xx}/{yy}/{id}.eml.gz` geschrieben, wobei `xx`/`yy` aus der E-Mail-ID abgeleitet werden.
Die Datenbankzeile behält nur die Metadaten.

Wählen Sie dies, wenn das Archiv groß genug ist, dass die Originale die Datenbankgröße dominieren würden, oder wenn Sie die Nachrichtendateien auf günstigerem oder separatem Speicher ablegen möchten.

!!! danger "Mount a volume in Docker"
    Das Standard-`BasePath` (`RawMessages`) löst sich zu `/app/RawMessages` **innerhalb des
    Containers** auf. Ohne einen Bind-Mount gehört dieses Verzeichnis zur beschreibbaren Schicht des Containers, sodass jeder neu erstellte Container – einschließlich `docker compose down && docker compose
    up -d`, eines Image-Updates oder eines Host-Reboots mit einer ephemeren Schicht – jedes gespeicherte
    Original mit sich nimmt, während die Datenbankzeilen überleben.

    Mounten Sie das Verzeichnis immer auf den Host, wenn Sie das Filesystem-Backend verwenden:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Der Host-Pfad (hier `./raw-messages`) ist das, was Sie sichern. Der Container-Pfad muss
    mit `RawMessages:FileSystem:BasePath` übereinstimmen oder Sie setzen `RawMessages:FileSystem:BasePath` auf den
    gewählten Mountpunkt. Wenn Sie den Mount verschieben, lassen Sie `BasePath` weiterhin darauf zeigen: Die
    gespeicherten `StoragePath`-Werte sind relativ, sodass sie weiterhin aufgelöst werden können.

### Switching backends {#switching-backends}

Lesezugriffe folgen dem in jeder Zeile aufgezeichneten Speicherort, nicht der konfigurierten Einstellung. Nachdem Sie `Storage` geändert haben, bleiben früher archivierte Nachrichten von dort aus downloadbar, wo auch immer sie liegen.
Die Einstellung wirkt sich nur auf **neu** archivierte E-Mails aus.

Um das bereits Gespeicherte zu verschieben, führen Sie die Storage-Migration aus. Siehe die
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
  die fehlschlägt, bleibt dort, wo sie ist, und wird gemeldet: Ein korruptes Original wird niemals propagiert und
  seine einzige verbleibende Kopie wird niemals gelöscht.
- Eine Datei wird erst nach dem Commit der Datenbanktransaktion gelöscht, die sie verschoben hat, sodass ein
  unterbrochener Durchlauf nicht die einzige Kopie verlieren kann.
- Der Durchlauf ist fortsetzbar. Die Arbeit wird über die Spalte `StorageType` ausgewählt, sodass das erneute
  Starten des Befehls mit dem fortfährt, was noch ausstehend ist.
- Der Exit-Code ist `1`, falls eine Nachricht fehlgeschlagen ist, andernfalls `0`.

Eine Migration ist nicht erforderlich, damit das Archiv funktioniert. Sie wird nur benötigt, um vorhandene
Bytes in das andere Backend zu verschieben.

## 🛟 Sicherung und Wiederherstellung {#backup-and-restore}

=== "postgres backend"
    Nichts Zusätzliches. Die Originale sind Teil der Datenbank und sind durch den normalen
    Dump abgedeckt, siehe [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Sichern Sie das `BasePath` Verzeichnis **zusammen mit** dem Datenbank-Dump.

    Eine Datenbank-Wiederherstellung ohne die Dateien hinterlässt das Archiv mit Zeilen, deren Inhalt
    nicht bereitgestellt werden kann. Der Download antwortet dann mit HTTP 404, anstatt etwas
    Falsches zurückzugeben, und die E-Mails selbst bleiben aus ihren analysierten Feldern lesbar.

## 🗑️ Eine E-Mail löschen {#deleting-an-email}

Das Löschen einer E-Mail entfernt mit ihr auch das Original: Aufbewahrungsregeln, das manuelle Einzel- und Massenlöschen sowie das Löschen eines gesamten E-Mail-Kontos entfernen die Datei als Teil desselben Vorgangs. Mit dem `postgres` Backend ist nichts weiter zu tun, da die Bytes innerhalb der Zeile übertragen werden, die die Kaskadierung bereits entfernt.

Die Datei wird **nach** dem Commit der E-Mail-Zeile gelöscht, niemals davor. Die beiden Schritte können nicht eine einzige Transaktion sein, da eine Datei nicht Teil der Datenbanktransaktion ist, daher muss einer von beiden als zweites erfolgen. Diese Reihenfolge ist die sichere: Eine Löschung, die fehlschlägt oder zurückrollt (zum Beispiel bei einem gesperrten Datensatz), lässt das Original unberührt, und ein Absturz zwischen den beiden Schritten hinterlässt eine Datei ohne Zeile, die der unten beschriebene Sweep zurückfordert. Die andere Reihenfolge würde das Original einer E-Mail zerstören, die noch existiert.

**Verwaiste Dateien werden gesammelt.** Alles, was durchgegangen ist, einschließlich Dateien, die durch Löschungen hinterlassen wurden, bevor diese Bereinigung existierte, wird durch einen periodischen Sweep von `BasePath` entfernt, der jede Datei mit den Zeilen vergleicht und diejenigen löscht, auf die keine E-Mail verweist:

| Einstellung | Standard | Bedeutung |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Den Sweep ein- oder ausschalten |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Wie oft er ausgeführt wird |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Dateien, die jünger als dies sind, werden nie angefasst |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Verzögerung vor dem ersten Durchlauf, damit eine ausstehende Speicher-Migration zuerst abgeschlossen wird |

Die Übergangsfrist ist das, was den Sweep sicher macht: Ein Import schreibt eine Datei, bevor er die Zeile committet, die auf sie verweist, und eine vor wenigen Momenten geschriebene Datei darf nicht mit einer verwaisten Datei verwechselt werden. Halb geschriebene `.tmp` Dateien aus einem unterbrochenen Schreibvorgang werden nach derselben Regel gesammelt. Mit dem `postgres` Backend bewirkt der Sweep nichts und durchläuft nicht einmal das Verzeichnis.

## 🔍 Verifying an Archiv {#verifying-an-archive}

Die zum Zeitpunkt der Archivierung aufgezeichnete Prüfsumme ermöglicht die Verifizierung.

- **Beim Download** (`VerifyChecksumOnRead=true`, der Standard) wird die Datei gehasht und
  verglichen. Bei einer Abweichung wird der Download mit HTTP 500 abgelehnt.
- **Bei der Speicher-Migration** wird jede Nachricht vor dem Kopieren in beide
  Richtungen verifiziert.

Da sich die Metadaten-Zeile in der Datenbank befindet und die Bytes auf der Festplatte liegen können, ist ein Prüfsummenvergleich der einzige Weg, um festzustellen, dass eine Datei außerhalb der Anwendung geändert wurde.

## ⚠️ Things to know {#things-to-know}

- **Das Aktivieren führt zu keinem Backfill.** Nur E-Mails, die nach der Umstellung im Archiv abgelegt werden, erhalten ein Original.
- **Ein fehlendes Original wird als Fehler protokolliert.** Wenn die Erfassung abgelehnt wird (siehe Nachricht oben `MaxMessageSizeMb`, keine nutzbare Erfassung) oder das Schreiben in das konfigurierte Backend fehlschlägt, enthält das Protokoll einen Fehler mit der E-Mail-ID und dem Grund. Die E-Mail verbleibt im Archiv ohne ihr Original; es erfolgt kein automatischer Backfill und kein Retry.
- **Die Detailseite kennzeichnet ein fehlendes Original.** Bei `RawMessages:Enabled` zeigt die Detailseite oberhalb der Header einen Hinweis an. Sie fragt das Storage-Backend ab, sodass auch eine verschwundene Datei im Dateisystem als fehlend markiert wird. Die E-Mail-Liste hat kein Badge pro Zeile; dies würde in der Statusspalte zu viel Platz einnehmen.
- **Die Rekonstruktion ist nicht die Nachricht.** Bei E-Mails ohne gespeichertes Original liefert jeder Download-Pfad eine neu aufgebauten Nachricht zurück und weist über `X-Raw-Message-Reconstructed: true` darauf hin.
- **Dateisystem bedeutet zwei Sicherungseinheiten.** Datenbank und Nachrichtenverzeichnis müssen gemeinsam wiederhergestellt werden, andernfalls überleben die Zeilen ohne ihren Inhalt.
- **`VerifyChecksumOnRead` kostet CPU.** Jede Sicherung einer großen Nachricht wird dekomprimiert und gehasht. Lassen Sie es aktiviert, sofern Sie kein Problem gemessen haben; es ist der einzige Schutz gegen stille Veränderungen auf der Festplatte.
- **Das gespeicherte Original wird nicht dedupliziert.** Ein Anhang, der in vielen E-Mails vorkommt, wird einmal im Anhang-Speicher abgelegt, aber einmal pro E-Mail innerhalb des Originals.
- **Das Löschen entfernt auch das Original.** Eine E-Mail, die durch Aufbewahrung oder manuell gelöscht wird, nimmt ihr gespeichertes Original mit sich, sowohl die Datei als auch die Zeile. Es wird nichts aufbewahrt, das das Archiv nicht mehr auflistet.

## 🔗 Siehe auch {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - die vollständige Speichermigration
- [Setup and Parameters](Setup.md) - alle Konfigurationsparameter
- [REST API](API.md#download-original-message) - der `/raw` Endpunkt
- [MCP Server](MCP.md#get_email_raw) - das `get_email_raw` Tool
- [Backup and Restore](BackupRestore.md) - was gesichert werden sollte
- [Attachment Deduplication](AttachmentDeduplication.md) - wie Anhänge gespeichert werden
