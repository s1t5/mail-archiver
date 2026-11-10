# 🖥️ CLI Commands {#cli-commands}

[← Documentation Index に戻る](index.md)

## 📋 Overview {#overview}

Web UIでは行えない作業があります。例えば、ローカルファイルシステムから既存のアーカイブをインポートしたり、保存されたオリジナルメッセージをストレージバックエンド間で移動したりする場合です。Mail Archiverは、これらをコマンドラインからのコマンドとして受け付けます。

| Command | Purpose |
|----------|---------|
| `--import-mbox` | マウントされたディレクトリからMBoxファイルをインポートする |
| `--import-eml` | マウントされたディレクトリからEMLファイルのZIPアーカイブをインポートする |
| `--migrate-raw-storage` | 保存されたオリジナルメッセージをデータベースとファイルシステムの間で移動する |

すべてのコマンドはWebパイプラインが開始される前に処理され、完了すると終了します。そのため、アーカイブの実行中に `docker compose exec` と併用しても安全です。

## ⚠️ セキュリティモデル {#security-model}

インポートコマンドは **CLI専用** です。ローカルファイルのブラウジングやインポートを行うためのウェブエンドポイントは存在しません。これらのコマンドを実行するには、Dockerホストへのアクセス権限 (`docker exec`) が必要です。

**パスのホワイトリスト** (`LocalImport:AllowedPaths`) によって、インポーターが読み取り可能なディレクトリが制御されます。これらのパスの外にあるファイルは拒否されます。

`--migrate-raw-storage` はこのホワイトリストの対象外です。これは、アーカイブ自体が `RawMessages:FileSystem:BasePath` の下に書き込んだメッセージファイルのみを読み取ります。

## 🛠️ セットアップ {#setup}

### 1. 許可されたパスの設定 {#1-configure-allowed-paths}

