#!/usr/bin/env bash
#
# Seed the local development archive with an Exchange-shaped source mailbox, and
# register the local Dovecot instance as a restore/offload target.
#
# The fixtures in tests/seed/fixtures are imported with the application's own
# --import-mbox CLI, so the rows land in exactly the format the IMAP sync would have
# produced. Seeding the database directly would bypass MailImporter and could store,
# for example, addresses joined differently from how the importer joins them.
#
# Each fixture message carries an X-Fixture-Purpose header explaining why it is there.
#
# Requires the local test stack:  docker compose -f docker-compose.test.yml up -d
# And the schema to exist, which the application creates on first start:
#   ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
#     dotnet run --project MailArchiver.csproj
#
# Usage:
#   tests/seed/seed.sh [--reset] [--db NAME]
#
#   --reset    delete previously seeded accounts and their mail first
#   --db       database to seed (default: MailArchiverDev)
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

COMPOSE_FILE="docker-compose.test.yml"
DB="MailArchiverDev"
DB_USER="mailuser"
RESET=0
FIXTURE_DIR="$REPO_ROOT/tests/seed/fixtures"

SOURCE_NAME="seed-source-exchange"
SOURCE_EMAIL="alice@source.example"
TARGET_NAME="seed-target-dovecot"
TARGET_EMAIL="offload-target@example.com"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --db)     DB="$2"; shift 2 ;;
    --reset)  RESET=1; shift ;;
    -h|--help) sed -n '2,25p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

psql_q() { docker compose -f "$COMPOSE_FILE" exec -T postgres psql -U "$DB_USER" -d "$DB" -tAc "$1"; }

step() { printf '\n=== %s ===\n' "$1"; }

# --------------------------------------------------------------- preconditions
step "checking preconditions"
if ! docker compose -f "$COMPOSE_FILE" ps --status running --services 2>/dev/null | grep -q postgres; then
  echo "ERROR: the test stack is not running." >&2
  echo "  docker compose -f $COMPOSE_FILE up -d" >&2
  exit 1
fi
echo "test stack: running"

if [[ "$(psql_q "select count(*) from information_schema.tables where table_schema='mail_archiver' and table_name='MailAccounts';")" != "1" ]]; then
  echo "ERROR: database '$DB' has no schema yet. Start the application once so it applies migrations:" >&2
  echo "  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 dotnet run --project MailArchiver.csproj" >&2
  exit 1
fi
echo "schema in $DB: present"

DLL="bin/Debug/net10.0/MailArchiver.dll"
if [[ ! -f "$DLL" ]]; then
  echo "building (dll not found)..."
  dotnet build MailArchiver.csproj -v q --nologo -p:AllowMissingPrunePackageData=true >/dev/null
fi
echo "binary: $DLL"

# --------------------------------------------------------------- optional reset
if [[ $RESET -eq 1 ]]; then
  step "resetting previously seeded data"
  for n in "$SOURCE_NAME" "$TARGET_NAME"; do
    id="$(psql_q "select \"Id\" from mail_archiver.\"MailAccounts\" where \"Name\"='$n';" || true)"
    if [[ -n "$id" ]]; then
      # ArchivedEmails cascade from the account, but delete explicitly so the counts print.
      removed="$(psql_q "with d as (delete from mail_archiver.\"ArchivedEmails\" where \"MailAccountId\"=$id returning 1) select count(*) from d;")"
      psql_q "delete from mail_archiver.\"MailAccounts\" where \"Id\"=$id;" >/dev/null
      echo "removed account '$n' (id $id) and $removed archived mails"
    else
      echo "no existing account '$n'"
    fi
  done
fi

