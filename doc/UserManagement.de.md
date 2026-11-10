# 👥 Benutzerverwaltung und Postfach-Berechtigungen {#user-management-and-mailbox-permissions}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Dieser Leitfaden bietet detaillierte Anweisungen zum Erstellen neuer Benutzerkonten und zum Zuweisen von Postfach-Berechtigungen in der Mail Archiver Anwendung. Er deckt auch das Ändern des eigenen Passworts und die erzwungene Passwortänderung bei einer frisch installierten Instanz ab.


## 🛠️ Voraussetzungen {#prerequisites}

- Administratorzugriff auf die Mail Archiver Anwendung
- Vorhandene E-Mail-Konten, die im System konfiguriert sind

## 🧑‍💻 Erstellen eines neuen Benutzerkontos {#creating-a-new-user-account}

1. Melden Sie sich mit einem Konto, das über Administratorrechte verfügt, in der Mail Archiver Anwendung an
2. Navigieren Sie im Hauptmenü zum Abschnitt "Users"
3. Klicken Sie auf die Schaltfläche "Create New User"
4. Füllen Sie die erforderlichen Benutzerinformationen aus:
   - **Username**: Geben Sie einen eindeutigen Benutzernamen für den neuen Benutzer ein
   - **Password**: Geben Sie ein sicheres Passwort für den Benutzer ein
   - **Email**: E-Mail-Adresse des Benutzers
   - **Admin**: Aktivieren Sie dieses Kontrollkästchen, wenn der Benutzer über Administratorrechte verfügen soll
   - **Self Manager**: Aktivieren Sie dieses Kontrollkästchen, wenn der Benutzer seine eigenen E-Mail-Konten verwalten können soll (bestehende bearbeiten und neue hinzufügen sowie Konten löschen)
   - **active**: Sollte aktiviert sein, um Logins für den Benutzer zu ermöglichen
5. Klicken Sie auf "Create User", um das neue Benutzerkonto zu speichern

## 🔐 Zuweisen von Postfach-Berechtigungen an Benutzer {#assigning-mailbox-permissions-to-users}

1. Navigieren Sie nach dem Erstellen des Benutzers oder beim Bearbeiten eines bestehenden Benutzers im Hauptmenü zum Abschnitt "Users"
2. Suchen Sie den Benutzer in der Liste, dem Sie E-Mail-Konten zuweisen möchten, und klicken Sie auf die Schaltfläche "Assign" für diesen Benutzer
3. Auf der Seite "Assign Mail Accounts" sehen Sie alle verfügbaren E-Mail-Konten und Kontrollkästchen, die angeben, ob diese dem Benutzer zugewiesen sind
5. Um ein neues E-Mail-Konto zuzuweisen, aktivieren Sie das entsprechende Kontrollkästchen für das spezifische Konto
6. Um ein E-Mail-Konto vom Benutzer zu entfernen, deaktivieren Sie das Kontrollkästchen
7. Klicken Sie auf "Save Assignments", um die Änderungen anzuwenden

## 👤 Benutzerkonten-Berechtigungen {#user-account-permissions}

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
- Auf E-Mails aus ihren zugewiesenen Konten zugreifen und diese verwalten (einschließlich Löschen)
- Ihre eigenen Zugriffsprotokolle einsehen

### Standard-Benutzer {#standard-users}
Standard-Benutzer haben eingeschränkten Zugriff und können:
- Archivierte E-Mails aus zugewiesenen E-Mail-Konten einsehen
- Innerhalb ihrer zugewiesenen E-Mail-Konten suchen
- E-Mails aus ihren zugewiesenen Konten exportieren
- E-Mails aus ihren zugewiesenen Konten wiederherstellen
- Auf E-Mail-Anhänge aus zugewiesenen Konten zugreifen

## 🔑 Ändern des eigenen Passworts {#changing-your-own-password}

Jeder Benutzer mit einem lokalen Passwort kann es ohne Administrator ändern:

