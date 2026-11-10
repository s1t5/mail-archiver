# 📧 Mail Archiver Documentation {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**自托管邮件归档 — 归档、搜索并导出来自任何提供商的邮件。**
只需设置一次，保留每一条消息，并在多年后再次找到它。
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __入门指南__

    ---

    使用 Docker Compose 安装 Mail Archiver 并连接您的第一个邮箱。

    [:octicons-arrow-right-24: 设置与参数](Setup.md)

-   :material-magnify:{ .lg .middle } __查找任何内容__

    ---

    跨所有账户进行全文搜索，支持过滤器、排序和批量操作。

    [:octicons-arrow-right-24: 邮件搜索](Search.md)

-   :material-api:{ .lg .middle } __自动化__

    ---

    通过脚本和 AI 代理读取您的归档内容 — 只读、范围受限的 API 密钥。

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __安全性__

    ---

    双重身份验证、SSO、访问日志和保留策略。

    [:octicons-arrow-right-24: 双重身份验证](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ 安装与维护 {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __设置与参数__

    ---

    所有环境变量、Kestrel HTTPS 和密钥管理。

    [:octicons-arrow-right-24: 阅读](Setup.md)

-   :material-server-network:{ .lg .middle } __反向代理__

    ---

    将 Mail Archiver 置于带有 TLS 终止功能的 nginx 或 Caddy 之后。

    [:octicons-arrow-right-24: 阅读](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL 升级__

    ---

    在不丢失数据的情况下迁移到新的 PostgreSQL 主要版本。

    [:octicons-arrow-right-24: 阅读](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __数据库维护__

    ---

    VACUUM、ANALYZE 以及随着时间的推移保持归档的响应能力。

    [:octicons-arrow-right-24: 阅读](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __备份与恢复__

    ---

    备份数据库并进行恢复 — 包括 Proxmox 设置。

    [:octicons-arrow-right-24: 阅读](BackupRestore.md)

-   :material-flask:{ .lg .middle } __本地测试环境__

    ---

    在接触生产环境之前，先在本地运行一个临时实例。

    [:octicons-arrow-right-24: 阅读](LocalTestEnvironment.md)

</div>

## 📥 导入邮件 {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __账户导入__

    ---

    通过 CSV 文件一次性添加多个账户。

    [:octicons-arrow-right-24: 阅读](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __同步__

    ---

    快速同步 vs. 全量同步、检查点，以及同步失败时会发生什么。

    [:octicons-arrow-right-24: 阅读](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI 命令__

    ---

    通过命令行导入现有归档，并在数据库与文件系统之间移动存储的原始消息。

    [:octicons-arrow-right-24: 阅读](CLI.md)

-   :material-upload:{ .lg .middle } __Web 导入 (EML / MBox)__

    ---

    通过浏览器上传 MBox 文件或 EML 文件的 ZIP 压缩包。

    [:octicons-arrow-right-24: 阅读](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __速率限制处理__

    ---

    带宽追踪，以及暂停的同步如何自动恢复。

    [:octicons-arrow-right-24: 阅读](RateLimitHandling.md)

</div>

## 🔍 使用归档 {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __邮件搜索__

    ---

    查询语法、过滤器、排序、分页和批量操作。

    [:octicons-arrow-right-24: 阅读](Search.md)

-   :material-progress-clock:{ .lg .middle } __后台任务__

    ---

    监控同步、导入、恢复和导出 — 并安全地取消它们。

    [:octicons-arrow-right-24: 阅读](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __邮箱迁移__

    ---

    在邮箱之间复制邮件，同时保留文件夹结构。

    [:octicons-arrow-right-24: 阅读](MailboxMigration.md)

-   :material-download:{ .lg .middle } __账户导出 (EML / MBox)__

    ---

    将整个账户写回为可移植的 EML 或 MBox 归档。

    [:octicons-arrow-right-24: 阅读](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __原始消息存储__

    ---

    保持消息接收时的原样，以便 DKIM 仍可验证且导出结果在字节级完全一致。

    [:octicons-arrow-right-24: 阅读](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __附件去重__

    ---

    相同的附件如何仅存储一次而不是多次。

    [:octicons-arrow-right-24: 阅读](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __按日期窗口卸载__

    ---

    按时间窗口将较旧的邮件移动到另一个账户。

    [:octicons-arrow-right-24: 阅读](Offload.md)

-   :material-harddisk:{ .lg .middle } __按账户存储统计__

    ---

    查看每个邮箱占用了多少空间。

    [:octicons-arrow-right-24: 阅读](AccountStorage.md)

</div>

## 🔐 管理与安全 {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __用户管理__

    ---

    创建用户、授予管理员权限、分配邮箱权限以及更改您自己的密码。

    [:octicons-arrow-right-24: 阅读](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __双重身份验证__

    ---

    TOTP 设置、备份代码，以及设备丢失时的应对措施。

    [:octicons-arrow-right-24: 阅读](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    通过 Entra ID、Authelia 或其他身份提供商登录。

    [:octicons-arrow-right-24: 阅读](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __访问日志__

    ---

    谁打开、搜索、导出或删除了什么。

    [:octicons-arrow-right-24: 阅读](Logs.md)

-   :material-file-export:{ .lg .middle } __审计数据导出__

    ---

    导出审计追踪以进行合规性审查。

    [:octicons-arrow-right-24: 阅读](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __紧急账户恢复__

    ---

    在账户被锁定时重新获得访问权限。

    [:octicons-arrow-right-24: 阅读](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __保留策略__

    ---

    从邮件服务器或本地归档中自动删除。

    [:octicons-arrow-right-24: 阅读](RetentionPolicies.md)

</div>

## ☁️ 提供商指南 {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure 应用注册 (M365)__

    ---

    注册应用并为 Microsoft 365 配置保留策略。

    [:octicons-arrow-right-24: 阅读](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 租户邮箱导入__

    ---

    通过单个表单批量导入租户的所有邮箱。

    [:octicons-arrow-right-24: 阅读](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow 实例__

    ---

    集中选择邮箱并配置专用的 IMAP 应用密码。

    [:octicons-arrow-right-24: 阅读](Mailcow.md)

-   :material-account-key:{ .lg .middle } __个人 Microsoft 账户__

    ---

    通过设备代码流支持 Outlook.com、Hotmail 和 M365 家庭版。

    [:octicons-arrow-right-24: 阅读](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail 最佳实践__

    ---

    应用密码、标签、清除 (expunge) 以及 Gmail 的速率限制。

    [:octicons-arrow-right-24: 阅读](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo 邮件限制__

    ---

    Yahoo 的 IMAP 和导出端点无法实现的功能。

    [:octicons-arrow-right-24: 阅读](YahooMailExportLimitations.md)

</div>

## 🔌 程序化访问 {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    通过范围受限的每用户 API 密钥实现对归档的只读访问。

    [:octicons-arrow-right-24: 阅读](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    将归档作为可发现的工具暴露给 AI 代理。

    [:octicons-arrow-right-24: 阅读](MCP.md)

</div>

## 🧰 运维与开发 {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __语言与本地化__

    ---

    切换界面语言，以及如何添加翻译。

    [:octicons-arrow-right-24: 阅读](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose 日志__

    ---

    读取容器日志并更改日志级别。

    [:octicons-arrow-right-24: 阅读](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __开发版本__

    ---

    切换到开发标签以测试即将推出的更改。

    [:octicons-arrow-right-24: 阅读](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **本文档正在持续扩展中。**
如果您发现错误或有任何建议，请
[提交 issue](https://github.com/s1t5/mail-archiver/issues) 或提交 pull request。
</div>
