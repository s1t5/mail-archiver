# 🔄 PostgreSQL Major Version Upgrade Guide {#postgresql-major-version-upgrade-guide}

[← Documentation Index に戻る](index.md)

## 📋 Overview {#overview}

このガイドでは、Mail Archiver Docker Compose 環境において、PostgreSQL データベースをあるメジャーバージョンから別のメジャーバージョンへアップグレードする方法を説明します。メジャーバージョンのアップグレードは、新しい PostgreSQL の機能、パフォーマンスの向上、およびセキュリティアップデートを活用するために必要です。

## ⚠️ Important Disclaimer {#important-disclaimer}
> **注意**: このアップグレード手順は一般的な PostgreSQL データベースの移行プロセスであり、Mail Archiver 特有のものではありません。これは PostgreSQL のベストプラクティスに基づいた推奨事項として提供されています。必ず事前に非本番環境で手順をテストし、作業を開始する前に適切なバックアップを取得していることを確認してください。


**このガイドに記載されているすべての手順は、まず非本番環境でテストする必要があります。** メジャーバージョンのアップグレードにはデータの移行が伴い、データの損失や破損のリスクがあります。常に以下の事項を遵守してください：
- アップグレードプロセスを開始する前に完全なバックアップを作成する
- 開発環境でアップグレード手順をテストする
- アップグレード後にすべてのアプリケーション機能を確認する
- 問題が発生した場合に備えてロールバックプランを用意する
- メンテナンスウィンドウ中にアップグレードをスケジュールする

**自己責任で使用してください。**

## 🎯 When to Upgrade {#when-to-upgrade}

以下の場合に PostgreSQL のアップグレードを検討してください：
- 現在のバージョンがサポート終了（end-of-life）に近づいている
- セキュリティアップデートのために新しいバージョンが必要である
- 大規模なシステムメンテナンスを計画している

## 🛠️ Prerequisites {#prerequisites}

- 実行中の Mail Archiver Docker セットアップ（[Setup Guide](Setup.md) を参照）
- 十分なディスク容量（現在のデータベースサイズの少なくとも 1.5 倍）
- Docker ホストシステムへのアクセス権
- 現在のデータベースの完全なバックアップ（[Backup and Restore Guide](BackupRestore.md) を参照）
- アプリケーションのダウンタイムのためのメンテナンスウィンドウ

## 📋 Current Configuration {#current-configuration}

現在の Mail Archiver セットアップでは以下を使用しています：
- **Database Name**: MailArchiver
- **Database User**: mailuser
- **Data Volume**: `/var/lib/postgresql/data` にマッピングされた `./postgres-data`

必要に応じて、以下の例のコマンドやスクリプト内でこれらを調整する必要があります。

## 🔄 Upgrade Procedures {#upgrade-procedures}

この方法は、データの論理バックアップを作成し、それを新しい PostgreSQL バージョンにリストアします。これは最も安全なアプローチであり、あらゆる PostgreSQL バージョン間で動作します。

> **Alternative Methods**: PostgreSQL は、より高速なアップグレードのための `pg_upgrade` など、他のアップグレード方法も提供しています。代替のアップグレードアプローチの詳細については、[official PostgreSQL upgrade documentation](https://www.postgresql.org/docs/current/upgrading.html) を参照してください。

#### Step 1: Prepare the Environment {#step-1-prepare-the-environment}

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

#### Step 2: Update Docker Compose Configuration {#step-2-update-docker-compose-configuration}

新しい PostgreSQL バージョンを使用するように `docker-compose.yml` ファイルを編集します：

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

**重要な変更点:**
- `postgres:17` から `postgres:18`（またはターゲットとなるバージョン）へ `image` を更新
- ボリュームマッピングを `./postgres-data:/var/lib/postgresql/data` から `./postgres-data-new:/var/lib/postgresql` に変更（内部パスの変更は postgres バージョン 18 以降で必要です）

#### Step 3: Start New PostgreSQL Instance {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### Step 4: Restore Data to New Database {#step-4-restore-data-to-new-database}

データベースのダンプを新しい PostgreSQL インスタンスにリストアします：

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
データベースのサイズによっては、これに時間がかかる場合があります。
リストア中にエラーメッセージが表示されないか注意してください。

#### Step 5: Test and Complete Migration {#step-5-test-and-complete-migration}
接続を確認するためにアプリケーションを起動します：

```bash
docker compose up -d
```

アプリケーションの機能をテストします：
- Web インターフェースにログインする
- メールの検索が機能することを確認する
- 既存のデータにアクセスできることを確認する
- バックアップ/リストア機能をテストする

すべてが正しく動作している場合は、古いデータをクリーンアップします：

コンテナを停止
```bash
docker compose down
```
古いデータディレクトリを削除（新しいセットアップが動作することを確認した後のみ）
```bash
rm -rf postgres-data
```
バックアップディレクトリを削除
```bash
rm -rf postgres-backup
```

新しいデータディレクトリを標準の名前にリネーム
```bash
mv postgres-data-new postgres-data
```

標準のボリューム名を使用するように docker-compose.yml を更新：
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

システム全体を起動
```bash
docker compose up -d
```

## ⚠️ Important Considerations {#important-considerations}

### Downtime Planning {#downtime-planning}

- システムやデータベースのサイズによっては、この手順に長い時間がかかる場合があります。
- メンテナンスウィンドウ中にスケジュールしてください。

### Disk Space Requirements {#disk-space-requirements}

十分なディスク容量（データベースサイズの約 2 倍）を確保してください。

空き容量を確認する：
```bash
df -h
du -sh postgres-data/
```

### Rollback Plan {#rollback-plan}

常にロールバックの準備をしておいてください：
1. アップグレードが検証されるまで古いデータディレクトリを保持する
2. 古い `docker-compose.yml` バックアップを維持する
3. 開発環境でロールバック手順を最初にテストする

### Post-Upgrade Verification {#post-upgrade-verification}

アップグレード後、以下を確認してください：
- データベースへのアプリケーションの接続性
- メールの検索および取得機能
- ユーザー認証とセッション管理
- バックアップおよびリストア機能
- データベースのパフォーマンス指標

## 📚 Related Documentation {#related-documentation}

- [Backup and Restore Guide](BackupRestore.md) - アップグレード前のバックアップに不可欠です
- [Database Maintenance Guide](DatabaseMaintenance.md) - アップグレード後のメンテナンス
- [Setup Guide](Setup.md) - Docker Compose の設定
- [PostgreSQL Release Notes](https://www.postgresql.org/docs/release/) - 公式リリースドキュメント
