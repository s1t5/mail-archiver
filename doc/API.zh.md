# 只读 REST API (v1) {#read-only-rest-api-v1}

Mail Archiver 提供了一个可选的**只读** REST API，用于通过编程方式访问已归档邮件。它专为自动化和集成用例（报告、电子取证导出、监控）而设计，无需通过 Web UI。

其核心目的是让脚本或 AI 代理在**无需暴露邮箱凭据或与邮件提供商通信**的情况下读取已归档邮件。受限的单用户 API 密钥是用于*读取归档*的能力，而不是*成为邮箱*的凭据 —— 它仅继承其所有者的邮箱权限，不授予任何写入路径，并且可以在不触及底层邮件账户的情况下立即撤销。

> ⚠️ **API 默认禁用。** 必须在配置中显式启用（参见 [Enabling the API](#enabling-the-api)）。禁用时，每个 `/api/*` 路由都会返回 `404 Not Found`。

该 API **在设计上是只读的**：它公开了账户、文件夹、邮件搜索、邮件详情和附件下载。它永远不会创建、修改或删除数据。

## 目录 {#table-of-contents}

- [启用 API](#enabling-the-api)
- [身份验证](#authentication)
  - [API key 格式](#api-key-format)
  - [密钥生命周期](#key-lifecycle)
  - [权限模型](#permission-model)
  - [安全原理](#security-rationale)
- [约定](#conventions)
  - [Base URL 与版本控制](#base-url-and-versioning)
  - [分页](#pagination)
  - [错误](#errors)
  - [速率限制](#rate-limiting)
- [端点](#endpoints)
  - [列出账户](#list-accounts)
  - [列出文件夹](#list-folders)
  - [搜索消息](#search-messages)
  - [获取消息](#get-message)
  - [下载附件](#download-attachment)
  - [下载消息 (.eml)](#download-message-eml)
  - [下载原始消息](#download-original-message)
  - [统计数据](#stats)
- [数据类型](#data-types)
- [访问日志](#access-logging)
- [OpenAPI 与 Swagger UI](#openapi-and-swagger-ui)
- [API 参考](API-Reference.md) — 生成的端点与数据类型参考

## 启用 API {#enabling-the-api}

API 在 `appsettings.json` 的 `Api` 章节下进行配置（或使用等效的环境变量，例如 `Api__Enabled=true`）：

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


### 密钥生命周期 {#key-lifecycle}

1. **创建 (Create)** — 用户使用描述性的 `Name` 创建一个密钥，并可选择设置有效期。响应中仅会显示一次明文密钥。
2. **使用 (Use)** — 将密钥作为 bearer token 发送。每个被接受的请求都会刷新密钥的 `LastUsedAt`（写入操作被限制为每 ~5 分钟最多一次，以避免每次调用都进行写入）。
3. **过期 (Expire)** — 如果设置了 `ExpiresAt` 且该时间已过，则该密钥会被拒绝。
4. **撤销 (Revoke)** — 所有者或管理员撤销该密钥（设置 `RevokedAt`）；该密钥会立即且永久被拒绝。

当密钥未被撤销、未过期，**且**其所有者仍处于活跃状态时，该密钥处于**活跃 (active)** 状态。若因上述任何原因导致密钥处于非活跃状态，则会返回 `401`。

### 权限模型 {#permission-model}

API 密钥继承其所有者的确切权限。经过身份验证的主体携带与普通 Web 登录相同的声明，因此所有现有的授权

| Setting | Default | Description |
| --- | --- | --- |
| `Enabled` | `false` | 总开关。当 `false` 时，所有 `/api/*` 路由均返回 `404`，且 OpenAPI 文档 / Swagger UI 不会被映射。 |
| `AllowAttachmentDownloads` | `true` | 当 `false` 时，附件下载端点返回 `403`。关于附件的元数据仍由消息详情端点返回。 |
| `AllowMessageDownloads` | `true` | 当 `false` 时，完整消息端点（`/eml` 和 `/raw`）返回 `403`。完整消息包含其附件，因此该开关和 `AllowAttachmentDownloads` **都**必须为 `true`；关闭其中任何一个都会禁用这两个端点。 |
| `EnableSwaggerUi` | `true` | 当 `true`（且 `Enabled` 为 `true`）时，在 `/apidocs` 挂载 Swagger UI，并在 `/apidocs/spec/v1.json` 挂载 OpenAPI 文档。两者都需要经过身份验证的 **Web (cookie) 会话** —— 它们不属于 `/api/`，且无法通过 API 密钥访问。 |
| `DefaultPageSize` | `20` | 当请求省略 `pageSize` 时使用的分页大小。 |
| `MaxPageSize` | `100` | 上限；较大的 `pageSize` 值将被限制在此值。 |
| `RateLimitPerMinute` | `120` | 每个 API 密钥每分钟的固定窗口请求预算（参见 [Rate limiting](#rate-limiting)）。 |

## Authentication {#authentication}

API 通过作为 bearer token 发送的**每用户 API 密钥**进行身份验证：

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` **仅**接受 API 密钥。浏览器 Cookie 不被认可，因此 API 没有 CSRF 攻击面。身份验证失败会返回带有 `WWW-Authenticate: Bearer` 标头和 [problem+json](#errors) 主体的 `401` —— API **绝不会**重定向到登录页面。

密钥通过 Web UI 中的 **API Keys** 进行自助管理（每个用户管理自己的密钥；管理员可以查看并撤销任何用户的密钥）。

### API key format {#api-key-format}

密钥是字符串 `ma_`，后跟 43 个 URL 安全的 base64 字符，对 32 个随机字节进行编码（256 位熵）：

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

存储：

- **`KeyPrefix`** — 前 ~11 个字符（例如 `ma_8Kf3pQ2`），以明文形式存储。用于在 UI 和日志中识别密钥。不是秘密。
- **`KeyHash`** — 完整密钥的十六进制编码 SHA-256，存储在**唯一索引**下。明文密钥永不存储。

完整密钥在创建时会向用户展示**仅一次**。之后无法检索；丢失的密钥必须撤销并更换。

### Key lifecycle {#key-lifecycle}

1. **Create** — 用户创建一个带有描述性 `Name` 和可选过期时间的密钥。响应会显示一次明文密钥。
2. **Use** — 将密钥作为 bearer token 发送。每个被接受的请求都会刷新密钥的 `LastUsedAt`（写入操作被限制为最多每 ~5 分钟一次，以避免每次调用都进行写入）。
3. **Expire** — 如果设置了 `ExpiresAt` 且已过期，则拒绝该密钥。
4. **Revoke** — 所有者或管理员撤销密钥（设置 `RevokedAt`）；它会被立即且永久地拒绝。

当密钥未被撤销、未过期，**且**其所有者仍处于活跃状态时，该密钥为**活跃**状态。因上述任何原因导致非活跃的密钥都会被拒绝并返回 `401`。

### Permission model {#permission-model}

API 密钥继承其所有者的确切权限。身份验证的主体携带与普通 Web 登录相同的声明，因此所有现有的授权逻辑都保持不变：

- **Administrators** (`IsAdmin`) 可以查看所有邮件账户和所有邮件。
- **Regular users** 只能看到分配给他们的邮件账户 (`UserMailAccount`)。该集合之外的账户和邮件被视为**不存在**：对它们的请求会返回 `404` 而不是 `403`，因此 API 不会泄露调用者无法看到的账户的存在性。

### Security rationale {#security-rationale}

- **2FA bypass.** API 密钥在设计上绕过了双重身份验证 —— 这是非交互式令牌的固有特性（GitHub/GitLab 上的个人访问令牌也是如此）。通过以下方式缓解风险：密钥**默认关闭**、可撤销、可选择过期、受限于用户的权限，并且每次访问都会记录在 [access log](#access-logging) 中。
- **No cookies on `/api`.** 在 `/api` 路由上不接受 Cookie 身份验证，从而消除了 API 的 CSRF 顾虑。
- **Transport.** 在生产环境中始终通过 HTTPS 提供 API 服务，以确保 bearer token 不会在传输线路上泄露。

## 约定 {#conventions}

### 基础 URL 和版本控制 {#base-url-and-versioning}

所有端点均位于 `/api/v1` 之下。版本号包含在路径中，因此未来的破坏性变更可以作为 `/api/v2` 发布，而不会干扰现有客户端。

除非另有说明，否则响应均为 `application/json`（附件下载返回附件自身的 content type）。所有时间戳均为 UTC，符合 ISO 8601 标准 (`2026-06-11T09:30:00Z`)。

### 分页 {#pagination}

列表端点返回一个分页封装：

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` 是从 1 开始的。超出范围的页码将返回一个空的 `items` 数组（而不是错误）。
- `pageSize` 默认为 `Api:DefaultPageSize`，且限制在 `[1, Api:MaxPageSize]` 以内。

### 错误 {#errors}

错误使用 [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
`application/problem+json`：

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Unknown sortBy value 'foo'.",
  "instance": "/api/v1/emails"
}
```

| 状态 | 触发场景 |
| --- | --- |
| `400 Bad Request` | 参数格式错误（例如：无法解析的日期、无效的 `sortBy`）。 |
| `401 Unauthorized` | 密钥缺失、格式错误、已过期、已撤销或存在其他无效情况。包括 `WWW-Authenticate: Bearer`。 |
| `403 Forbidden` | 在 `AllowAttachmentDownloads=false` 时请求下载附件，或在 `AllowMessageDownloads=false` 时请求下载消息。 |
| `404 Not Found` | 资源不存在 **或** 超出调用者允许的账户范围。在 `Api:Enabled=false` 时，每个 `/api/*` 路由也会返回此错误。 |
| `429 Too Many Requests` | 超过速率限制。 |
| `500 Internal Server Error` | 意外的服务器错误 (problem+json，绝非 HTML 错误页面)。 |

### 速率限制 {#rate-limiting}

请求受限于固定窗口策略，即每分钟 `Api:RateLimitPerMinute` 次请求（默认为 `120`），按 API key 前缀进行分区（若无 key 则回退至客户端 IP）。超过配额将返回 `429`。

## 端点 {#endpoints}

> 所有示例均假设 API 已启用，且 `$KEY` 持有有效密钥。

### 列出账户 {#list-accounts}

```
GET /api/v1/accounts
```

返回调用者可见的邮件账户。凭据字段
(IMAP/OAuth username, password, client secret, …) **绝不会**被包含在内。

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

### 列出文件夹 {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

以嵌套结构返回单个账户的文件夹树。如果账户不存在或对调用者不可见，则返回 `404`。

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

### 搜索消息 {#search-messages}

```
GET /api/v1/emails
```

以与 Web UI 相同的功能搜索存档消息。

| 查询参数 | 类型 | 描述 |
| --- | --- | --- |
| `q` | string | 对主题、正文、发件人、收件人、抄送、密送进行全文查询。支持字段语法，例如 `subject:invoice`、`from:alice@example.com`。 |
| `from` | date | 最早发送日期（包含），`YYYY-MM-DD`。 |
| `to` | date | 最晚发送日期（包含），`YYYY-MM-DD`。 |
| `accountId` | int | 限制于一个账户（必须对调用者可见）。 |
| `folder` | string | 通过完整路径限制于一个文件夹。 |
| `direction` | string | `incoming` 或 `outgoing`。 |
| `page` | int | 从 1 开始的页码（默认 `1`）。 |
| `pageSize` | int | 每页结果数（默认 `Api:DefaultPageSize`，限制在 `Api:MaxPageSize`）。 |
| `sortBy` | string | `sentDate`（默认）、`receivedDate`、`subject`、`from`、`to` 中的一个。 |
| `sortOrder` | string | `asc` 或 `desc`（默认 `desc`）。 |

返回 [`EmailSummaryDto`](#emailsummarydto) 的 [分页封装](#pagination)。
记录一条类型为 `Search` 的 [访问日志](#access-logging) 条目。

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

### 获取消息 {#get-message}

```
GET /api/v1/emails/{id}
```

返回包含正文的完整消息。正文使用与 Web UI 相同的回退链：`OriginalBodyHtml` (原始字节) → `BodyUntruncatedHtml` → `HtmlBody`，以及文本正文的等效回退链。如果消息不存在或属于调用者权限之外的账户，则返回 `404`。记录类型为 `Open` 的访问日志条目。

返回 [`EmailDetailDto`](#emaildetaildto)。

### 下载附件 {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

流式传输附件字节及其存储的内容类型和文件名。如果消息或附件不存在或对调用者不可见，则返回 `404`。当 `Api:AllowAttachmentDownloads=false` 时，返回 `403` (problem+json)。
记录类型为 `Download` 的访问日志条目。

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### 下载邮件 (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

将邮件作为 `message/rfc822` 返回，该邮件是根据存档字段重新构建的：
addresses、存储的原始 headers (`Received`, `Authentication-Results`,
`X-Spam-*`, …)、bodies 和 attachments。适用于每一封存档邮件。其重构方式与 Web UI 的账户导出功能相同，因此它与接收到的原始邮件**并非**字节级一致：DKIM 签名将无法通过验证。
如需进行此类操作，请使用 [`/raw`](#download-original-message)。

如果邮件不存在或对调用者不可见，则返回 `404`；当 `Api:AllowMessageDownloads=false` 时，返回 `403` (problem+json)。会记录一条类型为 `Download` 的访问日志。

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### 下载原始邮件 {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

返回接收到的原始邮件 (`message/rfc822`)，用于垃圾邮件分析、
DKIM 验证或取证检查。只有在归档邮件时设置了
`RawMessages:Enabled`，才会存储原始邮件；请参阅
[Original Message Storage Settings](Setup.md#-original-message-storage-settings)。
邮件是否包含原始邮件，可以通过
[`EmailDetailDto`](#emaildetaildto) 中的 `hasRawMessage` 来查看。

| 响应头 | 含义 |
| --- | --- |
| `X-Raw-Message-Sha256` | 返回字节的 SHA-256 哈希值，在邮件存储时记录 |
| `X-Raw-Message-Source` | `imap`、`graph`、`eml-file` (精确副本) 或 `serialized` (由解析器重写，换行符已归一化为 CRLF) |
| `X-Raw-Message-Storage` | `postgres` 或 `filesystem` — 字节的读取来源 (参见 [Storage backend](RawMessages.md#storage-backend)) |

字节可能存储在数据库中或文件中，这取决于邮件归档时的
`RawMessages:Storage`；该端点会针对每条邮件进行解析，因此两种存储方式的处理方式相同。
启用 `RawMessages:FileSystem:VerifyChecksumOnRead` (默认为 `true`) 后，文件在发送前会进行
重新哈希，如果邮件内容不再与记录的校验和匹配，则**不会**提供该邮件。

如果邮件不存在或对调用者不可见，则返回 `404`；
如果邮件存在但没有原始邮件，则返回带有问题标题 `No original message is stored for this email.` 的 `404`；
如果存储的内容不再与归档时记录的校验和匹配，则返回带有标题
`Stored original message failed its checksum.` 的 `500`。当 `Api:AllowMessageDownloads=false` 时，返回 `403` (problem+json)。
记录一条类型为 `Download` 的访问日志。

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

返回对调用者可见的聚合存档统计数据：已存档邮件总数、可见的邮件账户、这些邮件中的附件数量，以及以兆字节为单位的 PostgreSQL 数据库总大小。对于非管理员用户，计数范围仅限于分配给他们的邮件账户（与 Web 仪表板采用相同的范围限制）；管理员可以看到全局总量。

对于非管理员 API 密钥，`databaseSizeInMB` 为空字符串：数据库大小描述的是整个实例，而不是该密钥的范围。邮件、账户和附件始终受限于该密钥可以访问的账户范围。

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

| 字段 | 类型 | 备注 |
| --- | --- | --- |
| `emails` | int | 调用者可见的已归档消息数 |
| `accounts` | int | 调用者可见的邮件账户数 |
| `attachments` | int | 这些可见消息中的附件数 |
| `databaseSizeInMB` | string | 整个 PostgreSQL 数据库的大小（单位：MB）；对于非管理员密钥，返回为空 |

## 数据类型 {#data-types}

### MailAccountDto {#mailaccountdto}

| 字段 | 类型 | 备注 |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

凭据字段永远不会被序列化。

### FolderNodeDto {#foldernodedto}

| 字段 | 类型 | 备注 |
| --- | --- | --- |
| `name` | string | 叶节点名称 |
| `fullPath` | string | 完整路径 |
| `totalCount` | int | 文件夹中的邮件数量 |
| `level` | int | 嵌套深度 (0 = 根目录) |
| `children` | FolderNodeDto[] | 子文件夹 |

### EmailSummaryDto {#emailsummarydto}

| 字段 | 类型 |
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

包含 [`EmailSummaryDto`](#emailsummarydto) 的所有内容以及：

| 字段 | 类型 | 备注 |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | 通过正文回退链解析 |
| `htmlBody` | string | 通过正文回退链解析 |
| `attachments` | AttachmentDto[] | 仅包含元数据 |
| `hasRawMessage` | bool | 当原始邮件已存储且 [`/raw`](#download-original-message) 返回该邮件时，表示 `true` |

### AttachmentDto {#attachmentdto}

| 字段 | 类型 | 备注 |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | 字节数 |

附件字节数通过 [download endpoint](#download-attachment) 获取，
而不是从此 DTO 中获取。

## 访问日志 {#access-logging}

通过 API 进行的每一次数据访问都会记录在与 Web UI 使用相同的访问日志中（可在 **Logs** 下查看），并归属于该密钥的所有者用户：

| 端点 | 记录类型 |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI 和 Swagger UI {#openapi-and-swagger-ui}

当 API 已启用且 `Api:EnableSwaggerUi=true` 时：

- OpenAPI v1 文档在 **`/apidocs/spec/v1.json`** 提供服务。
- Swagger UI 在 **`/apidocs`** 提供服务。

这两个路径都位于 `/api/` 之外，因此受常规 Web (cookie) 身份验证限制 —— 需要已登录的浏览器会话才能查看它们，且无法通过 API key 进行访问。

### 本站上的端点参考 {#endpoint-reference-on-this-site}

端点和数据类型也会作为生成的页面发布在这里：
**[API Reference](API-Reference.md)**。它由 `tools/gen_api_reference.py` 从同一个 OpenAPI 文档渲染而成 —— 副本保存在 `doc/assets/openapi-v1.json` —— 如果页面与规范不同步，CI 将失败，因此该参考文档始终描述实际发布的 API。

!!! note "重新生成"
    该页面是生成的输出。更改 API（或生成器），然后运行
    `python3 tools/gen_api_reference.py` 并提交结果。

    刷新内置的规范本身意味着重新运行 `tools/README.md` 中描述的构建时
    文档生成。
