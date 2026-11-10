# 📧 Shranjevanje originalnih sporočil {#original-message-storage}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Arhiv po privzetih nastavitvah shranjuje **razčlenjene dele** e-poštnega sporočila: glave, besedilno in HTML vsebino ter priloge. Iz teh polj je znova sestavljeno `.eml`.

To je dovolj za branje e-poštnega sporočila, vendar sporočilo ni isto kot **prejeto**. Kodiranje prenosa, MIME meje in upogibanje glav (header folding) se razlikujejo od originala, zato znova sestavljeno sporočilo ne potrjuje več svoje DKIM podpisa in ga spam skenerji ocenjujejo drugače.

Ko je shranjevanje originalnih sporočil omogočeno, arhiv poleg razčlenjenih pol shrani celotno sporočilo, kot je prišlo. Shranjeno originalno sporočilo je tisto, kar lahko arhiv dokaže kot dostavljeno.

| | Le razčlenjena polja | S shranjenim originalom |
| --- | --- | --- |
| Glave, vsebina, priloge | ✅ | ✅ |
| Bajt po bajtu enoto sporočilo | ❌ | ✅ |
| DKIM podpis je veljavno | ❌ | ✅ |
| Forenzična uporaba (sodišče, revizija) | omejeno | ✅ |

## 🎯 Kje je na voljo original {#where-the-original-is-served}

Ko je original shranjen, se vrne kadar koli se pričakuje celotno sporočilo:

