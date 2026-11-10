# 🔐 Two-Factor Authentication {#two-factor-authentication}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

2要素認証 (2FA) は、ログインプロセスに2番目のステップを追加します。パスワードの入力後、Mail Archiver は電話の認証アプリによって生成された **6桁のコード** を要求します。これにより、パスワードが盗まれたとしても、それだけではアーカイブにアクセスできなくなります。

Mail Archiver は **TOTP** (Time-based One-Time Password, RFC 6238) を使用して 2FA を実装しています。これは Google Authenticator、Microsoft Authenticator、Authy、1Password、Bitwarden などで使用されているものと同じ標準規格です。外部サービスは介在せず、シークレットがインスタンスの外に出ることはありません。

**2FA はユーザーごとのオプトイン方式です。** 管理者によってグローバルに設定されるのではなく、各ユーザーが自身のカウントに対して設定します。

## 🛠️ Prerequisites {#prerequisites}

- 電話またはデスクトップ上の TOTP 対応認証アプリ
- 自身のカウントへのアクセス権（ログインしている必要があります）
- **ローカル (パスワード) アカウント** — 下記の OIDC に関する注記を参照してください

> **⚠️ OIDC ユーザーは利用できません。** 外部の ID プロバイダー (Entra ID, Authelia, Keycloak, …) を通じてサインインしている場合、2FA はそのプロバイダーによって管理されます。その場合、設定ページには *"OIDC users cannot enable 2FA. Authentication is managed by your OIDC provider."* というメッセージが表示され、ブロックされます。代わりに、使用している ID プロバイダーで多要素認証を有効にしてください — [OIDC / SSO](OIDC_Implementation.md) を参照してください。

## 🚀 Enabling Two-Factor Authentication {#enabling-two-factor-authentication}

### Step 1 — Open the setup page {#step-1-open-the-setup-page}

1. Mail Archiver にログインします
2. アカウントメニューを開き、**Two-Factor Authentication** を選択します
3. **Two-Factor Setup** ページが表示されます

### Step 2 — Scan the QR code {#step-2-scan-the-qr-code}

ページに QR コードが表示されます。認証アプリを開き、*アカウントを追加* → *QR コードをスキャン* を選択して、カメラを QR コードに向けてください。

**コードをスキャンできない場合:** 代わりに **Manual Entry** を使用してください。ページにテキストとしてシークレットが表示されます (`If you can't scan the QR code, enter this secret manually in your authenticator app`)。これをアプリに時間ベース/TOTP アカウントとして入力してください。

> **ℹ️ シークレットはまだ保存されていません。** これはブラウザのセッション中、セットアップの間だけ保持されます。2FA が有効になるのはステップ 3 が成功した後のみです。セットアップを中断しても、アカウントに変化はありません。

### Step 3 — Confirm with a code {#step-3-confirm-with-a-code}

アプリに表示されている 6 桁のコードを入力し、**Enable Two-Factor Authentication** をクリックします。

コードが拒否された場合、新しい QR コードとともに *"Invalid authentication code. Please try again."* というメッセージが表示され、セットアップページが再度表示されます。電話の時計が自動設定されていることを確認してください。TOTP は正確な時刻に依存します。

### Step 4 — Save your backup codes {#step-4-save-your-backup-codes}

成功すると、**Backup Codes** ページに移動します。ここには、それぞれ 16 文字の **10 個のコード** が表示されます。

> **⚠️ コードが表示されるのはこの時だけです。** コードはハッシュ化されて保存されるため、後であなたや管理者がそれらを表示することはできません。**Download Backup Codes** を使用してファイルに保存するか、ページを離れる前にパスワードマネージャーにコピーしてください。

ダウンロードされるファイルには、以下のリマインダーが含まれています：

> *Mail Archiver - Two-Factor Authentication Backup Codes. Store these backup codes in a safe place. You can use them to log in if you lose access to your authenticator app. Each backup code can only be used once. After using a code, it will be invalidated.*
> *Important: Keep these codes secure and do not share them with anyone.*

