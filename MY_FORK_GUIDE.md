# Mein MailArchiver Fork - Anleitung

## Was wurde bisher gemacht (automatisch)

✅ **Phase 1: Upstream-Repository verbunden**
- `upstream` Remote hinzugefügt: `https://github.com/s1t5/mail-archiver.git`
- Alle Branches und Tags von Upstream gefetched

✅ **Phase 2: Fork aktualisiert**
- `upstream/main` in deinen `main` Branch gemerged (Fast-Forward)
- 6 Dateien aktualisiert (u.a. neue IMAP Reconnect-Funktionen)
- Änderungen zu deinem Fork auf GitHub gepusht

✅ **Phase 3: Vorbereitungen getroffen**
- `docker-compose.my-fork.yml` erstellt (nutzt dein Image)
- `build-and-run-my-fork.bat` Skript erstellt

---

## Was du jetzt lokal machen musst

### 1. Docker-Image bauen

Führe diesen Befehl **in deinem Terminal** (PowerShell oder CMD) aus:

```bash
cd C:\Users\User\GIT\mail-archiver
docker build -t mailarchiver:my-fork-latest .
```

**Hinweis:** 
- Dies baut das Image aus deinem aktuellen Branch (`fix/progress-bar-decimal-culture`)
- Deine Änderungen aus dem PR sind **automatisch enthalten**
- Basis ist der aktuelle Stand von `upstream/main` (inkl. aller neueren Commits)

---

### 2. Image testen (optional)

```bash
# Einfacher Test ohne Datenbank
docker run --rm -p 5000:5000 mailarchiver:my-fork-latest

# Mit Datenbank-Connection (wenn PostgreSQL läuft)
docker run --rm -p 5000:5000 \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey" \
  mailarchiver:my-fork-latest
```

---

### 3. Mit docker-compose starten

#### Option A: Mit deinem lokalen Image
```bash
# Starte alles (App + PostgreSQL)
docker compose -f docker-compose.my-fork.yml up -d

# Stoppen
docker compose -f docker-compose.my-fork.yml down

# Logs anzeigen
docker compose -f docker-compose.my-fork.yml logs -f mailarchive-app
```

#### Option B: Mit dem originalen docker-compose.yml
Falls du die originale `docker-compose.yml` nutzen willst, ändere diese Zeile:
```yaml
services:
  mailarchive-app:
    # build: .  <- auskommentieren
    image: mailarchiver:my-fork-latest  <- einfügen
```

---

### 4. Image in Registry pushen (für Produktion)

#### Option A: Docker Hub
```bash
# Anmelden
docker login

# Image taggen
docker tag mailarchiver:my-fork-latest dein-dockerhub-name/mailarchiver:my-fork-latest

# Push
docker push dein-dockerhub-name/mailarchiver:my-fork-latest

# In docker-compose.my-fork.yml anpassen:
# image: dein-dockerhub-name/mailarchiver:my-fork-latest
```

#### Option B: GitHub Container Registry (GHCR) - EMPFOHLEN
```bash
# Anmelden (GitHub Personal Access Token mit package:write Scope)
echo GITHUB_TOKEN | docker login ghcr.io -u dein-github-username --password-stdin

# Image taggen
docker tag mailarchiver:my-fork-latest ghcr.io/git-usr123/mailarchiver:my-fork-latest

# Push
docker push ghcr.io/git-usr123/mailarchiver:my-fork-latest

# In docker-compose.my-fork.yml anpassen:
# image: ghcr.io/git-usr123/mailarchiver:my-fork-latest
```

---

## Regelmäßige Updates (wöchentlich empfohlen)

Führe diese Schritte aus, um Upstream-Änderungen in deinen Fork zu holen:

```bash
# 1. Upstream-Änderungen holen
git fetch upstream

# 2. In deinen main mergen
git checkout main
git merge upstream/main

# 3. Zu deinem Fork pushen
git push origin main

# 4. Deinen PR-Branch aktualisieren (optional - ohne Force-Push!)
git checkout fix/progress-bar-decimal-culture
git merge main

# 5. Neues Image bauen und pushen
docker build -t mailarchiver:my-fork-latest .
docker tag mailarchiver:my-fork-latest dein-dockerhub-name/mailarchiver:my-fork-latest
docker push dein-dockerhub-name/mailarchiver:my-fork-latest

# 6. Container neu starten
docker compose -f docker-compose.my-fork.yml down
docker compose -f docker-compose.my-fork.yml up -d
```

---

## Wichtige Befehle im Überblick

| Aktion | Befehl |
|--------|--------|
| Image bauen | `docker build -t mailarchiver:my-fork-latest .` |
| Image starten | `docker run -p 5000:5000 mailarchiver:my-fork-latest` |
| Mit Compose starten | `docker compose -f docker-compose.my-fork.yml up -d` |
| Logs anzeigen | `docker compose -f docker-compose.my-fork.yml logs -f` |
| Stoppen | `docker compose -f docker-compose.my-fork.yml down` |
| Upstream Updates | `git fetch upstream && git merge upstream/main` |

---

## Status deines Forks

- **Aktueller Branch:** `fix/progress-bar-decimal-culture` (dein PR)
- **Basis:** `main` (enthält jetzt alle Upstream-Updates bis c5b05f7)
- **Neue Dateien seit deinem letzten Update:**
  - `Services/Providers/Imap/ReconnectCircuitBreaker.cs`
  - `tests/MailArchiver.Tests/Services/ReconnectCircuitBreakerTests.cs`
  - Änderungen an `MailArchiver.csproj`, `ImapConnectionFactory.cs`, `ImapMailSyncService.cs`

---

## Troubleshooting

### Docker bricht beim Bauen ab
- **Problem:** .NET 10.0 SDK fehlt oder Speicherprobleme
- **Lösung:**
  ```bash
  # Mehr Speicher für Docker Desktop zuweisen (mind. 4GB)
  # Oder mit --no-cache neu bauen
docker build --no-cache -t mailarchiver:my-fork-latest .
  ```

### Port 5000 bereits belegt
- **Lösung:** Anderen Port verwenden:
  ```bash
docker run -p 5001:5000 mailarchiver:my-fork-latest
  ```

### Datenbank-Connection fehlgeschlagen
- **Lösung:** Stellen Sie sicher, dass PostgreSQL läuft und die Verbindungskette korrekt ist:
  ```yaml
  ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey
  ```

---

## Nächste Schritte für dich

1. **Docker Desktop starten** (falls noch nicht laufen)
2. **Image bauen:** `docker build -t mailarchiver:my-fork-latest .`
3. **Testen:** `docker run -p 5000:5000 mailarchiver:my-fork-latest`
4. **Mit Compose starten:** `docker compose -f docker-compose.my-fork.yml up -d`

---

## Fragen?
- **Wie prüfe ich, ob mein PR enthalten ist?** → `git log --oneline` sollte deinen Commit `85106c8` zeigen
- **Wie sehe ich, was neu von Upstream ist?** → `git log upstream/main..main --oneline`
- **Wie gehe ich zurück?** → `git checkout main` und dann `git reset --hard HEAD~1` (Vorsicht!)

---

*Erstellt: 2026-08-13 | Stand: Upstream bis Commit c5b05f7*
