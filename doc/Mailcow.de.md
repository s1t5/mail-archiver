# Mailcow-Instanz Archivierung {#mailcow-instance-archiving}

[← Zurück zum Dokumentationsindex](index.md)

## Übersicht {#overview}

Der Mail Archiver kann die Postfächer einer [Mailcow](https://mailcow.email)-Instanz zentral archivieren, ohne die persönlichen Passwörter der Postfachbesitzer zu erfassen.

Ein Administrator verbindet die Instanz einmalig mit ihrem API-Key. Der Mail Archiver listet daraufhin die Postfächer auf und erstellt für jedes für die Archivierung ausgewählte Postfach ein **dediziertes App-Passwort**, das auf `imap_access` beschränkt ist. Dieses App-Passwort wird vom bestehenden IMAP-Anbieter verwendet, um das Postfach zu archivieren, und kann jederzeit wieder entzogen werden.

Administratoren erreichen den Instanz-Manager über **Add Account → Mailcow** oder über das Aktionsmenü eines Kontos, das bereits von Mailcow verwaltet wird. Der Manager ist nur für Administratoren zugänglich, da der gespeicherte API-Key die Anmeldedaten für jedes Postfach in der Instanz verwalten kann.

## Erstellen von Konten aus Mailcow {#creating-accounts-from-mailcow}

In **Mail Accounts → Add Account** können Administratoren **Mailcow** als Anbieter auswählen.
Das Formular bietet dann die verbundenen Instanzen und eine Aktion **Manage mailboxes** an. Das Auswählen von
Postfächern dort erstellt gewöhnliche IMAP-Archiv-Konten mit dedizierten App-Passwörtern; es wird kein
separates Mailcow-Konto gespeichert. Wenn noch keine Instanz existiert, öffnet **Manage Mailcow instances**
den Instanz-Manager, wo eine verbunden werden kann.

## Verknüpfung eines bestehenden Kontos {#linking-an-existing-account}

In der Bearbeitungsansicht eines IMAP-Kontos können Administratoren den Anbieter auf **Mailcow** umstellen und eine Instanz auswählen. Das Konto behält den IMAP-Anbieter, wird aber in die Mailcow-Verwaltung übernommen: Der Mail Archiver erstellt ein dediziertes App-Passwort, verifiziert es über IMAP und ersetzt das gespeicherte Passwort des Kontos. Das Konto erscheint dann mit einer Mailcow-Aktion in der Kontoliste.

Um die Mailcow-Verwaltung zu verlassen, wählen Sie einen anderen Anbieter aus und speichern Sie. Das App-Passwort wird widerrufen und das lokale Passwort wird gelöscht. Wenn Sie zurück zu IMAP wechseln, geben Sie ein neues IMAP-Passwort ein; das Wechseln zu Microsoft 365 oder einem Import-only-Konto stellt die eigenen Anmeldedaten bereit. Nicht-Administratoren können weiterhin die Einstellungen ohne Anmeldedaten eines von Mailcow verwalteten Kontos bearbeiten, aber das Konto nicht verknüpfen oder die Verknüpfung aufheben.

## Voraussetzungen {#prerequisites}

### Die Verschlüsselung von Anmeldedaten muss aktiviert sein {#credential-encryption-must-be-enabled}

Mail Archiver speichert den Mailcow API-Schlüssel und jedes generierte App-Passwort verschlüsselt im Ruhezustand (at rest) unter Verwendung der vorhandenen Funktion zur Verschlüsselung von Anmeldedaten. Setzen Sie

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```

bevor Sie eine Instanz verbinden. Das Erstellen einer Instanz und das Aktivieren von Postfächern werden beide abgelehnt, solange die Verschlüsselung deaktiviert ist, sodass Anmeldedaten über diese Funktion niemals im Klartext geschrieben werden können. Bewahren Sie den Schlüssel außerhalb der Datenbank auf und nehmen Sie ihn in Ihre sichere Sicherung auf — ohne ihn können gespeicherte App-Passwörter nicht entschlüsselt und Postfächer nicht bereitgestellt werden. Weitere Details finden Sie unter [Setup](Setup.md#-security-settings).

### Mailcow API-Zugriff {#mailcow-api-access}

Erstellen Sie einen API-Schlüssel in Mailcow unter **Configuration → Access → API**. Der Schlüssel muss berechtigt sein,

- Postfächer aufzulisten (`get/mailbox/all`),
- App-Passwörter aufzulisten, zu erstellen und zu löschen (`get/app-passwd/all`, `add/app-passwd`,
  `delete/app-passwd`).

Beschränken Sie die API in den Mailcow-API-Einstellungen auf die IP-Adresse des Mail Archiver Hosts. Der App-Password-Endpunkt von Mailcow erfordert, dass die Funktion **App Passwords** für die Instanz aktiviert ist.

### Netzwerkzugriff {#network-access}

- **HTTPS** zum Mailcow-Ursprung (zum Beispiel `https://mail.example.com`).
- **IMAP über TLS** zum IMAP-Host, normalerweise Port `993`. Plaintext-IMAP wird für die Bereitstellung nicht unterstützt.

## Schritt-für-Schritt-Anleitung {#step-by-step-usage}

### Verbinden einer Instanz {#connecting-an-instance}

1. Melden Sie sich als Administrator bei Mail Archiver an.
2. Öffnen Sie **Mail Accounts** und klicken Sie auf **Mailcow instances**.
3. Klicken Sie auf **Connect Mailcow instance**.
4. Geben Sie die Verbindungsdetails ein:
   - **Account name** — ein Anzeigename für die Instanz in Mail Archiver.
   - **Mailcow URL** — nur die HTTPS-Origin, zum Beispiel `https://mail.example.com`.
     Pfade, Query-Strings und eingebettete Anmeldedaten werden abgelehnt.
   - **API key** — der in Mailcow erstellte Schlüssel.
   - **IMAP host** — der für die Archivierung verwendete Hostname, zum Beispiel `mail.example.com`.
     Geben Sie nur den Hostnamen ohne Protokoll oder Port ein.
   - **IMAP TLS port** — standardmäßig `993`.
5. Klicken Sie auf **Connect**. Mail Archiver verifiziert den API-Schlüssel, indem er die Postfächer auflistet, bevor die Instanz gespeichert wird. Bei Erfolg wird die Postfachauswahl geöffnet.

Der API-Schlüssel wird verschlüsselt gespeichert und niemals wieder in das Formular oder in eine Fehlermeldung zurückgegeben. HTTP-Weiterleitungen sind für API-Aufrufe deaktiviert, sodass der Schlüssel nicht an eine andere Origin weitergeleitet werden kann.

### Aktivieren von Postfächern {#activating-mailboxes}

1. Filtern Sie auf der Postfachseite optional die Liste mit dem Suchfeld. Der Filter gleicht die Adresse und den Anzeigenamen ab und ist nicht fallsensitiv.
2. Wählen Sie die zu archivierenden Postfächer aus. **Select all** und **Clear selection** wirken sich nur auf die aktuell sichtbaren (gefilterten) Postfächer aus, daher ist das Filtern ein sicherer Weg, um eine Teilmenge auszuwählen.
3. Klicken Sie auf **Activate archiving**.

Für jedes ausgewählte Postfach führt Mail Archiver folgende Schritte aus:

1. validiert die Adresse erneut gegen die aktuelle Postfachliste von Mailcow und ignoriert deaktivierte Postfächer,
2. erstellt ein Archiv-Konto mit dem Namen `{instance name} – {mailbox display name}`,
3. generiert ein zufälliges App-Passwort mit dem Namen `MailArchiver-<unique id>`, das auf `imap_access` beschränkt ist,
4. verifiziert die Anmeldedaten mit einem IMAP-Login über TLS,
5. und aktiviert erst dann die Synchronisierung.

Wenn die IMAP-Verifizierung fehlschlägt, bleibt das Konto deaktiviert und kann erneut versucht werden.

### Status-Badges der Postfächer {#mailbox-status-badges}

Die Auswahlseite kennzeichnet jedes Postfach, damit klar ist, warum ein Kontrollkästchen verfügbar ist oder nicht:

| Badge | Bedeutung | Auswählbar |
| --- | --- | --- |
| **Available** | In Mailcow gelistet, noch nicht bereitgestellt | ja |
| **Active** | Bereitgestellt und wird synchronisiert | nein |
| **Pending** | Bereitstellung gestartet, aber nicht abgeschlossen (wiederholbar) | ja |
| **Revoked** | App-Passwort wurde widerrufen; kann erneut aktiviert werden | ja |
| **Existing** | Ein unabhängiges IMAP-Konto mit dieser Adresse existiert bereits | nein |
| **Disabled** | Das Postfach ist in Mailcow deaktiviert | nein |

### Erneuter Versuch bei einem ausstehenden Postfach {#retrying-a-pending-mailbox}

Die generierten Anmeldedaten und der eindeutige App-Passwort-Name werden **vor** dem Aufruf der Mailcow-API dauerhaft gespeichert. Wenn eine Anfrage mehrdeutig fehlschlägt — zum Beispiel ein Timeout, nachdem das App-Passwort erstellt wurde — bleibt das Postfach als **Pending** sichtbar. Laden Sie die Seite neu und wählen Sie es erneut aus: Mail Archiver sucht zuerst nach dem App-Namen und erstellt nur dann ein neues App-Passwort, wenn keines existiert. Ein erneuter Versuch führt nicht zu Duplikaten bei den Anmeldedaten.

Die Bereitstellung und der Widerruf für eine Instanz werden mithilfe von PostgreSQL Advisory Locks serialisiert, sodass zwei gleichzeitige Vorgänge dasselbe Postfach nicht zweimal bereitstellen können.

### Widerrufen des Zugriffs {#revoking-access}

Klicken Sie neben einem Postfach auf **Revoke app password**. Dies

- deaktiviert die zukünftige Synchronisierung,
- löscht das verwaltete App-Passwort in Mailcow,
- und löscht das lokale Passwort.

**Archivierte E-Mails bleiben erhalten.** Nur das verwaltete App-Passwort wird entfernt — andere App-Passwörter, die zum Postfach gehören, werden nicht berührt. Bereits laufende Synchronisierungsoperationen können mit ihrer bestehenden Verbindung abgeschlossen werden.

Wenn die Remote-Anfrage fehlschlägt, bleibt das Konto deaktiviert und der Widerruf kann erneut versucht werden.

Das Löschen eines Archiv-Kontos unter **Mail Accounts** entfernt nicht stillschweigend die Remote-Anmeldedaten. Die Bindung bleibt unter **Remaining app passwords** auf der Instanzseite aufgeführt, damit sie explizit widerrufen werden kann.

### Bearbeiten einer Instanz {#editing-an-instance}

Öffnen Sie **Mailcow instances** und klicken Sie auf **Edit**. Das Feld für den API-Schlüssel ist immer leer — der gespeicherte Schlüssel wird niemals angezeigt. Lassen Sie es leer, um den aktuellen Schlüssel beizubehalten, oder geben Sie einen neuen ein, um ihn zu ersetzen. Geänderte Verbindungsdetails werden vor dem Speichern gegen Mailcow geprüft.

### Löschen einer Instanz {#deleting-an-instance}

Öffnen Sie **Mailcow instances** und klicken Sie auf **Delete**.

Das Löschen einer Instanz entfernt ihren verschlüsselten API-Schlüssel und ihre Postfach-Bindungen. **Archiv-Konten und deren archivierte E-Mails bleiben erhalten**, aber die Konten werden deaktiviert.

Das Löschen wird **verweigert, solange noch ein verwaltetes App-Passwort in Mailcow aktiv ist**: Das Löschen der Instanz würde den API-Schlüssel löschen, der zum Widerrufen erforderlich ist. Widerrufen Sie zuerst alle verwalteten App-Passwörter auf der Postfachseite.

## Audit-Protokoll {#audit-logging}

Jede administrative Operation wird mit dem ausführenden Benutzer im Zugriffsprotokoll gespeichert:

- Verbinden einer Instanz,
- Aktualisieren einer Instanz,
- Löschen einer Instanz,
- Aktivieren von Postfächern (mit der Anzahl der bereitgestellten und der Anzahl der fehlgeschlagenen),
- Widerrufen eines App-Passworts.

## Mailbox-Limit {#mailbox-limit}

Ein einzelner Aktivierungslauf kann maximal `Mailcow__MaxSelectedMailboxes` Postfächer
bereitstellen (Standard `1000`). Dies schützt vor versehentlicher Massenbereitstellung und übermäßiger
Mailcow-API- und IMAP-Auslastung. Führen Sie den Vorgang bei größeren Instanzen mehrmals aus.

Jedes Postfach erfordert mehrere API-Roundtrips plus eine IMAP-Verifizierung, und diese werden
sequenziell verarbeitet. Die Bereitstellung von hunderten Postfächern in einer Anfrage kann zu
Reverse-Proxy- oder Browser-Timeouts führen; teilen Sie die Auswahl in Batches auf.

## Sicherheitshinweise {#security-notes}

- Der API-Schlüssel verbleibt serverseitig. Er wird als `X-API-Key`-Header gesendet, ist in den Protokollen unkenntlich gemacht und wird niemals an den Browser zurückgegeben.
- Die Antworten der Mailcow-App-Passwörter können Klartextpasswörter in ihren `log`-Feldern ausgeben. Der Mail Archiver protokolliert absichtlich keine Antwortkörper und gibt API-Fehler nicht an den Browser weiter; Fehler werden als lokalisierte Meldungen gemeldet.
- Eine HTTP-200-Antwort wird nicht als Erfolg gewertet: Mutationen erfordern Mailcows pro-Ergebnis `type: success`.
- Die IMAP-Berechtigung ist nicht von Natur aus schreibgeschützt. Bereitgestellte Konten haben keine serverseitige Löschrichtlinie konfiguriert, und Administratoren behalten die Kontrolle über die Kontoeinstellungen. Diese Funktion archiviert Nachrichten, nicht die Mailcow-Konfiguration oder Sicherungen.

### Hostnamen und private Netzwerke {#hostnames-and-private-networks}

Die Mailcow-URL und der IMAP-Host werden von einem Administrator eingegeben und können auf einen privaten Adressraum verweisen. Dies ist beabsichtigt und wird unterstützt: Der Mail Archiver wird häufig direkt neben dem Mailserver bereitgestellt, daher sind `mail.example.lan`, `192.168.10.20` oder `10.0.0.5` normale Werte. **Keine private, Loopback- oder Link-Local-Adresse wird blockiert.**

Die Vertrauensgrenze ist die Administratorrolle, nicht die Netzwerkadresse. `Mail Accounts` steht Selbstverwaltern zur Verfügung, die möglicherweise bereits einen beliebigen IMAP-Server für ihre eigenen Konten konfigurieren; der Mailcow-Abschnitt ist auf Administratoren beschränkt, und ein Administrator hat bereits uneingeschränkten Zugriff auf jedes Archiv-Konto in der Installation. Die Einschränkung des IMAP-Hosts würde daher keine Privilegiengrenze schließen, während sie die beabsichtigte On-Premise-Bereitstellung unterbrechen würde.

Zwei Einschränkungen gelten weiterhin:

- Die Mailcow-URL muss ein reiner HTTPS-Origin sein. Pfade, Query-Strings, Benutzerinformationen und Redirects werden abgelehnt, sodass der API-Schlüssel nicht an einen anderen Origin weitergeleitet werden kann.
- Der IMAP-Host muss ein Hostname ohne Schema oder Port sein, und die Bereitstellung verbindet sich immer mit TLS (`SslOnConnect`).

Wenn Sie einschränken müssen, welche Hosts der Mail Archiver erreichen darf, erzwingen Sie dies auf der Netzwerkschicht — Egress-Firewall-Regeln oder eine DNS-Richtlinie für den Container — anstatt sich auf die Eingabevalidierung in der Anwendung zu verlassen.

## Fehlerbehebung {#troubleshooting}

| Symptom | Ursache und Abhilfe |
| --- | --- |
| *Encryption required* beim Verbinden oder Aktivieren | `Security__CredentialEncryptionKey` ist nicht gesetzt oder ungültig. Konfigurieren Sie einen Base64-kodierten 32-Byte-Schlüssel und starten Sie neu. |
| *HTTPS origin required* | Die URL enthält einen Pfad, eine Query-String oder eingebettete Anmeldedaten. Geben Sie die reine Origin ein, z. B. `https://mail.example.com`. |
| *IMAP host invalid* | Der IMAP-Host muss ein Hostname ohne Schema oder Port sein. |
| *Instance already connected* | Eine andere Instanz mit derselben URL existiert bereits. Bearbeiten Sie stattdessen diese Instanz. |
| *API request failed (401/403)* | Der API-Schlüssel verfügt nicht über die erforderlichen Berechtigungen oder die IP-Beschränkung schließt den Mail Archiver Host nicht ein. |
| *API request rejected* | Mailcow hat eine `error`/`danger`/`warning` Zeile zurückgegeben. Überprüfen Sie die Mailcow-Protokolle; der Mail Archiver leitet Response-Bodies nicht weiter. |
| *IMAP verification failed* | Falscher IMAP-Host oder Port, ein nicht vertrauenswürdiges Zertifikat oder die App-Passwort-Funktion ist in Mailcow deaktiviert. |
| *IMAP account exists* | Ein unabhängiges IMAP-Konto archiviert diese Adresse bereits. Verwenden Sie dieses Konto, anstatt ein zweites Archiv zu erstellen. |
| *Revoke previous password* | Das Archiv-Konto für dieses Postfach wurde gelöscht. Widerrufen Sie zuerst das verbleibende App-Passwort und aktivieren Sie es dann erneut. |
| Postfach bleibt auf **Pending** | Die Mailcow API hat ein Timeout verursacht. Laden Sie die Seite neu und aktivieren Sie das Postfach erneut; es wird kein doppeltes App-Passwort erstellt. |
| Löschen wird verweigert | Verwaltete App-Passwörter sind noch aktiv. Widerrufen Sie diese zuerst auf der Postfach-Seite. |

## Verwendete API-Endpunkte {#api-endpoints-used}

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## Datenbankänderungen {#database-changes}

Die Migration fügt nur die Tabellen `MailcowInstances` und `MailcowMailboxes` zusammen mit ihren Indizes und Fremdschlüsseln hinzu. Bestehende Postfach-Tabellen und archivierte Daten bleiben erhalten. Das Entfernen eines Archiv-Kontos setzt die `MailAccountId` der Bindung auf `NULL`, sodass die Remote-Anmeldedaten anschließend noch widerrufen werden können.
