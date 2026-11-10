# Konto-Import {#account-import}

[← Zurück zum Dokumentationsindex](index.md)

## Übersicht {#overview}

Mail Archiver unterstützt das gleichzeitige Importieren mehrerer E-Mail-Konten, sodass das Onboarding einer großen Anzahl von Postfächern nicht die individuelle Erstellung jedes Kontos erfordert.

Je nach Mail-Provider existieren zwei Wege für den Massenimport:

| Provider | Massenimport-Methode | Dokumentation |
|---|---|---|
| **Microsoft 365 (tenant)** | Tenant-Discovery via Microsoft Graph — eine App-Registrierung greift auf jedes Postfach im Tenant zu | [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) |
| **IMAP** | CSV-Massenimport — Hochladen einer CSV-Datei mit einer Zeile pro Postfach | Diese Seite (siehe unten) |

> **Hinweis:** Microsoft-Privatkonten (Outlook.com / M365 Family, Provider **MSA**) verwenden einen interaktiven Device-Code-Autorisierungsfluss und können nicht im Massenimport verwendet werden. Sie müssen einzeln erstellt und autorisiert werden. Siehe [MSA Outlook Setup](MSA_Outlook_Setup.md).

---

## IMAP CSV Massenimport {#imap-csv-bulk-import}

### Was es bewirkt {#what-it-does}

Der CSV-Massenimport ermöglicht es einem Administrator, durch das Hochladen einer CSV-Datei hunderte von IMAP-Mail-Konten in einem einzigen Vorgang zu erstellen. Jede Zeile in der Datei entspricht einem Postfach. Gemeinsame Servereinstellungen (IMAP-Host, Port, SSL) können einmal im Upload-Formular angegeben werden und gelten für jede Zeile, wobei optionale zeilenweise Überschreibungen möglich sind.

Der Import ist **nur für Administratoren** verfügbar. Er ist auf der Seite **Mail Accounts** über das Dropdown-Menü **Import** → **CSV Bulk Import** erreichbar.

### CSV-Dateiformat {#csv-file-format}

Die Datei muss eine UTF-8-kodierte CSV-Datei mit einer **Header-Zeile** und einem **Komma (`,`) als Trennzeichen** sein. Felder, die Kommas oder Anführungszeichen enthalten, müssen in doppelte Anführungszeichen (`"..."`) eingeschlossen werden.

#### Spalten {#columns}

| Spalte | Erforderlich | Beschreibung |
|---|---|---|
| `email` | Ja | Postfach-Adresse. Wird auch als standardmäßiger Login-Benutzername verwendet, wenn `username` nicht angegeben wird. |
| `password` | Ja | IMAP-Login-Passwort. Wird so gespeichert, wie es angegeben wurde – führende/folgende Leerzeichen bleiben erhalten. |
| `name` | Nein | Anzeigename für das Konto. Wenn weggelassen, wird der Standardwert `{prefix} - <{email}>` verwendet. |
| `username` | Nein | Login-Benutzername, falls dieser von der E-Mail-Adresse abweicht. |
| `imap_server` | Nein | Zeilenweiser IMAP-Host. Überschreibt den gemeinsamen Server aus dem Upload-Formular. |
| `imap_port` | Nein | Zeilenweiser IMAP-Port (1–65535). Überschreibt den gemeinsamen Port. |
| `use_ssl` | Nein | Zeilenweiser SSL-Umschalter (`true` oder `false`). Überschreibt die gemeinsame Einstellung. |

#### Beispiel {#example}

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- Zeile 1 verwendet den gemeinsamen Server/Port/SSL aus dem Upload-Formular.
- Zeile 2 legt einen benutzerdefinierten Benutzernamen fest, verwendet ansonsten aber die gemeinsamen Einstellungen.
- Zeile 3 überschreibt Server, Port und SSL.

Eine herunterladbare Beispieldatei ist auf der Importseite über die Schaltfläche **Download example CSV** verfügbar.

### Upload-Formular {#upload-form}

