# 📥 Importazione della posta nella Web UI (EML / MBox) {#importing-mail-in-the-web-ui-eml-mbox}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

Oltre alla [command line interface](CLI.md) e all'
[importazione Account tramite CSV](Account-Import.md), Mail Archiver può importare un
esistente Archivio di posta **tramite l'interfaccia web**. Sono presenti due moduli di caricamento:

| Modulo | File accettati | Risultato |
|------|---------------|--------|
| **Import MBox** | `.mbox`, `.mbx` | Ogni messaggio nel file viene importato in **una** Cartella a tua scelta |
| **Import EML** | `.zip` contenente file `.eml` | La **struttura delle Cartelle all'interno dello ZIP** decide dove finisce ogni messaggio |

Entrambi i moduli archiviano i messaggi in un Account Mail Archiver esistente, entrambi vengono eseguiti come
**background jobs** e entrambi **saltano automaticamente i duplicati** — importare
lo stesso file due volte non crea una seconda copia.

> 💡 **Suggerimento**: Se il tuo Archivio si trova già sul server come file, preferisci la
> [CLI](CLI.md). Evita il caricamento tramite browser ed è la
> scelta migliore per Archivio di molti gigabyte.

## 🧭 Accesso {#access}

1. Apri **Mail Accounts** nel menu principale.
2. Usa il menu a discesa **Import** sopra l'elenco degli Account e scegli
   **Import MBox** o **Import EML**.

I moduli sono disponibili per gli **admin** (tutti gli Account) e i **self manager**
(gli Account assegnati a loro); vedi
[User Management](UserManagement.md#-user-account-permissions). Solo gli Account **abilitati**
vengono offerti come destinazioni di importazione — un Account disabilitato non può essere selezionato.

## 📨 Import MBox {#import-mbox}

MBox è il formato utilizzato dalla maggior parte dei client di posta per le Cartelle esportate e dai
Backup in stile `mbox`.

| Campo | Significato |
|-------|---------|
| **MBox File** | Il file `.mbox` / `.mbx` da caricare |
| **Target Account** | L'Account Mail Archiver che riceve i messaggi |
| **Target Folder** | La Cartella in cui vengono importati i messaggi. Scegline una dall'elenco delle Cartelle esistenti dell'Account, oppure digita un nuovo nome — una Cartella che non esiste ancora viene **creata automaticamente** durante l'importazione. Di default è `INBOX`. |

Tutti i messaggi nel file vengono importati in quella singola Cartella di destinazione. Il file MBox
in sé non contiene informazioni sulle Cartelle, quindi la struttura della Casella di posta
originale non viene ricostruita — se ne hai bisogno, usa la procedura EML/ZIP.

## 🗂️ Import EML {#import-eml}

L'importazione EML richiede un **Archivio ZIP** i cui elementi sono file `.eml`.

- I file `.eml` nella **root** dello ZIP vengono importati in `INBOX`.
- I file `.eml` in una **sottocartella** vengono importati in una Cartella con lo
  **stesso nome** di quella sottocartella.

Quindi uno ZIP strutturato come segue:

```
mailbox.zip
├── 2024-invoice.eml            → INBOX
├── Archive/
│   └── old-message.eml         → Archive
└── Projects/
    └── 2026/
        └── kickoff.eml         → Projects/2026
```

ripristina l'albero delle Cartelle insieme ai messaggi. I singoli file `.eml` possono essere
caricati racchiudendoli prima in uno ZIP (`zip emails.zip *.eml`).

## ⚙️ Comportamento condiviso {#shared-behaviour}

Entrambi i moduli si comportano allo stesso modo una volta premuto **Start Import**:

- **Limite dimensione file** — configurato con `Upload__MaxFileSizeGB` (default **10 GB**).
  Il modulo mostra l'attuale limite accanto al selettore dei file.
- **Elaborazione in background** — il caricamento viene messo in coda come un job; non è necessario
  tenere aperto il browser.
- **Gestione duplicati** — i messaggi che sono già nell'Archivio vengono saltati
  e contati separatamente. I duplicati vengono identificati tramite l'header `Message-ID`.
- **Input malformati** — i file illeggibili vengono saltati singolarmente e segnalati nella
  pagina di stato invece di interrompere l'intera importazione.
- **Progresso** — verrai indirizzato direttamente alla pagina di stato del job, che mostra l'ID
  del job, il nome del file, la dimensione del file, lo stato, i messaggi processati/totali e i
  conteggi in esecuzione. Lo stesso job appare anche nella pagina [Jobs](BackgroundJobs.md).
- **Cancellazione** — un'importazione in corso può essere cancellata dalla sua pagina di stato. I
  messaggi importati fino a quel punto rimangono nell'Archivio.
- **Tracciamento** — l'importazione viene scritta nel [access log](Logs.md) e il
  valore di archiviazione per Account viene aggiornato successivamente (vedi
  [Per-Account Storage](AccountStorage.md)).

### Stati del job {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled`

Un job terminato riporta quanti messaggi sono stati importati, quanti sono falliti e quanti
sono stati saltati come duplicati — ad esempio
*"1420 imported, 3 failed, 87 duplicates"*. In caso di importazione riuscita, il file caricato
viene eliminato dal server; le voci fallite vengono mantenute per la durata del
job, così puoi vedere cosa è stato rifiutato.

## ⚠️ Cose a cui prestare attenzione {#things-to-watch-out-for}

- **Tipo di file errato** — il caricamento viene rifiutato a meno che l'estensione non sia una tra
  `.mbox`, `.eml` o `.zip`. Il selettore del modulo EML accetta solo `.zip`, quello del modulo MBox
  solo `.mbox` e `.mbx`.
- **"0 imported, N duplicates"** — questo non è un errore. I messaggi sono già
  nell'Archivio, molto probabilmente a causa di un'importazione o Sincronizzazione precedente della stessa sorgente.
- **Archivio molto grandi** — un file MBox viene letto messaggio per messaggio, quindi un
  file di molti gigabyte richiede del tempo anche in background. Controlla il progresso sulla
  pagina di stato invece di ricaricare il file.
- **Spazio libero su disco** — il file caricato viene memorizzato temporaneamente sul server e
  necessita di spazio accanto al database dell'Archivio.
- **Nomi di Cartelle con caratteri speciali** — i nomi delle Cartelle vengono presi dai percorsi ZIP
  o dal tuo input; controlla l'elenco delle Cartelle dell'Account dopo l'importazione se hai
  utilizzato caratteri insoliti.

## 🔗 Documentazione correlata {#related-documentation}

- [CLI commands](CLI.md) — importazione da una directory montata
- [Account Import (CSV)](Account-Import.md) — aggiungere molti Account in una volta sola
- [Background Jobs](BackgroundJobs.md) — monitorare e cancellare i job di importazione
- [Exporting a Mail Account](Export-Account.md) — la direzione opposta
- [Setup and Parameters](Setup.md) — impostazioni `Upload__*`
