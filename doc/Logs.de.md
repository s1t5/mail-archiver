# 📋 Access Log {#access-log}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden erklärt die Funktionalität des Audit-Protokolls in der Mail Archiver Anwendung, die das Protokollieren und Anzeigen von Benutzeraktivitäten innerhalb des Systems ermöglicht. Diese Funktionalität ist entscheidend für Überwachungs-, Sicherheits- und Debugging-Zwecke.

## 📊 Was protokolliert wird {#what-is-logged}

Die Anwendung protokolliert verschiedene Arten von Benutzeraktivitäten, einschließlich:

1. **Anmeldung und Abmeldung** – Protokollierung von Anmelde- und Abmeldevorgängen
2. **Suchvorgänge** – Protokollierung von E-Mail-Suchanfragen innerhalb des Systems
3. **Öffnen von E-Mails** – Protokollierung, wenn Benutzer E-Mail-Inhalte öffnen
4. **Herunterladen von E-Mails** – Protokollierung, wenn Benutzer E-Mails herunterladen
5. **Wiederherstellung von E-Mails** – Protokollierung, wenn Benutzer archivierte E-Mails wiederherstellen
6. **Kontoverwaltung** – Protokollierung von kontobezogenen Aktionen
7. **Löschvorgänge** – Protokollierung von Löschvorgängen
8. **Audit-Datenexport** – Protokollierung von Durchläufen des Audit-Datenexports (Start und Ergebnis, siehe [Audit Data Export](AuditExport.md))

## 📝 Aktionstypen und Beschreibungen {#action-types-and-descriptions}

Die protokollierten Aktionen werden in die folgenden Typen unterteilt:

### Login {#login}
- **Beschreibung**: Protokolliert, wenn sich ein Benutzer im System anmeldet
- **Details**: Benutzername, Zeitstempel

### Logout {#logout}
- **Beschreibung**: Protokolliert, wenn sich ein Benutzer vom System abmeldet
- **Details**: Benutzername, Zeitstempel

### Search {#search}
- **Beschreibung**: Protokolliert Suchanfragen innerhalb des E-Mail-Systems
- **Details**: Benutzername, Zeitstempel, Suchparameter

### Open {#open}
- **Beschreibung**: Protokolliert, wenn ein Benutzer eine E-Mail öffnet
- **Details**: Benutzername, Zeitstempel, E-Mail-ID, Absender, Betreff

### Download {#download}
- **Beschreibung**: Protokolliert, wenn Benutzer E-Mails herunterladen
- **Details**: Benutzername, Zeitstempel, E-Mail-ID, Absender, Betreff

### Restore {#restore}
- **Beschreibung**: Protokolliert, wenn Benutzer archivierte E-Mails wiederherstellen
- **Details**: Benutzername, Zeitstempel, E-Mail-ID, Absender, Betreff

### Account {#account}
- **Beschreibung**: Protokolliert kontobezogene Aktionen (Erstellung, Änderung usw.)
- **Details**: Benutzername, Zeitstempel, Konto-ID

### Deletion {#deletion}
- **Beschreibung**: Protokolliert Löschvorgänge bei E-Mails oder anderen Ressourcen
- **Details**: Benutzername, Zeitstempel, betroffene Ressource
- **Compliance-Hinweis**: Löschvorgänge werden zu Zwecken der Compliance-Prüfung protokolliert

## 🔍 Filteroptionen {#filter-options}

Die Protokollseite bietet die folgenden Filteroptionen:

1. **Datum** - Filtern nach Datumsbereich (Von-Bis)
2. **Benutzername** - Filtern nach einem bestimmten Benutzer (nur Admin)
3. **Aktionstyp** - Filtern nach einem bestimmten Aktionstyp

## 🖥️ Log-Anzeige {#log-display}

Protokolle werden in einem Tabellenformat mit den folgenden Spalten angezeigt:

- **Zeitstempel**: Datum und Uhrzeit der Aktion
- **Benutzername**: Name des beteiligten Benutzers
- **Typ**: Art der Aktion mit Farbcodierung
- **Information**: Zusätzliche Details zur Aktion

Auf Mobilgeräten werden Protokolle im Kartenformat angezeigt.

## 🔐 Zugriffsberechtigungen {#access-permissions}

- **Nicht-Admin-Benutzer**: Können nur ihre eigenen Protokolle einsehen
- **Admin-Benutzer**: Können Protokolle für alle Benutzer einsehen und nach Benutzer filtern

## 📤 Audit Data Export {#audit-data-export}

Admins können die dedizierte Seite [Audit Data Export](AuditExport.md) direkt von der Logs-Seite aus öffnen. Sie erstellt tabellarische Massendatenpakete (INDEX.XML + CSV) aus dem Archiv für externe Audit-Tools. Jeder Exportlauf schreibt zwei Einträge im Audit-Protokoll vom Typ **Audit Data Export** (Start und Ergebnis), die wie alle anderen Einträge auf der Logs-Seite aufgelistet und filterbar sind.

Details finden Sie im [Audit Data Export guide](AuditExport.md).
