# 💾 Guide de Sauvegarde et Restauration {#backup-and-restore-guide}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide fournit des instructions détaillées pour la sauvegarde et la restauration de votre environnement Mail Archiver Docker. Des sauvegardes régulières sont essentielles pour protéger vos e-mails archivés et vos données de configuration.

## ⚠️ Avertissement Important {#important-disclaimer}

**Tous les scripts et commandes fournis dans ce guide ne sont que des exemples.** Aucune garantie ou responsabilité n'est assumée quant à leur fonctionnalité ou leur adéquation à votre environnement spécifique. Veillez toujours à :
- Tester les procédures de sauvegarde et de restauration dans un environnement hors production au préalable
- Vérifier que les sauvegardes sont complètes et restaurables
- Adapter les scripts et les commandes à votre configuration et à vos exigences spécifiques
- Conserver plusieurs copies de sauvegarde dans différents emplacements
- Tester régulièrement vos procédures de reprise après sinistre

**Utilisez ces informations à vos propres risques.**

## 🎯 Recommandations de Sauvegarde {#backup-recommendations}

### Sauvegardes Hors Ligne (Fortement Recommandées) {#offline-backups-highly-recommended}
Pour une cohérence et une intégrité maximales des données, **effectuez toujours les sauvegardes lorsque l'application et la base de données sont arrêtées**. Cela garantit :
- Aucune transaction de base de données active pendant la sauvegarde
- Une cohérence complète des données sur tous les fichiers
- Aucun risque de corruption de fichier dû à des écritures simultanées
- Une capacité de restauration fiable

Bien que les sauvegardes à chaud (Méthodes 2 et 3) soient pratiques pour minimiser le temps d'arrêt, elles comportent un risque plus élevé d'incohérences. Pour les environnements de production critiques, planifiez des sauvegardes hors ligne régulières pendant les fenêtres de maintenance.

### Sauvegardes au Niveau Système {#system-level-backups}
En plus des sauvegardes spécifiques à l'application, il est **fortement recommandé** de créer des sauvegardes de l'ensemble de l'infrastructure système :
- **Sauvegardes de Conteneurs LXC** : Si vous utilisez un conteneur LXC, créez des instantanés (snapshots) hors ligne de l'ensemble du conteneur
- **Sauvegardes de Machines Virtuelles** : Pour les déploiements VM, créez des instantanés complets de la VM
- **Sauvegardes du Système Hôte** : Incluez les volumes Docker, les configurations de conteneurs et les paramètres système
- **Fichiers de Configuration** : Sauvegardez séparément tous les fichiers de configuration Docker et système

Les sauvegardes au niveau système fournissent une couche de sécurité supplémentaire et permettent une reprise après sinistre complète, y compris la possibilité de restaurer l'ensemble de l'environnement sur un autre hôte si nécessaire.

## 🛠️ Prérequis {#prerequisites}

- Installation Mail Archiver Docker en cours d'exécution (voir [Guide d'Installation](Setup.md))
- Accès au système hôte Docker
- Espace de stockage suffisant pour les sauvegardes

## 📦 Quoi Sauvegarder {#what-to-backup}

L'environnement Mail Archiver Docker se compose de plusieurs volumes de données importants qui doivent être sauvegardés :

### 1. Base de données PostgreSQL {#1-postgresql-database}
- **Volume** : `./postgres-data`
- **Contient** : Tous les e-mails archivés, les comptes utilisateurs, les comptes de messagerie et les données de l'application
- **Importance** : ⭐⭐⭐ Critique - Il s'agit de votre principal magasin de données

### 2. Clés de Protection des Données {#2-data-protection-keys}
- **Volume** : `./data-protection-keys`
- **Contient** : Les clés ASP.NET Core Data Protection pour le chiffrement des cookies et l'authentification
- **Importance** : ⭐⭐ Important - Requis pour les sessions utilisateur existantes et l'authentification sécurisée

### 2a. Fichiers de Messages Originaux (uniquement avec `RawMessages.Storage=filesystem`) {#2a-original-message-files-only-with-rawmessagesstoragefilesystem}
- **Volume** : `./raw-messages` (le côté hôte de `/app/RawMessages`)
- **Contient** : L'original `.eml` de chaque e-mail, compressé en gzip, écrit lorsque `RawMessages:Enabled` est défini et que le backend de stockage est `filesystem`
- **Importance** : ⭐⭐⭐ Critique **lorsqu'ils sont utilisés** - la base de données ne conserve que la somme de contrôle (checksum), la taille et le chemin relatif, donc une restauration sans ce répertoire laisse des originaux qui ne peuvent pas être servis
- Sauvegardez ce répertoire et le dump de la base de données **ensemble**. Restaurer la base de données sans les fichiers n'est pas une erreur que l'Archive peut réparer : les téléchargements répondront `404` et les sommes de contrôle ne peuvent pas être recalculées à partir d'autre chose. Restaurer les fichiers sans la base de données est tout aussi inutile, car rien ne les référence.
- Si vous utilisez `RawMessages.Storage=postgres` (par défaut), ce répertoire n'existe pas et les originaux se trouvent à l'intérieur de `./postgres-data` comme auparavant.
- Voir [Stockage des Messages Originaux](RawMessages.md) pour la fonctionnalité elle-même et comment changer de backend.

