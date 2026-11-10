# ☁️ Guía de Registro de Aplicación de Azure y Política de Retención para Mail Archiver {#azure-app-registration-and-retention-policy-guide-for-mail-archiver}

[← Volver al índice de documentación](index.md)

## 📋 Descripción general {#overview}

Esta guía proporciona instrucciones detalladas para la configuración del Registro de Aplicación de Azure para la recuperación de correos electrónicos y la configuración de políticas de retención en la aplicación Mail Archiver.

## 📚 Tabla de contenidos {#table-of-contents}

1. [Descripción general](#overview)
2. [Registro de Aplicación de Azure para la recuperación de correos electrónicos](#azure-app-registration-for-email-retrieval)
   - [Requisitos previos](#prerequisites)
   - [Crear Registro de Aplicación](#create-app-registration)
   - [Establecer Permisos de API requeridos](#set-required-api-permissions)
   - [Generar Secreto de Cliente](#generate-client-secret)
3. [Configurar Cuenta de M365 de Mail Archiver](#configure-mail-archiver-m365-account)
4. [Configuración de la Política de Retención](#retention-policy-setup)
   - [Configurar Política de Retención](#configure-retention-policy)

## 🌐 Descripción general {#overview}

La aplicación Mail Archiver es compatible con cuentas de Microsoft 365 (M365) utilizando el flujo de credenciales de cliente OAuth2 para la recuperación de correos electrónicos. Además, proporciona la funcionalidad de política de retención para eliminar automáticamente los correos electrónicos del servidor de correo después de un período especificado, lo que ayuda a gestionar el espacio de almacenamiento manteniendo un archivo completo.

## ☁️ Registro de Aplicación de Azure para la recuperación de correos electrónicos {#azure-app-registration-for-email-retrieval}

### 🛠️ Requisitos previos {#prerequisites}

- Acceso de administrador a Microsoft Entra ID (Azure AD)
- Un tenant de Microsoft 365 con licencias de Exchange Online

### 🚀 Crear Registro de Aplicación {#create-app-registration}

1. Navegue a [Microsoft Entra Admin Center](https://entra.microsoft.com)
2. Inicie sesión con su cuenta de administrador
3. En el panel de navegación izquierdo, seleccione **App registrations**
4. Haga clic en **+ New registration** en la parte superior de la página de App registrations
5. Complete los siguientes detalles:
   - **Name**: Introduzca un nombre descriptivo (por ejemplo, "Mail Archiver M365 Provider")
   - **Supported account types**: Seleccione **Accounts in this organizational directory only** (Single tenant)
     - Esto es necesario para el flujo de credenciales de cliente utilizado por Mail Archiver
   - **Redirect URI**: Deje este campo en blanco (no es necesario para el flujo de credenciales de cliente)
6. Haga clic en **Register**

**Importante**: Anote los siguientes valores de la página de **Overview**:
- **Application (client) ID** - Necesitará esto como `ClientId` en Mail Archiver
- **Directory (tenant) ID** - Necesitará esto como `TenantId` en Mail Archiver

### 🔐 Establecer Permisos de API requeridos {#set-required-api-permissions}

1. Navegue a **API permissions** en el menú izquierdo
2. Haga clic en **+ Add a permission**
3. Seleccione **Microsoft Graph**
4. Elija **Application permissions** (requerido para el flujo de credenciales de cliente de Mail Archiver)
5. Agregue los siguientes permisos:
   - **Mail.Read** - Leer correo en todos los Buzones
   - **Mail.ReadWrite** - Leer y escribir correo en todos los Buzones (para la función de Restauración y también para la eliminación)
   - **User.Read.All** - Leer perfiles completos de todos los usuarios (requerido para la importación de Buzones de tenant de Microsoft 365; también proporciona `assignedPlans` para el filtrado de licencias de Exchange)

6. Haga clic en **Add permissions**
7. **CRÍTICO**: Haga clic en **Grant admin consent for [Your Organization]**
8. Confirme haciendo clic en **Yes**

**Nota**: Los permisos de aplicación son necesarios porque Mail Archiver utiliza el flujo de credenciales de cliente para acceder a los Buzones sin interacción del usuario.

### 🔑 Generar Secreto de Cliente {#generate-client-secret}

1. Navegue a **Certificates & secrets** en el menú izquierdo
2. En **Client secrets**, haga clic en **+ New client secret**
3. Proporcione una descripción (por ejemplo, "Mail Archiver Secret")
4. Seleccione un período de expiración
5. Haga clic en **Add**
6. **Importante**: Copie el **Value** inmediatamente y guárdelo de forma segura. Este secreto no se volverá a mostrar.

## 📧 Configurar Cuenta de M365 de Mail Archiver {#configure-mail-archiver-m365-account}

Después de completar el registro de la aplicación, debe configurar una cuenta de correo de M365 en Mail Archiver con los siguientes valores:

### 📋 Valores requeridos del Registro de Aplicación: {#required-values-from-app-registration}

1. **Client ID**: Copie el **Application (client) ID** de la página **Overview** del registro de la aplicación
2. **Client Secret**: Copie el **Value** que guardó al crear el secreto de cliente
3. **Tenant ID**: Copie el **Directory (tenant) ID** de la página **Overview** del registro de la aplicación

### 🚀 Crear Cuenta de M365 en Mail Archiver: {#creating-m365-account-in-mail-archiver}

1. Inicie sesión en su aplicación Mail Archiver
2. Navegue a **Mail Accounts** > **Create**
3. Complete los siguientes campos:
   - **Name**: Nombre descriptivo para la Cuenta (por ejemplo, "Sales Team M365")
   - **Email Address**: La dirección de correo del Buzón de destino para archivar
   - **Provider**: Seleccione **M365**
   - **Client ID**: Introduzca el Application (client) ID de su registro de aplicación
   - **Client Secret**: Introduzca el valor del secreto de cliente que guardó
   - **Tenant ID**: Introduzca el Directory (tenant) ID de su registro de aplicación

4. Haga clic en **Create**

### ⚠️ Notas importantes: {#important-notes}

- El campo **Email Address** debe contener el Buzón real que desea archivar al crear una única Cuenta de M365.
- El registro de la aplicación debe tener permisos para acceder al Buzón especificado.
- Para crear Cuentas para todos o para Buzones seleccionados en el tenant a la vez, utilice la guía [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md).


## 🗑️ Configuración de la Política de Retención {#retention-policy-setup}

La aplicación Mail Archiver proporciona la funcionalidad de política de Retención para eliminar automáticamente los correos electrónicos del servidor IMAP después de haber sido archivados con éxito, lo que ayuda a gestionar el espacio de almacenamiento en el servidor IMAP mientras se mantiene un archivo completo.

### 🛠️ Configurar Política de Retención {#configure-retention-policy}

1. Navegue a **Mail Accounts** en la aplicación Mail Archiver
2. Cree una nueva Cuenta de M365 o edite una existente
3. En el formulario de configuración de la Cuenta, localice el campo **Delete After Days**
4. Introduzca el número de días tras los cuales los correos electrónicos archivados deben eliminarse del servidor:
   - Por ejemplo, introduzca `30` para eliminar correos después de 30 días
   - Deje vacío para desactivar la eliminación automática
5. Guarde la configuración de la Cuenta


## ℹ️ Información relativa a los Buzones Migrados {#ℹ-information-regarding-migrated-mailboxes}

### 📁 Carpetas no visibles tras la migración {#folders-not-visible-after-migration}

Si ha migrado su Buzón desde otro proveedor de correo electrónico a Microsoft 365, es posible que algunas carpetas no aparezcan en la lista de Carpetas.

#### 🔍 Antecedentes {#background}

Mail Archiver recupera la información de las Carpetas de los Buzones de M365 a través de la API de Microsoft Graph. La aplicación solicita correctamente todas las Carpetas disponibles (incluidas las carpetas ocultas) y recorre la jerarquía completa de Carpetas.

Sin embargo, la API de Microsoft Graph solo devuelve las Carpetas con `PR_CONTAINER_CLASS` establecido en `IPF.Note`. Durante las migraciones de Buzones desde otros sistemas de correo electrónico, las Carpetas pueden tener asignada una clase de contenedor diferente, como `IPF.Imap`. Estas Carpetas no se exponen a través de los endpoints de listado de Carpetas de la API de Graph.

Este comportamiento afecta a todas las aplicaciones que utilizan esta API para la recuperación de Carpetas.

#### 🔧 Opciones de solución temporal {#workaround-options}

**Opción 1: Usar la herramienta MFCMAPI (Recomendado)**

1. Descargue [MFCMAPI](https://github.com/Microsoft/mfcmapi/) desde el repositorio oficial de Microsoft
2. Inicie MFCMAPI y conéctese a su Buzón
3. Navegue a cada Carpeta faltante
4. Cambie la propiedad `PR_CONTAINER_CLASS` de `IPF.Imap` a `IPF.Note`
5. Reinicie Outlook y recargue las Carpetas en Mail Archiver

**Opción 2: Recrear Carpetas en Outlook**

1. Cree nuevas Carpetas directamente en Outlook (estas tendrán la clase `IPF.Note` correcta)
2. Mueva todos los correos electrónicos de las Carpetas migradas a las nuevas Carpetas
3. Elimine las antiguas Carpetas migradas si lo desea

**Opción 3: Script de PowerShell (Para cambios masivos)**

Para Buzones con muchas Carpetas migradas, puede usar PowerShell con el módulo Exchange Online para cambiar la clase de contenedor para múltiples Carpetas a la vez. Contacte con su administrador de Exchange para obtener ayuda con este enfoque.


---

**Nota**: Esta guía está actualizada a 2025. Microsoft actualiza sus servicios regularmente, por lo que algunos elementos de la interfaz de usuario pueden variar. Consulte siempre la documentación más reciente de Microsoft para obtener la información más actualizada.
