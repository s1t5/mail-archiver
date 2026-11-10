# 👥 Benutzerverwaltung und Postfach-Berechtigungen {#user-management-and-mailbox-permissions}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden bietet detaillierte Anweisungen zum Erstellen neuer Benutzerkonten und zum Zuweisen von Postfach-Berechtigungen in der Mail Archiver Anwendung. Er behandelt außerdem das Ändern Ihres eigenen Passworts sowie die erzwungene Passwortänderung bei einer frisch installierten Instanz.

## 🛠️ Voraussetzungen {#prerequisites}

- Administratorzugriff auf die Mail Archiver Anwendung
- Bestehende E-Mail-Konten, die im System konfiguriert sind

## 🧑‍💻 Erstellen eines neuen Benutzerkontos {#creating-a-new-user-account}

1. Melden Sie sich mit einem Konto an, das über Administratorrechte verfügt, bei der Mail Archiver Anwendung an
2. Navigieren Sie im Hauptmenü zum Abschnitt „Users“
3. Klicken Sie auf die Schaltfläche „Create New User“
4. Füllen Sie die erforderlichen Benutzerinformationen aus:
   - **Username**: Geben Sie einen eindeutigen Benutzernamen für den neuen Benutzer ein
   - **Password**: Geben Sie ein sicheres Passwort für den Benutzer ein
   - **Email**: E-Mail-Adresse des Benutzers
   - **Admin**: Aktivieren Sie dieses Kontrollkästchen, wenn der Benutzer über Administratorrechte verfügen soll
   - **Self Manager**: Aktivieren Sie dieses Kontrollkästchen, wenn der Benutzer seine eigenen Mail-Konten verwalten können soll (bestehende bearbeiten sowie neue hinzufügen und Konten löschen)
   - **active**: Muss aktiviert sein, um Logins für den Benutzer zu ermöglichen
5. Klicken Sie auf „Create User“, um das neue Benutzerkonto zu speichern

## 🔐 Zuweisen von Postfach-Berechtigungen an Benutzer {#assigning-mailbox-permissions-to-users}

1. Navigieren Sie nach dem Erstellen des Benutzers oder beim Bearbeiten eines vorhandenen Benutzers im Hauptmenü zum Abschnitt „Users“
2. Suchen Sie den Benutzer in der Liste, dem Sie Mail-Konten zuweisen möchten, und klicken Sie auf die Schaltfläche „Assign“ für diesen Benutzer
3. Auf der Seite „Assign Mail Accounts“ sehen Sie alle verfügbaren Mail-Konten und Kontrollkästchen, die angeben, ob diese dem Benutzer zugewiesen sind
5. Um ein neues E-Mail-Konto zuzuweisen, markieren Sie das entsprechende Kontrollkästchen für das spezifische Konto
6. Um ein E-Mail-Konto vom Benutzer zu entfernen, deaktivieren Sie das Kontrollkästchen
7. Klicken Sie auf „Save Assignments“, um die Änderungen zu übernehmen

## 👤 Benutzerkonto-Berechtigungen {#user-account-permissions}

### Admin-Benutzer {#admin-users}
Admin-Benutzer haben vollen Zugriff auf die Anwendung und können:
- Alle Benutzerkonten und deren Berechtigungen verwalten
- Auf alle E-Mail-Konten im System zugreifen
- Alle Zugriffsprotokolle einsehen
- Alle administrativen Aufgaben ausführen

### Self Manager-Benutzer {#self-manager-users}
Self Manager-Benutzer können:
- Neue E-Mail-Konten zu ihrem eigenen Konto hinzufügen
- Bestehende ihnen zugewiesene E-Mail-Konten bearbeiten
- Ihnen zugewiesene E-Mail-Konten löschen
- E-Mails aus ihren zugewiesenen Konten aufrufen und verwalten (einschließlich Löschen)
- Ihre eigenen Zugriffsprotokolle einsehen

### Standard-Benutzer {#standard-users}
Standard-Benutzer haben einen eingeschränkten Zugriff und können:
- Archivierte E-Mails aus zugewiesenen E-Mail-Konten einsehen
- In ihren zugewiesenen E-Mail-Konten suchen
- E-Mails aus ihren zugewiesenen Konten exportieren
- E-Mails aus ihren zugewiesenen Konten wiederherstellen
- E-Mail-Anhänge aus zugewiesenen Konten aufrufen

