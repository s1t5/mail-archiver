# ⚙️ Zadaja v ozadju {#background-jobs}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Večina oprav v Mail Archiverju se ne odvija med vašim čakanjem. Sinhronizacija
poštnega predala, uvoz datoteke MBOX, obnavitev tisočih e-poštnih sporočil ali izvoz
arhiva lahko traja od minut do ur. Te naloge se izvajajo kot **zadaje v ozadju** —
začnete jih, in te nadaljujejo z delovanjem, tudi če zaprete brskalnik.

Stran **Background Jobs** prikazuje, kaj se trenutno izvaja, kaj je bilo končano in
kjer je šlo narobe. To je kraj, kamor pogledate, ko nekaj traja dlje kot
pričakujejo ali ko sinhronizacija konča z napakami.

> **ℹ️ Administratorska stran.** Background Jobs zahteva administratorna prava in
> je dostopna iz glavnega menija. Običajni uporabniki je ne vidijo — lahko še vedno
> začnejo naloge (na primer množično obnavitev), vendar se spremljanje izvaja tukaj.

## 🗂️ Vrste oprav {#job-types}

Stran grupirira naloge po vrstah. Vsak razdelek prikazuje največ **20 najnovejših**
zadaj, pri čemer so te, ki tečejo in so v vrstah, razvrščene na vrh — tako stara
naloga ne bo izginila iz pogleda zaradi novih.

