# Состояние и метрики {#health-and-metrics}

Mail-Archiver предоставляет эндпоинты состояния (health endpoints) и совместимый с Prometheus эндпоинт метрик для внешнего мониторинга. Оба являются частью REST API: они находятся под `/api/v1`, требуют API key и требуют, чтобы этот ключ принадлежал администратору. Анонимного пути для проверки состояния и отдельного токена для мониторинга не существует.

## Requirements {#requirements}

| Requirement | Why |
|---|---|
| `Api:Enabled=true` | Эндпоинты находятся под `/api/`. Если API отключен, промежуточное ПО аутентификации возвращает 404. |
| An admin API key | Health и metrics раскрывают состояние всего экземпляра, поэтому ключ не администратора получает 403. |
| `Metrics:Enabled=true` | Эндпоинт metrics является опциональным. Health работает и без него. |

Создайте ключ в разделе **API Keys**, будучи авторизованным в качестве администратора. Используйте выделенный ключ и, если возможно, выделенного администратора для мониторинга, чтобы ротация ключа или отключение пользователя не влияли на доступ человека.

## Endpoints {#endpoints}

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Процесс жив | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL доступен и схема присутствует | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Агрегированный статус, включая свежесть синхронизации | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Формат текста Prometheus | 200 | 404 при отключении, 401, 403 |

Все четыре эндпоинта используют ту же аутентификацию, что и остальная часть API:

- отсутствующий или недействительный ключ: `401` с `WWW-Authenticate: Bearer`
- действительный ключ без роли admin: `403` с `application/problem+json`
- API отключен: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Liveness не обращается к PostgreSQL. Он доказывает только то, что процесс принимает запросы, поэтому сбой базы данных не заставит Docker или Kubernetes перезапускать исправный процесс.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

Проверка открывает собственное соединение с PostgreSQL, запрашивает схему archive и прекращает попытки через две секунды. Она возвращает `503`, когда база данных недоступна или отсутствует схема `mail_archiver`. Используйте этот эндпоинт, чтобы решить, должен ли экземпляр получать трафик.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Этот эндпоинт запускает проверку базы данных и проверку свежести синхронизации. Устаревшая синхронизация делает отчет `Degraded`, который сохраняет статус HTTP 200, так как процесс все еще может обрабатывать запросы. Только `Unhealthy` возвращает `503`.

Проверка синхронизации отключена по умолчанию. Установите `Health:SyncStaleMinutes` на количество минут, по истечении которых включенный аккаунт будет считаться устаревшим:

```yaml
Health:
  SyncStaleMinutes: 90
```

Аккаунт, который никогда не синхронизировался, имеет `LastSync` по значению по умолчанию, поэтому он считается устаревшим, как только проверка будет включена.

## Метрики {#metrics}

