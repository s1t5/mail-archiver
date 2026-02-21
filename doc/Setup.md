# 🛠️ Mail Archiver Setup Guide

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide provides detailed instructions for setting up the Mail Archiver application using Docker Compose.

## 🛠️ Prerequisites

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Installation Steps

1. Install the prerequisites on your system.

2. Create a `docker-compose.yml` file with the following content:

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey;

      # Authentication Settings
      - Authentication__Username=admin
      - Authentication__Password=secure123!
      - Authentication__SessionTimeoutMinutes=60
      - Authentication__CookieName=MailArchiverAuth
      - Authentication__CookieSameSite=Lax

      # MailSync Settings
      - MailSync__IntervalMinutes=15
      - MailSync__TimeoutMinutes=60
      - MailSync__ConnectionTimeoutSeconds=180
      - MailSync__CommandTimeoutSeconds=300
      - MailSync__AlwaysForceFullSync=false
      - MailSync__IgnoreSelfSignedCert=false

      # BatchRestore Settings
      - BatchRestore__AsyncThreshold=50
      - BatchRestore__MaxSyncEmails=150
      - BatchRestore__MaxAsyncEmails=50000
      - BatchRestore__SessionTimeoutMinutes=30
      - BatchRestore__DefaultBatchSize=50

      # BatchOperation Settings
      - BatchOperation__BatchSize=50
      - BatchOperation__PauseBetweenEmailsMs=50
      - BatchOperation__PauseBetweenBatchesMs=250

      # Selection Settings
      - Selection__MaxSelectableEmails=250

      # View Settings (Privacy & Display)
      - View__DefaultToPlainText=true
      - View__BlockExternalResources=false

      # Npgsql Settings
      - Npgsql__CommandTimeout=900

      # Upload Settings for MBox and EML files
      - Upload__MaxFileSizeGB=10
      - Upload__KeepAliveTimeoutHours=4
      - Upload__RequestHeadersTimeoutHours=2

      # TimeZone Settings
      - TimeZone__DisplayTimeZoneId=Etc/UCT

      # Database Maintenance Settings (Optional)
      - DatabaseMaintenance__Enabled=false
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30

      # Logging Settings (Optional - defaults to Information level)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning

      # Security Settings
      - AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com

      # OIDC Configuration (see OIDC_Implementation.md for detailed setup)
      - OAuth__Enabled=true
      - OAuth__Authority=https://example.com
      - OAuth__ClientId=YOUR-CLIENT-ID
      - OAuth__ClientSecret=YOUR-CLIENT-SECRET
      - OAuth__ClientScopes__0=openid
      - OAuth__ClientScopes__1=profile
      - OAuth__ClientScopes__2=email
      - OAuth__DisablePasswordLogin=false
      - OAuth__AutoRedirect=false
      - OAuth__AutoApproveUsers=false
      - OAuth__AdminEmails__0=admin@example.com
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

networks:
  postgres:
