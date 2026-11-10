# ⚙️ Hintergrundprozesse {#background-jobs}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Die meiste Arbeit in Mail Archiver findet nicht statt, während Sie warten. Die Synchronisierung eines Postfachs, das Importieren einer MBOX-Datei, die Wiederherstellung von Tausenden von E-Mails oder das Exportieren eines Archivs kann Minuten bis Stunden dauern. Diese Aufgaben laufen als **Hintergrundjobs** – Sie starten sie, und sie laufen weiter, selbst wenn Sie den Browser schließen.

Die Seite **Background Jobs** zeigt an, was derzeit läuft, was abgeschlossen wurde und was schiefgelaufen ist. Dies ist die richtige Anlaufstelle, wenn etwas länger als erwartet dauert oder wenn eine Synchronisierung mit Fehlern endet.

> **ℹ️ Administratorseite.** Background Jobs erfordert Administratorrechte und ist über das Hauptmenü erreichbar. Reguläre Benutzer sehen diese Seite nicht – sie können zwar weiterhin Jobs starten (zum Beispiel eine Stapel-Wiederherstellung), aber die Überwachung erfolgt hier.

## 🗂️ Job-Typen {#job-types}

Die Seite gruppiert Jobs nach Typ. Jeder Abschnitt zeigt bis zu die **20 neuesten**
Jobs an, wobei laufende und in der Warteschlange befindliche Jobs nach oben sortiert werden – so wird verhindert, dass ein alter Job durch neuere aus dem Blickfeld geschoben wird.

