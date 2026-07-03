# ☁️ Personal Microsoft Account (Outlook.com / M365 Family) Setup

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide explains how to archive emails from a **personal Microsoft account** (Outlook.com, Hotmail, live.com, or Microsoft 365 Family) in Mail Archiver.

Unlike organizational Microsoft 365 accounts (which use the client-credentials flow — see [Azure App Registration for M365](AZURE_APP_REGISTRATION_M365.md)), personal accounts use **OAuth2 with the Device Code Flow (RFC 8628)**. Microsoft has deprecated Basic Auth and app passwords for personal accounts, so OAuth2 is required.

Mail Archiver ships with a **pre-registered shared Client ID** so that most users do **not** need to create their own Azure App Registration. Power users can still register their own app (see Option B below).

## 📚 Table of Contents

1. [Option A: Standard Setup (Recommended)](#option-a-standard-setup-recommended)
2. [Option B: Custom App Registration (Power Users)](#option-b-custom-app-registration-power-users)
3. [Token Refresh & Revocation](#token-refresh--revocation)
4. [Important Notes](#important-notes)
5. [Troubleshooting](#troubleshooting)

---

## 🚀 Option A: Standard Setup (Recommended)

Use this option if your Mail Archiver instance has a default Client ID configured (the default for official Docker images). No Azure Portal access is required.

### 🛠️ Prerequisites

- A personal Microsoft account (Outlook.com, live.com, or M365 Family)
- A device with a browser (smartphone, laptop, or tablet) to complete the authorization

### 📝 Steps

1. Log into your Mail Archiver application.
2. Navigate to **Mail Accounts** > **Create**.
3. Fill in the following fields:
   - **Name**: Descriptive name for the account (e.g., "Personal Outlook")
   - **Email Address**: Your personal Microsoft email address (e.g., `you@outlook.com`)
   - **Provider**: Select **Microsoft Personal**
4. No Client ID or Client Secret fields are shown — Mail Archiver uses the built-in default registration automatically.
5. Click **Create**.
6. The device-code authorization page opens automatically. A short code (e.g., `ABCD-EFGH`) is displayed along with a link to `https://microsoft.com/devicelogin` (or the verification URL shown).
7. On **any device** with a browser, open that URL and sign in with your personal Microsoft account.
8. Enter the code from step 6.
9. A consent screen appears, showing **"Mail-Archiver"** requesting IMAP access. Review and approve.
10. The Mail Archiver page automatically detects success and redirects to the account edit view.
11. Done — synchronization starts automatically according to the account settings.

> ⚠️ **Unverified Publisher Warning**: The consent screen may show a warning that "Mail-Archiver" is an unverified publisher. This is expected — publisher verification requires a business identity (Microsoft Partner Network account) and is not needed for the flow to work. The app functions normally without it.

---

## 🔧 Option B: Custom App Registration (Power Users)

Use this option if you do not want to use the shared default Client ID (e.g., for compliance reasons or if you want full control over the app registration).

### 🛠️ Prerequisites

- A Microsoft account (personal or work/school) that can access the Azure Portal
- Basic familiarity with the Azure/Entra portal

### 🚀 Create App Registration

1. Navigate to the [Microsoft Entra Admin Center](https://entra.microsoft.com) or the [Azure Portal](https://portal.azure.com).
2. Sign in with your Microsoft account.
3. In the left navigation pane, select **App registrations** (under Identity > Applications).
4. Click **+ New registration**.
5. Fill in the following details:
   - **Name**: Enter a descriptive name (e.g., "Mail Archiver MSA")
   - **Supported account types**: Select **Accounts in any organizational directory and personal Microsoft accounts**
     - This is the multi-tenant option and supports both personal and organizational accounts.
   - **Redirect URI**: Leave this blank (not needed for Device Code Flow).
6. Click **Register**.
7. Note down the **Application (client) ID** from the **Overview** page.

### 🔐 Enable Public Client Flows

1. In your app registration, navigate to **Authentication** in the left menu.
2. At the bottom, find **Allow public client flows** and set it to **Yes**.
3. Click **Save**.

> ℹ️ No client secret, no redirect URI, and no API permissions need to be configured manually — the Device Code Flow uses delegated permissions that are requested at runtime via the `IMAP.AccessAsUser.All` scope.

### 📧 Configure Mail Archiver

You can also use your custom Client ID:

**Override the global default (affects all MSA accounts)**

Set the Client ID in your environment variable:


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 Token Refresh & Revocation

### Automatic Token Refresh

After the initial authorization, Mail Archiver receives:
- An **access token** (short-lived, ~1 hour)
- A **refresh token** (long-lived, used to obtain new access tokens automatically)

Mail Archiver automatically refreshes the access token before each sync run. You do not need to re-authorize unless the refresh token expires or is revoked.

### Revoking Access

To revoke Mail Archiver's access to your personal Microsoft account:

1. Visit [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage)
2. Sign in with your personal Microsoft account.
3. Find **"Mail-Archiver"** (or your custom app name) in the list.
4. Click **Remove** to revoke access.

After revoking, Mail Archiver can no longer access the account until you re-authorize.

---

## ⚠️ Important Notes
- **Shared Client ID**: When using the default Client ID, all Mail Archiver users on all instances share the same Azure App Registration. The maintainer of Mail Archiver is responsible for keeping the registration active. If Microsoft disables the shared registration, all users would need to switch to a custom registration (Option B) until a new release ships an updated Client ID.



- **Organizational Accounts**: The Microsoft Personal provider uses the `/common` authority endpoint and supports both personal and organizational (work/school) accounts. For organizational accounts requiring client-credentials access (app-only, no user sign-in), use the **M365** provider instead.


*This guide is current as of 2026. Microsoft regularly updates their services and UI, so some steps may differ. Refer to the [Microsoft identity platform documentation](https://learn.microsoft.com/en-us/entra/identity-platform/) for the latest details.*
