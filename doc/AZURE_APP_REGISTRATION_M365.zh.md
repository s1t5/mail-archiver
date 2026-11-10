# ☁️ Mail Archiver Azure 应用注册与保留策略指南 {#azure-app-registration-and-retention-policy-guide-for-mail-archiver}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

本指南提供了为邮件检索设置 Azure 应用注册以及在 Mail Archiver 应用程序中配置保留策略的详细说明。

## 📚 目录 {#table-of-contents}

1. [概述](#overview)
2. [用于邮件检索的 Azure 应用注册](#azure-app-registration-for-email-retrieval)
   - [前提条件](#prerequisites)
   - [创建应用注册](#create-app-registration)
   - [设置所需的 API 权限](#set-required-api-permissions)
   - [生成客户端密码 (Client Secret)](#generate-client-secret)
3. [配置 Mail Archiver M365 账户](#configure-mail-archiver-m365-account)
4. [保留策略设置](#retention-policy-setup)
   - [配置保留策略](#configure-retention-policy)

## 🌐 概述 {#overview}

Mail Archiver 应用程序支持使用 OAuth2 客户端凭据流 (client credentials flow) 的 Microsoft 365 (M365) 账户进行邮件检索。此外，它还提供保留策略功能，可以在指定时间后自动从邮件服务器删除邮件，在保持完整存档的同时帮助管理存储空间。

## ☁️ 用于邮件检索的 Azure 应用注册 {#azure-app-registration-for-email-retrieval}

### 🛠️ 前提条件 {#prerequisites}

- 具有 Microsoft Entra ID (Azure AD) 的管理员访问权限
- 拥有 Exchange Online 许可的 Microsoft 365 租户

### 🚀 创建应用注册 {#create-app-registration}

1. 导航至 [Microsoft Entra 管理中心](https://entra.microsoft.com)
2. 使用您的管理员账户登录
3. 在左侧导航栏中，选择 **App registrations**
4. 点击 App registrations 页面顶部的 **+ New registration**
5. 填写以下详细信息：
   - **Name**: 输入一个描述性的名称（例如，“Mail Archiver M365 Provider”）
   - **Supported account types**: 选择 **Accounts in this organizational directory only** (单租户)
     - 这是 Mail Archiver 使用的客户端凭据流所必需的
   - **Redirect URI**: 保持为空（客户端凭据流不需要此项）
6. 点击 **Register**

**重要提示**：请记录下 **Overview** 页面中的以下值：
- **Application (client) ID** - 您需要在 Mail Archiver 中将其作为 `ClientId` 使用
- **Directory (tenant) ID** - 您需要在 Mail Archiver 中将其作为 `TenantId` 使用

### 🔐 设置所需的 API 权限 {#set-required-api-permissions}

1. 在左侧菜单中导航至 **API permissions**
2. 点击 **+ Add a permission**
3. 选择 **Microsoft Graph**
4. 选择 **Application permissions**（Mail Archiver 的客户端凭据流所必需）
5. 添加以下权限：
   - **Mail.Read** - 读取所有邮箱中的邮件
   - **Mail.ReadWrite** - 读取并写入所有邮箱中的邮件（用于还原功能及删除功能）
   - **User.Read.All** - 读取所有用户的完整个人资料（用于 Microsoft 365 租户邮箱导入；同时也为 Exchange 许可筛选提供 `assignedPlans`）

6. 点击 **Add permissions**
7. **至关重要**：点击 **Grant admin consent for [Your Organization]**
8. 点击 **Yes** 进行确认

**注意**：需要使用应用权限 (Application permissions)，因为 Mail Archiver 使用客户端凭据流在无需用户交互的情况下访问邮箱。

### 🔑 生成客户端密码 (Client Secret) {#generate-client-secret}

1. 在左侧菜单中导航至 **Certificates & secrets**
2. 在 **Client secrets** 下，点击 **+ New client secret**
3. 提供一个描述（例如，“Mail Archiver Secret”）
4. 选择一个有效期
5. 点击 **Add**
6. **重要提示**：请立即复制 **Value** 并安全地存储。此密码将不会再次显示。

## 📧 配置 Mail Archiver M365 账户 {#configure-mail-archiver-m365-account}

完成应用注册后，您需要在 Mail Archiver 中使用以下值配置 M365 邮件账户：

### 📋 应用注册所需的数值： {#required-values-from-app-registration}

1. **Client ID**: 从应用注册的 **Overview** 页面复制 **Application (client) ID**
2. **Client Secret**: 复制您在创建客户端密码时保存的 **Value**
3. **Tenant ID**: 从应用注册的 **Overview** 页面复制 **Directory (tenant) ID**

### 🚀 在 Mail Archiver 中创建 M365 账户： {#creating-m365-account-in-mail-archiver}

1. 登录您的 Mail Archiver 应用程序
2. 导航至 **Mail Accounts** > **Create**
3. 填写以下字段：
   - **Name**: 账户的描述性名称（例如，“Sales Team M365”）
   - **Email Address**: 要存档的目标邮箱地址
   - **Provider**: 选择 **M365**
   - **Client ID**: 输入您应用注册中的 Application (client) ID
   - **Client Secret**: 输入您保存的客户端密码值
   - **Tenant ID**: 输入您应用注册中的 Directory (tenant) ID

4. 点击 **Create**

### ⚠️ 重要注意事项： {#important-notes}

- 在创建单个 M365 账户时，**Email Address** 字段必须包含您想要存档的实际邮箱地址。
- 应用注册必须拥有访问指定邮箱的权限。
- 若要一次性为租户中的所有或选定邮箱创建账户，请参考 [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) 指南。


## 🗑️ 保留策略设置 {#retention-policy-setup}

Mail Archiver 应用程序提供保留策略功能，可以在邮件成功存档后自动从 IMAP 服务器删除邮件，从而在保持完整存档的同时帮助管理 IMAP 服务器上的存储空间。

### 🛠️ 配置保留策略 {#configure-retention-policy}

1. 在 Mail Archiver 应用程序中导航至 **Mail Accounts**
2. 创建一个新的 M365 账户或编辑现有账户
3. 在账户配置表单中，找到 **Delete After Days** 字段
4. 输入存档邮件应从服务器删除的天数：
   - 例如，输入 `30` 表示在 30 天后删除邮件
   - 保持为空以禁用自动删除
5. 保存账户配置


## ℹ️ 关于迁移邮箱的信息 {#ℹ-information-regarding-migrated-mailboxes}

### 📁 迁移后文件夹不可见 {#folders-not-visible-after-migration}

如果您已将邮箱从其他邮件提供商迁移到 Microsoft 365，某些文件夹可能不会出现在文件夹列表中。

#### 🔍 背景原因 {#background}

Mail Archiver 通过 Microsoft Graph API 从 M365 邮箱中检索文件夹信息。应用程序会正确请求所有可用文件夹（包括隐藏文件夹）并遍历完整的文件夹层级。

然而，Microsoft Graph API 仅返回 `PR_CONTAINER_CLASS` 设置为 `IPF.Note` 的文件夹。在从其他邮件系统迁移邮箱期间，文件夹可能会被分配不同的容器类，例如 `IPF.Imap`。这些文件夹不会通过 Graph API 的文件夹列表端点公开。

此行为会影响所有使用此 API 进行文件夹检索的应用程序。

#### 🔧 解决方案 {#workaround-options}

**方案 1：使用 MFCMAPI 工具（推荐）**

1. 从 Microsoft 官方仓库下载 [MFCMAPI](https://github.com/Microsoft/mfcmapi/)
2. 启动 MFCMAPI 并连接到您的邮箱
3. 导航至每个缺失的文件夹
4. 将 `PR_CONTAINER_CLASS` 属性从 `IPF.Imap` 更改为 `IPF.Note`
5. 重启 Outlook 并在 Mail Archiver 中重新加载文件夹

**方案 2：在 Outlook 中重新创建文件夹**

1. 直接在 Outlook 中创建新文件夹（这些文件夹将具有正确的 `IPF.Note` 类）
2. 将所有邮件从迁移的文件夹移动到新文件夹中
3. 如果需要，可以删除旧的迁移文件夹

**方案 3：PowerShell 脚本（用于批量更改）**

对于拥有大量迁移文件夹的邮箱，您可以使用带有 Exchange Online 模块的 PowerShell 来一次性更改多个文件夹的容器类。请联系您的 Exchange 管理员寻求此类方法的协助。


---

**注意**：本指南截至 2025 年有效。Microsoft 会定期更新其服务，因此某些 UI 元素可能会有所不同。请始终参考最新的 Microsoft 文档以获取最新信息。
