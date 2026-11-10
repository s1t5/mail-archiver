# 🚨 Nujna obnavitev za zaledno pridobitev računa {#emergency-account-recovery}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodič nudi podrobna navodila za pridobitev dostopa do aplikacije Mail Archiver, ko ste pozabili geslo administratorja. Ta izredna postopanja vam omogočata ponovno pridobitev dostopa do sistema brez izgube podatkov.

> 📝 **Zahteva po različici**: Ta metoda pridobitve je na voljo v različici Mail Archiver 2601.1 in novejših.

## ⚠️ Pomembno obvestilo o varnosti {#important-security-notice}

To metodo pridobitve uporabljajte le v izrednih situacijah, ko ne morete dostopati do svojega administratorskega računa. Zahteva začasno spremembo konfiguracije vaše aplikacije in jo je treba takoj po pridobitvi gesla ponovno urediti v prvotno stanje.

## 🛠️ Koraki za pridobitev dostopa {#recovery-steps}

### 1. Ustvarite izredni administratorski račun {#1-create-emergency-administrator-account}

Spremenite svoje `appsettings.json` ali Docker spremenljive okoljske parametre, da ustvarite začasni izredni administratorski račun:

**Za Docker Compose (priporočeno):**
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

**Za neposredno urejanje appsettings.json:**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. Ponovno zaženite aplikacijo/kontejner {#2-restart-the-applicationcontainer}

Po spremembi konfiguracije ponovno zaženite svojo aplikacijo:

**Za Docker Compose:**
```bash
docker compose down
docker compose up -d
```

**Za neposredno aplikacijo:**
```bash
# Restart your application service
```

### 3. Prijava z izrednim računom {#3-login-with-emergency-account}

1. Dostopajte do spletnega vmesnika Mail Archiver
2. Prijavite se z izrednimi podatki za prijavo:
   - **Username**: `emergency_admin` (ali kar ste nastavili)
   - **Password**: `TempPass123!` (ali kar ste nastavili)

### 4. Ponastavite prvotno geslo administratorja {#4-reset-original-administrator-password}

1. V glavnem meniju pojdite v razdelek "Users"
2. V seznamu uporabnikov poiščite svoj prvotni administratorski račun
3. Za tega uporabnika kliknite "Edit"
4. Geslo spremenite v novo, varno geslo
5. Kliknite "Save Changes"

### 5. Obnavite prvotno konfiguracijo {#5-restore-original-configuration}

Spremembe konfiguracije vrnite na prvotno uporabniško ime administratorja:

**Za Docker Compose:**
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

**Za neposredno appsettings.json:**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. Ponovno zaženite aplikacijo {#6-restart-application-again}

Ponovno zaženite aplikacijo z obnovljeno konfiguracijo:

**Za Docker Compose:**
```bash
docker compose down
docker compose up -d
```

## 🎯 Primer: Celoten postopek pridobitve dostopa prek Docker Compose {#example-complete-docker-compose-recovery-process}

Tukaj je celoten primer izrednega postopka pridobitve dostopa z uporabo Docker Compose:

### Korak 1: Izredna konfiguracija {#step-1-emergency-configuration}
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

### Korak 2: Ponovni zagon in prijava {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### Korak 3: Ponastavitev prvotnega gesla {#step-3-reset-original-password}
1. Prijavite se v spletni vmesnik z izrednimi podatki za prijavo
2. Pojdite v razdelek Users
3. Uredite prvotnega uporabnika admin in nastavite novo geslo
4. Shranite spremembe

### Korak 4: Obnova normalne konfiguracije {#step-4-restore-normal-configuration}
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

### Korak 5: Končni ponovni zagon {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 Izgubljen ključ za šifriranje podatkov za prijavo {#lost-credential-encryption-key}

Če se izgubi `Security__CredentialEncryptionKey` (šifriranje podatkov za prijavo v miroju), shranjenih
podatkov za prijavo e-poštnih računov ne more več dešifrirati. Aplikacija ne **zastane**:
posledične vrednosti se obravnavajo kot "brez podatkov za prijavo" in zabeleži se opozorilo. Za pridobitev dostopa:

1. Obnovite ključ iz svoje varnostne kopije / upravljalnika skrivnosti (secret manager) in ponovno zaženite aplikacijo.
   Podatki za prijavo bodo takoj znova delovali, ni potrebne sprememba podatkov.
2. Če je ključ trajno izgubljen, šifriranih podatkov za prijavo ni mogoče obnoviti.
   Pričakovane račune je lahko enostavno identificirati: njihova sinhronizacija ne uspeva in v
   log aplikacije se zapiše opozorilo o dešifriranju (v uporabniškem vmesniku pa se prikaže kot težava s sinhronizacijo).
3. Odprite vsak pričakovani račun pod **Mail Accounts → Edit** in ponovno vnesite njegove podatke za prijavo:
   IMAP geslo, M365 client secret ali ponovno avtorizirajte račun (MSA). Shranjevanje
   obrazca ponovno šifrir nothing podatke za prijavo z zdaj konfiguriranim ključem — ročne
   spremembe v bazi podatkov niso potrebne.

> 💡 Ključ nikoli ni shranjen v bazi podatkov, zato same varnostne kopije baze podatkov niso dovolj —
> ključ vedno shranjujte posebej.

## 🔒 Najboljše prakse za varnost {#security-best-practices}

1. **Uporabljajte močna začasna gesla**: Naredite svoje izredno geslo kompleksno in unikatno
2. **Takoj ga spremenite**: Ponastavite izredno geslo takoj, ko ponovno pridobite dostop
