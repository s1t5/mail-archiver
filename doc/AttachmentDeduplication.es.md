# 🧬 Deduplicación de Adjuntos {#attachment-deduplication}

[← Volver al Índice de la Documentación](index.md)

## 📋 Descripción general {#overview}

Los Adjuntos de correo se almacenan como cargas útiles binarias en la base de datos. El **mismo archivo**
se adjunta con mucha frecuencia a muchos correos electrónicos (logotipos de empresas en firmas,
PDFs reenviados, boletines, contratos, ...). Sin la Deduplicación, cada Adjunto mantiene
su propia copia privada de esos bytes, lo que desperdicia una gran cantidad de espacio en la base de datos.

La **Deduplicación de Adjuntos** almacena cada carga útil *única* exactamente una vez en una
tabla de direccionamiento por contenido (identificada por el hash SHA-256 de los bytes) y permite que todos
los Adjuntos idénticos hagan referencia a esa única copia compartida. Esto reduce típicamente la
huella de almacenamiento de los Archivos con muchos Adjuntos de manera significativa.

## ⚙️ Cómo funciona {#how-it-works}

### Nuevos Adjuntos {#new-attachments}
Cada vez que se archiva un nuevo correo electrónico (IMAP, Microsoft 365 / Graph, importación de EML, importación de MBOX, ...) la carga útil del Adjunto se procesa mediante un hash y se almacena una vez. Si la misma carga útil ya existe, el nuevo Adjunto simplemente hace referencia a la fila de contenido existente. Esto sucede de forma transparente en la capa de datos, por lo que cada ruta de archivado está cubierta
automáticamente.

### Datos existentes (migración) {#existing-data-migration}
Para instalaciones que ya contienen Adjuntos archivados **antes** de que se introdujera esta función, una **migración en segundo plano robusta y reanudable** convierte las cargas útiles en línea existentes en filas de contenido compartidas:

- Procesa los Adjuntos **por lotes**, cada lote en su propia transacción.
- El progreso se persiste en una tabla de estado dedicada, por lo que la migración **sobrevive a un
  reinicio de la aplicación** y simplemente continúa donde se quedó.
- Es **idempotente**: los Adjuntos ya migrados nunca se vuelven a tocar, incluso
  después de una caída o un reinicio.
- Se ejecuta en segundo plano y no bloquea el uso normal de la aplicación.

Cuando la migración termina, registra un mensaje de finalización, por ejemplo:

```
Attachment Deduplication migration COMPLETED. 36199 attachments processed in 1.6 minutes.
```

En el siguiente inicio, detecta que no queda nada por hacer y permanece inactiva. Si algún
Adjunto sin deduplicar llegara a quedar (por ejemplo, tras la Restauración de un
Archivo más antiguo), la migración se reanuda automáticamente y los migra; el sistema siempre converge
a un estado totalmente deduplicado.

### Recolección de basura (siempre activa) {#garbage-collection-always-on}
Cuando se eliminan correos electrónicos (y, por lo tanto, Adjuntos), una carga útil compartida puede quedar
sin referencias. Una recolección de basura integrada elimina estas filas de contenido huérfanas.

> 🧹 **La limpieza de huérfanos SIEMPRE se ejecuta**, independientemente de la
> función `DatabaseMaintenance`. Se ejecuta una vez justo después de la migración y luego
> periódicamente (cada `OrphanCleanupIntervalHours`, por defecto 12 h). Cuando
> `DatabaseMaintenance__Enabled=true`, la misma limpieza se ejecuta adicionalmente antes de la
> `VACUUM` diaria para que el espacio liberado se reclame físicamente.

### Uso de disco después de la migración (importante) {#disk-usage-after-the-migration-important}

> 📈 **El valor "Storage" en el panel de control puede SUBIR brevemente justo después de la
> migración** (por ejemplo, 3.5 GB → 5.3 GB). Esto es esperado y no es un error.
>
> El panel de control informa el tamaño **físico** de la base de datos (`pg_database_size`). Cuando la
> migración mueve una carga útil al almacenamiento compartido, establece la antigua copia en línea como `NULL`,
> pero PostgreSQL (MVCC) mantiene esos bytes antiguos como *dead tuples* hasta que el archivo de la tabla es
> reescrito; por lo tanto, por un momento la base de datos contiene tanto la copia antigua como la nueva.
>
> Para solucionar esto automáticamente, la aplicación ejecuta un `VACUUM FULL` de **una sola vez**
> en la tabla de Adjuntos **una vez que la migración se ha completado y mientras no haya
> trabajos de Sincronización en ejecución**. Esto reescribe la tabla y devuelve el espacio liberado al
> sistema operativo; después, el tamaño reportado cae **por debajo** del original. Se ejecuta
> exactamente una vez (rastreado mediante un marcador `ReclaimedAt`), es seguro ante reinicios y se pospone
> mientras haya una Sincronización en progreso (reintenta automáticamente cuando está inactiva).

