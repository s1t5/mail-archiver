# 📥 Guide de migration de Boîte aux lettres {#mailbox-migration-guide}

[← Retour à l'index de la documentation](index.md)

Ce guide explique comment migrer une Boîte aux lettres d'un fournisseur de messagerie à un autre en utilisant l'application Mail Archiver.

## 📋 Aperçu {#overview}

L'application Mail Archiver fournit la fonctionnalité permettant de migrer des e-mails d'une Boîte aux lettres source vers une Boîte aux lettres cible. Ceci est particulièrement utile lors d'un changement de fournisseur de messagerie tout en préservant votre historique d'e-mails. La structure des Dossiers de la Boîte aux lettres source est également préservée pendant la migration.

> **Migrer uniquement les messages récents ?** Le flux décrit ci-dessous copie l'intégralité de la Boîte aux lettres archivée, et l'exécuter deux fois ajoutera tout une seconde fois. Si la nouvelle Boîte aux lettres ne doit contenir que les messages plus récents qu'une date limite tandis que l'historique complet reste dans l'Archive, utilisez plutôt le [date-windowed offload](Offload.md). Il prend une fenêtre de dates, peut exclure et renommer des Dossiers, propose un mode de simulation (dry run), et peut être répété en toute sécurité car il ignore ce que la cible contient déjà. Il est également pilotable en ligne de commande, ce qui permet de scripter une flotte de Boîtes aux lettres.

## 🚀 Processus de migration {#migration-process}

Suivez ces étapes pour migrer votre Boîte aux lettres :

1. **Ajouter le Compte source**
   - Naviguez vers la section "Email Accounts"
   - Cliquez sur "Add Account"
   - Saisissez les détails et les identifiants de votre Compte source
   - Enregistrez le Compte

2. **Synchroniser le Compte source**
   - Laissez l'application effectuer la Synchronisation de tous les e-mails du Compte source
   - Attendez que le processus de Synchronisation soit terminé

3. **Ajouter le Compte cible**
   - Dans la section "Email Accounts", cliquez sur "Add Account"
   - Saisissez les détails et les identifiants de votre Compte cible
   - Enregistrez le Compte

4. **Synchroniser le Compte cible**
   - Si le Compte cible est nouveau, effectuez la Synchronisation pour vous assurer qu'il est correctement configuré
   - Attendez que le processus de Synchronisation soit terminé

5. **Initier la migration**
   - Dans la section "Email Accounts", identifiez le Compte source
   - Allez dans les détails du Compte
   - Sélectionnez l'option "Copy All Emails to Another Mailbox"
   - Choisissez le Compte cible et un Dossier cible
   - Activez l'option **"Preserve Folder Structure"** – le Dossier cible sélectionné servira alors de racine sous laquelle toute la structure de Dossiers source est recréée de manière récursive
   - Si cette option n'est pas activée, tous les e-mails sont placés directement dans le Dossier cible sélectionné (à plat, sans sous-dossiers)
   - Lancez le processus de migration

6. **Surveiller la migration**
   - S'il y a un grand nombre d'e-mails, la migration sera effectuée comme une tâche de fond
   - Le statut et la progression de la migration peuvent être consultés dans l'onglet "Jobs"
   - Attendez que la migration se termine avec succès

7. **Désactiver le Compte source**
   - Après le transfert réussi, retournez au Compte source
   - Réglez le Compte source sur "Disabled" dans les paramètres du Compte
   - Cela garantit qu'il ne sera plus archivé à l'avenir

## ⚠️ Notes importantes {#important-notes}

- Ce flux n'est pas répétable : l'exécuter à nouveau ajoutera chaque message une seconde fois. Le [date-windowed offload](Offload.md) est l'alternative répétable.
- Assurez-vous que le Compte source dispose des Autorisations suffisantes pour lire tous les e-mails
- Assurez-vous que le Compte cible dispose d'un espace de stockage suffisant pour les e-mails migrés
- Le processus de migration peut prendre un certain temps selon le nombre d'e-mails et la vitesse de connexion
- Il est recommandé d'effectuer la migration pendant les heures creuses pour minimiser l'impact sur le service de messagerie
- Vérifiez régulièrement le statut de la migration dans l'onglet "Jobs" pour vous assurer que le processus progresse
- Après la migration, vérifiez que tous les e-mails ont été transférés avec succès vers le Compte cible

## ✅ Bonnes pratiques {#best-practices}

- Testez toujours le processus de migration avec un petit sous-ensemble d'e-mails avant de migrer l'intégralité de la Boîte aux lettres
- Conservez une Sauvegarde des e-mails importants avant de commencer la migration
- Planifiez la migration à un moment où l'utilisation de la messagerie est minimale
- Surveillez de près la progression de la migration et soyez prêt à résoudre tout problème qui pourrait survenir
- Une fois la migration terminée, vérifiez que tous les e-mails sont correctement transférés et accessibles
