# Microsoft 365 Tenant Mailbox Import

[← Back to Documentation Index](Index.md)

## Overview

Mail Archiver can create Microsoft 365 mail accounts for more than one mailbox in the same tenant from a single create form. This is useful when onboarding a customer or organization where all, or a chosen subset of, Microsoft 365 mailboxes should be archived.

The tenant import uses the Microsoft Graph application credentials configured in the form and creates one Mail Archiver mail account per imported mailbox.

## Prerequisites

Before using tenant import, complete the Microsoft 365 app registration setup described in the [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md).

You need these values from Microsoft Entra ID:

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

The app registration must have Microsoft Graph **application permissions** that allow Mail Archiver to read the tenant mailboxes. In addition to the permissions required for regular M365 account archiving, the following permission is required for tenant import:

- **User.Read.All** – required to list tenant users and their mailbox addresses.

Tenant import is selected as the provider **Microsoft 365 (tenant)** in the create form.

## Import Modes

Tenant import supports two modes:

### Import all listed mailboxes

Use this mode when every mailbox returned by Microsoft Graph should be created as a Mail Archiver account.

- Mail Archiver loads the tenant mailbox list from Microsoft Graph.
- Already existing M365 mail accounts are skipped.
- If **Skip disabled mailboxes** is enabled, disabled users are not included in the mailbox list.

### Import selected mailboxes only

Use this mode when only specific mailboxes in the tenant should be archived.

- Load the tenant mailbox list first.
- Disable **Import all listed mailboxes**.
- Select the mailboxes that should be imported.
- Submit the form to create accounts only for the selected mailboxes.

## Step-by-Step Usage

1. Sign in to Mail Archiver as a user who can create mail accounts.
2. Open **Mail Accounts** > **Create**.
3. Select **Microsoft 365 (tenant)** as the provider.
4. Enter the Microsoft 365 credentials:
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. Enter the **Account name** prefix that should be used for generated accounts.
6. Choose whether disabled mailboxes should be skipped.
7. Click **Load mailboxes** to list tenant mailboxes.
8. Choose the desired import behavior:
   - Keep **Import all listed mailboxes** enabled to import all listed mailboxes.
   - Disable it and select individual mailboxes to import only specific mailboxes.
9. Click **Save**.

After creation, Mail Archiver redirects back to the mail account list and shows how many tenant mail accounts were imported and how many existing accounts were skipped.

## Account Naming

For tenant imports, Mail Archiver uses the value entered in **Account name** as a prefix for every created mailbox account.

Example:

- Entered account name: `Test GmbH`
- Tenant mailbox: `mailbox@example.com`
- Created account name: `Test GmbH - <mailbox@example.com>`

This makes it easier to identify imported accounts that belong to the same tenant or customer.

## Existing Accounts

If a mailbox already exists as a Microsoft 365 account in Mail Archiver, tenant import skips it instead of creating a duplicate. Existing account detection is based on the mailbox email address and the M365 provider.

The mailbox list can also show whether a mailbox already exists before submitting the import.

## Disabled Mailboxes

The **Skip disabled mailboxes** checkbox controls whether disabled tenant users should be excluded from the mailbox list.

- Enabled: only enabled tenant users are listed and imported.
- Disabled: disabled tenant users can appear in the list and can be imported if selected or if all listed mailboxes are imported.

## Notes and Troubleshooting

### Mailbox list cannot be loaded

Check the following values and permissions:

- Client ID is correct.
- Client Secret is valid and has not expired.
- Tenant ID is correct and not empty.
- Admin consent has been granted for the required Microsoft Graph application permissions.
- The app registration has been granted the **User.Read.All** application permission (required for listing tenant mailboxes).

### No mailboxes are shown

Microsoft Graph must return tenant users with either a `mail` value or a `userPrincipalName`. Users without both values are ignored because Mail Archiver cannot create a mailbox account without an address.

### Some mailboxes are skipped during import

This is expected when the mailbox already exists as an M365 account in Mail Archiver. The success message after import includes the number of skipped existing accounts.

### IMAP accounts show different fields

Tenant import is only available for the **M365** provider. For **IMAP**, Mail Archiver shows the IMAP connection fields instead and does not use the tenant import options.
