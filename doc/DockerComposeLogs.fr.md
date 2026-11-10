# 🐳 Docker Compose Logs Guide {#docker-compose-logs-guide}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide explique comment consulter et configurer les logs lors de l'exécution de l'application Mail Archiver à l'aide de Docker Compose. Une gestion appropriée des logs est essentielle pour surveiller la santé de l'application, dépanner les problèmes et comprendre le comportement du système.

## 📖 Consultation des logs Docker Compose {#viewing-docker-compose-logs}

### Commandes de logs de base {#basic-log-commands}

Pour consulter les logs de tous les services de votre configuration Docker Compose :

```bash
docker compose logs
```

Pour consulter les logs d'un service spécifique (par exemple, l'application Mail Archiver) :

```bash
docker compose logs mailarchive-app
```

### Streaming de logs en temps réel {#real-time-log-streaming}

Pour suivre les logs en temps réel (similaire à `tail -f`) :

```bash
docker compose logs -f
```

Pour suivre les logs d'un service spécifique :

```bash
docker compose logs -f mailarchive-app
```

### Filtrage des logs et options {#log-filtering-and-options}

Afficher les N dernières lignes de logs :
```bash
docker compose logs --tail=100
```

Afficher les logs depuis une heure spécifique :
```bash
docker compose logs --since=1h
```

Afficher les logs jusqu'à une heure spécifique :
```bash
docker compose logs --until=2025-01-01T12:00:00
```

Afficher les horodatages dans les logs :
```bash
docker compose logs -t
```

## 📝 Configuration du niveau de log {#log-level-configuration}

L'application Mail Archiver prend en charge différents niveaux de log qui contrôlent la verbosité de la sortie. Vous pouvez configurer ces niveaux en utilisant des variables d'environnement dans votre fichier `docker-compose.yml`.

### Niveaux de log disponibles {#available-log-levels}

Les niveaux de log suivants sont disponibles, classés du plus verbeux au moins verbeux :

- **`Trace`** - Journalisation la plus détaillée, incluant les messages internes du framework
- **`Debug`** - Informations de débogage détaillées pour le développement et le dépannage
- **`Information`** - Informations générales sur le flux de l'application (niveau par défaut)
- **`Warning`** - Messages d'avertissement concernant des problèmes potentiels
- **`Error`** - Messages d'erreur concernant des opérations ayant échoué
- **`Critical`** - Messages d'erreur critiques nécessitant une attention immédiate
- **`None`** - Aucune sortie de journalisation

### Configuration par défaut des logs {#default-log-configuration}

Par défaut, l'application utilise la configuration de niveau de log suivante :
- **Par défaut** : `Information`
- **Microsoft.AspNetCore** : `Warning` (réduit le bruit du framework)
- **Microsoft.EntityFrameworkCore.Database.Command** : `Warning` (réduit le bruit des commandes de base de données)

### Configurer les niveaux de log dans Docker Compose {#configuring-log-levels-in-docker-compose}

Pour personnaliser les niveaux de log, ajoutez les variables d'environnement suivantes à votre fichier `docker-compose.yml` sous le service `mailarchive-app` :

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Logging Settings (Optional)
      - Logging__LogLevel__Default=Information
      - Logging__LogLevel__Microsoft_AspNetCore=Warning
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Warning
```

### Exemple : Configuration de débogage {#example-debug-configuration}

À des fins de dépannage, vous pourriez vouloir activer une journalisation plus verbeuse :

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Verbose Logging for Debugging
      - Logging__LogLevel__Default=Debug
      - Logging__LogLevel__Microsoft_AspNetCore=Information
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Debug
```

### Exemple : Configuration de production {#example-production-configuration}

Pour les environnements de production, vous pourriez vouloir réduire la verbosité des logs afin d'améliorer les performances et de réduire le volume de logs :

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Other environment variables...
      
      # Production Logging (Reduced Verbosity)
      - Logging__LogLevel__Default=Warning
      - Logging__LogLevel__Microsoft_AspNetCore=Error
      - Logging__LogLevel__Microsoft_EntityFrameworkCore_Database_Command=Error
```

## 🔍 Comprendre la sortie des logs {#understanding-log-output}

### Format des logs {#log-format}

Les logs suivent ce format général :
```
timestamp [log-level] category - message
```

Exemple :
```
2025-01-15T10:30:45.123Z [Information] MailArchiver.Services.EmailService - Starting email synchronization for account user@example.com
```

### Catégories de logs communes {#common-log-categories}

- **`MailArchiver.Services.*`** - Logs du service d'application
- **`MailArchiver.Controllers.*`** - Logs du contrôleur Web API
- **`Microsoft.AspNetCore`** - Logs du framework ASP.NET Core
- **`Microsoft.EntityFrameworkCore`** - Logs d'Entity Framework
- **`Microsoft.Extensions.Hosting`** - Logs liés à l'hébergement

## 🛠️ Conseils de dépannage {#troubleshooting-tips}

### 1. Trouver des messages d'erreur {#1-finding-error-messages}

Pour trouver rapidement des messages d'erreur dans les logs :
```bash
docker compose logs mailarchive-app | grep -i error
```

### 2. Surveiller des opérations spécifiques {#2-monitoring-specific-operations}

Pour surveiller la synchronisation des e-mails :
```bash
docker compose logs -f mailarchive-app | grep -i sync
```

### 3. Rechercher des avertissements {#3-checking-for-warnings}

Pour rechercher des messages d'avertissement :
```bash
docker compose logs mailarchive-app | grep -i warning
```

## 📊 Bonnes pratiques de gestion des logs {#log-management-best-practices}

### Pour le développement {#for-development}
- Utilisez le niveau `Debug` ou `Information` pour un dépannage détaillé
- Activez le streaming de logs en temps réel pendant le développement
- Surveillez les logs en continu lors du test de nouvelles fonctionnalités

### Pour la production {#for-production}
- Utilisez le niveau `Warning` ou `Error` pour réduire le volume de logs
- Implémentez la rotation des logs pour éviter les problèmes d'espace disque
- Envisagez des solutions de journalisation centralisée pour les configurations multi-conteneurs
- Examinez régulièrement les logs pour détecter les événements de sécurité et les problèmes de performance

## 📚 Documentation associée {#related-documentation}

- [Guide d'installation et de configuration](Setup.md) - Instructions de configuration complètes
- [Journalisation des accès](Logs.md) - Journalisation des accès au niveau de l'application
- [Guide de Sauvegarde et Restauration](BackupRestore.md) - Procédures de protection des données
