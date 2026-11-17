# Zdravje in metrike {#health-and-metrics}

Mail-Archiver razpoloaga zdravstvene končnice (health endpoints) in metrično končnico, zd compatible s sistemom Prometheus, za zunanje spremljanje. Obe sta del REST API: nahajata se pod `/api/v1`, zahtevata API ključ in zahtevata, da ta ključ pripada administratorju. Brezanonimne poti za zdravje ni in ločenega žetonja (token) za spremljanje ni.

## Zahteve {#requirements}

| Zahteva | Zakaj |
|---|---|
| `Api:Enabled=true` | Kontejnerji (endpoints) se nahajajo pod `/api/`. Če je API onemogočen, 인증na posredniška programska oprema (authentication middleware) vrne napako 404. |
| Admin API ključ | Health in metrics raz暴露ujajo stanje na ravni celotne instance, zato ključ brez admin pravic vrne napako 403. |
| `Metrics:Enabled=true` | Kontejner (endpoint) za metrike je izbirnega tipa (opt-in). Health deluje brez njega. |

Ustvarite ključ pod **API Keys**, ko ste prijavljeni kot administrator. Uporabite namenski ključ in, če je mogoče, namenskega admin uporabnika za spremljanje, da rotacija ključa ali onemogočitev uporabnika ne bo vplivala na dostop posameznika.

## Endpoints {#endpoints}

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Process is alive | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL reachable and schema present | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Aggregate status including sync freshness | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus text format | 200 | 404 when disabled, 401, 403 |

Vsi štirje uporabljajo isto avtentikacijo kot preostali del API:

- manjkajoč ali neveljavni ključ: `401` s `WWW-Authenticate: Bearer`
- veljavni ključ brez vloge admin: `403` s `application/problem+json`
- API onemogočen: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Liveness ne vpliva na PostgreSQL. Dokazuje le, da proces sprejema zahteve, zato izpad baze podatkov ne povzroči, da bi Docker ali Kubernetes ponovno zagnanal zdravo delujoč proces.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

Preverjanje odpre svojo PostgreSQL povezavo, vpraša za shemo arhiva in odda, če ne uspe v dveh sekundah. Vrne `503`, ko je baza podatkov nedostopna ali ko manjka shema `mail_archiver`. Uporabite ta endpoint, da se odločite, ali bi morala instanca prejemati promet.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

To izvaja preverjanje baze podatkov in preverjanje svežine sinhronizacije. Zastarela sinhronizacija naredi poročilo `Degraded`, ki ostane HTTP 200, ker lahko proces še vedno obdeluje zahteve. Le `Unhealthy` vrne `503`.

Preverjanje sinhronizacije je privzeto onemogočeno. Nastavite `Health:SyncStaleMinutes` na število
minut, po katerih se omogočena številka računa šteje za zastarelo:

```yaml
Health:
  SyncStaleMinutes: 90
```

Račun, ki nikoli ni bil sinhroniziran, ima `LastSync` na privzeti vrednosti, zato se šteje za zastarelo takoj, ko je preverjanje omogočeno.

## Metrike {#metrics}