| Feld | Zweck |
|---|---|
| **CSV file** | Die hochzuladende Datei. |
| **IMAP server** | Gemeinsamer Host für alle Zeilen ohne eine `imap_server`-Überschreibung. |
| **IMAP port** | Gemeinsamer Port (Standard 993). |
| **Use SSL** | Gemeinsamer SSL-Umschalter (standardmäßig aktiviert). |
| **Name prefix** | Präfix für automatisch generierte Kontonamen. Standard: `IMAP`. |
| **Skip existing mailboxes** | Wenn aktiviert, werden Zeilen, deren E-Mail bereits als IMAP-Konto existiert, übersprungen (werden als "skipped" gezählt, nicht als Fehler). Wenn deaktiviert, werden sie als Fehler behandelt. |
| **Account enabled** | Ob die erstellten Konten für die Hintergrund-Synchronisierung aktiviert werden sollen. |
| **Delete After Days / Local Retention Days** | Optionale Aufbewahrungseinstellungen, die auf jedes erstellte Konto angewendet werden. Siehe [Retention Policies](RetentionPolicies.md). |

### Verarbeitung {#processing}

1. Die CSV-Datei wird analysiert und jede Zeile wird validiert (E-Mail-Format, Passwort vorhanden, Portbereich, SSL-Wert).
2. Doppelte E-Mails innerhalb der Datei werden entfernt (das erste Vorkommen gewinnt).
3. Bestehende IMAP-Konten mit derselben E-Mail-Adresse werden erkannt; je nach Einstellung des **Skip existing**-Schalters werden sie entweder übersprungen oder als Fehler gemeldet.
4. Alle neuen Konten werden in einem einzigen Datenbank-Batch eingefügt.
5. Ein Eintrag im Audit-Protokoll wird geschrieben, der den Durchlauf zusammenfasst.

Während des Imports wird kein IMAP-Verbindungstest durchgeführt – Konten werden sofort gespeichert. Authentifizierungsprobleme treten erst bei der ersten Hintergrund-Synchronisierung auf. Dies hält den Import selbst bei hunderten von Postfächern schnell.

### Ergebnisseite {#result-page}

Nach Abschluss des Imports zeigt eine Ergebnisseite drei Tabellen an:

- **Created** — E-Mail und Name jedes erstellten Kontos.
- **Skipped** — E-Mails, die Duplikate waren (innerhalb der Datei oder bereits vorhanden), wenn **Skip existing** aktiviert ist.
- **Failed** — Zeilen, die nicht verarbeitet werden konnten, mit der Zeilennummer und dem Grund (fehlende E-Mail, fehlendes Passwort, ungültiger Port, kein IMAP-Server usw.).

Ein Zusammenfassungsbanner zeigt die Anzahl an: *{created} created, {skipped} skipped, {failed} failed*.

### Limits {#limits}

Die folgenden Limits können über `appsettings.json` / Umgebungsvariablen konfiguriert werden (siehe [Setup Guide](Setup.md) → *CSV Import Settings*):

| Einstellung | Standard | Beschreibung |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | Maximale Anzahl verarbeiteter Zeilen pro Upload. |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | Maximale Größe der hochgeladenen Datei. |

### Sicherheitshinweis {#security-note}

IMAP-Passwörter werden in der Datenbank auf die gleiche Weise gespeichert wie bei einzeln erstellten IMAP-Konten. Beachten Sie, dass die CSV-Datei alle Postfach-Passwörter im Klartext enthält – löschen Sie diese nach einem erfolgreichen Import vom Client-Rechner.

---

## Microsoft 365 Tenant Import {#microsoft-365-tenant-import}

Für Microsoft 365 (Exchange Online) erfordert der Massenimport keine CSV-Datei. Eine einzige Azure AD App-Registrierung mit den richtigen Graph-Berechtigungen kann auf jedes Postfach im Tenant zugreifen, sodass Mail Archiver die Postfächer automatisch entdecken kann.

Siehe den speziellen Leitfaden: **[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**.
