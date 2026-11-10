# Mailcow 实例归档 {#mailcow-instance-archiving}

[← 返回文档索引](index.md)

## 概述 {#overview}

Mail Archiver 可以集中归档 [Mailcow](https://mailcow.email) 实例的邮箱，而无需收集邮箱所有者的个人密码。

管理员只需使用 API 密钥连接一次实例。随后，Mail Archiver 会列出所有邮箱，并为每个选定归档的邮箱创建一个受限于 `imap_access` 的**专用应用密码 (dedicated app password)**。现有的 IMAP 提供程序将使用该应用密码来归档邮箱，并且可以随时撤销该密码。

管理员可以通过 **Add Account → Mailcow** 或从已由 Mailcow 管理的账户的操作菜单中进入实例管理器。管理器仅限管理员使用，因为存储的 API 密钥可以管理该实例中每个邮箱的凭据。

## 从 Mailcow 创建账户 {#creating-accounts-from-mailcow}

在 **Mail Accounts → Add Account** 中，管理员可以选择 **Mailcow** 作为提供程序。随后，表单会显示已连接的实例以及 **Manage mailboxes** 操作。在此处选择邮箱会创建带有专用应用密码的普通 IMAP 归档账户；不会存储单独的 Mailcow 账户。如果尚未存在实例，点击 **Manage Mailcow instances** 将打开实例管理器，您可以在其中进行连接。

## 链接现有账户 {#linking-an-existing-account}

在 IMAP 账户的编辑视图中，管理员可以将提供程序切换为 **Mailcow** 并选择一个实例。该账户将保留 IMAP 提供程序，但会被纳入 Mailcow 管理：Mail Archiver 会创建一个专用的应用密码，通过 IMAP 进行验证，并替换账户存储的密码。随后，该账户将出现在账户列表中，并带有 Mailcow 操作。

要退出 Mailcow 管理，请选择另一个提供程序并保存。应用密码将被撤销，本地密码将被清除。当切换回 IMAP 时，请输入新的 IMAP 密码；切换到 Microsoft 365 或仅限导入的账户时，则会提供其自身的凭据。非管理员仍然可以编辑 Mailcow 管理账户的非凭据设置，但无法对其进行链接或取消链接。

## 前提条件 {#prerequisites}

### 必须启用凭据加密 {#credential-encryption-must-be-enabled}

Mail Archiver 使用现有的凭据加密功能，将 Mailcow API 密钥和每个生成的应用密码以加密形式存储在磁盘上。在连接实例之前，请设置

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```


如果在禁用加密的情况下尝试创建实例或激活邮箱，操作将被拒绝，因此通过此功能永远不会以明文形式写入凭据。请将密钥保存在数据库之外，并将其包含在您的安全备份中 —— 如果没有它，存储的应用密码将无法解密，邮箱也无法进行配置。详情请参阅 [Setup](Setup.md#-security-settings)。

### Mailcow API 访问权限 {#mailcow-api-access}

在 Mailcow 的 **Configuration → Access → API** 下创建一个 API 密钥。该密钥必须被允许执行以下操作：

- 列出邮箱 (`get/mailbox/all`)，
- 列出、创建和删除应用密码 (`get/app-passwd/all`, `add/app-passwd`, `delete/app-passwd`)。

在 Mailcow 的 API 设置中，将 API 限制为 Mail Archiver 主机的 IP 地址。Mailcow 的 app-password 端点要求为该实例启用 **App Passwords** 功能。

### 网络访问 {#network-access}

- 到 Mailcow 源地址的 **HTTPS** 连接（例如 `https://mail.example.com`）。
- 到 IMAP 主机的 **IMAP over TLS** 连接，通常端口为 `993`。配置过程不支持明文 IMAP。

## 使用步骤 {#step-by-step-usage}

### 连接实例 {#connecting-an-instance}

1. 以管理员身份登录 Mail Archiver。
2. 打开 **Mail Accounts** 并点击 **Mailcow instances**。
3. 点击 **Connect Mailcow instance**。
4. 输入连接详情：
   - **Account name** — 在 Mail Archiver 中显示的实例名称。
   - **Mailcow URL** — 仅限 HTTPS 源地址，例如 `https://mail.example.com`。拒绝包含路径、查询字符串和嵌入式凭据的 URL。
   - **API key** — 在 Mailcow 中创建的密钥。
   - **IMAP host** — 用于归档的主机名，例如 `mail.example.com`。仅输入主机名，不要包含协议或端口。
   - **IMAP TLS port** — 默认为 `993`。
5. 点击 **Connect**。Mail Archiver 在保存实例前会通过列出邮箱来验证 API 密钥。成功后将打开邮箱选择界面。

API 密钥以加密形式存储，且永远不会重新渲染到表单或错误消息中。API 调用禁用了 HTTP 重定向，因此密钥不会被转发到另一个源地址。

### 激活邮箱 {#activating-mailboxes}

1. 在邮箱页面上，可以使用搜索字段可选地过滤列表。过滤器匹配地址和显示名称，且不区分大小写。
2. 选择要归档的邮箱。**Select all** 和 **Clear selection** 仅影响当前可见（已过滤）的邮箱，因此过滤是选择子集的一种安全方式。
3. 点击 **Activate archiving**。

对于每个选定的邮箱，Mail Archiver 会执行以下操作：

1. 根据 Mailcow 当前的邮箱列表重新验证地址，并忽略已禁用的邮箱，
2. 创建一个名为 `{instance name} – {mailbox display name}` 的归档账户，
3. 生成一个名为 `MailArchiver-<unique id>` 且受限于 `imap_access` 的随机应用密码，
4. 通过 TLS 进行 IMAP 登录以验证凭据，
5. 只有在完成上述步骤后才会启用同步。

如果 IMAP 验证失败，账户将保持禁用状态，可以重试。

### 邮箱状态徽章 {#mailbox-status-badges}

选择页面为每个邮箱贴上标签，以便明确说明为什么复选框可用或不可用：

| 徽章 | 含义 | 可选 |
| --- | --- | --- |
| **Available** | 已列在 Mailcow 中，尚未配置 | 是 |
| **Active** | 已配置并正在同步 | 否 |
| **Pending** | 配置已开始但未完成（可重试） | 是 |
| **Revoked** | 应用密码已被撤销；可以再次激活 | 是 |
| **Existing** | 已存在具有此地址的独立 IMAP 账户 | 否 |
| **Disabled** | 邮箱在 Mailcow 中已被禁用 | 否 |

### 重试待处理 (Pending) 邮箱 {#retrying-a-pending-mailbox}

生成的凭据和唯一的应用密码名称在调用 Mailcow API **之前**即已持久化。如果请求出现模糊失败 —— 例如在创建应用密码后发生超时 —— 邮箱将保持为 **Pending** 状态。重新加载页面并再次选择它：Mail Archiver 会先查找应用名称，只有在不存在时才会创建新的应用密码。重试不会累积重复的凭据。

一个实例的配置和撤销操作使用 PostgreSQL 咨询锁 (advisory locks) 进行串行化处理，因此两个并发操作无法对同一个邮箱进行两次配置。

### 撤销访问权限 {#revoking-access}

点击邮箱旁的 **Revoke app password**。这将：

- 禁用未来的同步，
- 删除 Mailcow 中受管理的应用密码，
- 并清除本地密码。

**归档邮件将被保留。** 仅移除受管理的应用密码 —— 属于该邮箱的其他应用密码不会受到任何影响。已经在运行的同步操作可能会使用其现有连接完成。

如果远程请求失败，账户将保持禁用状态，可以重试撤销操作。

在 **Mail Accounts** 中删除归档账户不会静默移除远程凭据。该绑定仍会列在实例页面的 **Remaining app passwords** 下，以便可以显式撤销。

### 编辑实例 {#editing-an-instance}

打开 **Mailcow instances** 并点击 **Edit**。API 密钥字段始终为空 —— 存储的密钥永远不会显示。保持为空以保留当前密钥，或输入新密钥进行替换。更改后的连接详情在保存前会针对 Mailcow 进行验证。

### 删除实例 {#deleting-an-instance}

打开 **Mailcow instances** 并点击 **Delete**。

删除实例会移除其加密的 API 密钥及其邮箱绑定。**归档账户及其归档邮件将被保留**，但账户会被禁用。

如果 Mailcow 中仍有任何受管理的应用密码处于 **active** 状态，则**拒绝删除实例**：删除实例将导致撤销该实例所需的 API 密钥也被删除。请先在邮箱页面撤销所有受管理的应用密码。

## 审计日志 {#audit-logging}

每次管理操作都会连同操作用户一起写入访问日志：

- 连接实例，
- 更新实例，
- 删除实例，
- 激活邮箱（包含已配置数量和失败数量），
- 撤销应用密码。

## 邮箱限制 {#mailbox-limit}

单次激活运行最多可配置 `Mailcow__MaxSelectedMailboxes` 个邮箱（默认 `1000`）。这可以防止意外的大规模配置以及过高的 Mailcow API 和 IMAP 负载。对于较大的实例，请多次运行该操作。

每个邮箱都需要多次 API 往返以及一次 IMAP 验证，并且是按顺序处理的。在一个请求中配置数百个邮箱可能会超过反向代理或浏览器的超时时间；请将选择内容分批处理。

## 安全说明 {#security-notes}

- API 密钥保留在服务端。它作为 `X-API-Key` 请求头发送，从日志中脱敏，且永远不会返回给浏览器。
- Mailcow 的 app-password 响应可能会在其 `log` 字段中回显明文密码。Mail Archiver 刻意不记录响应体，也不向浏览器展示 API 错误；失败会以本地化消息的形式报告。
- HTTP 200 响应不被视为成功：变更操作需要 Mailcow 的逐结果 `type: success`。
- IMAP 权限本质上不是只读的。已配置的账户没有配置服务端删除策略，且管理员保留对账户设置的控制权。此功能归档的是邮件，而不是 Mailcow 配置或备份。

### 主机名与私有网络 {#hostnames-and-private-networks}

Mailcow URL 和 IMAP 主机由管理员输入，可能指向私有地址空间。这是有意为之且受支持的：Mail Archiver 通常部署在邮件服务器旁边，因此 `mail.example.lan`、`192.168.10.20` 或 `10.0.0.5` 是正常值。**没有任何私有、回环或链路本地地址被拦截。**

信任边界是管理员角色，而不是网络地址。`Mail Accounts` 对自管人员可用，他们可能已经为自己的账户配置了任意 IMAP 服务器；Mailcow 部分仅限管理员，而管理员已经拥有对安装中每个归档账户的无限制访问权限。因此，限制 IMAP 主机并不能缩小权限边界，反而会破坏预期的本地部署。

仍有两个约束条件适用：

- Mailcow URL 必须是一个纯粹的 HTTPS 源地址。路径、查询字符串、用户信息和重定向都会被拒绝，因此 API 密钥不会被转发到另一个源地址。
- IMAP 主机必须是不带协议或端口的主机名，且配置过程始终使用 TLS (`SslOnConnect`) 连接。

如果您需要限制 Mail Archiver 可以访问哪些主机，请在网络层强制执行 —— 例如使用出口防火墙规则或容器的 DNS 策略 —— 而不是依赖应用程序中的输入验证。

## 故障排除 {#troubleshooting}

| 现象 | 原因与对策 |
| --- | --- |
| 连接或激活时显示 *Encryption required* | `Security__CredentialEncryptionKey` 未设置或无效。配置一个 base64 编码的 32 字节密钥并重启。 |
| 显示 *HTTPS origin required* | URL 包含路径、查询字符串或嵌入式凭据。请输入纯粹的源地址，例如 `https://mail.example.com`。 |
| *IMAP host invalid* | IMAP 主机必须是不带协议或端口的主机名。 |
| *Instance already connected* | 已存在具有相同 URL 的另一个实例。请编辑该实例。 |
| *API request failed (401/403)* | API 密钥缺少必要的权限，或者 IP 限制未包含 Mail Archiver 主机。 |
| *API request rejected* | Mailcow 返回了 `error`/`danger`/`warning` 行。请检查 Mailcow 日志；Mail Archiver 不会转发响应体。 |
| *IMAP verification failed* | IMAP 主机或端口错误、证书不受信任，或者 Mailcow 中禁用了 app-password 功能。 |
| *IMAP account exists* | 另一个独立的 IMAP 账户已经在归档此地址。请使用该账户而非创建第二个归档。 |
| *Revoke previous password* | 该邮箱的归档账户已被删除。请先撤销剩余的应用密码，然后再次激活。 |
| 邮箱保持 **Pending** 状态 | Mailcow API 超时。重新加载页面并再次激活邮箱；不会创建重复的应用密码。 |
| 删除被拒绝 | 受管理的应用密码仍处于 active 状态。请先在邮箱页面撤销它们。 |

## 使用的 API 端点 {#api-endpoints-used}

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## 数据库变更 {#database-changes}

迁移仅添加了 `MailcowInstances` 和 `MailcowMailboxes` 表及其索引和外键。现有的邮箱表和归档数据将被保留。删除归档账户会将绑定的 `MailAccountId` 设置为 `NULL`，以便之后仍可撤销远程凭据。
