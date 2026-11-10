# 🖥️ CLI Commands {#cli-commands}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

Alcune operazioni non appartengono all'interfaccia web: importare un Archivio esistente dal file system locale o spostare i messaggi originali memorizzati tra i backend di archiviazione. Mail Archiver accetta entrambi come comandi sulla riga di comando.

| Comando | Scopo |
|----------|---------|
| `--import-mbox` | Importa un file MBox da una directory montata |
| `--import-eml` | Importa un Archivio ZIP di file EML da una directory montata |
| `--migrate-raw-storage` | Sposta i messaggi originali memorizzati tra il database e il file system |

Tutti i comandi vengono gestiti prima dell'avvio della pipeline web e terminano una volta completati, pertanto è sicuro eseguirli con `docker compose exec` mentre l'Archivio è in esecuzione.

## ⚠️ Modello di sicurezza {#security-model}

I comandi di importazione sono **solo CLI** – non esiste un endpoint web per la navigazione o l'importazione di file locali. È necessario avere l'accesso all'host Docker (`docker exec`) per eseguirli.

Una **whitelist dei percorsi** (`LocalImport:AllowedPaths`) controlla quali directory l'importatore può leggere. I file al di fuori di questi percorsi vengono rifiutati.

`--migrate-raw-storage` non è soggetto a tale whitelist: legge solo i file dei messaggi che l'Archivio stesso ha scritto sotto `RawMessages:FileSystem:BasePath`.

## 🛠️ Configurazione {#setup}

### 1. Configurare i percorsi consentiti {#1-configure-allowed-paths}

Nel tuo `docker-compose.yml`, imposta i percorsi di importazione consentiti:

```yaml
environment:
  - LocalImport__AllowedPaths__0=/data/import
  # Optional: multiple paths
  # - LocalImport__AllowedPaths__1=/data/import2
```

### 2. Montare i file {#2-mount-your-files}

Monta la directory contenente i tuoi file MBox/EML nel container:

```yaml
services:
  mailarchive-app:
    volumes:
      - /home/user/mbox-archives:/data/import
      - ./data-protection-keys:/app/DataProtection-Keys
```

Sostituisci `/home/user/mbox-archives` con il percorso effettivo sull'host Docker.

### 3. Riavviare il container {#3-restart-the-container}

```bash
docker compose down && docker compose up -d
```

## 📝 Utilizzo {#usage}

### Trova l'ID dell'Account di destinazione {#find-your-target-account-id}

Prima dell'importazione, è necessario l'ID del database dell'Account di posta di destinazione. È possibile trovarlo tramite la Web UI:

Navigare in "Email Accounts", aprire i dettagli dell'Account di destinazione; l'ID dell'Account è visualizzato nell'elenco degli Account o nell'URL durante la visualizzazione di un Account.

### Importa un file MBox {#import-an-mbox-file}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-mbox --file /data/import/myarchive.mbox --account-id 1 --folder INBOX
```

**Parametri:**
| Parametro | Richiesto | Descrizione |
|-----------|----------|-------------|
| `--import-mbox` | Sì | Segnala un'operazione di importazione MBox |
| `--file <path>` | Sì | Percorso al file MBox all'interno del contenitore |
| `--account-id <id>` | Sì | ID del database dell'Account di posta di destinazione |
| `--folder <name>` | No | Nome della Cartella di destinazione (predefinito: `INBOX`) |

### Importa file EML (Archivio ZIP) {#import-eml-files-zip-archive}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll --import-eml --file /data/import/emails.zip --account-id 1
```

**Parametri:**
| Parametro | Richiesto | Descrizione |
|-----------|----------|-------------|
| `--import-eml` | Sì | Segnala un'operazione di importazione EML |
| `--file <path>` | Sì | Percorso a un **Archivio ZIP** contenente i file `.eml` all'interno del contenitore |
| `--account-id <id>` | Sì | ID del database dell'Account di posta di destinazione |

