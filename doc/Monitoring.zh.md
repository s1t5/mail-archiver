# Health and Metrics {#health-and-metrics}

Mail-Archiver 提供了健康检查端点和兼容 Prometheus 的指标端点，用于外部监控。两者都是 REST API 的一部分：它们位于 `/api/v1` 下，需要 API key，并且要求该 key 属于管理员。不存在匿名的健康检查路径，也不存在独立的监控令牌。

## Requirements {#requirements}

| Requirement | Why |
|---|---|
| `Api:Enabled=true` | 端点位于 `/api/` 下。如果禁用了 API，身份验证中间件将返回 404。 |
| An admin API key | 健康检查和指标会公开实例范围的状态，因此非管理员密钥会收到 403。 |
| `Metrics:Enabled=true` | 指标端点是可选开启的。健康检查在没有它时仍可工作。 |

请在以管理员身份登录时，在 **API Keys** 下创建密钥。请使用专用密钥，并且如果可能的话，为监控使用专用的管理员用户，这样轮换密钥或禁用用户就不会影响个人的访问权限。

## Endpoints {#endpoints}

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | 进程存活 | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQL 可达且 schema 存在 | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | 包含同步新鲜度的聚合状态 | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus 文本格式 | 200 | 禁用时返回 404, 401, 403 |

所有四个端点都使用与 API 其余部分相同的身份验证方式：

- 缺失或无效的 key：`401` 配合 `WWW-Authenticate: Bearer`
- 具有有效 key 但没有 admin 角色：`403` 配合 `application/problem+json`
- API 已禁用：`404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Liveness 不会触碰 PostgreSQL。它仅证明进程可以接受请求，因此数据库故障不会导致 Docker 或 Kubernetes 重启一个健康的进程。

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

该检查会打开自己的 PostgreSQL 连接，查询 archive schema，并在两秒后放弃。当数据库无法访问或 `mail_archiver` schema 缺失时，它会返回 `503`。使用此端点来决定一个实例是否应该接收流量。

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

此检查会运行数据库检查和同步新鲜度检查。同步过期会导致报告变为 `Degraded`，但仍保持 HTTP 200，因为进程仍然可以处理请求。只有 `Unhealthy` 会返回 `503`。

同步检查默认是禁用的。设置 `Health:SyncStaleMinutes` 为启用账户被视为过期的分钟数：

```yaml
Health:
  SyncStaleMinutes: 90
```

从未同步过的账户在默认值下为 `LastSync`，因此一旦启用检查，它就会被视为过期。

## Metrics {#metrics}

该端点返回 Prometheus 文本展示格式（version 0.0.4）：

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` 必须为 `true`。`Metrics:RefreshMinutes` 控制快照重建的频率（默认为 5，小于 1 的值会被提升为 1）。抓取过程本身仅读取快照，因此 Prometheus 的轮询不会演变成全表扫描。

| Metric | Type | Labels | Meaning |
|---|---|---|---|
| `mailarchiver_up` | gauge | | 当指标端点响应时为 1。 |
| `mailarchiver_metrics_ready` | gauge | | 第一次成功生成快照后为 1。 |
| `mailarchiver_db_up` | gauge | | 当最后一次刷新到达 PostgreSQL 时为 1。 |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | 最后一次成功刷新的 Unix 时间戳。 |
| `mailarchiver_emails` | gauge | | 已归档邮件的数量。 |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | 邮件账户的数量。 |
| `mailarchiver_raw_messages` | gauge | | 存储的原始消息数量。 |
| `mailarchiver_raw_messages_enabled` | gauge | | 当 `RawMessages:Enabled` 设置时为 1。 |
| `mailarchiver_raw_messages_missing` | gauge | | 没有存储原始消息的已归档邮件数量。 |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | 当账户从未完成同步时为 1。 |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | 最后一次成功同步的 Unix 时间戳。从未同步过的账户则缺失此项。 |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | 自上次成功同步以来的秒数。从未同步过的账户则缺失此项。 |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | 无法存储的原始消息数量。 |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | 以失败、速率限制或超时告终的同步运行次数。 |

注意：

- Counter 是进程本地的，在重启后从 0 开始。Prometheus 中的 `rate()` 和 `increase()` 可以正确处理这一点。
- `mailarchiver_raw_messages_missing` 统计了每一个没有原始消息的已归档邮件，包括在 `RawMessages:Enabled` 开启之前归档的邮件。对于从第一天起就存储原始消息的安装实例，它是相关的缺口。
- `mailarchiver_account_sync_age_seconds` 并不是真正的积压量。它表示的是最后一次同步何时完成，而不是服务器上有多少消息在等待。
- `mailarchiver_account_never_synced` 的存在是因为从未同步过的账户没有 `sync_age` 系列。仅针对 `sync_age` 的告警会遗漏这种情况。
- 该端点包含账户名称和 ID，但不包含邮件内容、主题或凭据。

