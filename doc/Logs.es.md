# 📋 Registro de auditoría {#access-log}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

Esta guía explica la funcionalidad del Registro de auditoría en la aplicación Mail Archiver, que permite el registro y la visualización de las actividades de los usuarios dentro del sistema. Esta funcionalidad es crucial para fines de monitoreo, seguridad y depuración.

## 📊 Qué se registra {#what-is-logged}

La aplicación registra varios tipos de actividades de usuario, incluyendo:

1. **Login and Logout** - Registro de las operaciones de inicio y cierre de sesión
2. **Search Operations** - Registro de las solicitudes de búsqueda de correo electrónico dentro del sistema
3. **Email Opening** - Registro de cuando los usuarios abren el contenido del correo electrónico
4. **Email Downloading** - Registro de cuando los usuarios descargan correos electrónicos
5. **Email Restoration** - Registro de cuando los usuarios realizan la Restauración de correos electrónicos archivados
6. **Account Management** - Registro de acciones relacionadas con la Cuenta
7. **Deletion Operations** - Registro de acciones de eliminación
8. **Audit Data Export** - Registro de las ejecuciones de exportación de datos de auditoría (inicio y resultado, ver [Audit Data Export](AuditExport.md))

## 📝 Action Types and Descriptions {#action-types-and-descriptions}

Las acciones registradas se categorizan en los siguientes tipos:

### Login {#login}
- **Description**: Registra cuando un usuario inicia sesión en el sistema
- **Details**: Username, timestamp

### Logout {#logout}
- **Description**: Registra cuando un usuario cierra sesión en el sistema
- **Details**: Username, timestamp

### Search {#search}
- **Description**: Registra las solicitudes de búsqueda dentro del sistema de correo electrónico
- **Details**: Username, timestamp, search parameters

### Open {#open}
- **Description**: Registra cuando un usuario abre un correo electrónico
- **Details**: Username, timestamp, email ID, sender, subject

### Download {#download}
- **Description**: Registra cuando los usuarios descargan correos electrónicos
- **Details**: Username, timestamp, email ID, sender, subject

### Restore {#restore}
- **Description**: Registra cuando los usuarios realizan la Restauración de correos electrónicos archivados
- **Details**: Username, timestamp, email ID, sender, subject

### Account {#account}
- **Description**: Registra acciones relacionadas con la Cuenta (creación, modificación, etc.)
- **Details**: Username, timestamp, account ID

### Deletion {#deletion}
- **Description**: Registra las operaciones de eliminación en correos electrónicos u otros recursos
- **Details**: Username, timestamp, affected resource
- **Compliance Note**: Las operaciones de eliminación se registran para fines de auditoría de cumplimiento

## 🔍 Filter Options {#filter-options}

La página de registros ofrece las siguientes opciones de filtrado:

1. **Date** - Filtrar por rango de fechas (From-To)
2. **Username** - Filtrar por un usuario específico (Solo Admin)
3. **Action Type** - Filtrar por un tipo de acción específico

## 🖥️ Log Display {#log-display}

Los registros se muestran en formato de tabla con las siguientes columnas:

- **Timestamp**: Fecha y hora de la acción
- **Username**: Nombre del usuario involucrado
- **Type**: Tipo de acción con codificación por colores
- **Information**: Detalles adicionales sobre la acción

Para dispositivos móviles, los registros se muestran en formato de tarjeta.

## 🔐 Access Permissions {#access-permissions}

- **Non-admin users**: Solo pueden ver sus propios registros
- **Admin users**: Pueden ver los registros de todos los usuarios y filtrar por usuario

## 📤 Audit Data Export {#audit-data-export}

Los administradores pueden abrir la página dedicada de [Audit Data Export](AuditExport.md) directamente desde la página de Logs. Esta crea paquetes de datos masivos tabulares (INDEX.XML + CSV) desde el Archivo para herramientas de auditoría externas. Cada ejecución de exportación escribe dos entradas de Registro de auditoría del tipo **Audit Data Export** (inicio y resultado), las cuales se enumeran y se pueden filtrar en la página de Logs al igual que todas las demás entradas.

Para más detalles, consulte la [Audit Data Export guide](AuditExport.md).
