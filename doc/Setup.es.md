# 🛠️ Guía de configuración de Mail Archiver {#mail-archiver-setup-guide}

[← Volver al índice de documentación](index.md)

## 📋 Descripción general {#overview}

Esta guía proporciona instrucciones detalladas para configurar la aplicación Mail Archiver utilizando Docker Compose.

## 🛠️ Requisitos previos {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Pasos de instalación {#installation-steps}

1. Instale los prerrequisitos en su sistema.

2. Cree un archivo `docker-compose.yml` con el siguiente contenido:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey;

      # Authentication Settings
      - Authentication__Username=admin
      - Authentication__Password=secure123!
      - Authentication__SessionTimeoutMinutes=60
      - Authentication__CookieName=MailArchiverAuth
      - Authentication__CookieSameSite=Lax

      # REST API Settings (read-only, disabled by default)
      - Api__Enabled=false
      - Api__AllowAttachmentDownloads=true
      - Api__AllowMessageDownloads=true
      - Api__EnableSwaggerUi=true
      - Api__DefaultPageSize=20
      - Api__MaxPageSize=100
      - Api__RateLimitPerMinute=120

      # MCP Server Settings (Model Context Protocol for AI agents, read-only, disabled by default)
      - Mcp__Enabled=false
      - Mcp__AllowAttachmentDownloads=true
      - Mcp__DefaultPageSize=20
      - Mcp__MaxResults=100
      - Mcp__MaxAttachmentBytes=10000000
      - Mcp__AllowMessageDownloads=true
      - Mcp__MaxMessageBytes=10000000
      - Mcp__RateLimitPerMinute=120

      # MailSync Settings
      - MailSync__IntervalMinutes=15
      - MailSync__TimeoutMinutes=0
      - MailSync__ConnectionTimeoutSeconds=180
      - MailSync__CommandTimeoutSeconds=300
      - MailSync__AlwaysForceFullSync=false
      - MailSync__IgnoreSelfSignedCert=false
      - MailSync__MaxConcurrentSyncs=1
      - MailSync__InterAccountDelaySeconds=0
      - MailSync__MaxIssuesPerKind=20
      - MailSync__FullSyncIntervalHours=24
      - MailSync__ExcludeSubfolders=false
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Contacts

      # Original Message Storage (complete .eml per email, disabled by default)
      - RawMessages__Enabled=false
      - RawMessages__Storage=postgres
      - RawMessages__MaxMessageSizeMb=50
      - RawMessages__IncludeGraph=true
      - RawMessages__FileSystem__BasePath=RawMessages
      - RawMessages__FileSystem__ShardDepth=2
      - RawMessages__FileSystem__VerifyChecksumOnRead=true

      # BatchRestore Settings
      - BatchRestore__AsyncThreshold=50
      - BatchRestore__MaxSyncEmails=150
      - BatchRestore__MaxAsyncEmails=50000
      - BatchRestore__SessionTimeoutMinutes=30
      - BatchRestore__DefaultBatchSize=50

      # Tenant Management Settings
      - TenantManagement__MaxSelectedMailboxes=1000

      # Mailcow Settings
      - Mailcow__MaxSelectedMailboxes=1000

      # BatchOperation Settings
      - BatchOperation__BatchSize=50
      - BatchOperation__PauseBetweenEmailsMs=50
      - BatchOperation__PauseBetweenBatchesMs=250

      # Bandwidth Tracking Settings (for IMAP rate limit handling)
      - BandwidthTracking__Enabled=false
      - BandwidthTracking__DailyLimitMb=25000
      - BandwidthTracking__WarningThresholdPercent=80
      - BandwidthTracking__PauseHoursOnLimit=24
      - BandwidthTracking__TrackUploadBytes=false

      # Selection Settings
      - Selection__MaxSelectableEmails=250

      # View Settings (Privacy & Display)
      - View__DefaultToPlainText=true
      - View__BlockExternalResources=false

      # Dashboard Settings (statistics cache, counter parts, chart periods)
      - Dashboard__CacheSeconds=60
      - Dashboard__RefreshIntervalMinutes=15
      - Dashboard__ShowDirectionSplits=true
      - Dashboard__SelectablePeriods=true

      # Jobs Settings (background jobs page)
      - Jobs__RefreshSeconds=30

      # Npgsql Settings
      - Npgsql__CommandTimeout=900

      # Upload Settings for MBox and EML files
      - Upload__MaxFileSizeGB=10
      - Upload__KeepAliveTimeoutHours=4
      - Upload__RequestHeadersTimeoutHours=2

      # Local Import Settings (for CLI imports from mounted volumes)
      - LocalImport__AllowedPaths__0=/data/import

      # CSV Import Settings (for bulk IMAP account import)
      - CsvImport__MaxRows=5000
      - CsvImport__MaxFileSizeBytes=10000000

      # Deletion Policy Settings (Optional - controls whether email deletion is allowed)
      - DeletionPolicy__DeletionAllowed=true

      # TimeZone Settings
      - TimeZone__DisplayTimeZoneId=Etc/UCT

      # Database Maintenance Settings (Optional)
      - DatabaseMaintenance__Enabled=false
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30

      # Attachment Deduplication Settings (Optional - feature is always on)
      - AttachmentDeduplication__BatchSize=200
      - AttachmentDeduplication__DelayBetweenBatchesMs=0
      - AttachmentDeduplication__StartupDelaySeconds=20
      - AttachmentDeduplication__OrphanCleanupIntervalHours=12
      - AttachmentDeduplication__CommandTimeoutSeconds=300

      # Account Storage Settings (Optional - per-account storage display)
      - AccountStorage__Enabled=true
      - AccountStorage__DailyExecutionTime=02:30
      - AccountStorage__BackfillDelayMs=5000
      - AccountStorage__RefreshBatchDelayMs=1000
      - AccountStorage__CommandTimeoutSeconds=300

      # ReleaseNotes Settings (Version Update Splash Screen)
      - ReleaseNotes__Enabled=true
      - ReleaseNotes__UpdateHintEnabled=true
      - ReleaseNotes__UpdateCheckIntervalHours=6

      # Logging Settings (Optional - defaults to Information level)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning

      # Security Settings
      - AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com

      # OIDC Configuration (see OIDC_Implementation.md for detailed setup)
      - OAuth__Enabled=true
      - OAuth__Authority=https://example.com
      - OAuth__ClientId=YOUR-CLIENT-ID
      - OAuth__ClientSecret=YOUR-CLIENT-SECRET
      - OAuth__DisplayName=PocketID SSO
      - OAuth__ClientScopes__0=openid
      - OAuth__ClientScopes__1=profile
      - OAuth__ClientScopes__2=email
      - OAuth__DisablePasswordLogin=false
      - OAuth__AutoRedirect=false
      - OAuth__AutoApproveUsers=false
      - OAuth__AdminEmails__0=admin@example.com
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      # Uncomment the following line to mount a directory for local file imports
      # (also configure LocalImport__AllowedPaths__0=/data/import in environment variables)
      # - /path/to/your/mbox/files:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
      # Only with RawMessages__Storage=filesystem: keep the original messages outside the
      # container, otherwise they are lost when the container is recreated.
      # - ./raw-messages:/app/RawMessages
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

networks:
  postgres:
