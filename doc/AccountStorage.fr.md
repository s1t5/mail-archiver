# 💾 💾 Affichage du stockage par Compte {#per-account-storage-display}

[← Retour à l'index de la Documentation](index.md)

## 📋 Aperçu {#overview}

Le tableau « Account Overview » du Dashboard et le tableau « Show All » de MailAccounts incluent une colonne **Storage**. Cette colonne indique l'espace de base de données utilisé par chaque compte — incluant **tous les champs de chaque mail archivé** ainsi que **toutes les pièces jointes**.

## ⚙️ Comment ça marche {#how-it-works}

Les valeurs de stockage sont **mises en cache** dans la table de base de données `AccountStorageCache`. Cela signifie que les pages web lisent des valeurs pré-calculées au lieu d'exécuter une requête coûteuse à chaque fois que vous ouvrez le Dashboard ou la liste des comptes. Le cache est maintenu à jour par un service d'arrière-plan indépendant (`AccountStorageRefreshService`).

Pour chaque compte, le service demande à PostgreSQL d'additionner `pg_column_size` pour chaque colonne des lignes du compte dans la table `ArchivedEmails`. Cela couvre **tous les champs d'un mail** (Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash, etc.), incluant la compression PostgreSQL et le stockage hors ligne (TOAST). La taille d'une valeur hors ligne est lue à partir de son pointeur TOAST, de sorte que le calcul scanne uniquement la table elle-même et ne charge jamais les corps de messages. La partie relative aux pièces jointes est ajoutée séparément en tant que somme logique de `EmailAttachment.Size`.

Le service de rafraîchissement met à jour le cache dans trois situations :

1. **Remplissage au démarrage (Startup backfill)** — Après une nouvelle installation ou après une mise à jour vers la version introduisant cette fonctionnalité, chaque compte commence sans valeur en cache. Le service parcourt tous les comptes un par un et calcule la première valeur.
2. **Rafraîchissement quotidien** — Une fois par jour à l'heure configurée (par défaut `02:30`), le service recalcule tous les comptes comme filet de sécurité.
3. **Rafraîchissement immédiat** — Après une synchronisation de mail, un import EML, un import MBOX ou une suppression de conservation, le compte concerné est recalculé immédiatement afin que la valeur affichée reste actuelle.

## 🤔 Pourquoi "0 B" est affiché au début {#why-0-b-is-shown-at-first}

Lorsque la fonctionnalité est activée pour la première fois, ou lorsqu'un nouveau compte est créé, la ligne de cache pour ce compte n'existe pas encore. Le service de rafraîchissement s'exécute en arrière-plan et calcule les comptes **un par un**, avec un court délai entre chaque compte afin que les archives très volumineuses ne surchargent pas la base de données.
Tant que le service n'a pas traité un compte, l'interface utilisateur affiche **"0 B"** comme espace réservé. C'est tout à fait normal. Dès que le service d'arrière-plan a terminé ce compte, la valeur réelle apparaît lors du prochain chargement ou rafraîchissement de la page.
Pour les archives existantes volumineuses, le remplissage peut prendre plusieurs minutes ou plus. Le service suit sa progression dans une table dédiée (`AccountStorageBackfillState`), de sorte que si l'application est redémarrée au milieu du remplissage, elle reprend simplement là où elle s'était arrêtée — aucun compte n'est calculé deux fois.

## 🔧 Configuration {#configuration}

La fonctionnalité peut être ajustée dans `appsettings.json` ou via des variables d'environnement. Consultez la section **Account Storage Settings** dans [Setup.md](Setup.md) pour connaître toutes les options disponibles, notamment :

- `AccountStorage__Enabled`: Activer ou désactiver le service de rafraîchissement (`true` par défaut).
- `AccountStorage__DailyExecutionTime`: Temps pour le rafraîchissement complet quotidien (`02:30` par défaut).
- `AccountStorage__BackfillDelayMs`: Pause entre les comptes pendant le remplissage au démarrage (`5000` ms par défaut).
- `AccountStorage__RefreshBatchDelayMs`: Pause entre les comptes pendant le rafraîchissement quotidien (`1000` ms par défaut).
- `AccountStorage__CommandTimeoutSeconds`: Délai d'attente de commande de la base de données pour le calcul du stockage par compte (`300` secondes par défaut). Augmentez cette valeur pour les archives très volumineuses où le scan `pg_column_size` prend plus de temps.

> 💡 **Conseil** : Vous n'avez **pas** besoin d'activer `DatabaseMaintenance__Enabled` pour que cette fonctionnalité fonctionne. Le service de rafraîchissement du stockage s'exécute indépendamment.
