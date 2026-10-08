# ⚙️ Background Jobs

[← Back to Documentation Index](index.md)

## 📋 Overview

Most work in Mail Archiver does not happen while you wait. Synchronising a
mailbox, importing an MBOX file, restoring thousands of emails or exporting an
archive can take minutes to hours. Those tasks run as **background jobs** — you
start them, and they keep running even if you close the browser.

The **Background Jobs** page shows what is currently running, what finished, and
what went wrong. It is the place to look when something takes longer than
expected or when a sync ends with errors.

> **ℹ️ Administrator page.** Background Jobs requires administrator rights and
> is reachable from the main menu. Regular users do not see it — they can still
> start jobs (for example a batch restore), but monitoring happens here.

## 🗂️ Job Types

The page groups jobs by type. Each section shows up to the **20 most recent**
jobs, with running and queued jobs sorted to the top — so an old job does not
get pushed out of sight by newer ones.

| Section | What it covers | Started from |
|---|---|---|
| **Synchronization Jobs** | Fetching new mail from the mail server | Automatic schedule, or *Sync now* on an account |
| **MBox Import Jobs** | Importing `.mbox` files | Mail Accounts → Import MBOX |
| **EML Import Jobs** | Importing `.eml` files / ZIP archives | Mail Accounts → Import EML |
| **Batch Restore Jobs** | Copying selected emails back to a mailbox | Archive → select emails → Batch Restore |
| **Account Export Jobs** | Exporting a whole account (mbox / EML) | Mail Accounts → Export |
| **Selected Emails Export Jobs** | Exporting a hand-picked set of emails | Archive → select emails → Export |
| **Email Deletion Jobs** | Deleting emails from server or archive | Retention policy, or manual deletion |
| **Account Deletion Jobs** | Removing an entire mail account | Mail Accounts → Delete |
| **Offload** | Moving date-windowed mail to another account | Mail Accounts → Offload |

## 📊 Reading a Job Row

Every row carries the same set of columns:

| Column | Meaning |
|---|---|
| **Job ID** | Short identifier — quote this when reporting a problem |
| **Account** | The mailbox the job works on |
| **Status** | Current state, see the table below |
| **Progress** | How far the job has come, with processed/total where known |
| **Created** | When the job was queued |
| **Started** | When it actually began running |
| **Completed** | When it ended (empty while still running) |
| **Duration** | Elapsed time |

Some job types add counters to the progress column:

- **Success** / **Failed** — emails processed successfully and unsuccessfully
- **Skipped (duplicates)** — emails already present in the archive, not imported twice
- **Recovered** — messages re-fetched after the server reported them missing
- **Malformed (skipped)** — messages that could not be parsed and were left out

### Status values

| Status | Meaning |
|---|---|
| **Queued** | Waiting to start — another job is running, or the worker is busy |
| **Running** | Currently executing |
| **Completed** | Finished successfully |
| **CompletedWithErrors** | Finished, but some items failed (MBox imports) |
| **Failed** | Ended with an error |
| **Cancelled** | Stopped by a user |
| **Rate Limited** | *Sync paused due to bandwidth limit. Will resume automatically.* |
| **Timed Out** | The sync exceeded the configured time limit and was stopped |

> **ℹ️ "Rate Limited" and "Timed Out" are sync-only states.** They look like
> failures but are not: a rate-limited sync resumes by itself once the bandwidth
> quota allows, and a timed-out sync continues from its checkpoint at the next
> run. See [Rate Limit Handling](RateLimitHandling.md) and
> [Mail Synchronization](Synchronization.md).

### Why progress can sit at 0%

Progress is measured by **completed folders**, not by individual emails:

> *Progress is measured by completed folders. A large folder (e.g. INBOX) keeps
> the bar at 0% until it finishes.*

On a mailbox where INBOX holds most of the mail, the bar can stay at 0% for a
long time and then jump. That is expected — the job is working, it just has not
finished its first large folder yet.

## 🔄 The Page Refreshes Itself

While the page is open it reloads every **30 seconds**, so you can leave it in a
tab and watch progress without clicking anything. The label shows when the data
was last updated.

The reload is cancelled the moment you click a link or button — otherwise your
click would land on a page that is being replaced under it.

