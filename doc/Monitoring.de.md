# Health und Metriken {#health-and-metrics}

Mail-Archiver stellt Health-Endpunkte und einen Prometheus-kompatiblen Metriken-Endpunkt für das externe Monitoring bereit. Beide sind Teil der REST API: Sie befinden sich unter `/api/v1`, erfordern einen API-Key und setzen voraus, dass dieser Key einem Administrator gehört. Es gibt keinen anonymen Health-Pfad und keinen separaten Monitoring-Token.

## Anforderungen {#requirements}

| Anforderung | Warum |
|---|---|
| `Api:Enabled=true` | Die Endpunkte befinden sich unter `/api/`. Wenn die API deaktiviert ist, antwortet die Authentifizierungs-Middleware mit 404. |
| Ein Admin-API-Key | Health und Metrics legen den instanzweiten Zustand offen, daher erhält ein Nicht-Admin-Key einen 403. |
| `Metrics:Enabled=true` | Der Metrics-Endpunkt ist optional. Health funktioniert auch ohne diesen. |

Erstellen Sie den Key unter **API Keys**, während Sie als Administrator angemeldet sind. Verwenden Sie einen dedizierten Key
und, wenn möglich, einen dedizierten Admin-Benutzer für das Monitoring, damit das Rotieren des Keys oder das Deaktivieren
des Benutzers den Zugriff einer Person nicht beeinträchtigt.

## Endpoints {#endpoints}

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Process is alive | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL reachable and schema present | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Aggregate status including sync freshness | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus text format | 200 | 404 when disabled, 401, 403 |

Alle vier verwenden dieselbe Authentifizierung wie der Rest der API:

- fehlender oder ungültiger Key: `401` mit `WWW-Authenticate: Bearer`
- gültiger Key ohne die Admin-Rolle: `403` mit `application/problem+json`
- API deaktiviert: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Liveness greift nicht auf PostgreSQL zu. Es beweist nur, dass der Prozess Anfragen akzeptiert, sodass ein Datenbankausfall Docker oder Kubernetes nicht dazu veranlasst, einen gesunden Prozess neu zu starten.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

Die Prüfung öffnet ihre eigene PostgreSQL-Verbindung, fragt nach dem Archiv-Schema und bricht nach zwei Sekunden ab. Sie gibt `503` zurück, wenn die Datenbank nicht erreichbar ist oder das `mail_archiver`-Schema fehlt. Verwenden Sie diesen Endpunkt, um zu entscheiden, ob eine Instanz Traffic erhalten sollte.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Dies führt die Datenbankprüfung und die Prüfung der Synchronisierungsaktualität aus. Eine veraltete Synchronisierung macht den Bericht `Degraded`, der bei HTTP 200 bleibt, da der Prozess weiterhin Anfragen bedienen kann. Nur `Unhealthy` gibt `503` zurück.

Die Synchronisierungsprüfung ist standardmäßig deaktiviert. Setzen Sie `Health:SyncStaleMinutes` auf die Anzahl der Minuten, nach denen ein aktiviertes Konto als veraltet gilt:

```yaml
Health:
  SyncStaleMinutes: 90
```

Ein Konto, das noch nie synchronisiert hat, hat `LastSync` mit dem Standardwert, sodass es als veraltet gilt, sobald die Prüfung aktiviert wird.

## Metriken {#metrics}

