# 🔍 Mail Search {#mail-search}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Die Suche ist das Herzstück eines E-Mail-Archivs. Mail Archiver durchsucht den **Volltext**
Ihrer archivierten E-Mails – Betreff, Textkörper, Absender und Empfänger – über alle
Konten hinweg, auf die Sie Zugriff haben, und liefert Ergebnisse in Millisekunden, selbst bei
Archiven mit Millionen von Nachrichten.

Dieser Leitfaden behandelt die Suchoberfläche, die Abfragesyntax, Filter, Sortierung,
Seitenumbruch und was zu tun ist, wenn Sie auf einen Ergebnissatz reagieren müssen.

> **ℹ️ Die Suche sieht nur das, was Sie auch sehen dürfen.** Ergebnisse werden durch Ihre
> Konto-Berechtigungen gefiltert. Ein Nicht-Administrator findet nur E-Mails aus den
> Postfächern, die ihm zugewiesen wurden – die Einschränkung wird direkt in der Abfrage
> angewendet, nicht in der Benutzeroberfläche.

## 🚀 Zugriff auf die Suche {#accessing-search}

1. Melden Sie sich an und wählen Sie **Archive** im Hauptmenü
2. Die Suchleiste befindet sich oberhalb der Ergebnisliste

Das Öffnen des Archivs ohne Suchbegriff listet Ihre aktuellsten E-Mails auf, die neuesten zuerst. Sie können von dort aus stöbern oder die Suche mit den Filtern eingrenzen.

## 🎯 Die Suchoberfläche {#the-search-interface}