**Configurable:** `Jobs:RefreshSeconds` in `appsettings.json` (default `30`).
Set it to `0` to turn automatic reloading off. Each reload rebuilds the page
from all job sources, so a very short interval on an installation with many
accounts is a real load — and that load multiplies with every open tab.

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 Cancelling a Job

Jobs that support cancellation show a **Cancel Job** button, which asks:

> *Are you sure you want to cancel this job?*

Import jobs use the wording **Cancel Import** / *Are you sure you want to cancel
this import job?*

Cancelling is available for **synchronization, batch restore, selected-emails
export and email deletion** jobs. The job stops as soon as the current item is
finished; the status becomes **Cancelled**.

> **⚠️ Cancelling is not a rollback.** Emails already imported, restored,
> exported or deleted stay that way. Cancelling stops the job from doing *more* —
> it does not undo what it has done. For a deletion job that means the emails
> already removed are gone.

Account and MBox import jobs cannot be cancelled from this page; stop them from
the account's own import page instead.

## ⚠️ Sync Failures and "Acknowledge Failures"

When a sync finishes with **Failed** status, the job row offers **Acknowledge
Failures**. This is the one action on the page that changes future behaviour, so
it is worth understanding.

Each sync run starts where the last one left off, using a **checkpoint**. If a
sync fails for some emails, the checkpoint stays behind those emails so the next
run tries them again. That is the safe default — nothing gets silently skipped.

But if the failures are permanent (a corrupt message, a folder the server
refuses to serve), every subsequent sync retries them and the sync keeps failing
in the same place.

**Acknowledge Failures** resolves that deadlock by advancing the checkpoint past
the failed emails. The confirmation is explicit about the consequences:

> *This will advance the sync timestamp to the completion time of this job, so
> the next sync will be a normal incremental sync. The failed emails will not be
> retried. Continue?*

On success you see: *"Sync timestamp has been advanced. The next sync will be
incremental."*

> **⚠️ Only acknowledge once you have looked at the failures.** After
> acknowledging, those emails are **not** retried — they will be missing from the
> archive unless you fetch them another way. Check the failure details first, and
> if the cause is a misconfiguration, fix that before acknowledging. See
> [Mail Synchronization](Synchronization.md) for the checkpoint behaviour in
> detail.

Acknowledging is recorded in the access log as *SyncAcknowledgeFailures*, with
the number of failed emails and the account, so it is traceable later.

## 👁️ What You Cannot Do Here

- **No retry button.** A failed job is not restarted from this page — start the
  operation again from where you began it (sync, import, export).
- **No editing.** Job parameters are fixed at creation.
- **Only the last 20 per type.** Older jobs are not listed. For long-term
  records, the access log and the application log are the sources — see
  [Access Logging](Logs.md) and [Docker Compose Logs](DockerComposeLogs.md).

## ❓ Troubleshooting

| Symptom | Likely cause | What to do |
|---|---|---|
| Job stays **Queued** | Another job of the same worker is running | Wait; check other sections for a long-running job |
| Progress stuck at 0% | One large folder is still being processed | Normal — see "Why progress can sit at 0%" |
| Sync repeatedly **Failed** on the same account | Permanent errors on individual emails | Inspect the failures, fix the cause, then acknowledge |
| Sync shows **Rate Limited** | Bandwidth quota reached | Nothing — it resumes automatically |
| Sync shows **Timed Out** | Mailbox larger than the time limit | It continues from the checkpoint next run; see [Synchronization](Synchronization.md) |
| **CompletedWithErrors** on an MBox import | Some messages malformed | The counters show how many; the rest was imported |
| Job disappears after reload | It fell out of the "last 20" window | Check the application log for its final status |
| Page reloads while you work | Automatic refresh every 30 s | Set `Jobs:RefreshSeconds` to `0` |

## 📚 Related Documentation

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — checkpoints, timed-out syncs
- [Rate Limit Handling](RateLimitHandling.md) — bandwidth quotas and automatic resumption
- [Access Logging](Logs.md) — who did what, including job actions
- [Batch Restore](MailboxMigration.md) — moving emails between mailboxes
- [Date-Windowed Offload](Offload.md) — offload jobs
- [Docker Compose Logs](DockerComposeLogs.md) — reading the application log
