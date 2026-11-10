# 📤 Date-Windowed Offload {#date-windowed-offload}

[← Volver a la documentación](index.md)

## 📋 Descripción general {#overview}

Una aplicación de offload añade correos archivados más recientes que un punto de corte en otro Buzón, y deja todo lo más antiguo en el Archivo. Existe para migraciones donde el nuevo Buzón debe contener solo el correo reciente mientras que el historial completo permanece accesible a través de Mail Archiver.

Se diferencia de [Copy All Emails to Another Mailbox](MailboxMigration.md) en cinco aspectos:

| | Copy All Emails | Offload |
|---|---|---|
| Filtro de fecha | ninguno, todo o nada | punto de corte, con un límite superior opcional |
| Repetible | no, añade todo de nuevo | sí, omite lo que el destino ya posee |
| Exclusiones de Carpeta | no | sí, configurable |
| Renombrado de Carpeta | no | sí, configurable |
| Vista previa | no | ejecución de prueba (dry run) con informes por Carpeta sin escribir |

El offload siempre añade al destino. Nunca elimina del Archivo y nunca toca el Buzón de origen.

## 🔁 Ejecutar una ejecución repetida es seguro {#repeating-a-run-is-safe}

Antes del primer append, todo el Buzón de destino se indexa y cada mensaje se verifica contra él. Por lo tanto, una ejecución repetida no añade nada que ya haya llegado, y lo informa como ya presente en su lugar. Eso tiene tres consecuencias prácticas:

- Una ejecución interrumpida se recupera simplemente ejecutándola de nuevo.
- Una ejecución finalizada puede repetirse como una fase de verificación. Un resultado correcto es que no se añada nada y que todo esté ya presente.
- Los mensajes eliminados en el destino vuelven en la siguiente ejecución, porque ya no están en el índice.

Dos criterios deciden si un mensaje ya está presente, en este orden:

1. **Message-ID**, normalizado. Esto hace casi todo el trabajo.
2. **Una huella digital (fingerprint)** sobre el remitente, destinatarios, asunto y hora de envío dentro de dos segundos. Esto cubre los mensajes cuyo Message-ID almacenado falta o está mal formado. Tales mensajes no pueden ser restaurados con su Message-ID original en absoluto, por lo que sin este segundo criterio se duplicarían en cada ejecución.

El trabajo informa cuántos coincidencias provinieron de la huella digital por separado, porque es el criterio que podría fallar silenciosamente.

## ⚙️ Configuración {#configuration}

Una nueva sección `Offload`, con valores predeterminados que reproducen el comportamiento anterior:

```json
"Offload": {
  "MaxConcurrentJobs": 1,
  "PrefetchMaxMessages": 500000,
  "ExcludedSourceFolders": [],
  "FolderRenameMap": {},
  "MarkAsSeen": true
}
```

| Clave | Predeterminado | Propósito |
|---|---|---|
| `MaxConcurrentJobs` | `1` | Cuántos trabajos de restauración o offload pueden ejecutarse a la vez. Uno mantiene el comportamiento estrictamente serial que la cola de trabajos siempre tuvo. Como máximo, un trabajo por Buzón de destino se ejecuta independientemente de este valor. |
| `PrefetchMaxMessages` | `500000` | Límite superior de cuántos mensajes se indexan de un Buzón de destino. Por encima de este, la comprobación de duplicados se reduce a una sola Carpeta y registra que lo ha hecho. |
| `ExcludedSourceFolders` | vacío | Carpetas de origen nunca objeto de offload. Se coinciden antes de renombrar, y cubren subcarpetas. |
| `FolderRenameMap` | vacío | Reescribe los segmentos iniciales de una ruta de Carpeta de origen. |
| `MarkAsSeen` | `true` | Si el correo añadido se marca como leído. |

Ambos ajustes de Carpeta se envían vacíos a propósito. Reescribir o eliminar Carpetas sin que se solicite sorprendería a cualquiera que ya esté usando la ruta de restauración, por lo que una ejecución sin configuración migra todo, incluyendo carpetas de spam, y crea un segundo conjunto de carpetas especiales junto a las propias del destino.

### Exclusiones y renombramiento de Carpetas {#folder-exclusions-and-renaming}

Para una migración de Exchange a Dovecot, los dos ajustes suelen ser así:

```json
"ExcludedSourceFolders": ["Junk E-Mail", "Deleted Items"],
"FolderRenameMap": {
  "Sent Items": "Sent",
  "Deleted Items": "Trash",
  "Junk E-Mail": "Junk"
}
```

Sin el mapa de renombramiento, cada Buzón migrado termina con tanto `Sent` como `Sent Items`, porque Dovecot pre-crea sus propias carpetas de uso especial.

