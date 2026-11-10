# 🧬 Déduplication des Pièces jointes {#attachment-deduplication}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Les pièces jointes de la Boîte aux lettres sont stockées sous forme de charges utiles binaires dans la base de données. Le **même fichier** est très souvent joint à de nombreux e-mails (logos d'entreprise dans les signatures, PDF transférés, newsletters, contrats, ...). Sans déduplication, chaque pièce jointe conserve sa propre copie privée de ces octets, ce qui gaspille une grande quantité d'espace de base de données.

La **déduplication des pièces jointes** stocke chaque charge utile *unique* exactement une fois dans une table adressée par le contenu (indexée par le hash SHA-256 des octets) et permet à toutes les pièces jointes identiques de référencer cette copie partagée unique. Cela réduit généralement de manière significative l'empreinte de stockage des Archives contenant de nombreuses pièces jointes.

## ⚙️ Comment cela fonctionne {#how-it-works}

### Nouvelles pièces jointes {#new-attachments}
Chaque fois qu'un nouvel e-mail est archivé (IMAP, Microsoft 365 / Graph, import EML, import MBOX, ...) la charge utile de la pièce jointe est hachée et stockée une seule fois. Si la même charge utile existe déjà, la nouvelle pièce jointe référence simplement la ligne de contenu existante. Cela se produit de manière transparente au niveau de la couche de données, de sorte que chaque chemin d'archivage est couvert automatiquement.

### Données existantes (migration) {#existing-data-migration}
Pour les installations qui contiennent déjà des pièces jointes archivées **avant** l'introduction de cette fonctionnalité, une **migration en arrière-plan robuste et reprise sur erreur** convertit les charges utiles en ligne existantes en lignes de contenu partagées :

- Elle traite les pièces jointes **lot par lot**, chaque lot dans sa propre transaction.
- La progression est conservée dans une table d'état dédiée, de sorte que la migration **survit à un redémarrage de l'application** et reprend simplement là où elle s'était arrêtée.
- Elle est **idempotente** – les pièces jointes déjà migrées ne sont jamais retouchées, même après un crash ou un redémarrage.
- Elle s'exécute en arrière-plan et ne bloque pas l'utilisation normale de l'application.

Lorsque la migration se termine, elle enregistre un message de fin, par exemple :

```
Attachment Deduplication migration COMPLETED. 36199 attachments processed in 1.6 minutes.
```

