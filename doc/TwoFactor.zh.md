# 🔐 双重身份验证 {#two-factor-authentication}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

双重身份验证 (2FA) 在登录过程中增加了第二个步骤：在输入密码后，Mail Archiver 会要求输入由您手机上的身份验证器应用生成的 **6 位数字代码**。这样一来，仅凭被盗的密码将不足以访问存档。

Mail Archiver 使用 **TOTP** (基于时间的一次性密码, RFC 6238) 来实现 2FA —— 这与 Google Authenticator、Microsoft Authenticator、Authy、1Password、Bitwarden 等使用的标准相同。不涉及任何外部服务；密钥永远不会离开您的实例。

**2FA 是按用户可选开启的。** 它由每位用户为自己的账户进行配置，而不是由管理员进行全局配置。

## 🛠️ 前提条件 {#prerequisites}

- 您的手机或桌面端安装了支持 TOTP 的身份验证器应用
- 拥有您自己账户的访问权限（您必须已登录）
- **本地（密码）账户** —— 请参阅下文关于 OIDC 的说明

> **⚠️ OIDC 用户不可使用。** 如果您通过外部身份提供商（Entra ID, Authelia, Keycloak, …）登录，2FA 将由该提供商管理，且设置页面会被拦截并显示消息 *“OIDC users cannot enable 2FA. Authentication is managed by your OIDC provider.”*（OIDC 用户无法启用 2FA。身份验证由您的 OIDC 提供商管理）。请改为在您的身份提供商中启用多因素身份验证 —— 请参阅 [OIDC / SSO](OIDC_Implementation.md)。

## 🚀 启用双重身份验证 {#enabling-two-factor-authentication}

### 第 1 步 — 打开设置页面 {#step-1-open-the-setup-page}

1. 登录 Mail Archiver
2. 打开您的账户菜单并选择 **Two-Factor Authentication**
3. 您将进入 **Two-Factor Setup** 页面

### 第 2 步 — 扫描 QR 码 {#step-2-scan-the-qr-code}

页面会显示一个 QR 码。打开您的身份验证器应用，选择 *Add account* → *Scan QR code*，然后将摄像头对准它。

**无法扫描代码？** 请使用 **Manual Entry**（手动输入）：页面会以文本形式显示密钥（`If you can't scan the QR code, enter this secret manually in your authenticator app`）。将其作为基于时间/TOTP 的账户类型输入到您的应用中。

> **ℹ️ 密钥尚未保存。** 它仅在您的浏览器会话设置期间持有。只有在第 3 步成功后，2FA 才会生效 —— 如果您放弃设置，您的账户不会发生任何变化。

### 第 3 步 — 使用代码确认 {#step-3-confirm-with-a-code}

输入您的应用当前显示的 6 位数字代码，然后点击 **Enable Two-Factor Authentication**。

如果代码被拒绝，设置页面会重新显示一个新的 QR 码，并提示消息 *"Invalid authentication code. Please try again."*（无效的身份验证代码。请重试。）。请检查您手机的时钟是否设置为自动同步 —— TOTP 依赖于准确的时间。

### 第 4 步 — 保存您的备份代码 {#step-4-save-your-backup-codes}

成功后，您将被引导至 **Backup Codes** 页面。页面会显示 **10 个代码**，每个代码长度为 16 个字符。

> **⚠️ 这是显示代码的唯一时间。** 它们以哈希形式存储 —— 您或管理员稍后都无法再次查看它们。请使用 **Download Backup Codes** 将其保存到文件中，或在离开页面前将其复制到您的密码管理器中。

下载的文件包含以下提醒：

> *Mail Archiver - Two-Factor Authentication Backup Codes. Store these backup codes in a safe place. You can use them to log in if you lose access to your authenticator app. Each backup code can only be used once. After using a code, it will be invalidated.*
> *(Mail Archiver - 双重身份验证备份代码。请将这些备份代码保存在安全的地方。如果您无法访问身份验证器应用，可以使用它们进行登录。每个备份代码只能使用一次。使用后，该代码将失效。)*
> *Important: Keep these codes secure and do not share them with anyone.*
> *(重要提示：请妥善保管这些代码，不要与任何人分享。)*

