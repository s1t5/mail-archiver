# 👥 Upravljanje uporabniki in dovoljenja za nabirnike pošte {#user-management-and-mailbox-permissions}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodič podaja podrobna navodila za ustvarjanje novih uporabniških računov in dodeljevanje pravic do nabirnikov pošte v aplikaciji Mail Archiver. Vključuje tudi postopek za spremembo lastne kotne besede in prisilno spremembo kotne besede ob prvem zagonu nove instance.

## 🛠️ Predposemni 조건 {#prerequisites}

- Administratorski dostop do aplikacije Mail Archiver
- Obstoječi e-poštni računi, konfigurirani v sistemu

## 🧑‍💻 Ustvarjanje novega uporabniškega računa {#creating-a-new-user-account}

1. Prijavite se v aplikacijo Mail Archiver z računom, ki ima administratorska privilegija
2. V glavnem meniju stopite na razdelek "Users"
3. Kliknite gumb "Create New User"
4. Izpolnite zahtevane podatke uporabnika:
   - **Username**: Vnesite edinstven uporabniški imen za novega uporabnika
   - **Password**: Vnesite varno kotno besedo za uporabnika
   - **Email**: E-poštni naslov uporabnika
   - **Admin**: Obrazite ta kotak, če mora imeti uporabnik administratorska privilegija
   - **Self Manager**: Obrazite ta kotak, če mora imeti uporabnik možnost upravljanja z lastnimi poštnimi računi (urejanje obstoječih in dodajanje novih, pa tudi brisanje računov)
   - **active**: Mora biti obnazeno, da bi bilo uporabniku dovoljeno prijavo
5. Kliknite "Create User", da shranite novi uporabniški račun

## 🔐 Dodeljevanje pravic do nabirnika pošte uporabnikom {#assigning-mailbox-permissions-to-users}

1. Po ustvarjanju uporabnika ali pri urejanju obstoječega uporabnika stopite v razdelek "Users" v glavnem meniju
2. Najdite uporabnika na seznamu, kateremu želite dodeliti poštne račune, in kliknite gvet "Assign" za tega uporabnika
3. Na strani "Assign Mail Accounts" boste videli vse razpoložljive poštne račune in okvire (checkbox), ki kažejo, ali so uporabniku dodeljeni
5. Za dodajanje novega e-poštnega računa obrazite ustrezen okvir za določeni račun
6. Za odstranitev e-poštnega računa z uporabnika odznačite okvir
7. Kliknite "Save Assignments", da uporabite spremembe

## 👤 Privilegiji uporabniškega računa {#user-account-permissions}

### Admin uporabniki {#admin-users}
Admin uporabniki imajo polen dostop do aplikacije in lahko:
- Upravljajo vse uporabniške račune in njihove privilegije
- Dostopajo do vseh e-poštnih računov v sistemu
- Pregledujejo vse loge dostopa
- Izvajajo vse administrativne naloge

### Self Manager uporabniki {#self-manager-users}
Self manager uporabniki lahko:
- Dodajo nove e-poštne račune v svoj lastni račun
- UrejaJo obstoječe e-poštne račune, ki so jim dodeljeni
- Brišejo e-poštne račune, ki so jim dodeljeni
- Dostopajo do e-pošte in jo upravljajo iz dodeljenih računov (vključno z brisanjem)
- Pregledujejo svoje lastne loge dostopa

### Standardni uporabniki {#standard-users}
Standardni uporabniki imajo omejen dostop in lahko:
- Pregledejajo arhivirano e-pošto iz dodeljenih e-poštnih računov
- Iskejo po e-pošti znotraj svojih dodeljenih računov
- Izvozejo e-pošto iz svojih dodeljenih računov
- Obnovijo e-pošto iz svojih dodeljenih računov
- Dostopajo do prilog iz dodeljenih računov

## 🔑 Sprememba vaše lastne kotne besede {#changing-your-own-password}

Vsak uporabnik z lokalno kotno besedo jo lahko spremeni brez administratorja:

1. Kliknite na svoje **ime uporabnika** v zgornjem desnem kotu, da odprete meni uporabnika.
2. Izberite **Change Password**.
3. Vnesite svojo **trenjojočasno besedo** in **novo kotno besedo** dvakrat, nato pa oddaljite zahtevo.

Nova kotna beseda mora izpolnjevati spodnje zahteve in mora biti **različna od trenutne**.

> ℹ️ **Uporabniki OIDC ne lahko spremenijo svoje kotne besede tukaj.** Stran odgovori z
> *"OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* Namesto tega spremenite kotno besedo pri svojem ponudniku identitete —
> poglejte [OIDC / SSO](OIDC_Implementation.md).

### Zahteve za kotno besedo {#password-requirements}

- vsaj **10 znakov**
- vsaj ena **velika črka**
- vsaj ena **mala črka**
- vsaj ena **številka**
- vsaj en **poseben znak**

Obraz prikazuje te zahteve kot seznam v živo med tipkanjem. Sloga velja tudi, ko administrator ustvari uporabnika ali ponastavi kotno besedo.

## ⚠️ Prisilna sprememba kotne besede (Začetna nastavitev) {#forced-password-change-initial-setup}

