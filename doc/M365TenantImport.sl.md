# Uvozdenje poštovnega predala Microsoft 365 Tenant {#microsoft-365-tenant-mailbox-import}

[← Nazaj na indeks dokumentacije](index.md)

## Pregled {#overview}

Mail Archiver lahko s poljem za ustvarjanje ustvari Microsoft 365 poštovna računa za več kot en poštovni predal v istem tenantu. To je uporabno pri uvajanju stranke ali organizacije, kjer je treba arhivirati vse ali izbran podobet Microsoft 365 poštovnih predalov.

Uvoz tenanta uporablja Microsoft Graph kredenciale aplikacije, konfigurirane v obrazcu, in ustvari en Mail Archiver poštovni račun za vsak uvozen poštovni predal.

## Predpogoji {#prerequisites}

Pred uporabo uvoza tenanta izvedite nastavitev registracije aplikacije Microsoft 365, opisano v [Priročniku za registracijo aplikacije Azure in politiko zadrževanja](AZURE_APP_REGISTRATION_M365.md).

Za te vrednosti potrebujete podatke iz Microsoft Entra ID:

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

Registracija aplikacije mora imeti **dovoljenja aplikacije** Microsoft Graph, ki omogočajo Mail Archiver branje poštovnih predal tenanta. Poleg dovoljenj, potrebnih za običajno arhiviranje M365 računov, je za uvoz tenanta zahtevano naslednje dovoljenje:

- **User.Read.All** – zahtevano za izpis uporabnikov tenanta in njihovih naslovov poštovnih predal.

Pri ustvarjanju je uvoz tenanta izbran kot ponudnik **Microsoft 365 (tenant)**.

### Filtriranje {#filtering}

Uvoz tenanta in upravljanje tenanta (Tenant Management) samodejno izključita:
- **Goste račune** (`userType == "Guest"`) — zunanje povano uporabnike brez lastnih poštovnih predalov v tenantu.
- **Uporabnike brez licence Exchange Online** — na seznamu so izlistani le uporabniki z aktivno storitvijo Exchange v svojem `assignedPlans`. To preprečuje uvoz računov, ki jih ni mogoče arhivirati.

## Načini uvoza {#import-modes}

Uvoz tenanta podpira dva načina:

### Uvozi vse izlistane poštovne predale {#import-all-listed-mailboxes}

Uporabite ta način, če želite, da se vsak poštovni predal, ki ga vrne Microsoft Graph, ustvari kot račun Mail Archiver.

- Mail Archiver naloži seznam poštovnih predal tenanta iz Microsoft Graph.
- Že obstoječi M365 poštovni računi so preskočeni.
- Če je omogočeno **Skip disabled mailboxes**, onemogočeni uporabniki niso vključeni v seznam poštovnih predal.

### Uvozi samo izbrane poštovne predale {#import-selected-mailboxes-only}

Uporabite ta način, če želite arhivirati le določene poštovne predale v tenantu.

- Najprej naložite seznam poštovnih predal tenanta.
- onemogočite **Import all listed mailboxes**.
- Izberite poštovne predale, ki jih želite uvesti.
- Oddajte obrazec za ustvarjanje računov le za izbrane poštovne predale.

## Koraki uporabe {#step-by-step-usage}

1. Prijavite se v Mail Archiver kot uporabnik, ki lahko ustvarja poštovne račune.
2. Odprite **Mail Accounts** > **Create**.
3. Kot ponudnik izberite **Microsoft 365 (tenant)**.
4. Vnesite kredenciale Microsoft 365:
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. Vnesite prefikse **Account name**, ki se bodo uporabili za ustvarjene račune.
6. Izberite, ali se bodo preskočili onemogočeni poštovni predali.
7. Kliknite **Load mailboxes**, da izlistate poštovne predale tenanta.
8. Izberite želeno vedenje uvoza:
   - Obtegnite **Import all listed mailboxes**, če želite uvesti vse izlistane poštovne predale.
   - Onemogočite to možnost in izberite posamezne poštovne predale, če želite uvesti le določene.
9. Kliknite **Save**.

Po ustvarjanju se Mail Archiver vrne na seznam poštovnih računov in prikaže, koliko računov tenanta je bilo uvoženih in koliko obstoječih računov je bilo preskočenih.

## Imenovanje računov {#account-naming}

Pri uvozu tenanta uporablja Mail Archiver vrednost, vneseno v **Account name**, kot prefik za vsak ustvarjen račun poštovnega predala.

Primer:

- Vneseno ime računa: `Test GmbH`
- Poštovni predal tenanta: `mailbox@example.com`
- Ustvarjeno ime računa: `Test GmbH - <mailbox@example.com>`

To olajša identifikacijo uvoženih računov, ki pripadajo istemu tenantu ali stranki.

## Obstoječi računi {#existing-accounts}

Če poštovni predal že obstaja kot Microsoft 365 račun v Mail Archiver, uvoz tenanta ga preskoči namesto ustvarjanja podvoja. Zaznavanje obstoječih računov temelji na e-poštnem naslovu poštovnega predala in ponudniku M365.

Seznam poštovnih predal lahko tudi pokaže, ali poštovni predal že obstaja, še preden oddate uvoz.

## Onemočeni poštovni predali {#disabled-mailboxes}

V polju za izbiro **Skip disabled mailboxes** nadzorujete, ali morajo biti onemogočeni uporabniki tenanta izključeni iz seznama poštovnih predal.

