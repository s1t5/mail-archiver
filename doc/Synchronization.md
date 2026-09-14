# 🔄 Mail Synchronization (Quick vs. Full Sync)

[← Back to Documentation Index](Index.md)

## 📋 Overview

Mail Archiver synchronizes every enabled mailbox automatically in the background through the `MailSyncBackgroundService`. The service runs a short polling loop (every 60 s), determines which accounts are due, and syncs them — up to `MailSync:MaxConcurrentSyncs` accounts in parallel within one cycle — before rescheduling each account according to its own sync interval. Intervals can be configured globally via `appsettings.json` and overridden per account from the Create/Edit page.

There are two distinct synchronization modes:

| Mode | Triggered by | Scope |
|------|--------------|-------|
| **Quick Sync** (incremental) | Every sync cycle (per-account interval) | Only new/changed messages since the last successful sync |
| **Full Sync** (resync) | New account, manual button, `AlwaysForceFullSync`, or per-account / global Full Sync interval | Every message in every non-excluded folder |

Both modes are safe to run repeatedly – Mail Archiver detects duplicates (by `MessageId`, or by From/To/Subject/SentDate when the `MessageId` is missing) and skips messages that are already archived.

---

## ⚡ Quick Sync (Incremental Sync)

Quick sync is the normal operating mode that runs automatically at the configured interval. The global default is `MailSync:IntervalMinutes` minutes (default 15); each account can override this with its own `SyncIntervalMinutes` value set on the Create/Edit page (leave empty to use the global default).

### How it works

1. For each enabled account (excluding `IMPORT` provider accounts), the service reads the account's `LastSync` timestamp.
2. A date filter is built from `LastSync`:
   - **IMAP**: `SearchQuery.DeliveredAfter(LastSync − 12 hours)`
   - **M365 (Graph)**: `receivedDateTime ge (LastSync − 12 hours)`
