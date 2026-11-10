# 🔐 Kétfaktoros hitelesítés {#two-factor-authentication}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

A kétfaktoros hitelesítés (2FA) egy második lépést ad a bejelentkezési folyamathoz: a jelszó megadása után a Mail Archiver egy telefonján található autentikátor alkalmazás által generált **6 jegyű kódot** kér. Így a lopott jelszó önmagában már nem elég a архів eléréséhez.

A Mail Archiver a **TOTP** (Time-based One-Time Password, RFC 6238) szabványt használja a 2FA implementálásához — ugyanezt a szabványt használja a Google Authenticator, a Microsoft Authenticator, az Authy, a 1Password, a Bitwarden és mások is. Nincs szükség külső szolgáltatásra; a titkos adat (secret) soha nem hagyja el az Ön példányát (instance).

**A 2FA felhasználónként választható (opt-in).** Minden egyes felhasználó a saját fiókjához konfigurálja azt, nem pedig egy adminisztrátor globálisan.

## 🛠️ Előfeltételek {#prerequisites}

- Egy TOTP-képes autentikátor alkalmazás a telefonján vagy az asztali gépen
- Hozzáférés a saját fiókjához (be kell jelentkeznie)
- **Egy helyi (jelszavas) fiók** — lássd az alatti megjegyzést az OIDC-ről

> **⚠️ OIDC felhasználóknak nem elérhető.** Ha egy külső identitásmegelőzőn (Entra ID, Authelia, Keycloak, …) keresztül jelentkezik be, a 2FA kezelését az adott szolgáltató végzi, és a beállítási oldal blokkolva lesz a következő üzenettel: *"OIDC users cannot enable 2FA. Authentication is managed by your OIDC provider."* Ehelyett az identitásmegelőzőjénél engedélyezze a többszoros hitelesítést — lássd a [OIDC / SSO](OIDC_Implementation.md) részt.

## 🚀 A kétfaktoros hitelesítés engedélyezése {#enabling-two-factor-authentication}

### 1. lépés — Nyissa meg a beállítási oldalt {#step-1-open-the-setup-page}

1. Jelentkezzen be a Mail Archiverbe
2. Nyissa meg a fiókmenüt, és válassza a **Two-Factor Authentication** lehetőséget
3. A **Two-Factor Setup** oldalra kerül

### 2. lépés — QR-kód beolvasása {#step-2-scan-the-qr-code}

Az oldal egy QR-kódot jelenít meg. Nyissa meg az autentikátor alkalmazását, válassza az *Add account* → *Scan QR code* lehetőséget, és irányítsa a kamerát a kódra.

**Nem tudja beolvasni a kódot?** Használja a **Manual Entry** lehetőséget helyette: az oldal szöveges formátumban mutatja meg a titkos adatot (`If you can't scan the QR code, enter this secret manually in your authenticator app`). Írja be a kódot az alkalmazásába időalapú/TOTP fiókként.

> **ℹ️ A titkos adat még nem mentve.** Csak a beállítás ideje alatt tárolódik a böngésző munkamenetében. A 2FA csak akkor válik aktívvá, ha a 3. lépés sikeresen lezajlik — ha félbeszakítja a beállítást, semmi nem változik a fiókjában.

### 3. lépés — Ellenőrzés kóddal {#step-3-confirm-with-a-code}

Adja meg az alkalmazása által mutatott 6 jegyű kódot, majd kattintson a **Enable Two-Factor Authentication** gombra.

Ha a kód hibás, a beállítási oldal újra megjelenik egy újabb QR-kóddal és a következő üzenettel: *"Invalid authentication code. Please try again."* Ellenőrizze, hogy a telefon órája automatikus beállítású e, mivel a TOTP a pontos időre támaszkodik.

### 4. lépés — A biztonsági kódok (backup codes) mentése {#step-4-save-your-backup-codes}

A sikeres beállítás után a **Backup Codes** oldalra kerül. Ez **10 kódot** mutat meg, amelyek mindegyike 16 karakter hosszú.

> **⚠️ Csak egyszer láthatja ezeket a kódokat.** A kódok hashelt formában vannak tárolva — sem Ön, sem egy adminisztrátor nem tudja később újra megjeleníteni őket. Használja a **Download Backup Codes** funkciót a kódok egy fájlba mentéséhez, vagy másolja át őket egy jelszókezelőbe, mielőtt elhagyja az oldalt.

A letöltési fájl tartalmazza ezt a figyelmeztetést:

> *Mail Archiver - Two-Factor Authentication Backup Codes. Tárolja ezeket a biztonsági kódokat egy biztonságos helyen. Használhatja őket a bejelentkezéshez, ha elveszíti hozzáférést az autentikátor alkalmazáshoz. Minden biztonsági kódot csak egyszer lehet használni. A használat után a kód érvényétét veszíti.*
> *Fontos: Tartsa ezeket a kódokat biztonságban, és ne ossza meg senkivel.*

