# 👥 Felhasználói kezelés és postafiók jogosultságok {#user-management-and-mailbox-permissions}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Ez az útmutató részletes utasításokat tartalmaz az új felhasználói fiókok létrehozásához és a postafiók jogosultságok kiosztásához a Mail Archiver alkalmazásban. Emellett érinti a saját jelszó megváltoztatását, valamint a frissen telepített példány esetén kötelező jelszócsere folyamatát is.


## 🛠️ Előfeltételek {#prerequisites}

- Adminisztrációs hozzáférés a Mail Archiver alkalmazáshoz
- A rendszerben már konfigurált meglévő e-mail fiókok

## 🧑‍💻 Új felhasználói fiók létrehozása {#creating-a-new-user-account}

1. Lépjen be a Mail Archiver alkalmazásba olyan fiókkal, amely rendelkezik adminisztrációs jogossággal
2. Navigáljon a főmenüből a "Users" szekcióba
3. Kattintson a "Create New User" gombra
4. Töltse ki a szükséges felhasználói adatokat:
   - **Username**: Adjon meg egy egyedi felhasználónamot az új felhasználóhoz
   - **Password**: Adjon meg egy biztonságos jelszót a felhasználóhoz
   - **Email**: A felhasználó e-mail címe
   - **Admin**: Jelölje be ezt a négyzetet, ha a felhasználónak adminisztrációs jogai kell, hogy legyenek
   - **Self Manager**: Jelölje be ezt a négyzetet, ha a felhasználó képes kell, hogy kezelje saját e-mail fiókjait (szerkesztheti a meglévőket és hozzáadhat újakat, valamint törölheti a fiókokat is)
   - **active**: Be kell jelölve, hogy lehetővé tezze a felhasználó bejelentkezését
5. Kattintson a "Create User" gombra az új felhasználói fiók mentéséhez

## 🔐 Postafiók jogosultságok kiosztása a felhasználókhoz {#assigning-mailbox-permissions-to-users}

1. A felhasználó létrehozása után, vagy egy meglévő felhasználó szerkesztésekor, navigáljon a főmenüből a "Users" szəkcióba
2. Keresse meg a listában azt a felhasználót, akinek e-mail fiókokat szeretne kiosztani, és kattintson az adott felhasználó mellett a "Assign" gombra
5. Az "Assign Mail Accounts" oldalon minden elérhető e-mail fiókot láthat, valamint jelölőnégyzetek, amelyek jelzik, hogy a fiók hozzá van-e rendelve a felhasználóhoz
6. Egy új e-mail fiók hozzárendeléséhez jelölje be a megfelelő négyzetet a konkrét fiókhoz
7. Egy e-mail fiók eltávolításához a felhasználóról, vegye ki a jelölést a négyzetből
8. Kattintson a "Save Assignments" gombra a változtatások alkalmazásához

## 👤 Felhasználói fiók jogosultságok {#user-account-permissions}

### Admin felhasználók {#admin-users}
Az admin felhasználók teljes hozzáféréssel rendelkeznek az alkalmazáshoz és képesek:
- Minden felhasználói fiókot és azok jogosultságait kezelni
- Hozzáférni az összes e-mail fiókhoz a rendszerben
- Megtekinteni az összes hozzáférési naplót (access log)
- Elvégezni minden adminisztrációs feladatot

### Self Manager felhasználók {#self-manager-users}
A Self Manager felhasználók képesek:
- Új e-mail fiókok hozzáadására a saját fiókjukhoz
- A hozzájuk rendelt meglévő e-mail fiókok szerkesztésére
- A hozzájuk rendelt e-mail fiókok törlésére
- Hozzáférést és kezelést biztosítanak a hozzájuk rendelt e-mailekhez (beleértve a törlést is)
- Megtekinthetik a saját hozzáférési naplóikat

