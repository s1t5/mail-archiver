# Zdrowie i metryki {#health-and-metrics}

Mail-Archiver udostępnia punkty końcowe stanu zdrowia oraz punkt końcowy metryk zgodny z Prometheus do zewnętrznego monitorowania. Oba są częścią REST API: znajdują się pod `/api/v1`, wymagają klucza API i wymagają, aby ten klucz należał do administratora. Nie ma anonimowej ścieżki stanu zdrowia ani oddzielnego tokenu monitorowania.

## Wymagania {#requirements}

| Wymaganie | Dlaczego |
|---|---|
| `Api:Enabled=true` | Punkty końcowe znajdują się pod `/api/`. Gdy API jest wyłączone, middleware uwierzytelniający zwraca 404. |
| Klucz API administratora | Health i metrics ujawniają stan całej instancji, więc klucz bez uprawnień administratora otrzymuje 403. |
| `Metrics:Enabled=true` | Punkt końcowy metrics jest opcjonalny. Health działa bez niego. |

Utwórz klucz w sekcji **API Keys**, będąc zalogowanym jako administrator. Użyj dedykowanego klucza i, jeśli to możliwe, dedykowanego użytkownika administratora do monitorowania, aby rotacja klucza lub wyłączenie użytkownika nie wpływało na dostęp danej osoby.

## Endpoints {#endpoints}

| Metoda | Ścieżka | Cel | Sukces | Błędy |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Proces żyje | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL jest osiągalny i schemat istnieje | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Zagregowany status, w tym świeżość synchronizacji | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Format tekstowy Prometheus | 200 | 404 gdy jest wyłączony, 401, 403 |

Wszystkie cztery punkty końcowe używają tej samej autoryzacji co reszta API:

- brakujący lub nieprawidłowy klucz: `401` z `WWW-Authenticate: Bearer`
- poprawny klucz bez roli admina: `403` z `application/problem+json`
- API wyłączone: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Liveness nie dotyka PostgreSQL. Dowodzi jedynie, że proces przyjmuje żądania, dzięki czemu awaria bazy danych nie powoduje, że Docker lub Kubernetes zrestartuje sprawny proces.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

Sprawdzenie otwiera własne połączenie z PostgreSQL, pyta o schemat Archiwum i przerywa po dwóch sekundach. Zwraca `503`, gdy baza danych jest nieosiągalna lub brakuje schematu `mail_archiver`. Użyj tego punktu końcowego, aby zdecydować, czy instancja powinna otrzymywać ruch.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

To uruchamia sprawdzenie bazy danych oraz sprawdzenie świeżości synchronizacji. Nieaktualna synchronizacja sprawia, że raport jest `Degraded`, co pozostaje HTTP 200, ponieważ proces wciąż może obsługiwać żądania. Tylko `Unhealthy` zwraca `503`.

Sprawdzenie synchronizacji jest domyślnie wyłączone. Ustaw `Health:SyncStaleMinutes` na liczbę minut, po których włączone Konto uznawane jest za nieaktualne:

```yaml
Health:
  SyncStaleMinutes: 90
```

Konto, które nigdy nie przeprowadziło synchronizacji, ma `LastSync` na domyślnej wartości, więc jest uznawane za nieaktualne natychmiast po włączeniu sprawdzenia.

## Metryki {#metrics}

