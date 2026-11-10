# 🔄 PostgreSQL Major Version Upgrade Guide {#postgresql-major-version-upgrade-guide}

[← Terug naar de Documentatie-index](index.md)

## 📋 Overzicht {#overview}

Deze gids legt uit hoe u de PostgreSQL-database van de ene naar de andere hoofdversie kunt upgraden in uw Mail Archiver Docker Compose-omgeving. Upgrades van de hoofdversie zijn noodzakelijk om gebruik te maken van nieuwe PostgreSQL-functies, prestatieverbeteringen en beveiligingsupdates.

## ⚠️ Belangrijke Disclaimer {#important-disclaimer}
> **Let op**: Deze upgradeprocedure is een algemeen migratieproces voor PostgreSQL-databases en is niet specifiek voor Mail Archiver. Het wordt verstrekt als een aanbeveling op basis van PostgreSQL best practices. Test de procedure altijd eerst in een niet-productieomgeving en zorg ervoor dat u over de juiste Back-ups beschikt voordat u doorgaat.


**Alle beschreven procedures in deze gids moeten eerst in een niet-productieomgeving worden getest.** Upgrades van de hoofdversie omvatten datamigratie en brengen risico's op gegevensverlies of corruptie met zich mee. Doe altijd het volgende:
- Maak een volledige Back-up aan voordat u het upgradeproces start
- Test de upgradeprocedure in een ontwikkelomgeving
- Verifieer alle applicatiefunctionaliteit na de upgrade
- Zorg voor een rollback-plan voor het geval er problemen optreden
- Plan upgrades tijdens onderhoudsvensters in

**Gebruik op eigen risico.**

## 🎯 Wanneer te upgraden {#when-to-upgrade}

Overweeg het upgraden van PostgreSQL wanneer:
- Uw huidige versie de einddatum van de ondersteuning (end-of-life) nadert
- Beveiligingsupdates een nieuwere versie vereisen
- U een groot systeemonderhoudsvenster plant

## 🛠️ Vereisten {#prerequisites}

- Draaiende Mail Archiver Docker-setup (zie [Setup Guide](Setup.md))
- Voldoende schijfruimte (minimaal 1,5x de huidige databasegrootte)
- Toegang tot het Docker host-systeem
- Volledige Back-up van de huidige database (zie [Backup and Restore Guide](BackupRestore.md))
- Onderhoudsvenster voor downtime van de applicatie

## 📋 Huidige Configuratie {#current-configuration}

Uw huidige Mail Archiver-setup gebruikt:
- **Database Name**: MailArchiver
- **Database User**: mailuser
- **Data Volume**: `./postgres-data` gekoppeld aan `/var/lib/postgresql/data`

Indien nodig moet dit worden aangepast in de volgende voorbeeldcommando's en scripts.

## 🔄 Upgrade Procedures {#upgrade-procedures}

Deze methode maakt een logische Back-up van uw gegevens aan en herstelt deze naar de nieuwe PostgreSQL-versie. Dit is de veiligste aanpak en werkt bij alle PostgreSQL-versies.

> **Alternatieve Methoden**: PostgreSQL biedt ook andere upgrade-methoden zoals `pg_upgrade` voor snellere upgrades. Raadpleeg voor meer informatie over alternatieve upgrade-aanpakken de [officiële PostgreSQL upgrade-documentatie](https://www.postgresql.org/docs/current/upgrading.html).

#### Stap 1: De omgeving voorbereiden {#step-1-prepare-the-environment}

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

#### Stap 2: Docker Compose-configuratie bijwerken {#step-2-update-docker-compose-configuration}

Bewerk uw `docker-compose.yml`-bestand om de nieuwe PostgreSQL-versie te gebruiken:

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

**Belangrijke wijzigingen:**
- Bijgewerkte `image` van `postgres:17` naar `postgres:18` (of uw doelversie)
- Volume mapping gewijzigd van `./postgres-data:/var/lib/postgresql/data` naar `./postgres-data-new:/var/lib/postgresql` (de wijziging van het interne pad is vereist door versie 18 en verder)

#### Stap 3: Nieuwe PostgreSQL-instantie starten {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### Stap 4: Gegevens herstellen naar de nieuwe database {#step-4-restore-data-to-new-database}

Herstel de database dump naar de nieuwe PostgreSQL-instantie:

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
Dit kan enige tijd in beslag nemen, afhankelijk van de grootte van de database
Let op eventuele foutmeldingen tijdens het Herstel

#### Stap 5: Testen en migratie voltooien {#step-5-test-and-complete-migration}
Start de applicatie om de connectiviteit te testen:

```bash
docker compose up -d
```

Test de applicatiefunctionaliteit:
- Log in op de webinterface
- Verifieer of e-mail zoeken werkt
- Controleer of bestaande gegevens toegankelijk zijn
- Test de Back-up/Herstel-functionaliteit

Als alles correct werkt, ruim dan de oude gegevens op:

Stop containers
```bash
docker compose down
```
Verwijder de oude gegevensmap (alleen nadat is bevestigd dat de nieuwe setup werkt)
```bash
rm -rf postgres-data
```
Verwijder de Back-up map
```bash
rm -rf postgres-backup
```

Hernoem de nieuwe gegevensmap naar de standaardnaam
```bash
mv postgres-data-new postgres-data
```

Update docker-compose.yml om de standaard volumenaam te gebruiken:
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Start het volledige systeem
```bash
docker compose up -d
```

## ⚠️ Belangrijke overwegingen {#important-considerations}

### Planning van downtime {#downtime-planning}

- Afhankelijk van uw systeem en de grootte van de database kan deze procedure lang duren.
- Plan dit tijdens onderhoudsvensters.

### Vereisten voor schijfruimte {#disk-space-requirements}

Zorg voor voldoende schijfruimte (ongeveer twee keer uw databasegrootte).

Controleer de beschikbare ruimte:
```bash
df -h
du -sh postgres-data/
```

### Rollback-plan {#rollback-plan}

Bereid u altijd voor op een rollback:
1. Bewaar de oude gegevensmap totdat de upgrade is geverifieerd
2. Behoud de oude `docker-compose.yml` Back-up
3. Test de rollback-procedure eerst in ontwikkeling

### Verificatie na de upgrade {#post-upgrade-verification}

Verifieer na de upgrade:
- Connectiviteit van de applicatie met de database
- Functionaliteit voor het zoeken en ophalen van e-mails
- Gebruikersauthenticatie en sessiebeheer
- Back-up en Herstel-functionaliteit
- Database-prestatiegegevens

## 📚 Gerelateerde Documentatie {#related-documentation}

- [Backup and Restore Guide](BackupRestore.md) - Essentieel voor Back-ups vóór de upgrade
- [Database Maintenance Guide](DatabaseMaintenance.md) - Onderhoud na de upgrade
- [Setup Guide](Setup.md) - Docker Compose-configuratie
- [PostgreSQL Release Notes](https://www.postgresql.org/docs/release/) - Officiële release-documentatie
