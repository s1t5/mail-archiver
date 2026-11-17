# Read-Only REST API (v1) {#read-only-rest-api-v1}

Mail Archiverは、アーカイブされたメールにプログラムからアクセスするための、オプションの**読み取り専用**REST APIを提供します。これは、Web UIを介さずに、自動化や統合のユースケース（レポート作成、e-discoveryエクスポート、モニタリング）を行うために設計されています。

その目的は、スクリプトやAIエージェントが、**メールボックスの認証情報を公開したり、メールプロバイダーと通信したりすることなく**、アーカイブされたメールを読み取れるようにすることです。スコープが限定されたユーザーごとのAPIキーは、「アーカイブを*読み取る*」ための機能であり、「メールボックス*そのものになる*」ための認証情報ではありません。このキーは所有者のメールボックス権限のみを継承し、書き込みパスは付与されず、基盤となるメールアカウントに影響を与えることなく即座に失効させることができます。

> ⚠️ **APIはデフォルトで無効になっています。** 設定で明示的に有効にする必要があります（[Enabling the API](#enabling-the-api)を参照）。無効な場合、すべての `/api/*` ルートは `404 Not Found` を返します。

このAPIは**設計上、読み取り専用**です。アカウント、フォルダ、メッセージ検索、メッセージ詳細、および添付ファイルのダウンロードを公開します。データを新規作成、変更、または削除することはありません。

## 目次 {#table-of-contents}

- [APIの有効化](#enabling-the-api)
- [認証](#authentication)
  - [API keyの形式](#api-key-format)
  - [キーのライフサイクル](#key-lifecycle)
  - [権限モデル](#permission-model)
  - [セキュリティの根拠](#security-rationale)
- [規約](#conventions)
  - [ベースURLとバージョニング](#base-url-and-versioning)
  - [ページネーション](#pagination)
  - [エラー](#errors)
  - [レート制限](#rate-limiting)
- [エンドポイント](#endpoints)
  - [アカウント一覧の取得](#list-accounts)
  - [フォルダ一覧の取得](#list-folders)
  - [メッセージの検索](#search-messages)
  - [メッセージの取得](#get-message)
  - [添付ファイルのダウンロード](#download-attachment)
  - [メッセージ(.eml)のダウンロード](#download-message-eml)
  - [オリジナルメッセージのダウンロード](#download-original-message)
  - [統計](#stats)
  - [ヘルスチェック](#health)
  - [メトリクス](#metrics)
- [データ型](#data-types)
- [アクセスログ](#access-logging)
- [OpenAPIおよびSwagger UI](#openapi-and-swagger-ui)
- [APIリファレンス](API-Reference.md) — 生成されたエンドポイントおよびデータ型の参照

## APIの有効化 {#enabling-the-api}

APIは、`appsettings.json`の`Api`セクション（または、`Api__Enabled=true`などの同等の環境変数）で設定されます。

```json
"Api": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "AllowMessageDownloads": true,
  "EnableSwaggerUi": true,
  "DefaultPageSize": 20,
  "MaxPageSize": 100,
  "RateLimitPerMinute": 120
}
```


### キーのライフサイクル {#key-lifecycle}

1. **作成 (Create)** — ユーザーが説明的な`Name`と、オプションの有効期限を指定してキーを作成します。レスポンスには、プレーンテキストのキーが一度だけ表示されます。
2. **使用 (Use)** — ベアラートークンとしてキーを送信します。リクエストが受理されるたびに、キーの`LastUsedAt`が更新されます（すべての呼び出しで書き込みが発生するのを避けるため、書き込みは少なくとも約5分に1回に制限されます）。
3. **期限切れ (Expire)** — `ExpiresAt`が設定されており、その日時が過去である場合、キーは拒否されます。
4. **失効 (Revoke)** — 所有ユーザーまたは管理者がキーを失効させます（`RevokedAt`が設定されます）。キーは即座に、かつ永久に拒否されます。

キーが**アクティブ**であるとは、失効しておらず、期限が切れておらず、**かつ**その所有ユーザーが依然としてアクティブであることを指します。これらの理由のいずれかによってキーが非アクティブな場合、`401`で拒否されます。

### 権限モデル {#permission-model}

APIキーは、その所有者であるユーザーと全く同じ権限を継承します。認証されたプリンシパルは、通常のウェブログインと同じクレームを保持するため、既存のすべての認可...

| 設定 | デフォルト | 説明 |
| --- | --- | --- |
| `Enabled` | `false` | マスタースイッチ。`false`の場合、すべての`/api/*`ルートは`404`を返し、OpenAPIドキュメントおよびSwagger UIはマッピングされません。 |
| `AllowAttachmentDownloads` | `true` | `false`の場合、添付ファイルのダウンロードエンドポイントは`403`を返します。添付ファイルに関するメタデータは、引き続きメッセージ詳細エンドポイントから返されます。 |
| `AllowMessageDownloads` | `true` | `false`の場合、完全なメッセージのエンドポイント（`/eml`および`/raw`）は`403`を返します。完全なメッセージには添付ファイルが含まれるため、このスイッチと`AllowAttachmentDownloads`の**両方**が`true`である必要があります。いずれかをオフにすると、両方のエンドポイントが無効になります。 |
| `EnableSwaggerUi` | `true` | `true`の場合（かつ`Enabled`が`true`の場合）、Swagger UIを`/apidocs`に、OpenAPIドキュメントを`/apidocs/spec/v1.json`にマウントします。両方とも認証された**ウェブ（クッキー）セッション**を必要とします。これらは`/api/`の一部ではなく、APIキーではアクセスできません。 |
| `DefaultPageSize` | `20` | リクエストで`pageSize`が省略された場合に使用されるページサイズ。 |
| `MaxPageSize` | `100` | 上限値。これより大きな`pageSize`の値は、この値に制限されます。 |
| `RateLimitPerMinute` | `120` | APIキーごとの1分あたりの固定ウィンドウ・リクエスト予算（[Rate limiting](#rate-limiting)を参照）。 |

## Authentication {#authentication}

APIは、bearer tokenとして送信される**ユーザーごとのAPIキー**によって認証されます。

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1`はAPIキー**のみ**を受け入れます。ブラウザのクッキーは尊重されないため、APIにはCSRFの攻撃対象領域（surface）が存在しません。認証失敗時には、`WWW-Authenticate: Bearer`ヘッダーと[problem+json](#errors)ボディを伴う`401`が返されます。APIがログインページにリダイレクトすることは**決してありません**。

キーの管理は、Web UIの**API Keys**からセルフサービスで行えます（各ユーザーは自身のキーを管理できます。管理者はすべてのユーザーのキーを表示および失効させることができます）。

### API key format {#api-key-format}

キーは、文字列`ma_`に、32バイトのランダムなバイト（256ビットのエントロピー）をエンコードした43文字のURLセーフなbase64文字が続く形式です。

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

ストレージ:

- **`KeyPrefix`** — 最初の約11文字（例: `ma_8Kf3pQ2`）。プレーンテキストで保存されます。UIやログでキーを識別するために使用されます。これは機密情報ではありません。
- **`KeyHash`** — フルキーのSHA-256を16進数エンコードしたもの。**一意のインデックス**の下に保存されます。プレーンテキストのキーが保存されることはありません。

フルキーは、作成時に**一度だけ**ユーザーに表示されます。その後、キーを回収することはできません。紛失した場合は、キーを失効させて新しいキーを作成する必要があります。

### Key lifecycle {#key-lifecycle}

1. **Create** — ユーザーが説明的な`Name`と、オプションの有効期限を指定してキーを作成します。レスポンスには、プレーンテキストのキーが一度だけ表示されます。
2. **Use** — キーをbearer tokenとして送信します。リクエストが受理されるたびに、キーの`LastUsedAt`が更新されます（すべての呼び出しで書き込みが発生するのを避けるため、書き込みは最大で約5分に1回に制限されます）。
3. **Expire** — `ExpiresAt`が設定されており、その日時が過去である場合、キーは拒否されます。
4. **Revoke** — 所有ユーザーまたは管理者がキーを失効させます（`RevokedAt`が設定されます）。失効させると、直ちに、かつ永久に拒否されます。

キーが**active**であるとは、失効しておらず、期限切れではなく、**かつ**その所有ユーザーがまだアクティブであることを指します。これらの理由のいずれかによって非アクティブになったキーは、`401`で拒否されます。

### Permission model {#permission-model}

APIキーは、その所有者であるユーザーと全く同じ権限を継承します。認証されたプリンシパルは、通常のWebログインと同じクレームを保持するため、既存のすべての認可ロジックが変更なしに適用されます。

- **Administrators** (`IsAdmin`) は、すべてのメールアカウントとすべてのメッセージを表示できます。
- **Regular users** は、自身に割り当てられたメールアカウントのみを表示できます (`UserMailAccount`)。それ以外のセットに含まれないアカウントやメッセージは、**存在しないもの**として扱われます。それらへのリクエストは`403`ではなく`404`を返すため、APIは呼び出し元が見ることができないアカウントの存在を漏洩させることはありません。

### Security rationale {#security-rationale}

- **2FA bypass.** APIキーは設計上、二要素認証をバイパスします。これは非対話型トークンに固有の性質です（GitHub/GitLabのパーソナルアクセストークンも同様です）。これは以下の方法で軽減されています：キーは**デフォルトでオフ**であること、失効可能であること、オプションで期限を設定できること、ユーザーの権限に限定されていること、およびすべてのアクセスが[access log](#access-logging)に記録されること。
- **No cookies on `/api`.** `/api`ルートではクッキー認証は受け入れられないため、APIにおけるCSRFの懸念は排除されています。
- **Transport.** 本番環境では、bearer tokenがネットワーク上で露出しないよう、常にHTTPS経由でAPIを提供してください。

## 規約 {#conventions}

### Base URL とバージョニング {#base-url-and-versioning}

すべてのエンドポイントは `/api/v1` の下にあります。バージョンはパスに含まれているため、将来的な破壊的変更を既存のクライアントに影響を与えることなく `/api/v2` としてリリースできます。

特に断りのない限り、レスポンスは `application/json` です（添付ファイルのダウンロードは、その添付ファイル自身の content type を返します）。すべてのタイムスタンプは UTC、ISO 8601 (`2026-06-11T09:30:00Z`) です。

### Pagination {#pagination}

リスト形式のエンドポイントは、pagination envelope を返します：

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` は 1 から始まります。範囲外のページは空の `items` 配列を返します（エラーではありません）。
- `pageSize` のデフォルトは `Api:DefaultPageSize` で、`[1, Api:MaxPageSize]` に制限されます。

### Errors {#errors}

エラーには [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) `application/problem+json` が使用されます：

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Unknown sortBy value 'foo'.",
  "instance": "/api/v1/emails"
}
```

| Status | When |
| --- | --- |
| `400 Bad Request` | 不正なパラメータ（例：解析不能な日付、無効な `sortBy`）。 |
| `401 Unauthorized` | キーの欠落、不正、期限切れ、失効、またはその他の無効な状態。`WWW-Authenticate: Bearer` を含みます。 |
| `403 Forbidden` | `AllowAttachmentDownloads=false` の間に添付ファイルのダウンロードが要求された、または `AllowMessageDownloads=false` の間にメッセージのダウンロードが要求された場合。 |
| `404 Not Found` | リソースが存在しない、**または** 呼び出し元に許可されたアカウントの範囲外である。また、`Api:Enabled=false` の場合、すべての `/api/*` ルートで返されます。 |
| `429 Too Many Requests` | レート制限を超過しました。 |
| `500 Internal Server Error` | 予期しないサーバーエラー (problem+json、HTMLのエラーページは返されません)。 |

### Rate limiting {#rate-limiting}

リクエストは、APIキーのプレフィックスごとに分割された、1分あたり `Api:RateLimitPerMinute` リクエスト（デフォルト `120`）の固定ウィンドウポリシーによって制限されます（キーがない場合はクライアントの IP にフォールバックされます）。予算を超過すると `429` が返されます。

## Endpoints {#endpoints}

> すべての例は、APIが有効化されており、`$KEY`に有効なキーが保持されていることを前提としています。

### List accounts {#list-accounts}

```
GET /api/v1/accounts
```

呼び出し元から参照可能なメールアカウントを返します。認証フィールド（IMAP/OAuth username、password、client secret、…）が**含まれることは決してありません**。

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/accounts
```

```json
[
  {
    "id": 1,
    "name": "Support Mailbox",
    "emailAddress": "support@example.com",
    "provider": "IMAP",
    "isEnabled": true,
    "lastSync": "2026-06-11T08:15:00Z"
  }
]
```

### List folders {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

1つのアカウントのフォルダツリーをネストされた構造として返します。アカウントが存在しない場合、または呼び出し元から参照できない場合は `404` を返します。

```json
[
  {
    "name": "INBOX",
    "fullPath": "INBOX",
    "totalCount": 4213,
    "level": 0,
    "children": [
      { "name": "Work", "fullPath": "INBOX/Work", "totalCount": 512, "level": 1, "children": [] }
    ]
  }
]
```

### Search messages {#search-messages}

```
GET /api/v1/emails
```

Web UIと同じ機能を使用して、アーカイブされたメッセージを検索します。

| クエリパラメータ | 型 | 説明 |
| --- | --- | --- |
| `q` | string | 件名、本文、from、to、cc、bccに対する全文検索クエリ。`subject:invoice`、`from:alice@example.com`などのフィールド構文をサポートしています。 |
| `from` | date | 最も古い送信日（含む）、`YYYY-MM-DD`。 |
| `to` | date | 最新の送信日（含む）、`YYYY-MM-DD`。 |
| `accountId` | int | 1つのアカウントに制限します（呼び出し元から表示可能である必要があります）。 |
| `folder` | string | フルパスによって1つのフォルダに制限します。 |
| `direction` | string | `incoming`または`outgoing`。 |
| `page` | int | 1から始まるページ番号（デフォルトは`1`）。 |
| `pageSize` | int | 1ページあたりの結果数（デフォルトは`Api:DefaultPageSize`、最大値は`Api:MaxPageSize`に制限されます）。 |
| `sortBy` | string | `sentDate`（デフォルト）、`receivedDate`、`subject`、`from`、`to`のいずれか。 |
| `sortOrder` | string | `asc`または`desc`（デフォルトは`desc`）。 |

[`EmailSummaryDto`](#emailsummarydto)の[ページ分割されたエンベロープ](#pagination)を返します。
タイプ`Search`の[アクセスログ](#access-logging)エントリを記録します。

```bash
curl -H "Authorization: Bearer $KEY" \
  "https://host/api/v1/emails?q=subject:invoice&from=2026-01-01&pageSize=50"
```

```json
{
  "items": [
    {
      "id": 9876,
      "accountId": 1,
      "subject": "Invoice #4711",
      "from": "billing@vendor.com",
      "to": "support@example.com",
      "sentDate": "2026-03-02T11:04:00Z",
      "isOutgoing": false,
      "hasAttachments": true,
      "folderName": "INBOX"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalItems": 3,
  "totalPages": 1
}
```

### Get message {#get-message}

```
GET /api/v1/emails/{id}
```

本文を含むメッセージ全体を返します。本文には、Web UIと同じフォールバックチェーン（`OriginalBodyHtml` (raw bytes) → `BodyUntruncatedHtml` → `HtmlBody`、およびテキスト本文についても同様）が使用されます。メッセージが存在しない場合、または呼び出し元の権限外のアカウントに属するメッセージである場合は、`404` を返します。タイプ `Open` のアクセスログエントリを記録します。

[`EmailDetailDto`](#emaildetaildto) を返します。

### 添付ファイルのダウンロード {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

添付ファイルのバイト列を、保存されている content type および filename と共にストリームします。メッセージまたは添付ファイルが存在しない場合、あるいは呼び出し元から不可視である場合は `404` を返します。`Api:AllowAttachmentDownloads=false` の場合は `403` (problem+json) を返します。
`Download` タイプのアクセスログエントリを記録します。

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### メッセージ (.eml) のダウンロード {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

アーカイブされたフィールド（アドレス、保存された raw ヘッダー (`Received`, `Authentication-Results`, `X-Spam-*`, …)、本文、および添付ファイル）から再構築された `message/rfc822` としてメッセージを返します。すべてのアーカイブされたメールに対して機能します。これは Web UI のアカウントエクスポートによって生成されるものと同じ再構築であるため、受信したものと**バイト単位で同一ではありません**。DKIM 署名の検証は行えません。その場合は [`/raw`](#download-original-message) を使用してください。

メッセージが存在しないか、呼び出し元から参照できない場合は `404` を返し、`Api:AllowMessageDownloads=false` の場合は `403` (problem+json) を返します。タイプ `Download` のアクセスログエントリを記録します。

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### オリジナルメッセージのダウンロード {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

スパム分析、DKIM検証、またはフォレンジック調査のために、受信時のオリジナルメッセージ（`message/rfc822`）を返します。オリジナルが保存されるのは、メールがアーカイブされた時点で `RawMessages:Enabled` が設定されていた場合のみです。詳細は [Original Message Storage Settings](Setup.md#-original-message-storage-settings) を参照してください。
メールにオリジナルが存在するかどうかは、[`EmailDetailDto`](#emaildetaildto) の `hasRawMessage` で確認できます。

| レスポンスヘッダー | 意味 |
| --- | --- |
| `X-Raw-Message-Sha256` | メッセージ保存時に記録された、返されるバイト列の SHA-256 |
| `X-Raw-Message-Source` | `imap`、`graph`、`eml-file` (完全なコピー) または `serialized` (パーサーによって書き換えられ、改行コードが CRLF に正規化されたもの) |
| `X-Raw-Message-Storage` | `postgres` または `filesystem` — バイト列の読み取り元 ( [Storage backend](RawMessages.md#storage-backend) を参照) |

バイト列は、メッセージがアーカイブされた時点の `RawMessages:Storage` に応じて、データベースまたはファイル内に存在します。このエンドポイントはメッセージごとにそれを解決するため、どちらの形式も同じ方法で提供されます。`RawMessages:FileSystem:VerifyChecksumOnRead` (デフォルトは `true`) では、ファイルは送信前に再ハッシュ化され、内容が記録されたチェックサムと一致しなくなったメッセージは提供**されません**。

メッセージが存在しない、または呼び出し元から見えない場合は `404` を返し、オリジナルなしでメッセージが存在する場合は問題タイトル `No original message is stored for this email.` を伴う `404` を返し、保存された内容がアーカイブ時のチェックサムと一致しなくなった場合はタイトル `Stored original message failed its checksum.` を伴う `500` を返します。`Api:AllowMessageDownloads=false` の場合は `403` (problem+json) を返します。タイプ `Download` のアクセスログエントリを記録します。

```bash
curl -H "Authorization: Bearer $KEY" -o 9876.eml \
  https://host/api/v1/emails/9876/raw
sha256sum 9876.eml          # matches X-Raw-Message-Sha256
spamassassin -t < 9876.eml  # or: rspamc < 9876.eml
```

### Stats {#stats}

```
GET /api/v1/stats
```

呼び出し元から参照可能なアーカイブ統計を返します。アーカイブされたメールの総数、表示可能なメールアカウント、それらのメールに付随する添付ファイル、およびPostgreSQLデータベースの総サイズ（メガバイト単位）が含まれます。管理者以外のユーザーの場合、カウントは割り当てられたメールアカウントに限定されます（Webダッシュボードと同じスコープが適用されます）。管理者はグローバルな合計値を確認できます。

管理者以外のAPIキーの場合、`databaseSizeInMB`は空の文字列になります。データベースサイズはインスタンス全体を表し、キーのスコープには限定されません。Emails、Accounts、およびAttachmentsは、常にそのキーがアクセス可能なアカウントに限定されます。

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/stats
```

```json
{
  "emails": 160132,
  "accounts": 4,
  "attachments": 43540,
  "databaseSizeInMB": "5432"
}
```

### StatsDto {#statsdto}

| Field | Type | Notes |
| --- | --- | --- |
| `emails` | int | 呼び出し元から参照可能なアーカイブ済みメッセージ数 |
| `accounts` | int | 呼び出し元から参照可能なメールアカウント数 |
| `attachments` | int | 参照可能なメッセージに付随する添付ファイル数 |
| `databaseSizeInMB` | string | PostgreSQLデータベース全体のサイズ（MB単位）。管理者権限のないキーの場合は空 |

### Health {#health}

```
GET /api/v1/health/live
GET /api/v1/health/ready
GET /api/v1/health
```

これら3つすべてに管理者用APIキーが必要です。`health/live`は、どの依存関係にも触れることなく200を返します。`health/ready`はPostgreSQLとアーカイブスキーマをチェックし、いずれかが利用不可の場合に503を返します。`health`は同期の鮮度チェックも実行し、有効なアカウントが`Health:SyncStaleMinutes`以内に同期されていない場合に`Degraded` (HTTP 200) を返します。

```json
{"status":"Healthy","checks":{"database":"Healthy","sync":"Healthy"}}
```

401/403/404の挙動およびDockerのヘルスチェックを含む詳細な説明は、**[Health and Metrics](Monitoring.md)** にあります。

### Metrics {#metrics}

```
GET /api/v1/metrics
```

Prometheus テキスト形式で返します。admin API key と `Metrics:Enabled=true` が必要です。それ以外の場合、エンドポイントは 404 を返します。メトリクス名、ラベル、およびアラートの例は **[Health and Metrics](Monitoring.md)** に記載されています。

## データ型 {#data-types}

### MailAccountDto {#mailaccountdto}

| Field | Type | Notes |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

認証情報は決してシリアル化されません。

### FolderNodeDto {#foldernodedto}

| Field | Type | Notes |
| --- | --- | --- |
| `name` | string | Leaf name |
| `fullPath` | string | Full path |
| `totalCount` | int | Messages in folder |
| `level` | int | Nesting depth (0 = root) |
| `children` | FolderNodeDto[] | Sub-folders |

### EmailSummaryDto {#emailsummarydto}

| Field | Type |
| --- | --- |
| `id` | int |
| `accountId` | int |
| `subject` | string |
| `from` | string |
| `to` | string |
| `sentDate` | datetime |
| `isOutgoing` | bool |
| `hasAttachments` | bool |
| `folderName` | string |

### EmailDetailDto {#emaildetaildto}

[`EmailSummaryDto`](#emailsummarydto) のすべてに加えて：

| Field | Type | Notes |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Resolved via the body fallback chain |
| `htmlBody` | string | Resolved via the body fallback chain |
| `attachments` | AttachmentDto[] | Metadata only |
| `hasRawMessage` | bool | `true` when the original message is stored and [`/raw`](#download-original-message) returns it |

### AttachmentDto {#attachmentdto}

| Field | Type | Notes |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bytes |

添付ファイルのバイトデータは、この DTO からではなく、[download endpoint](#download-attachment) から取得します。

## アクセスログ {#access-logging}

APIを介したすべてのデータアクセスは、Web UI（**Logs** で確認可能）で使用されているものと同じアクセスログに記録され、そのキーの所有ユーザーに紐付けられます。

| エンドポイント | 記録されるタイプ |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI and Swagger UI {#openapi-and-swagger-ui}

APIが有効で`Api:EnableSwaggerUi=true`の場合：

- OpenAPI v1 ドキュメントは **`/apidocs/spec/v1.json`** で提供されます。
- Swagger UI は **`/apidocs`** で提供されます。

両方のパスは `/api/` の外側にあり、そのため通常のウェブ（cookie）認証によって保護されています。これらを表示するにはログイン済みのブラウザセッションが必要であり、APIキーではアクセスできません。

### このサイト上のエンドポイントリファレンス {#endpoint-reference-on-this-site}

エンドポイントとデータ型は、生成されたページとしてここでも公開されています：
**[API Reference](API-Reference.md)**。これは `tools/gen_api_reference.py` によって、同じ OpenAPI ドキュメント（コピーは `doc/assets/openapi-v1.json` に保持されています）からレンダリングされます。ページと仕様の同期が崩れると CI が失敗するため、リファレンスは常に実際にリリースされる API を記述したものになります。

!!! note "再生成"
    このページは生成された出力です。API（またはジェネレーター）を変更した場合は、`python3 tools/gen_api_reference.py` を実行して結果をコミットしてください。

    同梱されている仕様自体を更新するには、`tools/README.md` に記載されているビルド時のドキュメント生成を再実行する必要があります。
