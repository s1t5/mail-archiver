# 🧪 Ambiente di test locale {#local-test-environment}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida descrive come eseguire la suite di test, l'applicazione e un target IMAP su una macchina di sviluppo. È rivolta ai collaboratori: lo stack che viene avviato è deliberatamente non sicuro e non è una guida alla distribuzione. Per la distribuzione, consultare la
[Setup Guide](Setup.md).

Sono coinvolte tre componenti:

| Componente | Fornito da | Scopo |
|---|---|---|
| PostgreSQL | `docker-compose.test.yml` | Due database, uno per `dotnet test` e uno per l'applicazione |
| Dovecot | `docker-compose.test.yml` | Un target IMAP che funge da Casella di posta mailcow |
| L'applicazione | `dotnet run` sull'host | Più veloce per iterare rispetto alla ricostruzione dell'immagine del container |

## 🚀 Avvio dello stack {#starting-the-stack}

```bash
docker compose -f docker-compose.test.yml up -d
```

Questo pubblica tutto solo su loopback:

| Service | Host address | Notes |
|---|---|---|
| PostgreSQL | `127.0.0.1:5433` | 5433, non 5432, così non va in conflitto con un server locale |
| Dovecot, plain | `127.0.0.1:1143` | L'autenticazione in chiaro è abilitata |
| Dovecot, TLS | `127.0.0.1:1993` | Il certificato self-signed dell'immagine |

Per interromperlo e scartare tutti i database e le email memorizzate:

```bash
docker compose -f docker-compose.test.yml down -v
```

## 🗄️ I due database {#the-two-databases}

La suite di test lascia dietro di sé delle righe di fixture: dopo un'esecuzione completa, il database di test contiene diverse decine di account di posta e utenti. Se l'applicazione condividesse quel database, la sua Sincronizzazione in background preleverebbe quelle fixture e tenterebbe ripetutamente di raggiungere server che non esistono. Pertanto, il container postgres crea due database al primo avvio
(`tests/postgres/10-create-dev-db.sql`):

| Database | Utilizzato da | Configurato in |
|---|---|---|
| `MailArchiver` | `dotnet test` | `tests/MailArchiver.Tests/appsettings.Test.json` |
| `MailArchiverDev` | `dotnet run` | `appsettings.Development.json` |

Entrambi i file di configurazione sono ignorati da git e devono essere creati localmente.

`tests/MailArchiver.Tests/appsettings.Test.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiver;Username=mailuser;Password=masterkey"
  }
}
```

`appsettings.Development.json` nella root del repository:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=MailArchiverDev;Username=mailuser;Password=masterkey"
  },
  "DataProtection": { "KeyPath": "DataProtection-Keys" },
  "MailSync": { "IgnoreSelfSignedCert": true }
}
```

`DataProtection:KeyPath` è importante all'esterno del container: di default è impostato sul percorso assoluto
`/app/DataProtection-Keys`, che non esiste su una macchina di sviluppo, e senza di esso ogni
richiesta fallisce durante la lettura del key ring. `IgnoreSelfSignedCert` è ciò che permette all'applicazione
di accettare il certificato Dovecot sulla porta 1993.

## ✅ Esecuzione dei test {#running-the-tests}

```bash
dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

Le migrazioni vengono applicate automaticamente da `TestDbFixture` al primo avvio. La maggior parte dei test non richiede alcun database; gli altri utilizzano PostgreSQL, quindi falliscono con `Name or service not known` se lo stack non è in esecuzione.

È possibile fornire anche una stringa di connessione tramite l'ambiente, che sovrascrive ogni file di configurazione:

```bash
MailArchiverTest__ConnectionStrings__DefaultConnection="Host=...;Port=...;Database=...;Username=...;Password=..." \
  dotnet test tests/MailArchiver.Tests/MailArchiver.Tests.csproj
```

