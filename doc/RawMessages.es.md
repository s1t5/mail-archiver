# 📧 Original Message Storage {#original-message-storage}

[← Volver al índice de la documentación](index.md)

## 📋 Resumen {#overview}

Por defecto, el Archivo conserva las **partes analizadas** de un correo electrónico: encabezados, cuerpos de texto y HTML, y Adjuntos. Un `.eml` exportado se reconstruye a partir de esos campos.

Eso es suficiente para leer un correo electrónico, pero no es el mensaje **tal como se recibió**. Las codificaciones de transferencia, los límites MIME y el plegado de encabezados difieren del original, por lo que un mensaje reconstruido ya no verifica su firma DKIM y los escáneres de spam lo califican de forma diferente.

Con el almacenamiento del mensaje original habilitado, el Archivo conserva el mensaje completo tal como llegó, junto con los campos analizados. El original almacenado es lo que el Archivo puede demostrar que fue entregado.

| | Solo campos analizados | Con el original almacenado |
| --- | --- | --- |
| Encabezados, cuerpo, Adjuntos | ✅ | ✅ |
| Mensaje exacto en bytes | ❌ | ✅ |
| La firma DKIM se verifica | ❌ | ✅ |
| Uso forense (tribunal, auditoría) | limitado | ✅ |

## 🎯 Dónde se sirve el original {#where-the-original-is-served}

Una vez almacenado, el original se devuelve donde sea que se espere un mensaje completo:

