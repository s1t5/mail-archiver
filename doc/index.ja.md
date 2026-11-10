# 📧 Mail Archiver Documentation {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**セルフホスト型のメールアーカイブ — あらゆるプロバイダーからメールをアーカイブ、検索、エクスポート。**
一度設定すれば、すべてのメッセージを保持し、数年後でも再び見つけ出すことができます。
</div>

!!! note "翻訳について"
    英語のドキュメントが正本です。他の言語はこれらのページから自動的に翻訳されているため、誤りが含まれている可能性があります。言語を変更するには、ヘッダーの言語スイッチャーを使用してください。

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __はじめに__

    ---

    Docker Compose で Mail Archiver をインストールし、最初のメールボックスに接続します。

    [:octicons-arrow-right-24: Setup and parameters](Setup.md)

-   :material-magnify:{ .lg .middle } __あらゆるものを検索__

    ---

    すべてのカウントを対象とした全文検索、フィルタ、ソート、および一括操作。

    [:octicons-arrow-right-24: Mail search](Search.md)

-   :material-api:{ .lg .middle } __自動化__

    ---

    スクリプトや AI エージェントからアーカイブを読み取る — 読み取り専用のスコープ付き API キー。

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __セキュリティの確保__

    ---

    二要素認証、SSO、アクセスログ、および保持ポリシー。

    [:octicons-arrow-right-24: Two-factor auth](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Installation & Maintenance {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Setup and parameters__

    ---

    すべての環境変数、Kestrel HTTPS、およびシークレット管理。

    [:octicons-arrow-right-24: Read](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    TLS 終端を備えた nginx または Caddy の背後に Mail Archiver を配置します。

    [:octicons-arrow-right-24: Read](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL upgrade__

    ---

    データを失うことなく、新しいメジャーバージョンの PostgreSQL に移行します。

    [:octicons-arrow-right-24: Read](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Database maintenance__

    ---

    VACUUM、ANALYZE、および長期にわたってアーカイブのレスポンスを維持する方法。

    [:octicons-arrow-right-24: Read](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Backup and restore__

    ---

    データベースのバックアップと復元 — Proxmox のセットアップも含まれます。

    [:octicons-arrow-right-24: Read](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Local test environment__

    ---

    本番環境に触れる前に、ローカルで使い捨てのインスタンスを実行します。

    [:octicons-arrow-right-24: Read](LocalTestEnvironment.md)

</div>

## 📥 Getting Mail In {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Account import__

    ---

    CSV ファイルから複数のアカウントを一度に追加します。

    [:octicons-arrow-right-24: Read](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Synchronization__

    ---

    クイック同期 vs フル同期、チェックポイント、および同期が失敗した場合の挙動。

    [:octicons-arrow-right-24: Read](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI commands__

    ---

    コマンドラインから既存のアーカイブをインポートし、データベースとファイルシステム間で保存されたオリジナルメッセージを移動します。

    [:octicons-arrow-right-24: Read](CLI.md)

-   :material-upload:{ .lg .middle } __Web import (EML / MBox)__

    ---

    ブラウザ経由で MBox ファイルまたは EML ファイルの ZIP をアップロードします。

    [:octicons-arrow-right-24: Read](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Rate limit handling__

    ---

    帯域幅の追跡、および一時停止された同期がどのように自動的に再開されるか。

    [:octicons-arrow-right-24: Read](RateLimitHandling.md)

</div>

## 🔍 Working with the Archive {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Mail search__

    ---

    クエリ構文、フィルタ、ソート、ページング、および一括操作。

    [:octicons-arrow-right-24: Read](Search.md)

-   :material-progress-clock:{ .lg .middle } __Background jobs__

    ---

    同期、インポート、復元、エクスポートの監視 — およびそれらの安全なキャンセル。

    [:octicons-arrow-right-24: Read](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Mailbox migration__

    ---

    フォルダ構造を維持しながら、メールボックス間でメールをコピーします。

    [:octicons-arrow-right-24: Read](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Account export (EML / MBox)__

    ---

    アカウント全体を、ポータブルな EML または MBox アーカイブとして書き出します。

    [:octicons-arrow-right-24: Read](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Original message storage__

    ---

    受信時のメッセージをそのまま保持するため、DKIM 検証が維持され、エクスポートはバイト単位で正確になります。

    [:octicons-arrow-right-24: Read](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Attachment deduplication__

    ---

    同一の添付ファイルを複数回ではなく、一度だけ保存する方法。

    [:octicons-arrow-right-24: Read](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Date-windowed offload__

    ---

    古いメールを、期間（ウィンドウ）ごとに別のカウントへ移動します。

    [:octicons-arrow-right-24: Read](Offload.md)

-   :material-harddisk:{ .lg .middle } __Per-account storage__

    ---

    各メールボックスがどれだけの容量を使用しているかを確認します。

    [:octicons-arrow-right-24: Read](AccountStorage.md)

</div>

## 🔐 Administration & Security {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __User management__

    ---

    ユーザーの作成、管理者権限の付与、メールボックスの権限割り当て、および自身のパスワード変更。

    [:octicons-arrow-right-24: Read](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Two-factor authentication__

    ---

    TOTP の設定、バックアップコード、およびデバイスを紛失した場合の対処法。

    [:octicons-arrow-right-24: Read](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Entra ID、Authelia、またはその他の ID プロバイダー経由でのサインイン。

    [:octicons-arrow-right-24: Read](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Access logging__

    ---

    誰が、何を、開き、検索し、エクスポートし、または削除したか。

    [:octicons-arrow-right-24: Read](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit data export__

    ---

    コンプライアンスレビューのための監査トレイルのエクスポート。

    [:octicons-arrow-right-24: Read](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Emergency account recovery__

    ---

    アカウントがロックアウトされた際のアクセス復旧。

    [:octicons-arrow-right-24: Read](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Retention policies__

    ---

    メールサーバーまたはローカルアーカイブからの自動削除。

    [:octicons-arrow-right-24: Read](RetentionPolicies.md)

</div>

## ☁️ Provider Guides {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure app registration (M365)__

    ---

    アプリを登録し、Microsoft 365 の保持設定を構成します。

    [:octicons-arrow-right-24: Read](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant mailbox import__

    ---

    単一のフォームから、テナント内のすべてのメールボックスを一括インポートします。

    [:octicons-arrow-right-24: Read](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow instances__

    ---

    メールボックスを中央で選択し、専用の IMAP アプリパスワードをプロビジョニングします。

    [:octicons-arrow-right-24: Read](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Personal Microsoft accounts__

    ---

    デバイスコードフローによる Outlook.com、Hotmail、および M365 Family。

    [:octicons-arrow-right-24: Read](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail best practices__

    ---

    アプリパスワード、ラベル、expunge、および Gmail のレート制限。

    [:octicons-arrow-right-24: Read](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail limitations__

    ---

    Yahoo の IMAP およびエクスポートエンドポイントが実行できないこと。

    [:octicons-arrow-right-24: Read](YahooMailExportLimitations.md)

</div>

## 🔌 Programmatic Access {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    スコープ付きのユーザー別 API キーを使用した、アーカイブへの読み取り専用アクセス。

    [:octicons-arrow-right-24: Read](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    アーカイブを、発見可能なツールとして AI エージェントに公開します。

    [:octicons-arrow-right-24: Read](MCP.md)

</div>

## 🧰 Operations & Development {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Language and localization__

    ---

    インターフェース言語の切り替え、および翻訳の追加方法。

    [:octicons-arrow-right-24: Read](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose logs__

    ---

    コンテナログの読み取りとログレベルの変更。

    [:octicons-arrow-right-24: Read](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Development versions__

    ---

    今後の変更をテストするために、dev タグに切り替えます。

    [:octicons-arrow-right-24: Read](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **このドキュメントは継続的に拡張されています。**
間違いを見つけた場合や提案がある場合は、
[open an issue](https://github.com/s1t5/mail-archiver/issues) するか、プルリクエストを送信してください。
</div>
