# 🔧 Guía de Mantenimiento de la Base de Datos {#database-maintenance-guide}

[← Volver al Índice de Documentación](index.md)

## 📋 Resumen {#overview}

Esta guía explica cómo configurar y utilizar la función de mantenimiento automático de la base de datos en Mail Archiver. La aplicación incluye un servicio en segundo plano integrado que realiza automáticamente operaciones de mantenimiento de la base de datos PostgreSQL para mantener un rendimiento óptimo y evitar el crecimiento excesivo de la base de datos.

## ⚠️ Aviso Importante {#important-notice}

**Las bases de datos PostgreSQL no se reducen automáticamente cuando se eliminan datos.** Cuando se eliminan correos electrónicos o Buzones, la base de datos marca el espacio como disponible para su reutilización, pero el tamaño real del archivo de la base de datos permanece igual. Este fenómeno se denomina "database bloat".

Mail Archiver incluye un servicio de mantenimiento automático que ejecuta operaciones VACUUM ANALYZE diariamente para:
- Reclamar almacenamiento para su reutilización dentro de la base de datos
- Actualizar las estadísticas de las consultas para un rendimiento óptimo
- Prevenir la degradación del rendimiento con el tiempo
- Mantener la salud de la base de datos

## 🚀 Mantenimiento Automático (Recomendado) {#automatic-maintenance-recommended}

### Configuración {#configuration}

Habilite el mantenimiento automático de la base de datos en su `appsettings.json` o a través de variables de entorno en `docker-compose.yml`:

#### Opción 1: Via appsettings.json {#option-1-via-appsettingsjson}

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### Opción 2: Via docker-compose.yml (Recomendado) {#option-2-via-docker-composeyml-recommended}

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Enable automatic database maintenance
      - DatabaseMaintenance__Enabled=true
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30
```

### Opciones de Configuración {#configuration-options}

| Parámetro | Tipo | Predeterminado | Descripción |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Habilitar o deshabilitar el mantenimiento automático |
| `DailyExecutionTime` | string | `"02:00"` | Hora del día para ejecutar el mantenimiento (formato HH:mm, 24 horas) |
| `TimeoutMinutes` | integer | `30` | Tiempo máximo permitido para las operaciones de mantenimiento en minutos |

### Cómo Funciona {#how-it-works}

Cuando está habilitado, el servicio de mantenimiento:

1. **Se inicia automáticamente** con la aplicación
2. **Espera** hasta la hora de ejecución configurada
3. **Ejecuta VACUUM ANALYZE** en toda la base de datos
4. **Registra la operación** en el Registro de auditoría de la aplicación con su duración
5. **Reprograma** para el día siguiente

La operación de mantenimiento:
- ✅ Se ejecuta sin tiempo de inactividad
- ✅ No interfiere con las operaciones normales de la aplicación
- ✅ Proporciona registro y monitoreo completo
- ✅ Reclama espacio para su reutilización dentro de la base de datos
- ✅ Actualiza las estadísticas de las consultas para un mejor rendimiento

### Elegir la Hora de Ejecución {#choosing-the-execution-time}

Seleccione una hora en la que su sistema tenga la menor actividad:
- **Recomendado**: Entre las 02:00 y las 04:00 (horas de la noche)
- **Considere**: Su configuración de zona horaria
- **Evite**: Horas de mayor uso o ventanas de Copia de seguridad

Ejemplos de configuración:

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### Monitoreo del Mantenimiento Automático {#monitoring-automatic-maintenance}

#### 1. Logs de la Aplicación {#1-application-logs}

Revise los logs de la aplicación para ver las actividades de mantenimiento:

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. Registro de auditoría (UI) {#2-access-log-ui}

Todas las operaciones de mantenimiento se registran en el Registro de auditoría de la aplicación:

1. Navegue a **Logs** → **Access Log** en la interfaz de usuario
2. Filtre por tipo: **Database Maintenance**
3. Vea los tiempos de ejecución, la duración y cualquier error

## 🔧 Mantenimiento Manual (Avanzado) {#manual-maintenance-advanced}

### VACUUM FULL para Recuperación de Espacio en Disco {#vacuum-full-for-disk-space-recovery}

**⚠️ Requiere Tiempo de Inactividad de la Aplicación**

VACUUM FULL compacta la base de datos y devuelve el espacio en disco al sistema operativo, pero requiere bloqueos exclusivos.

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**Cuándo usarlo:**
- Después de eliminar Buzones grandes
- Para reclamar un espacio significativo en disco
- Durante ventanas de mantenimiento programadas

**Importante:**
- Requiere espacio en disco libre igual al tamaño de la tabla
- Puede tardar horas en bases de datos grandes
- Bloquea todas las operaciones de la base de datos
- Debe programarse durante horas de baja actividad

Para procedimientos manuales detallados, consulte la [Documentación de PostgreSQL VACUUM](https://www.postgresql.org/docs/current/sql-vacuum.html).

## 📚 Documentación Relacionada {#related-documentation}

- [Guía de Configuración](Setup.md) - Configuración inicial y variables de entorno
- [Guía de Copia de seguridad y Restauración](BackupRestore.md) - Procedimientos de Copia de seguridad de la base de datos
- [Documentación de PostgreSQL VACUUM](https://www.postgresql.org/docs/current/sql-vacuum.html) - Documentación oficial de PostgreSQL

## 📝 Resumen {#summary}

✅ **Habilite el mantenimiento automático** para una optimización de la base de datos sin intervención manual
✅ **Monitoree a través de los registros de acceso** para verificar que las operaciones sean exitosas
✅ **Use VACUUM FULL manual** solo cuando sea necesaria la recuperación de espacio en disco
✅ **Programe VACUUM FULL** durante ventanas de mantenimiento para eliminaciones masivas
✅ **Mantenga las Copia de seguridad actualizadas** antes de cualquier operación de mantenimiento manual

El servicio de mantenimiento automático gestiona la optimización diaria de la base de datos, eliminando la necesidad de intervención manual en la mayoría de los casos.