> **Importante:** `--import-eml` si aspetta un **Archivio ZIP** contenente i file `.eml` — **non** accetta singoli file `.eml`. Per importare singoli file EML, racchiuderli prima in un Archivio ZIP:
> ```bash
> zip /data/import/emails.zip /data/import/*.eml
> ```
> 
> **Nota per `dotnet run`:** Quando si utilizza `dotnet run` (invece della DLL pubblicata), separare gli argomenti di dotnet dagli argomenti dell'applicazione con `--`:
> ```bash
> dotnet run -- --import-eml --file /data/import/emails.zip --account-id 1
> ```
> Il `--` comunica a `dotnet run` che tutti gli argomenti successivi appartengono alla propria applicazione, non al compilatore .NET.

## 📊 Esempio di output {#example-output}

```
Target account: user@example.com (ID: 1)

=== Local MBox Import ===
File: /data/import/myarchive.mbox
Size: 245.32 MB
Target Account ID: 1
Target Folder: INBOX

[Importing... progress logged to container logs]

=== Import Results ===
Status: Completed
Total Emails: 15234
Imported Successfully: 15120
Failed: 42
Skipped (malformed): 28
Skipped (duplicates): 44
Duration: 00:12:45
```

## 🔍 Monitoraggio {#monitoring}

Durante l'importazione, il progresso dettagliato viene scritto nei log del container:

```bash
docker compose logs -f mailarchive-app
```

## ⚙️ Riferimento di Configurazione {#configuration-reference}

| Variabile d'ambiente | Predefinito | Descrizione |
|---------------------|---------|-------------|
| `LocalImport__AllowedPaths__0` | `/data/import` | Primo percorso consentito per gli import locali |
| `LocalImport__AllowedPaths__1` | (empty) | Secondo percorso consentito (opzionale) |

Aggiungi altri percorsi incrementando l'indice (`__2`, `__3`, ecc.).

## 🚫 Gestione degli errori {#error-handling}

| Errore | Causa | Soluzione |
|-------|-------|----------|
| `File not found` | Il percorso `--file` non esiste nel container | Verificare il percorso del file e il volume mount |
| `File path is not in an allowed import directory` | Il file si trova al di fuori dei percorsi in whitelist | Spostare il file in un percorso consentito o aggiungere la sua directory a `LocalImport:AllowedPaths` |
| `Mail account with ID X not found` | ID Account errato | Verificare l'ID dell'Account tramite la Web UI o una query al database |
| `Invalid account-id` | Valore non numerico per `--account-id` | Utilizzare un ID numerico del database |

## 🔒 Best Practice di Sicurezza {#security-best-practices}

1. **Utilizzare mount in sola lettura** quando possibile:
   ```yaml
   volumes:
     - /home/user/mbox-archives:/data/import:ro
   ```

2. **Restringere AllowedPaths** solo alle directory che si utilizzano effettivamente.

3. **Rimuovere i volume mount** dopo il completamento dell'importazione per mantenere una superficie di sicurezza pulita.

4. **Non esporre mai** la directory di importazione tramite il web server – non esiste alcun endpoint web per le importazioni locali.

## 📦 Formati di Importazione in Breve {#import-formats-at-a-glance}

| CLI Flag | Formato di Input | Estensione File Tipica | Singola Email | Bulk |
|----------|-------------|----------------------|--------------|------|
| `--import-mbox` | File MBox | `.mbox`, `.mbx` | ❌ (sempre bulk) | ✅ |
| `--import-eml` | Archivio ZIP di EML | `.zip` | ❌ (richiesto ZIP) | ✅ |
| `--migrate-raw-storage` | Messaggi originali archiviati | — | — | ✅ |

*I singoli file `.eml` non sono supportati direttamente. Usa `zip` per racchiuderli, oppure importa tramite il [web upload](Import-EML-MBox.md).*

