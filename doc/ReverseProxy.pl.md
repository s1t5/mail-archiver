# 🌐 Przewodnik konfiguracji Reverse Proxy {#reverse-proxy-configuration-guide}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik zawiera szczegółowe instrukcje dotyczące konfiguracji serwerów reverse proxy w celu poprawnej współpracy z aplikacją Mail Archiver, w szczególności dla scenariuszy uwierzytelniania OIDC, gdzie obsługa protokołu HTTPS ma kluczowe znaczenie.

## 📚 Spis treści {#table-of-contents}

1. [Przegląd](#overview)
3. [Wymagane nagłówki HTTP](#required-http-headers)

## 🌐 Przegląd {#overview}

Podczas wdrażania Mail Archiver za reverse proxy, właściwa konfiguracja nagłówków HTTP jest niezbędna do:
- Prawidłowego wykrywania protokołu HTTPS
- Prawidłowego generowania adresu URL przekierowania OIDC
- Dokładnego logowania adresu IP klienta
- Zarządzania nagłówkami bezpieczeństwa

## 📡 Wymagane nagłówki HTTP {#required-http-headers}

Mail Archiver wymaga, aby Twój reverse proxy ustawiał następujące nagłówki:

| Nagłówek | Wartość | Cel |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Wskazuje oryginalny protokół |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | Oryginalny nagłówek host |
| `X-Forwarded-For` | Adres IP klienta | Oryginalny adres IP klienta |

## 📝 Przykładowe konfiguracje {#example-configurations}

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

> **Uwaga**: Zastąp `localhost:5000` rzeczywistym adresem, pod którym działa Twoja aplikacja Mail Archiver.

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

> **Uwaga**: Zastąp `localhost:5000` rzeczywistym adresem, pod którym działa Twoja aplikacja Mail Archiver.

---

**Uwaga**: Niniejszy przewodnik jest aktualny na rok 2025. Oprogramowanie reverse proxy regularnie aktualizuje swoje konfiguracje i funkcje, więc niektóre ustawienia mogą się różnić. Zawsze sprawdzaj najnowszą dokumentację dostawcy Twojego reverse proxy, aby uzyskać najbardziej aktualne informacje.
