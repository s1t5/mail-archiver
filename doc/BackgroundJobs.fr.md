# ⚙️ Background Jobs {#background-jobs}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

La majeure partie du travail dans Mail Archiver ne se déroule pas pendant que vous attendez. La Synchronisation d'une Boîte aux lettres, l'importation d'un fichier MBOX, la Restauration de milliers d'e-mails ou l'exportation d'une Archive peuvent prendre de quelques minutes à plusieurs heures. Ces tâches s'exécutent en tant que **background jobs** — vous les lancez, et elles continuent de s'exécuter même si vous fermez le navigateur.

La page **Background Jobs** montre ce qui est en cours d'exécution, ce qui est terminé et ce qui a échoué. C'est l'endroit où regarder lorsqu'une tâche prend plus de temps que prévu ou lorsqu'une synchronisation se termine avec des erreurs.

> **ℹ️ Page Administrateur.** Les Background Jobs nécessitent des droits d'administrateur et sont accessibles depuis le menu principal. Les utilisateurs réguliers ne les voient pas — ils peuvent toujours lancer des tâches (par exemple une Restauration par lots), mais la surveillance se fait ici.

## 🗂️ Types de tâches {#job-types}

La page regroupe les tâches par type. Chaque section affiche jusqu'aux **20 tâches les plus récentes**, les tâches en cours et en file d'attente étant triées en haut — afin qu'une ancienne tâche ne soit pas poussée hors de vue par des nouvelles.