| Lugar | Qué sucede |
| --- | --- |
| Botón **Export EML** en la página de detalles del email | Sirve el original almacenado cuando existe, de lo contrario, recurre a la reconstrucción para los correos electrónicos archivados sin uno. |
| **Restauración a un Buzón** | Añade el original almacenado cuando existe, de lo contrario, recurre a la reconstrucción para los correos electrónicos archivados sin uno. |
| `GET /api/v1/emails/{id}/raw` | Descarga de REST API, consulte la [guía de REST API](API.md#download-original-message). |
| `get_email_raw` | Herramienta MCP, consulte la [guía de MCP](MCP.md#get_email_raw). |

La respuesta le indica qué forma recibió, de modo que un llamador puede distinguir un original genuino de una reconstrucción sin inspeccionar los bytes:

| Señal | Significado |
| --- | --- |
| `X-Raw-Message-Source: imap` | Copia exacta del servidor IMAP (`BODY[]` tal como se entregó). |
| `X-Raw-Message-Source: graph` | Copia exacta, el Exchange de contenido MIME devuelto. |
| `X-Raw-Message-Source: eml-file` | Copia exacta, el contenido del archivo `.eml` importado. |
| `X-Raw-Message-Source: serialized` | Mensaje analizado y vuelto a escribir. Idéntico excepto que los finales de línea son CRLF (importación de MBox, recuperación de IMAP). |
| `X-Raw-Message-Sha256` | SHA-256 del original almacenado. |
| `X-Raw-Message-Reconstructed: true` | No hay original almacenado; el mensaje fue reconstruido. Sin cabecera de checksum. |
| `X-Raw-Message-Storage` | Backend del cual se leyó el original (`postgres` o `filesystem`). |

El nombre del archivo también lo indica: `email-{id}-original-...eml` para un original almacenado, `email-{id}-...eml` para una reconstrucción.

Cuando un original almacenado **falla su checksum**, la exportación y la restauración son rechazadas con un mensaje de error. Devolver la reconstrucción en su lugar ocultaría que la copia archivada fue alterada, que es exactamente lo que un Archivo nunca debe hacer.

Una restauración lee el original almacenado y lo añade a través del cliente del proveedor, por lo que el Buzón recibe los encabezados originales y la estructura MIME. El flujo de bytes exacto no está garantizado en el cable porque el cliente serializa el mensaje de nuevo; la copia archivada en sí misma permanece sin cambios.

## ⚙️ Settings {#settings}

Todos los ajustes residen en la sección `RawMessages`, o como variables de entorno con el prefijo `RawMessages__`.

| Setting | Default | Description |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Almacena el mensaje original de cada correo electrónico recién archivado. Los correos archivados antes de activar esta opción no tienen original. |
| `RawMessages:MaxMessageSizeMb` | `50` | Los mensajes más grandes que este se archivan sin su original. `0` = sin límite. |
| `RawMessages:IncludeGraph` | `true` | También almacena el original para las Cuentas de Microsoft 365 (Graph). Cuesta una solicitud Graph adicional por mensaje, lo que cuenta contra el presupuesto de limitación de velocidad. |
| `RawMessages:Storage` | `postgres` | Dónde se guardan los bytes del mensaje: `postgres` (en la base de datos) o `filesystem` (archivo gzip junto al Archivo). Ver [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Directorio donde se escriben los archivos de mensaje, relativo a la raíz de contenido de la aplicación. Apunte esto a un volumen montado en Docker. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Número de niveles de directorio de dos caracteres en los que se distribuye el id del correo electrónico. `2` genera 10.000 directorios, `0` es plano. Mantiene bajo el recuento de archivos por directorio en Archivos grandes. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Vuelve a calcular el SHA-256 del archivo en cada descarga y lo compara con la suma de comprobación registrada en el momento del Archivo. Un desajuste hace que la descarga falle con HTTP 500 en lugar de servir bytes alterados. |

### Sizing {#sizing}

El almacenamiento crece aproximadamente con el tamaño de cada correo, incluyendo sus Adjuntos, comprimidos con gzip. Los Adjuntos **no** se someten a Deduplicación dentro del original almacenado, por lo que un correo electrónico con un Adjunto grande cuesta ese Adjunto dos veces: una vez en el almacén de Adjuntos con Deduplicación y otra vez dentro del original. Planifique para aproximadamente el **doble** del tamaño de la base de datos de un Archivo sin originales.

Habilitar el ajuste no rellena el correo existente. Solo los correos electrónicos recién archivados obtienen un original.

## 💾 Backend de almacenamiento {#storage-backend}

El ajuste `Storage` decide dónde residen los bytes. La **fila de metadatos siempre permanece en
la base de datos**, independientemente del backend configurado, y contiene el SHA-256, el tamaño, la
fuente de captura y (para archivos) la ruta relativa. Esa fila es lo que permite al Archivo demostrar lo que un
mensaje almacenado debe contener.

### postgres (default) {#postgres-default}

El mensaje comprimido se encuentra en la columna `Content` de `mail_archiver."RawMessages"`.
Este es el comportamiento que el Archivo siempre ha tenido y no requiere un volumen adicional.

Elija esto cuando la base de datos sea ya su unidad de Copia de seguridad y desee tener una sola cosa para realizar la
Copia de seguridad y la Restauración.

### filesystem {#filesystem}

El mensaje se escribe en `{BasePath}/{xx}/{yy}/{id}.eml.gz`, donde `xx`/`yy` se derivan
del id del correo electrónico. La fila de la base de datos conserva únicamente los metadatos.

Elija esto cuando el Archivo sea lo suficientemente grande como para que los originales dominen el tamaño de la base de datos, o cuando desee colocar los archivos de mensajes en un almacenamiento más económico o separado.

!!! danger "Monte un volumen en Docker"
    El `BasePath` predeterminado (`RawMessages`) se resuelve en `/app/RawMessages` **dentro del
    contenedor**. Sin un bind mount, ese directorio pertenece a la capa escribible del contenedor, por lo que cada contenedor recreado - incluyendo `docker compose down && docker compose
    up -d`, una actualización de imagen, o un reinicio del host con una capa efímera - se lleva cada original almacenado
    con él, mientras que las filas de la base de datos sobreviven.

    Siempre monte el directorio en el host cuando utilice el backend de filesystem:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    La ruta del host (`./raw-messages` aquí) es lo que usted realiza la Copia de seguridad. La ruta del contenedor debe
    coincidir con `RawMessages:FileSystem:BasePath`, o establecer `RawMessages:FileSystem:BasePath` en
    el punto de montaje que eligió. Si mueve el montaje, mantenga `BasePath` apuntando a él: los valores
    de `StoragePath` almacenados son relativos, por lo que siguen resolviéndose.

### Cambio de backends {#switching-backends}

Las lecturas siguen el almacenamiento registrado en cada fila, no el ajuste configurado. Después de
cambiar `Storage`, los mensajes archivados anteriormente siguen siendo descargables desde dondequiera que estén.
El ajuste solo afecta al correo **recién** archivado.

Para mover lo que ya está almacenado, ejecute la migración de almacenamiento. Consulte los
[CLI commands](CLI.md#moving-stored-original-messages) para la referencia completa:

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Lo que la migración garantiza:

- Cada mensaje es verificado contra su checksum registrado antes de ser copiado. Un mensaje
  que falla se deja donde está y se reporta: un original corrupto nunca se propaga y
  su única copia restante nunca se elimina.
- Un archivo se elimina solo después de que la transacción de la base de datos que lo movió se haya confirmado, por lo que una ejecución
  interrumpida no puede perder la única copia.
- La ejecución es reanudables. El trabajo se selecciona mediante la columna `StorageType`, por lo que iniciar el
  comando de nuevo continúa con lo que aún está pendiente.
- El código de salida es `1` si algún mensaje falló, `0` en caso contrario.

Una migración no es necesaria para que el Archivo funcione. Solo es necesaria para mover los bytes existentes
al otro backend.

## 🛟 Copia de seguridad y restauración {#backup-and-restore}

=== "postgres backend"
    Nada extra. Los originales forman parte de la base de datos y están cubiertos por el
    dump normal, ver [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Realice una Copia de seguridad del directorio `BasePath` **junto con** el dump de la base de datos.

    Una restauración de la base de datos sin los archivos deja el Archivo con filas cuyo contenido
    no se puede servir. La descarga entonces responde con un HTTP 404 en lugar de devolver algo
    incorrecto, y los correos electrónicos en sí permanecen legibles desde sus campos analizados.

## 🗑️ Eliminar un correo electrónico {#deleting-an-email}

Eliminar un correo electrónico elimina también su original: las reglas de Retención, la eliminación manual individual y masiva, y la eliminación de una Cuenta de correo completa, eliminan el archivo como parte de la misma operación. Con el backend `postgres` no hay nada extra que hacer, porque los bytes viajan dentro de la fila que la cascada ya elimina.

El archivo se elimina **después** de que se confirma la fila del correo electrónico, nunca antes. Los dos pasos no pueden ser una sola transacción, porque un archivo no es parte de la transacción de la base de datos, por lo que uno de ellos tiene que ser el segundo. Este orden es el seguro: una eliminación que falla o se revierte, por ejemplo, en un registro bloqueado, deja el original intacto, y un error entre los dos pasos deja un archivo sin una fila, el cual el barrido de abajo reclama. El otro orden destruiría el original de un correo electrónico que aún existe.

**Los archivos huérfanos se recolectan.** Cualquier cosa que se haya escapado, incluidos los archivos dejados por eliminaciones de antes de que existiera esta limpieza, es eliminado por un barrido periódico de `BasePath` que compara cada archivo con las filas y elimina aquellos a los que ningún correo electrónico hace referencia:

| Setting | Default | Meaning |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Activa o desactiva el barrido |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Con qué frecuencia se ejecuta |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Los archivos más jóvenes que esto nunca se tocan |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Retraso antes de la primera ejecución, para que una migración de almacenamiento pendiente termine primero |

El periodo de gracia es lo que hace que el barrido sea seguro: una importación escribe un archivo antes de confirmar la fila que apunta a él, y un archivo escrito hace unos momentos no debe confundirse con un huérfano. Los archivos `.tmp` escritos a medias debido a una escritura interrumpida se recolectan bajo la misma regla. Con el backend `postgres` el barrido no hace nada y ni siquiera recorre el directorio.

## 🔍 Verificando un Archivo {#verifying-an-archive}

La suma de comprobación (checksum) registrada en el momento del Archivo es lo que hace posible la verificación.

- **Al descargar** (`VerifyChecksumOnRead=true`, el predeterminado) el archivo se procesa mediante un hash y se compara. Una discrepancia rechaza la descarga con un error HTTP 500.
- **En la migración de almacenamiento** cada mensaje se verifica antes de ser copiado, en ambas direcciones.

Debido a que la fila de metadatos está en la base de datos y los bytes pueden estar en el disco, una comparación de la suma de comprobación es la única forma de notar que un archivo fue cambiado fuera de la aplicación.

## ⚠️ Cosas que debe saber {#things-to-know}

- **La activación no realiza un relleno de datos.** Solo los correos archivados después del cambio obtienen un original.
- **La reconstrucción no es el mensaje.** Para los correos electrónicos sin un original almacenado, cada
  ruta de descarga devuelve un mensaje reconstruido y lo indica mediante
  `X-Raw-Message-Reconstructed: true`.
- **Filesystem significa dos unidades de Copia de seguridad.** La base de datos y el directorio de mensajes deben restaurarse
  juntos; de lo contrario, las filas sobreviven sin su contenido.
- **`VerifyChecksumOnRead` consume CPU.** Cada descarga de un mensaje grande se descomprime
  y se le aplica un hash. Déjelo activado a menos que haya detectado un problema; es la única protección
  contra la alteración silenciosa en el disco.
- **El original almacenado no tiene Deduplicación.** Un Adjunto que aparece en muchos correos electrónicos
  se almacena una vez en el almacén de Adjuntos, pero una vez por cada correo electrónico dentro del original.
- **Al eliminar, también se elimina el original.** Un correo electrónico eliminado por Retención o manualmente se lleva
  consigo su original almacenado, tanto el archivo como la fila. No se conserva nada que el Archivo ya no
  enumere.

## 🔗 Véase también {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - la migración de almacenamiento completa
- [Setup and Parameters](Setup.md) - todos los parámetros de configuración
- [REST API](API.md#download-original-message) - el endpoint `/raw`
- [MCP Server](MCP.md#get_email_raw) - la herramienta `get_email_raw`
- [Backup and Restore](BackupRestore.md) - qué realizar en la Copia de seguridad y Restauración
- [Attachment Deduplication](AttachmentDeduplication.md) - cómo se almacenan los Adjuntos mediante Deduplicación