## 🖥️ Esecuzione dell'applicazione {#running-the-application}

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj
```

`Properties/launchSettings.json` imposta già `ASPNETCORE_ENVIRONMENT=Development` per
`dotnet run`, quindi funziona anche un semplice `dotnet run --project MailArchiver.csproj` e serve
http://localhost:5179 (il profilo `http`). `ASPNETCORE_URLS` sopra seleziona invece solo la porta del container
5000.

Le migrazioni in sospeso vengono applicate a `MailArchiverDev` all'avvio. Accedi con le credenziali di `appsettings.json`
(`Authentication:Username` e `Authentication:Password`).

## 📬 Utilizzo del target Dovecot {#using-the-dovecot-target}

Dovecot accetta **qualsiasi username** con la password `pass`, quindi per una nuova Casella di posta target non è necessario il provisioning: scegli un nome ed effettua l'accesso. Aggiungila sotto Mail Accounts con:

| Field | Plain | TLS |
|---|---|---|
| IMAP Server | `localhost` | `localhost` |
| IMAP Port | `1143` | `1993` |
| Use SSL | off | on |
| Username | qualsiasi cosa, es. `offload-target@example.com` | same |
| Password | `pass` | same |

La configurazione in `tests/dovecot/dovecot.conf` è modellata sul default dell'immagine stessa, con
tre differenze deliberate che la fanno comportare come un target mailcow:

- **Archiviazione Maildir** invece della sdbox dell'immagine, che è ciò che mailcow utilizza.
- **`separator = /`**, in linea con mailcow. I percorsi delle Cartelle sono divisi su `/` e `\` quando un Ripristino
  ricrea una gerarchia di Cartelle, quindi un server che utilizza `.` si comporterebbe diversamente.
- **Le cartelle special-use `Drafts`, `Junk`, `Sent` e `Trash` sono pre-create**, poiché mailcow
  le pre-crea. Questo è importante quando si testa una migrazione da Exchange: una Cartella sorgente chiamata
  `Sent Items` finisce accanto alla `Sent` esistente invece di fondersi con essa, che è la
  situazione che il rinominamento delle Cartelle deve gestire.

L'autenticazione in chiaro è abilitata sulla porta 1143 perché l'applicazione si connette senza alcuna
sicurezza di trasporto quando un Account ha SSL disabilitato.

## 🌱 Popolamento di un Archivio per i test {#seeding-an-archive-to-test-against}

Un Archivio vuoto non è molto utile per testare un Ripristino o una migrazione, quindi
`tests/seed/seed.sh` ne crea uno:

```bash
# once, so the schema exists
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 \
  dotnet run --project MailArchiver.csproj

tests/seed/seed.sh            # seed
tests/seed/seed.sh --reset    # discard what was seeded before, then seed again
```

Crea due Account, importa i fixture e stampa i dati caricati. Entrambi gli Account sono
creati con un intervallo di Sincronizzazione per Account di un anno, e la sorgente è
ulteriormente disabilitata, in modo che il servizio di Sincronizzazione in background la ignori: il suo server non esiste e il target Dovecot
deve solo ricevere append. Una precisazione: lo stato di pianificazione risiede in memoria, quindi ogni
riavvio dell'applicazione pianifica il target una volta, immediatamente. Il target pertanto
esclude anche le Cartelle special-use di Dovecot (`INBOX`, `Drafts`, `Junk`, `Sent`, `Trash`), in modo che
la Sincronizzazione al primo riavvio non abbia nulla da archiviare. Eseguire lo script due volte è innocuo;
la seconda esecuzione segnala ogni messaggio come già presente.

I fixture sono salvati sotto `tests/seed/fixtures`, un mbox per ogni Cartella sorgente, con
`manifest.tsv` che mappa ogni file alla sua Cartella di destinazione. Vengono importati tramite la
CLI `--import-mbox` dell'applicazione stessa, e questo dettaglio è deliberato: la CLI instrada attraverso
`MailImporter.ImportEmailToDatabase`, lo stesso importer utilizzato dalla Sincronizzazione IMAP, quindi le righe atterrano con
esattamente il formato che un vero Archivio contiene -- indirizzi uniti con `", "`, oggetti passati attraverso
`CleanText`, `SentDate` convertiti nel fuso orario di visualizzazione, `RawHeaders` estratti e troncati.
Inserire le righe direttamente nel database salterebbe tutto questo e produrrebbe fixture che
differiscono silenziosamente dai dati di produzione.

Ogni messaggio fixture contiene un header `X-Fixture-Purpose` che spiega perché esiste, in modo che i file
documentino se stessi:

```
X-Fixture-Purpose: ImapMailRestorer.cs:1196 refuses to emit this, so MimeKit invents a
  fresh random Message-Id on every append and the row duplicates each repetition
