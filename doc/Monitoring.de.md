# Health and Metrics {#health-and-metrics}

Mail-Archiver stellt Health-Endpunkte und einen Prometheus-kompatiblen Metriken-Endpunkt für das externe Monitoring bereit. Beide sind Teil der REST API: Sie befinden sich unter `/api/v1`, erfordern einen API-Key und setzen voraus, dass dieser Key einem Administrator gehört. Es gibt keinen anonymen Health-Pfad und kein separates Monitoring-Token.

## Anforderungen {#requirements}

| Anforderung | Warum |
|---|---|
| `Api:Enabled=true` | Die Endpunkte befinden sich unter `/api/`. Wenn die API deaktiviert ist, antwortet die Authentifizierungs-Middleware mit 404. |
| Ein Admin-API-Schlüssel | Health und Metrics legen den instanzweiten Zustand offen, daher erhält ein Nicht-Admin-Schlüssel 403. |
| `Metrics:Enabled=true` | Der Metrics-Endpunkt ist optional. Health funktioniert auch ohne ihn. |

Erstellen Sie den Schlüssel unter **API Keys**, während Sie als Administrator angemeldet sind. Verwenden Sie einen dedizierten Schlüssel und, wenn möglich, einen dedizierten Admin-Benutzer für das Monitoring, damit das Rotieren des Schlüssels oder das Deaktivieren des Benutzers den Zugriff einer Person nicht beeinträchtigt.

## Endpoints {#endpoints}

| Methode | Pfad | Zweck | Erfolg | Fehler |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Prozess ist aktiv | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL erreichbar und Schema vorhanden | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Aggregierter Status inklusive Synchronisierung-Frische | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus-Textformat | 200 | 404 wenn deaktiviert, 401, 403 |

Alle vier nutzen dieselbe Authentifizierung wie der Rest der API:

- fehlender oder ungültiger Schlüssel: `401` mit `WWW-Authenticate: Bearer`
- gültiger Schlüssel ohne die Admin-Rolle: `403` mit `application/problem+json`
- API deaktiviert: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Liveness greift nicht auf PostgreSQL zu. Es beweist lediglich, dass der Prozess Anfragen annimmt, sodass ein Datenbankausfall Docker oder Kubernetes nicht dazu veranlasst, einen gesunden Prozess neu zu starten.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

Die Prüfung öffnet eine eigene PostgreSQL-Verbindung, fragt nach dem Archiv-Schema und bricht nach zwei Sekunden ab. Sie gibt `503` zurück, wenn die Datenbank nicht erreichbar ist oder das `mail_archiver`-Schema fehlt. Verwenden Sie diesen Endpunkt, um zu entscheiden, ob eine Instanz Traffic erhalten sollte.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Dies führt die Datenbankprüfung und die Prüfung der Synchronisierung-Frische aus. Eine veraltete Synchronisierung macht den Bericht `Degraded`, der weiterhin HTTP 200 zurückgibt, da der Prozess weiterhin Anfragen bedienen kann. Nur `Unhealthy` gibt `503` zurück.

Die Synchronisierung-Prüfung ist standardmäßig deaktiviert. Setzen Sie `Health:SyncStaleMinutes` auf die Anzahl der Minuten, nach denen ein aktiviertes Konto als veraltet gilt:

```yaml
Health:
  SyncStaleMinutes: 90
```

Ein Konto, das noch nie synchronisiert hat, hat `LastSync` mit dem Standardwert, sodass es als veraltet gilt, sobald die Prüfung aktiviert wird.

## Metriken {#metrics}

