# 📤 导出邮件账户 (EML / MBox) {#exporting-a-mail-account-eml-mbox}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

账户导出功能会将**一个账户的所有邮件**写回一个可下载的 ZIP 文件。它是[网页导入](Import-EML-MBox.md)的对应操作：您将获得一个可移植的账户存档，其格式可以被任何邮件客户端或迁移工具读取。

提供两种格式：

| 格式 | 您将获得的内容 | 典型用途 |
|--------|--------------|-------------|
| **EML** | 组织在文件夹中的单个 `.eml` 文件 | 重新导入邮件客户端、单条邮件处理、长期可读性 |
| **MBox** | 传统的邮箱文件 —— **每个文件夹一个文件** | 读取 MBox 的工具和客户端、批量文本处理 |

在这两种情况下，邮件都以原始 RFC-822 邮件格式写入，因此**导出内容包含附件**。

> 💡 **提示**：若要仅导出选定的邮件而非整个账户，请使用 [Mail search](Search.md#-acting-on-many-results-at-once) 中的批量操作。

## 🧭 访问路径 {#access}

1. 在主菜单中打开 **Mail Accounts**。
2. 点击账户行上的 **⋮** (更多操作) 按钮并选择 **Export**。相同的入口也位于账户的 **Details** 页面上。

导出需要 **admin** 或 **self manager** 角色 —— 普通用户可以看到该入口但无法使用。导出始终涵盖整个账户；没有日期或文件夹过滤器。

## 🖥️ 使用导出页面 {#using-the-export-page}

该页面会显示账户的**总计**、**传入**和**传出**邮件计数，因此您可以在开始之前查看任务规模。然后：

1. 选择格式 —— **EML** 或 **MBox**。
2. 点击 **Start Export**。

导出任务在**后台运行**。您将被引导至一个状态页面，显示任务 ID、所选格式、时间戳以及已处理/总邮件计数。

### 任务状态 {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

当任务达到 **Completed** 状态时，状态页面上会出现**下载按钮**。同一个任务也会列在 [Jobs](BackgroundJobs.md) 页面上。

> ⚠️ **导出文件会自动清理。** ZIP 文件将在**下载成功后**以及在任何情况下 **7 天后**被删除。请在文件仍可提供时及时下载 —— 存档本身不受影响，您稍后只需重新开始一次新的导出即可。

## ⚠️ 注意事项 {#things-to-watch-out-for}

- **大型账户需要较长时间。** 较大的邮箱可能需要几分钟时间，且根据邮件量和附件的大小，ZIP 文件可能会变得非常大。由于任务在后台运行，您可以关闭浏览器并在稍后回到状态页面。
- **服务器磁盘空间。** ZIP 文件在您下载之前会在服务器上进行组装，因此请确保存档旁边有足够的可用空间。
- **"Export file not found"** —— 任务尚未完成，或者文件已被下载或被 7 天清理机制移除。请开始新的导出。
- **没有过滤选项** —— 导出是针对每个账户的“全有或全无”操作。如需导出子集，请使用搜索页面的导出功能。
- **并非合规性导出。** 对于需要带有索引文件的审计就绪型数据介质移交，请改用 [Audit Data Export](AuditExport.md) —— 它会生成审计工具所预期的表格化海量数据格式包。

## 🔗 相关文档 {#related-documentation}

- [Importing Mail in the Web UI](Import-EML-MBox.md) — 相反方向的操作
- [Mail search](Search.md) — 导出选定的邮件
- [Background Jobs](BackgroundJobs.md) — 任务列表、进度和取消
- [Audit Data Export](AuditExport.md) — 审计就绪的导出包
- [CLI commands](CLI.md) — 从挂载目录导入
