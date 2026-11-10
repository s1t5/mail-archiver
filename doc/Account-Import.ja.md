# Account Import {#account-import}

[← Documentation Index に戻る](index.md)

## Overview {#overview}

Mail Archiver は、一度に複数のメールアカウントをインポートできるため、大量のメールボックスのオンボーディングにおいて、各アカウントを個別に作成する必要がありません。

メールプロバイダーに応じて、2つの一括インポートパスが存在します。

| プロバイダー | 一括インポート方法 | ドキュメント |
|---|---|---|
| **Microsoft 365 (tenant)** | Microsoft Graph によるテナント検出 — 1つのアプリ登録でテナント内のすべてのメールボックスにアクセス | [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) |
| **IMAP** | CSV 一括インポート — 1行につき1つのメールボックスを含む CSV ファイルをアップロード | このページ（下記参照） |

> **注意:** Microsoft 個人用アカウント (Outlook.com / M365 Family, プロバイダー **MSA**) は、インタラクティブなデバイスコード認証フローを使用するため、一括インポートはできません。これらは1つずつ作成および認証する必要があります。[MSA Outlook Setup](MSA_Outlook_Setup.md) を参照してください。

---

## IMAP CSV Bulk Import {#imap-csv-bulk-import}

### 機能 {#what-it-does}

CSV 一括インポートを使用すると、管理者は CSV ファイルをアップロードすることで、一度の操作で数百の IMAP メールアカウントを作成できます。ファイル内の各行は1つのメールボックスに対応します。共通のサーバー設定 (IMAP host, port, SSL) はアップロードフォームで一度だけ指定すればすべての行に適用されますが、行ごとに個別の設定で上書きすることも可能です。

インポートは **管理者のみ** が実行できます。**Mail Accounts** ページから **Import** ドロップダウン → **CSV Bulk Import** を選択してアクセスできます。

### CSV ファイル形式 {#csv-file-format}

ファイルは、**ヘッダー行** を持ち、**カンマ (`,`) を区切り文字** とする UTF-8 エンコードの CSV である必要があります。カンマまたは引用符を含むフィールドは、二重引用符 (`"..."`) で囲む必要があります。

#### カラム {#columns}

| カラム | 必須 | 説明 |
|---|---|---|
| `email` | はい | メールボックスのアドレス。`username` が提供されない場合、デフォルトのログインユーザー名としても使用されます。 |
| `password` | はい | IMAP ログインパスワード。入力された通りに保存されます — 前後のスペースは保持されます。 |
| `name` | いいえ | アカウントの表示名。省略した場合、デフォルトは `{prefix} - <{email}>` になります。 |
| `username` | いいえ | メールアドレスと異なる場合のログインユーザー名。 |
| `imap_server` | いいえ | 行ごとの IMAP host。アップロードフォームの共通サーバー設定を上書きします。 |
| `imap_port` | いいえ | 行ごとの IMAP port (1–65535)。共通のポートを上書きします。 |
| `use_ssl` | いいえ | 行ごとの SSL トグル (`true` または `false`)。共通の設定を上書きします。 |

#### 例 {#example}

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- 1行目はアップロードフォームの共通サーバー/ポート/SSL を使用します。
- 2行目はカスタムユーザー名を設定していますが、それ以外は共通設定を使用します。
- 3行目はサーバー、ポート、および SSL を上書きします。

ダウンロード可能なサンプルファイルは、インポートページの **Download example CSV** ボタンから入手できます。

### アップロードフォーム {#upload-form}

| フィールド | 目的 |
|---|---|
| **CSV file** | アップロードするファイル。 |
| **IMAP server** | `imap_server` による上書きがないすべての行に適用される共通のホスト。 |
| **IMAP port** | 共通のポート (デフォルト 993)。 |
| **Use SSL** | 共通の SSL トグル (デフォルトはオン)。 |
| **Name prefix** | 自動生成されるアカウント名の接頭辞。デフォルト: `IMAP`。 |
| **Skip existing mailboxes** | 有効にすると、メールアドレスが既に IMAP アカウントとして存在している行はスキップされます（エラーではなく "skipped" としてカウントされます）。無効にすると、それらは失敗として処理されます。 |
| **Account enabled** | 作成されたアカウントをバックグラウンド同期用に有効にするかどうか。 |
| **Delete After Days / Local Retention Days** | 作成されたすべてのアカウントに適用されるオプションの保持設定。[Retention Policies](RetentionPolicies.md) を参照してください。 |

### 処理プロセス {#processing}

1. CSV ファイルが解析され、各行が検証されます (メール形式、パスワードの有無、ポートの範囲、SSL の値)。
2. ファイル内の重複するメールアドレスは削除されます (最初に出現したものが優先されます)。
3. 同じメールアドレスを持つ既存の IMAP アカウントが検出されます。**Skip existing** トグルの設定に応じて、スキップされるか、失敗として報告されます。
4. すべての新しいアカウントは、単一のデータベースバッチとして挿入されます。
5. 実行結果の要約がアクセスログに書き込まれます。

インポート中に IMAP 接続テストは行われません — アカウントは即座に保存されます。認証の問題は、最初のバックグラウンド同期時にのみ表面化します。これにより、数百のメールボックスであってもインポートを高速に保つことができます。

### 結果ページ {#result-page}

インポートが完了すると、結果ページに 3 つのテーブルが表示されます。

- **Created** — 作成されたすべてのアカウントのメールアドレスと名前。
- **Skipped** — **Skip existing** が有効な場合に、重複していた（ファイル内または既に存在していた）メールアドレス。
- **Failed** — 処理できなかった行。行番号と理由（メールアドレス不足、パスワード不足、無効なポート、IMAP サーバーなし、など）が表示されます。

サマリーバナーに件数が表示されます: *{created} created, {skipped} skipped, {failed} failed*。

### 制限事項 {#limits}

以下の制限は `appsettings.json` / 環境変数を通じて設定可能です ([Setup Guide](Setup.md) → *CSV Import Settings* を参照)。

| 設定 | デフォルト | 説明 |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | 1 回のアップロードで処理される最大行数。 |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | アップロード可能な最大ファイルサイズ。 |

### セキュリティに関する注意 {#security-note}

IMAP パスワードは、個別に作成された IMAP アカウントと同様の方法でデータベースに保存されます。CSV ファイルにはすべてのメールボックスのパスワードがプレーンテキストで含まれていることに注意してください — インポート成功後は、クライアントマシンからファイルを削除してください。

---

## Microsoft 365 Tenant Import {#microsoft-365-tenant-import}

Microsoft 365 (Exchange Online) の場合、一括インポートに CSV ファイルは必要ありません。適切な Graph 権限を持つ単一の Azure AD アプリ登録があれば、テナント内のすべてのメールボックスにアクセスできるため、Mail Archiver はメールボックスを自動的に検出できます。

専用のガイドを参照してください: **[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**。
