# 🔐 Autenticación de dos factores {#two-factor-authentication}

[← Volver al índice de la documentación](index.md)

## 📋 Descripción general {#overview}

La autenticación de dos factores (2FA) añade un segundo paso al proceso de inicio de sesión: después de su contraseña, Mail Archiver solicita un **código de 6 dígitos** generado por una aplicación de autenticación en su teléfono. Una contraseña robada por sí sola ya no es suficiente para acceder al Archivo.

Mail Archiver implementa 2FA utilizando **TOTP** (Time-based One-Time Password, RFC 6238), el mismo estándar utilizado por Google Authenticator, Microsoft Authenticator, Authy, 1Password, Bitwarden y otros. No interviene ningún servicio externo; el secreto nunca sale de su instancia.

**La 2FA es opcional por usuario.** Cada usuario la configura para su propia Cuenta, no de forma global por un administrador.

## 🛠️ Requisitos previos {#prerequisites}

- Una aplicación de autenticación compatible con TOTP en su teléfono o escritorio
- Acceso a su propia Cuenta (debe haber iniciado sesión)
- **Una Cuenta (con contraseña) local** — consulte la nota sobre OIDC a continuación

> **⚠️ No disponible para usuarios de OIDC.** Si inicia sesión a través de un proveedor de identidad externo (Entra ID, Authelia, Keycloak, …), la 2FA es gestionada por ese proveedor y la página de configuración está bloqueada con el mensaje *"OIDC users cannot enable 2FA. Authentication is managed by your OIDC provider."* En su lugar, habilite la autenticación de múltiples factores en su proveedor de identidad; consulte [OIDC / SSO](OIDC_Implementation.md).

## 🚀 Habilitar la autenticación de dos factores {#enabling-two-factor-authentication}

### Paso 1 — Abrir la página de configuración {#step-1-open-the-setup-page}

1. Inicie sesión en Mail Archiver
2. Abra el menú de su Cuenta y elija **Two-Factor Authentication**
3. Llegará a **Two-Factor Setup**

### Paso 2 — Escanear el código QR {#step-2-scan-the-qr-code}

La página muestra un código QR. Abra su aplicación de autenticación, elija *Add account* → *Scan QR code* y apunte la cámara hacia él.

**¿No puede escanear el código?** Use la **Entrada manual** en su lugar: la página muestra el secreto como texto (`If you can't scan the QR code, enter this secret manually in your authenticator app`). Escríbalo en su aplicación como una cuenta basada en el tiempo/TOTP.

> **ℹ️ El secreto aún no se guarda.** Solo se mantiene durante la duración de la configuración en su sesión del navegador. La 2FA se activa solo después de que el paso 3 tenga éxito; si abandona la configuración, nada cambia en su Cuenta.

### Paso 3 — Confirmar con un código {#step-3-confirm-with-a-code}

Introduzca el código de 6 dígitos que muestra su aplicación y haga clic en **Enable Two-Factor Authentication**.

Si el código es rechazado, se mostrará la página de configuración nuevamente con un nuevo código QR y el mensaje *"Invalid authentication code. Please try again."*. Compruebe que el reloj de su teléfono esté configurado automáticamente; TOTP depende de una hora precisa.

### Paso 4 — Guardar sus códigos de respaldo {#step-4-save-your-backup-codes}

Tras el éxito, se le dirigirá a la página **Backup Codes**. Se muestran **10 códigos**, cada uno de 16 caracteres de longitud.

> **⚠️ Esta es la única vez que se muestran los códigos.** Se almacenan mediante un hash; ni usted ni un administrador pueden volver a mostrarlos más tarde. Use **Download Backup Codes** para guardarlos en un archivo, o cópielos en su gestor de contraseñas antes de salir de la página.

La descarga contiene este recordatorio:

> *Mail Archiver - Two-Factor Authentication Backup Codes. Store these backup codes in a safe place. You can use them to log in if you lose access to your authenticator app. Each backup code can only be used once. After using a code, it will be invalidated.*
> *Important: Keep these codes secure and do not share them with anyone.*

## 🔑 Iniciar sesión con 2FA {#logging-in-with-2fa}

Después de introducir su contraseña, se le dirigirá a **Two-Factor Verification** y se le pedirá un código:

