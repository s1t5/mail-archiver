# 🖥️ CLI parancsok {#cli-commands}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Néhány feladat nem tartozik a webes felülethez: például egy meglévő archívum importálása a helyi fájlrendszərből, vagy a tárolt eredeti üzenetek mozgatása a tároló backendek között. A Mail Archiver mindkét feladatot elfogad parancsként a parancssorban.

| Parancs | Cél |
|----------|---------|
| `--import-mbox` | Egy MBox fájl importálása egy csatlakoztatott könyvtárból |
| `--import-eml` | EML fájlokból álló ZIP archívum importálása egy csatlakoztatott könyvtárból |
| `--migrate-raw-storage` | A tárolt eredeti üzenetek mozgatása az adatbázis és a fájlrendszer között |

Minden parancs a webes folyamat elindítása előtt lefut, és befejezésükkor kilép, így biztonságosan futtathatók `docker compose exec` mellett, miközben az archívum folyamatban van.

## ⚠️ Biztonsági modell {#security-model}

Az importálási parancsok **kizárólag CLI-en** keresztül érhetőek el – nincs webes végpont a böngészéshez vagy a helyi fájlok importálásához. A parancsok futtatásához rendelkeznie kell a Docker host hozzáféréssel (`docker exec`).

Egy **path whitelist** (`LocalImport:AllowedPaths`) szabályozza, hogy mely könyvtárakból olvashat a betöltő. A tých útvonalakon kívüli fájlok elutasításra kerülnek.

A `--migrate-raw-storage` nem tartozik ebbe a fehérlistához: csak azokat az üzenetfájlokat olvassa be, amelyeket magát a архів `RawMessages:FileSystem:BasePath` alatt írt.

## 🛠️ Beállítás {#setup}

### 1. Engedélyezett útvonalak konfigurálása {#1-configure-allowed-paths}

