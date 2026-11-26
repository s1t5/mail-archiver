# 🛠️ Mail Archiver Setup Guide {#mail-archiver-setup-guide}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

このガイドでは、Docker Compose を使用して Mail Archiver アプリケーションをセットアップするための詳細な手順を提供します。

## 🛠️ Prerequisites {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 インストール手順 {#installation-steps}

1. システムに前提条件をインストールします。

2. 次の内容を含む `docker-compose.yml` ファイルを作成します：

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
      - RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds=300

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
      - ReleaseNotes__RequestTimeoutSeconds=10

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

6. オプションとして、アプリケーションログの冗長度を制御するために `Logging__LogLevel` 環境変数を設定します。利用可能なオプションについては、後述の「Logging Settings」セクションを参照してください。

7. アプリケーションへのアクセスを保護するために、https と認証を備えた任意の構成のリバースプロキシを設定してください。

> ⚠️ **注意**: アプリケーション自体は https による暗号化されたアクセスを提供しません！ リバースプロキシを介して設定する必要があります！

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
   - 「Email Accounts」セクションに移動します
   - 「New Account」をクリックします
   - サーバーの詳細と資格情報を入力します
   - 保存してアーカイブを開始します！
   - 必要に応じて、他のユーザーを作成し、アカウントを割り当てます。

## 📚 環境変数の説明 {#environment-variable-explanations}

### 🗄️ データベース接続 {#database-connection}
- `ConnectionStrings__DefaultConnection`: PostgreSQL データベースへの接続文字列。必要に応じて `Host`、`Database`、`Username`、および `Password` の値を変更してください。

### 🔐 Authentication Settings {#authentication-settings}

- `Authentication__Username`: 管理アカウントのユーザー名。
- `Authentication__Password`: 管理アカウントのパスワード。
- `Authentication__SessionTimeoutMinutes`: セッションタイムアウト（分単位）。これは非永続的なログインクッキーに適用されます。ログインフォームで **Remember Me** にチェックを入れたユーザーには、代わりに7日間有効な永続的クッキーが発行されます。詳細は [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox) を参照してください。
- `Authentication__CookieName`: 認証クッキーの名前。
- `Authentication__CookieSameSite`: 認証、セッション、および CSRF 保護クッキーの SameSite 属性を設定します。有効な値は以下の通りです：
  - `Strict` (デフォルト): 最大のセキュリティ。クッキーは same-site リクエストの場合にのみ送信されます。外部のリンク（例：他のウェブサイトからのリンクのクリック）からアプリケーションに移動する場合、既存のセッションが認識されないため、問題が発生する可能性があります。
  - `Lax`: リバースプロキシを使用する場合に推奨されます。クッキーはトップレベルのナビゲーションおよび same-site リクエストと共に送信されるため、POST リクエストに対する CSRF 保護を維持しつつ、ユーザーが外部リンクからアプリケーションへ遷移することを可能にします。
  - `None`: すべてのリクエストと共にクッキーが送信されます。HTTPS と `Secure` 属性が必要です。特定のクロスサイト要件があり、セキュリティ上の影響を理解している場合にのみ使用してください。

### 🔌 REST API Settings {#rest-api-settings}

オプションの読み取り専用 REST API は **デフォルトで無効** になっています。完全なリファレンスについては [REST API guide](API.md) を参照してください。
- `Api__Enabled`: 読み取り専用 REST API のマスタースイッチ (デフォルト `false`)。`false` のとき、すべての `/api/*` ルートは `404` を返します。
- `Api__AllowAttachmentDownloads`: API を介した添付ファイルのバイトデータのダウンロードを許可します (デフォルト `true`)。`false` のとき、添付ファイルエンドポイントは `403` を返します。
- `Api__AllowMessageDownloads`: API を介した完全なメッセージ（再構築された `/eml` とオリジナルの `/raw` の両方）のダウンロードを許可します (デフォルト `true`)。`false` のとき、両方のエンドポイントは `403` を返します。完全なメッセージには添付ファイルが含まれるため、このスイッチと `Api__AllowAttachmentDownloads` の **両方** が `true` である必要があります。どちらか一方をオフにすると、両方のエンドポイントが無効になります。
- `Api__EnableSwaggerUi`: `/apidocs` で Swagger UI を、`/apidocs/spec/v1.json` で OpenAPI ドキュメントを公開します (デフォルト `true`)。どちらもログイン済みのブラウザセッションが必要です。
- `Api__DefaultPageSize`: 指定がない場合のリストエンドポイントのデフォルトのページサイズ (デフォルト `20`)。
- `Api__MaxPageSize`: 許可される最大ページサイズ。これを超えるリクエストは制限されます (デフォルト `100`)。
- `Api__RateLimitPerMinute`: API キーごとの 1 分あたりの固定ウィンドウリクエスト予算 (デフォルト `120`)。

### 🤖 MCP Server Settings {#mcp-server-settings}

オプションのMCP (Model Context Protocol) サーバーは、Streamable HTTP トランスポートを介して、REST API と同じ読み取り専用のメールアクセスを AI エージェントに提供します。これは**デフォルトで無効**になっています。詳細なリファレンスについては [MCP guide](MCP.md) を参照してください。MCP は、REST API と全く同じ API キー (`ma_...` bearer tokens) および、ユーザーごとのアカウントアクセススコープを再利用します。
- `Mcp__Enabled`: MCP サーバーのマスタースイッチ (デフォルト `false`)。`false` のとき、`/mcp` エンドポイントは `404` を返します。
- `Mcp__AllowAttachmentDownloads`: `get_attachment` ツールが添付ファイルのバイト列を返すことを許可します (デフォルト `true`)。`false` のとき、ツールはエラーを返します。
- `Mcp__DefaultPageSize`: 呼び出し側が `pageSize` を省略した場合に `search_emails` が使用するデフォルトのページサイズ (デフォルト `20`)。
- `Mcp__MaxResults`: `search_emails` のページサイズの最大値。大きな値は制限されます (デフォルト `100`)。
- `Mcp__MaxAttachmentBytes`: `get_attachment` ツールが base64 としてインラインで返す添付ファイルの最大サイズ (バイト単位)。大きな添付ファイルは拒否されます (デフォルト `10000000` ≈ 10 MB)。
- `Mcp__AllowMessageDownloads`: `get_email_eml` および `get_email_raw` ツールが完全なメッセージを返すことを許可します (デフォルト `true`)。`false` のとき、ツールはエラーを返します。完全なメッセージには添付ファイルが含まれるため、このスイッチと `Mcp__AllowAttachmentDownloads` の**両方**が `true` である必要があります。どちらかをオフにすると、両方のツールが無効になります。
- `Mcp__MaxMessageBytes`: `get_email_eml` および `get_email_raw` ツールがインラインで返すメッセージの最大サイズ (バイト単位)。大きなメッセージは拒否されます (デフォルト `10000000` ≈ 10 MB)。
- `Mcp__RateLimitPerMinute`: API キーごとの 1 分間あたりの固定ウィンドウリクエスト予算 (デフォルト `120`)。

