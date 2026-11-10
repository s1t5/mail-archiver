# 📧 Original Message Storage {#original-message-storage}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Standardmäßig speichert das Archiv die **geparsten Teile** einer E-Mail: Header, Text- und HTML-Bodies, Anhänge. Ein exportiertes `.eml` wird aus diesen Feldern neu erstellt.

Dies reicht aus, um eine E-Mail zu lesen, aber es ist nicht die Nachricht **im Originalzustand**. Transfer-Kodierungen, MIME-Grenzen und Header-Folding unterscheiden sich vom Original, sodass eine neu erstellte Nachricht ihre DKIM-Signatur nicht mehr verifiziert und Spam-Scanner sie anders bewerten.

Wenn die Speicherung der Originalnachricht aktiviert ist, speichert das Archiv die vollständige Nachricht so, wie sie eingegangen ist, zusätzlich zu den geparsten Feldern. Das gespeicherte Original ist das, was das Archiv als zugestellt nachweisen kann.

| | Nur geparste Felder | Mit gespeicherten Originalen |
| --- | --- | --- |
| Header, Body, Anhänge | ✅ | ✅ |
| Byte-exakte Nachricht | ❌ | ✅ |
| DKIM-Signatur verifizierbar | ❌ | ✅ |
| Forensische Nutzung (Gericht, Audit) | eingeschränkt | ✅ |

## 🎯 Wo das Original bereitgestellt wird {#where-the-original-is-served}

Sobald es gespeichert wurde, wird das Original überall zurückgegeben, wo eine vollständige Nachricht erwartet wird:

| Ort | Was passiert |
| --- | --- |
| **Export EML**-Schaltfläche auf der E-Mail-Detailseite | Stellt das gespeicherte Original bereit, wenn eines vorhanden ist, und greift auf die Rekonstruktion zurück, wenn E-Mails ohne ein solches archiviert wurden. |
| **Wiederherstellung in ein Postfach** | Fügt das gespeicherte Original hinzu, wenn eines vorhanden ist, und greift auf die Rekonstruktion zurück, wenn E-Mails ohne ein solches archiviert wurden. |
| `GET /api/v1/emails/{id}/raw` | REST API Download, siehe den [REST API Guide](API.md#download-original-message). |
| `get_email_raw` | MCP Tool, siehe den [MCP Guide](MCP.md#get_email_raw). |

Die Antwort teilt Ihnen mit, welche Form Sie erhalten haben, sodass ein Aufrufer ein echtes Original von einer Rekonstruktion unterscheiden kann, ohne die Bytes zu untersuchen:

| Signal | Bedeutung |
| --- | --- |
| `X-Raw-Message-Source: imap` | Exakte Kopie vom IMAP-Server (`BODY[]` wie zugestellt). |
| `X-Raw-Message-Source: graph` | Exakte Kopie, die der MIME-Inhalt Exchange zurückgegeben hat. |
| `X-Raw-Message-Source: eml-file` | Exakte Kopie, der Inhalt der importierten `.eml`-Datei. |
| `X-Raw-Message-Source: serialized` | Geparste, neu geschriebene Nachricht. Identisch, außer dass die Zeilenendungen CRLF sind (MBox-Import, IMAP-Recovery-Fetch). |
| `X-Raw-Message-Sha256` | SHA-256 des gespeicherten Originals. |
| `X-Raw-Message-Reconstructed: true` | Kein Original gespeichert; die Nachricht wurde neu aufgebaut. Kein Prüfsummen-Header. |
| `X-Raw-Message-Storage` | Backend, aus dem das Original gelesen wurde (`postgres` oder `filesystem`). |

Der Dateiname sagt es ebenfalls: `email-{id}-original-...eml` für ein gespeichertes Original,
`email-{id}-...eml` für eine Rekonstruktion.

Wenn ein gespeichertes Original **seine Prüfsumme nicht besteht**, werden der Export und die Wiederherstellung mit einer Fehlermeldung abgelehnt. Die Rückgabe der Rekonstruktion anstelle des Originals würde verschleiern, dass die archivierte Kopie geändert wurde, was genau das ist, was ein Archiv niemals tun darf.

Eine Wiederherstellung liest das gespeicherte Original und fügt es über den Client des Anbieters hinzu, sodass das Postfach die Original-Header und die MIME-Struktur erhält. Der exakte Byte-Stream ist bei der Übertragung nicht garantiert, da der Client die Nachricht erneut serialisiert; die archivierte Kopie selbst bleibt unverändert.

## ⚙️ Einstellungen {#settings}

Alle Einstellungen befinden sich im `RawMessages`-Abschnitt oder als Umgebungsvariablen mit dem `RawMessages__`-Präfix.

| Einstellung | Standard | Beschreibung |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Speichert die Originalnachricht jeder neu archivierten E-Mail. E-Mails, die vor der Aktivierung archiviert wurden, haben kein Original. |
| `RawMessages:MaxMessageSizeMb` | `50` | Nachrichten, die größer als dieser Wert sind, werden ohne ihr Original archiviert. `0` = kein Limit. |
| `RawMessages:IncludeGraph` | `true` | Speichert das Original auch für Microsoft 365 (Graph) Konten. Dies verursacht eine zusätzliche Graph-Anfrage pro Nachricht, die auf das Throttling-Budget angerechnet wird. |
| `RawMessages:Storage` | `postgres` | Wo die Nachrichtenbytes aufbewahrt werden: `postgres` (in der Datenbank) oder `filesystem` (gzip-Datei neben dem Archiv). Siehe [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Verzeichnis, in das die Nachrichtendateien geschrieben werden, relativ zur Content-Root der Anwendung. Zeigen Sie dies auf ein gemountetes Volume in Docker. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Anzahl der zweistelligen Verzeichnisebenen, über die die E-Mail-ID verteilt wird. `2` ergibt 10.000 Verzeichnisse, `0` ist flach. Hält die Dateianzahl pro Verzeichnis in großen Archiven niedrig. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Berechnet das SHA-256 der Datei bei jedem Download neu und vergleicht es mit der zum Zeitpunkt der Archivierung aufgezeichneten Prüfsumme. Eine Diskrepanz führt zum Abbruch des Downloads mit HTTP 500, anstatt geänderte Bytes auszuliefern. |

### Dimensionierung {#sizing}

Der Speicherplatz wächst etwa um die Größe jeder Mail einschließlich ihrer Anhänge, gzip-komprimiert. Anhänge werden innerhalb des gespeicherten Originals **nicht** dedupliziert, sodass eine E-Mail mit einem großen Anhang diesen Anhang doppelt kostet: einmal im deduplizierten Anhang-Speicher und einmal innerhalb des Originals. Planen Sie mit etwa dem **doppelten** Datenbank-Größe eines Archivs ohne Originale.

Das Aktivieren der Einstellung führt keine Nachbearbeitung für vorhandene Mails durch. Nur neu archivierte E-Mails erhalten ein Original.

## 💾 Storage-Backend {#storage-backend}

Die `Storage`-Einstellung entscheidet darüber, wo die Bytes liegen. Die **Metadaten-Zeile bleibt immer in
der Datenbank**, egal welches Backend konfiguriert ist, und enthält den SHA-256-Hash, die Größe, die Erfassungsquelle und (bei Dateien) den relativen Pfad. Diese Zeile ist es, die das Archiv beweisen lässt, was eine
gespeicherte Nachricht enthalten sollte.

### postgres (default) {#postgres-default}

Die komprimierte Nachricht liegt in der `Content`-Spalte von `mail_archiver."RawMessages"`.
Dies ist das Verhalten, das das Archiv schon immer hatte, und es benötigt kein zusätzliches Volume.

Wählen Sie dies, wenn die Datenbank bereits Ihre Sicherungseinheit ist und Sie nur eine Sache
sichern und wiederherstellen möchten.

### filesystem {#filesystem}

Die Nachricht wird unter `{BasePath}/{xx}/{yy}/{id}.eml.gz` geschrieben, wobei `xx`/`yy` aus der E-Mail-ID abgeleitet
werden. Die Datenbankzeile behält nur die Metadaten.

Wählen Sie dies, wenn das Archiv so groß ist, dass die Originale die Datenbankgröße dominieren würden, oder wenn Sie die Nachrichten-Dateien auf günstigerem oder separatem Speicher ablegen möchten.

!!! danger "Ein Volume in Docker mounten"
    Das Standard-`BasePath` (`RawMessages`) wird zu `/app/RawMessages` **innerhalb des
    Containers** aufgelöst. Ohne einen Bind-Mount gehört dieses Verzeichnis zur beschreibbaren
    Schicht des Containers, sodass jeder neu erstellte Container – einschließlich `docker compose down && docker compose
    up -d`, ein Image-Update oder ein Host-Reboot mit einer ephemeral Schicht – jedes gespeicherte
    Original mitnimmt, während die Datenbankzeilen überleben.

    Mounten Sie das Verzeichnis immer auf den Host, wenn Sie das filesystem-Backend verwenden:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Der Host-Pfad (hier `./raw-messages`) ist das, was Sie sichern. Der Container-Pfad muss
    mit `RawMessages:FileSystem:BasePath` übereinstimmen oder `RawMessages:FileSystem:BasePath` auf den
    gewählten Mountpoint setzen. Wenn Sie den Mount verschieben, lassen Sie `BasePath` weiterhin darauf zeigen: Die
    gespeicherten `StoragePath`-Werte sind relativ, sodass sie weiterhin aufgelöst werden können.

### Wechseln der Backends {#switching-backends}

Das Lesen folgt dem in jeder Zeile aufgezeichneten Speicherort, nicht der konfigurierten Einstellung. Nachdem
Sie `Storage` geändert haben, bleiben früher archivierte Nachrichten von dort aus herunterladbar, wo sie sich befinden.
Die Einstellung wirkt sich nur auf **neu** archivierte E-Mails aus.

Um das bereits Gespeicherte zu verschieben, führen Sie die Storage-Migration aus. Siehe
die [CLI-Befehle](CLI.md#moving-stored-original-messages) für die vollständige Referenz:

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
  die fehlschlägt, bleibt dort, wo sie ist, und wird gemeldet: Ein beschädigtes Original wird niemals propagiert und
  seine einzige verbleibende Kopie wird niemals gelöscht.
- Eine Datei wird erst gelöscht, nachdem die Datenbanktransaktion, die sie verschoben hat, abgeschlossen wurde, sodass ein
  unterbrochener Durchlauf nicht die einzige Kopie verlieren kann.
- Der Durchlauf ist fortsetzbar. Die Arbeit wird durch die `StorageType`-Spalte ausgewählt, sodass der
  Befehl erneut gestartet wird, um mit dem fortzufahren, was noch ausstehend ist.
- Der Exit-Code ist `1`, falls eine Nachricht fehlgeschlagen ist, andernfalls `0`.

Eine Migration ist nicht erforderlich, damit das Archiv funktioniert. Sie wird nur benötigt, um vorhandene
Bytes in das andere Backend zu verschieben.

## 🛟 Sicherung und Wiederherstellung {#backup-and-restore}

=== "postgres backend"
    Nichts Besonderes. Die Originale sind Teil der Datenbank und sind durch den normalen
    Dump abgedeckt, siehe [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Sichern Sie das `BasePath` Verzeichnis **zusammen mit** dem Datenbank-Dump.

    Eine Datenbank-Wiederherstellung ohne die Dateien hinterlässt das Archiv mit Zeilen, deren Inhalt
    nicht bereitgestellt werden kann. Der Download antwortet dann mit HTTP 404, anstatt etwas
    Falsches zurückzugeben, und die E-Mails selbst bleiben über ihre geparsten Felder lesbar.

## 🗑️ Löschen einer E-Mail {#deleting-an-email}

Das Löschen einer E-Mail entfernt auch das Original: Aufbewahrungsregeln, das manuelle Löschen einzelner oder mehrerer E-Mails sowie das Löschen eines gesamten E-Mail-Kontos entfernen die Datei als Teil desselben Vorgangs. Mit dem `postgres`-Backend ist kein zusätzlicher Aufwand erforderlich, da die Bytes innerhalb der Zeile übertragen werden, die durch die Kaskadierung bereits entfernt wird.

Die Datei wird **nach** dem Commit der E-Mail-Zeile gelöscht, niemals davor. Die beiden Schritte können nicht eine einzige Transaktion bilden, da eine Datei nicht Teil der Datenbanktransaktion ist, sodass einer von beiden als zweiter Schritt erfolgen muss. Diese Reihenfolge ist die sichere: Ein Löschvorgang, der fehlschlägt oder zurückgerollt wird (zum Beispiel bei einem gesperrten Datensatz), lässt das Original unberührt, und ein Absturz zwischen den beiden Schritten hinterlässt eine Datei ohne Zeile, die durch den unten beschriebenen Sweep zurückgewonnen wird. Die umgekehrte Reihenfolge würde das Original einer E-Mail zerstören, die noch existiert.

**Verwaiste Dateien werden gesammelt.** Alles, was durchgegangen ist, einschließlich Dateien, die durch Löschvorgänge vor der Einführung dieser Bereinigung zurückgeblieben sind, wird durch einen periodischen Sweep von `BasePath` entfernt, der jede Datei mit den Zeilen vergleicht und diejenigen löscht, auf die sich keine E-Mail bezieht:

| Einstellung | Standard | Bedeutung |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Den Sweep ein- oder ausschalten |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Wie oft er ausgeführt wird |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Dateien, die jünger als dies sind, werden niemals angefasst |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Verzögerung vor dem ersten Durchlauf, damit eine anstehende Speicher-Migration zuerst abgeschlossen wird |

Die Schonfrist macht den Sweep sicher: Ein Import schreibt eine Datei, bevor er die Zeile committet, die auf sie verweist, und eine vor wenigen Augenblicken geschriebene Datei darf nicht mit einer verwaisten Datei verwechselt werden. Halb geschriebene `.tmp`-Dateien aus einem unterbrochenen Schreibvorgang werden unter derselben Regel gesammelt. Mit dem `postgres`-Backend bewirkt der Sweep nichts und durchläuft nicht einmal das Verzeichnis.

## 🔍 Überprüfen eines Archivs {#verifying-an-archive}

Der zum Zeitpunkt der Archivierung aufgezeichnete Prüfsummenwert ermöglicht die Überprüfung.

- **Beim Download** (`VerifyChecksumOnRead=true`, Standard) wird die Datei gehasht und verglichen. Bei einer Unstimmigkeit wird der Download mit HTTP 500 abgelehnt.
- **Bei der Speicher-Migration** wird jede Nachricht vor dem Kopieren in beide Richtungen überprüft.

Da die Metadaten-Zeile in der Datenbank liegt und die Bytes möglicherweise auf der Festplatte liegen, ist ein Prüfsummenvergleich der einzige Weg, um festzustellen, dass eine Datei außerhalb der Anwendung geändert wurde.

## ⚠️ Was Sie wissen sollten {#things-to-know}

- **Das Aktivieren führt zu keiner Nachverarbeitung.** Nur E-Mails, die nach der Umstellung im Archiv abgelegt werden, erhalten ein Original.
- **Die Rekonstruktion ist nicht die Nachricht.** Bei E-Mails ohne gespeichertes Original liefert jeder Downloadpfad eine neu erstellte Nachricht zurück und weist über `X-Raw-Message-Reconstructed: true` darauf hin.
- **Das Dateisystem bedeutet zwei Sicherungseinheiten.** Datenbank und Nachrichtenverzeichnis müssen gemeinsam wiederhergestellt werden, andernfalls bleiben die Zeilen ohne ihren Inhalt bestehen.
- **`VerifyChecksumOnRead` kostet CPU.** Jedes Herunterladen einer großen Nachricht wird dekomprimiert und gehasht. Lassen Sie es aktiviert, es sei denn, Sie haben ein Problem festgestellt; es ist der einzige Schutz gegen stille Veränderungen auf der Festplatte.
- **Das gespeicherte Original wird nicht dedupliziert.** Ein Anhang, der in vielen E-Mails vorkommt, wird einmal im Anhang-Speicher abgelegt, aber einmal pro E-Mail innerhalb des Originals.
- **Das Löschen entfernt auch das Original.** Eine E-Mail, die durch Aufbewahrung oder manuell gelöscht wird, nimmt ihr gespeichertes Original mit, Datei und Zeile. Nichts wird behalten, was das Archiv nicht mehr auflistet.

## 🔗 Siehe auch {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - die vollständige Speichermigration
- [Setup and Parameters](Setup.md) - alle Konfigurationsparameter
- [REST API](API.md#download-original-message) - der `/raw`-Endpunkt
- [MCP Server](MCP.md#get_email_raw) - das `get_email_raw`-Tool
- [Backup and Restore](BackupRestore.md) - was gesichert werden soll
- [Attachment Deduplication](AttachmentDeduplication.md) - wie Anhänge gespeichert werden
