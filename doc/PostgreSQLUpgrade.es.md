# 🔄 Guía de Actualización de Versión Mayor de PostgreSQL {#postgresql-major-version-upgrade-guide}

[← Volver al Índice de Documentación](index.md)

## 📋 Descripción General {#overview}

Esta guía explica cómo actualizar la base de datos PostgreSQL de una versión mayor a otra en su entorno Mail Archiver Docker Compose. Las actualizaciones de versiones mayores son necesarias para aprovechar las nuevas funciones de PostgreSQL, mejoras de rendimiento y actualizaciones de seguridad.

## ⚠️ Descargo de Responsabilidad Importante {#important-disclaimer}
> **Nota**: Este procedimiento de actualización es un proceso general de migración de base de datos PostgreSQL y no es específico para Mail Archiver. Se proporciona como una recomendación basada en las mejores prácticas de PostgreSQL. Siempre pruebe el procedimiento en un entorno que no sea de producción primero y asegúrese de tener las Copias de seguridad adecuadas antes de proceder.


**Todos los procedimientos descritos en esta guía deben probarse primero en un entorno que no sea de producción.** Las actualizaciones de versiones mayores implican la migración de datos y conllevan riesgos de pérdida o corrupción de datos. Siempre:
- Cree una Copia de seguridad completa antes de iniciar el proceso de actualización
- Pruebe el procedimiento de actualización en un entorno de desarrollo
- Verifique toda la funcionalidad de la aplicación después de la actualización
- Tenga un plan de reversión en caso de problemas
- Programe las actualizaciones durante las ventanas de mantenimiento

**Úselo bajo su propio riesgo.**

## 🎯 Cuándo Actualizar {#when-to-upgrade}

Considere actualizar PostgreSQL cuando:
- Su versión actual se acerca al fin de su vida útil
- Las actualizaciones de seguridad requieren una versión más reciente
- Está planeando una ventana de mantenimiento importante del sistema

## 🛠️ Requisitos Previos {#prerequisites}

- Configuración de Mail Archiver Docker en ejecución (ver [Guía de Configuración](Setup.md))
- Espacio de disco suficiente (al menos 1.5x el tamaño actual de la base de datos)
- Acceso al sistema host de Docker
- Copia de seguridad completa de la base de datos actual (ver [Guía de Copia de seguridad y Restauración](BackupRestore.md))
- Ventana de mantenimiento para el tiempo de inactividad de la aplicación

## 📋 Configuración Actual {#current-configuration}

Su configuración actual de Mail Archiver utiliza:
- **Nombre de la Base de Datos**: MailArchiver
- **Usuario de la Base de Datos**: mailuser
- **Volumen de Datos**: `./postgres-data` mapeado a `/var/lib/postgresql/data`

Si es necesario, esto debe ajustarse en los siguientes comandos y scripts de ejemplo.

## 🔄 Procedimientos de Actualización {#upgrade-procedures}

Este método crea una copia de seguridad lógica de sus datos y la restaura en la nueva versión de PostgreSQL. Es el enfoque más seguro y funciona en cualquier versión de PostgreSQL.