- Omogočeno: na seznamu in v uvozu so samo aktivni uporabniki tenanta.
- Onemočeno: onemogoženi uporabniki lahko pojavijo se na seznamu in jih je mogoče uvesti, če so izbrani ali če se uvozi celoten seznam.

## Upravljanje tenanta (Tenant Management) {#tenant-management}

Poleg uvoza tenanta prek obrazca za ustvarjanje Mail Archiver ponuja administratorjem namenski zavihek **Tenant Management**. Do njega lahko dostopate iz indeksa Mail Accounts preko gumba **Tenant Management** (ikona zgradbe) zraven katerega koli Microsoft 365 računa.

Upravljanje tenanta je uporabno za dodajanje poštovnih predal, ki niso bili sprva uvozeni — na primer, ko so po začetnem uvajanju v tenantu ustvarjeni novi poštovni predali, ali ko so bili računi sprva preskočeni.

### Kako se razlikuje od uvoza tenanta prek obrazca za ustvarjanje {#how-it-differs-from-the-create-form-tenant-import}

- Kredenciale Microsoft 365 (Client ID, Client Secret, Tenant ID) **ni treba ponovno vnašati**. Prevzamemo jih iz izvornega računa, na katerem je bil klikni gumb.
- Seznam poštovnih predal tenanta se naloži samodejno ob odpiru strani (ni gumba "Load mailboxes").
- Že uvoženi poštovni predali so prikazani z oznako "already exists" in jih ni mogoče ponovno izbrati.
- Običajni uporabniki ne lahko dostopajo do upravljanja tenanta; dostop imajo samo administratorji.

### Koraki uporabe {#step-by-step-usage}

1. Prijavite se v Mail Archiver kot administrator.
2. Odprite **Mail Accounts**.
3. Poiščite Microsoft 365 račun, kat whom želite upravljati tenant, in kliknite gumb **Tenant Management** zraven njega.
3. Stran se naloži in izlista vse poštovne predale, ki jih je Microsoft Graph vrnil za tenant izvornega računa.
   - Poštovni predali, ki že obstajajo kot M365 računi v Mail Archiver, so označeni z oznako "already exists" in jih ni mogoče izbrati.
   - Onemočeni uporabniki tenanta niso na seznamu; upravljanje tenanta vedno preskoči onemogočene poštovne predale.
4. Izberite poštovne predale, ki jih želite dodati.
5. Vnesite prefikse **Account name**, ki se bodo uporabili za nove račune (način imenovanja je `{prefix} - <{email}>`, identičen kot pri uvozu tenanta prek obrazca za ustvarjanje).
6. Po želji nastavite **Delete After Days** in **Local Retention Days** za nove račune.
7. Kliknite **Add selected mailboxes**.

Po ustvarjanju se Mail Archiver vrne na seznam poštovnih računov in prikaže, koliko poštovnih predal je bilo dodanih.

### Iskanje poštovnih predal {#searching-mailboxes}

Za velike trate z več tisoč poštovnimi predali vam polje za iskanje nad seznamom omogoča filtriranje po imenu ali e-poštnem naslovu. Filter je neobazljiv in se uporabi takoj, ko tipkate. Funkciji **Select all** in **Clear selection** operacija sta vezani le na trenutno vidne (filtrirane) poštovne predale, zato je filtriranje varen način za izbiro določenega podvojeta brez vpliva na skrite v entries.

### Preimenovanje obstoječih računov {#renaming-existing-accounts}

Polje za izbiro **Rename existing accounts** uporabi način imenovanja `{prefix} - <{email}>` na vse M3oč M365 račune, ki delijo isto registracijo aplikacije (Client ID in Tenant ID) kot izvorni račun. To je uporabno za normalizacijo imen računov po začetnem uvozu, ko imena niso bila dosledna.

Ko je polje označeno, ob shranstvu obrazca preimenuje vsak ustrezen račun — vključno z izvornim računom — ne glede na to, ali so novi poštovni predali izbrani za uvoz. To možnost lahko uporabite samo za normalizacijo imen brez dodajanja novih računov.

### Meja poštovnih predal {#mailbox-limit}

Ena operacija upravljanja tenanta lahko hkrati doda največ `TenantManagement__MaxSelectedMailboxes` poštovnih predal (default `1000`). To preprečuje slučajne masovne uvoze in preobremenitevje Microsoft Graph / baze podatkov. Če morate uvesti več kot toliko, operacijo ponovite večkisel. Več o tem, kako prilagoditi to mejo, najdete v [Navodjih za nastavitev](Setup.md#-tenant-management-settings).

### Zahtevana dovoljenja {#permissions-required}

Upravljanje tenanta uporablja ista dovoljenja aplikacije Microsoft Graph kot uvoz tenanta prek obrazca za ustvarjanje. Registracija aplikacije izvornega računa mora imeti:

- Dovoljenja, potrebna za običajno arhiviranje M365 računov, **in**
- **User.Read.All** — zahtevano za izpis uporabnikov tenanta in njihovih naslovov poštovnih predal.

Za začetno nastavitev poglejte [Priročnik za registracijo aplikacije Azure in politiko zadrževanja](AZURE_APP_REGISTRATION_M365.md).

### Zapisovanje revizijskih dnevnikov {#audit-logging}

Vsak ogled strani upravljanja tenanta in vsaka operacija dodajanja se zabeleži v dnevnik dostopa, vključno s številom izlistanih ali dodanih poštovnih predal in uporabljenim izvornim računom.
