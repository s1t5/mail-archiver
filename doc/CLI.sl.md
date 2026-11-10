# 🖥️ CLI Commands {#cli-commands}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Nekatere naloge niso primerne za spletni uporabniški vmesnik: uvoz obstoječega arhivskega datoteke iz lokalnega datotečnega sistema ali premikanje shranjenih originalnih sporočil med skladiščnimi zmislmi. Mail Archiver sprejema obe kot ukaz na vrstki ukazov.

| Ukaz | Namen |
|----------|---------|
| `--import-mbox` | Uvozi MBox datoteko iz montiranega direktorija |
| `--import-eml` | Uvozi ZIP arhiv EML datotek iz montiranega direktorija |
| `--migrate-raw-storage` | Premika shranjena originalna sporočila med bazo podatkov in datotečni sistem |

Vse ukaze obdeluje sistem pred začetkom spletnega procesa in se končajo, ko so opravljeni, zato so varni za uporabo z `docker compose exec`, medtem ko arhiv deluje.

## ⚠️ Varnostni model {#security-model}

Ukazi za uvoz so na voljo **izključno prek CLI** – ni spletnega končnega mesta (endpoint) za brskanje ali uvoz lokalnih datotek. Za njihovo izvajanje morate imeti dostop do Docker gostitelja (`docker exec`).

**Seznam dovoljenih poti** (`LocalImport:AllowedPaths`) določa, iz katerih direktorijev lahko uvoznik bere podatke. Datoteke izven teh poti so zavrnjene.

`--migrate-raw-storage` ni zamenjan tega seznama dovoljenih poti: bere le datoteke sporočil, ki jih je sam arhiv zapisal pod `RawMessages:FileSystem:BasePath`.

## 🛠️ Nastavitev {#setup}

### 1. Konfiguracija dovoljenih poti {#1-configure-allowed-paths}

