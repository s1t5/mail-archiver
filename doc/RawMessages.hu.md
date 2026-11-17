# 📧 Original Message Storage {#original-message-storage}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Alapértelmezés szerint az archívum az e-mail **elemzett részeit** tárolja: fejléceket, szöveges és HTML törzset, valamint csatolmányokat. Egy exportált `.eml` ezekből a mezőkből épül fel újra.

Ez elegendő egy e-mail elolvasásához, de nem jelenti a **fogadott** üzenetet. Az encoding-ek (átviteli kódolások), a MIME határértékek (boundaries) és a fejléc tördelések eltérhetnek az eredetitől, így az újraépített üzenet már nem igazolja a DKIM aláírását, a spamszűrők pedig másképpen értékelik.

Az eredeti üzenet tárolása bekapcsolásával az archívum a teljes üzenetet is megőrzi úgy, ahogy megérkezett, a feldolgozott mezők mellett. A tárolt eredeti üzenet az, amit az archívum bizonyítani tud, hogy ténylegesen megérkezett.

| | Csak elemzett mezők | Eredeti üzenet tárolásával |
| --- | --- | --- |
| Fejlécek, törzs, csatolmányok | ✅ | ✅ |
| Byte-exact üzenet | ❌ | ✅ |
| DKIM aláírás ellenőrzése | ❌ | ✅ |
| Forensikai célú használat (bíróság, audit) | korlátozott | ✅ |

## 🎯 Hol érhető el az eredeti üzenet {#where-the-original-is-served}

Tárolás után az eredeti üzenet ott érhető el, ahol teljes üzenetre van szükség:

