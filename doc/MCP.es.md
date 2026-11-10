# MCP Server (Model Context Protocol) {#mcp-server-model-context-protocol}

Mail Archiver incluye un **servidor MCP (Model Context Protocol)** opcional que
expone el correo archivado a agentes de IA a través del transporte Streamable HTTP.
Es un reflejo de la [REST API](API.md) de solo lectura: mismos datos, mismo modelo de
permiso por usuario, mismo registro de acceso; simplemente empaquetado como herramientas MCP en lugar de endpoints REST, para que un agente LLM pueda descubrirlos y llamarlos directamente.

El objetivo es permitir que un agente de IA lea el correo archivado **sin exponer nunca
las credenciales del Buzón ni comunicarse con el proveedor de correo.** Una clave de API por usuario con alcance limitado es una capacidad para *leer el Archivo*, no una credencial para *ser el Buzón*; hereda únicamente los permisos del Buzón de su propietario, no otorga ninguna ruta de escritura y puede revocarse instantáneamente sin tocar la Cuenta de correo subyacente.

> ⚠️ **El servidor MCP está desactivado por defecto.** Debe habilitarse explícitamente en la
> configuración (consulte [Enabling the MCP server](#enabling-the-mcp-server)). Cuando
> está desactivado, el endpoint `/mcp` devuelve `404 Not Found`.

El servidor MCP es **de solo lectura por diseño**: expone herramientas para listar cuentas,
listar carpetas, buscar mensajes, leer un mensaje y descargar un Adjunto.
Nunca crea, modifica ni elimina datos.

## Tabla de contenidos {#table-of-contents}

- [Habilitación del servidor MCP](#enabling-the-mcp-server)
- [Autenticación](#authentication)
- [Transporte y endpoint](#transport-and-endpoint)
- [Tools](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
  - [get_email_eml](#get_email_eml)
  - [get_email_raw](#get_email_raw)
- [Limitación de tasa](#rate-limiting)
- [Registro de acceso](#access-logging)
- [Conexión de un agente](#connecting-an-agent)
- [Guía de seguridad para la exposición a internet](#security-guidance-for-internet-exposure)

## Habilitar el servidor MCP {#enabling-the-mcp-server}

El servidor MCP se configura bajo la sección `Mcp` de `appsettings.json` (o las variables de entorno equivalentes, por ejemplo, `Mcp__Enabled=true`):

```json
"Mcp": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "DefaultPageSize": 20,
  "MaxResults": 100,
  "MaxAttachmentBytes": 10000000,
  "AllowMessageDownloads": true,
  "MaxMessageBytes": 10000000,
  "RateLimitPerMinute": 120
}
```

| Setting | Default | Description |
| --- | --- | --- |
| `Enabled` | `false` | Interruptor maestro. Cuando `false`, el endpoint `/mcp` devuelve `404`. |
| `AllowAttachmentDownloads` | `true` | Cuando `false`, `get_attachment` devuelve un error en lugar de los bytes del Adjunto. Los metadatos sobre los Adjuntos siguen siendo devueltos por `get_email`. |
| `DefaultPageSize` | `20` | Tamaño de página utilizado por `search_emails` cuando el llamador omite `pageSize`. |
| `MaxResults` | `100` | Límite superior para el tamaño de página `search_emails`; los valores más grandes se ajustan. |
| `MaxAttachmentBytes` | `10000000` | Tamaño máximo de Adjunto (en bytes) que `get_attachment` devolverá en línea como base64; los Adjuntos más grandes son rechazados. |
| `AllowMessageDownloads` | `true` | Cuando `false`, `get_email_eml` y `get_email_raw` devuelven un error en lugar del mensaje. Un mensaje completo incluye sus Adjuntos, por lo que **ambos**, este interruptor y `AllowAttachmentDownloads`, deben estar en `true`; desactivar cualquiera de los dos desactiva ambas herramientas. |
| `MaxMessageBytes` | `10000000` | Tamaño máximo de mensaje (en bytes) que `get_email_eml` y `get_email_raw` devolverán en línea; los mensajes más grandes son rechazados. |
| `RateLimitPerMinute` | `120` | Presupuesto de solicitudes de ventana fija por clave de API por minuto (ver [Rate limiting](#rate-limiting)). |

## Autenticación {#authentication}

El servidor MCP se autentica con las **mismas API keys por usuario que la REST API**, enviadas como un bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

Las API keys se crean y gestionan en la interfaz web bajo **API Keys**. Una clave hereda los permisos exactos del usuario que la posee (un administrador ve todas las cuentas; un usuario restringido ve solo sus cuentas asignadas). Las claves pueden ser revocadas o caducar independientemente de las cuentas de correo subyacentes. Consulte la [sección de autenticación de la REST API](API.md#authentication) para conocer el ciclo de vida completo de las claves y el modelo de permisos; las claves de MCP no son un almacén de credenciales separado.

Cuando el servidor MCP está desactivado, o no se presenta un bearer token válido, el endpoint devuelve `404` / `401` respectivamente (nunca redirige a la página de inicio de sesión).

## Transporte y endpoint {#transport-and-endpoint}

El servidor MCP utiliza el transporte **Streamable HTTP** y está montado en:

```
POST /mcp
```

Se ejecuta en modo **stateless** (una solicitud JSON-RPC por cada solicitud HTTP), que es el modo recomendado para servidores que no necesitan solicitudes de servidor a cliente como sampling o elicitation. Las herramientas se descubren a través de la solicitud estándar MCP `tools/list` y se llaman a través de `tools/call`.

## Herramientas {#tools}

### list_accounts {#list_accounts}

Enumera las cuentas de correo a las que la clave de API actual tiene permitido acceder. Devuelve
`id`, `name`, `emailAddress`, `provider`, `isEnabled` y `lastSync`.

**Parámetros:** ninguno.

**Resultado:** array de objetos de Cuenta. Utilice el `id` devuelto con `list_folders`
y `search_emails`.

### list_folders {#list_folders}

Lista el árbol de Carpetas de una única Cuenta de correo, con recuentos de correos por Carpeta.

**Parámetros:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `accountId` | integer | yes | El id de la Cuenta de correo (de `list_accounts`). |

**Resultado:** array de objetos de Carpeta, cada uno con `name`, `fullPath`, `totalCount`,
`level` y `children` recursivo. Devuelve una lista vacía si la Cuenta no
existe o si la clave no está autorizada para ella.

### search_emails {#search_emails}

Busca correos electrónicos en el Archivo mediante una consulta de texto completo, rango de fechas, Cuenta, Carpeta y dirección.

**Parámetros:**

| Nombre | Tipo | Requerido | Descripción |
| --- | --- | --- | --- |
| `q` | string | no | Consulta de búsqueda de texto completo (asunto, cuerpo, de/para). |
| `from` | string (ISO-8601) | no | Filtro de fecha/hora UTC de envío en o después de. |
| `to` | string (ISO-8601) | no | Filtro de fecha/hora UTC de envío en o antes de. |
| `accountId` | integer | no | Restringir a un único id de Cuenta de correo. |
| `folder` | string | no | Restringir a una ruta completa de Carpeta única (ej. `INBOX`). |
| `direction` | string | no | `incoming` o `outgoing`. |
| `page` | integer | no | Número de página basado en 1 (por defecto `1`). |
| `pageSize` | integer | no | Tamaño de página; limitado a `[1, MaxResults]`. `0` usa `DefaultPageSize`. |
| `sortBy` | string | no | `sentdate` (por defecto), `receiveddate`, `subject`, `from`, `to`. |
| `sortOrder` | string | no | `asc` o `desc` (por defecto `desc`). |

**Resultado:** un objeto paginado con `items` (arreglo de resúmenes de correo electrónico), `page`,
`pageSize`, `totalItems`, `totalPages`. Cada resumen contiene `id`, `accountId`,
`subject`, `from`, `to`, `sentDate`, `isOutgoing`, `hasAttachments` y
`folderName`. Pase un `id` devuelto a `get_email` para obtener el cuerpo completo y la lista de Adjuntos.

### get_email {#get_email}

Obtiene el contenido completo de un único correo electrónico archivado.

**Parámetros:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | El id del correo electrónico archivado (de `search_emails`). |

**Resultado:** un único objeto de correo electrónico con todos los campos de resumen más `cc`, `bcc`,
`receivedDate`, `messageId`, `textBody`, `htmlBody` y `attachments` (matriz de
`{ id, fileName, contentType, size }`) y `hasRawMessage` (si
`get_email_raw` puede devolver el original). Devuelve `null` si el correo electrónico no
existe o la clave no está autorizada para su Cuenta.

### get_attachment {#get_attachment}

Descarga un único Adjunto de correo electrónico como un blob base64.

**Parámetros:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | El id del correo electrónico en el Archivo al que pertenece el Adjunto. |
| `attachmentId` | integer | yes | El id del Adjunto (de `attachments` de `get_email`). |

**Resultado:** un objeto con `id`, `emailId`, `fileName`, `contentType`, `size`
y `base64Data` (los bytes brutos del Adjunto, codificados en base64). La herramienta devuelve un
error cuando `Mcp:AllowAttachmentDownloads=false`, cuando el Adjunto excede
`Mcp:MaxAttachmentBytes`, o cuando la clave no está autorizada para la
Cuenta del correo electrónico.

### get_email_eml {#get_email_eml}

Devuelve un correo electrónico como un mensaje RFC 5322 completo (`.eml`), reconstruido a partir de los campos en el Archivo: los encabezados almacenados (`Received`, `Authentication-Results`, `X-Spam-*`, …), cuerpos y Adjuntos. Funciona para cada correo electrónico en el Archivo, pero el resultado no es idéntico en bytes a lo que se recibió, por lo que las firmas DKIM no se verificarán contra él; use [`get_email_raw`](#get_email_raw) para ello. Refleja `GET /api/v1/emails/{id}/eml`.

**Parámetros:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | El id del correo electrónico en el Archivo (de `search_emails`). |

**Resultado:** un objeto con `emailId`, `kind` (`eml`), `source` (`null`), `sha256`, `size`, `encoding` y `content`. `content` es el texto del mensaje cuando `encoding` es `utf-8`, y el mensaje codificado en base64 cuando `encoding` es `base64` (mensajes que no son UTF-8 válidos, por lo que no se altera ningún byte). La herramienta devuelve un error cuando `Mcp:AllowMessageDownloads=false`, cuando el mensaje excede `Mcp:MaxMessageBytes`, o cuando la clave no está autorizada para la Cuenta del correo electrónico.

### get_email_raw {#get_email_raw}

Devuelve el mensaje original exactamente como se recibió, para análisis de spam,
comprobaciones DKIM/SPF/ARC o forense. Solo disponible cuando el original se almacenó en el momento del
Archivo (`RawMessages:Enabled`, ver
[Original Message Storage Settings](Setup.md#-original-message-storage-settings));
`get_email` lo informa como `hasRawMessage`. Refleja
`GET /api/v1/emails/{id}/raw`.

**Parámetros:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | El id del correo electrónico archivado (de `search_emails`). |

**Resultado:** el mismo objeto que `get_email_eml` con `kind` `raw`, `source` configurado
de la misma forma en que se capturó el original (`imap`, `graph`, `eml-file` — copias exactas —
o `serialized`, reescrito por el parser con los finales de línea normalizados a CRLF)
y `sha256` el checksum registrado en el momento del Archivo. Devuelve los mismos errores que
`get_email_eml`, y un error cuando no hay un original almacenado para el correo electrónico.

Los bytes se leen de donde sea que hayan sido almacenados — la base de datos o un archivo,
dependiendo de `RawMessages:Storage` en el momento del Archivo (ver
[Storage backend](RawMessages.md#storage-backend)). Con
`RawMessages:FileSystem:VerifyChecksumOnRead` (por defecto `true`) el archivo se
vuelve a procesar con un hash antes de ser devuelto; un mensaje cuyo contenido ya no coincide con el
checksum registrado es rechazado con un error en lugar de ser devuelto.

## Rate limiting {#rate-limiting}

El servidor MCP tiene su propia política de límite de tasa de ventana fija (`"Mcp"`), particionada
por el prefijo de la API-key no secreto (los primeros 11 caracteres del token `ma_...`),
recurriendo a la IP del cliente cuando no hay un token de portador presente. El presupuesto se
configura a través de `Mcp:RateLimitPerMinute` (por defecto `120` por minuto). Cuando
se excede, el endpoint devuelve `429 Too Many Requests` con un encabezado `Retry-After`. Esto es independiente de la política de límite de tasa de la REST API.

## Registro de acceso {#access-logging}

Cada llamada a una herramienta se registra a través del mismo `IAccessLogService` que la REST API, con el nombre de usuario propietario y el tipo de acceso correspondiente (`Search`, `Open`, `Download`). Por lo tanto, la actividad de MCP es visible en la página **Logs** junto con la actividad de la REST API y la interfaz web, para un único Registro de auditoría.

## Conexión de un agente {#connecting-an-agent}

El servidor MCP utiliza el transporte estándar MCP Streamable HTTP, por lo que cualquier cliente MCP que admita HTTP puede conectarse. La URL es la URL base de la aplicación más `/mcp`, por ejemplo `http://localhost:5179/mcp`, y la clave de API se proporciona a través del encabezado `Authorization: Bearer ma_...`.

Para agentes configurados mediante el JSON estándar del cliente MCP, una entrada se ve así:

```json
{
  "mcpServers": {
    "mail-archiver": {
      "url": "https://archive.example.com/mcp",
      "headers": {
        "Authorization": "Bearer ma_<your-api-key>"
      }
    }
  }
}
```

## Guía de seguridad para la exposición a internet {#security-guidance-for-internet-exposure}

El servidor MCP está diseñado de tal manera que, con los valores predeterminados y el endurecimiento detallado a continuación, el correo en Archivo no puede salir del sistema sin una clave API válida por usuario que haya sido emitida explícitamente a una Cuenta. Los puntos siguientes son una **lectura obligatoria antes de exponer `/mcp` a internet**.

### Obligatorio (bloquear explotación / exfiltración de datos) {#mandatory-block-exploitation-data-exfiltration}

1. **Solo HTTPS.** Termine el TLS en el borde (proxy inverso). Una clave API de portador (bearer) a través de HTTP sin cifrar puede ser interceptada y reutilizada.
2. **Mantenga `Mcp:Enabled=false` a menos que realmente lo use.** Cuando está desactivado, `/mcp` devuelve `404` y no se puede alcanzar ningún flujo de código; este es el valor predeterminado seguro y el estado seguro para implementaciones que no necesitan acceso de agentes de IA.
3. **Se aplica un límite de tasa (rate limiting) en el endpoint.** La política `"Mcp"` (`Mcp:RateLimitPerMinute`, predeterminado `120`) se aplica en el endpoint `/mcp` mapeado. Se particiona por el prefijo de la clave API no secreto (los primeros 11 caracteres del token `ma_...`) o la IP del cliente cuando no hay un token de portador presente. **Reduzca `RateLimitPerMinute`** para la exposición a internet (por ejemplo, `30`–`60`); las llamadas a herramientas de MCP son más pesadas que las llamadas REST y el endpoint no debe dejarse con un presupuesto alto cuando clientes no confiables pueden alcanzarlo.
4. **El `X-Forwarded-For` de proxy inverso debe ser verificado con cuidado.** El limitador de tasa recurre a `RemoteIpAddress`, que bajo un proxy es la IP del proxy a menos que se le proporcione a `UseForwardedHeaders` la IP real del cliente. Asegúrese de que su proxy configure `X-Forwarded-For` correctamente y que solo su proxy pueda configurarlo; de lo contrario, un atacante puede suplantar la partición del límite de tasa.
5. **No coloque `/mcp` detrás de una caché de CDN.** Las respuestas de MCP son JSON-RPC por solicitud y nunca deben almacenarse en caché; el almacenamiento en caché rompería el protocolo y filtraría el resultado de un usuario a otro. Marque `/mcp` como no almacenable en caché en el borde (`Cache-Control: no-store`).

### Higiene de la clave API (bloquear acceso no autorizado) {#api-key-hygiene-block-unauthorized-access}

6. **Emita claves dedicadas y de corta duración para los agentes MCP.** Una única clave `ma_...` es válida tanto para `/api` como para `/mcp`; hoy en día no existe un alcance por protocolo. Por lo tanto:
   - Cree una **clave separada por agente** (no reutilice una clave de script de informes para un agente de LLM externo).
   - Establezca siempre un `ExpiresAt` corto y rótelo; revóquela inmediatamente cuando el agente sea retirado.
   - Prefiera claves propiedad de un **usuario restringido (no administrador)** cuyas únicas Cuentas asignadas sean las que el agente necesita. Una clave de administrador otorga visibilidad sobre *todas* las cuentas de correo; una clave restringida limita el radio de impacto.
7. **Trate la clave API como una contraseña.** Es una credencial de portador. Nunca la incruste en código del lado del cliente, nunca la registre en logs, nunca la incluya en un repositorio. Colóquela en el almacén de secretos del cliente MCP y rótela ante cualquier sospecha de compromiso.
8. **Audite en la página de Logs.** Cada llamada a una herramienta se registra en `AccessLogs` (`Search`, `Open`, `Download`) bajo el usuario propietario. Revise periódicamente la actividad de MCP en busca de patrones inesperados (picos de volumen, lecturas fuera de horario, descargas masivas).

### Modelo de confianza para los datos devueltos (bloquear daños derivados) {#trust-model-for-returned-data-block-downstream-harm}

9. **Los cuerpos de los correos electrónicos y los Adjuntos son contenido no confiable**, exactamente igual que cuando llegaron al Buzón. Pueden contener malware, enlaces de phishing o HTML/JavaScript activo.
   - `get_email` devuelve `htmlBody` **en bruto y sin sanear**. **No** lo renderice para un humano sin sanearlo (DOMPurify / `HtmlSanitizer` / un sandbox). Renderizar HTML en Archivo sin sanear es un vector de XSS almacenado.
   - `get_attachment` devuelve los bytes en bruto con el `contentType` original. **No** abra ni ejecute automáticamente los Adjuntos; trate el campo `contentType` como metadatos no confiables, no como una instrucción segura.
   - `get_email_eml` y `get_email_raw` devuelven el mensaje completo incluyendo cada Adjunto. Las mismas reglas se aplican a cada una de sus partes.
10. **No permita que el agente realice acciones de escritura basadas en el contenido del correo sin confirmación.** Un correo malicioso puede contener texto de inyección de prompts dirigido al LLM que lo consume. Aplique Permisos y confirmación en la propia lógica del agente; el prompt del sistema no es un límite de seguridad.

### Recomendaciones de defensa en profundidad {#defense-in-depth-recommendations}

11. **Restrinja `/mcp` en la capa de red** cuando sea factible (por ejemplo, permita solo la IP / rango de VPN del host del agente en el proxy inverso), además de la autenticación por clave API. Este es el control más efectivo para la exposición a internet.
12. **Reduzca `Mcp:AllowAttachmentDownloads` a `false`** si sus agentes no necesitan los bytes del Adjunto; la exfiltración de Adjuntos es la ruta de datos de mayor volumen. Los metadatos (nombre de archivo, tipo de contenido, tamaño) se siguen devolviendo a través de `get_email`.
13. **Reduzca `Mcp:MaxAttachmentBytes`** al valor más pequeño que sus agentes realmente necesiten (predeterminado `10000000` ≈ 10 MB). Esto limita la respuesta individual más grande que una clave API puede extraer por solicitud. Lo mismo ocurre con `Mcp:MaxMessageBytes`, o establezca `Mcp:AllowMessageDownloads` en `false` cuando los agentes no necesiten mensajes completos.
14. **Reduzca `Mcp:MaxResults` y `Mcp:DefaultPageSize`** para limitar el resultado de búsqueda más grande que una clave API puede extraer por solicitud (predeterminado `100` / `20`).
15. **Supervise `AccessLogs` y los rechazos por límite de tasa** (`429`) de forma operativa. Un pico repentino desde un único prefijo de clave es una señal para revocar esa clave.
