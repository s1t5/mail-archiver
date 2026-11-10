# 📤 Exportación de Datos de Auditoría {#audit-data-export}

[← Volver al Índice de Documentación](index.md)

## 📋 Descripción general {#overview}

La exportación de datos de auditoría crea paquetes de datos masivos tabulares (ZIP con `INDEX.XML` + tablas CSV + índice DTD) a partir del Archivo existente. Estos paquetes siguen la estructura de índice establecida para la entrega de medios de datos y pueden ser importados directamente por herramientas externas comunes de auditoría y análisis que admitan este formato de índice.

La función está disponible como una **página dedicada accesible desde la página de Logs** y está restringida a usuarios administradores. Cada ejecución de exportación se registra en el registro de acceso, por lo que el historial de exportación es seguro para revisiones.

## 🧭 Acceso {#access}

1. Inicie sesión como usuario administrador
2. Abra la página **Logs**
3. Haga clic en el botón **Audit Data Export** en la esquina superior derecha

Los usuarios que no son administradores no ven el botón y no pueden llamar a ninguno de los endpoints de exportación (el servidor los rechaza).

## 📦 Estructura del paquete de exportación {#export-package-structure}

El archivo ZIP generado se llama `audit-export-<timestamp>.zip` y contiene:

| Archivo | Contenido |
|---|---|
| `INDEX.XML` | Archivo de índice que describe al proveedor de datos, el medio y todas las definiciones de tabla/columna |
| `index.dtd` | El DTD al que hace referencia el archivo de índice; requerido por las herramientas de importación para la validación |
| `emails.csv` | Una fila por cada correo electrónico archivado en el periodo/Buzón seleccionado |
| `attachments.csv` | Opcional; una fila por cada Adjunto de los correos electrónicos exportados |

### Tabla `emails.csv` {#table-emailscsv}

Columnas (sin fila de encabezado; los nombres de las columnas provienen de `INDEX.XML`, como es habitual en este formato):

| Columna | Descripción |
|---|---|
| Id | ID interno del correo electrónico (numérico) |
| MessageId | Encabezado Message-ID del correo electrónico |
| SentDate | Fecha de envío, ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`) |
| ReceivedDate | Fecha de recepción, ISO 8601 UTC |
| IsOutgoing | `1` = saliente, `0` = entrante |
| From | Remitente |
| To | Destinatarios |
| Cc | Destinatarios en CC |
| Bcc | Destinatarios en BCC |
| Subject | Asunto |
| FolderName | Carpeta en la que está archivado el correo electrónico |
| HasAttachments | `1` = con Adjuntos, `0` = sin ellos |
| AttachmentCount | Número de Adjuntos (numérico) |
| AccountEmail | Dirección de correo electrónico de la Cuenta de archivado del Buzón |

### Tabla `attachments.csv` {#table-attachmentscsv}

| Columna | Descripción |
|---|---|
| EmailId | ID interno del correo electrónico al que pertenece el Adjunto (clave de unión a `emails.csv`) |
| FileName | Nombre de archivo del Adjunto |
| ContentType | Tipo MIME |
| Size | Tamaño en bytes (numérico) |
| Sha256 | Hash SHA-256 del contenido (desde el almacenamiento de Adjuntos direccionado por contenido) |

### Convenciones CSV {#csv-conventions}

- Codificación: UTF-8 **sin** BOM (declarado como `UTF8` en `INDEX.XML`)
- Delimitador de columna: `;`
- Encapsulador de texto: `"` (duplicado para escape)
- Delimitador de registro: `\n` (declarado explícitamente en `INDEX.XML`)
- Fechas: ISO 8601, horas convertidas de la zona horaria de visualización configurada a UTC
- Sin fila de encabezado; sin números localizados (las columnas numéricas contienen enteros simples)

## 🖥️ Uso de la página de exportación {#using-the-export-page}

### Campos del formulario {#form-fields}

1. **From / To date** - Periodo a exportar (requerido, intervalo máximo configurable, predeterminado 10 años)
2. **Buzón** - Restringir la exportación a un único Buzón o exportar todos los Buzones
3. **Attachment metadata** - Cuando se marca, se añade `attachments.csv` al paquete
4. **Data supplier name / location / comment** - Se escribe en el bloque `DataSupplier` de `INDEX.XML`; se completa automáticamente desde la sección de configuración `AuditExport` y es editable por cada exportación

### Ciclo de vida del trabajo {#job-lifecycle}

