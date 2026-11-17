# Health and Metrics {#health-and-metrics}

Mail-Archiverは、外部モニタリング用にヘルスエンドポイントとPrometheus互換のメトリクスエンドポイントを公開しています。これらは両方ともREST APIの一部です。これらは`/api/v1`の下に配置され、APIキーが必要であり、そのキーが管理者権限を持っている必要があります。匿名でアクセスできるヘルスパスや、個別のモニタリング用トークンは存在しません。

## 要件 {#requirements}

| 要件 | 理由 |
|---|---|
| `Api:Enabled=true` | エンドポイントは `/api/` の下にあります。APIが無効になっていると、認証ミドルウェアが404を返します。 |
| 管理者APIキー | ヘルスチェックとメトリクスはインスタンス全体の状態を公開するため、管理者以外のキーでは403が返されます。 |
| `Metrics:Enabled=true` | メトリクスエンドポイントはオプトインです。ヘルスチェックはそれがなくても動作します。 |

管理者としてサインインした状態で、**API Keys** の下でキーを作成してください。監視用に専用のキーを使用し、可能であれば専用の管理者ユーザーを使用してください。これにより、キーのローテーションやユーザーの無効化が個人のアクセスに影響を与えないようにできます。

## Endpoints {#endpoints}

| Method | Path | Purpose | Success | Errors |
|---|---|---|---|---|
| GET | `/api/v1/health/live` | プロセスが生存している | 200 | 401, 403 |
| GET | `/api/v1/health/ready` | PostgreSQLに到達可能であり、スキーマが存在する | 200 | 503, 401, 403 |
| GET | `/api/v1/health` | 同期の鮮度を含む集計ステータス | 200 | 503, 401, 403 |
| GET | `/api/v1/metrics` | Prometheus テキスト形式 | 200 | 無効な場合は 404, 401, 403 |

これら4つはすべて、APIの他のエンドポイントと同じ認証を使用します：

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

Liveness は PostgreSQL にアクセスしません。これはプロセスがリクエストを受け付けることのみを証明するため、データベースの停止によって Docker や Kubernetes が正常なプロセスを再起動してしまうことを防ぎます。

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

これはデータベースのチェックと同期の鮮度のチェックを実行します。同期が古い場合、レポートは `Degraded` となりますが、プロセスは引き続きリクエストを処理できるため、HTTP 200 のままとなります。`Unhealthy` のみが `503` を返します。

同期チェックはデフォルトで無効になっています。有効なアカウントが「古い」とみなされる経過分数を `Health:SyncStaleMinutes` に設定してください：

```yaml
Health:
  SyncStaleMinutes: 90
```

一度も同期されていないアカウントは、デフォルト値で `LastSync` となるため、チェックを有効にした直後から「古い」とみなされます。

## メトリクス {#metrics}

このエンドポイントは、Prometheus text exposition format (version 0.0.4) を返します。

```bash
curl -H "Authorization: Bearer <admin-key>" \
  https://host/api/v1/metrics
```

`Metrics:Enabled` は `true` である必要があります。`Metrics:RefreshMinutes` はスナップショットが再構築される頻度を制御します（デフォルトは 5、1 未満の値は 1 に切り上げられます）。スクレイプ自体はスナップショットを読み取るだけなので、Prometheus のポーリングによってテーブルスキャンが発生することはありません。

| メトリクス | タイプ | ラベル | 意味 |
|---|---|---|---|
| `mailarchiver_up` | gauge | | メトリクスエンドポイントが応答しているときに 1 となります。 |
| `mailarchiver_metrics_ready` | gauge | | 最初のスナップショット作成に成功した後、1 となります。 |
| `mailarchiver_db_up` | gauge | | 最後の更新が PostgreSQL に到達したときに 1 となります。 |
| `mailarchiver_metrics_refresh_timestamp_seconds` | gauge | | 最後に更新が成功したときの Unix time です。 |
| `mailarchiver_emails` | gauge | | アーカイブされたメールの数です。 |
| `mailarchiver_accounts` | gauge | `state` (`enabled`, `disabled`) | メールアカウントの数です。 |
| `mailarchiver_raw_messages` | gauge | | 保存されているオリジナルメッセージの数です。 |
| `mailarchiver_raw_messages_enabled` | gauge | | `RawMessages:Enabled` が設定されているときに 1 となります。 |
| `mailarchiver_raw_messages_missing` | gauge | | オリジナルが保存されていないアーカイブ済みメールの数です。 |
| `mailarchiver_account_never_synced` | gauge | `account_id`, `account`, `enabled` | アカウントが一度も同期を完了していないときに 1 となります。 |
| `mailarchiver_account_last_sync_timestamp_seconds` | gauge | `account_id`, `account`, `enabled` | 最後に同期が成功したときの Unix time です。一度も同期していないアカウントでは存在しません。 |
| `mailarchiver_account_sync_age_seconds` | gauge | `account_id`, `account`, `enabled` | 最後に同期が成功してからの秒数です。一度も同期していないアカウントでは存在しません。 |
| `mailarchiver_raw_message_failures_total` | counter | `reason` (`size`, `write`, `no_capture`) | 保存できなかったオリジナルメッセージの数です。 |
| `mailarchiver_sync_failures_total` | counter | `reason` (`failed`, `rate_limited`, `timeout`) | 失敗、レート制限、またはタイムアウトで終了した同期実行の回数です。 |