## Prometheus {#prometheus}

```yaml
scrape_configs:
  - job_name: mail-archiver
    scrape_interval: 30s
    authorization:
      credentials_file: /etc/prometheus/mailarchiver-api-key
    static_configs:
      - targets: ["mail-archiver:5000"]
```

凭据文件包含原始密钥，不带 `Bearer` 前缀。请将其权限限制为 Prometheus 用户。

示例告警：

```yaml
groups:
  - name: mail-archiver
    rules:
      - alert: MailArchiverSyncStale
        expr: mailarchiver_account_sync_age_seconds{enabled="true"} > 3600
        for: 15m

      - alert: MailArchiverNeverSynced
        expr: mailarchiver_account_never_synced{enabled="true"} == 1
        for: 30m

      - alert: MailArchiverSyncFailures
        expr: increase(mailarchiver_sync_failures_total[1h]) > 0

      - alert: MailArchiverMissingOriginals
        expr: mailarchiver_raw_messages_enabled == 1 and mailarchiver_raw_messages_missing > 0

      - alert: MailArchiverDatabaseDown
        expr: mailarchiver_db_up == 0
        for: 5m

      - alert: MailArchiverMetricsStale
        expr: time() - mailarchiver_metrics_refresh_timestamp_seconds > 900
```

## Docker health check {#docker-health-check}

该镜像及其内置的 `docker-compose.yml` 使用管理员密钥检查 `/api/v1/health/live`。在首次启动之前，该密钥尚不存在，因此首次启动时不会使用该密钥，并且在密钥设置之前，容器会报告 `unhealthy`。

1. 使用空的 `MAILARCHIVER_HEALTH_API_KEY` 启动一次 stack。
2. 以管理员身份登录，并在 **API Keys** 下创建一个名为 `monitoring-health` 的密钥。
3. 将密钥放入 `.env`：

   ```dotenv
   MAILARCHIVER_HEALTH_API_KEY=<key>
   ```

4. 使用 `docker compose up -d` 重新创建容器。
5. 检查状态：

   ```bash
   docker inspect --format '{{.State.Health.Status}}' <container>
   ```

需要 `Api:Enabled=true`。如果禁用了 API，健康检查将收到 404，并且容器将保持 unhealthy 状态。

如果您不想在容器环境中配置管理员密钥，请在容器中使用 TCP 检查，并从外部监控主机使用该密钥调用 `/api/v1/health/ready`。
TCP 检查无法判断 PostgreSQL 是否可达。

## Security {#security}

- 该密钥是一个管理员密钥。它可以通过 API 读取整个归档文件。请像对待管理员密码一样对待它。
- 使用专门的管理员用户进行监控。禁用该用户会导致健康检查和指标功能失效。
- 将密钥保存在文件或 Docker secret 中，切勿将其放在查询参数中。查询参数最终会记录在代理和访问日志中。
- 限制对监控主机或 Prometheus 网络的访问。API 密钥是身份验证手段，不能替代网络边界。
- 健康检查端点仅返回状态字符串。异常详情保留在应用程序日志中。

## 故障排除 {#troubleshooting}

| 症状 | 原因 | 修复方法 |
|---|---|---|
| 所有四个路径均返回 404 | `Api:Enabled=false` | 设置 `Api:Enabled=true` 并重启。 |
| 仅在 `/metrics` 上返回 404 | `Metrics:Enabled=false` | 设置 `Metrics:Enabled=true` 并重启。 |
| 401 | 密钥缺失、错误、已撤销或已过期 | 创建新密钥并检查 `Authorization: Bearer` 请求头。 |
| 403 | 密钥属于非管理员用户 | 使用管理员密钥。 |
| `mailarchiver_metrics_ready 0` 持续超过一分钟 | 刷新服务尚未完成首次运行，或无法连接到数据库 | 检查应用程序日志中的 `Could not refresh the monitoring metrics snapshot`。 |
| `mailarchiver_db_up 0` | 上次刷新失败 | 检查 PostgreSQL 和连接字符串。旧计数将保留，直到下次刷新成功。 |
| 容器保持 unhealthy 状态 | 密钥为空或 `Api:Enabled=false` | 同时设置两者并重新创建容器。 |
| `health/ready` 返回 503 | PostgreSQL 无法访问或缺少 schema | 检查数据库以及迁移是否已运行。 |

## 指标未涵盖的内容 {#what-the-metrics-do-not-cover}

- 每个账户的 IMAP 或 Graph 可达性。同步时长（sync age）仅显示最后一次运行的时间已久。
- 服务器上等待的消息数量，即真实的积压量（backlog）。
- 每个存储的原始消息的校验和（Checksum）验证。请使用现有的原始消息验证（raw message verification）和孤儿清理（orphan cleanup）功能。
- 每个文件夹的数量或邮箱大小。

对于这些信息，请使用账户页面（account page）、后台作业页面（background jobs page）和应用程序日志（application log）。
