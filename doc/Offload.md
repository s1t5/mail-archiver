# 📤 Date-Windowed Offload

[← Back to Documentation Index](Index.md)

## 📋 Overview

An offload appends archived mail newer than a cutoff into another mailbox, and leaves everything
older in the archive. It exists for migrations where the new mailbox should hold only recent mail
while the full history stays reachable through Mail Archiver.

It differs from [Copy All Emails to Another Mailbox](MailboxMigration.md) in five ways:

| | Copy All Emails | Offload |
|---|---|---|
| Date filter | none, all or nothing | cutoff, with an optional upper bound |
| Repeatable | no, appends everything again | yes, skips what the target already holds |
| Folder exclusions | no | yes, configurable |
| Folder renaming | no | yes, configurable |
| Preview | no | dry run reports per folder without writing |

The offload only ever appends to the target. It never deletes from the archive and never touches
the source mailbox.

## 🔁 Repeating a run is safe

Before the first append, the whole target mailbox is indexed and every message is checked against
it. A repeated run therefore appends nothing that already arrived, and reports it as already
present instead. That has three practical consequences:

- An interrupted run is recovered by simply running it again.
- A finished run can be repeated as a verification pass. A correct result is nothing appended and
  everything already present.
- Messages deleted on the target come back on the next run, because they are no longer in the
  index.

Two criteria decide whether a message is already present, in this order:

1. **Message-ID**, normalised. This does nearly all of the work.
2. **A fingerprint** over sender, recipients, subject and send time within two seconds. This
   covers the messages whose stored Message-ID is missing or malformed. Such messages cannot be
   restored with their original Message-ID at all, so without this second criterion they would
   duplicate on every single run.

The job reports how many matches came from the fingerprint separately, because it is the
criterion that could fail quietly.

## ⚙️ Configuration

A new `Offload` section, with defaults that reproduce the previous behaviour:

```json
"Offload": {
  "MaxConcurrentJobs": 1,
  "PrefetchMaxMessages": 500000,
  "ExcludedSourceFolders": [],
  "FolderRenameMap": {},
  "MarkAsSeen": true
}
```

| Key | Default | Purpose |
|---|---|---|
| `MaxConcurrentJobs` | `1` | How many restore or offload jobs may run at once. One keeps the strictly serial behaviour the job queue always had. At most one job per target mailbox runs regardless of this value. |
| `PrefetchMaxMessages` | `500000` | Upper bound on how many messages are indexed from a target mailbox. Above it the duplicate check narrows to a single folder and logs that it did. |
| `ExcludedSourceFolders` | empty | Source folders never offloaded. Matched before renaming, and covering subfolders. |
| `FolderRenameMap` | empty | Rewrites the leading segments of a source folder path. |
| `MarkAsSeen` | `true` | Whether appended mail is flagged as read. |

Both folder settings ship empty on purpose. Rewriting or dropping folders without being asked
would surprise anyone already using the restore path, so a run with no configuration migrates
everything, spam folders included, and creates a second set of special folders next to the
target's own.

### Folder exclusions and renaming

For an Exchange to Dovecot migration the two settings usually look like this:

```json
"ExcludedSourceFolders": ["Junk E-Mail", "Deleted Items"],
"FolderRenameMap": {
  "Sent Items": "Sent",
  "Deleted Items": "Trash",
  "Junk E-Mail": "Junk"
}
```

Without the rename map every migrated mailbox ends up with both `Sent` and `Sent Items`, because
Dovecot pre-creates its own special-use folders.

Four things about how these are applied are worth knowing, because each one is a way to be
surprised:

1. **Exclusions are matched first**, against the source name. If renaming happened first, an
   exclusion on `Deleted Items` would never fire once it had already become `Trash`.
2. **Renaming rewrites the longest matching path prefix**, on segment boundaries. `Sent Items`
   becoming `Sent` also turns `Sent Items/2019` into `Sent/2019`, and leaves
   `Sent Items Archive` alone.
3. **Matching is case insensitive**, and both `/` and `\` count as separators.
4. **Two source folders may collapse onto one target.** `Sent Items` and `Sent` both becoming
   `Sent` is fine and does not open the target folder twice.

Changing the rename map between two runs does not duplicate anything, because the duplicate check
covers the whole target mailbox rather than one resolved folder.

## 🖥️ Running it from the interface

On an account's detail page, **Offload to Another Mailbox** opens a form with the target mailbox,
the target root folder, whether to preserve the folder structure, the date window, a dry run
switch, and whether to mark appended mail as read. The configured exclusions and rename map are
shown read-only, so a run is never a surprise.

The job is queued and its progress and per-folder report appear on the job status page and under
**Jobs**. The per-folder report names source folders, so it is shown only to the user who started
the job and to administrators.

The target mailbox must be an enabled IMAP account. Microsoft 365 accounts cannot be offload
targets; the Graph restore path is unchanged.

### Who may run it

Administrators can offload between any two accounts. A self-manager can offload **only between
the accounts assigned to them**, at both ends: the source, because the account page is already
scoped that way, and the target, because the target list is narrowed to the same set and the
request is checked against it again when the job is started. A self-manager with a single
assigned account therefore has no eligible target and is told so.

Scoping comes from `IAccountAccessResolver`, the same resolver the REST API and the MCP server
use, so there is one definition of who may use which mailbox. The decision itself lives in
`OffloadTargetEligibility`, which both the target list and the request check call, so what the
form offers and what the server accepts cannot drift apart.

## ⌨️ Running it from the command line

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll \
  --offload --source-account-id 3 --target-account-id 7 \
  --since 2025-08-01 \
  [--until 2026-08-01] [--target-folder INBOX] \
  [--preserve-folders] [--dry-run] [--no-mark-seen]
```

