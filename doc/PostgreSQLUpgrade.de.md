# 🔄 PostgreSQL Major Version Upgrade Guide {#postgresql-major-version-upgrade-guide}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Diese Anleitung erklärt, wie Sie die PostgreSQL-Datenbank in Ihrer Mail Archiver Docker Compose Umgebung von einer Major-Version auf eine andere aktualisieren. Major-Version-Upgrades sind notwendig, um von neuen PostgreSQL-Funktionen, Leistungsverbuchungen und Sicherheitsupdates zu profitieren.

## ⚠️ Wichtiger Haftungsausschluss {#important-disclaimer}
> **Hinweis**: Dieses Upgrade-Verfahren ist ein allgemeiner Migrationsprozess für PostgreSQL-Datenbanken und ist nicht spezifisch für Mail Archiver. Es wird als Empfehlung basierend auf PostgreSQL Best Practices bereitgestellt. Testen Sie das Verfahren immer zuerst in einer Nicht-Produktionsumgebung und stellen Sie sicher, dass Sie vor dem Fortfahren über angemessene Sicherungen verfügen.


**Alle in dieser Anleitung beschriebenen Verfahren sollten zuerst in einer Nicht-Produktionsumgebung getestet werden.** Major-Version-Upgrades beinhalten eine Datenmigration und bergen Risiken von Datenverlust oder Datenbeschädigung. Tun Sie immer Folgendes:
- Erstellen Sie eine vollständige Sicherung vor Beginn des Upgrade-Prozesses
- Testen Sie das Upgrade-Verfahren in einer Entwicklungsumgebung
- Überprüfen Sie nach dem Upgrade die gesamte Anwendungsfunktionalität
- Haben Sie einen Rollback-Plan für den Fall von Problemen bereit
- Planen Sie Upgrades während Wartungsfenstern ein

**Verwendung auf eigene Gefahr.**

## 🎯 Wann Sie upgraden sollten {#when-to-upgrade}

Ein Upgrade von PostgreSQL ist sinnvoll, wenn:
- Ihre aktuelle Version dem Ende des Lebenszyklus (End-of-Life) entgegengeht
- Sicherheitsupdates eine neuere Version erfordern
- Sie ein größeres System-Wartungsfenster planen

## 🛠️ Voraussetzungen {#prerequisites}

- Ausführung des Mail Archiver Docker-Setups (siehe [Setup Guide](Setup.md))
- Ausreichend Speicherplatz (mindestens 1,5x die aktuelle Datenbankgröße)
- Zugriff auf das Docker-Hostsystem
- Vollständige Sicherung der aktuellen Datenbank (siehe [Backup und Wiederherstellungs-Leitfaden](BackupRestore.md))
- Wartungsfenster für die Ausfallzeit der Anwendung

## 📋 Aktuelle Konfiguration {#current-configuration}

Ihr aktuelles Mail Archiver Setup verwendet:
- **Datenbankname**: MailArchiver
- **Datenbank-Benutzer**: mailuser
- **Daten-Volume**: `./postgres-data` gemappt auf `/var/lib/postgresql/data`

Falls erforderlich, muss dies in den folgenden Beispielbefehlen und Skripten angepasst werden.

## 🔄 Upgrade-Verfahren {#upgrade-procedures}

Diese Methode erstellt ein logisches Backup Ihrer Daten und stellt es in der neuen PostgreSQL-Version wieder her. Dies ist der sicherste Ansatz und funktioniert über alle PostgreSQL-Versionen hinweg.

> **Alternative Methoden**: PostgreSQL bietet auch andere Upgrade-Methoden wie `pg_upgrade` für schnellere Upgrades an. Weitere Informationen zu alternativen Upgrade-Ansätzen finden Sie in der [offiziellen PostgreSQL Upgrade-Dokumentation](https://www.postgresql.org/docs/current/upgrading.html).

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
- Aktualisiertes `image` von `postgres:17` auf `postgres:18` (oder Ihre Zielversion)
- Geändertes Volume-Mapping von `./postgres-data:/var/lib/postgresql/data` zu `./postgres-data-new:/var/lib/postgresql` (die Änderung des internen Pfads ist für Version 18 und folgende erforderlich)

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
Achten Sie auf etwaige Fehlermeldungen während der Wiederherstellung

#### Schritt 5: Testen und Migration abschließen {#step-5-test-and-complete-migration}
Starten Sie die Anwendung, um die Verbindung zu testen:

```bash
docker compose up -d
```

Testen Sie die Anwendungsfunktionalität:
- Loggen Sie sich in die Weboberfläche ein
- Überprüfen Sie, ob die E-Mail-Suche funktioniert
- Prüfen Sie, ob die vorhandenen Daten zugänglich sind
- Testen Sie die Backup/Wiederherstellungs-Funktionalität

Wenn alles korrekt funktioniert, bereinigen Sie die alten Daten:

Stoppen Sie die Container
```bash
docker compose down
```
Entfernen Sie das alte Datenverzeichnis (nur nach Bestätigung, dass das neue Setup funktioniert)
```bash
rm -rf postgres-data
```
Entfernen Sie das Backup-Verzeichnis
```bash
rm -rf postgres-backup
```

Benennen Sie das neue Datenverzeichnis in den Standardnamen um
```bash
mv postgres-data-new postgres-data
```

Aktualisieren Sie docker-compose.yml, um den Standard-Volume-Namen zu verwenden:
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Starten Sie das gesamte System
```bash
docker compose up -d
```

## ⚠️ Wichtige Überlegungen {#important-considerations}

### Planung der Ausfallzeit {#downtime-planning}

- Je nach System und Datenbankgröße kann dieser Vorgang lange dauern.
- Planen Sie dies während Wartungsfenstern ein.

### Speicherplatzanforderungen {#disk-space-requirements}

Stellen Sie sicher, dass ausreichender Speicherplatz vorhanden ist (ca. das Zweifache Ihrer Datenbankgröße).

Überprüfen Sie den verfügbaren Speicherplatz:
```bash
df -h
du -sh postgres-data/
```

### Rollback-Plan {#rollback-plan}

Bereiten Sie sich immer auf einen Rollback vor:
1. Behalten Sie das alte Datenverzeichnis bei, bis das Upgrade verifiziert wurde
2. Behalten Sie das alte `docker-compose.yml` Backup bei
3. Testen Sie das Rollback-Verfahren zuerst in einer Entwicklungsumgebung

### Verifizierung nach dem Upgrade {#post-upgrade-verification}

Überprüfen Sie nach dem Upgrade:
- Verbindung der Anwendung zur Datenbank
- E-Mail-Such- und Abruf-Funktionalität
- Benutzerauthentifizierung und Sitzungsverwaltung
- Backup und Wiederherstellungs-Funktionalität
- Datenbank-Leistungskennzahlen

## 📚 Verwandte Dokumentation {#related-documentation}

- [Backup und Wiederherstellungs-Leitfaden](BackupRestore.md) - Essentiell für Backups vor dem Upgrade
- [Datenbank-Wartungsleitfaden](DatabaseMaintenance.md) - Wartung nach dem Upgrade
- [Setup Guide](Setup.md) - Docker Compose Konfiguration
- [PostgreSQL Release Notes](https://www.postgresql.org/docs/release/) - Offizielle Release-Dokumentation