### Standard felhasználók {#standard-users}
A standard felhasználók korlátozott hozzáféréssel rendelkeznek és képesek:
- Megtekinteni a hozzárendelt e-mail fiókokból archivált e-maileket
- Keresni a hozzárendelt e-mail fiókaikban
- E-maileket exportálni a hozzárendelt fiókaikból
- E-maileket visszaállítani a hozzárendelt fiókaikból
- Hozzáférni a hozzárendelt fiókok csatrészeihez

## 🔑 A saját jelszó megváltoztatása {#changing-your-own-password}

Minden felhasználó, aki helyi jelszóval rendelkezik, megváltoztathatja azt adminisztrátor nélkül:

1. Kattintson a **felhasználónévre** a jobb felső sarokban a felhasználói menü megnyitásához.
2. Válassza a **Change Password** lehetőséget.
3. Adja meg a **jelenlegi jelszót**, majd a **novu jelszót** kétszer, majd küldje el.

A új jelszónak meg kell jsonObj a következő követelményeknek, és **eltérőnek kell lennie a jelenleginél**.

> ℹ️ **Az OIDC felhasználói itt nem változtathatják meg a jelszótukat.** A rendszer a következő választ adja:
> *"OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* Ehelyett az identitásmegelőzőnél (identity provider) változtassa meg a jelszót — lássd a [OIDC / SSO](OIDC_Implementation.md) részt.

### Jelszó követelmények {#password-requirements}

- legalább **10 karakter**
- legalább egy **nagybetű**
- legalább egy **kisbetű**
- legalább egy **szám**
- legalább egy **specialsimbol**

Az űrlap élő ellenőrzőlistaként mutatja ezeket, miközben gépel. Ugyanezek a szabályok érvényesek, amikor egy adminisztrátor felhasználót hoz létre vagy jelszót állít vissza.

## ⚠️ Kényszerített jelszócsere (Alapértelmezett beállítás) {#forced-password-change-initial-setup}

Egy frissen telepített példány az adminisztrátori hitelesítési adatokkal indul a konfigurációból (`Authentication__Username` / `Authentication__Password`). Amíg ez a gyári jelszó még érvényben van **és még nem létezik e-mail fiók**, az első bejelentkezés kényszerített jelszócsert vált ki:

- Egy figyelmeztető sáv magyarázza az okot: *"For security reasons, you must change your
  password before continuing. This is required because you are using the default
  system credentials on a newly set up system."*
- Nem tud elnavelgani az oldalról: *"You must change your password before
  you can continue using the system."*
- Az új jelszónak eltérőnek kell lennie a jelenleginél.
- Mentés után a jel törlődik, és a dashboardra kerül Ön.

Miután megváltoztatta a gyári jelszót, a kényszerített csere többé nem jelenik meg.

## 🕒 Bejelentkezve maradni — a "Remember Me" jelölőnégyzet {#staying-signed-in-the-remember-me-checkbox}

A bejelentkezési űrlapon található egy **Remember Me** feliratú jelölőnégyzet (*angemeldet bleiben* a német UI-ban). Ez pontosan egy dolgot tesz: meghatározza, hogy az autentikációs cookie egy munkamenet (session) cookie vagy egy tartós (persistent) cookie legyen.

| | Jelölőnégyzet ki van véve | Jelölőnégyzet be van jelölve |
|---|---|---|
| Cookie élettartam | `Authentication__SessionTimeoutMinutes` (alapértelmezett **60 perc**) | **7 nap** |
| Cookie típusa | Session cookie — törlődik, amikor a böngésző bezáródik | Persistent cookie — a lemezre mentve tárolódik |
| Böngésző újraindítása | Újra be kell jelentkeznie | Bejelentkezve marad |
| Idle timeout | Minden kérésnél meghosszabbodik (`SlidingExpiration`) | Minden kérésnél meghosszabbodik, a maximális 7 napig |
| Kijelentkezés | Az munkamenetet azonnal lezárja | Az munkemenetet azonnal lezárja |

Tehát a jelölőnégyzet a "jelenleg bejelentkezve ebben a böngésző munkamenetben, maximum 60 perc inaktivitás mellett" állapotot átalakítja "bejelentkezve akár 7 napig, beleértve a böngésző és a gép újraindítását" állapotra.