Au prochain démarrage, elle détecte qu'il n'y a plus rien à faire et reste inactive. Si des pièces jointes non dédupliquées devaient subsister (par exemple après une Restauration d'une ancienne Sauvegarde), elle reprend automatiquement et les migre — le système converge toujours vers un état entièrement dédupliqué.

### Garbage collection (toujours actif) {#garbage-collection-always-on}
Lorsque des e-mails (et donc des pièces jointes) sont supprimés, une charge utile partagée peut devenir sans référence. Un mécanisme de garbage collection intégré supprime ces lignes de contenu orphelines.

> 🧹 **Le nettoyage des orphelins s'exécute TOUJOURS**, indépendamment de la
> fonctionnalité `DatabaseMaintenance`. Il s'exécute une fois juste après la migration, puis
> périodiquement (toutes les `OrphanCleanupIntervalHours`, par défaut 12 h). Lorsque
> `DatabaseMaintenance__Enabled=true`, le même nettoyage s'exécute également avant la
> `VACUUM` quotidienne afin que l'espace libéré soit physiquement récupéré.

### Utilisation du disque après la migration (important) {#disk-usage-after-the-migration-important}

> 📈 **La valeur "Storage" sur le tableau de bord peut augmenter brièvement juste après la
> migration** (ex: 3,5 Go → 5,3 Go). C'est un comportement attendu et ce n'est pas un bug.
>
> Le tableau de bord rapporte la taille **physique** de la base de données (`pg_database_size`). Lorsque la
> migration déplace une charge utile vers le stockage partagé, elle marque l'ancienne copie en ligne comme `NULL`,
> mais PostgreSQL (MVCC) conserve ces anciens octets en tant que *dead tuples* jusqu'à ce que le fichier de la table soit
> réécrit — ainsi, pendant un moment, la base de données contient à la fois l'ancienne et la nouvelle copie.
>
> Pour corriger cela automatiquement, l'application exécute un `VACUUM FULL` **unique**
> sur la table des pièces jointes **une fois la migration terminée et lorsqu'aucun
> travail de synchronisation n'est en cours**. Cela réécrit la table et rend l'espace libéré au
> système d'exploitation ; ensuite, la taille rapportée descend **en dessous** de l'originale. Cela s'exécute
> exactement une fois (suivi via un marqueur `ReclaimedAt`), est sûr en cas de redémarrage, et est différé
> tant qu'une synchronisation est en cours (il réessaie automatiquement une fois inactif).

>
> ⚠️ Le `VACUUM FULL` verrouille la table des pièces jointes pendant sa durée (pas de lecture/écriture
> de pièces jointes entre-temps) et nécessite de l'espace disque libre pour la réécriture. Si vous voulez
> le déclencher manuellement à la place, exécutez :
> ```sql
> VACUUM (FULL, ANALYZE) mail_archiver."EmailAttachments";
> ```

## 🔧 Configuration {#configuration}


Ces paramètres sont optionnels ; les valeurs par défaut sont raisonnables pour la plupart des installations. Voir aussi le
[Guide d'installation](Setup.md#-attachment-deduplication-settings).

| Paramètre | Par défaut | Description |
|---------|---------|-------------|
| `AttachmentDeduplication__BatchSize` | `200` | Nombre de pièces jointes existantes migrées par transaction. Des valeurs plus élevées migrent plus vite mais augmentent la charge mémoire/DB par lot. |
| `AttachmentDeduplication__DelayBetweenBatchesMs` | `0` | Pause optionnelle (ms) entre les lots de migration pour limiter la charge DB sur les systèmes occupés. |
| `AttachmentDeduplication__StartupDelaySeconds` | `20` | Délai (s) après le démarrage de l'app avant que la migration ne commence (permet à la migration du schéma de se terminer d'abord). |
| `AttachmentDeduplication__OrphanCleanupIntervalHours` | `12` | Intervalle (h) du garbage collection des orphelins toujours actif. |
| `AttachmentDeduplication__CommandTimeoutSeconds` | `300` | Délai d'expiration de la commande de base de données (en secondes) pour les opérations par lots de migration. Par défaut est 5 minutes. Si un lot expire toujours, le service réessaie automatiquement avec une taille de lot divisée par deux. |

Exemple (`docker-compose.yml`) :

```yaml
environment:
  - AttachmentDeduplication__BatchSize=200
  - AttachmentDeduplication__DelayBetweenBatchesMs=0
  - AttachmentDeduplication__StartupDelaySeconds=20
  - AttachmentDeduplication__OrphanCleanupIntervalHours=12
  - AttachmentDeduplication__CommandTimeoutSeconds=300
```

## ❓ FAQ {#faq}

**Puis-je désactiver la déduplication ?**
Non. C'est une fonctionnalité de stockage centrale et elle est toujours active. Vous pouvez seulement ajuster la taille des lots et les paramètres de planification ci-dessus.

**Mes données sont-elles en sécurité pendant la migration ?**
Oui. Chaque lot est une transaction unique et le travail est idempotent. Un crash ou un
redémarrage en milieu de migration annule le lot inachevé et celui-ci est retraité au
prochain démarrage ; aucune donnée n'est perdue ou dupliquée.

**Cela ralentit-il l'archivage ?**
Le surcoût de hachage par pièce jointe est négligeable. La migration unique des
données existantes s'exécute en arrière-plan et peut être limitée avec
`DelayBetweenBatchesMs` si nécessaire.

**La migration s'interrompt et redémarre sans cesse au même curseur. Que puis-je faire ?**
Cela peut arriver sur de très grandes bases de données (des centaines de milliers d'e-mails) où un seul lot de 200 pièces jointes avec le hachage SHA-256 dépasse le délai d'expiration de commande par défaut. Le service réessaie désormais automatiquement avec une taille de lot réduite de moitié lorsqu'un délai d'expiration survient. Si vous voyez toujours des expirations, vous pouvez manuellement réduire la taille du lot et/ou augmenter le délai d'expiration de la commande :

```yaml
environment:
  - AttachmentDeduplication__BatchSize=10
  - AttachmentDeduplication__CommandTimeoutSeconds=600
```

**Combien d'espace vais-je économiser ?**
Cela dépend entièrement du nombre de pièces jointes en double que contient votre Archive.
Les Archives avec de nombreuses signatures, logos ou fichiers transférés répétés en bénéficient le plus.

---
