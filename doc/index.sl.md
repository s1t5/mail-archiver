# 📧 Dokumentacija Mail Archiver {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Arhiviranje e-pošte na lastni infrastrukturi — arhivirajte, iščite in izvozi pošto iz katerega koli ponudnika.**
Namestite enkrat, shranite vsako sporočilo in ga najdete ponovno čez leta.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Začetek uporabe__

    ---

    Namestite Mail Archiver z Docker Compose in povežite svoj prvi poštni predal.

    [:octicons-arrow-right-24: Nastavitev in parametri](Setup.md)

-   :material-magnify:{ .lg .middle } __Najdi karkoli__

    ---

    Iskanje po celotnem besedilu skozi vse račune, s filtri, sortiranjem in množinskimi dejanji.

    [:octicons-arrow-right-24: Iskanje pošte](Search.md)

-   :material-api:{ .lg .middle } __Avtomatizacija__

    ---

    Preberite svoj arhiv s skripti in AI agenti — API ključi z omejenimi pravicami samo za branje.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Varnost__

    ---

    Dvoslojna avtentikacija, SSO, beleženje dostopa in politike zadrževanja podatkov.

    [:octicons-arrow-right-24: Dvoslojna avtentikacija](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Namestitev in vzdrževanje {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Nastavitev in parametri__

    ---

    Vsaka okoljska spremenljivka, Kestrel HTTPS in upravljanje tajnosti (secrets).

    [:octicons-arrow-right-24: Preberi](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Postavite Mail Archiver za nginx ali Caddy z TLS terminacijo.

    [:octicons-arrow-right-24: Preberi](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __Nadgradnja PostgreSQL__

    ---

    Prehod na novo glavno različico PostgreSQL brez izgube podatkov.

    [:octicons-arrow-right-24: Preberi](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Vzdrževanje baze podatkov__

    ---

    VACUUM, ANALYZE in ohranjanje odzivnosti arhiviranja čez čas.

    [:octicons-arrow-right-24: Preberi](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Privzibavanje in obnavljanje__

    ---

    Privzibakujte bazo podatkov in jo obnovite — vključno s Proxmox nastavitvami.

    [:octicons-arrow-right-24: Preberi](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Lokalno testno okolje__

    ---

    Zaženite začasno instanco lokalno, preden posežete za produkcijo.

    [:octicons-arrow-right-24: Preberi](LocalTestEnvironment.md)

</div>

## 📥 Uvozi pošte {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Uvoz računov__

    ---

    Dodajte več računov hkrati iz datoteke CSV.

    [:octicons-arrow-right-24: Preberi](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Sinhronizacija__

    ---

    Hitra vs. polna sinhronizacija, kontrolne točke in kaj se zgodi, ko sinhronizacija ne uspe.

    [:octicons-arrow-right-24: Preberi](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI ukazi__

    ---

    Uvozite obstoječe arhive iz vrstičnega konzole in premikajte shranjena izvorna
    sporočila med bazo podatkov in datoteko.

    [:octicons-arrow-right-24: Preberi](CLI.md)

-   :material-upload:{ .lg .middle } __Web uvoz (EML / MBox)__

    ---

    Naložite MBox datoteko ali ZIP datotek EML prek brskalnika.

    [:octicons-arrow-right-24: Preberi](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Obvladovanje omejitev hitrosti__

    ---

    Spremljanje pasovnega obsega in način, kako se ustavljene sinhronizacije same nadaljujejo.

    [:octicons-arrow-right-24: Preberi](RateLimitHandling.md)

</div>

## 🔍 Delo z arhivom {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Iskanje pošte__

    ---

    Sintaksa za poizvedbe, filtri, sortiranje, straničenje in množinska dejanja.

    [:octicons-arrow-right-24: Preberi](Search.md)

-   :material-progress-clock:{ .lg .middle } __Stižna opravila__

    ---

    Spremljajte sinhronizacije, uvoze, obnavlave in izvoze — ter jih varno prekličite.

    [:octicons-arrow-right-24: Preberi](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Migracija poštnega predala__

    ---

    Kopirajte pošto med poštna predala ob ohranjanju strukture map.

    [:octicons-arrow-right-24: Preberi](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Izvoz računa (EML / MBox)__

    ---

    Izvozite celoten račun nazaj kot prenosljiv EML ali MBox arhiv.

    [:octicons-arrow-right-24: Preberi](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Shranjevanje izvornih sporočil__

    ---

    Sporočilo shranite tako, kot je bilo prejeto, da DKIM še vedno potrjuje in so izvozi byte-točno identični.

    [:octicons-arrow-right-24: Preberi](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Odpravljanje podvojkov prilog__

    ---

    Kako se identične priloge shranijo enkrat namesto večkrat.

    [:octicons-arrow-right-24: Preberi](AttachmentDeduplication.md)

-   :material-file-pdf-box:{ .lg .middle } __Predogled XRechnung računov__

    ---

    Preberite XRechnung XML priloge kot PDF neposredno v arhivu.

    [:octicons-arrow-right-24: Preberi](EInvoicing.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Preuslanjanje po časovnih oknih__

    ---

    Premikajte starejšo pošto na drug račun, okno po okno.

    [:octicons-arrow-right-24: Preberi](Offload.md)

-   :material-harddisk:{ .lg .middle } __Shranjevanje na račun__

    ---

    Preverite, koliko prostora zaseda vsak poštni predal.

    [:octicons-arrow-right-24: Preberi](AccountStorage.md)

</div>

## 🔐 Administracija in varnost {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Upravljanje uporabnikov__

    ---

    Ustvarite uporabnike, dodelite administrativne pravice, dodelite dovoljenja za poštne predale in spremenite svojo
    geslo.

    [:octicons-arrow-right-24: Preberi](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Dvoslojna avtentikacija__

    ---

    Nastavitev TOTP, nadgraditvene kode in kaj storiti, če izgubite napravo.

    [:octicons-arrow-right-24: Preberi](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Prijava prek Entra ID, Authelia ali drugega ponudnika identitete.

    [:octicons-arrow-right-24: Preberi](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Beleženje dostopa__

    ---

    Kdo je odprl, iskal, izvoзил ali izbrisal kaj.

    [:octicons-arrow-right-24: Preberi](Logs.md)

-   :material-file-export:{ .lg .middle } __Izvoz revizijskih podatkov__

    ---

    Izvozite revizijsko sled za preglede skladnosti.

    [:octicons-arrow-right-24: Preberi](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Nujna obnova računa__

    ---

    Povrazite dostop, ko je račun blokiran.

    [:octicons-arrow-right-24: Preberi](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Politike zadrževanja__

    ---    Avtomatsko brisanje s poštnega strežnika ali iz lokalnega arhiva.

    [:octicons-arrow-right-24: Preberi](RetentionPolicies.md)

</div>

## ☁️ Navodila za ponudnike {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Registracija Azure aplikacije (M365)__

    ---

    Registrirajte aplikacijo in nastavite zadrževanje za Microsoft 365.

    [:octicons-arrow-right-24: Preberi](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __Uvoz poštnih predal M365 tenantov__

    ---

    Mass-import vsakega poštnega predala tenanta iz enega obrazca.

    [:octicons-arrow-right-24: Preberi](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow instance__

    ---

    Izberite poštne predale centralno in zagotovite namenske IMAP gesla za aplikacije.

    [:octicons-arrow-right-24: Preberi](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Osebni Microsoft računi__

    ---

    Outlook.com, Hotmail in M365 Family prek postopka device code flow.

    [:octicons-arrow-right-24: Preberi](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail najboljše prakse__

    ---

    Gesla za aplikacije, oznake, expunge in omejitve hitrosti Gmaila.

    [:octicons-arrow-right-24: Preberi](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Omejitve Yahoo pošte__

    ---

    Kaj Yahoo-jevi IMAP in izvozni kontejner ne bodo naredili.

    [:octicons-arrow-right-24: Preberi](YahooMailExportLimitations.md)

</div>

## 🔌 Programski dostop {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Dostop samo za branje do arhiva s skopiranimi API klipi za posameznega uporabnika.

    [:octicons-arrow-right-24: Preberi](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Razpoložite arhiv za AI agente kot ugotavljive orodje.

    [:octicons-arrow-right-24: Preberi](MCP.md)

</div>

## 🧰 Operacije in razvoj {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Jezik in lokalizacija__

    ---

    Spremenite jezik v vmesniku in kako dodati prevod.

    [:octicons-arrow-right-24: Preberi](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose logi__

    ---

    Preberite loge kontejnerjev in spremenite raven logiranja.

    [:octicons-arrow-right-24: Preberi](DockerComposeLogs.md)

-   :material-heart-pulse:{ .lg .middle } __Zdravje in metrike__

    ---

    Spremljajte liveness, readiness in metrike arhiva iz Prometheus ali drugega sistema za spremljanje.

    [:octicons-arrow-right-24: Preberi](Monitoring.md)

-   :material-tag:{ .lg .middle } __Razvojne različice__

    ---

    Preklopite na razvojno oznako (dev tag) za testiranje prihajajočih sprememb.

    [:octicons-arrow-right-24: Preberi](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Ta dokumentacija se nenehno širi.**
Če opazite napako ali imate predlog, prosim
[odprite težavo (issue)](https://github.com/s1t5/mail-archiver/issues) ali pošljite pull request.
</div>
