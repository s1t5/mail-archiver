# 🔐 Authentification à deux facteurs {#two-factor-authentication}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

L'authentification à deux facteurs (2FA) ajoute une deuxième étape au processus de connexion : après votre mot de passe, Mail Archiver demande un **code à 6 chiffres** généré par une application d'authentification sur votre téléphone. Un mot de passe volé seul ne suffit plus pour accéder à l'Archive.

Mail Archiver implémente la 2FA en utilisant le **TOTP** (Time-based One-Time Password, RFC 6238) — le même standard utilisé par Google Authenticator, Microsoft Authenticator, Authy, 1Password, Bitwarden et d'autres. Aucun service externe n'est impliqué ; le secret ne quitte jamais votre instance.

**La 2FA est optionnelle par utilisateur.** Elle est configurée par chaque utilisateur pour son propre Compte, et non de manière globale par un administrateur.

## 🛠️ Prérequis {#prerequisites}

- Une application d'authentification compatible TOTP sur votre téléphone ou ordinateur
- Accès à votre propre Compte (vous devez être connecté)
- **Un Compte (mot de passe) local** — voir la note sur OIDC ci-dessous

> **⚠️ Non disponible pour les utilisateurs OIDC.** Si vous vous connectez via un fournisseur d'identité externe (Entra ID, Authelia, Keycloak, …), la 2FA est gérée par ce fournisseur et la page de configuration est bloquée avec le message *"OIDC users cannot enable 2FA. Authentication is managed by your OIDC provider."* Activez l'authentification multi-facteurs dans votre fournisseur d'identité à la place — voir [OIDC / SSO](OIDC_Implementation.md).

## 🚀 Activer l'authentification à deux facteurs {#enabling-two-factor-authentication}

### Étape 1 — Ouvrir la page de configuration {#step-1-open-the-setup-page}

1. Connectez-vous à Mail Archiver
2. Ouvrez votre menu de Compte et choisissez **Two-Factor Authentication**
3. Vous arrivez sur **Two-Factor Setup**

### Étape 2 — Scanner le code QR {#step-2-scan-the-qr-code}

La page affiche un code QR. Ouvrez votre application d'authentification, choisissez *Ajouter un compte* → *Scanner le code QR*, et pointez la caméra vers celui-ci.

**Impossible de scanner le code ?** Utilisez la **Saisie manuelle** à la place : la page affiche le secret sous forme de texte (`If you can't scan the QR code, enter this secret manually in your authenticator app`). Saisissez-le dans votre application comme un compte basé sur le temps/TOTP.

> **ℹ️ Le secret n'est pas encore sauvegardé.** Il n'est conservé que pour la durée de la configuration dans votre session de navigateur. La 2FA ne devient active qu'après la réussite de l'étape 3 — si vous abandonnez la configuration, rien ne change sur votre Compte.

### Étape 3 — Confirmer avec un code {#step-3-confirm-with-a-code}

Saisissez le code à 6 chiffres que votre application affiche maintenant et cliquez sur **Enable Two-Factor Authentication**.

Si le code est rejeté, la page de configuration s'affiche à nouveau avec un nouveau code QR et le message *"Invalid authentication code. Please try again."*. Vérifiez que l'horloge de votre téléphone est réglée automatiquement — le TOTP dépend de l'exactitude de l'heure.

### Étape 4 — Sauvegarder vos codes de secours {#step-4-save-your-backup-codes}

En cas de succès, vous êtes redirigé vers la page **Backup Codes**. Elle affiche **10 codes**, chacun de 16 caractères.

> **⚠️ C'est la seule fois où les codes sont affichés.** Ils sont stockés sous forme de hachage — ni vous ni un administrateur ne pourrez les afficher à nouveau plus tard. Utilisez **Download Backup Codes** pour les sauvegarder dans un fichier, ou copiez-les dans votre gestionnaire de mots de passe avant de quitter la page.

Le téléchargement contient ce rappel :

> *Mail Archiver - Two-Factor Authentication Backup Codes. Store these backup codes in a safe place. You can use them to log in if you lose access to your authenticator app. Each backup code can only be used once. After using a code, it will be invalidated.*
> *Important: Keep these codes secure and do not share them with anyone.*

## 🔑 Connexion avec la 2FA {#logging-in-with-2fa}

