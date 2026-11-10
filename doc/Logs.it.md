# 📋 Access Log {#access-log}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida spiega la funzionalità del registro di accesso nell'applicazione Mail Archiver, che consente la registrazione e la visualizzazione delle attività degli utenti all'interno del sistema. Questa funzionalità è fondamentale per scopi di monitoraggio, sicurezza e debugging.

## 📊 Cosa viene registrato {#what-is-logged}

L'applicazione registra vari tipi di attività degli utenti, tra cui:

1. **Login e Logout** - Registrazione delle operazioni di login e logout
2. **Operazioni di ricerca** - Registrazione delle richieste di ricerca e-mail all'interno del sistema
3. **Apertura e-mail** - Registrazione quando gli utenti aprono il contenuto di un'e-mail
4. **Download e-mail** - Registrazione quando gli utenti scaricano e-mail
5. **Ripristino e-mail** - Registrazione quando gli utenti effettuano il ripristino di e-mail archiviate
6. **Gestione Account** - Registrazione delle azioni relative all'Account
7. **Operazioni di eliminazione** - Registrazione delle azioni di eliminazione
8. **Esportazione dati di audit** - Registrazione delle esecuzioni di esportazione dei dati di audit (inizio e risultato, vedi [Audit Data Export](AuditExport.md))

## 📝 Tipi di azione e descrizioni {#action-types-and-descriptions}

Le azioni registrate sono categorizzate nei seguenti tipi:

### Login {#login}
- **Descrizione**: Registra quando un utente effettua il login nel sistema
- **Dettagli**: Username, timestamp

### Logout {#logout}
- **Descrizione**: Registra quando un utente effettua il logout dal sistema
- **Dettagli**: Username, timestamp

### Search {#search}
- **Descrizione**: Registra le richieste di ricerca all'interno del sistema e-mail
- **Dettagli**: Username, timestamp, parametri di ricerca

### Open {#open}
- **Descrizione**: Registra quando un utente apre un'e-mail
- **Dettagli**: Username, timestamp, email ID, mittente, oggetto

### Download {#download}
- **Descrizione**: Registra quando gli utenti scaricano e-mail
- **Dettagli**: Username, timestamp, email ID, mittente, oggetto

### Restore {#restore}
- **Descrizione**: Registra quando gli utenti effettuano il ripristino di e-mail archiviate
- **Dettagli**: Username, timestamp, email ID, mittente, oggetto

### Account {#account}
- **Descrizione**: Registra le azioni relative all'Account (creazione, modifica, ecc.)
- **Dettagli**: Username, timestamp, account ID

### Deletion {#deletion}
- **Descrizione**: Registra le operazioni di eliminazione su e-mail o altre risorse
- **Dettagli**: Username, timestamp, risorsa interessata
- **Nota sulla conformità**: Le operazioni di eliminazione vengono registrate per scopi di audit di conformità

## 🔍 Opzioni di filtro {#filter-options}

La pagina del registro offre le seguenti opzioni di filtro:

1. **Date** - Filtra per intervallo di date (Da-A)
2. **Username** - Filtra per utente specifico (Solo Admin)
3. **Action Type** - Filtra per tipo di azione specifico

## 🖥️ Visualizzazione del registro {#log-display}

I log sono visualizzati in formato tabella con le seguenti colonne:

- **Timestamp**: Data e ora dell'azione
- **Username**: Nome dell'utente coinvolto
- **Type**: Tipo di azione con codifica a colori
- **Information**: Dettagli aggiuntivi sull'azione

Per i dispositivi mobili, i log sono visualizzati in formato card.

## 🔐 Autorizzazioni di accesso {#access-permissions}

- **Utenti non-admin**: Possono visualizzare solo i propri log
- **Utenti Admin**: Possono visualizzare i log di tutti gli utenti e filtrare per utente

## 📤 Audit Data Export {#audit-data-export}

Gli Admin possono aprire la pagina dedicata [Audit Data Export](AuditExport.md) direttamente dalla pagina dei Log. Crea pacchetti di dati massivi tabulari (INDEX.XML + CSV) dall'Archivio per strumenti di audit esterni. Ogni esecuzione di esportazione scrive due voci nel registro di accesso di tipo **Audit Data Export** (inizio e risultato), che sono elencate e filtrabili nella pagina dei Log come tutte le altre voci.

Per i dettagli consultare la [guida Audit Data Export](AuditExport.md).
