# 📧 Gmail Best Practices {#gmail-best-practices}

[← Back to Documentation Index](index.md)

Al utilizar cuentas de Gmail con la aplicación Mail Archiver, existen varias configuraciones específicas y mejores prácticas que deben seguirse para garantizar un rendimiento óptimo y evitar problemas potenciales.

## 🔐 IMAP Configuration with App Passwords {#imap-configuration-with-app-passwords}

Las cuentas de Gmail pueden integrarse con la aplicación Mail Archiver utilizando IMAP con App Passwords:

1. Habilite la autenticación de 2 factores en su cuenta de Gmail
2. Genere una App Password específicamente para la aplicación Mail Archiver
3. Utilice la App Password en lugar de su contraseña habitual de Gmail al configurar la Cuenta en Mail Archiver

## 🏷️ Gmail Label Settings {#gmail-label-settings}

Se recomienda encarecidamente ajustar la configuración de etiquetas de Gmail para evitar que la etiqueta "All Mail" quede expuesta a través de IMAP:

1. Vaya a la interfaz web de Gmail y abra la configuración (icono de engranaje en la parte superior) → Ver toda la configuración → pestaña Labels
2. Busque la etiqueta "All Mail" en la lista
3. Desmarque la opción "Show in IMAP" para "All Mail"

### 📚 Background on Gmail Label Implementation {#background-on-gmail-label-implementation}

El sistema de etiquetas de Gmail funciona de manera diferente al de los proveedores de correo electrónico tradicionales. En Gmail:
- Todos los correos electrónicos existen en un único repositorio "All Mail"
- Las etiquetas actúan como etiquetas en lugar de Carpetas
- La etiqueta "All Mail" contiene cada correo electrónico de su Cuenta
- Cuando un correo electrónico tiene una etiqueta, aparece tanto en la Carpeta original como en la sección etiquetada. Por lo tanto, todos los correos en su bandeja de entrada también aparecen en la etiqueta "All Mail".

### ⚠️ Issues with IMAP Retrieval {#issues-with-imap-retrieval}

Cuando "All Mail" se expone a través de IMAP, puede causar varios problemas:

1. **Duplicate Processing**: El Archivo puede procesar el mismo correo electrónico varias veces, ya que aparece en múltiples carpetas/etiquetas
2. **Performance Impact**: La recuperación de "All Mail" puede ser extremadamente lenta para Cuentas grandes con miles de correos electrónicos
4. **Synchronization Issues**: Pueden surgir conflictos al intentar determinar a qué Carpeta pertenece realmente un correo electrónico

Al desactivar el acceso IMAP a "All Mail", se asegura que los correos electrónicos solo se recuperen una vez desde su ubicación principal, mejorando tanto el rendimiento como la precisión.

## 🗑️ Retention Policy and Expunge Settings {#retention-policy-and-expunge-settings}

Como se menciona en el README principal, las cuentas de Gmail requieren atención especial con respecto a las políticas de Retención:

- La función Auto-Expunge de Gmail debe desactivarse en la configuración de Gmail bajo la pestaña "Forwarding and POP/IMAP"
- Este ajuste es crucial para que las políticas de Retención funcionen correctamente con las cuentas de Gmail

Para configurar este ajuste:
1. Vaya a Gmail Settings → Ver toda la configuración → pestaña Forwarding and POP/IMAP
2. En la sección "IMAP Access", seleccione "Auto-Expunge off - Wait for the client to update the server." y "Move the message to the Bin"

## ⚠️ Gmail IMAP Rate Limiting {#gmail-imap-rate-limiting}

Gmail impone un límite de velocidad diario de 2500MB para la recuperación por IMAP. Al archivar grandes volúmenes de correos electrónicos, tenga en cuenta que puede alcanzar este límite, lo que podría pausar temporalmente la recuperación de correos hasta el día siguiente.

### 📊 Rate Limit Handling {#rate-limit-handling}

Mail Archiver proporciona un manejo de límites de velocidad integrado para gestionar los límites de ancho de banda de IMAP de Gmail:

- **Automatic bandwidth tracking**: Monitorea los datos descargados por Cuenta por día
- **Configurable limits**: Establezca `BandwidthTracking__DailyLimitMb=2500` para Gmail
- **Automatic pause**: La Sincronización se pausa cuando se alcanza el límite y se reanuda tras el reinicio
- **Checkpoint system**: El progreso de la Sincronización se guarda y se reanuda desde la última posición

Para una configuración y uso detallados, consulte la **[Rate Limit Handling Guide](RateLimitHandling.md)**.

### 🔧 Gmail Configuration Example {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 Large Mailbox Considerations {#large-mailbox-considerations}

Para Buzones de Gmail grandes (p. ej., 14 GB), la Sincronización inicial tardará varios días:

| Mailbox Size | Days to Complete (at 2500 MB/day) |
|--------------|-----------------------------------|
| 5 GB | ~2 days |
| 10 GB | ~4 days |
| 14 GB | ~6 days |
| 20 GB | ~8 days |

**Tip**: Habilite el seguimiento de ancho de banda **antes** de añadir Cuentas de Gmail grandes para asegurar un manejo adecuado del límite de velocidad desde el principio.

## ✅ Best Practices Summary {#best-practices-summary}

Seguir estas mejores prácticas ayudará a garantizar un funcionamiento fluido de Mail Archiver con sus cuentas de Gmail, manteniendo la integridad de los datos y un rendimiento óptimo.
