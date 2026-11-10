# Uvoz računov {#account-import}

[← Nazaj na indeks dokumentacije](index.md)

## Pregled {#overview}

Mail Archiver omogoča uvoz več e-poštnih računov hkrati, tako da uvajanje velike števila poštovnih predalov ne zahteva ustvarjanja vsakega računa posebej.

Obstajata dve poti za masovni uvoz, odvisno od ponudnika e-pošte:

| Ponudnik | Metoda masovnega uvoza | Dokumentacija |
|---|---|---|
| **Microsoft 365 (tenant)** | Iskanje tenanta prek Microsoft Graph — ena registracija aplikacije dostopa do vsakega poštovnega predala v tenantu | [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) |
| **IMAP** | Masovni uvoz CSV — naložite CSV datoteko z enim vrstico na poštovni predal | Ta stran (glejte spodaj) |

> **Opomba:** Osebni računi Microsoft (Outlook.com / M365 Family, ponudnik **MSA**) uporabljajo interaktivni proces avtorizacije s kodo naprave in jih ni mogoče uvoziti masovno. Te je treba ustvariti in avtorizirati posamešno. Poglejte [MSA Outlook Setup](MSA_Outlook_Setup.md).

---

## Masovni uvoz IMAP CSV {#imap-csv-bulk-import}

### Kaj to počne {#what-it-does}

Masovni uvoz CSV omogoča administratorju ustvarjanje stotih IMAP e-poštnih računov z enim operacijskim korakom z nalogovanjem CSV datoteke. Vsaka vrstica v datoteki ustreza enemu poštovnemu predalu. Skupne nastavitve strežnika (IMAP host, port, SSL) lahko navedete enkrat v obrazcu za nalogovano datoteko in se nanašajo na vsako vrstico, z možnostjo prepričevanja (override) za posamezno vrstico.

Uvoz je namenjen **izključno administratorjem**. Dostopajo do njega na strani **Mail Accounts** prek spustnega menija **Import** → **CSV Bulk Import**.

### Format CSV datoteke {#csv-file-format}

Datoteka mora biti v kodeliranju UTF-8, imeti **vrstico s fastavki (header)** in **zareza (`,`) kot ločitev**. Polja, ki vsebujejo zareze ali navodila, morajo biti zapakirana v dvojne navodajalne znake (`"..."`).

#### Stolpci {#columns}

| Stolpec | Obvezno | Opis |
|---|---|---|
| `email` | Da | Naslov poštovnega predala. Uporablja se tudi kot uporabniško ime za prijava, ko ni navedeno `username`. |
| `password` | Da | Geslo za IMAP prijavo. Shranjeno kot je navedeno — presledki na začetku/koncu se ohranijo. |
| `name` | Ne | Ime za prikaz računa. Če je izpuščeno, je privzeto `{prefix} - <{email}>`. |
| `username` | Ne | Uporabniško ime za prijavo, če se razlikuje od e-poštnega naslova. |
| `imap_server` | Ne | IMAP host za posamezno vrstico. Preponi (override) skupnega strežnika iz obrazca za nalogovano datoteko. |
| `imap_port` | Ne | IMAP port za posamezno vrstico (1–65535). Preponi skupnega porta. |
| `use_ssl` | Ne stik za SSL za posamezno vrstico (`true` ali `false`). Preponi skupne nastavitve. |

#### Primer {#example}

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- Vrstica 1 uporablja skupni strežnik/port/SSL iz obrazca za nalogovano datoteko.
- Vrstica 2 nastavi prilagojeno uporabniško ime, sicer pa uporablja skupne nastavitve.
- Vrstica 3 preponi (override) strežnik, port in SSL.

Primer datoteke, ki jo je mogoče prenesati, je na strani za uvoz na gumbu **Download example CSV**.

### Obrazec za nalogovano datoteko {#upload-form}

| Polje | Namen |
|---|---|
| **CSV file** | Datoteka za nalogovano datoteko. |
| **IMAP server** | Skupni host za vse vrstice brez `imap_server` preponi. |
| **IMAP port** | Skupni port (privzeto 993). |
| **Use SSL** | Skupni stik za SSL (privzeto vklopljeno). |
| **Name prefix** | Prefiks za samodejno generirana imena računov. Privzeto: `IMAP`. |
| **Skip existing mailboxes** | Ko je omogočeno, se vrstice, katerih e-pošta že obstaja kot IMAP račun, preskočijo (štete kot "preskočeno", ne kot napaka). Ko je onemogočeno, se obravnavajo kot neuspela operacija. |
| **Account enabled** | Ali morajo biti ustvarjeni računi omogočeni za delovanje v ozadju. |
| **Delete After Days / Local Retention Days** | Neprijetne nastavitve zadrževanja, ki se nanašajo na vsak ustvarjen račun. Poglejte [Retention Policies](RetentionPolicies.md). |

### Obdelava {#processing}

1. CSV datoteka je razčlenjena in vsaka vrstica je preverjena (format e-pošte, prisotnost gesla, obseg porta, vrednost SSL).
2. Podvojeni e-poštni naslovi znotraj datoteke so odstranjeni (zmaguje prvi pojav).
3. Zaznani so obstoječi IMAP računi z isto e-poštno napako; odvisno od stika **Skip existing** so ali preskočeni ali poročajo kot neuspela operacija.
4. Vsi novi računi so vstavljeni v en batch v bazi podatkov.
3. Zapis v dnevnik dostopa povzema izvedbo.

Med uvozom se ne izvaja test IMAP povezave — računi so shranjeni takoj. Težave z avtorizacijo se bodo pojavile šele med prvim sinhroniziranjem v ozadju. To zagotavlja hitrost uvoza tudi za stotine poštovnih predalov.

### Stran z rezultati {#result-page}

Po končnemu uvozu stran z rezultati prikaže tri tabele:

- **Created** — e-pošta in ime vsakega ustvarjenega računa.
- **Skipped** — e-poštni naslovi, ki so bili podvojeni (v datoteki ali že obstoječi), ko je vklopljen **Skip existing**.
- **Failed** — vrstice, ki jih ni bilo mogoče obdelati, s številko vrstice in razlogom (ni e-pošte, ni gesla, neveljavni port, ni IMAP strežnika itd.).

Povzetek v pasici prikazuje števila: *{created} ustvarjenih, {skipped} preskočenih, {failed} neuspelih*.

### Omejitve {#limits}

Slednje omejitve je mogoče nastaviti prek `appsettings.json` / okoljskih spremenljivk (glejte [Setup Guide](Setup.md) → *CSV Import Settings*):

| Nastavitev | Privzeto | Opis |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | Največja števila vrstic obdelanih na uvoz. |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | Največja velikost naložene datoteke. |

### Varnostna opomba {#security-note}

Gesla IMAP so v bazi podatkov shranjena na isti način kot pri posamešno ustvarjenih IMAP računih. Upravite z dejstvom, da CSV datoteka vseže vse dele gesla poštovnih predal v besedilni obliki — po uspešnem uvozu jo izbrišite z računalnika.

---

## Microsoft 365 Tenant Import {#microsoft-365-tenant-import}

Za Microsoft 365 (Exchange Online) masovni uvoz ne zahteva CSV datoteke. Ena registracija Azure AD aplikacije z ustreznimi Graph dovoljenji lahko dostopa do vsakega poštovnega predala v tenantu, zato lahko Mail Archiver poštovne predale odkrije samodejno.

Poglejte namenski vodič: **[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**.
