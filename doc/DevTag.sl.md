# 🧪 Uporaba razvojnih različic (Dev Tag) {#using-development-versions-dev-tag}

[← Nazaj na indeks dokumentacije](index.md)

## 📋 Pregled {#overview}

Ta vodnik pojasnjuje, kako preiti iz stabilne različice `latest` na razvojno različico `dev` Mail Archiverja za testiranje popravkov napak in novih funkcij. To je posebej uporabno za uporabnike, ki želijo pretešti popravke, preden so uradno izdani.

## ⚠️ Pomembna predvsem razpoložljiva razčlenitve {#important-considerations}

Pred prehodom na razvojno različico upoštevajte naslednje:

- **Razvojne različice lahko vsebujejo napake ali nestabilne funkcije**, ki niso prisotne v stabilni izdaji
- **Vedno naredite varnostno kopijo svojih podatkov**, preden preidete na razvojno različico
- **Razvojne različice uporabljajte le za testiranje** in ne v produkciskih okoljih
- **Prijavite vse težave**, ki jih naletite, da pomagate izboljšati programsko opremo

## 🛑 Ustavitev kontejnerja (priporočljivo) {#stopping-the-container-recommended}

Pred prehodom na razvojno različico je priporočljivo ustaviti kontejner:

```bash
docker compose down
```

## 🔄 Prehod na Dev Tag {#switching-to-the-dev-tag}

1. Odprite svojo datoteko `docker-compose.yml` v besedilnem urejevalniku

2. Poiščite razdelek storitve `mailarchive-app` in spremenite oznako slike (image tag) iz `latest` v `dev`:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. Shranite spremembe v svojo datoteko `docker-compose.yml`

## 📥 Prenos razvojne slike {#pulling-the-development-image}

Po posodobitvi datoteke docker-compose morate prenesti razvojno sliko:

```bash
docker compose pull
```

Ta ukaz bo predownloadal najnovejšo razvojno različico slike Mail Archiver.

## ▶️ Zagon kontejnerja z novo različico {#starting-the-container-with-the-new-version}

Po prenosu nove slike zaženite kontejner, da uporabite razvojno različico:

```bash
docker compose up -d
```

## 🔁 Vrnitev na stabilno izdajo {#switching-back-to-stable-release}

Če se želite vrniti na stabilno različico, sledite tem korakom:

1. Ustavite kontejner:
```bash
docker compose down
```

2. Uredite `docker-compose.yml` in vrnite sliko na:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. Prenesite najnovejšo sliko:
```bash
docker compose pull
```

4. Zaženite kontejner:
```bash
docker compose up -d
```

## 📝 Dodatne opombe {#additional-notes}

- Razvojne različice se posodabljajo pogosteje kot stabilne izdaje
- Če naletite na težave, preverite dnevniške zapis (logs) za sporočila o napakah:
  ```bash
  docker compose logs mailarchive-app
  ```
- Trenutno delujočo različico lahko preverite s pregledom informacij o sliki kontejnerja:
  ```bash
  docker compose images
  ```