### Amiket a jelölőnégyzet nem tesz meg {#things-the-checkbox-does-not-do}

- **Nem emlékszik a jelszavára vagy a felhasználó nevére.** A bejelentkezési űrlap nem lesz előre kitöltve a következő látogatáskor; csak a munkamenet (session) cookie marad meg.
- **A 7 nap egy kemény korlát, és nem konfigurálható.** Az érték fix a kódban (`Auth/Services/CookieAuthenticationService.cs`). Csak a nem tartós élettartam követi a `Authentication__SessionTimeoutMinutes` értéket.
- **Nem egy inaktivitási időzítő.** A 7 nap a bejelentkezéstől számítódik; ha lejárt, újra be kell jelentkezni, még akkor is, ha egy perccel ezelőtt használta az archívumot.
- **Nem marad meg kijelentkezés után sem.** A *Logout* gomb megnyomásával a cookie törlődik, függetlenül a jelölőnégyzet állásától.

### Interakció a 2FA-val {#interaction-with-2fa}

A kétfaktoros hitelesítés engedélyezése esetén a jelölőnégyzet megmarad a 2FA lépés alatt, és csak a második faktor sikeres beemelése után alkalmazódik. A 2FA átadás a szerveroldali munkamenetben (session) történik, amelynek saját **60 perces inaktivitási timeoutja** van: ha a kódot ezen időszakon belül nem adják meg, a bejelentkezés megszakad, és a jelölőnégyzet kiválasztása is elveszik.

### Interakció az alkalmazás munkamenetével (session) {#interaction-with-the-application-session}

A Mail Archiver egy második, különálló szerveroldali munkamenetet tart fenn a rövid távú állapotokhoz — a 2FA átadáshoz, a tömeges visszaállítási kiválasztáshoz és az utolsó keresési állapothoz. Ennek a munkamenetnek fix **60 perces inaktivitási timeoutja** van, és *nem* tartós. A böngésző újraindítása után egy érvényes "Remember Me" cookie esetén be van jelentkezve, de bármely függőben lévő művelet, amely ebben a munkamenetben futott, újra indul.

### Interakció OIDC / SSO esetén {#interaction-with-oidc-sso}

Az olyan felhasználók, akik külső identitásmegelőzőn keresztül jelentkeznek be, soha nem kapnak tartós (persistent) cookie-t: az OIDC callback mindig a munkamenetet a `persistAuthentication: false` értékkel indítja. Ezeknél a fiókoknál a jelölőnégyzet **nem hatással van** — a munkamenet hosszát az identitásmegelőző munkamenet-beállításai határozzák meg.

> ⚠️ **Biztonsági megjegyzés.** Megosztott vagy felügyelet nélküli gépen hagyja a jelölőné négyzetet **ki sem jelölve** — egy tartós cookie akár 7 napig hozzáférhetővé teszi az archívumot jelszó nélkül. Olyan környezetekben, ahol ez elfogadhatatlan, rövidítse le a `Authentication__SessionTimeoutMinutes` értéket, és kérje fel a felhasználókat, hogy ne használják a jelölőné négyzetet, vagy használjon OIDC-t, ahol a szolgáltató saját munkamenet-szabályzatot alkalmaz.

## 🔒 Biztonsági szempontok {#security-considerations}

1. Erős jelszavakat használjon minden felhasználói fiókhoz
2. Az adminisztrátori jelszót azonnal változtassa meg a telepítés után
3. Csak olyan felhasználóknak kiostja az Admin jogosultságot, akiknek szüksége van rá
4. Csak olyan felhasználóknak kiostja a Self Manager jogosultságot, akiknek szükségük van a saját fióka kezelésére
5. Rendszeresen ellenőrizze a felhasználói jogosultságokat a hozzáférés megfelelő volta érdekében
6. Távolítsa el a felhasználói hozzáférést, ha már nincs rá szükség
7. Kövesse a legkisebb privilégium elveit — csak a szükséges e-mail fiókokhoz biztosítson hozzáférést
