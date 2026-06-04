# 🧬 Attachment Deduplication

[← Back to Documentation Index](Index.md)

## 📋 Overview

Mail attachments are stored as binary payloads in the database. The **same file**
is very often attached to many e-mails (company logos in signatures, forwarded
PDFs, newsletters, contracts, ...). Without deduplication, every attachment keeps
its own private copy of those bytes, which wastes a large amount of database space.

**Attachment deduplication** stores every *unique* payload exactly once in a
content-addressed table (keyed by the SHA-256 hash of the bytes) and lets all
identical attachments reference that single shared copy. This typically reduces the
storage footprint of attachment-heavy archives significantly.

## ⚙️ How it works

### New attachments
Whenever a new e-mail is archived (IMAP, Microsoft 365 / Graph, EML import, MBOX
import, ...) the attachment payload is hashed and stored once. If the same payload
already exists, the new attachment simply references the existing content row. This
happens transparently at the data layer, so every archiving path is covered
automatically.

### Existing data (migration)
For installations that already contain attachments archived **before** this feature
was introduced, a **robust, resumable background migration** converts the existing
inline payloads into shared content rows:

- It processes attachments **batch by batch**, each batch in its own transaction.
- Progress is persisted in a dedicated state table, so the migration **survives an
  application restart** and simply continues where it left off.
- It is **idempotent** – already-migrated attachments are never touched again, even
  after a crash or restart.
- It runs in the background and does not block normal usage of the application.

When the migration finishes it logs a completion message, e.g.:

```
Attachment Deduplication migration COMPLETED. 36199 attachments processed in 1.6 minutes.
```

On the next start it detects there is nothing left to do and stays idle. Should any
un-deduplicated attachments ever remain (for example after restoring an older
backup), it automatically resumes and migrates them — the system always converges
to a fully deduplicated state.

### Garbage collection (always on)
When e-mails (and therefore attachments) are deleted, a shared payload can become
unreferenced. A built-in garbage collection removes these orphaned content rows.

> 🧹 **The orphan cleanup ALWAYS runs**, independent of the
> `DatabaseMaintenance` feature. It runs once right after the migration and then
> periodically (every `OrphanCleanupIntervalHours`, default 12 h). When
> `DatabaseMaintenance__Enabled=true`, the same cleanup additionally runs before the
> daily `VACUUM` so freed space is physically reclaimed.

## 🔧 Configuration

These settings are optional; defaults are sensible for most installations. See also
the [Setup Guide](Setup.md#-attachment-deduplication-settings).

| Setting | Default | Description |
|---------|---------|-------------|
| `AttachmentDeduplication__BatchSize` | `200` | Number of existing attachments migrated per transaction. Larger values migrate faster but increase memory/DB load per batch. |
| `AttachmentDeduplication__DelayBetweenBatchesMs` | `0` | Optional pause (ms) between migration batches to throttle DB load on busy systems. |
| `AttachmentDeduplication__StartupDelaySeconds` | `20` | Delay (s) after app start before the migration begins (lets the schema migration finish first). |
| `AttachmentDeduplication__OrphanCleanupIntervalHours` | `12` | Interval (h) of the always-on orphan garbage collection. |

Example (`docker-compose.yml`):

```yaml
environment:
  - AttachmentDeduplication__BatchSize=200
  - AttachmentDeduplication__DelayBetweenBatchesMs=0
  - AttachmentDeduplication__StartupDelaySeconds=20
  - AttachmentDeduplication__OrphanCleanupIntervalHours=12
```

## ❓ FAQ

**Can I disable deduplication?**
No. It is a core storage feature and is always active. You can only tune the batch
size and scheduling parameters above.

**Is my data safe during the migration?**
Yes. Each batch is a single transaction and the work is idempotent. A crash or
restart mid-migration rolls back the unfinished batch and it is reprocessed on the
next start; no data is lost or duplicated.

**Does it slow down archiving?**
The per-attachment hashing overhead is negligible. The one-time migration of
existing data runs in the background and can be throttled with
`DelayBetweenBatchesMs` if needed.

**How much space will I save?**
That depends entirely on how many duplicate attachments your archive contains.
Archives with many repeated signatures, logos or forwarded files benefit the most.

---