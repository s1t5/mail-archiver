# Egészségállapot és metrikák {#health-and-metrics}

A Mail-Archiver egészségállapot-végpontokat (health endpoints) és egy Prometheus-kompatibilis metrikák-végpontot tesz elérhetővé a külső monitorozás céljából. Mindkettő a REST API része: a `/api/v1` alatt érhetőek el, API kulcsot igényelnek, valamint azt a feltételt, hogy a kulcs egy adminisztrátorhoz tartozzon. Nincs anonim egészségállapot-útvonal és nincs külön monitorozási token.

## Követelmények {#requirements}

| Követelmény | Miért |
|---|---|
| `Api:Enabled=true` | Az endpointek a `/api/` alatt helyezkednek el. Ha az API le van tiltva, az authentication middleware 404-es választ ad. |
| Egy admin API key | Az egész példányra vonatkozó állapotot mutatják ki az egészségügyi és mérési adatok, ezért egy nem admin kulcs 403-as választ kap. |
| `Metrics:Enabled=true` | A metrics endpoint opcionális. Az egészségügyi adatok működnek nélküle is. |

Hozzon létre egy kulcsot az **API Keys** alatt, miközben administratorként van bejelentkezve. Használjon külön kulcsot és, ha lehetséges, külön admin felhasználót a monitorozáshoz, hogy a kulcs cseréje vagy a felhasználó letiltása ne befolyásolja egy személy hozzáférését.

## Endpontok {#endpoints}

| Metódus | Útvonal | Cél | Siker | Hibák |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | A folyamat életben van | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | A PostgreSQL elérhető és a schema jelen van | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Aggregált állapot, beleértve a szinkron frissességét | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus szöveges formátum | 200 | 404, ha letiltva van, 401, 403 |

Mind a négy ugyanazt a hitelesítést használja, mint a API többi része:

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

A Liveness nem érinti a PostgreSQL-t. Csak azt igazolja, hogy a folyamat elfogad kéréseket, így az adatbázis kiesése nem teszi lehetővé, hogy a Docker vagy a Kubernetes újraindítsa az egészséges folyamatot.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

A ellenőrzés megnyitja a saját PostgreSQL kapcsolatát, megkéri az archive schemát, majd két másodperc után feladja a kérést. `503` értéket ad vissza, ha az adatbázis nem elérhető, vagy ha hiányzik a `mail_archiver` schema. Használja ezt az endpointet ahhoz, hogy eldönölje, egy példány pedig fogadjon-e forgalmat.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Ez futtatja az adatbázis-ellenőrzést és a szinkron frissesség-ellenőrzést. A elavult szinkron miatt a jelentés `Degraded` lesz, ami továbbra is HTTP 200, mivel a folyamat továbbra is képes kéréseket kiszolgálni. Csak a `Unhealthy` ad vissza `503` értéket.

A szinkron ellenőrzés alapértelmezés szerint letiltva van. Állítsa a `Health:SyncStaleMinutes` értéket arra a percre, amely után egy engedélyezett fiók elavultnak számít:

```yaml
Health:
  SyncStaleMinutes: 90
```

Egy fiók, amely soha nem szinkronizált, a `LastSync` alapértelmezett értékkel rendelkezik, így amint az ellenőrzés engedélyezésre kerül, elavultnak számít.

## Metrics {#metrics}

