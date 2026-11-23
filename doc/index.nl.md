# 📧 Mail Archiver Documentatie {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Zelfgehost e-mailarchiveren — archiveer, zoek en exporteer e-mail van elke provider.**
Stel het eenmalig in, bewaar elke berichten en vind ze jaren later terug.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Aan de slag maken__

    ---

    Installeer Mail Archiver met Docker Compose en verbind je eerste Postbus.

    [:octicons-arrow-right-24: Setup en parameters](Setup.md)

-   :material-magnify:{ .lg .middle } __Alles vinden__

    ---

    Volledige tekstzoekopdrachten over elk Account, met filters, sortering en bulkacties.

    [:octicons-arrow-right-24: Mail zoeken](Search.md)

-   :material-api:{ .lg .middle } __Automatiseer het__

    ---

    Lees je Archief uit scripts en AI-agenten — read-only, scoped API-sleutels.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Beveilig het__

    ---

    Twee-factor authenticatie, SSO, toegangslogging en Bewaring-beleid.

    [:octicons-arrow-right-24: Twee-factor auth](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Installatie & Onderhoud {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Setup en parameters__

    ---

    Elke omgevingsvariabele, Kestrel HTTPS en beheer van secrets.

    [:octicons-arrow-right-24: Lees](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Plaats Mail Archiver achter nginx of Caddy met TLS-terminatie.

    [:octicons-arrow-right-24: Lees](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL upgrade__

    ---

    Verplaats naar een nieuwe belangrijke PostgreSQL-versie zonder gegevens te verliezen.

    [:octicons-arrow-right-24: Lees](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Database onderhoud__

    ---

    VACUUM, ANALYZE en het Archief op de lange termijn responsief houden.

    [:octicons-arrow-right-24: Lees](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Back-up en herstel__

    ---

    Maak een Back-up van de database en voer een Herstel uit — inclusief Proxmox-opstellingen.

    [:octicons-arrow-right-24: Lees](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Lokale testomgeving__

    ---

    Draai lokaal een tijdelijke instantie voordat je de productie aanraakt.

    [:octicons-arrow-right-24: Lees](LocalTestEnvironment.md)

</div>

## 📥 E-mail binnenhalen {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Account import__

    ---

    Voeg veel Accounts tegelijk toe vanuit een CSV-bestand.

    [:octicons-arrow-right-24: Lees](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Synchronisatie__

    ---

    Snelle vs. volledige Synchronisatie, checkpoints, en wat er gebeurt als een Synchronisatie mislukt.

    [:octicons-arrow-right-24: Lees](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI commando's__

    ---

    Importeer bestaande archieven via de command line, en verplaats opgeslagen originele
    berichten tussen de database en het bestandssysteem.

    [:octicons-arrow-right-24: Lees](CLI.md)

-   :material-upload:{ .lg .middle } __Web import (EML / MBox)__

    ---

    Upload een MBox-bestand of een ZIP met EML-bestanden via de browser.

    [:octicons-arrow-right-24: Lees](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Rate limit afhandeling__

    ---

    Bandbreedte tracking, en hoe gepauzeerde Synchronisaties vanzelf hervatten.

    [:octicons-arrow-right-24: Lees](RateLimitHandling.md)

</div>

## 🔍 Werken met het Archief {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Mail zoeken__

    ---

    Query-syntaxis, filters, sortering, paginering en bulkacties.

    [:octicons-arrow-right-24: Lees](Search.md)

-   :material-progress-clock:{ .lg .middle } __Background jobs__

    ---

    Volg Synchronisaties, imports, herstelacties en exports — en annuleer ze veilig.

    [:octicons-arrow-right-24: Lees](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Postbus migratie__

    ---

    Kopieer mail tussen Postbussen terwijl de Map-structuur behouden blijft.

    [:octicons-arrow-right-24: Lees](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Account export (EML / MBox)__

    ---

    Schrijf een heel Account terug als een draagbaar EML of MBox Archief.

    [:octicons-arrow-right-24: Lees](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Originele bericht opslag__

    ---

    Bewaar het bericht zoals ontvangen, zodat DKIM nog steeds valideert en exports byte-exact zijn.

    [:octicons-arrow-right-24: Lees](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Bijlage Deduplicatie__

    ---

    Hoe identieke Bijlagen één keer in plaats van meerdere keren worden opgeslagen.

    [:octicons-arrow-right-24: Lees](AttachmentDeduplication.md)

-   :material-file-pdf-box:{ .lg .middle } __XRechnung factuur preview__

    ---

    Lees XRechnung XML Bijlagen als een PDF direct in het Archief.

    [:octicons-arrow-right-24: Lees](EInvoicing.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Date-windowed offload__

    ---

    Verplaats oudere mail naar een ander Account, venster voor venster.

    [:octicons-arrow-right-24: Lees](Offload.md)

-   :material-harddisk:{ .lg .middle } __Opslag per Account__

    ---

    Zie hoeveel ruimte elke Postbus inneemt.

    [:octicons-arrow-right-24: Lees](AccountStorage.md)

</div>

## 🔐 Administratie & Beveiliging {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Gebruikersbeheer__

    ---

    Maak gebruikers aan, verleen admin-rechten, wijs Postbus machtigingen toe en wijzig je
    eigen wachtwoord.

    [:octicons-arrow-right-24: Lees](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Twee-factor authenticatie__

    ---

    TOTP setup, herstelcodes, en wat te doen als een apparaat verloren is.

    [:octicons-arrow-right-24: Lees](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Inloggen via Entra ID, Authelia of een andere identity provider.

    [:octicons-arrow-right-24: Lees](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Toegangslogging__

    ---

    Wie heeft wat geopend, gezocht, geëxporteerd of verwijderd.

    [:octicons-arrow-right-24: Lees](Logs.md)

-   :material-file-export:{ .lg .middle } __Auditdata export__

    ---

    Exporteer het auditspoor voor compliance reviews.

    [:octicons-arrow-right-24: Lees](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Noodgeval Account herstel__

    ---

    Toegang herwinnen wanneer een Account is geblokkeerd.

    [:octicons-arrow-right-24: Lees](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Bewaring-beleid__

    ---

    Automatische verwijdering van de mailserver of het lokale Archief.

    [:octicons-arrow-right-24: Lees](RetentionPolicies.md)

</div>

## ☁️ Provider Gidsen {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure app registratie (M365)__

    ---

    Registreer een app en configureer Bewaring voor Microsoft 365.

    [:octicons-arrow-right-24: Lees](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant Postbus import__

    ---

    Bulk-importeer elke Postbus van een tenant vanuit één enkel formulier.

    [:octicons-arrow-right-24: Lees](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow instanties__

    ---

    Selecteer Postbussen centraal en provisioneer toegewezen IMAP app-wachtwoorden.

    [:octicons-arrow-right-24: Lees](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Persoonlijke Microsoft Accounts__

    ---

    Outlook.com, Hotmail en M365 Family via device code flow.

    [:octicons-arrow-right-24: Lees](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail best practices__

    ---

    App-wachtwoorden, labels, expunge en de rate limits van Gmail.

    [:octicons-arrow-right-24: Lees](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail beperkingen__

    ---

    Wat de IMAP en export endpoints van Yahoo niet zullen doen.

    [:octicons-arrow-right-24: Lees](YahooMailExportLimitations.md)

</div>

## 🔌 Programmeerbare Toegang {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Read-only toegang tot het Archief met scoped per-user API-sleutels.

    [:octicons-arrow-right-24: Lees](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Exposeer het Archief aan AI-agenten als ontdekbare tools.

    [:octicons-arrow-right-24: Lees](MCP.md)

</div>

## 🧰 Operations & Ontwikkeling {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Taal en lokalisatie__

    ---

    De interface taal wijzigen, en hoe je een vertaling toevoegt.

    [:octicons-arrow-right-24: Lees](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose logs__

    ---

    Container logs lezen en het logniveau wijzigen.

    [:octicons-arrow-right-24: Lees](DockerComposeLogs.md)

-   :material-heart-pulse:{ .lg . middle } __Health en metrics__

    ---

    Houd liveness, readiness en Archief metrics in de gaten van Prometheus of een ander monitoringsysteem.

    [:octicons-arrow-right-24: Lees](Monitoring.md)

-   :material-tag:{ .lg . middle } __Development versies__

    ---

    Schakel naar een dev tag om aankomende wijzigingen te testen.

    [:octicons-arrow-right-24: Lees](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Deze documentatie wordt continu uitgebreid.**
Als je een fout ziet of een suggestie hebt, [open dan een issue](https://github.com/s1t5/mail-archiver/issues) of dien een pull request in.
</div>
