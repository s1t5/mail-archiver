# 🔐 Autenticazione a due fattori {#two-factor-authentication}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

L'autenticazione a due fattori (2FA) aggiunge un secondo passaggio al processo di login: dopo la password, Mail Archiver richiede un **codice di 6 cifre** generato da un'app di autenticazione sul tuo telefono. Una password rubata da sola non è più sufficiente per accedere all'Archivio.

Mail Archiver implementa la 2FA utilizzando **TOTP** (Time-based One-Time Password, RFC 6238) — lo stesso standard utilizzato da Google Authenticator, Microsoft Authenticator, Authy, 1Password, Bitwarden e altri. Nessun servizio esterno è coinvolto; il segreto non lascia mai la tua istanza.

**La 2FA è opzionale per ogni utente.** Viene configurata da ogni utente per il proprio Account, non globalmente da un amministratore.

## 🛠️ Prerequisiti {#prerequisites}

- Un'app di autenticazione compatibile con TOTP sul tuo telefono o desktop
- Accesso al tuo Account (devi aver effettuato l'accesso)
- **Un Account (password) locale** — vedi la nota su OIDC sotto

> **⚠️ Non disponibile per gli utenti OIDC.** Se effettui l'accesso tramite un provider di identità esterno (Entra ID, Authelia, Keycloak, …), la 2FA è gestita da quel provider e la pagina di configurazione è bloccata con il messaggio *"OIDC users cannot enable 2FA. Authentication is managed by your OIDC provider."* Abilita l'autenticazione a più fattori nel tuo provider di identità — vedi [OIDC / SSO](OIDC_Implementation.md).

## 🚀 Abilitare l'autenticazione a due fattori {#enabling-two-factor-authentication}

### Passaggio 1 — Apri la pagina di configurazione {#step-1-open-the-setup-page}

1. Effettua il login in Mail Archiver
2. Apri il menu del tuo Account e scegli **Two-Factor Authentication**
3. Verrai indirizzato alla pagina **Two-Factor Setup**

### Passaggio 2 — Scansiona il codice QR {#step-2-scan-the-qr-code}

La pagina mostra un codice QR. Apri la tua app di autenticazione, scegli *Aggiungi account* → *Scansiona codice QR* e punta la fotocamera verso di esso.

**Non riesci a scansionare il codice?** Usa l'**Inserimento manuale**: la pagina mostra il segreto come testo (`If you can't scan the QR code, enter this secret manually in your authenticator app`). Digitalo nella tua app come account basato sul tempo/TOTP.

> **ℹ️ Il segreto non è ancora salvato.** Viene mantenuto solo per la durata della configurazione nella sessione del browser. La 2FA diventa attiva solo dopo il successo del passaggio 3 — se abbandoni la configurazione, nulla cambia sul tuo Account.

### Passaggio 3 — Conferma con un codice {#step-3-confirm-with-a-code}

Inserisci il codice di 6 cifre che la tua app mostra ora e clicca su **Enable Two-Factor Authentication**.

Se il codice viene rifiutato, la pagina di configurazione viene mostrata nuovamente con un nuovo codice QR e il messaggio *"Invalid authentication code. Please try again."*. Verifica che l'orologio del tuo telefono sia impostato su automatico — il TOTP dipende da un orario accurato.

### Passaggio 4 — Salva i tuoi codici di backup {#step-4-save-your-backup-codes}

In caso di successo, verrai indirizzato alla pagina **Backup Codes**. Mostra **10 codici**, ciascuno lungo 16 caratteri.

> **⚠️ Questa è l'unica volta in cui i codici vengono mostrati.** Sono memorizzati tramite hash — né tu né un amministratore potrete visualizzarli nuovamente in seguito. Usa **Download Backup Codes** per salvarli in un file, o copiali nel tuo password manager prima di lasciare la pagina.

Il download contiene questo promemoria:

> *Mail Archiver - Two-Factor Authentication Backup Codes. Store these backup codes in a safe place. You can use them to log in if you lose access to your authenticator app. Each backup code can only be used once. After using a code, it will be invalidated.*
> *Importante: Mantieni questi codici al sicuro e non condividerli con nessuno.*

## 🔑 Effettuare il login con la 2FA {#logging-in-with-2fa}

Dopo aver inserito la password, verrai indirizzato alla **Two-Factor Verification** e ti verrà chiesto un codice:

> *Enter a 6-digit TOTP code from your authenticator app or a 16-character backup code*

Entrambi funzionano nello stesso campo — Mail Archiver li distingue in base alla lunghezza:

| Input | Lunghezza | Comportamento |
|---|---|---|
| Codice dalla tua app di autenticazione | 6 cifre | Verificato rispetto alla finestra temporale corrente |
| Codice di backup | 16 caratteri | Verificato rispetto ai tuoi codici memorizzati, quindi **invalidato** |

Per accettare piccole differenze di orario, vengono accettati anche i codici del passaggio temporale immediatamente precedente e successivo.

**"Remember me"** viene trasferito dal modulo di login: se lo hai selezionato, rimarrai connesso anche dopo il riavvio del browser dopo aver completato il passaggio 2FA. La selezione è mantenuta nella sessione lato server mentre il passaggio 2FA è in sospeso, quindi sopravvive solo se inserisci il codice entro il timeout di inattività di **60 minuti** della sessione. Dettagli e limiti (durata assoluta di 7 giorni, nessun effetto per gli utenti OIDC) si trovano in [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).

### Utilizzare un codice di backup {#using-a-backup-code}

1. Inserisci uno dei tuoi codici salvati di 16 caratteri nello stesso campo
2. Il codice viene accettato e **immediatamente invalidato** — non può essere riutilizzato
3. I login riusciti con un codice di backup vengono registrati nel registro di accesso con la nota `(2FA Backup Code)`, in modo da poter individuare utilizzi insoliti in seguito

### Rate limiting {#rate-limiting}

Per rallentare i tentativi di brute-force, la verifica 2FA è limitata a **5 tentativi ogni 15 minuti**, contati per indirizzo IP e nome utente. Dopo di che vedrai:

> **Too Many Attempts** — *You have made too many 2FA verification attempts. Please wait and try again later.*

Attendi la fine dell'intervallo e riprova. Il limite si applica sia ai tentativi falliti che a quelli riusciti all'interno dell'intervallo.

## 🚫 Disabilitare l'autenticazione a due fattori {#disabling-two-factor-authentication}

1. Apri **Two-Factor Authentication** nel menu del tuo Account
2. La pagina chiede conferma: *"Are you sure you want to disable two-factor authentication? This will reduce the security of your account."*
3. Inserisci la tua **password** — questo passaggio richiede la tua password attuale, non un codice 2FA
4. Clicca su **Disable Two-Factor Authentication**

La disabilitazione rimuove il segreto e invalida tutti i codici di backup rimanenti. Il tuo prossimo login richiederà solo la password.

## 🔄 Hai perso l'autenticatore? Due modi per rientrare {#lost-your-authenticator-two-ways-back-in}

### Se hai ancora i codici di backup {#if-you-still-have-backup-codes}

Effettua il login con un codice di backup come descritto sopra, quindi disabilita e riabilita la 2FA con il tuo nuovo dispositivo. Ricordati di salvare i nuovi codici di backup.

### Se non hai più codici di backup {#if-you-have-no-backup-codes-left}

Un **amministratore** deve resettare la 2FA per il tuo Account:

1. L'amministratore apre **Users**
2. Modifica l'utente interessato
3. Sceglie **Reset Two-Factor Authentication** e conferma:
   *"Are you sure you want to reset two-factor authentication for this user? They will need to set it up again."*
4. L'utente può quindi effettuare il login solo con la password e configurare nuovamente la 2FA

Il reset cancella il segreto e i codici di backup. Il messaggio di conferma
*"Two-factor authentication has been successfully reset for user …"* appare nella lista Users.

> **ℹ️ Ambito del reset.** Il reset riguarda solo la 2FA. **Non** è un reset della password — vedi [Emergency Account Recovery](EmergencyAccountRecovery.md) se il problema è la password o se l'Account è completamente bloccato.

## 👁️ Cosa possono vedere gli amministratori {#what-administrators-can-see}

La panoramica **Users** mostra una colonna 2FA:

- *"Two-factor authentication is currently enabled for this user."* — La 2FA è attiva
- Altrimenti la 2FA è disattivata per quell'utente

Gli amministratori possono resettare la 2FA (sopra) ma **non possono** leggere il segreto o i codici di backup — entrambi sono memorizzati tramite hash. Un utente che perde il proprio dispositivo senza codici di backup non ha altro modo di rientrare se non tramite il reset dell'admin.

## 🔍 Registro di controllo {#audit-trail}

L'attività 2FA viene scritta nel log e, per i login riusciti, nel registro di accesso:

| Evento | Dove | Voce |
|---|---|---|
| Login tramite app di autenticazione | Access log | `Login` con nota `IP: <address> (2FA)` |
| Login tramite codice di backup | Access log | `Login` con nota `IP: <address> (2FA Backup Code)` |
| Tentativo 2FA fallito | Application log | Warning con username, IP e token mascherato |
| Codice di backup non valido | Application log | Warning con username e IP |

La voce nel registro di accesso rende visibile l'uso del codice di backup a posteriori — utile per rilevare un codice utilizzato da qualcuno che non dovrebbe averlo. Vedi [Access Logging](Logs.md) per sapere come leggere queste voci.

## ⚙️ Come funziona internamente {#how-it-works-under-the-hood}

Per amministratori e revisori:

- **Standard**: TOTP (RFC 6238), segreto casuale a 20 byte, codificato in base32
- **Finestra di verifica**: ±1 passaggio temporale, per tollerare la deriva dell'orologio
- **Codici di backup**: 10 codici, 16 caratteri ciascuno, generati da un RNG crittografico, memorizzati tramite **hash**, invalidati individualmente all'uso
- **Memorizzazione del segreto**: nel record dell'utente; non viene mai restituito al browser dopo la configurazione
- **Gestione della sessione**: la sessione viene rigenerata prima che l'identità 2FA venga memorizzata, quindi un cookie di sessione piantato prima del login non può essere portato nella sessione autenticata
- **Account OIDC**: la gestione della 2FA è bloccata; il provider è l'autorità
- **API keys**: bypassano la 2FA per progettazione, poiché i token non interattivi non possono rispondere a una sfida — vedi la logica di sicurezza nella [REST API guide](API.md)

## ❓ Risoluzione dei problemi {#troubleshooting}

| Sintomo | Causa | Soluzione |
|---|---|---|
| *"Invalid authentication code"* | Orologio del telefono errato, o account sbagliato nell'app | Abilita la Sincronizzazione automatica dell'orario; verifica che l'app mostri la voce Mail Archiver |
| *"Two-factor secret is missing. Please restart the setup process."* | La pagina di configurazione è rimasta aperta troppo a lungo, sessione scaduta | Riavvia la configurazione dal menu dell'Account |
| *"Your session has expired. Please log in again."* | È trascorso troppo tempo nella pagina di verifica | Effettua nuovamente il login |
| *"Two-factor authentication is not enabled for this account."* | La 2FA è stata resettata o non è mai stata completata | Configurala di nuovo, o chiedi a un amministratore |
| Codice di backup rifiutato | Codice già utilizzato, o carattere letto male | I codici di backup funzionano **una volta**; usane un altro, o richiedi il reset della 2FA |
| *"Too Many Attempts"* | Più di 5 tentativi di verifica in 15 minuti | Attendi la fine dell'intervallo |
| Nessuna pagina di configurazione nel menu dell'Account | Effettui l'accesso tramite OIDC | Abilita la MFA presso il tuo provider di identità |
| Nessuna colonna 2FA nella lista Users | Non sei un amministratore | Chiedi a un amministratore |

## 📚 Documentazione correlata {#related-documentation}

- [User Management and Mailbox Permissions](UserManagement.md) — creazione Account, diritti admin
- [OIDC / SSO Authentication](OIDC_Implementation.md) — login tramite provider esterni
- [Emergency Account Recovery](EmergencyAccountRecovery.md) — Account bloccati
- [Access Logging](Logs.md) — lettura del Registro di controllo
- [REST API](API.md) — perché le API keys bypassano la 2FA
