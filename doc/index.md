# 📧 Mail Archiver Documentation

<div class="docs-lead" markdown>
**Self-hosted email archiving — archive, search and export mail from any provider.**
Set it up once, keep every message, and find it again years later.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Getting started__

    ---

    Install Mail Archiver with Docker Compose and connect your first mailbox.

    [:octicons-arrow-right-24: Setup and parameters](Setup.md)

-   :material-magnify:{ .lg .middle } __Find anything__

    ---

    Full-text search across every account, with filters, sorting and bulk actions.

    [:octicons-arrow-right-24: Mail search](Search.md)

-   :material-api:{ .lg .middle } __Automate it__

    ---

    Read your archive from scripts and AI agents — read-only, scoped API keys.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Secure it__

    ---

    Two-factor authentication, SSO, access logging and retention policies.

    [:octicons-arrow-right-24: Two-factor auth](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Installation & Maintenance

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Setup and parameters__

    ---

    Every environment variable, Kestrel HTTPS and secrets management.

    [:octicons-arrow-right-24: Read](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Put Mail Archiver behind nginx or Caddy with TLS termination.

    [:octicons-arrow-right-24: Read](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL upgrade__

    ---

    Move to a new major PostgreSQL version without losing data.

    [:octicons-arrow-right-24: Read](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Database maintenance__

    ---

    VACUUM, ANALYZE and keeping the archive responsive over time.

    [:octicons-arrow-right-24: Read](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Backup and restore__

    ---

    Back up the database and restore it — including Proxmox setups.

    [:octicons-arrow-right-24: Read](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Local test environment__

    ---

    Run a throwaway instance locally before touching production.

    [:octicons-arrow-right-24: Read](LocalTestEnvironment.md)

</div>

## 📥 Getting Mail In

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Account import__

    ---

    Add many accounts at once from a CSV file.

    [:octicons-arrow-right-24: Read](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Synchronization__

    ---

    Quick vs. full sync, checkpoints, and what happens when a sync fails.

    [:octicons-arrow-right-24: Read](Synchronization.md)

-   :material-file-import:{ .lg .middle } __CLI local import__

    ---

    Import existing archives from the command line.

    [:octicons-arrow-right-24: Read](CLI-Local-Import.md)

-   :material-upload:{ .lg .middle } __Web import (EML / MBox)__

    ---

    Upload an MBox file or a ZIP of EML files through the browser.

    [:octicons-arrow-right-24: Read](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Rate limit handling__

    ---

    Bandwidth tracking, and how paused syncs resume on their own.

    [:octicons-arrow-right-24: Read](RateLimitHandling.md)

</div>

## 🔍 Working with the Archive

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Mail search__

    ---

    Query syntax, filters, sorting, paging and bulk actions.

    [:octicons-arrow-right-24: Read](Search.md)

-   :material-progress-clock:{ .lg .middle } __Background jobs__

    ---

    Watch syncs, imports, restores and exports — and cancel them safely.

    [:octicons-arrow-right-24: Read](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Mailbox migration__

    ---

    Copy mail between mailboxes while preserving the folder structure.

    [:octicons-arrow-right-24: Read](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Account export (EML / MBox)__

    ---

    Write a whole account back out as a portable EML or MBox archive.

    [:octicons-arrow-right-24: Read](Export-Account.md)

-   :material-content-duplicate:{ .lg .middle } __Attachment deduplication__

    ---

    How identical attachments are stored once instead of many times.

    [:octicons-arrow-right-24: Read](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Date-windowed offload__

    ---

    Move older mail to another account, window by window.

    [:octicons-arrow-right-24: Read](Offload.md)

-   :material-harddisk:{ .lg .middle } __Per-account storage__

    ---

    See how much space each mailbox takes up.

    [:octicons-arrow-right-24: Read](AccountStorage.md)

</div>

## 🔐 Administration & Security

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __User management__

    ---

    Create users, grant admin rights, assign mailbox permissions and change your
    own password.

    [:octicons-arrow-right-24: Read](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Two-factor authentication__

    ---

    TOTP setup, backup codes, and what to do when a device is lost.

    [:octicons-arrow-right-24: Read](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Sign in through Entra ID, Authelia or another identity provider.

    [:octicons-arrow-right-24: Read](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Access logging__

    ---

    Who opened, searched, exported or deleted what.

    [:octicons-arrow-right-24: Read](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit data export__

    ---

    Export the audit trail for compliance reviews.

    [:octicons-arrow-right-24: Read](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Emergency account recovery__

    ---

    Regain access when an account is locked out.

    [:octicons-arrow-right-24: Read](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Retention policies__

    ---

    Automatic deletion from the mail server or the local archive.

    [:octicons-arrow-right-24: Read](RetentionPolicies.md)

</div>

## ☁️ Provider Guides

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure app registration (M365)__

    ---

    Register an app and configure retention for Microsoft 365.

    [:octicons-arrow-right-24: Read](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant mailbox import__

    ---

    Bulk-import every mailbox of a tenant from a single form.

    [:octicons-arrow-right-24: Read](M365TenantImport.md)

-   :material-account-key:{ .lg .middle } __Personal Microsoft accounts__

    ---

    Outlook.com, Hotmail and M365 Family via device code flow.

    [:octicons-arrow-right-24: Read](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail best practices__

    ---

    App passwords, labels, expunge and Gmail's rate limits.

    [:octicons-arrow-right-24: Read](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail limitations__

    ---

    What Yahoo's IMAP and export endpoints will not do.

    [:octicons-arrow-right-24: Read](YahooMailExportLimitations.md)

</div>

## 🔌 Programmatic Access

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Read-only access to the archive with scoped per-user API keys.

    [:octicons-arrow-right-24: Read](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Expose the archive to AI agents as discoverable tools.

    [:octicons-arrow-right-24: Read](MCP.md)

</div>

## 🧰 Operations & Development

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Language and localization__

    ---

    Switch the interface language, and how to add a translation.

    [:octicons-arrow-right-24: Read](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose logs__

    ---

    Read container logs and change the log level.

    [:octicons-arrow-right-24: Read](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Development versions__

    ---

    Switch to a dev tag to test upcoming changes.

    [:octicons-arrow-right-24: Read](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **This documentation is continuously being expanded.**
If you spot a mistake or have a suggestion, please
[open an issue](https://github.com/s1t5/mail-archiver/issues) or submit a pull request.
</div>
