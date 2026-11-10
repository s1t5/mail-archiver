# 🔍 Mail Search {#mail-search}

[← Volver al índice de la documentación](index.md)

## 📋 Descripción general {#overview}

La búsqueda es el corazón de un Archivo de correo. Mail Archiver busca en el **texto completo**
de su correo archivado —asunto, cuerpo, remitente y destinatarios— en todas las
cuentas a las que tiene acceso, y devuelve resultados en milisegundos incluso en
Archivos con millones de mensajes.

Esta guía cubre la interfaz de búsqueda, la sintaxis de consulta, los filtros, la ordenación,
la paginación y qué hacer cuando necesita actuar sobre un conjunto de resultados.

> **ℹ️ La búsqueda solo ve lo que usted puede ver.** Los resultados se filtran por sus
> permisos de Cuenta. Un usuario que no sea administrador solo encontrará correos de los
> Buzones asignados a él; la restricción se aplica en la propia consulta,
> no en la interfaz.

## 🚀 Acceder a la búsqueda {#accessing-search}

1. Inicie sesión y elija **Archive** en el menú principal
2. La barra de búsqueda se encuentra sobre la lista de resultados

Al abrir el Archivo sin un término de búsqueda, se enumeran sus correos electrónicos más recientes, los más nuevos
primero. Puede navegar desde allí o reducir los resultados con los filtros.

## 🎯 La interfaz de búsqueda {#the-search-interface}

