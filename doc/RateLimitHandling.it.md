# 📊 Gestione del Limite di Velocità {#rate-limit-handling}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

Molti fornitori di posta elettronica impongono limiti di larghezza di banda all'accesso IMAP. Questi limiti vanno tipicamente da poche centinaia di megabyte a diversi gigabyte per un periodo di 24 ore. Quando si esegue la Sincronizzazione di grandi Caselle di posta che superano questi limiti, il processo di sincronizzazione normalmente fallirebbe o sarebbe incompleto.

Mail Archiver include un sistema integrato per la gestione del limite di velocità che gestisce automaticamente queste limitazioni, garantendo una Sincronizzazione affidabile anche per grandi Caselle di posta.

## ⚙️ Come Funziona {#how-it-works}

### Monitoraggio Automatico della Larghezza di Banda {#automatic-bandwidth-tracking}

Il sistema monitora continuamente la quantità di dati scaricati durante la Sincronizzazione IMAP. Quando viene raggiunto il limite di larghezza di banda configurato:

1. **La Sincronizzazione si mette in pausa automaticamente**: L'operazione di sincronizzazione corrente si interrompe in modo fluido.
2. **Il progresso viene salvato**: I checkpoint memorizzano l'esatta posizione in cui la sincronizzazione si è fermata.
3. **Aggiornamenti di stato**: L'Account mostra lo stato "Rate-Limited" nella dashboard.
4. **Ripresa Automatica**: Quando il limite si resetta (tipicamente dopo 24 ore), la sincronizzazione riprende dal checkpoint salvato.

### Esempio: Casella di posta da 14 GB con limite giornaliero di 2,5 GB {#example-14-gb-mailbox-with-25-gb-daily-limit}

```
Day 1:  Sync starts → ~2.5 GB downloaded → Rate-Limited
        Status: Rate-Limited, Checkpoint saved

Day 2:  Limit still active → Sync skipped

Day 3:  Limit resets → Sync resumes from checkpoint
        → Additional ~2.5 GB → Rate-Limited again

Day 4-6: Continues...

Day N:   All 14 GB synced → Status: Completed
```

## 🔧 Configurazione {#configuration}

### Abilitare il Monitoraggio della Larghezza di Banda {#enabling-bandwidth-tracking}

Aggiungi la seguente sezione al tuo `appsettings.json`:

```json
{
  "BandwidthTracking": {
    "Enabled": true,
    "DailyLimitMb": 2500,
    "WarningThresholdPercent": 80,
    "PauseHoursOnLimit": 24
  }
}
```

### Opzioni di Configurazione {#configuration-options}

| Opzione | Descrizione | Predefinito | Esempio |
|--------|-------------|---------|---------|
| `Enabled` | Abilita/disabilita il monitoraggio della larghezza di banda | `false` | `true` |
| `DailyLimitMb` | Limite di download giornaliero in megabyte | `2500` | `2500` |
| `WarningThresholdPercent` | Percentuale del limite per attivare gli avvisi nei log | `80` | `80` |
| `PauseHoursOnLimit` | Ore di pausa quando viene raggiunto il limite | `24` | `24` |

### Impostare il Limite Corretto {#setting-the-correct-limit}

Per determinare il valore `DailyLimitMb` appropriato:

1. Controlla la documentazione o i termini di servizio del tuo fornitore di posta elettronica.
2. Cerca i limiti di larghezza di banda IMAP o le quote di download.
3. Contatta l'amministratore di posta elettronica se utilizzi un sistema di posta aziendale.
4. Inizia con un valore conservativo se non sei sicuro (ad es. 2000 MB).
5. Monitora i log per gli avvisi del limite di velocità durante le sincronizzazioni iniziali.

## 👤 Esperienza Utente {#user-experience}

### Indicatori della Dashboard {#dashboard-indicators}

Quando il limite di velocità è attivo, l'Account visualizza:

- **Stato**: "Rate-Limited" 
- **Banner Informativo**: Spiega la situazione e il tempo di reset stimato.
- **Progresso Sincronizzazione**: Mostra le email elaborate prima che il limite sia stato raggiunto.

### Comportamento Automatico {#automatic-behavior}

1. **Nessun Intervento Manuale Richiesto**: Il sistema gestisce tutto automaticamente.
2. **Riavvii dell'Applicazione**: Se l'applicazione si riavvia, lo stato del limite di velocità è preservato nel database.
3. **Account Multipli**: Ogni Account monitora la propria larghezza di banda in modo indipendente.

### Cosa Viene Salvato {#what-gets-saved}

Quando si verifica un limite di velocità, il sistema salva:

- **Checkpoint di Sincronizzazione**: Posizione esatta in ogni Cartella in cui la sincronizzazione si è fermata.
- **Utilizzo della Larghezza di Banda**: Totale byte scaricati per il giorno corrente.
- **Stato del Limite**: Se il limite è stato raggiunto e quando si resetterà.

## 🗄️ Manutenzione del Database {#database-maintenance}

L'`DatabaseMaintenanceService` pulisce automaticamente i vecchi dati:

- **Record di Larghezza di Banda**: Eliminati dopo 7 giorni.
- **Checkpoint di Sincronizzazione**: I checkpoint completati vengono eliminati dopo 30 giorni.
- **Checkpoint Incompleti**: Preservati fino al completamento della sincronizzazione.

