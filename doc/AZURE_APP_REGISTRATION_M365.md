# ☁️ Azure App Registration and Retention Policy Guide for Mail Archiver

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide provides comprehensive instructions for setting up Azure App Registration for email retrieval and configuring retention policies in the Mail Archiver application.

## 📚 Table of Contents

1. [Overview](#overview)
2. [Azure App Registration for Email Retrieval](#azure-app-registration-for-email-retrieval)
   - [Prerequisites](#prerequisites)
   - [Create App Registration](#create-app-registration)
   - [Set Required API Permissions](#set-required-api-permissions)
   - [Generate Client Secret](#generate-client-secret)
3. [Configure Mail Archiver M365 Account](#configure-mail-archiver-m365-account)
4. [Retention Policy Setup](#retention-policy-setup)
   - [Configure Retention Policy](#configure-retention-policy)

## 🌐 Overview

The Mail Archiver application supports Microsoft 365 (M365) accounts using OAuth2 client credentials flow for email retrieval. Additionally, it provides retention policy functionality to automatically delete emails from the mail server after a specified period, helping manage storage space while maintaining a complete archive.

## ☁️ Azure App Registration for Email Retrieval

### 🛠️ Prerequisites

- Administrative access to Microsoft Entra ID (Azure AD)
- A Microsoft 365 tenant with Exchange Online licenses

### 🚀 Create App Registration

1. Navigate to the [Microsoft Entra Admin Center](https://entra.microsoft.com)
2. Sign in with your administrator account
3. In the left navigation pane, select **App registrations**
4. Click **+ New registration** at the top of the App registrations page
5. Fill in the following details:
   - **Name**: Enter a descriptive name (e.g., "Mail Archiver M365 Provider")
   - **Supported account types**: Select **Accounts in this organizational directory only** (Single tenant)
     - This is required for the client credentials flow used by Mail Archiver
   - **Redirect URI**: Leave this blank (not needed for client credentials flow)
6. Click **Register**

**Important**: Note down the following values from the **Overview** page:
- **Application (client) ID** - You'll need this as `ClientId` in Mail Archiver
- **Directory (tenant) ID** - You'll need this as `TenantId` in Mail Archiver

### 🔐 Set Required API Permissions

1. Navigate to **API permissions** in the left menu
2. Click **+ Add a permission**
3. Select **Microsoft Graph**
4. Choose **Application permissions** (required for Mail Archiver's client credentials flow)
5. Add the following permissions:
   - **Mail.Read** - Read mail in all mailboxes
   - **Mail.ReadWrite** - Read and write mail in all mailboxes (for restore function as well as deletion)

6. Click **Add permissions**
7. **CRITICAL**: Click **Grant admin consent for [Your Organization]**
8. Confirm by clicking **Yes**

**Note**: Application permissions are required because Mail Archiver uses client credentials flow to access mailboxes without user interaction.

### 🔑 Generate Client Secret

1. Navigate to **Certificates & secrets** in the left menu
2. Under **Client secrets**, click **+ New client secret**
3. Provide a description (e.g., "Mail Archiver Secret")
4. Select an expiration period
5. Click **Add**
6. **Important**: Copy the **Value** immediately and store it securely. This secret will not be shown again.

## 📧 Configure Mail Archiver M365 Account

After completing the app registration, you need to configure a M365 mail account in Mail Archiver with the following values:

### 📋 Required Values from App Registration:

1. **Client ID**: Copy the **Application (client) ID** from the app registration **Overview** page
2. **Client Secret**: Copy the **Value** you saved when creating the client secret
3. **Tenant ID**: Copy the **Directory (tenant) ID** from the app registration **Overview** page

### 🚀 Creating M365 Account in Mail Archiver:

1. Log into your Mail Archiver application
2. Navigate to **Mail Accounts** > **Create**
3. Fill in the following fields:
   - **Name**: Descriptive name for the account (e.g., "Sales Team M365")
   - **Email Address**: The target mailbox email address to archive
   - **Provider**: Select **M365**
   - **Client ID**: Enter the Application (client) ID from your app registration
   - **Client Secret**: Enter the client secret value you saved
   - **Tenant ID**: Enter the Directory (tenant) ID from your app registration

4. Click **Create**

### ⚠️ Important Notes:

- The **Email Address** field must contain the actual mailbox you want to archive
- The app registration must have permissions to access the specified mailbox

## 🗑️ Retention Policy Setup

The Mail Archiver application provides retention policy functionality to automatically delete emails from the IMAP server after they have been successfully archived, helping manage storage space on the IMAP server while maintaining a complete archive.

### 🛠️ Configure Retention Policy

1. Navigate to **Mail Accounts** in the Mail Archiver application
2. Either create a new M365 account or edit an existing one
3. In the account configuration form, locate the **Delete After Days** field
4. Enter the number of days after which archived emails should be deleted from the server:
   - For example, enter `30` to delete emails after 30 days
   - Leave empty to disable automatic deletion
5. Save the account configuration

---

**Note**: This guide is current as of 2025. Microsoft regularly updates their services, so some UI elements may differ. Always refer to the latest Microsoft documentation for the most up-to-date information.
