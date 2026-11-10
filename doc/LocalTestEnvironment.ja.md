# 🧪 Local Test Environment {#local-test-environment}

[← Documentation Index に戻る](index.md)

## 📋 Overview {#overview}

このガイドでは、開発マシン上でテストスイート、アプリケーション、および IMAP ターゲットを実行する方法について説明します。これはコントリビューターを対象としています。起動されるスタックは意図的にセキュリティが低く設定されており、デプロイ用のガイドではありません。デプロイについては [Setup Guide](Setup.md) を参照してください。

以下の3つの要素が関与します：

| Piece | Provided by | Purpose |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | 2つのデータベース（1つは `dotnet test` 用、もう1つはアプリケーション用） |
| Dovecot | `docker-compose.test.yml` | mailcow メールボックスの代わりとなる IMAP ターゲット |
| The application | ホスト上の `dotnet run` | コンテナイメージを再ビルドするよりも高速にイテレーションが可能 |

## 🚀 スタックの起動 {#starting-the-stack}

```bash
docker compose -f docker-compose.test.yml up -d
```

これはすべてを loopback のみに公開します：

| Service | Host address | Notes |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5432 ではなく 5433 なので、ローカルサーバーと衝突しません |
| Dovecot, plain | `127.0.0.1:1143` | プレーンテキスト認証が有効になっています |
| Dovecot, TLS | `127.0.0.1:1993` | イメージの自己署名証明書を使用します |

