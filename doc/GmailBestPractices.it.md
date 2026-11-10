# 📧 Gmail Best Practices {#gmail-best-practices}

[← Back to Documentation Index](index.md)

Quando si utilizzano Account Gmail con l'applicazione Mail Archiver, ci sono diverse configurazioni specifiche e best practice da seguire per garantire prestazioni ottimali ed evitare potenziali problemi.

## 🔐 IMAP Configuration with App Passwords {#imap-configuration-with-app-passwords}

Gli Account Gmail possono essere integrati con l'applicazione Mail Archiver utilizzando IMAP con App Passwords:

1. Abilita l'autenticazione a due fattori sul tuo Account Gmail
2. Genera una App Password specificamente per l'applicazione Mail Archiver
3. Usa la App Password invece della tua password Gmail abituale quando configuri l'Account in Mail Archiver

## 🏷️ Gmail Label Settings {#gmail-label-settings}

Si raccomanda vivamente di regolare le impostazioni delle etichette Gmail per evitare che l'etichetta "All Mail" venga esposta tramite IMAP:

1. Vai alla webinterface di Gmail e apri le impostazioni (icona dell'ingranaggio in alto) → See all settings → Labels tab
2. Trova l'etichetta "All Mail" nell'elenco
3. Deseleziona l'opzione "Show in IMAP" per "All Mail"

### 📚 Background on Gmail Label Implementation {#background-on-gmail-label-implementation}

Il sistema di etichette di Gmail funziona diversamente dai fornitori di posta elettronica tradizionali. In Gmail:
- Tutte le email esistono in un unico repository "All Mail"
- Le etichette agiscono come tag piuttosto che come Cartella
- L'etichetta "All Mail" contiene ogni email nel tuo Account
- Quando un'email viene etichettata, appare sia nella Cartella originale che nella sezione etichettata. Pertanto, tutte le email nella tua posta in arrivo appaiono anche nell'etichetta All Mail.

### ⚠️ Issues with IMAP Retrieval {#issues-with-imap-retrieval}

Quando "All Mail" viene esposta tramite IMAP, può causare diversi problemi:

1. **Duplicate Processing**: L'Archivio potrebbe elaborare la stessa email più volte poiché appare in più Cartella/etichette
2. **Performance Impact**: Il recupero di "All Mail" può essere estremamente lento per Account di grandi dimensioni con migliaia di email
4. **Synchronization Issues**: Possono sorgere conflitti quando si cerca di determinare a quale Cartella appartenga effettivamente un'email

Disabilitando l'accesso IMAP a "All Mail", si garantisce che le email vengano recuperate una sola volta dalla loro posizione primaria, migliorando sia le prestazioni che l'accuratezza.

## 🗑️ Retention Policy and Expunge Settings {#retention-policy-and-expunge-settings}

Come menzionato nel README principale, gli Account Gmail richiedono particolare attenzione riguardo alle policy di Conservazione:

- La funzione Auto-Expunge di Gmail deve essere disabilitata nelle impostazioni di Gmail sotto la scheda "Forwarding and POP/IMAP"
- Questa impostazione è fondamentale affinché le policy di Conservazione funzionino correttamente con gli Account Gmail

Per configurare questa impostazione:
1. Vai in Gmail Settings → See all settings → Forwarding and POP/IMAP tab
2. Nella sezione "IMAP Access", seleziona "Auto-Expunge off - Wait for the client to update the server." e "Move the message to the Bin"

## ⚠️ Gmail IMAP Rate Limiting {#gmail-imap-rate-limiting}

Gmail impone un limite di velocità giornaliero di 2500MB per il recupero IMAP. Quando si archiviano grandi volumi di email, tieni presente che potresti raggiungere questo limite, il che potrebbe mettere in pausa temporaneamente il recupero delle email fino al giorno successivo.

### 📊 Rate Limit Handling {#rate-limit-handling}

Mail Archiver fornisce una gestione integrata del limite di velocità per gestire i limiti di banda IMAP di Gmail:

- **Automatic bandwidth tracking**: Monitora i dati scaricati per Account al giorno
- **Configurable limits**: Imposta `BandwidthTracking__DailyLimitMb=2500` per Gmail
- **Automatic pause**: La Sincronizzazione si mette in pausa quando viene raggiunto il limite e riprende dopo il reset
- **Checkpoint system**: Il progresso della Sincronizzazione viene salvato e ripreso dall'ultima posizione

Per una configurazione e un utilizzo dettagliati, consulta la **[Rate Limit Handling Guide](RateLimitHandling.md)**.

### 🔧 Gmail Configuration Example {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 Large Mailbox Considerations {#large-mailbox-considerations}

Per Casella di posta Gmail di grandi dimensioni (ad es. 14 GB), la Sincronizzazione iniziale richiederà più giorni:

| Mailbox Size | Days to Complete (at 2500 MB/day) |
|--------------|-----------------------------------|
| 5 GB | ~2 days |
| 10 GB | ~4 days |
| 14 GB | ~6 days |
| 20 GB | ~8 days |

**Tip**: Abilita il monitoraggio della banda **prima** di aggiungere grandi Account Gmail per garantire una corretta gestione del limite di velocità fin dall'inizio.

## ✅ Best Practices Summary {#best-practices-summary}

Seguire queste best practice aiuterà a garantire il regolare funzionamento di Mail Archiver con i tuoi Account Gmail, mantenendo l'integrità dei dati e prestazioni ottimali.
