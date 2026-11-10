# 🌐 Guide de configuration du Reverse Proxy {#reverse-proxy-configuration-guide}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide fournit des instructions complètes pour configurer les serveurs de reverse proxy afin qu'ils fonctionnent correctement avec l'application Mail Archiver, particulièrement pour les scénarios d'authentification OIDC où la gestion du protocole HTTPS est critique.

## 📚 Table des matières {#table-of-contents}

1. [Aperçu](#overview)
3. [En-têtes HTTP requis](#required-http-headers)

## 🌐 Aperçu {#overview}

Lors du déploiement de Mail Archiver derrière un reverse proxy, une configuration correcte des en-têtes HTTP est essentielle pour :
- La détection correcte du protocole HTTPS
- La génération correcte de l'URL de redirection OIDC
- L'enregistrement précis de l'adresse IP du client
- La gestion des en-têtes de sécurité

## 📡 En-têtes HTTP requis {#required-http-headers}

Mail Archiver nécessite que les en-têtes suivants soient définis par votre reverse proxy :

| Header | Value | Purpose |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Indique le protocole d'origine |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | En-tête host d'origine |
| `X-Forwarded-For` | Client IP address | IP client d'origine |

## 📝 Exemples de configurations {#example-configurations}

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

> **Note** : Remplacez `localhost:5000` par l'adresse réelle où votre application Mail Archiver est en cours d'exécution.

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

> **Note** : Remplacez `localhost:5000` par l'adresse réelle où votre application Mail Archiver est en cours d'exécution.

---

**Note** : Ce guide est à jour en 2025. Les logiciels de reverse proxy mettent régulièrement à jour leurs configurations et leurs fonctionnalités, certains paramètres peuvent donc différer. Référez-vous toujours à la documentation la plus récente de votre fournisseur de reverse proxy pour obtenir les informations les plus à jour.
