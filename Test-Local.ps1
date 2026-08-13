<#PSScriptInfo
.VERSION 1.0
.GUID    12345678-1234-1234-1234-123456789012
.AUTHOR  MailArchiver Fork Setup
.DESCRIPTION
    Test-Skript für dein MailArchiver Fork-Image
    Prüft Docker, baut Image, startet Container, zeigt Logs
#>

# ============================================
# MailArchiver Fork - PowerShell Test-Skript
# ============================================

param(
    [switch]$SkipBuild,      # Überspringt Docker Build (nur Starten)
    [switch]$UseGHCR,       # Nutzt GHCR-Image statt lokalem Build
    [string]$ImageTag = "my-fork-latest"
)

# Farben für Output
$Success = "\e[92m"
$Error = "\e[91m"
$Info = "\e[93m"
$Reset = "\e[0m"

function Write-Success { param($Message) Write-Host "$Success[$(Get-Date -Format 'HH:mm:ss')] $Message$Reset" }
function Write-ErrorMsg { param($Message) Write-Host "$Error[$(Get-Date -Format 'HH:mm:ss')] $Message$Reset" }
function Write-Info { param($Message) Write-Host "$Info[$(Get-Date -Format 'HH:mm:ss')] $Message$Reset" }

# ============================================
# VORAUSSETZUNGEN PRÜFEN
# ============================================

Write-Info "Prüfe Voraussetzungen..."

# Docker
try {
    $DockerVersion = docker --version 2>$null
    if (-not $DockerVersion) { throw "Docker nicht gefunden" }
    Write-Success "Docker: $DockerVersion"
} catch {
    Write-ErrorMsg "Docker ist nicht installiert oder läuft nicht!"
    Write-Info "Installation: https://docs.docker.com/desktop/install/windows-install/"
    exit 1
}

# Docker Desktop läuft?
try {
    $DockerRunning = docker info 2>$null
    if (-not $DockerRunning) { throw "Docker nicht gestartet" }
    Write-Success "Docker Desktop läuft"
} catch {
    Write-ErrorMsg "Docker Desktop ist nicht gestartet!"
    Write-Info "Starte Docker Desktop manuell und versuche es erneut"
    exit 1
}

# ============================================
# IMAGE BAUEN ODER PULLEN
# ============================================

if ($UseGHCR) {
    Write-Info "Pull Image von GHCR..."
    $ImageName = "ghcr.io/git-usr123/mailarchiver:$ImageTag"
    
    try {
        docker pull $ImageName 2>&1 | Write-Host
        Write-Success "Image $ImageName erfolgreich gepulled"
    } catch {
        Write-ErrorMsg "Fehler beim Pullen von GHCR!"
        Write-Info "Prüfe: https://github.com/users/Git-Usr123/packages/container/mailarchiver/versions"
        exit 1
    }
} else {
    if (-not $SkipBuild) {
        Write-Info "Baue Docker-Image lokal..."
        $ImageName = "mailarchiver:$ImageTag"
        
        try {
            docker build -t $ImageName . 2>&1 | Write-Host
            Write-Success "Image $ImageName erfolgreich gebaut"
        } catch {
            Write-ErrorMsg "Docker Build fehlgeschlagen!"
            Write-Info "Prüfe Dockerfile und Internetverbindung"
            exit 1
        }
    } else {
        $ImageName = "mailarchiver:$ImageTag"
        Write-Info "Nutze existierendes Image: $ImageName"
    }
}

# ============================================
# CONTAINER STARTEN
# ============================================

Write-Info "Starte Container mit docker-compose..."

# Prüfe, ob docker-compose.yml existiert
if (Test-Path "docker-compose.ghcr.yml") {
    $ComposeFile = "docker-compose.ghcr.yml"
    Write-Info "Nutze $ComposeFile (für GHCR)"
} elseif (Test-Path "docker-compose.my-fork.yml") {
    $ComposeFile = "docker-compose.my-fork.yml"
    Write-Info "Nutze $ComposeFile (für lokales Image)"
} else {
    $ComposeFile = "docker-compose.yml"
    Write-Info "Nutze Standard docker-compose.yml"
}

try {
    # Container herunterfahren (falls schon laufen)
    docker compose -f $ComposeFile down 2>&1 | Out-Null
    
    # Container starten
    docker compose -f $ComposeFile up -d 2>&1 | Write-Host
    Write-Success "Container erfolgreich gestartet!"
} catch {
    Write-ErrorMsg "Fehler beim Starten der Container!"
    Write-Info "Versuche: docker compose -f $ComposeFile up -d"
    exit 1
}

# ============================================
# STATUS PRÜFEN
# ============================================

Write-Info "Prüfe Container-Status..."

try {
    $Containers = docker compose -f $ComposeFile ps 2>&1
    Write-Host $Containers
    
    # Prüfe, ob Container läuft
    if ($Containers -match "Up") {
        Write-Success "Container läuft!"
    } else {
        Write-ErrorMsg "Container läuft nicht - prüfe Logs"
    }
} catch {
    Write-ErrorMsg "Konnte Container-Status nicht prüfen"
}

# ============================================
# LOGS ANZEIGEN
# ============================================

Write-Info "Zeige Logs (Drücke STRG+C zum Beenden)..."
Write-Info "Container sollte in ~30 Sekunden bereit sein"
Write-Info "App verfügbar unter: http://localhost:5000"

try {
    docker compose -f $ComposeFile logs -f mailarchive-app 2>&1 | Write-Host
} catch {
    Write-ErrorMsg "Konnte Logs nicht anzeigen"
    Write-Info "Versuche: docker compose -f $ComposeFile logs -f"
}
