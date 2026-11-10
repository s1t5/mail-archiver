# 🔐 Two-Factor Authentication {#two-factor-authentication}

[← Naz nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Dvoslojna avtentikacija (2FA) doda drugi korak v postopek prijave: po vnosu
všečnice Mail Archiver zahteva **6-ovdigitno kodo**, ki jo generira
avtentikovalna aplikacija na vašem telefonu. Vsak pretvorjen geslo samo po
sebem ne bi bilo več dovolj za dostop do arhiva.

Mail Archiver izvaja 2FA z uporabo **TOTP** (Time-based One-Time Password, RFC
6238) — isto standard, ki ga uporabljajo Google Authenticator, Microsoft
Authenticator, Authy, 1Password, Bitwarden in drugi. Zunanji storitve niso
vključene; skrivni ključ nikoli ne zapusti vaše instance.

**2FA je opcijska funkcija za vsakega uporabnika.** Konfigurira jo vsak uporabnik
za svoj lastni račun, ne pa globalno z administratorjem.

## 🛠️ Predpogoji {#prerequisites}

- Avtentikovalna aplikacija s podporo za TOTP na vašem telefonu ali namiznem računalniku
- Dostop do vašega lastnega računa (morate biti prijavljeni)
- **Lokalni (geselni) račun** — glejte spodaj opombo o OIDC

> **⚠️ Ni na voljo za uporabnike OIDC.** Če se prijavite prek zunanjega
> ponudnika identitete (Entra ID, Authelia, Keycloak, …), 2FA upravlja ta
> ponudnik in stran za nastavitev je blokirana z sporočilom *"OIDC users cannot
> enable 2FA. Authentication is managed by your OIDC provider."* Namesto tega
> vklopite večfaktorno avtentikacijo pri svojem ponudniku identitete — glejte
> [OIDC / SSO](OIDC_Implementation.md).

## 🚀 Omogočanje dvoslojne avtentikacije {#enabling-two-factor-authentication}

### Korak 1 — Odprite stran za nastavitev {#step-1-open-the-setup-page}

1. Prijavite se v Mail Archiver
2. Odprite meni svojega računa in izberite **Two-Factor Authentication**
3. Preusmerjeni ste na **Two-Factor Setup**

### Korak 2 — Skenirajte QR kodo {#step-2-scan-the-qr-code}

Stran prikazuje QR kodo. Odprite svojo avtentikovalno aplikacijo, izberite *Add account* →
*Scan QR code* in usmerite kamero njo.

**Ne morete skenirati kode?** Namesto tega uporabite **Manual Entry**: stran prikazuje
skrivni ključ kot besedilo (`If you can't scan the QR code, enter this secret manually in
your authenticator app`). Vnesite ga v svojo aplikacijo kot časovno omejeno/TOTP račun.

> **ℹ️ Skrivni ključ še ni shranjen.** Shranjen je le za trajanje
> nastavitve v vaši brskalni bližini. 2FA postane aktivna šele po uspehu 3. koraka — če
> opustite nastavitev, se nič ne spremeni na vašem računu.

### Korak 3 — Potrdite s kodo {#step-3-confirm-with-a-code}

Vnesite 6-ovdigitno kodo, ki jo zdaj prikazuje vaša aplikacija, in kliknite **Enable Two-Factor
Authentication**.

Če je koda zavrnjena, se stran za nastavitev ponovno prikaže s novo QR kodo in
sporočilom *"Invalid authentication code. Please try again."* Preverite, ali je
ura na vašem telefonu nastavljena samodejno — TOTP je odvisen od natančnega časa.

### Korak 4 — Shranite svoje rezervne kode {#step-4-save-your-backup-codes}

Po uspehu ste preusmerjeni na stran **Backup Codes**. Prikazuje **10 kod**,
vsaka dolga 16 znakov.

> **⚠️ To je edino odliko, ko so kode prikazane.** Shranjene so
> v hash obliki — niti vi niti administrator ju ne moreta pozneje znova prikazati.
> Uporabite **Download Backup Codes**, da jih shranite v datoteko, ali pa jih kopirajte
> v svoj program za upravljanje gesel, preden zaprete stran.

Prenos vsebuje ta opomnik:

> *Mail Archiver - Two-Factor Authentication Backup Codes. Store these backup
> codes in a safe place. You can use them to log in if you lose access to your
> authenticator app. Each backup code can only be used once. After using a
> code, it will be invalidated.*
> *Important: Keep these codes secure and do not share them with anyone.*

## 🔑 Prijava z 2FA {#logging-in-with-2fa}

Po vnosu gesla ste preusmerjeni na **Two-Factor Verification** in
zahtevate kodo:

> *Enter a 6-digit TOTP code from your authenticator app or a 16-character
> backup code*

Obe delujeta v istem polju — Mail Archiver ju loči po dolžini:

| Vnos | Dolžina | Obnašanje |
|---|---|---|
| Koda iz vaše avtentikovalne aplikacije | 6 številk | Preverja se glede na trenutno časovno okno |
| Rezervna koda | 16 znakov | Preverja se glede na vaše shranjene kode, nato pa je **neveljavna** |

Da bi sprejeli majhne razlike v času, se sprejmejo tudi kode iz neposredno prejšnjega in
naslednjega časovnega koraka.

**"Remember me"** se prenese iz obrazca za prijavo: če ste to obeležili, boste ostali
prijavljeni čez ponovni zagon brskalnika po opravljenem koraku 2FA. Izbira je
shranjena v sesiji na strani strežnika, medtem ko je korak 2FA v teku, zato preživi
le, če kodo vnesete v okviru **60-minutnega** časovnega omejitev neaktivnosti sesije.
Podrobnosti in omejitve (7-dnevna absolutna življenjska doba, brez učinka za uporabnike OIDC) so v
[User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).

### Uporaba rezervne kode {#using-a-backup-code}

1. V isto polje vnesite eno svojih shranjenih 16-znakovnih kod
2. Koda je sprejeta in **takoj neveljavna** — ni je mogoče ponovno uporabiti
3. Uspešne prijave z rezervno kodo so zabeležene v dnevniku dostopa z
   opombo `(2FA Backup Code)`, da lahko kasneje opazite необыčajno uporabo

### Omejitev števila poskusov (Rate limiting) {#rate-limiting}

Da bi upočasnili poskuse brute-force napadov, je preverjanje 2FA omejeno na **5 poskusov
na 15 minut**, pri čemer se štejejo na IP naslov in uporabniško ime. Po tem vidite:

> **Too Many Attempts** — *You have made too many 2FA verification attempts.
> Please wait and try again later.*

Počakajte časovno okno in poskusite znova. Omejitev velja za neuspešne *in* uspešne
poskuse znotraj okna.

## 🚫 Izklop dvoslojne avtentikacije {#disabling-two-factor-authentication}

1. Odprite **Two-Factor Authentication** v meniju svojega računa
2. Stran zahteva potrditev: *"Are you sure you want to disable
   two-factor authentication? This will reduce the security of your account."*
3. Vnesite svojo **geslo** — ta korak zahteva vaše trenutno geslo, ne pa
   2FA kodo
4. Kliknite **Disable Two-Factor Authentication**

Izklop odstrani skrivni ključ in neveljavno vse preostale rezervne kode. Vaša
naslednja prijava bo zahtevala le geslo.

## 🔄 Ste izgubili avtentikator? Dve poti nazaj {#lost-your-authenticator-two-ways-back-in}

### Če še imate rezervne kode {#if-you-still-have-backup-codes}

Prijavite se z rezervno kodo, kot je opisano zgoraj, nato pa onemogočite in ponovno omogočite 2FA
z vašo novo napravo. Ne pozabite shraniti novih rezervnih kod.

### Če nimate več rezervnih kod {#if-you-have-no-backup-codes-left}

**Administrator** mora ponastaviti 2FA za vaš račun:

1. Administrator odpre **Users**
2. Uredi uporabnika, ki je prijavljen
3. Izbere **Reset Two-Factor Authentication** in potrdi:
   *"Are you sure you want to reset two-factor authentication for this user?
   They will need to set it up again."*
4. Uporabnik se lahko nato prijavi le s svojim geslom in ponovno postavi 2FA

Ponastavitev izbriše skrivni ključ in rezervne kode. Potrditveno sporočilo
*"Two-factor authentication has been successfully reset for user …"* se pojavi v
seznamu Users.

> **ℹ️ Obseg ponastavitve.** Ponastavitev velja le za 2FA. To **ni**
> ponastavitev gesla — glejte [Emergency Account Recovery](EmergencyAccountRecovery.md), če je
> težava v geslu ali če je račun popolnoma blokiran.

## 👁️ Kaj lahko vidijo administratorji {#what-administrators-can-see}

Pregled **Users** prikazuje stolpec 2FA:

- *"Two-factor authentication is currently enabled for this user."* — 2FA je aktivno
- V nasprotnem primeru je 2FA za uporabnika izklopljeno

Administratorji lahko ponastavijo 2FA (zgoraj), vendar **ne morejo** prebrati skrivnega ključa ali
rezervnih kod — obe sta shranjeni v hash obliki. Uporabnik, ki izgubi svojo napravo brez
rezervnih kod, nima druge možnosti za vrnitev kot administratorjeva ponastavitev.

## 🔍 Revizijski слід (Audit Trail) {#audit-trail}

Dejavnost 2FA se piše v dnevnik in, za uspešne prijave, v dnevnik dostopa:

| Dogodek | Kje | Vpis |
|---|---|---|
| Prijava prek avtentikovalne aplikacije | Dnevnik dostopa | `Login` z opombo `IP: <address> (2FA)` |
| Prijava prek rezervne kode | Dnevnik dostopa | `Login` z opombo `IP: <address> (2FA Backup Code)` |
| Neuspešen poskus 2FA | Dnevnik aplikacije | Opozorilo z uporabniškim imenom, IP in maskiranim tokenom |
| Neveljavna rezervna koda | Dnevnik aplikacije | Opozorilo z uporabniškim imenom in IP |

Vpis v dnevnik dostopa omogoča vidnost uporabe rezervnih kod naknadno — uporabno za
odkritje, če kodo uporabi nekdo, ki je je ne bi smel imeti. Za informacije, kako brati te
vpiše, glejte [Access Logging](Logs.md).

## ⚙️ Kako to deluje "pod motornjem" {#how-it-works-under-the-hood}

Za administratorje in revizorje:

- **Standard**: TOTP (RFC 6238), 20-ovni naključni skrivni ključ, kodiran v base32
- **Časovno okno preverjanja**: ±1 časovni korak, da se zamajanje ure tolerira
- **Rezervne kode**: 10 kod, vsaka 16 znakov, generirane iz kriptografskega
  RNG, shranjene v **hash** obliki, neveljavne posamezno ob uporabi
- **Shranjevanje skrivnega ključa**: v zapisu uporabnika; nikoli ne vrne v brskalnik po nastavitvi
- **Obdelava sesije**: sesija se regenerira, preden je identiteta 2FA
  shranjena, zato se piškot sesije, nameščen pred prijavo, ne more prenesti v
  avtentikirano sesijo
- **OIDC računi**: upravljanje 2FA je blokirano; ponudnik je avtoritativni
- **API ključi**: po zasnovi obidejo 2FA, saj interaktivni tokeni ne morejo odgovoriti na
  izziv — glejte varnostno utemeljitev v [REST API guide](API.md)

## ❓ Reševanje težav {#troubleshooting}

| Simptom | Vzrok | Rešitev |
|---|---|---|
| *"Invalid authentication code"* | Ura na telefonu ni pravilna ali napačen račun v aplikaciji | Vklopite samodejno sinhronizacijo časa; preverite, ali aplikacija prikazuje vnos za Mail Archiver |
| *"Two-factor secret is missing. Please restart the setup process."* | Stran za nastavitev je bila predolgo odprta, sesija je zastarela | Ponovno začnite nastavitev iz menija računa |
| *"Your session has expired. Please log in again."* | Preveč časa je minilo na strani za preverjanje | Ponovno se prijavite |
| *"Two-factor authentication is not enabled for this account."* | 2FA je bila ponastavljena ali nikoli dokončana | Ponovno jo nastavite ali vprašajte administratorja |
| Rezervna koda zavrnjena | Koda je že uporabljena ali je bila napačno prebrana | Rezervne kode delujejo **enkrat**; uporabite drugo ali zahtevajte ponastavitev 2FA |
| *"Too Many Attempts"* | Več kot 5 poskusov preverjanja v 15 minutah | Počakajte časovno okno |
| Ni strani za nastavitev v meniju računa | Prijavljate se prek OIDC | Vklopite MFA pri svojem ponudniku identitete |
| Ni stolpca 2FA v seznamu Users | Niste administrator | Vprašajte administratorja |

## 📚 Sorodna dokumentacija {#related-documentation}

- [User Management and Mailbox Permissions](UserManagement.md) — ustvarjanje računov, admin pravice
- [OIDC / SSO Authentication](OIDC_Implementation.md) — prijava prek zunanjih ponudnikov
- [Emergency Account Recovery](EmergencyAccountRecovery.md) — blokirani računi
- [Access Logging](Logs.md) — branje revizijskega sleda
- [REST API](API.md) — zakaj API ključi obidejo 2FA