1. Al hacer clic en **Start Export** se valida el periodo y el Buzón, se crea un trabajo y se regresa a la página
2. El trabajo se ejecuta en segundo plano (las exportaciones en cola se procesan una tras otra); la tabla de trabajos muestra el progreso en vivo (correos procesados/totales con una barra de progreso) mediante sondeo automático
3. Cuando la exportación finaliza, el botón **Download** aparece en la fila del trabajo
4. Los trabajos en ejecución o en cola pueden ser **cancelados**

### Historial de exportación {#export-history}

La tabla de historial debajo del formulario enumera las exportaciones más recientes con marca de tiempo, usuario, periodo, Buzón, recuento de correos, estado y tamaño del archivo. El historial se **mantiene en la base de datos** (tabla `AuditExportJobs`), por lo que sobrevive a los reinicios de la aplicación y es seguro para revisiones.

Los archivos de descarga se conservan durante un periodo de Retención configurable (predeterminado 30 días, ver `AuditExport__RetentionDays`); después de eso, los archivos ZIP son eliminados por la limpieza diaria, mientras que las filas del historial permanecen.

> 📝 **Nota**: Los archivos de exportación se almacenan dentro del contenedor de la aplicación. Si necesita conservarlos a largo plazo, descárguelos y almacénelos en su repositorio de evidencia de auditoría.

## 🔐 Auditoría {#auditing}

Cada exportación produce dos entradas en el registro de acceso del nuevo tipo **Audit Data Export**:

1. **Entrada de inicio** - cuando la exportación se pone en cola (con ID de trabajo, periodo, Buzón, opciones como JSON)
2. **Entrada de resultado** - cuando la exportación se completa, falla o se cancela (con ID de trabajo, periodo, Buzón, recuento de correos, resultado y tamaño del paquete como JSON)

Las entradas aparecen en la página de Logs como todas las demás entradas del registro de acceso y pueden filtrarse por el nuevo tipo. El desplegable de filtro de tipo las detecta automáticamente.

## ⚙️ Configuración {#configuration}

La sección `AuditExport` en `appsettings.json` (las variables de entorno usan la sintaxis de doble guion bajo, ej. `AuditExport__RetentionDays`):

| Ajuste | Descripción | Predeterminado |
|---|---|---|
| `DataSupplierName` | Valor predeterminado para el campo de formulario del nombre del proveedor de datos | vacío |
| `DataSupplierLocation` | Valor predeterminado para el campo de formulario de la ubicación del proveedor de datos | vacío |
| `Comment` | Valor predeterminado para el campo de formulario de comentario | vacío |
| `OutputDirectory` | Directorio para los archivos ZIP generados (las rutas relativas se resuelven contra la raíz de contenido de la app) | `exports/audit` |
| `RetentionDays` | Días hasta que se eliminen los archivos de exportación completados | `30` |
| `MaxRangeYears` | Intervalo máximo permitido entre la fecha de inicio y fin del periodo | `10` |

## 🗄️ Base de datos {#database}

El historial de exportación se almacena en la tabla `mail_archiver."AuditExportJobs"` (creada por la migración `MigrateV2609_1`, aplicada automáticamente al iniciar). Ninguna tabla existente es modificada por esta función.

## 🔍 Resolución de problemas {#troubleshooting}

| Problema | Causa / Solución |
|---|---|
| Falta el botón de descarga tras un reinicio | Los archivos de exportación residen en el contenedor; si el contenedor fue recreado o pasó el periodo de Retención, el ZIP ha desaparecido pero la fila del historial permanece. Ejecute la exportación de nuevo. |
| "The period must not exceed X years" | Reduzca el periodo o aumente `AuditExport__MaxRangeYears`. |
| El trabajo permanece en *Running* durante mucho tiempo | Los Archivos grandes se transmiten en lotes de 1000 correos; el progreso se actualiza continuamente. Los periodos muy grandes pueden tardar un tiempo; compruebe el contador de procesados/totales. |
| La herramienta de importación informa problemas de codificación | El paquete es UTF-8 sin BOM con la bandera `UTF8` declarada y un delimitador de registro explícito; asegúrese de que la herramienta lea el `INDEX.XML` junto a los archivos de datos (extraiga todo el ZIP, no abra los archivos individualmente desde dentro del ZIP). |

## 🔗 Documentación relacionada {#related-documentation}

- [Access Logging](Logs.md)
- [Installation, Setup and Parameters](Setup.md)
