# État de santé et Métriques {#health-and-metrics}

Mail-Archiver expose des points de terminaison d'état de santé et un point de terminaison de métriques compatible Prometheus pour la surveillance externe. Tous deux font partie de l'API REST : ils se trouvent sous `/api/v1`, nécessitent une clé API, et exigent que cette clé appartienne à un administrateur. Il n'existe pas de chemin de santé anonyme ni de jeton de surveillance distinct.

## Exigences {#requirements}

| Exigence | Pourquoi |
|---|---|
| `Api:Enabled=true` | Les points de terminaison se trouvent sous `/api/`. Si l'API est désactivée, le middleware d'authentification répond par une erreur 404. |
| Une clé API administrateur | La santé et les métriques exposent l'état de l'instance entière, donc une clé non-administrateur reçoit une erreur 403. |
| `Metrics:Enabled=true` | Le point de terminaison des métriques est optionnel. La santé fonctionne sans lui. |

Créez la clé sous **API Keys** tout en étant connecté en tant qu'administrateur. Utilisez une clé dédiée et, si possible, un utilisateur administrateur dédié pour la surveillance, afin que la rotation de la clé ou la désactivation de l'utilisateur n'affecte pas l'accès d'une personne.

## Endpoints {#endpoints}

| Méthode | Chemin | Objectif | Succès | Erreurs |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | Le processus est en vie | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL est accessible et le schéma est présent | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | État agrégé incluant la fraîcheur de la synchronisation | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Format texte Prometheus | 200 | 404 si désactivé, 401, 403 |

Les quatre utilisent la même authentification que le reste de l'API :

- clé manquante ou invalide : `401` avec `WWW-Authenticate: Bearer`
- clé valide sans le rôle admin : `403` avec `application/problem+json`
- API désactivée : `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Le Liveness ne touche pas à PostgreSQL. Il prouve seulement que le processus accepte les requêtes, de sorte qu'une panne de base de données ne force pas Docker ou Kubernetes à redémarrer un processus sain.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

Le contrôle ouvre sa propre connexion PostgreSQL, demande le schéma de l'Archive, et abandonne après deux secondes. Il retourne `503` lorsque la base de données est inaccessible ou que le schéma `mail_archiver` est manquant. Utilisez cet endpoint pour décider si une instance doit recevoir du trafic.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Ceci exécute le contrôle de la base de données et le contrôle de la fraîcheur de la synchronisation. Une synchronisation obsolète rend le rapport `Degraded`, qui reste en HTTP 200 car le processus peut toujours servir des requêtes. Seul `Unhealthy` retourne `503`.

Le contrôle de la synchronisation est désactivé par défaut. Définissez `Health:SyncStaleMinutes` sur le nombre de minutes après lesquelles un Compte activé est considéré comme obsolète :

```yaml
Health:
  SyncStaleMinutes: 90
```

Un Compte qui n'a jamais effectué de synchronisation a `LastSync` à la valeur par défaut, il est donc considéré comme obsolète dès que le contrôle est activé.

## Métriques {#metrics}

Le point de terminaison renvoie le format d'exposition textuel Prometheus (version 0.0.4) :

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` doit être `true`. `Metrics:RefreshMinutes` contrôle la fréquence à laquelle le snapshot
est reconstruit (par défaut 5, les valeurs inférieures à 1 sont élevées à 1). Le scrape lui-même ne fait que lire le
snapshot, de sorte qu'un sondage Prometheus ne peut pas se transformer en scan de table.

| Métrique | Type | Labels | Signification |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1 lorsque le point de terminaison des métriques répond. |
| `mailarchiver_metrics_ready` | gauge | | 1 après le premier snapshot réussi. |
| `mailarchiver_db_up` | gauge | | 1 lorsque la dernière actualisation a atteint PostgreSQL. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Temps Unix de la dernière actualisation réussie. |
| `mailarchiver_emails` | gauge | | Nombre d'e-mails archivés. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Nombre de comptes de messagerie. |
| `mailarchiver_raw_messages` | gauge | | Nombre de messages originaux stockés. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1 lorsque `RawMessages:Enabled` est défini. |
| `mailarchiver_raw_messages_missing` | gauge | | E-mails archivés sans original stocké. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1 lorsque le compte n'a jamais terminé une synchronisation. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Temps Unix de la dernière synchronisation réussie. Absent pour les comptes qui n'ont jamais été synchronisés. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Secondes depuis la dernière synchronisation réussie. Absent pour les comptes qui n'ont jamais été synchronisés. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Messages originaux qui n'ont pas pu être stockés. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Exécutions de synchronisation qui se sont soldées par un échec, une limitation de débit ou un délai d'attente. |

Notes :

- Les compteurs sont locaux au processus et commencent à 0 après un redémarrage. `rate()` et `increase()`
  dans Prometheus gèrent cela correctement.
