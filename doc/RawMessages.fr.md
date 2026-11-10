# 📧 Original Message Storage {#original-message-storage}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Par défaut, l'Archive conserve les **parties analysées** d'un e-mail : en-têtes, corps de texte et HTML, pièces jointes. Un `.eml` exporté est reconstruit à partir de ces champs.

Cela suffit pour lire un e-mail, mais ce n'est pas le message **tel qu'il a été reçu**. Les encodages de transfert, les délimiteurs MIME et le repliement des en-têtes diffèrent de l'original, de sorte qu'un message reconstruit ne vérifie plus sa signature DKIM et les scanners de spam lui attribuent un score différent.

Lorsque le stockage du message original est activé, l'Archive conserve le message complet tel qu'il est arrivé, en plus des champs analysés. L'original stocké est ce que l'Archive peut prouver avoir été livré.

| | Champs analysés uniquement | Avec l'original stocké |
| --- | --- | --- |
| En-têtes, corps, pièces jointes | ✅ | ✅ |
| Message octet par octet | ❌ | ✅ |
| Vérification de la signature DKIM | ❌ | ✅ |
| Usage forensique (tribunal, audit) | limité | ✅ |

## 🎯 Où l'original est servi {#where-the-original-is-served}

Une fois stocké, l'original est renvoyé partout où un message complet est attendu :

