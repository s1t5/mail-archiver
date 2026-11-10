# 🔐 Zwei-Faktor-Authentifizierung {#two-factor-authentication}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Die Zwei-Faktor-Authentifizierung (2FA) fügt dem Anmeldeprozess einen zweiten Schritt hinzu: Nach
Ihrem Passwort fragt Mail Archiver nach einem **6-stelligen Code**, der von einer
Authenticator-App auf Ihrem Telefon generiert wird. Ein gestohlenes Passwort allein reicht dann nicht
mehr aus, um auf das Archiv zuzugreifen.

Mail Archiver implementiert 2FA mittels **TOTP** (Time-based One-Time Password, RFC
6238) — demselben Standard, der von Google Authenticator, Microsoft
Authenticator, Authy, 1Password, Bitwarden und anderen verwendet wird. Es ist kein externer Dienst
beteiligt; das Geheimnis verlässt Ihre Instanz nie.

**2FA ist pro Benutzer optional (opt-in).** Sie wird von jedem Benutzer für sein eigenes
Konto konfiguriert, nicht global durch einen Administrator.

## 🛠️ Voraussetzungen {#prerequisites}

- Eine TOTP-fähige Authenticator-App auf Ihrem Telefon oder Desktop
- Zugriff auf Ihr eigenes Konto (Sie müssen angemeldet sein)
- **Ein lokales (Passwort-)Konto** — siehe den Hinweis zu OIDC unten

> **⚠️ Nicht verfügbar für OIDC-Benutzer.** Wenn Sie sich über einen externen
> Identitätsanbieter (Entra ID, Authelia, Keycloak, …) anmelden, wird die 2FA durch diesen
> Anbieter verwaltet und die Konfigurationsseite wird mit der Meldung *"OIDC users cannot
> enable 2FA. Authentication is managed by your OIDC provider."* gesperrt. Aktivieren Sie
> stattdessen die Multi-Faktor-Authentifizierung in Ihrem Identitätsanbieter — siehe
> [OIDC / SSO](OIDC_Implementation.md).

## 🚀 Aktivieren der Zwei-Faktor-Authentifizierung {#enabling-two-factor-authentication}

### Schritt 1 — Öffnen Sie die Einrichtungsseite {#step-1-open-the-setup-page}

1. Melden Sie sich bei Mail Archiver an
2. Öffnen Sie Ihr Kontomenü und wählen Sie **Two-Factor Authentication**
3. Sie gelangen zur **Two-Factor Setup**

### Schritt 2 — Scannen Sie den QR-Code {#step-2-scan-the-qr-code}

Die Seite zeigt einen QR-Code an. Öffnen Sie Ihre Authenticator-App, wählen Sie *Konto hinzufügen* →
*QR-Code scannen* und richten Sie die Kamera darauf.

**Können Sie den Code nicht scannen?** Verwenden Sie stattdessen die **Manuelle Eingabe**: Die Seite zeigt das
Geheimnis als Text an (`If you can't scan the QR code, enter this secret manually in
your authenticator app`). Geben Sie es in Ihrer App als zeitbasiertes/TOTP-Konto ein.

> **ℹ️ Das Geheimnis ist noch nicht gespeichert.** Es wird nur für die Dauer der
> Einrichtung in Ihrer Browser-Sitzung vorgehalten. 2FA wird erst aktiv, wenn Schritt 3
> erfolgreich abgeschlossen wurde — wenn Sie die Einrichtung abbrechen, ändert sich nichts an Ihrem Konto.

### Schritt 3 — Bestätigung mit einem Code {#step-3-confirm-with-a-code}

Geben Sie den 6-stelligen Code ein, den Ihre App nun anzeigt, und klicken Sie auf **Enable Two-Factor
Authentication**.

Wenn der Code abgelehnt wird, wird die Einrichtungsseite erneut mit einem neuen QR-Code und
der Meldung *"Invalid authentication code. Please try again."* angezeigt. Stellen Sie sicher, dass
die Uhr Ihres Telefons auf automatische Einstellung steht — TOTP ist auf eine genaue Zeit angewiesen.

### Schritt 4 — Speichern Sie Ihre Backup-Codes {#step-4-save-your-backup-codes}

Nach erfolgreichem Abschluss werden Sie zur Seite **Backup Codes** weitergeleitet. Dort werden **10 Codes**
angezeigt, die jeweils 16 Zeichen lang sind.

> **⚠️ Dies ist das einzige Mal, dass die Codes angezeigt werden.** Sie werden
> gehasht gespeichert — weder Sie noch ein Administrator können sie später erneut anzeigen.
> Verwenden Sie **Download Backup Codes**, um sie in einer Datei zu speichern, oder kopieren Sie sie
> in Ihren Passwort-Manager, bevor Sie die Seite verlassen.

Der Download enthält diesen Hinweis:

> *Mail Archiver - Two-Factor Authentication Backup Codes. Store these backup
> codes in a safe place. You can use them to log in if you lose access to your
> authenticator app. Each backup code can only be used once. After using a
> code, it will be invalidated.*
> *Important: Keep these codes secure and do not share them with anyone.*

