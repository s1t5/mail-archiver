# 🔍 Mail Search {#mail-search}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

La recherche est le cœur d'une Archive d'e-mails. Mail Archiver recherche le **texte intégral**
de vos e-mails archivés — objet, corps, expéditeur et destinataires — à travers tous
les comptes auxquels vous avez accès, et renvoie des résultats en quelques millisecondes, même sur
des Archives contenant des millions de messages.

Ce guide couvre l'interface de recherche, la syntaxe de requête, les filtres, le tri,
la pagination, et la marche à suivre lorsque vous devez agir sur un ensemble de résultats.

> **ℹ️ La recherche ne voit que ce que vous pouvez voir.** Les résultats sont filtrés par vos
> Autorisations de Compte. Un non-administrateur ne trouvera que des e-mails provenant des
> Boîtes aux lettres qui lui sont assignées — la restriction est appliquée dans la requête elle-même,
> pas dans l'interface.

## 🚀 Accéder à la recherche {#accessing-search}

1. Connectez-vous et choisissez **Archive** dans le menu principal
2. La barre de recherche se situe au-dessus de la liste des résultats

Ouvrir l'Archive sans terme de recherche affiche vos e-mails les plus récents, du plus récent au plus ancien. Vous pouvez naviguer à partir de là ou affiner avec les filtres.

## 🎯 L'interface de recherche {#the-search-interface}

