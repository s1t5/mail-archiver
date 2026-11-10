# 📥 Mailbox Migration Guide {#mailbox-migration-guide}

[← Terug naar Documentatie Index](index.md)

Deze gids legt uit hoe u een Postbus migreert van de ene e-mailprovider naar de andere met behulp van de Mail Archiver-applicatie.

## 📋 Overzicht {#overview}

De Mail Archiver-applicatie biedt functionaliteit om e-mails te migreren van een bron-Postbus naar een doel-Postbus. Dit is bijzonder nuttig bij het wisselen van e-mailprovider terwijl u uw e-mailgeschiedenis behoudt. De mapstructuur van de bron-Postbus blijft tijdens de migratie ook behouden.

> **Alleen recente e-mail migreren?** De hieronder beschreven workflow kopieert de *volledige* gearchiveerde Postbus, en het twee keer uitvoeren ervan voegt alles een tweede keer toe. Als de nieuwe Postbus alleen e-mail moet bevatten die nieuwer is dan een bepaalde datum, terwijl de volledige geschiedenis in het Archief blijft, gebruik dan in plaats daarvan de [date-windowed offload](Offload.md). Deze neemt een datavenster in ontvangst, kan mappen uitsluiten en hernoemen, biedt een dry run, en is veilig te herhalen omdat het alles overslaat wat de doel-Postbus al bevat. Het is ook aan te sturen vanaf de opdrachtregel, wat een vloot aan Postbussen scriptbaar maakt.

## 🚀 Migratieproces {#migration-process}

Volg deze stappen om uw Postbus te migreren:

1. **Voeg het bron-Account toe**
   - Navigeer naar de sectie "Email Accounts"
   - Klik op "Add Account"
   - Voer de gegevens en inloggegevens van uw bron-Account in
   - Sla het Account op

2. **Synchroniseer het bron-Account**
   - Sta de applicatie toe om alle e-mails van het bron-Account te synchroniseren
   - Wacht tot het Synchronisatieproces is voltooid

3. **Voeg het doel-Account toe**
   - Klik in de sectie "Email Accounts" op "Add Account"
   - Voer de gegevens en inloggegevens van uw doel-Account in
   - Sla het Account op

4. **Synchroniseer het doel-Account**
   - Als het doel-Account nieuw is, synchroniseer het dan om ervoor te zorgen dat het correct is ingesteld
   - Wacht tot het Synchronisatieproces is voltooid

5. **Start de migratie**
   - Identificeer het bron-Account in de sectie "Email Accounts"
   - Ga naar de accountgegevens
   - Selecteer de optie "Copy All Emails to Another Mailbox"
   - Kies het doel-Account en een doel-Map
   - Schakel de optie **"Preserve Folder Structure"** in – de geselecteerde doel-Map dient dan als de root waaronder de volledige bron-mapstructuur recursief wordt aangemaakt
   - Als deze optie niet is ingeschakeld, worden alle e-mails rechtstreeks in de geselecteerde doel-Map geplaatst (plat, zonder submappen)
   - Start het migratieproces

6. **Monitor de migratie**
   - Als er een groot aantal e-mails is, wordt de migratie als een achtergrondtaak uitgevoerd
   - De status en voortgang van de migratie kunnen worden bekeken in het tabblad "Jobs"
   - Wacht tot de migratie succesvol is voltooid

7. **Schakel het bron-Account uit**
   - Ga na de succesvolle overdracht terug naar het bron-Account
   - Zet het bron-Account op "Disabled" in de accountinstellingen
   - Dit zorgt ervoor dat het in de toekomst niet langer wordt gearchiveerd

## ⚠️ Belangrijke opmerkingen {#important-notes}

- Deze workflow is niet herhaalbaar: het opnieuw uitvoeren ervan voegt elk bericht een tweede keer toe. De [date-windowed offload](Offload.md) is het herhaalbare alternatief.
- Zorg ervoor dat het bron-Account voldoende machtigingen heeft om alle e-mails te lezen
- Zorg ervoor dat het doel-Account voldoende opslagruimte heeft voor de gemigreerde e-mails
- Het migratieproces kan enige tijd in beslag nemen, afhankelijk van het aantal e-mails en de verbindingssnelheid
- Het wordt aanbevolen om de migratie uit te voeren tijdens daluren om de impact op de e-mailservice te minimaliseren
- Controleer regelmatig de migratiestatus in het tabblad "Jobs" om er zeker van te zijn dat het proces vordert
- Verifieer na de migratie of alle e-mails succesvol naar het doel-Account zijn overgebracht

## ✅ Best Practices {#best-practices}

- Test het migratieproces altijd met een kleine subset van e-mails voordat u de volledige Postbus migreert
- Houd een Back-up van belangrijke e-mails bij voordat u aan de migratie begint
- Plan de migratie in op een tijdstip waarop het e-mailgebruik minimaal is
- Monitor de voortgang van de migratie nauwlettend en wees voorbereid op het oplossen van eventuele problemen die zich kunnen voordoen
- Controleer na voltooiing van de migratie of alle e-mails correct zijn overgebracht en toegankelijk zijn
