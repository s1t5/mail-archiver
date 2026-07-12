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
5. On **successful completion** (no failed messages, no rate-limit hit), `LastSync` is set to `DateTime.UtcNow` and the next cycle starts from that point.
6. If **any message failed** to process, `LastSync` is **not** updated, so the next cycle re-attempts the same window.
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
- `LastSync` is updated to `DateTime.UtcNow` only after a Full Sync completes without failed messages, exactly like a Quick Sync.

### When to use a manual Full Sync

Use the **Full Resync** button when:

- You changed the account's **excluded folders** list and want to confirm the archive matches the current selection.
- You suspect the archive is missing messages (e.g. after a server migration, a provider outage, or a clock-skew incident).
- You migrated the mailbox to a different backend and want to verify completeness.
- You want to re-detect messages that were moved between folders on the server.

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
| `MailSync:TimeoutMinutes` | `120` | Per-account sync timeout. If an account takes longer, its sync is cancelled and retried next cycle. |
| `MailSync:ConnectionTimeoutSeconds` | `300` | IMAP connection timeout. |
| `MailSync:CommandTimeoutSeconds` | `600` | IMAP command timeout. |
| `MailSync:AlwaysForceFullSync` | `false` | When `true`, every cycle is a Full Sync for all accounts. **Diagnostics only – keep off in production.** |
| `MailSync:IgnoreSelfSignedCert` | `false` | Accept self-signed TLS certificates for IMAP connections. |
| `MailSync:MaxConcurrentSyncs` | `1` | Maximum number of account syncs that may run in parallel within one poll cycle. `1` reproduces the previous sequential behaviour; increase to parallelize — mind provider rate limits and local resource usage. |
| `MailSync:InterAccountDelaySeconds` | `0` | Optional stagger delay in seconds applied at the end of each account sync task. Useful to avoid burst-starts when `MaxConcurrentSyncs > 1`. `0` disables it. |

> 💡 Both the normal sync interval and the full-sync interval can be overridden per account on the **Create/Edit Mail Account** page. Leave the per-account fields empty to fall back to the global defaults above. To remove an account from the scheduler entirely, disable it (toggle *Enabled* off on the Account Details page).

---

## 🗑️ Server-Side Deletion During Sync

If an account has `DeleteAfterDays` configured (> 0), Mail Archiver deletes messages older than the configured threshold from the **mail server** after each sync:

- **IMAP**: `SearchQuery.SentBefore(now − DeleteAfterDays)` per folder, then expunge.
- **M365 (Graph)**: `receivedDateTime lt (now − DeleteAfterDays)` per folder, then delete.

The archived copies in Mail Archiver are **not** affected by this – only the server-side mailbox is trimmed. See [Retention Policies](RetentionPolicies.md) for the local retention counterpart that controls how long archived copies are kept.

---

## 👀 Observing the Sync

- **Account Details page**: Shows the current `LastSync` timestamp and the active sync job (folder, processed count, new count, failed count). The **Full Resync** button is located here.
- **Logs**: Sync progress is logged at `Information` level. In Docker:
  ```bash
  docker compose logs -f mailarchive-app | grep -i sync
  ```
  See [Docker Compose Logs Guide](DockerComposeLogs.md) for log filtering tips.
- **Rate limiting**: When a sync is paused due to bandwidth limits, the account shows "Rate-Limited" status and resumes automatically after the reset window. See [Rate Limit Handling](RateLimitHandling.md).

---