| Champ | Fonction |
|---|---|
| **Search Term** | Requête en texte intégral — voir [Query Syntax](#-query-syntax) ci-dessous |
| **From** | Uniquement les e-mails envoyés à partir de cette date |
| **To** | Uniquement les e-mails envoyés jusqu'à cette date |
| **Direction** | `All`, `Incoming` ou `Outgoing` |
| **Account** | Restreindre à un seul compte de messagerie |
| **Folders** | Restreindre à un seul dossier via l'arborescence des dossiers |
| **Search** | Exécuter la requête |
| **Reset** | Effacer tous les filtres |

Le terme de recherche correspond à l'**objet, au corps, à l'expéditeur et aux destinataires**
(`From`, `To`, `Cc`, `Bcc`).

### L'arborescence des dossiers {#the-folder-tree}

Les dossiers sont affichés sous forme d'**arbre hiérarchique** sur la gauche, reflétant la
structure de dossiers de la Boîte aux lettres source — y compris les dossiers imbriqués. Cliquez sur un dossier pour
restreindre la recherche à celui-ci, et utilisez le contrôle de réduction/développement pour replier l'arbre
lorsque vous avez besoin d'espace à l'écran.

Sélectionner un dossier ne modifie pas votre terme de recherche ; les deux se combinent.

## 🧩 Query Syntax {#query-syntax}

La syntaxe est délibérément simple. Tout ce qui suit est ce que l'application
comprend réellement.

### Mots simples — et pourquoi les préfixes fonctionnent {#plain-words-and-why-prefixes-work}

Tapez un ou plusieurs mots ; ils doivent tous être présents (une recherche **AND**) :

```
invoice january
```

Trouve les e-mails contenant à la fois `invoice` et `january` dans les champs recherchés.

> **💡 Les mots partiels sont correspondus automatiquement.** Chaque mot est traité comme un
> **préfixe**, donc une recherche pour `isenb` trouve aussi `isenboeck` et `isenböck`, et
> `rech` trouve `Rechnung` ainsi que `Rechnungen`. Vous n'avez pas besoin de caractère générique
> — il n'y en a pas à taper. Cela signifie également qu'un terme très court peut
> renvoyer beaucoup de résultats ; voir [Performance](#-performance-and-large-archives).

La recherche n'est **pas sensible à la casse**. `Invoice`, `invoice` et `INVOICE` sont la
même requête.

### Phrases exactes — guillemets {#exact-phrases-quotes}

Entourez le texte de guillemets doubles pour exiger que les mots soient **côte à côte** dans cet
ordre :

```
"important meeting"
```

Sans guillemets, `important meeting` correspondrait aussi à un e-mail où les deux
mots apparaissent dans des phrases sans rapport. Avec des guillemets, seule la phrase adjacente
correspond.

### Recherche spécifique par champ {#field-specific-search}

Préfixez un terme par un nom de champ suivi de deux-points pour rechercher **uniquement** ce champ :

| Syntaxe | Recherche dans |
|---|---|
| `subject:invoice` | Objet uniquement |
| `from:john` | Expéditeur uniquement |
| `to:example.com` | Destinataires uniquement |
| `body:important` | Corps uniquement |

Les noms de champs ne sont pas sensibles à la casse (`Subject:` fonctionne aussi), et chacun peut être
combiné avec des guillemets pour une phrase :

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ Quatre champs sont supportés :** `subject`, `body`, `from`, `to`. Écrire
> `cc:someone` ou `bcc:someone` n'est **pas** une recherche par champ — le terme est traité
> comme du texte ordinaire et recherché dans tous les champs. Pour trouver un destinataire qui était
> uniquement en Cc, recherchez l'adresse ou le nom sans préfixe.

### Combiner le tout {#combining-everything}

Les filtres et les termes s'additionnent. Cette requête trouve les e-mails de n'importe quel expéditeur d'assurance,
avec "car insurance" comme phrase exacte dans le corps, et "invoice" dans l'
objet :

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

Combinaisons pratiques :

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### Caractères ignorés {#characters-that-are-ignored}

L'analyseur de requête supprime les caractères `& | ! ( ) : *` des termes individuels,
car ils ont une signification dans le moteur de texte intégral sous-jacent. Si votre recherche
ne renvoie rien d'inattendu, vérifiez si un caractère spécial en est la cause.

## 📊 Tri et Pagination {#sorting-and-paging}

### Tri {#sorting}

Cliquez sur l'en-tête d'une colonne pour trier. Colonnes triables :

| Colonne | Trie par |
|---|---|
| **Subject** | Ligne d'objet, par ordre alphabétique |
| **From** | Expéditeur |
| **To** | Destinataires |
| **Date** | Date d'envoi |

Cliquer à nouveau sur le même en-tête inverse la direction. Par défaut, c'est le **plus récent en premier**.

### Taille de page {#page-size}

Choisissez le nombre de résultats à afficher par page :

**20** (par défaut) · **50** · **75** · **100** · **150**

Une page plus grande affiche plus de contexte mais met plus de temps à s'afficher. Utilisez les
contrôles **Previous** / **Next** pour naviguer dans les pages de résultats ; le nombre de résultats
vous indique combien d'e-mails ont été trouvés au total.

> **ℹ️ Votre fuseau horaire s'applique aux dates affichées.** Les heures affichées sont
> converties de l'UTC vers votre fuseau horaire configuré, de sorte que la date que vous voyez correspond
> à votre calendrier local — utile lorsque vous effectuez des recherches autour de minuit.

## 👁️ Travailler avec les résultats {#working-with-results}

Chaque ligne affiche le compte, l'expéditeur, l'objet, la date, et une **icône 📎 trombone**
lorsque l'e-mail contient des Pièces jointes. Le trombone est un indicateur uniquement — ce n'est pas
un filtre.

Cliquez sur **Details** pour ouvrir un e-mail :

- Corps complet (HTML ou texte brut)
- **Pièces jointes**, listées avec l'option de télécharger chacune d'elles
- **Download as EML** — le message complet, pour une utilisation ailleurs
- **Restauration** — copier l'e-mail vers une Boîte aux lettres active

Consultez [Mailbox Migration](MailboxMigration.md) pour la restauration d'e-mails vers une Boîte aux lettres.

## ✅ Agir sur de nombreux résultats à la fois {#acting-on-many-results-at-once}

Les résultats peuvent être sélectionnés et traités en masse. Activez le **Selection Mode** pour obtenir des
cases à cocher, puis :

- **Select All** / **Deselect All** pour la page actuelle
- **Select all {n} matching** — chaque résultat de la recherche actuelle sur toutes les
  pages (affiché lorsque les résultats s'étendent sur plus d'une page)
- **Copy Selected** — restauration vers une Boîte aux lettres
- **Export Selected** — au format MBOX ou EML
- **Delete Selected** — de l'Archive

Le compteur affiche *"{selected} of {total} selected"* pendant que vous travaillez.

### La limite de sélection {#the-selection-limit}

> **⚠️ Un maximum de 250 e-mails peut être sélectionné à la fois** (configurable via
> `Selection:MaxSelectableEmails`). Au-delà, l'interface affiche *"Maximum
> selections reached (250)"* et **Too Many — Split Required**.

Cette limite existe car chaque opération doit maintenir les messages concernés. L'invite
*"Split larger operations into multiple batches"* est la solution prévue : travaillez sur les résultats par groupes de 250 ou moins.

**Select all {n} matching** respecte la même limite. Lorsque la recherche correspond à plus d'
e-mails que la limite autorisée, elle sélectionne les premiers selon l'ordre de tri actuel
et affiche l'avis *"Maximum selections reached"*. Affinez la recherche (plage de dates,
dossier, compte) pour cibler le reste.

Tenter de la dépasser affiche : *"Too many emails selected. Maximum allowed is
250 emails. Please reduce your selection or split into multiple operations."*

### Supprimer chaque correspondance d'un filtre {#deleting-every-match-of-a-filter}

Le bouton **Delete all matches** à côté de *Reset* supprime chaque e-mail qui
correspond à la recherche actuelle, pas seulement la page ou la sélection actuelle. Il est disponible pour les administrateurs et les gestionnaires et s'exécute comme une tâche de fond, donc la limite de sélection de 250 e-mails ne s'applique pas.

La boîte de dialogue de confirmation indique combien d'e-mails sont supprimables et combien sont verrouillés par la politique de Conservation. Vous devez taper ce nombre pour activer la
confirmation. Le serveur compte à nouveau et annule si l'ensemble de résultats a changé entre-temps, par exemple parce qu'une Synchronisation a ajouté des e-mails.

Les e-mails verrouillés ne sont jamais supprimés par cette action ; ils sont signalés comme ignorés dans le statut de la tâche. `DeletionPolicy:MaxBulkDeleteEmails` (par défaut 100 000) limite une
tâche. Un filtre qui correspond à plus d'e-mails est rejeté avec une demande de l'affiner.

La tâche apparaît sur la page [Background Jobs](BackgroundJobs.md) et peut être
annulée à cet endroit.

### Traitement direct vs tâche de fond {#direct-processing-vs-background-job}

Pour **Copy** et **Export**, vous choisissez comment le travail s'exécute :

| Option | Comportement |
|---|---|
| **Process on this page (wait for completion)** | Le navigateur attend ; bien pour une poignée d'e-mails |
| **Process in background (monitor separately)** | S'exécute comme une tâche ; vous pouvez fermer le navigateur |

Les exports s'exécutent toujours en arrière-plan :

> *"L'export s'exécutera en arrière-plan et vous pourrez télécharger le
> résultat depuis la page Jobs une fois terminé."*

Retrouvez les tâches en cours et terminées sur la page [Background Jobs](BackgroundJobs.md).

> **⚠️ La suppression est permanente.** La confirmation est explicite : *"Are you sure you
> want to permanently delete {n} selected email(s)? This action cannot be undone
> and each deletion will be logged for audit purposes."* Les suppressions sont enregistrées
> dans le journal d'accès.

## ⚡ Performance et grandes Archives {#performance-and-large-archives}

La recherche est basée sur un index de texte intégral PostgreSQL, donc la différence entre une
recherche rapide et une recherche lente dépend généralement de la **sélectivité** de la requête.

**À faire :**

- Ajouter une **plage de dates** lorsque vous savez approximativement quand l'e-mail est arrivé — c'est
  le moyen le plus efficace d'affiner la recherche
- Restreindre à un **Compte** ou un **Dossier** lorsque vous savez où se trouve l'e-mail
- Utiliser **plusieurs mots** au lieu d'un seul mot très court
- Utiliser les recherches par champ (`subject:`, `from:`) pour éliminer les correspondances non liées

**À éviter :**

- Un seul préfixe court tel que `re` — en tant que recherche par préfixe, il correspond à une énorme
  partie de votre Archive
- Des plages de dates très larges combinées à un mot commun
- Naviguer sur de nombreuses pages alors que vous pourriez plutôt affiner la recherche

> **💡 Si une recherche semble lente, la plage de dates est généralement la solution.** Restreindre
> à un mois transforme souvent une recherche de plusieurs secondes en une recherche instantanée, car l'
> index peut ignorer la majeure partie de l'Archive.

## ❓ Dépannage {#troubleshooting}

| Symptôme | Cause | Que faire |
|---|---|---|
| Aucun résultat, mais vous en attendiez | Terme de recherche trop spécifique, ou faute de frappe | Essayez moins de mots ; vérifiez l'orthographe |
| `cc:name` ne trouve rien d'utile | `cc` n'est pas un champ supporté | Recherchez le nom sans préfixe |
| Un caractère spécial casse la requête | `& \| ! ( ) : *` sont supprimés | Supprimez ou remplacez le caractère |
| Trop de résultats | Mot commun ou préfixe court | Ajoutez une plage de dates ou une recherche par champ |
| *"Too Many — Split Required"* | Plus de 250 e-mails sélectionnés | Réduisez la sélection ou divisez l'opération |
| Un export est manquant | Il s'exécute comme une tâche de fond | Vérifiez [Background Jobs](BackgroundJobs.md) |
| Les résultats semblent manquer | Autorisations de Compte | Confirmez que la Boîte aux lettres est assignée à votre utilisateur |
| Les dates semblent décalées | Conversion de fuseau horaire | Vérifiez votre paramètre de fuseau horaire |

## 📚 Documentation associée {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — restaurer des résultats vers une Boîte aux lettres
- [Background Jobs](BackgroundJobs.md) — surveiller les exports et les Restauration
- [Access Logging](Logs.md) — la piste d'audit des recherches et suppressions
- [REST API](API.md) — la même Archive via l'API
- [MCP Server](MCP.md) — rechercher depuis un agent IA
- [Retention Policies](RetentionPolicies.md) — quand les e-mails quittent l'Archive
