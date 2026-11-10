# ☁️ Azure App Registration és Retention Policy útmutató a Mail Archiverhez {#azure-app-registration-and-retention-policy-guide-for-mail-archiver}

[← Vissza a Dokumentációs Indexhez](index.md)

## 📋 Overview {#overview}

Ez a útmutató részletes utasításokat tartalmaz az Azure App Registration létrehozásához az e-mailek lekérése céljából, valamint a retention policy konfigurálásához a Mail Archiver alkalmazásban.

## 📚 Tartalomjegyzék {#table-of-contents}

1. [Overview](#overview)
2. [Azure App Registration az e-mail lekérést céljából](#azure-app-registration-for-email-retrieval)
   - [Prerequisites](#prerequisites)
   - [Create App Registration](#create-app-registration)
   - [Set Required API Permissions](#set-required-api-permissions)
   - [Generate Client Secret](#generate-client-secret)
3. [Configure Mail Archiver M365 Account](#configure-mail-archiver-m365-account)
4. [Retention Policy Setup](#retention-policy-setup)
   - [Configure Retention Policy](#configure-retention-policy)

## 🌐 Overview {#overview}

A Mail Archiver alkalmazás Microsoft 365 (M365) fiókokat támogat az OAuth2 client credentials flow használatával az e-mail lekérése érdekében. Emellett rendelkezik retention policy funkcióval, amely lehetővé teszi az e-mailek automatikus törlését a mail szerverről egy megadott időszak után, így segíti a tárhelykezelést a teljes archivum fenntartása mellett.

## ☁️ Azure App Registration az e-mail lekérést céljából {#azure-app-registration-for-email-retrieval}

### 🛠️ Prerequisites {#prerequisites}

- Adminisztratív hozzáférés a Microsoft Entra ID (Azure AD) rendszerhez
- Microsoft 365 tenant Exchange Online licencsellal

### 🚀 Create App Registration {#create-app-registration}

1. Navigáljon a [Microsoft Entra Admin Center](https://entra.microsoft.com) felületre
2. Jollyuk jelentkezzen be az adminisztrátori fiókkal
3. A bal oldali navigációs paneлben válassza az **App registrations** lehetőséget
4. Kattintson a **+ New registration** gombra az App registrations oldal tetején
5. Töltse ki a következő adatokat:
   - **Name**: Adjon meg egy leíró nevet (pl. "Mail Archiver M365 Provider")
   - **Supported account types**: Válassza az **Accounts in this organizational directory only** (Single tenant) opciót
     - Ez szükséges a Mail Archiver által használt client credentials flowhoz
   - **Redirect URI**: Hagyja üresen (nincs rá szükség a client credentials flowhoz)
6. Kattintson a **Register** gombra

**Fontos**: Jegyezze fel a következő értékeket az **Overview** oldalról:
- **Application (client) ID** - Erre lesz szüksége a Mail Archiverben mint `ClientId`
- **Directory (tenant) ID** - Erre lesz szüksége a Mail Archiverben mint `TenantId`

### 🔐 Set Required API Permissions {#set-required-api-permissions}

1. Navigáljon az **API permissions** menüpontra a bal oldali menüben
2. Kattintson a **+ Add a permission** gombra
3. Válassza a **Microsoft Graph** lehetőséget
4. Válassza az **Application permissions** opciót (szükséges a Mail Archiver client credentials flowhoz)
5. Adjon hozzá az alábbi jogosultságokat:
   - **Mail.Read** - E-mailek olvasása az összes postafiókban
   - **Mail.ReadWrite** - E-mailek olvasása és írása az összes postafiókban (visszaállítási funkcióhoz és törléshez is)
   - **User.Read.All** - Minden felhasználó teljes profiljának olvasása (szükséges a Microsoft 365 tenant mailbox importáláshoz; emellett biztosítja a `assignedPlans` értéket az Exchange licenc szűréséhez)

6. Kattintson a **Add permissions** gombra
7. **KÖTELEZŐ**: Kattintson a **Grant admin consent for [Your Organization]** gombra
8. Erősítse meg a **Yes** gomb megnyomásával

**Megjegyzés**: Az Application permissions szükséges, mert a Mail Archiver a client credentials flow használatával fér hozzá a postafiókokhoz felhasználói interakció nélkül.

### 🔑 Generate Client Secret {#generate-client-secret}

1. Navigáljon a **Certificates & secrets** menüpontra a bal oldali menüben
2. A **Client secrets** alatt kattintson a **+ New client secret** gombra
3. Adjon meg egy leírást (pl. "Mail Archiver Secret")
4. Válasszon egy lejárati időt
5. Kattintson az **Add** gombra
6. **Fontos**: Másolja ki a **Value** értéket azonnal, és tárolja biztonságosan. Ez a titkos érték (secret) nem lesz újra látható.

## 📧 Configure Mail Archiver M365 Account {#configure-mail-archiver-m365-account}

Az alkalmazás regisztrációjának befejezését követően konfigurálnia kell egy M365 e-mail fiókot a Mail Archiverben az alábbi értékekkel:

### 📋 Required Values from App Registration: {#required-values-from-app-registration}

1. **Client ID**: Másolja ki az **Application (client) ID** értéket az app registration **Overview** oldaláról
2. **Client Secret**: Másolja ki a **Value** értéket, amelyet a client secret létrehozásakor mentett el
3. **Tenant ID**: Másolja ki a **Directory (tenant) ID** értéket az app registration **Overview** oldaláról

### 🚀 Creating M365 Account in Mail Archiver: {#creating-m365-account-in-mail-archiver}

1. Lépjen be a Mail Archiver alkalmazásába
2. Navigáljon a **Mail Accounts** > **Create** menüpontba
3. Töltse ki a következő mezőket:
   - **Name**: Leíró név a fiókhoz (pl. "Sales Team M365")
   - **Email Address**: A cél postafiók e-mail címe, amelynek archiválása történik
   - **Provider**: Válassza az **M365** lehetőséget
   - **Client ID**: Adja meg az Application (client) ID értéket az app registration alapján
   - **Client Secret**: Adja meg a mentett client secret értéket
   - **Tenant ID**: Adja meg a Directory (tenant) ID értéket az app registration alapján

4. Kattintson a **Create** gombra

### ⚠️ Important Notes: {#important-notes}

- A **Email Address** mezőnek tartalmaznia kell a tényleges postafiók e-mail címét, ha egyetlen M365 kontét hozon létre.
- Az app registration rendelkeznie kell a megadott postafiók eléréséhez szükséges jogosultságokkal.
- Amennyiben egyszerre több vagy kiválasztott postafiókot szeretne létrehozni a tenantből, használja a [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) útmutatót.


## 🗑️ Retention Policy Setup {#retention-policy-setup}

A Mail Archiver alkalmazás rendelkezik retention policy funkcióval, amely lehetővé teszi az e-mailek automatikus törlését az IMAP szerverről a sikeres archiválás után, segíti a tárhelykezelést az IMAP szerveren a teljes archivum fenntartása mellett.

### 🛠️ Configure Retention Policy {#configure-retention-policy}

1. Navigáljon a **Mail Accounts** menüpontba a Mail Archiver alkalmazásban
2. Vagy létrehoz egy új M365 fiókot, vagy szerkesztjen egy meglévőt
3. A fiók konfigurációs űrlapon keresse meg a **Delete After Days** mezőt
4. Adja meg a napok számát, amely után az archivált e-maileket törölni kell a szerverről:
   - Például: írjon be `30` az e-mailek 30 nap utáni törléséhez
   - Hagyja üresen az automatikus törlés kikapcsolásához
5. Mentse el a fiók konfigurációt


## ℹ️ Information Regarding Migrated Mailboxes {#ℹ-information-regarding-migrated-mailboxes}

### 📁 Folders Not Visible After Migration {#folders-not-visible-after-migration}

Ha a postafiókját egy másik e-mail szolgáltatóról Microsoft 365-re migrálta, előfordulhat, hogy egyes mappák nem jelennek meg a mappalista listájában.

#### 🔍 Background {#background}

A Mail Archiver a Microsoft Graph API keresztül szerez mappainformációkat a M365 postafiókokból. Az alkalmazás helyesen kéri az összes elérhető mappát (beleértve a rejtett mappákat is), és végigjárja a teljes mappahierarchiát.

Azonban a Microsoft Graph API csak olyan mappákat ad vissza, ahol a `PR_CONTAINER_CLASS` értéke `IPF.Note`. Más e-mail rendszerekből történő mailbox migráció során a mappák más konténertípushoz (container class) is rendelhetnek, mint például a `IPF.Imap`. Ezek a mappák nem érhetőek el a Graph API mappalista végpontjai through.

Ez a viselkedés minden olyan alkalmazásra vonatkozik, amely ezt az API-t használja a mappák lekérésehez.

#### 🔧 Workaround Options {#workaround-options}

**Option 1: Use MFCMAPI Tool (Recommended)**

1. Töltse le a [MFCMAPI](https://github.com/Microsoft/mfcmapi/) eszközt a Microsoft hivatalos tárolójából
2. Indítsa el a MFCMAPI-t, és csatlakozzon a postafiókjához
3. Navigáljon az egyes hiányzó mappákhoz
4. Állítsa át a `PR_CONTAINER_CLASS` tulajdonértéket `IPF.Imap`-ról `IPF.Note`-re
5. Indítsa újra az Outlookot, és töltje vissza a mappákat a Mail Archiverben

**Option 2: Recreate Folders in Outlook

1. Hozjon létre új mappákat közvetlenül az Outlookban (ezek a megfelelő `IPF.Note` osztályral rendelkeznek)
2. Mozgassa át az összes e-mailt a migrált mappákból az új mappákba
3. Amennyiben szeretné, törölje a régi, migrált mappákat

**Option 3: PowerShell Script (For Bulk Changes)**

Szásonként migrált mappákkal rendelkező postafiókok esetén használhat PowerShell-t az Exchange Online modüllal, hogy egyszerre több mappához módosítsa a konténertípust. Az erre vonatkozó segítségért forduljon Exchange adminisztrátorához.


---

**Megjegyzés**: Ez az útmutató 2025-ös állapotnak felel meg. A Microsoft rendszeresen frissíti szolgáltatásait, így egyes felületek eltérőek lehetnek. Mindig a legfrissebb Microsoft dokumentációt tekintse alapul a legaktuálisabb információkért.
