# Microsoft 365 Tenant Mailbox Import {#microsoft-365-tenant-mailbox-import}

[← 返回文档索引](index.md)

## 概述 {#overview}

Mail Archiver 可以通过单个创建表单，为同一个租户中的多个邮箱创建 Microsoft 365 邮件账户。这在入驻客户或组织时非常有用，因为您可以选择归档该组织中所有的或选定的部分 Microsoft 365 邮箱。

租户导入使用表单中配置的 Microsoft Graph 应用程序凭据，并为每个导入的邮箱创建一个 Mail Archiver 邮件账户。

## 前提条件 {#prerequisites}

在使用租户导入之前，请完成 [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md) 中描述的 Microsoft 365 应用注册设置。

您需要从 Microsoft Entra ID 获取以下值：

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

应用注册必须具有 Microsoft Graph **application permissions**（应用程序权限），允许 Mail Archiver 读取租户邮箱。除了常规 M365 账户归档所需的权限外，租户导入还需要以下权限：

- **User.Read.All** – 用于列出租户用户及其邮箱地址。

在创建表单中，选择 **Microsoft 365 (tenant)** 作为提供商即可进行租户导入。

### 过滤 {#filtering}

租户导入和 租户管理 (Tenant Management) 会自动排除：
- **Guest accounts** (`userType == "Guest"`) — 在租户中没有自己邮箱的外部受邀用户。
- **没有 Exchange Online 许可证的用户** — 只有在 `assignedPlans` 中拥有活跃 Exchange 服务计划的用户才会被列出。这避免了导入无法归档的账户。

## 导入模式 {#import-modes}

租户导入支持两种模式：

### 导入所有列出的邮箱 {#import-all-listed-mailboxes}

当 Microsoft Graph 返回的每个邮箱都应被创建为 Mail Archiver 账户时，请使用此模式。

- Mail Archiver 从 Microsoft Graph 加载租户邮箱列表。
- 已存在的 M365 邮件账户将被跳过。
- 如果启用了 **Skip disabled mailboxes**，则禁用的用户不会包含在邮箱列表中。

### 仅导入选定的邮箱 {#import-selected-mailboxes-only}

当只需归档租户中的特定邮箱时，请使用此模式。

- 先加载租户邮箱列表。
- 禁用 **Import all listed mailboxes**。
- 选择需要导入的邮箱。
- 提交表单，仅为选定的邮箱创建账户。

## 使用步骤 {#step-by-step-usage}

1. 以可以创建邮件账户的用户身份登录 Mail Archiver。
2. 打开 **Mail Accounts** > **Create**。
3. 选择 **Microsoft 365 (tenant)** 作为提供商。
4. 输入 Microsoft 365 凭据：
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. 输入用于生成账户的 **Account name** 前缀。
6. 选择是否跳过已禁用的邮箱。
7. 点击 **Load mailboxes** 以列出租户邮箱。
8. 选择所需的导入行为：
   - 保持 **Import all listed mailboxes** 启用状态以导入所有列出的邮箱。
   - 禁用它并选择单个邮箱，以仅导入特定邮箱。
9. 点击 **Save**。

创建完成后，Mail Archiver 会重定向回邮件账户列表，并显示导入了多少个租户邮件账户以及跳过了多少个现有账户。

## 账户命名 {#account-naming}

对于租户导入，Mail Archiver 使用在 **Account name** 中输入的值作为每个创建的邮箱账户的前缀。

示例：

- 输入的账户名称：`Test GmbH`
- 租户邮箱：`mailbox@example.com`
- 创建的账户名称：`Test GmbH - <mailbox@example.com>`

这使得识别属于同一租户或客户的导入账户变得更加容易。

## 现有账户 {#existing-accounts}

如果某个邮箱在 Mail Archiver 中已作为 Microsoft 365 账户存在，则租户导入会跳过它，而不是创建重复项。现有账户检测是基于邮箱电子邮件地址和 M365 提供商进行的。

在提交导入之前，邮箱列表也可以显示该邮箱是否已存在。

## 已禁用的邮箱 {#disabled-mailboxes}

**Skip disabled mailboxes** 复选框控制是否应将已禁用的租户用户从邮箱列表中排除。

