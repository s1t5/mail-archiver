# Microsoft 365 Tenant Postfach-Import {#microsoft-365-tenant-mailbox-import}

[← Zurück zum Dokumentationsindex](index.md)

## Übersicht {#overview}

Mail Archiver kann über ein einziges Erstellungsformular Microsoft 365 Mail-Konten für mehr als ein Postfach im selben Tenant erstellen. Dies ist nützlich beim Onboarding eines Kunden oder einer Organisation, bei der alle oder eine ausgewählte Teilmenge der Microsoft 365 Postfächer archiviert werden sollen.

Der Tenant-Import verwendet die im Formular konfigurierten Microsoft Graph Application Credentials und erstellt ein Mail Archiver Mail-Konto pro importiertem Postfach.

## Voraussetzungen {#prerequisites}

Bevor Sie den Tenant-Import verwenden, schließen Sie die Einrichtung der Microsoft 365 App-Registrierung ab, die im [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md) beschrieben wird.

Sie benötigen diese Werte aus Microsoft Entra ID:

- **Client ID** / Application ID
- **Client Secret**
- **Tenant ID** / Directory ID

Die App-Registrierung muss über Microsoft Graph **application permissions** verfügen, die es dem Mail Archiver ermöglichen, die Tenant-Postfächer zu lesen. Zusätzlich zu den Berechtigungen, die für die reguläre M365 Konto-Archivierung erforderlich sind, ist für den Tenant-Import die folgende Berechtigung erforderlich:

- **User.Read.All** – erforderlich, um Tenant-Benutzer und deren Postfachadressen aufzulisten.

Der Tenant-Import wird im Erstellungsformular als Anbieter **Microsoft 365 (tenant)** ausgewählt.

### Filterung {#filtering}

Der Tenant-Import und das Tenant Management schließen automatisch Folgendes aus:
- **Gastkonten** (`userType == "Guest"`) — extern eingeladene Benutzer ohne eigene Postfächer im Tenant.
- **Benutzer ohne Exchange Online-Lizenz** — es werden nur Benutzer mit einem aktiven Exchange-Serviceplan in ihrem `assignedPlans` aufgelistet. Dies verhindert das Importieren von Konten, die nicht archiviert werden können.

## Import-Modi {#import-modes}

Der Tenant-Import unterstützt zwei Modi:

### Alle aufgeführten Postfächer importieren {#import-all-listed-mailboxes}

Verwenden Sie diesen Modus, wenn jedes von Microsoft Graph zurückgegebene Postfach als Mail Archiver Konto erstellt werden soll.

- Mail Archiver lädt die Tenant-Postfachliste von Microsoft Graph.
- Bereits existierende M365-Mail-Konten werden übersprungen.
- Wenn **Skip disabled mailboxes** aktiviert ist, werden deaktivierte Benutzer nicht in die Postfachliste aufgenommen.

### Nur ausgewählte Postfächer importieren {#import-selected-mailboxes-only}

Verwenden Sie diesen Modus, wenn nur bestimmte Postfächer im Tenant archiviert werden sollen.

- Laden Sie zuerst die Tenant-Postfachliste.
- Deaktivieren Sie **Import all listed mailboxes**.
- Wählen Sie die Postfächer aus, die importiert werden sollen.
- Senden Sie das Formular ab, um Konten nur für die ausgewählten Postfächer zu erstellen.

## Schritt-für-Schritt-Anleitung {#step-by-step-usage}

1. Melden Sie sich als Benutzer an, der Mail-Konten erstellen kann, bei Mail Archiver an.
2. Öffnen Sie **Mail Accounts** > **Create**.
3. Wählen Sie **Microsoft 365 (tenant)** als Anbieter aus.
4. Geben Sie die Microsoft 365-Anmeldedaten ein:
   - **Client ID**
   - **Client Secret**
   - **Tenant ID**
