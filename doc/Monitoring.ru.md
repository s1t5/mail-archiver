# Health and Metrics {#health-and-metrics}

Mail-Archiver предоставляет эндпоинты состояния (health endpoints) и эндпоинт метрик, совместимый с Prometheus, для внешнего мониторинга. Оба являются частью REST API: они находятся под `/api/v1`, требуют API key, и этот ключ должен принадлежать администратору. Анонимного пути для проверки состояния и отдельного токена для мониторинга не существует.

## Требования {#requirements}

| Требование | Почему |
|---|---|
| `Api:Enabled=true` | Эндпоинты находятся под `/api/`. Если API отключен, промежуточное ПО аутентификации возвращает 404. |
| API key администратора | Health и metrics раскрывают состояние всего экземпляра, поэтому ключ не-администратора получает 403. |
| `Metrics:Enabled=true` | Эндпоинт metrics подключается отдельно. Health работает и без него. |

Создайте ключ в разделе **API Keys**, будучи авторизованным как администратор. Используйте выделенный ключ и, если возможно, выделенного администратора для мониторинга, чтобы ротация ключа или отключение пользователя не влияли на доступ конкретного человека.

## Endpoints {#endpoints}

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Процесс запущен | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL доступен и схема присутствует | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Агрегированный статус, включая актуальность синхронизации | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Формат текста Prometheus | 200 | 404 если отключено, 401, 403 |

Все четыре эндпоинта используют ту же аутентификацию, что и остальная часть API:

- отсутствует или недействительный ключ: `401` с `WWW-Authenticate: Bearer`
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

Liveness не взаимодействует с PostgreSQL. Он доказывает только то, что процесс принимает запросы, поэтому сбой базы данных не заставит Docker или Kubernetes перезапускать исправный процесс.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

Проверка открывает собственное соединение с PostgreSQL, запрашивает схему архива и прекращает попытки через две секунды. Она возвращает `503`, если база данных недоступна или отсутствует схема `mail_archiver`. Используйте этот эндпоинт, чтобы решить, должен ли экземпляр получать трафик.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Этот эндпоинт запускает проверку базы данных и проверку актуальности синхронизации. Устаревшая синхронизация делает отчет `Degraded`, который по-прежнему возвращает HTTP 200, так как процесс все еще может обрабатывать запросы. Только `Unhealthy` возвращает `503`.

Проверка синхронизации отключена по умолчанию. Установите `Health:SyncStaleMinutes` на количество минут, по истечении которых включенный аккаунт считается устаревшим:

```yaml
Health:
  SyncStaleMinutes: 90
```

Аккаунт, который никогда не синхронизировался, имеет `LastSync` со значением по умолчанию, поэтому он считается устаревшим сразу после включения проверки.

## Метрики {#metrics}