## 🔑 Logging In with 2FA {#logging-in-with-2fa}

パスワードを入力した後、**Two-Factor Verification** ページに移動し、コードを求められます：

> *Enter a 6-digit TOTP code from your authenticator app or a 16-character backup code*

どちらも同じフィールドで使用できます。Mail Archiver は長さによってこれらを区別します：

| Input | Length | Behaviour |
|---|---|---|
| 認証アプリからのコード | 6 桁 | 現在の時刻ウィンドウに対して検証されます |
| バックアップコード | 16 文字 | 保存されたコードに対して検証され、その後 **無効化** されます |

時計のわずかなずれを許容するため、直前または直後のタイムステップのコードも受け入れられます。

**"Remember me"** はログインフォームの設定を引き継ぎます。これにチェックを入れていた場合、2FA ステップを完了した後は、ブラウザを再起動してもサインイン状態が維持されます。この選択は 2FA ステップの待機中、サーバー側のセッションに保持されます。そのため、セッションの **60 分間** のアイドルタイムアウト以内にコードを入力した場合のみ有効となります。詳細と制限（7 日間の絶対的な有効期間、OIDC ユーザーには適用されない）については、[User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox) を参照してください。

### Using a backup code {#using-a-backup-code}

1. 保存した 16 文字のコードのいずれかを同じフィールドに入力します
2. コードは受理され、**直ちに無効化** されます — 再利用はできません
3. バックアップコードを使用したログインの成功は、アクセスログに `(2FA Backup Code)` という注記とともに記録されるため、後で異常な使用を特定できます

### Rate limiting {#rate-limiting}

ブルートフォース攻撃を遅延させるため、2FA の検証は IP アドレスとユーザー名ごとに **15 分間に 5 回まで** に制限されています。制限を超えると、以下のメッセージが表示されます：

> **Too Many Attempts** — *You have made too many 2FA verification attempts. Please wait and try again later.*

制限時間が経過するまで待ってから、再度試行してください。この制限は、ウィンドウ内の失敗した試行と成功した試行の両方に適用されます。

## 🚫 Disabling Two-Factor Authentication {#disabling-two-factor-authentication}

1. アカウントメニューで **Two-Factor Authentication** を開きます
2. 確認メッセージが表示されます：*"Are you sure you want to disable two-factor authentication? This will reduce the security of your account."*
3. **パスワード** を入力します — このステップでは、2FA コードではなく現在のパスワードが必要です
4. **Disable Two-Factor Authentication** をクリックします

無効化すると、シークレットが削除され、残っているすべてのバックアップコードが無効になります。次回のログインにはパスワードのみが必要になります。

## 🔄 Lost Your Authenticator? Two Ways Back In {#lost-your-authenticator-two-ways-back-in}

### If you still have backup codes {#if-you-still-have-backup-codes}

上述の方法でバックアップコードを使用してログインし、その後、新しいデバイスで 2FA を無効化して再度有効にしてください。新しいバックアップコードを保存することを忘れないでください。

### If you have no backup codes left {#if-you-have-no-backup-codes-left}

**管理者** がアカウントの 2FA をリセットする必要があります：

1. 管理者が **Users** を開きます
2. 対象のユーザーを編集します
3. **Reset Two-Factor Authentication** を選択し、以下を確認します：
   *"Are you sure you want to reset two-factor authentication for this user? They will need to set it up again."*
4. その後、ユーザーはパスワードのみでログインし、2FA を再度設定できます

リセットにより、シークレットとバックアップコードがクリアされます。Users リストに *"Two-factor authentication has been successfully reset for user …"* という確認メッセージが表示されます。

> **ℹ️ リセットの範囲。** リセットは 2FA のみに適用されます。パスワードの再設定では **ありません**。パスワードが問題である場合や、アカウントが完全にロックアウトされている場合は、[Emergency Account Recovery](EmergencyAccountRecovery.md) を参照してください。

## 👁️ What Administrators Can See {#what-administrators-can-see}

**Users** の概要には 2FA カラムが表示されます：

