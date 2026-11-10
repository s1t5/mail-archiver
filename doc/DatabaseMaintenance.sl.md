# 🔧 Vodnik za vzdrževanje baze podatkov {#database-maintenance-guide}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodnik pojasnjuje, kako konfigurirati in uporabljati funkcijo samodejnega vzdrževanja baze podatkov v Mail Archiver. Aplikacija vključuje vgrajeno storitev v ozadju, ki samodejno izvaja operacije vzdrževanja PostgreSQL baze podatkov, da ohrani optimalno zmogljivost in prepreči povečanje (bloat) baze podatkov.

## ⚠️ Pomembno obvestilo {#important-notice}

**PostgreSQL baze podatkov se ob brisanju podatkov ne zmanjšajo samodejno.** Ko izbrišete e-poštna sporočila ali predališča, baza podatkov označi prostor kot na voljo za ponovno uporabo, vendar dejanska velikost datoteke baze podatkov ostane enaka. Ta pojav se imenuje "database bloat".

Mail Archiver vključuje storitev za samodejno vzdrževanje, ki vsak dan izvaja operacije VACUUM ANALYZE, da:
- Pridobi prostor za ponovno uporabo znotraj baze podatkov
- Posveti statistiko poizvedb za optimalno zmogljivost
- Prepreči zmanjšanje zmogljivosti čez čas
- Vzdržuje zdravje baze podatkov

## 🚀 Samodejno vzdrževanje (Priporočeno) {#automatic-maintenance-recommended}

### Konfiguracija {#configuration}

Omogočite samodejno vzdrževanje baze podatkov v svojem `appsettings.json` ali preko okoljskih spremenljivih v `docker-compose.yml`:

#### Možnost 1: Preko appsettings.json {#option-1-via-appsettingsjson}

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### Možnost 2: Preko docker-compose.yml (Priporočeno) {#option-2-via-docker-composeyml-recommended}

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

### Možnosti konfiguracije {#configuration-options}

| Parameter | Tip | Z умолчанию | Opis |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Omogoči ali onemogoči samodejno vzdrževanje |
| `DailyExecutionTime` | string | `"02:00"` | Čas dneva za izvedbo vzdrževanja (format HH:mm, 24-urno uro) |
| `TimeoutMinutes` | integer | `30` | Najvišji dovoljeni čas v minutah za operacije vzdrževanja |

### Kako deluje {#how-it-works}

Ko je storitev vzdrževanja omogočena:

1. **Začne delovati samodejno** ob zagonu aplikacije
2. **Čaka** do konfiguriranega časa izvedbe
3. **Izvede VACUUM ANALYZE** na celotni bazi podatkov
4. **Zabeleže operacijo** v dostopni dnevnik (access log) aplikacije z trajenjem
5. **Napodtka** za naslednji dan

Operacija vzdrževanja:
- ✅ Poteka brez izpadov sistema
- ✅ Ne moti normalnega delovanja aplikacije
- ✅ Zagotavlja polno beleženje in spremljanje
- ✅ Pridobi prostor za ponovno uporabo znotraj baze podatkov
- ✅ Posodobi statistiko poizvedb za boljšo zmogljivost

### Izbira časa izvedbe {#choosing-the-execution-time}

Izberite čas, ko ima vaš sistem najmanj aktivnosti:
- **Priporočeno**: Med 02:00 in 04:00 (nočni urah)
- **Upoštevajte**: Nastavitve časovnega pasovesa
- **Izogibajte se**: Ur največje uporabe ali oken za varnostno kopiranje

Primeri konfiguracij:

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### Spremljanje samodejnega vzdrževanja {#monitoring-automatic-maintenance}

#### 1. Dnevnik aplikacije (Application Logs) {#1-application-logs}

Preverite dnevnike aplikacije za aktivnosti vzdrževanja:

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. Dostopni dnevnik (UI) {#2-access-log-ui}

Vse operacije vzdrževanja so zabeležene v dostopnem dnevniku aplikacije:

1. Navigirajte na **Logs** → **Access Log** v uporabniškem vmesniku
2. Filtrirajte po tipu: **Database Maintenance**
3. Preglejte čase izvedbe, trajanje in morebitne napake

## 🔧 Ročno vzdrževanje (Napredno) {#manual-maintenance-advanced}

### VACUUM FULL za okrevanje prostora na disku {#vacuum-full-for-disk-space-recovery}

**⚠️ Zahteva izpad sistema (downtime)**

VACUUM FULL zbije bazo podatkov in vrne prostor na disku operacijskemu sistemu, vendar zahteva ekskluzivne zaklene termine (locks).

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**Kdaj uporabiti:**
- Po brisanju velikih predali z e-pošto
- Za pridobitev znatnega prostora na disku
- Med načrtovanimi oknami vzdrževanja

**Pomembno:**
- Zahteva prost prostor na disku, ki je enak velikosti tabele
- Pri velikih bazah podatkov lahko traja uras
- Blokira vse operacije baze podatkov
- Mora biti načrtovano v urah z nizko obremenitvijo

Za podrobne ročne postopke si oglejte [PostgreSQL VACUUM Documentation](https://www.postgresql.org/docs/current/sql-vacuum.html).

## 📚 Sorodna dokumentacija {#related-documentation}

- [Setup Guide](Setup.md) - Začetna konfiguracija in okoljske spremenljivke
- [Backup and Restore Guide](BackupRestore.md) - Postopki varnostnega kopiranja baze podatkov
- [PostgreSQL VACUUM Documentation](https://www.postgresql.org/docs/current/sql-vacuum.html) - Uradna dokumentacija PostgreSQL

## 📝 Povzetek {#summary}

✅ **Omogočite samodejno vzdrževanje** za avtomatizirano optimizacijo baze podatkov
✅ **Spremljajte prek dostopnih dnevnikov**, da preverite uspešnost operacij
✅ **Uporabite ročni VACUUM FULL** le, ko je potrebno okrevanje prostora na disku
✅ **Načrtujte VACUUM FULL** med oknami vzdrževanja za velike izbris
✅ **Održujte sveže varnostne kopije** pred vsako ročno operacijo vzdrževanja

Storitev samodečnega vzdrževanja upravlja dnevno optimizacijo baze podatkov, kar v večini primerov odpravlja potrebo po ročnem poseganju.
