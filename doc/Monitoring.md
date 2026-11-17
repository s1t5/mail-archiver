# Health and Metrics

Mail-Archiver exposes health endpoints and a Prometheus-compatible metrics endpoint for
external monitoring. Both are part of the REST API: they live under `/api/v1`, require an
API key, and require that key to belong to an administrator. There is no anonymous health
path and no separate monitoring token.

## Requirements

| Requirement | Why |
|---|---|
| `Api:Enabled=true` | The endpoints live under `/api/`. With the API disabled the authentication middleware answers 404. |
| An admin API key | Health and metrics expose instance-wide state, so a non-admin key gets 403. |
| `Metrics:Enabled=true` | The metrics endpoint is opt-in. Health works without it. |

Create the key under **API Keys** while signed in as an administrator. Use a dedicated key
and, if possible, a dedicated admin user for monitoring, so rotating the key or disabling
the user does not affect a person's access.

## Endpoints

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Process is alive | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL reachable and schema present | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Aggregate status including sync freshness | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus text format | 200 | 404 when disabled, 401, 403 |

All four use the same authentication as the rest of the API:

- missing or invalid key: `401` with `WWW-Authenticate: Bearer`
- valid key without the admin role: `403` with `application/problem+json`
- API disabled: `404`

### Liveness

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Liveness does not touch PostgreSQL. It proves only that the process accepts requests, so a
database outage does not make Docker or Kubernetes restart a healthy process.

### Readiness

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

The check opens its own PostgreSQL connection, asks for the archive schema, and gives up
after two seconds. It returns `503` when the database is unreachable or the
`mail_archiver` schema is missing. Use this endpoint to decide whether an instance should
receive traffic.

### Aggregate health

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

This runs the database check and the sync freshness check. A stale sync makes the report
`Degraded`, which stays HTTP 200 because the process can still serve requests. Only
`Unhealthy` returns `503`.

The sync check is disabled by default. Set `Health:SyncStaleMinutes` to the number of
minutes after which an enabled account counts as stale:

```yaml
Health:
  SyncStaleMinutes: 90
```

An account that has never synced has `LastSync` at the default value, so it counts as stale
as soon as the check is enabled.

## Metrics

The endpoint returns the Prometheus text exposition format (version 0.0.4):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` must be `true`. `Metrics:RefreshMinutes` controls how often the snapshot
is rebuilt (default 5, values below 1 are raised to 1). The scrape itself only reads the
snapshot, so a Prometheus poll cannot turn into a table scan.

| Metric | Type | Labels | Meaning |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1 when the metrics endpoint responds. |
| `mailarchiver_metrics_ready` | gauge | | 1 after the first successful snapshot. |
| `mailarchiver_db_up` | gauge | | 1 when the last refresh reached PostgreSQL. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Unix time of the last successful refresh. |
| `mailarchiver_emails` | gauge | | Number of archived emails. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Number of mail accounts. |
| `mailarchiver_raw_messages` | gauge | | Number of stored original messages. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1 when `RawMessages:Enabled` is set. |
| `mailarchiver_raw_messages_missing` | gauge | | Archived emails without a stored original. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1 when the account has never completed a sync. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix time of the last successful sync. Absent for accounts that never synced. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Seconds since the last successful sync. Absent for accounts that never synced. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Original messages that could not be stored. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Sync runs that ended in failure, rate limiting or timeout. |

Notes:

- The counters are process-local and start at 0 after a restart. `rate()` and `increase()`
  in Prometheus handle that correctly.
- `mailarchiver_raw_messages_missing` counts every archived email without a raw message,
  including emails archived before `RawMessages:Enabled` was turned on. In an installation
  that has stored originals from day one, it is the relevant gap.
- `mailarchiver_account_sync_age_seconds` is not a true backlog. It says when the last sync
  finished, not how many messages are waiting on the server.
- `mailarchiver_account_never_synced` exists because a never-synced account has no
  `sync_age` series. An alert on `sync_age` alone would miss it.
- The endpoint contains account names and ids, but no mail contents, subjects or
  credentials.

## Prometheus

```yaml
scrape_configs:
  - job_name: mail-archiver
    scrape_interval: 30s
    authorization:
      credentials_file: /etc/prometheus/mailarchiver-api-key
    static_configs:
      - targets: ["mail-archiver:5000"]
```

The credentials file contains the raw key, without the `Bearer` prefix. Restrict its
permissions to the Prometheus user.

Example alerts:

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

## Docker health check

The image and the bundled `docker-compose.yml` check `/api/v1/health/live` with an admin
key. The key cannot exist before the first start, so the first start runs without it and
the container reports `unhealthy` until the key is set.

1. Start the stack once with `MAILARCHIVER_HEALTH_API_KEY` empty.
2. Sign in as an administrator and create a key named `monitoring-health` under
   **API Keys**.
3. Put the key in `.env`:

   ```dotenv
   MAILARCHIVER_HEALTH_API_KEY=<key>
   ```

4. Recreate the container with `docker compose up -d`.
5. Check the state:

   ```bash
   docker inspect --format '{{.State.Health.Status}}' <container>
   ```

`Api:Enabled=true` is required. With the API disabled the health check receives 404 and the
container stays unhealthy.

If you do not want an admin key in the container environment, use a TCP check in the
container and call `/api/v1/health/ready` from an external monitoring host with the key.
The TCP check cannot tell whether PostgreSQL is reachable.

## Security

- The key is an admin key. It can read the whole archive through the API. Treat it like an
  administrator password.
- Use a dedicated admin user for monitoring. Disabling that user disables the health check
  and the metrics.
- Keep the key in a file or a Docker secret, never in a query parameter. Query parameters
  end up in proxy and access logs.
- Restrict access to the monitoring host or the Prometheus network. The API key is the
  authentication, not a substitute for network boundaries.
- The health endpoints return only status strings. Exception details stay in the
  application log.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| 404 on all four paths | `Api:Enabled=false` | Set `Api:Enabled=true` and restart. |
| 404 only on `/metrics` | `Metrics:Enabled=false` | Set `Metrics:Enabled=true` and restart. |
| 401 | Key missing, wrong, revoked or expired | Create a new key and check the `Authorization: Bearer` header. |
| 403 | Key belongs to a non-admin user | Use an admin key. |
| `mailarchiver_metrics_ready 0` for more than a minute | The refresh service has not completed its first run, or cannot reach the database | Check the application log for `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | The last refresh failed | Check PostgreSQL and the connection string. Old counts remain until the next success. |
| Container stays unhealthy | Key empty or `Api:Enabled=false` | Set both and recreate the container. |
| `health/ready` returns 503 | PostgreSQL unreachable or schema missing | Check the database and whether migrations ran. |

## What the metrics do not cover

- IMAP or Graph reachability per account. A sync age only shows that the last run is old.
- The number of messages waiting on the server, the true backlog.
- Checksum verification of every stored original. Use the existing raw message verification
  and the orphan cleanup for that.
- Per-folder counts or mailbox sizes.

For these, use the account page, the background jobs page and the application log.
