# 🌐 Guía de Configuración de Reverse Proxy {#reverse-proxy-configuration-guide}

[← Volver al Índice de Documentación](index.md)

## 📋 Resumen {#overview}

Esta guía proporciona instrucciones exhaustivas para configurar servidores reverse proxy para que funcionen correctamente con la aplicación Mail Archiver, particularmente para escenarios de autenticación OIDC donde el manejo del protocolo HTTPS es crítico.

## 📚 Tabla de Contenidos {#table-of-contents}

1. [Resumen](#overview)
3. [Encabezados HTTP Requeridos](#required-http-headers)

## 🌐 Resumen {#overview}

Al desplegar Mail Archiver detrás de un reverse proxy, la configuración adecuada de los encabezados HTTP es esencial para:
- Detección correcta del protocolo HTTPS
- Generación adecuada de la URL de redirección OIDC
- Registro preciso de la dirección IP del cliente
- Gestión de encabezados de seguridad

## 📡 Encabezados HTTP Requeridos {#required-http-headers}

Mail Archiver requiere que su reverse proxy establezca los siguientes encabezados:

| Header | Value | Purpose |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Indica el protocolo original |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | Encabezado host original |
| `X-Forwarded-For` | Client IP address | IP del cliente original |

## 📝 Configuraciones de Ejemplo {#example-configurations}

### NGINX {#nginx}

```nginx
server {
    listen 443 ssl http2;
    server_name mailarchiver.domain.com;

    # SSL configuration
    ssl_certificate /path/to/certificate.crt;
    ssl_certificate_key /path/to/private.key;

    location / {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
    }
}
```

> **Nota**: Reemplace `localhost:5000` con la dirección real donde se está ejecutando su aplicación Mail Archiver.

### Caddy {#caddy}

```caddy
mailarchiver.domain.com {
    reverse_proxy localhost:5000 {
        header_up Host {http.reverse_proxy.upstream.hostport}
        header_up X-Real-IP {remote_host}
        header_up X-Forwarded-For {remote_host}
        header_up X-Forwarded-Proto {scheme}
        header_up X-Forwarded-Host {host}
    }
}
```

> **Nota**: Reemplace `localhost:5000` con la dirección real donde se está ejecutando su aplicación Mail Archiver.

---

**Nota**: Esta guía está actualizada a 2025. El software de reverse proxy actualiza regularmente sus configuraciones y características, por lo que algunos ajustes pueden variar. Consulte siempre la documentación más reciente de su proveedor de reverse proxy para obtener la información más actualizada.
