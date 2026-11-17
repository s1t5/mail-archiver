# 📧 Shranjevanje originalnega sporočila {#original-message-storage}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Privzeto arhiv ohranja **razčlenjene dele** e-pošte: glave, besedilno in HTML telo ter priloge. Iz teh polj se znova sestavi izvoženo `.eml`.

To je dovolj za branje e-pošte, vendar sporočilo ni tisto, **kot je bilo prejeto**. Prenosni kodiranja, MIME meje in zapoganje glav (header folding) se razlikujejo od originala, zato ponovno sestavljeno sporočilo ne potrdi več svoje DKIM podpisa in ga spam skenerji ocenijo drugače.

Z vklopljenim shranjevanjem originalnega sporočila arhiv poleg razčlenjenih polj ohrani celotno sporočilo, kot je prišlo. Shranjeni original je tisto, česar lahko arhiv dokaže, da je bilo dostavljeno.

| | Le razčlenjena polja | S shranjenim originalom |
| --- | --- | --- |
| Glave, telo, priloge | ✅ | ✅ |
| Byte-exact sporočilo | ❌ | ✅ |
| DKIM podpis se potrdi | ❌ | ✅ |
| Forenzična uporaba (sodišče, revizija) | omejena | ✅ |

## 🎯 Kje se posluži original {#where-the-original-is-served}

Ko je original shranjen, se vrne kjerkoli, kjer se pričakuje celotno sporočilo:

