# 🌐 Reverse Proxy Configuration Guide {#reverse-proxy-configuration-guide}

[← Documentation Index に戻る](index.md)

## 📋 概要 {#overview}

このガイドでは、Mail Archiver アプリケーションと適切に動作させるためのリバースプロキシサーバーの設定に関する包括的な手順を提供します。特に、HTTPS プロトコルの処理が重要となる OIDC 認証シナリオに重点を置いています。

## 📚 目次 {#table-of-contents}

1. [概要](#overview)
3. [必要な HTTP ヘッダー](#required-http-headers)

## 🌐 概要 {#overview}

Mail Archiver をリバースプロキシの背後にデプロイする場合、以下の目的のために HTTP ヘッダーを適切に設定することが不可欠です：
- 正しい HTTPS プロトコルの検出
- 適切な OIDC リダイレクト URL の生成
- 正確なクライアント IP アドレスのログ記録
- セキュリティヘッダーの管理

## 📡 必要な HTTP ヘッダー {#required-http-headers}

Mail Archiver は、リバースプロキシによって以下のヘッダーが設定されることを必要とします：

| ヘッダー | 値 | 目的 |
|--------|-------|---------|
| `X-Forwarded-Proto` | `https` | オリジナルのプロトコルを示す |
| `X-Forwarded-Host` | `mailarchiver.domain.com` | オリジナルの host ヘッダー |
| `X-Forwarded-For` | クライアント IP アドレス | オリジナルのクライアント IP |

## 📝 設定例 {#example-configurations}

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

> **注意**: `localhost:5000` を Mail Archiver アプリケーションが実行されている実際ののアドレスに置き換えてください。

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

> **注意**: `localhost:5000` を Mail Archiver アプリケーションが実行されている実際ののアドレスに置き換えてください。

---

**注意**: このガイドは 2025 年時点のものです。リバースプロキシ ソフトウェアは定期的に設定や機能を更新するため、一部の設定が異なる場合があります。常に最新の情報については、ご利用のリバースプロキシ プロバイダーの最新のドキュメントを参照してください。
