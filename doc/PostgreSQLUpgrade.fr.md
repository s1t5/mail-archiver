# 🔄 Guide de mise à niveau majeure de PostgreSQL {#postgresql-major-version-upgrade-guide}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide explique comment mettre à niveau la base de données PostgreSQL d'une version majeure à une autre dans votre environnement Mail Archiver Docker Compose. Les mises à niveau de versions majeures sont nécessaires pour profiter des nouvelles fonctionnalités de PostgreSQL, des améliorations de performance et des mises à jour de sécurité.

## ⚠️ Clause de non-responsabilité importante {#important-disclaimer}
> **Note** : Cette procédure de mise à niveau est un processus de migration de base de données PostgreSQL général et n'est pas spécifique à Mail Archiver. Elle est fournie à titre de recommandation basée sur les meilleures pratiques de PostgreSQL. Testez toujours la procédure dans un environnement hors production au préalable et assurez-vous d'avoir des Sauvegardes appropriées avant de procéder.


**Toutes les procédures décrites dans ce guide doivent d'abord être testées dans un environnement hors production.** Les mises à niveau de versions majeures impliquent une migration de données et comportent des risques de perte ou de corruption de données. Faites toujours :
- Créer une Sauvegarde complète avant de commencer le processus de mise à niveau
- Tester la procédure de mise à niveau dans un environnement de développement
- Vérifier toutes les fonctionnalités de l'application après la mise à niveau
- Prévoir un plan de retour en arrière (rollback) en cas de problème
- Planifier les mises à niveau pendant les fenêtres de maintenance

**Utilisez à vos propres risques.**

## 🎯 Quand effectuer la mise à niveau {#when-to-upgrade}

Envisagez de mettre à niveau PostgreSQL lorsque :
- Votre version actuelle approche de la fin de vie
- Les mises à jour de sécurité nécessitent une version plus récente
- Vous planifiez une fenêtre de maintenance majeure du système

## 🛠️ Prérequis {#prerequisites}

- Installation Mail Archiver Docker en cours d'exécution (voir [Guide d'installation](Setup.md))
- Espace disque suffisant (au moins 1,5x la taille actuelle de la base de données)
- Accès au système hôte Docker
- Sauvegarde complète de la base de données actuelle (voir [Guide de Sauvegarde et Restauration](BackupRestore.md))
- Fenêtre de maintenance pour l'indisponibilité de l'application

## 📋 Configuration actuelle {#current-configuration}

Votre configuration Mail Archiver actuelle utilise :
- **Nom de la base de données** : MailArchiver
- **Utilisateur de la base de données** : mailuser
- **Volume de données** : `./postgres-data` mappé sur `/var/lib/postgresql/data`

Si nécessaire, cela doit être ajusté dans les exemples de commandes et scripts suivants.

## 🔄 Procédures de mise à niveau {#upgrade-procedures}

Cette méthode crée une sauvegarde logique de vos données et la restaure sur la nouvelle version de PostgreSQL. C'est l'approche la plus sûre et elle fonctionne avec toutes les versions de PostgreSQL.