# --------------------------------------------------------------- accounts
# The source is created disabled and import-only: its server does not exist, so the
# background sync service must leave it alone. The target has to be enabled, because the
# offload path, the queued job path and the UI all refuse a disabled target mailbox.
#
# An enabled target would normally be synced, which would archive the mail an offload just
# appended straight back into the archive under the target account. The target therefore gets
# a per-account sync interval of a year. That keeps the background sync service away only
# within one process lifetime: the scheduling state is in-memory, so every application
# restart schedules the target once, immediately (MailSyncBackgroundService initialises
# nextRunUtc to nowUtc for accounts it has not seen). The target's ExcludedFolders therefore
# also list the Dovecot special-use folders, so that one restart sync has nothing to archive.
# Note that IsImportOnly does not help here: the column exists in the schema but is not read
# anywhere in the code.
step "ensuring accounts"
# Only the id goes to stdout; progress goes to stderr. psql prints the command tag
# alongside a RETURNING value, so the id is read back with a separate SELECT instead.
ensure_account() {
  local name="$1" email="$2" server="$3" port="$4" user="$5" pass="$6" ssl="$7" importonly="$8" enabled="${9:-false}" excluded="${10:-}"
  local id
  id="$(psql_q "select \"Id\" from mail_archiver.\"MailAccounts\" where \"Name\"='$name';" || true)"
  if [[ -z "$id" ]]; then
    psql_q "insert into mail_archiver.\"MailAccounts\"
            (\"Name\",\"EmailAddress\",\"ImapServer\",\"ImapPort\",\"Username\",\"Password\",
             \"UseSSL\",\"LastSync\",\"IsEnabled\",\"ExcludedFolders\",\"IsImportOnly\",\"Provider\")
          values ('$name','$email','$server',$port,'$user','$pass',
             $ssl, now(), $enabled, '$excluded', $importonly, 'IMAP');" >/dev/null
    # A year, so the background sync service effectively never picks this account up.
    psql_q "update mail_archiver.\"MailAccounts\" set \"SyncIntervalMinutes\"=525600
            where \"Name\"='$name';" >/dev/null
    id="$(psql_q "select \"Id\" from mail_archiver.\"MailAccounts\" where \"Name\"='$name';")"
    echo "created '$name' -> id $id" >&2
  else
    echo "reusing '$name' -> id $id" >&2
  fi
  printf '%s' "$id"
}

# ExcludedFolders is semicolon-separated (MailAccount.ExcludedFoldersList). The target
# excludes Dovecot's special-use folders so the single sync an application restart
# schedules finds nothing to archive.
SOURCE_ID="$(ensure_account "$SOURCE_NAME" "$SOURCE_EMAIL" "imap.source.invalid" 993 "$SOURCE_EMAIL" "unused" false true  false)"
TARGET_ID="$(ensure_account "$TARGET_NAME" "$TARGET_EMAIL" "localhost"           1143 "$TARGET_EMAIL" "pass"   false false true 'INBOX;Drafts;Junk;Sent;Trash')"

if [[ -z "$SOURCE_ID" || -z "$TARGET_ID" ]]; then
  echo "ERROR: could not resolve account ids (source='$SOURCE_ID' target='$TARGET_ID')" >&2
  exit 1
fi

# --------------------------------------------------------------- fixtures
step "checking fixtures"
if [[ ! -f "$FIXTURE_DIR/manifest.tsv" ]]; then
  echo "ERROR: no manifest at $FIXTURE_DIR/manifest.tsv" >&2
  exit 1
fi
while IFS=$'\t' read -r file folder count; do
  [[ -f "$FIXTURE_DIR/$file" ]] || { echo "ERROR: missing fixture $file" >&2; exit 1; }
done < "$FIXTURE_DIR/manifest.tsv"
# Actual SentDate ranges are reported per folder from the database further down, once
# the importer has parsed and converted them.
printf 'found %s mbox files, %s messages\n' \
  "$(wc -l < "$FIXTURE_DIR/manifest.tsv")" \
  "$(awk -F'\t' '{n+=$3} END {print n}' "$FIXTURE_DIR/manifest.tsv")"

# --------------------------------------------------------------- import
# LocalImport:AllowedPaths defaults to /app/imports and the CLI enforces it, so the
# fixture directory is allowed through the environment rather than by editing
# appsettings.Development.json.
step "importing fixtures into account $SOURCE_ID"
export ASPNETCORE_ENVIRONMENT=Development
export LocalImport__AllowedPaths__0="$FIXTURE_DIR"

