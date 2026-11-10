# Microsoft 365 Tenant Mailbox Import {#microsoft-365-tenant-mailbox-import}

[← Vissza a dokumentációs indexhez](index.md)

## Overview {#overview}

A Mail Archiver egyetlen létrehozási űrlapból több mailboxhoz is létrehozhat Microsoft 365 levelezési fiókokat ugyanabban a tenantben. Ez hasznos ügyfelek vagy szervezetek felonboardingolásakor, ahol az összes, vagy egy kiválasztott részszakasz Microsoft 365 mailboxok archiválása szükséges.

A tenant importálás a űrlapban konfigurált Microsoft Graph alkalmazás-hitelesítő adatokat használja, és minden importált mailboxhoz egy Mail Archiver levelezési fiókot hoz létre.

## Prerequisites {#prerequisites}

A tenant importálás használata előtt végezze el az [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md) dokumentációban leírt Microsoft 365 alkalmazásregisztrációs beállítást.

A következő értékeket kell lekérnie a Microsoft Entra ID-ból:

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

Az alkalmazásregisztráció rendelkeznie kell a Microsoft Graph **application permissions** jogosultságokkal, amelyek lehetővé teszik a Mail Archiver számára a tenant mailboxok olvasását. A normál M365 fiók archiválásához szükséges jogosultságokon felül a tenant importáláshoz a következő jogosultság szükséges:

- **User.Read.All** – szükséges a tenant felhasználók és mailbox címeik listázásához.

A tenant importálásnál a szolgáltatóként a **Microsoft 365 (tenant)** opciót kell választani a létrehozási űrlapon.

### Filtering {#filtering}

A Tenant importálás és a Tenant Management automatikusan kizárja a következőket:
- **Guest accounts** (`userType == "Guest"`) — külső, meghívott felhasználók, akik nem rendelkeznek saját mailboxkal a tenantben.
- **Users without an Exchange Online license** — csak azok a felhasználók jelennek meg a listában, akik aktív Exchange szolgáltatási tervvel rendelkeznek a `assignedPlans` területen. Ez megakadályozza azoknak a fiókoknak az importálását, amelyek nem archiválhatók.

## Import Modes {#import-modes}

A tenant importálás két módot támogat:

### Import all listed mailboxes {#import-all-listed-mailboxes}

Ezt a módot akkor használja, ha a Microsoft Graph által visszaadott minden mailboxot Mail Archiver fiókként kell létrehozni.

- A Mail Archiver betölti a tenant mailbox listáját a Microsoft Graph-ból.
- A már meglévő M365 levelezési fiókokat átugrója.
- Ha a **Skip disabled mailboxes** be van kapcsolva, a letiltott felhasználók nem szerepelnek a mailbox listában.

### Import selected mailboxes only {#import-selected-mailboxes-only}

Ezt a módot akkor használja, ha csak bizonyos mailboxokat szeretne archiválni a tenantben.

- Először töltse be a tenant mailbox listáját.
- Kapcsolja ki az **Import all listed mailboxes** opciót.
- Válassza ki azokat a mailboxokat, amelyeket importálni szeret.
- Küldje be az űrlapot, hogy csak a kiválasztott mailboxokhoz hozzon létre fiókokat.

## Step-by-Step Usage {#step-by-step-usage}

1. Jelentkezzen be a Mail Archiverbe olyan felhasználóként, aki létre tud hozni levelezési fiókokat.
2. Nyissa meg a **Mail Accounts** > **Create** menüpontot.
3. Válassza a szolgáltatóként a **Microsoft 365 (tenant)** opciót.
4. Adja meg a Microsoft 365 hitelesítő adatokat:
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. Adja meg az **Account name** előnevet, amelyet a generált fiókokhoz kell használni.
6. Válassza ki, hogy a letiltott mailboxokat át kell-e ugrani.
7. Kattintson a **Load mailboxes** gombra a tenant mailboxok listázásához.
8. Válassza ki a kívánt importálási módot:
   - Hagyja bekapcsolva az **Import all listed mailboxes** opciót az összes listázott mailbox importálásához.
   - Kapcsolja ki, és válasszon egyedi mailboxokat, ha csak meghatározott mailboxokat szeret importálni.
9. Kattintson a **Save** gombra.

A létrehozást követően a Mail Archiver visszatér a levelezési fiókok listájához, és megmutatja, hány tenant levelezési fiókot importált, és hány meglévő fiókot ugrott át.

## Account Naming {#account-naming}

A tenant importálás során a Mail Archiver az **Account name** mezőbe beírt értéket használja előnévként minden létrehozott mailbox fiókhoz.

Példa:

- Beírt fióknév: `Test GmbH`
- Tenant mailbox: `mailbox@example.com`
- Létrehozott fióknév: `Test GmbH - <mailbox@example.com>`

Ez megkönnyíti azoknak a fiókoknak az azonosítását, amelyek ugyanahhoz a tenanthez vagy ügyfélhez tartoznak.

## Existing Accounts {#existing-accounts}

Ha egy mailbox már létezik Microsoft 365 fiókként a Mail Archiverben, a tenant importálás átugrója azt a duplikáció létrehozása helyett. A meglévő fiókok felismerése a mailbox e-mail címén és az M365 szolgáltatón alapul.

A mailbox lista azt is mutathatja, hogy egy mailbox már létezik-e az import elküldése előtt.

## Disabled Mailboxes {#disabled-mailboxes}

A **Skip disabled mailboxes** jelzőnégyzet szabályozza, hogy a letiltott tenant felhasználókat ki kell-e zárni a mailbox listából.

