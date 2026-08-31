# 🧪 Local Test Environment

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide describes how to run the test suite, the application, and an IMAP target on a
development machine. It is aimed at contributors: the stack it brings up is deliberately
insecure and is not a deployment guide. For deployment see the
[Setup Guide](Setup.md).

Three pieces are involved:

| Piece | Provided by | Purpose |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | Two databases, one for `dotnet test` and one for the application |
| Dovecot | `docker-compose.test.yml` | An IMAP target standing in for a mailcow mailbox |
| The application | `dotnet run` on the host | Faster to iterate than rebuilding the container image |

## 🚀 Starting the stack

```bash
docker compose -f docker-compose.test.yml up -d
```

This publishes everything on loopback only:

| Service | Host address | Notes |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5433, not 5432, so it does not clash with a local server |
| Dovecot, plain | `127.0.0.1:1143` | Plaintext authentication is enabled |
| Dovecot, TLS | `127.0.0.1:1993` | The image's self-signed certificate |

To stop it, and to discard all databases and stored mail:

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ The two databases

The test suite leaves fixture rows behind: after a full run the test database holds several
dozen mail accounts and users. If the application shared that database, its background sync
would pick those fixtures up and repeatedly try to reach servers that do not exist. So the
postgres container creates two databases on first initialisation
(`tests/postgres/10-create-dev-db.sql`):

| Database | Used by | Configured in |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

Both configuration files are gitignored and have to be created locally.

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

`appsettings.Development.json` in the repository root:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` matters outside the container: it defaults to the absolute path
`/app/DataProtection-Keys`, which does not exist on a development machine, and without it every
request fails while reading the key ring. `IgnoreSelfSignedCert` is what lets the application
accept the Dovecot certificate on port 1993.

## ✅ Running the tests

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

Migrations are applied automatically by `TestDbFixture` on the first run. Most tests need
no database at all; the rest use PostgreSQL, so they fail with
`Name or service not known` if the stack is not running.

A connection string can also be supplied through the environment, which overrides every
configuration file:

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ Running the application

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` already sets `ASPNETCORE_ENVIRONMENT=Development` for
`dotnet run`, so a plain `dotnet run --project MailArchiver.csproj` works too and serves
http://localhost:5179 (the `http` profile). `ASPNETCORE_URLS` above only picks the container
port 5000 instead.

Pending migrations are applied to `MailArchiverDev` at startup. Sign in with the credentials from `appsettings.json`
(`Authentication:Username` and `Authentication:Password`).

## 📬 Using the Dovecot target

Dovecot accepts **any username** with the password `pass`, so a new target mailbox needs no
provisioning: pick a name and log in. Add it under Mail Accounts with:

| Field | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | anything, e.g. `offload-target@example.com` | same |
| Password | `pass` | same |

The configuration in `tests/dovecot/dovecot.conf` is modelled on the image's own default, with
three deliberate differences that make it behave like a mailcow target:

- **Maildir storage** instead of the image's sdbox, which is what mailcow uses.
- **`separator = /`**, matching mailcow. Folder paths are split on `/` and `\` when a restore
  recreates a folder hierarchy, so a server using `.` would behave differently.
- **The special-use folders `Drafts`, `Junk`, `Sent` and `Trash` are pre-created**, as mailcow
  pre-creates them. This matters when testing a migration from Exchange: a source folder named
  `Sent Items` lands next to the existing `Sent` rather than merging with it, which is the
  situation folder renaming has to deal with.

Plaintext authentication is enabled on port 1143 because the application connects without any
transport security when an account has SSL disabled.

## 🌱 Seeding an archive to test against

An empty archive is not much use for testing a restore or a migration, so
`tests/seed/seed.sh` builds one:

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```

It creates two accounts, imports the fixtures, and prints what landed. Both accounts are
created with a per-account sync interval of a year, and the source is additionally disabled,
so the background sync service ignores it: its server does not exist, and the Dovecot target
only ever needs to be appended to. One caveat: the scheduling state lives in memory, so every
application restart schedules the target once, immediately. The target therefore also
excludes the Dovecot special-use folders (`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`), so that
one restart sync has nothing to archive back. Running the script twice is harmless;
the second run reports every message as already present.

The fixtures are checked in under `tests/seed/fixtures`, one mbox per source folder, with
`manifest.tsv` mapping each file to its target folder. They are imported through the
application's own `--import-mbox` CLI, and that detail is deliberate: the CLI routes through
`MailImporter.ImportEmailToDatabase`, the same importer the IMAP sync uses, so the rows land in
exactly the format a real archive holds -- addresses joined with `", "`, subjects passed through
`CleanText`, `SentDate` converted to the display timezone, `RawHeaders` extracted and truncated.
Inserting rows straight into the database would skip all of that and produce fixtures that
quietly differ from production data.

Every fixture message carries an `X-Fixture-Purpose` header saying why it exists, so the files
document themselves:

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```

Dates are fixed rather than relative to the current day. A migration cutoff is resolved to an
absolute date before a job runs, so a fixed fixture set and a fixed cutoff test the same thing
more predictably than a sliding one. The messages span 2012 to 2026.

The folder layout mirrors an Exchange mailbox, and each folder exists to exercise something:

| Source folder | Why it is there |
|---|---|
| `INBOX` | dates spanning fifteen years, plus the edge-case messages below |
| `Sent Items` | collides with the Dovecot target's existing special-use `Sent` |
| `Sent Items/2019` | a rename has to rewrite the leading path segment only |
| `Sent Items Archive` | shares a prefix as a substring but not on a segment boundary, so it must not be rewritten |
| `Deleted Items` | folder exclusion |
| `Deleted Items/Old` | an exclusion on a parent has to cover descendants |
| `Junk E-Mail` | Exchange's name for a folder Dovecot calls `Junk` |
| `Archive/Projects/2020` | three levels of nesting |

The individual messages cover a message with no `Message-ID` header (which gets the deterministic
fallback), one whose `Message-ID` has no `@` (which does not survive a restore intact), a subject
containing a control character, a message with a three-hop `Received` chain and one with none,
several with two `To` recipients, and two near-duplicate pairs one second apart.

### A note on the exit code

`tests/seed/seed.sh` checks the failed and malformed counts from the import output rather than
the process exit code. `MBoxImportService` reports `CompletedWithErrors` when it skipped
duplicates, and the CLI maps every status other than `Completed` to exit 1, so re-running an
import exits non-zero even when nothing went wrong. Re-running the seed script is therefore safe
and reports every message as already present.

## ⚠️ Not for production

- Any username authenticates against Dovecot with a single fixed password.
- Plaintext authentication is enabled.
- Database credentials are the documented defaults.
- Mail and database state live in Docker volumes that `down -v` deletes.

## 🔧 Troubleshooting

**Tests fail with `Name or service not known`** — the stack is not running, or
`appsettings.Test.json` is missing so the connection string falls back to the container default
`Host=postgres`.

**`NETSDK1226` on build** — incomplete workload data in the local .NET SDK, unrelated to this
repository. Either run `dotnet workload repair`, or build with
`-p:AllowMissingPrunePackageData=true`.

**Dovecot restarts in a loop** — a configuration error. `docker compose -f
docker-compose.test.yml logs dovecot` reports the offending line. Note that Dovecot does not
accept several settings on one line inside a block.

**The application returns HTTP 500 on every request** — usually the data protection key ring.
Check that `DataProtection:KeyPath` points at a writable directory.
