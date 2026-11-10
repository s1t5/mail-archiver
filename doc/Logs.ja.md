# 📋 Access Log {#access-log}

[← Documentation Indexに戻る](index.md)

## 📋 概要 {#overview}

このガイドでは、Mail Archiverアプリケーションにおけるアクセスログ機能について説明します。この機能により、システム内でのユーザーアクティビティの記録と表示が可能になります。この機能は、モニタリング、セキュリティ、およびデバッグの目的において極めて重要です。

## 📊 ログに記録される内容 {#what-is-logged}

アプリケーションは、以下を含むさまざまな種類のユーザーアクティビティをログに記録します：

1. **Login and Logout** - ログインおよびログアウト操作の記録
2. **Search Operations** - システム内でのメール検索リクエストの記録
3. **Email Opening** - ユーザーがメールの内容を開いた際の記録
4. **Email Downloading** - ユーザーがメールをダウンロードした際の記録
5. **Email Restoration** - ユーザーがアーカイブされたメールを復元した際の記録
6. **Account Management** - アカウント関連のアクションの記録
7. **Deletion Operations** - 削除アクションの記録
8. **Audit Data Export** - 監査データエクスポートの実行記録（開始と結果。詳細は [Audit Data Export](AuditExport.md) を参照）

## 📝 アクションタイプと説明 {#action-types-and-descriptions}

記録されるアクションは、以下のタイプに分類されます：

### Login {#login}
- **説明**: ユーザーがシステムにログインした際に記録されます
- **詳細**: Username, timestamp

### Logout {#logout}
- **説明**: ユーザーがシステムからログアウトした際に記録されます
- **詳細**: Username, timestamp

### Search {#search}
- **説明**: メールシステム内での検索リクエストを記録します
- **詳細**: Username, timestamp, search parameters

### Open {#open}
- **説明**: ユーザーがメールを開いた際に記録されます
- **詳細**: Username, timestamp, email ID, sender, subject

### Download {#download}
- **説明**: ユーザーがメールをダウンロードした際に記録されます
- **詳細**: Username, timestamp, email ID, sender, subject

### Restore {#restore}
- **説明**: ユーザーがアーカイブされたメールを復元した際に記録されます
- **詳細**: Username, timestamp, email ID, sender, subject

### Account {#account}
- **説明**: アカウント関連のアクション（作成、変更など）を記録します
- **詳細**: Username, timestamp, account ID

### Deletion {#deletion}
- **説明**: メールまたはその他のリソースに対する削除操作を記録します
- **詳細**: Username, timestamp, affected resource
- **コンプライアンスに関する注記**: 削除操作は、コンプライアンス監査の目的でログに記録されます

## 🔍 フィルターオプション {#filter-options}

ログページでは、以下のフィルターオプションが利用可能です：

1. **Date** - 日付範囲（From-To）によるフィルター
2. **Username** - 特定のユーザーによるフィルター（Adminのみ）
3. **Action Type** - 特定のアクションタイプによるフィルター

## 🖥️ ログの表示 {#log-display}

ログは、以下のカラムを持つテーブル形式で表示されます：

- **Timestamp**: アクションの日時
- **Username**: 関連するユーザーの名前
- **Type**: カラーコード化されたアクションのタイプ
- **Information**: アクションに関する追加の詳細

モバイルデバイスでは、ログはカード形式で表示されます。

## 🔐 アクセス権限 {#access-permissions}

- **Non-admin users**: 自身のログのみ表示可能
- **Admin users**: すべてのユーザーのログを表示し、ユーザーごとにフィルター可能

## 📤 Audit Data Export {#audit-data-export}

管理者は、ログページから直接専用の [Audit Data Export](AuditExport.md) ページを開くことができます。これにより、外部の監査ツール向けに、アーカイブから表形式の大量データパッケージ（INDEX.XML + CSV）が作成されます。エクスポートの実行ごとに、**Audit Data Export** タイプのアクティビティが2つ（開始と結果）書き込まれ、これらは他のすべてのエントリと同様にログページにリストされ、フィルター可能です。

詳細は [Audit Data Export guide](AuditExport.md) を参照してください。
