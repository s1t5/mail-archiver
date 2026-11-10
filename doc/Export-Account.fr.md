# 📤 Exporting a Mail Account (EML / MBox) {#exporting-a-mail-account-eml-mbox}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

L'exportation du Compte écrit **chaque message d'un Compte** dans un fichier ZIP téléchargeable. C'est le pendant de l'
[web import](Import-EML-MBox.md) : vous obtenez une Archive portable du Compte dans un
format que n'importe quel client de messagerie ou outil de migration peut lire.

Deux formats sont proposés :

| Format | Ce que vous obtenez | Utilisation typique |
|--------|--------------|-------------|
| **EML** | Fichiers `.eml` individuels, organisés en Dossiers | Réimportation dans un client de messagerie, traitement par message, lisibilité à long terme |
| **MBox** | Fichiers de Boîte aux lettres traditionnels — **un fichier par Dossier** | Outils et clients lisant le MBox, traitement de texte en masse |

Dans les deux cas, les messages sont écrits sous forme de messages RFC-822 bruts, donc les **Pièces jointes
sont incluses** dans l'exportation.

> 💡 **Tip**: Pour exporter seulement une sélection de messages au lieu d'un Compte entier,
> utilisez les actions groupées dans [Mail search](Search.md#-acting-on-many-results-at-once).

## 🧭 Access {#access}

1. Ouvrez **Mail Accounts** dans le menu principal.
2. Cliquez sur le bouton **⋮** (plus d'actions) sur la ligne du Compte et choisissez
   **Export**. La même option se trouve sur la page **Details** du Compte.

L'exportation nécessite le rôle **admin** ou **self manager** — les utilisateurs standards voient
l'option mais ne peuvent pas l'utiliser. L'exportation couvre toujours l'ensemble du Compte ; il n'y a
pas de filtre par date ou par Dossier.

## 🖥️ Using the Export Page {#using-the-export-page}

La page affiche le **total**, le nombre de messages **entrants** et **sortants** du Compte,
afin que vous puissiez voir la taille de la tâche avant de la lancer. Ensuite :

1. Choisissez le format — **EML** ou **MBox**.
2. Appuyez sur **Start Export**.

L'exportation s'exécute **en arrière-plan**. Vous êtes redirigé vers une page de statut affichant l'ID
de la tâche, le format choisi, les horodatages et le nombre de messages traités/totaux.

### Job statuses {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

Lorsque la tâche atteint l'état **Completed**, un **bouton de téléchargement** apparaît sur la page de statut.
La même tâche est également répertoriée sur la page [Jobs](BackgroundJobs.md).

> ⚠️ **Les fichiers d'exportation sont nettoyés automatiquement.** Le ZIP est supprimé **après un
> téléchargement réussi**, et en tout cas **après 7 jours**. Téléchargez le fichier tant qu'il
> est encore proposé — l'Archive elle-même n'est pas affectée, vous pouvez simplement lancer
> une nouvelle exportation plus tard.

## ⚠️ Things to watch out for {#things-to-watch-out-for}

- **Les gros Comptes prennent du temps.** Une grosse Boîte aux lettres peut nécessiter plusieurs minutes et le
  ZIP peut devenir très volumineux, selon le volume de messages et les Pièces jointes.
  Comme la tâche s'exécute en arrière-plan, vous pouvez fermer le navigateur et revenir
  sur la page de statut plus tard.
- **Espace disque serveur.** Le ZIP est assemblé sur le serveur avant que vous ne le téléchargiez,
  assurez-vous donc qu'il y a assez d'espace libre à côté de l'Archive.
- **"Export file not found"** — la tâche n'est pas encore terminée, ou le fichier a
  déjà été téléchargé ou supprimé par le nettoyage de 7 jours. Lancez une nouvelle exportation.
- **Pas d'options de filtre** — l'exportation est "tout ou rien" par Compte. Utilisez l'exportation
  de la page de recherche pour un sous-ensemble.
- **Pas une exportation de conformité.** Pour un transfert de support de données prêt pour un audit avec un
  fichier d'index, utilisez plutôt l'[Audit Data Export](AuditExport.md) — il produit un
  paquet au format de données tabulaires de masse attendu par les outils d'audit.

## 🔗 Related Documentation {#related-documentation}

- [Importing Mail in the Web UI](Import-EML-MBox.md) — la direction opposée
- [Mail search](Search.md) — exporter une sélection de messages
- [Background Jobs](BackgroundJobs.md) — liste des tâches, progression et annulation
- [Audit Data Export](AuditExport.md) — paquets d'exportation prêts pour l'audit
- [CLI commands](CLI.md) — importer depuis un répertoire monté