# Exit code handling. MBoxImportService.cs:274 sets CompletedWithErrors when
# SkippedAlreadyExistsCount > 0, and Program.cs:669 exits 1 for any status other than
# Completed. So a re-run that correctly skips duplicates -- the expected outcome of
# importing the same file twice -- also exits 1. The real failure signals are the
# failed and malformed counts, so those are what is checked here.
imported=0
dupes_total=0
while IFS=$'\t' read -r file folder count; do
  printf '  %-28s <- %-30s (%2s msg) ... ' "$folder" "$file" "$count"
  set +e
  out="$(dotnet "$DLL" --import-mbox --file "$FIXTURE_DIR/$file" \
           --account-id "$SOURCE_ID" --folder "$folder" 2>&1)"
  rc=$?
  set -e

  ok_line="$(printf '%s\n' "$out" | grep -E '^Imported Successfully:' | head -1)"
  failed="$(printf '%s\n' "$out" | sed -n 's/^Failed: *\([0-9]*\).*/\1/p' | head -1)"
  malformed="$(printf '%s\n' "$out" | sed -n 's/^Skipped (malformed): *\([0-9]*\).*/\1/p' | head -1)"
  dupes="$(printf '%s\n' "$out" | sed -n 's/^Skipped (duplicates): *\([0-9]*\).*/\1/p' | head -1)"
  ok_n="${ok_line##*: }"

  if [[ -z "$ok_line" || "${failed:-1}" != "0" || "${malformed:-1}" != "0" ]]; then
    echo "FAILED (exit $rc)"
    printf '%s\n' "$out" | tail -25 >&2
    exit 1
  fi

  if [[ "${dupes:-0}" != "0" ]]; then
    echo "ok ($ok_n imported, ${dupes} already present)"
  else
    echo "ok ($ok_n imported)"
  fi
  imported=$((imported + 1))
  dupes_total=$((dupes_total + ${dupes:-0}))
done < "$FIXTURE_DIR/manifest.tsv"
echo "$imported mbox files processed, $dupes_total message(s) skipped as already present"

# --------------------------------------------------------------- verification
step "what landed in the archive"
psql_q "select \"FolderName\" || '  |  ' || count(*) || ' mails  |  ' ||
               to_char(min(\"SentDate\"),'YYYY-MM-DD') || ' .. ' ||
               to_char(max(\"SentDate\"),'YYYY-MM-DD')
        from mail_archiver.\"ArchivedEmails\" where \"MailAccountId\"=$SOURCE_ID
        group by \"FolderName\" order by 1;" | sed 's/^/  /'

total="$(psql_q "select count(*) from mail_archiver.\"ArchivedEmails\" where \"MailAccountId\"=$SOURCE_ID;")"
echo "  ----"
echo "  total: $total"

step "edge cases available for testing"
psql_q "select
    'Message-ID without @        : ' || count(*) filter (where \"MessageId\" <> '' and \"MessageId\" not like '%@%') || E'\n' ||
    'deterministic fallback IDs  : ' || count(*) filter (where \"MessageId\" like '%@mail-archiver.local') || E'\n' ||
    'rows with 2+ To recipients  : ' || count(*) filter (where \"To\" like '%,%') || E'\n' ||
    'rows with a Received chain  : ' || count(*) filter (where \"RawHeaders\" ilike '%Received:%')
  from mail_archiver.\"ArchivedEmails\" where \"MailAccountId\"=$SOURCE_ID;" | sed 's/^/  /'

step "import de-duplication, defect 4 made visible"
psql_q "select \"Subject\" || '  ->  ' || count(*) || ' row(s) stored'
        from mail_archiver.\"ArchivedEmails\"
        where \"MailAccountId\"=$SOURCE_ID and \"Subject\" like 'Near duplicate%'
        group by \"Subject\" order by 1;" | sed 's/^/  /'
echo "  Each pair was one second apart with distinct Message-IDs."
echo "  The single-recipient pair collapses to 1 row; the two-recipient pair does not,"
echo "  because the dedup query joins addresses with ',' while the stored column uses ', '."

cat <<SUMMARY

=== ready ===
  source account id : $SOURCE_ID  ($SOURCE_NAME, import-only, sync disabled)
  target account id : $TARGET_ID  ($TARGET_NAME -> localhost:1143, password 'pass')
  fixtures          : $FIXTURE_DIR

The target mailbox already holds Dovecot's special-use folders (Sent, Trash, Junk,
Drafts), so the source folder 'Sent Items' collides with the existing 'Sent' exactly
as it would against mailcow.
SUMMARY