Hay cuatro cosas sobre cómo se aplican que vale la pena saber, porque cada una es una forma de llevarse una sorpresa:

1. **Las exclusiones se coinciden primero**, contra el nombre de origen. Si el renombramiento ocurriera primero, una exclusión en `Deleted Items` nunca se activaría una vez que ya se hubiera convertido en `Trash`.
2. **El renombramiento reescribe el prefijo de ruta de coincidencia más largo**, en los límites de los segmentos. `Sent Items` convirtiéndose en `Sent` también convierte `Sent Items/2019` en `Sent/2019`, y deja `Sent Items Archive` intacta.
3. **La coincidencia no distingue entre mayúsculas y minúsculas**, y tanto `/` como `\` cuentan como separadores.
4. **Dos Carpetas de origen pueden colapsar en un solo destino.** `Sent Items` y `Sent` convirtiéndose ambos en `Sent` está bien y no abre la Carpeta de destino dos veces.

Cambiar el mapa de renombramiento entre dos ejecuciones no duplica nada, porque la comprobación de duplicados cubre todo el Buzón de destino en lugar de una Carpeta resuelta.

## 🖥️ Ejecución desde la interfaz {#running-it-from-the-interface}

En la página de detalles de una Cuenta, **Offload to Another Mailbox** abre un formulario con el Buzón de destino, la Carpeta raíz de destino, si se preserva la estructura de Carpetas, la ventana de fecha, un interruptor de ejecución de prueba, y si se marca el correo añadido como leído. Las exclusiones configuradas y el mapa de renombramiento se muestran de solo lectura, por lo que una ejecución nunca es una sorpresa.

El trabajo se pone en cola y su progreso y el informe por Carpeta aparecen en la página de estado del trabajo y bajo **Jobs**. El informe por Carpeta nombra las Carpetas de origen, por lo que solo se muestra al usuario que inició el trabajo y a los administradores.

El Buzón de destino puede ser una Cuenta IMAP deshabilitada, por ejemplo, para provisionar un objetivo de migración antes de que esté en línea; la ejecución falla ruidosamente al conectar si el destino es inalcanzable. Las Cuentas de Microsoft 365 no pueden ser objetivos de offload; la ruta de restauración de Graph no cambia.

### Quién puede ejecutarlo {#who-may-run-it}

Los administradores pueden realizar offload entre cualquier par de Cuentas. Un gestor autónomo puede realizar offload **solo entre las Cuentas asignadas a él**, en ambos extremos: el origen, porque la página de la Cuenta ya está delimitada de esa manera, y el destino, porque la lista de destinos se reduce al mismo conjunto y la solicitud se verifica contra él de nuevo cuando se inicia el trabajo. Un gestor autónomo con una sola Cuenta asignada, por lo tanto, no tiene un destino elegible y se le informa de ello.

La delimitación proviene de `IAccountAccessResolver`, el mismo resolvedor que la API REST y el servidor MCP utilizan, por lo que hay una única definición de quién puede usar qué Buzón. La decisión en sí reside en `OffloadTargetEligibility`, que tanto la lista de destino como la verificación de la solicitud llaman, por lo que lo que ofrece el formulario y lo que acepta el servidor no pueden desviarse.

## ⌨️ Ejecución desde la línea de comandos {#running-it-from-the-command-line}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll \
  --offload --source-account-id 3 --target-account-id 7 \
  --since 2025-08-01 \
  [--until 2026-08-01] [--target-folder INBOX] \
  [--preserve-folders] [--dry-run] [--no-mark-seen]
```

| Argumento | Significado |
|---|---|
| `--source-account-id` | Cuenta de la que se lee el correo archivado. Requerido. |
| `--target-account-id` | Cuenta IMAP en la que se añade. Requerido, y debe ser distinta al origen. |
| `--since` | Límite inferior inclusivo en la fecha de envío, `YYYY-MM-DD`. Requerido. |
| `--until` | Límite superior inclusivo opcional. |
| `--target-folder` | Carpeta raíz en el Buzón de destino. Por defecto `INBOX`. |
| `--preserve-folders` | Recrear la estructura de Carpetas de origen debajo de la raíz. |
| `--dry-run` | Informar de lo que sucedería, sin añadir nada. |
| `--no-mark-seen` | Añadir sin la bandera Seen. |

Códigos de salida:

| Código | Significado |
|---|---|
| `0` | Todo se añadió o ya estaba presente. |
| `1` | Al menos un mensaje falló, o la ejecución misma falló. |
| `2` | La invocación fue incorrecta: argumentos erróneos o faltantes, cuenta desconocida, el destino no es IMAP, el origen es igual al destino. |

Una invocación maneja un Buzón, lo que hace que un grupo de Buzones sea programable desde un CSV de origen, destino y punto de corte.

