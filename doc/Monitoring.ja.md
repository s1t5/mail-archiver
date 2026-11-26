# Health and Metrics {#health-and-metrics}

Mail-Archiver は、外部モニタリング用にヘルスエンドポイントおよび Prometheus 互換のメトリクスエンドポイントを公開しています。これらは両方とも REST API の一部です。これらは `/api/v1` の下に配置され、API キーが必要であり、そのキーが管理者によって所有されている必要があります。匿名でのヘルスパスや、個別のモニタリングトークンは存在しません。

## Requirements {#requirements}

| 要件 | 理由 |
|---|---|
| `Api:Enabled=true` | エンドポイントは `/api/` の下にあります。API が無効になっていると、認証ミドルウェアが 404 を返します。 |
| 管理者 API key | Health と metrics はインスタンス全体の状態を公開するため、管理者以外の key では 403 が返されます。 |
| `Metrics:Enabled=true` | metrics エンドポイントはオプトインです。Health はこれなしでも動作します。 |

管理者としてサインインした状態で、**API Keys** の下で key を作成してください。監視用に専用の key を使用し、可能であれば専用の管理者ユーザーを使用してください。これにより、key のローテーションやユーザーの無効化が個人のアクセスに影響を与えないようにします。

## エンドポイント {#endpoints}

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | プロセスが生存していること | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQLに到達可能であり、スキーマが存在すること | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | 同期鮮度を含む集約ステータス | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus テキスト形式 | 200 | 無効な場合は 404, 401, 403 |

これら4つはすべて、APIの他の部分と同じ認証を使用します：

- キーの欠落または無効なキー: `WWW-Authenticate: Bearer` を伴う `401`
- admin ロールを持たない有効なキー: `application/problem+json` を伴う `403`
- API が無効: `404`

### Liveness {#liveness}

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/live
```

```json
{"status":"Healthy"}
```

Liveness は PostgreSQL に触れません。これはプロセスがリクエストを受け付けていることのみを証明するため、データベースの停止によって Docker や Kubernetes が正常なプロセスを再起動することはありません。

### Readiness {#readiness}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health/ready
```

```json
{"status":"Healthy","checks":{"database":"Healthy"}}
```

このチェックは独自の PostgreSQL 接続を開き、archive スキーマを要求し、2秒後にタイムアウトします。データベースに到達できない場合、または `mail_archiver` スキーマが欠落している場合は `503` を返します。インスタンスがトラフィックを受信すべきかどうかを判断するためにこのエンドポイントを使用してください。

### Aggregate health {#aggregate-health}

```bash
curl -i -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/health
```

```json
{"status":"Degraded","checks":{"database":"Healthy","sync":"Degraded"}}
```

これはデータベースチェックと同期鮮度チェックを実行します。同期が古い（stale）場合、レポートは `Degraded` になりますが、プロセスは引き続きリクエストを処理できるため HTTP 200 のままとなります。`Unhealthy` のみが `503` を返します。

同期チェックはデフォルトで無効になっています。有効なアカウントが「古い（stale）」とみなされる経過分数を `Health:SyncStaleMinutes` に設定してください：

```yaml
Health:
  SyncStaleMinutes: 90
```

一度も同期していないアカウントは、デフォルト値で `LastSync` を持つため、チェックを有効にした直後に stale とみなされます。

## メトリクス {#metrics}

