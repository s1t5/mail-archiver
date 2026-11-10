# 🔍 Mail Search {#mail-search}

[← Documentation Index に戻る](index.md)

## 📋 概要 {#overview}

検索はメールアーカイブの核心です。Mail Archiver は、アクセス可能なすべての
アカウントにわたって、アーカイブされたメールの**全文**（件名、本文、送信者、
受信者）を検索し、数百万件のメッセージを含むアーカイブであっても数ミリ秒で
結果を返します。

このガイドでは、検索インターフェース、クエリ構文、フィルター、並べ替え、
ページング、および結果セットに対してアクションを実行する必要がある場合の
対処法について説明します。

> **ℹ️ 検索で見えるのは、あなたが閲覧可能なものだけです。** 結果はユーザーの
> アカウント権限によってフィルタリングされます。管理者でないユーザーは、
> 自身に割り当てられたメールボックスからのメールしか見つけることができません。
> この制限はインターフェースではなく、クエリ自体に適用されます。

## 🚀 検索へのアクセス {#accessing-search}

1. ログインし、メインメニューで **Archive** を選択します
2. 検索バーは結果リストの上に配置されています

検索語を入力せずに Archive を開くと、最新のメールが新しい順にリスト表示
されます。そこからブラウズするか、フィルターを使用して絞り込むことができます。

## 🎯 検索インターフェース {#the-search-interface}

