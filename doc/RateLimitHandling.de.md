# 📊 Rate Limit Handling {#rate-limit-handling}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Viele E-Mail-Anbieter legen Bandbreitenbeschränkungen für den IMAP-Zugriff fest. Diese Beschränkungen liegen typischerweise zwischen einigen hundert Megabyte und mehreren Gigabyte pro 24-Stunden-Zeitraum. Wenn große Postfächer synchronisiert werden, die diese Limits überschreiten, würde der Synchronisierungsprozess normalerweise fehlschlagen oder unvollständig sein.

Mail Archiver enthält ein integriertes System zur Handhabung von Ratenbegrenzungen, das diese Einschränkungen automatisch verwaltet und eine zuverlässige Synchronisierung selbst für große Postfächer gewährleistet.

## ⚙️ Funktionsweise {#how-it-works}

### Automatische Bandbreitenverfolgung {#automatic-bandwidth-tracking}

Das System überwacht kontinuierlich die Menge der während der IMAP-Synchronisierung heruntergeladenen Daten. Wenn das konfigurierte Bandbreitenlimit erreicht ist:

1. **Synchronisierung pausiert automatisch**: Der aktuelle Synchronisierungsvorgang wird ordnungsgemäß gestoppt
2. **Fortschritt wird gespeichert**: Checkpoints speichern die exakte Position, an der die Synchronisierung gestoppt wurde
3. **Statusaktualisierungen**: Das Konto zeigt im Dashboard den Status „Rate-Limited“ an
4. **Automatische Fortsetzung**: Wenn das Limit zurückgesetzt wird (in der Regel nach 24 Stunden), wird die Synchronisierung ab dem gespeicherten Checkpoint fortgesetzt

### Beispiel: 14 GB Postfach mit 2,5 GB Tageslimit {#example-14-gb-mailbox-with-25-gb-daily-limit}

```
Day 1:  Sync starts → ~2.5 GB downloaded → Rate-Limited
        Status: Rate-Limited, Checkpoint saved

Day 2:  Limit still active → Sync skipped

Day 3:  Limit resets → Sync resumes from checkpoint
        → Additional ~2.5 GB → Rate-Limited again

Day 4-6: Continues...

Day N:   All 14 GB synced → Status: Completed
```

## 🔧 Konfiguration {#configuration}

### Aktivieren des Bandbreiten-Trackings {#enabling-bandwidth-tracking}

Fügen Sie den folgenden Abschnitt zu Ihrem `appsettings.json` hinzu:

```json
{
  "BandwidthTracking": {
    "Enabled": true,
    "DailyLimitMb": 2500,
    "WarningThresholdPercent": 80,
    "PauseHoursOnLimit": 24
  }
}
```

### Konfigurationsoptionen {#configuration-options}

| Option | Beschreibung | Standard | Beispiel |
|--------|-------------|---------|---------|
| `Enabled` | Bandbreiten-Tracking aktivieren/deaktivieren | `false` | `true` |
| `DailyLimitMb` | Tägliches Download-Limit in Megabyte | `2500` | `2500` |
| `WarningThresholdPercent` | Prozentsatz des Limits, um Warnungen in den Protokollen auszulösen | `80` | `80` |
| `PauseHoursOnLimit` | Stunden der Pause, wenn das Limit erreicht ist | `24` | `24` |

### Festlegen des korrekten Limits {#setting-the-correct-limit}

Um den geeigneten `DailyLimitMb`-Wert zu ermitteln:

1. Überprüfen Sie die Dokumentation oder die Nutzungsbedingungen Ihres E-Mail-Anbieters
2. Suchen Sie nach IMAP-Bandbreitenlimits oder Download-Quotas
3. Kontaktieren Sie Ihren E-Mail-Administrator, wenn Sie ein Unternehmens-E-Mail-System verwenden
4. Beginnen Sie mit einem konservativen Wert, wenn Sie unsicher sind (z. B. 2000 MB)
5. Überwachen Sie die Protokolle auf Rate-Limit-Warnungen während der ersten Synchronisierung

