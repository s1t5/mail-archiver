# 📧 Mail Archiver Documentatie {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Zelf-gehost e-mailarchiveren — archiveer, zoek en exporteer e-mail van elke provider.**
Stel het eenmaal in, bewaar elk bericht en vind het jaren later weer terug.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Aan de slag__

    ---

    Installeer Mail Archiver met Docker Compose en verbind je eerste Postbus.

    [:octicons-arrow-right-24: Setup and parameters](Setup.md)

-   :material-magnify:{ .lg .middle } __Vind alles__

    ---

    Volledige tekstzoekopdrachten over elk Account, met filters, sortering en bulkacties.

    [:octicons-arrow-right-24: Mail search](Search.md)

-   :material-api:{ .lg .middle } __Automatiseer het__

    ---

    Lees je Archief uit scripts en AI-agenten — read-only, scoped API-sleutels.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Beveilig het__

    ---

    Twee-factor authenticatie, SSO, toegang-logging en Bewaring-beleid.

    [:octicons-arrow-right-24: Two-factor auth](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Installatie & Onderhoud {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Setup and parameters__

    ---

    Elke omgevingsvariabele, Kestrel HTTPS en secrets management.

    [:octicons-arrow-right-24: Read](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Plaats Mail Archiver achter nginx of Caddy met TLS termination.

    [:octicons-arrow-right-24: Read](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL upgrade__

    ---

    Ga over naar een nieuwe belangrijke PostgreSQL-versie zonder gegevens te verliezen.

    [:octicons-arrow-right-24: Read](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Database maintenance__

    ---

    VACUUM, ANALYZE en het Archief responsief houden over de tijd.

    [:octicons-arrow-right-24: Read](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Back-up en herstel__

    ---

    Maak een Back-up van de database en voer een Herstel uit — inclusief Proxmox-opstellingen.

    [:octicons-arrow-right-24: Read](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Lokale testomgeving__

    ---

    Draai lokaal een tijdelijke instantie voordat je de productie aanraakt.

    [:octicons-arrow-right-24: Read](LocalTestEnvironment.md)

</div>

## 📥 E-mail binnenhalen {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Account import__

    ---

    Voeg veel Accounts tegelijk toe vanuit een CSV-bestand.

    [:octicons-arrow-right-24: Read](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Synchronisatie__

    ---

    Snelle vs. volledige synchronisatie, checkpoints, en wat er gebeurt als een synchronisatie mislukt.

    [:octicons-arrow-right-24: Read](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI commands__

    ---

    Importeer bestaande Archieven via de command line, en verplaats opgeslagen originele
    berichten tussen de database en het bestandssysteem.

    [:octicons-arrow-right-24: Read](CLI.md)

-   :material-upload:{ .lg .middle } __Web import (EML / MBox)__

    ---

    Upload een MBox-bestand of een ZIP met EML-bestanden via de browser.

    [:octicons-arrow-right-24: Read](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Rate limit handling__

    ---

    Bandbreedte-tracking, en hoe gepauzeerde synchronisaties automatisch hervatten.

    [:octicons-arrow-right-24: Read](RateLimitHandling.md)

</div>

## 🔍 Werken met het Archief {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Mail search__

    ---

    Query-syntaxis, filters, sortering, paginering en bulkacties.

    [:octicons-arrow-right-24: Read](Search.md)

-   :material-progress-clock:{ .lg .middle } __Background jobs__

    ---

    Houd synchronisaties, imports, herstels en exports in de gaten — en annuleer ze veilig.

    [:octicons-arrow-right-24: Read](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Postbus migratie__

    ---

    Kopieer e-mail tussen Postbussen terwijl de Mapstructuur behouden blijft.

    [:octicons-arrow-right-24: Read](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Account export (EML / MBox)__

    ---

    Schrijf een heel Account weg als een draagbaar EML- of MBox-Archief.

    [:octicons-arrow-right-24: Read](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Original message storage__

    ---

    Bewaar het bericht zoals ontvangen, zodat DKIM nog steeds valideert en exports byte-exact zijn.

    [:octicons-arrow-right-24: Read](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Bijlage deduplicatie__

    ---

    Hoe identieke Bijlagen één keer in plaats van meerdere keren worden opgeslagen.

    [:octicons-arrow-right-24: Read](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Date-windowed offload__

    ---

    Verplaats oudere e-mail naar een ander Account, venster voor venster.

    [:octicons-arrow-right-24: Read](Offload.md)

-   :material-harddisk:{ .lg .middle } __Per-account storage__

    ---

    Zie hoeveel ruimte elke Postbus inneemt.

    [:octicons-arrow-right-24: Read](AccountStorage.md)

</div>

## 🔐 Administratie & Beveiliging {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __User management__

    ---

    Maak gebruikers aan, verleen admin-rechten, wijs Postbus machtigingen toe en wijzig je
    eigen wachtwoord.

    [:octicons-arrow-right-24: Read](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Two-factor authentication__

    ---

    TOTP-instelling, backup codes, en wat te doen als een apparaat verloren gaat.

    [:octicons-arrow-right-24: Read](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Inloggen via Entra ID, Authelia of een andere identity provider.

    [:octicons-arrow-right-24: Read](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Access logging__

    ---

    Wie heeft wat geopend, gezocht, geëxporteerd of verwijderd.

    [:octicons-arrow-right-24: Read](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit data export__

    ---

    Exporteer het auditspoor voor compliance-beoordelingen.

    [:octicons-arrow-right-24: Read](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Emergency account recovery__

    ---

    Krijg weer toegang wanneer een Account is buitengesloten.

    [:octicons-arrow-right-24: Read](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Retention policies__

    ---

    Automatische verwijdering van de e-mailserver of het lokale Archief.

    [:octicons-arrow-right-24: Read](RetentionPolicies.md)

</div>

## ☁️ Provider Gidsen {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure app registration (M365)__

    ---

    Registreer een app en configureer Bewaring voor Microsoft 365.

    [:octicons-arrow-right-24: Read](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant mailbox import__

    ---

    Bulk-import van elke Postbus van een tenant via een enkel formulier.

    [:octicons-arrow-right-24: Read](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow instances__

    ---

    Selecteer Postbussen centraal en bereid dedicated IMAP app-wachtwoorden voor.

    [:octicons-arrow-right-24: Read](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Personal Microsoft accounts__

    ---

    Outlook.com, Hotmail en M365 Family via device code flow.

    [:octicons-arrow-right-24: Read](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail best practices__

    ---

    App-wachtwoorden, labels, expunge en de rate limits van Gmail.

    [:octicons-arrow-right-24: Read](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail limitations__

    ---

    Wat de IMAP- en export-endpoints van Yahoo niet zullen doen.

    [:octicons-arrow-right-24: Read](YahooMailExportLimitations.md)

</div>

## 🔌 Programmatische Toegang {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Read-only toegang tot het Archief met scoped per-user API-sleutels.

    [:octicons-arrow-right-24: Read](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Stel het Archief beschikbaar voor AI-agenten als ontdekbare tools.

    [:octicons-arrow-right-24: Read](MCP.md)

</div>

## 🧰 Operations & Ontwikkeling {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Language and localization__

    ---

    Schakel de taal van de interface en hoe je een vertaling toevoegt.

    [:octicons-arrow-right-24: Read](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose logs__

    ---

    Lees container logs en wijzig het log-niveau.

    [:octicons-arrow-right-24: Read](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Development versions__

    ---

    Schakel over naar een dev tag om aankomende wijzigingen te testen.

    [:octicons-arrow-right-24: Read](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Deze documentatie wordt continu uitgebreid.**
Als je een fout ziet of een suggestie hebt, [open dan een issue](https://github.com/s1t5/mail-archiver/issues) of dien een pull request in.
</div>
