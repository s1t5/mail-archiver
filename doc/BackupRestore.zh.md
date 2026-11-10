# 💾 备份与恢复指南 {#backup-and-restore-guide}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

本指南提供了备份和恢复 Mail Archiver Docker 环境的详细说明。定期备份对于保护您的存档邮件和配置数据至关重要。

## ⚠️ 重要免责声明 {#important-disclaimer}

**本指南中提供的所有脚本和命令仅为示例。** 我们不对其功能或对您特定环境的适用性承担任何保证或责任。请务必：
- 先在非生产环境中测试备份和恢复流程
- 验证备份是否完整且可恢复
- 根据您的具体设置和需求调整脚本和命令
- 在不同位置保留多个备份副本
- 定期测试您的灾难恢复程序

**风险自担。**

## 🎯 备份建议 {#backup-recommendations}

### 离线备份（强烈推荐） {#offline-backups-highly-recommended}
为了获得最大的数据一致性和完整性，**请务必在停止应用程序和数据库的状态下进行备份**。这可以确保：
- 备份期间没有活跃的数据库事务
- 所有文件的数据一致性
- 没有因并发写入导致的文件损坏风险
- 可靠的恢复能力

虽然热备份（方法 2 和 3）对于减少停机时间很方便，但它们具有更高的不一致风险。对于关键的生产环境，请在维护窗口期间安排定期的离线备份。

### 系统级备份 {#system-level-backups}
除了特定于应用程序的备份外，**强烈建议**创建整个系统基础设施的备份：
- **LXC 容器备份**：如果运行在 LXC 容器中，请创建整个容器的离线快照
- **虚拟机备份**：对于 VM 部署，创建完整的 VM 快照
- **主机系统备份**：包括 Docker 卷、容器配置和系统设置
- **配置文件**：单独备份所有 Docker 和系统配置文件

系统级备份提供了额外的安全层，并支持完整的灾难恢复，包括在需要时将整个环境恢复到不同的主机。

## 🛠️ 前提条件 {#prerequisites}

- 正在运行的 Mail Archiver Docker 设置（参见 [设置指南](Setup.md)）
- 能够访问 Docker 主机系统
- 足够的备份存储空间

## 📦 备份内容 {#what-to-backup}

Mail Archiver Docker 环境由几个需要备份的重要数据卷组成：

### 1. PostgreSQL 数据库 {#1-postgresql-database}
- **卷 (Volume)**: `./postgres-data`
- **包含内容**: 所有存档邮件、用户账户、邮件账户和应用程序数据
- **重要性**: ⭐⭐⭐ 关键 - 这是您的主要数据存储

### 2. 数据保护密钥 (Data Protection Keys) {#2-data-protection-keys}
- **卷 (Volume)**: `./data-protection-keys`
- **包含内容**: 用于 Cookie 加密和身份验证的 ASP.NET Core 数据保护密钥
- **重要性**: ⭐⭐ 重要 - 对于现有的用户会话和安全身份验证是必需的

### 2a. 原始消息文件 (仅在使用 `RawMessages.Storage=filesystem` 时) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **卷 (Volume)**: `./raw-messages` (`/app/RawMessages` 的主机侧)
- **包含内容**: 每封邮件的原始 `.eml`，以 gzip 压缩格式，当 `RawMessages:Enabled` 设置且存储后端为 `filesystem` 时写入
- **重要性**: ⭐⭐⭐ **在使用时**为关键 - 数据库仅保留校验和、大小和相对路径，因此如果不包含此目录进行恢复，原始文件将无法提供服务
- 请将此目录和数据库转储 **放在一起** 备份。如果只恢复数据库而没有这些文件，会导致存档无法修复：下载操作会回答 `404` 且校验和无法从其他地方重新推导。如果不带文件恢复数据库同样毫无意义，因为没有任何东西引用它们。
- 如果您使用 `RawMessages.Storage=postgres`（默认设置），则此目录不存在，原始文件仍如以前一样位于 `./postgres-data` 中。
- 关于该功能本身以及如何切换后端，请参阅 [原始消息存储](RawMessages.md)。

### 3. Docker Compose 配置 {#3-docker-compose-configuration}
- **文件**: `docker-compose.yml`
- **包含内容**: 容器配置、环境变量和设置
- **重要性**: ⭐⭐ 重要 - 包含您的应用程序配置

## 🔄 备份程序 {#backup-procedures}

### 方法 1：完整备份（推荐） {#method-1-complete-backup-recommended}

此方法在容器停止时备份所有数据，以确保数据一致性。

