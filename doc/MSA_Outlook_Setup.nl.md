# ☁️ Personal Microsoft Account (Outlook.com / M365 Family) Setup {#personal-microsoft-account-outlookcom-m365-family-setup}

[← Terug naar Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Deze gids legt uit hoe u e-mails kunt archiveren van een **persoonlijk Microsoft-account** (Outlook.com, Hotmail, live.com of Microsoft 365 Family) in Mail Archiver.

In tegenstelling tot zakelijke Microsoft 365-accounts (die de client-credentials flow gebruiken — zie [Azure App Registration for M365](AZURE_APP_REGISTRATION_M365.md)), gebruiken persoonlijke accounts **OAuth2 met de Device Code Flow (RFC 8628)**. Microsoft heeft Basic Auth en app-wachtwoorden voor persoonlijke accounts uitgefaseerd, dus OAuth2 is vereist.

Mail Archiver wordt geleverd met een **vooraf geregistreerde gedeelde Client ID**, zodat de meeste gebruikers **geen** eigen Azure App Registration hoeven aan te maken. Power users kunnen nog steeds hun eigen app registreren (zie Optie B hieronder).

## 📚 Inhoudsopgave {#table-of-contents}

1. [Optie A: Standaard Setup (Aanbevolen)](#option-a-standard-setup-recommended)
2. [Optie B: Custom App Registration (Power Users)](#option-b-custom-app-registration-power-users)
3. [Token Refresh & Revocation](#token-refresh--revocation)
4. [Belangrijke opmerkingen](#important-notes)
5. [Probleemoplossing](#troubleshooting)

---

## 🚀 Optie A: Standaard Setup (Aanbevolen) {#option-a-standard-setup-recommended}

Gebruik deze optie als uw Mail Archiver-instantie een standaard Client ID heeft geconfigureerd (de standaard voor officiële Docker-images). Toegang tot de Azure Portal is niet vereist.

### 🛠️ Vereisten {#prerequisites}

- Een persoonlijk Microsoft-account (Outlook.com, live.com of M365 Family)
- Een apparaat met een browser (smartphone, laptop of tablet) om de autorisatie te voltooien

### 📝 Stappen {#steps}

1. Log in op uw Mail Archiver-applicatie.
2. Navigeer naar **Mail Accounts** > **Create**.
3. Vul de volgende velden in:
   - **Name**: Beschrijvende naam voor het account (bijv. "Personal Outlook")
   - **Email Address**: Uw persoonlijke Microsoft e-mailadres (bijv. `you@outlook.com`)
   - **Provider**: Selecteer **Microsoft Personal**
4. Er worden geen Client ID- of Client Secret-velden getoond — Mail Archiver gebruikt automatisch de ingebouwde standaardregistratie.
5. Klik op **Create**.
6. De device-code autorisatiepagina wordt automatisch geopend. Een korte code (bijv. `ABCD-EFGH`) wordt weergegeven samen met een link naar `https://microsoft.com/devicelogin` (of de getoonde verificatie-URL).
7. Open die URL op **elk apparaat** met een browser en log in met uw persoonlijke Microsoft-account.
8. Voer de code uit stap 6 in.
9. Er verschijnt een toestemmingsscherm waarin **"Mail-Archiver"** om IMAP-toegang vraagt. Controleer en keur dit goed.
10. De Mail Archiver-pagina detecteert automatisch het succes en wordt doorgeleid naar de bewerkingsweergave van het account.
11. Klaar — de synchronisatie start automatisch volgens de accountinstellingen.

> ⚠️ **Waarschuwing voor niet-geverifieerde uitgever**: Het toestemmingsscherm kan een waarschuwing tonen dat "Mail-Archiver" een niet-geverifieerde uitgever is. Dit is te verwachten — verificatie van de uitgever vereist een zakelijke identiteit (Microsoft Partner Network-account) en is niet nodig om de flow te laten werken. De app functioneert normaal zonder deze verificatie.

---

## 🔧 Optie B: Custom App Registration (Power Users) {#option-b-custom-app-registration-power-users}

Gebruik deze optie als u de gedeelde standaard Client ID niet wilt gebruiken (bijv. om nalevingsredenen of als u volledige controle wilt over de app-registratie).

### 🛠️ Vereisten {#prerequisites}

- Een Microsoft-account (persoonlijk of werk/school) waarmee u toegang heeft tot de Azure Portal
- Basisbekendheid met de Azure/Entra-portal

### 🚀 Create App Registration {#create-app-registration}

1. Navigeer naar de [Microsoft Entra Admin Center](https://entra.microsoft.com) of de [Azure Portal](https://portal.azure.com).
2. Log in met uw Microsoft-account.
3. Selecteer in het navigatievenster aan de linkerkant **App registrations** (onder Identity > Applications).
4. Klik op **+ New registration**.
5. Vul de volgende details in:
   - **Name**: Voer een beschrijvende naam in (bijv. "Mail Archiver MSA")
   - **Supported account types**: Selecteer **Accounts in any organizational directory and personal Microsoft accounts**
     - Dit is de multi-tenant optie en ondersteunt zowel persoonlijke als zakelijke accounts.
   - **Redirect URI**: Laat dit leeg (niet nodig voor Device Code Flow).
6. Klik op **Register**.
7. Noteer de **Application (client) ID** van de **Overview**-pagina.

### 🔐 Enable Public Client Flows {#enable-public-client-flows}

1. Navigeer in uw app-registratie naar **Authentication** in het linker menu.
2. Zoek onderaan naar **Allow public client flows** en zet dit op **Yes**.
3. Klik op **Save**.

> ℹ️ Er hoeven geen client secret, redirect URI of API-machtigingen handmatig te worden geconfigureerd — de Device Code Flow gebruikt gedelegeerde machtigingen die tijdens runtime worden aangevraagd via de `IMAP.AccessAsUser.All` scope.

### 📧 Configure Mail Archiver {#configure-mail-archiver}

De globale standaard overschrijven (beïnvloedt alle MSA-accounts)

Stel de Client ID in uw omgevingsvariabele in:


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 Token Refresh & Revocation {#token-refresh-revocation}

### Automatische Token Refresh {#automatic-token-refresh}

Na de initiële autorisatie ontvangt Mail Archiver:
- Een **access token** (kortstondig, ~1 uur)
- Een **refresh token** (langdurig, gebruikt om automatisch nieuwe access tokens te verkrijgen)

Mail Archiver ververst de access token automatisch voor elke sync-run. U hoeft niet opnieuw te autoriseren, tenzij de refresh token verloopt of wordt ingetrokken.

### Toegang intrekken (Revoking Access) {#revoking-access}

Om de toegang van Mail Archiver tot uw persoonlijke Microsoft-account in te trekken:

1. Ga naar [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage)
2. Log in met uw persoonlijke Microsoft-account.
3. Zoek **"Mail-Archiver"** (of uw aangepaste app-naam) in de lijst.
4. Klik op **Remove** om de toegang in te trekken.

Na het intrekken kan Mail Archiver niet langer bij het account totdat u opnieuw autoriseert.

---

## ⚠️ Belangrijke opmerkingen {#important-notes}
- **Gedeelde Client ID**: Bij gebruik van de standaard Client ID delen alle Mail Archiver-gebruikers op alle instanties dezelfde Azure App Registration. De beheerder van Mail Archiver is verantwoordelijk voor het actief houden van de registratie. Als Microsoft de gedeelde registratie uitschakelt, moeten alle gebruikers overstappen naar een aangepaste registratie (Optie B) totdat een nieuwe release een bijgewerkte Client ID bevat.


- **Zakelijke accounts**: De Microsoft Personal provider gebruikt het `/common` authority endpoint en ondersteunt zowel persoonlijke als zakelijke (werk/school) accounts. Gebruik voor zakelijke accounts die client-credentials toegang vereisen (app-only, geen gebruikersinlog), in plaats daarvan de **M365** provider.

---

## 🔍 Probleemoplossing {#troubleshooting}

### Mappen zichtbaar op outlook.com maar ontbreken in Mail Archiver {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**Symptoom**: Sommige mappen bestaan in de Postbus (zichtbaar op outlook.com of in Outlook), maar verschijnen nooit in Mail Archiver — noch in de mappenlijst op de accountinstellingenpagina, noch tijdens de synchronisatie. Er wordt geen fout gelogd.

**Oorzaak (serverzijde)**: Mappen die zijn aangemaakt door **third-party clients via EWS** — wat outlook.com-accounts instelt als Exchange-accounts in plaats van IMAP — kunnen een verkeerde `PR_CONTAINER_CLASS` eigenschap hebben (bijv. `IPF.Imap` in plaats van `IPF.Note`). Outlook.com **filtert deze mappen vervolgens volledig uit de IMAP `LIST` response**, ook al zijn ze volledig zichtbaar in de web UI. Dit is een gedrag aan de kant van Microsoft, geen bug in Mail Archiver — hetzelfde fenomeen is gedocumenteerd voor de Microsoft Graph API, waarbij mappen met een niet-`IPF.Note` container class ontbreken uit `mailFolders` lijsten totdat de eigenschap is gecorrigeerd:

Mail Archiver bevraagt de mappenlijst uit drie bronnen (recursieve `LIST`, traversaal per niveau van de kinderen van elke map, en `LSUB`) en voegt de resultaten samen. Wanneer de server een map niet via een van deze bronnen rapporteert, is er voor een IMAP-client geen manier om deze te zien.

**Workaround**: Pas de betreffende mappen eenmalig aan via de outlook.com web UI, zodat Microsoft de container class corrigeert:

1. Open <https://outlook.com> en zoek de ontbrekende map.
2. Hernoem deze (elke tijdelijke naam) en hernoem hem vervolgens weer naar de originele naam — of maak een nieuwe map aan en verplaats de e-mails.
3. Herhaal dit voor elke betreffende map.
4. Start een **Full Resync** van het account vanaf de Account Details pagina in Mail Archiver. Een volledige resync is vereist omdat incrementele syncs alleen e-mail ophalen die nieuwer is dan de laatste syncdatum van het account — e-mails in mappen die nooit zijn ontdekt, zouden anders niet worden gearchiveerd.


*Deze gids is actueel per 2026. Microsoft werkt hun services en UI regelmatig bij, dus sommige stappen kunnen afwijken. Raadpleeg de [Microsoft identity platform documentation](https://learn.microsoft.com/en-us/entra/identity-platform/) voor de laatste details.*
