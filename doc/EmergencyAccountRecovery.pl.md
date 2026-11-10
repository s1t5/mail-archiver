# 🚨 Emergency Account Recovery {#emergency-account-recovery}

[← Powrót do Indeksu Dokumentacji](index.md)

## 📋 Przegląd {#overview}

Niniejszy przewodnik zawiera szczegółowe instrukcje dotyczące odzyskiwania dostępu do aplikacji Mail Archiver w przypadku zapomnienia hasła administratora. Ta procedura awaryjna pozwala odzyskać dostęp do systemu bez utraty jakichkolwiek danych.

> 📝 **Wymagania dotyczące wersji**: Ta metoda odzyskiwania jest dostępna w wersji Mail Archiver 2601.1 i nowszych.

## ⚠️ Ważna uwaga dotycząca bezpieczeństwa {#important-security-notice}

Tę metodę odzyskiwania należy stosować wyłącznie w sytuacjach awaryjnych, gdy nie można uzyskać dostępu do swojego konta administratora. Wymaga ona tymczasowej modyfikacji konfiguracji aplikacji i powinna zostać natychmiast cofnięta po odzyskaniu hasła.

## 🛠️ Kroki odzyskiwania {#recovery-steps}

### 1. Utwórz awaryjne konto administratora {#1-create-emergency-administrator-account}

Zmodyfikuj swoje zmienne środowiskowe `appsettings.json` lub Docker, aby utworzyć tymczasowe awaryjne konto administratora:

**Dla Docker Compose (Zalecane):**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Emergency admin account
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempPass123!
      # ... other settings remain unchanged
```

**Dla bezpośredniej modyfikacji appsettings.json:**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. Uruchom ponownie aplikację/kontener {#2-restart-the-applicationcontainer}

Po zmodyfikowaniu konfiguracji uruchom ponownie aplikację:

**Dla Docker Compose:**
```bash
docker compose down
docker compose up -d
```

**Dla bezpośredniej aplikacji:**
```bash
# Restart your application service
```

### 3. Zaloguj się przy użyciu awaryjnego konta {#3-login-with-emergency-account}

1. Uzyskaj dostęp do interfejsu webowego Mail Archiver
2. Zaloguj się, używając awaryjnych danych uwierzytelniających:
   - **Username**: `emergency_admin` (lub to, co ustawisz)
   - **Password**: `TempPass123!` (lub to, co ustawisz)

### 4. Zresetuj oryginalne hasło administratora {#4-reset-original-administrator-password}

1. Przejdź do sekcji "Users" w menu głównym
2. Znajdź swoje oryginalne konto administratora na liście użytkowników
3. Kliknij "Edit" dla tego użytkownika
4. Zmień hasło na nowe, bezpieczne hasło
5. Kliknij "Save Changes"

### 5. Przywróć oryginalną konfigurację {#5-restore-original-configuration}

Cofnij zmiany w konfiguracji do oryginalnej nazwy użytkownika administratora:

**Dla Docker Compose:**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Original admin account
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword
      # ... other settings
```

**Dla bezpośredniego appsettings.json:**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. Ponownie uruchom aplikację {#6-restart-application-again}

Uruchom ponownie aplikację z przywróconą konfiguracją:

**Dla Docker Compose:**
```bash
docker compose down
docker compose up -d
```

## 🎯 Przykład: Kompletny proces odzyskiwania w Docker Compose {#example-complete-docker-compose-recovery-process}

Oto kompletny przykład procesu awaryjnego odzyskiwania przy użyciu Docker Compose:

### Krok 1: Awaryjna konfiguracja {#step-1-emergency-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # EMERGENCY ADMIN ACCOUNT
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempEmergencyPass2026!
      
      # Other settings remain unchanged...
      - MailSync__IntervalMinutes=15
      - TimeZone__DisplayTimeZoneId=Europe/Berlin
      # ... rest of configuration
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Krok 2: Restart i logowanie {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### Krok 3: Resetowanie oryginalnego hasła {#step-3-reset-original-password}
1. Zaloguj się do interfejsu webowego przy użyciu awaryjnych danych uwierzytelniających
2. Przejdź do sekcji Users
3. Edytuj oryginalnego użytkownika admin i ustaw nowe hasło
4. Zapisz zmiany

### Krok 4: Przywrócenie normalnej konfiguracji {#step-4-restore-normal-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # RESTORED ORIGINAL ADMIN ACCOUNT
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword2026!
      
      # ... rest of configuration unchanged
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Krok 5: Końcowy restart {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 Utrata klucza szyfrującego dane uwierzytelniające {#lost-credential-encryption-key}

Jeśli `Security__CredentialEncryptionKey` (szyfrowanie danych uwierzytelniających w spoczynku) zostanie utracony, przechowywane
dane uwierzytelniające konta pocztowego nie mogą zostać odszyfrowane. Aplikacja nie ulega awarii:
dotknięte wartości są traktowane jako "brak danych uwierzytelniających", a w dzienniku zostaje zapisane ostrzeżenie. Aby odzyskać dostęp:

1. Przywróć klucz z kopii zapasowej / menedżera sekretów i uruchom ponownie aplikację. Dane
   uwierzytelniające zaczną działać natychmiast, nie jest wymagana żadna zmiana danych.
2. Jeśli klucz został utracony na stałe, zaszyfrowanych danych uwierzytelniających nie można odzyskać.
   Dotknięte konta można łatwo zidentyfikować: ich synchronizacja kończy się niepowodzeniem, a ostrzeżenie
   o deszyfrowaniu jest zapisywane w dzienniku aplikacji (i pojawia się jako problem z synchronizacją w UI).
3. Otwórz każde dotknięte konto w **Mail Accounts → Edit** i ponownie wprowadź jego dane uwierzytelniające:
   hasło IMAP, client secret M365 lub ponownie autoryzuj konto (MSA). Zapisanie formularza
   ponownie szyfruje dane uwierzytelniające przy użyciu skonfigurowanego klucza — nie jest wymagana
   ręczna zmiana w bazie danych.

> 💡 Klucz nigdy nie jest przechowywany w bazie danych, więc same kopie zapasowe bazy danych nie są wystarczające —
> zawsze rób osobną kopię zapasową klucza.

## 🔒 Najlepsze praktyki bezpieczeństwa {#security-best-practices}

1. **Używaj silnych tymczasowych haseł**: Spraw, aby Twoje awaryjne hasło było złożone i unikalne
2. **Zmień je natychmiast**: Zresetuj awaryjne hasło natychmiast po odzyskaniu dostępu
