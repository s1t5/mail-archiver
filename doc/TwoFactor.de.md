# 🔐 Zwei-Faktor-Authentisierung {#two-factor-authentication}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Die Zwei-Faktor-Authentisierung (2FA) fügt dem Login-Prozess einen zweiten Schritt hinzu: Nach Ihrem Passwort fragt Mail Archiver nach einem **6-stelligen Code**, der von einer Authenticator-App auf Ihrem Telefon generiert wird. Ein gestohlenes Passwort allein reicht dann nicht mehr aus, um auf das Archiv zuzugreifen.

Mail Archiver implementiert 2FA mittels **TOTP** (Time-based One-Time Password, RFC 6238) — demselben Standard, der von Google Authenticator, Microsoft Authenticator, Authy, 1Password, Bitwarden und anderen verwendet wird. Es ist kein externer Dienst involviert; das Geheimnis verlässt Ihre Instanz nie.

**2FA ist pro Benutzer optional (opt-in).** Sie wird von jedem Benutzer für sein eigenes Konto konfiguriert, nicht global durch einen Administrator.

## 🛠️ Voraussetzungen {#prerequisites}

- Eine TOTP-fähige Authenticator-App auf Ihrem Telefon oder Desktop
- Zugriff auf Ihr eigenes Konto (Sie müssen angemeldet sein)
- **Ein lokales (Passwort-)Konto** — siehe den Hinweis zu OIDC unten

> **⚠️ Nicht verfügbar für OIDC-Benutzer.** Wenn Sie sich über einen externen Identitätsanbieter (Entra ID, Authelia, Keycloak, …) anmelden, wird die 2FA von diesem Anbieter verwaltet und die Setup-Seite wird mit der Meldung *"OIDC users cannot enable 2FA. Authentication is managed by your OIDC provider."* blockiert. Aktivieren Sie stattdessen die Multi-Faktor-Authentisierung in Ihrem Identitätsanbieter — siehe [OIDC / SSO](OIDC_Implementation.md).

## 🚀 Aktivieren der Zwei-Faktor-Authentisierung {#enabling-two-factor-authentication}

### Schritt 1 — Die Setup-Seite öffnen {#step-1-open-the-setup-page}

1. Melden Sie sich bei Mail Archiver an
2. Öffnen Sie Ihr Kontomenü und wählen Sie **Two-Factor Authentication**
3. Sie gelangen zur **Two-Factor Setup**-Seite

### Schritt 2 — Den QR-Code scannen {#step-2-scan-the-qr-code}

Die Seite zeigt einen QR-Code an. Öffnen Sie Ihre Authenticator-App, wählen Sie *Konto hinzufügen* → *QR-Code scannen* und richten Sie die Kamera darauf.

**Code kann nicht gescannt werden?** Verwenden Sie stattdessen die **Manuelle Eingabe**: Die Seite zeigt das Geheimnis als Text an (`If you can't scan the QR code, enter this secret manually in your authenticator app`). Geben Sie es in Ihre App als zeitbasiertes/TOTP-Konto ein.

> **ℹ️ Das Geheimnis ist noch nicht gespeichert.** Es wird während der Einrichtung nur in Ihrer Browsersitzung vorgehalten. 2FA wird erst aktiv, wenn Schritt 3 erfolgreich abgeschlossen wurde — wenn Sie die Einrichtung abbrechen, ändert sich an Ihrem Konto nichts.

### Schritt 3 — Mit einem Code bestätigen {#step-3-confirm-with-a-code}

Geben Sie den 6-stelligen Code ein, den Ihre App nun anzeigt, und klicken Sie auf **Enable Two-Factor Authentication**.

Falls der Code abgelehnt wird, wird die Setup-Seite mit einem neuen QR-Code und der Meldung *"Invalid authentication code. Please try again."* erneut angezeigt. Stellen Sie sicher, dass die Uhr Ihres Telefons auf "automatisch" eingestellt ist — TOTP ist auf eine genaue Uhrzeit angewiesen.

### Schritt 4 — Backup-Codes speichern {#step-4-save-your-backup-codes}

Nach erfolgreichem Abschluss werden Sie zur Seite **Backup Codes** weitergeleitet. Dort werden **10 Codes** angezeigt, die jeweils 16 Zeichen lang sind.

> **⚠️ Dies ist das einzige Mal, dass die Codes angezeigt werden.** Sie werden verschlüsselt (hashed) gespeichert — weder Sie noch ein Administrator können sie später erneut anzeigen lassen. Verwenden Sie **Download Backup Codes**, um sie in einer Datei zu speichern, oder kopieren Sie sie in Ihren Passwort-Manager, bevor Sie die Seite verlassen.

Der Download enthält diesen Hinweis:

> *Mail Archiver - Two-Factor Authentication Backup Codes. Speichern Sie diese Backup-Codes an einem sicheren Ort. Sie können diese verwenden, um sich anzumelden, falls Sie den Zugriff auf Ihre Authenticator-App verlieren. Jeder Backup-Code kann nur einmal verwendet werden. Nach der Verwendung eines Codes wird dieser ungültig.*
> *Wichtig: Bewahren Sie diese Codes sicher auf und teilen Sie sie mit niemandem.*

