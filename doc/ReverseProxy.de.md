# 🌐 Reverse Proxy Konfigurationsleitfaden {#reverse-proxy-configuration-guide}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden bietet umfassende Anweisungen zur Konfiguration von Reverse-Proxy-Servern, damit diese ordnungsgemäß mit der Mail Archiver Anwendung zusammenarbeiten, insbesondere für OIDC-Authentifizierungsszenarien, bei denen die Handhabung des HTTPS-Protokolls entscheidend ist.

## 📚 Inhaltsverzeichnis {#table-of-contents}

1. [Übersicht](#overview)
3. [Erforderliche HTTP-Header](#required-http-headers)

## 🌐 Übersicht {#overview}

Beim Bereitstellen von Mail Archiver hinter einem Reverse Proxy ist die korrekte Konfiguration der HTTP-Header unerlässlich für:
- Korrekte HTTPS-Protokollerkennung
- Korrekte Generierung der OIDC-Redirect-URL
- Genaue Protokollierung der Client-IP-Adresse
- Verwaltung von Security-Headern

## 📡 Erforderliche HTTP-Header {#required-http-headers}

Mail Archiver erfordert, dass die folgenden Header von Ihrem Reverse Proxy gesetzt werden:

| Header | Wert | Zweck |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Gibt das ursprüngliche Protokoll an |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | Ursprünglicher Host-Header |
| `X-Forwarded-For` | Client-IP-Adresse | Ursprüngliche Client-IP |

## 📝 Beispielkonfigurationen {#example-configurations}

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

> **Hinweis**: Ersetzen Sie `localhost:5000` durch die tatsächliche Adresse, unter der Ihre Mail Archiver Anwendung läuft.

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

> **Hinweis**: Ersetzen Sie `localhost:5000` durch die tatsächliche Adresse, unter der Ihre Mail Archiver Anwendung läuft.

---

**Hinweis**: Dieser Leitfaden ist aktuell (Stand 2025). Reverse-Proxy-Software aktualisiert ihre Konfigurationen und Funktionen regelmäßig, daher können einige Einstellungen abweichen. Beziehen Sie sich immer auf die neueste Dokumentation Ihres Reverse-Proxy-Anbieters, um die aktuellsten Informationen zu erhalten.
