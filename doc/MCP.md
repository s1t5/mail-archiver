# MCP Server (Model Context Protocol)

Mail Archiver ships an optional **MCP (Model Context Protocol) server** that
exposes the archived mail to AI agents over the Streamable HTTP transport. It
mirrors the read-only [REST API](API.md): same data, same per-user permission
model, same access logging — just packaged as MCP tools instead of REST
endpoints, so an LLM agent can discover and call them directly.

The point is to let an AI agent read archived mail **without ever exposing
mailbox credentials or talking to the mail provider.** A scoped per-user API key
is a capability to *read the archive*, not a credential to *be the mailbox* — it
inherits only its owner's mailbox permissions, grants no write path, and can be
revoked instantly without touching the underlying mail account.

> ⚠️ **The MCP server is disabled by default.** It must be explicitly enabled in
> configuration (see [Enabling the MCP server](#enabling-the-mcp-server)). When
> disabled, the `/mcp` endpoint returns `404 Not Found`.

The MCP server is **read-only by design**: it exposes tools to list accounts,
list folders, search messages, read a message and download an attachment. It
never creates, modifies or deletes data.

## Table of contents

- [Enabling the MCP server](#enabling-the-mcp-server)
- [Authentication](#authentication)
- [Transport and endpoint](#transport-and-endpoint)
- [Tools](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
- [Rate limiting](#rate-limiting)
- [Access logging](#access-logging)
- [Connecting an agent](#connecting-an-agent)
- [Security guidance for internet exposure](#security-guidance-for-internet-exposure)

## Enabling the MCP server

The MCP server is configured under the `Mcp` section of `appsettings.json` (or
the equivalent environment variables, e.g. `Mcp__Enabled=true`):

```json
"Mcp": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "DefaultPageSize": 20,
  "MaxResults": 100,
  "MaxAttachmentBytes": 10000000,
  "RateLimitPerMinute": 120
}
```

| Setting | Default | Description |
| --- | --- | --- |
| `Enabled` | `false` | Master switch. When `false`, the `/mcp` endpoint returns `404`. |
| `AllowAttachmentDownloads` | `true` | When `false`, `get_attachment` returns an error instead of attachment bytes. Metadata about attachments is still returned by `get_email`. |
| `DefaultPageSize` | `20` | Page size used by `search_emails` when the caller omits `pageSize`. |
| `MaxResults` | `100` | Upper bound for the `search_emails` page size; larger values are clamped. |
| `MaxAttachmentBytes` | `10000000` | Maximum attachment size (in bytes) `get_attachment` will return inline as base64; larger attachments are refused. |
| `RateLimitPerMinute` | `120` | Fixed-window request budget per API key per minute (see [Rate limiting](#rate-limiting)). |

## Authentication

The MCP server authenticates with the **same per-user API keys as the REST
API**, sent as a bearer token:

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

API keys are created and managed in the web UI under **API Keys**. A key
inherits the exact permissions of the user who owns it (admin sees all
accounts; a restricted user sees only their assigned accounts). Keys can be
revoked or expired independently of the underlying mail accounts. See the
[REST API authentication section](API.md#authentication) for the full key
lifecycle and permission model — MCP keys are not a separate credential store.

When the MCP server is disabled, or no/invalid bearer token is presented, the
endpoint returns `404` / `401` respectively (it never redirects to the login
page).

## Transport and endpoint

The MCP server uses the **Streamable HTTP** transport and is mounted at:

```
POST /mcp
```

It runs in **stateless** mode (one JSON-RPC request per HTTP request), which is
the recommended mode for servers that do not need server-to-client requests
like sampling or elicitation. Tools are discovered via the standard MCP
`tools/list` request and called via `tools/call`.

## Tools

### list_accounts

Lists the mail accounts the current API key is allowed to access. Returns
`id`, `name`, `emailAddress`, `provider`, `isEnabled` and `lastSync`.

**Parameters:** none.

**Result:** array of account objects. Use the returned `id` with `list_folders`
and `search_emails`.

### list_folders

Lists the folder tree of a single mail account, with per-folder email counts.

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `accountId` | integer | yes | The mail account id (from `list_accounts`). |

**Result:** array of folder objects, each with `name`, `fullPath`, `totalCount`,
`level` and recursive `children`. Returns an empty list if the account does not
exist or the key is not authorized for it.

### search_emails

Searches archived emails by full-text query, date range, account, folder and
direction.

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `q` | string | no | Full-text search query (subject, body, from/to). |
| `from` | string (ISO-8601) | no | Sent-on-or-after UTC date/time filter. |
| `to` | string (ISO-8601) | no | Sent-on-or-before UTC date/time filter. |
| `accountId` | integer | no | Restrict to a single mail account id. |
| `folder` | string | no | Restrict to a single folder full path (e.g. `INBOX`). |
| `direction` | string | no | `incoming` or `outgoing`. |
| `page` | integer | no | 1-based page number (default `1`). |
| `pageSize` | integer | no | Page size; clamped to `[1, MaxResults]`. `0` uses `DefaultPageSize`. |
| `sortBy` | string | no | `sentdate` (default), `receiveddate`, `subject`, `from`, `to`. |
| `sortOrder` | string | no | `asc` or `desc` (default `desc`). |

**Result:** a paged object with `items` (array of email summaries), `page`,
`pageSize`, `totalItems`, `totalPages`. Each summary carries `id`, `accountId`,
`subject`, `from`, `to`, `sentDate`, `isOutgoing`, `hasAttachments` and
`folderName`. Pass a returned `id` to `get_email` for the full body and
attachment list.

### get_email

Fetches the full content of a single archived email.

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | The archived email id (from `search_emails`). |

**Result:** a single email object with all summary fields plus `cc`, `bcc`,
`receivedDate`, `messageId`, `textBody`, `htmlBody` and `attachments` (array of
`{ id, fileName, contentType, size }`). Returns `null` if the email does not
exist or the key is not authorized for its account.

### get_attachment

Downloads a single email attachment as a base64 blob.

**Parameters:**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | The archived email id the attachment belongs to. |
| `attachmentId` | integer | yes | The attachment id (from `get_email`'s `attachments`). |

**Result:** an object with `id`, `emailId`, `fileName`, `contentType`, `size`
and `base64Data` (the raw attachment bytes, base64-encoded). The tool returns an
error when `Mcp:AllowAttachmentDownloads=false`, when the attachment exceeds
`Mcp:MaxAttachmentBytes`, or when the key is not authorized for the email's
account.

## Rate limiting

The MCP server has its own fixed-window rate-limit policy (`"Mcp"`), partitioned
by the non-secret API-key prefix (first 11 characters of the `ma_...` token),
falling back to the client IP when no bearer token is present. The budget is
configured via `Mcp:RateLimitPerMinute` (default `120` per minute). When
exceeded, the endpoint returns `429 Too Many Requests` with a `Retry-After`
header. This is independent of the REST API's rate-limit policy.

## Access logging

Every tool call is logged through the same `IAccessLogService` as the REST API,
with the owning username and the matching access type (`Search`, `Open`,
`Download`). MCP activity is therefore visible on the **Logs** page alongside
REST API and web-UI activity, for a single audit trail.

## Connecting an agent

The MCP server speaks the standard MCP Streamable HTTP transport, so any MCP
client that supports HTTP can connect. The URL is the application base URL plus
`/mcp`, e.g. `http://localhost:5179/mcp`, and the API key is supplied via the
`Authorization: Bearer ma_...` header.

For agents configured via the standard MCP client JSON, an entry looks like:

```json
{
  "mcpServers": {
    "mail-archiver": {
      "url": "https://archive.example.com/mcp",
      "headers": {
        "Authorization": "Bearer ma_<your-api-key>"
      }
    }
  }
}
```

## Security guidance for internet exposure

The MCP server is designed so that, with the defaults and the hardening below
in place, archived mail cannot leave the system without a valid per-user API
key that has been explicitly issued to an account. The points below are
**mandatory reading before exposing `/mcp` to the internet**.

### Mandatory (block exploitation / data exfiltration)

1. **HTTPS only.** Terminate TLS at the edge (reverse proxy). A bearer API key over 
   plain HTTP can be sniffed and reused.
2. **Keep `Mcp:Enabled=false` unless you actually use it.** When disabled,
   `/mcp` returns `404` and no code path is reachable; this is the safe default
   and the safe state for deployments that do not need AI-agent access.
3. **Rate limiting is enforced on the endpoint.** The `"Mcp"` policy
   (`Mcp:RateLimitPerMinute`, default `120`) is applied on the mapped `/mcp` endpoint.
   It is partitioned by the non-secret API-key prefix (first 11 chars of the `ma_...` 
   token) or the client IP when no bearer token is present. **Tune `RateLimitPerMinute`
   down** for internet exposure (e.g. `30`–`60`) — MCP tool calls are heavier
   than REST calls and the endpoint should not be left at a high budget when
   untrusted clients can reach it.
4. **Reverse-proxy `X-Forwarded-For` must be trusted carefully.** The rate
   limiter falls back to `RemoteIpAddress`, which under a proxy is the proxy IP
   unless `UseForwardedHeaders` is fed the real client IP. Ensure your proxy sets 
   `X-Forwarded-For` correctly and that    only your proxy can set it; otherwise an 
   attacker can spoof the rate-limit partition.
5. **Do not place `/mcp` behind a CDN cache.** MCP responses are per-request
   JSON-RPC and must never be cached — caching would both break the protocol
   and leak one user's result to another. Mark `/mcp` as uncacheable at the
   edge (`Cache-Control: no-store`).

### API-key hygiene (block unauthorized access)

6. **Issue dedicated, short-lived keys for MCP agents.** A single `ma_...` key
   is valid for both `/api` and `/mcp`; there is no per-protocol scope today.
   Therefore:
   - Create a **separate key per agent** (do not reuse a reporting-script key
     for an external LLM agent).
   - Always set a short `ExpiresAt` and rotate it; revoke immediately when the
     agent is decommissioned.
   - Prefer keys owned by a **restricted (non-admin) user** whose only assigned
     accounts are the ones the agent needs. An admin key grants visibility into
     *all* mail accounts; a restricted key limits blast radius.
7. **Treat the API key like a password.** It is a bearer credential. Never
   embed it in client-side code, never log it, never commit it to a repo. Put
   it in the MCP client's secret store and rotate on suspected compromise.
8. **Audit on the Logs page.** Every tool call is recorded in `AccessLogs`
   (`Search`, `Open`, `Download`) under the owning user. Periodically review
   MCP activity for unexpected patterns (volume spikes, off-hours reads,
   mass downloads).

### Trust model for returned data (block downstream harm)

9. **Email bodies and attachments are untrusted content**, exactly as they
   were when they arrived in the mailbox. They may contain malware, phishing
   links, or active HTML/JavaScript.
   - `get_email` returns `htmlBody` **raw and unsanitized**. Do **not** render
     it to a human without sanitizing (DOMPurify / `HtmlSanitizer` / a sandbox).
     Rendering raw archived HTML is a stored-XSS vector.
   - `get_attachment` returns the raw bytes with the original `contentType`.
     Do **not** auto-open or auto-execute attachments; treat the
     `contentType` field as untrusted metadata, not as a safe instruction.
10. **Do not let the agent take write actions based on mail content without
    confirmation.** A malicious email can contain prompt-injection text aimed
    at the consuming LLM. Enforce permissions and confirmation in the agent's
    own logic — the system prompt is not a security boundary.

### Defense-in-depth recommendations

11. **Restrict `/mcp` at the network layer** when feasible (e.g. allow only the
    agent host's IP / VPN range at the reverse proxy), in addition to API-key
    auth. This is the single most effective control for internet exposure.
12. **Lower `Mcp:AllowAttachmentDownloads` to `false`** if your agents do not
    need attachment bytes — attachment exfiltration is the highest-volume data
    path. Metadata (file name, content type, size) is still returned via
    `get_email`.
13. **Lower `Mcp:MaxAttachmentBytes`** to the smallest value your agents
    actually need (default `10000000` ≈ 10 MB). This bounds the largest single
    response an API key can pull per request.
14. **Lower `Mcp:MaxResults` and `Mcp:DefaultPageSize`** to bound the largest
    search result an API key can pull per request (default `100` / `20`).
15. **Monitor `AccessLogs` and rate-limit rejections** (`429`) operationally.
    A sudden spike from a single key prefix is a signal to revoke that key.