# 📧 Shranovanje originalnih sporočil {#original-message-storage}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Privzeto arhiv ohranja **razdeljene dele** e-pošte: glave, besedilno in HTML vsebino ter priloge. Iz teh polj se znova sestavi izvezeni `.eml`.

To je dovolj za branje e-pošte, vendar sporočilo ni tisto, **kot je bilo prejeto**. Prenosne kodiranja, MIME meje in upogibanje glav (header folding) se razlikujejo od originala, zato znova sestavljeno sporočilo ne potrjuje več svoje DKIM podpisa in ga spam filtri ocenijo drugače.

Z vklopljenim shranovanjem originalnih sporočil arhiv poleg razdeljenih pol ohrani celotno sporočilo, kot je prišlo. Shranjeno originalno sporočilo je tisto, za katerega lahko arhiv dokaže, da je bilo dostavljeno.

| | Le razdeljena polja | S shranjenim originalom |
| --- | --- | --- |
| Glave, vsebina, priloge | ✅ | ✅ |
| Bavenjem enakovestno sporočilo | ❌ | ✅ |
| DKIM podpis se potrdi | ❌ | ✅ |
| Forenzična uporaba (sodišče, revizija) | omejena | ✅ |

## 🎯 Kje se posreduje original {#where-the-original-is-served}

Ko je original shranjen, se vrne kjerkoli, kjer se pričakuje celovito sporočilo:

