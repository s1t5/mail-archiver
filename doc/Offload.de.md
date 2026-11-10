# 📤 Date-Windowed Offload {#date-windowed-offload}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Ein Offload fügt archivierte E-Mails, die neuer als ein Stichtag sind, einem anderen Postfach hinzu und lässt alles, was älter ist, im Archiv. Es dient für Migrationen, bei denen das neue Postfach nur aktuelle E-Mails enthalten soll, während die vollständige Historie über den Mail Archiver erreichbar bleibt.

Es unterscheidet sich in fünf Punkten von [Copy All Emails to Another Mailbox](MailboxMigration.md):

| | Copy All Emails | Offload |
|---|---|---|
| Datumfilter | keiner, alle oder nichts | Stichtag, mit optionaler Obergrenze |
| Wiederholbar | nein, fügt alles erneut hinzu | ja, überspringt, was das Ziel bereits enthält |
| Ordner-Ausschlüsse | nein | ja, konfigurierbar |
| Ordner-Umbenennung | nein | ja, konfigurierbar |
| Vorschau | nein | Dry Run berichtet pro Ordner, ohne zu schreiben |

Der Offload fügt immer nur dem Ziel hinzu. Er löscht niemals aus dem Archiv und berührt niemals das Quell-Postfach.

## 🔁 Ein erneuter Durchlauf ist sicher {#repeating-a-run-is-safe}

Vor dem ersten Anfügen wird das gesamte Ziel-Postfach indiziert und jede Nachricht mit diesem abgeglichen. Ein wiederholter Durchlauf fügt daher nichts hinzu, was bereits angekommen ist, sondern meldet dies stattdessen als bereits vorhanden. Das hat drei praktische Konsequenzen:

- Ein unterbrochener Durchlauf kann durch einen erneuten Start wiederhergestellt werden.
- Ein abgeschlossener Durchlauf kann als Verifizierungslauf wiederholt werden. Ein korrektes Ergebnis bedeutet, dass nichts angehängt wurde und alles bereits vorhanden ist.
- Nachrichten, die im Ziel gelöscht wurden, kehren beim nächsten Durchlauf zurück, da sie nicht mehr im Index enthalten sind.

Zwei Kriterien entscheiden darüber, ob eine Nachricht bereits vorhanden ist, in dieser Reihenfolge:

1. **Message-ID**, normalisiert. Dies erledigt fast die gesamte Arbeit.
2. **Ein Fingerabdruck** über Absender, Empfänger, Betreff und Sendezeit innerhalb von zwei Sekunden. Dies deckt die Nachrichten ab, deren gespeicherte Message-ID fehlt oder fehlerhaft ist. Solche Nachrichten können überhaupt nicht mit ihrer ursprünglichen Message-ID wiederhergestellt werden, sodass sie ohne dieses zweite Kriterium bei jedem einzelnen Durchlauf dupliziert würden.

Der Job meldet separat, wie viele Treffer durch den Fingerabdruck erzielt wurden, da dies das Kriterium ist, das stillschweigend fehlschlagen könnte.

## ⚙️ Konfiguration {#configuration}

Ein neuer `Offload`-Abschnitt mit Standardwerten, die das bisherige Verhalten reproduzieren:

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
| `MaxConcurrentJobs` | `1` | Wie viele Wiederherstellung oder Offload-Jobs gleichzeitig laufen dürfen. Ein Wert von 1 behält das streng serielle Verhalten bei, das die Job-Warteschlange immer hatte. Unabhängig von diesem Wert läuft maximal ein Job pro Ziel-Postfach. |
| `PrefetchMaxMessages` | `500000` | Obergrenze für die Anzahl der Nachrichten, die aus einem Ziel-Postfach indiziert werden. Darüber wird die Duplikatprüfung auf einen einzelnen Ordner eingegrenzt und protokolliert, dass dies geschehen ist. |
| `ExcludedSourceFolders` | empty | Quell-Ordner werden niemals ausgelagert. Sie werden vor dem Umbenennen abgeglichen und decken Unterordner ab. |
| `FolderRenameMap` | empty | Schreibt die führenden Segmente eines Quell-Ordnerpfads um. |
| `MarkAsSeen` | `true` | Ob angehängte E-Mails als gelesen markiert werden. |

