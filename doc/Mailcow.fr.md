# Archivage d'instance Mailcow {#mailcow-instance-archiving}

[← Retour à l'index de la documentation](index.md)

## Aperçu {#overview}

Mail Archiver peut archiver les Boîtes aux lettres d'une instance [Mailcow](https://mailcow.email)
de manière centralisée, sans collecter les mots de passe personnels des propriétaires des Boîtes aux lettres.

Un administrateur connecte l'instance une seule fois avec sa clé API. Mail Archiver liste
ensuite les Boîtes aux lettres et, pour chaque Boîte aux lettres sélectionnée pour l'archivage, crée un **mot de passe d'application dédié** restreint à `imap_access`. Ce mot de passe d'application est utilisé par le fournisseur IMAP existant pour archiver la Boîte aux lettres et peut être révoqué à tout moment.

Les administrateurs accèdent au gestionnaire d'instance via **Add Account → Mailcow** ou depuis le
menu d'actions d'un Compte déjà géré par Mailcow. Le gestionnaire est réservé aux administrateurs, car la clé API stockée peut gérer les identifiants de chaque Boîte aux lettres de l'instance.

## Création de comptes à partir de Mailcow {#creating-accounts-from-mailcow}

Dans **Mail Accounts → Add Account**, les administrateurs peuvent sélectionner **Mailcow** comme fournisseur.
Le formulaire propose alors les instances connectées et une action **Manage mailboxes**. La sélection de
Boîtes aux lettres crée alors des comptes d'archivage IMAP ordinaires avec des mots de passe d'application dédiés ; aucun Compte Mailcow distinct n'est stocké. Si aucune instance n'existe encore, **Manage Mailcow instances**
ouvre le gestionnaire d'instance où l'on peut en connecter une.

## Liaison d'un compte existant {#linking-an-existing-account}

Dans la vue d'édition d'un compte IMAP, les administrateurs peuvent changer le fournisseur pour **Mailcow**
et choisir une instance. Le compte conserve le fournisseur IMAP mais est adopté par la gestion
Mailcow : Mail Archiver crée un mot de passe d'application dédié, le vérifie via IMAP et
remplace le mot de passe stocké du compte. Le compte apparaît alors avec une action Mailcow dans
la liste des comptes.

Pour quitter la gestion Mailcow, sélectionnez un autre fournisseur et enregistrez. Le mot de passe d'application est
révoqué et le mot de passe local est effacé. Lors du retour à IMAP, saisissez un nouveau mot de passe
IMAP ; le passage à Microsoft 365 ou à un compte en import uniquement fournit ses propres
identifiants. Les non-administrateurs peuvent toujours modifier les paramètres sans identifiants d'un
compte géré par Mailcow, mais ne peuvent ni le lier ni le délier.

## Prérequis {#prerequisites}

### Le chiffrement des identifiants doit être activé {#credential-encryption-must-be-enabled}

Mail Archiver stocke la clé API Mailcow et chaque mot de passe d'application généré de manière chiffrée au
repos en utilisant la fonctionnalité de chiffrement des identifiants existante. Configurez

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```

avant de connecter une instance. La création d'une instance et l'activation de Boîtes aux lettres sont toutes deux
refusées tant que le chiffrement est désactivé, afin que les identifiants ne puissent jamais être écrits en clair
via cette fonctionnalité. Conservez la clé en dehors de la base de données et incluez-la dans votre Sauvegarde
sécurisée — sans elle, les mots de passe d'application stockés ne peuvent pas être déchiffrés et les Boîtes aux lettres ne peuvent pas être provisionnées. Voir [Setup](Setup.md#-security-settings) pour plus de détails.

### Accès à l'API Mailcow {#mailcow-api-access}

Créez une clé API dans Mailcow sous **Configuration → Access → API**. La clé doit être
autorisée à

- lister les Boîtes aux lettres (`get/mailbox/all`),
- lister, créer et supprimer des mots de passe d'application (`get/app-passwd/all`, `add/app-passwd`,
  `delete/app-passwd`).

Restreignez l'API à l'adresse IP de l'hôte Mail Archiver dans les paramètres API de Mailcow.
L'endpoint app-password de Mailcow nécessite que la fonctionnalité **App Passwords** soit activée pour
l'instance.

### Accès réseau {#network-access}

- **HTTPS** vers l'origine Mailcow (par exemple `https://mail.example.com`).
- **IMAP over TLS** vers l'hôte IMAP, normalement le port `993`. L'IMAP en clair n'est pas supporté
  pour le provisionnement.

## Utilisation étape par étape {#step-by-step-usage}

### Connexion d'une instance {#connecting-an-instance}

1. Connectez-vous à Mail Archiver en tant qu'administrateur.
2. Ouvrez **Mail Accounts** et cliquez sur **Mailcow instances**.
3. Cliquez sur **Connect Mailcow instance**.
4. Saisissez les détails de connexion :
   - **Account name** — un nom d'affichage pour l'instance dans Mail Archiver.
   - **Mailcow URL** — l'origine HTTPS uniquement, par exemple `https://mail.example.com`.
     Les chemins, les chaînes de requête et les identifiants intégrés sont rejetés.
   - **API key** — la clé créée dans Mailcow.
   - **IMAP host** — le nom d'hôte utilisé pour l'archivage, par exemple `mail.example.com`.
     Saisissez uniquement le nom d'hôte, sans schéma ni port.
   - **IMAP TLS port** — par défaut `993`.
5. Cliquez sur **Connect**. Mail Archiver vérifie la clé API en listant les Boîtes aux lettres avant
   d'enregistrer l'instance. En cas de succès, il ouvre la sélection des Boîtes aux lettres.

La clé API est stockée de manière chiffrée et n'est jamais réaffichée dans le formulaire ou dans un message
d'erreur. Les redirections HTTP sont désactivées pour les appels API afin que la clé ne puisse pas être transmise
à une autre origine.

### Activation des Boîtes aux lettres {#activating-mailboxes}

1. Sur la page des Boîtes aux lettres, filtrez optionnellement la liste avec le champ de recherche. Le filtre
   correspond à l'adresse et au nom d'affichage et n'est pas sensible à la casse.
2. Sélectionnez les Boîtes aux lettres à archiver. **Select all** et **Clear selection** n'affectent que
   les Boîtes aux lettres actuellement visibles (filtrées), le filtrage est donc un moyen sûr de choisir un
   sous-ensemble.
3. Cliquez sur **Activate archiving**.

Pour chaque Boîte aux lettres sélectionnée, Mail Archiver :

1. re-valide l'adresse par rapport à la liste actuelle des Boîtes aux lettres de Mailcow et ignore les
   Boîtes aux lettres désactivées,
2. crée un compte d'archive nommé `{instance name} – {mailbox display name}`,
3. génère un mot de passe d'application aléatoire nommé `MailArchiver-<unique id>` restreint à
   `imap_access`,
4. vérifie l'identifiant avec une connexion IMAP via TLS,
5. et seulement après, active la Synchronisation.

Si la vérification IMAP échoue, le compte reste désactivé et l'opération peut être rejouée.

### Badges de statut des Boîtes aux lettres {#mailbox-status-badges}

La page de sélection étiquette chaque Boîte aux lettres afin qu'il soit clair pourquoi une case à cocher est disponible ou non :

| Badge | Signification | Sélectionnable |
| --- | --- | --- |
| **Available** | Listée dans Mailcow, pas encore provisionnée | oui |
| **Active** | Provisionnée et en cours de synchronisation | non |
| **Pending** | Provisionnement commencé mais pas terminé (rejouable) | oui |
| **Revoked** | Le mot de passe d'application a été révoqué ; peut être réactivé | oui |
| **Existing** | Un compte IMAP indépendant avec cette adresse existe déjà | non |
| **Disabled** | La Boîte aux lettres est désactivée dans Mailcow | non |

### Rejouer une Boîte aux lettres en attente {#retrying-a-pending-mailbox}

L'identifiant généré et le nom unique du mot de passe d'application sont persistés **avant** que
l'API Mailcow ne soit appelée. Si une requête échoue de manière ambiguë — par exemple un timeout après que
le mot de passe d'application a été créé — la Boîte aux lettres reste visible en tant que **Pending**. Rechargez la page et
sélectionnez-la à nouveau : Mail Archiver recherche d'abord le nom de l'application et ne crée un nouveau mot de passe
d'application que si aucun n'existe. Rejouer ne crée pas de doublons d'identifiants.

Le provisionnement et la révocation pour une instance sont sérialisés à l'aide de verrous consultatifs PostgreSQL,
de sorte que deux opérations simultanées ne peuvent pas provisionner la même Boîte aux lettres deux fois.

### Révocation de l'accès {#revoking-access}

Cliquez sur **Revoke app password** à côté d'une Boîte aux lettres. Cela

- désactive la Synchronisation future,
- supprime le mot de passe d'application géré dans Mailcow,
- et efface le mot de passe local.

**Les e-mails archivés sont conservés.** Seul le mot de passe d'application géré est supprimé — les autres mots de passe
d'application appartenant à la Boîte aux lettres ne sont jamais touchés. Les opérations de synchronisation
déjà en cours peuvent se terminer avec leur connexion existante.

Si la requête distante échoue, le compte reste désactivé et la révocation peut être rejouée.

La suppression d'un compte d'archive dans **Mail Accounts** ne supprime pas silencieusement l'identifiant
distant. La liaison reste listée sous **Remaining app passwords** sur la page de l'instance afin de
pouvoir être révoquée explicitement.

### Édition d'une instance {#editing-an-instance}

Ouvrez **Mailcow instances** et cliquez sur **Edit**. Le champ de la clé API est toujours vide — la
clé stockée n'est jamais affichée. Laissez-le vide pour conserver la clé actuelle, ou saisissez-en une nouvelle
pour la remplacer. Les détails de connexion modifiés sont vérifiés par rapport à Mailcow avant l'enregistrement.

### Suppression d'une instance {#deleting-an-instance}

Ouvrez **Mailcow instances** et cliquez sur **Delete**.

La suppression d'une instance supprime sa clé API chiffrée et ses liaisons de Boîtes aux lettres. Les **Comptes
d'archive et leurs e-mails archivés sont conservés**, mais les comptes sont désactivés.

La suppression est **refusée tant qu'un mot de passe d'application géré est encore actif** dans Mailcow :
supprimer l'instance supprimerait la clé API requise pour la révoquer. Révokez d'abord tous les mots de passe
d'application gérés sur la page des Boîtes aux lettres.

## Journal d'audit {#audit-logging}

Chaque opération administrative est écrite dans le journal d'accès avec l'utilisateur agissant :

- connexion d'une instance,
- mise à jour d'une instance,
- suppression d'une instance,
- activation de Boîtes aux lettres (avec le nombre provisionné et le nombre d'échecs),
- révocation d'un mot de passe d'application.

## Limite de Boîtes aux lettres {#mailbox-limit}

Une seule exécution d'activation peut provisionner au maximum `Mailcow__MaxSelectedMailboxes` Boîtes aux lettres
(par défaut `1000`). Cela protège contre un provisionnement de masse accidentel et une charge excessive
sur l'API Mailcow et l'IMAP. Pour les instances plus grandes, lancez l'opération plusieurs fois.

Chaque Boîte aux lettres nécessite plusieurs allers-retours API plus une vérification IMAP, et elles sont
traitées séquentiellement. Provisionner des centaines de Boîtes aux lettres en une seule requête peut dépasser
les timeouts du reverse-proxy ou du navigateur ; divisez la sélection en lots.

## Notes de sécurité {#security-notes}

- La clé API reste côté serveur. Elle est envoyée en tant qu'en-tête `X-API-Key`, masquée des
  journaux, et n'est jamais renvoyée au navigateur.
- Les réponses de mot de passe d'application de Mailcow peuvent renvoyer des mots de passe en clair dans leurs champs `log`.
  Mail Archiver ne consigne délibérément pas les corps de réponse et ne fait pas remonter les erreurs
  API au navigateur ; les échecs sont signalés par des messages localisés.
- Une réponse HTTP 200 n'est pas traitée comme un succès : les mutations nécessitent le `type: success` de Mailcow par résultat.
- L'Autorisation IMAP n'est pas intrinsèquement en lecture seule. Les comptes provisionnés n'ont pas de
  politique de suppression côté serveur configurée, et les administrateurs conservent le contrôle des
  paramètres du compte. Cette fonctionnalité archive les messages, pas la configuration ou les Sauvegardes de Mailcow.

### Noms d'hôte et réseaux privés {#hostnames-and-private-networks}

L'URL Mailcow et l'hôte IMAP sont saisis par un administrateur et peuvent pointer vers
un espace d'adressage privé. Ceci est intentionnel et supporté : Mail Archiver est couramment
déployé à côté du serveur de messagerie, donc `mail.example.lan`, `192.168.10.20` ou `10.0.0.5`
sont des valeurs normales. **Aucune adresse privée, de loopback ou link-local n'est bloquée.**

La limite de confiance est le rôle d'administrateur, pas l'adresse réseau. `Mail Accounts`
est disponible pour les auto-gestionnaires, qui peuvent déjà configurer un serveur IMAP arbitraire pour
leurs propres comptes ; la section Mailcow est restreinte aux administrateurs, et un
administrateur a déjà un accès sans restriction à chaque compte d'archive de l'installation.
Restreindre l'hôte IMAP ne fermerait donc pas une limite de privilèges,
tout en brisant le déploiement sur site prévu.

Deux contraintes s'appliquent toujours :

- L'URL Mailcow doit être une origine HTTPS pure. Les chemins, les chaînes de requête, les informations utilisateur et les
  redirections sont rejetés, de sorte que la clé API ne peut pas être transmise à une autre origine.
- L'hôte IMAP doit être un nom d'hôte sans schéma ni port, et le provisionnement se connecte toujours
  avec TLS (`SslOnConnect`).

Si vous devez restreindre les hôtes que Mail Archiver peut atteindre, appliquez-le au niveau du réseau
— règles de pare-feu de sortie ou politique DNS pour le conteneur — plutôt que de compter sur la
validation des entrées dans l'application.

## Dépannage {#troubleshooting}

| Symptôme | Cause et remède |
| --- | --- |
| *Encryption required* lors de la connexion ou de l'activation | `Security__CredentialEncryptionKey` n'est pas défini ou est invalide. Configurez une clé de 32 octets encodée en base64 et redémarrez. |
| *HTTPS origin required* | L'URL contient un chemin, une chaîne de requête ou des identifiants intégrés. Saisissez l'origine pure, ex. `https://mail.example.com`. |
| *IMAP host invalid* | L'hôte IMAP doit être un nom d'hôte sans schéma ni port. |
| *Instance already connected* | Une autre instance avec la même URL existe. Modifiez cette instance à la place. |
| *API request failed (401/403)* | La clé API manque des permissions requises, ou la restriction IP n'inclut pas l'hôte Mail Archiver. |
| *API request rejected* | Mailcow a renvoyé une ligne `error`/`danger`/`warning`. Vérifiez les journaux Mailcow ; Mail Archiver ne transmet pas les corps de réponse. |
| *IMAP verification failed* | Mauvais hôte ou port IMAP, un certificat non approuvé, ou la fonctionnalité app-password est désactivée dans Mailcow. |
| *IMAP account exists* | Un compte IMAP indépendant archive déjà cette adresse. Utilisez ce compte au lieu de créer une seconde archive. |
| *Revoke previous password* | Le compte d'archive pour cette Boîte aux lettres a été supprimé. Révokez d'abord le mot de passe d'application restant, puis réactivez. |
| La Boîte aux lettres reste **Pending** | L'API Mailcow a expiré. Rechargez la page et réactivez la Boîte aux lettres ; aucun doublon de mot de passe d'application n'est créé. |
| La suppression est refusée | Des mots de passe d'application gérés sont encore actifs. Révokez-les d'abord sur la page de la Boîte aux lettres. |

## Endpoints API utilisés {#api-endpoints-used}

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## Changements de base de données {#database-changes}

La migration ajoute uniquement les tables `MailcowInstances` et `MailcowMailboxes` ainsi que
leurs index et clés étrangères. Les tables de Boîtes aux lettres existantes et les données archivées sont
conservées. La suppression d'un compte d'archive définit la `MailAccountId` de la liaison sur `NULL` afin que l
l'identifiant distant puisse toujours être révoqué par la suite.
