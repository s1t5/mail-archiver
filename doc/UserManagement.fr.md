# 👥 Gestion des utilisateurs et Autorisations de Boîte aux lettres {#user-management-and-mailbox-permissions}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide fournit des instructions détaillées pour la création de nouveaux comptes utilisateurs et l'attribution des autorisations de Boîte aux lettres dans l'application Mail Archiver. Il couvre également le changement de votre propre mot de passe et le changement forcé de mot de passe sur une instance fraîchement installée.


## 🛠️ Prérequis {#prerequisites}

- Accès administratif à l'application Mail Archiver
- Comptes de messagerie existants configurés dans le système

## 🧑‍💻 Création d'un nouveau compte utilisateur {#creating-a-new-user-account}

1. Connectez-vous à l'application Mail Archiver avec un compte disposant de privilèges administratifs
2. Naviguez vers la section "Users" depuis le menu principal
3. Cliquez sur le bouton "Create New User"
4. Remplissez les informations utilisateur requises :
   - **Username** : Entrez un nom d'utilisateur unique pour le nouvel utilisateur
   - **Password** : Entrez un mot de passe sécurisé pour l'utilisateur
   - **Email** : Adresse e-mail de l'utilisateur
   - **Admin** : Cochez cette case si l'utilisateur doit disposer de privilèges administratifs
   - **Self Manager** : Cochez cette case si l'utilisateur doit pouvoir gérer ses propres comptes de messagerie (modifier les comptes existants et en ajouter de nouveaux, ainsi que supprimer des comptes)
   - **active** : Doit être coché pour permettre les connexions de l'utilisateur
5. Cliquez sur "Create User" pour sauvegarder le nouveau compte utilisateur

## 🔐 Attribution des autorisations de Boîte aux lettres aux utilisateurs {#assigning-mailbox-permissions-to-users}

1. Après avoir créé l'utilisateur, ou lors de la modification d'un utilisateur existant, naviguez vers la section "Users" depuis le menu principal
2. Trouvez l'utilisateur dans la liste auquel vous souhaitez attribuer des comptes de messagerie et cliquez sur le bouton "Assign" pour cet utilisateur
3. Dans la page "Assign Mail Accounts", vous verrez tous les comptes de messagerie disponibles et des cases à cocher indiquant s'ils sont attribués à l'utilisateur
5. Pour attribuer un nouveau compte de messagerie, cochez la case correspondante au compte spécifique
6. Pour retirer un compte de messagerie à l'utilisateur, décochez la case
7. Cliquez sur "Save Assignments" pour appliquer les changements

## 👤 Autorisations des comptes utilisateurs {#user-account-permissions}

### Utilisateurs Admin {#admin-users}
Les utilisateurs Admin ont un accès complet à l'application et peuvent :
- Gérer tous les comptes utilisateurs et leurs autorisations
- Accéder à tous les comptes de messagerie du système
- Visualiser tous les journaux d'accès
- Effectuer toutes les tâches administratives

### Utilisateurs Self Manager {#self-manager-users}
Les utilisateurs Self Manager peuvent :
- Ajouter de nouveaux comptes de messagerie à leur propre compte
- Modifier les comptes de messagerie existants qui leur sont attribués
- Supprimer les comptes de messagerie qui leur sont attribués
- Accéder et gérer les e-mails de leurs comptes attribués (y compris la suppression)
- Visualiser leurs propres journaux d'accès

### Utilisateurs Standard {#standard-users}
Les utilisateurs Standard ont un accès limité et peuvent :
- Visualiser les e-mails archivés des comptes de messagerie attribués
- Rechercher au sein de leurs comptes de messagerie attribués
- Exporter des e-mails de leurs comptes attribués
- Restaurer des e-mails de leurs comptes attribués
- Accéder aux pièces jointes des e-mails de leurs comptes attribués

## 🔑 Changer votre propre mot de passe {#changing-your-own-password}

Chaque utilisateur disposant d'un mot de passe local peut le changer sans administrateur :

1. Cliquez sur votre **username** dans le coin supérieur droit pour ouvrir le menu utilisateur.
2. Choisissez **Change Password**.
3. Entrez votre **current password** et le **new password** deux fois, puis validez.

Le nouveau mot de passe doit respecter les exigences ci-dessous, et il doit être **différent du mot de passe actuel**.

> ℹ️ **Les utilisateurs OIDC ne peuvent pas changer leur mot de passe ici.** La page répond :
> *"OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* Changez le mot de passe auprès de votre fournisseur d'identité à la place — voir [OIDC / SSO](OIDC_Implementation.md).

### Exigences relatives au mot de passe {#password-requirements}

- au moins **10 caractères**
- au moins une lettre **majuscule**
- au moins une lettre **minuscule**
- au moins un **chiffre**
- au moins un **caractère spécial**

Le formulaire affiche ces éléments sous forme de liste de contrôle en direct pendant que vous tapez. Les mêmes règles s'appliquent lorsqu'un administrateur crée un utilisateur ou réinitialise un mot de passe.

## ⚠️ Changement de mot de passe forcé (Configuration initiale) {#forced-password-change-initial-setup}

Une instance fraîchement installée démarre avec les identifiants de l'administrateur de la configuration (`Authentication__Username` / `Authentication__Password`). Tant que ce mot de passe par défaut est encore utilisé **et qu'aucun compte de messagerie n'existe encore**, la première connexion force un changement :