## 🔑 使用 2FA 登录 {#logging-in-with-2fa}

输入密码后，您将被引导至 **Two-Factor Verification** 页面并被要求输入代码：

> *Enter a 6-digit TOTP code from your authenticator app or a 16-character backup code*
> *(请输入身份验证器应用的 6 位 TOTP 代码或 16 位备份代码)*

两者在同一个输入框中均可使用 —— Mail Archiver 通过长度来区分它们：

| 输入内容 | 长度 | 行为 |
|---|---|---|
| 来自身份验证器应用的验证码 | 6 位数字 | 针对当前时间窗口进行验证 |
| 备份代码 | 16 个字符 | 针对存储的代码进行验证，验证后即 **失效** |

为了容忍微小的时钟差异，系统也会接受紧接前一个和后一个时间步的代码。

**"Remember me"**（记住我）选项会从登录表单延续：如果您勾选了该选项，在完成 2FA 步骤后，即使重启浏览器，您也将保持登录状态。在 2FA 步骤待处理期间，该选择会保存在服务器端会话中，因此只有在您在会话的 **60 分钟** 空闲超时内输入代码时，该状态才会生效。详细信息和限制（7 天绝对有效期，对 OIDC 用户无效）请参见 [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox)。

### 使用备份代码 {#using-a-backup-code}

1. 在同一个输入框中输入您保存的 16 位代码之一
2. 代码会被接受并 **立即失效** —— 无法重复使用
3. 使用备份代码进行的成功登录会记录在访问日志中，并带有备注 `(2FA Backup Code)`，以便您稍后发现异常使用情况

### 速率限制 {#rate-limiting}

为了减缓暴力破解尝试，2FA 验证被限制为 **每 15 分钟 5 次尝试**，按 IP 地址和用户名进行计数。超过限制后您会看到：

> **Too Many Attempts** — *You have made too many 2FA verification attempts. Please wait and try again later.*
> （尝试次数过多 —— 您进行了过多的 2FA 验证尝试。请稍后再试。）

等待时间窗口结束后重试。该限制适用于窗口期内的失败 *和* 成功尝试。

## 🚫 禁用双重身份验证 {#disabling-two-factor-authentication}

1. 在您的账户菜单中打开 **Two-Factor Authentication**
2. 页面会要求确认：*"Are you sure you want to disable two-factor authentication? This will reduce the security of your account."*（您确定要禁用双重身份验证吗？这将降低您账户的安全性。）
3. 输入您的 **password** —— 此步骤需要您当前的密码，而不是 2FA 代码
4. 点击 **Disable Two-Factor Authentication**

禁用操作会移除密钥并使所有剩余的备份代码失效。下次登录时仅需密码即可。

## 🔄 丢失身份验证器？两种找回方式 {#lost-your-authenticator-two-ways-back-in}

### 如果您仍有备份代码 {#if-you-still-have-backup-codes}

按照上述方法使用备份代码登录，然后使用您的新设备禁用并重新启用 2FA。请记得保存新的备份代码。

### 如果您没有备份代码了 {#if-you-have-no-backup-codes-left}

必须由 **administrator**（管理员）为您的账户重置 2FA：

1. 管理员打开 **Users**
2. 编辑受影响的用户
3. 选择 **Reset Two-Factor Authentication** 并确认：*"Are you sure you want to reset two-factor authentication for this user? They will need to set it up again."*（您确定要重置此用户的双重身份验证吗？他们需要重新进行设置。）
4. 用户随后可以仅凭密码登录并重新设置 2FA

重置会清除密钥和备份代码。用户列表中会出现确认消息 *"Two-factor authentication has been successfully reset for user …"*。

