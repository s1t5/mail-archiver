# 🧬 Anhang Deduplizierung {#attachment-deduplication}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

E-Mail-Anhänge werden als binäre Nutzlasten in der Datenbank gespeichert. Die **gleiche Datei** wird sehr oft an viele E-Mails angehängt (Firmenlogos in Signaturen, weitergeleitete PDFs, Newsletter, Verträge, ...). Ohne Deduplizierung behält jeder Anhang seine eigene private Kopie dieser Bytes, was eine große Menge an Datenbankplatz verschwendet.

Die **Anhang-Deduplizierung** speichert jede *eindeutige* Nutzlast genau einmal in einer inhaltsadressierten Tabelle (geschlüsselt durch den SHA-256-Hash der Bytes) und ermöglicht es allen identischen Anhängen, auf diese eine gemeinsame Kopie zu verweisen. Dies reduziert in der Regel den Speicherbedarf von anhanglastigen Archiven erheblich.

## ⚙️ Funktionsweise {#how-it-works}

### Neue Anhänge {#new-attachments}
Wann immer eine neue E-Mail archiviert wird (IMAP, Microsoft 365 / Graph, EML-Import, MBOX-Import, ...), wird die Anhang-Nutzlast gehasht und nur einmal gespeichert. Wenn die gleiche Nutzlast bereits existiert, verweist der neue Anhang einfach auf die vorhandene Inhaltszeile. Dies geschieht transparent auf der Datenebene, sodass jeder Archivierungspfad automatisch abgedeckt ist.

### Vorhandene Daten (Migration) {#existing-data-migration}
Für Installationen, die bereits Anhänge enthalten, die **vor** der Einführung dieser Funktion archiviert wurden, konvertiert eine **robuste, fortsetzbare Hintergrund-Migration** die vorhandenen Inline-Nutzlasten in gemeinsam genutzte Inhaltszeilen:

- Sie verarbeitet Anhänge **schrittweise (Batch für Batch)**, wobei jeder Batch in einer eigenen Transaktion erfolgt.
- Der Fortschritt wird in einer speziellen Statustabelle gespeichert, sodass die Migration **einen Neustart der Anwendung übersteht** und einfach dort fortfährt, wo sie aufgehört hat.
- Sie ist **idempotent** – bereits migrierte Anhänge werden nie wieder angefasst, selbst nach einem Absturz oder Neustart.
- Sie läuft im Hintergrund und blockiert die normale Nutzung der Anwendung nicht.

Wenn die Migration abgeschlossen ist, protokolliert sie eine Abschlussmeldung, z. B.:

```
Attachment Deduplication migration COMPLETED. 36199 attachments processed in 1.6 minutes.
```

Beim nächsten Start erkennt das System, dass nichts mehr zu tun ist, und bleibt im Leerlauf. Sollten jemals nicht deduplizierte Anhänge verbleiben (zum Beispiel nach der Wiederherstellung einer älteren Sicherung), wird die Migration automatisch fortgesetzt und diese migriert – das System konvergiert immer zu einem vollständig deduplizierten Zustand.

### Garbage Collection (immer aktiv) {#garbage-collection-always-on}
Wenn E-Mails (und damit Anhänge) gelöscht werden, kann eine gemeinsam genutzte Nutzlast unverknüpft werden. Eine integrierte Garbage Collection entfernt diese verwaisten Inhaltszeilen.

> 🧹 **Die Bereinigung der Verwaisten läuft IMMER**, unabhängig von der
> `DatabaseMaintenance` Funktion. Sie läuft einmal direkt nach der Migration und dann
> periodisch (alle `OrphanCleanupIntervalHours`, standardmäßig 12 h). Wenn
> `DatabaseMaintenance__Enabled=true`, läuft die gleiche Bereinigung zusätzlich vor der
> täglichen `VACUUM`, damit der freigegebene Speicherplatz physisch zurückgewonnen wird.

### Festplattenbelegung nach der Migration (wichtig) {#disk-usage-after-the-migration-important}

> 📈 **Der Wert "Storage" im Dashboard kann unmittelbar nach der
> Migration kurzzeitig ANSTEIGEN** (z. B. 3,5 GB → 5,3 GB). Dies ist erwartet und kein Fehler.
>
> Das Dashboard meldet die **physische** Datenbankgröße (`pg_database_size`). Wenn die
> Migration eine Nutzlast in den gemeinsamen Speicher verschiebt, wird die alte Inline-Kopie auf `NULL` gesetzt,
> aber PostgreSQL (MVCC) behält diese alten Bytes als *dead tuples*, bis die Tabellendatei
> neu geschrieben wird – daher enthält die Datenbank für einen Moment sowohl die alte als auch die neue Kopie.
>
> Um dies automatisch zu beheben, führt die Anwendung eine **einmalige**
> `VACUUM FULL` auf der Anhänge-Tabelle aus, **sobald die Migration abgeschlossen ist und keine
> Synchronisierungsläufe (synchronization) laufen**. Dies schreibt die Tabelle neu und gibt den freigegebenen Speicherplatz an
> das Betriebssystem zurück; danach sinkt die gemeldete Größe **unter** den ursprünglichen Wert. Es wird genau einmal ausgeführt (verfolgt über einen `ReclaimedAt` Marker), ist absturzsicher und wird aufgeschoben,
> solange eine Synchronisierung läuft (es versucht es automatisch erneut, sobald das System im Leerlauf ist).

