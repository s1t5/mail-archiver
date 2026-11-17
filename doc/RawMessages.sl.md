# 📧 Shranjevanje originalne sporočilo {#original-message-storage}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Privzeto arhiv ohranja **razčlenjene dele** e-pošte: glave, besedilno in HTML vsebino ter priloge. Izvoženo `.eml` je zmedicirano iz teh polj.

To je dovolj za branje e-pošte, vendar sporočilo ni **tako, kot je bilo prejeto**. Prenosni kodiranja, MIME meje in zapoganje glav (header folding) se razlikujejo od originala, zato zmedicirano sporočilo ne potrdi več svoje DKIM podpisa in ga filtri za spam ocenijo drugače.

Z vklopljenim shranjevanjem originalnih sporočil arhiv poleg razčlenjenih polj ohranja celotno sporočilo, kot je prišlo. Shranjen original je tisto, česar lahko arhiv dokaže, da je bilo dostavljeno.

| | Le razčlenjena polja | S shranjenim originalom |
| --- | --- | --- |
| Glave, vsebina, priloge | ✅ | ✅ |
| Bitno natančno sporočilo | ❌ | ✅ |
| DKIM podpis se potrdi | ❌ | ✅ |
| Forenzična uporaba (sodišče, revizija) | omejena | ✅ |

## 🎯 Kje se posluži original {#where-the-original-is-served}

Ko je original shranjen, se vrne kadar koli, kjer se pričakuje celovito sporočilo:

| Kraj | Kaj se zgodi |
| --- | --- |
| gumb **Export EML** na strani s podrobnostmi e-pošte | Posluži shranjeni original, če obstaja; če ga ni pri arhiviranih e-poštnih sporočilih, se uporabi rekonstrukcija. |
| **Restore to a mailbox** | Dodaja shranjeni original, če obstaja; če ga ni pri arhiviranih e-poštnih sporočilih, se uporabi rekonstrukcija. |
| `GET /api/v1/emails/{id}/raw` | Prenos prek REST API, poglejte [REST API guide](API.md#download-original-message). |
| `get_email_raw` | MCP orodje, poglejte [MCP guide](MCP.md#get_email_raw). |

Odgovor vam pove, katero obliko ste prejeli, zato lahko klicatelj brez pregleda bajt loči pristni original od rekonstrukcije:

| Signal | Pom meaning |
| --- | --- |
| `X-Raw-Message-Source: imap` | Natančna kopija s strežnika IMAP (`BODY[]`, kot je bila dostavljena). |
| `X-Raw-Message-Source: graph` | Natančna kopija, ki jo je vrnil Exchange MIME vsebnosti. |
| `X-Raw-Message-Source: eml-file` | Natančna kopija, vsebina uvožene datoteke `.eml`. |
| `X-Raw-Message-Source: serialized` | Parsirano sporočilo, ki je bilo ponovno zapisano. Identično, razen ker so končnice vrnic CRLF (uvoz MBox, pridobitev pri IMAP recovery). |
| `X-Raw-Message-Sha256` | SHA-256 shranjenega originala. |
| `X-Raw-Message-Reconstructed: true` | Original ni shranjen; sporočilo je bilo zgrajeno ponovno. Brez kontrolne vrednosti (checksum). |
| `X-Raw-Message-Storage` | Backend, iz katerega je bil original prebran (`postgres` ali `filesystem`). |

Tudi ime datoteke to pove: `email-{id}-original-...eml` za shranjen original, `email-{id}-...eml` za rekonstrukcijo.

Ko shranjeni original **ne opravi kontrole kontrolne vrednosti (checksum)**, se izvoz in ponovno prihranjanje (restore) zavrni z besedilom o napaki. Namesto tega bi vrnitev rekonstrukcije prikrila dejstvo, da je bila arhivirana kopija spremenjena, kar je ravno tisto, česar arhiv nikoli ne sme storiti.

Postopek restore prebere shranjeni original in ga doda prek ponudnikjeve stranke (client), zato poštni predal prejme originalne glave in MIME strukturo. Natančen tok bajt na omrežju ni zagotovljen, saj stranka sporočilo ponovno serializira; sama arhivirana kopija pa ostane nespremenjena.

## ⚙️ Nastavitve {#settings}

Vse nastavitve se nahajajo v razdelku `RawMessages` ali pa so na voljo kot spremenljive okolja z predponom `RawMessages__`.

| Nastavitev | Z умолчанию | Opis |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Shranite originalno sporočilo vsakega novega arhiviranega e-poštnega sporočila. Sporočila, arhivirana pred vklopitvijo te nastavitve, nimajo originala. |
| `RawMessages:MaxMessageSizeMb` | `50` | Sporočila, večja od te vrednosti, se arhivirajo brez originala. `0` = brez omejitve. |
| `RawMessages:IncludeGraph` | `true` | Shranite tudi original za Microsoft 365 (Graph) račune. To zahteva en dodatni Graph zahtevek na sporočilo, kar vpliva na omejitve (throttling budget). |
| `RawMessages:Storage` | `postgres` | Kje se shranijo bajti sporočila: `postgres` (v bazi podatkov) ali `filesystem` (gzip datoteka poleg arhive). Poglejte [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Direktorij, kam se pišejo datoteke sporočil, v odnosu na korenino vsebine aplikacije. To usmerite na zmontiran volumen v Dockerju. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Število ravni direktorijev z dvema znakoma, na katere je razporen ID e-pošte. `2` zagotavlja 10.000 direktorijev, `0` je plošena struktura. To ohranja nizko število datotek na direktorij pri velikih arhivih. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Ponovno izračunaj SHA-256 datoteke ob vsakem prenosu in ga primerjaj s kontrolo vrednosti (checksum), zabeleženo ob arhiviranju. Neujemanje povzroči napako pri prenosu z HTTP 500 namesto serviranja spremenjenih bajtov. |

### Velikost {#sizing}

Shramba se povečuje približno za velikost vsakega e-poštnega sporočila vključno z njegovimi prilogami, v gzip obliki. Priloge **niso** deduplicirane znotraj shranjenega originala, zato e-poštno sporočilo z veliko prilogo stane to prilogo dvakrat: enkrat v deduplicirani shrambi prilog in enkrat znotraj originala. Načrtujte približno **dvakrat** večjo velikost baze podatkov kot pri arhivu brez originalov.

Vključitev nastavitve ne dopolni obstoječih sporočil. Le nove arhivirana e-poštna sporočila dobijo original.

## 💾 Shpritsnik za shranjevanje {#storage-backend}

Nastavitev `Storage` določa, kje se nahajajo bajti. **Metadata vrstica vedno ostane v
bazi podatkov**, ne glede na to, kateri shpritsnik je konfiguriran, in vsežuje SHA-256, velikost,
vir zajema in (za datoteke) relativno pot. Prav ta vrstica omogoča arhivu, da dokaže,
kaj bi arhivirano sporočilo moralo vsebovati.

### postgres (default) {#postgres-default}

Komprimirano sporočilo se nahaja v stolpcu `Content` v `mail_archiver."RawMessages"`.
To je vedenje, ki ga je arhiv vedno imel, in ne zahteva dodatnega volumna.

Izberite to, ko je baza podatkov že vaša enota za varnostno kopiranje in želite imeti
eno stvar, ki jo boste kopirali in obnovili.

### filesystem {#filesystem}

Sporočilo je zapisano v `{BasePath}/{xx}/{yy}/{id}.eml.gz`, kjer sta `xx`/`yy` izvedena
iz e-poštnega id. Vrstica v bazi podatkov ohranja le metapodatke.

Izberite to, ko je arhiv dovolj velik, da bi originali prevladali nad velikostjo baze
podatkov, ali ko želite datoteke sporočil postaviti na cenejš ali ločen shpritsnik.

!!! danger "Mount a volume in Docker"
    Privzeto `BasePath` (`RawMessages`) se razreši v `/app/RawMessages` **znotraj
    kontejnerja**. Brez bind mounta ta direktorij pripada kontejnerjevemu zapisnemu
    sloju, zato vsak ponovno ustvarjen kontejner - vključno s `docker compose down && docker compose
    up -d`, posodobitvijo slike ali ponovnim zagonom gostitelja z začasnim slojem - odnese vse
    shranjene originale s seboj, medtem ko vrstice v bazi podatkov preživijo.

    Pri uporabi shpritsnika filesystem vedno povežite direktorij z gostiteljem:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Pot na gostitelju (tukaj `./raw-messages`) je tisto, kar kopirate. Pot v kontejnerju se mora
    ujemati s `RawMessages:FileSystem:BasePath`, ali pa nastavite `RawMessages:FileSystem:BasePath` na
    točko povezave (mount point), ki ste jo izbrali. Če premaknete povezavo, ohranite `BasePath`,
    ki kaže nanj: shranjene vrednosti `StoragePath` so relativne, zato ostanejo resolvabilne.

### Menjava shpritsnikov {#switching-backends}

Brandenje sledi shpritsniku, zabeleženemu v vsaki vrstici, ne pa konfigurirani nastavitvi.
Ko spremenite `Storage`, ostanejo arhivirana sporočila prej prejeta na voljo za prenos od kjerkoli.
Nastavitev vpliva le na **novo** arhivirano pošto.

Za premikanje že shranjenih podatkov zaženite migracijo shpritsnika. Za celovito
referenco si oglejte [CLI commands](CLI.md#moving-stored-original-messages):

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
- Datoteka je izbrisana šele po tem, ko je transakcija baze podatkov, ki jo je premaknila,
  potrdljena (committed), tako da prekinjen zagon ne more izgubiti edine kopije.
- Zagon je lahko nadalčeval. Delo je izbrano s stolpcem `StorageType`, zato z ponovnim zagonom
  ukazov nadaljuje s tistim, kar še ni obdelnano.
- Izstopna koda je `1`, če katero sporočilo ne uspe, sicer `0`.

Migracija ni zahtevana za delovanje arhiva. Potrebna je le za premikanje obstoječih
bajt na drugega shpritsnik.

## 🛟 Varnostna kopija in obnovitev {#backup-and-restore}

=== "postgres backend"
    Ni dodatnothing. Originali so del baze podatkov in so zajeti s standardnim
    izvozom (dump), poglejte [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Zajemite direktorij `BasePath` **skupaj z** izvozom baze podatkov.

    Obnovitev baze podatkov brez datotek povzroči, da arhiv vsebuje vrstice, čwartor
    vsebine, ki je ni mogoče poslužiti. Prenos bo v tem primeru vrnil napako HTTP 404 namesto
    napačnih podatkov, samo e-poštna sporočila pa bodo še vedno čitljiva iz svojih razčlenjenih polj.

## 🗑️ Brisanje e-pošte {#deleting-an-email}

Brisanje e-pošte odstrani tudi njeno izvirno datoteko: pravila zadrževanja, ročno pojedinačno in množično brisanje ter brisanje celotnega e-poštnega računa vse odstranijo datoteko kot del iste operacije. S strežnikom `postgres` ni treba početi nič dodatnega, saj bajti potujejo znotraj vrstice, ki jo kaskadna brisanje že odstrani.

Datoteka je izbrisana **šele po tem**, ko je vrstica e-pošte potrditev (committed), nikoli prej. Ta dve koraki ne moreta biti ena transakcija, saj datoteka ni del transakcije baze podatkov, zato mora prva ali druga priti drugače. Ta vrtenje je varno: brisanje, ki ne uspe ali se zavrne (roll back), na primer pri zakleni zapisu, pusti izvirno datoteko neobstojsko, neuspeh med obema korakoma pa pusti datoteko brez vrstice, ki jo spodnji proces čiščenja (sweep) kasneje sprosti. Druga vrtača bi uničila izvirno datoteko e-pošte, ki še vedno obstaja.

**Osamljene datoteke se zberejo.** Vse, kar je ušlo, vključno s datotekami, ki so jih pustila brisanja pred uvedbo tega čiščenja, odstrani periodično čiščenje `BasePath`, ki primerja vsako datoteko z vrsticami in odstrani tiste, na katere se nobena e-pošta ne nanaša:

| Nastavitev | Z умолчанию | Pomen |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Vklopi ali izklopi čiščenje |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Kako pogosto se izvaja |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Datotek, mlajših od tega, se ne moti |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Zamika pred prvim zagonom, da se najprej zaključi tekoča migracija shrambe |

Čas grace period je tisto, kar čiščenje naredi varno: uvoz zapiše datoteko, preden potrdi vrstico, ki na njo kaže, datoteka, zapisana pred trenutkom, pa ne sme biti zamenjana za osamljeno. Polovično zapisane `.tmp` datoteke zaradi prekinjenega zapisa se zberejo po istem pravilu. S strežnikom `postgres` čiščenje nič ne naredi in celo ne prebere direktorija.

## 🔍 Preverjanje arhive {#verifying-an-archive}

Kontrolna vrednost (checksum), zabeležena ob ustvarjanju arhive, omogoča opravljanje preverjanja.

- **Ob prenosu** (`VerifyChecksumOnRead=true`, privzeto) se datoteka izračuna v obliki hash in primerja. Če se vrednosti ne ujemajo, se prenos zavrne z napako HTTP 500.
- **Pri migraciji shrambe** se vsaka sporočilo pred kopiranjem preveri v obeh smerih.

Ker se vrstica metapodatkov nahaja v bazi podatkov, bitov pa lahko so na disku, je primerjava kontrolnih vrednosti edini način, da se opazi, če je bila datoteka spremenjena zunaj aplikacije.

## ⚠️ Vesč, ki jih morate vedeti {#things-to-know}

- **Omogočanje ne vključuje preteklih podatkov.** Le poštni sporočilo, arhivirano po preklopu, dobi original.
- **Manjka original se zabeleži kot napaka.** Ko zajem zavrne (sporočilo nad `MaxMessageSizeMb`, zajem ni uporaben) ali ko zapis v konfigurirano pomembno shranjevalno enoto ne uspe, log vsebuje napako z identifikatorjem e-pošte in razlogom. E-pošta ostane v arhivu brez svojega originala; ni samodejnega dopolnjevanja in ni ponovnih poskusov.
- **Stran s podrobnostmi označi manjkajoč original.** Z `RawMessages:Enabled` stran s podrobnostmi prikazuje obvestilo nad glavi. Vpraša shranjevalno enoto, zato je označena tudi datoteka v sistemu datotek, ki je izginila. Seznam e-pošte nima oznake za vsak vrstec; to bi zavzelo preveč prostora v stolpcu statusa.
- **Rekonstrukcija ni sporočilo.** Za e-poštna sporočila brez shranjenega originala vsaka pot prenosu vrne zmedicirano sporočilo in to navede prek `X-Raw-Message-Reconstructed: true`.
- **Sistem datotek pomeni dve enoti varnostne kopije.** Bazo podatkov in direktorij s sporočili je treba obnoviti skupaj, sicer vrsti v bazi preživijo brez vsebine.
- **`VerifyChecksumOnRead` porabi CPU.** Vsak prenos velike e-poštne sporočilo se dekomprimira in izračuna njegova hash vrednost. Funkcijo pustite vklopljeno, razen če izmerite težavo; to je edina zaščita pred tiho spremembo na disku.
- **Shranjeni original ni dedupliciran.** Priloga, ki se pojavi v številnih e-poštnih sporočilih, je v shrambi prilog shranjena enkrat, vendar je v okviru originala shranjena enkrat za vsako e-poštno sporočilo.
- **Brisanje odstrani tudi original.** E-poštno sporočilo, ki ga izbriše politika zadržanja ali ročno, odvzame tudi svoj shranjeni original, datoteko in vrstico. Nič ne ostane v arhivu, če ga arhiv ne navaja več v seznamu.

## 🔗 Poglejte tudi {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - celotna migracija shrambe
- [Setup and Parameters](Setup.md) - vsi konfiguracijski parametri
- [REST API](API.md#download-original-message) - `/raw` končališče
- [MCP Server](MCP.md#get_email_raw) - orodje `get_email_raw`
- [Backup and Restore](BackupRestore.md) - kaj z amounts kopirati
- [Attachment Deduplication](AttachmentDeduplication.md) - kako so shranjene priloge
