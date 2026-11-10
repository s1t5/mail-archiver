# MCP Server (Model Context Protocol) {#mcp-server-model-context-protocol}

Mail Archiver 提供了一个可选的 **MCP (Model Context Protocol) server**，通过 Streamable HTTP 传输协议向 AI Agent 暴露归档邮件。它镜像了只读的 [REST API](API.md)：相同的数据、相同的每用户权限模型、相同的访问日志 —— 只是将其封装为 MCP tools 而不是 REST endpoints，以便 LLM agent 可以直接发现并调用它们。

其目的是让 AI agent 在**无需暴露邮箱凭据或与邮件提供商通信**的情况下读取归档邮件。一个受限的每用户 API key 是一种*读取归档*的能力，而不是*成为邮箱*的凭据 —— 它仅继承其所有者的邮箱权限，不授予任何写入路径，并且可以在不影响底层邮件账户的情况下立即撤销。

> ⚠️ **MCP server 默认是禁用的。** 必须在配置中显式启用（参见 [Enabling the MCP server](#enabling-the-mcp-server)）。当禁用时，`/mcp` 端点返回 `404 Not Found`。

MCP server **在设计上是只读的**：它提供用于列出账户、列出文件夹、搜索消息、读取消息和下载附件的 tools。它绝不会创建、修改或删除数据。

## 目录 {#table-of-contents}

- [启用 MCP server](#enabling-the-mcp-server)
- [身份验证](#authentication)
- [传输与端点](#transport-and-endpoint)
- [工具](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
  - [get_email_eml](#get_email_eml)
  - [get_email_raw](#get_email_raw)
- [速率限制](#rate-limiting)
- [访问日志](#access-logging)
- [连接 agent](#connecting-an-agent)
- [互联网暴露的安全指南](#security-guidance-for-internet-exposure)

## 启用 MCP server {#enabling-the-mcp-server}

MCP server 在 `appsettings.json` 的 `Mcp` 部分进行配置（或使用等效的环境变量，例如 `Mcp__Enabled=true`）：

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

| 设置 | 默认值 | 描述 |
| --- | --- | --- |
| `Enabled` | `false` | 主开关。当 `false` 时，`/mcp` 端点返回 `404`。 |
| `AllowAttachmentDownloads` | `true` | 当 `false` 时，`get_attachment` 会返回错误而不是附件字节。`get_email` 仍会返回关于附件的元数据。 |
| `DefaultPageSize` | `20` | 当调用方省略 `pageSize` 时，`search_emails` 使用的页大小。 |
| `MaxResults` | `100` | `search_emails` 页大小的上界；较大的值会被限制。 |
| `MaxAttachmentBytes` | `10000000` | `get_attachment` 将以 base64 形式内联返回的最大附件大小（以字节为单位）；较大的附件将被拒绝。 |
| `AllowMessageDownloads` | `true` | 当 `false` 时，`get_email_eml` 和 `get_email_raw` 会返回错误而不是消息。完整的消息会携带其附件，因此该开关和 `AllowAttachmentDownloads` **都** 必须为 `true`；关闭其中任何一个都会禁用这两个工具。 |
| `MaxMessageBytes` | `10000000` | `get_email_eml` 和 `get_email_raw` 将以内联形式返回的最大消息大小（以字节为单位）；较大的消息将被拒绝。 |
| `RateLimitPerMinute` | `120` | 每个 API key 每分钟的固定窗口请求预算（参见 [Rate limiting](#rate-limiting)）。 |

## 身份验证 {#authentication}

MCP 服务器使用与 **REST API 相同的用户级 API 密钥**进行身份验证，并以 bearer token 的形式发送：

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

API 密钥在 Web UI 的 **API Keys** 下创建和管理。密钥会继承其所有者的确切权限（管理员可以看到所有账户；受限用户只能看到其分配的账户）。密钥可以独立于底层邮件账户进行撤销或过期。有关完整的密钥生命周期和权限模型，请参阅 [REST API 身份验证部分](API.md#authentication) —— MCP 密钥并非独立的凭据存储。

当 MCP 服务器被禁用，或者未提供/提供了无效的 bearer token 时，端点会分别返回 `404` / `401`（它永远不会重定向到登录页面）。

## 传输与端点 {#transport-and-endpoint}

MCP 服务器使用 **Streamable HTTP** 传输，并挂载于：

```
POST /mcp
```

它以 **无状态 (stateless)** 模式运行（每个 HTTP 请求对应一个 JSON-RPC 请求），这是对于不需要服务器向客户端发起请求（如 sampling 或 elicitation）的服务器所推荐的模式。工具通过标准的 MCP `tools/list` 请求进行发现，并通过 `tools/call` 进行调用。

## 工具 {#tools}

### list_accounts {#list_accounts}

列出当前 API 密钥允许访问的邮件账户。返回
`id`, `name`, `emailAddress`, `provider`, `isEnabled` 和 `lastSync`。

**参数：** 无。

**结果：** 账户对象数组。将返回的 `id` 与 `list_folders` 和 `search_emails` 一起使用。

### list_folders {#list_folders}

列出单个邮件账户的文件夹树，并包含每个文件夹的邮件计数。

**参数：**

| 名称 | 类型 | 必填 | 描述 |
| --- | --- | --- | --- |
| `accountId` | integer | 是 | 邮件账户 ID（来自 `list_accounts`）。 |

**结果：** 文件夹对象数组，每个对象包含 `name`、`fullPath`、`totalCount`、
`level` 以及递归的 `children`。如果账户不存在或该密钥未获得授权，则返回空列表。

### search_emails {#search_emails}

通过全文查询、日期范围、账户、文件夹和方向搜索存档邮件。

**参数：**

| 名称 | 类型 | 是否必填 | 描述 |
| --- | --- | --- | --- |
| `q` | string | 否 | 全文搜索查询（主题、正文、发件人/收件人）。 |
| `from` | string (ISO-8601) | 否 | 发送于或晚于 UTC 日期/时间过滤器。 |
| `to` | string (ISO-8601) | 否 | 发送于或早于 UTC 日期/时间过滤器。 |
| `accountId` | integer | 否 | 限制为单个邮件账户 ID。 |
| `folder` | string | 否 | 限制为单个文件夹全路径（例如 `INBOX`）。 |
| `direction` | string | 否 | `incoming` 或 `outgoing`。 |
| `page` | integer | 否 | 从 1 开始的页码（默认为 `1`）。 |
| `pageSize` | integer | 否 | 每页大小；限制在 `[1, MaxResults]` 内。`0` 使用 `DefaultPageSize`。 |
| `sortBy` | string | 否 | `sentdate`（默认）、`receiveddate`、`subject`、`from`、`to`。 |
| `sortOrder` | string | 否 | `asc` 或 `desc`（默认 `desc`）。 |

**结果：** 一个包含 `items`（邮件摘要数组）、`page`、`pageSize`、`totalItems`、`totalPages` 的分页对象。每个摘要携带 `id`、`accountId`、`subject`、`from`、`to`、`sentDate`、`isOutgoing`、`hasAttachments` 和 `folderName`。将返回的 `id` 传递给 `get_email` 以获取完整的正文和附件列表。

### get_email {#get_email}

获取单封已归档电子邮件的完整内容。

**参数：**

| 名称 | 类型 | 必填 | 描述 |
| --- | --- | --- | --- |
| `id` | integer | 是 | 已归档电子邮件的 id（来自 `search_emails`）。 |

**结果：** 一个包含所有摘要字段以及 `cc`、`bcc`、`receivedDate`、`messageId`、`textBody`、`htmlBody` 和 `attachments`（`{ id, fileName, contentType, size }` 数组）以及 `hasRawMessage`（是否可以返回原文的 `get_email_raw`）的单个电子邮件对象。如果电子邮件不存在或该密钥未获得其账户的授权，则返回 `null`。

### get_attachment {#get_attachment}

将单个电子邮件附件下载为 base64 blob。

**参数：**

| 名称 | 类型 | 必填 | 描述 |
| --- | --- | --- | --- |
| `id` | integer | 是 | 附件所属的存档电子邮件 ID。 |
| `attachmentId` | integer | 是 | 附件 ID（来自 `get_email` 的 `attachments`）。 |

**结果：** 包含 `id`、`emailId`、`fileName`、`contentType`、`size` 和 `base64Data`（原始附件字节，base64 编码）的对象。当 `Mcp:AllowAttachmentDownloads=false`、当附件超过 `Mcp:MaxAttachmentBytes` 或当密钥未获得该电子邮件账户授权时，工具将返回错误。

### get_email_eml {#get_email_eml}

返回一个完整的 RFC 5322 消息 (`.eml`) 作为电子邮件，该消息由存档字段重建：存储的标头 (`Received`, `Authentication-Results`, `X-Spam-*`, …)、正文和附件。适用于所有存档的电子邮件，但结果与接收时的字节并不完全一致，因此 DKIM 签名将无法通过验证 — 请使用 [`get_email_raw`](#get_email_raw) 进行验证。镜像 `GET /api/v1/emails/{id}/eml`。

**参数：**

| 名称 | 类型 | 必填 | 描述 |
| --- | --- | --- | --- |
| `id` | integer | yes | 存档的电子邮件 id (来自 `search_emails`)。 |

**结果：** 包含 `emailId`, `kind` (`eml`), `source` (`null`), `sha256`, `size`, `encoding` 和 `content` 的对象。当 `encoding` 为 `utf-8` 时，`content` 是消息文本；当 `encoding` 为 `base64` 时，消息经过 base64 编码（针对非有效 UTF-8 的消息，因此不会更改字节）。当 `Mcp:AllowMessageDownloads=false`、消息超过 `Mcp:MaxMessageBytes` 或密钥未获得该电子邮件账户授权时，工具将返回错误。

### get_email_raw {#get_email_raw}

返回接收到的原始消息原文，用于垃圾邮件分析、DKIM/SPF/ARC 检查或取证。仅在原始消息在归档时已存储（`RawMessages:Enabled`，参见 [Original Message Storage Settings](Setup.md#-original-message-storage-settings)）时可用；`get_email` 将其报告为 `hasRawMessage`。镜像 `GET /api/v1/emails/{id}/raw`。

**参数：**

| 名称 | 类型 | 必填 | 描述 |
| --- | --- | --- | --- |
| `id` | integer | yes | 归档的电子邮件 ID（来自 `search_emails`）。 |

**结果：** 与 `get_email_eml` 相同的对象，带有 `kind` `raw`，并将 `source` 设置为原始捕获方式（`imap`, `graph`, `eml-file` — 精确副本 — 或 `serialized`，由解析器重写并将行尾符归一化为 CRLF）以及归档时记录的 `sha256` 校验和。返回与 `get_email_eml` 相同的错误，且当未为该电子邮件存储原始消息时返回错误。

字节从存储位置（数据库或文件）中读取，具体取决于归档时的 `RawMessages:Storage`（参见 [Storage backend](RawMessages.md#storage-backend)）。使用 `RawMessages:FileSystem:VerifyChecksumOnRead`（默认 `true`）时，文件在返回前会重新进行哈希计算；如果消息内容不再与记录的校验和匹配，则会报错拒绝，而不是返回消息。

## 速率限制 {#rate-limiting}

MCP 服务器拥有其自身的固定窗口速率限制策略 (`"Mcp"`)，该策略按非机密的 API-key 前缀（`ma_...` 令牌的前 11 个字符）进行分区；当不存在 Bearer 令牌时，则回退至客户端 IP。预算通过 `Mcp:RateLimitPerMinute` 进行配置（默认为每分钟 `120`）。当超过限制时，端点将返回带有 `Retry-After` 标头的 `429 Too Many Requests`。这与 REST API 的速率限制策略是相互独立的。

## 访问日志 {#access-logging}

每一次工具调用都会通过与 REST API 相同的 `IAccessLogService` 进行记录，并包含所属用户名及匹配的访问类型（`Search`、`Open`、`Download`）。因此，MCP 活动会与 REST API 和 Web-UI 活动一起显示在 **Logs** 页面上，从而实现统一的审计追踪。

## 连接智能体 {#connecting-an-agent}

MCP 服务器使用标准的 MCP Streamable HTTP 传输协议，因此任何支持 HTTP 的 MCP 客户端都可以进行连接。URL 是应用程序的基础 URL 加上 `/mcp`，例如 `http://localhost:5179/mcp`，API 密钥通过 `Authorization: Bearer ma_...` 请求头提供。

对于通过标准 MCP 客户端 JSON 配置的智能体，条目如下所示：

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

## 互联网暴露的安全指南 {#security-guidance-for-internet-exposure}

MCP 服务器的设计旨在通过使用默认设置并实施下述加固措施，确保在没有明确授予账户的有效用户级 API 密钥的情况下，归档邮件无法离开系统。以下要点是**在将 `/mcp` 暴露于互联网之前必须阅读的内容**。

### 强制性要求（阻止漏洞利用 / 数据外泄） {#mandatory-block-exploitation-data-exfiltration}

1. **仅限 HTTPS。** 在边缘端（反向代理）终止 TLS。通过明文 HTTP 传输的 Bearer API 密钥可能会被窃听并重用。
2. **除非确实需要，否则保持 `Mcp:Enabled=false` 处于禁用状态。** 当禁用时，`/mcp` 会返回 `404` 且没有任何代码路径可达；对于不需要 AI 代理访问的部署，这是安全的默认设置和安全状态。
3. **对端点强制执行速率限制。** `"Mcp"` 策略（`Mcp:RateLimitPerMinute`，默认为 `120`）应用于映射的 `/mcp` 端点。它通过非机密的 API 密钥前缀（`ma_...` 令牌的前 11 个字符）或在不存在 Bearer 令牌时的客户端 IP 进行分区。针对互联网暴露，请**调低 `RateLimitPerMinute`**（例如 `30`–`60`）—— MCP 工具调用比 REST 调用更重，在不受信任的客户端可以访问时，不应让端点保持高预算。
4. **必须谨慎信任反向代理 `X-Forwarded-For`。** 速率限制器会回退到 `RemoteIpAddress`，在代理环境下，除非向 `UseForwardedHeaders` 传递了真实的客户端 IP，否则 `RemoteIpAddress` 即为代理 IP。确保您的代理正确设置了 `X-Forwarded-For`，并且只有您的代理可以设置它；否则攻击者可以伪造速率限制分区。
5. **不要将 `/mcp` 放在 CDN 缓存之后。** MCP 响应是基于请求的 JSON-RPC，绝不能进行缓存——缓存既会破坏协议，也会导致一个用户的结果泄露给另一个用户。在边缘端将 `/mcp` 标记为不可缓存（`Cache-Control: no-store`）。

### API 密钥卫生（阻止未经授权的访问） {#api-key-hygiene-block-unauthorized-access}

6. **为 MCP 代理签发专用的、短寿命的密钥。** 单个 `ma_...` 密钥对 `/api` 和 `/mcp` 同时有效；目前不存在基于协议范围的区分。因此：
   - **为每个代理创建独立的密钥**（不要将报告脚本的密钥重用于外部 LLM 代理）。
   - 始终设置较短的 `ExpiresAt` 并进行轮换；在代理停用时立即撤销。
   - 优先使用由**受限（非管理员）用户**拥有的密钥，该用户仅被分配了代理所需的账户。管理员密钥可以查看*所有*邮件账户；受限密钥可以限制爆炸半径。
7. **像对待密码一样对待 API 密钥。** 它是一种 Bearer 凭据。切勿将其嵌入客户端代码，切勿记录日志，切勿将其提交到代码仓库。将其放入 MCP 客户端的密钥库中，并在怀疑遭到破坏时进行轮换。
8. **在日志页面进行审计。** 每个工具调用都会在所属用户的 `AccessLogs`（`Search`、`Open`、`Download`）中记录。定期检查 MCP 活动是否存在异常模式（如请求量激增、非工作时间读取、大规模下载）。

### 返回数据的信任模型（阻止下游危害） {#trust-model-for-returned-data-block-downstream-harm}

9. **邮件正文和附件是不可信的内容**，其状态与到达邮箱时完全一致。它们可能包含恶意软件、钓鱼链接或活跃的 HTML/JavaScript。
   - `get_email` 返回的是**未经处理且未经过滤的** `htmlBody` 原始数据。在未进行净化（使用 DOMPurify / `HtmlSanitizer` / 沙箱）之前，**不要**将其渲染给人类查看。渲染原始归档 HTML 是一个存储型 XSS 向量。
   - `get_attachment` 返回带有原始 `contentType` 的原始字节。**不要**自动打开或自动执行附件；将 `contentType` 字段视为不可信的元数据，而非安全的指令。
   - `get_email_eml` 和 `get_email_raw` 返回包含每个附件在内的完整消息。相同的规则适用于其每个部分。
10. **不要让代理在未经确认的情况下根据邮件内容执行写入操作。** 恶意邮件可能包含针对消费端 LLM 的提示词注入（prompt-injection）文本。在代理自身的逻辑中强制执行权限和确认——系统提示词（system prompt）并不是安全边界。

### 深度防御建议 {#defense-in-depth-recommendations}

11. **在可行的情况下，在网络层限制 `/mcp`**（例如，在反向代理处仅允许代理主机 IP / VPN 范围），作为 API 密钥认证的补充。这是针对互联网暴露最有效的控制措施。
12. **如果您的代理不需要附件字节，请将 `Mcp:AllowAttachmentDownloads` 降低至 `false`** ——附件外泄是数据量最大的路径。元数据（文件名、内容类型、大小）仍通过 `get_email` 返回。
13. **降低 `Mcp:MaxAttachmentBytes`** 至您的代理实际需要的最小值（默认 `10000000` ≈ 10 MB）。这限制了 API 密钥在每次请求中能拉取的最大单次响应量。对于 `Mcp:MaxMessageBytes` 也是如此，或者当代理不需要完整消息时，将 `Mcp:AllowMessageDownloads` 设置为 `false`。
14. **降低 `Mcp:MaxResults` 和 `Mcp:DefaultPageSize`** 以限制 API 密钥在每次请求中能拉取的最大搜索结果（默认 `100` / `20`）。
15. **在运营层面监控 `AccessLogs` 并限制速率拒绝（`429`）**。来自单个密钥前缀的突然激增是撤销该密钥的信号。
