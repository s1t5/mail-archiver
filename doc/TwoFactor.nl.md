# 🔐 Two-Factor Authentication {#two-factor-authentication}

[← Terug naar de documentatie-index](index.md)

## 📋 Overzicht {#overview}

Two-factor authenticatie (2FA) voegt een tweede stap toe aan het inlogproces: na
uw wachtwoord vraagt Mail Archiver om een **6-cijferige code** die wordt gegenereerd door een
authenticator-app op uw telefoon. Een gestolen wachtwoord alleen is dan niet langer
voldoende om toegang te krijgen tot het Archief.

Mail Archiver implementeert 2FA met behulp van **TOTP** (Time-based One-Time Password, RFC
6238) — dezelfde standaard die wordt gebruikt door Google Authenticator, Microsoft
Authenticator, Authy, 1Password, Bitwarden en anderen. Er is geen externe service
betrokken; het geheim verlaat uw instantie nooit.

**2FA is opt-in per gebruiker.** Het wordt door elke gebruiker geconfigureerd voor hun eigen
Account, niet globaal door een administrator.

## 🛠️ Vereisten {#prerequisites}

- Een TOTP-geschikte authenticator-app op uw telefoon of desktop
- Toegang tot uw eigen Account (u moet ingelogd zijn)
- **Een lokaal (wachtwoord) Account** — zie de opmerking over OIDC hieronder

> **⚠️ Niet beschikbaar voor OIDC-gebruikers.** Als u inlogt via een externe
> identiteitsprovider (Entra ID, Authelia, Keycloak, …), wordt 2FA beheerd door die
> provider en is de instellingenpagina geblokkeerd met de melding *"OIDC users cannot
> enable 2FA. Authentication is managed by your OIDC provider."* Schakel in plaats daarvan
> multi-factor authenticatie in bij uw identiteitsprovider — zie
> [OIDC / SSO](OIDC_Implementation.md).

## 🚀 Two-Factor Authentication inschakelen {#enabling-two-factor-authentication}

### Stap 1 — Open de instellingenpagina {#step-1-open-the-setup-page}

1. Log in bij Mail Archiver
2. Open uw accountmenu en kies **Two-Factor Authentication**
3. U komt terecht op **Two-Factor Setup**

### Stap 2 — Scan de QR-code {#step-2-scan-the-qr-code}

De pagina toont een QR-code. Open uw authenticator-app, kies *Account toevoegen* →
*QR-code scannen*, en richt de camera erop.

**Kunt u de code niet scannen?** Gebruik in plaats daarvan **Handmatige invoer**: de pagina toont de
geheime sleutel als tekst (`If you can't scan the QR code, enter this secret manually in
your authenticator app`). Typ deze in uw app als een time-based/TOTP-account.

> **ℹ️ Het geheim is nog niet opgeslagen.** Het wordt alleen bijgehouden voor de duur van de
> installatie in uw browsersessie. 2FA wordt pas actief nadat stap 3
> is geslaagd — als u de installatie afbreekt, verandert er niets aan uw Account.

### Stap 3 — Bevestig met een code {#step-3-confirm-with-a-code}

Voer de 6-cijferige code in die uw app nu toont en klik op **Enable Two-Factor
Authentication**.

Als de code wordt geweigerd, wordt de instellingenpagina opnieuw getoond met een nieuwe QR-code en
de melding *"Invalid authentication code. Please try again."* Controleer of de klok van uw
telefoon op automatisch staat — TOTP is afhankelijk van een nauwkeurige tijd.

### Stap 4 — Bewaar uw back-up codes {#step-4-save-your-backup-codes}

Bij succes wordt u naar de pagina **Backup Codes** geleid. Deze toont **10 codes**,
elk 16 tekens lang.

> **⚠️ Dit is de enige keer dat de codes worden getoond.** Ze worden
> gehasht opgeslagen — noch u, noch een administrator kan ze later opnieuw weergeven.
> Gebruik **Download Backup Codes** om ze op te slaan in een bestand, of kopieer ze naar uw
> wachtwoordbeheerder voordat u de pagina verlaat.

De download bevat deze herinnering:

> *Mail Archiver - Two-Factor Authentication Backup Codes. Bewaar deze back-up
> codes op een veilige plaats. U kunt ze gebruiken om in te loggen als u de toegang tot uw
> authenticator-app verliest. Elke back-up code kan slechts eenmaal worden gebruikt. Na het gebruik van een
> code wordt deze ongeldig gemaakt.*
> *Belangrijk: Houd deze codes veilig en deel ze met niemand.*

