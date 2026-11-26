# Salute e Metriche {#health-and-metrics}

Mail-Archiver espone endpoint di salute e un endpoint di metriche compatibile con Prometheus per il monitoraggio esterno. Entrambi fanno parte delle REST API: risiedono sotto `/api/v1`, richiedono una chiave API e richiedono che tale chiave appartenga a un amministratore. Non esiste un percorso di salute anonimo né un token di monitoraggio separato.

## Requisiti {#requirements}

| Requisito | Perché |
|---|---|
| `Api:Enabled=true` | Gli endpoint si trovano sotto `/api/`. Con l'API disabilitata, il middleware di autenticazione risponde con un errore 404. |
| Una chiave API admin | Health e metrics espongono lo stato dell'intera istanza, quindi una chiave non admin riceve un errore 403. |
| `Metrics:Enabled=true` | L'endpoint metrics è opzionale. Health funziona anche senza di esso. |

Crea la chiave sotto **API Keys** mentre sei autenticato come amministratore. Usa una chiave dedicata e, se possibile, un utente admin dedicato per il monitoraggio, in modo che la rotazione della chiave o la disabilitazione dell'utente non influiscano sull'accesso di una persona.

## Endpoints {#endpoints}

| Metodo | Path | Scopo | Successo | Errori |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Il processo è attivo | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL raggiungibile e schema presente | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Stato aggregato inclusa la freschezza della Sincronizzazione | 200 | 503, 401, 403 |
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

Liveness non tocca PostgreSQL. Dimostra solo che il processo accetta richieste, quindi un
down del database non fa sì che Docker o Kubernetes riavviino un processo sano.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

Il controllo apre la propria connessione PostgreSQL, richiede lo schema dell'Archivio e rinuncia
dopo due secondi. Restituisce `503` quando il database non è raggiungibile o lo
schema `mail_archiver` è mancante. Utilizza questo endpoint per decidere se un'istanza debba
ricevere traffico.

### Salute aggregata {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Questo esegue il controllo del database e il controllo della freschezza della Sincronizzazione. Una Sincronizzazione obsoleta rende il report
`Degraded`, che rimane HTTP 200 perché il processo può ancora servire richieste. Solo
`Unhealthy` restituisce `503`.

Il controllo della Sincronizzazione è disabilitato di default. Imposta `Health:SyncStaleMinutes` al numero di
minuti dopo i quali un Account abilitato è considerato obsoleto:

```yaml
Health:
  SyncStaleMinutes: 90
```

Un Account che non ha mai effettuato la Sincronizzazione ha `LastSync` al valore predefinito, quindi è considerato obsoleto
non appena il controllo viene abilitato.

## Metrics {#metrics}

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
| `mailarchiver_emails` | gauge | | Numero di email archiviate. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Numero di Account di posta. |
| `mailarchiver_raw_messages` | gauge | | Numero di messaggi originali memorizzati. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1 quando `RawMessages:Enabled` è impostato. |
| `mailarchiver_raw_messages_missing` | gauge | | Email archiviate senza un originale memorizzato. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1 quando l'Account non ha mai completato una Sincronizzazione. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Unix time dell'ultima Sincronizzazione riuscita. Assente per gli Account che non hanno mai effettuato la Sincronizzazione. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Secondi dall'ultima Sincronizzazione riuscita. Assente per gli Account che non hanno mai effettuato la Sincronizzazione. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Messaggi originali che non hanno potuto essere memorizzati. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Esecuzioni di Sincronizzazione terminate con errore, rate limiting o timeout. |

Note:

- I counter sono locali al processo e partono da 0 dopo un riavvio. `rate()` e `increase()`
  in Prometheus gestiscono la cosa correttamente.
- `mailarchiver_raw_messages_missing` conta ogni email archiviata senza un messaggio grezzo,
  incluse le email archiviate prima che `RawMessages:Enabled` fosse attivo. In un'installazione
  che ha memorizzato gli originali fin dal primo giorno, rappresenta il gap rilevante.
- `mailarchiver_account_sync_age_seconds` non è un vero backlog. Indica quando l'ultima Sincronizzazione
  è terminata, non quanti messaggi sono in attesa sul server.
