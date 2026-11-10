# ☁️ Azure App Registration and Retention Policy Guide for Mail Archiver {#azure-app-registration-and-retention-policy-guide-for-mail-archiver}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden bietet umfassende Anweisungen zur Einrichtung der Azure App Registration für den E-Mail-Abruf und zur Konfiguration von Aufbewahrungsrichtlinien in der Mail Archiver Anwendung.

## 📚 Inhaltsverzeichnis {#table-of-contents}

1. [Übersicht](#overview)
2. [Azure App Registration für den E-Mail-Abruf](#azure-app-registration-for-email-retrieval)
   - [Voraussetzungen](#prerequisites)
   - [App Registration erstellen](#create-app-registration)
   - [Erforderliche API-Berechtigungen festlegen](#set-required-api-permissions)
   - [Client Secret generieren](#generate-client-secret)
3. [Mail Archiver M365 Konto konfigurieren](#configure-mail-archiver-m365-account)
4. [Einrichtung der Aufbewahrungsrichtlinie](#retention-policy-setup)
   - [Aufbewahrungsrichtlinie konfigurieren](#configure-retention-policy)

## 🌐 Übersicht {#overview}

Die Mail Archiver Anwendung unterstützt Microsoft 365 (M365) Konten unter Verwendung des OAuth2 Client Credentials Flow für den E-Mail-Abruf. Zusätzlich bietet sie eine Aufbewahrungsrichtlinie-Funktionalität, um E-Mails nach einem festgelegten Zeitraum automatisch vom Mail-Server zu löschen, was die Verwaltung des Speicherplatzes unterstützt und gleichzeitig ein vollständiges Archiv sicherstellt.

## ☁️ Azure App Registration für den E-Mail-Abruf {#azure-app-registration-for-email-retrieval}

### 🛠️ Voraussetzungen {#prerequisites}

- Administrativer Zugriff auf Microsoft Entra ID (Azure AD)
- Ein Microsoft 365 Tenant mit Exchange Online Lizenzen

### 🚀 App Registration erstellen {#create-app-registration}

1. Navigieren Sie zum [Microsoft Entra Admin Center](https://entra.microsoft.com)
2. Melden Sie sich mit Ihrem Administrator-Konto an
3. Wählen Sie in der linken Navigationsleiste **App registrations**
4. Klicken Sie oben auf der Seite **App registrations** auf **+ New registration**
5. Füllen Sie die folgenden Details aus:
   - **Name**: Geben Sie einen beschreibenden Namen ein (z. B. "Mail Archiver M365 Provider")
   - **Supported account types**: Wählen Sie **Accounts in this organizational directory only** (Single Tenant)
     - Dies ist erforderlich für den Client Credentials Flow, der von Mail Archiver verwendet wird
   - **Redirect URI**: Lassen Sie dieses Feld leer (nicht benötigt für den Client Credentials Flow)
6. Klicken Sie auf **Register**

**Wichtig**: Notieren Sie sich die folgenden Werte von der Seite **Overview**:
- **Application (client) ID** - Sie benötigen diese als `ClientId` in Mail Archiver
- **Directory (tenant) ID** - Sie benötigen diese als `TenantId` in Mail Archiver

### 🔐 Erforderliche API-Berechtigungen festlegen {#set-required-api-permissions}

1. Navigieren Sie in der linken Menüführung zu **API permissions**
2. Klicken Sie auf **+ Add a permission**
3. Wählen Sie **Microsoft Graph**
4. Wählen Sie **Application permissions** (erforderlich für den Client Credentials Flow von Mail Archiver)
5. Fügen Sie die folgenden Berechtigungen hinzu:
   - **Mail.Read** - E-Mails in allen Postfächern lesen
   - **Mail.ReadWrite** - E-Mails in allen Postfächern lesen und schreiben (auch für die Wiederherstellung und Löschung)
   - **User.Read.All** - Vollständige Profile aller Benutzer lesen (erforderlich für den Microsoft 365 Tenant Postfach-Import; stellt zudem `assignedPlans` für die Exchange-Lizenzfilterung bereit)

6. Klicken Sie auf **Add permissions**
7. **KRITISCH**: Klicken Sie auf **Grant admin consent for [Your Organization]**
8. Bestätigen Sie mit einem Klick auf **Yes**

**Hinweis**: Application Permissions sind erforderlich, da Mail Archiver den Client Credentials Flow verwendet, um auf Postfächer ohne Benutzerinteraktion zuzugreifen.

### 🔑 Client Secret generieren {#generate-client-secret}

1. Navigieren Sie im linken Menü zu **Certificates & secrets**
2. Klicken Sie unter **Client secrets** auf **+ New client secret**
3. Geben Sie eine Beschreibung an (z. B. "Mail Archiver Secret")
4. Wählen Sie einen Ablaufzeitraum aus
5. Klicken Sie auf **Add**
6. **Wichtig**: Kopieren Sie den **Value** sofort und speichern Sie ihn sicher. Dieses Secret wird nicht erneut angezeigt.

## 📧 Mail Archiver M365 Konto konfigurieren {#configure-mail-archiver-m365-account}

Nach Abschluss der App Registration müssen Sie ein M365 E-Mail-Konto in Mail Archiver mit den folgenden Werten konfigurieren:

### 📋 Erforderliche Werte aus der App Registration: {#required-values-from-app-registration}

1. **Client ID**: Kopieren Sie die **Application (client) ID** von der **Overview** Seite der App Registration
2. **Client Secret**: Kopieren Sie den **Value**, den Sie beim Erstellen des Client Secrets gespeichert haben
3. **Tenant ID**: Kopieren Sie die **Directory (tenant) ID** von der **Overview** Seite der App Registration

### 🚀 Erstellen eines M365 Kontos in Mail Archiver: {#creating-m365-account-in-mail-archiver}

1. Melden Sie sich in Ihrer Mail Archiver Anwendung an
2. Navigieren Sie zu **Mail Accounts** > **Create**
3. Füllen Sie die folgenden Felder aus:
   - **Name**: Beschreibender Name für das Konto (z. B. "Sales Team M365")
   - **Email Address**: Die Ziel-E-Mail-Adresse des Postfachs, das archiviert werden soll
   - **Provider**: Wählen Sie **M365**
   - **Client ID**: Geben Sie die Application (client) ID Ihrer App Registration ein
   - **Client Secret**: Geben Sie den gespeicherten Client Secret Wert ein
   - **Tenant ID**: Geben Sie die Directory (tenant) ID Ihrer App Registration ein

4. Klicken Sie auf **Create**

### ⚠️ Wichtige Hinweise: {#important-notes}

- Das Feld **Email Address** muss die tatsächliche E-Mail-Adresse des Postfachs enthalten, das beim Erstellen eines einzelnen M365 Kontos archiviert werden soll.
- Die App Registration muss über die Berechtigung verfügen, auf das angegebene Postfach zuzugreifen.
- Um Konten für alle oder ausgewählte Postfächer im Tenant gleichzeitig zu erstellen, verwenden Sie den Leitfaden [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md).


## 🗑️ Einrichtung der Aufbewahrungsrichtlinie {#retention-policy-setup}

Die Mail Archiver Anwendung bietet eine Aufbewahrungsrichtlinie-Funktionalität, um E-Mails nach der erfolgreichen Archivierung automatisch vom IMAP-Server zu löschen. Dies hilft, den Speicherplatz auf dem IMAP-Server zu verwalten und gleichzeitig ein vollständiges Archiv zu erhalten.

### 🛠️ Aufbewahrungsrichtlinie konfigurieren {#configure-retention-policy}

1. Navigieren Sie in der Mail Archiver Anwendung zu **Mail Accounts**
2. Erstellen Sie entweder ein neues M365 Konto oder bearbeiten Sie ein bestehendes
3. Suchen Sie im Konfigurationsformular des Kontos das Feld **Delete After Days**
4. Geben Sie die Anzahl der Tage ein, nach denen archivierte E-Mails vom Server gelöscht werden sollen:
   - Geben Sie beispielsweise `30` ein, um E-Mails nach 30 Tagen zu löschen
   - Lassen Sie das Feld leer, um die automatische Löschung zu deaktivieren
5. Speichern Sie die Kontokonfiguration


## ℹ️ Informationen zu migrierten Postfächern {#ℹ-information-regarding-migrated-mailboxes}

### 📁 Ordner nach der Migration nicht sichtbar {#folders-not-visible-after-migration}

Wenn Sie Ihr Postfach von einem anderen E-Mail-Anbieter zu Microsoft 365 migriert haben, werden möglicherweise einige Ordner nicht in der Ordnerliste angezeigt.

#### 🔍 Hintergrund {#background}

Mail Archiver ruft Ordnerinformationen aus M365 Postfächern über die Microsoft Graph API ab. Die Anwendung fordert korrekt alle verfügbaren Ordner (einschließlich versteckter Ordner) an und durchläuft die vollständige Ordnerhierarchie.

Die Microsoft Graph API gibt jedoch nur Ordner zurück, bei denen `PR_CONTAINER_CLASS` auf `IPF.Note` gesetzt ist. Während der Migration von Postfächern von anderen E-Mail-Systemen können Ordnern eine andere Container-Klasse wie `IPF.Imap` zugewiesen werden. Diese Ordner werden über die Folder-Listing-Endpunkte der Graph API nicht exponiert.

Dieses Verhalten betrifft alle Anwendungen, die diese API für den Abruf von Ordnern nutzen.

#### 🔧 Workaround-Optionen {#workaround-options}

**Option 1: MFCMAPI Tool verwenden (Empfohlen)**

1. Laden Sie [MFCMAPI](https://github.com/Microsoft/mfcmapi/) aus dem offiziellen Microsoft-Repository herunter
2. Starten Sie MFCMAPI und verbinden Sie sich mit Ihrem Postfach
3. Navigieren Sie zu dem fehlenden Ordner
4. Ändern Sie die Eigenschaft `PR_CONTAINER_CLASS` von `IPF.Imap` zu `IPF.Note`
5. Starten Sie Outlook neu und laden Sie die Ordner in Mail Archiver neu

**Option 2: Ordner in Outlook neu erstellen**

1. Erstellen Sie neue Ordner direkt in Outlook (diese werden die korrekte `IPF.Note` Klasse haben)
2. Verschieben Sie alle E-Mails aus den migrierten Ordnern in die neuen Ordner
3. Löschen Sie die alten migrierten Ordner bei Bedarf

**Option 3: PowerShell Script (Für Massenänderungen)**

Für Postfächer mit vielen migrierten Ordnern können Sie PowerShell mit dem Exchange Online Modul verwenden, um die Container-Klasse für mehrere Ordner gleichzeitig zu ändern. Kontaktieren Sie Ihren Exchange-Administrator für Unterstützung bei diesem Vorgehen.


---

**Hinweis**: Dieser Leitfaden ist aktuell (Stand 2025). Microsoft aktualisiert seine Dienste regelmäßig, daher können einige UI-Elemente abweichen. Beziehen Sie sich für die aktuellsten Informationen immer auf die neueste Microsoft-Dokumentation.
