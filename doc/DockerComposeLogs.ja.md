# 🐳 Docker Compose Logs Guide {#docker-compose-logs-guide}

[← Documentation Index に戻る](index.md)

## 📋 概要 {#overview}

このガイドでは、Docker Compose を使用して Mail Archiver アプリケーションを実行する際のログの表示方法と設定方法について説明します。適切なログ管理は、アプリケーションの健全性の監視、問題のトラブルシューティング、およびシステム動作の把握に不可欠です。

## 📖 Docker Compose ログの表示 {#viewing-docker-compose-logs}

### 基本的なログコマンド {#basic-log-commands}

Docker Compose セットアップ内のすべてのサービスのログを表示するには：

```bash
docker compose logs
```

特定のサービス（例：Mail Archiver アプリケーション）のログを表示するには：

```bash
docker compose logs mailarchive-app
```

### リアルタイムのログストリーミング {#real-time-log-streaming}

リアルタイムでログを追跡するには（`tail -f` と同様）：

```bash
docker compose logs -f
```

特定のサービスのログを追跡するには：

```bash
docker compose logs -f mailarchive-app
```

### ログのフィルタリングとオプション {#log-filtering-and-options}

ログの直近 N 行を表示：
```bash
docker compose logs --tail=100
```

特定の時刻以降のログを表示：
```bash
docker compose logs --since=1h
```

特定の時刻までのログを表示：
```bash
docker compose logs --until=2025-01-01T12:00:00
```

ログにタイムスタンプを表示：
```bash
docker compose logs -t
```

## 📝 ログレベルの設定 {#log-level-configuration}

Mail Archiver アプリケーションは、出力の冗長性を制御するさまざまなログレベルをサポートしています。これらのレベルは、`docker-compose.yml` ファイル内の環境変数を使用して設定できます。

### 利用可能なログレベル {#available-log-levels}

以下のログレベルが利用可能です。詳細なものから順に並んでいます：

- **`Trace`** - フレームワークの内部メッセージを含む、最も詳細なログ
- **`Debug`** - 開発およびトラブルシューティング用の詳細なデバッグ情報
- **`Information`** - アプリケーションフローに関する一般的な情報（デフォルトレベル）
- **`Warning`** - 潜在的な問題に関する警告メッセージ
- **`Error`** - 失敗した操作に関するエラーメッセージ
- **`Critical`** - 即時の対応が必要な致命的なエラーメッセージ
- **`None`** - ログ出力なし

### デフォルトのログ設定 {#default-log-configuration}

デフォルトでは、アプリケーションは以下のログレベル設定を使用します：
- **Default**: `Information`
- **Microsoft.AspNetCore**: `Warning` （フレームワークのノイズを削減）
- **Microsoft.EntityFrameworkCore.Database.Command**: `Warning` （データベースコマンドのノイズを削減）

### Docker Compose でのログレベルの設定 {#configuring-log-levels-in-docker-compose}

ログレベルをカスタマイズするには、`docker-compose.yml` ファイルの `mailarchive-app` サービスの下に以下の環境変数を追加します：

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Logging Settings (Optional)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning
```

### 例：デバッグ設定 {#example-debug-configuration}

トラブルシューティングのために、より詳細なログを有効にしたい場合：

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Verbose Logging for Debugging
      - Logging__LogLevel__Default=Debug
      - Logging__LogLevel__Microsoft_AspNetCore=Information
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Debug
```

### 例：本番環境の設定 {#example-production-configuration}

本番環境では、パフォーマンスを向上させ、ログの量を減らすために、ログの冗長性を下げたい場合があります：

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Production Logging (Reduced Verbosity)
      - Logging__LogLevel__Default=Warning
      - Logging__LogLevel__Microsoft_AspNetCore=Error
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Error
```

## 🔍 ログ出力の理解 {#understanding-log-output}

### ログ形式 {#log-format}

ログは以下の一般的な形式に従います：
```
timestamp [log-level] category - message
```

例：
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### 一般的なログカテゴリ {#common-log-categories}

- **`MailArchiver.Services.*`** - アプリケーションサービスログ
- **`MailArchiver.Controllers.*`** - Web API コントローラーログ
- **`Microsoft.AspNetCore`** - ASP.NET Core フレームワークログ
- **`Microsoft.EntityFrameworkCore`** - Entity Framework ログ
- **`Microsoft.Extensions.Hosting`** - ホスティング関連のログ

## 🛠️ トラブルシューティングのヒント {#troubleshooting-tips}

### 1. エラーメッセージの検索 {#1-finding-error-messages}

ログからエラーメッセージを素早く見つけるには：
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. 特定の操作の監視 {#2-monitoring-specific-operations}

メールの同期を監視するには：
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. 警告の確認 {#3-checking-for-warnings}

警告メッセージを確認するには：
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 ログ管理のベストプラクティス {#log-management-best-practices}

### 開発時 {#for-development}
- 詳細なトラブルシューティングには `Debug` または `Information` レベルを使用する
- 開発中はリアルタイムのログストリーミングを有効にする
- 新機能のテスト中は継続的にログを監視する

### 本番環境 {#for-production}
- ログの量を減らすために `Warning` または `Error` レベルを使用する
- ディスク容量の問題を防ぐためにログローテーションを実装する
- マルチコンテナ構成の場合は、集中管理型のロギングソリューションを検討する
- セキュリティイベントやパフォーマンスの問題がないか、定期的にログを確認する

## 📚 関連ドキュメント {#related-documentation}

- [Installation and Setup Guide](Setup.md) - 完全なセットアップ手順
- [Access Logging](Logs.md) - アプリケーションレベルのアクセスログ
- [Backup and Restore Guide](BackupRestore.md) - データ保護手順
