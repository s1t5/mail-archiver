# ⚙️ 后台作业 {#background-jobs}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

Mail Archiver 中的大多数工作并不需要您实时等待。同步邮箱、导入 MBOX 文件、恢复数千封电子邮件或导出存档可能需要几分钟到几小时不等。这些任务作为 **后台作业 (background jobs)** 运行 —— 您启动它们后，即使关闭浏览器，它们也会继续运行。

**后台作业** 页面显示了当前正在运行的任务、已完成的任务以及出错的任务。当某个任务耗时超出预期，或者同步以错误结束时，应在此处查看。

> **ℹ️ 管理员页面。** 后台作业需要管理员权限，可通过主菜单访问。普通用户看不到此页面 —— 他们仍然可以启动作业（例如批量恢复），但监控工作在此处进行。

## 🗂️ 作业类型 {#job-types}

该页面按类型对作业进行分组。每个部分显示最多 **20 个最近的** 作业，运行中和排队中的作业会排在最前面 —— 这样旧的作业就不会被新的作业挤出视野。

| 章节 | 涵盖内容 | 启动来源 |
|---|---|---|
| **同步作业 (Synchronization Jobs)** | 从邮件服务器获取新邮件 | 自动计划，或账户上的 *Sync now* |
| **MBox 导入作业 (MBox Import Jobs)** | 导入 `.mbox` 文件 | 邮件账户 → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **EML 导入作业 (EML Import Jobs)** | 导入 `.eml` 文件 / ZIP 压缩包 | 邮件账户 → [Import EML](Import-EML-MBox.md#-import-eml) |
| **批量恢复作业 (Batch Restore Jobs)** | 将选定的电子邮件复制回邮箱 | 存档 → 选择电子邮件 → Batch Restore |
| **账户导出作业 (Account Export Jobs)** | 导出整个账户 (mbox / EML) | 邮件账户 → [Export](Export-Account.md) |
| **选定电子邮件导出作业 (Selected Emails Export Jobs)** | 导出精选的一组电子邮件 | 存档 → 选择电子邮件 → Export |
| **电子邮件删除作业 (Email Deletion Jobs)** | 从服务器或存档中删除电子邮件，包括基于过滤器的批量删除 | 保留策略、手动删除，或 存档 → *Delete all matches* |
| **账户删除作业 (Account Deletion Jobs)** | 移除整个邮件账户 | 邮件账户 → Delete |
| **卸载 (Offload)** | 将特定日期范围内的邮件移动到另一个账户 | 邮件账户 → Offload |

## 📊 阅读作业行 {#reading-a-job-row}

每一行都包含相同的一组列：

| 列名 | 含义 |
|---|---|
| **Job ID** | 短标识符 —— 报告问题时请引用此 ID |
| **Account** | 作业正在处理的邮箱 |
| **Status** | 当前状态，参见下表 |
| **Progress** | 作业进度，在已知的情况下显示已处理/总数 |
| **Created** | 作业进入队列的时间 |
| **Started** | 实际开始运行的时间 |
| **Completed** | 结束时间（运行时为空） |
| **Duration** | 耗时 |

某些作业类型会在进度列中添加计数器：

- **Success** / **Failed** — 成功处理和处理失败的电子邮件数量
- **Skipped (duplicates)** — 存档中已存在的电子邮件，不会重复导入
- **Recovered** — 在服务器报告丢失后重新获取的消息
- **Malformed (skipped)** — 无法解析并被跳过的消息

### 状态值 {#status-values}

| 状态 | 含义 |
|---|---|
| **Queued** | 等待开始 —— 另一个作业正在运行，或工作进程正忙 |
| **Running** | 正在执行 |
| **Completed** | 成功完成 |
| **CompletedWithErrors** | 已完成，但部分项目失败 (MBox 导入) |
| **Failed** | 以错误结束 |
| **Cancelled** | 被用户停止 |
| **Rate Limited** | *由于带宽限制，同步已暂停。将自动恢复。* |
| **Timed Out** | 同步超过了配置的时间限制并已停止 |

> **ℹ️ "Rate Limited" 和 "Timed Out" 仅针对同步状态。** 它们看起来像失败，但并非如此：带宽配额允许后，受限的同步会自动恢复；超时同步会在下次运行时从其检查点继续。请参阅 [Rate Limit Handling](RateLimitHandling.md) 和 [Mail Synchronization](Synchronization.md)。

### 为什么进度可能停留在 0% {#why-progress-can-sit-at-0}

进度是按 **完成的文件夹** 衡量的，而不是按单封电子邮件衡量的：

> *进度按完成的文件夹衡量。一个大型文件夹（例如 INBOX）在完成之前，进度条将保持在 0%。*

在 INBOX 存放了大部分邮件的邮箱中，进度条可能会长时间保持在 0%，然后突然跳跃。这是正常现象 —— 作业正在运行，只是尚未完成第一个大型文件夹。

## 🔄 页面自动刷新 {#the-page-refreshes-itself}

页面打开时每 **30 秒** 重新加载一次，因此您可以将其留在标签页中并观察进度，无需进行任何点击。标签会显示数据最后一次更新的时间。

一旦您点击链接或按钮，重新加载就会被取消 —— 否则您的点击可能会落在正在被替换的页面上。

**可配置：** `appsettings.json` 中的 `Jobs:RefreshSeconds` (默认 `30`)。
将其设置为 `0` 以关闭自动重新加载。每次重新加载都会从所有作业源重建页面，因此在拥有许多账户的安装环境中，过短的间隔会带来真实的负载 —— 并且该负载会随着打开的标签页数量而倍增。

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 取消作业 {#cancelling-a-job}

支持取消的作业会显示 **Cancel Job** 按钮，并询问：

> *Are you sure you want to cancel this job?*

导入作业使用 **Cancel Import** / *Are you sure you want to cancel this import job?* 的措辞。

**同步、批量恢复、选定电子邮件导出和电子邮件删除** 作业可以取消。作业会在当前项目完成后立即停止；状态变为 **Cancelled**。

> **⚠️ 取消操作不是回滚。** 已经导入、恢复、导出或删除的电子邮件将保持原样。取消只是停止作业执行 *更多* 操作 —— 它不会撤销已完成的操作。对于删除作业，这意味着已被移除的电子邮件将永久消失。

账户和 MBox 导入作业无法在此页面取消；请前往账户自身的导入页面停止它们。

## ⚠️ 同步失败与 "Acknowledge Failures" {#sync-failures-and-acknowledge-failures}

当同步以 **Failed** 状态结束时，作业行会提供 **Acknowledge Failures** 选项。这是页面上唯一一个会改变未来行为的操作，因此值得理解。

每次同步运行都会从上次停止的地方开始，使用 **checkpoint (检查点)**。如果同步在某些电子邮件处失败，检查点会停留在这些电子邮件之前，以便下次运行再次尝试。这是安全的默认设置 —— 不会有任何内容被静默跳过。

但如果失败是永久性的（例如损坏的消息、服务器拒绝提供的文件夹），后续的每次同步都会重试它们，导致同步在同一位置持续失败。

**Acknowledge Failures** 通过将检查点推进到失败的电子邮件之后来解决这种死锁。确认信息明确说明了后果：

> *This will advance the sync timestamp to the completion time of this job, so the next sync will be a normal incremental sync. The failed emails will not be retried. Continue?*

成功后您会看到：*"Sync timestamp has been advanced. The next sync will be incremental."*

> **⚠️ 只有在查看过失败详情后才进行确认。** 确认后，这些电子邮件 **不会** 被重试 —— 除非您通过其他方式获取，否则它们将从存档中缺失。请先检查失败详情，如果原因是配置错误，请在确认前修复它。有关检查点行为的详细信息，请参阅 [Mail Synchronization](Synchronization.md)。

确认操作会记录在访问日志中，标记为 *SyncAcknowledgeFailures*，并包含失败的电子邮件数量和账户，以便日后追溯。

## 👁️ 您在此处无法进行的操作 {#what-you-cannot-do-here}

- **没有重试按钮。** 失败的作业无法在此页面重启 —— 请从您开始操作的地方（同步、导入、导出）重新开始。
- **无法编辑。** 作业参数在创建时即已固定。
- **每种类型仅显示最后 20 条。** 不会列出较旧的作业。如需长期记录，请查看访问日志和应用程序日志 —— 参见 [Access Logging](Logs.md) 和 [Docker Compose Logs](DockerComposeLogs.md)。

## ❓ 故障排除 {#troubleshooting}

| 现象 | 可能的原因 | 应对措施 |
|---|---|---|
| 作业停留在 **Queued** | 同一工作进程的另一个作业正在运行 | 等待；检查其他章节是否有长时间运行的作业 |
| 进度卡在 0% | 一个大型文件夹仍在处理中 | 正常现象 —— 参见 "为什么进度可能停留在 0%" |
| 同一账户反复出现 **Failed** | 单个电子邮件存在永久性错误 | 检查失败详情，修复原因，然后进行 Acknowledge |
| 同步显示 **Rate Limited** | 达到带宽配额 | 无需操作 —— 它会自动恢复 |
| 同步显示 **Timed Out** | 邮箱大于时间限制 | 下次运行时会从检查点继续；参见 [Synchronization](Synchronization.md) |
| MBox 导入显示 **CompletedWithErrors** | 部分消息格式错误 | 计数器会显示数量；其余部分已成功导入 |
| 重新加载后作业消失 | 掉出了 "最后 20 条" 的窗口 | 检查应用程序日志以获取其最终状态 |
| 工作时页面自动刷新 | 每 30 秒自动刷新 | 将 `Jobs:RefreshSeconds` 设置为 `0` |

## 📚 相关文档 {#related-documentation}

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — 检查点、超时同步
- [Rate Limit Handling](RateLimitHandling.md) — 带宽配额与自动恢复
- [Access Logging](Logs.md) — 谁做了什么，包括作业操作
- [Batch Restore](MailboxMigration.md) — 在邮箱之间移动电子邮件
- [Date-Windowed Offload](Offload.md) — 卸载作业
- [Docker Compose Logs](DockerComposeLogs.md) — 阅读应用程序日志