- Une bannière d'avertissement explique pourquoi : *"For security reasons, you must change your
  password before continuing. This is required because you are using the default
  system credentials on a newly set up system."*
- Vous ne pouvez pas quitter la page : *"You must change your password before
  you can continue using the system."*
- Le nouveau mot de passe doit être différent du mot de passe actuel.
- Après la sauvegarde, le flag est effacé et vous êtes redirigé vers le tableau de bord.

Une fois que vous avez changé le mot de passe par défaut, le changement forcé n'apparaît plus jamais.

## 🕒 Rester connecté — la case à cocher "Remember Me" {#staying-signed-in-the-remember-me-checkbox}

Le formulaire de connexion comporte une case à cocher nommée **Remember Me** (*angemeldet bleiben* dans l'interface allemande). Elle ne fait qu'une seule chose : elle décide si le cookie d'authentification est un cookie de session ou un cookie persistant.

| | Case décochée | Case cochée |
|---|---|---|
| Durée de vie du cookie | `Authentication__SessionTimeoutMinutes` (par défaut **60 minutes**) | **7 jours** |
| Type de cookie | Cookie de session — supprimé à la fermeture du navigateur | Cookie persistant — stocké sur le disque |
| Redémarrage du navigateur | Vous devez vous reconnecter | Vous restez connecté |
| Délai d'inactivité | Étendu à chaque requête (`SlidingExpiration`) | Étendu à chaque requête, jusqu'à la limite absolue de 7 jours |
| Déconnexion | Termine la session immédiatement | Termine la session immédiatement |

Ainsi, la case à cocher transforme "connecté pour cette session de navigateur, maximum 60 minutes
d'inactivité" en "connecté jusqu'à 7 jours, incluant les redémarrages du navigateur et de la machine".

### Ce que la case à cocher ne fait pas {#things-the-checkbox-does-not-do}

- **Elle ne mémorise pas votre mot de passe ou votre nom d'utilisateur.** Le formulaire de connexion n'est pas
  pré-rempli lors de la visite suivante ; seul le cookie de session est conservé.
- **Les 7 jours sont une limite stricte et ne peuvent pas être configurés.** La valeur est fixée
  dans le code (`Auth/Services/CookieAuthenticationService.cs`). Seule la durée de vie non persistante suit `Authentication__SessionTimeoutMinutes`.
- **Ce n'est pas un minuteur d'inactivité.** Les 7 jours sont comptés à partir de la connexion ; une fois
  ce délai passé, une nouvelle connexion est requise même si vous avez utilisé l'Archive il y a une minute.
- **Elle ne survit pas à une déconnexion.** Cliquer sur *Logout* supprime le cookie
  quelle que soit l'état de la case à cocher.

### Interaction avec la 2FA {#interaction-with-2fa}

Avec l'authentification à deux facteurs activée, la case à cocher est mémorisée pendant l'étape
2FA et n'est appliquée qu'après la réussite du second facteur. Le transfert 2FA
réside dans la session côté serveur, qui possède son propre **délai d'inactivité de 60 minutes** : si le code n'est pas saisi dans ce délai, la connexion est annulée et la sélection de la case à cocher est perdue avec elle.

### Interaction avec la session de l'application {#interaction-with-the-application-session}

Mail Archiver conserve une seconde session distincte côté serveur pour l'état éphémère — le transfert 2FA, la sélection de la restauration par lot et le dernier état de recherche. Cette session a un **délai d'inactivité fixe de 60 minutes** et n'est *pas* persistante. Après un redémarrage du navigateur avec un cookie "Remember Me" valide, vous êtes connecté, mais toute opération en attente stockée dans cette session repart de zéro.

### Interaction avec OIDC / SSO {#interaction-with-oidc-sso}

Les utilisateurs qui se connectent via un fournisseur d'identité externe n'obtiennent jamais le
cookie persistant : le callback OIDC démarre toujours la session avec
`persistAuthentication: false`. Pour ces comptes, la case à cocher n'a **aucun
effet** — la durée d'une session est décidée par les paramètres de session du fournisseur d'identité.

> ⚠️ **Note de sécurité.** Sur une machine partagée ou non surveillée, laissez la case à cocher
> décochée — un cookie persistant garde l'Archive accessible pendant 7 jours
> sans mot de passe. Dans les environnements où cela est inacceptable, réduisez
> `Authentication__SessionTimeoutMinutes` et exigez que les utilisateurs n'utilisent pas la
> case à cocher, ou utilisez OIDC où le fournisseur impose sa propre politique de session.

## 🔒 Considérations de sécurité {#security-considerations}

1. Utilisez des mots de passe forts pour tous les comptes utilisateurs
2. Changez le mot de passe administrateur par défaut immédiatement après l'installation
3. N'attribuez l'Autorisation Admin qu'aux utilisateurs qui en ont besoin
4. N'attribuez l'Autorisation Self Manager qu'aux utilisateurs qui doivent gérer leurs propres comptes
5. Révisez régulièrement les autorisations des utilisateurs pour vous assurer que l'accès est approprié
6. Supprimez l'accès des utilisateurs lorsqu'il n'est plus nécessaire
7. Utilisez le principe du moindre privilège — n'attribuez l'accès qu'aux comptes de messagerie dont les utilisateurs ont besoin
