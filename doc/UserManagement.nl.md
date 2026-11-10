# 👥 Gebruikersbeheer en Postbus Machtigingen {#user-management-and-mailbox-permissions}

[← Terug naar Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Deze gids biedt gedetailleerde instructies voor het aanmaken van nieuwe gebruikersaccounts en het toewijzen van postbus machtigingen in de Mail Archiver-applicatie. Het behandelt ook het wijzigen van je eigen wachtwoord en de verplichte wachtwoordwijziging op een vers geïnstalleerde instantie.


## 🛠️ Vereisten {#prerequisites}

- Administratieve toegang tot de Mail Archiver-applicatie
- Bestaande e-mailaccounts die in het systeem zijn geconfigureerd

## 🧑‍💻 Een nieuwe gebruikersaccount aanmaken {#creating-a-new-user-account}

1. Log in bij de Mail Archiver-applicatie met een account dat over administratieve privileges beschikt
2. Navigeer naar de sectie "Users" in het hoofdmenu
3. Klik op de knop "Create New User"
4. Vul de vereiste gebruikersinformatie in:
   - **Username**: Voer een unieke gebruikersnaam in voor de nieuwe gebruiker
   - **Password**: Voer een veilig wachtwoord in voor de gebruiker
   - **Email**: E-mailadres van de gebruiker
   - **Admin**: Vink dit vakje aan als de gebruiker over administratieve privileges moet beschikken
   - **Self Manager**: Vink dit vakje aan als de gebruiker zijn eigen e-mailaccounts moet kunnen beheren (bestaande accounts bewerken en nieuwe toevoegen, evenals accounts verwijderen)
   - **active**: Moet aangevinkt zijn om logins voor de gebruiker toe te staan
5. Klik op "Create User" om de nieuwe gebruikersaccount op te slaan

## 🔐 Postbus Machtigingen toewijzen aan gebruikers {#assigning-mailbox-permissions-to-users}

1. Navigeer na het aanmaken van de gebruiker, of bij het bewerken van een bestaande gebruiker, naar de sectie "Users" in het hoofdmenu
2. Zoek de gebruiker in de lijst waarvoor je e-mailaccounts wilt toewijzen en klik op de knop "Assign" voor die gebruiker
3. Op de pagina "Assign Mail Accounts" zie je alle beschikbare e-mailaccounts en selectievakjes die aangeven of ze zijn toegewezen aan de gebruiker
5. Om een nieuw e-mailaccount toe te wijzen, vink je het bijbehorende vakje aan voor het specifieke account
6. Om een e-mailaccount van de gebruiker te verwijderen, vink je het vakje uit
7. Klik op "Save Assignments" om de wijzigingen toe te passen

## 👤 Gebruikersaccount Machtigingen {#user-account-permissions}

### Admin Users {#admin-users}
Admin-gebruikers hebben volledige toegang tot de applicatie en kunnen:
- Alle gebruikersaccounts en hun machtigingen beheren
- Toegang krijgen tot alle e-mailaccounts in het systeem
- Alle toegangslogs bekijken
- Alle administratieve taken uitvoeren

### Self Manager Users {#self-manager-users}
Self manager-gebruikers kunnen:
- Nieuwe e-mailaccounts toevoegen aan hun eigen account
- Bestaande e-mailaccounts die aan hen zijn toegewezen bewerken
- E-mailaccounts die aan hen zijn toegewezen verwijderen
- Toegang krijgen tot en e-mails beheren van hun toegewezen accounts (inclusief verwijderen)
- Hun eigen toegangslogs bekijken

### Standard Users {#standard-users}
Standaardgebruikers hebben beperkte toegang en kunnen:
- Gearchiveerde e-mails bekijken van toegewezen e-mailaccounts
- Zoeken binnen hun toegewezen e-mailaccounts
- E-mails exporteren uit hun toegewezen accounts
- E-mails herstellen uit hun toegewezen accounts
- Toegang krijgen tot e-mailbijlagen van toegewezen accounts

## 🔑 Je eigen wachtwoord wijzigen {#changing-your-own-password}

Elke gebruiker met een lokaal wachtwoord kan dit wijzigen zonder een administrator:

1. Klik op je **username** in de rechterbovenhoek om het gebruikersmenu te openen.
2. Kies **Change Password**.
3. Voer je **current password** in en voer het **new password** twee keer in, en verzend dan.

Het nieuwe wachtwoord moet voldoen aan de onderstaande vereisten en moet **anders zijn dan het huidige wachtwoord**.

> ℹ️ **OIDC-gebruikers kunnen hun wachtwoord hier niet wijzigen.** De pagina antwoordt met:
> *"OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* Wijzig het wachtwoord in plaats daarvan bij je identity provider —
> zie [OIDC / SSO](OIDC_Implementation.md).

### Wachtwoordvereisten {#password-requirements}

- ten minste **10 tekens**
- ten minste één **hoofdletter**
- ten minste één **kleine letter**
- ten minste één **cijfer**
- ten minste één **speciaal teken**

Het formulier toont deze als een live checklist terwijl je typt. Dezelfde regels gelden wanneer een administrator een gebruiker aanmaakt of een wachtwoord reset.

## ⚠️ Verplichte wachtwoordwijziging (Eerste installatie) {#forced-password-change-initial-setup}

Een vers geïnstalleerde instantie start met de administrator-gegevens uit de configuratie (`Authentication__Username` / `Authentication__Password`). Zolang dat standaardwachtwoord nog in gebruik is **en er nog geen e-mailaccount bestaat**, dwingt de eerste login een wijziging af:

- Een waarschuwingsbanner legt uit waarom: *"For security reasons, you must change your
  password before continuing. This is required because you are using the default
  system credentials on a newly set up system."*
- Je kunt de pagina niet verlaten: *"You must change your password before
  you can continue using the system."*
- Het nieuwe wachtwoord moet anders zijn dan het huidige wachtwoord.
- Na het opslaan wordt de flag gewist en word je naar het dashboard geleid.

Zodra je het standaardwachtwoord hebt gewijzigd, verschijnt de verplichte wijziging nooit meer.

## 🕒 Ingelogd blijven — het "Remember Me" selectievakje {#staying-signed-in-the-remember-me-checkbox}

Het inlogformulier heeft een selectievakje met het label **Remember Me** (*angemeldet bleiben* in de Duitse UI). Het doet precies één ding: het bepaalt of de authenticatiecookie een sessiecookie is of een persistente cookie.

| | Selectievakje niet aangevinkt | Selectievakje aangevinkt |
|---|---|---|
| Cookie levensduur | `Authentication__SessionTimeoutMinutes` (standaard **60 minuten**) | **7 dagen** |
| Cookie type | Sessiecookie — wordt verwijderd wanneer de browser sluit | Persistente cookie — wordt op schijf opgeslagen |
| Browser herstarten | Je moet opnieuw inloggen | Je blijft ingelogd |
| Idle timeout | Wordt verlengd bij elke aanvraag (`SlidingExpiration`) | Wordt verlengd bij elke aanvraag, tot de absolute limiet van 7 dagen |
| Uitloggen | Beëindigt de sessie onmiddellijk | Beëindigt de sessie onmiddellijk |

Het selectievakje verandert dus "ingelogd voor deze browsersessie, maximaal 60 minuten inactiviteit" in "ingelogd voor maximaal 7 dagen, inclusief browser- en machine-restarts".

### Dingen die het selectievakje niet doet {#things-the-checkbox-does-not-do}

- **Het onthoudt je wachtwoord of gebruikersnaam niet.** Het inlogformulier is niet
  vooraf ingevuld bij het volgende bezoek; alleen de sessiecookie wordt bewaard.
- **De 7 dagen zijn een harde limiet en kunnen niet worden geconfigureerd.** De waarde is vastgelegd
  in de code (`Auth/Services/CookieAuthenticationService.cs`). Alleen de
  niet-persistente levensduur volgt `Authentication__SessionTimeoutMinutes`.
- **Het is geen inactiviteitstimer.** 7 dagen worden geteld vanaf het inloggen; zodra
  deze voorbij zijn, is een nieuwe login vereist, zelfs als je het Archief een minuut geleden hebt gebruikt.
- **Het overleeft een uitlogactie niet.** Klikken op *Logout* verwijdert de cookie
  ongeacht het selectievakje.

### Interactie met 2FA {#interaction-with-2fa}

Wanneer tweestapsverificatie is ingeschakeld, wordt de keuze in het selectievakje onthouden tijdens de
2FA-stap en pas toegepast nadat de tweede factor is geslaagd. De 2FA-overdracht
bevindt zich in de server-side sessie, die een eigen **idle timeout van 60 minuten** heeft: als de code niet binnen die tijd wordt ingevoerd, wordt de login afgebroken en
gaat de selectie in het selectievakje verloren.

### Interactie met de applicatiesessie {#interaction-with-the-application-session}

Mail Archiver houdt een tweede, aparte server-side sessie bij voor kortstondige
status — de 2FA-overdracht, de selectie voor batch-herstel en de laatste zoekstatus.
Die sessie heeft een vaste **idle timeout van 60 minuten** en is *niet*
persistent. Na een browser-restart met een geldige "Remember Me"-cookie ben je
ingelogd, maar elke lopende bewerking die in die sessie werd vastgehouden, begint opnieuw.

### Interactie met OIDC / SSO {#interaction-with-oidc-sso}

Gebruikers die inloggen via een externe identity provider krijgen nooit de
persistente cookie: de OIDC-callback start de sessie altijd met
`persistAuthentication: false`. Voor die accounts heeft het selectievakje **geen
effect** — hoe lang een sessie duurt, wordt bepaald door de sessie-instellingen van de identity provider.

> ⚠️ **Beveiligingsnotitie.** Laat het selectievakje op een gedeelde of onbeheerde machine
> niet aangevinkt — een persistente cookie houdt het Archief tot 7 dagen
> toegankelijk zonder wachtwoord. In omgevingen waar dit onacceptabel is, verkort
> `Authentication__SessionTimeoutMinutes` en vereis dat gebruikers het
> selectievakje niet gebruiken, of gebruik OIDC waarbij de provider zijn eigen sessiebeleid afdwingt.

## 🔒 Beveiligingsoverwegingen {#security-considerations}

1. Gebruik sterke wachtwoorden voor alle gebruikersaccounts
2. Wijzig het standaard administrator-wachtwoord onmiddellijk na installatie
3. Ken de Admin machtiging alleen toe aan gebruikers die dit nodig hebben
4. Ken de Self Manager machtiging alleen toe aan gebruikers die hun eigen accounts moeten beheren
5. Controleer regelmatig de machtigingen van gebruikers om te zorgen dat de toegang passend is
6. Verwijder de toegang van gebruikers wanneer deze niet langer nodig is
7. Gebruik het principe van de minste privileges — ken alleen toegang toe tot de e-mailaccounts die gebruikers nodig hebben
