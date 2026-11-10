# 🌐 Reverse Proxy Konfigurációs Útmutató {#reverse-proxy-configuration-guide}

[← Vissza a Dokumentáció Indexhez](index.md)

## 📋 Áttekintés {#overview}

Ez az útmutató részletes utasításokat nyújt a reverse proxy szerverek konfigurálásához a Mail Archiver alkalmazással való megfelelő együttműködés érdekében, különösen az OIDC hitelesítési szcenáriók esetén, ahol az HTTPS protokoll kezelése kritikus fontosságú.

## 📚 Tartalomjegyzék {#table-of-contents}

1. [Áttekintés](#overview)
3. [Szükséges HTTP fejlécek](#required-http-headers)

## 🌐 Áttekintés {#overview}

Amikor a Mail Archiver alkalmazást egy reverse proxy mögé helyezik, az HTTP fejlécek megfelelő konfigurációja elengedhetetlen a következőkhöz:
- Helyes HTTPS protokoll detekció
- Megfelelő OIDC redirect URL generálás
- Pontos ügyfél IP-cím naplózás
- Biztonsági fejlécek kezelése

## 📡 Szükséges HTTP fejlécek {#required-http-headers}

A Mail Archiver megköveteli, hogy a reverse proxy az alábbi fejléceket beállítsa:

| Fejléc | Érték | Cél |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Jelzi az eredeti protokollt |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | Eredeti host fejléc |
| `X-Forwarded-For` | Client IP address | Eredeti ügyfél IP |

## 📝 Konfigurációs példák {#example-configurations}

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

> **Megjegyzés**: Cserélje ki a `localhost:5000` értéket azon a tényleges címetre, ahol a Mail Archiver alkalmazás fut.

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

> **Megjegyzés**: Cserélje ki a `localhost:5000` értéket azon a tényleges címetre, ahol a Mail Archiver alkalmazás fut.

---

**Megjegyzés**: Ez az útmutató 2025-ös állapotot tükrözi. A reverse proxy szoftverek rendszeresen frissítik konfigurációikat és funkcióikat, így egyes beállítások eltérhetnek. Mindig forduljon a reverse proxy szolgáltatója legfrissebb dokumentációjához a legpontosabb információkért.
