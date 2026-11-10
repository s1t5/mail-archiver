# ☁️ Einrichtung eines persönlichen Microsoft-Kontos (Outlook.com / M365 Family) {#personal-microsoft-account-outlookcom-m365-family-setup}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden erklärt, wie Sie E-Mails von einem **persönlichen Microsoft-Konto** (Outlook.com, Hotmail, live.com oder Microsoft 365 Family) in Mail Archiver archivieren.

Im Gegensatz zu organisatorischen Microsoft 365-Konten (die den Client-Credentials-Flow verwenden – siehe [Azure App Registration for M365](AZURE_APP_REGISTRATION_M365.md)) verwenden persönliche Konten **OAuth2 mit dem Device Code Flow (RFC 8628)**. Microsoft hat die Basic Auth und App-Passwörter für persönliche Konten eingestellt, daher ist OAuth2 erforderlich.

Mail Archiver wird mit einer **vorregistrierten gemeinsamen Client ID** ausgeliefert, sodass die meisten Benutzer **keine** eigene Azure App Registration erstellen müssen. Power-User können weiterhin ihre eigene App registrieren (siehe Option B unten).

## 📚 Inhaltsverzeichnis {#table-of-contents}

1. [Option A: Standard-Setup (Empfohlen)](#option-a-standard-setup-recommended)
2. [Option B: Benutzerdefinierte App-Registrierung (Power User)](#option-b-custom-app-registration-power-users)
3. [Token-Aktualisierung & Widerruf](#token-refresh--revocation)
4. [Wichtige Hinweise](#important-notes)
5. [Fehlerbehebung](#troubleshooting)

---

## 🚀 Option A: Standard-Setup (Empfohlen) {#option-a-standard-setup-recommended}

Verwenden Sie diese Option, wenn Ihre Mail Archiver Instanz eine Standard-Client-ID konfiguriert hat (der Standard für offizielle Docker-Images). Ein Zugriff auf das Azure Portal ist nicht erforderlich.

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
4. Es werden keine Felder für Client ID oder Client Secret angezeigt – Mail Archiver verwendet automatisch die integrierte Standardregistrierung.
5. Klicken Sie auf **Create**.
6. Die Seite für die Device-Code-Autorisierung öffnet sich automatisch. Ein kurzer Code (z. B. `ABCD-EFGH`) wird zusammen mit einem Link zu `https://microsoft.com/devicelogin` (oder der angezeigten Verifizierungs-URL) angezeigt.
7. Öffnen Sie diese URL auf **jedem beliebigen Gerät** mit einem Browser und melden Sie sich mit Ihrem persönlichen Microsoft-Konto an.
8. Geben Sie den Code aus Schritt 6 ein.
9. Ein Zustimmungsbildschirm erscheint, auf dem **"Mail-Archiver"** den Zugriff auf IMAP anfordert. Überprüfen und bestätigen Sie diesen.
10. Die Mail Archiver Seite erkennt den Erfolg automatisch und leitet zur Ansicht zur Kontobearbeitung weiter.
11. Fertig – die Synchronisierung startet automatisch gemäß den Kontoeinstellungen.

> ⚠️ **Warnung vor nicht verifiziertem Herausgeber**: Der Zustimmungsbildschirm kann eine Warnung anzeigen, dass "Mail-Archiver" ein nicht verifizierter Herausgeber ist. Dies ist normal – die Verifizierung des Herausgebers erfordert eine geschäftliche Identität (Microsoft Partner Network Konto) und ist nicht erforderlich, damit der Ablauf funktioniert. Die App funktioniert auch ohne diese Verifizierung normal.

---

## 🔧 Option B: Benutzerdefinierte App-Registrierung (Power User) {#option-b-custom-app-registration-power-users}

Verwenden Sie diese Option, wenn Sie nicht die gemeinsam genutzte Standard-Client-ID verwenden möchten (z. B. aus Compliance-Gründen oder wenn Sie die volle Kontrolle über die App-Registrierung haben möchten).

### 🛠️ Voraussetzungen {#prerequisites}

- Ein Microsoft-Konto (privat oder geschäftlich/schulisch), das Zugriff auf das Azure Portal hat
- Grundlegende Vertrautheit mit dem Azure/Entra-Portal

### 🚀 App-Registrierung erstellen {#create-app-registration}

1. Navigieren Sie zum [Microsoft Entra Admin Center](https://entra.microsoft.com) oder zum [Azure Portal](https://portal.azure.com).
2. Melden Sie sich mit Ihrem Microsoft-Konto an.
3. Wählen Sie im linken Navigationsbereich **App registrations** (unter Identity > Applications).
4. Klicken Sie auf **+ New registration**.
5. Füllen Sie die folgenden Details aus:
   - **Name**: Geben Sie einen beschreibenden Namen ein (z. B. "Mail Archiver MSA")
   - **Supported account types**: Wählen Sie **Accounts in any organizational directory and personal Microsoft accounts**
     - Dies ist die Multi-Tenant-Option und unterstützt sowohl private als auch organisatorische Konten.
   - **Redirect URI**: Lassen Sie dieses Feld leer (nicht erforderlich für den Device Code Flow).
6. Klicken Sie auf **Register**.
7. Notieren Sie sich die **Application (client) ID** auf der Seite **Overview**.

### 🔐 Public Client Flows aktivieren {#enable-public-client-flows}

1. Navigieren Sie in Ihrer App-Registrierung im linken Menü zu **Authentication**.
2. Suchen Sie unten den Punkt **Allow public client flows** und stellen Sie diesen auf **Yes**.
3. Klicken Sie auf **Save**.

> ℹ️ Es müssen kein Client-Secret, keine Redirect URI und keine API-Berechtigungen manuell konfiguriert werden — der Device Code Flow verwendet delegierte Berechtigungen, die zur Laufzeit über den `IMAP.AccessAsUser.All` Scope angefordert werden.

### 📧 Mail Archiver konfigurieren {#configure-mail-archiver}

Überschreiben Sie den globalen Standard (wirkt sich auf alle MSA-Konten aus)

Setzen Sie die Client ID in Ihrer Umgebungsvariable:


```yaml
environment:
  - MsaOAuth__DefaultClientId=your-client-id-here
```

---

## 🔄 Token-Aktualisierung & Widerruf {#token-refresh-revocation}

### Automatische Token-Aktualisierung {#automatic-token-refresh}

Nach der initialen Autorisierung erhält Mail Archiver:
- Ein **access token** (kurzlebig, ~1 Stunde)
- Ein **refresh token** (langlebig, wird verwendet, um automatisch neue access tokens zu erhalten)

Mail Archiver aktualisiert das access token automatisch vor jedem Synchronisierungslauf. Sie müssen nicht erneut autorisieren, es sei denn, das refresh token läuft ab oder wird widerrufen.

### Zugriff widerrufen {#revoking-access}

Um den Zugriff von Mail Archiver auf Ihr persönliches Microsoft-Konto zu widerrufen:

1. Besuchen Sie [https://account.live.com/consent/Manage](https://account.live.com/consent/Manage)
2. Melden Sie sich mit Ihrem persönlichen Microsoft-Konto an.
3. Suchen Sie in der Liste nach **"Mail-Archiver"** (oder Ihrem benutzerdefinierten App-Namen).
4. Klicken Sie auf **Entfernen**, um den Zugriff zu widerrufen.

Nach dem Widerruf kann Mail Archiver nicht mehr auf das Konto zugreifen, bis Sie die Autorisierung erneut durchführen.

---

## ⚠️ Wichtige Hinweise {#important-notes}
- **Gemeinsame Client ID**: Wenn die standardmäßige Client ID verwendet wird, teilen sich alle Mail Archiver-Benutzer auf allen Instanzen dieselbe Azure App Registration. Der Maintainer von Mail Archiver ist dafür verantwortlich, die Registrierung aktiv zu halten. Falls Microsoft die gemeinsame Registrierung deaktiviert, müssten alle Benutzer auf eine benutzerdefinierte Registrierung (Option B) umsteigen, bis ein neues Release eine aktualisierte Client ID bereitstellt.


- **Organisatorische Konten**: Der Microsoft Personal Provider verwendet den `/common` Authority-Endpunkt und unterstützt sowohl persönliche als auch organisatorische (Arbeits-/Schulkonto) Konten. Verwenden Sie für organisatorische Konten, die einen client-credentials-Zugriff erfordern (App-only, kein Benutzer-Login), stattdessen den **M365** Provider.

---

## 🔍 Fehlerbehebung {#troubleshooting}

### Ordner auf outlook.com sichtbar, aber in Mail Archiver nicht vorhanden {#folders-visible-on-outlookcom-but-missing-in-mail-archiver}

**Symptom**: Einige Ordner existieren im Postfach (sichtbar auf outlook.com oder in Outlook), aber sie erscheinen nie in Mail Archiver — weder in der Ordnerliste auf der Kontoeinstellungsseite noch während der Synchronisierung. Es wird kein Fehler protokolliert.

**Ursache (serverseitig)**: Ordner, die von **Drittanbieter-Clients über EWS** erstellt wurden — was outlook.com-Konten als Exchange-Konten statt als IMAP einrichtet — können eine falsche `PR_CONTAINER_CLASS`-Eigenschaft erhalten (z. B. `IPF.Imap` statt `IPF.Note`). Outlook.com **filtert diese Ordner dann vollständig aus der IMAP `LIST`-Antwort heraus**, obwohl sie in der Web-UI vollständig sichtbar sind. Dies ist ein Verhalten auf Microsoft-Seite und kein Fehler von Mail Archiver — dasselbe Phänomen ist für die Microsoft Graph API dokumentiert, wo Ordner mit einer nicht-`IPF.Note` Container-Klasse in `mailFolders`-Auflistungen fehlen, bis die Eigenschaft korrigiert wurde:

Mail Archiver fragt die Ordnerliste aus drei Quellen ab (rekursives `LIST`, Traversierung der Unterordner jedes Ordners auf jeder Ebene und `LSUB`) und führt die Ergebnisse zusammen. Wenn der Server keinen Ordner über eine dieser Quellen meldet, hat kein IMAP-Client die Möglichkeit, ihn zu sehen.

**Workaround**: Bearbeiten Sie die betroffenen Ordner einmal über die outlook.com Web-UI, damit Microsoft die Container-Klasse korrigiert:

1. Öffnen Sie <https://outlook.com> und suchen Sie einen fehlenden Ordner.
2. Benennen Sie ihn um (jeder beliebige temporäre Name) und benennen Sie ihn wieder zurück — oder erstellen Sie einen neuen Ordner und verschieben Sie die E-Mails dorthin.
3. Wiederholen Sie dies für jeden betroffenen Ordner.
4. Lösen Sie eine **Vollständige Synchronisierung** des Kontos von der Seite „Kontodetails“ in Mail Archiver aus. Eine vollständige Synchronisierung ist erforderlich, da inkrementelle Synchronisierungen nur E-Mails abrufen, die neuer als das letzte Synchronisierungsdatum des Kontos sind — E-Mails in Ordnern, die nie entdeckt wurden, würden ansonsten nicht archiviert werden.


*Dieser Leitfaden ist aktuell mit Stand 2026. Microsoft aktualisiert seine Dienste und die UI regelmäßig, daher können einige Schritte abweichen. Weitere Details finden Sie in der [Microsoft identity platform Dokumentation](https://learn.microsoft.com/en-us/entra/identity-platform/).*
