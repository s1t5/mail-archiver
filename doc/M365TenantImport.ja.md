# Microsoft 365 Tenant Mailbox Import {#microsoft-365-tenant-mailbox-import}

[← Documentation Index に戻る](index.md)

## 概要 {#overview}

Mail Archiver では、単一の作成フォームから、同じテナント内の複数のメールボックスに対して Microsoft 365 メールアカウントを作成できます。これは、すべての、または選択されたサブセットの Microsoft 365 メールボックスをアーカイブする必要がある顧客や組織をオンボーディングする際に便利です。

テナントインポートは、フォームに設定された Microsoft Graph アプリケーション資格情報を使用し、インポートされたメールボックスごとに 1 つの Mail Archiver メールアカウントを作成します。

## 前提条件 {#prerequisites}

テナントインポートを使用する前に、[Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md) に記載されている Microsoft 365 アプリの登録セットアップを完了してください。

Microsoft Entra ID から以下の値が必要です。

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

アプリの登録には、Mail Archiver がテナントのメールボックスを読み取ることを許可する Microsoft Graph の **アプリケーションの許可 (application permissions)** が必要です。通常の M365 アカウントのアーカイブに必要な権限に加えて、テナントインポートには以下の権限が必要です。

- **User.Read.All** – テナントユーザーとそのメールボックスアドレスをリスト表示するために必要です。

作成フォームのプロバイダーとして **Microsoft 365 (tenant)** が選択されている場合にテナントインポートが使用されます。

### フィルタリング {#filtering}

テナントインポートおよびテナント管理では、以下が自動的に除外されます。
- **ゲストアカウント** (`userType == "Guest"`) — テナント内に独自のメールボックスを持たない、外部から招待されたユーザー。
- **Exchange Online ライセンスのないユーザー** — `assignedPlans` 内でアクティブな Exchange サービスプランを持つユーザーのみがリストに表示されます。これにより、アーカイブできないアカウントのインポートを回避します。

## インポートモード {#import-modes}

テナントインポートには 2 つのモードがあります。

### リストされたすべてのメールボックスをインポート {#import-all-listed-mailboxes}

Microsoft Graph から返されるすべてのメールボックスを Mail Archiver アカウントとして作成する場合にこのモードを使用します。

- Mail Archiver は Microsoft Graph からテナントのメールボックスリストを読み込みます。
- 既に存在する M365 メールアカウントはスキップされます。
- **Skip disabled mailboxes** が有効な場合、無効化されたユーザーはメールボックスリストに含まれません。

### 選択したメールボックスのみをインポート {#import-selected-mailboxes-only}

テナント内の特定のメールボックスのみをアーカイブする場合にこのモードを使用します。

- まずテナントのメールボックスリストを読み込みます。
- **Import all listed mailboxes** を無効にします。
- インポートするメールボックスを選択します。
- フォームを送信して、選択したメールボックスに対してのみアカウントを作成します。

## 使用手順 {#step-by-step-usage}

1. メールアカウントを作成できるユーザーとして Mail Archiver にサインインします。
2. **Mail Accounts** > **Create** を開きます。
3. プロバイダーとして **Microsoft 365 (tenant)** を選択します。
4. Microsoft 365 の資格情報を入力します。
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. 生成されるアカウントに使用する **Account name** プレフィックスを入力します。
6. 無効化されたメールボックスをスキップするかどうかを選択します。
7. **Load mailboxes** をクリックして、テナントのメールボックスをリスト表示します。
8. 希望するインポート動作を選択します。
   - リストされたすべてのメールボックスをインポートする場合は、**Import all listed mailboxes** を有効にしたままにします。
   - 特定のメールボックスのみをインポートする場合は、これを無効にして個別のメールボックスを選択します。
9. **Save** をクリックします。

作成後、Mail Archiver はメールアカウント一覧にリダイレクトされ、いくつのテナントメールアカウントがインポートされ、いくつの既存アカウントがスキップされたかを表示します。

## アカウントの命名 {#account-naming}

テナントインポートでは、Mail Archiver は入力された **Account name** の値を、作成されるすべてのメールボックスアカウントのプレフィックスとして使用します。

例:

- 入力されたアカウント名: `Test GmbH`
- テナントメールボックス: `mailbox@example.com`
- 作成されたアカウント名: `Test GmbH - <mailbox@example.com>`

これにより、同じテナントまたは顧客に属するインポートされたアカウントを識別しやすくなります。

## 既存のアカウント {#existing-accounts}

メールボックスが既に Microsoft 365 アカウントとして Mail Archiver に存在する場合、テナントインポートは重複を作成せずにスキップします。既存アカウントの検出は、メールボックスのメールアドレスと M365 プロバイダーに基づいています。

インポートを送信する前に、メールボックスが既に存在するかどうかをメールボックスリストで確認することもできます。

## 無効化されたメールボックス {#disabled-mailboxes}

**Skip disabled mailboxes** チェックボックスは、無効化されたテナントユーザーをメールボックスリストから除外するかどうかを制御します。