| Campo | Qué hace |
|---|---|
| **Search Term** | Consulta de texto completo — vea [Query Syntax](#-query-syntax) a continuación |
| **From** | Solo correos enviados en o después de esta fecha |
| **To** | Solo correos enviados en o antes de esta fecha |
| **Direction** | `All`, `Incoming` o `Outgoing` |
| **Account** | Restringir a una Cuenta de correo |
| **Folders** | Restringir a una Carpeta a través del árbol de carpetas |
| **Search** | Ejecutar la consulta |
| **Reset** | Limpiar todos los filtros |

El término de búsqueda coincide con el **asunto, cuerpo, remitente y destinatarios**
(`From`, `To`, `Cc`, `Bcc`).

### El árbol de carpetas {#the-folder-tree}

Las Carpetas se muestran como un **árbol jerárquico** a la izquierda, que refleja la estructura de
carpetas del Buzón de origen, incluyendo carpetas anidadas. Haga clic en una Carpeta para
restringir la búsqueda a ella, y use el control de contraer/expandir para plegar el árbol
cuando necesite espacio en pantalla.

Seleccionar una Carpeta no cambia su término de búsqueda; ambos se combinan.

## 🧩 Query Syntax {#query-syntax}

La sintaxis es deliberadamente pequeña. Todo lo que se indica a continuación es lo que la aplicación
realmente entiende.

### Palabras sueltas — y por qué funcionan los prefijos {#plain-words-and-why-prefixes-work}

Escriba una o más palabras; todas ellas deben estar presentes (una búsqueda **AND**):

```
invoice january
```

Encuentra correos que contienen tanto `invoice` como `january` en los campos buscados.

> **💡 Las palabras parciales coinciden automáticamente.** Cada palabra se trata como un
> **prefijo**, por lo que una búsqueda de `isenb` también encuentra `isenboeck` y `isenböck`, y
> `rech` encuentra `Rechnung` así como `Rechnungen`. No necesita un carácter comodín
> —no hay ninguno que escribir—. Esto también significa que un término muy corto puede
> devolver mucho; consulte [Performance](#-performance-and-large-archives).

La búsqueda **no distingue entre mayúsculas y minúsculas**. `Invoice`, `invoice` y `INVOICE` son la
misma consulta.

### Frases exactas — comillas {#exact-phrases-quotes}

Encierre el texto entre comillas dobles para requerir que las palabras estén **juntas** en ese
orden:

```
"important meeting"
```

Sin comillas, `important meeting` también coincidiría con un correo donde las dos
palabras aparecen en frases no relacionadas. Con comillas, solo coincide la frase adyacente.

### Búsqueda específica por campo {#field-specific-search}

Prefije un término con un nombre de campo y dos puntos para buscar **solo** en ese campo:

| Sintaxis | Busca en |
|---|---|
| `subject:invoice` | Solo Subject |
| `from:john` | Solo From |
| `to:example.com` | Solo To |
| `body:important` | Solo Body |

Los nombres de los campos no distinguen entre mayúsculas y minúsculas (`Subject:` también funciona), y cada uno puede
combinarse con comillas para una frase:

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ Se admiten cuatro campos:** `subject`, `body`, `from`, `to`. Escribir
> `cc:someone` o `bcc:someone` **no** es una búsqueda de campo; el término se trata
> como texto ordinario y se busca en todos los campos. Para encontrar un destinatario que estuviera
> solo en Cc, busque la dirección o el nombre sin un prefijo.

### Combinarlo todo {#combining-everything}

Los filtros y los términos se acumulan. Esta consulta encuentra correos de cualquier remitente de seguros,
con "car insurance" como una frase exacta en el cuerpo, y "invoice" en el
asunto:

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

Combinaciones prácticas:

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### Caracteres que se ignoran {#characters-that-are-ignored}

El analizador de consultas elimina los caracteres `& | ! ( ) : *` de los términos individuales,
porque tienen significado en el motor de texto completo subyacente. Si su búsqueda
no devuelve nada inesperado, compruebe si un carácter especial es la razón.

## 📊 Sorting and Paging {#sorting-and-paging}

### Sorting {#sorting}

Haga clic en el encabezado de una columna para ordenar. Columnas ordenables:

| Columna | Ordena por |
|---|---|
| **Subject** | Línea de asunto, alfabéticamente |
| **From** | Remitente |
| **To** | Destinatarios |
| **Date** | Fecha de envío |

Hacer clic de nuevo en el mismo encabezado invierte la dirección. El valor predeterminado es **los más nuevos
primero**.

### Page size {#page-size}

Elija cuántos resultados mostrar por página:

**20** (predeterminado) · **50** · **75** · **100** · **150**

Una página más grande muestra más contexto pero tarda más en renderizarse. Use los
controles **Previous** / **Next** para moverse por las páginas de resultados; el
conteo de resultados le indica cuántos correos coincidieron en total.

> **ℹ️ Su zona horaria se aplica a las fechas mostradas.** Las horas mostradas se
> convierten de UTC a su zona horaria configurada, por lo que la fecha que ve coincide con
> su calendario local; útil cuando está buscando cerca de la medianoche.

## 👁️ Working with Results {#working-with-results}

Cada fila muestra la Cuenta, el remitente, el asunto, la fecha y un **📎 icono de clip**
cuando el correo tiene Adjuntos. El clip es solo un indicador; no es
un filtro.

Haga clic en **Details** para abrir un correo electrónico:

- Cuerpo completo (HTML o texto plano)
- **Adjuntos**, enumerados con la opción de descargar cada uno
- **Download as EML** — el mensaje completo, para usar en otro lugar
- **Restauración** — copia el correo de vuelta a un Buzón activo

Consulte [Mailbox Migration](MailboxMigration.md) para la Restauración de correos a un Buzón.

## ✅ Acting on Many Results at Once {#acting-on-many-results-at-once}

Los resultados pueden seleccionarse y procesarse en lote. Active el **Selection Mode** para obtener
casillas de verificación, luego:

- **Select All** / **Deselect All** para la página actual
- **Select all {n} matching** — cada resultado de la búsqueda actual en todas las
  páginas (se muestra cuando los resultados abarcan más de una página)
- **Copy Selected** — Restauración a un Buzón
- **Export Selected** — como MBOX o EML
- **Delete Selected** — del Archivo

El contador muestra *"{selected} de {total} seleccionados"* mientras trabaja.

### El límite de selección {#the-selection-limit}

> **⚠️ Se puede seleccionar un máximo de 250 correos electrónicos a la vez** (configurable a través de
> `Selection:MaxSelectableEmails`). Más allá de eso, la interfaz muestra *"Maximum
> selections reached (250)"* y **Too Many — Split Required**.

Ese límite existe porque cada operación tiene que mantener los mensajes afectados. El
mensaje *"Split larger operations into multiple batches"* es la solución prevista: trabaje con los resultados en grupos de 250 o menos.

**Select all {n} matching** respeta el mismo límite. Cuando la búsqueda coincide con más
correos de los que permite el límite, selecciona los primeros en el orden de clasificación actual
y muestra el aviso *"Maximum selections reached"*. Reduzca la búsqueda (rango de fechas,
Carpeta, Cuenta) para dirigirse al resto.

Intentar excederlo muestra: *"Too many emails selected. Maximum allowed is
250 emails. Please reduce your selection or split into multiple operations."*

### Eliminar cada coincidencia de un filtro {#deleting-every-match-of-a-filter}

El botón **Delete all matches** junto a *Reset* elimina cada correo que
coincide con la búsqueda actual, no solo la página o selección actual. Está disponible para administradores y autogestores y se ejecuta como un trabajo en segundo plano, por lo que
el límite de selección de 250 correos no se aplica.

El cuadro de diálogo de confirmación muestra cuántos correos son eliminables y cuántos están bloqueados por la política de Retención. Debe escribir ese número para habilitar la
confirmación. El servidor cuenta de nuevo y aborta si el conjunto de resultados cambió en el
mientras tanto, por ejemplo porque una Sincronización añadió correo.

Los correos bloqueados nunca se eliminan con esta acción; se informan como omitidos en
el estado del trabajo. `DeletionPolicy:MaxBulkDeleteEmails` (predeterminado 100,000) limita un
trabajo. Un filtro que coincida con más correos es rechazado con una solicitud para reducirlo.

El trabajo aparece en la página [Background Jobs](BackgroundJobs.md) y puede ser
cancelado allí.

### Procesamiento directo vs. trabajo en segundo plano {#direct-processing-vs-background-job}

Para **Copy** y **Export** usted elige cómo se ejecuta el trabajo:

| Opción | Comportamiento |
|---|---|
| **Process on this page (wait for completion)** | El navegador espera; bueno para un puñado de correos |
| **Process in background (monitor separately)** | Se ejecuta como un trabajo; puede cerrar el navegador |

Las exportaciones siempre se ejecutan en segundo plano:

> *"The export will run in the background and you will be able to download the
> result from the Jobs page once completed."*

Encuentre los trabajos en ejecución y finalizados en la página [Background Jobs](BackgroundJobs.md).

> **⚠️ La eliminación es permanente.** La confirmación es explícita: *"Are you sure you
> want to permanently delete {n} selected email(s)? This action cannot be undone
> and each deletion will be logged for audit purposes."* Las eliminaciones se registran
> en el Registro de auditoría.

## ⚡ Performance and Large Archives {#performance-and-large-archives}

La búsqueda está respaldada por un índice de texto completo de PostgreSQL, por lo que la diferencia entre una
búsqueda rápida y una lenta suele ser qué tan **selectiva** es la consulta.

**Haga:**

- Añadir un **rango de fechas** cuando sepa aproximadamente cuándo llegó el correo; esto es
  la forma más efectiva de reducir la búsqueda
- Restringir a una Cuenta o Carpeta cuando sepa dónde reside el correo
- Usar **varias palabras** en lugar de una muy corta
- Usar búsquedas de campo (`subject:`, `from:`) para descartar coincidencias no relacionadas

**Evite:**

- Un único prefijo corto como `re`; como búsqueda de prefijo, coincide con una gran
  parte de su Archivo
- Rangos de fechas muy amplios combinados con una palabra común
- Navegar por muchas páginas cuando podría ajustar la búsqueda en su lugar

> **💡 Si una búsqueda parece lenta, el rango de fechas suele ser la respuesta.** Restringir
> a un mes a menudo convierte una búsqueda de varios segundos en una instantánea, porque el
> índice puede saltarse la mayor parte del Archivo.

## ❓ Troubleshooting {#troubleshooting}

| Síntoma | Causa | Qué hacer |
|---|---|---|
| No hay resultados, pero esperaba algunos | Término de búsqueda demasiado específico, o un error tipográfico | Intente con menos palabras; compruebe la ortografía |
| `cc:name` no encuentra nada útil | `cc` no es un campo admitido | Busque el nombre sin un prefijo |
| Un carácter especial rompe la consulta | `& \| ! ( ) : *` se eliminan | Elimine o reemplace el carácter |
| Demasiados resultados | Palabra común o prefijo corto | Añada un rango de fechas o búsqueda de campo |
| *"Too Many — Split Required"* | Más de 250 correos seleccionados | Reduzca la selección o divida la operación |
| Falta una exportación | Se ejecuta como un trabajo en segundo plano | Compruebe [Background Jobs](BackgroundJobs.md) |
| Los resultados parecen faltar | Permisos de Cuenta | Confirme que el Buzón está asignado a su usuario |
| Las fechas aparecen desplazadas | Conversión de zona horaria | Compruebe su configuración de zona horaria |

## 📚 Related Documentation {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — Restauración de resultados a un Buzón
- [Background Jobs](BackgroundJobs.md) — Monitoreo de exportaciones y Restauraciones
- [Access Logging](Logs.md) — El Registro de auditoría de búsquedas y eliminaciones
- [REST API](API.md) — El mismo Archivo a través de la API
- [MCP Server](MCP.md) — Búsqueda desde un agente de IA
- [Retention Policies](RetentionPolicies.md) — Cuándo los correos salen del Archivo
