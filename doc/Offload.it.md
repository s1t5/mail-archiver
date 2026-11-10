Pagina: 📤 Date-Windowed Offload

# 📤 Date-Windowed Offload {#date-windowed-offload}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

Un offload aggiunge mail archiviate più recenti di una data di cutoff in un'altra Casella di posta, e lascia tutto ciò che è più vecchio nell'Archivio. Esiste per le migrazioni in cui la nuova Casella di posta deve contenere solo la mail recente, mentre la cronologia completa rimane accessibile tramite Mail Archiver.

Differisce da [Copy All Emails to Another Mailbox](MailboxMigration.md) in cinque modi:

| | Copy All Emails | Offload |
|---|---|---|
| Filtro data | nessuno, tutto o niente | cutoff, con un limite superiore opzionale |
| Ripetibile | no, aggiunge tutto di nuovo | sì, salta ciò che la destinazione contiene già |
| Esclusioni Cartella | no | sì, configurabili |
| Rinomina Cartella | no | sì, configurabile |
| Anteprima | no | dry run con report per Cartella senza scrivere |

L'offload aggiunge sempre e solo alla destinazione. Non elimina mai dall'Archivio e non tocca mai la Casella di posta di origine.

## 🔁 Ripetere un'esecuzione è sicuro {#repeating-a-run-is-safe}

Prima del primo append, l'intera Casella di posta di destinazione viene indicizzata e ogni messaggio viene controllato rispetto ad essa. Un'esecuzione ripetuta, quindi, non aggiunge nulla che è già arrivato, ma lo segnala come già presente. Ciò ha tre conseguenze pratiche:

- Un'esecuzione interrotta viene recuperata semplicemente eseguendola di nuovo.
- Un'esecuzione completata può essere ripetuta come passaggio di verifica. Un risultato corretto è: nulla aggiunto e tutto già presente.
- I messaggi eliminati sulla destinazione tornano alla successiva esecuzione, perché non sono più nell'indice.

Due criteri decidono se un messaggio è già presente, in questo ordine:

1. **Message-ID**, normalizzato. Questo svolge quasi tutto il lavoro.
2. **Un fingerprint** su mittente, destinatari, oggetto e ora di invio entro due secondi. Questo copre i messaggi il cui Message-ID memorizzato è mancante o malformato. Tali messaggi non possono essere ripristinati affatto con il loro Message-ID originale, quindi senza questo secondo criterio verrebbero duplicati ad ogni singola esecuzione.

Il job riporta quanti match sono derivati dal fingerprint separatamente, perché è il criterio che potrebbe fallire silenziosamente.

## ⚙️ Configurazione {#configuration}

Una nuova sezione `Offload`, con valori predefiniti che riproducono il comportamento precedente:

```json
"Offload": {
  "MaxConcurrentJobs": 1,
  "PrefetchMaxMessages": 500000,
  "ExcludedSourceFolders": [],
  "FolderRenameMap": {},
  "MarkAsSeen": true
}
```

| Key | Default | Purpose |
|---|---|---|
| `MaxConcurrentJobs` | `1` | Quanti job di ripristino o offload possono essere eseguiti contemporaneamente. Uno mantiene il comportamento strettamente seriale che la coda dei job ha sempre avuto. Al massimo un job per Casella di posta di destinazione viene eseguito indipendentemente da questo valore. |
| `PrefetchMaxMessages` | `500000` | Limite superiore su quanti messaggi vengono indicizzati da una Casella di posta di destinazione. Sopra tale limite, il controllo dei duplicati si restringe a una singola Cartella e registra di averlo fatto. |
| `ExcludedSourceFolders` | empty | Cartelle di origine mai sottoposte a offload. Verificate prima della rinomina, e includono le sottocartelle. |
| `FolderRenameMap` | empty | Riscrive i segmenti iniziali di un percorso di una Cartella di origine. |
| `MarkAsSeen` | `true` | Se la mail aggiunta viene contrassegnata come letta. |

Entrambe le impostazioni delle Cartelle vengono fornite vuote appositamente. Riscrivere o scartare Cartelle senza che venga richiesto sorprenderebbe chiunque utilizzi già il percorso di ripristino, quindi un'esecuzione senza configurazione migra tutto, incluse le cartelle spam, e crea un secondo set di cartelle speciali accanto a quelle della destinazione.

### Esclusioni e rinomina delle Cartelle {#folder-exclusions-and-renaming}

Per una migrazione da Exchange a Dovecot, le due impostazioni solitamente appaiono così:

```json
"ExcludedSourceFolders": ["Junk E-Mail", "Deleted Items"],
"FolderRenameMap": {
  "Sent Items": "Sent",
  "Deleted Items": "Trash",
  "Junk E-Mail": "Junk"
}
```

Senza la mappa di rinomina, ogni Casella di posta migrata finirà con sia `Sent` che `Sent Items`, perché Dovecot pre-crea le proprie cartelle special-use.

