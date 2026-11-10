# Archivio di istanze Mailcow {#mailcow-instance-archiving}

[← Torna all'indice della documentazione](index.md)

## Panoramica {#overview}

Mail Archiver può archiviare le Caselle di posta di un'istanza [Mailcow](https://mailcow.email)
centralmente, senza raccogliere le password personali dei proprietari delle Caselle di posta.

Un amministratore connette l'istanza una sola volta con la sua chiave API. Mail Archiver elenca
quindi le Caselle di posta e, per ogni Casella di posta selezionata per l'archivio, crea una **password app dedicata** limitata a `imap_access`. Quella password app viene utilizzata dal provider IMAP esistente per archiviare la Casella di posta e può essere revocata in qualsiasi momento.

Gli amministratori raggiungono il gestore dell'istanza da **Add Account → Mailcow** o dal
menu delle azioni di un Account che è già gestito da Mailcow. Il gestore è riservato agli
amministratori, perché la chiave API memorizzata può gestire le credenziali per ogni Casella di posta
nell'istanza.

## Creazione di Account da Mailcow {#creating-accounts-from-mailcow}

In **Mail Accounts → Add Account**, gli amministratori possono selezionare **Mailcow** come provider.
Il modulo offre quindi le istanze connesse e un'azione **Manage mailboxes**. Selezionando
le Caselle di posta in quel modo si creano normali Account di archivio IMAP con password app dedicate; non viene memorizzato alcun Account Mailcow separato. Se non esiste ancora alcuna istanza, **Manage Mailcow instances**
apre il gestore dell'istanza dove è possibile connettersi.

## Collegamento di un Account esistente {#linking-an-existing-account}

Nella vista di modifica di un Account IMAP, gli amministratori possono cambiare il provider in **Mailcow**
e scegliere un'istanza. L'Account mantiene il provider IMAP ma viene adottato nella gestione Mailcow: Mail Archiver crea una password app dedicata, la verifica tramite IMAP e
sostituisce la password memorizzata dell'Account. L'Account apparirà quindi con un'azione Mailcow nell'elenco degli Account.

Per uscire dalla gestione Mailcow, seleziona un altro provider e salva. La password app viene
revocata e la password locale viene cancellata. Tornando a IMAP, inserisci una nuova password
IMAP; passando a Microsoft 365 o a un Account di sola importazione verranno fornite le proprie credenziali. I non-amministratori possono comunque modificare le impostazioni non relative alle credenziali di un
Account gestito da Mailcow, ma non possono collegarlo o scollegarlo.

## Prerequisiti {#prerequisites}

### La crittografia delle credenziali deve essere abilitata {#credential-encryption-must-be-enabled}

Mail Archiver memorizza la chiave API di Mailcow e ogni password app generata in modo crittografato a
riposo utilizzando la funzione di crittografia delle credenziali esistente. Imposta

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```

prima di connettere un'istanza. La creazione di un'istanza e l'attivazione delle Caselle di posta vengono entrambe
rifiutate finché la crittografia è disabilitata, in modo che le credenziali non possano mai essere scritte in chiaro
tramite questa funzione. Conserva la chiave al di fuori del database e includila nel tuo Backup sicuro — senza di essa, le password app memorizzate non possono essere decifrate e le Caselle di posta non possono essere configurate. Consulta [Setup](Setup.md#-security-settings) per i dettagli.

### Accesso API Mailcow {#mailcow-api-access}

Crea una chiave API in Mailcow sotto **Configuration → Access → API**. La chiave deve
avere l'autorizzazione per

- elencare le Caselle di posta (`get/mailbox/all`),
- elencare, creare e eliminare password app (`get/app-passwd/all`, `add/app-passwd`,
  `delete/app-passwd`).

Limita l'API all'indirizzo IP dell'host di Mail Archiver nelle impostazioni API di Mailcow.
L'endpoint app-password di Mailcow richiede che la funzione **App Passwords** sia abilitata per
l'istanza.

### Accesso alla rete {#network-access}

- **HTTPS** verso l'origine Mailcow (ad esempio `https://mail.example.com`).
- **IMAP over TLS** verso l'host IMAP, normalmente porta `993`. L'IMAP in chiaro non è supportato
  per la configurazione.

## Utilizzo passo dopo passo {#step-by-step-usage}

### Connessione di un'istanza {#connecting-an-instance}

1. Accedi a Mail Archiver come amministratore.
2. Apri **Mail Accounts** e clicca su **Mailcow instances**.
3. Clicca su **Connect Mailcow instance**.
4. Inserisci i dettagli di connessione:
   - **Account name** — un nome visualizzato per l'istanza in Mail Archiver.
   - **Mailcow URL** — solo l'origine HTTPS, ad esempio `https://mail.example.com`.
     Percorsi, query string e credenziali incorporate vengono rifiutati.
   - **API key** — la chiave creata in Mailcow.
   - **IMAP host** — l'hostname utilizzato per l'archivio, ad esempio `mail.example.com`.
     Inserisci solo l'hostname, senza schema o porta.
   - **IMAP TLS port** — predefinito su `993`.
5. Clicca su **Connect**. Mail Archiver verifica la chiave API elencando le Caselle di posta prima di
   salvare l'istanza. In caso di successo, apre la selezione delle Caselle di posta.

La chiave API viene memorizzata in modo crittografato e non viene mai visualizzata nuovamente nel modulo o in un messaggio
di errore. I reindirizzamenti HTTP sono disabilitati per le chiamate API in modo che la chiave non possa essere inoltrata a
un'altra origine.

### Attivazione delle Caselle di posta {#activating-mailboxes}

1. Nella pagina delle Caselle di posta, opzionalmente filtra l'elenco con il campo di ricerca. Il filtro
   corrisponde all'indirizzo e al nome visualizzato ed è case-insensitive.
2. Seleziona le Caselle di posta da archiviare. **Select all** e **Clear selection** influenzano solo
   le Caselle di posta attualmente visibili (filtrate), quindi il filtraggio è un modo sicuro per scegliere un
   sottoinsieme.
3. Clicca su **Activate archiving**.

Per ogni Casella di posta selezionata, Mail Archiver:

1. rivalida l'indirizzo rispetto all'attuale elenco delle Caselle di posta di Mailcow e ignora le Caselle di posta disabilitate,
2. crea un Account di archivio denominato `{instance name} – {mailbox display name}`,
3. genera una password app casuale denominata `MailArchiver-<unique id>` limitata a
   `imap_access`,
4. verifica la credenziale con un login IMAP tramite TLS,
5. e solo allora abilita la Sincronizzazione.

Se la verifica IMAP fallisce, l'Account rimane disabilitato e può essere riprovato.

### Badge di stato della Casella di posta {#mailbox-status-badges}

La pagina di selezione etichetta ogni Casella di posta in modo che sia chiaro perché una casella di controllo è disponibile o meno:

| Badge | Significato | Selezionabile |
| --- | --- | --- |
| **Available** | Elencata in Mailcow, non ancora configurata | sì |
| **Active** | Configurata e in fase di Sincronizzazione | no |
| **Pending** | Configurazione iniziata ma non terminata (riprovabile) | sì |
| **Revoked** | La password app è stata revocata; può essere riattivata | sì |
| **Existing** | Un Account IMAP indipendente con questo indirizzo esiste già | no |
| **Disabled** | La Casella di posta è disabilitata in Mailcow | no |

### Riprovare con una Casella di posta in sospeso {#retrying-a-pending-mailbox}

La credenziale generata e il nome univoco della password app vengono persistiti **prima** che
l'API di Mailcow venga chiamata. Se una richiesta fallisce in modo ambiguo — ad esempio un timeout dopo che la
password app è stata creata — la Casella di posta rimane visibile come **Pending**. Ricarica la pagina e
selezionala di nuovo: Mail Archiver cerca prima il nome della password app e crea una nuova password
app solo se non ne esiste una. Riprovare non accumula credenziali duplicate.

La configurazione e la revoca per un'istanza sono serializzate utilizzando i lock advisory di PostgreSQL,
quindi due operazioni concorrenti non possono configurare la stessa Casella di posta due volte.

### Revoca dell'accesso {#revoking-access}

Clicca su **Revoke app password** accanto a una Casella di posta. Questo:

- disabilita la futura Sincronizzazione,
- elimina la password app gestita in Mailcow,
- e cancella la password locale.

**La posta archiviata viene conservata.** Viene rimossa solo la password app gestita — altre password
app appartenenti alla Casella di posta non vengono mai toccate. Le operazioni di Sincronizzazione già in corso
possono terminare con la loro connessione esistente.

Se la richiesta remota fallisce, l'Account rimane disabilitato e la revoca può essere riprovata.

Eliminare un Account di archivio in **Mail Accounts** non rimuove silenziosamente la credenziale
remota. Il collegamento rimane elencato sotto **Remaining app passwords** nella pagina dell'istanza in modo che
possa essere revocato esplicitamente.

### Modifica di un'istanza {#editing-an-instance}

Apri **Mailcow instances** e clicca su **Edit**. Il campo della chiave API è sempre vuoto — la
chiave memorizzata non viene mai visualizzata. Lascialo vuoto per mantenere la chiave corrente, o inserisci una nuova per sostituirla. I dettagli di connessione modificati vengono verificati rispetto a Mailcow prima del salvataggio.

### Eliminazione di un'istanza {#deleting-an-instance}

Apri **Mailcow instances** e clicca su **Delete**.

L'eliminazione di un'istanza rimuove la sua chiave API crittografata e i suoi collegamenti alle Caselle di posta. Gli **Account
di archivio e la loro posta archiviata vengono conservati**, ma gli Account vengono disabilitati.

L'eliminazione viene **rifiutata finché qualsiasi password app gestita è ancora attiva** in Mailcow:
rimuovere l'istanza eliminerebbe la chiave API necessaria per revocarla. Revoca prima tutte le password
app gestite nella pagina della Casella di posta.

## Registro di controllo {#audit-logging}

Ogni operazione amministrativa viene scritta nel registro di accesso con l'utente che agisce:

- connessione di un'istanza,
- aggiornamento di un'istanza,
- eliminazione di un'istanza,
- attivazione di Caselle di posta (con il numero di configurate e il numero di fallite),
- revoca di una password app.

## Limite Caselle di posta {#mailbox-limit}

Un singolo ciclo di attivazione può configurare al massimo `Mailcow__MaxSelectedMailboxes` Caselle di posta
(predefinito `1000`). Questo protegge da una configurazione di massa accidentale e da un carico eccessivo
sull'API Mailcow e su IMAP. Per istanze più grandi, esegui l'operazione più volte.

Ogni Casella di posta richiede diversi viaggi di andata e ritorno dell'API più una verifica IMAP, e vengono
elaborate sequenzialmente. Configurare molte centinaia di Caselle di posta in una singola richiesta può superare
i timeout del reverse-proxy o del browser; dividi la selezione in lotti.

## Note sulla sicurezza {#security-notes}

- La chiave API rimane lato server. Viene inviata come un header `X-API-Key`, oscurata dai
  log e non viene mai restituita al browser.
- Le risposte della password app di Mailcow possono riportare password in chiaro nei loro campi `log`.
  Mail Archiver deliberatamente non registra i corpi delle risposte e non espone gli errori API
  al browser; i fallimenti vengono riportati come messaggi localizzati.
- Una risposta HTTP 200 non è trattata come un successo: le mutazioni richiedono l'`type: success` di Mailcow per ogni risultato.
- L'Autorizzazione IMAP non è intrinsecamente di sola lettura. Gli Account configurati non hanno
  una policy di eliminazione lato server configurata, e gli amministratori mantengono il controllo delle
  impostazioni dell'Account. Questa funzione archivia i messaggi, non la configurazione o i Backup di Mailcow.

### Hostname e reti private {#hostnames-and-private-networks}

L'URL di Mailcow e l'host IMAP sono inseriti da un amministratore e possono puntare a
spazi di indirizzamento privati. Questo è intenzionale e supportato: Mail Archiver è comunemente
distribuito accanto al server di posta, quindi `mail.example.lan`, `192.168.10.20` o `10.0.0.5`
sono valori normali. **Nessun indirizzo privato, di loopback o link-local è bloccato.**

Il confine di fiducia è il ruolo di amministratore, non l'indirizzo di rete. `Mail Accounts`
è disponibile per i self-manager, che possono già configurare un server IMAP arbitrario per
i propri Account; la sezione Mailcow è limitata agli amministratori, e un
amministratore ha già l'accesso illimitato a ogni Account di archivio nell'installazione. Limitare l'host IMAP
non chiuderebbe quindi un confine di privilegi, mentre interromperebbe la distribuzione on-premise prevista.

Due vincoli si applicano ancora:

- L'URL di Mailcow deve essere un'origine HTTPS pura. Percorsi, query string, informazioni utente e
  reindirizzamenti sono rifiutati, quindi la chiave API non può essere inoltrata a un'altra origine.
- L'host IMAP deve essere un hostname senza schema o porta, e la configurazione si connette sempre
  con TLS (`SslOnConnect`).

Se hai bisogno di limitare quali host Mail Archiver può raggiungere, imponilo a livello di rete — regole del firewall in uscita o una policy DNS per il container — piuttosto che fare affidamento sulla
validazione dell'input nell'applicazione.

## Risoluzione dei problemi {#troubleshooting}

| Sintomo | Causa e rimedio |
| --- | --- |
| *Encryption required* al connettere o attivare | `Security__CredentialEncryptionKey` non è impostato o è non valido. Configura una chiave a 32 byte codificata in base64 e riavvia. |
| *HTTPS origin required* | L'URL ha un percorso, una query string o credenziali incorporate. Inserisci l'origine pura, ad es. `https://mail.example.com`. |
| *IMAP host invalid* | L'host IMAP deve essere un hostname senza schema o porta. |
| *Instance already connected* | Esiste un'altra istanza con lo stesso URL. Modifica quell'istanza. |
| *API request failed (401/403)* | La chiave API non ha le Autorizzazioni richieste, o la restrizione IP non include l'host di Mail Archiver. |
| *API request rejected* | Mailcow ha restituito una riga `error`/`danger`/`warning`. Controlla i log di Mailcow; Mail Archiver non inoltra i corpi delle risposte. |
| *IMAP verification failed* | Host o porta IMAP errati, un certificato non attendibile, o la funzione app-password è disabilitata in Mailcow. |
| *IMAP account exists* | Un Account IMAP indipendente sta già archiviando questo indirizzo. Usa quell'Account invece di creare un secondo archivio. |
| *Revoke previous password* | L'Account di archivio per questa Casella di posta è stato eliminato. Revoca prima la password app rimanente, quindi attiva di nuovo. |
| La Casella di posta rimane **Pending** | L'API di Mailcow è andata in timeout. Ricarica la pagina e attiva di nuovo la Casella di posta; non viene creata alcuna password app duplicata. |
| L'eliminazione è rifiutata | Le password app gestite sono ancora attive. Revocale prima nella pagina della Casella di posta. |

## Endpoint API utilizzati {#api-endpoints-used}

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## Modifiche al database {#database-changes}

La migrazione aggiunge solo le tabelle `MailcowInstances` e `MailcowMailboxes` insieme
ai loro indici e chiavi esterne. Le tabelle delle Caselle di posta esistenti e i dati archiviati vengono
conservati. La rimozione di un Account di archivio imposta l'`MailAccountId` del collegamento a `NULL` in modo che la
credenziale remota possa ancora essere revocata in seguito.
