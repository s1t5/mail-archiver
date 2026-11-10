# 📊 Rate Limit Handling {#rate-limit-handling}

[← Terug naar de Documentatie-index](index.md)

## 📋 Overzicht {#overview}

Veel e-mailproviders leggen bandbreedtelimieten op aan IMAP-toegang. Deze limieten variëren doorgaans van enkele honderden megabytes tot meerdere gigabytes per periode van 24 uur. Wanneer het synchroniseren van grote Postbus-inhoud die deze limieten overschrijdt, zou het synchronisatieproces normaal gesproken falen of onvolledig zijn.

Mail Archiver bevat een ingebouwd systeem voor het afhandelen van rate limits dat deze beperkingen automatisch beheert, wat zorgt voor een betrouwbare Synchronisatie, zelfs voor grote Postbus-inhoud.

## ⚙️ Hoe het werkt {#how-it-works}

### Automatische Bandbreedte Tracking {#automatic-bandwidth-tracking}

Het systeem monitort continu de hoeveelheid gegevens die wordt gedownload tijdens de IMAP-synchronisatie. Wanneer de geconfigureerde bandbreedtelimiet is bereikt:

1. **Sync pauzeert automatisch**: De huidige synchronisatie-operatie wordt op een nette manier gestopt.
2. **Voortgang wordt opgeslagen**: Checkpoints slaan de exacte positie op waar de synchronisatie stopte.
3. **Statusupdates**: Het Account toont de status "Rate-Limited" in het dashboard.
4. **Automatisch hervatten**: Wanneer de limiet wordt gereset (meestal na 24 uur), wordt de synchronisatie hervat vanaf het opgeslagen checkpoint.

### Voorbeeld: 14 GB Postbus met een dagelijkse limiet van 2,5 GB {#example-14-gb-mailbox-with-25-gb-daily-limit}

```
Day 1:  Sync starts → ~2.5 GB downloaded → Rate-Limited
        Status: Rate-Limited, Checkpoint saved

Day 2:  Limit still active → Sync skipped

Day 3:  Limit resets → Sync resumes from checkpoint
        → Additional ~2.5 GB → Rate-Limited again

Day 4-6: Continues...

Day N:   All 14 GB synced → Status: Completed
```

## 🔧 Configuratie {#configuration}

### Inschakelen van Bandbreedte Tracking {#enabling-bandwidth-tracking}

Voeg de volgende sectie toe aan uw `appsettings.json`:

```json
{
  "BandwidthTracking": {
    "Enabled": true,
    "DailyLimitMb": 2500,
    "WarningThresholdPercent": 80,
    "PauseHoursOnLimit": 24
  }
}
```

### Configuratie-opties {#configuration-options}

| Optie | Beschrijving | Standaard | Voorbeeld |
|--------|-------------|---------|---------|
| `Enabled` | Inschakelen/uitschakelen van bandbreedte tracking | `false` | `true` |
| `DailyLimitMb` | Dagelijkse downloadlimiet in megabytes | `2500` | `2500` |
| `WarningThresholdPercent` | Percentage van de limiet om waarschuwingen in logs te triggeren | `80` | `80` |
| `PauseHoursOnLimit` | Aantal uren om te pauzeren wanneer de limiet is bereikt | `24` | `24` |

### Het instellen van de juiste limiet {#setting-the-correct-limit}

Om de juiste `DailyLimitMb` waarde te bepalen:

1. Controleer de documentatie of de servicevoorwaarden van uw e-mailprovider.
2. Zoek naar IMAP-bandbreedtelimieten of downloadquota's.
3. Neem contact op met uw e-mailbeheerder als u een zakelijk e-mailsysteem gebruikt.
4. Begin met een conservatieve waarde als u het niet zeker weet (bijv. 2000 MB).
5. Monitor de logs op rate limit-waarschuwingen tijdens de eerste synchronisaties.

## 👤 Gebruikerservaring {#user-experience}

### Dashboard Indicatoren {#dashboard-indicators}

Wanneer rate limiting actief is, toont het Account:

- **Status**: "Rate-Limited" 
- **Info Banner**: Legt de situatie uit en geeft de geschatte reset-tijd aan.
- **Sync Voortgang**: Toont de e-mails die zijn verwerkt voordat de limiet werd bereikt.

### Automatisch Gedrag {#automatic-behavior}

1. **Geen handmatige interventie vereist**: Het systeem regelt alles automatisch.
2. **Applicatie herstarts**: Als de applicatie opnieuw wordt opgestart, wordt de rate limit-status bewaard in de database.
3. **Meerdere Accounts**: Elk Account houdt zijn eigen bandbreedte onafhankelijk bij.

### Wat wordt opgeslagen {#what-gets-saved}

Wanneer een rate limit optreedt, slaat het systeem het volgende op:

- **Sync Checkpoints**: De exacte positie in elke Map waar de synchronisatie stopte.
- **Bandbreedte Gebruik**: Het totaal aantal bytes dat die dag is gedownload.
- **Limiet Status**: Of de limiet is bereikt en wanneer deze wordt gereset.

## 🗄️ Database Onderhoud {#database-maintenance}

De `DatabaseMaintenanceService` ruimt automatisch oude gegevens op:

- **Bandbreedte Records**: Worden na 7 dagen verwijderd.
- **Sync Checkpoints**: Voltooide checkpoints worden na 30 dagen verwijderd.
- **Onvolledige Checkpoints**: Worden bewaard totdat de synchronisatie is voltooid.

## ✅ Best Practices {#best-practices}

### Voor grote Postbus-inhoud {#for-large-mailboxes}

