# ⚙️ バックグラウンドジョブ {#background-jobs}

[← ドキュメントインデックスに戻る](index.md)

## 📋 概要 {#overview}

Mail Archiverにおけるほとんどの作業は、待機中に行われるわけではありません。メールボックスの同期、MBOXファイルのインポート、数千通のメールの復元、またはアーカイブのエクスポートには、数分から数時間かかる場合があります。これらのタスクは**バックグラウンドジョブ**として実行されます。一度開始すると、ブラウザを閉じても実行が継続されます。

**Background Jobs** ページでは、現在実行中のジョブ、完了したジョブ、および失敗したジョブを確認できます。処理が予想よりも長い場合や、同期がエラーで終了した場合に確認すべき場所です。

> **ℹ️ 管理者用ページ。** Background Jobsには管理者権限が必要であり、メインメニューからアクセスできます。一般ユーザーには表示されません。一般ユーザーもジョブ（例：一括復元など）を開始することはできますが、その監視はここで行われます。

## 🗂️ ジョブの種類 {#job-types}

このページでは、ジョブをタイプ別にグループ化しています。各セクションには、**直近の20件**までのジョブが表示され、実行中およびキュー待ちのジョブが上部にソートされます。これにより、古いジョブが新しいジョブによって見えなくなるのを防ぎます。

