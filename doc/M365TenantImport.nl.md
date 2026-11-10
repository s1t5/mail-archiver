# Microsoft 365 Tenant Mailbox Import {#microsoft-365-tenant-mailbox-import}

[← Terug naar de documentatie-index](index.md)

## Overzicht {#overview}

Mail Archiver kan via een enkel aanmaakformulier Microsoft 365-mailaccounts aanmaken voor meer dan één Postbus in dezelfde tenant. Dit is nuttig bij het onboarden van een klant of organisatie waarbij alle, of een geselecteerde subset van, Microsoft 365-postboxen gearchiveerd moeten worden.

De tenant-import gebruikt de Microsoft Graph-applicatie-credentials die in het formulier zijn geconfigureerd en maakt één Mail Archiver-mailaccount aan per geïmporteerde Postbus.

## Vereisten {#prerequisites}

Voordat u tenant-import gebruikt, moet u de configuratie voor de Microsoft 365-appregistratie voltooien zoals beschreven in de [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md).

U heeft de volgende waarden nodig uit Microsoft Entra ID:

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

De appregistratie moet over Microsoft Graph **application permissions** beschikken waarmee Mail Archiver de tenant-postboxen kan lezen. Naast de machtigingen die vereist zijn voor reguliere M365-accountarchivering, is de volgende machtiging vereist voor tenant-import:

- **User.Read.All** – vereist om tenant-gebruikers en hun Postbus-adressen op te sommen.

Tenant-import wordt geselecteerd als de provider **Microsoft 365 (tenant)** in het aanmaakformulier.

### Filteren {#filtering}

Tenant-import en Tenant Management sluiten automatisch het volgende uit:
- **Guest accounts** (`userType == "Guest"`) — externe uitgenodigde gebruikers zonder hun eigen Postboxen in de tenant.
- **Gebruikers zonder een Exchange Online-licentie** — alleen gebruikers met een actief Exchange-serviceplan in hun `assignedPlans` worden opgesomd. Dit voorkomt het importeren van accounts die niet gearchiveerd kunnen worden.

## Importmodi {#import-modes}

Tenant-import ondersteunt twee modi:

### Alle vermelde Postboxen importeren {#import-all-listed-mailboxes}

Gebruik deze modus wanneer elke Postbus die door Microsoft Graph wordt geretourneerd, moet worden aangemaakt als een Mail Archiver-account.

- Mail Archiver laadt de tenant-postbuslijst van Microsoft Graph.
- Reeds bestaande M365-mailaccounts worden overgeslagen.
- Als **Skip disabled mailboxes** is ingeschakeld, worden uitgeschakelde gebruikers niet opgenomen in de postbuslijst.

### Alleen geselecteerde Postboxen importeren {#import-selected-mailboxes-only}

Gebruik deze modus wanneer alleen specifieke Postboxen in de tenant gearchiveerd moeten worden.

- Laad eerst de tenant-postbuslijst.
- Schakel **Import all listed mailboxes** uit.
- Selecteer de Postboxen die geïmporteerd moeten worden.
- Verstuur het formulier om alleen accounts aan te maken voor de geselecteerde Postboxen.

## Stapsgewijze instructies {#step-by-step-usage}

1. Meld u aan bij Mail Archiver als een gebruiker die mailaccounts kan aanmaken.
2. Open **Mail Accounts** > **Create**.
3. Selecteer **Microsoft 365 (tenant)** als de provider.
4. Voer de Microsoft 365-credentials in:
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. Voer de **Account name**-prefix in die gebruikt moet worden voor de gegenereerde accounts.
6. Kies of uitgeschakelde Postboxen moeten worden overgeslagen.
7. Klik op **Load mailboxes** om de tenant-postbuslijst op te sommen.
8. Kies het gewenste importgedrag:
   - Houd **Import all listed mailboxes** ingeschakeld om alle vermelde Postboxen te importeren.
   - Schakel dit uit en selecteer individuele Postboxen om alleen specifieke Postboxen te importeren.
9. Klik op **Save**.

Na het aanmaken wordt Mail Archiver teruggestuurd naar de lijst met mailaccounts en wordt getoond hoeveel tenant-mailaccounts er zijn geïmporteerd en hoeveel bestaande accounts zijn overgeslagen.

## Accountnaamgeving {#account-naming}

Voor tenant-importen gebruikt Mail Archiver de waarde die is ingevoerd bij **Account name** als prefix voor elke aangemaakte Postbus-account.

Voorbeeld:

- Ingevoerde accountnaam: `Test GmbH`
- Tenant Postbus: `mailbox@example.com`
- Aangemaakte accountnaam: `Test GmbH - <mailbox@example.com>`

Dit maakt het gemakkelijker om geïmporteerde accounts te identificeren die bij dezelfde tenant of klant horen.

## Bestaande Accounts {#existing-accounts}

Als een Postbus al bestaat als een Microsoft 365-account in Mail Archiver, slaat de tenant-import deze over in plaats van een duplicaat aan te maken. De detectie van bestaande accounts is gebaseerd op het e-mailadres van de Postbus en de M365-provider.

De postbuslijst kan ook aangeven of een Postbus al bestaat voordat de import wordt verzonden.

## Uitgeschakelde Postboxen {#disabled-mailboxes}

Het selectievakje **Skip disabled mailboxes** bepaalt of uitgeschakelde tenant-gebruikers moeten worden uitgesloten van de postbuslijst.

