# 🧬 附件去重 {#attachment-deduplication}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

邮件附件以二进制负载的形式存储在数据库中。**同一个文件**经常被附加到许多电子邮件中（签名中的公司 Logo、转发的 PDF、新闻稿、合同等...）。如果没有去重机制，每个附件都会保留这些字节的独立副本，这会浪费大量的数据库空间。

**附件去重**将每个*唯一的*负载精确地存储在一次内容寻址表中（通过字节的 SHA-256 哈希值作为键），并让所有相同的附件都引用这一个共享副本。这通常能显著减少附件密集型存档的存储占用。

## ⚙️ 工作原理 {#how-it-works}

### 新附件 {#new-attachments}
每当归档一封新邮件（IMAP、Microsoft 365 / Graph、EML 导入、MBOX 导入...）时，附件负载会被哈希处理并仅存储一次。如果相同的负载已经存在，新附件只需引用现有的内容行。这在数据层透明地完成，因此涵盖了所有的归档路径。

### 现有数据（迁移） {#existing-data-migration}
对于在引入此功能**之前**就已经归档了附件的安装实例，一个**健壮且可恢复的后台迁移**会将现有的内联负载转换为共享内容行：

- 它**分批次**处理附件，每个批次都在其独立的事务中运行。
- 进度被持久化在一个专门的状态表中，因此迁移**在应用程序重启后可以恢复**，并从上次停止的地方继续。
- 它是**幂等的** —— 已迁移的附件永远不会被再次触碰，即使在崩溃或重启之后也是如此。
- 它在后台运行，不会阻塞应用程序的正常使用。

迁移完成后会记录一条完成消息，例如：

```
Attachment Deduplication migration COMPLETED. 36199 attachments processed in 1.6 minutes.
```

在下次启动时，它会检测到没有剩余任务并保持空闲。如果出现任何未去重的附件（例如在恢复旧备份后），它会自动恢复并迁移它们 —— 系统始终会收敛到完全去重的状态。

### 垃圾回收（始终开启） {#garbage-collection-always-on}
当邮件（以及附件）被删除时，共享负载可能会变成无引用的状态。内置的垃圾回收会移除这些孤立的内容行。

> 🧹 **孤立清理始终运行**，且独立于 `DatabaseMaintenance` 功能。它在迁移完成后立即运行一次，然后定期（每 `OrphanCleanupIntervalHours`，默认为 12 小时）运行。当 `DatabaseMaintenance__Enabled=true` 时，同样的清理工作还会在每日 `VACUUM` 之前运行，从而物理回收释放的空间。

### 迁移后的磁盘占用（重要） {#disk-usage-after-the-migration-important}

> 📈 **仪表板上的“存储”值在迁移后可能会短暂上升**（例如 3.5 GB → 5.3 GB）。这是预期现象，并非 Bug。
>
> 仪表板报告的是**物理**数据库大小 (`pg_database_size`)。当迁移将负载移入共享存储时，它会将旧的内联副本设为 `NULL`，但 PostgreSQL (MVCC) 会将这些旧字节作为*死元组 (dead tuples)* 保留，直到表文件被重写 —— 因此在某一时刻，数据库会同时包含旧副本和新副本。
>
> 为了自动解决此问题，应用程序会在**迁移完成且没有同步任务运行时**，对附件表执行一次性的 `VACUUM FULL`。这会重写表并将释放的空间归还给操作系统；之后报告的大小会降至**低于**原始大小。它仅运行一次（通过 `ReclaimedAt` 标记进行跟踪），具备重启安全性，并且只要同步任务正在进行，它就会推迟执行（在空闲时会自动重试）。

>
> ⚠️ `VACUUM FULL` 在其运行期间会锁定附件表（期间无法读取/写入附件），并且重写操作需要一定的可用磁盘空间。如果你想手动触发，请运行：
> ```sql
> VACUUM (FULL, ANALYZE) mail_archiver."EmailAttachments";
> ```

## 🔧 配置 {#configuration}

这些设置是可选的；默认设置对大多数安装来说是合理的。另请参阅 [设置指南](Setup.md#-attachment-deduplication-settings)。

| 设置 | 默认值 | 描述 |
|---------|---------|-------------|
| `AttachmentDeduplication__BatchSize` | `200` | 每个事务迁移的现有附件数量。较大的值迁移速度更快，但会增加每个批次的内存/数据库负载。 |
| `AttachmentDeduplication__DelayBetweenBatchesMs` | `0` | 可选的批次间停顿时间 (ms)，用于在繁忙系统中限制数据库负载。 |
| `AttachmentDeduplication__StartupDelaySeconds` | `20` | 应用程序启动后迁移开始前的延迟时间 (s)（让架构迁移先完成）。 |
| `AttachmentDeduplication__OrphanCleanupIntervalHours` | `12` | 始终运行的孤立垃圾回收的间隔时间 (h)。 |
| `AttachmentDeduplication__CommandTimeoutSeconds` | `300` | 迁移批次操作的数据库命令超时时间（以秒为单位）。默认为 5 分钟。如果某个批次仍然超时，服务会自动以一半的批次大小进行重试。 |

示例 (`docker-compose.yml`):

```yaml
environment:
  - AttachmentDeduplication__BatchSize=200
  - AttachmentDeduplication__DelayBetweenBatchesMs=0
  - AttachmentDeduplication__StartupDelaySeconds=20
  - AttachmentDeduplication__OrphanCleanupIntervalHours=12
  - AttachmentDeduplication__CommandTimeoutSeconds=300
```

## ❓ 常见问题 {#faq}

**我可以禁用去重吗？**
不可以。它是一项核心存储功能，始终处于激活状态。你只能调整上述的批次大小和调度参数。

**迁移期间我的数据安全吗？**
安全。每个批次都是一个单一事务，且操作是幂等的。迁移过程中的崩溃或重启会回滚未完成的批次，并在下次启动时重新处理；不会丢失或重复数据。

**它会减慢归档速度吗？**
每个附件的哈希处理开销微乎其微。现有数据的单次迁移在后台运行，如果需要，可以通过 `DelayBetweenBatchesMs` 进行节流。

**迁移一直超时并从同一个游标处重启。我该怎么办？**
这可能会发生在非常大的数据库（数十万封邮件）上，其中包含 200 个附件且带有 SHA-256 哈希计算的单个批次超过了默认的命令超时时间。现在，当发生超时时，服务会自动以一半的批次大小进行重试。如果你仍然看到超时，可以手动降低批次大小和/或增加命令超时时间：

```yaml
environment:
  - AttachmentDeduplication__BatchSize=10
  - AttachmentDeduplication__CommandTimeoutSeconds=600
```

**我会节省多少空间？**
这完全取决于你的存档中包含多少重复附件。包含大量重复签名、Logo 或转发文件的存档获益最大。

---