> **Métodos Alternativos**: PostgreSQL también proporciona otros métodos de actualización como `pg_upgrade` para actualizaciones más rápidas. Para obtener más información sobre enfoques de actualización alternativos, consulte la [documentación oficial de actualización de PostgreSQL](https://www.postgresql.org/docs/current/upgrading.html).

#### Paso 1: Preparar el Entorno {#step-1-prepare-the-environment}

```bash
# 1. Stop the application container (keep database running for backup)
docker compose stop mailarchive-app

# 2. Create a backup directory
mkdir -p postgres-backup

# 3. Create a database dump
echo "Export DB content (this can take some time)"
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > postgres-backup/database_dump.sql

# 4. Verify the dump was created successfully
ls -la postgres-backup/database_dump.sql

# 5. Stop the database container
docker compose down
```

#### Paso 2: Actualizar la Configuración de Docker Compose {#step-2-update-docker-compose-configuration}

Edite su archivo `docker-compose.yml` para usar la nueva versión de PostgreSQL:

```yaml
  postgres:
    image: postgres:18  # Changed from old version
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data-new:/var/lib/postgresql  # New data directory
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
```

**Cambios Importantes:**
- `image` actualizado de `postgres:17` a `postgres:18` (o su versión de destino)
- Cambio de mapeo de volumen de `./postgres-data:/var/lib/postgresql/data` a `./postgres-data-new:/var/lib/postgresql` (el cambio de la ruta interna es requerido por la versión 18 y posteriores de postgres)

#### Paso 3: Iniciar Nueva Instancia de PostgreSQL {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### Paso 4: Restaurar Datos a la Nueva Base de Datos {#step-4-restore-data-to-new-database}

Restaure el volcado de la base de datos a la nueva instancia de PostgreSQL:

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
Esto puede tomar algún tiempo dependiendo del tamaño de la base de datos
Esté atento a cualquier mensaje de error durante la Restauración

#### Paso 5: Probar y Completar la Migración {#step-5-test-and-complete-migration}
Inicie la aplicación para probar la conectividad:

```bash
docker compose up -d
```

Pruebe la funcionalidad de la aplicación:
- Inicie sesión en la interfaz web
- Verifique que la búsqueda de correos electrónicos funcione
- Compruebe que los datos existentes sean accesibles
- Pruebe la funcionalidad de Copia de seguridad/Restauración

Si todo funciona correctamente, limpie los datos antiguos:

Detenga los contenedores
```bash
docker compose down
```
Elimine el directorio de datos antiguos (solo después de confirmar que la nueva configuración funciona)
```bash
rm -rf postgres-data
```
Elimine el directorio de Copia de seguridad
```bash
rm -rf postgres-backup
```

Cambie el nombre del nuevo directorio de datos al nombre estándar
```bash
mv postgres-data-new postgres-data
```

Actualice docker-compose.yml para usar el nombre de volumen estándar:
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Inicie el sistema completo
```bash
docker compose up -d
```

## ⚠️ Consideraciones Importantes {#important-considerations}

### Planificación del Tiempo de Inactividad {#downtime-planning}

- Dependiendo de su sistema y del tamaño de la base de datos, este procedimiento puede tardar mucho tiempo.
- Programe durante las ventanas de mantenimiento

### Requisitos de Espacio en Disco {#disk-space-requirements}

Asegúrese de tener espacio en disco adecuado (aproximadamente el doble del tamaño de su base de datos)

Verifique el espacio disponible:
```bash
df -h
du -sh postgres-data/
```

### Plan de Reversión {#rollback-plan}

Siempre esté preparado para una reversión:
1. Mantenga el directorio de datos antiguo hasta que la actualización se verifique
2. Mantenga la antigua Copia de seguridad de `docker-compose.yml`
3. Pruebe el procedimiento de reversión en desarrollo primero

### Verificación Post-Actualización {#post-upgrade-verification}

Después de la actualización, verifique:
- Conectividad de la aplicación con la base de datos
- Funcionalidad de búsqueda y recuperación de correos electrónicos
- Autenticación de usuario y gestión de sesiones
- Funcionalidad de Copia de seguridad y Restauración
- Métricas de rendimiento de la base de datos

## 📚 Documentación Relacionada {#related-documentation}

- [Guía de Copia de seguridad y Restauración](BackupRestore.md) - Esencial para las Copias de seguridad previas a la actualización
- [Guía de Mantenimiento de la Base de Datos](DatabaseMaintenance.md) - Mantenimiento post-actualización
- [Guía de Configuración](Setup.md) - Configuración de Docker Compose
- [Notas de la Versión de PostgreSQL](https://www.postgresql.org/docs/release/) - Documentación oficial de la versión
