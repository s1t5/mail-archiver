# 🌐 Vodnik za konfiguracijo obversega proxyja {#reverse-proxy-configuration-guide}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodnik nudi celovita navodila za konfiguracijo strežnikov reverse proxy, da bi pravilno delovali z aplikacijo Mail Archiver, zlasti za scenarije avtentikacije OIDC, kjer je pravilno ravnanje s protokolom HTTPS ključnega pomena.

## 📚 Kazalo vsebine {#table-of-contents}

1. [Pregled](#overview)
3. [Obvezni HTTP glaviči](#required-http-headers)

## 🌐 Pregled {#overview}

Ob uvajanju Mail Archiver za reverse proxy je pravilna konfiguracija HTTP glavičev bistvena za:
- Pravilno zaznavanje protokola HTTPS
- Pravilno generiranje URL-ja za OIDC preusmeritev
- Natančno beleženje IP naslova odjemilca
- Upravljanje varnostnih glavičev

## 📡 Obvezni HTTP glaviči {#required-http-headers}

Mail Archiver zahteva, da vaš reverse proxy nastavite naslednje glaviče:

| Glavič | Vrednost | Namen |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Navaja izvirni protokol |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | Izvirni host glavič |
| `X-Forwarded-For` | Client IP address | Izvirni IP naslov odjemilca |

## 📝 Primeri konfiguracij {#example-configurations}

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

> **Opomba**: Namesto `localhost:5000` vstavite dejanski naslov, kjer vaša aplikacija Mail Archiver teče.

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

> **Opomba**: Namesto `localhost:5000` vstavite dejanski naslov, kjer vaša aplikacija Mail Archiver teče.

---

**Opomba**: Ta vodnik je aktualen leta 2025. Programska oprema reverse proxy redno posodobi svoje konfiguracije in funkcije, zato se lahko nekateri nastavitve razlikujejo. Za najnovejše informacije vedno preverite najnovejšo dokumentacijo svojega ponudnika reverse proxy.
