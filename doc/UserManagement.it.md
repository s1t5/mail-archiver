# 👥 Gestione Utenti e Autorizzazioni Casella di posta {#user-management-and-mailbox-permissions}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida fornisce istruzioni dettagliate per la creazione di nuovi Account utente e l'assegnazione di autorizzazioni per la Casella di posta nell'applicazione Mail Archiver. Copre inoltre la modifica della propria password e il cambio password forzato su un'istanza appena installata.


## 🛠️ Prerequisiti {#prerequisites}

- Accesso amministrativo all'applicazione Mail Archiver
- Account email esistenti configurati nel sistema

## 🧑‍💻 Creazione di un Nuovo Account Utente {#creating-a-new-user-account}

1. Accedi all'applicazione Mail Archiver con un Account che abbia privilegi amministrativi
2. Naviga nella sezione "Users" dal menu principale
3. Clicca sul pulsante "Create New User"
4. Compila le informazioni utente richieste:
   - **Username**: Inserisci un username univoco per il nuovo utente
   - **Password**: Inserisci una password sicura per l'utente
   - **Email**: Indirizzo email dell'utente
   - **Admin**: Seleziona questa casella se l'utente deve avere privilegi amministrativi
   - **Self Manager**: Seleziona questa casella se l'utente deve essere in grado di gestire i propri account email (modificare quelli esistenti e aggiungerne di nuovi, oltre a eliminare gli account)
   - **active**: Deve essere selezionata per consentire l'accesso all'utente
5. Clicca su "Create User" per salvare il nuovo Account utente

## 🔐 Assegnazione Autorizzazioni Casella di posta agli Utenti {#assigning-mailbox-permissions-to-users}

1. Dopo aver creato l'utente, o quando si modifica un utente esistente, naviga nella sezione "Users" dal menu principale
2. Trova l'utente nell'elenco in cui desideri assegnare gli account email e clicca sul pulsante "Assign" per quell'utente
3. Nella pagina "Assign Mail Accounts", vedrai tutti gli account email disponibili e le caselle di controllo che indicano se sono assegnati all'utente
5. Per assegnare un nuovo account email, seleziona la casella corrispondente all'account specifico
6. Per rimuovere un account email dall'utente, deseleziona la casella
7. Clicca su "Save Assignments" per applicare le modifiche

## 👤 Autorizzazioni Account Utente {#user-account-permissions}

### Admin Users {#admin-users}
Gli utenti Admin hanno pieno accesso all'applicazione e possono:
- Gestire tutti gli Account utente e le loro autorizzazioni
- Accedere a tutti gli account email nel sistema
- Visualizzare tutti i log di accesso
- Eseguire tutti i compiti amministrativi

