# 📧 Original Message Storage {#original-message-storage}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Alapértelmezés szerint az archívum az e-mail **elemzett részeit** tárolja: fejléceket, szöveges és HTML törzset, valamint csatolmányokat. Egy exportált `.eml` ezekből a mezőkből épül fel újra.

Ez elegendő egy e-mail elolvasásához, de nem jelenti a üzenetet **úgy, ahogy megérkezett**. Az átviteli kódolások (transfer encodings), a MIME határértékek (boundaries) és a fejlécek összetűzése (header folding) eltérnek az eredetitől, így az újraépített üzenet már nem igazolja a DKIM aláírását, a spamszűrők pedig másképp értékelik.

Ha az eredeti üzenet tárolása engedélyezve van, az archívum a teljes üzenetet is megőrzi úgy, ahogy az megérkezett, az elemzett mezők mellett. A tárolt eredeti üzenet az, amit az archívum bizonyítani tud, hogy ténylegesen kézbesítve érkezett.

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
| **Export EML** gomb az e-mail részletező oldalán | Ha létezik tárolt eredeti, azt szolgálja ki; ha nem, visszavált az újraépítésre (reconstruction) az olyan e-mailek esetében, amelyeknél nem tároltunk eredetit. |
| **Restore to a mailbox** | Ha létezik tárolt eredeti, azt illeszti hozzá; ha nem, visszavált az újraépítésre az olyan e-mailek esetében, amelyeknél nem tároltunk eredetit. |
| `GET /api/v1/emails/{id}/raw` | REST API letöltés, lásd a [REST API guide](API.md#download-original-message) dokumentációt. |
| `get_email_raw` | MCP tool, lásd a [MCP guide](MCP.md#get_email_raw) dokumentációt. |

A válasz megmondja, mely formátumot kaptad, így a hívó a bájtok vizsgálata nélkül meg tudja különböztetni a valódi eredetit az újraépített változattól:

| Jelzés | Jelentés |
| --- | --- |
| `X-Raw-Message-Source: imap` | Pontos másolat az IMAP szerverről (`BODY[]` úgy, ahogy megérkezett). |
| `X-Raw-Message-Source: graph` | Pontos másolat, amit a MIME content Exchange visszaadott. |
| `X-Raw-Message-Source: eml-file` | Pontos másolat, az importált `.eml` fájl tartalma. |
| `X-Raw-Message-Source: serialized` | Megszakított (parsed) üzenet, amelyből újra írtuk ki. Azonos, kivéve a sorvégek, amik CRLF formátumúak (MBox import, IMAP recovery fetch). |
| `X-Raw-Message-Sha256` | A tárolt eredeti SHA-256 értékét jelzi. |
| `X-Raw-Message-Reconstructed: true` | Nincs tárolt eredeti; az üzenetet újraépítettük. Nincs ellenőrző összeg (checksum) fejléc. |
| `X-Raw-Message-Storage` | A háttérrendszer, ahonnan az eredetit beolvasták (`postgres` vagy `filesystem`). |

A fájlnév is jelzi: `email-{id}-original-...eml` a tárolt eredeti esetén, `email-{id}-...eml` az újraépített változat esetén.

Ha a tárolt eredeti **ellenőrző összege (checksum) nem egyezik**, az exportálást és a visszaállítást hibaüzenettel elutasítjuk. Ha helyette az újraépített változatot adnánk vissza, az elfedné, hogy az archivált másolat módosult, ami pontosan olyasmi, amit egy archívumnak soha nem szabad tennie.

A visszaállítás (restore) beolvassa a tárolt eredetit, majd a szolgáltató kliensén keresztül illeszti hozzá, így a postafalak az eredeti fejléceket és MIME struktúrát kapja meg. A hálózatokon keresztül történő átvitelnél nem garantálható a pontos bájtsorozat, mivel a kliens az üzenetet újra serializálja; magát az archivált másolatot azonban nem módosítjuk.

## ⚙️ Beállítások {#settings}

Minden beállítás a `RawMessages` szekcióban található, vagy környezeti változóként, a `RawMessages__` prefixgel.

| Beállítás | Alapértelmezett | Leírás |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Minden új módon archivált e-mail eredeti üzenetét tárolja. Azon e-mailek, amelyek a bekapcsolás előtt kerültek archiválásba, nem rendelkeznek eredetivel. |
| `RawMessages:MaxMessageSizeMb` | `50` | Eztől nagyobb méretű üzenetek az eredeti tartalmuk nélkül kerülnek archiválásba. `0` = nincs limit. |
| `RawMessages:IncludeGraph` | `true` | Microsoft 365 (Graph) fiókok esetén is tárolja az eredetit. Üzenetenként egy további Graph kérést igényel, ami a korlátozási (throttling) kerethez számít. |
| `RawMessages:Storage` | `postgres` | Ahol az üzenetбайtok tárolódnak: `postgres` (az adatbázisban) vagy `filesystem` (gzip fájl az archiválási fájl mellett). Lásd: [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Az az irányítósor, ahová az üzenetfájlokat írják, az alkalmazás tartalomgyökeréhez viszonyítva. Ezt egy Dockerben csatlakoztatott (mounted) volument mutasson rá. |
| `RawMessages:FileSystem:ShardDepth` | `2` | A két karakterből álló könyvtár-szintek száma, amelyre az e-mail id kiterjed. A `2` 10 000 könyvtárat eredményez, a `0` pedig sík (flat) szerkezetet. Ez alacsonkon tartja a könyvtáronkénti fájlszámot nagy archívumok esetén is. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Minden letöltéskor újra számolja a fájl SHA-256 értékét, és összehasonlítja az archiváláskor rögzített ellenőrzősumával. A nem egyezés esetén a letöltés HTTP 500 hibailles, ahelyett, hogy módosított byteket szolgáltatna. |

### Méretezés {#sizing}

A tároló容量 körülbelül minden e-mail méretével nő, a csatolmányokkal együtt, gzip-komprimált formában. A csatolmányok **nem** kerülnek duplikációra a tárolt eredeti üzeneten belül, így egy nagy csatolmányt tartalmazó e-mail kétszer is igényel helyet: egyszer a duplikációmentes csatolmány-tárolóban, egyszer pedig az eredeti üzeneten belül. A tervezésnél számíts roughly **kétszeres** adatbázis-méretre egy olyan archivumhoz, amely nem tartalmaz eredeti üzeneteket.

A beállítás engedélyezése nem tölti vissza az már meglévő e-maileket. Csak az új módon archivált e-mailek kapnak eredeti üzenetet.

## 💾 Tárolási háttér {#storage-backend}

A `Storage` beállítás határozza meg, hogy a byte-ok hol tárolódnak. A **metadata sor mindig az adatbázisban marad**, függetlenül attól, hogy melyik háttérrendszert választottad, és tartalmazza a SHA-256 értéket, a méretet, a beszerzési forrást és (fájlok esetén) a relatív elérési utat. Ez a sor teszi lehetővé az archívum számára, hogy bizonyítsa, mit kellene tartalmaznia egy tárolt üzenetnek.

### postgres (alapértelmezett) {#postgres-default}

A tömörített üzenet a `mail_archiver."RawMessages"` `Content` oszlopában található.
Ez az a viselkedés, ami az archívumtól eddig is ismert, és nem igényel extra tárhelyet.

Ezt akkor válaszd, ha az adatbázis már a mentési egységed, és szeretnél egyetlen dolgot menteni és visszaállítani.

### filesystem {#filesystem}

Az üzenet a `{BasePath}/{xx}/{yy}/{id}.eml.gz` helyre kerül, ahol a `xx`/`yy` az e-mail id alapján kerül ki. Az adatbázis sor csak a metadatokat őrzi.

Ezt akkor válaszd, ha az archívum olyan nagy, hogy az eredeti fájlok dominálnák az adatbázis méretét, vagy ha külön, olcsóbb tárolóeszközre szeretnéd helyezni az üzenetfájlokat.

!!! danger "Mount a volume in Docker"
    Az alapértelmezett `BasePath` (`RawMessages`) a `/app/RawMessages` helyre kerül a **konténeren belül**. Bind mount nélkül ez a könyvtár a konténer írható rétegéhez tartozik, így minden újra létrehozott konténer – beleértve a `docker compose down && docker compose up -d` parancsot, egy image frissítést vagy egy host újraindítását ideiglenes réteggel – elviszi minden tárolt eredeti fájlt, miközben az adatbázis sorok életben maradnak.

    Mindig csatlakoztasd (mount) a könyvtárat a hostra, ha a filesystem háttérrendszert használod:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    A host elérési út (itt `./raw-messages`) az, amit mentesz. A konténer elérési útnak meg kell egyeznie a `RawMessages:FileSystem:BasePath`-el, vagy állítsd a `RawMessages:FileSystem:BasePath` értéket a választott csatolási pontra. Ha mozgatod a csatolási pontot, a `BasePath`-at továbbra is erre mutassa: a tárolt `StoragePath` értékek relatívak, így továbbra is feloldódnak.

### Háttérrendszer váltása {#switching-backends}

Az olvasás az egyes sorokhoz rögzített tárolási módot követi, nem pedig a konfigurált beállítást. Miután megváltoztattad a `Storage` értékét, a korábban archivált üzenetek továbbra is letölthetők maradnak attól függően, hogy hol vannak. A beállítás csak az **új** archivált leveleket érinti.

Ahhoz, hogy a már tárolt adatokat átmozgasd, futtasd a tárolási migrációt. Tekints a [CLI commands](CLI.md#moving-stored-original-messages) dokumentációra a teljes referenciáért:

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Amit a migráció garantál:

- Minden üzenetet ellenőriznek a rögzített ellenőrzősumma (checksum) alapján a másolás előtt. Az üzenet, amelyik hibás, ott marad, ahol van, és jelentik a hibát: egy sérült eredeti fájl soha nem kerül tovább, és az egyetlen fennmaradó példányát sem törlik el.
- A fájlt csak azután törlik el, hogy az átmozgatást végző adatbázis tranzakció sikeresen lezárult (commit), így egy megszakadt futás nem veszítheti el az egyetlen példányt.
- A folyamat újraindítható. A munka a `StorageType` oszlop alapján történik, így a parancs újraindításával a még feldolgozatlan részekről folytatódik.
- A kilépési kód `1`, ha bármilyen üzenet elhibázódott, különben `0`.

A migráció nem feltétele az archívum működésének. Csak akkor szükséges, ha a meglévő byte-okat a másik háttérrendszerre szeretnéd átmozgatni.

## 🛟 Mentés és visszaállítás {#backup-and-restore}

=== "postgres backend"
    Nincs plusz teendő. Az eredeti fájlok a megvalósított adatbázis részét képezik, és a normál
    mentés (dump) részét képezik, lásd: [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Mentse el a `BasePath` könyvtárat **együttesen** az adatbázis mentésével.

    Az adatbázis visszaállítása a fájlok nélkül olyan sorokat hagy maga után, amelyek tartalma
    nem érhető el. Ilyenkor a letöltés HTTP 404 hibát ad vissza a hibás tartalom helyett,
    és maguk az e-mailek továbbra is olvashatók a parzolt mezőikből.

## 🗑️ E-mail törlése {#deleting-an-email}

Egy e-mail törlésével az eredeti fájlja is eltávolytódik: a megőrzési szabályok (retention rules), a manuális egyedi vagy tömeges törlés, valamint egy teljes e-mail fiók törlése egyేgy folyamat részeként távolítja el a fájlt. A `postgres` backend esetén nincs további teendő, mivel a byte-ok azon soron belül utaznak, amelyet a kaskadálás már törölt.

A fájl az e-mail sor **commit**-elése után kerül törlésre, soha előtte. A két lépés nem lehet egyetlen tranzakció, mivel a fájl nem része az adatbázis-tranzakciónak, így az egyiknek a másodiknak kell lɛnie. Ez a sorrend a biztonságos: egy törlés, amely elbukik vagy visszakerül (roll back), például egy zárolt rekord esetén, érintetlen hagyja az eredetit, a két lépés közötti összeomlás pedig egy sor nélküli fájlt hagy maga után, amelyet az alábbi tisztogás (sweep) visszaszerz. Az ellenkező sorrend egy olyan e-mail erediti fájlját zerstörtaná, amely még létezik.

**Az árvult fájlok összegyűjtésre kerülnek.** Bármi, ami „lecsúszott”, beleértve a még ezen tisztogás létezése előtt végzett törlések által hátrahagyott fájlokat is, `BasePath` periodikus tisztogása által kerül eltávolításra, amely minden fájlt összevet a sorokkal, és törli azokat, amelyekre már semmilyen e-mail nem hivatkozik:

| Beállítás | Alapértelmezett | Jelentés |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | A tisztogás be- vagy kikapcsolása |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Milyen gyakran fut le |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Ebbennél fiatalabb fájlok soha nem érintettek |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Késleltetés az első futás előtt, hogy a folyamatban lévő tárolási migráció először befejeződjön |

A kegyles időszak (grace period) teszi biztonságossá a tisztogást: egy importálás fájlt ír, mielőtt commit-elné az arra mutató sort, és egy pillanatok ago írt fájlt nem szabad árványnak tekinteni. A megszakadt írás során keletkező, félreírt `.tmp` fájlok ugyanazon szabály alapján kerülnek összegyűjtésre. A `postgres` backend esetén a tisztogás semmit nem tesz, és még a könyvtárat sem járja végig.

## 🔍 Egy archívum ellenőrzése {#verifying-an-archive}

Az archíváláskor rögzített ellenőrzőösszeg teszi lehetővé az ellenőrzést.

- **Letöltéskor** (`VerifyChecksumOnRead=true`, az alapértelmezett) a fájl hash-értékét kiszámoljuk és
  összevetjük. Az egyezés hiềnje esetén a letöltés HTTP 500 hibailleséssel megtagad undergoes.
- **A tároló migráció során** minden üzenetet ellenőrizünk a másolás előtt, mindkét
  irányban.

Mivel a metaadat sor az adatbázisban található, a byte-ok pedig a lemezen, az ellenőrzőösszeg-összehasonlítás az egyetlen módja annak, hogy észrevegyük, ha egy fájlt az alkalmazáson kívül módosítottak.

## ⚠️ Tudnivalók {#things-to-know}

- **A bekapcsolás nem tölti vissza a régebbi adatokat.** Csak a kapcsolás után archivált levelek kapnak eredeti példányt.
- **Az újraépítés nem az üzenet.** Azoknál az e-maileknél, amelyeknél nincs tárolt eredeti példány, minden letöltési útvonal egy újraépített üzenetet ad vissza, és ezt `X-Raw-Message-Reconstructed: true` segítségével jelzi.
- **A fájlrendszer két mentési egységet jelent.** Az adatbázist és az üzenet-címtárat együtt kell visszaállítani, külön otherwise a sorok megmaradnak a tartalmuk nélkül.
- **A `VerifyChecksumOnRead` CPU-t fogyaszt.** Minden nagy üzenet letöltésekor dekomprimálás és hash-elés történik. Ne kapcsolja ki, hacsak nem tapasztalt mérhető problémát; ez az egyetlen védelem a lemezen történő néma módosítások ellen.
- **A tárolt eredeti példányt nem többszörös deduplikáció történik.** Egy melléklet, amely számos e-mailben szerepel, egyszer tárolódik a melléklet-tárolóban, de minden egyes e-mail esetében egyszer jelenik meg az eredeti példányon belül.
- **A törlés az eredetit is eltávolítja.** Az archiválási szabályok vagy kézi törlése esetén az e-mail magával viszi a tárolt eredetit is, fájl és sor szinten egyaránt. Semmi sem marad meg, amit az archiváló már nem sorol fel.

## 🔗 See also {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - a teljes tároló migráció
- [Setup and Parameters](Setup.md) - összes konfigurációs paraméter
- [REST API](API.md#download-original-message) - a `/raw` endpoint
- [MCP Server](MCP.md#get_email_raw) - a `get_email_raw` eszköz
- [Backup and Restore](BackupRestore.md) - mit kell menteni vissza
- [Attachment Deduplication](AttachmentDeduplication.md) - hogyan tárolódnak a csatolmányok
