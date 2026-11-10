# 🖥️ CLI Commands {#cli-commands}

[← Volver al índice de la documentación](index.md)

## 📋 Overview {#overview}

Algunas tareas no pertenecen a la interfaz web: importar un Archivo existente desde el sistema de archivos local o mover mensajes originales almacenados entre los backends de almacenamiento. Mail Archiver acepta ambos como comandos en la línea de comandos.

| Command | Purpose |
|----------|---------|
| `--import-mbox` | Importar un archivo MBox desde un directorio montado |
| `--import-eml` | Importar un Archivo ZIP de archivos EML desde un directorio montado |
| `--migrate-raw-storage` | Mover mensajes originales almacenados entre la base de datos y el sistema de archivos |

Todos los comandos se gestionan antes de que comience el pipeline web y finalizan cuando terminan, por lo que es seguro ejecutarlos con `docker compose exec` mientras el Archivo se está ejecutando.

## ⚠️ Modelo de seguridad {#security-model}

Los comandos de importación son **solo CLI**; no existe un endpoint web para navegar o importar archivos locales. Debe tener acceso al host de Docker (`docker exec`) para ejecutarlos.

Una **lista blanca de rutas** (`LocalImport:AllowedPaths`) controla qué directorios puede leer el importador. Los archivos fuera de estas rutas son rechazados.

`--migrate-raw-storage` no está sujeto a esa lista blanca: solo lee los archivos de mensajes que el propio Archivo escribió bajo `RawMessages:FileSystem:BasePath`.

## 🛠️ Configuración {#setup}

### 1. Configurar Rutas Permitidas {#1-configure-allowed-paths}