Punkt końcowy zwraca format tekstowej ekspozycji Prometheus (wersja 0.0.4):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` musi być `true`. `Metrics:RefreshMinutes` kontroluje, jak często migawka
jest przebudowywana (domyślnie 5, wartości poniżej 1 są podnoszone do 1). Sam proces scrapowania odczytuje jedynie
migawkę, więc zapytanie Prometheus nie może zmienić się w skanowanie tabeli.

| Metryka | Typ | Etykiety | Znaczenie |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1, gdy punkt końcowy metryk odpowiada. |
| `mailarchiver_metrics_ready` | gauge | | 1 po pierwszej udanej migawce. |
| `mailarchiver_db_up` | gauge | | 1, gdy ostatnie odświeżanie dotarło do PostgreSQL. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Czas Unix ostatniego udanego odświeżenia. |
| `mailarchiver_emails` | gauge | | Liczba zarchiwizowanych e-maili. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Liczba kont pocztowych. |
| `mailarchiver_raw_messages` | gauge | | Liczba przechowywanych oryginalnych wiadomości. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1, gdy `RawMessages:Enabled` jest ustawione. |
| `mailarchiver_raw_messages_missing` | gauge | | Zarchiwizowane e-maile bez przechowywanego oryginału. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1, gdy Konto nigdy nie zakończyło Synchronizacji. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Czas Unix ostatniej udanej Synchronizacji. Brak dla kont, które nigdy nie przeprowadziły Synchronizacji. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Sekundy od ostatniej udanej Synchronizacji. Brak dla kont, które nigdy nie przeprowadziły Synchronizacji. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Oryginalne wiadomości, których nie można było zapisać. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Uruchomienia Synchronizacji, które zakończyły się błędem, ograniczeniem prędkości lub przekroczeniem czasu oczekiwania. |

Uwagi:

- Liczniki są lokalne dla procesu i zaczynają się od 0 po restarcie. `rate()` i `increase()`
  w Prometheus obsługują to poprawnie.
- `mailarchiver_raw_messages_missing` liczy każdy zarchiwizowany e-mail bez surowej wiadomości,
  w tym e-maile zarchiwizowane przed włączeniem `RawMessages:Enabled`. W instalacji,
  która od pierwszego dnia przechowuje oryginały, jest to istotna luka.
- `mailarchiver_account_sync_age_seconds` nie jest prawdziwym opóźnieniem (backlog). Wskazuje, kiedy zakończyła się ostatnia Synchronizacja,
  a nie ile wiadomości czeka na serwerze.
- `mailarchiver_account_never_synced` istnieje, ponieważ Konto, które nigdy nie przeprowadziło Synchronizacji, nie posiada
  szeregu `sync_age`. Alert na samym `sync_age` by go pominął.
- Punkt końcowy zawiera nazwy i identyfikatory kont, ale nie zawiera treści e-maili, tematów ani
  danych uwierzytelniających.

## Prometheus {#prometheus}

```yaml
scrape_configs:
  - job_name: mail-archiver
    scrape_interval: 30s
    authorization:
      credentials_file: /etc/prometheus/mailarchiver-api-key
    static_configs:
      - targets: ["mail-archiver:5000"]
```

Plik poświadczeń zawiera surowy klucz bez prefiksu `Bearer`. Ogranicz jego uprawnienia do użytkownika Prometheus.

Przykładowe alerty:

```yaml
groups:
  - name: mail-archiver
    rules:
      - alert: MailArchiverSyncStale
        expr: mailarchiver_account_sync_age_seconds{enabled="true"} > 3600
        for: 15m

      - alert: MailArchiverNeverSynced
        expr: mailarchiver_account_never_synced{enabled="true"} == 1
        for: 30m

      - alert: MailArchiverSyncFailures
        expr: increase(mailarchiver_sync_failures_total[1h]) > 0

      - alert: MailArchiverMissingOriginals
        expr: mailarchiver_raw_messages_enabled == 1 and mailarchiver_raw_messages_missing > 0

      - alert: MailArchiverDatabaseDown
        expr: mailarchiver_db_up == 0
        for: 5m

      - alert: MailArchiverMetricsStale
        expr: time() - mailarchiver_metrics_refresh_timestamp_seconds > 900