3. The 12-hour overlap is intentional. It catches messages that were delivered by the provider after the previous sync started but with a slightly older server timestamp, and it tolerates minor clock skew between the mail server and the Mail Archiver host. Duplicates are filtered out by the duplicate check, so the overlap never creates double entries.
4. For each non-excluded folder, the filtered message list is fetched in batches and archived. Existing messages are skipped.
5. On **successful completion** (no failed messages, no folders that failed as a whole, no rate-limit hit), `LastSync` is set to `DateTime.UtcNow` and the next cycle starts from that point.
6. If **any message failed** to process, or **any folder could not be synced at all** (see [Folders That Fail as a Whole](#-folders-that-fail-as-a-whole)), `LastSync` is **not** updated, so the next cycle re-attempts the same window.
7. If the account is **rate-limited** (see [Rate Limit Handling](RateLimitHandling.md)), `LastSync` is also left untouched and the sync resumes from a per-folder checkpoint once the daily quota resets.

### When you see it

- Automatically, every few minutes, for all enabled accounts.
- After the initial sync of a new account has completed (the first cycle is a Full Sync, see below).
- No user action required.

---

## 🗄️ Full Sync (Resync)

A Full Sync ignores the date filter and downloads **every** message in every non-excluded folder from the server, regardless of age.

### What triggers a Full Sync

A Full Sync is triggered whenever an account's `LastSync` is set to the Unix epoch (`1970-01-01T00:00:00Z`). This happens in four situations:

1. **New account** – Every newly created IMAP or M365 account starts with `LastSync = 1970-01-01`. The first scheduled sync cycle for that account is therefore automatically a Full Sync, which performs the initial archive import.
2. **Manual "Full Resync" button** – On the *Account Details* page, the **Full Resync** button (`MailAccounts/Resync`) resets `LastSync` to the epoch and starts the sync immediately in the foreground of the request.
3. **`MailSync:AlwaysForceFullSync = true`** – When this configuration flag is enabled, the background service resets `LastSync` to the epoch for **every enabled account at the beginning of every sync cycle**. This effectively turns every cycle into a Full Sync. This is intended for troubleshooting only and should be turned back off once the issue is resolved, because it drastically increases load on the mail server and the Mail Archiver instance.
4. **Per-account or global Full Sync interval** – When an account has `FullSyncIntervalHours` set (Create/Edit page), or the global default `MailSync:FullSyncIntervalHours` is configured in `appsettings.json`, the background service automatically triggers a Full Sync once that interval has elapsed since the account's last full sync (`LastFullSync`). The per-account value takes precedence; when neither is set no automatic full sync runs and only the manual resync button (and `AlwaysForceFullSync` above) remain. After the full sync completes, `LastFullSync` is updated to `DateTime.UtcNow` so the next full sync is scheduled from that point.

### Behavior during a Full Sync

- No `DeliveredAfter` / `receivedDateTime ge …` filter is applied. The server is asked to return all messages in the folder.
- If the server returns fewer results than the folder actually contains (some IMAP servers cap `SEARCH` results), Mail Archiver detects the discrepancy and falls back to fetching all `UniqueId`s by sequence number, so no messages are silently dropped.
- Messages that are already in the archive are detected as duplicates and skipped – the existing archived copy is **not** overwritten. If a duplicate is found in a different folder name than before, the stored `FolderName` field is updated to reflect the current location.
- For very large mailboxes, the Full Sync can take several hours or even days. When bandwidth tracking is enabled, the sync pauses gracefully at the daily quota and resumes from per-folder checkpoints on the next day (see [Rate Limit Handling](RateLimitHandling.md)).
- `LastSync` is updated to `DateTime.UtcNow` only after a Full Sync completes without failed messages and without failed folders, exactly like a Quick Sync.

### When to use a manual Full Sync

Use the **Full Resync** button when:

- You changed the account's **excluded folders** list and want to confirm the archive matches the current selection.
- You suspect the archive is missing messages (e.g. after a server migration, a provider outage, or a clock-skew incident).
- You migrated the mailbox to a different backend and want to verify completeness.
- You want to re-detect messages that were moved between folders on the server.
- Folders that were previously invisible to Mail Archiver became visible again — e.g. Outlook.com folders created through third-party clients that were filtered out of the IMAP folder listing until they were renamed via the web UI (see [Personal Microsoft Account Setup → Troubleshooting](MSA_Outlook_Setup.md#-troubleshooting)). A Full Sync is needed because a Quick Sync would not backfill the older mails in those folders.

Do **not** use Full Sync:

- As a regular operation – it puts unnecessary load on the provider and your Mail Archiver instance.
- To "refresh" message bodies or metadata – duplicates are skipped, so existing archived copies are not updated by a Full Sync. To replace an archived message you must delete it from the archive first, then run a (Quick or Full) sync.
- Permanently via `AlwaysForceFullSync=true` – leave this off in production. It is a diagnostic switch, not a mode of operation.

---

## 🆚 Quick Sync vs. Full Sync at a Glance

| Aspect | Quick Sync | Full Sync |
|--------|-----------|-----------|
| Date filter | `LastSync − 12 h` to now | None (all messages) |
| Typical volume | A few new messages | Entire mailbox |
| Duration | Seconds to minutes | Minutes to days (depending on mailbox size and provider limits) |
| Bandwidth impact | Low | High |
| `LastSync` updated on success | Yes | Yes |
| `LastSync` updated on failure / rate-limit | No (retry next cycle) | No (resume from checkpoint) |
| Duplicate handling | Skip already-archived messages | Skip already-archived messages |
| Triggered by | Scheduler (per-account interval) | New account, manual button, `AlwaysForceFullSync`, or Full Sync interval |
| Recommended for | Everyday operation | Initial import and occasional verification |

---

## ⚙️ Configuration

The sync behavior is controlled by the `MailSync` section of `appsettings.json` (or environment variables `MailSync__*` in Docker). See [Setup.md](Setup.md) for the full parameter list.

| Setting | Default | Description |
|---------|---------|-------------|
| `MailSync:IntervalMinutes` | `15` | Global default for the per-account sync interval, in minutes. Each account can override this on the Create/Edit page (leave empty to use this default). |
| `MailSync:FullSyncIntervalHours` | _unset_ | Optional global default for automatic full resyncs, in hours. When unset (the default), no automatic full sync runs unless a per-account `FullSyncIntervalHours` value is set. Per-account values override this. |
| `MailSync:TimeoutMinutes` | `0` (none) | Per-account sync timeout. A sync that runs longer stops at the next message boundary and resumes on the next run. `0` (the default, or any non-positive value) means no timeout. See [Stopping a Sync Early](#-stopping-a-sync-early). |
| `MailSync:ConnectionTimeoutSeconds` | `300` | IMAP connection timeout. |
| `MailSync:CommandTimeoutSeconds` | `600` | IMAP command timeout. |
| `MailSync:AlwaysForceFullSync` | `false` | When `true`, every cycle is a Full Sync for all accounts. **Diagnostics only – keep off in production.** |
| `MailSync:IgnoreSelfSignedCert` | `false` | Accept self-signed TLS certificates for IMAP connections. |
| `MailSync:MaxConcurrentSyncs` | `1` | How many account syncs may run at the same time. Slots are refilled as they come free, see [How accounts are scheduled](#-how-accounts-are-scheduled). `1` keeps syncs sequential; increase to parallelize — mind provider rate limits and local resource usage. |
| `MailSync:InterAccountDelaySeconds` | `0` | Optional stagger delay in seconds applied at the end of each account sync task. Useful to avoid burst-starts when `MaxConcurrentSyncs > 1`. `0` disables it. |
| `MailSync:MaxIssuesPerKind` | `20` | How many problems of each kind a sync job remembers for the account page — failed folders, missing folders and failed messages are budgeted separately. Anything beyond is counted, not kept. `0` switches the detail off and leaves only the counters. |
| `MailSync:GlobalExcludedFolders` | _empty_ | Folders excluded from synchronization for every account, additive to each account's own list. See [Excluded Folders](#-excluded-folders) below. |

> 💡 Both the normal sync interval and the full-sync interval can be overridden per account on the **Create/Edit Mail Account** page. Leave the per-account fields empty to fall back to the global defaults above. To remove an account from the scheduler entirely, disable it (toggle *Enabled* off on the Account Details page).

---

## 🚫 Excluded Folders

Folders can be excluded from synchronization from two places, and the two are **additive**: a folder
is skipped when it matches **either** list. Neither can re-include what the other excluded, so
adding an entry anywhere can only ever remove folders from the sync.

| Source | Scope | Where |
|--------|-------|-------|
| Account exclusion list | One account | *Create/Edit Mail Account* page |
| `MailSync:GlobalExcludedFolders` | Every account of the installation | `appsettings.json` or `MailSync__GlobalExcludedFolders__<n>` |

The global list is **empty by default**, so an installation that never configures it behaves exactly
as before. It exists for the case where many mailboxes are imported from the same server and the
alternative is maintaining an identical exclusion list on every single account.

Both lists use the same matching rules, so they cannot drift apart:

1. exact match against the folder's full path (`INBOX/Drafts`);
2. exact match against the folder's own name (`Drafts`), which catches an entry typed as the short
   name when the server reports a prefixed path;
3. path-suffix match, which catches separator variations — `Drafts` also matches `INBOX.Drafts` and
   `INBOX/Drafts` — and Gmail-style names such as `[Gmail]/Drafts`.

All comparisons are case-insensitive. The full-path suffix rule anchors on the path separator, so
`Kalender` does not take a folder named `Kalenderwoche` with it. The name comparison is not anchored
the same way: a short entry also matches any folder whose own name **ends with** it, so `Kalender`
takes a folder named `AltKalender` with it. To exclude exactly one folder among similarly named
ones, enter its full path.

**No folder name is ever excluded by default.** Which names are worth listing depends entirely on
the server and its language — a mailbox tree that also carries calendar, contact, task and note
folders is a common reason to use this, but Mail Archiver does not assume it. See
[Setup.md](Setup.md) for a configuration example.

Changing either list does not remove anything already archived. Run a
[Full Sync](#-full-sync-resync) afterwards if you want to confirm the archive matches the current
selection.

Exclusion applies everywhere the folder list is walked, not only to archiving: an excluded folder
is also skipped by the server-side deletion pass (see below), so an entry protects the folder on
the server as well.

---

## 🗑️ Server-Side Deletion During Sync

If an account has `DeleteAfterDays` configured (> 0), Mail Archiver deletes messages older than the configured threshold from the **mail server** after each sync:

- **IMAP**: `SearchQuery.SentBefore(now − DeleteAfterDays)` per folder, then expunge.
- **M365 (Graph)**: `receivedDateTime lt (now − DeleteAfterDays)` per folder, then delete.

The archived copies in Mail Archiver are **not** affected by this – only the server-side mailbox is trimmed. Folders on either exclusion list ([Excluded Folders](#-excluded-folders)) are skipped by this deletion pass and left untouched on the server. See [Retention Policies](RetentionPolicies.md) for the local retention counterpart that controls how long archived copies are kept.

---

## 🛟 Recovering Messages the Server Reports as Missing

Some servers refuse a message through the normal UID fetch and still return a usable MIME document
for a plain `BODY[]` fetch. This has been observed on legacy on-premises Exchange, where the fetch
fails with:

```
MailKit.MessageNotFoundException: The IMAP server did not return the requested message.
```

Without a fallback such a message is counted as a failed email, and because `LastSync` is not
advanced while an account has failures, the account re-reads the same messages on every run and
never makes progress.

**What Mail Archiver does:** exactly one further fetch attempt for that UID, and only for this
specific error. Authentication problems, connection loss, throttling and parse errors are not
affected, and the existing transient-retry behaviour is unchanged. The attempt is read-only: no
message is marked as read, no flag is changed, nothing is moved or deleted. It counts as a success
only if raw data actually arrived **and** parsed as a MIME message — anything less remains a failed
email. Recovered messages then take the normal archiving path, including duplicate detection, so a
repeated full sync does not archive them twice.

**Provider error placeholders:** what the server returns is often not the original message but a
document the provider generated in its place, for example:

```
Subject: Retrieval using the IMAP4 protocol failed for the following message: 270198
From: Microsoft Exchange Server 2010

The server couldn't retrieve the following message: ...
```

This is archived exactly as received and is never rewritten into the sender, subject or body quoted
inside it. Mail Archiver cannot recover content the provider itself refuses to convert to MIME; what
it preserves is the best representation the server is able to expose, plus the fact that a
placeholder is what arrived. Each one is logged at `Warning` level with account, folder, UID and
subject.

**When the fallback comes back empty**, the message stays a failed email exactly as before, and the
attempt is logged at `Warning` rather than passing quietly — an unretrieved message must never be
able to look archived. Should a server ever behave that way consistently, the documented next step is
to fetch `BODY.PEEK[HEADER]` and `BODY.PEEK[TEXT]` separately and assemble them into one MIME
document. That is a genuinely different request, where the current fallback is the same request read
differently. It is deliberately not implemented: it costs a second round trip per message and adds a
"header arrived, text did not" state to define, and no server seen so far needs it.

Both counts appear in the account's completion log line:

```
Sync completed for account: Example. New: 0, Failed: 0, Deleted: 0, Recovered: 25, Provider placeholders: 25, Failed folders: 0
```

`Recovered` counts messages the fallback rescued; `Provider placeholders` is the subset of those
that turned out to be provider-generated error documents. `Failed: 0` alongside them is the point of
the feature — the account is no longer stuck.

---

## 📁 Folders That Fail as a Whole

A message can fail on its own, and a folder can fail as a unit — the server lists it, but opening or
searching it raises an error. Some servers advertise folders through `LIST`/`LSUB` that then answer
`NO ... doesn't exist` on `EXAMINE`.

These are counted separately, one per folder:

```
Sync completed for account: Example. New: 120, Failed: 0, Deleted: 0, Recovered: 0, Provider placeholders: 0, Failed folders: 2
```

`Failed folders: 2` means two folders could not be synced at all. It does **not** mean any
particular message failed — when a folder cannot be opened, nothing is known about the messages in
it, so no number of failed messages would be truthful.

**Why this is its own counter.** A folder-level failure used to be recorded as failed *messages*,
as many as had been processed in that folder up to that point. That number was invented, and it
overwrote the folder's real per-message failures. One inaccessible folder could therefore report
hundreds of failed messages that were in fact archived perfectly well.

**Effect on `LastSync`, unchanged.** A folder that failed as a whole still prevents `LastSync` from
advancing, exactly as it did when it was being counted as failed messages. Only the reporting has
changed, not the behaviour: the account still retries the same window on the next cycle. This is
deliberate — an incremental sync that moved past a folder it never managed to read would never come
back for the mail in it.

Both counts are visible at `Information` level, and the warning that names them is emitted whenever
`LastSync` is held back:

```
Not updating LastSync for account Example: 0 failed emails, 2 folders that could not be synced at all
```

### Folders that are gone, as opposed to broken

A folder the server lists and then refuses to open is not automatically a failure. The most common
reason is not a fault at all: RFC 3501 forbids a server to remove a name from the subscription list
when the mailbox behind it is deleted, so a folder somebody once subscribed to in an IMAP client is
still reported by `LSUB` years later. Folder discovery takes the union of `LIST` and `LSUB`, so those
names reach the sync, and `EXAMINE` then answers `NO ... doesn't exist`.

That is an answer, not an error. There is nothing to retry and nothing to come back for, so it is
counted separately and does **not** hold `LastSync` back:

```
Sync completed for account: Example. New: 120, Failed: 0, ..., Failed folders: 0, Missing folders: 16
```

Counting it as a failure would stop the account permanently, because a folder that does not exist
never starts existing again. That is exactly what happened on a mailbox with 16 stale subscriptions:
the account stopped advancing `LastSync` and could not recover on its own.

Everything else stays a failure and keeps holding the account back — no permission, a dropped
connection, throttling, a server error. Those can succeed on the next run.

`Missing folders: N` is worth acting on even though it is harmless: it means the mailbox carries
subscriptions to folders that no longer exist. Clearing them is an `UNSUBSCRIBE` per name, done with
any IMAP client, and the sync deliberately does **not** do it — an archiver must not modify the
mailbox it reads.

IMAP only. Microsoft Graph enumerates folders through the API, where a deleted folder is simply not
returned, so the situation cannot arise there.

Applies to both providers, IMAP and M365 (Graph).

---

## 🗓️ How accounts are scheduled

A background tick runs once a minute. It loads the enabled accounts, works out which are due, and
starts as many of them as there are free sync slots: `MailSync:MaxConcurrentSyncs` of them in total.
It does **not** wait for the syncs it starts. Anything that does not fit is left for the next tick,
which re-decides from scratch.

Each account's next run is scheduled when its sync **finishes**, from that moment plus its interval.
An account whose sync takes longer than its interval is therefore due again as soon as it is done,
but never twice at once: an account already running is skipped, not queued.

When more accounts are due than there are slots, the **most overdue goes first**. Ties break by
account id so a backlog is worked through reproducibly.

A finishing sync wakes the scheduler immediately, so the loop does not sit out its minute of idle
time when a slot has already come free: a backlog is worked through back-to-back even with
`MaxConcurrentSyncs: 1`, exactly as the old batch loop did, while the minute remains only the
longest gap between two looks at the account list.

With more than one slot, consider setting `MailSync:TimeoutMinutes`, which is `0` (no timeout) by
default. A sync keeps its slot for as long as it runs, so a single account that stops making
progress permanently reduces the slots available to everything else. A timeout ends such a run as a
pause that keeps its checkpoints, and the slot is free again on the next tick.

> ℹ️ Before this, the tick collected every due account and waited for all of them before looking
> again. One large mailbox therefore held up every other account for as long as it ran: the slots
> came free, but nothing was re-evaluated until the last one returned, so an installation with
> `MaxConcurrentSyncs: 10` and one six-hour mailbox effectively synced everything on a six-hour
> interval. Accounts that finish quickly now keep to their own interval regardless of what else is
> running.

On shutdown the service waits up to 30 seconds for syncs still in flight rather than tearing the
process down underneath an open IMAP session. They are not cancelled; see
[Stopping a Sync Early](#-stopping-a-sync-early) for what does stop a sync.

---

## ⏹️ Stopping a Sync Early

A running sync stops before it has worked through every folder for exactly two reasons: somebody
cancels the job on the **Jobs** page, or the account's `MailSync:TimeoutMinutes` elapses. Both are
checked at three points: before each folder, before each batch, and before every single message,
so neither has to wait for a large folder to finish.

The timeout applies to **scheduled syncs only**. A sync started from the UI, the "Sync now" action
or a full resync, runs without a timeout, so a long manual sync is never cut short.

The two end the job differently, and the difference matters:

| | Job status | `LastSync` | Checkpoints | Retention passes |
|---|---|---|---|---|
| Cancelled from the UI | `Failed`, with "Job was cancelled" | not advanced | kept | skipped |
| Sync timeout elapsed | `Timed Out` | not advanced | kept | skipped |

A timeout is a **pause, not a failure**. The sync stops where it is, keeps what it has archived,
leaves `LastSync` untouched and skips the server-side and local retention passes, because running
those would defeat the point of bounding the runtime. The next scheduled run resumes from the checkpoint.

### Checkpoints

Progress is recorded per account and folder in `mail_archiver.SyncCheckpoints`: the UID of the last
message archived in that folder, together with the folder's UIDVALIDITY at that moment. Every
installation writes them; they used to be tied to `BandwidthTracking:Enabled`, which meant an
interrupted sync could only resume where that unrelated feature happened to be switched on. The
checkpoints are dropped again as soon as an account completes without failures.

A resumed folder runs **exactly the same search** it would have run without a checkpoint. Only the
UIDs at or below the watermark are dropped from the result afterwards, so the search window never
moves and no message can fall out of it.

The watermark stops advancing as soon as a message in that folder fails. Messages are walked in
ascending UID order, so a watermark above a failed UID would tell the next run that the failed
message is already archived and it would never be retried, which is exactly what the held-back
`LastSync` exists to force. Everything above the failure is therefore read again next time; that
costs a re-read and the duplicate check absorbs it.

The checkpoint is ignored, and the folder read in full, whenever it cannot be proven to apply:
no UID recorded yet, no UIDVALIDITY recorded, or a UIDVALIDITY that no longer matches the folder.
The last case means the server renumbered the mailbox, so the stored UID names a different message.
Re-reading a folder costs time and is absorbed by the duplicate check; skipping one would lose mail
silently, so every doubtful case reads in full.

The Graph API provider does not write UID checkpoints, because the Graph API has no equivalent of
IMAP's UIDs. A timed-out or bandwidth-limited M365 sync "resumes" only in the sense that `LastSync` is not
advanced, so the next run re-reads the whole window and the duplicate check absorbs the overlap.

> ℹ️ Before this, the checkpoint stored the **Date header** of the last archived message and fed it
> into the folder search, which the server answers by INTERNALDATE, a different clock that can be
> years off on migrated mail, while messages are processed in UID order rather than date order.
> Both mismatches could move the search window past mail that had never been archived. The same path
> is used when a sync pauses on a bandwidth limit, so that resume is fixed by this change too.

### Large mailboxes and the timeout

A first full sync of a mailbox with 150,000 messages runs for hours, and a timeout shorter than
that would stop it every time. It resumes from its checkpoint rather than starting over, but the
account will not reach a completed state, and therefore will not advance `LastSync`, until one run
gets all the way through. Review the value for your mailboxes, and set it to `0` if you would
rather have no timeout at all.

---

## 👀 Observing the Sync

### The account page answers "what did the last run do"

Every mail account carries a **Last Sync Run** card showing the run that finished most recently: when
it ended, how long it took, how many messages it processed and how many were new. It survives the
24-hour job retention, so a mailbox that has been quiet all day still has an answer — only a restart
clears it, and the card says so rather than pretending the account is fine.

When something went wrong the card lists it, grouped and budgeted per kind:

- **Folders that could not be read** — the folder failed as a unit. Holds `LastSync` back.
- **Folders the server does not have** — reported by discovery, denied on open. Usually left-over
  subscriptions to deleted folders; does not hold `LastSync` back. See
  [Folders that are gone](#folders-that-are-gone-as-opposed-to-broken).
- **Messages that could not be archived** — with folder, subject, UID and the server's own wording.

Each group is capped by `MailSync:MaxIssuesPerKind` and reports what did not fit as "and N more", so
a flood of one kind cannot bury the others. When `LastSync` was held back the card says that too, in
one sentence, because that is the question a stale timestamp actually raises.

The account list marks accounts whose last run reported problems, next to the timestamp. Deliberately
only those: a tick on every row would be noise and would defeat the point of the marker.

- **Account Details page**: Shows the current `LastSync` timestamp and the active sync job (folder, processed count, new count, failed count). The **Full Resync** button is located here.
- **Logs**: Sync progress is logged at `Information` level. In Docker:
  ```bash
  docker compose logs -f mailarchive-app | grep -i sync
  ```
  See [Docker Compose Logs Guide](DockerComposeLogs.md) for log filtering tips.
- **Rate limiting**: When a sync is paused due to bandwidth limits, the account shows "Rate-Limited" status and resumes automatically after the reset window. See [Rate Limit Handling](RateLimitHandling.md).
- **Timeouts**: A sync stopped by `MailSync:TimeoutMinutes` shows "Timed Out" and resumes from its checkpoint on the next run. See [Stopping a Sync Early](#-stopping-a-sync-early).

---
