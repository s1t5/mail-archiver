# 💾 💾 Visualizzazione dello spazio di archiviazione per Account {#per-account-storage-display}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

La tabella "Account Overview" della Dashboard e la tabella "Show All" di MailAccounts includono una colonna **Storage**. Questa colonna mostra quanto spazio nel database sta utilizzando ogni account — inclusi **tutti i campi di ogni mail archiviata** più **tutti gli allegati**.

## ⚙️ Come funziona {#how-it-works}

I valori di Storage sono **memorizzati nella cache** nella tabella del database `AccountStorageCache`. Ciò significa che le pagine web leggono valori pre-calcolati invece di eseguire una query onerosa ogni volta che si apre la Dashboard o l'elenco degli account. La cache viene mantenuta aggiornata da un servizio in background indipendente (`AccountStorageRefreshService`).

Per ogni account, il servizio chiede a PostgreSQL di sommare `pg_column_size` per ogni colonna delle righe dell'account nella tabella `ArchivedEmails`. Questo copre **tutti i campi di una mail** (Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash, ecc.), inclusa la compressione PostgreSQL e lo storage out-of-line (TOAST). La dimensione di un valore out-of-line viene letta dal suo puntatore TOAST, quindi il calcolo scansiona solo la tabella stessa e non carica mai i corpi delle mail. La parte relativa agli allegati viene aggiunta separatamente come somma logica di `EmailAttachment.Size`.

Il servizio di aggiornamento aggiorna la cache in tre situazioni:

1. **Startup backfill** — Dopo una nuova installazione o dopo l'aggiornamento alla versione che introduce questa funzionalità, ogni account inizia senza un valore in cache. Il servizio esamina tutti gli account uno per uno e calcola il primo valore.
2. **Daily refresh** — Una volta al giorno all'orario configurato (predefinito `02:30`), il servizio ricalcola tutti gli account come misura di sicurezza.
3. **Immediate refresh** — Dopo una sincronizzazione delle mail, un importazione EML, un'importazione MBOX o una cancellazione per conservazione, l'account interessato viene ricalcolato immediatamente in modo che il valore visualizzato rimanga aggiornato.

## 🤔 Perché inizialmente viene mostrato "0 B" {#why-0-b-is-shown-at-first}

Quando la funzionalità viene abilitata per la prima volta, o quando viene creato un nuovo account, la riga della cache per quell'account non esiste ancora. Il servizio di aggiornamento viene eseguito in background e calcola gli account **uno alla volta**, con un piccolo ritardo tra un account e l'altro affinché gli archivi molto grandi non sovraccarichino il database.
Finché il servizio non ha elaborato un account, l'interfaccia utente mostra **"0 B"** come segnaposto. Questo è completamente normale. Non appena il servizio in background termina l'elaborazione di quell'account, il valore reale appare al caricamento o all'aggiornamento successivo della pagina.
Per i grandi archivi esistenti, il backfill può richiedere diversi minuti o più a lungo. Il servizio monitora il proprio progresso in una tabella dedicata (`AccountStorageBackfillState`), quindi se l'applicazione viene riavviata a metà del backfill, riprende semplicemente da dove aveva interrotto — nessun account viene calcolato due volte.

## 🔧 Configurazione {#configuration}

La funzionalità può essere configurata in `appsettings.json` o tramite variabili d'ambiente. Consulta la sezione **Account Storage Settings** in [Setup.md](Setup.md) per tutte le opzioni disponibili, tra cui:

- `AccountStorage__Enabled`: Attiva o disattiva il servizio di aggiornamento (`true` di default).
- `AccountStorage__DailyExecutionTime`: Tempo per il refresh completo giornaliero (`02:30` di default).
- `AccountStorage__BackfillDelayMs`: Pausa tra gli account durante lo startup backfill (`5000` ms di default).
- `AccountStorage__RefreshBatchDelayMs`: Pausa tra gli account durante il refresh giornaliero (`1000` ms di default).
- `AccountStorage__CommandTimeoutSeconds`: Timeout del comando database per il calcolo dello storage per account (`300` secondi di default). Aumenta questo valore per archivi molto grandi dove la scansione `pg_column_size` richiede più tempo.

> 💡 **Suggerimento**: Non è necessario abilitare `DatabaseMaintenance__Enabled` affinché questa funzione funzioni. Il servizio di aggiornamento dello storage viene eseguito indipendentemente.
