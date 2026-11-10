# 🛠️ Mail Archiver 设置指南 {#mail-archiver-setup-guide}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

本指南提供了使用 Docker Compose 设置 Mail Archiver 应用程序的详细说明。

## 🛠️ 前提条件 {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 安装步骤 {#installation-steps}

1. 在您的系统中安装前置条件。

2. 创建一个包含以下内容的 `docker-compose.yml` 文件：

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey;

      # Authentication Settings
      - Authentication__Username=admin
      - Authentication__Password=secure123!
      - Authentication__SessionTimeoutMinutes=60
      - Authentication__CookieName=MailArchiverAuth
      - Authentication__CookieSameSite=Lax

      # REST API Settings (read-only, disabled by default)
      - Api__Enabled=false
      - Api__AllowAttachmentDownloads=true
      - Api__AllowMessageDownloads=true
      - Api__EnableSwaggerUi=true
      - Api__DefaultPageSize=20
      - Api__MaxPageSize=100
      - Api__RateLimitPerMinute=120

      # MCP Server Settings (Model Context Protocol for AI agents, read-only, disabled by default)
      - Mcp__Enabled=false
      - Mcp__AllowAttachmentDownloads=true
      - Mcp__DefaultPageSize=20
      - Mcp__MaxResults=100
      - Mcp__MaxAttachmentBytes=10000000
      - Mcp__AllowMessageDownloads=true
      - Mcp__MaxMessageBytes=10000000
      - Mcp__RateLimitPerMinute=120

      # MailSync Settings
      - MailSync__IntervalMinutes=15
      - MailSync__TimeoutMinutes=0
      - MailSync__ConnectionTimeoutSeconds=180
      - MailSync__CommandTimeoutSeconds=300
      - MailSync__AlwaysForceFullSync=false
      - MailSync__IgnoreSelfSignedCert=false
      - MailSync__MaxConcurrentSyncs=1
      - MailSync__InterAccountDelaySeconds=0
      - MailSync__MaxIssuesPerKind=20
      - MailSync__FullSyncIntervalHours=24
      - MailSync__ExcludeSubfolders=false
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Contacts

      # Original Message Storage (complete .eml per email, disabled by default)
      - RawMessages__Enabled=false
      - RawMessages__Storage=postgres
      - RawMessages__MaxMessageSizeMb=50
      - RawMessages__IncludeGraph=true
      - RawMessages__FileSystem__BasePath=RawMessages
      - RawMessages__FileSystem__ShardDepth=2
      - RawMessages__FileSystem__VerifyChecksumOnRead=true

      # BatchRestore Settings
      - BatchRestore__AsyncThreshold=50
      - BatchRestore__MaxSyncEmails=150
      - BatchRestore__MaxAsyncEmails=50000
      - BatchRestore__SessionTimeoutMinutes=30
      - BatchRestore__DefaultBatchSize=50

      # Tenant Management Settings
      - TenantManagement__MaxSelectedMailboxes=1000

      # Mailcow Settings
      - Mailcow__MaxSelectedMailboxes=1000

      # BatchOperation Settings
      - BatchOperation__BatchSize=50
      - BatchOperation__PauseBetweenEmailsMs=50
      - BatchOperation__PauseBetweenBatchesMs=250

      # Bandwidth Tracking Settings (for IMAP rate limit handling)
      - BandwidthTracking__Enabled=false
      - BandwidthTracking__DailyLimitMb=25000
      - BandwidthTracking__WarningThresholdPercent=80
      - BandwidthTracking__PauseHoursOnLimit=24
      - BandwidthTracking__TrackUploadBytes=false

      # Selection Settings
      - Selection__MaxSelectableEmails=250

      # View Settings (Privacy & Display)
      - View__DefaultToPlainText=true
      - View__BlockExternalResources=false

      # Dashboard Settings (statistics cache, counter parts, chart periods)
      - Dashboard__CacheSeconds=60
      - Dashboard__RefreshIntervalMinutes=15
      - Dashboard__ShowDirectionSplits=true
      - Dashboard__SelectablePeriods=true

      # Jobs Settings (background jobs page)
      - Jobs__RefreshSeconds=30

      # Npgsql Settings
      - Npgsql__CommandTimeout=900

      # Upload Settings for MBox and EML files
      - Upload__MaxFileSizeGB=10
      - Upload__KeepAliveTimeoutHours=4
      - Upload__RequestHeadersTimeoutHours=2

      # Local Import Settings (for CLI imports from mounted volumes)
      - LocalImport__AllowedPaths__0=/data/import

      # CSV Import Settings (for bulk IMAP account import)
      - CsvImport__MaxRows=5000
      - CsvImport__MaxFileSizeBytes=10000000

      # Deletion Policy Settings (Optional - controls whether email deletion is allowed)
      - DeletionPolicy__DeletionAllowed=true

      # TimeZone Settings
      - TimeZone__DisplayTimeZoneId=Etc/UCT

      # Database Maintenance Settings (Optional)
      - DatabaseMaintenance__Enabled=false
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30

      # Attachment Deduplication Settings (Optional - feature is always on)
      - AttachmentDeduplication__BatchSize=200
      - AttachmentDeduplication__DelayBetweenBatchesMs=0
      - AttachmentDeduplication__StartupDelaySeconds=20
      - AttachmentDeduplication__OrphanCleanupIntervalHours=12
      - AttachmentDeduplication__CommandTimeoutSeconds=300

      # Account Storage Settings (Optional - per-account storage display)
      - AccountStorage__Enabled=true
      - AccountStorage__DailyExecutionTime=02:30
      - AccountStorage__BackfillDelayMs=5000
      - AccountStorage__RefreshBatchDelayMs=1000
      - AccountStorage__CommandTimeoutSeconds=300

      # ReleaseNotes Settings (Version Update Splash Screen)
      - ReleaseNotes__Enabled=true
      - ReleaseNotes__UpdateHintEnabled=true
      - ReleaseNotes__UpdateCheckIntervalHours=6

      # Logging Settings (Optional - defaults to Information level)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning

      # Security Settings
      - AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com

      # OIDC Configuration (see OIDC_Implementation.md for detailed setup)
      - OAuth__Enabled=true
      - OAuth__Authority=https://example.com
      - OAuth__ClientId=YOUR-CLIENT-ID
      - OAuth__ClientSecret=YOUR-CLIENT-SECRET
      - OAuth__DisplayName=PocketID SSO
      - OAuth__ClientScopes__0=openid
      - OAuth__ClientScopes__1=profile
      - OAuth__ClientScopes__2=email
      - OAuth__DisablePasswordLogin=false
      - OAuth__AutoRedirect=false
      - OAuth__AutoApproveUsers=false
      - OAuth__AdminEmails__0=admin@example.com
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      # Uncomment the following line to mount a directory for local file imports
      # (also configure LocalImport__AllowedPaths__0=/data/import in environment variables)
      # - /path/to/your/mbox/files:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
      # Only with RawMessages__Storage=filesystem: keep the original messages outside the
      # container, otherwise they are lost when the container is recreated.
      # - ./raw-messages:/app/RawMessages
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

