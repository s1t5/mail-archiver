# 📤 Izvoz pošte (EML / MBox) {#exporting-a-mail-account-eml-mbox}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Izvoz računa zapiše **vsa sporočila enega računa** v prenosljivo ZIP datoteko. Je nasprotje
[web uvoza](Import-EML-MBox.md): pridobite prenosljivo arhivsko datoteko računa v
formatu, ki ga lahko bere kateri koli e-poštni klijent ali orodje za migracijo.

Na voljo sta dve obliki:

| Oblika | Kaj prejmete | Tipična uporaba |
|--------|--------------|-------------|
| **EML** | Posamezne `.eml` datoteke, organizirane v mapi | Ponovni uvoz v e-poštni klijent, obdelava posameznih sporočil, dolgoročna berljivost |
| **MBox** | Tradicionalne datoteke poštovnih predalov — **ena datoteka na mapo** | Orodja in klijenti, ki berejo MBox, masovna obdelava besedil |

V obeh primerih so sporočila zapisana kot surova sporočila RFC-822, zato so **priloge
vključene** v izvoz.

> 💡 **Nasvet**: Če želite izvoziti le izbiro sporočil namesto celotnega računa,
> uporabite masovne ukrepe v [Mail search](Search.md#-acting-on-many-results-at-once).

## 🧭 Dostop {#access}

1. V glavnem meniju odprite **Mail Accounts**.
2. Kliknite gumb **⋮** (več dejanj) v vrstici računa in izberite
   **Export**. Ista možnost je na strani **Details** računa.

Za izvoz zahtevana sta vloga **admin** ali **self manager** — standardni uporabniki vidijo
vpis, vendar ga ne morejo uporabiti. Izvoz vedno zajema celoten račun; ni
možnosti filtriranja po datumu ali mapi.

## 🖥️ Uporaba strani za izvoz {#using-the-export-page}

Stran prikazuje **skupno**, **prejemano** in **pošiljanje** število sporočil računa,
tako da lahko vidite velikost opravila, preden ga začnete. Nato:

1. Izberite obliko — **EML** ali **MBox**.
2. Pritisnite **Start Export**.

Izvoz poteka **v ozadju**. Preusmerjeni boste na stran s statusom, ki prikazuje
ID opravila, izbrano obliko, časovne žigove in število obdelanih/skupnih sporočil.

### Statusi opravila {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

Ko opravilo doseže status **Completed**, se na strani statusa pojavi **gumb za prenos** (download button). Isto opravilo je navedeno tudi na strani [Jobs](BackgroundJobs.md).

> ⚠️ **Datoteke izvoza se samodejno čistijo.** ZIP datoteka je izbrisana **po
> uspešnem prenosu** in v vsakem primeru **po 7 dneh**. Datoteko prenesite, dokler
> je še na voljo — sama arhiva ni vplivna, kasneje lahko preprosto začnete
> nov izvoz.

## ⚠️ Na kaj morate biti pozorni {#things-to-watch-out-for}

- **Veliki računi zahtevajo čas.** Velik poštni predal lahko zahteva več minut in
  ZIP lahko postane zelo velik, odvisno od količine sporočil in prilog.
  Ker opravilo teče v ozadju, lahko zaprete brskalnik in se kasneje vrnete
  na stran s statusom.
- **Prostor na strežniku.** ZIP se sestavi na strežniku, preden ga prenesete,
  zato poskrbite, da je zraven arhive dovolj prostora.
- **"Export file not found"** — opravilo še ni končano ali je bila datoteka
  že prenesena ali odstranjena zaradi 7-dnevnega čiščenja. Začnite nov izvoz.
- **Brez možnosti filtriranja** — izvoz je za vsak račun vse ali nič. Za
  podobino (podnabor) uporabite izvoz na strani za iskanje (search-page).
- **Ni izvoz za skladnost (compliance).** Za predviden prenos medijev podatkov,
  ki je pripravljen na revizijo s datoteko indeksa, uporabite namesto tega
  [Audit Data Export](AuditExport.md) — ta ustvari paket v tabelarni obliki masovnih podatkov,
  ki ga pričakujejo revizijska orodja.

## 🔗 Sorodna dokumentacija {#related-documentation}

- [Importing Mail in the Web UI](Import-EML-MBox.md) — obratna smer
- [Mail search](Search.md) — izvoz izbranega nabora sporočil
- [Background Jobs](BackgroundJobs.md) — seznam opravil, napredovanje in prekličanje
- [Audit Data Export](AuditExport.md) — paketi izvoza, pripravljeni na revizijo
- [CLI commands](CLI.md) — uvoz iz montiranega direktorija
