# 🔄 PostgreSQL Major Version Upgrade Guide {#postgresql-major-version-upgrade-guide}

[← Vissz a dokumentációs indexre](index.md)

## 📋 Áttekintés {#overview}

Ez a útmutató elmagyarázza, hogyan frissítheti a PostgreSQL adatbázist egyik főverzióból a másikba a Mail Archiver Docker Compose környezetében. A főverziós frissítések szükségesek az új PostgreSQL funkciók, teljesítményjavítások és biztonsági frissítések kihasználásához.

## ⚠️ Fontos nyilatkozat {#important-disclaimer}
> **Megjegyzés**: Ez a frissítési folyamat egy általános PostgreSQL adatbázis-migrációs folyamat, és nem kifejezetten a Mail Archiverre vonatkozik. A PostgreSQL legjobb gyakorlatai alapján ajánlott módszerkéntக்குழு. Mindig először tesztelje a folyamatot nem-termelési környezetben, és győződjön meg róla, hogy rendelkezik megfelelő biztonsági mentésekkel a folytatás előtt.


**Ebben az útmutatóban leírt minden folyamatot először nem-termelési környezetben kell tesztelni.** A főverziós frissítések adatmigrációt involveálnak, és az adatvesztés vagy adatkorrupció kockázatával járnak. Mindig:
- Készítsen teljes biztonsági mentést a frissítési folyamat megkezdése előtt
- Tesztelje a frissítési folyamatot fejlesztői környezetben
- A frissítés után ellenőrizze az összes alkalmazásfunkciót
- Legyen rendelkezésre rollback terv az esetleges hibák esetén
- A frissítéseket karbantartási ablakokban ütemezze be

**A használat saját felelősségére történik.**

## 🎯 Mikor frissítsen {#when-to-upgrade}

Fontoljon növelést a PostgreSQL verzióra, ha:
- A jelenlegi verziója a támogatási időszak (end-of-life) lejárata felé halad
- A biztonsági frissítések újabb verziót igényelnek
- Nagyobb rendszerkarbantartási ablakot tervez

## 🛠️ Előfeltételek {#prerequisites}

- Futó Mail Archiver Docker beállítás (lásd: [Setup Guide](Setup.md))
- Eléglelt tárhely (legalább az aktuális adatbázis méretének 1,5-szere)
- Hozzáférés a Docker host rendszerhez
- Az aktuális adatbázis teljes biztonsági mentése (lásd: [Backup and Restore Guide](BackupRestore.md))
- Karbantartási ablak az alkalmazás szünetelésehez

## 📋 Aktuális konfiguráció {#current-configuration}

Az Ön jelenlegi Mail Archiver beállítása a következőket használja:
- **Database Name**: MailArchiver
- **Database User**: mailuser
- **Data Volume**: `./postgres-data` térképezve a `/var/lib/postgresql/data` helyre

Szükség esetén ezt módosítani kell az alábbi példamondatokban és szkriptekben.

## 🔄 Frissítési folyamatok {#upgrade-procedures}

Ez a módszer logikai mentést készít az adatokról, majd visszahelyezi azokat az új PostgreSQL verzióra. Ez a legbiztonságosabb megközelítés, és bármilyen PostgreSQL verzió között működik.

> **Alternatív módszerek**: A PostgreSQL egyéb frissítési módszereket is kínál, például a `pg_upgrade` gyorsabb frissítésekhez. További információ az alternatív frissítési megközelítésekről, kérjük, tekintjen a [hivatalos PostgreSQL upgrade dokumentációra](https://www.postgresql.org/docs/current/upgrading.html).

#### 1. lépés: A környezet előkészítése {#step-1-prepare-the-environment}

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

#### 2. lépés: A Docker Compose konfiguráció frissítése {#step-2-update-docker-compose-configuration}

Módosítsa a `docker-compose.yml` fájlt az új PostgreSQL verzió használatához:

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

**Fontos változások:**
- Frissített `image` az `postgres:17`-ről `postgres:18`-ra (vagy az Ön célverziójára)
- Megváltozott a volume mapping az `./postgres-data:/var/lib/postgresql/data`-től a `./postgres-data-new:/var/lib/postgresql`-ig (a belső útvonal változtatása szükséges a 18-as verziótól kezdődően a postgresnél)

#### 3. lépés: Új PostgreSQL példány indítása {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### 4. lépés: Adatok visszahelyezése az új adatbázisba {#step-4-restore-data-to-new-database}

Helyezze vissza az adatbázis dump fájlt az új PostgreSQL példányba:

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
Ez eltarthat egy ideig az adatbázis méretétől függően
Figyeljen az esetleges hibaüzenetekre a visszaállítás során

#### 5. lépés: Tesztelés és a migráció befejezése {#step-5-test-and-complete-migration}
Indítsa el az alkalmazást a kapcsolat teszteléséhez:

```bash
docker compose up -d
```

Tesztelje az alkalmazás funkcióit:
- Lépjen be a webes felületre
- Ellenőrizze, hogy az e-mail keresés működik-e
- Ellenőrizze, hogy a meglévő adatok elérhetőek-e
- Tesztelje a mentés/visszaállítás funkciókat

Ha minden megfelelően működik, törölje a régi adatokat:

Állítsa le a konténereket
```bash
docker compose down
```
Távolítsa el a régi adat directory-t (csak miután megbizonyosodott arról, hogy az új beállítás működik)
```bash
rm -rf postgres-data
```
Távolítsa el a biztonsági mentési directory-t
```bash
rm -rf postgres-backup
```

Nevezze át az új adat directory-t a standard névre
```bash
mv postgres-data-new postgres-data
```

Frissítse a docker-compose.yml fájlt, hogy a standard volume nevet használja:
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Indítsa el a teljes rendszert
```bash
docker compose up -d
```

## ⚠️ Fontos megfontolandók {#important-considerations}

### Időtervezés (Downtime) {#downtime-planning}

- A rendszertől és az adatbázis méretétől függően ez a folyamat hosszú ideig eltarthat.
- Ütemezze a karbantartási ablakokba.

### Tárhelykövetelmények {#disk-space-requirements}

Győződjön meg az adekvát tárhelyről (kb. kétszer az adatbázis mérete).

Ellenőrizze a szabad helyet:
```bash
df -h
du -sh postgres-data/
```

### Rollback terv {#rollback-plan}

Mindig készüljön fel a visszaállásra (rollback):
1. Tartsa meg a régi adat directory-t, amíg a frissítés nem igazolódik
2. Tartsa meg a régi `docker-compose.yml` mentést
3. Tesztelje a rollback folyamatot először fejlesztői környezetben

### Frissítés utáni ellenőrzés {#post-upgrade-verification}

A frissítés után ellenőrizze:
- Az alkalmazás kapcsolatát az adatbázishoz
- Az e-mail keresési és lekérdezési funkciókat
- A felhasználói hitelesítést és a munkamenet (session) kezelést
- A mentési és visszaállítási funkciókat
- Az adatbázis teljesítmény-mutatóit

## 📚 Kapcsolódó dokumentáció {#related-documentation}

- [Backup and Restore Guide](BackupRestore.md) - Alapvető a frissítés előtti mentésekhez
- [Database Maintenance Guide](DatabaseMaintenance.md) - Frissítés utáni karbantartás
- [Setup Guide](Setup.md) - Docker Compose konfiguráció
- [PostgreSQL Release Notes](https://www.postgresql.org/docs/release/) - Hivatalos kiadási dokumentáció
