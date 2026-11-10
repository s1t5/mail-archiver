# 📤 Date-Windowed Offload {#date-windowed-offload}

[← Zur Dokumentationsübersicht](index.md)

## 📋 Übersicht {#overview}

Ein Offload hängt archivierte E-Mails, die neuer als ein Grenzwert sind, an ein anderes Postfach an und lässt alles, was älter ist, im Archiv. Es dient für Migrationen, bei denen das neue Postfach nur aktuelle E-Mails enthalten soll, während die vollständige Historie über den Mail Archiver erreichbar bleibt.

Es unterscheidet sich in fünf Punkten von [Copy All Emails to Another Mailbox](MailboxMigration.md):

| | Copy All Emails | Offload |
|---|---|---|
| Datumsfilter | keiner, alles oder nichts | Grenzwert, mit optionaler Obergrenze |
| Wiederholbar | nein, hängt alles erneut an | ja, überspringt, was das Ziel bereits enthält |
| Ordner-Ausschlüsse | nein | ja, konfigurierbar |
| Ordner-Umbenennung | nein | ja, konfigurierbar |
| Vorschau | nein | Dry Run berichtet pro Ordner, ohne zu schreiben |

Der Offload hängt immer nur an das Ziel an. Er löscht nie etwas aus dem Archiv und berührt niemals das Quell-Postfach.

## 🔁 Ein wiederholter Durchlauf ist sicher {#repeating-a-run-is-safe}

Vor dem ersten Anhängen wird das gesamte Ziel-Postfach indiziert und jede Nachricht dagegen geprüft. Ein wiederholter Durchlauf hängt daher nichts an, was bereits angekommen ist, sondern meldet es stattdessen als bereits vorhanden. Das hat drei praktische Konsequenzen:

- Ein unterbrochener Durchlauf kann einfach durch erneutes Ausführen wiederhergestellt werden.
- Ein abgeschlossener Durchlauf kann als Verifizierungsdurchgang wiederholt werden. Ein korrektes Ergebnis bedeutet: nichts wurde angehängt und alles ist bereits vorhanden.
- Nachrichten, die im Ziel gelöscht wurden, kehren beim nächsten Durchlauf zurück, da sie nicht mehr im Index sind.

Zwei Kriterien entscheiden in dieser Reihenfolge, ob eine Nachricht bereits vorhanden ist:

1. **Message-ID**, normalisiert. Dies erledigt fast die gesamte Arbeit.
2. **Ein Fingerabdruck** über Absender, Empfänger, Betreff und Sendezeit innerhalb von zwei Sekunden. Dies deckt Nachrichten ab, deren gespeicherte Message-ID fehlt oder fehlerhaft ist. Solche Nachrichten können überhaupt nicht mit ihrer ursprünglichen Message-ID wiederhergestellt werden, daher würden sie ohne dieses zweite Kriterium bei jedem einzelnen Durchlauf dupliziert werden.

Der Job meldet separat, wie viele Treffer durch den Fingerabdruck erzielt wurden, da dies das Kriterium ist, das stillschweigend fehlschlagen könnte.

## ⚙️ Konfiguration {#configuration}

Ein neuer `Offload`-Abschnitt mit Standardwerten, die das vorherige Verhalten reproduzieren:

```json
"Offload": {
  "MaxConcurrentJobs": 1,
  "PrefetchMaxMessages": 500000,
  "ExcludedSourceFolders": [],
  "FolderRenameMap": {},
  "MarkAsSeen": true
}
```

| Key | Default | Purpose |
|---|---|---|
| `MaxConcurrentJobs` | `1` | Wie viele Wiederherstellungs- oder Offload-Jobs gleichzeitig laufen dürfen. Ein Wert von 1 behält das streng serielle Verhalten bei, das die Job-Warteschlange immer hatte. Unabhängig von diesem Wert läuft maximal ein Job pro Ziel-Postfach. |
| `PrefetchMaxMessages` | `500000` | Obergrenze für die Anzahl der Nachrichten, die aus einem Ziel-Postfach indiziert werden. Darüber hinaus wird die Duplikatprüfung auf einen einzelnen Ordner eingeschränkt und dies protokolliert. |
| `ExcludedSourceFolders` | empty | Quell-Ordner, die nie ausgelagert werden. Wird vor der Umbenennung geprüft und umfasst Unterordner. |
| `FolderRenameMap` | empty | Schreibt die führenden Segmente eines Quell-Ordnerpfads um. |
| `MarkAsSeen` | `true` | Ob angehängte E-Mails als gelesen markiert werden sollen. |

