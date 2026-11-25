# 📧 Eredeti üzenetek tárolása {#original-message-storage}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Alapértelmezés szerint az archívum az e-mail **értelmezett részeit** tárolja: fejléceket, szöveges és HTML testadatokat, valamint csatolmányokat. Egy exportált `.eml` ebből a mezőkből épül fel újra.

Ez elegendő egy e-mail elolvasásához, de nem teszi lehetővé az üzenet pontos visszaállítását **így, ahogy megérkezett**. A továbbítási kódolások (transfer encodings), a MIME határértékek (MIME boundaries) és a fejlécösszeomlás (header folding) eltérnek az eredetitől, így az újraépített üzenet már nem igazolja a DKIM aláírását, a spamszűrők pedig másképpen értékelik.

Az eredeti üzenetek tárolása esetén az archívum az egész üzenetet is megőrzi a megérkezés pillanatában, az értelmezett mezők mellett. A tárolt eredeti üzenet az, amit az archívum bizonyítani tud, hogy átfutott.

| | Csak értelmezett mezők | Eredeti üzenet tárolásával |
| --- | --- | --- |
| Fejléc, test, csatolmányok | ✅ | ✅ |
| Pontos byte-szintű üzenet | ❌ | ✅ |
| DKIM aláírás ellenőrzése | ❌ | ✅ |
| Nyomozati célú felhasználás (bíróság, audit) | korlátozott | ✅ |

## 🎯 Hol érhető el az eredeti üzenet {#where-the-original-is-served}

Tárolás után az eredeti üzenet minden olyan helyen visszatér, ahol teljes üzenetre van szükség:

