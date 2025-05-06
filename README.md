# 📧 Mail-Archiver - IMAP Email Archiving System

**A comprehensive solution for archiving, searching, and exporting emails from IMAP accounts**

[![Docker](https://img.shields.io/badge/Docker-2CA5E0?style=for-the-badge&logo=docker&logoColor=white)](#)
[![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](#)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)](#)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-563D7C?style=for-the-badge&logo=bootstrap&logoColor=white)](#)

## ✨ Key Features

### 📥 Email Archiving
- Automated archiving of incoming and outgoing emails
- Support for multiple IMAP accounts
- Storage of email content and attachments
- Scheduled synchronization at configurable intervals

### 🔍 Advanced Search
- Search across all archived emails
- Filter by date range, sender, recipient, and more
- Preview emails with attachment list

### 📊 Dashboard & Statistics
- Account-specific statistics and overview
- Storage usage monitoring
- Top senders analysis

### 📤 Export Functions
- Export individual emails in EML format
- Bulk export search results to CSV or JSON
- Attachment download with original filenames

## 🖼️ Screenshots
![Mail-Archiver Screenshot](https://github.com/s1t5/mail-archiver/blob/main/Screenshots/dashboard.jpg?raw=true)

## 🚀 Quick Start

### Prerequisites
- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)
- IMAP email account credentials

### 🛠️ Installation

1. Clone the repository:
```bash
git clone https://github.com/s1t5/mail-archiver.git
cd mail-archiver
```

2. Adapt the `docker-compose.yml` and store a secure database password. Beyond that the password must also be specified in the connection string found in the `appsettings.json` file. 

3. Place your certificate in the certs folder which has to be created first and adjust the nginx.conf config. You may create a self singed cert if you're just using the app locally:
```bash
openssl req -x509 -nodes -days 3650 -newkey rsa:2048 -keyout certs/selfsigned.key -out certs/selfsigned.crt -subj "/CN=localhost"
```

4. Build and start containers:
```bash
docker compose up -d --build
```

5. Restart containers:
```bash
docker compose restart
```

6. Access the application:
- Web Interface: https://localhost

7. Add your first email account:
- Navigate to "Email Accounts" section
- Click "New Account"
- Enter your IMAP server details and credentials
- Save and start archiving!

## 🐳 Docker Deployment

| Service | Port | Description |
|---------|------|-------------|
| `mailarchive-app` | 5000 | ASP.NET Core Application |
| `postgres` | 5432 | PostgreSQL Database |
| `nginx` | 3003→3000 | Web Server / Reverse Proxy |

## 🔐 Security Note
- 🔒 Use strong passwords for PostgreSQL and change default credentials
- 🔐 Consider implementing HTTPS with a reverse proxy in production
- 💾 Regular backups of the PostgreSQL database recommended
- 🛡️ It is necessary to implement the authentication for the application via a reverse proxy as the application itself does not provide any authentication.

## 📋 Technical Details

### Architecture
- ASP.NET Core 8 MVC application
- PostgreSQL database for email storage
- MailKit library for IMAP communication
- Background service for email synchronization
- Bootstrap 5 and Chart.js for frontend

## 🤝 Contributing
Contributions welcome! Please open an Issue or Pull Request.

## 🚀 Roadmap
- Full-text search improvements
- OAuth support for major email providers
- Enhanced export options and formats

## 🚧 Known Issues
- Charts may require manual refresh after database synchronization
- Large attachments can affect performance

---

📄 *License: GNU GENERAL PUBLIC LICENSE Version 3 (see LICENSE file)*
