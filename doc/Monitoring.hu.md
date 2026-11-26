# Egészségállapot és metrikák {#health-and-metrics}

A Mail-Archiver egészségállapot-végpontokat (health endpoints) és egy Prometheus-kompatibilis metrikavégpontot tesz elérhetővé külső monitorozás céljából. Mindkettő a REST API része: a `/api/v1` alatt érhetőek el, API kulcsot igényelnek, és a kulcsnak adminisztrátornak kell byćnie. Nincs anonim egészségállapot-útvonal és nincs külön monitorozási token.

## Requirements {#requirements}

| Követelmény | Miért |
|---|---|
| `Api:Enabled=true` | A végpontok a `/api/` alatt vannak. Az API letiltásával az authentication middleware 404-et ad vissza. |
| Egy admin API key | A health és a metrics az egész példányra vonatkozó állapotot mutatja meg, ezért a nem admin kulcs 403-at kap. |
| `Metrics:Enabled=true` | A metrics végpont opcionális (opt-in). A health a nélküle is működik. |

Hozzon létre egy kulcsot a **API Keys** alatt, miközben adminisztrátorként van bejelentkezve. Használjon külön kulcsot és, amennyire lehetséges, egy külön admin felhasználót a monitorozáshoz, hogy a kulcs megcserélése vagy a felhasználó letiltása ne befolyásolja egy személy hozzáférését.

## Endpontok {#endpoints}

| Metódus | Útvonal | Cél | Sikeres | Hibák |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | A folyamat életben van | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | A PostgreSQL elérhető és a schema jelen van | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Összesített állapot, beleértve a szinkron frissességét | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus szöveges formátum | 200 | 404, ha letiltva van, 401, 403 |

Mind a négy ugyanazt a hitelesítésmét használja, mint a többi API:

- hiányzó vagy érvénytelen kulcs: `401` `WWW-Authenticate: Bearer` értékkel
- érvényes kulcs admin szerepkör nélkül: `403` `application/problem+json` értékkel
- API letiltva: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

A Liveness nem érinti a PostgreSQL-t. Csak azt igazolja, hogy a folyamat elfogad kéréseket, így egy adatbázis-leállás nem okozza azt, hogy a Docker vagy a Kubernetes újraindítsa az egészséges folyamatot.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

A ellenőrzés megnyitja a saját PostgreSQL kapcsolatát, megkérdezi az archive schemat, és két másodperc után feladja a kérést. `503` értéket ad vissza, ha az adatbázis nem elérhető, vagy hiányzik a `mail_archiver` schema. Használja ezt az endpontot ahhoz, hogy döntse el, az adott példány fogadjon-e forgalmat.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Ez futtatja az adatbázis-ellenőrzést és a szinkron frissességének ellenőrzését is. A elavult (stale) szinkron miatt a jelentés `Degraded` lesz, ami megőrzi a HTTP 200 állapotot, mivel a folyamat továbbra is képes kéréseket kiszolgálni. Csak a `Unhealthy` ad vissza `503` értéket.

A szinkron ellenőrzés alapértelmezés szerint letiltva van. Állítsa a `Health:SyncStaleMinutes` értéket annak a percek számára, amely után egy engedélyezett fiók elavultnak számít:

```yaml
Health:
  SyncStaleMinutes: 90
```

Egy fiók, amely soha nem szinkronizált, a `LastSync` alapértelmezett értékkel rendelkezik, így amint az ellenőrzés engedélyezésre kerül, elavultnak fog számítani.

## Metrics {#metrics}