| Abschnitt | Was er abdeckt | Gestartet von |
|---|---|---|
| **Synchronization Jobs** | Abrufen neuer E-Mails vom Mailserver | Automatischer Zeitplan oder *Sync now* bei einem Konto |
| **MBox Import Jobs** | Importieren von `.mbox` Dateien | Mail Accounts → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **EML Import Jobs** | Importieren von `.eml` Dateien / ZIP-Archiven | Mail Accounts → [Import EML](Import-EML-MBox.md#-import-eml) |
| **Batch Restore Jobs** | Kopieren ausgewählter E-Mails zurück in ein Postfach | Archiv → E-Mails auswählen → Batch Restore |
| **Account Export Jobs** | Exportieren eines gesamten Kontos (mbox / EML) | Mail Accounts → [Export](Export-Account.md) |
| **Selected Emails Export Jobs** | Exportieren einer handverlesenen Auswahl an E-Mails | Archiv → E-Mails auswählen → Export |
| **Email Deletion Jobs** | Löschen von E-Mails vom Server oder Archiv, einschließlich filterbasierter Massenlöschung | Aufbewahrungsrichtlinie, manuelle Löschung oder Archiv → *Delete all matches* |
| **Account Deletion Jobs** | Entfernen eines gesamten Mail-Kontos | Mail Accounts → Delete |
| **Offload** | Verschieben von E-Mails innerhalb eines Datumsfensters in ein anderes Konto | Mail Accounts → Offload |

## 📊 Lesen einer Job-Zeile {#reading-a-job-row}

Jede Zeile enthält denselben Satz an Spalten:

| Spalte | Bedeutung |
|---|---|
| **Job ID** | Kurze Kennung — zitieren Sie diese bei der Meldung eines Problems |
| **Account** | Das Postfach, an dem der Job arbeitet |
| **Status** | Aktueller Zustand, siehe Tabelle unten |
| **Progress** | Wie weit der Job fortgeschritten ist, mit verarbeitet/gesamt, sofern bekannt |
| **Created** | Wann der Job in die Warteschlange gestellt wurde |
| **Started** | Wann er tatsächlich mit der Ausführung begann |
| **Completed** | Wann er beendet wurde (leer, solange er noch läuft) |
| **Duration** | Vergangene Zeit |

Einige Jobtypen fügen der Progress-Spalte Zähler hinzu:

- **Success** / **Failed** — erfolgreich und erfolglos verarbeitete E-Mails
- **Skipped (duplicates)** — E-Mails, die bereits im Archiv vorhanden sind und nicht doppelt importiert werden
- **Recovered** — Nachrichten, die erneut abgerufen wurden, nachdem der Server meldete, dass sie fehlen
- **Malformed (skipped)** — Nachrichten, die nicht geparst werden konnten und ausgelassen wurden

### Statuswerte {#status-values}

| Status | Bedeutung |
|---|---|
| **Queued** | Wartet auf den Start — ein anderer Job läuft oder der Worker ist beschäftigt |
| **Running** | Wird derzeit ausgeführt |
| **Completed** | Erfolgreich abgeschlossen |
| **CompletedWithErrors** | Abgeschlossen, aber einige Elemente sind fehlgeschlagen (MBox-Importe) |
| **Failed** | Mit einem Fehler beendet |
| **Cancelled** | Vom Benutzer gestoppt |
| **Rate Limited** | *Synchronisierung aufgrund von Bandbreitenbeschränkung pausiert. Wird automatisch fortgesetzt.* |
| **Timed Out** | Die Synchronisierung hat das konfigurierte Zeitlimit überschritten und wurde gestoppt |

> **ℹ️ "Rate Limited" und "Timed Out" sind nur Synchronisierungs-Zustände.** Sie sehen aus wie
> Fehler, sind es aber nicht: Eine rate-limited Synchronisierung setzt sich von selbst fort, sobald
> das Bandbreitenkontingent dies zulässt, und eine timed-out Synchronisierung macht an ihrem
> Checkpoint beim nächsten Durchlauf weiter. Siehe [Rate Limit Handling](RateLimitHandling.md) und
> [Mail Synchronization](Synchronization.md).

### Warum der Fortschritt bei 0% verharren kann {#why-progress-can-sit-at-0}

Der Fortschritt wird an **abgeschlossenen Ordnern** gemessen, nicht an einzelnen E-Mails:

> *Der Fortschritt wird an abgeschlossenen Ordnern gemessen. Ein großer Ordner (z. B. INBOX) hält
> den Balken bei 0%, bis er fertiggestellt ist.*

In einem Postfach, in dem der INBOX den Großteil der E-Mails enthält, kann der Balken lange Zeit bei 0% bleiben und dann plötzlich springen. Das ist normal — der Job arbeitet, er hat nur seinen ersten großen Ordner noch nicht abgeschlossen.

## 🔄 Die Seite aktualisiert sich selbst {#the-page-refreshes-itself}

Solange die Seite geöffnet ist, wird sie alle **30 Sekunden** neu geladen, sodass Sie sie in einem Tab offen lassen und den Fortschritt beobachten können, ohne etwas anklicken zu müssen. Das Label zeigt an, wann die Daten zuletzt aktualisiert wurden.

Das Neuladen wird in dem Moment abgebrochen, in dem Sie auf einen Link oder eine Schaltfläche klicken – andernfalls würde Ihr Klick auf einer Seite landen, die gerade unter Ihnen ersetzt wird.

**Konfigurierbar:** `Jobs:RefreshSeconds` in `appsettings.json` (Standard `30`).
Setzen Sie es auf `0`, um das automatische Neuladen zu deaktivieren. Jedes Neuladen baut die Seite aus allen Job-Quellen neu auf, daher stellt ein sehr kurzes Intervall bei einer Installation mit vielen Konten eine echte Belastung dar – und diese Belastung vervielfacht sich mit jedem offenen Tab.

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 Abbrechen eines Jobs {#cancelling-a-job}

Jobs, die das Abbrechen unterstützen, zeigen eine Schaltfläche **Job abbrechen** an, mit der gefragt wird:

> *Sind Sie sicher, dass Sie diesen Job abbrechen möchten?*

Import-Jobs verwenden die Formulierung **Import abbrechen** / *Sind Sie sicher, dass Sie diesen Import-Job abbrechen möchten?*

Das Abbrechen ist für Jobs zur **Synchronisierung, Batch-Wiederherstellung, zum Export ausgewählter E-Mails und zur E-Mail-Löschung** verfügbar. Der Job stoppt, sobald das aktuelle Element abgeschlossen ist; der Status wird zu **Abgebrochen**.

> **⚠️ Das Abbrechen ist kein Rollback.** Bereits importierte, wiederhergestellte, exportierte oder gelöschte E-Mails bleiben in diesem Zustand. Das Abbrechen verhindert nur, dass der Job *weitere* Aktionen ausführt — es macht nicht rückgängig, was bereits getan wurde. Bei einem Lösch-Job bedeutet dies, dass die bereits entfernten E-Mails gelöscht sind.

Konto- und MBox-Import-Jobs können von dieser Seite aus nicht abgebrochen werden; stoppen Sie diese stattdessen über die eigene Import-Seite des Kontos.

## ⚠️ Synchronisierungsfehler und „Acknowledge Failures“ {#sync-failures-and-acknowledge-failures}

Wenn eine Synchronisierung mit dem Status **Failed** endet, bietet die Job-Zeile die Option **Acknowledge Failures**. Dies ist die einzige Aktion auf der Seite, die das zukünftige Verhalten ändert, daher ist es wichtig, dies zu verstehen.

Jeder Synchronisierungsdurchlauf beginnt dort, wo der letzte aufgehört hat, indem er einen **checkpoint** verwendet. Wenn eine Synchronisierung für einige E-Mails fehlschlägt, bleibt der checkpoint hinter diesen E-Mails zurück, sodass der nächste Durchlauf versucht, sie erneut zu verarbeiten. Das ist die sichere Standardeinstellung – nichts wird stillschweigend übersprungen.

Wenn die Fehler jedoch permanent sind (eine beschädigte Nachricht, ein Ordner, den der Server nicht bereitstellt), versucht jeder nachfolgende Synchronisierungsdurchlauf sie erneut, und die Synchronisierung schlägt immer an derselben Stelle fehl.

**Acknowledge Failures** löst diesen Deadlock, indem der checkpoint über die fehlgeschlagenen E-Mails hinaus vorrückt. Die Bestätigung ist explizit hinsichtlich der Konsequenzen:

> *This will advance the sync timestamp to the completion time of this job, so
> the next sync will be a normal incremental sync. The failed emails will not be
> retried. Continue?*

Im Erfolgsfall sehen Sie: *"Sync timestamp has been advanced. The next sync will be
incremental."*

> **⚠️ Bestätigen Sie erst, nachdem Sie sich die Fehler angesehen haben.** Nach
> der Bestätigung werden diese E-Mails **nicht** erneut versucht – sie werden im
> Archiv fehlen, es sei denn, Sie rufen sie auf anderem Weg ab. Überprüfen Sie
> zuerst die Fehlerdetails; wenn die Ursache eine Fehlkonfiguration ist, beheben Sie
> diese, bevor Sie bestätigen. Weitere Informationen zum Verhalten des
> checkpoints finden Sie unter [Mail Synchronization](Synchronization.md).

Das Bestätigen wird im Zugriffsprotokoll als *SyncAcknowledgeFailures* mit der Anzahl der fehlgeschlagenen E-Mails und dem Konto aufgezeichnet, sodass es später nachvollziehbar ist.

## 👁️ Was Sie hier nicht tun können {#what-you-cannot-do-here}

- **Kein Retry-Button.** Ein fehlgeschlagener Job wird von dieser Seite aus nicht neu gestartet — starten Sie den Vorgang (Synchronisierung, Import, Export) erneut dort, wo Sie ihn begonnen haben.
- **Kein Bearbeiten.** Job-Parameter sind bei der Erstellung fest vorgegeben.
- **Nur die letzten 20 pro Typ.** Ältere Jobs werden nicht aufgelistet. Für langfristige Aufzeichnungen sind das Access-Log und das Application-Log die Quellen — siehe [Access Logging](Logs.md) und [Docker Compose Logs](DockerComposeLogs.md).

## ❓ Fehlerbehebung {#troubleshooting}

| Symptom | Wahrscheinliche Ursache | Was zu tun ist |
|---|---|---|
| Job bleibt auf **Queued** | Ein anderer Job desselben Workers läuft gerade | Warten; prüfen Sie andere Abschnitte auf einen lang laufenden Job |
| Fortschritt hängt bei 0% | Ein großer Ordner wird noch verarbeitet | Normal — siehe „Warum der Fortschritt bei 0% verharren kann“ |
| Synchronisierung bei demselben Konto wiederholt **Failed** | Permanente Fehler bei einzelnen E-Mails | Fehler untersuchen, Ursache beheben und dann bestätigen |
| Synchronisierung zeigt **Rate Limited** | Bandbreitenkontingent erreicht | Nichts — sie wird automatisch fortgesetzt |
| Synchronisierung zeigt **Timed Out** | Postfach größer als das Zeitlimit | Sie setzt beim nächsten Durchlauf am Checkpoint fort; siehe [Synchronization](Synchronization.md) |
| **CompletedWithErrors** bei einem MBox-Import | Einige Nachrichten sind fehlerhaft formatiert | Die Zähler zeigen an, wie viele; der Rest wurde importiert |
| Job verschwindet nach dem Neuladen | Er ist aus dem „letzten 20“-Fenster gefallen | Überprüfen Sie das Anwendungsprotokoll auf den finalen Status |
| Seite lädt während der Arbeit neu | Automatisches Aktualisieren alle 30 s | Setzen Sie `Jobs:RefreshSeconds` auf `0` |

## 📚 Verwandte Dokumentation {#related-documentation}

- [Mail Synchronisierung (Schnelle vs. Vollständige Synchronisierung)](Synchronization.md) — Checkpoints, zeitüberschreitende Synchronisierungen
- [Rate Limit Handling](RateLimitHandling.md) — Bandbreitenkontingente und automatische Fortsetzung
- [Access Logging](Logs.md) — wer was getan hat, einschließlich Job-Aktionen
- [Batch Restore](MailboxMigration.md) — Verschieben von E-Mails zwischen Postfächern
- [Date-Windowed Offload](Offload.md) — Offload-Jobs
- [Docker Compose Logs](DockerComposeLogs.md) — Lesen des Anwendungsprotokolls
