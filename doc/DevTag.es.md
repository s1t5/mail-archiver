# 🧪 Uso de versiones de desarrollo (Dev Tag) {#using-development-versions-dev-tag}

[← Volver al índice de la documentación](index.md)

## 📋 Resumen {#overview}

Esta guía explica cómo cambiar de la versión estable `latest` a la versión de desarrollo `dev` de Mail Archiver para probar correcciones de errores y nuevas funciones. Esto es particularmente útil para los usuarios que desean probar las correcciones antes de que se lancen oficialmente.

## ⚠️ Consideraciones importantes {#important-considerations}

Antes de cambiar a la versión de desarrollo, tenga en cuenta lo siguiente:

- **Las versiones de desarrollo pueden contener errores o funciones inestables** que no están presentes en la versión estable
- **Realice siempre una Copia de seguridad de sus datos** antes de cambiar a una versión de desarrollo
- **Utilice las versiones de desarrollo únicamente para pruebas** y no en entornos de producción
- **Informe de cualquier problema** que encuentre para ayudar a mejorar el software

## 🛑 Detener el contenedor (Recomendado) {#stopping-the-container-recommended}

Antes de cambiar a la versión de desarrollo, se recomienda detener el contenedor:

```bash
docker compose down
```

## 🔄 Cambio a la etiqueta Dev {#switching-to-the-dev-tag}

1. Abra su archivo `docker-compose.yml` en un editor de texto

2. Localice la sección del servicio `mailarchive-app` y cambie la etiqueta de la imagen de `latest` a `dev`:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. Guarde los cambios en su archivo `docker-compose.yml`

## 📥 Descarga de la imagen de desarrollo {#pulling-the-development-image}

Después de actualizar el archivo docker-compose, debe descargar la imagen de desarrollo:

```bash
docker compose pull
```

Este comando descargará la última versión de desarrollo de la imagen de Mail Archiver.

## ▶️ Inicio del contenedor con la nueva versión {#starting-the-container-with-the-new-version}

Después de descargar la nueva imagen, inicie el contenedor para utilizar la versión de desarrollo:

```bash
docker compose up -d
```

## 🔁 Regreso a la versión estable {#switching-back-to-stable-release}

Si desea volver a la versión estable, siga estos pasos:

1. Detenga el contenedor:
```bash
docker compose down
```

2. Edite `docker-compose.yml` y cambie la imagen de nuevo a:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. Descargue la última imagen:
```bash
docker compose pull
```

4. Inicie el contenedor:
```bash
docker compose up -d
```

## 📝 Notas adicionales {#additional-notes}

- Las versiones de desarrollo se actualizan con más frecuencia que las versiones estables
- Si encuentra problemas, revise los registros en busca de mensajes de error:
  ```bash
  docker compose logs mailarchive-app
  ```
- Puede comprobar qué versión se está ejecutando actualmente consultando la información de la imagen del contenedor:
  ```bash
  docker compose images
  ```
