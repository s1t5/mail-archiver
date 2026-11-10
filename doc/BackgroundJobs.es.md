# ⚙️ Background Jobs {#background-jobs}

[← Volver al índice de la documentación](index.md)

## 📋 Resumen {#overview}

La mayor parte del trabajo en Mail Archiver no ocurre mientras usted espera. La Sincronización de un Buzón, la importación de un archivo MBOX, la Restauración de miles de correos electrónicos o la exportación de un Archivo pueden tardar de minutos a horas. Esas tareas se ejecutan como **background jobs** — usted las inicia y estas siguen ejecutándose incluso si cierra el navegador.

La página de **Background Jobs** muestra qué se está ejecutando actualmente, qué ha terminado y qué ha fallado. Es el lugar al que acudir cuando algo tarda más de lo esperado o cuando una sincronización termina con errores.

> **ℹ️ Página de administrador.** Background Jobs requiere permisos de administrador y es accesible desde el menú principal. Los usuarios regulares no la ven; aún pueden iniciar tareas (por ejemplo, una restauración por lotes), pero el seguimiento se realiza aquí.

## 🗂️ Tipos de tareas {#job-types}

La página agrupa las tareas por tipo. Cada sección muestra hasta las **20 más recientes**, con las tareas en ejecución y en cola ordenadas al principio, para que una tarea antigua no quede fuera de la vista por las más nuevas.