## 🔑 Inloggen met 2FA {#logging-in-with-2fa}

Na het invoeren van uw wachtwoord wordt u naar **Two-Factor Verification** geleid en
om een code gevraagd:

> *Enter a 6-digit TOTP code from your authenticator app or a 16-character
> backup code*

Beide werken in hetzelfde veld — Mail Archiver onderscheidt ze op basis van lengte:

| Invoer | Lengte | Gedrag |
|---|---|---|
| Code van uw authenticator-app | 6 cijfers | Wordt gecontroleerd tegen het huidige tijdvenster |
| Backup code | 16 tekens | Wordt gecontroleerd tegen uw opgeslagen codes, en daarna **ongeldig gemaakt** |

Om kleine klokverschillen op te vangen, worden codes van de direct voorafgaande en
volgende tijdstap ook geaccepteerd.

**"Remember me"** wordt overgenomen van het inlogformulier: als u dit heeft aangevinkt, blijft u
ingelogd na browserherstarts, mits u de 2FA-stap hebt voltooid. De selectie
wordt in de sessie aan de serverzijde bijgehouden terwijl de 2FA-stap in afwachting is, dus het blijft alleen
behouden als u de code invoert binnen de **60 minuten** inactieve timeout van de sessie.
Details en limieten (absolute levensduur van 7 dagen, geen effect voor OIDC-gebruikers) staan in
[User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).

### Gebruik van een back-up code {#using-a-backup-code}

1. Voer een van uw opgeslagen 16-tekens codes in in hetzelfde veld
2. De code wordt geaccepteerd en **onmiddellijk ongeldig gemaakt** — deze kan niet opnieuw worden gebruikt
3. Succesvolle inlogpogingen met een back-up code worden geregistreerd in het toegangsprotocol met de
   opmerking `(2FA Backup Code)`, zodat u later ongebruikelijk gebruik kunt opsporen

### Rate limiting {#rate-limiting}

Om brute-force pogingen te vertragen, is 2FA-verificatie beperkt tot **5 pogingen
per 15 minuten**, geteld per IP-adres en gebruikersnaam. Daarna ziet u:

> **Too Many Attempts** — *You have made too many 2FA verification attempts.
> Please wait and try again later.*

Wacht de tijd uit en probeer het later opnieuw. De limiet geldt voor zowel mislukte *als* succesvolle
pogingen binnen het venster.

## 🚫 Two-Factor Authentication uitschakelen {#disabling-two-factor-authentication}

1. Open **Two-Factor Authentication** in uw accountmenu
2. De pagina vraagt om bevestiging: *"Are you sure you want to disable
   two-factor authentication? This will reduce the security of your account."*
3. Voer uw **wachtwoord** in — deze stap vereist uw huidige wachtwoord, geen
   2FA-code
4. Klik op **Disable Two-Factor Authentication**

Het uitschakelen verwijdert het geheim en maakt alle resterende back-up codes ongeldig. Uw
volgende inlogpoging vereist alleen het wachtwoord.

## 🔄 Authenticator verloren? Twee manieren om weer binnen te komen {#lost-your-authenticator-two-ways-back-in}

### Als u nog back-up codes heeft {#if-you-still-have-backup-codes}

Log in met een back-up code zoals hierboven beschreven, schakel vervolgens 2FA uit en schakel het opnieuw in met uw nieuwe apparaat. Vergeet niet de nieuwe back-up codes op te slaan.

### Als u geen back-up codes meer heeft {#if-you-have-no-backup-codes-left}

Een **administrator** moet 2FA voor uw Account resetten:

1. Administrator opent **Users**
2. Bewerkt de betreffende gebruiker
3. Kiest **Reset Two-Factor Authentication** en bevestigt:
   *"Are you sure you want to reset two-factor authentication for this user?
   They will need to set it up again."*
4. De gebruiker kan daarna alleen met hun wachtwoord inloggen en 2FA opnieuw instellen

De reset verwijdert het geheim en de back-up codes. De bevestigingsmelding
*"Two-factor authentication has been successfully reset for user …"* verschijnt in
de Users-lijst.

> **ℹ️ Reikwijdte van de reset.** De reset omvat alleen 2FA. Het is **geen** wachtwoord
> reset — zie [Emergency Account Recovery](EmergencyAccountRecovery.md) als het
> wachtwoord het probleem is, of als de Account volledig is buitengesloten.

## 👁️ Wat Administrators kunnen zien {#what-administrators-can-see}