## ✅ Best Practices {#best-practices}

### Per Grandi Caselle di Posta {#for-large-mailboxes}

1. **Abilita il Monitoraggio della Larghezza di Banda**: Essenziale per le Caselle di posta più grandi del limite giornaliero.
2. **Pianifica la Sincronizzazione Iniziale**: Una grande Casella di posta potrebbe richiedere diversi giorni per essere completamente sincronizzata.
3. **Monitora il Progresso**: Controlla la dashboard per lo stato del limite di velocità.
4. **Non Interrompere**: Lascia che la ripresa automatica gestisca la continuazione.

### Per gli Amministratori {#for-administrators}

1. **Imposta Limiti Appropriati**: Fai corrispondere l'`DailyLimitMb` ai limiti effettivi del tuo fornitore di posta elettronica.
2. **Monitora i Log**: I messaggi di avviso compaiono all'80% del limite.
3. **Spazio del Database**: Il monitoraggio della larghezza di banda utilizza una quantità minima di spazio di archiviazione aggiuntivo.
4. **Backup Prima delle Modifiche**: Esegui sempre un Backup prima di modificare le impostazioni del limite di velocità.

### Considerazioni sulle Prestazioni {#performance-considerations}

- ✅ **Impatto sulla Memoria**: Trascurabile - monitora solo i byte, non memorizza le email.
- ✅ **Impatto sul Database**: Minimo - piccoli record vengono puliti automaticamente.
- ✅ **Velocità di Sincronizzazione**: Nessun impatto sulle prestazioni durante la sincronizzazione attiva.

## 🛡️ Errori FETCH Transitori / Throttling lato Server {#transient-fetch-errors-server-side-throttling}

Oltre al monitoraggio della larghezza di banda basato sui byte descritto sopra, Mail Archiver rileva e reagisce anche al **throttling per singolo messaggio** segnalato dal server IMAP stesso. Alcuni fornitori IMAP ospitati applicano un throttling per Account o per IP che è **indipendente dalle pause `BatchOperation` configurate**, poiché il throttling è applicato lato server in base al volume complessivo delle richieste durante la sessione corrente.

### Sintomi {#symptoms}

Il server restituisce una risposta `NO` (o `BAD`) a un singolo comando `FETCH` per il corpo di un messaggio, con messaggi quali:

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- risposte contenenti `throttle`, `rate limit` o marcatori simili.

L'autenticazione, l'elenco delle Cartelle e la ricerca stessa dei messaggi di solito hanno ancora successo — fallisce solo il recupero effettivo del messaggio completo.

### Gestione Automatica {#automatic-handling}

Quando viene rilevato un errore transitorio di questo tipo, Mail Archiver applica una strategia di riprovo per singolo messaggio:

1. **Riprova con backoff esponenziale**: Fino a **3 tentativi** per lo stesso messaggio, con ritardi di **5 s → 15 s → 60 s**.
2. **Ripristino della Connessione**: Prima di ogni riprovo, lo stato della connessione, dell'autenticazione e della selezione della Cartella viene convalidato e ripristinato se necessario.
3. **Reset in caso di successo**: Un FETCH riuscito resetta il contatore dei fallimenti consecutivi.
4. **Pausa fluida della sincronizzazione**: Se **10 messaggi consecutivi** falliscono ancora con errori transitori dopo tutti i tentativi, l'esecuzione della sincronizzazione corrente viene messa in pausa in modo fluido — lo stesso meccanismo utilizzato per la pausa del limite di larghezza di banda:
   - Il timestamp `LastSync` **non** viene aggiornato.
   - Se `BandwidthTracking` è abilitato, i checkpoint vengono preservati in modo che la successiva esecuzione della sincronizzazione riprenda dall'ultimo messaggio elaborato con successo.
   - Il lavoro viene contrassegnato come limitato dal tasso di velocità (rate-limited).
   - La prossima sincronizzazione pianificata riprenderà automaticamente.

### Mitigazione {#mitigation}

Se incontri regolarmente il throttling lato server, riduci il tasso di richiesta regolando le impostazioni `BatchOperation`:

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

Nota che alcuni fornitori applicano il throttling in base ai **byte totali** o ai **FETCH totali per finestra temporale** piuttosto che al tasso al secondo. In quei casi:

- Abilita `BandwidthTracking` (vedi sopra) e configura `DailyLimitMb` per corrispondere al limite documentato dal fornitore. La Sincronizzazione si metterà quindi in pausa correttamente quando il budget giornaliero sarà esaurito e riprenderà automaticamente il giorno successivo.
- Le sincronizzazioni iniziali di Caselle di posta molto grandi possono richiedere diversi giorni, il che è il comportamento previsto.

### Cosa è considerato un Errore Transitorio {#what-counts-as-a-transient-error}

Solo le risposte IMAP `NO`/`BAD` con testo o codici di risposta relativi al throttling sono trattate come transitorie. Altri errori (messaggi malformati, problemi di decodifica UTF-8, ecc.) vengono comunque contati come `FailedEmails` immediatamente e **non** attivano la logica di riprovo/pausa.

## 📚 Documentazione Correlata {#related-documentation}

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Procedure di pulizia del database
- [GmailBestPractices.md](GmailBestPractices.md) - Configurazione specifica per Gmail