| セクション | 対象となる内容 | 開始場所 |
|---|---|---|
| **Synchronization Jobs** | メールサーバーからの新しいメールの取得 | 自動スケジュール、またはアカウントの *Sync now* |
| **MBox Import Jobs** | `.mbox` ファイルのインポート | Mail Accounts → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **EML Import Jobs** | `.eml` ファイル / ZIPアーカイブのインポート | Mail Accounts → [Import EML](Import-EML-MBox.md#-import-eml) |
| **Batch Restore Jobs** | 選択したメールをメールボックスに書き戻す | Archive → メールを選択 → Batch Restore |
| **Account Export Jobs** | アカウント全体の書き出し (mbox / EML) | Mail Accounts → [Export](Export-Account.md) |
| **Selected Emails Export Jobs** | 選択したメールセットの書き出し | Archive → メールを選択 → Export |
| **Email Deletion Jobs** | サーバーまたはアーカイブからのメール削除（フィルターによる一括削除を含む） | 保存ポリシー、手動削除、または Archive → *Delete all matches* |
| **Account Deletion Jobs** | メールアカウント全体の削除 | Mail Accounts → Delete |
| **Offload** | 指定した期間のメールを別のカウントへ移動 | Mail Accounts → Offload |

## 📊 ジョブ行の読み方 {#reading-a-job-row}

すべての行には、以下の共通カラムが含まれています。

| カラム | 意味 |
|---|---|
| **Job ID** | 短い識別子 — 問題を報告する際にこれを引用してください |
| **Account** | ジョブが対象としているメールボックス |
| **Status** | 現在の状態（下の表を参照） |
| **Progress** | 進捗状況（判明している場合は 処理済み/合計数 を表示） |
| **Created** | ジョブがキューに追加された日時 |
| **Started** | 実際に実行が開始された日時 |
| **Completed** | 終了した日時（実行中は空欄） |
| **Duration** | 経過時間 |

一部のジョブタイプでは、Progress カラムにカウンターが追加されます。

- **Success** / **Failed** — 正常に処理されたメール数 / 失敗したメール数
- **Skipped (duplicates)** — すでにアーカイブに存在するメール（二重インポートを防止）
- **Recovered** — サーバーから消失が報告された後に再取得されたメッセージ数
- **Malformed (skipped)** — 解析できず除外されたメッセージ数

### ステータス値 {#status-values}

| Status | 意味 |
|---|---|
| **Queued** | 開始待ち — 他のジョブが実行中、またはワーカーがビジー状態 |
| **Running** | 現在実行中 |
| **Completed** | 正常に完了 |
| **CompletedWithErrors** | 完了したが、一部のアイテムが失敗した (MBox インポート) |
| **Failed** | エラーで終了 |
| **Cancelled** | ユーザーによって停止された |
| **Rate Limited** | *帯域幅制限のため同期を一時停止しました。自動的に再開されます。* |
| **Timed Out** | 同期が設定された制限時間を超えたため停止しました |

> **ℹ️ "Rate Limited" と "Timed Out" は同期専用のステータスです。** これらは失敗のように見えますが、実際には失敗ではありません。Rate-limited な同期は、帯域幅のクォータが許可され次第、自動的に再開されます。Timed-out な同期は、次回の実行時にチェックポイントから継続されます。詳細は [Rate Limit Handling](RateLimitHandling.md) および [Mail Synchronization](Synchronization.md) を参照してください。

### 進捗が 0% のまま止まって見える理由 {#why-progress-can-sit-at-0}

進捗は個々のメールではなく、**完了したフォルダ**によって測定されます。

> *進捗は完了したフォルダによって測定されます。大きなフォルダ（例：INBOX）がある場合、その処理が終わるまでバーは 0% のままになります。*

INBOXにほとんどのメールが含まれているメールボックスでは、バーが長い間 0% のままになり、その後一気に進むことがあります。これは想定された動作です。ジョブは動作していますが、最初の大きなフォルダの処理がまだ完了していないだけです。

## 🔄 ページの自動更新 {#the-page-refreshes-itself}

ページを開いている間、**30秒ごと**にリロードされます。そのため、タブを開いたままにしておけば、何もクリックせずに進捗を監視できます。ラベルには、データが最後に更新された時間が表示されます。

リンクまたはボタンをクリックした瞬間にリロードはキャンセルされます。そうしないと、クリックした対象が、リロードによって置き換わっているページ上の要素になってしまう可能性があるためです。

**設定可能:** `appsettings.json` 内の `Jobs:RefreshSeconds` (デフォルト `30`)。
`0` に設定すると、自動リロードをオフにできます。リロードのたびにすべてのジョブソースからページを再構築するため、多数のアカウントがある環境でインターバルを極端に短くすると、サーバーへの負荷が高まり、開いているタブの数だけその負荷が増大します。

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 ジョブのキャンセル {#cancelling-a-job}

キャンセルをサポートしているジョブには **Cancel Job** ボタンが表示され、以下の確認が表示されます。

> *Are you sure you want to cancel this job?* (本当にこのジョブをキャンセルしますか？)

インポートジョブの場合は、**Cancel Import** / *Are you sure you want to cancel this import job?* という文言になります。

キャンセルが可能なのは、**synchronization, batch restore, selected-emails export, および email deletion** のジョブです。ジョブは現在のアイテムの処理が完了した直後に停止し、ステータスは **Cancelled** になります。

> **⚠️ キャンセルはロールバックではありません。** すでにインポート、復元、エクスポート、または削除されたメールは、そのままの状態に維持されます。キャンセルは、ジョブが「それ以上」の操作を行うのを止めるものであり、それまでに行った操作を取り消すものではありません。削除ジョブの場合、すでに削除されたメールは消えたままとなります。

アカウントおよび MBox インポートジョブは、このページからキャンセルすることはできません。代わりに、各アカウントのインポートページから停止させてください。

## ⚠️ 同期の失敗と「Acknowledge Failures」 {#sync-failures-and-acknowledge-failures}

同期が **Failed** ステータスで終了した場合、ジョブ行に **Acknowledge Failures** が表示されます。これは、ページ上で将来の動作を変更する唯一のアクションであるため、理解しておく価値があります。

各同期の実行は、**チェックポイント**を使用して、前回の続きから開始されます。もし一部のメールで同期が失敗した場合、チェックポイントはそのメールの手前に留まるため、次回の実行時にそれらのメールが再度試行されます。これは、何も黙ってスキップされないための安全なデフォルト設定です。

しかし、失敗が永続的なものである場合（破損したメッセージ、サーバーが提供を拒否するフォルダなど）、その後の同期は毎回それらを再試行しようとし、同期は同じ場所で失敗し続けます。

**Acknowledge Failures** は、チェックポイントを失敗したメールの先に進めることで、このデッドロックを解消します。確認画面では、その結果について明示的に警告されます。

> *This will advance the sync timestamp to the completion time of this job, so the next sync will be a normal incremental sync. The failed emails will not be retried. Continue?* (これにより、同期タイムスタンプがこのジョブの完了時刻に進められます。そのため、次回の同期は通常の増分同期となります。失敗したメールは再試行されません。続行しますか？)

成功すると、*"Sync timestamp has been advanced. The next sync will be incremental."* と表示されます。

> **⚠️ 失敗の内容を確認してから、一度だけ Acknowledge してください。** Acknowledge した後、それらのメールは**再試行されません**。別の方法で取得しない限り、アーカイブから欠落することになります。まず失敗の詳細を確認し、原因が設定ミスである場合は、Acknowledge する前に修正してください。チェックポイントの動作の詳細については [Mail Synchronization](Synchronization.md) を参照してください。

Acknowledge の操作は、失敗したメール数とアカウント情報とともに *SyncAcknowledgeFailures* としてアクセスログに記録されるため、後で追跡可能です。

## 👁️ ここではできないこと {#what-you-cannot-do-here}

- **再試行ボタンはありません。** 失敗したジョブをこのページから再起動することはできません。操作を開始した場所（同期、インポート、エクスポート）から再度やり直してください。
- **編集はできません。** ジョブのパラメータは作成時に固定されます。
- **タイプごとに直近の20件のみ。** 古いジョブはリストに表示されません。長期的な記録が必要な場合は、アクセスログまたはアプリケーションログを確認してください。詳細は [Access Logging](Logs.md) および [Docker Compose Logs](DockerComposeLogs.md) を参照してください。

## ❓ トラブルシューティング {#troubleshooting}

| 症状 | 考えられる原因 | 対処法 |
|---|---|---|
| ジョブが **Queued** のまま | 同じワーカーで別のジョブが実行中 | 待機する。他のセクションに長時間実行中のジョブがないか確認する |
| 進捗が 0% で止まっている | 1つの大きなフォルダを処理中 | 通常の動作です。"Why progress can sit at 0%" を参照してください |
| 同じアカウントで同期が繰り返し **Failed** になる | 個別のメールにおける永続的なエラー | 失敗の詳細を調査し、原因を修正してから Acknowledge する |
| 同期が **Rate Limited** と表示される | 帯域幅のクォータに達した | 何もしなくてよい。自動的に再開されます |
| 同期が **Timed Out** と表示される | メールボックスが制限時間を超えるサイズである | 次回の実行時にチェックポイントから継続されます。[Synchronization](Synchronization.md) を参照してください |
| MBox インポートで **CompletedWithErrors** になる | 一部のメッセージが不正な形式 (malformed) | カウンターに件数が表示されます。それ以外のメッセージはインポートされています |
| リロード後にジョブが消える | 「直近20件」の範囲から外れた | アプリケーションログで最終ステータスを確認してください |
| 作業中にページがリロードされる | 30秒ごとの自動更新 | `Jobs:RefreshSeconds` を `0` に設定してください |

## 📚 関連ドキュメント {#related-documentation}

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — チェックポイント、タイムアウトした同期について
- [Rate Limit Handling](RateLimitHandling.md) — 帯域幅のクォータと自動再開について
- [Access Logging](Logs.md) — ジョブ操作を含む、誰が何を行ったかの記録について
- [Batch Restore](MailboxMigration.md) — メールボックス間のメール移動について
- [Date-Windowed Offload](Offload.md) — オフロードジョブについて
- [Docker Compose Logs](DockerComposeLogs.md) — アプリケーションログの読み方
