# Mailcow instance archiveren {#mailcow-instance-archiving}

[← Terug naar de documentatie-index](index.md)

## Overzicht {#overview}

Mail Archiver kan de Postboxen van een [Mailcow](https://mailcow.email) instance centraal archiveren, zonder de persoonlijke wachtwoorden van de eigenaren van de Postbus te verzamelen.

Een administrator verbindt de instance eenmalig met de API-sleutel. Mail Archiver toont vervolgens de Postboxen en maakt voor elke geselecteerde Postbus die gearchiveerd moet worden een **specifiek app-wachtwoord** aan dat beperkt is tot `imap_access`. Dat app-wachtwoord wordt door de bestaande IMAP-provider gebruikt om de Postbus te archiveren en kan op elk moment worden ingetrokken.

Administrators bereiken de instance manager via **Add Account → Mailcow** of via het actiemenu van een Account dat al door Mailcow wordt beheerd. De manager is alleen toegankelijk voor administrators, omdat de opgeslagen API-sleutel de inloggegevens voor elke Postbus in de instance kan beheren.

## Accounts aanmaken vanuit Mailcow {#creating-accounts-from-mailcow}

In **Mail Accounts → Add Account** kunnen administrators **Mailcow** selecteren als de provider. Het formulier biedt vervolgens de verbonden instances en een actie **Manage mailboxes**. Door daar Postboxen te selecteren, worden gewone IMAP-archiefaccounts aangemaakt met specifieke app-wachtwoorden; er wordt geen apart Mailcow-account opgeslagen. Als er nog geen instance bestaat, opent **Manage Mailcow instances** de instance manager waar een verbinding kan worden gemaakt.

## Een bestaand Account koppelen {#linking-an-existing-account}

In de bewerkingsweergave van een IMAP-account kunnen administrators de provider wijzigen naar **Mailcow** en een instance kiezen. Het account behoudt de IMAP-provider maar wordt overgenomen door het Mailcow-beheer: Mail Archiver maakt een specifiek app-wachtwoord aan, verifieert dit via IMAP en vervangt het opgeslagen wachtwoord van het account. Het account verschijnt vervolgens met een Mailcow-actie in de accountlijst.

Om het Mailcow-beheer te verlaten, selecteert u een andere provider en slaat u op. Het app-wachtwoord wordt ingetrokken en het lokale wachtwoord wordt gewist. Bij het terugschakelen naar IMAP voert u een nieuw IMAP-wachtwoord in; bij het schakelen naar Microsoft 365 of een account dat alleen voor import is, worden de eigen inloggegevens gebruikt. Niet-administrators kunnen nog steeds de niet-inloggegevensinstellingen van een door Mailcow beheerd Account bewerken, maar kunnen het niet koppelen of ontkoppelen.

## Vereisten {#prerequisites}

### Versleuteling van inloggegevens moet zijn ingeschakeld {#credential-encryption-must-be-enabled}

Mail Archiver slaat de Mailcow API-sleutel en elk gegenereerd app-wachtwoord versleuteld op (at rest) met behulp van de bestaande functie voor versleuteling van inloggegevens. Stel

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```

in voordat u een instance verbindt. Het aanmaken van een instance en het activeren van Postboxen wordt beide geweigerd terwijl versleuteling is uitgeschakeld, zodat inloggegevens nooit als plaintext via deze functie kunnen worden geschreven. Bewaar de sleutel buiten de database en neem deze op in uw beveiligde Back-up — zonder deze kunnen de opgeslagen app-wachtwoorden niet worden ontsleuteld en kunnen Postboxen niet worden aangemaakt. Zie [Setup](Setup.md#-security-settings) voor details.

### Mailcow API-toegang {#mailcow-api-access}

Maak een API-sleutel aan in Mailcow onder **Configuration → Access → API**. De sleutel moet toestemming hebben om:

- Postboxen op te sommen (`get/mailbox/all`),
- App-wachtwoorden op te sommen, aan te maken en te verwijderen (`get/app-passwd/all`, `add/app-passwd`, `delete/app-passwd`).

Beperk de API tot het IP-adres van de Mail Archiver host in de API-instellingen van Mailcow. De app-password endpoint van Mailcow vereist dat de **App Passwords** functie is ingeschakeld voor de instance.

### Netwerktoegang {#network-access}

- **HTTPS** naar de Mailcow origin (bijvoorbeeld `https://mail.example.com`).
- **IMAP over TLS** naar de IMAP host, normaal gesproken poort `993`. Plaintext IMAP wordt niet ondersteund voor het aanmaken van Postboxen.

## Stapsgewijze instructies {#step-by-step-usage}

### Een instance verbinden {#connecting-an-instance}

1. Meld u aan bij Mail Archiver als administrator.
2. Open **Mail Accounts** en klik op **Mailcow instances**.
3. Klik op **Connect Mailcow instance**.
4. Voer de verbindingsgegevens in:
   - **Account name** — een weergavenaam voor de instance in Mail Archiver.
   - **Mailcow URL** — alleen de HTTPS origin, bijvoorbeeld `https://mail.example.com`. Paden, query strings en ingebedde inloggegevens worden geweigerd.
   - **API key** — de sleutel die in Mailcow is aangemaakt.
   - **IMAP host** — de hostname die wordt gebruikt voor het archiveren, bijvoorbeeld `mail.example.com`. Voer alleen de hostname in, zonder schema of poort.
   - **IMAP TLS port** — standaard `993`.
5. Klik op **Connect**. Mail Archiver verifieert de API-sleutel door de Postboxen op te sommen voordat de instance wordt opgeslagen. Bij succes wordt de selectie van Postboxen geopend.

De API-sleutel wordt versleuteld opgeslagen en wordt nooit teruggegeven in het formulier of in een foutmelding. HTTP-omleidingen zijn uitgeschakeld voor API-aanroepen, zodat de sleutel niet naar een andere origin kan worden doorgestuurd.

### Postboxen activeren {#activating-mailboxes}

1. Filter op de Postbuspagina optioneel de lijst met het zoekveld. De filter komt overeen met het adres en de weergavenaam en is niet hoofdlettergevoelig.
2. Selecteer de Postboxen die gearchiveerd moeten worden. **Select all** en **Clear selection** hebben alleen invloed op de momenteel zichtbare (gefilterde) Postboxen, dus filteren is een veilige manier om een deel te kiezen.
3. Klik op **Activate archiving**.

Voor elke geselecteerde Postbus doet Mail Archiver het volgende:

1. valideert het adres opnieuw tegen de huidige Postbuslijst van Mailcow en negeert uitgeschakelde Postboxen,
2. maakt een archiefaccount aan met de naam `{instance name} – {mailbox display name}`,
3. genereert een willekeurig app-wachtwoord met de naam `MailArchiver-<unique id>` dat beperkt is tot `imap_access`,
4. verifieert de inloggegevens met een IMAP-login over TLS,
5. en activeert pas daarna de Synchronisatie.

Als de IMAP-verificatie mislukt, blijft het account uitgeschakeld en kan het opnieuw worden geprobeerd.

### Statusbadges voor Postboxen {#mailbox-status-badges}

De selectiepagina labelt elke Postbus zodat duidelijk is waarom een selectievakje wel of niet beschikbaar is:

| Badge | Betekenis | Selecteerbaar |
| --- | --- | --- |
| **Available** | Vermeld in Mailcow, nog niet aangemaakt | ja |
| **Active** | Aangemaakt en synchroniserend | nee |
| **Pending** | Aanmaken gestart maar niet voltooid (opnieuw te proberen) | ja |
| **Revoked** | App-wachtwoord is ingetrokken; kan opnieuw worden geactiveerd | ja |
| **Existing** | Een onafhankelijk IMAP-account met dit adres bestaat al | nee |
| **Disabled** | De Postbus is uitgeschakeld in Mailcow | nee |

### Een wachtende Postbus opnieuw proberen {#retrying-a-pending-mailbox}

De gegenereerde inloggegevens en de unieke naam van het app-wachtwoord worden opgeslagen **voordat** de Mailcow API wordt aangeroepen. Als een verzoek ambigu mislukt — bijvoorbeeld een timeout nadat het app-wachtwoord is aangemaakt — blijft de Postbus zichtbaar als **Pending**. Herlaad de pagina en selecteer deze opnieuw: Mail Archiver zoekt eerst naar de app-naam en maakt alleen een nieuw app-wachtwoord aan als er geen bestaat. Het opnieuw proberen leidt niet tot dubbele inloggegevens.

Het aanmaken en intrekken voor één instance wordt geserialiseerd met behulp van PostgreSQL advisory locks, zodat twee gelijktijdige bewerkingen niet twee keer dezelfde Postbus kunnen aanmaken.

### Toegang intrekken {#revoking-access}

Klik op **Revoke app password** naast een Postbus. Dit:

- schakelt toekomstige Synchronisatie uit,
- verwijdert het beheerde app-wachtwoord in Mailcow,
- en wist het lokale wachtwoord.

**Gearchiveerde mail wordt bewaard.** Alleen het beheerde app-wachtwoord wordt verwijderd — andere app-wachtwoorden die bij de Postbus horen, worden nooit aangeraakt. Lopende synchronisatie-operaties kunnen hun bestaande verbinding voltooien.

Als het externe verzoek mislukt, blijft het account uitgeschakeld en kan het intrekken opnieuw worden geprobeerd.

Het verwijderen van een archiefaccount in **Mail Accounts** verwijdert de externe inloggegevens niet stilzwijgend. De koppeling blijft vermeld onder **Remaining app passwords** op de instancepagina, zodat deze expliciet kan worden ingetrokken.

### Een instance bewerken {#editing-an-instance}

Open **Mailcow instances** en klik op **Edit**. Het veld voor de API-sleutel is altijd leeg — de opgeslagen sleutel wordt nooit weergegeven. Laat het leeg om de huidige sleutel te behouden, of voer een nieuwe in om deze te vervangen. Gewijzigde verbindingsgegevens worden tegen Mailcow gecontroleerd voordat ze worden opgeslagen.

### Een instance verwijderen {#deleting-an-instance}

Open **Mailcow instances** en klik op **Delete**.

Het verwijderen van een instance verwijdert de versleutelde API-sleutel en de Postbus-koppelingen. **Archiefaccounts en hun gearchiveerde mail worden bewaard**, maar de accounts worden uitgeschakeld.

Verwijdering wordt **geweigerd zolang een beheerd app-wachtwoord nog actief is** in Mailcow: het verwijderen van de instance zou de API-sleutel verwijderen die nodig is om het in te trekken. Trek eerst alle beheerde app-wachtwoorden op de Postbuspagina in.

## Auditlogboek {#audit-logging}

Elke administratieve bewerking wordt geschreven naar het toegangslogboek met de uitvoerende gebruiker:

- het verbinden van een instance,
- het bijwerken van een instance,
- het verwijderen van een instance,
- het activeren van Postboxen (met het aantal aangemaakt en het aantal mislukt),
- het intrekken van een app-wachtwoord.

## Postbuslimiet {#mailbox-limit}

Een enkele activatieronde kan maximaal `Mailcow__MaxSelectedMailboxes` Postboxen aanmaken (standaard `1000`). Dit beschermt tegen onbedoelde massale aanmaak en overmatige belasting van de Mailcow API en IMAP. Voer de operatie voor grotere instances meerdere keren uit.

Elke Postbus vereist meerdere API-roundtrips plus een IMAP-verificatie, en deze worden sequentieel verwerkt. Het aanmaken van vele honderden Postboxen in één verzoek kan de time-outs van de reverse-proxy of browser overschrijden; verdeel de selectie in batches.

## Beveiligingsnotities {#security-notes}

- De API-sleutel blijft aan de serverzijde. Het wordt verzonden als een `X-API-Key` header, wordt uit logs verwijderd en nooit teruggegeven aan de browser.
- De app-password reacties van Mailcow kunnen plaintext wachtwoorden echoën in hun `log` velden. Mail Archiver logt opzettelijk geen response bodies en geeft API-fouten niet door aan de browser; fouten worden gerapporteerd als lokale berichten.
- Een HTTP 200 reactie wordt niet als succes behandeld: wijzigingen vereisen de Mailcow per-resultaat `type: success`.
- IMAP-machtiging is niet inherent alleen-lezen. Aangemaakte accounts hebben geen serverzijde verwijderingsbeleid geconfigureerd, en administrators behouden de controle over de accountinstellingen. Deze functie archiveert berichten, geen Mailcow-configuratie of Back-ups.

### Hostnames en private netwerken {#hostnames-and-private-networks}

De Mailcow URL en de IMAP host worden door een administrator ingevoerd en kunnen verwijzen naar een private adresruimte. Dit is opzettelijk en wordt ondersteund: Mail Archiver wordt meestal naast de mailserver geïnstalleerd, dus `mail.example.lan`, `192.168.10.20` of `10.0.0.5` zijn normale waarden. **Geen enkel privaat, loopback of link-local adres wordt geblokkeerd.**

De vertrouwensgrens is de administrator-rol, niet het netwerkadres. `Mail Accounts` is beschikbaar voor zelfbeheerders, die mogelijk al een willekeurige IMAP-server configureren voor hun eigen Accounts; het Mailcow-gedeelte is beperkt tot administrators, en een administrator heeft al onbeperkte toegang tot elk archiefaccount in de installatie. Het beperken van de IMAP host zou daarom geen machtigheidsgrens sluiten, terwijl het de beoogde on-premise implementatie zou breken.

Twee beperkingen blijven van kracht:

- De Mailcow URL moet een kale HTTPS origin zijn. Paden, query strings, gebruikersinformatie en omleidingen worden geweigerd, zodat de API-sleutel niet naar een andere origin kan worden doorgestuurd.
- De IMAP host moet een hostname zijn zonder schema of poort, en het aanmaken verbindt altijd met TLS (`SslOnConnect`).

Als u wilt beperken welke hosts Mail Archiver kan bereiken, dwing dit dan af op de netwerklayer — egress firewall-regels of een DNS-beleid voor de container — in plaats van te vertrouwen op inputvalidatie in de applicatie.

## Probleemoplossing {#troubleshooting}

| Symptoom | Oorzaak en oplossing |
| --- | --- |
| *Encryption required* bij verbinden of activeren | `Security__CredentialEncryptionKey` is niet ingesteld of ongeldig. Configureer een base64-gecodeerde 32-byte sleutel en start opnieuw op. |
| *HTTPS origin required* | De URL bevat een pad, query string of ingebedde inloggegevens. Voer de kale origin in, bijv. `https://mail.example.com`. |
| *IMAP host invalid* | De IMAP host moet een hostname zijn zonder schema of poort. |
| *Instance already connected* | Er bestaat al een andere instance met dezelfde URL. Bewerk die instance in plaats daarvan. |
| *API request failed (401/403)* | De API-sleutel mist de vereiste machtigingen, of de IP-beperking bevat de Mail Archiver host niet. |
| *API request rejected* | Mailcow heeft een `error`/`danger`/`warning` regel geretourneerd. Controleer de Mailcow-logs; Mail Archiver geeft geen response bodies door. |
| *IMAP verification failed* | Verkeerde IMAP host of poort, een onbetrouwbaar certificaat, of de app-password functie is uitgeschakeld in Mailcow. |
| *IMAP account exists* | Een onafhankelijk IMAP-account archiveert dit adres al. Gebruik dat account in plaats van een tweede archief aan te maken. |
| *Revoke previous password* | Het archiefaccount voor deze Postbus is verwijderd. Trek eerst het resterende app-wachtwoord in en activeer dan opnieuw. |
| Postbus blijft **Pending** | De Mailcow API is verlopen (timeout). Herlaad de pagina en activeer de Postbus opnieuw; er wordt geen dubbel app-wachtwoord aangemaakt. |
| Verwijderen wordt geweigerd | Beheerde app-wachtwoorden zijn nog actief. Trek deze eerst in op de Postbuspagina. |

## Gebruikte API endpoints {#api-endpoints-used}

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## Database wijzigingen {#database-changes}

De migratie voegt alleen de tabellen `MailcowInstances` en `MailcowMailboxes` toe, samen met hun indexen en foreign keys. Bestaande Postbus-tabellen en gearchiveerde data blijven behouden. Het verwijderen van een archiefaccount zet de `MailAccountId` van de koppeling op `NULL`, zodat de externe inloggegevens later nog kunnen worden ingetrokken.