```

3. Edit the database configuration in the `docker-compose.yml` and set a secure password in the `POSTGRES_PASSWORD` variable and the `ConnectionString`.

4. If you want to use authentication (which is strongly recommended), define a `Authentication__Username` and `Authentication__Password` which is used for the admin user.

5. Adjust the `TimeZone__DisplayTimeZoneId` environment variable to match your preferred timezone (default is "Etc/UCT"). You can use any IANA timezone identifier (e.g., "Europe/Berlin", "Asia/Tokyo").

6. Optionally configure the `Logging__LogLevel` environment variables to control the verbosity of application logs. See the Logging Settings section below for available options.

7. Configure a reverse proxy of your choice with https and authentication to secure access to the application. 

> ⚠️ **Attention**: The application itself does not provide encrypted access via https! It must be set up via a reverse proxy!

8. Initial start of the containers:
```bash
docker compose up -d
```

9. Restart containers:
```bash
docker compose restart
```

10. Access the application

11. Login with your defined credentials and add your first email account:
   - Navigate to "Email Accounts" section
   - Click "New Account"
   - Enter your server details and credentials
   - Save and start archiving!
   - If you want, create other users and assign accounts.

## 📚 Environment Variable Explanations

### 🗄️ Database Connection
- `ConnectionStrings__DefaultConnection`: The connection string to the PostgreSQL database. Modify the `Host`, `Database`, `Username`, and `Password` values as needed.

### 🔐 Authentication Settings
- `Authentication__Username`: The username for the admin account.
- `Authentication__Password`: The password for the admin account.
- `Authentication__SessionTimeoutMinutes`: The session timeout in minutes.
- `Authentication__CookieName`: The name of the authentication cookie.
- `Authentication__CookieSameSite`: Configures the SameSite attribute for authentication, session, and CSRF protection cookies. Valid values are:
  - `Strict` (default): Maximum security. Cookies are only sent with same-site requests. This may cause issues when navigating to the application from external links (e.g., clicking a link from another website), as the existing session won't be recognized.
  - `Lax`: Recommended when using a reverse proxy. Cookies are sent with top-level navigations and same-site requests, allowing users to follow external links to the application while maintaining CSRF protection for POST requests.
  - `None`: Cookies are sent with all requests. Requires HTTPS and the `Secure` attribute. Only use this if you have specific cross-site requirements and understand the security implications.
  
### 📨 MailSync Settings
- `MailSync__IntervalMinutes`: The interval in minutes between email synchronization.
- `MailSync__TimeoutMinutes`: The timeout for the sync operation in minutes.
- `MailSync__ConnectionTimeoutSeconds`: The connection timeout for IMAP connections in seconds.
- `MailSync__CommandTimeoutSeconds`: The command timeout for IMAP commands in seconds.
- `MailSync__AlwaysForceFullSync`: Whether to always force a full sync (true/false).
- `MailSync__IgnoreSelfSignedCert`: Whether to ignore self-signed certificates (true/false).

### 📤 BatchRestore Settings
- `BatchRestore__AsyncThreshold`: The number of emails that triggers async processing.
- `BatchRestore__MaxSyncEmails`: The maximum number of emails for sync processing.
- `BatchRestore__MaxAsyncEmails`: The maximum number of emails for async processing.
- `BatchRestore__SessionTimeoutMinutes`: The session timeout for batch restore in minutes.
- `BatchRestore__DefaultBatchSize`: The default batch size for email operations.

### 📦 BatchOperation Settings
- `BatchOperation__BatchSize`: The batch size for email operations.
- `BatchOperation__PauseBetweenEmailsMs`: The pause between individual emails in milliseconds.
- `BatchOperation__PauseBetweenBatchesMs`: The pause between batches in milliseconds.

### 🎯 Selection Settings
- `Selection__MaxSelectableEmails`: The maximum number of emails that can be selected at once.

### 👁️ View Settings (Privacy & Display)
- `View__DefaultToPlainText`: Controls the default email view mode for privacy and tracking prevention (true/false). Default is `false`.
  - When set to `true`: Emails open in plain-text view by default, preventing automatic loading of tracking pixels, external images, and web beacons. This is recommended for users concerned about email tracking and privacy.
  - When set to `false`: Emails open in HTML view by default, displaying the formatted email with images and styling.
  - Users can toggle between HTML and plain-text views using a button in the email details page (only visible when both formats are available).
  - The "Full View" link respects the currently selected view mode.

- `View__BlockExternalResources`: Blocks external resources (remote images, external CSS, external scripts, web fonts, etc.) in HTML email views to prevent tracking and improve privacy (true/false). Default is `false`.
  - When set to `true`: External resources are filtered out when displaying HTML emails. Only inline content and data URIs (including inline attachments via `cid:` references) are displayed.
  - When set to `false`: HTML emails are displayed with all their original external resources.
  - **Important**: This setting only affects email **display**. Archived emails are stored completely unchanged in the database with all original content preserved.
  - Blocked resources include:
    - Remote images (tracking pixels, external images hosted on servers)
    - External CSS stylesheets
    - External fonts via @font-face
    - External CSS imports via @import
    - External background images
  - Allowed resources:
    - Inline images embedded as data: URIs
    - Inline attachments referenced via cid: URIs
    - Inline CSS styles and style tags
  - This setting works independently from `DefaultToPlainText` and provides an additional layer of privacy protection when viewing HTML emails.

### 🗃️ Npgsql Settings
- `Npgsql__CommandTimeout`: The timeout for database commands in seconds.

### 📥 Upload Settings
- `Upload__MaxFileSizeGB`: The maximum file size for uploads in GB.
- `Upload__KeepAliveTimeoutHours`: The keep alive timeout for uploads in hours.
- `Upload__RequestHeadersTimeoutHours`: The timeout for request headers in hours.

### 🕐 TimeZone Settings
- `TimeZone__DisplayTimeZoneId`: The time zone used for displaying email timestamps in the UI. Uses IANA time zone identifiers (e.g., "Europe/Berlin", "Asia/Tokyo"). Default is "Etc/UCT" for backward compatibility. When importing emails timestamps will be converted to this time zone for display purposes.

### 🔧 Database Maintenance Settings
- `DatabaseMaintenance__Enabled`: Enable or disable automatic daily database maintenance (true/false). Default is `false`. When enabled, the system will automatically run VACUUM ANALYZE operations to optimize database performance and prevent bloat. See [Database Maintenance Guide](DatabaseMaintenance.md) for more details.
- `DatabaseMaintenance__DailyExecutionTime`: The time of day when database maintenance should run, in 24-hour format (HH:mm). Default is `02:00`. Choose a time during low system activity.
- `DatabaseMaintenance__TimeoutMinutes`: Maximum time allowed for maintenance operations in minutes. Default is `30`. Increase this value for larger databases.

### Logging Settings
- `Logging__LogLevel__Default`: The default log level for the application. Available levels are: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Default is `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Log level for ASP.NET Core framework messages. Default is `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Log level for Entity Framework database commands. Default is `Warning`.

### 🛡️ Security Settings
- `AllowedHosts`: A semicolon-separated list of host names that the application is allowed to serve. This helps prevent HTTP Host header attacks. Example: `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Important**: Do not use `*` in production environments as it disables host header validation.

