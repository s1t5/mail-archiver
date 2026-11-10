# 💾 Guía de Copia de Seguridad y Restauración {#backup-and-restore-guide}

[← Volver al Índice de Documentación](index.md)

## 📋 Resumen {#overview}

Esta guía proporciona instrucciones detalladas para realizar la Copia de seguridad y la Restauración de su entorno Mail Archiver Docker. Las Copias de seguridad regulares son esenciales para proteger sus correos electrónicos archivados y los datos de configuración.

## ⚠️ Descargo de Responsabilidad Importante {#important-disclaimer}

**Todos los scripts y comandos proporcionados en esta guía son solo ejemplos.** No se asume ninguna garantía o responsabilidad por su funcionalidad o idoneidad para su entorno específico. Siempre:
- Pruebe los procedimientos de Copia de seguridad y Restauración en un entorno que no sea de producción primero
- Verifique que las Copias de seguridad estén completas y sean restaurables
- Adapte los scripts y comandos a su configuración y requisitos específicos
- Mantenga múltiples copias de Copia de seguridad en diferentes ubicaciones
- Pruebe regularmente sus procedimientos de recuperación ante desastres

**Úselos bajo su propio riesgo.**

## 🎯 Recomendaciones de Copia de Seguridad {#backup-recommendations}

### Copias de Seguridad Offline (Altamente Recomendadas) {#offline-backups-highly-recommended}
Para una máxima consistencia e integridad de los datos, **realice siempre las Copias de seguridad con la aplicación y la base de datos detenidas**. Esto garantiza:
- Sin transacciones de base de datos activas durante la Copia de seguridad
- Consistencia de datos completa en todos los archivos
- Sin riesgo de corrupción de archivos por escrituras concurrentes
- Capacidad de Restauración fiable

Aunque las copias de seguridad en caliente (Métodos 2 y 3) son convenientes para minimizar el tiempo de inactividad, conllevan un mayor riesgo de inconsistencias. Para entornos de producción críticos, programe Copias de seguridad offline regulares durante las ventanas de mantenimiento.

### Copias de Seguridad a Nivel de Sistema {#system-level-backups}
Además de las Copias de seguridad específicas de la aplicación, se **recomienda encarecidamente** crear Copias de seguridad de toda la infraestructura del sistema:
- **Copias de Seguridad de Contenedores LXC**: Si se ejecuta en un contenedor LXC, cree instantáneas (snapshots) offline de todo el contenedor
- **Copias de Seguridad de Máquinas Virtuales**: Para despliegues de VM, cree instantáneas completas de la VM
- **Copias de Seguridad del Sistema Host**: Incluya volúmenes de Docker, configuraciones de contenedores y ajustes del sistema
- **Archivos de Configuración**: Realice la Copia de seguridad de todos los archivos de configuración de Docker y del sistema por separado

Las Copias de seguridad a nivel de sistema proporcionan una capa de seguridad adicional y permiten una recuperación completa ante desastres, incluyendo la capacidad de restaurar todo el entorno en un host diferente si es necesario.

## 🛠️ Requisitos Previos {#prerequisites}

- Configuración de Mail Archiver Docker en ejecución (ver [Guía de Configuración](Setup.md))
- Acceso al sistema host de Docker
- Espacio de almacenamiento suficiente para las Copias de seguridad

## 📦 Qué Copiar de Seguridad {#what-to-backup}

El entorno Mail Archiver Docker consta de varios volúmenes de datos importantes que deben ser respaldados:

### 1. Base de Datos PostgreSQL {#1-postgresql-database}
- **Volumen**: `./postgres-data`
- **Contiene**: Todos los correos electrónicos archivados, cuentas de usuario, cuentas de correo y datos de la aplicación
- **Importancia**: ⭐⭐⭐ Crítico - Este es su almacén de datos principal

### 2. Claves de Protección de Datos {#2-data-protection-keys}
- **Volumen**: `./data-protection-keys`
- **Contiene**: Claves de protección de datos de ASP.NET Core para el cifrado de cookies y la autenticación
- **Importancia**: ⭐⭐ Importante - Requerido para las sesiones de usuario existentes y la autenticación segura

