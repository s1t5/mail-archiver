# 🐳 Vodnik za Docker Compose dnevnike {#docker-compose-logs-guide}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodnik pojasnjuje, kako pregledati in konfigurirati dnevnike (logs) pri uporabi aplikacije Mail Archiver z uporabo Docker Compose. Ustrezno upravljanje dnevnikov je ključno za spremljanje stanja aplikacije, reševanje težav in razumevanje vedenja sistema.

## 📖 Pregledovanje Docker Compose dnevnikov {#viewing-docker-compose-logs}

### Osnovne ukaze za dnevnike {#basic-log-commands}

Za ogled dnevnikov vseh storitev v vaši Docker Compose nastavitvi:

```bash
docker compose logs
```

Za ogled dnevnikov za določeno storitev (npr. aplikacijo Mail Archiver):

```bash
docker compose logs mailarchive-app
```

### Srečanje dnevnikov v realnem času {#real-time-log-streaming}

Za spremljanje dnevnikov v realnem času (podobno kot `tail -f`):

```bash
docker compose logs -f
```

Za spremljanje dnevnikov za določeno storitev:

```bash
docker compose logs -f mailarchive-app
```

### Filtriranje in možnosti dnevnikov {#log-filtering-and-options}

Prikaži zadnjih N vrstic dnevnikov:
```bash
docker compose logs --tail=100
```

Prikaži dnevnike od določenega časa:
```bash
docker compose logs --since=1h
```

Prikaži dnevnike do določenega časa:
```bash
docker compose logs --until=2025-01-01T12:00:00
```

Prikaži časovne žigove v dnevnikih:
```bash
docker compose logs -t
```

## 📝 Konfiguracija stopnje dnevnikovanja {#log-level-configuration}

Aplikacija Mail Archiver podpira različne stopnje dnevnikovanja (log levels), ki nadzorujejo podrobnost izhoda. Te stopnje lahko konfigurirate z uporabo okoljskih spremenljivih v svoji datoteki `docker-compose.yml`.

### Razpoložljive stopnje dnevnikovanja {#available-log-levels}

Na voljo so naslednje stopnje dnevnikovanja, razvrščene od najbolj podrobne do najmanj podrobne:

- **`Trace`** - Najpodrobnejše dnevnikovanje, vključno z notranji sporočili okvirja (framework)
- **`Debug`** - Podrobne informacije za odpravljanje napak (debugging) pri razvoju in reševanju težav
- **`Information`** - Splošne informacije o poteku aplikacije (privzeta stopnja)
- **`Warning`** - Opozorilna sporočila o morebitnih težavah
- **`Error`** - Napakovo sporočila o neuspelih operacijah
- **`Critical`** - Kritična napakovo sporočila, ki zahtevajo takojšnjo pozornost
- **`None`** - Brez izhoda dnevnikov

### Privzeta konfiguracija dnevnikov {#default-log-configuration}

Privzeto aplikacija uporablja naslednjo konfiguracijo stopnje dnevnikovanja:
- **Privzeto**: `Information`
- **Microsoft.AspNetCore**: `Warning` (zmanjša hrup okvirja)
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning` (zmanjša hrup ukazov za bazo podatkov)

### Konfiguracija stopenj dnevnikovanja v Docker Compose {#configuring-log-levels-in-docker-compose}

Za prilagoditev stopenj dnevnikovanja dodajte naslednje okoljske spremenljivke v svojo datoteko `docker-compose.yml` pod storitvijo `mailarchive-app`:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Logging Settings (Optional)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning
```

### Primer: Konfiguracija za debugging {#example-debug-configuration}

Za namene odpravljanja napak morda želite omogočiti bolj podrobno dnevnikovanje:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Verbose Logging for Debugging
      - Logging__LogLevel__Default=Debug
      - Logging__LogLevel__Microsoft_AspNetCore=Information
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Debug
```

### Primer: Produkcijska konfiguracija {#example-production-configuration}

Za produkcijska okolja morda želite zmanjšati podrobnost dnevnikov, da izboljšate zmogljivost in zmanjšate obseg dnevnikov:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Production Logging (Reduced Verbosity)
      - Logging__LogLevel__Default=Warning
      - Logging__LogLevel__Microsoft_AspNetCore=Error
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Error
```

## 🔍 Razumevanje izhoda dnevnikov {#understanding-log-output}

### Oblika dnevnika {#log-format}

Dnevniki sledijo tej splošni obliki:
```
timestamp [log-level] category - message
```

Primer:
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### Pogoste kategorije dnevnikov {#common-log-categories}

- **`MailArchiver.Services.*`** - Dnevniki storitve aplikacije
- **`MailArchiver.Controllers.*`** - Dnevniki Web API kontrolerja
- **`Microsoft.AspNetCore`** - Dnevniki ASP.NET Core okvirja
- **`Microsoft.EntityFrameworkCore`** - Dnevniki Entity Frameworka
- **`Microsoft.Extensions.Hosting`** - Dnevniki, povezani z hostingom

## 🛠️ Nasveti za odpravljanje težav {#troubleshooting-tips}

### 1. Iskanje napakovoih sporočil {#1-finding-error-messages}

Za hitro iskanje napakovoih sporočil v dnevnikih:
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. Spremljanje določenih operacij {#2-monitoring-specific-operations}

Za spremljanje sinhronizacije e-pošte:
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. Preverjanje opozoril {#3-checking-for-warnings}

Za preverjanje opozorilnih sporočil:
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 Najboljše prakse za upravljanje dnevnikov {#log-management-best-practices}

### Za razvoj {#for-development}
- Uporabite stopnjo `Debug` ali `Information` za podrobno odpravljanje napak
- Med razvojem omogočite spremljanje dnevnikov v realnem času
- Neprekinjeno spremljajte dnevnike med testiranjem novih funkcij

### Za produkcijo {#for-production}
- Uporabite stopnjo `Warning` ali `Error` za zmanjšanje obsega dnevnikov
- Implementirajte rotacijo dnevnikov (log rotation), da preprečite težave s prostorom na disku
- Za nastavitve z več kontejnerji razmislite o rešitvah za centralizirano beleženje dnevnikov
- Redno pregledujte dnevnike glede varnostnih dogodkov in težav z zmogljivostjo

## 📚 Sorodna dokumentacija {#related-documentation}

- [Vodnik za namestitev in postavitev](Setup.md) - Celoviti navodila za postavitev
- [Beleženje dostopa](Logs.md) - Beleženje dostopa na ravni aplikacije
- [Vodnik za varnostno kopiranje in obnavljanje](BackupRestore.md) - Postopki za zaščito podatkov
