# 账户导入 {#account-import}

[← 返回文档索引](index.md)

## 概述 {#overview}

Mail Archiver 支持一次性导入多个电子邮件账户，这样在配置大量邮箱时，无需逐个创建每个账户。

根据邮件提供商的不同，有两种批量导入路径：

| 提供商 | 批量导入方法 | 文档 |
|---|---|---|
| **Microsoft 365 (tenant)** | 通过 Microsoft Graph 进行租户发现 — 一个应用注册即可访问租户中的每个邮箱 | [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) |
| **IMAP** | CSV 批量导入 — 上传一个每行代表一个邮箱的 CSV 文件 | 本页面（见下文） |

> **注意：** Microsoft 个人账户 (Outlook.com / M365 Family，提供商为 **MSA**) 使用交互式设备代码授权流程，无法进行批量导入。它们必须逐个创建并授权。请参阅 [MSA Outlook Setup](MSA_Outlook_Setup.md)。

---

## IMAP CSV 批量导入 {#imap-csv-bulk-import}

### 功能说明 {#what-it-does}

CSV 批量导入允许管理员通过上传一个 CSV 文件，在单次操作中创建数百个 IMAP 邮件账户。文件中的每一行对应一个邮箱。通用的服务器设置（IMAP 主机、端口、SSL）可以在上传表单中统一指定并应用于每一行，同时也可以为每一行设置可选的覆盖参数。

导入操作**仅限管理员**执行。可以通过 **Mail Accounts** 页面，通过 **Import** 下拉菜单 → **CSV Bulk Import** 进行访问。

### CSV 文件格式 {#csv-file-format}

文件必须是 UTF-8 编码的 CSV 文件，包含**标题行**，并使用**逗号 (`,`) 作为分隔符**。包含逗号或引号的字段必须用双引号 (`"..."`) 括起来。

#### 列 {#columns}

| 列 | 必填 | 描述 |
|---|---|---|
| `email` | 是 | 邮箱地址。如果未提供 `username`，则也用作默认登录用户名。 |
| `password` | 是 | IMAP 登录密码。按原样存储 — 保留前导/尾随空格。 |
| `name` | 否 | 账户的显示名称。如果省略，默认为 `{prefix} - <{email}>`。 |
| `username` | 否 | 如果与电子邮件地址不同，则使用此登录用户名。 |
| `imap_server` | 否 | 每行专用的 IMAP 主机。覆盖上传表单中的通用服务器。 |
| `imap_port` | 否 | 每行专用的 IMAP 端口 (1–65535)。覆盖通用端口。 |
| `use_ssl` | 否 | 每行专用的 SSL 开关 (`true` 或 `false`)。覆盖通用设置。 |

#### 示例 {#example}

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- 第 1 行使用上传表单中的通用服务器/端口/SSL。
- 第 2 行设置了自定义用户名，但在其他方面使用通用设置。
- 第 3 行覆盖了服务器、端口和 SSL。

可以通过导入页面上的 **Download example CSV** 按钮下载示例文件。

### 上传表单 {#upload-form}

| 字段 | 用途 |
|---|---|
| **CSV file** | 要上传的文件。 |
| **IMAP server** | 在没有 `imap_server` 覆盖的情况下，应用于所有行的通用主机。 |
| **IMAP port** | 通用端口（默认为 993）。 |
| **Use SSL** | 通用 SSL 开关（默认为开启）。 |
| **Name prefix** | 自动生成的账户名称的前缀。默认值：`IMAP`。 |
| **Skip existing mailboxes** | 启用时，如果电子邮件作为 IMAP 账户已存在，则跳过该行（计为“已跳过”，而非错误）。禁用时，它们将被视为失败。 |
| **Account enabled** | 创建的账户是否应启用后台同步。 |
| **Delete After Days / Local Retention Days** | 应用于每个新创建账户的可选保留设置。请参阅 [Retention Policies](RetentionPolicies.md)。 |

### 处理流程 {#processing}

1. 解析 CSV 文件并验证每一行（电子邮件格式、密码是否存在、端口范围、SSL 值）。
2. 移除文件中的重复电子邮件（以第一次出现为准）。
3. 检测具有相同电子邮件地址的现有 IMAP 账户；根据 **Skip existing** 开关的状态，它们会被跳过或报告为失败。
4. 所有新账户将通过单个数据库批处理进行插入。
5. 写入一条访问日志摘要，总结运行情况。

导入期间不会进行 IMAP 连接测试 — 账户会立即保存。身份验证问题仅会在第一次后台同步时显现。这使得即使是处理数百个邮箱，导入过程也能保持快速。

### 结果页面 {#result-page}

导入完成后，结果页面会显示三个表格：

- **Created** — 每个已创建账户的电子邮件和名称。
- **Skipped** — 当启用 **Skip existing** 时，作为重复项（文件内重复或已存在）的电子邮件。
- **Failed** — 无法处理的行，包含行号和原因（缺少电子邮件、缺少密码、无效端口、无 IMAP 服务器等）。

摘要横幅会显示计数：*{created} 已创建, {skipped} 已跳过, {failed} 失败*。

### 限制 {#limits}

以下限制可以通过 `appsettings.json` / 环境变量进行配置（请参阅 [Setup Guide](Setup.md) → *CSV Import Settings*）：

| 设置 | 默认值 | 描述 |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | 每次上传处理的最大行数。 |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | 最大上传文件大小。 |

### 安全提示 {#security-note}

IMAP 密码在数据库中的存储方式与单独创建的 IMAP 账户相同。请注意，CSV 文件包含所有邮箱的明文密码 — 请在成功导入后将其从客户端机器中删除。

---

## Microsoft 365 租户导入 {#microsoft-365-tenant-import}

对于 Microsoft 365 (Exchange Online)，批量导入不需要 CSV 文件。一个具有正确 Graph 权限的单个 Azure AD 应用注册即可访问租户中的每个邮箱，因此 Mail Archiver 可以自动发现这些邮箱。

请参阅专用指南：**[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**。
