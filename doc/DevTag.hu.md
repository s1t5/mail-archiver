# 🧪 Fejlesztői verziók használata (Dev Tag) {#using-development-versions-dev-tag}

[← Vissza a dokumentáció indexéhez](index.md)

## 📋 Áttekintés {#overview}

Ez a útmutató elmagyarázza, hogyan válthat át a Mail Archiver stabil `latest` verziójáról a fejlesztői `dev` verzióra a hibajavítások és az új funkciók tesztelése érdekében. Ez különösen hasznos azok számára, akik a hivatalos kiadás előtt szeretnék tesztelni a javításokat.

## ⚠️ Fontos megfontolandók {#important-considerations}

Mielőtt átváltana a fejlesztői verzióra, kérjük, vegye figyelembe az alábbiakat:

- **A fejlesztői verziók tartalmazhatnak hibákat vagy instabil funkciókat**, amelyek nem szerepelnek a stabil kiadásban
- **Mindig készítsen biztonsági mentést az adataiáról**, mielőtt átvált a fejlesztői verzióra
- **A fejlesztői verziókat kizárólag tesztelésre használja**, ne használja termelési környezetben
- **Jelentse minden tapasztalt problémát**, hogy segítsen a szoftver fejlesztésében

## 🛑 A konténer leállítása (Ajánlott) {#stopping-the-container-recommended}

Mielőtt átváltana a fejlesztői verzióra, ajánlott a konténer leállítása:

```bash
docker compose down
```

## 🔄 Átváltás a Dev Tag-re {#switching-to-the-dev-tag}

1. Nyissa meg a `docker-compose.yml` fájlt egy szövegszerkesztőben

2. Keresse meg a `mailarchive-app` service szakaszt, és változtassa meg az image taget `latest`-ről `dev`-ra:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. Mentse el a módosításokat a `docker-compose.yml` fájlban

## 📥 A fejlesztői image letöltése {#pulling-the-development-image}

A docker-compose fájl frissítése után le kell töltenie a fejlesztői imaget:

```bash
docker compose pull
```

Ez a parancs letölti a Mail Archiver image legfrissebb fejlesztői verzióját.

## ▶️ A konténer indítása az új verzióval {#starting-the-container-with-the-new-version}

Az új image letöltése után indítsa el a konténerを a fejlesztői verzió használatához:

```bash
docker compose up -d
```

## 🔁 Visszatérés a stabil kiadáshoz {#switching-back-to-stable-release}

Ha vissza szeretne térni a stabil verzióhoz, kövesse az alábbi lépéseket:

1. Állítsa le a konténer를:
```bash
docker compose down
```

2. Szerkesztje a `docker-compose.yml` fájlt, és változtassa vissza az image-et:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. Húzza le a legfrissebb imaget:
```bash
docker compose pull
```

4. Indítsa el a konténer를:
```bash
docker compose up -d
```

## 📝 További megjegyzések {#additional-notes}

- A fejlesztői verziókat gyakrabban frissítik, mint a stabil kiadásokat
- Ha problémákba ütközik, ellenőrizze a naplót a hibaüzenetekért:
  ```bash
  docker compose logs mailarchive-app
  ```
- Ellenőrizheti, melyik verzió fut éppen a konténer image információinak megtekintésével:
  ```bash
  docker compose images
  ```