```bash
# 1. Stop the containers
docker compose down

# 2. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 3. Copy all data volumes
cp -r postgres-data "$BACKUP_DIR/"
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Copy docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

# 7. Start the containers again
docker compose up -d

echo "Backup completed: $BACKUP_DIR.tar.gz"
```

### 方法 2：热备份（最小停机时间） {#method-2-hot-backup-minimal-downtime}

此方法在系统运行时创建完整备份，停机时间极短。

**注意：** 请使用来自 `docker-compose.yml` 的实际数据库凭据替换 `mailuser` 和 `MailArchiver`（检查 `POSTGRES_USER` 和 `POSTGRES_DB` 环境变量）。

```bash
# 1. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 2. Backup the database
# Replace 'mailuser' and 'MailArchiver' with your actual POSTGRES_USER and POSTGRES_DB
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > "$BACKUP_DIR/database.sql"

# 3. Backup data protection keys (these change rarely, can be done while running)
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Backup docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

echo "Hot backup completed: $BACKUP_DIR.tar.gz"
```

### 方法 3：自动化备份脚本 {#method-3-automated-backup-script}

对于定期的自动化备份，我们提供了一个开箱即用的备份脚本。该脚本可在 [backup.sh](attachments/backup.sh) 中获取。

**功能特点：**
- ✅ 自动从 docker-compose.yml 检测数据库凭据
- ✅ 热备份（最小停机时间）
- ✅ 锁定文件机制，防止并发备份
- ✅ 备份前检查磁盘空间
- ✅ 自动清理旧备份

**配置：**

在使用脚本之前，请编辑文件顶部的配置部分：

```bash
# Required settings:
MAILARCHIVER_DIR="/path/to/your/mailarchiver"   # Mail Archiver installation directory
BACKUP_BASE_DIR="/path/to/your/backups"         # Where to store backups

# Optional settings (with defaults):
RETENTION_DAYS=30                               # How many days to keep backups
LOG_FILE="/var/log/mailarchiver-backup.log"     # Log file location
MIN_DISK_SPACE_MB=1000                          # Minimum free disk space required
LOCK_FILE="/var/lock/mailarchiver-backup.lock"  # Lock file to prevent concurrent runs
```

**设置：**

1. **将脚本复制到您想要的位置：**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **编辑配置：**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # 调整设置
   ```

3. **使脚本可执行：**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **手动测试脚本：**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **验证备份：**
   - 检查备份目录中是否生成了 `.tar.gz` 文件
   - 查看日志文件是否有任何错误或警告

**安排自动化备份：**

使用 cron 安排自动备份：

```bash
# Edit crontab
crontab -e

# Add a line to run backup daily at 2 AM
0 2 * * * /usr/local/bin/mailarchiver-backup.sh

# Or weekly on Sunday at 3 AM
0 3 * * 0 /usr/local/bin/mailarchiver-backup.sh

# Or multiple times (e.g., daily at 2 AM and 2 PM)
0 2,14 * * * /usr/local/bin/mailarchiver-backup.sh
```

## 🔙 恢复程序 {#restore-procedures}

恢复程序旨在与备份方法相匹配。请根据您使用的备份方法选择合适的恢复方法：

- **方法 1 (完整备份)**：使用“从完整备份进行全量恢复”程序。当您拥有包含 `postgres-data` 目录的完整备份时，使用此方法。
- **方法 2 (热备份)** 或 **方法 3 (自动化备份脚本)**：使用“从 SQL 转储恢复”程序（处理数据保护密钥和数据库）。当您拥有包含数据库 SQL 转储和数据保护密钥的热备份时，使用此方法。

为了方便起见，我们也提供了一个自动化恢复脚本，可以自动处理所有的恢复程序。您可以直接使用该脚本，而无需手动执行以下步骤。

### 自动化恢复脚本 {#automated-restore-script}

恢复脚本可在 [restore.sh](attachments/restore.sh) 文件中找到。它将提供引导式恢复，并询问备份方法、备份 tar 文件的路径以及安装目录，然后自动执行恢复过程。

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

该脚本将引导您完成恢复过程，并根据您使用的备份方法自动处理所有步骤。

### 从完整备份进行全量恢复（手动方法） {#full-restore-from-complete-backup-manual-method}

这会从完整的备份存档中恢复所有数据。当您拥有包含 `postgres-data` 目录（由方法一创建）的完整备份时，应使用此方法。

```bash
# 1. Stop the containers
docker compose down

# 2. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 3. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 4. Remove current data (make sure to backup first if needed!)
rm -rf postgres-data
rm -rf data-protection-keys

# 5. Restore data volumes
cp -r "$BACKUP_PATH/postgres-data" ./
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Start the containers
docker compose up -d