Subject: Message-ID without an at sign
Message-ID: <bare-token-no-at-sign>
```

Le date sono fisse piuttosto che relative al giorno corrente. Un limite di migrazione viene risolto in una
data assoluta prima dell'esecuzione di un job, quindi un set di fixture fisso e un limite fisso testano la stessa cosa
in modo più prevedibile rispetto a uno mobile. I messaggi spaziano dal 2012 al 2026.

Il layout delle Cartelle rispecchia una Casella di posta Exchange, e ogni Cartella esiste per testare qualcosa:

| Cartella sorgente | Perché è presente |
|---|---|
| `INBOX` | date che coprono quindici anni, più i messaggi edge-case sotto riportati |
| `Sent Items` | collide con l'esistente `Sent` special-use del target Dovecot |
| `Sent Items/2019` | un rinomina deve riscrivere solo il segmento di percorso iniziale |
| `Sent Items Archive` | condivide un prefisso come sottostringa ma non su un confine di segmento, quindi non deve essere riscritto |
| `Deleted Items` | esclusione della Cartella |
| `Deleted Items/Old` | un'esclusione su un genitore deve coprire i discendenti |
| `Junk E-Mail` | il nome usato da Exchange per una Cartella che Dovecot chiama `Junk` |
| `Archive/Projects/2020` | tre livelli di annidamento |

I singoli messaggi includono un messaggio senza header `Message-ID` (che riceve il fallback
deterministico), uno il cui `Message-ID` non ha `@` (che non sopravvive intatto a un Ripristino), un oggetto
contenente un carattere di controllo, un messaggio con una catena `Received` a tre hop e uno senza,
diversi con due destinatari `To`, e due coppie quasi duplicate a un secondo di distanza.

### Una nota sul codice di uscita {#a-note-on-the-exit-code}

`tests/seed/seed.sh` controlla i conteggi di fallimenti e messaggi malformati dall'output dell'importazione piuttosto che
dal codice di uscita del processo. `MBoxImportService` segnala `CompletedWithErrors` quando ha saltato i
duplicati, e la CLI mappa ogni stato diverso da `Completed` all'exit 1, quindi rieseguire un'
importazione termina con un codice non zero anche quando non si è verificato nulla di errato. Rieseguire lo script di seeding è quindi sicuro
e segnala ogni messaggio come già presente.

## ⚠️ Non per la produzione {#not-for-production}

- Qualsiasi username si autentica su Dovecot con una singola password fissa.
- L'autenticazione in chiaro è abilitata.
- Le credenziali del database sono quelle predefinite documentate.
- Lo stato della posta e del database risiede in volumi Docker che `down -v` elimina.

## 🔧 Risoluzione dei problemi {#troubleshooting}

**I test falliscono con `Name or service not known`** — lo stack non è in esecuzione, oppure
manca `appsettings.Test.json` quindi la stringa di connessione torna al valore predefinito del container
`Host=postgres`.

**`NETSDK1226` durante la build** — dati di workload incompleti nel locale .NET SDK, non correlati a questo
repository. Eseguire `dotnet workload repair`, oppure eseguire la build con
`-p:AllowMissingPrunePackageData=true`.

**Dovecot si riavvia in un loop** — un errore di configurazione. `docker compose -f
docker-compose.test.yml logs dovecot` riporta la riga incriminata. Nota che Dovecot non
accetta diverse impostazioni su una singola riga all'interno di un blocco.

**L'applicazione restituisce HTTP 500 su ogni richiesta** — solitamente il data protection key ring.
Verificare che `DataProtection:KeyPath` punti a una directory scrivibile.
