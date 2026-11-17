# Gezondheid en Metrieken {#health-and-metrics}

Mail-Archiver stelt health endpoints en een Prometheus-compatibel metrieken endpoint beschikbaar voor externe monitoring. Beide maken deel uit van de REST API: ze bevinden zich onder `/api/v1`, vereisen een API-sleutel, en vereisen dat die sleutel toebehoort aan een administrator. Er is geen anoniem health pad en geen aparte monitoring token.

## Vereisten {#requirements}

| Vereiste | Waarom |
|---|---|
| `Api:Enabled=true` | De endpoints bevinden zich onder `/api/`. Wanneer de API is uitgeschakeld, geeft de authentication middleware een 404. |
| Een admin API key | Health en metrics geven de instance-brede status weer, dus een niet-admin key krijgt een 403. |
| `Metrics:Enabled=true` | Het metrics endpoint is opt-in. Health werkt zonder dit endpoint. |

Maak de key aan onder **API Keys** terwijl u bent aangemeld als administrator. Gebruik een toegewezen key
en, indien mogelijk, een toegewezen admin user voor monitoring, zodat het roteren van de key of het uitschakelen
van de user de toegang van een persoon niet beïnvloedt.

## Endpoints {#endpoints}

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Proces is alive | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL bereikbaar en schema aanwezig | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Geaggregeerde status inclusief sync versheid | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus tekstformaat | 200 | 404 wanneer uitgeschakeld, 401, 403 |

Alle vier gebruiken dezelfde authenticatie als de rest van de API:

- ontbrekende of ongeldige sleutel: `401` met `WWW-Authenticate: Bearer`
- geldige sleutel zonder de admin rol: `403` met `application/problem+json`
- API uitgeschakeld: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Liveness raakt PostgreSQL niet aan. Het bewijst alleen dat het proces verzoeken accepteert, zodat een
database-onderbreking Docker of Kubernetes niet dwingt om een gezond proces te herstarten.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

De controle opent een eigen PostgreSQL-verbinding, vraagt naar het archive schema, en geeft
na twee seconden op. Het retourneert `503` wanneer de database onbereikbaar is of het
`mail_archiver` schema ontbreekt. Gebruik dit endpoint om te bepalen of een instantie verkeer
moet ontvangen.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Dit voert de databasecontrole en de sync versheidcontrole uit. Een verouderde sync maakt de rapportage
`Degraded`, die HTTP 200 blijft omdat het proces nog steeds verzoeken kan afhandelen. Alleen
`Unhealthy` retourneert `503`.

De sync-controle is standaard uitgeschakeld. Stel `Health:SyncStaleMinutes` in op het aantal
minuten waarna een ingeschakeld account als verouderd wordt beschouwd:

```yaml
Health:
  SyncStaleMinutes: 90
```

Een account dat nooit heeft gesynchroniseerd heeft `LastSync` op de standaardwaarde, dus het wordt als verouderd beschouwd
zodra de controle is ingeschakeld.

## Metrics {#metrics}

