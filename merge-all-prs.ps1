<#PSScriptInfo
.VERSION 1.0
.AUTHOR  MailArchiver Fork Setup
.DESCRIPTION
    Merge alle deine PR-Branches in fix/progress-bar-decimal-culture
#>

# ============================================
# MailArchiver - Alle PRs mergen
# ============================================

Write-Host "🔄 Starte Merge aller PR-Branches..." -ForegroundColor Cyan

# 1. Zu deinem Haupt-PR-Branch wechseln
cd C:\Users\User\GIT\mail-archiver
git checkout fix/progress-bar-decimal-culture

# 2. Bulk-Delete PR mergen
Write-Host "📦 Merge feature/folder-bulk-delete..." -ForegroundColor Yellow
git merge feature/folder-bulk-delete -m "merge: add bulk delete feature from PR"

# 3. Docker-Compose PR mergen
Write-Host "🐳 Merge fix/docker-compose-local-dev-setup..." -ForegroundColor Yellow
git merge fix/docker-compose-local-dev-setup -m "merge: add docker-compose fixes from PR"

# 4. Alle Änderungen pushen
Write-Host "🚀 Push zu GitHub (startet Workflow)..." -ForegroundColor Green
git push origin fix/progress-bar-decimal-culture

Write-Host "" -ForegroundColor Cyan
Write-Host "✅ Fertig! Alle 3 PRs sind jetzt in fix/progress-bar-decimal-culture" -ForegroundColor Green
Write-Host "📦 Workflow baut automatisch neues Image mit ALL deinen Änderungen" -ForegroundColor Green
