# 🌐 Guia de Configuração de Reverse Proxy {#reverse-proxy-configuration-guide}

[← Voltar para o Índice de Documentação](index.md)

## 📋 Visão Geral {#overview}

Este guia fornece instruções abrangentes para configurar servidores de reverse proxy para funcionar corretamente com o aplicativo Mail Archiver, particularmente para cenários de autenticação OIDC onde o tratamento do protocolo HTTPS é crítico.

## 📚 Sumário {#table-of-contents}

1. [Visão Geral](#overview)
3. [Cabeçalhos HTTP Necessários](#required-http-headers)

## 🌐 Visão Geral {#overview}

Ao implantar o Mail Archiver atrás de um reverse proxy, a configuração adequada dos cabeçalhos HTTP é essencial para:
- Detecção correta do protocolo HTTPS
- Geração adequada da URL de redirecionamento OIDC
- Registro preciso do endereço IP do cliente
- Gerenciamento de cabeçalhos de segurança

## 📡 Cabeçalhos HTTP Necessários {#required-http-headers}

O Mail Archiver requer que os seguintes cabeçalhos sejam definidos pelo seu reverse proxy:

| Cabeçalho | Valor | Propósito |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Indica o protocolo original |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | Cabeçalho host original |
| `X-Forwarded-For` | Client IP address | IP do cliente original |

## 📝 Exemplos de Configurações {#example-configurations}

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

> **Nota**: Substitua `localhost:5000` pelo endereço real onde seu aplicativo Mail Archiver está sendo executado.

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

> **Nota**: Substitua `localhost:5000` pelo endereço real onde seu aplicativo Mail Archiver está sendo executado.

---

**Nota**: Este guia está atualizado até 2025. Softwares de reverse proxy atualizam regularmente suas configurações e recursos, portanto, algumas definições podem diferir. Sempre consulte a documentação mais recente do seu provedor de reverse proxy para obter as informações mais atualizadas.