| Section | Ce qu'elle couvre | Lancée depuis |
|---|---|---|
| **Synchronization Jobs** | Récupération de nouveaux e-mails depuis le serveur de messagerie | Planning automatique, ou *Sync now* sur un Compte |
| **MBox Import Jobs** | Importation de fichiers `.mbox` | Mail Accounts → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **EML Import Jobs** | Importation de fichiers `.eml` / archives ZIP | Mail Accounts → [Import EML](Import-EML-MBox.md#-import-eml) |
| **Batch Restore Jobs** | Copie d'e-mails sélectionnés vers une Boîte aux lettres | Archive → select emails → Batch Restore |
| **Account Export Jobs** | Exportation d'un Compte entier (mbox / EML) | Mail Accounts → [Export](Export-Account.md) |
| **Selected Emails Export Jobs** | Exportation d'un ensemble d'e-mails choisis | Archive → select emails → Export |
| **Email Deletion Jobs** | Suppression d'e-mails du serveur ou de l'Archive, incluant la suppression groupée par filtre | Politique de Conservation, suppression manuelle, ou Archive → *Delete all matches* |
| **Account Deletion Jobs** | Suppression d'un Compte de messagerie entier | Mail Accounts → Delete |
| **Offload** | Déplacement des e-mails d'une période donnée vers un autre Compte | Mail Accounts → Offload |

## 📊 Lecture d'une ligne de tâche {#reading-a-job-row}

Chaque ligne comporte le même ensemble de colonnes :

| Colonne | Signification |
|---|---|
| **Job ID** | Identifiant court — citez-le lors du signalement d'un problème |
| **Account** | La Boîte aux lettres sur laquelle travaille la tâche |
| **Status** | État actuel, voir le tableau ci-dessous |
| **Progress** | Avancement de la tâche, avec traités/total si connus |
| **Created** | Date de mise en file d'attente de la tâche |
| **Started** | Date de début effectif de l'exécution |
| **Completed** | Date de fin (vide si toujours en cours) |
| **Duration** | Temps écoulé |

Certains types de tâches ajoutent des compteurs à la colonne de progression :

- **Success** / **Failed** — e-mails traités avec succès et sans succès
- **Skipped (duplicates)** — e-mails déjà présents dans l'Archive, non importés deux fois
- **Recovered** — messages récupérés après que le serveur les a signalés comme manquants
- **Malformed (skipped)** — messages qui n'ont pas pu être analysés et ont été ignorés

### Valeurs de Status {#status-values}

| Status | Signification |
|---|---|
| **Queued** | En attente de démarrage — une autre tâche est en cours, ou le worker est occupé |
| **Running** | En cours d'exécution |
| **Completed** | Terminé avec succès |
| **CompletedWithErrors** | Terminé, mais certains éléments ont échoué (importations MBox) |
| **Failed** | S'est terminé avec une erreur |
| **Cancelled** | Arrêté par un utilisateur |
| **Rate Limited** | *Sync paused due to bandwidth limit. Will resume automatically.* |
| **Timed Out** | La synchronisation a dépassé la limite de temps configurée et a été arrêtée |

> **ℹ️ "Rate Limited" et "Timed Out" sont des états propres à la synchronisation.** Ils ressemblent à des échecs mais ne le sont pas : une synchronisation limitée par le débit reprend d'elle-même une fois que le quota de bande passante le permet, et une synchronisation ayant expiré (timed-out) reprend à son point de contrôle lors de l'exécution suivante. Voir [Rate Limit Handling](RateLimitHandling.md) et [Mail Synchronization](Synchronization.md).

### Pourquoi la progression peut rester à 0% {#why-progress-can-sit-at-0}

La progression est mesurée par **Dossiers terminés**, et non par e-mails individuels :

> *La progression est mesurée par Dossiers terminés. Un gros Dossier (ex: INBOX) maintient la barre à 0% jusqu'à ce qu'il soit terminé.*

Sur une Boîte aux lettres où l'INBOX contient la majorité des e-mails, la barre peut rester à 0% pendant longtemps puis bondir. C'est normal — la tâche travaille, elle n'a juste pas encore terminé son premier gros Dossier.

## 🔄 La page s'actualise elle-même {#the-page-refreshes-itself}

Tant que la page est ouverte, elle se recharge toutes les **30 secondes**, vous pouvez donc la laisser dans un onglet et suivre la progression sans rien cliquer. L'étiquette indique quand les données ont été mises à jour pour la dernière fois.

Le rechargement est annulé dès que vous cliquez sur un lien ou un bouton — sinon votre clic tomberait sur une page en cours de remplacement.

**Configurable :** `Jobs:RefreshSeconds` dans `appsettings.json` (par défaut `30`).
Réglez sur `0` pour désactiver le rechargement automatique. Chaque rechargement reconstruit la page à partir de toutes les sources de tâches, donc un intervalle très court sur une installation avec de nombreux Comptes représente une charge réelle — et cette charge se multiplie avec chaque onglet ouvert.

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 Annuler une tâche {#cancelling-a-job}

Les tâches qui supportent l'annulation affichent un bouton **Cancel Job**, qui demande :

> *Are you sure you want to cancel this job?*

Les tâches d'importation utilisent la formulation **Cancel Import** / *Are you sure you want to cancel this import job?*

L'annulation est disponible pour les tâches de **synchronisation, restauration par lots, export d'e-mails sélectionnés et suppression d'e-mails**. La tâche s'arrête dès que l'élément actuel est terminé ; le statut devient **Cancelled**.

> **⚠️ L'annulation n'est pas un rollback.** Les e-mails déjà importés, restaurés, exportés ou supprimés le restent. Annuler empêche la tâche de faire *plus* — cela n'annule pas ce qu'elle a déjà fait. Pour une tâche de suppression, cela signifie que les e-mails déjà retirés sont perdus.

Les tâches d'importation de Compte et de MBox ne peuvent pas être annulées depuis cette page ; arrêtez-les depuis la page d'importation propre au Compte.

## ⚠️ Échecs de synchronisation et "Acknowledge Failures" {#sync-failures-and-acknowledge-failures}

Lorsqu'une synchronisation se termine avec le statut **Failed**, la ligne de la tâche propose **Acknowledge Failures**. C'est la seule action sur la page qui modifie le comportement futur, il est donc important de la comprendre.

Chaque exécution de synchronisation démarre là où la précédente s'est arrêtée, en utilisant un **checkpoint**. Si une synchronisation échoue pour certains e-mails, le checkpoint reste derrière ces e-mails afin que la prochaine exécution les tente à nouveau. C'est le comportement par défaut sécurisé — rien n'est ignoré silencieusement.

Mais si les échecs sont permanents (un message corrompu, un dossier que le serveur refuse de servir), chaque synchronisation suivante les réessaiera et la synchronisation échouera systématiquement au même endroit.

**Acknowledge Failures** résout ce blocage en faisant avancer le checkpoint au-delà des e-mails en échec. La confirmation est explicite sur les conséquences :

> *This will advance the sync timestamp to the completion time of this job, so the next sync will be a normal incremental sync. The failed emails will not be retried. Continue?*

En cas de succès, vous verrez : *"Sync timestamp has been advanced. The next sync will be incremental."*

> **⚠️ N'utilisez "Acknowledge" qu'après avoir examiné les échecs.** Après avoir validé, ces e-mails ne seront **pas** réessayés — ils manqueront à l'Archive à moins que vous ne les récupériez par un autre moyen. Vérifiez d'abord les détails de l'échec, et si la cause est une mauvaise configuration, corrigez-la avant de valider. Voir [Mail Synchronization](Synchronization.md) pour le comportement du checkpoint en détail.

L'action d'acquittement est enregistrée dans le Journal d'audit sous le nom *SyncAcknowledgeFailures*, avec le nombre d'e-mails en échec et le Compte concerné, afin d'être traçable ultérieurement.

## 👁️ Ce que vous ne pouvez pas faire ici {#what-you-cannot-do-here}

- **Pas de bouton de réessai.** Une tâche ayant échoué ne redémarre pas depuis cette page — relancez l'opération là où vous l'aviez commencée (sync, import, export).
- **Pas de modification.** Les paramètres de la tâche sont fixés à la création.
- **Seulement les 20 derniers par type.** Les tâches plus anciennes ne sont pas listées. Pour les archives à long terme, le Journal d'audit et le log de l'application sont les sources à utiliser — voir [Access Logging](Logs.md) et [Docker Compose Logs](DockerComposeLogs.md).

## ❓ Dépannage {#troubleshooting}

| Symptôme | Cause probable | Que faire |
|---|---|---|
| La tâche reste **Queued** | Une autre tâche du même worker est en cours | Attendre ; vérifier les autres sections pour une tâche de longue durée |
| La progression reste à 0% | Un gros Dossier est encore en cours de traitement | Normal — voir "Pourquoi la progression peut rester à 0%" |
| La synchronisation échoue (**Failed**) de façon répétée sur le même Compte | Erreurs permanentes sur des e-mails individuels | Inspecter les échecs, corriger la cause, puis acquitter |
| La synchronisation affiche **Rate Limited** | Quota de bande passante atteint | Rien — elle reprend automatiquement |
| La synchronisation affiche **Timed Out** | Boîte aux lettres plus grande que la limite de temps | Elle reprendra au checkpoint lors de la prochaine exécution ; voir [Synchronization](Synchronization.md) |
| **CompletedWithErrors** sur une importation MBox | Certains messages sont malformés | Les compteurs indiquent combien ; le reste a été importé |
| La tâche disparaît après rechargement | Elle est sortie de la fenêtre des "20 derniers" | Vérifier le log de l'application pour son statut final |
| La page se recharge pendant que vous travaillez | Rafraîchissement automatique toutes les 30 s | Régler `Jobs:RefreshSeconds` sur `0` |

## 📚 Documentation associée {#related-documentation}

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — checkpoints, synchronisations avec timeout
- [Rate Limit Handling](RateLimitHandling.md) — quotas de bande passante et reprise automatique
- [Access Logging](Logs.md) — qui a fait quoi, incluant les actions de tâches
- [Batch Restore](MailboxMigration.md) — déplacer des e-mails entre les Boîtes aux lettres
- [Date-Windowed Offload](Offload.md) — tâches d'offload
- [Docker Compose Logs](DockerComposeLogs.md) — lire le log de l'application
