# ☁️ Nastavitev osebnega Microsoftovega računa (Outlook.com / M365 Family) {#personal-microsoft-account-outlookcom-m365-family-setup}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodič pojasnjuje, kako v Mail Archiverju arhivirati e-poštna sporočila z **osebnenga Microsoftovega računa** (Outlook.com, Hotmail, live.com ali Microsoft 365 Family).

Za razliko od organizacijskih Microsoft 365 računov (ki uporabljajo tok client-credentials — poglejte [Azure App Registration for M365](AZURE_APP_REGISTRATION_M365.md)), osebni računi uporabljajo **OAuth2 s tokom Device Code Flow (RFC 8628)**. Microsoft je za osebne račune onemogočil Basic Auth in gesla za aplikacije, zato je OAuth2 obvezen.

Mail Archiver je opremljen s **predregistriranim skupnim Client ID**, tako da večina uporabnikov **ne** potrebuje ustvariti lastne Azure App Registration. Napredni uporabniki lahko še vedno registrirajo svojo aplikacijo (poglejte možnost B spodaj).

## 📚 Kazalo vsebine {#table-of-contents}

1. [Možnost A: Standardna nastavitev (priporočeno)](#option-a-standard-setup-recommended)
2. [Možnost B: Lastna registracija aplikacije (napredni uporabniki)](#option-b-custom-app-registration-power-users)
3. [Osveževanje in preklic tokena](#token-refresh--revocation)
4. [Pomembne opombe](#important-notes)
5. [Reševanje težav](#troubleshooting)

---

## 🚀 Možnost A: Standardna nastavitev (priporočeno) {#option-a-standard-setup-recommended}

Uporabite to možnost, če ima vaš Mail Archiver instance konfiguriran privzeto Client ID (privzeto za uradne Docker slike). Dostop do Azure Portala ni potreben.

### 🛠️ Predpogoji {#prerequisites}

- Osebni Microsoftov račun (Outlook.com, live.com ali M365 Family)
- Naprava s spletnim brskalnikom (pametni telefon, prenosni računalnik ali tablica) za dokončanje avtorizacije

### 📝 Koraki {#steps}

1. Prijavite se v svojo aplikacijo Mail Archiver.
2. Pojdite na **Mail Accounts** > **Create**.
3. Izpolnite naslednja polja:
   - **Name**: Opisno ime za račun (npr. "Personal Outlook")
   - **Email Address**: Vaš osebni Microsoftov e-poštni naslov (npr. `you@outlook.com`)
   - **Provider**: Izberite **Microsoft Personal**
4. Polja Client ID ali Client Secret niso prikazana — Mail Archiver samodejno uporabi vgrajeno privzeto registracijo.
5. Kliknite **Create**.
6. Stran za avtorizacijo s kodo naprave se odpre samodejno. Prikazana bo kratka koda (npr. `ABCD-EFGH`) skupaj z povezavo do `https://microsoft.com/devicelogin` (ali z prikazano URL-sko za preverjanje).
7. Na **kateri koli napravi** s spletnim brskalnikom odprite to URL-sko in se prijavite s svojim osebnim Microsoftovim računom.
8. Vnesite kodo iz koraka 6.
9. Pojavi se zaslon za privolitev, ki prikazuje **"Mail-Archiver"**, ko zahteva dostop do IMAP. Preglejte in odobrite.
10. Stran Mail Archiverja samodejno zazna uspeh in vas preusmeri na pogled za urejanje računa.
11. Končano — sinhronizacija se začne samodejno glede na nastavitve računa.

> ⚠️ **Opozorilo o nepreverjenem izdajatelju**: Zaslon za privolitev lahko prikaže opozorilo, da je "Mail-Archiver" nepreverjen izdajatelj. To je pričakovano — preverjanje izdajatelja zahteva poslovno identiteto (račun Microsoft Partner Network) in ni potrebno za delovanje toka. Aplikacija deluje normalno tudi brez tega.

---

## 🔧 Možnost B: Lastna registracija aplikacije (napredni uporabniki) {#option-b-custom-app-registration-power-users}

Uporabite to možnost, če ne želite uporabljati skupnega privzetega Client ID (npr. zaradi skladnosti ali če želite popoln nadzor nad registracijo aplikacije).

### 🛠️ Predpogoji {#prerequisites}

- Microsoftov račun (osebni ali delovni/šolski), ki lahko dostopa do Azure Portala
- Osnovno poznavanje Azure/Entra portala

### 🚀 Ustvarite registracijo aplikacije {#create-app-registration}

1. Pojdite na [Microsoft Entra Admin Center](https://entra.microsoft.com) ali [Azure Portal](https://portal.azure.com).
2. Prijavite se s svojim Microsoftovim računom.
3. V levem navigacijskem zavihku izberite **App registrations** (pod Identity > Applications).
4. Kliknite **+ New registration**.
5. Izpolnite naslednje podrobnosti:
   - **Name**: Vnesite opisno ime (npr. "Mail Archiver MSA")
   - **Supported account types**: Izberite **Accounts in any organizational directory and personal Microsoft accounts**
     - To je možnost za večtenančni (multi-tenant) dostop in podpira tako osebne kot organizacijske račune.
   - **Redirect URI**: Pustite prazno (ni potrebna za Device Code Flow).
6. Kliknite **Register**.
7. Zapišite si **Application (client) ID** s strani **Overview**.

### 🔐 Omogočite tokove javnih odjemalcev (Public Client Flows) {#enable-public-client-flows}

1. V registraciji svoje aplikacije pojdite na **Authentication** v levem meniju.
2. Na dnu poiščite **Allow public client flows** in nastavite na **Yes**.
3. Kliknite **Save**.

> ℹ️ Ni treba ročno konfigurirati client secret, redirect URI ali API dovoljenj — Device Code Flow uporablja deležene dovoljenja, ki se zahtevajo v času delovanja prek obsega `IMAP.AccessAsUser.All`.

### 📧 Konfiguracija Mail Archiverja {#configure-mail-archiver}

Pretečen privzeto nastavitve (vpliva na vse MSA račune)

Nastavite Client ID v svojo okolijsko spremenljivko:


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 Osveževanje in preklic tokena {#token-refresh-revocation}

### Avtomatsko osveževanje tokena {#automatic-token-refresh}

Po začetni avtorizaciji Mail Archiver prejme:
- **access token** (kratkoročen, ~1 ura)
- **refresh token** (dolgotrajen, uporabljen za samodejno pridobivanje novih dostopnih tokenov)

Mail Archiver pred vsakim zagonom sinhronizacije samodejno osveži dostopni token. Ponovne avtorizacije ne potrebujete, razen če izteče ali je preklican refresh token.

### Preklic dostopa {#revoking-access}

Za preklic dostopa Mail Archiverja do vašega osebnega Microsoftovega računa:

1. Obiščite [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage)
2. Prijavite se s svojim osebnim Microsoftovim računom.
3. Na seznamu poiščite **"Mail-Archiver"** (ali ime vaše prilagojene aplikacije).
4. Kliknite **Remove**, da prekličete dostop.

Po preklicu Mail Archiver ne more več dostopati do računa, dokler ga ne avtorizirate ponovno.

---

## ⚠️ Pomembne opombe {#important-notes}
- **Skupni Client ID**: Pri uporabi privzetega Client ID vsi uporabniki Mail Archiverja na vseh instancah uporabljajo isto Azure App Registration. Skrbnik Mail Archiverja je odgovoren za ohranjanje registracije aktivne. Če Microsoft onemogoči skupno registracijo, bodo morali vsi uporabniki preiti na lastno registracijo (Možnost B), dokler nova različica ne bo prinesla posodobljenega Client ID.


- **Organizacijski računi**: Ponudnik Microsoft Personal uporablja razcepni endpoint `/common` in podpira tako osebne kot organizacijske (delovne/šolske) račune. Za organizacijske račune, ki zahtevajo dostop prek client-credentials (samo aplikacija, brez prijave uporabnika), uporabite namesto tega ponudnika **M365**.

---

## 🔍 Reševanje težav {#troubleshooting}

### Mapi so vidni na outlook.com, vendar jih manjka v Mail Archiverju {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**Simptom**: Nekateri mapi obstajajo v poštnem predalu (so vidni na outlook.com ali v Outlooku), vendar se nikoli ne pojavijo v Mail Archiverju — ne na seznamu map na strani nastavitev računa niti med sinhronizacijo. Ni zabeležene nobene napake.

**Vzrok (strani strežnika)**: Mapi, ki so jih ustvarili **tretji uporabniki prek EWS** — kar nastavi račune outlook.com kot Exchange račune namesto IMAP — lahko imajo napačno lastnost `PR_CONTAINER_CLASS` (npr. `IPF.Imap` namesto `IPF.Note`). Outlook.com nato **te mape popolnoma izloči iz IMAP `LIST` odgovora**, čeprav so popolnoma vidni v spletnem vmesniku. To je vedenje na strani Microsofta, ne napaka v Mail Archiverju — isti pojav je dokumentiran za Microsoft Graph API, kjer mape z ne-`IPF.Note` klaso kontejnerja manjkajo v seznamih `mailFolders`, dokler lastnost ni popravljena:

Mail Archiver vpraša seznam map iz treh virov (rekurzivni `LIST`, pregled vsakega nivoja otrok mape in `LSUB`) in združi rezultate. Ko strežnik ne prijavi mape skozi noben od njih, IMAP klijent je ne more videti.

**Rešitev**: Enkrat "dotaknite" prizadete mape prek spletnega vmesnika outlook.com, da Microsoft popravi klaso kontejnerja:

1. Odprite <https://outlook.com> in poiščite manjkajočo mapo.
2. Preimenujte jo (karkoli začasno ime) in jo nato preimenujte nazaj — ali ustvarite novo mapo in v njo premaknite sporočila.
3. Ponovite za vsako prizadeto mapo.
4. Sprožite **Full Resync** računa z strani Account Details v Mail Archiverju. Celotna ponovna sinhronizacija je potrebna, ker inkrementalne sinhronizacije pridobijo le sporočila, ki so novejša od zadnjega datuma sinhronizacije računa — sporočila v mapah, ki nikoli niso bile odkrite, bi sicer ostala nearhivirana.


*Ta vodič velja za leto 2026. Microsoft redno posodablja svoje storitve in vmesnike, zato bodo nekateri koraki lahko drugačni. Za najnovejše podrobnosti si oglejte [Microsoft identity platform documentation](https://learn.microsoft.com/en-us/entra/identity-platform/).*
