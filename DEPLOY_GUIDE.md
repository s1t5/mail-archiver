# MailArchiver Fork - Komplette Deploy-Anleitung

## 📋 Zusammenfassung: Was du hast

✅ **Fork auf GitHub**: `https://github.com/Git-Usr123/mail-archiver`  
✅ **Upstream verbunden**: `s1t5/mail-archiver` (automatische Updates)  
✅ **PR-Branch**: `fix/progress-bar-decimal-culture` (deine Änderungen)  
✅ **Workflow**: `.github/workflows/build-and-push.yml` (automatisches Bauen)  
✅ **Image**: `ghcr.io/git-usr123/mailarchiver:my-fork-latest` (öffentlich)  

---

## 🎯 Schritt 1: Workflow zu GitHub pushen (EINMALIG)

**Führe diesen Befehl in PowerShell aus:**
```powershell
cd C:\Users\User\GIT\mail-archiver
git push origin fix/progress-bar-decimal-culture
```

**Was passiert dann?**
1. GitHub Actions startet automatisch
2. Docker Image wird auf GitHub gebaut (~5-10 Min)
3. Image wird zu **GHCR** gepusht
4. Dein Image ist unter `ghcr.io/git-usr123/mailarchiver:my-fork-latest` verfügbar

**Prüfen:**
- [Workflow-Status](https://github.com/Git-Usr123/mail-archiver/actions)
- [Dein Image in GHCR](https://github.com/users/Git-Usr123/packages/container/mailarchiver/versions)

---

## 💻 Schritt 2: Lokal testen (Windows / PowerShell)

### Option A: Mit PowerShell-Skript (empfohlen)
```powershell
# Image von GHCR pullen und starten
."C:\Users\User\GIT\mail-archiver\Test-Local.ps1" -UseGHCR

# Oder lokal bauen und starten
."C:\Users\User\GIT\mail-archiver\Test-Local.ps1"
```

**Skript-Optionen:**
| Option | Befehl | Beschreibung |
|--------|--------|--------------|
| Standard | `."Test-Local.ps1"` | Baut Image lokal und startet |
| GHCR | `."Test-Local.ps1" -UseGHCR` | Pullt Image von GHCR und startet |
| Skip Build | `."Test-Local.ps1" -SkipBuild` | Nutzt existierendes Image |
| Custom Tag | `."Test-Local.ps1" -ImageTag my-fork-20260813` | Nutzt spezifisches Tag |

### Option B: Manuell
```powershell
# 1. Image von GHCR pullen
docker pull ghcr.io/git-usr123/mailarchiver:my-fork-latest

# 2. Container starten
docker compose -f docker-compose.ghcr.yml up -d

# 3. App testen: http://localhost:5000
```

---

## 🐧 Schritt 3: Auf Linux-Server deployen

### Voraussetzungen
```bash
# Docker installieren (Ubuntu/Debian)
curl -fsSL https://get.docker.com | sh

# Docker Compose installieren
sudo apt install docker-compose-plugin

# Benutzer zu docker-Gruppe hinzufügen
sudo usermod -aG docker $USER
newgrp docker  # Oder neu einloggen
```

### Deploy-Skript kopieren
```bash
# Von deinem Windows-PC zum Linux-Server kopieren
# Beispiel mit scp (PowerShell):
scp C:\Users\User\GIT\mail-archiver\deploy-linux.sh user@dein-server:/home/user/
scp C:\Users\User\GIT\mail-archiver\docker-compose.ghcr.yml user@dein-server:/home/user/
scp C:\Users\User\GIT\mail-archiver\appsettings.json user@dein-server:/home/user/
```

### Deploy ausführen
```bash
# 1. Skript ausführbar machen
chmod +x deploy-linux.sh

# 2. Deployen (automatisch von GHCR)
./deploy-linux.sh

# 3. Status prüfen
./deploy-linux.sh logs
```

**Deploy-Optionen:**
| Befehl | Beschreibung |
|--------|--------------|
| `./deploy-linux.sh` | Deploys Image von GHCR |
| `./deploy-linux.sh local` | Baut Image lokal und deploys |
| `./deploy-linux.sh stop` | Stoppt alle Container |
| `./deploy-linux.sh logs` | Zeigt Application-Logs |
| `./deploy-linux.sh update` | Pullt neues Image und deploys |

---

## 🔄 Schritt 4: Upstream-Updates einpflegen (regelmäßig)

**Wenn du neue Änderungen aus dem Original-Repo (s1t5) haben willst:**

```powershell
cd C:\Users\User\GIT\mail-archiver

# 1. Upstream-Änderungen holen
git fetch upstream

# 2. In deinen main mergen
git checkout main
git merge upstream/main

# 3. Zu deinem Fork pushen
git push origin main

# 4. (Optional) Deinen PR-Branch aktualisieren
git checkout fix/progress-bar-decimal-culture
git merge main

# 5. Workflow baut automatisch neues Image
```

**Ergebnis:**
- Dein `my-fork-latest` Image enthält immer die neuesten Upstream-Updates
- Deine PR-Änderungen bleiben erhalten

---

## 🌐 Schritt 5: Production Setup (Linux-Server)

### Reverse Proxy (Nginx) für HTTPS
```bash
# Nginx installieren
sudo apt install nginx

# Konfiguration erstellen
sudo nano /etc/nginx/sites-available/mailarchiver
```

**Nginx-Konfiguration:**
```nginx
server {
    listen 80;
    server_name mailarchiver.deine-domain.de;
    
    location / {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

```bash
# Aktivieren und testen
sudo ln -s /etc/nginx/sites-available/mailarchiver /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx

# Let's Encrypt Zertifikat (für HTTPS)
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d mailarchiver.deine-domain.de
```

### Firewall konfigurieren
```bash
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 5000/tcp  # Falls direkt auf Port 5000 zugegriffen wird
sudo ufw enable
```

### Automatische Updates (Cron)
```bash
# Täglich um 3 Uhr morgens prüfen und updaten
crontab -e
```

**Cron-Eintrag:**
```cron
0 3 * * * cd /home/user && ./deploy-linux.sh update > /home/user/deploy.log 2>&1
```

---

## 📊 Überwachung & Wartung

### Image-Versionen prüfen
```bash
# Alle Tags deines Images
curl -s https://ghcr.io/v2/git-usr123/mailarchiver/tags/list | jq .tags

# Lokale Images
docker images | grep mailarchiver
```

### Container überwachen
```bash
# Status
docker compose -f docker-compose.ghcr.yml ps

# Ressourcen
 docker stats

# Logs
docker compose -f docker-compose.ghcr.yml logs -f
```

### Backup der Datenbank
```bash
# PostgreSQL Backup
docker compose -f docker-compose.ghcr.yml exec postgres pg_dump -U mailuser -d MailArchiver > backup.sql

# Volumes sichern
docker run --rm -v mailarchiver-fork_postgres-data:/volume alpine tar cvzf postgres-backup.tar.gz /volume
```

---

## 🔧 Troubleshooting

### Problem: Workflow fehlt in GitHub Actions
**Lösung:**
1. Prüfe, ob `.github/workflows/build-and-push.yml` in deinem Fork existiert
2. Push den Branch: `git push origin fix/progress-bar-decimal-culture`
3. Warte 1-2 Minuten und lade GitHub Actions Seite neu

### Problem: Image nicht in GHCR
**Lösung:**
1. Prüfe Workflow-Logs: [https://github.com/Git-Usr123/mail-archiver/actions](https://github.com/Git-Usr123/mail-archiver/actions)
2. Warte 5-10 Minuten nach erfolgreicher Workflow-Ausführung
3. Prüfe: [https://github.com/users/Git-Usr123/packages/container/mailarchiver](https://github.com/users/Git-Usr123/packages/container/mailarchiver)

### Problem: Container startet nicht
**Lösung:**
```bash
# Logs prüfen
docker compose -f docker-compose.ghcr.yml logs

# Container neu starten
docker compose -f docker-compose.ghcr.yml down
docker compose -f docker-compose.ghcr.yml up -d
```

### Problem: Port 5000 blockiert
**Lösung:**
```bash
# Anderen Port verwenden (in docker-compose.ghcr.yml)
# Ändere "5000:5000" zu "5001:5000" und starte neu
```

---

## 📁 Dateien-Übersicht

| Datei | Zweck | Pfad |
|-------|-------|------|
| `Test-Local.ps1` | PowerShell-Skript für lokale Tests | `C:\Users\User\GIT\mail-archiver\` |
| `deploy-linux.sh` | Bash-Skript für Linux-Deploy | `C:\Users\User\GIT\mail-archiver\` |
| `docker-compose.ghcr.yml` | Docker Compose für GHCR | `C:\Users\User\GIT\mail-archiver\` |
| `.github/workflows/build-and-push.yml` | GitHub Actions Workflow | `.github/workflows/` |
| `GHCR_GUIDE.md` | Detaillierte GHCR-Anleitung | `C:\Users\User\GIT\mail-archiver\` |

---

## 🎯 Zusammenfassung: Was du jetzt tun sollst

1. **✅ Workflow pushen** (1 Befehl in PowerShell)
2. **⏳ Warten** auf GitHub Actions (5-10 Min)
3. **🧪 Lokal testen** mit `Test-Local.ps1 -UseGHCR`
4. **🚀 Auf Server deployen** mit `deploy-linux.sh`

---

## 📞 Support

Falls etwas nicht funktioniert:
1. Prüfe die **Workflow-Logs** auf GitHub Actions
2. Lies die **GHCR_GUIDE.md** für detaillierte Anleitungen
3. Kontaktiere mich mit der **genauen Fehlermeldung**

---

*📅 Letzte Aktualisierung: 2026-08-13*
*🔧 Status: Bereit für Deploy*