Het **Users** overzicht toont een 2FA-kolom:

- *"Two-factor authentication is currently enabled for this user."* — 2FA is actief
- Anders is 2FA uit voor die gebruiker

Administrators kunnen 2FA resetten (hierboven), maar **kunnen niet** het geheim of de
back-up codes lezen — beide zijn gehasht opgeslagen. Een gebruiker die zijn apparaat verliest zonder
back-up codes, heeft geen andere manier om binnen te komen dan de admin-reset.

## 🔍 Audit Trail {#audit-trail}

2FA-activiteit wordt geschreven naar het logboek en, bij succesvolle inlogpogingen, naar het toegangsprotocol:

| Event | Waar | Invoer |
|---|---|---|
| Inloggen via authenticator-app | Access log | `Login` met opmerking `IP: <address> (2FA)` |
| Inloggen via back-up code | Access log | `Login` met opmerking `IP: <address> (2FA Backup Code)` |
| Mislukte 2FA-poging | Application log | Waarschuwing met gebruikersnaam, IP en gemaskeerde token |
| Ongeldige back-up code | Application log | Waarschuwing met gebruikersnaam en IP |

De invoer in het toegangsprotocol maakt het gebruik van back-up codes achteraf zichtbaar — nuttig om
te detecteren of een code wordt gebruikt door iemand die deze niet zou mogen hebben. Zie
[Access Logging](Logs.md) voor hoe u deze invoer leest.

## ⚙️ Hoe het onder de motorkap werkt {#how-it-works-under-the-hood}

Voor administrators en reviewers:

- **Standaard**: TOTP (RFC 6238), 20-byte willekeurig geheim, base32-gecodeerd
- **Verificatievenster**: ±1 tijdstap, om klokafwijking te tolereren
- **Back-up codes**: 10 codes, elk 16 tekens, gegenereerd uit een cryptografische
  RNG, opgeslagen **gehasht**, individueel ongeldig gemaakt bij gebruik
- **Opslag van geheim**: in het gebruikersrecord; wordt nooit meer naar de browser teruggestuurd na installatie
- **Sessieafhandeling**: de sessie wordt geregenereerd voordat de 2FA-identiteit wordt
  opgeslagen, zodat een sessiecookie die voor het inloggen is geplaatst, niet kan worden meegenomen naar de
  geauthenticeerde sessie
- **OIDC-accounts**: 2FA-beheer is geblokkeerd; de provider is leidend
- **API-sleutels**: omzeilen 2FA bij ontwerp, aangezien niet-interactieve tokens geen
  uitdaging kunnen beantwoorden — zie de beveiligingsratio in de [REST API guide](API.md)

## ❓ Probleemoplossing {#troubleshooting}

| Symptoom | Oorzaak | Oplossing |
|---|---|---|
| *"Invalid authentication code"* | Klok van telefoon niet correct, of verkeerd account in de app | Schakel automatische tijd-synchronisatie in; controleer of de app de Mail Archiver-invoer toont |
| *"Two-factor secret is missing. Please restart the setup process."* | Instellingenpagina te lang open gebleven, sessie verlopen | Start de installatie opnieuw via het accountmenu |
| *"Your session has expired. Please log in again."* | Te veel tijd verstreken op de verificatiepagina | Log opnieuw in |
| *"Two-factor authentication is not enabled for this account."* | 2FA is gereset of nooit voltooid | Stel het opnieuw in, of vraag een administrator |
| Back-up code geweigerd | Code al gebruikt, of een teken verkeerd gelezen | Back-up codes werken **één keer**; gebruik een andere, of laat 2FA resetten |
| *"Too Many Attempts"* | Meer dan 5 verificatiepogingen in 15 minuten | Wacht de tijd uit |
| Geen instellingenpagina in het accountmenu | U logt in via OIDC | Schakel MFA in bij uw identiteitsprovider |
| Geen 2FA-kolom in de Users-lijst | U bent geen administrator | Vraag een administrator |

## 📚 Gerelateerde documentatie {#related-documentation}

- [User Management and Mailbox Permissions](UserManagement.md) — accounts aanmaken, admin-rechten
- [OIDC / SSO Authentication](OIDC_Implementation.md) — inloggen via externe providers
- [Emergency Account Recovery](EmergencyAccountRecovery.md) — buitengesloten accounts
- [Access Logging](Logs.md) — het auditlogboek lezen
- [REST API](API.md) — waarom API-sleutels 2FA omzeilen
