# Account Import

[← Back to Documentation Index](Index.md)

## Overview

Mail Archiver supports importing multiple email accounts at once so that onboarding a large number of mailboxes does not require creating each account individually.

Two bulk import paths exist, depending on the mail provider:

| Provider | Bulk Import Method | Documentation |
|---|---|---|
| **Microsoft 365 (tenant)** | Tenant discovery via Microsoft Graph — one app registration accesses every mailbox in the tenant | [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) |
| **IMAP** | CSV bulk import — upload a CSV file with one row per mailbox | This page (see below) |

> **Note:** Microsoft personal accounts (Outlook.com / M365 Family, provider **MSA**) use an interactive device-code authorization flow and cannot be bulk-imported. They must be created and authorized one at a time. See [MSA Outlook Setup](MSA_Outlook_Setup.md).

---

## IMAP CSV Bulk Import

### What it does

The CSV bulk import lets an administrator create hundreds of IMAP mail accounts in a single operation by uploading a CSV file. Each row in the file corresponds to one mailbox. Common server settings (IMAP host, port, SSL) can be specified once in the upload form and apply to every row, with optional per-row overrides.

The import is **admin-only**. It is accessible from the **Mail Accounts** page via the **Import** dropdown → **CSV Bulk Import**.

### CSV File Format

The file must be a UTF-8 encoded CSV with a **header row** and **comma (`,`) as the delimiter**. Fields containing commas or quotes must be wrapped in double quotes (`"..."`).

#### Columns

| Column | Required | Description |
|---|---|---|
| `email` | Yes | Mailbox address. Also used as the default login username when `username` is not provided. |
| `password` | Yes | IMAP login password. Stored as provided — leading/trailing spaces are preserved. |
| `name` | No | Display name for the account. If omitted, defaults to `{prefix} - <{email}>`. |
| `username` | No | Login username if it differs from the email address. |
| `imap_server` | No | Per-row IMAP host. Overrides the common server from the upload form. |
| `imap_port` | No | Per-row IMAP port (1–65535). Overrides the common port. |
| `use_ssl` | No | Per-row SSL toggle (`true` or `false`). Overrides the common setting. |

#### Example

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- Row 1 uses the common server/port/SSL from the upload form.
- Row 2 sets a custom username but otherwise uses the common settings.
- Row 3 overrides server, port and SSL.

A downloadable example file is available on the import page via the **Download example CSV** button.

### Upload Form

| Field | Purpose |
|---|---|
| **CSV file** | The file to upload. |
| **IMAP server** | Common host for all rows without an `imap_server` override. |
| **IMAP port** | Common port (default 993). |
| **Use SSL** | Common SSL toggle (default on). |
| **Name prefix** | Prefix for auto-generated account names. Default: `IMAP`. |
| **Skip existing mailboxes** | When enabled, rows whose email already exists as an IMAP account are skipped (counted as "skipped", not as errors). When disabled, they are treated as failures. |
| **Account enabled** | Whether the created accounts should be enabled for background sync. |
| **Delete After Days / Local Retention Days** | Optional retention settings applied to every created account. See [Retention Policies](RetentionPolicies.md). |

### Processing

1. The CSV file is parsed and each row is validated (email format, password present, port range, SSL value).
2. Duplicate emails within the file are removed (first occurrence wins).
3. Existing IMAP accounts with the same email address are detected; depending on the **Skip existing** toggle they are either skipped or reported as failures.
4. All new accounts are inserted in a single database batch.
5. An access log entry is written summarising the run.

No IMAP connection test is performed during import — accounts are saved immediately. Authentication issues will only surface during the first background sync. This keeps the import fast even for hundreds of mailboxes.

### Result Page

After the import completes, a result page shows three tables:

- **Created** — email and name of every account that was created.
- **Skipped** — emails that were duplicates (in-file or already existing) when **Skip existing** is enabled.
- **Failed** — rows that could not be processed, with the line number and the reason (missing email, missing password, invalid port, no IMAP server, etc.).

A summary banner shows the counts: *{created} created, {skipped} skipped, {failed} failed*.

### Limits

The following limits can be configured via `appsettings.json` / environment variables (see [Setup Guide](Setup.md) → *CSV Import Settings*):

| Setting | Default | Description |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | Maximum rows processed per upload. |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | Maximum uploaded file size. |

### Security Note

IMAP passwords are stored in the database in the same way as for individually created IMAP accounts. Be aware that the CSV file contains all mailbox passwords in plaintext — delete it from the client machine after a successful import.

---

## Microsoft 365 Tenant Import

For Microsoft 365 (Exchange Online) the bulk import does not require a CSV file. A single Azure AD app registration with the right Graph permissions can access every mailbox in the tenant, so Mail Archiver can discover the mailboxes automatically.

See the dedicated guide: **[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**.