- `mailarchiver_raw_messages_missing` compte chaque e-mail archivé sans message brut,
  y compris les e-mails archivés avant que `RawMessages:Enabled` ne soit activé. Dans une installation
  qui possède des originaux stockés depuis le premier jour, c'est l'écart pertinent.
- `mailarchiver_account_sync_age_seconds` n'est pas un véritable backlog. Il indique quand la dernière synchronisation
  s'est terminée, et non combien de messages attendent sur le serveur.
- `mailarchiver_account_never_synced` existe car un compte n'ayant jamais été synchronisé n'a pas de série
  `sync_age`. Une alerte sur `sync_age` seule l'ignorerait.
- Le point de terminaison contient les noms et les ids de compte, mais aucun contenu d'e-mail, sujet ou
  identifiant.

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

Le fichier d'identifiants contient la clé brute, sans le préfixe `Bearer`. Restreignez ses
Autorisations à l'utilisateur Prometheus.

Exemples d'alertes :

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

L'image fournit un contrôle de vivacité qui appelle le chemin racine sans authentification. Le chemin racine redirige vers la page de connexion avant l'exécution de tout contrôleur, de sorte que le contrôle ne nécessite aucune clé API, fonctionne avec l'API REST désactivée et n'interagit pas avec PostgreSQL. Les installations et mises à niveau récentes rapportent `healthy` sans aucune configuration.

```bash
docker inspect --format '{{.State.Health.Status}}' <container>
```

Le contrôle prouve que le processus accepte les requêtes HTTP. Il ne prouve pas que PostgreSQL est accessible. Pour cela, appelez `/api/v1/health/ready` depuis un hôte de surveillance externe avec une clé API admin et `Api:Enabled=true` ; le point de terminaison répond 503 lorsque la base de données est hors service.

Le `docker-compose.yml` inclus ne remplace pas le contrôle. Ajoutez un bloc `healthcheck` au service si vous avez besoin d'un intervalle, d'un délai d'expiration ou d'une commande différente, ou définissez `healthcheck: disable: true` pour désactiver le contrôle.

L'ancienne variable `MAILARCHIVER_HEALTH_API_KEY` n'est plus utilisée. Supprimez-la des fichiers compose existants et des fichiers `.env` ; la laisser en place est sans danger.

## Sécurité {#security}

- La clé est une clé d'administrateur. Elle peut lire l'intégralité de l'Archive via l'API. Traitez-la comme un mot de passe d'administrateur.
- Utilisez un utilisateur administrateur dédié pour la surveillance. La désactivation de cet utilisateur désactive les points de terminaison externes de santé et de métriques, et non le propre contrôle de liveness du conteneur.
- Conservez la clé dans un fichier ou un secret Docker, jamais dans un paramètre de requête. Les paramètres de requête finissent dans les journaux du proxy et d'accès.
- Restreignez l'accès à l'hôte de surveillance ou au réseau Prometheus. La clé API est l'authentification, pas un substitut aux limites de réseau.
- Les points de terminaison de santé renvoient uniquement des chaînes de statut. Les détails de l'exception restent dans le journal de l'application.

## Dépannage {#troubleshooting}

| Symptôme | Cause | Solution |
|---|---|---|
| 404 sur les quatre chemins | `Api:Enabled=false` | Définissez `Api:Enabled=true` et redémarrez. |
| 404 uniquement sur `/metrics` | `Metrics:Enabled=false` | Définissez `Metrics:Enabled=true` et redémarrez. |
| 401 | Clé manquante, incorrecte, révoquée ou expirée | Créez une nouvelle clé et vérifiez l'en-tête `Authorization: Bearer`. |
| 403 | La clé appartient à un utilisateur non-administrateur | Utilisez une clé d'administrateur. |
| `mailarchiver_metrics_ready 0` pendant plus d'une minute | Le service de rafraîchissement n'a pas terminé sa première exécution, ou ne peut pas atteindre la base de données | Vérifiez le journal d'application pour `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | Le dernier rafraîchissement a échoué | Vérifiez PostgreSQL et la chaîne de connexion. Les anciens comptes restent jusqu'au prochain succès. |
| Le conteneur reste en état unhealthy | Clé vide ou `Api:Enabled=false` | Définissez les deux et recréez le conteneur. |
| `health/ready` renvoie 503 | PostgreSQL est inaccessible ou le schéma est manquant | Vérifiez la base de données et si les migrations ont été exécutées. |

## Ce que les métriques ne couvrent pas {#what-the-metrics-do-not-cover}

- La connectivité IMAP ou Graph par Compte. Un âge de Synchronisation indique seulement que la dernière exécution est ancienne.
- Le nombre de messages en attente sur le serveur, le véritable retard.
- La vérification du checksum de chaque original stocké. Utilisez la vérification des messages bruts existante et le nettoyage des orphelins pour cela.
- Les décomptes par Dossier ou la taille de la Boîte aux lettres.

Pour ces éléments, utilisez la page du Compte, la page des jobs en arrière-plan et le journal d'audit de l'application.