## 🔧 Comandi di Manutenzione {#maintenance-commands}

### Spostamento dei messaggi originali archiviati {#moving-stored-original-messages}

Quando `RawMessages:Enabled` è attivo, il messaggio originale di ogni email viene conservato nel database o in file, a seconda di `RawMessages:Storage`. Questo comando sposta gli originali già archiviati da un backend all'altro; consultare [Original Message Storage](RawMessages.md#storage-backend) per la configurazione.

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

| Argument | Default | Description |
|----------|---------|-------------|
| `--from` | configured `RawMessages:Storage` | Backend da cui i messaggi vengono spostati. |
| `--to` | configured `RawMessages:Storage` | Backend in cui i messaggi vengono spostati. |
| `--batch` | `200` | Messaggi per transazione del database. |

A differenza dei comandi di importazione, questo non è limitato da `LocalImport:AllowedPaths`: legge solo i file dei messaggi che l'Archivio stesso ha scritto sotto `RawMessages:FileSystem:BasePath`.

**Cosa garantisce:**

- Ogni messaggio viene verificato rispetto al checksum registrato al momento dell'archiviazione prima di essere copiato. Un messaggio che fallisce viene lasciato esattamente dove si trova e segnalato — un originale corrotto non viene mai propagato e la sua unica copia rimanente non viene mai eliminata.
- Un file viene eliminato solo dopo che la transazione del database che lo ha spostato è stata confermata, quindi un'esecuzione interrotta non può perdere l'unica copia.
- L'esecuzione è riprendibile: il lavoro viene selezionato in base alla memorizzazione registrata su ogni messaggio, quindi riavviando il comando si continua con ciò che è ancora in sospeso.
- Un messaggio errato non interrompe l'esecuzione. Il codice di uscita è `1` se un messaggio fallisce, `0` altrimenti.

**Esempio di output:**

```text
Batch: 200 moved, 0 skipped, 0 failed
Batch: 200 moved, 0 skipped, 0 failed
Batch: 42 moved, 0 skipped, 1 failed
Done: 442 moved, 1 failed.
  Email 12345: checksum mismatch (recorded 4f1c..., file has 9ab2...)
```

Le letture seguono sempre il backend registrato su ogni messaggio, non l'impostazione configurata, quindi un download continua a funzionare durante e dopo una migrazione. Modificare `RawMessages:Storage` influisce solo sulla posta di nuova archiviazione — eseguire questo comando per spostare ciò che è già archiviato.

> **Backup:** con il backend del filesystem i file dei messaggi risiedono all'esterno del database. Effettuare il Backup di `RawMessages:FileSystem:BasePath` insieme al dump del database; un Ripristino senza i file lascia gli originali che non possono essere serviti.

## ❓ FAQ {#faq}

**D: Perché `--import-eml` richiede un file ZIP?**
R: Il servizio di importazione EML sottostante (`EmlImportService`) elabora internamente gli Archivio ZIP utilizzando l'API `ZipFile` di .NET. Questo design supporta in modo efficiente le importazioni massive. Per i singoli file `.eml`, racchiudili in un Archivio ZIP (ad es. `zip emails.zip *.eml`) o utilizza il [web upload](Import-EML-MBox.md).

**D: Perché non posso sfogliare i file dall'interfaccia web?**
R: Si tratta di una scelta di progettazione intenzionale per la sicurezza. Questa funzione è implementata intenzionalmente tramite CLI per le interazioni a livello di sistema, poiché gli utenti normali non hanno accesso diretto al livello del sistema operativo; interagiscono solo con l'interfaccia Web.

Per prevenire rischi di sicurezza come gli attacchi di path traversal e l'esposizione non autorizzata del file system, gli utenti regolari sono limitati all'uso dell'interfaccia Web per il caricamento e l'importazione di file. Ciò garantisce che non possano bypassare l'interfaccia prevista per accedere al file system del container sottostante.
