# 📧 Original Message Storage {#original-message-storage}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

Per impostazione predefinita, l'Archivio conserva le **parti analizzate** di un'e-mail: intestazioni, corpi di testo e HTML, Allegati. Un `.eml` esportato viene ricostruito a partire da tali campi.

Questo è sufficiente per leggere un'e-mail, ma non è il messaggio **così come ricevuto**. Le codifiche di trasferimento, i delimitatori MIME e il folding delle intestazioni differiscono dall'originale, pertanto un messaggio ricostruito non verifica più la sua firma DKIM e gli scanner antispam lo valutano in modo diverso.

Con l'archiviazione del messaggio originale abilitata, l'Archivio conserva il messaggio completo così come è arrivato, insieme ai campi analizzati. L'originale memorizzato è ciò che l'Archivio può dimostrare essere stato consegnato.

| | Solo campi analizzati | Con originale memorizzato |
| --- | --- | --- |
| Intestazioni, corpo, Allegati | ✅ | ✅ |
| Messaggio byte-exact | ❌ | ✅ |
| Verifica firma DKIM | ❌ | ✅ |
| Uso forense (tribunale, audit) | limitato | ✅ |

## 🎯 Dove viene servito l'originale {#where-the-original-is-served}

Una volta archiviato, l'originale viene restituito ovunque sia richiesto un messaggio completo:

