# Gezondheid en Metrieken {#health-and-metrics}

Mail-Archiver stelt gezondheidspunten en een Prometheus-compatibel metriekenpunt beschikbaar voor externe monitoring. Beide maken deel uit van de REST API: ze bevinden zich onder `/api/v1`, vereisen een API-sleutel, en vereisen dat die sleutel toebehoort aan een administrator. Er is geen anoniem gezondheidspad en geen aparte monitoringtoken.

## Vereisten {#requirements}

| Vereiste | Waarom |
|---|---|
| `Api:Enabled=true` | De endpoints bevinden zich onder `/api/`. Wanneer de API is uitgeschakeld, geeft de authenticatie-middleware een 404. |
| Een admin API-sleutel | Health en metrics geven de status van de gehele instantie bloot, dus een niet-admin sleutel krijgt een 403. |
| `Metrics:Enabled=true` | De metrics endpoint is opt-in. Health werkt zonder deze. |

Maak de sleutel aan onder **API Keys** terwijl u bent ingelogd als administrator. Gebruik een specifieke sleutel
en, indien mogelijk, een specifieke admin gebruiker voor monitoring, zodat het roteren van de sleutel of het uitschakelen
van de gebruiker de toegang van een persoon niet beïnvloedt.

## Endpoints {#endpoints}

| Methode | Pad | Doel | Succes | Fouten |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Proces is actief | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL bereikbaar en schema aanwezig | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Geaggregeerde status inclusief synchronisatie versheid | 200 | 503, 401, 403 |
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

Liveness raakt PostgreSQL niet aan. Het bewijst alleen dat het proces verzoeken accepteert, zodat een database-uitval Docker of Kubernetes niet dwingt om een gezond proces te herstarten.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

De controle opent een eigen PostgreSQL-verbinding, vraagt naar het archive schema, en geeft het na twee seconden op. Het retourneert `503` wanneer de database onbereikbaar is of het `mail_archiver` schema ontbreekt. Gebruik dit endpoint om te beslissen of een instantie verkeer moet ontvangen.

### Geaggregeerde gezondheid {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Dit voert de databasecontrole en de synchronisatie versheidcontrole uit. Een verouderde synchronisatie maakt het rapport `Degraded`, wat HTTP 200 blijft omdat het proces nog steeds verzoeken kan afhandelen. Alleen `Unhealthy` retourneert `503`.

De synchronisatiecontrole is standaard uitgeschakeld. Stel `Health:SyncStaleMinutes` in op het aantal minuten waarna een geactiveerd account als verouderd wordt beschouwd:

```yaml
Health:
  SyncStaleMinutes: 90
```

Een account dat nooit heeft gesynchroniseerd heeft `LastSync` op de standaardwaarde, dus het wordt als verouderd beschouwd zodra de controle wordt ingeschakeld.

## Metrics {#metrics}

