# 🌍 Idioma y localización {#language-and-localization}

[← Volver al índice de la documentación](index.md)

## 📋 Descripción general {#overview}

La interfaz de Mail Archiver está disponible en **14 idiomas**. El idioma es una
**preferencia por navegador** almacenada en una cookie; no está vinculada a una Cuenta de usuario
y nunca afecta al correo archivado en sí: los mensajes siempre se almacenan exactamente
tal como se recibieron.

## 🌐 Idiomas compatibles {#supported-languages}

| Idioma | Código |
|----------|------|
| English (default) | `en` |
| English (GB) | `en-GB` |
| Slovenščina | `sl` |
| Deutsch | `de` |
| Italiano | `it` |
| Français | `fr` |
| Español | `es` |
| Nederlands | `nl` |
| Русский | `ru` |
| Magyar | `hu` |
| Polski | `pl` |
| Português (Brasil) | `pt-BR` |
| 日本語 | `ja` |
| 简体中文 | `zh` |

**El inglés es el idioma predeterminado y de respaldo.** Cualquier cosa que una traducción no cubra
se muestra en inglés, por lo que un idioma parcialmente traducido nunca te dejará con una
etiqueta vacía.

## 🔄 Cambio de idioma {#switching-the-language}

1. Haz clic en tu **nombre de usuario** en la esquina superior derecha para abrir el menú de usuario.
2. En **Language**, elige un idioma de la lista; la página se recargará
   inmediatamente en ese idioma.

La elección se escribe en una cookie que es válida durante **un año** y se aplica
solo a este navegador. Otros usuarios, y el mismo usuario en otro navegador o en
otro dispositivo, mantienen su propia configuración. Debido a que la cookie sobrevive a la sesión,
el idioma sigue seleccionado después de cerrar la sesión y volver a entrar.

## 🧩 Qué afecta — y qué no afecta — la configuración de idioma {#what-the-language-setting-does-and-does-not-affect}

- **Traducido:** todo el texto de la interfaz: menús, botones, etiquetas de formulario, validación y
  mensajes de error, páginas de estado de tareas y las descripciones de acciones del registro de auditoría.
- **No traducido:** el correo archivado. Los cuerpos, asuntos y Adjuntos se almacenan
  y muestran byte por byte tal como se recibieron.
- **Búsqueda:** la sintaxis de la consulta es la misma en todos los idiomas; consulta
  [Mail search](Search.md). Cambiar el idioma no cambia la forma en que el correo se
  almacena o se indexa.
- **Marcas de tiempo:** las fechas se muestran en la zona horaria local del navegador y
  se formatean para la cultura seleccionada. Para fijar la zona horaria de visualización en toda la instancia,
  configura `TimeZone__DisplayTimeZoneId` (identificador IANA, ej.
  `Europe/Berlin`); consulta [Setup and Parameters](Setup.md#-timezone-settings).

## 🛠️ Para colaboradores: añadir o cambiar traducciones {#for-contributors-adding-or-changing-translations}

Las cadenas traducidas residen en `Resources/`:

| Archivo | Propósito |
|------|---------|
| `SharedResource.resx` | Archivo de recursos neutro — **English**, 1148 claves |
| `SharedResource.<culture>.resx` | Un archivo por idioma, ej. `SharedResource.de.resx` |

Reglas que se derivan de cómo .NET resuelve los recursos aquí:

1. **Añade primero las nuevas claves al archivo neutro**, luego a los archivos de idioma. Una clave
   que falte en una traducción recurre al texto en inglés; nada
   se rompe, la página simplemente permanece parcialmente en inglés.
2. **Una clave que solo existe en un archivo de traducción es peso muerto.** Nunca se
   busca, porque la búsqueda comienza en el archivo neutro. (Al momento de escribir,
   `DeleteAccountAllEmailsButton` y `Sign Out` existen solo en algunas traducciones,
   y el archivo polaco también contiene claves de diseñador sobrantes: `Bitmap1`, `Icon1`,
   `Name1`.)
3. **`en-GB` es una anulación parcial, por diseño.** Define solo 245 de las 1148
   claves y hereda el resto del archivo neutro en inglés, razón por la cual
   se lee como inglés excepto donde el vocabulario británico o los formatos de fecha difieren.
   Chino (simplificado), alemán, español, francés, húngaro, italiano,
   japonés, neerlandés, polaco, portugués (Brasil), ruso y esloveno están
   actualmente completos.
4. **Añadir un idioma completo** requiere tres cosas: un nuevo
   `SharedResource.<culture>.resx`, una entrada en la lista de culturas en `Program.cs`
   (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`),
   y un `<option>` en el menú desplegable de idiomas en
   `Views/Shared/_UserInfo.cshtml`. Sin las dos últimas, el archivo nunca se utiliza.

Consulta [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md)
para conocer el proceso de contribución, los estándares de codificación y cómo enviar un pull request.

## 🔗 Documentación relacionada {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — sintaxis de consulta
- [User Management](UserManagement.md) — roles y Permiso
