# 🚨 Récupération d'urgence du Compte {#emergency-account-recovery}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide fournit des instructions détaillées pour récupérer l'accès à l'application Mail Archiver lorsque vous avez oublié le mot de passe administrateur. Cette procédure d'urgence vous permet de retrouver l'accès à votre système sans perdre de données.

> 📝 **Exigence de version** : Cette méthode de récupération est disponible dans la version 2601.1 de Mail Archiver et les versions ultérieures.

## ⚠️ Avis de sécurité important {#important-security-notice}

Cette méthode de récupération ne doit être utilisée que dans des situations d'urgence lorsque vous ne pouvez pas accéder à votre Compte administrateur. Elle nécessite une modification temporaire de la configuration de votre application et doit être annulée immédiatement après la récupération du mot de passe.

## 🛠️ Étapes de récupération {#recovery-steps}

### 1. Créer un Compte administrateur d'urgence {#1-create-emergency-administrator-account}

Modifiez vos variables d'environnement `appsettings.json` ou Docker pour créer un Compte administrateur d'urgence temporaire :

**Pour Docker Compose (Recommandé) :**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Emergency admin account
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempPass123!
      # ... other settings remain unchanged
```

**Pour une modification directe de appsettings.json :**
```json
{
  "Authentication": {
    "Username": "emergency_admin",
    "Password": "TempPass123!",
    // ... other settings remain unchanged
  }
}
```

### 2. Redémarrer l'application/le conteneur {#2-restart-the-applicationcontainer}

Après avoir modifié la configuration, redémarrez votre application :

**Pour Docker Compose :**
```bash
docker compose down
docker compose up -d
```

**Pour l'application directe :**
```bash
# Restart your application service
```

### 3. Connexion avec le Compte d'urgence {#3-login-with-emergency-account}

1. Accédez à l'interface web de Mail Archiver
2. Connectez-vous en utilisant les identifiants d'urgence :
   - **Username** : `emergency_admin` (ou ce que vous avez défini)
   - **Password** : `TempPass123!` (ou ce que vous avez défini)

### 4. Réinitialiser le mot de passe de l'administrateur d'origine {#4-reset-original-administrator-password}

1. Naviguez vers la section "Users" depuis le menu principal
2. Trouvez votre Compte administrateur d'origine dans la liste des utilisateurs
3. Cliquez sur "Edit" pour cet utilisateur
4. Changez le mot de passe par un nouveau mot de passe sécurisé
5. Cliquez sur "Save Changes"

### 5. Restaurer la configuration d'origine {#5-restore-original-configuration}

Rétablissez vos modifications de configuration vers le nom d'utilisateur administrateur d'origine :

**Pour Docker Compose :**
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Original admin account
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword
      # ... other settings
```

**Pour appsettings.json direct :**
```json
{
  "Authentication": {
    "Username": "admin",
    "Password": "YourNewSecurePassword",
    // ... other settings
  }
}
```

### 6. Redémarrer l'application à nouveau {#6-restart-application-again}

Redémarrez l'application avec la configuration restaurée :

**Pour Docker Compose :**
```bash
docker compose down
docker compose up -d
```

## 🎯 Exemple : Processus complet de récupération via Docker Compose {#example-complete-docker-compose-recovery-process}

Voici un exemple complet du processus de récupération d'urgence utilisant Docker Compose :

### Étape 1 : Configuration d'urgence {#step-1-emergency-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # EMERGENCY ADMIN ACCOUNT
      - Authentication__Username=emergency_admin
      - Authentication__Password=TempEmergencyPass2026!
      
      # Other settings remain unchanged...
      - MailSync__IntervalMinutes=15
      - TimeZone__DisplayTimeZoneId=Europe/Berlin
      # ... rest of configuration
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Étape 2 : Redémarrage et Connexion {#step-2-restart-and-login}
```bash
docker compose down
docker compose up -d
# Wait for application to start, then login with emergency_admin/TempEmergencyPass2026!
```

### Étape 3 : Réinitialisation du mot de passe d'origine {#step-3-reset-original-password}
1. Connectez-vous à l'interface web avec les identifiants d'urgence
2. Allez dans la section Users
3. Modifiez l'utilisateur admin d'origine et définissez un nouveau mot de passe
4. Enregistrez les modifications

### Étape 4 : Restauration de la configuration normale {#step-4-restore-normal-configuration}
```yaml
version: '3.8'
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    restart: always
    environment:
      # Database Connection
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=MailArchiver;Username=mailuser;Password=your_secure_password;
      
      # RESTORED ORIGINAL ADMIN ACCOUNT
      - Authentication__Username=admin
      - Authentication__Password=YourNewSecurePassword2026!
      
      # ... rest of configuration unchanged
    ports:
      - "5000:5000"
    networks:
      - postgres
    volumes:
      - ./data-protection-keys:/app/DataProtection-Keys
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:17-alpine
    # ... postgres configuration unchanged
```

### Étape 5 : Redémarrage final {#step-5-final-restart}
```bash
docker compose down
docker compose up -d
```

## 🔑 Perte de la clé de chiffrement des identifiants {#lost-credential-encryption-key}

Si `Security__CredentialEncryptionKey` (chiffrement des identifiants au repos) est perdu, les identifiants de la Boîte aux lettres stockés ne peuvent plus être déchiffrés. L'application ne plante **pas** : les valeurs affectées sont traitées comme "aucun identifiant" et un avertissement est consigné dans le journal. Pour récupérer :

1. Restaurez la clé à partir de votre Sauvegarde / gestionnaire de secrets et redémarrez l'application. Les identifiants fonctionnent à nouveau immédiatement, aucun changement de données n'est requis.
2. Si la clé est définitivement perdue, les identifiants chiffrés ne peuvent pas être récupérés. Les Comptes affectés sont faciles à identifier : leur Synchronisation échoue et un avertissement de déchiffrement est écrit dans le journal de l'application (et apparaît comme un problème de synchronisation dans l'interface utilisateur).
3. Ouvrez chaque Compte affecté sous **Mail Accounts → Edit** et saisissez à nouveau ses identifiants : le mot de passe IMAP, le client secret M365, ou ré-autorisez le Compte (MSA). L'enregistrement du formulaire rechiffre l'identifiant avec la clé désormais configurée — aucune modification manuelle de la base de données n'est nécessaire.

> 💡 La clé n'est jamais stockée dans la base de données, donc les Sauvegardes de la base de données seules ne sont pas suffisantes — sauvegardez toujours la clé séparément.

## 🔒 Bonnes pratiques de sécurité {#security-best-practices}

1. **Utilisez des mots de passe temporaires forts** : Rendez votre mot de passe d'urgence complexe et unique
2. **Changez-le immédiatement** : Réinitialisez le mot de passe d'urgence dès que vous avez retrouvé l'accès
