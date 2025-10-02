# 🧪 Using Development Versions (Dev Tag)

[← Back to Documentation Index](Index.md)

## 📋 Overview

This guide explains how to switch from the stable `latest` version to the development `dev` version of Mail Archiver for testing bug fixes and new features. This is particularly useful for users who want to test fixes before they are officially released.

## ⚠️ Important Considerations

Before switching to the development version, please consider the following:

- **Development versions may contain bugs or unstable features** that are not present in the stable release
- **Always backup your data** before switching to a development version
- **Use development versions only for testing** and not in production environments
- **Report any issues** you encounter to help improve the software

## 🛑 Stopping the Container (Recommended)

Before switching to the development version, it's recommended to stop the container:

```bash
docker compose down
```

## 🔄 Switching to the Dev Tag

1. Open your `docker-compose.yml` file in a text editor

2. Locate the `mailarchive-app` service section and change the image tag from `latest` to `dev`:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. Save the changes to your `docker-compose.yml` file

## 📥 Pulling the Development Image

After updating the docker-compose file, you need to pull the development image:

```bash
docker compose pull
```

This command will download the latest development version of the Mail Archiver image.

## ▶️ Starting the Container with the New Version

After pulling the new image, start the container to use the development version:

```bash
docker compose up -d
```

## 🔁 Switching Back to Stable Release

If you want to return to the stable version, follow these steps:

1. Stop the container:
```bash
docker compose down
```

2. Edit `docker-compose.yml` and change the image back to:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. Pull the latest image:
```bash
docker compose pull
```

4. Start the container:
```bash
docker compose up -d
```

## 📝 Additional Notes

- Development versions are updated more frequently than stable releases
- If you encounter issues, check the logs for error messages:
  ```bash
  docker compose logs mailarchive-app
  ```
- You can check which version is currently running by looking at the container's image information:
  ```bash
  docker compose images
