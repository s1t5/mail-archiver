# 🔧 Database Maintenance Guide {#database-maintenance-guide}

[← Documentation Index に戻る](index.md)

## 📋 概要 {#overview}

このガイドでは、Mail Archiver における自動データベースメンテナンス機能の設定方法と使用方法について説明します。本アプリケーションには、最適なパフォーマンスを維持し、データベースの肥大化（bloat）を防ぐために、PostgreSQL データベースのメンテナンス操作を自動的に実行する組み込みのバックグラウンドサービスが含まれています。

## ⚠️ 重要事項 {#important-notice}

**PostgreSQL データベースは、データを削除しても自動的にサイズが縮小されません。** メールやメールボックスを削除すると、データベースはその領域を再利用可能としてマークしますが、実際のデータベースファイルのサイズは変わりません。この現象は「データベースの肥大化（database bloat）」と呼ばれます。

Mail Archiver には、以下の目的で毎日 `VACUUM ANALYZE` 操作を実行する自動メンテナンスサービスが含まれています：
- データベース内での再利用のためにストレージを回収する
- 最適なパフォーマンスのためにクエリ統計を更新する
- 時間経過によるパフォーマンス低下を防ぐ
- データベースの健全性を維持する

## 🚀 自動メンテナンス (推奨) {#automatic-maintenance-recommended}

### 設定 {#configuration}

`appsettings.json` で、または `docker-compose.yml` の環境変数を通じて、自動データベースメンテナンスを有効にします：

#### オプション 1: appsettings.json を使用する場合 {#option-1-via-appsettingsjson}

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### オプション 2: docker-compose.yml を使用する場合 (推奨) {#option-2-via-docker-composeyml-recommended}

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

### 設定オプション {#configuration-options}

| パラメータ | 型 | デフォルト | 説明 |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | 自動メンテナンスの有効化/無効化 |
| `DailyExecutionTime` | string | `"02:00"` | メンテナンスを実行する時刻 (HH:mm 形式、24時間表記) |
| `TimeoutMinutes` | integer | `30` | メンテナンス操作に許可される最大時間（分） |

### 仕組み {#how-it-works}

有効にすると、メンテナンスサービスは以下の動作を行います：

1. アプリケーションと共に **自動的に起動** します
2. 設定された実行時刻まで **待機** します
3. データベース全体に対して **VACUUM ANALYZE を実行** します
4. 実行時間をアプリケーションのアクセスログに **記録** します
5. 翌日のために **スケジュールを再設定** します

メンテナンス操作の特徴：
- ✅ ダウンタイムなしで実行可能
- ✅ 通常のアプリケーション操作を妨げない
- ✅ 完全なログ記録とモニタリングを提供
- ✅ データベース内での再利用のためにスペースを回収
- ✅ パフォーマンス向上のためにクエリ統計を更新

### 実行時刻の選択 {#choosing-the-execution-time}

システムの活動が最も低い時間帯を選択してください：
- **推奨**: 02:00 から 04:00 の間 (深夜帯)
- **考慮事項**: タイムゾーンの設定
- **避けるべき時間**: ピーク時間帯またはバックアップ実行時間帯

設定例：

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### 自動メンテナンスのモニタリング {#monitoring-automatic-maintenance}

#### 1. アプリケーションログ {#1-application-logs}

メンテナンス活動についてはアプリケーションログを確認してください：

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. アクセスログ (UI) {#2-access-log-ui}

すべてのメンテナンス操作はアプリケーションのアクセスログに記録されます：

1. UI で **Logs** → **Access Log** に移動します
2. タイプでフィルタリングします：**Database Maintenance**
3. 実行時刻、所要時間、およびエラーを確認します

## 🔧 手動メンテナンス (上級者向け) {#manual-maintenance-advanced}

### ディスク容量回収のための VACUUM FULL {#vacuum-full-for-disk-space-recovery}

**⚠️ アプリケーションのダウンタイムが必要です**

`VACUUM FULL` はデータベースを圧縮してディスク容量をオペレーティングシステムに返却しますが、排他的ロックを必要とします。

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**使用すべき場面:**
- 大規模なメールボックスを削除した後
- 大幅なディスク容量の回収が必要な場合
- 計画的なメンテナンス時間内

**重要:**
- テーブルサイズと同等の空きディスク容量が必要です
- 大規模なデータベースでは数時間かかる場合があります
- すべてのデータベース操作をブロックします
- オフピークの時間帯にスケジュールする必要があります

詳細な手動手順については、[PostgreSQL VACUUM Documentation](https://www.postgresql.org/docs/current/sql-vacuum.html) を参照してください。

## 📚 関連ドキュメント {#related-documentation}

- [Setup Guide](Setup.md) - 初期設定と環境変数
- [Backup and Restore Guide](BackupRestore.md) - データベースバックアップ手順
- [PostgreSQL VACUUM Documentation](https://www.postgresql.org/docs/current/sql-vacuum.html) - 公式 PostgreSQL ドキュメント

## 📝 まとめ {#summary}

✅ **自動メンテナンスを有効にする** ことで、手動操作なしでデータベースを最適化できます
✅ **アクセスログを通じてモニタリング** し、操作が成功したことを確認してください
✅ **手動の VACUUM FULL は** ディスク容量の回収が必要な場合にのみ使用してください
✅ 大規模な削除を行った場合は、メンテナンス時間中に **VACUUM FULL をスケジュール** してください
✅ 手動のメンテナンス操作を行う前には、必ず **バックアップが最新であることを確認** してください

自動メンテナンスサービスが日々のデータベース最適化を処理するため、ほとんどの場合、手動での介入は不要です。