| Emplacement | Ce qui se passe |
| --- | --- |
| Bouton **Export EML** sur la page de détails de l'e-mail | Sert l'original stocké lorsqu'il existe, sinon utilise la reconstruction pour les e-mails archivés sans original. |
| **Restore to a mailbox** | Ajoute l'original stocké lorsqu'il existe, sinon utilise la reconstruction pour les e-mails archivés sans original. |
| `GET /api/v1/emails/{id}/raw` | Téléchargement via REST API, voir le [REST API guide](API.md#download-original-message). |
| `get_email_raw` | Outil MCP, voir le [MCP guide](MCP.md#get_email_raw). |

La réponse vous indique quelle forme vous avez reçue, de sorte qu'un appelant peut distinguer un véritable original d'une reconstruction sans inspecter les octets :

| Signal | Signification |
| --- | --- |
| `X-Raw-Message-Source: imap` | Copie exacte du serveur IMAP (`BODY[]` tel que livré). |
| `X-Raw-Message-Source: graph` | Copie exacte, le contenu MIME renvoyé par Exchange. |
| `X-Raw-Message-Source: eml-file` | Copie exacte, le contenu du fichier `.eml` importé. |
| `X-Raw-Message-Source: serialized` | Message analysé et réécrit. Identique sauf que les fins de ligne sont CRLF (import MBox, récupération IMAP). |
| `X-Raw-Message-Sha256` | SHA-256 de l'original stocké. |
| `X-Raw-Message-Reconstructed: true` | Aucun original stocké ; le message a été reconstruit. Pas d'en-tête de checksum. |
| `X-Raw-Message-Storage` | Backend duquel l'original a été lu (`postgres` ou `filesystem`). |

Le nom de fichier l'indique également : `email-{id}-original-...eml` pour un original stocké,
`email-{id}-...eml` pour une reconstruction.

Lorsqu'un original stocké **échoue à son checksum**, l'export et la restauration sont refusés
avec un message d'erreur. Retourner la reconstruction à la place masquerait le fait que la
copie archivée a été altérée, ce qui est précisément ce qu'une Archive ne doit jamais faire.

Une restauration lit l'original stocké et l'ajoute via le client du fournisseur, de sorte que la
Boîte aux lettres reçoit les en-têtes et la structure MIME originaux. Le flux d'octets exact n'est pas
garanti sur le réseau car le client sérialise à nouveau le message ; la copie archivée
elle-même reste inchangée.

## ⚙️ Paramètres {#settings}

Tous les paramètres se trouvent dans la section `RawMessages`, ou sous forme de variables d'environnement avec le préfixe `RawMessages__`.

| Paramètre | Valeur par défaut | Description |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Stocke le message original de chaque e-mail nouvellement archivé. Les e-mails archivés avant l'activation de cette option n'ont pas d'original. |
| `RawMessages:MaxMessageSizeMb` | `50` | Les messages plus grands que cette taille sont archivés sans leur original. `0` = pas de limite. |
| `RawMessages:IncludeGraph` | `true` | Stocke également l'original pour les Compte Microsoft 365 (Graph). Coûte une requête Graph supplémentaire par message, ce qui compte dans le budget de limitation de débit. |
| `RawMessages:Storage` | `postgres` | Lieu où les octets du message sont conservés : `postgres` (dans la base de données) ou `filesystem` (fichier gzip à côté de l'Archive). Voir [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Répertoire dans lequel les fichiers de messages sont écrits, par rapport à la racine du contenu de l'application. Pointez cela vers un volume monté dans Docker. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Nombre de niveaux de répertoires à deux caractères sur lesquels l'id de l'e-mail est réparti. `2` donne 10 000 répertoires, `0` est plat. Maintient le nombre de fichiers par répertoire bas sur les grandes Archives. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Recalcule le SHA-256 du fichier à chaque téléchargement et le compare avec la somme de contrôle enregistrée au moment de l'Archive. Une discordance fait échouer le téléchargement avec une erreur HTTP 500 au lieu de servir des octets altérés. |

### Dimensionnement {#sizing}

Le stockage augmente approximativement de la taille de chaque e-mail incluant ses Pièce jointe, compressé en gzip. Les Pièce jointe ne sont **pas** soumises à la Déduplication à l'intérieur de l'original stocké, donc un e-mail avec une grande Pièce jointe coûte cette Pièce jointe deux fois : une fois dans le magasin de Pièce jointe dédupliqué et une fois à l'intérieur de l'original. Prévoyez environ **deux fois** la taille de la base de données d'une Archive sans originaux.

L'activation du paramètre ne traite pas rétroactivement les e-mails existants. Seuls les e-mails nouvellement archivés reçoivent un original.

## 💾 Backend de stockage {#storage-backend}

Le paramètre `Storage` décide de l'emplacement des octets. La **ligne de métadonnées reste toujours dans
la base de données**, quel que soit le backend configuré, et contient le SHA-256, la taille, la
source de capture et (pour les fichiers) le chemin relatif. C'est cette ligne qui permet à l'Archive de prouver ce qu'un
message stocké doit contenir.

### postgres (default) {#postgres-default}

Le message compressé se trouve dans la colonne `Content` de `mail_archiver."RawMessages"`.
C'est le comportement que l'Archive a toujours eu et qui ne nécessite pas de volume supplémentaire.

Choisissez cette option lorsque la base de données est déjà votre unité de Sauvegarde et que vous voulez une seule chose à
Sauvegarder et à Restaurer.

### filesystem {#filesystem}

Le message est écrit dans `{BasePath}/{xx}/{yy}/{id}.eml.gz`, où `xx`/`yy` sont dérivés
de l'ID de l'e-mail. La ligne de la base de données ne conserve que les métadonnées.

Choisissez cette option lorsque l'Archive est suffisamment volumineuse pour que les originaux dominent la taille de la base de données, ou lorsque vous souhaitez placer les fichiers de messages sur un stockage moins coûteux ou séparé.

!!! danger "Monter un volume dans Docker"
    Le `BasePath` par défaut (`RawMessages`) correspond à `/app/RawMessages` **à l'intérieur du
    conteneur**. Sans un bind mount, ce répertoire appartient à la couche d'écriture du conteneur, donc chaque conteneur recréé - y compris `docker compose down && docker compose
    up -d`, une mise à jour d'image, ou un redémarrage de l'hôte avec une couche éphémère - emporte avec lui chaque original stocké alors que les lignes de la base de données survivent.

    Montez toujours le répertoire sur l'hôte lorsque vous utilisez le backend filesystem :

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Le chemin de l'hôte (`./raw-messages` ici) est ce que vous Sauvegardez. Le chemin du conteneur doit
    correspondre à `RawMessages:FileSystem:BasePath`, ou définissez `RawMessages:FileSystem:BasePath` sur
    le point de montage que vous avez choisi. Si vous déplacez le montage, maintenez `BasePath` pointant vers celui-ci : les
    valeurs `StoragePath` stockées sont relatives, elles continuent donc d'être résolues.

### Changement de backend {#switching-backends}

Les lectures suivent le stockage enregistré sur chaque ligne, et non le paramètre configuré. Après avoir
modifié `Storage`, les messages archivés précédemment restent téléchargeables depuis leur emplacement actuel.
Le paramètre n'affecte que les e-mails **nouvellement** archivés.

Pour déplacer ce qui est déjà stocké, exécutez la migration de stockage. Consultez les
[CLI commands](CLI.md#moving-stored-original-messages) pour la référence complète :

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Ce que la migration garantit :

- Chaque message est vérifié par rapport à sa somme de contrôle enregistrée avant d'être copié. Un message
  en échec est laissé là où il se trouve et signalé : un original corrompu n'est jamais propagé et
  sa seule copie restante n'est jamais supprimée.
- Un fichier n'est supprimé qu'après la validation de la transaction de la base de données qui l'a déplacé, de sorte qu'une
  exécution interrompue ne peut pas perdre la seule copie.
- L'exécution est reprise. Le travail est sélectionné par la colonne `StorageType`, donc relancer la
  commande permet de continuer avec ce qui reste à faire.
- Le code de sortie est `1` si un message a échoué, `0` sinon.

Une migration n'est pas requise pour que l'Archive fonctionne. Elle est seulement nécessaire pour déplacer les octets
existants vers l'autre backend.

## 🛟 Sauvegarde et restauration {#backup-and-restore}

=== "postgres backend"
    Rien de particulier. Les originaux font partie de la base de données et sont couverts par le
    dump normal, voir [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Sauvegardez le répertoire `BasePath` **en même temps que** le dump de la base de données.

    Une restauration de base de données sans les fichiers laisse l'Archive avec des lignes dont le contenu
    ne peut pas être servi. Le téléchargement répond alors par une erreur HTTP 404 plutôt que de renvoyer quelque chose
    d'erroné, et les e-mails eux-mêmes restent lisibles via leurs champs analysés.

## 🗑️ Suppression d'un e-mail {#deleting-an-email}

La suppression d'un e-mail supprime également son original : les règles de Conservation, la suppression manuelle individuelle ou groupée, ainsi que la suppression d'un Compte de messagerie complet suppriment le fichier dans le cadre de la même opération. Avec le backend `postgres`, il n'y a rien de plus à faire, car les octets voyagent à l'intérieur de la ligne que la cascade supprime déjà.

Le fichier est supprimé **après** que la ligne de l'e-mail a été validée, jamais avant. Les deux étapes ne peuvent pas faire partie d'une seule transaction, car un fichier ne fait pas partie de la transaction de la base de données, l'une d'elles doit donc intervenir en second. Cet ordre est le plus sûr : une suppression qui échoue ou qui subit un rollback, par exemple sur un enregistrement verrouillé, laisse l'original intact, et un plantage entre les deux étapes laisse un fichier sans ligne, que le nettoyage ci-dessous récupère. L'autre ordre détruirait l'original d'un e-mail qui existe encore.

**Les fichiers orphelins sont collectés.** Tout ce qui aurait pu échapper au processus, y compris les fichiers laissés par des suppressions antérieures à l'existence de ce nettoyage, est supprimé par un balayage périodique de `BasePath` qui compare chaque fichier aux lignes et supprime ceux auxquels aucun e-mail ne se réfère :

| Setting | Default | Meaning |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Activer ou désactiver le balayage |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Fréquence d'exécution |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Les fichiers plus récents que cette valeur ne sont jamais touchés |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Délai avant la première exécution, afin qu'une migration de stockage en attente se termine d'abord |

La période de grâce est ce qui rend le balayage sûr : un import écrit un fichier avant de valider la ligne qui pointe vers lui, et un fichier écrit il y a quelques instants ne doit pas être confondu avec un orphelin. Les fichiers `.tmp` semi-écrits provenant d'une écriture interrompue sont collectés selon la même règle. Avec le backend `postgres`, le balayage ne fait rien et ne parcourt même pas le répertoire.

## 🔍 Vérification d'une Archive {#verifying-an-archive}

La somme de contrôle enregistrée au moment de l'Archive rend la vérification possible.

- **Lors du téléchargement** (`VerifyChecksumOnRead=true`, par défaut) le fichier est haché et
  comparé. Une non-correspondance refuse le téléchargement avec une erreur HTTP 500.
- **Lors de la migration du stockage** chaque message est vérifié avant d'être copié, dans les deux
  sens.

Comme la ligne de métadonnées se trouve dans la base de données et que les octets peuvent être sur le disque, une comparaison de somme de contrôle est le seul moyen de remarquer qu'un fichier a été modifié en dehors de l'application.

## ⚠️ Choses à savoir {#things-to-know}

- **L'activation ne permet pas de remplissage rétroactif.** Seuls les e-mails archivés après le basculement reçoivent un original.
- **La reconstruction n'est pas le message.** Pour les e-mails sans original stocké, chaque
  chemin de téléchargement renvoie un message reconstruit et l'indique via
  `X-Raw-Message-Reconstructed: true`.
- **Le système de fichiers implique deux unités de Sauvegarde.** La base de données et le répertoire des messages doivent être restaurés
  ensemble, sinon les lignes survivent sans leur contenu.
- **`VerifyChecksumOnRead` consomme du CPU.** Chaque téléchargement d'un message volumineux est décompressé
  et haché. Laissez cette option activée à moins d'avoir mesuré un problème ; c'est la seule protection
  contre l'altération silencieuse sur le disque.
- **L'original stocké n'est pas soumis à la Déduplication.** Une Pièce jointe qui apparaît dans de nombreux e-mails
  est stockée une fois dans le magasin de pièces jointes, mais une fois par e-mail à l'intérieur de l'original.
- **La suppression supprime aussi l'original.** Un e-mail supprimé par la Conservation ou manuellement emporte
  son original stocké avec lui, le fichier et la ligne. Rien n'est conservé que l'Archive ne
  répertorie plus.

## 🔗 Voir aussi {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - la migration de stockage complète
- [Setup and Parameters](Setup.md) - tous les paramètres de configuration
- [REST API](API.md#download-original-message) - le point de terminaison `/raw`
- [MCP Server](MCP.md#get_email_raw) - l'outil `get_email_raw`
- [Backup and Restore](BackupRestore.md) - quoi sauvegarder
- [Attachment Deduplication](AttachmentDeduplication.md) - comment les pièces jointes sont stockées
