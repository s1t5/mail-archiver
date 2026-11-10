# MCP Server (Model Context Protocol) {#mcp-server-model-context-protocol}

Mail Archiver は、アーカイブされたメールを Streamable HTTP トランスポートを介して AI エージェントに公開する、オプションの **MCP (Model Context Protocol) サーバー**を提供しています。これは、[REST API](API.md) をミラーリングしています。つまり、同じデータ、同じユーザーごとの権限モデル、同じアクセスログを使用し、単に REST エンドポイントではなく MCP ツールとしてパッケージ化されているため、LLM エージェントがそれらを直接発見して呼び出すことができます。

その目的は、**メールボックスの認証情報を公開したり、メールプロバイダーと通信したりすることなく**、AI エージェントにアーカイブされたメールを読み取らせることにあります。スコープ化されたユーザーごとの API キーは、「アーカイブを読み取る」ための機能であり、「メールボックスそのものになる」ための認証情報ではありません。このキーは所有者のメールボックス権限のみを継承し、書き込みパスは付与されず、基盤となるメールアカウントに触れることなく即座に失効させることができます。

> ⚠️ **MCP サーバーはデフォルトで無効になっています。** 設定で明示的に有効にする必要があります（[Enabling the MCP server](#enabling-the-mcp-server) を参照）。無効な場合、`/mcp` エンドポイントは `404 Not Found` を返します。

MCP サーバーは **設計上、読み取り専用です**。アカウントの一覧表示、フォルダの一覧表示、メッセージの検索、メッセージの読み取り、および添付ファイルのダウンロードを行うためのツールを公開します。データを生成、変更、または削除することはありません。

## 目次 {#table-of-contents}

- [MCP サーバーの有効化](#enabling-the-mcp-server)
- [認証](#authentication)
- [トランスポートとエンドポイント](#transport-and-endpoint)
- [Tools](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
  - [get_email_eml](#get_email_eml)
  - [get_email_raw](#get_email_raw)
- [レート制限](#rate-limiting)
- [アクセスログ](#access-logging)
- [エージェントの接続](#connecting-an-agent)
- [インターネット公開時のセキュリティガイダンス](#security-guidance-for-internet-exposure)

## MCP サーバーの有効化 {#enabling-the-mcp-server}

MCP サーバーは、`appsettings.json` の `Mcp` セクション（または `Mcp__Enabled=true` などの同等の環境変数）で設定されます。

```json
"Mcp": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "DefaultPageSize": 20,
  "MaxResults": 100,
  "MaxAttachmentBytes": 10000000,
  "AllowMessageDownloads": true,
  "MaxMessageBytes": 10000000,
  "RateLimitPerMinute": 120
}
```

| 設定 | デフォルト | 説明 |
| --- | --- | --- |
| `Enabled` | `false` | マスタースイッチ。`false` のとき、`/mcp` エンドポイントは `404` を返します。 |
| `AllowAttachmentDownloads` | `true` | `false` のとき、`get_attachment` は添付ファイルのバイトの代わりにエラーを返します。添付ファイルに関するメタデータは引き続き `get_email` によって返されます。 |
| `DefaultPageSize` | `20` | 呼び出し側が `pageSize` を省略した場合に `search_emails` が使用するページサイズ。 |
| `MaxResults` | `100` | `search_emails` ページサイズの最大値。これより大きい値は制限されます。 |
| `MaxAttachmentBytes` | `10000000` | `get_attachment` が base64 としてインラインで返す添付ファイルの最大サイズ（バイト単位）。これより大きい添付ファイルは拒否されます。 |
| `AllowMessageDownloads` | `true` | `false` のとき、`get_email_eml` と `get_email_raw` はメッセージの代わりにエラーを返します。完全なメッセージには添付ファイルが含まれるため、このスイッチと `AllowAttachmentDownloads` の**両方**が `true` である必要があります。どちらか一方をオフにすると、両方のツールが無効になります。 |
| `MaxMessageBytes` | `10000000` | `get_email_eml` と `get_email_raw` がインラインで返すメッセージの最大サイズ（バイト単位）。これより大きいメッセージは拒否されます。 |
| `RateLimitPerMinute` | `120` | API キーごとの 1 分間あたりの固定ウィンドウ・リクエスト予算（[Rate limiting](#rate-limiting) を参照）。 |

## Authentication {#authentication}

MCP サーバーは、**REST API と同じユーザーごとの API キー**を使用して認証を行い、ベアラートークンとして送信されます：

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

API キーは、Web UI の **API Keys** で作成および管理されます。キーは、その所有者であるユーザーの権限を正確に継承します（管理者はすべてのカウントを表示でき、制限されたユーザーは割り当てられたアカウントのみを表示できます）。キーは、基盤となるメールアカウントとは独立して失効または期限切れにすることができます。キーのライフサイクルと権限モデルの詳細については、[REST API authentication section](API.md#authentication) を参照してください。MCP キーは、個別の認証情報ストアではありません。

MCP サーバーが無効化されている場合、または有効なベアラートークンが提示されていない場合、エンドポイントはそれぞれ `404` / `401` を返します（ログインページにリダイレクトされることはありません）。

## Transport and endpoint {#transport-and-endpoint}

MCP server は **Streamable HTTP** トランスポートを使用し、以下にマウントされます：

```
POST /mcp
```

これは **stateless** モード（1つの HTTP リクエストにつき 1つの JSON-RPC リクエスト）で動作します。これは、sampling や elicitation のようなサーバーからクライアントへのリクエストを必要としないサーバーに推奨されるモードです。ツールは標準的な MCP `tools/list` リクエストを介して検出され、`tools/call` を介して呼び出されます。

## Tools {#tools}

### list_accounts {#list_accounts}

現在のAPIキーがアクセスを許可されているメールアカウントをリストします。`id`、`name`、`emailAddress`、`provider`、`isEnabled`、および `lastSync` を返します。

**Parameters:** なし。

**Result:** アカウントオブジェクトの配列。返された `id` を `list_folders` および `search_emails` と併用してください。

### list_folders {#list_folders}

単一のメールアカウントのフォルダツリーを、フォルダごとのメール件数とともにリスト表示します。

**パラメータ:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `accountId` | integer | yes | メールアカウントの ID (`list_accounts` より)。 |

**結果:** フォルダオブジェクトの配列。各オブジェクトは `name`、`fullPath`、`totalCount`、`level` および再帰的な `children` を持ちます。アカウントが存在しない場合、またはキーがそのアカウントに対して承認されていない場合は、空のリストを返します。

### search_emails {#search_emails}

全文検索クエリ、日付範囲、アカウント、フォルダ、および方向によってアーカイブされたメールを検索します。

**パラメータ:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `q` | string | no | 全文検索クエリ（件名、本文、送信元/送信先）。 |
| `from` | string (ISO-8601) | no | 送信日時が指定したUTC日時以降であることのフィルタ。 |
| `to` | string (ISO-8601) | no | 送信日時が指定したUTC日時以前であることのフィルタ。 |
| `accountId` | integer | no | 単一のメールアカウント id に制限します。 |
| `folder` | string | no | 単一のフォルダのフルパスに制限します（例: `INBOX`）。 |
| `direction` | string | no | `incoming` または `outgoing`。 |
| `page` | integer | no | 1から始まるページ番号（デフォルトは `1`）。 |
| `pageSize` | integer | no | ページサイズ。`[1, MaxResults]` に制限されます。`0` は `DefaultPageSize` を使用します。 |
| `sortBy` | string | no | `sentdate`（デフォルト）、`receiveddate`、`subject`、`from`、`to`。 |
| `sortOrder` | string | no | `asc` または `desc`（デフォルトは `desc`）。 |

**結果:** `items`（メールサマリーの配列）、`page`、`pageSize`、`totalItems`、`totalPages` を含むページ分割されたオブジェクト。各サマリーには `id`、`accountId`、`subject`、`from`、`to`、`sentDate`、`isOutgoing`、`hasAttachments`、および `folderName` が含まれます。返された `id` を `get_email` に渡すと、本文の全文と添付ファイルリストを取得できます。

### get_email {#get_email}

アーカイブされた単一のメールの全内容を取得します。

**パラメータ:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | アーカイブされたメールの id (`search_emails` から取得)。 |

**結果:** すべてのサマリーフィールドに加えて、`cc`、`bcc`、`receivedDate`、`messageId`、`textBody`、`htmlBody`、`attachments` (`{ id, fileName, contentType, size }` の配列)、および `hasRawMessage` (`get_email_raw` がオリジナルを返せるかどうか) を含む単一のメールオブジェクト。メールが存在しない場合、またはキーがそのアカウントに対して許可されていない場合は `null` を返します。

### get_attachment {#get_attachment}

単一のメール添付ファイルをbase64 blobとしてダウンロードします。

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | 添付ファイルが属するアーカイブ済みメールのid。 |
| `attachmentId` | integer | yes | 添付ファイルのid（`get_email`の`attachments`から取得）。 |

**Result:** `id`、`emailId`、`fileName`、`contentType`、`size`、および `base64Data`（base64エンコードされた生の添付ファイルバイト）を含むオブジェクト。このツールは、`Mcp:AllowAttachmentDownloads=false`の場合、添付ファイルが `Mcp:MaxAttachmentBytes` を超える場合、またはキーがメールアカウントに対して権限を持っていない場合にエラーを返します。

### get_email_eml {#get_email_eml}

アーカイブされたフィールド（保存されたヘッダー（`Received`, `Authentication-Results`, `X-Spam-*`, …）、本文、および添付ファイル）から再構築された、完全な RFC 5322 メッセージ（`.eml`）としてメールを返します。すべてのアーカイブされたメールに対して機能しますが、結果は受信したものとバイト単位で一致しないため、DKIM 署名の検証には使用できません。その場合は [`get_email_raw`](#get_email_raw) を使用してください。`GET /api/v1/emails/{id}/eml` をミラーリングします。

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | アーカイブされたメール ID（`search_emails` より）。 |

**Result:** `emailId`, `kind` (`eml`), `source` (`null`), `sha256`, `size`, `encoding`, および `content` を含むオブジェクト。`encoding` が `utf-8` の場合、`content` はメッセージのテキストであり、`encoding` が `base64` の場合はメッセージが base64 エンコードされます（有効な UTF-8 ではないメッセージであり、バイトが変更されないようにするため）。このツールは、`Mcp:AllowMessageDownloads=false` の場合、メッセージが `Mcp:MaxMessageBytes` を超える場合、またはキーがそのメールアカウントに対して許可されていない場合にエラーを返します。

### get_email_raw {#get_email_raw}

スパム分析、DKIM/SPF/ARCチェック、またはフォレンジックのために、受信した通りのオリジナルのメッセージを返します。アーカイブ時にオリジナルが保存されている場合のみ利用可能です（`RawMessages:Enabled`、[Original Message Storage Settings](Setup.md#-original-message-storage-settings)を参照）。`get_email`はこれを`hasRawMessage`として報告します。`GET /api/v1/emails/{id}/raw`をミラーリングします。

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | アーカイブされたメールID（`search_emails`から取得）。 |

**Result:** `kind` `raw`、オリジナルのキャプチャ方法（`imap`、`graph`、`eml-file` — 完全なコピー — または、パーサーによって行末がCRLFに正規化されて書き換えられた`serialized`）に設定された`source`、およびアーカイブ時に記録されたチェックサムである`sha256`を持つ、`get_email_eml`と同じオブジェクト。`get_email_eml`と同じエラー、およびメールのオリジナルが保存されていない場合はエラーを返します。

バイトは、アーカイブ時の`RawMessages:Storage`に応じて、データベースまたはファイルのうち保存されている場所から読み取られます（[Storage backend](RawMessages.md#storage-backend)を参照）。`RawMessages:FileSystem:VerifyChecksumOnRead`（デフォルトは`true`）では、ファイルは返される前に再ハッシュ化されます。コンテンツが記録されたチェックサムと一致しなくなったメッセージは、返されるのではなくエラーとして拒否されます。

## Rate limiting {#rate-limiting}

MCP サーバーには、独自の固定ウィンドウ方式のレート制限ポリシー (`"Mcp"`) が備わっています。これは、非機密の API-key プレフィックス (`ma_...` トークンの最初の 11 文字) によってパーティショニングされており、ベアラートークンが存在しない場合はクライアント IP にフォールバックします。バジェットは `Mcp:RateLimitPerMinute` を介して設定されます (デフォルトは 1 分あたり `120`)。制限を超過した場合、エンドポイントは `Retry-After` ヘッダーとともに `429 Too Many Requests` を返します。これは REST API のレート制限ポリシーとは独立しています。

## アクセスログ {#access-logging}

すべてのツール呼び出しは、REST APIと同じ`IAccessLogService`を通じてログに記録され、所有するユーザー名と一致するアクセスタイプ（`Search`、`Open`、`Download`）が記録されます。したがって、MCPのアクティビティは**Logs**ページ上でREST APIやWeb-UIのアクティビティと並んで表示され、単一の監査証跡として確認できます。

## エージェントの接続 {#connecting-an-agent}

MCPサーバーは標準的なMCP Streamable HTTPトランスポートを使用しているため、HTTPをサポートする任意のMCPクライアントが接続可能です。URLはアプリケーションのベースURLに`/mcp`を加えたもの（例：`http://localhost:5179/mcp`）であり、APIキーは`Authorization: Bearer ma_...`ヘッダーを介して提供されます。

標準的なMCPクライアントJSONを介して設定されたエージェントの場合、エントリは以下のようになります：

```json
{
  "mcpServers": {
    "mail-archiver": {
      "url": "https://archive.example.com/mcp",
      "headers": {
        "Authorization": "Bearer ma_<your-api-key>"
      }
    }
  }
}
```

## インターネット公開に関するセキュリティガイダンス {#security-guidance-for-internet-exposure}

MCP サーバーは、デフォルト設定および以下の要件による要塞化が行われている場合、アカウントに対して明示的に発行された有効なユーザーごとの API キーなしでは、アーカイブされたメールがシステム外へ流出しないように設計されています。以下の項目は、**`/mcp` をインターネットに公開する前に必ず読んでください**。

### 必須事項（悪用 / データ流出の防止） {#mandatory-block-exploitation-data-exfiltration}

1. **HTTPS のみを使用すること。** TLS はエッジ（リバースプロキシ）で終端させてください。プレーンな HTTP 上のベアラ API キーは、スニッフィング（盗聴）され、再利用される可能性があります。
2. **実際に使用しない限り `Mcp:Enabled=false` を保持しないこと。** 無効化されている場合、`/mcp` は `404` を返し、どのコードパスにも到達できません。これは、AI エージェントによるアクセスを必要としないデプロイメントにおける、安全なデフォルト設定および安全な状態です。
3. **エンドポイントに対してレート制限を適用すること。** `"Mcp"` ポリシー（`Mcp:RateLimitPerMinute`、デフォルトは `120`）が、マッピングされた `/mcp` エンドポイントに適用されます。これは、非機密の API キー接頭辞（`ma_...` トークンの最初の 11 文字）によってパーティション化されるか、ベアラートークンが存在しない場合はクライアント IP によってパーティション化されます。インターネットに公開する場合は、**`RateLimitPerMinute` を低めに設定してください**（例：`30`–`60`）。MCP ツール呼び出しは REST 呼び出しよりも負荷が高いため、信頼できないクライアントがアクセスできる場合に、エンドポイントの予算（バジェット）を高く設定したままにしてはいけません。
4. **リバースプロキシの `X-Forwarded-For` は慎重に信頼する必要があります。** レートリミッターは `RemoteIpAddress` にフォールバックしますが、プロキシ経由の場合、`UseForwardedHeaders` に実際のクライアント IP が渡されない限り、それはプロキシの IP になります。プロキシが `X-Forwarded-For` を正しく設定していること、およびあなたのプロキシのみがそれを設定できることを確認してください。そうでない場合、攻撃者がレート制限のパーティションを偽装できる可能性があります。
5. **`/mcp` を CDN キャッシュの背後に配置しないでください。** MCP レスポンスはリクエストごとの JSON-RPC であり、決してキャッシュしてはいけません。キャッシュを行うと、プロトコルが壊れるだけでなく、あるユーザーの結果が別のユーザーに漏洩することになります。エッジ（`Cache-Control: no-store`）で `/mcp` をキャッシュ不可としてマークしてください。

### API キーの衛生管理（不正アクセスの防止） {#api-key-hygiene-block-unauthorized-access}

6. **MCP エージェントには、専用の短寿命のキーを発行すること。** 単一の `ma_...` キーは `/api` と `/mcp` の両方に有効です。現在、プロトコルごとのスコープは存在しません。したがって、以下の対応を行ってください：
   - **エージェントごとに個別のキーを作成する**（レポート作成スクリプト用のキーを外部 LLM エージェントと再利用しないでください）。
   - 常に短い `ExpiresAt` を設定し、ローテーションしてください。エージェントが廃止された場合は、直ちに失効させてください。
   - エージェントが必要とするアカウントのみが割り当てられた、**制限された（非管理者）ユーザー**が所有するキーを優先してください。管理者キーは *すべての* メールアカウントへの可視性を付与してしまいますが、制限されたキーは影響範囲（ブラスト半径）を限定できます。
7. **API キーをパスワードと同様に扱ってください。** これはベアラ資格情報です。クライアント側のコードに埋め込んだり、ログに記録したり、リポジトリにコミットしたりしないでください。MCP クライアントのシークレットストアに保存し、漏洩の疑いがある場合はローテーションしてください。
8. **Logs ページで監査を行うこと。** すべてのツール呼び出しは、所有ユーザーの下で `AccessLogs`（`Search`、`Open`、`Download`）に記録されます。予期しないパターン（ボリュームの急増、時間外の読み取り、大量のダウンロード）がないか、定期的に MCP アクティビティを確認してください。

### 返却データの信頼モデル（ダウンストリームの被害防止） {#trust-model-for-returned-data-block-downstream-harm}

9. **メール本文および添付ファイルは、信頼できないコンテンツです。** それらはメールボックスに届いたときと全く同じ状態です。マルウェア、フィッシングリンク、またはアクティブな HTML/JavaScript が含まれている可能性があります。
   - `get_email` は、**生のままサニタイズされていない** `htmlBody` を返します。サニタイズ（DOMPurify / `HtmlSanitizer` / サンドボックス）を行わずに、人間に対してこれをレンダリングしては**いけません**。生のアーカイブされた HTML をレンダリングすることは、蓄積型 XSS（Stored-XSS）のベクトルとなります。
   - `get_attachment` は、元の `contentType` を伴う生のバイトを返します。添付ファイルを自動的に開いたり、自動的に実行したりしては**いけません**。`contentType` フィールドは、安全な指示としてではなく、信頼できないメタデータとして扱ってください。
   - `get_email_eml` および `get_email_raw` は、すべての添付ファイルを含む完全なメッセージを返します。各パーツに対しても同様のルールが適用されます。
10. **確認なしに、メールの内容に基づいてエージェントに書き込みアクションを行わせないでください。** 悪意のあるメールには、利用する LLM を標的としたプロンプトインジェクションのテキストが含まれている可能性があります。エージェント自身のロジック内で権限と確認を強制してください。システムプロンプトはセキュリティ境界ではありません。

### 多層防御の推奨事項 {#defense-in-depth-recommendations}

11. **可能な場合はネットワーク層で `/mcp` を制限してください**（例：リバースプロキシでエージェントホストの IP / VPN 範囲のみを許可するなど）。これは、API キー認証に加えて、インターネット公開における最も効果的な制御策です。
12. エージェントに添付ファイルのバイトが必要ない場合は、**`Mcp:AllowAttachmentDownloads` を `false` に下げてください**。添付ファイルの流出は、最もデータ量の多いパスです。メタデータ（ファイル名、コンテンツタイプ、サイズ）は、引き続き `get_email` を通じて返されます。
13. **`Mcp:MaxAttachmentBytes` を、エージェントが実際に必要とする最小値に下げてください**（デフォルト `10000000` ≈ 10 MB）。これにより、API キーが 1 リクエストあたりに取得できる単一の最大レスポンスサイズを制限できます。同様のことが `Mcp:MaxMessageBytes` にも言えます。また、エージェントに完全なメッセージが必要ない場合は、`Mcp:AllowMessageDownloads` を `false` に設定してください。
14. **`Mcp:MaxResults` と `Mcp:DefaultPageSize` を下げて**、API キーが 1 リクエストあたりに取得できる最大の検索結果を制限してください（デフォルト `100` / `20`）。
15. 運用として、**`AccessLogs` とレート制限による拒否（`429`）を監視してください**。単一のキー接頭辞からの急激なスパイクは、そのキーを失効させるべきシグナルです。
