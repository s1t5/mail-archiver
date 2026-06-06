# 📥 CLI Local Import Guide

[← Back to Documentation Index](Index.md)

## 📋 Overview

The Mail Archiver supports importing MBox files and ZIP archives containing EML files directly from the local filesystem via CLI commands. This is ideal for large files that are impractical to upload through the web interface.

**Key benefits:**
- No file size limits imposed by HTTP uploads
- No browser timeout issues
- Direct access to files on the Docker host or mounted volumes
- Same deduplication and processing logic as web uploads

## ⚠️ Security Model

Local import is **CLI-only** – there is no web endpoint for browsing or importing local files. You must have Docker host access (`docker exec`) to run import commands.

A **path whitelist** (`LocalImport:AllowedPaths`) controls which directories the importer can read from. Files outside these paths are rejected.

## 🛠️ Setup

### 1. Configure Allowed Paths

In your `docker-compose.yml`, set the allowed import paths:

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. Mount Your Files

Mount the directory containing your MBox/EML files into the container:

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

Replace `/home/user/mbox-archives` with the actual path on your Docker host.

### 3. Restart the Container

```bash
docker compose down && docker compose up -d
```

## 📝 Usage

### Find Your Target Account ID

Before importing, you need the database ID of the target mail account. You can find it via the Web UI:

Navigate to "Email Accounts" open the destination account details, the account ID is displayed in the account list or in the URL when viewing an account.

### Import an MBox File

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**Parameters:**
| Parameter | Required | Description |
|-----------|----------|-------------|
| `--import-mbox` | Yes | Signals an MBox import operation |
| `--file <path>` | Yes | Path to the MBox file inside the container |
| `--account-id <id>` | Yes | Database ID of the target mail account |
| `--folder <name>` | No | Target folder name (default: `INBOX`) |

### Import EML Files (ZIP Archive)

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**Parameters:**
| Parameter | Required | Description |
|-----------|----------|-------------|
| `--import-eml` | Yes | Signals an EML import operation |
| `--file <path>` | Yes | Path to a **ZIP archive** containing `.eml` files inside the container |
| `--account-id <id>` | Yes | Database ID of the target mail account |

> **Important:** `--import-eml` expects a **ZIP archive** containing `.eml` files — it does **not** accept individual `.eml` files. To import single EML files, wrap them in a ZIP archive first:
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **Note for `dotnet run`:** When using `dotnet run` (instead of the published DLL), separate dotnet's arguments from the application's arguments with `--`:
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> The `--` tells `dotnet run` that all following arguments belong to your application, not to the .NET compiler.

## 📊 Example Output

```
Target account: user@example.com (ID: 1)

=== Local MBox Import ===
File: /data/import/myarchive.mbox
Size: 245.32 MB
Target Account ID: 1
Target Folder: INBOX

[Importing... progress logged to container logs]

=== Import Results ===
Status: Completed
Total Emails: 15234
Imported Successfully: 15120
Failed: 42
Skipped (malformed): 28
Skipped (duplicates): 44
Duration: 00:12:45
```

## 🔍 Monitoring

During import, detailed progress is written to the container logs:

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ Configuration Reference

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | First allowed path for local imports |
| `LocalImport__AllowedPaths__1` | (empty) | Second allowed path (optional) |

Add more paths by incrementing the index (`__2`, `__3`, etc.).

## 🚫 Error Handling

| Error | Cause | Solution |
|-------|-------|----------|
| `File not found` | The `--file` path does not exist in the container | Verify the file path and volume mount |
| `File path is not in an allowed import directory` | The file is outside whitelisted paths | Either move the file into an allowed path or add its directory to `LocalImport:AllowedPaths` |
| `Mail account with ID X not found` | Wrong account ID | Verify the account ID via the Web UI or database query |
| `Invalid account-id` | Non-numeric value for `--account-id` | Use a numeric database ID |

## 🔒 Security Best Practices

1. **Use read-only mounts** when possible:
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **Restrict AllowedPaths** to only the directories you actually use.

3. **Remove volume mounts** after import is complete to maintain a clean security surface.

4. **Never expose** the import directory via the web server – there is no web endpoint for local imports.

## 📂 Importing from Different Sources

### From a Network Share

Mount the network share on your Docker host, then mount it into the container:

```bash
# On Docker host
mount -t cifs //nas/email-archives /mnt/email-archives -o username=user,password=pass

# In docker-compose.yml
volumes:
  - /mnt/email-archives:/data/import
```

### From an External USB Drive

Mount the USB drive on your Docker host, then mount it into the container:

```bash
# On Docker host
mount /dev/sdb1 /mnt/usb-import

# In docker-compose.yml
volumes:
  - /mnt/usb-import:/data/import
```

## 📦 Import Formats at a Glance

| CLI Flag | Input Format | Typical File Extension | Single Email | Bulk |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | MBox file | `.mbox`, `.mbx` | ❌ (always bulk) | ✅ |
| `--import-eml` | ZIP archive of EMLs | `.zip` | ❌ (ZIP required) | ✅ |

*Single `.eml` files are not directly supported. Use `zip` to wrap them, or import via the web upload.*

## ❓ FAQ

**Q: Why does `--import-eml` require a ZIP file?**
A: The underlying EML import service (`EmlImportService`) processes ZIP archives internally using .NET's `ZipFile` API. This design supports bulk imports efficiently. For individual `.eml` files, wrap them in a ZIP archive (e.g., `zip emails.zip *.eml`) or use the web upload.

**Q: Why can't I browse files from the web UI?**
A: This is a deliberate security design. This function is intentionally implemented via CLI for system-level interactions because normal users do not have direct access to the OS level; they only interact with the Web UI.

To prevent security risks such as path traversal attacks and unauthorized filesystem exposure, regular users are restricted to using the Web UI for file uploads and imports. This ensures they cannot bypass the intended interface to access the underlying container filesystem.