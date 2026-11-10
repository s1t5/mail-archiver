# 🌐 Reverse Proxy Configuration Guide {#reverse-proxy-configuration-guide}

[← Terug naar de Documentatie-index](index.md)

## 📋 Overzicht {#overview}

Deze gids biedt uitgebreide instructies voor het configureren van reverse proxy-servers om correct te werken met de Mail Archiver-applicatie, met name voor OIDC-authenticatiescenario's waarbij de afhandeling van het HTTPS-protocol cruciaal is.

## 📚 Inhoudsopgave {#table-of-contents}

1. [Overzicht](#overview)
3. [Vereiste HTTP-headers](#required-http-headers)

## 🌐 Overzicht {#overview}

Bij het implementeren van Mail Archiver achter een reverse proxy is de juiste configuratie van HTTP-headers essentieel voor:
- Correcte detectie van het HTTPS-protocol
- Correcte generatie van de OIDC-omleidings-URL
- Nauwkeurige logging van het client-IP-adres
- Beheer van security-headers

## 📡 Vereiste HTTP-headers {#required-http-headers}

Mail Archiver vereist dat de volgende headers worden ingesteld door uw reverse proxy:

| Header | Waarde | Doel |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Geeft het oorspronkelijke protocol aan |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | Oorspronkelijke host-header |
| `X-Forwarded-For` | Client IP-adres | Oorspronkelijk client-IP |

## 📝 Voorbeeldconfiguraties {#example-configurations}

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

> **Opmerking**: Vervang `localhost:5000` door het werkelijke adres waar uw Mail Archiver-applicatie draait.

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

> **Opmerking**: Vervang `localhost:5000` door het werkelijke adres waar uw Mail Archiver-applicatie draait.

---

**Opmerking**: Deze gids is actueel per 2025. Reverse proxy-software werkt hun configuraties en functies regelmatig bij, dus sommige instellingen kunnen afwijken. Raadpleeg altijd de nieuwste documentatie van uw reverse proxy-provider voor de meest actuele informatie.
