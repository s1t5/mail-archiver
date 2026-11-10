# 📤 审计数据导出 {#audit-data-export}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

审计数据导出功能从现有存档中创建表格化的大量数据包（包含 `INDEX.XML` 的 ZIP 文件 + CSV 表格 + 索引 DTD）。这些数据包遵循用于数据介质移交的既定索引结构，可以直接被支持该索引格式的常用外部审计和分析工具导入。

该功能作为一个**从 Logs 页面可以访问的专用页面**提供，并仅限管理员用户使用。每次导出运行都会记录在访问日志中，因此导出历史记录是具有版本安全性（revision-safe）的。

## 🧭 访问方式 {#access}

1. 以管理员用户身份登录
2. 打开 **Logs** 页面
3. 点击右上角的 **Audit Data Export** 按钮

非管理员用户看不到该按钮，且无法调用任何导出端点（服务器会拒绝请求）。

## 📦 导出包结构 {#export-package-structure}

生成的 ZIP 文件名为 `audit-export-<timestamp>.zip`，包含：

| 文件 | 内容 |
|---|---|
| `INDEX.XML` | 描述数据供应商、介质以及所有表/列定义的索引文件 |
| `index.dtd` | 索引文件引用的 DTD；导入工具用于验证 |
| `emails.csv` | 在所选期间/邮箱中，每封存档邮件对应一行 |
| `attachments.csv` | 可选；每封导出邮件的每个附件对应一行 |

### 表 `emails.csv` {#table-emailscsv}

列（无标题行；列名来自 `INDEX.XML`，正如该格式的惯例）：

| 列 | 描述 |
|---|---|
| Id | 内部邮件 ID (数字) |
| MessageId | 邮件 Message-ID 请求头 |
| SentDate | 发送日期，ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`) |
| ReceivedDate | 接收日期，ISO 8601 UTC |
| IsOutgoing | `1` = 发出，`0` = 收到 |
| From | 发件人 |
| To | 收件人 |
| Cc | 抄送收件人 |
| Bcc | 密送收件人 |
| Subject | 主题 |
| FolderName | 邮件存档所在的文件夹 |
| HasAttachments | `1` = 有附件，`0` = 无附件 |
| AttachmentCount | 附件数量 (数字) |
| AccountEmail | 存档邮箱的电子邮件地址 |

### 表 `attachments.csv` {#table-attachmentscsv}

| 列 | 描述 |
|---|---|
| EmailId | 附件所属邮件的内部 ID (与 `emails.csv` 的关联键) |
| FileName | 附件文件名 |
| ContentType | MIME 类型 |
| Size | 大小（字节，数字） |
| Sha256 | 内容的 SHA-256 哈希值（来自内容寻址的附件存储） |

### CSV 约定 {#csv-conventions}

- 编码：UTF-8 **不带** BOM（在 `INDEX.XML` 中声明为 `UTF8`）
- 列分隔符：`;`
- 文本定界符：`"`（通过双重符号进行转义）
- 记录分隔符：`\n`（在 `INDEX.XML` 中明确声明）
- 日期：ISO 8601，时间从配置的显示时区转换为 UTC
- 无标题行；无本地化数字（数字列包含纯整数）

## 🖥️ 使用导出页面 {#using-the-export-page}

### 表单字段 {#form-fields}

1. **From / To date** - 要导出的期间（必填，最大跨度可配置，默认为 10 年）
2. **Mailbox** - 将导出限制在单个邮箱或导出所有邮箱
3. **Attachment metadata** - 勾选后，数据包中将添加 `attachments.csv`
4. **Data supplier name / location / comment** - 写入 `INDEX.XML` 的 `DataSupplier` 块中；从 `AuditExport` 配置部分预填并可按每次导出进行编辑

### 任务生命周期 {#job-lifecycle}

1. 点击 **Start Export** 会验证期间和邮箱，创建任务并返回页面
2. 任务在后台运行（排队的导出将依次处理）；任务表通过自动轮询显示实时进度（已处理/总邮件数及进度条）
3. 导出完成后，任务行中会出现 **Download** 按钮
4. 正在运行或排队的任务可以被**取消 (cancelled)**

### 导出历史 {#export-history}

表单下方的历史记录表列出了最近的导出记录，包含时间戳、用户、期间、邮箱、邮件计数、状态和文件大小。历史记录**持久化在数据库中**（`AuditExportJobs` 表），因此在应用程序重启后依然存在且具有版本安全性。

下载的文件保留在可配置的保留期内（默认为 30 天，参见 `AuditExport__RetentionDays`）；之后，ZIP 文件将被每日清理程序删除，而历史记录行将保留。

> 📝 **注意**：导出文件存储在应用程序容器内。如果您需要长期保存它们，请下载它们并将其存储在您的审计证据库中。

## 🔐 审计 {#auditing}

每次导出都会产生两个类型为 **Audit Data Export** 的新访问日志条目：

1. **Start entry** - 当导出进入队列时（包含任务 ID、期间、邮箱、选项的 JSON）
2. **Result entry** - 当导出完成、失败或被取消时（包含任务 ID、期间、邮箱、邮件计数、结果和包大小的 JSON）

这些条目会像所有其他访问日志条目一样出现在 Logs 页面中，并可以通过新类型进行过滤。类型过滤下拉菜单会自动获取它们。

## ⚙️ 配置 {#configuration}

`appsettings.json` 中的 `AuditExport` 部分（环境变量使用双下划线语法，例如 `AuditExport__RetentionDays`）：

| 设置 | 描述 | 默认值 |
|---|---|---|
| `DataSupplierName` | 数据供应商名称表单字段的默认值 | 空 |
| `DataSupplierLocation` | 数据供应商位置表单字段的默认值 | 空 |
| `Comment` | 备注表单字段的默认值 | 空 |
| `OutputDirectory` | 生成的 ZIP 文件的目录（相对路径相对于应用内容根目录解析） | `exports/audit` |
| `RetentionDays` | 已完成的导出文件在多少天后被删除 | `30` |
| `MaxRangeYears` | 允许的期间开始日期与结束日期之间的最大跨度 | `10` |

## 🗄️ 数据库 {#database}

导出历史存储在 `mail_archiver."AuditExportJobs"` 表中（由迁移 `MigrateV2609_1` 创建，启动时自动应用）。此功能不会修改任何现有表。

## 🔍 故障排除 {#troubleshooting}

| 问题 | 原因 / 解决方案 |
|---|---|
| 重启后下载按钮消失 | 导出文件存在于容器中；如果容器被重建或超过了保留期，ZIP 文件将消失，而历史记录行会保留。请重新运行导出。 |
| "The period must not exceed X years" | 缩短期间或提高 `AuditExport__MaxRangeYears`。 |
| 任务长时间处于 *Running* 状态 | 大型存档按每批 1000 封邮件进行流式传输；进度会持续更新。非常长的期间可能需要一段时间 —— 请检查已处理/总数计数器。 |
| 导入工具报告编码问题 | 该包是 UTF-8 且不带 BOM，带有声明的 `UTF8` 标志和显式的记录分隔符；请确保工具读取了数据文件旁的 `INDEX.XML`（请解压整个 ZIP，不要直接从 ZIP 内部单独打开文件）。 |

## 🔗 相关文档 {#related-documentation}

- [Access Logging](Logs.md)
- [Installation, Setup and Parameters](Setup.md)