A végpont a Prometheus szöveges kiállítási formátumot (0.0.4-es verzió) adja vissza:

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` értéke `true` kell, hogy legyen. A `Metrics:RefreshMinutes` határozza meg, milyen gyakran készül el újra a snapshot (alapértelmezett 5, az 1 alatti értékeket 1-re emeli). Maga a scrape csak olvassa a snapshotot, így a Prometheus lekérdezés nem válthat át teljes tábla-szkenningre.

| Metric | Type | Labels | Meaning |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1, ha a metrics endpoint válaszol. |
| `mailarchiver_metrics_ready` | gauge | | 1 az első sikeres snapshot után. |
| `mailarchiver_db_up` | gauge | | 1, amikor az utolsó frissítés elérte a PostgreSQL-t. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Az utolsó sikeres frissítés Unix időpontja. |
| `mailarchiver_emails` | gauge | | Az archivált e-mailek száma. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | A posta-fiókok száma. |
| `mailarchiver_raw_messages` | gauge | | A tárolt eredeti üzenetek száma. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1, ha a `RawMessages:Enabled` be van állítva. |
| `mailarchiver_raw_messages_missing` | gauge | | Archivált e-mailek tárolt eredeti üzenet nélkül. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1, ha a fiók még soha nem végzett sikeres szinkronizálást. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Az utolsó sikeres szinkronizálás Unix időpontja. Hiányzik azoknál a fiókoknál, amelyek sosem szinkronizáltak. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Másodpercek az utolsó sikeres szinkronizálás óta. Hiányzik azoknál a fiókoknál, amelyek sosem szinkronizáltak. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Eredeti üzenetek, amelyek nem tárolhatók voltak. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Szinkronizálási futások, amelyek hibával, sebességkorlátozással vagy időtúllépéssel kếttek véget. |

Megjegyzések:

- A counterek folyamat-helyi jellegűek, és egy újraindítás után 0-ról indulnak. A Prometheus `rate()` és `increase()` értékek ezt megfelelően kezelik.
- A `mailarchiver_raw_messages_missing` minden olyan archivált e-mailt számlál, amelyhez nincs tárolt eredeti üzenet, beleértve azokat az e-maileket is, amelyeket a `RawMessages:Enabled` bekapcsolása előtt archiváltak. Egy olyan telepítésben, amely az első naptól kezdve tárolt eredeti üzeneteket, ez jelzi a releváns különbséget.
- A `mailarchiver_account_sync_age_seconds` nem valódi hátralék (backlog). Azt jelzi, hogy mikor fejeződött be az utolsó szinkronizálás, nem azt, hogy hány üzenet vár a szerveren.
- A `mailarchiver_account_never_synced` azért létezik, mert egy sosem szinkronizált fiók nem rendelkezik `sync_age` sorozattal. Egy kizárólag a `sync_age` alapján készült riasztás lemaradna róla.
- A végpont tartalmazza a fiókneveket és az azonosítókat, de nem tartalmaz e-mail tartalmat, tárgyakat vagy hitelesítő adatokat.

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

A hitelesítési fájl a nyers kulcsot tartalmazza, `Bearer` prefix nélkül. A jogosultságokat korlátozzák a Prometheus felhasználóra.

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

A kép és a benne lévő `docker-compose.yml` ellenőrzi a `/api/v1/health/live`-et egy admin kulccsal. A kulcs nem létezhet az első indítás előtt, ezért az első indítás nélküle fut, és a konténer `unhealthy`-t jelent, amíg a kulcsot be nem állítják.

1. Indítsa el a stacket egyszer úgy, hogy a `MAILARCHIVER_HEALTH_API_KEY` üres.
2. Jelentkezzen be adminisztrátorként, és hozzon létre egy `monitoring-health` nevű kulcsot az **API Keys** alatt.
3. Helyezze a kulcsot a `.env`-be:

   ```dotenv
   MAILARCHIVER_HEALTH_API_KEY=<key>
   ```

4. Hozza létre újra a konténert a `docker compose up -d` segítségével.
5. Ellenőrizze az állapotot:

   ```bash
   docker inspect --format '{{.State.Health.Status}}' <container>
   ```

A `Api:Enabled=true` szükséges. Ha az API letiltva van, a health check 404-et kap, és a konténer unhealthy állapotban marad.

Ha nem szeretne admin kulcsot a konténer környezetében, használjon TCP ellenőrzést a konténerben, és hívja meg a `/api/v1/health/ready`-at egy külső monitorozó hostról a kulccsal.
A TCP ellenőrzés nem tudja megmondani, hogy a PostgreSQL elérhető-e.

## Biztonság {#security}

- A kulcs admin kulcs. Az API révén az egész archívumot be tudja olvasni. Kezelje úgy, mint egy
  adminisztrátori jelszót.
- Használjon külön admin felhasználót a monitorozáshoz. Ennek a felhasználónak a letiltása
  letiltolja az állapotellenőrzést (health check) és a metrikákat is.
- Tartsa a kulcsot egy fájlban vagy Docker secret-ben, soha ne használja query paraméterként. A query
  paraméterek a proxy és az hozzáférési naplókban (access logs) végződnek.
- Korlátozza a hozzáférést a monitoring hosthoz vagy a Prometheus hálózathoz. Az API kulcs a
  hitelesítés, nem hálózatbeli határok helyettesítője.
- Az állapotellenőrző végpontok (health endpoints) csak állapot-szövegeket (status strings) térítenek vissza.
  A kivétel részletei az alkalmazás naplójában (application log) maradnak.

## Hibaelégítés {#troubleshooting}

| Tünet | Ok | Megoldás |
|---|---|---|
| 404 mind a négy útvonalon | `Api:Enabled=false` | Állítsa be a `Api:Enabled=true` értéket, majd indítsa újra. |
| 404 csak a `/metrics` útvonalon | `Metrics:Enabled=false` | Állítsa be a `Metrics:Enabled=true` értéket, majd indítsa újra. |
| 401 | Hiányzó, rossz, visszavonott vagy lejárt kulcs | Hozzon létre új kulcsot, és ellenőrizze a `Authorization: Bearer` header értéket. |
| 403 | A kulcs nem admin felhasználóhoz tartozik | Használjon admin kulcsot. |
| `mailarchiver_metrics_ready 0` több mint egy percig | A frissítő szolgáltatás még nem fejezte be az első futást, vagy nem éri el az adatbázist | Ellenőrizze az alkalmazás logjait a `Could not refresh the monitoring metrics snapshot` után. |
| `mailarchiver_db_up 0` | Az utolsó frissítés elbukott | Ellenőrizze a PostgreSQL-t és a connection stringet. A régi számok addig maradnak meg, amíg meg nem történik a következő sikeres frissítés. |
| A konténer unhealthy állapotban marad | Üres kulcs vagy `Api:Enabled=false` | Állítsa be mindkettőt, majd hozzon létre újra konténert. |
| A `health/ready` 503-as kódot ad vissza | A PostgreSQL nem elérhető vagy hiányzik a schema | Ellenőrizze az adatbázist, és azt is, hogy lefutottak-e a migrációk. |

## Amit a metrikák nem fednek le {#what-the-metrics-do-not-cover}

- IMAP vagy Graph elérhetőség fiókonként. A szinkronizálási kor (sync age) csak azt mutatja, hogy az utolsó futás régebbi.
- A szerveren várakozó üzenetek száma, a valódi háttérfolyam (backlog).
- Minden tárolt eredeti üzenet ellenőrzési összegének (checksum) ellenőrzése. Használja ehhez a meglévő nyers üzenet ellenőrzést (raw message verification) és az árva tisztítást (orphan cleanup).
- Mappánkénti számok vagy postafiók méretei.

Ezekhez használja a fiók oldalt, a háttérfolyamok (background jobs) oldalt és az alkalmazás naplóját (application log).