### 3. Configuration Docker Compose {#3-docker-compose-configuration}
- **Fichier** : `docker-compose.yml`
- **Contient** : La configuration des conteneurs, les variables d'environnement et les paramètres
- **Importance** : ⭐⭐ Important - Contient la configuration de votre application

## 🔄 Procédures de Sauvegarde {#backup-procedures}

### Méthode 1 : Sauvegarde Complète (Recommandée) {#method-1-complete-backup-recommended}

Cette méthode sauvegarde toutes les données pendant que les conteneurs sont arrêtés pour garantir la cohérence des données.

```bash
# 1. Stop the containers
docker compose down

# 2. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 3. Copy all data volumes
cp -r postgres-data "$BACKUP_DIR/"
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Copy docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

# 7. Start the containers again
docker compose up -d

echo "Backup completed: $BACKUP_DIR.tar.gz"
```

### Méthode 2 : Sauvegarde à Chaud (Temps d'Arrêt Minimal) {#method-2-hot-backup-minimal-downtime}

Cette méthode crée une sauvegarde complète pendant que le système est en cours d'exécution avec un temps d'arrêt minimal.

**Note :** Remplacez `mailuser` et `MailArchiver` par vos véritables identifiants de base de données provenant de `docker-compose.yml` (vérifiez les variables d'environnement `POSTGRES_USER` et `POSTGRES_DB`).

```bash
# 1. Create a backup directory with timestamp
mkdir -p backups
BACKUP_DIR="backups/mailarchiver-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 2. Backup the database
# Replace 'mailuser' and 'MailArchiver' with your actual POSTGRES_USER and POSTGRES_DB
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > "$BACKUP_DIR/database.sql"

# 3. Backup data protection keys (these change rarely, can be done while running)
cp -r data-protection-keys "$BACKUP_DIR/"

# 4. Backup docker-compose configuration
cp docker-compose.yml "$BACKUP_DIR/"

# 5. Create a compressed archive
tar -czf "$BACKUP_DIR.tar.gz" -C backups "$(basename $BACKUP_DIR)"

# 6. Remove uncompressed backup directory
rm -rf "$BACKUP_DIR"

echo "Hot backup completed: $BACKUP_DIR.tar.gz"
```

### Méthode 3 : Script de Sauvegarde Automatisé {#method-3-automated-backup-script}

Pour des sauvegardes automatisées régulières, nous fournissons un script de sauvegarde prêt à l'emploi. Le script est disponible dans [backup.sh](attachments/backup.sh).

**Caractéristiques :**
- ✅ Détection automatique des identifiants de base de données depuis docker-compose.yml
- ✅ Sauvegarde à chaud (temps d'arrêt minimal)
- ✅ Mécanisme de fichier de verrouillage pour empêcher les sauvegardes simultanées
- ✅ Vérification de l'espace disque avant la sauvegarde
- ✅ Nettoyage automatique des anciennes sauvegardes

**Configuration :**

Avant d'utiliser le script, modifiez la section de configuration en haut du fichier :

```bash
# Required settings:
MAILARCHIVER_DIR="/path/to/your/mailarchiver"   # Mail Archiver installation directory
BACKUP_BASE_DIR="/path/to/your/backups"         # Where to store backups

# Optional settings (with defaults):
RETENTION_DAYS=30                               # How many days to keep backups
LOG_FILE="/var/log/mailarchiver-backup.log"     # Log file location
MIN_DISK_SPACE_MB=1000                          # Minimum free disk space required
LOCK_FILE="/var/lock/mailarchiver-backup.lock"  # Lock file to prevent concurrent runs
```

**Installation :**

1. **Copiez le script à l'emplacement souhaité :**
   ```bash
   cp backup.sh /usr/local/bin/mailarchiver-backup.sh
   ```

2. **Modifiez la configuration :**
   ```bash
   nano /usr/local/bin/mailarchiver-backup.sh
   # Ajustez les paramètres
   ```

3. **Rendez le script exécutable :**
   ```bash
   chmod +x /usr/local/bin/mailarchiver-backup.sh
   ```

4. **Testez le script manuellement :**
   ```bash
   /usr/local/bin/mailarchiver-backup.sh
   ```

5. **Vérifiez la sauvegarde :**
   - Vérifiez le répertoire de sauvegarde pour le fichier `.tar.gz` créé
   - Consultez le fichier journal pour toute erreur ou avertissement

**Planification des Sauvegardes Automatisées :**

Pour planifier des sauvegardes automatiques à l'aide de cron :

```bash
# Edit crontab
crontab -e

# Add a line to run backup daily at 2 AM
0 2 * * * /usr/local/bin/mailarchiver-backup.sh

# Or weekly on Sunday at 3 AM
0 3 * * 0 /usr/local/bin/mailarchiver-backup.sh

# Or multiple times (e.g., daily at 2 AM and 2 PM)
0 2,14 * * * /usr/local/bin/mailarchiver-backup.sh
```

## 🔙 Procédures de Restauration {#restore-procedures}

Les procédures de restauration sont conçues pour correspondre aux méthodes de sauvegarde. Choisissez la méthode de restauration appropriée en fonction de la méthode de sauvegarde que vous avez utilisée :

- **Méthode 1 (Sauvegarde Complète)** : Utilisez la procédure "Full Restore from Complete Backup". Cette méthode est utilisée lorsque vous avez une sauvegarde complète qui inclut le répertoire `postgres-data`.
- **Méthode 2 (Sauvegarde à Chaud)** ou **Méthode 3 (Script de Sauvegarde Automatisé)** : Utilisez la procédure "Restore from SQL Dump" (gère les clés de protection des données et la base de données). Cette méthode est utilisée lorsque vous avez une sauvegarde à chaud qui inclut le dump SQL de la base de données et les clés de protection des données.

Pour plus de commodité, nous avons également fourni un script de restauration automatisé qui peut gérer toutes les procédures de restauration automatiquement. Vous pouvez utiliser le script au lieu de suivre manuellement les étapes ci-dessous.

### Script de Restauration Automatisé {#automated-restore-script}

Le script de restauration est disponible dans le fichier [restore.sh](attachments/restore.sh). Il fournira une restauration guidée et demandera la méthode de sauvegarde, le chemin vers le fichier tar de sauvegarde et le répertoire d'installation, puis effectuera le processus de restauration automatiquement.

```bash
# Make the script executable
chmod +x restore.sh

# Run the script
./restore.sh
```

Le script vous guidera à travers le processus de restauration et gérera automatiquement toutes les étapes en fonction de la méthode de sauvegarde que vous avez utilisée.

### Restauration Complète depuis une Sauvegarde Complète (Méthode Manuelle) {#full-restore-from-complete-backup-manual-method}

Ceci restaure toutes les données à partir d'une archive de sauvegarde complète. Cette méthode doit être utilisée lorsque vous avez une sauvegarde complète qui inclut le répertoire `postgres-data` (créé par la méthode un).

```bash
# 1. Stop the containers
docker compose down

# 2. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 3. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 4. Remove current data (make sure to backup first if needed!)
rm -rf postgres-data
rm -rf data-protection-keys

# 5. Restore data volumes
cp -r "$BACKUP_PATH/postgres-data" ./
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Start the containers
docker compose up -d

echo "Restore completed from: $BACKUP_FILE"
```

### Restauration depuis un Dump SQL (Méthode Manuelle) {#restore-from-sql-dump-manual-method}

Ceci restaure la base de données et les clés de protection des données à partir d'une sauvegarde à chaud (Méthodes deux et trois).

**Note :** Les commandes ci-dessous utilisent des identifiants d'exemple (`mailuser` et `MailArchiver`). Vous devez les remplacer par les valeurs réelles de votre fichier `docker-compose.yml`. Vérifiez les variables d'environnement `POSTGRES_USER` et `POSTGRES_DB` sous le service `postgres`.

```bash
# 1. Extract the backup archive
BACKUP_FILE="backups/mailarchiver-backup-YYYYMMDD-HHMMSS.tar.gz"
tar -xzf "$BACKUP_FILE" -C backups/

# 2. Get the extracted directory name
BACKUP_DIR=$(tar -tzf "$BACKUP_FILE" | head -1 | cut -f1 -d"/")
BACKUP_PATH="backups/$BACKUP_DIR"

# 3. Stop the containers
docker compose down

# 4. Backup current data protection keys (optional, for safety)
# cp -r data-protection-keys data-protection-keys.backup

# 5. Restore data protection keys
rm -rf data-protection-keys
cp -r "$BACKUP_PATH/data-protection-keys" ./

# 6. Restore docker-compose configuration (optional, review first!)
# cp "$BACKUP_PATH/docker-compose.yml" ./

# 7. Extract database credentials from docker-compose.yml
# Check your docker-compose.yml for the actual values and replace in the commands below:
# - POSTGRES_USER (default: mailuser)
# - POSTGRES_DB (default: MailArchiver)
DB_USER="mailuser"  # Replace with your POSTGRES_USER
DB_NAME="MailArchiver"  # Replace with your POSTGRES_DB

# 8. Start the database container
docker compose up -d postgres

# 9. Wait for database to be ready
echo "Waiting for database to be ready..."
sleep 10

# 10. Verify database is ready (adjust -U and -d parameters if needed)
docker compose exec -T postgres pg_isready -U "$DB_USER" -d "$DB_NAME"

# 11. Drop and recreate the database (WARNING: This deletes all current data!)
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "DROP DATABASE IF EXISTS \"$DB_NAME\";"
docker compose exec -T postgres psql -U "$DB_USER" -d postgres -c "CREATE DATABASE \"$DB_NAME\";"

# 12. Restore the database from SQL dump
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" < "$BACKUP_PATH/database.sql"

# 13. Start all containers
docker compose up -d

# 14. Clean up extracted backup
rm -rf "$BACKUP_PATH"

echo "Restore completed from: $BACKUP_FILE"
```


## ⚠️ Notes Importantes {#important-notes}

### Paramètres de Connexion à la Base de Données {#database-connection-settings}
Assurez-vous que vos paramètres de connexion à la base de données dans `docker-compose.yml` correspondent entre la sauvegarde et la restauration :
- `POSTGRES_USER` (par défaut : mailuser)
- `POSTGRES_PASSWORD` (par défaut : masterkey)
- `POSTGRES_DB` (par défaut : MailArchiver)

### Clés de Protection des Données {#data-protection-keys}
Le répertoire `data-protection-keys` est utilisé pour :
- L'authentification des utilisateurs et la gestion des sessions
- Le chiffrement des cookies
- La manipulation sécurisée des données

Sans ces clés, les sessions utilisateur existantes seront invalidées et les utilisateurs devront se reconnecter.

### Variables d'Environnement {#environment-variables}
Tous les paramètres de l'application sont stockés dans des variables d'environnement dans `docker-compose.yml`. Assurez-vous de :
- Sauvegarder votre fichier `docker-compose.yml`
- Documenter tout paramètre personnalisé
- Utiliser la même configuration lors de la restauration

## 🔒 Bonnes Pratiques de Sécurité {#security-best-practices}

1. **Chiffrer les Sauvegardes** : Envisagez de chiffrer les archives de sauvegarde, surtout si elles contiennent des données d'e-mails sensibles.

2. **Stockage Sécurisé** : Stockez les sauvegardes dans un emplacement sécurisé :
   - Utilisez un serveur ou un système de stockage séparé
   - Implémentez des contrôles d'accès
   - Envisagez un stockage hors site ou dans le cloud

3. **Tests Réguliers** : Testez régulièrement vos procédures de sauvegarde et de restauration pour vous assurer qu'elles fonctionnent correctement.

4. **Surveiller la Taille des Sauvegardes** : Gardez un œil sur la taille des sauvegardes pour vous assurer que vous disposez d'un espace de stockage adéquat.

5. **Conservation des Sauvegardes** : Définissez et implémentez une politique de Conservation des sauvegardes (par exemple, conserver les sauvegardes quotidiennes pendant 7 jours, les sauvegardes hebdomadaires pendant 4 semaines, les sauvegardes mensuelles pendant 12 mois).

6. **Changer les Mots de Passe par Défaut** : Les mots de passe par défaut dans la configuration ne sont pas sécurisés et doivent être modifiés pour une utilisation en production.


## 🖥️ Sauvegarde Proxmox {#proxmox-backup}

Lorsque vous exécutez l'environnement Mail Archiver Docker dans un conteneur Proxmox LXC, il est recommandé d'utiliser le mode "stop" pour sauvegarder l'ensemble du conteneur afin de garantir la cohérence des données.

### Pourquoi le Mode Stop est Recommandé {#why-stop-mode-is-recommended}

1. **Cohérence des Données** : L'arrêt du conteneur garantit que toutes les transactions de la base de données sont terminées et que les données sont écrites sur le disque avant que la sauvegarde ne soit effectuée.
2. **Prévenir la Corruption des Données** : Effectuer des sauvegardes pendant que le conteneur est actif peut entraîner une corruption des données, en particulier avec les opérations de base de données.
3. **Intégrité du Système de Fichiers** : L'arrêt du conteneur garantit que toutes les opérations du système de fichiers sont terminées, empêchant les écritures partielles pendant la sauvegarde.

### Notes sur la Sauvegarde Proxmox {#proxmox-backup-notes}

- Assurez-vous toujours d'avoir suffisamment d'espace de stockage pour les sauvegardes.
- Testez régulièrement votre procédure de restauration pour vous assurer que les sauvegardes sont valides.
- Envisagez de configurer un calendrier de sauvegarde dans l'interface web de Proxmox pour des sauvegardes automatisées.
- Surveillez les journaux de sauvegarde pour détecter toute erreur ou problème.
