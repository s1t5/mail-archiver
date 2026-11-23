# 📧 Documentación de Mail Archiver {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Archivo de correo electrónico auto-alojado — archiva, busca y exporta correos de cualquier proveedor.**
Configúralo una vez, conserva cada mensaje y encuéntralo de nuevo años después.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Primeros pasos__

    ---

    Instala Mail Archiver con Docker Compose y conecta tu primer Buzón.

    [:octicons-arrow-right-24: Configuración y parámetros](Setup.md)

-   :material-magnify:{ .lg .middle } __Encuentra cualquier cosa__

    ---

    Búsqueda de texto completo en cada Cuenta, con filtros, ordenación y acciones masivas.

    [:octicons-arrow-right-24: Búsqueda de correo](Search.md)

-   :material-api:{ .lg .middle } __Automatízalo__

    ---

    Lee tu Archivo desde scripts y agentes de IA — claves de API de solo lectura y con alcance limitado.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Asegúralo__

    ---

    Autenticación de dos factores, SSO, registro de acceso y políticas de Retención.

    [:octicons-arrow-right-24: Autenticación de dos factores](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Instalación y Mantenimiento {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Configuración y parámetros__

    ---

    Cada variable de entorno, Kestrel HTTPS y gestión de secretos.

    [:octicons-arrow-right-24: Leer](Setup.md)

-   :material-server-network:{ .lg .middle } __Proxy inverso__

    ---

    Coloca Mail Archiver detrás de nginx o Caddy con terminación TLS.

    [:octicons-arrow-right-24: Leer](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __Actualización de PostgreSQL__

    ---

    Pasa a una nueva versión principal de PostgreSQL sin perder datos.

    [:octicons-arrow-right-24: Leer](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Mantenimiento de la base de datos__

    ---

    VACUUM, ANALYZE y mantener el Archivo con capacidad de respuesta a lo largo del tiempo.

    [:octicons-arrow-right-24: Leer](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Copia de seguridad y Restauración__

    ---

    Realiza una Copia de seguridad de la base de datos y su Restauración — incluyendo configuraciones de Proxmox.

    [:octicons-arrow-right-24: Leer](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Entorno de prueba local__

    ---

    Ejecuta una instancia temporal localmente antes de tocar producción.

    [:octicons-arrow-right-24: Leer](LocalTestEnvironment.md)

</div>

## 📥 Entrada de Correo {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Importación de Cuenta__

    ---

    Añade muchas Cuentas a la vez desde un archivo CSV.

    [:octicons-arrow-right-24: Leer](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Sincronización__

    ---

    Sincronización rápida vs. completa, puntos de control y qué sucede cuando una Sincronización falla.

    [:octicons-arrow-right-24: Leer](Synchronization.md)

-   :material-console:{ .lg .middle } __Comandos CLI__

    ---

    Importa Archivos existentes desde la línea de comandos y mueve mensajes originales almacenados entre la base de datos y el sistema de archivos.

    [:octicons-arrow-right-24: Leer](CLI.md)

-   :material-upload:{ .lg .middle } __Importación web (EML / MBox)__

    ---

    Sube un archivo MBox o un ZIP de archivos EML a través del navegador.

    [:octicons-arrow-right-24: Leer](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Gestión de límites de velocidad__

    ---

    Seguimiento de ancho de banda y cómo las Sincronizaciones pausadas se reanudan por sí mismas.

    [:octicons-arrow-right-24: Leer](RateLimitHandling.md)

</div>

## 🔍 Trabajando con el Archivo {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Búsqueda de correo__

    ---

    Sintaxis de consulta, filtros, ordenación, paginación y acciones masivas.

    [:octicons-arrow-right-24: Leer](Search.md)

-   :material-progress-clock:{ .lg .middle } __Tareas en segundo plano__

    ---

    Observa Sincronizaciones, importaciones, Restauraciones y exportaciones — y cancélalas de forma segura.

    [:octicons-arrow-right-24: Leer](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Migración de Buzón__

    ---

    Copia correos entre Buzones preservando la estructura de la Carpeta.

    [:octicons-arrow-right-24: Leer](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Exportación de Cuenta (EML / MBox)__

    ---

    Exporta una Cuenta completa como un Archivo EML o MBox portátil.

    [:octicons-arrow-right-24: Leer](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Almacenamiento de mensaje original__

    ---

    Mantén el mensaje tal como se recibió, para que DKIM siga verificando y las exportaciones sean exactas en bytes.

    [:octicons-arrow-right-24: Leer](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Deduplicación de Adjuntos__

    ---

    Cómo los Adjuntos idénticos se almacenan una vez en lugar de muchas veces.

    [:octicons-arrow-right-24: Leer](AttachmentDeduplication.md)

-   :material-file-pdf-box:{ .lg .middle } __Vista previa de factura XRechnung__

    ---

    Lee los Adjuntos XML de XRechnung como un PDF directamente en el Archivo.

    [:octicons-arrow-right-24: Leer](EInvoicing.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Descarga por ventana de fecha__

    ---

    Mueve correos antiguos a otra Cuenta, ventana por ventana.

    [:octicons-arrow-right-24: Leer](Offload.md)

-   :material-harddisk:{ .lg .middle } __Almacenamiento por Cuenta__

    ---

    Mira cuánto espacio ocupa cada Buzón.

    [:octicons-arrow-right-24: Leer](AccountStorage.md)

</div>

## 🔐 Administración y Seguridad {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Gestión de usuarios__

    ---

    Crea usuarios, otorga derechos de administrador, asigna Permisos de Buzón y cambia tu propia contraseña.

    [:octicons-arrow-right-24: Leer](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Autenticación de dos factores__

    ---

    Configuración de TOTP, códigos de respaldo y qué hacer cuando se pierde un dispositivo.

    [:octicons-arrow-right-24: Leer](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Inicia sesión a través de Entra ID, Authelia u otro proveedor de identidad.

    [:octicons-arrow-right-24: Leer](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Registro de acceso__

    ---

    Quién abrió, buscó, exportó o eliminó qué.

    [:octicons-arrow-right-24: Leer](Logs.md)

-   :material-file-export:{ .lg .middle } __Exportación de datos de auditoría__

    ---

    Exporta el rastro de auditoría para revisiones de cumplimiento.

    [:octicons-arrow-right-24: Leer](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Recuperación de Cuenta de emergencia__

    ---

    Recupera el acceso cuando una Cuenta ha sido bloqueada.

    [:octicons-arrow-right-24: Leer](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Políticas de Retención__

    ---

    Eliminación automática del servidor de correo o del Archivo local.

    [:octicons-arrow-right-24: Leer](RetentionPolicies.md)

</div>

## ☁️ Guías de Proveedores {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Registro de app en Azure (M365)__

    ---

    Registra una app y configura la Retención para Microsoft 365.

    [:octicons-arrow-right-24: Leer](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __Importación de Buzón de inquilino M365__

    ---

    Importación masiva de cada Buzón de un inquilino desde un único formulario.

    [:octicons-arrow-right-24: Leer](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Instancias de Mailcow__

    ---

    Selecciona Buzones de forma centralizada y provisiona contraseñas de aplicación IMAP dedicadas.

    [:octicons-arrow-right-24: Leer](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Cuentas personales de Microsoft__

    ---

    Outlook.com, Hotmail y M365 Family mediante flujo de código de dispositivo.

    [:octicons-arrow-right-24: Leer](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Mejores prácticas de Gmail__

    ---

    Contraseñas de aplicación, etiquetas, expunge y límites de velocidad de Gmail.

    [:octicons-arrow-right-24: Leer](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Limitaciones de Yahoo mail__

    ---

    Lo que los endpoints de IMAP y exportación de Yahoo no harán.

    [:octicons-arrow-right-24: Leer](YahooMailExportLimitations.md)

</div>

## 🔌 Acceso Programático {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg . middle } __REST API__

    ---

    Acceso de solo lectura al Archivo con claves de API con alcance por usuario.

    [:octicons-arrow-right-24: Leer](API.md)

-   :material-robot:{ .lg . middle } __MCP server__

    ---

    Expone el Archivo a agentes de IA como herramientas descubribles.

    [:octicons-arrow-right-24: Leer](MCP.md)

</div>

## 🧰 Operaciones y Desarrollo {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg . middle } __Idioma y localización__

    ---

    Cambia el idioma de la interfaz y cómo añadir una traducción.

    [:octicons-arrow-right-24: Leer](Localization.md)

-   :material-docker:{ .lg . middle } __Logs de Docker Compose__

    ---

    Lee los logs de los contenedores y cambia el nivel de log.

    [:octicons-arrow-right-24: Leer](DockerComposeLogs.md)

-   :material-heart-pulse:{ .lg . middle } __Salud y métricas__

    ---

    Observa la vitalidad, la preparación y las métricas del Archivo desde Prometheus u otro sistema de monitorización.

    [:octicons-arrow-right-24: Leer](Monitoring.md)

-   :material-tag:{ .lg . middle } __Versiones de desarrollo__

    ---

    Cambia a una etiqueta de desarrollo para probar cambios próximos.

    [:octicons-arrow-right-24: Leer](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Esta documentación se está expandiendo continuamente.**
Si detectas un error o tienes una sugerencia, por favor
[abre un issue](https://github.com/s1t5/mail-archiver/issues) o envía un pull request.
</div>