Čisto nameščena instanca se začne z administratornimi podatki iz konfiguracije (`Authentication__Username` / `Authentication__Password`). Dokler je ta privzeta kotna beseda še v uporabi **in še ne obstaja nobena poštna conta**, prva prijava zahteva spremembo:

- Opozorilni banner pojasnjuje razlog: *"For security reasons, you must change your
  password before continuing. This is required because you are using the default
  system credentials on a newly set up system."*
- Ne lahko odtepite z strani: *"You must change your password before
  you can continue using the system."*
- Nova kotna beseda mora biti drugačna od trenutne.
- Po shranjenju je zastavka izbrisanec in preusmerjeni ste na nadzorno ploščo.

Ko ste spremenili privzeto kotno besedo, se prisilna sprememba ne pojavi več.

## 🕒 Ostajanje pri prijavi — polje "Remember Me" {#staying-signed-in-the-remember-me-checkbox}

Pri prijavi je polje z oznako **Remember Me** (*angemeldet bleiben* v nemškem vmesniku). To počne le eno stvar: določi, ali je autentikacijski kleket (cookie) sesijski kleket ali trajen kleket.

| | Polje ni obnazeno | Polje je obnazeno |
|---|---|---|
| Trajanje kleketa | `Authentication__SessionTimeoutMinutes` (privzeto **60 minut**) | **7 dni** |
| Tip kleketa | Sesijski kleket — izbriše se ob zaprtju brskalnika | Trajen kleket — shranjen na disku |
| Ponovni zagon brskalnika | Znova se morate prijaviti | Ostajate prijavljeni |
| Neprekinjena uporaba (Idle timeout) | Podalša se ob vsaki zahtevi (`SlidingExpiration`) | Podalča se ob vsaki zahtevi, do absolutne omejitve 7 dni |
| Odjava | Takoj konča sesijo | Takoj konča sesijo |

Torej polje spremeni "prijavljen v tej sesiji brskalnika, največ 60 minut brez dejanja" v "prijavljen za do 7 dni, vključno s ponovnim zagonom brskalnika in stroja".

### Česar polje ne dela {#things-the-checkbox-does-not-do}

- **Ne pomni si vaše kotne besede ali uporabniškega imena.** Ob naslednjem obisku obraz za prijavo ni predpolnjen; ohranjen je le sesijski kleket.
- **7 dni je stroga omejitev in ni mogoče konfigurirati.** Vrednost je fiksna v kodi (`Auth/Services/CookieAuthenticationService.cs`). Le netrajen čas trajanja sledi `Authentication__SessionTimeoutMinutes`.
- **Ni časovnik neaktivnosti.** 7 dni se šteje od prijave; ko potečejo, je potrebna nova prijava, tudi če ste uporabili arhiv pred minuto nazaj.
- **Ne preživi odjave.** Klik na *Logout* izbriše kleket ne glede na polje.

### Interakcija z 2FA {#interaction-with-2fa}

Z vklopljenim dvojnim faktorjem avtentikacije se polje spomni med korakom 2FA in uporabi šele, ko uspe drugi faktor. Prenos 2FA poteka na strani strežnika, ki ima svoj **6j 60-minutni čas neaktivnosti**: če koda ni vnesena v tem oknu, je prijava prekinjena in izbira polja se izgubi.

### Interakcija z sesijo aplikacije {#interaction-with-the-application-session}

Mail Archiver ohranja drugo, ločeno sesijo na strani strežnika za kratkoročno stanje — prenos 2FA, izbiro za masovno obnovo in zadnje stanje iskanja. Ta sesija ima fiksno **60-minutno okno neaktivnosti** in *ni* trajna. Po ponovnem zagonu brskalnika z veljavnim kleketom "Remember Me" ste prijavljeni, vendar se kakršna koli prejšnja operacija, ki je bila v tej sesiji, začne znova.

### Interakacija z OIDC / SSO {#interaction-with-oidc-sso}

Uporabniki, ki se prijavijo preko zunanjega ponudnika identitete, nikoli ne prejestejo trajnega kleketa: OIDC povratni poziv vedno začne sesijo s `persistAuthentication: false`. Za te račune polje **ni brez učinka** — kako dolgo traja sesija, določa nastavitev sesije ponudnika identitete.

> ⚠️ ** Varnostna opomba.** Na skupnem ali neposredno dostopnem stroju pustite polje neobnazeno — trajen kleket ohranja arhiv dostopen do 7 dni brez kotne besede. V okoljih, kjer je to nesprejemljivo, skrajšajte `Authentication__SessionTimeoutMinutes` in zahtevajte od uporabnikov, naj ne uporabljajo polja, ali uporabite OIDC, kjer ponudnik utроне svojo politiko sesije.

## 🔒 Varnostni razoglasi {#security-considerations}

1. Za vse uporabniške račune uporabite močne kotne besede
2. Takoj po namestitvi spremenite privzeto administratorsko kotno besedo
3. Pravico Admin dodelite le tistim uporabnikom, ki jo potrebujejo
4. Pravico Self Manager dodelite le tistim uporabnikom, ki morajo upravljati svoje lastne račune
5. Redno preverjajte pravice uporabnikov, da zagotovite primeren dostop
6. Odstranite dostop uporabnika, ko več ni potreben
7. Uporabljajte načelo najmanjih privilegijev - dodelite dostop le do tistih e-poštnih računov, ki jih uporabniki potrebujejo
