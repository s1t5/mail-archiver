# Salud y métricas {#health-and-metrics}

Mail-Archiver expone puntos de conexión de salud y un punto de conexión de métricas compatible con Prometheus para la monitorización externa. Ambos forman parte de la REST API: residen bajo `/api/v1`, requieren una clave de API y requieren que dicha clave pertenezca a un administrador. No existe una ruta de salud anónima ni un token de monitorización independiente.

## Requisitos {#requirements}

| Requisito | Por qué |
|---|---|
| `Api:Enabled=true` | Los endpoints residen bajo `/api/`. Con la API desactivada, el middleware de autenticación responde con un 404. |
| Una clave de API de administrador | Health y metrics exponen el estado de toda la instancia, por lo que una clave que no sea de administrador recibe un 403. |
| `Metrics:Enabled=true` | El endpoint de metrics es opcional. Health funciona sin él. |

Cree la clave en **API Keys** mientras haya iniciado sesión como administrador. Utilice una clave dedicada y, si es posible, un usuario administrador dedicado para la monitorización, de modo que la rotación de la clave o la desactivación del usuario no afecte al acceso de una persona.

## Endpoints {#endpoints}

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | El proceso está vivo | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL es alcanzable y el esquema está presente | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | Estado agregado incluyendo la frescura de la Sincronización | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Formato de texto de Prometheus | 200 | 404 cuando está desactivado, 401, 403 |

Los cuatro utilizan la misma autenticación que el resto de la API:

- clave faltante o inválida: `401` con `WWW-Authenticate: Bearer`
- clave válida sin el rol de administrador: `403` con `application/problem+json`
- API desactivada: `404`

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
después de dos segundos. Devuelve `503` cuando la base de datos no es alcanzable o el
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

Esto ejecuta la comprobación de la base de datos y la comprobación de frescura de la Sincronización. Una Sincronización obsoleta hace que el informe sea
`Degraded`, que permanece en HTTP 200 porque el proceso aún puede atender solicitudes. Solo
`Unhealthy` devuelve `503`.

La comprobación de Sincronización está desactivada por defecto. Establezca `Health:SyncStaleMinutes` al número de
minutos después de los cuales una Cuenta habilitada se considera obsoleta:

```yaml
Health:
  SyncStaleMinutes: 90
```

Una Cuenta que nunca ha realizado la Sincronización tiene `LastSync` en el valor por defecto, por lo que se considera obsoleta
tan pronto como se habilita la comprobación.

## Métricas {#metrics}