| Sección | Qué cubre | Iniciado desde |
|---|---|---|
| **Synchronization Jobs** | Obtención de nuevo correo del servidor de correo | Programación automática, o *Sync now* en una Cuenta |
| **MBox Import Jobs** | Importación de archivos `.mbox` | Mail Accounts → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **EML Import Jobs** | Importación de archivos `.eml` / Archivos ZIP | Mail Accounts → [Import EML](Import-EML-MBox.md#-import-eml) |
| **Batch Restore Jobs** | Copiar correos electrónicos seleccionados de vuelta a un Buzón | Archive → select emails → Batch Restore |
| **Account Export Jobs** | Exportar una Cuenta completa (mbox / EML) | Mail Accounts → [Export](Export-Account.md) |
| **Selected Emails Export Jobs** | Exportar un conjunto de correos electrónicos seleccionados a mano | Archive → select emails → Export |
| **Email Deletion Jobs** | Eliminar correos electrónicos del servidor o del Archivo, incluyendo la eliminación masiva basada en filtros | Política de Retención, eliminación manual, o Archive → *Delete all matches* |
| **Account Deletion Jobs** | Eliminar una Cuenta de correo completa | Mail Accounts → Delete |
| **Offload** | Mover correo con un rango de fechas a otra Cuenta | Mail Accounts → Offload |

## 📊 Lectura de una fila de tarea {#reading-a-job-row}

Cada fila contiene el mismo conjunto de columnas:

| Columna | Significado |
|---|---|
| **Job ID** | Identificador corto — cítelo al informar de un problema |
| **Account** | El Buzón en el que trabaja la tarea |
| **Status** | Estado actual, consulte la tabla de abajo |
| **Progress** | Qué tan avanzada está la tarea, con procesados/total cuando se conoce |
| **Created** | Cuándo se puso la tarea en cola |
| **Started** | Cuándo comenzó a ejecutarse realmente |
| **Completed** | Cuándo terminó (vacío mientras aún se está ejecutando) |
| **Duration** | Tiempo transcurrido |

Algunos tipos de tareas añaden contadores a la columna de progreso:

- **Success** / **Failed** — correos procesados con éxito y sin éxito
- **Skipped (duplicates)** — correos ya presentes en el Archivo, no importados dos veces
- **Recovered** — mensajes recuperados tras el informe del servidor de que faltaban
- **Malformed (skipped)** — mensajes que no pudieron ser analizados y fueron omitidos

### Valores de Status {#status-values}

| Status | Significado |
|---|---|
| **Queued** | Esperando para comenzar — otra tarea se está ejecutando, o el worker está ocupado |
| **Running** | Ejecutándose actualmente |
| **Completed** | Finalizado con éxito |
| **CompletedWithErrors** | Finalizado, pero algunos elementos fallaron (importaciones MBox) |
| **Failed** | Finalizado con un error |
| **Cancelled** | Detenido por un usuario |
| **Rate Limited** | *Sync paused due to bandwidth limit. Will resume automatically.* |
| **Timed Out** | La sincronización excedió el límite de tiempo configurado y se detuvo |

> **ℹ️ "Rate Limited" y "Timed Out" son estados exclusivos de la sincronización.** Parecen fallos pero no lo son: una sincronización con límite de velocidad se reanuda por sí sola una vez que la cuota de ancho de banda lo permite, y una sincronización con tiempo agotado continúa desde su punto de control en la siguiente ejecución. Consulte [Rate Limit Handling](RateLimitHandling.md) y [Mail Synchronization](Synchronization.md).

### Por qué el progreso puede quedarse en 0% {#why-progress-can-sit-at-0}

El progreso se mide por **Carpetas completadas**, no por correos electrónicos individuales:

> *El progreso se mide por Carpetas completadas. Una Carpeta grande (p. ej. INBOX) mantiene la barra al 0% hasta que termine.*

En un Buzón donde INBOX contiene la mayor parte del correo, la barra puede permanecer al 0% durante mucho tiempo y luego saltar. Eso es lo esperado — la tarea está trabajando, simplemente aún no ha terminado su primera Carpeta grande.

## 🔄 La página se actualiza sola {#the-page-refreshes-itself}

Mientras la página esté abierta, se recarga cada **30 segundos**, para que pueda dejarla en una pestaña y observar el progreso sin hacer clic en nada. La etiqueta muestra cuándo se actualizaron los datos por última vez.

La recarga se cancela en el momento en que hace clic en un enlace o botón — de lo contrario, su clic caería en una página que está siendo reemplazada debajo de ella.

**Configurable:** `Jobs:RefreshSeconds` en `appsettings.json` (por defecto `30`).
Establézcalo en `0` para desactivar la recarga automática. Cada recarga reconstruye la página desde todas las fuentes de tareas, por lo que un intervalo muy corto en una instalación con muchas Cuentas es una carga real — y esa carga se multiplica con cada pestaña abierta.

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 Cancelar una tarea {#cancelling-a-job}

Las tareas que admiten la cancelación muestran un botón **Cancel Job**, que pregunta:

> *Are you sure you want to cancel this job?*

Las tareas de importación utilizan la redacción **Cancel Import** / *Are you sure you want to cancel this import job?*

La cancelación está disponible para tareas de **sincronización, restauración por lotes, exportación de correos seleccionados y eliminación de correos electrónicos**. La tarea se detiene tan pronto como se termina el elemento actual; el estado pasa a ser **Cancelled**.

> **⚠️ Cancelar no es un rollback.** Los correos electrónicos ya importados, restaurados, exportados o eliminados permanecen así. Cancelar evita que la tarea haga *más* — no deshace lo que ya ha hecho. Para una tarea de eliminación, esto significa que los correos ya eliminados se han ido.

Las tareas de Cuenta y de importación MBox no se pueden cancelar desde esta página; deténgalas desde la propia página de importación de la Cuenta.

## ⚠️ Fallos de sincronización y "Acknowledge Failures" {#sync-failures-and-acknowledge-failures}

Cuando una sincronización termina con el estado **Failed**, la fila de la tarea ofrece la opción **Acknowledge Failures**. Esta es la única acción en la página que cambia el comportamiento futuro, por lo que vale la pena entenderla.

Cada ejecución de sincronización comienza donde terminó la anterior, utilizando un **checkpoint**. Si una sincronización falla para algunos correos electrónicos, el checkpoint se queda detrás de esos correos para que la siguiente ejecución intente procesarlos de nuevo. Ese es el valor predeterminado seguro — nada se omite silenciosamente.

Pero si los fallos son permanentes (un mensaje corrupto, una Carpeta que el servidor se niega a servir), cada sincronización posterior vuelve a intentarlo y la sincronización sigue fallando en el mismo lugar.

**Acknowledge Failures** resuelve ese bloqueo adelantando el checkpoint más allá de los correos fallidos. La confirmación es explícita sobre las consecuencias:

> *This will advance the sync timestamp to the completion time of this job, so the next sync will be a normal incremental sync. The failed emails will not be retried. Continue?*

En caso de éxito verá: *"Sync timestamp has been advanced. The next sync will be incremental."*

> **⚠️ Solo confirme una vez que haya revisado los fallos.** Después de confirmar, esos correos electrónicos **no** se reintentarán — faltarán en el Archivo a menos que los obtenga de otra manera. Compruebe primero los detalles del fallo y, si la causa es una mala configuración, arréglela antes de confirmar. Consulte [Mail Synchronization](Synchronization.md) para conocer el comportamiento del checkpoint en detalle.

La confirmación se registra en el registro de acceso como *SyncAcknowledgeFailures*, con el número de correos fallidos y la Cuenta, para que sea rastreable más tarde.

## 👁️ Lo que no puede hacer aquí {#what-you-cannot-do-here}

- **Sin botón de reintento.** Una tarea fallida no se reinicia desde esta página — inicie la operación de nuevo desde donde la comenzó (sincronización, importación, exportación).
- **Sin edición.** Los parámetros de la tarea son fijos en el momento de la creación.
- **Solo las últimas 20 por tipo.** Las tareas más antiguas no se enumeran. Para registros a largo plazo, el registro de acceso y el registro de la aplicación son las fuentes — consulte [Access Logging](Logs.md) y [Docker Compose Logs](DockerComposeLogs.md).

## ❓ Resolución de problemas {#troubleshooting}

| Síntoma | Causa probable | Qué hacer |
|---|---|---|
| La tarea se queda en **Queued** | Otra tarea del mismo worker se está ejecutando | Espere; compruebe otras secciones para ver si hay una tarea de larga duración |
| El progreso se queda en 0% | Una Carpeta grande todavía se está procesando | Normal — consulte "Por qué el progreso puede quedarse en 0%" |
| La sincronización falla repetidamente (**Failed**) en la misma Cuenta | Errores permanentes en correos individuales | Inspeccione los fallos, corrija la causa y luego confirme |
| La sincronización muestra **Rate Limited** | Se ha alcanzado la cuota de ancho de banda | Nada — se reanuda automáticamente |
| La sincronización muestra **Timed Out** | El Buzón es más grande que el límite de tiempo | Continúa desde el checkpoint en la siguiente ejecución; consulte [Synchronization](Synchronization.md) |
| **CompletedWithErrors** en una importación MBox | Algunos mensajes malformados | Los contadores muestran cuántos; el resto fue importado |
| La tarea desaparece tras recargar | Ha salido de la ventana de las "últimas 20" | Compruebe el registro de la aplicación para ver su estado final |
| La página se recarga mientras trabaja | Actualización automática cada 30 s | Establezca `Jobs:RefreshSeconds` en `0` |

## 📚 Documentación relacionada {#related-documentation}

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — checkpoints, sincronizaciones con tiempo agotado
- [Rate Limit Handling](RateLimitHandling.md) — cuotas de ancho de banda y reanudación automática
- [Access Logging](Logs.md) — quién hizo qué, incluyendo acciones de tareas
- [Batch Restore](MailboxMigration.md) — mover correos entre Buzones
- [Date-Windowed Offload](Offload.md) — tareas de Offload
- [Docker Compose Logs](DockerComposeLogs.md) — lectura del registro de la aplicación
