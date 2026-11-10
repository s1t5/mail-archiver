# ☁️ Guide d'enregistrement d'application Azure et de politique de Conservation pour Mail Archiver {#azure-app-registration-and-retention-policy-guide-for-mail-archiver}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide fournit des instructions détaillées pour configurer l'enregistrement d'une application Azure pour la récupération d'e-mails et la configuration des politiques de conservation dans l'application Mail Archiver.

## 📚 Table des matières {#table-of-contents}

1. [Aperçu](#overview)
2. [Enregistrement d'application Azure pour la récupération d'e-mails](#azure-app-registration-for-email-retrieval)
   - [Prérequis](#prerequisites)
   - [Créer un enregistrement d'application](#create-app-registration)
   - [Définir les autorisations d'API requises](#set-required-api-permissions)
   - [Générer un secret client](#generate-client-secret)
3. [Configurer le Compte M365 de Mail Archiver](#configure-mail-archiver-m365-account)
4. [Configuration de la politique de Conservation](#retention-policy-setup)
   - [Configurer la politique de Conservation](#configure-retention-policy)

## 🌐 Aperçu {#overview}

L'application Mail Archiver prend en charge les comptes Microsoft 365 (M365) en utilisant le flux d'identifiants client OAuth2 pour la récupération d'e-mails. De plus, elle offre une fonctionnalité de politique de conservation pour supprimer automatiquement les e-mails du serveur de messagerie après une période spécifiée, aidant ainsi à gérer l'espace de stockage tout en maintenant une Archive complète.

## ☁️ Enregistrement d'application Azure pour la récupération d'e-mails {#azure-app-registration-for-email-retrieval}

### 🛠️ Prérequis {#prerequisites}

- Accès administratif à Microsoft Entra ID (Azure AD)
- Un tenant Microsoft 365 avec des licences Exchange Online

### 🚀 Créer un enregistrement d'application {#create-app-registration}

1. Accédez au [Microsoft Entra Admin Center](https://entra.microsoft.com)
2. Connectez-vous avec votre compte administrateur
3. Dans le volet de navigation de gauche, sélectionnez **App registrations**
4. Cliquez sur **+ New registration** en haut de la page des enregistrements d'applications
5. Remplissez les détails suivants :
   - **Name** : Entrez un nom descriptif (ex: "Mail Archiver M365 Provider")
   - **Supported account types** : Sélectionnez **Accounts in this organizational directory only** (Single tenant)
     - Ceci est requis pour le flux d'identifiants client utilisé par Mail Archiver
   - **Redirect URI** : Laissez ce champ vide (pas nécessaire pour le flux d'identifiants client)
6. Cliquez sur **Register**

**Important** : Notez les valeurs suivantes de la page **Overview** :
- **Application (client) ID** - Vous en aurez besoin en tant que `ClientId` dans Mail Archiver
- **Directory (tenant) ID** - Vous en aurez besoin en tant que `TenantId` dans Mail Archiver

### 🔐 Définir les autorisations d'API requises {#set-required-api-permissions}

1. Naviguez vers **API permissions** dans le menu de gauche
2. Cliquez sur **+ Add a permission**
3. Sélectionnez **Microsoft Graph**
4. Choisissez **Application permissions** (requis pour le flux d'identifiants client de Mail Archiver)
5. Ajoutez les autorisations suivantes :
   - **Mail.Read** - Lire les e-mails dans toutes les Boîtes aux lettres
   - **Mail.ReadWrite** - Lire et écrire des e-mails dans toutes les Boîtes aux lettres (pour la fonction de Restauration ainsi que la suppression)
   - **User.Read.All** - Lire les profils complets de tous les utilisateurs (requis pour l'importation de Boîtes aux lettres du tenant Microsoft 365 ; fournit également `assignedPlans` pour le filtrage de la licence Exchange)

6. Cliquez sur **Add permissions**
7. **CRITIQUE** : Cliquez sur **Grant admin consent for [Your Organization]**
8. Confirmez en cliquant sur **Yes**

**Note** : Les autorisations d'application sont nécessaires car Mail Archiver utilise le flux d'identifiants client pour accéder aux Boîtes aux lettres sans interaction de l'utilisateur.

### 🔑 Générer un secret client {#generate-client-secret}

1. Naviguez vers **Certificates & secrets** dans le menu de gauche
2. Sous **Client secrets**, cliquez sur **+ New client secret**
3. Fournissez une description (ex: "Mail Archiver Secret")
4. Sélectionnez une période d'expiration
5. Cliquez sur **Add**
6. **Important** : Copiez la **Value** immédiatement et stockez-la en toute sécurité. Ce secret ne sera plus affiché par la suite.

## 📧 Configurer le Compte M365 de Mail Archiver {#configure-mail-archiver-m365-account}

Après avoir terminé l'enregistrement de l'application, vous devez configurer un compte de messagerie M365 dans Mail Archiver avec les valeurs suivantes :

### 📋 Valeurs requises de l'enregistrement d'application : {#required-values-from-app-registration}

1. **Client ID** : Copiez l'**Application (client) ID** de la page **Overview** de l'enregistrement d'application
2. **Client Secret** : Copiez la **Value** que vous avez sauvegardée lors de la création du secret client
3. **Tenant ID** : Copiez le **Directory (tenant) ID** de la page **Overview** de l'enregistrement d'application

### 🚀 Créer un compte M365 dans Mail Archiver : {#creating-m365-account-in-mail-archiver}

1. Connectez-vous à votre application Mail Archiver
2. Naviguez vers **Mail Accounts** > **Create**
3. Remplissez les champs suivants :
   - **Name** : Nom descriptif pour le compte (ex: "Sales Team M365")
   - **Email Address** : L'adresse e-mail de la Boîte aux lettres cible à archiver
   - **Provider** : Sélectionnez **M365**
   - **Client ID** : Entrez l'Application (client) ID de votre enregistrement d'application
   - **Client Secret** : Entrez la valeur du secret client que vous avez sauvegardée
   - **Tenant ID** : Entrez le Directory (tenant) ID de votre enregistrement d'application

4. Cliquez sur **Create**

### ⚠️ Notes importantes : {#important-notes}

- Le champ **Email Address** doit contenir la Boîte aux lettres réelle que vous souhaitez archiver lors de la création d'un compte M365 unique.
- L'enregistrement d'application doit avoir les autorisations pour accéder à la Boîte aux lettres spécifiée.
- Pour créer des comptes pour toutes les Boîtes aux lettres ou une sélection de Boîtes aux lettres du tenant en une seule fois, utilisez le guide [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md).


## 🗑️ Configuration de la politique de Conservation {#retention-policy-setup}

L'application Mail Archiver fournit une fonctionnalité de politique de Conservation pour supprimer automatiquement les e-mails du serveur IMAP après qu'ils ont été archivés avec succès, aidant ainsi à gérer l'espace de stockage sur le serveur IMAP tout en maintenant une Archive complète.

### 🛠️ Configurer la politique de Conservation {#configure-retention-policy}

1. Naviguez vers **Mail Accounts** dans l'application Mail Archiver
2. Créez un nouveau compte M365 ou modifiez un compte existant
3. Dans le formulaire de configuration du compte, localisez le champ **Delete After Days**
4. Entrez le nombre de jours après lesquels les e-mails archivés doivent être supprimés du serveur :
   - Par exemple, entrez `30` pour supprimer les e-mails après 30 jours
   - Laissez vide pour désactiver la suppression automatique
5. Sauvegardez la configuration du compte


## ℹ️ Informations concernant les Boîtes aux lettres migrées {#ℹ-information-regarding-migrated-mailboxes}

### 📁 Dossiers non visibles après la migration {#folders-not-visible-after-migration}

Si vous avez migré votre Boîte aux lettres d'un autre fournisseur de messagerie vers Microsoft 365, certains dossiers peuvent ne pas apparaître dans la liste des dossiers.

#### 🔍 Contexte {#background}

Mail Archiver récupère les informations de dossier des Boîtes aux lettres M365 via l'API Microsoft Graph. L'application demande correctement tous les dossiers disponibles (y compris les dossiers cachés) et parcourt toute la hiérarchie des dossiers.

Cependant, l'API Microsoft Graph ne renvoie que les dossiers avec `PR_CONTAINER_CLASS` défini sur `IPF.Note`. Lors des migrations de Boîtes aux lettres depuis d'autres systèmes de messagerie, les dossiers peuvent se voir assigner une classe de conteneur différente telle que `IPF.Imap`. Ces dossiers ne sont pas exposés via les points de terminaison de liste de dossiers de l'API Graph.

Ce comportement affecte toutes les applications utilisant cette API pour la récupération de dossiers.

#### 🔧 Options de contournement {#workaround-options}

**Option 1 : Utiliser l'outil MFCMAPI (Recommandé)**

1. Téléchargez [MFCMAPI](https://github.com/Microsoft/mfcmapi/) depuis le dépôt officiel de Microsoft
2. Lancez MFCMAPI et connectez-vous à votre Boîte aux lettres
3. Naviguez vers chaque dossier manquant
4. Modifiez la propriété `PR_CONTAINER_CLASS` de `IPF.Imap` à `IPF.Note`
5. Redémarrez Outlook et rechargez les dossiers dans Mail Archiver

**Option 2 : Recréer les dossiers dans Outlook**

1. Créez de nouveaux dossiers directement dans Outlook (ceux-ci auront la classe `IPF.Note` correcte)
2. Déplacez tous les e-mails des dossiers migrés vers les nouveaux dossiers
3. Supprimez les anciens dossiers migrés si vous le souhaitez

**Option 3 : Script PowerShell (Pour les changements groupés)**

Pour les Boîtes aux lettres contenant de nombreux dossiers migrés, vous pouvez utiliser PowerShell avec le module Exchange Online pour changer la classe de conteneur pour plusieurs dossiers à la fois. Contactez votre administrateur Exchange pour obtenir de l'aide concernant cette approche.


---

**Note** : Ce guide est à jour en date de 2025. Microsoft met régulièrement ses services à jour, certains éléments de l'interface utilisateur peuvent donc différer. Référez-vous toujours à la dernière documentation de Microsoft pour obtenir les informations les plus récentes.