| フィールド | 機能 |
|---|---|
| **Search Term** | 全文クエリ — 下記の [Query Syntax](#-query-syntax) を参照 |
| **From** | 指定した日付以降に送信されたメールのみ |
| **To** | 指定した日付以前に送信されたメールのみ |
| **Direction** | `All`、`Incoming` または `Outgoing` |
| **Account** | 特定のメールアカウントに制限 |
| **Folders** | フォルダーツリー経由で特定のフォルダーに制限 |
| **Search** | クエリを実行 |
| **Reset** | すべてのフィルターをクリア |

検索語は、**件名、本文、送信者、受信者**
(`From`, `To`, `Cc`, `Bcc`) に一致します。

### フォルダーツリー {#the-folder-tree}

フォルダーは左側に**階層ツリー**として表示され、ネストされたフォルダーを含む
ソースメールボックスのフォルダー構造を反映しています。フォルダーをクリック
すると検索をそのフォルダーに限定でき、画面スペースが必要なときは展開/折りたたみ
コントロールを使用してツリーを折りたたむことができます。

フォルダーを選択しても検索語は変更されません。両方が組み合わされて適用されます。

## 🧩 Query Syntax {#query-syntax}

構文は意図的にシンプルに設計されています。以下に挙げるものが、アプリケーションが
実際に理解できる内容です。

### 単語 — 接頭辞が機能する理由 {#plain-words-and-why-prefixes-work}

1つ以上の単語を入力します。それらすべてが含まれている必要があります（**AND** 検索）:

```
invoice january
```

検索フィールドに `invoice` と `january` の両方が含まれるメールを見つけます。

> **💡 部分一致は自動的に行われます。** 各単語は**接頭辞**として扱われるため、
> `isenb` での検索は `isenboeck` や `isenböck` も見つけ、`rech` は `Rechnung` だけでなく
> `Rechnungen` も見つけます。ワイルドカード文字を入力する必要はありません。存在
> しないからです。これは、非常に短い検索語が大量の結果を返す可能性があることも
> 意味します。[Performance](#-performance-and-large-archives) を参照してください。

検索は**大文字と小文字を区別しません**。`Invoice`、`invoice`、`INVOICE` はすべて
同じクエリです。

### 完全一致フレーズ — 引用符 {#exact-phrases-quotes}

テキストを二重引用符で囲むと、単語がその順序で**隣接している**ことを
要求します:

```
"important meeting"
```

引用符がない場合、`important meeting` は2つの単語が無関係な文に現れるメールにも一致して
しまいます。引用符を使用すると、隣接するフレーズのみが一致します。

### フィールド指定検索 {#field-specific-search}

用語の前にフィールド名とコロンを付けることで、**そのフィールドのみ**を
検索できます:

| 構文 | 検索対象 |
|---|---|
| `subject:invoice` | 件名のみ |
| `from:john` | 送信者のみ |
| `to:example.com` | 受信者のみ |
| `body:important` | 本文のみ |

フィールド名はケースインセンシティブです（`Subject:` も動作します）。また、各
フィールドは引用符と組み合わせてフレーズとして使用できます:

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ 4つのフィールドがサポートされています:** `subject`、`body`、`from`、`to`。
> `cc:someone` や `bcc:someone` と書いた場合はフィールド検索では**ありません**。その用語は
> 通常のテキストとして扱われ、すべてのフィールドにわたって検索されます。
> Cc にのみ含まれていた受信者を見つけるには、接頭辞なしでアドレスまたは
> 名前を検索してください。

### すべての組み合わせ {#combining-everything}

フィルターと検索語は累積されます。このクエリは、送信者が保険関連のいずれかであり、
本文に "car insurance" という完全一致フレーズが含まれ、件名に "invoice" が
含まれるメールを見つけます:

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

実用的な組み合わせ:

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### 無視される文字 {#characters-that-are-ignored}

クエリパーサーは個々の用語から `& | ! ( ) : *` を取り除きます。これは、基礎となる
全文検索エンジンにおいてそれらが意味を持つためです。検索結果が予想外の
ものではないか確認する場合は、特殊文字が原因でないかチェックしてください。

## 📊 並べ替えとページング {#sorting-and-paging}

### 並べ替え {#sorting}

列ヘッダーをクリックして並べ替えます。並べ替え可能な列:

| 列 | 並べ替え基準 |
|---|---|
| **Subject** | 件名、アルファベット順 |
| **From** | 送信者 |
| **To** | 受信者 |
| **Date** | 送信日 |

同じヘッダーを再度クリックすると、方向が反転します。デフォルトは**新しい順**です。

### ページサイズ {#page-size}

1ページあたりに表示する結果の数を選択します:

**20** (デフォルト) · **50** · **75** · **100** · **150**

ページサイズが大きいほど多くのコンテキストが表示されますが、レンダリングに
時間がかかります。**Previous** / **Next** コントロールを使用して結果ページを
移動します。結果の総数によって、一致したメールの合計数がわかります。

> **ℹ️ 表示される日付には、あなたのタイムゾーンが適用されます。** 表示される
> 時間は UTC から設定されたタイムゾーンに変換されるため、表示される日付は
> ローカルのカレンダーと一致します。これは、深夜付近を検索する場合に便利です。

## 👁️ 結果の操作 {#working-with-results}

各行には、アカウント、送信者、件名、日付、およびメールに添付ファイルがある場合は
**📎 paperclip icon** が表示されます。クリップはインジケーターであり、フィルター
ではありません。

**Details** をクリックしてメールを開きます:

- 本文全文 (HTML またはプレーンテキスト)
- **Attachments** — 各ファイルをダウンロードするオプション付きのリスト
- **Download as EML** — 他で使用するための完全なメッセージ
- **Restore** — メールをライブメールボックスにコピーして戻す

メールをメールボックスに復元する方法については [Mailbox Migration](MailboxMigration.md) を
参照してください。

## ✅ 複数の結果への一括操作 {#acting-on-many-results-at-once}

結果を選択して一括処理できます。**Selection Mode** をオンにしてチェックボックスを
表示させ、次のように操作します:

- **Select All** / **Deselect All** — 現在のページに対して
- **Select all {n} matching** — 現在の検索に一致するすべての結果（全ページに
  わたるもの。結果が複数ページに及ぶ場合に表示されます）
- **Copy Selected** — メールボックスに復元
- **Export Selected** — MBOX または EML として書き出し
- **Delete Selected** — アーカイブから削除

作業中、カウンターには *"{selected} of {total} selected"* と表示されます。

### 選択制限 {#the-selection-limit}

> **⚠️ 一度に選択できるメールは最大 250 件までです** (`Selection:MaxSelectableEmails` で設定可能)。
> それを超えると、インターフェースに *"Maximum selections reached (250)"* および
> **Too Many — Split Required** と表示されます。

この制限は、各操作が対象となるメッセージを保持する必要があるために存在します。
*"Split larger operations into multiple batches"* というプロンプトは、意図された
回避策です。結果を 250 件以下のグループに分けて処理してください。

**Select all {n} matching** も同じ制限に従います。検索結果が制限を超える
メール数に一致する場合、現在のソート順の最初のメールが選択され、
*"Maximum selections reached"* という通知が表示されます。残りのメールを対象にする
には、検索条件（日付範囲、フォルダー、アカウント）を絞り込んでください。

制限を超えようとすると、次のように表示されます: *"Too many emails selected. Maximum
allowed is 250 emails. Please reduce your selection or split into multiple
operations."*

### フィルターの全一致結果の削除 {#deleting-every-match-of-a-filter}

*Reset* の隣にある **Delete all matches** ボタンは、現在のページや選択範囲
だけでなく、現在の検索に一致するすべてのメールを削除します。これは管理者または
セルフマネージャーが利用でき、バックグラウンドジョブとして実行されるため、
250 件の選択制限は適用されません。

確認ダイアログには、削除可能なメール数と、保持ポリシーによってロックされている
メール数が表示されます。確認を有効にするには、その数値を入力する必要があります。
サーバーは再度カウントを行い、その間に同期によってメールが追加されるなど、
結果セットが変更された場合は処理を中止します。

ロックされたメールはこの操作によって削除されることはありません。ジョブステータスで
スキップされたものとして報告されます。`DeletionPolicy:MaxBulkDeleteEmails` (デフォルト 100,000) は 1 つの
ジョブの上限です。一致するメールがこれより多いフィルターは、絞り込みを
求めるリクエストとともに拒否されます。

ジョブは [Background Jobs](BackgroundJobs.md) ページに表示され、そこでキャンセルできます。

### 直接処理 vs バックグラウンドジョブ {#direct-processing-vs-background-job}

**Copy** と **Export** では、処理の実行方法を選択できます:

| オプション | 動作 |
|---|---|
| **Process on this page (wait for completion)** | ブラウザが待機します。少数のメールに適しています |
| **Process in background (monitor separately)** | ジョブとして実行されます。ブラウザを閉じても構いません |

エクスポートは常にバックグラウンドで実行されます:

> *"The export will run in the background and you will be able to download the
> result from the Jobs page once completed."*

実行中および完了したジョブは [Background Jobs](BackgroundJobs.md) ページで確認できます。

> **⚠️ 削除は取り消せません。** 確認メッセージは明示的です: *"Are you sure you
> want to permanently delete {n} selected email(s)? This action cannot be undone
> and each deletion will be logged for audit purposes."* 削除はアクセスログに
> 記録されます。

## ⚡ パフォーマンスと大規模アーカイブ {#performance-and-large-archives}

検索は PostgreSQL の全文インデックスによって支えられているため、検索が
速いか遅いかの違いは、通常クエリがいかに**選択的（selective）**であるかに
依存します。

**推奨事項:**

- メールが届いた時期がおおよそわかっている場合は、**日付範囲 (date range)** を
  追加してください。これが最も効果的な絞り込み方法です
- メールの保存場所がわかっている場合は、**アカウント (account)** または
  **フォルダー (folder)** に制限してください
- 非常に短い単語 1 つではなく、**複数の単語**を使用してください
- フィールド検索 (`subject:`, `from:`) を使用して、無関係な一致を排除してください

**避けるべきこと:**

- `re` のような単一の短い接頭辞 — 接頭辞検索として使用すると、アーカイブの
  大部分に一致してしまいます
- 一般的な単語と非常に広い日付範囲の組み合わせ
- 検索を絞り込む代わりに、多くのページをブラウズすること

> **💡 検索が遅いと感じる場合、原因の多くは日付範囲にあります。** 1 ヶ月間に
> 制限するだけで、インデックスがアーカイブの大部分をスキップできるようになるため、
> 数秒かかっていた検索が瞬時に終わることがよくあります。

## ❓ トラブルシューティング {#troubleshooting}

| 症状 | 原因 | 対処法 |
|---|---|---|
| 結果が出ないが、あるはずである | 検索語が具体的すぎる、または入力ミス | 単語を減らすか、綴りを確認する |
| `cc:name` で有用なものが見つからない | `cc` はサポートされていないフィールドである | 接頭辞なしで名前を検索する |
| 特殊文字によってクエリが壊れる | `& \| ! ( ) : *` が取り除かれる | 文字を削除するか、別の文字に置き換える |
| 結果が多すぎる | 一般的な単語または短い接頭辞 | 日付範囲またはフィールド検索を追加する |
| *"Too Many — Split Required"* | 250 件以上のメールが選択されている | 選択数を減らすか、操作を分割する |
| エクスポートが見当たらない | バックグラウンドジョブとして実行されている | [Background Jobs](BackgroundJobs.md) を確認する |
| 結果が欠落しているように見える | アカウント権限 | メールボックスが自身のユーザーに割り当てられているか確認する |
| 日付がずれて表示される | タイムゾーン変換 | タイムゾーン設定を確認する |

## 📚 関連ドキュメント {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — 結果をメールボックスに復元する
- [Background Jobs](BackgroundJobs.md) — エクスポートと復元の監視
- [Access Logging](Logs.md) — 検索と削除の監査証跡
- [REST API](API.md) — API 経由でのアーカイブ操作
- [MCP Server](MCP.md) — AI エージェントからの検索
- [Retention Policies](RetentionPolicies.md) — メールがアーカイブから削除される条件
