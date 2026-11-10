# 🧪 Gebruik van Development Versies (Dev Tag) {#using-development-versions-dev-tag}

[← Terug naar Documentatie Index](index.md)

## 📋 Overzicht {#overview}

Deze gids legt uit hoe u kunt overschakelen van de stabiele `latest` versie naar de development `dev` versie van Mail Archiver om bugfixes en nieuwe functies te testen. Dit is vooral nuttig voor gebruikers die fixes willen testen voordat ze officieel worden uitgebracht.

## ⚠️ Belangrijke Overwegingen {#important-considerations}

Overweeg het volgende voordat u overschakelt naar de development versie:

- **Development versies kunnen bugs of onstabiele functies bevatten** die niet aanwezig zijn in de stabiele release
- **Maak altijd een Back-up van uw gegevens** voordat u overschakelt naar een development versie
- **Gebruik development versies alleen voor testen** en niet in productieomgevingen
- **Meld eventuele problemen** die u tegenkomt om te helpen de software te verbeteren

## 🛑 De Container Stoppen (Aanbevolen) {#stopping-the-container-recommended}

Voordat u overschakelt naar de development versie, wordt het aanbevolen om de container te stoppen:

```bash
docker compose down
```

## 🔄 Overschakelen naar de Dev Tag {#switching-to-the-dev-tag}

1. Open uw `docker-compose.yml` bestand in een tekstverwerker

2. Zoek de `mailarchive-app` service sectie en wijzig de image tag van `latest` naar `dev`:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. Sla de wijzigingen op in uw `docker-compose.yml` bestand

## 📥 De Development Image Pullen {#pulling-the-development-image}

Na het bijwerken van het docker-compose bestand moet u de development image pullen:

```bash
docker compose pull
```

Dit commando downloadt de nieuwste development versie van de Mail Archiver image.

## ▶️ De Container Starten met de Nieuwe Versie {#starting-the-container-with-the-new-version}

Na het pullen van de nieuwe image, start u de container om de development versie te gebruiken:

```bash
docker compose up -d
```

## 🔁 Terugschakelen naar de Stabiele Release {#switching-back-to-stable-release}

Als u wilt terugkeren naar de stabiele versie, volgt u deze stappen:

1. Stop de container:
```bash
docker compose down
```

2. Bewerk `docker-compose.yml` en wijzig de image terug naar:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. Pull de nieuwste image:
```bash
docker compose pull
```

4. Start de container:
```bash
docker compose up -d
```

## 📝 Aanvullende Notities {#additional-notes}

- Development versies worden vaker bijgewerkt dan stabiele releases
- Als u problemen tegenkomt, controleer dan de logs op foutmeldingen:
  ```bash
  docker compose logs mailarchive-app
  ```
- U kunt controleren welke versie momenteel wordt uitgevoerd door naar de image informatie van de container te kijken:
  ```bash
  docker compose images
  ```
