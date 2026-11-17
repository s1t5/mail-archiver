# 📧 Original Message Storage {#original-message-storage}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Alapértelmezés szerint az archívum az e-mail **解析t részeit** tárolja: fejléceket, szöveges és HTML törzset, valamint csatolmányokat. Egy exportált `.eml` ezekből a mezőkből épül fel újra.

Ez elegendő egy e-mail elolvasásához, de nem jelenti a **fogadott** üzenetet. Az enkódolások (transfer encodings), a MIME határértékek (boundaries) és a fejléc-összehajtások (header folding) eltérhetnek az eredetitől, így az újraépített üzenet már nem igazolja a DKIM aláírását, a spamszűrők pedig másképp értékelik.

Ha az eredeti üzenet tárolása engedélyezve van, az archívum a teljes üzenetet is megőrzi úgy, ahogy az megérkezett, a解析t mezők mellett. A tárolt eredeti üzenet az, amit az archívum bizonyítani tud, hogy ténylegesen kézbesítve érkezett.

| | Csak解析t mezőkkel | Eredeti üzenet tárolásával |
| --- | --- | --- |
| Fejléc, törzs, csatolmányok | ✅ | ✅ |
| Byte-exact üzenet | ❌ | ✅ |
| DKIM aláírás igazolása | ❌ | ✅ |
| Forensikai célú használat (bíróság, audit) | korlátozott | ✅ |

## 🎯 Hol érhető el az eredeti változat {#where-the-original-is-served}

Tárolás után az eredeti változat mindenhol visszaküldésre kerül, ahol egy teljes üzenetet várnak:

