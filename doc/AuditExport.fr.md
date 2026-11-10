# 📤 Export de données d'audit {#audit-data-export}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

L'export de données d'audit crée des paquets de données massives sous forme de tableaux (ZIP avec `INDEX.XML` + tables CSV + index DTD) à partir de l'Archive existante. Ces paquets suivent la structure d'index établie pour la remise de supports de données et peuvent être importés directement par les outils d'audit et d'analyse externes courants qui prennent en charge ce format d'index.

La fonctionnalité est disponible en tant que **page dédiée accessible depuis la page Logs** et est restreinte aux utilisateurs administrateurs. Chaque exécution d'export est enregistrée dans le journal d'accès, l'historique de l'export est donc protégé contre les révisions.

## 🧭 Accès {#access}

1. Connectez-vous en tant qu'utilisateur administrateur
2. Ouvrez la page **Logs**
3. Cliquez sur le bouton **Audit Data Export** dans le coin supérieur droit

Les utilisateurs non-administrateurs ne voient pas le bouton et ne peuvent pas appeler les points de terminaison d'export (le serveur les rejette).

## 📦 Structure du paquet d'export {#export-package-structure}

Le fichier ZIP généré se nomme `audit-export-<timestamp>.zip` et contient :

| Fichier | Contenu |
|---|---|
| `INDEX.XML` | Fichier d'index décrivant le fournisseur de données, le support et toutes les définitions de table/colonne |
| `index.dtd` | Le DTD auquel le fichier d'index fait référence ; requis par les outils d'importation pour la validation |
| `emails.csv` | Une ligne par e-mail archivé dans la période/Boîte aux lettres sélectionnée |
| `attachments.csv` | Optionnel ; une ligne par Pièce jointe des e-mails exportés |

### Table `emails.csv` {#table-emailscsv}

Colonnes (pas de ligne d'en-tête ; les noms de colonnes proviennent de `INDEX.XML`, comme d'habitude pour ce format) :

| Colonne | Description |
|---|---|
| Id | ID interne de l'e-mail (numérique) |
| MessageId | En-tête Message-ID de l'e-mail |
| SentDate | Date d'envoi, ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`) |
| ReceivedDate | Date de réception, ISO 8601 UTC |
| IsOutgoing | `1` = sortant, `0` = entrant |
| From | Expéditeur |
| To | Destinataires |
| Cc | Destinataires en CC |
| Bcc | Destinataires en BCC |
| Subject | Objet |
| FolderName | Dossier dans lequel l'e-mail est archivé |
| HasAttachments | `1` = avec pièces jointes, `0` = sans |
| AttachmentCount | Nombre de pièces jointes (numérique) |
| AccountEmail | Adresse e-mail de la Boîte aux lettres d'archivage |

### Table `attachments.csv` {#table-attachmentscsv}

| Colonne | Description |
|---|---|
| EmailId | ID interne de l'e-mail auquel appartient la pièce jointe (clé de jointure vers `emails.csv`) |
| FileName | Nom du fichier de la pièce jointe |
| ContentType | Type MIME |
| Size | Taille en octets (numérique) |
| Sha256 | Hash SHA-256 du contenu (provenant du stockage de pièces jointes adressé par contenu) |

### Conventions CSV {#csv-conventions}

- Encodage : UTF-8 **sans** BOM (déclaré comme `UTF8` dans `INDEX.XML`)
- Délimiteur de colonne : `;`
- Encapsulateur de texte : `"` (doublé pour l'échappement)
- Délimiteur d'enregistrement : `\n` (explicitement déclaré dans `INDEX.XML`)
- Dates : ISO 8601, heures converties du fuseau horaire d'affichage configuré vers l'UTC
- Pas de ligne d'en-tête ; pas de nombres localisés (les colonnes numériques contiennent des entiers simples)

## 🖥️ Utilisation de la page d'export {#using-the-export-page}

### Champs du formulaire {#form-fields}

1. **From / To date** - Période à exporter (requis, étendue maximale configurable, par défaut 10 ans)
2. **Mailbox** - Restreindre l'export à une seule Boîte aux lettres ou exporter toutes les Boîtes aux lettres
3. **Attachment metadata** - Lorsque coché, `attachments.csv` est ajouté au paquet
4. **Data supplier name / location / comment** - Écrit dans le bloc `DataSupplier` de `INDEX.XML` ; pré-rempli à partir de la section de configuration `AuditExport` et modifiable par export

### Cycle de vie de la tâche {#job-lifecycle}

1. Cliquer sur **Start Export** valide la période et la Boîte aux lettres, crée une tâche et revient à la page
2. La tâche s'exécute en arrière-plan (les exports mis en file d'attente sont traités les uns après les autres) ; le tableau des tâches affiche la progression en direct (e-mails traités/totaux avec une barre de progression) via un polling automatique
3. Lorsque l'export est terminé, le bouton **Download** apparaît sur la ligne de la tâche
4. Les tâches en cours ou en file d'attente peuvent être **annulées**

