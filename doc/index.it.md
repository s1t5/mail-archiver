# 📧 Mail Archiver Documentation {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Archiviazione email self-hosted — archivia, cerca ed esporta email da qualsiasi provider.**
Configuralo una volta, conserva ogni messaggio e ritrovalo anche anni dopo.
</div>

!!! note "Traduzioni"
    La documentazione in inglese è la fonte autorevole. Ogni altra lingua è tradotta automaticamente da queste pagine e potrebbe contenere errori. Usa il selettore della lingua nell'intestazione per cambiare lingua.

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Inizia subito__

    ---

    Installa Mail Archiver con Docker Compose e connetti la tua prima Casella di posta.

    [:octicons-arrow-right-24: Setup and parameters](Setup.md)

-   :material-magnify:{ .lg .middle } __Trova qualsiasi cosa__

    ---

    Ricerca full-text su ogni Account, con filtri, ordinamento e azioni di massa.

    [:octicons-arrow-right-24: Mail search](Search.md)

-   :material-api:{ .lg .middle } __Automatizzalo__

    ---

    Leggi il tuo Archivio tramite script e agenti AI — chiavi API in sola lettura e con scope limitato.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Proteggilo__

    ---

    Autenticazione a due fattori, SSO, registrazione degli accessi e policy di Conservazione.

    [:octicons-arrow-right-24: Two-factor auth](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Installazione & Manutenzione {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Setup and parameters__

    ---

    Ogni variabile d'ambiente, Kestrel HTTPS e gestione dei segreti.

    [:octicons-arrow-right-24: Read](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Posiziona Mail Archiver dietro nginx o Caddy con terminazione TLS.

    [:octicons-arrow-right-24: Read](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL upgrade__

    ---

    Passa a una nuova versione major di PostgreSQL senza perdere dati.

    [:octicons-arrow-right-24: Read](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Database maintenance__

    ---

    VACUUM, ANALYZE e mantenere l'Archivio reattivo nel tempo.

    [:octicons-arrow-right-24: Read](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Backup and restore__

    ---

    Esegui il Backup del database e il relativo Ripristino — incluse le configurazioni Proxmox.

    [:octicons-arrow-right-24: Read](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Local test environment__

    ---

    Esegui un'istanza temporanea localmente prima di toccare la produzione.

    [:octicons-arrow-right-24: Read](LocalTestEnvironment.md)

</div>

## 📥 Importazione Email {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Account import__

    ---

    Aggiungi molti Account contemporaneamente da un file CSV.

    [:octicons-arrow-right-24: Read](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Sincronizzazione__

    ---

    Sincronizzazione rapida vs completa, checkpoint e cosa succede quando una Sincronizzazione fallisce.

    [:octicons-arrow-right-24: Read](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI commands__

    ---

    Importa Archivio esistenti dalla riga di comando e sposta i messaggi originali memorizzati tra il database e il filesystem.

    [:octicons-arrow-right-24: Read](CLI.md)

-   :material-upload:{ .lg .middle } __Web import (EML / MBox)__

    ---

    Carica un file MBox o un file ZIP di file EML tramite browser.

    [:octicons-arrow-right-24: Read](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Rate limit handling__

    ---

    Monitoraggio della larghezza di banda e come le Sincronizzazioni in pausa riprendono autonomamente.

    [:octicons-arrow-right-24: Read](RateLimitHandling.md)

</div>

## 🔍 Lavorare con l'Archivio {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Mail search__

    ---

    Sintassi di query, filtri, ordinamento, paginazione e azioni di massa.

    [:octicons-arrow-right-24: Read](Search.md)

-   :material-progress-clock:{ .lg .middle } __Background jobs__

    ---

    Monitora Sincronizzazioni, importazioni, Ripristini ed esportazioni — e cancellali in sicurezza.

    [:octicons-arrow-right-24: Read](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Mailbox migration__

    ---

    Copia email tra Caselle di posta preservando la struttura delle Cartella.

    [:octicons-arrow-right-24: Read](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Account export (EML / MBox)__

    ---

    Esporta un intero Account come un Archivio EML o MBox portatile.

    [:octicons-arrow-right-24: Read](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Original message storage__

    ---

    Conserva il messaggio così come ricevuto, in modo che DKIM sia ancora verificabile ed le esportazioni siano identiche a livello di byte.

    [:octicons-arrow-right-24: Read](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Attachment deduplication__

    ---

    Come gli Allegati identici vengono memorizzati una sola volta invece di molteplici volte.

    [:octicons-arrow-right-24: Read](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Date-windowed offload__

    ---

    Sposta le email più vecchie su un altro Account, finestra per finestra.

    [:octicons-arrow-right-24: Read](Offload.md)

-   :material-harddisk:{ .lg .middle } __Per-account storage__

    ---

    Vedi quanto spazio occupa ogni Casella di posta.

    [:octicons-arrow-right-24: Read](AccountStorage.md)

</div>

## 🔐 Amministrazione & Sicurezza {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __User management__

    ---

    Crea utenti, concedi diritti di admin, assegna Autorizzazione alle Caselle di posta e cambia la tua password.

    [:octicons-arrow-right-24: Read](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Two-factor authentication__

    ---

    Configurazione TOTP, codici di backup e cosa fare in caso di smarrimento di un dispositivo.

    [:octicons-arrow-right-24: Read](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Accedi tramite Entra ID, Authelia o un altro identity provider.

    [:octicons-arrow-right-24: Read](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Access logging__

    ---

    Chi ha aperto, cercato, esportato o eliminato cosa.

    [:octicons-arrow-right-24: Read](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit data export__

    ---

    Esporta il Registro di controllo per revisioni di compliance.

    [:octicons-arrow-right-24: Read](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Emergency account recovery__

    ---

    Recupera l'accesso quando un Account è bloccato.

    [:octicons-arrow-right-24: Read](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Retention policies__

    ---

    Eliminazione automatica dal server email o dall'Archivio locale.

    [:octicons-arrow-right-24: Read](RetentionPolicies.md)

</div>

## ☁️ Guide ai Provider {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure app registration (M365)__

    ---

    Registra un'app e configura la Conservazione per Microsoft 365.

    [:octicons-arrow-right-24: Read](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant mailbox import__

    ---

    Importazione massiva di ogni Casella di posta di un tenant da un unico modulo.

    [:octicons-arrow-right-24: Read](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow instances__

    ---

    Seleziona le Caselle di posta centralmente e predisponi password app IMAP dedicate.

    [:octicons-arrow-right-24: Read](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Personal Microsoft accounts__

    ---

    Outlook.com, Hotmail e M365 Family tramite device code flow.

    [:octicons-arrow-right-24: Read](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail best practices__

    ---

    Password app, etichette, expunge e i limiti di velocità di Gmail.

    [:octicons-arrow-right-24: Read](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail limitations__

    ---

    Cosa non faranno gli endpoint IMAP ed esportazione di Yahoo.

    [:octicons-arrow-right-24: Read](YahooMailExportLimitations.md)

</div>

## 🔌 Accesso Programmatico {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Accesso in sola lettura all'Archivio con chiavi API per utente con scope limitato.

    [:octicons-arrow-right-24: Read](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Esponi l'Archivio agli agenti AI come strumenti scopribili.

    [:octicons-arrow-right-24: Read](MCP.md)

</div>

## 🧰 Operazioni & Sviluppo {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Language and localization__

    ---

    Cambia la lingua dell'interfaccia e come aggiungere una traduzione.

    [:octicons-arrow-right-24: Read](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose logs__

    ---

    Leggi i log dei container e cambia il livello di log.

    [:octicons-arrow-right-24: Read](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Development versions__

    ---

    Passa a un tag di sviluppo per testare le prossime modifiche.

    [:octicons-arrow-right-24: Read](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Questa documentazione è in continua espansione.**
Se noti un errore o hai un suggerimento, per favore
[apri un issue](https://github.com/s1t5/mail-archiver/issues) o invia una pull request.
</div>