> **Méthodes alternatives** : PostgreSQL propose également d'autres méthodes de mise à niveau telles que `pg_upgrade` pour des mises à niveau plus rapides. Pour plus d'informations sur les approches de mise à niveau alternatives, veuillez vous référer à la [documentation officielle de mise à niveau de PostgreSQL](https://www.postgresql.org/docs/current/upgrading.html).

#### Étape 1 : Préparer l'environnement {#step-1-prepare-the-environment}

```bash
# 1. Stop the application container (keep database running for backup)
docker compose stop mailarchive-app

# 2. Create a backup directory
mkdir -p postgres-backup

# 3. Create a database dump
echo "Export DB content (this can take some time)"
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > postgres-backup/database_dump.sql

# 4. Verify the dump was created successfully
ls -la postgres-backup/database_dump.sql

# 5. Stop the database container
docker compose down
```

#### Étape 2 : Mettre à jour la configuration Docker Compose {#step-2-update-docker-compose-configuration}

Modifiez votre fichier `docker-compose.yml` pour utiliser la nouvelle version de PostgreSQL :

```yaml
  postgres:
    image: postgres:18  # Changed from old version
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data-new:/var/lib/postgresql  # New data directory
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
```

**Changements importants :**
- Mise à jour de `image` de `postgres:17` à `postgres:18` (ou votre version cible)
- Changement du mappage de volume de `./postgres-data:/var/lib/postgresql/data` à `./postgres-data-new:/var/lib/postgresql` (le changement du chemin interne est requis par la version 18 et les suivantes de postgres)

#### Étape 3 : Démarrer la nouvelle instance PostgreSQL {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### Étape 4 : Restaurer les données dans la nouvelle base de données {#step-4-restore-data-to-new-database}

Restaurer le dump de la base de données vers la nouvelle instance PostgreSQL :

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
Cela peut prendre un certain temps selon la taille de la base de données
Surveillez tout message d'erreur pendant la Restauration

#### Étape 5 : Tester et terminer la migration {#step-5-test-and-complete-migration}
Démarrez l'application pour tester la connectivité :

```bash
docker compose up -d
```

Testez les fonctionnalités de l'application :
- Connectez-vous à l'interface web
- Vérifiez que la recherche d'e-mails fonctionne
- Vérifiez que les données existantes sont accessibles
- Testez les fonctionnalités de Sauvegarde/Restauration

Si tout fonctionne correctement, nettoyez les anciennes données :

Arrêtez les conteneurs
```bash
docker compose down
```
Supprimez l'ancien répertoire de données (uniquement après avoir confirmé que la nouvelle configuration fonctionne)
```bash
rm -rf postgres-data
```
Supprimez le répertoire de Sauvegarde
```bash
rm -rf postgres-backup
```

Renommez le nouveau répertoire de données avec le nom standard
```bash
mv postgres-data-new postgres-data
```

Mettez à jour docker-compose.yml pour utiliser le nom de volume standard :
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Démarrez le système complet
```bash
docker compose up -d
```

## ⚠️ Considérations importantes {#important-considerations}

### Planification de l'indisponibilité {#downtime-planning}

- Selon votre système et la taille de la base de données, cette procédure peut prendre beaucoup de temps.
- Planifiez l'opération pendant les fenêtres de maintenance

### Exigences d'espace disque {#disk-space-requirements}

Assurez-vous d'avoir un espace disque adéquat (environ deux fois la taille de votre base de données)

Vérifiez l'espace disponible :
```bash
df -h
du -sh postgres-data/
```

### Plan de retour en arrière (Rollback) {#rollback-plan}

Préparez toujours un retour en arrière :
1. Conservez l'ancien répertoire de données jusqu'à ce que la mise à niveau soit vérifiée
2. Maintenez l'ancienne Sauvegarde `docker-compose.yml`
3. Testez la procédure de retour en arrière dans un environnement de développement d'abord

### Vérification post-mise à niveau {#post-upgrade-verification}

Après la mise à niveau, vérifiez :
- La connectivité de l'application à la base de données
- Les fonctionnalités de recherche et de récupération d'e-mails
- L'authentification des utilisateurs et la gestion des sessions
- Les fonctionnalités de Sauvegarde et Restauration
- Les métriques de performance de la base de données

## 📚 Documentation connexe {#related-documentation}

- [Guide de Sauvegarde et Restauration](BackupRestore.md) - Essentiel pour les Sauvegardes pré-mise à niveau
- [Guide de maintenance de la base de données](DatabaseMaintenance.md) - Maintenance post-mise à niveau
- [Guide d'installation](Setup.md) - Configuration Docker Compose
- [Notes de version PostgreSQL](https://www.postgresql.org/docs/release/) - Documentation officielle des versions
