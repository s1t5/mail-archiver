# Mailcow instance archiving

[← Back to Documentation Index](index.md)

## Overview

Mail Archiver can archive the mailboxes of a [Mailcow](https://mailcow.email) instance
centrally, without collecting the personal passwords of the mailbox owners.

An administrator connects the instance once with its API key. Mail Archiver then lists
the mailboxes and, for each mailbox selected for archiving, creates a **dedicated app
password** restricted to `imap_access`. That app password is used by the existing IMAP
provider to archive the mailbox and can be revoked again at any time.

Mailcow instances are managed under **Mail Accounts → Mailcow instances**. The section is
only visible to administrators, because the stored API key can manage credentials for
every mailbox in the instance.

## Prerequisites

### Credential encryption must be enabled

Mail Archiver stores the Mailcow API key and every generated app password encrypted at
rest using the existing credential encryption feature. Set

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```

before connecting an instance. Creating an instance and activating mailboxes are both
refused while encryption is disabled, so credentials can never be written as plaintext
through this feature. Keep the key outside the database and include it in your secure
backup — without it, stored app passwords cannot be decrypted and mailboxes cannot be
provisioned. See [Setup](Setup.md#-security-settings) for details.

### Mailcow API access

Create an API key in Mailcow under **Configuration → Access → API**. The key must be
allowed to

- list mailboxes (`get/mailbox/all`),
- list, create and delete app passwords (`get/app-passwd/all`, `add/app-passwd`,
  `delete/app-passwd`).

Restrict the API to the IP address of the Mail Archiver host in Mailcow's API settings.
Mailcow's app-password endpoint requires the **App Passwords** feature to be enabled for
the instance.

### Network access

- **HTTPS** to the Mailcow origin (for example `https://mail.example.com`).
- **IMAP over TLS** to the IMAP host, normally port `993`. Plaintext IMAP is not supported
  for provisioning.

## Step-by-step usage

### Connecting an instance

1. Sign in to Mail Archiver as an administrator.
2. Open **Mail Accounts** and click **Mailcow instances**.
3. Click **Connect Mailcow instance**.
4. Enter the connection details:
   - **Account name** — a display name for the instance in Mail Archiver.
   - **Mailcow URL** — the HTTPS origin only, for example `https://mail.example.com`.
     Paths, query strings and embedded credentials are rejected.
   - **API key** — the key created in Mailcow.
   - **IMAP host** — the hostname used for archiving, for example `mail.example.com`.
     Enter the hostname only, without a scheme or port.
   - **IMAP TLS port** — defaults to `993`.
5. Click **Connect**. Mail Archiver verifies the API key by listing the mailboxes before
   saving the instance. On success it opens the mailbox selection.

The API key is stored encrypted and never rendered back into the form or into an error
message. HTTP redirects are disabled for API calls so the key cannot be forwarded to
another origin.

### Activating mailboxes

1. On the mailbox page, optionally filter the list with the search field. The filter
   matches the address and the display name and is case-insensitive.
2. Select the mailboxes to archive. **Select all** and **Clear selection** only affect
   the currently visible (filtered) mailboxes, so filtering is a safe way to pick a
   subset.
3. Click **Activate archiving**.

For each selected mailbox Mail Archiver:

1. re-validates the address against Mailcow's current mailbox list and ignores disabled
   mailboxes,
2. creates an archive account named `{instance name} – {mailbox display name}`,
3. generates a random app password named `MailArchiver-<unique id>` restricted to
   `imap_access`,
4. verifies the credential with an IMAP login over TLS,
5. and only then enables synchronization.

If the IMAP verification fails, the account stays disabled and can be retried.

### Mailbox status badges

The selection page labels each mailbox so it is clear why a checkbox is available or not:

| Badge | Meaning | Selectable |
| --- | --- | --- |
| **Available** | Listed in Mailcow, not yet provisioned | yes |
| **Active** | Provisioned and synchronizing | no |
| **Pending** | Provisioning started but not finished (retryable) | yes |
| **Revoked** | App password was revoked; can be activated again | yes |
| **Existing** | An independent IMAP account with this address already exists | no |
| **Disabled** | The mailbox is disabled in Mailcow | no |

### Retrying a pending mailbox

The generated credential and the unique app password name are persisted **before** the
Mailcow API is called. If a request fails ambiguously — for example a timeout after the
app password was created — the mailbox stays visible as **Pending**. Reload the page and
select it again: Mail Archiver looks up the app name first and only creates a new app
password if none exists. Retrying does not accumulate duplicate credentials.

Provisioning and revocation for one instance are serialized using PostgreSQL advisory
locks, so two concurrent operations cannot provision the same mailbox twice.

### Revoking access

Click **Revoke app password** next to a mailbox. This

- disables future synchronization,
- deletes the managed app password in Mailcow,
- and clears the local password.

**Archived mail is retained.** Only the managed app password is removed — other app
passwords belonging to the mailbox are never touched. Already running synchronization
operations may finish with their existing connection.

If the remote request fails, the account stays disabled and revocation can be retried.

Deleting an archive account in **Mail Accounts** does not silently remove the remote
credential. The binding stays listed under **Remaining app passwords** on the instance
page so it can be revoked explicitly.

### Editing an instance

Open **Mailcow instances** and click **Edit**. The API key field is always empty — the
stored key is never displayed. Leave it empty to keep the current key, or enter a new one
to replace it. Changed connection details are verified against Mailcow before saving.

### Deleting an instance

Open **Mailcow instances** and click **Delete**.

Deleting an instance removes its encrypted API key and its mailbox bindings. **Archive
accounts and their archived mail are kept**, but the accounts are disabled.

Deletion is **refused while any managed app password is still active** in Mailcow:
removing the instance would delete the API key required to revoke it. Revoke all managed
app passwords on the mailbox page first.

## Audit logging

Every administrative operation is written to the access log with the acting user:

- connecting an instance,
- updating an instance,
- deleting an instance,
- activating mailboxes (with the number provisioned and the number failed),
- revoking an app password.

## Mailbox limit

A single activation run can provision at most `Mailcow__MaxSelectedMailboxes` mailboxes
(default `1000`). This protects against accidental mass provisioning and excessive
Mailcow API and IMAP load. For larger instances, run the operation several times.

Each mailbox requires several API round trips plus an IMAP verification, and they are
processed sequentially. Provisioning many hundred mailboxes in one request can exceed
reverse-proxy or browser timeouts; split the selection into batches.

## Security notes

- The API key remains server-side. It is sent as an `X-API-Key` header, redacted from
  logs, and never returned to the browser.
- Mailcow's app-password responses can echo plaintext passwords in their `log` fields.
  Mail Archiver deliberately does not log response bodies and does not surface API
  errors to the browser; failures are reported as localized messages.
- An HTTP 200 response is not treated as success: mutations require Mailcow's per-result
  `type: success`.
- IMAP permission is not intrinsically read-only. Provisioned accounts have no
  server-side deletion policy configured, and administrators retain control of the
  account settings. This feature archives messages, not Mailcow configuration or backups.

### Hostnames and private networks

The Mailcow URL and the IMAP host are entered by an administrator and may point to
private address space. This is intentional and supported: Mail Archiver is commonly
deployed next to the mail server, so `mail.example.lan`, `192.168.10.20` or `10.0.0.5`
are normal values. **No private, loopback or link-local address is blocked.**

The trust boundary is the administrator role, not the network address. `Mail Accounts`
is available to self-managers, who may already configure an arbitrary IMAP server for
their own accounts; the Mailcow section is restricted to administrators, and an
administrator already has unrestricted access to every archive account in the
installation. Restricting the IMAP host would therefore not close a privilege boundary,
while it would break the intended on-premise deployment.

Two constraints still apply:

- The Mailcow URL must be a bare HTTPS origin. Paths, query strings, user info and
  redirects are rejected, so the API key cannot be forwarded to another origin.
- The IMAP host must be a hostname without a scheme or port, and provisioning always
  connects with TLS (`SslOnConnect`).

If you need to restrict which hosts Mail Archiver may reach, enforce it at the network
layer — egress firewall rules or a DNS policy for the container — rather than relying on
input validation in the application.

## Troubleshooting

| Symptom | Cause and remedy |
| --- | --- |
| *Encryption required* on connect or activate | `Security__CredentialEncryptionKey` is not set or invalid. Configure a base64-encoded 32-byte key and restart. |
| *HTTPS origin required* | The URL has a path, query string or embedded credentials. Enter the plain origin, e.g. `https://mail.example.com`. |
| *IMAP host invalid* | The IMAP host must be a hostname without scheme or port. |
| *Instance already connected* | Another instance with the same URL exists. Edit that instance instead. |
| *API request failed (401/403)* | The API key lacks the required permissions, or the IP restriction does not include the Mail Archiver host. |
| *API request rejected* | Mailcow returned an `error`/`danger`/`warning` row. Check the Mailcow logs; Mail Archiver does not forward response bodies. |
| *IMAP verification failed* | Wrong IMAP host or port, an untrusted certificate, or the app-password feature is disabled in Mailcow. |
| *IMAP account exists* | An independent IMAP account already archives this address. Use that account instead of creating a second archive. |
| *Revoke previous password* | The archive account for this mailbox was deleted. Revoke the remaining app password first, then activate again. |
| Mailbox stays **Pending** | The Mailcow API timed out. Reload the page and activate the mailbox again; no duplicate app password is created. |
| Delete is refused | Managed app passwords are still active. Revoke them on the mailbox page first. |

## API endpoints used

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## Database changes

The migration adds only the `MailcowInstances` and `MailcowMailboxes` tables together
with their indexes and foreign keys. Existing mailbox tables and archived data are
retained. Removing an archive account sets the binding's `MailAccountId` to `NULL` so the
remote credential can still be revoked afterwards.
