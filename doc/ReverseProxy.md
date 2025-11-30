# 🌐 Reverse Proxy Configuration Guide

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide provides comprehensive instructions for configuring reverse proxy servers to work properly with the Mail Archiver application, particularly for OIDC authentication scenarios where HTTPS protocol handling is critical.

## 📚 Table of Contents

1. [Overview](#overview)
3. [Required HTTP Headers](#required-http-headers)

## 🌐 Overview

When deploying Mail Archiver behind a reverse proxy, proper configuration of HTTP headers is essential for:
- Correct HTTPS protocol detection
- Proper OIDC redirect URL generation
- Accurate client IP address logging
- Security header management

## 📡 Required HTTP Headers

Mail Archiver requires the following headers to be set by your reverse proxy:

| Header | Value | Purpose |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Indicates the original protocol |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | Original host header |
| `X-Forwarded-For` | Client IP address | Original client IP |

## 📝 Example Configurations

### NGINX

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

> **Note**: Replace `localhost:5000` with the actual address where your Mail Archiver application is running.

### Caddy

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

> **Note**: Replace `localhost:5000` with the actual address where your Mail Archiver application is running.

---

**Note**: This guide is current as of 2025. Reverse proxy software regularly updates their configurations and features, so some settings may differ. Always refer to the latest documentation from your reverse proxy provider for the most up-to-date information.