Der Endpunkt gibt das Prometheus Text Exposition Format (Version 0.0.4) zurück:

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` muss `true` sein. `Metrics:RefreshMinutes` steuert, wie oft der Snapshot
neu erstellt wird (Standard 5, Werte unter 1 werden auf 1 gesetzt). Das Scrape selbst liest nur den
Snapshot, sodass eine Prometheus-Abfrage nicht zu einem Tabellenscan werden kann.

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
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix-Zeit der letzten erfolgreichen Synchronisierung. Fehlt bei Konten, die nie synchronisiert wurden. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Sekunden seit der letzten erfolgreichen Synchronisierung. Fehlt bei Konten, die nie synchronisiert wurden. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Originalnachrichten, die nicht gespeichert werden konnten. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Synchronisierungsdurchläufe, die mit einem Fehler, Rate Limiting oder Timeout endeten. |

Hinweise:

- Die Counter sind prozesslokal und beginnen nach einem Neustart bei 0. `rate()` und `increase()`
  in Prometheus handhaben dies korrekt.
- `mailarchiver_raw_messages_missing` zählt jede archivierte E-Mail ohne Rohnachricht,
  einschließlich E-Mails, die archiviert wurden, bevor `RawMessages:Enabled` aktiviert wurde. In einer Installation,
  die von Beginn an Originale gespeichert hat, stellt dies die relevante Lücke dar.
- `mailarchiver_account_sync_age_seconds` ist kein echter Rückstau. Es gibt an, wann die letzte Synchronisierung
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

Die Datei mit den Anmeldedaten enthält den Rohschlüssel ohne das `Bearer`-Präfix. Beschränken Sie dessen Berechtigungen auf den Prometheus-Benutzer.

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

## Docker Health Check {#docker-health-check}

Das Image enthält einen Liveness-Check, der den Root-Pfad ohne Authentifizierung aufruft. Der Root-Pfad leitet zur Login-Seite weiter, bevor ein Controller ausgeführt wird, daher benötigt der Check keinen API-Key, funktioniert auch mit deaktivierter REST-API und greift nicht auf PostgreSQL zu. Neuinstallationen und Upgrades melden `healthy` ohne jegliche Konfiguration.

```bash
docker inspect --format '{{.State.Health.Status}}' <container>
```

Der Check beweist, dass der Prozess HTTP-Anfragen akzeptiert. Er beweist nicht, dass PostgreSQL erreichbar ist. Rufen Sie hierfür `/api/v1/health/ready` von einem externen Monitoring-Host mit einem Admin-API-Key und `Api:Enabled=true` auf; der Endpunkt antwortet mit 503, wenn die Datenbank nicht erreichbar ist.

Die enthaltene `docker-compose.yml` überschreibt den Check nicht. Fügen Sie einen `healthcheck`-Block zum Service hinzu, wenn Sie ein anderes Intervall, ein anderes Timeout oder einen anderen Befehl benötigen, oder setzen Sie `healthcheck: disable: true`, um den Check zu deaktivieren.

Die ehemalige `MAILARCHIVER_HEALTH_API_KEY`-Variable wird nicht mehr verwendet. Entfernen Sie sie aus bestehenden Compose-Dateien und `.env`-Dateien; das Beibehalten ist harmlos.

## Sicherheit {#security}

- Der Schlüssel ist ein Admin-Schlüssel. Er kann über die API das gesamte Archiv lesen. Behandeln Sie ihn wie ein Administratorpasswort.
- Verwenden Sie einen dedizierten Admin-Benutzer für das Monitoring. Das Deaktivieren dieses Benutzers deaktiviert die externen Health- und Metrics-Endpunkte, nicht den Liveness-Check des Containers selbst.
- Speichern Sie den Schlüssel in einer Datei oder einem Docker Secret, niemals in einem Query-Parameter. Query-Parameter landen in Proxy- und Access-Logs.
- Beschränken Sie den Zugriff auf den Monitoring-Host oder das Prometheus-Netzwerk. Der API-Schlüssel ist die Authentifizierung, kein Ersatz für Netzwerk-Grenzen.
- Die Health-Endpunkte geben nur Status-Strings zurück. Details zu Ausnahmen verbleiben im Anwendungsprotokoll.

## Fehlerbehebung {#troubleshooting}

| Symptom | Ursache | Lösung |
|---|---|---|
| 404 auf allen vier Pfaden | `Api:Enabled=false` | Setzen Sie `Api:Enabled=true` und starten Sie neu. |
| 404 nur auf `/metrics` | `Metrics:Enabled=false` | Setzen Sie `Metrics:Enabled=true` und starten Sie neu. |
| 401 | Key fehlt, ist falsch, widerrufen oder abgelaufen | Erstellen Sie einen neuen Key und prüfen Sie den `Authorization: Bearer` Header. |
| 403 | Key gehört zu einem Nicht-Admin-Benutzer | Verwenden Sie einen Admin-Key. |
| `mailarchiver_metrics_ready 0` für mehr als eine Minute | Der Refresh-Service hat seinen ersten Durchlauf noch nicht abgeschlossen oder kann die Datenbank nicht erreichen | Prüfen Sie das Anwendungsprotokoll auf `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | Der letzte Refresh ist fehlgeschlagen | Prüfen Sie PostgreSQL und den Connection String. Alte Zählwerte bleiben bis zum nächsten Erfolg bestehen. |
| Container bleibt unhealthy | Key leer oder `Api:Enabled=false` | Setzen Sie beide und erstellen Sie den Container neu. |
| `health/ready` liefert 503 | PostgreSQL nicht erreichbar oder Schema fehlt | Prüfen Sie die Datenbank und ob die Migrationen ausgeführt wurden. |

## Was die Metriken nicht abdecken {#what-the-metrics-do-not-cover}

- IMAP- oder Graph-Erreichbarkeit pro Konto. Ein Sync-Alter zeigt lediglich an, dass der letzte Durchlauf alt ist.
- Die Anzahl der Nachrichten, die auf dem Server warten, der tatsächliche Rückstau.
- Prüfsummenverifizierung jedes gespeicherten Originals. Verwenden Sie dafür die vorhandene Rohnachrichten-Verifizierung und die Bereinigung von Waisen (orphan cleanup).
- Pro-Ordner-Zählungen oder Postfachgrößen.

Verwenden Sie hierfür die Kontoseite, die Seite für Hintergrundjobs und das Anwendungsprotokoll.