Het eindpunt geeft het Prometheus text exposition format (versie 0.0.4) terug:

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` moet `true` zijn. `Metrics:RefreshMinutes` bepaalt hoe vaak de snapshot
wordt opnieuw opgebouwd (standaard 5, waarden onder de 1 worden verhoogd naar 1). De scrape zelf leest alleen de
snapshot, dus een Prometheus poll kan niet uitlopen op een table scan.

| Metric | Type | Labels | Betekenis |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1 wanneer het metrics eindpunt reageert. |
| `mailarchiver_metrics_ready` | gauge | | 1 na de eerste succesvolle snapshot. |
| `mailarchiver_db_up` | gauge | | 1 wanneer de laatste verversing PostgreSQL heeft bereikt. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Unix-tijd van de laatste succesvolle verversing. |
| `mailarchiver_emails` | gauge | | Aantal gearchiveerde e-mails. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Aantal mail accounts. |
| `mailarchiver_raw_messages` | gauge | | Aantal opgeslagen originele berichten. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1 wanneer `RawMessages:Enabled` is ingesteld. |
| `mailarchiver_raw_messages_missing` | gauge | | Gearchiveerde e-mails zonder een opgeslagen origineel. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1 wanneer het account nooit een synchronisatie heeft voltooid. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix-tijd van de laatste succesvolle synchronisatie. Afwezig voor accounts die nooit zijn gesynchroniseerd. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Seconden sinds de laatste succesvolle synchronisatie. Afwezig voor accounts die nooit zijn gesynchroniseerd. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Originele berichten die niet konden worden opgeslagen. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Synchronisatie-runs die eindigden in een fout, rate limiting of timeout. |

Opmerkingen:

- De counters zijn proces-lokaal en beginnen bij 0 na een herstart. `rate()` en `increase()`
  in Prometheus verwerken dit correct.
- `mailarchiver_raw_messages_missing` telt elke gearchiveerde e-mail zonder een raw message,
  inclusief e-mails die zijn gearchiveerd voordat `RawMessages:Enabled` werd ingeschakeld. In een installatie
  die vanaf dag één originele berichten heeft opgeslagen, is dit het relevante gat.
- `mailarchiver_account_sync_age_seconds` is geen echte backlog. Het geeft aan wanneer de laatste synchronisatie
  is voltooid, niet hoeveel berichten er op de server wachten.
- `mailarchiver_account_never_synced` bestaat omdat een account dat nooit is gesynchroniseerd geen
  `sync_age`-reeks heeft. Een alert op alleen `sync_age` zou dit missen.
- Het eindpunt bevat accountnamen en ids, maar geen mailinhoud, onderwerpen of
  inloggegevens.

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

Het bestand met inloggegevens bevat de ruwe sleutel, zonder het `Bearer` prefix. Beperk de machtigingen ervan tot de Prometheus-gebruiker.

Voorbeeldmeldingen:

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

De image en de gebundelde `docker-compose.yml` controleren `/api/v1/health/live` met een admin-sleutel. De sleutel kan niet bestaan vóór de eerste start, dus de eerste start wordt zonder de sleutel uitgevoerd en de container rapporteert `unhealthy` totdat de sleutel is ingesteld.

1. Start de stack eenmaal met `MAILARCHIVER_HEALTH_API_KEY` leeg.
2. Log in als administrator en maak een sleutel genaamd `monitoring-health` aan onder
   **API Keys**.
3. Plaats de sleutel in `.env`:

   ```dotenv
   MAILARCHIVER_HEALTH_API_KEY=<key>
   ```

4. Maak de container opnieuw aan met `docker compose up -d`.
5. Controleer de status:

   ```bash
   docker inspect --format '{{.State.Health.Status}}' <container>
   ```

`Api:Enabled=true` is vereist. Wanneer de API is uitgeschakeld, ontvangt de health check een 404 en blijft de container unhealthy.

Als u geen admin-sleutel in de containeromgeving wilt, gebruik dan een TCP-check in de
container en roep `/api/v1/health/ready` aan vanaf een externe monitoring host met de sleutel.
De TCP-check kan niet bepalen of PostgreSQL bereikbaar is.

## Beveiliging {#security}

- De sleutel is een admin-sleutel. Deze kan het hele Archief lezen via de API. Behandel deze als een
  administrator wachtwoord.
- Gebruik een speciale admin-gebruiker voor monitoring. Het uitschakelen van die gebruiker schakelt de health check
  en de metrics uit.
- Bewaar de sleutel in een bestand of een Docker secret, nooit in een query parameter. Query parameters
  komen terecht in proxy- en access logs.
- Beperk de toegang tot de monitoring host of het Prometheus-netwerk. De API-sleutel is de
  authenticatie, geen vervanging voor netwerkbeperkingen.
- De health endpoints retourneren alleen status strings. Details over uitzonderingen blijven in het
  application log.

## Probleemoplossing {#troubleshooting}

| Symptoom | Oorzaak | Oplossing |
|---|---|---|
| 404 op alle vier de paden | `Api:Enabled=false` | Stel `Api:Enabled=true` in en start opnieuw op. |
| 404 alleen op `/metrics` | `Metrics:Enabled=false` | Stel `Metrics:Enabled=true` in en start opnieuw op. |
| 401 | Sleutel ontbreekt, is onjuist, ingetrokken of verlopen | Maak een nieuwe sleutel aan en controleer de `Authorization: Bearer` header. |
| 403 | Sleutel behoort tot een niet-beheerdersgebruiker | Gebruik een admin-sleutel. |
| `mailarchiver_metrics_ready 0` gedurende meer dan een minuut | De refresh-service heeft de eerste run niet voltooid, of kan de database niet bereiken | Controleer het applicatielogboek op `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | De laatste refresh is mislukt | Controleer PostgreSQL en de connection string. Oude tellingen blijven staan tot de volgende succesvolle run. |
| Container blijft unhealthy | Sleutel leeg of `Api:Enabled=false` | Stel beide in en maak de container opnieuw aan. |
| `health/ready` geeft 503 terug | PostgreSQL onbereikbaar of schema ontbreekt | Controleer de database en of de migraties zijn uitgevoerd. |

## Wat de metrieken niet dekken {#what-the-metrics-do-not-cover}

- IMAP- of Graph-bereikbaarheid per account. Een sync age geeft alleen aan dat de laatste run oud is.
- Het aantal berichten dat op de server wacht, de werkelijke backlog.
- Checksum-verificatie van elk opgeslagen origineel. Gebruik hiervoor de bestaande raw message verification
  en de orphan cleanup.
- Per-folder tellingen of mailbox groottes.

Gebruik hiervoor de account pagina, de background jobs pagina en het application log.
