# 📊 Rate Limit Handling {#rate-limit-handling}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Viele E-Mail-Anbieter legen Bandbreitenbeschränkungen für den IMAP-Zugriff fest. Diese Limits liegen typischerweise zwischen einigen hundert Megabyte und mehreren Gigabyte pro 24-Stunden-Zeitraum. Wenn beim Synchronisieren großer Postfächer, die diese Limits überschreiten, die Synchronisierung stattfinden soll, würde der Prozess normalerweise fehlschlagen oder unvollständig bleiben.

Mail Archiver enthält ein integriertes System zur Handhabung von Ratenbeschränkungen (Rate Limit Handling), das diese Einschränkungen automatisch verwaltet und eine zuverlässige Synchronisierung auch bei großen Postfächern gewährleistet.

## ⚙️ Funktionsweise {#how-it-works}

### Automatische Bandbreitenverfolgung {#automatic-bandwidth-tracking}

Das System überwacht kontinuierlich die Menge der während der IMAP-Synchronisierung heruntergeladenen Daten. Wenn das konfigurierte Bandbreitenlimit erreicht ist:

1. **Synchronisierung pausiert automatisch**: Der aktuelle Synchronisierungsvorgang wird kontrolliert unterbrochen.
2. **Fortschritt wird gespeichert**: Checkpoints speichern die exakte Position, an der die Synchronisierung gestoppt wurde.
3. **Status-Updates**: Das Konto zeigt im Dashboard den Status "Rate-Limited" an.
4. **Automatische Fortsetzung**: Wenn das Limit zurückgesetzt wird (typischerweise nach 24 Stunden), setzt die Synchronisierung die Arbeit ab dem gespeicherten Checkpoint fort.

### Beispiel: 14 GB Postfach mit 2,5 GB täglichem Limit {#example-14-gb-mailbox-with-25-gb-daily-limit}

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

### Aktivierung der Bandbreitenverfolgung {#enabling-bandwidth-tracking}

Fügen Sie den folgenden Abschnitt zu Ihrer `appsettings.json` hinzu:

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
| `Enabled` | Bandbreitenverfolgung aktivieren/deaktivieren | `false` | `true` |
| `DailyLimitMb` | Tägliches Download-Limit in Megabytes | `2500` | `2500` |
| `WarningThresholdPercent` | Prozentsatz des Limits, um Warnungen in den Logs auszulösen | `80` | `80` |
| `PauseHoursOnLimit` | Stunden der Pause, wenn das Limit erreicht ist | `24` | `24` |

### Festlegen des korrekten Limits {#setting-the-correct-limit}

Um den geeigneten `DailyLimitMb`-Wert zu bestimmen:

1. Überprüfen Sie die Dokumentation oder die Nutzungsbedingungen Ihres E-Mail-Anbieters.
2. Suchen Sie nach IMAP-Bandbreitenlimits oder Download-Quoten.
3. Kontaktieren Sie Ihren E-Mail-Administrator, falls Sie ein Unternehmens-E-Mail-System verwenden.
4. Beginnen Sie mit einem konservativen Wert, wenn Sie unsicher sind (z. B. 2000 MB).
5. Überwachen Sie die Logs auf Rate-Limit-Warnungen während der ersten Synchronisierungen.

## 👤 Benutzererfahrung {#user-experience}

### Dashboard-Indikatoren {#dashboard-indicators}

Wenn die Ratenbeschränkung aktiv ist, zeigt das Konto an:

- **Status**: "Rate-Limited" 
- **Info-Banner**: Erklärt die Situation und die geschätzte Zeit bis zum Reset.
- **Sync-Fortschritt**: Zeigt die Anzahl der verarbeiteten E-Mails vor Erreichen des Limits an.

### Automatisches Verhalten {#automatic-behavior}

1. **Kein manuelles Eingreifen erforderlich**: Das System erledigt alles automatisch.
2. **Anwendungsneustarts**: Wenn die Anwendung neu startet, bleibt der Zustand der Ratenbeschränkung in der Datenbank erhalten.
3. **Mehrere Konten**: Jedes Konto verfolgt seine Bandbreite unabhängig.

### Was gespeichert wird {#what-gets-saved}

Wenn eine Ratenbeschränkung auftritt, speichert das System:

- **Sync-Checkpoints**: Die exakte Position in jedem Ordner, an der die Synchronisierung gestoppt wurde.
- **Bandbreitennutzung**: Die gesamten für den aktuellen Tag heruntergeladenen Bytes.
- **Limit-Status**: Ob das Limit erreicht wurde und wann es zurückgesetzt wird.

## 🗄️ Datenbankwartung {#database-maintenance}

Die `DatabaseMaintenanceService` bereinigt automatisch alte Daten:

- **Bandbreiten-Datensätze**: Werden nach 7 Tagen gelöscht.
- **Sync-Checkpoints**: Abgeschlossene Checkpoints werden nach 30 Tagen gelöscht.
- **Unvollständige Checkpoints**: Bleiben erhalten, bis die Synchronisierung abgeschlossen ist.

## ✅ Best Practices {#best-practices}

### Für große Postfächer {#for-large-mailboxes}

1. **Bandbreitenverfolgung aktivieren**: Essentiell für Postfächer, die größer als das tägliche Limit sind.
2. **Planung der Erstsynchronisierung**: Ein großes Postfach kann mehrere Tage benötigen, um vollständig synchronisiert zu werden.
3. **Fortschritt überwachen**: Prüfen Sie das Dashboard auf den Rate-Limit-Status.
4. **Nicht unterbrechen**: Lassen Sie die automatische Fortsetzung die Arbeit übernehmen.

