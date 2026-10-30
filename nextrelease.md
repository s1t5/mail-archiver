## ✨ New Features
- **MP4 Attachment Preview** - MP4 and M4V attachments can be played in the attachment preview dialog, including seeking
- **Mailcow Integration** - Connect Mailcow instances and provision dedicated IMAP app passwords for selected mailboxes, including retry and revocation.
- **Chinese Language Support** - Complete Simplified Chinese translation with automatic `zh`/`zh-Hans` browser detection. Thanks to @wolfbolin
- **Brazilian Portuguese Language Support** - Complete Brazilian Portuguese translation, selectable in the language menu. Thanks to @mvldebian
- **Japanese Language Support** - Complete Japanese translation, selectable in the language menu. Thanks to @ikawa-ohno

## ⚙️ Improvements
- **Mailcow Provider Entry Points** - Select Mailcow during account creation or link an existing IMAP account from the edit view; unlink it again by choosing another provider.
- **Mailcow Logging** - Provisioning, revocation and instance deletion now log state transitions; all Mailcow failures (API, IMAP verification, linking, detaching) are logged with reason and context, without ever exposing API keys, app passwords or Mailcow response bodies.
- **Mailcow Delete Semantics** - Deleting a Mailcow instance keeps archive accounts and their state untouched; accounts of revoked bindings that were deliberately re-configured keep their working credentials.

## 🐛 Bug Fixes
- **Mailcow Link Retry** - A failed account link leaves the account in a pending (disabled) state and can be retried; previously a retry silently reported success while the account could not authenticate.
- **Mailcow Instance Deletion** - Deleting an instance no longer wipes credentials and disables accounts of revoked bindings that an admin had re-configured manually.
