# Importazione Account {#account-import}

[← Torna all'Indice della Documentazione](index.md)

## Panoramica {#overview}

Mail Archiver supporta l'importazione di più account email contemporaneamente, in modo che l'onboarding di un gran numero di Caselle di posta non richieda la creazione di ogni Account individualmente.

Esistono due percorsi di importazione massiva, a seconda del provider di posta:

| Provider | Metodo di Importazione Massiva | Documentazione |
|---|---|---|
| **Microsoft 365 (tenant)** | Scoperta del tenant tramite Microsoft Graph — una singola registrazione app accede a ogni Casella di posta nel tenant | [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) |
| **IMAP** | Importazione massiva CSV — caricamento di un file CSV con una riga per ogni Casella di posta | Questa pagina (vedi sotto) |

> **Nota:** Gli Account Microsoft personali (Outlook.com / M365 Family, provider **MSA**) utilizzano un flusso di Autorizzazione interattivo tramite device-code e non possono essere importati in massa. Devono essere creati e autorizzati uno alla volta. Vedere [MSA Outlook Setup](MSA_Outlook_Setup.md).

---

## Importazione Massiva CSV IMAP {#imap-csv-bulk-import}

### Cosa fa {#what-it-does}

L'importazione massiva CSV consente a un amministratore di creare centinaia di Account email IMAP in un'unica operazione caricando un file CSV. Ogni riga nel file corrisponde a una Casella di posta. Le impostazioni comuni del server (host IMAP, porta, SSL) possono essere specificate una sola volta nel modulo di caricamento e si applicano a ogni riga, con la possibilità di sovrascritture opzionali per singola riga.

L'importazione è riservata agli **amministratori**. È accessibile dalla pagina **Mail Accounts** tramite il menu a discesa **Import** → **CSV Bulk Import**.

### Formato File CSV {#csv-file-format}

Il file deve essere un CSV codificato in UTF-8 con una **riga di intestazione** e la **virgola (`,`) come delimitatore**. I campi contenenti virgole o virgolette devono essere racchiusi tra virgolette doppie (`"..."`).

#### Colonne {#columns}

| Colonna | Richiesto | Descrizione |
|---|---|---|
| `email` | Sì | Indirizzo della Casella di posta. Utilizzato anche come username di login predefinito quando `username` non viene fornito. |
| `password` | Sì | Password di login IMAP. Memorizzata così come fornita — gli spazi iniziali/finali vengono preservati. |
| `name` | No | Nome visualizzato per l'Account. Se omesso, il valore predefinito è `{prefix} - <{email}>`. |
| `username` | No | Username di login se diverso dall'indirizzo email. |
| `imap_server` | No | Host IMAP per riga. Sovrascrive il server comune del modulo di caricamento. |
| `imap_port` | No | Porta IMAP per riga (1–65535). Sovrascrive la porta comune. |
| `use_ssl` | No | Interruttore SSL per riga (`true` o `false`). Sovrascrive l'impostazione comune. |

#### Esempio {#example}

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- La riga 1 utilizza il server/porta/SSL comune del modulo di caricamento.
- La riga 2 imposta un username personalizzato ma utilizza altrimenti le impostazioni comuni.
- La riga 3 sovrascrive server, porta e SSL.

Un file di esempio scaricabile è disponibile nella pagina di importazione tramite il pulsante **Download example CSV**.

### Modulo di Caricamento {#upload-form}

| Campo | Scopo |
|---|---|
| **CSV file** | Il file da caricare. |
| **IMAP server** | Host comune per tutte le righe senza una sovrascrittura `imap_server`. |
| **IMAP port** | Porta comune (predefinita 993). |
| **Use SSL** | Interruttore SSL comune (predefinito su). |
| **Name prefix** | Prefisso per i nomi degli Account generati automaticamente. Predefinito: `IMAP`. |
| **Skip existing mailboxes** | Quando abilitato, le righe le cui email esistono già come Account IMAP vengono saltate (contate come "saltate", non come errori). Quando disabilitato, vengono trattate come fallimenti. |
| **Account enabled** | Se gli Account creati debbano essere abilitati per la Sincronizzazione in background. |
| **Delete After Days / Local Retention Days** | Impostazioni di Conservazione opzionali applicate a ogni Account creato. Vedere [Retention Policies](RetentionPolicies.md). |

### Elaborazione {#processing}

1. Il file CSV viene analizzato e ogni riga viene validata (formato email, password presente, intervallo porta, valore SSL).
2. Gli indirizzi email duplicati all'interno del file vengono rimossi (vince la prima occorrenza).
3. Vengono rilevati gli Account IMAP esistenti con lo stesso indirizzo email; a seconda dell'interruttore **Skip existing**, vengono saltati o segnalati come fallimenti.
4. Tutti i nuovi Account vengono inseriti in un unico batch nel database.
5. Viene scritto un'entry nel registro di controllo che riassume l'esecuzione.

Non viene eseguito alcun test di connessione IMAP durante l'importazione — gli Account vengono salvati immediatamente. I problemi di autenticazione emergeranno solo durante la prima Sincronizzazione in background. Questo mantiene l'importazione veloce anche per centinaia di Caselle di posta.

### Pagina dei Risultati {#result-page}

Al termine dell'importazione, una pagina dei risultati mostra tre tabelle:

- **Created** — email e nome di ogni Account creato.
- **Skipped** — email che erano duplicati (nel file o già esistenti) quando **Skip existing** è abilitato.
- **Failed** — righe che non hanno potuto essere elaborate, con il numero di riga e il motivo (email mancante, password mancante, porta non valida, nessun server IMAP, ecc.).

Un banner di riepilogo mostra i conteggi: *{created} creati, {skipped} saltati, {failed} falliti*.

### Limiti {#limits}

I seguenti limiti possono essere configurati tramite `appsettings.json` / variabili d'ambiente (vedere [Setup Guide](Setup.md) → *CSV Import Settings*):

| Impostazione | Predefinito | Descrizione |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | Massimo numero di righe elaborate per caricamento. |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | Dimensione massima del file caricato. |

### Nota sulla Sicurezza {#security-note}

Le password IMAP sono memorizzate nel database nello stesso modo in cui vengono memorizzate per gli Account IMAP creati individualmente. Si prega di notare che il file CSV contiene tutte le password delle Caselle di posta in chiaro — eliminarlo dalla macchina client dopo un'importazione riuscita.

---

## Importazione Tenant Microsoft 365 {#microsoft-365-tenant-import}

Per Microsoft 365 (Exchange Online) l'importazione massiva non richiede un file CSV. Una singola registrazione app Azure AD con i giusti permessi Graph può accedere a ogni Casella di posta nel tenant, consentendo a Mail Archiver di scoprire automaticamente le Caselle di posta.

Vedere la guida dedicata: **[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**.
