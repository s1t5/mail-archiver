# 🧪 Using Development Versions (Dev Tag) {#using-development-versions-dev-tag}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

このガイドでは、バグ修正や新機能をテストするために、Mail Archiver の安定版 `latest` から開発版 `dev` へ切り替える方法について説明します。これは、修正が正式にリリースされる前にテストを行いたいユーザーにとって特に有用です。

## ⚠️ Important Considerations {#important-considerations}

開発版に切り替える前に、以下の点に注意してください：

- **開発版には、安定版には存在しないバグや不安定な機能が含まれている可能性があります**
- **開発版に切り替える前に、必ずデータのバックアップを取ってください**
- **開発版はテスト目的のみで使用し**、本番環境では使用しないでください
- **問題が発生した場合は、** ソフトウェアの改善に役立てるため、報告をお願いします

## 🛑 Stopping the Container (Recommended) {#stopping-the-container-recommended}

開発版に切り替える前に、コンテナを停止することをお勧めします：

```bash
docker compose down
```

## 🔄 Switching to the Dev Tag {#switching-to-the-dev-tag}

1. テキストエディタで `docker-compose.yml` ファイルを開きます

2. `mailarchive-app` サービスセクションを探し、イメージタグを `latest` から `dev` に変更します：
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. 変更を `docker-compose.yml` ファイルに保存します

## 📥 Pulling the Development Image {#pulling-the-development-image}

docker-compose ファイルを更新した後、開発用イメージをプルする必要があります：

```bash
docker compose pull
```

このコマンドにより、Mail Archiver イメージの最新の開発版がダウンロードされます。

## ▶️ Starting the Container with the New Version {#starting-the-container-with-the-new-version}

新しいイメージをプルした後、コンテナを起動して開発版を使用します：

```bash
docker compose up -d
```

## 🔁 Switching Back to Stable Release {#switching-back-to-stable-release}

安定版に戻したい場合は、以下の手順に従ってください：

1. コンテナを停止します：
```bash
docker compose down
```

2. `docker-compose.yml` を編集し、イメージを以下に戻します：
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. 最新のイメージをプルします：
```bash
docker compose pull
```

4. コンテナを起動します：
```bash
docker compose up -d
```

## 📝 Additional Notes {#additional-notes}

- 開発版は、安定版よりも頻繁に更新されます
- 問題が発生した場合は、ログでエラーメッセージを確認してください：
  ```bash
  docker compose logs mailarchive-app
  ```
- コンテナのイメージ情報を確認することで、現在どのバージョンが実行されているかを確認できます：
  ```bash
  docker compose images
  ```