echo "Restore completed from: $BACKUP_FILE"
```

### 从 SQL 转储恢复（手动方法） {#restore-from-sql-dump-manual-method}

这会从热备份（方法二和三）中恢复数据库和数据保护密钥。

**注意：** 下面的命令使用了示例凭据（`mailuser` 和 `MailArchiver`）。您必须将这些值替换为来自 `docker-compose.yml` 文件的实际值。请检查 `postgres` 服务下的 `POSTGRES_USER` 和 `POSTGRES_DB` 环境变量。

```bash
# 1. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 2. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 3. Stop the containers
docker compose down

# 4. Backup current data protection keys (optional, for safety)
# cp -r data-protection-keys data-protection-keys.backup

# 5. Restore data protection keys
rm -rf data-protection-keys
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Extract database credentials from docker-compose.yml
# Check your docker-compose.yml for the actual values and replace in the commands below:
# - POSTGRES_USER (default: mailuser)
# - POSTGRES_DB (default: MailArchiver)
DB_USER="mailuser"  # Replace with your POSTGRES_USER
DB_NAME="MailArchiver"  # Replace with your POSTGRES_DB

# 8. Start the database container
docker compose up -d postgres

# 9. Wait for database to be ready
echo "Waiting for database to be ready..."
sleep 10

# 10. Verify database is ready (adjust -U and -d parameters if needed)
docker compose exec -T postgres pg_isready -U "$DB_USER" -d "$DB_NAME"

# 11. Drop and recreate the database (WARNING: This deletes all current data!)
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "DROP DATABASE IF EXISTS \"$DB_NAME\";"
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "CREATE DATABASE \"$DB_NAME\";"

# 12. Restore the database from SQL dump
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" < "$BACKUP_PATH/database.sql"

# 13. Start all containers
docker compose up -d

# 14. Clean up extracted backup
rm -rf "$BACKUP_PATH"

echo "Restore completed from: $BACKUP_FILE"
```


## ⚠️ 重要注意事项 {#important-notes}

### 数据库连接设置 {#database-connection-settings}
确保 `docker-compose.yml` 中的数据库连接设置在备份和恢复时保持一致：
- `POSTGRES_USER` (默认: mailuser)
- `POSTGRES_PASSWORD` (默认: masterkey)
- `POSTGRES_DB` (默认: MailArchiver)

### 数据保护密钥 {#data-protection-keys}
`data-protection-keys` 目录用于：
- 用户身份验证和会话管理
- Cookie 加密
- 安全数据处理

如果没有这些密钥，现有的用户会话将失效，用户必须重新登录。

### 环境变量 {#environment-variables}
所有应用程序设置都存储在 `docker-compose.yml` 中的环境变量中。请确保：
- 备份您的 `docker-compose.yml` 文件
- 记录任何自定义设置
- 在恢复期间使用相同的配置

## 🔒 安全最佳实践 {#security-best-practices}

1. **加密备份**：考虑对备份存档进行加密，特别是如果其中包含敏感邮件数据。

2. **安全存储**：将备份存储在安全的位置：
   - 使用独立的服务器或存储系统
   - 实现访问控制
   - 考虑异地或云存储

3. **定期测试**：定期测试您的备份和恢复程序，以确保它们可以正常工作。

4. **监控备份大小**：跟踪备份大小，以确保您有足够的存储空间。

5. **备份保留策略**：定义并实施备份保留策略（例如，保留每日备份 7 天，每周备份 4 周，每月备份 12 个月）。

6. **更改默认密码**：配置中的默认密码是不安全的，在生产环境中使用时应予以更改。


## 🖥️ Proxmox 备份 {#proxmox-backup}

在 Proxmox LXC 容器中运行 Mail Archiver Docker 环境时，建议使用“停止 (stop)”模式来备份整个容器，以确保数据一致性。

### 为什么推荐使用停止模式 {#why-stop-mode-is-recommended}

1. **数据一致性**：停止容器可以确保在进行备份之前，所有数据库事务都已完成，且数据已写入磁盘。
2. **防止数据损坏**：在容器运行期间进行备份可能会导致数据损坏，尤其是在进行数据库操作时。
3. **文件系统完整性**：停止容器可以确保所有文件系统操作已完成，防止在备份期间发生部分写入。

### Proxmox 备份注意事项 {#proxmox-backup-notes}

- 始终确保您有足够的存储空间进行备份。
- 定期测试您的恢复程序，以确保备份有效。
- 考虑在 Proxmox Web 界面中设置备份计划以实现自动化备份。
- 监控备份日志中的任何错误或问题。