5. Geben Sie das Präfix für den **Account name** ein, das für die generierten Konten verwendet werden soll.
6. Wählen Sie aus, ob deaktivierte Postfächer übersprungen werden sollen.
7. Klicken Sie auf **Load mailboxes**, um die Postfächer des Tenants aufzulisten.
8. Wählen Sie das gewünschte Importverhalten:
   - Lassen Sie **Import all listed mailboxes** aktiviert, um alle aufgeführten Postfächer zu importieren.
   - Deaktivieren Sie diese Option und wählen Sie einzelne Postfächer aus, um nur bestimmte Postfächer zu importieren.
9. Klicken Sie auf **Save**.

Nach der Erstellung leitet Mail Archiver zurück zur Liste der Mail-Konten und zeigt an, wie viele Tenant-Konten importiert wurden und wie viele vorhandene Konten übersprungen wurden.

## Konto-Benennung {#account-naming}

Bei Tenant-Importen verwendet Mail Archiver den unter **Account name** eingegebenen Wert als Präfix für jedes erstellte Postfach-Konto.

Beispiel:

- Eingegebener Konto-Name: `Test GmbH`
- Tenant-Postfach: `mailbox@example.com`
- Erstellter Konto-Name: `Test GmbH - <mailbox@example.com>`

Dies erleichtert die Identifizierung importierter Konten, die zum selben Tenant oder Kunden gehören.

## Bestehende Konten {#existing-accounts}

Wenn ein Postfach bereits als Microsoft 365 Konto in Mail Archiver existiert, überspringt der Tenant-Import dieses, anstatt ein Duplikat zu erstellen. Die Erkennung bestehender Konten basiert auf der E-Mail-Adresse des Postfachs und dem M365-Anbieter.

Die Postfachliste kann zudem anzeigen, ob ein Postfach bereits existiert, bevor der Import gestartet wird.

## Deaktivierte Postfächer {#disabled-mailboxes}

Das Kontrollkästchen **Skip disabled mailboxes** steuert, ob deaktivierte Mandantennutzer von der Postfachliste ausgeschlossen werden sollen.

- Aktiviert: Es werden nur aktivierte Mandantennutzer aufgelistet und importiert.
- Deaktiviert: Deaktivierte Mandantennutzer können in der Liste erscheinen und können importiert werden, wenn sie ausgewählt werden oder wenn alle aufgelisteten Postfächer importiert werden.

## Tenant-Verwaltung {#tenant-management}

Zusätzlich zum Tenant-Import über das Erstellungsformular bietet Mail Archiver eine spezielle Seite für die **Tenant-Verwaltung** für Administratoren an. Diese ist über den Index der Mail-Konten über die Schaltfläche **Tenant-Verwaltung** (Gebäude-Symbol) neben jedem Microsoft 365 Konto erreichbar.

Die Tenant-Verwaltung ist nützlich, um Postfächer hinzuzufügen, die ursprünglich nicht importiert wurden – zum Beispiel, wenn nach dem initialen Onboarding neue Postfächer im Tenant erstellt wurden oder wenn Konten ursprünglich übersprungen wurden.

### Unterschiede zum Tenant-Import über das Erstellungsformular {#how-it-differs-from-the-create-form-tenant-import}

- Die Microsoft 365 Anmeldedaten (Client ID, Client Secret, Tenant ID) müssen **nicht erneut eingegeben werden**. Sie werden aus dem Quellkonto übernommen, bei dem die Schaltfläche angeklickt wurde.
- Die Liste der Tenant-Postfächer wird automatisch geladen, wenn die Seite geöffnet wird (keine Schaltfläche "Postfächer laden").
- Bereits importierte Postfächer werden mit einem "existiert bereits"-Badge angezeigt und können nicht erneut ausgewählt werden.
- Nur Administratoren haben Zugriff auf die Tenant-Verwaltung.

### Schritt-für-Schritt-Anleitung {#step-by-step-usage}

