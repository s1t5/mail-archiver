# 🧪 Entorno de prueba local {#local-test-environment}

[← Volver al índice de la documentación](index.md)

## 📋 Descripción general {#overview}

Esta guía describe cómo ejecutar la suite de pruebas, la aplicación y un objetivo IMAP en una máquina de desarrollo. Está dirigida a colaboradores: el stack que levanta es deliberadamente inseguro y no es una guía de despliegue. Para el despliegue, consulte la
[Guía de configuración](Setup.md).

Hay tres piezas involucradas:

| Pieza | Proporcionado por | Propósito |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | Dos bases de datos, una para `dotnet test` y otra para la aplicación |
| Dovecot | `docker-compose.test.yml` | Un objetivo IMAP que actúa como un Buzón de mailcow |
| La aplicación | `dotnet run` en el host | Más rápido para iterar que reconstruir la imagen del contenedor |

## 🚀 Iniciando el stack {#starting-the-stack}

```bash
docker compose -f docker-compose.test.yml up -d
```

Esto publica todo únicamente en loopback:

| Servicio | Dirección de Host | Notas |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5433, no 5432, para que no haya conflicto con un servidor local |
| Dovecot, plain | `127.0.0.1:1143` | La autenticación en texto plano está habilitada |
| Dovecot, TLS | `127.0.0.1:1993` | El certificado autofirmado de la imagen |

