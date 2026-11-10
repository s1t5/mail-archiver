# Importación de Buzones de Tenant de Microsoft 365 {#microsoft-365-tenant-mailbox-import}

[← Volver al índice de documentación](index.md)

## Descripción general {#overview}

Mail Archiver puede crear cuentas de correo de Microsoft 365 para más de un Buzón en el mismo tenant desde un único formulario de creación. Esto es útil al incorporar a un cliente u organización donde se deben archivar todos, o un subconjunto seleccionado, de los Buzones de Microsoft 365.

La importación de tenant utiliza las credenciales de la aplicación Microsoft Graph configuradas en el formulario y crea una cuenta de correo de Mail Archiver por cada Buzón importado.

## Requisitos previos {#prerequisites}

Antes de utilizar la importación de tenant, complete la configuración del registro de la aplicación Microsoft 365 descrita en la [Guía de registro de aplicaciones de Azure y política de Retención](AZURE_APP_REGISTRATION_M365.md).

Necesita estos valores de Microsoft Entra ID:

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

El registro de la aplicación debe tener **permisos de aplicación** de Microsoft Graph que permitan a Mail Archiver leer los Buzones del tenant. Además de los permisos requeridos para el archivado de Cuenta de M365 regular, se requiere el siguiente permiso para la importación de tenant:

- **User.Read.All** – requerido para listar los usuarios del tenant y sus direcciones de Buzón.

La importación de tenant se selecciona como el proveedor **Microsoft 365 (tenant)** en el formulario de creación.

### Filtrado {#filtering}

La importación de tenant y la Gestión de Tenant excluyen automáticamente:
- **Guest accounts** (`userType == "Guest"`) — usuarios externos invitados sin sus propios Buzones en el tenant.
- **Users without an Exchange Online license** — solo se enumeran los usuarios con un plan de servicio Exchange activo en su `assignedPlans`. Esto evita la importación de cuentas que no se pueden archivar.

## Modos de importación {#import-modes}

La importación de tenant admite dos modos:

### Importar todos los Buzones enumerados {#import-all-listed-mailboxes}

Utilice este modo cuando cada Buzón devuelto por Microsoft Graph deba crearse como una cuenta de Mail Archiver.

- Mail Archiver carga la lista de Buzones del tenant desde Microsoft Graph.
- Las cuentas de correo de M365 ya existentes se omiten.
- Si **Skip disabled mailboxes** está habilitado, los usuarios deshabilitados no se incluyen en la lista de Buzones.

### Importar solo Buzones seleccionados {#import-selected-mailboxes-only}

Utilice este modo cuando solo deban archivarse Buzones específicos en el tenant.

- Cargue primero la lista de Buzones del tenant.
- Desactive **Import all listed mailboxes**.
- Seleccione los Buzones que deben importarse.
- Envíe el formulario para crear cuentas solo para los Buzones seleccionados.

## Uso paso a paso {#step-by-step-usage}

1. Inicie sesión en Mail Archiver como un usuario que pueda crear cuentas de correo.
2. Abra **Mail Accounts** > **Create**.
3. Seleccione **Microsoft 365 (tenant)** como proveedor.
4. Ingrese las credenciales de Microsoft 365:
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. Ingrese el prefijo **Account name** que debe utilizarse para las cuentas generadas.
6. Elija si se deben omitir los Buzones deshabilitados.
7. Haga clic en **Load mailboxes** para listar los Buzones del tenant.
8. Elija el comportamiento de importación deseado:
   - Mantenga habilitado **Import all listed mailboxes** para importar todos los Buzones enumerados.
   - Desactívelo y seleccione Buzones individuales para importar solo Buzones específicos.
9. Haga clic en **Save**.

Después de la creación, Mail Archiver redirige a la lista de cuentas de correo y muestra cuántas cuentas de correo del tenant se importaron y cuántas cuentas existentes se omitieron.

## Nomenclatura de cuentas {#account-naming}

Para las importaciones de tenant, Mail Archiver utiliza el valor ingresado en **Account name** como prefijo para cada cuenta de Buzón creada.

Ejemplo:

- Nombre de cuenta ingresado: `Test GmbH`
- Buzón del tenant: `mailbox@example.com`
- Nombre de cuenta creado: `Test GmbH - <mailbox@example.com>`

Esto facilita la identificación de las cuentas importadas que pertenecen al mismo tenant o cliente.

## Cuentas existentes {#existing-accounts}

Si un Buzón ya existe como una cuenta de Microsoft 365 en Mail Archiver, la importación de tenant lo omite en lugar de crear un duplicado. La detección de cuentas existentes se basa en la dirección de correo electrónico del Buzón y el proveedor de M365.

La lista de Buzones también puede mostrar si un Buzón ya existe antes de enviar la importación.

## Buzones deshabilitados {#disabled-mailboxes}

La casilla de verificación **Skip disabled mailboxes** controla si los usuarios deshabilitados del tenant deben excluirse de la lista de Buzones.

