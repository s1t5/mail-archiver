@echo off
REM ============================================================
REM Skript zum Bauen und Ausfuehren deines eigenen MailArchiver-Images
REM ============================================================

REM Schritt 1: Docker-Image bauen
echo [1/4] Baue Docker-Image aus deinem Fork (mit deinen Aenderungen)...
docker build -t mailarchiver:my-fork-latest .

REM Schritt 2: Image testen
echo [2/4] Teste das Image lokal...
docker run --rm -p 5000:5000 \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey" \
  mailarchiver:my-fork-latest

REM Schritt 3: Container mit docker-compose starten
echo.
echo [3/4] Starte Container mit docker-compose (mit PostgreSQL)...
echo   (Druecke STRG+C zum Beenden des Tests)
pause

echo.
echo [4/4] Starte Production-Setup mit docker-compose.my-fork.yml...
docker compose -f docker-compose.my-fork.yml down
docker compose -f docker-compose.my-fork.yml up -d

echo.
echo Fertig! Application laeuft auf http://localhost:5000
echo.
echo Zum Stoppen:
echo   docker compose -f docker-compose.my-fork.yml down
