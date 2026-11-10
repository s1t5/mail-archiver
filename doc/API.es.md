# Read-Only REST API (v1) {#read-only-rest-api-v1}

Mail Archiver incluye una API REST opcional de **solo lectura** para el acceso programático al correo archivado. Está diseñada para casos de uso de automatización e integración (informes, exportaciones de e-discovery, monitoreo) sin pasar por la interfaz web.

El objetivo es permitir que un script o un agente de IA lea el correo archivado **sin exponer nunca las credenciales del Buzón ni comunicarse con el proveedor de correo.** Una clave de API con alcance por usuario es una capacidad para *leer el Archivo*, no una credencial para *ser el Buzón*; hereda únicamente los permisos del Buzón de su propietario, no otorga ninguna vía de escritura y puede revocarse instantáneamente sin tocar la Cuenta de correo subyacente.

> ⚠️ **La API está desactivada por defecto.** Debe habilitarse explícitamente en la configuración (ver [Enabling the API](#enabling-the-api)). Cuando está desactivada, cada ruta `/api/*` devuelve `404 Not Found`.

La API es de **solo lectura por diseño**: expone cuentas, Carpetas, búsqueda de mensajes, detalles de mensajes y descargas de Adjuntos. Nunca crea, modifica ni elimina datos.

## Tabla de contenidos {#table-of-contents}

- [Habilitación de la API](#enabling-the-api)
- [Autenticación](#authentication)
  - [Formato de la clave de API](#api-key-format)
  - [Ciclo de vida de la clave](#key-lifecycle)
  - [Modelo de Permiso](#permission-model)
  - [Justificación de seguridad](#security-rationale)
- [Convenciones](#conventions)
  - [URL base y versionado](#base-url-and-versioning)
  - [Paginación](#pagination)
  - [Errores](#errors)
  - [Limitación de tasa](#rate-limiting)
- [Endpoints](#endpoints)
  - [Listar Cuentas](#list-accounts)
  - [Listar Carpetas](#list-folders)
  - [Buscar mensajes](#search-messages)
  - [Obtener mensaje](#get-message)
  - [Descargar Adjunto](#download-attachment)
  - [Descargar mensaje (.eml)](#download-message-eml)
  - [Descargar mensaje original](#download-original-message)
  - [Estadísticas](#stats)
- [Tipos de datos](#data-types)
- [Registro de acceso](#access-logging)
- [OpenAPI y Swagger UI](#openapi-and-swagger-ui)
- [Referencia de la API](API-Reference.md) — referencia de endpoint y tipo de datos generada

## Habilitación de la API {#enabling-the-api}

La API se configura bajo la sección `Api` de `appsettings.json` (o las variables de entorno equivalentes, por ejemplo, `Api__Enabled=true`):

```json
"Api": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "AllowMessageDownloads": true,
  "EnableSwaggerUi": true,
  "DefaultPageSize": 20,
  "MaxPageSize": 100,
  "RateLimitPerMinute": 120
}
```


### Ciclo de vida de la clave {#key-lifecycle}

1. **Crear** — un usuario crea una clave con un `Name` descriptivo y una expiración opcional. La respuesta muestra la clave en texto plano una sola vez.
2. **Usar** — enviar la clave como un bearer token. Cada solicitud aceptada actualiza la `LastUsedAt` de la clave (las escrituras se limitan a un máximo de una vez cada ~5 minutos para evitar una escritura en cada llamada).
3. **Expirar** — si `ExpiresAt` está configurado y en el pasado, la clave es rechazada.
4. **Revocar** — el usuario propietario o un administrador revoca la clave (se establece `RevokedAt`); es rechazada de forma inmediata y permanente.

Una clave está **activa** cuando no ha sido revocada, no ha expirado **y** su usuario propietario sigue activo. Una clave inactiva por cualquiera de estos motivos es rechazada con `401`.

### Modelo de Permisos {#permission-model}

Una clave de API hereda los permisos exactos del usuario que la posee. El principal autenticado lleva las mismas reclamaciones que emite un inicio de sesión web normal, por lo que toda la autorización existente

| Configuración | Predeterminado | Descripción |
| --- | --- | --- |
| `Enabled` | `false` | Interruptor maestro. Cuando `false`, todas las rutas `/api/*` devuelven `404` y el documento OpenAPI / Swagger UI no se mapean. |
| `AllowAttachmentDownloads` | `true` | Cuando `false`, el endpoint de descarga de Adjuntos devuelve `403`. Los metadatos sobre los Adjuntos aún son devueltos por el endpoint de detalle de mensaje. |
| `AllowMessageDownloads` | `true` | Cuando `false`, los endpoints de mensaje completo (`/eml` y `/raw`) devuelven `403`. Un mensaje completo lleva sus Adjuntos, por lo que **tanto** este interruptor como `AllowAttachmentDownloads` deben ser `true`; desactivar cualquiera de los dos deshabilita ambos endpoints. |
| `EnableSwaggerUi` | `true` | Cuando `true` (y `Enabled` es `true`), monta Swagger UI en `/apidocs` y el documento OpenAPI en `/apidocs/spec/v1.json`. Ambos requieren una **sesión web (cookie)** autenticada — no son parte de `/api/` y no son alcanzables con una clave de API. |
| `DefaultPageSize` | `20` | Tamaño de página utilizado cuando una solicitud omite `pageSize`. |
| `MaxPageSize` | `100` | Límite superior; los valores de `pageSize` más grandes se ajustan a este. |
| `RateLimitPerMinute` | `120` | Presupuesto de solicitud de ventana fija por clave de API por minuto (ver [Rate limiting](#rate-limiting)). |

## Autenticación {#authentication}

La API se autentica con **claves API por usuario** enviadas como un bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` acepta **únicamente** claves API. Las cookies del navegador no son respetadas, por lo que la
API no tiene superficie de CSRF. Los fallos de autenticación devuelven `401` con un
encabezado `WWW-Authenticate: Bearer` y un cuerpo [problem+json](#errors); la API
**nunca** redirige a la página de inicio de sesión.

Las claves se gestionan mediante autoservicio en la interfaz web bajo **API Keys** (cada usuario
gestiona sus propias claves; los administradores pueden ver y revocar las claves de cualquier usuario).

### Formato de la clave API {#api-key-format}

Una clave es la cadena `ma_` seguida de 43 caracteres base64 seguros para URL que codifican 32
bytes aleatorios (256 bits de entropía):

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Almacenamiento:

- **`KeyPrefix`** — los primeros ~11 caracteres (p. ej., `ma_8Kf3pQ2`), almacenados en
  texto plano. Se utiliza para identificar una clave en la interfaz de usuario y en los logs. No es un secreto.
- **`KeyHash`** — el SHA-256 codificado en hexadecimal de la clave completa, almacenado bajo un
  **índice único**. La clave en texto plano nunca se almacena.

La clave completa se muestra al usuario **exactamente una vez**, en el momento de la creación. No se puede
recuperar después; una clave perdida debe ser revocada y reemplazada.

### Ciclo de vida de la clave {#key-lifecycle}

1. **Crear** — un usuario crea una clave con un `Name` descriptivo y una expiración opcional.
   La respuesta muestra la clave en texto plano una vez.
2. **Usar** — enviar la clave como un bearer token. Cada solicitud aceptada actualiza la `LastUsedAt` de la
   clave (las escrituras están limitadas a como máximo una vez cada ~5 minutos para
   evitar una escritura en cada llamada).
3. **Expirar** — si `ExpiresAt` está establecido y en el pasado, la clave es rechazada.
4. **Revocar** — el usuario propietario o un administrador revoca la clave
   (se establece `RevokedAt`); es rechazada de forma inmediata y permanente.

Una clave está **activa** cuando no está revocada, no ha expirado **y** su usuario propietario
sigue activo. Una clave inactiva por cualquiera de estas razones es rechazada con `401`.

### Modelo de permisos {#permission-model}

Una clave API hereda los permisos exactos del usuario que la posee. El
principal autenticado lleva las mismas claims que un inicio de sesión web normal emite, por lo que
toda la lógica de autorización existente se aplica sin cambios:

- **Administradores** (`IsAdmin`) ven todas las cuentas de correo y todos los mensajes.
- **Usuarios regulares** ven solo las cuentas de correo asignadas a ellos
  (`UserMailAccount`). Las cuentas y mensajes fuera de ese conjunto se tratan como
  **inexistentes**: las solicitudes para ellos devuelven `404`, no `403`, por lo que la API
  no filtra la existencia de cuentas que un llamador no puede ver.

### Justificación de seguridad {#security-rationale}

- **Evasión de 2FA.** Las claves API eluden la autenticación de dos factores por diseño — esto es
  inherente a los tokens no interactivos (lo mismo ocurre con los tokens de acceso personal
  en GitHub/GitLab). Se mitiga mediante: claves que están **desactivadas por defecto**,
  revocables, con expiración opcional, limitadas a los permisos del usuario y cada
  acceso registrado en el [access log](#access-logging).
- **Sin cookies en `/api`.** La autenticación por cookies no es aceptada en las rutas `/api`,
  eliminando las preocupaciones de CSRF para la API.
- **Transporte.** Siempre sirva la API a través de HTTPS en producción para que los bearer tokens
  no queden expuestos en el cable.

## Convenciones {#conventions}

### URL base y versionado {#base-url-and-versioning}

Todos los endpoints se encuentran bajo `/api/v1`. La versión está en la ruta para que futuros cambios disruptivos puedan lanzarse como `/api/v2` sin afectar a los clientes existentes.

Las respuestas son `application/json` a menos que se indique lo contrario (las descargas de Adjunto devuelven el propio content type del Adjunto). Todas las marcas de tiempo son UTC, ISO 8601 (`2026-06-11T09:30:00Z`).

### Paginación {#pagination}

Los endpoints de listado devuelven un sobre de paginación:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` es basado en 1. Las páginas fuera de rango devuelven un array `items` vacío (no es un error).
- `pageSize` tiene por defecto `Api:DefaultPageSize` y está limitado a `[1, Api:MaxPageSize]`.

### Errores {#errors}

Los errores utilizan [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
`application/problem+json`:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Unknown sortBy value 'foo'.",
  "instance": "/api/v1/emails"
}
```

| Status | Cuándo |
| --- | --- |
| `400 Bad Request` | Parámetros malformados (p. ej., fecha no procesable, `sortBy` inválido). |
| `401 Unauthorized` | Clave faltante, malformada, expirada, revocada o de otro modo inválida. Incluye `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Se solicitaron descargas de Adjunto mientras `AllowAttachmentDownloads=false`, o descargas de mensajes mientras `AllowMessageDownloads=false`. |
| `404 Not Found` | El recurso no existe **o** está fuera de las cuentas permitidas del llamante. También se devuelve para cada ruta `/api/*` cuando `Api:Enabled=false`. |
| `429 Too Many Requests` | Límite de tasa excedido. |
| `500 Internal Server Error` | Error inesperado del servidor (problem+json, nunca la página de error HTML). |

### Límite de tasa {#rate-limiting}

Las solicitudes están limitadas por una política de ventana fija de `Api:RateLimitPerMinute` solicitudes por minuto (por defecto `120`), particionadas por el prefijo de la clave de API (recurriendo a la IP del cliente cuando no hay una clave presente). Exceder el presupuesto devuelve `429`.

## Endpoints {#endpoints}

> Todos los ejemplos asumen que la API está habilitada y que `$KEY` contiene una clave válida.

### Listar cuentas {#list-accounts}

```
GET /api/v1/accounts
```

Devuelve las cuentas de correo visibles para el solicitante. Los campos de credenciales
(IMAP/OAuth username, password, client secret, …) **nunca** se incluyen.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/accounts
```

```json
[
  {
    "id": 1,
    "name": "Support Mailbox",
    "emailAddress": "support@example.com",
    "provider": "IMAP",
    "isEnabled": true,
    "lastSync": "2026-06-11T08:15:00Z"
  }
]
```

### List folders {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

Devuelve el árbol de la Carpeta para una Cuenta como una estructura anidada. Devuelve `404` si la Cuenta no existe o no es visible para el solicitante.

```json
[
  {
    "name": "INBOX",
    "fullPath": "INBOX",
    "totalCount": 4213,
    "level": 0,
    "children": [
      { "name": "Work", "fullPath": "INBOX/Work", "totalCount": 512, "level": 1, "children": [] }
    ]
  }
]
```

### Buscar mensajes {#search-messages}

```
GET /api/v1/emails
```

Busca mensajes en el Archivo con las mismas capacidades que la interfaz de usuario web.

| Parámetro de consulta | Tipo | Descripción |
| --- | --- | --- |
| `q` | string | Consulta de texto completo sobre subject, body, from, to, cc, bcc. Soporta sintaxis de campo, ej. `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Fecha de envío más temprana (inclusive), `YYYY-MM-DD`. |
| `to` | date | Fecha de envío más tardía (inclusive), `YYYY-MM-DD`. |
| `accountId` | int | Restringir a una Cuenta (debe ser visible para el llamador). |
| `folder` | string | Restringir a una Carpeta mediante la ruta completa. |
| `direction` | string | `incoming` o `outgoing`. |
| `page` | int | Número de página basado en 1 (por defecto `1`). |
| `pageSize` | int | Resultados por página (por defecto `Api:DefaultPageSize`, limitado a `Api:MaxPageSize`). |
| `sortBy` | string | Uno de `sentDate` (por defecto), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` o `desc` (por defecto `desc`). |

Devuelve un [paged envelope](#pagination) de [`EmailSummaryDto`](#emailsummarydto).
Registra una entrada de [access log](#access-logging) de tipo `Search`.

```bash
curl -H "Authorization: Bearer $KEY" \
  "https://host/api/v1/emails?q=subject:invoice&from=2026-01-01&pageSize=50"
```

```json
{
  "items": [
    {
      "id": 9876,
      "accountId": 1,
      "subject": "Invoice #4711",
      "from": "billing@vendor.com",
      "to": "support@example.com",
      "sentDate": "2026-03-02T11:04:00Z",
      "isOutgoing": false,
      "hasAttachments": true,
      "folderName": "INBOX"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalItems": 3,
  "totalPages": 1
}
```

### Obtener mensaje {#get-message}

```
GET /api/v1/emails/{id}
```

Devuelve el mensaje completo, incluyendo los cuerpos. El cuerpo utiliza la misma cadena de fallback que la interfaz web: `OriginalBodyHtml` (bytes brutos) → `BodyUntruncatedHtml` →
`HtmlBody`, y el equivalente para el cuerpo de texto. Devuelve `404` si el mensaje
no existe o pertenece a una Cuenta fuera de los permisos del llamador. Registra una entrada de registro de acceso de tipo `Open`.

Devuelve un [`EmailDetailDto`](#emaildetaildto).

### Descargar Adjunto {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Transmite los bytes del Adjunto con su tipo de contenido y nombre de archivo almacenados. Devuelve
`404` si el mensaje o el Adjunto no existen o no son visibles para el solicitante. Devuelve `403` (problem+json) cuando `Api:AllowAttachmentDownloads=false`.
Registra una entrada de registro de acceso de tipo `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Descargar mensaje (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Devuelve el mensaje como `message/rfc822`, reconstruido a partir de los campos archivados:
direcciones, los encabezados raw almacenados (`Received`, `Authentication-Results`,
`X-Spam-*`, …), cuerpos y Adjuntos. Funciona para cada correo electrónico archivado. Es la
misma reconstrucción que produce la exportación de Cuenta de la interfaz de usuario web, por lo que **no** es
idéntico en bytes a lo que se recibió: las firmas DKIM no se verificarán contra él.
Use [`/raw`](#download-original-message) para eso.

Devuelve `404` si el mensaje no existe o no es visible para el solicitante, y
`403` (problem+json) cuando `Api:AllowMessageDownloads=false`. Registra una entrada de Registro de auditoría
de tipo `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Descargar mensaje original {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Devuelve el mensaje original tal como se recibió (`message/rfc822`), para análisis de spam,
verificación de DKIM o comprobaciones forenses. El original solo se almacena cuando
`RawMessages:Enabled` se estableció en el momento en que el correo electrónico fue archivado; consulte
[Original Message Storage Settings](Setup.md#-original-message-storage-settings).
Si un correo electrónico tiene uno se muestra mediante `hasRawMessage` en
[`EmailDetailDto`](#emaildetaildto).

| Response header | Significado |
| --- | --- |
| `X-Raw-Message-Sha256` | SHA-256 de los bytes devueltos, registrado cuando el mensaje fue almacenado |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (copias exactas) o `serialized` (reescrito por el parser, finales de línea normalizados a CRLF) |
| `X-Raw-Message-Storage` | `postgres` o `filesystem` — de dónde se leyeron los bytes (ver [Storage backend](RawMessages.md#storage-backend)) |

Los bytes pueden residir en la base de datos o en un archivo, dependiendo de
`RawMessages:Storage` en el momento en que el mensaje fue archivado; el endpoint
lo resuelve por mensaje, por lo que ambos tipos se sirven de la misma manera. Con
`RawMessages:FileSystem:VerifyChecksumOnRead` (por defecto `true`) el archivo es
re-hasheado antes de ser enviado, y un mensaje cuyo contenido ya no coincide con el
checksum registrado **no** se sirve.

Devuelve `404` si el mensaje no existe o no es visible para el llamante,
`404` con el título del problema `No original message is stored for this email.`
cuando existe sin un original, y `500` con el título
`Stored original message failed its checksum.` cuando el contenido almacenado ya no
coincide con el checksum registrado en el momento del archivo. Devuelve `403` (problem+json) cuando
`Api:AllowMessageDownloads=false`. Registra una entrada de registro de acceso de tipo `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -o 9876.eml \
  https://host/api/v1/emails/9876/raw
sha256sum 9876.eml          # matches X-Raw-Message-Sha256
spamassassin -t < 9876.eml  # or: rspamc < 9876.eml
```

### Estadísticas {#stats}

```
GET /api/v1/stats
```

Devuelve estadísticas agregadas del Archivo visibles para el solicitante: total de correos electrónicos archivados, cuentas de correo visibles, Adjuntos en esos correos electrónicos y el tamaño total de la base de datos PostgreSQL en megabytes. Para usuarios que no son administradores, los recuentos se limitan a las cuentas de correo asignadas a ellos (el mismo alcance que aplica el panel web); los administradores ven los totales globales.

Para claves de API que no son de administrador, `databaseSizeInMB` es una cadena vacía: el tamaño de la base de datos describe toda la instancia, no el alcance de la clave. Los correos electrónicos, las Cuentas y los Adjuntos siempre están limitados a las cuentas a las que la clave puede acceder.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/stats
```

```json
{
  "emails": 160132,
  "accounts": 4,
  "attachments": 43540,
  "databaseSizeInMB": "5432"
}
```

### StatsDto {#statsdto}

| Campo | Tipo | Notas |
| --- | --- | --- |
| `emails` | int | Mensajes en Archivo visibles para el solicitante |
| `accounts` | int | Cuentas de Buzón visibles para el solicitante |
| `attachments` | int | Adjuntos en esos mensajes visibles |
| `databaseSizeInMB` | string | Tamaño total de la base de datos PostgreSQL en MB; vacío para claves que no son de administrador |

## Tipos de datos {#data-types}

### MailAccountDto {#mailaccountdto}

| Campo | Tipo | Notas |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

Nunca se serializan campos de credenciales.

### FolderNodeDto {#foldernodedto}

| Campo | Tipo | Notas |
| --- | --- | --- |
| `name` | string | Nombre de la hoja |
| `fullPath` | string | Ruta completa |
| `totalCount` | int | Mensajes en la Carpeta |
| `level` | int | Profundidad de anidamiento (0 = raíz) |
| `children` | FolderNodeDto[] | Subcarpetas |

### EmailSummaryDto {#emailsummarydto}

| Campo | Tipo |
| --- | --- |
| `id` | int |
| `accountId` | int |
| `subject` | string |
| `from` | string |
| `to` | string |
| `sentDate` | datetime |
| `isOutgoing` | bool |
| `hasAttachments` | bool |
| `folderName` | string |

### EmailDetailDto {#emaildetaildto}

Todo lo de [`EmailSummaryDto`](#emailsummarydto) más:

| Campo | Tipo | Notas |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Resuelto mediante la cadena de fallback del cuerpo |
| `htmlBody` | string | Resuelto mediante la cadena de fallback del cuerpo |
| `attachments` | AttachmentDto[] | Solo metadatos |
| `hasRawMessage` | bool | `true` cuando el mensaje original se almacena y [`/raw`](#download-original-message) lo devuelve |

### AttachmentDto {#attachmentdto}

| Campo | Tipo | Notas |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bytes |

Los bytes del Adjunto se obtienen del [endpoint de descarga](#download-attachment),
no de este DTO.

## Registro de acceso {#access-logging}

Cada acceso a los datos a través de la API se registra en el mismo registro de acceso utilizado por la interfaz web (visible en **Logs**), atribuido al usuario propietario de la clave:

| Endpoint | Tipo registrado |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI y Swagger UI {#openapi-and-swagger-ui}

Cuando la API está habilitada y `Api:EnableSwaggerUi=true`:

- El documento OpenAPI v1 se sirve en **`/apidocs/spec/v1.json`**.
- Swagger UI se sirve en **`/apidocs`**.

Ambas rutas se encuentran fuera de `/api/` y, por lo tanto, están protegidas por la autenticación web (cookie) normal — se requiere una sesión de navegador iniciada para visualizarlas, y no se puede acceder a ellas con una clave de API.

### Referencia de endpoints en este sitio {#endpoint-reference-on-this-site}

Los endpoints y los tipos de datos también se publican aquí como una página generada:
**[API Reference](API-Reference.md)**. Se renderiza a partir del mismo documento OpenAPI — se mantiene una copia en `doc/assets/openapi-v1.json` — por
`tools/gen_api_reference.py`. El CI falla cuando la página y la especificación pierden la Sincronización, por lo que la referencia siempre describe la API que realmente se distribuye.

!!! note "Regeneración"
    La página es un resultado generado. Cambie la API (o el generador), luego ejecute
    `python3 tools/gen_api_reference.py` y realice el commit del resultado.

    Actualizar la especificación suministrada significa volver a ejecutar la generación de
    documentos en tiempo de compilación descrita en `tools/README.md`.