> **Ejecutar varios a la vez.** La regla de un trabajo por destino solo existe dentro de un único proceso, por lo que dos invocaciones de línea de comandos simultáneas contra el *mismo* Buzón de destino podrían cada una tomar su índice de duplicados antes de que la otra comience a escribir, y luego ambas podrían añadir el mismo correo. Particione el trabajo para que cada invocación simultánea tenga un destino diferente, lo cual es el caso normal cuando cada fila de un CSV es un Buzón distinto.

## 🗓️ Cómo se interpreta el punto de corte {#how-the-cutoff-is-interpreted}

- El filtro es sobre **la fecha de envío**, no sobre cuándo se archivó un mensaje. La marca de tiempo de archivado es la hora de la ejecución de archivado y no tiene significado para un punto de corte.
- Una ventana relativa como "los últimos doce meses" se **resuelve a una fecha fija cuando se crea el trabajo**. Un trabajo puede ejecutarse durante mucho tiempo y repetirse después, y tiene que seleccionar el mismo correo cada vez; una ventana relativa almacenada se desviaría.
- Los puntos de corte se interpretan en la **zona horaria de visualización configurada**, la misma que muestra la pantalla de búsqueda, y el límite inferior es inclusivo desde las 00:00 de ese día. Un límite superior incluye todo su día. Las ventanas relativas ("los últimos N meses") también se resuelven en esa zona horaria de visualización, coincidiendo con cómo se almacena `SentDate`.

### Limitación conocida {#known-limitation}

- Cuando el índice de duplicados llega a `Offload:PrefetchMaxMessages`, el alcance se reduce y se registra en lugar de reintentar contra una sola Carpeta. El fallback de `restrictToFolder` que ofrece el constructor de índices actualmente no es usado por la ruta de offload; aumentar el límite es el remedio soportado.

## 📋 Una migración, de principio a fin {#a-migration-end-to-end}

1. Crear los Buzones de destino. Para muchos a la vez, vea la importación masiva de CSV en [Account Import](Account-Import.md).
2. Configurar `Offload:ExcludedSourceFolders` y `Offload:FolderRenameMap`. Ambos están vacíos por defecto, por lo que saltarse esto migra las carpetas de spam y crea carpetas especiales duplicadas.
3. Confirmar que cada Buzón de origen esté totalmente archivado antes de realizar el offload.
4. **Realizar una prueba de todo.** Comprobar los recuentos y los nombres de las Carpetas de destino resueltas antes de escribir nada.
5. Ejecución real. Comience con `MaxConcurrentJobs` en 2 y elévelo solo después de observar cómo se comporta el servidor de destino; Dovecot limita las conexiones simultáneas por usuario y por IP.
6. Repetir la ejecución para verificar. No se debe añadir nada y todo debe estar ya presente.
7. Deshabilitar las Cuentas de origen.

El rendimiento está limitado por `BatchOperation:PauseBetweenEmailsMs`, que es de 50 ms por defecto y permite como máximo veinte adiciones por segundo antes de que se cuente la latencia del servidor.

## 🔍 Qué significan los contadores {#what-the-counters-mean}

| Contador | Significado |
|---|---|
| Appended | Escrito en el destino. En una ejecución de prueba, lo que se habría escrito. |
| Already present | El destino ya lo poseía, por cualquiera de los dos criterios. |
| of those by fingerprint | Cuántos fueron reconocidos por el segundo criterio en lugar de por Message-ID. |
| Excluded folder | Omitida porque la Carpeta de origen está excluida. |
| Failed | No se pudo añadir. |

Se informa de una advertencia si el índice de duplicados no cubrió todo el Buzón de destino, lo que ocurre por encima de `PrefetchMaxMessages` o cuando no se pudo leer una Carpeta. En ese estado, una ejecución repetida puede añadir correo que ya está presente en las partes que no fueron indexadas.

## ⚠️ Notas y límites {#notes-and-limits}

- El correo restaurado ahora lleva su **fecha de entrega original** como la fecha interna de IMAP, tomada de la cadena `Received` del mensaje y recurriendo a la fecha de envío. Anteriormente llevaba la hora de la ejecución de archivado, lo que rompía el orden del lado del servidor por llegada y cualquier regla basada en la edad en el destino.
- Debido a que una ejecución repetida omite lo que ya está presente, una fecha interna incorrecta no puede corregirse volviendo a ejecutar: los mensajes son reconocidos y omitidos. Asegúrese de que esto sea correcto antes de una ejecución de producción, o elimine los mensajes afectados en el destino y ejecute de nuevo.
- Microsoft 365 no es compatible como objetivo de offload.
- La detección de duplicados solo puede reconocer lo que puede ver en el Buzón de destino. El correo que llegó al destino por otra ruta solo se detecta en la medida en que los dos criterios coincidan con él.
