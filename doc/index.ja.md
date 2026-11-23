# 📧 Mail Archiver Documentation {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**セルフホスト型のメールアーカイブ — あらゆるプロバイダーからメールをアーカイブ、検索、エクスポート。**
一度設定すれば、すべてのメッセージを保持し、数年後でも再び見つけることができます。
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __はじめに__

    ---

    Docker Compose で Mail Archiver をインストールし、最初のメールボックスを接続します。

    [:octicons-arrow-right-24: セットアップとパラメータ](Setup.md)

-   :material-magnify:{ .lg .middle } __あらゆるものを検索__

    ---

    すべてのカウントを対象とした全文検索、フィルタ、ソート、および一括操作。

    [:octicons-arrow-right-24: メール検索](Search.md)

-   :material-api:{ .lg .middle } __自動化__

    ---

    スクリプトや AI エージェントからアーカイブを読み取る — 読み取り専用のスコープ付き API キー。

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __セキュリティの確保__

    ---

    二要素認証、SSO、アクセスログ、および保持ポリシー。

    [:octicons-arrow-right-24: 二要素認証](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ インストールとメンテナンス {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __セットアップとパラメータ__

    ---

    すべての環境変数、Kestrel HTTPS、およびシークレット管理。

    [:octicons-arrow-right-24: 読む](Setup.md)

-   :material-server-network:{ .lg .middle } __リバースプロキシ__

    ---

    TLS 終端を備えた nginx または Caddy の背後に Mail Archiver を配置します。

    [:octicons-arrow-right-24: 読む](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL アップグレード__

    ---

    データを失うことなく、新しいメジャーバージョンの PostgreSQL に移行します。

    [:octicons-arrow-right-24: 読む](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __データベースメンテナンス__

    ---

    VACUUM、ANALYZE、および長期にわたってアーカイブの応答性を維持する方法。

    [:octicons-arrow-right-24: 読む](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __バックアップと復元__

    ---

    データベースのバックアップと復元 — Proxmox 設定を含む。

    [:octicons-arrow-right-24: 読む](BackupRestore.md)

-   :material-flask:{ .lg .middle } __ローカルテスト環境__

    ---

    本番環境に触れる前に、ローカルで使い捨てのインスタンスを実行します。

    [:octicons-arrow-right-24: 読む](LocalTestEnvironment.md)

</div>

## 📥 メールの取り込み {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __アカウントインポート__

    ---

    CSV ファイルから複数のアカウントを一度に一括追加します。

    [:octicons-arrow-right-24: 読む](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __同期__

    ---

    クイック同期 vs フル同期、チェックポイント、および同期が失敗した場合の挙動。

    [:octicons-arrow-right-24: 読む](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI コマンド__

    ---

    コマンドラインから既存のアーカイブをインポートしたり、データベースとファイルシステムの間の保存済みオリジナルメッセージを移動したりします。

    [:octicons-arrow-right-24: 読む](CLI.md)

-   :material-upload:{ .lg .middle } __Web インポート (EML / MBox)__

    ---

    ブラウザ経由で MBox ファイルまたは EML ファイルの ZIP をアップロードします。

    [:octicons-arrow-right-24: 読む](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __レート制限の処理__

    ---

    帯域幅の追跡、および一時停止された同期がどのように自動再開されるか。

    [:octicons-arrow-right-24: 読む](RateLimitHandling.md)

</div>

## 🔍 アーカイブの操作 {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __メール検索__

    ---

    クエリ構文、フィルタ、ソート、ページング、および一括操作。

    [:octicons-arrow-right-24: 読む](Search.md)

-   :material-progress-clock:{ .lg .middle } __バックグラウンドジョブ__

    ---

    同期、インポート、復元、エクスポートの監視 — およびそれらの安全なキャンセル。

    [:octicons-arrow-right-24: 読む](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __メールボックス移行__

    ---

    フォルダ構造を維持しながら、メールボックス間でメールをコピーします。

    [:octicons-arrow-right-24: 読む](MailboxMigration.md)

-   :material-download:{ .lg .middle } __アカウントエクスポート (EML / MBox)__

    ---

    アカウント全体をポータブルな EML または MBox アーカイブとして書き出します。

    [:octicons-arrow-right-24: 読む](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __オリジナルメッセージの保存__

    ---

    受信時のメッセージをそのまま保持するため、DKIM 検証が引き続き有効であり、エクスポートがバイト単位で正確になります。

    [:octicons-arrow-right-24: 読む](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __添付ファイルの重複排除__

    ---

    同一の添付ファイルが複数回ではなく、一度だけどのように保存されるか。

    [:octicons-arrow-right-24: 読む](AttachmentDeduplication.md)

-   :material-file-pdf-box:{ .lg .middle } __XRechnung 請求書プレビュー__

    ---

    XRechnung XML 添付ファイルをアーカイブ内で直接 PDF として読み取ります。

    [:octicons-arrow-right-24: 読む](EInvoicing.md)

-   :material-calendar-arrow-right:{ .lg .middle } __日付ウィンドウによるオフロード__

    ---

    古いメールを、一定期間（ウィンドウ）ごとに別のカウントへ移動します。

    [:octicons-arrow-right-24: 読む](Offload.md)

-   :material-harddisk:{ .lg .middle } __アカウントごとのストレージ使用量__

    ---

    各メールボックスがどれだけの容量を使用しているかを確認します。

    [:octicons-arrow-right-24: 読む](AccountStorage.md)

</div>

## 🔐 管理とセキュリティ {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __ユーザー管理__

    ---

    ユーザーの作成、管理者権限の付与、メールボックス権限の割り当て、および自身のパスワード変更。

    [:octicons-arrow-right-24: 読む](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __二要素認証__

    ---

    TOTP の設定、バックアップコード、およびデバイス紛失時の対処法。

    [:octicons-arrow-right-24: 読む](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Entra ID、Authelia、またはその他の ID プロバイダーを介したサインイン。

    [:octicons-arrow-right-24: 読む](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __アクセスログ__

    ---

    誰が、何を、開き、検索し、エクスポートし、または削除したか。

    [:octicons-arrow-right-24: 読む](Logs.md)

-   :material-file-export:{ .lg .middle } __監査データのエクスポート__

    ---

    コンプライアンスレビューのために監査トレイルをエクスポートします。

    [:octicons-arrow-right-24: 読む](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __緊急アカウントリカバリ__

    ---

    アカウントがロックされた際のアクセス復旧。

    [:octicons-arrow-right-24: 読む](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __保持ポリシー__

    ---

    メールサーバーまたはローカルアーカイブからの自動削除。

    [:octicons-arrow-right-24: 読む](RetentionPolicies.md)

</div>

## ☁️ プロバイダーガイド {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure アプリ登録 (M365)__

    ---

    アプリを登録し、Microsoft 365 の保持設定を構成します。

    [:octicons-arrow-right-24: 読む](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 テナント メールボックスインポート__

    ---

    単一のフォームから、テナント内のすべてのメールボックスを一括インポートします。

    [:octicons-arrow-right-24: 読む](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow インスタンス__

    ---

    メールボックスを中央で選択し、専用の IMAP アプリパスワードをプロビジョニングします。

    [:octicons-arrow-right-24: 読む](Mailcow.md)

-   :material-account-key:{ .lg .middle } __個人用 Microsoft アカウント__

    ---

    デバイスコードフローによる Outlook.com、Hotmail、および M365 Family。

    [:octicons-arrow-right-24: 読む](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail ベストプラクティス__

    ---

    アプリパスワード、ラベル、expunge、および Gmail のレート制限。

    [:octicons-arrow-right-24: 読む](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo メールの制限事項__

    ---

    Yahoo の IMAP およびエクスポートエンドポイントが実行できないこと。

    [:octicons-arrow-right-24: 読む](YahooMailExportLimitations.md)

</div>

## 🔌 プログラマティックアクセス {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    スコープ付きのユーザー別 API キーを使用した、アーカイブへの読み取り専用アクセス。

    [:octicons-arrow-right-24: 読む](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    発見可能なツールとして、AI エージェントにアーカイブを公開します。

    [:octicons-arrow-right-24: 読む](MCP.md)

</div>

## 🧰 オペレーションと開発 {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __言語とローカライゼーション__

    ---

    インターフェース言語の切り替え、および翻訳の追加方法。

    [:octicons-arrow-right-24: 読む](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose ログ__

    ---

    コンテナログの読み取りとログレベルの変更。

    [:octicons-arrow-right-24: 読む](DockerComposeLogs.md)

-   :material-heart-pulse:{ .lg .middle } __ヘルスチェックとメトリクス__

    ---

    Prometheus または他のモニタリングシステムからの liveness、readiness、およびアーカイブメトリクスの監視。

    [:octicons-arrow-right-24: 読む](Monitoring.md)

-   :material-tag:{ .lg .middle } __開発バージョン__

    ---

    今後の変更をテストするために、dev タグに切り替えます。

    [:octicons-arrow-right-24: 読む](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **このドキュメントは継続的に拡張されています。**
間違いを見つけた場合や提案がある場合は、
[issue を作成](https://github.com/s1t5/mail-archiver/issues)するか、プルリクエストを送信してください。
</div>
