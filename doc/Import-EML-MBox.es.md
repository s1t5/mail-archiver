# 📥 Importación de correo en la Web UI (EML / MBox) {#importing-mail-in-the-web-ui-eml-mbox}

[← Volver al índice de la documentación](index.md)

## 📋 Resumen {#overview}

Además de la [command line interface](CLI.md) y la
[importación de Cuenta mediante CSV](Account-Import.md), Mail Archiver puede importar un
Archivo de correo existente **a través de la interfaz web**. Existen dos formularios de carga:

| Formulario | Archivo aceptado | Resultado |
|------|---------------|--------|
| **Import MBox** | `.mbox`, `.mbx` | Cada mensaje en el archivo se importa en **una** Carpeta que usted elija |
| **Import EML** | `.zip` que contiene archivos `.eml` | La **estructura de Carpetas dentro del ZIP** decide dónde llega cada mensaje |

Ambos formularios archivan los mensajes en una Cuenta de Mail Archiver existente, ambos se ejecutan como
**trabajos en segundo plano** y ambos **omiten duplicados automáticamente** — importar
el mismo archivo dos veces no crea una segunda copia.

> 💡 **Consejo**: Si su Archivo ya está en el servidor como un archivo, prefiera el
> [CLI](CLI.md). Evita la carga del navegador y es la
> mejor opción para Archivos de varios gigabytes.

## 🧭 Acceso {#access}

1. Abra **Mail Accounts** en el menú principal.
2. Use el desplegable **Import** sobre la lista de cuentas y elija
   **Import MBox** o **Import EML**.

Los formularios están disponibles para **administradores** (todas las cuentas) y **gestores propios**
(las cuentas asignadas a ellos); consulte
[User Management](UserManagement.md#-user-account-permissions). Solo las cuentas **habilitadas**
se ofrecen como objetivos de importación — una cuenta deshabilitada no puede ser seleccionada.

## 📨 Import MBox {#import-mbox}

MBox es el formato utilizado por la mayoría de los clientes de correo para Carpetas exportadas, y por
copias de seguridad estilo `mbox`.

| Campo | Significado |
|-------|---------|
| **MBox File** | El archivo `.mbox` / `.mbx` para cargar |
| **Target Account** | La Cuenta de Mail Archiver que recibe los mensajes |
| **Target Folder** | La Carpeta en la que se importan los mensajes. Elija una de la lista de Carpetas existentes de la Cuenta, o escriba un nombre nuevo — una Carpeta que aún no existe se
  **crea automáticamente** durante la importación. Por defecto es `INBOX`. |

Todos los mensajes en el archivo se importan en esa única Carpeta de destino. El archivo MBox
en sí no contiene información de Carpetas, por lo que la estructura del Buzón
original no se reconstruye — si necesita eso, use la ruta EML/ZIP.

## 🗂️ Import EML {#import-eml}

La importación EML espera un **Archivo ZIP** cuyos elementos son archivos `.eml`.

- Los archivos `.eml` en la **raíz** del ZIP se importan en `INBOX`.
- Los archivos `.eml` en una **subcarpeta** se importan en una Carpeta con el **mismo
  nombre** que esa subcarpeta.

Así, un ZIP organizado de esta manera:

```
mailbox.zip
├── 2024-invoice.eml            → INBOX
├── Archive/
│   └── old-message.eml         → Archive
└── Projects/
    └── 2026/
        └── kickoff.eml         → Projects/2026
```

restaura el árbol de Carpetas junto con los mensajes. Los archivos `.eml` individuales pueden ser
cargados envolviéndolos primero en un ZIP (`zip emails.zip *.eml`).

## ⚙️ Comportamiento compartido {#shared-behaviour}

Ambos formularios se comportan de la misma manera una vez que presiona **Start Import**:

- **Límite de tamaño de archivo** — configurado con `Upload__MaxFileSizeGB` (por defecto **10 GB**).
  El formulario muestra el límite actual junto al selector de archivos.
- **Procesamiento en segundo plano** — la carga se pone en cola como un trabajo; no tiene que
  mantener el navegador abierto.
- **Gestión de duplicados** — los mensajes que ya están en el Archivo se omiten
  y se cuentan por separado. Los duplicados se comparan mediante el encabezado `Message-ID`.
- **Entrada malformada** — los archivos ilegibles se omiten individualmente y se informan en
  la página de estado en lugar de abortar toda la importación.
- **Progreso** — se le lleva directamente a la página de estado del trabajo, que muestra el
  ID del trabajo, el nombre del archivo, el tamaño del archivo, el estado, los mensajes procesados/totales y los recuentos en ejecución. El mismo trabajo también aparece en la página [Jobs](BackgroundJobs.md).
- **Cancelación** — una importación en curso puede cancelarse desde su página de estado. Los
  mensajes importados hasta ese punto permanecen en el Archivo.
- **Rastro de auditoría** — la importación se escribe en el [access log](Logs.md), y el
  valor de almacenamiento por Cuenta se actualiza después (consulte
  [Per-Account Storage](AccountStorage.md)).

### Estados del trabajo {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled`

Un trabajo finalizado informa cuántos mensajes se importaron, cuántos fallaron y cuántos
se omitieron como duplicados — por ejemplo:
*"1420 imported, 3 failed, 87 duplicates"*. En una importación exitosa, el archivo cargado
se elimina del servidor; las entradas fallidas se conservan durante la vida útil del
trabajo para que pueda ver qué fue rechazado.

## ⚠️ Cosas a tener en cuenta {#things-to-watch-out-for}

- **Tipo de archivo incorrecto** — la carga es rechazada a menos que la extensión sea una de
  `.mbox`, `.eml` o `.zip`. El selector del formulario EML solo acepta `.zip`, el del formulario
  MBox solo `.mbox` y `.mbx`.
- **"0 imported, N duplicates"** — esto no es un error. Los mensajes ya están
  en el Archivo, muy probablemente de una importación o Sincronización anterior de la misma fuente.
- **Archivos muy grandes** — un archivo MBox se lee mensaje por mensaje, por lo que un
  archivo de varios gigabytes tarda un tiempo incluso en segundo plano. Observe el progreso en
  la página de estado en lugar de volver a cargarlo.
- **Espacio libre en disco** — el archivo cargado se almacena temporalmente en el servidor y
  necesita espacio junto a su base de datos de Archivo.
- **Nombres de Carpeta con caracteres especiales** — los nombres de las Carpetas se toman de las rutas del ZIP
  o de su entrada; verifique la lista de Carpetas de la Cuenta después de la importación si
  utilizó caracteres inusuales.

## 🔗 Documentación relacionada {#related-documentation}

- [CLI commands](CLI.md) — importar desde un directorio montado
- [Account Import (CSV)](Account-Import.md) — añadir muchas Cuentas a la vez
- [Background Jobs](BackgroundJobs.md) — monitorear y cancelar trabajos de importación
- [Exporting a Mail Account](Export-Account.md) — la dirección opuesta
- [Setup and Parameters](Setup.md) — ajustes de `Upload__*`
