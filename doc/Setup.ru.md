# 🛠️ Mail Archiver Setup Guide {#mail-archiver-setup-guide}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

Данное руководство содержит подробные инструкции по настройке приложения Mail Archiver с использованием Docker Compose.

## 🛠️ Prerequisites {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Шаги установки {#installation-steps}

1. Установите необходимые предварительные требования в вашей системе.

2. Создайте файл `docker-compose.yml` со следующим содержимым:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey;

      # Authentication Settings
      - Authentication__Username=admin
      - Authentication__Password=secure123!
      - Authentication__SessionTimeoutMinutes=60
      - Authentication__CookieName=MailArchiverAuth
      - Authentication__CookieSameSite=Lax

      # REST API Settings (read-only, disabled by default)
      - Api__Enabled=false
      - Api__AllowAttachmentDownloads=true
      - Api__AllowMessageDownloads=true
      - Api__EnableSwaggerUi=true
      - Api__DefaultPageSize=20
      - Api__MaxPageSize=100
      - Api__RateLimitPerMinute=120

      # MCP Server Settings (Model Context Protocol for AI agents, read-only, disabled by default)
      - Mcp__Enabled=false
      - Mcp__AllowAttachmentDownloads=true
      - Mcp__DefaultPageSize=20
      - Mcp__MaxResults=100
      - Mcp__MaxAttachmentBytes=10000000
      - Mcp__AllowMessageDownloads=true
      - Mcp__MaxMessageBytes=10000000
      - Mcp__RateLimitPerMinute=120

      # MailSync Settings
      - MailSync__IntervalMinutes=15
      - MailSync__TimeoutMinutes=0
      - MailSync__ConnectionTimeoutSeconds=180
      - MailSync__CommandTimeoutSeconds=300
      - MailSync__AlwaysForceFullSync=false
      - MailSync__IgnoreSelfSignedCert=false
      - MailSync__MaxConcurrentSyncs=1
      - MailSync__InterAccountDelaySeconds=0
      - MailSync__MaxIssuesPerKind=20
      - MailSync__FullSyncIntervalHours=24
      - MailSync__ExcludeSubfolders=false
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Contacts

      # Original Message Storage (complete .eml per email, disabled by default)
      - RawMessages__Enabled=false
      - RawMessages__Storage=postgres
      - RawMessages__MaxMessageSizeMb=50
      - RawMessages__IncludeGraph=true
      - RawMessages__FileSystem__BasePath=RawMessages
      - RawMessages__FileSystem__ShardDepth=2
      - RawMessages__FileSystem__VerifyChecksumOnRead=true
      - RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds=300

      # BatchRestore Settings
      - BatchRestore__AsyncThreshold=50
      - BatchRestore__MaxSyncEmails=150
      - BatchRestore__MaxAsyncEmails=50000
      - BatchRestore__SessionTimeoutMinutes=30
      - BatchRestore__DefaultBatchSize=50

      # Tenant Management Settings
      - TenantManagement__MaxSelectedMailboxes=1000

      # Mailcow Settings
      - Mailcow__MaxSelectedMailboxes=1000

      # BatchOperation Settings
      - BatchOperation__BatchSize=50
      - BatchOperation__PauseBetweenEmailsMs=50
      - BatchOperation__PauseBetweenBatchesMs=250

      # Bandwidth Tracking Settings (for IMAP rate limit handling)
      - BandwidthTracking__Enabled=false
      - BandwidthTracking__DailyLimitMb=25000
      - BandwidthTracking__WarningThresholdPercent=80
      - BandwidthTracking__PauseHoursOnLimit=24
      - BandwidthTracking__TrackUploadBytes=false

      # Selection Settings
      - Selection__MaxSelectableEmails=250

      # View Settings (Privacy & Display)
      - View__DefaultToPlainText=true
      - View__BlockExternalResources=false

      # Dashboard Settings (statistics cache, counter parts, chart periods)
      - Dashboard__CacheSeconds=60
      - Dashboard__RefreshIntervalMinutes=15
      - Dashboard__ShowDirectionSplits=true
      - Dashboard__SelectablePeriods=true

      # Jobs Settings (background jobs page)
      - Jobs__RefreshSeconds=30

      # Npgsql Settings
      - Npgsql__CommandTimeout=900

      # Upload Settings for MBox and EML files
      - Upload__MaxFileSizeGB=10
      - Upload__KeepAliveTimeoutHours=4
      - Upload__RequestHeadersTimeoutHours=2

      # Local Import Settings (for CLI imports from mounted volumes)
      - LocalImport__AllowedPaths__0=/data/import

      # CSV Import Settings (for bulk IMAP account import)
      - CsvImport__MaxRows=5000
      - CsvImport__MaxFileSizeBytes=10000000

      # Deletion Policy Settings (Optional - controls whether email deletion is allowed)
      - DeletionPolicy__DeletionAllowed=true

      # TimeZone Settings
      - TimeZone__DisplayTimeZoneId=Etc/UCT

      # Database Maintenance Settings (Optional)
      - DatabaseMaintenance__Enabled=false
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30

      # Attachment Deduplication Settings (Optional - feature is always on)
      - AttachmentDeduplication__BatchSize=200
      - AttachmentDeduplication__DelayBetweenBatchesMs=0
      - AttachmentDeduplication__StartupDelaySeconds=20
      - AttachmentDeduplication__OrphanCleanupIntervalHours=12
      - AttachmentDeduplication__CommandTimeoutSeconds=300

      # Account Storage Settings (Optional - per-account storage display)
      - AccountStorage__Enabled=true
      - AccountStorage__DailyExecutionTime=02:30
      - AccountStorage__BackfillDelayMs=5000
      - AccountStorage__RefreshBatchDelayMs=1000
      - AccountStorage__CommandTimeoutSeconds=300

      # ReleaseNotes Settings (Version Update Splash Screen)
      - ReleaseNotes__Enabled=true
      - ReleaseNotes__UpdateHintEnabled=true
      - ReleaseNotes__UpdateCheckIntervalHours=6
      - ReleaseNotes__RequestTimeoutSeconds=10

      # Logging Settings (Optional - defaults to Information level)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning

      # Security Settings
      - AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com

      # OIDC Configuration (see OIDC_Implementation.md for detailed setup)
      - OAuth__Enabled=true
      - OAuth__Authority=https://example.com
      - OAuth__ClientId=YOUR-CLIENT-ID
      - OAuth__ClientSecret=YOUR-CLIENT-SECRET
      - OAuth__DisplayName=PocketID SSO
      - OAuth__ClientScopes__0=openid
      - OAuth__ClientScopes__1=profile
      - OAuth__ClientScopes__2=email
      - OAuth__DisablePasswordLogin=false
      - OAuth__AutoRedirect=false
      - OAuth__AutoApproveUsers=false
      - OAuth__AdminEmails__0=admin@example.com
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      # Uncomment the following line to mount a directory for local file imports
      # (also configure LocalImport__AllowedPaths__0=/data/import in environment variables)
      # - /path/to/your/mbox/files:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
      # Only with RawMessages__Storage=filesystem: keep the original messages outside the
      # container, otherwise they are lost when the container is recreated.
      # - ./raw-messages:/app/RawMessages
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

networks:
  postgres:
