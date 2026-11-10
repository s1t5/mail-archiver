# 🌐 Руководство по настройке Reverse Proxy {#reverse-proxy-configuration-guide}

[← Назад к индексу документации](index.md)

## 📋 Обзор {#overview}

Данное руководство содержит подробные инструкции по настройке серверов обратного прокси (reverse proxy) для корректной работы с приложением Mail Archiver, особенно в сценариях аутентификации OIDC, где критически важна обработка протокола HTTPS.

## 📚 Содержание {#table-of-contents}

1. [Обзор](#overview)
3. [Необходимые HTTP-заголовки](#required-http-headers)

## 🌐 Обзор {#overview}

При развертывании Mail Archiver за обратным прокси правильная настройка HTTP-заголовков необходима для:
- Корректного определения протокола HTTPS
- Правильной генерации URL-адресов перенаправления (redirect URL) OIDC
- Точного логирования IP-адресов клиентов
- Управления заголовками безопасности

## 📡 Необходимые HTTP-заголовки {#required-http-headers}

Mail Archiver требует, чтобы ваш обратный прокси устанавливал следующие заголовки:

| Заголовок | Значение | Назначение |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | Указывает исходный протокол |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | Исходный заголовок host |
| `X-Forwarded-For` | IP-адрес клиента | Исходный IP-адрес клиента |

## 📝 Примеры конфигураций {#example-configurations}

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

> **Примечание**: Замените `localhost:5000` на фактический адрес, по которому работает ваше приложение Mail Archiver.

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

> **Примечание**: Замените `localhost:5000` на фактический адрес, по которому работает ваше приложение Mail Archiver.

---

**Примечание**: Данное руководство актуально на 2025 год. Программное обеспечение для обратного прокси регулярно обновляет свои конфигурации и функции, поэтому некоторые настройки могут отличаться. Всегда обращайтесь к последней документации вашего поставщика обратного прокси для получения самой актуальной информации.
