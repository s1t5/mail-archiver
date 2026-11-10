# ☁️ Azure App Registration and Retention Policy Guide for Mail Archiver {#azure-app-registration-and-retention-policy-guide-for-mail-archiver}

[← Documentation Index に戻る](index.md)

## 📋 概要 {#overview}

このガイドでは、メール取得のための Azure App Registration（アプリの登録）の設定、および Mail Archiver アプリケーションにおける保持ポリシーの設定に関する詳細な手順を説明します。

## 📚 目次 {#table-of-contents}

1. [概要](#overview)
2. [メール取得のための Azure App Registration](#azure-app-registration-for-email-retrieval)
   - [前提条件](#prerequisites)
   - [アプリの登録を作成する](#create-app-registration)
   - [必要な API 権限を設定する](#set-required-api-permissions)
   - [クライアント シークレットを生成する](#generate-client-secret)
3. [Mail Archiver M365 アカウントの設定](#configure-mail-archiver-m365-account)
4. [保持ポリシーの設定](#retention-policy-setup)
   - [保持ポリシーを構成する](#configure-retention-policy)

## 🌐 概要 {#overview}

Mail Archiver アプリケーションは、メール取得に OAuth2 クライアント クレデンシャル フローを使用する Microsoft 365 (M365) アカウントをサポートしています。さらに、指定された期間が経過した後にメールサーバーからメールを自動的に削除する保持ポリシー機能を提供しており、完全なアーカイブを維持しながらストレージ容量の管理を支援します。

## ☁️ メール取得のための Azure App Registration {#azure-app-registration-for-email-retrieval}

### 🛠️ 前提条件 {#prerequisites}

- Microsoft Entra ID (Azure AD) への管理者アクセス権
- Exchange Online ライセンスを持つ Microsoft 365 テナント

### 🚀 アプリの登録を作成する {#create-app-registration}

1. [Microsoft Entra 管理センター](https://entra.microsoft.com) に移動します
2. 管理者アカウントでサインインします
3. 左側のナビゲーション ペーンで、**App registrations** を選択します
4. App registrations ページの最上部にある **+ New registration** をクリックします
5. 次の詳細を入力します：
   - **Name**: 説明的な名前を入力します (例: "Mail Archiver M365 Provider")
   - **Supported account types**: **Accounts in this organizational directory only** (シングル テナント) を選択します
     - これは Mail Archiver で使用されるクライアント クレデンシャル フローに必要です
   - **Redirect URI**: 空欄のままにします (クライアント クレデンシャル フローには不要です)
6. **Register** をクリックします

**重要**: **Overview** ページから以下の値をメモしておいてください：
- **Application (client) ID - Mail Archiver では `ClientId` として使用します
- **Directory (tenant) ID - Mail Archiver では `TenantId` として使用します

### 🔐 必要な API 権限を設定する {#set-required-api-permissions}

1. 左メニューの **API permissions** に移動します
2. **+ Add a permission** をクリックします
3. **Microsoft Graph** を選択します
4. **Application permissions** を選択します (Mail Archiver のクライアント クレデンシャル フローに必要です)
5. 以下の権限を追加します：
   - **Mail.Read** - すべてのメールボックス内のメールを読み取る
   - **Mail.ReadWrite** - すべてのメールボックス内のメールを読み取りおよび書き込む (復元機能および削除機能のため)
   - **User.Read.All** - すべてのユーザーの完全なプロファイルを読み取る (Microsoft 365 テナントのメールボックス インポートに必要。また、Exchange ライセンス フィルタリング用の `assignedPlans` も提供します)

6. **Add permissions** をクリックします
7. **重要**: **Grant admin consent for [Your Organization]** をクリックします
8. **Yes** をクリックして確定します

**注意**: Mail Archiver はユーザーの操作なしにメールボックスにアクセスするためにクライアント クレデンシャル フローを使用するため、アプリケーションの権限が必要です。

### 🔑 クライアント シークレットを生成する {#generate-client-secret}

1. 左メニューの **Certificates & secrets** に移動します
2. **Client secrets** の下にある **+ New client secret** をクリックします
3. 説明を入力します (例: "Mail Archiver Secret")
4. 有効期限を選択します
5. **Add** をクリックします
6. **重要**: 生成された **Value** をすぐにコピーし、安全に保管してください。このシークレットは二度と表示されません。

## 📧 Mail Archiver M365 アカウントの設定 {#configure-mail-archiver-m365-account}

アプリの登録が完了したら、以下の値を使用して Mail Archiver に M365 メール アカウントを設定する必要があります。

### 📋 アプリの登録から取得する必須の値: {#required-values-from-app-registration}

1. **Client ID**: アプリの登録の **Overview** ページから **Application (client) ID** をコピーします
2. **Client Secret**: クライアント シークレット作成時に保存した **Value** をコピーします
3. **Tenant ID**: アプリの登録の **Overview** ページから **Directory (tenant) ID** をコピーします

### 🚀 Mail Archiver で M365 アカウントを作成する: {#creating-m365-account-in-mail-archiver}

1. Mail Archiver アプリケーションにログインします
2. **Mail Accounts** > **Create** に移動します
3. 以下のフィールドを入力します：
   - **Name**: アカウントの説明的な名前 (例: "Sales Team M365")
   - **Email Address**: アーカイブ対象のメールボックスのメールアドレス
   - **Provider**: **M365** を選択します
   - **Client ID**: アプリの登録から取得した Application (client) ID を入力します
   - **Client Secret**: 保存したクライアント シークレットの値を入力します
   - **Tenant ID**: アプリの登録から取得した Directory (tenant) ID を入力します

4. **Create** をクリックします

### ⚠️ 注意事項: {#important-notes}

- 単一の M365 アカウントを作成する場合、**Email Address** フィールドにはアーカイブしたい実際のメールボックスを指定する必要があります。
- アプリの登録は、指定されたメールボックスにアクセスするための権限を持っている必要があります。
- テナント内のすべての、または選択したメールボックスのアカウントを一度に作成するには、[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) ガイドを使用してください。


## 🗑️ 保持ポリシーの設定 {#retention-policy-setup}

Mail Archiver アプリケーションは、メールが正常にアーカイブされた後に IMAP サーバーからメールを自動的に削除する保持ポリシー機能を提供しており、完全なアーカイブを維持しながら IMAP サーバーのストレージ容量を管理するのに役立ちます。

### 🛠️ 保持ポリシーを構成する {#configure-retention-policy}

1. Mail Archiver アプリケーションの **Mail Accounts** に移動します
2. 新しい M365 アカウントを作成するか、既存のアカウントを編集します
3. アカウント設定フォーム内の **Delete After Days** フィールドを探します
4. アーカイブされたメールをサーバーから削除する日数を入力します：
   - 例: 30 日後にメールを削除する場合は `30` と入力します
   - 自動削除を無効にする場合は空欄のままにします
5. アカウント設定を保存します


## ℹ️ 移行済みメールボックスに関する情報 {#ℹ-information-regarding-migrated-mailboxes}

### 📁 移行後にフォルダが表示されない場合 {#folders-not-visible-after-migration}

他のメール プロバイダーから Microsoft 365 にメールボックスを移行した場合、一部のフォルダがフォルダ リストに表示されないことがあります。

#### 🔍 背景 {#background}

Mail Archiver は、Microsoft Graph API を介して M365 メールボックスからフォルダ情報を取得します。アプリケーションは、利用可能なすべてのフォルダ (非表示フォルダを含む) を正しくリクエストし、完全なフォルダ階層を辿ります。

しかし、Microsoft Graph API は、`PR_CONTAINER_CLASS` が `IPF.Note` に設定されているフォルダのみを返します。他のメール システムからメールボックスを移行する際、フォルダに `IPF.Imap` などの異なるコンテナ クラスが割り当てられることがあります。これらのフォルダは、Graph API のフォルダ リスト エンドポイントには公開されません。

この動作は、フォルダ取得にこの API を使用しているすべてのアプリケーションに影響します。

#### 🔧 回避策 {#workaround-options}

**オプション 1: MFCMAPI ツールを使用する (推奨)**

1. Microsoft の公式リポジトリから [MFCMAPI](https://github.com/Microsoft/mfcmapi/) をダウンロードします
2. MFCMAPI を起動し、メールボックスに接続します
3. 見当たらない各フォルダに移動します
4. `PR_CONTAINER_CLASS` プロパティを `IPF.Imap` から `IPF.Note` に変更します
5. Outlook を再起動し、Mail Archiver でフォルダを再読み込みします

**オプション 2: Outlook でフォルダを再作成する**

1. Outlook で直接新しいフォルダを作成します (これらは正しい `IPF.Note` クラスを持ちます)
2. 移行されたフォルダ内のすべてのメールを新しいフォルダに移動します
3. 必要に応じて、古い移行済みフォルダを削除します

**オプション 3: PowerShell スクリプト (一括変更用)**

多くの移行済みフォルダがあるメールボックスの場合、Exchange Online モジュールを使用した PowerShell を使用して、複数のフォルダのコンテナ クラスを一度に変更できます。この方法については、Exchange 管理者に相談してください。


---

**注意**: このガイドは 2025 年時点のものです。Microsoft は定期的にサービスを更新しているため、UI 要素が異なる場合があります。常に最新の Microsoft ドキュメントを参照してください。
