# Read-Only REST API (v1)

Mail Archiver ships an optional, **read-only** REST API for programmatic access
to archived mail. It is designed for automation and integration use cases
(reporting, e-discovery exports, monitoring) without going through the web UI.

The point is to let a script or AI agent read archived mail **without ever
exposing mailbox credentials or talking to the mail provider.** A scoped
per-user API key is a capability to *read the archive*, not a credential to *be
the mailbox* — it inherits only its owner's mailbox permissions, grants no write
path, and can be revoked instantly without touching the underlying mail account.

> ⚠️ **The API is disabled by default.** It must be explicitly enabled in
> configuration (see [Enabling the API](#enabling-the-api)). When disabled,
> every `/api/*` route returns `404 Not Found`.

The API is **read-only by design**: it exposes accounts, folders, message
search, message detail and attachment downloads. It never creates, modifies or
deletes data.

## Table of contents

- [Enabling the API](#enabling-the-api)
- [Authentication](#authentication)
  - [API key format](#api-key-format)
  - [Key lifecycle](#key-lifecycle)
  - [Permission model](#permission-model)
  - [Security rationale](#security-rationale)
- [Conventions](#conventions)
  - [Base URL and versioning](#base-url-and-versioning)
  - [Pagination](#pagination)
  - [Errors](#errors)
  - [Rate limiting](#rate-limiting)
- [Endpoints](#endpoints)
  - [List accounts](#list-accounts)
  - [List folders](#list-folders)
  - [Search messages](#search-messages)
  - [Get message](#get-message)
  - [Download attachment](#download-attachment)
- [Data types](#data-types)
- [Access logging](#access-logging)
- [OpenAPI and Swagger UI](#openapi-and-swagger-ui)

## Enabling the API

The API is configured under the `Api` section of `appsettings.json` (or the
equivalent environment variables, e.g. `Api__Enabled=true`):

```json
"Api": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "EnableSwaggerUi": true,
  "DefaultPageSize": 20,
  "MaxPageSize": 100,
  "RateLimitPerMinute": 120
}
```

| Setting | Default | Description |
| --- | --- | --- |
| `Enabled` | `false` | Master switch. When `false`, all `/api/*` routes return `404` and the OpenAPI document / Swagger UI are not mapped. |
| `AllowAttachmentDownloads` | `true` | When `false`, the attachment download endpoint returns `403`. Metadata about attachments is still returned by the message detail endpoint. |
| `EnableSwaggerUi` | `true` | When `true` (and `Enabled` is `true`), mounts Swagger UI at `/apidocs` and the OpenAPI document at `/apidocs/spec/v1.json`. Both require an authenticated **web (cookie) session** — they are not part of `/api/` and are not reachable with an API key. |
| `DefaultPageSize` | `20` | Page size used when a request omits `pageSize`. |
| `MaxPageSize` | `100` | Upper bound; larger `pageSize` values are clamped to this. |
| `RateLimitPerMinute` | `120` | Fixed-window request budget per API key per minute (see [Rate limiting](#rate-limiting)). |

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
manages their own keys; administrators can view and revoke any user's keys).

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

#### Why SHA-256 and not PBKDF2/bcrypt?

This is a deliberate deviation from the password-hashing scheme used for user
login passwords, and the reasoning is documented here so it is not mistaken for
an oversight:

- Password hashes use PBKDF2 because human-chosen passwords have **low
  entropy** and need key-stretching to resist brute force. An API key carries a
  full **256 bits of cryptographic entropy** — it is computationally infeasible
  to brute-force regardless of hash speed, so stretching adds no security.
- Stretching would actively hurt: PBKDF2 at a realistic work factor costs on the
  order of ~100 ms of CPU **per request**, adding latency and creating a trivial
  CPU-exhaustion (DoS) vector on a public endpoint.
- Per-row salts (required by PBKDF2) make the stored value non-deterministic,
  which defeats an indexed `WHERE "KeyHash" = @hash` lookup — you would have to
  load and verify every candidate row.

SHA-256 gives a deterministic, indexable digest enabling an O(1) lookup, which
matches industry practice for high-entropy tokens (GitHub, Stripe). Submitted
keys are compared using `CryptographicOperations.FixedTimeEquals` to avoid
timing side channels.

### Key lifecycle

1. **Create** — a user creates a key with a descriptive `Name` and an optional
   expiry. The response shows the plaintext key once.
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

### Security rationale

- **2FA bypass.** API keys bypass two-factor authentication by design — this is
  inherent to non-interactive tokens (the same is true of personal access tokens
  on GitHub/GitLab). It is mitigated by: keys being **off by default**,
  revocable, optionally expiring, scoped to the user's permissions, and every
  access being recorded in the [access log](#access-logging).
- **No cookies on `/api`.** Cookie authentication is not accepted on `/api`
  routes, eliminating CSRF concerns for the API.
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
| `400 Bad Request` | Malformed parameters (e.g. unparseable date, invalid `sortBy`). |
| `401 Unauthorized` | Missing, malformed, expired, revoked or otherwise invalid key. Includes `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Attachment downloads requested while `AllowAttachmentDownloads=false`. |
| `404 Not Found` | Resource does not exist **or** is outside the caller's permitted accounts. Also returned for every `/api/*` route when `Api:Enabled=false`. |
| `429 Too Many Requests` | Rate limit exceeded. |
| `500 Internal Server Error` | Unexpected server error (problem+json, never the HTML error page). |

### Rate limiting

Requests are limited by a fixed-window policy of `Api:RateLimitPerMinute`
requests per minute (default `120`), partitioned by API key prefix (falling
back to client IP when no key is present). Exceeding the budget returns `429`.

## Endpoints

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

## OpenAPI and Swagger UI

When the API is enabled and `Api:EnableSwaggerUi=true`:

- The OpenAPI v1 document is served at **`/apidocs/spec/v1.json`**.
- Swagger UI is served at **`/apidocs`**.

Both paths sit outside `/api/` and are therefore gated by the normal web
(cookie) authentication — a logged-in browser session is required to view them,
and they cannot be reached with an API key.