### 2a. Archivos de Mensajes Originales (solo con `RawMessages.Storage=filesystem`) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **Volumen**: `./raw-messages` (el lado del host de `/app/RawMessages`)
- **Contiene**: El `.eml` original de cada correo electrónico, comprimido con gzip, escrito cuando `RawMessages:Enabled` está configurado y el backend de almacenamiento es `filesystem`
- **Importancia**: ⭐⭐⭐ Crítico **cuando está en uso** - la base de datos solo guarda el checksum, el tamaño y la ruta relativa, por lo que una Restauración sin este directorio deja los originales que no pueden ser servidos
- Realice la Copia de seguridad de este directorio y del volcado de la base de datos **juntos**. Restaurar la base de datos sin los archivos no es un error que el Archivo pueda reparar: las descargas responden `404` y los checksums no pueden volver a derivarse de nada más. Restaurar los archivos sin la base de datos es igualmente inútil, porque nada los referencia.
- Si utiliza `RawMessages.Storage=postgres` (el predeterminado), este directorio no existe y los originales están dentro de `./postgres-data` como antes.
- Consulte [Almacenamiento de Mensajes Originales](RawMessages.md) para conocer la función en sí y cómo cambiar de backend.

### 3. Configuración de Docker Compose {#3-docker-compose-configuration}
- **Archivo**: `docker-compose.yml`
- **Contiene**: Configuración del contenedor, variables de entorno y ajustes
- **Importancia**: ⭐⭐ Importante - Contiene la configuración de su aplicación

## 🔄 Procedimientos de Copia de Seguridad {#backup-procedures}

### Método 1: Copia de Seguridad Completa (Recomendado) {#method-1-complete-backup-recommended}

Este método realiza la Copia de seguridad de todos los datos mientras los contenedores están detenidos para garantizar la consistencia de los datos.

```bash
# 1. Stop the containers
docker compose down

# 2. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 3. Copy all data volumes
cp -r postgres-data "$BACKUP_DIR/"
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Copy docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

# 7. Start the containers again
docker compose up -d

echo "Backup completed: $BACKUP_DIR.tar.gz"
```

### Método 2: Copia de Seguridad en Caliente (Tiempo de Inactividad Mínimo) {#method-2-hot-backup-minimal-downtime}

Este método crea una Copia de seguridad completa mientras el sistema está en ejecución con un tiempo de inactividad mínimo.

**Nota:** Reemplace `mailuser` y `MailArchiver` con sus credenciales de base de datos reales de `docker-compose.yml` (verifique las variables de entorno `POSTGRES_USER` y `POSTGRES_DB`).

```bash
# 1. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 2. Backup the database
# Replace 'mailuser' and 'MailArchiver' with your actual POSTGRES_USER and POSTGRES_DB
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > "$BACKUP_DIR/database.sql"

# 3. Backup data protection keys (these change rarely, can be done while running)
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Backup docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

echo "Hot backup completed: $BACKUP_DIR.tar.gz"
```

### Método 3: Script de Copia de Seguridad Automatizada {#method-3-automated-backup-script}

Para Copias de seguridad automatizadas regulares, proporcionamos un script de Copia de seguridad listo para usar. El script está disponible en [backup.sh](attachments/backup.sh).

**Características:**
- ✅ Detección automática de credenciales de la base de datos desde docker-compose.yml
- ✅ Copia de seguridad en caliente (tiempo de inactividad mínimo)
- ✅ Mecanismo de archivo de bloqueo para evitar Copias de seguridad concurrentes
- ✅ Verificación de espacio en disco antes de la Copia de seguridad
- ✅ Limpieza automática de Copias de seguridad antiguas

**Configuración:**

Antes de usar el script, edite la sección de configuración en la parte superior del archivo:

```bash
# Required settings:
MAILARCHIVER_DIR="/path/to/your/mailarchiver"   # Mail Archiver installation directory
BACKUP_BASE_DIR="/path/to/your/backups"         # Where to store backups

# Optional settings (with defaults):
RETENTION_DAYS=30                               # How many days to keep backups
LOG_FILE="/var/log/mailarchiver-backup.log"     # Log file location
MIN_DISK_SPACE_MB=1000                          # Minimum free disk space required
LOCK_FILE="/var/lock/mailarchiver-backup.lock"  # Lock file to prevent concurrent runs
```

**Configuración:**

