# 📧 Documentation de Mail Archiver {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Archivage d'e-mails auto-hébergé — archivez, recherchez et exportez des e-mails de n'importe quel fournisseur.**
Configurez-le une fois, conservez chaque message, et retrouvez-le des années plus tard.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Mise en route__

    ---

    Installez Mail Archiver avec Docker Compose et connectez votre première Boîte aux lettres.

    [:octicons-arrow-right-24: Configuration et paramètres](Setup.md)

-   :material-magnify:{ .lg .middle } __Trouver n'importe quoi__

    ---

    Recherche plein texte sur chaque Compte, avec filtres, tri et actions groupées.

    [:octicons-arrow-right-24: Recherche de mails](Search.md)

-   :material-api:{ .lg .middle } __Automatisez-le__

    ---

    Lisez votre Archive depuis des scripts et des agents IA — clés API en lecture seule et à portée limitée.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Sécurisez-le__

    ---

    Authentification à deux facteurs, SSO, journalisation des accès et politiques de Conservation.

    [:octicons-arrow-right-24: Authentification à deux facteurs](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Installation & Maintenance {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Configuration et paramètres__

    ---

    Chaque variable d'environnement, la gestion HTTPS de Kestrel et des secrets.

    [:octicons-arrow-right-24: Lire](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse proxy__

    ---

    Placez Mail Archiver derrière nginx ou Caddy avec terminaison TLS.

    [:octicons-arrow-right-24: Lire](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __Mise à niveau PostgreSQL__

    ---

    Passez à une nouvelle version majeure de PostgreSQL sans perdre de données.

    [:octicons-arrow-right-24: Lire](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Maintenance de la base de données__

    ---

    VACUUM, ANALYZE et maintien de la réactivité de l'Archive au fil du temps.

    [:octicons-arrow-right-24: Lire](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Sauvegarde et Restauration__

    ---

    Sauvegardez la base de données et restaurez-la — y compris les configurations Proxmox.

    [:octicons-arrow-right-24: Lire](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Environnement de test local__

    ---

    Exécutez une instance temporaire localement avant de toucher à la production.

    [:octicons-arrow-right-24: Lire](LocalTestEnvironment.md)

</div>

## 📥 Réception des mails {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Import de Compte__

    ---

    Ajoutez de nombreux comptes à la fois à partir d'un fichier CSV.

    [:octicons-arrow-right-24: Lire](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Synchronisation__

    ---

    Synchronisation rapide vs complète, points de contrôle, et ce qui se passe lorsqu'une synchronisation échoue.

    [:octicons-arrow-right-24: Lire](Synchronization.md)

-   :material-console:{ .lg .middle } __Commandes CLI__

    ---

    Importez des archives existantes depuis la ligne de commande, et déplacez les messages originaux stockés entre la base de données et le système de fichiers.

    [:octicons-arrow-right-24: Lire](CLI.md)

-   :material-upload:{ .lg .middle } __Import Web (EML / MBox)__

    ---

    Téléchargez un fichier MBox ou un ZIP de fichiers EML via le navigateur.

    [:octicons-arrow-right-24: Lire](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Gestion de la limite de débit__

    ---

    Suivi de la bande passante, et comment les synchronisations en pause reprennent d'elles-mêmes.

    [:octicons-arrow-right-24: Lire](RateLimitHandling.md)

</div>

## 🔍 Utilisation de l'Archive {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __Recherche de mails__

    ---

    Syntaxe de requête, filtres, tri, pagination et actions groupées.

    [:octicons-arrow-right-24: Lire](Search.md)

-   :material-progress-clock:{ .lg .middle } __Tâches de fond__

    ---

    Surveillez les synchronisations, imports, Restauration et exports — et annulez-les en toute sécurité.

    [:octicons-arrow-right-24: Lire](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Migration de Boîte aux lettres__

    ---

    Copiez les mails entre les Boîtes aux lettres tout en préservant la structure des Dossiers.

    [:octicons-arrow-right-24: Lire](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Export de Compte (EML / MBox)__

    ---

    Réexportez un Compte entier sous forme d'Archive EML ou MBox portable.

    [:octicons-arrow-right-24: Lire](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Stockage du message original__

    ---

    Conservez le message tel qu'il a été reçu, afin que le DKIM soit toujours vérifié et que les exports soient strictement identiques au niveau des octets.

    [:octicons-arrow-right-24: Lire](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Déduplication des Pièces jointes__

    ---

    Comment les Pièces jointes identiques sont stockées une seule fois au lieu de plusieurs.

    [:octicons-arrow-right-24: Lire](AttachmentDeduplication.md)

-   :material-file-pdf-box:{ .lg .middle } __Aperçu de facture XRechnung__

    ---

    Lisez les Pièces jointes XML XRechnung en tant que PDF directement dans l'Archive.

    [:octicons-arrow-right-24: Lire](EInvoicing.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Déchargement par fenêtre de date__

    ---

    Déplacez les anciens mails vers un autre Compte, fenêtre par fenêtre.

    [:octicons-arrow-right-24: Lire](Offload.md)

-   :material-harddisk:{ .lg .middle } __Stockage par Compte__

    ---

    Voyez l'espace que prend chaque Boîte aux lettres.

    [:octicons-arrow-right-24: Lire](AccountStorage.md)

</div>

## 🔐 Administration & Sécurité {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Gestion des utilisateurs__

    ---

    Créez des utilisateurs, accordez des droits d'administration, assignez des Autorisations de Boîte aux lettres et changez votre propre mot de passe.

    [:octicons-arrow-right-24: Lire](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Authentification à deux facteurs__

    ---

    Configuration TOTP, codes de secours, et que faire en cas de perte d'un appareil.

    [:octicons-arrow-right-24: Lire](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Connectez-vous via Entra ID, Authelia ou un autre fournisseur d'identité.

    [:octicons-arrow-right-24: Lire](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Journalisation des accès__

    ---

    Qui a ouvert, recherché, exporté ou supprimé quoi.

    [:octicons-arrow-right-24: Lire](Logs.md)

-   :material-file-export:{ .lg .middle } __Export des données d'audit__

    ---

    Exportez la piste d'audit pour les examens de conformité.

    [:octicons-arrow-right-24: Lire](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Récupération de Compte d'urgence__

    ---    Regagnez l'accès lorsqu'un Compte est verrouillé.

    [:octicons-arrow-right-24: Lire](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Politiques de Conservation__

    ---

    Suppression automatique du serveur de mail ou de l'Archive locale.

    [:octicons-arrow-right-24: Lire](RetentionPolicies.md)

</div>

## ☁️ Guides des Fournisseurs {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Enregistrement d'application Azure (M365)__

    ---

    Enregistrez une application et configurez la Conservation pour Microsoft 365.

    [:octicons-arrow-right-24: Lire](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __Import de Boîte aux lettres de tenant M365__

    ---

    Importez en masse chaque Boîte aux lettres d'un tenant à partir d'un formulaire unique.

    [:octicons-arrow-right-24: Lire](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Instances Mailcow__

    ---

    Sélectionnez les Boîtes aux lettres de manière centralisée et provisionnez des mots de passe d'application IMAP dédiés.

    [:octicons-arrow-right-24: Lire](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Comptes Microsoft personnels__

    ---

    Outlook.com, Hotmail et M365 Family via le flux de code d'appareil.

    [:octicons-arrow-right-24: Lire](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Bonnes pratiques Gmail__

    ---

    Mots de passe d'application, libellés, expunge et limites de débit de Gmail.

    [:octicons-arrow-right-24: Lire](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Limitations de Yahoo mail__

    ---

    Ce que les points de terminaison IMAP et d'export de Yahoo ne feront pas.

    [:octicons-arrow-right-24: Lire](YahooMailExportLimitations.md)

</div>

## 🔌 Accès Programmatique {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Accès en lecture seule à l'Archive avec des clés API par utilisateur à portée limitée.

    [:octicons-arrow-right-24: Lire](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Exposez l'Archive aux agents IA en tant qu'outils découvrables.

    [:octicons-arrow-right-24: Lire](MCP.md)

</div>

## 🧰 Opérations & Développement {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Langue et localisation__

    ---

    Changer la langue de l'interface, et comment ajouter une traduction.

    [:octicons-arrow-right-24: Lire](Localization.md)

-   :material-docker:{ .lg .middle } __Logs Docker Compose__

    ---

    Lire les logs des conteneurs et changer le niveau de log.

    [:octicons-arrow-right-24: Lire](DockerComposeLogs.md)

-   :material-heart-pulse:{ .lg .middle } __Santé et métriques__

    ---

    Surveillez la liveness, la readiness et les métriques de l'Archive depuis Prometheus ou un autre système de surveillance.

    [:octicons-arrow-right-24: Lire](Monitoring.md)

-   :material-tag:{ .lg .middle } __Versions de développement__

    ---

    Passez à un tag de dev pour tester les changements à venir.

    [:octicons-arrow-right-24: Lire](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Cette documentation est continuellement étendue.**
Si vous remarquez une erreur ou avez une suggestion, veuillez
[ouvrir un issue](https://github.com/s1t5/mail-archiver/issues) ou soumettre une pull request.
</div>
