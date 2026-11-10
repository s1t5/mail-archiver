# Import de Boîte aux lettres de locataire Microsoft 365 {#microsoft-365-tenant-mailbox-import}

[← Retour à l'index de la documentation](index.md)

## Aperçu {#overview}

Mail Archiver peut créer des comptes de messagerie Microsoft 365 pour plus d'une Boîte aux lettres dans le même locataire à partir d'un seul formulaire de création. Ceci est utile lors de l'intégration d'un client ou d'une organisation où toutes les Boîtes aux lettres Microsoft 365, ou un sous-ensemble choisi, doivent être archivées.

L'importation de locataire utilise les identifiants d'application Microsoft Graph configurés dans le formulaire et crée un compte de messagerie Mail Archiver par Boîte aux lettres importée.

## Prérequis {#prerequisites}

Avant d'utiliser l'importation de locataire, terminez la configuration de l'enregistrement de l'application Microsoft 365 décrite dans le [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md).

Vous avez besoin de ces valeurs provenant de Microsoft Entra ID :

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

L'enregistrement de l'application doit disposer des **autorisations d'application** Microsoft Graph qui permettent à Mail Archiver de lire les Boîtes aux lettres du locataire. En plus des autorisations requises pour l'archivage régulier de Compte M365, l'autorisation suivante est requise pour l'importation de locataire :

- **User.Read.All** – requis pour lister les utilisateurs du locataire et leurs adresses de Boîte aux lettres.

L'importation de locataire est sélectionnée comme fournisseur **Microsoft 365 (tenant)** dans le formulaire de création.

### Filtrage {#filtering}

L'importation de locataire et la Gestion du locataire excluent automatiquement :
- **Guest accounts** (`userType == "Guest"`) — utilisateurs externes invités sans leurs propres Boîtes aux lettres dans le locataire.
- **Users without an Exchange Online license** — seuls les utilisateurs avec un plan de service Exchange actif dans leur `assignedPlans` sont listés. Cela évite d'importer des comptes qui ne peuvent pas être archivés.

## Modes d'importation {#import-modes}

L'importation de locataire prend en charge deux modes :

### Importer toutes les Boîtes aux lettres listées {#import-all-listed-mailboxes}

Utilisez ce mode lorsque chaque Boîte aux lettres renvoyée par Microsoft Graph doit être créée en tant que compte Mail Archiver.

- Mail Archiver charge la liste des Boîtes aux lettres du locataire depuis Microsoft Graph.
- Les comptes de messagerie M365 déjà existants sont ignorés.
- Si **Skip disabled mailboxes** est activé, les utilisateurs désactivés ne sont pas inclus dans la liste des Boîtes aux lettres.

### Importer uniquement les Boîtes aux lettres sélectionnées {#import-selected-mailboxes-only}

Utilisez ce mode lorsque seules des Boîtes aux lettres spécifiques dans le locataire doivent être archivées.

- Chargez d'abord la liste des Boîtes aux lettres du locataire.
- Désactivez **Import all listed mailboxes**.
- Sélectionnez les Boîtes aux lettres qui doivent être importées.
- Soumettez le formulaire pour créer des comptes uniquement pour les Boîtes aux lettres sélectionnées.

## Utilisation étape par étape {#step-by-step-usage}

1. Connectez-vous à Mail Archiver en tant qu'utilisateur pouvant créer des comptes de messagerie.
2. Ouvrez **Mail Accounts** > **Create**.
3. Sélectionnez **Microsoft 365 (tenant)** comme fournisseur.
4. Saisissez les identifiants Microsoft 365 :
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. Saisissez le préfixe **Account name** qui doit être utilisé pour les comptes générés.
6. Choisissez si les Boîtes aux lettres désactivées doivent être ignorées.
7. Cliquez sur **Load mailboxes** pour lister les Boîtes aux lettres du locataire.
8. Choisissez le comportement d'importation souhaité :
   - Laissez **Import all listed mailboxes** activé pour importer toutes les Boîtes aux lettres listées.
   - Désactivez-le et sélectionnez des Boîtes aux lettres individuelles pour importer uniquement des Boîtes aux lettres spécifiques.
9. Cliquez sur **Save**.

Après la création, Mail Archiver redirige vers la liste des comptes de messagerie et indique combien de comptes de locataire ont été importés et combien de comptes existants ont été ignorés.

## Nommage des comptes {#account-naming}

Pour les importations de locataire, Mail Archiver utilise la valeur saisie dans **Account name** comme préfixe pour chaque compte de Boîte aux lettres créé.

Exemple :

- Nom de compte saisi : `Test GmbH`
- Boîte aux lettres du locataire : `mailbox@example.com`
- Nom de compte créé : `Test GmbH - <mailbox@example.com>`

Cela facilite l'identification des comptes importés appartenant au même locataire ou client.

## Comptes existants {#existing-accounts}

Si une Boîte aux lettres existe déjà en tant que compte Microsoft 365 dans Mail Archiver, l'importation de locataire l'ignore au lieu de créer un doublon. La détection des comptes existants est basée sur l'adresse e-mail de la Boîte aux lettres et le fournisseur M365.

La liste des Boîtes aux lettres peut également indiquer si une Boîte aux lettres existe déjà avant de soumettre l'importation.

## Boîtes aux lettres désactivées {#disabled-mailboxes}

La case à cocher **Skip disabled mailboxes** contrôle si les utilisateurs désactivés du locataire doivent être exclus de la liste des Boîtes aux lettres.

- Activée : seuls les utilisateurs activés du locataire sont listés et importés.
- Désactivée : les utilisateurs désactivés du locataire peuvent apparaître dans la liste et peuvent être importés s'ils sont sélectionnés ou si toutes les Boîtes aux lettres listées sont importées.

## Gestion du locataire {#tenant-management}

En plus de l'importation de locataire via le formulaire de création, Mail Archiver propose une page dédiée **Tenant Management** pour les administrateurs. Elle est accessible depuis l'index des Mail Accounts via le bouton **Tenant Management** (icône de bâtiment) à côté de n'importe quel compte Microsoft 365.

La Gestion du locataire est utile pour ajouter des Boîtes aux lettres qui n'ont pas été importées initialement — par exemple, lorsque de nouvelles Boîtes aux lettres sont créées dans le locataire après l'intégration initiale, ou lorsque des comptes ont été ignorés au départ.

### Différences avec l'importation de locataire du formulaire de création {#how-it-differs-from-the-create-form-tenant-import}

- Les identifiants Microsoft 365 (Client ID, Client Secret, Tenant ID) ne sont **pas saisis à nouveau**. Ils sont récupérés à partir du compte source sur lequel le bouton a été cliqué.
- La liste des Boîtes aux lettres du locataire est chargée automatiquement à l'ouverture de la page (pas de bouton "Load mailboxes").
- Les Boîtes aux lettres déjà importées sont affichées avec un badge "already exists" et ne peuvent pas être sélectionnées à nouveau.
- Seuls les administrateurs peuvent accéder à la Gestion du locataire.

### Utilisation étape par étape {#step-by-step-usage}

1. Connectez-vous à Mail Archiver en tant qu'administrateur.
2. Ouvrez **Mail Accounts**.
3. Localisez le compte Microsoft 365 dont vous souhaitez gérer le locataire et cliquez sur le bouton **Tenant Management** à côté de celui-ci.
4. La page se charge et liste toutes les Boîtes aux lettres renvoyées par Microsoft Graph pour le locataire du compte source.
   - Les Boîtes aux lettres qui existent déjà en tant que comptes M365 dans Mail Archiver sont marquées d'un badge "already exists" et ne peuvent pas être sélectionnées.
   - Les utilisateurs désactivés du locataire ne sont pas listés ; la Gestion du locataire ignore toujours les Boîtes aux lettres désactivées.
5. Sélectionnez les Boîtes aux lettres que vous souhaitez ajouter.
6. Saisissez le préfixe **Account name** qui doit être utilisé pour les nouveaux comptes (le schéma de nommage est `{prefix} - <{email}>`, identique à l'importation de locataire du formulaire de création).
7. Configurez optionnellement **Delete After Days** et **Local Retention Days** pour les nouveaux comptes.
8. Cliquez sur **Add selected mailboxes**.

Après la création, Mail Archiver redirige vers la liste des comptes de messagerie et indique combien de Boîtes aux lettres ont été ajoutées.

### Rechercher des Boîtes aux lettres {#searching-mailboxes}

Pour les grands locataires possédant plusieurs milliers de Boîtes aux lettres, un champ de recherche au-dessus de la liste des Boîtes aux lettres vous permet de filtrer par nom d'affichage ou par adresse e-mail. Le filtre ne tient pas compte de la casse et s'applique instantanément pendant la saisie. **Select all** et **Clear selection** ne s'appliquent qu'aux Boîtes aux lettres actuellement visibles (filtrées), le filtrage est donc un moyen sûr de sélectionner un sous-ensemble spécifique sans affecter les entrées masquées.

### Renommer les comptes existants {#renaming-existing-accounts}

La case à cocher **Rename existing accounts** applique le schéma de nommage `{prefix} - <{email}>` à tous les comptes M365 qui partagent la même enregistrement d'application (Client ID et Tenant ID) que le compte source. Ceci est utile pour normaliser les noms de comptes après une importation initiale où les noms étaient incohérents.

Lorsqu'elle est cochée, l'enregistrement du formulaire renomme chaque compte correspondant — y compris le compte source — que de nouvelles Boîtes aux lettres soient sélectionnées pour l'importation ou non. Vous pouvez utiliser cette option seule (sans sélectionner de nouvelles Boîtes aux lettres) pour normaliser les noms sans ajouter de comptes.

### Limite de Boîte aux lettres {#mailbox-limit}

Une seule opération de Gestion du locataire peut ajouter au maximum `TenantManagement__MaxSelectedMailboxes` Boîtes aux lettres à la fois (par défaut `1000`). Cela protège contre les importations massives accidentelles et une charge excessive sur Microsoft Graph / la base de données. Si vous devez importer plus de Boîtes aux lettres, exécutez l'opération plusieurs fois. Consultez le [Setup Guide](Setup.md#-tenant-management-settings) pour savoir comment ajuster cette limite.

### Autorisations requises {#permissions-required}

La Gestion du locataire utilise les mêmes autorisations d'application Microsoft Graph que l'importation de locataire du formulaire de création. L'enregistrement d'application du compte source doit avoir :

- Les autorisations requises pour l'archivage régulier de compte M365, **et**
- **User.Read.All** — requis pour lister les utilisateurs du locataire et leurs adresses de Boîte aux lettres.

Consultez le [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md) pour la configuration initiale.

### Journal d'audit {#audit-logging}

Chaque vue de page de Gestion du locataire et chaque opération d'ajout est enregistrée dans le journal d'accès, y compris le nombre de Boîtes aux lettres listées ou ajoutées et le compte source utilisé.