`docker-compose.yml`において、許可されたインポートパスを設定します：

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. ファイルのマウント {#2-mount-your-files}

MBox/EMLファイルが含まれているディレクトリをコンテナにマウントします：

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

`/home/user/mbox-archives`をDockerホスト上の実際のパスに置き換えてください。

### 3. コンテナの再起動 {#3-restart-the-container}

```bash
docker compose down && docker compose up -d
```

## 📝 使用方法 {#usage}

### ターゲットとなるアカウント ID の確認 {#find-your-target-account-id}

インポートを行う前に、ターゲットとなるメールアカウントのデータベース ID が必要です。これは Web UI から確認できます。

"Email Accounts" に移動して、移行先のアカウントの詳細を開いてください。アカウント ID はアカウントリスト内、またはアカウント表示中の URL に表示されています。

### MBox ファイルのインポート {#import-an-mbox-file}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**パラメータ:**
| パラメータ | 必須 | 説明 |
|-----------|----------|-------------|
| `--import-mbox` | はい | MBox インポート操作であることを示します |
| `--file <path>` | はい | コンテナ内の MBox ファイルへのパス |
| `--account-id <id>` | はい | ターゲットとなるメールアカウントのデータベース ID |
| `--folder <name>` | いいえ | ターゲットとなるフォルダ名 (デフォルト: `INBOX`) |

### EML ファイルのインポート (ZIP アーカイブ) {#import-eml-files-zip-archive}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**パラメータ:**
| パラメータ | 必須 | 説明 |
|-----------|----------|-------------|
| `--import-eml` | はい | EML インポート操作であることを示します |
| `--file <path>` | はい | コンテナ内の `.eml` ファイルを含む **ZIP アーカイブ** へのパス |
| `--account-id <id>` | はい | ターゲットとなるメールアカウントのデータベース ID |

> **重要:** `--import-eml` は `.eml` ファイルを含む **ZIP アーカイブ** を想定しています。個別の `.eml` ファイルは受け付け**ません**。単一の EML ファイルをインポートする場合は、まずそれらを ZIP アーカイブにまとめてください。
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **`dotnet run` に関する注意:** 公開されている DLL の代わりに `dotnet run` を使用する場合、dotnet の引数とアプリケーションの引数を `--` で区切ってください。
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> `--` は、それ以降のすべての引数が .NET コンパイラではなく、お客様のアプリケーションに属することであることを `dotnet run` に伝えます。

## 📊 出力例 {#example-output}

```
Target account: user@example.com (ID: 1)

=== Local MBox Import ===
File: /data/import/myarchive.mbox
Size: 245.32 MB
Target Account ID: 1
Target Folder: INBOX

[Importing... progress logged to container logs]

=== Import Results ===
Status: Completed
Total Emails: 15234
Imported Successfully: 15120
Failed: 42
Skipped (malformed): 28
Skipped (duplicates): 44
Duration: 00:12:45
```

## 🔍 Monitoring {#monitoring}

インポート中、詳細な進捗状況がコンテナログに書き込まれます：

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ Configuration Reference {#configuration-reference}

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | ローカルインポートに対して最初に許可されるパス |
| `LocalImport__AllowedPaths__1` | (empty) | 2番目に許可されるパス（オプション） |

インデックスを増やすことで、さらに多くのパスを追加できます（`__2`、`__3`など）。

## 🚫 エラーハンドリング {#error-handling}

| エラー | 原因 | 解決策 |
|-------|-------|----------|
| `File not found` | コンテナ内に `--file` パスが存在しません | ファイルパスとボリュームマウントを確認してください |
| `File path is not in an allowed import directory` | ファイルがホワイトリストに登録されたパスの外にあります | ファイルを許可されたパスに移動するか、そのディレクトリを `LocalImport:AllowedPaths` に追加してください |
| `Mail account with ID X not found` | アカウントIDが正しくありません | Web UI またはデータベースクエリを介してアカウントIDを確認してください |
| `Invalid account-id` | `--account-id` に数値以外の値が指定されています | 数値のデータベースIDを使用してください |

## 🔒 セキュリティのベストプラクティス {#security-best-practices}

1. **可能な限り read-only マウントを使用してください**:
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **AllowedPaths** を、実際に使用するディレクトリのみに制限してください。

3. インポート完了後は、クリーンなセキュリティサーフェスを維持するために **volume mounts を削除**してください。

4. インポートディレクトリをウェブサーバー経由で**絶対に公開しないでください** – ローカルインポート用のウェブエンドポイントは存在しません。

## 📦 インポート形式の概要 {#import-formats-at-a-glance}

| CLI Flag | Input Format | Typical File Extension | Single Email | Bulk |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | MBox file | `.mbox`, `.mbx` | ❌ (常に一括) | ✅ |
| `--import-eml` | ZIP archive of EMLs | `.zip` | ❌ (ZIPが必要) | ✅ |
| `--migrate-raw-storage` | Stored original messages | — | — | ✅ |

*単一の `.eml` ファイルは直接サポートされていません。`zip` を使用してラップするか、[web upload](Import-EML-MBox.md) を介してインポートしてください。*

## 🔧 Maintenance Commands {#maintenance-commands}

### Moving Stored Original Messages {#moving-stored-original-messages}

`RawMessages:Enabled` がオンの場合、各メールのオリジナルメッセージは `RawMessages:Storage` に応じて、データベースまたはファイルに保存されます。このコマンドは、すでに保存されているオリジナルをあるバックエンドから別のバックエンドへ移動します。設定については [Original Message Storage](RawMessages.md#storage-backend) を参照してください。

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

| Argument | Default | Description |
|----------|---------|-------------|
| `--from` | configured `RawMessages:Storage` | メッセージの移動元となるバックエンド。 |
| `--to` | configured `RawMessages:Storage` | メッセージの移動先となるバックエンド。 |
| `--batch` | `200` | 1回のデータベーストランザクションあたりのメッセージ数。 |

インポートコマンドとは異なり、このコマンドは `LocalImport:AllowedPaths` による制限を受けません。アーカイブ自体が `RawMessages:FileSystem:BasePath` の下に書き込んだメッセージファイルのみを読み取ります。

**保証されること:**

- 各メッセージは、コピーされる前にアーカイブ時に記録されたチェックサムと照合されます。検証に失敗したメッセージはそのままの場所に残され、報告されます。破損したオリジナルが伝播されることはなく、その唯一のコピーが削除されることもありません。
- ファイルは、それを移動させたデータベーストランザクションがコミットされた後にのみ削除されます。そのため、実行が中断されても唯一のコピーが失われることはありません。
- 実行の再開が可能です。各メッセージに記録されているストレージによって処理対象が選択されるため、コマンドを再実行すると未完了の処理から継続されます。
- 1つのメッセージに問題があっても、実行全体が中断されることはありません。いずれかのメッセージが失敗した場合は終了コード `1`、それ以外は `0` となります。

**出力例:**

```text
Batch: 200 moved, 0 skipped, 0 failed
Batch: 200 moved, 0 skipped, 0 failed
Batch: 42 moved, 0 skipped, 1 failed
Done: 442 moved, 1 failed.
  Email 12345: checksum mismatch (recorded 4f1c..., file has 9ab2...)
```

読み取りは常に各メッセージに記録されているバックエンドに従い、設定値に従うわけではないため、移行中および移行後もダウンロードは機能し続けます。`RawMessages:Storage` の変更は新しくアーカイブされるメールにのみ影響します。すでに保存されているものを移動するには、このコマンドを実行してください。

> **バックアップ:** ファイルシステムバックエンドを使用する場合、メッセージファイルはデータベースの外に存在します。データベースのダンプとともに `RawMessages:FileSystem:BasePath` もバックアップしてください。ファイルなしでリストアを行うと、提供できないオリジナルが残ることになります。

## ❓ FAQ {#faq}

**Q: なぜ `--import-eml` は ZIP ファイルを必要とするのですか？**
A: 基盤となる EML インポートサービス (`EmlImportService`) は、.NET の `ZipFile` API を使用して ZIP アーカイブを内部的に処理します。この設計により、一括インポートを効率的にサポートしています。個別の `.eml` ファイルについては、ZIP アーカイブ (例: `zip emails.zip *.eml`) にまとめるか、[web upload](Import-EML-MBox.md) を使用してください。

**Q: Web UI からファイルをブラウズできないのはなぜですか？**
A: これは意図的なセキュリティ設計です。通常のユーザーは OS レベルに直接アクセスできず、Web UI とのみやり取りするため、システムレベルの操作を行うこの機能は意図的に CLI を介して実装されています。

パス・トラバーサル攻撃や不正なファイルシステムへの露出などのセキュリティリスクを防ぐため、一般ユーザーはファイルのアップロードとインポートに Web UI を使用することに制限されています。これにより、意図されたインターフェースをバイパスして、基盤となるコンテナのファイルシステムにアクセスできないようになっています。