| Krain | Kaj se zgodi |
| --- | --- |
| gumb **Export EML** na strani s podrobnostiami e-pošte | Posreduje shranjeni original, če obstaja; če ne, se uporabi rekonstrukcija za e-pošto, ki je bila arhivirana brez njega. |
| **Restore to a mailbox** | Občezuje shranjeni original, če obstaja; če ne, se uporabi rekonstrukcija za e-pošto, ki je bila arhivirana brez njega. |
| `GET /api/v1/emails/{id}/raw` | Prenos prek REST API, poglejte [vodnik za REST API](API.md#download-original-message). |
| `get_email_raw` | MCP orodje, poglejte [vodnik za MCP](MCP.md#get_email_raw). |

Odgovor vam pove, katero obliko ste prejeli, tako da lahko klicatelj razlikuje pristojen original od rekonstrukcije, ne da bi preverjal bajte:

| Signal | Pomen |
| --- | --- |
| `X-Raw-Message-Source: imap` | Natančna kopija z IMAP strežnika (`BODY[]`, kot je bila dostavljena). |
| `X-Raw-Message-Source: graph` | Natančna kopija, ki jo je vrnil Exchange z MIME vsebino. |
| `X-Raw-Message-Source: eml-file` | Natančna kopija, vsebina uvozne datoteke `.eml`. |
| `X-Raw-Message-Source: serialized` | Parsirano sporočilo, ponovno zapisano. Identično, razen če so končnice vrstic CRLF (uvoz MBox, pridobitev pri IMAP ponovni okrešanju). |
| `X-Raw-Message-Sha256` | SHA-256 shranjenega originala. |
| `X-Raw-Message-Reconstructed: true` | Original ni shranjen; sporočilo je bilo zgrajeno ponovno. Brez glave s kontrolo sume. |
| `X-Raw-Message-Storage` | Nazadnje (backend), iz katerega je bil original prebran (`postgres` ali `filesystem`). |

Tudi ime datoteke to pove: `email-{id}-original-...eml` za shranjen original, `email-{id}-...eml` za rekonstrukcijo.

Ko shranjen original **ne opravi kontrole sume**, se izvoz in ponovna obnova zavrneta z besedilom o napaki. Namesto tega bi vrnili rekonstrukcijo, kar bi prikrilo dejstvo, da je bila arhivirana kopija spremenjena, kar je nekaj, česar arhiv nikoli ne sme storiti.

Ob obnovi (restore) se shranjeni original prebere in doda prek klientske strani ponudnika, tako da nabiralnik prejme originalne glave in MIME strukturo. Pri IMAP stranka sporočilo ponovno serializira, zato natančen tok bajtov na povezavi ni zagotovljen. Pri Microsoft 365 arhivirani bajti potujejo nespremenjeni, ker Graph sprejema sporočilo v obliki MIME. Sama arhivirana kopija nikoli ni modificirana.

## ⚙️ Nastavitve {#settings}

Vse nastavitve se nahajajo v razdelku `RawMessages` ali pa so na voljo kot okoljske spremenljivke z prefiksom `RawMessages__`.

| Nastavitev | Privzeto | Opis |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Shranite originalno sporočilo vsakega novoportanega e-poštnega sporočila. Sporočila, arhivirana pred vklopitvijo te nastavitve, nimajo originala. |
| `RawMessages:MaxMessageSizeMb` | `50` | Sporočila, večja od te vrednosti, se arhivirajo brez svojega originala. `0` = brez omejitve. |
| `RawMessages:IncludeGraph` | `true` | Shranite tudi original za Microsoft 365 (Graph) račune. To zahteva en dodatni Graph zahtev na sporočilo, kar vpliva na omejitev (throttling budget). |
| `RawMessages:Storage` | `postgres` | Kje so shranjeni bajti sporočila: `postgres` (v bazi podatkov) ali `filesystem` (gzip datoteka zraven arhiv samega). Poglejte [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Mreža direktorijev, kamor se pišejo datoteke sporočil, relativno do korenine vsebine aplikacije. To usmerite na montiran volumen v Dockerju. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Število direktorijskih ravni z dvema znakoma, na katere je raztrebljen ID e-pošte. `2` zagotavlja 10.000 direktorijev, `0` je ravna struktura. To ohranja nizko število datotek na direktorij pri velikih arhivih. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Ponovno izračunaj SHA-256 datoteke ob vsakem prenosu in ga primerjaj s kontrolo vsote, zabeleženo ob času arhiviranja. Neustreznost povzroči napako prenova z HTTP 500 namesto serviranja spremenjenih bajtov. |

### Velikost {#sizing}

Shrana raste približno toliko, kolik je velikost vsakega e-poštnega sporočila vključno z njegovimi prilogami, v gzip obliki. Priloge **niso** deduplicirane znotraj shranjenega originala, zato e-poštno sporočilo z veliko prilogo stane to prilogo dvakrat: enkrat v dedupliciranem skladišču prilog in enkrat znotraj originala. Planirajte približno **dvakrat** večjo velikost baze podatkov kot pri arhivu brez originalov.

Omogočanje nastavitve ne popolnjuje obstoječih sporočil. Le novoportana e-poštna sporočila dobijo original.

## 💾 Shransje (Storage backend) {#storage-backend}

Nastavitev `Storage` določa, kje se bajti nahajajo. **Metadata vrstica vedno ostane v
bazi podatkov**, ne glede na to, kateri backend je konfiguriran, in vsebuje SHA-256, velikost, vir
predvajanja in (za datoteke) relativno pot. Prav ta vrstica omogoča arhivu dokazati,서 kaj bi
hranjeno sporočilo moralo vsebovati.

### postgres (default) {#postgres-default}

Komprimirano sporočilo se nahaja v stolbcu `Content` v `mail_archiver."RawMessages"`.
To je vedenje, ki ga je arhiv vedno imel, in ne zahteva dodatnega prostora.

Izberite to, če je baza podatkov že vaša enota za varnostno kopiranje in želite imeti eno stvar,
ki jo boste yedno varnostno kopirali in obnovili.

### filesystem {#filesystem}

Sporočilo je zapisano v `{BasePath}/{xx}/{yy}/{id}.eml.gz`, kjer sta `xx`/`yy` izvedena
iz e-poštnega id. Vrstica v bazi podatkov shrani le metapodatke.

Izberite to, ko je arhiv dovolj velik, da bi originali prevladovali nad velikostjo baze podatkov,
ali ko želite datoteke sporočil postaviti na cenejš ali ločen shranjevanje.

!!! danger "Mount a volume in Docker"
    Privzeto `BasePath` (`RawMessages`) se razrešuje na `/app/RawMessages` **znotraj
    kontejnerja**. Brez bind mounta ta direktorij pripada zapisnemu sloju kontejnerja, zato vsak
    ponovno ustvarjen kontejner - vključno s `docker compose down && docker compose
    up -d`, posodobitev slike ali ponovni zagon gostitelja z privzremenim slojem - odnese vse shranjene
    originale s seboj, medtem ko vrstice v bazi podatkov preživijo.

    Pri uporabi filesystem backenda vedno montirajte direktorij na gostitelja:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Pot na gostitelju (tukaj `./raw-messages`) je tisto, kar varnostno kopirate. Pot v kontejnerju se mora
    složiti z `RawMessages:FileSystem:BasePath` ali pa nastavite `RawMessages:FileSystem:BasePath` na
    montažno točko, ki ste jo izbrali. Če premaknete montažo, ohranite `BasePath`, ki kaže nanjo:
    hranjene vrednosti `StoragePath` so relativne, zato se bodo še naprej pravilno razreševale.

### Sprostitev backendov (Switching backends) {#switching-backends}

Brastev sledijo shranjenju, zabeleženemu v vsaki vrstici, ne pa konfigurirani nastavitvi. Ko
spremenite `Storage`, ostajajo prej arhivirana sporočila preberljiva od koder koli, kjer se nahajajo.
Nastavitev vpliva le na **novo** arhivirano pošto.

Za premikanje že shranjenih podatkov zaženite migracijo shranjevanja. Za celoten pregled poglejte
[CLI commands](CLI.md#moving-stored-original-messages):

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Kaj migracija zagotavlja:

- Vsako sporočilo se pred kopiranjem preveri glede na njegov zabeležen kontrolni zглоžek (checksum).
  Sporočilo, ki ne uspe, ostane tam, kjer je, in je prijavljeno: poškodovan original nikoli ne
  propagira in njegova edina preostala kopija nikoli ne biti izbrisan.
- Datoteka je izbrisana šele po tem, ko je transakcija baze podatkov, ki jo je premaknila, uspešno
  izvedena (committed), tako da prekinjen tek lahko ne izbriše edine kopije.
- Tek lahko nadaljujete. Delo je izbrano s stolbcem `StorageType`, zato ponovni zagon ukaza nadaljuje s
  tistim, kar je še nedokončano.
- Izstopna koda je `1`, če je katero sporočilo ne uspelo, sicer `0`.

Migracija ni nujna za delovanje arhiva. Potrebna je le za premikanje obstoječih
bajt na drugi backend.

## 🛟 Varnovano kopiranje in obnavljanje {#backup-and-restore}

=== "postgres backend"
    Nič dodatnega. Originali so del baze podatkov in so zajeti s standardnim
    izvozi (dump), poglejte [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Zarukujte direktorij `BasePath` **skupaj z** izvozom baze podatkov.

    Obnova baze podatkov brez datotek povzroči, da arhiv vsebuje vrstice, čeprav njihova vsebina
    ni na voljo. Prevoz (download) nato vrne HTTP 404 namesto napačnih podatkov,
    za same e-poštne sporočila pa ostane mogoče branje iz njihovih razdeljenih polj.

## 🗑️ Brisanje e-pošte {#deleting-an-email}

Brisanje e-pošte odstrani tudi njeno izvirno datoteko: pravila o obdržativanju, ročno pojedinačno in massovno brisanje ter brisanje celotnega e-poštnega računa vse odstranijo datoteko kot del iste operacije. Pri zaledju `postgres` ni treba opraviti ničesar dodatnega, ker bajti potujejo znotraj vrstice, ki jo kaskadno že odstrani.

Datoteka je izbrisana **šele POTOM**, ko je vrstica e-pošte potrdljena (committed), nikoli prej. V obeh korakih ne more biti enega transakcije, ker datoteka ni del transakcije baze podatkov, zato mora en od njima priti drug v drugo. Ta vrstni red je varen: brisanje, ki ne uspe ali se prehiti (roll back), na primer pri zaključeni zapisu, pusti izvirno datoteko nedotikjeno, če pa pride do okrطلا med obema korakoma, ostane datoteka brez vrstice, ki jo spodnji proces čiščenja (sweep) spet pridobi. Drug vrstni red bi uničil izvirno datoteko e-pošte, ki še vedno obstaja.

**Ositane datoteke se zberejo.** Vse, kar je izbleknelo, vključno z datotekami, ki so jih pustila brisanja pred nastavitvijo tega čiščenja, odstrani periodic sweep `BasePath`, ki primerja vsako datoteko z vrsticami in odstrani tiste, na katere se nobena e-pošta ne nanaša:

| Nastavitev | Z умолчанию | Pomen |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Vklopi ali izklopi sweep |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Kako pogosto se izvaja |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Datotek, nav younger kot to, se nikoli ne dotikajo |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Zamika pred prvim zagonom, da se najprej zaključi preostala migracija shranilnega prostora |

Čas grace period je tisto, kar naredi sweep varen: uvoz zapiše datoteko preden potrdi vrstico, ki na njo kaže, in datoteko, napisano pred trenutkom, ne sme zmotiti z ositno. Polovičeno zapisane `.tmp` datoteke iz prekinjenega zapisa se zberejo pod istim pravilom. Pri zaledju `postgres` sweep ne naredi ničesar in celo ne pregleduje direktorija.

## 🔍 Preverjanje arhive {#verifying-an-archive}

Kontrolna vrednost (checksum), zabeležena ob času arhiviranja, omogoča preverjanje.

- **Ob prenosu** (`VerifyChecksumOnRead=true`, privzeto) se datoteka izračuna v obliki hash in
  primerja. Če se vrednosti ne ujamajo, se prenos zavrne z napako HTTP 500.
- **Pri migraciji shranjevanja** se vsaka sporočilo preveri, preden je kopirana, v
  obej direction.

Ker se vrstica metapodatkov nahaja v bazi podatkov, byte pa so lahko na disku, je primerjava kontrolnih vrednosti edini način, da se opazi, če je bila datoteka spremenjena zunaj aplikacije.

## ⚠️ Ves kati {#things-to-know}

- **Omogočanje ne popolnjuje preteklih podatkov.** Le pošta, ki je arhivirana po preklopitvi, dobi original.
- **Obtečen izguben original se zabeleži kot napaka.** Ko je zajem zavrnjen (glej zgornje 
  `MaxMessageSizeMb`, ni uporabnega zajema) ali ko zapis v konfigurirano pozadje ne uspe, 
  log vsebuje napako z identifikatorjem e-pošte in razlogom. E-pošta ostane v arhivu 
  brez svojega originala; ni samodejnega popolnjevanja in ni ponovnih poskusov.
- **Stran z podrobnostmi označi manjkajoč original.** Z `RawMessages:Enabled` stran z podrobnostmi 
  prikaže obvestilo nad gllavami. Vpraša shranjevalno pozadino, zato je tudi datoteka 
  v datotekovnem sistemu, ki je izginila, označena. Seznam e-pošte nima oznake za vsak vrstec; 
  to bi zelo zasedlo prostor v stolpcu statusa.
- **Rekonstrukcija ni sporočilo.** Za e-pošto brez shranjenega originala vsaka 
  pot za prenos vrne znova zgrajeno sporočilo in to navede prek 
  `X-Raw-Message-Reconstructed: true`.
- **Datotekovni sistem pomeni dve enoti varnostne kopije.** Bazo podatkov in direktorij 
  sporočil je treba obnoviti skupaj, sicer vrstci preživijo brez svoje vsebine.
- **`VerifyChecksumOnRead` porablja CPU.** Vsak prenos velike e-pošte se dekomprimira 
  in izračuna njegova hash vrednost. Funkcijo pustite vklopljeno, razen če ste izmerili težavo; 
  to je edina zaščita pred tiho spremembo na disku.
- **Shranjeni original ni dedupliciran.** Priloga, ki se pojavi v številnih e-poštnih sporočilih, 
  je shranjena enkrat v shrambi prilog, vendar enkrat za vsako e-pošto znotraj originala.
- **Brisanje odstrani tudi original.** E-pošta, ki je izbrisana zaradi politike zadrževanja ali ročno, 
  odvzame svoj shranjeni original, datoteko in vrstico. Nič ne ostane v arhivu, če ga arhiv 
  ne vseže na seznamu.

## 🔗 Poglej tudi {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - celoten proces migracije shranjevanja
- [Setup and Parameters](Setup.md) - vse konfiguracijske parametre
- [REST API](API.md#download-original-message) - `/raw` končališče
- [MCP Server](MCP.md#get_email_raw) - orodje `get_email_raw`
- [Backup and Restore](BackupRestore.md) - kaj zamejati
- [Attachment Deduplication](AttachmentDeduplication.md) - kako so priloge shranjene
