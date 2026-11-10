# 🚨 Recupero Account di Emergenza {#emergency-account-recovery}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida fornisce istruzioni dettagliate per recuperare l'accesso all'applicazione Mail Archiver quando si è dimenticata la password dell'amministratore. Questa procedura di emergenza consente di riacquistare l'accesso al sistema senza perdere alcun dato.

> 📝 **Requisito di Versione**: Questo metodo di recupero è disponibile nella versione 2601.1 di Mail Archiver e successive.

## ⚠️ Avviso di Sicurezza Importante {#important-security-notice}

Questo metodo di recupero deve essere utilizzato solo in situazioni di emergenza in cui non è possibile accedere al proprio Account amministratore. Richiede una modifica temporanea della configurazione dell'applicazione e deve essere annullata immediatamente dopo il recupero della password.

## 🛠️ Passaggi per il Recupero {#recovery-steps}

### 1. Creare un Account Amministratore di Emergenza {#1-create-emergency-administrator-account}

Modificare le variabili d'ambiente `appsettings.json` o Docker per creare un Account amministratore di emergenza temporaneo:

**Per Docker Compose (Consigliato):**
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

**Per la modifica diretta di appsettings.json:**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. Riavviare l'Applicazione/Container {#2-restart-the-applicationcontainer}

Dopo aver modificato la configurazione, riavviare l'applicazione:

**Per Docker Compose:**
```bash
docker compose down
docker compose up -d
```

**Per l'applicazione diretta:**
```bash
# Restart your application service
```

### 3. Effettuare il Login con l'Account di Emergenza {#3-login-with-emergency-account}

1. Accedere all'interfaccia web di Mail Archiver
2. Effettuare il login utilizzando le credenziali di emergenza:
   - **Username**: `emergency_admin` (o qualunque cosa sia stata impostata)
   - **Password**: `TempPass123!` (o qualunque cosa sia stata impostata)

### 4. Reimpostare la Password dell'Amministratore Originale {#4-reset-original-administrator-password}

1. Navigare nella sezione "Users" dal menu principale
2. Trovare l'Account amministratore originale nell'elenco utenti
3. Cliccare su "Edit" per quell'utente
4. Cambiare la password con una nuova password sicura
5. Cliccare su "Save Changes"

### 5. Ripristinare la Configurazione Originale {#5-restore-original-configuration}

Annullare le modifiche alla configurazione tornando allo username dell'amministratore originale:

**Per Docker Compose:**
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

**Per appsettings.json diretto:**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. Riavviare nuovamente l'Applicazione {#6-restart-application-again}

Riavviare l'applicazione con la configurazione ripristinata:

**Per Docker Compose:**
```bash
docker compose down
docker compose up -d
```

## 🎯 Esempio: Processo di Recupero Docker Compose Completo {#example-complete-docker-compose-recovery-process}

Ecco un esempio completo del processo di recupero di emergenza utilizzando Docker Compose:

### Step 1: Configurazione di Emergenza {#step-1-emergency-configuration}
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

### Step 2: Riavvio e Login {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### Step 3: Reimpostazione Password Originale {#step-3-reset-original-password}
1. Effettuare il login all'interfaccia web con le credenziali di emergenza
2. Andare nella sezione Users
3. Modificare l'utente admin originale e impostare la nuova password
4. Salvare le modifiche

### Step 4: Ripristino della Configurazione Normale {#step-4-restore-normal-configuration}
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

### Step 5: Riavvio Finale {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 Chiave di Crittografia delle Credenziali Perse {#lost-credential-encryption-key}

Se `Security__CredentialEncryptionKey` (crittografia delle credenziali a riposo) viene persa, le credenziali dell'Account mail memorizzate non possono più essere decifrate. L'applicazione non va in crash: i valori interessati vengono trattati come "nessuna credenziale" e viene registrato un avviso nel Registro di controllo. Per recuperare:

1. Ripristinare la chiave dal proprio Backup / secret manager e riavviare l'applicazione. Le credenziali torneranno a funzionare immediatamente, non sono richiesti cambiamenti ai dati.
2. Se la chiave è persa permanentemente, le credenziali crittografate non possono essere recuperate. Gli Account interessati sono facili da identificare: la loro Sincronizzazione fallisce e un avviso di decrittazione viene scritto nel log dell'applicazione (e appare come un problema di sincronizzazione nell'interfaccia utente).
3. Aprire ogni Account interessato sotto **Mail Accounts → Edit** e reinserire le proprie credenziali: la password IMAP, il client secret di M365, o ri-autorizzare l'Account (MSA). Il salvataggio del modulo ricripta la credenziale con la chiave ora configurata — non sono necessarie modifiche manuali al database.

> 💡 La chiave non viene mai memorizzata nel database, quindi i Backup del database da soli non sono sufficienti — eseguire sempre il Backup della chiave separatamente.

## 🔒 Best Practice di Sicurezza {#security-best-practices}

1. **Utilizzare Password Temporanee Forti**: Rendere la password di emergenza complessa e univoca
2. **Cambiarla Immediatamente**: Reimpostare la password di emergenza non appena si riacquista l'accesso
