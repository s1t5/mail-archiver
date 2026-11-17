# Salute e metriche {#health-and-metrics}

Mail-Archiver espone endpoint di salute e un endpoint di metriche compatibile con Prometheus per il monitoraggio esterno. Entrambi fanno parte della REST API: si trovano sotto `/api/v1`, richiedono una API key e richiedono che tale chiave appartenga a un amministratore. Non esiste un percorso di salute anonimo né un token di monitoraggio separato.

## Requisiti {#requirements}

| Requisito | Perché |
|---|---|
| `Api:Enabled=true` | Gli endpoint si trovano sotto `/api/`. Con l'API disabilitata, il middleware di autenticazione risponde con un errore 404. |
| Una chiave API admin | Health e metrics espongono lo stato dell'intera istanza, quindi una chiave non admin riceve un errore 403. |
| `Metrics:Enabled=true` | L'endpoint metrics è opzionale. Health funziona anche senza di esso. |

Crea la chiave sotto **API Keys** mentre sei autenticato come amministratore. Usa una chiave dedicata e, se possibile, un utente admin dedicato per il monitoraggio, in modo che la rotazione della chiave o la disabilitazione dell'utente non influenzino l'accesso di una persona.

## Endpoints {#endpoints}

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Il processo è attivo | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL raggiungibile e schema presente | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Stato aggregato inclusa la freschezza della sincronizzazione | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Formato testuale Prometheus | 200 | 404 quando disabilitato, 401, 403 |

Tutti e quattro utilizzano la stessa autenticazione del resto dell'API:

- chiave mancante o non valida: `401` con `WWW-Authenticate: Bearer`
- chiave valida senza il ruolo admin: `403` con `application/problem+json`
- API disabilitata: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Il controllo di Liveness non tocca PostgreSQL. Dimostra solo che il processo accetta richieste, quindi un'interruzione del database non fa sì che Docker o Kubernetes riaventino un processo sano.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

Il controllo apre la propria connessione PostgreSQL, richiede lo schema dell'Archivio e rinuncia dopo due secondi. Restituisce `503` quando il database non è raggiungibile o lo schema `mail_archiver` è mancante. Utilizza questo endpoint per decidere se un'istanza debba ricevere traffico.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Questo esegue il controllo del database e il controllo della freschezza della sincronizzazione. Una sincronizzazione obsoleta rende il report `Degraded`, che rimane HTTP 200 perché il processo può ancora servire richieste. Solo `Unhealthy` restituisce `503`.

Il controllo della sincronizzazione è disabilitato di default. Imposta `Health:SyncStaleMinutes` al numero di minuti dopo i quali un Account abilitato è considerato obsoleto:

```yaml
Health:
  SyncStaleMinutes: 90
```

Un Account che non si è mai sincronizzato ha `LastSync` al valore predefinito, quindi viene considerato obsoleto non appena il controllo viene abilitato.

## Metriche {#metrics}

L'endpoint restituisce il formato di esposizione testuale di Prometheus (versione 0.0.4):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` deve essere `true`. `Metrics:RefreshMinutes` controlla la frequenza con cui lo snapshot
viene ricostruito (predefinito 5, i valori inferiori a 1 vengono elevati a 1). Lo scrape stesso legge solo lo
snapshot, quindi un poll di Prometheus non può trasformarsi in una scansione di tabella.

| Metric | Type | Labels | Meaning |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1 quando l'endpoint delle metriche risponde. |
| `mailarchiver_metrics_ready` | gauge | | 1 dopo il primo snapshot riuscito. |
| `mailarchiver_db_up` | gauge | | 1 quando l'ultimo aggiornamento ha raggiunto PostgreSQL. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Unix time dell'ultimo aggiornamento riuscito. |
| `mailarchiver_emails` | gauge | | Numero di email in Archivio. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Numero di Account email. |
| `mailarchiver_raw_messages` | gauge | | Numero di messaggi originali memorizzati. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1 quando `RawMessages:Enabled` è impostato. |
| `mailarchiver_raw_messages_missing` | gauge | | Email in Archivio senza un originale memorizzato. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1 quando l'Account non ha mai completato una Sincronizzazione. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix time dell'ultima Sincronizzazione riuscita. Assente per gli Account che non si sono mai sincronizzati. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Secondi dall'ultima Sincronizzazione riuscita. Assente per gli Account che non si sono mai sincronizzati. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Messaggi originali che non hanno potuto essere memorizzati. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Esecuzioni di Sincronizzazione terminate con errore, rate limiting o timeout. |

Note:

- I counter sono locali al processo e partono da 0 dopo un riavvio. `rate()` e `increase()`
  in Prometheus gestiscono correttamente la cosa.
