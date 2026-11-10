# 🔄 Vodnik za nadgradnjo glavne različice PostgreSQL {#postgresql-major-version-upgrade-guide}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodnik pojasnjuje, kako nadgraditi PostgreSQL bazo podatkov iz ene glavne različice v drugo v vašem Mail Archiver Docker Compose okolju. Nadgradnje glavnih različic so nødvne za izkoriščanje novih funkcij PostgreSQL, izboljšav zmogljivosti in varnostnih posodobitev.

## ⚠️ Pomembno izjava o omejitvi odgovornosti {#important-disclaimer}
> **Opomba**: Ta postopek nadgradnje je splošni proces migracije PostgreSQL baze podatkov in ni specifičen za Mail Archiver. Predlagan je kot priporočilo na podlagi najboljih praks PostgreSQL. Postopek vedno najprej preizkusite v neprodukcijskem okolju in pred nadaljevanjem zagotovite ustrezne varnostne kopije.


**Vse postopke, opisane v tem vodniku, je treba najprej preizkusiti v neprodukcijskem okolju.** Nadgradnje glavnih različic vključujejo migracijo podatkov in nosijo tveganje izgube ali poškodbe podatkov. Vedno:
- Pred začetkom procesa nadgradnje naredite popolno varnostno kopijo
- Postopek nadgradnje preizkusite v razvojnem okolju
- Po nadgradnji preverite vso funkcionalnost aplikacije
- Pripravite načrt za vrnitev (rollback) v primer težav
- Nadgradnje načrtujte v okviru oken za vzdrževanje

**Uporabljate na lastno odgovornost.**

## 🎯 Kdaj nadgraditi {#when-to-upgrade}

Razmislite o nadgradnji PostgreSQL, ko:
- Vaša trenutna različica približuje koncu življenjske dobe (end-of-life)
- Varnostne posodobitve zahtevajo novejšo različico
- načrtujete dolgo obdobje vzdrževanja sistema

## 🛠️ Predpogoji {#prerequisites}

- Zažgali Mail Archiver Docker nastavitve (poglejte [Setup Guide](Setup.md))
- Zadostno diskovno mesto (vsaj 1,5x trenutne velikosti baze podatkov)
- Dostop do Docker gostiteljskega sistema
- Popolna varnostna kopija trenutne baze podatkov (poglejte [Backup and Restore Guide](BackupRestore.md))
- Okno za vzdrževanje (downtime) aplikacije

## 📋 Trenutna konfiguracija {#current-configuration}

Vaša trenutna Mail Archiver nastavitev uporablja:
- **Ime baze podatkov**: MailArchiver
- **Uporabnik baze podatkov**: mailuser
- **Volumen podatkov**: `./postgres-data`, mapiran na `/var/lib/postgresql/data`

Če je potrebno, je ta konfiguracija treba prilagoditi v spodnjih primerih ukazov in skript.

## 🔄 Postopki nadgradnje {#upgrade-procedures}

Ta metoda ustvari logično varnostno kopijo vaših podatkov in jo obnovi v novo različico PostgreSQL. Je najvarnejši pristop in deluje čez vse različice PostgreSQL.

> **Alternative metode**: PostgreSQL ponuja tudi druge metode nadgradnje, kot je `pg_upgrade` za hitrejše nadgradnje. Za več informacij o alternativnih pristopih k nadgradnji si oglejte [uradno dokumentacijo za nadgradnjo PostgreSQL](https://www.postgresql.org/docs/current/upgrading.html).

#### Korak 1: Priprava okolja {#step-1-prepare-the-environment}

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

#### Korak 2: Posodobitev Docker Compose konfiguracije {#step-2-update-docker-compose-configuration}

Uredite svojo datoteko `docker-compose.yml`, da uporabite novo različico PostgreSQL:

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

**Pomembne spremembe:**
- Posodobljena `image` iz `postgres:17` v `postgres:18` (ali vašo ciljno različico)
- Spremenjeno mapiranje volumena iz `./postgres-data:/var/lib/postgresql/data` v `./postgres-data-new:/var/lib/postgresql` (sprememba notranje poti je zahtevana od različice 18 naprej za postgres)

#### Korak 3: Zažganje nove instance PostgreSQL {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### Korak 4: Obnova podatkov v novo bazo podatkov {#step-4-restore-data-to-new-database}

Obnovite izvoz baze podatkov v novo instanco PostgreSQL:

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
To lahko traja nekaj časa, odvisno od velikosti baze podatkov
Bodite pozorni na morebitna sporočila o napakah med obnovo

#### Korak 5: Testiranje in zaključek migracije {#step-5-test-and-complete-migration}
Zaženite aplikacijo za preverjanje povezljivosti:

```bash
docker compose up -d
```

Preverite funkcionalnost aplikacije:
- Prijavite se v spletni vmesnik
- Preverite, ali iskanje e-pošte deluje
- Preverite, ali so obstoječi podatki dostopni
- Preizkusite funkcionalnost varnostne kopije/obnove

Če vse deluje pravilno, očistite stare podatke:

Ustavite kontejnerje
```bash
docker compose down
```
Odstranite staro mapo s podatki (šele po potrditvi, da nova nastavitev deluje)
```bash
rm -rf postgres-data
```
Odstranite mapo z varnostno kopijo
```bash
rm -rf postgres-backup
```

Preimenujte novo mapo s podatki na standardno ime
```bash
mv postgres-data-new postgres-data
```

Posodobite docker-compose.yml, da uporabljate standardno ime volumena:
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Zaženite celoten sistem
```bash
docker compose up -d
```

## ⚠️ Pomembna obravnavanja {#important-considerations}

### Načrtovanje izpadov (Downtime) {#downtime-planning}

- Odvisno od vašega sistema in velikosti baze podatkov lahko ta postopek traja dolgo časa.
- Načrtujte v okviru oken za vzdrževanje.

### Zahteve glede prostora na disku {#disk-space-requirements}

Zagotovite zadostno prostora na disku (približno dvojno velikost vaše baze podatkov).

Preverite razpoložljivo prostino:
```bash
df -h
du -sh postgres-data/
```

### Načrt za vrnitev (Rollback) {#rollback-plan}

Vedno se pripravite na vrnitev:
1. Obdržite staro mapo s podatki, dokler nadgradnja ni preverjena
2. Shranite staro `docker-compose.yml` varnostno kopijo
3. Postopek vrnitve najprej preizkusite v razvojnem okolju

### Preverjanje po nadgradnji {#post-upgrade-verification}

Po nadgradnji preverite:
- Povezljivost aplikacije z bazo podatkov
- Funkcionalnost iskanja in pridobivanja e-pošte
- Avtentikacijo uporabnikov in upravljanje z zaposedi (sessions)
- Funkcionalnost varnostne kopije in obnove
- Metrike zmogljivosti baze podatkov

## 📚 Sorodna dokumentacija {#related-documentation}

- [Backup and Restore Guide](BackupRestore.md) - Nujno za varnostne kopije pred nadgradnjo
- [Database Maintenance Guide](DatabaseMaintenance.md) - Vzdrževanje po nadgradnji
- [Setup Guide](Setup.md) - Docker Compose konfiguracija
- [PostgreSQL Release Notes](https://www.postgresql.org/docs/release/) - Uradna dokumentacija o izdajah
