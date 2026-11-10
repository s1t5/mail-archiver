# ☁️ Configuration d'un Compte Microsoft Personnel (Outlook.com / M365 Family) {#personal-microsoft-account-outlookcom-m365-family-setup}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide explique comment effectuer l'Archive d'e-mails provenant d'un **compte Microsoft personnel** (Outlook.com, Hotmail, live.com ou Microsoft 365 Family) dans Mail Archiver.

Contrairement aux comptes Microsoft 365 organisationnels (qui utilisent le flux client-credentials — voir [Azure App Registration for M365](AZURE_APP_REGISTRATION_M365.md)), les comptes personnels utilisent **OAuth2 avec le Device Code Flow (RFC 8628)**. Microsoft a abandonné l'authentification Basic et les mots de passe d'application pour les comptes personnels, l'utilisation d'OAuth2 est donc requise.

Mail Archiver est livré avec un **Client ID partagé pré-enregistré** afin que la plupart des utilisateurs n'aient **pas** besoin de créer leur propre Azure App Registration. Les utilisateurs avancés peuvent toujours enregistrer leur propre application (voir l'Option B ci-dessous).

## 📚 Table des matières {#table-of-contents}

1. [Option A : Configuration standard (Recommandée)](#option-a-standard-setup-recommended)
2. [Option B : Enregistrement d'application personnalisé (Utilisateurs avancés)](#option-b-custom-app-registration-power-users)
3. [Rafraîchissement et révocation du jeton](#token-refresh--revocation)
4. [Notes importantes](#important-notes)
5. [Dépannage](#troubleshooting)

---

## 🚀 Option A : Configuration standard (Recommandée) {#option-a-standard-setup-recommended}

Utilisez cette option si votre instance Mail Archiver dispose d'un Client ID par défaut configuré (par défaut pour les images Docker officielles). Aucun accès au portail Azure n'est requis.

### 🛠️ Prérequis {#prerequisites}

- Un compte Microsoft personnel (Outlook.com, live.com ou M365 Family)
- Un appareil avec un navigateur (smartphone, ordinateur portable ou tablette) pour compléter l'autorisation

### 📝 Étapes {#steps}

1. Connectez-vous à votre application Mail Archiver.
2. Naviguez vers **Mail Accounts** > **Create**.
3. Remplissez les champs suivants :
   - **Name** : Nom descriptif pour le Compte (ex: "Personal Outlook")
   - **Email Address** : Votre adresse e-mail Microsoft personnelle (ex: `you@outlook.com`)
   - **Provider** : Sélectionnez **Microsoft Personal**
4. Aucun champ Client ID ou Client Secret n'est affiché — Mail Archiver utilise automatiquement l'enregistrement par défaut intégré.
5. Cliquez sur **Create**.
6. La page d'autorisation par code d'appareil s'ouvre automatiquement. Un code court (ex: `ABCD-EFGH`) s'affiche avec un lien vers `https://microsoft.com/devicelogin` (ou l'URL de vérification affichée).
7. Sur **n'importe quel appareil** doté d'un navigateur, ouvrez cette URL et connectez-vous avec votre compte Microsoft personnel.
8. Saisissez le code de l'étape 6.
9. Un écran de consentement apparaît, indiquant que **"Mail-Archiver"** demande l'accès IMAP. Examinez et approuvez.
10. La page Mail Archiver détecte automatiquement le succès et redirige vers la vue d'édition du Compte.
11. Terminé — la Synchronisation démarre automatiquement selon les paramètres du Compte.

> ⚠️ **Avertissement Éditeur non vérifié** : L'écran de consentement peut afficher un avertissement indiquant que "Mail-Archiver" est un éditeur non vérifié. C'est normal — la vérification de l'éditeur nécessite une identité commerciale (compte Microsoft Partner Network) et n'est pas nécessaire pour le fonctionnement du flux. L'application fonctionne normalement sans cela.

---

## 🔧 Option B : Enregistrement d'application personnalisé (Utilisateurs avancés) {#option-b-custom-app-registration-power-users}

Utilisez cette option si vous ne souhaitez pas utiliser le Client ID partagé par défaut (par exemple, pour des raisons de conformité ou si vous souhaitez un contrôle total sur l'enregistrement de l'application).

### 🛠️ Prérequis {#prerequisites}

- Un compte Microsoft (personnel ou professionnel/scolaire) pouvant accéder au portail Azure
- Une connaissance de base du portail Azure/Entra

### 🚀 Créer l'enregistrement d'application {#create-app-registration}

1. Naviguez vers le [Microsoft Entra Admin Center](https://entra.microsoft.com) ou le [Azure Portal](https://portal.azure.com).
2. Connectez-vous avec votre compte Microsoft.
3. Dans le volet de navigation de gauche, sélectionnez **App registrations** (sous Identity > Applications).
4. Cliquez sur **+ New registration**.
5. Remplissez les détails suivants :
   - **Name** : Entrez un nom descriptif (ex: "Mail Archiver MSA")
   - **Supported account types** : Sélectionnez **Accounts in any organizational directory and personal Microsoft accounts**
     - Il s'agit de l'option multi-tenant qui prend en charge à la fois les comptes personnels et organisationnels.
   - **Redirect URI** : Laissez ce champ vide (non nécessaire pour le Device Code Flow).
6. Cliquez sur **Register**.
7. Notez l'**Application (client) ID** sur la page **Overview**.

### 🔐 Activer les flux de client public {#enable-public-client-flows}

1. Dans votre enregistrement d'application, naviguez vers **Authentication** dans le menu de gauche.
2. En bas, trouvez **Allow public client flows** et réglez-le sur **Yes**.
3. Cliquez sur **Save**.

> ℹ️ Aucun client secret, aucune URI de redirection et aucune autorisation d'API ne doivent être configurés manuellement — le Device Code Flow utilise des autorisations déléguées qui sont demandées lors de l'exécution via le scope `IMAP.AccessAsUser.All`.

### 📧 Configurer Mail Archiver {#configure-mail-archiver}

Remplacer la valeur par défaut globale (affecte tous les comptes MSA)

Définissez le Client ID dans votre variable d'environnement :


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 Rafraîchissement et révocation du jeton {#token-refresh-revocation}

### Rafraîchissement automatique du jeton {#automatic-token-refresh}

Après l'autorisation initiale, Mail Archiver reçoit :
- Un **access token** (court, ~1 heure)
- Un **refresh token** (long, utilisé pour obtenir de nouveaux access tokens automatiquement)

Mail Archiver rafraîchit automatiquement l'access token avant chaque exécution de Synchronisation. Vous n'avez pas besoin de ré-autoriser, sauf si le refresh token expire ou est révoqué.

### Révoquer l'accès {#revoking-access}

Pour révoquer l'accès de Mail Archiver à votre compte Microsoft personnel :

1. Visitez [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage)
2. Connectez-vous avec votre compte Microsoft personnel.
3. Trouvez **"Mail-Archiver"** (ou le nom de votre application personnalisée) dans la liste.
4. Cliquez sur **Remove** pour révoquer l'accès.

Après la révocation, Mail Archiver ne pourra plus accéder au Compte jusqu'à ce que vous l'autorisiez à nouveau.

---

## ⚠️ Notes importantes {#important-notes}
- **Client ID partagé** : Lors de l'utilisation du Client ID par défaut, tous les utilisateurs de Mail Archiver sur toutes les instances partagent le même Azure App Registration. Le mainteneur de Mail Archiver est responsable du maintien de l'enregistrement actif. Si Microsoft désactive l'enregistrement partagé, tous les utilisateurs devront passer à un enregistrement personnalisé (Option B) jusqu'à ce qu'une nouvelle version inclue un Client ID mis à jour.


- **Comptes organisationnels** : Le fournisseur Microsoft Personal utilise le point de terminaison d'autorité `/common` et prend en charge les comptes personnels et organisationnels (professionnels/scolaires). Pour les comptes organisationnels nécessitant un accès client-credentials (application uniquement, sans connexion utilisateur), utilisez plutôt le fournisseur **M365**.

---

## 🔍 Dépannage {#troubleshooting}

### Dossiers visibles sur outlook.com mais manquants dans Mail Archiver {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**Symptôme** : Certains Dossiers existent dans la Boîte aux lettres (visibles sur outlook.com ou dans Outlook), mais n'apparaissent jamais dans Mail Archiver — ni dans la liste des Dossiers sur la page des paramètres du Compte, ni pendant la Synchronisation. Aucun message d'erreur n'est enregistré.

**Cause (côté serveur)** : Les Dossiers qui ont été créés par des **clients tiers via EWS** — ce qui configure les comptes outlook.com comme des comptes Exchange plutôt qu'IMAP — peuvent se retrouver avec une propriété `PR_CONTAINER_CLASS` incorrecte (ex: `IPF.Imap` au lieu de `IPF.Note`). Outlook.com **filtre alors complètement ces dossiers de la réponse IMAP `LIST`**, même s'ils sont entièrement visibles dans l'interface web. Il s'agit d'un comportement côté Microsoft, et non d'un bug de Mail Archiver — le même phénomène est documenté pour l'API Microsoft Graph, où les dossiers avec une classe de conteneur non-`IPF.Note` manquent des listes `mailFolders` jusqu'à ce que la propriété soit corrigée :

Mail Archiver interroge la liste des Dossiers à partir de trois sources (`LIST` récursif, parcours par niveau de chaque enfant de Dossier, et `LSUB`) et fusionne les résultats. Lorsque le serveur ne signale pas un dossier via l'une d'entre elles, aucun client IMAP ne peut le voir.

**Solution de contournement** : Modifiez les Dossiers concernés une fois via l'interface web d'outlook.com pour que Microsoft corrige la classe du conteneur :

1. Ouvrez <https://outlook.com> et localisez un Dossier manquant.
2. Renommez-le (n'importe quel nom temporaire) puis renommez-le à nouveau — ou créez un nouveau Dossier et déplacez les e-mails.
3. Répétez l'opération pour chaque Dossier concerné.
4. Déclenchez une **Full Resync** du Compte depuis la page Account Details dans Mail Archiver. Une resynchronisation complète est nécessaire car les synchronisations incrémentielles ne récupèrent que les e-mails plus récents que la dernière date de Synchronisation du Compte — les e-mails dans des dossiers qui n'ont jamais été découverts resteraient autrement non archivés.


*Ce guide est à jour en 2026. Microsoft met régulièrement à jour ses services et son interface utilisateur, certaines étapes peuvent donc différer. Reportez-vous à la [documentation de la plateforme d'identité Microsoft](https://learn.microsoft.com/en-us/entra/identity-platform/) pour les derniers détails.*
