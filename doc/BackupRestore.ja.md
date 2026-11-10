# 💾 バックアップと復元ガイド {#backup-and-restore-guide}

[← ドキュメントインデックスに戻る](index.md)

## 📋 概要 {#overview}

このガイドでは、Mail Archiver Docker環境のバックアップと復元に関する詳細な手順を提供します。アーカイブされたメールと設定データを保護するために、定期的なバックアップは不可欠です。

## ⚠️ 重要な免責事項 {#important-disclaimer}

**このガイドで提供されるすべてのスクリプトとコマンドは例示に過ぎません。** それらの機能や、お客様の特定の環境への適合性について、いかなる保証も責任も負いません。常に以下の事項を遵守してください：
- バックアップおよび復元手順は、まず本番環境ではない環境でテストすること
- バックアップが完全であり、復元可能であることを確認すること
- スクリプトやコマンドをお客様の特定のセットアップや要件に合わせて調整すること
- 異なる場所に複数のバックアップコピーを保持すること
- 災害復旧手順を定期的にテストすること

**自己責任で使用してください。**

## 🎯 バックアップの推奨事項 {#backup-recommendations}

### オフラインバックアップ（強く推奨） {#offline-backups-highly-recommended}
データの整合性と完全性を最大限に確保するため、**常にアプリケーションとデータベースを停止した状態でバックアップを実行してください**。これにより、以下が保証されます：
- バックアップ中にアクティブなデータベーストランザクションが発生しない
- すべてのファイルにわたって完全なデータ整合性が保たれる
- 同時書き込みによるファイル破損のリスクがない
- 信頼性の高い復元機能

ホットバックアップ（方法2および3）はダウンタイムを最小限に抑えるには便利ですが、不整合のリスクが高まります。重要な本番環境では、メンテナンス時間中に定期的なオフラインバックアップをスケジュールしてください。

### システムレベルのバックアップ {#system-level-backups}
アプリケーション固有のバックアップに加えて、システムインフラ全体をバックアップすることを**強く推奨します**：
- **LXCコンテナのバックアップ**: LXCコンテナで実行している場合は、コンテナ全体のオフラインスナップショットを作成してください
- **仮想マシンのバックアップ**: VMデプロイメントの場合は、VM全体のフルスナップショットを作成してください
- **ホストシステムのバックアップ**: Dockerボリューム、コンテナ設定、およびシステム設定を含めてください
- **設定ファイル**: すべてのDockerおよびシステム設定ファイルを個別にバックアップしてください

システムレベルのバックアップは追加の安全層を提供し、必要に応じて環境全体を別のホストに復元できるなど、完全な災害復旧を可能にします。

## 🛠️ 前提条件 {#prerequisites}

- 実行中の Mail Archiver Docker セットアップ（[セットアップガイド](Setup.md)を参照）
- Dockerホストシステムへのアクセス権
- バックアップ用の十分なストレージ容量

## 📦 バックアップ対象 {#what-to-backup}

Mail Archiver Docker環境は、バックアップが必要ないくつかの重要なデータボリュームで構成されています：

### 1. PostgreSQL Database {#1-postgresql-database}
- **Volume**: `./postgres-data`
- **内容**: すべてのアーカイブ済みメール、ユーザーアカウント、メールアカウント、およびアプリケーションデータ
- **重要度**: ⭐⭐⭐ 重要 - これがメインのデータストアです

### 2. Data Protection Keys {#2-data-protection-keys}
- **Volume**: `./data-protection-keys`
- **内容**: クッキーの暗号化と認証に使用される ASP.NET Core Data Protection キー
- **重要度**: ⭐⭐ 重要 - 既存のユーザーセッションと安全な認証に必要です