1. Melden Sie sich als Administrator bei Mail Archiver an.
2. Öffnen Sie **Mail Accounts**.
3. Suchen Sie das Microsoft 365 Konto, dessen Tenant Sie verwalten möchten, und klicken Sie auf die Schaltfläche **Tenant-Verwaltung** daneben.
4. Die Seite wird geladen und listet alle Postfächer auf, die Microsoft Graph für den Tenant des Quellkontos zurückgibt.
   - Postfächer, die bereits als M365 Konten in Mail Archiver existieren, sind mit einem "existiert bereits"-Badge markiert und können nicht ausgewählt werden.
   - Deaktivierte Tenant-Benutzer werden nicht aufgelistet; die Tenant-Verwaltung überspringt deaktivierte Postfächer immer.
5. Wählen Sie die Postfächer aus, die Sie hinzufügen möchten.
6. Geben Sie das Präfix für den **Account name** ein, das für die neuen Konten verwendet werden soll (das Benennungsschema ist `{prefix} - <{email}>`, identisch mit dem Tenant-Import über das Erstellungsformular).
7. Konfigurieren Sie optional **Delete After Days** und **Local Retention Days** für die neuen Konten.
8. Klicken Sie auf **Add selected mailboxes**.

Nach der Erstellung leitet Mail Archiver zur Liste der Mail-Konten weiter und zeigt an, wie viele Postfächer hinzugefügt wurden.

### Suche nach Postfächern {#searching-mailboxes}

Bei großen Tenants mit mehreren tausend Postfächern ermöglicht ein Suchfeld über der Postfachliste das Filtern nach Anzeigename oder E-Mail-Adresse. Der Filter ist nicht case-sensitive und wird sofort beim Tippen angewendet. **Select all** und **Clear selection** wirken sich nur auf die aktuell sichtbaren (gefilterten) Postfächer aus, sodass das Filtern ein sicherer Weg ist, um eine bestimmte Teilmenge auszuwählen, ohne verborgene Einträge zu beeinflussen.

### Umbenennen bestehender Konten {#renaming-existing-accounts}

Das Kontrollkästchen **Rename existing accounts** wendet das Benennungsschema `{prefix} - <{email}>` auf alle M365 Konten an, die dieselbe App-Registrierung (Client ID und Tenant ID) wie das Quellkonto teilen. Dies ist nützlich, um Kontonamen nach einem initialen Import zu normalisieren, bei dem die Namen inkonsistent waren.

Wenn dieses Kästchen aktiviert ist, benennt das Speichern des Formulars jedes passende Konto um – einschließlich des Quellkontos –, unabhängig davon, ob neue Postfächer für den Import ausgewählt wurden. Sie können diese Option allein verwenden (ohne neue Postfächer auszuwählen), um Namen zu normalisieren, ohne Konten hinzuzufügen.

### Postfach-Limit {#mailbox-limit}

Eine einzelne Tenant-Verwaltung-Operation kann maximal `TenantManagement__MaxSelectedMailboxes` Postfächer auf einmal hinzufügen (Standard `1000`). Dies schützt vor versehentlichen Massenimporten und übermäßiger Microsoft Graph / Datenbank-Last. Wenn Sie mehr Postfächer importieren müssen, führen Sie den Vorgang mehrmals aus. Weitere Informationen zur Anpassung dieses Limits finden Sie im [Setup Guide](Setup.md#-tenant-management-settings).

### Erforderliche Berechtigungen {#permissions-required}

Die Tenant-Verwaltung verwendet dieselben Microsoft Graph App-Berechtigungen wie der Tenant-Import über das Erstellungsformular. Die App-Registrierung des Quellkontos muss über Folgendes verfügen:

- Die Berechtigungen, die für die reguläre M365 Konto-Archivierung erforderlich sind, **und**
- **User.Read.All** — erforderlich, um Tenant-Benutzer und deren Postfachadressen aufzulisten.

Weitere Informationen zur Ersteinrichtung finden Sie im [Azure App Registration and Retention Policy Guide](AZURE_APP_REGISTRATION_M365.md).

### Audit-Protokollierung {#audit-logging}

Jeder Aufruf der Tenant-Verwaltung-Seite und jede Hinzufügen-Operation wird im Zugriffsprotokoll aufgezeichnet, einschließlich der Anzahl der aufgelisteten oder hinzugefügten Postfächer und des verwendeten Quellkontos.
