# 📊 Gestion de la limite de débit {#rate-limit-handling}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

De nombreux fournisseurs de messagerie imposent des limites de bande passante sur l'accès IMAP. Ces limites varient généralement de quelques centaines de mégaoctets à plusieurs gigaoctets par période de 24 heures. Lors de la Synchronisation de grosses Boîtes aux lettres qui dépassent ces limites, le processus de synchronisation échouerait normalement ou serait incomplet.

Mail Archiver inclut un système intégré de gestion de la limite de débit qui gère automatiquement ces limitations, assurant une Synchronisation fiable même pour les grosses Boîtes aux lettres.

## ⚙️ Comment ça marche {#how-it-works}

### Suivi automatique de la bande passante {#automatic-bandwidth-tracking}

Le système surveille continuellement la quantité de données téléchargées pendant la Synchronisation IMAP. Lorsque la limite de bande passante configurée est atteinte :

1. **La Synchronisation s'interrompt automatiquement** : L'opération de Synchronisation en cours s'arrête proprement.
2. **La progression est sauvegardée** : Des points de contrôle stockent la position exacte où la Synchronisation s'est arrêtée.
3. **Mises à jour du statut** : Le Compte affiche le statut "Rate-Limited" dans le tableau de bord.
4. **Reprise automatique** : Lorsque la limite est réinitialisée (généralement après 24 heures), la Synchronisation reprend à partir du point de contrôle sauvegardé.

### Exemple : Boîte aux lettres de 14 Go avec une limite quotidienne de 2,5 Go {#example-14-gb-mailbox-with-25-gb-daily-limit}

```
Day 1:  Sync starts → ~2.5 GB downloaded → Rate-Limited
        Status: Rate-Limited, Checkpoint saved

Day 2:  Limit still active → Sync skipped

Day 3:  Limit resets → Sync resumes from checkpoint
        → Additional ~2.5 GB → Rate-Limited again

Day 4-6: Continues...

Day N:   All 14 GB synced → Status: Completed
```

## 🔧 Configuration {#configuration}

### Activer le suivi de la bande passante {#enabling-bandwidth-tracking}

Ajoutez la section suivante à votre `appsettings.json` :

```json
{
  "BandwidthTracking": {
    "Enabled": true,
    "DailyLimitMb": 2500,
    "WarningThresholdPercent": 80,
    "PauseHoursOnLimit": 24
  }
}
```

### Options de configuration {#configuration-options}

| Option | Description | Par défaut | Exemple |
|--------|-------------|---------|---------|
| `Enabled` | Activer/désactiver le suivi de la bande passante | `false` | `true` |
| `DailyLimitMb` | Limite de téléchargement quotidienne en mégaoctets | `2500` | `2500` |
| `WarningThresholdPercent` | Pourcentage de la limite pour déclencher des avertissements dans les logs | `80` | `80` |
| `PauseHoursOnLimit` | Heures de pause lorsque la limite est atteinte | `24` | `24` |

### Définir la limite correcte {#setting-the-correct-limit}

Pour déterminer la valeur `DailyLimitMb` appropriée :

1. Consultez la documentation ou les conditions d'utilisation de votre fournisseur de messagerie.
2. Recherchez les limites de bande passante IMAP ou les quotas de téléchargement.
3. Contactez votre administrateur de messagerie si vous utilisez un système de messagerie d'entreprise.
4. Commencez par une valeur prudente si vous n'êtes pas sûr (ex: 2000 MB).
5. Surveillez les logs pour les avertissements de limite de débit pendant les premières Synchronisations.

## 👤 Expérience utilisateur {#user-experience}

### Indicateurs du tableau de bord {#dashboard-indicators}

Lorsque la limitation de débit est active, le Compte affiche :

- **Statut** : "Rate-Limited"
- **Bannière d'info** : Explique la situation et l'heure de réinitialisation estimée.
- **Progression de la Synchronisation** : Affiche les e-mails traités avant que la limite ne soit atteinte.

### Comportement automatique {#automatic-behavior}

1. **Aucune intervention manuelle requise** : Le système gère tout automatiquement.
2. **Redémarrages de l'application** : Si l'application redémarre, l'état de la limite de débit est préservé dans la base de données.
3. **Comptes multiples** : Chaque Compte suit sa bande passante indépendamment.

### Ce qui est sauvegardé {#what-gets-saved}

Lorsqu'une limite de débit survient, le système sauvegarde :

- **Points de contrôle de Synchronisation** : Position exacte dans chaque Dossier où la Synchronisation s'est arrêtée.
- **Utilisation de la bande passante** : Total des octets téléchargés pour la journée en cours.
- **État de la limite** : Si la limite est atteinte et quand elle sera réinitialisée.

## 🗄️ Maintenance de la base de données {#database-maintenance}

Le `DatabaseMaintenanceService` nettoie automatiquement les anciennes données :

- **Enregistrements de bande passante** : Supprimés après 7 jours.
- **Points de contrôle de Synchronisation** : Points de contrôle terminés supprimés après 30 jours.
- **Points de contrôle incomplets** : Préservés jusqu'à ce que la Synchronisation se termine.

