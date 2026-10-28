# REST API (v1)

Mail Archiver ships an optional REST API for programmatic access to archived
mail. It is designed for automation and integration use cases (reporting,
e-discovery exports, monitoring, feeding an archive from a script) without going
through the web UI.

The point is to let a script or AI agent work with archived mail **without ever
exposing mailbox credentials or talking to the mail provider.** A scoped
per-user API key is a capability on *the archive*, not a credential to *be the
mailbox* — it inherits only its owner's mailbox permissions and can be revoked
instantly without touching the underlying mail account.

> ⚠️ **The API is disabled by default.** It must be explicitly enabled in
> configuration (see [Enabling the API](#enabling-the-api)). When disabled,
> every `/api/*` route returns `404 Not Found`.

The API is **read-only unless you opt in to writes**:

- **Read endpoints** (accounts, folders, message search, message detail,
  attachment downloads, stats) work with any active key.
- **Write endpoints** (create/update/delete accounts, import/move/delete
  messages, manage keys) require **all** of: `Api:AllowWrites=true`, a key that
  carries the `write` scope, and — for destructive calls — the matching
  `Api:AllowAccountDeletion` / `Api:AllowEmailDeletion` switch. Deleting archived
  mail is additionally blocked while `DeletionPolicy:DeletionAllowed` is `false`.

Every key that existed before the write API was introduced is stored with the
`read` scope only, so upgrading never widens what an existing key can do.

## Table of contents

- [Enabling the API](#enabling-the-api)
- [Authentication](#authentication)
  - [API key format](#api-key-format)
  - [Key lifecycle](#key-lifecycle)
  - [Permission model](#permission-model)
  - [Scopes](#scopes)
  - [Security rationale](#security-rationale)
- [Conventions](#conventions)
  - [Base URL and versioning](#base-url-and-versioning)
  - [Pagination](#pagination)
  - [Errors](#errors)
  - [Rate limiting](#rate-limiting)
- [Read endpoints](#read-endpoints)
  - [List accounts](#list-accounts)
  - [Get account](#get-account)
  - [List folders](#list-folders)
  - [Search messages](#search-messages)
  - [Get message](#get-message)
  - [Download attachment](#download-attachment)
  - [Stats](#stats)
- [Write endpoints](#write-endpoints)
  - [Create account](#create-account)
  - [Update account (PUT)](#update-account-put)
  - [Patch account (PATCH)](#patch-account-patch)
  - [Delete account](#delete-account)
  - [Account deletion status](#account-deletion-status)
  - [Import message](#import-message)
  - [Move message](#move-message)
  - [Delete message](#delete-message)
  - [List keys](#list-keys)
  - [Create key](#create-key)
  - [Revoke key](#revoke-key)
- [Data types](#data-types)
- [Access logging](#access-logging)
- [OpenAPI and Swagger UI](#openapi-and-swagger-ui)
- [API Reference](API-Reference.md) — generated endpoint and data-type reference

## Enabling the API

The API is configured under the `Api` section of `appsettings.json` (or the
equivalent environment variables, e.g. `Api__Enabled=true`):

```json
"Api": {
  "Enabled": false,
  "AllowWrites": false,
  "AllowAccountDeletion": false,
  "AllowEmailDeletion": false,
  "AllowAttachmentDownloads": true,
  "EnableSwaggerUi": true,
  "DefaultPageSize": 20,
  "MaxPageSize": 100,
  "RateLimitPerMinute": 120,
  "MaxImportBytes": 25000000
}
```

| Setting | Default | Description |
| --- | --- | --- |
| `Enabled` | `false` | Master switch. When `false`, all `/api/*` routes return `404` and the OpenAPI document / Swagger UI are not mapped. |
| `AllowWrites` | `false` | Master switch for every non-GET endpoint. When `false`, write calls return `403` even for a write-scoped key. |
| `AllowAccountDeletion` | `false` | Allows `DELETE /api/v1/accounts/{id}`, which deletes the account **and every message archived for it**. |
| `AllowEmailDeletion` | `false` | Allows `DELETE /api/v1/emails/{id}`. |
| `AllowAttachmentDownloads` | `true` | When `false`, the attachment download endpoint returns `403`. Metadata about attachments is still returned by the message detail endpoint. |
| `EnableSwaggerUi` | `true` | When `true` (and `Enabled` is `true`), mounts Swagger UI at `/apidocs` and the OpenAPI document at `/apidocs/spec/v1.json`. Both require an authenticated **web (cookie) session** — they are not part of `/api/` and are not reachable with an API key. |
| `DefaultPageSize` | `20` | Page size used when a request omits `pageSize`. |
| `MaxPageSize` | `100` | Upper bound; larger `pageSize` values are clamped to this. |
| `RateLimitPerMinute` | `120` | Fixed-window request budget per API key per minute (see [Rate limiting](#rate-limiting)). |
| `MaxImportBytes` | `25000000` | Upper bound for the decoded message accepted by `POST /api/v1/emails` (25 MB). Larger payloads return `413`. |



## Authentication

The API authenticates with **per-user API keys** sent as a bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` accepts API keys **only**. Browser cookies are not honoured, so the
API has no CSRF surface. Authentication failures return `401` with a
`WWW-Authenticate: Bearer` header and a [problem+json](#errors) body — the API
**never** redirects to the login page.

Keys are managed self-service in the web UI under **API Keys** (each user
manages their own keys; administrators can view and revoke any user's keys) or
through the [key endpoints](#list-keys) with a write-scoped key.

### API key format

A key is the string `ma_` followed by 43 URL-safe base64 characters encoding 32
random bytes (256 bits of entropy):

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Storage:

- **`KeyPrefix`** — the first ~11 characters (e.g. `ma_8Kf3pQ2`), stored in
  plaintext. Used to identify a key in the UI and in logs. Not a secret.
- **`KeyHash`** — the hex-encoded SHA-256 of the full key, stored under a
  **unique index**. The plaintext key is never stored.

The full key is shown to the user **exactly once**, at creation time. It cannot
be retrieved afterwards; a lost key must be revoked and replaced.

### Key lifecycle

1. **Create** — a user creates a key with a descriptive `Name`, an optional
   expiry and an optional write scope. The response shows the plaintext key once.
2. **Use** — send the key as a bearer token. Each accepted request refreshes the
   key's `LastUsedAt` (writes are throttled to at most once every ~5 minutes to
   avoid a write on every call).
3. **Expire** — if `ExpiresAt` is set and in the past, the key is rejected.
4. **Revoke** — the owning user or an administrator revokes the key
   (`RevokedAt` is set); it is immediately and permanently rejected.

A key is **active** when it is not revoked, not expired, **and** its owning user
is still active. A key inactive for any of these reasons is rejected with `401`.

### Permission model

An API key inherits the exact permissions of the user who owns it. The
authenticated principal carries the same claims a normal web login issues, so
all existing authorization logic applies unchanged:

- **Administrators** (`IsAdmin`) see all mail accounts and all messages.
- **Regular users** see only the mail accounts assigned to them
  (`UserMailAccount`). Accounts and messages outside that set are treated as
  **non-existent**: requests for them return `404`, not `403`, so the API does
  not leak the existence of accounts a caller cannot see.
- **Account management** (create, update, delete) additionally requires the
  `Admin` or `SelfManager` role, mirroring the web UI. A self-manager's new
  account is automatically assigned to them; administrators see every account.

### Scopes

A key carries a scope list, stored as a comma-separated value:

| Scope | Grants |
| --- | --- |
| `read` | Every GET endpoint. This is the default and the only scope of keys created before the write API existed. |
| `write` | Every non-GET endpoint (account create/update/delete, message import/move/delete, key management). |

Write access is a **three-part** decision — all of these must be true:

1. `Api:AllowWrites=true` in the deployment configuration,
2. the key carries the `write` scope,
3. for deletions, the matching `Api:AllowAccountDeletion` /
   `Api:AllowEmailDeletion` switch is on and `DeletionPolicy:DeletionAllowed`
   is `true`.

A read-only key cannot escalate itself: creating a key through the API requires
an existing write-scoped key. Set the scope when creating a key in the web UI
with **Allow write access** (the checkbox is off by default).

```
read            # read-only (default)
read,write      # full access, subject to the Api switches above
```

### Security rationale

- **2FA bypass.** API keys bypass two-factor authentication by design — this is
  inherent to non-interactive tokens (the same is true of personal access tokens
  on GitHub/GitLab). It is mitigated by: keys being **off by default**,
  revocable, optionally expiring, scoped to the user's permissions, and every
  access being recorded in the [access log](#access-logging).
- **No cookies on `/api`.** Cookie authentication is not accepted on `/api`
  routes, eliminating CSRF concerns for the API.
- **Least privilege for writes.** A key is read-only unless it was explicitly
  created with the `write` scope, and writes stay disabled until the deployment
  enables `Api:AllowWrites`. A leaked read-only key cannot modify or delete the
  archive, and destructive endpoints need a second switch on top of the scope.
- **Transport.** Always serve the API over HTTPS in production so bearer tokens
  are not exposed on the wire.

## Conventions

### Base URL and versioning

All endpoints are under `/api/v1`. The version is in the path so future
breaking changes can ship as `/api/v2` without disturbing existing clients.

Responses are `application/json` unless noted (attachment downloads return the
attachment's own content type). All timestamps are UTC, ISO 8601
(`2026-06-11T09:30:00Z`).

### Pagination

List endpoints return a pagination envelope:

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` is 1-based. Out-of-range pages return an empty `items` array (not an
  error).
- `pageSize` defaults to `Api:DefaultPageSize` and is clamped to
  `[1, Api:MaxPageSize]`.

### Errors

Errors use [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
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

| Status | When |
| --- | --- |
| `400 Bad Request` | Malformed parameters (e.g. unparseable date, invalid `sortBy`), an unparsable message body, or a validation error. Validation failures carry an `errors` object keyed by field name. |
| `401 Unauthorized` | Missing, malformed, expired, revoked or otherwise invalid key. Includes `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | The call is understood but not permitted: attachment downloads disabled, write API disabled (`Api:AllowWrites=false`), a read-only key on a write endpoint, a destructive endpoint disabled by its `Api` switch, or a missing `Admin`/`SelfManager` role. |
| `404 Not Found` | Resource does not exist **or** is outside the caller's permitted accounts. Also returned for every `/api/*` route when `Api:Enabled=false`. |
| `409 Conflict` | The resource is in a state that forbids the call: duplicate account address, duplicate message on import, deletion blocked by `DeletionPolicy`, or a retention-locked message. |
| `413 Payload Too Large` | Import payload exceeds `Api:MaxImportBytes`. |
| `422 Unprocessable Entity` | The message parsed but could not be stored (importer failure); `detail` carries the reason. |
| `429 Too Many Requests` | Rate limit exceeded. |
| `500 Internal Server Error` | Unexpected server error (problem+json, never the HTML error page). |

### Rate limiting

Requests are limited by a fixed-window policy of `Api:RateLimitPerMinute`
requests per minute (default `120`), partitioned by API key prefix (falling
back to client IP when no key is present). Exceeding the budget returns `429`.

## Read endpoints

> All examples assume the API is enabled and `$KEY` holds a valid key.

### List accounts

```
GET /api/v1/accounts
```

Returns the mail accounts visible to the caller. Credential fields
(IMAP/OAuth username, password, client secret, …) are **never** included.

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

### Get account

```
GET /api/v1/accounts/{id}
```

Returns the full configuration of one account — retention windows, sync
intervals, excluded folders, IMAP host/port/username and OAuth ids. Secrets are
never returned; `hasPassword` and `hasClientSecret` tell you whether a secret is
stored. Returns `404` if the account does not exist or is not visible to the
caller.

Returns a [`MailAccountDetailDto`](#mailaccountdetaildto).

### List folders

```
GET /api/v1/accounts/{id}/folders
```

Returns the folder tree for one account as a nested structure. Returns `404` if
the account does not exist or is not visible to the caller.

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

### Search messages

```
GET /api/v1/emails
```

Searches archived messages with the same capabilities as the web UI.

| Query parameter | Type | Description |
| --- | --- | --- |
| `q` | string | Full-text query over subject, body, from, to, cc, bcc. Supports field syntax, e.g. `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Earliest sent date (inclusive), `YYYY-MM-DD`. |
| `to` | date | Latest sent date (inclusive), `YYYY-MM-DD`. |
| `accountId` | int | Restrict to one account (must be visible to the caller). |
| `folder` | string | Restrict to one folder by full path. |
| `direction` | string | `incoming` or `outgoing`. |
| `page` | int | 1-based page number (default `1`). |
| `pageSize` | int | Results per page (default `Api:DefaultPageSize`, clamped to `Api:MaxPageSize`). |
| `sortBy` | string | One of `sentDate` (default), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` or `desc` (default `desc`). |

Returns a [paged envelope](#pagination) of [`EmailSummaryDto`](#emailsummarydto).
Logs an [access log](#access-logging) entry of type `Search`.

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

### Get message

```
GET /api/v1/emails/{id}
```

Returns the full message including bodies. The body uses the same fallback
chain as the web UI: `OriginalBodyHtml` (raw bytes) → `BodyUntruncatedHtml` →
`HtmlBody`, and the equivalent for the text body. Returns `404` if the message
does not exist or belongs to an account outside the caller's permissions. Logs
an access log entry of type `Open`.

Returns an [`EmailDetailDto`](#emaildetaildto).

### Download attachment

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Streams the attachment bytes with its stored content type and filename. Returns
`404` if the message or attachment does not exist or is not visible to the
caller. Returns `403` (problem+json) when `Api:AllowAttachmentDownloads=false`.
Logs an access log entry of type `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Stats

```
GET /api/v1/stats
```

Returns aggregate archive statistics visible to the caller: total archived
emails, visible mail accounts, attachments on those emails, and the total
PostgreSQL database size in megabytes. For non-admin users the counts are
scoped to the mail accounts assigned to them (the same scoping the web
dashboard applies); administrators see global totals.

For non-admin API keys, `databaseSizeInMB` is an empty string: the database
size describes the whole instance, not the key's scope. Emails, Accounts and
Attachments are always scoped to the accounts the key may access.

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

### StatsDto

| Field | Type | Notes |
| --- | --- | --- |
| `emails` | int | Archived messages visible to the caller |
| `accounts` | int | Mail accounts visible to the caller |
| `attachments` | int | Attachments on those visible messages |
| `databaseSizeInMB` | string | Whole PostgreSQL database size in MB; empty for non-admin keys |

## Write endpoints

> Every write endpoint requires `Api:AllowWrites=true` **and** a key with the
> `write` scope. Without the scope the API answers `403` with
> `"This API key is read-only."`; with the master switch off it answers `403`
> with `"Write access is disabled."`. Deletions add the policy checks described
> below.

### Create account

```
POST /api/v1/accounts
```

Requires the `Admin` or `SelfManager` role. Creates a mail account with the same
fields the web UI offers. `provider` is `IMAP` (default), `M365` or `MSA`;
`IMPORT` accounts are created by the import pipeline and are rejected here.

```bash
curl -X POST -H "Authorization: Bearer $KEY" -H "Content-Type: application/json" \
  -d '{
        "name": "Support Mailbox",
        "emailAddress": "support@example.com",
        "provider": "IMAP",
        "imapServer": "imap.example.com",
        "imapPort": 993,
        "username": "support@example.com",
        "password": "app-password",
        "useSsl": true
      }' \
  https://host/api/v1/accounts
```

| Field | Type | Notes |
| --- | --- | --- |
| `name` | string | required, max 200 characters |
| `emailAddress` | string | required, must be unique (case-insensitive) |
| `provider` | string | `IMAP` (default), `M365`, `MSA` |
| `isEnabled` | bool | default `true` |
| `imapServer`, `imapPort`, `username`, `password`, `useSsl` | | required for `IMAP`; port defaults to `993`, `useSsl` to `true` |
| `clientId`, `clientSecret`, `tenantId` | string | required for `M365`; `clientId` required for `MSA` unless a default client id is configured |
| `excludedFolders` | string[] | folder paths excluded from sync |
| `deleteAfterDays` | int | server-side retention |
| `localRetentionDays` | int | requires `deleteAfterDays` and must be ≥ it |
| `syncIntervalMinutes`, `fullSyncIntervalHours` | int | per-account sync schedule |

Returns `201 Created` with a `Location` header and the
[`MailAccountDetailDto`](#mailaccountdetaildto). `400` carries an `errors`
object keyed by field name; `409` means an account with that address already
exists.

!!! note "No connection test"
    Unlike the web UI, the API does not open an IMAP connection while creating
    the account — a REST call should not block on a foreign server. A wrong
    password surfaces on the first sync and is reported in the sync log.

### Update account (PUT)

```
PUT /api/v1/accounts/{id}
```

Replaces the mutable configuration of an account. The provider and the e-mail
address are immutable. Omitted nullable settings are **cleared** — use this
endpoint when you want to remove a retention window. Secrets are write-only:
omit `password` / `clientSecret` to keep the stored value, send `""` to clear it.

Returns `200` with the updated [`MailAccountDetailDto`](#mailaccountdetaildto).

### Patch account (PATCH)

```
PATCH /api/v1/accounts/{id}
```

Partial update: only properties supplied with a non-null value are changed. Use
this endpoint to disable an account or change a single setting without restating
the whole configuration.

```bash
curl -X PATCH -H "Authorization: Bearer $KEY" -H "Content-Type: application/json" \
  -d '{ "isEnabled": false }' https://host/api/v1/accounts/1
```

!!! note
    Because `null` means "unchanged" here, PATCH cannot clear a nullable setting
    (`deleteAfterDays`, `localRetentionDays`, …). Send a PUT for that.

### Delete account

```
DELETE /api/v1/accounts/{id}
```

Deletes the account **and every message archived for it**. Requires
`Api:AllowAccountDeletion=true`; when `DeletionPolicy:DeletionAllowed=false` the
call is refused with `409`. The deletion runs as a background job (the same job
the web UI uses), so the response is immediate:

```json
{
  "jobId": "3f6c1b0e-9d2a-4a51-8f0e-6c1d2b7a9e44",
  "accountId": 1,
  "accountName": "Support Mailbox",
  "status": "Queued",
  "currentPhase": "Initializing",
  "created": "2026-10-28T14:02:11Z",
  "started": null,
  "completed": null,
  "deletedEmails": 0,
  "totalEmails": 0,
  "deletedAttachments": 0,
  "totalAttachments": 0,
  "errorMessage": null
}
```

Returns `202 Accepted` with `Location: /api/v1/accounts/deletions/{jobId}`.

### Account deletion status

```
GET /api/v1/accounts/deletions/{jobId}
```

Returns the [`AccountDeletionJobDto`](#accountdeletionjobdto) for a job started
through the API: `status` (`Queued`, `Running`, `Completed`, `Failed`,
`Cancelled`), `currentPhase`, `deletedEmails`/`totalEmails`,
`deletedAttachments`/`totalAttachments` and `errorMessage`. Returns `404` when
the job is unknown or belongs to an account outside the caller's permissions.

### Import message

```
POST /api/v1/emails
```

Stores a raw RFC 822 message in the archive of one account. The message is only
written to the archive — nothing is uploaded to the IMAP server.

```bash
# base64 (default)
curl -X POST -H "Authorization: Bearer $KEY" -H "Content-Type: application/json" \
  -d "{\"accountId\": 1, \"folder\": \"INBOX\", \"content\": \"$(base64 -w0 mail.eml)\"}" \
  https://host/api/v1/emails

# or plain text
curl -X POST -H "Authorization: Bearer $KEY" -H "Content-Type: application/json" \
  -d '{"accountId": 1, "content": "From: …", "encoding": "utf8"}' \
  https://host/api/v1/emails
```

| Field | Type | Notes |
| --- | --- | --- |
| `accountId` | int | required, must be visible to the caller |
| `folder` | string | archive folder, default `INBOX`, max 255 characters |
| `content` | string | required, the raw message |
| `encoding` | string | `base64` (default) or `utf8` |

Returns `201 Created` with `{ id, accountId, folder, messageId, subject }` and a
`Location` header pointing at the message detail endpoint.

- `400` — malformed JSON/base64 or a message that cannot be parsed.
- `409` — the same message is already archived; the body carries `existingId`.
- `413` — the decoded message exceeds `Api:MaxImportBytes`.

Attachments, body fallbacks, calendar parts and duplicate detection are handled
by the same importer the EML/MBox import uses.

### Move message

```
PATCH /api/v1/emails/{id}
```

```json
{ "folderName": "Archiv/2026" }
```

Moves the message to another folder **inside the archive**. Message content is
immutable (compliance), which is why the folder is the only mutable field — this
also works for locked messages, because the database trigger explicitly allows
`FolderName` changes. Returns `200` with the updated
[`EmailSummaryDto`](#emailsummarydto).

### Delete message

```
DELETE /api/v1/emails/{id}
```

Deletes one archived message. Requires `Api:AllowEmailDeletion=true` and
`DeletionPolicy:DeletionAllowed=true`. A message locked by the retention policy
cannot be deleted and returns `409`; it is unlocked only by retention expiry or
by deleting the whole account.

Returns `204 No Content`. The deletion is written to the access log
(`Deletion`).

### List keys

```
GET /api/v1/keys
```

Lists the caller's own keys (never another user's, even for an administrator):
`id`, `name`, `keyPrefix`, `scopes`, `createdAt`, `lastUsedAt`, `expiresAt`,
`revokedAt`, `isActive`. The key value itself is not recoverable.

### Create key

```
POST /api/v1/keys
```

```json
{ "name": "nightly export", "expiresAt": "2027-01-01T00:00:00Z", "scopes": ["read"] }
```

Creates a key for the caller. `scopes` defaults to `["read"]`;
`["read","write"]` creates a key that may call the write endpoints. Creating a
key requires a write-scoped key, so a read-only key cannot escalate itself.

Returns `201 Created` with the plaintext `key` — **the only time it is ever
returned**.

### Revoke key

```
DELETE /api/v1/keys/{id}
```

Revokes one of the caller's keys (administrators may revoke any key). Returns
`204 No Content`; an unknown or foreign key returns `404`.

## Data types

### MailAccountDto

| Field | Type | Notes |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

No credential fields are ever serialized.

### MailAccountDetailDto

All of [`MailAccountDto`](#mailaccountdto) plus the account configuration:

| Field | Type | Notes |
| --- | --- | --- |
| `lastFullSync` | datetime? | UTC |
| `imapServer`, `imapPort`, `useSsl`, `username` | | IMAP settings |
| `clientId`, `tenantId` | string | OAuth settings |
| `hasPassword` | bool | true when an IMAP password is stored (value never returned) |
| `hasClientSecret` | bool | true when an OAuth client secret is stored (value never returned) |
| `excludedFolders` | string[] | Folder paths excluded from sync |
| `deleteAfterDays` | int? | Server-side retention |
| `localRetentionDays` | int? | Local archive retention |
| `syncIntervalMinutes`, `fullSyncIntervalHours` | int? | Per-account schedule |

### Write request bodies

| Type | Used by | Fields |
| --- | --- | --- |
| `CreateMailAccountRequest` | `POST /api/v1/accounts` | `name`, `emailAddress`, `provider`, `isEnabled`, `imapServer`, `imapPort`, `username`, `password`, `useSsl`, `excludedFolders`, `deleteAfterDays`, `localRetentionDays`, `syncIntervalMinutes`, `fullSyncIntervalHours`, `clientId`, `clientSecret`, `tenantId` |
| `UpdateMailAccountRequest` | `PUT` / `PATCH /api/v1/accounts/{id}` | same as create, without `emailAddress` and `provider` |
| `ImportEmailRequest` | `POST /api/v1/emails` | `accountId`, `folder`, `content`, `encoding` |
| `MoveEmailRequest` | `PATCH /api/v1/emails/{id}` | `folderName` |
| `CreateApiKeyRequest` | `POST /api/v1/keys` | `name`, `expiresAt`, `scopes` |

### ImportEmailResultDto

| Field | Type | Notes |
| --- | --- | --- |
| `id` | int | Id of the archived message |
| `accountId` | int | |
| `folder` | string | Folder the message was stored in |
| `messageId` | string | Message-Id header (or the deterministic fallback id) |
| `subject` | string | |

### ApiKeyDto

| Field | Type | Notes |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `keyPrefix` | string | First characters of the key, not a secret |
| `scopes` | string[] | `["read"]` or `["read","write"]` |
| `createdAt`, `lastUsedAt`, `expiresAt`, `revokedAt` | datetime? | UTC |
| `isActive` | bool | Not revoked and not expired |

`CreatedApiKeyDto` adds `key` — the plaintext key, returned only by
`POST /api/v1/keys`.

### AccountDeletionJobDto

| Field | Type | Notes |
| --- | --- | --- |
| `jobId` | string | |
| `accountId` | int | |
| `accountName` | string | |
| `status` | string | `Queued`, `Running`, `Completed`, `Failed`, `Cancelled` |
| `currentPhase` | string | Human-readable phase of the deletion |
| `created`, `started`, `completed` | datetime? | UTC |
| `deletedEmails`, `totalEmails` | int | Progress counters |
| `deletedAttachments`, `totalAttachments` | int | Progress counters |
| `errorMessage` | string? | Set when `status` is `Failed` |

### FolderNodeDto

| Field | Type | Notes |
| --- | --- | --- |
| `name` | string | Leaf name |
| `fullPath` | string | Full path |
| `totalCount` | int | Messages in folder |
| `level` | int | Nesting depth (0 = root) |
| `children` | FolderNodeDto[] | Sub-folders |

### EmailSummaryDto

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

### EmailDetailDto

All of [`EmailSummaryDto`](#emailsummarydto) plus:

| Field | Type | Notes |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Resolved via the body fallback chain |
| `htmlBody` | string | Resolved via the body fallback chain |
| `attachments` | AttachmentDto[] | Metadata only |

### AttachmentDto

| Field | Type | Notes |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bytes |

Attachment bytes are obtained from the [download endpoint](#download-attachment),
not from this DTO.

## Access logging

Every data access through the API is recorded in the same access log used by the
web UI (visible under **Logs**), attributed to the key's owning user:

| Endpoint | Logged type |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Create / update / delete account | `Account` |
| Import message | `Account` |
| Move message | `Account` |
| Delete message | `Deletion` |
| Create / revoke API key | `Account` |

## OpenAPI and Swagger UI

When the API is enabled and `Api:EnableSwaggerUi=true`:

- The OpenAPI v1 document is served at **`/apidocs/spec/v1.json`**.
- Swagger UI is served at **`/apidocs`**.

Both paths sit outside `/api/` and are therefore gated by the normal web
(cookie) authentication — a logged-in browser session is required to view them,
and they cannot be reached with an API key.

### Endpoint reference on this site

The endpoints and data types are also published here as a generated page:
**[API Reference](API-Reference.md)**. It is rendered from the same OpenAPI
document — a copy is kept at `doc/assets/openapi-v1.json` — by
`tools/gen_api_reference.py`. CI fails when the page and the specification fall
out of sync, so the reference always describes the API that actually ships.

!!! note "Regenerating"
    The page is generated output. Change the API (or the generator), then run
    `python3 tools/gen_api_reference.py` and commit the result.

    Refreshing the vendored specification itself means re-running the build-time
    document generation described in `tools/README.md`.
