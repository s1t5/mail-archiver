# 📤 Exporting a Mail Account (EML / MBox) {#exporting-a-mail-account-eml-mbox}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

La exportación de la Cuenta escribe **cada mensaje de una cuenta** en un
Archivo ZIP descargable. Es la contraparte de la
[web import](Import-EML-MBox.md): obtiene un Archivo portátil de la Cuenta en un
formato que cualquier cliente de correo o herramienta de migración puede leer.

Se ofrecen dos formatos:

| Format | What you get | Typical use |
|--------|--------------|-------------|
| **EML** | Archivos `.eml` individuales, organizados en Carpetas | Re-importar en un cliente de correo, procesamiento por mensaje, legibilidad a largo plazo |
| **MBox** | Archivos de Buzón tradicionales — **un archivo por Carpeta** | Herramientas y clientes que leen MBox, procesamiento de texto masivo |

En ambos casos, los mensajes se escriben como mensajes RFC-822 sin procesar, por lo que los
**Adjuntos están incluidos** en la exportación.

> 💡 **Tip**: Para exportar solo una selección de mensajes en lugar de una Cuenta completa,
> use las acciones masivas en [Mail search](Search.md#-acting-on-many-results-at-once).

## 🧭 Access {#access}

1. Abra **Mail Accounts** en el menú principal.
2. Haga clic en el botón **⋮** (más acciones) en la fila de la Cuenta y elija
   **Export**. La misma entrada se encuentra en la página de **Details** de la Cuenta.

La exportación requiere el rol de **admin** o **self manager** — los usuarios estándar ven
la entrada pero no pueden usarla. La exportación siempre cubre la Cuenta completa; no hay
filtro de fecha o Carpeta.

## 🖥️ Using the Export Page {#using-the-export-page}

La página muestra los recuentos **totales**, **entrantes** y **salientes** de mensajes de la Cuenta,
para que pueda ver el tamaño del trabajo antes de comenzarlo. Luego:

1. Elija el formato — **EML** o **MBox**.
2. Presione **Start Export**.

La exportación se ejecuta **en segundo plano**. Se le dirigirá a una página de estado que muestra el
ID del trabajo, el formato elegido, las marcas de tiempo y los recuentos de mensajes procesados/totales.

### Job statuses {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

Cuando el trabajo alcanza el estado **Completed**, aparece un **botón de descarga** en la página de estado.
El mismo trabajo también se enumera en la página [Jobs](BackgroundJobs.md).

> ⚠️ **Los archivos de exportación se limpian automáticamente.** El ZIP se elimina **después de una
> descarga exitosa**, y en cualquier caso **después de 7 días**. Descargue el archivo mientras
> todavía se ofrece — el Archivo en sí no se ve afectado, simplemente puede iniciar una
> nueva exportación más tarde.

## ⚠️ Things to watch out for {#things-to-watch-out-for}

- **Las Cuentas grandes tardan un tiempo.** Un Buzón grande puede necesitar varios minutos y el
  ZIP puede volverse muy grande, dependiendo del volumen de mensajes y los Adjuntos.
  Debido a que el trabajo se ejecuta en segundo plano, puede cerrar el navegador y volver
  a la página de estado más tarde.
- **Espacio en disco del servidor.** El ZIP se ensambla en el servidor antes de que lo descargue,
  así que asegúrese de que haya suficiente espacio libre junto al Archivo.
- **"Export file not found"** — el trabajo aún no ha terminado, o el archivo ya ha sido
  descargado o eliminado por la limpieza de 7 días. Inicie una nueva exportación.
- **Sin opciones de filtro** — la exportación es todo o nada por Cuenta. Use la exportación de la
  página de búsqueda para un subconjunto.
- **No es una exportación de cumplimiento.** Para una entrega de medios de datos lista para auditoría con un
  archivo de índice, use [Audit Data Export](AuditExport.md) en su lugar — produce un
  paquete en el formato de datos masivos tabulares que esperan las herramientas de auditoría.

## 🔗 Related Documentation {#related-documentation}

- [Importing Mail in the Web UI](Import-EML-MBox.md) — la dirección opuesta
- [Mail search](Search.md) — exportar una selección de mensajes
- [Background Jobs](BackgroundJobs.md) — lista de trabajos, progreso y cancelación
- [Audit Data Export](AuditExport.md) — paquetes de exportación listos para auditoría
- [CLI commands](CLI.md) — importar desde un directorio montado
