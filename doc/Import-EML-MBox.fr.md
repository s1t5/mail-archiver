# 📥 Importing Mail in the Web UI (EML / MBox) {#importing-mail-in-the-web-ui-eml-mbox}

[← Back to Documentation Index](index.md)

## 📋 Overview {#overview}

En plus de l'[interface en ligne de commande](CLI.md) et de l'[import de Compte CSV](Account-Import.md), Mail Archiver peut importer une Archive de mail existante **via l'interface web**. Il existe deux formulaires de téléchargement :

| Formulaire | Fichier accepté | Résultat |
|------|---------------|--------|
| **Import MBox** | `.mbox`, `.mbx` | Chaque message du fichier est importé dans **un seul** Dossier de votre choix |
| **Import EML** | `.zip` contenant des fichiers `.eml` | La **structure des Dossiers à l'intérieur du ZIP** détermine l'emplacement de chaque message |

Les deux formulaires archivent les messages dans un Compte Mail Archiver existant, les deux s'exécutent en tant que **tâches de fond** (background jobs), et les deux **ignorent automatiquement les doublons** — importer le même fichier deux fois ne crée pas de seconde copie.

> 💡 **Astuce** : Si votre Archive est déjà sur le serveur sous forme de fichier, préférez le
> [CLI](CLI.md). Cela évite le téléchargement via le navigateur et est le
> meilleur choix pour les Archives de plusieurs gigaoctets.

## 🧭 Access {#access}

1. Ouvrez **Mail Accounts** dans le menu principal.
2. Utilisez le menu déroulant **Import** au-dessus de la liste des comptes et choisissez
   **Import MBox** ou **Import EML**.

Les formulaires sont disponibles pour les **admins** (tous les comptes) et les **self managers**
(les comptes qui leur sont assignés) ; voir
[User Management](UserManagement.md#-user-account-permissions). Seuls les comptes **activés**
sont proposés comme cibles d'importation — un compte désactivé ne peut pas être sélectionné.

## 📨 Import MBox {#import-mbox}

MBox est le format utilisé par la plupart des clients mail pour les dossiers exportés, et par les Sauvegardes de type `mbox`.

| Champ | Signification |
|-------|---------|
| **MBox File** | Le fichier `.mbox` / `.mbx` à télécharger |
| **Target Account** | Le Compte Mail Archiver qui reçoit les messages |
| **Target Folder** | Le Dossier dans lequel les messages sont importés. Choisissez-en un dans la liste des Dossiers existants du compte, ou tapez un nouveau nom — un Dossier qui n'existe pas encore est **créé automatiquement** pendant l'importation. Par défaut : `INBOX`. |

Tous les messages du fichier sont importés dans ce dossier cible unique. Le fichier MBox
lui-même ne contient aucune information de Dossier, la structure de la Boîte aux lettres
originale n'est donc pas reconstruite — si vous avez besoin de cela, utilisez la méthode EML/ZIP.

## 🗂️ Import EML {#import-eml}

L'import EML attend une **Archive ZIP** dont les entrées sont des fichiers `.eml`.

- Les fichiers `.eml` à la **racine** du ZIP sont importés dans `INBOX`.
- Les fichiers `.eml` dans un **sous-dossier** sont importés dans un Dossier portant le **même
  nom** que ce sous-dossier.

Ainsi, un ZIP organisé comme ceci :

```
mailbox.zip
├── 2024-invoice.eml            → INBOX
├── Archive/
│   └── old-message.eml         → Archive
└── Projects/
    └── 2026/
        └── kickoff.eml         → Projects/2026
```

restaure l'arborescence des Dossiers ainsi que les messages. Les fichiers `.eml` individuels peuvent être
téléchargés en les encapsulant d'abord dans un ZIP (`zip emails.zip *.eml`).

## ⚙️ Shared behaviour {#shared-behaviour}

Les deux formulaires se comportent de la même manière une fois que vous appuyez sur **Start Import** :

- **Limite de taille de fichier** — configurée avec `Upload__MaxFileSizeGB` (par défaut **10 GB**).
  Le formulaire affiche la limite actuelle à côté du sélecteur de fichier.
- **Traitement en arrière-plan** — le téléchargement est mis en file d'attente comme une tâche ; vous n'avez pas besoin de
  garder le navigateur ouvert.
- **Gestion des doublons** — les messages qui sont déjà dans l'Archive sont ignorés
  et comptabilisés séparément. Les doublons sont identifiés via l'en-tête `Message-ID`.
- **Entrée malformée** — les fichiers illisibles sont ignorés individuellement et signalés sur
  la page de statut au lieu d'interrompre toute l'importation.
- **Progression** — vous êtes redirigé directement vers la page de statut de la tâche, qui affiche l'ID
  de la tâche, le nom du fichier, la taille du fichier, le statut, le nombre de messages traités/totaux et les
  comptages en cours. La même tâche apparaît également sur la page [Jobs](BackgroundJobs.md).
- **Annulation** — une importation en cours peut être annulée depuis sa page de statut. Les
  messages importés jusqu'à ce point restent dans l'Archive.
- **Piste d'audit** — l'importation est inscrite dans le [access log](Logs.md), et la
  valeur de stockage par Compte est actualisée ensuite (voir
  [Per-Account Storage](AccountStorage.md)).

### Job statuses {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled`

Une tâche terminée indique combien de messages ont été importés, combien ont échoué et combien
ont été ignorés comme doublons — par exemple
*"1420 imported, 3 failed, 87 duplicates"*. Lors d'une importation réussie, le fichier téléchargé
est supprimé du serveur ; les entrées ayant échoué sont conservées pendant la durée de vie de la
tâche afin que vous puissiez voir ce qui a été rejeté.

## ⚠️ Things to watch out for {#things-to-watch-out-for}

- **Mauvais type de fichier** — le téléchargement est rejeté à moins que l'extension ne soit l'une des
  `.mbox`, `.eml` ou `.zip`. Le sélecteur du formulaire EML n'accepte que `.zip`, celui du formulaire MBox
  uniquement `.mbox` et `.mbx`.
- **"0 imported, N duplicates"** — ce n'est pas une erreur. Les messages sont déjà
  dans l'Archive, très probablement suite à une importation ou une Synchronisation antérieure de la même source.
- **Archives très volumineuses** — un fichier MBox est lu message par message, donc un
  fichier de plusieurs gigaoctets prend du temps, même en arrière-plan. Surveillez la progression sur
  la page de statut plutôt que de relancer un téléchargement.
- **Espace disque libre** — le fichier téléchargé est stocké temporairement sur le serveur et
  nécessite de l'espace à côté de votre base de données d'Archive.
- **Noms de Dossiers avec caractères spéciaux** — les noms de Dossiers sont extraits des
  chemins ZIP ou de votre saisie ; vérifiez la liste des Dossiers du Compte après l'importation si vous
  avez utilisé des caractères inhabituels.

## 🔗 Related Documentation {#related-documentation}

- [CLI commands](CLI.md) — importer depuis un répertoire monté
- [Account Import (CSV)](Account-Import.md) — ajouter de nombreux comptes à la fois
- [Background Jobs](BackgroundJobs.md) — surveiller et annuler les tâches d'importation
- [Exporting a Mail Account](Export-Account.md) — le sens inverse
- [Setup and Parameters](Setup.md) — paramètres `Upload__*`
