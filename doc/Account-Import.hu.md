# Fiókok importálása {#account-import}

[← Vissza a dokumentáció indexéhez](index.md)

## Áttekintés {#overview}

A Mail Archiver támogatja a több e-mail fiók egyszerre történő importálását, így a nagy számú postalakás bevezetése nem igényel egyesével létrehozott fiókokat.

A poskytatótól függően két tömeges importálási útvonal létezik:

| Provider | Bulk Import Method | Documentation |
|---|---|---|
| **Microsoft 365 (tenant)** | Tenant discovery via Microsoft Graph — one app registration accesses every mailbox in the tenant | [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) |
| **IMAP** | CSV bulk import — upload a CSV file with one row per mailbox | Ez az oldal (lásd alább) |

> **Megjegyzés:** A Microsoft személyes fiókjai (Outlook.com / M365 Family, poskytó **MSA**) interaktív eszközkód-hitelesítési folyamatot használnak, és nem importálhatók tömegesen. Ezeket egyenként kell létrehozni és hitelesíteni. Lásd: [MSA Outlook Setup](MSA_Outlook_Setup.md).

---

## IMAP CSV tömeges importálás {#imap-csv-bulk-import}

### Mit végez el {#what-it-does}

A CSV tömeges importálás lehetővé teszi a rendszergazdák számára, hogy egyetlen művelettel, egy CSV fájl feltöltésével több száz IMAP e-mail fiókot hozzanak létre. A fájl minden egyes sorozata egyetlen postalakáshoz tartozik. A gyakori szerverbeállítások (IMAP host, port, SSL) egyszer megadhatók a feltöltési űrlapon, és minden sorra vonatkoznak, de egyes sorokhoz opcionálisan megadhatók egyedi felülbírálások.

Az importálás **csak adminisztrátorok számára** elérhető. A **Mail Accounts** oldalon keresztül érhető el az **Import** legördülő menü → **CSV Bulk Import** menüpont alatt.

### CSV fájlformátum {#csv-file-format}

A fájlnak UTF-8 kódolású CSV-nek kell lennie, amely rendelkezik **fejléc sorral**, és a **szótagkezelő (delimiter) a vessző (`,`)**. A vesszőt vagy idézőjelet tartalmazó mezőket dupla idézőjelbe (`"..."`) kell zárni.

#### Oszlopok {#columns}

| Column | Required | Description |
|---|---|---|
| `email` | Igen | Postalakás címe. Ez szolgál alapértelmezett bejelentkezési felhasználónévként is, ha a `username` nem van megadva. |
| `password` | Igen | IMAP bejelentkezési jelszó. Úgy tárolódik, ahogy megadták — a kezdeti/dini szóközök megmaradnak. |
| `name` | Nem | A fiók megjelenítési neve. Ha elmarad, az alapértelmezett a `{prefix} - <{email}>`. |
| `username` | Nem | Bejelentkezési felhasználónév, ha az eltér az e-mail címtől. |
| `imap_server` | Nem | Soronkénti IMAP host. Felülírja a feltöltési űrlapon megadott közös szervert. |
| `imap_port` | Nem | Soronkénti IMAP port (1–65535). Felülírja a közös portot. |
| `use_ssl` | Nem | Soronkénti SSL kapcsoló (`true` vagy `false`). Felülírja a közös beállítást. |

#### Példa {#example}

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- Az 1. sor a feltöltési űrlapon megadott közös szervert/portot/SSL-t használja.
- A 2. sor egy egyedi felhasználónevet állít be, de egyébként a közös beállításokat használja.
- A 3. sor felülírja a szervert, a portot és az SSL-t.

Letölthető példafájl érhető el az importálási oldalon a **Download example CSV** gomb segítségével.

### Feltöltési űrlap {#upload-form}

