# 🧪 Environnement de test local {#local-test-environment}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide décrit comment exécuter la suite de tests, l'application et une cible IMAP sur une machine de développement. Il s'adresse aux contributeurs : la pile logicielle mise en place est délibérément non sécurisée et ne constitue pas un guide de déploiement. Pour le déploiement, consultez le
[Setup Guide](Setup.md).

Trois éléments sont impliqués :

| Élément | Fourni par | Utilité |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | Deux bases de données, une pour `dotnet test` et une pour l'application |
| Dovecot | `docker-compose.test.yml` | Une cible IMAP remplaçant une Boîte aux lettres mailcow |
| L'application | `dotnet run` sur l'hôte | Plus rapide pour itérer que de reconstruire l'image du conteneur |

## 🚀 Démarrage de la pile {#starting-the-stack}

```bash
docker compose -f docker-compose.test.yml up -d
```

Ceci publie tout uniquement sur loopback :

| Service | Adresse d'hôte | Notes |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5433, pas 5432, afin de ne pas entrer en conflit avec un serveur local |
| Dovecot, plain | `127.0.0.1:1143` | L'authentification en clair est activée |
| Dovecot, TLS | `127.0.0.1:1993` | Le certificat auto-signé de l'image |

Pour l'arrêter, et pour supprimer toutes les bases de données et les e-mails stockés :

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ Les deux bases de données {#the-two-databases}

La suite de tests laisse des lignes de fixtures derrière elle : après un cycle complet, la base de données de test contient plusieurs dizaines de comptes de messagerie et d'utilisateurs. Si l'application partageait cette base de données, sa synchronisation en arrière-plan récupérerait ces fixtures et tenterait de manière répétée de joindre des serveurs qui n'existent pas. Ainsi, le conteneur postgres crée deux bases de données lors de la première initialisation
(`tests/postgres/10-create-dev-db.sql`) :

| Database | Used by | Configured in |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

Les deux fichiers de configuration sont ignorés par git et doivent être créés localement.

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

`appsettings.Development.json` à la racine du dépôt :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` importe en dehors du conteneur : il pointe par défaut vers le chemin absolu
`/app/DataProtection-Keys`, qui n'existe pas sur une machine de développement, et sans lui, chaque
requête échoue lors de la lecture du trousseau de clés. `IgnoreSelfSignedCert` est ce qui permet à l'application
d'accepter le certificat Dovecot sur le port 1993.

## ✅ Exécution des tests {#running-the-tests}

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

Les migrations sont appliquées automatiquement par `TestDbFixture` lors du premier lancement. La plupart des tests n'ont pas du tout besoin de base de données ; les autres utilisent PostgreSQL, ils échouent donc avec `Name or service not known` si la pile n'est pas en cours d'exécution.

Une chaîne de connexion peut également être fournie via l'environnement, ce qui remplace chaque fichier de configuration :

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ Exécution de l'application {#running-the-application}

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` définit déjà `ASPNETCORE_ENVIRONMENT=Development` pour
`dotnet run`, donc un simple `dotnet run --project MailArchiver.csproj` fonctionne également et dessert
http://localhost:5179 (le profil `http`). `ASPNETCORE_URLS` ci-dessus choisit seulement le port
du conteneur 5000 à la place.

Les migrations en attente sont appliquées à `MailArchiverDev` au démarrage. Connectez-vous avec les identifiants de `appsettings.json`
(`Authentication:Username` et `Authentication:Password`).

## 📬 Utilisation de la cible Dovecot {#using-the-dovecot-target}

Dovecot accepte **n'importe quel nom d'utilisateur** avec le mot de passe `pass`, donc une nouvelle Boîte aux lettres cible ne nécessite aucun provisionnement : choisissez un nom et connectez-vous. Ajoutez-la sous Mail Accounts avec :

| Field | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | anything, e.g. `offload-target@example.com` | same |
| Password | `pass` | same |

La configuration dans `tests/dovecot/dovecot.conf` est modélisée sur le défaut de l'image elle-même, avec
trois différences délibérées qui la font se comporter comme une cible mailcow :

- **Stockage Maildir** au lieu du sdbox de l'image, ce qui est ce que mailcow utilise.
- **`separator = /`**, correspondant à mailcow. Les chemins de Dossier sont séparés par `/` et `\` lorsqu'une Restauration
  recrée une hiérarchie de Dossiers, donc un serveur utilisant `.` se comporterait différemment.
- **Les dossiers special-use `Drafts`, `Junk`, `Sent` et `Trash` sont pré-créés**, car mailcow
  les pré-crée. Cela importe lors du test d'une migration depuis Exchange : un Dossier source nommé
  `Sent Items` atterrit à côté du `Sent` existant plutôt que de fusionner avec lui, ce qui est la
  situation que le renommage de Dossier doit gérer.

L'authentification en clair est activée sur le port 1143 car l'application se connecte sans aucune
sécurité de transport lorsqu'un Compte a SSL désactivé.

## 🌱 Alimentation d'une Archive pour les tests {#seeding-an-archive-to-test-against}

Une Archive vide n'est pas très utile pour tester une Restauration ou une migration, donc
`tests/seed/seed.sh` en construit une :

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```

Elle crée deux comptes, importe les fixtures, et affiche ce qui a été chargé. Les deux comptes sont
créés avec un intervalle de Synchronisation par compte d'un an, et la source est également désactivée,
de sorte que le service de Synchronisation en arrière-plan l'ignore : son serveur n'existe pas, et la cible
Dovecot ne nécessite que des ajouts. Une mise en garde : l'état de planification réside en mémoire, donc chaque
redémarrage de l'application planifie la cible une fois, immédiatement. La cible exclut donc également les
Dossiers Dovecot à usage spécial (`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`), de sorte qu'une
Synchronisation au redémarrage n'a rien à archiver. Exécuter le script deux fois est sans danger ;
la deuxième exécution signale chaque message comme déjà présent.

Les fixtures sont enregistrées sous `tests/seed/fixtures`, un mbox par Dossier source, avec
`manifest.tsv` mappant chaque fichier à son Dossier cible. Elles sont importées via le
propre CLI `--import-mbox` de l'application, et ce détail est délibéré : le CLI passe par
`MailImporter.ImportEmailToDatabase`, le même importateur que la Synchronisation IMAP, de sorte que les lignes arrivent
exactement au format qu'une véritable Archive contient -- adresses jointes avec `", "`, sujets transmis via
`CleanText`, `SentDate` convertis au fuseau horaire d'affichage, `RawHeaders` extraits et tronqués.
Insérer des lignes directement dans la base de données ignorerait tout cela et produirait des fixtures qui
diffèrent discrètement des données de production.

Chaque message de fixture porte un en-tête `X-Fixture-Purpose` indiquant pourquoi il existe, de sorte que les fichiers
se documentent eux-mêmes :

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```

Les dates sont fixes plutôt que relatives au jour actuel. Une date de coupure de migration est résolue en une
date absolue avant l'exécution d'un travail, de sorte qu'un ensemble de fixtures fixe et une coupure fixe testent la même chose
de manière plus prévisible qu'une coupure glissante. Les messages s'étendent de 2012 à 2026.

La structure des Dossiers reflète une Boîte aux lettres Exchange, et chaque Dossier existe pour tester quelque chose :

| Dossier source | Pourquoi il est là |
|---|---|
| `INBOX` | dates s'étalant sur quinze ans, plus les messages de cas limites ci-dessous |
| `Sent Items` | entre en collision avec le `Sent` à usage spécial existant de la cible Dovecot |
| `Sent Items/2019` | un renommage doit réécrire uniquement le premier segment du chemin |
| `Sent Items Archive` | partage un préfixe en tant que sous-chaîne mais pas sur une limite de segment, il ne doit donc pas être réécrit |
| `Deleted Items` | exclusion de Dossier |
| `Deleted Items/Old` | une exclusion sur un parent doit couvrir les descendants |
| `Junk E-Mail` | le nom Exchange pour un Dossier que Dovecot appelle `Junk` |
| `Archive/Projects/2020` | trois niveaux d'imbrication |

Les messages individuels couvrent un message sans en-tête `Message-ID` (qui reçoit la solution de repli
déterministe), un dont le `Message-ID` n'a pas de `@` (qui ne survit pas intact à une Restauration), un sujet
contenant un caractère de contrôle, un message avec une chaîne `Received` à trois sauts et un sans aucun,
plusieurs avec deux destinataires `To`, et deux paires de quasi-doublons à une seconde d'intervalle.

### Note sur le code de sortie {#a-note-on-the-exit-code}

`tests/seed/seed.sh` vérifie les comptes d'échecs et de messages malformés de la sortie d'importation plutôt que
le code de sortie du processus. `MBoxImportService` signale `CompletedWithErrors` lorsqu'il a ignoré les
doublons, et le CLI mappe chaque statut autre que `Completed` vers l'exit 1, donc relancer une
importation se termine par un code non nul même si rien ne s'est mal passé. Relancer le script de seed est donc sans danger
et signale chaque message comme déjà présent.

## ⚠️ Pas pour la production {#not-for-production}

- N'importe quel nom d'utilisateur s'authentifie auprès de Dovecot avec un mot de passe unique et fixe.
- L'authentification en clair est activée.
- Les identifiants de la base de données sont les valeurs par défaut documentées.
- L'état du courrier et de la base de données réside dans des volumes Docker que `down -v` supprime.

## 🔧 Dépannage {#troubleshooting}

**Les tests échouent avec `Name or service not known`** — la pile ne fonctionne pas, ou
`appsettings.Test.json` est manquant, donc la chaîne de connexion utilise la valeur par défaut du conteneur
`Host=postgres`.

**`NETSDK1226` lors de la compilation** — données de charge de travail incomplètes dans le SDK .NET local, sans rapport avec ce
dépôt. Soit exécuter `dotnet workload repair`, soit compiler avec
`-p:AllowMissingPrunePackageData=true`.

**Dovecot redémarre en boucle** — une erreur de configuration. `docker compose -f
docker-compose.test.yml logs dovecot` signale la ligne coupable. Notez que Dovecot ne
accepte pas plusieurs paramètres sur une seule ligne à l'intérieur d'un bloc.

**L'application renvoie une erreur HTTP 500 à chaque requête** — généralement le ring de clés de protection des données.
Vérifiez que `DataProtection:KeyPath` pointe vers un répertoire accessible en écriture.
