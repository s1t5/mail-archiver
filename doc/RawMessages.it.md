# 📧 Original Message Storage {#original-message-storage}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

Per impostazione predefinita, l'Archivio conserva le **parti analizzate** di un'e-mail: intestazioni, corpi di testo e HTML, Allegati. Un `.eml` esportato viene ricostruito da tali campi.

Questo è sufficiente per leggere un'e-mail, ma non è il messaggio **così come ricevuto**. Le codifiche di trasferimento, i confini MIME e il ripiegamento delle intestazioni differiscono dall'originale, quindi un messaggio ricostruito non verifica più la sua firma DKIM e gli scanner antispam lo valutano in modo diverso.

Con l'archiviazione del messaggio originale abilitata, l'Archivio conserva il messaggio completo così come è arrivato, insieme ai campi analizzati. L'originale memorizzato è ciò che l'Archivio può dimostrare sia stato consegnato.

| | Solo campi analizzati | Con originale memorizzato |
| --- | --- | --- |
| Intestazioni, corpo, Allegati | ✅ | ✅ |
| Messaggio byte-esatto | ❌ | ✅ |
| Verifica firma DKIM | ❌ | ✅ |
| Uso forense (tribunale, audit) | limitato | ✅ |

## 🎯 Dove viene servito l'originale {#where-the-original-is-served}

Una volta archiviato, l'originale viene restituito ovunque sia previsto un messaggio completo:

| Luogo | Cosa succede |
| --- | --- |
| Pulsante **Export EML** nella pagina dei dettagli dell'email | Serve l'originale archiviato quando presente, altrimenti ricorre alla ricostruzione per le email archiviate senza di esso. |
| **Ripristino in una Casella di posta** | Aggiunge l'originale archiviato quando presente, altrimenti ricorre alla ricostruzione per le email archiviate senza di esso. |
| `GET /api/v1/emails/{id}/raw` | Download REST API, vedi la [guida REST API](API.md#download-original-message). |
| `get_email_raw` | Strumento MCP, vedi la [guida MCP](MCP.md#get_email_raw). |

La risposta indica quale forma hai ricevuto, in modo che l'utente possa distinguere un originale autentico da una ricostruzione senza ispezionare i byte:

| Segnale | Significato |
| --- | --- |
| `X-Raw-Message-Source: imap` | Copia esatta dal server IMAP (`BODY[]` come consegnato). |
| `X-Raw-Message-Source: graph` | Copia esatta, il contenuto MIME restituito da Exchange. |
| `X-Raw-Message-Source: eml-file` | Copia esatta, il contenuto del file `.eml` importato. |
| `X-Raw-Message-Source: serialized` | Messaggio analizzato e riscritto. Identico tranne per la fine della riga che è CRLF (importazione MBox, recupero IMAP fetch). |
| `X-Raw-Message-Sha256` | SHA-256 dell'originale archiviato. |
| `X-Raw-Message-Reconstructed: true` | Nessun originale archiviato; il messaggio è stato ricostruito. Nessun header di checksum. |
| `X-Raw-Message-Storage` | Backend da cui è stato letto l'originale (`postgres` o `filesystem`). |

Anche il nome del file lo indica: `email-{id}-original-...eml` per un originale archiviato, `email-{id}-...eml` per una ricostruzione.

Quando un originale archiviato **fallisce il controllo del checksum**, l'export e il ripristino vengono rifiutati con un messaggio di errore. Restituire la ricostruzione al posto dell'originale nasconderebbe il fatto che la copia archiviata è stata alterata, il che è esattamente ciò che un Archivio non deve mai fare.

Un ripristino legge l'originale archiviato e lo aggiunge tramite il client del provider, in modo che la Casella di posta riceva gli header originali e la struttura MIME. Su IMAP il client serializza nuovamente il messaggio, quindi il flusso di byte esatto non è garantito sul cavo. Su Microsoft 365 i byte archiviati viaggiano invariati, perché Graph accetta il messaggio in forma MIME. La copia archiviata stessa non viene mai modificata.

## ⚙️ Impostazioni {#settings}

Tutte le impostazioni si trovano nella sezione `RawMessages`, o come variabili d'ambiente con il prefisso `RawMessages__`.

| Impostazione | Predefinito | Descrizione |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Archivia il messaggio originale di ogni email appena archiviata. Le email archiviate prima dell'attivazione non hanno un originale. |
| `RawMessages:MaxMessageSizeMb` | `50` | I messaggi più grandi di questo valore vengono archiviati senza il loro originale. `0` = nessun limite. |
| `RawMessages:IncludeGraph` | `true` | Archivia l'originale anche per gli Account Microsoft 365 (Graph). Costa una richiesta Graph aggiuntiva per messaggio, che influisce sul budget di throttling. |
| `RawMessages:Storage` | `postgres` | Dove vengono conservati i byte del messaggio: `postgres` (nel database) o `filesystem` (file gzip accanto all'Archivio). Vedere [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Directory in cui vengono scritti i file dei messaggi, relativa alla content root dell'applicazione. Puntare questa impostazione a un volume montato in Docker. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Numero di livelli di directory a due caratteri su cui è distribuito l'ID dell'email. `2` genera 10.000 directory, `0` è piatta. Mantiene basso il numero di file per directory negli Archivio di grandi dimensioni. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Ricalcola lo SHA-256 del file ad ogni download e lo confronta con il checksum registrato al momento dell'archiviazione. Un mismatch interrompe il download con HTTP 500 invece di servire byte alterati. |

### Dimensionamento {#sizing}

Lo storage cresce approssimativamente della dimensione di ogni email inclusi i suoi Allegati, compressi in gzip. Gli Allegati **non** sono sottoposti a Deduplicazione all'interno dell'originale memorizzato, quindi un'email con un Allegato grande costa il doppio di quell'Allegato: una volta nel repository degli Allegati con Deduplicazione e una volta all'interno dell'originale. Pianificare per circa il **doppio** della dimensione del database di un Archivio senza originali.

L'attivazione dell'impostazione non esegue il backfill della posta esistente. Solo le nuove email archiviate ricevono un originale.

## 💾 Storage backend {#storage-backend}

L'impostazione `Storage` decide dove risiedono i byte. La **riga di metadati rimane sempre nel database**, qualunque backend sia configurato, e contiene lo SHA-256, la dimensione, la sorgente di acquisizione e (per i file) il percorso relativo. Quella riga è ciò che permette all'Archivio di provare cosa dovrebbe contenere un messaggio archiviato.

### postgres (default) {#postgres-default}

Il messaggio compresso risiede nella colonna `Content` di `mail_archiver."RawMessages"`.
Questo è il comportamento che l'Archivio ha sempre avuto e non richiede volumi extra.

Scegli questo quando il database è già la tua unità di Backup e desideri un unico elemento da eseguire il Backup e il Ripristino.

### filesystem {#filesystem}

Il messaggio viene scritto in `{BasePath}/{xx}/{yy}/{id}.eml.gz`, dove `xx`/`yy` sono derivati dall'ID dell'email. La riga del database conserva solo i metadati.

Scegli questo quando l'Archivio è abbastanza grande che gli originali dominerebbero la dimensione del database, o quando desideri inserire i file dei messaggi su un'archiviazione più economica o separata.

!!! danger "Montare un volume in Docker"
    Il valore predefinito `BasePath` (`RawMessages`) si risolve in `/app/RawMessages` **all'interno del
    container**. Senza un bind mount, quella directory appartiene allo strato scrivibile del container, quindi ogni container ricreato - inclusi `docker compose down && docker compose
    up -d`, un aggiornamento dell'immagine o un riavvio dell'host con uno strato effimero - porta via con sé ogni originale archiviato, mentre le righe del database sopravvivono.

    Monta sempre la directory sull'host quando utilizzi il backend filesystem:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Il percorso dell'host (`./raw-messages` qui) è ciò che metti in Backup. Il percorso del container deve
    corrispondere a `RawMessages:FileSystem:BasePath`, o imposta `RawMessages:FileSystem:BasePath` sul
    punto di montaggio scelto. Se sposti il montaggio, mantieni `BasePath` che punta ad esso: i
    valori `StoragePath` archiviati sono relativi, quindi continuano a essere risolti.

### Switching backends {#switching-backends}

Le letture seguono lo storage registrato su ogni riga, non l'impostazione configurata. Dopo che hai
cambiato `Storage`, i messaggi archiviati in precedenza rimangono scaricabili da qualunque luogo si trovino.
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

- Ogni messaggio viene verificato rispetto al proprio checksum registrato prima di essere copiato. Un messaggio
  che fallisce viene lasciato dove si trova e segnalato: un originale corrotto non viene mai propagato e
  la sua unica copia rimanente non viene mai eliminata.
- Un file viene eliminato solo dopo che la transazione del database che lo ha spostato è stata confermata, quindi un
  esecuzione interrotta non può perdere l'unica copia.
- L'esecuzione è riprendibile. Il lavoro viene selezionato dalla colonna `StorageType`, quindi riavviando il
  comando si continua con ciò che è ancora in sospeso.
- Il codice di uscita è `1` se un messaggio è fallito, `0` altrimenti.

Una migrazione non è richiesta per il funzionamento dell'Archivio. È necessaria solo per spostare i byte esistenti
verso l'altro backend.

## 🛟 Backup e ripristino {#backup-and-restore}

=== "postgres backend"
    Niente di extra. Gli originali fanno parte del database e sono coperti dal normale
    dump, vedi [Backup e ripristino](BackupRestore.md).

=== "filesystem backend"
    Esegui il Backup della directory `BasePath` **insieme al** dump del database.

    Un ripristino del database senza i file lascia l'Archivio con righe il cui contenuto
    non può essere servito. Il download risponderà quindi con HTTP 404 invece di restituire qualcosa di
    errato, e le email stesse rimangono leggibili dai loro campi analizzati.

## 🗑️ Eliminazione di un'e-mail {#deleting-an-email}

L'eliminazione di un'e-mail rimuove anche il suo originale: le regole di Conservazione, l'eliminazione manuale singola e massiva, e l'eliminazione di un intero Account e-mail rimuovono il file come parte della stessa operazione. Con il backend `postgres` non c'è nulla di extra da fare, perché i byte viaggiano all'interno della riga che la cascata rimuove già.

Il file viene eliminato **dopo** che la riga dell'e-mail è stata confermata, mai prima. I due passaggi non possono essere un'unica transazione, perché un file non fa parte della transazione del database, quindi uno dei due deve venire dopo. Questo ordine è quello sicuro: un'eliminazione che fallisce o viene annullata, ad esempio su un record bloccato, lascia l'originale intatto, e un crash tra i due passaggi lascia un file senza una riga, che il sweep sottostante recupera. L'altro ordine distruggerebbe l'originale di un'e-mail che esiste ancora.

**I file orfani vengono raccolti.** Qualsiasi cosa sia sfuggita, inclusi i file lasciati da eliminazioni precedenti a questa pulizia, viene rimosso da un periodic sweep di `BasePath` che confronta ogni file con le righe e cancella quelli a cui nessuna e-mail fa riferimento:

| Impostazione | Predefinito | Significato |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Attiva o disattiva lo sweep |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Frequenza con cui viene eseguito |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | I file più recenti di questo valore non vengono mai toccati |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Ritardo prima della prima esecuzione, in modo che una migrazione di archiviazione in sospeso termini prima |

Il periodo di grazia è ciò che rende lo sweep sicuro: un import scrive un file prima di confermare la riga che punta ad esso, e un file scritto pochi istanti fa non deve essere scambiato per un orfano. I file `.tmp` scritti a metà a causa di una scrittura interrotta vengono raccolti sotto la stessa regola. Con il backend `postgres` lo sweep non fa nulla e non percorre nemmeno la directory.

## 🔍 Verifica di un Archivio {#verifying-an-archive}

Il checksum registrato al momento dell'Archivio è ciò che rende possibile la verifica.

- **Al download** (`VerifyChecksumOnRead=true`, il valore predefinito) il file viene sottoposto a hashing e
  confrontato. Un'incongruenza rifiuta il download con errore HTTP 500.
- **Nella migrazione dello storage** ogni messaggio viene verificato prima di essere copiato, in entrambe
  le direzioni.

Poiché la riga di metadati si trova nel database e i byte possono trovarsi su disco, un confronto del checksum
è l'unico modo per accorgersi che un file è stato modificato al di fuori dell'applicazione.

## ⚠️ Cose da sapere {#things-to-know}

- **L'abilitazione non effettua il backfill.** Solo le email archiviate dopo l'attivazione ricevono un originale.
- **Un originale mancante viene registrato come errore.** Quando la cattura viene rifiutata (messaggio sopra `MaxMessageSizeMb`, nessuna cattura utilizzabile) o la scrittura nel backend configurato fallisce, il registro riporta un errore con l'id dell'email e la ragione. L'email rimane nell'Archivio senza il suo originale; non c'è backfill automatico né tentativi di rielaborazione.
- **La pagina dei dettagli segnala un originale mancante.** Con `RawMessages:Enabled`, la pagina dei dettagli mostra un avviso sopra gli header. Interroga il backend di archiviazione, quindi viene segnalato anche un file del filesystem che è scomparso. L'elenco delle email non ha un badge per riga; occuperebbe troppo spazio nella colonna dello stato.
- **La ricostruzione non è il messaggio.** Per le email senza un originale memorizzato, ogni percorso di download restituisce un messaggio ricostruito e lo dichiara tramite `X-Raw-Message-Reconstructed: true`.
- **Filesystem significa due unità di Backup.** Database e directory dei messaggi devono essere sottoposti a Ripristino insieme, altrimenti le righe sopravvivono senza il loro contenuto.
- **`VerifyChecksumOnRead` consuma CPU.** Ogni download di un messaggio di grandi dimensioni viene decompresso e sottoposto a hash. Lascialo attivo a meno che tu non abbia misurato un problema; è l'unica protezione contro l'alterazione silenziosa su disco.
- **L'originale memorizzato non è sottoposto a Deduplicazione.** Un Allegato che appare in molte email viene memorizzato una sola volta nel repository degli allegati, ma una volta per ogni email all'interno dell'originale.
- **L'eliminazione rimuove anche l'originale.** Un'email eliminata tramite Conservazione o manualmente porta con sé il suo originale memorizzato, sia come file che come riga. Nulla viene conservato se l'Archivio non lo elenca più.

## 🔗 Vedi anche {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - la migrazione dello storage completa
- [Setup and Parameters](Setup.md) - tutti i parametri di configurazione
- [REST API](API.md#download-original-message) - l'endpoint `/raw`
- [MCP Server](MCP.md#get_email_raw) - lo strumento `get_email_raw`
- [Backup and Restore](BackupRestore.md) - cosa eseguire in Backup e Ripristino
- [Attachment Deduplication](AttachmentDeduplication.md) - come vengono conservati gli Allegati