> *Enter a 6-digit TOTP code from your authenticator app or a 16-character backup code*

Ambos funcionan en el mismo campo: Mail Archiver los distingue por su longitud:

| Entrada | Longitud | Comportamiento |
|---|---|---|
| Código de su aplicación de autenticación | 6 dígitos | Verificado contra la ventana de tiempo actual |
| Código de respaldo | 16 caracteres | Verificado contra sus códigos almacenados, luego se **invalida** |

Para aceptar pequeñas diferencias de reloj, también se aceptan códigos del paso de tiempo inmediatamente anterior y posterior.

La opción **"Remember me"** se transfiere desde el formulario de inicio de sesión: si la marcó, permanecerá con la sesión iniciada tras reiniciar el navegador después de completar el paso de 2FA. La selección se mantiene en la sesión del lado del servidor mientras el paso de 2FA está pendiente, por lo que solo sobrevive si introduce el código dentro del tiempo de espera de inactividad de **60 minutos** de la sesión. Los detalles y límites (vida útil absoluta de 7 días, sin efecto para usuarios de OIDC) se encuentran en [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).

### Uso de un código de respaldo {#using-a-backup-code}

1. Introduzca uno de sus códigos de 16 caracteres guardados en el mismo campo
2. El código se acepta e **invalida inmediatamente**; no puede volver a utilizarse
3. Los inicios de sesión exitosos con un código de respaldo se registran en el registro de acceso con la nota `(2FA Backup Code)`, para que pueda detectar un uso inusual más tarde

### Limitación de tasa (Rate limiting) {#rate-limiting}

Para ralentizar los intentos de fuerza bruta, la verificación de 2FA está limitada a **5 intentos por cada 15 minutos**, contados por dirección IP y nombre de usuario. Después de eso, verá:

> **Too Many Attempts** — *You have made too many 2FA verification attempts. Please wait and try again later.*

Espere a que pase el intervalo e inténtelo de nuevo. El límite se aplica tanto a los intentos fallidos como a los exitosos dentro de la ventana.

## 🚫 Deshabilitar la autenticación de dos factores {#disabling-two-factor-authentication}

1. Abra **Two-Factor Authentication** en el menú de su Cuenta
2. La página solicita confirmación: *"Are you sure you want to disable two-factor authentication? This will reduce the security of your account."*
3. Introduzca su **contraseña** — este paso requiere su contraseña actual, no un código 2FA
4. Haga clic en **Disable Two-Factor Authentication**

Deshabilitar la función elimina el secreto e invalida todos los códigos de respaldo restantes. Su próximo inicio de sesión solo requerirá la contraseña.

## 🔄 ¿Perdió su autenticador? Dos formas de volver a entrar {#lost-your-authenticator-two-ways-back-in}

### Si aún tiene códigos de respaldo {#if-you-still-have-backup-codes}

Inicie sesión con un código de respaldo como se describió anteriormente, luego deshabilite y vuelva a habilitar la 2FA con su nuevo dispositivo. Recuerde guardar los nuevos códigos de respaldo.

### Si no le quedan códigos de respaldo {#if-you-have-no-backup-codes-left}

Un **administrador** debe restablecer la 2FA de su Cuenta:

1. El administrador abre **Users**
2. Edita al usuario afectado
3. Elige **Reset Two-Factor Authentication** y confirma:
   *"Are you sure you want to reset two-factor authentication for this user? They will need to set it up again."*
4. El usuario podrá entonces iniciar sesión solo con su contraseña y configurar la 2FA de nuevo

El restablecimiento borra el secreto y los códigos de respaldo. El mensaje de confirmación *"Two-factor authentication has been successfully reset for user …"* aparece en la lista de Usuarios.

> **ℹ️ Alcance del restablecimiento.** El restablecimiento cubre solo la 2FA. **No** es un restablecimiento de contraseña; consulte [Emergency Account Recovery](EmergencyAccountRecovery.md) si el problema es la contraseña o si la Cuenta está bloqueada por completo.

## 👁️ Lo que los administradores pueden ver {#what-administrators-can-see}

La vista general de **Users** muestra una columna de 2FA:

- *"Two-factor authentication is currently enabled for this user."* — La 2FA está activa
- De lo contrario, la 2FA está desactivada para ese usuario

