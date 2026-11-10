# 👥 Gestión de usuarios y permisos de Buzón {#user-management-and-mailbox-permissions}

[← Volver al índice de la documentación](index.md)

## 📋 Descripción general {#overview}

Esta guía proporciona instrucciones detalladas para crear nuevas Cuentas de usuario y asignar permisos de Buzón en la aplicación Mail Archiver. También cubre cómo cambiar su propia contraseña y el cambio de contraseña forzado en una instancia recién instalada.


## 🛠️ Requisitos previos {#prerequisites}

- Acceso administrativo a la aplicación Mail Archiver
- Cuentas de correo electrónico existentes configuradas en el sistema

## 🧑‍💻 Crear una nueva Cuenta de usuario {#creating-a-new-user-account}

1. Inicie sesión en la aplicación Mail Archiver con una Cuenta que tenga privilegios administrativos
2. Navegue a la sección "Users" desde el menú principal
3. Haga clic en el botón "Create New User"
4. Complete la información requerida del usuario:
   - **Username**: Ingrese un nombre de usuario único para el nuevo usuario
   - **Password**: Ingrese una contraseña segura para el usuario
   - **Email**: Dirección de correo electrónico del usuario
   - **Admin**: Marque esta casilla si el usuario debe tener privilegios administrativos
   - **Self Manager**: Marque esta casilla si el usuario debe poder gestionar sus propias cuentas de correo (editar las existentes y añadir nuevas, así como eliminar cuentas)
   - **active**: Debe estar marcada para permitir los inicios de sesión del usuario
5. Haga clic en "Create User" para guardar la nueva Cuenta de usuario

## 🔐 Asignar permisos de Buzón a los usuarios {#assigning-mailbox-permissions-to-users}

1. Después de crear el usuario, o al editar un usuario existente, navegue a la sección "Users" desde el menú principal
2. Busque al usuario en la lista donde desea asignar cuentas de correo y haga clic en el botón "Assign" para ese usuario
3. En la página "Assign Mail Accounts", verá todas las cuentas de correo disponibles y las casillas de verificación que indican si están asignadas al usuario
5. Para asignar una nueva cuenta de correo, marque la casilla correspondiente a la cuenta específica
6. Para eliminar una cuenta de correo del usuario, desmarque la casilla
7. Haga clic en "Save Assignments" para aplicar los cambios

## 👤 Permisos de la Cuenta de usuario {#user-account-permissions}

### Usuarios Admin {#admin-users}
Los usuarios Admin tienen acceso completo a la aplicación y pueden:
- Gestionar todas las Cuentas de usuario y sus permisos
- Acceder a todas las cuentas de correo en el sistema
- Ver todos los registros de acceso
- Realizar todas las tareas administrativas

### Usuarios Self Manager {#self-manager-users}
Los usuarios Self Manager pueden:
- Añadir nuevas cuentas de correo a su propia Cuenta
- Editar las cuentas de correo existentes que tengan asignadas
- Eliminar las cuentas de correo que tengan asignadas
- Acceder y gestionar los correos de sus cuentas asignadas (incluyendo la eliminación)
- Ver sus propios registros de acceso

### Usuarios Standard {#standard-users}
Los usuarios Standard tienen acceso limitado y pueden:
- Ver correos electrónicos archivados de las cuentas de correo asignadas
- Buscar dentro de sus cuentas de correo asignadas
- Exportar correos electrónicos de sus cuentas asignadas
- Realizar la Restauración de correos electrónicos de sus cuentas asignadas
- Acceder a los Adjuntos de correo de las cuentas asignadas

## 🔑 Cambiar su propia contraseña {#changing-your-own-password}

Cada usuario con una contraseña local puede cambiarla sin un administrador:

1. Haga clic en su **username** en la esquina superior derecha para abrir el menú de usuario.
2. Elija **Change Password**.
3. Ingrese su **current password** y la **new password** dos veces, luego envíe.

La nueva contraseña debe cumplir con los requisitos a continuación y debe ser **diferente de la actual**.

> ℹ️ **Los usuarios OIDC no pueden cambiar su contraseña aquí.** La página responde con:
> *"OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* Cambie la contraseña en su proveedor de identidad en su lugar —
> consulte [OIDC / SSO](OIDC_Implementation.md).

### Requisitos de la contraseña {#password-requirements}

- al menos **10 caracteres**
- al menos una letra **mayúscula**
- al menos una letra **minúscula**
- al menos un **número**
- al menos un **carácter especial**

El formulario muestra esto como una lista de verificación en vivo mientras escribe. Las mismas reglas se aplican cuando un administrador crea un usuario o restablece una contraseña.

## ⚠️ Cambio de contraseña forzado (Configuración inicial) {#forced-password-change-initial-setup}

Una instancia recién instalada comienza con las credenciales de administrador de la
configuración (`Authentication__Username` / `Authentication__Password`). Mientras
esa contraseña predeterminada se siga utilizando **y aún no exista ninguna cuenta de correo**, el primer
inicio de sesión fuerza un cambio:

- Un banner de advertencia explica el motivo: *"For security reasons, you must change your
  password before continuing. This is required because you are using the default
  system credentials on a newly set up system."*
