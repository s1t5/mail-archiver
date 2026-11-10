# 📧 Mail Archiver Documentation {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Self-hosted email archiving — archive, search and export mail from any provider.**
Настройте один раз, сохраняйте каждое сообщение и находите его снова спустя годы.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Getting started__

    ---

    Установите Mail Archiver с помощью Docker Compose и подключите свой первый почтовый ящик.

    [:octicons-arrow-right-24: Setup and parameters](Setup.md)

-   :material-magnify:{ .lg .middle } __Find anything__

    ---

    Полнотекстовый поиск по всем аккаунтам с фильтрами, сортировкой и массовыми действиями.

    [:octicons-arrow-right-24: Mail search](Search.md)

-   :material-api:{ .lg .middle } __Automate it__

    ---

    Читайте свой архив с помощью скриптов и AI-агентов — API-ключи только для чтения с ограниченной областью доступа.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Secure it__

    ---

    Двухфакторная аутентификация, SSO, логирование доступа и политики хранения данных.

    [:octicons-arrow-right-24: Two-factor auth](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Installation & Maintenance {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Setup and parameters__

    ---

    Каждая переменная окружения, Kestrel HTTPS и управление секретами.

    [:octicons-arrow-right-24: Read](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Разместите Mail Archiver за nginx или Caddy с терминацией TLS.

    [:octicons-arrow-right-24: Read](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL upgrade__

    ---

    Переход на новую мажорную версию PostgreSQL без потери данных.

    [:octicons-arrow-right-24: Read](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Database maintenance__

    ---

    VACUUM, ANALYZE и поддержание быстродействия архива с течением времени.

    [:octicons-arrow-right-24: Read](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Backup and restore__

    ---

    Резервное копирование базы данных и её восстановление — включая настройки Proxmox.

    [:octicons-arrow-right-24: Read](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Local test environment__

    ---

    Запустите временный экземпляр локально, прежде чем вносить изменения в продакшн.

    [:octicons-arrow-right-24: Read](LocalTestEnvironment.md)

</div>

## 📥 Getting Mail In {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Account import__

    ---

    Добавьте множество аккаунтов одновременно из CSV-файла.

    [:octicons-arrow-right-24: Read](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Synchronization__

    ---

    Быстрая vs полная синхронизация, контрольные точки и что происходит при сбое синхронизации.

    [:octicons-arrow-right-24: Read](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI commands__

    ---

    Импорт существующих архивов из командной строки и перемещение сохраненных оригинальных
    сообщений между базой данных и файловой системой.

    [:octicons-arrow-right-24: Read](CLI.md)

-   :material-upload:{ .lg .middle } __Web import (EML / MBox)__

    ---

    Загрузите файл MBox или ZIP с файлами EML через браузер.

    [:octicons-arrow-right-24: Read](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Rate limit handling__

    ---

    Отслеживание пропускной способности и то, как приостановленные синхронизации возобновляются автоматически.

    [:octicons-arrow-right-24: Read](RateLimitHandling.md)

</div>

## 🔍 Working with the Archive {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Mail search__

    ---

    Синтаксис запросов, фильтры, сортировка, пагинация и массовые действия.

    [:octicons-arrow-right-24: Read](Search.md)

-   :material-progress-clock:{ .lg .middle } __Background jobs__

    ---

    Отслеживайте синхронизации, импорты, восстановления и экспорты — и безопасно отменяйте их.

    [:octicons-arrow-right-24: Read](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Mailbox migration__

    ---

    Копируйте почту между ящиками с сохранением структуры папок.

    [:octicons-arrow-right-24: Read](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Account export (EML / MBox)__

    ---

    Выгрузите весь аккаунт обратно в виде переносимого архива EML или MBox.

    [:octicons-arrow-right-24: Read](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Original message storage__

    ---

    Сохраняйте сообщение в том виде, в котором оно было получено, чтобы DKIM по-прежнему проходил проверку, а экспорты были байт-в-байт точными.

    [:octicons-arrow-right-24: Read](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Attachment deduplication__

    ---

    Как идентичные вложения хранятся один раз вместо множества.

    [:octicons-arrow-right-24: Read](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Date-windowed offload__

    ---

    Перемещайте старую почту в другой аккаунт по временным интервалам.

    [:octicons-arrow-right-24: Read](Offload.md)

-   :material-harddisk:{ .lg .middle } __Per-account storage__

    ---

    Узнайте, сколько места занимает каждый почтовый ящик.

    [:octicons-arrow-right-24: Read](AccountStorage.md)

</div>

## 🔐 Administration & Security {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __User management__

    ---

    Создавайте пользователей, предоставляйте права администратора, назначайте разрешения для почтовых ящиков и меняйте свой
    собственный пароль.

    [:octicons-arrow-right-24: Read](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Two-factor authentication__

    ---

    Настройка TOTP, резервные коды и что делать при потере устройства.

    [:octicons-arrow-right-24: Read](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Вход через Entra ID, Authelia или другой провайдер идентификации.

    [:octicons-arrow-right-24: Read](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Access logging__

    ---

    Кто открывал, искал, экспортировал или удалял и что именно.

    [:octicons-arrow-right-24: Read](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit data export__

    ---

    Экспорт журнала аудита для проверок на соответствие требованиям.

    [:octicons-arrow-right-24: Read](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Emergency account recovery__

    ---

    Восстановление доступа, если аккаунт заблокирован.

    [:octicons-arrow-right-24: Read](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Retention policies__

    ---

    Автоматическое удаление с почтового сервера или из локального архива.

    [:octicons-arrow-right-24: Read](RetentionPolicies.md)

</div>

## ☁️ Provider Guides {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure app registration (M365)__

    ---

    Зарегистрируйте приложение и настройте политику хранения для Microsoft 365.

    [:octicons-arrow-right-24: Read](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant mailbox import__

    ---

    Массовый импорт всех почтовых ящиков тенанта через одну форму.

    [:octicons-arrow-right-24: Read](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow instances__

    ---

    Выбирайте почтовые ящики централизованно и создавайте выделенные пароли приложений IMAP.

    [:octicons-arrow-right-24: Read](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Personal Microsoft accounts__

    ---

    Outlook.com, Hotmail и M365 Family через device code flow.

    [:octicons-arrow-right-24: Read](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail best practices__

    ---

    Пароли приложений, метки, expunge и ограничения скорости Gmail.

    [:octicons-arrow-right-24: Read](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail limitations__

    ---

    Что не смогут сделать IMAP и эндпоинты экспорта Yahoo.

    [:octicons-arrow-right-24: Read](YahooMailExportLimitations.md)

</div>

## 🔌 Programmatic Access {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Доступ к архиву только для чтения с ограниченными API-ключами для каждого пользователя.

    [:octicons-arrow-right-24: Read](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Предоставьте архив AI-агентам в качестве обнаруживаемых инструментов.

    [:octicons-arrow-right-24: Read](MCP.md)

</div>

## 🧰 Operations & Development {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Language and localization__

    ---

    Смена языка интерфейса и способы добавления перевода.

    [:octicons-arrow-right-24: Read](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose logs__

    ---

    Чтение логов контейнеров и изменение уровня логирования.

    [:octicons-arrow-right-24: Read](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Development versions__

    ---

    Переключение на dev-тег для тестирования предстоящих изменений.

    [:octicons-arrow-right-24: Read](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Эта документация постоянно дополняется.**
Если вы заметили ошибку или у вас есть предложение, пожалуйста,
[откройте issue](https://github.com/s1t5/mail-archiver/issues) или отправьте pull request.
</div>