## 🔑 Bejelentkezés 2FA-val {#logging-in-with-2fa}

A jelszó megadása után a **Two-Factor Verification** oldalra kerül, ahol kódot kér:

> *Enter a 6-digit TOTP code from your authenticator app or a 16-character backup code*

Mindkét típus működik ugyanabban a mezőben — a Mail Archiver a hossz alapján különbözteti meg őket:

| Input | Length | Behaviour |
|---|---|---|
| Code from your authenticator app | 6 digits | Verified against the current time window |
| Backup code | 16 characters | Verified against your stored codes, then **invalidated** |

A kis óraeltérések elfogadásához az közvetlenül előtti és a következő időintervallum kódjai is elfogadhatóak.

A **"Remember me"** beállítás átkerül a bejelentkezési űrlapból: ha be van pipálva, a 2FA lépés után a böngésző újraindítása után is bejelentkezve marad. A választás a szerveroldali munkamenetben (session) marad meg a 2FA lépés folyamánál, így csak akkor marad meg, ha a kódot a munkamenet **60 perces** inaktív timeoutján belül megadja. A részletek és korlátok (7 napos abszolút élettartam, nem vonatkozik az OIDC felhasználókra) a [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox) oldalon találhatók.

### Biztonsági kód (backup code) használata {#using-a-backup-code}

1. Adja meg a mentett 16 karakteres kódok egyikét ugyanabban a mezőben
2. A kód elfogadásra kerül és **azonnal érvényét veszíti** — nem használható újra
3. A biztonsági kóddal történő sikeres bejelentkezések a hozzáférést naplózza a `(2FA Backup Code)` megjegyzéssel, így később felismerheti a rendeltelen használatot

### Sebességkorlátozás (Rate limiting) {#rate-limiting}

A brute-force kísérletek lassítása érdekében a 2FA hitelesítés **15 percenként 5 próbára** van korlátozva, IP-csoporton és felhasználónévenként számolva. Ez után a következő üzenetet látja:

> **Too Many Attempts** — *You have made too many 2FA verification attempts. Please wait and try again later.*

Várja meg az időszak lejártát, és próbálja újra. A korlát a abban az időszakban elbukott *és* sikeres próbálatokra is vonatkozik.

## 🚫 A kétfaktoros hitelesítés kikapcsolása {#disabling-two-factor-authentication}

1. Nyissa meg a **Two-Factor Authentication** menüt a fiókjában
2. Az oldal visszaigazolást kér: *"Are you sure you want to disable two-factor authentication? This will reduce the security of your account."*
3. Adja meg a **jelszavát** — ez a lépés a jelenlegi jelszót igényli, nem pedig a 2FA kódot
4. Kattintson a **Disable Two-Factor Authentication** gombra

A kikapcsolás törli a titkos adatot és érvényét veszteti a megmaradt biztonsági kódoknak is. A következő bejelentkezésnél már csak a jelszó szükséges.

## 🔄 Elvesztette az autentikátort? Két út a visszatéréshez {#lost-your-authenticator-two-ways-back-in}

### Ha még vannak biztonsági kódjai (backup codes) {#if-you-still-have-backup-codes}

Jelentkezzen be egy biztonsági kóddal a fent leírt módon, majd kapcsolja ki és újra engedélyezze a 2FA-t az új eszközével. Ne feledje el menteni az új biztonsági kódokat.

### Ha nincsenek több biztonsági kódja {#if-you-have-no-backup-codes-left}

Egy **adminisztrátornak** vissza kell állítania (reset) a 2FA-t a fiókjához:

1. Az adminisztrátor megnyitja a **Users** menüt
2. Szerkeszteli a érintett felhasználót
3. Kiválasztja a **Reset Two-Factor Authentication** lehetőséget, és megerősíti: *"Are you sure you want to reset two-factor authentication for this user? They will need to set it up again."*
4. A felhasználó ezután csak a jelszavával tud bejelentkezni, és újra beállíthatja a 2FA-t

A visszaállítás törli a titkos adatot és a biztonsági kódokat. A *"Two-factor authentication has been successfully reset for user …"* visszaigazoló üzenet megjelenik a Users listában.

> **ℹ️ A visszaállítás hatoskora.** A visszaállítás csak a 2FA-t érinti. Ez **nem** jelszó-visszaállítás — lásd a [Emergency Account Recovery](EmergencyAccountRecovery.md) részt, ha a jelszó a probléma, vagy ha a fiók teljesen le van zárva.

## 👁️ Amit az adminisztrátorok láthatnak {#what-administrators-can-see}

A **Users** áttekintő táblázat tartalmaz egy 2FA oszlopot:

