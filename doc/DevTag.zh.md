# 🧪 使用开发版本 (Dev Tag) {#using-development-versions-dev-tag}

[← 返回文档索引](index.md)

## 📋 概述 {#overview}

本指南介绍了如何从 Mail Archiver 的稳定 `latest` 版本切换到开发 `dev` 版本，以测试错误修复和新功能。这对于希望在正式发布前测试修复程序的用户特别有用。

## ⚠️ 重要注意事项 {#important-considerations}

在切换到开发版本之前，请考虑以下事项：

- **开发版本可能包含错误或不稳定的功能**，这些问题在稳定版本中并不存在
- **在切换到开发版本之前，请务必备份您的数据**
- **仅将开发版本用于测试**，请勿在生产环境中使用
- **报告您遇到的任何问题**，以帮助改进软件

## 🛑 停止容器（推荐） {#stopping-the-container-recommended}

在切换到开发版本之前，建议停止容器：

```bash
docker compose down
```

## 🔄 切换到 Dev Tag {#switching-to-the-dev-tag}

1. 在文本编辑器中打开您的 `docker-compose.yml` 文件

2. 找到 `mailarchive-app` 服务部分，并将镜像标签从 `latest` 更改为 `dev`：
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. 保存对 `docker-compose.yml` 文件的更改

## 📥 拉取开发镜像 {#pulling-the-development-image}

更新 docker-compose 文件后，您需要拉取开发镜像：

```bash
docker compose pull
```

此命令将下载 Mail Archiver 镜像的最新开发版本。

## ▶️ 使用新版本启动容器 {#starting-the-container-with-the-new-version}

拉取新镜像后，启动容器以使用开发版本：

```bash
docker compose up -d
```

## 🔁 切换回稳定版本 {#switching-back-to-stable-release}

如果您想返回稳定版本，请按照以下步骤操作：

1. 停止容器：
```bash
docker compose down
```

2. 编辑 `docker-compose.yml` 并将镜像改回：
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. 拉取最新镜像：
```bash
docker compose pull
```

4. 启动容器：
```bash
docker compose up -d
```

## 📝 补充说明 {#additional-notes}

- 开发版本的更新频率高于稳定版本
- 如果遇到问题，请检查日志中的错误信息：
  ```bash
  docker compose logs mailarchive-app
  ```
- 您可以通过查看容器的镜像信息来检查当前正在运行的版本：
  ```bash
  docker compose images
  ```