>
> ⚠️ Die `VACUUM FULL` sperrt die Anhänge-Tabelle für ihre Dauer (in dieser Zeit keine Anhang-
> Lese-/Schreibvorgänge) und benötigt etwas freien Festplattenspeicher für das Neuschreiben. Wenn Sie dies stattdessen manuell auslösen möchten, führen Sie aus:
> ```sql
> VACUUM (FULL, ANALYZE) mail_archiver."EmailAttachments";
> ```

## 🔧 Konfiguration {#configuration}


Diese Einstellungen sind optional; die Standardwerte sind für die meisten Installationen sinnvoll. Siehe auch den [Setup Guide](Setup.md#-attachment-deduplication-settings).

| Einstellung | Standard | Beschreibung |
|---------|---------|-------------|
| `AttachmentDeduplication__BatchSize` | `200` | Anzahl der pro Transaktion migrierten vorhandenen Anhänge. Größere Werte migrieren schneller, erhöhen aber die Speicher-/DB-Last pro Batch. |
| `AttachmentDeduplication__DelayBetweenBatchesMs` | `0` | Optionale Pause (ms) zwischen Migrations-Batches, um die DB-Last auf ausgelasteten Systemen zu drosseln. |
| `AttachmentDeduplication__StartupDelaySeconds` | `20` | Verzögerung (s) nach App-Start, bevor die Migration beginnt (lässt die Schema-Migration zuerst abgeschlossen werden). |
| `AttachmentDeduplication__OrphanCleanupIntervalHours` | `12` | Intervall (h) der permanent laufenden Orphan-Garbage-Collection. |
| `AttachmentDeduplication__CommandTimeoutSeconds` | `300` | Datenbank-Befehls-Timeout (in Sekunden) für die Migrations-Batch-Operationen. Standard ist 5 Minuten. Wenn ein Batch immer noch ein Timeout verursacht, versucht der Dienst es automatisch mit der halben Batch-Größe erneut. |

Beispiel (`docker-compose.yml`):

```yaml
environment:
  - AttachmentDeduplication__BatchSize=200
  - AttachmentDeduplication__DelayBetweenBatchesMs=0
  - AttachmentDeduplication__StartupDelaySeconds=20
  - AttachmentDeduplication__OrphanCleanupIntervalHours=12
  - AttachmentDeduplication__CommandTimeoutSeconds=300
```

## ❓ FAQ {#faq}

**Kann ich die Deduplizierung deaktivieren?**
Nein. Sie ist eine Kernfunktion der Speicherung und immer aktiv. Sie können lediglich die oben genannten Batch-Größen und Zeitplanparameter anpassen.

**Sind meine Daten während der Migration sicher?**
Ja. Jeder Batch ist eine einzelne Transaktion und die Arbeit ist idempotent. Ein Absturz oder Neustart während der Migration macht den unvollständigen Batch rückgängig, und er wird beim nächsten Start erneut verarbeitet; es gehen keine Daten verloren und es entstehen keine Duplikate.

**Verlangsamt es das Archivieren?**
Der Overhead durch das Hashing pro Anhang ist vernachlässigbar. Die einmalige Migration bestehender Daten läuft im Hintergrund und kann bei Bedarf mit `DelayBetweenBatchesMs` gedrosselt werden.

**Die Migration läuft ständig in einen Timeout und startet am selben Cursor neu. Was kann ich tun?**
Dies kann bei sehr großen Datenbanken (Hunderttausende von E-Mails) passieren, wenn ein einzelner Batch von 200 Anhängen mit SHA-256-Hashing das Standard-Befehls-Timeout überschreitet. Der Dienst versucht es bei einem Timeout nun automatisch mit der halben Batch-Größe erneut. Wenn Sie weiterhin Timeouts sehen, können Sie die Batch-Größe manuell verringern und/oder das Befehls-Timeout erhöhen:

```yaml
environment:
  - AttachmentDeduplication__BatchSize=10
  - AttachmentDeduplication__CommandTimeoutSeconds=600
```

**Wie viel Platz werde ich sparen?**
Das hängt ganz davon ab, wie viele doppelte Anhänge Ihr Archiv enthält. Archive mit vielen wiederholten Signaturen, Logos oder weitergeleiteten Dateien profitieren am meisten.

---
