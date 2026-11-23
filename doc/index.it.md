# 📧 Documentazione Mail Archiver {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Archiviazione email self-hosted — archivia, cerca ed esporta email da qualsiasi provider.**
Configuralo una volta, conserva ogni messaggio e ritrovalo anche anni dopo.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Iniziare__

    ---

    Installa Mail Archiver con Docker Compose e collega la tua prima Casella di posta.

    [:octicons-arrow-right-24: Setup e parametri](Setup.md)

-   :material-magnify:{ .lg .middle } __Trova qualsiasi cosa__

    ---

    Ricerca full-text su ogni Account, con filtri, ordinamento e azioni di massa.

    [:octicons-arrow-right-24: Ricerca email](Search.md)

-   :material-api:{ .lg .middle } __Automatizzalo__

    ---

    Leggi il tuo Archivio tramite script e agenti AI — chiavi API in sola lettura e con scope limitato.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Proteggilo__

    ---

    Autenticazione a due fattori, SSO, registrazione degli accessi e politiche di Conservazione.

    [:octicons-arrow-right-24: Autenticazione a due fattori](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Installazione & Manutenzione {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Setup e parametri__

    ---

    Ogni variabile d'ambiente, gestione Kestrel HTTPS e dei segreti.

    [:octicons-arrow-right-24: Leggi](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Posiziona Mail Archiver dietro nginx o Caddy con terminazione TLS.

    [:octicons-arrow-right-24: Leggi](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __Aggiornamento PostgreSQL__

    ---

    Passa a una nuova versione major di PostgreSQL senza perdere dati.

    [:octicons-arrow-right-24: Leggi](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Manutenzione del database__

    ---

    VACUUM, ANALYZE e mantenimento della reattività dell'Archivio nel tempo.

    [:octicons-arrow-right-24: Leggi](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Backup e Ripristino__

    ---

    Esegui il Backup del database e il relativo Ripristino — inclusi i setup Proxmox.

    [:octicons-arrow-right-24: Leggi](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Ambiente di test locale__

    ---

    Esegui un'istanza temporanea localmente prima di toccare la produzione.

    [:octicons-arrow-right-24: Leggi](LocalTestEnvironment.md)

</div>

## 📥 Inserimento Email {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Importazione Account__

    ---

    Aggiungi molti Account contemporaneamente da un file CSV.

    [:octicons-arrow-right-24: Leggi](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Sincronizzazione__

    ---

    Sincronizzazione rapida vs completa, checkpoint e cosa succede quando una Sincronizzazione fallisce.

    [:octicons-arrow-right-24: Leggi](Synchronization.md)

-   :material-console:{ .lg .middle } __Comandi CLI__

    ---

    Importa Archivio esistenti dalla riga di comando e sposta i messaggi originali memorizzati tra il database e il file system.

    [:octicons-arrow-right-24: Leggi](CLI.md)

-   :material-upload:{ .lg .middle } __Importazione Web (EML / MBox)__

    ---

    Carica un file MBox o un archivio ZIP di file EML tramite browser.

    [:octicons-arrow-right-24: Leggi](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Gestione Rate limit__

    ---

    Monitoraggio della larghezza di banda e come le Sincronizzazioni in pausa riprendono automaticamente.

    [:octicons-arrow-right-24: Leggi](RateLimitHandling.md)

</div>

## 🔍 Lavorare con l'Archivio {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Ricerca email__

    ---

    Sintassi di query, filtri, ordinamento, paginazione e azioni di massa.

    [:octicons-arrow-right-24: Leggi](Search.md)

-   :material-progress-clock:{ .lg .middle } __Job in background__

    ---

    Monitora Sincronizzazioni, importazioni, Ripristini ed esportazioni — e cancellali in sicurezza.

    [:octicons-arrow-right-24: Leggi](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Migrazione Casella di posta__

    ---

    Copia email tra Caselle di posta preservando la struttura delle Cartelle.

    [:octicons-arrow-right-24: Leggi](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Esportazione Account (EML / MBox)__

    ---

    Esporta un intero Account come un archivio EML o MBox portatile.

    [:octicons-arrow-right-24: Leggi](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Memorizzazione messaggi originali__

    ---

    Mantieni il messaggio così come ricevuto, in modo che DKIM venga ancora verificato ed le esportazioni siano byte-exact.

    [:octicons-arrow-right-24: Leggi](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Deduplicazione Allegati__

    ---

    Come gli Allegati identici vengono memorizzati una sola volta invece di molteplici volte.

    [:octicons-arrow-right-24: Leggi](AttachmentDeduplication.md)

-   :material-file-pdf-box:{ .lg .middle } __Anteprima fattura XRechnung__

    ---

    Leggi gli Allegati XML XRechnung come PDF direttamente nell'Archivio.

    [:octicons-arrow-right-24: Leggi](EInvoicing.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Offload basato su intervallo di date__

    ---

    Sposta le email più vecchie in un altro Account, per intervalli di tempo.

    [:octicons-arrow-right-24: Leggi](Offload.md)

-   :material-harddisk:{ .lg .middle } __Spazio per Account__

    ---

    Vedi quanto spazio occupa ogni Casella di posta.

    [:octicons-arrow-right-24: Leggi](AccountStorage.md)

</div>

## 🔐 Amministrazione & Sicurezza {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Gestione utenti__

    ---

    Crea utenti, concede diritti di admin, assegna Autorizzazioni per la Casella di posta e cambia la tua password.

    [:octicons-arrow-right-24: Leggi](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Autenticazione a due fattori__

    ---

    Setup TOTP, codici di Backup e cosa fare in caso di smarrimento di un dispositivo.

    [:octicons-arrow-right-24: Leggi](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Accedi tramite Entra ID, Authelia o un altro identity provider.

    [:octicons-arrow-right-24: Leggi](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Registrazione accessi__

    ---

    Chi ha aperto, cercato, esportato o eliminato cosa.

    [:octicons-arrow-right-24: Leggi](Logs.md)

-   :material-file-export:{ .lg .middle } __Esportazione dati di audit__

    ---

    Esporta il Registro di controllo per revisioni di conformità.

    [:octicons-arrow-right-24: Leggi](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Ripristino Account di emergenza__

    ---

    Recupera l'accesso quando un Account è bloccato.

    [:octicons-arrow-right-24: Leggi](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Politiche di Conservazione__

    ---

    Eliminazione automatica dal server email o dall'Archivio locale.

    [:octicons-arrow-right-24: Leggi](RetentionPolicies.md)

</div>

## ☁️ Guide per i Provider {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Registrazione app Azure (M365)__

    ---

    Registra un'app e configura la Conservazione per Microsoft 365.

    [:octicons-arrow-right-24: Leggi](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __Importazione Caselle di posta tenant M365__

    ---

    Importazione massiva di ogni Casella di posta di un tenant da un unico modulo.

    [:octicons-arrow-right-24: Leggi](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Istanze Mailcow__

    ---

    Seleziona le Caselle di posta centralmente e predisponi password app IMAP dedicate.

    [:octicons-arrow-right-24: Leggi](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Account Microsoft personali__

    ---

    Outlook.com, Hotmail e M365 Family tramite device code flow.

    [:octicons-arrow-right-24: Leggi](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Best practice Gmail__

    ---

    Password app, etichette, expunge e i limiti di velocità di Gmail.

    [:octicons-arrow-right-24: Leggi](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Limitazioni Yahoo mail__

    ---

    Cosa non faranno gli endpoint IMAP ed esportazione di Yahoo.

    [:octicons-arrow-right-24: Leggi](YahooMailExportLimitations.md)

</div>

## 🔌 Accesso Programmatico {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Accesso in sola lettura all'Archivio con chiavi API per utente con scope limitato.

    [:octicons-arrow-right-24: Leggi](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Esponi l'Archivio agli agenti AI come strumenti scopribili.

    [:octicons-arrow-right-24: Leggi](MCP.md)

</div>

## 🧰 Operazioni & Sviluppo {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Lingua e localizzazione__

    ---

    Cambia la lingua dell'interfaccia e come aggiungere una traduzione.

    [:octicons-arrow-right-24: Leggi](Localization.md)

-   :material-docker:{ .lg .middle } __Log Docker Compose__

    ---

    Leggi i log dei container e cambia il livello di log.

    [:octicons-arrow-right-24: Leggi](DockerComposeLogs.md)

-   :material-heart-pulse:{ .lg .middle } __Health e metriche__

    ---

    Monitora liveness, readiness e le metriche dell'Archivio da Prometheus o altri sistemi di monitoraggio.

    [:octicons-arrow-right-24: Leggi](Monitoring.md)

-   :material-tag:{ .lg .middle } __Versioni di sviluppo__

    ---

    Passa a un tag di sviluppo per testare i prossimi cambiamenti.

    [:octicons-arrow-right-24: Leggi](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Questa documentazione viene espansa continuamente.**
Se noti un errore o hai un suggerimento, per favore
[apri un issue](https://github.com/s1t5/mail-archiver/issues) o invia una pull request.
</div>
