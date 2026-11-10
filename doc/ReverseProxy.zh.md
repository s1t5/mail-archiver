# 🌐 Reverse Proxy Configuration Guide {#reverse-proxy-configuration-guide}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

本指南提供了配置反向代理服务器以使其与 Mail Archiver 应用程序正常工作的全面说明，特别是在 HTTPS 协议处理至关重要的 OIDC 身份验证场景中。

## 📚 目录 {#table-of-contents}

1. [概述](#overview)
3. [必需的 HTTP 请求头](#required-http-headers)

## 🌐 概述 {#overview}

在反向代理后部署 Mail Archiver 时，正确配置 HTTP 请求头对于以下方面至关重要：
- 正确的 HTTPS 协议检测
- 正确的 OIDC 重定向 URL 生成
- 准确的客户端 IP 地址日志记录
- 安全请求头管理

## 📡 必需的 HTTP 请求头 {#required-http-headers}

Mail Archiver 要求您的反向代理设置以下请求头：

| Header | Value | Purpose |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | 表示原始协议 |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | 原始 host 请求头 |
| `X-Forwarded-For` | 客户端 IP 地址 | 原始客户端 IP |

## 📝 配置示例 {#example-configurations}

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

> **注意**：将 `localhost:5000` 替换为运行 Mail Archiver 应用程序的实际地址。

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

> **注意**：将 `localhost:5000` 替换为运行 Mail Archiver 应用程序的实际地址。

---

**注意**：本指南截至 2025 年。反向代理软件会定期更新其配置和功能，因此某些设置可能会有所不同。请始终参考您所使用的反向代理提供商的最新文档，以获取最新信息。