Beide Ordner-Einstellungen sind absichtlich leer. Das Umbenennen oder Verwerfen von Ordnern ohne explizite Anweisung würde jeden überraschen, der bereits den Wiederherstellungspfad nutzt; ein Durchlauf ohne Konfiguration migriert alles, einschließlich Spam-Ordner, und erstellt neben den eigenen Ordnern des Ziels einen zweiten Satz spezieller Ordner.

### Ordner-Ausschlüsse und Umbenennung {#folder-exclusions-and-renaming}

Für eine Migration von Exchange zu Dovecot sehen die beiden Einstellungen normalerweise so aus:

```json
"ExcludedSourceFolders": ["Junk E-Mail", "Deleted Items"],
"FolderRenameMap": {
  "Sent Items": "Sent",
  "Deleted Items": "Trash",
  "Junk E-Mail": "Junk"
}
```

Ohne die Rename-Map endet jedes migrierte Postfach mit sowohl `Sent` als auch `Sent Items`, da Dovecot seine eigenen Special-Use-Ordner vorab erstellt.

Vier Punkte zur Anwendung sind nennenswert, da jeder einzelne eine potenzielle Überraschung darstellt:

1. **Ausschlüsse werden zuerst abgeglichen**, gegen den Quellnamen. Wenn die Umbenennung zuerst erfolgt wäre, würde ein Ausschluss für `Deleted Items` niemals greifen, sobald dieser bereits zu `Trash` umbenannt wurde.
2. **Die Umbenennung schreibt den längsten passenden Pfadpräfix um**, an Segmentgrenzen. Wenn `Sent Items` zu `Sent` wird, wird auch `Sent Items/2019` zu `Sent/2019`, während `Sent Items Archive` unverändert bleibt.
3. **Der Abgleich ist nicht fallsensitiv**, und sowohl `/` als auch `\` zählen als Trennzeichen.
4. **Zwei Quell-Ordner können auf ein Ziel kollabieren.** Wenn `Sent Items` und `Sent` beide zu `Sent` werden, ist das in Ordnung und öffnet den Ziel-Ordner nicht doppelt.

Das Ändern der Rename-Map zwischen zwei Durchläufen dupliziert nichts, da die Duplikatprüfung das gesamte Ziel-Postfach abdeckt und nicht nur einen aufgelösten Ordner.

## 🖥️ Ausführung über die Benutzeroberfläche {#running-it-from-the-interface}

Auf der Detailseite eines Kontos öffnet **Offload to Another Mailbox** ein Formular mit dem Ziel-Postfach, dem Ziel-Stammordner, der Option zur Beibehaltung der Ordnerstruktur, dem Datumsfenster, einem Dry-Run-Schalter und der Option, angehängte E-Mails als gelesen zu markieren. Die konfigurierten Ausschlüsse und die Rename-Map werden schreibgeschützt angezeigt, sodass ein Durchlauf nie eine Überraschung ist.

Der Job wird in die Warteschlange gestellt, und sein Fortschritt sowie der Bericht pro Ordner erscheinen auf der Job-Statusseite und unter **Jobs**. Der Bericht pro Ordner nennt die Quell-Ordner, daher wird er nur dem Benutzer angezeigt, der den Job gestartet hat, sowie den Administratoren.

Das Ziel-Postfach kann ein deaktiviertes IMAP-Konto sein, z. B. um ein Migrationsziel bereitzustellen, bevor es live geht; der Durchlauf schlägt beim Verbindungsaufbau lautstark fehl, wenn das Ziel nicht erreichbar ist. Microsoft 365 Konten können keine Offload-Ziele sein; der Graph-Wiederherstellungspfad bleibt unverändert.

### Wer darf es ausführen {#who-may-run-it}

Administratoren können zwischen beliebigen zwei Konten auslagern. Ein Self-Manager kann **nur zwischen den ihm zugewiesenen Konten** auslagern, sowohl am Anfang als auch am Ende: an der Quelle, da die Kontoseite bereits so eingeschränkt ist, und am Ziel, da die Zielliste auf denselben Satz eingeschränkt wird und die Anfrage beim Start des Jobs erneut dagegen geprüft wird. Ein Self-Manager mit nur einem zugewiesenen Konto hat daher kein geeignetes Ziel und wird entsprechend informiert.

Die Einschränkung (Scoping) stammt von `IAccountAccessResolver`, demselben Resolver, den die REST API und der MCP-Server verwenden, sodass es eine Definition davon gibt, wer welches Postfach nutzen darf. Die Entscheidung selbst liegt in `OffloadTargetEligibility`, die sowohl die Zielliste als auch die Anfrageprüfung aufrufen, sodass das, was das Formular anbietet und was der Server akzeptiert, nicht auseinanderdriften kann.

## ⌨️ Ausführung über die Befehlszeile {#running-it-from-the-command-line}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll \
  --offload --source-account-id 3 --target-account-id 7 \
  --since 2025-08-01 \
  [--until 2026-08-01] [--target-folder INBOX] \
  [--preserve-folders] [--dry-run] [--no-mark-seen]
```