1. Klicken Sie oben rechts auf Ihren **username**, um das Benutzermenü zu öffnen.
2. Wählen Sie **Change Password**.
3. Geben Sie Ihr **current password** und das **new password** zweimal ein und senden Sie es ab.

Das neue Passwort muss die unten stehenden Anforderungen erfüllen und muss sich **vom aktuellen unterscheiden**.

> ℹ️ **OIDC-Benutzer können ihr Passwort hier nicht ändern.** Die Seite antwortet mit:
> *"OIDC users cannot change their password. Password management is handled by
> your OIDC provider."* Ändern Sie das Passwort stattdessen bei Ihrem Identitätsanbieter —
> siehe [OIDC / SSO](OIDC_Implementation.md).

### Passwortanforderungen {#password-requirements}

- mindestens **10 Zeichen**
- mindestens ein **Großbuchstabe**
- mindestens ein **Kleinbuchstabe**
- mindestens eine **Zahl**
- mindestens ein **Sonderzeichen**

Das Formular zeigt diese als Live-Checkliste an, während Sie tippen. Dieselben Regeln gelten, wenn ein Administrator einen Benutzer erstellt oder ein Passwort zurücksetzt.

## ⚠️ Erzwungene Passwortänderung (Erstinstallation) {#forced-password-change-initial-setup}

Eine frisch installierte Instanz startet mit den Administrator-Anmeldedaten aus der Konfiguration (`Authentication__Username` / `Authentication__Password`). Solange dieses Standardpasswort noch verwendet wird **und noch kein E-Mail-Konto existiert**, erzwingt der erste Login eine Änderung:

- Ein Warnbanner erklärt warum: *"For security reasons, you must change your
  password before continuing. This is required because you are using the default
  system credentials on a newly set up system."*
- Sie können die Seite nicht verlassen: *"You must change your password before
  you can continue using the system."*
- Das neue Passwort muss sich vom aktuellen unterscheiden.
- Nach dem Speichern wird das Flag gelöscht und Sie werden zum Dashboard weitergeleitet.

Sobald Sie das Standardpasswort geändert haben, erscheint die erzwungene Änderung nie wieder.

## 🕒 Angemeldet bleiben — das "Remember Me" Kontrollkästchen {#staying-signed-in-the-remember-me-checkbox}

Das Login-Formular hat ein Kontrollkästchen mit der Beschriftung **Remember Me** (*angemeldet bleiben* in der deutschen Benutzeroberfläche). Es bewirkt genau eine Sache: Es entscheidet, ob das Authentifizierungs-Cookie ein Sitzungs-Cookie oder ein persistentes Cookie ist.

| | Kontrollkästchen nicht aktiviert | Kontrollkästchen aktiviert |
|---|---|---|
| Cookie-Lebensdauer | `Authentication__SessionTimeoutMinutes` (Standard **60 Minuten**) | **7 Tage** |
| Cookie-Typ | Sitzungs-Cookie — wird beim Schließen des Browsers verworfen | Persistentes Cookie — wird auf der Festplatte gespeichert |
| Browser-Neustart | Sie müssen sich erneut anmelden | Sie bleiben angemeldet |
| Idle-Timeout | Wird bei jeder Anfrage verlängert (`SlidingExpiration`) | Wird bei jeder Anfrage verlängert, bis zum absoluten 7-Tage-Limit |
| Abmelden | Beendet die Sitzung sofort | Beendet die Sitzung sofort |

Das Kontrollkästchen wandelt also "angemeldet für diese Browser-Sitzung, maximal 60 Minuten Inaktivität" in "angemeldet für bis zu 7 Tage, einschließlich Browser- und Geräte-Neustarts" um.

### Dinge, die das Kontrollkästchen nicht tut {#things-the-checkbox-does-not-do}

