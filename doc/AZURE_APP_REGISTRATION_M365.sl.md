# ☁️ Vodnik za registracijo aplikacije v Azure in politiko zadrževanja za Mail Archiver {#azure-app-registration-and-retention-policy-guide-for-mail-archiver}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodnik nudi celovita navodila za nastavitev registracije aplikacije v Azure za pridobivanje e-pošte in konfiguracijo politik zadrževanja v aplikaciji Mail Archiver.

## 📚 Vsebina {#table-of-contents}

1. [Pregled](#overview)
2. [Registracija aplikacije v Azure za pridobivanje e-pošte](#azure-app-registration-for-email-retrieval)
   - [Predpogoji](#prerequisites)
   - [Ustvarite registracijo aplikacije](#create-app-registration)
   - [Nastavite zahtevane API dovolilnice](#set-required-api-permissions)
   - [Generirajte klijantski tajni ključ (Client Secret)](#generate-client-secret)
3. [Konfiguracija M365 računa Mail Archiver](#configure-mail-archiver-m365-account)
4. [Nastavitev politike zadrževanja](#retention-policy-setup)
   - [Konfiguracija politike zadrževanja](#configure-retention-policy)

## 🌐 Pregled {#overview}

Aplikacija Mail Archiver podpira Microsoft 365 (M365) račune z uporabo OAuth2 pretoka klijentskih uverjenj za pridobivanje e-pošte. Poleg tega nudi funkcionalnost politike zadrževanja za samodejno brisanje e-pošte z e-poštn worthega strežnika po določenem obdobju, kar pomaga pri upravljanju prostora za shranovanje ob ohranjanju celovite arhive.

## ☁️ Registracija aplikacije v Azure za pridobivanje e-pošte {#azure-app-registration-for-email-retrieval}

### 🛠️ Predpogoji {#prerequisites}

- Administratorski dostop do Microsoft Entra ID (Azure AD)
- Microsoft 365 tenant z Exchange Online licencami

### 🚀 Ustvarite registracijo aplikacije {#create-app-registration}

1. Pojdite na [Microsoft Entra Admin Center](https://entra.microsoft.com)
2. Prijavite se s svojim administratorskim računom
3. V levem navigacijskem polju izberite **App registrations**
4. Na vrhu strani App registrations kliknite **+ New registration**
5. Izpolnite naslednje podrobnosti:
   - **Name**: Vnesite opisno ime (npr. "Mail Archiver M365 Provider")
   - **Supported account types**: Izberite **Accounts in this organizational directory only** (Single tenant)
     - To je zahtevano za pretok klijentskih uverjenj, ki ga uporablja Mail Archiver
   - **Redirect URI**: Pustite prazno (ni必要 za pretok klijentskih uverjenj)
6. Kliknite **Register**

**Pomembno**: Zapišite si naslednje vrednosti s strani **Overview**:
- **Application (client) ID** - To boste potrebovali kot `ClientId` v Mail Archiver
- **Directory (tenant) ID** - To boste potrebovali kot `TenantId` v Mail Archiver

### 🔐 Nastavite zahtevane API dovolilnice {#set-required-api-permissions}

1. V levem meniju pojdite na **API permissions**
2. Kliknite **+ Add a permission**
3. Izberite **Microsoft Graph**
4. Izberite **Application permissions** (zahtevano za pretok klijentskih uverjenj v Mail Archiver)
5. Dodajte naslednje dovolilnice:
   - **Mail.Read** - Branje pošte v vseh poštovnih predalih
   - **Mail.ReadWrite** - Branje in zapisovanje pošte v vseh poštovnih predalih (za funkcijo obnavitve in brisanje)
   - **User.Read.All** - Branje celotnih profilov vseh uporabnikov (za uvoz poštovnih predalov Microsoft 365 tenant; prav tako zagotavlja `assignedPlans` za filtriranje Exchange licenc)

6. Kliknite **Add permissions**
7. **KRITIČNO**: Kliknite **Grant admin consent za [Vaša organizacija]**
8. Potrdite s klikom na **Yes**

**Opomba**: Aplikacijske dovolilnice so potrebne, ker Mail Archiver uporablja pretok klijentskih uverjenj za dostop do poštovnih predalov brez interakcije uporabnika.

### 🔑 Generirajte klijantski tajni ključ (Client Secret) {#generate-client-secret}

1. V levem meniju pojdite na **Certificates & secrets**
2. Pod **Client secrets** kliknite **+ New client secret**
3. Navedite opis (npr. "Mail Archiver Secret")
4. Izberite obdobje potečne
5. Kliknite **Add**
6. **Pomembno**: Takoj kopirajte **Value** in ga varno shranite. Ta tajni ključ ne bo več prikazan.

## 📧 Konfiguracija M365 računa Mail Archiver {#configure-mail-archiver-m365-account}

Po opravljeni registraciji aplikacije morate v Mail Archiver konfigurirati M365 e-poštni račun z naslednjimi vrednostmi:

### 📋 Obvezne vrednosti iz registracije aplikacije: {#required-values-from-app-registration}

1. **Client ID**: Kopirajte **Application (client) ID** s strani **Overview** registracije aplikacije
2. **Client Secret**: Kopirajte **Value**, ki ste ga shranili ob ustvarjanju klijentskega tajnega ključa
3. **Tenant ID**: Kopirajte **Directory (tenant) ID** s strani **Overview** registracije aplikacije

### 🚀 Ustvarjanje M365 računa v Mail Archiver: {#creating-m365-account-in-mail-archiver}

1. Prijavite se v svojo aplikacijo Mail Archiver
2. Pojdite na **Mail Accounts** > **Create**
3. Izpolnite naslednja polja:
   - **Name**: Opisno ime za račun (npr. "Sales Team M365")
   - **Email Address**: Ciljni e-poštni naslov poštovnega predala za arhiviranje
   - **Provider**: Izberite **M365**
   - **Client ID**: Vnesite Application (client) ID iz registracije vaše aplikacije
   - **Client Secret**: Vnesite vrednost klijentskega tajnega ključa, ki ste jo shranili
   - **Tenant ID**: Vnesite Directory (tenant) ID iz registracije vaše aplikacije

4. Kliknite **Create**

### ⚠️ Pomembne opombe: {#important-notes}

- Polje **Email Address** mora vsebovati dejanski poštovni predal, ki ga želite arhivirati, ko ustvarjate en M365 račun.
- Registracija aplikacije mora imeti dovolilnice za dostop do navedenega poštovnega predala.
- Za h fasto ustvarjanje računov za vse ali izbrane poštovne predale v tenantu uporabilte vodnik [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md).


## 🗑️ Nastavitev politike zadrževanja {#retention-policy-setup}

Aplikacija Mail Archiver nudi funkcionalnost politike zadrževanja za samodejno brisanje e-pošte z IMAP strežnika, ko je bila uspešno arhivirana, kar pomaga pri upravljanju prostora za shranovanje na IMAP strežniku ob ohranjanju celovite arhive.

### 🛠️ Konfiguracija politike zadrževanja {#configure-retention-policy}

1. V aplikaciji Mail Archiver pojdite na **Mail Accounts**
2. Ustvarite nov M365 račun ali uredite obstoječi
3. V obrazcu za konfiguracijo računa poiščite polje **Delete After Days**
4. Vnesite število dni, po katerih naj se arhivirana e-pošta izbriše s strežnika:
   - Na primer, vnesite `30` za brisanje e-pošte po 30 dneh
   - Pustite prazno za onemogočanje samodejnega brisanja
5. Shrani konfiguracijo računa


## ℹ️ Informacije glede migriranih poštovnih predalov {#ℹ-information-regarding-migrated-mailboxes}

### 📁 Mapi niso vidne po migraciji {#folders-not-visible-after-migration}

Če ste svoj poštovni predal migrirali iz drugega ponudnika e-pošte v Microsoft 365, se lahko nekatere mape ne pojavijo na seznamu map.

#### 🔍 Ozadje {#background}

Mail Archiver pridobavlja informacije o mapah iz M365 poštovnih predalov prek Microsoft Graph API. Aplikacija pravilno zahteva vse razpoložljive mape (vključno s skritimi mapami) in prenaša celotno hierarhijo map.

Vendar pa Microsoft Graph API vrne le mape, kjer je `PR_CONTAINER_CLASS` nastavljeno na `IPF.Note`. Med migracijami poštovnih predalov iz drugih e-poštnih sistemov so lahko mapam dodeljene druge klase kontejnerjev, kot je `IPF.Imap`. Te mape niso razpoložljive preko koncev endpoints za seznam map v Graph API.

Ta vedenje vpliva na vse aplikacije, ki uporabljajo ta API za pridobivanje map.

#### 🔧 Možnosti reševanja {#workaround-options}

**Možnost 1: Uporaba orodja MFCMAPI (Priporočeno)**

1. Prenesite [MFCMAPI](https://github.com/Microsoft/mfcmapi/) iz uradnega skladišča Microsoft
2. Zaženite MFCMAPI in se povežite s svojim poštovnim predalom
3. Pojdite do vsake manjkajoče mape
4. Spremenite lastnost `PR_CONTAINER_CLASS` iz `IPF.Imap` v `IPF.Note`
5. Ponovno zaženite Outlook in ponovno naložite mape v Mail Archiver

**Možnost 2: Ponovno ustvarjanje map v Outlooku**

1. Izberite nove mape neposredno v Outlooku (te bodo imele pravilno klaso `IPF.Note`)
2. Premaknite vse e-poštne sporočilo iz migriranih map v nove mape
3. Po želji izbrišite stare migrirane mape

**Možnost 3: PowerShell skripta (za množične spremembe)**

Za poštovne predale z veliko migriranih map lahko uporabite PowerShell z modulom Exchange Online za h fasto spremembo klase kontejnerja za več map hkrati. Za pomoč pri tem pristopu kontaktirajte svojega administratorja Exchange.


---

**Opomba**: Ta vodnik je aktualen do leta 2025. Microsoft redno posodablja svoje storitve, zato se lahko nekateri elementi uporabniškega vmesnika razlikujejo. Za najnovejše informacije vedno preverite najnovejšo Microsoftovo dokumentacijo.
