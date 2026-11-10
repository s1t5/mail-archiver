# 🖥️ CLI Commands {#cli-commands}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Certaines tâches n'ont pas leur place dans l'interface Web : l'importation d'une Archive existante depuis le système de fichiers local, ou le déplacement de messages originaux stockés entre les backends de stockage. Mail Archiver accepte les deux comme commandes en ligne de commande.

| Commande | Objectif |
|----------|---------|
| `--import-mbox` | Importer un fichier MBox depuis un répertoire monté |
| `--import-eml` | Importer une Archive ZIP de fichiers EML depuis un répertoire monté |
| `--migrate-raw-storage` | Déplacer les messages originaux stockés entre la base de données et le système de fichiers |

Toutes les commandes sont traitées avant le démarrage du pipeline Web et s'arrêtent lorsqu'elles ont terminé, elles peuvent donc être exécutées en toute sécurité avec `docker compose exec` pendant que l'Archive est en cours d'exécution.

## ⚠️ Modèle de sécurité {#security-model}

Les commandes d'importation sont exclusivement disponibles via **CLI** – il n'existe aucun point de terminaison web pour parcourir ou importer des fichiers locaux. Vous devez avoir un accès à l'hôte Docker (`docker exec`) pour les exécuter.

Une **liste blanche de chemins** (`LocalImport:AllowedPaths`) contrôle les répertoires que l'importateur peut lire. Les fichiers situés en dehors de ces chemins sont rejetés.

`--migrate-raw-storage` n'est pas soumis à cette liste blanche : il lit uniquement les fichiers de messages que l'Archive elle-même a écrits sous `RawMessages:FileSystem:BasePath`.

## 🛠️ Configuration {#setup}

### 1. Configurer les chemins autorisés {#1-configure-allowed-paths}