Ci sono quattro cose riguardo al modo in cui queste vengono applicate che vale la pena conoscere, perché ognuna è un modo per essere sorpresi:

1. **Le esclusioni vengono verificate per prime**, rispetto al nome di origine. Se la rinomina avvenisse per prima, un'esclusione su `Deleted Items` non verrebbe mai attivata una volta che è diventata `Trash`.
2. **La rinomina riscrive il prefisso del percorso corrispondente più lungo**, sui confini dei segmenti. `Sent Items` che diventa `Sent` trasforma anche `Sent Items/2019` in `Sent/2019`, e lascia `Sent Items Archive` invariata.
3. **Il matching è case insensitive**, e sia `/` che `\` contano come separatori.
4. **Due Cartelle di origine possono confluire in una sola di destinazione.** `Sent Items` e `Sent` che diventano entrambe `Sent` va bene e non apre la Cartella di destinazione due volte.

Cambiare la mappa di rinomina tra due esecuzioni non duplica nulla, perché il controllo dei duplicati copre l'intera Casella di posta di destinazione piuttosto che una singola Cartella risolta.

## 🖥️ Esecuzione dall'interfaccia {#running-it-from-the-interface}

Nella pagina dei dettagli di un Account, **Offload to Another Mailbox** apre un modulo con la Casella di posta di destinazione, la Cartella radice di destinazione, l'opzione per preservare la struttura delle Cartelle, la finestra temporale, un interruttore per la dry run e l'opzione per contrassegnare la mail aggiunta come letta. Le esclusioni e la mappa di rinomina configurate sono mostrate in sola lettura, quindi un'esecuzione non è mai una sorpresa.

Il job viene messo in coda e il suo progresso e il report per Cartella appaiono nella pagina dello stato dei job e sotto **Jobs**. Il report per Cartella riporta i nomi delle Cartelle di origine, quindi è mostrato solo all'utente che ha avviato il job e agli amministratori.

La Casella di posta di destinazione può essere un Account IMAP disabilitato, ad esempio per predisporre un target di migrazione prima che vada online; l'esecuzione fallisce con un errore di connessione se la destinazione non è raggiungibile. Gli Account Microsoft 365 non possono essere target di offload; il percorso di ripristino Graph rimane invariato.

### Chi può eseguirlo {#who-may-run-it}

Gli amministratori possono eseguire l'offload tra due Account qualsiasi. Un self-manager può eseguire l'offload **solo tra gli Account a lui assegnati**, sia all'origine che alla destinazione: l'origine, perché la pagina dell'Account è già limitata in questo modo, e la destinazione, perché l'elenco dei target è ristretto allo stesso set e la richiesta viene controllata nuovamente rispetto ad esso quando il job viene avviato. Un self-manager con un singolo Account assegnato non ha quindi alcun target idoneo e ne viene informato.

La delimitazione deriva da `IAccountAccessResolver`, lo stesso resolver utilizzato dalla REST API e dal server MCP, quindi esiste un'unica definizione di chi può usare quale Casella di posta. La decisione stessa risiede in `OffloadTargetEligibility`, che sia l'elenco dei target che il controllo della richiesta chiamano, quindi ciò che il modulo offre e ciò che il server accetta non possono divergere.

## ⌨️ Esecuzione da riga di comando {#running-it-from-the-command-line}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll \
  --offload --source-account-id 3 --target-account-id 7 \
  --since 2025-08-01 \
  [--until 2026-08-01] [--target-folder INBOX] \
  [--preserve-folders] [--dry-run] [--no-mark-seen]
```

| Argument | Meaning |
|---|---|
| `--source-account-id` | Account da cui leggere la mail archiviata. Richiesto. |
| `--target-account-id` | Account IMAP in cui aggiungere. Richiesto, e deve differire dall'origine. |
| `--since` | Limite inferiore inclusivo sulla data di invio, `YYYY-MM-DD`. Richiesto. |
| `--until` | Limite superiore inclusivo opzionale. |
| `--target-folder` | Cartella radice nella Casella di posta di destinazione. Predefinito a `INBOX`. |
| `--preserve-folders` | Ricrea la struttura delle Cartelle di origine sotto la radice. |
| `--dry-run` | Riporta cosa accadrebbe, senza aggiungere nulla. |
| `--no-mark-seen` | Aggiunge senza il flag Seen. |

Codici di uscita:

| Code | Meaning |
|---|---|
| `0` | Tutto è stato aggiunto o è già presente. |
| `1` | Almeno un messaggio è fallito, o l'esecuzione stessa è fallita. |
| `2` | L'invocazione era errata: argomenti errati o mancanti, account sconosciuto, destinazione non IMAP, origine uguale alla destinazione. |

Un'invocazione gestisce una Casella di posta, il che rende gestibile tramite script una flotta di Caselle di posta partendo da un CSV di origine, destinazione e cutoff.

