# 💾 Visualización de Almacenamiento por Cuenta {#per-account-storage-display}

[← Volver al Índice de Documentación](index.md)

## 📋 Resumen {#overview}

La tabla "Account Overview" del Dashboard y la tabla "Show All" de MailAccounts incluyen una columna de **Storage**. Esta columna muestra cuánto espacio de base de datos está utilizando cada Cuenta, incluyendo **todos los campos de cada correo archivado** más **todos los Adjuntos**.

## ⚙️ Cómo funciona {#how-it-works}

Los valores de almacenamiento están en la caché en la tabla de base de datos `AccountStorageCache`. Esto significa que las páginas web leen valores precalculados en lugar de ejecutar una consulta costosa cada vez que abres el Dashboard o la lista de cuentas. La caché se mantiene actualizada mediante un servicio de fondo independiente (`AccountStorageRefreshService`).

Para cada Cuenta, el servicio solicita a PostgreSQL que sume `pg_column_size` para cada columna de las filas de la Cuenta en la tabla `ArchivedEmails`. Esto cubre **todos los campos de un correo** (Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash, etc.), incluyendo la compresión de PostgreSQL y el almacenamiento fuera de línea (TOAST). El tamaño de un valor fuera de línea se lee de su puntero TOAST, por lo que el cálculo solo escanea la tabla en sí y nunca carga los cuerpos de los correos. La parte de los Adjuntos se añade por separado como la suma lógica de `EmailAttachment.Size`.

El servicio de actualización actualiza la caché en tres situaciones:

1. **Backfill de inicio** — Después de una nueva instalación o tras actualizar a la versión que introduce esta función, cada Cuenta comienza sin un valor en caché. El servicio recorre todas las Cuentas una por una y calcula el primer valor.
2. **Actualización diaria** — Una vez al día en la hora configurada (por defecto `02:30`), el servicio vuelve a calcular todas las Cuentas como medida de seguridad.
3. **Actualización inmediata** — Después de una Sincronización de correo, importación de EML, importación de MBOX o eliminación de Retención, la Cuenta afectada se vuelve a calcular de inmediato para que el valor mostrado se mantenga al día.

## 🤔 Por qué se muestra "0 B" al principio {#why-0-b-is-shown-at-first}

Cuando se habilita la función por primera vez, o cuando se crea una nueva Cuenta, la fila de la caché para esa Cuenta aún no existe. El servicio de actualización se ejecuta en segundo plano y calcula las Cuentas **una por una**, con un pequeño retraso entre cada Cuenta para que los Archivos muy grandes no sobrecarguen la base de datos.
Hasta que el servicio haya procesado una Cuenta, la interfaz muestra **"0 B"** como marcador de posición. Esto es completamente normal. Tan pronto como el servicio de fondo termine con esa Cuenta, el valor real aparecerá la próxima vez que se cargue o se actualice la página.
Para Archivos existentes grandes, el backfill puede tardar varios minutos o más. El servicio rastrea su progreso en una tabla dedicada (`AccountStorageBackfillState`), por lo que si la aplicación se reinicia en medio del backfill, simplemente continúa donde se quedó; ninguna Cuenta se calcula dos veces.

## 🔧 Configuración {#configuration}

La función se puede ajustar en `appsettings.json` o mediante variables de entorno. Consulte la sección **Account Storage Settings** en [Setup.md](Setup.md) para ver todas las opciones disponibles, incluyendo:

- `AccountStorage__Enabled`: Activa o desactiva el servicio de actualización (`true` por defecto).
- `AccountStorage__DailyExecutionTime`: Tiempo para la actualización completa diaria (`02:30` por defecto).
- `AccountStorage__BackfillDelayMs`: Pausa entre Cuentas durante el backfill de inicio (`5000` ms por defecto).
- `AccountStorage__RefreshBatchDelayMs`: Pausa entre Cuentas durante la actualización diaria (`1000` ms por defecto).
- `AccountStorage__CommandTimeoutSeconds`: Tiempo de espera del comando de la base de datos para el cálculo de almacenamiento por Cuenta (`300` segundos por defecto). Aumente esto para Archivos muy grandes donde el escaneo `pg_column_size` tarda más.

> 💡 **Consejo**: No es necesario habilitar `DatabaseMaintenance__Enabled` para que esta función funcione. El servicio de actualización de almacenamiento se ejecuta de forma independiente.
