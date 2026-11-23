# 📧 Mail Archiver Dokumentáció {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Saját tárhelyű email archiválás — archiváljon, keressen és exportáljon leveleket bármely szolgáltatótól.**
Állítsa be egyszer, őrizze meg minden üzenetet, és találja meg újra évek később.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Kezdés__

    ---

    Telepítse a Mail Archivert Docker Compose segítségével, és csatlakoztassa az első levélzárat.

    [:octicons-arrow-right-24: Beállítások és paraméterek](Setup.md)

-   :material-magnify:{ .lg .middle } __Keressen bármit__

    ---

    Teljes szövegkeresés minden fiókon keresztül, szűrőkkel, rendezéssel és tömeges műveletekkel.

    [:octicons-arrow-right-24: Mail keresés](Search.md)

-   :material-api:{ .lg .middle } __Automatizálja__

    ---

    Olvassa ki az archivált adatokat szkriptekből és AI ügynökökből — csak olvasható, korlátozott API kulcsokkal.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Biztonságosítsa__

    ---

    Kétfaktoros hitelesítés, SSO, hozzáférési naplózás és megőrzési szabályok.

    [:octicons-arrow-right-24: Kétfaktoros hitelesítés](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Telepítés és Karbantartás {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Beállítások és paraméterek__

    ---

    Minden környezeti változó, Kestrel HTTPS és titkosítás kezelés.

    [:octicons-arrow-right-24: Olvasd el](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Helyezze a Mail Archivert nginx vagy Caddy mögé TLS terminációval.

    [:octicons-arrow-right-24: Olvasd el](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL frissítés__

    ---

    Váltás új, fő verziós PostgreSQL verzióra adatvesztés nélkül.

    [:octicons-arrow-right-24: Olvasd el](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Adatbázis karbantartás__

    ---

    VACUUM, ANALYZE és az archiv gyorsaságának fenntartása időbbi folyamat során.

    [:octicons-arrow-right-24: Olvasd el](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Mentés és visszaállítás__

    ---

    Az adatbázis mentése és visszaállítása — beleértve a Proxmox konfigurációkat is.

    [:octicons-arrow-right-24: Olvasd el](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Lokal tesztkörnyezet__

    ---

    Futtasson egy ideiglenes példányt helyileg, mielőtt a term production környezettel dolgozna.

    [:octicons-arrow-right-24: Olvasd el](LocalTestEnvironment.md)

</div>

## 📥 Beáramló levelek {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Fiók importálás__

    ---

    Több fiókot egyszerre hozzáadása egy CSV fájlból.

    [:octicons-arrow-right-24: Olvasd el](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Szinkronizálás__

    ---

    Gyors vs. teljes szinkronizálás, ellenőrző pontok (checkpoints), és mi történik, ha egy szinkronization megszakad.

    [:octicons-arrow-right-24: Olvasd el](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI parancsok__

    ---

    A meglévő architektúrák importálása parancssorból, és a tárolt eredeti üzenetek mozgatása az adatbázis és a fájlrendszer között.

    [:octicons-arrow-right-24: Olvasd el](CLI.md)

-   :material-upload:{ .lg .middle } __Webes importálás (EML / MBox)__

    ---

    MBox fájl vagy EML fájlokból álló ZIP feltöltése a böngészőn keresztül.

    [:octicons-arrow-right-24: Olvasd el](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Sebességkorlátozás kezelése__

    ---

    Bandwidth követés, és hogyan folytatódnak meg a szüneteltetett szinkronizálások automatikusan.

    [:octicons-arrow-right-24: Olvasd el](RateLimitHandling.md)

</div>

## 🔍 Az archiv kezelése {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Mail keresés__

    ---

    Keresési szintaxis, szűrők, rendezés, oldaltörlés és tömeges műveletek.

    [:octicons-arrow-right-24: Olvasd el](Search.md)

-   :material-progress-clock:{ .lg .middle } __Háttérfeladatok__

    ---

    Szinkronizálások, importok, visszaállítások és exportok figyelése — és azok biztonságos leállítása.

    [:octicons-arrow-right-24: Olvasd el](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Levélzárak migrációja__

    ---

    Level mássolása levélzárak között a mappastruktúra megőrzésével.

    [:octicons-arrow-right-24: Olvasd el](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Fiók exportálás (EML / MBox)__

    ---

    Egy teljes fiók k导出álása portabilis EML vagy MBox archivként.

    [:octicons-arrow-right-24: Olvasd el](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Eredeti üzenetek tárolása__

    ---

    Az üzenet megőrzése úgy, ahogy megérkezett, hogy a DKIM továbbra is érvényes legyen, és az exportok byte-pontosak legyenek.

    [:octicons-arrow-right-24: Olvasd el](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Mellékletek de-duplikációja__

    ---

    Hogyan tárolódnak az azonos mellékletek egyszer, ahelyett hogy többször tárolódnának.

    [:octicons-arrow-right-24: Olvasd el](AttachmentDeduplication.md)

-   :material-file-pdf-box:{ .lg .middle } __XRechnung számla előnézet__

    ---

    XRechnung XML mellékletek PDF-ként való megjelenítése közvetlenül az archivban.

    [:octicons-arrow-right-24: Olvasd el](EInvoicing.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Időintervallumos elhelyezés (offload)__

    ---

    Régebbi levelek mozgatása egy másik fiókba, időintervallumokban.

    [:octicons-arrow-right-24: Olvasd el](Offload.md)

-   :material-harddisk:{ .lg .middle } __Fiókonkénti tárolás__

    ---

    Láthatja, mennyi helyet foglal el minden egyes levélzára.

    [:octicons-arrow-right-24: Olvasd el](AccountStorage.md)

</div>

## 🔐 Adminisztráció és Biztonság {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Felhasználói kezelés__

    ---

    Felhasználók létrehozása, admin jogok megadása, levélzárakhoz tartozó engedélyek kiosztása és a saját jelszó megváltoztatása.

    [:octicons-arrow-right-24: Olvasd el](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Kétfaktoros hitelesítés__

    ---

    TOTP beállítás, biztonsági kódok, és mi a teendő, ha egy eszköz elveszik.

    [:octicons-arrow-right-24: Olvasd el](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Bejelentkezés Entra ID, Authelia vagy egy másik azonosító szolgáltatón keresztül.

    [:octicons-arrow-right-24: Olvasd el](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Hozzáférés naplózása__

    ---

    Ki nyitott, keresett, exportált vagy törölt mit.

    [:octicons-arrow-right-24: Olvasd el](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit adatok exportálása__

    ---

    Az audit nyomvonal exportálása a megfelelőségi ellenőrzésekhez.

    [:octicons-arrow-right-24: Olvasd el](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Vészhelyzeti fiók helyreállítása__

    ---

    Hozzáférés visszaszerzése, ha egy fiók kitiltódott.

    [:octicons-arrow-right-24: Olvasd el](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Megőrzési szabályok__

    ---

    Automatikus törlés a mail szerverről vagy a helyi archivból.

    [:octicons-arrow-right-24: Olvasd el](RetentionPolicies.md)

</div>

## ☁️ Szolgáltatói útmutatók {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure app regisztráció (M365)__

    ---

    Alkalmaz regisztrálása és a Microsoft 365 megőrzési szabályainak konfigurálása.

    [:octicons-arrow-right-24: Olvasd el](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant levélzárak importálása__

    ---

    Egy single formon keresztül egy tenant összes levélzárakának tömeges importálása.

    [:octicons-arrow-right-24: Olvasd el](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow példányok__

    ---

    Levélzárak központilag kiválasztása és dedikált IMAP alkalmazási jelszavak biztosítása.

    [:octicons-arrow-right-24: Olvasd el](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Személyes Microsoft fiókok__

    ---

    Outlook.com, Hotmail és M365 Family a device code flow segítségével.

    [:octicons-arrow-right-24: Olvasd el](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail legjobb gyakorlatok__

    ---

    Alkalmazási jelszavak, címkék, expunge és a Gmail sebességkorlátai.

    [:octicons-arrow-right-24: Olvasd el](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail korlátozások__

    ---

    Amit a Yahoo IMAP és export végpontjai nem tesznek meg.

    [:octicons-arrow-right-24: Olvasd el](YahooMailExportLimitations.md)

</div>

## 🔌 Programozható hozzáférés {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Csak olvasható hozzáférés az архівhez, felhasználónként korlátozott API kulcsokkal.

    [:octicons-arrow-right-24: Olvasd el](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Az archiv tesztelhető eszközként való megmutatása az AI ügynökök számára.

    [:octicons-arrow-right-24: Olvasd el](MCP.md)

</div>

## 🧰 Üzemeltetés és Fejlesztés {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Nyelv és lokalizáció__

    ---

    A felület nyelvének váltása, és hogyan kell fordítást hozzáadni.

    [:octicons-arrow-right-24: Olvasd el](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose naplók__

    ---

    Konténer naplók olvasása és a log szint megváltoztatása.

    [:octicons-arrow-right-24: Olvasd el](DockerComposeLogs.md)

-   :material-heart-pulse:{ .lg .middle } __Egészség és metrika__

    ---

    Liveness, readiness és archiv metrikák figyelése Prometheus vagy egy másik monitorozó rendszerből.

    [:octicons-arrow-right-24: Olvasd el](Monitoring.md)

-   :material-tag:{ .lg .middle } __Fejlesztői verziók__

    ---

    Váltás dev tagre a közelgő változások teszteléséhez.

    [:octicons-arrow-right-24: Olvasd el](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Ez a dokumentáció folyamatosan bővül.**
Ha hibát talál vagy javaslatot szeretne tenni, kérjük
[nyisson meg egy issue-t](https://github.com/s1t5/mail-archiver/issues) vagy küldjön egy pull request-et.
</div>
