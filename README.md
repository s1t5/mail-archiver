# 📧 Mail-Archiver - Email Archiving System

**A comprehensive solution for archiving, searching, and exporting emails**

<div style="display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 20px;">
  <a href="#"><img src="https://img.shields.io/badge/Docker-2CA5E0?style=for-the-badge&logo=docker&logoColor=white" alt="Docker"></a>
  <a href="#"><img src="https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET"></a>
  <a href="#"><img src="https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white" alt="PostgreSQL"></a>
  <a href="#"><img src="https://img.shields.io/badge/Bootstrap-563D7C?style=for-the-badge&logo=bootstrap&logoColor=white" alt="Bootstrap"></a>
  <a href="https://github.com/s1t5/mail-archiver"><img src="https://img.shields.io/github/stars/s1t5/mail-archiver?style=for-the-badge&logo=github" alt="GitHub Stars"></a>
  <a href="https://www.buymeacoffee.com/s1t5" target="_blank"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-s1t5-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee"></a>
</div>

## ✨ Key Features

### 📌 Core Features
- Automated archiving of incoming and outgoing emails from multiple accounts
- Storage of email content and attachments with scheduled synchronization
- Mobile and desktop optimized, multilingual responsive UI with dark mode

### 🔍 Search & Access
- Advanced search across all archived emails with filtering options
- Preview emails with attachment list
- Export entire mail accounts as mbox files or zipped EML archives
- Export selected individual emails or email batches

### 👥 User Management
- Multi-user support with account-specific permissions
- Dashboard with statistics, storage monitoring, and sender analysis
- Comprehensive access logging with detailed activity tracking of user activities (Access, Export, Deletion, Restore and many more) - see [Access Logging Guide](doc/Logs.md) for details

### 🧩 Email Provider Support
- **IMAP**: Traditional IMAP accounts with full synchronization capabilities
- **M365**: Microsoft 365 mail accounts via Microsoft Graph API ([Setup Guide](doc/AZURE_APP_REGISTRATION_M365.md))
- **IMPORT**: Import-only accounts for migrating existing email archives

### 📥 Import & Restore Functions
- MBox Import and EML Import (ZIP files with folder structure support)
- Restore selected emails or entire mailboxes to destination mailboxes

### 🗑️ Retention Policies
- Configure automatic deletion of archived emails from mailserver after specified days
- Set retention period per email account (e.g., 30, 90, or 365 days)
- **Local Archive Retention**: Configure separate retention period for local archive

For detailed information about retention policies, see [Retention Policies Documentation](doc/RetentionPolicies.md).

## 📚 Documentation

For detailed documentation on installation, configuration, and usage, please refer to the [Documentation Index](doc/Index.md). Please note that the documentation is still fresh and is continuously being expanded.

## 🖼️ Screenshots

### Dashboard
![Mail-Archiver Dashboard](https://github.com/s1t5/mail-archiver/blob/main/Screenshots/dashboard.jpg?raw=true)

### Archive
![Mail-Archiver Archive](https://github.com/s1t5/mail-archiver/blob/main/Screenshots/archive.jpg?raw=true)

### Email Details
![Mail-Archiver Mail](https://github.com/s1t5/mail-archiver/blob/main/Screenshots/details.jpg?raw=true)

## 🚀 Quick Start

### Prerequisites
- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

### 🛠️ Installation

1. Install the prerequisites on your system

2. Create a `docker-compose.yml` file 
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

      # TimeZone Settings
      - TimeZone__DisplayTimeZoneId=Etc/UCT
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

4. Definie a `Authentication__Username` and `Authentication__Password` which is used for the admin user.

5. Adjust the `TimeZone__DisplayTimeZoneId` environment variable to match your preferred timezone (default is "Etc/UCT"). You can use any IANA timezone identifier (e.g., "Europe/Berlin", "Asia/Tokyo").

6. Configure a reverse proxy of your choice with https to secure access to the application. 

> ⚠️ **Attention**
> The application itself does not provide encrypted access via https! It must be set up via a reverse proxy! Moreover the application is not build for public internet access!

7. Initial start of the containers:
```bash
docker compose up -d
```

8. Restart containers:
```bash
docker compose restart
```

9. Access the application in your prefered browser.

10. Login with your defined credentials and add your first email account:
- Navigate to "Email Accounts" section
- Click "New Account"
- Enter your server details and credentials
- Save and start archiving!
- If you want, create other users and assign accounts.

## 🔐 Security Notes
- Use strong passwords and change default credentials
- Consider implementing HTTPS with a reverse proxy in production
- Regular backups of the PostgreSQL database recommended (see [Backup & Restore Guide](doc/BackupRestore.md) for detailed instructions)

## ⚙️ Advanced Setup

For a complete list of all configuration options, please refer to the [Setup Guide](doc/Setup.md).


## 📋 Technical Details

### Architecture
- ASP.NET Core 8 MVC application
- PostgreSQL database for email storage
- MailKit library for IMAP communication
- Microsoft Graph API for M365 email access
- Background service for email synchronization
- Bootstrap 5 and Chart.js for frontend

## 🤝 Contributing
Contributions welcome! Please open an Issue or Pull Request. Also feel free to contact me by mail.

## 💖 Support the Project
If you find this project useful and would like to support its continued development, you can buy me a coffee! Your support helps me dedicate more time and resources to improving the application and adding new features. While financial support is not required, it is greatly appreciated and helps ensure the project's ongoing maintenance and enhancement.

<a href="https://www.buymeacoffee.com/s1t5" target="_blank"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-s1t5-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee"></a>

---

📄 *License: GNU GENERAL PUBLIC LICENSE Version 3 (see LICENSE file)*