Der Endpunkt gibt das Prometheus Text-Exposition-Format (Version 0.0.4) zurück:

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` muss `true` sein. `Metrics:RefreshMinutes` steuert, wie oft der Snapshot
neu erstellt wird (Standard 5, Werte unter 1 werden auf 1 aufgerundet). Der Scrape selbst liest nur den
Snapshot, sodass ein Prometheus-Poll nicht in einen Table Scan ausarten kann.

| Metrik | Typ | Labels | Bedeutung |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1, wenn der Metriken-Endpunkt antwortet. |
| `mailarchiver_metrics_ready` | gauge | | 1 nach dem ersten erfolgreichen Snapshot. |
| `mailarchiver_db_up` | gauge | | 1, wenn die letzte Aktualisierung PostgreSQL erreicht hat. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Unix-Zeit der letzten erfolgreichen Aktualisierung. |
| `mailarchiver_emails` | gauge | | Anzahl der archivierten E-Mails. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Anzahl der Mail-Konten. |
| `mailarchiver_raw_messages` | gauge | | Anzahl der gespeicherten Originalnachrichten. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1, wenn `RawMessages:Enabled` gesetzt ist. |
| `mailarchiver_raw_messages_missing` | gauge | | Archivierte E-Mails ohne gespeichertes Original. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1, wenn das Konto noch nie eine Synchronisierung abgeschlossen hat. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix-Zeit der letzten erfolgreichen Synchronisierung. Nicht vorhanden bei Konten, die nie synchronisiert haben. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Sekunden seit der letzten erfolgreichen Synchronisierung. Nicht vorhanden bei Konten, die nie synchronisiert haben. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Originalnachrichten, die nicht gespeichert werden konnten. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Synchronisierungsdurchläufe, die mit einem Fehler, Rate Limiting oder Timeout endeten. |

Hinweise:

- Die Counter sind prozesslokal und starten nach einem Neustart bei 0. `rate()` und `increase()`
  in Prometheus handhaben dies korrekt.
- `mailarchiver_raw_messages_missing` zählt jede archivierte E-Mail ohne Rohnachricht,
  einschließlich E-Mails, die archiviert wurden, bevor `RawMessages:Enabled` aktiviert wurde. In einer Installation,
  die seit dem ersten Tag Originale gespeichert hat, stellt dies die relevante Lücke dar.
- `mailarchiver_account_sync_age_seconds` ist kein echter Backlog. Er gibt an, wann die letzte Synchronisierung
  beendet wurde, nicht wie viele Nachrichten auf dem Server warten.
- `mailarchiver_account_never_synced` existiert, weil ein nie synchronisiertes Konto keine
  `sync_age`-Serie hat. Ein Alert nur auf `sync_age` würde dies übersehen.
- Der Endpunkt enthält Kontonamen und IDs, aber keine Mail-Inhalte, Betreffzeilen oder
  Anmeldedaten.

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

Die Datei mit den Anmeldedaten enthält den Rohschlüssel ohne das `Bearer`-Präfix. Beschränken Sie die Berechtigungen auf den Prometheus-Benutzer.

Beispielhafte Alarme:

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

Das Image und der gebündelte `docker-compose.yml` prüfen `/api/v1/health/live` mit einem Admin-Key. Der Key kann vor dem ersten Start nicht existieren, daher läuft der erste Start ohne ihn und der Container meldet `unhealthy`, bis der Key gesetzt ist.

1. Starten Sie den Stack einmal mit leerem `MAILARCHIVER_HEALTH_API_KEY`.
2. Melden Sie sich als Administrator an und erstellen Sie einen Key namens `monitoring-health` unter
   **API Keys**.
3. Fügen Sie den Key in `.env` ein:

   ```dotenv
   MAILARCHIVER_HEALTH_API_KEY=<key>
   ```

4. Erstellen Sie den Container mit `docker compose up -d` neu.
5. Überprüfen Sie den Status:

   ```bash
   docker inspect --format '{{.State.Health.Status}}' <container>
   ```

`Api:Enabled=true` ist erforderlich. Wenn die API deaktiviert ist, erhält der health check einen 404-Fehler und der
Container bleibt unhealthy.

Wenn Sie keinen Admin-Key in der Container-Umgebung verwenden möchten, nutzen Sie einen TCP-Check im
Container und rufen Sie `/api/v1/health/ready` von einem externen Monitoring-Host mit dem Key auf.
Der TCP-Check kann nicht feststellen, ob PostgreSQL erreichbar ist.

## Sicherheit {#security}

- Der Schlüssel ist ein Admin-Schlüssel. Er kann das gesamte Archiv über die API lesen. Behandeln Sie ihn wie ein
  Administrator-Passwort.
- Verwenden Sie einen dedizierten Admin-Benutzer für das Monitoring. Das Deaktivieren dieses Benutzers deaktiviert den Health Check
  und die Metriken.
- Speichern Sie den Schlüssel in einer Datei oder einem Docker secret, niemals in einem Query-Parameter. Query-Parameter
  landen in Proxy- und Access-Logs.
- Beschränken Sie den Zugriff auf den Monitoring-Host oder das Prometheus-Netzwerk. Der API-Schlüssel ist die
  Authentifizierung, kein Ersatz für Netzwerkgrenzen.
- Die Health-Endpoints geben nur Status-Strings zurück. Details zu Ausnahmen verbleiben im
  Anwendungsprotokoll.

## Fehlerbehebung {#troubleshooting}

| Symptom | Ursache | Lösung |
|---|---|---|
| 404 auf allen vier Pfaden | `Api:Enabled=false` | Setzen Sie `Api:Enabled=true` und starten Sie neu. |
| 404 nur auf `/metrics` | `Metrics:Enabled=false` | Setzen Sie `Metrics:Enabled=true` und starten Sie neu. |
| 401 | Schlüssel fehlt, ist falsch, widerrufen oder abgelaufen | Erstellen Sie einen neuen Schlüssel und prüfen Sie den `Authorization: Bearer` Header. |
| 403 | Schlüssel gehört zu einem Nicht-Admin-Benutzer | Verwenden Sie einen Admin-Schlüssel. |
| `mailarchiver_metrics_ready 0` für mehr als eine Minute | Der Refresh-Service hat seinen ersten Durchlauf noch nicht abgeschlossen oder kann die Datenbank nicht erreichen | Überprüfen Sie das Anwendungsprotokoll auf `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | Der letzte Refresh ist fehlgeschlagen | Überprüfen Sie PostgreSQL und den Connection String. Alte Zählwerte bleiben bis zum nächsten Erfolg bestehen. |
| Container bleibt unhealthy | Schlüssel leer oder `Api:Enabled=false` | Setzen Sie beide und erstellen Sie den Container neu. |
| `health/ready` gibt 503 zurück | PostgreSQL nicht erreichbar oder Schema fehlt | Überprüfen Sie die Datenbank und ob die Migrationen ausgeführt wurden. |

## Was die Metriken nicht abdecken {#what-the-metrics-do-not-cover}

- IMAP- oder Graph-Erreichbarkeit pro Konto. Ein Sync-Alter zeigt lediglich an, dass der letzte Durchlauf alt ist.
- Die Anzahl der auf dem Server wartenden Nachrichten, der tatsächliche Backlog.
- Prüfsummenverifizierung jedes gespeicherten Originals. Verwenden Sie hierfür die bestehende Rohnachrichten-Verifizierung und die Bereinigung von Waisen (orphan cleanup).
- Pro-Ordner-Zählungen oder Postfachgrößen.

Verwenden Sie hierfür die Konto-Seite, die Seite für Hintergrundaufträge und das Anwendungsprotokoll.
