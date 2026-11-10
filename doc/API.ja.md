# 読み取り専用 REST API (v1) {#read-only-rest-api-v1}

Mail Archiver は、アーカイブされたメールへのプログラムによるアクセスのための、オプションの**読み取り専用** REST API を提供します。これは、Web UI を介さずに、自動化および統合のユースケース（レポート作成、e-discovery エクスポート、モニタリング）を行うために設計されています。

目的は、スクリプトや AI エージェントが、**メールボックスの認証情報を公開したり、メールプロバイダーと通信したりすることなく**、アーカイブされたメールを読み取れるようにすることです。スコープが限定されたユーザーごとの API キーは、「アーカイブを*読み取る*」ための機能であり、「メールボックス*そのものになる*」ための認証情報ではありません。これは所有者のメールボックス権限のみを継承し、書き込みパスを一切提供せず、基盤となるメールアカウントに触れることなく即座に失効させることができます。

> ⚠️ **API はデフォルトで無効になっています。** 設定で明示的に有効にする必要があります（[Enabling the API](#enabling-the-api) を参照）。無効な場合、すべての `/api/*` ルートは `404 Not Found` を返します。

API は**設計上、読み取り専用**です。アカウント、フォルダー、メッセージ検索、メッセージ詳細、および添付ファイルのダウンロードを公開します。データを生成、変更、または削除することはありません。

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
  - [メッセージ (.eml) のダウンロード](#download-message-eml)
  - [オリジナルメッセージのダウンロード](#download-original-message)
  - [統計](#stats)
- [データ型](#data-types)
- [アクセスログ](#access-logging)
- [OpenAPI および Swagger UI](#openapi-and-swagger-ui)
- [APIリファレンス](API-Reference.md) — 生成されたエンドポイントおよびデータ型のリファレンス

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

1. **作成 (Create)** — ユーザーが記述的な`Name`と任意の有効期限を指定してキーを作成します。レスポンスには、平文のキーが一度だけ表示されます。
2. **使用 (Use)** — ベアラートークンとしてキーを送信します。リクエストが受理されるたびに、キーの`LastUsedAt`が更新されます（すべての呼び出しで書き込みが発生するのを避けるため、書き込みは最大で約5分に1回に制限されます）。
3. **期限切れ (Expire)** — `ExpiresAt`が設定されており、かつ過去の日時である場合、キーは拒否されます。
4. **失効 (Revoke)** — 所有ユーザーまたは管理者がキーを失効させます（`RevokedAt`が設定されます）。失効したキーは、直ちに、かつ永久に拒否されます。

キーが、失効しておらず、期限切れではなく、**かつ**所有ユーザーがまだアクティブである場合、そのキーは**アクティブ**です。これらの理由のいずれかによってキーが非アクティブになった場合、`401`で拒否されます。

### 権限モデル {#permission-model}

APIキーは、その所有者であるユーザーと全く同じ権限を継承します。認証されたプリンシパルは、通常のウェブログインが発行するものと同じクレームを保持するため、既存のすべての認可...

| 設定 | デフォルト | 説明 |
| --- | --- | --- |
| `Enabled` | `false` | マスタースイッチ。`false`のとき、すべての`/api/*`ルートは`404`を返し、OpenAPIドキュメントおよびSwagger UIはマッピングされません。 |
| `AllowAttachmentDownloads` | `true` | `false`のとき、添付ファイルのダウンロードエンドポイントは`403`を返します。添付ファイルに関するメタデータは、引き続きメッセージ詳細エンドポイントによって返されます。 |
| `AllowMessageDownloads` | `true` | `false`のとき、complete-messageエンドポイント（`/eml`および`/raw`）は`403`を返します。完全なメッセージには添付ファイルが含まれるため、このスイッチと`AllowAttachmentDownloads`の**両方**が`true`である必要があります。どちらか一方をオフにすると、両方のエンドポイントが無効になります。 |
| `EnableSwaggerUi` | `true` | `true`のとき（かつ`Enabled`が`true`のとき）、`/apidocs`にSwagger UIを、`/apidocs/spec/v1.json`にOpenAPIドキュメントをマウントします。両方とも認証された**ウェブ（クッキー）セッション**を必要とします。これらは`/api/`の一部ではなく、APIキーではアクセスできません。 |
| `DefaultPageSize` | `20` | リクエストで`pageSize`が省略された場合に使用されるページサイズ。 |
| `MaxPageSize` | `100` | 上限。より大きな`pageSize`の値はこの値にクランプされます。 |
| `RateLimitPerMinute` | `120` | APIキーごとの1分あたりの固定ウィンドウリクエスト予算（[Rate limiting](#rate-limiting)を参照）。 |

## 認証 {#authentication}

APIは、ベアラートークンとして送信される**ユーザーごとのAPIキー**を使用して認証を行います。

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1`はAPIキー**のみ**を受け付けます。ブラウザのクッキーは尊重されないため、APIにCSRFの攻撃対象は存在しません。認証に失敗した場合、`401`、`WWW-Authenticate: Bearer`ヘッダー、および[problem+json](#errors)ボディが返されます。APIがログインページにリダイレクトすることは**ありません**。

キーは、Web UIの**API Keys**からセルフサービスで管理されます（各ユーザーは自身のキーを管理し、管理者は任意のユーザーのキーを表示および失効させることができます）。

### APIキーの形式 {#api-key-format}

キーは、文字列`ma_`に、32バイトのランダムなバイト（256ビットのエントロピー）をエンコードした43文字のURLセーフなbase64文字が続くものです。

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

ストレージ:

- **`KeyPrefix`** — 最初の約11文字（例：`ma_8Kf3pQ2`）。プレーンテキストで保存されます。UIやログ内でキーを識別するために使用されます。これは秘密情報ではありません。
- **`KeyHash`** — フルキーのSHA-256（16進数エンコード）であり、**一意のインデックス**の下に保存されます。プレーンテキストのキーは決して保存されません。

フルキーは、作成時に**一度だけ**ユーザーに表示されます。その後、キーを回収することはできません。紛失したキーは、失効させて新しいものと交換する必要があります。

### キーのライフサイクル {#key-lifecycle}

1. **作成 (Create)** — ユーザーが説明的な`Name`と、オプションの有効期限を指定してキーを作成します。レスポンスには、プレーンテキストのキーが一度だけ表示されます。
2. **使用 (Use)** — キーをベアラートークンとして送信します。リクエストが受理されるたびに、キーの`LastUsedAt`が更新されます（すべての呼び出しに対して書き込みが発生するのを防ぐため、書き込みは最大で約5分に1回に制限されます）。
3. **期限切れ (Expire)** — `ExpiresAt`が設定されており、かつ過去の日時である場合、キーは拒否されます。
4. **失効 (Revoke)** — 所有ユーザーまたは管理者がキーを失効させます（`RevokedAt`が設定されます）。失効したキーは、直ちに、かつ永久に拒否されます。

キーが**アクティブ**であるとは、失効しておらず、期限切れではなく、**かつ**その所有ユーザーがまだアクティブである状態を指します。これらの理由のいずれかによって非アクティブになったキーは、`401`で拒否されます。

### 権限モデル {#permission-model}

APIキーは、その所有者であるユーザーと全く同じ権限を継承します。認証されたプリンシパルは、通常のWebログインと同じクレームを保持するため、既存のすべての認可ロジックがそのまま適用されます。

- **管理者 (Administrators)** (`IsAdmin`) は、すべてのメールアカウントとすべてのメッセージを表示できます。
- **一般ユーザー (Regular users)** は、自身に割り当てられたメールアカウント（`UserMailAccount`）のみを表示できます。その範囲外のアカウントやメッセージは**存在しないもの**として扱われます。それらへのリクエストは`403`ではなく`404`を返すため、APIは呼び出し元が見ることができないアカウントの存在を漏洩させることはありません。

### セキュリティ上の根拠 {#security-rationale}

- **2FAのバイパス。** APIキーは設計上、二要素認証をバイパスします。これは非対話型トークンの特性です（GitHub/GitLabのパーソナルアクセストークンも同様です）。これは、キーが**デフォルトでオフ**であること、失効可能であること、オプションで期限切れを設定できること、ユーザーの権限に限定されていること、およびすべてのアクセスが[アクセスログ](#access-logging)に記録されることによって軽減されています。
- **`/api`でのクッキー不使用。** `/api`ルートではクッキー認証を受け付けないため、APIにおけるCSRFの懸念が排除されています。
- **トランスポート。** 本番環境では、ベアラートークンがネットワーク上で露出しないよう、常にHTTPS経由でAPIを提供してください。

## 規約 {#conventions}

### ベース URL とバージョニング {#base-url-and-versioning}

すべてのエンドポイントは `/api/v1` の下にあります。将来的な破壊的変更を、既存のクライアントに影響を与えることなく `/api/v2` としてリリースできるように、バージョンはパスに含まれています。

特に断りがない限り、レスポンスは `application/json` です（添付ファイルのダウンロードは、その添付ファイル自身の content type を返します）。すべてのタイムスタンプは UTC、ISO 8601 (`2026-06-11T09:30:00Z`) です。

### ページネーション {#pagination}

リストのエンドポイントは、ページネーションのエンベロープを返します。

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

### エラー {#errors}

エラーには [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) を使用します。
`application/problem+json`:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Unknown sortBy value 'foo'.",
  "instance": "/api/v1/emails"
}
```

| ステータス | 発生条件 |
| --- | --- |
| `400 Bad Request` | 不正なパラメータ（例：解析不能な日付、無効な `sortBy`）。 |
| `401 Unauthorized` | キーの欠落、不正、期限切れ、失効、またはその他の無効なキー。`WWW-Authenticate: Bearer` を含みます。 |
| `403 Forbidden` | `AllowAttachmentDownloads=false` 中の添付ファイルのダウンロード、または `AllowMessageDownloads=false` 中のメッセージのダウンロード。 |
| `404 Not Found` | リソースが存在しない、**または** 呼び出し者の許可されたアカウント外である。また、`Api:Enabled=false` の際のすべての `/api/*` ルートでも返されます。 |
| `429 Too Many Requests` | レート制限を超過しました。 |
| `500 Internal Server Error` | 予期しないサーバーエラー (problem+json、HTMLのエラーページではありません)。 |

### レート制限 {#rate-limiting}

リクエストは、API キーのプレフィックスによって分割された、1 分間あたりの `Api:RateLimitPerMinute` 回（デフォルト `120`）の固定ウィンドウポリシーによって制限されます（キーが存在しない場合はクライアント IP にフォールバックされます）。予算を超過すると `429` が返されます。

## エンドポイント {#endpoints}

> すべての例は、APIが有効であり、かつ `$KEY` が有効なキーを保持していることを前提としています。

### アカウントの一覧表示 {#list-accounts}

```
GET /api/v1/accounts
```

呼び出し元から参照可能なメールアカウントを返します。認証情報フィールド（IMAP/OAuthのユーザー名、パスワード、クライアントシークレットなど）は**決して**含まれません。

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

### フォルダの一覧表示 {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

1つのアカウントのフォルダツリーをネストされた構造で返します。アカウントが存在しない場合、または呼び出し元から参照できない場合は `404` を返します。

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

### メッセージの検索 {#search-messages}

```
GET /api/v1/emails
```

Web UIと同じ機能を使用して、アーカイブされたメッセージを検索します。

| クエリパラメータ | 型 | 説明 |
| --- | --- | --- |
| `q` | string | 件名、本文、from、to、cc、bccに対する全文検索。`subject:invoice`、`from:alice@example.com`などのフィールド構文をサポートしています。 |
| `from` | date | 送信日の最早（含む）、`YYYY-MM-DD`。 |
| `to` | date | 送信日の最新（含む）、`YYYY-MM-DD`。 |
| `accountId` | int | 1つのアカウントに制限（呼び出し元から見えるものである必要があります）。 |
| `folder` | string | フルパスによる1つのフォルダへの制限。 |
| `direction` | string | `incoming`または`outgoing`。 |
| `page` | int | 1から始まるページ番号（デフォルトは`1`）。 |
| `pageSize` | int | 1ページあたりの結果数（デフォルトは`Api:DefaultPageSize`、上限は`Api:MaxPageSize`）。 |
| `sortBy` | string | `sentDate`（デフォルト）、`receivedDate`、`subject`、`from`、`to`のいずれか。 |
| `sortOrder` | string | `asc`または`desc`（デフォルトは`desc`）。 |

[`EmailSummaryDto`](#emailsummarydto)の[ページングされたエンベロープ](#pagination)を返します。
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

### メッセージの取得 {#get-message}

```
GET /api/v1/emails/{id}
```

ボディを含む完全なメッセージを返します。ボディは、Web UIと同じフォールバックチェーン（`OriginalBodyHtml` (raw bytes) → `BodyUntruncatedHtml` → `HtmlBody`、およびテキストボディの同等のもの）を使用します。メッセージが存在しない場合、または呼び出し出しの権限外のアカウントに属している場合は、`404` を返します。タイプ `Open` のアクセスログエントリを記録します。

[`EmailDetailDto`](#emaildetaildto) を返します。

### 添付ファイルのダウンロード {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

添付ファイルのバイト列を、保存されているコンテンツタイプおよびファイル名とともにストリームします。メッセージまたは添付ファイルが存在しないか、呼び出し元から参照できない場合は `404` を返します。`Api:AllowAttachmentDownloads=false` の場合は `403` (problem+json) を返します。
タイプ `Download` のアクセスログエントリを記録します。

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### メッセージ (.eml) のダウンロード {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

アーカイブされたフィールド（アドレス、保存された raw ヘッダー（`Received`、`Authentication-Results`、`X-Spam-*`、…）、本文、および添付ファイル）から再構築された `message/rfc822` としてメッセージを返します。これはすべてのアーカイブされたメールに対して機能します。これは Web UI のアカウントエクスポートによって生成されるものと同じ再構築であるため、受信したものと**バイト単位で同一ではありません**。DKIM 署名の検証には使用できません。その場合は [`/raw`](#download-original-message) を使用してください。

メッセージが存在しないか、呼び出し元から参照できない場合は `404` を返し、`Api:AllowMessageDownloads=false` の場合は `403` (problem+json) を返します。タイプ `Download` のアクセスログエントリを記録します。

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### オリジナルメッセージのダウンロード {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

スパム分析、DKIM検証、またはフォレンジックチェックのために、受信時のオリジナルメッセージ（`message/rfc822`）を返します。オリジナルは、メールがアーカイブされた時点で `RawMessages:Enabled` が設定されていた場合にのみ保存されます。詳細は [Original Message Storage Settings](Setup.md#-original-message-storage-settings) を参照してください。
メールにオリジナルが存在するかどうかは、[`EmailDetailDto`](#emaildetaildto) の `hasRawMessage` で示されます。

| レスポンスヘッダー | 意味 |
| --- | --- |
| `X-Raw-Message-Sha256` | メッセージが保存されたときに記録された、返却されるバイトの SHA-256 |
| `X-Raw-Message-Source` | `imap`、`graph`、`eml-file` (正確なコピー) または `serialized` (パーサーによって書き換えられ、行末が CRLF に正規化されたもの) |
| `X-Raw-Message-Storage` | `postgres` または `filesystem` — バイトが読み取られた場所 ( [Storage backend](RawMessages.md#storage-backend) を参照) |

バイトは、メッセージがアーカイブされた時点の `RawMessages:Storage` に応じて、データベースまたはファイル内に存在します。エンドポイントはメッセージごとにこれを解決するため、どちらの形式も同様に提供されます。`RawMessages:FileSystem:VerifyChecksumOnRead` (デフォルトは `true`) を使用すると、ファイルは送信前に再ハッシュ化され、記録されたチェックサムとコンテンツが一致しなくなったメッセージは**提供されません**。

メッセージが存在しない場合、または呼び出し元から不可視である場合は `404` を返します。オリジナルが存在しないメッセージに対しては、問題のタイトル `No original message is stored for this email.` を伴う `404` を返します。保存されたコンテンツがアーカイブ時のチェックサムと一致しなくなった場合は、タイトル `Stored original message failed its checksum.` を伴う `500` を返します。`Api:AllowMessageDownloads=false` の場合は `403` (problem+json) を返します。`Download` 型のアクセスログエントリを記録します。

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

呼び出し元に表示されるアーカイブ統計の集計値を返します：アーカイブされたメールの総数、表示可能なメールアカウント、それらのメールに付随する添付ファイル、およびメガバイト単位のPostgreSQLデータベースの総サイズ。管理者権限のないユーザーの場合、カウントは割り当てられたメールアカウントに限定されます（これはウェブダッシュボードに適用されるものと同じスコープです）。管理者はグローバルな合計値を確認できます。

管理者権限のないAPIキーの場合、`databaseSizeInMB`は空の文字列になります。データベースサイズはインスタンス全体を表し、キーのスコープには限定されません。Emails、Accounts、およびAttachmentsは、常にそのキーがアクセス可能なアカウントに限定されます。

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

| フィールド | 型 | 備考 |
| --- | --- | --- |
| `emails` | int | 呼び出し元から参照可能なアーカイブ済みメッセージ数 |
| `accounts` | int | 呼び出し元から参照可能なメールアカウント数 |
| `attachments` | int | 参照可能なメッセージに付随する添付ファイル数 |
| `databaseSizeInMB` | string | PostgreSQLデータベース全体のサイズ（MB単位）。管理者以外のキーの場合は空 |

## データ型 {#data-types}

### MailAccountDto {#mailaccountdto}

| フィールド | 型 | 備考 |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

認証情報フィールドがシリアライズされることはありません。

### FolderNodeDto {#foldernodedto}

| フィールド | 型 | 備考 |
| --- | --- | --- |
| `name` | string | Leaf name |
| `fullPath` | string | Full path |
| `totalCount` | int | フォルダ内のメッセージ数 |
| `level` | int | ネストの深さ (0 = root) |
| `children` | FolderNodeDto[] | サブフォルダ |

### EmailSummaryDto {#emailsummarydto}

| フィールド | 型 |
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

[`EmailSummaryDto`](#emailsummarydto) のすべてに加えて:

| フィールド | 型 | 備考 |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | body fallback chain を介して解決 |
| `htmlBody` | string | body fallback chain を介して解決 |
| `attachments` | AttachmentDto[] | メタデータのみ |
| `hasRawMessage` | bool | 元のメッセージが保存されており [`/raw`](#download-original-message) がそれを返す場合の `true` |

### AttachmentDto {#attachmentdto}

| フィールド | 型 | 備考 |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | バイト数 |

添付ファイルのバイトデータは、この DTO からではなく [download endpoint](#download-attachment) から取得します。

## アクセスログ {#access-logging}

APIを介したすべてのデータアクセスは、Web UI（**Logs**の下に表示）で使用されるものと同じアクセスログに記録され、キーの所有ユーザーに関連付けられます。

| エンドポイント | ログに記録されるタイプ |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI と Swagger UI {#openapi-and-swagger-ui}

API が有効で `Api:EnableSwaggerUi=true` のとき：

- OpenAPI v1 ドキュメントは **`/apidocs/spec/v1.json`** で提供されます。
- Swagger UI は **`/apidocs`** で提供されます。

両方のパスは `/api/` の外側にあり、そのため通常の Web (cookie) 認証によって制限されています。これらを表示するには、ログイン済みのブラウザセッションが必要であり、API キーではアクセスできません。

### このサイト上のエンドポイントリファレンス {#endpoint-reference-on-this-site}

エンドポイントとデータ型は、生成されたページとしてここにも公開されています：
**[API Reference](API-Reference.md)**。これは `tools/gen_api_reference.py` によって、同じ OpenAPI ドキュメント（コピーは `doc/assets/openapi-v1.json` に保持されています）からレンダリングされます。ページと仕様の同期が取れなくなると CI が失敗するため、リファレンスは常に実際にリリースされる API を記述したものになります。

!!! note "再生成"
    このページは生成された出力です。API（またはジェネレーター）を変更した場合は、`python3 tools/gen_api_reference.py` を実行して結果をコミットしてください。

    同梱されている仕様自体を更新する場合は、`tools/README.md` に記載されているビルド時のドキュメント生成を再実行することを意味します。
