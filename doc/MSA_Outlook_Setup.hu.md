# ☁️ Personal Microsoft Account (Outlook.com / M365 Family) Setup {#personal-microsoft-account-outlookcom-m365-family-setup}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Ez az útmutató elmagyarázza, hogyan lehet e-maileket archiválni egy **személyes Microsoft fiókból** (Outlook.com, Hotmail, live.com vagy Microsoft 365 Family) a Mail Archiverben.

A szervezeti Microsoft 365 fiókokkal ellentétben (amelyek a client-credentials folyamatát használják — lásd: [Azure App Registration for M365](AZURE_APP_REGISTRATION_M365.md)), a személyes fiókok az **OAuth2 Device Code Flow (RFC 8628)** módszert használják. A Microsoft megvonta a Basic Auth és az alkalmazásjelszavarak (app passwords) használatát a személyes fiókok esetében, ezért OAuth2 szükséges.

A Mail Archiver egy **előregistrált közös Client ID**-val érkezik, így a legtöbb felhasználónak **nem** kell saját Azure App Registration-t létrehoznia. A haladó felhasználók továbbra is regisztrálhatják saját alkalmazásukat (lásd az alábbi B opciót).

## 📚 Tartalomjegyzék {#table-of-contents}

1. [A opció: Standard beállítás (Ajánlott)](#option-a-standard-setup-recommended)
2. [B opció: Egyedi alkalmazásregisztráció (Haladó felhasználók)](#option-b-custom-app-registration-power-users)
3. [Token frissítés és visszavonás](#token-refresh--revocation)
4. [Fontos megjegyzések](#important-notes)
5. [Hibaelégítés](#troubleshooting)

---

## 🚀 A opció: Standard beállítás (Ajánlott) {#option-a-standard-setup-recommended}

Ezt az opciót akkor használja, ha a Mail Archiver példányában alapértelmezett Client ID van konfigurálva (ez az alapértelmezett az hivatalos Docker imágoknál). Azure Portal hozzáférés nem szükséges.

### 🛠️ Előfeltételek {#prerequisites}

- Egy személyes Microsoft fiók (Outlook.com, live.com vagy M365 Family)
- Egy böngészővel rendelkező eszköz (okostelefon, laptop vagy táblagép) az engedélyezés befejezéséhez

### 📝 Lépések {#steps}

1. Jelentkezzen be a Mail Archiver alkalmazásba.
2. Navigáljon a **Mail Accounts** > **Create** menüpontra.
3. Töltse ki az alábbi mezőket:
   - **Name**: Leíró név a fiókhoz (pl. "Personal Outlook")
   - **Email Address**: A személyes Microsoft e-mail címe (pl. `you@outlook.com`)
   - **Provider**: Válassza a **Microsoft Personal** lehetőséget
4. A Client ID vagy Client Secret mezők nem jelennek meg — a Mail Archiver automatikusan használja a beépített alapértelmezett regisztrációt.
5. Kattintson a **Create** gombra.
6. Az eszközkód-engedélyezési oldal automatikusan megnyílik. Egy rövid kódot (pl. `ABCD-EFGH`) jelenít meg egy `https://microsoft.com/devicelogin` linkkel együtt (vagy a megjelenített ellenőrző URL-rel).
7. Bármilyen böngészővel rendelkező **eszközön** nyissa meg azt az URL-t, és jelentkezzen be a személyes Microsoft fiókjával.
8. Adja meg a 6. lépésben kapott kódot.
9. Megjelenik egy hozzájárulási képernyő, amely azt jelzi, hogy a **"Mail-Archiver"** IMAP hozzáférést kér. Ellenőrizze és hagyja jóvá.
10. A Mail Archiver oldal automatikusan felismeri a sikert, és az fiók szerkesztési nézetére irányít át.
11. Kész — az szinkronizálás az обліadatok beállításai szerint automatikusan elindul.

> ⚠️ **Nem ellenőrzött kiadó figyelmeztetés**: A hozzájárulási képernyőn megjelenhet egy figyelmeztetés, miszerint a "Mail-Archiver" egy nem ellenőrzött kiadó. Ez várható — a kiadói ellenőrzéshez vállalati azonosításra (Microsoft Partner Network fiók) van szükség, de ez nem szükséges a folyamat működéséhez. Az alkalmazás e nélkül is normálisan működik.

---

## 🔧 B opció: Egyedi alkalmazásregisztráció (Haladó felhasználók) {#option-b-custom-app-registration-power-users}

Ezt az opciót akkor használja, ha nem szeretné a közös alapértelmezett Client ID-t használni (pl. megfelelési okokból, vagy ha teljes ellenőrzést szeretne a regisztráció felett).

### 🛠️ Előfeltételek {#prerequisites}

- Egy Microsoft fiók (személyes vagy munka/iskola), amelyhez hozzáférése van az Azure Portalhoz
- Alapvető ismeretek az Azure/Entra portálról

### 🚀 Alkalmazásregisztráció létrehozása {#create-app-registration}

1. Navigáljon a [Microsoft Entra Admin Center](https://entra.microsoft.com) vagy az [Azure Portal](https://portal.azure.com) felületre.
2. Jelentkezzen be a Microsoft fiókjával.
3. A bal oldali navigációs paneleken válassza az **App registrations** lehetőséget (az Identity > Applications alatt).
4. Kattintson a **+ New registration** gombra.
5. Adja meg az alábbi adatokat:
   - **Name**: Adjon meg egy leíró nevet (pl. "Mail Archiver MSA")
   - **Supported account types**: Válassza a **Accounts in any organizational directory and personal Microsoft accounts** lehetőséget
     - Ez a multi-tenant opció, amely a személyes és a szervezeti fiókokat is támogatja.
   - **Redirect URI**: Hagyja üresen (nem szükséges a Device Code Flow esetén).
6. Kattintson a **Register** gombra.
7. Jegyezze fel az **Application (client) ID**-t az **Overview** oldalról.

### 🔐 Public Client Flow engedélyezése {#enable-public-client-flows}

1. Az alkalmazásregisztrációban navigáljon az **Authentication** menüpontra a bal oldali menüben.
2. Az oldal alján keresse meg az **Allow public client flows** opciót, és állítsa **Yes** értékre.
3. Kattintson a **Save** gombra.

> ℹ️ Nem szükséges client secret, redirect URI vagy API jogosultságok manuális konfigurálása — a Device Code Flow delegált jogosultságokat használ, amelyeket a futás közben a `IMAP.AccessAsUser.All` scope kéri meg.

### 📧 Mail Archiver konfigurálása {#configure-mail-archiver}

Felülírja a globális alapértelmezett beállítást (ez minden MSA fiókot érint):

Állítsa be a Client ID-t a környezeti változóban:


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 Token frissítés és visszavonás {#token-refresh-revocation}

### Automatikus token frissítés {#automatic-token-refresh}

Az első engedélyezés után a Mail Archiver receives:
- Egy **access token**-t (rövid élettartamú, ~1 óra)
- Egy **refresh token**-t (hosszú élettartamú, amely új access tokeneket szerez automatikusan)

A Mail Archiver minden szinkron futtatás előtt automatikusan frissíti az access tokenet. Nem kell újra engedélyeznie, hacsak a refresh token nem jár elfogytá, vagy nem vonják vissz.

### Hozzáférés visszavonása {#revoking-access}

A Mail Archiver személyes Microsoft fiókjához való hozzáférésének visszavonásához:

1. Látogasson el a [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage) oldalra.
2. Jelentkezzen be a személyes Microsoft fiókjával.
3. Keresse meg a listában a **"Mail-Archiver"** (vagy az Ön által megadott alkalmazásnév) elemet.
4. Kattintson a **Remove** gombra a hozzáférés visszavonásához.

A visszavonás után a Mail Archiver nem tud hozzáférni a fiókhoz, amíg újra nem engedélyezi.

---

## ⚠️ Fontos megjegyzések {#important-notes}
- **Közös Client ID**: A gyári Client ID használatakor minden Mail Archiver felhasználó minden példányban ugyanazt az Azure App Registration-t használja. A Mail Archiver fenntartója felelős a regisztráció aktív állapotban tartásáért. Ha a Microsoft letiltja a közös regisztrációt, minden felhasználónak át kell váltania egy egyedi regisztrációra (B opció), amíg egy új kiadás nem tartalmaz frissített Client ID-t.


- **Szervezeti fiókok**: A Microsoft Personal provider a `/common` authority endpointet használja, és támogatja a személyes valamint a szervezeti (munka/iskola) fiókokat is. Szervezeti fiókok esetén, amelyek client-credentials hozzáférést igényelnek (csak alkalmazás, nincs felhasználói bejelentkezés), használja helyette a **M365** providert.

---

## 🔍 Hibaelégítés {#troubleshooting}

### A mappák láthatóak outlook.com-on, de hiányoznak a Mail Archiverben {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**Jelenség**: Bizonyos mappák léteznek a postafiókban (láthatóak outlook.com-on vagy Outlookban), de sosem jelennek meg a Mail Archiverben — sem a fiókbeállítások mappalista oldalán, sem a szinkron során. Nem történik hibaírás a naplóba.

**Ok (szerver oldali)**: A mappák, amelyeket **harmadik féltől származó kliensek EWS prostředén** hoztak létre — ami úgy állítja be az outlook.com fiókokat, hogy azok Exchange fiókként, nem pedig IMAP fiókként működjenek — hibás `PR_CONTAINER_CLASS` tulajdonsággal rendelkezhetnek (pl. `IPF.Imap` a `IPF.Note` helyett). Ekkor az Outlook.com **teljesen kiszűri ezeket a mappákat az IMAP `LIST` válaszból**, annak ellenére, hogy a webes felületen teljesen láthatóak. Ez a Microsoft oldali működés, nem a Mail Archiver hibája — ugyanezt a jelenséget dokumentálták a Microsoft Graph API esetében is, ahol a nem `IPF.Note` konténerosztályú mappák hiányoznak az `mailFolders` listázásokból, amíg a tulajdonság nem kerül korrekcióra:

A Mail Archiver három forrásból lekérdezi a mappalistát (reziurzió `LIST`, minden mappa gyermeklapjainak szintenkénti átlása, és `LSUB`), majd összevonja az eredményeket. Ha a szerver egyik forrásból sem jelzi a mappát, nincs módja semmilyen IMAP kliensnek, hogy lássa azt.

**Megoldás**: Érintse meg az érintett mappákat egyszer a outlook.com webes felületén, hogy a Microsoft korrigálja a konténerosztályt:

1. Nyissa meg a <https://outlook.com> oldalt, és keresse meg a hiányzó mappát.
2. Nevezze át (bármilyen ideiglenes névvel), majd nevezze vissza az eredeti nevére — vagy hozzon létre egy új mappát, és mozgalja át oda az e-maileket.
3. Ismételje minden érintett mappával.
4. Indítson el egy **Full Resync** folyamatot a Mail Archiver Account Details oldaláról. A teljes újraszinkronizálás szükséges, mert az incrementális szinkronizálás csak az utolsó szinkron dátumánál újabb e-maileket tölti le — különben az olyan mappákban lévő e-mailek, amelyeket sosem fedeztek fel, archiválatlanul maradnának.


*Ez az útmutató 2026-os állapot mukaan készült. A Microsoft rendszeresen frissíti szolgáltatásait és felhasználói felületét, így egyes lépések eltérhetnek. A legfrissebb részletekért tekintse át a [Microsoft identity platform documentation](https://learn.microsoft.com/en-us/entra/identity-platform/) dokumentációt.*