A végpont a Prometheus szöveges kiadás (exposition) formátumát adja vissza (0.0.4-es verzió):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` értékel `true`. A `Metrics:RefreshMinutes` szabályozza, milyen gyakran készül el a snapshot (alapértelmezetten 5, az 1 alatti értékek 1-re emelődnek). Mag本身 a scrape csak az snapshotot olvassa ki, így a Prometheus lekérdezés nem válhat táblavizsgálatossá (table scan).

| Metrika | Típus | Címkék | Jelentés |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1, amikor a metrikas végpont válaszol. |
| `mailarchiver_metrics_ready` | gauge | | 1 az első sikeres snapshot után. |
| `mailarchiver_db_up` | gauge | | 1, amikor az utolsó frissítés elérte a PostgreSQL-t. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Az utolsó sikeres frissítés Unix időbélyeg. |
| `mailarchiver_emails` | gauge | | Archívumba került e-mailek száma. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Mail fiókok száma. |
| `mailarchiver_raw_messages` | gauge | | Tárolt eredeti üzenetek száma. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1, ha a `RawMessages:Enabled` be van állítva. |
| `mailarchiver_raw_messages_missing` | gauge | | Archív e-mailek tárolt eredeti üzenet nélkül. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1, ha a fiók még soha nem végezt sikeres szinkronizálást. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Az utolsó sikeres szinkronizálás Unix időbélyeg. Hiányzik azoknál a fiókoknál, amelyek sosem szinkronizáltak. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Utolsó sikeres szinkronizálás óta eltelt másodpercek. Hiányzik azoknál a fiókoknál, amelyek sosem szinkronizáltak. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Eredeti üzenetek, amelyek nem tárolhatóak voltak. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Szinkronizációs futások, amelyek hibával, sebességkorlátozással vagy timeouttal értek véget. |

Megjegyzések:

- A számlálók (counters) folyamat-lokálisak, és egy újraindítás után 0-ról indulnak. A Prometheus `rate()` és `increase()` értékek ezt megfelelően kezelik.
- A `mailarchiver_raw_messages_missing` minden olyan archív e-mailt megszámol, amelyhez nincs nyers üzenet, beleértve azokat az e-maileket is, amelyek a `RawMessages:Enabled` bekapcsolása előtt kerültek az archívumba. Olyan telepítés esetén, amely az első naptól kezdve tárolt eredeti üzeneteket, ez a releváns különbség.
- A `mailarchiver_account_sync_age_seconds` nem valódi hátralék (backlog). Azt jelzi, mikor fejeződött be az utolsó szinkronizálás, nem pedig azt, hogy hány üzenet vár a szerveren.
- A `mailarchiver_account_never_synced` azért létezik, mert egy sosem szinkronizált fióknak nincs `sync_age` sorozata. Egy riasztás kizárólag a `sync_age` alapján ezt kihagyná.
- A végpont tartalmaz fiókneveket és azonosítókat, de nem tartalmaz e-mail tartalmakat, tárgyakat vagy hitelesítő adatokat.

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

A hitelesítő adatokat tartalmazó fájl a nyers kulcsot tartalmazza, a `Bearer` prefix nélkül. A fájl jogosultságait korlátozza a Prometheus felhasználó számára.

Példa riasztások:

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

A kép egy liveness ellenőrzést tartalmaz, amely hitelesítés nélkül hívja a gyökér utat (root path). A gyökér út a kontrollerek futása előtt átirányítja a bejelentkezési oldalra, így az ellenőrzéshez nincs szükség API kulcsra, működik a REST API kikapcsolva mellett, és nem érinti a PostgreSQL-t. Az új telepítések és frissítések `healthy` állapotot jelentenek konfiguráció nélkül.

```bash
docker inspect --format '{{.State.Health.Status}}' <container>
```

Az ellenőrzés igazolja, hogy a folyamat elfogad HTTP kéréseket. Ez azonban nem igazolja, hogy a PostgreSQL elérhető. Ehhez hívja a `/api/v1/health/ready`-t egy külső monitorozó hostról admin API kulccsal és `Api:Enabled=true`; a végpont 503-as választ ad, ha az adatbázis le van állítva.

A csomagolt `docker-compose.yml` nem felülírja az ellenőrzést. Adj hozzá egy `healthcheck` blokkot a szolgáltatáshoz, ha más intervallumra, timeoutra vagy parancsra van szükséged, vagy állítsd be a `healthcheck: disable: true`-ot a ellenőrzés kikapcsolásához.

A korábbi `MAILARCHIVER_HEALTH_API_KEY` változó már nem használatos. Távolítsd el a meglévő compose fájlokból és `.env` fájlokból; a helyén hagyva semmi kárt nem okoz.

## Biztonság {#security}

- A kulcs egy admin kulcs. Az API keresztül az egész archivát olvashatja. Kezelje úgy, mint egy
  adminisztrátori jelszót.
- Használjon külön admin felhasználót a monitorozáshoz. Az adott felhasználó letiltása a
  külső health és metrics végpontokat tilts le, nem pedig a konténer saját liveness ellenőrzését.
- A kulcsot tárolja fájlban vagy Docker secret formájában, soha ne használja query paraméterként. A query paraméterek
  a proxy és az hozzáférési logokba kerülnek.
- Korlátozza a hozzáférést a monitoring hosthoz vagy a Prometheus hálózathoz. Az API kulcs a
  hitelesítés, nem pedig a hálózati határok helyettesítője.
- A health végpontok csak státusz szinteket adnak vissza. A hibarészletek az
  alkalmazás logjában maradnak.

## Hibaelégítés {#troubleshooting}

| Tünet | Ok | Megoldás |
|---|---|---|
| 404 mind a négy útvonalon | `Api:Enabled=false` | Állítsa be a `Api:Enabled=true` értéket, majd indítsa újra. |
| Csak a `/metrics` útvonalon 404 | `Metrics:Enabled=false` | Állítsa be a `Metrics:Enabled=true` értéket, majd indítsa újra. |
| 401 | Hiányzó, hibás, visszavonott vagy lejárt kulcs | Hozzon létre egy új kulcsot, és ellenőrizze a `Authorization: Bearer` header értéket. |
| 403 | A kulcs nem admin felhasználóhoz tartozik | Használjon admin kulcsot. |
| `mailarchiver_metrics_ready 0` több mint egy percnél | A frissítési szolgáltatás még nem fejezte be az első futást, vagy nem éri el az adatbázist | Ellenőrizze az alkalmazás logjait a `Could not refresh the monitoring metrics snapshot` után. |
| `mailarchiver_db_up 0` | Az utolsó frissítés sikertelen volt | Ellenőrizze a PostgreSQL-t és a connection stringet. A régi számok addig maradnak, amíg nincs következő sikeres futás. |
| A konténer unhealthy állapotban marad | Üres kulcs vagy `Api:Enabled=false` | Állítsa be mindkettőt, majd hozzon létre újra a konténert. |
| A `health/ready` 503-as kóddal tér vissza | A PostgreSQL nem elérhető vagy hiányzik a schema | Ellenőrizze az adatbázist, és azt is, hogy futtak-e migrációk. |

## Mit nem fednek le a metrikák {#what-the-metrics-do-not-cover}

- IMAP vagy Graph elérhetőség fiókonként. A szinkronizálási kor (sync age) csak azt mutatja, hogy az utolsó futás régebbi.
- A szerveren várakozó üzenetek száma, a valódi hátralék (backlog).
- Minden tárolt eredeti üzenet ellenőrző összegének (checksum) ellenőrzése. Ehhez használja a meglévő nyers üzenetellenőrzést (raw message verification) és az oszlop nélküli tisztítást (orphan cleanup).
- Mappánkénti számok vagy postaíró méretek.

Ezekhez használja a fiókoldalt (account page), a háttérfolyamat oldalát (background jobs page) és az alkalmazásnaplót (application log).
