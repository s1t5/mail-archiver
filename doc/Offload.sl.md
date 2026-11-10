# 📤 Date-Windowed Offload {#date-windowed-offload}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Offload funkcija doda arhivirano pošto, ki je novejše od določenega časovnega okna, v drug poštni predal, v arhiv pa pusti vse, kar je starejše. Uporablja se za migracije, kjer naj bo novi poštni predal vseboval le nedavne pošte, medtem ko ostane polna zgodovina dostopna prek Mail Archiverja.

Od [Copy All Emails to Another Mailbox](MailboxMigration.md) se razlikuje na pet načinov:

| | Copy All Emails | Offload |
|---|---|---|
| Filter datuma | nobena, vse ali nič | cutoff, s poljubnim zgornjim mejami |
| Ponovljivost | ne, ponovno doda vse | da, preskoči tisto, kar cilj že vsebuje |
| Izključitev map | ne | da, konfigurabilno |
| Preimenovanje map | ne | da, konfigurabilno |
| Predogled | ne | dry run poročila po mapah brez pisanja |

Offload vedno le dodaja v cilj. Nikoli ne briše iz arhivov in nikoli ne posega v izvorni poštni predal.

## 🔁 Ponovitev teka je varna {#repeating-a-run-is-safe}

Pred prvim dodajanjem se celotni ciljni poštni predal indeksira in vsaka sporočilo se preveri glede na njega. Ponovljen tek zato ne doda ničesar, kar je že prišlo, temveč to poročila kot že prisotno. To ima tri praktične posledice:

- Prekinjen tek se obnovi preprosto s ponovnim zagonom.
- Končan tek lahko ponovite kot preverovalni prehod. Pravilen rezultat je, da ni nič dodano in vse že vsebuje.
- Sporočila, izbrisana v cilju, se pri naslednjem teku vrnejo, ker jih ni več v indeksu.

O tem, ali je sporočilo že prisotno, odločata dva kriterija v tem vrstnem redu:

1. **Message-ID**, normaliziran. To opravi skoraj ves delo.
2. **Prstni odtis (fingerprint)** po pošiljatelju, prejemnikih, zadevi in času pošiljanja v okviru dveh sekund. To pokriva sporočila, katerih shranjen Message-ID ni prisoten ali je malformiran. Takšnih sporočil ni mogoče obnoviti z njihove originalno Message-ID, zato bi se brez tega drugega kriterija pri vsakem samostojnem teku ponovila.

Zadatak poročila, koliko ujemanj je prišlo iz prstnega odtisa posebej, ker gre za kriterij, ki lahko tiho zavede.

## ⚙️ Konfiguracija {#configuration}

Novi razdelek `Offload` z privzetimi nastavitvami, ki ponavljajo prejšnje vedenje:

```json
"Offload": {
  "MaxConcurrentJobs": 1,
  "PrefetchMaxMessages": 500000,
  "ExcludedSourceFolders": [],
  "FolderRenameMap": {},
  "MarkAsSeen": true
}
```

| Ključ | Privzeto | Namen |
|---|---|---|
| `MaxConcurrentJobs` | `1` | Koliko oprav offload ali obnovitvenih oprav lahko teče hkrati. Ena ohranja strogo serijsko vedenje, ki ga je vrstica oprav v vedno imela. Največ ena oprav na ciljni poštni predal teče ne glede na to vrednost. |
| `PrefetchMaxMessages` | `500000` | Zgornja meja, koliko sporočil se indeksira iz ciljnega poštnega predala. Nad njo se preverjanje duplikatov zoži na eno mapo in zabeleži, da je to storilo. |
| `ExcludedSourceFolders` | empty | Izvorni folderji, ki se nikoli ne offloadujejo. Preverjeno pred preimenovanjem in zajema podmape. |
| `FolderRenameMap` | empty | Prepiše začetne dele poti izvorne mape. |
| `MarkAsSeen` | `true` | Ali je dodana pošta označena kot prebrana. |

Oba nastavitva map sta namerno prazni. Prepisovanje ali izbrisovanje map brez vprašanja bi presenetilo vsakogar, ki že uporablja pot za obnovo, zato zagon brez konfiguracije migrira vse, vključno s spam mapami, in ustvari drugi nabor posebnih map poleg tistih v cilju.

### Izključitev in preimenovanje map {#folder-exclusions-and-renaming}

Za migracijo iz Exchange v Dovecot sta nastavitvi običajno takšni:

```json
"ExcludedSourceFolders": ["Junk E-Mail", "Deleted Items"],
"FolderRenameMap": {
  "Sent Items": "Sent",
  "Deleted Items": "Trash",
  "Junk E-Mail": "Junk"
}
```

Brez mape za preimenovanje vsak migriran poštni predal konča z `Sent` in `Sent Items`, ker Dovecot vnaprej ustvari svoje posebne (special-use) mape.

O tem, kako se te uporabijo, je vredno vedeti štiri stvari, saj je vsaka od njih način, da vas nekaj preseneti:

1. **Izključitve se najprej ujemajo** z imenom izvora. Če bi se preimenovanje zgodilo najprej, izključitev za `Deleted Items` nikoli ne bi 작동ala, ko bi že postala `Trash`.
2. **Preimenovanje prepiše najdaljšo ujemajočo pot** na mejah segmentov. `Sent Items` postane `Sent`, kar tudi spremeni `Sent Items/2019` v `Sent/2019`, in pusti `Sent Items Archive` brez sprememb.
3. **Ujemanje je neobčutno na velike črke**, pri čemer veljata tako `/` kot `\` kot ločilniki.
4. **Dve izvorni mapi se lahko združita v eno ciljno.** To, da postaneta `Sent Items` in `Sent` obe `Sent`, je v redu in ne odpre ciljne mape dvakrat.

Sprememba mape za preimenovanje med dve teki ne ustvari duplikatov, saj preverjanje duplikatov pokriva celotni ciljni poštni predal namesto ene razžebrane mape.

## 🖥️ Zagon prek vmesnika {#running-it-from-the-interface}

Na strani podrobnosti računa **Offload to Another Mailbox** odpre obrazec s ciljnim poštnim predalom, ciljno korensko mapo, možnost ohranjanja strukture map, časovnim oknom, stikom za dry run in možnostjo označevanja dodane pošte kot prebrane. Konfigurirane izključitve in mapa za preimenovanje so prikazane samo za branje, zato zagon nikoli ni presenečenje.

Zadatak se doda v vrstico, njegov napredek in poročilo po mapah sta vidna na strani statusa oprav in pod **Jobs**. Poročilo po mapah navaja izvorne mape, zato je vidno le uporabniku, ki je začel oprav, in administratorjem.

Ciljni poštni predal je lahko onemogočen IMAP račun, npr. za pripravo cilja migracije, preden postane dostopen; zagon neuspešno konča z napako pri povezovanju, če je cilj nedostopen. Računi Microsoft 365 ne morejo biti cilji za offload; pot za obnovo prek Grapha ostaja nespremenjena.

### Kdo lahko izvaja opravo {#who-may-run-it}

Administratorji lahko izvajajo offload med katerimi koli dvema računoma. Samostojni upravitelj (self-manager) lahko izvaja offload **le med računi, ki so mu dodeleni**, na obeh straneh: na izvoru, ker je stran računa že omejena na to, in na cilju, ker je seznam ciljev omejen na isto množico in se zahteva ponovno preveri ob zagonu oprav. Samostojni upravitelj z enim dodeljenim računom zato nima primernega cilja in je o tem obvestil.

Omejitev prihaja iz `IAccountAccessResolver`, istega ločevalca, ki ga uporabljata REST API in MCP strežnik, zato obstaja ena definicija tega, kdo lahko uporablja kateri poštni predal. Odločitev sama se nahaja v `OffloadTargetEligibility`, ki jo kličeta tako seznam ciljev kot preverjanje zahtev, zato se ponujeno v obrazcu in sprejeto na strežniku ne moreta razlikovati.

## ⌨️ Zagon iz vrstice ukazov {#running-it-from-the-command-line}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll \
  --offload --source-account-id 3 --target-account-id 7 \
  --since 2025-08-01 \
  [--until 2026-08-01] [--target-folder INBOX] \
  [--preserve-folders] [--dry-run] [--no-mark-seen]
```

| Argument | Pomen |
|---|---|
| `--source-account-id` | Račun, iz katerega se bere arhivirana pošta. Obvezno. |
| `--target-account-id` | IMAP račun, kamor se dodaja. Obvezno in mora biti drugačen od izvora. |
| `--since` | Vključna spodnja meja datuma pošiljanja, `YYYY-MM-DD`. Obvezno. |
| `--until` | Prostovoljna vključna zgornja meja. |
| `--target-folder` | Korensko mapo v ciljnem poštnem predalu. Privzeto je `INBOX`. |
| `--preserve-folders` | Ponovno ustvari strukturo izvornih map pod korenino. |
| `--dry-run` | Poroča o tem, kaj bi se zgodilo, ne doda ničesar. |
| `--no-mark-seen` | Dodaj brez oznake Seen. |

Exit kodi:

| Koda | Pomen |
|---|---|
| `0` | Vse je bilo dodano ali je že prisotno. |
| `1` | Vsajena sporočilo je neuspelo ali pa je neuspešen bil sam tek. |
| `2` | Klic je bil napačen: napačni ali manjkajoči argumenti, neznan račun, cilj ni IMAP, vir je enak cilju. |

Ena izvedba obdeluje en poštni predal, kar omogoča programiranje celotne flote poštnih predal iz CSV datoteke s viri, cilji in cutoff-om.