## 🔑 Anmeldung mit 2FA {#logging-in-with-2fa}

Nach der Eingabe Ihres Passworts gelangen Sie zur **Two-Factor Verification** und werden nach einem Code gefragt:

> *Enter a 6-digit TOTP code from your authenticator app or a 16-character backup code*

Beides funktioniert im selben Feld — Mail Archiver unterscheidet sie anhand der Länge:

| Eingabe | Länge | Verhalten |
|---|---|---|
| Code aus Ihrer Authenticator-App | 6 Ziffern | Wird gegen das aktuelle Zeitfenster geprüft |
| Backup-Code | 16 Zeichen | Wird gegen Ihre gespeicherten Codes geprüft und dann **ungültig gemacht** |

Um kleine Zeitabweichungen der Uhr auszugleichen, werden auch Codes aus dem unmittelbar vorangegangenen oder nachfolgenden Zeitschritt akzeptiert.

**"Remember me"** wird aus dem Login-Formular übernommen: Wenn Sie dies angekreuzt haben, bleiben Sie nach Abschluss des 2FA-Schritts auch nach dem Neustart des Browsers angemeldet. Die Auswahl wird in der serverseitigen Sitzung vorgehalten, während der 2FA-Schritt aussteht, und bleibt daher nur erhalten, wenn Sie den Code innerhalb des **60-minütigen** Inaktivitäts-Timeouts der Sitzung eingeben. Details und Limits (7 Tage absolute Lebensdauer, keine Auswirkung für OIDC-Benutzer) finden Sie unter [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).

### Verwendung eines Backup-Codes {#using-a-backup-code}

1. Geben Sie einen Ihrer gespeicherten 16-stelligen Codes in das Feld ein
2. Der Code wird akzeptiert und **sofort ungültig gemacht** — er kann nicht erneut verwendet werden
3. Erfolgreiche Logins mit einem Backup-Code werden im Zugriffsprotokoll mit dem Vermerk `(2FA Backup Code)` aufgezeichnet, damit Sie ungewöhnliche Nutzung später erkennen können

### Rate Limiting {#rate-limiting}

Um Brute-Force-Angriffe zu verlangsamen, ist die 2FA-Verifizierung auf **5 Versuche pro 15 Minuten** begrenzt, gezählt pro IP-Adresse und Benutzername. Danach sehen Sie:

> **Too Many Attempts** — *You have made too many 2FA verification attempts. Please wait and try again later.*

Warten Sie das Zeitfenster ab und versuchen Sie es erneut. Das Limit gilt für fehlgeschlagene *und* erfolgreiche Versuche innerhalb des Zeitfensters.

## 🚫 Deaktivieren der Zwei-Faktor-Authentisierung {#disabling-two-factor-authentication}

1. Öffnen Sie **Two-Factor Authentication** in Ihrem Kontomenü
2. Die Seite fragt nach einer Bestätigung: *"Are you sure you want to disable two-factor authentication? This will reduce the security of your account."*
3. Geben Sie Ihr **Passwort** ein — dieser Schritt erfordert Ihr aktuelles Passwort, keinen 2FA-Code
4. Klicken Sie auf **Disable Two-Factor Authentication**

Durch das Deaktivieren wird das Geheimnis entfernt und alle verbleibenden Backup-Codes werden ungültig gemacht. Ihr nächster Login benötigt nur noch das Passwort.

## 🔄 Authenticator verloren? Zwei Wege zurück {#lost-your-authenticator-two-ways-back-in}

### Wenn Sie noch Backup-Codes haben {#if-you-still-have-backup-codes}

Melden Sie sich mit einem Backup-Code an, wie oben beschrieben, und deaktivieren und reaktivieren Sie dann die 2FA mit Ihrem neuen Gerät. Denken Sie daran, die neuen Backup-Codes zu speichern.

### Wenn Sie keine Backup-Codes mehr haben {#if-you-have-no-backup-codes-left}

Ein **Administrator** muss die 2FA für Ihr Konto zurücksetzen:

1. Administrator öffnet **Users**
2. Bearbeitet den betroffenen Benutzer
3. Wählt **Reset Two-Factor Authentication** und bestätigt:
   *"Are you sure you want to reset two-factor authentication for this user? They will need to set it up again."*
4. Der Benutzer kann sich dann nur noch mit seinem Passwort anmelden und die 2FA erneut einrichten

Der Reset löscht das Geheimnis und die Backup-Codes. Die Bestätigungsmeldung *"Two-factor authentication has been successfully reset for user …"* erscheint in der Benutzerliste.

> **ℹ️ Umfang des Resets.** Der Reset betrifft nur die 2FA. Es ist **kein** Passwort-Reset — siehe [Emergency Account Recovery](EmergencyAccountRecovery.md), falls das Passwort das Problem ist oder das Konto vollständig gesperrt wurde.

