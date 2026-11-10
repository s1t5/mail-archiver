# ☁️ 個人用 Microsoft アカウント (Outlook.com / M365 Family) のセットアップ {#personal-microsoft-account-outlookcom-m365-family-setup}

[← ドキュメントインデックスに戻る](index.md)

## 📋 概要 {#overview}

このガイドでは、Mail Archiver で **個人用 Microsoft アカウント** (Outlook.com、Hotmail、live.com、または Microsoft 365 Family) のメールをアーカイブする方法を説明します。

組織用の Microsoft 365 アカウント (client-credentials フローを使用 — [M365 用 Azure App Registration](AZURE_APP_REGISTRATION_M365.md) を参照) とは異なり、個人用アカウントでは **Device Code Flow (RFC 8628) を使用した OAuth2** が使用されます。Microsoft は個人用アカウントにおける Basic Auth およびアプリ パスワードを廃止しているため、OAuth2 が必須となります。

Mail Archiver には **事前登録済みの共有 Client ID** が同梱されているため、ほとんどのユーザーは独自の Azure App Registration を作成する **必要はありません**。パワーユーザーは、引き続き独自のアプリを登録することも可能です (後述のオプション B を参照)。

## 📚 目次 {#table-of-contents}

1. [オプション A: 標準セットアップ (推奨)](#option-a-standard-setup-recommended)
2. [オプション B: カスタムアプリ登録 (パワーユーザー向け)](#option-b-custom-app-registration-power-users)
3. [トークンの更新と取り消し](#token-refresh--revocation)
4. [重要な注意事項](#important-notes)
5. [トラブルシューティング](#troubleshooting)

---

## 🚀 オプション A: 標準セットアップ (推奨) {#option-a-standard-setup-recommended}

Mail Archiver インスタンスにデフォルトの Client ID が設定されている場合 (公式 Docker イメージのデフォルト) は、このオプションを使用してください。Azure Portal へのアクセスは不要です。

### 🛠️ 前提条件 {#prerequisites}

- 個人用 Microsoft アカウント (Outlook.com、live.com、または M365 Family)
- 認証を完了するためのブラウザを備えたデバイス (スマートフォン、ノート PC、またはタブレット)

### 📝 手順 {#steps}

1. Mail Archiver アプリケーションにログインします。
2. **Mail Accounts** > **Create** に移動します。
3. 以下のフィールドを入力します：
   - **Name**: アカウントの識別名 (例: "Personal Outlook")
   - **Email Address**: あなたの個人用 Microsoft メールアドレス (例: `you@outlook.com`)
   - **Provider**: **Microsoft Personal** を選択
4. Client ID や Client Secret のフィールドは表示されません — Mail Archiver は組み込みのデフォルト登録を自動的に使用します。
5. **Create** をクリックします。
6. デバイスコード認証ページが自動的に開きます。短いコード (例: `ABCD-EFGH`) が、`https://microsoft.com/devicelogin` へのリンクとともに表示されます (または表示されている検証 URL)。
7. ブラウザを備えた **任意のデバイス** でその URL を開き、個人の Microsoft アカウントでサインインします。
8. 手順 6 のコードを入力します。
9. 承諾画面が表示され、**"Mail-Archiver"** が IMAP アクセスを要求していることが示されます。内容を確認し、承認します。
10. Mail Archiver のページが成功を自動的に検出し、アカウント編集ビューにリダイレクトされます。
11. 完了 — アカウント設定に従って同期が自動的に開始されます。

> ⚠️ **未検証のパブリッシャーに関する警告**: 承諾画面に "Mail-Archiver" が未検証のパブリッシャーであるという警告が表示される場合があります。これは想定内の動作です。パブリッシャーの検証にはビジネス ID (Microsoft Partner Network アカウント) が必要ですが、フローを動作させるためにこれが必要なわけではありません。アプリはこれなしでも正常に機能します。

---

## 🔧 オプション B: カスタムアプリ登録 (パワーユーザー向け) {#option-b-custom-app-registration-power-users}

共有のデフォルト Client ID を使用したくない場合 (コンプライアンス上の理由や、アプリ登録を完全に制御したい場合など) は、このオプションを使用してください。

### 🛠️ 前提条件 {#prerequisites}

- Azure Portal にアクセス可能な Microsoft アカウント (個人用または職場/学校用)
- Azure/Entra ポータルに関する基本的な知識

### 🚀 アプリの登録を作成する {#create-app-registration}

1. [Microsoft Entra 管理センター](https://entra.microsoft.com) または [Azure Portal](https://portal.azure.com) に移動します。
2. Microsoft アカウントでサインインします。
3. 左側のナビゲーション ペーンで、(Identity > Applications の下にある) **App registrations** を選択します。
4. **+ New registration** をクリックします。
5. 以下の詳細を入力します：
   - **Name**: 識別名を入力 (例: "Mail Archiver MSA")
   - **Supported account types**: **Accounts in any organizational directory and personal Microsoft accounts** を選択
     - これはマルチテナント オプションであり、個人用および組織用アカウントの両方をサポートします。
   - **Redirect URI**: 空欄のままにします (Device Code Flow では不要です)。
6. **Register** をクリックします。
7. **Overview** ページから **Application (client) ID** をメモしておきます。

### 🔐 パブリック クライアント フローを有効にする {#enable-public-client-flows}

1. アプリ登録内で、左メニューの **Authentication** に移動します。
2. 下部にある **Allow public client flows** を探し、**Yes** に設定します。
3. **Save** をクリックします。

> ℹ️ Client secret、Redirect URI、および API permissions を手動で設定する必要はありません — Device Code Flow は、実行時に `IMAP.AccessAsUser.All` スコープを介して要求される委任された権限を使用します。

### 📧 Mail Archiver の設定 {#configure-mail-archiver}

グローバルなデフォルト設定を上書きします (すべての MSA アカウントに影響します)。

環境変数に Client ID を設定してください：


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 トークンの更新と取り消し {#token-refresh-revocation}

### 自動トークン更新 {#automatic-token-refresh}

初回認証後、Mail Archiver は以下を受け取ります：
- **access token** (短寿命、約 1 時間)
- **refresh token** (長寿命、新しい access token を自動的に取得するために使用)

Mail Archiver は、各同期実行の前に access token を自動的に更新します。refresh token が期限切れになるか取り消されない限り、再認証の必要はありません。

### アクセス権の取り消し {#revoking-access}

Mail Archiver の個人用 Microsoft アカウントへのアクセス権を取り消すには：

1. [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage) にアクセスします。
2. 個人の Microsoft アカウントでサインインします。
3. リストから **"Mail-Archiver"** (または作成したカスタムアプリ名) を探します。
4. **Remove** をクリックしてアクセス権を取り消します。

取り消し後、再認証するまで Mail Archiver はアカウントにアクセスできなくなります。

---

## ⚠️ 重要な注意事項 {#important-notes}
- **共有 Client ID**: デフォルトの Client ID を使用する場合、すべてのインスタンスのすべての Mail Archiver ユーザーが同じ Azure App Registration を共有します。Mail Archiver のメンテナーは、登録を有効に保つ責任を負います。もし Microsoft が共有登録を無効にした場合、新しいリリースで更新された Client ID が提供されるまで、すべてのユーザーはカスタム登録 (オプション B) に切り替える必要があります。


- **組織用アカウント**: Microsoft Personal プロバイダーは `/common` 認証エンドポイントを使用し、個人用および組織用 (職場/学校) アカウントの両方をサポートしています。client-credentials アクセス (アプリ専用、ユーザーのサインインなし) が必要な組織用アカウントの場合は、代わりに **M365** プロバイダーを使用してください。

---

## 🔍 トラブルシューティング {#troubleshooting}

### outlook.com ではフォルダが見えるが、Mail Archiver で見当たらない場合 {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**症状**: メールボックスにフォルダが存在する (outlook.com または Outlook で確認可能) が、Mail Archiver には表示されない — アカウント設定ページのフォルダリストにも、同期中にも表示されません。エラーログも出力されません。

**原因 (サーバー側)**: **サードパーティ製クライアントが EWS を通じて作成した** フォルダ (これにより、outlook.com アカウントが IMAP ではなく Exchange アカウントとしてセットアップされます) は、誤った `PR_CONTAINER_CLASS` プロパティ (例: `IPF.Note` ではなく `IPF.Imap`) を持つことがあります。すると Outlook.com は、Web UI では完全に表示されているにもかかわらず、**IMAP `LIST` レスポンスからこれらのフォルダを完全に除外してしまいます**。これは Microsoft 側の動作であり、Mail Archiver のバグではありません。同様の現象は Microsoft Graph API でも文書化されており、non-`IPF.Note` の container class を持つフォルダは、プロパティが修正されるまで `mailFolders` リスティングから欠落します。

Mail Archiver は、3 つのソース (再帰的な `LIST`、すべてのフォルダの子要素のレベルごとのトラバーサル、および `LSUB`) からフォルダリストを照会し、結果をマージします。サーバーがいずれのソースからもフォルダを報告しない場合、IMAP クライアントがそれを見る方法はありません。

**回避策**: outlook.com の Web UI を通じて、該当するフォルダに一度アクセス (Touch) して、Microsoft に container class を修正させます。

1. <https://outlook.com> を開き、見当たらないフォルダを探します。
2. 名前を変更 (一時的な名前で可) して元に戻すか、新しいフォルダを作成してメールを移動します。
3. すべての該当するフォルダに対してこれを繰り返します。
4. Mail Archiver の Account Details ページから、アカウントの **Full Resync** を実行します。増分同期はアカウントの最終同期日より新しいメールのみを取得するため、一度も検出されなかったフォルダ内のメールをアーカイブするには、フルリシンクが必要です。


*このガイドは 2026 年時点のものです。Microsoft は定期的にサービスと UI を更新するため、手順が異なる場合があります。最新の詳細については [Microsoft identity platform documentation](https://learn.microsoft.com/en-us/entra/identity-platform/) を参照してください。*
