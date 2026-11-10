# 📤 Date-Windowed Offload {#date-windowed-offload}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Un offload ajoute des e-mails archivés plus récents qu'une date de coupure dans une autre Boîte aux lettres, et laisse tout ce qui est plus ancien dans l'Archive. Il existe pour les migrations où la nouvelle Boîte aux lettres doit ne contenir que les e-mails récents tandis que l'historique complet reste accessible via Mail Archiver.

Il diffère de [Copy All Emails to Another Mailbox](MailboxMigration.md) de cinq manières :

| | Copy All Emails | Offload |
|---|---|---|
| Filtre de date | aucun, tout ou rien | date de coupure, avec une limite supérieure optionnelle |
| Répétable | non, rajoute tout à nouveau | oui, ignore ce que la cible contient déjà |
| Exclusions de Dossier | non | oui, configurable |
| Renommage de Dossier | non | oui, configurable |
| Aperçu | non | rapport de simulation (dry run) par Dossier sans écriture |

L'offload ne fait qu'ajouter des éléments à la cible. Il ne supprime jamais rien de l'Archive et ne touche jamais à la Boîte aux lettres source.

## 🔁 Répéter une exécution est sans danger {#repeating-a-run-is-safe}

Avant le premier ajout, toute la Boîte aux lettres cible est indexée et chaque message est vérifié par rapport à celle-ci. Une exécution répétée n'ajoute donc rien qui soit déjà arrivé, et le signale comme déjà présent à la place. Cela a trois conséquences pratiques :

- Une exécution interrompue est récupérée en la relançant simplement.
- Une exécution terminée peut être répétée comme une passe de vérification. Un résultat correct signifie que rien n'a été ajouté et que tout est déjà présent.
- Les messages supprimés sur la cible reviennent lors de l'exécution suivante, car ils ne sont plus dans l'index.

Deux critères décident si un message est déjà présent, dans cet ordre :

1. **Message-ID**, normalisé. Cela fait la quasi-totalité du travail.
2. **Une empreinte digitale (fingerprint)** sur l'expéditeur, les destinataires, l'objet et l'heure d'envoi à deux secondes près. Cela couvre les messages dont le Message-ID stocké est manquant ou malformé. De tels messages ne peuvent pas du tout être restaurés avec leur Message-ID d'origine, donc sans ce second critère, ils seraient dupliqués à chaque exécution.

Le travail rapporte combien de correspondances proviennent de l'empreinte digitale séparément, car c'est le critère qui pourrait échouer silencieusement.

## ⚙️ Configuration {#configuration}

Une nouvelle section `Offload`, avec des valeurs par défaut qui reproduisent le comportement précédent :

```json
"Offload": {
  "MaxConcurrentJobs": 1,
  "PrefetchMaxMessages": 500000,
  "ExcludedSourceFolders": [],
  "FolderRenameMap": {},
  "MarkAsSeen": true
}
```

| Clé | Valeur par défaut | Utilité |
|---|---|---|
| `MaxConcurrentJobs` | `1` | Nombre de jobs de Restauration ou d'offload pouvant s'exécuter simultanément. Une valeur de 1 conserve le comportement strictement séquentiel que la file d'attente des jobs avait toujours. Au maximum, un job par Boîte aux lettres cible s'exécute, quelle que soit cette valeur. |
| `PrefetchMaxMessages` | `500000` | Limite supérieure sur le nombre de messages indexés d'une Boîte aux lettres cible. Au-delà, la vérification des doublons se restreint à un seul Dossier et l'indique dans les logs. |
| `ExcludedSourceFolders` | vide | Dossiers sources qui ne sont jamais déchargés. Vérifiés avant le renommage, et incluent les sous-dossiers. |
| `FolderRenameMap` | vide | Réécrit les segments de tête d'un chemin de Dossier source. |
| `MarkAsSeen` | `true` | Si les e-mails ajoutés sont marqués comme lus. |

Les deux paramètres de Dossier sont volontairement vides. Réécrire ou supprimer des dossiers sans y être invité surprendrait quiconque utilise déjà le chemin de Restauration ; ainsi, une exécution sans configuration migre tout, dossiers de spam inclus, et crée un second ensemble de dossiers spéciaux à côté de ceux de la cible.

### Exclusions et renommage de Dossier {#folder-exclusions-and-renaming}

Pour une migration d'Exchange vers Dovecot, les deux paramètres ressemblent généralement à ceci :

```json
"ExcludedSourceFolders": ["Junk E-Mail", "Deleted Items"],
"FolderRenameMap": {
  "Sent Items": "Sent",
  "Deleted Items": "Trash",
  "Junk E-Mail": "Junk"
}
```

Sans la table de renommage, chaque Boîte aux lettres migrée se retrouve avec à la fois `Sent` et `Sent Items`, car Dovecot pré-crée ses propres dossiers à usage spécial.

Quatre points sur la manière dont ceux-ci sont appliqués méritent d'être connus, car chacun est une source potentielle de surprise :

