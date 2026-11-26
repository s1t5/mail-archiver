# Zdravje in metrike {#health-and-metrics}

Mail-Archiver razpoloaga zdravstvene končnice (health endpoints) in metrično končnico, zd compatible s sistemom Prometheus, za zunanje spremljanje. Obe sta del REST API: nahajata se pod `/api/v1`, zahtevata API ključ in zahtevajo, da ta ključ pripada administratorju. Brez anonimne poti za zdravje in brez ločenega nadzornega žetonja (monitoring token) ni mogoče.

## Zahteve {#requirements}

| Zahteva | Razlog |
|---|---|
| `Api:Enabled=true` | Končališča se nahajajo pod `/api/`. Če je API onemogočen, autentifikacijska posredniška programska oprema vrne 404. |
| Ključ API-ja administratorja | Health in metrics raz Exposirata stanje na ravni celotne instance, zato ključ brez administratorjevih pravic vrne 403. |
| `Metrics:Enabled=true` | Končališče metrics je izbirno (opt-in). Health deluje brez njega. |

Ustvarite ključ pod **API Keys**, ko ste prijavljeni kot administrator. Za spremljanje uporabite namenski ključ in, če je to mogoče, namenskega uporabnika administratorja, da rotacija ključa ali onemogočanje uporabnika ne bo vplivalo na dostop posamezne osebe.

## Endpoints {#endpoints}

| Metoda | Pot | Namen | Uspeh | Napake |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Proces je živ | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL je dosegljiv in shema je prisotna | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Povprečen status, vključno s svežino sinhronizacije | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus tekstovna oblika | 200 | 404 ko je onemogočeno, 401, 403 |

Vse štiri uporabljajo isto avtentikacijo kot preostali del API-ja:

- manjkajoč ali neveljavni ključ: `401` z `WWW-Authenticate: Bearer`
- veljavni ključ brez vloge admin: `403` z `application/problem+json`
- API je onemogočen: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Liveness ne uporablja PostgreSQL. Dokazuje le, da proces sprejema zahteve, tako da izpad baze podatkov ne povzroči, da bi Docker ali Kubernetes ponovno začela proces, ki je funkcionalen.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

Preverjanje odpre lastno povezavo s PostgreSQL, poizvedba za shemo arhivov in po dveh sekundah odstopi. Vrne `503`, ko baza podatkov ni dosegljiva ali ko manjka shema `mail_archiver`. Uporabite to kontejner (endpoint), da se odločite, ali bi morala poslovanjska enota (instance) prejemati promet.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

To izvaja preverjanje baze podatkov in preverjanje svežine sinhronizacije. Zastarela sinhronizacija naredi poročilo `Degraded`, ki ostane HTTP 200, ker proces še vedno lahko obravnava zahteve. Le `Unhealthy` vrne `503`.

Preverjanje sinhronizacije je privzeto onemogočeno. Nastavite `Health:SyncStaleMinutes` na število
 minut, po katerih se omogočena številka računa šteje za zastarelo:

```yaml
Health:
  SyncStaleMinutes: 90
```

Račun, ki nikoli ni sinhroniziral, ima `LastSync` na privzeli vrednosti, zato se šteje za zastarelo takoj ko je preverjanje omogočeno.

## Metrike {#metrics}

