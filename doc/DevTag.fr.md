# 🧪 Utilisation des versions de développement (Dev Tag) {#using-development-versions-dev-tag}

[← Retour à l'index de la documentation](index.md)

## 📋 Aperçu {#overview}

Ce guide explique comment passer de la version stable `latest` à la version de développement `dev` de Mail Archiver pour tester les corrections de bugs et les nouvelles fonctionnalités. Ceci est particulièrement utile pour les utilisateurs qui souhaitent tester les correctifs avant leur sortie officielle.

## ⚠️ Considérations importantes {#important-considerations}

Avant de passer à la version de développement, veuillez prendre en compte les points suivants :

- **Les versions de développement peuvent contenir des bugs ou des fonctionnalités instables** qui ne sont pas présentes dans la version stable
- **Effectuez toujours une Sauvegarde de vos données** avant de passer à une version de développement
- **Utilisez les versions de développement uniquement pour les tests** et non dans des environnements de production
- **Signalez tout problème** rencontré pour aider à améliorer le logiciel

## 🛑 Arrêt du conteneur (Recommandé) {#stopping-the-container-recommended}

Avant de passer à la version de développement, il est recommandé d'arrêter le conteneur :

```bash
docker compose down
```

## 🔄 Passage au Dev Tag {#switching-to-the-dev-tag}

1. Ouvrez votre fichier `docker-compose.yml` dans un éditeur de texte

2. Localisez la section du service `mailarchive-app` et modifiez le tag de l'image de `latest` à `dev` :
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. Enregistrez les modifications dans votre fichier `docker-compose.yml`

## 📥 Récupération de l'image de développement {#pulling-the-development-image}

Après avoir mis à jour le fichier docker-compose, vous devez récupérer l'image de développement :

```bash
docker compose pull
```

Cette commande téléchargera la dernière version de développement de l'image Mail Archiver.

## ▶️ Démarrage du conteneur avec la nouvelle version {#starting-the-container-with-the-new-version}

Après avoir récupéré la nouvelle image, démarrez le conteneur pour utiliser la version de développement :

```bash
docker compose up -d
```

## 🔁 Retour à la version stable {#switching-back-to-stable-release}

Si vous souhaitez revenir à la version stable, suivez ces étapes :

1. Arrêtez le conteneur :
```bash
docker compose down
```

2. Modifiez `docker-compose.yml` et remettez l'image à :
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. Récupérez la dernière image :
```bash
docker compose pull
```

4. Démarrez le conteneur :
```bash
docker compose up -d
```

## 📝 Notes additionnelles {#additional-notes}

- Les versions de développement sont mises à jour plus fréquemment que les versions stables
- Si vous rencontrez des problèmes, vérifiez les journaux pour trouver des messages d'erreur :
  ```bash
  docker compose logs mailarchive-app
  ```
- Vous pouvez vérifier quelle version est actuellement en cours d'exécution en consultant les informations de l'image du conteneur :
  ```bash
  docker compose images
  ```
