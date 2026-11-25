# 📧 Original Message Storage {#original-message-storage}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

默认情况下，归档保留电子邮件的**解析部分**：标头、文本和 HTML 正文、附件。导出的 `.eml` 是从这些字段重建的。

这足以阅读电子邮件，但它并不是**接收时**的原始消息。传输编码、MIME 边界和标头折叠（header folding）与原始消息不同，因此重建的消息不再能通过其 DKIM 签名验证，且垃圾邮件扫描器的评分也会有所不同。

启用原始消息存储后，归档将在保留解析字段的同时，保留完整的消息原文。存储的原始消息是归档可以证明其已被投递的内容。

| | 仅解析字段 | 包含存储的原始消息 |
| --- | --- | --- |
| 标头、正文、附件 | ✅ | ✅ |
| 字节级精确的消息 | ❌ | ✅ |
| DKIM 签名验证 | ❌ | ✅ |
| 取证用途 (法庭、审计) | 有限 | ✅ |

## 🎯 🎯 Where the original is served {#where-the-original-is-served}

一旦存储，只要需要完整消息的地方，都会返回原始邮件：

| Place | What happens |
| --- | --- |
| **Export EML** button on the email detail page | 如果存在存储的原始邮件，则提供存储的原始邮件；对于未存储原始邮件的存档邮件，则回退到重构版本。 |
| **Restore to a mailbox** | 如果存在存储的原始邮件，则追加存储的原始邮件；对于未存储原始邮件的存档邮件，则回退到重构版本。 |
| `GET /api/v1/emails/{id}/raw` | REST API download，参见 [REST API guide](API.md#download-original-message)。 |
| `get_email_raw` | MCP tool，参见 [MCP guide](MCP.md#get_email_raw)。 |

响应会告知你获取的是哪种形式，因此调用者无需检查字节即可区分真实的原始邮件与重构邮件：

| Signal | Meaning |
| --- | --- |
| `X-Raw-Message-Source: imap` | 来自 IMAP 服务器的精确副本 (`BODY[]` 交付时的状态)。 |
| `X-Raw-Message-Source: graph` | 精确副本，MIME content Exchange 返回的内容。 |
| `X-Raw-Message-Source: eml-file` | 精确副本，导入的 `.eml` 文件的内容。 |
| `X-Raw-Message-Source: serialized` | 解析后重新写入的消息。除了行尾为 CRLF 外，其余部分完全相同 (MBox import, IMAP recovery fetch)。 |
| `X-Raw-Message-Sha256` | 存储的原始邮件的 SHA-256 值。 |
| `X-Raw-Message-Reconstructed: true` | 未存储原始邮件；消息是重建的。没有校验和标头。 |
| `X-Raw-Message-Storage` | 读取原始邮件的后端 (`postgres` 或 `filesystem`)。 |

文件名也会说明这一点：存储的原始邮件为 `email-{id}-original-...eml`，重构版本为 `email-{id}-...eml`。

当存储的原始邮件**校验和失败**时，导出和还原操作将被拒绝并返回错误消息。如果改为返回重构版本，则会掩盖存档副本已被篡改的事实，而这正是存档系统绝对不能做的事情。

还原操作会读取存储的原始邮件并通过提供商的客户端进行追加，因此邮箱会收到原始的标头和 MIME 结构。在 IMAP 上，客户端会再次序列化消息，因此无法保证线缆上传输的字节流完全一致。在 Microsoft 365 上，存档字节会原封不动地传输，因为 Graph 接受 MIME 形式的消息。存档副本本身永远不会被修改。

## ⚙️ Settings {#settings}

所有设置都位于 `RawMessages` 部分，或者作为带有 `RawMessages__` 前缀的环境变量。

| 设置 | 默认值 | 描述 |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | 存储每封新归档电子邮件的原始消息。在启用此设置之前归档的电子邮件没有原始消息。 |
| `RawMessages:MaxMessageSizeMb` | `50` | 大于此大小的消息在归档时将不包含其原始消息。`0` = 无限制。 |
| `RawMessages:IncludeGraph` | `true` | 同时也为 Microsoft 365 (Graph) 账户存储原始消息。每条消息会增加一次额外的 Graph 请求，这会占用节流预算。 |
| `RawMessages:Storage` | `postgres` | 消息字节的存储位置：`postgres`（在数据库中）或 `filesystem`（在归档文件旁边的 gzip 文件）。参见 [Storage backend](#storage-backend)。 |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | 消息文件写入的目录，相对于应用程序的内容根目录。在 Docker 中请将其指向挂载的卷。 |
| `RawMessages:FileSystem:ShardDepth` | `2` | 电子邮件 ID 分布的两个字符目录层级数量。`2` 会产生 10,000 个目录，`0` 是扁平的。这可以在大型归档中保持较低的每目录文件计数。 |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | 在每次下载时重新计算文件的 SHA-256，并将其与归档时记录的校验和进行比较。如果校验不匹配，下载将以 HTTP 500 错误失败，而不是提供被篡改的字节。 |

### Sizing {#sizing}

存储容量的增长量大约等于每封邮件的大小（包括其附件），且经过 gzip 压缩。附件在存储的原始消息中**不会**进行去重，因此带有大型附件的电子邮件会产生两次该附件的成本：一次在去重后的附件存储中，一次在原始消息内部。请按不含原始消息的归档数据库大小的约 **两倍** 进行规划。

启用此设置不会回填现有的邮件。只有新归档的电子邮件才会获得原始消息。

## 💾 Storage backend {#storage-backend}

`Storage` 设置决定了字节存储的位置。无论配置何种后端，**元数据行始终保留在
数据库中**，并持有 SHA-256、大小、捕获源以及（对于文件）相对路径。该行让归档能够证明
存储的消息应当包含的内容。

### postgres (default) {#postgres-default}

压缩后的消息存储在 `mail_archiver."RawMessages"` 的 `Content` 列中。
这是归档一直以来的行为，且不需要额外的卷。

当数据库已经是您的备份单元，且您希望只需备份和恢复一个对象时，请选择此项。

### filesystem {#filesystem}

消息被写入 `{BasePath}/{xx}/{yy}/{id}.eml.gz`，其中 `xx`/`yy` 是由电子邮件 ID 派生的。
数据库行仅保留元数据。

当归档规模大到原始文件会占据数据库主要体积时，或者当您想将消息文件放在更便宜或独立的存储上时，请选择此项。

!!! danger "在 Docker 中挂载卷"
    默认的 `BasePath` (`RawMessages`) 解析为容器**内部**的 `/app/RawMessages`。
    如果没有绑定挂载（bind mount），该目录将属于容器的可写层，因此每次重新创建容器——包括 `docker compose down && docker compose
    up -d`、镜像更新，或带有临时层的宿主机重启——都会带走所有存储的原始文件，而数据库行却得以幸存。

    使用 filesystem 后端时，务必将目录挂载到宿主机：

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    宿主机路径（此处为 `./raw-messages`）才是您需要备份的内容。容器路径必须
    与 `RawMessages:FileSystem:BasePath` 匹配，或者将 `RawMessages:FileSystem:BasePath` 设置为您选择的
    挂载点。如果您移动了挂载点，请保持 `BasePath` 指向它：存储的 `StoragePath` 值是相对的，因此它们仍能持续解析。

### Switching backends {#switching-backends}

读取操作遵循每行记录的存储方式，而非配置的设置。在您更改 `Storage` 后，
早期归档的消息仍可从其原处下载。
该设置仅影响**新归档**的邮件。

要迁移已存储的内容，请运行存储迁移。请参阅
[CLI commands](CLI.md#moving-stored-original-messages) 获取完整参考：

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

迁移保证的内容：

- 在复制之前，每条消息都会根据其记录的校验和进行验证。校验失败的消息将留在原处并报告：损坏的原始文件绝不会被传播，且其仅存的副本绝不会被删除。
- 只有在移动文件的数据库事务提交后，文件才会被删除，因此中断的运行不会导致唯一副本丢失。
- 运行过程可恢复。工作任务通过 `StorageType` 列进行选择，因此重新启动命令将继续处理尚未完成的任务。
- 如果有任何消息失败，退出码为 `1`，否则为 `0`。

归档工作并不强制要求进行迁移。迁移仅用于将现有的字节移动到另一个后端。

## 🛟 备份与恢复 {#backup-and-restore}

=== "postgres backend"
    无需额外操作。原始数据是数据库的一部分，包含在常规的
    dump 中，参见 [Backup and Restore](BackupRestore.md)。

=== "filesystem backend"
    请将 `BasePath` 目录**连同**数据库 dump 一起进行备份。

    如果不备份文件而仅进行数据库恢复，会导致存档中的某些行因无法提供内容而失效。
    此时下载请求会返回 HTTP 404 而不是返回错误内容，且邮件本身仍可通过其解析后的字段进行读取。

## 🗑️ Deleting an email {#deleting-an-email}

删除电子邮件会将其原始文件一并移除：保留规则、手动单条或批量删除，以及整个邮件账户的删除，都是作为同一操作的一部分来移除该文件的。使用 `postgres` 后端时，无需进行额外操作，因为字节存储在级联删除时已经移除的行内。

文件是在电子邮件行提交**之后**删除的，绝不会在之前。这两个步骤不能合并为一个事务，因为文件不属于数据库事务的一部分，所以其中一个必须放在第二个。这种顺序是安全的：例如，如果删除操作在锁定记录上失败或回滚，则原始文件保持不变；如果在两个步骤之间发生崩溃，则会留下一个没有对应行的文件，下文提到的清理程序会回收该文件。如果采用另一种顺序，则会破坏仍然存在的电子邮件的原始文件。

**孤儿文件会被收集。** 任何遗漏的内容，包括在此清理机制存在之前的删除操作所留下的文件，都会通过 `BasePath` 的定期扫描来移除，该扫描会将每个文件与行进行比对，并删除没有任何电子邮件引用的文件：

| Setting | Default | Meaning |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | 开启或关闭扫描 |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | 运行频率 |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | 比此时间更短的文件永远不会被触碰 |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | 第一次运行前的延迟，以便先完成待处理的存储迁移 |

宽限期是确保扫描安全的关键：导入操作会在提交指向该文件的行之前先写入文件，因此刚刚写入的文件不应被误认为是孤儿文件。由于写入中断而产生的半写入 `.tmp` 文件也会根据同一规则被收集。使用 `postgres` 后端时，扫描不会执行任何操作，甚至不会遍历目录。

## 🔍 验证归档文件 {#verifying-an-archive}

归档时记录的校验和是实现验证的基础。

- **下载时**（`VerifyChecksumOnRead=true`，默认设置）会对文件进行哈希处理并进行比较。如果校验不匹配，将以 HTTP 500 错误拒绝下载。
- **在存储迁移时**，每个消息在双向复制之前都会进行验证。

由于元数据行存储在数据库中，而字节可能存储在磁盘上，因此校验和比较是发现文件在应用程序外部被修改的唯一方法。

## ⚠️ 须知事项 {#things-to-know}

- **启用并不意味着回填。** 只有在切换后归档的邮件才会获得原始邮件。
- **缺失原始邮件会被记录为错误。** 当捕获被拒绝（见上文 `MaxMessageSizeMb`，无可用捕获）或写入配置的后端失败时，日志会携带错误信息，包含邮件 ID 和原因。该邮件将保留在归档中，但没有其原始邮件；既没有自动回填，也没有重试机制。
- **详情页会标记缺失的原始邮件。** 在 `RawMessages:Enabled` 模式下，详情页会在标头上方显示一条通知。它会查询存储后端，因此消失的文件系统文件也会被标记。邮件列表不会在每行显示徽章；否则会占用状态列过多的空间。
- **重构的内容并非原始消息。** 对于没有存储原始邮件的邮件，每个下载路径都会返回一个重构的消息，并通过 `X-Raw-Message-Reconstructed: true` 进行说明。
- **文件系统意味着两个备份单元。** 数据库和消息目录必须一起恢复，否则行记录虽然存在，但会丢失其内容。
- **`VerifyChecksumOnRead` 会消耗 CPU。** 每次下载大型消息时都会进行解压缩和哈希计算。除非你已经测量到了问题，否则请保持开启；这是防止磁盘上发生静默篡改的唯一手段。
- **存储的原始邮件不会进行去重。** 出现在许多邮件中的附件会在附件存储中存储一次，但在每个原始邮件内部会各存储一次。
- **删除也会移除原始邮件。** 通过保留策略或手动删除的邮件，其存储的原始邮件（包括文件和行记录）也会随之删除。归档不再列出的任何内容都不会被保留。

## 🔗 相关链接 {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - 完整的存储迁移说明
- [Setup and Parameters](Setup.md) - 所有的配置参数
- [REST API](API.md#download-original-message) - `/raw` 端点
- [MCP Server](MCP.md#get_email_raw) - `get_email_raw` 工具
- [Backup and Restore](BackupRestore.md) - 备份内容说明
- [Attachment Deduplication](AttachmentDeduplication.md) - 附件存储方式说明
