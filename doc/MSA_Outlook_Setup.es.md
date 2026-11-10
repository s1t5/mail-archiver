# ☁️ Configuración de Cuenta Microsoft Personal (Outlook.com / M365 Family) {#personal-microsoft-account-outlookcom-m365-family-setup}

[← Volver al Índice de Documentación](index.md)

## 📋 Resumen {#overview}

Esta guía explica cómo realizar el Archivo de correos electrónicos de una **cuenta Microsoft personal** (Outlook.com, Hotmail, live.com o Microsoft 365 Family) en Mail Archiver.

A diferencia de las cuentas organizacionales de Microsoft 365 (que utilizan el flujo client-credentials — ver [Azure App Registration for M365](AZURE_APP_REGISTRATION_M365.md)), las cuentas personales utilizan **OAuth2 con el Device Code Flow (RFC 8628)**. Microsoft ha dejado de utilizar Basic Auth y las contraseñas de aplicación para cuentas personales, por lo que se requiere OAuth2.

Mail Archiver se distribuye con un **Client ID compartido pre-registrado** para que la mayoría de los usuarios **no** necesiten crear su propio Azure App Registration. Los usuarios avanzados aún pueden registrar su propia aplicación (ver la Opción B a continuación).

## 📚 Tabla de Contenidos {#table-of-contents}

