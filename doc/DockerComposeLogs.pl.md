# 🐳 Przewodnik po logach Docker Compose {#docker-compose-logs-guide}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik wyjaśnia, jak przeglądać i konfigurować logi podczas uruchamiania aplikacji Mail Archiver przy użyciu Docker Compose. Właściwe zarządzanie logami jest niezbędne do monitorowania stanu aplikacji, rozwiązywania problemów i zrozumienia zachowania systemu.

## 📖 Przeglądanie logów Docker Compose {#viewing-docker-compose-logs}

### Podstawowe polecenia logów {#basic-log-commands}

Aby wyświetlić logi ze wszystkich usług w konfiguracji Docker Compose:

```bash
docker compose logs
```

Aby wyświetlić logi dla konkretnej usługi (np. aplikacji Mail Archiver):

```bash
docker compose logs mailarchive-app
```

### Strumieniowanie logów w czasie rzeczywistym {#real-time-log-streaming}

Aby śledzić logi w czasie rzeczywistym (podobnie jak `tail -f`):

```bash
docker compose logs -f
```

Aby śledzić logi dla konkretnej usługi:

```bash
docker compose logs -f mailarchive-app
```

### Filtrowanie i opcje logów {#log-filtering-and-options}

Wyświetl ostatnie N linii logów:
```bash
docker compose logs --tail=100
```

Wyświetl logi od określonego czasu:
```bash
docker compose logs --since=1h
```

Wyświetl logi do określonego czasu:
```bash
docker compose logs --until=2025-01-01T12:00:00
```

Wyświetl znaczniki czasu w logach:
```bash
docker compose logs -t
```

## 📝 Konfiguracja poziomu logów {#log-level-configuration}

Aplikacja Mail Archiver obsługuje różne poziomy logów, które kontrolują szczegółowość wyjścia. Możesz skonfigurować te poziomy za pomocą zmiennych środowiskowych w swoim pliku `docker-compose.yml`.

### Dostępne poziomy logów {#available-log-levels}

Dostępne są następujące poziomy logów, uporządkowane od najbardziej szczegółowych do najmniej szczegółowych:

- **`Trace`** - Najbardziej szczegółowe logowanie, w tym komunikaty wewnętrzne frameworka
- **`Debug`** - Szczegółowe informacje do debugowania na potrzeby rozwoju i rozwiązywania problemów
- **`Information`** - Ogólne informacje o przepływie aplikacji (poziom domyślny)
- **`Warning`** - Komunikaty ostrzegawcze o potencjalnych problemach
- **`Error`** - Komunikaty o błędach dotyczących nieudanych operacji
- **`Critical`** - Krytyczne komunikaty o błędach wymagające natychmiastowej uwagi
- **`None`** - Brak wyjścia logów

### Domyślna konfiguracja logów {#default-log-configuration}

Domyślnie aplikacja używa następującej konfiguracji poziomu logów:
- **Domyślny**: `Information`
- **Microsoft.AspNetCore**: `Warning` (zmniejsza szum frameworka)
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning` (zmniejsza szum komend bazy danych)

### Konfigurowanie poziomów logów w Docker Compose {#configuring-log-levels-in-docker-compose}

Aby dostosować poziomy logów, dodaj następujące zmienne środowiskowe do swojego pliku `docker-compose.yml` w sekcji usługi `mailarchive-app`:

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

### Przykład: Konfiguracja Debug {#example-debug-configuration}

Na potrzeby rozwiązywania problemów możesz chcieć włączyć bardziej szczegółowe logowanie:

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

### Przykład: Konfiguracja Produkcyjna {#example-production-configuration}

W środowiskach produkcyjnych możesz chcieć zmniejszyć szczegółowość logów, aby poprawić wydajność i zmniejszyć objętość logów:

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

## 🔍 Zrozumienie wyjścia logów {#understanding-log-output}

### Format logów {#log-format}

Logi mają następujący ogólny format:
```
timestamp [log-level] category - message
```

Przykład:
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### Typowe kategorie logów {#common-log-categories}

- **`MailArchiver.Services.*`** - Logi usług aplikacji
- **`MailArchiver.Controllers.*`** - Logi kontrolerów Web API
- **`Microsoft.AspNetCore`** - Logi frameworka ASP.NET Core
- **`Microsoft.EntityFrameworkCore`** - Logi Entity Framework
- **`Microsoft.Extensions.Hosting`** - Logi związane z hostingiem

## 🛠️ Wskazówki dotyczące rozwiązywania problemów {#troubleshooting-tips}

### 1. Znajdowanie komunikatów o błędach {#1-finding-error-messages}

Aby szybko znaleźć komunikaty o błędach w logach:
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. Monitorowanie konkretnych operacji {#2-monitoring-specific-operations}

Aby monitorować synchronizację poczty:
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. Sprawdzanie ostrzeżeń {#3-checking-for-warnings}

Aby sprawdzić komunikaty ostrzegawcze:
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 Najlepsze praktyki zarządzania logami {#log-management-best-practices}

### Dla deweloperów {#for-development}
- Używaj poziomu `Debug` lub `Information` do szczegółowego rozwiązywania problemów
- Włącz strumieniowanie logów w czasie rzeczywistym podczas rozwoju
- Monitoruj logi w sposób ciągły podczas testowania nowych funkcji

### Dla środowisk produkcyjnych {#for-production}
- Używaj poziomu `Warning` lub `Error`, aby zmniejszyć objętość logów
- Wdróż rotację logów, aby zapobiec problemom z miejscem na dysku
- Rozważ rozwiązania do scentralizowanego logowania w konfiguracjach wielokontenerowych
- Regularnie przeglądaj logi pod kątem zdarzeń bezpieczeństwa i problemów z wydajnością

## 📚 Powiązana dokumentacja {#related-documentation}

- [Przewodnik instalacji i konfiguracji](Setup.md) - Pełne instrukcje konfiguracji
- [Logowanie dostępu](Logs.md) - Logowanie dostępu na poziomie aplikacji
- [Przewodnik Kopia zapasowa i Przywracanie](BackupRestore.md) - Procedury ochrony danych
