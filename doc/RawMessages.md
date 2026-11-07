# 📧 Original Message Storage

[← Back to Documentation Index](index.md)

## 📋 Overview

By default the archive keeps the **parsed parts** of an email: headers, text and HTML
bodies, attachments. An exported `.eml` is rebuilt from those fields.

That is enough to read an email, but it is not the message **as received**. Transfer
encodings, MIME boundaries and header folding differ from the original, so a rebuilt
message no longer verifies its DKIM signature and spam scanners score it differently.

With original message storage enabled, the archive keeps the complete message as it
arrived, alongside the parsed fields. The stored original is what the archive can prove
was delivered.

| | Parsed fields only | With stored original |
| --- | --- | --- |
| Headers, body, attachments | ✅ | ✅ |
| Byte-exact message | ❌ | ✅ |
| DKIM signature verifies | ❌ | ✅ |
| Forensic use (court, audit) | limited | ✅ |

## 🎯 Where the original is served

Once stored, the original is returned wherever a complete message is expected:

| Place | What happens |
| --- | --- |
| **Export EML** button on the email detail page | Serves the stored original when one exists, falls back to the reconstruction for emails archived without one. |
| **Restore to a mailbox** | Appends the stored original when one exists, falls back to the reconstruction for emails archived without one. |
| `GET /api/v1/emails/{id}/raw` | REST API download, see the [REST API guide](API.md#download-original-message). |
| `get_email_raw` | MCP tool, see the [MCP guide](MCP.md#get_email_raw). |

The response tells you which form you got, so a caller can tell a genuine original from
a reconstruction without inspecting the bytes:

| Signal | Meaning |
| --- | --- |
| `X-Raw-Message-Source: imap` | Exact copy from the IMAP server (`BODY[]` as delivered). |
| `X-Raw-Message-Source: graph` | Exact copy, the MIME content Exchange returned. |
| `X-Raw-Message-Source: eml-file` | Exact copy, the content of the imported `.eml` file. |
| `X-Raw-Message-Source: serialized` | Parsed message written back out. Identical except line endings are CRLF (MBox import, IMAP recovery fetch). |
| `X-Raw-Message-Sha256` | SHA-256 of the stored original. |
| `X-Raw-Message-Reconstructed: true` | No original stored; the message was rebuilt. No checksum header. |
| `X-Raw-Message-Storage` | Backend the original was read from (`postgres` or `filesystem`). |

The filename says it too: `email-{id}-original-...eml` for a stored original,
`email-{id}-...eml` for a reconstruction.

When a stored original **fails its checksum**, the export and the restore are refused
with an error message. Returning the reconstruction instead would hide that the archived
copy was altered, which is exactly what an archive must never do.

A restore reads the stored original and appends it through the provider's client, so the
mailbox receives the original headers and MIME structure. The exact byte stream is not
guaranteed on the wire because the client serializes the message again; the archived
copy itself is unchanged.

## ⚙️ Settings

All settings live in the `RawMessages` section, or as environment variables with the
`RawMessages__` prefix.

| Setting | Default | Description |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Store the original message of every newly archived email. Emails archived before switching it on have no original. |
| `RawMessages:MaxMessageSizeMb` | `50` | Messages larger than this are archived without their original. `0` = no limit. |
| `RawMessages:IncludeGraph` | `true` | Also store the original for Microsoft 365 (Graph) accounts. Costs one additional Graph request per message, which counts against the throttling budget. |
| `RawMessages:Storage` | `postgres` | Where the message bytes are kept: `postgres` (in the database) or `filesystem` (gzip file next to the archive). See [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Directory the message files are written to, relative to the application's content root. Point this at a mounted volume in Docker. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Number of two-character directory levels the email id is spread over. `2` gives 10,000 directories, `0` is flat. Keeps the per-directory file count low on large archives. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Re-compute the SHA-256 of the file on every download and compare it with the checksum recorded at archive time. A mismatch fails the download with HTTP 500 instead of serving altered bytes. |

### Sizing

Storage grows by roughly the size of each mail including its attachments,
gzip-compressed. Attachments are **not** deduplicated inside the stored original, so an
email with a large attachment costs that attachment twice: once in the deduplicated
attachment store and once inside the original. Plan for roughly **twice** the database
size of an archive without originals.

Enabling the setting does not backfill existing mail. Only newly archived emails get an
original.

## 💾 Storage backend

The `Storage` setting decides where the bytes live. The **metadata row always stays in
the database**, whichever backend is configured, and holds the SHA-256, size, capture
source and (for files) the relative path. That row is what lets the archive prove what a
stored message should contain.

### postgres (default)

The compressed message sits in the `Content` column of `mail_archiver."RawMessages"`.
This is the behaviour the archive always had and needs no extra volume.

Choose this when the database is already your backup unit and you want one thing to back
up and restore.

### filesystem

The message is written to `{BasePath}/{xx}/{yy}/{id}.eml.gz`, where `xx`/`yy` are derived
from the email id. The database row keeps only the metadata.

Choose this when the archive is large enough that originals would dominate the database
size, or when you want to put the message files on cheaper or separate storage.

!!! danger "Mount a volume in Docker"
    The default `BasePath` (`RawMessages`) resolves to `/app/RawMessages` **inside the
    container**. Without a bind mount that directory belongs to the container's writable
    layer, so every recreated container - including `docker compose down && docker compose
    up -d`, an image update, or a host reboot with an ephemeral layer - takes every stored
    original with it while the database rows survive.

    Always mount the directory to the host when using the filesystem backend:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    The host path (`./raw-messages` here) is what you back up. The container path must
    match `RawMessages:FileSystem:BasePath`, or set `RawMessages:FileSystem:BasePath` to
    the mount point you chose. If you move the mount, keep `BasePath` pointing at it: the
    stored `StoragePath` values are relative, so they keep resolving.

### Switching backends

Reads follow the storage recorded on each row, not the configured setting. After you
change `Storage`, messages archived earlier stay downloadable from wherever they are.
The setting only affects **newly** archived mail.

To move what is already stored, run the storage migration. See the
[CLI commands](CLI.md#moving-stored-original-messages) for the full reference:

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

What the migration guarantees:

- Every message is verified against its recorded checksum before it is copied. A message
  that fails is left where it is and reported: a corrupt original is never propagated and
  its only remaining copy is never deleted.
- A file is deleted only after the database transaction that moved it has committed, so an
  interrupted run cannot lose the only copy.
- The run is resumable. Work is selected by the `StorageType` column, so starting the
  command again continues with what is still outstanding.
- The exit code is `1` if any message failed, `0` otherwise.

A migration is not required for the archive to work. It is only needed to move existing
bytes to the other backend.

## 🛟 Backup and restore

=== "postgres backend"
    Nothing extra. The originals are part of the database and are covered by the normal
    dump, see [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Back up the `BasePath` directory **together with** the database dump.

    A database restore without the files leaves the archive with rows whose content
    cannot be served. The download then answers HTTP 404 rather than returning something
    wrong, and the emails themselves remain readable from their parsed fields.

## 🗑️ Deleting an email

Deleting an email removes its original with it: retention rules, the manual single and
bulk deletion, and the deletion of a whole mail account all remove the file as part of the
same operation. With the `postgres` backend there is nothing extra to do, because the bytes
travel inside the row the cascade already removes.

The file is deleted **after** the email row is committed, never before. The two steps cannot
be one transaction, because a file is not part of the database transaction, so one of them
has to come second. This order is the safe one: a deletion that fails or rolls back, for
example on a locked record, leaves the original untouched, and a crash between the two steps
leaves a file without a row, which the sweep below reclaims. The other order would destroy
the original of an email that still exists.

**Orphaned files are collected.** Anything that slipped through, including files left by
deletions from before this cleanup existed, is removed by a periodic sweep of `BasePath`
that compares every file against the rows and deletes the ones no email refers to:

| Setting | Default | Meaning |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Turn the sweep on or off |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | How often it runs |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Files younger than this are never touched |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Delay before the first run, so a pending storage migration finishes first |

The grace period is what makes the sweep safe: an import writes a file before it commits
the row that points at it, and a file written moments ago must not be mistaken for an
orphan. Half-written `.tmp` files from an interrupted write are collected under the same
rule. With the `postgres` backend the sweep does nothing and does not even walk the
directory.

## 🔍 Verifying an archive

The checksum recorded at archive time is what makes verification possible.

- **On download** (`VerifyChecksumOnRead=true`, the default) the file is hashed and
  compared. A mismatch refuses the download with HTTP 500.
- **In the storage migration** every message is verified before it is copied, in both
  directions.

Because the metadata row is in the database and the bytes may be on disk, a checksum
comparison is the only way to notice that a file was changed outside the application.

## ⚠️ Things to know

- **Enabling does not backfill.** Only mail archived after the switch gets an original.
- **The reconstruction is not the message.** For emails without a stored original, every
  download path returns a rebuilt message and says so via
  `X-Raw-Message-Reconstructed: true`.
- **Filesystem means two backup units.** Database and message directory must be restored
  together, otherwise the rows survive without their content.
- **`VerifyChecksumOnRead` costs CPU.** Every download of a large message is decompressed
  and hashed. Leave it on unless you have measured a problem; it is the only protection
  against silent alteration on disk.
- **The stored original is not deduplicated.** An attachment that appears in many emails
  is stored once in the attachment store, but once per email inside the original.
- **Deleting removes the original, too.** An email deleted by retention or by hand takes
  its stored original with it, file and row. Nothing is retained that the archive no
  longer lists.

## 🔗 See also

- [CLI Commands](CLI.md#moving-stored-original-messages) - the storage migration in full
- [Setup and Parameters](Setup.md) - all configuration parameters
- [REST API](API.md#download-original-message) - the `/raw` endpoint
- [MCP Server](MCP.md#get_email_raw) - the `get_email_raw` tool
- [Backup and Restore](BackupRestore.md) - what to back up
- [Attachment Deduplication](AttachmentDeduplication.md) - how attachments are stored
