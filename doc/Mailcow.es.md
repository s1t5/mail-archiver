# Archivo de instancias de Mailcow {#mailcow-instance-archiving}

[← Volver al índice de la documentación](index.md)

## Descripción general {#overview}

Mail Archiver puede archivar los buzones de una instancia de [Mailcow](https://mailcow.email) de forma centralizada, sin recopilar las contraseñas personales de los propietarios de los buzones.

Un administrador conecta la instancia una vez con su clave API. Mail Archiver luego enumera los buzones y, para cada buzón seleccionado para el archivo, crea una **contraseña de aplicación dedicada** restringida a `imap_access`. Esa contraseña de aplicación es utilizada por el proveedor IMAP existente para archivar el buzón y puede ser revocada nuevamente en cualquier momento.

Los administradores acceden al gestor de instancias desde **Add Account → Mailcow** o desde el menú de acciones de una cuenta que ya es gestionada por Mailcow. El gestor es exclusivo para administradores, porque la clave API almacenada puede gestionar las credenciales de cada buzón en la instancia.

## Creación de cuentas desde Mailcow {#creating-accounts-from-mailcow}

En **Mail Accounts → Add Account**, los administradores pueden seleccionar **Mailcow** como proveedor. El formulario ofrece entonces las instancias conectadas y una acción de **Manage mailboxes**. Seleccionar buzones allí crea cuentas de archivo IMAP ordinarias con contrasezas de aplicación dedicadas; no se almacena ninguna cuenta de Mailcow separada. Si aún no existe una instancia, **Manage Mailcow instances** abre el gestor de instancias donde se puede conectar una.

## Vincular una cuenta existente {#linking-an-existing-account}

En la vista de edición de una cuenta IMAP, los administradores pueden cambiar el proveedor a **Mailcow** y elegir una instancia. La cuenta mantiene el proveedor IMAP pero es adoptada por la gestión de Mailcow: Mail Archiver crea una contraseña de aplicación dedicada, la verifica mediante IMAP y reemplaza la contraseña almacenada de la cuenta. La cuenta aparece entonces con una acción de Mailcow en la lista de cuentas.

Para dejar la gestión de Mailcow, seleccione otro proveedor y guarde. La contraseña de aplicación se revoca y la contraseña local se borra. Al volver a cambiar a IMAP, introduzca una nueva contraseña IMAP; cambiar a Microsoft 365 o a una cuenta de solo importación proporciona sus propias credenciales. Los no administradores aún pueden editar los ajustes que no son de credenciales de una cuenta gestionada por Mailcow, pero no pueden vincularla ni desvincularla.

## Requisitos previos {#prerequisites}

### El cifrado de credenciales debe estar habilitado {#credential-encryption-must-be-enabled}

Mail Archiver almacena la clave API de Mailcow y cada contraseña de aplicación generada cifradas en reposo utilizando la función de cifrado de credenciales existente. Establezca

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```

antes de conectar una instancia. La creación de una instancia y la activación de buzones se rechazan si el cifrado está deshabilitado, por lo que las credenciales nunca pueden escribirse en texto plano a través de esta función. Mantenga la clave fuera de la base de datos e inclúyala en su Copia de seguridad segura — sin ella, las contraseñas de aplicación almacenadas no se pueden descifrar y los buzones no se pueden aprovisionar. Consulte [Setup](Setup.md#-security-settings) para más detalles.

### Acceso a la API de Mailcow {#mailcow-api-access}

Cree una clave API en Mailcow bajo **Configuration → Access → API**. La clave debe tener permiso para

- listar buzones (`get/mailbox/all`),
- listar, crear y eliminar contraseñas de aplicación (`get/app-passwd/all`, `add/app-passwd`,
  `delete/app-passwd`).

Restrinja la API a la dirección IP del host de Mail Archiver en los ajustes de la API de Mailcow. El endpoint de contraseñas de aplicación de Mailcow requiere que la función **App Passwords** esté habilitada para la instancia.

### Acceso a la red {#network-access}

- **HTTPS** al origen de Mailcow (por ejemplo `https://mail.example.com`).
- **IMAP over TLS** al host de IMAP, normalmente el puerto `993`. El IMAP en texto plano no es compatible para el aprovisionamiento.

## Uso paso a paso {#step-by-step-usage}

### Conectar una instancia {#connecting-an-instance}

1. Inicie sesión en Mail Archiver como administrador.
2. Abra **Mail Accounts** y haga clic en **Mailcow instances**.
3. Haga clic en **Connect Mailcow instance**.
4. Introduzca los detalles de conexión:
   - **Account name** — un nombre de visualización para la instancia en Mail Archiver.
   - **Mailcow URL** — solo el origen HTTPS, por ejemplo `https://mail.example.com`.
     Las rutas, cadenas de consulta y credenciales incrustadas son rechazadas.
   - **API key** — la clave creada en Mailcow.
   - **IMAP host** — el nombre de host utilizado para el archivo, por ejemplo `mail.example.com`.
     Introduzca solo el nombre de host, sin esquema ni puerto.
   - **IMAP TLS port** — por defecto es `993`.
5. Haga clic en **Connect**. Mail Archiver verifica la clave API listando los buzones antes de guardar la instancia. Si tiene éxito, abre la selección de buzones.

La clave API se almacena cifrada y nunca se vuelve a mostrar en el formulario ni en un mensaje de error. Las redirecciones HTTP están deshabilitadas para las llamadas a la API para que la clave no pueda ser reenviada a otro origen.

### Activar buzones {#activating-mailboxes}

1. En la página de buzones, opcionalmente filtre la lista con el campo de búsqueda. El filtro coincide con la dirección y el nombre de visualización y no distingue entre mayúsculas y minúsculas.
2. Seleccione los buzones para archivar. **Select all** y **Clear selection** solo afectan a los buzones actualmente visibles (filtrados), por lo que filtrar es una forma segura de elegir un subconjunto.
3. Haga clic en **Activate archiving**.

Para cada buzón seleccionado, Mail Archiver:

1. vuelve a validar la dirección contra la lista de buzones actual de Mailcow e ignora los buzones deshabilitados,
2. crea una cuenta de archivo llamada `{instance name} – {mailbox display name}`,
3. genera una contraseña de aplicación aleatoria llamada `MailArchiver-<unique id>` restringida a
   `imap_access`,
4. verifica la credencial con un inicio de sesión IMAP sobre TLS,
5. y solo entonces habilita la Sincronización.

Si la verificación IMAP falla, la cuenta permanece deshabilitada y se puede reintentar.

### Indicadores de estado del buzón {#mailbox-status-badges}

La página de selección etiqueta cada buzón para que quede claro por qué un checkbox está disponible o no:

| Indicador | Significado | Seleccionable |
| --- | --- | --- |
| **Available** | Listado en Mailcow, aún no aprovisionado | sí |
| **Active** | Aprovisionado y sincronizando | no |
| **Pending** | Aprovisionamiento iniciado pero no terminado (reintentable) | sí |
| **Revoked** | La contraseña de aplicación fue revocada; puede activarse de nuevo | sí |
| **Existing** | Ya existe una cuenta IMAP independiente con esta dirección | no |
| **Disabled** | El buzón está deshabilitado en Mailcow | no |

### Reintentar un buzón pendiente {#retrying-a-pending-mailbox}

La credencial generada y el nombre de la contraseña de aplicación única se persisten **antes** de que se llame a la API de Mailcow. Si una solicitud falla de forma ambigua — por ejemplo, un tiempo de espera tras la creación de la contraseña de aplicación — el buzón permanece visible como **Pending**. Recargue la página y selecciónelo de nuevo: Mail Archiver busca primero el nombre de la aplicación y solo crea una nueva contraseña de aplicación si no existe ninguna. Reintentar no acumula credenciales duplicadas.

El aprovisionamiento y la revocación para una instancia se serializan utilizando bloqueos de asesoría de PostgreSQL, por lo que dos operaciones concurrentes no pueden aprovisionar el mismo buzón dos veces.

### Revocar acceso {#revoking-access}

Haga clic en **Revoke app password** junto a un buzón. Esto

- deshabilita la Sincronización futura,
- elimina la contraseña de aplicación gestionada en Mailcow,
- y borra la contraseña local.

**El correo archivado se mantiene.** Solo se elimina la contraseña de aplicación gestionada; otras contraseñas de aplicación que pertenecen al buzón nunca se tocan. Las operaciones de Sincronización que ya se están ejecutando pueden terminar con su conexión existente.

Si la solicitud remota falla, la cuenta permanece deshabilitada y la revocación puede reintentarse.

Eliminar una cuenta de archivo en **Mail Accounts** no elimina silenciosamente la credencial remota. El vínculo permanece listado bajo **Remaining app passwords** en la página de la instancia para que pueda ser revocado explícitamente.

### Editar una instancia {#editing-an-instance}

Abra **Mailcow instances** y haga clic en **Edit**. El campo de la clave API siempre está vacío — la clave almacenada nunca se muestra. Déjelo vacío para mantener la clave actual, o introduzca una nueva para reemplazarla. Los detalles de conexión cambiados se verifican contra Mailcow antes de guardar.

### Eliminar una instancia {#deleting-an-instance}

Abra **Mailcow instances** y haga clic en **Delete**.

Eliminar una instancia quita su clave API cifrada y sus vínculos de buzones. Las **cuentas de archivo y su correo archivado se mantienen**, pero las cuentas se deshabilitan.

La eliminación es **rechazada mientras haya alguna contraseña de aplicación gestionada activa** en Mailcow: eliminar la instancia borraría la clave API necesaria para revocarla. Revoque primero todas las contras de aplicación gestionadas en la página del buzón.

## Registro de auditoría {#audit-logging}

Cada operación administrativa se escribe en el registro de acceso con el usuario que actúa:

- conectar una instancia,
- actualizar una instancia,
- eliminar una instancia,
- activar buzones (con el número aprovisionado y el número fallido),
- revocar una contraseña de aplicación.

## Límite de buzones {#mailbox-limit}

Una sola ejecución de activación puede aprovisionar como máximo `Mailcow__MaxSelectedMailboxes` buzones
(por defecto `1000`). Esto protege contra el aprovisionamiento masivo accidental y la carga excesiva de la API de Mailcow e IMAP. Para instancias más grandes, ejecute la operación varias veces.

Cada buzón requiere varios viajes de ida y vuelta de la API más una verificación IMAP, y se procesan secuencialmente. Aprovisionar cientos de buzones en una sola solicitud puede exceder los tiempos de espera del proxy inverso o del navegador; divida la selección en lotes.

## Notas de seguridad {#security-notes}

- La clave API permanece en el lado del servidor. Se envía como un encabezado `X-API-Key`, se redacta de los registros y nunca se devuelve al navegador.
- Las respuestas de la contraseña de aplicación de Mailcow pueden devolver contraseñas en texto plano en sus campos `log`. Mail Archiver deliberadamente no registra los cuerpos de respuesta y no muestra errores de la API al navegador; los fallos se informan como mensajes localizados.
- Una respuesta HTTP 200 no se trata como un éxito: las mutaciones requieren el `type: success` de Mailcow por resultado.
- El permiso IMAP no es intrínsecamente de solo lectura. Las cuentas aprovisionadas no tienen una política de eliminación en el servidor configurada, y los administradores mantienen el control de la configuración de la cuenta. Esta función archiva mensajes, no la configuración de Mailcow ni las Copias de seguridad.

### Hostnames y redes privadas {#hostnames-and-private-networks}

La URL de Mailcow y el host de IMAP son introducidos por un administrador y pueden apuntar a un espacio de direcciones privado. Esto es intencional y compatible: Mail Archiver se despliega comúnmentmente junto al servidor de correo, por lo que `mail.example.lan`, `192.168.10.20` o `10.0.0.5`
son valores normales. **No se bloquea ninguna dirección privada, de loopback o local.**

El límite de confianza es el rol de administrador, no la dirección de red. `Mail Accounts`
está disponible para los autogestores, quienes ya pueden configurar un servidor IMAP arbitrario para sus propias cuentas; la sección de Mailcow está restringida a administradores, y un administrador ya tiene acceso sin restricciones a cada cuenta de archivo en la instalación. Por lo tanto, restringir el host de IMAP no cerraría un límite de privilegios, mientras que rompería el despliegue on-premise previsto.

Siguen aplicándose dos restricciones:

- La URL de Mailcow debe ser un origen HTTPS puro. Las rutas, cadenas de consulta, información de usuario y redirecciones son rechazadas, por lo que la clave API no puede ser reenviada a otro origen.
- El host de IMAP debe ser un nombre de host sin esquema ni puerto, y el aprovisionamiento siempre se conecta con TLS (`SslOnConnect`).

Si necesita restringir a qué hosts puede llegar Mail Archiver, aplíquelo en la capa de red — reglas de firewall de salida o una política de DNS para el contenedor — en lugar de confiar en la validación de entrada en la aplicación.

## Resolución de problemas {#troubleshooting}

| Síntoma | Causa y remedio |
| --- | --- |
| *Encryption required* al conectar o activar | `Security__CredentialEncryptionKey` no está configurado o es inválido. Configure una clave de 32 bytes codificada en base64 y reinicie. |
| *HTTPS origin required* | La URL tiene una ruta, cadena de consulta o credenciales incrustadas. Introduzca el origen puro, ej. `https://mail.example.com`. |
| *IMAP host invalid* | El host de IMAP debe ser un nombre de host sin esquema ni puerto. |
| *Instance already connected* | Existe otra instancia con la misma URL. Edite esa instancia en su lugar. |
| *API request failed (401/403)* | La clave API carece de los permisos requeridos, o la restricción de IP no incluye el host de Mail Archiver. |
| *API request rejected* | Mailcow devolvió una fila `error`/`danger`/`warning`. Compruebe los registros de Mailcow; Mail Archiver no reenvía los cuerpos de respuesta. |
| *IMAP verification failed* | Host o puerto IMAP incorrectos, un certificado no confiable, o la función de contraseña de aplicación está deshabilitada en Mailcow. |
| *IMAP account exists* | Una cuenta IMAP independiente ya archiva esta dirección. Use esa cuenta en lugar de crear un segundo archivo. |
| *Revoke previous password* | La cuenta de archivo para este buzón fue eliminada. Revoque primero la contraseña de aplicación restante y luego active de nuevo. |
| El buzón permanece **Pending** | La API de Mailcow agotó el tiempo de espera. Recargue la página y active el buzón de nuevo; no se crea una contraseña de aplicación duplicada. |
| La eliminación es rechazada | Las contraseñas de aplicación gestionadas aún están activas. Revóquelas primero en la página del buzón. |

## Endpoints de la API utilizados {#api-endpoints-used}

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## Cambios en la base de datos {#database-changes}

La migración añade solo las tablas `MailcowInstances` y `MailcowMailboxes` junto con sus índices y claves foráneas. Las tablas de buzones existentes y los datos archivados se mantienen. Eliminar una cuenta de archivo establece el `MailAccountId` del vínculo a `NULL` para que la credencial remota aún pueda ser revocada después.
