# Read-Only REST API (v1) {#read-only-rest-api-v1}

Mail Archiver поставляется с опциональным **read-only** REST API для программного доступа к архивированной почте. Он предназначен для сценариев автоматизации и интеграции (отчетность, экспорт для e-discovery, мониторинг) без использования веб-интерфейса.

Суть заключается в том, чтобы позволить скрипту или ИИ-агенту читать архивированную почту, **никогда не раскрывая учетные данные почтового ящика и не взаимодействуя напрямую с почтовым провайдером.** Ограниченный API-ключ пользователя — это возможность *читать архив*, а не учетные данные, позволяющие *стать почтовым ящиком*. Он наследует только разрешения почтового ящика владельца, не предоставляет прав на запись и может быть мгновенно отозван без изменения настроек основного почтового аккаунта.

> ⚠️ **API отключен по умолчанию.** Его необходимо явно включить в конфигурации (см. [Enabling the API](#enabling-the-api)). Если API отключен, каждый маршрут `/api/*` возвращает `404 Not Found`.

API **по своей конструкции предназначен только для чтения**: он предоставляет доступ к учетным записям, папкам, поиску сообщений, деталям сообщений и загрузке вложений. Он никогда не создает, не изменяет и не удаляет данные.

## Содержание {#table-of-contents}

- [Включение API](#enabling-the-api)
- [Аутентификация](#authentication)
  - [Формат API key](#api-key-format)
  - [Жизненный цикл ключа](#key-lifecycle)
  - [Модель разрешений](#permission-model)
  - [Обоснование безопасности](#security-rationale)
- [Соглашения](#conventions)
  - [Base URL и версионирование](#base-url-and-versioning)
  - [Пагинация](#pagination)
  - [Ошибки](#errors)
  - [Rate limiting](#rate-limiting)
- [Эндпоинты](#endpoints)
  - [List accounts](#list-accounts)
  - [List folders](#list-folders)
  - [Search messages](#search-messages)
  - [Get message](#get-message)
  - [Download attachment](#download-attachment)
  - [Download message (.eml)](#download-message-eml)
  - [Download original message](#download-original-message)
  - [Stats](#stats)
  - [Health](#health)
  - [Metrics](#metrics)
- [Типы данных](#data-types)
- [Логирование доступа](#access-logging)
- [OpenAPI и Swagger UI](#openapi-and-swagger-ui)
- [API Reference](API-Reference.md) — автоматически сгенерированный справочник эндпоинтов и типов данных

## Включение API {#enabling-the-api}

API настраивается в разделе `Api` `appsettings.json` (или через эквивалентные переменные окружения, например, `Api__Enabled=true`):

```json
"Api": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "AllowMessageDownloads": true,
  "EnableSwaggerUi": true,
  "DefaultPageSize": 20,
  "MaxPageSize": 100,
  "RateLimitPerMinute": 120
}
```


### Жизненный цикл ключа {#key-lifecycle}

1. **Create** — пользователь создает ключ с описательным `Name` и необязательным
   сроком действия. В ответе ключ в открытом виде отображается только один раз.
2. **Use** — отправляйте ключ как bearer token. Каждый принятый запрос обновляет
   `LastUsedAt` ключа (запись ограничена частотой не более одного раза в ~5 минут, чтобы
   избежать записи при каждом вызове).
3. **Expire** — если установлен `ExpiresAt` и это время уже прошло, ключ отклоняется.
4. **Revoke** — владелец ключа или администратор отзывает ключ
   (устанавливается `RevokedAt`); он немедленно и навсегда отклоняется.

Ключ считается **active**, если он не отозван, не просрочен **и** его владелец
все еще активен. Ключ, неактивный по любой из этих причин, отклоняется с ошибкой `401`.

### Модель разрешений {#permission-model}

API-ключ наследует именно те разрешения, которыми обладает его владелец.
Аутентифицированный субъект (principal) несет те же claims, которые выдаются при
обычном веб-входе, поэтому все существующие авторизац
| Setting | Default | Description |
| --- | --- | --- |
| `Enabled` | `false` | Главный переключатель. Когда `false`, все маршруты `/api/*` возвращают `404`, а документ OpenAPI и Swagger UI не отображаются. |
| `AllowAttachmentDownloads` | `true` | Когда `false`, эндпоинт загрузки вложений возвращает `403`. Метаданные о вложениях по-прежнему возвращаются эндпоинтом деталей сообщения. |
| `AllowMessageDownloads` | `true` | Когда `false`, эндпоинты complete-message (`/eml` и `/raw`) возвращают `403`. Полное сообщение включает в себя свои вложения, поэтому **оба** этот переключатель и `AllowAttachmentDownloads` должны быть `true`; отключение любого из них отключает оба эндпоинта. |
| `EnableSwaggerUi` | `true` | Когда `true` (и `Enabled` установлено в `true`), монтирует Swagger UI по адресу `/apidocs` и документ OpenAPI по адресу `/apidocs/spec/v1.json`. Оба требуют аутентифицированной **веб-сессии (cookie)** — они не являются частью `/api/` и недоступны с помощью API-ключа. |
| `DefaultPageSize` | `20` | Размер страницы, используемый, когда в запросе отсутствует `pageSize`. |
| `MaxPageSize` | `100` | Верхняя граница; значения `pageSize` больше этого значения будут ограничены этим пределом. |
| `RateLimitPerMinute` | `120` | Бюджет запросов в фиксированном окне на один API-ключ в минуту (см. [Rate limiting](#rate-limiting)). |

## Authentication {#authentication}

API выполняет аутентификацию с помощью **индивидуальных API-ключей пользователя**, передаваемых в качестве bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` принимает **только** API-ключи. Браузерные cookie не учитываются, поэтому у API нет поверхности для CSRF-атак. Ошибки аутентификации возвращают `401` с заголовком `WWW-Authenticate: Bearer` и телом в формате [problem+json](#errors) — API **никогда** не перенаправляет на страницу входа.

Управление ключами осуществляется пользователями самостоятельно через веб-интерфейс в разделе **API Keys** (каждый пользователь управляет своими ключами; администраторы могут просматривать и отзывать ключи любого пользователя).

### API key format {#api-key-format}

Ключ представляет собой строку `ma_`, за которой следуют 43 URL-safe base64 символа, кодирующих 32 случайных байта (256 бит энтропии):

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Хранение:

- **`KeyPrefix`** — первые ~11 символов (например, `ma_8Kf3pQ2`), хранятся в открытом виде. Используются для идентификации ключа в UI и в логах. Не является секретом.
- **`KeyHash`** — SHA-256 от полного ключа в шестнадцатеричном кодировании, хранящийся под **уникальным индексом**. Полный ключ в открытом виде никогда не сохраняется.

Полный ключ показывается пользователю **ровно один раз** в момент создания. Его невозможно получить позже; утерянный ключ необходимо отозвать и заменить новым.

### Key lifecycle {#key-lifecycle}

1. **Create** — пользователь создает ключ с описанием `Name` и необязательным сроком действия. В ответе ключ в открытом виде отображается один раз.
2. **Use** — отправка ключа в качестве bearer token. Каждый принятый запрос обновляет `LastUsedAt` ключа (запись обновляется не чаще одного раза в ~5 минут, чтобы избежать записи при каждом вызове).
3. **Expire** — если установлен `ExpiresAt` и это время уже прошло, ключ отклоняется.
4. **Revoke** — владелец ключа или администратор отзывает ключ (устанавливается `RevokedAt`); он немедленно и навсегда отклоняется.

Ключ считается **active**, если он не отозван, не просрочен **и** его владелец все еще активен. Ключ, неактивный по любой из этих причин, отклоняется с ошибкой `401`.

### Permission model {#permission-model}

API-ключ наследует ровно те же разрешения, которыми обладает его владелец. Аутентифицированный субъект (principal) несет те же claims, что и при обычном входе через веб-интерфейс, поэтому вся существующая логика авторизации применяется без изменений:

- **Administrators** (`IsAdmin`) видят все почтовые аккаунты и все сообщения.
- **Regular users** видят только назначенные им почтовые аккаунты (`UserMailAccount`). Аккаунты и сообщения вне этого набора рассматриваются как **несуществующие**: запросы к ним возвращают `404`, а не `403`, поэтому API не раскрывает факт существования аккаунтов, которые вызывающая сторона не может видеть.

### Security rationale {#security-rationale}

- **Обход 2FA.** API-ключи по своей конструкции обходят двухфакторную аутентификацию — это свойственно неинтерактивным токенам (то же самое относится к personal access tokens в GitHub/GitLab). Это нивелируется тем, что ключи **выключены по умолчанию**, их можно отозвать, у них может быть срок действия, они ограничены правами пользователя, а каждый доступ фиксируется в [access log](#access-logging).
- **Отсутствие cookie на `/api`.** Аутентификация через cookie не принимается на маршрутах `/api`, что устраняет опасения по поводу CSRF для API.
- **Транспорт.** В рабочей среде (production) всегда используйте HTTPS для работы с API, чтобы bearer tokens не передавались в открытом виде.

## Соглашения {#conventions}

### Базовый URL и версионирование {#base-url-and-versioning}

Все эндпоинты находятся под `/api/v1`. Версия указана в пути, чтобы будущие критические изменения могли выпускаться как `/api/v2`, не нарушая работу существующих клиентов.

Ответы являются `application/json`, если не указано иное (загрузка вложений возвращает собственный content type вложения). Все временные метки указаны в UTC, ISO 8601 (`2026-06-11T09:30:00Z`).

### Пагинация {#pagination}

Эндпоинты списков возвращают оболочку пагинации:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` начинается с 1. Страницы вне диапазона возвращают пустой массив `items` (не ошибка).
- `pageSize` по умолчанию равен `Api:DefaultPageSize` и ограничен значением `[1, Api:MaxPageSize]`.

### Ошибки {#errors}

Ошибки используют [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
`application/problem+json`:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Unknown sortBy value 'foo'.",
  "instance": "/api/v1/emails"
}
```

| Статус | Когда |
| --- | --- |
| `400 Bad Request` | Некорректные параметры (например, нераспознанная дата, невалидный `sortBy`). |
| `401 Unauthorized` | Отсутствующий, некорректный, просроченный, отозванный или иным образом недействительный ключ. Включая `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Запрос на загрузку вложения во время `AllowAttachmentDownloads=false` или запрос на загрузку сообщения во время `AllowMessageDownloads=false`. |
| `404 Not Found` | Ресурс не существует **или** находится за пределами разрешенных аккаунтов вызывающей стороны. Также возвращается для каждого маршрута `/api/*`, когда `Api:Enabled=false`. |
| `429 Too Many Requests` | Превышен лимит запросов (Rate limit exceeded). |
| `500 Internal Server Error` | Непредвиденная ошибка сервера (problem+json, никогда не HTML-страница ошибки). |

### Ограничение частоты запросов (Rate limiting) {#rate-limiting}

Запросы ограничены политикой фиксированного окна в `Api:RateLimitPerMinute`
запросов в минуту (по умолчанию `120`), разделенных по префиксу API-ключа (с переходом на IP-адрес клиента, если ключ отсутствует). Превышение лимита возвращает `429`.

## Эндпоинты {#endpoints}

> Все примеры предполагают, что API включен и `$KEY` содержит действительный ключ.

### List accounts {#list-accounts}

```
GET /api/v1/accounts
```

Возвращает почтовые аккаунты, видимые вызывающей стороне. Поля учетных данных
(IMAP/OAuth username, password, client secret, …) **никогда** не включаются.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/accounts
```

```json
[
  {
    "id": 1,
    "name": "Support Mailbox",
    "emailAddress": "support@example.com",
    "provider": "IMAP",
    "isEnabled": true,
    "lastSync": "2026-06-11T08:15:00Z"
  }
]
```

### List folders {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

Возвращает дерево папок для одного аккаунта в виде вложенной структуры. Возвращает `404`, если
аккаунт не существует или недоступен вызывающему лицу.

```json
[
  {
    "name": "INBOX",
    "fullPath": "INBOX",
    "totalCount": 4213,
    "level": 0,
    "children": [
      { "name": "Work", "fullPath": "INBOX/Work", "totalCount": 512, "level": 1, "children": [] }
    ]
  }
]
```

### Поиск сообщений {#search-messages}

```
GET /api/v1/emails
```

Выполняет поиск по архивным сообщениям с теми же возможностями, что и веб-интерфейс.

| Параметр запроса | Тип | Описание |
| --- | --- | --- |
| `q` | string | Полнотекстовый запрос по полям subject, body, from, to, cc, bcc. Поддерживает синтаксис полей, например, `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Самая ранняя дата отправки (включительно), `YYYY-MM-DD`. |
| `to` | date | Самая поздняя дата отправки (включительно), `YYYY-MM-DD`. |
| `accountId` | int | Ограничить одним аккаунтом (должен быть доступен вызывающему пользователю). |
| `folder` | string | Ограничить одной папкой по полному пути. |
| `direction` | string | `incoming` или `outgoing`. |
| `page` | int | Номер страницы (начиная с 1, по умолчанию `1`). |
| `pageSize` | int | Количество результатов на странице (по умолчанию `Api:DefaultPageSize`, ограничено значением `Api:MaxPageSize`). |
| `sortBy` | string | Одно из `sentDate` (по умолчанию), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` или `desc` (по умолчанию `desc`). |

Возвращает [paged envelope](#pagination) со списком [`EmailSummaryDto`](#emailsummarydto).
Создает запись в [access log](#access-logging) типа `Search`.

```bash
curl -H "Authorization: Bearer $KEY" \
  "https://host/api/v1/emails?q=subject:invoice&from=2026-01-01&pageSize=50"
```

```json
{
  "items": [
    {
      "id": 9876,
      "accountId": 1,
      "subject": "Invoice #4711",
      "from": "billing@vendor.com",
      "to": "support@example.com",
      "sentDate": "2026-03-02T11:04:00Z",
      "isOutgoing": false,
      "hasAttachments": true,
      "folderName": "INBOX"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalItems": 3,
  "totalPages": 1
}
```

### Get message {#get-message}

```
GET /api/v1/emails/{id}
```

Возвращает полное сообщение, включая тела. Для тела используется та же цепочка резервных механизмов, что и в веб-интерфейсе: `OriginalBodyHtml` (сырые байты) → `BodyUntruncatedHtml` →
`HtmlBody`, и эквивалентная цепочка для текстового тела. Возвращает `404`, если сообщение
не существует или принадлежит аккаунту, на который у вызывающей стороны нет прав доступа. Записывает запись в журнал доступа типа `Open`.

Возвращает [`EmailDetailDto`](#emaildetaildto).

### Скачивание вложения {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Передает байты вложения с его сохраненным типом содержимого и именем файла. Возвращает
`404`, если сообщение или вложение не существует или невидимо для вызывающей стороны. Возвращает `403` (problem+json), когда `Api:AllowAttachmentDownloads=false`.
Создает запись в журнале доступа типа `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Download message (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Возвращает сообщение в формате `message/rfc822`, воссозданное на основе архивных полей:
адресов, сохраненных необработанных заголовков (`Received`, `Authentication-Results`,
`X-Spam-*`, …), тел писем и вложений. Работает для каждого архивного письма. Это
такая же реконструкция, которую выдает экспорт аккаунта в веб-интерфейсе, поэтому она **не**
является байт-в-байт идентичной полученному сообщению: подписи DKIM не пройдут проверку.
Для этого используйте [`/raw`](#download-original-message).

Возвращает `404`, если сообщение не существует или не доступно вызывающему лицу, и
`403` (problem+json), если `Api:AllowMessageDownloads=false`. Создает запись в журнале доступа типа `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Скачать исходное сообщение {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Возвращает исходное сообщение в том виде, в котором оно было получено (`message/rfc822`), для анализа спама,
проверки DKIM или криминалистической экспертизы. Исходное сообщение сохраняется только в том случае, если
была установлена настройка `RawMessages:Enabled` в момент архивации электронного письма; см.
[Original Message Storage Settings](Setup.md#-original-message-storage-settings).
Наличие исходного сообщения у письма отображается через `hasRawMessage` в
[`EmailDetailDto`](#emaildetaildto).

| Заголовок ответа | Значение |
| --- | --- |
| `X-Raw-Message-Sha256` | SHA-256 возвращаемых байтов, записанный в момент сохранения сообщения |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (точные копии) или `serialized` (переписано парсером, окончания строк нормализованы до CRLF) |
| `X-Raw-Message-Storage` | `postgres` или `filesystem` — источник, из которого были прочитаны байты (см. [Storage backend](RawMessages.md#storage-backend)) |

Байты могут храниться в базе данных или в файле, в зависимости от
`RawMessages:Storage` в момент архивации сообщения; эндпоинт определяет это для каждого сообщения, поэтому оба типа данных обслуживаются одинаково. При использовании
`RawMessages:FileSystem:VerifyChecksumOnRead` (по умолчанию `true`) файл
повторно хешируется перед отправкой, и сообщение, содержимое которого больше не соответствует
записанной контрольной сумме, **не** будет отправлено.

Возвращает `404`, если сообщение не существует или невидимо для вызывающей стороны,
`404` с заголовком проблемы `No original message is stored for this email.`,
если сообщение существует, но исходник отсутствует, и `500` с заголовком
`Stored original message failed its checksum.`, если сохраненное содержимое больше не
соответствует контрольной сумме, записанной в момент архивации. Возвращает `403` (problem+json), когда
`Api:AllowMessageDownloads=false`. Записывает запись в лог доступа типа `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -o 9876.eml \
  https://host/api/v1/emails/9876/raw
sha256sum 9876.eml          # matches X-Raw-Message-Sha256
spamassassin -t < 9876.eml  # or: rspamc < 9876.eml
```

### Stats {#stats}

```
GET /api/v1/stats
```

Возвращает агрегированную статистику архива, доступную вызывающей стороне: общее количество заархивированных
сообщений, количество доступных почтовых аккаунтов, количество вложений в этих сообщениях и общий
размер базы данных PostgreSQL в мегабайтах. Для пользователей без прав администратора значения ограничены
только теми почтовыми аккаунтами, которые им назначены (аналогично ограничению в веб-панели управления);
администраторы видят глобальные итоги.

Для API-ключей без прав администратора `databaseSizeInMB` является пустой строкой: размер базы данных
относится ко всему экземпляру, а не к области доступа ключа. Emails, Accounts и Attachments
всегда ограничены аккаунтами, к которым ключ имеет доступ.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/stats
```

```json
{
  "emails": 160132,
  "accounts": 4,
  "attachments": 43540,
  "databaseSizeInMB": "5432"
}
```

### StatsDto {#statsdto}

| Field | Type | Notes |
| --- | --- | --- |
| `emails` | int | Количество архивированных сообщений, видимых вызывающему пользователю |
| `accounts` | int | Количество почтовых аккаунтов, видимых вызывающему пользователю |
| `attachments` | int | Количество вложений в этих видимых сообщениях |
| `databaseSizeInMB` | string | Полный размер базы данных PostgreSQL в МБ; пустое значение для ключей без прав администратора |

### Health {#health}

```
GET /api/v1/health/live
GET /api/v1/health/ready
GET /api/v1/health
```

Для всех трех требуется admin API key. `health/live` отвечает 200, не обращаясь к какой-либо
зависимости. `health/ready` проверяет PostgreSQL и схему архива и возвращает 503, если
хотя бы одна из них недоступна. `health` также запускает проверку свежести синхронизации и возвращает `Degraded`
(HTTP 200), если включенная учетная запись не синхронизировалась в течение `Health:SyncStaleMinutes`.

```json
{"status":"Healthy","checks":{"database":"Healthy","sync":"Healthy"}}
```

Полное описание, включая поведение 401/403/404 и проверку состояния Docker, приведено в
**[Health and Metrics](Monitoring.md)**.

### Metrics {#metrics}

```
GET /api/v1/metrics
```

Возвращает данные в формате Prometheus text exposition format. Требуется admin API key и `Metrics:Enabled=true`; в противном случае эндпоинт возвращает 404. Названия метрик, метки и примеры алертов приведены в разделе **[Health and Metrics](Monitoring.md)**.

## Типы данных {#data-types}

### MailAccountDto {#mailaccountdto}

| Field | Type | Notes |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

Поля учетных данных никогда не сериализуются.

### FolderNodeDto {#foldernodedto}

| Field | Type | Notes |
| --- | --- | --- |
| `name` | string | Leaf name |
| `fullPath` | string | Full path |
| `totalCount` | int | Messages in folder |
| `level` | int | Nesting depth (0 = root) |
| `children` | FolderNodeDto[] | Sub-folders |

### EmailSummaryDto {#emailsummarydto}

| Field | Type |
| --- | --- |
| `id` | int |
| `accountId` | int |
| `subject` | string |
| `from` | string |
| `to` | string |
| `sentDate` | datetime |
| `isOutgoing` | bool |
| `hasAttachments` | bool |
| `folderName` | string |

### EmailDetailDto {#emaildetaildto}

Все из [`EmailSummaryDto`](#emailsummarydto) плюс:

| Field | Type | Notes |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Resolved via the body fallback chain |
| `htmlBody` | string | Resolved via the body fallback chain |
| `attachments` | AttachmentDto[] | Metadata only |
| `hasRawMessage` | bool | `true` when the original message is stored and [`/raw`](#download-original-message) returns it |

### AttachmentDto {#attachmentdto}

| Field | Type | Notes |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bytes |

Байты вложения получаются через [download endpoint](#download-attachment),
а не из этого DTO.

## Логирование доступа {#access-logging}

Каждый случай доступа к данным через API записывается в тот же журнал доступа, который используется веб-интерфейсом (доступен в разделе **Logs**), и относится к пользователю, владеющему ключом:

| Endpoint | Тип записи в логе |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI и Swagger UI {#openapi-and-swagger-ui}

Когда API включен и `Api:EnableSwaggerUi=true`:

- Документ OpenAPI v1 доступен по адресу **`/apidocs/spec/v1.json`**.
- Swagger UI доступен по адресу **`/apidocs`**.

Оба пути находятся за пределами `/api/` и, следовательно, защищены обычной веб-аутентификацией (cookie) — для их просмотра требуется сессия авторизованного пользователя в браузере, и к ним нельзя получить доступ с помощью API key.

### Справочник эндпоинтов на этом сайте {#endpoint-reference-on-this-site}

Эндпоинты и типы данных также публикуются здесь в виде сгенерированной страницы:
**[API Reference](API-Reference.md)**. Она отрисовывается на основе того же документа OpenAPI — копия хранится в `doc/assets/openapi-v1.json` — с помощью `tools/gen_api_reference.py`. CI выдает ошибку, если страница и спецификация перестают синхронизироваться, поэтому справочник всегда описывает именно ту версию API, которая поставляется в продукте.

!!! note "Регенерация"
    Эта страница является сгенерированным выводом. Измените API (или генератор), затем запустите `python3 tools/gen_api_reference.py` и закоммитьте результат.

    Обновление самой поставляемой спецификации означает повторный запуск процесса генерации документации во время сборки, описанного в `tools/README.md`.