### 2a. Original Message Files (`RawMessages.Storage=filesystem`を使用している場合のみ) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **Volume**: `./raw-messages` (`/app/RawMessages`のホスト側)
- **内容**: すべてのメールのオリジナルの `.eml`。gzip圧縮されており、`RawMessages:Enabled`が設定され、ストレージバックエンドが `filesystem`であるときに書き込まれます
- **重要度**: ⭐⭐⭐ 使用時は **極めて重要** - データベースにはチェックサム、サイズ、相対パスのみが保持されるため、このディレクトリなしで復元すると、配信できないオリジナルファイルが残ることになります
- このディレクトリとデータベースのダンプは**必ずセットで**バックアップしてください。ファイルなしでデータベースのみを復元しても、アーカイブ側で修復することはできません。ダウンロードは `404` に回答できず、チェックサムも他のものから再導出できません。データベースなしでファイルのみを復元しても、それらを参照するものがないため、同様に無意味です。
- `RawMessages.Storage=postgres` (デフォルト) を使用している場合、このディレクトリは存在せず、オリジナルは以前と同様に `./postgres-data` 内にあります。
- 機能の詳細およびバックエンドの切り替え方法については、[Original Message Storage](RawMessages.md) を参照してください。

### 3. Docker Compose Configuration {#3-docker-compose-configuration}
- **File**: `docker-compose.yml`
- **内容**: コンテナ設定、環境変数、および設定
- **重要度**: ⭐⭐ 重要 - アプリケーションの設定が含まれています

## 🔄 バックアップ手順 {#backup-procedures}

### 方法 1: 完全なバックアップ (推奨) {#method-1-complete-backup-recommended}

この方法は、データの整合性を確保するために、コンテナを停止した状態ですべてのデータをバックアップします。

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

### 方法 2: ホットバックアップ (最小限のダウンタイム) {#method-2-hot-backup-minimal-downtime}

この方法は、システムを稼働させたまま、最小限のダウンタイムで完全なバックアップを作成します。

**注意:** `mailuser` と `MailArchiver` は、`docker-compose.yml` から取得した実際のデータベース認証情報に置き換えてください（`POSTGRES_USER` と `POSTGRES_DB` 環境変数を確認してください）。

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

### 方法 3: 自動バックアップスクリプト {#method-3-automated-backup-script}

定期的な自動バックアップのために、すぐに使用できるバックアップスクリプトを提供しています。スクリプトは [backup.sh](attachments/backup.sh) で入手可能です。

**機能:**
- ✅ docker-compose.yml からのデータベース認証情報の自動検出
- ✅ ホットバックアップ (最小限のダウンタイム)
- ✅ 同時バックアップを防ぐロックファイルメカニズム
- ✅ バックアップ前のディスク容量チェック
- ✅ 古いバックアップの自動クリーンアップ

**設定:**

スクリプトを使用する前に、ファイル上部の設定セクションを編集してください：

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

**セットアップ:**

