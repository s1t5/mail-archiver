# MCP Server (Model Context Protocol) {#mcp-server-model-context-protocol}

Mail Archiver fournit un **serveur MCP (Model Context Protocol)** optionnel qui
expose les e-mails archivés aux agents IA via le transport HTTP Streamable. Il
reflète l'API [REST](API.md) en lecture seule : mêmes données, même modèle d'
Autorisation par utilisateur, mêmes journaux d'accès — simplement packagé sous forme d'outils MCP au lieu de points de terminaison REST, afin qu'un agent LLM puisse les découvrir et les appeler directement.

L'objectif est de permettre à un agent IA de lire les e-mails archivés **sans jamais exposer
les identifiants de la Boîte aux lettres ou communiquer avec le fournisseur de messagerie.** Une clé API par utilisateur à portée limitée est une capacité de *lire l'Archive*, et non un identifiant pour *être la Boîte aux lettres* — elle n'hérite que des Autorisations de la Boîte aux lettres de son propriétaire, n'accorde aucun chemin d'écriture et peut être révoquée instantanément sans toucher au Compte de messagerie sous-jacent.

> ⚠️ **Le serveur MCP est désactivé par défaut.** Il doit être explicitement activé dans la
> configuration (voir [Enabling the MCP server](#enabling-the-mcp-server)). Lorsqu'il est
> désactivé, le point de terminaison `/mcp` renvoie `404 Not Found`.

Le serveur MCP est **conçu pour être en lecture seule** : il expose des outils pour lister les comptes,
lister les Dossiers, rechercher des messages, lire un message et télécharger une Pièce jointe. Il
ne crée, ne modifie et ne supprime jamais de données.

## Table des matières {#table-of-contents}

- [Activer le serveur MCP](#enabling-the-mcp-server)
- [Authentification](#authentication)
- [Transport et endpoint](#transport-and-endpoint)
- [Tools](#tools)
  - [list_accounts](#list_accounts)
  - [list_folders](#list_folders)
  - [search_emails](#search_emails)
  - [get_email](#get_email)
  - [get_attachment](#get_attachment)
  - [get_email_eml](#get_email_eml)
  - [get_email_raw](#get_email_raw)
- [Limitation du débit](#rate-limiting)
- [Journalisation des accès](#access-logging)
- [Connexion d'un agent](#connecting-an-agent)
- [Conseils de sécurité pour l'exposition sur Internet](#security-guidance-for-internet-exposure)

## Activer le serveur MCP {#enabling-the-mcp-server}

Le serveur MCP est configuré sous la section `Mcp` de `appsettings.json` (ou les variables d'environnement équivalentes, par exemple `Mcp__Enabled=true`) :

```json
"Mcp": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "DefaultPageSize": 20,
  "MaxResults": 100,
  "MaxAttachmentBytes": 10000000,
  "AllowMessageDownloads": true,
  "MaxMessageBytes": 10000000,
  "RateLimitPerMinute": 120
}
```

| Setting | Default | Description |
| --- | --- | --- |
| `Enabled` | `false` | Commutateur principal. Lorsque `false`, le point de terminaison `/mcp` renvoie `404`. |
| `AllowAttachmentDownloads` | `true` | Lorsque `false`, `get_attachment` renvoie une erreur au lieu des octets de la pièce jointe. Les métadonnées concernant les pièces jointes sont toujours renvoyées par `get_email`. |
| `DefaultPageSize` | `20` | Taille de page utilisée par `search_emails` lorsque l'appelant omet `pageSize`. |
| `MaxResults` | `100` | Limite supérieure pour la taille de page `search_emails` ; les valeurs plus grandes sont plafonnées. |
| `MaxAttachmentBytes` | `10000000` | Taille maximale de la pièce jointe (en octets) que `get_attachment` renverra en ligne au format base64 ; les pièces jointes plus volumineuses sont refusées. |
| `AllowMessageDownloads` | `true` | Lorsque `false`, `get_email_eml` et `get_email_raw` renvoient une erreur au lieu du message. Un message complet transporte ses pièces jointes, donc **à la fois** ce commutateur et `AllowAttachmentDownloads` doivent être `true` ; désactiver l'un ou l'autre désactive les deux outils. |
| `MaxMessageBytes` | `10000000` | Taille maximale du message (en octets) que `get_email_eml` et `get_email_raw` renverront en ligne ; les messages plus volumineux sont refusés. |
| `RateLimitPerMinute` | `120` | Budget de requête à fenêtre fixe par clé API par minute (voir [Rate limiting](#rate-limiting)). |

## Authentification {#authentication}

Le serveur MCP s'authentifie avec les **mêmes clés API par utilisateur que l'API REST**, envoyées en tant que jeton bearer :

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

Les clés API sont créées et gérées dans l'interface Web sous **API Keys**. Une clé hérite des autorisations exactes de l'utilisateur qui en est le propriétaire (un admin voit tous les comptes ; un utilisateur restreint ne voit que les comptes qui lui sont assignés). Les clés peuvent être révoquées ou expirer indépendamment des comptes de messagerie sous-jacents. Consultez la [section authentification de l'API REST](API.md#authentication) pour connaître l'intégralité du cycle de vie des clés et du modèle d'autorisation — les clés MCP ne constituent pas un magasin d'identifiants distinct.

Lorsque le serveur MCP est désactivé, ou qu'aucun jeton bearer n'est présenté ou qu'il est invalide, le point de terminaison renvoie respectivement `404` / `401` (il ne redirige jamais vers la page de connexion).

## Transport et endpoint {#transport-and-endpoint}

Le serveur MCP utilise le transport **Streamable HTTP** et est monté à l'adresse :

```
POST /mcp
```

Il s'exécute en mode **stateless** (une requête JSON-RPC par requête HTTP), ce qui est le mode recommandé pour les serveurs qui n'ont pas besoin de requêtes serveur-vers-client comme le sampling ou l'elicitation. Les outils sont découverts via la requête MCP standard `tools/list` et appelés via `tools/call`.

## Outils {#tools}

### list_accounts {#list_accounts}

Liste les comptes de messagerie auxquels la clé API actuelle est autorisée à accéder. Retourne
`id`, `name`, `emailAddress`, `provider`, `isEnabled` et `lastSync`.

**Paramètres :** aucun.

**Résultat :** tableau d'objets de Compte. Utilisez le `id` retourné avec `list_folders`
et `search_emails`.

### list_folders {#list_folders}

Liste l'arborescence des Dossiers d'un seul Compte de messagerie, avec le nombre d'e-mails par Dossier.

**Paramètres :**

| Nom | Type | Requis | Description |
| --- | --- | --- | --- |
| `accountId` | integer | oui | L'identifiant du Compte de messagerie (provenant de `list_accounts`). |

**Résultat :** tableau d'objets Dossier, chacun contenant `name`, `fullPath`, `totalCount`,
`level` et `children` récursif. Retourne une liste vide si le Compte n'existe pas
ou si la clé n'est pas autorisée pour celui-ci.

### search_emails {#search_emails}

Recherche des e-mails dans l'Archive par requête textuelle complète, plage de dates, Compte, Dossier et direction.

**Paramètres :**

| Nom | Type | Requis | Description |
| --- | --- | --- | --- |
| `q` | string | no | Requête de recherche textuelle complète (objet, corps, de/à). |
| `from` | string (ISO-8601) | no | Filtre de date/heure UTC envoyé le ou après. |
| `to` | string (ISO-8601) | no | Filtre de date/heure UTC envoyé le ou avant. |
| `accountId` | integer | no | Restreindre à un seul identifiant de Compte de messagerie. |
| `folder` | string | no | Restreindre à un seul chemin complet de Dossier (ex: `INBOX`). |
| `direction` | string | no | `incoming` ou `outgoing`. |
| `page` | integer | no | Numéro de page commençant à 1 (par défaut `1`). |
| `pageSize` | integer | no | Taille de la page ; limitée à `[1, MaxResults]`. `0` utilise `DefaultPageSize`. |
| `sortBy` | string | no | `sentdate` (par défaut), `receiveddate`, `subject`, `from`, `to`. |
| `sortOrder` | string | no | `asc` ou `desc` (par défaut `desc`). |

**Résultat :** un objet paginé avec `items` (tableau de résumés d'e-mails), `page`,
`pageSize`, `totalItems`, `totalPages`. Chaque résumé contient `id`, `accountId`,
`subject`, `from`, `to`, `sentDate`, `isOutgoing`, `hasAttachments` et
`folderName`. Passez un `id` retourné à `get_email` pour obtenir le corps complet et la liste des Pièce jointe.

### get_email {#get_email}

Récupère le contenu complet d'un seul e-mail archivé.

**Paramètres :**

| Nom | Type | Requis | Description |
| --- | --- | --- | --- |
| `id` | integer | oui | L'identifiant de l'e-mail archivé (provenant de `search_emails`). |

**Résultat :** un objet e-mail unique avec tous les champs de résumé plus `cc`, `bcc`,
`receivedDate`, `messageId`, `textBody`, `htmlBody` et `attachments` (tableau de
`{ id, fileName, contentType, size }`) et `hasRawMessage` (si
`get_email_raw` peut retourner l'original). Retourne `null` si l'e-mail n'existe pas
ou si la clé n'est pas autorisée pour son Compte.

### get_attachment {#get_attachment}

Télécharge une seule pièce jointe d'e-mail sous forme de blob base64.

**Paramètres :**

| Nom | Type | Requis | Description |
| --- | --- | --- | --- |
| `id` | integer | oui | L'identifiant de l'e-mail archivé auquel appartient la pièce jointe. |
| `attachmentId` | integer | oui | L'identifiant de la pièce jointe (provenant de `attachments` de `get_email`). |

**Résultat :** un objet avec `id`, `emailId`, `fileName`, `contentType`, `size`
et `base64Data` (les octets bruts de la pièce jointe, encodés en base64). L'outil renvoie une
erreur lorsque `Mcp:AllowAttachmentDownloads=false`, lorsque la pièce jointe dépasse
`Mcp:MaxAttachmentBytes`, ou lorsque la clé n'a pas l'autorisation pour le
compte de l'e-mail.

### get_email_eml {#get_email_eml}

Renvoie un e-mail sous la forme d'un message RFC 5322 complet (`.eml`), reconstruit à partir des champs archivés : les en-têtes stockés (`Received`, `Authentication-Results`, `X-Spam-*`, …), les corps et les pièces jointes. Fonctionne pour chaque e-mail archivé, mais le résultat n'est pas identique au niveau des octets par rapport à ce qui a été reçu, de sorte que les signatures DKIM ne pourront pas être vérifiées avec celui-ci — utilisez [`get_email_raw`](#get_email_raw) pour cela. Reflète `GET /api/v1/emails/{id}/eml`.

**Paramètres :**

| Nom | Type | Requis | Description |
| --- | --- | --- | --- |
| `id` | integer | oui | L'identifiant de l'e-mail archivé (provenant de `search_emails`). |

**Résultat :** un objet avec `emailId`, `kind` (`eml`), `source` (`null`), `sha256`, `size`, `encoding` et `content`. `content` est le texte du message lorsque `encoding` est `utf-8`, et le message encodé en base64 lorsque `encoding` est `base64` (messages qui ne sont pas en UTF-8 valide, afin qu'aucun octet ne soit altéré). L'outil renvoie une erreur lorsque `Mcp:AllowMessageDownloads=false`, lorsque le message dépasse `Mcp:MaxMessageBytes`, ou lorsque la clé n'a pas l'autorisation pour le Compte de l'e-mail.

### get_email_raw {#get_email_raw}

Renvoie le message original exactement tel qu'il a été reçu, pour l'analyse du spam, les vérifications DKIM/SPF/ARC ou la criminalistique. Disponible uniquement lorsque l'original a été stocké au moment de l'Archive (`RawMessages:Enabled`, voir
[Original Message Storage Settings](Setup.md#-original-message-storage-settings)) ;
`get_email` le rapporte comme `hasRawMessage`. Reflète
`GET /api/v1/emails/{id}/raw`.

**Paramètres :**

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `id` | integer | yes | L'identifiant de l'e-mail archivé (provenant de `search_emails`). |

**Résultat :** le même objet que `get_email_eml` avec `kind` `raw`, `source` défini
selon la manière dont l'original a été capturé (`imap`, `graph`, `eml-file` — copies exactes —
ou `serialized`, réécrit par le parseur avec des fins de ligne normalisées en CRLF)
et `sha256` la somme de contrôle enregistrée au moment de l'Archive. Renvoie les mêmes erreurs que
`get_email_eml`, et une erreur lorsqu'aucun original n'est stocké pour l'e-mail.

Les octets sont lus partout où ils ont été stockés — la base de données ou un fichier,
selon `RawMessages:Storage` au moment de l'Archive (voir
[Storage backend](RawMessages.md#storage-backend)). Avec
`RawMessages:FileSystem:VerifyChecksumOnRead` (par défaut `true`), le fichier est
re-haché avant d'être renvoyé ; un message dont le contenu ne correspond plus à la
somme de contrôle enregistrée est refusé avec une erreur plutôt que d'être renvoyé.

## Limitation de débit {#rate-limiting}

Le serveur MCP possède sa propre politique de limitation de débit à fenêtre fixe (`"Mcp"`), partitionnée par le préfixe de la clé API non confidentiel (les 11 premiers caractères du jeton `ma_...`), avec un repli sur l'IP du client lorsqu'aucun jeton porteur n'est présent. Le budget est configuré via `Mcp:RateLimitPerMinute` (par défaut `120` par minute). En cas de dépassement, le point de terminaison renvoie `429 Too Many Requests` avec un en-tête `Retry-After`. Ceci est indépendant de la politique de limitation de débit de l'API REST.

## Journalisation des accès {#access-logging}

Chaque appel d'outil est consigné via le même `IAccessLogService` que l'API REST, avec le nom d'utilisateur propriétaire et le type d'accès correspondant (`Search`, `Open`, `Download`). L'activité MCP est donc visible sur la page **Logs** aux côtés de l'activité de l'API REST et de l'interface web, pour un journal d'audit unique.

## Connexion d'un agent {#connecting-an-agent}

Le serveur MCP utilise le transport standard MCP Streamable HTTP, ainsi tout client MCP prenant en charge HTTP peut se connecter. L'URL est l'URL de base de l'application plus `/mcp`, par exemple `http://localhost:5179/mcp`, et la clé API est fournie via l'en-tête `Authorization: Bearer ma_...`.

Pour les agents configurés via le JSON standard du client MCP, une entrée ressemble à ceci :

```json
{
  "mcpServers": {
    "mail-archiver": {
      "url": "https://archive.example.com/mcp",
      "headers": {
        "Authorization": "Bearer ma_<your-api-key>"
      }
    }
  }
}
```

## Conseils de sécurité pour l'exposition sur Internet {#security-guidance-for-internet-exposure}

Le serveur MCP est conçu de telle sorte que, avec les paramètres par défaut et le durcissement ci-dessous, les e-mails archivés ne peuvent pas quitter le système sans une clé API par utilisateur valide qui a été explicitement délivrée à un Compte. Les points ci-dessous sont une **lecture obligatoire avant d'exposer `/mcp` à Internet**.

### Obligatoire (bloquer l'exploitation / l'exfiltration de données) {#mandatory-block-exploitation-data-exfiltration}

1. **HTTPS uniquement.** Terminez le TLS à la périphérie (reverse proxy). Une clé API de type bearer sur du HTTP en clair peut être interceptée et réutilisée.
2. **Conservez `Mcp:Enabled=false` sauf si vous l'utilisez réellement.** Lorsqu'il est désactivé, `/mcp` renvoie `404` et aucun chemin de code n'est accessible ; c'est le paramètre par défaut sûr et l'état sûr pour les déploiements qui n'ont pas besoin d'accès par agent IA.
3. **La limitation de débit (rate limiting) est appliquée sur le point de terminaison.** La politique `"Mcp"` (`Mcp:RateLimitPerMinute`, par défaut `120`) est appliquée sur le point de terminaison `/mcp` mappé. Elle est partitionnée par le préfixe non secret de la clé API (les 11 premiers caractères du jeton `ma_...`) ou par l'IP du client lorsqu'aucun jeton bearer n'est présent. **Réduisez `RateLimitPerMinute`** pour l'exposition sur Internet (par exemple `30`–`60`) — les appels d'outils MCP sont plus lourds que les appels REST et le point de terminaison ne doit pas être laissé avec un budget élevé lorsque des clients non approuvés peuvent l'atteindre.
4. **Le `X-Forwarded-For` en reverse-proxy doit être surveillé avec prudence.** Le limiteur de débit bascule sur `RemoteIpAddress`, qui, sous un proxy, est l'IP du proxy à moins que `UseForwardedHeaders` ne reçoive la véritable IP du client. Assurez-vous que votre proxy configure `X-Forwarded-For` correctement et que seul votre proxy peut le faire ; sinon, un attaquant peut usurper le partitionnement de la limitation de débit.
5. **Ne placez pas `/mcp` derrière un cache CDN.** Les réponses MCP sont en JSON-RPC par requête et ne doivent jamais être mises en cache — la mise en cache briserait le protocole et laisserait fuiter le résultat d'un utilisateur à un autre. Marquez `/mcp` comme non-cacheable à la périphérie (`Cache-Control: no-store`).

### Hygiène des clés API (bloquer les accès non autorisés) {#api-key-hygiene-block-unauthorized-access}

6. **Délivrez des clés dédiées et à courte durée de vie pour les agents MCP.** Une seule clé `ma_...` est valide pour `/api` et `/mcp` ; il n'y a pas de portée par protocole aujourd'hui. Par conséquent :
   - Créez une **clé séparée par agent** (ne réutilisez pas une clé de script de reporting pour un agent LLM externe).
   - Définissez toujours une `ExpiresAt` courte et faites-la tourner ; révoquez-la immédiatement lorsque l'agent est mis hors service.
   - Privilégiez les clés appartenant à un **utilisateur restreint (non-admin)** dont les seuls Comptes assignés sont ceux dont l'agent a besoin. Une clé admin donne une visibilité sur *tous* les comptes de messagerie ; une clé restreinte limite le rayon d'action en cas de compromission.
7. **Traitez la clé API comme un mot de passe.** C'est un identifiant de type bearer. Ne l'intégrez jamais dans le code côté client, ne la journalisez jamais, ne la validez jamais dans un dépôt. Placez-la dans le magasin de secrets du client MCP et faites-la tourner en cas de suspicion de compromission.
8. **Audit sur la page Logs.** Chaque appel d'outil est enregistré dans `AccessLogs` (`Search`, `Open`, `Download`) sous l'utilisateur propriétaire. Examinez périodiquement l'activité MCP pour détecter des modèles inattendus (pics de volume, lectures en dehors des heures de bureau, téléchargements massifs).

### Modèle de confiance pour les données renvoyées (bloquer les dommages en aval) {#trust-model-for-returned-data-block-downstream-harm}

9. **Le corps des e-mails et les Pièces jointes sont des contenus non approuvés**, exactement comme lorsqu'ils sont arrivés dans la Boîte aux lettres. Ils peuvent contenir des logiciels malveillants, des liens de phishing ou du HTML/JavaScript actif.
   - `get_email` renvoie `htmlBody` **brut et non assaini**. Ne le rendez **pas** à un humain sans l'assainir (DOMPurify / `HtmlSanitizer` / un sandbox). Le rendu d'un HTML archivé brut est un vecteur de stockage-XSS.
   - `get_attachment` renvoie les octets bruts avec le `contentType` original. N'ouvrez pas et n'exécutez pas automatiquement les Pièces jointes ; traitez le champ `contentType` comme des métadonnées non approuvées, et non comme une instruction sûre.
   - `get_email_eml` et `get_email_raw` renvoient le message complet incluant chaque Pièce jointe. Les mêmes règles s'appliquent à chacune de ses parties.
10. **Ne laissez pas l'agent effectuer des actions d'écriture basées sur le contenu des e-mails sans confirmation.** Un e-mail malveillant peut contenir du texte d'injection de prompt ciblant le LLM consommateur. Appliquez les Autorisations et la confirmation dans la propre logique de l'agent — le prompt système n'est pas une limite de sécurité.

### Recommandations de défense en profondeur {#defense-in-depth-recommendations}

11. **Restreignez `/mcp` au niveau réseau** lorsque cela est possible (par exemple, autorisez uniquement l'IP / la plage VPN de l'hôte de l'agent au niveau du reverse proxy), en complément de l'authentification par clé API. C'est le contrôle le plus efficace pour l'exposition sur Internet.
12. **Abaissez `Mcp:AllowAttachmentDownloads` à `false`** si vos agents n'ont pas besoin des octets des Pièces jointes — l'exfiltration de Pièces jointes est le chemin de données le plus volumineux. Les métadonnées (nom de fichier, type de contenu, taille) sont toujours renvoyées via `get_email`.
13. **Abaissez `Mcp:MaxAttachmentBytes`** à la plus petite valeur dont vos agents ont réellement besoin (par défaut `10000000` ≈ 10 Mo). Cela limite la plus grande réponse unique qu'une clé API peut extraire par requête. Il en va de même pour `Mcp:MaxMessageBytes`, ou réglez `Mcp:AllowMessageDownloads` sur `false` lorsque les agents n'ont pas besoin de messages complets.
14. **Abaissez `Mcp:MaxResults` et `Mcp:DefaultPageSize`** pour limiter le plus grand résultat de recherche qu'une clé API peut extraire par requête (par défaut `100` / `20`).
15. **Surveillez `AccessLogs` et les rejets de limitation de débit** (`429`) de manière opérationnelle. Un pic soudain provenant d'un seul préfixe de clé est un signal pour révoquer cette clé.
