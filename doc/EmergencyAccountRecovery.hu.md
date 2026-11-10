# 🚨 Sürgős fiókhelyreállítás {#emergency-account-recovery}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Ez az útmutató részletes utasításokat nyújt a Mail Archiver alkalmazás elérésének helyreállításához, amennyiben elfelejtette a rendszergazda jelszavát. Ez a vészhelyzeti eljárás lehetővé teszi a rendszerhez való visszaélést anélkül, hogy bármilyen adat elvesznétne.

> 📝 **Verziós követelmény**: Ez a helyreállítási módszer a Mail Archiver 2601.1 és hátróbbi verzióiban érhető el.

## ⚠️ Fontos biztonsági figyelmeztetés {#important-security-notice}

Ezt a helyreállítási módszert endast vészhelyzeti helyzetekben szabad használni, amikor nem férjen hozzá a rendszergazda fiókjához. A folyamat ideiglenes alkalmazáskonfigurációs módosítást igényel, amelyet a jelszó helyreállítása után azonnal vissza kell állítani.

## 🛠️ Helyreállítási lépések {#recovery-steps}

### 1. Sürgős rendszergazda fiók létrehozása {#1-create-emergency-administrator-account}

Módosítsa a `appsettings.json` vagy a Docker környezeti változóit egy ideiglenes, sürgős rendszergazda fiók létrehozásához:

**Docker Compose esetén (ajánlott):**
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

**appsettings.json közvetlen módosításával:**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. Az alkalmazás/konténer újraindítása {#2-restart-the-applicationcontainer}

A konfiguráció módosítása után indítsa újra az alkalmazást:

**Docker Compose esetén:**
```bash
docker compose down
docker compose up -d
```

**Közvetlen alkalmazás esetén:**
```bash
# Restart your application service
```

### 3. Bejelentkezés a sürgős fiókkal {#3-login-with-emergency-account}

1. Férjen hozzá a Mail Archiver web interfészhez
2. Jelentkezzen be a sürgős hitelesítő adatokkal:
   - **Username**: `emergency_admin` (vagy amilyeneket beállított)
   - **Password**: `TempPass123!` (vagy amilyeneket beállított)

### 4. Az eredeti rendszergazda jelszó alaphelyzetbe állítása {#4-reset-original-administrator-password}

1. Navigáljon a főmenüből a "Users" szekcióba
2. Keresse meg az eredeti rendszergazda fiókját a felhasználói listában
3. Kattintson az adott felhasználó mellett az "Edit" gombra
4. Illesztse be az új, biztonságos jelszót
5. Kattintson a "Save Changes" gombra

### 5. Az eredeti konfiguráció visszaállítása {#5-restore-original-configuration}

Váltsa vissza konfigurációs módosításait az eredeti rendszergazda felhasználónévre:

**Docker Compose esetén:**
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

**Közvetlen appsettings.json esetén:**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. Az alkalmazás ismételt újraindítása {#6-restart-application-again}

Indítsa újra az alkalmazást a visszaállított konfigurációval:

**Docker Compose esetén:**
```bash
docker compose down
docker compose up -d
```

## 🎯 Példa: Teljes Docker Compose helyreállítási folyamat {#example-complete-docker-compose-recovery-process}

Íme egy teljes példa a sürgős helyreállítási folyamatra Docker Compose használatával:

### 1. lépés: Sürgős konfiguráció {#step-1-emergency-configuration}
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

### 2. lépés: Újraindítás és bejelentkezés {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### 3. lépés: Eredeti jelszó alaphelyzetbe állítása {#step-3-reset-original-password}
1. Jelentkezzen be a web interfészre a sürgős hitelesítő adatokkal
2. Menjen a Users szekcióba
3. Szerkesztse az eredeti admin felhasználót és állítson be új jelszót
4. Mentse a módosításokat

### 4. lépés: Normál konfiguráció visszaállítása {#step-4-restore-normal-configuration}
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

### 5. lépés: Végső újraindítás {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 Elveszett hitelesítési kulcs (Encryption Key) {#lost-credential-encryption-key}

Ha a `Security__CredentialEncryptionKey` (credential encryption at rest) elveszik, a tárolt
levélszámlák hitelesítő adatai nemlehetnek visszaoldva. Az alkalmazás **nem** omlik össze: a
érintett értékeket úgy kezeli, mint "nincs hitelesítő adat", és figyelmeztetés kerül naplózásra. A helyreállításhoz:

1. Állítsa vissza a kulcsot a mentéséből / titkaimanagárjáról (secret manager), majd indítsa újra az alkalmazást. A
   hitelesítő adatok azonnal újra működnek, nincs szükség adatváltoztatásra.
2. Ha a kulcs véglegesen elveszett, a titkosított hitelesítő adatok nem állíthatók vissza. Az érintett
   fiókok könnyen azonosíthatók: a szinkronizálásuk sikertelen, és visszaírásra kerül egy visszaoldási figyelmeztetés
   az alkalmazás naplóba (és szinkronizációs hibaként jelenik meg a felületen).
3. Nyissa meg az egyes érintett fiókot a **Mail Accounts → Edit** menüpont alatt, és adja meg újra a hitelesítő adatokat:
   az IMAP jelszót, a M365 client secretet, vagy újrahívja a fiókot (MSA). Az űrlap mentése vissza titkosítja a
pedig a hitelesítő adatot a most konfigurált kulccsal — nincs szükség manuális adatbázis-módosításra.

> 💡 A kulcs soha nem kerül az adatbázisba, így az adatbázis-mentések önmagukban nem elegendőek —
> mindig külön mentse el a kulcsot is.

## 🔒 Biztonsági legjobb gyakorlatok {#security-best-practices}

1. **Erős ideiglenes jelszavakat használjon**: Tegye összetetté és egyedivé a sürgős jelszavát
2. **Módosítsa azonnal**: Állítsa vissza a sürgős jelszót, amint visszakapta a hozzáférést
