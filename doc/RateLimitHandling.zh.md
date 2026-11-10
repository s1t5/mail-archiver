# 📊 速率限制处理 {#rate-limit-handling}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

许多电子邮件提供商会对 IMAP 访问实施带宽限制。这些限制通常在 24 小时内从几百 MB 到几个 GB 不等。当同步超过这些限制的大型邮箱时，同步过程通常会失败或无法完成。

Mail Archiver 内置了速率限制处理系统，可自动管理这些限制，确保即使对于大型邮箱也能实现可靠的同步。

## ⚙️ 工作原理 {#how-it-works}

### 自动带宽追踪 {#automatic-bandwidth-tracking}

系统会持续监控 IMAP 同步期间下载的数据量。当达到配置的带宽限制时：

1. **同步自动暂停**：当前的同步操作会优雅地停止
2. **保存进度**：检查点（Checkpoints）会存储同步停止时的精确位置
3. **状态更新**：账户在仪表板中显示“Rate-Limited”（受速率限制）状态
4. **自动恢复**：当限制重置时（通常在 24 小时后），同步将从保存的检查点处恢复

### 示例：具有 2.5 GB 每日限制的 14 GB 邮箱 {#example-14-gb-mailbox-with-25-gb-daily-limit}

```
Day 1:  Sync starts → ~2.5 GB downloaded → Rate-Limited
        Status: Rate-Limited, Checkpoint saved

Day 2:  Limit still active → Sync skipped

Day 3:  Limit resets → Sync resumes from checkpoint
        → Additional ~2.5 GB → Rate-Limited again

Day 4-6: Continues...

Day N:   All 14 GB synced → Status: Completed
```

## 🔧 配置 {#configuration}

### 启用带宽追踪 {#enabling-bandwidth-tracking}

在您的 `appsettings.json` 中添加以下部分：

```json
{
  "BandwidthTracking": {
    "Enabled": true,
    "DailyLimitMb": 2500,
    "WarningThresholdPercent": 80,
    "PauseHoursOnLimit": 24
  }
}
```

### 配置选项 {#configuration-options}

| 选项 | 描述 | 默认值 | 示例 |
|--------|-------------|---------|---------|
| `Enabled` | 启用/禁用带宽追踪 | `false` | `true` |
| `DailyLimitMb` | 每日下载限制（单位：MB） | `2500` | `2500` |
| `WarningThresholdPercent` | 触发日志警告的限制百分比 | `80` | `80` |
| `PauseHoursOnLimit` | 达到限制后暂停的小时数 | `24` | `24` |

### 设置正确的限制 {#setting-the-correct-limit}

要确定合适的 `DailyLimitMb` 值：

1. 查看您的电子邮件提供商的文档或服务条款
2. 查找 IMAP 带宽限制或下载配额
3. 如果使用企业邮件系统，请联系您的电子邮件管理员
4. 如果不确定，请从保守值开始（例如 2000 MB）
5. 在初始同步期间监控日志中的速率限制警告

## 👤 用户体验 {#user-experience}

### 仪表板指示器 {#dashboard-indicators}

当速率限制处于激活状态时，账户会显示：

- **状态**："Rate-Limited"
- **信息横幅**：解释当前情况及预计重置时间
- **同步进度**：显示在达到限制前已处理的邮件数量

### 自动行为 {#automatic-behavior}

1. **无需人工干预**：系统会自动处理一切
2. **应用程序重启**：如果应用程序重启，速率限制状态会保存在数据库中
3. **多账户支持**：每个账户独立追踪其带宽

### 保存内容 {#what-gets-saved}

当发生速率限制时，系统会保存：

- **同步检查点**：每个文件夹中同步停止的精确位置
- **带宽使用情况**：当前天下载的总字节数
- **限制状态**：是否达到限制以及何时重置

## 🗄️ 数据库维护 {#database-maintenance}

`DatabaseMaintenanceService` 会自动清理旧数据：

- **带宽记录**：在 7 天后删除
- **同步检查点**：已完成的检查点在 30 天后删除
- **未完成的检查点**：保留直至同步完成