1. **Les exclusions sont vérifiées en premier**, par rapport au nom source. Si le renommage avait lieu en premier, une exclusion sur `Deleted Items` ne s'appliquerait jamais une fois qu'il serait devenu `Trash`.
2. **Le renommage réécrit le préfixe de chemin correspondant le plus long**, sur les limites de segments. `Sent Items` devenant `Sent` transforme également `Sent Items/2019` en `Sent/2019`, et laisse `Sent Items Archive` tel quel.
3. **La correspondance est insensible à la casse**, et `/` ainsi que `\` comptent comme des séparateurs.
4. **Deux Dossiers sources peuvent converger vers une seule cible.** `Sent Items` et `Sent` devenant tous deux `Sent` est correct et n'ouvre pas le Dossier cible deux fois.

Changer la table de renommage entre deux exécutions ne duplique rien, car la vérification des doublons couvre l'ensemble de la Boîte aux lettres cible plutôt qu'un seul Dossier résolu.

## 🖥️ Exécution depuis l'interface {#running-it-from-the-interface}

Sur la page de détails d'un Compte, **Offload to Another Mailbox** ouvre un formulaire avec la Boîte aux lettres cible, le Dossier racine cible, l'option de préserver la structure des Dossiers, la fenêtre de date, un commutateur de simulation (dry run), et l'option de marquer les e-mails ajoutés comme lus. Les exclusions et la table de renommage configurées sont affichées en lecture seule, de sorte qu'une exécution n'est jamais une surprise.

Le job est mis en file d'attente et sa progression ainsi que le rapport par Dossier apparaissent sur la page de statut des jobs et sous **Jobs**. Le rapport par Dossier nomme les Dossiers sources, il n'est donc affiché qu'à l'utilisateur qui a lancé le job et aux administrateurs.

La Boîte aux lettres cible peut être un Compte IMAP désactivé, par exemple pour préparer une cible de migration avant sa mise en service ; l'exécution échoue bruyamment à la connexion si la cible est injoignable. Les Comptes Microsoft 365 ne peuvent pas être des cibles d'offload ; le chemin de Restauration Graph reste inchangé.

### Qui peut l'exécuter {#who-may-run-it}

Les administrateurs peuvent effectuer un offload entre n'importe quels deux Comptes. Un self-manager peut effectuer un offload **uniquement entre les Comptes qui lui sont assignés**, aux deux extrémités : la source, car la page du Compte est déjà limitée de cette façon, et la cible, car la liste des cibles est restreinte au même ensemble et la requête est vérifiée à nouveau lors du lancement du job. Un self-manager avec un seul Compte assigné n'a donc aucune cible éligible et en est informé.

Le périmètre provient de `IAccountAccessResolver`, le même résolveur que l'API REST et le serveur MCP utilisent, il y a donc une seule définition de qui peut utiliser quelle Boîte aux lettres. La décision elle-même réside dans `OffloadTargetEligibility`, que la liste des cibles et la vérification de la requête appellent tous deux, de sorte que ce que le formulaire propose et ce que le serveur accepte ne peuvent pas diverger.

## ⌨️ Exécution depuis la ligne de commande {#running-it-from-the-command-line}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll \
  --offload --source-account-id 3 --target-account-id 7 \
  --since 2025-08-01 \
  [--until 2026-08-01] [--target-folder INBOX] \
  [--preserve-folders] [--dry-run] [--no-mark-seen]
```

| Argument | Signification |
|---|---|
| `--source-account-id` | Compte à partir duquel lire les e-mails archivés. Requis. |
| `--target-account-id` | Compte IMAP dans lequel ajouter. Requis, et doit différer de la source. |
| `--since` | Limite inférieure inclusive sur la date d'envoi, `YYYY-MM-DD`. Requis. |
| `--until` | Limite supérieure inclusive optionnelle. |
| `--target-folder` | Dossier racine dans la Boîte aux lettres cible. Par défaut `INBOX`. |
| `--preserve-folders` | Recréer la structure des Dossiers source sous la racine. |
| `--dry-run` | Rapporter ce qui se passerait, sans rien ajouter. |
| `--no-mark-seen` | Ajouter sans le flag Seen. |

Codes de sortie :

| Code | Signification |
|---|---|
| `0` | Tout a été ajouté ou est déjà présent. |
| `1` | Au moins un message a échoué, ou l'exécution elle-même a échoué. |
| `2` | L'invocation était incorrecte : arguments erronés ou manquants, Compte inconnu, cible n'étant pas IMAP, source égale à la cible. |

Une invocation gère une Boîte aux lettres, ce qui permet de piloter une flotte de Boîtes aux lettres via un script à partir d'un CSV contenant la source, la cible et la date de coupure.

