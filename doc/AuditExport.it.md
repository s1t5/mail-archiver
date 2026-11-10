# 📤 Esportazione Dati di Audit {#audit-data-export}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

L'esportazione dei dati di audit crea pacchetti massivi di dati tabulari (ZIP con `INDEX.XML` + tabelle CSV + indice DTD) dall'Archivio esistente. Questi pacchetti seguono la struttura di indice stabilita per la consegna di supporti dati e possono essere importati direttamente da comuni strumenti esterni di audit e analisi che supportano questo formato di indice.

La funzione è disponibile come **pagina dedicata raggiungibile dalla pagina Logs** ed è limitata agli utenti admin. Ogni esecuzione di esportazione viene registrata nel registro di controllo, pertanto la cronologia delle esportazioni è sicura rispetto alle revisioni.

## 🧭 Accesso {#access}

1. Accedi come utente admin
2. Apri la pagina **Logs**
3. Clicca sul pulsante **Audit Data Export** nell'angolo in alto a destra

Gli utenti non admin non vedono il pulsante e non possono chiamare nessuno degli endpoint di esportazione (il server li rifiuta).

## 📦 Struttura del Pacchetto di Esportazione {#export-package-structure}

Il file ZIP generato si chiama `audit-export-<timestamp>.zip` e contiene:

| File | Contenuto |
|---|---|
| `INDEX.XML` | File di indice che descrive il fornitore di dati, il supporto e tutte le definizioni di tabella/colonna |
| `index.dtd` | Il DTD a cui fa riferimento il file di indice; richiesto dagli strumenti di importazione per la validazione |
| `emails.csv` | Una riga per ogni email archiviata nel periodo/Casella di posta selezionato |
| `attachments.csv` | Opzionale; una riga per ogni Allegato delle email esportate |

### Tabella `emails.csv` {#table-emailscsv}

Colonne (nessuna riga di intestazione; i nomi delle colonne provengono da `INDEX.XML`, come di consueto per questo formato):

| Colonna | Descrizione |
|---|---|
| Id | ID email interno (numerico) |
| MessageId | Intestazione Email Message-ID |
| SentDate | Data di invio, ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`) |
| ReceivedDate | Data di ricezione, ISO 8601 UTC |
| IsOutgoing | `1` = in uscita, `0` = in entrata |
| From | Mittente |
| To | Destinatari |
| Cc | Destinatari CC |
| Bcc | Destinatari BCC |
| Subject | Oggetto |
| FolderName | Cartella in cui è archiviata l'email |
| HasAttachments | `1` = con Allegati, `0` = senza |
| AttachmentCount | Numero di Allegati (numerico) |
| AccountEmail | Indirizzo email della Casella di posta di archiviazione |

### Tabella `attachments.csv` {#table-attachmentscsv}

| Colonna | Descrizione |
|---|---|
| EmailId | ID interno dell'email a cui appartiene l'Allegato (chiave di join per `emails.csv`) |
| FileName | Nome file dell'Allegato |
| ContentType | Tipo MIME |
| Size | Dimensione in byte (numerico) |
| Sha256 | Hash SHA-256 del contenuto (dallo storage degli Allegati basato sul contenuto) |

### Convenzioni CSV {#csv-conventions}

- Codifica: UTF-8 **senza** BOM (dichiarata come `UTF8` in `INDEX.XML`)
- Delimitatore di colonna: `;`
- Incapsulatore di testo: `"` (raddoppiato per l'escaping)
- Delimitatore di record: `\n` (dichiarato esplicitamente in `INDEX.XML`)
- Date: ISO 8601, orari convertiti dal fuso orario di visualizzazione configurato a UTC
- Nessuna riga di intestazione; nessun numero localizzato (le colonne numeriche contengono semplici interi)

## 🖥️ Utilizzo della Pagina di Esportazione {#using-the-export-page}

### Campi del Modulo {#form-fields}

1. **From / To date** - Periodo da esportare (obbligatorio, intervallo massimo configurabile, predefinito 10 anni)
2. **Mailbox** - Limita l'esportazione a una singola Casella di posta o esporta tutte le Caselle di posta
3. **Attachment metadata** - Se selezionato, `attachments.csv` viene aggiunto al pacchetto
4. **Data supplier name / location / comment** - Scritto nel blocco `DataSupplier` di `INDEX.XML`; precompilato dalla sezione di configurazione `AuditExport` ed editabile per ogni esportazione

### Ciclo di Vita del Job {#job-lifecycle}