## 👁️ Was Administratoren sehen können {#what-administrators-can-see}

Die **Users**-Übersicht zeigt eine 2FA-Spalte:

- *"Two-factor authentication is currently enabled for this user."* — 2FA ist aktiv
- Andernfalls ist die 2FA für diesen Benutzer deaktiviert

Administratoren können die 2FA zurücksetzen (siehe oben), können aber **nicht** das Geheimnis oder die Backup-Codes lesen — beide werden verschlüsselt (hashed) gespeichert. Ein Benutzer, der sein Gerät ohne Backup-Codes verliert, hat keine andere Möglichkeit als den Admin-Reset.

## 🔍 Audit-Protokoll {#audit-trail}

2FA-Aktivitäten werden in das Protokoll geschrieben und bei erfolgreichen Logins auch in das Zugriffsprotokoll:

| Ereignis | Wo | Eintrag |
|---|---|---|
| Login via Authenticator-App | Zugriffsprotokoll | `Login` mit Vermerk `IP: <address> (2FA)` |
| Login via Backup-Code | Zugriffsprotokoll | `Login` mit Vermerk `IP: <address> (2FA Backup Code)` |
| Fehlgeschlagener 2FA-Versuch | Anwendungs-Protokoll | Warnung mit Benutzername, IP und maskiertem Token |
| Ungültiger Backup-Code | Anwendungs-Protokoll | Warnung mit Benutzername und IP |

Der Eintrag im Zugriffsprotokoll macht die Nutzung von Backup-Codes im Nachhinein sichtbar — nützlich, um zu erkennen, ob ein Code von jemandem verwendet wurde, der ihn nicht haben sollte. Siehe [Access Logging](Logs.md), um zu erfahren, wie Sie diese Einträge lesen.

## ⚙️ Funktionsweise im Hintergrund {#how-it-works-under-the-hood}

Für Administratoren und Prüfer:

- **Standard**: TOTP (RFC 6238), 20-Byte zufälliges Geheimnis, base32-kodiert
- **Verifizierungsfenster**: ±1 Zeitschritt, um Uhrendrift zu tolerieren
- **Backup-Codes**: 10 Codes, jeweils 16 Zeichen, generiert aus einem kryptografischen RNG, **hashed** gespeichert, bei Verwendung einzeln ungültig gemacht
- **Speicherung des Geheimnisses**: im Benutzerdatensatz; wird nach dem Setup niemals an den Browser zurückgegeben
- **Sitzungshandhabung**: Die Sitzung wird neu generiert, bevor die 2FA-Identität gespeichert wird, sodass ein vor dem Login platzierter Session-Cookie nicht in die authentifizierte Sitzung übernommen werden kann
- **OIDC-Konten**: Die 2FA-Verwaltung ist blockiert; der Identitätsanbieter ist maßgeblich
- **API-Keys**: Umgehen 2FA systembedingt, da nicht-interaktive Token keine Challenge beantworten können — siehe die Sicherheitsbegründung im [REST API guide](API.md)

## ❓ Fehlerbehebung {#troubleshooting}

| Symptom | Ursache | Lösung |
|---|---|---|
| *"Invalid authentication code"* | Uhr des Telefons falsch oder falsches Konto in der App | Automatische Zeit-Synchronisierung aktivieren; prüfen, ob die App den Mail Archiver-Eintrag anzeigt |
| *"Two-factor secret is missing. Please restart the setup process."* | Setup-Seite zu lange offen, Sitzung abgelaufen | Starten Sie das Setup erneut über das Kontomenü |
| *"Your session has expired. Please log in again."* | Zu viel Zeit auf der Verifizierungsseite vergangen | Melden Sie sich erneut an |
| *"Two-factor authentication is not enabled for this account."* | 2FA wurde zurückgesetzt oder nie abgeschlossen | Richten Sie es erneut ein oder fragen Sie einen Administrator |
| Backup-Code abgelehnt | Code bereits verwendet oder Zeichen falsch gelesen | Backup-Codes funktionieren nur **einmal**; verwenden Sie einen anderen oder lassen Sie die 2FA zurücksetzen |
| *"Too Many Attempts"* | Mehr als 5 Verifizierungsversuche in 15 Minuten | Warten Sie das Zeitfenster ab |
| Keine Setup-Seite im Kontomenü | Sie melden sich via OIDC an | Aktivieren Sie MFA bei Ihrem Identitätsanbieter |
| Keine 2FA-Spalte in der Benutzerliste | Sie sind kein Administrator | Fragen Sie einen Administrator |

## 📚 Verwandte Dokumentation {#related-documentation}

- [User Management and Mailbox Permissions](UserManagement.md) — Erstellen von Konten, Admin-Rechte
- [OIDC / SSO Authentication](OIDC_Implementation.md) — Anmeldung über externe Anbieter
- [Emergency Account Recovery](EmergencyAccountRecovery.md) — Gesperrte Konten
- [Access Logging](Logs.md) — Lesen des Audit-Protokolls
- [REST API](API.md) — Warum API-Keys die 2FA umgehen