注意:

- カウンターはプロセスローカルであり、再起動後に 0 から始まります。Prometheus の `rate()` と `increase()` はこれを正しく処理します。
- `mailarchiver_raw_messages_missing` は、`RawMessages:Enabled` が有効になる前にアーカイブされたメールを含め、生メッセージのないすべてのアーカイブ済みメールをカウントします。初日からオリジナルを保存しているインストール環境では、これが関連するギャップとなります。
- `mailarchiver_account_sync_age_seconds` は真のバックログではありません。これは、サーバー上でいくつのメッセージが待機しているかではなく、最後の同期がいつ完了したかを示します。
- `mailarchiver_account_never_synced` が存在する理由は、一度も同期されていないアカウントには `sync_age` シリーズが存在しないためです。`sync_age` だけのアラートではこれを見逃してしまいます。
- エンドポイントにはアカウント名と ID が含まれていますが、メールの内容、件名、または認証情報は含まれていません。

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

イメージと同梱の `docker-compose.yml` は、管理者キーを使用して `/api/v1/health/live` をチェックします。このキーは初回起動前には存在できないため、初回起動時はキーなしで実行され、キーが設定されるまでコンテナは `unhealthy` を報告します。

1. `MAILARCHIVER_HEALTH_API_KEY` が空の状態でスタックを一度起動します。
2. 管理者としてサインインし、**API Keys** の下で `monitoring-health` という名前のキーを作成します。
3. キーを `.env` に設定します：

   ```dotenv
   MAILARCHIVER_HEALTH_API_KEY=<key>
   ```

4. `docker compose up -d` を使用してコンテナを再作成します。
5. 状態を確認します：

   ```bash
   docker inspect --format '{{.State.Health.Status}}' <container>
   ```

`Api:Enabled=true` が必要です。API が無効になっていると、ヘルスチェックは 404 を受信し、コンテナは unhealthy のままになります。

コンテナ環境に管理者キーを置きたくない場合は、コンテナ内で TCP チェックを使用し、外部の監視ホストからキーを使用して `/api/v1/health/ready` を呼び出してください。
TCP チェックでは、PostgreSQL に到達可能かどうかを判断することはできません。

## Security {#security}

- このキーは管理者キーです。APIを通じてアーカイブ全体を読み取ることができます。管理者パスワードと同様に扱ってください。
- モニタリングには専用の管理者ユーザーを使用してください。そのユーザーを無効にすると、ヘルスチェックとメトリクスも無効になります。
- キーはファイルまたは Docker secret に保存し、決してクエリパラメータとして使用しないでください。クエリパラメータはプロキシやアクセスログに残ります。
- モニタリングホストまたは Prometheus ネットワークへのアクセスを制限してください。APIキーは認証手段であり、ネットワーク境界の代わりになるものではありません。
- ヘルスエンドポイントはステータス文字列のみを返します。例外の詳細はアプリケーションログに保持されます。

## Troubleshooting {#troubleshooting}

| 症状 | 原因 | 解決策 |
|---|---|---|
| 4つのパスすべてで404 | `Api:Enabled=false` | `Api:Enabled=true` を設定して再起動してください。 |
| `/metrics` でのみ404 | `Metrics:Enabled=false` | `Metrics:Enabled=true` を設定して再起動してください。 |
| 401 | キーの欠落、誤り、失効、または期限切れ | 新しいキーを作成し、`Authorization: Bearer` ヘッダーを確認してください。 |
| 403 | キーが管理者以外のユーザーに属している | 管理者キーを使用してください。 |
| 1分以上 `mailarchiver_metrics_ready 0` | リフレッシュサービスが初回実行を完了していないか、データベースに到達できない | アプリケーションログで `Could not refresh the monitoring metrics snapshot` を確認してください。 |
| `mailarchiver_db_up 0` | 前回のリフレッシュが失敗した | PostgreSQL と接続文字列を確認してください。古いカウントは、次に成功するまで保持されます。 |
| コンテナが unhealthy のまま | キーが空、または `Api:Enabled=false` | 両方を設定してコンテナを再作成してください。 |
| `health/ready` が 503 を返す | PostgreSQL に到達できないか、スキーマが不足している | データベースとマイグレーションが実行されたかを確認してください。 |

## メトリクスがカバーしない内容 {#what-the-metrics-do-not-cover}

- アカウントごとの IMAP または Graph の到達可能性。同期の経過時間は、最後の実行が古いことのみを示します。
- サーバーで待機中のメッセージ数（真のバックログ）。
- 保存されたすべてのオリジナルに対するチェックサム検証。これには、既存の raw message verification および orphan cleanup を使用してください。
- フォルダーごとのカウントまたはメールボックスのサイズ。

これらについては、アカウントページ、バックグラウンドジョブページ、およびアプリケーションログを使用してください。
