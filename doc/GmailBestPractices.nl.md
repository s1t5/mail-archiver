# 📧 Gmail Best Practices {#gmail-best-practices}

[← Terug naar de Documentatie-index](index.md)

Bij het gebruik van Gmail accounts met de Mail Archiver applicatie zijn er verschillende specifieke configuraties en best practices die gevolgd moeten worden om optimale prestaties te garanderen en potentiële problemen te voorkomen.

## 🔐 IMAP Configuratie met App Passwords {#imap-configuration-with-app-passwords}

Gmail accounts kunnen worden geïntegreerd met de Mail Archiver applicatie via IMAP met App Passwords:

1. Schakel 2-Factor Authenticatie in op uw Gmail account
2. Genereer een App Password specifiek voor de Mail Archiver applicatie
3. Gebruik het App Password in plaats van uw reguliere Gmail wachtwoord bij het configureren van het account in Mail Archiver

## 🏷️ Gmail Label Instellingen {#gmail-label-settings}

Het wordt ten zeerste aanbevolen om uw Gmail label instellingen aan te passen om te voorkomen dat het "All Mail" label via IMAP wordt blootgesteld:

1. Ga naar uw Gmail webinterface en open de instellingen (het kleine tandwielpictogram bovenaan) → Alle instellingen bekijken → Tabblad Labels
2. Zoek het "All Mail" label in de lijst
3. Vink de optie "In IMAP weergeven" uit voor "All Mail"

### 📚 Achtergrond over de Gmail Label Implementatie {#background-on-gmail-label-implementation}

Het labelsysteem van Gmail werkt anders dan dat van traditionele e-mailproviders. In Gmail:
- Bestaan alle e-mails in één enkele "All Mail" repository
- Fungeren labels als tags in plaats van mappen
- Het "All Mail" label bevat elke e-mail in uw account
- Wanneer een e-mail een label krijgt, verschijnt deze zowel in de oorspronkelijke map als in het gelabelde gedeelte. Dus alle e-mails in uw inbox verschijnen ook in het All Mail label.

### ⚠️ Problemen met IMAP Ophalen {#issues-with-imap-retrieval}

Wanneer "All Mail" via IMAP wordt blootgesteld, kan dit verschillende problemen veroorzaken:

1. **Dubbele Verwerking**: De archiver kan dezelfde e-mail meerdere keren verwerken omdat deze in meerdere mappen/labels verschijnt
2. **Impact op Prestaties**: Het ophalen van "All Mail" kan extreem traag zijn voor grote accounts met duizenden e-mails
4. **Synchronisatieproblemen**: Er kunnen conflicten ontstaan bij het bepalen naar welke map een e-mail daadwerkelijk behoort

Door IMAP-toegang tot "All Mail" uit te schakelen, zorgt u ervoor dat e-mails slechts één keer van hun primaire locatie worden opgehaald, wat zowel de prestaties als de nauwkeurigheid verbetert.

## 🗑️ Bewaring Beleid en Expunge Instellingen {#retention-policy-and-expunge-settings}

Zoals vermeld in de hoofd-README, vereisen Gmail accounts speciale aandacht met betrekking tot bewaring beleid:

- De Auto-Expunge functie van Gmail moet zijn uitgeschakeld in de Gmail instellingen onder het tabblad "Doorsturen en POP/IMAP"
- Deze instelling is cruciaal om te zorgen dat bewaring beleid correct werkt met Gmail accounts

Om deze instelling te configureren:
1. Ga naar Gmail Instellingen → Alle instellingen bekijken → Tabblad Doorsturen en POP/IMAP
2. Selecteer in de sectie "IMAP-toegang" de optie "Auto-Expunge uit - Wacht tot de client de server bijwerkt." en "Bericht naar de Prullenbak verplaatsen"

## ⚠️ Gmail IMAP Rate Limiting {#gmail-imap-rate-limiting}

Gmail hanteert een dagelijkse limiet van 2500MB voor IMAP ophalen. Houd er bij het archiveren van grote volumes e-mails rekening mee dat u deze limiet kunt bereiken, wat het ophalen van e-mails tijdelijk kan pauzeren tot de volgende dag.

### 📊 Afhandeling van de Limiet {#rate-limit-handling}

Mail Archiver biedt ingebouwde afhandeling van limieten om de IMAP-bandbreedtebeperkingen van Gmail te beheren:

- **Automatische bandbreedte tracking**: Houdt de gedownloade gegevens per account per dag bij
- **Configureerbare limieten**: Stel `BandwidthTracking__DailyLimitMb=2500` in voor Gmail
- **Automatische pauze**: Synchronisatie pauzeert wanneer de limiet is bereikt en hervat na reset
- **Checkpoint systeem**: Synchronisatievoortgang wordt opgeslagen en hervat vanaf de laatste positie

Zie de **[Rate Limit Handling Guide](RateLimitHandling.md)** voor gedetailleerde configuratie en gebruik.

### 🔧 Gmail Configuratie Voorbeeld {#gmail-configuration-example}

```yaml
environment:
  # Enable bandwidth tracking for Gmail
  - BandwidthTracking__Enabled=true
  - BandwidthTracking__DailyLimitMb=2500
  - BandwidthTracking__WarningThresholdPercent=80
  - BandwidthTracking__PauseHoursOnLimit=24
```

### 📈 Overwegingen voor Grote Postbus {#large-mailbox-considerations}

Voor grote Gmail postbussen (bijv. 14 GB) zal de initiële synchronisatie meerdere dagen in beslag nemen:

| Postbus Grootte | Dagen om te voltooien (bij 2500 MB/dag) |
|--------------|-----------------------------------|
| 5 GB | ~2 dagen |
| 10 GB | ~4 dagen |
| 14 GB | ~6 dagen |
| 20 GB | ~8 dagen |

**Tip**: Schakel bandbreedte tracking in **voordat** u grote Gmail accounts toevoegt om vanaf het begin een correcte afhandeling van de limieten te garanderen.

## ✅ Samenvatting Best Practices {#best-practices-summary}

Het volgen van deze best practices helpt om een soepele werking van de Mail Archiver met uw Gmail accounts te garanderen, terwijl de gegevensintegriteit en optimale prestaties behouden blijven.
