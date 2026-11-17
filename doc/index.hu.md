# 📧 Mail Archiver Dokumentáció {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Sajátুপ্তálású e-mail archiválás — archiváljon, keressen és exportáljon e-maileket bármely szolgáltatótól.**
Állítsa be egyszer, őrizze meg minden üzenetet, és találja meg újra évek később is.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Kezdés__

    ---

    Telepítse a Mail Archivert Docker Compose segítségével, és csatolja az első postafiókját.

    [:octicons-arrow-right-24: Beállítások és paraméterek](Setup.md)

-   :material-magnify:{ .lg .middle } __Bármit megtaláljon__

    ---

    Teljes szövegkeresés minden fiókban, szűrőkkel, rendezéssel és tömeges műveletekkel.

    [:octicons-arrow-right-24: E-mail keresés](Search.md)

-   :material-api:{ .lg .middle } __Automatizálja__

    ---

    Olvassa el az archívumot szkriptekből és AI ügynökökből — csak olvasási jogú, korlátozott API kulcsokkal.

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

    Áll transition egy újabb fő PostgreSQL verzióra adatvesztés nélkül.

    [:octicons-arrow-right-24: Olvasás](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Adatbázis karbantartás__

    ---

    VACUUM, ANALYZE és az archívum válaszkészségének fenntartása idővel.

    [:octicons-arrow-right-24: Olvasás](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Mentés és visszaállítás__

    ---

    Adatbázis mentése és visszaállítása — beleértve a Proxmox beállításokat is.

    [:octicons-arrow-right-24: Olvasás](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Helyi tesztkörnyezet__

    ---

    Futtasson egy ideiglenes példányt helyileg, mielőtt a termeléshez nyúlna.

    [:octicons-arrow-right-24: Olvasás](LocalTestEnvironment.md)

</div>

## 📥 E-mailek bevitele {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Fiók importálás__

    ---

    Több fiókot egyszerre egy CSV fájlból.

    [:octicons-arrow-right-24: Olvasás](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Szinkronizálás__

    ---

    Gyors vs. teljes szinkronizálás, ellenőrzőpontok, és mi történik, ha a szinkron megszakad.

    [:octicons-arrow-right-24: Olvasás](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI parancsok__

    ---

    Már meglévő archívumok importálása parancssorból, és a tárolt eredeti üzenetek mozgatása az adatbázis és a fájlrendszer között.

    [:octicons-arrow-right-24: Olvasás](CLI.md)

-   :material-upload:{ .lg .middle } __Webes importálás (EML / MBox)__

    ---

    MBox fájl vagy EML fájlokból álló ZIP feltöltése a böngészőn keresztül.

    [:octicons-arrow-right-24: Olvasás](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Rate limit kezelés__

    ---

    Sávszélesség nyomon követése, és a szüneteltetett szinkronok automatikus folytatása.

    [:octicons-arrow-right-24: Olvasás](RateLimitHandling.md)

</div>

## 🔍 Az archívummal való munka {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __E-mail keresés__

    ---

    Keresési szintaxis, szűrők, rendezés, oldaltörlés és tömeges műveletek.

    [:octicons-arrow-right-24: Olvasás](Search.md)

-   :material-progress-clock:{ .lg .middle } __Háttérfolyamatok__

    ---

    Szinkronizálás, importálás, visszaállítás és exportálás figyelése — és azok biztonságos leállítása.

    [:octicons-arrow-right-24: Olvasás](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Postafiók migráció__

    ---

    E-mailek másolása postafiókok között a mappastruktúra megőrzésével.

    [:octicons-arrow-right-24: Olvasás](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Fiók exportálás (EML / MBox)__

    ---

Egy teljes fiók kiírása hordozható EML vagy MBox archívumként.

    [:octicons-arrow-right-24: Olvasás](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Eredeti üzenetek tárolása__

    ---

Az üzenet megmarad úgy, ahogy megérkezett, így a DKIM továbbra is hitelesíti, az exportok pedig byte-pontosak maradnak.

    [:octicons-arrow-right-24: Olvasás](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Melléklet de-duplikáció__

    ---

Hogyan tárolódnak az azonos mellékletek egyszer, ahelyett, hogy többször tárolódnának.

    [:octicons-arrow-right-24: Olvasás](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Dátumal alapozott archiválás lecsökkentése__

    ---

    Régebbi e-mailek mozgatása egy másik fiókba, időszakosan.

    [:octicons-arrow-right-24: Olvasás](Offload.md)

-   :material-harddisk:{ .lg .middle } __Fiókonkénti tárolás__

    ---

    Nézze meg, mennyi helyet foglal elfoglal minden postafiók.

    [:octicons-arrow-right-24: Olvasás](AccountStorage.md)

</div>

## 🔐 Adminisztráció és biztonság {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Felhasználók kezelése__

    ---

    Felhasználók létrehozása, admin jogok engedélyezése, postafiók jogosultságok kiosztása és a saját jelszó megváltoztatása.

    [:octicons-arrow-right-24: Olvasás](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Kétfaktoros hitelesítés__

    ---

    TOTP beállítás, mentési kódok, és mi a teendő, ha egy eszköz elveszik.

    [:octicons-arrow-right-24: Olvasás](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Bejelentkezés Entra ID, Authelia vagy más azonosítót sağlayón keresztül.

    [:octicons-arrow-right-24: Olvasás](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Hozzáférési naplózás__

    ---

    Ki nyitott, keresett, exportált vagy törölt mit.

    [:octicons-arrow-right-24: Olvasás](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit adatok exportálása__

    ---

    Az audit ny迹 (audit trail) exportálása a megfelelőségi ellenőrzésekhez.

    [:octicons-arrow-right-24: Olvasás](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Vészhelyzeti fiókhelyreállítás__

    ---

    Hozzáférés visszaszerzése, ha egy fiók kitiltódott.

    [:octicons-arrow-right-24: Olvasás](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Megőrzési irányelvek__

    ---

    Automatikus törlés az e-mail szerverről vagy a helyi archívumból.

    [:octicons-arrow-right-24: Olvasás](RetentionPolicies.md)

</div>

## ☁️ Szolgáltatói útmutatók {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure alkalmazásregisztráció (M365)__

    ---

    Alkalmazás regisztrálása és a Microsoft 365 megőrzési beállításainak konfigurálása.

    [:octicons-arrow-right-24: Olvasás](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant postafiók importálás__

    ---

    Egy single formon keresztül egy tenant minden postafiókjának tömeges importálása.

    [:octicons-arrow-right-24: Olvasás](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow példányok__

    ---

    Postafiókok központilag kiválasztása és dedikált IMAP alkalmazási jelszavak biztosítása.

    [:octicons-arrow-right-24: Olvasás](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Személyes Microsoft fiókok__

    ---

    Outlook.com, Hotmail és M365 Family a device code flow segítségével.

    [:octicons-arrow-right-24: Olvasás](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail legjobb gyakorlatok__

    ---

    Alkalmazási jelszavak, címkék, expunge és a Gmail sebességi korlátai.

    [:octicons-arrow-right-24: Olvasás](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail korlátozások__

    ---

Amit a Yahoo IMAP és export végpontjai nem tesznek meg.

    [:octicons-arrow-right-24: Olvasás](YahooMailExportLimitations.md)

</div>

## 🔌 Programprogramozható hozzáférés {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Csak olvasási jogú hozzáférés az archívumhoz, felhasználónként korlátozott API kulcsokkal.

    [:octicons-arrow-right-24: Olvasás](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Az archívum tesztelhető eszközként (discoverable tools) való bemutatása az AI ügynökök számára.

    [:octicons-arrow-right-24: Olvasás](MCP.md)

</div>

## 🧰 Üzemeltetés és fejlesztés {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Nyelv és lokalizáció__

    ---

    A felület nyelvének váltása, és hogyan lehet fordítást hozzáadni.

    [:octicons-arrow-right-24: Olvasás](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose naplók__

    ---

    Konténer naplók olvasása és a naplózási szint megváltoztatása.

    [:octicons-arrow-right-24: Olvasás](DockerComposeLogs.md)

-   :material-heart-pulse:{ .lg .middle } __Egészségállapot és metrikák__

    ---

    Liveness, readiness és archívum metrikák figyelése Prometheus-ból vagy más monitorozó rendszerből.

    [:octicons-arrow-right-24: Olvasás](Monitoring.md)

-   :material-tag:{ .lg .middle } __Fejlesztői verziók__

    ---

    Váltás fejlesztői (dev) tagre az közelgő változások tesztelése érdekében.

    [:octicons-arrow-right-24: Olvasás](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Ezt a dokumentációt folyamatosan bővítjük.**
Ha hibát talál, vagy javaslatot szeretne tenni, kérjük
[nyisson meg egy issue-t](https://github.com/s1t5/mail-archiver/issues) vagy küldjön pull request-et.
</div>