Après avoir saisi votre mot de passe, vous êtes redirigé vers **Two-Factor Verification** et on vous demande un code :

> *Enter a 6-digit TOTP code from your authenticator app or a 16-character backup code*

Les deux fonctionnent dans le même champ — Mail Archiver les distingue par leur longueur :

| Entrée | Longueur | Comportement |
|---|---|---|
| Code de votre application d'authentification | 6 chiffres | Vérifié par rapport à la fenêtre temporelle actuelle |
| Code de secours | 16 caractères | Vérifié par rapport à vos codes stockés, puis **invalidé** |

Pour accepter de légères différences d'horloge, les codes du pas de temps immédiatement précédent et suivant sont également acceptés.

**"Remember me"** est repris du formulaire de connexion : si vous l'avez coché, vous restez connecté après les redémarrages du navigateur une fois l'étape 2FA terminée. La sélection est conservée dans la session côté serveur pendant que l'étape 2FA est en attente, elle ne survit donc que si vous saisissez le code dans le délai d'inactivité de **60 minutes** de la session. Les détails et les limites (durée de vie absolue de 7 jours, aucun effet pour les utilisateurs OIDC) se trouvent dans [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).

### Utiliser un code de secours {#using-a-backup-code}

1. Saisissez l'un de vos codes de 16 caractères sauvegardés dans le même champ
2. Le code est accepté et **immédiatement invalidé** — il ne peut pas être réutilisé
3. Les connexions réussies avec un code de secours sont enregistrées dans le journal d'accès avec la note `(2FA Backup Code)`, afin que vous puissiez repérer une utilisation inhabituelle plus tard

### Limitation du débit (Rate limiting) {#rate-limiting}

Pour ralentir les tentatives de force brute, la vérification 2FA est limitée à **5 tentatives par 15 minutes**, comptées par adresse IP et par nom d'utilisateur. Après cela, vous verrez :

> **Too Many Attempts** — *You have made too many 2FA verification attempts. Please wait and try again later.*

Attendez la fin de la fenêtre de temps et réessayez. La limite s'applique aux tentatives échouées *et* réussies au sein de la fenêtre.

## 🚫 Désactiver l'authentification à deux facteurs {#disabling-two-factor-authentication}

1. Ouvrez **Two-Factor Authentication** dans votre menu de Compte
2. La page demande une confirmation : *"Are you sure you want to disable two-factor authentication? This will reduce the security of your account."*
3. Saisissez votre **mot de passe** — cette étape nécessite votre mot de passe actuel, et non un code 2FA
4. Cliquez sur **Disable Two-Factor Authentication**

La désactivation supprime le secret et invalide tous les codes de secours restants. Votre prochaine connexion ne nécessitera que le mot de passe.

## 🔄 Perte de votre application d'authentification ? Deux solutions {#lost-your-authenticator-two-ways-back-in}

### Si vous avez encore des codes de secours {#if-you-still-have-backup-codes}

Connectez-vous avec un code de secours comme décrit ci-dessus, puis désactivez et réactivez la 2FA avec votre nouvel appareil. N'oubliez pas de sauvegarder les nouveaux codes de secours.

### Si vous n'avez plus de codes de secours {#if-you-have-no-backup-codes-left}

Un **administrateur** doit réinitialiser la 2FA pour votre Compte :

1. L'administrateur ouvre **Users**
2. Modifie l'utilisateur concerné
3. Choisit **Reset Two-Factor Authentication** et confirme :
   *"Are you sure you want to reset two-factor authentication for this user? They will need to set it up again."*
4. L'utilisateur peut alors se connecter avec son mot de passe uniquement et configurer à nouveau la 2FA

La réinitialisation efface le secret et les codes de secours. Le message de confirmation *"Two-factor authentication has been successfully reset for user …"* apparaît dans la liste des Utilisateurs.

> **ℹ️ Portée de la réinitialisation.** La réinitialisation ne couvre que la 2FA. Ce n'est **pas** une réinitialisation de mot de passe — consultez [Emergency Account Recovery](EmergencyAccountRecovery.md) si le problème est le mot de passe, ou si le Compte est entièrement bloqué.

## 👁️ Ce que les administrateurs peuvent voir {#what-administrators-can-see}

L'aperçu des **Users** affiche une colonne 2FA :

