# 🧪 Utilizzo delle versioni di sviluppo (Dev Tag) {#using-development-versions-dev-tag}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida spiega come passare dalla versione stabile `latest` alla versione di sviluppo `dev` di Mail Archiver per testare correzioni di bug e nuove funzionalità. Questo è particolarmente utile per gli utenti che desiderano testare le correzioni prima che vengano rilasciate ufficialmente.

## ⚠️ Considerazioni importanti {#important-considerations}

Prima di passare alla versione di sviluppo, si prega di considerare quanto segue:

- **Le versioni di sviluppo possono contenere bug o funzionalità instabili** che non sono presenti nel rilascio stabile
- **Effettuare sempre il Backup dei dati** prima di passare a una versione di sviluppo
- **Utilizzare le versioni di sviluppo solo per il testing** e non in ambienti di produzione
- **Segnalare eventuali problemi** riscontrati per aiutare a migliorare il software

## 🛑 Arresto del container (Consigliato) {#stopping-the-container-recommended}

Prima di passare alla versione di sviluppo, si raccomanda di arrestare il container:

```bash
docker compose down
```

## 🔄 Passaggio al Dev Tag {#switching-to-the-dev-tag}

1. Apri il tuo file `docker-compose.yml` in un editor di testo

2. Individua la sezione del servizio `mailarchive-app` e cambia il tag dell'immagine da `latest` a `dev`:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:dev  # Changed from :latest to :dev
    # ... other configuration remains the same
```

3. Salva le modifiche nel tuo file `docker-compose.yml`

## 📥 Download dell'immagine di sviluppo {#pulling-the-development-image}

Dopo aver aggiornato il file docker-compose, è necessario scaricare l'immagine di sviluppo:

```bash
docker compose pull
```

Questo comando scaricherà l'ultima versione di sviluppo dell'immagine di Mail Archiver.

## ▶️ Avvio del container con la nuova versione {#starting-the-container-with-the-new-version}

Dopo aver scaricato la nuova immagine, avvia il container per utilizzare la versione di sviluppo:

```bash
docker compose up -d
```

## 🔁 Ritorno al rilascio stabile {#switching-back-to-stable-release}

Se desideri tornare alla versione stabile, segui questi passaggi:

1. Arresta il container:
```bash
docker compose down
```

2. Modifica `docker-compose.yml` e riporta l'immagine a:
```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
```

3. Scarica l'ultima immagine:
```bash
docker compose pull
```

4. Avvia il container:
```bash
docker compose up -d
```

## 📝 Note aggiuntive {#additional-notes}

- Le versioni di sviluppo vengono aggiornate più frequentemente rispetto ai rilasci stabili
- Se riscontri problemi, controlla i log per i messaggi di errore:
  ```bash
  docker compose logs mailarchive-app
  ```
- Puoi verificare quale versione è attualmente in esecuzione controllando le informazioni sull'immagine del container:
  ```bash
  docker compose images
  ```
