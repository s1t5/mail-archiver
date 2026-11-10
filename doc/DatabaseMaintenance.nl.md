# 🔧 Database Maintenance Guide {#database-maintenance-guide}

[← Terug naar de Documentatie-index](index.md)

## 📋 Overzicht {#overview}

Deze gids legt uit hoe u de automatische database-onderhoudsfunctie in Mail Archiver kunt configureren en gebruiken. De applicatie bevat een ingebouwde achtergrondservice die automatisch PostgreSQL-database-onderhoudsacties uitvoert om optimale prestaties te behouden en database-bloat te voorkomen.

## ⚠️ Belangrijke mededeling {#important-notice}

**PostgreSQL-databases krimpen niet automatisch wanneer gegevens worden verwijderd.** Wanneer u e-mails of Postbussen verwijdert, markeert de database de ruimte als beschikbaar voor hergebruik, maar de werkelijke grootte van het databasebestand blijft gelijk. Dit fenomeen wordt "database bloat" genoemd.

Mail Archiver bevat een automatische onderhoudsservice die dagelijks VACUUM ANALYZE-acties uitvoert om:
- Opslagruimte terug te winnen voor hergebruik binnen de database
- Querystatistieken bij te werken voor optimale prestaties
- Prestatievermindering in de loop van de tijd te voorkomen
- De gezondheid van de database te behouden

## 🚀 Automatisch onderhoud (Aanbevolen) {#automatic-maintenance-recommended}

### Configuratie {#configuration}

Schakel automatisch database-onderhoud in via uw `appsettings.json` of via omgevingsvariabelen in `docker-compose.yml`:

#### Optie 1: Via appsettings.json {#option-1-via-appsettingsjson}

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### Optie 2: Via docker-compose.yml (Aanbevolen) {#option-2-via-docker-composeyml-recommended}

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

### Configuratie-opties {#configuration-options}

| Parameter | Type | Standaard | Beschrijving |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Schakel automatisch onderhoud in of uit |
| `DailyExecutionTime` | string | `"02:00"` | Tijdstip van de dag waarop onderhoud wordt uitgevoerd (formaat HH:mm, 24-uurs) |
| `TimeoutMinutes` | integer | `30` | Maximale toegestane tijd voor onderhoudsacties in minuten |

### Hoe het werkt {#how-it-works}

Wanneer ingeschakeld, voert de onderhoudsservice het volgende uit:

1. **Start automatisch** met de applicatie
2. **Wacht** tot de geconfigureerde uitvoeringstijd
3. **Voert VACUUM ANALYZE uit** op de gehele database
4. **Logt de actie** in het access log van de applicatie inclusief duur
5. **Plant opnieuw in** voor de volgende dag

De onderhoudsactie:
- ✅ Draait zonder downtime
- ✅ Verstoort normale applicatie-acties niet
- ✅ Biedt volledige logging en monitoring
- ✅ Wint ruimte terug voor hergebruik binnen de database
- ✅ Update querystatistieken voor betere prestaties

### Het kiezen van de uitvoeringstijd {#choosing-the-execution-time}

Selecteer een tijdstip waarop uw systeem de minste activiteit heeft:
- **Aanbevolen**: Tussen 02:00 en 04:00 (nachturen)
- **Overweeg**: Uw tijdzone-instelling
- **Vermijd**: Piekuren of vensters voor een Back-up

Voorbeeldconfiguraties:

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### Monitoring van automatisch onderhoud {#monitoring-automatic-maintenance}

#### 1. Applicatielogs {#1-application-logs}

Controleer de applicatielogs voor onderhoudsactiviteiten:

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. Access Log (UI) {#2-access-log-ui}

Alle onderhoudsacties worden gelogd in het access log van de applicatie:

1. Navigeer naar **Logs** → **Access Log** in de UI
2. Filter op type: **Database Maintenance**
3. Bekijk uitvoeringstijden, duur en eventuele fouten

## 🔧 Handmatig onderhoud (Geavanceerd) {#manual-maintenance-advanced}

### VACUUM FULL voor herstel van schijfruimte {#vacuum-full-for-disk-space-recovery}

**⚠️ Vereist downtime van de applicatie**

VACUUM FULL comprimeert de database en geeft schijfruimte terug aan het besturingssysteem, maar vereist exclusieve vergrendelingen (locks).

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**Wanneer te gebruiken:**
- Na het verwijderen van grote Postbussen
- Om aanzienlijke schijfruimte terug te winnen
- Tijdens geplande onderhoudsvensters

**Belangrijk:**
- Vereist vrije schijfruimte gelijk aan de tabelgrootte
- Kan uren duren bij grote databases
- Blokkeert alle database-acties
- Moet worden gepland tijdens daluren

Zie voor gedetailleerde handmatige procedures de [PostgreSQL VACUUM Documentatie](https://www.postgresql.org/docs/current/sql-vacuum.html).

## 📚 Gerelateerde documentatie {#related-documentation}

- [Setup Guide](Setup.md) - Initiële configuratie en omgevingsvariabelen
- [Backup and Restore Guide](BackupRestore.md) - Procedures voor database Back-up en Herstel
- [PostgreSQL VACUUM Documentatie](https://www.postgresql.org/docs/current/sql-vacuum.html) - Officiële PostgreSQL-documentatie

## 📝 Samenvatting {#summary}

✅ **Schakel automatisch onderhoud in** voor hands-off database-optimalisatie
✅ **Monitor via access logs** om succesvolle acties te verifiëren
✅ **Gebruik handmatige VACUUM FULL** alleen wanneer herstel van schijfruimte nodig is
✅ **Plan VACUUM FULL** tijdens onderhoudsvensters bij grote verwijderingen
✅ **Houd Back-ups up-to-date** vóór eventuele handmatige onderhoudsacties

De automatische onderhoudsservice regelt de dagelijkse database-optimalisatie, waardoor handmatige interventie in de meeste gevallen overbodig is.
