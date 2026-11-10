# 📧 Gmail Best Practices {#gmail-best-practices}

[← Zurück zum Dokumentationsindex](index.md)

Wenn Sie Gmail-Konten mit der Mail Archiver Anwendung verwenden, sollten mehrere spezifische Konfigurationen und Best Practices befolgt werden, um eine optimale Leistung zu gewährleisten und potenzielle Probleme zu vermeiden.

## 🔐 IMAP-Konfiguration mit App-Passwörtern {#imap-configuration-with-app-passwords}

Gmail-Konten können mit der Mail Archiver Anwendung unter Verwendung von IMAP mit App-Passwörtern integriert werden:

1. Aktivieren Sie die 2-Faktor-Authentifizierung in Ihrem Gmail-Konto
2. Generieren Sie ein App-Passwort speziell für die Mail Archiver Anwendung
3. Verwenden Sie das App-Passwort anstelle Ihres regulären Gmail-Passworts, wenn Sie das Konto in Mail Archiver konfigurieren

## 🏷️ Gmail Label-Einstellungen {#gmail-label-settings}

Es wird dringend empfohlen, Ihre Gmail Label-Einstellungen anzupassen, um zu verhindern, dass das "All Mail"-Label über IMAP exponiert wird:

1. Gehen Sie zu Ihrem Gmail-Webinterface und öffnen Sie die Einstellungen (kleines Zahnrad-Symbol oben) → Alle Einstellungen anzeigen → Tab "Labels"
2. Suchen Sie das "All Mail"-Label in der Liste
3. Deaktivieren Sie die Option "In IMAP anzeigen" für "All Mail"

### 📚 Hintergrund zur Gmail Label-Implementierung {#background-on-gmail-label-implementation}

Das Label-System von Gmail funktioniert anders als bei herkömmlichen E-Mail-Anbietern. In Gmail:
- Existieren alle E-Mails in einem einzigen "All Mail"-Repository
- Fungieren Labels als Tags anstatt als Ordner
- Das "All Mail"-Label enthält jede E-Mail in Ihrem Konto
- Wenn eine E-Mail mit einem Label versehen wird, erscheint sie sowohl im ursprünglichen Ordner als auch im Label-Bereich. Daher erscheinen alle Mails in Ihrem Posteingang auch im "All Mail"-Label.

### ⚠️ Probleme bei der IMAP-Abruf {#issues-with-imap-retrieval}

Wenn "All Mail" über IMAP exponiert wird, kann dies mehrere Probleme verursachen:

1. **Doppelte Verarbeitung**: Das Archiv kann dieselbe E-Mail mehrfach verarbeiten, da sie in mehreren Ordnern/Labels erscheint
2. **Leistungsauswirkungen**: Das Abrufen von "All Mail" kann bei großen Konten mit Tausenden von E-Mails extrem langsam sein
4. **Synchronisierungsfehler**: Es können Konflikte auftreten, wenn versucht wird zu bestimmen, zu welchem Ordner eine E-Mail tatsächlich gehört

Durch das Deaktivieren des IMAP-Zugriffs auf "All Mail" stellen Sie sicher, dass E-Mails nur einmal von ihrem primären Standort abgerufen werden, was sowohl die Leistung als auch die Genauigkeit verbessert.

## 🗑️ Aufbewahrungsrichtlinie und Expunge-Einstellungen {#retention-policy-and-expunge-settings}

Wie im Haupt-README erwähnt, erfordern Gmail-Konten besondere Aufmerksamkeit hinsichtlich der Aufbewahrungsrichtlinien:

- Die Gmail "Auto-Expunge"-Funktion muss in den Gmail-Einstellungen unter dem Tab "Weiterleitung und POP/IMAP" deaktiviert werden
- Diese Einstellung ist entscheidend, damit Aufbewahrungsrichtlinien mit Gmail-Konten korrekt funktionieren

Um diese Einstellung zu konfigurieren:
1. Gehen Sie zu Gmail-Einstellungen → Alle Einstellungen anzeigen → Tab "Weiterleitung und POP/IMAP"
2. Wählen Sie im Abschnitt "IMAP-Zugriff" "Auto-Expunge aus - Warten Sie, bis der Client den Server aktualisiert" und "Nachricht in den Papierkorb verschieben"

## ⚠️ Gmail IMAP Rate Limiting {#gmail-imap-rate-limiting}

Gmail legt ein tägliches Limit von 2500 MB für den IMAP-Abruf fest. Wenn Sie große Mengen an E-Mails archivieren, beachten Sie, dass Sie dieses Limit erreichen können, was den E-Mail-Abruf vorübergehend bis zum nächsten Tag pausieren kann.

### 📊 Handhabung des Rate Limits {#rate-limit-handling}

Mail Archiver bietet eine integrierte Handhabung des Rate Limits, um die IMAP-Bandbreitenlimits von Gmail zu verwalten:

- **Automatische Bandbreitenverfolgung**: Überwacht die heruntergeladenen Daten pro Konto pro Tag
- **Konfigurierbare Limits**: Setzen Sie `BandwidthTracking__DailyLimitMb=2500` für Gmail
- **Automatische Pause**: Die Synchronisierung pausiert, wenn das Limit erreicht ist, und wird nach dem Reset fortgesetzt
- **Checkpoint-System**: Der Synchronisierungsfortschritt wird gespeichert und vom letzten Stand aus fortgesetzt

Für detaillierte Konfiguration und Verwendung siehe den **[Rate Limit Handling Guide](RateLimitHandling.md)**.

### 🔧 Gmail Konfigurationsbeispiel {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 Überlegungen für große Postfächer {#large-mailbox-considerations}

Bei großen Gmail-Postfächern (z. B. 14 GB) wird die initiale Synchronisierung mehrere Tage dauern:

| Postfachgröße | Tage bis zum Abschluss (bei 2500 MB/Tag) |
|--------------|-----------------------------------|
| 5 GB | ~2 Tage |
| 10 GB | ~4 Tage |
| 14 GB | ~6 Tage |
| 20 GB | ~8 Tage |

**Tipp**: Aktivieren Sie die Bandbreitenverfolgung, **bevor** Sie große Gmail-Konten hinzufügen, um sicherzustellen, dass das Rate Limit von Beginn an korrekt gehandhabt wird.

## ✅ Zusammenfassung der Best Practices {#best-practices-summary}

Das Befolgen dieser Best Practices hilft dabei, einen reibungslosen Betrieb von Mail Archiver mit Ihren Gmail-Konten zu gewährleisten und gleichzeitig die Datenintegrität und optimale Leistung aufrechtzuerhalten.
