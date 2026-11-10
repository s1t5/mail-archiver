# 📥 Guida alla migrazione della Casella di posta {#mailbox-migration-guide}

[← Torna all'Indice della Documentazione](index.md)

Questa guida spiega come migrare una Casella di posta da un fornitore di posta elettronica a un altro utilizzando l'applicazione Mail Archiver.

## 📋 Panoramica {#overview}

L'applicazione Mail Archiver fornisce la funzionalità per migrare le email da una Casella di posta di origine a una Casella di posta di destinazione. Questo è particolarmente utile quando si cambia fornitore di posta elettronica preservando la cronologia delle email. Anche la struttura delle Cartelle della Casella di posta di origine viene preservata durante la migrazione.

> **Vuoi migrare solo le email recenti?** Il flusso descritto di seguito copia l'intera Casella di posta in Archivio, ed eseguendolo due volte aggiunge tutto una seconda volta. Se la nuova Casella di posta deve contenere solo le email più recenti rispetto a una data di interruzione, mentre la cronologia completa rimane nell'Archivio, utilizza invece il [date-windowed offload](Offload.md). Questo metodo accetta una finestra temporale, può escludere e rinominare le Cartelle, offre una simulazione (dry run) ed è sicuro da ripetere perché salta ciò che la destinazione contiene già. È inoltre gestibile da riga di comando, il che rende possibile l'automazione di una flotta di Caselle di posta tramite script.

## 🚀 Processo di migrazione {#migration-process}

Segui questi passaggi per migrare la tua Casella di posta:

1. **Aggiungi l'Account di origine**
   - Naviga nella sezione "Email Accounts"
   - Clicca su "Add Account"
   - Inserisci i dettagli e le credenziali dell'Account di origine
   - Salva l'Account

2. **Sincronizza l'Account di origine**
   - Consenti all'applicazione di effettuare la Sincronizzazione di tutte le email dall'Account di origine
   - Attendi il completamento del processo di Sincronizzazione

3. **Aggiungi l'Account di destinazione**
   - Nella sezione "Email Accounts", clicca su "Add Account"
   - Inserisci i dettagli e le credenziali dell'Account di destinazione
   - Salva l'Account

4. **Sincronizza l'Account di destinazione**
   - Se l'Account di destinazione è nuovo, effettua la Sincronizzazione per assicurarti che sia configurato correttamente
   - Attendi il completamento del processo di Sincronizzazione

5. **Avvia la migrazione**
   - Nella sezione "Email Accounts", identifica l'Account di origine
   - Vai ai dettagli dell'Account
   - Seleziona l'opzione "Copy All Emails to Another Mailbox"
   - Scegli l'Account di destinazione e una Cartella di destinazione
   - Abilita l'opzione **"Preserve Folder Structure"** – la Cartella di destinazione selezionata fungerà quindi da radice sotto la quale l'intera struttura delle Cartelle di origine viene ricreata ricorsivamente
   - Se questa opzione non è abilitata, tutte le email vengono collocate direttamente nella Cartella di destinazione selezionata (piatta, senza sottocartelle)
   - Avvia il processo di migrazione

6. **Monitora la migrazione**
   - Se è presente un numero elevato di email, la migrazione verrà eseguita come attività in background
   - Lo stato e l'avanzamento della migrazione possono essere visualizzati nella scheda "Jobs"
   - Attendi il completamento della migrazione con successo

7. **Disabilita l'Account di origine**
   - Dopo il trasferimento con successo, torna all'Account di origine
   - Imposta l'Account di origine su "Disabled" nelle impostazioni dell'Account
   - Ciò garantisce che non venga più archiviato in futuro

## ⚠️ Note importanti {#important-notes}

- Questo flusso non è ripetibile: eseguirlo nuovamente aggiunge ogni messaggio una seconda volta. Il [date-windowed offload](Offload.md) è l'alternativa ripetibile.
- Assicurati che l'Account di origine abbia sufficienti Autorizzazioni per leggere tutte le email
- Assicurati che l'Account di destinazione abbia spazio di archiviazione sufficiente per le email migrate
- Il processo di migrazione può richiedere del tempo a seconda del numero di email e della velocità di connessione
- Si raccomanda di eseguire la migrazione durante le ore di minor traffico per minimizzare l'impatto sul servizio di posta elettronica
- Controlla regolarmente lo stato della migrazione nella scheda "Jobs" per assicurarti che il processo stia procedendo
- Dopo la migrazione, verifica che tutte le email siano state trasferite con successo all'Account di destinazione

## ✅ Best Practices {#best-practices}

- Testa sempre il processo di migrazione con un piccolo sottoinsieme di email prima di migrare l'intera Casella di posta
- Mantieni un Backup delle email importanti prima di iniziare la migrazione
- Programma la migrazione in un momento in cui l'uso della posta elettronica è minimo
- Monitora attentamente l'avanzamento della migrazione e sii pronto a gestire eventuali problemi che potrebbero sorgere
- Una volta completata la migrazione, verifica che tutte le email siano state trasferite correttamente e siano accessibili
