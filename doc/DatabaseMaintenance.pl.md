# 🔧 Przewodnik po konserwacji bazy danych {#database-maintenance-guide}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik wyjaśnia, jak skonfigurować i korzystać z funkcji automatycznej konserwacji bazy danych w Mail Archiver. Aplikacja zawiera wbudowaną usługę działającą w tle, która automatycznie wykonuje operacje konserwacji bazy danych PostgreSQL, aby utrzymać optymalną wydajność i zapobiegać rozrostowi bazy danych.

## ⚠️ Ważna uwaga {#important-notice}

**Bazy danych PostgreSQL nie zmniejszają się automatycznie po usunięciu danych.** Gdy usuwasz e-maile lub skrzynki pocztowe, baza danych oznacza to miejsce jako dostępne do ponownego wykorzystania, ale faktyczny rozmiar pliku bazy danych pozostaje taki sam. Zjawisko to nazywane jest „rozrostem bazy danych” (database bloat).

Mail Archiver zawiera usługę automatycznej konserwacji, która codziennie wykonuje operacje VACUUM ANALYZE, aby:
- Odzyskać miejsce do ponownego wykorzystania wewnątrz bazy danych
- Zaktualizować statystyki zapytań dla optymalnej wydajności
- Zapobiec spadkowi wydajności w czasie
- Utrzymać stan zdrowia bazy danych

## 🚀 Automatyczna konserwacja (Zalecane) {#automatic-maintenance-recommended}

### Konfiguracja {#configuration}

Włącz automatyczną konserwację bazy danych w swoim `appsettings.json` lub za pomocą zmiennych środowiskowych w `docker-compose.yml`:

#### Opcja 1: Przez appsettings.json {#option-1-via-appsettingsjson}

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### Opcja 2: Przez docker-compose.yml (Zalecane) {#option-2-via-docker-composeyml-recommended}

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

### Opcje konfiguracji {#configuration-options}

| Parametr | Typ | Domyślny | Opis |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Włącz lub wyłącz automatyczną konserwację |
| `DailyExecutionTime` | string | `"02:00"` | Godzina wykonania konserwacji (format HH:mm, 24-godzinny) |
| `TimeoutMinutes` | integer | `30` | Maksymalny czas dozwolony na operacje konserwacji w minutach |

### Jak to działa {#how-it-works}

Po włączeniu usługa konserwacji:

1. **Uruchamia się automatycznie** wraz z aplikacją
2. **Czeka** do skonfigurowanej godziny wykonania
3. **Wykonuje VACUUM ANALYZE** na całej bazie danych
4. **Loguje operację** w dzienniku dostępu aplikacji wraz z czasem trwania
5. **Planuje ponownie** wykonanie na następny dzień

Operacja konserwacji:
- ✅ Uruchamia się bez przestojów
- ✅ Nie zakłóca normalnego działania aplikacji
- ✅ Zapewnia pełne logowanie i monitorowanie
- ✅ Odzyskuje miejsce do ponownego wykorzystania wewnątrz bazy danych
- ✅ Aktualizuje statystyki zapytań dla lepszej wydajności

### Wybór czasu wykonania {#choosing-the-execution-time}

Wybierz czas, w którym Twój system ma najniższą aktywność:
- **Zalecane**: Między 02:00 a 04:00 (godziny nocne)
- **Uwzględnij**: Ustawienia strefy czasowej
- **Unikaj**: Godzin szczytowego użycia lub okien kopii zapasowej

Przykładowe konfiguracje:

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### Monitorowanie automatycznej konserwacji {#monitoring-automatic-maintenance}

#### 1. Logi aplikacji {#1-application-logs}

Sprawdź logi aplikacji pod kątem aktywności konserwacji:

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. Dziennik dostępu (UI) {#2-access-log-ui}

Wszystkie operacje konserwacji są logowane w dzienniku dostępu aplikacji:

1. Przejdź do **Logs** → **Access Log** w interfejsie użytkownika
2. Przefiltruj według typu: **Database Maintenance**
3. Wyświetl czasy wykonania, czas trwania i ewentualne błędy

## 🔧 Ręczna konserwacja (Zaawansowane) {#manual-maintenance-advanced}

### VACUUM FULL w celu odzyskania miejsca na dysku {#vacuum-full-for-disk-space-recovery}

**⚠️ Wymaga przestoju aplikacji**

VACUUM FULL zagęszcza bazę danych i zwraca miejsce na dysku do systemu operacyjnego, ale wymaga blokad wyłącznych.

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**Kiedy używać:**
- Po usunięciu dużych skrzynek pocztowych
- Aby odzyskać znaczną ilość miejsca na dysku
- Podczas zaplanowanych okien konserwacji

**Ważne:**
- Wymaga wolnego miejsca na dysku równego rozmiarowi tabeli
- Może trwać wiele godzin w przypadku dużych baz danych
- Blokuje wszystkie operacje bazy danych
- Powinno być planowane w godzinach mniejszego obciążenia

Aby uzyskać szczegółowe procedury ręczne, zobacz [PostgreSQL VACUUM Documentation](https://www.postgresql.org/docs/current/sql-vacuum.html).

## 📚 Powiązana dokumentacja {#related-documentation}

- [Setup Guide](Setup.md) - Wstępna konfiguracja i zmienne środowiskowe
- [Backup and Restore Guide](BackupRestore.md) - Procedury kopii zapasowej bazy danych
- [PostgreSQL VACUUM Documentation](https://www.postgresql.org/docs/current/sql-vacuum.html) - Oficjalna dokumentacja PostgreSQL

## 📝 Podsumowanie {#summary}

✅ **Włącz automatyczną konserwację**, aby zapewnić optymalizację bazy danych bez ingerencji ręcznej
✅ **Monitoruj za pomocą dzienników dostępu**, aby weryfikować pomyślne operacje
✅ **Używaj ręcznego VACUUM FULL** tylko wtedy, gdy wymagane jest odzyskanie miejsca na dysku
✅ **Planuj VACUUM FULL** podczas okien konserwacji przy dużych usuwaniach
✅ **Dbaj o aktualność kopii zapasowych** przed wszelkimi ręcznymi operacjami konserwacji

Usługa automatycznej konserwacji obsługuje codzienną optymalizację bazy danych, eliminując potrzebę ręcznej interwencji w większości przypadków.