```

## Docker health check {#docker-health-check}

Obraz zawiera sprawdzenie żywotności (liveness check), które wywołuje ścieżkę główną bez uwierzytelniania. Ścieżka główna przekierowuje do strony logowania przed uruchomieniem jakiegokolwiek kontrolera, więc sprawdzenie nie wymaga klucza API, działa przy wyłączonym REST API i nie dotyka PostgreSQL. Nowe instalacje i aktualizacje raportują `healthy` bez żadnej konfiguracji.

```bash
docker inspect --format '{{.State.Health.Status}}' <container>
```

Sprawdzenie dowodzi, że proces przyjmuje żądania HTTP. Nie dowodzi ono, że PostgreSQL jest osiągalny. Aby to sprawdzić, wywołaj `/api/v1/health/ready` z zewnętrznego hosta monitorującego przy użyciu klucza API administratora i `Api:Enabled=true`; punkt końcowy zwraca 503, gdy baza danych nie działa.

Dołączony `docker-compose.yml` nie nadpisuje sprawdzenia. Dodaj blok `healthcheck` do usługi, jeśli potrzebujesz innego interwału, czasu oczekiwania lub polecenia, bądź ustaw `healthcheck: disable: true`, aby wyłączyć sprawdzenie.

Poprzednia zmienna `MAILARCHIVER_HEALTH_API_KEY` nie jest już używana. Usuń ją z istniejących plików compose oraz plików `.env`; pozostawienie jej na miejscu nie jest szkodliwe.

## Bezpieczeństwo {#security}

- Klucz jest kluczem administratora. Może on odczytać całe Archiwum poprzez API. Traktuj go jak hasło administratora.
- Do monitorowania używaj dedykowanego użytkownika administratora. Wyłączenie tego użytkownika wyłącza zewnętrzne punkty końcowe health i metrics, a nie własny liveness check kontenera.
- Przechowuj klucz w pliku lub w Docker secret, nigdy w parametrze zapytania. Parametry zapytania trafiają do logów serwera proxy i logów dostępu.
- Ogranicz dostęp do hosta monitorującego lub sieci Prometheus. Klucz API służy do uwierzytelniania, a nie jest substytutem barier sieciowych.
- Punkty końcowe health zwracają jedynie ciągi znaków ze statusem. Szczegóły wyjątków pozostają w dzienniku aplikacji.

## Rozwiązywanie problemów {#troubleshooting}

| Objaw | Przyczyna | Rozwiązanie |
|---|---|---|
| 404 na wszystkich czterech ścieżkach | `Api:Enabled=false` | Ustaw `Api:Enabled=true` i zrestartuj. |
| 404 tylko na `/metrics` | `Metrics:Enabled=false` | Ustaw `Metrics:Enabled=true` i zrestartuj. |
| 401 | Brakujący, błędny, cofnięty lub wygasły klucz | Utwórz nowy klucz i sprawdź nagłówek `Authorization: Bearer`. |
| 403 | Klucz należy do użytkownika bez uprawnień administratora | Użyj klucza administratora. |
| `mailarchiver_metrics_ready 0` przez ponad minutę | Usługa odświeżania nie zakończyła swojego pierwszego uruchomienia lub nie może połączyć się z bazą danych | Sprawdź dziennik aplikacji pod kątem `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | Ostatnie odświeżanie zakończyło się niepowodzeniem | Sprawdź PostgreSQL i ciąg połączenia. Stare liczniki pozostają do czasu kolejnego sukcesu. |
| Kontener pozostaje w stanie unhealthy | Klucz jest pusty lub `Api:Enabled=false` | Ustaw oba i ponownie utwórz kontener. |
| `health/ready` zwraca 503 | PostgreSQL jest nieosiągalny lub brakuje schematu | Sprawdź bazę danych i czy migracje zostały wykonane. |

## Czego nie obejmują metryki {#what-the-metrics-do-not-cover}

- Dostępność IMAP lub Graph dla każdego ⟦X0⟧. Wiek synchronizacji wskazuje jedynie, że ostatnie uruchomienie jest stare.
- Liczba wiadomości oczekujących na serwerze, czyli rzeczywista kolejka.
- Weryfikacja sum kontrolnych każdego przechowywanego oryginału. W tym celu należy użyć istniejącej weryfikacji surowych wiadomości oraz czyszczenia osieroconych elementów.
- Liczniki dla każdego ⟦X0⟧ lub rozmiary ⟦X0⟧.

W tym celu należy użyć strony ⟦X0⟧, strony zadań w tle oraz dziennika aplikacji.
