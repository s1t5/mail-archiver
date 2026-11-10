# 📧 Dokumentacija Mail Archiver {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Samostojno arhiviranje e-pošte — arhivirajte, israjte in izvozite pošto iz katerega koli ponudnika.**
Nastavite enkrat, obdržite vsako sporočilo in ga ponovno najdite čez leta.
</div>

!!! note "Prevodi"
    Angleška dokumentacija je avtentični vir. Vsak drug jezik je samodejno preveden s teh strani in lahko vsebuje napake. Za spremembo jezika uporabite preklopnik jezikov v glavi.

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Začetek uporabe__

    ---

    Namestite Mail Archiver z Docker Compose in povežite svoj prvi poštni predal.

    [:octicons-arrow-right-24: Nastavitev in parametri](Setup.md)

-   :material-magnify:{ .lg .middle } __Najdite karkoli__

    ---

    Iskanje po celotnem besedilu skozi vsako račun, s filtri, sortiranjem in množičnimi dejanji.

    [:octicons-arrow-right-24: Iskanje pošte](Search.md)

-   :material-api:{ .lg .middle } __Avtomatizacija__

    ---

    Berite svoj arhiv s skripti in AI agenti — API ključi z omejenimi pravicami (read-only).

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Varnost__

    ---

    Dvajček vezana avtentikacija, SSO, beleženje dostopa in politike shovanjja.

    [:octicons-arrow-right-24: Dvajček vezana avtentikacija](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Instalacija in vzdrževanje {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Nastavitev in parametri__

    ---

    Vsaka okoljska spremenljivka, Kestrel HTTPS in upravljanje tajnosti.

    [:octicons-arrow-right-24: Beri](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Postavite Mail Archiver za nginx ali Caddy z zaključevanjem TLS.

    [:octicons-arrow-right-24: Beri](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL nadgradnja__

    ---

    Prehod na novo glavno različico PostgreSQL brez izgube podatkov.

    [:octicons-arrow-right-24: Beri](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Vzdrževanje baze podatkov__

    ---

    VACUUM, ANALYZE in ohranjanje odzivnosti arhiviranja čez čas.

    [:octicons-arrow-right-24: Beri](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Privetkovanje in obrestavljanje__

    ---

    Privetkajte bazo podatkov in jo obnovite — vključno s Proxmox nastavitvami.

    [:octicons-arrow-right-24: Beri](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Lokalno testno okolje__

    ---

    Zaženite začasno različico lokalno, preden se dotaknete produkcije.

    [:octicons-arrow-right-24: Beri](LocalTestEnvironment.md)

</div>

## 📥 Spretikanje pošte {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Uvoz računov__

    ---

    Dodajte številne račune hkrati iz datoteke CSV.

    [:octicons-arrow-right-24: Beri](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Sinhronizacija__

    ---

    Hitra vs. polna sinhronizacija, kontrolne točke in kaj se zgodi, ko sinhronizacija ne uspe.

    [:octicons-arrow-right-24: Beri](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI ukazi__

    ---

    Uvezite obstoječe arhive iz ukaznega reda in premikajte shranjena originalna
    sporočila med bazo podatkov in datoteko.

    [:octicons-arrow-right-24: Beri](CLI.md)

-   :material-upload:{ .lg .middle } __Web uvoz (EML / MBox)__

    ---

    Naložite MBox datoteko ali ZIP datotek EML prek brskalnika.

    [:octicons-arrow-right-24: Beri](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Obvladovanje omejitev hitrosti__

    ---

    Sledenje širini hrbotnika in kako se pavzirane sinhronizacije same nadaljujejo.

    [:octicons-arrow-right-24: Beri](RateLimitHandling.md)

</div>

## 🔍 Delo z arhivom {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Iskanje pošte__

    ---

    Sintaksa poizvedbe, filtri, sortiranje, strani in množična dejanja.

    [:octicons-arrow-right-24: Beri](Search.md)

-   :material-progress-clock:{ .lg .middle } __Stroji v ozadju__

    ---

    Spremljajte sinhronizacije, uvoze, obrestovanja in izvoze — in jih varno prekličite.

    [:octicons-arrow-right-24: Beri](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Migracija poštovnega predala__

    ---

    Kopirajte pošto med poštovnimi predali ob ohranjanju strukture map.

    [:octicons-arrow-right-24: Beri](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Izvoz računa (EML / MBox)__

    ---

    Izvozite cel račun nazaj kot prenosljivo EML ali MBox arhiv.

    [:octicons-arrow-right-24: Beri](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Shranjevanje originalnih sporočil__

    ---

    Sporočilo obdržite v takšni obliki, kot je bilo prejeto, da DKIM še vedno preverja in izvozi byte-to-byte natančne podatke.

    [:octicons-arrow-right-24: Beri](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Odpravljanje podvojenih prilog__

    ---

    Kako se identične priloge shranijo enkrat namesto večkrat.

    [:octicons-arrow-right-24: Beri](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Preusidanje po datumu__

    ---

    Premikajte starejšo pošto na drug račun, obdobje po obdobje.

    [:octicons-arrow-right-24: Beri](Offload.md)

-   :material-harddisk:{ .lg .middle } __Shranjevanje na račun__

    ---

    Preverite, kolikega prostora zaseda vsak poštovni predal.

    [:octicons-arrow-right-24: Beri](AccountStorage.md)

</div>

## 🔐 Administracija in varnost {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Upravljanje uporabnikov__

    ---

    Ustvarite uporabnike, dodelite admin pravice, določite dovoljenja za poštovni predal in spremenite svojo
    geslo.

    [:octicons-arrow-right-24: Beri](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Dvajček vezana avtentikacija__

    ---

    Nastavitev TOTP, rezervne kode in kaj storiti, ko izgubite napravo.

    [:octicons-arrow-right-24: Beri](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Prijava prek Entra ID, Authelia ali drugega ponudnika identitete.

    [:octicons-arrow-right-24: Beri](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Beleženje dostopa__

    ---

    Kdo je odprl, iskal, izvozil ali izbrisal kaj.

    [:octicons-arrow-right-24: Beri](Logs.md)

-   :material-file-export:{ .lg .middle } __Izvoz revizijskih podatkov__

    ---

    Izvozite sled revizije za skladnost.

    [:octicons-arrow-right-24: Beri](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Nujno okrevanje računa__

    ---    Dobite dostop nazaj, ko je račun blokiran.

    [:octicons-arrow-right-24: Beri](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Politiike shovanjja__

    ---

    Avtomatsko brisanje s poštnega strežnika ali lokalnega arhiv samega.

    [:octicons-arrow-right-24: Beri](RetentionPolicies.md)

</div>

## ☁️ Vodniki ponudnikov {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Registracija aplikacije Azure (M365)__

    ---

    Registrirajte aplikacijo in nastavite shovanjje za Microsoft 365.

    [:octicons-arrow-right-24: Beri](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __Uvoz poštovnega predala M365 tenant__

    ---

    Vmasujte vse poštovne predale tega tenantom hkrati preko enega obrazca.

    [:octicons-arrow-right-24: Beri](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow instance__

    ---

    Centralno izberite poštovne predale in pripravite namenske IMAP gesle za aplikacije.

    [:octicons-arrow-right-24: Beri](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Osebni Microsoft računi__

    ---

    Outlook.com, Hotmail in M365 Family preko procesa device code flow.

    [:octicons-arrow-right-24: Beri](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail najboljše prakse__

    ---

    Gesla za aplikacije, oznake, izbris (expunge) in omejitve hitrosti Gmaila.

    [:octicons-arrow-right-24: Beri](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Omejitve Yahoo pošte__

    ---

    Kaj IMAP in izvozni končnici Yahoo ne bodo naredili.

    [:octicons-arrow-right-24: Beri](YahooMailExportLimitations.md)

</div>

## 🔌 Programski dostop {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Dostop do arhiv samega za branje s sklopiranimi API klji po uporabniku.

    [:octicons-arrow-right-24: Beri](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Razpostavite arhiv za AI agente kot razrešljive orodje.

    [:octicons-arrow-right-24: Beri](MCP.md)

</div>

## 🧰 Operacije in razvoj {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Jezik in lokalizacija__

    ---

    Spremeba jezika v vmesniku in kako dodati prevod.

    [:octicons-arrow-right-24: Beri](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose dnevniki__

    ---

    Berite dnevnike kontejnerja in spremenite raven beleženja.

    [:octicons-arrow-right-24: Beri](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Razvojne različice__

    ---

    Preklopite na dev tag za testiranje prihajajočih sprememb.

    [:octicons-arrow-right-24: Beri](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Ta dokumentacija se nenehno razširja.**
Če opazite napako ali imate predlog, prosimo,
[odprite vprašanje](https://github.com/s1t5/mail-archiver/issues) ali pošljite pull request.
</div>
