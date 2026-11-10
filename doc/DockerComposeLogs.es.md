# 🐳 Guía de Logs de Docker Compose {#docker-compose-logs-guide}

[← Volver al Índice de Documentación](index.md)

## 📋 Resumen {#overview}

Esta guía explica cómo ver y configurar los logs al ejecutar la aplicación Mail Archiver utilizando Docker Compose. Una gestión adecuada de los logs es esencial para supervisar la salud de la aplicación, solucionar problemas y comprender el comportamiento del sistema.

## 📖 Visualización de Logs de Docker Compose {#viewing-docker-compose-logs}

### Comandos Básicos de Logs {#basic-log-commands}

Para ver los logs de todos los servicios en su configuración de Docker Compose:

```bash
docker compose logs
```

Para ver los logs de un servicio específico (por ejemplo, la aplicación Mail Archiver):

```bash
docker compose logs mailarchive-app
```

### Transmisión de Logs en Tiempo Real {#real-time-log-streaming}

Para seguir los logs en tiempo real (similar a `tail -f`):

```bash
docker compose logs -f
```

Para seguir los logs de un servicio específico:

```bash
docker compose logs -f mailarchive-app
```

### Filtrado de Logs y Opciones {#log-filtering-and-options}

Mostrar las últimas N líneas de logs:
```bash
docker compose logs --tail=100
```

Mostrar logs desde una hora específica:
```bash
docker compose logs --since=1h
```

Mostrar logs hasta una hora específica:
```bash
docker compose logs --until=2025-01-01T12:00:00
```

Mostrar marcas de tiempo en los logs:
```bash
docker compose logs -t
```

## 📝 Configuración del Nivel de Log {#log-level-configuration}

La aplicación Mail Archiver admite varios niveles de log que controlan la verbosidad de la salida. Puede configurar estos niveles utilizando variables de entorno en su archivo `docker-compose.yml`.

### Niveles de Log Disponibles {#available-log-levels}

Los siguientes niveles de log están disponibles, ordenados de mayor a menor verbosidad:

- **`Trace`** - Registro más detallado, incluyendo mensajes internos del framework
- **`Debug`** - Información detallada de depuración para desarrollo y resolución de problemas
- **`Information`** - Información general sobre el flujo de la aplicación (nivel predeterminado)
- **`Warning`** - Mensajes de advertencia sobre problemas potenciales
- **`Error`** - Mensajes de error sobre operaciones fallidas
- **`Critical`** - Mensajes de error críticos que requieren atención inmediata
- **`None`** - Sin salida de logs

### Configuración de Log Predeterminada {#default-log-configuration}

Por defecto, la aplicación utiliza la siguiente configuración de nivel de log:
- **Predeterminado**: `Information`
- **Microsoft.AspNetCore**: `Warning` (reduce el ruido del framework)
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning` (reduce el ruido de los comandos de la base de datos)

### Configuración de Niveles de Log en Docker Compose {#configuring-log-levels-in-docker-compose}

Para personalizar los niveles de log, añada las siguientes variables de entorno a su archivo `docker-compose.yml` bajo el servicio `mailarchive-app`:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Logging Settings (Optional)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning
```

### Ejemplo: Configuración de Depuración {#example-debug-configuration}

Para fines de resolución de problemas, es posible que desee habilitar un registro más detallado:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Verbose Logging for Debugging
      - Logging__LogLevel__Default=Debug
      - Logging__LogLevel__Microsoft_AspNetCore=Information
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Debug
```

### Ejemplo: Configuración de Producción {#example-production-configuration}

Para entornos de producción, es posible que desee reducir la verbosidad de los logs para mejorar el rendimiento y reducir el volumen de logs:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Production Logging (Reduced Verbosity)
      - Logging__LogLevel__Default=Warning
      - Logging__LogLevel__Microsoft_AspNetCore=Error
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Error
```

## 🔍 Comprensión de la Salida de Logs {#understanding-log-output}

### Formato de Log {#log-format}

Los logs siguen este formato general:
```
timestamp [log-level] category - message
```

Ejemplo:
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### Categorías de Log Comunes {#common-log-categories}

- **`MailArchiver.Services.*`** - Logs del servicio de la aplicación
- **`MailArchiver.Controllers.*`** - Logs del controlador de la Web API
- **`Microsoft.AspNetCore`** - Logs del framework ASP.NET Core
- **`Microsoft.EntityFrameworkCore`** - Logs de Entity Framework
- **`Microsoft.Extensions.Hosting`** - Logs relacionados con el Hosting

## 🛠️ Consejos para la Resolución de Problemas {#troubleshooting-tips}

### 1. Encontrar Mensajes de Error {#1-finding-error-messages}

Para encontrar rápidamente mensajes de error en los logs:
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. Supervisar Operaciones Específicas {#2-monitoring-specific-operations}

Para supervisar la Sincronización de correo electrónico:
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. Comprobar Advertencias {#3-checking-for-warnings}

Para comprobar si hay mensajes de advertencia:
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 Mejores Prácticas de Gestión de Logs {#log-management-best-practices}

### Para Desarrollo {#for-development}
- Use el nivel `Debug` o `Information` para una resolución de problemas detallada
- Habilite la transmisión de logs en tiempo real durante el desarrollo
- Supervise los logs continuamente mientras prueba nuevas funciones

### Para Producción {#for-production}
- Use el nivel `Warning` o `Error` para reducir el volumen de logs
- Implemente la rotación de logs para evitar problemas de espacio en disco
- Considere soluciones de registro centralizado para configuraciones de múltiples contenedores
- Revise regularmente los logs en busca de eventos de seguridad y problemas de rendimiento

## 📚 Documentación Relacionada {#related-documentation}

- [Guía de Instalación y Configuración](Setup.md) - Instrucciones de configuración completas
- [Registro de Acceso](Logs.md) - Registro de acceso a nivel de aplicación
- [Guía de Copia de seguridad y Restauración](BackupRestore.md) - Procedimientos de protección de datos
