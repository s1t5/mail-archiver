# 👥 用户管理与邮箱权限 {#user-management-and-mailbox-permissions}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

本指南提供了在 Mail Archiver 应用程序中创建新用户账号以及分配邮箱权限的详细说明。它还涵盖了更改您自己的密码以及在全新安装的实例上强制更改密码的操作。


## 🛠️ 前提条件 {#prerequisites}

- 拥有 Mail Archiver 应用程序的管理权限
- 系统中已配置现有的电子邮件账号

## 🧑‍💻 创建新用户账号 {#creating-a-new-user-account}

1. 使用具有管理权限的账号登录 Mail Archiver 应用程序
2. 从主菜单导航至 "Users" 部分
3. 点击 "Create New User" 按钮
4. 填写所需的用户信息：
   - **Username**: 为新用户输入唯一的用户名
   - **Password**: 为用户输入一个安全的密码
   - **Email**: 用户的电子邮件地址
   - **Admin**: 如果用户应具有管理权限，请勾选此框
   - **Self Manager**: 如果用户应能够管理自己的邮件账号（编辑现有账号、添加新账号以及删除账号），请勾选此框
   - **active**: 应勾选此项以允许用户登录
5. 点击 "Create User" 保存新用户账号

## 🔐 为用户分配邮箱权限 {#assigning-mailbox-permissions-to-users}

1. 创建用户后，或在编辑现有用户时，从主菜单导航至 "Users" 部分
2. 在列表中找到您想要分配邮件账号的用户，并点击该用户的 "Assign" 按钮
3. 在 "Assign Mail Accounts" 页面中，您将看到所有可用的邮件账号以及指示它们是否已分配给该用户的复选框
5. 要分配一个新的电子邮件账号，请勾选特定账号对应的复选框
6. 要从用户中移除电子邮件账号，请取消勾选复选框
7. 点击 "Save Assignments" 应用更改

## 👤 用户账号权限 {#user-account-permissions}

### Admin Users (管理员用户) {#admin-users}
管理员用户拥有应用程序的完全访问权限，可以：
- 管理所有用户账号及其权限
- 访问系统中的所有电子邮件账号
- 查看所有访问日志
- 执行所有管理任务

### Self Manager Users (自我管理用户) {#self-manager-users}
自我管理用户可以：
- 向自己的账号添加新的电子邮件账号
- 编辑分配给他们的现有电子邮件账号
- 删除分配给他们的电子邮件账号
- 访问并管理其分配账号中的电子邮件（包括删除）
- 查看自己的访问日志

### Standard Users (标准用户) {#standard-users}
标准用户权限有限，可以：
- 查看分配的电子邮件账号中的存档邮件
- 在分配的电子邮件账号内进行搜索
- 从分配的账号中导出电子邮件
- 从分配的账号中恢复电子邮件
- 访问分配账号中的邮件附件

## 🔑 更改您自己的密码 {#changing-your-own-password}

每个拥有本地密码的用户都可以在无需管理员的情况下更改密码：

1. 点击右上角的 **username** 以打开用户菜单。
2. 选择 **Change Password**。
3. 输入您的 **current password**（当前密码）和 **new password**（新密码）两次，然后提交。

新密码必须符合以下要求，并且必须**与当前密码不同**。

> ℹ️ **OIDC 用户无法在此处更改密码。** 页面会提示：
> *"O</strong>OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* 请前往您的身份提供商处更改密码 —— 参见 [OIDC / SSO](OIDC_Implementation.md)。

### 密码要求 {#password-requirements}

- 至少 **10 个字符**
- 至少一个 **大写字母**
- 至少一个 **小写字母**
- 至少一个 **数字**
- 至少一个 **特殊字符**

在您输入时，表单会以实时清单的形式显示这些规则。当管理员创建用户或重置密码时，适用相同的规则。

## ⚠️ 强制更改密码（初始设置） {#forced-password-change-initial-setup}

全新安装的实例会以配置中的管理员凭据（`Authentication__Username` / `Authentication__Password`）启动。只要该默认密码仍在被使用 **且尚不存在邮件账号**，首次登录将强制要求更改：