## 🔑 Ändern Ihres eigenen Passworts {#changing-your-own-password}

Jeder Benutzer mit einem lokalen Passwort kann es ohne einen Administrator ändern:

1. Klicken Sie oben rechts auf Ihren **Benutzernamen**, um das Benutzermenü zu öffnen.
2. Wählen Sie **Change Password**.
3. Geben Sie Ihr **aktuelles Passwort** und das **neue Passwort** zweimal ein und senden Sie es ab.

Das neue Passwort muss die unten aufgeführten Anforderungen erfüllen und muss sich **vom aktuellen unterscheiden**.

> ℹ️ **OIDC-Benutzer können ihr Passwort hier nicht ändern.** Die Seite antwortet mit
> *"OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* Ändern Sie das Passwort stattdessen bei Ihrem Identity Provider —
> siehe [OIDC / SSO](OIDC_Implementation.md).

### Passwortanforderungen {#password-requirements}

- mindestens **10 Zeichen**
- mindestens ein **Großbuchstabe**
- mindestens ein **Kleinbuchstabe**
- mindestens eine **Zahl**
- mindestens ein **Sonderzeichen**

Das Formular zeigt diese während der Eingabe als Live-Checkliste an. Dieselben Regeln gelten, wenn ein Administrator einen Benutzer erstellt oder ein Passwort zurücksetzt.

## ⚠️ Erzwungene Passwortänderung (Erstinstallation) {#forced-password-change-initial-setup}

Eine frisch installierte Instanz startet mit den Administrator-Anmeldedaten aus der
Konfiguration (`Authentication__Username` / `Authentication__Password`). Solange
dieses Standardpasswort noch verwendet wird **und noch kein Mail-Konto existiert**, erzwingt die erste Anmeldung eine Änderung:

- Ein Warnbanner erklärt den Grund: *"Aus Sicherheitsgründen müssen Sie Ihr
  Passwort ändern, bevor Sie fortfahren können. Dies ist erforderlich, da Sie die Standard-
  Systemanmeldedaten auf einem neu eingerichteten System verwenden."*
- Sie können die Seite nicht verlassen: *"Sie müssen Ihr Passwort ändern, bevor
  Sie die Nutzung des Systems fortsetzen können."*
- Das neue Passwort muss sich vom aktuellen unterscheiden.
- Nach dem Speichern wird das Flag gelöscht und Sie werden zum Dashboard weitergeleitet.

Sobald Sie das Standardpasswort geändert haben, erscheint die erzwungene Änderung nie wieder.

## 🕒 Angemeldet bleiben — das Kontrollkästchen „Angemeldet bleiben“ {#staying-signed-in-the-remember-me-checkbox}

Das Anmeldeformular enthält ein Kontrollkästchen mit der Beschriftung **Remember Me** (*angemeldet bleiben* in der deutschen Benutzeroberfläche). Es bewirkt genau eine Sache: Es entscheidet, ob das Authentifizierungs-Cookie ein Sitzungscookie oder ein persistentes Cookie ist.

| | Kontrollkästchen nicht markiert | Kontrollkästchen markiert |
|---|---|---|
| Cookie-Lebensdauer | `Authentication__SessionTimeoutMinutes` (Standard **60 Minuten**) | **7 Tage** |
| Cookie-Typ | Sitzungscookie — wird beim Schließen des Browsers verworfen | Persistentes Cookie — auf der Festplatte gespeichert |
| Browser-Neustart | Sie müssen sich erneut anmelden | Sie bleiben angemeldet |
| Idle-Timeout | Wird bei jeder Anfrage verlängert (`SlidingExpiration`) | Wird bei jeder Anfrage verlängert, bis zum absoluten Limit von 7 Tagen |
| Abmelden | Beendet die Sitzung sofort | Beendet die Sitzung sofort |

Das Kontrollkästchen wandelt also „angemeldet für diese Browser-Sitzung, maximal 60 Minuten Inaktivität“ in „angemeldet für bis zu 7 Tage, einschließlich Browser- und Computer-Neustarts“ um.