1. **Schakel Bandbreedte Tracking in**: Essentieel voor Postbus-inhoud die groter is dan de dagelijkse limiet.
2. **Plan voor de eerste synchronisatie**: Een grote Postbus-inhoud kan er meerdere dagen over doen om volledig te synchroniseren.
3. **Monitor de voortgang**: Controleer het dashboard voor de rate-limit status.
4. **Niet onderbreken**: Laat het automatische hervatten de voortzetting afhandelen.

### Voor Beheerders {#for-administrators}

1. **Stel passende limieten in**: Stem de `DailyLimitMb` af op de werkelijke limieten van uw e-mailprovider.
2. **Monitor Logs**: Waarschuwingsberichten verschijnen bij 80% van de limiet.
3. **Database Ruimte**: Bandbreedte tracking verbruikt minimale extra opslag.
4. **Back-up voor wijzigingen**: Maak altijd een Back-up voordat u de rate limit-instellingen wijzigt.

### Prestatieoverwegingen {#performance-considerations}

- ✅ **Geheugeneffect**: Verwaarloosbaar - er worden alleen bytes bijgehouden, geen e-mails opgeslagen.
- ✅ **Database Effect**: Minimaal - kleine records worden automatisch opgeruimd.
- ✅ **Sync Snelheid**: Geen impact op de prestaties tijdens actieve synchronisatie.

## 🛡️ Transient FETCH Errors / Server-Side Throttling {#transient-fetch-errors-server-side-throttling}

Naast de byte-gebaseerde bandbreedte tracking die hierboven is beschreven, detecteert Mail Archiver ook **per-bericht throttling** die wordt gesignaleerd door de IMAP-server zelf en reageert hierop. Sommige gehoste IMAP-providers passen per-account of per-IP throttling toe die **onafhankelijk is van de geconfigureerde `BatchOperation` pauzes**, omdat de throttling wordt afgedwongen aan de serverzijde op basis van het totale verzoekvolume tijdens de huidige sessie.

### Symptomen {#symptoms}

De server geeft een `NO` (of `BAD`) respons op een enkele `FETCH` opdracht voor een berichtendatabestand, met berichten zoals:

- `NO Service temporarily unavailable`
- `NO Server unavailable, try again later`
- `NO [LIMIT] ...`
- `NO [OVERQUOTA] ...`
- `NO [UNAVAILABLE] ...`
- `NO [INUSE] ...`
- responsen die `throttle`, `rate limit` of soortgelijke markeringen bevatten

Authenticatie, het opvragen van de Map-lijst en de berichtzoekopdracht zelf slagen meestal nog wel — alleen het daadwerkelijke ophalen van het volledige bericht mislukt.

### Automatische Afhandeling {#automatic-handling}

Wanneer een dergelijke tijdelijke (transient) fout wordt gedetecteerd, past Mail Archiver een per-bericht retry-strategie toe:

1. **Retry met exponentiële backoff**: Tot **3 pogingen** voor hetzelfde bericht, met vertragingen van **5 s → 15 s → 60 s**.
2. **Herstel van verbinding**: Voor elke poging worden de verbinding, authenticatie en de status van de Map-selectie gevalideerd en indien nodig hersteld.
3. **Reset bij succes**: Een succesvolle FETCH reset de teller voor opeenvolgende fouten.
4. **Nette sync pauze**: Als **10 opeenvolgende berichten** na alle pogingen nog steeds tijdelijke fouten geven, wordt de huidige synchronisatie op een nette manier gepauzeerd — via hetzelfde mechanisme als de pauze bij de bandbreedtelimiet:
   - De `LastSync` tijdstempel wordt **niet** bijgewerkt.
   - Als `BandwidthTracking` is ingeschakeld, worden checkpoints bewaard zodat de volgende synchronisatie wordt hervat vanaf het laatst succesvol verwerkte bericht.
   - De taak wordt gemarkeerd als rate-limited.
   - De volgende geplande synchronisatie zal automatisch worden hervat.

### Mitigatie {#mitigation}

Als u regelmatig tegen server-side throttling aanloopt, verlaag dan de snelheid van de verzoeken door uw `BatchOperation` instellingen aan te passen:

```yaml
BatchOperation__BatchSize=10
BatchOperation__PauseBetweenEmailsMs=500
BatchOperation__PauseBetweenBatchesMs=5000
```

Let op: sommige providers passen throttling toe op basis van het **totaal aantal bytes** of het **totaal aantal FETCHs per tijdsvenster** in plaats van de snelheid per seconde. In die gevallen:

- Schakel `BandwidthTracking` in (zie hierboven) en configureer `DailyLimitMb` om overeen te komen met de gedocumenteerde limiet van de provider. De synchronisatie zal dan netjes pauzeren wanneer het dagelijkse budget is uitgeput en de volgende dag automatisch hervatten.
- De eerste synchronisaties van zeer grote Postbus-inhoud kunnen meerdere dagen in beslag nemen, wat het beoogde gedrag is.

### Wat telt als een tijdelijke (transient) fout {#what-counts-as-a-transient-error}

Alleen IMAP `NO`/`BAD` responsen met tekst of respons-codes gerelateerd aan throttling worden behandeld als tijdelijk. Andere fouten (ongeldige berichten, UTF-8 decodeerproblemen, etc.) worden nog steeds onmiddellijk als `FailedEmails` geteld en triggeren de retry/pauze-logica **niet**.

## 📚 Gerelateerde Documentatie {#related-documentation}

- [DatabaseMaintenance.md](DatabaseMaintenance.md) - Procedures voor database-opschoning
- [GmailBestPractices.md](GmailBestPractices.md) - Gmail-specifieke configuratie
