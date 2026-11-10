# 🚨 緊急アカウント復旧 {#emergency-account-recovery}

[← Documentation Index に戻る](index.md)

## 📋 概要 {#overview}

このガイドでは、管理者パスワードを忘れた場合に Mail Archiver アプリケーションへのアクセスを復旧するための詳細な手順を提供します。この緊急手順を使用することで、データを失うことなくシステムへのアクセスを再開できます。

> 📝 **バージョン要件**: この復旧方法は Mail Archiver バージョン 2601.1 以降で利用可能です。

## ⚠️ 重要なセキュリティ通知 {#important-security-notice}

この復旧方法は、管理者アカウントにアクセスできない緊急事態にのみ使用してください。これにはアプリケーション設定の一時的な変更が必要であり、パスワード復旧後には直ちに元の状態に戻す必要があります。

## 🛠️ 復旧手順 {#recovery-steps}

### 1. 緊急管理者アカウントの作成 {#1-create-emergency-administrator-account}

`appsettings.json` または Docker の環境変数を変更して、一時的な緊急管理者アカウントを作成します。

**Docker Compose の場合 (推奨):**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Emergency admin account
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempPass123!
      # ... other settings remain unchanged
```

**appsettings.json を直接変更する場合:**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. アプリケーション/コンテナの再起動 {#2-restart-the-applicationcontainer}

設定を変更した後、アプリケーションを再起動します。

**Docker Compose の場合:**
```bash
docker compose down
docker compose up -d
```

**直接アプリケーションを実行している場合:**
```bash
# Restart your application service
```

### 3. 緊急アカウントでのログイン {#3-login-with-emergency-account}

1. Mail Archiver の Web インターフェースにアクセスします
2. 緊急の認証情報を使用してログインします：
   - **Username**: `emergency_admin` (または設定したもの)
   - **Password**: `TempPass123!` (または設定したもの)

### 4. 元の管理者パスワードのリセット {#4-reset-original-administrator-password}

1. メインメニューから "Users" セクションに移動します
2. ユーザーリストから元の管理者アカウントを探します
3. そのユーザーの "Edit" をクリックします
4. パスワードを新しい安全なパスワードに変更します
5. "Save Changes" をクリックします

### 5. 元の設定への復元 {#5-restore-original-configuration}

設定の変更を元の管理者ユーザー名に戻します。

**Docker Compose の場合:**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Original admin account
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword
      # ... other settings
```

**appsettings.json を直接変更する場合:**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. 再度アプリケーションを再起動 {#6-restart-application-again}

復元された設定でアプリケーションを再起動します。

**Docker Compose の場合:**
```bash
docker compose down
docker compose up -d
```

## 🎯 例: Docker Compose を使用した完全な復旧プロセス {#example-complete-docker-compose-recovery-process}

以下は、Docker Compose を使用した緊急復旧プロセスの完全な例です。

### ステップ 1: 緊急設定 {#step-1-emergency-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # EMERGENCY ADMIN ACCOUNT
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempEmergencyPass2026!
      
      # Other settings remain unchanged...
      - MailSync__IntervalMinutes=15
      - TimeZone__DisplayTimeZoneId=Europe/Berlin
      # ... rest of configuration
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### ステップ 2: 再起動とログイン {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### ステップ 3: 元のパスワードのリセット {#step-3-reset-original-password}
1. 緊急の認証情報を使用して Web インターフェースにログインします
2. Users セクションに移動します
3. 元の管理者ユーザーを編集し、新しいパスワードを設定します
4. 変更を保存します

### ステップ 4: 通常の設定への復元 {#step-4-restore-normal-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # RESTORED ORIGINAL ADMIN ACCOUNT
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword2026!
      
      # ... rest of configuration unchanged
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### ステップ 5: 最終的な再起動 {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 紛失した認証情報暗号化キー {#lost-credential-encryption-key}

`Security__CredentialEncryptionKey` (保存時の認証情報暗号化) が失われると、保存されているメールアカウントの認証情報を復号できなくなります。アプリケーションはクラッシュ**しません**。影響を受ける値は「認証情報なし」として扱われ、警告がログに記録されます。復旧するには：

1. バックアップまたはシークレットマネージャーからキーを復元し、アプリケーションを再起動します。認証情報はすぐに再び利用可能になり、データの変更は必要ありません。
2. キーが永久に失われた場合、暗号化された認証情報を復元することはできません。影響を受けるアカウントの特定は容易です。同期に失敗し、アプリケーションログに復号警告が書き込まれます（UI 上では同期の問題として表示されます）。
3. 影響を受ける各アカウントを **Mail Accounts → Edit** で開き、認証情報を再入力します：IMAP パスワード、M365 クライアントシークレット、またはアカウントの再認可 (MSA) を行います。フォームを保存することで、現在設定されているキーで認証情報が再暗号化されます。手動でのデータベース変更は不要です。

> 💡 キーはデータベースには保存されないため、データベースのバックアップだけでは不十分です。必ずキーを別途バックアップしてください。

## 🔒 セキュリティのベストプラクティス {#security-best-practices}

1. **強力な一時パスワードを使用する**: 緊急用パスワードは複雑で一意なものにしてください
2. **直ちに変更する**: アクセス権を回復したら、すぐに緊急用パスワードをリセットしてください
