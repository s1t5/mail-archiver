# 📋 Access Log {#access-log}

[← Terug naar de Documentatie-index](index.md)

## 📋 Overzicht {#overview}

Deze gids legt de functionaliteit van het access log uit in de Mail Archiver-applicatie, waarmee het loggen en bekijken van gebruikersactiviteiten binnen het systeem mogelijk is. Deze functionaliteit is cruciaal voor monitoring, beveiliging en het oplossen van problemen.

## 📊 Wat er wordt gelogd {#what-is-logged}

De applicatie logt verschillende soorten gebruikersactiviteiten, waaronder:

1. **Login en Logout** - Logging van inlog- en uitlogbewerkingen
2. **Zoekacties** - Logging van zoekopdrachten naar e-mail binnen het systeem
3. **E-mail openen** - Logging wanneer gebruikers e-mailinhoud openen
4. **E-mail downloaden** - Logging wanneer gebruikers e-mails downloaden
5. **E-mail Herstel** - Logging wanneer gebruikers gearchiveerde e-mails herstellen
6. **Accountbeheer** - Logging van acties gerelateerd aan het Account
7. **Verwijderacties** - Logging van verwijderacties
8. **Audit Data Export** - Logging van runs voor de export van auditgegevens (start en resultaat, zie [Audit Data Export](AuditExport.md))

## 📝 Actietypes en beschrijvingen {#action-types-and-descriptions}

De gelogde acties zijn onderverdeeld in de volgende types:

### Login {#login}
- **Beschrijving**: Logt wanneer een gebruiker inlogt in het systeem
- **Details**: Gebruikersnaam, tijdstempel

### Logout {#logout}
- **Beschrijving**: Logt wanneer een gebruiker uitlogt uit het systeem
- **Details**: Gebruikersnaam, tijdstempel

### Search {#search}
- **Beschrijving**: Logt zoekopdrachten binnen het e-mailsysteem
- **Details**: Gebruikersnaam, tijdstempel, zoekparameters

### Open {#open}
- **Beschrijving**: Logt wanneer een gebruiker een e-mail opent
- **Details**: Gebruikersnaam, tijdstempel, e-mail ID, afzender, onderwerp

### Download {#download}
- **Beschrijving**: Logt wanneer gebruikers e-mails downloaden
- **Details**: Gebruikersnaam, tijdstempel, e-mail ID, afzender, onderwerp

### Restore {#restore}
- **Beschrijving**: Logt wanneer gebruikers gearchiveerde e-mails herstellen
- **Details**: Gebruikersnaam, tijdstempel, e-mail ID, afzender, onderwerp

### Account {#account}
- **Beschrijving**: Logt acties gerelateerd aan het Account (creatie, wijziging, etc.)
- **Details**: Gebruikersnaam, tijdstempel, account ID

### Deletion {#deletion}
- **Beschrijving**: Logt verwijderacties op e-mails of andere resources
- **Details**: Gebruikersnaam, tijdstempel, getroffen resource
- **Compliance opmerking**: Verwijderacties worden gelogd voor compliance-auditingdoeleinden

## 🔍 Filteropties {#filter-options}

De logpagina biedt de volgende filteropties:

1. **Datum** - Filteren op datumbereik (Van-Tot)
2. **Gebruikersnaam** - Filteren op een specifieke gebruiker (Alleen Admin)
3. **Actietype** - Filteren op een specifiek actietype

## 🖥️ Logweergave {#log-display}

Logs worden weergegeven in een tabelformaat met de volgende kolommen:

- **Timestamp**: Datum en tijd van de actie
- **Username**: Naam van de betrokken gebruiker
- **Type**: Type actie met kleurcodering
- **Information**: Aanvullende details over de actie

Voor mobiele apparaten worden logs weergegeven in kaartformaat.

## 🔐 Toegangsrechten {#access-permissions}

- **Niet-admin gebruikers**: Kunnen alleen hun eigen logs bekijken
- **Admin gebruikers**: Kunnen logs van alle gebruikers bekijken en filteren op gebruiker

## 📤 Audit Data Export {#audit-data-export}

Admins kunnen de speciale [Audit Data Export](AuditExport.md) pagina direct vanaf de Logs-pagina openen. Deze maakt tabelvormige massadata-pakketten (INDEX.XML + CSV) aan vanuit het Archief voor externe audittools. Elke exportrun schrijft twee access log-vermeldingen van het type **Audit Data Export** (start en resultaat), die worden vermeld en filterbaar zijn op de Logs-pagina, net als alle andere vermeldingen.

Zie de [Audit Data Export gids](AuditExport.md) voor details.