- *"Two-factor authentication is currently enabled for this user."* — 2FA が有効です
- それ以外の場合、そのユーザーの 2FA はオフです

管理者は 2FA をリセット（上記）できますが、シークレットやバックアップコードを**読み取ることはできません**。どちらもハッシュ化されて保存されています。バックアップコードなしでデバイスを紛失したユーザーは、管理者のリセット以外に復旧する方法はありません。

## 🔍 Audit Trail {#audit-trail}

2FA のアクティビティはログに書き込まれ、ログイン成功時にはアクセスログにも書き込まれます：

| Event | Where | Entry |
|---|---|---|
| 認証アプリ経由のログイン | Access log | `Login` (注記 `IP: <address> (2FA)` 付き) |
| バックアップコード経由のログイン | Access log | `Login` (注記 `IP: <address> (2FA Backup Code)` 付き) |
| 2FA 検証の失敗 | Application log | ユーザー名、IP、およびマスクされたトークンを含む Warning |
| 無効なバックアップコード | Application log | ユーザー名と IP を含む Warning |

アクセスログのエントリにより、バックアップコードの使用が後から確認できます。これは、本来持っているべきではない人物によってコードが使用されたことを検知するのに役立ちます。これらのエントリの読み方は [Access Logging](Logs.md) を参照してください。

## ⚙️ How It Works Under the Hood {#how-it-works-under-the-hood}

管理者およびレビュアー向け：

- **Standard**: TOTP (RFC 6238), 20-byte random secret, base32-encoded
- **Verification window**: 時刻のずれを許容するため ±1 タイムステップ
- **Backup codes**: 10 個のコード、各 16 文字、暗号学的 RNG から生成、**ハッシュ化** して保存、使用時に個別に無効化
- **Secret storage**: ユーザーレコード内に保存。セットアップ後にブラウザへ返されることはありません
- **Session handling**: 2FA の認証情報が保存される前にセッションが再生成されるため、ログイン前に設置されたセッションクッキーが認証済みセッションに引き継がれることはありません
- **OIDC accounts**: 2FA 管理はブロックされます。プロバイダーが権限を持ちます
- **API keys**: 設計上 2FA をバイパスします。非対話型トークンはチャレンジに回答できないためです。セキュリティ上の根拠については [REST API guide](API.md) を参照してください

## ❓ Troubleshooting {#troubleshooting}

| Symptom | Cause | Fix |
|---|---|---|
| *"Invalid authentication code"* | 電話の時計のずれ、またはアプリ内の誤ったアカウント | 時刻の自動同期を有効にする。アプリに Mail Archiver のエントリが表示されているか確認する |
| *"Two-factor secret is missing. Please restart the setup process."* | セットアップページを開いたまま時間が経過し、セッションが切れた | アカウントメニューからセットアップをやり直す |
| *"Your session has expired. Please log in again."* | 検証ページで時間が経過しすぎた | 再度ログインする |
| *"Two-factor authentication is not enabled for this account."* | 2FA がリセットされたか、完了していない | 再度設定するか、管理者に依頼する |
| Backup code rejected | コードが既に使用されているか、文字の読み間違い | バックアップコードは **一度限り** です。別のコードを使用するか、2FA をリセットしてください |
| *"Too Many Attempts"* | 15 分間に 5 回を超える検証試行 | 制限時間が経過するまで待つ |
| アカウントメニューにセットアップページがない | OIDC を介してサインインしている | ID プロバイダーで MFA を有効にする |
| Users リストに 2FA カラムがない | 管理者ではない | 管理者に依頼する |

## 📚 Related Documentation {#related-documentation}

- [User Management and Mailbox Permissions](UserManagement.md) — アカウント作成、管理者権限
- [OIDC / SSO Authentication](OIDC_Implementation.md) — 外部プロバイダー経由のサインイン
- [Emergency Account Recovery](EmergencyAccountRecovery.md) — ロックアウトされたアカウント
- [Access Logging](Logs.md) — 監査トレイルの読み方
- [REST API](API.md) — なぜ API キーが 2FA をバイパスするのか
