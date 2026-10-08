# 🔍 Mail Search

[← Back to Documentation Index](index.md)

## 📋 Overview

Search is the heart of an email archive. Mail Archiver searches the **full text**
of your archived mail — subject, body, sender and recipients — across all
accounts you have access to, and returns results in milliseconds even on
archives with millions of messages.

This guide covers the search interface, the query syntax, filters, sorting,
paging, and what to do when you need to act on a result set.

> **ℹ️ Search only sees what you may see.** Results are filtered by your
> account permissions. A non-administrator only ever finds mail from the
> mailboxes assigned to them — the restriction is applied in the query itself,
> not in the interface.

## 🚀 Accessing Search

1. Log in and choose **Archive** in the main menu
2. The search bar sits above the result list

Opening the Archive without a search term lists your most recent emails, newest
first. You can browse from there or narrow down with the filters.

## 🎯 The Search Interface

| Field | What it does |
|---|---|
| **Search Term** | Full-text query — see [Query Syntax](#-query-syntax) below |
| **From** | Only emails sent on or after this date |
| **To** | Only emails sent on or before this date |
| **Direction** | `All`, `Incoming` or `Outgoing` |
| **Account** | Restrict to one mail account |
| **Folders** | Restrict to one folder via the folder tree |
| **Search** | Run the query |
| **Reset** | Clear all filters |

The search term matches the **subject, body, sender and recipients**
(`From`, `To`, `Cc`, `Bcc`).

### The folder tree

Folders are shown as a **hierarchical tree** on the left, mirroring the folder
structure of the source mailbox — including nested folders. Click a folder to
restrict the search to it, and use the collapse/expand control to fold the tree
away when you need the screen space.

Selecting a folder does not change your search term; the two combine.

## 🧩 Query Syntax

The syntax is deliberately small. Everything below is what the application
actually understands.

### Plain words — and why prefixes work

Type one or more words; all of them must be present (an **AND** search):

```
invoice january
```

Finds emails containing both `invoice` and `january` in the searched fields.

> **💡 Partial words are matched automatically.** Each word is treated as a
> **prefix**, so a search for `isenb` also finds `isenboeck` and `isenböck`, and
> `rech` finds `Rechnung` as well as `Rechnungen`. You do not need a wildcard
> character — there is none to type. This also means a very short term can
> return a lot; see [Performance](#-performance-and-large-archives).

The search is **case-insensitive**. `Invoice`, `invoice` and `INVOICE` are the
same query.

### Exact phrases — quotes

Wrap text in double quotes to require the words **next to each other** in that
order:

```
"important meeting"
```

Without quotes, `important meeting` would also match an email where the two
words appear in unrelated sentences. With quotes, only the adjacent phrase
matches.

### Field-specific search

Prefix a term with a field name and a colon to search **only** that field:

| Syntax | Searches in |
|---|---|
| `subject:invoice` | Subject only |
| `from:john` | Sender only |
| `to:example.com` | Recipients only |
| `body:important` | Body only |

Field names are case-insensitive (`Subject:` works too), and each can be
combined with quotes for a phrase:

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ Four fields are supported:** `subject`, `body`, `from`, `to`. Writing
> `cc:someone` or `bcc:someone` is **not** a field search — the term is treated
> as ordinary text and searched across all fields. To find a recipient who was
> only on Cc, search for the address or name without a prefix.

### Combining everything

Filters and terms add up. This query finds emails from any insurance sender,
with "car insurance" as an exact phrase in the body, and "invoice" in the
subject:

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

Practical combinations:

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### Characters that are ignored

The query parser strips the characters `& | ! ( ) : *` from individual terms,
because they carry meaning in the underlying full-text engine. If your search
returns nothing unexpected, check whether a special character is the reason.

## 📊 Sorting and Paging

### Sorting

Click a column header to sort. Sortable columns:

| Column | Sorts by |
|---|---|
| **Subject** | Subject line, alphabetically |
| **From** | Sender |
| **To** | Recipients |
| **Date** | Send date |

Clicking the same header again reverses the direction. The default is **newest
first**.

### Page size

Choose how many results to show per page:

**20** (default) · **50** · **75** · **100** · **150**

A larger page shows more context but takes longer to render. Use the
**Previous** / **Next** controls to move through the result pages; the result
count tells you how many emails matched in total.

> **ℹ️ Your timezone applies to the dates shown.** Displayed times are
> converted from UTC to your configured timezone, so the date you see matches
> your local calendar — useful when you are searching around midnight.

## 👁️ Working with Results

Each row shows the account, sender, subject, date, and a **📎 paperclip icon**
when the email has attachments. The paperclip is an indicator only — it is not
a filter.

Click **Details** to open an email:

- Full body (HTML or plain text)
- **Attachments**, listed with the option to download each one
- **Download as EML** — the complete message, for use elsewhere
- **Restore** — copy the email back to a live mailbox

See [Mailbox Migration](MailboxMigration.md) for restoring emails to a mailbox.

## ✅ Acting on Many Results at Once

Results can be selected and processed in bulk. Turn on **Selection Mode** to get
checkboxes, then:

- **Select All** / **Deselect All** for the current page
- **Copy Selected** — restore to a mailbox
- **Export Selected** — as MBOX or EML
- **Delete Selected** — from the archive

The counter shows *"{selected} of {total} selected"* as you work.

### The selection limit

> **⚠️ A maximum of 250 emails can be selected at once** (configurable via
> `Selection:MaxSelectableEmails`). Beyond that the interface shows *"Maximum
> selections reached (250)"* and **Too Many — Split Required**.

That limit exists because each operation has to hold the affected messages. The
prompt *"Split larger operations into multiple batches"* is the intended
workaround: work through the results in groups of 250 or fewer.

Attempting to exceed it shows: *"Too many emails selected. Maximum allowed is
250 emails. Please reduce your selection or split into multiple operations."*

### Direct processing vs. background job

For **Copy** and **Export** you choose how the work runs:

| Option | Behaviour |
|---|---|
| **Process on this page (wait for completion)** | The browser waits; good for a handful of emails |
| **Process in background (monitor separately)** | Runs as a job; you can close the browser |

Exports always run in the background:

> *"The export will run in the background and you will be able to download the
> result from the Jobs page once completed."*

Find running and finished jobs on the [Background Jobs](BackgroundJobs.md) page.

> **⚠️ Deleting is permanent.** The confirmation is explicit: *"Are you sure you
> want to permanently delete {n} selected email(s)? This action cannot be undone
> and each deletion will be logged for audit purposes."* Deletions are recorded
> in the access log.

## ⚡ Performance and Large Archives

Search is backed by a PostgreSQL full-text index, so the difference between a
fast and a slow search is usually how **selective** the query is.

**Do:**

- Add a **date range** when you know roughly when the email arrived — this is
  the single most effective narrowing
- Restrict to an **account** or **folder** when you know where the mail lives
- Use **several words** instead of one very short one
- Use field searches (`subject:`, `from:`) to cut out unrelated matches

**Avoid:**

- A single short prefix such as `re` — as a prefix search it matches a huge
  share of your archive
- Very wide date ranges combined with a common word
- Browsing many pages when you could tighten the search instead

> **💡 If a search feels slow, the date range is usually the answer.** Restricting
> to a month often turns a multi-second search into an instant one, because the
> index can skip most of the archive.

## ❓ Troubleshooting

| Symptom | Cause | What to do |
|---|---|---|
| No results, but you expect some | Search term too specific, or a typo | Try fewer words; check spelling |
| `cc:name` finds nothing useful | `cc` is not a supported field | Search the name without a prefix |
| A special character breaks the query | `& \| ! ( ) : *` are stripped | Remove or replace the character |
| Too many results | Common word or short prefix | Add a date range or field search |
| *"Too Many — Split Required"* | More than 250 emails selected | Reduce the selection or split the operation |
| An export is missing | It runs as a background job | Check [Background Jobs](BackgroundJobs.md) |
| Results look like they are missing | Account permissions | Confirm the mailbox is assigned to your user |
| Dates appear shifted | Timezone conversion | Check your timezone setting |

## 📚 Related Documentation

- [Mailbox Migration](MailboxMigration.md) — restoring results to a mailbox
- [Background Jobs](BackgroundJobs.md) — monitoring exports and restores
- [Access Logging](Logs.md) — the audit trail of searches and deletions
- [REST API](API.md) — the same archive over the API
- [MCP Server](MCP.md) — search from an AI agent
- [Retention Policies](RetentionPolicies.md) — when emails leave the archive
