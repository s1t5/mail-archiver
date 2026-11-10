# 📧 Gmail Best Practices {#gmail-best-practices}

[← Zurück zum Dokumentationsindex](index.md)

Wenn Sie Gmail-Konten mit der Mail Archiver Anwendung verwenden, sollten mehrere spezifische Konfigurationen und Best Practices befolgt werden, um eine optimale Leistung zu gewährleisten und potenzielle Probleme zu vermeiden.

## 🔐 IMAP-Konfiguration mit App-Passwörtern {#imap-configuration-with-app-passwords}

Gmail-Konten können mit der Mail Archiver Anwendung unter Verwendung von IMAP mit App-Passwörtern integriert werden:

1. Aktivieren Sie die 2-Faktor-Authentifizierung in Ihrem Gmail-Konto
2. Generieren Sie ein App-Passwort speziell für die Mail Archiver Anwendung
3. Verwenden Sie das App-Passwort anstelle Ihres regulären Gmail-Passworts, wenn Sie das Konto in Mail Archiver konfigurieren

## 🏷️ Gmail Label-Einstellungen {#gmail-label-settings}

Es wird dringend empfohlen, Ihre Gmail Label-Einstellungen anzupassen, um zu verhindern, dass das Label „Alle Nachrichten“ über IMAP exponiert wird:

1. Gehen Sie zu Ihrem Gmail-Webinterface und öffnen Sie die Einstellungen (kleines Zahnrad-Symbol oben) → Alle Einstellungen aufrufen → Tab Labels
2. Suchen Sie das Label „Alle Nachrichten“ in der Liste
3. Deaktivieren Sie die Option „In IMAP anzeigen“ für „Alle Nachrichten“

### 📚 Hintergrund zur Gmail Label-Implementierung {#background-on-gmail-label-implementation}

Das Label-System von Gmail funktioniert anders als bei herkömmlichen E-Mail-Anbietern. In Gmail:
- Existieren alle E-Mails in einem einzigen „Alle Nachrichten“-Repository
- Fungieren Labels eher als Tags denn als Ordner
- Das Label „Alle Nachrichten“ enthält jede E-Mail in Ihrem Konto
- Wenn eine E-Mail mit einem Label versehen wird, erscheint sie sowohl im ursprünglichen Ordner als auch im Bereich des Labels. Daher erscheinen alle Mails in Ihrem Posteingang auch im Label „Alle Nachrichten“.

### ⚠️ Probleme bei der IMAP-Abfrage {#issues-with-imap-retrieval}

Wenn „Alle Nachrichten“ über IMAP exponiert wird, kann dies mehrere Probleme verursachen:

1. **Duplizierte Verarbeitung**: Das Archiv kann dieselbe E-Mail mehrfach verarbeiten, da sie in mehreren Ordnern/Labels erscheint
2. **Auswirkung auf die Performance**: Das Abrufen von „Alle Nachrichten“ kann bei großen Konten mit Tausenden von E-Mails extrem langsam sein
4. **Synchronisierungsprobleme**: Es können Konflikte auftreten, wenn versucht wird zu bestimmen, zu welchem Ordner eine E-Mail tatsächlich gehört

Durch das Deaktivieren des IMAP-Zugriffs auf „Alle Nachrichten“ stellen Sie sicher, dass E-Mails nur einmal von ihrem primären Speicherort abgerufen werden, was sowohl die Performance als auch die Genauigkeit verbessert.

## 🗑️ Aufbewahrungsrichtlinie und Expunge-Einstellungen {#retention-policy-and-expunge-settings}

Wie im Haupt-README erwähnt, erfordern Gmail-Konten besondere Aufmerksamkeit in Bezug auf Aufbewahrungsrichtlinien:

- Die Auto-Expunge-Funktion von Gmail muss in den Gmail-Einstellungen unter dem Tab „Weiterleitung und POP/IMAP“ deaktiviert werden
- Diese Einstellung ist entscheidend, damit Aufbewahrungsrichtlinien mit Gmail-Konten korrekt funktionieren

Um diese Einstellung zu konfigurieren:
1. Gehen Sie zu Gmail Settings → See all settings → Forwarding and POP/IMAP tab
2. Wählen Sie im Abschnitt „IMAP Access“ „Auto-Expunge off - Wait for the client to update the server.“ und „Move the message to the Bin“

## ⚠️ Gmail IMAP Rate Limiting {#gmail-imap-rate-limiting}

Gmail legt ein tägliches Limit von 2500 MB für den IMAP-Abruf fest. Wenn Sie große Mengen an E-Mails archivieren, beachten Sie bitte, dass Sie dieses Limit erreichen können, was den E-Mail-Abruf bis zum nächsten Tag vorübergehend pausieren kann.

### 📊 Rate Limit Handling {#rate-limit-handling}

Mail Archiver bietet eine integrierte Handhabung von Rate Limits, um die IMAP-Bandbreitenbeschränkungen von Gmail zu verwalten:

- **Automatische Bandbreitenverfolgung**: Überwacht die heruntergeladenen Daten pro Konto pro Tag
- **Konfigurierbare Limits**: Legen Sie `BandwidthTracking__DailyLimitMb=2500` für Gmail fest
- **Automatische Pause**: Die Synchronisierung wird pausiert, wenn das Limit erreicht ist, und setzt sich nach dem Reset fort
- **Checkpoint-System**: Der Synchronisierungsfortschritt wird gespeichert und am letzten Punkt fortgesetzt

Für detaillierte Konfiguration und Nutzung lesen Sie den **[Rate Limit Handling Guide](RateLimitHandling.md)**.

### 🔧 Gmail Configuration Example {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 Large Mailbox Considerations {#large-mailbox-considerations}

Bei großen Gmail Postfächern (z. B. 14 GB) wird die initiale Synchronisierung mehrere Tage dauern:

| Mailbox Size | Days to Complete (at 2500 MB/day) |
|--------------|-----------------------------------|
| 5 GB | ~2 days |
| 10 GB | ~4 days |
| 14 GB | ~6 days |
| 20 GB | ~8 days |

**Tipp**: Aktivieren Sie die Bandbreitenverfolgung, **bevor** Sie große Gmail Konten hinzufügen, um eine ordnungsgemäße Handhabung der Rate Limits von Beginn an zu gewährleisten.

## ✅ Zusammenfassung der Best Practices {#best-practices-summary}

Die Befolgung dieser Best Practices trägt dazu bei, einen reibungslosen Betrieb des Mail Archiver mit Ihren Gmail-Konten zu gewährleisten und gleichzeitig die Datenintegrität sowie eine optimale Leistung aufrechtzuerhalten.
