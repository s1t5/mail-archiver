# 💾 Per-Account Storage Display {#per-account-storage-display}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

Dashboardの「Account Overview」テーブルとMailAccountsの「Show All」テーブルには、**Storage**列が含まれています。この列には、各アカウントが使用しているデータベース容量が表示されます。これには、**すべてのアーカイブ済みメールの全フィールド**および**すべての添付ファイル**が含まれます。

## ⚙️ How it works {#how-it-works}

ストレージの値は、`AccountStorageCache`データベーステーブルに**キャッシュ**されます。これにより、Dashboardやアカウントリストを開くたびに高コストなクエリを実行する代わりに、ウェブページは事前に計算された値を読み取ります。キャッシュは、独立したバックグラウンドサービス（`AccountStorageRefreshService`）によって最新の状態に保たれます。

各アカウントについて、サービスはPostgreSQLに対し、`ArchivedEmails`テーブルにおけるそのアカウントの各行のすべての列について`pg_column_size`を合算するよう要求します。これには、PostgreSQLの圧縮およびout-of-lineストレージ（TOAST）を含め、**メールのすべてのフィールド**（Subject、From、To、Cc、Bcc、Body、HtmlBody、RawHeaders、ContentHashなど）が含まれます。out-of-lineの値のサイズは、そのTOASTポインタから読み取られるため、計算ではテーブル自体のみをスキャンし、メール本文をロードすることはありません。添付ファイルの分は、`EmailAttachment.Size`の論理的な合計として別途加算されます。

リフレッシュサービスは、以下の3つの状況でキャッシュを更新します：

1. **Startup backfill** — 新規インストール後、またはこの機能が導入されたバージョンへのアップデート後、すべてのアカウントはキャッシュ値がない状態で始まります。サービスはすべてのアカウントを一つずつ処理し、最初の値を計算します。
2. **Daily refresh** — 設定された時刻（デフォルトは`02:30`）に1日1回、セーフティネットとしてサービスがすべてのアカウントを再計算します。
3. **Immediate refresh** — メールの同期、EMLインポート、MBOXインポート、または保持ポリシーによる削除が行われた後、影響を受けるアカウントがすぐに再計算され、表示される値が最新の状態に保たれます。

## 🤔 Why "0 B" is shown at first {#why-0-b-is-shown-at-first}

この機能が最初に有効になったとき、または新しいアカウントが作成されたとき、そのアカウントのキャッシュ行はまだ存在しません。リフレッシュサービスはバックグラウンドで実行され、非常に大きなアーカイブがデータベースに過負荷をかけないよう、各アカウントの間に短い遅延を設けながら**一つずつ**アカウントを計算します。
サービスがアカウントの処理を完了するまで、UIはプレースホルダーとして**「0 B」**を表示します。これは完全に正常な動作です。バックグラウンドサービスがそのアカウントの処理を終えると、次にページを読み込んだとき、またはリフレッシュしたときに実際の値が表示されます。
既存の大きなアーカイブの場合、バックフィルには数分以上かかることがあります。サービスは専用のテーブル（`AccountStorageBackfillState`）で進捗を追跡しているため、バックフィルの途中でアプリケーションが再起動された場合でも、中断した箇所から単に続行されます。アカウントが二重に計算されることはありません。

## 🔧 Configuration {#configuration}

この機能は、`appsettings.json`または環境変数を通じて調整できます。利用可能なすべてのオプションについては、[Setup.md](Setup.md)の**Account Storage Settings**セクションを参照してください。オプションには以下が含まれます：

- `AccountStorage__Enabled`: リフレッシュサービスの有効/無効の切り替え（デフォルトは`true`）。
- `AccountStorage__DailyExecutionTime`: 毎日のフルリフレッシュの時刻（デフォルトは`02:30`）。
- `AccountStorage__BackfillDelayMs`: スタートアップバックフィル中のアカウント間の休止時間（デフォルトは`5000` ms）。
- `AccountStorage__RefreshBatchDelayMs`: 毎日のリフレッシュ中のアカウント間の休止時間（デフォルトは`1000` ms）。
- `AccountStorage__CommandTimeoutSeconds`: アカウントごとのストレージ計算のデータベースコマンドタイムアウト（デフォルトは`300`秒）。`pg_column_size`スキャンに時間がかかる非常に大きなアーカイブの場合は、この値を増やしてください。

> 💡 **Tip**: この機能が動作するために `DatabaseMaintenance__Enabled` を有効にする必要は**ありません**。ストレージリフレッシュサービスは独立して動作します。
