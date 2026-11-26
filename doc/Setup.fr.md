# 🛠️ Guide de configuration de Mail Archiver {#mail-archiver-setup-guide}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide fournit des instructions détaillées pour la configuration de l'application Mail Archiver en utilisant Docker Compose.

## 🛠️ Prérequis {#prerequisites}

- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Étapes d'installation {#installation-steps}

1. Installez les prérequis sur votre système.

2. Créez un fichier `docker-compose.yml` avec le contenu suivant :

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=masterkey;

      # Authentication Settings
      - Authentication__Username=admin
      - Authentication__Password=secure123!
      - Authentication__SessionTimeoutMinutes=60
      - Authentication__CookieName=MailArchiverAuth
      - Authentication__CookieSameSite=Lax

      # REST API Settings (read-only, disabled by default)
      - Api__Enabled=false
      - Api__AllowAttachmentDownloads=true
      - Api__AllowMessageDownloads=true
      - Api__EnableSwaggerUi=true
      - Api__DefaultPageSize=20
      - Api__MaxPageSize=100
      - Api__RateLimitPerMinute=120

      # MCP Server Settings (Model Context Protocol for AI agents, read-only, disabled by default)
      - Mcp__Enabled=false
      - Mcp__AllowAttachmentDownloads=true
      - Mcp__DefaultPageSize=20
      - Mcp__MaxResults=100
      - Mcp__MaxAttachmentBytes=10000000
      - Mcp__AllowMessageDownloads=true
      - Mcp__MaxMessageBytes=10000000
      - Mcp__RateLimitPerMinute=120

      # MailSync Settings
      - MailSync__IntervalMinutes=15
      - MailSync__TimeoutMinutes=0
      - MailSync__ConnectionTimeoutSeconds=180
      - MailSync__CommandTimeoutSeconds=300
      - MailSync__AlwaysForceFullSync=false
      - MailSync__IgnoreSelfSignedCert=false
      - MailSync__MaxConcurrentSyncs=1
      - MailSync__InterAccountDelaySeconds=0
      - MailSync__MaxIssuesPerKind=20
      - MailSync__FullSyncIntervalHours=24
      - MailSync__ExcludeSubfolders=false
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Contacts

      # Original Message Storage (complete .eml per email, disabled by default)
      - RawMessages__Enabled=false
      - RawMessages__Storage=postgres
      - RawMessages__MaxMessageSizeMb=50
      - RawMessages__IncludeGraph=true
      - RawMessages__FileSystem__BasePath=RawMessages
      - RawMessages__FileSystem__ShardDepth=2
      - RawMessages__FileSystem__VerifyChecksumOnRead=true
      - RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds=300

      # BatchRestore Settings
      - BatchRestore__AsyncThreshold=50
      - BatchRestore__MaxSyncEmails=150
      - BatchRestore__MaxAsyncEmails=50000
      - BatchRestore__SessionTimeoutMinutes=30
      - BatchRestore__DefaultBatchSize=50

      # Tenant Management Settings
      - TenantManagement__MaxSelectedMailboxes=1000

      # Mailcow Settings
      - Mailcow__MaxSelectedMailboxes=1000

      # BatchOperation Settings
      - BatchOperation__BatchSize=50
      - BatchOperation__PauseBetweenEmailsMs=50
      - BatchOperation__PauseBetweenBatchesMs=250

      # Bandwidth Tracking Settings (for IMAP rate limit handling)
      - BandwidthTracking__Enabled=false
      - BandwidthTracking__DailyLimitMb=25000
      - BandwidthTracking__WarningThresholdPercent=80
      - BandwidthTracking__PauseHoursOnLimit=24
      - BandwidthTracking__TrackUploadBytes=false

      # Selection Settings
      - Selection__MaxSelectableEmails=250

      # View Settings (Privacy & Display)
      - View__DefaultToPlainText=true
      - View__BlockExternalResources=false

      # Dashboard Settings (statistics cache, counter parts, chart periods)
      - Dashboard__CacheSeconds=60
      - Dashboard__RefreshIntervalMinutes=15
      - Dashboard__ShowDirectionSplits=true
      - Dashboard__SelectablePeriods=true

      # Jobs Settings (background jobs page)
      - Jobs__RefreshSeconds=30

      # Npgsql Settings
      - Npgsql__CommandTimeout=900

      # Upload Settings for MBox and EML files
      - Upload__MaxFileSizeGB=10
      - Upload__KeepAliveTimeoutHours=4
      - Upload__RequestHeadersTimeoutHours=2

      # Local Import Settings (for CLI imports from mounted volumes)
      - LocalImport__AllowedPaths__0=/data/import

      # CSV Import Settings (for bulk IMAP account import)
      - CsvImport__MaxRows=5000
      - CsvImport__MaxFileSizeBytes=10000000

      # Deletion Policy Settings (Optional - controls whether email deletion is allowed)
      - DeletionPolicy__DeletionAllowed=true

      # TimeZone Settings
      - TimeZone__DisplayTimeZoneId=Etc/UCT

      # Database Maintenance Settings (Optional)
      - DatabaseMaintenance__Enabled=false
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30

      # Attachment Deduplication Settings (Optional - feature is always on)
      - AttachmentDeduplication__BatchSize=200
      - AttachmentDeduplication__DelayBetweenBatchesMs=0
      - AttachmentDeduplication__StartupDelaySeconds=20
      - AttachmentDeduplication__OrphanCleanupIntervalHours=12
      - AttachmentDeduplication__CommandTimeoutSeconds=300

      # Account Storage Settings (Optional - per-account storage display)
      - AccountStorage__Enabled=true
      - AccountStorage__DailyExecutionTime=02:30
      - AccountStorage__BackfillDelayMs=5000
      - AccountStorage__RefreshBatchDelayMs=1000
      - AccountStorage__CommandTimeoutSeconds=300

      # ReleaseNotes Settings (Version Update Splash Screen)
      - ReleaseNotes__Enabled=true
      - ReleaseNotes__UpdateHintEnabled=true
      - ReleaseNotes__UpdateCheckIntervalHours=6
      - ReleaseNotes__RequestTimeoutSeconds=10

      # Logging Settings (Optional - defaults to Information level)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning

      # Security Settings
      - AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com

      # OIDC Configuration (see OIDC_Implementation.md for detailed setup)
      - OAuth__Enabled=true
      - OAuth__Authority=https://example.com
      - OAuth__ClientId=YOUR-CLIENT-ID
      - OAuth__ClientSecret=YOUR-CLIENT-SECRET
      - OAuth__DisplayName=PocketID SSO
      - OAuth__ClientScopes__0=openid
      - OAuth__ClientScopes__1=profile
      - OAuth__ClientScopes__2=email
      - OAuth__DisablePasswordLogin=false
      - OAuth__AutoRedirect=false
      - OAuth__AutoApproveUsers=false
      - OAuth__AdminEmails__0=admin@example.com
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      # Uncomment the following line to mount a directory for local file imports
      # (also configure LocalImport__AllowedPaths__0=/data/import in environment variables)
      # - /path/to/your/mbox/files:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
      # Only with RawMessages__Storage=filesystem: keep the original messages outside the
      # container, otherwise they are lost when the container is recreated.
      # - ./raw-messages:/app/RawMessages
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data:/var/lib/postgresql/data
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s

networks:
  postgres:
```

3. Modifiez la configuration de la base de données dans le `docker-compose.yml` et définissez un mot de passe sécurisé dans la variable `POSTGRES_PASSWORD` et le `ConnectionString`.

4. Si vous souhaitez utiliser l'authentification (ce qui est fortement recommandé), définissez un `Authentication__Username` et `Authentication__Password` qui est utilisé pour l'utilisateur administrateur.

5. Ajustez la variable d'environnement `TimeZone__DisplayTimeZoneId` pour correspondre à votre fuseau horaire préféré (par défaut est "Etc/UCT"). Vous pouvez utiliser n'importe quel identifiant de fuseau horaire IANA (par exemple, "Europe/Berlin", "Asia/Tokyo").

6. Configurez optionnellement les variables d'environnement `Logging__LogLevel` pour contrôler la verbosité des journaux de l'application. Consultez la section Paramètres de journalisation ci-dessous pour les options disponibles.

7. Configurez un reverse proxy de votre choix avec https et l'authentification pour sécuriser l'accès à l'application. 

> ⚠️ **Attention** : L'application elle-même ne fournit pas d'accès chiffré via https ! Elle doit être configurée via un reverse proxy !

8. Premier démarrage des conteneurs :
```bash
docker compose up -d
```

9. Redémarrer les conteneurs :
```bash
docker compose restart
```

10. Accédez à l'application

11. Connectez-vous avec vos identifiants définis et ajoutez votre premier Compte de messagerie :
   - Naviguez vers la section "Email Accounts"
   - Cliquez sur "New Account"
   - Saisissez les détails de votre serveur et vos identifiants
   - Enregistrez et commencez l'archivage !
   - Si vous le souhaitez, créez d'autres utilisateurs et assignez des comptes.

## 📚 Explications des variables d'environnement {#environment-variable-explanations}

### 🗄️ Connexion à la base de données {#database-connection}
- `ConnectionStrings__DefaultConnection`: La chaîne de connexion à la base de données PostgreSQL. Modifiez les valeurs `Host`, `Database`, `Username` et `Password` selon les besoins.

### 🔐 Paramètres d'authentification {#authentication-settings}
- `Authentication__Username`: Le nom d'utilisateur pour le Compte admin.
- `Authentication__Password`: Le mot de passe pour le Compte admin.
- `Authentication__SessionTimeoutMinutes`: Le délai d'expiration de la session en minutes. Cela s'applique au cookie de connexion non persistant ; les utilisateurs qui cochent **Remember Me** sur le formulaire de connexion reçoivent un cookie persistant valide pour une période fixe de 7 jours à la place. Voir [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).
- `Authentication__CookieName`: Le nom du cookie d'authentification.
- `Authentication__CookieSameSite`: Configure l'attribut SameSite pour les cookies d'authentification, de session et de protection CSRF. Les valeurs valides sont :
  - `Strict` (par défaut) : Sécurité maximale. Les cookies ne sont envoyés qu'avec les requêtes same-site. Cela peut causer des problèmes lors de la navigation vers l'application à partir de liens externes (par exemple, en cliquant sur un lien depuis un autre site web), car la session existante ne sera pas reconnue.
  - `Lax` : Recommandé lors de l'utilisation d'un reverse proxy. Les cookies sont envoyés avec les navigations de haut niveau et les requêtes same-site, permettant aux utilisateurs de suivre des liens externes vers l'application tout en maintenant la protection CSRF pour les requêtes POST.
  - `None` : Les cookies sont envoyés avec toutes les requêtes. Nécessite HTTPS et l'attribut `Secure`. N'utilisez ceci que si vous avez des exigences cross-site spécifiques et que vous comprenez les implications en matière de sécurité.

### 🔌 Paramètres de l'API REST {#rest-api-settings}
L'API REST optionnelle en lecture seule est **désactivée par défaut**. Consultez le [guide de l'API REST](API.md) pour la référence complète.
- `Api__Enabled`: Commutateur principal pour l'API REST en lecture seule (par défaut `false`). Lorsque `false`, toutes les routes `/api/*` renvoient `404`.
- `Api__AllowAttachmentDownloads`: Autoriser le téléchargement des octets de la Pièce jointe via l'API (par défaut `true`). Lorsque `false`, le point de terminaison de la Pièce jointe renvoie `403`.
- `Api__AllowMessageDownloads`: Autoriser le téléchargement de messages complets via l'API, à la fois le `/eml` reconstruit et l'original `/raw` (par défaut `true`). Lorsque `false`, les deux points de terminaison renvoient `403`. Un message complet transporte ses Pièces jointes, donc **les deux**, ce commutateur et `Api__AllowAttachmentDownloads`, doivent être `true` ; désactiver l'un ou l'autre désactive les deux points de terminaison.
- `Api__EnableSwaggerUi`: Exposer l'interface Swagger UI à `/apidocs` et le document OpenAPI à `/apidocs/spec/v1.json` (par défaut `true`). Les deux nécessitent une session de navigateur connectée.
- `Api__DefaultPageSize`: Taille de page par défaut pour les points de terminaison de liste lorsqu'elle n'est pas spécifiée (par défaut `20`).
- `Api__MaxPageSize`: Taille de page maximale autorisée ; les requêtes plus grandes sont limitées (par défaut `100`).
- `Api__RateLimitPerMinute`: Budget de requête à fenêtre fixe par clé d'API par minute (par défaut `120`).

### 🤖 Paramètres du serveur MCP {#mcp-server-settings}
Le serveur MCP (Model Context Protocol) optionnel expose le même accès aux e-mails en lecture seule que l'API REST aux agents IA via le transport HTTP Streamable. Il est **désactivé par défaut**. Consultez le [guide MCP](MCP.md) pour la référence complète. MCP réutilise exactement les mêmes clés API que l'API REST (jetons de porteur `ma_...`) et le même cloisonnement de l'accès au Compte par utilisateur.
- `Mcp__Enabled`: Commutateur principal pour le serveur MCP (par défaut `false`). Lorsque `false`, le point de terminaison `/mcp` renvoie `404`.
- `Mcp__AllowAttachmentDownloads`: Autoriser l'outil `get_attachment` à renvoyer les octets de la Pièce jointe (par défaut `true`). Lorsque `false`, l'outil renvoie une erreur.
- `Mcp__DefaultPageSize`: Taille de page par défaut utilisée par `search_emails` lorsque l'appelant omet `pageSize` (par défaut `20`).
- `Mcp__MaxResults`: Limite supérieure pour la taille de page de `search_emails` ; les valeurs plus élevées sont plafonnées (par défaut `100`).
- `Mcp__MaxAttachmentBytes`: Taille maximale de la Pièce jointe (en octets) que l'outil `get_attachment` renverra en ligne sous forme de base64 ; les Pièces jointes plus volumineuses sont refusées (par défaut `10000000` ≈ 10 MB).
- `Mcp__AllowMessageDownloads`: Autoriser les outils `get_email_eml` et `get_email_raw` à renvoyer des messages complets (par défaut `true`). Lorsque `false`, les outils renvoient une erreur. Un message complet transporte ses Pièces jointes, donc **à la fois** ce commutateur et `Mcp__AllowAttachmentDownloads` doivent être `true` ; désactiver l'un ou l'autre désactive les deux outils.
- `Mcp__MaxMessageBytes`: Taille maximale du message (en octets) que les outils `get_email_eml` et `get_email_raw` renverront en ligne ; les messages plus volumineux sont refusés (par défaut `10000000` ≈ 10 MB).
- `Mcp__RateLimitPerMinute`: Budget de requête à fenêtre fixe par clé API par minute (par défaut `120`).

### 📨 Paramètres MailSync {#mailsync-settings}
- `MailSync__IntervalMinutes`: L'intervalle en minutes entre chaque Synchronisation d'e-mails. Il s'agit de la valeur par défaut globale ; chaque Compte peut la surcharger individuellement depuis la page Créer/Modifier (laisser vide pour utiliser cette valeur par défaut).
- `MailSync__FullSyncIntervalHours`: Valeur par défaut globale optionnelle pour les resynchronisations complètes automatiques, en heures. Lorsqu'elle n'est pas définie (par défaut), aucune synchronisation complète automatique ne s'exécute à moins qu'une valeur `FullSyncIntervalHours` par Compte ne soit définie sur la page Créer/Modifier. Les valeurs par Compte remplacent cette valeur par défaut globale.
- `MailSync__TimeoutMinutes`: Délai d'expiration de la synchronisation par Compte en minutes. Une synchronisation qui dure trop longtemps s'arrête à la prochaine limite de message, conserve ses points de contrôle et reprend lors de l'exécution suivante ; la tâche est signalée comme `Timed Out` et `LastSync` n'est pas avancé. S'applique uniquement aux synchronisations planifiées ; une synchronisation lancée depuis l'interface utilisateur (synchronisation manuelle ou resynchronisation complète) s'exécute sans délai d'expiration. `0` (par défaut) ou toute valeur non positive signifie aucun délai d'expiration. Vérifiez la valeur avant la mise à niveau si vous synchronisez de grandes Boîtes aux lettres. Voir [Synchronization.md](Synchronization.md#-stopping-a-sync-early).
- `MailSync__ConnectionTimeoutSeconds`: Le délai d'expiration de la connexion pour les connexions IMAP en secondes.
- `MailSync__CommandTimeoutSeconds`: Le délai d'expiration de la commande pour les commandes IMAP en secondes.
- `MailSync__AlwaysForceFullSync`: S'il faut toujours forcer une synchronisation complète (true/false).
- `MailSync__IgnoreSelfSignedCert`: S'il faut ignorer les certificats auto-signés (true/false).
- `MailSync__MaxConcurrentSyncs`: Nombre de synchronisations de Compte pouvant s'exécuter simultanément. Par défaut `1` (séquentiel, rétrocompatible). L'ordonnanceur remplit les créneaux dès qu'ils se libèrent plutôt que d'attendre un lot complet, de sorte qu'une Boîte aux lettres lente ne bloque plus les autres. Augmentez pour synchroniser plusieurs comptes simultanément — gardez à l'esprit les limites de débit des fournisseurs et l'utilisation des ressources locales. Voir [Synchronization.md](Synchronization.md#-how-accounts-are-scheduled).
- `MailSync__InterAccountDelaySeconds`: Délai de décalage optionnel en secondes appliqué à la fin de chaque tâche de synchronisation de Compte. Par défaut `0` (aucun délai). Utile pour éviter les démarrages en rafale lorsque `MaxConcurrentSyncs > 1`.
- `MailSync__MaxIssuesPerKind`: Nombre de problèmes de chaque type qu'une tâche de synchronisation mémorise pour la page du Compte. Les Dossiers échoués, les Dossiers manquants et les messages échoués sont budgétisés séparément, de sorte qu'une inondation d'un type ne peut pas évincer les autres ; tout ce qui dépasse le budget est comptabilisé plutôt que conservé. Par défaut `20`. `0` désactive le détail et ne laisse que les compteurs. Voir [Synchronization.md](Synchronization.md#-observing-the-sync).
- `MailSync__GlobalExcludedFolders__<n>`: Dossiers exclus de la Synchronisation pour **chaque** Compte, en plus de la propre liste de dossiers exclus de chaque Compte. Vide par défaut, donc les configurations existantes ne sont pas affectées. Les deux listes sont **additives** — un dossier est ignoré s'il correspond à l'une ou l'autre — et les deux utilisent les mêmes règles de correspondance : correspondance exacte sur le chemin complet, correspondance exacte sur le nom du dossier, une correspondance de suffixe de chemin (ainsi `Drafts` correspond aussi à `INBOX/Drafts` et `INBOX.Drafts`), et tout ce qui se trouve sous le dossier nommé lorsqu'`MailSync__ExcludeSubfolders` est activé. La correspondance est insensible à la casse. Utile lors de l'importation de nombreuses Boîtes aux lettres provenant du même serveur, où l'alternative est de maintenir une liste d'exclusion identique sur chaque Compte. Aucun dossier n'est exclu par défaut ; les noms qui valent la peine d'être listés dépendent du serveur et de sa langue. Exemple pour un arbre de Boîte aux lettres qui contient également des dossiers de calendrier et de contacts :
  ```yaml
      - MailSync__GlobalExcludedFolders__0=Calendar
      - MailSync__GlobalExcludedFolders__1=Calendars
      - MailSync__GlobalExcludedFolders__2=Contacts
      - MailSync__GlobalExcludedFolders__3=AddressBook
      - MailSync__GlobalExcludedFolders__4=Tasks
      - MailSync__GlobalExcludedFolders__5=TaskList
      - MailSync__GlobalExcludedFolders__6=Notes
      - MailSync__GlobalExcludedFolders__7=NoteList
      - MailSync__GlobalExcludedFolders__8=Journal
  ```
- `MailSync__ExcludeSubfolders`: Si une entrée d'exclusion couvre également les dossiers situés sous celui qu'elle nomme. S'applique aussi bien à la liste par Compte qu'à celle de l'installation, et aux deux fournisseurs. Par défaut `false`, correspondant au comportement avant l'existence de l'option : une entrée `Deleted Items` ne correspond qu'à ce dossier, et un arbre est maintenu hors de l'Archive en nommant sa racine et chaque dossier en dessous. Définissez sur `true` pour qu'une entrée prenne également l'arbre en dessous — une entrée `Deleted Items` maintient alors aussi `Deleted Items/2024` et le reste de cette branche hors de l'Archive. La règle s'ancre sur le séparateur de chemin, elle prend donc des dossiers entiers et jamais une partie d'un nom : `Archive` couvre `Archive` et `Archive/Team`, et laisse de côté un dossier de courrier appelé `Team Archive Review`. Un `.` compte également comme un séparateur, de la même manière que la règle de suffixe de chemin le traite.

### 📧 Paramètres de stockage du message original {#original-message-storage-settings}

Par défaut, l'Archive ne conserve que les parties analysées d'un e-mail, de sorte qu'un `.eml` exporté est une reconstruction : les encodages de transfert, les délimitations MIME et le repliement d'en-tête diffèrent du message tel que reçu, et les signatures DKIM ne sont plus vérifiables. Avec `RawMessages:Enabled`, l'original complet est également stocké.

| Paramètre | Valeur par défaut | Utilité |
|-----------|---------|---------|
| `RawMessages__Enabled` | `false` | Stocker l'original de chaque e-mail nouvellement archivé. |
| `RawMessages__Storage` | `postgres` | Où résident les octets : `postgres` ou `filesystem`. |
| `RawMessages__MaxMessageSizeMb` | `50` | Les messages plus volumineux sont archivés sans leur original. `0` = pas de limite. |
| `RawMessages__IncludeGraph` | `true` | Stocker également les originaux pour les Compte Microsoft 365 (Graph). |
| `RawMessages__FileSystem__BasePath` | `RawMessages` | Dossier pour les fichiers de messages. Doit pointer vers un volume monté dans Docker. |
| `RawMessages__FileSystem__ShardDepth` | `2` | Niveaux de répertoire sur lesquels l'id de l'e-mail est réparti. |
| `RawMessages__FileSystem__VerifyChecksumOnRead` | `true` | Vérifier le SHA-256 lors de chaque téléchargement. |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Supprimer les fichiers de messages auxquels aucun e-mail ne fait référence. |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Fréquence de passage de ce nettoyage. |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Les fichiers plus récents que cela ne sont jamais supprimés. |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Délai avant le premier nettoyage après le démarrage, afin qu'un import en cours d'écriture ne soit pas collecté. |

!!! warning "Le backend du système de fichiers nécessite un volume monté"
    Avec `RawMessages__Storage=filesystem`, les fichiers de messages résident en dehors de la base de données.
    Sans un bind mount, ils se trouvent dans la couche d'écriture du conteneur et sont perdus lors de la
    prochaine recréation du conteneur, alors que les lignes de la base de données survivent. Effectuez une Sauvegarde du dossier
    en même temps que le dump de la base de données.

Consultez **[Original Message Storage](RawMessages.md)** pour la description complète : où l'original est servi, les backends de stockage, comment déplacer les messages existants entre eux avec
`--migrate-raw-storage`, et ce qu'il faut sauvegarder.

### 📈 Paramètres de surveillance {#monitoring-settings}

| Paramètre | Valeur par défaut | Objectif |
|-----------|------------------|----------|
| `Health__SyncStaleMinutes` | `0` | Les comptes activés plus anciens que cette valeur génèrent un rapport `Degraded` de `/api/v1/health`. `0` désactive la vérification. |
| `Metrics__Enabled` | `false` | Active le point de terminaison des métriques Prometheus et sa boucle de rafraîchissement. |
| `Metrics__RefreshMinutes` | `5` | Fréquence à laquelle le snapshot des métriques est reconstruit. Les valeurs inférieures à 1 sont relevées à 1. |

Les points de terminaison de santé et de métriques nécessitent une clé API admin et `Api__Enabled=true`. Le propre contrôle de liveness du conteneur n'est pas authentifié et ne nécessite aucune configuration. Voir **[Health and Metrics](Monitoring.md)** pour les points de terminaison, les noms de métriques, la configuration Prometheus et le Docker health check.

### 📤 Paramètres de BatchRestore {#batchrestore-settings}
- `BatchRestore__AsyncThreshold`: Le nombre d'e-mails qui déclenche le traitement asynchrone.
- `BatchRestore__MaxSyncEmails`: Le nombre maximum d'e-mails pour le traitement synchrone.
- `BatchRestore__MaxAsyncEmails`: Le nombre maximum d'e-mails pour le traitement asynchrone.
- `BatchRestore__SessionTimeoutMinutes`: Le délai d'expiration de la session pour la Restauration par lots en minutes.
- `BatchRestore__DefaultBatchSize`: La taille de lot par défaut pour les opérations d'e-mails.

### 🏢 Paramètres de gestion de Tenant {#tenant-management-settings}
- `TenantManagement__MaxSelectedMailboxes`: Nombre maximum de Boîtes aux lettres pouvant être ajoutées lors d'une seule opération de gestion de Tenant. La valeur par défaut est `1000`. Augmentez cette valeur pour les tenants très volumineux, ou diminuez-la pour empêcher les imports de masse accidentels. Lorsque la limite est dépassée, l'opération est rejetée avec une erreur de validation et aucun Compte n'est créé. Consultez le [M365 Tenant Import Guide](M365TenantImport.md) pour plus de détails.

### 🐄 Paramètres Mailcow {#mailcow-settings}
- `Mailcow__MaxSelectedMailboxes`: Nombre maximum de Boîtes aux lettres pouvant être provisionnées lors d'un seul cycle d'activation Mailcow. La valeur par défaut est `1000`. Réduisez-la pour empêcher un provisionnement de masse accidentel, ou augmentez-la pour les instances de grande taille. Lorsque la limite est dépassée, aucun Compte n'est créé. Consultez [Mailcow Instances](Mailcow.md) pour plus de détails.
- L'intégration Mailcow nécessite également `Security__CredentialEncryptionKey` (voir [Security Settings](#-security-settings)). La connexion d'une instance et l'activation des Boîtes aux lettres sont toutes deux refusées tant que le chiffrement des identifiants est désactivé, de sorte que la clé API et les mots de passe d'application générés ne peuvent jamais être stockés en texte clair.
- Un accès sortant est requis vers l'API HTTPS de Mailcow et vers l'hôte IMAP via TLS (normalement le port `993`).

### 📦 Paramètres de BatchOperation {#batchoperation-settings}
- `BatchOperation__BatchSize`: La taille du lot pour les opérations d'e-mail.
- `BatchOperation__PauseBetweenEmailsMs`: La pause entre les e-mails individuels en millisecondes.
- `BatchOperation__PauseBetweenBatchesMs`: La pause entre les lots en millisecondes.

### 📤 Paramètres de déchargement {#offload-settings}
Paramètres pour le [date-windowed offload](Offload.md). Toutes les valeurs par défaut reproduisent le comportement que l'application avait avant l'existence de la fonctionnalité, de sorte qu'une installation qui ne configure pas cette section n'est pas affectée.
- `Offload__MaxConcurrentJobs`: Nombre de tâches de Restauration ou de déchargement pouvant s'exécuter simultanément. La valeur par défaut est `1`, ce qui maintient le traitement strictement séquentiel que la file d'attente des tâches a toujours utilisé. Au plus une tâche par Boîte aux lettres cible s'exécute, quelle que soit cette valeur.
- `Offload__PrefetchMaxMessages`: Limite supérieure du nombre de messages indexés à partir d'une Boîte aux lettres cible pour la détection de doublons. La valeur par défaut est `500000`. Au-delà, la vérification se restreint à un seul Dossier et enregistre que la portée a été réduite.
- `Offload__ExcludedSourceFolders__0`, `__1`, ...: Dossiers sources qui ne sont jamais déchargés, correspondants avant le renommage et couvrant les sous-dossiers. Vide par défaut.
- `Offload__FolderRenameMap__<SourceFolder>`: Réécrit les segments initiaux d'un chemin de Dossier source, par exemple `Offload__FolderRenameMap__Sent Items=Sent`. Vide par défaut.
- `Offload__MarkAsSeen`: Indique si les e-mails ajoutés sont marqués comme lus. La valeur par défaut est `true`, correspondant au comportement de Restauration existant.

Les deux paramètres de Dossier sont volontairement vides : réécrire ou supprimer des Dossiers sans y être sollicité surprendrait quiconque utilise déjà le chemin de Restauration. Une exécution sans configuration migre donc tout, Dossiers de spam inclus, et crée un second ensemble de dossiers spéciaux à côté de ceux de la cible.

### 📊 Paramètres de suivi de la bande passante {#bandwidth-tracking-settings}

- `BandwidthTracking__Enabled`: Activer ou désactiver le suivi de la bande passante pour la gestion de la limite de débit IMAP (true/false). La valeur par défaut est `false`. Lorsqu'il est activé, le système suit l'utilisation de la bande passante par Compte et peut mettre en pause la Synchronisation lorsque les limites du fournisseur sont atteintes. Consultez [Rate Limit Handling](RateLimitHandling.md) pour des informations détaillées.
- `BandwidthTracking__DailyLimitMb`: Limite de téléchargement quotidienne en mégaoctets par Compte. La valeur par défaut est `25000` (25 GB). Pour les fournisseurs avec des limites de bande passante, réglez cette valeur pour correspondre à leur limite de débit (par exemple, `2500` pour les fournisseurs avec des limites quotidiennes d'environ ~2500 MB). Le système mettra en pause la Synchronisation lorsque cette limite sera atteinte.
- `BandwidthTracking__WarningThresholdPercent`: Pourcentage de la limite quotidienne à partir duquel des messages d'avertissement sont enregistrés. La valeur par défaut est `80`. Lorsque l'utilisation de la bande passante atteint ce pourcentage, des avertissements sont enregistrés pour aider à surveiller l'approche des limites.
- `BandwidthTracking__PauseHoursOnLimit`: Nombre d'heures pour mettre en pause la Synchronisation lorsque la limite quotidienne est atteinte. La valeur par défaut est `24`. Après cette période, le drapeau de limite est automatiquement effacé et la Synchronisation reprend.
- `BandwidthTracking__TrackUploadBytes`: Si l'on doit également suivre la bande passante d'envoi (true/false). La valeur par défaut est `false`. La plupart des fournisseurs IMAP limitent uniquement les téléchargements, ce n'est donc généralement pas nécessaire.

### 🎯 Paramètres de sélection {#selection-settings}
- `Selection__MaxSelectableEmails`: Le nombre maximum d'e-mails pouvant être sélectionnés à la fois.

### 👁️ Paramètres de vue (Confidentialité et Affichage) {#view-settings-privacy-display}
- `View__DefaultToPlainText`: Contrôle le mode de vue d'e-mail par défaut pour la confidentialité et la prévention du suivi (true/false). La valeur par défaut est `false`.
  - Lorsque défini sur `true` : Les e-mails s'ouvrent par défaut en mode texte brut, empêchant le chargement automatique des pixels de suivi, des images externes et des balises web. Cela est recommandé pour les utilisateurs soucieux du suivi des e-mails et de la confidentialité.
  - Lorsque défini sur `false` : Les e-mails s'ouvrent par défaut en mode HTML, affichant l'e-mail formaté avec images et styles.
  - Les utilisateurs peuvent basculer entre les vues HTML et texte brut à l'aide d'un bouton sur la page des détails de l'e-mail (visible uniquement lorsque les deux formats sont disponibles).
  - Le lien "Full View" respecte le mode de vue actuellement sélectionné.

- `View__BlockExternalResources`: Bloque les ressources externes (images distantes, CSS externes, scripts externes, polices web, etc.) dans les vues d'e-mails HTML afin d'empêcher le suivi et d'améliorer la confidentialité (true/false). La valeur par défaut est `false`.
  - Lorsque défini sur `true` : Les ressources externes sont filtrées lors de l'affichage des e-mails HTML. Seuls le contenu en ligne et les URI de données (y compris les Pièces jointes en ligne via les références `cid:`) sont affichés.
  - Lorsque défini sur `false` : Les e-mails HTML sont affichés avec toutes leurs ressources externes d'origine.
  - **Important** : Ce paramètre n'affecte que l'**affichage** des e-mails. Les e-mails archivés sont stockés dans la base de données sans aucune modification, avec tout le contenu d'origine préservé.
  - Les ressources bloquées incluent :
    - Les images distantes (pixels de suivi, images externes hébergées sur des serveurs)
    - Les feuilles de style CSS externes
    - Les polices externes via @font-face
    - Les imports CSS externes via @import
    - Les images d'arrière-plan externes
  - Les ressources autorisées :
    - Les images intégrées en tant que data: URIs
    - Les Pièces jointes intégrées référencées via des URI cid:
    - Les styles CSS intégrés et les balises de style
  - Ce paramètre fonctionne indépendamment de `DefaultToPlainText` et fournit une couche supplémentaire de protection de la confidentialité lors de la consultation d'e-mails HTML.

### 📊 Paramètres du tableau de bord {#dashboard-settings}

- `Dashboard__ShowDirectionSplits`: Indique si les cartes de compteur portent leurs parties entrantes et sortantes, et si la carte de Compte porte le nombre de domaines distincts. La valeur par défaut est `true`. Si réglé sur `false`, ces nombres ne sont pas calculés plutôt que calculés et masqués : les compteurs lisent un simple décompte, et le nombre de Pièce jointe cesse de s'associer au mail auquel il est rattaché, ce qui correspond à la seule requête que ces parties ajoutent.
- `Dashboard__SelectablePeriods`: Indique si les graphiques du tableau de bord proposent une résolution, une période et des flèches pour les déplacer. La valeur par défaut est `true`. Si réglé sur `false`, les graphiques affichent les douze derniers mois par mois et les expéditeurs de l'ensemble de l'Archive, le point de terminaison du graphique répond comme s'il n'existait pas, et la date d'envoi la plus ancienne n'est pas recherchée.
- `Dashboard__CacheSeconds`: Durée pendant laquelle les statistiques calculées du tableau de bord (totaux avec leurs répartitions entrantes et sortantes, décomptes par Compte, séries de graphiques, principaux expéditeurs, emails récents, taille de la base de données) sont conservées dans le cache en mémoire du serveur. La valeur par défaut est `60` secondes. Réglez sur `0` pour désactiver la mise en cache et toujours recalculer les statistiques. Chaque sélection de résolution et de période pour un graphique est mise en cache individuellement, ainsi un tableau de bord qui bascule entre des sélections conserve une entrée par sélection. Des valeurs plus élevées réduisent la charge de la base de données dans les environnements de grande taille au prix de chiffres plus obsolètes. Les badges de statut de Synchronisation, les valeurs de stockage et la liste des emails récents sont toujours récupérés en direct et ne sont pas affectés par ce cache.
- `Dashboard__RefreshIntervalMinutes`: Fréquence à laquelle le service d'arrière-plan `DashboardStatsRefreshService` recalcule les agrégats coûteux du tableau de bord (comptages totaux avec leurs répartitions entrantes et sortantes, décomptes par Compte, nombre de domaines de Compte, la série de graphiques par défaut avec ses principaux expéditeurs, taille de la base de données) dans la table de la base de données `DashboardStatsCache`. Sur les installations de plusieurs millions de mails, le tableau de bord lit ces valeurs préparées au lieu d'agréger l'ensemble de l'Archive dans le chemin de la requête, ce qui transforme un premier chargement d'environ 10 secondes en quelques millisecondes. Le point de terminaison du graphique répond à la sélection par défaut (mois sur un an, expéditeurs entrants, période actuelle) à partir de la même ligne, de sorte que revenir à celle-ci affiche les chiffres du premier affichage ; toute autre sélection est calculée à la demande et conservée dans le cache en mémoire mentionné ci-dessus. La valeur par défaut est `15` minutes. Réglez sur `0` pour désactiver le rafraîchissement en arrière-plan ; le tableau de bord calcule alors à nouveau les statistiques en direct (avec l'application du cache en mémoire ci-dessus). Les valeurs affichées peuvent accuser un retard allant jusqu'à l'intervalle configuré par rapport à l'état réel de l'Archive. Une ligne calculée avec des valeurs `ShowDirectionSplits` ou `SelectablePeriods` différentes des valeurs actuelles n'est pas utilisée ; le tableau de bord calcule en direct jusqu'au prochain rafraîchissement.

### 🔄 Paramètres des tâches {#jobs-settings}
- `Jobs__RefreshSeconds`: La fréquence à laquelle la page des tâches en arrière-plan se recharge elle-même lorsqu'un onglet de navigateur l'a ouverte. La valeur par défaut est `30` secondes. Définissez sur `0` pour désactiver le rechargement automatique et rafraîchir manuellement. Chaque rechargement reconstruit la page à partir des huit sources de tâches, ainsi, sur les installations avec de nombreux comptes, un intervalle plus long limite la charge, multiplié par chaque onglet ouvert sur la page.

### 🗃️ Paramètres Npgsql {#npgsql-settings}
- `Npgsql__CommandTimeout`: Le délai d'expiration pour les commandes de base de données en secondes.

### 📥 Paramètres de téléchargement {#upload-settings}
- `Upload__MaxFileSizeGB`: La taille maximale des fichiers pour les téléchargements en GB.
- `Upload__KeepAliveTimeoutHours`: Le délai d'expiration keep alive pour les téléchargements en heures.
- `Upload__RequestHeadersTimeoutHours`: Le délai d'expiration pour les en-têtes de requête en heures.

### 📂 Paramètres d'importation locale {#local-import-settings}
- `LocalImport__AllowedPaths__0`, `LocalImport__AllowedPaths__1`, etc. : Liste blanche de répertoires locaux à partir desquels les commandes d'importation CLI (`--import-mbox`, `--import-eml`) sont autorisées à lire des fichiers. Chaque entrée est un chemin à l'intérieur du conteneur. Vous devez monter vos fichiers d'importation dans l'un de ces répertoires en utilisant des volumes Docker. Par défaut : `/data/import` (automatiquement défini sur `/app/uploads` comme repli).
  - Lors de l'utilisation de `docker exec` pour exécuter la commande d'importation, le chemin du fichier fourni via `--file` doit se trouver dans l'un de ces chemins autorisés.
  - Cette mesure de sécurité empêche l'accès arbitraire au système de fichiers à partir des commandes CLI.
  - Plusieurs chemins peuvent être configurés pour différentes sources d'importation.
  - Voir [CLI commands](CLI.md) pour des instructions d'utilisation détaillées.

### 📄 Paramètres d'importation CSV {#csv-import-settings}
- `CsvImport__MaxRows`: Nombre maximum de lignes CSV (Boîtes aux lettres) traitées lors d'une seule importation groupée. La valeur par défaut est `5000`. Augmentez cette valeur pour les déploiements de grande envergure ; diminuez-la pour limiter l'impact d'une seule exécution d'importation sur la charge de la base de données.
- `CsvImport__MaxFileSizeBytes`: Taille maximale autorisée (en octets) du fichier CSV téléchargé. La valeur par défaut est `10000000` (10 MB). Ajustez cette valeur pour correspondre à vos limites de téléchargement si nécessaire.
- Consultez le [Account Import Guide](Account-Import.md) pour des instructions détaillées sur l'utilisation de l'importation groupée de Compte IMAP via CSV.

### 📤 Paramètres d'exportation du Journal d'audit {#audit-export-settings}
Paramètres pour la page d'exportation des données d'audit (administrateur uniquement, accessible depuis la page Logs). L'exportation génère un package ZIP contenant des données de masse tabulaires (INDEX.XML + tables CSV + DTD) à partir de l'Archive existante pour les outils d'audit externes. Consultez le [Guide d'exportation des données d'audit](AuditExport.md) pour les détails d'utilisation.
- `AuditExport__DataSupplierName`: Valeur par défaut pour le champ de formulaire "Data supplier name" qui identifie votre organisation dans le fichier d'index exporté. Par défaut : vide.
- `AuditExport__DataSupplierLocation`: Valeur par défaut pour le champ de formulaire "Data supplier location" (ex. siège social). Par défaut : vide.
- `AuditExport__Comment`: Valeur par défaut pour le champ de formulaire "Comment", inclus sous forme de texte libre dans le fichier d'index exporté. Par défaut : vide.
- `AuditExport__OutputDirectory`: Répertoire où les fichiers ZIP générés sont stockés. Les chemins relatifs sont résolus par rapport à la racine du contenu de l'application. Par défaut : `exports/audit`.
- `AuditExport__RetentionDays`: Nombre de jours après lesquels les fichiers d'exportation terminés sont supprimés par le nettoyage quotidien. Par défaut : `30`.
- `AuditExport__MaxRangeYears`: Écart maximal autorisé entre la date de début et la date de fin de la période d'exportation. Par défaut : `10`.
- Les champs de formulaire sont pré-remplis à partir de ces valeurs par défaut mais peuvent être modifiés par exportation.
- Chaque exportation écrit une entrée de début et une entrée de résultat dans le journal d'accès (type "Audit Data Export"), de sorte que l'historique est protégé contre les révisions sans aucun changement de schéma de base de données.

### 🔒 Paramètres de la politique de suppression {#deletion-policy-settings}
- `DeletionPolicy__DeletionAllowed`: Contrôle si la suppression manuelle des e-mails archivés est autorisée (true/false). La valeur par défaut est `true`. Lorsqu'il est défini sur `false` :
  - Tous les e-mails archivés sont verrouillés (`IsLocked = true`) au démarrage via le déclencheur de conformité de la base de données, empêchant toute modification ou suppression au niveau de la base de données.
  - La suppression manuelle (individuelle et groupée) est bloquée au niveau de l'application avec un message d'erreur.
  - La valeur par défaut de la colonne est ajustée afin que les e-mails nouvellement importés soient également verrouillés.
  - L'état actuel de la politique est consigné dans la table AccessLogs à chaque démarrage (visible sur la page Logs sous forme d'entrées "Deletion Policy") pour l'auditabilité.
  - La suppression liée à la Conservation locale est exemptée : les e-mails qui tombent sous une période de Conservation configurée sont toujours supprimés (ils sont déverrouillés immédiatement avant la suppression au sein du processus de Conservation).
- `DeletionPolicy__MaxBulkDeleteEmails`: Limite supérieure pour une suppression groupée basée sur un filtre (*Delete all matches*). La valeur par défaut est `100000`. Un filtre qui correspond à plus d'e-mails est rejeté avec un message demandant de l'affiner ; le travail ne tronque jamais silencieusement. Les e-mails verrouillés par la politique de Conservation sont ignorés et signalés dans le statut du travail, sans être déverrouillés. Le travail vérifie à nouveau cette limite et `DeletionPolicy__DeletionAllowed` lorsqu'il démarre, de sorte qu'un travail mis en file d'attente avant un changement de politique s'arrête au lieu de s'exécuter avec l'ancien paramètre.
- **Protection d'immuabilité :** Lorsque `IsLocked = true`, le déclencheur de conformité de la base de données (`prevent_locked_email_changes`) bloque TOUTE modification de la ligne de l'e-mail — toutes les colonnes sont protégées, pas seulement une liste de champs fixes. Les seules colonnes exemptées sont `IsLocked` elle-même (afin que le déverrouillage pour la suppression liée à la Conservation et l'application de la politique au démarrage reste possible) et `FolderName` (afin que la Synchronisation IMAP puisse mettre à jour le Dossier lorsqu'un e-mail est déplacé côté serveur). La protection est agnostique vis-à-vis des colonnes (comparaison basée sur JSONB) et couvre automatiquement les futurs ajouts au schéma.

### 🕐 Paramètres de TimeZone {#timezone-settings}
- `TimeZone__DisplayTimeZoneId`: La zone horaire utilisée pour l'affichage des horodatages des e-mails dans l'interface utilisateur. Utilise les identifiants de zone horaire IANA (par exemple, "Europe/Berlin", "Asia/Tokyo"). La valeur par défaut est "Etc/UCT" pour la compatibilité ascendante. Lors de l'importation, les horodatages des e-mails seront convertis vers cette zone horaire à des fins d'affichage.

### 🎉 Paramètres ReleaseNotes (Écran de démarrage de mise à jour de version) {#releasenotes-settings-version-update-splash-screen}

- `ReleaseNotes__AppVersion`: La chaîne de version que cette instance rapporte. Elle est comparée aux tags de release GitHub et détermine si une version plus récente existe. Lorsqu'elle est vide, le changelog utilise la version de l'assembly compilé et l'indice de mise à jour reste désactivé.
- `ReleaseNotes__Enabled`: Activer ou désactiver l'écran de démarrage de mise à jour de version (true/false). La valeur par défaut est `true`. Lorsqu'il est activé, les administrateurs verront une fenêtre modale de changelog unique après une mise à jour de l'application, affichant les notes de version récupérées depuis GitHub Releases pour la version actuelle. Chaque administrateur peut fermer la modale, et elle ne réapparaîtra que pour une nouvelle version. Définissez sur `false` pour désactiver complètement cette fonctionnalité, y compris l'indice de mise à jour.
- `ReleaseNotes__UpdateHintEnabled`: Afficher aux administrateurs un petit toast dans le coin inférieur droit lorsque GitHub publie une version stable plus récente que la version installée (true/false). La valeur par défaut est `true`. Le toast contient un lien vers cette version et est ignoré par version dans le navigateur. Les brouillons et les pré-versions sont ignorés. Nécessite `ReleaseNotes__AppVersion` ; sans cela, aucun indice n'est affiché.
- `ReleaseNotes__UpdateCheckIntervalHours`: Fréquence de rafraîchissement de la liste des releases GitHub, en heures. La valeur par défaut est `6`. Un intervalle plus court rend une nouvelle version visible plus rapidement dans une instance de longue durée, un intervalle plus long réduit les appels à l'API GitHub. Le minimum est `1`.
- `ReleaseNotes__RequestTimeoutSeconds`: Délai d'expiration pour une requête API GitHub, en secondes. La valeur par défaut est `10`. Lorsqu'il s'écoule, le changelog et l'indice de mise à jour restent désactivés pour cette tentative au lieu de maintenir la requête ouverte ; la vérification suivante réessaiera.

### 🔧 Paramètres de maintenance de la base de données {#database-maintenance-settings}
- `DatabaseMaintenance__Enabled`: Activer ou désactiver la maintenance automatique quotidienne de la base de données (true/false). La valeur par défaut est `false`. Lorsqu'elle est activée, le système exécutera automatiquement des opérations VACUUM ANALYZE pour optimiser les performances de la base de données et prévenir l'accumulation de données inutiles. Consultez le [Database Maintenance Guide](DatabaseMaintenance.md) pour plus de détails.
- `DatabaseMaintenance__DailyExecutionTime`: L'heure de la journée à laquelle la maintenance de la base de données doit s'exécuter, au format 24 heures (HH:mm). La valeur par défaut est `02:00`. Choisissez un moment durant une période de faible activité du système.
- `DatabaseMaintenance__TimeoutMinutes`: Temps maximum autorisé pour les opérations de maintenance en minutes. La valeur par défaut est `30`. Augmentez cette valeur pour les bases de données plus volumineuses.

### 🧬 Paramètres de Déduplication des Pièces jointes {#attachment-deduplication-settings}

La déduplication des Pièces jointes stocke chaque charge utile de Pièce jointe unique une seule fois (adressée par contenu via SHA-256) et est une **fonctionnalité de base qui est toujours activée** – il n'y a intentionnellement pas d'interrupteur on/off. Seuls les paramètres de lot/planification ci-dessous peuvent être ajustés. Consultez le [Guide de Déduplication des Pièces jointes](AttachmentDeduplication.md) pour plus de détails.

- `AttachmentDeduplication__BatchSize`: Nombre de Pièces jointes existantes migrées par transaction lors de la migration en arrière-plan unique des données préexistantes. La valeur par défaut est `200`. Des valeurs plus élevées permettent une migration plus rapide mais utilisent plus de mémoire/charge DB par lot.
- `AttachmentDeduplication__DelayBetweenBatchesMs`: Pause optionnelle (en millisecondes) entre les lots de migration pour limiter la charge de la base de données sur les systèmes occupés. La valeur par défaut est `0` (pas de pause).
- `AttachmentDeduplication__StartupDelaySeconds`: Délai (en secondes) après le démarrage de l'application avant que la migration en arrière-plan ne commence, pour laisser le temps à la migration du schéma de se terminer. La valeur par défaut est `20`.
- `AttachmentDeduplication__OrphanCleanupIntervalHours`: Intervalle (en heures) du ramasse-miettes (garbage collection) toujours actif qui supprime les charges utiles de Pièces jointes qui ne sont plus référencées par aucun e-mail. La valeur par défaut est `12`. Cela s'exécute indépendamment de `DatabaseMaintenance__Enabled`.
- `AttachmentDeduplication__CommandTimeoutSeconds`: Délai d'expiration de la commande de base de données (en secondes) pour les opérations de lot de migration (INSERT avec hachage SHA-256 et UPDATE). La valeur par défaut est `300` (5 minutes). Augmentez cette valeur pour les bases de données ou les Pièces jointes très volumineuses, ou diminuez-la si vous souhaitez une détection d'échec plus rapide. Si un lot expire toujours, le service réessaie automatiquement avec une taille de lot réduite de moitié.

### 💾 Paramètres de stockage du Compte {#account-storage-settings}

L'affichage du stockage par Compte montre l'utilisation du stockage de la base de données (tous les champs d'e-mail + Pièces jointes) pour chaque Compte dans le tableau « Account Overview » du Dashboard et le tableau « Show All » de MailAccounts. Un service d'arrière-plan autarke (`AccountStorageRefreshService`) calcule les valeurs en additionnant les PostgreSQL `pg_column_size` par colonne (couvrant tous les champs d'un e-mail sans lire le corps de l'e-mail) et les met en cache dans la table `AccountStorageCache`. Ce service s'exécute indépendamment de `DatabaseMaintenance__Enabled`.

- `AccountStorage__Enabled`: Activer ou désactiver le service de rafraîchissement du stockage (true/false). La valeur par défaut est `true`. Lorsqu'il est activé, le service effectue un remplissage (backfill) repositionnable des comptes en attente au démarrage (sécurisé contre les crashs via `AccountStorageBackfillState`) et un rafraîchissement complet quotidien par la suite. Lorsqu'il est désactivé, les valeurs de stockage ne sont mises à jour que lorsque les e-mails sont synchronisés, importés ou supprimés, mais les valeurs affichées peuvent devenir obsolètes avec le temps.
- `AccountStorage__DailyExecutionTime`: Heure de la journée (format 24 heures `HH:mm`) pour le rafraîchissement complet quotidien de tous les comptes. La valeur par défaut est `02:30`. Choisissez une heure durant une période de faible activité du système.
- `AccountStorage__BackfillDelayMs`: Délai (en millisecondes) entre les comptes lors du remplissage initial au démarrage. La valeur par défaut est `5000`. Des valeurs plus basses accélèrent le remplissage mais augmentent la charge de la base de données ; augmentez cette valeur pour les Archives très volumineuses afin d'éviter de surcharger la base de données.
- `AccountStorage__RefreshBatchDelayMs`: Délai (en millisecondes) entre les comptes lors du rafraîchissement complet quotidien. La valeur par défaut est `1000`. Des valeurs plus basses accélèrent le rafraîchissement mais augmentent la charge de la base de données ; augmentez cette valeur pour les Archives très volumineuses.
- `AccountStorage__CommandTimeoutSeconds`: Délai d'expiration de la commande de la base de données (en secondes) pour le calcul du stockage par Compte (`pg_column_size` sur toutes les lignes d'un Compte). La valeur par défaut est `300` (5 minutes). Le calcul lit chaque ligne d'e-mail y compris les données TOAST, ce qui peut prendre bien plus d'une minute sur des Archives très volumineuses (plus de 250k e-mails) ; l'ancien défaut Npgsql de 30 secondes entraînait l'échec silencieux du rafraîchissement et laissait le dashboard afficher des valeurs obsolètes. Augmentez cette valeur si le calcul expire toujours sur votre Archive.

> 💡 **Note** : Les valeurs de stockage sont rafraîchies immédiatement après chaque synchronisation d'e-mail, import ou suppression de Conservation, de sorte que les valeurs affichées restent à jour même sans le rafraîchissement quotidien. Le rafraîchissement quotidien est un filet de sécurité qui capture les cas particuliers (par exemple, les modifications directes de la base de données).

### 📝 Paramètres de journalisation {#logging-settings}
- `Logging__LogLevel__Default`: Le niveau de journalisation par défaut pour l'application. Les niveaux disponibles sont : `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. La valeur par défaut est `Information`.
- `Logging__LogLevel__Microsoft_AspNetCore`: Niveau de journalisation pour les messages du framework ASP.NET Core. La valeur par défaut est `Warning`.
- `Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command`: Niveau de journalisation pour les commandes de base de données Entity Framework. La valeur par défaut est `Warning`.

### 🛡️ Paramètres de sécurité {#security-settings}
- `AllowedHosts`: Une liste de noms d'hôtes séparés par des points-virgules que l'application est autorisée à servir. Cela aide à prévenir les attaques HTTP Host header. Exemple : `AllowedHosts=mailarchiver.example.com;www.mailarchiver.example.com`. **Important** : N'utilisez pas `*` dans les environnements de production car cela désactive la validation du Host header.

#### 🔐 Chiffrement des identifiants au repos {#credential-encryption-at-rest}
Les identifiants du Compte de messagerie (mot de passe IMAP, client secret M365 et jetons OAuth MSA) peuvent être chiffrés dans la base de données. La clé de chiffrement est conservée **en dehors** de la base de données (variable d'environnement / Docker secret), de sorte qu'un simple dump de la base de données ne suffit pas pour lire les identifiants.

- `Security__CredentialEncryptionKey`: Clé de chiffrement de 256 bits (32 octets) encodée en Base64. Générez-en une avec :
  ```bash
  openssl rand -base64 32
  ```
  Définissez la valeur comme une variable d'environnement (recommandé : `Security__CredentialEncryptionKey` dans votre fichier compose ou environnement de conteneur). L'application ne prend pas en charge les Docker secrets de style `_FILE` — si vous utilisez un Docker secret, montez-le et chargez son contenu dans la variable d'environnement de votre entrypoint. Ne commitez jamais la clé dans `appsettings.json`. Lorsqu'une clé est configurée, les identifiants sont chiffrés lors de l'écriture, et un service en arrière-plan chiffre les identifiants en texte clair existants une seule fois.
- `Security__EncryptAccountCredentials`: Activer ou désactiver le chiffrement des nouvelles/mises à jour des valeurs d'identifiants (true/false). La valeur par défaut est `true`. Lorsqu'aucune clé n'est configurée, les identifiants sont stockés en texte clair et un avertissement est consigné dans le journal d'audit au démarrage — les installations existantes ne sont pas affectées tant qu'une clé n'est pas fournie.
- `Security__CredentialEncryptionBackfill`: Indique si le service en arrière-plan doit chiffrer les identifiants en texte clair existants au démarrage (true/false). La valeur par défaut est `true`. Opération nulle sans clé configurée.
- `Security__CredentialEncryptionKeyPrevious`: Ancienne clé optionnelle, utilisée uniquement pour déchiffrer les valeurs qui y ont été chiffrées (pour la rotation de clé). Les nouvelles valeurs sont toujours chiffrées avec `Security__CredentialEncryptionKey`.

  > ⚠️ **Rotation de clé** : après la rotation, conservez `Security__CredentialEncryptionKeyPrevious` configurée **jusqu'à ce que chaque identifiant ait été réécrit** (par exemple, en modifiant le Compte ou par un rafraîchissement automatique du jeton OAuth) — les valeurs existantes chiffrées avec l'ancienne clé ne sont *pas* automatiquement rechiffrées avec la nouvelle clé. Une fois que vous supprimez l'ancienne clé, tout identifiant encore chiffré avec celle-ci devient illisible et doit être saisi à nouveau. Tant qu'elle est configurée, elle doit être conservée aussi précieusement que la clé actuelle.

> ⚠️ **Gardez la clé en sécurité** : Sans la clé, les identifiants chiffrés ne peuvent pas être récupérés et doivent être saisis à nouveau. Effectuez une Sauvegarde de celle-ci en même temps que (mais séparément de) vos Sauvegarde de base de données.
>
> 💡 **Installations existantes** : Mettez à jour en toute sécurité — tant qu'aucune `Security__CredentialEncryptionKey` n'est définie, rien ne change. Définissez la clé lorsque vous êtes prêt ; les identifiants en texte clair existants restent lisibles et sont chiffrés en arrière-plan.

### 🔐 Configuration OIDC {#oidc-configuration}

Pour des instructions de configuration détaillées concernant l'authentification OpenID Connect, consultez le [OIDC Implementation Guide](OIDC_Implementation.md).

#### Paramètres OIDC de base {#basic-oidc-settings}
- `OAuth__Enabled`: Activer ou désactiver l'authentification OIDC (true/false)
- `OAuth__Authority`: L'URL de l'autorité OpenID Connect (par exemple, https://sts.windows.net/{TENANT-ID}/ pour Azure AD)
- `OAuth__ClientId`: Le client ID assigné par votre fournisseur d'identité
- `OAuth__ClientSecret`: Le client secret assigné par votre fournisseur d'identité
- `OAuth__DisplayName`: Nom d'affichage optionnel affiché sur le bouton de connexion OIDC et la page de redirection automatique (par exemple, `PocketID SSO`). Si omis, l'étiquette générique "Login with OAuth" est utilisée.
- `OAuth__ClientScopes__0`: Premier scope demandé au fournisseur d'identité (openid)
- `OAuth__ClientScopes__1`: Deuxième scope demandé au fournisseur d'identité (profile)
- `OAuth__ClientScopes__2`: Troisième scope demandé au fournisseur d'identité (email)
- `OAuth__ResponseMode`: Mode de réponse optionnel de la requête d'autorisation (`query`, `fragment` ou `form_post`). La valeur par défaut est `form_post`, la valeur par défaut d'ASP.NET Core. Définissez sur `query` pour les fournisseurs qui ne supportent pas `form_post`, comme Synology SSO Server. Voir [Provider Compatibility](OIDC_Implementation.md#provider-compatibility).
- `OAuth__AutoApproveUsers`: Approuver automatiquement les nouveaux utilisateurs OIDC sans nécessiter d'approbation manuelle de l'administrateur (true/false). La valeur par défaut est `false`.

#### Paramètres de provisionnement des utilisateurs {#user-provisioning-settings}
- `OAuth__AutoApproveUsers`: Approuver automatiquement les nouveaux utilisateurs OIDC sans nécessiter d'approbation manuelle de l'administrateur (true/false). La valeur par défaut est `false`. Lorsqu'il est activé, les utilisateurs qui s'authentifient via le fournisseur OIDC sont immédiatement activés et peuvent accéder à l'application. Lorsqu'il est désactivé (par défaut), les nouveaux utilisateurs OIDC sont créés comme inactifs et nécessitent une activation manuelle par un administrateur. Voir [Auto-Approve OIDC Users](OIDC_Implementation.md#auto-approve-oidc-users) pour des informations détaillées.
- `OAuth__AdminEmails__0`, `OAuth__AdminEmails__1`, etc. : Adresses e-mail qui doivent être automatiquement provisionnées en tant qu'administrateurs. Les utilisateurs avec ces adresses e-mail seront créés en tant qu'admins actifs lors de la première connexion OAuth, contournant le processus d'approbation normal. La correspondance des e-mails ne tient pas compte de la casse.

#### Paramètres de connexion sans mot de passe {#passwordless-login-settings}
- `OAuth__DisablePasswordLogin`: Désactiver la connexion par nom d'utilisateur/mot de passe (true/false). La valeur par défaut est `false`. Lorsqu'il est activé, le formulaire de mot de passe est masqué **et** le serveur rejette chaque POST de mot de passe, de sorte que seule la connexion OAuth est disponible. Nécessite `OAuth__Enabled=true`. Si OAuth est désactivé, ce paramètre n'a aucun effet.
- `OAuth__AutoRedirect`: Rediriger automatiquement les utilisateurs vers le fournisseur OAuth (true/false). La valeur par défaut est `false`. Nécessite que `OAuth__DisablePasswordLogin` soit `true`. Les utilisateurs verront un bref écran de chargement avant d'être redirigés.

#### Exemple : Configuration complète OIDC-First {#example-full-oidc-first-configuration}
```yaml
environment:
  - OAuth__Enabled=true
  - OAuth__Authority=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0
  - OAuth__ClientId=your-client-id
  - OAuth__ClientSecret=your-client-secret
  - OAuth__DisplayName=PocketID SSO
  - OAuth__ClientScopes__0=openid
  - OAuth__ClientScopes__1=profile
  - OAuth__ClientScopes__2=email
  - OAuth__DisablePasswordLogin=true
  - OAuth__AutoRedirect=true
  - OAuth__AutoApproveUsers=true
  - OAuth__AdminEmails__0=admin@example.com
  - OAuth__AdminEmails__1=manager@example.com
```

## 🔐 Configuration HTTPS de Kestrel (Optionnel) {#kestrel-https-configuration-optional}

Bien que l'application soit destinée à être accédée via un reverse proxy avec HTTPS, vous pouvez également configurer le serveur web Kestrel pour utiliser des certificats SSL/TLS. Cela fournit un chiffrement de bout en bout entre le reverse proxy et le conteneur de l'application.

### Étapes de configuration {#configuration-steps}

1. **Générez ou obtenez un certificat SSL** au format PFX (ex: `localhost.pfx`)

2. **Ajoutez les variables d'environnement suivantes** à votre `docker-compose.yml` pour le service `mailarchive-app` :

```yaml
environment:
  # Kestrel HTTPS Settings
  - Kestrel__Endpoints__Http__Url=http://0.0.0.0:5000
  - Kestrel__Endpoints__Https__Url=https://0.0.0.0:5001
  - Kestrel__Endpoints__Https__Certificate__Path=/https/localhost.pfx
  - Kestrel__Endpoints__Https__Certificate__Password=MyPassword
```

3. **Mettez à jour le mappage des ports** dans le service `mailarchive-app` :

```yaml
ports:
  - "5000:5000"
  - "5001:5001"  # HTTPS port
```

4. **Ajoutez un mappage de volume** pour le certificat :

```yaml
volumes:
  - ./data-protection-keys:/app/DataProtection-Keys
  - ./certs:/https  # Certificate directory
```

5. **Placez votre fichier de certificat** (ex: `localhost.pfx`) dans le répertoire `./certs` de votre système hôte.

### Explications des variables d'environnement {#environment-variable-explanations}

- `Kestrel__Endpoints__Http__Url`: URL du point de terminaison HTTP (par défaut : http://0.0.0.0:5000)
- `Kestrel__Endpoints__Https__Url`: URL du point de terminaison HTTPS (par défaut : https://0.0.0.0:5001)
- `Kestrel__Endpoints__Https__Certificate__Path`: Chemin vers le fichier de certificat PFX à l'intérieur du conteneur
- `Kestrel__Endpoints__Https__Certificate__Password`: Mot de passe pour le fichier de certificat PFX

> 💡 **Note** : Cette configuration est optionnelle. Si vous utilisez un reverse proxy avec HTTPS (recommandé), la communication entre le reverse proxy et l'application peut rester en HTTP. Cependant, pour une sécurité maximale dans des environnements sensibles, vous pouvez également vouloir activer HTTPS sur Kestrel afin de chiffrer l'intégralité du chemin de communication.

## 🔑 Gestion des secrets pour la production {#secrets-management-for-production}

Le codage en dur de données sensibles telles que les mots de passe de base de données, les identifiants d'administrateur et les secrets de client OAuth directement dans `docker-compose.yml` est un risque de sécurité, en particulier lorsque le fichier est versionné. L'approche recommandée consiste à externaliser ces valeurs dans un fichier `.env` que Docker Compose charge automatiquement.

### 📄 Le fichier `.env` {#the-env-file}

Créez un fichier `.env` dans le même répertoire que votre `docker-compose.yml` avec le contenu suivant :

```env
# Database
POSTGRES_PASSWORD=YourSecureDBPassword123!

# Admin Account
AUTH_USERNAME=admin
AUTH_PASSWORD=YourSecureAdminPassword456!

# OIDC / OAuth Secrets
OAUTH_CLIENT_SECRET=YourOAuthClientSecret

# Credential Encryption at Rest (generate with: openssl rand -base64 32)
CREDENTIAL_ENCRYPTION_KEY=YourBase64Encoded32ByteKey

# Kestrel HTTPS (optional)
KESTREL_CERT_PASSWORD=YourCertPassword
```

**Important** : Docker Compose lit automatiquement le fichier `.env` du même répertoire — vous n'avez **pas** besoin de le référencer manuellement. Toutes les variables définies dans `.env` sont disponibles dans `docker-compose.yml` via la syntaxe `${VARIABLE_NAME}`.

### 🐳 `docker-compose.yml` adapté {#adapted-docker-composeyml}

Le `docker-compose.yml` des étapes d'installation doit être adapté pour utiliser des espaces réservés au lieu de secrets codés en dur :

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=${POSTGRES_PASSWORD}

      # Authentication Settings
      - Authentication__Username=${AUTH_USERNAME}
      - Authentication__Password=${AUTH_PASSWORD}
      # ... other settings remain unchanged ...

      # OIDC Configuration
      - OAuth__ClientSecret=${OAUTH_CLIENT_SECRET}

      # Credential Encryption at Rest
      - Security__CredentialEncryptionKey=${CREDENTIAL_ENCRYPTION_KEY}
    # ... ports, volumes, networks ...

  postgres:
    image: postgres:17-alpine
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    # ... volumes, networks ...
```

### 📋 Modèle `.env.example` pour les administrateurs {#envexample-template-for-administrators}

Créez un fichier `.env.example` dans votre dépôt qui sert de modèle pour les autres administrateurs. Il contient des valeurs d'espace réservé et de la documentation mais **aucun vrai secret** :

```env
# ============================================
# Mail Archiver - Environment Configuration
# ============================================
# Copy this file to .env and fill in your values.
# Never commit the actual .env file to version control!

# --- PostgreSQL ---
POSTGRES_PASSWORD=change_me_db_password

# --- Admin Account ---
AUTH_USERNAME=admin
AUTH_PASSWORD=change_me_admin_password

# --- OIDC / OAuth ---
OAUTH_CLIENT_SECRET=change_me_client_secret

# --- Credential Encryption at Rest ---
# Generate with: openssl rand -base64 32
CREDENTIAL_ENCRYPTION_KEY=change_me_base64_32_byte_key

#...and so on
```

### ✅ Bonnes pratiques {#best-practices}

- **Utilisez `.env.example` comme modèle**
- **Définissez des Autorisations de fichier strictes** — Restreignez l'accès au fichier `.env` :
  ```bash
  chmod 600 .env
  ```
- **Utilisez des mots de passe forts** — Générez des mots de passe longs et aléatoires (au moins 16 caractères) avec un mélange de lettres, de chiffres et de caractères spéciaux.
- **Limitez l'accès au fichier `.env`** — Seul l'utilisateur exécutant Docker Compose doit avoir les Autorisations de lecture pour le fichier `.env`.

## 🔒 Notes de sécurité {#security-notes}

- Utilisez des mots de passe robustes et changez les identifiants par défaut. Les mots de passe doivent comporter au moins 12 caractères et inclure un mélange de lettres majuscules, de lettres minuscules, de chiffres et de caractères spéciaux. Évitez d'utiliser des mots courants ou des informations faciles à deviner.
- En production, envisagez d'implémenter HTTPS avec un reverse proxy.
- Des Sauvegardes régulières de la base de données PostgreSQL sont recommandées. Pour des procédures détaillées de Sauvegarde et de Restauration, consultez le [Guide de Sauvegarde et Restauration](BackupRestore.md).