El endpoint devuelve el formato de exposición de texto de Prometheus (versión 0.0.4):

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` debe ser `true`. `Metrics:RefreshMinutes` controla con qué frecuencia se reconstruye el
snapshot (predeterminado 5, los valores inferiores a 1 se elevan a 1). El scrape en sí mismo solo lee el
snapshot, por lo que una consulta de Prometheus no puede convertirse en un escaneo de tabla.

| Métrica | Tipo | Etiquetas | Significado |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 1 cuando el endpoint de métricas responde. |
| `mailarchiver_metrics_ready` | gauge | | 1 después del primer snapshot exitoso. |
| `mailarchiver_db_up` | gauge | | 1 cuando la última actualización llegó a PostgreSQL. |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | Tiempo Unix de la última actualización exitosa. |
| `mailarchiver_emails` | gauge | | Número de correos electrónicos en Archivo. |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | Número de Cuenta de correo. |
| `mailarchiver_raw_messages` | gauge | | Número de mensajes originales almacenados. |
| `mailarchiver_raw_messages_enabled` | gauge | | 1 cuando `RawMessages:Enabled` está configurado. |
| `mailarchiver_raw_messages_missing` | gauge | | Correos electrónicos en Archivo sin un original almacenado. |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 1 cuando la Cuenta nunca ha completado una Sincronización. |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | Tiempo Unix de la última Sincronización exitosa. Ausente para Cuentas que nunca se sincronizaron. |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | Segundos desde la última Sincronización exitosa. Ausente para Cuentas que nunca se sincronizaron. |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | Mensajes originales que no pudieron ser almacenados. |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | Ejecuciones de Sincronización que terminaron en fallo, limitación de tasa o tiempo de espera agotado. |

Notas:

- Los contadores son locales al proceso y comienzan en 0 tras un reinicio. `rate()` y `increase()`
  en Prometheus gestionan eso correctamente.
- `mailarchiver_raw_messages_missing` cuenta cada correo electrónico en Archivo sin un mensaje sin procesar,
  incluyendo correos electrónicos archivados antes de que se activara `RawMessages:Enabled`. En una instalación
  que ha almacenado originales desde el primer día, es la brecha relevante.
- `mailarchiver_account_sync_age_seconds` no es un backlog real. Indica cuándo terminó la última Sincronización,
  no cuántos mensajes están esperando en el servidor.
- `mailarchiver_account_never_synced` existe porque una Cuenta que nunca se ha sincronizado no tiene una serie
  `sync_age`. Una alerta solo sobre `sync_age` la pasaría por alto.
- El endpoint contiene nombres e ids de Cuenta, pero no contenidos de correo, asuntos o
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

El archivo de credenciales contiene la clave sin el prefijo `Bearer`. Restrinja sus
permisos al usuario Prometheus.

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

## Docker health check {#docker-health-check}

La imagen y el `docker-compose.yml` incluido comprueban `/api/v1/health/live` con una clave de administrador. La clave no puede existir antes del primer inicio, por lo que el primer inicio se ejecuta sin ella y el contenedor informa `unhealthy` hasta que se establece la clave.

1. Inicie la pila una vez con `MAILARCHIVER_HEALTH_API_KEY` vacío.
2. Inicie sesión como administrador y cree una clave llamada `monitoring-health` en **API Keys**.
3. Coloque la clave en `.env`:

   ```dotenv
   MAILARCHIVER_HEALTH_API_KEY=<key>
   ```

4. Recree el contenedor con `docker compose up -d`.
5. Compruebe el estado:

   ```bash
   docker inspect --format '{{.State.Health.Status}}' <container>
   ```

Se requiere `Api:Enabled=true`. Con la API desactivada, el health check recibe un 404 y el contenedor permanece en estado unhealthy.

Si no desea una clave de administrador en el entorno del contenedor, utilice una comprobación TCP en el contenedor y llame a `/api/v1/health/ready` desde un host de monitorización externo con la clave.
La comprobación TCP no puede determinar si PostgreSQL es accesible.

## Seguridad {#security}

- La clave es una clave de administrador. Puede leer todo el Archivo a través de la API. Trátela como una
  contraseña de administrador.
- Utilice un usuario administrador dedicado para el monitoreo. Deshabilitar ese usuario deshabilita el
  control de salud y las métricas.
- Mantenga la clave en un archivo o en un secreto de Docker, nunca en un parámetro de consulta. Los parámetros de consulta
  terminan en los registros de acceso y del proxy.
- Restrinja el acceso al host de monitoreo o a la red de Prometheus. La clave de la API es la
  autenticación, no un sustituto de los límites de red.
- Los endpoints de salud devuelven solo cadenas de estado. Los detalles de las excepciones permanecen en el
  registro de la aplicación.

## Resolución de problemas {#troubleshooting}

| Síntoma | Causa | Solución |
|---|---|---|
| 404 en las cuatro rutas | `Api:Enabled=false` | Establezca `Api:Enabled=true` y reinicie. |
| 404 solo en `/metrics` | `Metrics:Enabled=false` | Establezca `Metrics:Enabled=true` y reinicie. |
| 401 | Clave faltante, incorrecta, revocada o expirada | Cree una nueva clave y compruebe el encabezado `Authorization: Bearer`. |
| 403 | La clave pertenece a un usuario que no es administrador | Use una clave de administrador. |
| `mailarchiver_metrics_ready 0` durante más de un minuto | El servicio de actualización no ha completado su primera ejecución, o no puede llegar a la base de datos | Compruebe el registro de la aplicación para `Could not refresh the monitoring metrics snapshot`. |
| `mailarchiver_db_up 0` | La última actualización falló | Compruebe PostgreSQL y la cadena de conexión. Los recuentos antiguos permanecen hasta el siguiente éxito. |
| El contenedor permanece en estado unhealthy | Clave vacía o `Api:Enabled=false` | Establezca ambos y recree el contenedor. |
| `health/ready` devuelve 503 | PostgreSQL inaccesible o falta el esquema | Compruebe la base de datos y si se ejecutaron las migraciones. |

## Lo que las métricas no cubren {#what-the-metrics-do-not-cover}

- Accesibilidad de IMAP o Graph por Cuenta. Una edad de Sincronización solo muestra que la última ejecución es antigua.
- El número de mensajes esperando en el servidor, el backlog real.
- Verificación de checksum de cada original almacenado. Utilice la verificación de mensajes sin procesar existente y la limpieza de huérfanos para ello.
- Recuentos por Carpeta o tamaños de Buzón.

Para esto, utilice la página de la Cuenta, la página de trabajos en segundo plano y el registro de la aplicación.
