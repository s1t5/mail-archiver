# 🧪 本地测试环境 {#local-test-environment}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

本指南介绍了如何在开发机上运行测试套件、应用程序以及 IMAP 目标。本指南面向贡献者：其启动的堆栈是刻意不安全的，并非部署指南。有关部署，请参阅 [Setup Guide](Setup.md)。

涉及三个部分：

| 部分 | 提供方 | 用途 |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | 两个数据库，一个用于 `dotnet test`，一个用于应用程序 |
| Dovecot | `docker-compose.test.yml` | 代替 mailcow 信箱的 IMAP 目标 |
| The application | 宿主机上的 `dotnet run` | 比重新构建容器镜像迭代速度更快 |

## 🚀 启动技术栈 {#starting-the-stack}

```bash
docker compose -f docker-compose.test.yml up -d
```

此操作仅在 loopback 上发布所有内容：

| 服务 | 主机地址 | 备注 |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5433，而非 5432，因此不会与本地服务器冲突 |
| Dovecot, plain | `127.0.0.1:1143` | 已启用明文身份验证 |
| Dovecot, TLS | `127.0.0.1:1993` | 使用镜像自带的自签名证书 |

要停止运行并丢弃所有数据库和存储的邮件：

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ 两个数据库 {#the-two-databases}

测试套件会留下 fixture 行：在完整运行后，测试数据库会持有数十个邮件账户和用户。如果应用程序共享该数据库，其后台同步过程会抓取这些 fixtures 并反复尝试连接不存在的服务器。因此，postgres 容器在首次初始化时会创建两个数据库 (`tests/postgres/10-create-dev-db.sql`)：

| Database | Used by | Configured in |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

两个配置文件都已被 gitignore，必须在本地创建。

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

仓库根目录下的 `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` 在容器外部非常重要：它默认为绝对路径 `/app/DataProtection-Keys`，该路径在开发机上并不存在，如果没有它，每次读取 key ring 时请求都会失败。`IgnoreSelfSignedCert` 是让应用程序能够接受 1993 端口上 Dovecot 证书的关键。

## ✅ 运行测试 {#running-the-tests}

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

Migrations 会在首次运行时由 `TestDbFixture` 自动应用。大多数测试完全不需要数据库；其余测试使用 PostgreSQL，因此如果 stack 未运行，则会报 `Name or service not known` 错误。

也可以通过环境变量提供连接字符串，这会覆盖所有的配置文件：

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ 运行应用程序 {#running-the-application}

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` 已经为 `dotnet run` 设置了 `ASPNETCORE_ENVIRONMENT=Development`，因此使用普通的 `dotnet run --project MailArchiver.csproj` 也可以工作，并提供
http://localhost:5179 (`http` 配置文件的服务)。上述 `ASPNETCORE_URLS` 则仅选择了容器端口
5000。

待处理的迁移会在启动时应用于 `MailArchiverDev`。请使用来自 `appsettings.json` 的凭据进行登录
(`Authentication:Username` 和 `Authentication:Password`)。

## 📬 使用 Dovecot 目标 {#using-the-dovecot-target}

Dovecot 接受使用密码 `pass` 的**任何用户名**，因此新的目标邮箱无需进行配置：只需选择一个名称并登录即可。在 Mail Accounts 下通过以下方式添加：

| 字段 | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | 任意内容，例如 `offload-target@example.com` | 相同 |
| Password | `pass` | 相同 |

`tests/dovecot/dovecot.conf` 中的配置基于镜像自身的默认配置进行建模，并刻意做了三个差异，使其表现得像 mailcow 目标：

- **Maildir 存储**而非镜像的 sdbox，这正是 mailcow 所使用的。
- **`separator = /`**，与 mailcow 匹配。当恢复程序重建文件夹层级时，文件夹路径会根据 `/` 和 `\` 进行拆分，因此使用 `.` 的服务器表现会不同。
- **预先创建了特殊用途文件夹 `Drafts`、`Junk`、`Sent` 和 `Trash`**，正如 mailcow 会预先创建它们一样。这在测试从 Exchange 迁移时非常重要：名为 `Sent Items` 的源文件夹会落在现有的 `Sent` 旁边，而不是与其合并，这正是文件夹重命名需要处理的情况。

在端口 1143 上启用了明文身份验证，因为当账户禁用 SSL 时，应用程序会在没有任何传输安全的情况下进行连接。

## 🌱 播种归档以进行测试 {#seeding-an-archive-to-test-against}

空归档对于测试恢复或迁移没有太大用处，因此 `tests/seed/seed.sh` 构建了一个：

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```

它创建了两个账户，导入了 fixtures，并打印了落库的内容。两个账户的创建都设置了为期一年的单账户同步间隔，并且 source 被额外禁用，因此后台同步服务会忽略它：其服务器不存在，且 Dovecot 目标仅需进行追加。一个注意事项：调度状态存在于内存中，因此每次应用程序重启都会立即调度一次目标。因此，目标也排除了 Dovecot 特殊用途文件夹（`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`），以便一次重启同步后没有内容可以归档回传。运行两次脚本是无害的；第二次运行会将每条消息报告为已存在。

Fixtures 检入在 `tests/seed/fixtures` 下，每个 source 文件夹对应一个 mbox，并使用 `manifest.tsv` 将每个文件映射到其目标文件夹。它们通过应用程序自带的 `--import-mbox` CLI 导入，这一细节是刻意为之：CLI 通过 `MailImporter.ImportEmailToDatabase` 进行路由，这与 IMAP 同步使用的导入器相同，因此数据行落库时的格式与真实归档完全一致 —— 地址通过 `", "` 连接，主题通过 `CleanText` 传递，`SentDate` 转换为显示时区，`RawHeaders` 被提取并截断。如果直接将行插入数据库，将会跳过所有这些步骤，从而产生与生产数据存在细微差异的 fixtures。

每条 fixture 消息都携带一个 `X-Fixture-Purpose` 头部，说明其存在的原因，因此文件本身就具有文档属性：

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```

日期是固定的，而不是相对于当前日期。迁移截止时间（migration cutoff）在作业运行前会被解析为绝对日期，因此固定的 fixture 集和固定的截止时间比滑动时间更能可预测地测试相同的内容。消息跨度从 2012 年到 2026 年。

文件夹布局镜像了 Exchange 邮箱，每个文件夹的存在都是为了测试某些功能：

| Source folder | 存在原因 |
|---|---|
| `INBOX` | 日期跨越十五年，外加下方的边缘情况消息 |
| `Sent Items` | 与 Dovecot 目标现有的特殊用途 `Sent` 冲突 |
| `Sent Items/2019` | 重命名必须仅重写领先的路径段 |
| `Sent Items Archive` | 共享一个前缀作为子字符串，但不在段边界上，因此不得被重写 |
| `Deleted Items` | 文件夹排除 |
| `Deleted Items/Old` | 父级上的排除必须覆盖后代 |
| `Junk E-Mail` | Exchange 对 Dovecot 称为 `Junk` 的文件夹的命名 |
| `Archive/Projects/2020` | 三层嵌套 |

单个消息涵盖了：没有 `Message-ID` 头部（使用确定性的回退方案）的消息、`Message-ID` 没有 `@` 的消息（在恢复后无法完整保留）、包含控制字符的主题、具有三跳 `Received` 链的消息以及没有该链的消息、若干具有两个 `To` 收件人的消息，以及两对间隔一秒的近乎重复的对。

### 关于退出码的说明 {#a-note-on-the-exit-code}

`tests/seed/seed.sh` 检查导入输出中的失败和格式错误计数，而不是进程的退出码。`MBoxImportService` 在跳过重复项时报告 `CompletedWithErrors`，且 CLI 会将除 `Completed` 以外的所有状态映射为退出 1，因此即使没有发生错误，重新运行导入也会以非零状态退出。因此，重新运行播种脚本是安全的，并且会将每条消息报告为已存在。

## ⚠️ 不适用于生产环境 {#not-for-production}

- 任何用户名都使用单一的固定密码向 Dovecot 进行身份验证。
- 已启用明文身份验证。
- 数据库凭据为文档中记载的默认值。
- 邮件和数据库状态存储在 `down -v` 会删除的 Docker volumes 中。

## 🔧 故障排除 {#troubleshooting}

**测试因 `Name or service not known` 而失败** — 堆栈未运行，或者
由于缺少 `appsettings.Test.json`，导致连接字符串回退到容器默认值
`Host=postgres`。

**构建时出现 `NETSDK1226`** — 本地 .NET SDK 中的工作负载数据不完整，与此
仓库无关。请运行 `dotnet workload repair`，或使用
`-p:AllowMissingPrunePackageData=true` 进行构建。

**Dovecot 循环重启** — 配置错误。`docker compose -f
docker-compose.test.yml logs dovecot` 会报告出错的行。请注意，Dovecot 不
允许在块内的同一行中包含多个设置。

**应用程序在每个请求上都返回 HTTP 500** — 通常是数据保护密钥环（data protection key ring）的问题。
请检查 `DataProtection:KeyPath` 是否指向一个可写目录。