> **ℹ️ 重置范围。** 重置仅针对 2FA。它 **不是** 密码重置 —— 如果问题在于密码或账户被完全锁定，请参阅 [Emergency Account Recovery](EmergencyAccountRecovery.md)。

## 👁️ 管理员可以看到什么 {#what-administrators-can-see}

**Users** 概览显示 2FA 列：

- *"Two-factor authentication is currently enabled for this user."* —— 2FA 已激活
- 否则该用户的 2FA 为关闭状态

管理员可以重置 2FA（如上所述），但 **无法** 读取密钥或备份代码 —— 两者都以哈希形式存储。丢失设备且没有备份代码的用户除了通过管理员重置外，没有其他办法进入账户。

## 🔍 审计追踪 {#audit-trail}

2FA 活动会被写入日志，对于成功的登录，也会写入访问日志：

| 事件 | 位置 | 条目 |
|---|---|---|
| 通过身份验证器应用登录 | Access log | 带有备注 `IP: <address> (2FA)` 的 `Login` |
| 通过备份代码登录 | Access log | 带有备注 `IP: <address> (2FA Backup Code)` 的 `Login` |
| 2FA 尝试失败 | Application log | 带有用户名、IP 和掩码 token 的 Warning |
| 无效的备份代码 | Application log | 带有用户名和 IP 的 Warning |

访问日志条目使得备份代码的使用在事后可见 —— 这对于检测代码是否被不该拥有它的人使用非常有用。有关如何阅读这些条目的信息，请参阅 [Access Logging](Logs.md)。

## ⚙️ 底层工作原理 {#how-it-works-under-the-hood}

面向管理员和审查人员：

- **标准**：TOTP (RFC 6238)，20 字节随机密钥，base32 编码
- **验证窗口**：±1 个时间步，以容忍时钟漂移
- **备份代码**：10 个代码，每个 16 个字符，由加密 RNG 生成，以 **哈希** 形式存储，使用时单独失效
- **密钥存储**：存储在用户记录中；设置后永远不会返回给浏览器
- **会话处理**：在存储 2FA 身份之前会重新生成会话，因此登录前植入的会话 Cookie 不会被带入已认证的会话中
- **OIDC 账户**：2FA 管理被拦截；由提供商拥有权威性
- **API keys**：设计上绕过 2FA，因为非交互式 token 无法回答挑战 —— 请参阅 [REST API guide](API.md) 中的安全原理

## ❓ 故障排除 {#troubleshooting}

| 现象 | 原因 | 修复方法 |
|---|---|---|
| *"Invalid authentication code"* | 手机时钟不准，或应用中选错了账户 | 启用自动时间同步；检查应用是否显示了 Mail Archiver 条目 |
| *"Two-factor secret is missing. Please restart the setup process."* | 设置页面打开时间过长，会话已过期 | 从账户菜单重新开始设置 |
| *"Your session has expired. Please log in again."* | 验证页面停留时间过长 | 重新登录 |
| *"Two-factor authentication is not enabled for this account."* | 2FA 被重置或从未完成设置 | 重新设置，或咨询管理员 |
| 备份代码被拒绝 | 代码已被使用，或字符识别错误 | 备份代码仅限使用 **一次**；使用另一个，或重置 2FA |
| *"Too Many Attempts"* | 15 分钟内超过 5 次验证尝试 | 等待时间窗口结束 |
| 账户菜单中没有设置页面 | 您通过 OIDC 登录 | 在您的身份提供商处启用 MFA |
| Users 列表中没有 2FA 列 | 您不是管理员 | 咨询管理员 |

## 📚 相关文档 {#related-documentation}

- [User Management and Mailbox Permissions](UserManagement.md) — 创建账户、管理权限
- [OIDC / SSO Authentication](OIDC_Implementation.md) — 通过外部提供商登录
- [Emergency Account Recovery](EmergencyAccountRecovery.md) — 账户被锁定
- [Access Logging](Logs.md) — 阅读审计追踪
- [REST API](API.md) — 为什么 API keys 绕过 2FA