## ✅ Bonnes pratiques {#best-practices}

### Pour les grosses Boîtes aux lettres {#for-large-mailboxes}

1. **Activer le suivi de la bande passante** : Essentiel pour les Boîtes aux lettres plus grandes que la limite quotidienne.
2. **Prévoir la Synchronisation initiale** : Une grosse Boîte aux lettres peut prendre plusieurs jours pour être entièrement synchronisée.
3. **Surveiller la progression** : Vérifiez le tableau de bord pour le statut de la limite de débit.
4. **Ne pas interrompre** : Laissez la reprise automatique gérer la continuation.

### Pour les administrateurs {#for-administrators}

1. **Définir des limites appropriées** : Faites correspondre le `DailyLimitMb` aux limites réelles de votre fournisseur de messagerie.
2. **Surveiller les logs** : Des messages d'avertissement apparaissent à 80 % de la limite.
3. **Espace de base de données** : Le suivi de la bande passante utilise un stockage supplémentaire minimal.
4. **Sauvegarde avant modifications** : Effectuez toujours une Sauvegarde avant de modifier les paramètres de limite de débit.

### Considérations de performance {#performance-considerations}

- ✅ **Impact mémoire** : Négligeable - suit uniquement les octets, ne stocke pas les e-mails.
- ✅ **Impact base de données** : Minimal - petits enregistrements nettoyés automatiquement.
- ✅ **Vitesse de Synchronisation** : Aucun impact sur la performance pendant la Synchronisation active.

## 🛡️ Erreurs FETCH transitoires / Limitation côté serveur {#transient-fetch-errors-server-side-throttling}

En plus du suivi de la bande passante basé sur les octets décrit ci-dessus, Mail Archiver détecte et réagit également à la **limitation par message** signalée par le serveur IMAP lui-même. Certains fournisseurs IMAP hébergés appliquent une limitation par Compte ou par IP qui est **indépendante des pauses `BatchOperation` configurées**, car la limitation est appliquée côté serveur en fonction du volume global de requêtes pendant la session actuelle.

### Symptômes {#symptoms}

Le serveur renvoie une réponse `NO` (ou `BAD`) à une commande `FETCH` unique pour un corps de message, avec des messages tels que :

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- des réponses contenant `throttle`, `rate limit` ou des marqueurs similaires.

L'authentification, la liste des Dossiers et la recherche de messages elle-même réussissent généralement — seule la récupération réelle du message complet échoue.

### Gestion automatique {#automatic-handling}

Lorsqu'une telle erreur transitoire est détectée, Mail Archiver applique une stratégie de tentative par message :

1. **Nouvelle tentative avec backoff exponentiel** : Jusqu'à **3 tentatives** pour le même message, avec des délais de **5 s → 15 s → 60 s**.
2. **Récupération de la connexion** : Avant chaque tentative, l'état de la connexion, de l'authentification et de la sélection du Dossier est validé et restauré si nécessaire.
3. **Réinitialisation en cas de succès** : Un FETCH réussi réinitialise le compteur d'échecs consécutifs.
4. **Pause de Synchronisation propre** : Si **10 messages consécutifs** échouent toujours avec des erreurs transitoires après toutes les tentatives, l'exécution de Synchronisation en cours est mise en pause proprement — le même mécanisme utilisé par la pause de limite de bande passante :
   - L'horodatage `LastSync` n'est **pas** mis à jour.
   - Si `BandwidthTracking` est activé, les points de contrôle sont préservés afin que la prochaine exécution de Synchronisation reprenne à partir du dernier message traité avec succès.
   - La tâche est marquée comme limitée par le débit.
   - La prochaine Synchronisation prévue reprendra automatiquement.

### Atténuation {#mitigation}

Si vous atteignez régulièrement la limitation côté serveur, réduisez le taux de requête en ajustant vos paramètres `BatchOperation` :

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

Notez que certains fournisseurs limitent en fonction du **nombre total d'octets** ou du **nombre total de FETCH par fenêtre de temps** plutôt que par taux par seconde. Dans ces cas :

- Activez `BandwidthTracking` (voir ci-dessus) et configurez `DailyLimitMb` pour correspondre à la limite documentée du fournisseur. La Synchronisation s'interrompra alors proprement lorsque le budget quotidien sera épuisé et reprendra automatiquement le lendemain.
- Les Synchronisations initiales de très grosses Boîtes aux lettres peuvent prendre plusieurs jours, ce qui est le comportement prévu.

### Ce qui est considéré comme une erreur transitoire {#what-counts-as-a-transient-error}

Seules les réponses IMAP `NO`/`BAD` avec du texte ou des codes de réponse liés à la limitation sont traitées comme transitoires. Les autres erreurs (messages malformés, problèmes de décodage UTF-8, etc.) sont toujours comptabilisées comme `FailedEmails` immédiatement et **ne déclenchent pas** la logique de tentative/pause.

## 📚 Documentation associée {#related-documentation}

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Procédures de nettoyage de la base de données
- [GmailBestPractices.md](GmailBestPractices.md) - Configuration spécifique à Gmail
