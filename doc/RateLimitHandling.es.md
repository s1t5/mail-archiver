# 📊 Manejo de Límites de Velocidad {#rate-limit-handling}

[← Volver al Índice de Documentación](index.md)

## 📋 Descripción general {#overview}

Muchos proveedores de correo electrónico imponen límites de ancho de banda al acceso IMAP. Estos límites suelen oscilar entre unos pocos cientos de megabytes y varios gigabytes por período de 24 horas. Al realizar la Sincronización de Buzones grandes que superan estos límites, el proceso de sincronización normalmente fallaría o quedaría incompleto.

Mail Archiver incluye un sistema integrado de manejo de límites de velocidad que gestiona automáticamente estas limitaciones, garantizando una Sincronización fiable incluso para Buzones grandes.

## ⚙️ Cómo funciona {#how-it-works}

### Seguimiento automático de ancho de banda {#automatic-bandwidth-tracking}

El sistema supervisa continuamente la cantidad de datos descargados durante la Sincronización IMAP. Cuando se alcanza el límite de ancho de banda configurado:

1. **La Sincronización se pausa automáticamente**: La operación de sincronización actual se detiene de forma controlada.
2. **El progreso se guarda**: Los puntos de control almacenan la posición exacta donde se detuvo la sincronización.
3. **Actualizaciones de estado**: La Cuenta muestra el estado "Rate-Limited" en el panel de control.
4. **Reanudación automática**: Cuando el límite se restablece (normalamente después de 24 horas), la sincronización se reanuda desde el punto de control guardado.

### Ejemplo: Buzón de 14 GB con límite diario de 2.5 GB {#example-14-gb-mailbox-with-25-gb-daily-limit}

```
Day 1:  Sync starts → ~2.5 GB downloaded → Rate-Limited
        Status: Rate-Limited, Checkpoint saved

Day 2:  Limit still active → Sync skipped

Day 3:  Limit resets → Sync resumes from checkpoint
        → Additional ~2.5 GB → Rate-Limited again

Day 4-6: Continues...

Day N:   All 14 GB synced → Status: Completed
```

## 🔧 Configuración {#configuration}

### Habilitar el seguimiento de ancho de banda {#enabling-bandwidth-tracking}

Añada la siguiente sección a su `appsettings.json`:

```json
{
  "BandwidthTracking": {
    "Enabled": true,
    "DailyLimitMb": 2500,
    "WarningThresholdPercent": 80,
    "PauseHoursOnLimit": 24
  }
}
```

### Opciones de configuración {#configuration-options}

| Opción | Descripción | Predeterminado | Ejemplo |
|--------|-------------|---------|---------|
| `Enabled` | Habilitar/deshabilitar el seguimiento de ancho de banda | `false` | `true` |
| `DailyLimitMb` | Límite de descarga diario en megabytes | `2500` | `2500` |
| `WarningThresholdPercent` | Porcentaje del límite para activar advertencias en los registros | `80` | `80` |
| `PauseHoursOnLimit` | Horas para pausar cuando se alcanza el límite | `24` | `24` |

### Establecer el límite correcto {#setting-the-correct-limit}

Para determinar el valor de `DailyLimitMb` apropiado:

1. Consulte la documentación o los términos de servicio de su proveedor de correo electrónico.
2. Busque límites de ancho de banda IMAP o cuotas de descarga.
3. Contacte con su administrador de correo electrónico si utiliza un sistema de correo corporativo.
4. Comience con un valor conservador si no está seguro (p. ej., 2000 MB).
5. Supervise los registros en busca de advertencias de límite de velocidad durante las sincronizaciones iniciales.

## 👤 Experiencia del usuario {#user-experience}

### Indicadores del panel de control {#dashboard-indicators}

Cuando el límite de velocidad está activo, la Cuenta muestra:

- **Estado**: "Rate-Limited" 
- **Banner de información**: Explica la situación y el tiempo estimado de restablecimiento.
- **Progreso de Sincronización**: Muestra los correos electrónicos procesados antes de alcanzar el límite.

### Comportamiento automático {#automatic-behavior}

1. **No requiere intervención manual**: El sistema lo gestiona todo automáticamente.
2. **Reinicios de la aplicación**: Si la aplicación se reinicia, el estado del límite de velocidad se preserva en la base de datos.
3. **Múltiples Cuentas**: Cada Cuenta realiza el seguimiento de su ancho de banda de forma independiente.

### Qué se guarda {#what-gets-saved}

Cuando ocurre un límite de velocidad, el sistema guarda:

- **Puntos de control de Sincronización**: Posición exacta en cada Carpeta donde se detuvo la sincronización.
- **Uso de ancho de banda**: Total de bytes descargados para el día actual.
- **Estado del límite**: Si se ha alcanzado el límite y cuándo se restablecerá.

## 🗄️ Mantenimiento de la base de datos {#database-maintenance}

El `DatabaseMaintenanceService` limpia automáticamente los datos antiguos:

- **Registros de ancho de banda**: Se eliminan después de 7 días.
- **Puntos de control de Sincronización**: Los puntos de control completados se eliminan después de 30 días.
- **Puntos de control incompletos**: Se preservan hasta que la sincronización se completa.