- 警告横幅会解释原因：*"For security reasons, you must change your
  password before continuing. This is required because you are using the default
  system credentials on a newly set up system."*
- 您无法离开该页面：*"You must change your password before
  you can continue using the system."*
- 新密码必须与当前密码不同。
- 保存后，该标记将被清除，您将被带到仪表板。

一旦您更改了默认密码，强制更改将不再出现。

## 🕒 保持登录状态 — "Remember Me" 复选框 {#staying-signed-in-the-remember-me-checkbox}

登录表单有一个标有 **Remember Me** 的复选框（在德语 UI 中为 *angemeldet bleiben*）。它只做一件事：决定身份验证 Cookie 是会话 Cookie 还是持久性 Cookie。

| | 未勾选复选框 | 已勾选复选框 |
|---|---|---|
| Cookie 有效期 | `Authentication__SessionTimeoutMinutes` (默认 **60 分钟**) | **7 天** |
| Cookie 类型 | 会话 Cookie — 浏览器关闭时丢弃 | 持久性 Cookie — 存储在磁盘上 |
| 重启浏览器 | 您必须重新登录 | 您保持登录状态 |
| 空闲超时 | 每次请求都会延长 (`SlidingExpiration`) | 每次请求都会延长，直到达到 7 天的绝对限制 |
| 退出登录 | 立即结束会话 | 立即结束会话 |

因此，该复选框将“在此浏览器会话中登录，最多空闲 60 分钟”转变为“登录长达 7 天，包括浏览器和机器重启”。

### 复选框不具备的功能 {#things-the-checkbox-does-not-do}

- **它不会记住您的密码或用户名。** 下次访问时登录表单不会预填；仅保留会话 Cookie。
- **7 天是一个硬性限制，无法配置。** 该值在代码中是固定的 (`Auth/Services/CookieAuthenticationService.cs`)。只有非持久性有效期遵循 `Authentication__SessionTimeoutMinutes`。
- **它不是空闲计时器。** 7 天是从登录时开始计算的；一旦时间到期，即使您一分钟前刚使用过存档，也需要重新登录。
- **它在注销后无法生存。** 无论是否勾选了复选框，点击 *Logout* 都会删除 Cookie。

### 与 2FA 的交互 {#interaction-with-2fa}

启用双因子身份验证（2FA）时，复选框会在 2FA 步骤期间被记住，并仅在第二个因子验证成功后才生效。2FA 的交接存在于服务器端会话中，该会话拥有自己的 **60 分钟空闲超时**：如果未在该窗口期内输入代码，登录将被中止，复选框的选择也会随之丢失。

### 与应用程序会话的交互 {#interaction-with-the-application-session}

Mail Archiver 为短时状态保留了第二个独立的服务器端会话 —— 包括 2FA 交接、批量恢复选择和最后的搜索状态。该会话具有固定的 **60 分钟空闲超时**，且 *不是* 持久性的。在带有有效 "Remember Me" Cookie 的情况下重启浏览器后，您处于登录状态，但该会话中持有的任何待处理操作都将重新开始。

### 与 OIDC / SSO 的交互 {#interaction-with-oidc-sso}

通过外部身份提供商登录的用户永远不会获得持久性 Cookie：OIDC 回调始终以 `persistAuthentication: false` 开始会话。对于这些账号，该复选框 **不起作用** —— 会话持续多久由身份提供商的会话设置决定。

> ⚠️ **安全提示。** 在共享或无人看管的机器上，请保持复选框**未勾选**状态 —— 持久性 Cookie 会使存档在长达 7 天的时间内无需密码即可访问。在无法接受这种情况的环境中，请缩短 `Authentication__SessionTimeoutMinutes` 并要求用户不要使用该复选框，或者使用由提供商强制执行其自身会话策略的 OIDC。

## 🔒 安全考虑因素 {#security-considerations}

1. 为所有用户账号使用强密码
2. 安装后立即更改默认管理员密码
3. 仅将 Admin 权限分配给需要的用户
4. 仅将 Self Manager 权限分配给需要管理自己账号的用户
5. 定期审查用户权限以确保访问权限是适当的
6. 在不再需要时移除用户访问权限
7. 使用最小权限原则 —— 仅分配用户所需的电子邮件账号访问权限