| Hely | Mi történik |
| --- | --- |
| **Export EML** gomb az e-mail részletein | Az eredeti üzenetet szolgálja ki, ha az létezik; ha nem, visszavált a rekonstrukcióra az olyan e-mailek esetén, amelyeknél nem tároltunk eredetit. |
| **Restore to a mailbox** | Az eredeti üzenetet illeszti hozzá, ha az létezik; ha nem, visszavált a rekonstrukcióra az olyan e-mailek esetén, amelyeknél nem tároltunk eredetit. |
| `GET /api/v1/emails/{id}/raw` | REST API letöltés, lásd a [REST API guide](API.md#download-original-message) dokumentációt. |
| `get_email_raw` | MCP tool, lásd a [MCP guide](MCP.md#get_email_raw) dokumentációt. |

A válasz jelzi, melyik formátumot kaptad, így a hívó a byte-ok vizsgálata nélkül is meg tudja különböztetni a valódi eredetit a rekonstruált üzenettől:

| Jel (Signal) | Jelentés |
| --- | --- |
| `X-Raw-Message-Source: imap` | Pontos másolat az IMAP szerverről (`BODY[]` úgy, ahogy érkezett). |
| `X-Raw-Message-Source: graph` | Pontos másolat, a MIME tartalom, amit az Exchange visszaküldött. |
| `X-Raw-Message-Source: eml-file` | Pontos másolat, az importált `.eml` fájl tartalma. |
| `X-Raw-Message-Source: serialized` | Felértékelt (parsed) üzenet, amelyből újra felépítették a formátumot. Azonos, kivéve, hogy a sorvégek CRLF-ek (MBox import, IMAP recovery fetch). |
| `X-Raw-Message-Sha256` | A tárolt eredeti üzenet SHA-256 összegét jelzi. |
| `X-Raw-Message-Reconstructed: true` | Nincs tárolt eredeti; az üzenet újraépült. Nincs ellenőrző összeg (checksum) fejléc. |
| `X-Raw-Message-Storage` | A backend, ahonnan az eredeti üzenetet beolvasta (`postgres` vagy `filesystem`). |

A fájlnév is azt mondja: `email-{id}-original-...eml` a tárolt eredeti üzenethez, `email-{id}-...eml` a rekonstrukcióhoz.

Ha a tárolt eredeti üzenet **ellenőrző összegének (checksum) nem felel meg**, az exportálás és a visszaállítás (restore) hibaüzenettel elutasításra kerül. Ha a rekonstruált üzenetet adná vissza helyette, az elfedné azt, hogy az archivált példány módosítva lett volna, ami éppen az, amit egy archívumnak soha nem szabad tennie.

A visszaállítás (restore) beolvassa a tárolt eredetit, és a szolgáltató kliなján keresztül illeszti hozzá, így a mailbox kapja meg az eredeti fejléceket és a MIME szerkezetet. IMAP esetén a kliens az üzenetet újra serializálja, így a hálózat feletti pontos byte-áramlás nem garantált. Microsoft 365 esetén az archivált byte-ok változatlanul továbbjutnak, mivel a Graph elfogadja az üzenetet MIME formátumban. Magát az archivált példányt soha nem módosítjuk.

## ⚙️ Beállítások {#settings}

Minden beállítás a `RawMessages` szekcióban található, vagy környezeti változóként, a `RawMessages__` prefixszel ellátva.

| Beállítás | Alapértelmezett | Leírás |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Minden új módon archivált e-mail eredeti üzenetét tárolja. Azon e-maileknek, amelyeket a beállítás engedélyezése előtt archiváltak, nincs eredeti üzenetük. |
| `RawMessages:MaxMessageSizeMb` | `50` | E nagyobb üzenetek az eredeti üzenet nélkül kerülnek archiválásba. `0` = nincs limit. |
| `RawMessages:IncludeGraph` | `true` | Microsoft 365 (Graph) fiókok esetén is tárolja az eredeti üzenetet. Ez üzenetenként egy plusz Graph kérést igényel, ami a korlátozási (throttling) keretbe számít. |
| `RawMessages:Storage` | `postgres` | Ahol az üzenetбайtokat tárolja: `postgres` (az adatbázisban) vagy `filesystem` (gzip fájl az archiv mellett). Lásd: [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Az az irányítószáll, ahová az üzenetfájlokat írja, az alkalmazás tartalomgyökeréhez (content root) viszonyítva. A Dockerben ezt egy felszerelt (mounted) volument指áljon rá. |
| `RawMessages:FileSystem:ShardDepth` | `2` | A két karakterből álló dizendirectory szintek száma, amelyre az e-mail azonosító kiterjed. A `2` 10 000 dizendirectory-t ad, a `0` pedig sík (flat) szerkezetet. Ez alacsonyon tartja a dizendirectory-onkénti fájlszámot a nagy archivok esetén. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Minden letöltéskor újra számolja a fájl SHA-256 értékét, és összevetíti az archiváláskor rögzített ellenőrző összeggel (checksum). Az egyezés hiány esetén a letöltés HTTP 500 hibával megszakad, ahelyett, hogy módosított byteokat szolgáltatna. |

### Méretezés {#sizing}

A tároló mérete körülbelül az egyes e-mailek méretének megfelel, a csatolt fájlok (gzip-komprimálva) együtt számítva. A csatolt fájlokat a tárolt eredeti üzeneten belül **nem** végződik deduplikáció, így egy nagy csatolt fájllal rendelkező e-mail kétszer kerül szükségessé: egyszer a deduplikált csatolt fájl tárolóban, egyszer pedig az eredeti üzeneten belül. A tervezésnél körülbelül **kétszeresére** számítsuk az eredeti üzenetek nélküli archiválás adatbázis méretét.

A beállítás engedélyezése nem tölti visszafelé a meglévő e-maileket. Csak az új módon archivált e-mailek kapnak eredeti üzenetet.

## 💾 Tárolási háttérের (Storage backend) {#storage-backend}

A `Storage` beállítás határozza meg, hol élnek a byteok. A **metadata sor mindig az adatbázisban marad**, függetlenül attól, hogy mely háttérrendszert konfigurált, és tartalmazza a SHA-256 értékét, a méretet, a felvételi forrást és (fájlok esetén) a relatív útvonalat. Ez a sor teszi lehetővé az archívum számára, hogy bizonyítsa, mit kell tartalmaznia egy tárolt üzenetnek.

### postgres (alapértelmezett) {#postgres-default}

A tömörített üzenet a `mail_archiver."RawMessages"` `Content` oszlopában található.
Ez az a viselkedés, amely az archívumról mindig isთვის jellemzett, és nem igényel extra tárhelyet.

Ezt akkor válaszd, ha az adatbázis már a mentési egységed, és szeretnél egyetlen dolgot menteni és visszaállítani.

### filesystem {#filesystem}

Az üzenet a `{BasePath}/{xx}/{yy}/{id}.eml.gz` helyre kerül írásra, ahol a `xx`/`yy` az e-mail azonosítóból származik. Az adatbázis sor csak a metaadatokat őrzi.

Ezt akkor válaszd, ha az archívum olyan nagy, hogy az eredeti fájlok dominálnák az adatbázis méretét, vagy ha a message fájlokat olcsóbb vagy külön tárolóra szeretnéd helyezni.

!!! danger "Volume csatolása Dockerben"
    A gyári `BasePath` (`RawMessages`) a konténer **belül** a `/app/RawMessages` helyre oldódik ki. Bind mount nélkül ez a dizájni a konténer írható rétegéhez tartozik, így minden újra létrehozott konténer – beleértve a `docker compose down && docker compose up -d` parancsot, egy image frissítést vagy egy ideiglenes réteggel rendelkező host újraindítást – magával viszi az összes tárolt eredetit, miközben az adatbázis sorok túlélnek.

    Mindig csatold a dizájnt a hosthoz, ha a filesystem háttérrendszert használod:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    A host útvonalat (itt `./raw-messages`) kell mentened. A konténer útvonalnak meg kell egyeznie a `RawMessages:FileSystem:BasePath` értékkel, vagy állítsd be a `RawMessages:FileSystem:BasePath` értéket a választott csatolási pontra. Ha mozgatod a csatolást, a `BasePath` értéket úgy hagyd, hogy arra mutasson: a tárolt `StoragePath` értékek relatívak, így továbbra is feloldhatóak.

### Háttérrendszer váltása {#switching-backends}

Az olvasási műveletek a soronként rögzített tárolási helyet követik, nem pedig a konfigurált beállítást. Miután megváltoztattad a `Storage` értékét, a korábban archivált üzenetek továbbra is letölthetők maradnak attól a helyről, ahol vannak. A beállítás csak az **új** archivált levelekre hatással van.

Ahhoz, hogy a már tárolt elemeket átmozgasd, futtasd a tárolási migrációt. A teljes referenciáért lásd a [CLI parancsokat](CLI.md#moving-stored-original-messages):

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Mit garantál a migráció:

- Minden üzenetet ellenőriznek a rögzített ellenőrzőérték (checksum) alapján a másolás előtt. Ha egy üzenet hibás, ott marad, ahol van, és jelentik: egy sérült eredeti fájlt soha nem terjesztnek tovább, és az egyetlen fennmaradó példányát sem törlik törlésre.
- Egy fájlt csak akkor törölnek, ha az átmozogást végző adatbázis tranzakció sikeresen lezárult (committed), így egy megszakadt futás nem veszítheti el az egyetlen példányt.
- A futás újraindítható. A munka a `StorageType` oszlop alapján történik kiválasztása, így a parancs újraindításával a még feldolgozatlan elemekkel folytatódik.
- A kilépési kód `1`, ha bármely üzenet hibás volt, ellenkező esetben `0`.

A migráció nem feltétele az archívum működésének. Csak akkor szükséges, ha a meglévő byteokat át szeretnéd mozgatni a másik háttérrendszerhez.

## 🛟 Mentés és visszaállítás {#backup-and-restore}

=== "postgres backend"
    Nincs plusz teendő. Az eredetiek aຖານის részét képezik, és a normál
    dump lefedíti őket, lásd [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    A `BasePath` könyvtárat a database dump-al **együtt** mentse vissza.

    A fájlok nélküli adatbázis-visszaállítás olyan sorokat hagy az archívumban,
    melyek tartalma nem érhető el. A letöltés ilyenkor HTTP 404 választ ad a helyes
    helyett, a maguk az e-mailek pedig továbbra is olvashatóak a parsoolt mezőikből.

## 🗑️ Egy e-mail törlése {#deleting-an-email}

Egy e-mail törlésével az eredeti fájl is eltávolításra kerül: a megőrzési szabályok (retention rules), a manuális egyedi és tömeges törlés, valamint egy teljes e-mail fiók törlése mind ugyanazon az operáció részeként távolítják el a fájlt. A `postgres` backend esetén nincs extra teendő, mert a byte-ok azon az oldalon belül utaznak, amelyet a kaskádás törlés már maga is eltávolít.

A fájlt akkor töröljük, ha az e-mail sor már commitálódott, soha előtte. A két lépés nem lehet egyetlen tranzakció, mivel a fájl nem része az adatbázis-tranzakciónak, így az egyiknek másodiknak kell történnie. Ez a sorrend a biztonságos: egy törlés, amely elbukik vagy visszavon (rollback), például egy zárolt rekord esetén, az eredeti fájlt érintetlenül hagyja, алapvetően a két lépés közötti összeomlás (crash) pedig egy olyan fájlt hagy sor nélkül, amelyet az alábbi módszer (sweep) visszaszed. Az ellenkező sorrend egy olyan e-mail eredeti fájlját semmisítené meg, amely még létezik.

**Az elárulási (orphaned) fájlok összegyűjtésre kerülnek.** Minden, ami átment a szűrésen – beleértve azokat a fájlokat is, amelyeket a korábbi törlések hagytak hátra, még azelőtt, hogy ez a tisztítás létezett volna –, a `BasePath` periódikus sweepje által eltávolításra kerül, amely minden fájlt összehasonlít az sorokkal, és törli azokat, amelyekhez már nincs e-mail hivatkozás:

| Beállítás | Alapértelmezett | Jelentés |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | A sweep be- vagy kikapcsolása |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Milyen gyakran fut le |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Ekkelnél fiatalabb fájlokhoz soha nem nyúlunk |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Késleltetés az első futás előtt, hogy egy függőben lévő tároló-migráció először lezáruljon |

A kegyelmes időszak (grace period) teszi a sweepet biztonságossá: egy importálás egy fájlt ír le, mielőtt commitálná azt a sort, amely rá mutat, és egy pillanatok ago leírt fájlt nem szabad árulási fájlnak tekinteni. A megszakadt írás során félreírt `.tmp` fájlok is ugyanazon szabály szerint kerülnek összegyűjtésre. A `postgres` backend esetén a sweep semmit nem tesz, és az irányítótér (directory) átjárására sem teszi.

## 🔍 Egy archívum ellenőrzése {#verifying-an-archive}

Az archíváláskor rögzített ellenőrző összeg (checksum) teszi lehetővé az ellenőrzést.

- **Letöltéskor** (`VerifyChecksumOnRead=true`, az alapértelmezett) a fájl hash-eltetése történik, majd összeссörlíti. A nem egyezés HTTP 500 hibaillesztéssel utasítja el a letöltést.
- **Tároló migráció során** minden üzenetet ellenőrizünk, mielőtt másolnánk, mindkét irányban.

Mivel a metaadat sor az adatbázisban található, a byte-ok pedig a lemezön lehetnek, az ellenőrző összeg összehasonlítása az egyetlen módja annak, hogy észrevegyük, ha egy fájlt az alkalmazáson kívül módosítottak.

## ⚠️ Tudni való dolgok {#things-to-know}

- **A bekapcsolás nem tölti vissza a régit (backfill).** Csak a váltás után archivált levelek kapnak eredeti példányt.
- **A hiányzó eredeti példány hibaként kerül naplózásra.** Amikor a mentés elutasítódik (a fenti üzenet `MaxMessageSizeMb`, nem használható mentés esetén) vagy a mentés a konfigurált backend felé sikertelen, a napló egy hibát tartalmaz az e-mail azonosítóval és a magyarázattal. Az e-mail az archivumban marad az eredeti példány nélkül; nincs automatikus visszatöltés és nincs újradísztítás (retry).
- **A részletoldal jelzi a hiányzó eredeti példányt.** `RawMessages:Enabled` esetén a részletoldal egy figyelmeztetést jelenít meg a fejléc felett. Megkérdezi a tároló backendet, így egy eltűnt fájlrendszeri fájl is jelzett lesz. Az e-mail lista nem tartalmaz soronkénti jelzést; ez túl veelná a státusz oszlopban.
- **A rekonstrukció nem maga az üzenet.** Azok az e-mailek, amelyek nem tárolnak eredeti példányt, minden letöltési út egy újraépített üzenetet ad vissza, és ezt `X-Raw-Message-Reconstructed: true` keresztül jelzi.
- **A fájlrendszer két mentési egységet jelent.** Az adatbázis és az üzenet répertoire két egészen együtt kell visszaállítani, különben a sorok megmaradnak a tartalmuk nélkül.
- **A `VerifyChecksumOnRead` CPU-t igényel.** Minden nagy üzenet letöltésekor dekompresszió történik és hashelés выполняется. Ne kapcsolja ki, hacsak nem mérhető problémát tapasztal; ez az egyetlen védelem a lemezről történő néma módosítások ellen.
- **A tárolt eredeti példány nem de-duplikált.** Egy csatolmány, amely sok e-mailben szerepel, egyszer kerül tárolásra a csatolmány-tárolóban, de minden egyes e-mail esetében egyszer szerepel az eredeti példányon belül.
- **A törlés az eredeti példányt is eltávolítja.** Egy e-mail, amelyet az élettartam (retention) vagy kézzel töröltek, magával viszi a tárolt eredeti példányt is, fájl és sor szinten is. Semmi sem marad meg, amit az archivumnak már nem felsorol.

## 🔗 See also {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - a teljes tároló migráció
- [Setup and Parameters](Setup.md) - összes konfigurációs paraméter
- [REST API](API.md#download-original-message) - a `/raw` endpoint
- [MCP Server](MCP.md#get_email_raw) - a `get_email_raw` eszköz
- [Backup and Restore](BackupRestore.md) - mit kell menteni
- [Attachment Deduplication](AttachmentDeduplication.md) - hogyan tárolódnak a csatolmányok
