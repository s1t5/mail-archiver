# 🔄 PostgreSQL Major Version Upgrade Guide {#postgresql-major-version-upgrade-guide}

[← Back to Documentation Index](index.md)

## 📋 概述 {#overview}

本指南说明如何在您的 Mail Archiver Docker Compose 环境中将 PostgreSQL 数据库从一个主版本升级到另一个主版本。升级到新主版本对于利用新的 PostgreSQL 特性、性能改进和安全更新是必要的。

## ⚠️ 重要免责声明 {#important-disclaimer}
> **注意**：此升级程序是一个通用的 PostgreSQL 数据库迁移过程，并不专门针对 Mail Archiver。它是基于 PostgreSQL 最佳实践提供的建议。在进行操作之前，请务必先在非生产环境中测试该程序，并确保您已做好妥善的备份。


**本指南中描述的所有程序都应先在非生产环境中进行测试。** 主版本升级涉及数据迁移，并带有数据丢失或损坏的风险。请务必：
- 在开始升级过程之前创建完整的备份
- 在开发环境中测试升级程序
- 在升级后验证所有应用程序功能
- 准备好应对问题的回滚计划
- 在维护窗口期间安排升级

**风险自担。**

## 🎯 何时进行升级 {#when-to-upgrade}

在以下情况下考虑升级 PostgreSQL：
- 您当前的版本即将达到生命周期终点 (EOL)
- 安全更新需要更高版本
- 您正在计划一次重大的系统维护窗口

## 🛠️ 前提条件 {#prerequisites}

- 正在运行的 Mail Archiver Docker 设置（参见 [Setup Guide](Setup.md)）
- 足够的磁盘空间（至少为当前数据库大小的 1.5 倍）
- 对 Docker 宿主系统的访问权限
- 当前数据库的完整备份（参见 [Backup and Restore Guide](BackupRestore.md)）
- 应用程序停机维护窗口

## 📋 当前配置 {#current-configuration}

您当前的 Mail Archiver 设置使用：
- **Database Name**: MailArchiver
- **Database User**: mailuser
- **Data Volume**: `./postgres-data` 映射到 `/var/lib/postgresql/data`

如有必要，必须在以下示例命令和脚本中进行调整。

## 🔄 升级程序 {#upgrade-procedures}

此方法通过创建数据的逻辑备份并将其恢复到新的 PostgreSQL 版本来完成。这是最安全的方法，且适用于任何 PostgreSQL 版本。

> **替代方法**：PostgreSQL 还提供了其他升级方法，例如使用 `pg_upgrade` 以实现更快的升级。有关替代升级方法的更多信息，请参考 [official PostgreSQL upgrade documentation](https://www.postgresql.org/docs/current/upgrading.html)。

#### 第 1 步：准备环境 {#step-1-prepare-the-environment}

```bash
# 1. Stop the application container (keep database running for backup)
docker compose stop mailarchive-app

# 2. Create a backup directory
mkdir -p postgres-backup

# 3. Create a database dump
echo "Export DB content (this can take some time)"
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > postgres-backup/database_dump.sql

# 4. Verify the dump was created successfully
ls -la postgres-backup/database_dump.sql

# 5. Stop the database container
docker compose down
```

#### 第 2 步：更新 Docker Compose 配置 {#step-2-update-docker-compose-configuration}

编辑您的 `docker-compose.yml` 文件以使用新的 PostgreSQL 版本：

```yaml
  postgres:
    image: postgres:18  # Changed from old version
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data-new:/var/lib/postgresql  # New data directory
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
```

**重要变更：**
- 将 `image` 从 `postgres:17` 更新为 `postgres:18`（或您的目标版本）
- 将卷映射从 `./postgres-data:/var/lib/postgresql/data` 更改为 `./postgres-data-new:/var/lib/postgresql`（版本 18 及后续版本要求更改内部路径）

#### 第 3 步：启动新的 PostgreSQL 实例 {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### 第 4 步：将数据恢复到新数据库 {#step-4-restore-data-to-new-database}

将数据库转储恢复到新的 PostgreSQL 实例：

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
这可能需要一些时间，具体取决于数据库大小
在恢复期间请留意任何错误消息

#### 第 5 步：测试并完成迁移 {#step-5-test-and-complete-migration}
启动应用程序以测试连接性：

```bash
docker compose up -d
```

测试应用程序功能：
- 登录 Web 界面
- 验证邮件搜索功能是否正常
- 检查现有数据是否可访问
- 测试备份/恢复功能

如果一切运行正常，请清理旧数据：

停止容器
```bash
docker compose down
```
删除旧的数据目录（仅在确认新设置工作正常后）
```bash
rm -rf postgres-data
```
删除备份目录
```bash
rm -rf postgres-backup
```

将新的数据目录重命名为标准名称
```bash
mv postgres-data-new postgres-data
```

更新 docker-compose.yml 以使用标准卷名称：
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

启动整个系统
```bash
docker compose up -d
```

## ⚠️ 重要注意事项 {#important-considerations}

### 停机计划 {#downtime-planning}

- 根据您的系统和数据库大小，此过程可能需要很长时间。
- 请在维护窗口期间进行安排

### 磁盘空间要求 {#disk-space-requirements}

确保有足够的磁盘空间（约为数据库大小的两倍）

检查可用空间：
```bash
df -h
du -sh postgres-data/
```

### 回滚计划 {#rollback-plan}

始终做好回滚准备：
1. 在验证升级成功之前，保留旧的数据目录
2. 保留旧的 `docker-compose.yml` 备份
3. 先在开发环境中测试回滚程序

### 升级后验证 {#post-upgrade-verification}

升级后，请验证：
- 应用程序与数据库的连接性
- 邮件搜索和检索功能
- 用户身份验证和会话管理
- 备份和恢复功能
- 数据库性能指标

## 📚 相关文档 {#related-documentation}

- [Backup and Restore Guide](BackupRestore.md) - 升级前备份必读
- [Database Maintenance Guide](DatabaseMaintenance.md) - 升级后维护
- [Setup Guide](Setup.md) - Docker Compose 配置
- [PostgreSQL Release Notes](https://www.postgresql.org/docs/release/) - 官方发布文档
