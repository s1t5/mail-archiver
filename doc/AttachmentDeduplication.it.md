# 🧬 Deduplicazione Allegati {#attachment-deduplication}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

Gli Allegati delle e-mail sono memorizzati come payload binari nel database. Lo **stesso file**
viene molto spesso allegato a molte e-mail (loghi aziendali nelle firme, PDF inoltrati,
newsletter, contratti, ...). Senza deduplicazione, ogni allegato mantiene
la propria copia privata di quei byte, il che spreca una grande quantità di spazio nel database.

La **deduplicazione degli allegati** memorizza ogni payload *unico* esattamente una volta in una
tabella indirizzata per contenuto (identificata dall'hash SHA-256 dei byte) e consente a tutti
gli allegati identici di fare riferimento a quella singola copia condivisa. Ciò riduce
tipicamente in modo significativo l'impronta di archiviazione degli Archivio ricchi di allegati.

## ⚙️ Come funziona {#how-it-works}

### Nuovi allegati {#new-attachments}
Ogni volta che una nuova e-mail viene archiviata (IMAP, Microsoft 365 / Graph, importazione EML, importazione MBOX
...), il payload dell'allegato viene sottoposto a hashing e memorizzato una sola volta. Se lo stesso payload
esiste già, il nuovo allegato si limita a fare riferimento alla riga di contenuto esistente.
Questo avviene in modo trasparente a livello di dati, quindi ogni percorso di archiviazione è coperto
automaticamente.

### Dati esistenti (migrazione) {#existing-data-migration}
Per le installazioni che contengono già allegati archiviati **prima** che questa funzione
venisse introdotta, una **migrazione in background robusta e riprendibile** converte i payload
inline esistenti in righe di contenuto condivise:

- Elabora gli allegati **batch per batch**, ogni batch in una propria transazione.
- Il progresso è persistito in una tabella di stato dedicata, quindi la migrazione **sopravvive a un
  riavvio dell'applicazione** e continua semplicemente da dove si era interrotta.
- È **idempotente** – gli allegati già migrati non vengono mai più toccati, anche
  dopo un crash o un riavvio.
- Esegue in background e non blocca l'uso normale dell'applicazione.

Al termine della migrazione, viene registrato un messaggio di completamento, ad esempio:

```
Attachment Deduplication migration COMPLETED. 36199 attachments processed in 1.6 minutes.
```

Al successivo avvio, rileva che non c'è altro da fare e rimane inattivo. Qualora dovessero
rimanere allegati non deduplicati (ad esempio dopo il ripristino di un vecchio
backup), la migrazione riprende automaticamente e li migra — il sistema converge sempre
verso uno stato completamente deduplicato.

### Garbage collection (sempre attiva) {#garbage-collection-always-on}
Quando le e-mail (e quindi gli allegati) vengono eliminate, un payload condiviso può diventare
privo di riferimenti. Una garbage collection integrata rimuove queste righe di contenuto orfane.

> 🧹 **La pulizia degli orfani viene SEMPRE eseguita**, indipendentemente dalla
> funzione `DatabaseMaintenance`. Viene eseguita una volta subito dopo la migrazione e poi
> periodicamente (ogni `OrphanCleanupIntervalHours`, predefinito 12 h). Quando
> `DatabaseMaintenance__Enabled=true`, la stessa pulizia viene eseguita anche prima del
> `VACUUM` giornaliero, in modo che lo spazio liberato venga fisicamente recuperato.

### Utilizzo del disco dopo la migrazione (importante) {#disk-usage-after-the-migration-important}

> 📈 **Il valore "Storage" sulla dashboard può aumentare brevemente subito dopo la
> migrazione** (ad es. 3.5 GB → 5.3 GB). Questo è previsto e non è un bug.
>
> La dashboard riporta la dimensione **fisica** del database (`pg_database_size`). Quando la
> migrazione sposta un payload nello storage condiviso, imposta la vecchia copia inline a `NULL`,
> ma PostgreSQL (MVCC) mantiene quei vecchi byte come *dead tuples* finché il file della tabella non viene
> riscritto — quindi per un momento il database contiene sia la vecchia che la nuova copia.
>
> Per risolvere automaticamente questo problema, l'applicazione esegue un `VACUUM FULL`
> **una tantum** sulla tabella degli allegati **una volta completata la migrazione e mentre non sono in corso
> job di sincronizzazione**. Questo riscrive la tabella e restituisce lo spazio liberato al
> sistema operativo; in seguito la dimensione riportata scende **al di sotto** dell'originale. Viene eseguito
> esattamente una volta (tracciato tramite un marker `ReclaimedAt`), è sicuro in caso di riavvio ed è differito
> finché una sincronizzazione è in corso (riprova automaticamente una volta inattivo).

>
> ⚠️ Il `VACUUM FULL` blocca la tabella degli allegati per la sua durata (nessuna lettura/scrittura di allegati
> nel frattempo) e necessita di un po' di spazio libero su disco per la riscrittura. Se si desidera
> attivarlo manualmente, eseguire:
> ```sql
> VACUUM (FULL, ANALYZE) mail_archiver."EmailAttachments";
> ```

## 🔧 Configurazione {#configuration}


Queste impostazioni sono opzionali; i valori predefiniti sono sensati per la maggior parte delle installazioni. Vedere anche
la [Guida alla configurazione](Setup.md#-attachment-deduplication-settings).

| Impostazione | Predefinito | Descrizione |
|---------|---------|-------------|
| `AttachmentDeduplication__BatchSize` | `200` | Numero di allegati esistenti migrati per transazione. Valori più grandi migrano più velocemente ma aumentano il carico di memoria/DB per batch. |
| `AttachmentDeduplication__DelayBetweenBatchesMs` | `0` | Pausa opzionale (ms) tra i batch di migrazione per limitare il carico sul DB in sistemi molto occupati. |
| `AttachmentDeduplication__StartupDelaySeconds` | `20` | Ritardo (s) dopo l'avvio dell'app prima dell'inizio della migrazione (permette alla migrazione dello schema di terminare prima). |
| `AttachmentDeduplication__OrphanCleanupIntervalHours` | `12` | Intervallo (h) della garbage collection degli orfani sempre attiva. |
| `AttachmentDeduplication__CommandTimeoutSeconds` | `300` | Timeout del comando del database (in secondi) per le operazioni dei batch di migrazione. Il predefinito è 5 minuti. Se un batch va ancora in timeout, il servizio riprova automaticamente con la metà della dimensione del batch. |

Esempio (`docker-compose.yml`):

```yaml
environment:
  - AttachmentDeduplication__BatchSize=200
  - AttachmentDeduplication__DelayBetweenBatchesMs=0
  - AttachmentDeduplication__StartupDelaySeconds=20
  - AttachmentDeduplication__OrphanCleanupIntervalHours=12
  - AttachmentDeduplication__CommandTimeoutSeconds=300
```

## ❓ FAQ {#faq}

**Posso disabilitare la deduplicazione?**
No. È una funzione di storage fondamentale ed è sempre attiva. È possibile regolare solo la dimensione del batch
e i parametri di pianificazione sopra indicati.

**I miei dati sono al sicuro durante la migrazione?**
Sì. Ogni batch è una singola transazione e il lavoro è idempotente. Un crash o un
riavvio a metà migrazione annulla il batch non completato e questo viene rielaborato al
successivo avvio; nessun dato viene perso o duplicato.

**Rallenta l'archiviazione?**
L'overhead di hashing per allegato è trascurabile. La migrazione una tantum dei
dati esistenti viene eseguita in background e può essere limitata con
`DelayBetweenBatchesMs` se necessario.

**La migrazione continua a andare in timeout e a riavviarsi allo stesso cursore. Cosa posso fare?**
Questo può accadere su database molto grandi (centinaia di migliaia di e-mail) dove un singolo batch di 200 allegati con hashing SHA-256 supera il timeout predefinito del comando. Il servizio ora riprova automaticamente con la metà della dimensione del batch quando si verifica un timeout. Se si riscontrano ancora timeout, è possibile ridurre manualmente la dimensione del batch e/o aumentare il timeout del comando:

```yaml
environment:
  - AttachmentDeduplication__BatchSize=10
  - AttachmentDeduplication__CommandTimeoutSeconds=600
```

**Quanto spazio risparmierò?**
Dipende interamente da quanti allegati duplicati contiene il tuo Archivio.
Gli Archivio con molte firme, loghi o file inoltrati ripetuti ne traggono maggior beneficio.

---