| Argument | Meaning |
|---|---|
| `--source-account-id` | Account to read archived mail from. Required. |
| `--target-account-id` | Enabled IMAP account to append into. Required, and must differ from the source. |
| `--since` | Inclusive lower bound on the send date, `YYYY-MM-DD`. Required. |
| `--until` | Optional inclusive upper bound. |
| `--target-folder` | Root folder in the target mailbox. Defaults to `INBOX`. |
| `--preserve-folders` | Recreate the source folder structure below the root. |
| `--dry-run` | Report what would happen, append nothing. |
| `--no-mark-seen` | Append without the Seen flag. |

Exit codes:

| Code | Meaning |
|---|---|
| `0` | Everything was appended or already present. |
| `1` | At least one message failed, or the run itself failed. |
| `2` | The invocation was wrong: bad or missing arguments, unknown account, target not IMAP, target disabled, source equal to target. |

One invocation handles one mailbox, which makes a fleet of mailboxes scriptable from a CSV of
source, target and cutoff.

> **Running several at once.** The one-job-per-target rule only exists inside a single process,
> so two concurrent command line invocations against the *same* target mailbox could each take
> their duplicate index before the other starts writing, and could then both append the same
> mail. Partition the work so that every concurrent invocation has a different target, which is
> the normal case when each row of a CSV is a different mailbox.

## 🗓️ How the cutoff is interpreted

- The filter is on **the send date**, not on when a message was archived. The archived-at
  timestamp is the time of the archiving run and carries no meaning for a cutoff.
- A relative window such as "the last twelve months" is **resolved to a fixed date when the job
  is created**. A job may run for a long time and be repeated afterwards, and it has to select
  the same mail every time; a stored relative window would drift.
- Cutoffs are interpreted in the **configured display timezone**, the same one the search screen
  shows, and the lower bound is inclusive from 00:00 of that day. An upper bound includes the
  whole of its day.

## 📋 A migration, end to end

1. Create the target mailboxes. For many at once see the CSV bulk import in
   [Account Import](Account%20Import.md).
2. Configure `Offload:ExcludedSourceFolders` and `Offload:FolderRenameMap`. Both are empty by
   default, so skipping this migrates spam folders and creates duplicated special folders.
3. Confirm each source mailbox is fully archived before offloading it.
4. **Dry run everything.** Check the counts and the resolved target folder names before anything
   is written.
5. Real run. Start with `MaxConcurrentJobs` at 2 and raise it only after watching how the target
   server copes; Dovecot limits concurrent connections per user and per IP.
6. Repeat the run to verify. Expect nothing appended and everything already present.
7. Disable the source accounts.

Throughput is capped by `BatchOperation:PauseBetweenEmailsMs`, which is 50 ms by default and so
allows at most twenty appends per second before server latency is counted.

## 🔍 What the counters mean

| Counter | Meaning |
|---|---|
| Appended | Written to the target. On a dry run, what would have been written. |
| Already present | The target already held it, by either criterion. |
| of those by fingerprint | How many were recognised by the second criterion rather than by Message-ID. |
| Excluded folder | Skipped because the source folder is excluded. |
| Failed | Could not be appended. |

A warning is reported if the duplicate index did not cover the whole target mailbox, which
happens above `PrefetchMaxMessages` or when a folder could not be read. In that state a repeated
run may append mail that is already present in the parts that were not indexed.

## ⚠️ Notes and limits

- Restored mail now carries its **original delivery time** as the IMAP internal date, taken from
  the message's `Received` chain and falling back to the send date. It previously carried the
  time of the archiving run, which broke server side sorting by arrival and any age based rule on
  the target.
- Because a repeated run skips what is already present, a wrong internal date cannot be corrected
  by re-running: the messages are recognised and skipped. Get this right before a production run,
  or delete the affected messages on the target and run again.
- Microsoft 365 is not supported as an offload target.
- Duplicate detection can only recognise what it can see in the target mailbox. Mail that reached
  the target by some other route is caught only insofar as the two criteria happen to match it.
