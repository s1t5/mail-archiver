# API REST en lecture seule (v1) {#read-only-rest-api-v1}

Mail Archiver fournit une API REST optionnelle en **lecture seule** pour un accès programmatique aux e-mails archivés. Elle est conçue pour des cas d'utilisation d'automatisation et d'intégration (reporting, exports e-discovery, monitoring) sans passer par l'interface web.

L'objectif est de permettre à un script ou à un agent IA de lire les e-mails archivés **sans jamais exposer les identifiants de la Boîte aux lettres ou communiquer avec le fournisseur de messagerie.** Une clé API limitée par utilisateur est une capacité de *lire l'Archive*, et non un identifiant pour *être la Boîte aux lettres* — elle n'hérite que des autorisations de la Boîte aux lettres de son propriétaire, n'accorde aucun droit d'écriture et peut être révoquée instantanément sans toucher au Compte de messagerie sous-jacent.

> ⚠️ **L'API est désactivée par défaut.** Elle doit être explicitement activée dans la configuration (voir [Enabling the API](#enabling-the-api)). Lorsqu'elle est désactivée, chaque route `/api/*` renvoie `404 Not Found`.

L'API est **conçue pour être en lecture seule** : elle expose les Comptes, les Dossiers, la recherche de messages, les détails des messages et les téléchargements de Pièces jointes. Elle ne crée, ne modifie et ne supprime jamais de données.

## Table des matières {#table-of-contents}