Het eindpunt geeft het Prometheus text exposition format terug (versie 0.0.4):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` moet `true` zijn. `Metrics:RefreshMinutes` bepaalt hoe vaak de snapshot
wordt opgebouwd (standaard 5, waarden onder de 1 worden verhoogd naar 1). De scrape zelf leest alleen de
snapshot, dus een Prometheus poll kan niet uitmonden in een table scan.

| Metric | Type | Labels | Betekenis |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1 wanneer de metrics endpoint reageert. |
| `mailarchiver_metrics_ready` | gauge | | 1 na de eerste succesvolle snapshot. |
| `mailarchiver_db_up` | gauge | | 1 wanneer de laatste refresh PostgreSQL heeft bereikt. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Unix tijd van de laatste succesvolle refresh. |
| `mailarchiver_emails` | gauge | | Aantal gearchiveerde e-mails. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Aantal mail accounts. |
| `mailarchiver_raw_messages` | gauge | | Aantal opgeslagen originele berichten. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1 wanneer `RawMessages:Enabled` is ingesteld. |
| `mailarchiver_raw_messages_missing` | gauge | | Gearchiveerde e-mails zonder een opgeslagen origineel. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1 wanneer het account nooit een synchronisatie heeft voltooid. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix tijd van de laatste succesvolle synchronisatie. Afwezig voor accounts die nooit hebben gesynchroniseerd. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Seconden sinds de laatste succesvolle synchronisatie. Afwezig voor accounts die nooit hebben gesynchroniseerd. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Originele berichten die niet opgeslagen konden worden. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Synchronisatie-runs die eindigden in een fout, rate limiting of timeout. |

Opmerkingen:

- De counters zijn proces-lokaal en beginnen bij 0 na een herstart. `rate()` en `increase()`
  in Prometheus verwerken dat correct.
- `mailarchiver_raw_messages_missing` telt elke gearchiveerde e-mail zonder een raw bericht,
  inclusief e-mails die zijn gearchiveerd voordat `RawMessages:Enabled` werd ingeschakeld. In een installatie
  die vanaf de eerste dag originele berichten heeft opgeslagen, is dit het relevante gat.
- `mailarchiver_account_sync_age_seconds` is geen echte backlog. Het geeft aan wanneer de laatste synchronisatie
  is voltooid, niet hoeveel berichten er op de server wachten.
- `mailarchiver_account_never_synced` bestaat omdat een account dat nooit is gesynchroniseerd geen
  `sync_age` serie heeft. Een alert op alleen `sync_age` zou dit missen.
- Het eindpunt bevat accountnamen en ids, maar geen mailinhoud, onderwerpen of
  credentials.

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

Het bestand met de credentials bevat de ruwe sleutel, zonder het `Bearer` prefix. Beperk de machtigingen ervan tot de Prometheus user.

Voorbeeld alerts:

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

De afbeelding bevat een liveness check die het root pad aanroept zonder authenticatie. Het root pad verwijst door naar de loginpagina voordat een controller wordt uitgevoerd, dus de check heeft geen API-sleutel nodig, werkt wanneer de REST API is uitgeschakeld en raakt PostgreSQL niet aan. Nieuwe installaties en upgrades rapporteren `healthy` zonder enige configuratie.

```bash
docker inspect --format '{{.State.Health.Status}}' <container>
```

De check bewijst dat het proces HTTP-verzoeken accepteert. Het bewijst niet dat PostgreSQL bereikbaar is. Roep hiervoor `/api/v1/health/ready` aan vanaf een externe monitoring host met een admin API-sleutel en `Api:Enabled=true`; het eindpunt antwoordt met 503 wanneer de database niet beschikbaar is.

De meegeleverde `docker-compose.yml` overschrijft de check niet. Voeg een `healthcheck` blok toe aan de service als u een ander interval, timeout of commando nodig heeft, of stel `healthcheck: disable: true` in om de check uit te schakelen.

De voormalige `MAILARCHIVER_HEALTH_API_KEY` variabele wordt niet meer gebruikt. Verwijder deze uit bestaande compose bestanden en `.env` bestanden; het laten staan ervan is onschadelijk.

## Beveiliging {#security}

- De sleutel is een admin-sleutel. Deze kan het hele Archief lezen via de API. Behandel deze als een
  administrator wachtwoord.
- Gebruik een specifieke admin-gebruiker voor monitoring. Het uitschakelen van die gebruiker schakelt de externe
  health en metrics endpoints uit, niet de eigen liveness check van de container.
- Bewaar de sleutel in een bestand of een Docker secret, nooit in een query parameter. Query parameters
  komen terecht in proxy- en access logs.
- Beperk de toegang tot de monitoring host of het Prometheus netwerk. De API key is de
  authenticatie, geen vervanging voor netwerkbeperkingen.
- De health endpoints retourneren alleen status strings. Details over uitzonderingen blijven in de
  application log.

## Troubleshooting {#troubleshooting}

| Symptoom | Oorzaak | Oplossing |
|---|---|---|
| 404 op alle vier de paden | `Api:Enabled=false` | Stel `Api:Enabled=true` in en herstart. |
| 404 alleen op `/metrics` | `Metrics:Enabled=false` | Stel `Metrics:Enabled=true` in en herstart. |
| 401 | Sleutel ontbreekt, is onjuist, ingetrokken of verlopen | Maak een nieuwe sleutel aan en controleer de `Authorization: Bearer` header. |
| 403 | Sleutel behoort tot een niet-admin gebruiker | Gebruik een admin sleutel. |
| `mailarchiver_metrics_ready 0` voor meer dan een minuut | De refresh service heeft de eerste run niet voltooid, of kan de database niet bereiken | Controleer het applicatielogboek op `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | De laatste refresh is mislukt | Controleer PostgreSQL en de connection string. Oude tellingen blijven staan tot de volgende succesvolle run. |
| Container blijft unhealthy | Sleutel leeg of `Api:Enabled=false` | Stel beide in en maak de container opnieuw aan. |
| `health/ready` geeft 503 terug | PostgreSQL onbereikbaar of schema ontbreekt | Controleer de database en of de migraties zijn uitgevoerd. |

## Wat de metrieken niet dekken {#what-the-metrics-do-not-cover}

- IMAP of Graph bereikbaarheid per Account. Een sync age geeft alleen aan dat de laatste run oud is.
- Het aantal berichten dat op de server wacht, de werkelijke achterstand.
- Checksum verificatie van elk opgeslagen origineel. Gebruik hiervoor de bestaande raw message verificatie
  en de orphan cleanup.
- Per-folder tellingen of Postbus grootte.

Gebruik hiervoor de account pagina, de background jobs pagina en het applicatielog.