| Argument | Bedeutung |
|---|---|
| `--source-account-id` | Konto, von dem archivierte E-Mails gelesen werden sollen. Erforderlich. |
| `--target-account-id` | IMAP-Konto, in das angehängt wird. Erforderlich und muss sich von der Quelle unterscheiden. |
| `--since` | Inklusive Untergrenze für das Sendedatum, `YYYY-MM-DD`. Erforderlich. |
| `--until` | Optionale inklusive Obergrenze. |
| `--target-folder` | Stammordner im Ziel-Postfach. Standardmäßig `INBOX`. |
| `--preserve-folders` | Die Quell-Ordnerstruktur unter dem Stammordner rekonstruieren. |
| `--dry-run` | Berichtet, was passieren würde, ohne etwas anzuhängen. |
| `--no-mark-seen` | Anhängen ohne das Seen-Flag. |

Exit-Codes:

| Code | Bedeutung |
|---|---|
| `0` | Alles wurde angehängt oder war bereits vorhanden. |
| `1` | Mindestens eine Nachricht ist fehlgeschlagen oder der Durchlauf selbst ist fehlgeschlagen. |
| `2` | Der Aufruf war falsch: ungültige oder fehlende Argumente, unbekanntes Konto, Ziel ist kein IMAP, Quelle ist gleich Ziel. |

Ein Aufruf verarbeitet ein Postfach, was eine Flotte von Postfächern über eine CSV mit Quelle, Ziel und Grenzwert skriptbar macht.

> **Mehrere gleichzeitig ausführen.** Die Regel "ein Job pro Ziel" gilt nur innerhalb eines einzelnen Prozesses. Zwei gleichzeitige Befehlszeilen-Aufrufe gegen dasselbe Ziel-Postfach könnten daher jeweils ihren Duplikat-Index nehmen, bevor der andere mit dem Schreiben beginnt, und könnten dann beide dieselbe E-Mail anhängen. Partitionieren Sie die Arbeit so, dass jeder gleichzeitige Aufruf ein anderes Ziel hat, was der Normalfall ist, wenn jede Zeile einer CSV ein anderes Postfach ist.

## 🗓️ Wie der Grenzwert interpretiert wird {#how-the-cutoff-is-interpreted}

- Der Filter bezieht sich auf **das Sendedatum**, nicht darauf, wann eine Nachricht archiviert wurde. Der Zeitstempel "archiviert am" ist die Zeit des Archivierungsdurchlaufs und hat keine Bedeutung für einen Grenzwert.
- Ein relatives Fenster wie "die letzten zwölf Monate" wird **zu einem festen Datum aufgelöst, wenn der Job erstellt wird**. Ein Job kann lange laufen und danach wiederholt werden, und er muss jedes Mal dieselben E-Mails auswählen; ein gespeichertes relatives Fenster würde driften.
- Grenzwerte werden in der **konfigurierten Zeitzone der Anzeige** interpretiert, der gleichen, die der Suchbildschirm anzeigt, und die Untergrenze ist inklusive ab 00:00 Uhr dieses Tages. Eine Obergrenze schließt den gesamten Tag ein. Relative Fenster ("die letzten N Monate") werden in dieser Zeitzone der Anzeige ebenfalls aufgelöst, passend zur Art, wie `SentDate` gespeichert wird.

