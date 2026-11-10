# 📧 Mail Archiver Dokumentáció {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Saját tárhelyű e-mail archiválás — archiváljon, keressen és exportáljon e-maileket bármely szolgáltatótól.**
Állítsa be egyszer, őrizze meg minden üzenetet, és találja meg újra évekkel később.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Kezdés__

    ---

    Telepítse a Mail Archivert Docker Compose segítségével, és csatolja az első postafiókját.

    [:octicons-arrow-right-24: Beállítások és paraméterek](Setup.md)

-   :material-magnify:{ .lg .middle } __Találjon meg bármit__

    ---

    Teljes szöveges keresés minden fiókban, szűrőkkel, rendezéssel és tömeges műveletekkel.

    [:octicons-arrow-right-24: E-mail keresés](Search.md)

-   :material-api:{ .lg .middle } __Automatizálja__

    ---

    Olvassa el az archívumot szkriptekből és AI ügynökökből — olvasható, korlátozott API kulcsokkal.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Biztonságosítsa__

    ---

    Kétfaktoros hitelesítés, SSO, hozzáférési naplózás és megőrzési irányelvek.

    [:octicons-arrow-right-24: Kétfaktoros hitelesítés](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Telepítés és karbantartás {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Beállítások és paraméterek__

    ---

    Minden környezeti változó, Kestrel HTTPS és titkosítás kezelés.

    [:octicons-arrow-right-24: Olvasás](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Helyezze a Mail Archivert egy nginx vagy Caddy mögé TLS terminálással.

    [:octicons-arrow-right-24: Olvasás](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL frissítés__

    ---

\\Átállás egy új fő PostgreSQL verzióra adatvesztés nélkül.

    [:octicons-arrow-right-24: Olvasás](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Adatbázis karbantartás__

    ---

    VACUUM, ANALYZE és az archívum válaszkészségének fenntartása idő:])

    [:octicons-arrow-right-24: Olvasás](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Mentés és visszaállítás__

    ---

    Adatbázis mentése és visszaállítása — Proxmox beállításokkal együtt is.

    [:octicons-arrow-right-24: Olvasás](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Helyi tesztkörnyezet__

    ---

    Futtasson egy ideiglenes példányt helyileg, mielőtt a term production környezettel foglalkozna.

    [:octicons-arrow-right-24: Olvasás](LocalTestEnvironment.md)

</div>

## 📥 E-mailek bevitele {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Fiók importálás__

    ---

    Több fiókot egyszerre hozzáadása egy CSV fájlból.

    [:octicons-arrow-right-24: Olvasás](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Szinkronizálás__

    ---

    Gyors vs. teljes szinkronizálás, ellenőrzőpontok, és mi történik, ha a szinkron megszakad.

    [:octicons-arrow-right-24: Olvasás](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI parancsok__

    ---

    Létező archívumok importálása parancssorból, és a tárolt eredeti üzenetek mozgatása az adatbázis és a fájlrendszer között.

    [:octicons-arrow-right-24: Olvasás](CLI.md)

-   :material-upload:{ .lg .middle } __Webes importálás (EML / MBox)__

    ---

    MBox fájl vagy EML fájlokból álló ZIP feltöltése a böngészőn keresztül.

    [:octicons-arrow-right-24: Olvasás](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Sebességkorlátozás kezelése__

    ---

    Sávszélesség nyomon követése, és a szüneteltetett szinkronizálások automatikus folytatása.

    [:octicons-arrow-right-24: Olvasás](RateLimitHandling.md)

</div>

## 🔍 Archívummal való munka {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __E-mail keresés__

    ---

    Keresési szintaxis, szűrők, rendezés, oldaltörlés és tömeges műveletek.

    [:octicons-arrow-right-24: Olvasás](Search.md)

-   :material-progress-clock:{ .lg .middle } __Háttérfolyamatok__

    ---

    Szinkronizálások, importálások, visszaállítások és exportok figyelése — és azok biztonságos leállítása.

    [:octicons-arrow-right-24: Olvasás](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Postafiók migráció__

    ---

    E-mailek másolása postafiókok között a mappastruktúra megőrzésével.

    [:octicons-arrow-right-24: Olvasás](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Fiók exportálás (EML / MBox)__

    ---

    Egy teljes fiók kiírása portábil EML vagy MBox archívumként.

    [:octicons-arrow-right-24: Olvasás](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Eredeti üzenetek tárolása__

    ---

    Az üzenet megőrzése úgy, ahogy megérkezett, hogy a DKIM továbbra is érvényesítsen, és az exportok byte-szintűen pontosak legyenek.

    [:octicons-arrow-right-24: Olvasás](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Melléklet de-duplikációja__

    ---

    Hogyan tárolódnak az azonos mellékletek egyszer, ahelyett, hogy többször tárolódnának.

    [:octicons-arrow-right-24: Olvasás](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Dátumal alapozott leültetés__

    ---

    Régebbi e-mailek mozgatása másik fiókba, időszakosan.

    [:octicons-arrow-right-24: Olvasás](Offload.md)

-   :material-harddisk:{ .lg .middle } __Fiókonkénti tárolás__

    ---

    Láthatja, mennyi helyet foglal elfoglal minden postafiók.

    [:octicons-arrow-right-24: Olvasás](AccountStorage.md)

</div>

## 🔐 Adminisztráció és biztonság {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Felhasználói kezelés__

    ---

    Felhasználók létrehozása, admin jogok megadása, postafiók jogosultságok kiosztása és a saját jelszó megváltoztatása.

    [:octicons-arrow-right-24: Olvasás](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Kétfaktoros hitelesítés__

    ---

    TOTP beállítás, mentőkódok, és teendők, ha egy eszköz elveszik.

    [:octicons-arrow-right-24: Olvasás](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Bejelentkezés Entra ID, Authelia vagy más azonosító szolgáltatón keresztül.

    [:octicons-arrow-right-24: Olvasás](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Hozzáférési naplózás__

    ---

Ki nyitott, keresett, exportált vagy törölt mit.

    [:octicons-arrow-right-24: Olvasás](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit adatok exportálása__

    ---

    Az audit nyomvonal exportálása a megfelelőségi ellenőrzésekhez.

    [:octicons-arrow-right-24: Olvasás](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Vészhelyzeti fiók helyreállítása__

    ---

    Hozzáférés visszaszerzése, ha egy fiók kitiltva lett.

    [:octicons-arrow-right-24: Olvasás](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Megőrzési irányelvek__

    ---

    Automatikus törlés az e-mail szerverről vagy a helyi archívumból.

    [:octicons-arrow-right-24: Olvasás](RetentionPolicies.md)

</div>

## ☁️ Szolgáltatói útmutatók {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure app regisztráció (M365)__

    ---

    Alkalmazás regisztrálása és a Microsoft 365 megőrzési beállításainak konfigurálása.

    [:octicons-arrow-right-24: Olvasás](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant postafiók importálás__

    ---

    Egy tenant összes postafiókjának tömeges importálása egyetlen űrlapból.

    [:octicons-arrow-right-24: Olvasás](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow példányok__

    ---

    Postafiókok központi kiválasztása és dedikált IMAP alkalmazási jelszavak biztosítása.

    [:octicons-arrow-right-24: Olvasás](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Személyes Microsoft fiókok__

    ---

    Outlook.com, Hotmail és M365 Family eszközkód-folyamon keresztül.

    [:octicons-arrow-right-24: Olvasás](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail legjobb gyakorlatok__

    ---

    Alkalmazási jelszavak, címkék, expunge és a Gmail sebességkorlátozásai.

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

    Az archívum megjelenítése az AI ügynökök számára felfedezhető eszközökként.

    [:octicons-arrow-right-24: Olvasás](MCP.md)

</div>

## 🧰 Üzemeltetés és fejlesztés {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Nyelv és lokalizáció__

    ---

    A felület nyelvének váltása, és hogyan adjon hozzá fordítást.

    [:octicons-arrow-right-24: Olvasás](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose naplók__

    ---

    Conténer naplók olvasása és a naplózási szint megváltoztatása.

    [:octicons-arrow-right-24: Olvasás](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Fejlesztői verziók__

    ---

    Váltás fejlesztői (dev) tagre a közelgő változások teszteléséhez.

    [:octicons-arrow-right-24: Olvasás](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Ezt a dokumentációt folyamatosan bővítjük.**
Ha hibát talál, vagy van javaslata, kérjük
[nyisson egy issue-t](https://github.com/s1t5/mail-archiver/issues) vagy küldjön egy pull requestet.
</div>
