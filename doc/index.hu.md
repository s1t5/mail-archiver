# 📧 Mail Archiver Dokumentáció {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Saját hosted e-mail archiválás — archiváljon, keressen és exportáljon e-maileket bármely szolgáltatótól.**
Állítsa be egyszer, őrizze meg minden üzenetet, és találja meg újra évekkel később is.
</div>

!!! note "Fordítások"
    Az angol nyelvű dokumentáció a hiteles forrás. Minden más nyelv automatikusan ezekről a oldalakról kerül fordítva, és hibákat tartalmazhat. A nyelv megváltoztatásához használja a fejlécben található nyelvválasztót.

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Kezdés__

    ---

    Telepítse a Mail Archivert Docker Compose segítségével, és csatlakoztassa első e-mail fiókját.

    [:octicons-arrow-right-24: Beállítások és paraméterek](Setup.md)

-   :material-magnify:{ .lg .middle } __Keressen bármit__

    ---

    Teljes szöveges keresés minden fiókban, szűrőkkel, rendezéssel és tömeges műveletekkel.

    [:octicons-arrow-right-24: E-mail keresés](Search.md)

-   :material-api:{ .lg .middle } __Automatizálja__

    ---

    Olvasson az archívumból szkriptek és AI ügynökök segítségével — csak olvasható, korlátozott jogosultságú API kulcsokkal.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Biztonságos kezelés__

    ---

    Kétfaktoros hitelesítés, SSO, hozzáférési naplózás és megőrzési irányelvek.

    [:octicons-arrow-right-24: Kétfaktoros hitelesítés](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Telepítés és karbantartás {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Beállítások és paraméterek__

    ---

    Minden környezeti változó, Kestrel HTTPS és titkosítási kezelés.

    [:octicons-arrow-right-24: Olvasás](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---    Helyezze a Mail Archivert egy nginx vagy Caddy mögé TLS terminációval.

    [:octicons-arrow-right-24: Olvasás](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL frissítés__

    ---

    Váltson új, fő verziós PostgreSQL verzióra adatvesztés nélkül.

    [:octicons-arrow-right-24: Olvasás](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Adatbázis karbantartás__

    ---

    VACUUM, ANALYZE és az archívum gyorsaságának fenntartása idő:])

    [:octicons-arrow-right-24: Olvasás](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Mentés és visszaállítás__

    ---

    Adatbázis mentése és visszaállítása — Proxmox beállításokkal együtt is.

    [:octicons-arrow-right-24: Olvasás](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Helyi teszt környezet__

    ---

    Futtasson egy ideiglenes példányt helyileg, mielőtt a term production környezettel dolgozna.

    [:octicons-arrow-right-24: Olvasás](LocalTestEnvironment.md)

</div>

## 📥 E-mailek bevezetése {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Fiók importálás__

    ---

    Több fiókot egyszerre adjon hozzá egy CSV fájlból.

    [:octicons-arrow-right-24: Olvasás](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Szinkronizálás__

    ---

    Gyors vs. teljes szinkronizálás, ellenőrző pontok (checkpoints), és mi történik, ha a szinkronizálás megszakad.

    [:octicons-arrow-right-24: Olvasás](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI parancsok__

    ---

    Létező architektúrák importálása parancssorból, és a tárolt eredeti üzenetek mozgatása az adatbázis és a fájlrendszer között.

    [:octicons-arrow-right-24: Olvasás](CLI.md)

-   :material-upload:{ .lg .middle } __Webes importálás (EML / MBox)__

    ---

    MBox fájl vagy EML fájlokból álló ZIP feltöltése a böngészőn keresztül.

    [:octicons-arrow-right-24: Olvasás](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Sebességkorlátozás kezelése__

    ---

    Sávszélesség figyelés, és hogyan folytatódnak meg a szüneteltetett szinkronizálások automatikusan.

    [:octicons-arrow-right-24: Olvasás](RateLimitHandling.md)

</div>

## 🔍 Archívum kezelése {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __E-mail keresés__

    ---

    Keresési szintaxis, szűrők, rendezés, oldaltörlés (paging) és tömeges műveletek.

    [:octicons-arrow-right-24: Olvasás](Search.md)

-   :material-progress-clock:{ .lg .middle } __Háttérfolyamatok__

    ---

    Szinkronizálások, importálások, visszaállítások és exportok figyelése — és azok biztonságos leállítása.

    [:octicons-arrow-right-24: Olvasás](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __E-mail fiók migráció__

    ---

    E-mailek másolása e-mail fiókok között a mappastruktúra megőrzésével.

    [:octicons-arrow-right-24: Olvasás](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Fiók exportálás (EML / MBox)__

    ---

    Egy teljes fiókot exportáljon vissza portálható EML vagy MBox archívumként.

    [:octicons-arrow-right-24: Olvasás](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Eredeti üzenet tárolása__

    ---

    Tartsa meg az üzenetet úgy, ahogy megérkezett, hogy a DKIM továbbra is érvényesítsen, és az exportok byte-szinten pontosak legyenek.

    [:octicons-arrow-right-24: Olvasás](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Melléklet de-duplikáció__

    ---

    Hogyan tárolódnak az azonos mellékletek egyszer ahelyett, hogy többször.

    [:octicons-arrow-right-24: Olvasás](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Időintervallum alapú elhelyezés (offload)__

    ---

    Régebbi e-mailek mozgatása egy másik fiókba, időintervallumonként.

    [:octicons-arrow-right-24: Olvasás](Offload.md)

-   :material-harddisk:{ .lg .middle } __Fiókonkénti tárolás__

    ---

    Láthatja, mennyi helyet foglal el minden egyes e-mail fiók.

    [:octicons-arrow-right-24: Olvasás](AccountStorage.md)

</div>

## 🔐 Adminisztráció és Biztonság {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Felhasználói kezelés__

    ---

    Felhasználók létrehozása, adminisztrátori jogok megadása, e-mail fiók jogosultságok kiosztása és a saját jelszó megváltoztatása.

    [:octicons-arrow-right-24: Olvasás](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Kétfaktoros hitelesítés__

    ---

    TOTP beállítás, biztonsági kódok és teendők, ha egy eszköz elveszett.

    [:octicons-arrow-right-24: Olvasás](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Bejelentkezés Entra ID, Authelia vagy más identitásmegelőzően keresztül.

    [:octicons-arrow-right-24: Olvasás](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Hozzáférés naplózása__

    ---

    Ki nyitott meg, keresett, exportált vagy törölt mit.

    [:octicons-arrow-right-24: Olvasás](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit adatok exportálása__

    ---

    Exportálja az audit nyomfolapot a megfelelőségi vizsgálatokhoz.

    [:octicons-arrow-right-24: Olvasás](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Vészhelyzeti fiók helyreállítása__

    ---

    Hozzáférés visszaszerzése, ha egy fiók kitiltásra került.

    [:octicons-arrow-right-24: Olvasás](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Megőrzési irányelvek__

    ---

    Automatikus törlés az e-mail szerverről vagy a helyi archívumból.

    [:octicons-arrow-right-24: Olvasás](RetentionPolicies.md)

</div>

## ☁️ Szolgáltatói útmutatók {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure alkalmazás regisztráció (M365)__

    ---

    Regisztráljon egy alkalmazást és konfigurálja a megőrzést a Microsoft 365 számára.

    [:octicons-arrow-right-24: Olvasás](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant e-mail fiók importálás__

    ---

    Egyetlen űrlapon keresztül egy tenant minden e-mail fiókját tömegesen importálja be.

    [:octicons-arrow-right-24: Olvasás](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow példányok__

    ---

    Válasszon e-mail fiókat központilag, és biztosítson dedikált IMAP alkalmazási jelszavakat.

    [:octicons-arrow-right-24: Olvasás](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Személyes Microsoft fiókok__

    ---

    Outlook.com, Hotmail és M365 Family a device code flow segítségével.

    [:octicons-arrow-right-24: Olvasás](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail legjobb gyakorlatok__

    ---

    Alkalmazási jelszavak, címkék, expunge és a Gmail sebességhatárai.

    [:octicons-arrow-right-24: Olvasás](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail korlátozások__

    ---

    Amit a Yahoo IMAP és export végpontjai nem tesznek meg.

    [:octicons-arrow-right-24: Olvasás](YahooMailExportLimitations.md)

</div>

## 🔌 Programozható hozzáférés {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Csak olvasható hozzáférés az archívumhoz, felhasználónként korlátozott API kulcsokkal.

    [:octicons-arrow-right-24: Olvasás](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Tegye az archívumot elérhető eszközként az AI ügynökök számára.

    [:octicons-arrow-right-24: Olvasás](MCP.md)

</div>

## 🧰 Üzemeltetés és fejlesztés {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Nyelv és lokalizáció__

    ---

    A felület nyelvének váltása, és hogyan adjon hozzá egy fordítást.

    [:octicons-arrow-right-24: Olvasás](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose logok__

    ---

    Olvasson konténer logokat, és változtassa meg a log szintet.

    [:octicons-arrow-right-24: Olvasás](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Fejlesztői verziók__

    ---

    Váltson fejlesztői tagre a közelgő változások teszteléséhez.

    [:octicons-arrow-right-24: Olvasás](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Ez a dokumentáció folyamatosan bővül.**
Ha hibát talál, vagy van javaslata, kérjük
[nyisson meg egy issue-t](https://github.com/s1t5/mail-archiver/issues) vagy küldjön egy pull request-et.
</div>