- **Es merkt sich nicht Ihr Passwort oder Benutzernamen.** Das Login-Formular ist beim nächsten Besuch nicht vorausgefüllt; es wird nur das Sitzungs-Cookie beibehalten.
- **Die 7 Tage sind ein hartes Limit und können nicht konfiguriert werden.** Der Wert ist im Code fest hinterlegt (`Auth/Services/CookieAuthenticationService.cs`). Nur die nicht-persistente Lebensdauer folgt `Authentication__SessionTimeoutMinutes`.
- **Es ist kein Inaktivitäts-Timer.** 7 Tage werden ab dem Login gezählt; sobald diese abgelaufen sind, ist ein neuer Login erforderlich, selbst wenn Sie das Archiv vor einer Minute benutzt haben.
- **Es überlebt kein Logout.** Das Klicken auf *Logout* löscht das Cookie, unabhängig vom Kontrollkästchen.

### Interaktion mit 2FA {#interaction-with-2fa}

Wenn die Zwei-Faktor-Authentifizierung aktiviert ist, wird die Auswahl des Kontrollkästchens während des 2FA-Schritts gespeichert und erst nach Erfolg des zweiten Faktors angewendet. Der 2FA-Übergang findet in der serverseitigen Sitzung statt, die über einen eigenen **60-minütigen Idle-Timeout** verfügt: Wenn der Code nicht innerhalb dieses Zeitfensters eingegeben wird, wird der Login abgebrochen und die Auswahl des Kontrollkästchens geht verloren.

### Interaktion mit der Anwendungssitzung {#interaction-with-the-application-session}

Mail Archiver führt eine zweite, separate serverseitige Sitzung für kurzlebige Zustände — den 2FA-Übergang, die Auswahl der Batch-Wiederherstellung und den letzten Suchzustand. Diese Sitzung hat einen festen **60-minütigen Idle-Timeout** und ist *nicht* persistent. Nach einem Browser-Neustart mit einem gültigen "Remember Me"-Cookie sind Sie zwar angemeldet, aber jede ausstehende Operation, die in dieser Sitzung gehalten wurde, beginnt von vorn.

### Interaktion mit OIDC / SSO {#interaction-with-oidc-sso}

Benutzer, die sich über einen externen Identitätsanbieter anmelden, erhalten niemals das persistente Cookie: Der OIDC-Callback startet die Sitzung immer mit `persistAuthentication: false`. Für diese Konten hat das Kontrollkästchen **keine Auswirkung** — wie lange eine Sitzung dauert, wird durch die Sitzungseinstellungen des Identitätsanbieters entschieden.

> ⚠️ **Sicherheitshinweis.** Lassen Sie das Kontrollkästchen auf einem gemeinsam genutzten oder unbeaufsichtigten Computer nicht aktiviert — ein persistentes Cookie hält das Archiv bis zu 7 Tage lang ohne Passwort zugänglich. In Umgebungen, in denen dies inakzeptabel ist, verkürzen Sie `Authentication__SessionTimeoutMinutes` und weisen Sie die Benutzer an, das Kontrollkästchen nicht zu verwenden, oder nutzen Sie OIDC, bei dem der Anbieter seine eigene Sitzungsrichtlinie erzwingt.

## 🔒 Sicherheitsüberlegungen {#security-considerations}

1. Verwenden Sie starke Passwörter für alle Benutzerkonten
2. Ändern Sie das Standard-Administratorpasswort sofort nach der Installation
3. Weisen Sie die Admin-Berechtigung nur Benutzern zu, die sie benötigen
4. Weisen Sie die Self Manager-Berechtigung nur Benutzern zu, die ihre eigenen Konten verwalten müssen
5. Überprüfen Sie regelmäßig die Benutzerberechtigungen, um sicherzustellen, dass der Zugriff angemessen ist
6. Entfernen Sie den Benutzerzugriff, wenn er nicht mehr benötigt wird
7. Verwenden Sie das Prinzip der geringsten Privilegien — weisen Sie nur Zugriff auf die E-Mail-Konten zu, die Benutzer tatsächlich benötigen
