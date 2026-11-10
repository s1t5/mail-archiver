# 🚨 Notfall-Konto-Wiederherstellung {#emergency-account-recovery}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden bietet detaillierte Anweisungen zur Wiederherstellung des Zugriffs auf die Mail Archiver Anwendung, wenn Sie das Administratorpasswort vergessen haben. Dieses Notfallverfahren ermöglicht es Ihnen, den Zugriff auf Ihr System wiederzuerlangen, ohne Daten zu verlieren.

> 📝 **Versionsanforderung**: Diese Wiederherstellungsmethode ist in Mail Archiver Version 2601.1 und höher verfügbar.

## ⚠️ Wichtiger Sicherheitshinweis {#important-security-notice}

Diese Wiederherstellungsmethode sollte nur in Notsituationen verwendet werden, wenn Sie keinen Zugriff auf Ihr Administrator-Konto haben. Sie erfordert eine vorübergehende Änderung Ihrer Anwendungskonfiguration und sollte unmittelbar nach der Passwort-Wiederherstellung rückgängig gemacht werden.

## 🛠️ Wiederherstellungsschritte {#recovery-steps}

### 1. Notfall-Administrator-Konto erstellen {#1-create-emergency-administrator-account}

Ändern Sie Ihre `appsettings.json` oder Docker-Umgebungsvariablen, um ein temporäres Notfall-Administrator-Konto zu erstellen:

**Für Docker Compose (Empfohlen):**
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

**Für die direkte Änderung der appsettings.json:**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. Anwendung/Container neu starten {#2-restart-the-applicationcontainer}

Nachdem Sie die Konfiguration geändert haben, starten Sie Ihre Anwendung neu:

**Für Docker Compose:**
```bash
docker compose down
docker compose up -d
```

**Für die direkte Anwendung:**
```bash
# Restart your application service
```

### 3. Anmeldung mit dem Notfall-Konto {#3-login-with-emergency-account}

1. Rufen Sie die Mail Archiver Web-Oberfläche auf
2. Melden Sie sich mit den Notfall-Zugangsdaten an:
   - **Benutzername**: `emergency_admin` (oder was auch immer Sie festgelegt haben)
   - **Passwort**: `TempPass123!` (oder was auch immer Sie festgelegt haben)

### 4. Passwort des ursprünglichen Administrators zurücksetzen {#4-reset-original-administrator-password}

1. Navigieren Sie im Hauptmenü zum Abschnitt "Users"
2. Suchen Sie Ihr ursprüngliches Administrator-Konto in der Benutzerliste
3. Klicken Sie bei diesem Benutzer auf "Edit"
4. Ändern Sie das Passwort in ein neues, sicheres Passwort
5. Klicken Sie auf "Save Changes"

### 5. Ursprüngliche Konfiguration wiederherstellen {#5-restore-original-configuration}

Setzen Sie Ihre Konfigurationsänderungen auf den ursprünglichen Administrator-Benutzernamen zurück:

**Für Docker Compose:**
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

**Für die direkte appsettings.json:**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. Anwendung erneut neu starten {#6-restart-application-again}

Starten Sie die Anwendung mit der wiederhergestellten Konfiguration neu:

**Für Docker Compose:**
```bash
docker compose down
docker compose up -d
```

## 🎯 Beispiel: Vollständiger Docker Compose Wiederherstellungsprozess {#example-complete-docker-compose-recovery-process}

Hier ist ein vollständiges Beispiel für den Notfall-Wiederherstellungsprozess unter Verwendung von Docker Compose:

### Schritt 1: Notfallkonfiguration {#step-1-emergency-configuration}
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

### Schritt 2: Neustart und Anmeldung {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### Schritt 3: Originalpasswort zurücksetzen {#step-3-reset-original-password}
1. Anmeldung an der Weboberfläche mit Notfall-Anmeldedaten
2. Zum Bereich Benutzer navigieren
3. Den ursprünglichen Admin-Benutzer bearbeiten und ein neues Passwort festlegen
4. Änderungen speichern

### Schritt 4: Normale Konfiguration wiederherstellen {#step-4-restore-normal-configuration}
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

### Schritt 5: Abschließender Neustart {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 Verlorener Verschlüsselungsschlüssel für Anmeldedaten {#lost-credential-encryption-key}

Wenn `Security__CredentialEncryptionKey` (Verschlüsselung der Anmeldedaten im Ruhezustand) verloren geht, können die gespeicherten
Anmeldedaten des Mail-Kontos nicht mehr entschlüsselt werden. Die Anwendung stürzt **nicht** ab: die
betroffenen Werte werden als „keine Anmeldedaten“ behandelt und eine Warnung wird protokolliert. Zur Wiederherstellung:

1. Stellen Sie den Schlüssel aus Ihrer Sicherung / Ihrem Secret Manager wieder her und starten Sie die Anwendung neu. Die
   Anmeldedaten funktionieren sofort wieder, es sind keine Datenänderungen erforderlich.
2. Wenn der Schlüssel dauerhaft verloren geht, können die verschlüsselten Anmeldedaten nicht wiederhergestellt werden. Betroffene
   Konten lassen sich leicht identifizieren: Ihre Synchronisierung schlägt fehl und eine Entschlüsselungswarnung wird
   im Anwendungsprotokoll geschrieben (und erscheint als Synchronisierungsproblem in der Benutzeroberfläche).
3. Öffnen Sie jedes betroffene Konto unter **Mail Accounts → Edit** und geben Sie die Anmeldedaten erneut ein:
   das IMAP-Passwort, das M365 Client Secret, oder autorisieren Sie das Konto neu (MSA). Durch Speichern des
   Formulars werden die Anmeldedaten mit dem nun konfigurierten Schlüssel neu verschlüsselt — manuelle Datenbankänderungen
   sind nicht erforderlich.

> 💡 Der Schlüssel wird niemals in der Datenbank gespeichert, daher reichen Datenbank-Sicherungen allein nicht aus —
> sichern Sie den Schlüssel immer separat.

## 🔒 Best Practices für die Sicherheit {#security-best-practices}

1. **Verwenden Sie starke temporäre Passwörter**: Gestalten Sie Ihr Notfallpasswort komplex und einzigartig
2. **Ändern Sie es sofort**: Setzen Sie das Notfallpasswort zurück, sobald Sie wieder Zugriff haben
