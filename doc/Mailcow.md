# Mailcow instance archiving

Administrators can open **Mail Accounts → Mailcow instances**, connect a Mailcow
instance, and select its mailboxes. No personal mailbox passwords are needed.

## Prerequisites

- Mailcow API access with permission to list mailboxes and list, create and delete
  app passwords. Configure Mailcow's API source IP restrictions for the archiver.
- HTTPS access to the Mailcow origin and verified IMAP TLS access (normally port
  993). Configure the environment's network policy for the actual API hostname.
- Enable the existing credential encryption feature using
  `Security__CredentialEncryptionKey` (a base64-encoded 32-byte key). Keep this key
  outside the database and include it in your secure backup process. Import is
  refused if encryption is disabled, so API keys and generated passwords cannot
  be newly stored as plaintext through this workflow.

Enter the HTTPS origin (for example `https://mail.example.com`), API key and IMAP
hostname. The API key remains server-side. Redirects are disabled to prevent
forwarding credentials to another origin. The connection is restricted to
administrators because the API key can manage credentials for multiple users.

## Selecting mailboxes

Use search and **Select all** to select the visible, available mailboxes, then
activate archiving. The server validates selections against Mailcow's current
mailbox list and ignores disabled mailboxes. Each selected mailbox receives a
random app password named `MailArchiver-<unique id>` with only `imap_access`.
The archiver verifies the password using IMAP over TLS before enabling sync.
Mail is then archived using the existing IMAP provider. Existing independent IMAP
accounts are not overwritten or duplicated. The maximum selection size is
configurable with `Mailcow__MaxSelectedMailboxes` (default `1000`); see
[Setup](Setup.md#mailcow-settings).

IMAP permission is not intrinsically read-only. New accounts have no server-side
deletion policy configured; administrators retain control of the existing account
settings. This feature archives messages, not Mailcow configuration or backups.

## Retry and revoke

The credential and unique app name are persisted before calling Mailcow. On an
ambiguous API failure, reload the list and retry the pending mailbox: the archiver
looks up its app name before creating another password. Provisioning and revocation
for one instance are serialized across application processes using PostgreSQL
advisory locks. Pending accounts remain disabled until IMAP verification succeeds.

**Revoke app password** disables future synchronization, removes the managed app
password from Mailcow, and clears the local password. Existing archived mail is
retained. If the remote request fails, the account remains disabled and revocation
can be retried. Already running operations may finish using their existing connection.
Removing an archive account alone does not silently delete remote credentials:
its managed access remains listed in the instance view for explicit revocation.
Other app passwords belonging to the mailbox are never removed.

Mailcow's app-password responses can echo passwords in their `log` fields. This
integration does not log response bodies or return API secrets to the browser.

## API endpoints

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

An HTTP 200 response is not sufficient: mutations require Mailcow's per-result
`type: success`. The migration adds only `MailcowInstances` and `MailcowMailboxes`
and their indexes/foreign keys; existing mailbox tables and data are retained.
