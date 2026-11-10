# ☁️ Configurazione Account Microsoft Personale (Outlook.com / M365 Family) {#personal-microsoft-account-outlookcom-m365-family-setup}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida spiega come archiviare le email da un **account Microsoft personale** (Outlook.com, Hotmail, live.com o Microsoft 365 Family) in Mail Archiver.

A differenza degli account Microsoft 365 aziendali (che utilizzano il flusso client-credentials — vedi [Azure App Registration for M365](AZURE_APP_REGISTRATION_M365.md)), gli account personali utilizzano **OAuth2 con il Device Code Flow (RFC 8628)**. Microsoft ha rimosso l'autenticazione Basic e le password delle app per gli account personali, pertanto è richiesto l'uso di OAuth2.

Mail Archiver viene fornito con un **Client ID condiviso pre-registrato**, in modo che la maggior parte degli utenti **non** debba creare la propria Azure App Registration. Gli utenti esperti possono comunque registrare la propria app (vedi l'Opzione B di seguito).

## 📚 Sommario {#table-of-contents}

1. [Opzione A: Configurazione Standard (Consigliata)](#option-a-standard-setup-recommended)
2. [Opzione B: Registrazione App Personalizzata (Utenti Esperti)](#option-b-custom-app-registration-power-users)
3. [Aggiornamento e Revoca del Token](#token-refresh--revocation)
4. [Note Importanti](#important-notes)
5. [Risoluzione dei Problemi](#troubleshooting)

---

## 🚀 Opzione A: Configurazione Standard (Consigliata) {#option-a-standard-setup-recommended}

Utilizza questa opzione se la tua istanza di Mail Archiver ha un Client ID predefinito configurato (il predefinito per le immagini Docker ufficiali). Non è richiesto l'accesso al portale Azure.

### 🛠️ Prerequisiti {#prerequisites}

- Un account Microsoft personale (Outlook.com, live.com o M365 Family)
- Un dispositivo con un browser (smartphone, laptop o tablet) per completare l'autorizzazione

### 📝 Passaggi {#steps}

1. Accedi alla tua applicazione Mail Archiver.
2. Vai su **Mail Accounts** > **Create**.
3. Compila i seguenti campi:
   - **Name**: Nome descrittivo per l'account (es. "Personal Outlook")
   - **Email Address**: Il tuo indirizzo email Microsoft personale (es. `you@outlook.com`)
   - **Provider**: Seleziona **Microsoft Personal**
4. Non verranno mostrati i campi Client ID o Client Secret — Mail Archiver utilizza automaticamente la registrazione predefinita integrata.
5. Clicca su **Create**.
6. La pagina di autorizzazione tramite device-code si apre automaticamente. Viene visualizzato un codice breve (es. `ABCD-EFGH`) insieme a un link per `https://microsoft.com/devicelogin` (o l'URL di verifica mostrato).
7. Su **qualsiasi dispositivo** dotato di browser, apri quell'URL e accedi con il tuo account Microsoft personale.
8. Inserisci il codice ottenuto al punto 6.
9. Appare una schermata di consenso che mostra **"Mail-Archiver"** che richiede l'accesso IMAP. Esamina e approva.
10. La pagina di Mail Archiver rileva automaticamente il successo e reindirizza alla visualizzazione di modifica dell'account.
11. Fatto — la sincronizzazione inizia automaticamente in base alle impostazioni dell'account.

> ⚠️ **Avviso Publisher non verificato**: La schermata di consenso potrebbe mostrare un avviso secondo cui "Mail-Archiver" è un publisher non verificato. È normale — la verifica del publisher richiede un'identità aziendale (account Microsoft Partner Network) e non è necessaria affinché il flusso funzioni. L'app funziona normalmente anche senza di essa.

---

## 🔧 Opzione B: Registrazione App Personalizzata (Utenti Esperti) {#option-b-custom-app-registration-power-users}

Utilizza questa opzione se non desideri utilizzare il Client ID predefinito condiviso (ad esempio, per motivi di conformità o se desideri il pieno controllo sulla registrazione dell'app).

### 🛠️ Prerequisiti {#prerequisites}

- Un account Microsoft (personale o di lavoro/scuola) che possa accedere al portale Azure
- Conoscenza di base del portale Azure/Entra

### 🚀 Crea la Registrazione dell'App {#create-app-registration}

1. Vai al [Microsoft Entra Admin Center](https://entra.microsoft.com) o al [Azure Portal](https://portal.azure.com).
2. Accedi con il tuo account Microsoft.
3. Nel pannello di navigazione a sinistra, seleziona **App registrations** (sotto Identity > Applications).
4. Clicca su **+ New registration**.
5. Compila i seguenti dettagli:
   - **Name**: Inserisci un nome descrittivo (es. "Mail Archiver MSA")
   - **Supported account types**: Seleziona **Accounts in any organizational directory and personal Microsoft accounts**
     - Questa è l'opzione multi-tenant e supporta sia account personali che aziendali.
   - **Redirect URI**: Lascia questo campo vuoto (non necessario per il Device Code Flow).
6. Clicca su **Register**.
7. Prendi nota dell'**Application (client) ID** dalla pagina **Overview**.

### 🔐 Abilita i Public Client Flows {#enable-public-client-flows}

1. Nella registrazione della tua app, vai su **Authentication** nel menu a sinistra.
2. In fondo, trova **Allow public client flows** e impostalo su **Yes**.
3. Clicca su **Save**.

> ℹ️ Non è necessario configurare manualmente alcun client secret, redirect URI o autorizzazioni API — il Device Code Flow utilizza autorizzazioni delegate che vengono richieste a runtime tramite lo scope `IMAP.AccessAsUser.All`.

### 📧 Configura Mail Archiver {#configure-mail-archiver}

Sovrascrivi il valore predefinito globale (influisce su tutti gli account MSA)

Imposta il Client ID nella tua variabile d'ambiente:


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 Aggiornamento e Revoca del Token {#token-refresh-revocation}

### Aggiornamento Automatico del Token {#automatic-token-refresh}

Dopo l'autorizzazione iniziale, Mail Archiver riceve:
- Un **access token** (a breve durata, ~1 ora)
- Un **refresh token** (a lunga durata, utilizzato per ottenere automaticamente nuovi access token)

Mail Archiver aggiorna automaticamente l'access token prima di ogni esecuzione di sincronizzazione. Non è necessario ri-autorizzare a meno che il refresh token non scada o venga revocato.

### Revoca dell'Accesso {#revoking-access}

Per revocare l'accesso di Mail Archiver al tuo account Microsoft personale:

1. Visita [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage)
2. Accedi con il tuo account Microsoft personale.
3. Trova **"Mail-Archiver"** (o il nome della tua app personalizzata) nell'elenco.
4. Clicca su **Remove** per revocare l'accesso.

Dopo la revoca, Mail Archiver non potrà più accedere all'account finché non lo ri-autorizzerai.

---

## ⚠️ Note Importanti {#important-notes}
- **Client ID Condiviso**: Quando si utilizza il Client ID predefinito, tutti gli utenti di Mail Archiver su tutte le istanze condividono la stessa Azure App Registration. Il manutentore di Mail Archiver è responsabile del mantenimento attiva la registrazione. Se Microsoft disabilita la registrazione condivisa, tutti gli utenti dovranno passare a una registrazione personalizzata (Opzione B) finché un nuovo rilascio non fornirà un Client ID aggiornato.


- **Account Aziendali**: Il provider Microsoft Personal utilizza l'endpoint di autorità `/common` e supporta sia account personali che aziendali (lavoro/scuola). Per gli account aziendali che richiedono l'accesso tramite client-credentials (solo app, senza accesso dell'utente), utilizza invece il provider **M365**.

---

## 🔍 Risoluzione dei Problemi {#troubleshooting}

### Cartelle visibili su outlook.com ma mancanti in Mail Archiver {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**Sintomo**: Alcune cartelle esistono nella Casella di posta (visibili su outlook.com o in Outlook), ma non compaiono mai in Mail Archiver — né nell'elenco delle cartelle nella pagina delle impostazioni dell'account, né durante la sincronizzazione. Non viene registrato alcun errore.

**Causa (lato server)**: Le cartelle create da **client di terze parti tramite EWS** — che configura gli account outlook.com come account Exchange anziché IMAP — possono finire con una proprietà `PR_CONTAINER_CLASS` errata (es. `IPF.Imap` invece di `IPF.Note`). Outlook.com quindi **filtra completamente queste cartelle dalla risposta IMAP `LIST`**, anche se sono pienamente visibili nell'interfaccia web. Questo è un comportamento lato Microsoft, non un bug di Mail Archiver — lo stesso fenomeno è documentato per le Microsoft Graph API, dove le cartelle con una classe di contenitore non `IPF.Note` mancano dagli elenchi `mailFolders` finché la proprietà non viene corretta:

Mail Archiver interroga l'elenco delle cartelle da tre fonti (`LIST` ricorsivo, attraversamento per livello di ogni sottocartella e `LSUB`) e unisce i risultati. Quando il server non riporta una cartella attraverso nessuno di essi, non c'è modo per alcun client IMAP di vederla.

**Soluzione alternativa**: Tocca le cartelle interessate una volta tramite l'interfaccia web di outlook.com in modo che Microsoft corregga la classe del contenitore:

1. Apri <https://outlook.com> e individua la cartella mancante.
2. Rinominala (qualsiasi nome temporaneo) e rinominala di nuovo — oppure crea una nuova cartella e sposta le email.
3. Ripeti per ogni cartella interessata.
4. Avvia una **Full Resync** dell'account dalla pagina Account Details in Mail Archiver. È necessaria una sincronizzazione completa perché le sincronizzazioni incrementali recuperano solo le email più recenti rispetto all'ultima data di sincronizzazione dell'account — le email in cartelle che non sono mai state scoperte rimarrebbero altrimenti non archiviate.


*Questa guida è aggiornata al 2026. Microsoft aggiorna regolarmente i propri servizi e l'interfaccia utente, quindi alcuni passaggi potrebbero differire. Fai riferimento alla [documentazione della Microsoft identity platform](https://learn.microsoft.com/en-us/entra/identity-platform/) per gli ultimi dettagli.*
