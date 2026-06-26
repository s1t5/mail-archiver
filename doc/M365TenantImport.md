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

### Filtering

Tenant import and Tenant Management automatically exclude:
- **Guest accounts** (`userType == "Guest"`) — external invited users without their own mailboxes in the tenant.
- **Users without an Exchange Online license** — only users with an active Exchange service plan in their `assignedPlans` are listed. This avoids importing accounts that cannot be archived.

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

## Tenant Management

In addition to the create-form tenant import, Mail Archiver provides a dedicated **Tenant Management** page for administrators. It is reachable from the Mail Accounts index via the **Tenant Management** button (building icon) next to any Microsoft 365 account.

Tenant Management is useful for adding mailboxes that were not imported initially — for example when new mailboxes are created in the tenant after the initial onboarding, or when accounts were skipped originally.

### How it differs from the create-form tenant import

- The Microsoft 365 credentials (Client ID, Client Secret, Tenant ID) are **not entered again**. They are taken from the source account that the button was clicked on.
- The tenant mailbox list is loaded automatically when the page opens (no "Load mailboxes" button).
- Already-imported mailboxes are shown with an "already exists" badge and cannot be selected again.
- Only administrators can access Tenant Management.

### Step-by-Step Usage

1. Sign in to Mail Archiver as an administrator.
2. Open **Mail Accounts**.
3. Locate the Microsoft 365 account whose tenant you want to manage and click the **Tenant Management** button next to it.
4. The page loads and lists all mailboxes returned by Microsoft Graph for the source account's tenant.
   - Mailboxes that already exist as M365 accounts in Mail Archiver are marked with an "already exists" badge and cannot be selected.
   - Disabled tenant users are not listed; Tenant Management always skips disabled mailboxes.
5. Select the mailboxes you want to add.
6. Enter the **Account name** prefix that should be used for the new accounts (the naming scheme is `{prefix} - <{email}>`, identical to the create-form tenant import).
7. Optionally configure **Delete After Days** and **Local Retention Days** for the new accounts.
8. Click **Add selected mailboxes**.

After creation, Mail Archiver redirects to the mail account list and shows how many mailboxes were added.

### Searching mailboxes

For large tenants with several thousand mailboxes, a search field above the mailbox list lets you filter by display name or email address. The filter is case-insensitive and applies instantly as you type. **Select all** and **Clear selection** operate only on the currently visible (filtered) mailboxes, so filtering is a safe way to select a specific subset without affecting hidden entries.

### Renaming existing accounts

The **Rename existing accounts** checkbox applies the naming scheme `{prefix} - <{email}>` to all M365 accounts that share the same app registration (Client ID and Tenant ID) as the source account. This is useful for normalizing account names after an initial import where names were inconsistent.

When checked, saving the form renames every matching account — including the source account — regardless of whether new mailboxes are selected for import. You can use this option alone (without selecting any new mailboxes) to normalize names without adding accounts.

### Mailbox limit

A single Tenant Management operation can add at most `TenantManagement__MaxSelectedMailboxes` mailboxes at once (default `1000`). This protects against accidental mass imports and excessive Microsoft Graph / database load. If you need to import more mailboxes, run the operation multiple times. See the [Setup Guide](Setup.md#-tenant-management-settings) for how to adjust this limit.

### Permissions required

Tenant Management uses the same Microsoft Graph application permissions as the create-form tenant import. The source account's app registration must have:

- The permissions required for regular M365 account archiving, **and**
- **User.Read.All** — required to list tenant users and their mailbox addresses.

See [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md) for the initial setup.

### Audit logging

Each Tenant Management page view and each add operation is recorded in the access log, including the number of mailboxes listed or added and the source account used.