Beide Ordner-Einstellungen sind absichtlich leer. Das Umbenennen oder Löschen von Ordnern ohne explizite Anforderung würde jeden überraschen, der bereits den Wiederherstellungspfad nutzt; daher migriert ein Durchlauf ohne Konfiguration alles, einschließlich Spam-Ordner, und erstellt einen zweiten Satz spezieller Ordner neben den eigenen des Ziels.

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

Ohne die Umbenennungs-Map endet jedes migrierte Postfach mit sowohl `Sent` als auch `Sent Items`, da Dovecot seine eigenen Special-Use-Ordner vorinstalliert.

Vier Dinge über die Anwendung sind wichtig zu wissen, da jedes davon eine Möglichkeit ist, überrascht zu werden:

1. **Ausschlüsse werden zuerst abgeglichen**, gegen den Quellnamen. Wenn die Umbenennung zuerst erfolgt wäre, würde ein Ausschluss für `Deleted Items` niemals greifen, sobald dieser bereits zu `Trash` geworden ist.
2. **Die Umbenennung schreibt den längsten passenden Pfadpräfix um**, an Segmentgrenzen. `Sent Items` wird zu `Sent`, was auch `Sent Items/2019` in `Sent/2019` umwandelt, und lässt `Sent Items Archive` unverändert.
3. **Der Abgleich ist nicht fallsensitiv**, und sowohl `/` als auch `\` zählen als Trennzeichen.
4. **Zwei Quell-Ordner können auf einen Ziel-Ordner kollabieren.** `Sent Items` und `Sent`, die beide zu `Sent` werden, sind in Ordnung und öffnen den Ziel-Ordner nicht doppelt.

Das Ändern der Umbenennungs-Map zwischen zwei Durchläufen dupliziert nichts, da die Duplikatprüfung das gesamte Ziel-Postfach abdeckt, anstatt nur einen aufgelösten Ordner.

## 🖥️ Ausführung über die Benutzeroberfläche {#running-it-from-the-interface}

Auf der Detailseite eines Kontos öffnet **Offload to Another Mailbox** ein Formular mit dem Ziel-Postfach, dem Ziel-Stammordner, der Option zur Beibehaltung der Ordnerstruktur, dem Datumsfenster, einem Dry-Run-Schalter und der Option, angehängte E-Mails als gelesen zu markieren. Die konfigurierten Ausschlüsse und die Umbenennungs-Map werden schreibgeschützt angezeigt, sodass ein Durchlauf niemals eine Überraschung ist.

Der Job wird in eine Warteschlange gestellt; sein Fortschritt und der Bericht pro Ordner erscheinen auf der Job-Statusseite und unter **Jobs**. Der Bericht pro Ordner nennt die Quellordner, daher wird er nur dem Benutzer angezeigt, der den Job gestartet hat, sowie Administratoren.

Das Ziel-Postfach kann ein deaktiviertes IMAP-Konto sein, z. B. um ein Migrationsziel bereitzustellen, bevor es live geht; der Durchlauf schlägt bei der Verbindung mit einer Fehlermeldung fehl, wenn das Ziel nicht erreichbar ist. Microsoft 365 Konten können keine Offload-Ziele sein; der Graph restore Pfad bleibt unverändert.

### Wer darf es ausführen {#who-may-run-it}

Administratoren können zwischen beliebigen zwei Konten offloaden. Ein Self-Manager kann **nur zwischen den Konten offloaden, die ihm zugewiesen sind**, an beiden Enden: der Quelle, da die Kontoseite bereits so eingeschränkt ist, und dem Ziel, da die Zielliste auf denselben Satz eingegrenzt ist und die Anfrage beim Start des Jobs erneut dagegen geprüft wird. Ein Self-Manager mit nur einem zugewiesenen Konto hat daher kein berechtigtes Ziel und wird entsprechend informiert.

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
| `--source-account-id` | Konto, aus dem archivierte E-Mails gelesen werden sollen. Erforderlich. |
| `--target-account-id` | IMAP-Konto, in das angehängt werden soll. Erforderlich und muss sich von der Quelle unterscheiden. |
| `--since` | Inklusive untere Schranke für das Sendedatum, `YYYY-MM-DD`. Erforderlich. |
| `--until` | Optionale inklusive obere Schranke. |
| `--target-folder` | Stammordner im Ziel-Postfach. Standardmäßig `INBOX`. |
| `--preserve-folders` | Die Ordnerstruktur der Quelle unterhalb des Stammordners rekonstruieren. |
| `--dry-run` | Berichtet, was passieren würde, ohne etwas anzuhängen. |
| `--no-mark-seen` | Anhängen ohne das Seen-Flag. |

Exit-Codes:

| Code | Bedeutung |
|---|---|
| `0` | Alles wurde angehängt oder war bereits vorhanden. |
| `1` | Mindestens eine Nachricht ist fehlgeschlagen oder der Durchlauf selbst ist fehlgeschlagen. |
| `2` | Der Aufruf war fehlerhaft: ungültige oder fehlende Argumente, unbekanntes Konto, Ziel ist kein IMAP, Quelle ist gleich dem Ziel. |

Ein Aufruf verarbeitet ein Postfach, was eine Flotte von Postfächern anhand einer CSV mit Quelle, Ziel und Stichtag skriptbar macht.

> **Gleichzeitiges Ausführen mehrerer Aufgaben.** Die Regel „Ein Job pro Ziel“ gilt nur innerhalb eines einzelnen Prozesses, daher könnten zwei gleichzeitige Befehlszeilen-Aufrufe gegen dasselbe Ziel-Postfach jeweils ihren Duplikat-Index abrufen, bevor der andere mit dem Schreiben beginnt, und könnten dann beide dieselbe E-Mail anhängen. Partitionieren Sie die Arbeit so, dass jeder gleichzeitige Aufruf ein anderes Ziel hat, was der Normalfall ist, wenn jede Zeile einer CSV ein anderes Postfach ist.

## 🗓️ Wie der Cutoff interpretiert wird {#how-the-cutoff-is-interpreted}

- Der Filter bezieht sich auf **das Sendedatum**, nicht darauf, wann eine Nachricht archiviert wurde. Der Zeitstempel „archiviert am“ ist der Zeitpunkt des Archivierungslaufs und hat keine Bedeutung für einen Cutoff.
- Ein relativer Zeitraum wie „die letzten zwölf Monate“ wird **auf ein festes Datum aufgelöst, wenn der Job erstellt wird**. Ein Job kann lange laufen und danach wiederholt werden, und er muss jedes Mal dieselben E-Mails auswählen; ein gespeicherter relativer Zeitraum würde sich verschieben.
- Cutoffs werden in der **konfigurierten Anzeige-Zeitzone** interpretiert, der gleichen, die der Suchbildschirm anzeigt, und die untere Grenze ist ab 00:00 Uhr dieses Tages inklusive. Eine obere Grenze schließt den gesamten Tag ein. Relative Zeiträume („die letzten N Monate“) werden ebenfalls in dieser Anzeigenzeitzone aufgelöst, passend dazu, wie `SentDate` gespeichert wird.

### Bekannte Einschränkung {#known-limitation}

- Wenn der Duplikat-Index `Offload:PrefetchMaxMessages` erreicht, wird der Umfang reduziert und protokolliert, anstatt einen Versuch gegen einen einzelnen Ordner zu unternehmen. Der `restrictToFolder`-Fallback, den der Index-Builder anbietet, wird derzeit vom Offload-Pfad nicht verwendet; das Erhöhen des Limits ist das unterstützte Abhilfemaß.

## 📋 Eine Migration, von Anfang bis Ende {#a-migration-end-to-end}

1. Erstellen Sie die Ziel-Postfächer. Informationen zur Massenbearbeitung vieler Postfächer gleichzeitig finden Sie im CSV-Stapelimport unter
   [Account Import](Account-Import.md).
2. Konfigurieren Sie `Offload:ExcludedSourceFolders` und `Offload:FolderRenameMap`. Beide sind standardmäßig leer; das Überspringen dieses Schritts führt dazu, dass Spam-Ordner migriert werden und duplizierte Sonderordner erstellt werden.
3. Bestätigen Sie, dass jedes Quell-Postfach vollständig archiviert ist, bevor Sie es auslagern.
4. **Führen Sie alles als Testlauf aus.** Überprüfen Sie die Zählungen und die aufgelösten Zielordnernamen, bevor etwas geschrieben wird.
5. Echter Durchlauf. Beginnen Sie mit `MaxConcurrentJobs` bei 2 und erhöhen Sie diesen Wert nur, nachdem Sie beobachtet haben, wie der Zielserver damit zurechtkommt; Dovecot begrenzt die gleichzeitigen Verbindungen pro Benutzer und pro IP.
6. Wiederholen Sie den Durchlauf zur Überprüfung. Es sollte nichts angehängt werden, das nicht bereits vorhanden ist.
7. Deaktivieren Sie die Quell-Konten.

Der Durchsatz wird durch `BatchOperation:PauseBetweenEmailsMs` begrenzt, was standardmäßig 50 ms beträgt und somit maximal zwanzig Appends pro Sekunde zulässt, bevor die Serverlatenz berücksichtigt wird.

## 🔍 Bedeutung der Zähler {#what-the-counters-mean}

| Zähler | Bedeutung |
|---|---|
| Appended | An das Ziel geschrieben. Bei einem Testlauf: Was wäre geschrieben worden. |
| Already present | Das Ziel enthielt es bereits, basierend auf einem der Kriterien. |
| of those by fingerprint | Wie viele wurden durch das zweite Kriterium anstatt durch die Message-ID erkannt. |
| Excluded folder | Übersprungen, da der Quellordner ausgeschlossen ist. |
| Failed | Konnte nicht angehängt werden. |

Es wird eine Warnung ausgegeben, wenn der Duplikat-Index nicht das gesamte Ziel-Postfach abdeckt, was oberhalb von `PrefetchMaxMessages` oder wenn ein Ordner nicht gelesen werden konnte, der Fall ist. In diesem Zustand kann ein wiederholter Durchlauf E-Mails anhängen, die in den Teilen, die nicht indiziert wurden, bereits vorhanden sind.

## ⚠️ Hinweise und Einschränkungen {#notes-and-limits}

- Wiederhergestellte E-Mails tragen nun ihr **ursprüngliches Zustelldatum** als IMAP-internes Datum, das aus der `Received`-Kette der Nachricht entnommen wird und auf das Sendedatum zurückgreift. Zuvor wurde das Datum des Archivierungslaufs verwendet, was die serverseitige Sortierung nach Ankunftszeit und alle altersbasierten Regeln auf dem Ziel unterbrach.
- Da ein wiederholter Durchlauf bereits vorhandene Elemente überspringt, kann ein falsches internes Datum nicht durch erneutes Ausführen korrigiert werden: Die Nachrichten werden erkannt und übersprungen. Stellen Sie dies vor einem produktiven Durchlauf sicher oder löschen Sie die betroffenen Nachrichten auf dem Ziel und führen Sie den Vorgang erneut aus.
- Microsoft 365 wird nicht als Offload-Ziel unterstützt.
- Die Duplikaterkennung kann nur das erkennen, was sie im Ziel-Postfach sehen kann. E-Mails, die auf einem anderen Weg das Ziel erreicht haben, werden nur dann erfasst, wenn die beiden Kriterien zufällig übereinstimmen.
