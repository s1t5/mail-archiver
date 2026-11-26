# Salud y Métricas {#health-and-metrics}

Mail-Archiver expone endpoints de salud y un endpoint de métricas compatible con Prometheus para el monitoreo externo. Ambos son parte de la REST API: residen bajo `/api/v1`, requieren una clave de API y requieren que dicha clave pertenezca a un administrador. No existe una ruta de salud anónima ni un token de monitoreo separado.

## Requisitos {#requirements}

| Requisito | Por qué |
|---|---|
| `Api:Enabled=true` | Los endpoints residen bajo `/api/`. Con la API deshabilitada, el middleware de autenticación responde 404. |
| Una clave de API de administrador | Health y metrics exponen el estado de toda la instancia, por lo que una clave que no sea de administrador recibe un 403. |
| `Metrics:Enabled=true` | El endpoint de metrics es opcional (opt-in). Health funciona sin él. |

Cree la clave en **API Keys** mientras esté conectado como administrador. Utilice una clave dedicada y, si es posible, un usuario administrador dedicado para el monitoreo, de modo que la rotación de la clave o la deshabilitación del usuario no afecte el acceso de una persona.

## Endpoints {#endpoints}

| Método | Ruta | Propósito | Éxito | Errores |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | El proceso está vivo | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL es accesible y el esquema está presente | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Estado agregado incluyendo la frescura de la Sincronización | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Formato de texto de Prometheus | 200 | 404 cuando está deshabilitado, 401, 403 |

Los cuatro utilizan la misma autenticación que el resto de la API:

- clave faltante o inválida: `401` con `WWW-Authenticate: Bearer`
- clave válida sin el rol de administrador: `403` con `application/problem+json`
- API deshabilitada: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Liveness no toca PostgreSQL. Solo demuestra que el proceso acepta solicitudes, por lo que una
caída de la base de datos no hace que Docker o Kubernetes reinicien un proceso saludable.

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

La comprobación abre su propia conexión a PostgreSQL, solicita el esquema de Archivo y se rinde
después de dos segundos. Devuelve `503` cuando la base de datos no es accesible o el
esquema `mail_archiver` falta. Use este endpoint para decidir si una instancia debe
recibir tráfico.

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

Esto ejecuta la comprobación de la base de datos y la comprobación de frescura de la Sincronización. Una Sincronización desactualizada hace que el informe sea
`Degraded`, que permanece en HTTP 200 porque el proceso aún puede atender solicitudes. Solo
`Unhealthy` devuelve `503`.

La comprobación de Sincronización está deshabilitada por defecto. Establezca `Health:SyncStaleMinutes` al número de
minutos después de los cuales una Cuenta habilitada se considera desactualizada:

```yaml
Health:
  SyncStaleMinutes: 90
```

Una Cuenta que nunca se ha sincronizado tiene `LastSync` en el valor por defecto, por lo que se cuenta como desactualizada
tan pronto como se habilita la comprobación.

## Métricas {#metrics}