- `mailarchiver_account_never_synced` esiste perché un Account che non ha mai effettuato la Sincronizzazione non ha una
  serie `sync_age`. Un alert solo su `sync_age` lo perderebbe.
- L'endpoint contiene nomi e ID degli Account, ma non contenuti delle email, oggetti o
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

L'immagine include un controllo di liveness che chiama il percorso root senza autenticazione. Il percorso root reindirizza alla pagina di login prima che venga eseguito qualsiasi controller, quindi il controllo non richiede una API key, funziona con la REST API disabilitata e non tocca PostgreSQL. Le installazioni e gli aggiornamenti recenti riportano `healthy` senza alcuna configurazione.

```bash
docker inspect --format '{{.State.Health.Status}}' <container>
```

Il controllo dimostra che il processo accetta richieste HTTP. Non dimostra che PostgreSQL sia raggiungibile. Per quello, chiama `/api/v1/health/ready` da un host di monitoraggio esterno con una API key di amministrazione e `Api:Enabled=true`; l'endpoint risponde 503 quando il database è offline.

Il `docker-compose.yml` incluso non sovrascrive il controllo. Aggiungi un blocco `healthcheck` al servizio se hai bisogno di un intervallo, un timeout o un comando diversi, oppure imposta `healthcheck: disable: true` per disattivare il controllo.

La precedente variabile `MAILARCHIVER_HEALTH_API_KEY` non viene più utilizzata. Rimuovila dai file compose esistenti e dai file `.env`; lasciarla in posizione non è dannoso.

## Sicurezza {#security}

- La chiave è una chiave di amministrazione. Può leggere l'intero Archivio tramite l'API. Trattala come una
  password di un amministratore.
- Utilizza un utente amministratore dedicato per il monitoraggio. Disabilitare quell'utente disabilita gli
  endpoint esterni di health e metrics, non il controllo di liveness del container stesso.
- Mantieni la chiave in un file o in un Docker secret, mai in un parametro di query. I parametri di query
  finiscono nei log del proxy e di accesso.
- Limita l'accesso all'host di monitoraggio o alla rete Prometheus. La chiave API è l'autenticazione, non un
  sostituto dei confini di rete.
- Gli endpoint di health restituiscono solo stringhe di stato. I dettagli delle eccezioni rimangono nel
  log dell'applicazione.

## Risoluzione dei problemi {#troubleshooting}

| Sintomo | Causa | Soluzione |
|---|---|---|
| 404 su tutti e quattro i percorsi | `Api:Enabled=false` | Impostare `Api:Enabled=true` e riavviare. |
| 404 solo su `/metrics` | `Metrics:Enabled=false` | Impostare `Metrics:Enabled=true` e riavviare. |
| 401 | Chiave mancante, errata, revocata o scaduta | Creare una nuova chiave e controllare l'header `Authorization: Bearer`. |
| 403 | La chiave appartiene a un utente non amministratore | Utilizzare una chiave admin. |
| `mailarchiver_metrics_ready 0` per più di un minuto | Il servizio di aggiornamento non ha completato la sua prima esecuzione, o non riesce a raggiungere il database | Controllare il log dell'applicazione per `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | L'ultimo aggiornamento è fallito | Controllare PostgreSQL e la stringa di connessione. I conteggi precedenti rimangono fino al prossimo aggiornamento riuscito. |
| Il container rimane unhealthy | Chiave vuota o `Api:Enabled=false` | Impostare entrambi e ricreare il container. |
| `health/ready` restituisce 503 | PostgreSQL non raggiungibile o schema mancante | Controllare il database e se le migrazioni sono state eseguite. |

## Cosa non coprono le metriche {#what-the-metrics-do-not-cover}

- Raggiungibilità IMAP o Graph per Account. Un'età di Sincronizzazione mostra solo che l'ultima esecuzione è vecchia.
- Il numero di messaggi in attesa sul server, il vero backlog.
- Verifica del checksum di ogni originale memorizzato. Utilizzare la verifica dei messaggi raw esistente e la pulizia degli orfani per questo scopo.
- Conteggi per Cartella o dimensioni della Casella di posta.

Per questi, utilizzare la pagina dell'Account, la pagina dei job in background e il log dell'applicazione.