## 👤 Benutzererfahrung {#user-experience}

### Dashboard-Anzeigen {#dashboard-indicators}

Wenn das Rate-Limiting aktiv ist, zeigt das Konto an:

- **Status**: "Rate-Limited" 
- **Info-Banner**: Erklärt die Situation und die geschätzte Reset-Zeit
- **Synchronisierungsfortschritt**: Zeigt die E-Mails an, die vor Erreichen des Limits verarbeitet wurden

### Automatisches Verhalten {#automatic-behavior}

1. **Keine manuelle Intervention erforderlich**: Das System erledigt alles automatisch
2. **Anwendungsneustarts**: Wenn die Anwendung neu startet, bleibt der Rate-Limit-Status in der Datenbank erhalten
3. **Mehrere Konten**: Jedes Konto verfolgt seine Bandbreite unabhängig

### Was gespeichert wird {#what-gets-saved}

Wenn ein Rate-Limit auftritt, speichert das System:

- **Synchronisierungs-Checkpoints**: Die genaue Position in jedem Ordner, an der die Synchronisierung gestoppt wurde
- **Bandbreitennutzung**: Gesamte für den aktuellen Tag heruntergeladene Bytes
- **Limit-Status**: Ob das Limit erreicht wurde und wann es zurückgesetzt wird

## 🗄️ Datenbankwartung {#database-maintenance}

Der `DatabaseMaintenanceService` bereinigt automatisch alte Daten:

- **Bandbreiten-Datensätze**: Werden nach 7 Tagen gelöscht
- **Sync-Checkpoints**: Abgeschlossene Checkpoints werden nach 30 Tagen gelöscht
- **Unvollständige Checkpoints**: Werden aufbewahrt, bis die Synchronisierung abgeschlossen ist

## ✅ Best Practices {#best-practices}

### Für große Postfächer {#for-large-mailboxes}

1. **Bandbreiten-Tracking aktivieren**: Essenziell für Postfächer, die größer als das tägliche Limit sind
2. **Planung für die initiale Synchronisierung**: Ein großes Postfach benötigt möglicherweise mehrere Tage, um vollständig zu synchronisieren
3. **Fortschritt überwachen**: Überprüfen Sie das Dashboard auf den Status des Rate-Limits
4. **Nicht unterbrechen**: Lassen Sie die automatische Fortsetzung die weitere Verarbeitung übernehmen

### Für Administratoren {#for-administrators}

1. **Angemessene Limits festlegen**: Passen Sie das `DailyLimitMb` an die tatsächlichen Limits Ihres E-Mail-Anbieters an
2. **Protokolle überwachen**: Warnmeldungen erscheinen bei 80 % des Limits
3. **Datenbankplatz**: Das Bandbreiten-Tracking verbraucht nur minimal zusätzlichen Speicherplatz
4. **Sicherung vor Änderungen**: Erstellen Sie immer eine Sicherung, bevor Sie die Rate-Limit-Einstellungen ändern

### Leistungsaspekte {#performance-considerations}

- ✅ **Speicherauswirkung**: Vernachlässigbar – es werden nur Bytes verfolgt, keine E-Mails gespeichert
- ✅ **Datenbankauswirkung**: Minimal – kleine Datensätze werden automatisch bereinigt
- ✅ **Synchronisierungsgeschwindigkeit**: Keine Leistungsauswirkung während der aktiven Synchronisierung

## 🛡️ Vorübergehende FETCH-Fehler / Serverseitiges Throttling {#transient-fetch-errors-server-side-throttling}

Zusätzlich zur oben beschriebenen Byte-basierten Bandbreitenverfolgung erkennt Mail Archiver auch das vom IMAP-Server selbst signalisierte **pro-Nachricht-Throttling** und reagiert darauf. Einige gehostete IMAP-Anbieter wenden ein pro-Konto oder pro-IP Throttling an, das **unabhängig von den konfigurierten `BatchOperation` Pausen** ist, da das Throttling serverseitig basierend auf dem Gesamtanfragevolumen während der aktuellen Sitzung erzwungen wird.

