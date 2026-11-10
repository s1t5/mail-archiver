# 📤 Izvoz revizijskih podatkov {#audit-data-export}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Izvoz revizijskih podatkov ustvari pakete masovnih tabelarnih podatkov (ZIP z `INDEX.XML` + CSV tabele + indeks DTD) iz obstoječega arhiviv. Ti paketi sledijo uveljavljeni strukturi indeksa za predajo medijev s podatki in jih lahko neposredno uvožejo običajni zunanji revizijski in analitični pripomočki, ki podpirajo to format indeksa.

Ta funkcija je na voljo kot **namenska stran, dostopna z strani Logs** in je omejena na uporabnike z administracijskimi pravicami. Vsak izvoz se zabeleži v dnevnik dostopa, zato je zgodovina izvoza varno revidiranih.

## 🧭 Dostop {#access}

1. Prijavite se kot uporabnik z administracijskimi pravicami (admin)
2. Odprite stran **Logs**
3. Kliknite gumb **Audit Data Export** v zgornjem desnem kotu

Uporabniki brez administracijskih pravic ne vidijo gumba in ne morejo klicati nobene od končnic (endpoints) za izvoz (strežnik jih zavrne).

## 📦 Struktura paketa izvoza {#export-package-structure}

Generirana ZIP datoteka se imenuje `audit-export-<timestamp>.zip` in vsebuje:

| Datoteka | Vsebina |
|---|---|
| `INDEX.XML` | Indeksna datoteka, ki opisuje dobavitelja podatkov, medij in vse definicije tabel/stolpcev |
| `index.dtd` | DTD, na katerega se nanaša indeksna datoteka; zahtevano s strani uvoznih orodij za validacijo |
| `emails.csv` | Ena vrstica za vsako arhivirano e-pošto v izbranem obdobju/poštnem predalu |
| `attachments.csv` | Neobvezno; ena vrstica za vsako prilogo izvoženih e-poštnih sporočil |

### Tabela `emails.csv` {#table-emailscsv}

Stolpci (brez vrstice s ponavodili; imena stolpcev prihajajo iz `INDEX.XML`, kot je običajno za to obliko):

| Stolpec | Opis |
|---|---|
| Id | Interni ID e-pošte (numerični) |
| MessageId | E-poštni Message-ID header |
| SentDate | Datum pošiljanja, ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`) |
| ReceivedDate | Datum prejema, ISO 8601 UTC |
| IsOutgoing | `1` = odhajajoče, `0` = prihajajoče |
| From | Pošiljatelj |
| To | Prejemniki |
| Cc | Cc prejemniki |
| Bcc | Bcc prejemniki |
| Subject | Zadeva |
| FolderName | Mapo, v katero je e-pošta arhivirana |
| HasAttachments | `1` = s prilogami, `0` = brez |
| AttachmentCount | Število prilog (numerični) |
| AccountEmail | E-poštni naslov arhivskega poštnega predala |

### Tabela `attachments.csv` {#table-attachmentscsv}

| Stolpec | Opis |
|---|---|
| EmailId | Interni ID e-pošte, kateri pripada priloga (kljjuč za povezavo z `emails.csv`) |
| FileName | Ime datoteke priloge |
| ContentType | MIME tip |
| Size | Velikost v bajtih (numerični) |
| Sha256 | SHA-256 hash vsebine (iz shrambe prilog z adresiranjem vsebine) |

### Konvencije CSV {#csv-conventions}

- Kodiranje: UTF-8 **brez** BOM (označeno kot `UTF8` v `INDEX.XML`)
- Delilnik stolpcev: `;`
- Encapsulator besedila: `"` (dvojno za izogibanje znakom)
- Delilnik zapisa: `\n` (izrecno navedeno v `INDEX.XML`)
- Datumi: ISO 8601, časi pretvorjeni iz konfigurirane časovne zone prikazov v UTC
- Brez vrstice s ponavodili; brez lokaliziranih številk (numerični stolpci vsebujejo navadno celotenja števil)

## 🖥️ Uporaba strani za izvoz {#using-the-export-page}

### Polja obrazca {#form-fields}

1. **From / To date** - Obdobje za izvoz (obvezno, največje razpon je lahko konfiguriran, privzeto 10 let)
2. **Mailbox** - Omejite izvoz na eno poštno predalo ali izvozite vsa poštna predala
3. **Attachment metadata** - Če je območčeno, se v paket doda `attachments.csv`
4. **Data supplier name / location / comment** - Zapiše se v blok `DataSupplier` v `INDEX.XML`; predpolnjeno iz razdelka konfiguracije `AuditExport` in lahko ureja za vsak izvoz posebej

### Življenjski cikel opravila {#job-lifecycle}