1. Cliccando su **Start Export** si valida il periodo e la Casella di posta, si crea un job e si torna alla pagina
2. Il job viene eseguito in background (le esportazioni in coda vengono elaborate una dopo l'altra); la tabella dei job mostra il progresso in tempo reale (email elaborate/totali con una barra di avanzamento) tramite polling automatico
3. Quando l'esportazione termina, il pulsante **Download** appare nella riga del job
4. I job in esecuzione o in coda possono essere **annullati**

### Cronologia delle Esportazioni {#export-history}

La tabella della cronologia sotto il modulo elenca le esportazioni più recenti con timestamp, utente, periodo, Casella di posta, conteggio email, stato e dimensione del file. La cronologia è **conservata nel database** (tabella `AuditExportJobs`), quindi sopravvive ai riavvii dell'applicazione ed è sicura rispetto alle revisioni.

I file scaricabili sono conservati per una finestra di Conservazione configurabile (predefinito 30 giorni, vedi `AuditExport__RetentionDays`); dopo tale periodo i file ZIP vengono eliminati dalla pulizia giornaliera, mentre le righe della cronologia rimangono.

> 📝 **Nota**: I file di esportazione sono memorizzati all'interno del container dell'applicazione. Se hai bisogno di conservarli a lungo termine, scaricali e conservali nel tuo repository di prove di audit.

## 🔐 Audit {#auditing}

Ogni esportazione produce due voci nel registro di controllo del nuovo tipo **Audit Data Export**:

1. **Voce di inizio** - quando l'esportazione viene messa in coda (con ID job, periodo, Casella di posta, opzioni in formato JSON)
2. **Voce di risultato** - quando l'esportazione termina, fallisce o viene annullata (con ID job, periodo, Casella di posta, conteggio email, risultato e dimensione del pacchetto in formato JSON)

Le voci appaiono nella pagina Logs come tutte le altre voci del registro di controllo e possono essere filtrate per il nuovo tipo. Il menu a discesa del filtro per tipo le rileva automaticamente.

## ⚙️ Configurazione {#configuration}

La sezione `AuditExport` in `appsettings.json` (le variabili d'ambiente utilizzano la sintassi con doppio underscore, ad esempio `AuditExport__RetentionDays`):

| Impostazione | Descrizione | Predefinito |
|---|---|---|
| `DataSupplierName` | Valore predefinito per il campo del modulo del nome del fornitore di dati | vuoto |
| `DataSupplierLocation` | Valore predefinito per il campo del modulo della posizione del fornitore di dati | vuoto |
| `Comment` | Valore predefinito per il campo del modulo del commento | vuoto |
| `OutputDirectory` | Directory per i file ZIP generati (i percorsi relativi si risolvono rispetto alla root del contenuto dell'app) | `exports/audit` |
| `RetentionDays` | Giorni fino all'eliminazione dei file di esportazione completati | `30` |
| `MaxRangeYears` | Intervallo massimo consentito tra la data di inizio e fine del periodo | `10` |

## 🗄️ Database {#database}

La cronologia delle esportazioni è memorizzata nella tabella `mail_archiver."AuditExportJobs"` (creata dalla migrazione `MigrateV2609_1`, applicata automaticamente all'avvio). Nessuna tabella esistente viene modificata da questa funzione.

## 🔍 Risoluzione dei Problemi {#troubleshooting}

| Problema | Causa / Soluzione |
|---|---|
| Pulsante Download mancante dopo il riavvio | I file di esportazione risiedono nel container; se il container è stato ricreato o è passata la finestra di Conservazione, lo ZIP è andato perduto mentre la riga della cronologia rimane. Riesegui l'esportazione. |
| "Il periodo non deve superare X anni" | Riduci il periodo o aumenta `AuditExport__MaxRangeYears`. |
| Il job rimane in *Running* per molto tempo | Gli Archivio di grandi dimensioni vengono trasmessi in batch di 1000 email; il progresso viene aggiornato continuamente. Periodi molto ampi possono richiedere tempo — controlla il contatore elaborati/totali. |
| Lo strumento di importazione segnala problemi di codifica | Il pacchetto è UTF-8 senza BOM con flag `UTF8` dichiarato e delimitatore di record esplicito; assicurati che lo strumento legga il `INDEX.XML` accanto ai file di dati (estrai l'intero ZIP, non aprire i file singolarmente dall'interno dello ZIP). |

## 🔗 Documentazione Correlata {#related-documentation}

- [Access Logging](Logs.md)
- [Installation, Setup and Parameters](Setup.md)
