# 💾 Per-Account Storage Display {#per-account-storage-display}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

Dashboard 的“Account Overview”表格和 MailAccounts 的“Show All”表格包含一个 **Storage** 列。该列显示每个账户使用的数据库空间大小 —— 包括**每封归档邮件的所有字段**以及**所有附件**。

## ⚙️ How it works {#how-it-works}

存储值被**缓存**在 `AccountStorageCache` 数据库表中。这意味着 Web 页面读取的是预先计算好的值，而不是每次打开 Dashboard 或账户列表时都运行一次高开销查询。缓存由一个独立的后台服务 (`AccountStorageRefreshService`) 保持更新。

对于每个账户，该服务会要求 PostgreSQL 对 `ArchivedEmails` 表中该账户每一行每一列的 `pg_column_size` 进行求和。这涵盖了**邮件的所有字段**（Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash 等），包括 PostgreSQL 的压缩和行外存储 (TOAST)。行外值的大小是从其 TOAST 指针中读取的，因此计算过程仅扫描表本身，而永远不会加载邮件正文。附件部分作为 `EmailAttachment.Size` 的逻辑总和单独添加。

刷新服务在以下三种情况下更新缓存：

1. **Startup backfill** — 在新安装后或更新到引入此功能的版本后，每个账户的初始缓存值都为空。该服务会逐个遍历所有账户并计算第一个值。
2. **Daily refresh** — 每天在配置的时间（默认为 `02:30`）进行一次全量重新计算，作为安全保障。
3. **Immediate refresh** — 在邮件同步、EML 导入、MBOX 导入或保留策略删除后，受影响的账户会立即重新计算，以确保显示的值保持最新。

## 🤔 Why "0 B" is shown at first {#why-0-b-is-shown-at-first}

当功能首次启用或创建新账户时，该账户的缓存行尚不存在。刷新服务在后台运行并**逐个**计算账户，每个账户之间会有短暂的延迟，以防止超大型归档导致数据库过载。
在服务处理完该账户之前，UI 会显示 **"0 B"** 作为占位符。这完全正常。一旦后台服务完成了该账户的计算，下次加载或刷新页面时就会显示真实数值。
对于现有的庞大归档，回填过程可能需要几分钟或更长时间。该服务会在专用表 (`AccountStorageBackfillState`) 中跟踪其进度，因此如果应用在回填过程中重启，它将从上次停止的地方继续 —— 账户不会被重复计算。

## 🔧 Configuration {#configuration}

可以通过 `appsettings.json` 或通过环境变量来调整此功能。请参阅 [Setup.md](Setup.md) 中的 **Account Storage Settings** 部分以了解所有可用选项，包括：

- `AccountStorage__Enabled`: 开启或关闭刷新服务（默认为 `true`）。
- `AccountStorage__DailyExecutionTime`: 每日全量刷新的时间（默认为 `02:30`）。
- `AccountStorage__BackfillDelayMs`: 启动回填期间账户之间的停顿时间（默认为 `5000` ms）。
- `AccountStorage__RefreshBatchDelayMs`: 每日刷新期间账户之间的停顿时间（默认为 `1000` ms）。
- `AccountStorage__CommandTimeoutSeconds`: 每个账户存储计算的数据库命令超时时间（默认为 `300` 秒）。对于 `pg_column_size` 扫描耗时较长的超大型归档，请增加此值。

> 💡 **Tip**: 您**不需要**为了使用此功能而启用 `DatabaseMaintenance__Enabled`。存储刷新服务是独立运行的。
