# 📥 Uvozi pošte v spletnem vmesniku (EML / MBox) {#importing-mail-in-the-web-ui-eml-mbox}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Poleg [vmesnika z zvočnimi ukazki](CLI.md) in [uvoza računov s CSV](Account-Import.md) lahko Mail Archiver uvozi obstoječi arhiv pošte **preko spletnega vmesnika**. Na voljo sta dve obliki nalaganja:

| Obrazec | Sprejeta datoteka | Rezultat |
|------|---------------|--------|
| **Import MBox** | `.mbox`, `.mbx` | Vsako sporočilo v datoteki se uvozi v **eno** izbrano mapo |
| **Import EML** | `.zip`, ki vsebuje `.eml` datoteke | **Struktura map v ZIP** določa, kam vsako sporočilo pristane |

Oba obrazca arhivirata sporočila v obstoječi Mail Archiver račun, oba delujeta kot **ozadinske naloge** in oba **samodejno preskočijo duplikate** — dvakratni uvoz iste datoteke ne ustvari druge kopije.

> 💡 **Nasvet**: Če je vaš arhiv že na strežniku kot datoteka, uporabite raje [CLI](CLI.md). To izogne nalaganju prek brskalnika in je boljša izbira za arhive večji od nekaj gigabajtov.

## 🧭 Dostop {#access}

1. V glavnem meniju odprite **Mail Accounts**.
2. Uporabite spustni seznam **Import** nad seznamom računov in izberite **Import MBox** ali **Import EML**.

Obrazce lahko uporabljajo **administratorji** (vsi računi) in **upravljavci lastnih računov** (računi, ki so jim dodeljeni); glejte [User Management](UserManagement.md#-user-account-permissions). Kot ciljne račune za uvoz so na voljo le **aktivni** računi — onemogočenega računa ni mogoče izbrati.

## 📨 Import MBox {#import-mbox}

MBox je format, ki ga večina e-poštnih odjemalcev uporablja za izvožene mape in za varnostne kopije v slogu `mbox`.

| Polje | Pom meaning |
|-------|---------|
| **MBox File** | Datoteka `.mbox` / `.mbx`, ki jo želite naložiti |
| **Target Account** | Mail Archiver račun, ki prejme sporočila |
| **Target Folder** | Mapa, v katero se sporočila uvože. Izberite eno iz obstoječega seznama map računa ali vpišite novo ime — mapa, ki še ne obstaja, se med uvozom **samodejno ustvari**. Privzeto je `INBOX`. |

Vsa sporočila v datoteki se uvože v to eno ciljno mapo. Sama datoteka MBox ne vsebuje informacij o mapah, zato se struktura prvotnega nabiralnika ne rekonstruira — če to potrebujete, uporabite pot EML/ZIP.

## 🗂️ Import EML {#import-eml}

Uvoz EML pričakuje **ZIP arhiv**, katerega vsebine so datoteke `.eml`.

- Datoteke `.eml` v **korenini** ZIP datoteke se uvože v `INBOX`.
- Datoteke `.eml` v **podmapi** se uvože v mapo z **isto imeno** kot ta podmapa.

Torej ZIP, postavljen takole:

```
mailbox.zip
├── 2024-invoice.eml            → INBOX
├── Archive/
│   └── old-message.eml         → Archive
└── Projects/
    └── 2026/
        └── kickoff.eml         → Projects/2026
```

obnovi drevo map z vsemi sporočili. Posameznih datotek `.eml` lahko uvozite tako, da jih najprej zapakirate v ZIP (`zip emails.zip *.eml`).

## ⚙️ Skupno vedenje {#shared-behaviour}

Oba obrazca se obravnavata na enak način, ko pritisnete **Start Import**:

- **나me limit velikosti datoteke** — konfiguriran z `Upload__MaxFileSizeGB` (privzeto **10 GB**). Obrazec ob izbiri datoteke prikaže trenutni limit.
- **Obdelava v ozadju** — nalaganje se doda v vrstico kot naloga; ni vam treba imeti brskalnika odprtega.
- **Obdelava duplikata** — sporočila, ki so že v arhivu, se preskočijo in štejejo posebej. Duplikati se ugotovijo glede na glavo `Message-ID`.
- **Napačni vnos** — nečitljive datoteke se preskočijo posamezno in o njih se poročilo na strani s statusom, namesto da bi se uvoz celotno prekinil.
- **Napredovanje** — neposredno ste preusmerjeni na stran s statusom naloge, ki prikazuje ID naloge, ime datoteke, velikost datoteke, status, število obdelanih/skupno število sporočil in tekoče števec. Ista naloga se pojavi tudi na strani [Jobs](BackgroundJobs.md).
- **Prekinjanje** — tekoči uvoz lahko prekinete na njegovi strani s statusom. Sporočila, uvožena do tiste točke, ostanejo v arhivu.
- **Zapisnik preglednosti** — uvoz se zapiše v [access log](Logs.md), po tem pa se osveži vrednost shrambe na račun (glejte [Per-Account Storage](AccountStorage.md)).

### Statusi nalog {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled`

Končana naloga poroča, koliko sporočil je bilo uvoženih, koliko jih je bilo neuspešnih in koliko jih je bilo preskočenih kot duplikati — na primer:
*"1420 uvoženih, 3 neuspešnih, 87 duplikatov"*. Ob uspešnem uvozu se naložena datoteka iz strežnika izbriše; neuspešni vstopi so ohranjeni skozi trajanje naloge, da lahko vidite, kaj je bilo zavrnjeno.

## ⚠️ Na kaj morate biti pozorni {#things-to-watch-out-for}

- **Napačna vrsta datoteke** — nalaganje bo zavrnjeno, razen če je razširitev ena izmed `.mbox`, `.eml` ali `.zip`. Izbirnik obrazca EML sprejema le `.zip`, izbirnik obrazca MBox pa le `.mbox` in `.mbx`.
- **"0 uvoženih, N duplikatov"** — to ni napaka. Sporočila so že v arhivu, najverjetneje zaradi predhodnega uvoza ali sinhronizacije istega vira.
- **Zelo veliki arhivi** — datoteka MBox se bere sporočilo po sporočilu, zato več gigabajtna datoteka vzame čas tudi v ozadju. Spremljajte napredovanje na strani s statusom namesto ponovnega nalaganja.
- **Prostor na disku** — naložena datoteka se začasno shrani na strežniku in potrebuje prostor poleg vaše baze podatkov arhivov.
- **Imena map s posebnimi znaki** — imena map se prevzamejo iz poti v ZIP datoteki ali vašega unosa; po uvozu preverite seznam map računa, če ste uporabili neobičajne znake.

## 🔗 Sorodna dokumentacija {#related-documentation}

- [CLI commands](CLI.md) — uvoz iz montiranega direktorija
- [Account Import (CSV)](Account-Import.md) — dodajanje več računov hkrati
- [Background Jobs](BackgroundJobs.md) — spremljanje in prekinjanje nalog uvoza
- [Exporting a Mail Account](Export-Account.md) — obratna smer
- [Setup and Parameters](Setup.md) — nastavitve `Upload__*`