Эндпоинт возвращает данные в формате Prometheus text exposition (версия 0.0.4):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` должен быть `true`. `Metrics:RefreshMinutes` управляет частотой, с которой пересобирается
снимок (по умолчанию 5, значения ниже 1 округляются до 1). Сам процесс scrape только читает
снимок, поэтому опрос Prometheus не может превратиться в сканирование таблицы.

| Метрика | Тип | Лейблы | Значение |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1, когда эндпоинт метрик отвечает. |
| `mailarchiver_metrics_ready` | gauge | | 1 после первого успешного создания снимка. |
| `mailarchiver_db_up` | gauge | | 1, когда последнее обновление достигло PostgreSQL. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Unix time последнего успешного обновления. |
| `mailarchiver_emails` | gauge | | Количество архивированных писем. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Количество почтовых аккаунтов. |
| `mailarchiver_raw_messages` | gauge | | Количество сохраненных оригинальных сообщений. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1, когда установлен `RawMessages:Enabled`. |
| `mailarchiver_raw_messages_missing` | gauge | | Архивированные письма без сохраненного оригинала. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1, если аккаунт никогда не завершал синхронизацию. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix time последней успешной синхронизации. Отсутствует для аккаунтов, которые никогда не синхронизировались. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Секунд с момента последней успешной синхронизации. Отсутствует для аккаунтов, которые никогда не синхронизировались. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Оригинальные сообщения, которые не удалось сохранить. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Запуски синхронизации, завершившиеся ошибкой, ограничением частоты запросов (rate limiting) или таймаутом. |

Примечания:

- Счетчики являются локальными для процесса и сбрасываются в 0 после перезапуска. `rate()` и `increase()`
  в Prometheus обрабатывают это корректно.
- `mailarchiver_raw_messages_missing` учитывает каждое архивированное письмо без исходного сообщения,
  включая письма, архивированные до включения `RawMessages:Enabled`. В установках,
  где оригиналы сохранялись с первого дня, это показатель актуального разрыва.
- `mailarchiver_account_sync_age_seconds` не является полноценным бэклогом. Он показывает, когда завершилась последняя синхронизация,
  а не то, сколько сообщений ожидает на сервере.
- `mailarchiver_account_never_synced` существует потому, что у аккаунта, который никогда не синхронизировался, нет
  серии `sync_age`. Оповещение только по `sync_age` пропустило бы его.
- Эндпоинт содержит имена и id аккаунтов, но не содержит содержимого писем, тем или
  учетных данных.

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

Образ и встроенный `docker-compose.yml` проверяют `/api/v1/health/live` с помощью ключа администратора. Ключ не может существовать до первого запуска, поэтому первый запуск проходит без него, и контейнер сообщает о состоянии `unhealthy` до тех пор, пока ключ не будет установлен.

1. Запустите стек один раз с пустым `MAILARCHIVER_HEALTH_API_KEY`.
2. Войдите как администратор и создайте ключ с именем `monitoring-health` в разделе **API Keys**.
3. Поместите ключ в `.env`:

   ```dotenv
   MAILARCHIVER_HEALTH_API_KEY=<key>
   ```

4. Пересоздайте контейнер с `docker compose up -d`.
5. Проверьте состояние:

   ```bash
   docker inspect --format '{{.State.Health.Status}}' <container>
   ```

Требуется `Api:Enabled=true`. Если API отключен, проверка состояния (health check) получит ответ 404, и контейнер будет иметь статус unhealthy.

Если вы не хотите хранить ключ администратора в окружении контейнера, используйте TCP-проверку в контейнере и вызывайте `/api/v1/health/ready` с внешнего хоста мониторинга, используя ключ.
TCP-проверка не может определить, доступен ли PostgreSQL.

## Security {#security}

- Ключ является административным ключом. Он может считывать весь архив через API. Относитесь к нему как к
  паролю администратора.
- Используйте выделенного административного пользователя для мониторинга. Отключение этого пользователя отключает
  проверку состояния (health check) и метрики.
- Храните ключ в файле или в Docker secret, но никогда не используйте его в параметрах запроса. Параметры
  запроса попадают в логи прокси-серверов и логи доступа.
- Ограничьте доступ к хосту мониторинга или сети Prometheus. API key является
  аутентификацией, а не заменой сетевых границ.
- Эндпоинты состояния возвращают только строки статуса. Подробности исключений остаются в
  логе приложения.

## Troubleshooting {#troubleshooting}

| Симптом | Причина | Решение |
|---|---|---|
| 404 на всех четырех путях | `Api:Enabled=false` | Установите `Api:Enabled=true` и перезапустите. |
| 404 только на `/metrics` | `Metrics:Enabled=false` | Установите `Metrics:Enabled=true` и перезапустите. |
| 401 | Ключ отсутствует, неверен, отозван или истек | Создайте новый ключ и проверьте заголовок `Authorization: Bearer`. |
| 403 | Ключ принадлежит пользователю без прав администратора | Используйте ключ администратора. |
| `mailarchiver_metrics_ready 0` более минуты | Сервис обновления не завершил свой первый запуск или не может связаться с базой данных | Проверьте лог приложения на наличие `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | Последнее обновление завершилось ошибкой | Проверьте PostgreSQL и строку подключения. Старые значения сохраняются до следующего успешного обновления. |
| Контейнер остается unhealthy | Ключ пуст или `Api:Enabled=false` | Установите оба значения и пересоздайте контейнер. |
| `health/ready` возвращает 503 | PostgreSQL недоступен или отсутствует схема | Проверьте базу данных и выполнение миграций. |

## Что не охватывают метрики {#what-the-metrics-do-not-cover}

- Доступность IMAP или Graph для каждого аккаунта. `sync age` лишь показывает, что последний запуск был давно.
- Количество сообщений, ожидающих на сервере (реальный бэклог).
- Проверка контрольных сумм каждого сохраненного оригинала. Для этого используйте существующую проверку необработанных сообщений (`raw message verification`) и очистку сиротских записей (`orphan cleanup`).
- Подсчет сообщений по папкам или размер почтовых ящиков.

Для получения этих данных используйте страницу аккаунта, страницу фоновых задач (`background jobs`) и лог приложения.