### Self Manager Users {#self-manager-users}
Gli utenti Self Manager possono:
- Aggiungere nuovi account email al proprio Account
- Modificare gli account email esistenti assegnati loro
- Eliminare gli account email assegnati loro
- Accedere e gestire le email dai propri account assegnati (inclusa l'eliminazione)
- Visualizzare i propri log di accesso

### Standard Users {#standard-users}
Gli utenti Standard hanno un accesso limitato e possono:
- Visualizzare le email archiviate dagli account email assegnati
- Cercare all'interno degli account email assegnati
- Esportare email dai propri account assegnati
- Ripristino di email dai propri account assegnati
- Accedere agli Allegati email degli account assegnati

## 🔑 Cambiare la Propria Password {#changing-your-own-password}

Ogni utente con una password locale può cambiarla senza un amministratore:

1. Clicca sul tuo **username** nell'angolo in alto a destra per aprire il menu utente.
2. Scegli **Change Password**.
3. Inserisci la tua **current password** e la **new password** due volte, quindi invia.

La nuova password deve soddisfare i requisiti riportati di seguito e deve essere **diversa dalla password attuale**.

> ℹ️ **Gli utenti OIDC non possono cambiare la password qui.** La pagina risponde con:
> *"OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* Cambia la password presso il tuo provider di identità —
> vedi [OIDC / SSO](OIDC_Implementation.md).

### Requisiti della password {#password-requirements}

- almeno **10 caratteri**
- almeno una lettera **maiuscola**
- almeno una lettera **minuscola**
- almeno un **numero**
- almeno un **carattere speciale**

Il modulo mostra questi requisiti come una checklist in tempo reale mentre scrivi. Le stesse regole si applicano quando un amministratore crea un utente o resetta una password.

## ⚠️ Cambio Password Forzato (Configurazione Iniziale) {#forced-password-change-initial-setup}

Un'istanza appena installata inizia con le credenziali dell'amministratore provenienti dalla configurazione (`Authentication__Username` / `Authentication__Password`). Finché quella password predefinita è ancora in uso **e non esiste ancora alcun account email**, il primo login forza un cambio:

- Un banner di avviso spiega il motivo: *"For security reasons, you must change your
  password before continuing. This is required because you are using the default
  system credentials on a newly set up system."*
- Non è possibile navigare altrove dalla pagina: *"You must change your password before
  you can continue using the system."*
- La nuova password deve essere diversa da quella attuale.
- Dopo il salvataggio, il flag viene rimosso e verrai indirizzato alla dashboard.

Una volta cambiata la password predefinita, il cambio forzato non apparirà più.

## 🕒 Rimanere Connessi — la casella di controllo "Remember Me" {#staying-signed-in-the-remember-me-checkbox}

Il modulo di login ha una casella di controllo denominata **Remember Me** (*angemeldet bleiben* nell'interfaccia tedesca). Fa esattamente una cosa: decide se il cookie di autenticazione è un cookie di sessione o uno persistente.

| | Casella non selezionata | Casella selezionata |
|---|---|---|
| Durata del cookie | `Authentication__SessionTimeoutMinutes` (predefinito **60 minuti**) | **7 giorni** |
| Tipo di cookie | Session cookie — scartato alla chiusura del browser | Persistent cookie — memorizzato su disco |
| Riavvio browser | Devi effettuare nuovamente il login | Rimani connesso |
| Timeout inattività | Esteso ad ogni richiesta (`SlidingExpiration`) | Esteso ad ogni richiesta, fino al limite assoluto di 7 giorni |
| Logout | Termina immediatamente la sessione | Termina immediatamente la sessione |

Quindi la casella di controllo trasforma "connesso per questa sessione del browser, al massimo 60 minuti di inattività" in "connesso fino a 7 giorni, inclusi i riavvii del browser e della macchina".

### Cose che la casella di controllo non fa {#things-the-checkbox-does-not-do}

- **Non ricorda la tua password o username.** Il modulo di login non viene
  pre-compilato alla visita successiva; viene mantenuto solo il cookie di sessione.
- **I 7 giorni sono un limite rigido e non possono essere configurati.** Il valore è fisso
  nel codice (`Auth/Services/CookieAuthenticationService.cs`). Solo la durata non persistente segue `Authentication__SessionTimeoutMinutes`.
- **Non è un timer di inattività.** I 7 giorni vengono contati dal login; una volta
  terminati, è richiesto un nuovo login anche se hai usato l'Archivio un minuto fa.
- **Non sopravvive a un logout.** Cliccare su *Logout* elimina il cookie
  indipendentemente dalla casella di controllo.

### Interazione con 2FA {#interaction-with-2fa}

Con l'autenticazione a due fattori abilitata, la selezione della casella viene ricordata durante
il passaggio 2FA e applicata solo dopo il successo del secondo fattore. Il passaggio del 2FA
risiede nella sessione lato server, che ha un proprio **timeout di inattività di 60 minuti**: se il codice non viene inserito entro quella finestra, il login viene abortito e
la selezione della casella viene persa di conseguenza.

### Interazione con la sessione dell'applicazione {#interaction-with-the-application-session}

Mail Archiver mantiene una seconda sessione separata lato server per lo stato a breve termine — il passaggio 2FA, la selezione del batch-restore e l'ultimo stato di ricerca. Quella sessione ha un **timeout di inattività fisso di 60 minuti** e *non* è persistente. Dopo un riavvio del browser con un cookie "Remember Me" valido rimani connesso, ma qualsiasi operazione in sospeso contenuta in quella sessione ricomincia da capo.

### Interazione con OIDC / SSO {#interaction-with-oidc-sso}

Gli utenti che effettuano l'accesso tramite un provider di identità esterno non ottengono mai il
cookie persistente: il callback OIDC avvia sempre la sessione con
`persistAuthentication: false`. Per quegli Account la casella di controllo **non ha
effetto** — la durata di una sessione è decisa dalle impostazioni di sessione del provider di identità.

> ⚠️ **Nota di sicurezza.** Su una macchina condivisa o non presidiata, lascia la casella
> non selezionata — un cookie persistente mantiene l'Archivio accessibile per un massimo di 7 giorni
> senza password. In ambienti in cui ciò è inaccettabile, accorcia
> `Authentication__SessionTimeoutMinutes` e richiedi agli utenti di non usare la
> casella di controllo, o usa OIDC dove il provider impone la propria policy di sessione.

## 🔒 Considerazioni sulla Sicurezza {#security-considerations}

1. Usa password forti per tutti gli Account utente
2. Cambia la password predefinita dell'amministratore immediatamente dopo l'installazione
3. Assegna l'Autorizzazione Admin solo agli utenti che ne hanno bisogno
4. Assegna l'Autorizzazione Self Manager solo agli utenti che devono gestire i propri Account
5. Revisiona regolarmente le autorizzazioni degli utenti per garantire che l'accesso sia appropriato
6. Rimuovi l'accesso all'utente quando non è più necessario
7. Usa il principio del minimo privilegio - assegna l'accesso solo agli account email di cui gli utenti hanno bisogno