1. [Opción A: Configuración Estándar (Recomendado)](#option-a-standard-setup-recommended)
2. [Opción B: Registro de Aplicación Personalizado (Usuarios Avanzados)](#option-b-custom-app-registration-power-users)
3. [Actualización y Revocación de Tokens](#token-refresh--revocation)
4. [Notas Importantes](#important-notes)
5. [Solución de Problemas](#troubleshooting)

---

## 🚀 Opción A: Configuración Estándar (Recomendado) {#option-a-standard-setup-recommended}

Utilice esta opción si su instancia de Mail Archiver tiene un Client ID predeterminado configurado (el predeterminado para las imágenes oficiales de Docker). No se requiere acceso al Azure Portal.

### 🛠️ Requisitos Previos {#prerequisites}

- Una cuenta Microsoft personal (Outlook.com, live.com o M365 Family)
- Un dispositivo con navegador (smartphone, portátil o tableta) para completar la autorización

### 📝 Pasos {#steps}

1. Inicie sesión en su aplicación Mail Archiver.
2. Navegue a **Mail Accounts** > **Create**.
3. Complete los siguientes campos:
   - **Name**: Nombre descriptivo para la Cuenta (ej. "Personal Outlook")
   - **Email Address**: Su dirección de correo electrónico personal de Microsoft (ej. `you@outlook.com`)
   - **Provider**: Seleccione **Microsoft Personal**
4. No se muestran los campos Client ID o Client Secret — Mail Archiver utiliza automáticamente el registro predeterminado integrado.
5. Haga clic en **Create**.
6. La página de autorización por código de dispositivo se abre automáticamente. Se muestra un código corto (ej. `ABCD-EFGH`) junto con un enlace a `https://microsoft.com/devicelogin` (o la URL de verificación que se muestra).
7. En **cualquier dispositivo** con un navegador, abra esa URL e inicie sesión con su cuenta Microsoft personal.
8. Ingrese el código del paso 6.
9. Aparecerá una pantalla de consentimiento que muestra que **"Mail-Archiver"** solicita acceso IMAP. Revise y apruebe.
10. La página de Mail Archiver detecta automáticamente el éxito y redirige a la vista de edición de la Cuenta.
11. Listo — la Sincronización comienza automáticamente de acuerdo con la configuración de la Cuenta.

> ⚠️ **Advertencia de Editor no Verificado**: La pantalla de consentimiento puede mostrar una advertencia de que "Mail-Archiver" es un editor no verificado. Esto es normal; la verificación del editor requiere una identidad comercial (cuenta de Microsoft Partner Network) y no es necesaria para que el flujo funcione. La aplicación funciona normalmente sin ella.

---

## 🔧 Opción B: Registro de Aplicación Personalizado (Usuarios Avanzados) {#option-b-custom-app-registration-power-users}

Utilice esta opción si no desea utilizar el Client ID predeterminado compartido (por ejemplo, por razones de cumplimiento o si desea tener un control total sobre el registro de la aplicación).

### 🛠️ Requisitos Previos {#prerequisites}

- Una cuenta Microsoft (personal o de trabajo/escuela) que pueda acceder al Azure Portal
- Familiaridad básica con el portal Azure/Entra

### 🚀 Crear Registro de Aplicación {#create-app-registration}

1. Navegue al [Microsoft Entra Admin Center](https://entra.microsoft.com) o al [Azure Portal](https://portal.azure.com).
2. Inicie sesión con su cuenta Microsoft.
3. En el panel de navegación izquierdo, seleccione **App registrations** (bajo Identity > Applications).
4. Haga clic en **+ New registration**.
5. Complete los siguientes detalles:
   - **Name**: Ingrese un nombre descriptivo (ej. "Mail Archiver MSA")
   - **Supported account types**: Seleccione **Accounts in any organizational directory and personal Microsoft accounts**
     - Esta es la opción multi-inquilino y admite tanto cuentas personales como organizacionales.
   - **Redirect URI**: Deje esto en blanco (no es necesario para el Device Code Flow).
6. Haga clic en **Register**.
7. Anote el **Application (client) ID** de la página **Overview**.

### 🔐 Habilitar Flujos de Cliente Público {#enable-public-client-flows}

1. En su registro de aplicación, navegue a **Authentication** en el menú izquierdo.
2. En la parte inferior, busque **Allow public client flows** y cámbielo a **Yes**.
3. Haga clic en **Save**.

> ℹ️ No es necesario configurar manualmente ningún client secret, redirect URI ni Permisos de API — el Device Code Flow utiliza permisos delegados que se solicitan en tiempo de ejecución a través del alcance `IMAP.AccessAsUser.All`.

### 📧 Configurar Mail Archiver {#configure-mail-archiver}

Sobrescriba el valor predeterminado global (afecta a todas las cuentas MSA)

Establezca el Client ID en su variable de entorno:


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 Actualización y Revocación de Tokens {#token-refresh-revocation}

### Actualización Automática de Tokens {#automatic-token-refresh}

Después de la autorización inicial, Mail Archiver recibe:
- Un **access token** (de corta duración, ~1 hora)
- Un **refresh token** (de larga duración, utilizado para obtener nuevos access tokens automáticamente)

Mail Archiver actualiza automáticamente el access token antes de cada ejecución de Sincronización. No es necesario volver a autorizar a menos que el refresh token expire o sea revocado.

### Revocar Acceso {#revoking-access}

Para revocar el acceso de Mail Archiver a su cuenta Microsoft personal:

1. Visite [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage)
2. Inicie sesión con su cuenta Microsoft personal.
3. Busque **"Mail-Archiver"** (o el nombre de su aplicación personalizada) en la lista.
4. Haga clic en **Remove** para revocar el acceso.

Después de la revocación, Mail Archiver ya no podrá acceder a la Cuenta hasta que vuelva a autorizarla.

---

## ⚠️ Notas Importantes {#important-notes}
- **Client ID Compartido**: Al utilizar el Client ID predeterminado, todos los usuarios de Mail Archiver en todas las instancias comparten el mismo Azure App Registration. El mantenedor de Mail Archiver es responsable de mantener el registro activo. Si Microsoft desactiva el registro compartido, todos los usuarios tendrían que cambiar a un registro personalizado (Opción B) hasta que una nueva versión incluya un Client ID actualizado.


- **Cuentas Organizacionales**: El proveedor Microsoft Personal utiliza el endpoint de autoridad `/common` y admite tanto cuentas personales como organizacionales (trabajo/escuela). Para cuentas organizacionales que requieren acceso mediante client-credentials (solo aplicación, sin inicio de sesión de usuario), utilice el proveedor **M365** en su lugar.

---

## 🔍 Solución de Problemas {#troubleshooting}

### Carpetas visibles en outlook.com pero ausentes en Mail Archiver {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**Síntoma**: Algunas Carpetas existen en el Buzón (visibles en outlook.com o en Outlook), pero nunca aparecen en Mail Archiver — ni en la lista de Carpetas en la página de configuración de la Cuenta ni durante la Sincronización. No se registra ningún error.

**Causa (lado del servidor)**: Las Carpetas que fueron creadas por **clientes de terceros a través de EWS** — lo que configura las cuentas de outlook.com como cuentas de Exchange en lugar de IMAP — pueden terminar con una propiedad `PR_CONTAINER_CLASS` incorrecta (ej. `IPF.Imap` en lugar de `IPF.Note`). Outlook.com entonces **filtra estas carpetas de la respuesta IMAP `LIST` por completo**, aunque sean totalmente visibles en la interfaz web. Este es un comportamiento del lado de Microsoft, no un error de Mail Archiver — el mismo fenómeno está documentado para la Microsoft Graph API, donde las carpetas con una clase de contenedor que no es `IPF.Note` faltan en los listados de `mailFolders` hasta que se corrige la propiedad:

Mail Archiver consulta la lista de Carpetas desde tres fuentes (`LIST` recursivo, recorrido por niveles de los hijos de cada Carpeta, y `LSUB`) y combina los resultados. Cuando el servidor no informa una Carpeta a través de ninguna de ellas, no hay forma de que ningún cliente IMAP pueda verla.

**Solución alternativa**: Toque las Carpetas afectadas una vez a través de la interfaz web de outlook.com para que Microsoft corrija la clase de contenedor:

1. Abra <https://outlook.com> y localice la Carpeta ausente.
2. Cámbiele el nombre (cualquier nombre temporal) y vuelva a cambiarlo al original — o cree una nueva Carpeta y mueva los correos.
3. Repita para cada Carpeta afectada.
4. Inicie una **Full Resync** de la Cuenta desde la página de Account Details en Mail Archiver. Se requiere una resincronización completa porque las sincronizaciones incrementales solo obtienen correos más nuevos que la última fecha de Sincronización de la Cuenta — de lo contrario, los correos en Carpetas que nunca fueron descubiertas permanecerían sin Archivo.


*Esta guía está actualizada a 2026. Microsoft actualiza regularmente sus servicios e interfaz de usuario, por lo que algunos pasos pueden variar. Consulte la [documentación de la plataforma de identidad de Microsoft](https://learn.microsoft.com/en-us/entra/identity-platform/) para obtener los últimos detalles.*
