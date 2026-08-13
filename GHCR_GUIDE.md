# GitHub Container Registry (GHCR) - Schnelleinrichtung

## ✅ Was ist schon fertig

Ich habe für dich erstellt:
- **Workflow-Datei**: `.github/workflows/build-and-push.yml`
- **Docker Compose für GHCR**: `docker-compose.ghcr.yml`
- **Automatische Tags**:
  - `my-fork-latest` (immer aktuell)
  - `my-fork-<sha>` (z. B. `my-fork-a1b2c3d`)
  - `fix-progress-bar-decimal-culture` (Branch-Name)

---

## 🚀 Einmalige Einrichtung (5 Minuten)

### Schritt 1: Workflow zu GitHub pushen
```bash
cd /c/Users/User/GIT/mail-archiver

# Workflow und docker-compose zu GitHub pushen
git add .github/workflows/build-and-push.yml docker-compose.ghcr.yml
git commit -m "Add GitHub Actions workflow for GHCR"
git push origin main
```

**Wichtig:** 
- Der Workflow nutzt **automatisch** `GITHUB_TOKEN` (keine manuellen Secrets nötig!)
- GHCR ist für **öffentliche Repos kostenlos**

---

### Schritt 2: Workflow auslösen
Sobald du gepusht hast, startet GitHub Actions automatisch:

1. **Gehe zu:** `https://github.com/Git-Usr123/mail-archiver/actions`
2. **Wähle den Workflow:** "Build and Push Docker Image to GHCR"
3. **Warte 5-10 Minuten** bis der Build abgeschlossen ist

---

## 📦 Dein Image in GHCR

Nach erfolgreicher Ausführung findest du dein Image hier:
```
https://github.com/users/Git-Usr123/packages/container/mailarchiver/versions
```

**Image-URLs:**
```
ghcr.io/git-usr123/mailarchiver:my-fork-latest
ghcr.io/git-usr123/mailarchiver:my-fork-a1b2c3d  # spezifischer Commit
ghcr.io/git-usr123/mailarchiver:fix-progress-bar-decimal-culture  # Branch-Name
```

---

## 🏃 Lokales Starten mit GHCR-Image

### Option A: Mit docker-compose
```bash
# Starte alles (App + PostgreSQL)
docker compose -f docker-compose.ghcr.yml up -d

# Stoppen
docker compose -f docker-compose.ghcr.yml down

# Logs anzeigen
docker compose -f docker-compose.ghcr.yml logs -f mailarchive-app
```

### Option B: Direkt mit Docker
```bash
# Image pullen
docker pull ghcr.io/git-usr123/mailarchiver:my-fork-latest

# Container starten
docker run -d -p 5000:5000 \
  -v ./appsettings.json:/app/appsettings.json \
  -v ./logs:/app/logs \
  -v ./data-protection-keys:/app/DataProtection-Keys \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey" \
  --name mailarchiver \
  ghcr.io/git-usr123/mailarchiver:my-fork-latest
```

---

## 🔄 Automatische Updates

Der Workflow **läuft automatisch** bei:
- ✅ **Push zu `main`** → Baut neues `my-fork-latest`
- ✅ **Push zu `fix/progress-bar-decimal-culture`** → Baut Branch-spezifisches Image
- ✅ **Neue Tags** → Baut Tag-spezifisches Image

**Beispiel:**
1. Du mergest `upstream/main` in deinen `main`:
   ```bash
   git merge upstream/main
   git push origin main
   ```
2. GitHub Actions baut automatisch ein neues Image mit allen Upstream-Updates
3. Dein `my-fork-latest` Tag wird aktualisiert

---

## 📊 Workflow Überwachung

| Aktion | Befehl / URL |
|--------|-------------|
| **Workflow-Logs** | `https://github.com/Git-Usr123/mail-archiver/actions` |
| **Image anzeigen** | `https://github.com/users/Git-Usr123/packages/container/mailarchiver` |
| **Manuell auslösen** | Workflow → "Run workflow" → Branch auswählen |
| **Lokales Image löschen** | `docker rmi ghcr.io/git-usr123/mailarchiver:my-fork-latest` |

---

## 🔐 Zugriff auf private Images (falls nötig)

Falls dein Fork **privat** ist:

1. **GitHub Token erstellen** (Settings → Developer Settings → Personal Access Tokens)
   - Scope: `read:packages`
   - Scope: `write:packages`

2. **Docker login** (lokal, falls du das Image pullen willst):
   ```bash
   echo GITHUB_TOKEN | docker login ghcr.io -u Git-Usr123 --password-stdin
   ```

---

## 💡 Wichtige Hinweise

### 1. GHCR Rate Limits
- **Anonym:** 60 Requests/Stunde
- **Authentifiziert:** 5.000 Requests/Stunde
→ **Kein Problem** für normale Nutzung

### 2. Image Retention
- **Standard:** Images werden **90 Tage** behalten
- **Änderbar:** Repository Settings → Packages → Package Retention

### 3. Image Größe
- Dein Image wird ca. **200-400MB** groß sein (.NET 10.0 + Abhängigkeiten)
- GitHub bietet **unbegrenzten Speicher** für öffentliche Packages

---

## ❓ FAQ

### Wie prüfe ich, ob der Build erfolgreich war?
→ Gehe zu: `https://github.com/Git-Usr123/mail-archiver/actions`
→ Klicke auf den letzten Workflow-Run
→ Prüfe, ob alle Steps grün sind

### Wie pull ich das Image manuell?
```bash
docker pull ghcr.io/git-usr123/mailarchiver:my-fork-latest
```

### Wie sehe ich alle Tags meines Images?
→ `https://github.com/users/Git-Usr123/packages/container/mailarchiver/versions`

### Kann ich das Image privat halten?
Ja! Ändere die Sichtbarkeit deines Forks zu **Private** in den Repository Settings.

---

## 🎯 Nächste Schritte für dich

1. **Workflow pushen:**
   ```bash
   git add .github/workflows/build-and-push.yml docker-compose.ghcr.yml
   git commit -m "Add GHCR workflow"
   git push origin main
   ```

2. **Warten** bis der Workflow durchläuft (ca. 5-10 Minuten)

3. **Image testen:**
   ```bash
   docker compose -f docker-compose.ghcr.yml up -d
   ```

4. **App testen:** `http://localhost:5000`

---

## 📞 Hilfe

Falls etwas schiefgeht:
- **Workflow fehlt?** → Prüfe, ob `.github/workflows/build-and-push.yml` existiert
- **Build fehlgeschlagen?** → Klicke auf den Workflow-Run und prüfe die Logs
- **Image nicht gefunden?** → Warte 5 Minuten und lade die Seite neu

---

*📅 Letzte Aktualisierung: 2026-08-13*