- 有効: 有効なテナントユーザーのみがリストに表示され、インポートされます。
- 無効: 無効化されたテナントユーザーがリストに表示される可能性があり、選択された場合、またはすべてのリストされたメールボックスがインポートされる場合にインポートされます。

## テナント管理 {#tenant-management}

作成フォームによるテナントインポートに加えて、Mail Archiver は管理者向けに専用の **Tenant Management** ページを提供しています。これは、Mail Accounts インデックスから、任意の Microsoft 365 アカウントの横にある **Tenant Management** ボタン（ビルアイコン）を介してアクセスできます。

テナント管理は、最初にインポートされなかったメールボックスを追加するのに便利です。例えば、初期オンボーディング後にテナント内で新しいメールボックスが作成された場合や、当初アカウントがスキップされた場合などが挙げられます。

### 作成フォームによるテナントインポートとの違い {#how-it-differs-from-the-create-form-tenant-import}

- Microsoft 365 の資格情報 (Client ID, Client Secret, Tenant ID) は**再入力する必要はありません**。ボタンをクリックしたソースアカウントから取得されます。
- ページを開くとテナントのメールボックスリストが自動的に読み込まれます（「Load mailboxes」ボタンはありません）。
- 既にインポート済みのメールボックスには「already exists」バッジが表示され、再度選択することはできません。
- 管理者のみが Tenant Management にアクセスできます。

### 使用手順 {#step-by-step-usage}

1. 管理者として Mail Archiver にサインインします。
2. **Mail Accounts** を開きます。
3. 管理したいテナントを持つ Microsoft 365 アカウントを見つけ、その横にある **Tenant Management** ボタンをクリックします。
4. ページが読み込まれ、ソースアカウントのテナントに対して Microsoft Graph から返されたすべてのメールボックスがリスト表示されます。
   - Mail Archiver に M365 アカウントとして既に存在するメールボックスには「already exists」バッジが表示され、選択できません。
   - 無効化されたテナントユーザーはリストに表示されません。Tenant Management は常に無効化されたメールボックスをスキップします。
5. 追加したいメールボックスを選択します。
6. 新しいアカウントに使用する **Account name** プレフィックスを入力します（命名規則は作成フォームのテナントインポートと同じ `{prefix} - <{email}>` です）。
7. 必要に応じて、新しいアカウントの **Delete After Days** および **Local Retention Days** を設定します。
8. **Add selected mailboxes** をクリックします。

作成後、Mail Archiver はメールアカウント一覧にリダイレクトされ、いくつのメールボックスが追加されたかを表示します。

### メールボックスの検索 {#searching-mailboxes}

数千のメールボックスを持つ大規模なテナントの場合、メールボックスリストの上部にある検索フィールドを使用して、表示名またはメールアドレスでフィルタリングできます。フィルターはケースインセンシティブ（大文字小文字を区別しない）で、入力に合わせて即座に適用されます。**Select all** および **Clear selection** は、現在表示されている（フィルタリングされた）メールボックスに対してのみ動作するため、フィルタリングは非表示のエントリに影響を与えることなく、特定のサブセットを選択するための安全な方法です。

### 既存アカウントの名称変更 {#renaming-existing-accounts}

**Rename existing accounts** チェックボックスをオンにすると、ソースアカウントと同じアプリ登録 (Client ID および Tenant ID) を共有するすべての M365 アカウントに命名規則 `{prefix} - <{email}>` が適用されます。これは、名前が不一致であった初期インポートの後に、アカウント名を正規化するのに役立ちます。

チェックを入れた状態でフォームを保存すると、新しいメールボックスがインポート用に選択されているかどうかにかかわらず、ソースアカウントを含むすべての該当するアカウントの名前が変更されます。アカウントを追加せずに名前を正規化したい場合は、このオプションのみを使用できます（新しいメールボックスを選択せずに）。

### メールボックスの制限 {#mailbox-limit}

単一の Tenant Management 操作では、一度に最大 `TenantManagement__MaxSelectedMailboxes` 個のメールボックスを追加できます（デフォルト `1000`）。これは、誤った大量インポートや、過度な Microsoft Graph / データベースへの負荷を防ぐためのものです。より多くのメールボックスをインポートする必要がある場合は、操作を複数回実行してください。この制限を調整する方法については、[Setup Guide](Setup.md#-tenant-management-settings) を参照してください。

### 必要な権限 {#permissions-required}

Tenant Management は、作成フォームのテナントインポートと同じ Microsoft Graph アプリケーションの許可を使用します。ソースアカウントのアプリ登録には以下が必要です。

- 通常の M365 アカウントのアーカイブに必要な権限、**および**
- **User.Read.All** — テナントユーザーとそのメールボックスアドレスをリスト表示するために必要です。

初期セットアップについては、[Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md) を参照してください。

### 監査ログ {#audit-logging}

各 Tenant Management ページの表示および各追加操作は、リストされたまたは追加されたメールボックスの数、および使用されたソースアカウントを含めて、アクセスログに記録されます。
