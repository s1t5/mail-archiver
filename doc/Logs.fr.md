# 📋 Journal d'audit {#access-log}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide explique la fonctionnalité de journal d'audit dans l'application Mail Archiver, qui permet l'enregistrement et la consultation des activités des utilisateurs au sein du système. Cette fonctionnalité est cruciale à des fins de surveillance, de sécurité et de débogage.

## 📊 Ce qui est enregistré {#what-is-logged}

L'application enregistre différents types d'activités utilisateur, notamment :

1. **Connexion et déconnexion** - Enregistrement des opérations de connexion et de déconnexion
2. **Opérations de recherche** - Enregistrement des requêtes de recherche d'e-mails au sein du système
3. **Ouverture d'e-mail** - Enregistrement lorsque les utilisateurs ouvrent le contenu d'un e-mail
4. **Téléchargement d'e-mail** - Enregistrement lorsque les utilisateurs téléchargent des e-mails
5. **Restauration d'e-mail** - Enregistrement lorsque les utilisateurs restaurent des e-mails archivés
6. **Gestion de Compte** - Enregistrement des actions liées au Compte
7. **Opérations de suppression** - Enregistrement des actions de suppression
8. **Exportation de données d'audit** - Enregistrement des exécutions d'exportation de données d'audit (début et résultat, voir [Audit Data Export](AuditExport.md))

## 📝 Types d'actions et descriptions {#action-types-and-descriptions}

Les actions enregistrées sont classées dans les types suivants :

### Login {#login}
- **Description** : Enregistre lorsqu'un utilisateur se connecte au système
- **Détails** : Username, timestamp

### Logout {#logout}
- **Description** : Enregistre lorsqu'un utilisateur se déconnecte du système
- **Détails** : Username, timestamp

### Search {#search}
- **Description** : Enregistre les requêtes de recherche au sein du système d'e-mails
- **Détails** : Username, timestamp, search parameters

### Open {#open}
- **Description** : Enregistre lorsqu'un utilisateur ouvre un e-mail
- **Détails** : Username, timestamp, email ID, sender, subject

### Download {#download}
- **Description** : Enregistre lorsque les utilisateurs téléchargent des e-mails
- **Détails** : Username, timestamp, email ID, sender, subject

### Restore {#restore}
- **Description** : Enregistre lorsque les utilisateurs restaurent des e-mails archivés
- **Détails** : Username, timestamp, email ID, sender, subject

### Account {#account}
- **Description** : Enregistre les actions liées au Compte (création, modification, etc.)
- **Détails** : Username, timestamp, account ID

### Deletion {#deletion}
- **Description** : Enregistre les opérations de suppression sur les e-mails ou d'autres ressources
- **Détails** : Username, timestamp, affected resource
- **Note de conformité** : Les opérations de suppression sont enregistrées à des fins d'audit de conformité

## 🔍 Options de filtrage {#filter-options}

La page de journal propose les options de filtrage suivantes :

1. **Date** - Filtrer par plage de dates (De-À)
2. **Username** - Filtrer par utilisateur spécifique (Admin uniquement)
3. **Action Type** - Filtrer par type d'action spécifique

## 🖥️ Affichage du journal {#log-display}

Les journaux sont affichés sous forme de tableau avec les colonnes suivantes :

- **Timestamp** : Date et heure de l'action
- **Username** : Nom de l'utilisateur impliqué
- **Type** : Type d'action avec un code couleur
- **Information** : Détails supplémentaires sur l'action

Pour les appareils mobiles, les journaux sont affichés sous forme de cartes.

## 🔐 Autorisations d'accès {#access-permissions}

- **Utilisateurs non-admin** : Peuvent uniquement consulter leurs propres journaux
- **Utilisateurs Admin** : Peuvent consulter les journaux de tous les utilisateurs et filtrer par utilisateur

## 📤 Audit Data Export {#audit-data-export}

Les administrateurs peuvent ouvrir la page dédiée [Audit Data Export](AuditExport.md) directement depuis la page des journaux. Elle crée des packages de données massives sous forme de tableaux (INDEX.XML + CSV) à partir de l'Archive pour des outils d'audit externes. Chaque exécution d'exportation écrit deux entrées de journal du type **Audit Data Export** (début et résultat), qui sont listées et filtrables sur la page des journaux comme toutes les autres entrées.

Pour plus de détails, consultez le [guide Audit Data Export](AuditExport.md).