| Hely | Mi történik |
| --- | --- |
| **Export EML** gomb az e-mail részletein | Ha létezik tárolt eredeti, azt szolgálja ki; ha nem, visszavált a rekonstrukcióra az olyan e-mailek esetében, amelyek nélküle kerültek archiválásra. |
| **Restore to a mailbox** | Ha létezik tárolt eredeti, azt illeszti hozzá; ha nem, visszavált a rekonstrukcióra az olyan e-mailek esetében, amelyek nélküle kerültek archiválásra. |
| `GET /api/v1/emails/{id}/raw` | REST API letöltés, lásd a [REST API guide](API.md#download-original-message) dokumentációt. |
| `get_email_raw` | MCP tool, lásd a [MCP guide](MCP.md#get_email_raw) dokumentációt. |

A válasz megmondja, mely formátumot kaptál meg, így a hívó a bájtok vizsgálata nélkül is megkülönböztetheti a valódi eredetit a rekonstruált változatól:

| Jel (Signal) | Jelentés |
| --- | --- |
| `X-Raw-Message-Source: imap` | Pontos másolat az IMAP szerverről (`BODY[]` úgy, ahogy megérkezett). |
| `X-Raw-Message-Source: graph` | Pontos másolat, amit az Exchange MIME tartalma visszaadott. |
| `X-Raw-Message-Source: eml-file` | Pontos másolat, az importált `.eml` fájl tartalma. |
| `X-Raw-Message-Source: serialized` | Megnyilvánított (parsed) üzenet, amelyből újra kiírták. Azonos, kivéve a sorvégek, amik CRLF formátumúak (MBox import, IMAP recovery fetch). |
| `X-Raw-Message-Sha256` | A tárolt eredeti SHA-256 ellenőrzőjegye. |
| `X-Raw-Message-Reconstructed: true` | Nincs tárolt eredeti; az üzenetet újraépítették. Nincs ellenőrzőjegy fejléc. |
| `X-Raw-Message-Storage` | A háttérrendszer, ahonnan az eredetit beolvasták (`postgres` vagy `filesystem`). |

A fájlnév is jelzi: `email-{id}-original-...eml` a tárolt eredeti esetén, `email-{id}-...eml` a rekonstrukció esetén.

Ha a tárolt eredeti **ellenőrzőjegye (checksum) nem egyezik**, az exportálást és a visszaállítási (restore) folyamatot hibaüzenettel elutasítják. Ha helyette a rekonstruált változatot adnák vissza, az elfedné, hogy az archivált másolat módosult, ami pontosan olyasmi, amit egy archívumnak soha nem szabad tennie.

A visszaállítás (restore) beolvassa a tárolt eredetit, és a szolgáltató kliensén keresztül illeszti hozzá, így a postafalak (mailbox) megkapja az eredeti fejléceket és a MIME szerkezetet. A hálózatokon keresztül történő átvitelnél a pontos bájtsorozat nem garantált, mivel a kliens az üzenetet újra serializálja; magát az archivált másolatot azonban nem módosítják.

## ⚙️ Beállítások {#settings}

Minden beállítás a `RawMessages` szekcióban található, vagy környezeti változóként, a `RawMessages__` prefixgel.

| Beállítás | Alapértelmezett | Leírás |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Minden új módon archivált e-mail eredeti üzenetét elmenti. Azon e-mailek, amelyek a bekapcsolás előtt kerültek archiválásba, nem rendelkeznek eredetivel. |
| `RawMessages:MaxMessageSizeMb` | `50` | Ebbe az értékbe nagyobb üzenetek az eredeti tartalmuk nélkül kerülnek archiválásba. `0` = nincs limit. |
| `RawMessages:IncludeGraph` | `true` | Microsoft 365 (Graph) fiókok esetén is elmenti az eredetit. Ez üzenetenként egy további Graph kérést igényel, ami a korlátozási (throttling) kerethez számít. |
| `RawMessages:Storage` | `postgres` | Ahol az üzenetбайtok tárolódnak: `postgres` (az adatbázisban) vagy `filesystem` (gzip fájl az archiválási fájl mellett). Lásd: [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Az az irányítósor, ahová az üzenetfájlokat írják, az alkalmazás tartalomgyökeréhez (content root) viszonyítva. Ezt egy Dockerben csatlakoztatott (mounted) volument mutasson rá. |
| `RawMessages:FileSystem:ShardDepth` | `2` | A két karakterből álló könyvtár-szintek száma, amelyre az e-mail id kiterjed. A `2` 10 000 könyvtárat eredményez, a `0` pedig sík (flat) szerkezetet. Ez alacsonkon tartja a könyvtáronkénti fájlszámot nagy archivumok esetén is. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Minden letöltéskor újra számolja a fájl SHA-256 értékét, és összehasonlítja az archiváláskor rögzített ellenőrzősummával. Az egyezés hiányában a letöltés HTTP 500 hibailleséssel zárul, a módosított байtok szerzése helyett. |

### Méretezés {#sizing}

A tárolás mérete körülbelül minden e-mail méretének megfelelően nő, a csatolásokkal együtt, gzip-komprimálva. A csatolások **nem** kerülnek duplikációmentesítésre (deduplication) a tárolt eredeti üzeneten belül, így egy nagy csatolással rendelkező e-mail kétszer is igényel helyet a csatolásért: egyszer a duplikációmentesített csatoló-tárolóban, egyszer pedig az eredeti üzeneten belül. A tervezésnél számoljon körülbelül **kétszeres** adatbázis-mérettel az eredeti üzenetek nélküli archivumhoz képest.

A beállítás engedélyezése nem tölti vissza az már meglévő e-maileket. Csak az új módon archivált e-mailek kapnak eredeti tartalmat.

## 💾 Tárolási háttér {#storage-backend}

A `Storage` beállítás határozza meg, hogy a byte-ok hol tárolódnak. A **metadata sor mindig az adatbázisban marad**, függetlenül attól, hogy melyik háttérrendszer van konfigurálva, és tartalmazza a SHA-256 értékét, a méretet, a felvétel forrását és (fájlok esetén) a relatív elérési utat. Ez a sor teszi lehetővé az archívum számára, hogy igazolja, mit kellene tartalmaznia egy tárolt üzenetnek.

### postgres (alapértelmezett) {#postgres-default}

A tömörített üzenet a `mail_archiver."RawMessages"` `Content` oszlopában található.
Ez az a viselkedés, ami az archívumot mindig alkalmazta, és nem igényel extra tárhelyet.

Ezt akkor válaszd, ha az adatbázis már a mentési egységed, és egyetlen dolgot szeretnél menteni és visszaállítani.

### filesystem {#filesystem}

Az üzenet a `{BasePath}/{xx}/{yy}/{id}.eml.gz` helyre kerül escrever, ahol a `xx`/`yy` az e-mail id alapján kerül kiaknázásra. Az adatbázis sor csak a metadatokat őrzi.

Ezt akkor válaszd, ha az archívum olyan nagy, hogy az eredeti fájlok dominálnák az adatbázis méretét, vagy ha az üzenetfájlokat olcsóbb vagy külön tárolóeszközre szeretnéd helyezni.

!!! danger "Mount a volume in Docker"
    A gyári `BasePath` (`RawMessages`) a `/app/RawMessages` helyre kerül megoldódva **a konténeren belül**. Bind mount nélkül ez a könyvtár a konténer írható rétegéhez tartozik, így minden újra létrehozott konténer – beleértve a `docker compose down && docker compose up -d` parancsot, egy image frissítést vagy egy ideiglenes réteggel rendelkező host újraindítását – magával viszi minden tárolt eredeti fájlt, miközben az adatbázis sorok életben maradnak.

    Mindig csatlold a könyvtárat a hosthoz, ha a filesystem háttérrendszert használod:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    A host elérési útja (itt `./raw-messages`) az, amit te mentesz. A konténer elérési útnak meg kell egyeznie a `RawMessages:FileSystem:BasePath` értékkel, vagy állítsd a `RawMessages:FileSystem:BasePath` értéket a választott csatolási pontra. Ha mozgatod a csatolást, a `BasePath`-at továbbra is erre muttassa: a tárolt `StoragePath` értékek relatívak, így továbbra is feloldódnak.

### Háttérrendszer váltása {#switching-backends}

Az olvasás az egyes sorokon rögzített tárolási módot követi, nem pedig a konfigurált beállítást. Miután megváltoztatod a `Storage` értékét, a korábban archivált üzenetek továbbra is letölthetők maradnak attól függően, hogy hol vannak. A beállítás csak az **új** archivált levelekre vonatkozik.

A már tárolt adatok mozgatásához futtasd a tárolási migrációt. Tekints a [CLI commands](CLI.md#moving-stored-original-messages) részre a teljes referenciáért:

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Amit a migráció garantál:

- Minden üzenetet ellenőriznek a rögzített ellenőrzősumma alapján a másolás előtt. Az üzenet, amelynél hiba történik, ott marad, ahol van, és jelentésre kerül: egy sérült eredeti fájl soha nem kerül továbbított, és az egyetlen fennmaradó példánya sem törlődik el.
- A fájlt csak azután törlik, hogy az átmozgatást végző adatbázis tranzakció sikeresen lezárult, így egy megszakadt futás nem veszítheti el az egyetlen példányt.
- A folyamat újraindítható. A munka a `StorageType` oszlop alapján történik meg, így a parancs újraindításakor a még feldolgozatlan részekkel folytatja.
- A kilépési kód `1`, ha bármilyen üzenet elbukott, különben `0`.

A migráció nem feltétele az archívum működésének. Csak akkor szükséges, ha a meglévő byte-okat át akarod mozgatni a másik háttérrendszerre.

## 🛟 Mentés és visszaállítás {#backup-and-restore}

=== "postgres backend"
    Nincs plusz teendő. Az eredetiek a részei az adatbázisnak, és a normál
    dump lefedte, lásd a [Backup and Restore](BackupRestore.md) részt.

=== "filesystem backend"
    Mentse a `BasePath` könyvtárat **együttesen** az adatbázis dump-jával.

    Az adatbázis visszaállítása a fájlok nélkül olyan sorokat hagy maga után az archívumban,
    amelyek tartalma nem érhető el. A letöltés ilyenkor HTTP 404 hibát ad vissza ahelyett,
    hogy hibás adatot küldene, az e-mailek pedig továbbra is olvashatók a parzolt mezőikből.

## 🗑️ E-mail törlése {#deleting-an-email}

Egy e-mail törlése az eredeti fájlt is eltávolítja vele együtt: a megőrzési szabályok (retention rules), a manuális egyedi és tömeges törlés, valamint egy teljes e-mail fiók törlése egyేgy folyamat részeként távolítja el a fájlt. A `postgres` backend esetén nincs további teendő, mivel az adatok azon soron belül utaznak, amelyet a cascade törlés már maga is eltávolít.

A fájl az e-mail sor lezárása (**after**) után kerül törlésre, soha nem előtte. A két lépés nem lehet egyetlen tranzakció, mivel a fájl nem része az adatbázis-tranzakciónak, így az egyiknek másodiknak kell következnie. Ez a sorrend a biztonságos: egy törlés, amely elbukik vagy visszavon (rollback) például egy zárolt rekord esetén, az eredetit érintetlenül hagyja, алacsonyabb szintű hiba (crash) a két lépés között pedig egy sor nélküli fájlt hagy maga után, amelyet az alábbi tisztogató folyamat (sweep) visszaszerz. A másik sorrend megsemmisítené egy olyan e-mail eredetijét, amely még létezik.

**Az árva fájlok összegyűjtésre kerülnek.** Minden, ami „átcsúszott”, beleértve a még a tisztogás bevezetése előtti törlésekből maradt fájlokat is, `BasePath` periodikus tisztogása által kerül eltávolításra, amely minden fájlt összehasonlít a sorokkal, és törli azokat, amelyekre már nincs e-mail hivatkozás:

| Beállítás | Alapértelmezett | Jelentés |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | A tisztogás be- vagy kikapcsolása |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Milyen gyakran fut le |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | E ennél fiatalabb fájlok soha nem érintettek |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Késleltetés az első futás előtt, hogy a folyamatban lévő tárolási migráció először befejeződjön |

A kegyles időszak (grace period) teszi a tisztogást biztonságossá: egy importálás ír egy fájlt, mielőtt lezárná azt a sort, amely erre hivatkozik, és egy pillanatok geleden írt fájlt nem szabad árva fájlként azonosítani. A megszakadt írás során félreírt `.tmp` fájlok ugyanazon szabály alapján kerülnek összegyűjtésre. A `postgres` backend esetén a tisztogás semmit nem tesz, és még a könyvtárat sem járja végig.

## 🔍 Egy archívum ellenőrzése {#verifying-an-archive}

Az archívum létrehozásakor rögzített ellenőrzőösszeg (checksum) teszi lehetővé az ellenőrzést.

- **Letöltéskor** (`VerifyChecksumOnRead=true`, az alapértelmezett) a fájl hash-eltetésre kerül és összehasonlításra kerül. Az egyezés hiány esetén a letöltés HTTP 500 hibaillesel elutasításra kerül.
- **A tároló migráció során** minden üzenetet ellenőriznek a másolás előtt, mindkét irányban.

Mivel a metaadat sor az adatbázisban található, a byte-ok pedig a lemezen, az ellenőrzőösszeg-összehasonlítás az egyetlen módja annak, hogy észrevegyéljük, ha egy fájlt a alkalmazáson kívül módosítottak.

## ⚠️ Tudnivalók {#things-to-know}

- **A bekapcsolás nem tölti vissza a régi adatokat.** Csak a kapcsolás után archivált levelek kapnak eredeti példányt.
- **A hiányzó eredeti példány hiba esetén kerül naplózásra.** Ha a rögzítés elutasításra kerül (a fenti üzenet `MaxMessageSizeMb`, nincs használható rögzítés), vagy a beállított backendre történő írás sikertelen, a napló az e-mail azonosítójával és az ok megadásával tartalmaz egy hibát. Az e-mail az eredeti példány nélkül marad az archivumban; nincs automatikus visszafeltöltés és nincs újabb próbálkozás.
- **Az újraépítés nem az eredeti üzenet.** Azoknál az e-maileknél, amelyek nem rendelkeznek tárolt eredeti példánnyal, minden letöltési útvonal egy újraépített üzenetet ad vissza, és ezt `X-Raw-Message-Reconstructed: true` keresztül jelzi.
- **A fájlrendszer két mentési egységet jelent.** Az adatbázist és az üzenet-címeket egyszerre kell visszaállítani, külön otherwise a sorok a tartalmuk nélkül maradnak életben.
- **A `VerifyChecksumOnRead` CPU-t igényel.** Minden nagy üzenet letöltésekor dekomprimálás és hash-elés történik. Ne kapcsolja ki, hacsak nem mérte be, hogy problémát okoz; ez az egyetlen védelem a lemezen történő néma módosítások ellen.
- **A tárolt eredeti példányt nem de-duplikáljuk.** Egy csatolmány, amely számos e-mailben megjelenik, egyszer kerül tárolásra a csatolmány-tárolóban, de minden egyes e-mail esetében egyszer jelenik meg az eredeti példányon belül.
- **A törlés az eredeti példányt is eltávolítja.** Az archiváló szabályok vagy kézi törlése esetén az e-mail magával viszi a tárolt eredeti példányt, a fájlt és a sort is. Semmi sem marad meg, amit az archivum már nem sorol fel.

## 🔗 Lásd tovább {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - a teljes tárolási migráció
- [Setup and Parameters](Setup.md) - összes konfigurációs paraméter
- [REST API](API.md#download-original-message) - a `/raw` végpont
- [MCP Server](MCP.md#get_email_raw) - a `get_email_raw` eszköz
- [Backup and Restore](BackupRestore.md) - mit kell menteni
- [Attachment Deduplication](AttachmentDeduplication.md) - hogyan tárolódnak a csatolmányok
