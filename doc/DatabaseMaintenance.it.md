# 🔧 Guida alla manutenzione del database {#database-maintenance-guide}

[← Torna all'indice della documentazione](index.md)

## 📋 Panoramica {#overview}

Questa guida spiega come configurare e utilizzare la funzione di manutenzione automatica del database in Mail Archiver. L'applicazione include un servizio in background integrato che esegue automaticamente le operazioni di manutenzione del database PostgreSQL per mantenere prestazioni ottimali e prevenire l'ingrossamento del database.

## ⚠️ Avviso importante {#important-notice}

**I database PostgreSQL non si restringono automaticamente quando i dati vengono eliminati.** Quando si eliminano email o Caselle di posta, il database contrassegna lo spazio come disponibile per il riutilizzo, ma la dimensione effettiva del file del database rimane la stessa. Questo fenomeno è chiamato "database bloat".

Mail Archiver include un servizio di manutenzione automatica che esegue quotidianamente operazioni VACUUM ANALYZE per:
- Recuperare spazio di archiviazione per il riutilizzo all'interno del database
- Aggiornare le statistiche delle query per prestazioni ottimali
- Prevenire il degrado delle prestazioni nel tempo
- Mantenere la salute del database

## 🚀 Manutenzione automatica (Consigliata) {#automatic-maintenance-recommended}

### Configurazione {#configuration}

Abilita la manutenzione automatica del database nel tuo `appsettings.json` o tramite variabili d'ambiente in `docker-compose.yml`:

#### Opzione 1: Tramite appsettings.json {#option-1-via-appsettingsjson}

```json
{
  "DatabaseMaintenance": {
    "Enabled": true,
    "DailyExecutionTime": "02:00",
    "TimeoutMinutes": 30
  }
}
```

#### Opzione 2: Tramite docker-compose.yml (Consigliata) {#option-2-via-docker-composeyml-recommended}

```yaml
services:
  mailarchive-app:
    image: s1t5/mailarchiver:latest
    environment:
      # Enable automatic database maintenance
      - DatabaseMaintenance__Enabled=true
      - DatabaseMaintenance__DailyExecutionTime=02:00
      - DatabaseMaintenance__TimeoutMinutes=30
```

### Opzioni di configurazione {#configuration-options}

| Parametro | Tipo | Predefinito | Descrizione |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Abilita o disabilita la manutenzione automatica |
| `DailyExecutionTime` | string | `"02:00"` | Ora del giorno in cui eseguire la manutenzione (formato HH:mm, 24 ore) |
| `TimeoutMinutes` | integer | `30` | Tempo massimo consentito per le operazioni di manutenzione in minuti |

### Come funziona {#how-it-works}

Quando abilitato, il servizio di manutenzione:

1. **Si avvia automaticamente** con l'applicazione
2. **Attende** fino all'orario di esecuzione configurato
3. **Esegue VACUUM ANALYZE** sull'intero database
4. **Registra l'operazione** nel Registro di controllo dell'applicazione con la durata
5. **Programma nuovamente** l'esecuzione per il giorno successivo

L'operazione di manutenzione:
- ✅ Si esegue senza tempi di inattività
- ✅ Non interferisce con le normali operazioni dell'applicazione
- ✅ Fornisce log e monitoraggio completi
- ✅ Recupera spazio per il riutilizzo all'interno del database
- ✅ Aggiorna le statistiche delle query per prestazioni migliori

### Scegliere l'orario di esecuzione {#choosing-the-execution-time}

Seleziona un orario in cui il sistema ha la minore attività:
- **Consigliato**: Tra le 02:00 e le 04:00 (ore notturne)
- **Considerare**: L'impostazione del fuso orario
- **Evitare**: Le ore di picco di utilizzo o le finestre di Backup

Esempi di configurazione:

```yaml
# Night maintenance (recommended)
- DatabaseMaintenance__DailyExecutionTime=02:00
```

### Monitoraggio della manutenzione automatica {#monitoring-automatic-maintenance}

#### 1. Log dell'applicazione {#1-application-logs}

Controlla i log dell'applicazione per le attività di manutenzione:

```bash
# View recent logs
docker compose logs mailarchive-app | grep "Maintenance"

# View logs in real-time
docker compose logs -f mailarchive-app | grep "Maintenance"
```

#### 2. Registro di controllo (UI) {#2-access-log-ui}

Tutte le operazioni di manutenzione sono registrate nel Registro di controllo dell'applicazione:

1. Naviga in **Logs** → **Access Log** nell'interfaccia utente
2. Filtra per tipo: **Database Maintenance**
3. Visualizza gli orari di esecuzione, la durata e eventuali errori

## 🔧 Manutenzione manuale (Avanzata) {#manual-maintenance-advanced}

### VACUUM FULL per il recupero dello spazio su disco {#vacuum-full-for-disk-space-recovery}

**⚠️ Richiede tempi di inattività dell'applicazione**

VACUUM FULL compatta il database e restituisce lo spazio su disco al sistema operativo, ma richiede lock esclusivi.

```bash
# 1. Stop the application
docker compose stop mailarchive-app

# 2. Run VACUUM FULL
docker compose exec postgres psql -U mailuser -d MailArchiver -c \
  "VACUUM FULL VERBOSE ANALYZE;"

# 3. Start the application
docker compose start mailarchive-app
```

**Quando usarlo:**
- Dopo l'eliminazione di grandi Caselle di posta
- Per recuperare una quantità significativa di spazio su disco
- Durante finestre di manutenzione pianificate

**Importante:**
- Richiede spazio libero su disco pari alla dimensione della tabella
- Può richiedere ore su database di grandi dimensioni
- Blocca tutte le operazioni del database
- Dovrebbe essere programmato durante le ore di bassa attività

Per procedure manuali dettagliate, consultare la [Documentazione PostgreSQL VACUUM](https://www.postgresql.org/docs/current/sql-vacuum.html).

## 📚 Documentazione correlata {#related-documentation}

- [Guida alla configurazione](Setup.md) - Configurazione iniziale e variabili d'ambiente
- [Guida al Backup e Ripristino](BackupRestore.md) - Procedure di Backup del database
- [Documentazione PostgreSQL VACUUM](https://www.postgresql.org/docs/current/sql-vacuum.html) - Documentazione ufficiale di PostgreSQL

## 📝 Riepilogo {#summary}

✅ **Abilita la manutenzione automatica** per un'ottimizzazione del database senza interventi manuali
✅ **Monitora tramite i registri di controllo** per verificare il successo delle operazioni
✅ **Usa VACUUM FULL manuale** solo quando è necessario il recupero dello spazio su disco
✅ **Programma VACUUM FULL** durante le finestre di manutenzione per eliminazioni massicce
✅ **Mantieni aggiornati i Backup** prima di qualsiasi operazione di manutenzione manuale

Il servizio di manutenzione automatica gestisce l'ottimizzazione quotidiana del database, eliminando la necessità di interventi manuali nella maggior parte dei casi.