> **Exécuter plusieurs jobs à la fois.** La règle d'un seul job par cible n'existe qu'à l'intérieur d'un seul processus, donc deux invocations simultanées en ligne de commande contre la *même* Boîte aux lettres cible pourraient chacune prendre leur index de doublons avant que l'autre ne commence l'écriture, et pourraient alors toutes deux ajouter le même e-mail. Partitionnez le travail de sorte que chaque invocation simultanée ait une cible différente, ce qui est le cas normal lorsqu'une ligne de CSV correspond à une Boîte aux lettres différente.

## 🗓️ Interprétation de la date de coupure {#how-the-cutoff-is-interpreted}

- Le filtre porte sur **la date d'envoi**, et non sur le moment où un message a été archivé. L'horodatage de l'archivage est l'heure de l'exécution de l'archivage et n'a aucune signification pour une date de coupure.
- Une fenêtre relative telle que "les douze derniers mois" est **résolue en une date fixe lors de la création du job**. Un job peut s'exécuter pendant longtemps et être répété par la suite, et il doit sélectionner les mêmes e-mails à chaque fois ; une fenêtre relative stockée dériverait.
- Les dates de coupure sont interprétées dans le **fuseau horaire d'affichage configuré**, le même que celui affiché par l'écran de recherche, et la limite inférieure est inclusive à partir de 00:00 de ce jour. Une limite supérieure inclut la totalité de sa journée. Les fenêtres relatives ("les N derniers mois") sont également résolues dans ce fuseau horaire d'affichage, correspondant à la façon dont `SentDate` est stocké.

### Limitation connue {#known-limitation}

- Lorsque l'index des doublons atteint `Offload:PrefetchMaxMessages`, le périmètre est réduit et consigné dans les logs plutôt que d'être retesté sur un seul Dossier. Le repli `restrictToFolder` proposé par le constructeur d'index n'est actuellement pas utilisé par le chemin d'offload ; augmenter la limite est le remède supporté.

## 📋 Une migration, de bout en bout {#a-migration-end-to-end}

1. Créez les Boîtes aux lettres cibles. Pour un import massif, consultez l'importation CSV dans [Account Import](Account-Import.md).
2. Configurez `Offload:ExcludedSourceFolders` et `Offload:FolderRenameMap`. Les deux sont vides par défaut, donc les sauter migrera les dossiers de spam et créera des dossiers spéciaux dupliqués.
3. Confirmez que chaque Boîte aux lettres source est entièrement archivée avant de procéder à l'offload.
4. **Faites une simulation (dry run) de tout.** Vérifiez les comptes et les noms de Dossiers cibles résolus avant toute écriture.
5. Exécution réelle. Commencez avec `MaxConcurrentJobs` à 2 et augmentez-le seulement après avoir observé comment le serveur cible gère la charge ; Dovecot limite les connexions simultanées par utilisateur et par IP.
6. Répétez l'exécution pour vérifier. Ne vous attendez à rien d'ajouté, tout doit être déjà présent.
7. Désactivez les Comptes sources.

Le débit est limité par `BatchOperation:PauseBetweenEmailsMs`, qui est de 50 ms par défaut, ce qui permet au maximum vingt ajouts par seconde avant que la latence du serveur ne soit comptabilisée.

## 🔍 Signification des compteurs {#what-the-counters-mean}

| Compteur | Signification |
|---|---|
| Appended | Écrit sur la cible. Lors d'une simulation, ce qui aurait été écrit. |
| Already present | La cible le contenait déjà, par l'un ou l'autre des critères. |
| of those by fingerprint | Combien ont été reconnus par le second critère plutôt que par le Message-ID. |
| Excluded folder | Ignoré car le Dossier source est exclu. |
| Failed | Impossible d'ajouter. |

Un avertissement est signalé si l'index des doublons ne couvre pas l'ensemble de la Boîte aux lettres cible, ce qui arrive au-dessus de `PrefetchMaxMessages` ou lorsqu'un Dossier ne peut pas être lu. Dans cet état, une exécution répétée peut ajouter des e-mails qui sont déjà présents dans les parties qui n'ont pas été indexées.

## ⚠️ Notes et limites {#notes-and-limits}

- Les e-mails restaurés portent désormais leur **heure de livraison originale** comme date interne IMAP, extraite de la chaîne `Received` du message et utilisant la date d'envoi en repli. Auparavant, ils portaient l'heure de l'exécution de l'archivage, ce qui cassait le tri côté serveur par arrivée et toute règle basée sur l'âge sur la cible.
- Comme une exécution répétée ignore ce qui est déjà présent, une mauvaise date interne ne peut pas être corrigée par une réexécution : les messages sont reconnus et ignorés. Réglez cela correctement avant une exécution de production, ou supprimez les messages concernés sur la cible et relancez.
- Microsoft 365 n'est pas supporté comme cible d'offload.
- La détection des doublons ne peut reconnaître que ce qu'elle peut voir dans la Boîte aux lettres cible. Les e-mails ayant atteint la cible par un autre chemin ne sont capturés que dans la mesure où les deux critères se trouvent à correspondre.
