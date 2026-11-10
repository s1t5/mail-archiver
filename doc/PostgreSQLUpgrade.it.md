# 🔄 Guida all'aggiornamento della versione maggiore di PostgreSQL {#postgresql-major-version-upgrade-guide}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida spiega come aggiornare il database PostgreSQL da una versione maggiore a un'altra nel tuo ambiente Mail Archiver Docker Compose. Gli aggiornamenti della versione maggiore sono necessari per sfruttare le nuove funzionalità di PostgreSQL, i miglioramenti delle prestazioni e gli aggiornamenti di sicurezza.

## ⚠️ Disclaimer importante {#important-disclaimer}
> **Nota**: Questa procedura di aggiornamento è un processo di migrazione generico del database PostgreSQL e non è specifica per Mail Archiver. Viene fornita come raccomandazione basata sulle best practice di PostgreSQL. Testare sempre la procedura in un ambiente non di produzione e assicurarsi di avere Backup adeguati prima di procedere.


**Tutte le procedure descritte in questa guida devono essere testate prima in un ambiente non di produzione.** Gli aggiornamenti della versione maggiore comportano la migrazione dei dati e comportano rischi di perdita o corruzione dei dati. Sempre:
- Creare un Backup completo prima di iniziare il processo di aggiornamento
- Testare la procedura di aggiornamento in un ambiente di sviluppo
- Verificare tutte le funzionalità dell'applicazione dopo l'aggiornamento
- Avere un piano di rollback in caso di problemi
- Programmare gli aggiornamenti durante le finestre di manutenzione

**Utilizzare a proprio rischio.**

## 🎯 Quando aggiornare {#when-to-upgrade}

Considera l'aggiornamento di PostgreSQL quando:
- La versione corrente si sta avvicinando alla fine del ciclo di vita (end-of-life)
- Gli aggiornamenti di sicurezza richiedono una versione più recente
- Stai pianificando una finestra di manutenzione importante del sistema

## 🛠️ Prerequisiti {#prerequisites}

- Setup Mail Archiver Docker funzionante (vedi [Guida alla configurazione](Setup.md))
- Spazio su disco sufficiente (almeno 1,5 volte la dimensione attuale del database)
- Accesso al sistema host Docker
- Backup completo dell'attuale database (vedi [Guida al Backup e Ripristino](BackupRestore.md))
- Finestra di manutenzione per il downtime dell'applicazione

## 📋 Configurazione attuale {#current-configuration}

Il tuo setup attuale di Mail Archiver utilizza:
- **Nome Database**: MailArchiver
- **Utente Database**: mailuser
- **Volume Dati**: `./postgres-data` mappato su `/var/lib/postgresql/data`

Se necessario, ciò deve essere regolato nei seguenti esempi di comandi e script.

## 🔄 Procedure di aggiornamento {#upgrade-procedures}

Questo metodo crea un backup logico dei tuoi dati e lo ripristina nella nuova versione di PostgreSQL. È l'approccio più sicuro e funziona tra qualsiasi versione di PostgreSQL.