| Krain | Kaj se zgodi |
| --- | --- |
| gumb **Export EML** na strani s podrobnostmi e-pošte | Če je original shranjen, ga posluži; če ga ni (pri arhiviranih e-poštnih sporočilih), se uporabi rekonstrukcija. |
| **Restore to a mailbox** | Če je original shranjen, ga doda; če ga ni (pri arhiviranih e-poštnih sporočilih), se uporabi rekonstrukcija. |
| `GET /api/v1/emails/{id}/raw` | Prenos prek REST API, poglejte [vodnik REST API](API.md#download-original-message). |
| `get_email_raw` | MCP orodje, poglejte [vodnik MCP](MCP.md#get_email_raw). |

Odgovor vam pove, katero obliko ste prejeli, tako da lahko klicatelj brez pregledovanja bajt razlikuje dejanski original od rekonstrukcije:

| Signal | Pomen |
| --- | --- |
| `X-Raw-Message-Source: imap` | Natančna kopija z IMAP strežnika (`BODY[]` kot je bila dostavljena). |
| `X-Raw-Message-Source: graph` | Natančna kopija, ki jo je vrnil Exchange MIME vsebine. |
| `X-Raw-Message-Source: eml-file` | Natančna kopija vsebine uvožene datoteke `.eml`. |
| `X-Raw-Message-Source: serialized` | Parsirano sporočilo, ponovno zapisano. Identično, razen v tem, da so končnice vrstic CRLF (uvoz MBox, pridobitev pri IMAP obnovi). |
| `X-Raw-Message-Sha256` | SHA-256 shranjenega originala. |
| `X-Raw-Message-Reconstructed: true` | Original ni shranjen; sporočilo je bilo zgrajeno ponovno. Brez glave s kontrolo vsota. |
| `X-Raw-Message-Storage` | Nazadnje (backend), iz katerega je bil original prebran (`postgres` ali `filesystem`). |

Imenovska datoteka to pove tudi: `email-{id}-original-...eml` za shranjen original, `email-{id}-...eml` za rekonstrukcijo.

Ko **shranjeni original ne opravi kontrole vsote**, se izvoz in obnavljanje zavrnejo z besedilom o napaki. Če bi namesto tega vrnili rekonstrukcijo, bi to prikrilo dejstvo, da je bila arhivirana kopija spremenjena, kar je nekaj, česar arhiv nikoli ne sme storiti.

Ob obnavi (restore) se shranjeni original prebere in doda prek odjemnikov clove (provider's client), zato e-poštni predal prejme originalne glave in MIME strukturo. Natančen tok bajtov na omrežju ni zagotovljen, ker stranka sporočilo ponovno serializira; sama arhivirana kopija pa ostane nespremenjena.

## ⚙️ Nastavitve {#settings}

Vse nastavitve se nahajajo v razdelku `RawMessages` ali pa kot okoljske spremenljivke z predponom `RawMessages__`.

| Nastavitev | Privzeto | Opis |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Shranite originalno sporočilo vsakega novo arhiviranega e-poštnega sporočila. Sporočila, arhivirana pred vklitvijo te nastavitve, nimajo originala. |
| `RawMessages:MaxMessageSizeMb` | `50` | Sporočila, večja od te vrednosti, se arhivirajo brez svojega originala. `0` = brez omejitve. |
| `RawMessages:IncludeGraph` | `true` | Prav tako shranite original za Microsoft 365 (Graph) račune. To zahteva en dodatni Graph zahtevek na sporočilo, kar vpliva na omejitev (throttling budget). |
| `RawMessages:Storage` | `postgres` | Kje se shranjeni bajti sporočila hranijo: `postgres` (v bazi podatkov) ali `filesystem` (gzip datoteka poleg arhive). Poglejte [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Mesto (directory), kamor se pišejo datoteke sporočil, v odnosu na korenino vsebine aplikacije. To usmerite na montiran volumen v Dockerju. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Število ravni direktorijev z dvema znakoma, na katere je raztrebljen id e-poštnega sporočila. `2` zagotavlja 10.000 direktorijev, `0` je ravna struktura. To ohranja nizko število datotek na direktorij pri velikih arhivih. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Ponovno izračunaj SHA-256 datoteke ob vsem prenosu in ga primerjaj s kontrolo vrednosti (checksum), zabeleženo ob arhiviranju. Če se vrednosti ne ujemata, prenos ne uspe zaradi napake HTTP 500 namesto serviranja spremenjenih bajtov. |

### Velikost {#sizing}

Shranišče raste približno toliko, kolika je velikost vsakega e-poštnega sporočila vključno z njegovimi prilogami, v gzip obliki. Priloge **niso** deduplicirane znotraj shranjenega originala, zato e-poštno sporočilo z veliko prilogo obravnava to prilogo dvakrat: enkrat v dedupliciranem skladišču prilog in enkrat znotraj originala. Planirajte približno **dvakrat** večjo velikost baze podatkov kot pri arhivu brez originalov.

Omogočanje nastavitve ne popolni obstoječih sporočil. Le nove arhivirana e-poštna sporočila dobijo original.

## 💾 Shranilni pogon {#storage-backend}

Nastavitev `Storage` določa, kje se nahajajo bajti. **Metadata vrstica vedno ostane v
bazi podatkov**, ne glede na to, kateri pogon je konfiguriran, in vsebuje SHA-256, velikost, vir
vzorčevanja in (za datoteke) relativno pot. Ta vrstica omogoča arhivu, da dokaže,서 kaj bi
hranjeno sporočilo moralo vsebovati.

### postgres (default) {#postgres-default}

Komprimirano sporočilo se nahaja v stolpcu `Content` v `mail_archiver."RawMessages"`.
To je vedenje, ki ga je arhiv vedno imel in ne potrebuje dodatne volumenske enote.

Izberite to, če je baza podatkov že vaša enota za varnostno kopiranje in želite imeti eno stvar
za varnostno kopiranje in obnavljanje.

### filesystem {#filesystem}

Sporočilo je zapisano v `{BasePath}/{xx}/{yy}/{id}.eml.gz`, kjer sta `xx`/`yy` izvedena
iz id sporočila. Vrstica v bazi podatkov shrani le metapodatke.

Izberite to, ko je arhiv dovolj velik, da bi originali prevladovali nad velikostjo baze podatkov,
ali ko želite postaviti datoteke sporočil na cenejš trženjski nosilnik ali ločen shrambni prostor.

!!! danger "Montirajte volumen v Docker"
    Privzeti `BasePath` (`RawMessages`) se razrešuje na `/app/RawMessages` **znotraj
    kontejnerja**. Brez vezanega montaža (bind mount) ta direktorij pripada zapisovalnemu
    slojini kontejnerja, zato vsak ponovno ustvarjeni kontejner - vključno s
    `docker compose down && docker compose up -d`, posodobitev slike ali ponastavitev gostitelja
    z prolazno plastjo - odnese vse shranjeno originalno datoteko, medtem ko vrstice v bazi
    podatkov preživejo.

    Direktorij vedno montirajte na gostitelja, ko uporabljate pogon filesystem:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Gostiteljska pot (`./raw-messages` tukaj) je tisto, ki varnostno kopirate. Pot v kontejnerju mora
    ustrezati `RawMessages:FileSystem:BasePath`, ali pa nastavite `RawMessages:FileSystem:BasePath` na
    izbrani točko montaže. Če premaknete montažo, ohranite `BasePath`, da kaže nanj: shranjene
    vrednosti `StoragePath` so relativne, zato se bodo še naprej pravilno razreševale.

### Sprostitev pogona (Switching backends) {#switching-backends}

Brreadenje sledi shranjenju, zabeleženem na vsaki vrstici, ne pa konfigurirani nastavitvi.
Po spremembi `Storage` ostanejo arhivirana sporočila, ki so bila arhivirana prej, prejemna za prenos
od koli zanje. Nastavitev vpliva le na **novo** arhivirano pošto.

Za premikanje tistega, kar je že shranjeno, zomejite migracijo shrambe. Za celovitoreferenco
preverite [ukaz CLI](CLI.md#moving-stored-original-messages):

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Kaj migracija zagotavlja:

- Vsako sporočilo je preverjeno glede na svojo zabeleženo kontrolno vrednost (checksum), preden se
  kopira. Sporočilo, ki ne uspe, ostane tam, kjer je, in je prijavljeno: okvarjen original nikoli ne
  propagira in njegova edina preostala kopija nikoli ne biti izbrisan.
- Datoteka je izbrisana šele po tem, ko je transakcija baze podatkov, ki jo je premaknila, potrjena (committed),
  tako da prekinjena izvedba ne lahko izgubi edine kopije.
- Proces je lahko nadaljeval (resumable). Delo je izbrano s stolpcem `StorageType`, zato z ponovnim zagonom
  ukaza nadaljuje s tistim, kar še ni bilo obdelovano.
- Izhodna koda je `1`, če katero sporočilo ni uspelo, sicer `0`.

Migracija ni zahtevna za delovanje arhiviranja. Potrebna je le za prenašanje obstoječih
bajt na drug pogon.

## 🛟 Varnovni backup in obnavljanje {#backup-and-restore}

=== "postgres backend"
    Nič dodatnega. Originali so del baze podatkov in so zajeti s standardnim
    izvozom (dump), glejte [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Izvozite direktorij `BasePath` **skupaj z** izvozom baze podatkov.

    Obnova baze podatkov brez datotek povzroči, da arhiv vsebuje vrstice,
    s katerimi ni mogoče delovati. Prenos nato odgovori z HTTP 404 namesto
    tega, da bi vrnil napačno vsebino, e-poštni sporočili pa ostanejo berljiva
    iz svojih razčlenjenih polj.

## 🗑️ Brisanje e-poštne sporočil {#deleting-an-email}

Brisanje e-poštnega sporočila odstrani tudi njegovo izvirno datoteko: pravila o zadrževanju, ročno pojednično in množinsko brisanje ter brisanje celotnega e-poštnega računa vsi odstranijo datoteko kot del iste operacije. S pomočjo `postgres` backend ne treba počela ničesar dodatnega, saj bajti potujejo znotraj vrstice, ki jo kaskadno že odstrani.

Datoteka je izbrisan **po tem**, ko je vrstica e-poštnega sporočila zapišana (committed), nikoli pa pred tem. V obeh korakih ne more biti eno transakcijo, saj datoteka ni del transakcije baze podatkov, zato mora priti eden od njima kasneje. Ta redosled je varen: brisanje, ki ne uspe ali se vrača (rollback), na primer pri zaklenjeni zapisu, pusti izvirnik nedotikojen, medtem ko nesreča med obema korakoma pusti datoteko brez vrstice, ki jo spodaj povrne proces sweep. Drugi red bi uničil izvirnik e-poštnega sporočila, ki še vedno obstaja.

**Zlatovate datoteke se zberejo.** Vse, kar je ušlo, vključno z datotekami, ki jih je pustilo brisanje pred nastavitvijo tega čiščenja, odstrani periodični sweep `BasePath`, ki primerja vsako datoteko z vrsticami in izbriše tiste, na katere se nobeno e-poštno sporočilo ne nanaša:

| Nastavitev | Privzeto | Pomen |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Vklopi ali izklopi sweep |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Kako pogosto se izvaja |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Datoteke, mlajše od tega, se nikoli ne dotikajo |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Odložitev pred prvim zagonom, da se najprej zaključi prek moving storage migracija |

Čas grace period je tisti, ki naredi sweep varnim: uvoz zapiše datoteko, preden zapiše vrstico, ki na njo kaže, in datoteko, ki je bila zapisana pred trenutkom, ne sme zamenjati za zlatovato datoteko. Polovičen zapisano `.tmp` datoteke iz prekinjenega zapisa se zberejo pod istim pravilom. S `postgres` backend sweep nič ne naredi in celo ne preišče direktorija.

## 🔍 Preverjanje arhive {#verifying-an-archive}

Kontrolna vrednost (checksum), zabeležena ob času arhiviranja, omogoča preverjanje.

- **Ob prenosu** (`VerifyChecksumOnRead=true`, privzeto) se datoteka izračuna (hashed) in primerja. Če se vrednosti ne skladajo, se prenos zavrne z napako HTTP 500.
- **Pri migraciji shrambe** se vsaka sporočilo preveri pred kopiranjem, v obeh smerih.

Ker se vrstica metapodatkov nahaja v bazi podatkov, byte pa so lahko na disku, je primerjava kontrolnih vrednosti edini način, da se opazi, če je bila datoteka spremenjena zunaj aplikacije.

## ⚠️ Vesжно obvestilo {#things-to-know}

- **Omemba ne popolnjuje preteklih podatkov.** Le pošiljatelji, ki so arhivirani po preklopitvi, bodo imeli original.
- **Rekonstrukcija ni sporočilo.** Za e-poštna sporočila brez shranjenega originala vsaka pot prenosavanja vrne rekonstruirano sporočilo in to navede prek `X-Raw-Message-Reconstructed: true`.
- **Datoteka pomeni dve enoti varnega shranjevanja.** Baza podatkov in direktorij s sporočili morata biti obnovljena skupaj, sicer zapiši preživijo brez vsebine.
- **`VerifyChecksumOnRead` porablja CPU.** Vsak prenos velikega sporočila je dekomprimiran in izračunan njegov hash. Funkcijo pustite vklopljeno, razen če ste izmerili težavo; to je edina zaščita pred tiho spremenbo na disku.
- **Shranjeni original ni dedupliciran.** Priloga, ki se pojavi v številnih e-poštnih sporočilih, je shranjena enkrat v shrambi prilog, vendar enkrat za vsako e-poštno sporočilo znotraj originala.
- **Brisanje odstrani tudi original.** E-poštno sporočilo, ki je izbrisan zaradi politike zadrževanja ali ročno, odvzame tudi svoj shranjeni original, datoteko in zapis. Nič ne ostane shranjeno, če ga arhiv ne vseže na seznamu.

## 🔗 Poglejte tudi {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - celotna migracija shrambe
- [Setup and Parameters](Setup.md) - vse konfiguracijske parametre
- [REST API](API.md#download-original-message) - `/raw` končališče
- [MCP Server](MCP.md#get_email_raw) - orodje `get_email_raw`
- [Backup and Restore](BackupRestore.md) - kaj varnostno kopirati
- [Attachment Deduplication](AttachmentDeduplication.md) - kako so shranjene priloge
