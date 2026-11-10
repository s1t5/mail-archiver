# 🌐 Guida alla configurazione del Reverse Proxy {#reverse-proxy-configuration-guide}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida fornisce istruzioni complete per configurare i server reverse proxy affinché funzionino correttamente con l'applicazione Mail Archiver, in particolare per gli scenari di autenticazione OIDC in cui la gestione del protocollo HTTPS è critica.

## 📚 Indice {#table-of-contents}

1. [Panoramica](#overview)
3. [Header HTTP richiesti](#required-http-headers)

## 🌐 Panoramica {#overview}

Quando si distribuisce Mail Archiver dietro un reverse proxy, una corretta configurazione degli header HTTP è essenziale per:
- Il corretto rilevamento del protocollo HTTPS
- La corretta generazione dell'URL di reindirizzamento OIDC
- La registrazione accurata dell'indirizzo IP del client
- La gestione degli header di sicurezza

## 📡 Header HTTP richiesti {#required-http-headers}

Mail Archiver richiede che il reverse proxy imposti i seguenti header:

| Header | Valore | Scopo |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Indica il protocollo originale |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | Header host originale |
| `X-Forwarded-For` | Indirizzo IP del client | IP del client originale |

## 📝 Esempi di configurazione {#example-configurations}

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

> **Nota**: Sostituire `localhost:5000` con l'indirizzo effettivo in cui è in esecuzione l'applicazione Mail Archiver.

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

> **Nota**: Sostituire `localhost:5000` con l'indirizzo effettivo in cui è in esecuzione l'applicazione Mail Archiver.

---

**Nota**: Questa guida è aggiornata al 2025. I software di reverse proxy aggiornano regolarmente le loro configurazioni e funzionalità, pertanto alcune impostazioni potrebbero differire. Fare sempre riferimento alla documentazione più recente del fornitore del reverse proxy per le informazioni più aggiornate.