- `mailarchiver_raw_messages_missing` conta ogni email in Archivio senza un messaggio grezzo,
  incluse le email in Archivio prima che `RawMessages:Enabled` fosse attivato. In un'installazione
  che ha memorizzato gli originali fin dal primo giorno, rappresenta il gap rilevante.
- `mailarchiver_account_sync_age_seconds` non è un vero backlog. Indica quando l'ultima Sincronizzazione
  è terminata, non quanti messaggi sono in attesa sul server.
- `mailarchiver_account_never_synced` esiste perché un Account mai sincronizzato non ha una serie
  `sync_age`. Un alert solo su `sync_age` lo perderebbe.
- L'endpoint contiene nomi e ID degli Account, ma nessun contenuto delle email, oggetti o
  credenziali.

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

Il file delle credenziali contiene la chiave grezza, senza il prefisso `Bearer`. Limitare le sue
Autorizzazione all'utente Prometheus.

Esempi di alert:

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

L'immagine e il `docker-compose.yml` incluso controllano `/api/v1/health/live` con una chiave admin. La chiave non può esistere prima del primo avvio, quindi il primo avvio viene eseguito senza di essa e il container riporta `unhealthy` finché la chiave non viene impostata.

1. Avvia lo stack una volta con `MAILARCHIVER_HEALTH_API_KEY` vuoto.
2. Accedi come amministratore e crea una chiave denominata `monitoring-health` sotto
   **API Keys**.
3. Inserisci la chiave in `.env`:

   ```dotenv
   MAILARCHIVER_HEALTH_API_KEY=<key>
   ```

4. Ricrea il container con `docker compose up -d`.
5. Controlla lo stato:

   ```bash
   docker inspect --format '{{.State.Health.Status}}' <container>
   ```

`Api:Enabled=true` è richiesto. Con l'API disabilitata, l'health check riceve un 404 e il
container rimane in stato unhealthy.

Se non desideri una chiave admin nell'ambiente del container, utilizza un controllo TCP nel
container e chiama `/api/v1/health/ready` da un host di monitoraggio esterno con la chiave.
Il controllo TCP non può determinare se PostgreSQL sia raggiungibile.

## Sicurezza {#security}

- La chiave è una chiave admin. Può leggere l'intero Archivio tramite l'API. Trattala come una
  password di amministratore.
- Utilizza un utente admin dedicato per il monitoraggio. Disabilitare quell'utente disabilita il
  health check e le metriche.
- Mantieni la chiave in un file o in un segreto Docker, mai in un parametro di query. I parametri
  di query finiscono nei log del proxy e di accesso.
- Limita l'accesso all'host di monitoraggio o alla rete Prometheus. La chiave API è l'
  autenticazione, non un sostituto dei confini di rete.
- Gli endpoint di health restituiscono solo stringhe di stato. I dettagli delle eccezioni rimangono nel
  log dell'applicazione.

## Risoluzione dei problemi {#troubleshooting}

| Sintomo | Causa | Soluzione |
|---|---|---|
| 404 su tutti e quattro i percorsi | `Api:Enabled=false` | Impostare `Api:Enabled=true` e riavviare. |
| 404 solo su `/metrics` | `Metrics:Enabled=false` | Impostare `Metrics:Enabled=true` e riavviare. |
| 401 | Chiave mancante, errata, revocata o scaduta | Creare una nuova chiave e controllare l'header `Authorization: Bearer`. |
| 403 | La chiave appartiene a un utente non amministratore | Utilizzare una chiave admin. |
| `mailarchiver_metrics_ready 0` per più di un minuto | Il servizio di aggiornamento non ha completato la prima esecuzione o non riesce a raggiungere il database | Controllare il log dell'applicazione per `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | L'ultimo aggiornamento è fallito | Controllare PostgreSQL e la stringa di connessione. I conteggi precedenti rimangono fino al prossimo aggiornamento riuscito. |
| Il container rimane in stato unhealthy | Chiave vuota o `Api:Enabled=false` | Impostare entrambi e ricreare il container. |
| `health/ready` restituisce 503 | PostgreSQL non raggiungibile o schema mancante | Controllare il database e verificare se le migrazioni sono state eseguite. |

## Cosa non coprono le metriche {#what-the-metrics-do-not-cover}

- Raggiungibilità IMAP o Graph per Account. Un'età di Sincronizzazione indica solo che l'ultima esecuzione è vecchia.
- Il numero di messaggi in attesa sul server, il vero backlog.
- Verifica del checksum di ogni originale memorizzato. Utilizzare la verifica dei messaggi grezzi esistente e la pulizia degli orfani per questo scopo.
- Conteggi per Cartella o dimensioni della Casella di posta.

Per questi dati, utilizzare la pagina dell'Account, la pagina dei job in background e il log dell'applicazione.
