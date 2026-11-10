# 🚨 紧急账户恢复 {#emergency-account-recovery}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

本指南提供了在忘记管理员密码时，恢复 Mail Archiver 应用程序访问权限的详细说明。此紧急程序允许您在不丢失任何数据的情况下重新获得系统访问权限。

> 📝 **版本要求**：此恢复方法适用于 Mail Archiver 2601.1 及更高版本。

## ⚠️ 重要安全提示 {#important-security-notice}

此恢复方法仅应在无法访问管理员账户的紧急情况下使用。它需要临时修改您的应用程序配置，并在密码恢复后应立即撤销修改。

## 🛠️ 恢复步骤 {#recovery-steps}

### 1. 创建紧急管理员账户 {#1-create-emergency-administrator-account}

修改您的 `appsettings.json` 或 Docker 环境变量，以创建一个临时的紧急管理员账户：

**对于 Docker Compose（推荐）：**
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

**对于直接修改 appsettings.json：**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. 重启应用程序/容器 {#2-restart-the-applicationcontainer}

修改配置后，请重启您的应用程序：

**对于 Docker Compose：**
```bash
docker compose down
docker compose up -d
```

**对于直接运行的应用程序：**
```bash
# Restart your application service
```

### 3. 使用紧急账户登录 {#3-login-with-emergency-account}

1. 访问 Mail Archiver Web 界面
2. 使用紧急凭据登录：
   - **用户名**：`emergency_admin`（或您设置的任何内容）
   - **密码**：`TempPass123!`（或您设置的任何内容）

### 4. 重置原管理员密码 {#4-reset-original-administrator-password}

1. 从主菜单导航至“Users”部分
2. 在用户列表中找到您原有的管理员账户
3. 点击该用户的“Edit”
4. 将密码更改为新的安全密码
5. 点击“Save Changes”

### 5. 还原原始配置 {#5-restore-original-configuration}

将您的配置更改还原回原始管理员用户名：

**对于 Docker Compose：**
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

**对于直接修改 appsettings.json：**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. 再次重启应用程序 {#6-restart-application-again}

使用还原后的配置重启应用程序：

**对于 Docker Compose：**
```bash
docker compose down
docker compose up -d
```

## 🎯 示例：完整的 Docker Compose 恢复流程 {#example-complete-docker-compose-recovery-process}

以下是使用 Docker Compose 进行紧急恢复流程的完整示例：

### 第 1 步：紧急配置 {#step-1-emergency-configuration}
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

### 第 2 步：重启并登录 {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### 第 3 步：重置原密码 {#step-3-reset-original-password}
1. 使用紧急凭据登录 Web 界面
2. 进入 Users 部分
3. 编辑原管理员用户并设置新密码
4. 保存更改

### 第 4 步：还原正常配置 {#step-4-restore-normal-configuration}
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

### 第 5 步：最终重启 {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 丢失凭据加密密钥 {#lost-credential-encryption-key}

如果 `Security__CredentialEncryptionKey`（静态凭据加密）丢失，存储的邮件账户凭据将无法再被解密。应用程序**不会**崩溃：受影响的值将被视为“无凭据”，并记录一条警告。如需恢复：

1. 从您的备份/密钥管理器中恢复密钥并重启应用程序。凭据会立即恢复正常工作，无需更改数据。
2. 如果密钥永久丢失，则加密的凭据无法恢复。受影响的账户很容易识别：它们的同步会失败，并且应用程序日志中会写入解密警告（并在 UI 中显示为同步问题）。
3. 打开每个受影响账户下的 **Mail Accounts → Edit** 并重新输入其凭据：IMAP 密码、M365 client secret，或重新授权账户 (MSA)。保存表单会将凭据使用当前配置的密钥重新加密 —— 无需手动更改数据库。

> 💡 密钥永远不会存储在数据库中，因此仅靠数据库备份是不够的 —— 请务必单独备份密钥。

## 🔒 安全最佳实践 {#security-best-practices}

1. **使用强临时密码**：确保您的紧急密码复杂且唯一
2. **立即更改**：一旦重新获得访问权限，请立即重置紧急密码