Dans votre `docker-compose.yml`, définissez les chemins d'importation autorisés :

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. Monter vos fichiers {#2-mount-your-files}

Montez le répertoire contenant vos fichiers MBox/EML dans le conteneur :

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

Remplacez `/home/user/mbox-archives` par le chemin réel sur votre hôte Docker.

### 3. Redémarrer le conteneur {#3-restart-the-container}

```bash
docker compose down && docker compose up -d
```

## 📝 Utilisation {#usage}

### Trouver l'ID de votre Compte cible {#find-your-target-account-id}

Avant l'importation, vous avez besoin de l'ID de la base de données du Compte de messagerie cible. Vous pouvez le trouver via l'interface Web :

Naviguez vers "Email Accounts", ouvrez les détails du compte de destination, l'ID du compte est affiché dans la liste des comptes ou dans l'URL lors de la consultation d'un compte.

### Importer un fichier MBox {#import-an-mbox-file}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**Paramètres :**
| Paramètre | Requis | Description |
|-----------|----------|-------------|
| `--import-mbox` | Oui | Signale une opération d'importation MBox |
| `--file <path>` | Oui | Chemin vers le fichier MBox à l'intérieur du conteneur |
| `--account-id <id>` | Oui | ID de la base de données du Compte de messagerie cible |
| `--folder <name>` | Non | Nom du Dossier cible (par défaut : `INBOX`) |

### Importer des fichiers EML (Archive ZIP) {#import-eml-files-zip-archive}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**Paramètres :**
| Paramètre | Requis | Description |
|-----------|----------|-------------|
| `--import-eml` | Oui | Signale une opération d'importation EML |
| `--file <path>` | Oui | Chemin vers une **Archive ZIP** contenant des fichiers `.eml` à l'intérieur du conteneur |
| `--account-id <id>` | Oui | ID de la base de données du Compte de messagerie cible |

> **Important :** `--import-eml` attend une **Archive ZIP** contenant des fichiers `.eml` — elle n'accepte **pas** les fichiers `.eml` individuels. Pour importer des fichiers EML seuls, compressez-les d'abord dans une Archive ZIP :
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **Note pour `dotnet run` :** Lors de l'utilisation de `dotnet run` (au lieu de la DLL publiée), séparez les arguments de dotnet des arguments de l'application avec `--` :
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> Le `--` indique à `dotnet run` que tous les arguments suivants appartiennent à votre application, et non au compilateur .NET.

## 📊 Exemple de sortie {#example-output}

```
Target account: user@example.com (ID: 1)

=== Local MBox Import ===
File: /data/import/myarchive.mbox
Size: 245.32 MB
Target Account ID: 1
Target Folder: INBOX

[Importing... progress logged to container logs]

=== Import Results ===
Status: Completed
Total Emails: 15234
Imported Successfully: 15120
Failed: 42
Skipped (malformed): 28
Skipped (duplicates): 44
Duration: 00:12:45
```

## 🔍 Surveillance {#monitoring}

Pendant l'importation, la progression détaillée est écrite dans les logs du conteneur :

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ Référence de configuration {#configuration-reference}

| Variable d'environnement | Valeur par défaut | Description |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | Premier chemin autorisé pour les importations locales |
| `LocalImport__AllowedPaths__1` | (vide) | Deuxième chemin autorisé (optionnel) |

Ajoutez d'autres chemins en incrémentant l'index (`__2`, `__3`, etc.).

## 🚫 Gestion des erreurs {#error-handling}

| Erreur | Cause | Solution |
|-------|-------|----------|
| `File not found` | Le chemin `--file` n'existe pas dans le conteneur | Vérifiez le chemin du fichier et le montage du volume |
| `File path is not in an allowed import directory` | Le fichier est en dehors des chemins autorisés | Déplacez le fichier vers un chemin autorisé ou ajoutez son répertoire à `LocalImport:AllowedPaths` |
| `Mail account with ID X not found` | ID de Compte incorrect | Vérifiez l'ID du Compte via l'interface Web ou une requête de base de données |
| `Invalid account-id` | Valeur non numérique pour `--account-id` | Utilisez un ID de base de données numérique |

## 🔒 Bonnes pratiques de sécurité {#security-best-practices}

1. **Utilisez des montages en lecture seule** lorsque cela est possible :
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **Restreignez AllowedPaths** aux seuls répertoires que vous utilisez réellement.

3. **Supprimez les montages de volume** une fois l'importation terminée afin de maintenir une surface de sécurité propre.

4. **N'exposez jamais** le répertoire d'importation via le serveur web – il n'existe aucun point de terminaison web pour les importations locales.

## 📦 Aperçu des formats d'importation {#import-formats-at-a-glance}

| CLI Flag | Format d'entrée | Extension de fichier typique | Email unique | En masse |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | Fichier MBox | `.mbox`, `.mbx` | ❌ (toujours en masse) | ✅ |
| `--import-eml` | Archive ZIP d'EMLs | `.zip` | ❌ (ZIP requis) | ✅ |
| `--migrate-raw-storage` | Messages originaux stockés | — | — | ✅ |

*Les fichiers `.eml` uniques ne sont pas directement pris en charge. Utilisez `zip` pour les encapsuler, ou importez via l'[upload web](Import-EML-MBox.md).*

## 🔧 Commandes de maintenance {#maintenance-commands}

### Déplacement des messages originaux stockés {#moving-stored-original-messages}

Lorsque `RawMessages:Enabled` est activé, le message original de chaque e-mail est conservé soit dans la base de données, soit dans des fichiers, selon `RawMessages:Storage`. Cette commande déplace les originaux déjà stockés d'un backend vers un autre ; voir [Original Message Storage](RawMessages.md#storage-backend) pour la configuration.

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

| Argument | Valeur par défaut | Description |
|----------|---------|-------------|
| `--from` | `RawMessages:Storage` configuré | Backend duquel les messages sont déplacés. |
| `--to` | `RawMessages:Storage` configuré | Backend dans lequel les messages sont déplacés. |
| `--batch` | `200` | Messages par transaction de base de données. |

Contrairement aux commandes d'importation, celle-ci n'est pas limitée par `LocalImport:AllowedPaths` : elle ne lit que les fichiers de messages que l'Archive elle-même a écrits sous `RawMessages:FileSystem:BasePath`.

**Ce qu'elle garantit :**

- Chaque message est vérifié par rapport à la somme de contrôle (checksum) enregistrée lors de l'archivage avant d'être copié. Un message qui échoue est laissé exactement là où il se trouve et est signalé — un original corrompu n'est jamais propagé et sa seule copie restante n'est jamais supprimée.
- Un fichier est supprimé uniquement après que la transaction de la base de données qui l'a déplacé a été validée, ainsi une exécution interrompue ne peut pas perdre l'unique copie.
- L'exécution est reprise possible : le travail est sélectionné par le stockage enregistré sur chaque message, ainsi relancer la commande permet de continuer avec ce qui est encore en attente.
- Un message défectueux n'interrompt pas l'exécution. Le code de sortie est `1` si un message a échoué, `0` sinon.

**Exemple de sortie :**

```text
Batch: 200 moved, 0 skipped, 0 failed
Batch: 200 moved, 0 skipped, 0 failed
Batch: 42 moved, 0 skipped, 1 failed
Done: 442 moved, 1 failed.
  Email 12345: checksum mismatch (recorded 4f1c..., file has 9ab2...)
```

Les lectures suivent toujours le backend enregistré sur chaque message, et non le paramètre configuré, ainsi un téléchargement continue de fonctionner pendant et après une migration. Modifier `RawMessages:Storage` n'affecte que les nouveaux e-mails archivés — lancez cette commande pour déplacer ce qui est déjà stocké.

> **Sauvegarde :** avec le backend du système de fichiers, les fichiers de messages résident en dehors de la base de données. Effectuez une Sauvegarde de `RawMessages:FileSystem:BasePath` en même temps que le dump de la base de données ; une Restauration sans les fichiers laisse des originaux qui ne peuvent pas être servis.

## ❓ FAQ {#faq}

**Q: Pourquoi `--import-eml` nécessite-t-il un fichier ZIP ?**
R: Le service d'importation EML sous-jacent (`EmlImportService`) traite les Archive ZIP en interne en utilisant l'API `ZipFile` de .NET. Cette conception permet de gérer efficacement les importations groupées. Pour les fichiers `.eml` individuels, placez-les dans une Archive ZIP (par ex. `zip emails.zip *.eml`) ou utilisez le [web upload](Import-EML-MBox.md).

**Q: Pourquoi ne puis-je pas parcourir les fichiers depuis l'interface Web ?**
R: Il s'agit d'une conception de sécurité délibérée. Cette fonction est intentionnellement implémentée via CLI pour les interactions au niveau du système car les utilisateurs normaux n'ont pas d'accès direct au niveau de l'OS ; ils interagissent uniquement avec l'interface Web.

Pour prévenir les risques de sécurité tels que les attaques par traversée de chemin et l'exposition non autorisée du système de fichiers, les utilisateurs réguliers sont limités à l'utilisation de l'interface Web pour les téléchargements et les importations de fichiers. Cela garantit qu'ils ne peuvent pas contourner l'interface prévue pour accéder au système de fichiers du conteneur sous-jacent.