### Historique de l'export {#export-history}

Le tableau d'historique sous le formulaire liste les exports les plus récents avec l'horodatage, l'utilisateur, la période, la Boîte aux lettres, le nombre d'e-mails, le statut et la taille du fichier. L'historique est **conservé dans la base de données** (table `AuditExportJobs`), il survit donc aux redémarrages de l'application et est protégé contre les révisions.

Les fichiers de téléchargement sont conservés pendant une période de Conservation configurable (par défaut 30 jours, voir `AuditExport__RetentionDays`) ; après cela, les fichiers ZIP sont supprimés par le nettoyage quotidien tandis que les lignes d'historique subsistent.

> 📝 **Note** : Les fichiers d'export sont stockés à l'intérieur du conteneur de l'application. Si vous devez les conserver à long terme, téléchargez-les et stockez-les dans votre répertoire de preuves d'audit.

## 🔐 Audit {#auditing}

Chaque export produit deux entrées de journal d'accès du nouveau type **Audit Data Export** :

1. **Entrée de début** - lorsque l'export est mis en file d'attente (avec l'ID de tâche, la période, la Boîte aux lettres, les options en JSON)
2. **Entrée de résultat** - lorsque l'export se termine, échoue ou est annulé (avec l'ID de tâche, la période, la Boîte aux lettres, le nombre d'e-mails, le résultat et la taille du paquet en JSON)

Les entrées apparaissent sur la page Logs comme toutes les autres entrées du journal d'accès et peuvent être filtrées par le nouveau type. Le menu déroulant de filtre par type les récupère automatiquement.

## ⚙️ Configuration {#configuration}

La section `AuditExport` dans `appsettings.json` (les variables d'environnement utilisent la syntaxe avec double tiret bas, ex: `AuditExport__RetentionDays`) :

| Paramètre | Description | Par défaut |
|---|---|---|
| `DataSupplierName` | Valeur par défaut pour le champ de formulaire du nom du fournisseur de données | vide |
| `DataSupplierLocation` | Valeur par défaut pour le champ de formulaire de l'emplacement du fournisseur de données | vide |
| `Comment` | Valeur par défaut pour le champ de formulaire de commentaire | vide |
| `OutputDirectory` | Répertoire pour les fichiers ZIP générés (les chemins relatifs se résolvent par rapport à la racine du contenu de l'app) | `exports/audit` |
| `RetentionDays` | Jours avant que les fichiers d'export terminés ne soient supprimés | `30` |
| `MaxRangeYears` | Étendue maximale autorisée entre la date de début et de fin de période | `10` |

## 🗄️ Base de données {#database}

L'historique de l'export est stocké dans la table `mail_archiver."AuditExportJobs"` (créée par la migration `MigrateV2609_1`, appliquée automatiquement au démarrage). Aucune table existante n'est modifiée par cette fonctionnalité.

## 🔍 Dépannage {#troubleshooting}

| Problème | Cause / Solution |
|---|---|
| Bouton de téléchargement manquant après redémarrage | Les fichiers d'export résident dans le conteneur ; si le conteneur a été recréé ou si la période de Conservation est dépassée, le ZIP a disparu tandis que la ligne d'historique subsiste. Relancez l'export. |
| "La période ne doit pas dépasser X années" | Réduisez la période ou augmentez `AuditExport__MaxRangeYears`. |
| La tâche reste en *Running* pendant longtemps | Les Archives volumineuses sont transmises par lots de 1000 e-mails ; la progression est mise à jour continuellement. Les périodes très larges peuvent prendre du temps — vérifiez le compteur traités/totaux. |
| L'outil d'importation signale des problèmes d'encodage | Le paquet est en UTF-8 sans BOM avec le flag `UTF8` déclaré et un délimiteur d'enregistrement explicite ; assurez-vous que l'outil lit le `INDEX.XML` à côté des fichiers de données (extrayez tout le ZIP, n'ouvrez pas les fichiers individuellement depuis l'intérieur du ZIP). |

## 🔗 Documentation associée {#related-documentation}

- [Access Logging](Logs.md)
- [Installation, Setup and Parameters](Setup.md)
