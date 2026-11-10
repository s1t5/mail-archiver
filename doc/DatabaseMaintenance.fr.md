# 🔧 Guide de maintenance de la base de données {#database-maintenance-guide}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide explique comment configurer et utiliser la fonctionnalité de maintenance automatique de la base de données dans Mail Archiver. L'application inclut un service d'arrière-plan intégré qui effectue automatiquement des opérations de maintenance de la base de données PostgreSQL afin de maintenir des performances optimales et d'éviter l'encombrement de la base de données.

## ⚠️ Avis important {#important-notice}

**Les bases de données PostgreSQL ne rétrécissent pas automatiquement lorsque des données sont supprimées.** Lorsque vous supprimez des e-mails ou des Boîtes aux lettres, la base de données marque l'espace comme disponible pour une réutilisation, mais la taille réelle du fichier de la base de données reste la même. Ce phénomène est appelé "database bloat" (encombrement de la base de données).

Mail Archiver inclut un service de maintenance automatique qui exécute quotidiennement des opérations VACUUM ANALYZE pour :
- Récupérer de l'espace de stockage pour une réutilisation au sein de la base de données
- Mettre à jour les statistiques de requête pour des performances optimales
- Prévenir la dégradation des performances au fil du temps
- Maintenir la santé de la base de données

## 🚀 Maintenance automatique (Recommandé) {#automatic-maintenance-recommended}

### Configuration {#configuration}

Activez la maintenance automatique de la base de données dans votre `appsettings.json` ou via des variables d'environnement dans `docker-compose.yml` :

#### Option 1 : Via appsettings.json {#option-1-via-appsettingsjson}

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### Option 2 : Via docker-compose.yml (Recommandé) {#option-2-via-docker-composeyml-recommended}

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Enable automatic database maintenance
      - DatabaseMaintenance__Enabled=true
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30
```

### Options de configuration {#configuration-options}

| Paramètre | Type | Par défaut | Description |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Activer ou désactiver la maintenance automatique |
| `DailyExecutionTime` | string | `"02:00"` | Heure de la journée pour l'exécution de la maintenance (format HH:mm, 24 heures) |
| `TimeoutMinutes` | integer | `30` | Temps maximum autorisé pour les opérations de maintenance en minutes |

### Comment cela fonctionne {#how-it-works}

Lorsqu'elle est activée, le service de maintenance :

1. **Démarre automatiquement** avec l'application
2. **Attend** jusqu'à l'heure d'exécution configurée
3. **Exécute VACUUM ANALYZE** sur l'ensemble de la base de données
4. **Enregistre l'opération** dans le Journal d'audit de l'application avec la durée
5. **Reprogramme** pour le lendemain

L'opération de maintenance :
- ✅ S'exécute sans interruption de service
- ✅ N'interfère pas avec les opérations normales de l'application
- ✅ Fournit une journalisation et une surveillance complètes
- ✅ Récupère l'espace pour une réutilisation au sein de la base de données
- ✅ Met à jour les statistiques de requête pour de meilleures performances

### Choisir l'heure d'exécution {#choosing-the-execution-time}

Sélectionnez un moment où votre système a l'activité la plus faible :
- **Recommandé** : Entre 02:00 et 04:00 (heures nocturnes)
- **À considérer** : Votre paramètre de fuseau horaire
- **À éviter** : Les heures de pointe ou les fenêtres de Sauvegarde

Exemples de configurations :

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### Surveiller la maintenance automatique {#monitoring-automatic-maintenance}

#### 1. Journaux de l'application {#1-application-logs}

Consultez les journaux de l'application pour les activités de maintenance :

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. Journal d'audit (UI) {#2-access-log-ui}

Toutes les opérations de maintenance sont enregistrées dans le Journal d'audit de l'application :

1. Naviguez vers **Logs** → **Access Log** dans l'interface utilisateur
2. Filtrez par type : **Database Maintenance**
3. Consultez les heures d'exécution, la durée et les éventuelles erreurs

## 🔧 Maintenance manuelle (Avancé) {#manual-maintenance-advanced}

### VACUUM FULL pour la récupération d'espace disque {#vacuum-full-for-disk-space-recovery}

**⚠️ Nécessite une interruption de l'application**

VACUUM FULL compacte la base de données et restitue l'espace disque au système d'exploitation, mais nécessite des verrous exclusifs.

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**Quand l'utiliser :**
- Après la suppression de grosses Boîtes aux lettres
- Pour récupérer un espace disque significatif
- Pendant les fenêtres de maintenance planifiées

**Important :**
- Nécessite un espace disque libre égal à la taille de la table
- Peut prendre des heures sur de grandes bases de données
- Bloque toutes les opérations de la base de données
- Doit être planifié pendant les heures creuses

Pour des procédures manuelles détaillées, consultez la [Documentation PostgreSQL VACUUM](https://www.postgresql.org/docs/current/sql-vacuum.html).

## 📚 Documentation associée {#related-documentation}

- [Guide d'installation](Setup.md) - Configuration initiale et variables d'environnement
- [Guide de Sauvegarde et Restauration](BackupRestore.md) - Procédures de Sauvegarde de la base de données
- [Documentation PostgreSQL VACUUM](https://www.postgresql.org/docs/current/sql-vacuum.html) - Documentation officielle de PostgreSQL

## 📝 Résumé {#summary}

✅ **Activez la maintenance automatique** pour une optimisation de la base de données sans intervention manuelle
✅ **Surveillez via le Journal d'audit** pour vérifier le succès des opérations
✅ **Utilisez VACUUM FULL manuellement** uniquement lorsque la récupération d'espace disque est nécessaire
✅ **Planifiez VACUUM FULL** pendant les fenêtres de maintenance pour les suppressions massives
✅ **Maintenez vos Sauvegarde à jour** avant toute opération de maintenance manuelle

Le service de maintenance automatique gère l'optimisation quotidienne de la base de données, éliminant le besoin d'intervention manuelle dans la plupart des cas.