停止し、すべてのデータベースと保存されたメールを破棄するには：

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ 2つのデータベース {#the-two-databases}

テストスイートはフィクスチャの行を残します。フルランの後は、テストデータベースに数十のメールアカウントとユーザーが保持されます。もしアプリケーションがそのデータベースを共有してしまうと、バックグラウンド同期がそれらのフィクスチャを拾い上げ、存在しないサーバーに繰り返し接続しようとしてしまいます。そのため、postgresコンテナは初回初期化時に2つのデータベースを作成します (`tests/postgres/10-create-dev-db.sql`):

| Database | Used by | Configured in |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

両方の設定ファイルはgitignoredされており、ローカルで作成する必要があります。

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

リポジトリのルートにある `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` はコンテナの外でも重要です。これは絶対パス `/app/DataProtection-Keys` にデフォルト設定されますが、これは開発マシンには存在しません。これがないと、キーリングの読み取り中にすべてのリクエストが失敗します。`IgnoreSelfSignedCert` によって、アプリケーションはポート 1993 での Dovecot 証明書を受け入れることができます。

## ✅ テストの実行 {#running-the-tests}

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

初回実行時には `TestDbFixture` によってマイグレーションが自動的に適用されます。ほとんどのテストではデータベースを必要としません。それ以外のテストでは PostgreSQL を使用するため、スタックが実行されていない場合は `Name or service not known` で失敗します。

接続文字列は環境変数経由で提供することもでき、その場合はすべての設定ファイルよりも優先されます。

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ アプリケーションの実行 {#running-the-application}

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` はすでに `dotnet run` 用の `ASPNETCORE_ENVIRONMENT=Development` を設定しているため、通常の `dotnet run --project MailArchiver.csproj` も動作し、
http://localhost:5179 (`http` プロファイル) を提供します。上記の `ASPNETCORE_URLS` は代わりに
コンテナポート 5000 を選択します。

保留中のマイグレーションは、起動時に `MailArchiverDev` に適用されます。`appsettings.json` の認証情報
(`Authentication:Username` および `Authentication:Password`) を使用してサインインしてください。

## 📬 Dovecot ターゲットの使用 {#using-the-dovecot-target}

Dovecot はパスワード `pass` を使用すれば **任意のユーザー名** を受け入れるため、新しいターゲットメールボックスのプロビジョニングは不要です。名前を決めてログインするだけです。Mail Accounts にて以下のように追加してください：

| Field | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | anything, e.g. `offload-target@example.com` | same |
| Password | `pass` | same |

`tests/dovecot/dovecot.conf` の設定はイメージ自体のデフォルトをモデルにしていますが、mailcow ターゲットのように動作させるために、意図的に 3 つの差異を設けています：

- イメージの sdbox ではなく、mailcow が使用している **Maildir ストレージ** を使用します。
- mailcow に合わせて **`separator = /`** を設定しています。リストア時にフォルダ階層を再作成する際、フォルダパスは `/` と `\` で分割されるため、`.` を使用するサーバーでは動作が異なります。
- mailcow が事前に作成するのと同様に、**special-use フォルダである `Drafts`、`Junk`、`Sent`、および `Trash` が事前に作成されています**。これは Exchange からの移行をテストする際に重要です。`Sent Items` という名前のソースフォルダは、既存の `Sent` とマージされるのではなく、その隣に配置されます。これがフォルダ名の変更（folder renaming）が対処すべき状況です。

アカウントの SSL が無効になっている場合、アプリケーションはトランスポートセキュリティなしで接続するため、ポート 1143 でプレーンテキスト認証が有効になっています。

## 🌱 テスト用のアーカイブのシーディング {#seeding-an-archive-to-test-against}

リストアや移行のテストを行う際、空のアーカイブではあまり役に立ちません。そのため、`tests/seed/seed.sh` は以下を作成します：

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```


これは2つのアカウントを作成し、フィクスチャをインポートして、何が取り込まれたかを表示します。両方のアカウントはアカウントごとの同期間隔が1年に設定されており、さらにソースが無効化されているため、バックグラウンド同期サービスはそれを無視します。つまり、そのサーバーは存在せず、Dovecot ターゲットには追加（append）されることしかありません。1つ注意点として、スケジューリング状態はメモリ内に保持されるため、アプリケーションを再起動するたびにターゲットが即座に1回スケジュールされます。そのため、ターゲットには Dovecot の special-use フォルダ（`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`）も含まれておらず、1回の再起動同期ではアーカイブすべきものが何も残らないようになっています。スクリプトを2回実行しても害はありません。2回目の実行では、すべてのメッセージが既に存在すると報告されます。

フィクスチャは `tests/seed/fixtures` にチェックインされており、ソースフォルダごとに1つの mbox があり、`manifest.tsv` によって各ファイルがターゲットフォルダにマッピングされています。これらはアプリケーション独自の `--import-mbox` CLI を通じてインポートされますが、これは意図的な詳細です。CLI は IMAP 同期で使用されるものと同じインポーターである `MailImporter.ImportEmailToDatabase` を介してルーティングされるため、データは実際のアーカイブと全く同じ形式で格納されます。具体的には、アドレスは `", "` で結合され、件名は `CleanText` を通過し、`SentDate` は表示タイムゾーンに変換され、`RawHeaders` は抽出および切り詰められます。データベースに直接行を挿入すると、これらすべてがスキップされ、本番データとは密かに異なるフィクスチャが生成されてしまいます。

各フィクスチャメッセージには、なぜそれが存在するのかを示す `X-Fixture-Purpose` ヘッダーが含まれており、ファイル自体がドキュメントとして機能します：

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```


日付は現在の日付からの相対的なものではなく、固定されています。移行のカットオフはジョブが実行される前に絶対日付に解決されるため、固定されたフィクスチャセットと固定されたカットオフを使用することで、スライディング（変動）する日付を使用するよりも予測可能な形で同じ内容をテストできます。メッセージの範囲は 2012 年から 2026 年にわたります。

フォルダ構成は Exchange のメールボックスを模しており、各フォルダは何らかの動作を確認するために存在します：

| Source folder | Why it is there |
|---|---|
| `INBOX` | 15年間にわたる日付、および以下のエッジケースのメッセージ |
| `Sent Items` | Dovecot ターゲットの既存の special-use `Sent` と衝突する |
| `Sent Items/2019` | リネーム時に先頭のパスセグメントのみを書き換える必要がある |
| `Sent Items Archive` | 部分文字列としてプレフィックスを共有しているが、セグメントの境界ではないため、書き換えられてはならない |
| `Deleted Items` | フォルダの除外 |
| `Deleted Items/Old` | 親フォルダの除外が子孫フォルダにも適用される必要がある |
| `Junk E-Mail` | Dovecot が `Junk` と呼ぶフォルダの Exchange での名前 |
| `Archive/Projects/2020` | 3レベルのネスト |

個々のメッセージは、`Message-ID` ヘッダーのないメッセージ（決定論的なフォールバックが適用される）、`Message-ID` に `@` がないメッセージ（リストア後に完全な状態で残らない）、制御文字を含む件名、3ホップの `Received` チェーンを持つメッセージと持たないメッセージ、2つの `To` 受信者を持つメッセージ数件、および1秒の間隔で存在する2つのほぼ重複したペアをカバーしています。

### 終了コードに関する注意 {#a-note-on-the-exit-code}

`tests/seed/seed.sh` は、プロセスの終了コードではなく、インポート出力からの失敗および不正な形式のカウントを確認します。`MBoxImportService` は、重複をスキップしたときに `CompletedWithErrors` を報告します。また、CLI は `Completed` 以外のすべてのステータスを終了コード 1 にマッピングするため、インポートを再実行すると、何も問題が発生しなくても終了コードが非ゼロになります。したがって、シードスクリプトを再実行しても安全であり、すべてのメッセージが既に存在すると報告されます。

## ⚠️ 本番環境用ではありません {#not-for-production}

- すべてのユーザー名は、単一の固定パスワードで Dovecot に対して認証されます。
- 平文認証が有効になっています。
- データベースの認証情報は、ドキュメントに記載されているデフォルト値です。
- メールおよびデータベースの状態は、`down -v` が削除する Docker ボリューム内に存在します。

## 🔧 トラブルシューティング {#troubleshooting}

**Tests fail with `Name or service not known`** — スタックが実行されていないか、
`appsettings.Test.json` が不足しているため、接続文字列がコンテナのデフォルトである
`Host=postgres` にフォールバックしています。

**`NETSDK1226` on build** — ローカルの .NET SDK 内の不完全なワークロードデータが原因であり、この
リポジトリとは無関係です。`dotnet workload repair` を実行するか、
`-p:AllowMissingPrunePackageData=true` を使用してビルドしてください。

**Dovecot restarts in a loop** — 設定エラーです。`docker compose -f
docker-compose.test.yml logs dovecot` を実行すると、エラーの原因となっている行が表示されます。Dovecot は、ブロック内の 1 行に複数の設定を記述することを許可しない点に注意してください。

**The application returns HTTP 500 on every request** — 通常はデータ保護キーリングが原因です。
`DataProtection:KeyPath` が書き込み可能なディレクトリを指しているか確認してください。