## ✅ Mejores prácticas {#best-practices}

### Para Buzones grandes {#for-large-mailboxes}

1. **Habilitar el seguimiento de ancho de banda**: Es esencial para Buzones más grandes que el límite diario.
2. **Planificar la Sincronización inicial**: Un Buzón grande puede tardar varios días en sincronizarse por completo.
3. **Supervisar el progreso**: Consulte el panel de control para ver el estado del límite de velocidad.
4. **No interrumpir**: Deje que la reanudación automática se encargue de la continuación.

### Para administradores {#for-administrators}

1. **Establecer límites apropiados**: Ajuste el `DailyLimitMb` a los límites reales de su proveedor de correo electrónico.
2. **Supervisar los registros**: Los mensajes de advertencia aparecen al 80% del límite.
3. **Espacio de base de datos**: El seguimiento del ancho de banda utiliza un almacenamiento adicional mínimo.
4. **Copia de seguridad antes de los cambios**: Realice siempre una Copia de seguridad antes de modificar la configuración del límite de velocidad.

### Consideraciones de rendimiento {#performance-considerations}

- ✅ **Impacto en la memoria**: Insignificante - solo realiza el seguimiento de bytes, no almacena correos electrónicos.
- ✅ **Impacto en la base de datos**: Mínimo - los registros pequeños se limpian automáticamente.
- ✅ **Velocidad de Sincronización**: Sin impacto en el rendimiento durante la sincronización activa.

## 🛡️ Errores FETCH transitorios / Throttling del lado del servidor {#transient-fetch-errors-server-side-throttling}

Además del seguimiento de ancho de banda basado en bytes descrito anteriormente, Mail Archiver también detecta y reacciona al **throttling por mensaje** señalizado por el propio servidor IMAP. Algunos proveedores de IMAP alojados aplican un throttling por Cuenta o por IP que es **independiente de las pausas de `BatchOperation` configuradas**, porque el throttling se aplica en el lado del servidor basándose en el volumen total de solicitudes durante la sesión actual.

### Síntomas {#symptoms}

El servidor devuelve una respuesta `NO` (o `BAD`) a un único comando `FETCH` para el cuerpo de un mensaje, con mensajes tales como:

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- respuestas que contienen `throttle`, `rate limit` o marcadores similares.

La autenticación, el listado de Carpetas y la propia búsqueda de mensajes suelen tener éxito; solo falla la recuperación real del mensaje completo.

### Manejo automático {#automatic-handling}

Cuando se detecta un error transitorio de este tipo, Mail Archiver aplica una estrategia de reintento por mensaje:

1. **Reintento con retroceso exponencial**: Hasta **3 reintentos** para el mismo mensaje, con retrasos de **5 s → 15 s → 60 s**.
2. **Recuperación de la conexión**: Antes de cada reintento, se validan y restauran el estado de la conexión, la autenticación y la selección de Carpeta si es necesario.
3. **Restablecimiento tras éxito**: Un FETCH exitoso restablece el contador de fallos consecutivos.
4. **Pausa de sincronización controlada**: Si **10 mensajes consecutivos** siguen fallando con errores transitorios después de todos los reintentos, la ejecución de sincronización actual se pausa de forma controlada, utilizando el mismo mecanismo que la pausa por límite de ancho de banda:
   - La marca de tiempo `LastSync` **no** se actualiza.
   - Si `BandwidthTracking` está habilitado, los puntos de control se preservan para que la siguiente ejecución de sincronización se reanude desde el último mensaje procesado con éxito.
   - El trabajo se marca como con límite de velocidad.
   - La siguiente sincronización programada se reanudará automáticamente.

### Mitigación {#mitigation}

Si se encuentra regularmente con throttling del lado del servidor, reduzca la tasa de solicitudes ajustando su configuración de `BatchOperation`:

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

Tenga en cuenta que algunos proveedores aplican el throttling basándose en el **total de bytes** o el **total de FETCHs por ventana de tiempo** en lugar de la tasa por segundo. En esos casos:

- Habilite `BandwidthTracking` (ver arriba) y configure `DailyLimitMb` para que coincida con el límite documentado del proveedor. La Sincronización se pausará limpiamente cuando se agote el presupuesto diario y se reanudará automáticamente al día siguiente.
- Las sincronizaciones iniciales de Buzones muy grandes pueden tardar varios días, lo cual es el comportamiento previsto.

### Qué se considera un error transitorio {#what-counts-as-a-transient-error}

Solo las respuestas IMAP `NO`/`BAD` con texto o códigos de respuesta relacionados con el throttling se tratan como transitorios. Otros errores (mensajes mal formados, problemas de decodificación UTF-8, etc.) se siguen contando como `FailedEmails` inmediatamente y **no** activan la lógica de reintento/pausa.

## 📚 Documentación relacionada {#related-documentation}

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Procedimientos de limpieza de la base de datos
- [GmailBestPractices.md](GmailBestPractices.md) - Configuración específica para Gmail
