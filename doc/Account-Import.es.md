# Importación de Cuenta {#account-import}

[← Volver al Índice de Documentación](index.md)

## Resumen {#overview}

Mail Archiver permite la importación de múltiples cuentas de correo electrónico a la vez para que la incorporación de un gran número de buzones no requiera la creación de cada cuenta individualmente.

Existen dos rutas de importación masiva, dependiendo del proveedor de correo:

| Proveedor | Método de Importación Masiva | Documentación |
|---|---|---|
| **Microsoft 365 (tenant)** | Descubrimiento de tenant mediante Microsoft Graph — un registro de aplicación accede a cada buzón en el tenant | [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) |
| **IMAP** | Importación masiva por CSV — suba un archivo CSV con una fila por cada buzón | Esta página (ver más abajo) |

> **Nota:** Las cuentas personales de Microsoft (Outlook.com / M365 Family, proveedor **MSA**) utilizan un flujo de autorización de código de dispositivo interactivo y no pueden importarse de forma masiva. Deben crearse y autorizarse una a una. Consulte [MSA Outlook Setup](MSA_Outlook_Setup.md).

---

## Importación Masiva de CSV para IMAP {#imap-csv-bulk-import}

### Qué hace {#what-it-does}

La importación masiva por CSV permite a un administrador crear cientos de cuentas de correo IMAP en una sola operación mediante la subida de un archivo CSV. Cada fila en el archivo corresponde a un buzón. Los ajustes de servidor comunes (host IMAP, puerto, SSL) pueden especificarse una vez en el formulario de subida y se aplican a cada fila, con la opción de sobrescribirlos por fila.

La importación es **solo para administradores**. Es accesible desde la página de **Mail Accounts** a través del menú desplegable **Import** → **CSV Bulk Import**.

### Formato de archivo CSV {#csv-file-format}

El archivo debe ser un CSV codificado en UTF-8 con una **fila de encabezado** y la **coma (`,`) como delimitador**. Los campos que contengan comas o comillas deben estar encerrados entre comillas dobles (`"..."`).

#### Columnas {#columns}

| Columna | Requerido | Descripción |
|---|---|---|
| `email` | Sí | Dirección del buzón. También se usa como el nombre de usuario de inicio de sesión por defecto cuando no se proporciona `username`. |
| `password` | Sí | Contraseña de inicio de sesión IMAP. Se almacena tal cual se proporciona — los espacios al principio o al final se preservan. |
| `name` | No | Nombre para mostrar de la cuenta. Si se omite, el valor por defecto es `{prefix} - <{email}>`. |
| `username` | No | Nombre de usuario de inicio de sesión si difiere de la dirección de correo electrónico. |
| `imap_server` | No | Host IMAP por fila. Sobrescribe el servidor común del formulario de subida. |
| `imap_port` | No | Puerto IMAP por fila (1–65535). Sobrescribe el puerto común. |
| `use_ssl` | No | Alternancia de SSL por fila (`true` o `false`). Sobrescribe el ajuste común. |

#### Ejemplo {#example}

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- La Fila 1 usa el servidor/puerto/SSL común del formulario de subida.
- La Fila 2 establece un nombre de usuario personalizado pero, por lo demás, usa los ajustes comunes.
- La Fila 3 sobrescribe el servidor, el puerto y el SSL.

Un archivo de ejemplo descargable está disponible en la página de importación a través del botón **Download example CSV**.

### Formulario de Subida {#upload-form}

| Campo | Propósito |
|---|---|
| **CSV file** | El archivo a subir. |
| **IMAP server** | Host común para todas las filas sin un `imap_server` de sobrescritura. |
| **IMAP port** | Puerto común (por defecto 993). |
| **Use SSL** | Alternancia de SSL común (activada por defecto). |
| **Name prefix** | Prefijo para los nombres de cuenta autogenerados. Por defecto: `IMAP`. |
| **Skip existing mailboxes** | Cuando está habilitado, las filas cuyo correo ya existe como cuenta IMAP se omiten (se cuentan como "omitidas", no como errores). Cuando está deshabilitado, se tratan como fallos. |
| **Account enabled** | Si las cuentas creadas deben estar habilitadas para la Sincronización en segundo plano. |
| **Delete After Days / Local Retention Days** | Ajustes de Retención opcionales aplicados a cada cuenta creada. Consulte [Retention Policies](RetentionPolicies.md). |

### Procesamiento {#processing}

1. El archivo CSV se analiza y cada fila se valida (formato de correo, presencia de contraseña, rango de puerto, valor de SSL).
2. Los correos electrónicos duplicados dentro del archivo se eliminan (la primera aparición gana).
3. Se detectan las cuentas IMAP existentes con la misma dirección de correo electrónico; dependiendo del interruptor **Skip existing**, se omiten o se reportan como fallos.
4. Todas las nuevas cuentas se insertan en un único lote de base de datos.
5. Se escribe una entrada en el registro de acceso resumiendo la ejecución.

No se realiza una prueba de conexión IMAP durante la importación — las cuentas se guardan inmediatamente. Los problemas de autenticación solo aparecerán durante la primera Sincronización en segundo plano. Esto mantiene la importación rápida incluso para cientos de buzones.

### Página de Resultados {#result-page}

Tras completar la importación, una página de resultados muestra tres tablas:

- **Created** — correo electrónico y nombre de cada cuenta que fue creada.
- **Skipped** — correos electrónicos que eran duplicados (en el archivo o ya existentes) cuando **Skip existing** está habilitado.
- **Failed** — filas que no pudieron procesarse, con el número de línea y el motivo (falta de correo, falta de contraseña, puerto inválido, sin servidor IMAP, etc.).

Un banner de resumen muestra los recuentos: *{created} creados, {skipped} omitidos, {failed} fallidos*.

### Límites {#limits}

Los siguientes límites pueden configurarse mediante `appsettings.json` / variables de entorno (consulte [Setup Guide](Setup.md) → *CSV Import Settings*):

| Ajuste | Por defecto | Descripción |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | Máximo de filas procesadas por subida. |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | Tamaño máximo del archivo subido. |

### Nota de Seguridad {#security-note}

Las contraseñas IMAP se almacenan en la base de datos de la misma manera que para las cuentas IMAP creadas individualmente. Tenga en cuenta que el archivo CSV contiene todas las contraseñas de los buzones en texto plano — elimínelo de la máquina cliente tras una importación exitosa.

---

## Importación de Tenant de Microsoft 365 {#microsoft-365-tenant-import}

Para Microsoft 365 (Exchange Online), la importación masiva no requiere un archivo CSV. Un único registro de aplicación de Azure AD con los permisos de Graph correctos puede acceder a cada buzón en el tenant, por lo que Mail Archiver puede descubrir los buzones automáticamente.

Consulte la guía dedicada: **[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**.