> **Zagon večih hkrati.** Pravilo enega oprava na cilj obstaja le znotraj enega procesa, zato lahko dve hkrati zrazni izvedbi v vrstici ukazov proti *istemu* ciljnemu poštnemu predalu vsaka pred začetkom pisanja vzameta svoj indeks duplikatov in lahko oba dodata isto pošto. Razdelite delo tako, da ima vsaka hkrati zrazna izvedba drugačen cilj, kar je običajen primer, ko je vsak vrstica v CSV datoteki drug poštni predal.

## 🗓️ Kako se interpretira cutoff {#how-the-cutoff-is-interpreted}

- Filter je na **datumu pošiljanja**, ne na tem, kdaj je bila sporočilo arhivirano. Časovna oznaka arhiviranja je čas izvedbe arhiviranja in nima pomena za cutoff.
- Relativno okno, kot je "zadnjih dvanajst mesecev", se **razreši v fiksni datum ob ustvarjanju oprav**. Oprav lahko teče dolgo in se kasneje ponavlja, mora pa vsakič izbrati isto pošto; shranjeno relativno okno bi se premikalo.
- Cutoff-i se interpretirajo v **konfigurirani časovni coni prikazov**, isti kot na zaslonu za iskanje, spodnja meja pa je vključna od 00:00 dnešnjega dne. Zgornja meja vključuje celoten dan. Relativna okna ("zadnjih N mesecev") se prav tako razrešijo v tej časovni coni prikazov, kar ustreza načinu shranjevanja `SentDate`.

### Znana omejitev {#known-limitation}

- Ko indeks duplikatov doseže `Offload:PrefetchMaxMessages`, se obseg zmanjša in zabeleži, namesto da bi se poskušalo znova z eno mapo. Nadomestna možnost `restrictToFolder`, ki jo ponuja graditelj indeksa, trenutno ni uporabljena v poti offload; povečanje meje je podprto rešitev.

## 📋 Migracija, od začetka do konca {#a-migration-end-to-end}

1. Ustvarite ciljne poštne predale. Za več njih hkrati glejte masovni uvoz računov v [Account Import](Account-Import.md).
2. Konfigurirajte `Offload:ExcludedSourceFolders` in `Offload:FolderRenameMap`. Oba sta privzeto prazna, zato preskok z tega migrira spam mape in ustvari dvojne posebne mape.
3. Potrdite, da je vsak izvorni poštni predal popolnoma arhiviran, preden ga začnete offloadovati.
4. **Preverite vse s dry run.** Preverite števila in razžešene imena ciljnih map, preden karkoli zapišete.
5. Pravi zagon. Začnite s `MaxConcurrentJobs` na vrednosti 2 in jo povečajte šele po opazovanju, kako se ciljni strežnik справlja; Dovecot omejuje hkrati povezave na uporabnika in na IP.
6. Ponovite tek za preverjanje. Pričakujte, da ni nič dodano in da je vse že prisotno.
7. Onemogočite izvorne račune.

Protok je omejen s `BatchOperation:PauseBetweenEmailsMs`, ki je privzeto 50 ms, kar omogoča največ dvajset dodajanj na sekundo, preden se upošteva latenca strežnika.

## 🔍 Kaj pomenijo števniki {#what-the-counters-mean}

| Števec | Pomen |
|---|---|
| Appended | Zapisano v cilj. Pri dry runu je to, kar bi bilo zapisano. |
| Already present | Cilj je že vsebuje, po katerem koli od obeh kriterijih. |
| of those by fingerprint | Koliko je bilo prepoznano s drugim kriterijem namesto z Message-ID. |
| Excluded folder | Preskočeno, ker je izvorna mapa izključena. |
| Failed | Ni bilo mogoče dodati. |

Opozorilo se prijavi, če indeks duplikatov ni pokril celotnega ciljnega poštnega predala, kar se zgodi nad `PrefetchMaxMessages` ali ko mape ni mogoče brati. V tem stanju lahko ponovljen tek doda pošto, ki je že prisotna v delih, ki niso bili indeksirani.

## ⚠️ Opombe in omejitve {#notes-and-limits}

- Obnovljena pošta zdaj nosi svoj **originalni čas dostave** kot IMAP interno datum, ki ga vzame iz verige `Received` sporočila in se, če to ni mogoče, zamenja z datumom pošiljanja. Pred tem je nosila čas izvedbe arhiviranja, kar je uničilo razvrščanje na strani strežnika po prihodu in vse pravilo na podlagi starosti v cilju.
- Ker ponovljen tek preskoči tisto, kar je že prisotno, se napačnega notranjega datuma ni mogoče popraviti s ponovnim zagonom: sporočila so prepoznana in preskočena. To uredite pred produkcijskim zagonom ali pa izbrišite priAfficirana sporočila v cilju in zagonajte znova.
- Microsoft 365 ni podprt kot cilj za offload.
- Zaznavanje duplikatov lahko prepozna le tisto, kar lahko vidi v ciljnem poštnem predalu. Pošto, ki je do cilja prišla po drugi poti, ustreti le toliko, kolikor se ujema s kriterijema.
