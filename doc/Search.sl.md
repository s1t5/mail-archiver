# 🔍 Iskanje pošte {#mail-search}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Iskanje je srce arhive e-pošte. Mail Archiver išče po **celotnem besedilu**
vaše arhivirane pošte — z 제목, vsebino, pošiljateljem in prejemniki — skozi vse
račune, do katerih imate dostop, in rezultate vrne v nekaj milisekundah, tudi pri
arhivih z milijoni sporočil.

Ta vodič pokriva vmesnik za iskanje, sintakso poizvedb, filtre, sortiranje,
stranjenje in postopke, ko morate ukrepati na setu rezultatov.

> **ℹ️ Iskanje vidi le tisto, kar lahko vidite vi.** Rezultate filtrira vaša
> dovoljenja računa. Neupravitelj lahko vedno najde le pošto iz poštovnih
> nabiralnikov, ki so mu dodeljeni — omejitev se uporabi v samem poizvedbi,
> ne v vmesniku.

## 🚀 Dostop do iskanja {#accessing-search}

1. Prijavite se in v glavnem meniju izberite **Archive**
2. Vsebinski vrstica za iskanje se nahaja nad seznamom rezultatov

Odpiranje arhive brez iskalnega izrazu prikazuje vaša najsodobnejša e-poštna sporočila,
najprej nova. Od tam lahko brskate ali se zoščite s filtri.

## 🎯 Vmesnik za iskanje {#the-search-interface}