- *"Two-factor authentication is currently enabled for this user."* — La 2FA est active
- Sinon, la 2FA est désactivée pour cet utilisateur

Les administrateurs peuvent réinitialiser la 2FA (ci-dessus) mais **ne peuvent pas** lire le secret ou les codes de secours — les deux sont stockés sous forme de hachage. Un utilisateur qui perd son appareil sans codes de secours n'a aucun moyen de revenir en dehors de la réinitialisation par l'admin.

## 🔍 Piste d'audit {#audit-trail}

L'activité 2FA est écrite dans le journal et, pour les connexions réussies, dans le journal d'accès :

| Événement | Où | Entrée |
|---|---|---|
| Connexion via application d'authentification | Journal d'accès | `Login` avec la note `IP: <address> (2FA)` |
| Connexion via code de secours | Journal d'accès | `Login` avec la note `IP: <address> (2FA Backup Code)` |
| Échec de tentative 2FA | Journal d'application | Warning avec username, IP et token masqué |
| Code de secours invalide | Journal d'application | Warning avec username et IP |

L'entrée du journal d'accès rend l'utilisation des codes de secours visible après coup — utile pour détecter un code utilisé par quelqu'un qui ne devrait pas l'avoir. Consultez [Access Logging](Logs.md) pour savoir comment lire ces entrées.

## ⚙️ Fonctionnement interne {#how-it-works-under-the-hood}

Pour les administrateurs et les réviseurs :

- **Standard** : TOTP (RFC 6238), secret aléatoire de 20 octets, encodage base32
- **Fenêtre de vérification** : ±1 pas de temps, pour tolérer la dérive de l'horloge
- **Codes de secours** : 10 codes, 16 caractères chacun, générés à partir d'un RNG cryptographique, stockés sous forme de **hachage**, invalidés individuellement lors de l'utilisation
- **Stockage du secret** : dans l'enregistrement de l'utilisateur ; ne retourne jamais au navigateur après la configuration
- **Gestion de session** : la session est régénérée avant que l'identité 2FA ne soit stockée, de sorte qu'un cookie de session implanté avant la connexion ne puisse pas être transporté dans la session authentifiée
- **Comptes OIDC** : la gestion de la 2FA est bloquée ; le fournisseur est faisant autorité
- **API keys** : contournent la 2FA par conception, car les jetons non interactifs ne peuvent pas répondre à un défi — voir la justification de sécurité dans le [REST API guide](API.md)

## ❓ Dépannage {#troubleshooting}

| Symptôme | Cause | Solution |
|---|---|---|
| *"Invalid authentication code"* | Horloge du téléphone décalée, ou mauvais compte dans l'application | Activez la synchronisation automatique de l'heure ; vérifiez que l'application affiche l'entrée Mail Archiver |
| *"Two-factor secret is missing. Please restart the setup process."* | La page de configuration est restée ouverte trop longtemps, session expirée | Recommencez la configuration depuis le menu de Compte |
| *"Your session has expired. Please log in again."* | Trop de temps s'est écoulé sur la page de vérification | Connectez-vous à nouveau |
| *"Two-factor authentication is not enabled for this account."* | La 2FA a été réinitialisée ou n'a jamais été terminée | Configurez-la à nouveau, ou demandez à un administrateur |
| Code de secours rejeté | Code déjà utilisé, ou caractère mal lu | Les codes de secours ne fonctionnent qu'**une seule fois** ; utilisez-en un autre, ou demandez une réinitialisation 2FA |
| *"Too Many Attempts"* | Plus de 5 tentatives de vérification en 15 minutes | Attendez la fin de la fenêtre de temps |
| Pas de page de configuration dans le menu de Compte | Vous vous connectez via OIDC | Activez la MFA chez votre fournisseur d'identité |
| Pas de colonne 2FA dans la liste des Utilisateurs | Vous n'êtes pas un administrateur | Demandez à un administrateur |

## 📚 Documentation associée {#related-documentation}

- [User Management and Mailbox Permissions](UserManagement.md) — création de comptes, droits d'administration
- [OIDC / SSO Authentication](OIDC_Implementation.md) — connexion via des fournisseurs externes
- [Emergency Account Recovery](EmergencyAccountRecovery.md) — comptes bloqués
- [Access Logging](Logs.md) — lecture de la piste d'audit
- [REST API](API.md) — pourquoi les API keys contournent la 2FA