Los administradores pueden restablecer la 2FA (arriba) pero **no pueden** leer el secreto ni los códigos de respaldo; ambos se almacenan mediante hash. Un usuario que pierde su dispositivo sin códigos de respaldo no tiene otra forma de entrar que mediante el restablecimiento del administrador.

## 🔍 Pista de auditoría {#audit-trail}

La actividad de 2FA se escribe en el registro y, para los inicios de sesión exitosos, en el registro de acceso:

| Evento | Dónde | Entrada |
|---|---|---|
| Inicio de sesión vía aplicación de autenticación | Registro de acceso | `Login` con nota `IP: <address> (2FA)` |
| Inicio de sesión vía código de respaldo | Registro de acceso | `Login` con nota `IP: <address> (2FA Backup Code)` |
| Intento de 2FA fallido | Registro de la aplicación | Advertencia con nombre de usuario, IP y token enmascarado |
| Código de respaldo inválido | Registro de la aplicación | Advertencia con nombre de usuario e IP |

La entrada en el registro de acceso hace visible el uso de códigos de respaldo a posteriori, lo cual es útil para detectar si un código está siendo utilizado por alguien que no debería tenerlo. Consulte [Access Logging](Logs.md) para saber cómo leer estas entradas.

## ⚙️ Cómo funciona internamente {#how-it-works-under-the-hood}

Para administradores y revisores:

- **Estándar**: TOTP (RFC 6238), secreto aleatorio de 20 bytes, codificado en base32
- **Ventana de verificación**: ±1 paso de tiempo, para tolerar la deriva del reloj
- **Códigos de respaldo**: 10 códigos, 16 caracteres cada uno, generados a partir de un RNG criptográfico, almacenados mediante **hash**, invalidados individualmente al usarse
- **Almacenamiento del secreto**: en el registro del usuario; nunca se devuelve al navegador después de la configuración
- **Gestión de sesiones**: la sesión se regenera antes de que se almacene la identidad de 2FA, por lo que una cookie de sesión plantada antes del inicio de sesión no puede trasladarse a la sesión autenticada
- **Cuentas OIDC**: la gestión de 2FA está bloqueada; el proveedor es la autoridad
- **API keys**: omiten la 2FA por diseño, ya que los tokens no interactivos no pueden responder a un desafío; consulte la justificación de seguridad en la [REST API guide](API.md)

## ❓ Resolución de problemas {#troubleshooting}

| Síntoma | Causa | Solución |
|---|---|---|
| *"Invalid authentication code"* | Reloj del teléfono desajustado, o cuenta incorrecta en la aplicación | Habilite la Sincronización automática de la hora; compruebe que la aplicación muestra la entrada de Mail Archiver |
| *"Two-factor secret is missing. Please restart the setup process."* | La página de configuración estuvo abierta demasiado tiempo, la sesión expiró | Inicie la configuración de nuevo desde el menú de la Cuenta |
| *"Your session has expired. Please log in again."* | Pasó demasiado tiempo en la página de verificación | Inicie sesión de nuevo |
| *"Two-factor authentication is not enabled for this account."* | La 2FA fue restablecida o nunca se completó | Configúrela de nuevo, o pida ayuda a un administrador |
| Código de respaldo rechazado | Código ya utilizado, o carácter mal leído | Los códigos de respaldo funcionan **una vez**; use otro, o solicite el restablecimiento de la 2FA |
| *"Too Many Attempts"* | Más de 5 intentos de verificación en 15 minutos | Espere a que pase el intervalo |
| No hay página de configuración en el menú de la Cuenta | Inicia sesión vía OIDC | Habilite MFA en su proveedor de identidad |
| No hay columna de 2FA en la lista de Usuarios | No es un administrador | Pida ayuda a un administrador |

## 📚 Documentación relacionada {#related-documentation}

- [User Management and Mailbox Permissions](UserManagement.md) — creación de Cuentas, derechos de administrador
- [OIDC / SSO Authentication](OIDC_Implementation.md) — inicio de sesión a través de proveedores externos
- [Emergency Account Recovery](EmergencyAccountRecovery.md) — Cuentas bloqueadas
- [Access Logging](Logs.md) — lectura del Registro de auditoría
- [REST API](API.md) — por qué las API keys omiten la 2FA