- No puede navegar fuera de la página: *"You must change your password before
  you can continue using the system."*
- La nueva contraseña debe ser diferente de la actual.
- Después de guardar, la bandera se borra y se le lleva al panel de control.

Una vez que haya cambiado la contraseña predeterminada, el cambio forzado no volverá a aparecer.

## 🕒 Mantener la sesión iniciada — la casilla "Remember Me" {#staying-signed-in-the-remember-me-checkbox}

El formulario de inicio de sesión tiene una casilla de verificación llamada **Remember Me** (*angemeldet bleiben* en
la interfaz en alemán). Hace exactamente una cosa: decide si la
cookie de autenticación es una cookie de sesión o una persistente.

| | Casilla desmarcada | Casilla marcada |
|---|---|---|
| Vida útil de la cookie | `Authentication__SessionTimeoutMinutes` (predeterminado **60 minutos**) | **7 días** |
| Tipo de cookie | Cookie de sesión — se descarta al cerrar el navegador | Cookie persistente — se almacena en el disco |
| Reinicio del navegador | Debe iniciar sesión de nuevo | Se mantiene con la sesión iniciada |
| Tiempo de espera por inactividad | Se extiende con cada solicitud (`SlidingExpiration`) | Se extiende con cada solicitud, hasta el límite absoluto de 7 días |
| Cerrar sesión | Finaliza la sesión inmediatamente | Finaliza la sesión inmediatamente |

Por lo tanto, la casilla convierte "sesión iniciada para esta sesión de navegador, máximo 60 minutos
de inactividad" en "sesión iniciada por hasta 7 días, incluyendo reinicios de
navegador y de la máquina".

### Cosas que la casilla no hace {#things-the-checkbox-does-not-do}

- **No recuerda su contraseña ni su nombre de usuario.** El formulario de inicio de sesión no
  se completa automáticamente en la siguiente visita; solo se mantiene la cookie de sesión.
- **Los 7 días son un límite estricto y no se pueden configurar.** El valor es fijo
  en el código (`Auth/Services/CookieAuthenticationService.cs`). Solo el
  tiempo de vida no persistente sigue a `Authentication__SessionTimeoutMinutes`.
- **No es un temporizador de inactividad.** Los 7 días se cuentan desde el inicio de sesión; una vez
  que se agotan, se requiere un nuevo inicio de sesión incluso si usó el Archivo hace un minuto.
- **No sobrevive a un cierre de sesión.** Hacer clic en *Logout* elimina la cookie
  independientemente de la casilla de verificación.

### Interacción con 2FA {#interaction-with-2fa}

Con la autenticación de dos factores habilitada, la casilla se recuerda durante el
paso de 2FA y se aplica solo después de que el segundo factor tiene éxito. El traspaso de 2FA
reside en la sesión del lado del servidor, que tiene un **tiempo de espera por inactividad de 60 minutos**
propio: si el código no se ingresa dentro de ese intervalo, el inicio de sesión se aborta y
la selección de la casilla se pierde con él.

### Interacción con la sesión de la aplicación {#interaction-with-the-application-session}

Mail Archiver mantiene una segunda sesión separada en el lado del servidor para estados de corta duración — el traspaso de 2FA, la selección de la Restauración por lotes y el último estado de búsqueda. Esa sesión tiene un tiempo de espera por inactividad fijo de **60 minutos** y *no* es persistente. Después de un reinicio del navegador con una cookie "Remember Me" válida, usted tiene la sesión iniciada, pero cualquier operación pendiente mantenida en esa sesión comienza de nuevo.

### Interacción con OIDC / SSO {#interaction-with-oidc-sso}

Los usuarios que inician sesión a través de un proveedor de identidad externo nunca obtienen la
cookie persistente: el callback de OIDC siempre inicia la sesión con
`persistAuthentication: false`. Para esas Cuentas, la casilla **no tiene
efecto** — cuánto dura una sesión la deciden los ajustes de sesión del proveedor de identidad.

> ⚠️ **Nota de seguridad.** En una máquina compartida o desatendida, deje la casilla
> desmarcada — una cookie persistente mantiene el Archivo accesible durante hasta 7 días
> sin una contraseña. En entornos donde eso es inaceptable, acorte
> `Authentication__SessionTimeoutMinutes` y requiera que los usuarios no usen la
> casilla, o use OIDC donde el proveedor impone su propia política de sesión.

## 🔒 Consideraciones de seguridad {#security-considerations}

1. Use contraseñas fuertes para todas las Cuentas de usuario
2. Cambie la contraseña predeterminada del administrador inmediatamente después de la instalación
3. Solo asigne el Permiso Admin a los usuarios que lo necesiten
4. Solo asigne el Permiso Self Manager a los usuarios que necesiten gestionar sus propias Cuentas
5. Revise regularmente los permisos de usuario para asegurar que el acceso sea apropiado
6. Elimine el acceso de usuario cuando ya no sea necesario
7. Use el principio de mínimo privilegio: asigne acceso solo a las cuentas de correo que los usuarios necesiten