1. **Copie el script a la ubicación deseada:**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **Edite la configuración:**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # Ajuste los ajustes
   ```

3. **Haga que el script sea ejecutable:**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **Pruebe el script manualmente:**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **Verifique la Copia de seguridad:**
   - Revise el directorio de la Copia de seguridad para ver el archivo `.tar.gz` creado
   - Revise el archivo de registro para cualquier error o advertencia

**Programación de Copias de Seguridad Automatizadas:**

Para programar Copias de seguridad automáticas usando cron:

```bash
# Edit crontab
crontab -e

# Add a line to run backup daily at 2 AM
0 2 * * * /usr/local/bin/mailarchiver-backup.sh

# Or weekly on Sunday at 3 AM
0 3 * * 0 /usr/local/bin/mailarchiver-backup.sh

# Or multiple times (e.g., daily at 2 AM and 2 PM)
0 2,14 * * * /usr/local/bin/mailarchiver-backup.sh
```

## 🔙 Procedimientos de Restauración {#restore-procedures}

Los procedimientos de Restauración están diseñados para coincidir con los métodos de Copia de seguridad. Elija el método de Restauración apropiado basado en qué método de Copia de seguridad utilizó:

- **Método 1 (Copia de Seguridad Completa)**: Utilice el procedimiento "Full Restore from Complete Backup". Este método se utiliza cuando tiene una Copia de seguridad completa que incluye el directorio `postgres-data`.
- **Mét-odo 2 (Copia de Seguridad en Caliente) o Método 3 (Script de Copia de Seguridad Automatizada)**: Utilice el procedimiento "Restore from SQL Dump" (gestiona las claves de protección de datos y la base de datos). Este método se utiliza cuando tiene una Copia de seguridad en caliente que incluye el volcado SQL de la base de datos y las claves de protección de datos.

Para su conveniencia, también hemos proporcionado un script de Restauración automatizado que puede manejar todos los procedimientos de Restauración automáticamente. Puede usar el script en lugar de seguir manualmente los pasos a continuación.

### Script de Restauración Automatizada {#automated-restore-script}

El script de Restauración está disponible en el archivo [restore.sh](attachments/restore.sh). Proporcionará una Restauración guiada y solicitará el método de Copia de seguridad, la ruta al archivo tar de la Copia de seguridad y el directorio de instalación, luego realizará el proceso de Restauración automáticamente.

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

El script lo guiará a través del proceso de Restauración y manejará automáticamente todos los pasos basados en el método de Copia de seguridad que utilizó.

### Full Restore from Complete Backup (Método Manual) {#full-restore-from-complete-backup-manual-method}

Esto restaura todos los datos desde un archivo de Copia de seguridad completo. Este método debe usarse cuando tiene una Copia de seguridad completa que incluye el directorio `postgres-data` (creado por el método uno).

```bash
# 1. Stop the containers
docker compose down

# 2. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 3. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 4. Remove current data (make sure to backup first if needed!)
rm -rf postgres-data
rm -rf data-protection-keys

# 5. Restore data volumes
cp -r "$BACKUP_PATH/postgres-data" ./
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Start the containers
docker compose up -d

echo "Restore completed from: $BACKUP_FILE"
```

### Restore from SQL Dump (Método Manual) {#restore-from-sql-dump-manual-method}

Esto restaura la base de datos y las claves de protección de datos desde una Copia de seguridad en caliente (Método dos y tres).

**Nota:** Los comandos a continuación utilizan credenciales de ejemplo (`mailuser` y `MailArchiver`). Debe reemplazar estos valores con los valores reales de su archivo `docker-compose.yml`. Verifique las variables de entorno `POSTGRES_USER` y `POSTGRES_DB` bajo el servicio `postgres`.

```bash
# 1. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 2. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 3. Stop the containers
docker compose down

# 4. Backup current data protection keys (optional, for safety)
# cp -r data-protection-keys data-protection-keys.backup

# 5. Restore data protection keys
rm -rf data-protection-keys
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Extract database credentials from docker-compose.yml
# Check your docker-compose.yml for the actual values and replace in the commands below:
# - POSTGRES_USER (default: mailuser)
# - POSTGRES_DB (default: MailArchiver)
DB_USER="mailuser"  # Replace with your POSTGRES_USER
DB_NAME="MailArchiver"  # Replace with your POSTGRES_DB

# 8. Start the database container
docker compose up -d postgres

# 9. Wait for database to be ready
echo "Waiting for database to be ready..."
sleep 10

