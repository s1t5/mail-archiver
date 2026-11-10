# ☁️ 个人 Microsoft 账户 (Outlook.com / M365 Family) 设置 {#personal-microsoft-account-outlookcom-m365-family-setup}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

本指南说明如何在 Mail Archiver 中归档**个人 Microsoft 账户**（Outlook.com、Hotmail、live.com 或 Microsoft 365 Family）的电子邮件。

与组织级 Microsoft 365 账户（使用客户端凭据流 — 参见 [Azure App Registration for M365](AZURE_APP_REGISTRATION_M365.md)）不同，个人账户使用 **带有设备代码流 (Device Code Flow, RFC 8628) 的 OAuth2**。Microsoft 已弃用个人账户的 Basic Auth 和应用密码，因此必须使用 OAuth2。

Mail Archiver 自带了一个**预注册的共享 Client ID**，因此大多数用户**不需要**创建自己的 Azure App Registration。高级用户仍可以注册自己的应用（参见下方的选项 B）。

## 📚 目录 {#table-of-contents}

1. [选项 A：标准设置（推荐）](#option-a-standard-setup-recommended)
2. [选项 B：自定义应用注册（高级用户）](#option-b-custom-app-registration-power-users)
3. [令牌刷新与撤销](#token-refresh--revocation)
4. [重要注意事项](#important-notes)
5. [故障排除](#troubleshooting)

---

## 🚀 选项 A：标准设置（推荐） {#option-a-standard-setup-recommended}

如果您的 Mail Archiver 实例配置了默认 Client ID（官方 Docker 镜像的默认配置），请使用此选项。无需访问 Azure Portal。

### 🛠️ 前提条件 {#prerequisites}

- 一个个人 Microsoft 账户（Outlook.com、live.com 或 M365 Family）
- 一个带有浏览器的设备（智能手机、笔记本电脑或平板电脑）以完成授权

### 📝 步骤 {#steps}

1. 登录您的 Mail Archiver 应用程序。
2. 导航至 **Mail Accounts** > **Create**。
3. 填写以下字段：
   - **Name**：账户的描述性名称（例如："Personal Outlook"）
   - **Email Address**：您的个人 Microsoft 电子邮件地址（例如：`you@outlook.com`）
   - **Provider**：选择 **Microsoft Personal**
4. 不会显示 Client ID 或 Client Secret 字段 — Mail Archiver 会自动使用内置的默认注册信息。
5. 点击 **Create**。
6. 设备代码授权页面会自动打开。页面会显示一个短代码（例如：`ABCD-EFGH`）以及指向 `https://microsoft.com/devicelogin` 的链接（或显示的验证 URL）。
7. 在**任何带有浏览器**的设备上，打开该 URL 并使用您的个人 Microsoft 账户登录。
8. 输入步骤 6 中的代码。
9. 出现同意屏幕，显示 **"Mail-Archiver"** 请求 IMAP 访问权限。请检查并批准。
10. Mail Archiver 页面会自动检测成功并重定向到账户编辑视图。
11. 完成 — 同步将根据账户设置自动开始。

> ⚠️ **未验证发布者警告**：同意屏幕可能会显示 "Mail-Archiver" 是一个未验证发布者的警告。这是正常现象 — 发布者验证需要商业身份（Microsoft Partner Network 账户），但对于流程的正常运行并非必需。应用无需验证即可正常工作。

---

## 🔧 选项 B：自定义应用注册（高级用户） {#option-b-custom-app-registration-power-users}

如果您不想使用共享的默认 Client ID（例如出于合规性原因，或希望完全控制应用注册），请使用此选项。

### 🛠️ 前提条件 {#prerequisites}

- 一个可以访问 Azure Portal 的 Microsoft 账户（个人或工作/学校账户）
- 对 Azure/Entra portal 有基本了解

### 🚀 创建应用注册 {#create-app-registration}

1. 导航至 [Microsoft Entra Admin Center](https://entra.microsoft.com) 或 [Azure Portal](https://portal.azure.com)。
2. 使用您的 Microsoft 账户登录。
3. 在左侧导航栏中，选择 **App registrations**（位于 Identity > Applications 下）。
4. 点击 **+ New registration**。
5. 填写以下详细信息：
   - **Name**：输入描述性名称（例如："Mail Archiver MSA"）
   - **Supported account types**：选择 **Accounts in any organizational directory and personal Microsoft accounts**
     - 这是多租户选项，同时支持个人账户和组织账户。
   - **Redirect URI**：保持为空（Device Code Flow 不需要此项）。
6. 点击 **Register**。
7. 从 **Overview** 页面记录下 **Application (client) ID**。

### 🔐 启用公共客户端流 (Public Client Flows) {#enable-public-client-flows}

1. 在您的应用注册中，导航至左侧菜单的 **Authentication**。
2. 在底部找到 **Allow public client flows** 并将其设置为 **Yes**。
3. 点击 **Save**。

> ℹ️ 无需手动配置 Client Secret、Redirect URI 或 API 权限 — Device Code Flow 使用通过 `IMAP.AccessAsUser.All` 作用域在运行时请求的委托权限。

### 📧 配置 Mail Archiver {#configure-mail-archiver}

覆盖全局默认设置（影响所有 MSA 账户）

在您的环境变量中设置 Client ID：


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 令牌刷新与撤销 {#token-refresh-revocation}

### 自动令牌刷新 {#automatic-token-refresh}

在初始授权后，Mail Archiver 会收到：
- 一个 **access token**（短期，约 1 小时）
- 一个 **refresh token**（长期，用于自动获取新的 access tokens）

Mail Archiver 在每次同步运行前会自动刷新 access token。除非 refresh token 过期或被撤销，否则您无需重新授权。

### 撤销访问权限 {#revoking-access}

要撤销 Mail Archiver 对您个人 Microsoft 账户的访问权限：

1. 访问 [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage)
2. 使用您的个人 Microsoft 账户登录。
3. 在列表中找到 **"Mail-Archiver"**（或您的自定义应用名称）。
4. 点击 **Remove** 以撤销访问。

撤销后，在您重新授权之前，Mail Archiver 将无法访问该账户。

---

## ⚠️ 重要注意事项 {#important-notes}
- **共享 Client ID**：使用默认 Client ID 时，所有实例上的所有 Mail Archiver 用户都共享同一个 Azure App Registration。Mail Archiver 的维护者负责保持该注册处于激活状态。如果 Microsoft 禁用了共享注册，所有用户都需要切换到自定义注册（选项 B），直到新版本发布并包含更新的 Client ID。

- **组织账户**：Microsoft Personal 提供程序使用 `/common` 权威端点 (authority endpoint)，并支持个人和组织（工作/学校）账户。对于需要客户端凭据访问（仅限应用，无需用户登录）的组织账户，请改用 **M365** 提供程序。

---

## 🔍 故障排除 {#troubleshooting}

### 文件夹在 outlook.com 上可见但在 Mail Archiver 中缺失 {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**症状**：某些文件夹存在于邮箱中（在 outlook.com 或 Outlook 中可见），但从未出现在 Mail Archiver 中 — 既不在账户设置页面的文件夹列表中，也不在同步过程中出现。且没有记录错误日志。

**原因 (服务端)**：由 **第三方客户端通过 EWS 创建** 的文件夹（这会将 outlook.com 账户设置为 Exchange 账户而非 IMAP）可能会导致错误的 `PR_CONTAINER_CLASS` 属性（例如，是 `IPF.Imap` 而不是 `IPF.Note`）。随后 Outlook.com 会 **完全将这些文件夹从 IMAP `LIST` 响应中过滤掉**，即使它们在 Web UI 中完全可见。这是 Microsoft 侧的行为，而非 Mail Archiver 的 Bug — 同样的问题也记录在 Microsoft Graph API 中，即具有非 `IPF.Note` 容器类的文件夹在属性修正前不会出现在 `mailFolders` 列表中：

Mail Archiver 从三个来源查询文件夹列表（递归 `LIST`、对每个文件夹子级的遍历以及 `LSUB`）并合并结果。当服务器未通过其中任何一个途径报告文件夹时，任何 IMAP 客户端都无法看到它。

**解决方法**：通过 outlook.com Web UI “触碰”受影响的文件夹，使 Microsoft 修正容器类：

1. 打开 <https://outlook.com> 并找到缺失的文件夹。
2. 重命名它（任何临时名称）然后再重命名回来 — 或者创建一个新文件夹并将邮件移动过去。
3. 对每个受影响的文件夹重复此操作。
4. 在 Mail Archiver 的 Account Details 页面触发账户的 **Full Resync**。必须进行全量同步，因为增量同步仅获取晚于账户上次同步日期的邮件 — 如果不进行全量同步，那些从未被发现的文件夹中的邮件将保持未归档状态。


*本指南更新于 2026 年。Microsoft 会定期更新其服务和 UI，因此某些步骤可能会有所不同。请参考 [Microsoft identity platform documentation](https://learn.microsoft.com/en-us/entra/identity-platform/) 获取最新详情。*
