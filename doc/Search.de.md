# 🔍 Mail Search {#mail-search}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Die Suche ist das Herzstück eines E-Mail-Archivs. Mail Archiver durchsucht den **Volltext**
Ihrer archivierten E-Mails – Betreff, Textkörper, Absender und Empfänger – über alle
Konten, auf die Sie Zugriff haben, und liefert Ergebnisse in Millisekunden, selbst bei
Archiven mit Millionen von Nachrichten.

Dieser Leitfaden behandelt die Suchoberfläche, die Abfragesyntax, Filter, Sortierung,
Seitenaufteilung und was zu tun ist, wenn Sie auf einen Ergebnissatz reagieren müssen.

> **ℹ️ Die Suche sieht nur das, was Sie sehen dürfen.** Ergebnisse werden durch Ihre
> Konto-Berechtigungen gefiltert. Ein Nicht-Administrator findet nur E-Mails aus den
> Postfächern, die ihm zugewiesen sind – die Einschränkung wird in der Abfrage selbst
> angewendet, nicht in der Benutzeroberfläche.

## 🚀 Zugriff auf die Suche {#accessing-search}

1. Melden Sie sich an und wählen Sie **Archive** im Hauptmenü
2. Die Suchleiste befindet sich oberhalb der Ergebnisliste

Das Öffnen des Archivs ohne Suchbegriff listet Ihre neuesten E-Mails auf, die neuesten
zuerst. Sie können von dort aus stöbern oder die Ergebnisse mit Filtern eingrenzen.

## 🎯 Die Suchoberfläche {#the-search-interface}