1. **スクリプトを任意の場所にコピーします:**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **設定を編集します:**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # 設定を調整してください
   ```

3. **スクリプトに実行権限を与えます:**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **スクリプトを手動でテストします:**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **バックアップを確認します:**
   - バックアップディレクトリに作成された `.tar.gz` ファイルがあるか確認してください
   - ログファイルを確認し、エラーや警告がないか確認してください

**自動バックアップのスケジューリング:**

cron を使用して自動バックアップをスケジュールするには：

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

## 🔙 復元手順 {#restore-procedures}

復元手順は、バックアップ方法と一致するように設計されています。使用したバックアップ方法に基づいて、適切な復元方法を選択してください：

- **方法 1 (完全なバックアップ)**: 「完全なバックアップからのフル復元」手順を使用してください。この方法は、`postgres-data` ディレクトリを含む完全なバックアップがある場合に使用します。
- **方法 2 (ホットバックアップ)** または **方法 3 (自動バックアップスクリプト)**: 「SQLダンプからの復元」手順を使用してください（データ保護キーとデータベースを処理します）。この方法は、データベースのSQLダンプとデータ保護キーを含むホットバックアップがある場合に使用します。

利便性を高めるため、すべての復元手順を自動的に処理できる自動復元スクリプトも用意しています。以下の手順を手動で行う代わりに、このスクリプトを使用することもできます。

### 自動復元スクリプト {#automated-restore-script}

復元スクリプトは [restore.sh](attachments/restore.sh) ファイルで入手可能です。このスクリプトは、ガイド付きの復元を提供し、バックアップ方法、バックアップtarファイルへのパス、およびインストールディレクトリを尋ねた後、復元プロセスを自動的に実行します。

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

スクリプトが復元プロセスを案内し、使用したバックアップ方法に基づいてすべてのステップを自動的に処理します。

### 完全なバックアップからのフル復元 (手動方法) {#full-restore-from-complete-backup-manual-method}

これは、完全なバックアップアーカイブからすべてのデータを復元します。この方法は、(方法1によって作成された) `postgres-data` ディレクトリを含む完全なバックアップがある場合に使用する必要があります。

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

### SQLダンプからの復元 (手動方法) {#restore-from-sql-dump-manual-method}

これは、ホットバックアップ (方法2および3) からデータベースとデータ保護キーを復元します。

**注意:** 以下のコマンドは例示の認証情報 (`mailuser` および `MailArchiver`) を使用しています。これらを `docker-compose.yml` ファイルの実際の値に置き換える必要があります。`postgres` サービスの `POSTGRES_USER` および `POSTGRES_DB` 環境変数を確認してください。

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


## ⚠️ 重要な注意事項 {#important-notes}

### データベース接続設定 {#database-connection-settings}
`docker-compose.yml` におけるデータベース接続設定が、バックアップと復元の間で一致していることを確認してください：
- `POSTGRES_USER` (デフォルト: mailuser)
- `POSTGRES_PASSWORD` (デフォルト: masterkey)
- `POSTGRES_DB` (デフォルト: MailArchiver)

### Data Protection Keys {#data-protection-keys}
`data-protection-keys` ディレクトリは以下に使用されます：
- ユーザー認証およびセッション管理
- クッキーの暗号化
- 安全なデータ処理

これらのキーがないと、既存のユーザーセッションが無効になり、ユーザーは再度ログインする必要があります。

### 環境変数 {#environment-variables}
すべてのアプリケーション設定は、`docker-compose.yml` 内の環境変数に保存されます。以下の点に注意してください：
- `docker-compose.yml` ファイルをバックアップする
- カスタム設定を文書化する
- 復元時にも同じ設定を使用する

## 🔒 セキュリティのベストプラクティス {#security-best-practices}

1. **バックアップの暗号化**: バックアップアーカイブには、特に機密性の高いメールデータが含まれる場合、暗号化を検討してください。

2. **安全な保管**: バックアップは安全な場所に保管してください：
   - 別のサーバーまたはストレージシステムを使用する
   - アクセス制御を実装する
   - オフサイトまたはクラウドストレージを検討する

3. **定期的なテスト**: バックアップと復元手順が正しく動作することを確認するために、定期的にテストを行ってください。

4. **バックアップサイズの監視**: 十分なストレージ容量を確保するために、バックアップのサイズを追跡してください。

5. **バックアップの保持**: バックアップの保持ポリシーを定義し、実施してください (例: 日次バックアップは7日間、週次バックアップは4週間、月次バックアップは12ヶ月間保持するなど)。

6. **デフォルトパスワードの変更**: 設定内のデフォルトパスワードは安全ではないため、本番環境で使用する場合は変更する必要があります。


## 🖥️ Proxmox バックアップ {#proxmox-backup}

Mail Archiver Docker環境を Proxmox LXC コンテナで実行している場合は、データの整合性を確保するために、コンテナ全体をバックアップする際は "stop" モードを使用することをお勧めします。

### なぜ Stop モードが推奨されるのか {#why-stop-mode-is-recommended}

1. **データの整合性**: コンテナを停止することで、バックアップが取得される前にすべてのデータベーストランザクションが完了し、データがディスクに書き込まれることが保証されます。
2. **データ破損の防止**: コンテナがアクティブな状態でバックアップを実行すると、特にデータベース操作においてデータ破損につながる可能性があります。
3. **ファイルシステムの整合性**: コンテナを停止することで、すべてのファイルシステム操作が完了することを保証し、バックアップ中の部分的な書き込みを防ぎます。

### Proxmox バックアップに関する注意点 {#proxmox-backup-notes}

- 常にバックアップ用の十分なストレージ容量があることを確認してください。
- バックアップが有効であることを確認するために、定期的に復元手順をテストしてください。
- 自動バックアップのために、Proxmox ウェブインターフェースでバックアップスケジュールの設定を検討してください。
- バックアップログを確認し、エラーや問題がないか監視してください。