### Dinge, die das Kontrollkästchen nicht tut {#things-the-checkbox-does-not-do}

- **Es merkt sich weder Ihr Passwort noch Ihren Benutzernamen.** Das Anmeldeformular ist beim nächsten Besuch nicht vorausgefüllt; es wird nur das Sitzungscookie beibehalten.
- **Die 7 Tage sind ein fester Grenzwert und können nicht konfiguriert werden.** Der Wert ist im Code fest hinterlegt (`Auth/Services/CookieAuthenticationService.cs`). Nur die nicht-persistente Lebensdauer folgt `Authentication__SessionTimeoutMinutes`.
- **Es ist kein Inaktivitäts-Timer.** Die 7 Tage werden ab der Anmeldung gezählt; sobald sie abgelaufen sind, ist eine neue Anmeldung erforderlich, selbst wenn Sie das Archiv vor einer Minute genutzt haben.
- **Es überlebt eine Abmeldung nicht.** Durch Klicken auf *Logout* wird das Cookie unabhängig vom Kontrollkästchen gelöscht.

### Interaktion mit 2FA {#interaction-with-2fa}

Wenn die Zwei-Faktor-Authentifizierung aktiviert ist, wird die Auswahl des Kontrollkästchens während des 2FA-Schritts gespeichert und erst angewendet, nachdem der zweite Faktor erfolgreich war. Die 2FA-Übergabe erfolgt in der serverseitigen Sitzung, die über ein eigenes **60-minütiges Idle-Timeout** verfügt: Wenn der Code nicht innerhalb dieses Zeitfensters eingegeben wird, wird die Anmeldung abgebrochen und die Auswahl des Kontrollkästchens geht verloren.

### Interaktion mit der Anwendungssitzung {#interaction-with-the-application-session}

Mail Archiver führt eine zweite, separate serverseitige Sitzung für kurzlebige Zustände – die 2FA-Übergabe, die Batch-Wiederherstellungs-Auswahl und den letzten Suchstatus. Diese Sitzung hat ein festes **60-minütiges Idle-Timeout** und ist *nicht* persistent. Nach einem Browser-Neustart mit einem gültigen „Remember Me“-Cookie sind Sie angemeldet, aber jede ausstehende Operation, die in dieser Sitzung gehalten wurde, beginnt von vorn.

### Interaktion mit OIDC / SSO {#interaction-with-oidc-sso}

Benutzer, die sich über einen externen Identity Provider anmelden, erhalten niemals das persistente Cookie: Der OIDC-Callback startet die Sitzung immer mit `persistAuthentication: false`. Für diese Konten hat das Kontrollkästchen **keine Auswirkung** — wie lange eine Sitzung dauert, wird durch die Sitzungseinstellungen des Identity Providers entschieden.

> ⚠️ **Sicherheitshinweis.** Lassen Sie das Kontrollkästchen auf einem gemeinsam genutzten oder unbeaufsichtigten Computer nicht markiert — ein persistentes Cookie hält das Archiv bis zu 7 Tage lang ohne Passwort zugänglich. In Umgebungen, in denen dies inakzeptabel ist, verkürzen Sie `Authentication__SessionTimeoutMinutes` und verlangen Sie von den Benutzern, das Kontrollkästchen nicht zu verwenden, oder verwenden Sie OIDC, wo der Anbieter seine eigene Sitzungsrichtlinie durchsetzt.

## 🔒 Sicherheitsaspekte {#security-considerations}

1. Verwenden Sie starke Passwörter für alle Benutzerkonten
2. Ändern Sie das Standard-Administratorpasswort sofort nach der Installation
3. Weisen Sie die Admin-Berechtigung nur Benutzern zu, die sie benötigen
4. Weisen Sie die Self Manager-Berechtigung nur Benutzern zu, die ihre eigenen Konten verwalten müssen
5. Überprüfen Sie regelmäßig die Benutzerberechtigungen, um sicherzustellen, dass der Zugriff angemessen ist
6. Entfernen Sie den Benutzerzugriff, wenn er nicht mehr benötigt wird
7. Wenden Sie das Prinzip der minimalen Rechtevergabe an – weisen Sie nur Zugriff auf die E-Mail-Konten zu, die Benutzer benötigen
