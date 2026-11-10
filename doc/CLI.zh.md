# 🖥️ CLI Commands {#cli-commands}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

某些工作并不适合在 Web UI 中完成：例如从本地文件系统导入现有存档，或在存储后端之间移动存储的原始邮件。Mail Archiver 支持将这两者作为命令行命令执行。

| 命令 | 用途 |
|----------|---------|
| `--import-mbox` | 从挂载目录导入 MBox 文件 |
| `--import-eml` | 从挂载目录导入 EML 文件的 ZIP 存档 |
| `--migrate-raw-storage` | 在数据库和文件系统之间移动存储的原始邮件 |

所有命令都在 Web 流水线启动前处理，并在完成后退出，因此可以在 `docker compose exec` 运行存档时安全地运行。

## ⚠️ 安全模型 {#security-model}

导入命令**仅限 CLI** —— 没有用于浏览或导入本地文件的 Web 端点。您必须拥有 Docker 宿主机访问权限 (`docker exec`) 才能运行它们。

**路径白名单** (`LocalImport:AllowedPaths`) 控制导入器可以读取哪些目录。这些路径之外的文件将被拒绝。

`--migrate-raw-storage` 不受该白名单限制：它仅读取存档本身在 `RawMessages:FileSystem:BasePath` 下写入的消息文件。

## 🛠️ 设置 {#setup}

### 1. 配置允许的路径 {#1-configure-allowed-paths}