> **Metodi alternativi**: PostgreSQL fornisce anche altri metodi di aggiornamento come `pg_upgrade` per aggiornamenti più rapidi. Per ulteriori informazioni sugli approcci di aggiornamento alternativi, consultare la [documentazione ufficiale per l'aggiornamento di PostgreSQL](https://www.postgresql.org/docs/current/upgrading.html).

#### Passaggio 1: Preparare l'ambiente {#step-1-prepare-the-environment}

```bash
# 1. Stop the application container (keep database running for backup)
docker compose stop mailarchive-app

# 2. Create a backup directory
mkdir -p postgres-backup

# 3. Create a database dump
echo "Export DB content (this can take some time)"
docker compose exec -T postgres pg_dump -U mailuser -d MailArchiver > postgres-backup/database_dump.sql

# 4. Verify the dump was created successfully
ls -la postgres-backup/database_dump.sql

# 5. Stop the database container
docker compose down
```

#### Passaggio 2: Aggiornare la configurazione Docker Compose {#step-2-update-docker-compose-configuration}

Modifica il tuo file `docker-compose.yml` per utilizzare la nuova versione di PostgreSQL:

```yaml
  postgres:
    image: postgres:18  # Changed from old version
    restart: always
    environment:
      POSTGRES_DB: MailArchiver
      POSTGRES_USER: mailuser
      POSTGRES_PASSWORD: masterkey
    volumes:
      - ./postgres-data-new:/var/lib/postgresql  # New data directory
    networks:
      - postgres
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mailuser -d MailArchiver"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
```

**Modifiche importanti:**
- Aggiornato `image` da `postgres:17` a `postgres:18` (o la tua versione di destinazione)
- Cambiato il mapping del volume da `./postgres-data:/var/lib/postgresql/data` a `./postgres-data-new:/var/lib/postgresql` (la modifica del percorso interno è richiesta dalla versione 18 e successive di postgres)

#### Passaggio 3: Avviare la nuova istanza PostgreSQL {#step-3-start-new-postgresql-instance}

```bash
# Start only the new PostgreSQL container
docker compose up -d postgres

# Wait for the database to be ready
echo "Waiting for new PostgreSQL to start..."
sleep 30

# Verify the new database is ready
docker compose exec -T postgres pg_isready -U mailuser -d MailArchiver
```

#### Passaggio 4: Ripristinare i dati nel nuovo database {#step-4-restore-data-to-new-database}

Ripristinare il dump del database nella nuova istanza PostgreSQL:

```bash
docker compose exec -T postgres psql -U mailuser -d MailArchiver < postgres-backup/database_dump.sql
```
Questo potrebbe richiedere del tempo a seconda della dimensione del database
Prestare attenzione a eventuali messaggi di errore durante il Ripristino

#### Passaggio 5: Testare e completare la migrazione {#step-5-test-and-complete-migration}
Avviare l'applicazione per testare la connettività:

```bash
docker compose up -d
```

Testare le funzionalità dell'applicazione:
- Effettuare il login nell'interfaccia web
- Verificare che la ricerca delle email funzioni
- Controllare che i dati esistenti siano accessibili
- Testare la funzionalità di Backup/Ripristino

Se tutto funziona correttamente, pulire i vecchi dati:

Fermare i container
```bash
docker compose down
```
Rimuovere la vecchia directory dei dati (solo dopo aver confermato che il nuovo setup funzioni)
```bash
rm -rf postgres-data
```
Rimuovere la directory di backup
```bash
rm -rf postgres-backup
```

Rinominare la nuova directory dei dati con il nome standard
```bash
mv postgres-data-new postgres-data
```

Aggiornare docker-compose.yml per utilizzare il nome del volume standard:
```yaml
    volumes:
      - ./postgres-data:/var/lib/postgresql
```

Avviare l'intero sistema
```bash
docker compose up -d
```

## ⚠️ Considerazioni importanti {#important-considerations}

### Pianificazione del downtime {#downtime-planning}

- A seconda del sistema e della dimensione del database, questa procedura può richiedere molto tempo.
- Programmare durante le finestre di manutenzione

### Requisiti di spazio su disco {#disk-space-requirements}

Assicurarsi di avere spazio su disco adeguato (circa il doppio della dimensione del database)

Controllare lo spazio disponibile:
```bash
df -h
du -sh postgres-data/
```

### Piano di rollback {#rollback-plan}

Prepararsi sempre per un rollback:
1. Conservare la vecchia directory dei dati fino a quando l'aggiornamento non è verificato
2. Mantenere il vecchio `docker-compose.yml` backup
3. Testare la procedura di rollback in sviluppo prima di procedere

### Verifica post-aggiornamento {#post-upgrade-verification}

Dopo l'aggiornamento, verificare:
- Connettività dell'applicazione al database
- Funzionalità di ricerca e recupero delle email
- Autenticazione utente e gestione delle sessioni
- Funzionalità di Backup e Ripristino
- Metriche delle prestazioni del database

## 📚 Documentazione correlata {#related-documentation}

- [Guida al Backup e Ripristino](BackupRestore.md) - Essenziale per i backup pre-aggiornamento
- [Guida alla manutenzione del database](DatabaseMaintenance.md) - Manutenzione post-aggiornamento
- [Guida alla configurazione](Setup.md) - Configurazione Docker Compose
- [Note di rilascio di PostgreSQL](https://www.postgresql.org/docs/release/) - Documentazione ufficiale del rilascio
