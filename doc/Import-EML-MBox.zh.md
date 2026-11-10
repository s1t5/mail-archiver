# 📥 在 Web UI 中导入邮件 (EML / MBox) {#importing-mail-in-the-web-ui-eml-mbox}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

除了 [命令行界面](CLI.md) 和 [CSV 账户导入](Account-Import.md) 之外，Mail Archiver 还可以**通过 Web 界面**导入现有的邮件存档。这里有两种上传表单：

| 表单 | 接受的文件 | 结果 |
|------|---------------|--------|
| **Import MBox** | `.mbox`, `.mbx` | 文件中的每条消息都会被导入到您选择的**一个**文件夹中 |
| **Import EML** | 包含 `.eml` 文件的 `.zip` | **ZIP 压缩包内的文件夹结构**决定了每条消息的存放位置 |

两种表单都会将消息归档到现有的 Mail Archiver 账户中，均以**后台作业 (background jobs)** 形式运行，并且都会**自动跳过重复项** —— 两次导入同一个文件不会创建第二个副本。

> 💡 **提示**：如果您的存档已经是服务器上的文件，请优先使用 [CLI](CLI.md)。它可以避免浏览器上传，对于数 GB 大小的存档是更好的选择。

## 🧭 访问方式 {#access}

1. 在主菜单中打开 **Mail Accounts**。
2. 使用账户列表上方的 **Import** 下拉菜单，选择 **Import MBox** 或 **Import EML**。

这些表单对**管理员 (admins)**（所有账户）和**自我管理人员 (self managers)**（分配给他们的账户）可用；请参阅 [User Management](UserManagement.md#-user-account-permissions)。只有**已启用 (enabled)** 的账户才会被提供作为导入目标 —— 禁用的账户无法被选中。

## 📨 Import MBox {#import-mbox}

MBox 是大多数邮件客户端用于导出文件夹以及 `mbox` 类型备份的格式。

| 字段 | 含义 |
|-------|---------|
| **MBox File** | 要上传的 `.mbox` / `.mbx` 文件 |
| **Target Account** | 接收消息的 Mail Archiver 账户 |
| **Target Folder** | 消息导入到的文件夹。从账户现有的文件夹列表中选择一个，或输入一个新名称 —— 导入过程中会**自动创建**尚不存在的文件夹。默认为 `INBOX`。 |

文件中的所有消息都会被导入到该单一目标文件夹中。MBox 文件本身不携带文件夹信息，因此不会重建原始邮箱的结构 —— 如果您需要此功能，请使用 EML/ZIP 路径。

## 🗂️ Import EML {#import-eml}

EML 导入要求提供一个 **ZIP 压缩包**，其条目为 `.eml` 文件。

- ZIP **根目录**下的 `.eml` 文件将被导入到 `INBOX`。
- **子文件夹**中的 `.eml` 文件将被导入到与该子文件夹**同名**的文件夹中。

因此，如下所示的 ZIP 文件：

```
mailbox.zip
├── 2024-invoice.eml            → INBOX
├── Archive/
│   └── old-message.eml         → Archive
└── Projects/
    └── 2026/
        └── kickoff.eml         → Projects/2026
```


可以恢复文件夹树以及消息。单个 `.eml` 文件可以通过先将其封装在 ZIP 中进行上传 (`zip emails.zip *.eml`)。

## ⚙️ 共同行为 {#shared-behaviour}

一旦您点击 **Start Import**，两种表单的行为都是相同的：

- **文件大小限制** — 通过 `Upload__MaxFileSizeGB` 配置（默认为 **10 GB**）。表单会在文件选择器旁边显示当前限制。
- **后台处理** — 上传会被排队作为一个作业；您无需一直保持浏览器开启。
- **重复处理** — 已经在存档中的消息会被跳过并单独计数。重复项通过 `Message-ID` 标头进行匹配。
- **格式错误输入** — 无法读取的文件会被单独跳过，并在状态页面报告，而不会中止整个导入过程。
- **进度** — 您会直接跳转到作业状态页面，该页面显示作业 ID、文件名、文件大小、状态、已处理/总消息数以及运行计数。同一个作业也会出现在 [Jobs](BackgroundJobs.md) 页面上。
- **取消** — 正在运行的导入可以从其状态页面取消。截至该时间点已导入的消息将保留在存档中。
- **审计追踪** — 导入操作会被写入 [access log](Logs.md)，并且随后会刷新每个账户的存储值（参见 [Per-Account Storage](AccountStorage.md)）。

### 作业状态 {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled`

完成的作业会报告导入了多少条消息、失败了多少条以及作为重复项跳过了多少条 —— 例如：
*"1420 imported, 3 failed, 87 duplicates"*。导入成功后，上传的文件会从服务器上删除；失败的条目会在作业有效期内保留，以便您查看哪些内容被拒绝了。

## ⚠️ 注意事项 {#things-to-watch-out-for}

- **错误的文件类型** — 除非扩展名为 `.mbox`、`.eml` 或 `.zip` 之一，否则上传将被拒绝。EML 表单的选择器仅接受 `.zip`，MBox 表单仅接受 `.mbox` 和 `.mbx`。
- **"0 imported, N duplicates"** — 这不是错误。消息已存在于存档中，很可能是来自之前对同一源的导入或同步。
- **非常大的存档** — MBox 文件是逐条消息读取的，因此即使在后台运行，数 GB 的文件也需要一段时间。请在状态页面查看进度，而不是重新上传。
- **磁盘剩余空间** — 上传的文件临时存储在服务器上，需要在您的存档数据库旁边留有空间。
- **带有特殊字符的文件夹名称** — 文件夹名称取自 ZIP 路径或您的输入；如果您使用了不寻常的字符，请在导入后检查账户的文件夹列表。

## 🔗 相关文档 {#related-documentation}

- [CLI commands](CLI.md) — 从挂载目录导入
- [Account Import (CSV)](Account-Import.md) — 一次添加多个账户
- [Background Jobs](BackgroundJobs.md) — 监控和取消导入作业
- [Exporting a Mail Account](Export-Account.md) — 相反的方向
- [Setup and Parameters](Setup.md) — `Upload__*` 设置
