# 🧪 Korzystanie z wersji rozwojowych (Dev Tag) {#using-development-versions-dev-tag}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik wyjaśnia, jak przełączyć się ze stabilnej wersji `latest` na wersję rozwojową `dev` programu Mail Archiver w celu przetestowania poprawek błędów i nowych funkcji. Jest to szczególnie przydatne dla użytkowników, którzy chcą przetestować poprawki przed ich oficjalnym wydaniem.

## ⚠️ Ważne uwagi {#important-considerations}

Przed przełączeniem się na wersję rozwojową należy wziąć pod uwagę następujące kwestie:

- **Wersje rozwojowe mogą zawierać błędy lub niestabilne funkcje**, których nie ma w stabilnym wydaniu
- **Zawsze wykonuj kopię zapasową swoich danych** przed przełączeniem się na wersję rozwojową
- **Używaj wersji rozwojowych wyłącznie do testów**, a nie w środowiskach produkcyjnych
- **Zgłaszaj wszelkie napotkane problemy**, aby pomóc w ulepszeniu oprogramowania

## 🛑 Zatrzymywanie kontenera (Zalecane) {#stopping-the-container-recommended}

Przed przełączeniem się na wersję rozwojową zaleca się zatrzymanie kontenera:

```bash
docker compose down
```

## 🔄 Przełączanie na Dev Tag {#switching-to-the-dev-tag}

1. Otwórz plik `docker-compose.yml` w edytorze tekstu

2. Znajdź sekcję usługi `mailarchive-app` i zmień tag obrazu z `latest` na `dev`:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. Zapisz zmiany w pliku `docker-compose.yml`

## 📥 Pobieranie obrazu rozwojowego {#pulling-the-development-image}

Po zaktualizowaniu pliku docker-compose należy pobrać obraz rozwojowy:

```bash
docker compose pull
```

To polecenie pobierze najnowszą wersję rozwojową obrazu Mail Archiver.

## ▶️ Uruchamianie kontenera z nową wersją {#starting-the-container-with-the-new-version}

Po pobraniu nowego obrazu uruchom kontener, aby korzystać z wersji rozwojowej:

```bash
docker compose up -d
```

## 🔁 Powrót do stabilnego wydania {#switching-back-to-stable-release}

Jeśli chcesz wrócić do stabilnej wersji, wykonaj następujące kroki:

1. Zatrzymaj kontener:
```bash
docker compose down
```

2. Edytuj `docker-compose.yml` i zmień obraz z powrotem na:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. Pobierz najnowszy obraz:
```bash
docker compose pull
```

4. Uruchom kontener:
```bash
docker compose up -d
```

## 📝 Dodatkowe uwagi {#additional-notes}

- Wersje rozwojowe są aktualizowane częściej niż stabilne wydania
- Jeśli napotkasz problemy, sprawdź logi pod kątem komunikatów o błędach:
  ```bash
  docker compose logs mailarchive-app
  ```
- Możesz sprawdzić, która wersja jest aktualnie uruchomiona, sprawdzając informacje o obrazie kontenera:
  ```bash
  docker compose images
  ```