> **Eseguire diverse esecuzioni contemporaneamente.** La regola di un solo job per target esiste solo all'interno di un singolo processo, quindi due invocazioni contemporanee da riga di comando contro la *stessa* Casella di posta di destinazione potrebbero ciascuna acquisire il proprio indice dei duplicati prima che l'altra inizi a scrivere, e potrebbero quindi entrambe aggiungere la stessa mail. Suddividi il lavoro in modo che ogni invocazione concorrente abbia un target diverso, che è il caso normale quando ogni riga di un CSV è una Casella di posta diversa.

## 🗓️ Come viene interpretato il cutoff {#how-the-cutoff-is-interpreted}

- Il filtro è sulla **data di invio**, non su quando un messaggio è stato archiviato. Il timestamp di archiviazione è l'ora dell'esecuzione di archiviazione e non ha significato per un cutoff.
- Una finestra relativa come "gli ultimi dodici mesi" viene **risolta in una data fissa quando il job viene creato**. Un job può girare per molto tempo e essere ripetuto in seguito, e deve selezionare la stessa mail ogni volta; una finestra relativa memorizzata divergerebbe.
- I cutoff sono interpretati nel **fuso orario di visualizzazione configurato**, lo stesso che mostra la schermata di ricerca, e il limite inferiore è inclusivo a partire dalle 00:00 di quel giorno. Un limite superiore include l'intero giorno. Anche le finestre relative ("gli ultimi N mesi") sono risolte in quel fuso orario di visualizzazione, corrispondendo a come viene memorizzato `SentDate`.

### Limitazione nota {#known-limitation}

- Quando l'indice dei duplicati raggiunge `Offload:PrefetchMaxMessages`, l'ambito viene ridotto e registrato invece di essere riprovato su una singola Cartella. Il fallback `restrictToFolder` offerto dal costruttore di indici non è attualmente utilizzato dal percorso di offload; aumentare il limite è il rimedio supportato.

## 📋 Una migrazione, end to end {#a-migration-end-to-end}

1. Crea le Caselle di posta di destinazione. Per molte contemporaneamente, vedi l'importazione bulk CSV in [Account Import](Account-Import.md).
2. Configura `Offload:ExcludedSourceFolders` e `Offload:FolderRenameMap`. Entrambi sono vuoti per impostazione predefinita, quindi saltare questo passaggio migra le cartelle spam e crea cartelle speciali duplicate.
3. Conferma che ogni Casella di posta di origine sia completamente archiviata prima di eseguire l'offload.
4. **Esegui una dry run di tutto.** Controlla i conteggi e i nomi delle Cartelle di destinazione risolti prima che venga scritto qualsiasi cosa.
5. Esecuzione reale. Inizia con `MaxConcurrentJobs` a 2 e aumentalo solo dopo aver osservato come il server di destinazione gestisce il carico; Dovecot limita le connessioni concorrenti per utente e per IP.
6. Ripeti l'esecuzione per verificare. Non aspettarti nulla aggiunto e tutto già presente.
7. Disabilita gli Account di origine.

Il throughput è limitato da `BatchOperation:PauseBetweenEmailsMs`, che è di 50 ms per impostazione predefinita e quindi consente al massimo venti append al secondo prima che la latenza del server venga conteggiata.

## 🔍 Cosa significano i contatori {#what-the-counters-mean}

| Counter | Meaning |
|---|---|
| Appended | Scritto nella destinazione. In una dry run, ciò che sarebbe stato scritto. |
| Already present | La destinazione lo conteneva già, tramite uno dei due criteri. |
| of those by fingerprint | Quanti sono stati riconosciuti dal secondo criterio invece che dal Message-ID. |
| Excluded folder | Saltata perché la Cartella di origine è esclusa. |
| Failed | Impossibile aggiungere. |

Viene riportato un avviso se l'indice dei duplicati non ha coperto l'intera Casella di posta di destinazione, il che accade sopra `PrefetchMaxMessages` o quando una Cartella non può essere letta. In quello stato, un'esecuzione ripetuta potrebbe aggiungere mail che sono già presenti nelle parti che non sono state indicizzate.

## ⚠️ Note e limiti {#notes-and-limits}

- La mail ripristinata porta ora il suo **tempo di consegna originale** come data interna IMAP, preso dalla catena `Received` del messaggio e con fallback alla data di invio. In precedenza portava l'ora dell'esecuzione di archiviazione, il che interrompeva l'ordinamento lato server per arrivo e qualsiasi regola basata sull'età sulla destinazione.
- Poiché un'esecuzione ripetuta salta ciò che è già presente, una data interna errata non può essere corretta rieseguendo: i messaggi vengono riconosciuti e saltati. Assicurati che sia corretto prima di un'esecuzione in produzione, o elimina i messaggi interessati sulla destinazione ed esegui di nuovo.
- Microsoft 365 non è supportato come target di offload.
- Il rilevamento dei duplicati può riconoscere solo ciò che può vedere nella Casella di posta di destinazione. La mail che ha raggiunto la destinazione tramite un'altra via viene catturata solo nella misura in cui i due criteri coincidono.
