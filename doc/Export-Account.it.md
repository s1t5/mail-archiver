# 📤 Esportazione di un Account Mail (EML / MBox) {#exporting-a-mail-account-eml-mbox}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

L'esportazione dell'account scrive **ogni messaggio di un account** in un file ZIP scaricabile. È la controparte dell'
[web import](Import-EML-MBox.md): si ottiene un Archivio portabile dell'account in un
formato che qualsiasi client mail o strumento di migrazione può leggere.

Vengono offerti due formati:

| Formato | Cosa ottieni | Uso tipico |
|--------|--------------|-------------|
| **EML** | File `.eml` individuali, organizzati in Cartella | Re-import in un client mail, elaborazione per singolo messaggio, leggibilità a lungo termine |
| **MBox** | File Casella di posta tradizionali — **un file per Cartella** | Strumenti e client che leggono MBox, elaborazione massiva di testo |

In entrambi i casi i messaggi vengono scritti come messaggi RFC-822 grezzi, quindi gli
**Allegati sono inclusi** nell'esportazione.

> 💡 **Suggerimento**: Per esportare solo una selezione di messaggi invece di un intero account,
> usa le azioni di massa in [Mail search](Search.md#-acting-on-many-results-at-once).

## 🧭 Accesso {#access}

1. Apri **Mail Accounts** nel menu principale.
2. Clicca sul pulsante **⋮** (altre azioni) sulla riga dell'account e scegli
   **Export**. La stessa voce si trova nella pagina **Details** dell'account.

L'esportazione richiede il ruolo **admin** o **self manager** — gli utenti standard vedono
la voce ma non possono utilizzarla. L'esportazione copre sempre l'intero account; non ci
sono filtri per data o Cartella.

## 🖥️ Utilizzo della pagina di Esportazione {#using-the-export-page}

La pagina mostra i conteggi **totali**, **in entrata** e **in uscita** dei messaggi dell'account,
così puoi vedere la dimensione del lavoro prima di avviarlo. Quindi:

1. Scegli il formato — **EML** o **MBox**.
2. Premi **Start Export**.

L'esportazione viene eseguita **in background**. Verrai indirizzato a una pagina di stato che mostra l'ID
del lavoro, il formato scelto, i timestamp e i conteggi dei messaggi elaborati/totali.

### Stati del lavoro {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled` → `Downloaded`

Quando il lavoro raggiunge lo stato **Completed**, appare un **pulsante di download** sulla pagina di stato.
Lo stesso lavoro è elencato anche nella pagina [Jobs](BackgroundJobs.md).

> ⚠️ **I file di esportazione vengono puliti automaticamente.** Lo ZIP viene eliminato **dopo un
> download riuscito** e, in ogni caso, **dopo 7 giorni**. Scarica il file finché
> viene ancora offerto — l'Archivio stesso non ne risente, puoi semplicemente avviare
> una nuova esportazione in seguito.

## ⚠️ Cose a cui prestare attenzione {#things-to-watch-out-for}

- **Gli account di grandi dimensioni richiedono tempo.** Una Casella di posta molto grande può richiedere diversi minuti e lo
  ZIP può diventare molto grande, a seconda del volume dei messaggi e degli Allegati.
  Poiché il lavoro viene eseguito in background, puoi chiudere il browser e tornare
  alla pagina di stato in seguito.
- **Spazio su disco del server.** Lo ZIP viene assemblato sul server prima che tu lo scarichi,
  quindi assicurati che ci sia abbastanza spazio libero accanto all'Archivio.
- **"Export file not found"** — il lavoro non è ancora terminato, oppure il file è
  già stato scaricato o rimosso dalla pulizia dei 7 giorni. Avvia una nuova esportazione.
- **Nessuna opzione di filtro** — l'esportazione è "tutto o niente" per Account. Usa l'esportazione
  della pagina di ricerca per un sottoinsieme.
- **Non è un'esportazione di conformità.** Per una consegna di supporti dati pronta per l'audit con un
  file di indice, usa invece [Audit Data Export](AuditExport.md) — produce un
  pacchetto nel formato tabulare di massa dati che gli strumenti di audit si aspettano.

## 🔗 Documentazione correlata {#related-documentation}

- [Importing Mail in the Web UI](Import-EML-MBox.md) — la direzione opposta
- [Mail search](Search.md) — esportazione di una selezione di messaggi
- [Background Jobs](BackgroundJobs.md) — elenco lavori, progresso e cancellazione
- [Audit Data Export](AuditExport.md) — pacchetti di esportazione pronti per l'audit
- [CLI commands](CLI.md) — importazione da una directory montata