```

3. Edite la configuración de la base de datos en `docker-compose.yml` y establezca una contraseña segura en la variable `POSTGRES_PASSWORD` y en `ConnectionString`.

4. Si desea utilizar la autenticación (lo cual se recomienda encarecidamente), defina un `Authentication__Username` y `Authentication__Password` que se utilizará para el usuario administrador.

5. Ajuste la variable de entorno `TimeZone__DisplayTimeZoneId` para que coincida con su zona horaria preferida (por defecto es "Etc/UCT"). Puede utilizar cualquier identificador de zona horaria IANA (por ejemplo, "Europe/Berlin", "Asia/Tokyo").

6. Opcionalmente, configure las variables de entorno `Logging__LogLevel` para controlar la verbosidad de los registros de la aplicación. Consulte la sección de Configuración de Logging a continuación para conocer las opciones disponibles.

7. Configure un proxy inverso de su elección con https y autenticación para asegurar el acceso a la aplicación. 

> ⚠️ **Atención**: ¡La aplicación en sí no proporciona acceso cifrado a través de https! ¡Debe configurarse a través de un proxy inverso!

8. Inicio inicial de los contenedores:
```bash
docker compose up -d
```

9. Reiniciar contenedores:
```bash
docker compose restart
```

10. Acceder a la aplicación

11. Inicie sesión con sus credenciales definidas y añada su primera Cuenta de correo electrónico:
   - Navegue a la sección "Email Accounts"
   - Haga clic en "New Account"
   - Introduzca los detalles de su servidor y sus credenciales
   - ¡Guarde y comience el Archivo!
   - Si lo desea, cree otros usuarios y asigne Cuentas.

## 📚 Explicaciones de las variables de entorno {#environment-variable-explanations}

### 🗄️ Conexión a la base de datos {#database-connection}
- `ConnectionStrings__DefaultConnection`: La cadena de conexión a la base de datos PostgreSQL. Modifique los valores `Host`, `Database`, `Username` y `Password` según sea necesario.

### 🔐 Configuración de Autenticación {#authentication-settings}
- `Authentication__Username`: El nombre de usuario para la Cuenta de administrador.
- `Authentication__Password`: La contraseña para la Cuenta de administrador.
- `Authentication__SessionTimeoutMinutes`: El tiempo de espera de la sesión en minutos. Esto se aplica a la cookie de inicio de sesión no persistente; los usuarios que marquen **Remember Me** en el formulario de inicio de sesión obtienen una cookie persistente válida por un periodo fijo de 7 días en su lugar. Consulte [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: El nombre de la cookie de autenticación.
- `Authentication__CookieSameSite`: Configura el atributo SameSite para las cookies de autenticación, de sesión y de protección CSRF. Los valores válidos son:
  - `Strict` (predeterminado): Seguridad máxima. Las cookies solo se envían con solicitudes del mismo sitio. Esto puede causar problemas al navegar hacia la aplicación desde enlaces externos (por ejemplo, al hacer clic en un enlace desde otro sitio web), ya que la sesión existente no será reconocida.
  - `Lax`: Recomendado cuando se utiliza un proxy inverso. Las cookies se envían con navegaciones de nivel superior y solicitudes del mismo sitio, lo que permite a los usuarios seguir enlaces externos hacia la aplicación manteniendo la protección CSRF para las solicitudes POST.
  - `None`: Las cookies se envían con todas las solicitudes. Requiere HTTPS y el atributo `Secure`. Utilice esto solo si tiene requisitos específicos de sitios cruzados y comprende las implicaciones de seguridad.

### 🔌 REST API Settings {#rest-api-settings}
La REST API de solo lectura opcional está **desactivada por defecto**. Consulte la [REST API guide](API.md) para obtener la referencia completa.
- `Api__Enabled`: Interruptor maestro para la REST API de solo lectura (por defecto `false`). Cuando `false`, todas las rutas `/api/*` devuelven `404`.
- `Api__AllowAttachmentDownloads`: Permite la descarga de bytes de Adjunto a través de la API (por defecto `true`). Cuando `false`, el endpoint de Adjunto devuelve `403`.
- `Api__AllowMessageDownloads`: Permite la descarga de mensajes completos a través de la API, tanto el `/eml` reconstruido como el `/raw` original (por defecto `true`). Cuando `false`, ambos endpoints devuelven `403`. Un mensaje completo incluye sus Adjuntos, por lo que **tanto** este interruptor como `Api__AllowAttachmentDownloads` deben estar en `true`; desactivar cualquiera de los dos desactiva ambos endpoints.
- `Api__EnableSwaggerUi`: Expone Swagger UI en `/apidocs` y el documento OpenAPI en `/apidocs/spec/v1.json` (por defecto `true`). Ambos requieren una sesión de navegador iniciada.
- `Api__DefaultPageSize`: Tamaño de página por defecto para los endpoints de lista cuando no se especifica (por defecto `20`).
- `Api__MaxPageSize`: Tamaño de página máximo permitido; las solicitudes más grandes se limitan (por defecto `100`).
- `Api__RateLimitPerMinute`: Presupuesto de solicitudes de ventana fija por clave de API por minuto (por defecto `120`).

### 🤖 Configuración del servidor MCP {#mcp-server-settings}
El servidor MCP (Model Context Protocol) opcional expone el mismo acceso de solo lectura al correo que la REST API a los agentes de IA a través del transporte Streamable HTTP. Está **desactivado por defecto**. Consulte la [guía MCP](MCP.md) para obtener la referencia completa. MCP reutiliza exactamente las mismas claves de API que la REST API (tokens de portador `ma_...`) y el mismo alcance de acceso a la Cuenta por usuario.
- `Mcp__Enabled`: Interruptor maestro para el servidor MCP (por defecto `false`). Cuando `false`, el endpoint `/mcp` devuelve `404`.
- `Mcp__AllowAttachmentDownloads`: Permite que la herramienta `get_attachment` devuelva bytes de Adjunto (por defecto `true`). Cuando `false`, la herramienta devuelve un error.
- `Mcp__DefaultPageSize`: Tamaño de página por defecto utilizado por `search_emails` cuando el llamador omite `pageSize` (por defecto `20`).
- `Mcp__MaxResults`: Límite superior para el tamaño de página de `search_emails`; los valores más grandes se ajustan (por defecto `100`).
- `Mcp__MaxAttachmentBytes`: Tamaño máximo de Adjunto (en bytes) que la herramienta `get_attachment` devolverá en línea como base64; los Adjuntos más grandes son rechazados (por defecto `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Permite que las herramientas `get_email_eml` y `get_email_raw` devuelvan mensajes completos (por defecto `true`). Cuando `false`, las herramientas devuelven un error. Un mensaje completo incluye sus Adjuntos, por lo que **tanto** este interruptor como `Mcp__AllowAttachmentDownloads` deben estar en `true`; desactivar cualquiera de los dos desactiva ambas herramientas.
- `Mcp__MaxMessageBytes`: Tamaño máximo de mensaje (en bytes) que las herramientas `get_email_eml` y `get_email_raw` devolverán en línea; los mensajes más grandes son rechazados (por defecto `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Presupuesto de solicitudes de ventana fija por clave de API por minuto (por defecto `120`).

### 📨 MailSync Settings {#mailsync-settings}
- `MailSync__IntervalMinutes`: El intervalo en minutos entre la Sincronización de correos electrónicos. Este es el valor predeterminado global; cada Cuenta puede sobrescribirlo individualmente desde la página de Crear/Editar (deje vacío para usar este valor predeterminado).
- `MailSync__FullSyncIntervalHours`: Valor predeterminado global opcional para resincronizaciones completas automáticas, en horas. Cuando no se establece (el valor predeterminado), no se ejecuta ninguna sincronización completa automática a menos que se establezca un valor `FullSyncIntervalHours` por Cuenta en la página de Crear/Editar. Los valores por Cuenta sobrescriben este valor predeterminado global.
- `MailSync__TimeoutMinutes`: Tiempo de espera de sincronización por Cuenta en minutos. Una sincronización que se ejecute durante más tiempo se detiene en el siguiente límite de mensaje, mantiene sus puntos de control y se reanuda en la siguiente ejecución; el trabajo se informa como `Timed Out` y `LastSync` no avanza. Se aplica solo a las sincronizaciones programadas; una sincronización iniciada desde la interfaz de usuario (sincronización manual o resincronización completa) se ejecuta sin tiempo de espera. `0` (el valor predeterminado) o cualquier valor no positivo significa que no hay tiempo de espera. Revise el valor antes de actualizar si sincroniza Buzones grandes. Consulte [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: El tiempo de espera de conexión para las conexiones IMAP en segundos.
- `MailSync__CommandTimeoutSeconds`: El tiempo de espera de comando para los comandos IMAP en segundos.
- `MailSync__AlwaysForceFullSync`: Si se debe forzar siempre una sincronización completa (true/false).
- `MailSync__IgnoreSelfSignedCert`: Si se deben ignorar los certificados autofirmados (true/false).
- `MailSync__MaxConcurrentSyncs`: Cuántas sincronizaciones de Cuenta pueden ejecutarse al mismo tiempo. Predeterminado `1` (secuencial, compatible con versiones anteriores). El programador rellena los espacios a medida que quedan libres en lugar de esperar a un lote completo, por lo que un Buzón lento ya no detiene a los demás. Aumente para sincronizar múltiples Cuentas simultáneamente; tenga en cuenta los límites de velocidad del proveedor y el uso de recursos locales. Consulte [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Retraso de escalonamiento opcional en segundos aplicado al final de cada tarea de sincronización de Cuenta. Predeterminado `0` (sin retraso). Útil para evitar arranques en ráfaga cuando `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Cuántos problemas de cada tipo recuerda un trabajo de sincronización para la página de la Cuenta. Las Carpetas fallidas, las Carpetas faltantes y los mensajes fallidos se presupuestan por separado, por lo que una inundación de un tipo no puede desplazar a los demás de la vista; cualquier cosa que supere el presupuesto se cuenta en lugar de guardarse. Predeterminado `20`. `0` desactiva el detalle y deja solo los contadores. Consulte [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Carpetas excluidas de la Sincronización para **cada** Cuenta, además de la propia lista de carpetas excluidas de cada Cuenta. Vacío por defecto, por lo que las configuraciones existentes no se ven afectadas. Las dos listas son **aditivas** (una Carpeta se omite cuando coincide con cualquiera de las dos) y ambas utilizan las mismas reglas de coincidencia: coincidencia exacta en la ruta completa, coincidencia exacta en el nombre de la Carpeta, una coincidencia de sufijo de ruta (por lo que `Drafts` también coincide con `INBOX/Drafts` y `INBOX.Drafts`), y todo lo que hay debajo de la Carpeta que una entrada nombra cuando `MailSync__ExcludeSubfolders` está activado. La coincidencia no distingue entre mayúsculas y minúsculas. Útil al importar muchos Buzones desde el mismo servidor, donde la alternativa es mantener una lista de exclusión idéntica en cada Cuenta. Ninguna Carpeta se excluye por defecto; qué nombres vale la pena enumerar depende del servidor y su idioma. Ejemplo para un árbol de Buzón que también contiene Carpetas de calendario y contactos:
  ```yaml
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Calendars
      - MailSync__GlobalExcludedFolders__2=Contacts
      - MailSync__GlobalExcludedFolders__3=AddressBook
      - MailSync__GlobalExcludedFolders__4=Tasks
      - MailSync__GlobalExcludedFolders__5=TaskList
      - MailSync__GlobalExcludedFolders__6=Notes
      - MailSync__GlobalExcludedFolders__7=NoteList
      - MailSync__GlobalExcludedFolders__8=Journal
  ```
- `MailSync__ExcludeSubfolders`: Si una entrada de exclusión también cubre las Carpetas debajo de la que nombra. Se aplica tanto a la lista por Cuenta como a la de toda la instalación, y a ambos proveedores. Predeterminado `false`, coincidiendo con el comportamiento antes de que existiera la opción: una entrada `Deleted Items` coincide solo con esa Carpeta, y un árbol se mantiene fuera del Archivo nombrando su raíz y cada Carpeta debajo de ella. Establezca en `true` para que una entrada tome también el árbol de abajo; una entrada `Deleted Items` entonces también mantiene `Deleted Items/2024` y el resto de esa rama fuera del Archivo. La regla se ancla en el separador de ruta, por lo que toma Carpetas completas y nunca parte de un nombre: `Archive` cubre `Archive` y `Archive/Team`, y deja intacta una Carpeta de correo llamada `Team Archive Review`. Un `.` también cuenta como separador, de la misma manera que la regla de sufijo de ruta lo trata.

### 📧 Configuración de Almacenamiento del Mensaje Original {#original-message-storage-settings}

Por defecto, el Archivo mantiene solo las partes analizadas de un correo electrónico, por lo que un `.eml` exportado es
una reconstrucción: las codificaciones de transferencia, los límites MIME y el plegado de encabezados difieren del
mensaje tal como se recibió, y las firmas DKIM ya no se verifican. Con `RawMessages:Enabled` el
original completo también se almacena.

| Parámetro | Por defecto | Propósito |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Almacenar el original de cada correo electrónico recién archivado. |
| `RawMessages__Storage` | `postgres` | Dónde residen los bytes: `postgres` o `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Los mensajes más grandes se archivan sin su original. `0` = sin límite. |
| `RawMessages__IncludeGraph` | `true` | También almacenar originales para Cuentas de Microsoft 365 (Graph). |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Directorio para los archivos de mensajes. Debe apuntar a un volumen montado en Docker. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Niveles de directorio en los que se distribuye el id del correo electrónico. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Verificar el SHA-256 en cada descarga. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Eliminar archivos de mensajes a los que ningún correo electrónico haga referencia. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Con qué frecuencia se ejecuta esa limpieza. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Los archivos más jóvenes que esto nunca se eliminan. |

!!! warning "El backend del sistema de archivos necesita un volumen montado"
    Con `RawMessages__Storage=filesystem` los archivos de mensajes residen fuera de la base de datos.
    Sin un bind mount, se encuentran en la capa escribible del contenedor y se pierden en la
    siguiente recreación del contenedor, mientras que las filas de la base de datos sobreviven. Realice una Copia de seguridad del directorio
    junto con el volcado de la base de datos.

Consulte **[Original Message Storage](RawMessages.md)** para obtener la descripción completa: dónde se sirve el
original, los backends de almacenamiento, cómo mover mensajes existentes entre ellos con
`--migrate-raw-storage`, y qué realizar una Copia de seguridad.

### 📤 Configuración de BatchRestore {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: El número de correos electrónicos que activa el procesamiento asíncrono.
- `BatchRestore__MaxSyncEmails`: El número máximo de correos electrónicos para el procesamiento sincrónico.
- `BatchRestore__MaxAsyncEmails`: El número máximo de correos electrónicos para el procesamiento asíncrono.
- `BatchRestore__SessionTimeoutMinutes`: El tiempo de espera de la sesión para la Restauración por lotes en minutos.
- `BatchRestore__DefaultBatchSize`: El tamaño de lote predeterminado para las operaciones de correo electrónico.

### 🏢 Configuración de la gestión de inquilinos {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Número máximo de Buzones que se pueden añadir en una única operación de gestión de inquilinos. El valor predeterminado es `1000`. Aumente este valor para inquilinos muy grandes, o redúzcalo para evitar importaciones masivas accidentales. Cuando se supera el límite, la operación se rechaza con un error de validación y no se crean ninguna Cuenta. Consulte la [M365 Tenant Import Guide](M365TenantImport.md) para obtener más detalles.

### 🐄 Configuración de Mailcow {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Número máximo de buzones que se pueden aprovisionar en una única ejecución de activación de Mailcow. El valor predeterminado es `1000`. Redúzcalo para evitar el aprovisionamiento masivo accidental, o auméntelo para instancias grandes. Cuando se supera el límite, no se crean Cuentas. Consulte [Mailcow Instances](Mailcow.md) para más detalles.
- La integración con Mailcow requiere adicionalmente `Security__CredentialEncryptionKey` (consulte [Security Settings](#-security-settings)). Tanto la conexión de una instancia como la activación de buzones son rechazadas mientras el cifrado de credenciales esté desactivado, por lo que la clave API y las contraseñas de aplicación generadas nunca podrán almacenarse en texto plano.
- Se requiere acceso de salida a la API HTTPS de Mailcow y al host IMAP a través de TLS (normalmente el puerto `993`).

### 📦 Configuración de BatchOperation {#batchoperation-settings}
- `BatchOperation__BatchSize`: El tamaño del lote para las operaciones de correo electrónico.
- `BatchOperation__PauseBetweenEmailsMs`: La pausa entre correos electrónicos individuales en milisegundos.
- `BatchOperation__PauseBetweenBatchesMs`: La pausa entre lotes en milisegundos.

### 📤 Configuración de Offload {#offload-settings}
Configuración para el [date-windowed offload](Offload.md). Todos los valores predeterminados reproducen el comportamiento que tenía la aplicación antes de que existiera la función, por lo que una instalación que no configure esta sección no se verá afectada.
- `Offload__MaxConcurrentJobs`: Cuántos trabajos de restauración o offload pueden ejecutarse al mismo tiempo. El valor predeterminado es `1`, lo que mantiene el procesamiento estrictamente serial que la cola de trabajos siempre ha utilizado. Se ejecuta como máximo un trabajo por cada Buzón de destino, independientemente de este valor.
- `Offload__PrefetchMaxMessages`: Límite superior de cuántos mensajes se indexan de un Buzón de destino para la detección de duplicados. El valor predeterminado es `500000`. Por encima de este valor, la comprobación se reduce a una única Carpeta y se registra que el alcance se ha reducido.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Carpetas de origen que nunca se pasan a offload, se comparan antes de renombrar y cubren subcarpetas. Vacío por defecto.
- `Offload__FolderRenameMap__<SourceFolder>`: Reescribe los segmentos iniciales de la ruta de una Carpeta de origen, por ejemplo `Offload__FolderRenameMap__Sent Items=Sent`. Vacío por defecto.
- `Offload__MarkAsSeen`: Si el correo adjunto se marca como leído. El valor predeterminado es `true`, coincidiendo con el comportamiento de restauración existente.

Ambas configuraciones de Carpeta se envían vacías a propósito: reescribir o descartar carpetas sin que se solicite sorprendería a cualquiera que ya esté utilizando la ruta de restauración. Por lo tanto, una ejecución sin configuración migra todo, incluidas las carpetas de spam, y crea un segundo conjunto de carpetas especiales junto a las propias del destino.

### 📊 Configuración de Seguimiento de Ancho de Banda {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Habilitar o deshabilitar el seguimiento del ancho de banda para el manejo de límites de velocidad de IMAP (true/false). El valor predeterminado es `false`. Cuando está habilitado, el sistema realiza un seguimiento del uso del ancho de banda por Cuenta y puede pausar la Sincronización cuando se alcanzan los límites del proveedor. Consulte [Rate Limit Handling](RateLimitHandling.md) para obtener información detallada.
- `BandwidthTracking__DailyLimitMb`: Límite diario de descarga en megabytes por Cuenta. El valor predeterminado es `25000` (25 GB). Para proveedores con límites de ancho de banda, establezca esto para que coincida con su límite de velocidad (p. ej., `2500` para proveedores con límites diarios de ~2500 MB). El sistema pausará la sincronización cuando se alcance este límite.
- `BandwidthTracking__WarningThresholdPercent`: Porcentaje del límite diario en el cual se registran los mensajes de advertencia. El valor predeterminado es `80`. Cuando el uso del ancho de banda alcanza este porcentaje, se registran advertencias para ayudar a monitorear los límites próximos a alcanzarse.
- `BandwidthTracking__PauseHoursOnLimit`: Número de horas para pausar la Sincronización cuando se alcanza el límite diario. El valor predeterminado es `24`. Después de este período, la bandera de límite se limpia automáticamente y la sincronización se reanuda.
- `BandwidthTracking__TrackUploadBytes`: Si también se debe realizar el seguimiento del ancho de banda de carga (true/false). El valor predeterminado es `false`. La mayoría de los proveedores de IMAP solo limitan las descargas, por lo que normalmente esto no es necesario.

### 🎯 Configuración de selección {#selection-settings}
- `Selection__MaxSelectableEmails`: El número máximo de correos electrónicos que se pueden seleccionar a la vez.

### 👁️ View Settings (Privacy & Display) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Controla el modo de vista de correo electrónico predeterminado para la privacidad y la prevención de rastreo (true/false). El valor predeterminado es `false`.
  - Cuando se establece en `true`: Los correos electrónicos se abren en vista de texto plano de forma predeterminada, lo que evita la carga automática de píxeles de seguimiento, imágenes externas y balizas web. Esto se recomienda para usuarios preocupados por el rastreo de correo electrónico y la privacidad.
  - Cuando se establece en `false`: Los correos electrónicos se abren en vista HTML de forma predeterminada, mostrando el correo electrónico con formato, imágenes y estilos.
  - Los usuarios pueden alternar entre las vistas HTML y de texto plano utilizando un botón en la página de detalles del correo electrónico (solo visible cuando ambos formatos están disponibles).
  - El enlace "Full View" respeta el modo de vista seleccionado actualmente.

- `View__BlockExternalResources`: Bloquea recursos externos (imágenes remotas, CSS externo, scripts externos, fuentes web, etc.) en las vistas de correo electrónico HTML para evitar el rastreo y mejorar la privacidad (true/false). El valor predeterminado es `false`.
  - Cuando se establece en `true`: Los recursos externos se filtran al mostrar correos electrónicos HTML. Solo se muestran el contenido en línea y las URIs de datos (incluyendo los Adjuntos en línea a través de referencias `cid:`).
  - Cuando se establece en `false`: Los correos electrónicos HTML se muestran con todos sus recursos externos originales.
  - **Importante**: Este ajuste solo afecta a la **visualización** del correo electrónico. Los correos electrónicos archivados se almacenan completamente sin cambios en la base de datos, preservando todo el contenido original.
  - Los recursos bloqueados incluyen:
    - Imágenes remotas (píxeles de seguimiento, imágenes externas alojadas en servidores)
    - Hojas de estilo CSS externas
    - Fuentes externas a través de @font-face
    - Importaciones de CSS externas a través de @import
    - Imágenes de fondo externas
  - Recursos permitidos:
    - Imágenes en línea incrustadas como data: URIs
    - Adjuntos en línea referenciados a través de cid: URIs
    - Estilos CSS en línea y etiquetas de estilo
  - Este ajuste funciona independientemente de `DefaultToPlainText` y proporciona una capa adicional de protección de privacidad al visualizar correos electrónicos HTML.

### 📊 Configuración del Dashboard {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: Si las tarjetas de contador incluyen sus partes entrantes y salientes, y si la tarjeta de Cuenta incluye el número de dominios distintos. El valor predeterminado es `true`. Establezca en `false` y esos números no se calcularán en lugar de calcularse y ocultarse: los contadores leerán un recuento simple, y el recuento de Adjunto dejará de unirse al correo al que se vincula, que es la única consulta que estas partes añaden.
- `Dashboard__SelectablePeriods`: Si los gráficos del dashboard ofrecen una resolución, un periodo y flechas para moverlo. El valor predeterminado es `true`. Establezca en `false` y los gráficos mostrarán los últimos doce meses por mes y los remitentes de todo el Archivo; el endpoint del gráfico responde como si no existiera, y no se busca la fecha de envío más antigua.
- `Dashboard__CacheSeconds`: Cuánto tiempo se mantienen las estadísticas calculadas del dashboard (totales con sus desgloses de entrada y salida, recuentos por Cuenta, la serie del gráfico, principales remitentes, correos recientes, tamaño de la base de datos) en la caché en memoria del servidor. El valor predeterminado es `60` segundos. Establezca en `0` para desactivar la caché y recalcular siempre las estadísticas. Cada selección de resolución y periodo del gráfico se almacena en su propia caché, por lo que un dashboard que alterna entre selecciones mantiene una entrada por cada selección. Los valores más altos reducen la carga de la base de datos en entornos grandes a costa de tener números más desactualizados. Los indicadores de estado de Sincronización, los valores de almacenamiento y la lista de correos recientes siempre se obtienen en tiempo real y no se ven afectados por esta caché.
- `Dashboard__RefreshIntervalMinutes`: Con qué frecuencia el servicio en segundo plano `DashboardStatsRefreshService` recalcula los agregados costosos del dashboard (recuentos totales con sus desgloses de entrada y salida, recuentos por Cuenta, el número de dominios de Cuenta, la serie de gráficos predeterminada con sus principales remitentes, tamaño de la base de datos) en la tabla de la base de datos `DashboardStatsCache`. En instalaciones con varios millones de correos, el dashboard lee estos valores preparados en lugar de agregar todo el Archivo en la ruta de la solicitud, lo que convierte una primera carga de ~10 segundos en milisegundos. El endpoint del gráfico responde a la selección predeterminada (meses durante un año, remitentes entrantes, el periodo actual) desde la misma fila, por lo que al volver a ella se muestran los números de la primera carga; cualquier otra selección se calcula al solicitarla y se mantiene en la caché en memoria mencionada anteriormente. El valor predeterminado es `15` minutos. Establezca en `0` para desactivar la actualización en segundo plano; entonces el dashboard volverá a calcular las estadísticas en tiempo real (aplicando aún la caché en memoria anterior). Los valores mostrados pueden tener un retraso de hasta el intervalo configurado respecto al estado real del Archivo. Una fila calculada con valores `ShowDirectionSplits` o `SelectablePeriods` distintos a los actuales no se utiliza; el dashboard calcula en tiempo real hasta la próxima actualización.

### 🔄 Configuración de Jobs {#jobs-settings}
- `Jobs__RefreshSeconds`: Con qué frecuencia se recarga la página de jobs en segundo plano mientras una pestaña del navegador la tiene abierta. El valor predeterminado es `30` segundos. Establezca en `0` para desactivar la recarga automática y actualizar manualmente. Cada recarga reconstruye la página a partir de las ocho fuentes de job, por lo que en instalaciones con muchas Cuenta, un intervalo más largo reduce la carga, multiplicado por cada pestaña que esté abierta en la página.

### 🗃️ Configuración de Npgsql {#npgsql-settings}
- `Npgsql__CommandTimeout`: El tiempo de espera para los comandos de la base de datos en segundos.

### 📥 Configuración de carga {#upload-settings}
- `Upload__MaxFileSizeGB`: El tamaño máximo de archivo para las cargas en GB.
- `Upload__KeepAliveTimeoutHours`: El tiempo de espera de keep alive para las cargas en horas.
- `Upload__RequestHeadersTimeoutHours`: El tiempo de espera para los encabezados de la solicitud en horas.

### 📂 Configuración de Importación Local {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, etc.: Lista blanca de directorios locales desde los cuales los comandos de importación de la CLI (`--import-mbox`, `--import-eml`) tienen permitido leer archivos. Cada entrada es una ruta dentro del contenedor. Debe montar sus archivos de importación en uno de estos directorios utilizando volúmenes de Docker. Predeterminado: `/data/import` (establecido automáticamente como `/app/uploads` como alternativa).
  - Al usar `docker exec` para ejecutar el comando de importación, la ruta del archivo proporcionada a través de `--file` debe estar dentro de una de estas rutas permitidas.
  - Esta medida de seguridad evita el acceso arbitrario al sistema de archivos desde los comandos de la CLI.
  - Se pueden configurar múltiples rutas para diferentes fuentes de importación.
  - Consulte [CLI commands](CLI.md) para obtener instrucciones detalladas de uso.

### 📄 Configuración de importación de CSV {#csv-import-settings}
- `CsvImport__MaxRows`: Número máximo de filas de CSV (Buzones) procesadas en una única importación masiva. El valor predeterminado es `5000`. Aumente este valor para despliegues grandes; redúzcalo para limitar el impacto de una única ejecución de importación en la carga de la base de datos.
- `CsvImport__MaxFileSizeBytes`: Tamaño máximo permitido (en bytes) del archivo CSV cargado. El valor predeterminado es `10000000` (10 MB). Ajuste este valor para que coincida con sus límites de carga si es necesario.
- Consulte la [Account Import Guide](Account-Import.md) para obtener instrucciones detalladas sobre el uso de la importación masiva de Cuenta IMAP a través de CSV.

### 📤 Configuración de Exportación de Auditoría {#audit-export-settings}
Configuración para la página de exportación de datos de auditoría (solo administradores, accesible desde la página de Logs). La exportación genera un paquete ZIP con datos masivos tabulares (INDEX.XML + tablas CSV + DTD) del Archivo existente para herramientas de auditoría externas. Consulte la [Guía de Exportación de Datos de Auditoría](AuditExport.md) para obtener detalles sobre su uso.
- `AuditExport__DataSupplierName`: Valor predeterminado para el campo de formulario "Data supplier name" que identifica a su organización en el archivo de índice exportado. Predeterminado: vacío.
- `AuditExport__DataSupplierLocation`: Valor predeterminado para el campo de formulario "Data supplier location" (p. ej., sede de la empresa). Predeterminado: vacío.
- `AuditExport__Comment`: Valor predeterminado para el campo de formulario "Comment", incluido como texto libre en el archivo de índice exportado. Predeterminado: vacío.
- `AuditExport__OutputDirectory`: Directorio donde se almacenan los archivos ZIP generados. Las rutas relativas se resuelven con respecto a la raíz de contenido de la aplicación. Predeterminado: `exports/audit`.
- `AuditExport__RetentionDays`: Número de días tras los cuales los archivos de exportación completados son eliminados por la limpieza diaria. Predeterminado: `30`.
- `AuditExport__MaxRangeYears`: Intervalo máximo permitido entre la fecha de inicio y la fecha de finalización del período de exportación. Predeterminado: `10`.
- Los campos del formulario se completan automáticamente con estos valores predeterminados, pero pueden editarse por cada exportación.
- Cada exportación escribe una entrada de inicio y una entrada de resultado en el registro de acceso (tipo "Audit Data Export"), por lo que el historial es seguro frente a revisiones sin necesidad de realizar ningún cambio en el esquema de la DB.

### 🔒 Configuración de la Política de Eliminación {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Controla si se permite la eliminación manual de correos electrónicos archivados (true/false). El valor predeterminado es `true`. Cuando se establece en `false`:
  - Todos los correos electrónicos archivados se bloquean (`IsLocked = true`) al iniciar mediante el disparador de cumplimiento de la base de datos, evitando cualquier modificación o eliminación a nivel de base de datos.
  - La eliminación manual (individual y por lotes) se bloquea a nivel de aplicación con un mensaje de error.
  - El valor predeterminado de la columna se ajusta para que los correos electrónicos recién importados también estén bloqueados.
  - El estado actual de la política se registra en la tabla AccessLogs en cada inicio (visible en la página Logs como entradas "Deletion Policy") para su auditabilidad.
  - La eliminación por Retención local está exenta: los correos electrónicos que entran en un periodo de Retención configurado se siguen eliminando (se desbloquean inmediatamente antes de la eliminación dentro del proceso de Retención).
- `DeletionPolicy__MaxBulkDeleteEmails`: Límite superior para una eliminación por lotes basada en un filtro (*Delete all matches*). El valor predeterminado es `100000`. Un filtro que coincida con más correos electrónicos es rechazado con un mensaje pidiendo que se estreche; el trabajo nunca se trunca silenciosamente. Los correos electrónicos bloqueados por la política de Retención se omiten y se informan en el estado del trabajo, no se desbloquean. El trabajo vuelve a comprobar este límite y `DeletionPolicy__DeletionAllowed` cuando comienza, por lo que un trabajo en cola antes de un cambio de política se detiene en lugar de ejecutarse con la configuración antigua.
- **Protección de inmutabilidad:** Cuando `IsLocked = true`, el disparador de cumplimiento de la base de datos (`prevent_locked_email_changes`) bloquea CUALQUIER modificación de la fila del correo electrónico; todas las columnas están protegidas, no solo una lista de campos fija. Las únicas columnas exentas son `IsLocked` en sí misma (para que el desbloqueo para la eliminación por Retención y la aplicación de la política al inicio siga siendo posible) y `FolderName` (para que la Sincronización IMAP pueda actualizar la Carpeta cuando un correo electrónico se mueve en el servidor). La protección es agnóstica a la columna (comparación basada en JSONB) y cubre automáticamente futuras adiciones al esquema.

### 🕐 Configuración de la Zona Horaria {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: La zona horaria utilizada para mostrar las marcas de tiempo de los correos electrónicos en la interfaz de usuario. Utiliza identificadores de zona horaria IANA (por ejemplo, "Europe/Berlin", "Asia/Tokyo"). El valor predeterminado es "Etc/UCT" para compatibilidad con versiones anteriores. Al importar correos electrónicos, las marcas de tiempo se convertirán a esta zona horaria para fines de visualización.

### 🎉 Configuración de ReleaseNotes (Pantalla de bienvenida de actualización de versión) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: La cadena de versión que reporta esta instancia. Se compara con las etiquetas de lanzamiento de GitHub y decide si existe una versión más reciente. Cuando está vacío, el registro de cambios recurre a la versión del ensamblado compilado y el aviso de actualización permanece desactivado.
- `ReleaseNotes__Enabled`: Habilitar o deshabilitar la pantalla de bienvenida de actualización de versión (true/false). El valor predeterminado es `true`. Cuando está habilitado, los administradores verán un modal de registro de cambios de una sola vez después de una actualización de la aplicación, mostrando las notas de la versión obtenidas de GitHub Releases para la versión actual. Cada administrador puede cerrar el modal, y solo volverá a aparecer para una nueva versión. Establezca en `false` para deshabilitar completamente esta función, incluyendo el aviso de actualización.
- `ReleaseNotes__UpdateHintEnabled`: Mostrar a los administradores un pequeño toast en la esquina inferior derecha cuando GitHub publica una versión estable más reciente que la versión instalada (true/false). El valor predeterminado es `true`. El toast enlaza a ese lanzamiento y se descarta por versión en el navegador. Los borradores y las versiones preliminares se ignoran. Requiere `ReleaseNotes__AppVersion`; sin ello no se muestra ningún aviso.
- `ReleaseNotes__UpdateCheckIntervalHours`: Con qué frecuencia se actualiza la lista de lanzamientos de GitHub, en horas. El valor predeterminado es `6`. Un intervalo más corto hace que un nuevo lanzamiento sea visible más pronto en una instancia de larga ejecución; uno más largo reduce las llamadas a la API de GitHub. El mínimo es `1`.

### 🔧 Configuración de Mantenimiento de la Base de Datos {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Habilita o deshabilita el mantenimiento automático diario de la base de datos (true/false). El valor predeterminado es `false`. Cuando está habilitado, el sistema ejecutará automáticamente operaciones VACUUM ANALYZE para optimizar el rendimiento de la base de datos y evitar el crecimiento excesivo. Consulte la [Guía de Mantenimiento de la Base de Datos](DatabaseMaintenance.md) para obtener más detalles.
- `DatabaseMaintenance__DailyExecutionTime`: La hora del día en que debe ejecutarse el mantenimiento de la base de datos, en formato de 24 horas (HH:mm). El valor predeterminado es `02:00`. Elija una hora durante un periodo de baja actividad del sistema.
- `DatabaseMaintenance__TimeoutMinutes`: Tiempo máximo permitido para las operaciones de mantenimiento en minutos. El valor predeterminado es `30`. Aumente este valor para bases de datos más grandes.

### 🧬 Configuración de Deduplicación de Adjuntos {#attachment-deduplication-settings}
La Deduplicación de Adjuntos almacena cada carga útil de Adjunto única solo una vez (direccionada por contenido mediante SHA-256) y es una **característica principal que siempre está habilitada**; no existe intencionadamente un interruptor de encendido/apagado. Solo se pueden ajustar los parámetros de lote/programación que se presentan a continuación. Consulte la [Guía de Deduplicación de Adjuntos](AttachmentDeduplication.md) para obtener todos los detalles.

- `AttachmentDeduplication__BatchSize`: Número de Adjuntos existentes migrados por transacción durante la migración en segundo plano de una sola vez de los datos preexistentes. El valor predeterminado es `200`. Los valores más grandes migran más rápido pero utilizan más memoria/carga de la base de datos por lote.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Pausa opcional (en milisegundos) entre lotes de migración para limitar la carga de la base de datos en sistemas con mucha actividad. El valor predeterminado es `0` (sin pausa).
- `AttachmentDeduplication__StartupDelaySeconds`: Retraso (en segundos) después del inicio de la aplicación antes de que comience la migración en segundo plano, para dar tiempo a que se complete la migración del esquema. El valor predeterminado es `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Intervalo (en horas) de la recolección de basura siempre activa que elimina las cargas útiles de Adjuntos que ya no son referenciadas por ningún correo electrónico. El valor predeterminado es `12`. Esto se ejecuta independientemente de `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Tiempo de espera de comando de la base de datos (en segundos) para las operaciones de lote de migración (INSERT con hashing SHA-256 y UPDATE). El valor predeterminado es `300` (5 minutos). Aumente este valor para bases de datos o Adjuntos muy grandes, o redúzcalo si desea una detección de errores más rápida. Si un lote sigue agotando el tiempo de espera, el servicio reintenta automáticamente con la mitad del tamaño del lote.

### 💾 Configuración de Almacenamiento de Cuenta {#account-storage-settings}
La visualización de almacenamiento por Cuenta muestra el uso de almacenamiento de la base de datos (todos los campos de correo + Adjunto) para cada Cuenta en la tabla "Account Overview" del Dashboard y en la tabla "Show All" de MailAccounts. Un servicio de fondo autárquico (`AccountStorageRefreshService`) calcula los valores sumando los PostgreSQL `pg_column_size` por columna (cubriendo todos los campos de un correo sin leer los cuerpos de los correos) y los almacena en caché en la tabla `AccountStorageCache`. Este servicio se ejecuta independientemente de `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Habilitar o deshabilitar el servicio de actualización de almacenamiento (true/false). El valor predeterminado es `true`. Cuando está habilitado, el servicio realiza un backfill reanudable de las Cuentas pendientes al iniciar (seguro ante fallos mediante `AccountStorageBackfillState`) y una actualización completa diaria posteriormente. Cuando está deshabilitado, los valores de almacenamiento solo se actualizan cuando los correos electrónicos se sincronizan, se importan o se eliminan, pero los valores mostrados pueden quedar desactualizados con el tiempo.
- `AccountStorage__DailyExecutionTime`: Hora del día (formato de 24 horas `HH:mm`) para la actualización completa diaria de todas las Cuentas. El valor predeterminado es `02:30`. Elija una hora durante un periodo de baja actividad del sistema.
- `AccountStorage__BackfillDelayMs`: Retraso (en milisegundos) entre Cuentas durante el backfill inicial al iniciar. El valor predeterminado es `5000`. Los valores más bajos aceleran el backfill pero aumentan la carga de la base de datos; aumente este valor para Archivo muy grandes para evitar sobrecargar la base de datos.
- `AccountStorage__RefreshBatchDelayMs`: Retraso (en milisegundos) entre Cuentas durante la actualización completa diaria. El valor predeterminado es `1000`. Los valores más bajos aceleran la actualización pero aumentan la carga de la base de datos; aumente este valor para Archivo muy grandes.
- `AccountStorage__CommandTimeoutSeconds`: Tiempo de espera del comando de la base de datos (en segundos) para el cálculo de almacenamiento por Cuenta (`pg_column_size` sobre todas las filas de una Cuenta). El valor predeterminado es `300` (5 minutos). El cálculo lee cada fila de correo, incluyendo los datos TOAST, lo que puede tardar mucho más de un minuto en Archivo muy grandes (más de 250k correos); el valor predeterminado anterior de Npgsql de 30 segundos causaba que la actualización fallara silenciosamente y dejaba el dashboard mostrando valores desactualizados. Aumente este valor si el cálculo sigue agotando el tiempo de espera en su Archivo.

> 💡 **Nota**: Los valores de almacenamiento se actualizan inmediatamente después de cada sincronización, importación o eliminación por Retención de correo, por lo que los valores mostrados se mantienen actuales incluso sin la actualización diaria. La actualización diaria es una red de seguridad que captura casos excepcionales (por ejemplo, cambios directos en la base de datos).

### 📝 Configuración de Logging {#logging-settings}
- `Logging__LogLevel__Default`: El nivel de log predeterminado para la aplicación. Los niveles disponibles son: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. El predeterminado es `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Nivel de log para los mensajes del framework ASP.NET Core. El predeterminado es `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Nivel de log para los comandos de base de datos de Entity Framework. El predeterminado es `Warning`.

### 🛡️ Configuración de Seguridad {#security-settings}
- `AllowedHosts`: Una lista separada por puntos y comas de los nombres de host que la aplicación tiene permitido servir. Esto ayuda a prevenir ataques de cabecera HTTP Host. Ejemplo: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Importante**: No utilice `*` en entornos de producción, ya que desactiva la validación de la cabecera host.

#### 🔐 Cifrado de credenciales en reposo {#credential-encryption-at-rest}
Las credenciales de la Cuenta de correo (contraseña IMAP, secreto de cliente M365 y tokens OAuth de MSA) pueden cifrarse en la base de datos. La clave de cifrado se mantiene **fuera** de la base de datos (variable de entorno / Docker secret), por lo que un volcado de la base de datos por sí solo no es suficiente para leer las credenciales.

- `Security__CredentialEncryptionKey`: Clave de cifrado de 256 bits (32 bytes) codificada en Base64. Genere una con:
  ```bash
  openssl rand -base64 32
  ```
  Establezca el valor como una variable de entorno (recomendado: `Security__CredentialEncryptionKey` en su archivo compose o entorno de contenedor). La aplicación no admite secretos de Docker estilo `_FILE`; si utiliza un secreto de Docker, móntelo y cargue su contenido en la variable de entorno en su entrypoint. Nunca incluya la clave en `appsettings.json`. Cuando se configura una clave, las credenciales se cifran al escribirse, y un servicio en segundo plano cifra las credenciales de texto plano existentes una vez.
- `Security__EncryptAccountCredentials`: Habilita o deshabilita el cifrado de los valores de credenciales nuevos o actualizados (true/false). El valor predeterminado es `true`. Cuando no hay ninguna clave configurada, las credenciales se almacenan como texto plano y se registra una advertencia al iniciar — las instalaciones existentes no se ven afectadas hasta que se proporcione una clave.
- `Security__CredentialEncryptionBackfill`: Indica si el servicio en segundo plano debe cifrar las credenciales de texto plano existentes al iniciar (true/false). El valor predeterminado es `true`. No realiza ninguna acción si no hay una clave configurada.
- `Security__CredentialEncryptionKeyPrevious`: Clave anterior opcional, solo se utiliza para descifrar valores cifrados con ella (para la rotación de claves). Los nuevos valores siempre se cifran con `Security__CredentialEncryptionKey`.

  > ⚠️ **Rotación de claves**: después de rotar, mantenga `Security__CredentialEncryptionKeyPrevious` configurada **hasta que cada credencial haya sido reescrita** (por ejemplo, editando la Cuenta o mediante una actualización automática de un token OAuth) — los valores existentes cifrados con la clave antigua *no* se re-cifran automáticamente con la nueva clave. Una vez que elimine la clave anterior, cualquier credencial que aún esté cifrada con ella dejará de ser legible y tendrá que introducirse de nuevo. Mientras esté configurada, debe mantenerse tan segura como la clave actual.

> ⚠️ **Mantenga la clave segura**: Sin la clave, las credenciales cifradas no se pueden recuperar y deben introducirse de nuevo. Realice una Copia de seguridad de la misma junto con (pero de forma separada a) sus Copia de seguridad de la base de datos.
>
> 💡 **Instalaciones existentes**: Actualice de forma segura — mientras no se establezca ninguna `Security__CredentialEncryptionKey`, nada cambia. Establezca la clave cuando esté listo; las credenciales de texto plano existentes siguen siendo legibles y se cifran en segundo plano.

### 🔐 Configuración de OIDC {#oidc-configuration}

Para obtener instrucciones detalladas de configuración para la autenticación OpenID Connect, consulte la [OIDC Implementation Guide](OIDC_Implementation.md).

#### Configuración básica de OIDC {#basic-oidc-settings}
- `OAuth__Enabled`: Habilitar o deshabilitar la autenticación OIDC (true/false)
- `OAuth__Authority`: La URL de autoridad de OpenID Connect (p. ej., https://sts.windows.net/{TENANT-ID}/ para Azure AD)
- `OAuth__ClientId`: El client ID asignado por su proveedor de identidad
- `OAuth__ClientSecret`: El client secret asignado por su proveedor de identidad
- `OAuth__DisplayName`: Nombre de visualización opcional que se muestra en el botón de inicio de sesión OIDC y en la página de redirección automática (p. ej., `PocketID SSO`). Si se omite, se utiliza la etiqueta genérica "Login with OAuth".
- `OAuth__ClientScopes__0`: Primer scope solicitado al proveedor de identidad (openid)
- `OAuth__ClientScopes__1`: Segundo scope solicitado al proveedor de identidad (profile)
- `OAuth__ClientScopes__2`: Tercer scope solicitado al proveedor de identidad (email)
- `OAuth__ResponseMode`: Modo de respuesta opcional de la solicitud de autorización (`query`, `fragment` o `form_post`). El valor predeterminado es `form_post`, el predeterminado de ASP.NET Core. Establezca en `query` para proveedores que no admiten `form_post`, como Synology SSO Server. Consulte [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Aprobar automáticamente a los nuevos usuarios de OIDC sin requerir la aprobación manual del administrador (true/false). El valor predeterminado es `false`.

#### Configuración de aprovisionamiento de usuarios {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Aprobar automáticamente a los nuevos usuarios de OIDC sin requerir la aprobación manual del administrador (true/false). El valor predeterminado es `false`. Cuando está habilitado, los usuarios que se autentican a través del proveedor OIDC se activan inmediatamente y pueden acceder a la aplicación. Cuando está deshabilitado (predeterminado), los nuevos usuarios de OIDC se crean como inactivos y requieren la activación manual por parte de un administrador. Consulte [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users) para obtener información detallada.
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, etc.: Direcciones de correo electrónico que deben aprovisionarse automáticamente como administradores. Los usuarios con estas direcciones de correo electrónico se crearán como administradores activos en el primer inicio de sesión de OAuth, omitiendo el proceso de aprobación normal. La coincidencia de correo electrónico no distingue entre mayúsculas y minúsculas.

#### Configuración de inicio de sesión sin contraseña {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Deshabilitar el inicio de sesión con nombre de usuario/contraseña (true/false). El valor predeterminado es `false`. Cuando está habilitado, el formulario de contraseña se oculta **y** el servidor rechaza cada POST de contraseña, por lo que solo está disponible el inicio de sesión OAuth. Requiere `OAuth__Enabled=true`. Si OAuth está deshabilitado, esta configuración no tiene efecto.
- `OAuth__AutoRedirect`: Redirigir automáticamente a los usuarios al proveedor de OAuth (true/false). El valor predeterminado es `false`. Requiere que `OAuth__DisablePasswordLogin` sea `true`. Los usuarios verán una breve pantalla de carga antes de ser redirigidos.

#### Ejemplo: Configuración completa de OIDC-First {#example-full-oidc-first-configuration}
```yaml
environment:
  - OAuth__Enabled=true
  - OAuth__Authority=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0
  - OAuth__ClientId=your-client-id
  - OAuth__ClientSecret=your-client-secret
  - OAuth__DisplayName=PocketID SSO
  - OAuth__ClientScopes__0=openid
  - OAuth__ClientScopes__1=profile
  - OAuth__ClientScopes__2=email
  - OAuth__DisablePasswordLogin=true
  - OAuth__AutoRedirect=true
  - OAuth__AutoApproveUsers=true
  - OAuth__AdminEmails__0=admin@example.com
  - OAuth__AdminEmails__1=manager@example.com
```

## 🔐 Configuración de Kestrel HTTPS (Opcional) {#kestrel-https-configuration-optional}

Si bien la aplicación está diseñada para ser accedida a través de un proxy inverso con HTTPS, también puede configurar el servidor web Kestrel para utilizar certificados SSL/TLS. Esto proporciona cifrado de extremo a extremo entre el proxy inverso y el contenedor de la aplicación.

### Pasos de configuración {#configuration-steps}

1. **Generar u obtener un certificado SSL** en formato PFX (ej., `localhost.pfx`)

2. **Añadir las siguientes variables de entorno** a su `docker-compose.yml` para el servicio `mailarchive-app`:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Actualizar el mapeo de puertos** en el servicio `mailarchive-app`:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Añadir un mapeo de volumen** para el certificado:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Colocar su archivo de certificado** (ej., `localhost.pfx`) en el directorio `./certs` de su sistema host.

### Explicación de las variables de entorno {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: URL del endpoint HTTP (por defecto: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: URL del endpoint HTTPS (por defecto: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Ruta al archivo de certificado PFX dentro del contenedor
- `Kestrel__Endpoints__Https__Certificate__Password`: Contraseña para el archivo de certificado PFX

> 💡 **Nota**: Esta configuración es opcional. Si está utilizando un proxy inverso con HTTPS (recomendado), la comunicación entre el proxy inverso y la aplicación puede permanecer en HTTP. Sin embargo, para obtener la máxima seguridad en entornos sensibles, es posible que desee habilitar HTTPS en Kestrel también para cifrar toda la ruta de comunicación.

## 🔑 Gestión de secretos para producción {#secrets-management-for-production}

Incluir datos sensibles como contraseñas de bases de datos, credenciales de administrador y secretos de cliente OAuth directamente en `docker-compose.yml` es un riesgo de seguridad, especialmente cuando el archivo se incluye en el control de versiones. El enfoque recomendado es externalizar estos valores en un archivo `.env` que Docker Compose cargue automáticamente.

### 📄 El archivo `.env` {#the-env-file}

Cree un archivo `.env` en el mismo directorio que su `docker-compose.yml` con el siguiente contenido:

```env
# Database
POSTGRES_PASSWORD=YourSecureDBPassword123!

# Admin Account
AUTH_USERNAME=admin
AUTH_PASSWORD=YourSecureAdminPassword456!

# OIDC / OAuth Secrets
OAUTH_CLIENT_SECRET=YourOAuthClientSecret

# Credential Encryption at Rest (generate with: openssl rand -base64 32)
CREDENTIAL_ENCRYPTION_KEY=YourBase64Encoded32ByteKey

# Kestrel HTTPS (optional)
KESTREL_CERT_PASSWORD=YourCertPassword
```

**Importante**: Docker Compose lee automáticamente el archivo `.env` del mismo directorio; **no** es necesario referenciarlo manualmente. Todas las variables definidas en `.env` están disponibles en `docker-compose.yml` a través de la sintaxis `${VARIABLE_NAME}`.

### 🐳 `docker-compose.yml` adaptado {#adapted-docker-composeyml}

El `docker-compose.yml` de los pasos de instalación debe adaptarse para usar marcadores de posición en lugar de secretos codificados:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=${POSTGRES_PASSWORD}

      # Authentication Settings
      - Authentication__Username=${AUTH_USERNAME}
      - Authentication__Password=${AUTH_PASSWORD}
      # ... other settings remain unchanged ...

      # OIDC Configuration
      - OAuth__ClientSecret=${OAUTH_CLIENT_SECRET}

      # Credential Encryption at Rest
      - Security__CredentialEncryptionKey=${CREDENTIAL_ENCRYPTION_KEY}
    # ... ports, volumes, networks ...

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    # ... volumes, networks ...
```

### 📋 Plantilla de `.env.example` para administradores {#envexample-template-for-administrators}

Cree un archivo `.env.example` en su repositorio que sirva como plantilla para otros administradores. Contiene valores de marcador de posición y documentación, pero **ningún secreto real**:

```env
# ============================================
# Mail Archiver - Environment Configuration
# ============================================
# Copy this file to .env and fill in your values.
# Never commit the actual .env file to version control!

# --- PostgreSQL ---
POSTGRES_PASSWORD=change_me_db_password

# --- Admin Account ---
AUTH_USERNAME=admin
AUTH_PASSWORD=change_me_admin_password

# --- OIDC / OAuth ---
OAUTH_CLIENT_SECRET=change_me_client_secret

# --- Credential Encryption at Rest ---
# Generate with: openssl rand -base64 32
CREDENTIAL_ENCRYPTION_KEY=change_me_base64_32_byte_key

#...and so on
```

### ✅ Mejores prácticas {#best-practices}

- **Use `.env.example` como plantilla**
- **Establezca Permisos de archivo estrictos** — Restrinja el acceso al archivo `.env`:
  ```bash
  chmod 600 .env
  ```
- **Use contraseñas fuertes** — Genere contraseñas largas y aleatorias (al menos 16 caracteres) con una mezcla de letras, números y caracteres especiales.
- **Limite el acceso al archivo `.env`** — Solo el usuario que ejecuta Docker Compose debe tener permisos de lectura para el archivo `.env`.

## 🔒 Notas de seguridad {#security-notes}

- Use contraseñas fuertes y cambie las credenciales por defecto. Las contraseñas deben tener al menos 12 caracteres e incluir una combinación de letras mayúsculas, letras minúsculas, números y caracteres especiales. Evite el uso de palabras comunes o información fácilmente adivinable.
- Considere la implementación de HTTPS con un reverse proxy en producción
- Se recomiendan Copia de seguridad regulares de la base de datos PostgreSQL. Para procedimientos detallados de Copia de seguridad y Restauración, consulte la [Guía de Copia de seguridad y Restauración](BackupRestore.md).
