# 🔍 邮件搜索 {#mail-search}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

搜索是邮件归档的核心。Mail Archiver 会在您有权访问的所有账户中，对归档邮件的**全文**（包括主题、正文、发件人和收件人）进行搜索，即使是在拥有数百万条消息的归档库中，也能在毫秒级内返回结果。

本指南涵盖了搜索界面、查询语法、过滤器、排序、分页，以及在需要对结果集进行操作时该如何处理。

> **ℹ️ 搜索仅显示您可能可见的内容。** 结果会根据您的账户权限进行过滤。非管理员用户只能找到分配给其邮箱的邮件 —— 这种限制是在查询本身中应用的，而不仅仅是在界面中。

## 🚀 访问搜索 {#accessing-search}

1. 登录并在主菜单中选择 **Archive**
2. 搜索栏位于结果列表上方

在不输入搜索词的情况下打开 Archive，会列出您最近的电子邮件，按时间从新到旧排序。您可以从那里进行浏览，或使用过滤器进行缩小范围。

## 🎯 搜索界面 {#the-search-interface}

| 字段 | 功能 |
|---|---|
| **Search Term** | 全文查询 —— 参见下文的 [Query Syntax](#-query-syntax) |
| **From** | 仅限在此日期或之后发送的电子邮件 |
| **To** | 仅限在此日期或之前发送的电子邮件 |
| **Direction** | `All`, `Incoming` 或 `Outgoing` |
| **Account** | 限制为单个邮件账户 |
| **Folders** | 通过文件夹树限制为单个文件夹 |
| **Search** | 执行查询 |
| **Reset** | 清除所有过滤器 |

搜索词会匹配**主题、正文、发件人和收件人**（`From`, `To`, `Cc`, `Bcc`）。

### 文件夹树 {#the-folder-tree}

文件夹以**层级树**的形式显示在左侧，镜像了源邮箱的文件夹结构 —— 包括嵌套文件夹。点击文件夹可将其设为搜索范围，并使用折叠/展开控件在需要屏幕空间时折叠树。

选择文件夹不会改变您的搜索词；两者会结合使用。

## 🧩 查询语法 {#query-syntax}

语法设计得非常精简。以下是应用程序实际理解的所有内容。

### 普通单词 —— 以及为什么前缀有效 {#plain-words-and-why-prefixes-work}

输入一个或多个单词；所有单词都必须存在（**AND** 搜索）：

```
invoice january
```

在搜索字段中同时找到包含 `invoice` 和 `january` 的电子邮件。

> **💡 单词会自动进行部分匹配。** 每个单词都被视为**前缀**，因此搜索 `isenb` 也会找到 `isenboeck` 和 `isenböck`，而 `rech` 也会找到 `Rechnung` 以及 `Rechnungen`。您不需要输入通配符 —— 也没有可输入的通配符。这也意味着非常短的词可能会返回大量结果；请参阅 [Performance](#-performance-and-large-archives)。

搜索是**不区分大小写**的。`Invoice`, `invoice` 和 `INVOICE` 是相同的查询。

### 精确短语 —— 引号 {#exact-phrases-quotes}

将文本包裹在双引号中，以要求单词按该顺序**紧挨在一起**：

```
"important meeting"
```

如果不使用引号，`important meeting` 也会匹配两个单词出现在无关句子中的电子邮件。使用引号后，只有相邻的短语才会匹配。

### 特定字段搜索 {#field-specific-search}

在术语前加上字段名称和冒号，即可**仅**在该字段中进行搜索：

| 语法 | 搜索范围 |
|---|---|
| `subject:invoice` | 仅主题 (Subject) |
| `from:john` | 仅发件人 (Sender) |
| `to:example.com` | 仅收件人 (Recipients) |
| `body:important` | 仅正文 (Body) |

字段名称不区分大小写（`Subject:` 同样有效），且每个字段都可以结合引号使用以进行短语搜索：

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ 支持四个字段：** `subject`, `body`, `from`, `to`。编写 `cc:someone` 或 `bcc:someone` **不是**字段搜索 —— 该术语会被视为普通文本并在所有字段中进行搜索。要查找仅在 Cc 中的收件人，请直接搜索其地址或姓名，不要加前缀。

### 组合使用 {#combining-everything}

过滤器和术语是累加的。此查询会找到来自任何保险发件人的电子邮件，且正文中包含 "car insurance" 精确短语，且主题中包含 "invoice"：

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

实际组合示例：

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### 会被忽略的字符 {#characters-that-are-ignored}

查询解析器会从单个术语中剥离字符 `& | ! ( ) : *`，因为它们在底层全文引擎中具有特定含义。如果您的搜索没有返回预期结果，请检查是否是特殊字符的原因。

## 📊 排序与分页 {#sorting-and-paging}

### 排序 {#sorting}

点击列标题进行排序。可排序列包括：

| 列 | 排序依据 |
|---|---|
| **Subject** | 主题行，按字母顺序 |
| **From** | 发件人 |
| **To** | 收件人 |
| **Date** | 发送日期 |

再次点击同一个标题会反转排序方向。默认是**由新到旧**。

### 每页数量 {#page-size}

选择每页显示的搜索结果数量：

**20** (默认) · **50** · **75** · **100** · **150**

较大的页面显示更多上下文，但渲染时间较长。使用 **Previous** / **Next** 控件在结果页之间移动；结果计数会告诉您总共有多少封电子邮件匹配。

> **ℹ️ 您的时区适用于显示的日期。** 显示的时间会从 UTC 转换为您配置的时区，因此您看到的日期与您的本地日历一致 —— 这在您搜索午夜前后时间时非常有用。

## 👁️ 处理结果 {#working-with-results}

每一行都会显示账户、发件人、主题、日期，以及当电子邮件带有附件时的 **📎 回形针图标**。回形针仅作为指示器 —— 它不是过滤器。

点击 **Details** 打开电子邮件：

- 完整正文 (HTML 或纯文本)
- **Attachments**（附件），列出并提供下载每个附件的选项
- **Download as EML** —— 完整的消息，用于在其他地方使用
- **Restore** —— 将电子邮件复制回活动邮箱

有关将电子邮件还原到邮箱的操作，请参阅 [Mailbox Migration](MailboxMigration.md)。

## ✅ 一次处理多个结果 {#acting-on-many-results-at-once}

可以批量选择并处理结果。开启 **Selection Mode** 以显示复选框，然后：

- **Select All** / **Deselect All** 用于当前页面
- **Select all {n} matching** —— 当前搜索在所有页面中的每一个结果（当结果跨越多个页面时显示）
- **Copy Selected** —— 还原到邮箱
- **Export Selected** —— 导出为 MBOX 或 EML
- **Delete Selected** —— 从归档中删除

在操作过程中，计数器会显示 *"{selected} of {total} selected"*。

### 选择限制 {#the-selection-limit}

> **⚠️ 一次最多只能选择 250 封电子邮件**（可通过 `Selection:MaxSelectableEmails` 配置）。超过此限制时，界面会显示 *"Maximum selections reached (250)"* 和 **Too Many — Split Required**。

存在此限制是因为每个操作都必须持有受影响的消息。提示 *"Split larger operations into multiple batches"* 是预期的解决方法：将结果分成 250 封或更少的组进行处理。

**Select all {n} matching** 同样遵循此限制。当搜索匹配的电子邮件超过限制时，它会选择当前排序顺序中的前一部分，并显示 *"Maximum selections reached"* 通知。缩小搜索范围（日期范围、文件夹、账户）以针对剩余部分进行操作。

尝试超过限制时会显示：*"Too many emails selected. Maximum allowed is 250 emails. Please reduce your selection or split into multiple operations."*

### 删除过滤器的所有匹配项 {#deleting-every-match-of-a-filter}

*Reset* 旁边的 **Delete all matches** 按钮会删除所有匹配当前搜索的电子邮件，而不仅仅是当前页面或选中的内容。该功能面向管理员和自我管理人员，并作为后台作业运行，因此不受 250 封电子邮件选择限制的影响。

确认对话框会显示多少封电子邮件可以被删除，以及多少封被保留策略锁定。您必须输入该数字才能启用确认。如果在此期间结果集发生了变化（例如因为同步增加了邮件），服务器会重新计数并中止操作。

被锁定的电子邮件永远不会通过此操作删除；它们会在作业状态中被报告为已跳过。`DeletionPolicy:MaxBulkDeleteEmails` (默认 100,000) 限制了单个作业。如果过滤器匹配了更多电子邮件，则该请求会被拒绝并要求缩小范围。

该作业会出现在 [Background Jobs](BackgroundJobs.md) 页面，并可以在那里取消。

### 直接处理 vs 后台作业 {#direct-processing-vs-background-job}

对于 **Copy** 和 **Export**，您可以选择运行方式：

| 选项 | 行为 |
|---|---|
| **Process on this page (wait for completion)** | 浏览器等待完成；适用于少量电子邮件 |
| **Process in background (monitor separately)** | 作为作业运行；您可以关闭浏览器 |

导出始终在后台运行：

> *"The export will run in the background and you will be able to download the result from the Jobs page once completed."*

在 [Background Jobs](BackgroundJobs.md) 页面查找正在运行和已完成的作业。

> **⚠️ 删除是永久性的。** 确认信息非常明确：*"Are you sure you want to permanently delete {n} selected email(s)? This action cannot be undone and each deletion will be logged for audit purposes."* 删除操作会被记录在访问日志中。

## ⚡ 性能与大型归档 {#performance-and-large-archives}

搜索由 PostgreSQL 全文索引支持，因此快速搜索与慢速搜索的区别通常在于查询的**选择性 (selective)**。

**建议：**

- 当您大致知道电子邮件到达的时间时，添加 **date range** —— 这是最有效的缩小范围方法
- 当您知道邮件存放位置时，限制在某个 **account** 或 **folder**
- 使用**多个单词**而不是一个非常短的单词
- 使用字段搜索 (`subject:`, `from:`) 来排除无关匹配

**避免：**

- 使用单个短前缀，如 `re` —— 作为前缀搜索，它会匹配归档中的极大比例
- 使用非常宽泛的日期范围结合一个常用词
- 在可以收紧搜索范围时浏览大量页面

> **💡 如果搜索感觉很慢，通常日期范围就是答案。** 将范围限制在一个月内通常能将数秒钟的搜索变为瞬时完成，因为索引可以跳过大部分归档。

## ❓ 故障排除 {#troubleshooting}

| 现象 | 原因 | 做法 |
|---|---|---|
| 没有结果，但您预期会有 | 搜索词过于具体，或有拼写错误 | 尝试减少单词；检查拼写 |
| `cc:name` 找不到有用的内容 | `cc` 不是受支持的字段 | 不带前缀搜索名称 |
| 特殊字符破坏了查询 | `& \| ! ( ) : *` 会被剥离 | 删除或替换该字符 |
| 结果过多 | 常用词或短前缀 | 添加日期范围或字段搜索 |
| *"Too Many — Split Required"* | 选择了超过 250 封电子邮件 | 减少选择或拆分操作 |
| 导出缺失 | 正在作为后台作业运行 | 检查 [Background Jobs](BackgroundJobs.md) |
| 结果看起来像是丢失了 | 账户权限 | 确认邮箱已分配给您的用户 |
| 日期显示偏移 | 时区转换 | 检查您的时区设置 |

## 📚 相关文档 {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — 将结果还原到邮箱
- [Background Jobs](BackgroundJobs.md) — 监控导出和还原
- [Access Logging](Logs.md) — 搜索和删除的审计追踪
- [REST API](API.md) — 通过 API 访问相同的归档
- [MCP Server](MCP.md) — 从 AI Agent 进行搜索
- [Retention Policies](RetentionPolicies.md) — 电子邮件何时离开归档
