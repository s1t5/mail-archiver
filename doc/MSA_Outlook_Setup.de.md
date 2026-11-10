# ☁️ Einrichtung eines persönlichen Microsoft-Kontos (Outlook.com / M365 Family) {#personal-microsoft-account-outlookcom-m365-family-setup}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Diese Anleitung erklärt, wie Sie E-Mails von einem **persönlichen Microsoft-Konto** (Outlook.com, Hotmail, live.com oder Microsoft 365 Family) in Mail Archiver archivieren.

Im Gegensatz zu organisatorischen Microsoft 365 Konten (die den Client-Credentials-Flow verwenden — siehe [Azure App Registration für M365](AZURE_APP_REGISTRATION_M365.md)) verwenden persönliche Konten **OAuth2 mit dem Device Code Flow (RFC 8628)**. Microsoft hat Basic Auth und App-Passwörter für persönliche Konten abgeschafft, daher ist OAuth2 erforderlich.

Mail Archiver wird mit einer **vorregistrierten, gemeinsamen Client ID** ausgeliefert, sodass die meisten Benutzer **keine** eigene Azure App Registration erstellen müssen. Power-User können dennoch ihre eigene App registrieren (siehe Option B unten).

## 📚 Inhaltsverzeichnis {#table-of-contents}

1. [Option A: Standard-Setup (Empfohlen)](#option-a-standard-setup-recommended)
2. [Option B: Eigene App-Registrierung (Power-User)](#option-b-custom-app-registration-power-users)
3. [Token-Aktualisierung & Entzug](#token-refresh--revocation)
4. [Wichtige Hinweise](#important-notes)
5. [Fehlerbehebung](#troubleshooting)

---

## 🚀 Option A: Standard-Setup (Empfohlen) {#option-a-standard-setup-recommended}

Verwenden Sie diese Option, wenn Ihre Mail Archiver Instanz eine standardmäßige Client ID konfiguriert hat (der Standard für offizielle Docker-Images). Ein Zugriff auf das Azure Portal ist nicht erforderlich.

### 🛠️ Voraussetzungen {#prerequisites}

- Ein persönliches Microsoft-Konto (Outlook.com, live.com oder M365 Family)
- Ein Gerät mit einem Browser (Smartphone, Laptop oder Tablet), um die Autorisierung abzuschließen

### 📝 Schritte {#steps}

1. Melden Sie sich in Ihrer Mail Archiver Anwendung an.
2. Navigieren Sie zu **Mail Accounts** > **Create**.
3. Füllen Sie die folgenden Felder aus:
   - **Name**: Beschreibender Name für das Konto (z. B. "Personal Outlook")
   - **Email Address**: Ihre persönliche Microsoft-E-Mail-Adresse (z. B. `you@outlook.com`)
   - **Provider**: Wählen Sie **Microsoft Personal**
4. Es werden keine Felder für Client ID oder Client Secret angezeigt — Mail Archiver verwendet automatisch die integrierte Standardregistrierung.
5. Klicken Sie auf **Create**.
6. Die Autorisierung mit dem Device Code öffnet sich automatisch. Ein kurzer Code (z. B. `ABCD-EFGH`) wird zusammen mit einem Link zu `https://microsoft.com/devicelogin` (oder der angezeigten Verifizierungs-URL) angezeigt.
7. Öffnen Sie diese URL auf **jedem beliebigen Gerät** mit einem Browser und melden Sie sich mit Ihrem persönlichen Microsoft-Konto an.
8. Geben Sie den Code aus Schritt 6 ein.
9. Ein Zustimmungsbildschirm erscheint und zeigt an, dass **"Mail-Archiver"** den IMAP-Zugriff anfordert. Überprüfen und bestätigen Sie dies.
10. Die Mail Archiver Seite erkennt den Erfolg automatisch und leitet zur Ansicht der Kontobearbeitung weiter.
11. Fertig — die Synchronisierung startet automatisch gemäß den Kontoeinstellungen.

> ⚠️ **Warnung vor nicht verifiziertem Herausgeber**: Der Zustimmungsbildschirm kann eine Warnung anzeigen, dass "Mail-Archiver" ein nicht verifizierter Herausgeber ist. Dies ist zu erwarten — die Verifizierung des Herausgebers erfordert eine geschäftliche Identität (Microsoft Partner Network Konto) und ist für die Funktion des Ablaufs nicht erforderlich. Die App funktioniert auch ohne diese Verifizierung normal.

---

## 🔧 Option B: Eigene App-Registrierung (Power-User) {#option-b-custom-app-registration-power-users}

Verwenden Sie diese Option, wenn Sie nicht die gemeinsam genutzte Standard-Client-ID verwenden möchten (z. B. aus Compliance-Gründen oder wenn Sie die volle Kontrolle über die App-Registrierung haben möchten).

### 🛠️ Voraussetzungen {#prerequisites}

- Ein Microsoft-Konto (persönlich oder geschäftlich/Schule), das Zugriff auf das Azure Portal hat
- Grundlegende Kenntnisse des Azure/Entra Portals

### 🚀 App-Registrierung erstellen {#create-app-registration}

1. Navigieren Sie zum [Microsoft Entra Admin Center](https://entra.microsoft.com) oder zum [Azure Portal](https://portal.azure.com).
2. Melden Sie sich mit Ihrem Microsoft-Konto an.
3. Wählen Sie im linken Navigationsbereich **App registrations** (unter Identity > Applications).
4. Klicken Sie auf **+ New registration**.
5. Füllen Sie die folgenden Details aus:
   - **Name**: Geben Sie einen beschreibenden Namen ein (z. B. "Mail Archiver MSA")
   - **Supported account types**: Wählen Sie **Accounts in any organizational directory and personal Microsoft accounts**
     - Dies ist die Multi-Tenant-Option, die sowohl persönliche als auch organisatorische Konten unterstützt.
   - **Redirect URI**: Lassen Sie dieses Feld leer (nicht benötigt für den Device Code Flow).
6. Klicken Sie auf **Register**.
7. Notieren Sie sich die **Application (client) ID** von der Seite **Overview**.

### 🔐 Public Client Flows aktivieren {#enable-public-client-flows}

1. Navigieren Sie in Ihrer App-Registrierung im linken Menü zu **Authentication**.
2. Suchen Sie unten nach **Allow public client flows** und setzen Sie es auf **Yes**.
3. Klicken Sie auf **Save**.

> ℹ️ Es müssen kein Client Secret, keine Redirect URI und keine API-Berechtigungen manuell konfiguriert werden — der Device Code Flow verwendet delegierte Berechtigungen, die zur Laufzeit über den `IMAP.AccessAsUser.All` Scope angefordert werden.

### 📧 Mail Archiver konfigurieren {#configure-mail-archiver}

Überschreiben des globalen Standards (wirkt sich auf alle MSA-Konten aus)

Setzen Sie die Client ID in Ihrer Umgebungsvariable:


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 Token-Aktualisierung & Entzug {#token-refresh-revocation}

### Automatische Token-Aktualisierung {#automatic-token-refresh}

Nach der initialen Autorisierung erhält Mail Archiver:
- Ein **Access Token** (kurzlebig, ~1 Stunde)
- Ein **Refresh Token** (langlebig, wird verwendet, um automatisch neue Access Tokens zu erhalten)

Mail Archiver aktualisiert das Access Token vor jedem Synchronisierungsdurchlauf automatisch. Sie müssen nicht erneut autorisieren, es sei denn, das Refresh Token läuft ab oder wird entzogen.

### Entzug des Zugriffs {#revoking-access}

Um den Zugriff von Mail Archiver auf Ihr persönliches Microsoft-Konto zu entziehen:

1. Besuchen Sie [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage)
2. Melden Sie sich mit Ihrem persönlichen Microsoft-Konto an.
3. Suchen Sie in der Liste nach **"Mail-Archiver"** (oder Ihrem benutzerdefinierten App-Namen).
4. Klicken Sie auf **Remove**, um den Zugriff zu entziehen.

Nach dem Entzug kann Mail Archiver nicht mehr auf das Konto zugreifen, bis Sie die Autorisierung erneut durchführen.

---

## ⚠️ Wichtige Hinweise {#important-notes}
- **Gemeinsame Client ID**: Wenn Sie die Standard-Client-ID verwenden, teilen sich alle Mail Archiver Benutzer auf allen Instanzen dieselbe Azure App Registration. Der Maintainer von Mail Archiver ist dafür verantwortlich, die Registrierung aktiv zu halten. Wenn Microsoft die gemeinsame Registrierung deaktiviert, müssten alle Benutzer auf eine eigene Registrierung (Option B) umsteigen, bis ein neues Release eine aktualisierte Client ID bereitstellt.

- **Organisatorische Konten**: Der Microsoft Personal Provider verwendet den `/common` Authority Endpoint und unterstützt sowohl persönliche als auch organisatorische (Arbeit/Schule) Konten. Verwenden Sie für organisatorische Konten, die einen Client-Credentials-Zugriff erfordern (App-only, kein Benutzer-Login), stattdessen den **M365** Provider.

---

## 🔍 Fehlerbehebung {#troubleshooting}

### Ordner sind auf outlook.com sichtbar, fehlen aber in Mail Archiver {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**Symptom**: Einige Ordner existieren im Postfach (sichtbar auf outlook.com oder in Outlook), erscheinen aber nie in Mail Archiver — weder in der Ordnerliste auf der Kontoeinstellungsseite noch während der Synchronisierung. Es wird kein Fehler protokolliert.

**Ursache (Serverseitig)**: Ordner, die durch **Drittanbieter-Clients über EWS** erstellt wurden — was outlook.com Konten als Exchange-Konten statt als IMAP einrichtet — können eine falsche `PR_CONTAINER_CLASS` Eigenschaft haben (z. B. `IPF.Imap` statt `IPF.Note`). Outlook.com **filtert diese Ordner dann vollständig aus der IMAP `LIST` Response heraus**, obwohl sie in der Web-UI vollständig sichtbar sind. Dies ist ein Verhalten auf Microsoft-Seite und kein Bug in Mail Archiver — dasselbe Phänomen ist für die Microsoft Graph API dokumentiert, wo Ordner mit einer nicht-`IPF.Note` Container-Klasse in `mailFolders` Listen fehlen, bis die Eigenschaft korrigiert wird:

Mail Archiver fragt die Ordnerliste aus drei Quellen ab (rekursives `LIST`, Traversierung der Kinder jedes Ordners pro Ebene und `LSUB`) und führt die Ergebnisse zusammen. Wenn der Server einen Ordner über keine dieser Quellen meldet, kann kein IMAP-Client ihn sehen.

**Workaround**: Bearbeiten Sie die betroffenen Ordner einmal über die outlook.com Web-UI, damit Microsoft die Container-Klasse korrigiert:

1. Öffnen Sie <https://outlook.com> und suchen Sie den fehlenden Ordner.
2. Benennen Sie ihn um (beliebiger temporärer Name) und benennen Sie ihn wieder zurück — oder erstellen Sie einen neuen Ordner und verschieben Sie die Mails hinein.
3. Wiederholen Sie dies für jeden betroffenen Ordner.
4. Starten Sie eine **Full Resync** (Vollständige Synchronisierung) des Kontos über die Kontodetails-Seite in Mail Archiver. Eine vollständige Synchronisierung ist erforderlich, da inkrementelle Synchronisierungen nur Mails abrufen, die neuer als das letzte Synchronisierungsdatum des Kontos sind — Mails in Ordnern, die nie entdeckt wurden, würden sonst unarchiviert bleiben.


*Diese Anleitung ist aktuell (Stand 2026). Microsoft aktualisiert regelmäßig ihre Dienste und Benutzeroberflächen, daher können einige Schritte abweichen. Informationen finden Sie in der [Microsoft Identity Platform Dokumentation](https://learn.microsoft.com/en-us/entra/identity-platform/).*