| Razdelek | Kaj zajema | Začeto iz |
|---|---|---|
| **Synchronization Jobs** | Pridobivanje nove pošte s poštnega strežnika | Avtomatski načrt ali *Sync now* pri računu |
| **MBox Import Jobs** | Uvoz `.mbox` datotek | Mail Accounts → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **EML Import Jobs** | Uvoz `.eml` datotek / ZIP arhivov | Mail Accounts → [Import EML](Import-EML-MBox.md#-import-eml) |
| **Batch Restore Jobs** | Kopiranje izbranih e-poštnih sporočil nazaj v poštni predal | Archive → select emails → Batch Restore |
| **Account Export Jobs** | Izvoz celotnega računa (mbox / EML) | Mail Accounts → [Export](Export-Account.md) |
| **Selected Emails Export Jobs** | Izvoz ročno izbranega nabora e-poštnih sporočil | Archive → select emails → Export |
| **Email Deletion Jobs** | Brisanje e-poštnih sporočil s strežnika ali iz arhiva, vključno s masovnim brisanjem na podlagi filtrov | Pravila zadržanja, ročno brisanje ali Archive → *Delete all matches* |
| **Account Deletion Jobs** | Odstranitev celotnega poštnega računa | Mail Accounts → Delete |
| **Offload** | Premikanje pošte v določenem časovnem oknu v drug račun | Mail Accounts → Offload |

## 📊 Branje vrstice naloge {#reading-a-job-row}

Vsaka vrstica vsebuje isti nabor stolpcev:

| Stolpec | Pomen |
|---|---|
| **Job ID** | Kratka identifikacija — navedite to pri prijavi težave |
| **Account** | Poštni predal, na katerem naloga dela |
| **Status** | Trenutno stanje, glejte spodnjo tabelo |
| **Progress** | Do katerega točke je naloga napredovala, s prikazom obdelanih/skupno, če je znano |
| **Created** | Kdaj je bila naloga dodana v vrsto |
| **Started** | Kdaj se je dejansko začela izvajati |
| **Completed** | Kdaj je bila končana (prazno, medtem ko še teče) |
| **Duration** | Minili čas |

Neke vrste oprav dodajo štejalnike v stolpec progress:

- **Success** / **Failed** — e-poštna sporočila, obdelana uspešno in neuspešno
- **Skipped (duplicates)** — e-poštna sporočila, ki so že prisotna v arhivu, niso uvožena dvakrat
- **Recovered** — sporočila, pridobljena ponovno po tem, ko je strežnik poročal, da jih ni |
- **Malformed (skipped)** — sporočila, ki jih ni bilo mogoče razvrstiti in so bila izpuščena |

### Vrednosti statusa {#status-values}

| Status | Pomen |
|---|---|
| **Queued** | Čaka na začetek — druga naloga teče ali je delavec zaposlen |
| **Running** | Trenutno se izvaja |
| **Completed** | Uspešno končano |
| **CompletedWithErrors** | Končano, vendar nekatere postavke niso uspele (MBox uvozi) |
| **Failed** | Končano z napako |
| **Cancelled** | Urejeno s strani uporabnika |
| **Rate Limited** | *Sinhronizacija ustavljena zaradi omejitve pasovne širine. Bo samodejno nadalčala.* |
| **Timed Out** | Sinhronizacija je presegel konfigurirano časovno omejitev in je bila ustavljena |

> **ℹ️ "Rate Limited" in "Timed Out" sta stanja, ki se nanašajo le na sinhronizacijo.** Izgledata kot
> napaki, vendar niso: sinhronizacija z omejeno hitrostjo (rate-limited) nadaljuje sama, ko
> kvota pasovne širine to dovoli, in sinhronizacija s časovno omejitvijo (timed-out) nadaljuje
> od svojega kontrolnega točka pri naslednjem zagonu. Poglejte [Rate Limit Handling](RateLimitHandling.md) in
> [Mail Synchronization](Synchronization.md).

### Zakaj lahko progress ostane pri 0 % {#why-progress-can-sit-at-0}

Napredovanje se meri s **končanimi mapami**, ne z posameznimi e-poštnimi sporočili:

> *Napredovanje se meri s končanimi mapami. Velika mapa (npr. INBOX) ohranja
> stolpec na 0 %, dokler se ne konča.*

Pri poštnem predalu, kjer INBOX vsebuje večino pošte, lahko stolpec ostane pri 0 %
dolga časa in nato nenadoma skoči naprej. To je pričakovano — naloga dela, le da
še ni končala s svojo prvo veliko mapo.

## 🔄 Stran se samodejno osvežuje {#the-page-refreshes-itself}

Ko je stran odprta, se ponovno naloži vsakih **30 sekund**, tako da jo lahko pustite v
zavihani zavihki in spremljate napredovanje brez klikanja česar koli. Oznaka prikazuje,
kdaj so bili podatki nazadnje posodobljeni.

Osvežitev se prekliče v trenutku, ko kliknete povezavo ali gumb — sicer bi vaš klik
padel na stran, ki se prav takrat nad njo zamenjuje.

**Konfigurabilno:** `Jobs:RefreshSeconds` v `appsettings.json` (privzeto `30`).
Nastavite na `0`, da izklopite samodejno osveževanje. Vsako osvežitev ponovno zgradi stran
vsem iz vseh virov oprav, zato je zelo kratek interval pri namestitvi z veliko računov
resnično obremenitev — in ta obremenitev se мноži z vsakim odprtim zavihkom.

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 Prekinjanje naloge {#cancelling-a-job}

Naloge, ki podpirajo prekinjanje, prikazujejo gumb **Cancel Job**, ki vpraša:

> *Ali ste prepričani, da želite prekiniti to nalogo?*

Naloge uvoza uporabljajo besedilo **Cancel Import** / *Are you sure you want to cancel
this import job?*

Prekinjanje je na voljo za naloge **sinhronizacije, množinske obнове, izvoza izbranih
e-poštnih sporočil in brisanja e-poštnih sporočil**. Naloga se ustavi takoj, ko je trenutna
postavka končana; status postane **Cancelled**.

> **⚠️ Prekinjanje ni vračilo v prejšnje stanje (rollback).** E-poštna sporočila, ki so že
> uvožena, obnovljena, izvožena ali izbrisana, ostanejo v takšnem stanju. Prekinjanje ne
> prepreči nalogi, da bi opravila *več* — ne ponaga tistega, kar je že opravila. Pri
> nalogi za brisanje to pomeni, da so e-poštna sporočila, ki so že bila odstranjena, trajno izgubljena.

Nalog uvoza računov in MBox ne lahko prekinete na tej strani; namesto tega jih ustavite
na lastni strani za uvoz računa.

## ⚠️ Napake pri sinhronizaciji in "Potrditev napak" (Acknowledge Failures) {#sync-failures-and-acknowledge-failures}

Ko sinhronizacija konča s statusom **Failed**, vrstica naloge ponuja možnost **Acknowledge
Failures**. To je edina akcija na strani, ki spremeni prihodnje vedenje, zato jo je
smiselno razumeti.

Vsak zagon sinhronizacije se začne tam, kjer se je zadnji končal, z uporabo **kontrolnega
točka** (checkpoint). Če sinhronizacija ne uspe zaradi nekaterih e-poštnih sporočil,
kontrolni toček ostane za temi sporočili, da jih naslednji zagon poskusi znova. To je
varna privzeta nastavitev — nič ne bo tiho preskočeno.

Toda če so napake trajne (poštnem sporočilu je poškodovano, strežnik pa ne želi poslužiti
mape), vsaka naslednja sinhronizacija teh sporočil ponovno poskuša in sinhronizacija
še vedno ne uspe na istem mestu.

**Acknowledge Failures** reši to zastoj s pomočjo premika kontrolnega točka mimo
neuspešnih e-poštnih sporočil. Potrditev je eksplicitna glede posledic:

> *To bo premaknilo časovni žig sinhronizacije na čas končanja te naloge, tako da
> naslednja sinhronizacija bo običajna inkrementalna sinhronizacija. Neuspešnih
> e-poštnih sporočil se ne bo ponovno poskušalo obdelati. Nadaljevati?*

Ob uspehu vidite: *"Sync timestamp has been advanced. The next sync will be
incremental."*

> **⚠️ Napake potrdite šele, ko ste jih pregledali.** Po potrditvi se teh e-poštnih
> sporočil **ne** bo ponovno poskušalo obdelati — v arhivu jih ne bo, razen če jih
> pridobite na kakšen drug način. Najprej preverite podrobnosti napake in če je
> vzrok napačna konfiguracija, to popravite, preden potrdite. Za podrobnosti o
> vedenju kontrolnih točk si oglejte [Mail Synchronization](Synchronization.md).

Potrditev se v dnevniku dostopa zabeleži kot *SyncAcknowledgeFailures*, s številom
neuspešnih e-poštnih sporočil in računom, zato je kasneje sledljiva.

## 👁️ Česa tukaj ne morete početi {#what-you-cannot-do-here}

- **Ni gumba za ponovni poskus.** Neuspešne naloge ne zagonite z te strani — ponovno
  začnite operacijo tam, kjer ste jo začeli (sinhronizacija, uvoz, izvoz).
- **Ni urejanja.** Parametri naloge so določeni ob ustvarjanju.
- **Le zadnjih 20 na vrsto.** Starejše naloge niso navedene. Za dolgoročne zapise
  so viri dnevnik dostopa in dnevnik aplikacije — poglejte [Access Logging](Logs.md) in
  [Docker Compose Logs](DockerComposeLogs.md).

## ❓ Reševanje težav {#troubleshooting}

| Simptom | Verjetni vzrok | Kaj storiti |
|---|---|---|
| Naloga ostane **Queued** | Druga naloga istega delavca teče | Počakajte; preverite druge razdelke za dolgotrajno nalogo |
| Napredovanje je ustavljeno pri 0 % | Ena velika mapa se še vedno obdelava | Normalno — glejte "Zakaj lahko progress ostane pri 0 %" |
| Sinhronizacija večkrat **Failed** na istem računu | Trajne napake pri posameznih e-poštnih sporočilih | Pregledajte napake, popravite vzrok, nato potrdite |
| Sinhronizacija prikazuje **Rate Limited** | Dosegnięta kvota pasovne širine | Nič — samodejno nadaljuje |
| Sinhronizacija prikazuje **Timed Out** | Poštni predal je večji od časovne omejitve | Pri naslednjem zagonu nadaljuje od kontrolnega točka; glejte [Synchronization](Synchronization.md) |
| **CompletedWithErrors** pri MBox uvozu | Nekatera sporočila so poškodovana | Števalci kažejo, koliko; ostalo je bilo uvoženo |
| Naloga izgine po osvežitvi | Izpadla je iz okna "zadnjih 20" | Preverite dnevnik aplikacije za njen končni status |
| Stran se osvežuje med delom | Avtomatsko osveževanje vsakih 30 s | Nastavite `Jobs:RefreshSeconds` na `0` |

## 📚 Sorodna dokumentacija {#related-documentation}

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — kontrolni točki, sinhronizacije s časovno omejitvijo
- [Rate Limit Handling](RateLimitHandling.md) — kvote pasovne širine in samodejno nadaljevanje
- [Access Logging](Logs.md) — kdo je kaj naredil, vključno z dejavnostmi oprav
- [Batch Restore](MailboxMigration.md) — premikanje e-poštnih sporočil med poštnimi predali
- [Date-Windowed Offload](Offload.md) — naloge za premikanje (offload)
- [Docker Compose Logs](DockerComposeLogs.md) — branje dnevnika aplikacije