| Feld | Funktion |
|---|---|
| **Suchbegriff** | Volltextabfrage — siehe [Query Syntax](#-query-syntax) unten |
| **Von** | Nur E-Mails, die an oder nach diesem Datum gesendet wurden |
| **An** | Nur E-Mails, die an oder vor diesem Datum gesendet wurden |
| **Richtung** | `All`, `Incoming` oder `Outgoing` |
| **Konto** | Auf ein einzelnes E-Mail-Konto beschränken |
| **Ordner** | Über den Ordnerbaum auf einen einzelnen Ordner beschränken |
| **Suchen** | Die Abfrage ausführen |
| **Zurücksetzen** | Alle Filter löschen |

Der Suchbegriff gleicht den **Betreff, den Textkörper, den Absender und die Empfänger** ab
(`From`, `To`, `Cc`, `Bcc`).

### Der Ordnerbaum {#the-folder-tree}

Ordner werden als **hierarchischer Baum** auf der linken Seite angezeigt, der die
Ordnerstruktur des Quell-Postfachs widerspiegelt — einschließlich untergeordneter Ordner.
Klicken Sie auf einen Ordner, um die Suche darauf zu beschränken, und verwenden Sie die
Ein-/Ausklappfunktion, um den Baum auszublenden, wenn Sie den Platz auf dem Bildschirm
benötigen.

Das Auswählen eines Ordners ändert Ihren Suchbegriff nicht; beide werden kombiniert.

## 🧩 Abfragesyntax {#query-syntax}

Die Syntax ist bewusst minimalistisch gehalten. Alles Folgende ist das, was die Anwendung
tatsächlich versteht.

### Einfache Wörter — und warum Präfixe funktionieren {#plain-words-and-why-prefixes-work}

Geben Sie ein oder mehrere Wörter ein; alle müssen vorhanden sein (eine **AND**-Suche):

```
invoice january
```

Findet E-Mails, die sowohl `invoice` als auch `january` in den durchsuchten Feldern enthalten.

> **💡 Teilwörter werden automatisch gematcht.** Jedes Wort wird als
> **Präfix** behandelt, sodass eine Suche nach `isenb` auch `isenboeck` und `isenböck` findet, und
> `rech` findet sowohl `Rechnung` als auch `Rechnungen`. Sie benötigen kein Platzhalterzeichen
> — es gibt keines zum Eintippen. Dies bedeutet auch, dass ein sehr kurzer Begriff
> viele Ergebnisse liefern kann; siehe [Performance](#-performance-and-large-archives).

Die Suche ist **nicht fallsensitiv**. `Invoice`, `invoice` und `INVOICE` sind die
gleiche Abfrage.

### Exakte Phrasen — Anführungszeichen {#exact-phrases-quotes}

Umschließen Sie Text mit doppelten Anführungszeichen, um zu verlangen, dass die Wörter
**nebeneinander** in dieser Reihenfolge stehen:

```
"important meeting"
```

Ohne Anführungszeichen würde `important meeting` auch eine E-Mail finden, in der die beiden
Wörter in nicht zusammenhängenden Sätzen erscheinen. Mit Anführungszeichen wird nur die direkt aufeinanderfolgende Phrase gematcht.

### Feldspezifische Suche {#field-specific-search}

Setzen Sie einen Feldnamen und einen Doppelpunkt vor einen Begriff, um **nur** in diesem Feld zu suchen:

| Syntax | Sucht in |
|---|---|
| `subject:invoice` | Nur Betreff |
| `from:john` | Nur Absender |
| `to:example.com` | Nur Empfänger |
| `body:important` | Nur Textkörper |

Feldnamen sind nicht fallsensitiv (`Subject:` funktioniert ebenfalls), und jedes kann
mit Anführungszeichen für eine Phrase kombiniert werden:

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ Vier Felder werden unterstützt:** `subject`, `body`, `from`, `to`. Das Schreiben von
> `cc:someone` oder `bcc:someone` ist **keine** Feltsuche — der Begriff wird als
> gewöhnlicher Text behandelt und über alle Felder hinweg durchsucht. Um einen Empfänger zu finden, der
> nur in Cc war, suchen Sie nach der Adresse oder dem Namen ohne Präfix.

### Alles kombinieren {#combining-everything}

Filter und Begriffe summieren sich. Diese Abfrage findet E-Mails von jedem Versicherungsabsender,
mit "car insurance" als exakter Phrase im Textkörper und "invoice" im
Betreff:

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

Praktische Kombinationen:

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### Zeichen, die ignoriert werden {#characters-that-are-ignored}

Der Abfrage-Parser entfernt die Zeichen `& | ! ( ) : *` von einzelnen Begriffen,
da diese in der zugrunde liegenden Volltext-Engine eine Bedeutung haben. Wenn Ihre Suche
unerwartet nichts zurückgibt, prüfen Sie, ob ein Sonderzeichen der Grund dafür ist.

## 📊 Sortierung und Paging {#sorting-and-paging}

### Sortierung {#sorting}

Klicken Sie auf einen Spaltenkopf, um zu sortieren. Sortierbare Spalten:

| Spalte | Sortiert nach |
|---|---|
| **Subject** | Betreffzeile, alphabetisch |
| **From** | Absender |
| **To** | Empfänger |
| **Date** | Sendedatum |

Ein erneuter Klick auf denselben Kopf kehrt die Richtung um. Standardmäßig wird **neueste zuerst** angezeigt.

### Seitengröße {#page-size}

Wählen Sie aus, wie viele Ergebnisse pro Seite angezeigt werden sollen:

**20** (Standard) · **50** · **75** · **100** · **150**

Eine größere Seite bietet mehr Kontext, benötigt aber länger zum Rendern. Verwenden Sie die Steuerelemente **Previous** / **Next**, um durch die Ergebnisseiten zu navigieren; die Anzahl der Ergebnisse gibt an, wie viele E-Mails insgesamt übereinstimmten.

> **ℹ️ Ihre Zeitzone gilt für die angezeigten Daten.** Die angezeigten Zeiten werden von UTC in Ihre konfigurierte Zeitzone umgerechnet, sodass das Datum, das Sie sehen, mit Ihrem lokalen Kalender übereinstimmt — nützlich, wenn Sie um Mitternacht suchen.

## 👁️ Arbeiten mit Ergebnissen {#working-with-results}

Jede Zeile zeigt das Konto, den Absender, den Betreff, das Datum und ein **📎 Büroklammer-Symbol**, wenn die E-Mail Anhänge hat. Die Büroklammer ist nur ein Indikator – sie ist kein Filter.

Klicken Sie auf **Details**, um eine E-Mail zu öffnen:

- Vollständiger Textkörper (HTML oder Reintext)
- **Anhänge**, aufgelistet mit der Option, jeden einzelnen herunterzuladen
- **Download as EML** — die vollständige Nachricht zur Verwendung an anderer Stelle
- **Restore** — die E-Mail zurück in ein aktives Postfach kopieren

Weitere Informationen zur Wiederherstellung von E-Mails in ein Postfach finden Sie unter [Mailbox Migration](MailboxMigration.md).

## ✅ Handeln mit vielen Ergebnissen gleichzeitig {#acting-on-many-results-at-once}

Ergebnisse können in großen Mengen ausgewählt und verarbeitet werden. Aktivieren Sie den **Selection Mode**, um Kontrollkästchen zu erhalten, und wählen Sie dann:

- **Select All** / **Deselect All** für die aktuelle Seite
- **Select all {n} matching** — jedes Ergebnis der aktuellen Suche über alle
  Seiten hinweg (wird angezeigt, wenn die Ergebnisse über mehr als eine Seite verteilt sind)
- **Copy Selected** — Wiederherstellung in ein Postfach
- **Export Selected** — als MBOX oder EML
- **Delete Selected** — aus dem Archiv

Der Zähler zeigt während der Arbeit *"{selected} von {total} ausgewählt"* an.

### Das Auswahllimit {#the-selection-limit}

> **⚠️ Es können maximal 250 E-Mails gleichzeitig ausgewählt werden** (konfigurierbar über
> `Selection:MaxSelectableEmails`). Darüber hinaus zeigt die Benutzeroberfläche *"Maximum
> selections reached (250)"* und **Too Many — Split Required** an.

Dieses Limit besteht, weil jeder Vorgang die betroffenen Nachrichten halten muss. Der
Hinweis *"Split larger operations into multiple batches"* ist der vorgesehene Workaround:
Arbeiten Sie die Ergebnisse in Gruppen von 250 oder weniger ab.

**Select all {n} matching** respektiert dasselbe Limit. Wenn die Suche mehr
E-Mails findet, als das Limit erlaubt, werden die ersten in der aktuellen Sortierung
ausgewählt und der Hinweis *"Maximum selections reached"* angezeigt. Verfeinern Sie die
Suche (Datumsbereich, Ordner, Konto), um den Rest gezielt zu finden.

Der Versuch, das Limit zu überschreiten, zeigt an: *"Too many emails selected. Maximum allowed is
250 emails. Please reduce your selection or split into multiple operations."*

### Löschen aller Treffer eines Filters {#deleting-every-match-of-a-filter}

Die Schaltfläche **Delete all matches** neben *Reset* löscht jede E-Mail, die
der aktuellen Suche entspricht, nicht nur die aktuelle Seite oder Auswahl. Sie steht
Administratoren und Selbstverwaltern zur Verfügung und wird als Hintergrundauftrag ausgeführt,
sodass das Auswahllimit von 250 E-Mails nicht gilt.

Der Bestätigungsdialog zeigt an, wie viele E-Mails löschbar sind und wie viele durch
die Aufbewahrungsrichtlinie gesperrt sind. Sie müssen diese Zahl eintippen, um die
Bestätigung zu aktivieren. Der Server zählt erneut nach und bricht ab, falls sich der
Ergebnissatz in der Zwischenzeit geändert hat, zum Beispiel weil eine Synchronisierung eine Mail hinzugefügt hat.

Gesperrte E-Mails werden durch diese Aktion niemals gelöscht; sie werden im
Job-Status als übersprungen gemeldet. `DeletionPolicy:MaxBulkDeleteEmails` (Standard 100.000) begrenzt einen
Job. Ein Filter, der mehr E-Mails findet, wird mit der Aufforderung zur Verfeinerung abgelehnt.

Der Job erscheint auf der Seite [Background Jobs](BackgroundJobs.md) und kann dort
abgebrochen werden.

### Direkte Verarbeitung vs. Hintergrundauftrag {#direct-processing-vs-background-job}

Für **Copy** und **Export** wählen Sie, wie die Arbeit ausgeführt wird:

| Option | Verhalten |
|---|---|
| **Process on this page (wait for completion)** | Der Browser wartet; gut für eine Handvoll E-Mails |
| **Process in background (monitor separately)** | Läuft als Job; Sie können den Browser schließen |

Exporte laufen immer im Hintergrund:

> *"The export will run in the background and you will be able to download the
> result from the Jobs page once completed."*

Finden Sie laufende und abgeschlossene Jobs auf der Seite [Background Jobs](BackgroundJobs.md).

> **⚠️ Löschen ist permanent.** Die Bestätigung ist explizit: *"Are you sure you
> want to permanently delete {n} selected email(s)? This action cannot be undone
> and each deletion will be logged for audit purposes."* Löschvorgänge werden
> im Zugriffsprotokoll aufgezeichnet.

## ⚡ Leistung und große Archive {#performance-and-large-archives}

Die Suche wird durch einen PostgreSQL-Volltextindex unterstützt, daher liegt der Unterschied zwischen einer schnellen und einer langsamen Suche meist darin, wie **selektiv** die Abfrage ist.

**Richtig:**

- Fügen Sie einen **Zeitraum** hinzu, wenn Sie ungefähr wissen, wann die E-Mail eingegangen ist — dies ist die effektivste Einschränkung
- Beschränken Sie die Suche auf ein **Konto** oder einen **Ordner**, wenn Sie wissen, wo die Mail liegt
- Verwenden Sie **mehrere Wörter** anstatt eines sehr kurzen Wortes
- Verwenden Sie Feldersuchen (`subject:`, `from:`), um unpassende Treffer auszuschließen

**Vermeiden Sie:**

- Ein einzelnes kurzes Präfix wie `re` — als Präfixsuche trifft es einen riesigen Teil Ihres Archivs
- Sehr weite Zeiträume in Kombination mit einem häufigen Wort
- Das Durchblättern vieler Seiten, wenn Sie stattdessen die Suche verfeinern könnten

> **💡 Wenn sich eine Suche langsam anfühlt, ist der Zeitraum meist die Lösung.** Die Beschränkung auf einen Monat verwandelt eine mehrsekündige Suche oft in eine sofortige Suche, da der Index den Großteil des Archivs überspringen kann.

## ❓ Fehlerbehebung {#troubleshooting}

| Symptom | Ursache | Was zu tun ist |
|---|---|---|
| Keine Ergebnisse, obwohl welche erwartet werden | Suchbegriff zu spezifisch oder Tippfehler | Weniger Wörter verwenden; Rechtschreibung prüfen |
| `cc:name` findet nichts Nützliches | `cc` ist kein unterstütztes Feld | Suchen Sie den Namen ohne Präfix |
| Ein Sonderzeichen unterbricht die Abfrage | `& \| ! ( ) : *` werden entfernt | Das Zeichen entfernen oder ersetzen |
| Zu viele Ergebnisse | Häufiges Wort oder kurzes Präfix | Datumsbereich oder Feldesuche hinzufügen |
| *"Too Many — Split Required"* | Mehr als 250 E-Mails ausgewählt | Auswahl reduzieren oder Vorgang aufteilen |
| Ein Export fehlt | Er läuft als Hintergrundjob | Prüfen Sie [Background Jobs](BackgroundJobs.md) |
| Ergebnisse wirken unvollständig | Konto-Berechtigungen | Bestätigen Sie, dass das Postfach Ihrem Benutzer zugewiesen ist |
| Daten erscheinen verschoben | Zeitzonenkonvertierung | Überprüfen Sie Ihre Zeitzoneneinstellung |

## 📚 Verwandte Dokumentation {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — Wiederherstellung von Ergebnissen in ein Postfach
- [Background Jobs](BackgroundJobs.md) — Überwachung von Exporten und Wiederherstellungen
- [Access Logging](Logs.md) — das Audit-Protokoll von Suchen und Löschungen
- [REST API](API.md) — dasselbe Archiv über die API
- [MCP Server](MCP.md) — Suche über einen KI-Agenten
- [Retention Policies](RetentionPolicies.md) — wenn E-Mails das Archiv verlassen