Končališče vrne format tekstovne izpostavitve Prometheus (verzija 0.0.4):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` mora biti `true`. `Metrics:RefreshMinutes` določa, kako pogosto se
posodobitev (snapshot) ponovno zgradi (privzeto 5, vrednosti pod 1 se dvignejo na 1). Sam scrape samo bere
posodobitev, zato povpraševanje (poll) v Prometheus ne more postati pregled tabele (table scan).

| Metrika | Tip | Etikete | Pomen |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1, ko končališče metrik odgovori. |
| `mailarchiver_metrics_ready` | gauge | | 1 po prvi uspešni posodobitvi. |
| `mailarchiver_db_up` | gauge | | 1, ko je zadnja osvežitev dosegla PostgreSQL. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Unix čas zadnje uspešne osvežitve. |
| `mailarchiver_emails` | gauge | | Število arhiviranih e-poštnih sporočil. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Število e-poštnih računov. |
| `mailarchiver_raw_messages` | gauge | | Število shranjenih originalnih sporočil. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1, ko je `RawMessages:Enabled` nastavljeno. |
| `mailarchiver_raw_messages_missing` | gauge | | Arhivirana e-poštna sporočila brez shranjenega originala. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1, ko račun še nikoli ni dokončal sinhronizacije. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix čas zadnje uspešne sinhronizacije. Manjka pri računih, ki niso nikoli sinhronizirani. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Število sekund od zadnje uspešne sinhronizacije. Manjka pri računih, ki niso nikoli sinhronizirani. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Originalna sporočila, ki jih ni bilo mogoče shraniti. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Zalışki sinhronizacije, ki so končali s ponaganjo, omejitvijo hitrosti ali časovno izteklo. |

Opombe:

- Števitelji (counters) so lokalni za proces in se po ponovnem zagonu začnejo od 0. `rate()` in `increase()`
  v Prometheusom to obdelavata pravilno.
- `mailarchiver_raw_messages_missing` šteje vsako arhivirano e-poštno sporočilo brez surovega sporočila,
  vključno s sporočili, ki so bila arhivirana preden, ko je bila funkcija `RawMessages:Enabled` vklopljena. Pri namestitvi,
  ki od prvega dne shranjuje originale, predstavlja relevantno razliko.
- `mailarchiver_account_sync_age_seconds` ni pravi zaostanek (backlog). Pove, kdaj je zadnja sinhronizacija
  končala, ne pa koliko sporočil čaka na strežniku.
- `mailarchiver_account_never_synced` obstaja zato, ker račun, ki nikoli ni bil sinhroniziran, nima
  serije `sync_age`. Alarm zgolj na `sync_age` bi ga spregledal.
- Končališče vsebuje imena in ID-je računov, vendar ne vsebino e-pošte, zadeve ali
  podatke za prijavo.

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

Datoteka s pooblastmi vsebuje surov ključ brez predpone `Bearer`. Omejite njene
dovoljenja na uporabnika Prometheus.

Primerji opozoril:

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

Slika in vključeni `docker-compose.yml` preverjata `/api/v1/health/live` z
upravniškimi ključem. Ključ ne more obstajati pred prvim zagonom, zato se prvi zagon izvede brez njega in
kontejner poroča `unhealthy`, dokler ključ ni nastavljen.

1. Enkrat zagonite stek `MAILARCHIVER_HEALTH_API_KEY` prazno.
2. Prijavite se kot administrator in pod
   **API Keys** ustvarite ključ z imenom `monitoring-health`.
3. Vstavite ključ v `.env`:

   ```dotenv
   MAILARCHIVER_HEALTH_API_KEY=<key>
   ```

4. Ponovno ustvarite kontejner z `docker compose up -d`.
5. Preverite stanje:

   ```bash
   docker inspect --format '{{.State.Health.Status}}' <container>
   ```

Potreben je `Api:Enabled=true`. Če je API onemogočen, preverjanje zdravja prejme 404 in
kontejner ostane nezdrav (unhealthy).

Če ne želite upravniškega ključa v okolju kontejnerja, uporabite TCP preverjanje v
kontejnerju in s ključem pokličite `/api/v1/health/ready` z zunanjega monitornega gostitelja.
TCP preverjanje ne more ugotoviti, ali je PostgreSQL dosegljiv.

## Varnost {#security}

- Ključ je administratorski ključ. Preko API lahko bere celoten arhiv. Ravnajte z njim kot s
  geslom administratorja.
- Za spremljanje uporabite namenski uporabniko administratorja. Onemogočitev te uporabnika
  onemogoči preverjanje stanja (health check) in metrike.
- Ključ shranite v datoteki ali v Docker secretu, nikoli pa v parametru poizvedbe (query parameter).
  Parametri poizvedbe se končno zapišejo v protokolske in dostopne dnevnike.
- Omejite dostop do gostitelja za spremljanje ali Prometheus omrežja. API ključ je
  avtentikacija, ne nadomestek za omrežne meje.
- Končne točke (endpoints) za preverjanje stanja vračajo le nizove s statusom. Podrobnosti
  izjem ostanejo v dnevniku aplikacije.

## Reševanje težav {#troubleshooting}

| Simptom | Vzrok | Rešitev |
|---|---|---|
| 404 na vseh štirih poti | `Api:Enabled=false` | Nastavite `Api:Enabled=true` in ponovno zaženite. |
| 404 samo na `/metrics` | `Metrics:Enabled=false` | Nastavite `Metrics:Enabled=true` in ponovno zaženite. |
| 401 | Ključ manjkajoč, napačen, preklican ali zastareli | Ustvarite nov ključ in preverite razpon `Authorization: Bearer`. |
| 403 | Ključ pripada uporabniku brez privilegijev administratorja | Uporabite administratorski ključ. |
| `mailarchiver_metrics_ready 0` več kot eno minuto | Storitev osveževanja ni dokončala svojega prvega teka ali ne more doseči baze podatkov | Preverite dnevnik aplikacije za `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | Zadnje osveževanje je bilo neuspešno | Preverite PostgreSQL in povezovalni niz. Stare številke ostanejo do naslednjega uspešnega osveževanja. |
| Kontejner ostane nezdrav (unhealthy) | Ključ je prazen ali `Api:Enabled=false` | Nastavite oba in ponovno ustvarite kontejner. |
| `health/ready` vrne 503 | PostgreSQL ni dosegljiv ali manjka shema | Preverite bazo podatkov in ali so se izvedli migracije. |

## Česa metrike ne zajemajo {#what-the-metrics-do-not-cover}

- Dostopnost IMAP ali Graph za posamezen račun. Starost sinhronizacije (sync age) le kaže, da je zadnje izvajanje staro.
- Število sporočil, ki čakajo na strežniku, torej dejanski zaostanek (backlog).
- Preverjanje kontrolnih sum (checksum) vsakega shranjenega izvirnika. Za to uporabite obstoječo preverjanje raw sporočil in čiščenje sirotčanskih (orphan) elementov.
- Število sporočil po mapah ali velikost poštnega predala.

Za te podatke uporabite stran računa, stran ozadinskih opravil in dnevnik aplikacije.
