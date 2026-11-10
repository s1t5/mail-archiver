# 🔄 PostgreSQL Major Version Upgrade Guide {#postgresql-major-version-upgrade-guide}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden erklärt, wie Sie die PostgreSQL-Datenbank in Ihrer Mail Archiver Docker Compose-Umgebung von einer Hauptversion auf eine andere aktualisieren. Upgrades der Hauptversion sind notwendig, um von neuen PostgreSQL-Funktionen, Leistungsverbesserungen und Sicherheitsupdates zu profitieren.

## ⚠️ Wichtiger Haftungsausschluss {#important-disclaimer}
> **Hinweis**: Dieses Upgrade-Verfahren ist ein allgemeiner Migrationsprozess für PostgreSQL-Datenbanken und ist nicht spezifisch für Mail Archiver. Es wird als Empfehlung basierend auf PostgreSQL-Best Practices bereitgestellt. Testen Sie das Verfahren immer zuerst in einer Nicht-Produktionsumgebung und stellen Sie sicher, dass Sie über angemessene Sicherungen verfügen, bevor Sie fortfahren.


**Alle in diesem Leitfaden beschriebenen Verfahren sollten zuerst in einer Nicht-Produktionsumgebung getestet werden.** Major-Version-Upgrades beinhalten Datenmigrationen und bergen Risiken von Datenverlust oder Datenbeschädigung. Beachten Sie immer:
- Erstellen Sie eine vollständige Sicherung, bevor Sie den Upgrade-Prozess starten
- Testen Sie das Upgrade-Verfahren in einer Entwicklungsumgebung
- Überprüfen Sie nach dem Upgrade alle Anwendungsfunktionen
- Halten Sie einen Rollback-Plan für den Fall von Problemen bereit
- Planen Sie Upgrades während Wartungsfenstern ein

**Die Nutzung erfolgt auf eigene Gefahr.**

## 🎯 Wann Sie ein Upgrade durchführen sollten {#when-to-upgrade}

Ein Upgrade von PostgreSQL ist in folgenden Fällen in Betracht zu ziehen:
- Ihre aktuelle Version nähert sich dem Ende des Lebenszyklus (End-of-Life)
- Sicherheitsupdates erfordern eine neuere Version
- Sie planen ein größeres Wartungsfenster für das System

## 🛠️ Voraussetzungen {#prerequisites}

- Lauffende Mail Archiver Docker-Installation (siehe [Setup Guide](Setup.md))
- Ausreichend Speicherplatz (mindestens das 1,5-fache der aktuellen Datenbankgröße)
- Zugriff auf das Docker-Hostsystem
- Vollständige Sicherung der aktuellen Datenbank (siehe [Backup and Restore Guide](BackupRestore.md))
- Wartungsfenster für die Ausfallzeit der Anwendung

## 📋 Aktuelle Konfiguration {#current-configuration}

Ihr aktuelles Mail Archiver Setup verwendet:
- **Datenbankname**: MailArchiver
- **Datenbankbenutzer**: mailuser
- **Datenvolumen**: `./postgres-data` gemappt auf `/var/lib/postgresql/data`

Falls erforderlich, muss dies in den folgenden Beispielbefehlen und Skripten angepasst werden.

## 🔄 Upgrade-Verfahren {#upgrade-procedures}

Diese Methode erstellt eine logische Sicherung Ihrer Daten und stellt diese in der neuen PostgreSQL-Version wieder her. Dies ist der sicherste Ansatz und funktioniert über alle PostgreSQL-Versionen hinweg.

