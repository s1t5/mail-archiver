# Import de Compte {#account-import}

[← Retour à l'index de la documentation](index.md)

## Aperçu {#overview}

Mail Archiver permet d'importer plusieurs comptes de messagerie à la fois afin que l'intégration d'un grand nombre de Boîtes aux lettres ne nécessite pas la création de chaque compte individuellement.

Deux méthodes d'importation groupée existent, selon le fournisseur de messagerie :

| Fournisseur | Méthode d'importation groupée | Documentation |
|---|---|---|
| **Microsoft 365 (tenant)** | Découverte du tenant via Microsoft Graph — un enregistrement d'application accède à chaque Boîte aux lettres du tenant | [Importation de Boîte aux lettres de tenant Microsoft 365](M365TenantImport.md) |
| **IMAP** | Importation groupée par CSV — téléchargez un fichier CSV avec une ligne par Boîte aux lettres | Cette page (voir ci-dessous) |

> **Note :** Les comptes personnels Microsoft (Outlook.com / M365 Family, fournisseur **MSA**) utilisent un flux d'autorisation par code de l'appareil interactif et ne peuvent pas être importés en masse. Ils doivent être créés et autorisés un par un. Voir [Configuration MSA Outlook](MSA_Outlook_Setup.md).

---

## Importation groupée CSV IMAP {#imap-csv-bulk-import}

### Ce qu'elle fait {#what-it-does}

L'importation groupée par CSV permet à un administrateur de créer des centaines de comptes de messagerie IMAP en une seule opération en téléchargeant un fichier CSV. Chaque ligne du fichier correspond à une Boîte aux lettres. Les paramètres de serveur courants (hôte IMAP, port, SSL) peuvent être spécifiés une seule fois dans le formulaire de téléchargement et s'appliquent à chaque ligne, avec des options de remplacement par ligne.

L'importation est réservée à l'**administrateur**. Elle est accessible depuis la page **Mail Accounts** via le menu déroulant **Import** → **CSV Bulk Import**.

### Format du fichier CSV {#csv-file-format}

Le fichier doit être un CSV encodé en UTF-8 avec une **ligne d'en-tête** et la **virgule (`,`) comme délimiteur**. Les champs contenant des virgules ou des guillemets doivent être entourés de doubles guillemets (`"..."`).

#### Colonnes {#columns}

| Colonne | Requis | Description |
|---|---|---|
| `email` | Oui | Adresse de la Boîte aux lettres. Utilisée également comme nom d'utilisateur de connexion par défaut lorsque `username` n'est pas fournie. |
| `password` | Oui | Mot de passe de connexion IMAP. Stocké tel quel — les espaces au début et à la fin sont conservés. |
| `name` | Non | Nom d'affichage pour le Compte. Si omis, utilise `{prefix} - <{email}>` par défaut. |
| `username` | Non | Nom d'utilisateur de connexion si différent de l'adresse e-mail. |
| `imap_server` | Non | Hôte IMAP par ligne. Remplace le serveur commun du formulaire de téléchargement. |
| `imap_port` | Non | Port IMAP par ligne (1–65535). Remplace le port commun. |
| `use_ssl` | Non | Bascule SSL par ligne (`true` ou `false`). Remplace le paramètre commun. |

#### Exemple {#example}

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- La ligne 1 utilise le serveur/port/SSL commun du formulaire de téléchargement.
- La ligne 2 définit un nom d'utilisateur personnalisé mais utilise les autres paramètres communs.
- La ligne 3 remplace le serveur, le port et le SSL.

Un fichier d'exemple téléchargeable est disponible sur la page d'importation via le bouton **Download example CSV**.

### Formulaire de téléchargement {#upload-form}

| Champ | Utilité |
|---|---|
| **CSV file** | Le fichier à télécharger. |
| **IMAP server** | Hôte commun pour toutes les lignes sans `imap_server` de remplacement. |
| **IMAP port** | Port commun (par défaut 993). |
| **Use SSL** | Bascule SSL commune (activée par défaut). |
| **Name prefix** | Préfixe pour les noms de Compte auto-générés. Par défaut : `IMAP`. |
| **Skip existing mailboxes** | Lorsqu'activé, les lignes dont l'e-mail existe déjà en tant que compte IMAP sont ignorées (comptées comme "ignorées", pas comme des erreurs). Lorsqu'elle est désactivée, elles sont traitées comme des échecs. |
| **Account enabled** | Si les comptes créés doivent être activés pour la Synchronisation en arrière-plan. |
| **Delete After Days / Local Retention Days** | Paramètres de Conservation optionnels appliqués à chaque Compte créé. Voir [Politiques de Conservation](RetentionPolicies.md). |

### Traitement {#processing}

1. Le fichier CSV est analysé et chaque ligne est validée (format d'e-mail, mot de passe présent, plage de port, valeur SSL).
2. Les e-mails en double dans le fichier sont supprimés (la première occurrence l'emporte).
3. Les comptes IMAP existants avec la même adresse e-mail sont détectés ; selon la bascule **Skip existing**, ils sont soit ignorés, soit signalés comme des échecs.
4. Tous les nouveaux comptes sont insérés dans un seul lot de base de données.
5. Une entrée de journal d'accès est écrite pour résumer l'exécution.

Aucun test de connexion IMAP n'est effectué pendant l'importation — les comptes sont sauvegardés immédiatement. Les problèmes d'authentification ne n'apparaîtront que lors de la première Synchronisation en arrière-plan. Cela permet de garder l'importation rapide, même pour des centaines de Boîtes aux lettres.

### Page de résultat {#result-page}

Une fois l'importation terminée, une page de résultat affiche trois tableaux :

- **Created** — l'e-mail et le nom de chaque Compte qui a été créé.
- **Skipped** — les e-mails qui étaient des doublons (dans le fichier ou déjà existants) lorsque **Skip existing** est activé.
- **Failed** — les lignes qui n'ont pas pu être traitées, avec le numéro de ligne et la raison (e-mail manquant, mot de passe manquant, port invalide, pas de serveur IMAP, etc.).

Une bannière de résumé affiche les comptes : *{created} créés, {skipped} ignorés, {failed} échoués*.

### Limites {#limits}

Les limites suivantes peuvent être configurées via `appsettings.json` / variables d'environnement (voir [Setup Guide](Setup.md) → *CSV Import Settings*) :

| Paramètre | Par défaut | Description |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | Nombre maximum de lignes traitées par téléchargement. |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | Taille maximale du fichier téléchargé. |

### Note de sécurité {#security-note}

Les mots de passe IMAP sont stockés dans la base de données de la même manière que pour les comptes IMAP créés individuellement. Soyez conscient que le fichier CSV contient tous les mots de passe des Boîtes aux lettres en clair — supprimez-le de la machine cliente après une importation réussie.

---

## Importation de Tenant Microsoft 365 {#microsoft-365-tenant-import}

Pour Microsoft 365 (Exchange Online), l'importation groupée ne nécessite pas de fichier CSV. Un seul enregistrement d'application Azure AD avec les bonnes permissions Graph peut accéder à chaque Boîte aux lettres du tenant, de sorte que Mail Archiver peut découvrir les Boîtes aux lettres automatiquement.

Consultez le guide dédié : **[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**.
