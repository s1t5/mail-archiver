# 🔐 OpenID Connect (OIDC) Authentication Guide

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide provides comprehensive instructions for setting up OpenID Connect (OIDC) authentication in the Mail Archiver application, with specific examples for Microsoft Entra ID (Azure AD) integration. OIDC enables secure single sign-on (SSO) authentication using external identity providers.

## 📚 Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [OIDC Configuration](#oidc-configuration)
   - [Enable OIDC in Configuration](#enable-oidc-in-configuration)
   - [Environment Variables](#environment-variables)
4. [Microsoft Entra ID Setup](#microsoft-entra-id-setup)
   - [Create App Registration](#create-app-registration)
   - [Configure Authentication](#configure-authentication)
   - [Set Token Configuration](#set-token-configuration)
5. [Authelia Setup](#authelia-setup)
   - [Authelia Configuration](#authelia-configuration)
   - [Client Registration](#client-registration)
6. [Testing and Validation](#testing-and-validation)
7. [User Management with OIDC](#user-management-with-oidc)

## 🌐 Overview

The Mail Archiver application supports OpenID Connect (OIDC) authentication, allowing users to authenticate using external identity providers such as Microsoft Entra ID and other OIDC-compliant providers. This feature enhances security by leveraging enterprise identity management systems and enables single sign-on (SSO) capabilities.

## 🛠️ Prerequisites

- Administrative access to your chosen OIDC identity provider (e.g., Microsoft Entra ID)
- Mail Archiver application already deployed and accessible via HTTPS
- DNS name configured for your Mail Archiver instance (required for callback URLs)
- Administrative access to the Mail Archiver application and host system for configuration
- If using a reverse proxy, proper [reverse proxy configuration](ReverseProxy.md) for HTTPS header handling

## ⚙️ OIDC Configuration

### Enable OIDC in Configuration

To enable OIDC authentication, you need to configure the OAuth section in your `appsettings.json` file or environment variables for the docker deployment (see [Installation and Setup](Setup.md)).

#### Example Configuration (appsettings.json)

```json
{
  "OAuth": {
    "Enabled": true,
    "Authority": "https://sts.windows.net/{TENANT-ID}/",
    "ClientId": "YOUR-CLIENT-ID",
    "ClientSecret": "YOUR-CLIENT-SECRET",
    "ClientScopes": [
      "openid",
      "profile",
      "email"
    ],
    "DisablePasswordLogin": false
  }
}
```

### Environment Variables

When using Docker Compose or environment variables, configure the following variables in your `docker-compose.yml` file under the `mailarchive-app` service environment section:

```yaml
environment:
  # OIDC Configuration
  - OAuth__Enabled=true
  - OAuth__Authority=https://sts.windows.net/{TENANT-ID}/
  - OAuth__ClientId=YOUR-CLIENT-ID
  - OAuth__ClientSecret=YOUR-CLIENT-SECRET
  - OAuth__ClientScopes__0=openid
  - OAuth__ClientScopes__1=profile
  - OAuth__ClientScopes__2=email
  - OAuth__DisablePasswordLogin=false
```

### Configuration Parameters Explained

- **OAuth__Enabled**: Set to `true` to enable OIDC authentication
- **OAuth__Authority**: The OpenID Connect authority URL of your identity provider
- **OAuth__ClientId**: The client ID assigned by your identity provider
- **OAuth__ClientSecret**: The client secret assigned by your identity provider
- **OAuth__ClientScopes**: Array of scopes requested from the identity provider
- **OAuth__DisablePasswordLogin**: Set to `true` to disable traditional username/password login and enforce OAuth-only authentication (see Passwordless Login Configuration for more details)

> ⚠️ **Important**: The Client Secret should be kept secure. Use Docker secrets or environment variables in production environments.

## ☁️ Microsoft Entra ID Setup

This section provides step-by-step instructions for configuring Microsoft Entra ID (formerly Azure AD) as an OIDC identity provider for Mail Archiver.

### 🚀 Create App Registration

1. Navigate to the [Microsoft Entra Admin Center](https://entra.microsoft.com)
2. Sign in with your administrator account
3. In the left navigation pane, select **App registrations**
4. Click **+ New registration** at the top of the App registrations page
5. Fill in the following details:
   - **Name**: Enter a descriptive name (e.g., "Mail Archiver OIDC")
   - **Supported account types**: Select **Accounts in this organizational directory only**
   - **Redirect URI**: Select **Web** and enter your Mail Archiver callback URL:
     ```
     https://your-mail-archiver-domain/oidc-signin-completed
     ```
     Replace `your-mail-archiver-domain` with your actual domain (e.g., `mailarchiver.example.com`)

6. Click **Register**

### 🔐 Configure Authentication

1. After registration, note down the following values from the **Overview** page:
   - **Application (client) ID** - You'll need this as `OAuth__ClientId`
   - **Directory (tenant) ID** - You'll need this in the Authority URL

2. In the left menu, select **Authentication**
3. Click **Settings**
4. Under **Implicit grant and hybrid flows**, ensure **ID tokens** is checked
5. Under **Supported account types**, verify your selection matches your requirements
6. Under **Redirect URIs**, ensure your callback URL is listed correctly

### 🎯 Configure API Permissions

1. In the left menu, select **API permissions**
2. Click **+ Add a permission**
3. Select **Microsoft Graph**
4. Choose **Delegated permissions**
5. Add the following permissions (if needed for additional features):
   - **openid** - Sign users in 
   - **email** - View users' email address
   - **profile** - View users' basic profile
6. Click **Add permissions**
7. Click **Grant admin consent for [Your Organization]** if you want to pre-authorize the application

### 🔑 Generate Client Secret

1. Navigate to **Certificates & secrets** in the left menu
2. Under **Client secrets**, click **+ New client secret**
3. Provide a description (e.g., "Mail Archiver Auth Secret")
4. Select an expiration period
5. Click **Add**
6. **Important**: Copy the **Value** immediately and store it securely. This secret will not be shown again. It's needed as the `OAuth__ClientSecret`

## 🔐 Authelia Setup

This section provides configuration details for setting up Authelia as an OIDC identity provider for Mail Archiver. Authelia is an open-source authentication and authorization server that supports OIDC.

### 🛠️ Authelia Configuration

```yaml
identity_providers:
  oidc:
    clients:
      - client_id: 'mailarchiver'
        client_name: 'mailarchiver'
        client_secret: '$xyzg'  # The digest of 'insecure_secret'.
        public: false
        redirect_uris:
          - 'https://your-mail-archiver-domain/oidc-signin-completed'
        scopes:
          - 'openid'
          - 'profile'
          - 'email'
          - 'groups'
        response_types:
          - 'code'
        grant_types:
          - 'authorization_code'
        token_endpoint_auth_method: 'client_secret_post'
```

### 📝 Client Registration Details

- **client_id**: `mailarchiver` - The unique identifier for the Mail Archiver application
- **client_name**: `mailarchiver` - The display name shown to users during authentication
- **client_secret**: The hashed secret used for client authentication (replace with your own secure secret in production)
- **redirect_uris**: The callback URL where Authelia will redirect users after authentication. Replace `[YOUR_MAILARCHIVER_URL]` with your actual Mail Archiver domain
- **scopes**: The permissions requested from Authelia:
  - `openid`: Required for OIDC authentication
  - `profile`: Access to basic profile information
  - `email`: Access to email address
  - `groups`: Access to group memberships (optional, can be removed if not needed)
- **response_types**: `code` - Authorization code flow
- **grant_types**: `authorization_code` - Authorization code grant type
- **token_endpoint_auth_method**: `client_secret_post` - Client authentication method

> ⚠️ **Security Note**: The example uses a default secret for demonstration. In production, generate a secure secret and hash it using Authelia's tools. Never use the example secret in production environments.

## 🧪 Testing and Validation

### Initial Setup Testing

1. Restart your Mail Archiver application after configuring OIDC settings
2. Navigate to your Mail Archiver login page
3. You should see a new "Login with OAuth" button alongside the regular login form
4. Click the "Login with OAuth" button to initiate the OIDC flow
5. You should be redirected to your identity provider's login page
6. After successful authentication, you should be redirected back to Mail Archiver

### User Provisioning Validation

- First-time OIDC users will be automatically created in the Mail Archiver database
- Users will be created with the default "User" role and will need admin approval for access

## 👥 User Management with OIDC

### User Roles and Permissions

OIDC users follow the same role-based access control as local users:
- **Admin**: Full system access (requires manual assignment by existing admin, or automatic via AdminEmails configuration)
- **User**: Standard user access to assigned mail accounts
- **Self-Manager**: Can manage their own account and add new accounts

### Auto-Provisioning Administrators

You can configure specific email addresses to be automatically provisioned as administrators when they first log in via OIDC:

```json
{
  "OAuth": {
    "AdminEmails": [
      "admin@example.com",
      "it-manager@example.com"
    ]
  }
}
```

When a user with one of these email addresses logs in for the first time:
- They are automatically created as an active administrator
- No manual approval is required
- They gain full administrative privileges immediately
- Email matching is case-insensitive

**Security Note**: Only add trusted email addresses to this list, as these users will have full system access.

### User Provisioning Flow

#### New Users (Not in AdminEmails)
1. User authenticates via OAuth provider
2. System checks if email exists in local database
3. If new user:
   - Account is created with username format: `{DisplayName}_{UniqueId}`
   - User is marked as **inactive** and **requires approval**
   - Admin must manually activate the user in Users management
4. User receives "Account pending approval" message

#### New Users (In AdminEmails)
1. User authenticates via OAuth provider
2. System recognizes email is in AdminEmails list
3. Account is automatically created:
   - User is marked as **active** and **admin**
   - No approval required
   - User can immediately access the system
4. User is logged in successfully

### Password Management

OIDC users cannot have or change passwords within Mail Archiver since authentication is handled by the external identity provider:
- The "Change Password" option will be disabled for OIDC users
- Password reset must be handled through the OIDC provider
- Local password fields will remain empty for OIDC users

## 🔒 Passwordless Login Configuration

> ⚠️ **Important**: Even with passwordless login configuration, an initial admin user is still created during the first startup. You must set a secure password for this admin user in your `appsettings.json` or `docker-compose.yml` file to ensure system security.

For organizations that want to enforce OAuth-only authentication, the application supports disabling password login entirely.

### Disabling Password Login

Set `DisablePasswordLogin` to `true` to hide username/password fields:

```json
{
  "OAuth": {
    "Enabled": true,
    "DisablePasswordLogin": true,
    "Authority": "https://your-provider.com",
    "ClientId": "your-client-id",
    "ClientSecret": "your-secret"
  }
}
```

**Result**: Login page displays only the "Login with OAuth" button.

### Auto-Redirect to OAuth Provider

Enable `AutoRedirect` to automatically redirect users to your OAuth provider:

```json
{
  "OAuth": {
    "Enabled": true,
    "DisablePasswordLogin": true,
    "AutoRedirect": true,
    "Authority": "https://your-provider.com",
    "ClientId": "your-client-id",
    "ClientSecret": "your-secret"
  }
}
```

**Requirements**: 
- `DisablePasswordLogin` must be `true`
- `OAuth.Enabled` must be `true`

**Result**: Users see a brief loading screen and are automatically redirected to the OAuth provider.

### Complete Passwordless Setup Example

```json
{
  "OAuth": {
    "Enabled": true,
    "Authority": "https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0",
    "ClientId": "your-client-id",
    "ClientSecret": "your-secret",
    "ClientScopes": ["openid", "profile", "email"],
    "DisablePasswordLogin": true,
    "AutoRedirect": true,
    "AdminEmails": ["admin@company.com", "it@company.com"]
  }
}
```

### Security Considerations

- **Admin Email Provisioning**: The OAuth provider is trusted to verify email addresses. Only add trusted addresses to AdminEmails.
- **Emergency Access**: Consider keeping one password-based admin account for emergency access before fully disabling password login.
- **Testing**: Thoroughly test OAuth authentication before disabling password login in production.
- **SSO Environment**: Auto-redirect is best suited for single sign-on environments where all users authenticate via the same provider.

---

**Note**: This guide is current as of 2025. Identity provider services regularly update their interfaces and features, so some UI elements may differ. Always refer to the latest documentation from your identity provider for the most up-to-date information.
