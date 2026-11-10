# 📧 Mail Archiver Documentation {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Archivo de correo electrónico auto-alojado — archive, search and export mail from any provider.**
Configúrelo una vez, conserve cada mensaje y encuéntrelo de nuevo años después.
</div>

!!! note "Traducciones"
    La documentación en inglés es la fuente autorizada. Cualquier otro idioma se traduce automáticamente desde estas páginas y puede contener errores. Utilice el selector de idioma en el encabezado para cambiar el idioma.

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Primeros pasos__

    ---

    Instale Mail Archiver con Docker Compose y conecte su primer Buzón.

    [:octicons-arrow-right-24: Setup and parameters](Setup.md)

-   :material-magnify:{ .lg .middle } __Encuentre cualquier cosa__

    ---

    Búsqueda de texto completo en cada Cuenta, con filtros, ordenación y acciones en lote.

    [:octicons-arrow-right-24: Mail search](Search.md)

-   :material-api:{ .lg .middle } __Automatícelo__

    ---

    Lea su Archivo desde scripts y agentes de IA — claves de API de solo lectura y con alcance limitado.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Asegúrelo__

    ---

    Autenticación de dos factores, SSO, registro de acceso y políticas de Retención.

    [:octicons-arrow-right-24: Two-factor auth](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Instalación y Mantenimiento {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Setup and parameters__

    ---

    Cada variable de entorno, Kestrel HTTPS y gestión de secretos.

    [:octicons-arrow-right-24: Read](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Coloque Mail Archiver detrás de nginx o Caddy con terminación TLS.

    [:octicons-arrow-right-24: Read](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL upgrade__

    ---

    Pase a una nueva versión principal de PostgreSQL sin perder datos.

    [:octicons-arrow-right-24: Read](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Database maintenance__

    ---

    VACUUM, ANALYZE y mantener el Archivo con capacidad de respuesta a lo largo del tiempo.

    [:octicons-arrow-right-24: Read](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Backup and restore__

    ---

    Realice una Copia de seguridad de la base de datos y su Restauración — incluyendo configuraciones de Proxmox.

    [:octicons-arrow-right-24: Read](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Local test environment__

    ---

    Ejecute una instancia temporal localmente antes de tocar producción.

    [:octicons-arrow-right-24: Read](LocalTestEnvironment.md)

</div>

## 📥 Entrada de Correo {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Account import__

    ---

    Añada muchas Cuentas a la vez desde un archivo CSV.

    [:octicons-arrow-right-24: Read](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Sincronización__

    ---

    Sincronización rápida vs. completa, puntos de control y qué sucede cuando una sincronización falla.

    [:octicons-arrow-right-24: Read](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI commands__

    ---

    Importe Archivos existentes desde la línea de comandos y mueva mensajes originales almacenados entre la base de datos y el sistema de archivos.

    [:octicons-arrow-right-24: Read](CLI.md)

-   :material-upload:{ .lg .middle } __Web import (EML / MBox)__

    ---

    Suba un archivo MBox o un ZIP de archivos EML a través del navegador.

    [:octicons-arrow-right-24: Read](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Rate limit handling__

    ---

    Seguimiento de ancho de banda y cómo las sincronizaciones pausadas se reanudan por sí solas.

    [:octicons-arrow-right-24: Read](RateLimitHandling.md)

</div>

## 🔍 Trabajo con el Archivo {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Mail search__

    ---

    Sintaxis de consulta, filtros, ordenación, paginación y acciones en lote.

    [:octicons-arrow-right-24: Read](Search.md)

-   :material-progress-clock:{ .lg .middle } __Background jobs__

    ---

    Vigile las sincronizaciones, importaciones, Restauraciones y exportaciones — y cancélelas de forma segura.

    [:octicons-arrow-right-24: Read](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Mailbox migration__

    ---

    Copie correos entre Buzones preservando la estructura de la Carpeta.

    [:octicons-arrow-right-24: Read](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Account export (EML / MBox)__

    ---

    Escriba una Cuenta completa como un Archivo EML o MBox portable.

    [:octicons-arrow-right-24: Read](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Original message storage__

    ---

    Mantenga el mensaje tal como se recibió, para que DKIM siga verificando y las exportaciones sean exactas en bytes.

    [:octicons-arrow-right-24: Read](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Attachment deduplication__

    ---

    Cómo los Adjuntos idénticos se almacenan una vez en lugar de muchas veces.

    [:octicons-arrow-right-24: Read](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Date-windowed offload__

    ---

    Mueva el correo más antiguo a otra Cuenta, ventana por ventana.

    [:octicons-arrow-right-24: Read](Offload.md)

-   :material-harddisk:{ .lg .middle } __Per-account storage__

    ---

    Vea cuánto espacio ocupa cada Buzón.

    [:octicons-arrow-right-24: Read](AccountStorage.md)

</div>

## 🔐 Administración y Seguridad {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __User management__

    ---

    Cree usuarios, otorgue derechos de administrador, asigne Permisos de Buzón y cambie su propia contraseña.

    [:octicons-arrow-right-24: Read](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Two-factor authentication__

    ---

    Configuración de TOTP, códigos de respaldo y qué hacer cuando se pierde un dispositivo.

    [:octicons-arrow-right-24: Read](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Inicie sesión a través de Entra ID, Authelia u otro proveedor de identidad.

    [:octicons-arrow-right-24: Read](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Access logging__

    ---

    Quién abrió, buscó, exportó o eliminó qué.

    [:octicons-arrow-right-24: Read](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit data export__

    ---

    Exporte el rastro de auditoría para revisiones de cumplimiento.

    [:octicons-arrow-right-24: Read](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Emergency account recovery__

    ---

    Recupere el acceso cuando una Cuenta esté bloqueada.

    [:octicons-arrow-right-24: Read](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Retention policies__

    ---

    Eliminación automática del servidor de correo o del Archivo local.

    [:octicons-arrow-right-24: Read](RetentionPolicies.md)

</div>

## ☁️ Guías de Proveedores {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure app registration (M365)__

    ---

    Registre una aplicación y configure la Retención para Microsoft 365.

    [:octicons-arrow-right-24: Read](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant mailbox import__

    ---

    Importación masiva de cada Buzón de un tenant desde un único formulario.

    [:octicons-arrow-right-24: Read](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow instances__

    ---

    Seleccione Buzones centralmente y proporcione contraseñas de aplicación IMAP dedicadas.

    [:octicons-arrow-right-24: Read](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Personal Microsoft accounts__

    ---

    Outlook.com, Hotmail y M365 Family mediante el flujo de código de dispositivo.

    [:octicons-arrow-right-24: Read](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail best practices__

    ---

    Contraseñas de aplicación, etiquetas, expunge y los límites de velocidad de Gmail.

    [:octicons-arrow-right-24: Read](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail limitations__

    ---

    Lo que los endpoints de IMAP y exportación de Yahoo no harán.

    [:octicons-arrow-right-24: Read](YahooMailExportLimitations.md)

</div>

## 🔌 Acceso Programático {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Acceso de solo lectura al Archivo con claves de API con alcance limitado por usuario.

    [:octicons-arrow-right-24: Read](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Exponga el Archivo a agentes de IA como herramientas descubribles.

    [:octicons-arrow-right-24: Read](MCP.md)

</div>

## 🧰 Operaciones y Desarrollo {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Language and localization__

    ---

    Cambie el idioma de la interfaz y cómo añadir una traducción.

    [:octicons-arrow-right-24: Read](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose logs__

    ---

    Lea los logs del contenedor y cambie el nivel de log.

    [:octicons-arrow-right-24: Read](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Development versions__

    ---

    Cambie a una etiqueta de desarrollo para probar los próximos cambios.

    [:octicons-arrow-right-24: Read](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Esta documentación se está expandiendo continuamente.**
Si detecta un error o tiene una sugerencia, por favor
[open an issue](https://github.com/s1t5/mail-archiver/issues) o envíe un pull request.
</div>