## 🔑 Anmeldung mit 2FA {#logging-in-with-2fa}

Nach der Eingabe Ihres Passworts werden Sie zur **Zwei-Faktor-Verifizierung** weitergeleitet und aufgefordert, einen Code einzugeben:

> *Geben Sie einen 6-stelligen TOTP-Code aus Ihrer Authenticator-App oder einen 16-stelligen
> Backup-Code ein*

Beide funktionieren im selben Feld — Mail Archiver unterscheidet sie anhand der Länge:

| Eingabe | Länge | Verhalten |
|---|---|---|
| Code aus Ihrer Authenticator-App | 6 Ziffern | Wird gegen das aktuelle Zeitfenster geprüft |
| Backup-Code | 16 Zeichen | Wird gegen Ihre gespeicherten Codes geprüft, dann **ungültig gemacht** |

Um geringfügige Zeitunterschiede auszugleichen, werden auch Codes aus dem unmittelbar vorangegangenen und dem nachfolgenden Zeitschritt akzeptiert.

**"Angemeldet bleiben"** wird vom Anmeldeformular übernommen: Wenn Sie dies aktiviert haben, bleiben Sie nach Abschluss des 2FA-Schritts auch nach einem Neustart des Browsers angemeldet. Die Auswahl wird in der serverseitigen Sitzung gehalten, während der 2FA-Schritt aussteht, sodass sie nur erhalten bleibt, wenn Sie den Code innerhalb des **60-minütigen** Inaktivitäts-Timeouts der Sitzung eingeben. Details und Limits (7 Tage absolute Lebensdauer, keine Auswirkung für OIDC-Benutzer) finden Sie unter [User Management → Remember Me](UserManagement.md#staying-signed-in-the-remember-me-checkbox).

### Verwendung eines Backup-Codes {#using-a-backup-code}

1. Geben Sie einen Ihrer gespeicherten 16-stelligen Codes im selben Feld ein
2. Der Code wird akzeptiert und **sofort ungültig gemacht** — er kann nicht erneut verwendet werden
3. Erfolgreiche Anmeldungen mit einem Backup-Code werden im Zugriffsprotokoll mit dem
   Vermerk `(2FA Backup Code)` aufgezeichnet, damit Sie ungewöhnliche Nutzungen später erkennen können

### Rate Limiting {#rate-limiting}

Um Brute-Force-Angriffe zu verlangsamen, ist die 2FA-Verifizierung auf **5 Versuche pro 15 Minuten** begrenzt, gezählt pro IP-Adresse und Benutzername. Danach sehen Sie:

> **Zu viele Versuche** — *Sie haben zu viele 2FA-Verifizierungsversuche unternommen.
> Bitte warten Sie und versuchen Sie es später erneut.*

Warten Sie das Zeitfenster ab und versuchen Sie es erneut. Das Limit gilt sowohl für fehlgeschlagene *als auch* erfolgreiche Versuche innerhalb des Zeitfensters.

## 🚫 Deaktivieren der Zwei-Faktor-Authentifizierung {#disabling-two-factor-authentication}

1. Öffnen Sie **Two-Factor Authentication** in Ihrem Kontomenü
2. Die Seite fordert zur Bestätigung auf: *"Sind Sie sicher, dass Sie die Zwei-Faktor-Authentifizierung deaktivieren möchten? Dies wird die Sicherheit Ihres Kontos verringern."*
3. Geben Sie Ihr **password** ein — dieser Schritt erfordert Ihr aktuelles Passwort, keinen 2FA-Code
4. Klicken Sie auf **Disable Two-Factor Authentication**

Durch das Deaktivieren wird der Secret entfernt und alle verbleibenden backup codes werden ungültig. Für Ihre nächste Anmeldung wird nur noch das Passwort benötigt.

## 🔄 Authenticator verloren? Zwei Wege zurück {#lost-your-authenticator-two-ways-back-in}

### Wenn Sie noch Backup-Codes haben {#if-you-still-have-backup-codes}

Melden Sie sich mit einem Backup-Code an, wie oben beschrieben, und deaktivieren Sie dann die 2FA, um sie mit Ihrem neuen Gerät erneut zu aktivieren. Denken Sie daran, die neuen Backup-Codes zu speichern.

### Wenn Sie keine Backup-Codes mehr haben {#if-you-have-no-backup-codes-left}

Ein **Administrator** muss die 2FA für Ihr Konto zurücksetzen:

1. Administrator öffnet **Users**
2. Bearbeitet den betroffenen Benutzer
3. Wählt **Reset Two-Factor Authentication** und bestätigt:
   *"Are you sure you want to reset two-factor authentication for this user?
   They will need to set it up again."*
4. Der Benutzer kann sich dann nur mit seinem Passwort anmelden und die 2FA erneut einrichten

Der Reset löscht den Secret und die Backup-Codes. Die Bestätigungsmeldung
*"Two-factor authentication has been successfully reset for user …"* erscheint in
der Users-Liste.

> **ℹ️ Umfang des Resets.** Der Reset betrifft nur die 2FA. Es handelt sich **nicht** um einen
> Passwort-Reset — siehe [Emergency Account Recovery](EmergencyAccountRecovery.md), falls das
> Passwort das Problem ist oder das Konto vollständig gesperrt wurde.

## 👁️ Was Administratoren sehen können {#what-administrators-can-see}

Die **Users**-Übersicht zeigt eine 2FA-Spalte an:

- *"Two-factor authentication is currently enabled for this user."* — 2FA ist aktiv
- Andernfalls ist 2FA für diesen Benutzer deaktiviert

Administratoren können 2FA (oben) zurücksetzen, können aber **weder** das Geheimnis noch die Backup-Codes lesen — beide werden gehasht gespeichert. Ein Benutzer, der sein Gerät ohne Backup-Codes verliert, hat keine andere Möglichkeit als den Reset durch den Administrator.

## 🔍 Audit-Protokoll {#audit-trail}

2FA-Aktivitäten werden in das Protokoll und bei erfolgreichen Anmeldungen in das Zugriffsprotokoll geschrieben:

| Event | Wo | Eintrag |
|---|---|---|
| Login via authenticator app | Access log | `Login` mit Notiz `IP: <address> (2FA)` |
| Login via backup code | Access log | `Login` mit Notiz `IP: <address> (2FA Backup Code)` |
| Fehlgeschlagener 2FA-Versuch | Application log | Warnung mit Benutzername, IP und maskiertem Token |
| Ungültiger backup code | Application log | Warnung mit Benutzername und IP |

Der Eintrag im Zugriffsprotokoll macht die Nutzung von backup codes im Nachhinein sichtbar – nützlich, um zu erkennen, ob ein Code von jemandem verwendet wurde, der ihn nicht haben sollte. Weitere Informationen zum Lesen dieser Einträge finden Sie unter [Access Logging](Logs.md).

## ⚙️ Funktionsweise im Hintergrund {#how-it-works-under-the-hood}

Für Administratoren und Prüfer:

- **Standard**: TOTP (RFC 6238), 20-Byte zufälliges Secret, base32-kodiert
- **Verifizierungsfenster**: ±1 Zeitschritt, um Uhrendrift zu tolerieren
- **Backup-Codes**: 10 Codes, jeweils 16 Zeichen, generiert aus einem kryptografischen
  RNG, gespeichert als **Hash**, bei Verwendung einzeln ungültig gemacht
- **Secret-Speicherung**: im Benutzerdatensatz; wird nach der Einrichtung niemals an den Browser zurückgegeben
- **Sitzungsverwaltung**: Die Sitzung wird regeneriert, bevor die 2FA-Identität
  gespeichert wird, sodass ein vor dem Login platzierter Session-Cookie nicht in die
  authentifizierte Sitzung übernommen werden kann
- **OIDC-Konten**: 2FA-Verwaltung ist blockiert; der Provider ist maßgeblich
- **API-Keys**: umgehen 2FA systembedingt, da nicht-interaktive Token eine
  Herausforderung nicht beantworten können — siehe die Sicherheitsbegründung im [REST API guide](API.md)

## ❓ Fehlerbehebung {#troubleshooting}

| Symptom | Ursache | Lösung |
|---|---|---|
| *"Invalid authentication code"* | Telefonuhr falsch eingestellt oder falsches Konto in der App | Automatische Zeit-Synchronisierung aktivieren; prüfen, ob die App den Mail Archiver-Eintrag anzeigt |
| *"Two-factor secret is missing. Please restart the setup process."* | Setup-Seite war zu lange geöffnet, Sitzung abgelaufen | Starten Sie das Setup erneut über das Konto-Menü |
| *"Your session has expired. Please log in again."* | Zu viel Zeit auf der Verifizierungsseite vergangen | Erneut anmelden |
| *"Two-factor authentication is not enabled for this account."* | 2FA wurde zurückgesetzt oder nie abgeschlossen | Richten Sie es erneut ein oder fragen Sie einen Administrator |
| Backup code rejected | Code bereits verwendet oder ein Zeichen falsch gelesen | Backup-Codes funktionieren nur **einmal**; verwenden Sie einen anderen oder lassen Sie 2FA zurücksetzen |
| *"Too Many Attempts"* | Mehr als 5 Verifizierungsversuche in 15 Minuten | Warten Sie das Zeitfenster ab |
| No setup page in the account menu | Sie melden sich via OIDC an | Aktivieren Sie MFA bei Ihrem Identity Provider |
| No 2FA column in the Users list | Sie sind kein Administrator | Fragen Sie einen Administrator |

## 📚 Verwandte Dokumentation {#related-documentation}

- [Benutzerverwaltung und Postfach-Berechtigungen](UserManagement.md) — Erstellen von Konten, Admin-Rechte
- [OIDC / SSO Authentication](OIDC_Implementation.md) — Anmeldung über externe Anbieter
- [Notfall-Kontowiederherstellung](EmergencyAccountRecovery.md) — gesperrte Konten
- [Zugriffsprotokollierung](Logs.md) — Lesen des Audit-Protokolls
- [REST API](API.md) — Warum API-Keys die 2FA umgehen