在您的 `docker-compose.yml` 中，设置允许的导入路径：

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. 挂载您的文件 {#2-mount-your-files}

将包含 MBox/EML 文件的目录挂载到容器中：

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

将 `/home/user/mbox-archives` 替换为 Docker 宿主机上的实际路径。

### 3. 重启容器 {#3-restart-the-container}

```bash
docker compose down && docker compose up -d
```

## 📝 用法 {#usage}

### 查找您的目标账户 ID {#find-your-target-account-id}

在导入之前，您需要目标邮件账户的数据库 ID。您可以通过 Web UI 找到它：

导航至 "Email Accounts"，打开目标账户详情，账户 ID 会显示在账户列表中，或者在查看账户时的 URL 中。

### 导入 MBox 文件 {#import-an-mbox-file}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**参数：**
| 参数 | 是否必填 | 描述 |
|-----------|----------|-------------|
| `--import-mbox` | 是 | 标识 MBox 导入操作 |
| `--file <path>` | 是 | 容器内 MBox 文件的路径 |
| `--account-id <id>` | 是 | 目标邮件账户的数据库 ID |
| `--folder <name>` | 否 | 目标文件夹名称 (默认: `INBOX`) |

### 导入 EML 文件 (ZIP 压缩包) {#import-eml-files-zip-archive}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**参数：**
| 参数 | 是否必填 | 描述 |
|-----------|----------|-------------|
| `--import-eml` | 是 | 标识 EML 导入操作 |
| `--file <path>` | 是 | 容器内包含 `.eml` 文件的 **ZIP 压缩包** 路径 |
| `--account-id <id>` | 是 | 目标邮件账户的数据库 ID |

> **重要提示：** `--import-eml` 期望一个包含 `.eml` 文件的 **ZIP 压缩包** —— 它**不**接受单个的 `.eml` 文件。要导入单个 EML 文件，请先将其封装在 ZIP 压缩包中：
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **针对 `dotnet run` 的说明：** 当使用 `dotnet run` (而非发布的 DLL) 时，请使用 `--` 将 dotnet 的参数与应用程序的参数分隔开：
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> `--` 会告知 `dotnet run` 所有后续参数都属于您的应用程序，而不是 .NET 编译器。

## 📊 示例输出 {#example-output}

```
Target account: user@example.com (ID: 1)

=== Local MBox Import ===
File: /data/import/myarchive.mbox
Size: 245.32 MB
Target Account ID: 1
Target Folder: INBOX

[Importing... progress logged to container logs]

=== Import Results ===
Status: Completed
Total Emails: 15234
Imported Successfully: 15120
Failed: 42
Skipped (malformed): 28
Skipped (duplicates): 44
Duration: 00:12:45
```

## 🔍 监控 {#monitoring}

在导入期间，详细的进度信息会被写入容器日志：

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ 配置参考 {#configuration-reference}

| 环境变量 | 默认值 | 描述 |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | 允许的第一个本地导入路径 |
| `LocalImport__AllowedPaths__1` | (empty) | 允许的第二个路径（可选） |

可以通过递增索引来添加更多路径（`__2`, `__3` 等）。

## 🚫 错误处理 {#error-handling}

| 错误 | 原因 | 解决方案 |
|-------|-------|----------|
| `File not found` | 容器中不存在 `--file` 路径 | 验证文件路径和卷挂载 |
| `File path is not in an allowed import directory` | 文件在白名单路径之外 | 将文件移动到允许的路径，或将其目录添加到 `LocalImport:AllowedPaths` |
| `Mail account with ID X not found` | 账户 ID 错误 | 通过 Web UI 或数据库查询验证账户 ID |
| `Invalid account-id` | `--account-id` 的值不是数字 | 使用数字形式的数据库 ID |

## 🔒 安全最佳实践 {#security-best-practices}

1. **尽可能使用只读挂载**：
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **限制 AllowedPaths**，仅允许您实际使用的目录。

3. **在导入完成后移除卷挂载**，以保持干净的安全边界。

4. **切勿通过 Web 服务器暴露**导入目录 —— 本地导入没有 Web 端点。

## 📦 导入格式概览 {#import-formats-at-a-glance}

| CLI Flag | 输入格式 | 典型文件扩展名 | 单封邮件 | 批量 |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | MBox 文件 | `.mbox`, `.mbx` | ❌ (始终为批量) | ✅ |
| `--import-eml` | EMLs 的 ZIP 压缩包 | `.zip` | ❌ (需要 ZIP) | ✅ |
| `--migrate-raw-storage` | 存储的原始邮件 | — | — | ✅ |

*不支持直接使用单个 `.eml` 文件。请使用 `zip` 对其进行封装，或通过 [Web 上传](Import-EML-MBox.md) 进行导入。*

## 🔧 维护命令 {#maintenance-commands}

### 移动存储的原始消息 {#moving-stored-original-messages}

当 `RawMessages:Enabled` 开启时，每封电子邮件的原始消息会根据 `RawMessages:Storage` 的不同，保存在数据库或文件中。此命令将已存储的原始消息从一个后端移动到另一个后端；配置详情请参见 [Original Message Storage](RawMessages.md#storage-backend)。

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

| 参数 | 默认值 | 描述 |
|----------|---------|-------------|
| `--from` | 已配置的 `RawMessages:Storage` | 消息移出的后端。 |
| `--to` | 已配置的 `RawMessages:Storage` | 消息移入的后端。 |
| `--batch` | `200` | 每个数据库事务处理的消息数。 |

与导入命令不同，此命令不受 `LocalImport:AllowedPaths` 的限制：它仅读取归档程序本身在 `RawMessages:FileSystem:BasePath` 下写入的消息文件。

**其保证的内容：**

- 在复制每条消息之前，都会根据归档时的校验和对其进行验证。失败的消息将保留在原处并进行报告 —— 损坏的原始消息绝不会被传播，且其唯一的剩余副本绝不会被删除。
- 只有在移动文件的数据库事务提交后，该文件才会被删除，因此中断的运行不会导致唯一副本丢失。
- 运行过程可恢复：通过每条消息上记录的存储方式来选择工作任务，因此重新启动命令将继续处理尚未完成的任务。
- 单条错误消息不会导致整个运行中止。如果有任何消息失败，退出码为 `1`，否则为 `0`。

**示例输出：**

```text
Batch: 200 moved, 0 skipped, 0 failed
Batch: 200 moved, 0 skipped, 0 failed
Batch: 42 moved, 0 skipped, 1 failed
Done: 442 moved, 1 failed.
  Email 12345: checksum mismatch (recorded 4f1c..., file has 9ab2...)
```

读取操作始终遵循每条消息上记录的后端，而非配置设置，因此在迁移期间及之后，下载操作仍能正常工作。更改 `RawMessages:Storage` 仅影响新归档的邮件 —— 运行此命令以移动已存储的内容。

> **备份：** 使用文件系统后端时，消息文件存在于数据库之外。请将 `RawMessages:FileSystem:BasePath` 与数据库转储一起进行备份；如果不包含这些文件进行恢复，原始消息将无法被提供。

## ❓ FAQ {#faq}

**Q: 为什么 `--import-eml` 需要 ZIP 文件？**
A: 底层的 EML 导入服务 (`EmlImportService`) 在内部使用 .NET 的 `ZipFile` API 处理 ZIP 压缩包。这种设计可以高效地支持批量导入。对于单个 `.eml` 文件，请将其封装在 ZIP 压缩包中（例如 `zip emails.zip *.eml`）或使用 [web upload](Import-EML-MBox.md)。

**Q: 为什么我无法从 Web UI 浏览文件？**
A: 这是一个刻意的安全设计。此功能特意通过 CLI 实现，用于系统级交互，因为普通用户无法直接访问操作系统层级；他们仅能与 Web UI 进行交互。

为了防止路径遍历攻击和未经授权的文件系统暴露等安全风险，普通用户被限制仅能使用 Web UI 进行文件上传和导入。这确保了他们无法绕过预设界面来访问底层的容器文件系统。