- 启用：仅列出并导入启用的租户用户。
- 禁用：已禁用的租户用户可能会出现在列表中，并且如果被选中或在导入所有列出的邮箱时，也可以被导入。

## 租户管理 (Tenant Management) {#tenant-management}

除了创建表单中的租户导入外，Mail Archiver 还为管理员提供了一个专门的 **Tenant Management** 页面。可以通过任何 Microsoft 365 账户旁边的 **Tenant Management** 按钮（建筑图标）从 Mail Accounts 索引访问。

租户管理对于添加最初未导入的邮箱非常有用——例如，在初始入驻后租户中创建了新邮箱，或者最初跳过了某些账户。

### 它与创建表单中的租户导入有何不同 {#how-it-differs-from-the-create-form-tenant-import}

- **无需再次输入** Microsoft 365 凭据 (Client ID, Client Secret, Tenant ID)。它们直接从点击该按钮的源账户中获取。
- 页面打开时会自动加载租户邮箱列表（没有 "Load mailboxes" 按钮）。
- 已导入的邮箱会显示 "already exists" 徽章，且无法再次选择。
- 只有管理员可以访问 Tenant Management。

### 使用步骤 {#step-by-step-usage}

1. 以管理员身份登录 Mail Archiver。
2. 打开 **Mail Accounts**。
3. 找到您想要管理的 Microsoft 365 账户，并点击其旁边的 **Tenant Management** 按钮。
4. 页面加载并列出 Microsoft Graph 为源账户租户返回的所有邮箱。
   - 在 Mail Archiver 中已作为 M365 账户存在的邮箱会标记为 "already exists" 徽章，且无法选择。
   - 已禁用的租户用户不会被列出；Tenant Management 始终跳过已禁用的邮箱。
5. 选择您想要添加的邮箱。
6. 输入用于新账户的 **Account name** 前缀（命名方案为 `{prefix} - <{email}>`，与创建表单中的租户导入相同）。
7. （可选）为新账户配置 **Delete After Days** 和 **Local Retention Days**。
8. 点击 **Add selected mailboxes**。

创建完成后，Mail Archiver 会重定向到邮件账户列表，并显示添加了多少个邮箱。

### 搜索邮箱 {#searching-mailboxes}

对于拥有数千个邮箱的大型租户，邮箱列表上方的搜索字段允许您按显示名称或电子邮件地址进行过滤。该过滤器不区分大小写，并在您输入时立即生效。**Select all** 和 **Clear selection** 仅对当前可见（已过滤）的邮箱进行操作，因此过滤是选择特定子集而不影响隐藏条目的安全方式。

### 重命名现有账户 {#renaming-existing-accounts}

**Rename existing accounts** 复选框会将命名方案 `{prefix} - <{email}>` 应用于所有与源账户共享相同应用注册（Client ID 和 Tenant ID）的 M365 账户。这对于在初始导入后规范化名称不一致的账户名称非常有用。

勾选后，保存表单将重命名每个匹配的账户——包括源账户——无论是否选择了新的邮箱进行导入。您可以单独使用此选项（不选择任何新邮箱）来规范化名称而不添加账户。

### 邮箱限制 {#mailbox-limit}

单次 Tenant Management 操作最多可一次性添加 `TenantManagement__MaxSelectedMailboxes` 个邮箱（默认 `1000`）。这可以防止意外的大规模导入和过度的 Microsoft Graph / 数据库负载。如果您需要导入更多邮箱，请多次运行该操作。有关如何调整此限制，请参阅 [Setup Guide](Setup.md#-tenant-management-settings)。

### 所需权限 {#permissions-required}

Tenant Management 使用与创建表单中的租户导入相同的 Microsoft Graph 应用程序权限。源账户的应用注册必须具有：

- 常规 M365 账户归档所需的权限，**以及**
- **User.Read.All** — 用于列出租户用户及其邮箱地址。

有关初始设置，请参阅 [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md)。

### 审计日志 {#audit-logging}

每次 Tenant Management 页面查看和每次添加操作都会记录在访问日志中，包括列出或添加的邮箱数量以及使用的源账户。
