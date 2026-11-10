# 📥 Guía de Migración de Buzón {#mailbox-migration-guide}

[← Volver al Índice de Documentación](index.md)

Esta guía explica cómo migrar un Buzón de un proveedor de correo electrónico a otro utilizando la aplicación Mail Archiver.

## 📋 Resumen {#overview}

La aplicación Mail Archiver proporciona la funcionalidad para migrar correos electrónicos desde un Buzón de origen a un Buzón de destino. Esto es particularmente útil al cambiar de proveedor de correo electrónico mientras se preserva su historial de correo. La estructura de Carpeta del Buzón de origen también se preserva durante la migración.

> **¿Migrar solo el correo reciente?** El flujo descrito a continuación copia el Buzón archivado *completo*, y ejecutarlo dos veces añade todo una segunda vez. Si el nuevo Buzón debe contener solo el correo más reciente que una fecha de corte mientras que el historial completo permanece en el Archivo, utilice el [date-windowed offload](Offload.md) en su lugar. Este acepta un intervalo de fechas, puede excluir y renombrar Carpetas, ofrece una ejecución de prueba (dry run) y es seguro de repetir porque omite lo que el destino ya contiene. También se puede ejecutar desde la línea de comandos, lo que permite automatizar un conjunto de Buzones.

## 🚀 Proceso de Migración {#migration-process}

Siga estos pasos para migrar su Buzón:

1. **Añadir la Cuenta de origen**
   - Navegue a la sección "Email Accounts"
   - Haga clic en "Add Account"
   - Introduzca los detalles y credenciales de su Cuenta de origen
   - Guarde la Cuenta

2. **Sincronizar la Cuenta de origen**
   - Permita que la aplicación realice la Sincronización de todos los correos electrónicos de la Cuenta de origen
   - Espere a que el proceso de Sincronización se complete

3. **Añadir la Cuenta de destino**
   - En la sección "Email Accounts", haga clic en "Add Account"
   - Introduzca los detalles y credenciales de su Cuenta de destino
   - Guarde la Cuenta

4. **Sincronizar la Cuenta de destino**
   - Si la Cuenta de destino es nueva, realice la Sincronización para asegurar que esté configurada correctamente
   - Espere a que el proceso de Sincronización se complete

5. **Iniciar la migración**
   - En la sección "Email Accounts", identifique la Cuenta de origen
   - Vaya a los detalles de la Cuenta
   - Seleccione la opción "Copy All Emails to Another Mailbox"
   - Elija la Cuenta de destino y una Carpeta de destino
   - Habilite la opción **"Preserve Folder Structure"** – la Carpeta de destino seleccionada servirá entonces como la raíz bajo la cual se recreará recursivamente toda la estructura de Carpetas de origen
   - Si esta opción no está habilitada, todos los correos electrónicos se colocarán directamente en la Carpeta de destino seleccionada (plano, sin subcarpetas)
   - Inicie el proceso de migración

6. **Monitorear la migración**
   - Si hay un gran número de correos electrónicos, la migración se llevará a cabo como una tarea en segundo plano
   - El estado y el progreso de la migración se pueden ver en la pestaña "Jobs"
   - Espere a que la migración se complete con éxito

7. **Desactivar la Cuenta de origen**
   - Tras la transferencia exitosa, vuelva a la Cuenta de origen
   - Establezca la Cuenta de origen como "Disabled" en la configuración de la Cuenta
   - Esto asegura que ya no se archive en el futuro

## ⚠️ Notas Importantes {#important-notes}

- Este flujo no es repetible: ejecutarlo de nuevo añade cada mensaje una segunda vez. El [date-windowed offload](Offload.md) es la alternativa repetible.
- Asegúrese de que la Cuenta de origen tenga los Permisos suficientes para leer todos los correos electrónicos
- Asegúrese de que la Cuenta de destino tenga suficiente espacio de almacenamiento para los correos electrónicos migrados
- El proceso de migración puede tardar algún tiempo dependiendo del número de correos electrónicos y la velocidad de la conexión
- Se recomienda realizar la migración durante horas de baja actividad para minimizar el impacto en el servicio de correo electrónico
- Compruebe regularmente el estado de la migración en la pestaña "Jobs" para asegurar que el proceso progresa
- Después de la migración, verifique que todos los correos electrónicos se hayan transferido correctamente a la Cuenta de destino

## ✅ Mejores Prácticas {#best-practices}

- Siempre pruebe el proceso de migración con un pequeño subconjunto de correos electrónicos antes de migrar el Buzón completo
- Mantenga una Copia de seguridad de los correos electrónicos importantes antes de comenzar la migración
- Programe la migración durante un momento en que el uso del correo electrónico sea mínimo
- Monitoree de cerca el progreso de la migración y esté preparado para abordar cualquier problema que pueda surgir
- Una vez completada la migración, verifique que todos los correos electrónicos se hayan transferido correctamente y sean accesibles