- Habilitado: solo se enumeran e importan los usuarios habilitados del tenant.
- Deshabilitado: los usuarios deshabilitados del tenant pueden aparecer en la lista y pueden ser importados si se seleccionan o si se importan todos los Buzones enumerados.

## Gestión de Tenant {#tenant-management}

Además de la importación de tenant del formulario de creación, Mail Archiver proporciona una página dedicada de **Tenant Management** para administradores. Es accesible desde el índice de Mail Accounts a través del botón **Tenant Management** (icono de edificio) junto a cualquier cuenta de Microsoft 365.

Tenant Management es útil para agregar Buzones que no se importaron inicialmente —por ejemplo, cuando se crean nuevos Buzones en el tenant después de la incorporación inicial, o cuando las cuentas fueron omitidas originalmente.

### Diferencias con la importación de tenant del formulario de creación {#how-it-differs-from-the-create-form-tenant-import}

- Las credenciales de Microsoft 365 (Client ID, Client Secret, Tenant ID) **no se ingresan de nuevo**. Se toman de la cuenta de origen en la que se hizo clic en el botón.
- La lista de Buzones del tenant se carga automáticamente cuando se abre la página (no hay botón "Load mailboxes").
- Los Buzones ya importados se muestran con una etiqueta de "already exists" y no se pueden seleccionar de nuevo.
- Solo los administradores pueden acceder a Tenant Management.

### Uso paso a paso {#step-by-step-usage}

1. Inicie sesión en Mail Archiver como administrador.
2. Abra **Mail Accounts**.
3. Localice la cuenta de Microsoft 365 cuyo tenant desea gestionar y haga clic en el botón **Tenant Management** junto a ella.
4. La página se carga y enumera todos los Buzones devueltos por Microsoft Graph para el tenant de la cuenta de origen.
   - Los Buzones que ya existen como cuentas de M365 en Mail Archiver se marcan con una etiqueta de "already exists" y no se pueden seleccionar.
   - Los usuarios deshabilitados del tenant no se enumeran; Tenant Management siempre omite los Buzones deshabilitados.
5. Seleccione los Buzones que desea agregar.
6. Ingrese el prefijo **Account name** que debe utilizarse para las nuevas cuentas (el esquema de nomenclatura es `{prefix} - <{email}>`, idéntico a la importación de tenant del formulario de creación).
7. Configure opcionalmente **Delete After Days** y **Local Retention Days** para las nuevas cuentas.
8. Haga clic en **Add selected mailboxes**.

Después de la creación, Mail Archiver redirige a la lista de cuentas de correo y muestra cuántos Buzones se agregaron.

### Búsqueda de Buzones {#searching-mailboxes}

Para tenants grandes con varios miles de Buzones, un campo de búsqueda sobre la lista de Buzones le permite filtrar por nombre de visualización o dirección de correo electrónico. El filtro no distingue entre mayúsculas y minúsculas y se aplica instantáneamente mientras escribe. **Select all** y **Clear selection** operan solo en los Buzones actualmente visibles (filtrados), por lo que el filtrado es una forma segura de seleccionar un subconjunto específico sin afectar las entradas ocultas.

### Renombrar cuentas existentes {#renaming-existing-accounts}

La casilla de verificación **Rename existing accounts** aplica el esquema de nomenclatura `{prefix} - <{email}>` a todas las cuentas de M365 que comparten el mismo registro de aplicación (Client ID y Tenant ID) que la cuenta de origen. Esto es útil para normalizar los nombres de las cuentas después de una importación inicial donde los nombres eran inconsistentes.

Cuando está marcada, al guardar el formulario se renombra cada cuenta coincidente —incluyendo la cuenta de origen— independientemente de si se seleccionan nuevos Buzones para la importación. Puede usar esta opción sola (sin seleccionar ningún Buzón nuevo) para normalizar los nombres sin agregar cuentas.

### Límite de Buzones {#mailbox-limit}

Una única operación de Tenant Management puede agregar como máximo `TenantManagement__MaxSelectedMailboxes` Buzones a la vez (predeterminado `1000`). Esto protege contra importaciones masivas accidentales y una carga excesiva de Microsoft Graph / base de datos. Si necesita importar más Buzones, ejecute la operación varias veces. Consulte la [Guía de Configuración](Setup.md#-tenant-management-settings) para saber cómo ajustar este límite.

### Permisos requeridos {#permissions-required}

Tenant Management utiliza los mismos permisos de aplicación de Microsoft Graph que la importación de tenant del formulario de creación. El registro de la aplicación de la cuenta de origen debe tener:

- Los permisos requeridos para el archivado de Cuenta de M365 regular, **y**
- **User.Read.All** — requerido para listar los usuarios del tenant y sus direcciones de Buzón.

Consulte la [Guía de registro de aplicaciones de Azure y política de Retención](AZURE_APP_REGISTRATION_M365.md) para la configuración inicial.

### Registro de auditoría {#audit-logging}

Cada vista de la página de Tenant Management y cada operación de adición se registra en el registro de acceso, incluyendo el número de Buzones enumerados o agregados y la cuenta de origen utilizada.
