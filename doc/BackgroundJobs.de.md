# ⚙️ Hintergrund-Jobs {#background-jobs}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Die meiste Arbeit in Mail Archiver findet nicht statt, während Sie warten. Das Synchronisieren eines Postfachs, das Importieren einer MBOX-Datei, die Wiederherstellung von Tausenden von E-Mails oder das Exportieren eines Archivs kann Minuten bis Stunden dauern. Diese Aufgaben laufen als **Hintergrund-Jobs** — Sie starten sie, und sie laufen weiter, selbst wenn Sie den Browser schließen.

Die Seite **Hintergrund-Jobs** zeigt an, was derzeit läuft, was abgeschlossen wurde und was fehlgeschlagen ist. Dies ist die Anlaufstelle, wenn etwas länger als erwartet dauert oder wenn eine Synchronisierung mit Fehlern endet.

> **ℹ️ Administrator-Seite.** Hintergrund-Jobs erfordern Administratorrechte und sind über das Hauptmenü erreichbar. Reguläre Benutzer sehen diese Seite nicht — sie können zwar weiterhin Jobs starten (zum Beispiel eine Batch-Wiederherstellung), aber die Überwachung erfolgt hier.

## 🗂️ Job-Typen {#job-types}

Die Seite gruppiert Jobs nach Typ. Jeder Abschnitt zeigt bis zu die **20 neuesten** Jobs an, wobei laufende und in der Warteschlange befindliche Jobs nach oben sortiert werden — so wird ein alter Job nicht durch neuere aus dem Blickfeld gedrängt.

