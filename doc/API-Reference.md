# API Reference

!!! warning "Generated page — do not edit"

    This page is rendered from the OpenAPI specification at
    `doc/assets/openapi-v1.json`. Edit the specification or the
    generator (`tools/gen_api_reference.py`), not this file.
    CI fails if the two drift apart.

Specification: `MailArchiver | v1`, version `1.0.0`

For authentication, pagination, error handling and rate limiting see
[the API guide](API.md). This page is the endpoint and data-type reference.

## Endpoints

| Method | Path |
| ------ | ---- |
| `GET` | [`/api/v1/accounts`](#apiv1accounts) |
| `POST` | [`/api/v1/accounts`](#apiv1accounts) |
| `GET` | [`/api/v1/accounts/deletions/{jobId}`](#apiv1accountsdeletionsjobid) |
| `GET` | [`/api/v1/accounts/{id}`](#apiv1accountsid) |
| `PUT` | [`/api/v1/accounts/{id}`](#apiv1accountsid) |
| `PATCH` | [`/api/v1/accounts/{id}`](#apiv1accountsid) |
| `DELETE` | [`/api/v1/accounts/{id}`](#apiv1accountsid) |
| `GET` | [`/api/v1/accounts/{id}/folders`](#apiv1accountsidfolders) |
| `GET` | [`/api/v1/emails`](#apiv1emails) |
| `POST` | [`/api/v1/emails`](#apiv1emails) |
| `GET` | [`/api/v1/emails/{id}`](#apiv1emailsid) |
| `PATCH` | [`/api/v1/emails/{id}`](#apiv1emailsid) |
| `DELETE` | [`/api/v1/emails/{id}`](#apiv1emailsid) |
| `GET` | [`/api/v1/emails/{id}/attachments/{attachmentId}`](#apiv1emailsidattachmentsattachmentid) |
| `GET` | [`/api/v1/keys`](#apiv1keys) |
| `POST` | [`/api/v1/keys`](#apiv1keys) |
| `DELETE` | [`/api/v1/keys/{id}`](#apiv1keysid) |
| `GET` | [`/api/v1/stats`](#apiv1stats) |

### `GET` /api/v1/accounts { #apiv1accounts }

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [MailAccountDto](#mailaccountdto)[] |

### `POST` /api/v1/accounts { #apiv1accounts }

Request body (required): `application/json` → [CreateMailAccountRequest](#createmailaccountrequest)
Request body (required): `application/*+json` → [CreateMailAccountRequest](#createmailaccountrequest)
| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [MailAccountDetailDto](#mailaccountdetaildto) |

### `GET` /api/v1/accounts/deletions/{jobId} { #apiv1accountsdeletionsjobid }

| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `jobId` | path | string | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [AccountDeletionJobDto](#accountdeletionjobdto) |

### `GET` /api/v1/accounts/{id} { #apiv1accountsid }

| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [MailAccountDetailDto](#mailaccountdetaildto) |

### `PUT` /api/v1/accounts/{id} { #apiv1accountsid }

Request body (required): `application/json` → [UpdateMailAccountRequest](#updatemailaccountrequest)
Request body (required): `application/*+json` → [UpdateMailAccountRequest](#updatemailaccountrequest)
| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [MailAccountDetailDto](#mailaccountdetaildto) |

### `PATCH` /api/v1/accounts/{id} { #apiv1accountsid }

Request body (required): `application/json` → [UpdateMailAccountRequest](#updatemailaccountrequest)
Request body (required): `application/*+json` → [UpdateMailAccountRequest](#updatemailaccountrequest)
| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [MailAccountDetailDto](#mailaccountdetaildto) |

### `DELETE` /api/v1/accounts/{id} { #apiv1accountsid }

| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [AccountDeletionJobDto](#accountdeletionjobdto) |

### `GET` /api/v1/accounts/{id}/folders { #apiv1accountsidfolders }

| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [FolderNodeDto](#foldernodedto)[] |

### `GET` /api/v1/emails { #apiv1emails }

| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `q` | query | string | no | — |
| `from` | query | string (date-time) | no | — |
| `to` | query | string (date-time) | no | — |
| `accountId` | query | integer (int32) | no | — |
| `folder` | query | string | no | — |
| `direction` | query | string | no | — |
| `page` | query | integer (int32) | no | 1 |
| `pageSize` | query | integer (int32) | no | 0 |
| `sortBy` | query | string | no | — |
| `sortOrder` | query | string | no | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [PagedResultDtoOfEmailSummaryDto](#pagedresultdtoofemailsummarydto) |

### `POST` /api/v1/emails { #apiv1emails }

Request body (required): `application/json` → [ImportEmailRequest](#importemailrequest)
Request body (required): `application/*+json` → [ImportEmailRequest](#importemailrequest)
| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [ImportEmailResultDto](#importemailresultdto) |

### `GET` /api/v1/emails/{id} { #apiv1emailsid }

| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [EmailDetailDto](#emaildetaildto) |

### `PATCH` /api/v1/emails/{id} { #apiv1emailsid }

Request body (required): `application/json` → [MoveEmailRequest](#moveemailrequest)
Request body (required): `application/*+json` → [MoveEmailRequest](#moveemailrequest)
| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [EmailSummaryDto](#emailsummarydto) |

### `DELETE` /api/v1/emails/{id} { #apiv1emailsid }

| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | — | no body |

### `GET` /api/v1/emails/{id}/attachments/{attachmentId} { #apiv1emailsidattachmentsattachmentid }

| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |
| `attachmentId` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | — | no body |

### `GET` /api/v1/keys { #apiv1keys }

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [ApiKeyDto](#apikeydto)[] |

### `POST` /api/v1/keys { #apiv1keys }

Request body (required): `application/json` → [CreateApiKeyRequest](#createapikeyrequest)
Request body (required): `application/*+json` → [CreateApiKeyRequest](#createapikeyrequest)
| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [CreatedApiKeyDto](#createdapikeydto) |

### `DELETE` /api/v1/keys/{id} { #apiv1keysid }

| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | — | no body |

### `GET` /api/v1/stats { #apiv1stats }

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [StatsDto](#statsdto) |

## Data types

### AccountDeletionJobDto { #accountdeletionjobdto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `accountId` | integer (int32) | no |
| `accountName` | string | no |
| `completed` | null (date-time) | no |
| `created` | string (date-time) | no |
| `currentPhase` | string | no |
| `deletedAttachments` | integer (int32) | no |
| `deletedEmails` | integer (int32) | no |
| `errorMessage` | null | no |
| `jobId` | string | no |
| `started` | null (date-time) | no |
| `status` | string | no |
| `totalAttachments` | integer (int32) | no |
| `totalEmails` | integer (int32) | no |

### ApiKeyDto { #apikeydto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `createdAt` | string (date-time) | no |
| `expiresAt` | null (date-time) | no |
| `id` | integer (int32) | no |
| `isActive` | boolean | no |
| `keyPrefix` | string | no |
| `lastUsedAt` | null (date-time) | no |
| `name` | string | no |
| `ownerUsername` | null | no |
| `revokedAt` | null (date-time) | no |
| `scopes` | string[] | no |

### AttachmentDto { #attachmentdto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `contentType` | string | no |
| `fileName` | string | no |
| `id` | integer (int32) | no |
| `size` | integer (int64) | no |

### CreateApiKeyRequest { #createapikeyrequest }

| Property | Type | Required |
| -------- | ---- | -------- |
| `expiresAt` | null (date-time) | no |
| `name` | null | no |
| `scopes` | null | no |

### CreateMailAccountRequest { #createmailaccountrequest }

| Property | Type | Required |
| -------- | ---- | -------- |
| `clientId` | null | no |
| `clientSecret` | null | no |
| `deleteAfterDays` | null (int32) | no |
| `emailAddress` | null | no |
| `excludedFolders` | null | no |
| `fullSyncIntervalHours` | null (int32) | no |
| `imapPort` | null (int32) | no |
| `imapServer` | null | no |
| `isEnabled` | null | no |
| `localRetentionDays` | null (int32) | no |
| `name` | null | no |
| `password` | null | no |
| `provider` | null | no |
| `syncIntervalMinutes` | null (int32) | no |
| `tenantId` | null | no |
| `useSsl` | null | no |
| `username` | null | no |

### CreatedApiKeyDto { #createdapikeydto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `createdAt` | string (date-time) | no |
| `expiresAt` | null (date-time) | no |
| `id` | integer (int32) | no |
| `isActive` | boolean | no |
| `key` | string | no |
| `keyPrefix` | string | no |
| `lastUsedAt` | null (date-time) | no |
| `name` | string | no |
| `ownerUsername` | null | no |
| `revokedAt` | null (date-time) | no |
| `scopes` | string[] | no |

### EmailDetailDto { #emaildetaildto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `accountId` | integer (int32) | no |
| `attachments` | [AttachmentDto](#attachmentdto)[] | no |
| `bcc` | string | no |
| `cc` | string | no |
| `folderName` | string | no |
| `from` | string | no |
| `hasAttachments` | boolean | no |
| `htmlBody` | null | no |
| `id` | integer (int32) | no |
| `isOutgoing` | boolean | no |
| `messageId` | string | no |
| `receivedDate` | string (date-time) | no |
| `sentDate` | string (date-time) | no |
| `subject` | string | no |
| `textBody` | null | no |
| `to` | string | no |

### EmailSummaryDto { #emailsummarydto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `accountId` | integer (int32) | no |
| `folderName` | string | no |
| `from` | string | no |
| `hasAttachments` | boolean | no |
| `id` | integer (int32) | no |
| `isOutgoing` | boolean | no |
| `sentDate` | string (date-time) | no |
| `subject` | string | no |
| `to` | string | no |

### FolderNodeDto { #foldernodedto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `children` | [FolderNodeDto](#foldernodedto)[] | no |
| `fullPath` | string | no |
| `level` | integer (int32) | no |
| `name` | string | no |
| `totalCount` | integer (int32) | no |

### ImportEmailRequest { #importemailrequest }

| Property | Type | Required |
| -------- | ---- | -------- |
| `accountId` | integer (int32) | no |
| `content` | null | no |
| `encoding` | null | no |
| `folder` | null | no |

### ImportEmailResultDto { #importemailresultdto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `accountId` | integer (int32) | no |
| `folder` | string | no |
| `id` | integer (int32) | no |
| `messageId` | string | no |
| `subject` | string | no |

### MailAccountDetailDto { #mailaccountdetaildto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `clientId` | null | no |
| `deleteAfterDays` | null (int32) | no |
| `emailAddress` | string | no |
| `excludedFolders` | string[] | no |
| `fullSyncIntervalHours` | null (int32) | no |
| `hasClientSecret` | boolean | no |
| `hasPassword` | boolean | no |
| `id` | integer (int32) | no |
| `imapPort` | null (int32) | no |
| `imapServer` | null | no |
| `isEnabled` | boolean | no |
| `lastFullSync` | null (date-time) | no |
| `lastSync` | string (date-time) | no |
| `localRetentionDays` | null (int32) | no |
| `name` | string | no |
| `provider` | string | no |
| `syncIntervalMinutes` | null (int32) | no |
| `tenantId` | null | no |
| `useSsl` | boolean | no |
| `username` | null | no |

### MailAccountDto { #mailaccountdto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `emailAddress` | string | no |
| `id` | integer (int32) | no |
| `isEnabled` | boolean | no |
| `lastSync` | string (date-time) | no |
| `name` | string | no |
| `provider` | string | no |

### MoveEmailRequest { #moveemailrequest }

| Property | Type | Required |
| -------- | ---- | -------- |
| `folderName` | null | no |

### PagedResultDtoOfEmailSummaryDto { #pagedresultdtoofemailsummarydto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `items` | [EmailSummaryDto](#emailsummarydto)[] | no |
| `page` | integer (int32) | no |
| `pageSize` | integer (int32) | no |
| `totalItems` | integer (int32) | no |
| `totalPages` | integer (int32) | no |

### StatsDto { #statsdto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `accounts` | integer (int32) | no |
| `attachments` | integer (int32) | no |
| `databaseSizeInMB` | string | no |
| `emails` | integer (int32) | no |

### UpdateMailAccountRequest { #updatemailaccountrequest }

| Property | Type | Required |
| -------- | ---- | -------- |
| `clientId` | null | no |
| `clientSecret` | null | no |
| `deleteAfterDays` | null (int32) | no |
| `excludedFolders` | null | no |
| `fullSyncIntervalHours` | null (int32) | no |
| `imapPort` | null (int32) | no |
| `imapServer` | null | no |
| `isEnabled` | null | no |
| `localRetentionDays` | null (int32) | no |
| `name` | null | no |
| `password` | null | no |
| `syncIntervalMinutes` | null (int32) | no |
| `tenantId` | null | no |
| `useSsl` | null | no |
| `username` | null | no |