- Bekapcsolva: csak a letiltott (aktív) tenant felhasználók jelennek meg a listában és kerülnek importálásra.
- Kikapcsolva: a letiltott tenant felhasználók is megjelenhetnek a listában, és kiválasztható módon importálhatók.

## Tenant Management {#tenant-management}

A létrehozási űrlapos történő tenant importálás mellett a Mail Archiver rendelkezik egy dedikált **Tenant Management** oldallal is a rendszergazdák számára. Ez a Mail Accounts indexről érhető el a bármely Microsoft 365 fiók mellett található **Tenant Management** gomb (épület ikon) segítségével.

A Tenant Management hasznos olyan mailboxok hozzáadásához, amelyek nem kerültek kezdetben importálásra — például amikor új mailboxok jönnek létre a tenantben az első onboarding után, vagy amikor a fiókok eredetileg le voltak ugráltak.

### How it differs from the create-form tenant import {#how-it-differs-from-the-create-form-tenant-import}

- A Microsoft 365 hitelesítő adatokat (Client ID, Client Secret, Tenant ID) **nem kell újra megadni**. Ezeket a gomb megnyomásakor használt forrásfiókból veszi fel.
- A tenant mailbox listája automatikusan betöltődik az oldal megnyitásakor (nincs "Load mailboxes" gomb).
- A már importált mailboxok "already exists" jelzéssel jelennek meg, és nem választhatóak újra.
- Csak a rendszergazdák férhetnek hozzá a Tenant Managementhez.

### Step-by-Step Usage {#step-by-step-usage}

1. Jelentkezzen be a Mail Archiverbe rendszergazdaként.
2. Nyissa meg a **Mail Accounts** menüpontot.
3. Keresse meg azt a Microsoft 365 fiókot, amelynek tenantjét kezelni szeretné, és kattintson mellette a **Tenant Management** gombra.
4. Az oldal betöltődik, és listázza a Microsoft Graph által visszaadott összes mailboxot a forrásfiók tenantjéhez.
   - A mailboxok, amelyek már léteznek M365 fiókként a Mail Archiverben, "already exists" jelzéssel vannak ellátva, és nem választhatóak ki.
   - A letiltott tenant felhasználók nem szerepelnek a listában; a Tenant Management mindig kihagyja a letiltott mailboxokat.
5. Válassza ki azokat a mailboxokat, amelyeket hozzá szeretne adni.
6. Adja meg az **Account name** előnevet, amelyet az új fiókokhoz kell használni (a nevezési séma `{prefix} - <{email}>`, azonos a létrehozási űrlapos tenant importálással).
7. Opcionálisan konfigurálja a **Delete After Days** és a **Local Retention Days** értékeket az új fiókokhoz.
8. Kattintson az **Add selected mailboxes** gombra.

A létrehozást követően a Mail Archiver visszatér a levelezési fiókok listájához, és megmutatja, hány mailbox lett hozzáadva.

### Searching mailboxes {#searching-mailboxes}

Nagyobb tenantek esetén, amelyek több ezer mailboxkal rendelkeznek, a mailbox lista feletti keresőmező lehetővé teszi a szűrést megjelenítési név vagy e-mail cím alapján. A szűrő nem érzékeny a kis- és nagybetűkre, és azonnal alkalmazódik gépelés közben. A **Select all** és a **Clear selection** funkciók csak a jelenleg látható (szűrt) mailboxokra vonatkoznak, így a szűrés biztonságos módja egy konkrét részszakasz kiválasztásának anélkül, hogy a rejtett bejegyzéseket érintené.

### Renaming existing accounts {#renaming-existing-accounts}

A **Rename existing accounts** jelzőnégyzet a `{prefix} - <{email}>` nevezési sémát alkalmazza minden olyan M365 fiókra, amely ugyanazt az alkalmazásregisztrációt (Client ID és Tenant ID) használja, mint a forrásfiók. Ez hasznos a fióknevek normalizálásához egy olyan kezdeti import után, ahol a nevek nem voltak összhangban.

Ha be van kapcsolva, a űrlap mentése átnevez minden megfelelő fiókot — beleértve a forrásfiókot is —, függetlenül attól, hogy új mailboxok kerültek-e kiválasztásra importáláshoz. Ezt az opciót önállóan is használhatja (új mailboxok kiválasztása nélkül) a nevek normalizálásához fiókok hozzáadása nélkül.

### Mailbox limit {#mailbox-limit}

Egyetlen Tenant Management művelet legfeljebb `TenantManagement__MaxSelectedMailboxes` mailboxot adhat hozzá egyszerre (alapértelmezett `1000`). Ez megvédi a véletlen tömeges importálástól és a túlzott Microsoft Graph / adatbázis terheléstől. Ha több mailboxot szeret importálni, futtassa a műveletet többször. Tekintse meg a [Setup Guide](Setup.md#-tenant-management-settings) dokumentációt a limit módosításához.

### Permissions required {#permissions-required}

A Tenant Management ugyanazokat a Microsoft Graph alkalmazásjogosultságokat használja, mint a létrehozási űrlapos tenant importálás. A forrásfiók alkalmazásregisztrációjának rendelkeznie kell a következőképpen:

- A normál M365 fiók archiválásához szükséges jogosultságokkal, **és**
- **User.Read.All** — szükséges a tenant felhasználók és mailbox címeik listázásához.

Tekintse meg az [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md) dokumentációt az elsődleges beállításhoz.

### Audit logging {#audit-logging}

Minden Tenant Management oldalmegtekintést és minden hozzáadási műveletet rögzít a hozzáférési napló, beleértve a listázott vagy hozzáadott mailboxok számát és a használt forrásfiókot.