このエンドポイントは、Prometheus テキスト公開フォーマット (version 0.0.4) を返します。

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` は `true` でなければなりません。`Metrics:RefreshMinutes` はスナップショットが再構築される頻度を制御します (デフォルトは 5、1 未満の値は 1 に引き上げられます)。スクレイプ自体はスナップショットを読み取るだけなので、Prometheus によるポーリングがテーブルスキャンに変わることはありません。

| メトリクス | タイプ | ラベル | 意味 |
|---|---|---|---|
| `mailarchiver_up` | gauge | | メトリクスエンドポイントが応答しているときは 1。 |
| `mailarchiver_metrics_ready` | gauge | | 最初のスナップショットが成功した後に 1。 |
| `mailarchiver_db_up` | gauge | | 最後のリフレッシュが PostgreSQL に到達したときに 1。 |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | 最後に成功したリフレッシュの Unix 時間。 |
| `mailarchiver_emails` | gauge | | アーカイブされたメールの数。 |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | メールアカウントの数。 |
| `mailarchiver_raw_messages` | gauge | | 保存されているオリジナルメッセージの数。 |
| `mailarchiver_raw_messages_enabled` | gauge | | `RawMessages:Enabled` が設定されているときは 1。 |
| `mailarchiver_raw_messages_missing` | gauge | | 保存されたオリジナルがないアーカイブ済みメール。 |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | アカウントが一度も同期を完了していないときは 1。 |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | 最後に成功した同期の Unix 時間。一度も同期していないアカウントの場合は存在しません。 |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | 最後に成功した同期からの経過秒数。一度も同期していないアカウントの場合は存在しません。 |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | 保存できなかったオリジナルメッセージ。 |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | 失敗、レート制限、またはタイムアウトで終了した同期実行。 |

注意:

- カウンターはプロセスローカルであり、再起動後に 0 から始まります。Prometheus の `rate()` と `increase()` はそれを正しく処理します。
- `mailarchiver_raw_messages_missing` は、`RawMessages:Enabled` がオンになる前にアーカイブされたメールを含め、生メッセージのないすべてのアーカイブ済みメールをカウントします。初日からオリジナルを保存しているインストール環境では、これが関連するギャップとなります。
- `mailarchiver_account_sync_age_seconds` は真のバックログではありません。これは、サーバー上でいくつのメッセージが待機しているかではなく、最後の同期がいつ終了したかを示します。
- `mailarchiver_account_never_synced` が存在する理由は、一度も同期されていないアカウントには `sync_age` シリーズが存在しないためです。`sync_age` だけのアラートではそれを見逃してしまいます。
- エンドポイントにはアカウント名と ID が含まれますが、メールの内容、件名、または認証情報は含まれません。

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

認証ファイルには、`Bearer` プレフィックスのない生のキーが含まれています。その権限を Prometheus ユーザーに制限してください。

アラートの例:

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

イメージには、認証なしでルートパスを呼び出す liveness check が同梱されています。ルートパスはコントローラーが実行される前にログインページへリダイレクトされるため、このチェックには API key は不要であり、REST API が無効な状態でも動作し、PostgreSQL にも触れません。新規インストールやアップグレードでは、設定なしで `healthy` が報告されます。

```bash
docker inspect --format '{{.State.Health.Status}}' <container>
```

このチェックは、プロセスが HTTP リクエストを受け付けることを証明します。PostgreSQL に到達可能であることを証明するものではありません。それを確認するには、外部のモニタリングホストから admin API key を使用して `/api/v1/health/ready` と `Api:Enabled=true` を呼び出してください。データベースがダウンしている場合、このエンドポイントは 503 を返します。

同梱されている `docker-compose.yml` はチェックを上書きしません。異なる interval、timeout、または command が必要な場合は、サービスに `healthcheck` ブロックを追加するか、チェックをオフにするには `healthcheck: disable: true` を設定してください。

以前の `MAILARCHIVER_HEALTH_API_KEY` 変数はもう使用されていません。既存の compose ファイルや `.env` ファイルから削除してください。そのまま残しておいても害はありません。

## Security {#security}

- このキーは管理者キーです。APIを通じてアーカイブ全体を読み取ることができます。管理者パスワードと同様に扱ってください。
- モニタリングには専用の管理者ユーザーを使用してください。そのユーザーを無効にすると、外部のヘルスおよびメトリクスエンドポイントは無効になりますが、コンテナ自体のliveness checkは無効になりません。
- キーはファイルまたはDocker secretに保存し、決してクエリパラメータとして使用しないでください。クエリパラメータはプロキシやアクセスログに残ります。
- モニタリングホストまたはPrometheusネットワークへのアクセスを制限してください。APIキーは認証手段であり、ネットワーク境界の代わりになるものではありません。
- ヘルスエンドポイントはステータス文字列のみを返します。例外の詳細はアプリケーションログに保持されます。

## Troubleshooting {#troubleshooting}

| 症状 | 原因 | 解決策 |
|---|---|---|
| 4つのパスすべてで 404 | `Api:Enabled=false` | `Api:Enabled=true` を設定して再起動してください。 |
| `/metrics` でのみ 404 | `Metrics:Enabled=false` | `Metrics:Enabled=true` を設定して再起動してください。 |
| 401 | キーの欠落、誤り、失効、または期限切れ | 新しいキーを作成し、`Authorization: Bearer` ヘッダーを確認してください。 |
| 403 | キーが管理者以外のユーザーに属している | 管理者キーを使用してください。 |
| 1分以上 `mailarchiver_metrics_ready 0` | リフレッシュサービスが最初の実行を完了していないか、データベースに到達できない | アプリケーションログで `Could not refresh the monitoring metrics snapshot` を確認してください。 |
| `mailarchiver_db_up 0` | 直前のリフレッシュが失敗した | PostgreSQL と接続文字列を確認してください。古いカウントは次の成功まで残ります。 |
| コンテナが unhealthy のまま | キーが空、または `Api:Enabled=false` | 両方を設定してコンテナを再作成してください。 |
| `health/ready` が 503 を返す | PostgreSQL に到達できないか、スキーマが不足している | データベースとマイグレーションが実行されたかを確認してください。 |

## メトリクスがカバーしない内容 {#what-the-metrics-do-not-cover}

- アカウントごとの IMAP または Graph の到達可能性。同期の経過時間は、最後の実行が古いことのみを示します。
- サーバー上で待機しているメッセージの数（真のバックログ）。
- 保存されているすべてのオリジナルに対するチェックサム検証。これについては、既存の raw message verification および orphan cleanup を使用してください。
- フォルダごとのカウントやメールボックスのサイズ。

これらについては、アカウントページ、バックグラウンドジョブページ、およびアプリケーションログを使用してください。