A `docker-compose.yml` fájljában állítsa be az engedélyezett importálási útvonalakat:

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. Fájlok csatoltatása {#2-mount-your-files}

Csatoltassa a MBox/EML fájlokat tartalmazó könyvtárat a konténerbe:

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

A `/home/user/mbox-archives` helyére adja meg a Docker hoston található tényleges útvonalat.

### 3. A konténer újraindítása {#3-restart-the-container}

```bash
docker compose down && docker compose up -d
```

## 📝 Használat {#usage}

### Keresse meg a cél fiók ID-ját {#find-your-target-account-id}

Az importálás előtt szüksége van a cél e-mail fiók adatbázis-ID-jára. Ezt a Web UI segítségével találhatja meg:

Navigáljon az "Email Accounts" menüpontra, nyissa meg a cél fiók részleteit; a fiók ID-ja megjelenik a fiókok listájában vagy az URL-ben, amikor egy fiókot megtekint.

### MBox fájl importálása {#import-an-mbox-file}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**Paraméterek:**
| Paraméter | Kötelező | Leírás |
|-----------|----------|-------------|
| `--import-mbox` | Igen | Jelezi az MBox import műveletet |
| `--file <path>` | Igen | Az MBox fájl elérési útja a konténeren belül |
| `--account-id <id>` | Igen | A cél e-mail fiók adatbázis-ID-ja |
| `--folder <name>` | Nem | Cél mappanev | (alapértelmezett: `INBOX`) |

### EML fájlok importálása (ZIP archívum) {#import-eml-files-zip-archive}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**Paraméterek:**
| Paraméter | Kötelező | Leírás |
|-----------|----------|-------------|
| `--import-eml` | Igen | Jelezi az EML import műveletet |
| `--file <path>` | Igen | Egy **ZIP archívum** elérési útja, amely `.eml` fájlokat tartalmaz a konténeren belül |
| `--account-id <id>` | Igen | A cél e-mail fiók adatbázis-ID-ja |

> **Fontos:** `--import-eml` egy olyan **ZIP archívumot** vár, amely `.eml` fájlokat tartalmaz — egyes `.eml` fájlokat **nem** fogad el. Ahhoz, hogy egyes EML fájlokat importáljon, először csomagolja őket egy ZIP archívumba:
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **Megjegyzés `dotnet run` számára:** `dotnet run` használatakor (a közzétett DLL helyett) a dotnet argumentumait és az alkalmazás argumentumait a `--` karakterrel kell elkülöníteni:
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> A `--` jelzi a `dotnet run` számára, hogy az összes utolsó argumentum az Ön alkalmazásához tartozik, nem pedig a .NET fordítónak.

## 📊 Példa kimenet {#example-output}

```
Target account: user@example.com (ID: 1)

=== Local MBox Import ===
File: /data/import/myarchive.mbox
Size: 245.32 MB
Target Account ID: 1
Target Folder: INBOX

[Importing... progress logged to container logs]

=== Import Results ===
Status: Completed
Total Emails: 15234
Imported Successfully: 15120
Failed: 42
Skipped (malformed): 28
Skipped (duplicates): 44
Duration: 00:12:45
```

## 🔍 Monitorozás {#monitoring}

Az importálás során részletes folyamatjegyzék kerül a konténer logokba:

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ Konfigurációs referencia {#configuration-reference}

| Környezeti változó | Alapértelmezett | Leírás |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | Az első engedélyezett útvonal a helyi importokhoz |
| `LocalImport__AllowedPaths__1` | (üres) | A második engedélyezett útvonal (opcionális) |

Több útvonal hozzáadásához növelje az indexet (`__2`, `__3`, stb.).

## 🚫 Hibakezelés {#error-handling}

| Hiba | Ok | Megoldás |
|-------|-------|----------|
| `File not found` | A `--file` elérési út nem található a konténerben | Ellenőrizze a fájl elérési útját és a volume mount-ot |
| `File path is not in an allowed import directory` | A fájl a fehérlistás útvonalakon kívül található | Ent either move the file into an allowed path or add its directory to `LocalImport:AllowedPaths` |
| `Mail account with ID X not found` | Rossz fiók azonosító | Ellenőrizze a fiók azonosítót a Web UI-n vagy adatbázis lekérdezéssel |
| `Invalid account-id` | Nem numerikus érték a `--account-id` számára | Használjon numerikus adatbázis azonosítót |

## 🔒 Biztonsági legjobb gyakorlatok {#security-best-practices}

1. **Használjon read-only mountokat**, ahol csak lehet:
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **Korlátozza az AllowedPaths** paramétert kizárólag azokra a könyvtárakra, amelyeket ténylegesen használ.

3. **Távolítsa el a volume mountokat** az import befejeztével a tiszta biztonsági felület fenntartása érdekében.

4. **Soha ne tegye elérhetővé** az import könyvtárat a web szerveren keresztül – nincs webes végpont a helyi importokhoz.

## 📦 Import Formátumok Összefoglalója {#import-formats-at-a-glance}

| CLI Flag | Input Format | Typical File Extension | Single Email | Bulk |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | MBox file | `.mbox`, `.mbx` | ❌ (mindig bulk) | ✅ |
| `--import-eml` | ZIP archive of EMLs | `.zip` | ❌ (ZIP szükséges) | ✅ |
| `--migrate-raw-storage` | Stored original messages | — | — | ✅ |

*A single `.eml` fájlok nem támogatottak közvetlenül. Használja a `zip` parancsot a csomagolásukhoz, vagy importáljon a [web upload](Import-EML-MBox.md) keresztül.*

## 🔧 Karbantartási parancsok {#maintenance-commands}

### A tárolt eredeti üzenetek mozgatása {#moving-stored-original-messages}

Amikor a `RawMessages:Enabled` be van kapcsolva, minden e-mail eredeti üzenetét az `RawMessages:Storage`től függően az adatbázisban vagy fájlokban tároljuk. Ez a parancs az már tárolt eredeti üzeneteket egyik backendről a másikra mozgatja; a konfigurációhoz tekintse az [Original Message Storage](RawMessages.md#storage-backend) részt.

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

| Argumentum | Alapértelmezett | Leírás |
|----------|---------|-------------|
| `--from` | konfigurált `RawMessages:Storage` | Amelyik backendből az üzenetek távolodnak el. |
| `--to` | konfigurált `RawMessages:Storage` | Amelyik backendbe az üzenetek kerülnek. |
| `--batch` | `200` | Üzenetek az adatbázis-tranzakciónként. |

Az import parancsokkal ellentétben ez nem függ a `LocalImport:AllowedPaths`-től: csak azokat az üzenetfájlokat olvassa be, amelyeket magát az archívum írt az `RawMessages:FileSystem:BasePath` alá.

**Amit garantál:**

- Minden üzenetet ellenőriz a learchíváláskor rögzített ellenőrzősumma (checksum) alapján a másolás előtt. Az üzenet, amely elbukik, pontosan ott marad, ahol van, és jelentésre kerül — egy sérült eredeti üzenet soha nem kerül tovább, és az egyetlen fennmaradó másolata sem törlődik el.
- Egy fájlt csak akkor törölnek, ha az átmozgatást elvégző adatbázis-tranzakció sikeresen lezárult (commit), így egy megszakadt futtatás nem okozhat egyetlen másolat elvesztését sem.
- A folyamat újraindítható: a munka az egyes üzeneteken rögzített tárolási mód alapján történik meg, így a parancs újraindításakor a még végezetlen feladatokkal folytatja.
- Egy rossz üzenet nem állítja le a folyamatot. A kilépési kód `1`, ha bármilyen üzenet elbukott, különben `0`.

**Példa kimenet:**

```text
Batch: 200 moved, 0 skipped, 0 failed
Batch: 200 moved, 0 skipped, 0 failed
Batch: 42 moved, 0 skipped, 1 failed
Done: 442 moved, 1 failed.
  Email 12345: checksum mismatch (recorded 4f1c..., file has 9ab2...)
```

Az olvasás mindig az egyes üzeneteken rögzített backendet követi, nem pedig a konfigurált beállítást, így a letöltés továbbra is működik a migráció alatt és után is. A `RawMessages:Storage` módosítása csak az újból archivált leveleket érinti — ezt a parancsot futtassa az már tárolt üzenetek mozgatásához.

> **Biztoslat:** a filesystem backend esetén az üzenetfájlok az adatbázison kívül élnek. A `RawMessages:FileSystem:BasePath` elmentését az adatbázis mentésével (dump) együtt végezze; a fájlok nélküli visszaállítás olyan eredeti üzeneteket hagy maga után, amelyeket nem lehet majd elérni.

## ❓ GYIK {#faq}

**K: Miért igényel a `--import-eml` ZIP fájlt?**
A: A zugó EML import szolgáltatás (`EmlImportService`) belsőleg a .NET `ZipFile` API-ját használja a ZIP archívumok feldolgozásához. Ez a kialakítás hatékony tömeges importálást tesz lehetővé. Egyedi `.eml` fájlok esetén csomagolja őket egy ZIP archívumba (pl. `zip emails.zip *.eml`), vagy használja a [webes feltöltést](Import-EML-MBox.md).

**K: Miért nem tudok fájlokat böngészni a webes felületről?**
A: Ez egy tudatos biztonsági tervezés. Ezt a funkciót szándékosan CLI-n keresztül valósítjuk meg a rendszer szintű interakciókhoz, mivel a normál felhasználóknak nincs közvetlen hozzáférésük az OS szintjű rétegekhez; ők csak a Web UI-val kommunikálnak.

A biztonsági kockázatok elkerülése érdekében – mint például a path traversal támadások vagy a jogosulatlan fájlrendszer-expozíció – a rendszeres felhasználók számára a fájlfeltöltés és az importálás a Web UI használatára korlátozott. Ez biztosítja, hogy ne tudják megkerülni a tervezett felületet az alapvető konténer fájlrendszerének eléréséhez.