### 📨 MailSync Settings {#mailsync-settings}
- `MailSync__IntervalMinutes`: メール同期の間隔（分）。これはグローバルなデフォルト値です。各アカウントは作成/編集ページから個別にこれを上書きできます（空欄の場合はこのデフォルト値が使用されます）。
- `MailSync__FullSyncIntervalHours`: 自動フル再同期のためのオプションのグローバルデフォルト（時間）。設定されていない場合（デフォルト）、作成/編集ページでアカウントごとの `FullSyncIntervalHours` 値が設定されていない限り、自動フル同期は実行されません。アカウントごとの値はこのグローバルデフォルトを上書きします。
- `MailSync__TimeoutMinutes`: アカウントごとの同期タイムアウト（分）。実行時間が長くなった同期は、次のメッセージ境界で停止し、チェックポイントを保持して次回の実行時に再開されます。ジョブは `Timed Out` として報告され、`LastSync` は進められません。これはスケジュールされた同期にのみ適用されます。UIから開始された同期（手動同期またはフル再同期）はタイムアウトなしで実行されます。`0`（デフォルト）または 0 以下の値は、タイムアウトなしを意味します。大きなメールボックスを同期している場合は、アップグレード前にこの値を確認してください。[Synchronization.md](Synchronization.md#-stopping-a-sync-early) を参照してください。
- `MailSync__ConnectionTimeoutSeconds`: IMAP接続の接続タイムアウト（秒）。
- `MailSync__CommandTimeoutSeconds`: IMAPコマンドのコマンドタイムアウト（秒）。
- `MailSync__AlwaysForceFullSync`: 常にフル同期を強制するかどうか (true/false)。
- `MailSync__IgnoreSelfSignedCert`: 自己署名証明書を無視するかどうか (true/false)。
- `MailSync__MaxConcurrentSyncs`: 同時に実行できるアカウント同期の数。デフォルトは `1`（逐次実行、後方互換性あり）。スケジューラは、バッチ全体を待つのではなく、スロットが空き次第補充するため、一つの遅いメールボックスが他の同期を妨げることがなくなりました。複数のアカウントを並行して同期するために増やしてください。プロバイダーのレート制限とローカルリソースの使用量に注意してください。[Synchronization.md](Synchronization.md#-how-accounts-are-scheduled) を参照してください。
- `MailSync__InterAccountDelaySeconds`: 各アカウントの同期タスクの終了時に適用される、オプションのディレイ（秒）。デフォルトは `0`（ディレイなし）。`MaxConcurrentSyncs > 1` の際のバースト開始を避けるのに役立ちます。
- `MailSync__MaxIssuesPerKind`: アカウントページ用に、同期ジョブが各種類の問題をいくつ記憶するか。失敗したフォルダ、欠落したフォルダ、失敗したメッセージは個別に予算（バジェット）が割り当てられているため、一種の大量発生によって他の種類が画面外に押し出されることはありません。予算を超えたものは、保持されるのではなくカウントされます。デフォルトは `20`。`0` を設定すると詳細がオフになり、カウンターのみが残ります。[Synchronization.md](Synchronization.md#-observing-the-sync) を参照してください。
- `MailSync__GlobalExcludedFolders__<n>`: 各アカウント独自の除外フォルダリストに加えて、**すべての**アカウントで同期から除外されるフォルダ。デフォルトは空なので、既存の設定には影響しません。これら2つのリストは**加算的**です（どちらかに一致する場合にフォルダがスキップされます）。両方のリストは同じマッチングルールを使用します：フルパスの完全一致、フォルダ名の完全一致、パスサフィックス一致（したがって `Drafts` は `INBOX/Drafts` と `INBOX.Drafts` にも一致します）、および `MailSync__ExcludeSubfolders` がオンの場合にエントリが指定するフォルダの下位すべて。マッチングはケースインセンシティブ（大文字小文字を区別しない）です。同じサーバーから多くのメールボックスをインポートする場合に便利です。そうしない場合は、すべてのアカウントで同一の除外リストを維持する必要があります。デフォルトではフォルダは除外されません。どの名前をリストアップすべきかは、サーバーとその言語に依存します。カレンダーや連絡先フォルダも含まれるメールボックスツリーの例：
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
- `MailSync__ExcludeSubfolders`: 除外エントリが、その名前が指すフォルダの下位フォルダもカバーするかどうか。これはアカウントごとのリストとインストール全体の設定の両方に、また両方のプロバイダーに適用されます。デフォルトは `false` で、このオプションが存在する前の動作と一致します：エントリ `Deleted Items` はそのフォルダのみに一致し、ツリーをアーカイブから除外するには、そのルートとその下のすべてのフォルダを指定する必要があります。`true` に設定すると、エントリがその下のツリーもカバーするようになります。この場合、エントリ `Deleted Items` は `Deleted Items/2024` とそのブランチの残りの部分もアーカイブから除外します。ルールはパスセパレータに固定されるため、フォルダ全体を対象とし、名前の一部を対象にすることはありません。例えば `Archive` は `Archive` と `Archive/Team` をカバーし、`Team Archive Review` という名前のメールフォルダはそのままにします。`.` もパスサフィックスルールと同様にセパレータとしてカウントされます。

### 📧 Original Message Storage Settings {#original-message-storage-settings}

デフォルトでは、アーカイブはメールの解析された部分のみを保持するため、エクスポートされた `.eml` は再構成されたものになります。つまり、転送エンコーディング、MIME境界、およびヘッダーの折り返しが受信時のメッセージとは異なり、DKIM署名も検証できなくなります。`RawMessages:Enabled` を使用すると、完全なオリジナルも保存されます。

| パラメータ | デフォルト | 用途 |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | 新規にアーカイブされたすべてのメールのオリジナルを保存します。 |
| `RawMessages__Storage` | `postgres` | バイトが格納される場所: `postgres` または `filesystem`。 |
| `RawMessages__MaxMessageSizeMb` | `50` | 大きなメッセージはオリジナルなしでアーカイブされます。 `0` = 無制限。 |
| `RawMessages__IncludeGraph` | `true` | Microsoft 365 (Graph) アカウントのオリジナルも保存します。 |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | メッセージファイルのディレクトリ。Docker内のマウントされたボリュームを指している必要があります。 |
| `RawMessages__FileSystem__ShardDepth` | `2` | メール ID が分散されるディレクトリの階層。 |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | すべてのダウンロード時に SHA-256 を検証します。 |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | どのメールからも参照されていないメッセージファイルを削除します。 |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | そのスウィープが実行される頻度。 |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | これより新しいファイルは決して削除されません。 |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | 開始後の最初のスウィープまでの遅延。これにより、まだ書き込み中のインポートが収集されるのを防ぎます。 |

!!! warning "Filesystem backend needs a mounted volume"
    `RawMessages__Storage=filesystem` を使用する場合、メッセージファイルはデータベースの外に存在します。
    バインドマウントがない場合、ファイルはコンテナの書き込み可能レイヤーに置かれ、データベースの行は生存しますが、次のコンテナ再作成時に失われます。ディレクトリをデータベースのダンプと一緒にバックアップしてください。

詳細な説明については **[Original Message Storage](RawMessages.md)** を参照してください：オリジナルの提供方法、ストレージバックエンド、`--migrate-raw-storage` を使用して既存のメッセージをそれらの間で移動する方法、およびバックアップすべき内容について説明しています。

### 📈 Monitoring Settings {#monitoring-settings}

| パラメータ | デフォルト | 用途 |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | これより古い有効なアカウントは `/api/v1/health` レポート `Degraded` を生成します。 `0` はチェックを無効にします。 |
| `Metrics__Enabled` | `false` | Prometheus メトリクスエンドポイントとそのリフレッシュループを有効にします。 |
| `Metrics__RefreshMinutes` | `5` | メトリクススナップショットを再構築する頻度。1未満の値は1に引き上げられます。 |

ヘルスおよびメトリクスエンドポイントには、admin API key と `Api__Enabled=true` が必要です。
コンテナ自身の liveness check は認証不要で、設定も必要ありません。
エンドポイント、メトリクス名、Prometheus の設定、および Docker の health check については、**[Health and Metrics](Monitoring.md)** を参照してください。

### 📤 BatchRestore 設定 {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: 非同期処理をトリガーするメールの数。
- `BatchRestore__MaxSyncEmails`: 同期処理におけるメールの最大数。
- `BatchRestore__MaxAsyncEmails`: 非同期処理におけるメールの最大数。
- `BatchRestore__SessionTimeoutMinutes`: バッチリストアのセッションタイムアウト（分単位）。
- `BatchRestore__DefaultBatchSize`: メール操作のデフォルトのバッチサイズ。

### 🏢 Tenant Management Settings {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: 1回のTenant Management操作で追加できる最大メールボックス数です。デフォルトは`1000`です。非常に大規模なテナントの場合はこの値を増やし、誤って大量のインポートを行うのを防ぐにはこの値を下げてください。制限を超えた場合、操作はバリデーションエラーとして拒否され、アカウントは作成されません。詳細は [M365 Tenant Import Guide](M365TenantImport.md) を参照してください。

### 🐄 Mailcow Settings {#mailcow-settings}

- `Mailcow__MaxSelectedMailboxes`: 1回のMailcowアクティベーション実行中にプロビジョニングできるメールボックスの最大数です。デフォルトは`1000`です。誤った大量プロビジョニングを防ぐには値を下げ、大規模なインスタンスの場合は値を上げてください。制限を超えた場合、アカウントは作成されません。詳細は [Mailcow Instances](Mailcow.md) を参照してください。
- Mailcowの統合には、さらに `Security__CredentialEncryptionKey` が必要です（[Security Settings](#-security-settings) を参照）。認証情報の暗号化が無効化されている間は、インスタンスの接続およびメールボックスのアクティベーションはどちらも拒否されるため、APIキーや生成されたアプリパスワードがプレーンテキストとして保存されることはありません。
- Mailcow HTTPS API、およびTLS経由のIMAPホスト（通常はポート `993`）へのアウトバウンドアクセスが必要です。

### 📦 BatchOperation 設定 {#batchoperation-settings}
- `BatchOperation__BatchSize`: メール操作のバッチサイズ。
- `BatchOperation__PauseBetweenEmailsMs`: 個々のメール間のポーズ（ミリ秒）。
- `BatchOperation__PauseBetweenBatchesMs`: バッチ間のポーズ（ミリ秒）。

### 📤 Offload Settings {#offload-settings}

[date-windowed offload](Offload.md) の設定。すべてのデフォルト値は、この機能が存在する前のアプリケーションの動作を再現するため、このセクションを設定しないインストールは影響を受けません。
- `Offload__MaxConcurrentJobs`: 同時に実行できるリストアまたはオフロードジョブの数。デフォルトは `1` で、これはジョブキューが常に使用してきた厳密なシリアル処理を維持します。この値に関わらず、ターゲットメールボックスごとに実行されるジョブは最大 1 つです。
- `Offload__PrefetchMaxMessages`: 重複検出のためにターゲットメールボックスからインデックスを作成するメッセージ数の上限。デフォルトは `500000` です。これを超えると、チェックは単一のフォルダに絞り込まれ、スコープが縮小されたことがログに記録されます。
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: オフロードされないソースフォルダ。名前変更の前に照合され、サブフォルダも対象となります。デフォルトは空です。
- `Offload__FolderRenameMap__<SourceFolder>`: ソースフォルダパスの先頭セグメントを書き換えます（例：`Offload__FolderRenameMap__Sent Items=Sent`）。デフォルトは空です。
- `Offload__MarkAsSeen`: 追加されたメールを既読としてフラグを立てるかどうか。デフォルトは `true` で、既存のリストア動作と一致します。

どちらのフォルダ設定も、意図的に空の状態で提供されています。要求なしにフォルダを書き換えたり削除したりすることは、すでにリストアパスを使用しているユーザーを驚かせることになるためです。したがって、設定なしでの実行では、スパムフォルダを含めすべてが移行され、ターゲット自身のフォルダの隣に 2 番目の特別なフォルダのセットが作成されます。

### 📊 Bandwidth Tracking Settings {#bandwidth-tracking-settings}

- `BandwidthTracking__Enabled`: IMAPレート制限のハンドリングにおける帯域幅トラッキングを有効または無効にします (true/false)。デフォルトは `false` です。有効にすると、システムはアカウントごとの帯域幅使用量を追跡し、プロバイダーの制限に達したときに同期を一時停止できます。詳細については [Rate Limit Handling](RateLimitHandling.md) を参照してください。
- `BandwidthTracking__DailyLimitMb`: アカウントあたりの1日あたりのダウンロード制限（メガバイト単位）。デフォルトは `25000` (25 GB) です。帯域幅制限のあるプロバイダーの場合は、そのレート制限に合わせて設定してください (例: 1日あたりの制限が約2500 MBのプロバイダーの場合は `2500`)。この制限に達すると、システムは同期を一時停止します。
- `BandwidthTracking__WarningThresholdPercent`: 警告メッセージがログに記録される、1日あたりの制限に対する割合（パーセンテージ）。デフォルトは `80` です。帯域幅の使用量がこのパーセンテージに達すると、制限が近づいていることを監視しやすくするために警告がログに記録されます。
- `BandwidthTracking__PauseHoursOnLimit`: 1日の制限に達したときに同期を一時停止する時間（時間単位）。デフォルトは `24` です。この期間が経過すると、制限フラグは自動的にクリアされ、同期が再開されます。
- `BandwidthTracking__TrackUploadBytes`: アップロードの帯域幅も追跡するかどうか (true/false)。デフォルトは `false` です。ほとんどのIMAPプロバイダーはダウンロードのみを制限するため、通常これは必要ありません。

### 🎯 Selection Settings {#selection-settings}
- `Selection__MaxSelectableEmails`: 一度に選択できるメールの最大数。

### 👁️ 設定の表示 (プライバシーと表示) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: プライバシーとトラッキング防止のためのデフォルトのメール表示モードを制御します (true/false)。デフォルトは `false` です。
  - `true` に設定されている場合: デフォルトでメールがプレーンテキスト表示で開かれ、トラッキングピクセル、外部画像、およびウェブビーコンの自動読み込みが防止されます。これは、メールのトラッキングやプライバシーを気にするユーザーに推奨されます。
  - `false` に設定されている場合: デフォルトでメールが HTML 表示で開かれ、画像やスタイリングを含むフォーマットされたメールが表示されます。
  - ユーザーは、メール詳細ページのボタンを使用して、HTML 表示とプレーンテキスト表示を切り替えることができます (両方の形式が利用可能な場合にのみ表示されます)。
  - 「Full View」リンクは、現在選択されている表示モードに従います。

- `View__BlockExternalResources`: トラッキングを防止しプライバシーを向上させるために、HTML メール表示において外部リソース (リモート画像、外部 CSS、外部スクリプト、ウェブフォントなど) をブロックします (true/false)。デフォルトは `false` です。
  - `true` に設定されている場合: HTML メールを表示する際、外部リソースはフィルタリングされます。インラインコンテンツとデータ URI (`cid:` 参照によるインライン添付ファイルを含む) のみが表示されます。
  - `false` に設定されている場合: HTML メールは、すべての元の外部リソースとともに表示されます。
  - **重要**: この設定はメールの **表示** にのみ影響します。アーカイブされたメールは、すべての元のコンテンツが保持された状態で、変更されることなくデータベースに完全に保存されます。
  - ブロックされるリソースには以下が含まれます:
    - リモート画像 (トラッキングピクセル、サーバーにホストされた外部画像)
    - 外部 CSS スタイルシート
    - @font-face による外部フォント
    - @import による外部 CSS インポート
    - 外部背景画像
  - 許可されるリソース:
    - data: URI として埋め込まれたインライン画像
    - cid: URI を介して参照されるインライン添付ファイル
    - インライン CSS スタイルおよび style タグ
  - この設定は `DefaultToPlainText` とは独立して動作し、HTML メールを閲覧する際の追加のプライバシー保護レイヤーを提供します。

### 📊 Dashboard Settings {#dashboard-settings}

- `Dashboard__ShowDirectionSplits`: カウンターカードが受信・送信の内訳を保持するか、およびアカウントカードが個別のドメイン数を保持するかどうかを設定します。デフォルトは `true` です。`false` に設定すると、それらの数値は計算されて隠されるのではなく、計算自体が行われなくなります。つまり、カウンターは単純なカウントを表示し、添付ファイルのカウントは、これらのパーツが追加する唯一のクエリである、対象となるメールへの結合を停止します。
- `Dashboard__SelectablePeriods`: ダッシュボードのチャートが、解像度（resolution）、期間（period）、およびそれらを移動するための矢印を提供するかどうかを設定します。デフォルトは `true` です。`false` に設定すると、チャートは過去12ヶ月を月ごとに表示し、アーカイブ全体の送信者を表示します。チャートのエンドポイントは、存在しないかのように応答し、最古の送信日は検索されません。
- `Dashboard__CacheSeconds`: 計算されたダッシュボード統計（受信・送信の内訳を含む合計、アカウントごとのカウント、チャートシリーズ、トップ送信者、最近のメール、データベースサイズ）を、サーバーのインメモリキャッシュにどのくらいの期間保持するかを設定します。デフォルトは `60` 秒です。`0` に設定すると、キャッシュを無効にし、常に統計を再計算します。解像度と期間の各チャート選択はそれぞれ個別にキャッシュされるため、選択を切り替えるダッシュボードは、選択ごとに1つのエントリを保持します。値が高いほど、数値が古くなる可能性がありますが、大規模な環境でのデータベース負荷を軽減できます。同期ステータスバッジ、ストレージ値、および最近のメールリストは常にライブで取得され、このキャッシュの影響を受けません。
- `Dashboard__RefreshIntervalMinutes`: `DashboardStatsRefreshService` バックグラウンドサービスが、高負荷なダッシュボード集計（受信・送信の内訳を含む合計カウント、アカウントごとのカウント、アカウントドメイン数、トップ送信者を含むデフォルトのチャートシリーズ、データベースサイズ）を `DashboardStatsCache` データベーステーブルにどのくらいの頻度で再計算するかを設定します。数百万通のメールがあるインストール環境では、ダッシュボードはリクエストパス内でアーカイブ全体を集計する代わりに、これらの準備された値を読み取るため、初回ロードの約10秒がミリ秒単位に短縮されます。チャートのエンドポイントは、デフォルトの選択（1年間の月次、受信送信者、現在の期間）を同じ行から回答するため、そこに戻ると初回描画時の数値が表示されます。その他のすべての選択は、リクエスト時に計算され、上記のインメモリキャッシュに保持されます。デフォルトは `15` 分です。`0` に設定すると、バックグラウンド更新が無効になります。その場合、ダッシュボードは（上記のインメモリキャッシュは引き続き適用されますが）統計を再びライブで計算します。表示される値は、実際のアーカイブの状態から設定された間隔分遅れる場合があります。現在の値とは異なる `ShowDirectionSplits` または `SelectablePeriods` 値で計算された行は使用されず、ダッシュボードは次のリフレッシュまでライブで計算を行います。

### 🔄 Jobs Settings {#jobs-settings}

- `Jobs__RefreshSeconds`: ブラウザのタブが開いている間、バックグラウンドジョブページが自身をリロードする頻度です。デフォルトは `30` 秒です。自動リロードをオフにして手動で更新する場合は `0` に設定してください。各リロードでは8つのすべてのジョブソースからページを再構築するため、多数のアカウントを持つインストール環境では、リロードの間隔を長くすることで、ページ上に開いているすべてのタブの数に比例して負荷を低く抑えることができます。

### 🗃️ Npgsql Settings {#npgsql-settings}
- `Npgsql__CommandTimeout`: データベースコマンドのタイムアウト（秒単位）。

### 📥 アップロード設定 {#upload-settings}
- `Upload__MaxFileSizeGB`: アップロードの最大ファイルサイズ（GB）。
- `Upload__KeepAliveTimeoutHours`: アップロードのキープアライブタイムアウト（時間）。
- `Upload__RequestHeadersTimeoutHours`: リクエストヘッダーのタイムアウト（時間）。

### 📂 ローカルインポート設定 {#local-import-settings}

- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, etc.: CLIインポートコマンド（`--import-mbox`, `--import-eml`）がファイルを読み取ることを許可されているローカルディレクトリのホワイトリストです。各エントリはコンテナ内のパスです。Dockerボリュームを使用して、インポートファイルをこれらのディレクトリのいずれかにマウントする必要があります。デフォルト: `/data/import`（フォールバックとして自動的に`/app/uploads`に設定されます）。
  - `docker exec`を使用してインポートコマンドを実行する場合、`--file`を介して提供されるファイルパスは、これらの許可されたパスのいずれか ضمنにある必要があります。
  - このセキュリティ対策により、CLIコマンドからの任意のファイルシステムへのアクセスが防止されます。
  - 異なるインポートソースに対して複数のパスを構成できます。
  - 詳細な使用方法については、[CLI commands](CLI.md)を参照してください。

### 📄 CSV インポート設定 {#csv-import-settings}
- `CsvImport__MaxRows`: 1回のバルクインポートで処理されるCSV行（メールボックス）の最大数。デフォルトは `5000` です。大規模なデプロイメントの場合はこの値を増やし、データベース負荷への影響を制限したい場合は値を下げてください。
- `CsvImport__MaxFileSizeBytes`: アップロードされるCSVファイルの最大許容サイズ（バイト単位）。デフォルトは `10000000` (10 MB) です。必要に応じて、アップロード制限に合わせてこの値を調整してください。
- CSVによるIMAPアカウントのバルクインポートに関する詳細な使用手順については、[Account Import Guide](Account-Import.md) を参照してください。

### 📤 Audit Export Settings {#audit-export-settings}

監査データエクスポートページ（管理者のみ、Logsページからアクセス可能）の設定です。エクスポートは、外部監査ツール向けに、既存のアーカイブから表形式の大量データ（INDEX.XML + CSV tables + DTD）を含むZIPパッケージを生成します。使用方法の詳細については、[Audit Data Export Guide](AuditExport.md) を参照してください。

- `AuditExport__DataSupplierName`: エクスポートされたインデックスファイル内で組織を識別するための「Data supplier name」フォームフィールドのデフォルト値。デフォルト: 空。
- `AuditExport__DataSupplierLocation`: 「Data supplier location」フォームフィールド（例：本社の所在地）のデフォルト値。デフォルト: 空。
- `AuditExport__Comment`: エクスポートされたインデックスファイルに自由形式のテキストとして含まれる「Comment」フォームフィールドのデフォルト値。デフォルト: 空。
- `AuditExport__OutputDirectory`: 生成されたZIPファイルが保存されるディレクトリ。相対パスはアプリのコンテンツルートに対して解決されます。デフォルト: `exports/audit`。
- `AuditExport__RetentionDays`: 完了したエクスポートファイルが日次のクリーンアップによって削除されるまでの日数。デフォルト: `30`。
- `AuditExport__MaxRangeYears`: エクスポート期間の開始日と終了日の間で許容される最大期間。デフォルト: `10`。
- フォームフィールドにはこれらのデフォルト値が事前に入力されますが、エクスポートごとに編集可能です。
- すべてのエクスポートは、アクセスログに開始エントリと結果エントリを書き込みます（タイプ "Audit Data Export"）。これにより、DBスキーマの変更なしに、履歴の改ざん防止（revision-safe）が実現されます。

### 🔒 Deletion Policy Settings {#deletion-policy-settings}

- `DeletionPolicy__DeletionAllowed`: アーカイブされたメールの手動削除を許可するかどうかを制御します (true/false)。デフォルトは `true` です。`false` に設定されている場合：
  - 起動時にデータベースのコンプライアンストリガーを介して、すべてのアーカイブされたメールがロック (`IsLocked = true`) され、データベースレベルでのあらゆる変更や削除が防止されます。
  - アプリケーションレベルで手動削除 (単一および一括) がブロックされ、エラーメッセージが表示されます。
  - 新しくインポートされたメールもロックされるように、カラムのデフォルト値が調整されます。
  - 監査可能性を確保するため、現在のポリシー状態が起動のたびに AccessLogs テーブルにログ記録されます (Logs ページに "Deletion Policy" エントリとして表示されます)。
  - ローカル保持期間による削除は例外となります：設定された保持期間に該当するメールは、引き続き削除されます (保持プロセス内の削除直前にアンロックされます)。
- `DeletionPolicy__MaxBulkDeleteEmails`: フィルターベースの一括削除 (*Delete all matches*) の上限値です。デフォルトは `100000` です。より多くのメールに一致するフィルターは、絞り込みを求めるメッセージと共に拒否されます。ジョブがサイレントに切り捨てられることはありません。保持ポリシーによってロックされているメールはスキップされ、ジョブステータスに報告されますが、アンロックはされません。ジョブは開始時にこの制限と `DeletionPolicy__DeletionAllowed` を再チェックするため、ポリシー変更前にキューに追加されたジョブは、古い設定で実行されるのではなく停止します。
- **Immutability protection:** `IsLocked = true` のとき、データベースのコンプライアンストリガー (`prevent_locked_email_changes`) は、メール行への ANY な変更をブロックします — 固定されたフィールドリストだけでなく、すべてのカラムが保護されます。唯一の例外となるカラムは、`IsLocked` 自体 (保持期間による削除および起動時のポリシー適用のためにアンロックを可能にするため) と `FolderName` (サーバー側でメールが移動された際に IMAP 同期がフォルダを更新できるようにするため) です。この保護はカラムに依存せず (JSONB ベースの比較)、将来のスキーマ追加も自動的にカバーします。

### 🕐 タイムゾーン設定 {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: UI上でメールのタイムスタンプを表示するために使用されるタイムゾーン。IANAタイムゾーン識別子（例: "Europe/Berlin", "Asia/Tokyo"）を使用します。デフォルトは後方互換性のために "Etc/UCT" です。メールをインポートする際、タイムスタンプは表示用にこのタイムゾーンに変換されます。

### 🎉 ReleaseNotes Settings (Version Update Splash Screen) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: このインスタンスが報告するバージョン文字列です。GitHubのリリースタグと照合され、より新しいリリースが存在するかどうかを判断します。空の場合、変更履歴はコンパイルされたアセンブリのバージョンにフォールバックされ、アップデートのヒントはオフのままになります。
- `ReleaseNotes__Enabled`: バージョンアップデートのスプラッシュスクリーンを有効または無効にします (true/false)。デフォルトは `true` です。有効にすると、アプリケーションのアップデート後、管理者に一度限りの変更履歴モーダルが表示され、現在のバージョンに対して GitHub Releases から取得されたリリースノートが表示されます。各管理者はモーダルを閉じることができ、新しいバージョンが出たときにのみ再表示されます。`false` に設定すると、アップデートのヒントを含め、この機能を完全に無効にします。
- `ReleaseNotes__UpdateHintEnabled`: GitHub がインストールされているバージョンよりも新しい安定版リリースを公開したときに、管理者に右下隅で小さなトーストを表示します (true/false)。デフォルトは `true` です。トーストはそのリリースへのリンクを含み、ブラウザ内でバージョンごとに非表示にされます。ドラフトおよびプリリリースは無視されます。`ReleaseNotes__AppVersion` が必要です。これがない場合、ヒントは表示されません。
- `ReleaseNotes__UpdateCheckIntervalHours`: GitHub のリリースリストを更新する頻度（時間単位）です。デフォルトは `6` です。間隔を短くすると、長時間稼働しているインスタンスにおいて新しいリリースがより早く表示されるようになり、間隔を長くすると GitHub API の呼び出し回数が減少します。最小値は `1` です。
- `ReleaseNotes__RequestTimeoutSeconds`: 1 回の GitHub API リクエストのタイムアウト（秒単位）です。デフォルトは `10` です。タイムアウトが発生した場合、リクエストを保持し続けるのではなく、その試行における変更履歴とアップデートのヒントはオフのままになります。次のチェック時に再試行されます。

### 🔧 データベースメンテナンス設定 {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: 自動的な日次データベースメンテナンスを有効または無効にします (true/false)。デフォルトは `false` です。有効にすると、システムはデータベースのパフォーマンスを最適化し、肥大化を防ぐために VACUUM ANALYZE 操作を自動的に実行します。詳細については [Database Maintenance Guide](DatabaseMaintenance.md) を参照してください。
- `DatabaseMaintenance__DailyExecutionTime`: データベースメンテナンスを実行する時刻を 24 時間形式 (HH:mm) で指定します。デフォルトは `02:00` です。システムの活動が少ない時間帯を選択してください。
- `DatabaseMaintenance__TimeoutMinutes`: メンテナンス操作に許可される最大時間（分）です。デフォルトは `30` です。より大きなデータベースの場合は、この値を増やしてください。

### 🧬 添付ファイルの重複排除設定 {#attachment-deduplication-settings}
添付ファイルの重複排除は、すべてのユニークな添付ファイルペイロードを一度だけ保存し（SHA-256によるコンテンツアドレス指定）、これは**常に有効化されているコア機能**です。意図的にオン/オフのスイッチは設けられていません。以下のバッチ/スケジューリングパラメータのみを調整できます。詳細については[Attachment Deduplication Guide](AttachmentDeduplication.md)を参照してください。

- `AttachmentDeduplication__BatchSize`: 既存データの初回バックグラウンド移行中、1回のトランザクションで移行される既存の添付ファイルの数。デフォルトは `200` です。値を大きくすると移行は速くなりますが、バッチあたりのメモリ使用量/DB負荷が増加します。
- `AttachmentDeduplication__DelayBetweenBatchesMs`: 負荷の高いシステムにおいてデータベース負荷を抑制するために、移行バッチ間に設けるオプションの休止時間（ミリ秒）。デフォルトは `0`（休止なし）です。
- `AttachmentDeduplication__StartupDelaySeconds`: スキーマ移行が完了する時間を確保するため、アプリケーション起動後にバックグラウンド移行が開始されるまでの遅延時間（秒）。デフォルトは `20` です。
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: どのメールからも参照されなくなった添付ファイルペイロードを削除する、常時稼働のガベージコレクションの間隔（時間）。デフォルトは `12` です。これは `DatabaseMaintenance__Enabled` とは独立して動作します。
- `AttachmentDeduplication__CommandTimeoutSeconds`: 移行バッチ操作（SHA-256ハッシュ化を伴う INSERT および UPDATE）のデータベースコマンドタイムアウト（秒）。デフォルトは `300`（5分）です。非常に大規模なデータベースや添付ファイルの場合はこの値を増やし、より迅速な失敗検知を行いたい場合は値を下げてください。バッチがタイムアウトした場合、サービスはバッチサイズを半分にして自動的にリトライします。

### 💾 Account Storage Settings {#account-storage-settings}

アカウントごとのストレージ表示は、Dashboardの「Account Overview」テーブルおよびMailAccountsの「Show All」テーブルにおいて、各アカウントのデータベースストレージ使用量（すべてのメールフィールド + 添付ファイル）を表示します。autarkバックグラウンドサービス（`AccountStorageRefreshService`）が、PostgreSQLの`pg_column_size`を列ごとに合算（メール本文を読み取ることなく、メールのすべてのフィールドをカバー）して値を計算し、それらを`AccountStorageCache`テーブルにキャッシュします。このサービスは`DatabaseMaintenance__Enabled`とは独立して動作します。

- `AccountStorage__Enabled`: ストレージ更新サービスを有効または無効にします (true/false)。デフォルトは`true`です。有効な場合、サービスは起動時に保留中のアカウントに対して再開可能なバックフィルを実行し（`AccountStorageBackfillState`を介してクラッシュセーフを実現）、その後は毎日フルリフレッシュを実行します。無効な場合、ストレージの値はメールが同期、インポート、または削除されたときにのみ更新されますが、表示される値は時間の経過とともに古くなる可能性があります。
- `AccountStorage__DailyExecutionTime`: すべてのアカウントの毎日フルリフレッシュを行う時刻（24時間形式 `HH:mm`）。デフォルトは`02:30`です。システムの活動が低い時間帯を選択してください。
- `AccountStorage__BackfillDelayMs`: 起動時の初期バックフィルにおけるアカウント間の遅延（ミリ秒）。デフォルトは`5000`です。値を小さくするとバックフィルは高速化されますが、データベースの負荷が増加します。非常に大規模なアーカイブの場合は、データベースへの過負荷を避けるためにこの値を大きくしてください。
- `AccountStorage__RefreshBatchDelayMs`: 毎日のフルリフレッシュにおけるアカウント間の遅延（ミリ秒）。デフォルトは`1000`です。値を小さくするとリフレッシュは高速化されますが、データベースの負荷が増加します。非常に大規模なアーカイブの場合はこの値を大きくしてください。
- `AccountStorage__CommandTimeoutSeconds`: アカウントごとのストレージ計算（アカウントのすべての行に対する`pg_column_size`）のデータベースコマンドタイムアウト（秒）。デフォルトは`300`（5分）です。計算ではTOASTデータを含むすべてのメール行を読み取るため、非常に大規模なアーカイブ（25万件以上のメール）では1分以上かかることがあります。以前の30秒というNpgsqlのデフォルト値では、リフレッシュがサイレントに失敗し、ダッシュボードに古い値が表示され続けていました。アーカイブの計算が依然としてタイムアウトする場合は、この値を上げてください。

> 💡 **Note**: ストレージの値は、各メールの同期、インポート、または保持ポリシーによる削除の直後に更新されるため、毎日のリフレッシュが行われなくても表示される値は最新の状態に保たれます。毎日のリフレッシュは、エッジケース（例：データベースへの直接的な変更）を捕捉するためのセーフティネットです。

### 📝 Logging Settings {#logging-settings}
- `Logging__LogLevel__Default`: アプリケーションのデフォルトのログレベルです。利用可能なレベルは以下の通りです: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`。デフォルトは `Information` です。
- `Logging__LogLevel__Microsoft_AspNetCore`: ASP.NET Core フレームワーク メッセージのログレベルです。デフォルトは `Warning` です。
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Entity Framework データベース コマンドのログレベルです。デフォルトは `Warning` です。

### 🛡️ Security Settings {#security-settings}
- `AllowedHosts`: アプリケーションが提供を許可されるホスト名のセミコロン区切りのリストです。これにより、HTTP Host header 攻撃を防ぐことができます。例: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`。**重要**: `*` を使用するとホストヘッダーの検証が無効になるため、本番環境では使用しないでください。

#### 🔐 Credential Encryption at Rest {#credential-encryption-at-rest}
メールアカウントの資格情報（IMAP パスワード、M365 クライアントシークレット、および MSA OAuth トークン）は、データベース内で暗号化できます。暗号化キーはデータベースの**外部**（環境変数 / Docker secret）に保持されるため、データベースのダンプだけでは資格情報を読み取ることはできません。

- `Security__CredentialEncryptionKey`: Base64 エンコードされた 256 ビット（32 バイト）のキー暗号化キー。以下のコマンドで生成できます:
  ```bash
  openssl rand -base64 32
  ```
  値を環境変数として設定してください（推奨: compose ファイル内の `Security__CredentialEncryptionKey` またはコンテナ環境）。アプリケーションは `_FILE` スタイルの Docker secret をサポートしていません。Docker secret を使用する場合は、それをマウントし、entrypoint 内でその内容を環境変数にロードしてください。決して `appsettings.json` にキーをコミットしないでください。キーが設定されると、資格情報は書き込み時に暗号化され、バックグラウンドサービスが既存の平文の資格情報を一度暗号化します。
- `Security__EncryptAccountCredentials`: 新規または更新された資格情報値の暗号化を有効または無効にします (true/false)。デフォルトは `true` です。キーが設定されていない場合、資格情報は平文として保存され、起動時に警告がログに記録されます。既存のインストールには、キーが提供されるまで影響しません。
- `Security__CredentialEncryptionBackfill`: バックグラウンドサービスが起動時に既存の平文の資格情報を暗号化するかどうか (true/false)。デフォルトは `true` です。設定されたキーがない場合は動作しません。
- `Security__CredentialEncryptionKeyPrevious`: オプションの以前のキー。それを使用して暗号化された値を復号するためにのみ使用されます（キーローテーション用）。新しい値は常に `Security__CredentialEncryptionKey` で暗号化されます。

  > ⚠️ **キーローテーション**: ローテーション後は、**すべての資格情報が書き換えられるまで**（例：アカウントの編集や自動的な OAuth トークンのリフレッシュによる）、`Security__CredentialEncryptionKeyPrevious` を設定したままにしてください。古いキーで暗号化された既存の値は、新しいキーで自動的に再暗号化*されません*。以前のキーを削除すると、そのキーで暗号化されたままの資格情報は読み取り不能になり、再度入力する必要があります。設定されている限り、現在のキーと同様に安全に保管する必要があります。

> ⚠️ **キーを安全に保管してください**: キーがないと、暗号化された資格情報を復元できず、再度入力する必要があります。データベースのバックアップと一緒に（ただし、データベースとは別に）バックアップを取ってください。
>
> 💡 **既存のインストール**: 安全にアップグレードできます。`Security__CredentialEncryptionKey` が設定されていない限り、何も変わりません。準備ができたらキーを設定してください。既存の平文の資格情報は読み取り可能なまま維持され、バックグラウンドで暗号化されます。

### 🔐 OIDC Configuration {#oidc-configuration}

OpenID Connect 認証の詳細なセットアップ手順については、[OIDC Implementation Guide](OIDC_Implementation.md) を参照してください。

#### Basic OIDC Settings {#basic-oidc-settings}
- `OAuth__Enabled`: OIDC 認証を有効または無効にする (true/false)
- `OAuth__Authority`: OpenID Connect のオーソリティ URL (例: Azure AD の場合は https://sts.windows.net/{TENANT-ID}/)
- `OAuth__ClientId`: ID プロバイダーから割り当てられたクライアント ID
- `OAuth__ClientSecret`: ID プロバイダーから割り当てられたクライアント シークレット
- `OAuth__DisplayName`: OIDC ログインボタンおよび自動リダイレクト ページに表示されるオプションの表示名 (例: `PocketID SSO`)。省略された場合は、汎用的な "Login with OAuth" ラベルが使用されます。
- `OAuth__ClientScopes__0`: ID プロバイダーに要求する最初のスコープ (openid)
- `OAuth__ClientScopes__1`: ID プロバイダーに要求する 2 番目のスコープ (profile)
- `OAuth__ClientScopes__2`: ID プロバイダーに要求する 3 番目のスコープ (email)
- `OAuth__ResponseMode`: 認可リクエストのオプションのレスポンス モード (`query`、`fragment` または `form_post`)。デフォルトは ASP.NET Core のデフォルトである `form_post` です。Synology SSO Server など、`form_post` をサポートしていないプロバイダーの場合は `query` に設定してください。[Provider Compatibility](OIDC_Implementation.md#provider-compatibility) を参照してください。
- `OAuth__AutoApproveUsers`: 管理者の手動承認を必要とせずに、新しい OIDC ユーザーを自動的に承認する (true/false)。デフォルトは `false` です。

#### User Provisioning Settings {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: 管理者の手動承認を必要とせずに、新しい OIDC ユーザーを自動的に承認する (true/false)。デフォルトは `false` です。有効にすると、OIDC プロバイダーを介して認証されたユーザーは即座にアクティブ化され、アプリケーションにアクセスできるようになります。無効（デフォルト）の場合、新しい OIDC ユーザーは非アクティブとして作成され、管理者による手動の有効化が必要になります。詳細については [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users) を参照してください。
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1` など: 管理者として自動的にプロビジョニングされるメールアドレス。これらのメールアドレスを持つユーザーは、最初の OAuth ログイン時に通常の承認プロセスをバイパスして、アクティブな管理者として作成されます。メールの一致はケースインセンティブ（大文字小文字を区別しません）です。

#### Passwordless Login Settings {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: ユーザー名/パスワードによるログインを無効にする (true/false)。デフォルトは `false` です。有効にすると、パスワードフォームが非表示になり、**かつ** サーバーがすべてのパスワード POST を拒否するため、OAuth ログインのみが利用可能になります。`OAuth__Enabled=true` が必要です。OAuth が無効になっている場合、この設定は効果がありません。
- `OAuth__AutoRedirect`: ユーザーを OAuth プロバイダーに自動的にリダイレクトする (true/false)。デフォルトは `false` です。`OAuth__DisablePasswordLogin` が `true` である必要があります。ユーザーはリダイレクトされる前に短いロード画面が表示されます。

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

アプリケーションはリバースプロキシを介してHTTPSでアクセスされることを想定していますが、Kestrel Web サーバーを使用して SSL/TLS 証明書を使用するように構成することもできます。これにより、リバースプロキシとアプリケーションコンテナ間のエンドツーエンドの暗号化が提供されます。

### Configuration Steps {#configuration-steps}

1. **PFX 形式の SSL 証明書を生成または取得します** (例: `localhost.pfx`)

2. **`mailarchive-app` サービス用の `docker-compose.yml` に以下の環境変数を追加します**:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **`mailarchive-app` サービス内のポートマッピングを更新します**:

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

5. **証明書ファイル** (例: `localhost.pfx`) をホストシステムの `./certs` ディレクトリに配置します。

### Environment Variable Explanations {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: HTTP エンドポイント URL (デフォルト: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: HTTPS エンドポイント URL (デフォルト: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: コンテナ内の PFX 証明書ファイルへのパス
- `Kestrel__Endpoints__Https__Certificate__Password`: PFX 証明書ファイルのパスワード

> 💡 **Note**: この構成はオプションです。HTTPS を備えたリバースプロキシを使用している場合 (推奨)、リバースプロキシとアプリケーション間の通信は HTTP のままで問題ありません。ただし、機密性の高い環境で最大限のセキュリティを確保するために、通信経路全体を暗号化する目的で Kestrel でも HTTPS を有効にしたい場合があります。

## 🔑 本番環境向けの Secrets Management {#secrets-management-for-production}

データベースのパスワード、管理者資格情報、OAuth クライアントシークレットなどの機密データを `docker-compose.yml` に直接ハードコードすることは、特にそのファイルがバージョン管理にチェックインされる場合、セキュリティ上のリスクとなります。推奨されるアプローチは、これらの値を Docker Compose が自動的にロードする `.env` ファイルに外部化することです。

### 📄 `.env` ファイル {#the-env-file}

以下の内容を含む `.env` ファイルを、`docker-compose.yml` と同じディレクトリに作成してください：

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

**重要**: Docker Compose は同じディレクトリから `.env` ファイルを自動的に読み取ります。手動で参照する**必要はありません**。`.env` で定義されたすべての変数は、`${VARIABLE_NAME}` 構文を介して `docker-compose.yml` で利用可能です。

### 🐳 適合された `docker-compose.yml` {#adapted-docker-composeyml}

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

リポジトリ内に、他の管理者のためのテンプレートとして機能する `.env.example` ファイルを作成してください。これにはプレースホルダーの値とドキュメントが含まれますが、**実際のシークレットは含まれません**：

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
- **`.env` ファイルへのアクセスを制限する** — Docker Compose を実行するユーザーのみが `.env` ファイルへの読み取り権限を持つようにしてください。

## 🔒 セキュリティに関する注意点 {#security-notes}

- 強力なパスワードを使用し、デフォルトの認証情報を変更してください。パスワードは少なくとも12文字以上で、大文字、小文字、数字、および特殊文字を組み合わせたものである必要があります。一般的な単語や推測しやすい情報の使用は避けてください。
- 本番環境では、リバースプロキシを使用したHTTPSの実装を検討してください。
- PostgreSQLデータベースの定期的なバックアップを推奨します。詳細なバックアップおよびリストアの手順については、[Backup and Restore Guide](BackupRestore.md) を参照してください。