El endpoint devuelve el formato de exposición de texto de Prometheus (versión 0.0.4):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` debe ser `true`. `Metrics:RefreshMinutes` controla con qué frecuencia se reconstruye el
snapshot (predeterminado 5, los valores inferiores a 1 se elevan a 1). El scrape en sí solo lee el
snapshot, por lo que una consulta de Prometheus no puede convertirse en un escaneo de tabla.

| Métrica | Tipo | Etiquetas | Significado |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1 cuando el endpoint de métricas responde. |
| `mailarchiver_metrics_ready` | gauge | | 1 después del primer snapshot exitoso. |
| `mailarchiver_db_up` | gauge | | 1 cuando la última actualización llegó a PostgreSQL. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Tiempo Unix de la última actualización exitosa. |
| `mailarchiver_emails` | gauge | | Número de correos electrónicos archivados. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Número de cuentas de correo. |
| `mailarchiver_raw_messages` | gauge | | Número de mensajes originales almacenados. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1 cuando `RawMessages:Enabled` está configurado. |
| `mailarchiver_raw_messages_missing` | gauge | | Correos electrónicos archivados sin un original almacenado. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1 cuando la Cuenta nunca ha completado una Sincronización. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Tiempo Unix de la última Sincronización exitosa. Ausente para cuentas que nunca se han sincronizado. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Segundos desde la última Sincronización exitosa. Ausente para cuentas que nunca se han sincronizado. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Mensajes originales que no pudieron almacenarse. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Ejecuciones de Sincronización que terminaron en error, limitación de tasa o tiempo de espera agotado. |

Notas:

- Los contadores son locales al proceso y comienzan en 0 tras un reinicio. `rate()` y `increase()`
  en Prometheus gestionan eso correctamente.
- `mailarchiver_raw_messages_missing` cuenta cada correo electrónico archivado sin un mensaje bruto,
  incluyendo correos archivados antes de que se activara `RawMessages:Enabled`. En una instalación
  que ha almacenado originales desde el primer día, es la brecha relevante.
- `mailarchiver_account_sync_age_seconds` no es un backlog real. Indica cuándo terminó la última Sincronización,
  no cuántos mensajes están esperando en el servidor.
- `mailarchiver_account_never_synced` existe porque una Cuenta que nunca se ha sincronizado no tiene una serie
  `sync_age`. Una alerta solo sobre `sync_age` la omitiría.
- El endpoint contiene nombres e ids de cuentas, pero no contenidos de correo, asuntos o
  credenciales.

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

El archivo de credenciales contiene la clave bruta, sin el prefijo `Bearer`. Restrinja sus
Permisos al usuario de Prometheus.

Ejemplos de alertas:

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

## Verificación de estado de Docker {#docker-health-check}

La imagen incluye una verificación de vida (liveness check) que llama a la ruta raíz sin autenticación. La ruta raíz redirige a la página de inicio de sesión antes de que se ejecute cualquier controlador, por lo que la verificación no necesita una clave de API, funciona con la REST API desactivada y no toca PostgreSQL. Las instalaciones y actualizaciones nuevas reportan `healthy` sin ninguna configuración.

```bash
docker inspect --format '{{.State.Health.Status}}' <container>
```

La verificación demuestra que el proceso acepta solicitudes HTTP. No demuestra que PostgreSQL sea accesible. Para ello, llame a `/api/v1/health/ready` desde un host de monitoreo externo con una clave de API de administrador y `Api:Enabled=true`; el endpoint responde 503 cuando la base de datos está caída.

El `docker-compose.yml` incluido no sobrescribe la verificación. Agregue un bloque `healthcheck` al servicio si necesita un intervalo, tiempo de espera o comando diferente, o establezca `healthcheck: disable: true` para desactivar la verificación.

La antigua variable `MAILARCHIVER_HEALTH_API_KEY` ya no se utiliza. Elimínela de los archivos compose y `.env` existentes; dejarla en su lugar no causa daños.

## Seguridad {#security}

- La clave es una clave de administrador. Puede leer todo el Archivo a través de la API. Trátela como una
  contraseña de administrador.
- Utilice un usuario administrador dedicado para el monitoreo. Deshabilitar ese usuario deshabilita los
  endpoints externos de salud y métricas, no la propia comprobación de liveness del contenedor.
- Mantenga la clave en un archivo o en un Docker secret, nunca en un parámetro de consulta. Los parámetros de consulta
  terminan en los registros de proxy y de acceso.
- Restrinja el acceso al host de monitoreo o a la red de Prometheus. La clave de la API es la
  autenticación, no un sustituto de los límites de red.
- Los endpoints de salud devuelven solo cadenas de estado. Los detalles de la excepción permanecen en el
  registro de la aplicación.

## Solución de problemas {#troubleshooting}

| Síntoma | Causa | Solución |
|---|---|---|
| 404 en las cuatro rutas | `Api:Enabled=false` | Establecer `Api:Enabled=true` y reiniciar. |
| 404 solo en `/metrics` | `Metrics:Enabled=false` | Establecer `Metrics:Enabled=true` y reiniciar. |
| 401 | Clave faltante, incorrecta, revocada o expirada | Crear una nueva clave y verificar el encabezado `Authorization: Bearer`. |
| 403 | La clave pertenece a un usuario que no es administrador | Usar una clave de administrador. |
| `mailarchiver_metrics_ready 0` durante más de un minuto | El servicio de actualización no ha completado su primera ejecución o no puede alcanzar la base de datos | Revisar el registro de la aplicación para `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | La última actualización falló | Revisar PostgreSQL y la cadena de conexión. Los conteos antiguos permanecen hasta el siguiente éxito. |
| El contenedor permanece en estado unhealthy | Clave vacía o `Api:Enabled=false` | Establecer ambos y recrear el contenedor. |
| `health/ready` devuelve 503 | PostgreSQL inaccesible o falta el esquema | Revisar la base de datos y si las migraciones se ejecutaron. |

## Qué no cubren las métricas {#what-the-metrics-do-not-cover}

- Accesibilidad de IMAP o Graph por Cuenta. Una edad de Sincronización solo muestra que la última ejecución es antigua.
- El número de mensajes esperando en el servidor, el backlog real.
- Verificación de Checksum de cada original almacenado. Utilice la verificación de mensajes raw existente y la limpieza de huérfanos para ello.
- Conteos por Carpeta o tamaños de Buzón.

Para esto, utilice la página de Cuenta, la página de trabajos en segundo plano y el registro de la aplicación.
