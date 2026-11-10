# 📧 Mail Archiver Documentation {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Archivage d'e-mails auto-hébergé — archivez, recherchez et exportez des e-mails de n'importe quel fournisseur.**
Configurez-le une fois, conservez chaque message, et retrouvez-le des années plus tard.
</div>

!!! note "Translations"
    La documentation en anglais est la source faisant autorité. Toutes les autres langues sont traduites automatiquement à partir de ces pages et peuvent contenir des erreurs. Utilisez le sélecteur de langue dans l'en-tête pour changer de langue.

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Démarrage rapide__

    ---

    Installez Mail Archiver avec Docker Compose et connectez votre première Boîte aux lettres.

    [:octicons-arrow-right-24: Setup and parameters](Setup.md)

-   :material-magnify:{ .lg .middle } __Trouver n'importe quoi__

    ---

    Recherche plein texte sur chaque Compte, avec filtres, tri et actions groupées.

    [:octicons-arrow-right-24: Mail search](Search.md)

-   :material-api:{ .lg .middle } __Automatisez__

    ---

    Lisez votre Archive depuis des scripts et des agents IA — clés API en lecture seule et limitées.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Sécurisez__

    ---

    Authentification à deux facteurs, SSO, journalisation des accès et politiques de Conservation.

    [:octicons-arrow-right-24: Two-factor auth](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Installation & Maintenance {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Setup and parameters__

    ---

    Chaque variable d'environnement, Kestrel HTTPS et gestion des secrets.

    [:octicons-arrow-right-24: Read](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Placez Mail Archiver derrière nginx ou Caddy avec terminaison TLS.

    [:octicons-arrow-right-24: Read](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL upgrade__

    ---

    Passez à une nouvelle version majeure de PostgreSQL sans perdre de données.

    [:octicons-arrow-right-24: Read](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Database maintenance__

    ---

    VACUUM, ANALYZE et maintien de la réactivité de l'Archive au fil du temps.

    [:octicons-arrow-right-24: Read](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Backup and restore__

    ---

    Sauvegardez la base de données et effectuez une Restauration — y compris les configurations Proxmox.

    [:octicons-arrow-right-24: Read](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Local test environment__

    ---

    Exécutez une instance temporaire localement avant de toucher à la production.

    [:octicons-arrow-right-24: Read](LocalTestEnvironment.md)

</div>

## 📥 Réception des e-mails {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Account import__

    ---

    Ajoutez de nombreux comptes à la fois à partir d'un fichier CSV.

    [:octicons-arrow-right-24: Read](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Synchronisation__

    ---

    Synchronisation rapide vs complète, points de contrôle, et ce qui se passe lorsqu'une synchronisation échoue.

    [:octicons-arrow-right-24: Read](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI commands__

    ---

    Importez des Archives existantes depuis la ligne de commande, et déplacez les messages originaux stockés entre la base de données et le système de fichiers.

    [:octicons-arrow-right-24: Read](CLI.md)

-   :material-upload:{ .lg .middle } __Web import (EML / MBox)__

    ---

    Téléchargez un fichier MBox ou un ZIP de fichiers EML via le navigateur.

    [:octicons-arrow-right-24: Read](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Rate limit handling__

    ---

    Suivi de la bande passante, et comment les synchronisations en pause reprennent d'elles-mêmes.

    [:octicons-arrow-right-24: Read](RateLimitHandling.md)

</div>

## 🔍 Utilisation de l'Archive {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Mail search__

    ---

    Syntaxe de requête, filtres, tri, pagination et actions groupées.

    [:octicons-arrow-right-24: Read](Search.md)

-   :material-progress-clock:{ .lg .middle } __Background jobs__

    ---

    Surveillez les synchronisations, imports, Restaurations et exports — et annulez-les en toute sécurité.

    [:octicons-arrow-right-24: Read](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Mailbox migration__

    ---

    Copiez des e-mails entre les Boîtes aux lettres tout en préservant la structure des Dossiers.

    [:octicons-arrow-right-24: Read](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Account export (EML / MBox)__

    ---

    Exportez un Compte entier sous forme d'Archive EML ou MBox portable.

    [:octicons-arrow-right-24: Read](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Original message storage__

    ---

    Conservez le message tel qu'il a été reçu, afin que le DKIM soit toujours vérifié et que les exports soient identiques au bit près.

    [:octicons-arrow-right-24: Read](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Attachment deduplication__

    ---

    Comment les Pièces jointes identiques sont stockées une seule fois au lieu de plusieurs.

    [:octicons-arrow-right-24: Read](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Date-windowed offload__

    ---

    Déplacez les e-mails plus anciens vers un autre Compte, fenêtre par fenêtre.

    [:octicons-arrow-right-24: Read](Offload.md)

-   :material-harddisk:{ .lg .middle } __Per-account storage__

    ---

    Voyez l'espace que prend chaque Boîte aux lettres.

    [:octicons-arrow-right-24: Read](AccountStorage.md)

</div>

## 🔐 Administration & Sécurité {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __User management__

    ---

    Créez des utilisateurs, accordez des droits d'administration, assignez des Autorisations de Boîte aux lettres et changez votre propre mot de passe.

    [:octicons-arrow-right-24: Read](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Two-factor authentication__

    ---

    Configuration TOTP, codes de secours, et quoi faire en cas de perte d'un appareil.

    [:octicons-arrow-right-24: Read](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Connectez-vous via Entra ID, Authelia ou un autre fournisseur d'identité.

    [:octicons-arrow-right-24: Read](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Access logging__

    ---

    Qui a ouvert, recherché, exporté ou supprimé quoi.

    [:octicons-arrow-right-24: Read](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit data export__

    ---

    Exportez la piste d'audit pour les revues de conformité.

    [:octicons-arrow-right-24: Read](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Emergency account recovery__

    ---

    Retrouvez l'accès lorsqu'un Compte est verrouillé.

    [:octicons-arrow-right-24: Read](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Retention policies__

    ---

    Suppression automatique du serveur de messagerie ou de l'Archive locale.

    [:octicons-arrow-right-24: Read](RetentionPolicies.md)

</div>

## ☁️ Guides par fournisseur {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure app registration (M365)__

    ---

    Enregistrez une application et configurez la Conservation pour Microsoft 365.

    [:octicons-arrow-right-24: Read](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 tenant mailbox import__

    ---

    Importez en masse chaque Boîte aux lettres d'un tenant à partir d'un seul formulaire.

    [:octicons-arrow-right-24: Read](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow instances__

    ---

    Sélectionnez les Boîtes aux lettres de manière centralisée et provisionnez des mots de passe d'application IMAP dédiés.

    [:octicons-arrow-right-24: Read](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Personal Microsoft accounts__

    ---

    Outlook.com, Hotmail et M365 Family via le flux de code d'appareil.

    [:octicons-arrow-right-24: Read](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail best practices__

    ---

    Mots de passe d'application, libellés, expunge et limites de débit de Gmail.

    [:octicons-arrow-right-24: Read](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo mail limitations__

    ---

    Ce que les points de terminaison IMAP et d'export de Yahoo ne feront pas.

    [:octicons-arrow-right-24: Read](YahooMailExportLimitations.md)

</div>

## 🔌 Accès programmatique {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Accès en lecture seule à l'Archive avec des clés API limitées par utilisateur.

    [:octicons-arrow-right-24: Read](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Exposez l'Archive aux agents IA en tant qu'outils découvrables.

    [:octicons-arrow-right-24: Read](MCP.md)

</div>

## 🧰 Opérations & Développement {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Language and localization__

    ---

    Changez la langue de l'interface et apprenez comment ajouter une traduction.

    [:octicons-arrow-right-24: Read](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose logs__

    ---

    Lisez les journaux des conteneurs et changez le niveau de log.

    [:octicons-arrow-right-24: Read](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Development versions__

    ---

    Passez à un tag de développement pour tester les changements à venir.

    [:octicons-arrow-right-24: Read](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Cette documentation est continuellement enrichie.**
Si vous remarquez une erreur ou avez une suggestion, veuillez
[open an issue](https://github.com/s1t5/mail-archiver/issues) ou soumettre une pull request.
</div>
