# ☁️ Guida alla Registrazione dell'App Azure e alla Policy di Conservazione per Mail Archiver {#azure-app-registration-and-retention-policy-guide-for-mail-archiver}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida fornisce istruzioni complete per configurare la Registrazione dell'App Azure per il recupero delle email e per configurare le policy di conservazione nell'applicazione Mail Archiver.

## 📚 Indice {#table-of-contents}

1. [Panoramica](#overview)
2. [Registrazione dell'App Azure per il Recupero delle Email](#azure-app-registration-for-email-retrieval)
   - [Prerequisiti](#prerequisites)
   - [Creare la Registrazione dell'App](#create-app-registration)
   - [Impostare le Autorizzazioni API Richieste](#set-required-api-permissions)
   - [Generare il Client Secret](#generate-client-secret)
3. [Configurare l'Account M365 di Mail Archiver](#configure-mail-archiver-m365-account)
4. [Configurazione della Policy di Conservazione](#retention-policy-setup)
   - [Configurare la Policy di Conservazione](#configure-retention-policy)

## 🌐 Panoramica {#overview}

L'applicazione Mail Archiver supporta gli account Microsoft 365 (M365) utilizzando il flusso OAuth2 client credentials per il recupero delle email. Inoltre, fornisce la funzionalità di policy di conservazione per eliminare automaticamente le email dal server di posta dopo un periodo specificato, aiutando a gestire lo spazio di archiviazione mantenendo al contempo un Archivio completo.

## ☁️ Registrazione dell'App Azure per il Recupero delle Email {#azure-app-registration-for-email-retrieval}

### 🛠️ Prerequisiti {#prerequisites}

- Accesso amministrativo a Microsoft Entra ID (Azure AD)
- Un tenant Microsoft 365 con licenze Exchange Online

### 🚀 Creare la Registrazione dell'App {#create-app-registration}

1. Navigare in [Microsoft Entra Admin Center](https://entra.microsoft.com)
2. Accedere con il proprio account amministratore
3. Nel pannello di navigazione a sinistra, selezionare **App registrations**
4. Fare clic su **+ New registration** nella parte superiore della pagina App registrations
5. Inserire i seguenti dettagli:
   - **Name**: Inserire un nome descrittivo (ad es., "Mail Archiver M365 Provider")
   - **Supported account types**: Selezionare **Accounts in this organizational directory only** (Single tenant)
     - Questo è richiesto per il flusso client credentials utilizzato da Mail Archiver
   - **Redirect URI**: Lasciare vuoto (non necessario per il flusso client credentials)
6. Fare clic su **Register**

**Importante**: Annotare i seguenti valori dalla pagina **Overview**:
- **Application (client) ID - Avrete bisogno di questo come `ClientId` in Mail Archiver
- **Directory (tenant) ID - Avrete bisogno di questo come `TenantId` in Mail Archiver

### 🔐 Impostare le Autorizzazioni API Richieste {#set-required-api-permissions}

1. Navigare in **API permissions** nel menu a sinistra
2. Fare clic su **+ Add a permission**
3. Selezionare **Microsoft Graph**
4. Scegliere **Application permissions** (richieste per il flusso client credentials di Mail Archiver)
5. Aggiungere le seguenti autorizzazioni:
   - **Mail.Read** - Legge le email in tutte le Caselle di posta
   - **Mail.ReadWrite** - Legge e scrive email in tutte le Caselle di posta (sia per la funzione di Ripristino che per l'eliminazione)
   - **User.Read.All** - Legge i profili completi di tutti gli utenti (richiesto per l'importazione della Casella di posta del tenant Microsoft 365; fornisce anche `assignedPlans` per il filtraggio della licenza Exchange)

6. Fare clic su **Add permissions**
7. **CRITICO**: Fare clic su **Grant admin consent for [Your Organization]**
8. Confermare facendo clic su **Yes**

**Nota**: Le autorizzazioni applicative sono necessarie perché Mail Archiver utilizza il flusso client credentials per accedere alle Caselle di posta senza interazione dell'utente.

### 🔑 Generare il Client Secret {#generate-client-secret}

1. Navigare in **Certificates & secrets** nel menu a sinistra
2. Sotto **Client secrets**, fare clic su **+ New client secret**
3. Fornire una descrizione (ad es., "Mail Archiver Secret")
4. Selezionare un periodo di scadenza
5. Fare clic su **Add**
6. **Importante**: Copiare immediatamente il **Value** e conservarlo in modo sicuro. Questo secret non verrà mostrato nuovamente.

## 📧 Configurare l'Account M365 di Mail Archiver {#configure-mail-archiver-m365-account}

Dopo aver completato la registrazione dell'app, è necessario configurare un account email M365 in Mail Archiver con i seguenti valori:

### 📋 Valori Richiesti dalla Registrazione dell'App: {#required-values-from-app-registration}

1. **Client ID**: Copiare l'**Application (client) ID** dalla pagina **Overview** della registrazione dell'app
2. **Client Secret**: Copiare il **Value** salvato durante la creazione del client secret
3. **Tenant ID**: Copiare il **Directory (tenant) ID** dalla pagina **Overview** della registrazione dell'app

### 🚀 Creazione dell'Account M365 in Mail Archiver: {#creating-m365-account-in-mail-archiver}

1. Accedere all'applicazione Mail Archiver
2. Navigare in **Mail Accounts** > **Create**
3. Compilare i seguenti campi:
   - **Name**: Nome descrittivo per l'account (ad es., "Sales Team M365")
   - **Email Address**: L'indirizzo email della Casella di posta di destinazione da archiviare
   - **Provider**: Selezionare **M365**
   - **Client ID**: Inserire l'Application (client) ID della registrazione dell'app
   - **Client Secret**: Inserire il valore del client secret salvato
   - **Tenant ID**: Inserire il Directory (tenant) ID della registrazione dell'app

4. Fare clic su **Create**

### ⚠️ Note Importanti: {#important-notes}

- Il campo **Email Address** deve contenere l'effettiva Casella di posta che si desidera archiviare quando si crea un singolo account M365.
- La registrazione dell'app deve avere le autorizzazioni per accedere alla Casella di posta specificata.
- Per creare account per tutte o alcune Caselle di posta nel tenant contemporaneamente, utilizzare la guida [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md).


## 🗑️ Configurazione della Policy di Conservazione {#retention-policy-setup}

L'applicazione Mail Archiver fornisce la funzionalità di policy di conservazione per eliminare automaticamente le email dal server IMAP dopo che sono state archiviate con successo, aiutando a gestire lo spazio di archiviazione sul server IMAP mantenendo al contempo un Archivio completo.

### 🛠️ Configurare la Policy di Conservazione {#configure-retention-policy}

1. Navigare in **Mail Accounts** nell'applicazione Mail Archiver
2. Creare un nuovo account M365 o modificare uno esistente
3. Nel modulo di configurazione dell'account, individuare il campo **Delete After Days**
4. Inserire il numero di giorni dopo i quali le email archiviate devono essere eliminate dal server:
   - Ad esempio, inserire `30` per eliminare le email dopo 30 giorni
   - Lasciare vuoto per disabilitare l'eliminazione automatica
5. Salvare la configurazione dell'account


## ℹ️ Informazioni Relative alle Caselle di Posta Migrate {#ℹ-information-regarding-migrated-mailboxes}

### 📁 Cartelle Non Visibili Dopo la Migrazione {#folders-not-visible-after-migration}

Se si è migrata la propria Casella di posta da un altro provider di posta elettronica a Microsoft 365, alcune cartelle potrebbero non apparire nell'elenco delle Cartelle.

#### 🔍 Background {#background}

Mail Archiver recupera le informazioni sulle cartelle dalle Caselle di posta M365 tramite la Microsoft Graph API. L'applicazione richiede correttamente tutte le cartelle disponibili (incluse le cartelle nascoste) e attraversa l'intera gerarchia di Cartelle.

Tuttavia, la Microsoft Graph API restituisce solo le cartelle con `PR_CONTAINER_CLASS` impostato su `IPF.Note`. Durante le migrazioni di Caselle di posta da altri sistemi di posta, le cartelle possono essere assegnate a una classe di contenitore diversa come `IPF.Imap`. Queste cartelle non sono esposte attraverso gli endpoint di elenco delle cartelle della Graph API.

Questo comportamento riguarda tutte le applicazioni che utilizzano questa API per il recupero delle cartelle.

#### 🔧 Opzioni di Soluzione Alternativa {#workaround-options}

**Opzione 1: Utilizzare lo strumento MFCMAPI (Raccomandato)**

1. Scaricare [MFCMAPI](https://github.com/Microsoft/mfcmapi/) dal repository ufficiale di Microsoft
2. Avviare MFCMAPI e connettersi alla propria Casella di posta
3. Navigare in ogni cartella mancante
4. Cambiare la proprietà `PR_CONTAINER_CLASS` da `IPF.Imap` a `IPF.Note`
5. Riavviare Outlook e ricaricare le cartelle in Mail Archiver

**Opzione 2: Ricreare le Cartelle in Outlook**

1. Creare nuove Cartelle direttamente in Outlook (queste avranno la classe `IPF.Note` corretta)
2. Spostare tutte le email dalle cartelle migrate alle nuove cartelle
3. Eliminare le vecchie cartelle migrate, se desiderato

**Opzione 3: Script PowerShell (Per Modifiche di Massa)**

Per le Caselle di posta con molte cartelle migrate, è possibile utilizzare PowerShell con il modulo Exchange Online per cambiare la classe del contenitore per più cartelle contemporaneamente. Contattare l'amministratore di Exchange per assistenza con questo approccio.


---

**Nota**: Questa guida è aggiornata al 2025. Microsoft aggiorna regolarmente i propri servizi, quindi alcuni elementi dell'interfaccia utente potrebbero differire. Fare sempre riferimento alla documentazione Microsoft più recente per le informazioni più aggiornate.