| Polje | Kaj počne |
|---|---|
| **Search Term** | Poizvedba po celotnem besedilu — glejte [Query Syntax](#-query-syntax) spodaj |
| **From** | Le e-pošta, poslana na ta datum ali kasneje |
| **To** | Le e-pošta, poslana na ta datum ali prej |
| **Direction** | `All`, `Incoming` ali `Outgoing` |
| **Account** | Omejite na en e-poštni račun |
| **Folders** | Omejite na eno mapo preko drevesa map |
| **Search** | Zažene poizvedbo |
| **Reset** | Počisti vse filtre |

Iskalni izraz ujema **naslov, vsebino, pošiljatelja in prejemnike**
(`From`, `To`, `Cc`, `Bcc`).

### Drevo map {#the-folder-tree}

Mape so prikazane kot **hierarhično drevo** na levi strani, ki zrcali strukturo
map v izvornem nabiralniku — vključno z vključenimi mapami. Kliknite na mapo,
da omejite iskanje njo, in uporabite nadzor za zapiranje/odpiranje, da skrijete
drevo, ko potrebujete prostor na zaslonu.

Izbira mape ne spremeni vašega iskalnega izraza; oba se kombinirata.

## 🧩 Sintaksa poizvedb {#query-syntax}

Sintaksa je namerno preprosta. Vse spodaj je tisto, kar aplikacija
dejansko razume.

### Navadne besede — in zakaj predponi delujejo {#plain-words-and-why-prefixes-work}

Vtipkajte eno ali več besed; vse morajo biti prisotne (iskanje **AND**):

```
invoice january
```

Najde e-poštna sporočila, ki vsebujejo tako `invoice` kot `january` v iskanih poljih.

> **💡 Delne besede se samodejno ujamejo.** Vsaka beseda se obravnava kot
> **predpon**, zato iskanje za `isenb` najde tudi `isenboeck` in `isenböck`,
> `rech` pa najde tudi `Rechnung` in `Rechnungen`. Za iskanje ne potrebujete
> karakterja wildcards — tega ni treba tipkati. To pomeni tudi, da lahko
> zelo kratek izraz vrne veliko rezultatov; glejte [Performance](#-performance-and-large-archives).

Iskanje je **ne glede na velikost črk**. `Invoice`, `invoice` in `INVOICE` so
ista poizvedba.

### Točne fraze — navodila v narekovalnikih {#exact-phrases-quotes}

Besedilo vstavite v dvojne narekovalnike, da zahtevate besede **dbesedno drug z drugim** v tem
vrstnem redu:

```
"important meeting"
```

Brez narekovalnikov bi `important meeting` ujemalo tudi e-pošto, kjer se dve
besedi pojavita v različnih stavkih. Z narekovniki bo ustrezala le neposredna fraza.

### Iskanje po specifičnih poljih {#field-specific-search}

Pred izrazom dodajte ime polja in dvonič, da iščete **le** v tem polju:

| Sintaksa | Išče v |
|---|---|
| `subject:invoice` | Le v naslovu (Subject) |
| `from:john` | Le v pošiljatelju (Sender) |
| `to:example.com` | Le v prejemnikih (Recipients) |
| `body:important` | Le v vsebini (Body) |

Imena polj niso odvisna od velikosti črk (deluje tudi `Subject:`), vsako lahko pa
kombinirate z narekovniki za frazo:

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ Podprta so štirje polja:** `subject`, `body`, `from`, `to`. Zapisovanje
> `cc:someone` ali `bcc:someone` **ni** iskanje po polju — izraz se obravnava
> kot navadno besedilo in se išče po vseh poljih. Če želite najti prejemnika,
> ki je bil le v Cc, poiščite naslov ali ime brez predpona.

### Kombiniranje vsega {#combining-everything}

Filtri in izrazi se seštevajo. Ta poizvedba najde e-pošto od katerega koli pošiljatelja zavarovalne družbe,
z frazo "car insurance" v vsebini in "invoice" v
naslovu:

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

Praktične kombinacije:

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### Karakterji, ki se ignorirajo {#characters-that-are-ignored}

Analizator poizvedb odstrani karakterje `& | ! ( ) : *` iz posameznih izrazov,
ker imajo pomen v osnovnem motorju za iskanje po celotnem besedilu. Če vaše iskanje
ne vrne ničesar nenavadnega, preverite, ali je razlog poseben karakter.

## 📊 Sortiranje in stranjenje {#sorting-and-paging}

### Sortiranje {#sorting}

Kliknite glavo stolpca za sortiranje. Sortabilni stolpci:

| Stolpec | Sortira po |
|---|---|
| **Subject** | Naslov, po abecedi |
| **From** | Pošiljatelj |
| **To** | Prejemniki |
| **Date** | Datum pošiljanja |

Ponovni klik na isto glavo obrne smer. Privzeto je **najprej najnovejše**.

### Velikost strani {#page-size}

Izberite, koliko rezultatov želite prikazati na strani:

**20** (privzeto) · **50** · **75** · **100** · **150**

Večja stran prikazuje več konteksta, vendar se počasneje osvežuje. Uporabite
nadzor **Previous** / **Next** za premikanje po straneh rezultatov; število rezultatov
vam pove, koliko e-poštnih sporočil je bilo ujemljeno v celoti.

> **ℹ️ Vaša časovna zona velja za prikazane datume.** Prikazani časi so
> pretvorjeni iz UTC v vašo nastavljeno časovno cono, tako da datum, ki ga vidite, ustreza
> vašemu lokalnemu koledarju — uporabno, ko iščete okoli polnoči.

## 👁️ Delo z rezultati {#working-with-results}

Vsak vrstica prikazuje račun, pošiljatelja, naslov, datum in **ikono 📎 spise (paperclip)**,
ko ima e-poštno sporočilo priloge. Spica je le indikator — ni
filter.

Kliknite **Details**, da odprete e-poštno sporočilo:

- Celotna vsebina (HTML ali plain text)
- **Attachments**, na seznamu z možnostjo prenosa vsakega posameznega
- **Download as EML** — celotno sporočilo za uporabo drugje
- **Restore** — kopiranje e-pošte nazaj v živejočni nabiralnik

Za vračanje e-pošte v nabiralnik poglejte [Mailbox Migration](MailboxMigration.md).

## ✅ Ukrepanje na več rezultatih hkrati {#acting-on-many-results-at-once}

Rezultate lahko izberete in obdelate v množini. Vklopite **Selection Mode**, da dobite
potrditvena polja (checkboxes), nato pa:

- **Select All** / **Deselect All** za trenutno stran
- **Select all {n} matching** — vsak rezultat trenutnega iskanja skozi vse
  strani (prikaže se, ko rezultati prehajajo več kot eno stran)
- **Copy Selected** — vrnite v nabiralnik
- **Export Selected** — kot MBOX ali EML
- **Delete Selected** — iz arhive

Števec prikazuje *"{selected} od {total} izbranih"* med delom.

### Meja izbire {#the-selection-limit}

> **⚠️ Hkrati lahko izberete največ 250 e-poštnih sporočil** (nastavljivo preko
> `Selection:MaxSelectableEmails`). Če presežete to, vmesnik izpiše *"Maximum
> selections reached (250)"* in **Too Many — Split Required**.

Ta omejitev obstaja, ker mora vsaka operacija obdržati vključene sporočile.
Spoznavalnik *"Split larger operations into multiple batches"* je predviden način reševanja:
delajte z rezultati v skupinah po 250 ali manj.

**Select all {n} matching** upošteva isto omejitev. Ko iskanje ujame več
e-poštnih sporočil kot dovoljuje omejitev, izbere prva v trenutnem vrstnem redu
in prikaže obvestilo *"Maximum selections reached"*. Zoščite iskanje (časovni obod,
mapa, račun), da ciljate preostale.

Poskus presežanja izpiše: *"Too many emails selected. Maximum allowed is
250 emails. Please reduce your selection or split into multiple operations."*

### Brisanje vsakega ujemljajočega filtra {#deleting-every-match-of-a-filter}

Gumb **Delete all matches** poleg *Reset* izbriše vsako e-poštno sporočilo,
ki ustreza trenutnemu iskanju, ne le trenutno stran ali izbiro. Na voljo je
upraviteljem in samogospodovalcem in se izvaja kot opravilo v ozadju, zato
omejitev izbire 250 sporočil ne velja.

Potrditveni dialog prikazuje, koliko sporočil je lahko izbrisanih in koliko je
zaklenjenih zaradi politike zadrževanja. Da bi omogočili potrditev, morate vtipkati
to številko. Strežnik ponovno prešteje in prekine, če se je set rezultatov medtem
spremenil, na primer ker je sinhronizacija dodala pošto.

Zaklenjena sporočila s tem dejanjem nikoli ne izbriše; v statusu opravila so
poročena kot preskočena. `DeletionPolicy:MaxBulkDeleteEmails` (privzeto 100.000) omejuje eno
opravilo. Filter, ki ujame več e-poštnih sporočil, je zavrnjen z zahtevo za zoščivanje.

Opravilo se pojavi na strani [Background Jobs](BackgroundJobs.md) in ga je tam mogoče
prekiniti.

### Neposredna obdelava vs. opravilo v ozadju {#direct-processing-vs-background-job}

Za **Copy** in **Export** izberete način izvajanja dela:

| Možnost | Obnašanje |
|---|---|
| **Process on this page (wait for completion)** | Браuzer čaka; dobro za nekaj sporočil |
| **Process in background (monitor separately)** | Teče kot opravilo; lahko zaprete bralnik |

Izvozi (Exports) vedno tečejo v ozadju:

> *"Izvoz bo tečel v ozadju in rezultat boste lahko prenesli z
> strani Jobs, ko bo končan."*

Teča in končana opravila najdete na strani [Background Jobs](BackgroundJobs.md).

> **⚠️ Brisanje je trajno.** Potrditev je eksplicitna: *"Ali ste prepričani,
> da želite trajno izbrisati {n} izbranih e-poštnih sporočil? To dejanje je
> neodvrnitevno in vsako brisanje bo zabeleženo za potrebe revizije."* Brisanja so
> zabeležena v dnevniku dostopa.

## ⚡ Zmogljivost in velike arhive {#performance-and-large-archives}

Iskanje temelji na PostgreSQL indeksu celotnega besedila, zato je razlika med
hitrim in počasnim iskanjem običajno v tem, kako **selektiven** je poizvedba.

**Naredite:**

- Dodajte **časovni obod**, ko približno veste, kdaj je e-pošta prišla — to je
  najučinkovitejši način zoščiranja
- Omejite na **račun** ali **mapo**, ko veste, kje se pošta nahaja
- Uporabite **več besed** namesto ene zelo kratke
- Uporabite iskanje po poljih (`subject:`, `from:`), da izključite nepovezane ujeme

**Izogibajte se:**

- Enemu kratkem predponu, kot je `re` — kot predpon ujame ogromen del
  vaše arhive
- Zelo širokim časovnim obodom v kombinaciji z pogosto besedo
- Brskanju skozi številne strani, ko bi lahko namesto tega zoščili iskanje

> **💡 Če je iskanje počasno, je odgovor običajno časovni obod.** Zoščanje na
> en mesec pogosto spremeni večsekundno iskanje v instantno iskanje, ker lahko
> indeks preskoči večino arhive.

## ❓ Reševanje težav {#troubleshooting}

| Simptom | Vzrok | Kaj storiti |
|---|---|---|
| Ni rezultatov, vendar jih pričakujete | Preveč specifičen izraz ali tipkarska napaka | Poskusite z manj besed; preverite pravopis |
| `cc:name` ne najde nič koristnega | `cc` ni podprto polje | Poiščite ime brez predpona |
| Poseben karakter prekine poizvedbo | `& \| ! ( ) : *` se odstranijo | Odstranite ali zamenjajte karakter |
| Preveč rezultatov | Pogosta beseda ali kratek predpon | Dodajte časovni obod ali iskanje po polju |
| *"Too Many — Split Required"* | Izbrano več kot 250 sporočil | Zmanjšajte izbiro ali razdelite operacijo |
| Izvoza ni na voljo | Teče kot opravilo v ozadju | Preverite [Background Jobs](BackgroundJobs.md) |
| Rezultati se zdi manjkajoči | Do dovoljenja računa | Potrdite, da je nabiralnik dodeljen vašemu uporabniku |
| Datumi so premaknjeni | Pretvorba časovne cone | Preverite nastavitve časovne cone |

## 📚 Sorodna dokumentacija {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — vračanje rezultatov v nabiralnik
- [Background Jobs](BackgroundJobs.md) — spremljanje izvozov in vračanj
- [Access Logging](Logs.md) — revizijska sled iskanj in brisanj
- [REST API](API.md) — ista arhiva preko API-ja
- [MCP Server](MCP.md) — iskanje preko AI agenta
- [Retention Policies](RetentionPolicies.md) — ko e-pošta zapusti arhivu
