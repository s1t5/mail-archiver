# 📤 Exporting a Mail Account (EML / MBox)

[← Back to Documentation Index](index.md)

## 📋 Overview

The account export writes **every message of one account** back out to a
downloadable ZIP file. It is the counterpart to the
[web import](Import-EML-MBox.md): you get a portable archive of the account in a
format that any mail client or migration tool can read.

Two formats are offered:

| Format | What you get | Typical use |
|--------|--------------|-------------|
| **EML** | Individual `.eml` files, organised into folders | Re-import into a mail client, per-message processing, long-term readability |
| **MBox** | Traditional mailbox files — **one file per folder** | Tools and clients that read MBox, bulk text processing |

In both cases the messages are written as raw RFC-822 messages, so **attachments
are included** in the export.

> 💡 **Tip**: To export only a selection of messages instead of a whole account,
> use the bulk actions in [Mail search](Search.md#-acting-on-many-results-at-once).

## 🧭 Access

1. Open **Mail Accounts** in the main menu.
2. Click the **⋮** (more actions) button on the account row and choose
   **Export**. The same entry is on the account's **Details** page.

Exporting requires the **admin** or **self manager** role — standard users see
the entry but cannot use it. The export always covers the whole account; there is
no date or folder filter.

## 🖥️ Using the Export Page

The page shows the account's **total**, **incoming** and **outgoing** message
counts, so you can see the size of the job before starting it. Then:

1. Choose the format — **EML** or **MBox**.
2. Press **Start Export**.

The export runs **in the background**. You are taken to a status page showing the
job ID, the chosen format, the timestamps and the processed/total message counts.

### Job statuses

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

When the job reaches **Completed**, a **download button** appears on the status
page. The same job is also listed on the [Jobs](BackgroundJobs.md) page.

> ⚠️ **Export files are cleaned up automatically.** The ZIP is deleted **after a
> successful download**, and in any case **after 7 days**. Download the file while
> it is still offered — the archive itself is unaffected, you can simply start a
> new export later.

## ⚠️ Things to watch out for

- **Large accounts take a while.** A big mailbox may need several minutes and the
  ZIP can become very large, depending on the message volume and attachments.
  Because the job runs in the background you can close the browser and come back
  to the status page later.
- **Server disk space.** The ZIP is assembled on the server before you download
  it, so make sure there is enough free space next to the archive.
- **"Export file not found"** — the job has not finished yet, or the file has
  already been downloaded or removed by the 7-day cleanup. Start a new export.
- **No filter options** — the export is all-or-nothing per account. Use the
  search-page export for a subset.
- **Not a compliance export.** For an audit-ready data-media handover with an
  index file, use the [Audit Data Export](AuditExport.md) instead — it produces a
  package in the tabular mass-data format that audit tools expect.

## 🔗 Related Documentation

- [Importing Mail in the Web UI](Import-EML-MBox.md) — the opposite direction
- [Mail search](Search.md) — exporting a selection of messages
- [Background Jobs](BackgroundJobs.md) — job list, progress and cancelling
- [Audit Data Export](AuditExport.md) — audit-ready export packages
- [CLI Local Import](CLI-Local-Import.md) — importing from a mounted directory
