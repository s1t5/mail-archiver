# 🔧 Database Maintenance Guide {#database-maintenance-guide}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden erklärt, wie Sie die automatische Datenbankwartungsfunktion in Mail Archiver konfigurieren und verwenden. Die Anwendung enthält einen integrierten Hintergrunddienst, der automatisch PostgreSQL-Datenbankwartungsoperationen durchführt, um eine optimale Leistung aufrechtzuerhalten und Datenbank-Bloat zu verhindern.

## ⚠️ Wichtiger Hinweis {#important-notice}

**PostgreSQL-Datenbanken verkleinern sich nicht automatisch, wenn Daten gelöscht werden.** Wenn Sie E-Mails oder Postfächer löschen, markiert die Datenbank den Speicherplatz als verfügbar für die Wiederverwendung, aber die tatsächliche Größe der Datenbankdatei bleibt gleich. Dieses Phänomen wird als „Database Bloat“ bezeichnet.

Mail Archiver enthält einen automatischen Wartungsdienst, der täglich VACUUM ANALYZE-Operationen ausführt, um:
- Speicherplatz für die Wiederverwendung innerhalb der Datenbank zurückzugewinnen
- Abfrage-Statistiken für optimale Leistung zu aktualisieren
- Leistungsverschlechterungen im Laufe der Zeit zu verhindern
- Die Datenbankgesundheit aufrechtzuerhalten

## 🚀 Automatische Wartung (Empfohlen) {#automatic-maintenance-recommended}

### Konfiguration {#configuration}

Aktivieren Sie die automatische Datenbankwartung in Ihrem `appsettings.json` oder über Umgebungsvariablen in `docker-compose.yml`:

#### Option 1: Über appsettings.json {#option-1-via-appsettingsjson}

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### Option 2: Über docker-compose.yml (Empfohlen) {#option-2-via-docker-composeyml-recommended}

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Enable automatic database maintenance
      - DatabaseMaintenance__Enabled=true
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30
```

### Konfigurationsoptionen {#configuration-options}

| Parameter | Typ | Standard | Beschreibung |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Automatische Wartung aktivieren oder deaktivieren |
| `DailyExecutionTime` | string | `"02:00"` | Tageszeit für die Ausführung der Wartung (HH:mm-Format, 24-Stunden) |
| `TimeoutMinutes` | integer | `30` | Maximale Zeit für Wartungsoperationen in Minuten |

### Funktionsweise {#how-it-works}

Wenn aktiviert, führt der Wartungsdienst folgende Schritte aus:

1. **Startet automatisch** mit der Anwendung
2. **Wartet**, bis die konfigurierte Ausführungszeit erreicht ist
3. **Führt VACUUM ANALYZE** auf der gesamten Datenbank aus
4. **Protokolliert den Vorgang** im Zugriffsprotokoll der Anwendung inklusive Dauer
5. **Plant den nächsten Termin** für den nächsten Tag ein

Die Wartungsoperation:
- ✅ Läuft ohne Ausfallzeiten
- ✅ Beeinträchtigt den normalen Anwendungsbetrieb nicht
- ✅ Bietet vollständige Protokollierung und Überwachung
- ✅ Gibt Speicherplatz für die Wiederverwendung innerhalb der Datenbank frei
- ✅ Aktualisiert Abfrage-Statistiken für eine bessere Performance

### Auswahl der Ausführungszeit {#choosing-the-execution-time}

Wählen Sie eine Zeit, zu der Ihr System die geringste Auslastung aufweist:
- **Empfohlen**: Zwischen 02:00 und 04:00 Uhr (Nachtstunden)
- **Beachten Sie**: Ihre Zeitzoneneinstellung
- **Vermeiden Sie**: Spitzenlastzeiten oder Zeitfenster für die Sicherung

Beispielkonfigurationen:

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### Überwachung der automatischen Wartung {#monitoring-automatic-maintenance}

#### 1. Anwendungsprotokolle {#1-application-logs}

Überprüfen Sie die Anwendungsprotokolle auf Wartungsaktivitäten:

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. Zugriffsprotokoll (UI) {#2-access-log-ui}

Alle Wartungsoperationen werden im Zugriffsprotokoll der Anwendung protokolliert:

1. Navigieren Sie in der Benutzeroberfläche zu **Logs** → **Access Log**
2. Filtern Sie nach Typ: **Database Maintenance**
3. Sehen Sie Ausführungszeiten, Dauer und etwaige Fehler ein

## 🔧 Manuelle Wartung (Erweitert) {#manual-maintenance-advanced}

### VACUUM FULL zur Wiederherstellung von Festplattenspeicher {#vacuum-full-for-disk-space-recovery}

**⚠️ Erfordert Anwendungsstillstand**

VACUUM FULL komprimiert die Datenbank und gibt Festplattenspeicher an das Betriebssystem zurück, erfordert jedoch exklusive Sperren.

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**Wann anzuwenden:**
- Nach dem Löschen großer Postfächer
- Um signifikanten Festplattenspeicher zurückzugewinnen
- Während geplanter Wartungsfenster

**Wichtig:**
- Erfordert freien Festplattenspeicher in Höhe der Tabellengröße
- Kann bei großen Datenbanken Stunden dauern
- Blockiert alle Datenbankoperationen
- Sollte während Zeiten mit geringer Auslastung geplant werden

Detaillierte manuelle Verfahren finden Sie in der [PostgreSQL VACUUM Dokumentation](https://www.postgresql.org/docs/current/sql-vacuum.html).

## 📚 Verwandte Dokumentation {#related-documentation}

- [Setup Guide](Setup.md) - Erstkonfiguration und Umgebungsvariablen
- [Backup and Restore Guide](BackupRestore.md) - Datenbank-Sicherungungsverfahren
- [PostgreSQL VACUUM Documentation](https://www.postgresql.org/docs/current/sql-vacuum.html) - Offizielle PostgreSQL-Dokumentation

## 📝 Zusammenfassung {#summary}

✅ **Automatische Wartung aktivieren** für eine automatisierte Datenbankoptimierung
✅ **Über Zugriffsprotokolle überwachen**, um erfolgreiche Vorgänge zu verifizieren
✅ **Manuelles VACUUM FULL verwenden**, nur wenn die Wiederherstellung von Festplattenspeicher erforderlich ist
✅ **VACUUM FULL planen** während Wartungsfenstern für große Löschvorgänge
✅ **Sicherungen aktuell halten** vor allen manuellen Wartungsarbeiten

Der automatische Wartungsdienst übernimmt die tägliche Datenbankoptimierung, sodass in den meisten Fällen kein manuelles Eingreifen erforderlich ist.
