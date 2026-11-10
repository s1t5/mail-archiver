# 🐳 Docker Compose Logs Guide {#docker-compose-logs-guide}

[← Vissz a dokumentáció indexre](index.md)

## 📋 Áttekintés {#overview}

Ez a útmutató elmagyarázza, hogyan tekinthetők meg és konfigurálhatók a naplók (logs), amikor a Mail Archiver alkalmazást Docker Compose segítségével futtatjuk. A megfelelő naplózás elengedhetetlen az alkalmazás egészségi állapának monitorozásához, a hibaelégítéshez és a rendszer működésének megértéséhez.

## 📖 Docker Compose naplók megtekintése {#viewing-docker-compose-logs}

### Alapvető naplózási parancsok {#basic-log-commands}

A Docker Compose beállításai közötti összes szolgáltatás naplóinak megtekintéséhez:

```bash
docker compose logs
```

Egy konkrét szolgáltatás (pl. a Mail Archiver alkalmazás) naplóinak megtekintéséhez:

```bash
docker compose logs mailarchive-app
```

### Valós idejű naplózás (Streaming) {#real-time-log-streaming}

A naplók valós időben történő követéséhez (hasonlóan a `tail -f` parancshoz):

```bash
docker compose logs -f
```

Egy konkrét szolgáltatás naplóinak követéséhez:

```bash
docker compose logs -f mailarchive-app
```

### Szűrés és opciók {#log-filtering-and-options}

Az utolsó N sor megjelenítése:
```bash
docker compose logs --tail=100
```

Naplók megjelenítése egy meghatározott időpont óta:
```bash
docker compose logs --since=1h
```

Naplók megjelenítése egy meghatározott időpontig:
```bash
docker compose logs --until=2025-01-01T12:00:00
```

Időbélyeggel ellátott naplók megjelenítése:
```bash
docker compose logs -t
```

## 📝 Naplózási szint konfiguráció {#log-level-configuration}

A Mail Archiver alkalmazás különböző naplózási szinteket támogat, amelyek szabályozzák a kimenet részletességét. Ezeket a szinteket a `docker-compose.yml` fájlban, környezeti változókon keresztül konfigurálhatja.

### Elérhető naplózási szintek {#available-log-levels}

A következő naplózási szintek érhetők el, a leghatóványabbaktól a legkevésbé részletesig sorrendben:

- **`Trace`** - Legrészletesebb naplózás, beleértve a belső keretrendszer üzeneteket is
- **`Debug`** - Részletes hibakeresési információk fejlesztéshez és hibaelégítéshez
- **`Information`** - Általános információk az alkalmazás folyamatáról (alapértelmezett szint)
- **`Warning`** - Figyelmeztető üzenetek a lehetséges problémákról
- **`Error`** - Hibaüzenetek a sikertelen műveletekről
- **`Critical`** - Kritikus hibaüzenetek, amelyek azonnali figyelmet igényelnek
- **`None`** - Nincs naplózás

### Alapértelmezett naplózási konfiguráció {#default-log-configuration}

Alapértelmezés szerint az alkalmazás a következő naplózási szint konfigurációt használja:
- **Alapértelmezett**: `Information`
- **Microsoft.AspNetCore**: `Warning` (csökkenti a keretrendszer zajt)
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning` (csökkenti az adatbázis-parancsok zaját)

### Naplózási szintek konfigurálása Docker Compose-ban {#configuring-log-levels-in-docker-compose}

A naplózási szintek testreszabásához adja hozzá a következő környezeti változókat a `docker-compose.yml` fájlhoz a `mailarchive-app` szolgáltatás alatt:

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

### Példa: Debug konfiguráció {#example-debug-configuration}

Hibaelégítés céljából érdemes lehet részletesebb naplózást engedélyezni:

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

### Példa: Production konfiguráció {#example-production-configuration}

Termelési környezetben érdemes lehet csökkenteni a naplózás részletességét a teljesítmény javítása és a naplók mennyiségének csökkentése érdekében:

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

## 🔍 A naplók értelmezése {#understanding-log-output}

### Naplózási formátum {#log-format}

A naplók az alábbi általános formátumot követik:
```
timestamp [log-level] category - message
```

Példa:
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### Gyakori naplózási kategóriák {#common-log-categories}

- **`MailArchiver.Services.*`** - Alkalmazási szolgáltatási naplók
- **`MailArchiver.Controllers.*`** - Web API controller naplók
- **`Microsoft.AspNetCore`** - ASP.NET Core keretrendszer naplók
- **`Microsoft.EntityFrameworkCore`** - Entity Framework naplók
- **`Microsoft.Extensions.Hosting`** - Hosting-hez kapcsolódó naplók

## 🛠️ Hibaelégítési tippek {#troubleshooting-tips}

### 1. Hibaüzenetek megt findálása {#1-finding-error-messages}

A naplókban található hibaüzenetek gyors megtalálása érdekében:
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. Specifikus műveletek monitorozása {#2-monitoring-specific-operations}

Az e-mail szinkronizáció figyelése érdekében:
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. Figyelmeztetések ellenőrzése {#3-checking-for-warnings}

Figyelmeztető üzenetek ellenőrzéséhez:
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 Naplózási legjobb gyakorlatok {#log-management-best-practices}

### Fejlesztéshez {#for-development}
- Használjon `Debug` vagy `Information` szintet a részletes hibaelégítéshez
- Engedélyezze a valós idejű naplózási streamelést a fejlesztés során
- Folyamatosan figyelje a naplókat az új funkciók tesztelése közben

### Termeléshez {#for-production}
- Használjon `Warning` vagy `Error` szintet a naplózási mennyiség csökkentésére
- Implementáljon naplók rotációját a le mezzotérfoglalási problémák elkerülése érdekében
- Több konténeres beállítások esetén fontolná meg a centralizált naplózási megoldásokat
- Rendszeresen ellenőrizze a naplókat biztonsági események és teljesítményproblémák tekintetében

## 📚 Kapcsolódó dokumentáció {#related-documentation}

- [Telepítési és beállítási útmutató](Setup.md) - Teljes telepítési utasítások
- [Hozzáférés naplózás](Logs.md) - Alkalmazásszintű hozzáférési naplózás
- [Mentés és visszaállítás útmutató](BackupRestore.md) - Adatvédelmi eljárások