### 🔐 OIDC Configuration

For detailed setup instructions for OpenID Connect authentication, see [OIDC Implementation Guide](OIDC_Implementation.md).

#### Basic OIDC Settings
- `OAuth__Enabled`: Enable or disable OIDC authentication (true/false)
- `OAuth__Authority`: The OpenID Connect authority URL (e.g., https://sts.windows.net/{TENANT-ID}/ for Azure AD)
- `OAuth__ClientId`: The client ID assigned by your identity provider
- `OAuth__ClientSecret`: The client secret assigned by your identity provider
- `OAuth__ClientScopes__0`: First scope requested from the identity provider (openid)
- `OAuth__ClientScopes__1`: Second scope requested from the identity provider (profile)
- `OAuth__ClientScopes__2`: Third scope requested from the identity provider (email)

#### User Provisioning Settings
- `OAuth__AutoApproveUsers`: Automatically approve new OIDC users without requiring manual admin approval (true/false). Default is `false`. When enabled, users who authenticate via the OIDC provider are immediately activated and can access the application. When disabled (default), new OIDC users are created as inactive and require manual activation by an administrator. See [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users) for detailed information.
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, etc.: Email addresses that should be automatically provisioned as administrators. Users with these email addresses will be created as active admins on first OAuth login, bypassing the normal approval process. Email matching is case-insensitive.

#### Passwordless Login Settings
- `OAuth__DisablePasswordLogin`: Hide username/password fields on login page (true/false). Default is `false`. When enabled, only the OAuth login button is displayed.
- `OAuth__AutoRedirect`: Automatically redirect users to OAuth provider (true/false). Default is `false`. Requires `OAuth__DisablePasswordLogin` to be `true`. Users will see a brief loading screen before being redirected.

#### Example: Full OIDC-First Configuration
```yaml
environment:
  - OAuth__Enabled=true
  - OAuth__Authority=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0
  - OAuth__ClientId=your-client-id
  - OAuth__ClientSecret=your-client-secret
  - OAuth__ClientScopes__0=openid
  - OAuth__ClientScopes__1=profile
  - OAuth__ClientScopes__2=email
  - OAuth__DisablePasswordLogin=true
  - OAuth__AutoRedirect=true
  - OAuth__AutoApproveUsers=true
  - OAuth__AdminEmails__0=admin@example.com
  - OAuth__AdminEmails__1=manager@example.com
```

## 🔐 Kestrel HTTPS Configuration (Optional)

While the application is meant to be accessed through a reverse proxy with HTTPS, you can also configure the Kestrel web server to use SSL/TLS certificates. This provides end-to-end encryption between the reverse proxy and the application container.

### Configuration Steps

1. **Generate or obtain an SSL certificate** in PFX format (e.g., `localhost.pfx`)

2. **Add the following environment variables** to your `docker-compose.yml` for the `mailarchive-app` service:

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Update the ports mapping** in the `mailarchive-app` service:

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Add a volume mapping** for the certificate:

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Place your certificate file** (e.g., `localhost.pfx`) in the `./certs` directory on your host system.

### Environment Variable Explanations

- `Kestrel__Endpoints__Http__Url`: HTTP endpoint URL (default: http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: HTTPS endpoint URL (default: https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Path to the PFX certificate file inside the container
- `Kestrel__Endpoints__Https__Certificate__Password`: Password for the PFX certificate file

> 💡 **Note**: This configuration is optional. If you're using a reverse proxy with HTTPS (recommended), the communication between reverse proxy and application can remain HTTP. However, for maximum security in sensitive environments, you may want to enable HTTPS on Kestrel as well to encrypt the entire communication path.

## 🔒 Security Notes

- Use strong passwords and change default credentials. Passwords should be at least 12 characters long and include a mix of uppercase letters, lowercase letters, numbers, and special characters. Avoid using common words or easily guessable information.
- Consider implementing HTTPS with a reverse proxy in production
- Regular backups of the PostgreSQL database are recommended. For detailed backup and restore procedures, see [Backup and Restore Guide](BackupRestore.md).
