# 🔍 Mail Search {#mail-search}

[← Torna all'Indice della Documentazione](index.md)

## 📋 Panoramica {#overview}

La ricerca è il cuore di un Archivio di email. Mail Archiver cerca nel **testo completo**
della tua posta archiviata — oggetto, corpo, mittente e destinatari — in tutti gli
account a cui hai accesso, e restituisce i risultati in millisecondi anche su
Archivi con milioni di messaggi.

Questa guida copre l'interfaccia di ricerca, la sintassi delle query, i filtri, l'ordinamento,
la paginazione e cosa fare quando è necessario agire su un set di risultati.

> **ℹ️ La ricerca vede solo ciò che puoi vedere tu.** I risultati sono filtrati dalle
> autorizzazioni del tuo account. Un non-amministratore troverà solo email provenienti dalle
> Caselle di posta a lui assegnate — la restrizione è applicata nella query stessa,
> non nell'interfaccia.

## 🚀 Accedere alla Ricerca {#accessing-search}

1. Effettua il login e scegli **Archive** nel menu principale
2. La barra di ricerca si trova sopra l'elenco dei risultati

Aprire l'Archivio senza un termine di ricerca elenca le email più recenti, dalla più nuova alla meno nuova. Puoi navigare da lì o restringere il campo con i filtri.

## 🎯 L'Interfaccia di Ricerca {#the-search-interface}

| Campo | Cosa fa |
|---|---|
| **Search Term** | Query full-text — vedi [Query Syntax](#-query-syntax) sotto |
| **From** | Solo email inviate in tale data o successivamente |
| **To** | Solo email inviate in tale data o precedentemente |
| **Direction** | `All`, `Incoming` o `Outgoing` |
| **Account** | Restringi a un singolo account email |
| **Folders** | Restringi a una singola Cartella tramite l'albero delle cartelle |
| **Search** | Esegue la query |
| **Reset** | Cancella tutti i filtri |

Il termine di ricerca corrisponde a **oggetto, corpo, mittente e destinatari**
(`From`, `To`, `Cc`, `Bcc`).

### L'albero delle cartelle {#the-folder-tree}

Le Cartelle sono mostrate come un **albero gerarchico** sulla sinistra, che rispecchia la
struttura delle cartelle della Casella di posta di origine — incluse le cartelle annidate. Clicca su una cartella per
restringere la ricerca ad essa, e usa il controllo di espansione/compressione per nascondere l'albero
quando hai bisogno di spazio sullo schermo.

Selezionare una cartella non cambia il termine di ricerca; i due si combinano.

## 🧩 Query Syntax {#query-syntax}

La sintassi è deliberatamente ridotta. Tutto ciò che segue è ciò che l'applicazione
effettivamente comprende.

### Parole semplici — e perché i prefissi funzionano {#plain-words-and-why-prefixes-work}

Digita una o più parole; devono essere tutte presenti (una ricerca **AND**):

```
invoice january
```

Trova le email che contengono sia `invoice` che `january` nei campi cercati.

> **💡 Le parole parziali vengono trovate automaticamente.** Ogni parola è trattata come un
> **prefisso**, quindi una ricerca per `isenb` trova anche `isenboeck` e `isenböck`, e
> `rech` trova `Rechnung` oltre a `Rechnungen`. Non è necessario un carattere jolly
> — non ce n'è uno da digitare. Questo significa anche che un termine molto breve può
> restituire molti risultati; vedi [Performance](#-performance-and-large-archives).

La ricerca non fa distinzione tra maiuscole e minuscole. `Invoice`, `invoice` e `INVOICE` sono
la stessa query.

### Frasi esatte — virgolette {#exact-phrases-quotes}

Racchiudi il testo tra virgolette doppie per richiedere che le parole siano **una accanto all'altra** in quell'ordine:

```
"important meeting"
```

Senza virgolette, `important meeting` corrisponderebbe anche a un'email in cui le due
parole appaiono in frasi non correlate. Con le virgolette, corrisponde solo alla frase adiacente.

### Ricerca specifica per campo {#field-specific-search}

Aggiungi un prefisso a un termine con il nome di un campo seguito da due punti per cercare **solo** in quel campo:

| Sintassi | Cerca in |
|---|---|
| `subject:invoice` | Solo Oggetto |
| `from:john` | Solo Mittente |
| `to:example.com` | Solo Destinatari |
| `body:important` | Solo Corpo |

I nomi dei campi non fanno distinzione tra maiuscole e minuscole (funziona anche `Subject:`), e ciascuno può essere
combinato con le virgolette per una frase:

```
subject:"monthly invoice"
body:"payment terms"
```

> **ℹ️ Sono supportati quattro campi:** `subject`, `body`, `from`, `to`. Scrivere
> `cc:someone` o `bcc:someone` **non** è una ricerca per campo — il termine è trattato
> come testo ordinario e cercato in tutti i campi. Per trovare un destinatario che era
> solo in Cc, cerca l'indirizzo o il nome senza prefisso.

### Combinare tutto {#combining-everything}

Filtri e termini si sommano. Questa query trova email da qualsiasi mittente assicurativo,
con "car insurance" come frase esatta nel corpo, e "invoice" nell'oggetto:

```
"car insurance" body:"car insurance" subject:invoice from:insurance
```

Combinazioni pratiche:

```
from:amazon subject:"your order"        # order confirmations from Amazon
to:@example.com body:"contract"          # contracts sent to a domain
"rechnung" from:telekom                  # Telekom invoices
subject:mahnung                          # everything that looks like a reminder
```

### Caratteri ignorati {#characters-that-are-ignored}

Il parser della query rimuove i caratteri `& | ! ( ) : *` dai singoli termini,
perché hanno un significato nel motore full-text sottostante. Se la tua ricerca
non restituisce nulla di inaspettato, controlla se un carattere speciale è la causa.

## 📊 Ordinamento e Paginazione {#sorting-and-paging}

### Ordinamento {#sorting}

Clicca sull'intestazione di una colonna per ordinare. Colonne ordinabili:

| Colonna | Ordina per |
|---|---|
| **Subject** | Riga dell'oggetto, in ordine alfabetico |
| **From** | Mittente |
| **To** | Destinatari |
| **Date** | Data di invio |

Cliccare nuovamente sulla stessa intestazione inverte la direzione. Il predefinito è **dalla più recente alla meno recente**.

### Dimensione pagina {#page-size}

Scegli quanti risultati mostrare per pagina:

**20** (predefinito) · **50** · **75** · **100** · **150**

Una pagina più grande mostra più contesto ma richiede più tempo per il rendering. Usa i
controlli **Previous** / **Next** per scorrere le pagine dei risultati; il conteggio dei risultati
ti dice quante email sono state trovate in totale.

> **ℹ️ Il tuo fuso orario si applica alle date mostrate.** Gli orari visualizzati sono
> convertiti da UTC al tuo fuso orario configurato, quindi la data che vedi corrisponde
> al tuo calendario locale — utile quando stai effettuando ricerche intorno alla mezzanotte.

## 👁️ Lavorare con i Risultati {#working-with-results}

Ogni riga mostra l'account, il mittente, l'oggetto, la data e un'**icona 📎 a forma di graffetta**
quando l'email ha degli Allegati. La graffetta è solo un indicatore — non è
un filtro.

Clicca su **Details** per aprire un'email:

- Corpo completo (HTML o testo semplice)
- **Allegati**, elencati con l'opzione di scaricare ciascuno di essi
- **Download as EML** — il messaggio completo, per l'uso altrove
- **Ripristino** — copia l'email nuovamente in una Casella di posta attiva

Consulta [Mailbox Migration](MailboxMigration.md) per il ripristino delle email in una Casella di posta.

## ✅ Agire su molti risultati contemporaneamente {#acting-on-many-results-at-once}

I risultati possono essere selezionati ed elaborati in blocco. Attiva la **Selection Mode** per ottenere le
caselle di controllo, quindi:

- **Select All** / **Deselect All** per la pagina corrente
- **Select all {n} matching** — ogni risultato della ricerca corrente in tutte le
  pagine (mostrato quando i risultati si estendono su più di una pagina)
- **Copy Selected** — ripristina in una Casella di posta
- **Export Selected** — come MBOX o EML
- **Delete Selected** — dall'Archivio

Il contatore mostra *"{selected} di {total} selezionati"* mentre lavori.

### Il limite di selezione {#the-selection-limit}

> **⚠️ È possibile selezionare al massimo 250 email alla volta** (configurabile tramite
> `Selection:MaxSelectableEmails`). Oltre questo limite, l'interfaccia mostra *"Maximum
> selections reached (250)"* e **Too Many — Split Required**.

Quel limite esiste perché ogni operazione deve mantenere i messaggi interessati. L'avviso
*"Split larger operations into multiple batches"* è la soluzione prevista: lavora sui
risultati in gruppi di 250 o meno.

**Select all {n} matching** rispetta lo stesso limite. Quando la ricerca trova più
email del limite consentito, seleziona le prime secondo l'ordine di ordinamento corrente
e mostra l'avviso *"Maximum selections reached"*. Restringi la ricerca (intervallo di date,
cartella, account) per mirare al resto.

Tentare di superarlo mostra: *"Too many emails selected. Maximum allowed is
250 emails. Please reduce your selection or split into multiple operations."*

### Eliminare ogni corrispondenza di un filtro {#deleting-every-match-of-a-filter}

Il pulsante **Delete all matches** accanto a *Reset* elimina ogni email che
corrisponde alla ricerca corrente, non solo la pagina o la selezione attuale. È disponibile per gli amministratori e i self-manager e viene eseguito come un lavoro in background, quindi
il limite di selezione di 250 email non si applica.

La finestra di conferma mostra quante email sono eliminabili e quante sono bloccate dalla policy di Conservazione. Devi digitare quel numero per abilitare la
conferma. Il server conta di nuovo e interrompe l'operazione se il set di risultati è cambiato nel frattempo, ad esempio perché una Sincronizzazione ha aggiunto posta.

Le email bloccate non vengono mai eliminate da questa azione; vengono segnalate come saltate nel
stato del lavoro. `DeletionPolicy:MaxBulkDeleteEmails` (predefinito 100.000) limita un
lavoro. Un filtro che corrisponde a più email viene rifiutato con una richiesta di restringerlo.

Il lavoro appare nella pagina [Background Jobs](BackgroundJobs.md) e può essere
annullato lì.

### Elaborazione diretta vs lavoro in background {#direct-processing-vs-background-job}

Per **Copy** ed **Export** scegli come eseguire il lavoro:

| Opzione | Comportamento |
|---|---|
| **Process on this page (wait for completion)** | Il browser attende; ideale per un piccolo numero di email |
| **Process in background (monitor separately)** | Viene eseguito come un lavoro; puoi chiudere il browser |

Gli export vengono sempre eseguiti in background:

> *"L'export verrà eseguito in background e potrai scaricare il
> risultato dalla pagina Jobs una volta completato."*

Trova i lavori in esecuzione e quelli terminati nella pagina [Background Jobs](BackgroundJobs.md).

> **⚠️ L'eliminazione è permanente.** La conferma è esplicita: *"Sei sicuro di
> voler eliminare permanentemente {n} email selezionate? Questa azione non può essere annullata
> e ogni eliminazione sarà registrata per scopi di audit."* Le eliminazioni sono registrate
> nel registro di accesso.

## ⚡ Performance e Grandi Archivi {#performance-and-large-archives}

La ricerca è supportata da un indice full-text PostgreSQL, quindi la differenza tra una
ricerca veloce e una lenta è solitamente quanto la query sia **selettiva**.

**Cosa fare:**

- Aggiungi un **intervallo di date** quando sai approssimativamente quando è arrivata l'email — questo è
  il metodo più efficace per restringere il campo
- Restringi a un **account** o a una **Cartella** quando sai dove si trova la posta
- Usa **diverse parole** invece di una sola molto breve
- Usa le ricerche per campo (`subject:`, `from:`) per escludere corrispondenze non correlate

**Cosa evitare:**

- Un singolo prefisso breve come `re` — come ricerca per prefisso, corrisponde a una porzione enorme
  del tuo Archivio
- Intervalli di date molto ampi combinati con una parola comune
- Navigare tra molte pagine quando potresti invece restringere la ricerca

> **💡 Se una ricerca sembra lenta, la soluzione è solitamente l'intervallo di date.** Restringere
> a un mese spesso trasforma una ricerca di diversi secondi in una istantanea, perché l'indice
> può saltare la maggior parte dell'Archivio.

## ❓ Risoluzione dei problemi {#troubleshooting}

| Sintomo | Causa | Cosa fare |
|---|---|---|
| Nessun risultato, ma ne ti aspetti alcuni | Termine di ricerca troppo specifico o un errore di battitura | Prova con meno parole; controlla l'ortografia |
| `cc:name` non trova nulla di utile | `cc` non è un campo supportato | Cerca il nome senza prefisso |
| Un carattere speciale interrompe la query | `& \| ! ( ) : *` vengono rimossi | Rimuovi o sostituisci il carattere |
| Troppi risultati | Parola comune o prefisso breve | Aggiungi un intervallo di date o una ricerca per campo |
| *"Too Many — Split Required"* | Più di 250 email selezionate | Riduci la selezione o dividi l'operazione |
| Manca un export | Viene eseguito come un lavoro in background | Controlla [Background Jobs](BackgroundJobs.md) |
| I risultati sembrano mancare | Autorizzazioni dell'account | Conferma che la Casella di posta sia assegnata al tuo utente |
| Le date appaiono spostate | Conversione del fuso orario | Controlla l'impostazione del fuso orario |

## 📚 Documentazione Correlata {#related-documentation}

- [Mailbox Migration](MailboxMigration.md) — ripristinare i risultati in una Casella di posta
- [Background Jobs](BackgroundJobs.md) — monitorare export e ripristini
- [Access Logging](Logs.md) — il Registro di controllo delle ricerche e delle eliminazioni
- [REST API](API.md) — lo stesso Archivio tramite API
- [MCP Server](MCP.md) — ricerca da un agente AI
- [Retention Policies](RetentionPolicies.md) — quando le email lasciano l'Archivio