## ✅ 最佳实践 {#best-practices}

### 针对大型邮箱 {#for-large-mailboxes}

1. **启用带宽追踪**：对于大于每日限制的邮箱，这至关重要
2. **规划初始同步**：大型邮箱可能需要几天时间才能完全同步
3. **监控进度**：通过仪表板检查速率限制状态
4. **不要中断**：让自动恢复机制处理后续同步

### 针对管理员 {#for-administrators}

1. **设置合适的限制**：使 `DailyLimitMb` 与您的电子邮件提供商的实际限制相匹配
2. **监控日志**：当达到限制的 80% 时会出现警告消息
3. **数据库空间**：带宽追踪占用的额外存储空间极小
4. **更改前备份**：在修改速率限制设置之前，请务必进行备份

### 性能考虑 {#performance-considerations}

- ✅ **内存影响**：微乎其微 —— 仅追踪字节，不存储邮件
- ✅ **数据库影响**：极小 —— 小额记录会自动清理
- ✅ **同步速度**：在活跃同步期间不会影响性能

## 🛡️ 瞬时 FETCH 错误 / 服务端节流 {#transient-fetch-errors-server-side-throttling}

除了上述基于字节的带宽追踪外，Mail Archiver 还能检测并响应由 IMAP 服务器本身发出的**单条消息节流（per-message throttling）**信号。一些托管 IMAP 提供商会实施与**配置的 `BatchOperation` 暂停无关**的单账户或单 IP 节流，因为这种节流是基于当前会话期间的整体请求量在服务端强制执行的。

### 症状 {#symptoms}

服务器对单个 `FETCH` 命令返回 `NO`（或 `BAD`）响应，包含如下消息：

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- 包含 `throttle`、`rate limit` 或类似标记的响应

身份验证、文件夹列表和消息搜索本身通常仍会成功 —— 只有实际的完整消息检索会失败。

### 自动处理 {#automatic-handling}

当检测到此类瞬时错误时，Mail アーチバー会应用单条消息重试策略：

1. **指数退避重试**：对同一条消息最多进行 **3 次重试**，延迟时间为 **5 s → 15 s → 60 s**。
2. **连接恢复**：在每次重试之前，都会验证连接、身份验证和文件夹选择状态，并在必要时进行恢复。
3. **成功后重置**：成功的 FETCH 会重置连续失败计数器。
4. **优雅同步暂停**：如果经过所有重试后，**连续 10 条消息**仍出现瞬时错误，则当前同步任务将优雅地暂停 —— 使用与带宽限制暂停相同的机制：
   - `LastSync` 时间戳**不会**更新。
   - 如果启用了 `BandwidthTracking`，则保留检查点，以便下次同步运行时从最后一条成功处理的消息处恢复。
   - 任务被标记为速率受限。
   - 下一次预定的同步将自动恢复。

### 缓解措施 {#mitigation}

如果您经常遇到服务端节流，可以通过调整 `BatchOperation` 设置来降低请求频率：

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

请注意，某些提供商是基于**总字节数**或**每个时间窗口内的总 FETCH 次数**进行节流，而不是基于每秒速率。在这些情况下：

- 启用 `BandwidthTracking`（见上文）并配置 `DailyLimitMb` 以匹配提供商的文档限制。当每日预算耗尽时，同步将干净地暂停，并在第二天自动恢复。
- 非常大型邮箱的初始同步可能需要几天时间，这是预期行为。

### 哪些属于瞬时错误 {#what-counts-as-a-transient-error}

只有带有节流相关文本或响应码的 IMAP `NO`/`BAD` 响应才被视为瞬时错误。其他错误（格式错误的邮件、UTF-8 解码问题等）仍会被立即计为 `FailedEmails`，并且**不会**触发重试/暂停逻辑。

## 📚 相关文档 {#related-documentation}

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - 数据库清理程序
- [GmailBestPractices.md](GmailBestPractices.md) - Gmail 特定配置