### Symptome {#symptoms}

Der Server gibt eine `NO` (oder `BAD`) Antwort auf einen einzelnen `FETCH` Befehl für einen Nachrichtenkörper zurück, mit Nachrichten wie:

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- Antworten, die `throttle`, `rate limit` oder ähnliche Marker enthalten

Authentifizierung, Ordnerauflistung und die Nachrichtensuche selbst sind in der Regel weiterhin erfolgreich — nur der eigentliche Abruf der vollständigen Nachricht schlägt fehl.

### Automatische Handhabung {#automatic-handling}

Wenn ein solcher vorübergehender Fehler erkannt wird, wendet Mail Archiver eine pro-Nachricht-Wiederholungsstrategie an:

1. **Wiederholung mit exponentiellem Backoff**: Bis zu **3 Wiederholungen** für dieselbe Nachricht, mit Verzögerungen von **5 s → 15 s → 60 s**.
2. **Verbindungsaufbau**: Vor jeder Wiederholung werden die Verbindung, die Authentifizierung und der Zustand der Ordnerauswahl validiert und bei Bedarf wiederhergestellt.
3. **Zurücksetzen bei Erfolg**: Ein erfolgreicher FETCH setzt den Zähler für aufeinanderfolgende Fehler zurück.
4. **Sanfte Synchronisierungspause**: Wenn **10 aufeinanderfolgende Nachrichten** nach allen Wiederholungen weiterhin vorübergehende Fehler aufweisen, wird der aktuelle Synchronisierungsdurchlauf sanft unterbrochen — derselbe Mechanismus, der auch für die Bandbreitenlimit-Pause verwendet wird:
   - Der `LastSync` Zeitstempel wird **nicht** aktualisiert.
   - Wenn `BandwidthTracking` aktiviert ist, bleiben Checkpoints erhalten, sodass der nächste Synchronisierungsdurchlauf mit der letzten erfolgreich verarbeiteten Nachricht fortgesetzt wird.
   - Der Job wird als rate-limited markiert.
   - Die nächste geplante Synchronisierung wird automatisch fortgesetzt.

### Maßnahmen zur Minderung {#mitigation}

Wenn Sie regelmäßig auf serverseitiges Throttling stoßen, senken Sie die Anfragerate, indem Sie Ihre `BatchOperation` Einstellungen anpassen:

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

Beachten Sie, dass einige Anbieter basierend auf der **Gesamtbyte-Menge** oder der **Gesamtanzahl der FETCHs pro Zeitfenster** drosseln und nicht nach der Rate pro Sekunde. In diesen Fällen:

- Aktivieren Sie `BandwidthTracking` (siehe oben) und konfigurieren Sie `DailyLimitMb` so, dass es dem dokumentierten Limit des Anbieters entspricht. Die Synchronisierung wird dann sauber pausiert, wenn das Tagesbudget erschöpft ist, und setzt sich am nächsten Tag automatisch fort.
- Die initiale Synchronisierung sehr großer Postfächer kann mehrere Tage dauern, was dem beabsichtigten Verhalten entspricht.

### Was als vorübergehender Fehler gilt {#what-counts-as-a-transient-error}

Nur IMAP `NO`/`BAD` Antworten mit throttling-bezogenem Text oder Antwortcodes werden als vorübergehend behandelt. Andere Fehler (fehlerhafte Nachrichten, UTF-8-Dekodierungsprobleme usw.) werden sofort als `FailedEmails` gezählt und lösen die Wiederholungs-/Pause-Logik **nicht** aus.

## 📚 Verwandte Dokumentation {#related-documentation}

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Datenbankbereinigungsverfahren
- [GmailBestPractices.md](GmailBestPractices.md) - Gmail-spezifische Konfiguration
