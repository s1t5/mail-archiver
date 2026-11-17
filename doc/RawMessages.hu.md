# 📧 Original Message Storage {#original-message-storage}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Alapértelmezés szerint az archívum az e-mail **elemzett részeit** tárolja: fejléceket, szöveges és HTML törzset, valamint csatolmányokat. Egy exportált `.eml` ezekből a mezőkből épül fel újra.

Ez elegendő egy e-mail elolvasásához, de nem jelenti a **fogadott** üzenetet. Az átviteli kódolások (transfer encodings), a MIME határértékek (boundaries) és a fejléc-összehajtások (header folding) eltérhetnek az eredetitől, így az újraépített üzenet már nem igazolja a DKIM aláírását, a spamszűrők pedig másképp értékelik.

Az eredeti üzenet tárolása bekapcsolásával az archívum a teljes üzenetet is megőrzi, amint az megérkezett, a feldolgozott mezők mellett. A tárolt eredeti üzenet az, amit az archívum bizonyítani tud, hogy ténylegesen kézhez kapott.

| | Csak elemzett mezők | Eredeti üzenet tárolásával |
| --- | --- | --- |
| Fejléc, törzs, csatolmányok | ✅ | ✅ |
| Byte-exact üzenet | ❌ | ✅ |
| DKIM aláírás ellenőrzése | ❌ | ✅ |
| Forensikai célú használat (bíróság, audit) | korlátozott | ✅ |

## 🎯 Hol érhető el az eredeti üzenet {#where-the-original-is-served}

Tárolás után az eredeti üzenet ott érhető el, ahol teljes üzenetre van szükség:

| Hely | Mi történik |
| --- | --- |
| **Export EML** gomb az e-mail részletező oldalán | Ha létezik tárolt eredeti, azt szolgálja ki; ha nem, visszavált az újraépítésre (reconstruction) az olyan e-mailek esetében, amelyek nélküle kerültek archiválásra. |
| **Restore to a mailbox** | Ha létezik tárolt eredeti, azt illeszti hozzá; ha nem, visszavált az újraépítésre az olyan e-mailek esetében, amelyek nélküle kerültek archiválásra. |
| `GET /api/v1/emails/{id}/raw` | REST API letöltés, lásd a [REST API guide](API.md#download-original-message) dokumentációt. |
| `get_email_raw` | MCP tool, lásd a [MCP guide](MCP.md#get_email_raw) dokumentációt. |

A válasz megmondja, mely formátumot kaptad, így a hívó a byte-ok vizsgálata nélkül is megkülönbözteti a valódi eredetit az újraépített változattól:

| Jelzés | Jelentés |
| --- | --- |
| `X-Raw-Message-Source: imap` | Pontos másolat az IMAP szerverről (`BODY[]` úgy, ahogy megérkezett). |
| `X-Raw-Message-Source: graph` | Pontos másolat, amit az Exchange MIME tartalma visszaadott. |
| `X-Raw-Message-Source: eml-file` | Pontos másolat, az importált `.eml` fájl tartalma. |
| `X-Raw-Message-Source: serialized` | Felásolt üzenet, amelyből újra írták ki. Azonos, kivéve, hogy a sorvégek CRLF formátumúak (MBox import, IMAP recovery fetch). |
| `X-Raw-Message-Sha256` | A tárolt eredeti SHA-256 ellenőrzőjegye. |
| `X-Raw-Message-Reconstructed: true` | Nincs tárolt eredeti; az üzenetet újraépítették. Nincs checksum header. |
| `X-Raw-Message-Storage` | A backend, ahonnan az eredetit beolvasták (`postgres` vagy `filesystem`). |

A fájlnév is jelzi: `email-{id}-original-...eml` a tárolt eredeti esetén, `email-{id}-...eml` az újraépített változat esetén.

Ha a tárolt eredeti **ellenőrzőjegye (checksum) nem egyezik**, az exportálást és a visszaállítási (restore) folyamatot hibaüzenettel elutasítják. Ha az újraépített változatot adnák vissza helyette, az elfedné, hogy az archivált másolat módosult, ami pontosan olyasmi, amit egy archívumnak soha nem szabad tennie.

A visszaállítás (restore) beolvassa a tárolt eredetit, és a szolgáltató kliensén keresztül illeszti hozzá, így a postafalak (mailbox) megkapja az eredeti fejléceket (headers) és a MIME szerkezetet. A hálózatokon keresztül történő átvitelnél nem garantálható a pontos byte-áramlás, mivel a kliens az üzenetet újra serializálja; maga az archivált másolat azonban változatlan marad.

## ⚙️ Beállítások {#settings}

Minden beállítás a `RawMessages` szekcióban található, vagy környezeti változóként, a `RawMessages__` prefixgel.

| Beállítás | Alapértelmezett | Leírás |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Minden új módon archivált e-mail eredeti üzenetét elmenti. Azon e-mailek, amelyeket a bekapcsolás előtt archiváltak, nem rendelkeznek eredetivel. |
| `RawMessages:MaxMessageSizeMb` | `50` | Eztől nagyobb méretű üzenetek az eredeti tartalom nélkül kerülnek archivásra. `0` = nincs korlát. |
| `RawMessages:IncludeGraph` | `true` | Microsoft 365 (Graph) fiókok esetén is elmenti az eredetit. Minden üzenetnél egy további Graph kérést igényel, ami a korlátozási (throttling) kerethez számít. |
| `RawMessages:Storage` | `postgres` | Ahol az üzenetбайtok tárolódnak: `postgres` (az adatbázisban) vagy `filesystem` (gzip fájl az archiv mellett). Lásd: [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Az az irányítósor, ahová az üzenetfájlokat írják, az alkalmazás tartalomgyökeréhez (content root) viszonyítva. Ezt egy Dockerben csatlakoztatott (mounted) volument mutasson rá. |
| `RawMessages:FileSystem:ShardDepth` | `2` | A két karakterből álló könyvtár-szintek száma, amelyre az e-mail id oszlik. A `2` 10 000 könyvtárat eredményez, a `0` pedig sík (flat) szerkezetet. Ez alacsonkon tartja a könyvtáronkénti fájlszámot nagy archivumok esetén is. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Minden letöltéskor újra számolja a fájl SHA-256 értékét, és összehasonlítja az archiváláskor rögzített ellenőrző összeget (checksum) ezzel. Az egyezés hiány esetén a letöltés HTTP 500 hibailleséssel megszakad, ahelyett, hogy módosított байtokat szolgáltatna. |

### Méretezés {#sizing}

A tárolás mérete körülbelül minden e-mail méretének megfelelő bővül, a csatolásokkal együtt, gzip-komprimált formában. A csatolások **nem** kerülnek dedupikálásra a tárolt eredeti üzeneten belül, így egy nagy csatolással rendelkező e-mail kétszer is igényel helyet a csatolás méretének megfelelő: egyszer a dedupikált csatolás-tárolóban, egyszer pedig az eredeti üzeneten belül. A tervezésnél számítsanak körülbelül **kétszeres** adatbázis-méretre az eredeti változat nélküli archivumhoz képest.

A beállítás engedélyezése nem tölti vissza az már meglévő e-maileket. Csak az új e-mailek kapnak eredeti tartalmat.

## 💾 Tárolási háttér {#storage-backend}

A `Storage` beállítás határozza meg, hogy a byte-ok hol tárolódnak. A **metadata sor mindig az
adatbázisban marad**, függetlenül attól, hogy melyik háttérrendszer van konfigurálva, és tartalmazza a SHA-256, a méret, a felvétel forrás és (fájlok esetén) a relatív elérési utat. Ez a sor teszi lehetővé az archívum számára, hogy bizonyítsa, mit kellene tartalmaznia egy tárolt üzenetnek.

### postgres (alapértelmezett) {#postgres-default}

A tömörített üzenet a `mail_archiver."RawMessages"` `Content` oszlopában található.
Ez az a viselkedés, ami az archívumtól eddig mindig várt, és nem igényel extra tárhelyet.

Ezt akkor válaszd, ha az adatbázis már a mentési egységed, és csak egyetlen dolgot szeretnél menteni és visszaállítani.

### filesystem {#filesystem}

Az üzenet a `{BasePath}/{xx}/{yy}/{id}.eml.gz` helyre kerül escrever, ahol az `xx`/`yy` az e-mail id alapján kerül ki. Az adatbázis sor csak a metadatokat őrzi.

Ezt akkor válaszd, ha az archívum olyan nagy, hogy az eredeti fájlok dominálnák az adatbázis méretét, vagy ha az üzenetfájlokat olcsóbb vagy külön tárolóeszközre szeretnéd helyezni.

!!! danger "Mount a volume in Docker"
    A gyári `BasePath` (`RawMessages`) a `/app/RawMessages` helyre kerül **a konténeren belül**. Bind mount nélkül ez a könyvtár a konténer írható rétegéhez tartozik, így minden újra létrehozott konténer – beleértve a `docker compose down && docker compose
    up -d` parancsot, egy image frissítést vagy egy host újraindítását is, amely ideiglenes réteggel rendelkezik – elviszi minden tárolt eredeti fájlt, miközben az adatbázis sorok életben maradnak.

    Mindig csatlakoztasd (mount) a könyvtárat a hostra, ha a filesystem háttérrendszert használod:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    A host elérési út (itt `./raw-messages`) az, amit te mentesz. A konténer elérési útnak meg kell egyeznie a `RawMessages:FileSystem:BasePath`-el, vagy állítsd a `RawMessages:FileSystem:BasePath` értéket a választott csatolási pontra. Ha mozgatod a csatolást, a `BasePath`-at továbbra is erre irányítsd: a tárolt `StoragePath` értékek relatívak, így továbbra is feloldódnak.

### Háttérrendszer váltása {#switching-backends}

Az olvasás az egyes sorokon rögzített tárolási módot követi, nem pedig a konfigurált beállítást. Miután megváltoztatod a `Storage` értékét, a korábban archivált üzenetek továbbra is letölthetők maradnak attól függően, hogy hol vannak. A beállítás csak az **új** archivált levelekre vonatkozik.

Ahhoz, hogy a már tárolt adatokat átmozgasd, futtasd a tárolási migrációt. Tekints a
[CLI commands](CLI.md#moving-stored-original-messages) részre a teljes referenciáért:

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Amit a migráció garantál:

- Minden üzenetet ellenőriznek a rögzített ellenőrzősumma alapján a másolás előtt. Az üzenet, amely hibás, ott marad, ahol van, és jelentésre kerül: egy sérült eredeti fájl soha nem kerül továbbított, és az egyetlen megmaradt példányát sem törlik el.
- A fájlt csak azután törlik el, miután az átmozgatást végző adatbázis tranzakció sikeresen lezárult, így egy megszakadt futás nem veszítheti el az egyetlen példányt.
- A folyamat folytatható. A munka a `StorageType` oszlop alapján történik meg, így a parancs újraindítása ott folytatja, ahol eddig nem volt kész.
- A kilépési kód `1`, ha bármilyen üzenet hibás volt, különben `0`.

A migráció nem feltétele az archívum működésének. Csak akkor szükséges, ha a meglévő byte-okat a másik háttérrendszerre szeretnéd átmozgatni.

## 🛟 Mentés és visszaállítás {#backup-and-restore}

=== "postgres backend"
    Nincs plusz teendő. Az eredeti üzenetek a részei az adatbázisnak, és a normál
    mentés (dump) részét képezik, lásd: [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Mentse el a `BasePath` könyvtárat **egyaránt** az adatbázis mentésével (dump) együtt.

    Az adatbázis visszaállítása a fájlok nélkül olyan sorokat hagy maga után az archívumban,
    amelyek tartalma nem érhető el. Ilyenkor a letöltés HTTP 404 hibát ad vissza a hibás adat
    helyett, az e-mailek pedig továbbra is olvashatók a parzolt mezőikből.

## 🗑️ E-mail törlése {#deleting-an-email}

Egy e-mail törlésével az eredeti fájl is eltávolításra kerül: a megőrzési szabályok (retention rules), a manuális egyedi vagy tömeges törlés, valamint egy teljes e-mail fiók törlése egyేgy folyamat részeként távolítja el a fájlt. A `postgres` backend esetén nincs további teendő, mivel az adatok azon soron belül mozognak, amelyet a kaskadálás már törölt.

A fájl az e-mail sor **commit**-elése után kerül törlésre, soha előtte. A két lépés nem lehet egyetlen tranzakció, mivel a fájl nem része az adatbázis-tranzakcióknak, ezért az egyiküknek a másodiknak kell lennie. Ez a sorrend a biztonságos: egy törlés, amely elbukik vagy visszakerül (roll back), például egy zárolt rekord esetén, az eredeti fájlt érintetlenül hagyja, алacsonyabb szintű hiba a két lépés között pedig egy sor nélküli fájlt hagy maga után, amelyet az alábbi tisztogás (sweep) visszaszerz. Az ellenkező sorrend egy olyan e-mail eredeti fájlját zerstörtaná meg, amely még létezik.

**Az eláruló (orphaned) fájlok összegyűjtésre kerülnek.** Bármi, ami „átcsúszott”, beleértve a még a tisztogás bevezetése előtti törlések által hátra hagyott fájlokat is, `BasePath` periodikus tisztogása által kerül eltávolításra, amely minden fájlt összevet a sorokkal, és törli azokat, amelyekhez egyetlen e-mail sem mutat rá:

| Setting | Default | Meaning |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Kapcsolja be vagy ki a tisztogást |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Milyen gyakran fut le |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Ebbennél fiatalabb fájlok soha nem érintettek |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Késleltetés az első futás előtt, hogy egy folyamatban lévő tárolási migráció előbb befejeződjön |

A kegyles időszak (grace period) teszi biztonságossá a tisztogást: egy importálás fájlt ír le, mielőtt commit-elné az arra mutató sort, és egy pillanatok ago megírt fájlt nem szabad tévesen árulni elárulónak. A megszakadt írás során félreírt `.tmp` fájlok is ugyanazon szabály alapján kerülnek összegyűjtésre. A `postgres` backend esetén a tisztogás semmit nem tesz, és még a könyvtárat sem járja végig.

## 🔍 Egy archívum ellenőrzése {#verifying-an-archive}

Az archívum létrehozásakor rögzített ellenőrző összeg (checksum) teszi lehetővé az ellenőrzést.

- **Letöltéskor** (`VerifyChecksumOnRead=true`, az alapértelmezett) a fájl hash-eltetésre kerül és összehasonlításra kerül. Az egyezés hiány esetén a letöltés HTTP 500 hibailleséssel megtagadódik.
- **A tároló migráció során** minden üzenetet ellenőrizünk a másolás előtt, mindkét irányban.

Mivel a metaadat sor az adatbázisban található, a byte-ok pedig a lemezen, az ellenőrző összeg összehasonlítása az egyetlen módja annak, hogy észrevegyük, ha egy fájlt a alkalmazáson kívül módosítottak.

## ⚠️ Tudnivalók {#things-to-know}

- **A bekapcsolás nem tölti vissza a régi adatokat.** Csak a kapcsolás után archivált leveleknél lesz elérhető az eredeti változat.
- **A hiányzó eredeti üzenetet hiba esetén logolja.** Ha a rögzítés elutasításra kerül (a fenti üzenet `MaxMessageSizeMb`, nincs használható rögzítés), vagy a beállított backendre történő írás sikertelen, a log tartalmaz egy hibát az e-mail azonosítóval és a hiba okával. Az e-mail az archivában marad az eredeti változat nélkül; nincs automatikus visszafeltöltés és nincs újradátaozás.
- **A felület jelzi a hiányzó eredetit.** A `RawMessages:Enabled` használatával az e-mail lista minden olyan üzenetnél figyelmeztető jelzést mutat, amelyhez nem tárolódott eredeti változat, a részletoldalon pedig figyelmeztetés jelenik meg a fejléc felett. A lista a jelzést az oldalakonként egyetlen lekérdezéssel oldja fel; a részletoldal megkérdezi a tároló backendet, így egy eltűnt fájlrendszeri fájl is jelzett lesz.
- **Az újraépítés nem az üzenet.** Azoknál az e-maileknél, amelyekhez nem tárolódott eredeti változat, minden letöltési útvonal egy újraépített üzenetet ad vissza, és ezt `X-Raw-Message-Reconstructed: true` segítségével jelzi.
- **A fájlrendszer két mentési egységet jelent.** Az adatbázist és az üzenet-címeket egy időben kell visszaállítani, külön otherwise a sorok életben maradnak a tartalmuk nélkül.
- **A `VerifyChecksumOnRead` CPU-t igényel.** Minden nagy üzenet letöltésekor dekomprimálás és hash-elés történik. Ne kapcsolja ki, hacsak nem mérhető problémát nem tapasztal ⋯; ez az egyetlen védelem a lemezen történő néma módosítások ellen.
- **A tárolt eredeti változat nem kerül dedupikációra.** Egy csatolmány, amely számos e-mailben szerepel, egyszer tárolódik a csatolmány-tárolóban, de minden e-mailnél egyszer a saját eredeti változatában.
- **A törlés az eredetit is eltávolítja.** Az archiválás szabályai vagy kézi törlése esetén az e-mail magával viszi a tárolt eredeti változatát is, fájl és sor szintén. Semmi sem marad meg, amit az archiváló már nem sorol fel.

## 🔗 Lásd tovább {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - a teljes tárolási migráció
- [Setup and Parameters](Setup.md) - összes konfigurációs paraméter
- [REST API](API.md#download-original-message) - a `/raw` endpoint
- [MCP Server](MCP.md#get_email_raw) - a `get_email_raw` eszköz
- [Backup and Restore](BackupRestore.md) - mit kell menteni
- [Attachment Deduplication](AttachmentDeduplication.md) - hogyan tárolódnak a csatolmányok