- [Activer l'API](#enabling-the-api)
- [Authentification](#authentication)
  - [Format de la clé API](#api-key-format)
  - [Cycle de vie de la clé](#key-lifecycle)
  - [Modèle d'Autorisation](#permission-model)
  - [Justification de sécurité](#security-rationale)
- [Conventions](#conventions)
  - [URL de base et versionnage](#base-url-and-versioning)
  - [Pagination](#pagination)
  - [Erreurs](#errors)
  - [Limitation du débit](#rate-limiting)
- [Points de terminaison](#endpoints)
  - [Lister les Compte](#list-accounts)
  - [Lister les Dossier](#list-folders)
  - [Rechercher des messages](#search-messages)
  - [Obtenir un message](#get-message)
  - [Télécharger une Pièce jointe](#download-attachment)
  - [Télécharger le message (.eml)](#download-message-eml)
  - [Télécharger le message original](#download-original-message)
  - [Stats](#stats)
  - [Health](#health)
  - [Metrics](#metrics)
- [Types de données](#data-types)
- [Journalisation des accès](#access-logging)
- [OpenAPI et Swagger UI](#openapi-and-swagger-ui)
- [Référence de l'API](API-Reference.md) — référence générée des points de terminaison et des types de données

## Activation de l'API {#enabling-the-api}

L'API est configurée sous la section `Api` de `appsettings.json` (ou les variables d'environnement équivalentes, par exemple `Api__Enabled=true`) :

```json
"Api": {
  "Enabled": false,
  "AllowAttachmentDownloads": true,
  "AllowMessageDownloads": true,
  "EnableSwaggerUi": true,
  "DefaultPageSize": 20,
  "MaxPageSize": 100,
  "RateLimitPerMinute": 120
}
```


### Cycle de vie des clés {#key-lifecycle}

1. **Création** — un utilisateur crée une clé avec un `Name` descriptif et une expiration optionnelle. La réponse affiche la clé en clair une seule fois.
2. **Utilisation** — envoyez la clé en tant que bearer token. Chaque requête acceptée rafraîchit la `LastUsedAt` de la clé (les écritures sont limitées à au plus une fois toutes les ~5 minutes pour éviter une écriture à chaque appel).
3. **Expiration** — si `ExpiresAt` est défini et que la date est passée, la clé est rejetée.
4. **Révocation** — l'utilisateur propriétaire ou un administrateur révoque la clé (`RevokedAt` est défini) ; elle est immédiatement et définitivement rejetée.

Une clé est **active** lorsqu'elle n'est pas révoquée, qu'elle n'est pas expirée, **et** que son utilisateur propriétaire est toujours actif. Une clé inactive pour l'une de ces raisons est rejetée avec `401`.

### Modèle d'Autorisation {#permission-model}

Une clé API hérite des mêmes autorisations que l'utilisateur qui en est le propriétaire. Le principal authentifié porte les mêmes revendications qu'une connexion web normale, donc toutes les autorisations existantes...

| Réglage | Valeur par défaut | Description |
| --- | --- | --- |
| `Enabled` | `false` | Interrupteur principal. Lorsque `false`, toutes les routes `/api/*` renvoient `404` et le document OpenAPI / Swagger UI ne sont pas mappés. |
| `AllowAttachmentDownloads` | `true` | Lorsque `false`, le point de terminaison de téléchargement de la pièce jointe renvoie `403`. Les métadonnées concernant les pièces jointes sont toujours renvoyées par le point de terminaison de détail du message. |
| `AllowMessageDownloads` | `true` | Lorsque `false`, les points de terminaison complete-message (`/eml` et `/raw`) renvoient `403`. Un message complet contient ses pièces jointes, donc **à la fois** cet interrupteur et `AllowAttachmentDownloads` doivent être à `true` ; désactiver l'un ou l'autre désactive les deux points de terminaison. |
| `EnableSwaggerUi` | `true` | Lorsque `true` (et que `Enabled` est `true`), monte Swagger UI à `/apidocs` et le document OpenAPI à `/apidocs/spec/v1.json`. Les deux nécessitent une **session web (cookie)** authentifiée — ils ne font pas partie de `/api/` et ne sont pas accessibles avec une clé API. |
| `DefaultPageSize` | `20` | Taille de page utilisée lorsqu'une requête omet `pageSize`. |
| `MaxPageSize` | `100` | Limite supérieure ; les valeurs `pageSize` plus grandes sont plafonnées à cette valeur. |
| `RateLimitPerMinute` | `120` | Budget de requêtes par fenêtre fixe par clé API par minute (voir [Rate limiting](#rate-limiting)). |

## Authentication {#authentication}

L'API s'authentifie avec des **clés API par utilisateur** envoyées en tant que bearer token :

```
Authorization: Bearer ma_<43 url-safe base64 characters>
```

`/api/v1` accepte **uniquement** les clés API. Les cookies de navigateur ne sont pas pris en compte, l'API n'a donc aucune surface CSRF. Les échecs d'authentification renvoient `401` avec un en-tête `WWW-Authenticate: Bearer` et un corps [problem+json](#errors) — l'API ne redirige **jamais** vers la page de connexion.

Les clés sont gérées en libre-service dans l'interface web sous **API Keys** (chaque utilisateur gère ses propres clés ; les administrateurs peuvent consulter et révoquer les clés de n'importe quel utilisateur).

### API key format {#api-key-format}

Une clé est la chaîne `ma_` suivie de 43 caractères base64 sûrs pour l'URL encodant 32 octets aléatoires (256 bits d'entropie) :

```
ma_8Kf3pQ2sV9xT1bN6mW0rZ4yH7jL5dC8gA3eU2iO1kP
└┬┘└──────────────────────┬──────────────────────┘
prefix          43 chars base64url(32 random bytes)
```

Stockage :

- **`KeyPrefix`** — les ~11 premiers caractères (ex. `ma_8Kf3pQ2`), stockés en
  clair. Utilisés pour identifier une clé dans l'interface utilisateur et dans les journaux. Ce n'est pas un secret.
- **`KeyHash`** — le SHA-256 encodé en hexadécimal de la clé complète, stocké sous un
  **index unique**. La clé en clair n'est jamais stockée.

La clé complète est affichée à l'utilisateur **exactement une fois**, au moment de la création. Elle ne peut pas
être récupérée par la suite ; une clé perdue doit être révoquée et remplacée.

### Key lifecycle {#key-lifecycle}

1. **Create** — un utilisateur crée une clé avec un `Name` descriptif et une
   expiration optionnelle. La réponse affiche la clé en clair une seule fois.
2. **Use** — envoyez la clé en tant que bearer token. Chaque requête acceptée rafraîchit l'`LastUsedAt`
   de la clé (les écritures sont limitées à au plus une fois toutes les ~5 minutes pour
   éviter une écriture à chaque appel).
3. **Expire** — si `ExpiresAt` est défini et se trouve dans le passé, la clé est rejetée.
4. **Revoke** — l'utilisateur propriétaire ou un administrateur révoque la clé
   (`RevokedAt` est défini) ; elle est immédiatement et définitivement rejetée.

Une clé est **active** lorsqu'elle n'est pas révoquée, n'est pas expirée, **et** que son utilisateur
propriétaire est toujours actif. Une clé inactive pour l'une de ces raisons est rejetée avec `401`.

### Permission model {#permission-model}

Une clé API hérite des permissions exactes de l'utilisateur qui en est le propriétaire. Le
principal authentifié porte les mêmes revendications qu'une connexion web normale, donc
toute la logique d'autorisation existante s'applique sans changement :

- **Administrators** (`IsAdmin`) voient tous les comptes de messagerie et tous les messages.
- **Regular users** ne voient que les comptes de messagerie qui leur sont assignés
  (`UserMailAccount`). Les comptes et messages en dehors de cet ensemble sont traités comme
  **inexistants** : les requêtes pour ceux-ci renvoient `404`, et non `403`, de sorte que l'API
  ne divulgue pas l'existence de comptes qu'un appelant ne peut pas voir.

### Security rationale {#security-rationale}

- **2FA bypass.** Les clés API contournent l'authentification à deux facteurs par conception — cela est
  inhérent aux jetons non interactifs (il en va de même pour les jetons d'accès personnels
  sur GitHub/GitLab). Cela est atténué par : les clés étant **désactivées par défaut**,
  révocables, avec expiration optionnelle, limitées aux permissions de l'utilisateur, et chaque
  accès étant enregistré dans le [access log](#access-logging).
- **No cookies on `/api`.** L'authentification par cookie n'est pas acceptée sur les routes `/api`,
  éliminant les préoccupations CSRF pour l'API.
- **Transport.** Servez toujours l'API via HTTPS en production afin que les bearer tokens
  ne soient pas exposés sur le réseau.

## Conventions {#conventions}

### Base URL et versioning {#base-url-and-versioning}

Tous les points de terminaison se trouvent sous `/api/v1`. La version est incluse dans le chemin afin que les futurs changements de rupture puissent être déployés en tant que `/api/v2` sans perturber les clients existants.

Les réponses sont en `application/json` sauf indication contraire (les téléchargements de Pièce jointe renvoient le propre type de contenu de la Pièce jointe). Tous les horodatages sont en UTC, ISO 8601 (`2026-06-11T09:30:00Z`).

### Pagination {#pagination}

Les points de terminaison de liste renvoient une enveloppe de pagination :

```json
{
  "items": [ /* ... */ ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

- `page` commence à 1. Les pages hors plage renvoient un tableau `items` vide (ce n'est pas une erreur).
- `pageSize` est par défaut à `Api:DefaultPageSize` et est limité à `[1, Api:MaxPageSize]`.

### Erreurs {#errors}

Les erreurs utilisent [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807)
`application/problem+json` :

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Unknown sortBy value 'foo'.",
  "instance": "/api/v1/emails"
}
```

| Statut | Quand |
| --- | --- |
| `400 Bad Request` | Paramètres malformés (par ex. date impossible à analyser, `sortBy` invalide). |
| `401 Unauthorized` | Clé manquante, malformée, expirée, révoquée ou autrement invalide. Inclut `WWW-Authenticate: Bearer`. |
| `403 Forbidden` | Téléchargements de Pièce jointe demandés pendant `AllowAttachmentDownloads=false`, ou téléchargements de messages pendant `AllowMessageDownloads=false`. |
| `404 Not Found` | La ressource n'existe pas **ou** se trouve en dehors des Compte autorisés de l'appelant. Renvoyé également pour chaque route `/api/*` lorsque `Api:Enabled=false`. |
| `429 Too Many Requests` | Limite de débit dépassée. |
| `500 Internal Server Error` | Erreur serveur inattendue (problem+json, jamais la page d'erreur HTML). |

### Limite de débit {#rate-limiting}

Les requêtes sont limitées par une politique de fenêtre fixe de `Api:RateLimitPerMinute` requêtes par minute (par défaut `120`), partitionnées par préfixe de clé API (avec repli sur l'IP du client lorsqu'aucune clé n'est présente). Le dépassement du quota renvoie `429`.

## Endpoints {#endpoints}

> Tous les exemples supposent que l'API est activée et que `$KEY` détient une clé valide.

### Liste des comptes {#list-accounts}

```
GET /api/v1/accounts
```

Retourne les comptes de messagerie visibles par l'appelant. Les champs d'identification
(IMAP/OAuth username, password, client secret, …) ne sont **jamais** inclus.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/accounts
```

```json
[
  {
    "id": 1,
    "name": "Support Mailbox",
    "emailAddress": "support@example.com",
    "provider": "IMAP",
    "isEnabled": true,
    "lastSync": "2026-06-11T08:15:00Z"
  }
]
```

### Liste des dossiers {#list-folders}

```
GET /api/v1/accounts/{id}/folders
```

Renvoie l'arborescence des dossiers pour un Compte sous forme de structure imbriquée. Renvoie `404` si le Compte n'existe pas ou n'est pas visible pour l'appelant.

```json
[
  {
    "name": "INBOX",
    "fullPath": "INBOX",
    "totalCount": 4213,
    "level": 0,
    "children": [
      { "name": "Work", "fullPath": "INBOX/Work", "totalCount": 512, "level": 1, "children": [] }
    ]
  }
]
```

### Rechercher des messages {#search-messages}

```
GET /api/v1/emails
```

Recherche des messages archivés avec les mêmes capacités que l'interface utilisateur web.

| Paramètre de requête | Type | Description |
| --- | --- | --- |
| `q` | string | Requête en texte intégral sur le sujet, le corps, de, à, cc, bcc. Prend en charge la syntaxe de champ, par exemple `subject:invoice`, `from:alice@example.com`. |
| `from` | date | Date d'envoi la plus ancienne (inclusive), `YYYY-MM-DD`. |
| `to` | date | Date d'envoi la plus récente (inclusive), `YYYY-MM-DD`. |
| `accountId` | int | Restreindre à un seul Compte (doit être visible par l'appelant). |
| `folder` | string | Restreindre à un seul Dossier par chemin complet. |
| `direction` | string | `incoming` ou `outgoing`. |
| `page` | int | Numéro de page commençant à 1 (par défaut `1`). |
| `pageSize` | int | Résultats par page (par défaut `Api:DefaultPageSize`, limité à `Api:MaxPageSize`). |
| `sortBy` | string | L'un des `sentDate` (par défaut), `receivedDate`, `subject`, `from`, `to`. |
| `sortOrder` | string | `asc` ou `desc` (par défaut `desc`). |

Renvoie une [enveloppe paginée](#pagination) de [`EmailSummaryDto`](#emailsummarydto).
Enregistre une entrée de [Journal d'audit](#access-logging) de type `Search`.

```bash
curl -H "Authorization: Bearer $KEY" \
  "https://host/api/v1/emails?q=subject:invoice&from=2026-01-01&pageSize=50"
```

```json
{
  "items": [
    {
      "id": 9876,
      "accountId": 1,
      "subject": "Invoice #4711",
      "from": "billing@vendor.com",
      "to": "support@example.com",
      "sentDate": "2026-03-02T11:04:00Z",
      "isOutgoing": false,
      "hasAttachments": true,
      "folderName": "INBOX"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalItems": 3,
  "totalPages": 1
}
```

### Get message {#get-message}

```
GET /api/v1/emails/{id}
```

Renvoie le message complet, y compris les corps. Le corps utilise la même chaîne de repli que l'interface web : `OriginalBodyHtml` (octets bruts) → `BodyUntruncatedHtml` →
`HtmlBody`, et l'équivalent pour le corps de texte. Renvoie `404` si le message
n'existe pas ou appartient à un Compte en dehors des Autorisation de l'appelant. Enregistre une entrée dans le Journal d'audit de type `Open`.

Renvoie un [`EmailDetailDto`](#emaildetaildto).

### Télécharger la pièce jointe {#download-attachment}

```
GET /api/v1/emails/{id}/attachments/{attachmentId}
```

Transmet les octets de la pièce jointe avec son type de contenu et son nom de fichier stockés. Renvoie
`404` si le message ou la pièce jointe n'existe pas ou n'est pas visible pour l'appelant. Renvoie `403` (problem+json) lorsque `Api:AllowAttachmentDownloads=false`.
Enregistre une entrée de journal d'audit de type `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/attachments/12
```

### Télécharger le message (.eml) {#download-message-eml}

```
GET /api/v1/emails/{id}/eml
```

Renvoie le message sous forme de `message/rfc822`, reconstruit à partir des champs archivés :
addresses, les en-têtes bruts stockés (`Received`, `Authentication-Results`,
`X-Spam-*`, …), les corps et les pièces jointes. Fonctionne pour chaque e-mail archivé. Il s'agit de la
même reconstruction que celle produite par l'export de Compte de l'interface web, il n'est donc **pas**
identique au niveau de l'octet à ce qui a été reçu : les signatures DKIM ne pourront pas être vérifiées avec celui-ci.
Utilisez [`/raw`](#download-original-message) pour cela.

Renvoie `404` si le message n'existe pas ou n'est pas visible pour l'appelant, et
`403` (problem+json) lorsque `Api:AllowMessageDownloads=false`. Enregistre une entrée de journal d'audit
de type `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -OJ \
  https://host/api/v1/emails/9876/eml
```

### Télécharger le message original {#download-original-message}

```
GET /api/v1/emails/{id}/raw
```

Renvoie le message original tel qu'il a été reçu (`message/rfc822`), pour l'analyse du spam,
la vérification DKIM ou les contrôles médico-légaux. L'original n'est stocké que lorsque
`RawMessages:Enabled` était défini au moment où l'e-mail a été archivé ; voir
[Original Message Storage Settings](Setup.md#-original-message-storage-settings).
Le fait qu'un e-mail en possède un est indiqué par `hasRawMessage` dans
[`EmailDetailDto`](#emaildetaildto).

| En-tête de réponse | Signification |
| --- | --- |
| `X-Raw-Message-Sha256` | SHA-256 des octets renvoyés, enregistré lors du stockage du message |
| `X-Raw-Message-Source` | `imap`, `graph`, `eml-file` (copies exactes) ou `serialized` (réécrit par l'analyseur, fins de ligne normalisées en CRLF) |
| `X-Raw-Message-Storage` | `postgres` ou `filesystem` — l'endroit d'où les octets ont été lus (voir [Storage backend](RawMessages.md#storage-backend)) |

Les octets peuvent résider dans la base de données ou dans un fichier, selon
`RawMessages:Storage` au moment où le message a été archivé ; le point de terminaison
résout cela par message, de sorte que les deux types sont servis de la même manière. Avec
`RawMessages:FileSystem:VerifyChecksumOnRead` (par défaut `true`), le fichier est
re-haché avant d'être envoyé, et un message dont le contenu ne correspond plus au
checksum enregistré n'est **pas** servi.

Renvoie `404` si le message n'existe pas ou n'est pas visible pour l'appelant,
`404` avec le titre du problème `No original message is stored for this email.`
lorsqu'il existe sans original, et `500` avec le titre
`Stored original message failed its checksum.` lorsque le contenu stocké ne correspond plus au
checksum enregistré au moment de l'Archive. Renvoie `403` (problem+json) lorsque
`Api:AllowMessageDownloads=false`. Enregistre une entrée de Journal d'audit de type `Download`.

```bash
curl -H "Authorization: Bearer $KEY" -o 9876.eml \
  https://host/api/v1/emails/9876/raw
sha256sum 9876.eml          # matches X-Raw-Message-Sha256
spamassassin -t < 9876.eml  # or: rspamc < 9876.eml
```

### Stats {#stats}

```
GET /api/v1/stats
```

Renvoie les statistiques agrégées de l'Archive visibles par l'appelant : nombre total d'e-mails archivés, Boîtes aux lettres visibles, Pièces jointes sur ces e-mails, et la taille totale de la base de données PostgreSQL en mégaoctets. Pour les utilisateurs non-administrateurs, les décomptes sont limités aux comptes de messagerie qui leur sont assignés (le même périmètre que celui appliqué au tableau de bord web) ; les administrateurs voient les totaux globaux.

Pour les clés API non-administrateurs, `databaseSizeInMB` est une chaîne vide : la taille de la base de données décrit l'instance entière, et non le périmètre de la clé. Les e-mails, Comptes et Pièces jointes sont toujours limités aux comptes auxquels la clé peut accéder.

```bash
curl -H "Authorization: Bearer $KEY" https://host/api/v1/stats
```

```json
{
  "emails": 160132,
  "accounts": 4,
  "attachments": 43540,
  "databaseSizeInMB": "5432"
}
```

### StatsDto {#statsdto}

| Field | Type | Notes |
| --- | --- | --- |
| `emails` | int | Messages archivés visibles par l'appelant |
| `accounts` | int | Comptes de Boîte aux lettres visibles par l'appelant |
| `attachments` | int | Pièces jointes sur ces messages visibles |
| `databaseSizeInMB` | string | Taille totale de la base de données PostgreSQL en MB ; vide pour les clés non-admin |

### Health {#health}

```
GET /api/v1/health/live
GET /api/v1/health/ready
GET /api/v1/health
```

Les trois nécessitent une clé API admin. `health/live` répond 200 sans toucher à aucune
dépendance. `health/ready` vérifie PostgreSQL et le schéma de l'Archive et renvoie 503 lorsque
l'un des deux est indisponible. `health` exécute également la vérification de la fraîcheur de la synchronisation et renvoie `Degraded`
(HTTP 200) lorsqu'un Compte activé n'a pas effectué de synchronisation depuis `Health:SyncStaleMinutes`.

```json
{"status":"Healthy","checks":{"database":"Healthy","sync":"Healthy"}}
```

La description complète, incluant le comportement 401/403/404 et le health check Docker, se trouve dans
**[Health and Metrics](Monitoring.md)**.

### Métriques {#metrics}

```
GET /api/v1/metrics
```

Retourne le format d'exposition textuel Prometheus. Nécessite une clé API admin et `Metrics:Enabled=true` ; sinon, le point de terminaison retourne 404. Les noms des métriques, les étiquettes et les exemples d'alertes se trouvent dans **[Health and Metrics](Monitoring.md)**.

## Types de données {#data-types}

### MailAccountDto {#mailaccountdto}

| Field | Type | Notes |
| --- | --- | --- |
| `id` | int | |
| `name` | string | |
| `emailAddress` | string | |
| `provider` | string | `IMAP`, `M365`, … |
| `isEnabled` | bool | |
| `lastSync` | datetime | UTC |

Aucun champ d'identifiant n'est jamais sérialisé.

### FolderNodeDto {#foldernodedto}

| Field | Type | Notes |
| --- | --- | --- |
| `name` | string | Leaf name |
| `fullPath` | string | Full path |
| `totalCount` | int | Messages in folder |
| `level` | int | Nesting depth (0 = root) |
| `children` | FolderNodeDto[] | Sub-folders |

### EmailSummaryDto {#emailsummarydto}

| Field | Type |
| --- | --- |
| `id` | int |
| `accountId` | int |
| `subject` | string |
| `from` | string |
| `to` | string |
| `sentDate` | datetime |
| `isOutgoing` | bool |
| `hasAttachments` | bool |
| `folderName` | string |

### EmailDetailDto {#emaildetaildto}

Tout ce qui se trouve dans [`EmailSummaryDto`](#emailsummarydto) plus :

| Field | Type | Notes |
| --- | --- | --- |
| `cc` | string | |
| `bcc` | string | |
| `receivedDate` | datetime | |
| `messageId` | string | |
| `textBody` | string | Résolu via la chaîne de repli du corps |
| `htmlBody` | string | Résolu via la chaîne de repli du corps |
| `attachments` | AttachmentDto[] | Métadonnées uniquement |
| `hasRawMessage` | bool | `true` lorsque le message original est stocké et que [`/raw`](#download-original-message) le renvoie |

### AttachmentDto {#attachmentdto}

| Field | Type | Notes |
| --- | --- | --- |
| `id` | int | |
| `fileName` | string | |
| `contentType` | string | |
| `size` | long | Bytes |

Les octets de la Pièce jointe sont obtenus à partir de l'[endpoint de téléchargement](#download-attachment),
et non à partir de ce DTO.

## Journalisation des accès {#access-logging}

Chaque accès aux données via l'API est enregistré dans le même journal d'accès utilisé par l'interface web (visible sous **Logs**), et attribué à l'utilisateur propriétaire de la clé :

| Endpoint | Type enregistré |
| --- | --- |
| Search messages | `Search` |
| Get message | `Open` |
| Download attachment | `Download` |
| Download message (.eml) | `Download` |
| Download original message | `Download` |

## OpenAPI et Swagger UI {#openapi-and-swagger-ui}

Lorsque l'API est activée et `Api:EnableSwaggerUi=true` :

- Le document OpenAPI v1 est servi sur **`/apidocs/spec/v1.json`**.
- Swagger UI est servi sur **`/apidocs`**.

Les deux chemins se situent en dehors de `/api/` et sont donc protégés par l'authentification web (cookie) normale — une session de navigateur connectée est requise pour les consulter, et ils ne peuvent pas être atteints avec une clé API.

### Référence des points de terminaison sur ce site {#endpoint-reference-on-this-site}

Les points de terminaison et les types de données sont également publiés ici sous la forme d'une page générée :
**[API Reference](API-Reference.md)**. Elle est rendue à partir du même document OpenAPI — une copie est conservée à `doc/assets/openapi-v1.json` — par
`tools/gen_api_reference.py`. Le CI échoue lorsque la page et la spécification ne sont plus en
synchronisation, ainsi la référence décrit toujours l'API qui est réellement livrée.

!!! note "Régénération"
    La page est un résultat généré. Modifiez l'API (ou le générateur), puis exécutez
    `python3 tools/gen_api_reference.py` et validez le résultat.

    Actualiser la spécification fournie elle-même signifie réexécuter la génération
    de document au moment de la construction décrite dans `tools/README.md`.