V svojem `docker-compose.yml` nastavite dovoljene uvozne poti:

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. Povezovanje datotek {#2-mount-your-files}

Povežite direktorij, ki vsebuje vaše MBox/EML datoteke, v posledo container:

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

Zamenjajte `/home/user/mbox-archives` z dejško potjo na vašem Docker hostu.

### 3. Ponovni zagon poslednega containerja {#3-restart-the-container}

```bash
docker compose down && docker compose up -d
```

## 📝 Uporaba {#usage}

### Poiščite ID ciljnega računa {#find-your-target-account-id}

Pred uvozom potrebujete ID baze podatkov ciljnega e-poštnega računa. To lahko najdete prek Web UI:

Pojdite na "Email Accounts", odprite podrobnosti ciljnega računa; ID računa je prikazan na seznamu računov ali v URL-ju, ko ogledate račun.

### Uvozi MBox datoteko {#import-an-mbox-file}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**Parametri:**
| Parameter | Zahtevano | Opis |
|-----------|----------|-------------|
| `--import-mbox` | Da | Signalizira operacijo uvoza MBox |
| `--file <path>` | Da | Pot do MBox datoteke znotraj kontejnerja |
| `--account-id <id>` | Da | ID baze podatkov ciljnega e-poštnega računa |
| `--folder <name>` | Ne | Ime ciljnega mapnega imenika (privzeto: `INBOX`) |

### Uvozi EML datoteke (ZIP arhiv) {#import-eml-files-zip-archive}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**Parametri:**
| Parameter | Zahtevano | Opis |
|-----------|----------|-------------|
| `--import-eml` | Da | Signalizira operacijo uvoza EML |
| `--file <path>` | Da | Pot do **ZIP arhivov**, ki vsebuje `.eml` datoteke znotraj kontejnerja |
| `--account-id <id>` | Da | ID baze podatkov ciljnega e-poštnega računa |

> **Pomembno:** `--import-eml` pričakuje **ZIP arhiv**, ki vsebuje `.eml` datoteke — **ne sprejme** posameznih `.eml` datotek. Za uvoz posameznih EML datotek jih najprej zapakirajte v ZIP arhiv:
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **Opomba za `dotnet run`:** Pri uporabi `dotnet run` (namesto objavljene DLL) ločite argumente dotnet od argumentov aplikacije z `--`:
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> `--` pove `dotnet run`, da vsi naslednji argumenti pripadajo vaši aplikaciji, ne .NET kompajlerju.

## 📊 Primer izhoda {#example-output}

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

## 🔍 Spremljanje {#monitoring}

Med uvozom se podrobni napredki zapisujejo v dnevnike (logs) posledi:

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ Referenčni priročnik za konfiguracijo {#configuration-reference}

| Spletna spremenljiva okolja | Z умолчанию | Opis |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | Prva dovoljena pot za lokalne uvoze |
| `LocalImport__AllowedPaths__1` | (prazno) | Druga dovoljena pot (neobvezno) |

Dodajte več poti z povečanjem indeksa (`__2`, `__3` itd.).

## 🚫 Obravnava napak {#error-handling}

| Napaka | Vzrok | Rešitev |
|-------|-------|----------|
| `File not found` | Pot `--file` ne obstaja v kontejnerju | Preverite pot do datoteke in besedišče (volume mount) |
| `File path is not in an allowed import directory` | Datoteka je izven seznamu dovoljenih poti | Datoteko premaknite v dovoljeno pot ali dodajte njeno direktorij v `LocalImport:AllowedPaths` |
| `Mail account with ID X not found` | Napačna ID številka računa | Preverite ID računa prek Web UI ali poizvedbi v bazi podatkov |
| `Invalid account-id` | Neštevilčna vrednost za `--account-id` | Uporabite numerični ID baze podatkov |

## 🔒 Varnostne najboljše prakse {#security-best-practices}

1. **Uporabite montaže samo za branje** kadar je to mogoče:
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **Omejite AllowedPaths** le na direktorije, ki jih dejansko uporabljate.

3. **Odstranite montaže volumov** po končani uvozi, da ohranite čisto varnostno površino.

4. **Nikoli ne raz暴露ite** direktorija za uvoz prek spletnega strežnika – za lokalne uvoze ni nobenega spletnega končnega mesta (endpoint).

## 📦 Pregled formatov uvoza {#import-formats-at-a-glance}

| CLI Flag | Input Format | Typical File Extension | Single Email | Bulk |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | MBox file | `.mbox`, `.mbx` | ❌ (vedno bulk) | ✅ |
| `--import-eml` | ZIP archive of EMLs | `.zip` | ❌ (zahteva ZIP) | ✅ |
| `--migrate-raw-storage` | Stored original messages | — | — | ✅ |

*Posamešne `.eml` datoteke niso neposredno podprte. Uporabite `zip` za njihovo zapakiranje ali pa uvoz izvedite prek [web upload](Import-EML-MBox.md).*

## 🔧 Ukaz za vzdrževanje {#maintenance-commands}

### Premikanje shranjenih originalnih sporočil {#moving-stored-original-messages}

Ko je `RawMessages:Enabled` vklopljen, se originalno sporočilo vsakega e-poštnega sporočila shrani v bazi podatkov ali v datotekah, odvisno od `RawMessages:Storage`. Ta ukaz premika že shranjene originale iz enega strežnika (backend) v drugega; za konfiguracijo poglejte [Original Message Storage](RawMessages.md#storage-backend).

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

| Argument | Z умолчанию | Opis |
|----------|---------|-------------|
| `--from` | konfiguriran `RawMessages:Storage` | Strežnik (backend), iz katerega se sporočila premikajo. |
| `--to` | konfiguriran `RawMessages:Storage` | Strežnik (backend), kam se sporočila premikajo. |
| `--batch` | `200` | Število sporočil na poslovanje z bazo podatkov (database transaction). |

Za razliko od ukazov za uvoz ta ukaz ni omejen s `LocalImport:AllowedPaths`: bere le tiste datoteke sporočil, ki jih je sam arhiv zapisal pod `RawMessages:FileSystem:BasePath`.

**Kaj zagotavlja:**

- Vsako sporočilo se pred kopiranjem preveri glede na kontrolno vrednost (checksum), zabeleženo ob arhiviranju. Sporočilo, ki ne uspe preverjanja, ostane točno tam, kjer je, in je prijavljeno — poškodovan original nikoli ne pride do širjenja in njegova edina preostala kopija nikoli ne biti izbrisana.
- Datoteka je izbrisana šele po tem, ko je poslovanje z bazo podatkov, ki jo je premaknilo, zaključeno (committed), tako da prekinjen proces ne lahko izgubi edine kopije.
- Proces je lahko nadalčovan: delo izberejo shranjeni podatki o shranjevanju pri vsakem sporočilu, zato ponoven zagon ukaza nadaljuje s tistim, kar še ni bilo opravljeno.
- Ebad sporočilo ne prekine celotnega procesa. Izstopna koda je `1`, če katero sporočilo ne uspe, v nasprotnem primeru pa `0`.

**Primer izhoda:**

```text
Batch: 200 moved, 0 skipped, 0 failed
Batch: 200 moved, 0 skipped, 0 failed
Batch: 42 moved, 0 skipped, 1 failed
Done: 442 moved, 1 failed.
  Email 12345: checksum mismatch (recorded 4f1c..., file has 9ab2...)
```

Brandenje vedno sledi strežniku (backend), zabeleženemu pri vsakem sporočilu, ne pa konfigurirani nastavitvi, zato prenos ostane delujoč med in po migraciji. Sprememba `RawMessages:Storage` vpliva le na novo arhivirano pošto — če želite premakniti že shranjeno, zagnajte ta ukaz.

> **Zalogovanje (Backup):** pri strežniku, ki uporablja datoteko (filesystem backend), datoteke sporočil ne obstajajo v bazi podatkov. Zalogirajte `RawMessages:FileSystem:BasePath` skupaj z izvozom baze podatkov; brez datotek ob obnovitvi ostanejo originali, ki jih ni mogoče uporabiti.

## ❓ FAQ {#faq}

**V: Zakaj `--import-eml` zahteva ZIP datoteko?**
O: Podvajajoča se EML uvozna storitev (`EmlImportService`) interno obdeljuje ZIP arhive z uporabo .NET-ovega `ZipFile` API. Ta zasnova omogoča učinkovite masovne uvoze. Za posamezne `.eml` datoteke jih zapakirajte v ZIP arhiv (npr. `zip emails.zip *.eml`) ali uporabite [web upload](Import-EML-MBox.md).

**V: Zakaj ne morem brskati po datotekah prek spletnega uporabniškega vmesnika?**
O: Gre za načrtno varnostno zasnovo. Ta funkcija je namerno implementirana prek CLI za interakcije na ravni sistema, saj običajni uporabniki nimajo neposrednega dostopa do ravni operacijskega sistema; uporabljajo le spletni uporabniški vmesnik.

Da bi preprečili varnostne tveganja, kot so napadi s prehajanjem poti (path traversal attacks) in nepooblaščen raz暴露 (exposure) datotečnega sistema, so običajni uporabniki omejeni na uporabo spletnega uporabniškega vmesnika za nalaganje in uvoz datotek. To zagotavlja, da ne morejo zaobiti namerjenega vmesnika in pridrati do datotečnega sistema v posredovalniku.