En su `docker-compose.yml`, establezca las rutas de importación permitidas:

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. Montar sus Archivos {#2-mount-your-files}

Monte el directorio que contiene sus archivos MBox/EML en el contenedor:

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

Reemplace `/home/user/mbox-archives` con la ruta real en su host de Docker.

### 3. Reiniciar el Contenedor {#3-restart-the-container}

```bash
docker compose down && docker compose up -d
```

## 📝 Uso {#usage}

### Encuentre el ID de su Cuenta de destino {#find-your-target-account-id}

Antes de importar, necesita el ID de la base de datos de la Cuenta de correo de destino. Puede encontrarlo a través de la Web UI:

Navegue hasta "Email Accounts", abra los detalles de la Cuenta de destino; el ID de la Cuenta se muestra en la lista de Cuentas o en la URL al visualizar una Cuenta.

### Importar un archivo MBox {#import-an-mbox-file}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**Parámetros:**
| Parámetro | Requerido | Descripción |
|-----------|----------|-------------|
| `--import-mbox` | Sí | Señala una operación de importación MBox |
| `--file <path>` | Sí | Ruta al archivo MBox dentro del contenedor |
| `--account-id <id>` | Sí | ID de la base de datos de la Cuenta de correo de destino |
| `--folder <name>` | No | Nombre de la Carpeta de destino (por defecto: `INBOX`) |

### Importar archivos EML (Archivo ZIP) {#import-eml-files-zip-archive}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**Parámetros:**
| Parámetro | Requerido | Descripción |
|-----------|----------|-------------|
| `--import-eml` | Sí | Señala una operación de importación EML |
| `--file <path>` | Sí | Ruta a un **Archivo ZIP** que contiene archivos `.eml` dentro del contenedor |
| `--account-id <id>` | Sí | ID de la base de datos de la Cuenta de correo de destino |

> **Importante:** `--import-eml` espera un **Archivo ZIP** que contenga archivos `.eml`; **no** acepta archivos `.eml` individuales. Para importar archivos EML sueltos, empaquételos primero en un Archivo ZIP:
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **Nota para `dotnet run`:** Al usar `dotnet run` (en lugar de la DLL publicada), separe los argumentos de dotnet de los argumentos de la aplicación con `--`:
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> El `--` le indica a `dotnet run` que todos los argumentos siguientes pertenecen a su aplicación, no al compilador de .NET.

## 📊 Ejemplo de salida {#example-output}

```
Target account: user@example.com (ID: 1)

=== Local MBox Import ===
File: /data/import/myarchive.mbox
Size: 245.32 MB
Target Account ID: 1
Target Folder: INBOX

[Importing... progress logged to container logs]

=== Import Results ===
Status: Completed
Total Emails: 15234
Imported Successfully: 15120
Failed: 42
Skipped (malformed): 28
Skipped (duplicates): 44
Duration: 00:12:45
```

## 🔍 Monitorización {#monitoring}

Durante la importación, se escribe el progreso detallado en los registros del contenedor:

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ Referencia de configuración {#configuration-reference}

| Variable de entorno | Predeterminado | Descripción |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | Primera ruta permitida para importaciones locales |
| `LocalImport__AllowedPaths__1` | (vacío) | Segunda ruta permitida (opcional) |

Añada más rutas incrementando el índice (`__2`, `__3`, etc.).

## 🚫 Manejo de errores {#error-handling}

| Error | Causa | Solución |
|-------|-------|----------|
| `File not found` | La ruta `--file` no existe en el contenedor | Verifique la ruta del archivo y el montaje del volumen |
| `File path is not in an allowed import directory` | El archivo está fuera de las rutas permitidas | Mueva el archivo a una ruta permitida o agregue su directorio a `LocalImport:AllowedPaths` |
| `Mail account with ID X not found` | ID de Cuenta incorrecto | Verifique el ID de la Cuenta a través de la Web UI o una consulta de base de datos |
| `Invalid account-id` | Valor no numérico para `--account-id` | Use un ID de base de datos numérico |

## 🔒 Mejores prácticas de seguridad {#security-best-practices}

1. **Use montajes de solo lectura** cuando sea posible:
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **Restrinja AllowedPaths** solo a los directorios que realmente utilice.

3. **Elimine los montajes de volumen** una vez que la importación se haya completado para mantener una superficie de seguridad limpia.

4. **Nunca exponga** el directorio de importación a través del servidor web; no existe un endpoint web para importaciones locales.

## 📦 Formatos de importación de un vistazo {#import-formats-at-a-glance}

| CLI Flag | Formato de entrada | Extensión de archivo típica | Correo electrónico individual | Lote |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | Archivo MBox | `.mbox`, `.mbx` | ❌ (siempre en lote) | ✅ |
| `--import-eml` | Archivo ZIP de EMLs | `.zip` | ❌ (se requiere ZIP) | ✅ |
| `--migrate-raw-storage` | Mensajes originales almacenados | — | — | ✅ |

*Los archivos `.eml` individuales no son compatibles directamente. Use `zip` para envolverlos, o impórtelos a través de la [carga web](Import-EML-MBox.md).*

## 🔧 Comandos de mantenimiento {#maintenance-commands}

### Mover mensajes originales almacenados {#moving-stored-original-messages}

Cuando `RawMessages:Enabled` está activado, el mensaje original de cada correo electrónico se conserva ya sea en la base de datos o en archivos, dependiendo de `RawMessages:Storage`. Este comando mueve los originales ya almacenados de un backend a otro; consulte [Original Message Storage](RawMessages.md#storage-backend) para la configuración.

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

| Argumento | Predeterminado | Descripción |
|----------|---------|-------------|
| `--from` | `RawMessages:Storage` configurado | Backend del cual se mueven los mensajes. |
| `--to` | `RawMessages:Storage` configurado | Backend al que se mueven los mensajes. |
| `--batch` | `200` | Mensajes por transacción de base de datos. |

A diferencia de los comandos de importación, este no está restringido por `LocalImport:AllowedPaths`: solo lee los archivos de mensajes que el propio Archivo escribió bajo `RawMessages:FileSystem:BasePath`.

**Lo que garantiza:**

- Cada mensaje se verifica contra la suma de comprobación (checksum) registrada en el momento del Archivo antes de ser copiado. Un mensaje que falle se deja exactamente donde está y se informa — un original corrupto nunca se propaga y su única copia restante nunca se elimina.
- Un archivo se elimina solo después de que la transacción de la base de datos que lo movió se haya confirmado, por lo que una ejecución interrumpida no puede perder la única copia.
- La ejecución es reanudable: el trabajo se selecciona mediante el almacenamiento registrado en cada mensaje, por lo que al iniciar el comando de nuevo se continúa con lo que aún está pendiente.
- Un mensaje defectuoso no aborta la ejecución. El código de salida es `1` si algún mensaje falló, `0` en caso contrario.

**Ejemplo de salida:**

```text
Batch: 200 moved, 0 skipped, 0 failed
Batch: 200 moved, 0 skipped, 0 failed
Batch: 42 moved, 0 skipped, 1 failed
Done: 442 moved, 1 failed.
  Email 12345: checksum mismatch (recorded 4f1c..., file has 9ab2...)
```

Las lecturas siempre siguen el backend registrado en cada mensaje, no la configuración establecida, por lo que una descarga sigue funcionando durante y después de una migración. Cambiar `RawMessages:Storage` solo afecta al correo recién archivado — ejecute este comando para mover lo que ya está almacenado.

> **Copia de seguridad:** con el backend del sistema de archivos, los archivos de mensajes residen fuera de la base de datos. Realice una Copia de seguridad de `RawMessages:FileSystem:BasePath` junto con el volcado de la base de datos; una Restauración sin los archivos deja originales que no pueden ser servidos.

## ❓ FAQ {#faq}

**P: ¿Por qué `--import-eml` requiere un archivo ZIP?**
R: El servicio de importación de EML subyacente (`EmlImportService`) procesa Archivos ZIP internamente utilizando la API `ZipFile` de .NET. Este diseño permite realizar importaciones masivas de manera eficiente. Para archivos `.eml` individuales, envuélvalos en un Archivo ZIP (p. ej., `zip emails.zip *.eml`) o utilice la [carga web](Import-EML-MBox.md).

**P: ¿Por qué no puedo navegar por los archivos desde la interfaz web?**
R: Este es un diseño de seguridad deliberado. Esta función se implementa intencionadamente a través de CLI para interacciones a nivel de sistema porque los usuarios normales no tienen acceso directo al nivel del SO; solo interactúan con la interfaz web.

Para evitar riesgos de seguridad como ataques de salto de directorio y la exposición no autorizada del sistema de archivos, los usuarios regulares tienen restringido el uso de la interfaz web para la carga e importación de archivos. Esto garantiza que no puedan eludir la interfaz prevista para acceder al sistema de archivos del contenedor subyacente.
