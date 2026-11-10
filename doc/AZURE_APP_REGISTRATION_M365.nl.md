# ☁️ Azure App Registration en Bewaring Policy Gids voor Mail Archiver {#azure-app-registration-and-retention-policy-guide-for-mail-archiver}

[← Terug naar Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Deze gids biedt uitgebreide instructies voor het instellen van Azure App Registration voor e-mail ophalen en het configureren van bewaring policies in de Mail Archiver applicatie.

## 📚 Inhoudsopgave {#table-of-contents}

1. [Overzicht](#overview)
2. [Azure App Registration voor E-mail ophalen](#azure-app-registration-for-email-retrieval)
   - [Vereisten](#prerequisites)
   - [App Registration aanmaken](#create-app-registration)
   - [Vereiste API-machtigingen instellen](#set-required-api-permissions)
   - [Client Secret genereren](#generate-client-secret)
3. [Mail Archiver M365 Account configureren](#configure-mail-archiver-m365-account)
4. [Bewaring Policy Setup](#retention-policy-setup)
   - [Bewaring Policy configureren](#configure-retention-policy)

## 🌐 Overzicht {#overview}

De Mail Archiver applicatie ondersteunt Microsoft 365 (M365) accounts met behulp van de OAuth2 client credentials flow voor het ophalen van e-mail. Daarnaast biedt het functionaliteit voor bewaring policies om e-mails automatisch van de mailserver te verwijderen na een gespecificeerde periode, wat helpt bij het beheren van de opslagruimte terwijl een volledig archief behouden blijft.

## ☁️ Azure App Registration voor E-mail ophalen {#azure-app-registration-for-email-retrieval}

### 🛠️ Vereisten {#prerequisites}

- Administratieve toegang tot Microsoft Entra ID (Azure AD)
- Een Microsoft 365 tenant met Exchange Online licenties

### 🚀 App Registration aanmaken {#create-app-registration}

1. Navigeer naar de [Microsoft Entra Admin Center](https://entra.microsoft.com)
2. Log in met uw administrator account
3. Selecteer **App registrations** in het navigatiepaneel aan de linkerzijde
4. Klik op **+ New registration** bovenaan de App registrations pagina
5. Vul de volgende details in:
   - **Name**: Voer een beschrijvende naam in (bijv. "Mail Archiver M365 Provider")
   - **Supported account types**: Selecteer **Accounts in this organizational directory only** (Single tenant)
     - Dit is vereist voor de client credentials flow die door Mail Archiver wordt gebruikt
   - **Redirect URI**: Laat dit leeg (niet nodig voor de client credentials flow)
6. Klik op **Register**

**Belangrijk**: Noteer de volgende waarden van de **Overview** pagina:
- **Application (client) ID** - Dit heeft u nodig als `ClientId` in Mail Archiver
- **Directory (tenant) ID** - Dit heeft u nodig als `TenantId` in Mail Archiver

### 🔐 Vereiste API-machtigingen instellen {#set-required-api-permissions}

1. Navigeer naar **API permissions** in het menu aan de linkerzijde
2. Klik op **+ Add a permission**
3. Selecteer **Microsoft Graph**
4. Kies **Application permissions** (vereist voor de client credentials flow van Mail Archiver)
5. Voeg de volgende machtigingen toe:
   - **Mail.Read** - E-mail lezen in alle Postbussen
   - **Mail.ReadWrite** - E-mail lezen en schrijven in alle Postbussen (ook voor de herstel functie en verwijdering)
   - **User.Read.All** - Alle profielen van gebruikers lezen (vereist voor de import van Microsoft 365 tenant Postbussen; biedt ook `assignedPlans` voor Exchange licentie filtering)

6. Klik op **Add permissions**
7. **CRITIEK**: Klik op **Grant admin consent for [Your Organization]**
8. Bevestig door op **Yes** te klikken

**Opmerking**: Application permissions zijn vereist omdat Mail Archiver de client credentials flow gebruikt om toegang te krijgen tot Postbussen zonder gebruikersinteractie.

### 🔑 Client Secret genereren {#generate-client-secret}

1. Navigeer naar **Certificates & secrets** in het menu aan de linkerzijde
2. Klik onder **Client secrets** op **+ New client secret**
3. Geef een beschrijving op (bijv. "Mail Archiver Secret")
4. Selecteer een vervaldatum
5. Klik op **Add**
6. **Belangrijk**: Kopieer de **Value** onmiddellijk en bewaar deze veilig. Deze secret wordt niet opnieuw getoond.

## 📧 Mail Archiver M365 Account configureren {#configure-mail-archiver-m365-account}

Nadat de app registration is voltooid, moet u een M365 mail account configureren in Mail Archiver met de volgende waarden:

### 📋 Vereiste waarden uit App Registration: {#required-values-from-app-registration}

1. **Client ID**: Kopieer de **Application (client) ID** van de **Overview** pagina van de app registration
2. **Client Secret**: Kopieer de **Value** die u heeft opgeslagen bij het aanmaken van de client secret
3. **Tenant ID**: Kopieer de **Directory (tenant) ID** van de **Overview** pagina van de app registration

### 🚀 Een M365 Account aanmaken in Mail Archiver: {#creating-m365-account-in-mail-archiver}

1. Log in op uw Mail Archiver applicatie
2. Navigeer naar **Mail Accounts** > **Create**
3. Vul de volgende velden in:
   - **Name**: Beschrijvende naam voor het account (bijv. "Sales Team M365")
   - **Email Address**: Het doel Postbus e-mailadres om te archiveren
   - **Provider**: Selecteer **M365**
   - **Client ID**: Voer de Application (client) ID in van uw app registration
   - **Client Secret**: Voer de opgeslagen client secret waarde in
   - **Tenant ID**: Voer de Directory (tenant) ID in van uw app registration

4. Klik op **Create**

### ⚠️ Belangrijke opmerkingen: {#important-notes}

- Het veld **Email Address** moet het werkelijke Postbus adres bevatten dat u wilt archiveren bij het aanmaken van een enkel M365 account.
- De app registration moet de machtigingen hebben om toegang te krijgen tot de gespecificeerde Postbus.
- Om in één keer accounts aan te maken voor alle of geselecteerde Postbussen in de tenant, kunt u de [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) gids gebruiken.


## 🗑️ Bewaring Policy Setup {#retention-policy-setup}

De Mail Archiver applicatie biedt bewaring policy functionaliteit om e-mails automatisch van de IMAP server te verwijderen nadat ze succesvol zijn gearchiveerd, wat helpt bij het beheren van de opslagruimte op de IMAP server terwijl een volledig archief behouden blijft.

### 🛠️ Bewaring Policy configureren {#configure-retention-policy}

1. Navigeer naar **Mail Accounts** in de Mail Archiver applicatie
2. Maak een nieuwe M365 account aan of bewerk een bestaande
3. Zoek in het account configuratieformulier het veld **Delete After Days**
4. Voer het aantal dagen in waarna gearchiveerde e-mails van de server verwijderd moeten worden:
   - Voer bijvoorbeeld `30` in om e-mails na 30 dagen te verwijderen
   - Laat het leeg om automatische verwijdering uit te schakelen
5. Sla de account configuratie op


## ℹ️ Informatie over gemigreerde Postbussen {#ℹ-information-regarding-migrated-mailboxes}

### 📁 Mappen niet zichtbaar na migratie {#folders-not-visible-after-migration}

Als u uw Postbus heeft gemigreerd van een andere e-mailprovider naar Microsoft 365, worden sommige mappen mogelijk niet getoond in de mappenlijst.

#### 🔍 Achtergrond {#background}

Mail Archiver haalt mapinformatie op uit M365 Postbussen via de Microsoft Graph API. De applicatie vraagt correct alle beschikbare mappen op (inclusclusief verborgen mappen) en doorloopt de volledige mappenhiërarchie.

De Microsoft Graph API geeft echter alleen mappen terug met `PR_CONTAINER_CLASS` ingesteld op `IPF.Note`. Tijdens Postbus migraties van andere e-mailsystemen kunnen mappen een andere container class toegewezen krijgen, zoals `IPF.Imap`. Deze mappen worden niet blootgesteld via de folder listing endpoints van de Graph API.

Dit gedrag beïnvloedt alle applicaties die deze API gebruiken voor het ophalen van mappen.

#### 🔧 Workaround opties {#workaround-options}

**Optie 1: Gebruik MFCMAPI Tool (Aanbevolen)**

1. Download [MFCMAPI](https://github.com/Microsoft/mfcmapi/) van de officiële repository van Microsoft
2. Start MFCMAPI en maak verbinding met uw Postbus
3. Navigeer naar de ontbrekende map
4. Wijzig de `PR_CONTAINER_CLASS` eigenschap van `IPF.Imap` naar `IPF.Note`
5. Start Outlook opnieuw op en laad de mappen opnieuw in Mail Archiver

**Optie 2: Mappen opnieuw aanmaken in Outlook**

1. Maak nieuwe mappen direct in Outlook aan (deze zullen de juiste `IPF.Note` class hebben)
2. Verplaats alle e-mails van de gemigreerde mappen naar de nieuwe mappen
3. Verwijder de oude gemigreerde mappen indien gewenst

**Optie 3: PowerShell Script (Voor bulk wijzigingen)**

Voor Postbussen met veel gemigreerde mappen kunt u PowerShell gebruiken met de Exchange Online module om de container class voor meerdere mappen tegelijk te wijzigen. Neem contact op met uw Exchange administrator voor hulp bij deze aanpak.


---

**Opmerking**: Deze gids is actueel per 2025. Microsoft werkt hun services regelmatig bij, dus sommige UI-elementen kunnen afwijken. Raadpleeg altijd de nieuwste Microsoft documentatie voor de meest actuele informatie.