```

3. Отредактируйте конфигурацию базы данных в `docker-compose.yml` и установите безопасный пароль в переменной `POSTGRES_PASSWORD` и `ConnectionString`.

4. Если вы хотите использовать аутентификацию (что настоятельно рекомендуется), определите `Authentication__Username` и `Authentication__Password`, которые используются для администратора.

5. Настройте переменную окружения `TimeZone__DisplayTimeZoneId` в соответствии с вашим предпочтительным часовым поясом (по умолчанию "Etc/UCT"). Вы можете использовать любой идентификатор часового пояса IANA (например, "Europe/Berlin", "Asia/Tokyo").

6. Опционально настройте переменные окружения `Logging__LogLevel`, чтобы управлять детализацией логов приложения. Доступные варианты см. в разделе "Logging Settings" ниже.

7. Настройте обратный прокси по вашему выбору с https и аутентификацией, чтобы обезопасить доступ к приложению. 

> ⚠️ **Внимание**: Само приложение не обеспечивает зашифрованный доступ через https! Это должно быть настроено через обратный прокси!

8. Первый запуск контейнеров:
```bash
docker compose up -d
```

9. Перезапуск контейнеров:
```bash
docker compose restart
```

10. Доступ к приложению

11. Войдите в систему, используя заданные учетные данные, и добавьте свой первый почтовый аккаунт:
   - Перейдите в раздел "Email Accounts"
   - Нажмите "New Account"
   - Введите данные вашего сервера и учетные данные
   - Сохраните и начните архивацию!
   - При желании создайте других пользователей и назначьте им аккаунты.

## 📚 Объяснение переменных окружения {#environment-variable-explanations}

### 🗄️ Подключение к базе данных {#database-connection}
- `ConnectionStrings__DefaultConnection`: Строка подключения к базе данных PostgreSQL. Измените значения `Host`, `Database`, `Username` и `Password` по мере необходимости.

### 🔐 Настройки аутентификации {#authentication-settings}
- `Authentication__Username`: Имя пользователя для учетной записи администратора.
- `Authentication__Password`: Пароль для учетной записи администратора.
- `Authentication__SessionTimeoutMinutes`: Время ожидания сессии в минутах. Это относится к неперсистентному файлу cookie для входа; пользователи, которые отмечают **Remember Me** в форме входа, получают персистентный файл cookie, действительный в течение фиксированных 7 дней. См. [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: Имя файла cookie аутентификации.
- `Authentication__CookieSameSite`: Настраивает атрибут SameSite для файлов cookie аутентификации, сессии и защиты от CSRF. Допустимые значения:
  - `Strict` (по умолчанию): Максимальная безопасность. Файлы cookie отправляются только при запросах внутри того же сайта. Это может вызвать проблемы при переходе в приложение из внешних ссылок (например, при нажатии на ссылку с другого веб-сайта), так как существующая сессия не будет распознана.
  - `Lax`: Рекомендуется при использовании обратного прокси-сервера. Файлы cookie отправляются при навигации верхнего уровня и запросах внутри того же сайта, что позволяет пользователям переходить по внешним ссылкам в приложение, сохраняя при этом защиту CSRF для POST-запросов.
  - `None`: Файлы cookie отправляются со всеми запросами. Требует HTTPS и атрибута `Secure`. Используйте это только в том случае, если у вас есть специфические требования к межсайтовым запросам и вы понимаете последствия для безопасности.

### 🔌 Настройки REST API {#rest-api-settings}

Необязательный REST API только для чтения **отключен по умолчанию**. Полное описание см. в [руководстве по REST API](API.md).

- `Api__Enabled`: Главный переключатель для REST API только для чтения (по умолчанию `false`). Когда `false`, все маршруты `/api/*` возвращают `404`.
- `Api__AllowAttachmentDownloads`: Разрешить скачивание байтов вложений через API (по умолчанию `true`). Когда `false`, эндпоинт вложений возвращает `403`.
- `Api__AllowMessageDownloads`: Разрешить скачивание полных сообщений через API, как пересобранного `/eml`, так и оригинального `/raw` (по умолчанию `true`). Когда `false`, оба эндпоинта возвращают `403`. Полное сообщение содержит свои вложения, поэтому **и** этот переключатель, и `Api__AllowAttachmentDownloads` должны быть `true`; отключение любого из них отключает оба эндпоинта.
- `Api__EnableSwaggerUi`: Открывать Swagger UI по адресу `/apidocs` и документ OpenAPI по адресу `/apidocs/spec/v1.json` (по умолчанию `true`). Оба требуют активной сессии браузера с авторизацией.
- `Api__DefaultPageSize`: Размер страницы по умолчанию для эндпоинтов списков, если он не указан (по умолчанию `20`).
- `Api__MaxPageSize`: Максимально допустимый размер страницы; более крупные запросы будут ограничены (по умолчанию `100`).
- `Api__RateLimitPerMinute`: Бюджет запросов с фиксированным окном на один API key в минуту (по умолчанию `120`).

### 🤖 Настройки MCP-сервера {#mcp-server-settings}

Необязательный MCP (Model Context Protocol) сервер предоставляет ИИ-агентам такой же доступ к почте только для чтения, как и REST API, через транспорт Streamable HTTP. Он **отключен по умолчанию**. См. [руководство по MCP](MCP.md) для получения полной информации. MCP использует те же самые API-ключи, что и REST API (токены `ma_...` bearer), и ту же область видимости доступа к учетным записям для каждого пользователя.
- `Mcp__Enabled`: Главный переключатель MCP-сервера (по умолчанию `false`). Когда `false`, эндпоинт `/mcp` возвращает `404`.
- `Mcp__AllowAttachmentDownloads`: Разрешить инструменту `get_attachment` возвращать байты вложений (по умолчанию `true`). Когда `false`, инструмент возвращает ошибку.
- `Mcp__DefaultPageSize`: Размер страницы по умолчанию, используемый `search_emails`, если вызывающая сторона опускает `pageSize` (по умолчанию `20`).
- `Mcp__MaxResults`: Верхний предел размера страницы для `search_emails`; большие значения ограничиваются (по умолчанию `100`).
- `Mcp__MaxAttachmentBytes`: Максимальный размер вложения (в байтах), которое инструмент `get_attachment` будет возвращать в виде base64; вложения большего размера отклоняются (по умолчанию `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Разрешить инструментам `get_email_eml` и `get_email_raw` возвращать полные сообщения (по умолчанию `true`). Когда `false`, инструменты возвращают ошибку. Полное сообщение содержит свои вложения, поэтому **и** этот переключатель, и `Mcp__AllowAttachmentDownloads` должны быть `true`; отключение любого из них отключает оба инструмента.
- `Mcp__MaxMessageBytes`: Максимальный размер сообщения (в байтах), который инструменты `get_email_eml` и `get_email_raw` будут возвращать в виде inline-данных; сообщения большего размера отклоняются (по умолчанию `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Бюджет запросов в фиксированном окне на один API-ключ в минуту (по умолчанию `120`).

### 📨 Настройки MailSync {#mailsync-settings}
- `MailSync__IntervalMinutes`: Интервал в минутах между синхронизациями электронной почты. Это глобальное значение по умолчанию; для каждого аккаунта его можно переопределить индивидуально на странице Create/Edit (оставьте пустым, чтобы использовать это значение по умолчанию).
- `MailSync__FullSyncIntervalHours`: Необязательное глобальное значение по умолчанию для автоматических полных ресинхронизаций, в часах. Если не установлено (по умолчанию), автоматическая полная синхронизация не запускается, если только для конкретного аккаунта не задано значение `FullSyncIntervalHours` на странице Create/Edit. Значения для отдельных аккаунтов переопределяют это глобальное значение.
- `MailSync__TimeoutMinutes`: Таймаут синхронизации для отдельного аккаунта в минутах. Синхронизация, которая выполняется дольше положенного, останавливается на границе следующего сообщения, сохраняет свои контрольные точки и возобновляется при следующем запуске; задача помечается как `Timed Out`, и `LastSync` не продвигается. Применяется только к запланированным синхронизациям; синхронизация, запущенная из интерфейса (ручная синхронизация или полная ресинхронизация), выполняется без таймаута. `0` (по умолчанию) или любое неотрицательное значение означает отсутствие таймаута. Рекомендуется проверить это значение перед обновлением, если вы синхронизируете большие почтовые ящики. См. [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Таймаут соединения для IMAP-соединений в секундах.
- `MailSync__CommandTimeoutSeconds`: Таймаут выполнения команд для IMAP-команд в секундах.
- `MailSync__AlwaysForceFullSync`: Нужно ли всегда принудительно выполнять полную синхронизацию (true/false).
- `MailSync__IgnoreSelfSignedCert`: Нужно ли игнорировать самоподписанные сертификаты (true/false).
- `MailSync__MaxConcurrentSyncs`: Количество синхронизаций аккаунтов, которые могут выполняться одновременно. По умолчанию `1` (последовательно, с сохранением обратной совместимости). Планировщик заполняет слоты по мере их освобождения, а не ждет завершения всей группы, поэтому один медленный почтовый ящик больше не задерживает остальные. Увеличьте это значение для одновременной синхронизации нескольких аккаунтов — учитывайте ограничения провайдеров по частоте запросов и использование локальных ресурсов. См. [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Необязательная задержка (stagger delay) в секундах, применяемая в конце каждой задачи синхронизации аккаунта. По умолчанию `0` (без задержки). Полезно для предотвращения всплесков активности при `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Количество проблем каждого типа, которые задача синхронизации запоминает для страницы аккаунта. Ошибки папок, отсутствие папок и ошибки сообщений учитываются отдельно, поэтому поток проблем одного типа не может вытеснить из вида другие; всё, что превышает лимит, просто подсчитывается, а не сохраняется в списке. По умолчанию `20`. `0` отключает детализацию и оставляет только счетчики. См. [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Папки, исключенные из синхронизации для **каждого** аккаунта, в дополнение к собственному списку исключенных папок каждого аккаунта. По умолчанию пусто, поэтому существующие настройки не затронуты. Два списка являются **аддитивными** — папка пропускается, если она соответствует любому из списков, — и оба используют одни и те же правила сопоставления: точное совпадение полного пути, точное совпадение имени папки, совпадение по суффиксу пути (так что `Drafts` также соответствует `INBOX/Drafts` и `INBOX.Drafts`), а также всё содержимое папки, если включен параметр `MailSync__ExcludeSubfolders`. Сопоставление нечувствительно к регистру. Полезно при импорте множества почтовых ящиков с одного и того же сервера, когда альтернативой является поддержка идентичного списка исключений для каждого аккаунта. По умолчанию ни одна папка не исключается; какие имена стоит вносить в список, зависит от сервера и его языка. Пример для дерева почтового ящика, которое также содержит папки календаря и контактов:
  ```yaml
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Calendars
      - MailSync__GlobalExcludedFolders__2=Contacts
      - MailSync__GlobalExcludedFolders__3=AddressBook
      - MailSync__GlobalExcludedFolders__4=Tasks
      - MailSync__GlobalExcludedFolders__5=TaskList
      - MailSync__GlobalExcludedFolders__6=Notes
      - MailSync__GlobalExcludedFolders__7=NoteList
      - MailSync__GlobalExcludedFolders__8=Journal
  ```
- `MailSync__ExcludeSubfolders`: Охватывает ли запись об исключении также папки ниже указанной ею папки. Применяется как к списку для отдельного аккаунта, так и к общесистемному списку, а также к обоим провайдерам. По умолчанию `false`, что соответствует поведению до появления этой опции: запись `Deleted Items` соответствует только этой папке, и дерево исключается из архива путем указания его корня и каждой папки ниже него. Установите `true`, чтобы запись охватывала и всё дерево под ней — тогда запись `Deleted Items` также будет исключать `Deleted Items/2024` и остальную часть этой ветки из архива. Правило привязывается к разделителю пути, поэтому оно охватывает целые папки и никогда не часть имени: `Archive` охватывает `Archive` и `Archive/Team`, но не трогает почтовую папку с названием `Team Archive Review`. `.` также считается разделителем, точно так же, как его обрабатывает правило суффикса пути.

### 📧 Настройки хранения оригинальных сообщений {#original-message-storage-settings}

По умолчанию архив хранит только обработанные части электронного письма, поэтому экспортированный `.eml` является
реконструкцией: кодировки передачи, MIME-границы и свертка заголовков отличаются от
полученного сообщения, а подписи DKIM больше не проходят проверку. С `RawMessages:Enabled`
хранится и полный оригинал.

| Параметр | Значение по умолчанию | Назначение |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Хранить оригинал каждого нового архивируемого письма. |
| `RawMessages__Storage` | `postgres` | Где хранятся байты: `postgres` или `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Большие сообщения архивируются без оригинала. `0` = без ограничений. |
| `RawMessages__IncludeGraph` | `true` | Также хранить оригиналы для учетных записей Microsoft 365 (Graph). |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Директория для файлов сообщений. Должна указывать на смонтированный том в Docker. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Уровни вложенности директорий, на которые распределяется id письма. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Проверять SHA-256 при каждой загрузке. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Удалять файлы сообщений, на которые не ссылается ни одно письмо. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Как часто запускается эта очистка. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Файлы моложе этого возраста никогда не удаляются. |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Задержка перед первой очисткой после запуска, чтобы не удалить файлы во время импорта. |

!!! warning "Файловому бэкенду требуется смонтированный том"
    При использовании `RawMessages__Storage=filesystem` файлы сообщений хранятся вне базы данных.
    Без bind mount они находятся в записываемом слое контейнера и будут потеряны при следующем
    пересоздании контейнера, в то время как строки в базе данных сохранятся. Резервное копирование
    директории необходимо делать вместе с дампом базы данных.

См. **[Original Message Storage](RawMessages.md)** для получения полного описания: где
предоставляется оригинал, доступные бэкенды хранения, как перемещать существующие сообщения между ними с помощью `--migrate-raw-storage`
и что именно нужно резервировать.

### 📈 Настройки мониторинга {#monitoring-settings}

| Параметр | По умолчанию | Назначение |
|-----------|---------|---------|
| `Health__SyncStaleMinutes` | `0` | Включенные учетные записи старше этого значения вызывают отчет `/api/v1/health` `Degraded`. `0` отключает проверку. |
| `Metrics__Enabled` | `false` | Включить эндпоинт метрик Prometheus и цикл его обновления. |
| `Metrics__RefreshMinutes` | `5` | Как часто перестраивается снимок метрик. Значения ниже 1 повышаются до 1. |

Для эндпоинтов health и metrics требуется ключ admin API и `Api__Enabled=true`.
Собственная проверка liveness контейнера не требует аутентификации и не требует конфигурации.
См. **[Health and Metrics](Monitoring.md)** для получения информации об эндпоинтах, именах метрик,
конфигурации Prometheus и проверке состояния Docker (health check).

### 📤 Настройки BatchRestore {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Количество электронных писем, которое запускает асинхронную обработку.
- `BatchRestore__MaxSyncEmails`: Максимальное количество писем для синхронной обработки.
- `BatchRestore__MaxAsyncEmails`: Максимальное количество писем для асинхронной обработки.
- `BatchRestore__SessionTimeoutMinutes`: Время ожидания сессии для пакетного восстановления в минутах.
- `BatchRestore__DefaultBatchSize`: Размер пакета по умолчанию для операций с электронной почтой.

### 🏢 Настройки управления тенантами {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Максимальное количество почтовых ящиков, которые можно добавить за одну операцию управления тенантом. По умолчанию — `1000`. Увеличьте это значение для очень крупных тенантов или уменьшите его, чтобы предотвратить случайный массовый импорт. При превышении лимита операция отклоняется с ошибкой валидации, и учетные записи не создаются. Подробности см. в [M365 Tenant Import Guide](M365TenantImport.md).

### 🐄 Настройки Mailcow {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Максимальное количество почтовых ящиков, которые могут быть созданы за один запуск активации Mailcow. По умолчанию установлено `1000`. Уменьшите это значение, чтобы предотвратить случайное массовое создание ящиков, или увеличьте его для крупных инстансов. Если лимит превышен, аккаунты не будут созданы. Подробности см. в разделе [Mailcow Instances](Mailcow.md).
- Интеграция с Mailcow дополнительно требует `Security__CredentialEncryptionKey` (см. [Security Settings](#-security-settings)). Подключение инстанса и активация почтовых ящиков отклоняются, если шифрование учетных данных отключено, поэтому API key и сгенерированные app passwords никогда не могут храниться в виде открытого текста.
- Требуется исходящий доступ к Mailcow HTTPS API и к IMAP хосту через TLS (обычно порт `993`).

### 📦 Настройки BatchOperation {#batchoperation-settings}
- `BatchOperation__BatchSize`: Размер пакета для операций с электронной почтой.
- `BatchOperation__PauseBetweenEmailsMs`: Пауза между отдельными письмами в миллисекундах.
- `BatchOperation__PauseBetweenBatchesMs`: Пауза между пакетами в миллисекундах.

### 📤 Настройки выгрузки (Offload Settings) {#offload-settings}
Настройки для [date-windowed offload](Offload.md). Все значения по умолчанию воспроизводят поведение приложения, которое было до появления этой функции, поэтому установка, в которой не настроен этот раздел, не подвергается изменениям.
- `Offload__MaxConcurrentJobs`: Количество одновременно выполняемых задач восстановления или выгрузки. По умолчанию установлено `1`, что сохраняет строго последовательную обработку, которую всегда использовала очередь задач. Максимум одна задача на один целевой почтовый ящик выполняется независимо от этого значения.
- `Offload__PrefetchMaxMessages`: Верхний предел количества сообщений, индексируемых из целевого почтового ящика для обнаружения дубликатов. По умолчанию `500000`. Выше этого значения проверка сужается до одной папки, и в лог записывается, что область проверки была уменьшена.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Исходные папки, которые никогда не выгружаются, проверяются перед переименованием и включают подпапки. По умолчанию пусто.
- `Offload__FolderRenameMap__<SourceFolder>`: Перезаписывает начальные сегменты пути исходной папки, например `Offload__FolderRenameMap__Sent Items=Sent`. По умолчанию пусто.
- `Offload__MarkAsSeen`: Помечается ли добавленная почта как прочитанная. По умолчанию `true`, что соответствует существующему поведению при восстановлении.

Обе настройки папок намеренно поставляются пустыми: перезапись или удаление папок без запроса пользователя удивили бы любого, кто уже использует путь восстановления. Таким образом, запуск без конфигурации переносит всё, включая папки со спамом, и создает второй набор специальных папок рядом с собственными папками целевого ящика.

### 📊 Настройки отслеживания пропускной способности {#bandwidth-tracking-settings}
- `BandwidthTracking__Enabled`: Включить или выключить отслеживание пропускной способности для обработки ограничений скорости IMAP (true/false). По умолчанию установлено `false`. Когда эта функция включена, система отслеживает использование пропускной способности для каждого аккаунта и может приостановить синхронизацию при достижении лимитов провайдера. См. [Rate Limit Handling](RateLimitHandling.md) для получения подробной информации.
- `BandwidthTracking__DailyLimitMb`: Ежедневный лимит загрузки в мегабайтах на один аккаунт. По умолчанию `25000` (25 GB). Для провайдеров с ограничениями пропускной способности установите это значение в соответствии с их лимитом скорости (например, `2500` для провайдеров с ежедневным лимитом ~2500 MB). Система приостановит синхронизацию при достижении этого лимита.
- `BandwidthTracking__WarningThresholdPercent`: Процент от ежедневного лимита, при достижении которого в лог записываются предупреждающие сообщения. По умолчанию `80`. Когда использование пропускной способности достигает этого процента, записываются предупреждения, чтобы помочь контролировать приближение к лимитам.
- `BandwidthTracking__PauseHoursOnLimit`: Количество часов для приостановки синхронизации при достижении ежедневного лимита. По умолчанию `24`. По истечении этого периода флаг лимита автоматически сбрасывается и синхронизация возобновляется.
- `BandwidthTracking__TrackUploadBytes`: Нужно ли также отслеживать пропускную способность исходящего трафика (true/false). По умолчанию `false`. Большинство IMAP-провайдеров ограничивают только загрузку (download), поэтому это обычно не требуется.

### 🎯 Настройки выбора {#selection-settings}
- `Selection__MaxSelectableEmails`: Максимальное количество электронных писем, которые можно выбрать одновременно.

### 👁️ Настройки просмотра (Конфиденциальность и отображение) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Управляет режимом просмотра электронной почты по умолчанию для обеспечения конфиденциальности и предотвращения отслеживания (true/false). По умолчанию установлено `false`.
  - При значении `true`: Электронные письма по умолчанию открываются в текстовом режиме (plain-text), что предотвращает автоматическую загрузку пикселей отслеживания, внешних изображений и веб-маяков. Это рекомендуется пользователям, заботящимся о конфиденциальности и защите от отслеживания.
  - При значении `false`: Электронные письма по умолчанию открываются в HTML-режиме, отображая форматированное письмо с изображениями и стилями.
  - Пользователи могут переключаться между HTML и текстовым режимами с помощью кнопки на странице с деталями письма (кнопка видна только тогда, когда доступны оба формата).
  - Ссылка "Full View" учитывает текущий выбранный режим просмотра.

- `View__BlockExternalResources`: Блокирует внешние ресурсы (удаленные изображения, внешние CSS, внешние скрипты, веб-шрифты и т. д.) в HTML-режимах просмотра писем для предотвращения отслеживания и повышения конфиденциальности (true/false). По умолчанию установлено `false`.
  - При значении `true`: Внешние ресурсы фильтруются при отображении HTML-писем. Отображается только встроенный контент и data URIs (включая встроенные вложения через ссылки `cid:`).
  - При значении `false`: HTML-письма отображаются со всеми их исходными внешними ресурсами.
  - **Важно**: Эта настройка влияет только на **отображение** письма. Архивные письма хранятся в базе данных полностью неизменными со всеми сохраненными исходными данными.
  - Блокируемые ресурсы включают:
    - Удаленные изображения (пиксели отслеживания, внешние изображения, размещенные на серверах)
    - Внешние таблицы стилей CSS
    - Внешние шрифты через @font-face
    - Внешние импорты CSS через @import
    - Внешние фоновые изображения
  - Разрешенные ресурсы:
    - Встроенные изображения, внедренные как data: URIs
    - Встроенные вложения, на которые ссылаются через cid: URIs
    - Встроенные стили CSS и теги style
  - Эта настройка работает независимо от `DefaultToPlainText` и обеспечивает дополнительный уровень защиты конфиденциальности при просмотре HTML-писем.

### 📊 Dashboard Settings {#dashboard-settings}

- `Dashboard__ShowDirectionSplits`: Определяет, содержат ли карточки счетчиков свои части входящих и исходящих сообщений, а карточка аккаунта — количество различных доменов. По умолчанию установлено `true`. Если установить `false`, эти числа не будут вычисляться, а вместо вычисления будут скрываться: счетчики будут показывать простое количество, а подсчет вложений перестанет объединяться с письмом, на котором он прикреплен (что является единственным запросом, который добавляют эти части).
- `Dashboard__SelectablePeriods`: Определяет, предлагают ли графики дашборда разрешение, период и стрелки для их изменения. По умолчанию установлено `true`. Если установить `false`, графики будут показывать последние двенадцать месяцев по месяцам и отправителей всего архива; при этом эндпоинт графика будет отвечать так, будто этих параметров не существует, а поиск самой старой даты отправки производиться не будет.
- `Dashboard__CacheSeconds`: Определяет, как долго вычисленная статистика дашборда (общие итоги с разделением на входящие и исходящие, количество по каждому аккаунту, серии графиков, топ отправителей, последние письма, размер базы данных) хранится во встроенном в память кэше сервера. По умолчанию `60` секунд. Установите `0`, чтобы отключить кэширование и всегда пересчитывать статистику. Каждая комбинация разрешения и периода для графиков кэшируется отдельно, поэтому дашборд, переключающийся между вариантами, хранит одну запись на каждый выбор. Более высокие значения снижают нагрузку на базу данных в крупных средах ценой более устаревших данных. Значки статуса синхронизации, значения хранилища и список последних писем всегда запрашиваются в реальном времени и не зависят от этого кэша.
- `Dashboard__RefreshIntervalMinutes`: Определяет, как часто фоновая служба `DashboardStatsRefreshService` пересчитывает ресурсоемкие агрегаты дашборда (общие количества с разделением на входящие и исходящие, количества по аккаунтам, количество доменов аккаунтов, стандартную серию графиков с топ отправителями, размер базы данных) в таблицу базы данных `DashboardStatsCache`. В инсталляциях с несколькими миллионами писем дашборд считывает эти подготовленные значения вместо агрегации всего архива в процессе обработки запроса, что превращает первую загрузку длительностью ~10 секунд в миллисекунды. Эндпоинт графика отвечает на выбор по умолчанию (месяцы за один год, входящие отправители, текущий период) из той же строки, поэтому при возврате к нему отображаются числа из первой отрисовки; все остальные варианты вычисляются по запросу и сохраняются в вышеупомянутом кэше в памяти. По умолчанию `15` минут. Установите `0`, чтобы отключить фоновое обновление; в этом случае дашборд снова будет вычислять статистику в реальном времени (с применением вышеупомянутого кэша в памяти). Отображаемые значения могут отставать от фактического состояния архива на величину, не превышающую настроенный интервал. Строка, вычисленная с другими значениями `ShowDirectionSplits` или `SelectablePeriods`, отличными от текущих, не используется; дашборд вычисляет данные в реальном времени до следующего обновления.

### 🔄 Настройки заданий (Jobs Settings) {#jobs-settings}
- `Jobs__RefreshSeconds`: Как часто страница фоновых заданий перезагружается сама по себе, пока открыта вкладка браузера. По умолчанию составляет `30` секунд. Установите `0`, чтобы отключить автоматическую перезагрузку и обновлять страницу вручную. Каждая перезагрузка заново перестраивает страницу из всех восьми источников заданий, поэтому на установках со множеством аккаунтов более длительный интервал снижает нагрузку, которая умножается на каждую открытую вкладку с этой страницей.

### 🗃️ Настройки Npgsql {#npgsql-settings}
- `Npgsql__CommandTimeout`: Таймаут для команд базы данных в секундах.

### 📥 Настройки загрузки {#upload-settings}
- `Upload__MaxFileSizeGB`: Максимальный размер файла для загрузки в ГБ.
- `Upload__KeepAliveTimeoutHours`: Таймаут ожидания (keep alive) для загрузок в часах.
- `Upload__RequestHeadersTimeoutHours`: Таймаут для заголовков запроса в часах.

### 📂 Настройки локального импорта {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1` и т. д.: Белый список локальных директорий, из которых командам импорта CLI (`--import-mbox`, `--import-eml`) разрешено считывать файлы. Каждая запись является путем внутри контейнера. Вы должны смонтировать ваши файлы для импорта в одну из этих директорий, используя Docker volumes. По умолчанию: `/data/import` (автоматически устанавливается как `/app/uploads` в качестве резервного варианта).
  - При использовании `docker exec` для запуска команды импорта путь к файлу, переданный через `--file`, должен находиться в пределах одного из этих разрешенных путей.
  - Эта мера безопасности предотвращает произвольный доступ к файловой системе из команд CLI.
  - Для различных источников импорта можно настроить несколько путей.
  - Подробные инструкции по использованию см. в разделе [CLI commands](CLI.md).

### 📄 Настройки импорта CSV {#csv-import-settings}
- `CsvImport__MaxRows`: Максимальное количество строк CSV (почтовых ящиков), обрабатываемых за один массовый импорт. По умолчанию — `5000`. Увеличьте это значение для крупных развертываний; уменьшите его, чтобы ограничить влияние одного запуска импорта на нагрузку базы данных.
- `CsvImport__MaxFileSizeBytes`: Максимально допустимый размер (в байтах) загружаемого CSV-файла. По умолчанию — `10000000` (10 MB). При необходимости измените это значение в соответствии с вашими лимитами на загрузку.
- Подробные инструкции по массовому импорту IMAP-аккаунтов через CSV см. в [Account Import Guide](Account-Import.md).

### 📤 Настройки экспорта аудита {#audit-export-settings}
Настройки для страницы экспорта данных аудита (только для администратора, доступна со страницы Logs). Экспорт создает ZIP-пакет с табличными массовыми данными (INDEX.XML + CSV-таблицы + DTD) из существующего архива для внешних инструментов аудита. Подробности использования см. в [Audit Data Export Guide](AuditExport.md).
- `AuditExport__DataSupplierName`: Значение по умолчанию для поля формы "Data supplier name", которое идентифицирует вашу организацию в экспортируемом индексном файле. По умолчанию: пусто.
- `AuditExport__DataSupplierLocation`: Значение по умолчанию для поля формы "Data supplier location" (например, местонахождение компании). По умолчанию: пусто.
- `AuditExport__Comment`: Значение по умолчанию для поля формы "Comment", которое включается в виде произвольного текста в экспортируемый индексный файл. По умолчанию: пусто.
- `AuditExport__OutputDirectory`: Директория, в которой хранятся сгенерированные ZIP-файлы. Относительные пути разрешаются относительно корня содержимого приложения. По умолчанию: `exports/audit`.
- `AuditExport__RetentionDays`: Количество дней, по истечении которых завершенные файлы экспорта удаляются ежедневной очисткой. По умолчанию: `30`.
- `AuditExport__MaxRangeYears`: Максимально допустимый промежуток между датой начала и датой окончания периода экспорта. По умолчанию: `10`.
- Поля формы заполняются этими значениями по умолчанию, но могут быть отредактированы для каждого отдельного экспорта.
- Каждый экспорт записывает запись о начале и запись о результате в лог доступа (тип "Audit Data Export"), поэтому история является безопасной с точки зрения ревизий без каких-либо изменений схемы БД.

### 🔒 Настройки политики удаления {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Определяет, разрешено ли ручное удаление архивированных писем (true/false). По умолчанию установлено `true`. При значении `false`:
  - Все архивированные письма блокируются (`IsLocked = true`) при запуске через триггер соответствия базы данных (database compliance trigger), что предотвращает любое изменение или удаление на уровне базы данных.
  - Ручное удаление (единичное и массовое) блокируется на уровне приложения с выводом сообщения об ошибке.
  - Значение по умолчанию для столбца корректируется таким образом, чтобы вновь импортированные письма также блокировались.
  - Текущее состояние политики записывается в таблицу AccessLogs при каждом запуске (отображается на странице Logs как записи "Deletion Policy") для обеспечения возможности аудита.
  - Удаление по локальному сроку хранения (local retention) является исключением: письма, подпадающие под настроенный период хранения, все равно удаляются (они разблокируются непосредственно перед удалением в процессе обработки срока хранения).
- `DeletionPolicy__MaxBulkDeleteEmails`: Верхняя граница для массового удаления на основе одного фильтра (*Delete all matches*). По умолчанию `100000`. Фильтр, который находит слишком много писем, отклоняется с сообщением с просьбой сузить область поиска; задача никогда не обрезается незаметно. Письма, заблокированные политикой хранения, пропускаются и фиксируются в статусе задачи, а не разблокируются. Задача повторно проверяет этот лимит и `DeletionPolicy__DeletionAllowed` при запуске, поэтому задача, поставленная в очередь до изменения политики, остановится вместо выполнения со старой настройкой.
- **Защита неизменяемости (Immutability protection):** Когда `IsLocked = true`, триггер соответствия базы данных (`prevent_locked_email_changes`) блокирует ЛЮБОЕ изменение строки письма — защищены все столбцы, а не только фиксированный список полей. Единственными исключениями являются столбцы `IsLocked` (чтобы оставалась возможность разблокировки для удаления по сроку хранения и применения политики при запуске) и `FolderName` (чтобы IMAP-синхронизация могла обновлять папку при перемещении письма на стороне сервера). Защита не зависит от столбцов (сравнение на основе JSONB) и автоматически охватывает будущие изменения схемы.

### 🕐 Настройки TimeZone {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: Часовой пояс, используемый для отображения меток времени электронных писем в интерфейсе. Используются идентификаторы часовых поясов IANA (например, "Europe/Berlin", "Asia/Tokyo"). По умолчанию установлено "Etc/UCT" для обеспечения обратной совместимости. При импорте писем метки времени будут преобразованы в этот часовой пояс для целей отображения.

### 🎉 ReleaseNotes Settings (Version Update Splash Screen) {#releasenotes-settings-version-update-splash-screen}
- `ReleaseNotes__AppVersion`: Строка версии, которую сообщает этот экземпляр. Она сопоставляется с тегами релизов GitHub и определяет, существует ли более новая версия. Если значение пустое, список изменений (changelog) берется из версии скомпилированной сборки, а подсказка об обновлении остается выключенной.
- `ReleaseNotes__Enabled`: Включить или выключить всплывающее окно обновления версии (true/false). По умолчанию установлено `true`. При включении администраторы увидят однократное модальное окно со списком изменений после обновления приложения, в котором будут показаны примечания к релизу, полученные из GitHub Releases для текущей версии. Каждый администратор может закрыть модальное окно, и оно появится снова только для новой версии. Установите `false`, чтобы полностью отключить эту функцию, включая подсказку об обновлении.
- `ReleaseNotes__UpdateHintEnabled`: Показывать администраторам небольшое уведомление (toast) в нижнем правом углу, когда GitHub публикует более новую стабильную версию, чем установленная (true/false). По умолчанию установлено `true`. Уведомление содержит ссылку на этот релиз и закрывается в браузере для каждой версии отдельно. Черновики (drafts) и предварительные версии (prereleases) игнорируются. Требует `ReleaseNotes__AppVersion`; без этого никакая подсказка не будет показана.
- `ReleaseNotes__UpdateCheckIntervalHours`: Как часто обновляется список релизов GitHub, в часах. По умолчанию `6`. Более короткий интервал позволяет быстрее увидеть новый релиз в работающем экземпляре, более длинный — сокращает количество вызовов GitHub API. Минимум — `1`.
- `ReleaseNotes__RequestTimeoutSeconds`: Тайм-аут для одного запроса к GitHub API, в секундах. По умолчанию `10`. Если время истекает, список изменений и подсказка об обновлении остаются выключенными для этой попытки вместо того, чтобы держать запрос открытым; следующая проверка будет повторной.

### 🔧 Настройки обслуживания базы данных {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Включить или выключить автоматическое ежедневное обслуживание базы данных (true/false). По умолчанию установлено `false`. При включении система будет автоматически выполнять операции VACUUM ANALYZE для оптимизации производительности базы данных и предотвращения раздувания (bloat). Для получения более подробной информации см. [Database Maintenance Guide](DatabaseMaintenance.md).
- `DatabaseMaintenance__DailyExecutionTime`: Время суток, когда должно выполняться обслуживание базы данных, в 24-часовом формате (HH:mm). По умолчанию установлено `02:00`. Выбирайте время с низкой активностью системы.
- `DatabaseMaintenance__TimeoutMinutes`: Максимальное время, разрешенное для операций обслуживания, в минутах. По умолчанию установлено `30`. Увеличьте это значение для более крупных баз данных.

### 🧬 Настройки дедупликации вложений {#attachment-deduplication-settings}

Дедупликация вложений сохраняет каждый уникальный полезный нагрузок вложения только один раз (с привязкой к содержимому через SHA-256) и является **основной функцией, которая всегда включена** — переключатель вкл/выкл намеренно отсутствует. Настраивать можно только параметры пакетной обработки/планирования, приведенные ниже. Полную информацию см. в [Attachment Deduplication Guide](AttachmentDeduplication.md).

- `AttachmentDeduplication__BatchSize`: Количество существующих вложений, переносимых за одну транзакцию во время разовой фоновой миграции существующих данных. По умолчанию `200`. Большие значения ускоряют миграцию, но увеличивают потребление памяти и нагрузку на БД на каждый пакет.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Необязательная пауза (в миллисекундах) между пакетами миграции для ограничения нагрузки на базу данных в загруженных системах. По умолчанию `0` (без паузы).
- `AttachmentDeduplication__StartupDelaySeconds`: Задержка (в секундах) после запуска приложения перед началом фоновой миграции, чтобы дать время на завершение миграции схемы. По умолчанию `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Интервал (в часах) постоянно работающей очистки мусора (garbage collection), которая удаляет полезные нагрузки вложений, на которые больше не ссылается ни одно письмо. По умолчанию `12`. Это работает независимо от `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Тайм-аут команды базы данных (в секундах) для операций пакета миграции (INSERT с хешированием SHA-256 и UPDATE). По умолчанию `300` (5 минут). Увеличьте это значение для очень больших баз данных или вложений, или уменьшите его, если хотите более быстрого обнаружения ошибок. Если пакет все еще завершается по тайм-ауту, сервис автоматически пробует снова, уменьшив размер пакета вдвое.

### 💾 Настройки хранения аккаунтов {#account-storage-settings}

Отображение хранилища для каждого аккаунта показывает использование хранилища базы данных (все поля почты + вложения) для каждого аккаунта в таблице "Account Overview" в Dashboard и в таблице "Show All" в MailAccounts. Автономная фоновая служба (`AccountStorageRefreshService`) вычисляет значения, суммируя PostgreSQL `pg_column_size` по каждой колонке (охватывая все поля письма без чтения тел писем) и кэширует их в таблице `AccountStorageCache`. Эта служба работает независимо от `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Включить или выключить службу обновления хранилища (true/false). По умолчанию — `true`. При включении служба выполняет возобновляемое заполнение (backfill) ожидающих аккаунтов при запуске (безопасно при сбоях через `AccountStorageBackfillState`), а затем выполняет ежедневное полное обновление. При выключении значения хранилища обновляются только при синхронизации, импорте или удалении электронных писем, но отображаемые значения могут со временем устаревать.
- `AccountStorage__DailyExecutionTime`: Время суток (24-часовой формат `HH:mm`) для ежедневного полного обновления всех аккаунтов. По умолчанию — `02:30`. Выбирайте время в периоды низкой активности системы.
- `AccountStorage__BackfillDelayMs`: Задержка (в миллисекундах) между аккаунтами во время начального заполнения при запуске. По умолчанию — `5000`. Меньшие значения ускоряют заполнение, но увеличивают нагрузку на базу данных; увеличивайте это значение для очень больших архивов, чтобы избежать перегрузки базы данных.
- `AccountStorage__RefreshBatchDelayMs`: Задержка (в миллисекундах) между аккаунтами во время ежедневного полного обновления. По умолчанию — `1000`. Меньшие значения ускоряют обновление, но увеличивают нагрузку на базу данных; увеличивайте это значение для очень больших архивов.
- `AccountStorage__CommandTimeoutSeconds`: Тайм-аут команды базы данных (в секундах) для расчета хранилища на аккаунт (`pg_column_size` по всем строкам аккаунта). По умолчанию — `300` (5 минут). Расчет считывает каждую строку письма, включая данные TOAST, что может занять более минуты на очень больших архивах (250k+ писем); предыдущий 30-секундный тайм-аут по умолчанию Npgsql приводил к скрытому сбою обновления, из-за чего в дашборде отображались устаревшие значения. Увеличьте это значение, если расчет все еще завершается по тайм-ауту на вашем архиве.

> 💡 **Примечание**: Значения хранилища обновляются сразу после каждой синхронизации, импорта или удаления писем по правилам хранения, поэтому отображаемые значения остаются актуальными даже без ежедневного обновления. Ежедневное обновление служит страховкой, которая обрабатывает исключительные случаи (например, прямые изменения в базе данных).

### 📝 Настройки логирования {#logging-settings}
- `Logging__LogLevel__Default`: Уровень логирования по умолчанию для приложения. Доступные уровни: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. По умолчанию — `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Уровень логирования для сообщений фреймворка ASP.NET Core. По умолчанию — `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Уровень логирования для команд базы данных Entity Framework. По умолчанию — `Warning`.

### 🛡️ Security Settings {#security-settings}
- `AllowedHosts`: Список имен хостов, разделенных точкой с запятой, которым разрешено обслуживать приложение. Это помогает предотвратить атаки через HTTP Host header. Пример: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Важно**: Не используйте `*` в производственных средах, так как это отключает проверку заголовка хоста.

#### 🔐 Credential Encryption at Rest {#credential-encryption-at-rest}
Учетные данные почтовых аккаунтов (пароль IMAP, client secret M365 и токены MSA OAuth) могут быть зашифрованы в базе данных. Ключ шифрования хранится **вне** базы данных (переменная окружения / Docker secret), поэтому одного дампа базы данных недостаточно для чтения учетных данных.

- `Security__CredentialEncryptionKey`: Ключ шифрования (KEK), закодированный в Base64, размером 256 бит (32 байта). Сгенерируйте его с помощью:
  ```bash
  openssl rand -base64 32
  ```
  Установите значение как переменную окружения (рекомендуется: `Security__CredentialEncryptionKey` в вашем compose file или окружении контейнера). Приложение не поддерживает Docker secrets в стиле `_FILE` — если вы используете Docker secret, смонтируйте его и загрузите его содержимое в переменную окружения в вашем entrypoint. Никогда не фиксируйте ключ в `appsettings.json`. Когда ключ настроен, учетные данные шифруются при записи, а фоновая служба один раз шифрует существующие учетные данные в открытом виде.
- `Security__EncryptAccountCredentials`: Включить или выключить шифрование новых/обновленных значений учетных данных (true/false). По умолчанию — `true`. Если ключ не настроен, учетные данные хранятся в виде открытого текста, и при запуске регистрируется предупреждение — существующие установки не затронуты, пока не будет предоставлен ключ.
- `Security__CredentialEncryptionBackfill`: Должна ли фоновая служба шифровать существующие учетные данные в открытом виде при запуске (true/false). По умолчанию — `true`. Без настроенного ключа действие не выполняется.
- `Security__CredentialEncryptionKeyPrevious`: Необязательный предыдущий ключ, используемый только для расшифровки значений, зашифрованных с его помощью (для ротации ключей). Новые значения всегда шифруются с помощью `Security__CredentialEncryptionKey`.

  > ⚠️ **Key rotation**: после ротации продолжайте держать `Security__CredentialEncryptionKeyPrevious` настроенным **до тех пор, пока каждое учетное данное не будет перезаписано** (например, путем редактирования аккаунта или автоматического обновления токена OAuth) — существующие значения, зашифрованные старым ключом, *не* перешифровываются автоматически новым ключом. Как только вы удалите предыдущий ключ, любые учетные данные, все еще зашифрованные им, станут нечитаемыми, и их придется вводить заново. Пока он настроен, его необходимо хранить так же надежно, как и текущий ключ.

> ⚠️ **Keep the key safe**: Без ключа зашифрованные учетные данные невозможно восстановить, и их придется вводить заново. Создавайте резервную копию ключа вместе с резервными копиями вашей базы данных (но отдельно от них).
>
> 💡 **Existing installations**: Обновляйтесь безопасно — пока не установлен `Security__CredentialEncryptionKey`, ничего не меняется. Установите ключ, когда будете готовы; существующие учетные данные в открытом виде останутся доступными и будут зашифрованы в фоновом режиме.

### 🔐 OIDC Configuration {#oidc-configuration}

Для получения подробных инструкций по настройке аутентификации OpenID Connect см. [OIDC Implementation Guide](OIDC_Implementation.md).

#### Basic OIDC Settings {#basic-oidc-settings}
- `OAuth__Enabled`: Включить или выключить OIDC аутентификацию (true/false)
- `OAuth__Authority`: URL-адрес authority OpenID Connect (например, https://sts.windows.net/{TENANT-ID}/ для Azure AD)
- `OAuth__ClientId`: client ID, назначенный вашим поставщиком удостоверений
- `OAuth__ClientSecret`: client secret, назначенный вашим поставщиком удостоверений
- `OAuth__DisplayName`: Необязательное отображаемое имя, которое будет показано на кнопке входа OIDC и на странице автоматического перенаправления (например, `PocketID SSO`). Если оно опущено, используется стандартная метка "Login with OAuth".
- `OAuth__ClientScopes__0`: Первый scope, запрашиваемый у поставщика удостоверений (openid)
- `OAuth__ClientScopes__1`: Второй scope, запрашиваемый у поставщика удостоверений (profile)
- `OAuth__ClientScopes__2`: Третий scope, запрашиваемый у поставщика удостоверений (email)
- `OAuth__ResponseMode`: Необязательный режим ответа (response mode) запроса авторизации (`query`, `fragment` или `form_post`). По умолчанию используется `form_post`, значение по умолчанию в ASP.NET Core. Установите `query` для провайдеров, которые не поддерживают `form_post`, таких как Synology SSO Server. См. [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Автоматически одобрять новых OIDC пользователей без необходимости ручного одобрения администратором (true/false). По умолчанию `false`.

#### User Provisioning Settings {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Автоматически одобрять новых OIDC пользователей без необходимости ручного одобрения администратором (true/false). По умолчанию `false`. Если включено, пользователи, прошедшие аутентификацию через OIDC провайдера, немедленно активируются и могут получить доступ к приложению. Если выключено (по умолчанию), новые OIDC пользователи создаются как неактивные и требуют ручной активации администратором. См. [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users) для получения подробной информации.
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1` и т. д.: Адреса электронной почты, которые должны быть автоматически назначены в качестве администраторов. Пользователи с этими адресами электронной почты будут созданы как активные администраторы при первом входе через OAuth, минуя обычный процесс одобрения. Сопоставление адресов электронной почты нечувствительно к регистру.

#### Passwordless Login Settings {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Отключить вход по имени пользователя/паролю (true/false). По умолчанию `false`. Если включено, форма пароля скрывается **и** сервер отклоняет каждый POST-запрос с паролем, поэтому доступен только вход через OAuth. Требует `OAuth__Enabled=true`. Если OAuth отключен, эта настройка не имеет эффекта.
- `OAuth__AutoRedirect`: Автоматически перенаправлять пользователей к OAuth провайдеру (true/false). По умолчанию `false`. Требует, чтобы `OAuth__DisablePasswordLogin` было равно `true`. Перед перенаправлением пользователи увидят кратковременный экран загрузки.

#### Example: Full OIDC-First Configuration {#example-full-oidc-first-configuration}
```yaml
environment:
  - OAuth__Enabled=true
  - OAuth__Authority=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0
  - OAuth__ClientId=your-client-id
  - OAuth__ClientSecret=your-client-secret
  - OAuth__DisplayName=PocketID SSO
  - OAuth__ClientScopes__0=openid
  - OAuth__ClientScopes__1=profile
  - OAuth__ClientScopes__2=email
  - OAuth__DisablePasswordLogin=true
  - OAuth__AutoRedirect=true
  - OAuth__AutoApproveUsers=true
  - OAuth__AdminEmails__0=admin@example.com
  - OAuth__AdminEmails__1=manager@example.com
```

## 🔐 Конфигурация Kestrel HTTPS (опционально) {#kestrel-https-configuration-optional}

Хотя приложение предназначено для доступа через обратный прокси-сервер с HTTPS, вы также можете настроить веб-сервер Kestrel на использование SSL/TLS сертификатов. Это обеспечит сквозное шифрование между обратным прокси-сервером и контейнером приложения.

### Шаги конфигурации {#configuration-steps}

1. **Сгенерируйте или получите SSL-сертификат** в формате PFX (например, `localhost.pfx`)

2. **Добавьте следующие переменные окружения** в ваш `docker-compose.yml` для сервиса `mailarchive-app`:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Обновите сопоставление портов** в сервисе `mailarchive-app`:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Добавьте сопоставление томов** для сертификата:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Поместите файл вашего сертификата** (например, `localhost.pfx`) в директорию `./certs` в вашей хост-системе.

### Объяснение переменных окружения {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: URL-адрес конечной точки HTTP (по умолчанию: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: URL-адрес конечной точки HTTPS (по умолчанию: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Путь к файлу сертификата PFX внутри контейнера
- `Kestrel__Endpoints__Https__Certificate__Password`: Пароль для файла сертификата PFX

> 💡 **Примечание**: Данная конфигурация является необязательной. Если вы используете обратный прокси-сервер с HTTPS (рекомендуется), взаимодействие между обратным прокси и приложением может оставаться по HTTP. Однако для обеспечения максимальной безопасности в критически важных средах вы можете захотеть включить HTTPS также и на Kestrel, чтобы зашифровать весь путь передачи данных.

## 🔑 Управление секретами для продакшена {#secrets-management-for-production}

Жесткое кодирование конфиденциальных данных, таких как пароли баз данных, учетные данные администратора и секреты OAuth client, непосредственно в `docker-compose.yml` является риском безопасности, особенно если файл попадает в систему контроля версий. Рекомендуемый подход — вынести эти значения во внешний `.env` файл, который Docker Compose загружает автоматически.

### 📄 Файл `.env` {#the-env-file}

Создайте `.env` файл в той же директории, что и ваш `docker-compose.yml`, со следующим содержимым:

```env
# Database
POSTGRES_PASSWORD=YourSecureDBPassword123!

# Admin Account
AUTH_USERNAME=admin
AUTH_PASSWORD=YourSecureAdminPassword456!

# OIDC / OAuth Secrets
OAUTH_CLIENT_SECRET=YourOAuthClientSecret

# Credential Encryption at Rest (generate with: openssl rand -base64 32)
CREDENTIAL_ENCRYPTION_KEY=YourBase64Encoded32ByteKey

# Kestrel HTTPS (optional)
KESTREL_CERT_PASSWORD=YourCertPassword
```

**Важно**: Docker Compose автоматически считывает `.env` файл из той же директории — вам **не нужно** ссылаться на него вручную. Все переменные, определенные в `.env`, доступны в `docker-compose.yml` через синтаксис `${VARIABLE_NAME}`.

### 🐳 Адаптированный `docker-compose.yml` {#adapted-docker-composeyml}

`docker-compose.yml` из шагов установки следует адаптировать, чтобы использовать плейсхолдеры вместо жестко закодированных секретов:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=${POSTGRES_PASSWORD}

      # Authentication Settings
      - Authentication__Username=${AUTH_USERNAME}
      - Authentication__Password=${AUTH_PASSWORD}
      # ... other settings remain unchanged ...

      # OIDC Configuration
      - OAuth__ClientSecret=${OAUTH_CLIENT_SECRET}

      # Credential Encryption at Rest
      - Security__CredentialEncryptionKey=${CREDENTIAL_ENCRYPTION_KEY}
    # ... ports, volumes, networks ...

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    # ... volumes, networks ...
```

### 📋 Шаблон `.env.example` для администраторов {#envexample-template-for-administrators}

Создайте `.env.example` файл в вашем репозитории, который будет служить шаблоном для других администраторов. Он содержит плейсхолдеры и документацию, но **не содержит реальных секретов**:

```env
# ============================================
# Mail Archiver - Environment Configuration
# ============================================
# Copy this file to .env and fill in your values.
# Never commit the actual .env file to version control!

# --- PostgreSQL ---
POSTGRES_PASSWORD=change_me_db_password

# --- Admin Account ---
AUTH_USERNAME=admin
AUTH_PASSWORD=change_me_admin_password

# --- OIDC / OAuth ---
OAUTH_CLIENT_SECRET=change_me_client_secret

# --- Credential Encryption at Rest ---
# Generate with: openssl rand -base64 32
CREDENTIAL_ENCRYPTION_KEY=change_me_base64_32_byte_key

#...and so on
```

### ✅ Лучшие практики {#best-practices}

- **Используйте `.env.example` в качестве шаблона**
- **Установите строгие права доступа к файлам** — ограничьте доступ к `.env` файлу:
  ```bash
  chmod 600 .env
  ```
- **Используйте сложные пароли** — генерируйте длинные случайные пароли (минимум 16 символов) с использованием букв, цифр и специальных символов.
- **Ограничьте доступ к `.env` файлу** — только пользователь, запускающий Docker Compose, должен иметь права на чтение `.env` файла.

## 🔒 Примечания по безопасности {#security-notes}

- Используйте сложные пароли и меняйте учетные данные по умолчанию. Пароли должны состоять минимум из 12 символов и включать в себя сочетание заглавных и строчных букв, цифр и специальных символов. Избегайте использования распространенных слов или легко угадываемой информации.
- В производственной среде (production) рекомендуется внедрить HTTPS с использованием обратного прокси-сервера (reverse proxy).
- Рекомендуется регулярно делать резервные копии базы данных PostgreSQL. Подробные процедуры резервного копирования и восстановления см. в [Backup and Restore Guide](BackupRestore.md).