### Bekannte Einschränkung {#known-limitation}

- Wenn der Duplikat-Index `Offload:PrefetchMaxMessages` erreicht, wird der Umfang reduziert und protokolliert, anstatt es gegen einen einzelnen Ordner erneut zu versuchen. Der `restrictToFolder`-Fallback, den der Index-Builder bietet, wird derzeit vom Offload-Pfad nicht verwendet; das Erhöhen des Limits ist das empfohlene Abhilfemaßnahme.

## 📋 Eine Migration, End-to-End {#a-migration-end-to-end}

1. Erstellen Sie die Ziel-Postfächer. Für viele gleichzeitig siehe den CSV-Masseneimport in [Account Import](Account-Import.md).
2. Konfigurieren Sie `Offload:ExcludedSourceFolders` und `Offload:FolderRenameMap`. Beide sind standardmäßig leer, daher migriert das Überspringen dieser Schritte Spam-Ordner und erstellt duplizierte spezielle Ordner.
3. Bestätigen Sie, dass jedes Quell-Postfach vollständig archiviert ist, bevor Sie es auslagern.
4. **Führen Sie für alles einen Dry Run durch.** Überprüfen Sie die Zählungen und die aufgelösten Zielordnernamen, bevor etwas geschrieben wird.
5. Realer Durchlauf. Beginnen Sie mit `MaxConcurrentJobs` auf 2 und erhöhen Sie diesen erst, nachdem Sie beobachtet haben, wie der Zielserver damit umgeht; Dovecot begrenzt gleichzeitige Verbindungen pro Benutzer und pro IP.
6. Wiederholen Sie den Durchlauf zur Verifizierung. Erwarten Sie, dass nichts angehängt wird und alles bereits vorhanden ist.
7. Deaktivieren Sie die Quell-Konten.

Der Durchsatz wird durch `BatchOperation:PauseBetweenEmailsMs` begrenzt, was standardmäßig 50 ms beträgt und somit maximal zwanzig Anhänge pro Sekunde zulässt, bevor die Serverlatenz berücksichtigt wird.

## 🔍 Was die Zähler bedeuten {#what-the-counters-mean}

| Zähler | Bedeutung |
|---|---|
| Appended | An das Ziel geschrieben. Bei einem Dry Run: was geschrieben worden wäre. |
| Already present | Das Ziel enthielt es bereits, basierend auf einem der beiden Kriterien. |
| of those by fingerprint | Wie viele durch das zweite Kriterium statt durch die Message-ID erkannt wurden. |
| Excluded folder | Übersprungen, weil der Quell-Ordner ausgeschlossen ist. |
| Failed | Konnte nicht angehängt werden. |

Eine Warnung wird gemeldet, wenn der Duplikat-Index nicht das gesamte Ziel-Postfach abdeckt, was oberhalb von `PrefetchMaxMessages` oder wenn ein Ordner nicht gelesen werden konnte, der Fall ist. In diesem Zustand kann ein wiederholter Durchlauf E-Mails anhängen, die in den Teilen, die nicht indiziert wurden, bereits vorhanden sind.

## ⚠️ Hinweise und Limits {#notes-and-limits}

- Restaurierte E-Mails tragen nun ihr **ursprüngliches Lieferdatum** als IMAP-internes Datum, abgeleitet von der `Received`-Kette der Nachricht und mit Fallback auf das Sendedatum. Zuvor trugen sie das Datum des Archivierungsdurchlaufs, was die serverseitige Sortierung nach Ankunft und jede altersbasierte Regel im Ziel unterbrach.
- Da ein wiederholter Durchlauf das überspringt, was bereits vorhanden ist, kann ein falsches internes Datum nicht durch erneutes Ausführen korrigiert werden: Die Nachrichten werden erkannt und übersprungen. Stellen Sie dies vor einem produktiven Durchlauf korrekt ein oder löschen Sie die betroffenen Nachrichten im Ziel und führen Sie den Prozess erneut aus.
- Microsoft 365 wird nicht als Offload-Ziel unterstützt.
- Die Duplikat-Erkennung kann nur erkennen, was sie im Ziel-Postfach sehen kann. E-Mails, die das Ziel auf einem anderen Weg erreicht haben, werden nur insoweit erfasst, als die beiden Kriterien zufällig darauf passen.