| Abschnitt | Was er abdeckt | Gestartet von |
|---|---|---|
| **Synchronisierungs-Jobs** | Abrufen neuer E-Mails vom Mailserver | Automatischer Zeitplan oder *Sync now* bei einem Konto |
| **MBox Import-Jobs** | Importieren von `.mbox` Dateien | Mail Accounts → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **EML Import-Jobs** | Importieren von `.eml` Dateien / ZIP-Archiven | Mail Accounts → [Import EML](Import-EML-MBox.md#-import-eml) |
| **Batch-Wiederherstellungs-Jobs** | Zurückkopieren ausgewählter E-Mails in ein Postfach | Archive → E-Mails auswählen → Batch Restore |
| **Konto-Export-Jobs** | Exportieren eines gesamten Kontos (mbox / EML) | Mail Accounts → [Export](Export-Account.md) |
| **Export-Jobs für ausgewählte E-Mails** | Exportieren einer handverlesenen Menge an E-Mails | Archive → E-Mails auswählen → Export |
| **E-Mail-Lösch-Jobs** | Löschen von E-Mails vom Server oder Archiv, einschließlich filterbasierter Massenlöschung | Aufbewahrungsrichtlinie, manuelle Löschung oder Archive → *Delete all matches* |
| **Konto-Lösch-Jobs** | Entfernen eines gesamten Mail-Kontos | Mail Accounts → Delete |
| **Offload** | Verschieben von E-Mails innerhalb eines Datumsfensters in ein anderes Konto | Mail Accounts → Offload |

## 📊 Lesen einer Job-Zeile {#reading-a-job-row}

Jede Zeile enthält denselben Satz an Spalten:

| Spalte | Bedeutung |
|---|---|
| **Job ID** | Kurzer Identifikator — geben Sie diesen bei der Meldung eines Problems an |
| **Konto** | Das Postfach, an dem der Job arbeitet |
| **Status** | Aktueller Zustand, siehe Tabelle unten |
| **Fortschritt** | Wie weit der Job fortgeschritten ist, mit verarbeitet/gesamt, sofern bekannt |
| **Erstellt** | Wann der Job in die Warteschlange eingereiht wurde |
| **Gestartet** | Wann er tatsächlich mit der Ausführung begann |
| **Abgeschlossen** | Wann er endete (leer, während er noch läuft) |
| **Dauer** | Vergangene Zeit |

Einige Job-Typen fügen der Fortschrittsspalte Zähler hinzu:

- **Success** / **Failed** — erfolgreich und erfolglos verarbeitete E-Mails
- **Skipped (duplicates)** — E-Mails, die bereits im Archiv vorhanden sind und nicht doppelt importiert wurden
- **Recovered** — Nachrichten, die erneut abgerufen wurden, nachdem der Server meldete, dass sie fehlen
- **Malformed (skipped)** — Nachrichten, die nicht geparst werden konnten und ausgelassen wurden

### Statuswerte {#status-values}

| Status | Bedeutung |
|---|---|
| **Queued** | Wartet auf Start — ein anderer Job läuft oder der Worker ist beschäftigt |
| **Running** | Wird derzeit ausgeführt |
| **Completed** | Erfolgreich abgeschlossen |
| **CompletedWithErrors** | Abgeschlossen, aber einige Elemente sind fehlgeschlagen (MBox-Importe) |
| **Failed** | Mit einem Fehler beendet |
| **Cancelled** | Durch einen Benutzer gestoppt |
| **Rate Limited** | *Sync pausiert aufgrund von Bandbreitenlimit. Wird automatisch fortgesetzt.* |
| **Timed Out** | Die Synchronisierung hat das konfigurierte Zeitlimit überschritten und wurde gestoppt |

> **ℹ️ "Rate Limited" und "Timed Out" sind nur bei der Synchronisierung auftretende Zustände.** Sie sehen wie Fehler aus, sind es aber nicht: Eine durch Rate Limiting pausierte Synchronisierung setzt sich von selbst fort, sobald das Bandbreitenkontingent es zulässt, und eine durch Timeout gestoppte Synchronisierung setzt beim nächsten Durchlauf an ihrem Checkpoint fort. Siehe [Rate Limit Handling](RateLimitHandling.md) und [Mail Synchronization](Synchronization.md).

### Warum der Fortschritt bei 0% verharren kann {#why-progress-can-sit-at-0}

Der Fortschritt wird anhand der **abgeschlossenen Ordner** gemessen, nicht anhand einzelner E-Mails:

> *Der Fortschritt wird anhand der abgeschlossenen Ordner gemessen. Ein großer Ordner (z. B. INBOX) hält den Balken bei 0%, bis er fertig ist.*

Bei einem Postfach, in dem der INBOX den Großteil der E-Mails enthält, kann der Balken lange bei 0% bleiben und dann plötzlich springen. Das ist normal — der Job arbeitet, er hat nur seinen ersten großen Ordner noch nicht fertiggestellt.

## 🔄 Die Seite aktualisiert sich selbst {#the-page-refreshes-itself}

Solange die Seite geöffnet ist, lädt sie alle **30 Sekunden** neu, sodass Sie sie in einem Tab offen lassen können, um den Fortschritt zu beobachten, ohne etwas klicken zu müssen. Das Label zeigt an, wann die Daten zuletzt aktualisiert wurden.

Das Neuladen wird abgebrochen, sobald Sie auf einen Link oder eine Schaltfläche klicken — andernfalls würde Ihr Klick auf einer Seite landen, die gerade unter Ihnen ersetzt wird.

**Konfigurierbar:** `Jobs:RefreshSeconds` in `appsettings.json` (Standard `30`).
Setzen Sie es auf `0`, um das automatische Neuladen zu deaktivieren. Jedes Neuladen baut die Seite aus allen Job-Quellen neu auf. Ein sehr kurzes Intervall bei einer Installation mit vielen Konten stellt eine echte Belastung dar — und diese Belastung vervielfacht sich mit jedem offenen Tab.

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 Abbrechen eines Jobs {#cancelling-a-job}

Jobs, die ein Abbrechen unterstützen, zeigen eine Schaltfläche **Cancel Job** an, die fragt:

> *Are you sure you want to cancel this job?*

Import-Jobs verwenden die Formulierung **Cancel Import** / *Are you sure you want to cancel this import job?*

Das Abbrechen ist für **Synchronisierungs-, Batch-Wiederherstellungs-, E-Mail-Export- und E-Mail-Lösch-Jobs** verfügbar. Der Job stoppt, sobald das aktuelle Element fertiggestellt ist; der Status wird zu **Cancelled**.

> **⚠️ Das Abbrechen ist kein Rollback.** Bereits importierte, wiederhergestellte, exportierte oder gelöschte E-Mails bleiben in diesem Zustand. Das Abbrechen verhindert nur, dass der Job *weiter* macht — es macht nicht rückgängig, was bereits getan wurde. Bei einem Lösch-Job bedeutet dies, dass die bereits entfernten E-Mails weg sind.

Konto- und MBox-Import-Jobs können nicht von dieser Seite aus abgebrochen werden; stoppen Sie diese stattdessen auf der Import-Seite des jeweiligen Kontos.

## ⚠️ Synchronisierungsfehler und "Acknowledge Failures" {#sync-failures-and-acknowledge-failures}

Wenn eine Synchronisierung mit dem Status **Failed** endet, bietet die Job-Zeile die Option **Acknowledge Failures** an. Dies ist die einzige Aktion auf der Seite, die das zukünftige Verhalten ändert, daher ist es wichtig, sie zu verstehen.

Jeder Synchronisierungsdurchlauf beginnt dort, wo der letzte aufgehört hat, unter Verwendung eines **Checkpoints**. Wenn eine Synchronisierung bei einigen E-Mails fehlschlägt, bleibt der Checkpoint hinter diesen E-Mails zurück, sodass der nächste Durchlauf versucht, sie erneut zu verarbeiten. Das ist der sichere Standard — nichts wird stillschweigend übersprungen.

Wenn die Fehler jedoch permanent sind (eine beschädigte Nachricht, ein Ordner, den der Server nicht bereitstellt), versucht jede nachfolgende Synchronisierung sie erneut, und die Synchronisierung schlägt immer an der gleichen Stelle fehl.

**Acknowledge Failures** löst diesen Stillstand, indem der Checkpoint über die fehlgeschlagenen E-Mails hinweg verschoben wird. Die Bestätigung ist explizit in Bezug auf die Konsequenzen:

> *This will advance the sync timestamp to the completion time of this job, so the next sync will be a normal incremental sync. The failed emails will not be retried. Continue?*

Im Erfolgsfall sehen Sie: *"Sync timestamp has been advanced. The next sync will be incremental."*

> **⚠️ Bestätigen Sie erst, wenn Sie sich die Fehler angesehen haben.** Nach der Bestätigung werden diese E-Mails **nicht** erneut versucht — sie werden im Archiv fehlen, es sei denn, Sie rufen sie auf anderem Weg ab. Überprüfen Sie zuerst die Fehlerdetails. Wenn die Ursache eine Fehlkonfiguration ist, beheben Sie diese, bevor Sie bestätigen. Siehe [Mail Synchronization](Synchronization.md) für das detaillierte Checkpoint-Verhalten.

Die Bestätigung wird im Audit-Protokoll als *SyncAcknowledgeFailures* mit der Anzahl der fehlgeschlagenen E-Mails und dem Konto aufgezeichnet, sodass sie später nachvollziehbar ist.

## 👁️ Was Sie hier nicht tun können {#what-you-cannot-do-here}

- **Kein Retry-Button.** Ein fehlgeschlagener Job wird von dieser Seite aus nicht neu gestartet — starten Sie den Vorgang erneut von dort, wo Sie ihn begonnen haben (Sync, Import, Export).
- **Keine Bearbeitung.** Die Job-Parameter sind bei der Erstellung fixiert.
- **Nur die letzten 20 pro Typ.** Ältere Jobs werden nicht aufgelistet. Für langfristige Aufzeichnungen sind das Audit-Protokoll und das Anwendungslog die Quellen — siehe [Access Logging](Logs.md) und [Docker Compose Logs](DockerComposeLogs.md).

## ❓ Fehlerbehebung {#troubleshooting}

| Symptom | Wahrscheinliche Ursache | Was zu tun ist |
|---|---|---|
| Job bleibt in **Queued** | Ein anderer Job desselben Workers läuft | Warten; andere Abschnitte nach einem lang laufenden Job durchsuchen |
| Fortschritt bei 0% festgefahren | Ein großer Ordner wird noch verarbeitet | Normal — siehe "Warum der Fortschritt bei 0% verharren kann" |
| Sync wiederholt **Failed** beim selben Konto | Permanente Fehler bei einzelnen E-Mails | Fehler prüfen, Ursache beheben, dann bestätigen (Acknowledge) |
| Sync zeigt **Rate Limited** | Bandbreitenkontingent erreicht | Nichts — es setzt sich automatisch fort |
| Sync zeigt **Timed Out** | Postfach größer als das Zeitlimit | Es setzt beim nächsten Durchlauf am Checkpoint fort; siehe [Synchronization](Synchronization.md) |
| **CompletedWithErrors** bei MBox-Import | Einige Nachrichten fehlerhaft | Die Zähler zeigen an, wie viele; der Rest wurde importiert |
| Job verschwindet nach Neuladen | Er ist aus dem "letzten 20"-Fenster gefallen | Prüfen Sie das Anwendungslog auf den finalen Status |
| Seite lädt neu während der Arbeit | Automatisches Neuladen alle 30 s | Setzen Sie `Jobs:RefreshSeconds` auf `0` |

## 📚 Verwandte Dokumentation {#related-documentation}

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — Checkpoints, Timeouts bei der Synchronisierung
- [Rate Limit Handling](RateLimitHandling.md) — Bandbreitenkontingente und automatisches Fortsetzen
- [Access Logging](Logs.md) — Wer hat was getan, einschließlich Job-Aktionen
- [Batch Restore](MailboxMigration.md) — Verschieben von E-Mails zwischen Postfächern
- [Date-Windowed Offload](Offload.md) — Offload-Jobs
- [Docker Compose Logs](DockerComposeLogs.md) — Lesen des Anwendungslogs
