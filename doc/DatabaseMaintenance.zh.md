# 🔧 数据库维护指南 {#database-maintenance-guide}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

本指南介绍了如何在 Mail Archiver 中配置和使用自动数据库维护功能。应用程序包含一个内置的后台服务，可自动执行 PostgreSQL 数据库维护操作，以保持最佳性能并防止数据库膨胀。

## ⚠️ 重要通知 {#important-notice}

**当删除数据时，PostgreSQL 数据库不会自动缩小体积。** 当您删除电子邮件或邮箱时，数据库会将该空间标记为可重用，但实际的数据库文件大小保持不变。这种现象被称为“数据库膨胀 (database bloat)”。

Mail Archiver 包含一个自动维护服务，每天运行 VACUUM ANALYZE 操作，以实现：
- 回收数据库内的存储空间以供重用
- 更新查询统计信息以实现最佳性能
- 防止性能随时间下降
- 维护数据库健康

## 🚀 自动维护（推荐） {#automatic-maintenance-recommended}

### 配置 {#configuration}

在您的 `appsettings.json` 中启用自动数据库维护，或通过 `docker-compose.yml` 中的环境变量进行配置：

#### 选项 1：通过 appsettings.json {#option-1-via-appsettingsjson}

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### 选项 2：通过 docker-compose.yml（推荐） {#option-2-via-docker-composeyml-recommended}

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Enable automatic database maintenance
      - DatabaseMaintenance__Enabled=true
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30
```

### 配置选项 {#configuration-options}

| 参数 | 类型 | 默认值 | 描述 |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | 启用或禁用自动维护 |
| `DailyExecutionTime` | string | `"02:00"` | 运行维护的时间（HH:mm 格式，24 小时制） |
| `TimeoutMinutes` | integer | `30` | 允许维护操作运行的最大分钟数 |

### 工作原理 {#how-it-works}

启用后，维护服务将：

1. **随应用程序自动启动**
2. **等待** 直到配置的执行时间
3. **对整个数据库运行 VACUUM ANALYZE**
4. **将操作记录** 在应用程序的访问日志中，并包含持续时间
5. **重新调度** 到第二天

维护操作：
- ✅ 无需停机即可运行
- ✅ 不会干扰正常的应用程序操作
- ✅ 提供完整的日志记录和监控
- ✅ 回收数据库内的空间以供重用
- ✅ 更新查询统计信息以提升性能

### 选择执行时间 {#choosing-the-execution-time}

选择系统活动最低的时段：
- **推荐**：02:00 至 04:00 之间（深夜时段）
- **考虑**：您的时区设置
- **避免**：高峰使用时段或备份窗口

配置示例：

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### 监控自动维护 {#monitoring-automatic-maintenance}

#### 1. 应用程序日志 {#1-application-logs}

检查应用程序日志中的维护活动：

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. 访问日志 (UI) {#2-access-log-ui}

所有维护操作都会记录在应用程序的访问日志中：

1. 在 UI 中导航至 **Logs** → **Access Log**
2. 按类型过滤：**Database Maintenance**
3. 查看执行时间、持续时间以及任何错误

## 🔧 手动维护（高级） {#manual-maintenance-advanced}

### 用于磁盘空间回收的 VACUUM FULL {#vacuum-full-for-disk-space-recovery}

**⚠️ 需要应用程序停机**

VACUUM FULL 会压缩数据库并将磁盘空间归还给操作系统，但需要排他锁。

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**何时使用：**
- 删除大型邮箱之后
- 为了回收大量磁盘空间
- 在计划的维护窗口期间

**重要提示：**
- 需要等于表大小的可用磁盘空间
- 在大型数据库上可能需要数小时
- 会阻塞所有数据库操作
- 应安排在非高峰时段进行

有关详细的手动操作步骤，请参阅 [PostgreSQL VACUUM 文档](https://www.postgresql.org/docs/current/sql-vacuum.html)。

## 📚 相关文档 {#related-documentation}

- [设置指南](Setup.md) - 初始配置和环境变量
- [备份与恢复指南](BackupRestore.md) - 数据库备份程序
- [PostgreSQL VACUUM 文档](https://www.postgresql.org/docs/current/sql-vacuum.html) - 官方 PostgreSQL 文档

## 📝 总结 {#summary}

✅ **启用自动维护** 以实现无需人工干预的数据库优化
✅ **通过访问日志进行监控** 以验证操作是否成功
✅ 仅在需要回收磁盘空间时**使用手动 VACUUM FULL**
✅ 在进行大规模删除后，在维护窗口期间**安排 VACUUM FULL**
✅ 在进行任何手动维护操作之前，**确保备份是最新的**

自动维护服务处理日常数据库优化，在大多数情况下无需人工干预。