| Field | Purpose |
|---|---|
| **CSV file** | A feltöltendő fájl. |
| **IMAP server** | Közös host minden olyan sorhoz, amely nem tartalmaz `imap_server` felülbírálást. |
| **IMAP port** | Közös port (alapértelmezett: 993). |
| **Use SSL** | Közös SSL kapcsoló (alapértelmezett: be). |
| **Name prefix** | Előtag az automatikusan generált fióknevekhez. Alapértelmezett: `IMAP`. |
| **Skip existing mailboxes** | Ha be van kapcsolva, azokat a sorokat, amelyek e-mail címe már létezik IMAP fiókként, figyelmen kívül hagyja (ezek "kimaradtnak" számítanak, nem hibának). Ha ki van kapcsolva, ezek hibaként jelennek meg. |
| **Account enabled** | Ez határozza meg, hogy a létrehozott fiókok engedélyezettek legyenek-e a háttérszinkronizáláshoz. |
| **Delete After Days / Local Retention Days** | Opcionális megőrzési beállítások, amelyek minden létrehozott fiókra vonatkoznak. Lásd: [Retention Policies](RetentionPolicies.md). |

### Feldolgozás {#processing}

1. A CSV fájlt elemzik, és minden sort ellenőriznek (e-mail formátum, jelszó megléte, port tartomány, SSL érték).
2. A fájlon belüli duplikált e-mail címeket eltávolítják (az első előfordulás marad meg).
3. A meglévő, azonos e-mail címmel rendelkező IMAP fiókokat érzékelik; a **Skip existing** kapcsolótól függően vagy figyelmen kívül hagyják, vagy hibaként jelentik.
4. Minden új fiókot egyetlen adatbázis-csoportosításban (batch) tesznek be.
5. Egy hozzáférési naplóbejegyzés készül a futtatás összefoglalásáról.

Az importálás során nem történik IMAP kapcsolatteszt — a fiókok azonnal elmentésre kerülnek. Az autentikációs hibák csak az első háttérszinkron során fognak felmerülni. Ez gyorsítja az importálást, még több száz postalakás esetén is.

### Eredménysorold {#result-page}

Az importálás befejezdése után egy eredményoldal három táblázatot mutat:

- **Created** — minden létrehozott fiók e-mail címe és neve.
- **Skipped** — azok az e-mail címek, amelyek duplikáltak voltak (a fájlon belül vagy már léteztek), ha a **Skip existing** be van kapcsolva.
- **Failed** — azok a sorok, amelyeket nem lehetett feldolgozni, a sorazattal és a hiba okával együtt (hiányzó e-mail, hiányzó jelszó, érvénytelen port, nincs IMAP szerver stb.).

Egy összefoglaló sáv mutatja a számokat: *{created} létrehozott, {skipped} kimaradt, {failed} sikertelen*.

### Korlátok {#limits}

A következő korlátok `appsettings.json` / környezeti változókon keresztül konfigurálhatók (lásd: [Setup Guide](Setup.md) → *CSV Import Settings*):

| Setting | Default | Description |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | A feltöltésenként feldolgozható maximális sorok száma. |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | A feltöltött fájl maximális mérete. |

### Biztonsági megjegyzés {#security-note}

Az IMAP jelszavakat ugyanúgy tárolják az adatbázisban, mint az egyenként létrehozott IMAP fiókokat. Tudjon mielőtt elindítja: a CSV fájl minden postalakás jelszavát szöveges formátumban tartalmazza — a sikeres importálás után törölje le a kliens gépről.

---

## Microsoft 365 Tenant Import {#microsoft-365-tenant-import}

A Microsoft 365 (Exchange Online) esetében a tömeges importálás nem igényel CSV fájlt. Egyetlen Azure AD alkalmazás-regisztráció a megfelelő Graph jogosultságokkal minden postalakáshoz hozzáférhet a tenantben, így a Mail Archiver automatikusan felismeri a postalakásokat.

Lásd a dedicated útmutatót: **[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**.