### Für Administratoren {#for-administrators}

1. **Angemessene Limits festlegen**: Passen Sie den `DailyLimitMb` an die tatsächlichen Limits Ihres E-Mail-Anbieters an.
2. **Logs überwachen**: Warnmeldungen erscheinen bei 80 % des Limits.
3. **Datenbankplatz**: Die Bandbreitenverfolgung benötigt minimalen zusätzlichen Speicherplatz.
4. **Sicherung vor Änderungen**: Erstellen Sie immer eine Sicherung, bevor Sie die Rate-Limit-Einstellungen ändern.

### Leistungsaspekte {#performance-considerations}

- ✅ **Speicherauswirkung**: Vernachlässigbar – es werden nur Bytes verfolgt, keine E-Mails gespeichert.
- ✅ **Datenbank-Auswirkung**: Minimal – kleine Datensätze werden automatisch bereinigt.
- ✅ **Sync-Geschwindigkeit**: Keine Leistungseinbußen während der aktiven Synchronisierung.

## 🛡️ Transiente FETCH-Fehler / Serverseitiges Throttling {#transient-fetch-errors-server-side-throttling}

Zusätzlich zur oben beschriebenen bytebasierten Bandbreitenverfolgung erkennt Mail Archiver auch das **pro Nachricht erfolgende Throttling**, das durch den IMAP-Server selbst signalisiert wird. Einige gehostete IMAP-Anbieter wenden ein pro Konto oder pro IP erfolgendes Throttling an, das **unabhängig von den konfigurierten `BatchOperation`-Pausen** ist, da das Throttling serverseitig basierend auf dem gesamten Anfragevolumen während der aktuellen Sitzung erzwungen wird.

### Symptome {#symptoms}

Der Server gibt eine `NO` (oder `BAD`) Antwort auf einen einzelnen `FETCH`-Befehl für einen Nachrichtenkörper zurück, mit Meldungen wie:

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- Antworten, die `throttle`, `rate limit` oder ähnliche Marker enthalten.

Authentifizierung, Ordnerauflistung und die Nachrichtensuche selbst funktionieren in der Regel weiterhin — nur der eigentliche Abruf der vollständigen Nachricht schlägt fehl.

### Automatische Handhabung {#automatic-handling}

Wenn ein solcher transienter Fehler erkannt wird, wendet Mail Archiver eine Strategie für Wiederholungsversuche pro Nachricht an:

1. **Wiederholung mit exponentiellem Backoff**: Bis zu **3 Versuche** für dieselbe Nachricht, mit Verzögerungen von **5 s → 15 s → 60 s**.
2. **Verbindungsreparatur**: Vor jedem Wiederholungsversuch werden der Verbindungs-, Authentifizierungs- und Ordnerauswahlstatus validiert und bei Bedarf wiederhergestellt.
3. **Reset bei Erfolg**: Ein erfolgreicher FETCH setzt den Zähler für aufeinanderfolgende Fehler zurück.
4. **Sanfte Synchronisierungspause**: Wenn **10 aufeinanderfolgende Nachrichten** nach allen Versuchen immer noch transiente Fehler aufweisen, wird der aktuelle Synchronisierungslauf sanft pausiert — derselbe Mechanismus wie bei der Bandbreitenlimit-Pause:
   - Der `LastSync`-Zeitstempel wird **nicht** aktualisiert.
   - Wenn `BandwidthTracking` aktiviert ist, werden Checkpoints erhalten, sodass der nächste Synchronisierungslauf ab der letzten erfolgreich verarbeiteten Nachricht fortgesetzt wird.
   - Der Job wird als "rate-limited" markiert.
   - Die nächste geplante Synchronisierung wird automatisch fortgesetzt.

### Abhilfe {#mitigation}

Wenn Sie regelmäßig auf serverseitiges Throttling stoßen, senken Sie die Anfragerate durch Anpassung Ihrer `BatchOperation`-Einstellungen:

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

Beachten Sie, dass einige Anbieter das Throttling basierend auf den **Gesamtbytes** oder der **Gesamtzahl der FETCHs pro Zeitfenster** und nicht nach der Rate pro Sekunde durchführen. In diesen Fällen:

- Aktivieren Sie `BandwidthTracking` (siehe oben) und konfigurieren Sie `DailyLimitMb` passend zum dokumentierten Limit des Anbieters. Die Synchronisierung pausiert dann sauber, wenn das tägliche Budget erschöpft ist, und setzt sich am nächsten Tag automatisch fort.
- Die Erstsynchronisierung sehr großer Postfächer kann mehrere Tage dauern, was das beabsichtigte Verhalten ist.

### Was als transienter Fehler gilt {#what-counts-as-a-transient-error}

Nur IMAP `NO`/`BAD`-Antworten mit throttling-bezogenem Text oder Antwortcodes werden als transient behandelt. Andere Fehler (fehlerhafte Nachrichten, UTF-8-Dekodierungsprobleme usw.) werden sofort als `FailedEmails` gezählt und lösen die Retry/Pause-Logik **nicht** aus.

## 📚 Verwandte Dokumentation {#related-documentation}

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Verfahren zur Datenbankbereinigung
- [GmailBestPractices.md](GmailBestPractices.md) - Gmail-spezifische Konfiguration
