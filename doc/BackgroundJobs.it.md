# ⚙️ Background Jobs {#background-jobs}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

La maggior parte del lavoro in Mail Archiver non avviene mentre aspetti. La Sincronizzazione di una Casella di posta, l'importazione di un file MBOX, il ripristino di migliaia di email o l'esportazione di un Archivio possono richiedere da minuti a ore. Tali attività vengono eseguite come **background jobs** — le avvii e continuano a girare anche se chiudi il browser.

La pagina **Background Jobs** mostra cosa è attualmente in esecuzione, cosa è terminato e cosa è andato storto. È il posto dove guardare quando qualcosa richiede più tempo del previsto o quando una sincronizzazione termina con errori.

> **ℹ️ Pagina per l'Amministratore.** Background Jobs richiede diritti di amministratore ed è raggiungibile dal menu principale. Gli utenti regolari non la vedono — possono comunque avviare i job (ad esempio un ripristino batch), ma il monitoraggio avviene qui.

## 🗂️ Tipi di Job {#job-types}

La pagina raggruppa i job per tipo. Ogni sezione mostra fino ai **20 più recenti**, con i job in esecuzione e in coda ordinati in alto — in modo che un vecchio job non venga nascosto da quelli più recenti.

| Sezione | Cosa copre | Avviato da |
|---|---|---|
| **Synchronization Jobs** | Recupero di nuova posta dal server mail | Programmazione automatica, o *Sync now* su un Account |
| **MBox Import Jobs** | Importazione di file `.mbox` | Mail Accounts → [Import MBOX](Import-EML-MBox.md#-import-mbox) |
| **EML Import Jobs** | Importazione di file `.eml` / Archivio ZIP | Mail Accounts → [Import EML](Import-EML-MBox.md#-import-eml) |
| **Batch Restore Jobs** | Copia di email selezionate nuovamente in una Casella di posta | Archive → select emails → Batch Restore |
| **Account Export Jobs** | Esportazione di un intero Account (mbox / EML) | Mail Accounts → [Export](Export-Account.md) |
| **Selected Emails Export Jobs** | Esportazione di un set di email selezionate manualmente | Archive → select emails → Export |
| **Email Deletion Jobs** | Eliminazione di email dal server o dall'Archivio, inclusa l'eliminazione massiva basata su filtri | Policy di Conservazione, eliminazione manuale, o Archive → *Delete all matches* |
| **Account Deletion Jobs** | Rimozione di un intero Account mail | Mail Accounts → Delete |
| **Offload** | Spostamento della posta con finestra temporale in un altro Account | Mail Accounts → Offload |

## 📊 Leggere una riga di un Job {#reading-a-job-row}

Ogni riga presenta lo stesso set di colonne:

| Colonna | Significato |
|---|---|
| **Job ID** | Identificatore breve — citarlo quando si segnala un problema |
| **Account** | La Casella di posta su cui lavora il job |
| **Status** | Stato attuale, vedi la tabella seguente |
| **Progress** | Quanto è avanzato il job, con elaborati/totale dove noto |
| **Created** | Quando il job è stato messo in coda |
| **Started** | Quando ha effettivamente iniziato l'esecuzione |
| **Completed** | Quando è terminato (vuoto se è ancora in esecuzione) |
| **Duration** | Tempo trascorso |

Alcuni tipi di job aggiungono dei contatori alla colonna progress:

- **Success** / **Failed** — email elaborate con successo o senza successo
- **Skipped (duplicates)** — email già presenti nell'Archivio, non importate due volte
- **Recovered** — messaggi recuperati dopo che il server ha segnalato la loro mancanza
- **Malformed (skipped)** — messaggi che non potevano essere analizzati e sono stati saltati

### Valori di Status {#status-values}

| Status | Significato |
|---|---|
| **Queued** | In attesa di inizio — un altro job è in esecuzione, o il worker è occupato |
| **Running** | Attualmente in esecuzione |
| **Completed** | Terminato con successo |
| **CompletedWithErrors** | Terminato, ma alcuni elementi sono falliti (importazioni MBox) |
| **Failed** | Terminato con un errore |
| **Cancelled** | Interrotto da un utente |
| **Rate Limited** | *Sincronizzazione in pausa a causa del limite di banda. Riprenderà automaticamente.* |
| **Timed Out** | La sincronizzazione ha superato il limite di tempo configurato ed è stata interrotta |

> **ℹ️ "Rate Limited" e "Timed Out" sono stati esclusivi della sincronizzazione.** Sembrano fallimenti ma non lo sono: una sincronizzazione con rate limit riprende da sola una volta che la quota di banda lo consente, e una sincronizzazione in timeout continua dal suo checkpoint alla prossima esecuzione. Vedi [Rate Limit Handling](RateLimitHandling.md) e [Mail Synchronization](Synchronization.md).

### Perché il progresso può rimanere allo 0% {#why-progress-can-sit-at-0}

Il progresso è misurato in base alle **Cartelle completate**, non alle singole email:

> *Il progresso è misurato dalle Cartelle completate. Una cartella di grandi dimensioni (es. INBOX) mantiene la barra allo 0% finché non viene completata.*

Su una Casella di posta in cui INBOX contiene la maggior parte della posta, la barra può rimanere allo 0% per molto tempo e poi saltare improvvisamente. È normale — il job sta lavorando, semplicemente non ha ancora terminato la sua prima cartella di grandi dimensioni.

## 🔄 La pagina si aggiorna da sola {#the-page-refreshes-itself}

Mentre la pagina è aperta, si ricarica ogni **30 secondi**, così puoi lasciarla in una scheda e monitorare il progresso senza cliccare nulla. L'etichetta mostra quando i dati sono stati aggiornati l'ultima volta.

Il ricaricamento viene annullato nel momento in cui clicchi un link o un pulsante — altrimenti il tuo clic finirebbe su una pagina che viene sostituita proprio sotto di esso.

**Configurabile:** `Jobs:RefreshSeconds` in `appsettings.json` (default `30`).
Imposta a `0` per disattivare il ricaricamento automatico. Ogni ricaricamento ricostruisce la pagina da tutte le sorgenti dei job, quindi un intervallo molto breve su un'installazione con molti Account è un carico reale — e quel carico si moltiplica per ogni scheda aperta.

```json
"Jobs": {
  "RefreshSeconds": 30
}
```

## 🛑 Annullare un Job {#cancelling-a-job}

I job che supportano l'annullamento mostrano un pulsante **Cancel Job**, che chiede:

> *Sei sicuro di voler annullare questo job?*

I job di importazione usano la dicitura **Cancel Import** / *Sei sicuro di voler annullare questo job di importazione?*

L'annullamento è disponibile per i job di **sincronizzazione, ripristino batch, esportazione di email selezionate e eliminazione email**. Il job si ferma non appena l'elemento corrente è terminato; lo stato diventa **Cancelled**.

> **⚠️ Annullare non è un rollback.** Le email già importate, ripristinate, esportate o eliminate rimangono tali. Annullare impedisce al job di fare *altro* — non annulla ciò che ha già fatto. Per un job di eliminazione, questo significa che le email già rimosse sono sparite.

I job di Account e di importazione MBox non possono essere annullati da questa pagina; interrompili invece dalla pagina di importazione specifica dell'Account.

## ⚠️ Errori di Sincronizzazione e "Acknowledge Failures" {#sync-failures-and-acknowledge-failures}

Quando una sincronizzazione termina con lo stato **Failed**, la riga del job offre l'opzione **Acknowledge Failures**. Questa è l'unica azione sulla pagina che cambia il comportamento futuro, quindi vale la pena capirla.

Ogni esecuzione di sincronizzazione parte da dove l'ultima si è fermata, utilizzando un **checkpoint**. Se una sincronizzazione fallisce per alcune email, il checkpoint rimane prima di quelle email in modo che la successiva esecuzione le riprovi. Questo è il comportamento predefinito sicuro — nulla viene saltato silenziosamente.

Ma se i fallimenti sono permanenti (un messaggio corrotto, una cartella che il server rifiuta di servire), ogni sincronizzazione successiva proverà a riprenderli e la sincronizzazione continuerà a fallire nello stesso punto.

**Acknowledge Failures** risolve questo stallo facendo avanzare il checkpoint oltre le email fallite. La conferma è esplicita riguardo alle conseguenze:

> *Questo farà avanzare il timestamp della sincronizzazione all'ora di completamento di questo job, quindi la prossima sincronizzazione sarà una normale sincronizzazione incrementale. Le email fallite non verranno riprovate. Continuare?*

In caso di successo vedrai: *"Sync timestamp has been advanced. The next sync will be incremental."*

> **⚠️ Conferma solo dopo aver esaminato i fallimenti.** Dopo la conferma, quelle email **non** verranno riprovate — mancheranno dall'Archivio a meno che tu non le recuperi in altro modo. Controlla prima i dettagli del fallimento e, se la causa è una configurazione errata, correggila prima di confermare. Vedi [Mail Synchronization](Synchronization.md) per il comportamento del checkpoint nel dettaglio.

L'azione di conferma viene registrata nel registro di controllo come *SyncAcknowledgeFailures*, con il numero di email fallite e l'Account, in modo da essere tracciabile in seguito.

## 👁️ Cosa non puoi fare qui {#what-you-cannot-do-here}

- **Nessun pulsante di riprova.** Un job fallito non viene riavviato da questa pagina — avvia nuovamente l'operazione da dove l'avevi iniziata (sincronizzazione, importazione, esportazione).
- **Nessuna modifica.** I parametri del job sono fissi al momento della creazione.
- **Solo gli ultimi 20 per tipo.** I job più vecchi non sono elencati. Per registrazioni a lungo termine, il registro di controllo e il log dell'applicazione sono le fonti — vedi [Access Logging](Logs.md) e [Docker Compose Logs](DockerComposeLogs.md).

## ❓ Risoluzione dei problemi {#troubleshooting}

| Sintomo | Causa probabile | Cosa fare |
|---|---|---|
| Il job rimane **Queued** | Un altro job dello stesso worker è in esecuzione | Attendi; controlla le altre sezioni per un job a lunga esecuzione |
| Progresso bloccato allo 0% | Una cartella di grandi dimensioni è ancora in fase di elaborazione | Normale — vedi "Perché il progresso può rimanere allo 0%" |
| Sincronizzazione ripetutamente **Failed** sullo stesso Account | Errori permanenti su singole email | Ispeziona i fallimenti, correggi la causa, quindi conferma |
| Sincronizzazione mostra **Rate Limited** | Quota di banda raggiunta | Nulla — riprende automaticamente |
| Sincronizzazione mostra **Timed Out** | Casella di posta più grande del limite di tempo | Continuerà dal checkpoint alla prossima esecuzione; vedi [Synchronization](Synchronization.md) |
| **CompletedWithErrors** su un'importazione MBox | Alcuni messaggi malformati | I contatori mostrano quanti; il resto è stato importato |
| Il job scompare dopo il ricaricamento | È uscito dalla finestra degli "ultimi 20" | Controlla il log dell'applicazione per il suo stato finale |
| La pagina si ricarica mentre lavori | Refresh automatico ogni 30 s | Imposta `Jobs:RefreshSeconds` a `0` |

## 📚 Documentazione correlata {#related-documentation}

- [Mail Synchronization (Quick vs. Full Sync)](Synchronization.md) — checkpoint, sincronizzazioni in timeout
- [Rate Limit Handling](RateLimitHandling.md) — quote di banda e ripresa automatica
- [Access Logging](Logs.md) — chi ha fatto cosa, incluse le azioni sui job
- [Batch Restore](MailboxMigration.md) — spostamento di email tra Caselle di posta
- [Date-Windowed Offload](Offload.md) — job di offload
- [Docker Compose Logs](DockerComposeLogs.md) — lettura del log dell'applicazione