Эндпоинт возвращает данные в текстовом формате экспозиции Prometheus (версия 0.0.4):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` должен быть `true`. `Metrics:RefreshMinutes` управляет тем, как часто перестраивается
снимок (по умолчанию 5, значения ниже 1 повышаются до 1). Сам процесс scrape только читает
снимок, поэтому опрос Prometheus не может превратиться в сканирование таблицы.

| Метрика | Тип | Метки | Значение |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1, когда эндпоинт метрик отвечает. |
| `mailarchiver_metrics_ready` | gauge | | 1 после первого успешного снимка. |
| `mailarchiver_db_up` | gauge | | 1, когда последнее обновление достигло PostgreSQL. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Unix время последнего успешного обновления. |
| `mailarchiver_emails` | gauge | | Количество архивированных писем. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Количество почтовых аккаунтов. |
| `mailarchiver_raw_messages` | gauge | | Количество сохраненных оригинальных сообщений. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1, если установлен `RawMessages:Enabled`. |
| `mailarchiver_raw_messages_missing` | gauge | | Архивированные письма без сохраненного оригинала. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1, если аккаунт никогда не завершал синхронизацию. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix время последней успешной синхронизации. Отсутствует для аккаунтов, которые никогда не синхронизировались. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Секунд с момента последней успешной синхронизации. Отсутствует для аккаунтов, которые никогда не синхронизировались. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Оригинальные сообщения, которые не удалось сохранить. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Запуски синхронизации, завершившиеся ошибкой, ограничением скорости или таймаутом. |

Примечания:

- Счетчики являются локальными для процесса и сбрасываются в 0 после перезапуска. `rate()` и `increase()`
  в Prometheus обрабатывают это корректно.
- `mailarchiver_raw_messages_missing` подсчитывает каждое архивированное письмо без необработанного сообщения (raw message),
  включая письма, архивированные до того, как была включена `RawMessages:Enabled`. В установках,
  которые хранят оригиналы с первого дня, это и есть актуальный разрыв.
- `mailarchiver_account_sync_age_seconds` не является истинной очередью (backlog). Он показывает, когда завершилась последняя синхронизация,
  а не то, сколько сообщений ожидает на сервере.
- `mailarchiver_account_never_synced` существует, потому что у аккаунта, который никогда не синхронизировался, нет
  серии `sync_age`. Оповещение только по `sync_age` пропустило бы его.
- Эндпоинт содержит имена и id аккаунтов, но не содержит содержимое писем, темы или
  учетные данные.

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

Файл учетных данных содержит необработанный ключ без префикса `Bearer`. Ограничьте права доступа к нему для пользователя Prometheus.

Примеры алертов:

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

Образ поставляется с проверкой работоспособности (liveness check), которая вызывает корневой путь без аутентификации. Корневой путь перенаправляет на страницу входа до выполнения любого контроллера, поэтому для проверки не требуется API key, она работает при отключенном REST API и не затрагивает PostgreSQL. Чистые установки и обновления сообщают `healthy` без какой-либо конфигурации.

```bash
docker inspect --format '{{.State.Health.Status}}' <container>
```

Проверка доказывает, что процесс принимает HTTP-запросы. Она не доказывает, что PostgreSQL доступен. Для этого вызовите `/api/v1/health/ready` с внешнего хоста мониторинга, используя admin API key и `Api:Enabled=true`; эндпоинт отвечает 503, когда база данных отключена.

Встроенный `docker-compose.yml` не переопределяет проверку. Добавьте блок `healthcheck` в сервис, если вам нужен другой интервал, таймаут или команда, или установите `healthcheck: disable: true`, чтобы отключить проверку.

Прежняя переменная `MAILARCHIVER_HEALTH_API_KEY` больше не используется. Удалите ее из существующих compose files и `.env` files; ее наличие в файлах не нанесет вреда.

## Безопасность {#security}

- Ключ является ключом администратора. Он может читать весь архив через API. Обращайтесь с ним как с паролем администратора.
- Используйте выделенного пользователя администратора для мониторинга. Отключение этого пользователя отключает внешние эндпоинты health и metrics, а не собственный liveness check контейнера.
- Храните ключ в файле или в Docker secret, но никогда не используйте его в параметрах запроса. Параметры запроса попадают в логи прокси-сервера и логи доступа.
- Ограничьте доступ хостом мониторинга или сетью Prometheus. API key — это аутентификация, а не замена сетевым границам.
- Эндпоинты health возвращают только строки статуса. Подробности исключений остаются в логе приложения.

## Troubleshooting {#troubleshooting}

| Симптом | Причина | Решение |
|---|---|---|
| 404 на всех четырех путях | `Api:Enabled=false` | Установите `Api:Enabled=true` и перезапустите. |
| 404 только на `/metrics` | `Metrics:Enabled=false` | Установите `Metrics:Enabled=true` и перезапустите. |
| 401 | Ключ отсутствует, неверен, отозван или истек | Создайте новый ключ и проверьте заголовок `Authorization: Bearer`. |
| 403 | Ключ принадлежит пользователю без прав администратора | Используйте ключ администратора. |
| `mailarchiver_metrics_ready 0` более одной минуты | Сервис обновления не завершил свой первый запуск или не может достичь базы данных | Проверьте лог приложения на наличие `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | Последнее обновление завершилось ошибкой | Проверьте PostgreSQL и строку подключения. Старые значения сохраняются до следующего успешного обновления. |
| Контейнер остается в состоянии unhealthy | Ключ пуст или `Api:Enabled=false` | Установите оба значения и пересоздайте контейнер. |
| `health/ready` возвращает 503 | PostgreSQL недоступен или отсутствует схема | Проверьте базу данных и выполнение миграций. |

## Что не охватывают метрики {#what-the-metrics-do-not-cover}

- Доступность IMAP или Graph для каждого аккаунта. Sync age лишь показывает, что последний запуск был давно.
- Количество сообщений, ожидающих на сервере, то есть реальный бэклог.
- Проверка контрольной суммы каждого сохраненного оригинала. Для этого используйте существующую проверку необработанных сообщений (raw message verification) и очистку сирот (orphan cleanup).
- Подсчет по папкам или размеры почтовых ящиков.

Для получения этой информации используйте страницу аккаунта, страницу фоновых заданий и лог приложения.
