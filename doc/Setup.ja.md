# 🛠️ Mail Archiver Setup Guide {#mail-archiver-setup-guide}

[← Documentation Index に戻る](index.md)

## 📋 概要 {#overview}

このガイドでは、Docker Compose を使用して Mail Archiver アプリケーションをセットアップするための詳細な手順を提供します。

## 🛠️ 前提条件 {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 インストール手順 {#installation-steps}

1. システムに前提条件をインストールします。

2. 以下の内容で `docker-compose.yml` ファイルを作成します：

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey;

      # Authentication Settings
      - Authentication__Username=admin
      - Authentication__Password=secure123!
      - Authentication__SessionTimeoutMinutes=60
      - Authentication__CookieName=MailArchiverAuth
      - Authentication__CookieSameSite=Lax

      # REST API Settings (read-only, disabled by default)
      - Api__Enabled=false
      - Api__AllowAttachmentDownloads=true
      - Api__AllowMessageDownloads=true
      - Api__EnableSwaggerUi=true
      - Api__DefaultPageSize=20
      - Api__MaxPageSize=100
      - Api__RateLimitPerMinute=120

      # MCP Server Settings (Model Context Protocol for AI agents, read-only, disabled by default)
      - Mcp__Enabled=false
      - Mcp__AllowAttachmentDownloads=true
      - Mcp__DefaultPageSize=20
      - Mcp__MaxResults=100
      - Mcp__MaxAttachmentBytes=10000000
      - Mcp__AllowMessageDownloads=true
      - Mcp__MaxMessageBytes=10000000
      - Mcp__RateLimitPerMinute=120

      # MailSync Settings
      - MailSync__IntervalMinutes=15
      - MailSync__TimeoutMinutes=0
      - MailSync__ConnectionTimeoutSeconds=180
      - MailSync__CommandTimeoutSeconds=300
      - MailSync__AlwaysForceFullSync=false
      - MailSync__IgnoreSelfSignedCert=false
      - MailSync__MaxConcurrentSyncs=1
      - MailSync__InterAccountDelaySeconds=0
      - MailSync__MaxIssuesPerKind=20
      - MailSync__FullSyncIntervalHours=24
      - MailSync__ExcludeSubfolders=false
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Contacts

      # Original Message Storage (complete .eml per email, disabled by default)
      - RawMessages__Enabled=false
      - RawMessages__Storage=postgres
      - RawMessages__MaxMessageSizeMb=50
      - RawMessages__IncludeGraph=true
      - RawMessages__FileSystem__BasePath=RawMessages
      - RawMessages__FileSystem__ShardDepth=2
      - RawMessages__FileSystem__VerifyChecksumOnRead=true

      # BatchRestore Settings
      - BatchRestore__AsyncThreshold=50
      - BatchRestore__MaxSyncEmails=150
      - BatchRestore__MaxAsyncEmails=50000
      - BatchRestore__SessionTimeoutMinutes=30
      - BatchRestore__DefaultBatchSize=50

      # Tenant Management Settings
      - TenantManagement__MaxSelectedMailboxes=1000

      # Mailcow Settings
      - Mailcow__MaxSelectedMailboxes=1000

      # BatchOperation Settings
      - BatchOperation__BatchSize=50
      - BatchOperation__PauseBetweenEmailsMs=50
      - BatchOperation__PauseBetweenBatchesMs=250

      # Bandwidth Tracking Settings (for IMAP rate limit handling)
      - BandwidthTracking__Enabled=false
      - BandwidthTracking__DailyLimitMb=25000
      - BandwidthTracking__WarningThresholdPercent=80
      - BandwidthTracking__PauseHoursOnLimit=24
      - BandwidthTracking__TrackUploadBytes=false

      # Selection Settings
      - Selection__MaxSelectableEmails=250

      # View Settings (Privacy & Display)
      - View__DefaultToPlainText=true
      - View__BlockExternalResources=false

      # Dashboard Settings (statistics cache, counter parts, chart periods)
      - Dashboard__CacheSeconds=60
      - Dashboard__RefreshIntervalMinutes=15
      - Dashboard__ShowDirectionSplits=true
      - Dashboard__SelectablePeriods=true

      # Jobs Settings (background jobs page)
      - Jobs__RefreshSeconds=30

      # Npgsql Settings
      - Npgsql__CommandTimeout=900

      # Upload Settings for MBox and EML files
      - Upload__MaxFileSizeGB=10
      - Upload__KeepAliveTimeoutHours=4
      - Upload__RequestHeadersTimeoutHours=2

      # Local Import Settings (for CLI imports from mounted volumes)
      - LocalImport__AllowedPaths__0=/data/import

      # CSV Import Settings (for bulk IMAP account import)
      - CsvImport__MaxRows=5000
      - CsvImport__MaxFileSizeBytes=10000000

      # Deletion Policy Settings (Optional - controls whether email deletion is allowed)
      - DeletionPolicy__DeletionAllowed=true

      # TimeZone Settings
      - TimeZone__DisplayTimeZoneId=Etc/UCT

      # Database Maintenance Settings (Optional)
      - DatabaseMaintenance__Enabled=false
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30

      # Attachment Deduplication Settings (Optional - feature is always on)
      - AttachmentDeduplication__BatchSize=200
      - AttachmentDeduplication__DelayBetweenBatchesMs=0
      - AttachmentDeduplication__StartupDelaySeconds=20
      - AttachmentDeduplication__OrphanCleanupIntervalHours=12
      - AttachmentDeduplication__CommandTimeoutSeconds=300

      # Account Storage Settings (Optional - per-account storage display)
      - AccountStorage__Enabled=true
      - AccountStorage__DailyExecutionTime=02:30
      - AccountStorage__BackfillDelayMs=5000
      - AccountStorage__RefreshBatchDelayMs=1000
      - AccountStorage__CommandTimeoutSeconds=300

      # ReleaseNotes Settings (Version Update Splash Screen)
      - ReleaseNotes__Enabled=true
      - ReleaseNotes__UpdateHintEnabled=true
      - ReleaseNotes__UpdateCheckIntervalHours=6

      # Logging Settings (Optional - defaults to Information level)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning

      # Security Settings
      - AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com

      # OIDC Configuration (see OIDC_Implementation.md for detailed setup)
      - OAuth__Enabled=true
      - OAuth__Authority=https://example.com
      - OAuth__ClientId=YOUR-CLIENT-ID
      - OAuth__ClientSecret=YOUR-CLIENT-SECRET
      - OAuth__DisplayName=PocketID SSO
      - OAuth__ClientScopes__0=openid
      - OAuth__ClientScopes__1=profile
      - OAuth__ClientScopes__2=email
      - OAuth__DisablePasswordLogin=false
      - OAuth__AutoRedirect=false
      - OAuth__AutoApproveUsers=false
      - OAuth__AdminEmails__0=admin@example.com
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      # Uncomment the following line to mount a directory for local file imports
      # (also configure LocalImport__AllowedPaths__0=/data/import in environment variables)
      # - /path/to/your/mbox/files:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
      # Only with RawMessages__Storage=filesystem: keep the original messages outside the
      # container, otherwise they are lost when the container is recreated.
      # - ./raw-messages:/app/RawMessages
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

networks:
  postgres:
```

3. `docker-compose.yml` 内のデータベース設定を編集し、`POSTGRES_PASSWORD` 変数と `ConnectionString` に安全なパスワードを設定します。

4. 認証を使用する場合（強く推奨されます）、管理者ユーザーに使用される `Authentication__Username` と `Authentication__Password` を定義します。

5. `TimeZone__DisplayTimeZoneId` 環境変数を、希望するタイムゾーンに合わせて調整します（デフォルトは "Etc/UCT" です）。任意の IANA タイムゾーン識別子（例：「Europe/Berlin」、「Asia/Tokyo」）を使用できます。

6. オプションとして、アプリケーションログの冗長性を制御するために `Logging__LogLevel` 環境変数を設定します。利用可能なオプションについては、後述の Logging Settings セクションを参照してください。

7. アプリケーションへのアクセスを保護するために、https と認証を備えた任意のリバースプロキシを構成してください。

> ⚠️ **注意**: アプリケーション自体は https による暗号化されたアクセスを提供しません！リバースプロキシを介して設定する必要があります！

8. コンテナの初回起動：
```bash
docker compose up -d
```

9. コンテナの再起動：
```bash
docker compose restart
```

10. アプリケーションへのアクセス

11. 定義した資格情報でログインし、最初のメールアカウントを追加します：
   - "Email Accounts" セクションに移動します
   - "New Account" をクリックします
   - サーバーの詳細と資格情報を入力します
   - 保存してアーカイブを開始します！
   - 必要に応じて、他のユーザーを作成し、アカウントを割り当てます。

## 📚 環境変数の説明 {#environment-variable-explanations}

### 🗄️ Database Connection {#database-connection}
- `ConnectionStrings__DefaultConnection`: PostgreSQL データベースへの接続文字列。必要に応じて `Host`、`Database`、`Username`、および `Password` の値を変更してください。

### 🔐 Authentication Settings {#authentication-settings}
- `Authentication__Username`: 管理者アカウントのユーザー名。
- `Authentication__Password`: 管理者アカウントのパスワード。
- `Authentication__SessionTimeoutMinutes`: セッションタイムアウト（分単位）。これは非永続的なログインクッキーに適用されます。ログインフォームで **Remember Me** にチェックを入れたユーザーには、代わりに7日間有効な永続的なクッキーが発行されます。詳細は [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox) を参照してください。
- `Authentication__CookieName`: 認証クッキーの名前。
- `Authentication__CookieSameSite`: 認証、セッション、および CSRF 保護クッキーの SameSite 属性を設定します。有効な値は以下の通りです：
  - `Strict` (デフォルト): 最大のセキュリティ。クッキーは Same-site リクエストでのみ送信されます。外部リンク（例：他のウェブサイトからのリンクをクリックする場合）からアプリケーションに移動する際、既存のセッションが認識されないため、問題が発生する可能性があります。
  - `Lax`: リバースプロキシを使用する場合に推奨されます。クッキーはトップレベルのナビゲーションおよび Same-site リクエストと共に送信されるため、POST リクエストの CSRF 保護を維持しつつ、ユーザーが外部リンクからアプリケーションへ遷移することを可能にします。
  - `None`: クッキーはすべてのリクエストと共に送信されます。HTTPS と `Secure` 属性が必要です。特定のクロスサイト要件があり、セキュリティ上の影響を理解している場合にのみ使用してください。

### 🔌 REST API Settings {#rest-api-settings}
オプションの読み取り専用 REST API は **デフォルトで無効** になっています。完全なリファレンスについては [REST API guide](API.md) を参照してください。
- `Api__Enabled`: 読み取り専用 REST API のマスタースイッチ (デフォルト `false`)。`false` の場合、すべての `/api/*` ルートは `404` を返します。
- `Api__AllowAttachmentDownloads`: API 経由での添付ファイルのバイトデータのダウンロードを許可します (デフォルト `true`)。`false` の場合、添付ファイルエンドポイントは `403` を返します。
- `Api__AllowMessageDownloads`: API 経由での完全なメッセージ（再構築された `/eml` とオリジナルの `/raw` の両方）のダウンロードを許可します (デフォルト `true`)。`false` の場合、両方のエンドポイントは `403` を返します。完全なメッセージには添付ファイルが含まれるため、このスイッチと `Api__AllowAttachmentDownloads` の**両方**が `true` である必要があります。いずれか一方をオフにすると、両方のエンドポイントが無効になります。
- `Api__EnableSwaggerUi`: `/apidocs` で Swagger UI を、`/apidocs/spec/v1.json` で OpenAPI ドキュメントを公開します (デフォルト `true`)。どちらもログイン済みのブラウザセッションが必要です。
- `Api__DefaultPageSize`: 指定がない場合のリストエンドポイントのデフォルトのページサイズ (デフォルト `20`)。
- `Api__MaxPageSize`: 許可される最大ページサイズ。これより大きいリクエストは制限されます (デフォルト `100`)。
- `Api__RateLimitPerMinute`: API キーごとの 1 分間あたりの固定ウィンドウ・リクエスト予算 (デフォルト `120`)。

### 🤖 MCP Server Settings {#mcp-server-settings}
オプションのMCP (Model Context Protocol) サーバーは、Streamable HTTPトランスポートを介して、REST APIと同じ読み取り専用のメールアクセスをAIエージェントに公開します。これは**デフォルトで無効**になっています。詳細なリファレンスについては、[MCP guide](MCP.md)を参照してください。MCPは、REST APIと全く同じAPIキー（`ma_...` bearer tokens）および、ユーザーごとのアカウントアクセススコーピングを再利用します。
- `Mcp__Enabled`: MCPサーバーのマスタースイッチ（デフォルト `false`）。`false`の場合、`/mcp` エンドポイントは `404` を返します。
- `Mcp__AllowAttachmentDownloads`: `get_attachment` ツールが添付ファイルのバイトデータを返すことを許可します（デフォルト `true`）。`false` の場合、ツールはエラーを返します。
- `Mcp__DefaultPageSize`: 呼び出し側が `pageSize` を省略した場合に `search_emails` が使用するデフォルトのページサイズ（デフォルト `20`）。
- `Mcp__MaxResults`: `search_emails` のページサイズの最大値。これより大きい値は制限されます（デフォルト `100`）。
- `Mcp__MaxAttachmentBytes`: `get_attachment` ツールがbase64としてインラインで返す添付ファイルの最大サイズ（バイト単位）。これより大きい添付ファイルは拒否されます（デフォルト `10000000` ≈ 10 MB）。
- `Mcp__AllowMessageDownloads`: `get_email_eml` および `get_email_raw` ツールが完全なメッセージを返すことを許可します（デフォルト `true`）。`false` の場合、ツールはエラーを返します。完全なメッセージには添付ファイルが含まれるため、このスイッチと `Mcp__AllowAttachmentDownloads` の**両方**が `true` である必要があります。どちらか一方をオフにすると、両方のツールが無効になります。
- `Mcp__MaxMessageBytes`: `get_email_eml` および `get_email_raw` ツールがインラインで返すメッセージの最大サイズ（バイト単位）。これより大きいメッセージは拒否されます（デフォルト `10000000` ≈ 10 MB）。
- `Mcp__RateLimitPerMinute`: APIキーごとの1分あたりの固定ウィンドウリクエスト予算（デフォルト `120`）。

### 📨 MailSync Settings {#mailsync-settings}
- `MailSync__IntervalMinutes`: メール同期の間隔（分単位）。これはグローバルなデフォルト値です。各アカウントは、作成/編集ページから個別にこの値を上書きできます（デフォルトを使用する場合は空欄のままにします）。
- `MailSync__FullSyncIntervalHours`: 自動フル再同期のためのオプションのグローバルデフォルト（時間単位）。設定されていない場合（デフォルト）、作成/編集ページでアカウントごとの `FullSyncIntervalHours` 値が設定されていない限り、自動フル同期は実行されません。アカウントごとの値は、このグローバルデフォルトを上書きします。
- `MailSync__TimeoutMinutes`: アカウントごとの同期タイムアウト（分単位）。実行時間が長くなった同期は、次のメッセージ境界で停止し、チェックポイントを保持して次回の実行時に再開されます。このジョブは `Timed Out` として報告され、`LastSync` は進められません。これはスケジュールされた同期にのみ適用されます。UIから開始された同期（手動同期またはフル再同期）はタイムアウトなしで実行されます。`0`（デフォルト）または 0 以下の値は、タイムアウトなしを意味します。大きなメールボックスを同期している場合は、アップグレード前にこの値を確認してください。詳細は [Synchronization.md](Synchronization.md#-stopping-a-sync-early) を参照してください。
- `MailSync__ConnectionTimeoutSeconds`: IMAP接続の接続タイムアウト（秒単位）。
- `MailSync__CommandTimeoutSeconds`: IMAPコマンドのコマンドタイムアウト（秒単位）。
- `MailSync__AlwaysForceFullSync`: 常にフル同期を強制するかどうか (true/false)。
- `MailSync__IgnoreSelfSignedCert`: 自己署名証明書を無視するかどうか (true/false)。
- `MailSync__MaxConcurrentSyncs`: 同時に実行できるアカウント同期の数。デフォルトは `1`（逐次実行、後方互換性あり）。スケジューラーは、バッチ全体を待つのではなく、スロットが空き次第補充するため、一つの遅いメールボックスが他の同期を妨げることがなくなりました。複数のアカウントを並行して同期するには、この値を増やしてください。その際、プロバイダーのレート制限とローカルのリソース使用量に注意してください。詳細は [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled) を参照してください。
- `MailSync__InterAccountDelaySeconds`: 各アカウントの同期タスクの終了時に適用される、オプションのディレイ（秒単位）。デフォルトは `0`（ディレイなし）。`MaxConcurrentSyncs > 1` 時のバースト的な開始を避けるのに役立ちます。
- `MailSync__MaxIssuesPerKind`: アカウントページに表示するために、同期ジョブが各種類の問題をいくつ保持するか。失敗したフォルダ、不足しているフォルダ、失敗したメッセージは個別に予算（バジェット）が割り当てられているため、一種の問題が大量に発生しても他の問題が画面から押し出されることはありません。予算を超えたものは、保持されるのではなくカウントされます。デフォルトは `20` です。`0` を設定すると詳細表示がオフになり、カウンターのみが残ります。詳細は [Synchronization.md](Synchronization.md#-observing-the-sync) を参照してください。
- `MailSync__GlobalExcludedFolders__<n>`: 各アカウント独自の除外フォルダリストに加えて、**すべての**アカウントで同期から除外されるフォルダ。デフォルトでは空であるため、既存の設定には影響しません。2つのリストは**加算的**であり、いずれかに一致するフォルダはスキップされます。両方のリストは同じマッチングルールを使用します：フルパスの完全一致、フォルダ名の完全一致、パスサフィックスの一致（したがって `Drafts` は `INBOX/Drafts` と `INBOX.Drafts` にも一致します）、および `MailSync__ExcludeSubfolders` がオンの場合にエントリが指定するフォルダ以下のすべての項目。マッチングはケースインセンシティブ（大文字小文字を区別しない）です。同じサーバーから多くのメールボックスをインポートする場合、各アカウントで同一の除外リストを維持する代わりにこれを使用すると便利です。デフォルトではフォルダは除外されません。どの名前をリストすべきかは、サーバーとその言語に依存します。カレンダーや連絡先フォルダも含まれるメールボックスツリーの例：
  ```yaml
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Calendars
      - MailSync__GlobalExcludedFolders__2=Contacts
      - MailSync__GlobalExcludedFolders__3=AddressBook
      - MailSync__GlobalExcludedFolders__4=Tasks
      - MailSync__GlobalExcludedFolders__5=TaskList
      - MailSync__GlobalExcludedFolders__6=Notes
      - MailSync__GlobalExcludedFolders__7=NoteList
      - MailSync__GlobalExcludedFolders__8=Journal
  ```
- `MailSync__ExcludeSubfolders`: 除外エントリが、指定されたフォルダの下にあるフォルダも対象とするかどうか。これはアカウントごとのリストとインストール全体の設定の両方に適用され、両方のプロバイダーで動作します。デフォルトは `false` で、このオプションが存在する前の動作と一致します。つまり、エントリ `Deleted Items` はそのフォルダのみに一致し、ツリー全体をアーカイブから除外するには、そのルートとそれ以下のすべてのフォルダを指定する必要があります。`true` に設定すると、エントリがその下のツリーも対象とするようになります。この場合、エントリ `Deleted Items` は `Deleted Items/2024` とそのブランチの残りの部分もアーカイブから除外します。このルールはパスセパレータに固定されるため、フォルダ全体を対象とし、名前の一部を対象にすることはありません。例えば、`Archive` は `Archive` と `Archive/Team` をカバーしますが、`Team Archive Review` という名前のメールフォルダはそのまま残します。パスサフィックスルールと同様に、`.` もセパレータとしてカウントされます。

### 📧 Original Message Storage Settings {#original-message-storage-settings}

デフォルトでは、アーカイブはメールの解析された部分のみを保持するため、エクスポートされた `.eml` は再構成されたものになります。転送エンコーディング、MIME境界、およびヘッダーの折り返しは受信時のメッセージとは異なり、DKIM署名も検証できなくなります。`RawMessages:Enabled` を使用すると、完全なオリジナルも保存されます。

| Parameter | Default | Purpose |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | 新しくアーカイブされたすべてのメールのオリジナルを保存します。 |
| `RawMessages__Storage` | `postgres` | バイトの保存先: `postgres` または `filesystem`。 |
| `RawMessages__MaxMessageSizeMb` | `50` | 大きなメッセージはオリジナルなしでアーカイブされます。 `0` = 無制限。 |
| `RawMessages__IncludeGraph` | `true` | Microsoft 365 (Graph) アカウントについてもオリジナルを保存します。 |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | メッセージファイルのディレクトリ。Docker でマウントされたボリュームを指している必要があります。 |
| `RawMessages__FileSystem__ShardDepth` | `2` | メール ID が分散されるディレクトリの階層。 |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | すべてのダウンロード時に SHA-256 を検証します。 |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | どのメールからも参照されていないメッセージファイルを削除します。 |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | そのスウィープ（一掃）を実行する頻度。 |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | これより新しいファイルは削除されません。 |

!!! warning "Filesystem backend needs a mounted volume"
    `RawMessages__Storage=filesystem` を使用すると、メッセージファイルはデータベースの外に存在します。
    バインドマウントがない場合、ファイルはコンテナの書き込み可能レイヤーに置かれ、
    データベースの行は生存しますが、次回のコンテナ再作成時に失われます。
    ディレクトリをデータベースのダンプと一緒にバックアップしてください。

詳細な説明については **[Original Message Storage](RawMessages.md)** を参照してください：オリジナルが提供される場所、ストレージバックエンド、`--migrate-raw-storage` を使用して既存のメッセージをそれらの間で移動する方法、およびバックアップすべき内容について説明しています。

### 📈 Monitoring Settings {#monitoring-settings}

| Parameter | Default | Purpose |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | これより古い有効なアカウントは `/api/v1/health` レポート `Degraded` を生成します。`0` でチェックを無効にします。 |
| `Metrics__Enabled` | `false` | Prometheus メトリクスエンドポイントとそのリフレッシュループを有効にします。 |
| `Metrics__RefreshMinutes` | `5` | メトリクススナップショットが再構築される頻度。1未満の値は1に引き上げられます。 |

ヘルスおよびメトリクスエンドポイントには、admin API key と `Api__Enabled=true` が必要です。エンドポイント、メトリクス名、Prometheus の設定、および Docker のヘルスチェックについては、**[Health and Metrics](Monitoring.md)** を参照してください。

### 📤 BatchRestore 設定 {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: 非同期処理をトリガーするメールの数。
- `BatchRestore__MaxSyncEmails`: 同期処理におけるメールの最大数。
- `BatchRestore__MaxAsyncEmails`: 非同期処理におけるメールの最大数。
- `BatchRestore__SessionTimeoutMinutes`: バッチリストアのセッションタイムアウト（分単位）。
- `BatchRestore__DefaultBatchSize`: メール操作のデフォルトのバッチサイズ。

### 🏢 Tenant Management Settings {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: 1回の Tenant Management 操作で追加できるメールボックスの最大数です。デフォルトは `1000` です。非常に大規模なテナントの場合はこの値を増やし、誤った大量インポートを防ぐ場合は値を下げてください。制限を超えた場合、操作はバリデーションエラーとして拒否され、アカウントは作成されません。詳細は [M365 Tenant Import Guide](M365TenantImport.md) を参照してください。

### 🐄 Mailcow Settings {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: 1回のMailcowアクティベーション実行でプロビジョニング可能なメールボックスの最大数です。デフォルトは`1000`です。誤った大量プロビジョニングを防ぐには値を下げ、大規模なインスタンスの場合は値を上げてください。制限を超えた場合、アカウントは作成されません。詳細は [Mailcow Instances](Mailcow.md) を参照してください。
- Mailcowの統合には、さらに `Security__CredentialEncryptionKey` が必要です（[Security Settings](#-security-settings) を参照）。認証情報の暗号化が無効化されている間は、インスタンスへの接続およびメールボックスのアクティベーションはどちらも拒否されるため、APIキーや生成されたアプリパスワードがプレーンテキストとして保存されることはありません。
- Mailcow HTTPS API、およびTLS経由のIMAPホスト（通常はポート `993`）へのアウトバウンドアクセスが必要です。

### 📦 BatchOperation 設定 {#batchoperation-settings}
- `BatchOperation__BatchSize`: メール操作のバッチサイズ。
- `BatchOperation__PauseBetweenEmailsMs`: 個々のメール間の休止時間（ミリ秒単位）。
- `BatchOperation__PauseBetweenBatchesMs`: バッチ間の休止時間（ミリ秒単位）。

### 📤 Offload Settings {#offload-settings}
[date-windowed offload](Offload.md) の設定です。すべてのデフォルト値は、この機能が存在する前のアプリケーションの動作を再現するため、このセクションを設定しないインストール環境には影響を与えません。
- `Offload__MaxConcurrentJobs`: 同時に実行可能なリストアまたはオフロードジョブの数。デフォルトは `1` であり、ジョブキューが従来使用してきた厳密なシリアル処理を維持します。この値に関わらず、ターゲットメールボックスごとに実行されるジョブは最大1つです。
- `Offload__PrefetchMaxMessages`: 重複検出のためにターゲットメールボックスからインデックスを作成するメッセージ数の上限。デフォルトは `500000` です。この値を超えると、チェックは単一のフォルダに絞り込まれ、スコープが縮小されたことがログに記録されます。
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: オフロードされないソースフォルダ。名前の変更前に照合され、サブフォルダも対象となります。デフォルトでは空です。
- `Offload__FolderRenameMap__<SourceFolder>`: ソースフォルダパスの先頭セグメントを書き換えます（例: `Offload__FolderRenameMap__Sent Items=Sent`）。デフォルトでは空です。
- `Offload__MarkAsSeen`: 追加されたメールを既読としてフラグを立てるかどうか。デフォルトは `true` で、既存のリストア動作と一致します。

どちらのフォルダ設定も、意図的に空の状態で提供されています。要求なしにフォルダを書き換えたり削除したりすることは、すでにリストアパスを使用しているユーザーを驚かせてしまうためです。したがって、設定なしで実行した場合は、スパムフォルダを含めすべてが移行され、ターゲット自身のフォルダの隣に2つ目の特殊フォルダセットが作成されます。

### 📊 Bandwidth Tracking Settings {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: IMAPのレート制限への対応として、帯域幅トラッキングを有効または無効にします (true/false)。デフォルトは `false` です。有効にすると、システムはアカウントごとの帯域幅使用量を追跡し、プロバイダーの制限に達したときに同期を一時停止できます。詳細については [Rate Limit Handling](RateLimitHandling.md) を参照してください。
- `BandwidthTracking__DailyLimitMb`: アカウントあたりの1日あたりのダウンロード制限（メガバイト単位）。デフォルトは `25000` (25 GB) です。帯域幅制限のあるプロバイダーの場合は、そのレート制限に合わせて設定してください（例：1日の制限が約2500 MBのプロバイダーの場合は `2500`）。この制限に達すると、システムは同期を一時停止します。
- `BandwidthTracking__WarningThresholdPercent`: 警告メッセージがログに記録される、1日の制限に対するパーセンテージ。デフォルトは `80` です。帯域幅の使用量がこのパーセンテージに達すると、制限への接近を監視するために警告がログに記録されます。
- `BandwidthTracking__PauseHoursOnLimit`: 1日の制限に達した際に、同期を一時停止する時間（時間単位）。デフォルトは `24` です。この期間が経過すると、制限フラグは自動的にクリアされ、同期が再開されます。
- `BandwidthTracking__TrackUploadBytes`: アップロードの帯域幅も追跡するかどうか (true/false)。デフォルトは `false` です。ほとんどのIMAPプロバイダーはダウンロードのみを制限するため、通常は必要ありません。

### 🎯 Selection Settings {#selection-settings}
- `Selection__MaxSelectableEmails`: 一度に選択できるメールの最大数。

### 👁️ 表示設定 (プライバシー & ディスプレイ) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: プライバシーとトラッキング防止のためのデフォルトのメール表示モードを制御します (true/false)。デフォルトは `false` です。
  - `true` に設定されている場合: メールはデフォルトでプレーンテキスト表示で開き、トラッキングピクセル、外部画像、およびウェブビーコンの自動読み込みを防止します。これは、メールのトラッキングとプライバシーを懸念するユーザーに推奨されます。
  - `false` に設定されている場合: メールはデフォルトで HTML 表示で開き、画像やスタイリングを含むフォーマットされたメールを表示します。
  - ユーザーは、メール詳細ページのボタンを使用して HTML 表示とプレーンテキスト表示を切り替えることができます（両方のフォーマットが利用可能な場合にのみ表示されます）。
  - 「Full View」リンクは、現在選択されている表示モードに従います。

- `View__BlockExternalResources`: トラッキングを防止しプライバシーを向上させるため、HTMLメール表示において外部リソース（リモート画像、外部 CSS、外部スクリプト、ウェブフォントなど）をブロックします (true/false)。デフォルトは `false` です。
  - `true` に設定されている場合: HTMLメールを表示する際、外部リソースはフィルタリングされます。インラインコンテンツとデータ URI（`cid:` 参照によるインライン添付ファイルを含む）のみが表示されます。
  - `false` に設定されている場合: HTMLメールは、すべての元の外部リソースとともに表示されます。
  - **重要**: この設定はメールの **表示** にのみ影響します。アーカイブされたメールは、すべての元のコンテンツが保持された状態で、変更されることなくデータベースに完全に保存されます。
  - ブロックされるリソース:
    - リモート画像（トラッキングピクセル、サーバーにホストされている外部画像）
    - 外部 CSS スタイルシート
    - @font-face による外部フォント
    - @import による外部 CSS インポート
    - 外部背景画像
  - 許可されるリソース:
    - data: URIs として埋め込まれたインライン画像
    - cid: URIs を介して参照されるインライン添付ファイル
    - インライン CSS スタイルおよび style タグ
  - この設定は `DefaultToPlainText` とは独立して動作し、HTMLメールを閲覧する際の追加のプライバシー保護レイヤーを提供します。

### 📊 Dashboard Settings {#dashboard-settings}
- `Dashboard__ShowDirectionSplits`: カウンターカードに受信・送信の内訳を表示するか、およびアカウントカードに一意のドメイン数を含めるかどうかを設定します。デフォルトは `true` です。`false` に設定すると、これらの数値は計算されて非表示になるのではなく、計算自体が行われなくなります。つまり、カウンターは単純なカウント値を読み取り、添付ファイルのカウントは、これらのパーツが追加する唯一のクエリである、対象のメールへの結合を停止します。
- `Dashboard__SelectablePeriods`: ダッシュボードのチャートに、解像度（resolution）、期間（period）、およびそれらを移動するための矢印を表示するかどうかを設定します。デフォルトは `true` です。`false` に設定すると、チャートは過去12ヶ月間を月単位で、およびアーカイブ全体の送信者を表示します。チャートのエンドポイントは、存在しないかのように応答し、最も古い送信日は検索されません。
- `Dashboard__CacheSeconds`: 計算されたダッシュボード統計（受信・送信の内訳を含む合計、アカウントごとのカウント、チャートシリーズ、トップ送信者、最近のメール、データベースサイズ）を、サーバーのインメモリキャッシュに保持する期間を設定します。デフォルトは `60` 秒です。`0` に設定すると、キャッシュを無効にし、常に統計を再計算します。解像度と期間の各チャート選択は個別にキャッシュされるため、選択を切り替えるダッシュボードは、選択ごとに1つのエントリを保持します。値が大きいほど、数値が古くなる可能性がありますが、大規模な環境でのデータベース負荷を軽減できます。同期ステータスバッジ、ストレージ値、および最近のメールリストは常にライブで取得されるため、このキャッシュの影響を受けません。
- `Dashboard__RefreshIntervalMinutes`: `DashboardStatsRefreshService` バックグラウンドサービスが、高負荷なダッシュボード集計（受信・送信の内訳を含む合計カウント、アカウントごとのカウント、アカウントドメイン数、トップ送信者を含むデフォルトのチャートシリーズ、データベースサイズ）を `DashboardStatsCache` データベーステーブルにどの頻度で再計算するかを設定します。数百万通のメールがあるインストール環境では、ダッシュボードはリクエストパス内でアーカイブ全体を集計する代わりに、これらの準備された値を読み取るため、初回ロードの約10秒がミリ秒単位に短縮されます。チャートのエンドポイントは、デフォルトの選択（1年間の月次、受信送信者、現在の期間）に対して同じ行から応答するため、そこに戻ると初回描画時の数値が表示されます。それ以外のすべての選択は、リクエスト時に計算され、上記のインメモリキャッシュに保持されます。デフォルトは `15` 分です。`0` に設定すると、バックグラウンド更新が無効になります。その場合、ダッシュボードは（上記のインメモリキャッシュを適用しつつ）再び統計をライブで計算します。表示される値は、実際のアーカイブの状態から設定された間隔分遅れる可能性があります。現在の値と異なる `ShowDirectionSplits` または `SelectablePeriods` 値で計算された行は使用されず、ダッシュボードは次の更新までライブで計算を行います。

### 🔄 Jobs Settings {#jobs-settings}
- `Jobs__RefreshSeconds`: ブラウザのタブで開いている間、バックグラウンドジョブのページが自動的にリロードされる頻度です。デフォルトは `30` 秒です。自動リロードをオフにして手動で更新する場合は `0` に設定してください。リロードのたびに8つのジョブソースすべてからページを再構築するため、多数のアカウントを持つ環境では、リロードの間隔を長く設定することで、開いているすべてのタブによる負荷を軽減できます。

### 🗃️ Npgsql Settings {#npgsql-settings}
- `Npgsql__CommandTimeout`: データベースコマンドのタイムアウト（秒単位）。

### 📥 Upload Settings {#upload-settings}
- `Upload__MaxFileSizeGB`: アップロードの最大ファイルサイズ（GB）。
- `Upload__KeepAliveTimeoutHours`: アップロードのキープアライブ・タイムアウト（時間）。
- `Upload__RequestHeadersTimeoutHours`: リクエストヘッダーのタイムアウト（時間）。

### 📂 Local Import Settings {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, etc.: CLI import commands (`--import-mbox`, `--import-eml`) がファイルを読み取ることを許可するローカルディレクトリのホワイトリストです。各エントリはコンテナ内のパスです。Docker volumes を使用して、インポートするファイルをこれらのディレクトリのいずれかにマウントする必要があります。デフォルト: `/data/import` (フォールバックとして `/app/uploads` が自動的に設定されます)。
  - `docker exec` を使用してインポートコマンドを実行する場合、`--file` を介して提供されるファイルパスは、これらの許可されたパスのいずれかである必要があります。
  - このセキュリティ対策により、CLI コマンドからの任意のファイルシステムへのアクセスを防止します。
  - 異なるインポートソースに対して、複数のパスを設定できます。
  - 詳細な使用方法については [CLI commands](CLI.md) を参照してください。

### 📄 CSV Import Settings {#csv-import-settings}
- `CsvImport__MaxRows`: 1回のバルクインポートで処理されるCSV行数（メールボックス）の最大数です。デフォルトは `5000` です。大規模なデプロイメントの場合はこの値を増やし、単一のインポート実行によるデータベース負荷を制限したい場合は値を下げてください。
- `CsvImport__MaxFileSizeBytes`: アップロード可能なCSVファイルの最大サイズ（バイト単位）です。デフォルトは `10000000` (10 MB) です。必要に応じて、アップロード制限に合わせてこの値を調整してください。
- CSVによるIMAPアカウントのバルクインポートに関する詳細な使用手順については、[Account Import Guide](Account-Import.md) を参照してください。

### 📤 Audit Export Settings {#audit-export-settings}
監査データエクスポートページの各設定（管理者のみ、Logsページからアクセス可能）。エクスポートにより、外部監査ツール向けに既存のアーカイブから表形式の大量データ（INDEX.XML + CSV tables + DTD）を含むZIPパッケージが生成されます。使用方法の詳細については、[Audit Data Export Guide](AuditExport.md)を参照してください。
- `AuditExport__DataSupplierName`: エクスポートされたインデックスファイル内で組織を識別するための「Data supplier name」フォームフィールドのデフォルト値。デフォルト: 空。
- `AuditExport__DataSupplierLocation`: 「Data supplier location」フォームフィールド（例：会社の所在地）のデフォルト値。デフォルト: 空。
- `AuditExport__Comment`: エクスポートされたインデックスファイルに自由形式のテキストとして含まれる「Comment」フォームフィールドのデフォルト値。デフォルト: 空。
- `AuditExport__OutputDirectory`: 生成されたZIPファイルが保存されるディレクトリ。相対パスはアプリのコンテンツルートに対して解決されます。デフォルト: `exports/audit`。
- `AuditExport__RetentionDays`: 完了したエクスポートファイルが日次のクリーンアップによって削除されるまでの日数。デフォルト: `30`。
- `AuditExport__MaxRangeYears`: エクスポート期間の開始日と終了日の間に許容される最大期間。デフォルト: `10`。
- フォームフィールドにはこれらのデフォルト値が事前入力されますが、エクスポートごとに編集可能です。
- すべてのエクスポートは、アクセスログに開始エントリと結果エントリを書き込みます（タイプは "Audit Data Export"）。そのため、DBスキーマの変更なしに履歴の改ざん防止が可能です。

### 🔒 Deletion Policy Settings {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: アーカイブされたメールの手動削除を許可するかどうかを制御します (true/false)。デフォルトは `true` です。`false` に設定されている場合：
  - 起動時にデータベースのコンプライアンストリガーを介して、すべてのアーカイブ済みメールがロック (`IsLocked = true`) され、データベースレベルでのいかなる変更や削除も防止されます。
  - アプリケーションレベルで手動削除（単一および一括）がブロックされ、エラーメッセージが表示されます。
  - 新しくインポートされたメールもロックされるように、カラムのデフォルト値が調整されます。
  - 監査可能性を確保するため、現在のポリシー状態が起動のたびに AccessLogs テーブルにログ記録されます（Logs ページに "Deletion Policy" エントリとして表示されます）。
  - ローカル保持期間による削除は例外です：設定された保持期間に該当するメールは、引き続き削除されます（保持プロセス内の削除直前にアンロックされます）。
- `DeletionPolicy__MaxBulkDeleteEmails`: フィルターベースの一括削除（*Delete all matches*）の上限値です。デフォルトは `100000` です。より多くのメールに一致するフィルターは、条件を絞り込むよう求めるメッセージとともに拒否されます。ジョブがサイレントに切り捨てられることはありません。保持ポリシーによってロックされているメールはスキップされ、アンロックされることなくジョブステータスに報告されます。ジョブは開始時にこの制限と `DeletionPolicy__DeletionAllowed` を再確認するため、ポリシー変更前にキューに追加されたジョブは、古い設定で実行されるのではなく停止します。
- **Immutability protection:** `IsLocked = true` のとき、データベースのコンプライアンストリガー (`prevent_locked_email_changes`) は、メール行への「いかなる」変更もブロックします。特定のフィールドリストだけでなく、すべてのカラムが保護されます。唯一の例外となるカラムは、`IsLocked` 自体（保持期間による削除のためのアンロックおよび起動時のポリシー適用を可能にするため）と `FolderName` （サーバー側でメールが移動された際に IMAP 同期がフォルダを更新できるようにするため）です。この保護はカラムに依存せず（JSONB ベースの比較）、将来のスキーマ追加も自動的にカバーします。

### 🕐 TimeZone Settings {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: UI上でメールのタイムスタンプを表示するために使用されるタイムゾーンです。IANAタイムゾーン識別子（例: "Europe/Berlin", "Asia/Tokyo"）を使用します。デフォルトは後方互換性のために "Etc/UCT" に設定されています。メールをインポートする際、タイムスタンプは表示目的のためにこのタイムゾーンに変換されます。

### 🎉 ReleaseNotes Settings (Version Update Splash Screen) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: このインスタンスが報告するバージョン文字列です。GitHubのリリース・タグと照合され、より新しいリリースが存在するかどうかを判断します。空の場合、変更履歴はコンパイルされたアセンブリのバージョンにフォールバックされ、アップデートのヒントは表示されません。
- `ReleaseNotes__Enabled`: バージョンアップデートの起動画面（splash screen）を有効または無効にします (true/false)。デフォルトは `true` です。有効な場合、管理者はアプリケーションのアップデート後に、現在のバージョンについてGitHub Releasesから取得されたリリースノートを表示する、一度限りの変更履歴モーダルを確認できます。各管理者はモーダルを閉じることができ、新しいバージョンが出た場合にのみ再度表示されます。アップデートのヒントを含め、この機能を完全に無効にするには `false` に設定してください。
- `ReleaseNotes__UpdateHintEnabled`: インストールされているバージョンよりも新しい安定版リリースがGitHubで公開された際、管理者に右下隅に小さなトーストを表示します (true/false)。デフォルトは `true` です。トーストはそのリリースへのリンクを含み、ブラウザ内でバージョンごとに閉じられます。ドラフトおよびプリリリースは無視されます。これには `ReleaseNotes__AppVersion` が必要です。これがない場合、ヒントは表示されません。
- `ReleaseNotes__UpdateCheckIntervalHours`: GitHubのリリースリストをリフレッシュする頻度（時間単位）です。デフォルトは `6` です。間隔を短くすると、長時間稼働しているインスタンスにおいて新しいリリースがより早く表示されますが、間隔を長くするとGitHub APIの呼び出し回数が減少します。最小値は `1` です。

### 🔧 Database Maintenance Settings {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: 自動的な日次データベースメンテナンスを有効または無効にします (true/false)。デフォルトは `false` です。有効にすると、システムはデータベースのパフォーマンスを最適化し、肥大化を防ぐために VACUUM ANALYZE 操作を自動的に実行します。詳細については [Database Maintenance Guide](DatabaseMaintenance.md) を参照してください。
- `DatabaseMaintenance__DailyExecutionTime`: データベースメンテナンスを実行する時刻を24時間形式 (HH:mm) で指定します。デフォルトは `02:00` です。システムの活動が少ない時間帯を選択してください。
- `DatabaseMaintenance__TimeoutMinutes`: メンテナンス操作に許可される最大時間（分単位）です。デフォルトは `30` です。大規模なデータベースの場合は、この値を増やしてください。

### 🧬 Attachment Deduplication Settings {#attachment-deduplication-settings}
Attachment deduplicationは、すべてのユニークな添付ファイルペイロードを一度だけ保存し（SHA-256によるコンテンツアドレッシング）、これは**常に有効化されているコア機能**です。意図的にオン/オフのスイッチは設けられていません。以下のバッチ/スケジューリングパラメータのみを調整できます。詳細については[Attachment Deduplication Guide](AttachmentDeduplication.md)を参照してください。

- `AttachmentDeduplication__BatchSize`: 既存データの初回バックグラウンド移行中に、1回のトランザクションあたりに移行される既存の添付ファイルの数。デフォルトは`200`です。値が大きいほど移行は速くなりますが、バッチあたりのメモリ使用量およびDB負荷が増加します。
- `AttachmentDeduplication__DelayBetweenBatchesMs`: 負荷の高いシステムにおいてデータベース負荷を抑制するための、移行バッチ間のオプションの休止時間（ミリ秒単位）。デフォルトは`0`（休止なし）です。
- `AttachmentDeduplication__StartupDelaySeconds`: スキーマ移行の完了時間を確保するため、アプリケーション起動後にバックグラウンド移行が開始されるまでの遅延時間（秒単位）。デフォルトは`20`です。
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: どのメールからも参照されなくなった添付ファイルペイロードを削除する、常時実行されるガベージコレクションの間隔（時間単位）。デフォルトは`12`です。これは`DatabaseMaintenance__Enabled`とは独立して実行されます。
- `AttachmentDeduplication__CommandTimeoutSeconds`: 移行バッチ操作（SHA-256ハッシュ化を伴うINSERTおよびUPDATE）のデータベースコマンドタイムアウト（秒単位）。デフォルトは`300`（5分）です。非常に大規模なデータベースや添付ファイルを扱う場合はこの値を増やし、より迅速な失敗検知を行いたい場合は値を下げてください。バッチがタイムアウトした場合、サービスはバッチサイズを半分にして自動的にリトライします。

### 💾 アカウントストレージ設定 {#account-storage-settings}

アカウントごとのストレージ表示は、Dashboardの「Account Overview」テーブルおよびMailAccountsの「Show All」テーブルにおいて、各アカウントのデータベースストレージ使用量（すべてのメールフィールド + 添付ファイル）を示します。autarkバックグラウンドサービス（`AccountStorageRefreshService`）が、PostgreSQLの`pg_column_size`をカラムごとに合算する（メール本文を読み取ることなく、メールのすべてのフィールドをカバーします）ことで値を計算し、それらを`AccountStorageCache`テーブルにキャッシュします。このサービスは`DatabaseMaintenance__Enabled`とは独立して動作します。

- `AccountStorage__Enabled`: ストレージ更新サービスを有効または無効にします (true/false)。デフォルトは`true`です。有効な場合、サービスは起動時に保留中のアカウントの再開可能なバックフィル（`AccountStorageBackfillState`によりクラッシュセーフ）を実行し、その後は毎日フルリフレッシュを実行します。無効な場合、ストレージ値はメールが同期、インポート、または削除されたときにのみ更新されますが、表示される値は時間の経過とともに古くなる可能性があります。
- `AccountStorage__DailyExecutionTime`: すべてのアカウントの毎日のフルリフレッシュを行う時刻（24時間形式 `HH:mm`）。デフォルトは`02:30`です。システムの活動が少ない時間帯を選択してください。
- `AccountStorage__BackfillDelayMs`: 起動時の初期バックフィルにおけるアカウント間の遅延（ミリ秒単位）。デフォルトは`5000`です。値を小さくするとバックフィルは高速化されますが、データベースの負荷が増加します。非常に大規模なアーカイブの場合は、データベースへの過負荷を避けるためにこの値を大きくしてください。
- `AccountStorage__RefreshBatchDelayMs`: 毎日のフルリフレッシュにおけるアカウント間の遅延（ミリ秒単位）。デフォルトは`1000`です。値を小さくするとリフレッシュは高速化されますが、データベースの負荷が増加します。非常に大規模なアーカイブの場合は、この値を大きくしてください。
- `AccountStorage__CommandTimeoutSeconds`: アカウントごとのストレージ計算（アカウントの全行に対する`pg_column_size`）のデータベースコマンドタイムアウト（秒単位）。デフォルトは`300`（5分）です。計算ではTOASTデータを含むすべてのメール行を読み取るため、非常に大規模なアーカイブ（25万通以上のメール）では1分以上かかることがあります。以前の30秒というNpgsqlのデフォルト値では、リフレッシュがサイレントに失敗し、ダッシュボードに古い値が表示され続ける原因となっていました。アーカイブの計算で依然としてタイムアウトが発生する場合は、この値を大きくしてください。

> 💡 **注意**: ストレージ値は、各メールの同期、インポート、または保持ポリシーによる削除の直後に更新されるため、毎日のリフレッシュが行われなくても表示される値は最新の状態に保たれます。毎日のリフレッシュは、エッジケース（例：データベースへの直接的な変更）を捕捉するためのセーフティネットです。

### 📝 Logging Settings {#logging-settings}
- `Logging__LogLevel__Default`: アプリケーションのデフォルトのログレベルです。利用可能なレベルは、`Trace`、`Debug`、`Information`、`Warning`、`Error`、`Critical`、`None` です。デフォルトは `Information` です。
- `Logging__LogLevel__Microsoft_AspNetCore`: ASP.NET Core フレームワーク メッセージのログレベルです。デフォルトは `Warning` です。
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Entity Framework データベース コマンドのログレベルです。デフォルトは `Warning` です。

### 🛡️ Security Settings {#security-settings}
- `AllowedHosts`: アプリケーションがサービスを提供することを許可されるホスト名のセミコロン区切りのリストです。これにより、HTTP Host header attacks を防ぐことができます。例: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`。**重要**: `*` はホストヘッダーの検証を無効にするため、本番環境では使用しないでください。

#### 🔐 Credential Encryption at Rest {#credential-encryption-at-rest}
メールアカウントの資格情報（IMAP password、M365 client secret、および MSA OAuth tokens）は、データベース内で暗号化できます。暗号化キーはデータベースの**外部**（環境変数 / Docker secret）に保持されるため、データベースのダンプだけでは資格情報を読み取ることはできません。

- `Security__CredentialEncryptionKey`: Base64 エンコードされた 256ビット (32 バイト) のキー暗号化キーです。以下のコマンドで生成してください:
  ```bash
  openssl rand -base64 32
  ```
  値を環境変数として設定してください（推奨: compose ファイルまたはコンテナ環境内の `Security__CredentialEncryptionKey`）。本アプリケーションは `_FILE` スタイルの Docker secrets をサポートしていません。Docker secret を使用する場合は、それをマウントし、entrypoint 内でその内容を環境変数にロードしてください。決して `appsettings.json` にキーをコミットしないでください。キーが設定されると、資格情報は書き込み時に暗号化され、バックグラウンドサービスが既存のプレーンテキストの資格情報を一度だけ暗号化します。
- `Security__EncryptAccountCredentials`: 新規または更新された資格情報値の暗号化を有効または無効にします (true/false)。デフォルトは `true` です。キーが設定されていない場合、資格情報はプレーンテキストとして保存され、起動時に警告がログに記録されます。既存のインストール環境は、キーが提供されるまで影響を受けません。
- `Security__CredentialEncryptionBackfill`: バックグラウンドサービスが起動時に既存のプレーンテキストの資格情報を暗号化するかどうか (true/false)。デフォルトは `true` です。設定されたキーがない場合は何もしません。
- `Security__CredentialEncryptionKeyPrevious`: オプションの以前のキーです。キーのローテーション（key rotation）のために、それを使用して暗号化された値を復号する場合にのみ使用されます。新しい値は常に `Security__CredentialEncryptionKey` で暗号化されます。

  > ⚠️ **Key rotation**: ローテーション後は、**すべての資格情報が書き換えられるまで**（例: アカウントの編集や自動 OAuth token refresh による） `Security__CredentialEncryptionKeyPrevious` を設定したままにしてください。古いキーで暗号化された既存の値は、新しいキーで自動的に再暗号化されることはありません。以前のキーを削除すると、まだそのキーで暗号化されている資格情報は読み取り不能になり、再度入力する必要があります。設定されている間は、現在のキーと同様に安全に保管する必要があります。

> ⚠️ **Keep the key safe**: キーがないと、暗号化された資格情報を復元することはできず、再度入力する必要があります。データベースのバックアップと一緒に（ただし、データベースとは別に）バックアップを取っておいてください。
>
> 💡 **Existing installations**: 安全にアップグレードできます。 `Security__CredentialEncryptionKey` が設定されていない限り、何も変更されません。準備が整ったらキーを設定してください。既存のプレーンテキストの資格情報は読み取り可能なまま維持され、バックグラウンドで暗号化されます。

### 🔐 OIDC Configuration {#oidc-configuration}

OpenID Connect認証の詳細なセットアップ手順については、[OIDC Implementation Guide](OIDC_Implementation.md)を参照してください。

#### Basic OIDC Settings {#basic-oidc-settings}
- `OAuth__Enabled`: OIDC認証を有効または無効にする (true/false)
- `OAuth__Authority`: OpenID ConnectのオーソリティURL (例: Azure ADの場合は https://sts.windows.net/{TENANT-ID}/)
- `OAuth__ClientId`: IDプロバイダーから割り当てられた client ID
- `OAuth__ClientSecret`: IDプロバイダーから割り当てられた client secret
- `OAuth__DisplayName`: OIDCログインボタンおよび自動リダイレクトページに表示される任意の表示名 (例: `PocketID SSO`)。省略した場合、汎用的な "Login with OAuth" ラベルが使用されます。
- `OAuth__ClientScopes__0`: IDプロバイダーに要求する最初の scope (openid)
- `OAuth__ClientScopes__1`: IDプロバイダーに要求する2番目の scope (profile)
- `OAuth__ClientScopes__2`: IDプロバイダーに要求する3番目の scope (email)
- `OAuth__ResponseMode`: 認可リクエストの任意の response mode (`query`、`fragment`、または `form_post`)。デフォルトは ASP.NET Core のデフォルトである `form_post` です。Synology SSO Server のように `form_post` をサポートしていないプロバイダーの場合は、`query` に設定してください。詳細は [Provider Compatibility](OIDC_Implementation.md#provider-compatibility) を参照してください。
- `OAuth__AutoApproveUsers`: 管理者による手動承認を必要とせず、新しいOIDCユーザーを自動的に承認する (true/false)。デフォルトは `false` です。

#### User Provisioning Settings {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: 管理者による手動承認を必要とせず、新しいOIDCユーザーを自動的に承認する (true/false)。デフォルトは `false` です。有効にすると、OIDCプロバイダー経由で認証されたユーザーは即座にアクティブ化され、アプリケーションにアクセスできるようになります。無効（デフォルト）の場合、新しいOIDCユーザーは非アクティブ状態で作成され、管理者による手動の有効化が必要になります。詳細については [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users) を参照してください。
- `OAuth__AdminEmails__0`、`OAuth__AdminEmails__1` など: 管理者として自動的にプロビジョニングされるメールアドレス。これらのメールアドレスを持つユーザーは、最初のOAuthログイン時に通常の承認プロセスをバイパスして、アクティブな管理者として作成されます。メールアドレスの一致は、大文字と小文字を区別しません。

#### Passwordless Login Settings {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: ユーザー名/パスワードによるログインを無効にする (true/false)。デフォルトは `false` です。有効にすると、パスワードフォームが非表示になり、**かつ** サーバーがすべてのパスワード POST を拒否するため、OAuthログインのみが利用可能になります。これには `OAuth__Enabled=true` が必要です。OAuthが無効な場合、この設定は効果がありません。
- `OAuth__AutoRedirect`: ユーザーをOAuthプロバイダーに自動的にリダイレクトする (true/false)。デフォルトは `false` です。これには `OAuth__DisablePasswordLogin` が `true` である必要があります。リダイレクトされる前に、ユーザーには短いロード画面が表示されます。

#### Example: Full OIDC-First Configuration {#example-full-oidc-first-configuration}
```yaml
environment:
  - OAuth__Enabled=true
  - OAuth__Authority=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0
  - OAuth__ClientId=your-client-id
  - OAuth__ClientSecret=your-client-secret
  - OAuth__DisplayName=PocketID SSO
  - OAuth__ClientScopes__0=openid
  - OAuth__ClientScopes__1=profile
  - OAuth__ClientScopes__2=email
  - OAuth__DisablePasswordLogin=true
  - OAuth__AutoRedirect=true
  - OAuth__AutoApproveUsers=true
  - OAuth__AdminEmails__0=admin@example.com
  - OAuth__AdminEmails__1=manager@example.com
```

## 🔐 Kestrel HTTPS Configuration (Optional) {#kestrel-https-configuration-optional}

このアプリケーションはリバースプロキシ経由でHTTPSを使用してアクセスすることを想定していますが、KestrelウェブサーバーをSSL/TLS証明書を使用するように構成することも可能です。これにより、リバースプロキシとアプリケーションコンテナ間のエンドツーエンドの暗号化が提供されます。

### Configuration Steps {#configuration-steps}

1. **PFX形式のSSL証明書を生成または取得します** (例: `localhost.pfx`)

2. **`mailarchive-app`サービス用に、以下の環境変数を`docker-compose.yml`に追加します**:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **`mailarchive-app`サービス内のポートマッピングを更新します**:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **証明書用のボリュームマッピングを追加します**:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **証明書ファイル** (例: `localhost.pfx`) を、ホストシステムの `./certs` ディレクトリに配置します。

### Environment Variable Explanations {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: HTTPエンドポイントURL (デフォルト: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: HTTPSエンドポイントURL (デフォルト: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: コンテナ内のPFX証明書ファイルへのパス
- `Kestrel__Endpoints__Https__Certificate__Password`: PFX証明書ファイルのパスワード

> 💡 **Note**: この構成はオプションです。HTTPSを備えたリバースプロキシを使用している場合（推奨）、リバースプロキシとアプリケーション間の通信はHTTPのままでも問題ありません。ただし、機密性の高い環境で最大限のセキュリティを確保するには、通信経路全体を暗号化するためにKestrelでもHTTPSを有効にすることを検討してください。

## 🔑 本番環境における Secrets Management {#secrets-management-for-production}

データベースのパスワード、管理者資格情報、OAuth クライアントシークレットなどの機密データを `docker-compose.yml` に直接ハードコードすることは、特にそのファイルがバージョン管理にチェックインされる場合、セキュリティ上のリスクとなります。推奨されるアプローチは、これらの値を Docker Compose が自動的に読み込む `.env` ファイルに外部化することです。

### 📄 `.env` ファイル {#the-env-file}

`docker-compose.yml` と同じディレクトリに、以下の内容を含む `.env` ファイルを作成してください：

```env
# Database
POSTGRES_PASSWORD=YourSecureDBPassword123!

# Admin Account
AUTH_USERNAME=admin
AUTH_PASSWORD=YourSecureAdminPassword456!

# OIDC / OAuth Secrets
OAUTH_CLIENT_SECRET=YourOAuthClientSecret

# Credential Encryption at Rest (generate with: openssl rand -base64 32)
CREDENTIAL_ENCRYPTION_KEY=YourBase64Encoded32ByteKey

# Kestrel HTTPS (optional)
KESTREL_CERT_PASSWORD=YourCertPassword
```

**重要**: Docker Compose は同じディレクトリにある `.env` ファイルを自動的に読み込みます。手動で参照する**必要はありません**。`.env` で定義されたすべての変数は、`${VARIABLE_NAME}` 構文を介して `docker-compose.yml` で利用可能です。

### 🐳 適合させた `docker-compose.yml` {#adapted-docker-composeyml}

インストール手順の `docker-compose.yml` は、ハードコードされたシークレットの代わりにプレースホルダーを使用するように適合させる必要があります：

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=${POSTGRES_PASSWORD}

      # Authentication Settings
      - Authentication__Username=${AUTH_USERNAME}
      - Authentication__Password=${AUTH_PASSWORD}
      # ... other settings remain unchanged ...

      # OIDC Configuration
      - OAuth__ClientSecret=${OAUTH_CLIENT_SECRET}

      # Credential Encryption at Rest
      - Security__CredentialEncryptionKey=${CREDENTIAL_ENCRYPTION_KEY}
    # ... ports, volumes, networks ...

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    # ... volumes, networks ...
```

### 📋 管理者向け `.env.example` テンプレート {#envexample-template-for-administrators}

他の管理者のためのテンプレートとして機能する `.env.example` ファイルをリポジトリ内に作成します。これにはプレースホルダーの値とドキュメントが含まれますが、**実際のシークレットは含まれません**：

```env
# ============================================
# Mail Archiver - Environment Configuration
# ============================================
# Copy this file to .env and fill in your values.
# Never commit the actual .env file to version control!

# --- PostgreSQL ---
POSTGRES_PASSWORD=change_me_db_password

# --- Admin Account ---
AUTH_USERNAME=admin
AUTH_PASSWORD=change_me_admin_password

# --- OIDC / OAuth ---
OAUTH_CLIENT_SECRET=change_me_client_secret

# --- Credential Encryption at Rest ---
# Generate with: openssl rand -base64 32
CREDENTIAL_ENCRYPTION_KEY=change_me_base64_32_byte_key

#...and so on
```

### ✅ ベストプラクティス {#best-practices}

- **`.env.example` をテンプレートとして使用する**
- **厳格なファイル権限を設定する** — `.env` ファイルへのアクセスを制限します：
  ```bash
  chmod 600 .env
  ```
- **強力なパスワードを使用する** — 文字、数字、特殊文字を組み合わせた、長くランダムなパスワード（少なくとも16文字）を生成してください。
- **`.env` ファイルへのアクセスを制限する** — Docker Compose を実行するユーザーのみが `.env` ファイルの読み取り権限を持つようにしてください。

## 🔒 セキュリティに関する注意点 {#security-notes}

- 強力なパスワードを使用し、デフォルトの認証情報を変更してください。パスワードは12文字以上とし、大文字、小文字、数字、および特殊文字を組み合わせる必要があります。一般的な単語や推測しやすい情報は避けてください。
- 本番環境では、リバースプロキシを使用したHTTPSの導入を検討してください。
- PostgreSQLデータベースの定期的なバックアップを推奨します。詳細なバックアップおよびリストアの手順については、[Backup and Restore Guide](BackupRestore.md)を参照してください。
