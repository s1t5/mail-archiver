# Importazione Casella di posta Tenant Microsoft 365 {#microsoft-365-tenant-mailbox-import}

[← Torna all'indice della documentazione](index.md)

## Panoramica {#overview}

Mail Archiver può creare Account mail Microsoft 365 per più di una Casella di posta nello stesso tenant partendo da un singolo modulo di creazione. Questo è utile durante l'onboarding di un cliente o di un'organizzazione in cui devono essere archiviate tutte le Caselle di posta Microsoft 365 o un sottoinsieme selezionato.

L'importazione del tenant utilizza le credenziali dell'applicazione Microsoft Graph configurate nel modulo e crea un Account mail di Mail Archiver per ogni Casella di posta importata.

## Prerequisiti {#prerequisites}

Prima di utilizzare l'importazione del tenant, completare la configurazione della registrazione dell'app Microsoft 365 descritta nella [Guida alla registrazione dell'app Azure e alla policy di Conservazione](AZURE_APP_REGISTRATION_M365.md).

È necessario ottenere questi valori da Microsoft Entra ID:

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

La registrazione dell'app deve avere le **autorizzazioni applicative** di Microsoft Graph che consentano a Mail Archiver di leggere le Caselle di posta del tenant. Oltre alle autorizzazioni richieste per l'archiviazione regolare degli Account M365, per l'importazione del tenant è richiesta la seguente autorizzazione:

- **User.Read.All** – richiesta per elencare gli utenti del tenant e i loro indirizzi di Casella di posta.

L'importazione del tenant viene selezionata come provider **Microsoft 365 (tenant)** nel modulo di creazione.

### Filtraggio {#filtering}

L'importazione del tenant e la Gestione del Tenant escludono automaticamente:
- **Guest accounts** (`userType == "Guest"`) — utenti esterni invitati senza le proprie Caselle di posta nel tenant.
- **Utenti senza una licenza Exchange Online** — vengono elencati solo gli utenti con un piano di servizio Exchange attivo nel loro `assignedPlans`. Ciò evita l'importazione di Account che non possono essere archiviati.

## Modalità di importazione {#import-modes}

L'importazione del tenant supporta due modalità:

### Importa tutte le Caselle di posta elencate {#import-all-listed-mailboxes}

Utilizzare questa modalità quando ogni Casella di posta restituita da Microsoft Graph deve essere creata come un Account Mail Archiver.

- Mail Archiver carica l'elenco delle Caselle di posta del tenant da Microsoft Graph.
- Gli Account mail M365 già esistenti vengono saltati.
- Se **Skip disabled mailboxes** è abilitato, gli utenti disabilitati non vengono inclusi nell'elenco delle Caselle di posta.

### Importa solo le Caselle di posta selezionate {#import-selected-mailboxes-only}

Utilizzare questa modalità quando devono essere archiviate solo specifiche Caselle di posta nel tenant.

- Caricare prima l'elenco delle Caselle di posta del tenant.
- Disabilitare **Import all listed mailboxes**.
- Selezionare le Caselle di posta che devono essere importate.
- Inviare il modulo per creare Account solo per le Caselle di posta selezionate.

## Utilizzo passo dopo passo {#step-by-step-usage}

1. Accedere a Mail Archiver come utente con i permessi per creare Account mail.
2. Aprire **Mail Accounts** > **Create**.
3. Selezionare **Microsoft 365 (tenant)** come provider.
4. Inserire le credenziali Microsoft 365:
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. Inserire il prefisso **Account name** che dovrà essere utilizzato per gli Account generati.
6. Scegliere se le Caselle di posta disabilitate debbano essere saltate.
7. Fare clic su **Load mailboxes** per elencare le Caselle di posta del tenant.
8. Scegliere il comportamento di importazione desiderato:
   - Mantenere **Import all listed mailboxes** abilitato per importare tutte le Caselle di posta elencate.
   - Disabilitarlo e selezionare le singole Caselle di posta per importare solo quelle specifiche.
9. Fare clic su **Save**.

Dopo la creazione, Mail Archiver reindirizza all'elenco degli Account mail e mostra quanti Account mail del tenant sono stati importati e quanti Account esistenti sono stati saltati.

## Naming degli Account {#account-naming}

Per le importazioni del tenant, Mail Archiver utilizza il valore inserito in **Account name** come prefisso per ogni Account Casella di posta creato.

Esempio:

- Account name inserito: `Test GmbH`
- Tenant mailbox: `mailbox@example.com`
- Account name creato: `Test GmbH - <mailbox@example.com>`

Ciò facilita l'identificazione degli Account importati che appartengono allo stesso tenant o cliente.

## Account esistenti {#existing-accounts}

Se una Casella di posta esiste già come Account Microsoft 365 in Mail Archiver, l'importazione del tenant la salta invece di creare un duplicato. Il rilevamento degli Account esistenti si basa sull'indirizzo email della Casella di posta e sul provider M365.

L'elenco delle Caselle di posta può anche mostrare se una Casella di posta esiste già prima di inviare l'importazione.

## Caselle di posta disabilitate {#disabled-mailboxes}

La casella di controllo **Skip disabled mailboxes** controlla se gli utenti del tenant disabilitati debbano essere esclusi dall'elenco delle Caselle di posta.

- Abilitata: vengono elencati e importati solo gli utenti del tenant abilitati.
- Disabilitata: gli utenti del tenant disabilitati possono apparire nell'elenco e possono essere importati se selezionati o se vengono importate tutte le Caselle di posta elencate.

## Gestione del Tenant {#tenant-management}

Oltre all'importazione del tenant tramite modulo di creazione, Mail Archiver fornisce una pagina dedicata **Tenant Management** per gli amministratori. È raggiungibile dall'indice Mail Accounts tramite il pulsante **Tenant Management** (icona edificio) accanto a qualsiasi Account Microsoft 365.

La Gestione del Tenant è utile per aggiungere Caselle di posta che non sono state importate inizialmente — ad esempio quando vengono create nuove Caselle di posta nel tenant dopo l'onboarding iniziale, o quando gli Account sono stati saltati originariamente.

### Differenze rispetto all'importazione del tenant tramite modulo di creazione {#how-it-differs-from-the-create-form-tenant-import}

- Le credenziali Microsoft 365 (Client ID, Client Secret, Tenant ID) **non vengono inserite nuovamente**. Vengono prese dall'Account di origine su cui è stato cliccato il pulsante.
- L'elenco delle Caselle di posta del tenant viene caricato automaticamente all'apertura della pagina (nessun pulsante "Load mailboxes").
- Le Caselle di posta già importate vengono mostrate con un badge "already exists" e non possono essere selezionate nuovamente.
- Solo gli amministratori possono accedere alla Gestione del Tenant.

### Utilizzo passo dopo passo {#step-by-step-usage}

1. Accedere a Mail Archiver come amministratore.
2. Aprire **Mail Accounts**.
3. Individuare l'Account Microsoft 365 del cui tenant si desidera la gestione e fare clic sul pulsante **Tenant Management** accanto ad esso.
4. La pagina si carica ed elenca tutte le Caselle di posta restituite da Microsoft Graph per il tenant dell'Account di origine.
   - Le Caselle di posta che esistono già come Account M365 in Mail Archiver sono contrassegnate da un badge "already exists" e non possono essere selezionate.
   - Gli utenti del tenant disabilitati non vengono elencati; la Gestione del Tenant salta sempre le Caselle di posta disabilitate.
5. Selezionare le Caselle di posta che si desidera aggiungere.
6. Inserire il prefisso **Account name** che dovrà essere utilizzato per i nuovi Account (lo schema di denominazione è `{prefix} - <{email}>`, identico all'importazione del tenant tramite modulo di creazione).
7. Opzionalmente, configurare **Delete After Days** e **Local Retention Days** per i nuovi Account.
8. Fare clic su **Add selected mailboxes**.

Dopo la creazione, Mail Archiver reindirizza all'elenco degli Account mail e mostra quante Caselle di posta sono state aggiunte.

### Ricerca delle Caselle di posta {#searching-mailboxes}

Per i tenant di grandi dimensioni con diverse migliaia di Caselle di posta, un campo di ricerca sopra l'elenco delle Caselle di posta consente di filtrare per nome visualizzato o indirizzo email. Il filtro non fa distinzione tra maiuscole e minuscole e si applica istantaneamente durante la digitazione. **Select all** e **Clear selection** operano solo sulle Caselle di posta attualmente visibili (filtrate), quindi il filtraggio è un modo sicuro per selezionare un sottoinsieme specifico senza influenzare le voci nascoste.

### Rinominare gli Account esistenti {#renaming-existing-accounts}

La casella di controllo **Rename existing accounts** applica lo schema di denominazione `{prefix} - <{email}>` a tutti gli Account M365 che condividono la stessa registrazione dell'app (Client ID e Tenant ID) dell'Account di origine. Ciò è utile per normalizzare i nomi degli Account dopo un'importazione iniziale in cui i nomi erano inconsistenti.

Quando è selezionata, il salvataggio del modulo rinomina ogni Account corrispondente — incluso l'Account di origine — indipendentemente dal fatto che vengano selezionate nuove Caselle di posta per l'importazione. È possibile utilizzare questa opzione da sola (senza selezionare nuovi Account) per normalizzare i nomi senza aggiungere Account.

### Limite delle Caselle di posta {#mailbox-limit}

Una singola operazione di Gestione del Tenant può aggiungere al massimo `TenantManagement__MaxSelectedMailboxes` Caselle di posta alla volta (predefinito `1000`). Ciò protegge da importazioni massicce accidentali e da un carico eccessivo su Microsoft Graph / database. Se è necessario importare più Caselle di posta, eseguire l'operazione più volte. Consultare la [Guida alla configurazione](Setup.md#-tenant-management-settings) per sapere come regolare questo limite.

### Autorizzazioni richieste {#permissions-required}

La Gestione del Tenant utilizza le stesse autorizzazioni applicative di Microsoft Graph dell'importazione del tenant tramite modulo di creazione. La registrazione dell'app dell'Account di origine deve avere:

- Le autorizzazioni richieste per l'archiviazione regolare degli Account M365, **e**
- **User.Read.All** — richiesta per elencare gli utenti del tenant e i loro indirizzi di Casella di posta.

Consultare la [Guida alla registrazione dell'app Azure e alla policy di Conservazione](AZURE_APP_REGISTRATION_M365.md) per la configurazione iniziale.

### Registrazione degli Audit log {#audit-logging}

Ogni visualizzazione della pagina di Gestione del Tenant e ogni operazione di aggiunta viene registrata nel registro di controllo, incluso il numero di Caselle di posta elencate o aggiunte e l'Account di origine utilizzato.