Končališče vrne format tekstualne izložbe Prometheus (verzija 0.0.4):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` mora biti `true`. `Metrics:RefreshMinutes` določa, tako pogosto kot se posodoblj aproksimacije (privzeto 5, vrednosti pod 1 se dvignejo na 1). Sam scrape samo bere aproksimacijo, zato se Prometheusovo odpiranje ne lahko spremeni v preiskavo tabele.

| Metrika | Tip | Oznake | Pomen |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1, ko končališče metrik odgovarja. |
| `mailarchiver_metrics_ready` | gauge | | 1 po prvi uspešni aproksimaciji. |
| `mailarchiver_db_up` | gauge | | 1, ko je zadnje osveževanje doseglo PostgreSQL. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Unix čas zadnjega uspešnega osveževanja. |
| `mailarchiver_emails` | gauge | | Število arhiviranih e-poštnih sporočil. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Število e-poštnih računov. |
| `mailarchiver_raw_messages` | gauge | | Število shranjenih originalnih sporočil. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1, ko je `RawMessages:Enabled` nastavljeno. |
| `mailarchiver_raw_messages_missing` | gauge | | Arhivirana e-poštna sporočila brez shranjenega originala. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1, ko račun še nikoli ni dokončal sinhronizacije. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix čas zadnje uspešne sinhronizacije. Ni prisotno za račune, ki nikoli niso sinhronizirali. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Število sekund od zadnje uspešne sinhronizacije. Ni prisotno za račune, ki nikoli niso sinhronizirali. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Originalna sporočila, ki jih ni bilo mogoče shraniti. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Izvedbe sinhronizacije, ki so končale z neuspehom, omejitvijo hitrosti ali časovno iztekom. |

Opombe:

- Odštevalniki so lokalni za proces in se po ponovnem zagonu začnejo od 0. `rate()` in `increase()` v Prometheusom to obdelavata pravilno.
- `mailarchiver_raw_messages_missing` šteje vsako arhivirano e-poštno sporočilo brez surovnega sporočila, vključno z e-poštnimi sporočili, arhiviranimi pred tem, ko je bila funkcija `RawMessages:Enabled` vključena. V namestitvi, ki od prvega dneما shranjuje originale, predstavlja relevantno vrzel.
- `mailarchiver_account_sync_age_seconds` ni pravi backlog. Pove, kdaj je bila zadnja sinhronizacija končana, ne pa koliko sporočil čaka na strežniku.
- `mailarchiver_account_never_synced` obstaja zato, ker račun, ki nikoli ni sinhroniziran, nima serije `sync_age`. Alarm z vesonly `sync_age` bi ga zamislil.
- Končališče vsebuje imena in identifikatorje računov, vendar ne vsebino e-pošte, z Име (subjects) ali poverljive podatke.

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

Datoteka s povjemilji vsebuje surov ključ brez predpone `Bearer`. Omejite njene
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

Slika vsebuje preverjanje življenja (liveness check), ki kliče koreninsko pot (root path) brez avtentikacije. Koreninska pot preusmerja na stran za prijavo še preden se zažene kateri koli krmar (controller), zato preverjanje ne potrebuje API ključa, deluje, tudi če je REST API onemogočen, in ne posega v PostgreSQL. Nove namestitve in posodobitve poročajo `healthy` brez kakršne koli konfiguracije.

```bash
docker inspect --format '{{.State.Health.Status}}' <container>
```

Preverjanje dokazuje, da proces sprejema HTTP zahteve. Ne dokazuje, da je PostgreSQL dosegljiv. Za to pokličite `/api/v1/health/ready` z zunanjega monitornega gostitelja z admin API ključem in `Api:Enabled=true`; končališče (endpoint) odgovori s kodo 503, ko je baza podatkov nedosegljiva.

Vključena `docker-compose.yml` ne nadomešča preverjanja. Če potrebujete drugačen interval, čas izteka (timeout) ali ukaz, dodajte blok `healthcheck` v storitev, ali pa nastavite `healthcheck: disable: true`, da izklopite preverjanje.

Prejšnja spremenljivka `MAILARCHIVER_HEALTH_API_KEY` se ne uporablja več. Odpravite jo iz obstoječih datotek compose in `.env`; pustiti jo na mestu je neškodljivo.

## Varnost {#security}

- Ključ je administratorski ključ. Preko API lahko bere celoten arhiv. Ravnajte z njim kot z
  geslom administratorja.
- Za spremljanje uporabite namenskega uporabnika administratorja. Če onemogočite tega uporabnika, se onemogočijo zunanji
  končnice (endpoints) za zdravje in metrike, ne pa lastna preverjanje življenja (liveness check) posameznega kontejnerja.
- Ključ shranite v datoteki ali v Docker secretu, nikoli pa v parametru poizvedbe (query parameter). Parametri poizvedbe
  se končajo v proxy in dostopnih dnevnikih (access logs).
- Omejite dostop na gostiteljski stroj za spremljanje ali na Prometheus omrežje. API ključ je
  avtentikacija, ne nadomestek za omrežjne meje.
- Končnice za zdravje vračajo le nizovske vrednosti stanja. Podrobnosti o izjemkah ostanejo v
  dnevniku aplikacije.

## Reševanje težav {#troubleshooting}

| Simptom | Vzrok | Rešitev |
|---|---|---|
| 404 na vseh štirih potih | `Api:Enabled=false` | Nastavite `Api:Enabled=true` in ponovno zaženite. |
| 404 samo na `/metrics` | `Metrics:Enabled=false` | Nastavite `Metrics:Enabled=true` in ponovno zaženite. |
| 401 | Ključestest ali napačen, odvzet ali iztečen | Ustvarite nov ključ in preverite glavo `Authorization: Bearer`. |
| 403 | Ključ pripada uporabniku brez administratorskih pravic | Uporabite ključ administratorja. |
| `mailarchiver_metrics_ready 0` več kot eno minuto | Posveževalna storitev ni dokončala svojega prvega pogona ali ne more doseči baze podatkov | V logih aplikacije preverite `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | Zadnje osveževanje je bilo neuspešno | Preverite PostgreSQL in povezovalni niz. Stara številčna vrednost ostaja do naslednjega uspešnega osveževanja. |
| Posodebnik (Container) ostaja nezdrav (unhealthy) | Ključ je prazen ali `Api:Enabled=false` | Nastavite oba in ponovno ustvarite posodebnik. |
| `health/ready` vrne 503 | PostgreSQL ni dosegljiv ali manjka shema | Preverite bazo podatkov in ali so se izvedle migracije. |

## Česa metrike ne zajemajo {#what-the-metrics-do-not-cover}

- Dosegljjivost IMAP ali Graph za posamezen račun. Starost sinhronizacije (sync age) le kaže, da je zadnje izvajanje staro.
- Število sporočil, ki čakajo na strežniku, torej dejanski zaostanek (backlog).
- Preverjanje kontrolnih sum (checksum) vsakega shranjenega originala. Za to uporabite obstoječe preverjanje surovih sporočil in čiščenje orphan sporočil.
- Številčilo po mapah ali velikost poštanske predale.

Za te informacije uporabite stran računa, stran s poslovnimi opravili v ozadju (background jobs) in dnevnik aplikacije.
