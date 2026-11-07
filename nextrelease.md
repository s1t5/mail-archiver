## ⚙️ Improvements
- **Original Message Storage Has Its Own Documentation Page** - The feature now has a dedicated page (`doc/RawMessages.md`) covering where the stored original is served, the postgres and filesystem backends, moving existing messages between them with `--migrate-raw-storage`, verification and backup. `Setup.md` keeps only the parameter table and links to it.

## 🐛 Bug Fixes
- **Deleting an Email Removes Its Stored Original** - With the `filesystem` backend the original message file survived every deletion. The metadata row went with the email through the foreign key cascade, but the file lives outside the database where no cascade can reach it. Retention rules, manual single and bulk deletion, and mail account deletion now remove the file as part of the same operation, after the email row is committed so a failed deletion cannot destroy an original. Files already orphaned by earlier deletions are reclaimed by a new periodic cleanup, so the disk space comes back.
