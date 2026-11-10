# 🚨 Recuperación de Cuenta de Emergencia {#emergency-account-recovery}

[← Volver al Índice de Documentación](index.md)

## 📋 Descripción general {#overview}

Esta guía proporciona instrucciones detalladas para recuperar el acceso a la aplicación Mail Archiver cuando ha olvidado la contraseña de administrador. Este procedimiento de emergencia le permite recuperar el acceso a su sistema sin perder ningún dato.

> 📝 **Requisito de Versión**: Este método de recuperación está disponible en la versión 2601.1 de Mail Archiver y versiones posteriores.

## ⚠️ Aviso de seguridad importante {#important-security-notice}

Este método de recuperación solo debe utilizarse en situaciones de emergencia cuando no pueda acceder a su Cuenta de administrador. Requiere la modificación temporal de la configuración de su aplicación y debe revertirse inmediatamente después de la recuperación de la contraseña.

## 🛠️ Pasos de recuperación {#recovery-steps}

### 1. Crear Cuenta de Administrador de Emergencia {#1-create-emergency-administrator-account}

Modifique sus variables de entorno de `appsettings.json` o Docker para crear una Cuenta de administrador de emergencia temporal:

**Para Docker Compose (Recomendado):**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Emergency admin account
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempPass123!
      # ... other settings remain unchanged
```

**Para la modificación directa de appsettings.json:**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. Reiniciar la Aplicación/Contenedor {#2-restart-the-applicationcontainer}

Después de modificar la configuración, reinicie su aplicación:

**Para Docker Compose:**
```bash
docker compose down
docker compose up -d
```

**Para la aplicación directa:**
```bash
# Restart your application service
```

### 3. Iniciar sesión con la Cuenta de Emergencia {#3-login-with-emergency-account}

1. Acceda a la interfaz web de Mail Archiver
2. Inicie sesión utilizando las credenciales de emergencia:
   - **Username**: `emergency_admin` (o lo que haya configurado)
   - **Password**: `TempPass123!` (o lo que haya configurado)

### 4. Restablecer la Contraseña de Administrador Original {#4-reset-original-administrator-password}

1. Navegue hasta la sección "Users" desde el menú principal
2. Busque su Cuenta de administrador original en la lista de usuarios
3. Haga clic en "Edit" para ese usuario
4. Cambie la contraseña por una nueva contraseña segura
5. Haga clic en "Save Changes"

### 5. Restaurar la Configuración Original {#5-restore-original-configuration}

Revierte los cambios de configuración al nombre de usuario de administrador original:

**Para Docker Compose:**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Original admin account
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword
      # ... other settings
```

**Para appsettings.json directo:**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. Reiniciar la Aplicación Nuevamente {#6-restart-application-again}

Reinicie la aplicación con la configuración restaurada:

**Para Docker Compose:**
```bash
docker compose down
docker compose up -d
```

## 🎯 Ejemplo: Proceso de Recuperación Completo con Docker Compose {#example-complete-docker-compose-recovery-process}

Aquí tiene un ejemplo completo del proceso de recuperación de emergencia utilizando Docker Compose:

### Paso 1: Configuración de Emergencia {#step-1-emergency-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # EMERGENCY ADMIN ACCOUNT
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempEmergencyPass2026!
      
      # Other settings remain unchanged...
      - MailSync__IntervalMinutes=15
      - TimeZone__DisplayTimeZoneId=Europe/Berlin
      # ... rest of configuration
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Paso 2: Reiniciar e Iniciar Sesión {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### Paso 3: Restablecer la Contraseña Original {#step-3-reset-original-password}
1. Inicie sesión en la interfaz web con las credenciales de emergencia
2. Vaya a la sección Users
3. Edite el usuario administrador original y establezca una nueva contraseña
4. Guarde los cambios

### Paso 4: Restaurar la Configuración Normal {#step-4-restore-normal-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # RESTORED ORIGINAL ADMIN ACCOUNT
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword2026!
      
      # ... rest of configuration unchanged
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Paso 5: Reinicio Final {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 Clave de Cifrado de Credenciales Perdida {#lost-credential-encryption-key}

Si se pierde `Security__CredentialEncryptionKey` (cifrado de credenciales en reposo), las credenciales de la Cuenta de correo almacenadas ya no se pueden descifrar. La aplicación **no** se bloquea: los valores afectados se tratan como "sin credenciales" y se registra una advertencia. Para recuperar:

1. Restaure la clave desde su Copia de seguridad / gestor de secretos y reinicie la aplicación. Las credenciales funcionarán de nuevo inmediatamente, no se requieren cambios de datos.
2. Si la clave se pierde permanentemente, las credenciales cifradas no se pueden recuperar. Las Cuentas afectadas son fáciles de identificar: su Sincronización falla y se escribe una advertencia de descifrado en el registro de la aplicación (y aparece como un problema de sincronización en la interfaz de usuario).
3. Abra cada Cuenta afectada en **Mail Accounts → Edit** y vuelva a introducir sus credenciales: la contraseña IMAP, el client secret de M365, o vuelva a autorizar la Cuenta (MSA). Al guardar el formulario, se vuelve a cifrar la credencial con la clave ahora configurada; no es necesario realizar cambios manuales en la base de datos.

> 💡 La clave nunca se almacena en la base de datos, por lo que las Copias de seguridad de la base de datos por sí solas no son suficientes; siempre realice una Copia de seguridad de la clave por separado.

## 🔒 Mejores Prácticas de Seguridad {#security-best-practices}

1. **Use Contraseñas Temporales Fuertes**: Haga que su contraseña de emergencia sea compleja y única
2. **Cámbiela Inmediatamente**: Restablezca la contraseña de emergencia tan pronto como recupere el acceso
