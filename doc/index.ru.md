# 📧 Документация Mail Archiver {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Self-hosted архивация электронной почты — архивируйте, ищите и экспортируйте почту от любого провайдера.**
Настройте один раз, сохраняйте каждое сообщение и находите его спустя годы.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Начало работы__

    ---

    Установите Mail Archiver с помощью Docker Compose и подключите свой первый почтовый ящик.

    [:octicons-arrow-right-24: Настройка и параметры](Setup.md)

-   :material-magnify:{ .lg .middle } __Найти что угодно__

    ---

    Полнотекстовый поиск по всем аккаунтам с фильтрами, сортировкой и массовыми действиями.

    [:octicons-arrow-right-24: Поиск почты](Search.md)

-   :material-api:{ .lg .middle } __Автоматизация__

    ---

    Читайте свой архив с помощью скриптов и AI-агентов — API-ключи только для чтения с ограниченной областью доступа.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Безопасность__

    ---

    Двухфакторная аутентификация, SSO, логирование доступа и политики хранения данных.

    [:octicons-arrow-right-24: Двухфакторная аутентификация](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Установка и обслуживание {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Настройка и параметры__

    ---

    Каждая переменная окружения, Kestrel HTTPS и управление секретами.

    [:octicons-arrow-right-24: Читать](Setup.md)

-   :material-server-network:{ .lg .middle } __Обратный прокси__

    ---

    Разместите Mail Archiver за nginx или Caddy с терминацией TLS.

    [:octicons-arrow-right-24: Читать](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __Обновление PostgreSQL__

    ---

    Переход на новую мажорную версию PostgreSQL без потери данных.

    [:octicons-arrow-right-24: Читать](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Обслуживание базы данных__

    ---

    VACUUM, ANALYZE и поддержание отзывчивости архива с течением времени.

    [:octicons-arrow-right-24: Читать](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Резервное копирование и восстановление__

    ---

    Создание резервной копии базы данных и её восстановление — включая конфигурации Proxmox.

    [:octicons-arrow-right-24: Читать](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Локальная тестовая среда__

    ---

    Запустите временный экземпляр локально, прежде чем вносить изменения в продакшн.

    [:octicons-arrow-right-24: Читать](LocalTestEnvironment.md)

</div>

## 📥 Импорт почты {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Импорт аккаунтов__

    ---

    Добавьте множество аккаунтов одновременно из CSV-файла.

    [:octicons-arrow-right-24: Читать](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Синхронизация__

    ---

    Быстрая против полной синхронизации, контрольные точки и что происходит при сбое синхронизации.

    [:octicons-arrow-right-24: Читать](Synchronization.md)

-   :material-console:{ .lg .middle } __Команды CLI__

    ---

    Импорт существующих архивов из командной строки и перемещение сохраненных оригинальных сообщений между базой данных и файловой системой.

    [:octicons-arrow-right-24: Читать](CLI.md)

-   :material-upload:{ .lg .middle } __Веб-импорт (EML / MBox)__

    ---

    Загрузите файл MBox или ZIP с файлами EML через браузер.

    [:octicons-arrow-right-24: Читать](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Обработка ограничений скорости (Rate limit)__

    ---

    Отслеживание пропускной способности и то, как приостановленные синхронизации возобновляются автоматически.

    [:octicons-arrow-right-24: Читать](RateLimitHandling.md)

</div>

## 🔍 Работа с архивом {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Поиск почты__

    ---

    Синтаксис запросов, фильтры, сортировка, пагинация и массовые действия.

    [:octicons-arrow-right-24: Читать](Search.md)

-   :material-progress-clock:{ .lg .middle } __Фоновые задачи__

    ---

    Отслеживание синхронизаций, импортов, восстановлений и экспортов — и их безопасная отмена.

    [:octicons-arrow-right-24: Читать](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Миграция почтовых ящиков__

    ---

    Копирование почты между ящиками с сохранением структуры папок.

    [:octicons-arrow-right-24: Читать](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Экспорт аккаунта (EML / MBox)__

    ---

    Экспорт всего аккаунта обратно в переносимый архив EML или MBox.

    [:octicons-arrow-right-24: Читать](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Хранение оригинальных сообщений__

    ---

    Сохранение сообщения в полученном виде, чтобы DKIM по-прежнему проходил проверку, а экспорты были байт-в-байт идентичными.

    [:octicons-arrow-right-24: Читать](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Дедупликация вложений__

    ---

    Как идентичные вложения хранятся один раз вместо множества.

    [:octicons-arrow-right-24: Читать](AttachmentDeduplication.md)

-   :material-file-pdf-box:{ .lg .middle } __Предпросмотр счетов XRechnung__

    ---

    Чтение XML-вложений XRechnung как PDF непосредственно в архиве.

    [:octicons-arrow-right-24: Читать](EInvoicing.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Выгрузка по временным окнам__

    ---

    Перемещение старой почты в другой аккаунт, окно за окном.

    [:octicons-arrow-right-24: Читать](Offload.md)

-   :material-harddisk:{ .lg .middle } __Хранилище для каждого аккаунта__

    ---

    Посмотрите, сколько места занимает каждый почтовый ящик.

    [:octicons-arrow-right-24: Читать](AccountStorage.md)

</div>

## 🔐 Администрирование и безопасность {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Управление пользователями__

    ---

    Создание пользователей, предоставление прав администратора, назначение разрешений на почтовые ящики и смена собственного пароля.

    [:octicons-arrow-right-24: Читать](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Двухфакторная аутентификация__

    ---

    Настройка TOTP, резервные коды и что делать при потере устройства.

    [:octicons-arrow-right-24: Читать](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Вход через Entra ID, Authelia или другой провайдер идентификации.

    [:octicons-arrow-right-24: Читать](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Логирование доступа__

    ---

    Кто открыл, искал, экспортировал или удалил.

    [:octicons-arrow-right-24: Читать](Logs.md)

-   :material-file-export:{ .lg .middle } __Экспорт данных аудита__

    ---

    Экспорт журнала аудита для проверок на соответствие требованиям.

    [:octicons-arrow-right-24: Читать](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Экстренное восстановление аккаунта__

    ---

    Восстановление доступа, если аккаунт заблокирован.

    [:octicons-arrow-right-24: Читать](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Политики хранения__

    ---

    Автоматическое удаление с почтового сервера или из локального архива.

    [:octicons-arrow-right-24: Читать](RetentionPolicies.md)

</div>

## ☁️ Руководства по провайдерам {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Регистрация приложения Azure (M365)__

    ---

    Регистрация приложения и настройка политик хранения для Microsoft 365.

    [:octicons-arrow-right-24: Читать](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __Импорт почтовых ящиков тенанта M365__

    ---

    Массовый импорт всех почтовых ящиков тенанта из одной формы.

    [:octicons-arrow-right-24: Читать](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Экземпляры Mailcow__

    ---

    Централизованный выбор почтовых ящиков и создание выделенных паролей приложений IMAP.

    [:octicons-arrow-right-24: Читать](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Личные аккаунты Microsoft__

    ---

    Outlook.com, Hotmail и M365 Family через device code flow.

    [:octicons-arrow-right-24: Читать](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Лучшие практики Gmail__

    ---

    Пароли приложений, ярлыки, expunge и ограничения скорости Gmail.

    [:octicons-arrow-right-24: Читать](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Ограничения Yahoo mail__

    ---

    Что не смогут делать IMAP и эндпоинты экспорта Yahoo.

    [:octicons-arrow-right-24: Читать](YahooMailExportLimitations.md)

</div>

## 🔌 Программный доступ {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Доступ к архиву только для чтения с ограниченными API-ключами для каждого пользователя.

    [:octicons-arrow-right-24: Читать](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Предоставление архива AI-агентам в качестве обнаруживаемых инструментов.

    [:octicons-arrow-right-24: Читать](MCP.md)

</div>

## 🧰 Эксплуатация и разработка {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Язык и локализация__

    ---

    Смена языка интерфейса и способы добавления перевода.

    [:octicons-arrow-right-24: Читать](Localization.md)

-   :material-docker:{ .lg .middle } __Логи Docker Compose__

    ---

    Чтение логов контейнера и изменение уровня логирования.

    [:octicons-arrow-right-24: Читать](DockerComposeLogs.md)

-   :material-heart-pulse:{ .lg .middle } __Здоровье и метрики__

    ---

    Отслеживание liveness, readiness и метрик архива из Prometheus или другой системы мониторинга.

    [:octicons-arrow-right-24: Читать](Monitoring.md)

-   :material-tag:{ .lg .middle } __Разрабатываемые версии__

    ---

    Переключение на dev-тег для тестирования предстоящих изменений.

    [:octicons-arrow-right-24: Читать](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Эта документация постоянно дополняется.**
Если вы заметили ошибку или у вас есть предложение, пожалуйста,
[откройте issue](https://github.com/s1t5/mail-archiver/issues) или отправьте pull request.
</div>