| Luogo | Cosa succede |
| --- | --- |
| pulsante **Export EML** nella pagina dei dettagli dell'email | Serve l'originale archiviato se presente, altrimenti ricorre alla ricostruzione per le email archiviate senza di esso. |
| **Ripristino in una Casella di posta** | Aggiunge l'originale archiviato se presente, altrimenti ricorre alla ricostruzione per le email archiviate senza di esso. |
| `GET /api/v1/emails/{id}/raw` | Download tramite REST API, vedi la [guida REST API](API.md#download-original-message). |
| `get_email_raw` | Strumento MCP, vedi la [guida MCP](MCP.md#get_email_raw). |

La risposta indica quale versione hai ricevuto, così un chiamante può distinguere un originale autentico da una ricostruzione senza ispezionare i byte:

| Segnale | Significato |
| --- | --- |
| `X-Raw-Message-Source: imap` | Copia esatta dal server IMAP (`BODY[]` come consegnato). |
| `X-Raw-Message-Source: graph` | Copia esatta, il contenuto MIME restituito da Exchange. |
| `X-Raw-Message-Source: eml-file` | Copia esatta, il contenuto del file `.eml` importato. |
| `X-Raw-Message-Source: serialized` | Messaggio analizzato e riscritto. Identico eccetto per la fine riga che è CRLF (importazione MBox, recupero IMAP). |
| `X-Raw-Message-Sha256` | SHA-256 dell'originale archiviato. |
| `X-Raw-Message-Reconstructed: true` | Nessun originale archiviato; il messaggio è stato ricostruito. Nessun header di checksum. |
| `X-Raw-Message-Storage` | Backend da cui è stato letto l'originale (`postgres` o `filesystem`). |

Anche il nome del file lo indica: `email-{id}-original-...eml` per un originale archiviato, `email-{id}-...eml` per una ricostruzione.

Quando un originale archiviato **fallisce il controllo del checksum**, l'export e il ripristino vengono rifiutati con un messaggio di errore. Restituire la ricostruzione al suo posto nasconderebbe il fatto che la copia archiviata è stata alterata, che è esattamente ciò che un Archivio non deve mai fare.

Un ripristino legge l'originale archiviato e lo aggiunge tramite il client del provider, in modo che la Casella di posta riceva gli header originali e la struttura MIME. Il flusso di byte esatto non è garantito durante la trasmissione perché il client serializza nuovamente il messaggio; la copia archiviata stessa rimane invariata.

## ⚙️ Impostazioni {#settings}

Tutte le impostazioni si trovano nella sezione `RawMessages`, o come variabili d'ambiente con il prefisso `RawMessages__`.

| Impostazione | Predefinito | Descrizione |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Memorizza il messaggio originale di ogni email appena archiviata. Le email archiviate prima dell'attivazione non hanno un originale. |
| `RawMessages:MaxMessageSizeMb` | `50` | I messaggi più grandi di questo valore vengono archiviati senza il loro originale. `0` = nessun limite. |
| `RawMessages:IncludeGraph` | `true` | Memorizza anche l'originale per gli Account Microsoft 365 (Graph). Costa una richiesta Graph aggiuntiva per messaggio, che incide sul budget di throttling. |
| `RawMessages:Storage` | `postgres` | Dove vengono conservati i byte del messaggio: `postgres` (nel database) o `filesystem` (file gzip accanto all'Archivio). Vedere [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Directory in cui vengono scritti i file del messaggio, relativa al content root dell'applicazione. Puntare a un volume montato in Docker. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Numero di livelli di directory a due caratteri su cui è distribuito l'id dell'email. `2` fornisce 10.000 directory, `0` è piatta. Mantiene basso il numero di file per directory negli Archivio di grandi dimensioni. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Ricalcola lo SHA-256 del file ad ogni download e lo confronta con il checksum registrato al momento dell'archiviazione. Un mismatch interrompe il download con HTTP 500 invece di servire byte alterati. |

### Dimensionamento {#sizing}

Lo storage cresce approssimativamente della dimensione di ogni email, inclusi i suoi Allegati, compressi in gzip. Gli Allegati **non** sono sottoposti a Deduplicazione all'interno dell'originale memorizzato, quindi un'email con un Allegato di grandi dimensioni costa quell'Allegato due volte: una volta nello store degli Allegati con Deduplicazione e una volta all'interno dell'originale. Pianificare per circa il **doppio** della dimensione del database di un Archivio senza originali.

L'attivazione dell'impostazione non esegue il backfill della posta esistente. Solo le email appena archiviate ricevono un originale.

## 💾 Backend di archiviazione {#storage-backend}

L'impostazione `Storage` decide dove risiedono i byte. La **riga di metadati rimane sempre nel
database**, qualunque backend sia configurato, e contiene lo SHA-256, la dimensione, la sorgente di
cattura e (per i file) il percorso relativo. Quella riga è ciò che permette all'Archivio di provare cosa
debba contenere un messaggio archiviato.

### postgres (default) {#postgres-default}

Il messaggio compresso risiede nella colonna `Content` di `mail_archiver."RawMessages"`.
Questo è il comportamento che l'Archivio ha sempre avuto e non richiede volumi extra.

Scegli questa opzione quando il database è già la tua unità di Backup e desideri avere un'unica cosa da
effettuare il Backup e il Ripristino.

### filesystem {#filesystem}

Il messaggio viene scritto in `{BasePath}/{xx}/{yy}/{id}.eml.gz`, dove `xx`/`yy` sono derivati
dall'ID dell'email. La riga del database conserva solo i metadati.

Scegli questa opzione quando l'Archivio è abbastanza grande che gli originali dominerebbero la dimensione del database,
o quando desideri posizionare i file dei messaggi su un'archiviazione più economica o separata.

!!! danger "Montare un volume in Docker"
    Il `BasePath` predefinito (`RawMessages`) si risolve in `/app/RawMessages` **all'interno del
    container**. Senza un bind mount, quella directory appartiene allo strato scrivibile del container,
    quindi ogni container ricreato - inclusi `docker compose down && docker compose
    up -d`, un aggiornamento dell'immagine o un riavvio dell'host con uno strato effimero - porta via con sé ogni originale
    archiviato, mentre le righe del database sopravvivono.

    Monta sempre la directory sull'host quando utilizzi il backend filesystem:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Il percorso dell'host (`./raw-messages` qui) è ciò che effettui il Backup. Il percorso del container deve
    corrispondere a `RawMessages:FileSystem:BasePath`, o imposta `RawMessages:FileSystem:BasePath` al
    punto di montaggio scelto. Se sposti il montaggio, mantieni `BasePath` che punta ad esso: i
    valori `StoragePath` archiviati sono relativi, quindi continuano a essere risolti.

### Passaggio tra backend {#switching-backends}

Le letture seguono l'archiviazione registrata su ogni riga, non l'impostazione configurata. Dopo che
hai cambiato `Storage`, i messaggi archiviati in precedenza rimangono scaricabili da qualunque luogo si trovino.
L'impostazione influisce solo sulla posta **nuovamente** archiviata.

Per spostare ciò che è già archiviato, esegui la migrazione dello storage. Consulta i
[CLI commands](CLI.md#moving-stored-original-messages) per il riferimento completo:

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Cosa garantisce la migrazione:

- Ogni messaggio viene verificato rispetto al suo checksum registrato prima di essere copiato. Un messaggio
  che fallisce viene lasciato dove si trova e segnalato: un originale corrotto non viene mai propagato e
  la sua unica copia rimanente non viene mai eliminata.
- Un file viene eliminato solo dopo che la transazione del database che lo ha spostato è stata confermata, quindi un
  esecuzione interrotta non può perdere l'unica copia.
- L'esecuzione è riprendibile. Il lavoro viene selezionato dalla colonna `StorageType`, quindi riavviare il
  comando permette di continuare con ciò che è ancora in sospeso.
- Il codice di uscita è `1` se un messaggio fallisce, `0` altrimenti.

Una migrazione non è richiesta per il funzionamento dell'Archivio. È necessaria solo per spostare i byte
esistenti verso l'altro backend.

## 🛟 Backup e ripristino {#backup-and-restore}

=== "postgres backend"
    Niente di extra. Gli originali fanno parte del database e sono coperti dal normale
    dump, vedi [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Esegui il Backup della directory `BasePath` **insieme al** dump del database.

    Un ripristino del database senza i file lascia l'Archivio con righe il cui contenuto
    non può essere servito. Il download risponderà quindi con HTTP 404 invece di restituire qualcosa
    di errato, e le email stesse rimarranno leggibili dai loro campi analizzati.

## 🗑️ Eliminazione di un'e-mail {#deleting-an-email}

L'eliminazione di un'e-mail rimuove anche il suo originale: le regole di Conservazione, l'eliminazione manuale singola o massiva e l'eliminazione di un intero Account di posta rimuovono il file come parte della stessa operazione. Con il backend `postgres` non c'è nulla di extra da fare, perché i byte viaggiano all'interno della riga che la cascata rimuove già.

Il file viene eliminato **dopo** che la riga dell'e-mail è stata confermata, mai prima. I due passaggi non possono essere un'unica transazione, perché un file non fa parte della transazione del database, quindi uno dei due deve venire per secondo. Questo ordine è quello sicuro: un'eliminazione che fallisce o viene annullata, ad esempio su un record bloccato, lascia l'originale intatto, e un crash tra i due passaggi lascia un file senza una riga, che il sweep sottostante recupera. L'altro ordine distruggerebbe l'originale di un'e-mail che esiste ancora.

**I file orfani vengono raccolti.** Tutto ciò che è sfuggito, inclusi i file lasciati da eliminazioni precedenti a questa pulizia, viene rimosso da un periodic sweep di `BasePath` che confronta ogni file con le righe e cancella quelli a cui nessuna e-mail fa riferimento:

| Setting | Default | Meaning |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Attiva o disattiva il sweep |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Ogni quanto viene eseguito |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | I file più recenti di questo valore non vengono mai toccati |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Ritardo prima della prima esecuzione, in modo che una migrazione di storage in sospeso termini prima |

Il periodo di grazia è ciò che rende sicuro il sweep: un import scrive un file prima di confermare la riga che vi punta, e un file scritto pochi istanti prima non deve essere scambiato per un orfano. I file `.tmp` scritti a metà a causa di una scrittura interrotta vengono raccolti con la stessa regola. Con il backend `postgres` il sweep non fa nulla e non scansiona nemmeno la directory.

## 🔍 Verifica di un Archivio {#verifying-an-archive}

Il checksum registrato al momento dell'Archivio è ciò che rende possibile la verifica.

- **Al download** (`VerifyChecksumOnRead=true`, il predefinito) il file viene sottoposto a hashing e
  confrontato. Un'incongruenza rifiuta il download con errore HTTP 500.
- **Nella migrazione dello storage** ogni messaggio viene verificato prima di essere copiato, in entrambe
  le direzioni.

Poiché la riga dei metadati si trova nel database e i byte possono trovarsi su disco, un confronto del checksum
è l'unico modo per accorgersi che un file è stato modificato al di fuori dell'applicazione.

## ⚠️ Cose da sapere {#things-to-know}

- **L'abilitazione non effettua il backfill.** Solo le email archiviate dopo l'attivazione ricevono un originale.
- **La ricostruzione non è il messaggio.** Per le email senza un originale memorizzato, ogni
  percorso di download restituisce un messaggio ricostruito e lo segnala tramite
  `X-Raw-Message-Reconstructed: true`.
- **Filesystem significa due unità di backup.** Database e directory dei messaggi devono essere ripristinati
  insieme, altrimenti le righe sopravvivono senza il loro contenuto.
- **`VerifyChecksumOnRead` consuma CPU.** Ogni download di un messaggio di grandi dimensioni viene decompresso
  e sottoposto a hashing. Lascialo attivo a meno che tu non abbia riscontrato un problema; è l'unica protezione
  contro l'alterazione silenziosa su disco.
- **L'originale memorizzato non è sottoposto a deduplicazione.** Un allegato che appare in molte email
  viene memorizzato una volta nell'archivio degli allegati, ma una volta per ogni email all'interno dell'originale.
- **L'eliminazione rimuove anche l'originale.** Un'email eliminata tramite conservazione o manualmente porta
  con sé il suo originale memorizzato, file e riga. Nulla viene conservato che l'Archivio non elenchi più.

## 🔗 Vedi anche {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - la migrazione dello storage completa
- [Setup and Parameters](Setup.md) - tutti i parametri di configurazione
- [REST API](API.md#download-original-message) - l'endpoint `/raw`
- [MCP Server](MCP.md#get_email_raw) - lo strumento `get_email_raw`
- [Backup and Restore](BackupRestore.md) - cosa eseguire in Backup e Ripristino
- [Attachment Deduplication](AttachmentDeduplication.md) - come vengono archiviati gli Allegati
