# 📧 Gmail Best Practices {#gmail-best-practices}

[← Back to Documentation Index](index.md)

Lors de l'utilisation de comptes Gmail avec l'application Mail Archiver, plusieurs configurations spécifiques et bonnes pratiques doivent être suivies pour garantir des performances optimales et éviter des problèmes potentiels.

## 🔐 IMAP Configuration with App Passwords {#imap-configuration-with-app-passwords}

Les comptes Gmail peuvent être intégrés à l'application Mail Archiver en utilisant l'IMAP avec des mots de passe d'application (App Passwords) :

1. Activez l'authentification à deux facteurs sur votre compte Gmail
2. Générez un mot de passe d'application spécifiquement pour l'application Mail Archiver
3. Utilisez le mot de passe d'application au lieu de votre mot de passe Gmail habituel lors de la configuration du Compte dans Mail Archiver

## 🏷️ Gmail Label Settings {#gmail-label-settings}

Il est fortement recommandé d'ajuster vos paramètres d'étiquettes Gmail pour éviter que l'étiquette "Tous les messages" ne soit exposée via IMAP :

1. Accédez à l'interface web de Gmail et ouvrez les paramètres (petite icône d'engrenage en haut) → Voir tous les paramètres → Onglet Libellés
2. Recherchez l'étiquette "Tous les messages" dans la liste
3. Décochez l'option "Afficher en IMAP" pour "Tous les messages"

### 📚 Background on Gmail Label Implementation {#background-on-gmail-label-implementation}

Le système d'étiquettes de Gmail fonctionne différemment des fournisseurs de messagerie traditionnels. Dans Gmail :
- Tous les e-mails existent dans un répertoire unique "Tous les messages"
- Les étiquettes agissent comme des tags plutôt que comme des dossiers
- L'étiquette "Tous les messages" contient chaque e-mail de votre Compte
- Lorsqu'un e-mail est étiqueté, il apparaît à la fois dans le Dossier d'origine et dans la section étiquetée. Ainsi, tous les messages de votre boîte de réception apparaissent également dans l'étiquette "Tous les messages".

### ⚠️ Issues with IMAP Retrieval {#issues-with-imap-retrieval}

Lorsque "Tous les messages" est exposé via IMAP, cela peut causer plusieurs problèmes :

1. **Duplicate Processing** : L'Archiveur peut traiter le même e-mail plusieurs fois puisqu'il apparaît dans plusieurs dossiers/étiquettes
2. **Performance Impact** : La récupération de "Tous les messages" peut être extrêmement lente pour les comptes volumineux contenant des milliers d'e-mails
4. **Synchronization Issues** : Des conflits peuvent survenir lors de la tentative de déterminer à quel Dossier un e-mail appartient réellement

En désactivant l'accès IMAP à "Tous les messages", vous garantissez que les e-mails ne sont récupérés qu'une seule fois depuis leur emplacement principal, améliorant ainsi la performance et la précision.

## 🗑️ Retention Policy and Expunge Settings {#retention-policy-and-expunge-settings}

Comme mentionné dans le README principal, les comptes Gmail nécessitent une attention particulière concernant les politiques de Conservation :

- La fonction Auto-Expunge de Gmail doit être désactivée dans les paramètres Gmail sous l'onglet "Transfert et POP/IMAP"
- Ce paramètre est crucial pour que les politiques de Conservation fonctionnent correctement avec les comptes Gmail

Pour configurer ce paramètre :
1. Allez dans les Paramètres Gmail → Voir tous les paramètres → Onglet Transfert et POP/IMAP
2. Dans la section "Accès IMAP", sélectionnez "Auto-Expunge désactivé - Attendre que le client mette à jour le serveur." et "Déplacer le message vers la Corbeille"

## ⚠️ Gmail IMAP Rate Limiting {#gmail-imap-rate-limiting}

Gmail impose une limite de débit quotidienne de 2500 Mo pour la récupération IMAP. Lors de l'archivage de gros volumes d'e-mails, sachez que vous pourriez atteindre cette limite, ce qui pourrait suspendre temporairement la récupération des e-mails jusqu'au lendemain.

### 📊 Rate Limit Handling {#rate-limit-handling}

Mail Archiver fournit une gestion intégrée des limites de débit pour gérer les limites de bande passante IMAP de Gmail :

- **Automatic bandwidth tracking** : Surveille les données téléchargées par Compte par jour
- **Configurable limits** : Définit `BandwidthTracking__DailyLimitMb=2500` pour Gmail
- **Automatic pause** : La Synchronisation s'interrompt lorsque la limite est atteinte et reprend après la réinitialisation
- **Checkpoint system** : La progression de la Synchronisation est sauvegardée et reprise à partir de la dernière position

Pour une configuration et une utilisation détaillées, consultez le **[Rate Limit Handling Guide](RateLimitHandling.md)**.

### 🔧 Gmail Configuration Example {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 Large Mailbox Considerations {#large-mailbox-considerations}

Pour les Boîtes aux lettres Gmail volumineuses (ex: 14 Go), la Synchronisation initiale prendra plusieurs jours :

| Mailbox Size | Days to Complete (at 2500 MB/day) |
|--------------|-----------------------------------|
| 5 GB | ~2 days |
| 10 GB | ~4 days |
| 14 GB | ~6 days |
| 20 GB | ~8 days |

**Conseil** : Activez le suivi de la bande passante **avant** d'ajouter de gros comptes Gmail pour assurer une gestion correcte des limites de débit dès le début.

## ✅ Best Practices Summary {#best-practices-summary}

Le respect de ces bonnes pratiques aidera à assurer un fonctionnement fluide de Mail Archiver avec vos comptes Gmail tout en maintenant l'intégrité des données et des performances optimales.
