## ✨ New Features
- **Complete Message Download & Original Message Storage** - Download any archived email as a complete `.eml` through the REST API (`GET /emails/{id}/eml`) and the MCP server (`get_email_eml`). With `RawMessages:Enabled` (off by default) the original message is stored as received and returned byte for byte by `GET /emails/{id}/raw` and `get_email_raw`, including its SHA-256 and capture source — for spam analysis, DKIM/SPF/ARC checks and forensics.
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
- **Leftover Client Secret on Provider Switch** - Switching an account to Microsoft Personal (MSA) now drops a stored client secret instead of sending it with every token refresh, e.g. after switching from Microsoft 365 or when falling back to the shared default client ID.
- **Mailcow Link Retry** - A failed account link leaves the account in a pending (disabled) state and can be retried; previously a retry silently reported success while the account could not authenticate.
- **Mailcow Instance Deletion** - Deleting an instance no longer wipes credentials and disables accounts of revoked bindings that an admin had re-configured manually.
- **Export of Emails with Non-Standard Addresses** - Emails whose stored sender or recipient is not a valid email address (e.g. an Exchange X.500 DN such as `/O=EXCHANGELABS/.../CN=RECIPIENTS/CN=...`) are no longer skipped during EML/MBox export. The original value is preserved in an `X-MailArchiver-Original-From`/`-To`/`-Cc`/`-Bcc` header, and valid addresses next to it are kept.
- **Restore Keeps Original Addresses** - Restoring to IMAP no longer replaces an unparsable sender or recipient with a placeholder address (`sender@example.com`/`recipient@example.com`); the original value is preserved in a header instead.
