# 📥 Importing Mail in the Web UI (EML / MBox)

[← Back to Documentation Index](index.md)

## 📋 Overview

Besides the [CLI local import](CLI-Local-Import.md) and the
[CSV account import](Account-Import.md), Mail Archiver can import an existing
mail archive **through the web interface**. There are two upload forms:

| Form | Accepted file | Result |
|------|---------------|--------|
| **Import MBox** | `.mbox`, `.mbx` | Every message in the file is imported into **one** folder you choose |
| **Import EML** | `.zip` containing `.eml` files | The **folder structure inside the ZIP** decides where each message lands |

Both forms archive the messages into an existing Mail Archiver account, both run
as **background jobs**, and both **skip duplicates automatically** — importing
the same file twice does not create a second copy.

> 💡 **Tip**: If your archive is already on the server as a file, prefer the
> [CLI local import](CLI-Local-Import.md). It avoids the browser upload and is the
> better choice for multi-gigabyte archives.

## 🧭 Access

1. Open **Mail Accounts** in the main menu.
2. Use the **Import** dropdown above the account list and pick
   **Import MBox** or **Import EML**.

The forms are available to **admins** (all accounts) and **self managers**
(the accounts assigned to them); see
[User Management](UserManagement.md#-user-account-permissions). Only **enabled**
accounts are offered as import targets — a disabled account cannot be selected.

## 📨 Import MBox

MBox is the format used by most mail clients for exported folders, and by
`mbox`-style backups.

| Field | Meaning |
|-------|---------|
| **MBox File** | The `.mbox` / `.mbx` file to upload |
| **Target Account** | The Mail Archiver account that receives the messages |
| **Target Folder** | The folder the messages are imported into. Pick one from the account's existing folder list, or type a new name — a folder that does not exist yet is **created automatically** during the import. Defaults to `INBOX`. |

All messages in the file are imported into that single target folder. The MBox
file itself carries no folder information, so the structure of the original
mailbox is not reconstructed — if you need that, use the EML/ZIP route.

## 🗂️ Import EML

EML import expects a **ZIP archive** whose entries are `.eml` files.

- `.eml` files in the **root** of the ZIP are imported into `INBOX`.
- `.eml` files in a **subfolder** are imported into a folder with the **same
  name** as that subfolder.

So a ZIP laid out like this:

```
mailbox.zip
├── 2024-invoice.eml            → INBOX
├── Archive/
│   └── old-message.eml         → Archive
└── Projects/
    └── 2026/
        └── kickoff.eml         → Projects/2026
```

restores the folder tree along with the messages. Individual `.eml` files can be
uploaded by wrapping them in a ZIP first (`zip emails.zip *.eml`).

## ⚙️ Shared behaviour

Both forms behave the same way once you press **Start Import**:

- **File size limit** — configured with `Upload__MaxFileSizeGB` (default **10 GB**).
  The form shows the current limit next to the file picker.
- **Background processing** — the upload is queued as a job; you do not have to
  keep the browser open.
- **Duplicate handling** — messages that are already in the archive are skipped
  and counted separately. Duplicates are matched on the `Message-ID` header.
- **Malformed input** — unreadable files are skipped individually and reported on
  the status page instead of aborting the whole import.
- **Progress** — you are taken straight to the job status page, which shows the
  job ID, file name, file size, status, processed/total messages and the running
  counts. The same job also appears on the [Jobs](BackgroundJobs.md) page.
- **Cancelling** — a running import can be cancelled from its status page. The
  messages imported up to that point stay in the archive.
- **Audit trail** — the import is written to the [access log](Logs.md), and the
  per-account storage value is refreshed afterwards (see
  [Per-Account Storage](AccountStorage.md)).

### Job statuses

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled`

A finished job reports how many messages were imported, how many failed and how
many were skipped as duplicates — for example
*"1420 imported, 3 failed, 87 duplicates"*. On a successful import the uploaded
file is deleted from the server; failed entries are kept for the lifetime of the
job so you can see what was rejected.

## ⚠️ Things to watch out for

- **Wrong file type** — the upload is rejected unless the extension is one of
  `.mbox`, `.eml` or `.zip`. The EML form's picker only accepts `.zip`, the MBox
  form's only `.mbox` and `.mbx`.
- **"0 imported, N duplicates"** — this is not an error. The messages are already
  in the archive, most likely from an earlier import or sync of the same source.
- **Very large archives** — an MBox file is read message by message, so a
  multi-gigabyte file takes a while even in the background. Watch the progress on
  the status page rather than re-uploading.
- **Free disk space** — the uploaded file is stored temporarily on the server and
  needs room next to your archive database.
- **Folder names with special characters** — folder names are taken from the ZIP
  paths or your input; check the account's folder list after the import if you
  used unusual characters.

## 🔗 Related Documentation

- [CLI Local Import](CLI-Local-Import.md) — importing from a mounted directory
- [Account Import (CSV)](Account-Import.md) — adding many accounts at once
- [Background Jobs](BackgroundJobs.md) — monitoring and cancelling import jobs
- [Exporting a Mail Account](Export-Account.md) — the opposite direction
- [Setup and Parameters](Setup.md) — `Upload__*` settings
