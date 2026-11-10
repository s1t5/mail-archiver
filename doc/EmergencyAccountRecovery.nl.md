# 🚨 Noodherstel van Account {#emergency-account-recovery}

[← Terug naar Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Deze gids biedt gedetailleerde instructies voor het herstellen van de toegang tot de Mail Archiver-applicatie wanneer u het wachtwoord van de administrator bent vergeten. Deze noodprocedure stelt u in staat om weer toegang te krijgen tot uw systeem zonder gegevens te verliezen.

> 📝 **Versievereiste**: Deze herstelmethode is beschikbaar in Mail Archiver versie 2601.1 en later.

## ⚠️ Belangrijke beveiligingsmededeling {#important-security-notice}

Deze herstelmethode mag alleen worden gebruikt in noodsituaties waarin u geen toegang heeft tot uw administrator Account. Het vereist tijdelijke wijziging van uw applicatieconfiguratie en moet onmiddellijk na het herstellen van het wachtwoord worden teruggedraaid.

## 🛠️ Herstelstappen {#recovery-steps}

### 1. Maak nood-administrator Account aan {#1-create-emergency-administrator-account}

Wijzig uw `appsettings.json` of Docker-omgevingsvariabelen om een tijdelijke nood-administrator Account aan te maken:

**Voor Docker Compose (Aanbevolen):**
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

**Voor directe appsettings.json wijziging:**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. Start de applicatie/container opnieuw op {#2-restart-the-applicationcontainer}

Na het wijzigen van de configuratie, start u uw applicatie opnieuw op:

**Voor Docker Compose:**
```bash
docker compose down
docker compose up -d
```

**Voor directe applicatie:**
```bash
# Restart your application service
```

### 3. Inloggen met nood-Account {#3-login-with-emergency-account}

1. Open de webinterface van Mail Archiver
2. Log in met de noodgegevens:
   - **Gebruikersnaam**: `emergency_admin` (of wat u heeft ingesteld)
   - **Wachtwoord**: `TempPass123!` (of wat u heeft ingesteld)

### 4. Origineel administrator wachtwoord resetten {#4-reset-original-administrator-password}

1. Navigeer naar de sectie "Users" in het hoofdmenu
2. Zoek uw originele administrator Account in de gebruikerslijst
3. Klik op "Edit" voor die gebruiker
4. Wijzig het wachtwoord naar een nieuw veilig wachtwoord
5. Klik op "Save Changes"

### 5. Originele configuratie herstellen {#5-restore-original-configuration}

Zet uw configuratie wijzigingen terug naar de originele administrator gebruikersnaam:

**Voor Docker Compose:**
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

**Voor directe appsettings.json:**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. Applicatie opnieuw opstarten {#6-restart-application-again}

Start de applicatie opnieuw op met de herstelde configuratie:

**Voor Docker Compose:**
```bash
docker compose down
docker compose up -d
```

## 🎯 Voorbeeld: Volledig Docker Compose herstelproces {#example-complete-docker-compose-recovery-process}

Hier is een volledig voorbeeld van het noodherstelproces met behulp van Docker Compose:

### Stap 1: Noodconfiguratie {#step-1-emergency-configuration}
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

### Stap 2: Opnieuw opstarten en inloggen {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### Stap 3: Origineel wachtwoord resetten {#step-3-reset-original-password}
1. Log in op de webinterface met de noodgegevens
2. Ga naar de Users sectie
3. Bewerk de originele admin gebruiker en stel een nieuw wachtwoord in
4. Sla wijzigingen op

### Stap 4: Normale configuratie herstellen {#step-4-restore-normal-configuration}
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

### Stap 5: Definitieve herstart {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 Verloren encryptiesleutel voor gegevens {#lost-credential-encryption-key}

Als `Security__CredentialEncryptionKey` (credential encryptie bij rust) verloren gaat, kunnen de opgeslagen
mail Account gegevens niet langer worden gedecrypt. De applicatie crasht **niet**: de
beïnvloede waarden worden behandeld als "geen gegevens" en er wordt een waarschuwing gelogd. Om te herstellen:

1. Herstel de sleutel uit uw Back-up / secret manager en start de applicatie opnieuw op. De
   gegevens werken onmiddellijk weer, er zijn geen wijzigingen in de gegevens vereist.
2. Als de sleutel permanent verloren is gegaan, kunnen de versleutelde gegevens niet worden hersteld.
   Beïnvloede Accounts zijn eenvoudig te identificeren: hun Synchronisatie mislukt en een decryptie-waarschuwing
   wordt naar het applicatielogboek geschreven (en verschijnt als een synchronisatieprobleem in de UI).
3. Open elk beïnvloed Account onder **Mail Accounts → Edit** en voer de gegevens opnieuw in:
   het IMAP-wachtwoord, het M365 client secret, of herautoriseer het Account (MSA). Het opslaan van het
   formulier versleutelt de gegevens opnieuw met de nu geconfigureerde sleutel — handmatige wijzigingen in de
   database zijn niet nodig.

> 💡 De sleutel wordt nooit in de database opgeslagen, dus database Back-ups alleen zijn niet voldoende —
> maak de sleutel altijd apart een Back-up.

## 🔒 Best practices voor beveiliging {#security-best-practices}

1. **Gebruik sterke tijdelijke wachtwoorden**: Maak uw noodwachtwoord complex en uniek
2. **Wijzig het onmiddellijk**: Reset het noodwachtwoord zodra u weer toegang heeft hersteld