Para detenerlo y descartar todas las bases de datos y el correo almacenado:

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ Las dos bases de datos {#the-two-databases}

La suite de pruebas deja filas de fixtures: después de una ejecución completa, la base de datos de prueba contiene varias docenas de cuentas de correo y usuarios. Si la aplicación compartiera esa base de datos, su Sincronización en segundo plano tomaría esos fixtures e intentaría repetidamente alcanzar servidores que no existen. Por lo tanto, el contenedor de postgres crea dos bases de datos en la primera inicialización (`tests/postgres/10-create-dev-db.sql`):

| Base de datos | Utilizada por | Configurada en |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

Ambos archivos de configuración están en el gitignore y deben crearse localmente.

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

`appsettings.Development.json` en la raíz del repositorio:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` importa fuera del contenedor: por defecto es la ruta absoluta
`/app/DataProtection-Keys`, que no existe en una máquina de desarrollo, y sin ella cada
solicitud falla al leer el llavero de claves. `IgnoreSelfSignedCert` es lo que permite que la aplicación
acepte el certificado de Dovecot en el puerto 1993.

## ✅ Ejecución de las pruebas {#running-the-tests}

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

Las migraciones se aplican automáticamente mediante `TestDbFixture` en la primera ejecución. La mayoría de las pruebas no requieren ninguna base de datos; el resto utiliza PostgreSQL, por lo que fallarán con `Name or service not known` si el stack no se está ejecutando.

También se puede proporcionar una cadena de conexión a través del entorno, lo cual anula cada archivo de configuración:

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ Ejecución de la aplicación {#running-the-application}

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` ya establece `ASPNETCORE_ENVIRONMENT=Development` para
`dotnet run`, por lo que un `dotnet run --project MailArchiver.csproj` simple también funciona y sirve
http://localhost:5179 (el perfil `http`). `ASPNETCORE_URLS` arriba solo elige el puerto del contenedor
5000 en su lugar.

Las migraciones pendientes se aplican a `MailArchiverDev` al inicio. Inicie sesión con las credenciales de `appsettings.json`
(`Authentication:Username` y `Authentication:Password`).

## 📬 Uso del objetivo Dovecot {#using-the-dovecot-target}

Dovecot acepta **cualquier nombre de usuario** con la contraseña `pass`, por lo que un nuevo Buzón de objetivo no requiere aprovisionamiento: elija un nombre e inicie sesión. Añádalo en Mail Accounts con:

| Field | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | anything, e.g. `offload-target@example.com` | same |
| Password | `pass` | same |

La configuración en `tests/dovecot/dovecot.conf` está modelada según el valor predeterminado de la propia imagen, con
tres diferencias deliberadas que hacen que se comporte como un objetivo de mailcow:

- **Almacenamiento Maildir** en lugar del sdbox de la imagen, que es lo que utiliza mailcow.
- **`separator = /`**, coincidiendo con mailcow. Las rutas de Carpeta se dividen en `/` y `\` cuando una Restauración
  recrea una jerarquía de Carpetas, por lo que un servidor que utilice `.` se comportaría de forma diferente.
- **Las carpetas de uso especial `Drafts`, `Junk`, `Sent` y `Trash` están precreadas**, ya que mailcow
  las precrea. Esto es importante al probar una migración desde Exchange: una Carpeta de origen llamada
  `Sent Items` aterriza junto a la `Sent` existente en lugar de fusionarse con ella, que es la
  situación que el renombrado de Carpetas tiene que gestionar.

La autenticación en texto plano está habilitada en el puerto 1143 porque la aplicación se conecta sin ninguna
seguridad de transporte cuando una Cuenta tiene el SSL desactivado.

## 🌱 Sembrar un Archivo para realizar pruebas {#seeding-an-archive-to-test-against}

Un Archivo vacío no es de mucha utilidad para probar una Restauración o una migración, por lo que
`tests/seed/seed.sh` construye uno:

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```

Crea dos Cuentas, importa los fixtures y muestra lo que se ha procesado. Ambas Cuentas se
crean con un intervalo de Sincronización por Cuenta de un año, y la fuente está adicionalmente desactivada,
por lo que el servicio de Sincronización en segundo plano la ignora: su servidor no existe, y el objetivo
Dovecot solo requiere que se le añadan elementos. Una advertencia: el estado de la programación reside en memoria, por lo que cada
reinicio de la aplicación programa el objetivo una vez, de inmediato. Por lo tanto, el objetivo también
excluye las Carpetas de uso especial de Dovecot (`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`), de modo que
una Sincronización tras un reinicio no tiene nada que archivar. Ejecutar el script dos veces es inofensivo;
la segunda ejecución informa que cada mensaje ya está presente.

Los fixtures están guardados en `tests/seed/fixtures`, un mbox por Carpeta de origen, con
`manifest.tsv` mapeando cada archivo a su Carpeta de destino. Se importan a través del
propio CLI `--import-mbox` de la aplicación, y ese detalle es deliberado: el CLI se enruta a través de
`MailImporter.ImportEmailToDatabase`, el mismo importador que utiliza la Sincronización IMAP, por lo que las filas aterrizan en
exactamente el formato que contiene un Archivo real: direcciones unidas con `", "`, asuntos pasados por
`CleanText`, `SentDate` convertidos a la zona horaria de visualización, `RawHeaders` extraído y truncado.
Insertar filas directamente en la base de datos omitiría todo eso y produciría fixtures que
difieren silenciosamente de los datos de producción.

Cada mensaje de fixture lleva un encabezado `X-Fixture-Purpose` que indica por qué existe, para que los archivos
se documenten a sí mismos:

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```

Las fechas son fijas en lugar de relativas al día actual. Un límite de migración se resuelve a una
fecha absoluta antes de que se ejecute un trabajo, por lo que un conjunto de fixtures fijo y un límite fijo prueban lo mismo de forma más predecible que uno deslizante. Los mensajes abarcan desde 2012 hasta 2026.

El diseño de las Carpetas refleja un Buzón de Exchange, y cada Carpeta existe para ejercitar algo:

| Carpeta de origen | Por qué está ahí |
|---|---|
| `INBOX` | fechas que abarcan quince años, más los mensajes de casos límite de abajo |
| `Sent Items` | colisiona con el `Sent` de uso especial existente del objetivo Dovecot |
| `Sent Items/2019` | un renombre tiene que reescribir solo el segmento de ruta inicial |
| `Sent Items Archive` | comparte un prefijo como subcadena pero no en un límite de segmento, por lo que no debe reescribirse |
| `Deleted Items` | exclusión de Carpeta |
| `Deleted Items/Old` | una exclusión en un padre tiene que cubrir a los descendientes |
| `Junk E-Mail` | el nombre de Exchange para una Carpeta que Dovecot llama `Junk` |
| `Archive/Projects/2020` | tres niveles de anidamiento |

Los mensajes individuales cubren un mensaje sin encabezado `Message-ID` (que obtiene el fallback determinista), uno cuyo `Message-ID` no tiene `@` (que no sobrevive intacto a una Restauración), un asunto que contiene un carácter de control, un mensaje con una cadena `Received` de tres saltos y uno sin ella, varios con dos destinatarios `To`, y dos pares de duplicados casi idénticos con un segundo de diferencia.

### Una nota sobre el código de salida {#a-note-on-the-exit-code}

`tests/seed/seed.sh` comprueba los recuentos de fallidos y malformados de la salida de la importación en lugar del
código de salida del proceso. `MBoxImportService` informa de `CompletedWithErrors` cuando omitió
duplicados, y el CLI mapea cada estado que no sea `Completed` al código de salida 1, por lo que volver a ejecutar una
importación termina con un código de salida distinto de cero incluso cuando nada salió mal. Por lo tanto, volver a ejecutar el script de siembra es seguro
e informa que cada mensaje ya está presente.

## ⚠️ No apto para producción {#not-for-production}

- Cualquier nombre de usuario se autentica contra Dovecot con una única contraseña fija.
- La autenticación en texto plano está habilitada.
- Las credenciales de la base de datos son los valores predeterminados documentados.
- El estado del correo y de la base de datos reside en volúmenes de Docker que `down -v` elimina.

## 🔧 Resolución de problemas {#troubleshooting}

**Los tests fallan con `Name or service not known`** — el stack no se está ejecutando, o
falta `appsettings.Test.json` por lo que la cadena de conexión recurre al valor predeterminado del contenedor
`Host=postgres`.

**`NETSDK1226` al compilar** — datos de carga de trabajo incompletos en el SDK local de .NET, no relacionados con este
repositorio. Ejecute `dotnet workload repair`, o compile con
`-p:AllowMissingPrunePackageData=true`.

**Dovecot se reinicia en un bucle** — un error de configuración. `docker compose -f
docker-compose.test.yml logs dovecot` informa de la línea causante. Tenga en cuenta que Dovecot no
acepta varios ajustes en una sola línea dentro de un bloque.

**La aplicación devuelve HTTP 500 en cada solicitud** — generalmente el anillo de claves de protección de datos.
Compruebe que `DataProtection:KeyPath` apunta a un directorio escribible.
