# 📧 Gmail Best Practices {#gmail-best-practices}

[← Back to Documentation Index](index.md)

Mail ArchiverアプリケーションでGmailアカウントを使用する場合、最適なパフォーマンスを確保し、潜在的な問題を回避するために従うべき特定の構成とベストプラクティスがいくつかあります。

## 🔐 App Passwordsを使用したIMAP構成 {#imap-configuration-with-app-passwords}

Gmailアカウントは、App Passwordsを使用したIMAPを用いてMail Archiverアプリケーションと統合できます。

1. Gmailアカウントで2要素認証を有効にする
2. Mail Archiverアプリケーション専用のApp Passwordを生成する
3. Mail Archiverでアカウントを構成する際、通常のGmailパスワードの代わりにApp Passwordを使用する

## 🏷️ Gmailラベル設定 {#gmail-label-settings}

IMAP経由で「すべてのメール」ラベルが公開されるのを防ぐために、Gmailのラベル設定を調整することを強く推奨します。

1. Gmailのウェブインターフェースに移動し、設定（上部の歯車アイコン）を開く → すべての設定を表示 → ラベルタブ
2. リストから「すべてのメール」ラベルを探す
3. 「すべてのメール」の「IMAPで表示」のチェックを外す

### 📚 Gmailラベルの実装に関する背景 {#background-on-gmail-label-implementation}

Gmailのラベルシステムは、従来のメールプロバイダーとは動作が異なります。Gmailでは：
- すべてのメールは単一の「すべてのメール」リポジトリに存在します
- ラベルはフォルダではなくタグとして機能します
- 「すべてのメール」ラベルには、アカウント内のすべてのメールが含まれます
- メールにラベルが付くと、元のフォルダとラベル付けされたセクションの両方に表示されます。そのため、受信トレイにあるすべてのメールも「すべてのメール」ラベルに表示されます。

### ⚠️ IMAP取得における問題 {#issues-with-imap-retrieval}

IMAP経由で「すべてのメール」が公開されると、いくつかの問題が発生する可能性があります：

1. **重複処理**: メールが複数のフォルダ/ラベルに表示されるため、アーカイバが同じメールを複数回処理してしまう可能性があります
2. **パフォーマンスへの影響**: 数千通のメールを持つ大規模なアカウントでは、「すべてのメール」の取得が極端に遅くなることがあります
4. **同期の問題**: メールが実際にどのフォルダに属しているかを判断しようとする際に、競合が発生する可能性があります

「すべてのメール」へのIMAPアクセスを無効にすることで、メールがプライマリな場所から一度だけ取得されることが保証され、パフォーマンスと正確性の両方が向上します。

## 🗑️ 保存ポリシーとExpunge設定 {#retention-policy-and-expunge-settings}

メインのREADMEで言及されているように、Gmailアカウントでは保存ポリシーに関して特別な注意が必要です：

- Gmailの設定の「メールの転送と POP/IMAP」タブで、GmailのAuto-Expunge機能を無効にする必要があります
- この設定は、Gmailアカウントで保存ポリシーを正しく機能させるために不可欠です

この設定を構成するには：
1. Gmailの設定 → すべての設定を表示 → メールの転送と POP/IMAP タブに移動する
2. 「IMAPアクセス」セクションで、「Auto-Expunge off - Wait for the client to update the server.」および「メッセージをゴミ箱に移動する」を選択する

## ⚠️ Gmail IMAPレート制限 {#gmail-imap-rate-limiting}

Gmailは、IMAP取得に対して1日2500MBのレート制限を課しています。大量のメールをアーカイブする場合、この制限に達する可能性があり、その場合、翌日までメールの取得が一時的に停止される可能性があることに注意してください。

### 📊 レート制限の処理 {#rate-limit-handling}

Mail Archiverは、GmailのIMAP帯域幅制限を管理するために、組み込みのレート制限処理を提供しています：

- **自動帯域幅トラッキング**: アカウントごとの1日のダウンロードデータを監視します
- **構成可能な制限**: Gmail用に `BandwidthTracking__DailyLimitMb=2500` を設定します
- **自動一時停止**: 制限に達すると同期が一時停止し、リセット後に再開されます
- **チェックポイントシステム**: 同期の進捗が保存され、最後の位置から再開されます

詳細な構成と使用方法については、**[Rate Limit Handling Guide](RateLimitHandling.md)** を参照してください。

### 🔧 Gmail構成例 {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 大規模メールボックスに関する考慮事項 {#large-mailbox-considerations}

大規模なGmailメールボックス（例：14 GB）の場合、初期同期には数日かかります：

| メールボックスのサイズ | 完了までの日数 (2500 MB/日として) |
|--------------|-----------------------------------|
| 5 GB | ~2日 |
| 10 GB | ~4日 |
| 14 GB | ~6日 |
| 20 GB | ~8日 |

**ヒント**: 最初から適切なレート制限処理が行われるよう、大規模なGmailアカウントを追加する**前**に帯域幅トラッキングを有効にしてください。

## ✅ ベストプラクティスのまとめ {#best-practices-summary}

これらのベストプラクティスに従うことで、データの整合性と最適なパフォーマンスを維持しながら、GmailアカウントとのMail Archiverの円滑な運用を確保できます。