- *"Two-factor authentication is currently enabled for this-user."* — A 2FA aktív
- Egyéb esetben a 2FA kikapcsolva az adott felhasználó számára

Az adminisztrátorok visszaállíthatják a 2FA-t (fent), de **nem tudják** elolvasni a titkos adatot vagy a biztonsági kódokat — mindkettő hashelt formában tárolódik. A felhasználó, aki elveszíti a eszközét biztonsági kódok nélkül, nem tud visszatérni a rendszerbe az adminisztrátori visszaállítás kivélemelően.

## 🔍 Audit nyomvonal (Audit Trail) {#audit-trail}

A 2FA tevékenységet a logba, a sikeres bejelentkezéseknél pedig a hozzáférést naplóba írják:

| Event | Where | Entry |
|---|---|---|
| Login via authenticator app | Access log | `Login` with note `IP: <address> (2FA)` |
| Login via backup code | Access log | `Login` with note `IP: <address> (2FA Backup Code)` |
| Failed 2FA attempt | Application log | Warning with username, IP and masked token |
| Invalid backup code | Application log | Warning with username and IP |

A hozzáférést naplózza bejegyzés lehetővé teszi a biztonsági kód használatának utólagos felismerését — ez hasznos, ha egy kódot valaki használja, akinek nincs joga hozzá. Lásd a [Access Logging](Logs.md) részt a bejegyzések értelmezéséhez.

## ⚙️ Hogyan működik a háttérben {#how-it-works-under-the-hood}

Adminisztrátoroknak és ellenőröknek:

- **Szabvány**: TOTP (RFC 6238), 20 byteos véletlenszerű titkos adat, base32 kódolással
- **Ellenőrzési ablak**: ±1 időlépés az óraeltérés tolerálása érdekében
- **Biztonsági kódok**: 10 kód, mindegyik 16 karakter hosszú, kriptográfiai RNG-ből generált, **hashelt** formában tárolt, használatkor egyenként érvényét vesztő kódok
- **Titkos adat tárolása**: a felhasználói rekordban; a beállítás után soha nem kerül vissza a böngészőbe
- **Session kezelés**: a munkamenetet (session) újраgenerálja a 2FA azonosító tárolása előtt, így a bejelentkezés előtt telepített session cookie nem kerül tovább a hitelesített munkamenetbe
- **OIDC fiókok**: a 2FA kezelése blokkolva van; a szolgáltató (provider) az irányadó
- **API kulcsok**: alapértelmezetten megkerülik a 2FA-t, mivel a nem interaktív tokenek nem tudnak válaszolni egy kérdésre (challenge) — lásd a biztonsági indoklást a [REST API guide](API.md) dokumentációban

## ❓ Hibaelégítés {#troubleshooting}

| Symptom | Cause | Fix |
|---|---|---|
| *"Invalid authentication code"* | Rossz időbeállítás a telefonon, vagy rossz fiók az alkalmazásban | Engedélyezze az automatikus időszinkronizációt; ellenőrizze, hogy az alkalmazás a Mail Archiver bejegyzést mutatja-e |
| *"Two-factor secret is missing. Please restart the setup process."* | Túl sokáig volt nyitva a beállítási oldal, lejár났 a munkamenet | Indítsa újra a beállítást a fiókmenüből |
| *"Your session has expired. Please log in again."* | Túl sok idő telt el a hitelesítési oldalon | Jelentkezzen be újra |
| *"Two-factor authentication is not enabled for this account."* | A 2FA visszaállítva lett vagy sosem fejeződött be | Állítsa be újra, vagy kérjen segítséget az adminisztrátortól |
| Backup code rejected | A kód már használt, vagy egy karakter félreolvasható volt | A biztonsági kódok csak **egyszer** használhatóak; használjon egy másikat, vagy kérjen 2FA visszaállítást |
| *"Too Many Attempts"* | Több mint 5 hitelesítési kíslet 15 percen belül | Várjon az időszak lejártáig |
| No setup page in the account menu | OIDC keresztül jelentkezett be | Engedélyezze a MFA-t az identitásmegelőítőjénél |
| No 2FA column in the Users list | Nem adminisztrátor | Kérjen segítséget az adminisztrátortól |

## 📚 Kapcsolódó dokumentáció {#related-documentation}

- [User Management and Mailbox Permissions](UserManagement.md) — fiókok létrehozása, admin jogok
- [OIDC / SSO Authentication](OIDC_Implementation.md) — bejelentkezés külső szolgáltatókon keresztül
- [Emergency Account Recovery](EmergencyAccountRecovery.md) — lezárt fiókok
- [Access Logging](Logs.md) — az audit nyomvonal olvasása
- [REST API](API.md) — miért kerülik ki a 2FA-t az API kulcsok
