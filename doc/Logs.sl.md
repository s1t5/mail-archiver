# 📋 Dostopni dnevnik {#access-log}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodič pojasnjuje funkcionalnost dostopnega dnevnika v aplikaciji Mail Archiver, ki omogoča beleženje in ogled uporabniških aktivnosti znotraj sistema. Ta funkcionalnost je ključna za namene spremljanja, varnosti in odpravljanja napak.

## 📊 Kaj se beleži {#what-is-logged}

Aplikacija beleži različne vrste uporabniških aktivnosti, vključno s:

1. **Login in Logout** - Beleženje operacij prijave in odjave
2. **Search Operations** - Beleženje zahtev za iskanje e-pošte znotraj sistema
3. **Email Opening** - Beleženje, ko uporabniki odprejo vsebino e-pošte
4. **Email Downloading** - Beleženje, ko uporabniki predownloadirajo e-pošto
5. **Email Restoration** - Beleženje, ko uporabniki obnovijo arhivirano e-pošto
6. **Account Management** - Beleženje dejanj v povezavi z računom
7. **Deletion Operations** - Beleženje dejanj brisanja
8. **Audit Data Export** - Beleženje izvajanja izvoza revizijskih podatkov (začetek in rezultat, poglejte [Audit Data Export](AuditExport.md))

## 📝 Vrste dejanj in opisi {#action-types-and-descriptions}

Zabeležena dejanja so razvrščena v naslednje vrste:

### Login {#login}
- **Opis**: Beleži, ko se uporabnik prijavi v sistem
- **Podrobnosti**: Username, timestamp

### Logout {#logout}
- **Opis**: Beleži, ko se uporabnik odjavi iz sistema
- **Podrobnosti**: Username, timestamp

### Search {#search}
- **Opis**: Beleži zahteve za iskanje znotraj e-poštnega sistema
- **Podrobnosti**: Username, timestamp, search parameters

### Open {#open}
- **Opis**: Beleži, ko uporabnik odpre e-pošto
- **Podrobnosti**: Username, timestamp, email ID, sender, subject

### Download {#download}
- **Opis**: Beleži, ko uporabniki predownloadirajo e-pošto
- **Podrobnosti**: Username, timestamp, email ID, sender, subject

### Restore {#restore}
- **Opis**: Beleži, ko uporabniki obnovijo arhivirano e-pošto
- **Podrobnosti**: Username, timestamp, email ID, sender, subject

### Account {#account}
- **Opis**: Beleži dejanja v povezavi z računom (ustvarjanje, sprememba itd.)
- **Podrobnosti**: Username, timestamp, account ID

### Deletion {#deletion}
- **Opis**: Beleži operacije brisanja e-pošte ali drugih virov
- **Podrobnosti**: Username, timestamp, affected resource
- **Opomba o skladnosti**: Operacije brisanja se beležijo za namene revizije skladnosti

## 🔍 Možnosti filtriranja {#filter-options}

Stran dnevnika ponuja naslednje možnosti filtriranja:

1. **Date** - Filtriranje po časovnem obdobju (od-do)
2. **Username** - Filtriranje po določenem uporabniku (le za Admin)
3. **Action Type** - Filtriranje po določeni vrsti dejanja

## 🖥️ Prikaz dnevnika {#log-display}

Dnevniki so prikazani v tabelarni obliki z naslednjimi stolpci:

- **Timestamp**: Datum in čas dejanja
- **Username**: Ime vpletenega uporabnika
- **Type**: Vrsta dejanja s barvno kodiranjem
- **Information**: Dodatne podrobnosti o dejanju

Za mobilne naprave so dnevniki prikazani v obliki kartic.

## 🔐 Dovoličenja dostopa {#access-permissions}

- **Uporabniki brez admin pravic**: Lahko vidijo le svoje lastne dnevnike
- **Admin uporabniki**: Lahko vidijo dnevnike vseh uporabnikov in jih filtrirajo po uporabniku

## 📤 Audit Data Export {#audit-data-export}

Admini lahko odjavno stran [Audit Data Export](AuditExport.md) odprejo neposredno z strani Dnevnikov. Ta ustvari tabelne pakete masovnih podatkov (INDEX.XML + CSV) iz arhive za zunanje revizijske orodje. Vsako izvajanje izvoza zapiše dve vpišava v dostopni dnevnik tipa **Audit Data Export** (začetek in rezultat), ki so na strani Dnevnikov navedeni in jih je mogoče filtrirati, podobno kot vsi ostali zapisi.

Za podrobnosti poglejte [vodič Audit Data Export](AuditExport.md).
