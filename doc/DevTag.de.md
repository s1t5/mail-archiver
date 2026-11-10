# 🧪 Verwendung von Entwicklungsversionen (Dev Tag) {#using-development-versions-dev-tag}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden erklärt, wie Sie von der stabilen `latest`-Version zur Entwicklerversion `dev` von Mail Archiver wechseln, um Fehlerbehebungen und neue Funktionen zu testen. Dies ist besonders nützlich für Benutzer, die Korrekturen testen möchten, bevor diese offiziell veröffentlicht werden.

## ⚠️ Wichtige Überlegungen {#important-considerations}

Bevor Sie zur Entwicklungsversion wechseln, beachten Sie bitte Folgendes:

- **Entwicklungsversionen können Fehler oder instabile Funktionen enthalten**, die in der stabilen Version nicht vorhanden sind
- **Erstellen Sie immer eine Sicherung Ihrer Daten**, bevor Sie zu einer Entwicklungsversion wechseln
- **Verwenden Sie Entwicklungsversionen nur zum Testen** und nicht in Produktionsumgebungen
- **Melden Sie alle auftretenden Probleme**, um zur Verbesserung der Software beizutragen

## 🛑 Stoppen des Containers (Empfohlen) {#stopping-the-container-recommended}

Bevor Sie zur Entwicklungsversion wechseln, wird empfohlen, den Container zu stoppen:

```bash
docker compose down
```

## 🔄 Wechseln zum Dev Tag {#switching-to-the-dev-tag}

1. Öffnen Sie Ihre `docker-compose.yml`-Datei in einem Texteditor

2. Suchen Sie den `mailarchive-app`-Serviceabschnitt und ändern Sie das Image-Tag von `latest` zu `dev`:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. Speichern Sie die Änderungen in Ihrer `docker-compose.yml`-Datei

## 📥 Herunterladen des Development-Images {#pulling-the-development-image}

Nachdem Sie die docker-compose Datei aktualisiert haben, müssen Sie das Development-Image herunterladen:

```bash
docker compose pull
```

Dieser Befehl lädt die neueste Development-Version des Mail Archiver Images herunter.

## ▶️ Starten des Containers mit der neuen Version {#starting-the-container-with-the-new-version}

Nachdem Sie das neue Image gezogen haben, starten Sie den Container, um die Entwicklungsversion zu verwenden:

```bash
docker compose up -d
```

## 🔁 Zurückwechseln zur stabilen Version {#switching-back-to-stable-release}

Wenn Sie zur stabilen Version zurückkehren möchten, führen Sie diese Schritte aus:

1. Stoppen Sie den Container:
```bash
docker compose down
```

2. Bearbeiten Sie `docker-compose.yml` und ändern Sie das Image zurück auf:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. Ziehen Sie das neueste Image:
```bash
docker compose pull
```

4. Starten Sie den Container:
```bash
docker compose up -d
```

## 📝 Zusätzliche Hinweise {#additional-notes}

- Entwicklungsversionen werden häufiger aktualisiert als stabile Releases
- Wenn Sie auf Probleme stoßen, überprüfen Sie die Logs auf Fehlermeldungen:
  ```bash
  docker compose logs mailarchive-app
  ```
- Sie können überprüfen, welche Version derzeit läuft, indem Sie die Image-Informationen des Containers prüfen:
  ```bash
  docker compose images
  ```