- Ingeschakeld: alleen ingeschakelde tenant-gebruikers worden opgesomd en geïmporteerd.
- Uitgeschakeld: uitgeschakelde tenant-gebruikers kunnen in de lijst verschijnen en kunnen worden geïmporteerd als ze zijn geselecteerd of als alle vermelde Postboxen worden geïmporteerd.

## Tenant Management {#tenant-management}

Naast de tenant-import via het aanmaakformulier biedt Mail Archiver een speciale **Tenant Management**-pagina voor beheerders. Deze is bereikbaar vanuit de Mail Accounts-index via de **Tenant Management**-knop (icoon van een gebouw) naast elk Microsoft 365-account.

Tenant Management is nuttig voor het toevoegen van Postboxen die niet aanvankelijk zijn geïmporteerd — bijvoorbeeld wanneer er nieuwe Postboxen worden aangemaakt in de tenant na de initiële onboarding, of wanneer accounts oorspronkelijk zijn overgeslagen.

### Hoe het verschilt van de tenant-import via het aanmaakformulier {#how-it-differs-from-the-create-form-tenant-import}

- De Microsoft 365-credentials (Client ID, Client Secret, Tenant ID) worden **niet opnieuw ingevoerd**. Ze worden overgenomen van het bronaccount waarop op de knop is geklikt.
- De tenant-postbuslijst wordt automatisch geladen wanneer de pagina wordt geopend (geen "Load mailboxes"-knop).
- Reeds geïmporteerde Postboxen worden getoond met een "already exists"-badge en kunnen niet opnieuw worden geselecteerd.
- Alleen beheerders hebben toegang tot Tenant Management.

### Stapsgewijze instructies {#step-by-step-usage}

1. Meld u aan bij Mail Archiver als een beheerder.
2. Open **Mail Accounts**.
3. Zoek het Microsoft 365-account waarvan u de tenant wilt beheren en klik op de **Tenant Management**-knop ernaast.
4. De pagina wordt geladen en somt alle Postboxen op die door Microsoft Graph worden geretourneerd voor de tenant van het bronaccount.
   - Postboxen die al bestaan als M365-accounts in Mail Archiver zijn gemarkeerd met een "already exists"-badge en kunnen niet worden geselecteerd.
   - Uitgeschakelde tenant-gebruikers worden niet opgesomd; Tenant Management slaat altijd uitgeschakelde Postboxen over.
5. Selecteer de Postboxen die u wilt toevoegen.
6. Voer de **Account name**-prefix in die gebruikt moet worden voor de nieuwe accounts (het naamgevingsschema is `{prefix} - <{email}>`, identiek aan de tenant-import via het aanmaakformulier).
7. Configureer optioneel **Delete After Days** en **Local Retention Days** voor de nieuwe accounts.
8. Klik op **Add selected mailboxes**.

Na het aanmaken wordt Mail Archiver teruggestuurd naar de lijst met mailaccounts en wordt getoond hoeveel Postboxen er zijn toegevoegd.

### Zoeken naar Postboxen {#searching-mailboxes}

Voor grote tenants met enkele duizenden Postboxen laat een zoekveld boven de postbuslijst u filteren op vertoonnaam of e-mailadres. Het filter is niet hoofdlettergevoelig en wordt direct toegepast terwijl u typt. **Select all** en **Clear selection** werken alleen op de momenteel zichtbare (gefilterde) Postboxen, waardoor filteren een veilige manier is om een specifieke subset te selecteren zonder de verborgen vermeldingen te beïnvloeden.

### Bestaande accounts hernoemen {#renaming-existing-accounts}

Het selectievakje **Rename existing accounts** past het naamgevingsschema `{prefix} - <{email}>` toe op alle M365-accounts die dezelfde appregistratie (Client ID en Tenant ID) delen als het bronaccount. Dit is nuttig voor het normaliseren van accountnamen na een initiële import waarbij de namen inconsistent waren.

Wanneer dit is aangevinkt, hernoemt het opslaan van het formulier elk overeenkomend account — inclusief het bronaccount — ongeacht of er nieuwe Postboxen zijn geselecteerd voor import. U kunt deze optie alleen gebruiken (zonder nieuwe Postboxen te selecteren) om namen te normaliseren zonder accounts toe te voegen.

### Limiet voor Postboxen {#mailbox-limit}

Een enkele Tenant Management-operatie kan maximaal `TenantManagement__MaxSelectedMailboxes` Postboxen tegelijk toevoegen (standaard `1000`). Dit beschermt tegen accidentele massale importen en overmatige Microsoft Graph / databasebelasting. Als u meer Postboxen moet importeren, voer de operatie dan meerdere keren uit. Raadpleeg de [Setup Guide](Setup.md#-tenant-management-settings) voor hoe u deze limiet kunt aanpassen.

### Vereiste machtigingen {#permissions-required}

Tenant Management gebruikt dezelfde Microsoft Graph-applicatie-machtigingen als de tenant-import via het aanmaakformulier. De appregistratie van het bronaccount moet beschikken over:

- De machtigingen die vereist zijn voor reguliere M365-accountarchivering, **en**
- **User.Read.All** — vereist om tenant-gebruikers en hun Postbus-adressen op te sommen.

Zie de [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md) voor de initiële configuratie.

### Auditlogboek {#audit-logging}

Elk bekeken Tenant Management-pagina en elke toevoegingsoperatie wordt opgenomen in het toegangsprotocol, inclusief het aantal opgesomde of toegevoegde Postboxen en het gebruikte bronaccount.