| Feld | Funktion |
|---|---|
| **Search Term** | Volltextabfrage – siehe [Query Syntax](#-query-syntax) unten |
| **From** | Nur E-Mails, die an oder nach diesem Datum gesendet wurden |
| **To** | Nur E-Mails, die an oder vor diesem Datum gesendet wurden |
| **Direction** | `All`, `Incoming` oder `Outgoing` |
| **Account** | Auf ein E-Mail-Konto beschränken |
| **Folders** | Über den Ordnerbaum auf einen Ordner beschränken |
| **Search** | Abfrage ausführen |
| **Reset** | Alle Filter löschen |

Der Suchbegriff gleicht den **Betreff, Textkörper, Absender und Empfänger**
(`From`, `To`, `Cc`, `Bcc`) ab.

### Der Ordnerbaum {#the-folder-tree}

Ordner werden links als **hierarchischer Baum** angezeigt, der die Ordnerstruktur
des Quell-Postfachs widerspiegelt – einschließlich verschachtelter Ordner. Klicken Sie auf einen Ordner,
um die Suche darauf zu beschränken, und verwenden Sie die Ein-/Ausklapp-Steuerung, um den Baum
einzuklappen, wenn Sie Platz auf dem Bildschirm benötigen.

Das Auswählen eines Ordners ändert Ihren Suchbegriff nicht; beide werden kombiniert.

## 🧩 Query Syntax {#query-syntax}

Die Syntax ist bewusst einfach gehalten. Alles Folgende ist das, was die Anwendung
tatsächlich versteht.

### Einfache Wörter – und warum Präfixe funktionieren {#plain-words-and-why-prefixes-work}

Geben Sie ein oder mehrere Wörter ein; alle müssen vorhanden sein (eine **AND**-Suche):

```
invoice january
```

Findet E-Mails, die sowohl `invoice` als auch `january` in den durchsuchten Feldern enthalten.

> **💡 Teilwörter werden automatisch erkannt.** Jedes Wort wird als
> **Präfix** behandelt, sodass eine Suche nach `isenb` auch `isenboeck` und `isenböck` findet, und
> `rech` findet sowohl `Rechnung` als auch `Rechnungen`. Sie benötigen kein Platzhalterzeichen
> – es gibt keines zum Eintippen. Dies bedeutet auch, dass ein sehr kurzer Begriff
> viele Ergebnisse liefern kann; siehe [Performance](#-performance-and-large-archives).

Die Suche ist **nicht fallsensitiv**. `Invoice`, `invoice` und `INVOICE` sind die
gleiche Abfrage.

### Exakte Phrasen – Anführungszeichen {#exact-phrases-quotes}

Umschließen Sie Text mit doppelten Anführungszeichen, um zu verlangen, dass die Wörter
**direkt nebeneinander** in dieser Reihenfolge stehen:

```
"important meeting"
```

Ohne Anführungszeichen würde `important meeting` auch eine E-Mail finden, in der die beiden
Wörter in nicht zusammenhängenden Sätzen vorkommen. Mit Anführungszeichen passt nur die
direkt aufeinanderfolgende Phrase.

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
> `cc:someone` oder `bcc:someone` ist **keine** Feldesuche – der Begriff wird als
> gewöhnlicher Text behandelt und über alle Felder hinweg durchsucht. Um einen Empfänger zu finden, der
> nur in Cc war, suchen Sie nach der Adresse oder dem Namen ohne Präfix.

### Alles kombinieren {#combining-everything}

Filter und Begriffe summieren sich. Diese Abfrage findet E-Mails von jedem Versicherungs-Absender,
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

Der Query-Parser entfernt die Zeichen `& | ! ( ) : *` von einzelnen Begriffen,
da sie in der zugrunde liegenden Volltext-Engine eine Bedeutung haben. Wenn Ihre Suche
unerwartet nichts zurückgibt, prüfen Sie, ob ein Sonderzeichen der Grund dafür ist.

## 📊 Sortierung und Seitenaufteilung {#sorting-and-paging}

### Sortierung {#sorting}

Klicken Sie auf eine Spaltenüberschrift, um zu sortieren. Sortierbare Spalten:

| Spalte | Sortiert nach |
|---|---|
| **Subject** | Betreffzeile, alphabetisch |
| **From** | Absender |
| **To** | Empfänger |
| **Date** | Sendedatum |

Ein erneuter Klick auf dieselbe Überschrift kehrt die Richtung um. Standard ist **neueste
zuerst**.

### Seitengröße {#page-size}

Wählen Sie aus, wie viele Ergebnisse pro Seite angezeigt werden sollen:

**20** (Standard) · **50** · **75** · **100** · **150**

Eine größere Seite zeigt mehr Kontext, benötigt aber länger zum Rendern. Verwenden Sie die
**Zurück** / **Weiter**-Steuerung, um durch die Ergebnisseiten zu blättern; die
Ergebnisanzahl zeigt Ihnen, wie viele E-Mails insgesamt übereinstimmen.

> **ℹ️ Ihre Zeitzone gilt für die angezeigten Daten.** Angezeigte Zeiten werden
> von UTC in Ihre konfigurierte Zeitzone umgerechnet, sodass das Datum, das Sie sehen,
> Ihrem lokalen Kalender entspricht – nützlich, wenn Sie um Mitternacht suchen.

## 👁️ Arbeiten mit Ergebnissen {#working-with-results}

Jede Zeile zeigt das Konto, den Absender, den Betreff, das Datum und ein **📎 Büroklammer-Symbol**,
wenn die E-Mail Anhänge hat. Die Büroklammer ist nur ein Indikator – sie ist kein
Filter.

Klicken Sie auf **Details**, um eine E-Mail zu öffnen:

- Vollständiger Textkörper (HTML oder Reintext)
- **Anhänge**, aufgelistet mit der Option, jeden einzeln herunterzuladen
- **Download als EML** – die vollständige Nachricht, zur Verwendung an anderer Stelle
- **Wiederherstellung** – die E-Mail zurück in ein aktives Postfach kopieren

Siehe [Mailbox Migration](MailboxMigration.md) für die Wiederherstellung von E-Mails in ein Postfach.

## ✅ Massenverarbeitung von Ergebnissen {#acting-on-many-results-at-once}

Ergebnisse können ausgewählt und in Stapel verarbeitet werden. Aktivieren Sie den **Selection Mode**, um
Kontrollkästchen zu erhalten, und dann:

- **Select All** / **Deselect All** für die aktuelle Seite
- **Select all {n} matching** – jedes Ergebnis der aktuellen Suche über alle
  Seiten hinweg (wird angezeigt, wenn die Ergebnisse mehr als eine Seite umfassen)
- **Copy Selected** – Wiederherstellung in ein Postfach
- **Export Selected** – als MBOX oder EML
- **Delete Selected** – aus dem Archiv löschen

Der Zähler zeigt während der Arbeit *"{selected} von {total} ausgewählt"* an.

### Das Auswahllimit {#the-selection-limit}

> **⚠️ Es können maximal 250 E-Mails auf einmal ausgewählt werden** (konfigurierbar über
> `Selection:MaxSelectableEmails`). Darüber hinaus zeigt die Oberfläche *"Maximum
> selections reached (250)"* und **Too Many — Split Required** an.

Dieses Limit existiert, weil jede Operation die betroffenen Nachrichten halten muss. Der
Hinweis *"Split larger operations into multiple batches"* ist der vorgesehene
Workaround: Arbeiten Sie die Ergebnisse in Gruppen von 250 oder weniger ab.

**Select all {n} matching** respektiert dasselbe Limit. Wenn die Suche mehr
E-Mails findet, als das Limit erlaubt, werden die ersten in der aktuellen Sortierreihenfolge
ausgewählt und die Meldung *"Maximum selections reached"* angezeigt. Verkleinern Sie die Suche (Datumsbereich,
Ordner, Konto), um den Rest zu erreichen.

Ein Versuch, das Limit zu überschreiten, zeigt: *"Too many emails selected. Maximum allowed is
250 emails. Please reduce your selection or split into multiple operations."*

### Löschen aller Treffer eines Filters {#deleting-every-match-of-a-filter}

Die Schaltfläche **Delete all matches** neben *Reset* löscht jede E-Mail, die
dem aktuellen Suchbegriff entspricht, nicht nur die aktuelle Seite oder Auswahl. Sie steht Administratoren und Self-Managern zur Verfügung und läuft als Hintergrundjob, sodass
das Auswahllimit von 250 E-Mails nicht gilt.

Der Bestätigungsdialog zeigt an, wie viele E-Mails löschbar sind und wie viele durch die Aufbewahrungsrichtlinie gesperrt sind. Sie müssen diese Zahl eintippen, um die
Bestätigung zu aktivieren. Der Server zählt erneut und bricht ab, falls sich der Ergebnissatz in der Zwischenzeit geändert hat, zum Beispiel weil eine Synchronisierung E-Mails hinzugefügt hat.

Gesperrte E-Mails werden durch diese Aktion niemals gelöscht; sie werden im
Job-Status als übersprungen gemeldet. `DeletionPolicy:MaxBulkDeleteEmails` (Standard 100.000) begrenzt einen
Job. Ein Filter, der mehr E-Mails findet, wird mit der Aufforderung abgelehnt, ihn einzugrenzen.

Der Job erscheint auf der [Background Jobs](BackgroundJobs.md)-Seite und kann dort
abgebrochen werden.

### Direkte Verarbeitung vs. Hintergrundjob {#direct-processing-vs-background-job}

Bei **Copy** und **Export** wählen Sie, wie die Arbeit ausgeführt wird:

| Option | Verhalten |
|---|---|
| **Process on this page (wait for completion)** | Der Browser wartet; gut für eine Handvoll E-Mails |
| **Process in background (monitor separately)** | Läuft als Job; Sie können den Browser schließen |

Exporte laufen immer im Hintergrund:

> *"Der Export wird im Hintergrund ausgeführt, und Sie können das
> Ergebnis auf der Jobs-Seite herunterladen, sobald es abgeschlossen ist."*

Laufende und abgeschlossene Jobs finden Sie auf der [Background Jobs](BackgroundJobs.md)-Seite.

> **⚠️ Löschen ist permanent.** Die Bestätigung ist explizit: *"Are you sure you
> want to permanently delete {n} selected email(s)? This action cannot be undone
> and each deletion will be logged for audit purposes."* Löschungen werden
> im Zugriffsprotokoll aufgezeichnet.

## ⚡ Performance und große Archive {#performance-and-large-archives}

Die Suche wird durch einen PostgreSQL-Volltextindex unterstützt, daher ist der Unterschied zwischen einer
schnellen und einer langsamen Suche meistens die Frage, wie **selektiv** die Abfrage ist.

**Tun Sie Folgendes:**

- Fügen Sie einen **Datumsbereich** hinzu, wenn Sie ungefähr wissen, wann die E-Mail ankam – dies ist
  die effektivste Methode zur Eingrenzung
- Beschränken Sie die Suche auf ein **Konto** oder einen **Ordner**, wenn Sie wissen, wo die Mail liegt
- Verwenden Sie **mehrere Wörter** anstatt eines sehr kurzen Wortes
- Verwenden Sie Feldesuchen (`subject:`, `from:`), um irrelevante Treffer auszuschließen

**Vermeiden Sie:**

- Ein einzelnes kurzes Präfix wie `re` – als Präfixsuche trifft es einen riesigen
  Teil Ihres Archivs
- Sehr weite Datumsbereiche in Kombination mit einem häufigen Wort
- Das Durchblättern vieler Seiten, wenn Sie stattdessen die Suche hätten verengen können

> **💡 Wenn sich eine Suche langsam anfühlt, ist der Datumsbereich meist die Lösung.** Die Beschränkung
> auf einen Monat macht aus einer mehrsekündigen Suche oft eine sofortige, weil der
> Index den Großteil des Archivs überspringen kann.

## ❓ Fehlerbehebung {#troubleshooting}

| Symptom | Ursache | Was zu tun ist |
|---|---|---|
| Keine Ergebnisse, obwohl welche erwartet werden | Suchbegriff zu spezifisch oder Tippfehler | Weniger Wörter versuchen; Rechtschreibung prüfen |
| `cc:name` findet nichts Nützliches | `cc` ist kein unterstütztes Feld | Namen ohne Präfix suchen |
| Ein Sonderzeichen unterbricht die Abfrage | `& \| ! ( ) : *` werden entfernt | Zeichen entfernen oder ersetzen |
| Zu viele Ergebnisse | Häufiges Wort oder kurzes Präfix | Datumsbereich oder Feldesuche hinzufügen |
| *"Too Many — Split Required"* | Mehr als 250 E-Mails ausgewählt | Auswahl reduzieren oder Operation aufteilen |
| Ein Export fehlt | Er läuft als Hintergrundjob | [Background Jobs](BackgroundJobs.md) prüfen |
| Ergebnisse scheinen zu fehlen | Konto-Berechtigungen | Bestätigen Sie, dass das Postfach Ihrem Benutzer zugewiesen ist |
| Daten erscheinen verschoben | Zeitzonenkonvertierung | Prüfen Sie Ihre Zeitzoneneinstellung |

## 📚 Verwandte Dokumentation {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — Ergebnisse in ein Postfach wiederherstellen
- [Background Jobs](BackgroundJobs.md) — Exporte und Wiederherstellungen überwachen
- [Access Logging](Logs.md) — das Audit-Protokoll von Suchen und Löschungen
- [REST API](API.md) — dasselbe Archiv über die API
- [MCP Server](MCP.md) — Suche über einen KI-Agenten
- [Retention Policies](RetentionPolicies.md) — wann E-Mails das Archiv verlassen