| Hely | Mi történik |
| --- | --- |
| **Export EML** gomb az e-mail részletein | Ha létezik tárolt eredeti, azt szolgálja ki; ha nem, visszavált az újraépítésre (reconstruction) az olyan e-mailek esetében, amelyek nélküle kerültek archiválásra. |
| **Restore to a mailbox** | Ha létezik tárolt eredeti, azt illeszti hozzá; ha nem, visszavált az újraépítésre az olyan e-mailek esetében, amelyek nélküle kerültek archiválásra. |
| `GET /api/v1/emails/{id}/raw` | REST API letöltés, lásd a [REST API guide](API.md#download-original-message) dokumentációt. |
| `get_email_raw` | MCP tool, lásd a [MCP guide](MCP.md#get_email_raw) dokumentációt. |

A válasz megmondja, mely formátumot kaptad, így a hívó a byte-ok vizsgálata nélkül is megkülönbözteti az autentikus eredetit az újraépített változattól:

| Jelzés | Jelentés |
| --- | --- |
| `X-Raw-Message-Source: imap` | Pontos másolat az IMAP szerverről (`BODY[]` úgy, ahogy megérkezett). |
| `X-Raw-Message-Source: graph` | Pontos másolat, amit a MIME content Exchange vissadirált. |
| `X-Raw-Message-Source: eml-file` | Pontos másolat, az importált `.eml` fájl tartalma. |
| `X-Raw-Message-Source: serialized` | Megnyilvánított (parsed) üzenet, amelyből újra írták ki. Azonos, kivéve, hogy a sorvégek CRLF formátumúak (MBox import, IMAP recovery fetch). |
| `X-Raw-Message-Sha256` | A tárolt eredeti SHA-256 ellenőrzőjegye. |
| `X-Raw-Message-Reconstructed: true` | Nincs tárolt eredeti; az üzenetet újraépítették. Nincs ellenőrzőjegy fejléc. |
| `X-Raw-Message-Storage` | A backend, ahonnan az eredetit beolvasták (`postgres` vagy `filesystem`). |

A fájlnév is jelzi: `email-{id}-original-...eml` a tárolt eredeti esetén, `email-{id}-...eml` az újraépített változat esetén.

Ha a tárolt eredeti **ellenőrzőjegye (checksum) nem egyezik**, az export és a restore elutasításra kerül egy hibaüzenettel. Ha helyette az újraépített változatot adnák vissza, az elfedné, hogy az archivált másolat módosult, ami pontosan olyasmi, amit egy archívumnak soha nem szabad tennie.

A restore beolvassa a tárolt eredetit, és a szolgáltató kliensén keresztül illeszti hozzá, így a postafalak (mailbox) megkapja az eredeti fejléceket (headers) és MIME szerkezetet. A hálózaton keresztüli átvitt pontos byte-áramlás nem garantált, mivel a kliens az üzenetet újra serializálja; magát az archivált másolatot azonban nem módosítják.

## ⚙️ Beállítások {#settings}

Minden beállítás a `RawMessages` szekcióban található, vagy környezeti változóként, a `RawMessages__` prefixgel.

| Beállítás | Alapértelmezett | Leírás |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Minden új módon archivált e-mail eredeti üzenetét elmenti. Azon e-mailek, amelyeket a beállítás aktiválása előtt archiváltak, nem rendelkeznek eredetivel. |
| `RawMessages:MaxMessageSizeMb` | `50` | Ezt a méretet meghaladó üzenetek az eredeti üzenet nélkül kerülnek archivásra. `0` = nincs korlát. |
| `RawMessages:IncludeGraph` | `true` | Microsoft 365 (Graph) fiókok esetén is elmenti az eredetit. Minden üzenetnél egy további Graph kérést igényel, ami a korlátozási (throttling) kerethez számít. |
| `RawMessages:Storage` | `postgres` | Ahol az üzenetбайtok tárolódnak: `postgres` (az adatbázisban) vagy `filesystem` (gzip fájl az archiválási fájl mellett). Lásd: [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Az az irányítószó, ahová az üzenetfájlokat írják, az alkalmazás tartalomgyökeréhez viszonyítva. A Docker esetén mutasson egy felcsatlakoztatott (mounted) volumúra. |
| `RawMessages:FileSystem:ShardDepth` | `2` | A két karakterből álló könyvtárfiókok száma, amelyeken az e-mail id szét van osztva. A `2` 10 000 könyvtárat biztosít, a `0` pedig sík (flat) szerkezetet. Ez alacsonkon tartja a könyvtáronkénti fájlszámot nagy archiváknál is. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Minden letöltéskor újra számolja ki a fájl SHA-256 értékét, és összehasonlítja az archiváláskor rögzített ellenőrző összeggel. Az egyezés hiány esetén a letöltés HTTP 500 hibahelyre lép, ahelyett, hogy módosított byteokat szolgáltatna. |

### Méretezés {#sizing}

A tárolás mérete körülbelül minden e-mail méretének megfelelő bővül (a csatolásokkal együtt, gzip-komprimálva). A csatolások **nem** kerülnek duplikációmentesítésre (deduplication) az eredeti üzeneten belül, így egy nagy csatolással rendelkező e-mail kétszer is igényel helyet a csatolásért: egyszer a duplikációmentes csatoló-tárolóban, egyszer pedig az eredeti üzeneten belül. A tervezésnél számoljon el körülbelül **kétszeres** adatbázisméretet az eredeti üzenetek nélküli archivához képest.

A beállítás engedélyezése nem tölti vissza az már meglévő e-maileket. Csak az új módon archivált e-mailek kapnak eredeti üzenetet.

## 💾 Tárolási háttér {#storage-backend}

A `Storage` beállítás határozza meg, hogy a byte-ok hol tárolódnak. A **metadata sor mindig az adatbázisban marad**, függetlenül attól, hogy melyik háttérrendszer van konfigurálva, és tartalmazza a SHA-256 értékét, a méretet, a beszerzési forrást és (fájlok esetén) a relatív elérési utat. Ez a sor teszi lehetővé az archívum számára, hogy igazolja, mit kellene tartalmaznia egy tárolt üzenetnek.

### postgres (alapértelmezett) {#postgres-default}

A tömörített üzenet a `mail_archiver."RawMessages"` `Content` oszlopában található.
Ez az a viselkedés, ami az archívumtól eddig is ismert, és nem igényel extra tárhelyet.

Ezt akkor válaszd, ha az adatbázis már a mentési egységed, és szeretnél egyetlen dolgot menteni és visszaállítani.

### filesystem {#filesystem}

Az üzenet a `{BasePath}/{xx}/{yy}/{id}.eml.gz` helyre kerül escrever, ahol a `xx`/`yy` az e-mail id alapján kerül kiaknázásra. Az adatbázis sor csak a metadatokat őrzi.

Ezt akkor válaszd, ha az archívum olyan nagy, hogy az eredeti üzenetek dominálnák az adatbázis méretét, vagy ha külön, olcsóbb tárolóeszközre szeretnéd helyezni az üzenetfájlokat.

!!! danger "Volume futtatása Dockerben"
    Az alapértelmezett `BasePath` (`RawMessages`) a `/app/RawMessages` helyre oldódik a **konténeren belül**. Bind mount nélkül ez a könyvtár a konténer írható rétegéhez tartozik, így minden újra létrehozott konténer – beleértve a `docker compose down && docker compose up -d` parancsot, egy image frissítést vagy egy ideiglenes réteggel rendelkező host újraindítást – magával viszi minden tárolt eredeti üzenetet, miközben az adatbázis sorok életben maradnak.

    Mindig csatlakoztasd (mount) a könyvtárat a hostra, ha a filesystem háttérrendszert használod:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    A host elérési út (itt `./raw-messages`) az, amit te mentesz. A konténer elérési útnak meg kell egyeznie a `RawMessages:FileSystem:BasePath`-el, vagy állítsd a `RawMessages:FileSystem:BasePath` értéket a választott csatolási pontra. Ha mozgatod a csatolási pontot, a `BasePath`-at továbbra is erre mutassa: a tárolt `StoragePath` értékek relatívak, így továbbra is megfelelően oldódnak fel.

### Háttérrendszer váltása {#switching-backends}

Az olvasás minden egyes sorhoz tartozó tárolási információt követi, nem pedig a konfigurált beállítást. Miután megváltoztatod a `Storage` értékét, a korábban archivált üzenetek továbbra is letölthetők maradnak attól függően, hogy hol vannak. A beállítás csak az **új** archivált levelekre hat ki.

A már tárolt adatok mozgatásához futtasd a tárolási migrációt. Tekints a [CLI commands](CLI.md#moving-stored-original-messages) dokumentációra a teljes referenciáért:

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Amit a migráció garantál:

- Minden üzenetet ellenőriznek a rögzített ellenőrzősumma alapján a másolás előtt. Az üzenet, amely elhibásodik, ott marad, ahol volt, és jelentik a hibát: egy sérült eredeti üzenet soha nem kerül tovább, és az egyetlen fennmaradó példányát sem törlik ki.
- A fájlt csak azután törlik, hogy az átmozgatást végző adatbázis-tranzakció sikeresen lezárult (commit), így egy megszakadt futás nem veszítheti el az egyetlen példányt.
- A folyamat újraindítható. A munka a `StorageType` oszlop alapján történik meg, így a parancs újraindításakor a még feldolgozatlan részekkel folytatja.
- A kilépési kód `1`, ha bármilyen üzenet elhibásodott, különben `0`.

A migráció nem feltétele az archívum működésének. Csak akkor szükséges, ha a meglévő byte-okat a másik háttérrendszerre szeretnéd mozgatni.

## 🛟 Mentés és visszaállítás {#backup-and-restore}

=== "postgres backend"
    Nincs plusz teendő. Az eredeti fájlok a megadott adatbázis részét képezik, és a normál
    mentés (dump) részét képezik, lásd: [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    A `BasePath` könyvtárat **együttesen** mentse vissza az adatbázis mentésével (dump).

    Az adatbázis visszaállítása a fájlok nélkül olyan sorokat hagy maga után az archívumban,
    amelyek tartalma nem érhető el. Ilyenkor a letöltés HTTP 404 hibát ad vissza a hibás adat
    helyett, maguk az e-mailek pedig továbbra is olvashatók a parzolt mezőikből.

## 🗑️ E-mail törlése {#deleting-an-email}

Egy e-mail törlésével az eredeti fájlja is eltávolításra kerül: a megőrzési szabályok (retention rules), a manuális egyedi vagy tömeges törlés, valamint egy teljes e-mail fiók törlése egyేgy folyamat részeként távolítja el a fájlt. A `postgres` backend használatával nincs plusz teendő, mivel az adatok azon soron belül mozognak, amelyet a folyamat (cascade) már törölt.

A fájl az e-mail sor **commit**-elése után kerül törlésre, soha nem előtte. A két lépés nem lehet egyetlen tranzakció, mivel a fájl nem része az adatbázis-tranzakciónak, így az egyiknek a másodiknak kell lennie. Ez a sorrend a biztonságos: egy olyan törlés, amely elbukik vagy visszakerül (rollback), például egy zárolt rekord esetén, érintetlen hagyja az eredetit, a két lépés közötti összeomlás pedig egy sor nélküli fájlt hagy maga után, amelyet az alábbi tisztogás (sweep) visszaszerez. Az ellenkező sorrend egy olyan e-mail eredeti fájlját zerstörtaná meg, amely még létezik.

**Az eláruló (orphan) fájlok összegyűjtésre kerülnek.** Bármi, ami „átcsúszott” – beleértve az ezen tisztogás létezése előtti törlések által hátrahagyott fájlokat is –, `BasePath` periodikus tisztogása által eltávolításra kerül, amely minden fájlt összevet a sorokkal, és törli azokat, amelyekhez egyetlen e-mail sem hivatkozik:

| Beállítás | Alapértelmezett | Jelentés |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | A tisztogás be- vagy kikapcsolása |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Milyen gyakran fut le |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Ebbennél fiatalabb fájlok soha nem érintettek |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Késleltetés az első futás előtt, hogy a folyamatban lévő tárolási migráció először befejeződjön |

A kegyles időszak (grace period) teszi biztonságossá a tisztogást: egy importálás fájlt ír, mielőtt commit-elné az arra mutató sort, és egy pillanatok ago megírt fájlt nem szabad tévesen elárulónak (orphan) tekinteni. A megszakadt írás során félreírt `.tmp` fájlok ugyanazok szabályai szerint kerülnek összegyűjtésre. A `postgres` backend esetén a tisztogás semmit nem tesz, és még a könyvtárat sem járja végig.

## 🔍 Egy archívum ellenőrzése {#verifying-an-archive}

Az archívum létrehozásakor rögzített ellenőrzőösszeg teszi lehetővé az ellenőrzést.

- **Letöltéskor** (`VerifyChecksumOnRead=true`, az alapértelmezett) a fájl hash-eltetése történik, majd összehasonlítják. Az egyezés hiány esetén a letöltés HTTP 500 hibailleséssel elutasításra kerül.
- **A tároló migráció során** minden üzenetet ellenőriznek a másolás előtt, mindkét irányban.

Mivel a metaadat sor az adatbázisban található, a bájtok pedig a lemezen, az ellenőrzőösszeg-összehasonlítás az egyetlen módja annak, hogy észrevegyék, ha egy fájlt a alkalmazáson kívül módosítottak.

## ⚠️ Tudnivalók {#things-to-know}

- **A bekapcsolás nem tölti vissza a korábbi adatokat.** Csak a kapcsolás után archivált levelek kapnak eredeti példányt.
- **A hiányzó eredeti példány hibaként kerül naplóba.** Amikor a rögzítés elutasításra kerül (a fenti üzenet `MaxMessageSizeMb`, nincs használható rögzítés), vagy a konfigurált backendre történő írás sikertelen, a napló az e-mail azonosítójával és a hiba okával együtt rögzíti a hibát. Az e-mail az archivumban marad az eredeti példány nélkül; nincs automatikus visszafeltöltés és nincs újabb kísérlet.
- **A részletoldal jelzi a hiányzó eredeti példányt.** `RawMessages:Enabled` esetén a részletoldal a fejléc felett figyelmeztetést jelenít meg. Megkérdezi a tároló backendet, így egy eltűnt fájlrendszeri fájl is jelzett lesz. Az e-mail listában nincs soronkénti jelzés; túl veelszembelepne a státusz oszlopban.
- **Az újraépítés nem az eredeti üzenet.** Azoknál az e-maileknél, amelyeknél nincs tárolt eredeti példány, minden letöltési útvonal egy újraépített üzenetet ad vissza, és ezt `X-Raw-Message-Reconstructed: true` keresztül tájékoztatja a felhasználót.
- **A fájlrendszer két mentési egységet jelent.** Az adatbázist és az üzenet-címtárat egyszerre kell visszaállítani, külön otherwise a sorok megmaradnak a tartalmuk nélkül.
- **A `VerifyChecksumOnRead` CPU-t igényel.** Minden nagy üzenet letöltésekor dekomprimálás és hash-elés történik. Ne kapcsolja ki, hacsak nem mérhető problémát nem tapasztal, mivel ez az egyetlen védelem a lemezen történő néma módú módosítások ellen.
- **A tárolt eredeti példányt nem vélik duplikációmentesítésre.** Egy melléklet, amely sok e-mailben szerepel, egyszer tárolódik a melléklet-tárolóban, de minden e-mail esetében egyszer szerepel az eredeti példányon belül.
- **A törlés az eredeti példányt is eltávolítja.** Az archiválás szabályai vagy kézi törlése esetén a törölt e-mail magával viszi a tárolt eredeti példányt is, fájl és sor szinten egyaránt. Semmi sem marad meg, amit az archivum már nem listáz.

## 🔗 Lásd tovább {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - a tárolás migrációja részletesen
- [Setup and Parameters](Setup.md) - összes konfigurációs paraméter
- [REST API](API.md#download-original-message) - a `/raw` endpoint
- [MCP Server](MCP.md#get_email_raw) - a `get_email_raw` eszköz
- [Backup and Restore](BackupRestore.md) - mit érdemes menteni
- [Attachment Deduplication](AttachmentDeduplication.md) - hogyan tárolódnak a csatolmányok