> **Alternative Methoden**: PostgreSQL bietet auch andere Upgrade-Methoden wie `pg_upgrade` für schnellere Upgrades an. Weitere Informationen zu alternativen Upgrade-Ansätzen finden Sie in der [offiziellen PostgreSQL-Upgrade-Dokumentation](https://www.postgresql.org/docs/current/upgrading.html).

#### Schritt 1: Umgebung vorbereiten {#step-1-prepare-the-environment}

```bash
# 1. Stop the application container (keep database running for backup)
docker compose stop mailarchive-app

# 2. Create a backup directory
mkdir -p postgres-backup

# 3. Create a database dump
echo "Export DB content (this can take some time)"
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > postgres-backup/database_dump.sql

# 4. Verify the dump was created successfully
ls -la postgres-backup/database_dump.sql

# 5. Stop the database container
docker compose down
```

#### Schritt 2: Docker Compose Konfiguration aktualisieren {#step-2-update-docker-compose-configuration}

Bearbeiten Sie Ihre `docker-compose.yml` Datei, um die neue PostgreSQL-Version zu verwenden:

```yaml
  postgres:
    image: postgres:18  # Changed from old version
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data-new:/var/lib/postgresql  # New data directory
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
```

**Wichtige Änderungen:**
- `image` von `postgres:17` auf `postgres:18` aktualisiert (oder Ihre Zielversion)
- Volume-Mapping von `./postgres-data:/var/lib/postgresql/data` zu `./postgres-data-new:/var/lib/postgresql` geändert (die Änderung des internen Pfads ist ab Version 18 und fortlaufend für postgres erforderlich)

#### Schritt 3: Neue PostgreSQL-Instanz starten {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### Schritt 4: Daten in die neue Datenbank wiederherstellen {#step-4-restore-data-to-new-database}

Stellen Sie den Datenbank-Dump in der neuen PostgreSQL-Instanz wieder her:

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
Dies kann je nach Datenbankgröße einige Zeit in Anspruch nehmen
Achten Sie während der Wiederherstellung auf Fehlermeldungen

#### Schritt 5: Testen und Migration abschließen {#step-5-test-and-complete-migration}
Starten Sie die Anwendung, um die Verbindung zu testen:

```bash
docker compose up -d
```

Testen Sie die Funktionalität der Anwendung:
- Melden Sie sich an der Weboberfläche an
- Überprüfen Sie, ob die E-Mail-Suche funktioniert
- Prüfen Sie, ob vorhandene Daten zugänglich sind
- Testen Sie die Sicherung/Wiederherstellung-Funktionalität

Wenn alles korrekt funktioniert, bereinigen Sie die alten Daten:

Container stoppen
```bash
docker compose down
```
Alten Datenverzeichnis entfernen (erst nach Bestätigung, dass das neue Setup funktioniert)
```bash
rm -rf postgres-data
```
Das Archiv-Verzeichnis entfernen
```bash
rm -rf postgres-backup
```

Neues Datenverzeichnis in Standardnamen umbenennen
```bash
mv postgres-data-new postgres-data
```

docker-compose.yml aktualisieren, um den Standard-Volumenamen zu verwenden:
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Das gesamte System starten
```bash
docker compose up -d
```

## ⚠️ Wichtige Überlegungen {#important-considerations}

### Downtime-Planung {#downtime-planning}

- Je nach System und Datenbankgröße kann dieser Vorgang lange dauern.
- Planen Sie dies während Wartungsfenstern ein

### Festplattenspeicher-Anforderungen {#disk-space-requirements}

Stellen Sie sicher, dass ausreichend Festplattenspeicher vorhanden ist (ca. das Zweifache Ihrer Datenbankgröße)

Verfügbaren Speicher prüfen:
```bash
df -h
du -sh postgres-data/
```

### Rollback-Plan {#rollback-plan}

Bereiten Sie sich immer auf einen Rollback vor:
1. Behalten Sie das alte Datenverzeichnis bei, bis das Upgrade verifiziert ist
2. Behalten Sie die alte `docker-compose.yml` Sicherung bei
3. Testen Sie das Rollback-Verfahren zuerst in der Entwicklungsumgebung

### Überprüfung nach dem Upgrade {#post-upgrade-verification}

Überprüfen Sie nach dem Upgrade:
- Anwendungsverbindung zur Datenbank
- E-Mail-Such- und AbrufFunktionalität
- Benutzerauthentifizierung und Sitzungsverwaltung
- Sicherung und Wiederherstellung Funktionalität
- Datenbank-Leistungskennzahlen

## 📚 Verwandte Dokumentation {#related-documentation}

- [Backup and Restore Guide](BackupRestore.md) - Essenziell für Sicherungen vor dem Upgrade
- [Database Maintenance Guide](DatabaseMaintenance.md) - Wartung nach dem Upgrade
- [Setup Guide](Setup.md) - Docker Compose Konfiguration
- [PostgreSQL Release Notes](https://www.postgresql.org/docs/release/) - Offizielle Release-Dokumentation
