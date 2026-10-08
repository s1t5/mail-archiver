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
| `GET` | [`/api/v1/accounts/{id}/folders`](#apiv1accountsidfolders) |
| `GET` | [`/api/v1/emails`](#apiv1emails) |
| `GET` | [`/api/v1/emails/{id}`](#apiv1emailsid) |
| `GET` | [`/api/v1/emails/{id}/attachments/{attachmentId}`](#apiv1emailsidattachmentsattachmentid) |
| `GET` | [`/api/v1/stats`](#apiv1stats) |

### `GET` /api/v1/accounts { #apiv1accounts }

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [MailAccountDto](#mailaccountdto)[] |

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

### `GET` /api/v1/emails/{id} { #apiv1emailsid }

| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [EmailDetailDto](#emaildetaildto) |

### `GET` /api/v1/emails/{id}/attachments/{attachmentId} { #apiv1emailsidattachmentsattachmentid }

| Name | In | Type | Required | Default |
| ---- | -- | ---- | -------- | ------- |
| `id` | path | integer (int32) | yes | — |
| `attachmentId` | path | integer (int32) | yes | — |

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | — | no body |

### `GET` /api/v1/stats { #apiv1stats }

| Status | Content type | Schema |
| ------ | ------------ | ------ |
| 200 | `application/json` | [StatsDto](#statsdto) |

## Data types

### AttachmentDto { #attachmentdto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `contentType` | string | no |
| `fileName` | string | no |
| `id` | integer (int32) | no |
| `size` | integer (int64) | no |

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

### MailAccountDto { #mailaccountdto }

| Property | Type | Required |
| -------- | ---- | -------- |
| `emailAddress` | string | no |
| `id` | integer (int32) | no |
| `isEnabled` | boolean | no |
| `lastSync` | string (date-time) | no |
| `name` | string | no |
| `provider` | string | no |

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