networks:
  postgres:
```

3. 编辑 `docker-compose.yml` 中的数据库配置，并在 `POSTGRES_PASSWORD` 变量和 `ConnectionString` 中设置安全密码。

4. 如果您想使用身份验证（强烈建议），请定义用于管理员用户的 `Authentication__Username` 和 `Authentication__Password`。

5. 调整 `TimeZone__DisplayTimeZoneId` 环境变量以匹配您偏好的时区（默认为 "Etc/UCT"）。您可以使用任何 IANA 时区标识符（例如 "Europe/Berlin"、"Asia/Tokyo"）。

6. （可选）配置 `Logging__LogLevel` 环境变量以控制应用程序日志的详细程度。有关可用选项，请参阅下方的“日志设置”部分。

7. 配置您选择的带有 https 和身份验证的反向代理，以确保访问应用程序的安全。

> ⚠️ **注意**：应用程序本身不通过 https 提供加密访问！必须通过反向代理进行设置！

8. 首次启动容器：
```bash
docker compose up -d
```

9. 重启容器：
```bash
docker compose restart
```

10. 访问应用程序

11. 使用您定义的凭据登录并添加您的第一个电子邮件账户：
   - 导航至 "Email Accounts" 部分
   - 点击 "New Account"
   - 输入您的服务器详情和凭据
   - 保存并开始归档！
   - 如果需要，可以创建其他用户并分配账户。

## 📚 环境变量说明 {#environment-variable-explanations}

### 🗄️ 数据库连接 {#database-connection}
- `ConnectionStrings__DefaultConnection`: 连接到 PostgreSQL 数据库的连接字符串。根据需要修改 `Host`、`Database`、`Username` 和 `Password` 的值。

### 🔐 Authentication Settings {#authentication-settings}
- `Authentication__Username`：管理员账户的用户名。
- `Authentication__Password`：管理员账户的密码。
- `Authentication__SessionTimeoutMinutes`：会话超时时间（分钟）。这适用于非持久性登录 Cookie；在登录表单上勾选 **Remember Me** 的用户将获得有效期为固定 7 天的持久性 Cookie。参见 [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox)。
- `Authentication__CookieName`：身份验证 Cookie 的名称。
- `Authentication__CookieSameSite`：配置身份验证、会话和 CSRF 防护 Cookie 的 SameSite 属性。有效值包括：
  - `Strict`（默认）：最高安全性。Cookie 仅在同站请求中发送。从外部链接导航到应用程序（例如，从另一个网站点击链接）时，可能会导致现有会话无法被识别。
  - `Lax`：推荐在配合反向代理使用时配置。Cookie 会随顶级导航和同站请求一起发送，允许用户通过外部链接访问应用程序，同时保持对 POST 请求的 CSRF 防护。
  - `None`：Cookie 会随所有请求发送。需要 HTTPS 和 `Secure` 属性。仅在您有特定的跨站需求并了解安全影响时使用此项。

### 🔌 REST API Settings {#rest-api-settings}
可选的只读 REST API **默认禁用**。请参阅 [REST API guide](API.md) 获取完整参考。
- `Api__Enabled`: 只读 REST API 的主开关（默认 `false`）。当 `false` 时，所有 `/api/*` 路由均返回 `404`。
- `Api__AllowAttachmentDownloads`: 允许通过 API 下载附件字节（默认 `true`）。当 `false` 时，附件端点返回 `403`。
- `Api__AllowMessageDownloads`: 允许通过 API 下载完整消息，包括重建的 `/eml` 和原始的 `/raw`（默认 `true`）。当 `false` 时，两个端点均返回 `403`。完整消息携带其附件，因此该开关与 `Api__AllowAttachmentDownloads` **都必须** 设为 `true`；关闭其中任何一个都会禁用这两个端点。
- `Api__EnableSwaggerUi`: 在 `/apidocs` 处公开 Swagger UI，并在 `/apidocs/spec/v1.json` 处公开 OpenAPI 文档（默认 `true`）。两者均需要已登录的浏览器会话。
- `Api__DefaultPageSize`: 未指定时，列表端点的默认分页大小（默认 `20`）。
- `Api__MaxPageSize`: 允许的最大分页大小；较大的请求将被限制（默认 `100`）。
- `Api__RateLimitPerMinute`: 每个 API 密钥每分钟的固定窗口请求预算（默认 `120`）。

### 🤖 MCP Server Settings {#mcp-server-settings}
可选的 MCP (Model Context Protocol) 服务器通过 Streamable HTTP 传输层向 AI agent 提供与 REST API 相同的只读邮件访问权限。它**默认是禁用的**。有关完整参考，请参阅 [MCP guide](MCP.md)。MCP 复用与 REST API 完全相同的 API 密钥 (`ma_...` bearer tokens) 以及相同的每用户账户访问范围。
- `Mcp__Enabled`: MCP 服务器的主开关 (默认 `false`)。当 `false` 时，`/mcp` 端点返回 `404`。
- `Mcp__AllowAttachmentDownloads`: 允许 `get_attachment` 工具返回附件字节 (默认 `true`)。当 `false` 时，该工具会返回错误。
- `Mcp__DefaultPageSize`: 当调用方省略 `pageSize` 时，`search_emails` 使用的默认分页大小 (默认 `20`)。
- `Mcp__MaxResults`: `search_emails` 分页大小的上界；较大的值会被限制 (默认 `100`)。
- `Mcp__MaxAttachmentBytes`: `get_attachment` 工具将以 base64 形式内联返回的最大附件大小 (以字节为单位)；较大的附件将被拒绝 (默认 `10000000` ≈ 10 MB)。
- `Mcp__AllowMessageDownloads`: 允许 `get_email_eml` 和 `get_email_raw` 工具返回完整消息 (默认 `true`)。当 `false` 时，工具会返回错误。完整消息包含其附件，因此该开关和 `Mcp__AllowAttachmentDownloads` **都**必须为 `true`；关闭其中任何一个都会禁用这两个工具。
- `Mcp__MaxMessageBytes`: `get_email_eml` 和 `get_email_raw` 工具将内联返回的最大消息大小 (以字节为单位)；较大的消息将被拒绝 (默认 `10000000` ≈ 10 MB)。
- `Mcp__RateLimitPerMinute`: 每个 API 密钥每分钟的固定窗口请求预算 (默认 `120`)。

### 📨 MailSync Settings {#mailsync-settings}
- `MailSync__IntervalMinutes`: 电子邮件同步之间的间隔分钟数。这是全局默认值；每个账户都可以在 Create/Edit 页面单独覆盖它（留空则使用此默认值）。
- `MailSync__FullSyncIntervalHours`: 可选的全局默认值，用于自动全量重新同步，单位为小时。若未设置（默认情况），除非在 Create/Edit 页面设置了针对单个账户的 `FullSyncIntervalHours` 值，否则不会运行自动全量同步。针对账户的设置会覆盖此全局默认值。
- `MailSync__TimeoutMinutes`: 单个账户同步的超时分钟数。运行时间过长的同步会在下一个消息边界处停止，保留其检查点并在下次运行时恢复；该任务会被报告为 `Timed Out` 且 `LastSync` 不会推进。此设置仅适用于计划同步，从 UI 发起的同步（手动同步或全量重新同步）不会有超时限制。`0`（默认值）或任何非正值表示无超时。如果您同步大型邮箱，请在升级前检查该值。参见 [Synchronization.md](Synchronization.md#-stopping-a-sync-early)。
- `MailSync__ConnectionTimeoutSeconds`: IMAP 连接的连接超时时间（秒）。
- `MailSync__CommandTimeoutSeconds`: IMAP 命令的命令超时时间（秒）。
- `MailSync__AlwaysForceFullSync`: 是否始终强制进行全量同步 (true/false)。
- `MailSync__IgnoreSelfSignedCert`: 是否忽略自签名证书 (true/false)。
- `MailSync__MaxConcurrentSyncs`: 可同时运行的账户同步数量。默认 `1`（顺序执行，向后兼容）。调度器会在插槽空闲时立即填补，而不是等待整个批次完成，因此一个缓慢的邮箱不再会阻塞其他邮箱。增加此值可并发同步多个账户 —— 请注意服务商的速率限制和本地资源使用情况。参见 [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled)。
- `MailSync__InterAccountDelaySeconds`: 在每个账户同步任务结束时应用的可选交错延迟（秒）。默认 `0`（无延迟）。有助于避免 `MaxConcurrentSyncs > 1` 时的爆发式启动。
- `MailSync__MaxIssuesPerKind`: 同步任务为账户页面记录的每种问题的数量。失败的文件夹、缺失的文件夹和失败的消息是分别计数的，因此某一类问题的激增不会挤掉其他类别的显示；超出预算的内容仅计数而不保留详情。默认 `20`。`0` 会关闭详情显示，仅保留计数器。参见 [Synchronization.md](Synchronization.md#-observing-the-sync)。
- `MailSync__GlobalExcludedFolders__<n>`: 除了每个账户自身的排除文件夹列表外，**所有**账户都排除的文件夹。默认为空，因此现有的设置不受影响。这两个列表是**累加**的 —— 当文件夹匹配其中任何一个时，都会被跳过 —— 并且两者都使用相同的匹配规则：完整路径精确匹配、文件夹名称精确匹配、路径后缀匹配（因此 `Drafts` 也匹配 `INBOX/Drafts` 和 `INBOX.Drafts`），以及当 `MailSync__ExcludeSubfolders` 开启时，匹配该条目下方的所有内容。匹配不区分大小写。当从同一服务器导入多个邮箱时非常有用，否则需要为每个账户维护一个完全相同的排除列表。默认情况下不排除任何文件夹；哪些名称值得列出取决于服务器及其语言。以下是带有日历和联系人文件夹的邮箱树示例：
  ```yaml
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Calendars
      - MailSync__GlobalExcludedFolders__2=Contacts
      - MailSync__GlobalExcludedFolders__3=AddressBook
      - MailSync__GlobalExcludedFolders__4=Tasks
      - MailSync__GlobalExcludedFolders__5=TaskList
      - MailSync__GlobalExcludedFolders__6=Notes
      - MailSync__GlobalExcludedFolders__7=NoteList
      - MailSync__GlobalExcludedFolders__8=Journal
  ```
- `MailSync__ExcludeSubfolders`: 排除条目是否也涵盖其名称下方的文件夹。同样适用于针对账户的列表和安装范围内的列表，并适用于两种提供商。默认 `false`，与该选项存在之前的行为一致：条目 `Deleted Items` 仅匹配该文件夹，通过命名其根目录及其下方的每个文件夹来将整个树排除在存档之外。设置为 `true` 则使条目同时涵盖其下方的树 —— 这样条目 `Deleted Items` 也会将 `Deleted Items/2024` 和该分支的其余部分排除在存档之外。规则锚定在路径分隔符上，因此它处理的是整个文件夹，而不是名称的一部分：`Archive` 涵盖 `Archive` 和 `Archive/Team`，但不会影响名为 `Team Archive Review` 的邮件文件夹。`.` 也被视为分隔符，其处理方式与路径后缀规则相同。

### 📧 原始邮件存储设置 {#original-message-storage-settings}

默认情况下，归档仅保留电子邮件的解析部分，因此导出的 `.eml` 是一个重构版本：传输编码、MIME 边界和标头折叠与接收时的原始消息不同，且 DKIM 签名将不再通过验证。通过 `RawMessages:Enabled`，完整的原始消息也会被存储。

| 参数 | 默认值 | 用途 |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | 存储每封新归档邮件的原始版本。 |
| `RawMessages__Storage` | `postgres` | 字节存储位置：`postgres` 或 `filesystem`。 |
| `RawMessages__MaxMessageSizeMb` | `50` | 较大的消息将不带原始版本进行归档。 `0` = 无限制。 |
| `RawMessages__IncludeGraph` | `true` | 同时为 Microsoft 365 (Graph) 账户存储原始版本。 |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | 消息文件目录。必须指向 Docker 中的挂载卷。 |
| `RawMessages__FileSystem__ShardDepth` | `2` | 邮件 ID 分散的目录层级。 |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | 每次下载时验证 SHA-256。 |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | 删除没有任何邮件引用的消息文件。 |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | 该清理任务运行的频率。 |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | 早于此时间的文件的永远不会被删除。 |

!!! warning "文件系统后端需要挂载卷"
    使用 `RawMessages__Storage=filesystem` 时，消息文件存储在数据库之外。
    如果没有绑定挂载（bind mount），它们将存在于容器的可写层中，并在下次重建容器时丢失，而数据库行则会保留。请务必将该目录与数据库转储（dump）一起备份。

有关完整描述，请参阅 **[Original Message Storage](RawMessages.md)**：包括原始消息的提供方式、存储后端、如何使用 `--migrate-raw-storage` 在它们之间移动现有消息，以及需要备份的内容。

### 📤 BatchRestore 设置 {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: 触发异步处理的邮件数量。
- `BatchRestore__MaxSyncEmails`: 同步处理的最大邮件数量。
- `BatchRestore__MaxAsyncEmails`: 异步处理的最大邮件数量。
- `BatchRestore__SessionTimeoutMinutes`: 批量恢复的会话超时时间（分钟）。
- `BatchRestore__DefaultBatchSize`: 邮件操作的默认批次大小。

### 🏢 租户管理设置 {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`：在单次租户管理操作中可以添加的最大邮箱数量。默认值为 `1000`。对于超大型租户，可以增加此值；或者降低此值以防止意外的大规模导入。当超过限制时，操作将被拒绝并返回验证错误，且不会创建任何账户。详情请参阅 [M365 Tenant Import Guide](M365TenantImport.md)。

### 🐄 Mailcow 设置 {#mailcow-settings}

- `Mailcow__MaxSelectedMailboxes`: 单次 Mailcow 激活运行中可配置的最大邮箱数量。默认值为 `1000`。降低该值可防止意外的大规模配置，对于大型实例可提高该值。当超过限制时，将不会创建任何账户。详情请参阅 [Mailcow Instances](Mailcow.md)。
- Mailcow 集成还需要 `Security__CredentialEncryptionKey`（参见 [Security Settings](#-security-settings)）。在禁用凭据加密时，连接实例和激活邮箱都会被拒绝，因此 API 密钥和生成的应用密码永远不会以明文形式存储。
- 需要能够通过 HTTPS 访问 Mailcow API 以及通过 TLS 访问 IMAP 主机（通常为端口 `993`）。

### 📦 BatchOperation 设置 {#batchoperation-settings}
- `BatchOperation__BatchSize`: 邮件操作的批次大小。
- `BatchOperation__PauseBetweenEmailsMs`: 每封邮件之间的暂停时间（毫秒）。
- `BatchOperation__PauseBetweenBatchesMs`: 每批次之间的暂停时间（毫秒）。

### 📤 卸载设置 (Offload Settings) {#offload-settings}
[按日期窗口卸载](Offload.md)的设置。所有默认值都还原了该功能存在之前的应用行为，因此未配置此部分的安装不会受到影响。
- `Offload__MaxConcurrentJobs`: 同时可以运行多少个恢复或卸载作业。默认值为 `1`，这保持了作业队列一直使用的严格串行处理方式。无论此值如何，每个目标邮箱最多只能运行一个作业。
- `Offload__PrefetchMaxMessages`: 用于重复检测的目标邮箱中索引消息数量的上界。默认值为 `500000`。超过此值后，检查将缩小到单个文件夹，并记录范围已缩小。
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: 永远不会被卸载的源文件夹，在重命名之前进行匹配并覆盖子文件夹。默认为空。
- `Offload__FolderRenameMap__<SourceFolder>`: 重写源文件夹路径的前导段，例如 `Offload__FolderRenameMap__Sent Items=Sent`。默认为空。
- `Offload__MarkAsSeen`: 追加的邮件是否标记为已读。默认值为 `true`，与现有的恢复行为一致。

两个文件夹设置都是故意为空的：在未被要求的情况下重写或丢弃文件夹会令任何已经在执行恢复路径的用户感到意外。因此，在没有任何配置的情况下运行会迁移所有内容（包括垃圾邮件文件夹），并在目标自身的特殊文件夹旁边创建第二套特殊文件夹。

### 📊 带宽追踪设置 {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: 启用或禁用用于 IMAP 速率限制处理的带宽追踪 (true/false)。默认值为 `false`。启用后，系统会追踪每个账户的带宽使用情况，并在达到提供商限制时暂停同步。有关详细信息，请参阅 [Rate Limit Handling](RateLimitHandling.md)。
- `BandwidthTracking__DailyLimitMb`: 每个账户每天的下载限制（单位：MB）。默认值为 `25000` (25 GB)。对于有带宽限制的提供商，请将其设置为与其速率限制相匹配的值（例如，对于每日限制约为 2500 MB 的提供商，设置为 `2500`）。当达到此限制时，系统将暂停同步。
- `BandwidthTracking__WarningThresholdPercent`: 记录警告消息的每日限制百分比。默认值为 `80`。当带宽使用达到此百分比时，系统会记录警告，以帮助监控接近限制的情况。
- `BandwidthTracking__PauseHoursOnLimit`: 达到每日限制时暂停同步的小时数。默认值为 `24`。在此期间结束后，限制标志会自动清除并恢复同步。
- `BandwidthTracking__TrackUploadBytes`: 是否也追踪上传带宽 (true/false)。默认值为 `false`。大多数 IMAP 提供商仅限制下载，因此通常不需要此项。

### 🎯 选择设置 {#selection-settings}
- `Selection__MaxSelectableEmails`: 一次最多可以选择的电子邮件数量。

### 👁️ 查看设置 (隐私与显示) {#view-settings-privacy-display}
- `View__DefaultToPlainText`：控制用于隐私和追踪防护的默认电子邮件查看模式 (true/false)。默认值为 `false`。
  - 当设置为 `true` 时：电子邮件默认以纯文本视图打开，防止自动加载追踪像素、外部图像和 Web 信标。这对于关注电子邮件追踪和隐私的用户是推荐的。
  - 当设置为 `false` 时：电子邮件默认以 HTML 视图打开，显示带有图像和样式的格式化电子邮件。
  - 用户可以使用电子邮件详情页面中的按钮在 HTML 和纯文本视图之间进行切换（仅在两种格式都可用时可见）。
  - “全视图”链接会遵循当前选择的查看模式。

- `View__BlockExternalResources`：在 HTML 电子邮件视图中拦截外部资源（远程图像、外部 CSS、外部脚本、Web 字体等），以防止追踪并提高隐私性 (true/false)。默认值为 `false`。
  - 当设置为 `true` 时：在显示 HTML 电子邮件时，外部资源会被过滤掉。仅显示内联内容和 Data URI（包括通过 `cid:` 引用的内联附件）。
  - 当设置为 `false` 时：HTML 电子邮件将显示其所有原始的外部资源。
  - **重要提示**：此设置仅影响电子邮件的**显示**。归档的电子邮件会完整且不加修改地存储在数据库中，保留所有原始内容。
  - 被拦截的资源包括：
    - 远程图像（追踪像素、托管在服务器上的外部图像）
    - 外部 CSS 样式表
    - 通过 @font-face 使用的外部字体
    - 通过 @import 使用的外部 CSS 导入
    - 外部背景图像
  - 允许的资源：
    - 作为 Data: URIs 嵌入的内联图像
    - 通过 cid: URIs 引用的内联附件
    - 内联 CSS 样式和 style 标签
  - 此设置独立于 `DefaultToPlainText` 工作，并在查看 HTML 电子邮件时提供额外的隐私保护层。

### 📊 Dashboard Settings {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: 计数卡片是否携带其入站和出站部分，以及账户卡片是否携带不同域的数量。默认值为 `true`。设置为 `false` 后，这些数字将不再进行计算而是直接显示：计数器读取纯计数，且附件计数停止与挂载的邮件进行关联，这正是这些部分所增加的唯一查询。
- `Dashboard__SelectablePeriods`: 仪表板图表是否提供分辨率、周期以及用于移动它们的箭头。默认值为 `true`。设置为 `false` 后，图表将按月显示过去十二个月以及整个存档的发送者，图表端点将表现得如同不存在该功能一样，且不会查找最早的发送日期。
- `Dashboard__CacheSeconds`: 计算出的仪表板统计数据（包含入站和出站拆分的总量、每账户计数、图表序列、热门发送者、最近邮件、数据库大小）在服务器内存缓存中保留的时间。默认值为 `60` 秒。设置为 `0` 可禁用缓存并始终重新计算统计数据。每个分辨率和周期的图表选择都会独立缓存，因此在不同选择之间切换的仪表板，每个选择都会持有一个条目。较高的值可以减少大型环境中的数据库负载，但代价是数据可能更陈旧。同步状态徽章、存储值和最近邮件列表始终实时获取，不受此缓存影响。
- `Dashboard__RefreshIntervalMinutes`: `DashboardStatsRefreshService` 后台服务重新计算高开销仪表板聚合数据（包含入站和出站拆分的总量、每账户计数、账户域数量、带有热门发送者的默认图表序列、数据库大小）并写入 `DashboardStatsCache` 数据库表的时间间隔。在拥有数百万封邮件的安装环境中，仪表板会读取这些预处理后的值，而不是在请求路径中聚合整个存档，从而将约 10 秒的首次加载时间缩短至毫秒级。图表端点从同一行返回默认选择（一年内的月份、入站发送者、当前周期），因此返回该选择时会显示首次渲染时的数字；其他所有选择都会在请求时计算，并保存在上述内存缓存中。默认值为 `15` 分钟。设置为 `0` 可禁用后台刷新；此时仪表板将再次实时计算统计数据（仍应用上述内存缓存）。显示的数值可能会比实际存档状态滞后最多一个配置的间隔。如果计算出的行与当前的 `ShowDirectionSplits` 或 `SelectablePeriods` 值不同，则不会被使用；仪表板将持续实时计算，直到下一次刷新。

### 🔄 Jobs Settings {#jobs-settings}
- `Jobs__RefreshSeconds`: 当浏览器标签页打开时，后台作业页面自动刷新的频率。默认值为 `30` 秒。设置为 `0` 可关闭自动刷新，改为手动刷新。每次刷新都会从所有八个作业源重新构建页面，因此在拥有多个账户的安装环境中，较长的间隔可以降低负载，因为负载会随着页面上打开的每个标签页而倍增。

### 🗃️ Npgsql Settings {#npgsql-settings}
- `Npgsql__CommandTimeout`: 数据库命令的超时时间（秒）。

### 📥 上传设置 {#upload-settings}
- `Upload__MaxFileSizeGB`: 上传文件的最大大小（单位：GB）。
- `Upload__KeepAliveTimeoutHours`: 上传保持活跃的超时时间（单位：小时）。
- `Upload__RequestHeadersTimeoutHours`: 请求标头的超时时间（单位：小时）。

### 📂 本地导入设置 {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1` 等：CLI 导入命令（`--import-mbox`, `--import-eml`）允许读取文件的本地目录白名单。每个条目都是容器内的路径。您必须使用 Docker volumes 将您的导入文件挂载到这些目录之一。默认值：`/data/import`（自动设置为 `/app/uploads` 作为回退方案）。
  - 当使用 `docker exec` 运行导入命令时，通过 `--file` 提供的文件路径必须位于这些允许的路径之一。
  - 此安全措施可防止 CLI 命令进行任意文件系统访问。
  - 可以为不同的导入源配置多个路径。
  - 详见 [CLI commands](CLI.md) 获取详细的使用说明。

### 📄 CSV Import Settings {#csv-import-settings}

- `CsvImport__MaxRows`: 单次批量导入中处理的 CSV 行（邮箱）最大数量。默认值为 `5000`。对于大规模部署，请增加此值；若要限制单次导入运行对数据库负载的影响，请降低此值。
- `CsvImport__MaxFileSizeBytes`: 上传的 CSV 文件允许的最大尺寸（以字节为单位）。默认值为 `10000000` (10 MB)。如果需要，请调整此值以匹配您的上传限制。
- 有关通过 CSV 进行批量 IMAP 账户导入的详细使用说明，请参阅 [Account Import Guide](Account-Import.md)。

### 📤 审计导出设置 {#audit-export-settings}
审计数据导出页面的设置（仅限管理员，可从 Logs 页面访问）。导出功能会从现有存档中生成一个包含表格化海量数据（INDEX.XML + CSV 表格 + DTD）的 ZIP 包，供外部审计工具使用。使用详情请参阅 [Audit Data Export Guide](AuditExport.md)。
- `AuditExport__DataSupplierName`: “Data supplier name” 表单字段的默认值，用于在导出的索引文件中标识您的组织。默认值：空。
- `AuditExport__DataSupplierLocation`: “Data supplier location” 表单字段（例如公司所在地）的默认值。默认值：空。
- `AuditExport__Comment`: “Comment” 表单字段的默认值，作为自由文本包含在导出的索引文件中。默认值：空。
- `AuditExport__OutputDirectory`: 生成的 ZIP 文件存储的目录。相对路径相对于 app content root 进行解析。默认值：`exports/audit`。
- `AuditExport__RetentionDays`: 完成导出的文件在经过多少天后会被每日清理程序删除。默认值：`30`。
- `AuditExport__MaxRangeYears`: 导出周期开始日期与结束日期之间允许的最大跨度。默认值：`10`。
- 表单字段会根据这些默认值进行预填，但可以在每次导出时进行编辑。
- 每次导出都会向访问日志（类型为 "Audit Data Export"）写入一条开始条目和一条结果条目，因此无需任何 DB schema 变更即可实现历史记录的修订安全。

### 🔒 Deletion Policy Settings {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: 控制是否允许手动删除已归档的电子邮件 (true/false)。默认值为 `true`。当设置为 `false` 时：
  - 启动时，所有已归档的电子邮件都将通过数据库合规性触发器被锁定 (`IsLocked = true`)，防止在数据库层面进行任何修改或删除。
  - 应用层级将阻止手动删除（单个或批量），并显示错误消息。
  - 列默认值会进行调整，使得新导入的电子邮件也会被锁定。
  - 当前策略状态会在每次启动时记录到 AccessLogs 表中（在 Logs 页面上以 "Deletion Policy" 条目显示），以供审计。
  - 本地保留删除不受限制：属于配置的保留期内的电子邮件仍会被删除（在保留处理过程中的删除前会立即解锁）。
- `DeletionPolicy__MaxBulkDeleteEmails`: 基于过滤器的批量删除（*Delete all matches*）的上界。默认值为 `100000`。如果过滤器匹配了过多的电子邮件，将被拒绝并提示要求缩小范围；任务绝不会在静默状态下截断。被保留策略锁定的电子邮件会被跳过并在任务状态中报告，而不会被解锁。任务在启动时会重新检查此限制和 `DeletionPolicy__DeletionAllowed`，因此在策略变更前排队的任务会停止运行，而不是使用旧设置运行。
- **不可变性保护：** 当 `IsLocked = true` 时，数据库合规性触发器 (`prevent_locked_email_changes`) 会阻止对电子邮件行的任何修改 —— 所有列都受到保护，而不仅仅是固定的字段列表。唯一的例外列是 `IsLocked` 本身（以便在保留删除和启动策略应用时仍能进行解锁）以及 `FolderName`（以便在服务器端移动电子邮件时，IMAP 同步可以更新文件夹）。该保护是与列无关的（基于 JSONB 的比较），并会自动涵盖未来新增的模式。

### 🕐 时区设置 {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: 用于在 UI 中显示电子邮件时间戳的时区。使用 IANA 时区标识符（例如 "Europe/Berlin", "Asia/Tokyo"）。为了向后兼容，默认值为 "Etc/UCT"。在导入电子邮件时，时间戳将转换为此时区以进行展示。

### 🎉 ReleaseNotes Settings (版本更新启动界面) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: 此实例报告的版本字符串。它将与 GitHub release 标签进行匹配，以决定是否存在更新的版本。当为空时，更新日志将回退到编译后的程序集版本，且更新提示将保持关闭状态。
- `ReleaseNotes__Enabled`: 启用或禁用版本更新启动界面 (true/false)。默认值为 `true`。启用后，管理员在应用程序更新后会看到一个一次性的更新日志模态框，显示从 GitHub Releases 获取的当前版本的发布说明。每位管理员都可以关闭该模态框，且它仅会在新版本发布时再次出现。设置为 `false` 可完全禁用此功能，包括更新提示。
- `ReleaseNotes__UpdateHintEnabled`: 当 GitHub 发布了比当前安装版本更新的稳定版本时，在右下角向管理员显示一个小的 toast 提示 (true/false)。默认值为 `true`。该 toast 会链接到该版本，并在浏览器中按版本进行忽略。草稿 (Drafts) 和预发布版本 (prereleases) 会被忽略。此功能需要 `ReleaseNotes__AppVersion`；如果没有它，则不会显示提示。
- `ReleaseNotes__UpdateCheckIntervalHours`: GitHub 版本列表刷新的频率，单位为小时。默认值为 `6`。较短的间隔可以让长时间运行的实例更快看到新版本，较长的间隔则会减少 GitHub API 的调用次数。最小值为 `1`。

### 🔧 数据库维护设置 {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: 启用或禁用每日自动数据库维护 (true/false)。默认值为 `false`。启用后，系统将自动运行 VACUUM ANALYZE 操作，以优化数据库性能并防止膨胀。有关更多详细信息，请参阅 [Database Maintenance Guide](DatabaseMaintenance.md)。
- `DatabaseMaintenance__DailyExecutionTime`: 数据库维护运行的具体时间，采用 24 小时制 (HH:mm)。默认值为 `02:00`。请选择系统活动较低的时段。
- `DatabaseMaintenance__TimeoutMinutes`: 允许维护操作运行的最大分钟数。默认值为 `30`。对于较大的数据库，请增加此值。

### 🧬 Attachment Deduplication Settings {#attachment-deduplication-settings}
附件去重功能仅存储一次每个唯一的附件有效负载（通过 SHA-256 进行内容寻址），这是**始终开启的核心功能**——因此故意没有提供开关。只有下方的批处理/调度参数可以进行调整。有关完整详情，请参阅 [Attachment Deduplication Guide](AttachmentDeduplication.md)。

- `AttachmentDeduplication__BatchSize`: 在对现有数据进行一次性后台迁移期间，每次事务迁移的现有附件数量。默认为 `200`。较大的值迁移速度更快，但每个批次消耗的内存/数据库负载更高。
- `AttachmentDeduplication__DelayBetweenBatchesMs`: 可选的暂停时间（以毫秒为单位），用于在繁忙系统中限制数据库负载。默认为 `0`（无暂停）。
- `AttachmentDeduplication__StartupDelaySeconds`: 应用启动后开始后台迁移前的延迟时间（以秒为单位），为架构迁移（schema migration）完成提供时间。默认为 `20`。
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: 始终运行的垃圾回收间隔（以小时为单位），用于移除不再被任何电子邮件引用的附件有效负载。默认为 `12`。该过程独立于 `DatabaseMaintenance__Enabled` 运行。
- `AttachmentDeduplication__CommandTimeoutSeconds`: 迁移批处理操作（带有 SHA-256 哈希的 INSERT 和 UPDATE）的数据库命令超时时间（以秒为单位）。默认为 `300`（5 分钟）。对于非常大的数据库或附件，请增加此值；如果您希望更快地检测到失败，请降低此值。如果批处理仍然超时，服务会自动以一半的批次大小进行重试。

### 💾 账户存储设置 {#account-storage-settings}
每个账户的存储显示展示了 Dashboard 中 "Account Overview" 表以及 MailAccounts "Show All" 表中每个账户的数据库存储使用情况（所有邮件字段 + 附件）。一个自给自足的后台服务 (`AccountStorageRefreshService`) 通过累加 PostgreSQL `pg_column_size` 每列（覆盖邮件的所有字段而不读取邮件正文）来计算这些值，并将其缓存到 `AccountStorageCache` 表中。该服务独立于 `DatabaseMaintenance__Enabled` 运行。

- `AccountStorage__Enabled`: 启用或禁用存储刷新服务 (true/false)。默认值为 `true`。启用时，该服务会在启动时对待处理账户进行可恢复的填充 (backfill)（通过 `AccountStorageBackfillState` 实现崩溃安全），此后每日进行一次全量刷新。禁用时，仅在同步、导入或删除电子邮件时才会更新存储值，但显示的数值可能会随时间推移而变得陈旧。
- `AccountStorage__DailyExecutionTime`: 所有账户每日全量刷新的时间（24 小时制 `HH:mm`）。默认值为 `02:30`。请选择系统活动较低的时段。
- `AccountStorage__BackfillDelayMs`: 启动时初始填充期间账户之间的延迟（以毫秒为单位）。默认值为 `5000`。较低的值会加快填充速度，但会增加数据库负载；对于超大型存档，请调高此值以避免数据库过载。
- `AccountStorage__RefreshBatchDelayMs`: 每日全量刷新期间账户之间的延迟（以毫秒为单位）。默认值为 `1000`。较低的值会加快刷新速度，但会增加数据库负载；对于超大型存档，请调高此值。
- `AccountStorage__CommandTimeoutSeconds`: 每个账户存储计算的数据库命令超时时间（针对一个账户的所有行进行 `pg_column_size`）。默认值为 `300` (5 分钟)。计算会读取每一行邮件，包括 TOAST 数据，在超大型存档（25 万封以上邮件）上可能需要超过一分钟；之前的 30 秒 Npgsql 默认值会导致刷新静默失败，并导致仪表板显示陈旧数值。如果您的存档计算仍然超时，请调高此值。

> 💡 **注意**：存储值会在每次邮件同步、导入或保留策略删除后立即刷新，因此即使没有每日刷新，显示的数值也能保持最新。每日刷新是一个安全网，用于捕捉边缘情况（例如：直接对数据库进行的更改）。

### 📝 日志设置 {#logging-settings}
- `Logging__LogLevel__Default`: 应用程序的默认日志级别。可用级别包括：`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`。默认值为 `Information`。
- `Logging__LogLevel__Microsoft_AspNetCore`: ASP.NET Core 框架消息的日志级别。默认值为 `Warning`。
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Entity Framework 数据库命令的日志级别。默认值为 `Warning`。

### 🛡️ 安全设置 {#security-settings}
- `AllowedHosts`: 一个由分号分隔的主机名列表，允许应用程序提供服务。这有助于防止 HTTP Host header 攻击。示例：`AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`。**重要提示**：请勿在生产环境中使用 `*`，因为它会禁用 host header 验证。

#### 🔐 凭据静态加密 {#credential-encryption-at-rest}
邮件账户凭据（IMAP 密码、M365 client secret 和 MSA OAuth tokens）可以在数据库中进行加密。加密密钥保存在数据库**之外**（环境变量 / Docker secret），因此仅凭数据库转储不足以读取凭据。

- `Security__CredentialEncryptionKey`: Base64 编码的 256 位（32 字节）密钥加密密钥。使用以下命令生成：
  ```bash
  openssl rand -base64 32
  ```
  将该值设置为环境变量（推荐：在您的 compose 文件或容器环境中使用 `Security__CredentialEncryptionKey`）。应用程序不支持 `_FILE` 风格的 Docker secrets —— 如果您使用 Docker secret，请将其挂载并将其内容加载到 entrypoint 的环境变量中。切勿将密钥提交到 `appsettings.json`。配置密钥后，凭据将在写入时加密，且后台服务会执行一次现有明文凭据的加密。
- `Security__EncryptAccountCredentials`: 启用或禁用新/更新凭据值的加密（true/false）。默认值为 `true`。当未配置密钥时，凭据以明文形式存储，并在启动时记录警告 —— 现有安装在提供密钥之前不会受到影响。
- `Security__CredentialEncryptionBackfill`: 后台服务是否应在启动时加密现有的明文凭据（true/false）。默认值为 `true`。如果没有配置密钥，则此项无效。
- `Security__CredentialEncryptionKeyPrevious`: 可选的上一个密钥，仅用于解密使用该密钥加密的值（用于密钥轮转）。新值始终使用 `Security__CredentialEncryptionKey` 加密。

  > ⚠️ **密钥轮转**：轮转后，请保持 `Security__CredentialEncryptionKeyPrevious` 已配置，**直到每个凭据都被重写**（例如，通过编辑账户或自动 OAuth token 刷新）—— 使用旧密钥加密的现有值*不会*自动使用新密钥重新加密。一旦您移除了上一个密钥，任何仍由该密钥加密的凭据都将变得无法读取，并且必须重新输入。只要它处于配置状态，就必须像当前密钥一样安全。

> ⚠️ **保护密钥安全**：如果没有密钥，加密的凭据将无法恢复，必须重新输入。请将其与数据库备份一起（但要分开）进行备份。
>
> 💡 **现有安装**：安全升级 —— 只要没有设置 `Security__CredentialEncryptionKey`，一切都不会改变。在您准备好时设置密钥；现有的明文凭据保持可读，并在后台进行加密。

### 🔐 OIDC 配置 {#oidc-configuration}

有关 OpenID Connect 身份验证的详细设置说明，请参阅 [OIDC 实现指南](OIDC_Implementation.md)。

#### 基础 OIDC 设置 {#basic-oidc-settings}
- `OAuth__Enabled`: 启用或禁用 OIDC 身份验证 (true/false)
- `OAuth__Authority`: OpenID Connect 权威机构 URL (例如，Azure AD 为 https://sts.windows.net/{TENANT-ID}/)
- `OAuth__ClientId`: 由您的身份提供程序分配的客户端 ID
- `OAuth__ClientSecret`: 由您的身份提供程序分配的客户端密钥
- `OAuth__DisplayName`: 显示在 OIDC 登录按钮和自动重定向页面上的可选显示名称 (例如，`PocketID SSO`)。如果省略，则使用通用的 "Login with OAuth" 标签。
- `OAuth__ClientScopes__0`: 向身份提供程序请求的第一个作用域 (openid)
- `OAuth__ClientScopes__1`: 向身份提供程序请求的第二个作用域 (profile)
- `OAuth__ClientScopes__2`: 向身份提供程序请求的第三个作用域 (email)
- `OAuth__ResponseMode`: 授权请求的可选响应模式 (`query`, `fragment` 或 `form_post`)。默认为 `form_post`，即 ASP.NET Core 的默认值。对于不支持 `form_post` 的提供程序（如 Synology SSO Server），请将其设置为 `query`。请参阅 [提供程序兼容性](OIDC_Implementation.md#provider-compatibility)。
- `OAuth__AutoApproveUsers`: 自动批准新的 OIDC 用户，无需管理员手动批准 (true/false)。默认为 `false`。

#### 用户置备设置 {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: 自动批准新的 OIDC 用户，无需管理员手动批准 (true/false)。默认为 `false`。启用时，通过 OIDC 提供程序进行身份验证的用户将立即激活并可以访问应用程序。禁用时（默认），新的 OIDC 用户将被创建为非活动状态，需要管理员手动激活。有关详细信息，请参阅 [自动批准 OIDC 用户](OIDC_Implementation.md#auto-approve-oidc-users)。
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1` 等: 应自动置备为管理员的电子邮件地址。具有这些电子邮件地址的用户在首次 OAuth 登录时将作为活动管理员创建，从而绕过正常的审批流程。电子邮件匹配不区分大小写。

#### 无密码登录设置 {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: 禁用用户名/密码登录 (true/false)。默认为 `false`。启用时，密码表单将被隐藏，**并且** 服务器会拒绝所有的密码 POST 请求，因此仅提供 OAuth 登录。需要 `OAuth__Enabled=true`。如果禁用了 OAuth，此设置将不起作用。
- `OAuth__AutoRedirect`: 自动将用户重定向到 OAuth 提供程序 (true/false)。默认为 `false`。需要 `OAuth__DisablePasswordLogin` 设置为 `true`。用户在被重定向之前会看到一个简短的加载界面。

#### 示例：完整的 OIDC 优先配置 {#example-full-oidc-first-configuration}
```yaml
environment:
  - OAuth__Enabled=true
  - OAuth__Authority=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0
  - OAuth__ClientId=your-client-id
  - OAuth__ClientSecret=your-client-secret
  - OAuth__DisplayName=PocketID SSO
  - OAuth__ClientScopes__0=openid
  - OAuth__ClientScopes__1=profile
  - OAuth__ClientScopes__2=email
  - OAuth__DisablePasswordLogin=true
  - OAuth__AutoRedirect=true
  - OAuth__AutoApproveUsers=true
  - OAuth__AdminEmails__0=admin@example.com
  - OAuth__AdminEmails__1=manager@example.com
```

## 🔐 Kestrel HTTPS 配置 (可选) {#kestrel-https-configuration-optional}

虽然应用程序旨在通过带有 HTTPS 的反向代理进行访问，但您也可以配置 Kestrel Web 服务器以使用 SSL/TLS 证书。这可以在反向代理与应用程序容器之间提供端到端加密。

### 配置步骤 {#configuration-steps}

1. **生成或获取** PFX 格式的 SSL 证书 (例如：`localhost.pfx`)

2. **将以下环境变量添加到** 您的 `docker-compose.yml` 中，用于 `mailarchive-app` 服务：

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **更新** `mailarchive-app` 服务中的端口映射：

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **为证书添加** 卷映射：

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **将您的证书文件** (例如：`localhost.pfx`) 放置在宿主机系统的 `./certs` 目录中。

### 环境变量说明 {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: HTTP 端点 URL (默认: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: HTTPS 端点 URL (默认: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: 容器内 PFX 证书文件的路径
- `Kestrel__Endpoints__Https__Certificate__Password`: PFX 证书文件的密码

> 💡 **注意**：此配置是可选的。如果您正在使用带有 HTTPS 的反向代理（推荐），反向代理与应用程序之间的通信可以保持为 HTTP。但是，为了在敏感环境中实现最高安全性，您也可以在 Kestrel 上启用 HTTPS 以加密整个通信路径。

## 🔑 生产环境的密钥管理 {#secrets-management-for-production}

将数据库密码、管理员凭据和 OAuth 客户端密钥等敏感数据直接硬编码在 `docker-compose.yml` 中存在安全风险，尤其是当该文件被提交到版本控制时。推荐的方法是将这些值外部化到 Docker Compose 会自动加载的 `.env` 文件中。

### 📄 `.env` 文件 {#the-env-file}

在 `docker-compose.yml` 所在的同一目录下创建一个包含以下内容的 `.env` 文件：

```env
# Database
POSTGRES_PASSWORD=YourSecureDBPassword123!

# Admin Account
AUTH_USERNAME=admin
AUTH_PASSWORD=YourSecureAdminPassword456!

# OIDC / OAuth Secrets
OAUTH_CLIENT_SECRET=YourOAuthClientSecret

# Credential Encryption at Rest (generate with: openssl rand -base64 32)
CREDENTIAL_ENCRYPTION_KEY=YourBase64Encoded32ByteKey

# Kestrel HTTPS (optional)
KESTREL_CERT_PASSWORD=YourCertPassword
```

**重要提示**：Docker Compose 会自动读取同一目录下的 `.env` 文件 —— 您 **不需要** 手动引用它。在 `.env` 中定义的所有变量都可以通过 `${VARIABLE_NAME}` 语法在 `docker-compose.yml` 中使用。

### 🐳 适配后的 `docker-compose.yml` {#adapted-docker-composeyml}

应将安装步骤中的 `docker-compose.yml` 进行适配，使用占位符代替硬编码的密钥：

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=${POSTGRES_PASSWORD}

      # Authentication Settings
      - Authentication__Username=${AUTH_USERNAME}
      - Authentication__Password=${AUTH_PASSWORD}
      # ... other settings remain unchanged ...

      # OIDC Configuration
      - OAuth__ClientSecret=${OAUTH_CLIENT_SECRET}

      # Credential Encryption at Rest
      - Security__CredentialEncryptionKey=${CREDENTIAL_ENCRYPTION_KEY}
    # ... ports, volumes, networks ...

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    # ... volumes, networks ...
```

### 📋 管理员 `.env.example` 模板 {#envexample-template-for-administrators}

在您的仓库中创建一个 `.env.example` 文件，作为其他管理员的模板。它包含占位符值和文档，但 **不包含真实的密钥**：

```env
# ============================================
# Mail Archiver - Environment Configuration
# ============================================
# Copy this file to .env and fill in your values.
# Never commit the actual .env file to version control!

# --- PostgreSQL ---
POSTGRES_PASSWORD=change_me_db_password

# --- Admin Account ---
AUTH_USERNAME=admin
AUTH_PASSWORD=change_me_admin_password

# --- OIDC / OAuth ---
OAUTH_CLIENT_SECRET=change_me_client_secret

# --- Credential Encryption at Rest ---
# Generate with: openssl rand -base64 32
CREDENTIAL_ENCRYPTION_KEY=change_me_base64_32_byte_key

#...and so on
```

### ✅ 最佳实践 {#best-practices}

- **使用 `.env.example` 作为模板**
- **设置严格的文件权限** — 限制对 `.env` 文件的访问：
  ```bash
  chmod 600 .env
  ```
- **使用强密码** — 生成包含字母、数字和特殊字符的长度较长且随机的密码（至少 16 个字符）。
- **限制 `.env` 文件访问** — 只有运行 Docker Compose 的用户才应具有对 `.env` 文件的读取权限。

## 🔒 安全注意事项 {#security-notes}

- 使用强密码并更改默认凭据。密码长度应至少为 12 个字符，并包含大写字母、小写字母、数字和特殊字符的组合。避免使用常用词汇或易于猜测的信息。
- 在生产环境中，考虑通过反向代理实现 HTTPS。
- 建议定期备份 PostgreSQL 数据库。有关详细的备份和恢复流程，请参阅 [Backup and Restore Guide](BackupRestore.md)。