# 10. Verify database is ready (adjust -U and -d parameters if needed)
docker compose exec -T postgres pg_isready -U "$DB_USER" -d "$DB_NAME"

# 11. Drop and recreate the database (WARNING: This deletes all current data!)
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "DROP DATABASE IF EXISTS \"$DB_NAME\";"
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "CREATE DATABASE \"$DB_NAME\";"

# 12. Restore the database from SQL dump
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" < "$BACKUP_PATH/database.sql"

# 13. Start all containers
docker compose up -d

# 14. Clean up extracted backup
rm -rf "$BACKUP_PATH"

echo "Restore completed from: $BACKUP_FILE"
```


## ⚠️ Notas Importantes {#important-notes}

### Ajustes de Conexión a la Base de Datos {#database-connection-settings}
Asegúrese de que sus ajustes de conexión a la base de datos en `docker-compose.yml` coincidan durante la Copia de seguridad y la Restauración:
- `POSTGRES_USER` (predeterminado: mailuser)
- `POSTGRES_PASSWORD` (predeterminado: masterkey)
- `POSTGRES_DB` (predeterminado: MailArchiver)

### Claves de Protección de Datos {#data-protection-keys}
El directorio `data-protection-keys` se utiliza para:
- Autenticación de usuario y gestión de sesiones
- Cifrado de cookies
- Manejo seguro de datos

Sin estas claves, las sesiones de usuario existentes se invalidarán y los usuarios deberán iniciar sesión nuevamente.

### Variables de Entorno {#environment-variables}
Todos los ajustes de la aplicación se almacenan en variables de entorno en `docker-compose.yml`. Asegúrese de:
- Realizar la Copia de seguridad de su archivo `docker-compose.yml`
- Documentar cualquier ajuste personalizado
- Usar la misma configuración durante la Restauración

## 🔒 Mejores Prácticas de Seguridad {#security-best-practices}

1. **Cifrar Copias de Seguridad**: Considere cifrar los archivos de Copia de seguridad, especialmente si contienen datos de correo electrónico sensibles.

2. **Almacenamiento Seguro**: Almacene las Copias de seguridad en un lugar seguro:
   - Use un servidor o sistema de almacenamiento separado
   - Implemente controles de acceso
   - Considere el almacenamiento en la nube o fuera del sitio

3. **Pruebas Regulares**: Pruebe regularmente sus procedimientos de Copia de seguridad y Restauración para asegurar que funcionen correctamente

4. **Monitorear el Tamaño de la Copia de Seguridad**: Realice un seguimiento de los tamaños de las Copias de seguridad para asegurar que tiene suficiente espacio de almacenamiento

5. **Retención de Copia de Seguridad**: Defina e implemente una política de Retención de Copia de seguridad (por ejemplo, mantener Copias de seguridad diarias por 7 días, semanales por 4 semanas, mensuales por 12 meses)

6. **Cambiar Contraseñas Predeterminadas**: Las contraseñas predeterminadas en la configuración son inseguras y deben cambiarse para uso en producción.


## 🖥️ Proxmox Backup {#proxmox-backup}

Al ejecutar el entorno Mail Archiver Docker en un contenedor Proxmox LXC, se recomienda usar el modo "stop" para realizar la Copia de seguridad de todo el contenedor para asegurar la consistencia de los datos.

### Por qué se recomienda el modo Stop {#why-stop-mode-is-recommended}

1. **Consistencia de Datos**: Detener el contenedor asegura que todas las transacciones de la base de datos se completen y los datos se escriban en el disco antes de que se tome la Copia de seguridad.
2. **Prevenir la Corrupción de Datos**: Realizar Copias de seguridad mientras el contenedor está activo puede llevar a la corrupción de datos, especialmente con operaciones de base de datos.
3. **Integridad del Sistema de Archivos**: Detener el contenedor asegura que todas las operaciones del sistema de archivos se completen, evitando escrituras parciales durante la Copia de seguridad.

### Notas de Proxmox Backup {#proxmox-backup-notes}

- Asegúrese siempre de tener suficiente espacio de almacenamiento para las Copias de seguridad.
- Pruebe su procedimiento de Restauración regularmente para asegurar que las Copias de seguridad sean válidas.
- Considere configurar un programa de Copia de seguridad en la interfaz web de Proxmox para Copias de seguridad automatizadas.
- Monitoree los registros de la Copia de seguridad para detectar errores o problemas.