| Kraj | Kaj se zgodi |
| --- | --- |
| gumb **Export EML** na strani s podrobnostmi e-pošte | Posluži shranjeni original, če obstaja; če ga ni pri arhiviranih e-poštnih sporočilih, se uporabi rekonstrukcija. |
| **Restore to a mailbox** | Doda shranjeni original, če obstaja; če ga ni pri arhiviranih e-poštnih sporočilih, se uporabi rekonstrukcija. |
| `GET /api/v1/emails/{id}/raw` | Prenos prek REST API, poglejte [REST API guide](API.md#download-original-message). |
| `get_email_raw` | MCP orodje, poglejte [MCP guide](MCP.md#get_email_raw). |

Odgovor vam pove, katero obliko ste prejeli, tako da lahko klicatelj prepozna pravi original od rekonstrukcije brez pregledovanja bajtov:

| Signal | Pom meaning |
| --- | --- |
| `X-Raw-Message-Source: imap` | Natančna kopija z IMAP strežnika (`BODY[]`, kot je bilo dostavljeno). |
| `X-Raw-Message-Source: graph` | Natančna kopija, ki jo je vrnil Exchange MIME vsebnosti. |
| `X-Raw-Message-Source: eml-file` | Natančna kopija, vsebina uvožene datoteke `.eml`. |
| `X-Raw-Message-Source: serialized` | Parsirano sporočilo, ki je bilo ponovno zapisano. Identično, razen ker so končnice vrnic CRLF (uvoz MBox, pridobitev pri IMAP obnovi). |
| `X-Raw-Message-Sha256` | SHA-256 shranjenega originala. |
| `X-Raw-Message-Reconstructed: true` | Original ni shranjen; sporočilo je bilo zgrajeno ponovno. Brez kontrolne vrednosti (checksum header). |
| `X-Raw-Message-Storage` | Nazadnje (backend), iz katerega je bil original prebran (`postgres` ali `filesystem`). |

Tudi ime datoteke to pove: `email-{id}-original-...eml` za shranjen original, `email-{id}-...eml` za rekonstrukcijo.

Ko **shranjeni original ne ustreza svoji kontrolni vrednosti (checksum)**, izvoz in obnovitev zavrni z besedilom o napaki. Namesto tega bi vrnili rekonstrukcijo, kar bi prikrilo dejstvo, da je bila arhivirana kopija spremenjena, kar je pravzaprav nekaj, česar arhiv nikoli ne sme storiti.

Ob obnovi (restore) se shranjeni original prebere in doda prek ponudnikjevega odjemalca (client), zato poštni predal prejme originalne glave in MIME strukturo. Natančen tok bajtov v omrežju ni zagotovljen, ker odjemalec sporočilo ponovno serializira; sama arhivirana kopija pa ostane nespremenjena.

## ⚙️ Nastavitve {#settings}

Vse nastavitve se nahajajo v razdelku `RawMessages` ali pa so na voljo kot spremenljive okolja z predponom `RawMessages__`.

| Nastavitev | Z умолчанию | Opis |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Shranite originalno sporočilo vsakega novega arhiviranega e-poštnega sporočila. E-poštna sporočila, arhivirana pred vklopitvijo te nastavitve, nimajo originala. |
| `RawMessages:MaxMessageSizeMb` | `50` | Sporočila, večja od te vrednosti, se arhivirajo brez svojega originala. `0` = brez omejitve. |
| `RawMessages:IncludeGraph` | `true` | Prav tako shranite original za Microsoft 365 (Graph) račune. To zahteva en dodatni Graph zahtevek na sporočilo, kar vpliva na omejitve hiterosti (throttling budget). |
| `RawMessages:Storage` | `postgres` | Kje se shranjeni bajti sporočila hranijo: `postgres` (v bazi podatkov) ali `filesystem` (gzip datoteka poleg arhive). Poglejte [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Direktorij, kam se pišejo datoteke sporočil, v odnosu na korenino vsebine aplikacije. To usmerite na montiran volumen v Dockerju. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Število direktorijskih ravni z dvema znakoma, na katere je razdeljena identifikacijska številka e-pošte. `2` zagotavlja 10.000 direktorijev, `0` je plošena struktura. To ohranja nizko število datotek na direktorij pri velikih arhivih. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Ponovno izračunaj SHA-256 datoteke ob vsakem prenosu in ga primerjaj s kontrolo sume, zabeležene ob arhiviranju. Neustreznost povzroči napako pri prenosu z HTTP 500 namesto serviranja spremenjenih bajtov. |

### Velikost {#sizing}

Shramba se povečuje približno za velikost vsakega e-poštnega sporočila vključno z njegovimi prilogami, v gzip-stisnjeni obliki. Priloge **niso** deduplicirane znotraj shranjenega originala, zato e-poštno sporočilo z veliko prilogo stane to prilogo dvakrat: enkrat v dedupliciranem skladišču prilog in enkrat znotraj originala. Načrtujte približno **dvakrat** večjo velikost baze podatkov kot pri arhivu brez originalov.

Omogočanje nastavitve ne dopolni obstoječih sporočil. Le nove arhivirana e-poštna sporočila dobijo original.

## 💾 Shrineski backend {#storage-backend}

Nastavitev `Storage` določa, kje se nahajajo bajti. **Metadata vrstica vedno ostane v
bazi podatkov**, ne glede na to, kateri backend je konfiguriran, in vsežbuje SHA-256, velikost, vir zajema in (za datoteke) relativno pot. Ta vrstica omogoča arhivu, da dokaže, kaj bi arhivirana
sposobnost morala vsebovati.

### postgres (default) {#postgres-default}

Komprimirano sporočilo se nahaja v stolocu `Content` v `mail_archiver."RawMessages"`.
To je vedenje, ki ga je arhiv vedno imel, in ne zahteva dodatnega volumena.

Izberite to, ko je baza podatkov že vaša enota za varnostno kopiranje in želite imeti le eno stvar za
kopiranje in obnavljanje.

### filesystem {#filesystem}

Sporočilo je zapisano v `{BasePath}/{xx}/{yy}/{id}.eml.gz`, kjer sta `xx`/`yy` izvedena
iz e-poštnega id. Vrstica v bazi podatkov ohranja le metapodatke.

Izberite to, ko je arhiv dovolj velik, da bi originali prevladovali nad velikostjo baze podatkov,
ali ko želite datoteke sporočil postaviti na cenejš ali ločen shranjevalni prostor.

!!! danger "Mount a volume in Docker"
    Privzeto `BasePath` (`RawMessages`) se razreši v `/app/RawMessages` **znotraj
    kontejnerja**. Brez bind mounta ta direktorij pripada primetnemu sloju kontejnerja, zato vsak ponovno ustvarjen kontejner - vključno s `docker compose down && docker compose
    up -d`, posodobitvijo slike ali ponovnim zagonom gostitelja z začasnim slojem - odnese vse shranjene
    originale s seboj, medtem ko vrstice v bazi podatkov preživijo.

    Pri uporabi backendov filesystem vedno povežite direktorij na gostitelja:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Pot na gostitelju (tukaj `./raw-messages`) je tisto, kar kopirate. Pot v kontejnerju se mora
    ujemati s `RawMessages:FileSystem:BasePath`, ali pa nastavite `RawMessages:FileSystem:BasePath` na
    točko povezave (mount point), ki ste jo izbrali. Če premaknete povezavo, ohranite `BasePath`, ki kaže nanj:
    shranjene vrednosti `StoragePath` so relativne, zato ostanejo resolvirne.

### Menjava backendov {#switching-backends}

Brandenje sledi shranjevanju, zabeleženemu v vsaki vrstici, ne pa konfigurirani nastavitvi. Po
spremembi `Storage` ostanejo arhivirana sporočila pred tem prenosljiva od koderkoli so.
Nastavitev vpliva le na **novo** arhivirano pošto.

Za premikanje že shranjenih podatkov zaženite migracijo shranjevanja. Za celoten pregled
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

- Vsako sporočilo se pred kopiranjem preveri glede na njegovo zabeleženo kontrolno vrednost (checksum). Sporočilo,
  ki ne uspe, ostane tam, kjer je, in je prijavljeno: poškodovan original nikoli ne pride do širjenja in
  njegova edina preostala kopija nikoli ni izbrisana.
- Datoteka je izbrisana šele po tem, ko je transakcija baze podatkov, ki jo je premaknila, potrdila (committed), tako da
  prekinjen zagon ne lahko izgubi edine kopije.
- Zagon je lahko nadaljevan. Delo je izbrano s stolocem `StorageType`, zato ponovni zagon
  ukazov nadaljuje s tistim, kar še ni obdelano.
- Izstopna koda je `1`, če katero sporočilo ne uspe, sicer `0`.

Migracija ni nujna za delovanje arhiva. Potrebna je le za premikanje obstoječih
bajt na drugi backend.

## 🛟 Varnovanje in obnovitev {#backup-and-restore}

=== "postgres backend"
    Ni dodatnothing. Originali so del baze podatkov in so zajeti s standardnim
    izvozom (dump), poglejte [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Zaradi varnosti izvezite direktorij `BasePath` **skupaj z** izvozom baze podatkov.

    Obnovitev baze podatkov brez datotek povzroči, da arhiv vsebuje vrstice, čeprav
    vsebina ni dostopna. Prenos bo v tem primeru vrnil napako HTTP 404 namesto
    napačnih podatkov, samo e-poštna sporočila pa bodo ostala berljiva iz svojih
    razčlenjenih polj.

## 🗑️ Brisanje e-pošte {#deleting-an-email}

Brisanje e-pošte odstrani tudi njeno izvirno datoteko: pravila o zadržanju, ročno pojedinačno in množično brisanje ter brisanje celotnega e-poštnega računa vsi odstranijo datoteko kot del iste operacije. S podbo `postgres` ni treba početi ničesar dodatnega, saj se bajti premikajo znotraj vrstice, ki jo kaskadna brisanje že odstrani.

Datoteka je izbrisana **šele po tem**, ko je vrstica e-pošte potrditev (committed), nikoli prej. Ta dve koraki ne moreta biti ena transakcija, ker datoteka ni del transakcije baze podatkov, zato mora eden od njima priti drug v redu. Ta vrteno red je varen: brisanje, ki ne uspe ali se zavrne (roll back), na primer pri zaklenjenem zapisu, pusti izvirno datoteko neobstojsko, neuspešen proces med obema korakoma pa pusti datoteko brez vrstice, ki jo spodnji čistilni proces (sweep) ponovno pridobi. Nasprotni red bi uničil izvirno datoteko e-pošte, ki še vedno obstaja.

**Osamljene datoteke se zberejo.** Vse, kar je ušlo, vključno s datotekami, ki so jih pustila brisanja pred uvedbo tega čiščenja, odstrani periodično čiščenje `BasePath`, ki primerja vsako datoteko z vrsticami in odstrani tiste, na katere se nobena e-pošta ne nanaša:

| Nastavitev | Z умолчанию | Pomen |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Vklopi ali izklopi čiščenje |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Kako pogosto se izvaja |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Datotek, mlajših od tega, se nikoli ne moti |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Zamika pred prvim zagonom, da se najprej zaključi čakajoča migracija shrambe |

Čas grace period je tisto, kar čiščenje naredi varnim: uvoz zapiše datoteko, preden potrdi vrstico, ki na njo kaže, datoteka, zapisana pred trenutkom, pa ne sme biti zamenjana za osamljeno. Polovično zapisane datoteke `.tmp` iz prekinjenega zapisa se zberejo po istem pravilu. S podbo `postgres` čiščenje nič ne naredi in celo ne preišče direktorija.

## 🔍 Preverjanje arhive {#verifying-an-archive}

Kontrolna vrednost (checksum), zabeležena ob ustvarjanju arhive, omogoča preverjanje.

- **Ob prenosu** (`VerifyChecksumOnRead=true`, privzeto) se datoteka izračuna s hash funkcijijo in primerja. Če se vrednosti ne ujemajo, se prenos zavrne z napako HTTP 500.
- **Pri migraciji shranjevanja** se vsaka sporočilo preveri pred kopiranjem, v obeh smerah.

Ker se vrstica metapodatkov nahaja v bazi podatkov, bitov pa lahko na disku, je primerjava kontrolnih vrednosti edini način, da ugotovimo, da je bila datoteka spremenjena zunaj aplikacije.

## ⚠️ Vesč, ki jih morate vedeti {#things-to-know}

- **Omogočanje ne vključuje preteklih podatkov.** Le poštni sporočilo, arhivirano po preklopu, dobi original.
- **Manjka original se zabeleži kot napaka.** Ko zajem zavrne (spodaj navedeno `MaxMessageSizeMb`, brez uporabljenega zajema) ali ko zapis v konfigurirano pomembno okolje (backend) ne uspe, log vsebuje napako z identifikatorjem e-pošte in razlogom. E-pošta ostane v arhivu brez svojega originala; ni samodejnega obnavljanja in ni ponovnih poskusov.
- **Utičje (UI) označi manjkajoč original.** S `RawMessages:Enabled` seznam e-pošte pri vsakem sporočilu brez shranjenega originala prikaže opozorilno oznako, stran s podrobnostmi pa nad glavi prikazuje obvestilo. Seznam oznako razreši z enim poizvedbo na stran; stran s podrobnostmi vpraša shranjevalno okolje, zato je označena tudi datoteka v datotebnem sistemu, ki je izginila.
- **Rekonstrukcija ni sporočilo.** Za e-poštna sporočila brez shranjenega originala vsaka pot prenosu vrne zgrajeno sporočilo in to navede prek `X-Raw-Message-Reconstructed: true`.
- **Datotečni sistem pomeni dve enoti za varnostno kopiranje.** Bazo podatkov in direktorij s sporočili je treba obnoviti skupaj, sicer vrstice preživijo brez svoje vsebine.
- **`VerifyChecksumOnRead` porabi CPU.** Vsak prenos velike e-poštne sporočile zahteva dekompresijo in izračun hash vrednosti. Funkcijo pustite vklopljeno, razen če ste izmerili težavo; to je edina zaščita pred tiho spremembo na disku.
- **Shranjeni original ni dedupliciran.** Priloga, ki se pojavi v številnih e-poštnih sporočilih, se v shrambi prilog shrani enkrat, v originalu pa enkrat za vsako e-poštno sporočilo.
- **Brisanje odstrani tudi original.** E-poštno sporočilo, ki ga izbrišejo zaradi politike zadržanja podatkov ali ročno, odvzame tudi svoj shranjeni original, datoteko in vrstico. Nič ne ostane v arhivu, če ga arhiv ne vsebuje na seznamu.

## 🔗 Poglej tudi {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - celotna migracija shrambe
- [Setup and Parameters](Setup.md) - vse parametre konfiguracije
- [REST API](API.md#download-original-message) - `/raw` končališče
- [MCP Server](MCP.md#get_email_raw) - orodje `get_email_raw`
- [Backup and Restore](BackupRestore.md) - kaj z amounts kopirati
- [Attachment Deduplication](AttachmentDeduplication.md) - kako so shranjene priloge