1. Klik na **Start Export** validira obdobje in poštno predalo, ustvari opravilo in vrne na stran
2. Opravilo teče v ozadju (vsi izvozi v vrsti so obdelovani enega za drugim); tabela opravil prikazuje v živo napredek (obdelanih/skupno število e-poštnih sporočil s stekom za napredovanje) prek samodejnega osveževanja (polling)
3. Ko se izvoz konča, se v vrstici opravila prikaže gumb **Download**
4. Tečoča ali na vrsti opravila lahko **prekinete**

### Zgodovina izvoza {#export-history}

Tabela zgodovine pod obrazcem navaja najnovejše izvoze s časovnim žigom, uporabnikom, obdobjem, poštovnim predalom, število e-poštnih sporočil, statusom in velikostjo datoteke. Zgodovina je **shranjena v bazi podatkov** (tabela `AuditExportJobs`), zato ostane tudi po ponovnem zagonu aplikacije in je varno revidirana.

Prenosne datoteke so shranjene za določeno obdobje shranjevanja (privzeto 30 dni, poglejte `AuditExport__RetentionDays`); po tem jih dnevno čiščenje izbriše, vrstice v zgodovini pa ostanejo.

> 📝 **Opomba**: Datoteke izvoza so shranjene znotraj posrednika (container) aplikacije. Če jih potrebujete dolgoročno, jih prenesite in shranite v svoje skladišče revizijskih dokazov.

## 🔐 Revizija {#auditing}

Vsak izvoz ustvari dve novovi vrsti v dnevniku dostopa **Audit Data Export**:

1. **Start entry** - ko je izvoz v vrsti (z ID opravila, obdobjem, poštovnim predalom, možnostmi v obliki JSON)
2. **Result entry** - ko je izvoz končan, neuspešen ali prekinjen (z ID opravila, obdobjem, poštovnim predalom, število e-poštnih sporočil, rezultatom in velikostjo paketa v obliki JSON)

Vpiski se pojavijo na strani Logs kot vsi ostali vpisi v dnevnik dostopa in jih lahko filtrirate po novi vrsti. Spustni meni za filtriranje vrst jih samodejno prepozna.

## ⚙️ Konfiguracija {#configuration}

Razdelek `AuditExport` v `appsettings.json` (okoljske spremenljivke uporabljajo sintakso z dvojnim podčrtajem, npr. `AuditExport__RetentionDays`):

| Nastavitev | Opis | Privzeto |
|---|---|---|
| `DataSupplierName` | Privzena vrednost za polje imena dobavca podatkov | prazno |
| `DataSupplierLocation` | Privzena vrednost za polje lokacije dobavca podatkov | prazno |
| `Comment` | Privzena vrednost za polje komentarja | prazno |
| `OutputDirectory` | Direktorij za generirane ZIP datoteke (relativne poti se razrešujejo glede na korenino vsebine aplikacije) | `exports/audit` |
| `RetentionDays` | Število dni, preden so končani izvozi izbrisani | `30` |
| `MaxRangeYears` | Največ allowed razpon med začetnim in končnim datumom obdobja | `10` |

## 🗄️ Baza podatkov {#database}

Zgodovina izvoza je shranjena v tabeli `mail_archiver."AuditExportJobs"` (ustvarjena z migracijo `MigrateV2609_1`, ki se samodejno uporabi ob zagonu). Ta funkcija ne spreminja obstoječih tabel.

## 🔍 Reševanje težav {#troubleshooting}

| Problem | Vzrok / Rešitev |
|---|---|
| Gumb Download manjka po ponovnem zagonu | Datoteke izvoza so v posredniku; če je bil posrednik ponovno ustvarjen ali je minilo obdobje shranjevanja, je ZIP izbrisan, vrstica v zgodovini pa ostane. Ponovno izvedite izvoz. |
| "The period must not exceed X years" | Zmanjšajte obdobje ali povečajte `AuditExport__MaxRangeYears`. |
| Opravilo je dolgo v stanju *Running* | Veliki arhiivi se pretakajo v paketih po 1000 e-poštnih sporočil; napredovanje se nenehno posodablja. Velika obdobja lahko trajajo precej časa — preverite števec obdelanih/skupno število. |
| Orodje za uvoz prijavi težave s kodiranjem | Paket je UTF-8 brez BOM z navedeno zastavico `UTF8` in izrecnim delilnikom zapisa; prepričajte se, da orodje bere `INDEX.XML` poleg datotek s podatki (razpakirajte celoten ZIP, ne odpirajte datotek posamezno znotraj ZIP-a). |

## 🔗 Sorodna dokumentacija {#related-documentation}

- [Access Logging](Logs.md)
- [Installation, Setup and Parameters](Setup.md)
