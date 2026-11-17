# 📧 Shranjevanje originalnega sporočila {#original-message-storage}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Privzeto arhiv ohranja **razčlenjene dele** e-poštnega sporočila: glave, besedilno in HTML vsebino ter priloge. Izvoženi `.eml` je znotraj teh polj znova sestavljen.

To je dovolj za branje e-poštnega sporočila, vendar sporočilo ni tisto, **kot je bilo prejeto**. Prenosni kodiranja, MIME meje in prepogibanje glav se razlikujejo od originala, zato ponovno sestavljeno sporočilo ne potrdi več svoje DKIM podpisa in ga filtri za spam ocenijo drugače.

Z vklopljenim shranjevanjem originalnega sporočila arhiv poleg razčlenjenih polj ohranja celotno sporočilo, kot je prišlo. Shranjeni original je tisto, kar lahko arhiv dokaže, da je bilo dostavljeno.

| | Le razčlenjena polja | S shranjenim originalom |
| --- | --- | --- |
| Glave, vsebina, priloge | ✅ | ✅ |
| Bitno natančno sporočilo | ❌ | ✅ |
| DKIM podpis se potrdi | ❌ | ✅ |
| Forenzična uporaba (sod, revizija) | omejena | ✅ |

## 🎯 Kje se posluži original {#where-the-original-is-served}

Ko je original shranjen, se vrne kjerkoli se pričakuje celotno sporočilo:

| Kraj | Kaj se zgodi |
| --- | --- |
| gumb **Export EML** na strani s podrobnostmi e-pošte | Posluži shranjeni original, če obstaja; če e-pošte ni shranjena s tem, se uporabi rekonstrukcija. |
| **Restore to a mailbox** | Dodaja shranjeni original, če obstaja; če e-pošte ni shranjena s tem, se uporabi rekonstrukcija. |
| `GET /api/v1/emails/{id}/raw` | Prenos prek REST API, poglejte [REST API guide](API.md#download-original-message). |
| `get_email_raw` | MCP orodje, poglejte [MCP guide](MCP.md#get_email_raw). |

Odgovor vam pove, katero obliko ste prejeli, zato lahko klicatelj prepozna pravi original od rekonstrukcije brez pregledovanja bajtov:

| Signal | Pom meaning |
| --- | --- |
| `X-Raw-Message-Source: imap` | Natančna kopija z IMAP strežnika (`BODY[]`, kot je bila dostavljena). |
| `X-Raw-Message-Source: graph` | Natančna kopija, ki jo je vrnil Exchange z MIME vsebino. |
| `X-Raw-Message-Source: eml-file` | Natančna kopija, vsebina uvozbene datoteke `.eml`. |
| `X-Raw-Message-Source: serialized` | Parsirano sporočilo, ki je bilo ponovno zapisano. Identično, razen ker so končnice vrnic CRLF (uvoz MBox, pridobitev pri IMAP obnovi). |
| `X-Raw-Message-Sha256` | SHA-256 shranjenega originala. |
| `X-Raw-Message-Reconstructed: true` | Original ni shranjen; sporočilo je bilo zgrajeno ponovno. Brez kontrolne vrednosti (checksum). |
| `X-Raw-Message-Storage` | Backend, iz katerega je bil original prebran (`postgres` ali `filesystem`). |

Tudi ime datoteke to pove: `email-{id}-original-...eml` za shranjen original, `email-{id}-...eml` za rekonstrukcijo.

Ko shranjeni original **ne ustreza svoji kontrolni vrednosti (checksum)**, sta izvoz in obnovitev zavrbljena z napako. Namesto tega bi vrnitev rekonstrukcije prikrila dejstvo, da je bila arhivirana kopija spremenjena, kar je ravno tisto, česar arhiv nikoli ne sme storiti.

Obnovitev (restore) prebere shranjeni original in ga doda prek ponudnikovega odjemalca, zato poštni predal prejme originalne glavice in MIME strukturo. Natančen tok bajtov v omrežju ni zagotovljen, ker odjemalec sporočilo ponovno serializira; sama arhivirana kopija pa ostane nespremenjena.

## ⚙️ Nastavitve {#settings}

Vse nastavitve se nahajajo v razdelku `RawMessages` ali pa so na voljo kot spremenljivke okolja z prefixedom `RawMessages__`.

| Nastavitev | Z умолчанию | Opis |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Shranite originalno sporočilo vsakega novega arhiviranega e-poštnega sporočila. E-poštna sporočila, arhivirana pred vklopljitvijo te nastavitve, nimajo originala. |
| `RawMessages:MaxMessageSizeMb` | `50` | Sporočila, večja od te vrednosti, se arhivirajo brez originala. `0` = brez omejitve. |
| `RawMessages:IncludeGraph` | `true` | Shranite tudi original za Microsoft 365 (Graph) račune. To zahteva en dodatni Graph zahtev na sporočilo, kar vpliva na omejitev hitrosti (throttling budget). |
| `RawMessages:Storage` | `postgres` | Kje se shranijo bajti sporočila: `postgres` (v bazi podatkov) ali `filesystem` (gzip datoteka poleg arhive). Poglejte [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Direktorij, kamor se pišejo datoteke sporočil, relativno do korenine vsebine aplikacije. To usmerite na montiran volumen v Dockerju. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Število ravni direktorijev z dvema znakoma, na katere je razporedil ID e-poštnega sporočila. `2` zagotavlja 10.000 direktorijev, `0` je ravna struktura. To ohranja nizko število datotek na direktorij pri velikih arhivih. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Ponovno izračunaj SHA-256 datoteke ob vsakem prenosu in ga primerjaj s kontrolo vrednosti (checksum), zabeleženo ob arhiviranju. Neujemanje povzroči napako pri prenosu z HTTP 500 namesto serviranja spremenjenih bajtov. |

### Velikost {#sizing}

Shramba raste približno za velikost vsakega e-poštnega sporočila, vključno z njegovimi prilogami, v gzip-komprimirani obliki. Priloge **niso** deduplicirane znotraj shranjenega originala, zato e-poštno sporočilo z veliko prilogo stane to prilogo dvakrat: enkrat v deduplicirani shrambi prilog in enkrat znotraj originala. Planirajte približno **dvakrat** večjo velikost baze podatkov kot pri arhivu brez originalov.

Omogočanje nastavitve ne popravi obstoječe pošte. Le nove arhivirane e-poštne sporočilo dobijo original.

## 💾 Shranilni način (Storage backend) {#storage-backend}

Nastavitev `Storage` določa, kje se nahajajo bajti. **Metadata vrstica vedno ostane v
bazi podatkov**, ne glede na to, kateri backend je konfiguriran, in vsežuje SHA-256, velikost,
vir zajema in (za datoteke) relativno pot. Prav ta vrstica omogoča arhivu, da dokaže,
kaj bi arhivirana sporočilo morala vsebovati.

### postgres (default) {#postgres-default}

Komprimirano sporočilo se nahaja v stolpcu `Content` v `mail_archiver."RawMessages"`.
To je vedenje, ki ga je arhiv vedno imel, in ne zahteva dodatnega volumena.

Izberite to, ko je baza podatkov že vaša enota za varnostno kopiranje in želite imeti
eno stvar, ki jo boste kopirali in obnovili.

### filesystem {#filesystem}

Sporočilo je zapisano v `{BasePath}/{xx}/{yy}/{id}.eml.gz`, kjer sta `xx`/`yy` izvedena
iz e-poštnega id. Vrstica v bazi podatkov ohranja le metapodatke.

Izberite to, ko je arhiv dovolj velik, da bi originali prevladovali nad velikostjo
baze podatkov, ali ko želite datoteke sporočil postaviti na cenejš ali ločen shranjevalni prostor.

!!! danger "Mount a volume in Docker"
    Privzeto `BasePath` (`RawMessages`) se razreši v `/app/RawMessages` **znotraj
    kontejnerja**. Brez bind mounta ta direktorij pripada zapisljivemu sloju kontejnerja,
    tako da vsak ponovno ustvarjen kontejner - vključno s `docker compose down && docker compose
    up -d`, posodobitvijo slike ali ponovnim zagonom gostitelja z začasnim slojem -
    vzame vse shranjene originale s seboj, medtem ko vrstice v bazi podatkov preživijo.

    Pri uporabi filesystem backenda vedno povežite direktorij na gostitelja:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Pot na gostitelju (tukaj `./raw-messages`) je tisto, kar kopirate. Pot v kontejnerju mora
    ustrezati `RawMessages:FileSystem:BasePath` ali pa nastavite `RawMessages:FileSystem:BasePath` na
    točko povezave (mount point), ki ste jo izbrali. Če premaknete mount, ohranite `BasePath`,
    ki kaže nanj: shranjene vrednosti `StoragePath` so relativne, zato ostanejo razrešljive.

### Preklop backenda {#switching-backends}

Brandenje sledi shranjevanju, zabeleženemu v vsaki vrstici, ne pa konfigurirani nastavitvi.
Ko spremenite `Storage`, bodo prej arhivirane sporočila ostala prenosljiva od koderkoli so.
Nastavitev vpliva le na **novo** arhivirano pošto.

Za premikanje že shranjenih podatkov zaženite migracijo shranjev}}. Za celoten pregled
poglejte [CLI commands](CLI.md#moving-stored-original-messages):

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Kaj migracija zagotavlja:

- Vsako sporočilo se pred kopiranjem preveri glede na njegov zabeležen kontrolni zmisl (checksum).
  Sporočilo, ki ne uspe, ostane tam, kjer je, in je prijavljeno: poškodovan original nikoli ne
  propagira in njegova edina preostala kopija nikoli ne biti izbrisana.
- Datoteka se izbriše šele po tem, ko je transakcija baze podatkov, ki jo je premaknila,
  potrdljena (committed), tako da prekinjen proces ne more izgubiti edine kopije.
- Proces je lahko nadalčeval. Delo je izbrano s stolpcem `StorageType`, zato z ponovnim zagonom
  ukazov nadaljuje s tistim, kar še ni bilo opravljeno.
- Izstopna koda je `1`, če katero sporočilo ne uspe, sicer `0`.

Migracija ni obvezna za delovanje arhiva. Potrebna je le za premikanje obstoječih
bajt na drugi backend.

## 🛟 Varnovanje in obnovitev {#backup-and-restore}

=== "postgres backend"
    Ni dodatnothing. Originali so del baze podatkov in so zajeti s standardnim
    izvozom (dump), poglejte [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Zaradi varnosti izvezite direktorij `BasePath` **skupaj z** izvozom baze podatkov.

    Obnovitev baze podatkov brez datotek povzroči, da arhiv vsebuje vrstice, čwartor
    vsebina katerih ni na voljo. Prenos bo v tem primeru vrnil napako HTTP 404 namesto
    napačnih podatkov, samo e-poštna sporočila pa bodo ostala berljiva iz svojih razčlenjenih polj.

## 🗑️ Brisanje e-pošte {#deleting-an-email}

Brisanje e-pošte z njenim izbrisanjem odstrani tudi original: pravila o shranjevanju, ročno brisanje posameznih sporočil in brisanje v množini ter brisanje celotnega e-poštnega računa vsi odstranijo datoteko kot del iste operacije. S `postgres` backendom ni treba početi nič dodatnega, saj bajti potujejo znotraj vrstice, ki jo kaskadna brisanje že odstrani.

Datoteka je izbrisana **šele po** potrditvi (commit) vrstice e-pošte, nikoli prej. Ta dva koraka ne moreta biti del ene transakcije, ker datoteka ni del baze podatkov, zato mora eden od njima priti drug v redu. Ta vrteno red je varen: brisanje, ki ne uspe ali se prekiné (roll back), na primer pri zakleni zapisu, pusti original neobdelanega, če pa pride do okvare med obema korakoma, ostane datoteka brez vrstice, ki jo spodnji proces čiščenja (sweep) spet osvobodi. Nasprotni red bi uničil original e-pošte, ki še vedno obstaja.

**Osamljene datoteke so zbrane.** Vse, kar je ušlo, vključno s datotekami, ki so jih pustila brisanja pred uvedbo tega čiščenja, odstrani periodično čiščenje `BasePath`, ki primerja vsako datoteko z vrsticami in izbriše tiste, na katere se nobena e-pošta ne nanaša:

| Nastavitev | Z умолчанию | Pomen |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Vklopi ali izklopi čiščenje |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Kako pogosto se izvaja |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Datotek, mlajših od tega, se nikoli ne dotikajo |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Zamika pred prvim zagonom, da se najprej zaključi tekoča migracija shranjevanja |

Čas grace period naredi čiščenje varnega: uvoz zapiše datoteko, preden potrdi vrstico, ki na njo kaže, datoteka, zapisana pred nekaj trenutki, pa ne sme biti zamenjana za osamljeno. Polovičeno zapisane `.tmp` datoteke zaradi prekinjenega zapisa se zberejo po istem pravilu. S `postgres` backendom čiščenje nič ne naredi in celo ne pregleduje direktorija.

## 🔍 Preverjanje arhive {#verifying-an-archive}

Kontrolna vrednost (checksum), zabeležena ob ustvarjanju arhive, omogoča preverjanje.

- **Ob prenosu** (`VerifyChecksumOnRead=true`, privzeto) se datoteka izračuna njezina hash vrednost in primerja. Če se vrednosti ne ujemajo, se prenos zavrne z napako HTTP 500.
- **Pri migraciji shrambe** se vsako sporočilo preveri, preden se kopira, v obeh smerih.

Ker se vrstica metapodatkov nahaja v bazi podatkov, byte pa lahko so na disku, je primerjava kontrolnih vrednosti edini način, da ugotovimo, da je bila datoteka spremenjena zunaj aplikacije.

## ⚠️ Te stvari morate vedeti {#things-to-know}

- **Omogočanje ne vključuje nazajjšanja podatkov (backfill).** Le poštni sporočilo, arhivirano po preklopu, dobi original.
- **Manjka original se zabeleži kot napaka.** Ko je zajem zavrnjen (spodnje sporočilo `MaxMessageSizeMb`, zajem ni uporaben) ali ko zapis v konfigurirano pomembno enoto (backend) ne uspe, log vsebuje napako z identifikatorjem e-pošte in razlogom. E-pošta ostane v arhivu brez svojega originala; ni samodejnega nazajšanja in ni ponovnih poskusov.
- **Rekonstrukcija ni sporočilo.** Za e-poštna sporočila brez shranjenega originala vsaka pot prenosu vrne znova zgrajeno sporočilo in to navede prek `X-Raw-Message-Reconstructed: true`.
- **Datoteka (filesystem) pomeni dve enoti za varnostno kopiranje.** Baza podatkov in direktorij s sporočili morata biti obnovljena skupaj, sicer vrstice preživijo brez vsebine.
- **`VerifyChecksumOnRead` porabi CPU.** Vsak prenos velike e-poštne sporočilo se dekomprimira in zhashira. Funkcijo pustite vklopljeno, razen če ste izmerili težavo; to je edina zaščita pred tiho spremembo na disku.
- **Shranjeni original ni dedupliciran.** Priloga, ki se pojavi v številnih e-poštnih sporočilih, se v shrambi prilog shrani enkrat, vendar enkrat za vsako e-poštno sporočilo znotraj originala.
- **Izbrisanje odstrani tudi original.** E-poštno sporočilo, ki ga izbriše politika zadržanja podatkov ali ročno, odvzame tudi svoj shranjeni original, datoteko in vrstico. Nič ne ostane v arhivu, če ga arhiv ne vsebuje več na seznamu.

## 🔗 Poglej tudi {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - celotna migracija shrambe
- [Setup and Parameters](Setup.md) - vse parametre konfiguracije
- [REST API](API.md#download-original-message) - `/raw` končališče
- [MCP Server](MCP.md#get_email_raw) - orodje `get_email_raw`
- [Backup and Restore](BackupRestore.md) - kaj z amounts za varnostno kopiranje
- [Attachment Deduplication](AttachmentDeduplication.md) - kako so shranjene priloge