>
> ⚠️ El `VACUUM FULL` bloquea la tabla de Adjuntos durante su duración (no hay lecturas/escrituras de Adjuntos
> mientras tanto) y necesita algo de espacio libre en disco para la reescritura. Si desea
> activarlo manualmente en su lugar, ejecute:
> ```sql
> VACUUM (FULL, ANALYZE) mail_archiver."EmailAttachments";
> ```

## 🔧 Configuración {#configuration}

Estos ajustes son opcionales; los valores por defecto son sensatos para la mayoría de las instalaciones. Consulte también la
[Guía de Configuración](Setup.md#-attachment-deduplication-settings).

| Ajuste | Por defecto | Descripción |
|---------|---------|-------------|
| `AttachmentDeduplication__BatchSize` | `200` | Número de Adjuntos existentes migrados por transacción. Los valores más grandes migran más rápido pero aumentan la carga de memoria/DB por lote. |
| `AttachmentDeduplication__DelayBetweenBatchesMs` | `0` | Pausa opcional (ms) entre los lotes de migración para limitar la carga de la DB en sistemas con mucha actividad. |
| `AttachmentDeduplication__StartupDelaySeconds` | `20` | Retraso (s) tras el inicio de la app antes de que comience la migración (permite que la migración del esquema termine primero). |
| `AttachmentDeduplication__OrphanCleanupIntervalHours` | `12` | Intervalo (h) de la recolección de basura de huérfanos siempre activa. |
| `AttachmentDeduplication__CommandTimeoutSeconds` | `300` | Tiempo de espera del comando de la base de datos (en segundos) para las operaciones de lote de migración. El valor por defecto es 5 minutos. Si un lote sigue excediendo el tiempo de espera, el servicio reintenta automáticamente con la mitad del tamaño del lote. |

Ejemplo (`docker-compose.yml`):

```yaml
environment:
  - AttachmentDeduplication__BatchSize=200
  - AttachmentDeduplication__DelayBetweenBatchesMs=0
  - AttachmentDeduplication__StartupDelaySeconds=20
  - AttachmentDeduplication__OrphanCleanupIntervalHours=12
  - AttachmentDeduplication__CommandTimeoutSeconds=300
```

## ❓ FAQ {#faq}

**¿Puedo desactivar la Deduplicación?**
No. Es una función de almacenamiento central y siempre está activa. Solo puede ajustar el tamaño del lote y los parámetros de programación anteriores.

**¿Están seguros mis datos durante la migración?**
Sí. Cada lote es una única transacción y el trabajo es idempotente. Una caída o un
reinicio a mitad de la migración revierte el lote incompleto y este se vuelve a procesar en el
siguiente inicio; no se pierden ni se duplican datos.

**¿Ralentiza el archivado?**
La sobrecarga de hashing por Adjunto es insignificante. La migración de una sola vez de los
datos existentes se ejecuta en segundo plano y puede limitarse con
`DelayBetweenBatchesMs` si es necesario.

**La migración sigue excediendo el tiempo de espera y reiniciándose en el mismo cursor. ¿Qué puedo hacer?**
Esto puede suceder en bases de datos muy grandes (cientos de miles de correos electrónicos) donde un solo lote de 200 Adjuntos con hashing SHA-256 excede el tiempo de espera de comando por defecto. El servicio ahora reintenta automáticamente con la mitad del tamaño del lote cuando ocurre un tiempo de espera. Si sigue viendo tiempos de espera, puede reducir manualmente el tamaño del lote y/o aumentar el tiempo de espera del comando:

```yaml
environment:
  - AttachmentDeduplication__BatchSize=10
  - AttachmentDeduplication__CommandTimeoutSeconds=600
```

**¿Cuánto espacio ahorraré?**
Eso depende enteramente de cuántos Adjuntos duplicados contenga su Archivo.
Los Archivos con muchas firmas repetidas, logotipos o archivos reenviados son los que más se benefician.

---
