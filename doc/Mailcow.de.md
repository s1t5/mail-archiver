# Mailcow-Instanz-Archivierung {#mailcow-instance-archiving}

[← Zurück zum Dokumentationsindex](index.md)

## Übersicht {#overview}

Mail Archiver kann die Postfächer einer [Mailcow](https://mailcow.email)-Instanz zentral archivieren, ohne die persönlichen Passwörter der Postfachbesitzer zu erfassen.

Ein Administrator verbindet die Instanz einmalig mit ihrem API-Key. Mail Archiver listet dann die Postfächer auf und erstellt für jedes zur Archivierung ausgewählte Postfach ein **dediziertes App-Passwort**, das auf `imap_access` beschränkt ist. Dieses App-Passwort wird vom bestehenden IMAP-Provider verwendet, um das Postfach zu archivieren, und kann jederzeit wieder entzogen werden.

Administratoren erreichen den Instanz-Manager über **Konto hinzufügen → Mailcow** oder über das Aktionsmenü eines Kontos, das bereits von Mailcow verwaltet wird. Der Manager ist nur für Administratoren zugänglich, da der gespeicherte API-Key die Anmeldedaten für jedes Postfach in der Instanz verwalten kann.

## Erstellen von Konten aus Mailcow {#creating-accounts-from-mailcow}

In **Mail-Konten → Konto hinzufügen** können Administratoren **Mailcow** als Provider auswählen. Das Formular bietet dann die verbundenen Instanzen und eine Aktion **Postfächer verwalten** an. Durch Auswahl von Postfächern dort werden gewöhnliche IMAP-Archivkonten mit dedizierten App-Passwörtern erstellt; es wird kein separates Mailcow-Konto gespeichert. Falls noch keine Instanz existiert, öffnet **Mailcow-Instanzen verwalten** den Instanz-Manager, wo eine Verbindung hergestellt werden kann.

## Verknüpfen eines bestehenden Kontos {#linking-an-existing-account}

In der Bearbeitungsansicht eines IMAP-Kontos können Administratoren den Provider auf **Mailcow** umstellen und eine Instanz auswählen. Das Konto behält den IMAP-Provider, wird aber in die Mailcow-Verwaltung übernommen: Mail Archiver erstellt ein dediziertes App-Passwort, verifiziert es über IMAP und ersetzt das gespeicherte Passwort des Kontos. Das Konto erscheint dann mit einer Mailcow-Aktion in der Kontoliste.

Um die Mailcow-Verwaltung zu verlassen, wählen Sie einen anderen Provider aus und speichern Sie. Das App-Passwort wird entzogen und das lokale Passwort gelöscht. Beim Zurückwechseln zu IMAP geben Sie ein neues IMAP-Passwort ein; der Wechsel zu Microsoft 365 oder einem Import-only-Konto liefert die eigenen Anmeldedaten. Nicht-Administratoren können weiterhin die nicht-anmeldedatenbezogenen Einstellungen eines von Mailcow verwalteten Kontos bearbeiten, können es aber nicht verknüpfen oder entknüpfen.

## Voraussetzungen {#prerequisites}

### Verschlüsselung der Anmeldedaten muss aktiviert sein {#credential-encryption-must-be-enabled}

Mail Archiver speichert den Mailcow API-Key und jedes generierte App-Passwort verschlüsselt auf dem Datenträger unter Verwendung der vorhandenen Funktion zur Verschlüsselung von Anmeldedaten. Setzen Sie

```
Security__CredentialEncryptionKey=<base64-encoded 32-byte key>
```

bevor Sie eine Instanz verbinden. Das Erstellen einer Instanz und das Aktivieren von Postfächern wird beides verweigert, solange die Verschlüsselung deaktiviert ist, sodass Anmeldedaten niemals im Klartext durch diese Funktion geschrieben werden können. Bewahren Sie den Schlüssel außerhalb der Datenbank auf und nehmen Sie ihn in Ihre sichere Sicherung auf — ohne ihn können gespeicherte App-Passwörter nicht entschlüsselt und Postfächer nicht bereitgestellt werden. Details finden Sie unter [Setup](Setup.md#-security-settings).

### Mailcow API-Zugriff {#mailcow-api-access}

Erstellen Sie einen API-Key in Mailcow unter **Konfiguration → Zugriff → API**. Dem Key muss erlaubt werden,

- Postfächer aufzulisten (`get/mailbox/all`),
- App-Passwörter aufzulisten, zu erstellen und zu löschen (`get/app-passwd/all`, `add/app-passwd`,
  `delete/app-passwd`).

Beschränken Sie die API in den Mailcow-API-Einstellungen auf die IP-Adresse des Mail Archiver Hosts. Der App-Passwort-Endpunkt von Mailcow erfordert, dass die Funktion **App Passwörter** für die Instanz aktiviert ist.

### Netzwerkzugriff {#network-access}

- **HTTPS** zur Mailcow-Origin (zum Beispiel `https://mail.example.com`).
- **IMAP über TLS** zum IMAP-Host, normalerweise Port `993`. Plaintext-IMAP wird für die Bereitstellung nicht unterstützt.

## Schritt-für-Schritt-Anwendung {#step-by-step-usage}

### Verbinden einer Instanz {#connecting-an-instance}

1. Melden Sie sich als Administrator bei Mail Archiver an.
2. Öffnen Sie **Mail-Konten** und klicken Sie auf **Mailcow-Instanzen**.
3. Klicken Sie auf **Mailcow-Instanz verbinden**.
4. Geben Sie die Verbindungsdetails ein:
   - **Kontoname** — ein Anzeigename für die Instanz in Mail Archiver.
   - **Mailcow URL** — nur die HTTPS-Origin, zum Beispiel `https://mail.example.com`.
     Pfade, Query-Strings und eingebettete Anmeldedaten werden abgelehnt.
   - **API-Key** — der in Mailcow erstellte Schlüssel.
   - **IMAP-Host** — der Hostname, der für die Archivierung verwendet wird, zum Beispiel `mail.example.com`.
     Geben Sie nur den Hostnamen ohne Schema oder Port ein.
   - **IMAP TLS Port** — Standard ist `993`.
5. Klicken Sie auf **Verbinden**. Mail Archiver verifiziert den API-Key durch Auflisten der Postfächer, bevor die Instanz gespeichert wird. Bei Erfolg wird die Postfachauswahl geöffnet.

Der API-Key wird verschlüsselt gespeichert und niemals wieder in das Formular oder in eine Fehlermeldung zurückgegeben. HTTP-Redirects sind für API-Aufrufe deaktiviert, sodass der Key nicht an eine andere Origin weitergeleitet werden kann.

### Aktivieren von Postfächern {#activating-mailboxes}

1. Filtern Sie auf der Postfachseite optional die Liste mit dem Suchfeld. Der Filter gleicht die Adresse und den Anzeigenamen ab und ist nicht fallsensitiv.
2. Wählen Sie die zu archivierenden Postfächer aus. **Alle auswählen** und **Auswahl aufheben** wirken sich nur auf die aktuell sichtbaren (gefilterten) Postfächer aus, daher ist das Filtern ein sicherer Weg, um eine Teilmenge auszuwählen.
3. Klicken Sie auf **Archivierung aktivieren**.

Für jedes ausgewählte Postfach führt Mail Archiver Folgendes aus:

1. validiert die Adresse erneut gegen die aktuelle Mailcow-Postfachliste und ignoriert deaktivierte
   Postfächer,
2. erstellt ein Archivkonto mit dem Namen `{instance name} – {mailbox display name}`,
3. generiert ein zufälliges App-Passwort mit dem Namen `MailArchiver-<unique id>`, das auf
   `imap_access` beschränkt ist,
4. verifiziert die Anmeldedaten mit einem IMAP-Login über TLS,
5. und aktiviert erst dann die Synchronisierung.

Falls die IMAP-Verifizierung fehlschlägt, bleibt das Konto deaktiviert und kann erneut versucht werden.

### Status-Badges der Postfächer {#mailbox-status-badges}

Die Auswahlseite kennzeichnet jedes Postfach, damit klar ist, warum ein Kontrollkästchen verfügbar ist oder nicht:

| Badge | Bedeutung | Auswählbar |
| --- | --- | --- |
| **Verfügbar** | In Mailcow gelistet, noch nicht bereitgestellt | ja |
| **Aktiv** | Bereitgestellt und synchronisiert | nein |
| **Ausstehend** | Bereitstellung gestartet, aber nicht abgeschlossen (wiederholbar) | ja |
| **Entzogen** | App-Passwort wurde entzogen; kann wieder aktiviert werden | ja |
| **Bestehend** | Ein unabhängiges IMAP-Konto mit dieser Adresse existiert bereits | nein |
| **Deaktiviert** | Das Postfach ist in Mailcow deaktiviert | nein |

### Wiederholen eines ausstehenden Postfachs {#retrying-a-pending-mailbox}

Die generierten Anmeldedaten und der eindeutige App-Passwort-Name werden **vor** dem Aufruf der Mailcow API dauerhaft gespeichert. Wenn eine Anfrage mehrdeutig fehlschlägt — zum Beispiel ein Timeout, nachdem das App-Passwort erstellt wurde — bleibt das Postfach als **Ausstehend** sichtbar. Laden Sie die Seite neu und wählen Sie es erneut aus: Mail Archiver sucht zuerst nach dem App-Namen und erstellt nur dann ein neues App-Passwort, wenn keines existiert. Ein Wiederholungsversuch führt nicht zu duplizierten Anmeldedaten.

Die Bereitstellung und der Entzug für eine Instanz werden unter Verwendung von PostgreSQL Advisory Locks serialisiert, sodass zwei gleichzeitige Operationen nicht dasselbe Postfach zweimal bereitstellen können.

### Entziehen des Zugriffs {#revoking-access}

Klicken Sie neben einem Postfach auf **App-Passwort entziehen**. Dies

- deaktiviert die zukünftige Synchronisierung,
- löscht das verwaltete App-Passwort in Mailcow,
- und löscht das lokale Passwort.

**Archivierte E-Mails bleiben erhalten.** Nur das verwaltete App-Passwort wird entfernt — andere App-Passwörter, die zum Postfach gehören, werden nicht berührt. Bereits laufende Synchronisierungsoperationen können mit ihrer bestehenden Verbindung abgeschlossen werden.

Falls die Remote-Anfrage fehlschlägt, bleibt das Konto deaktiviert und der Entzug kann erneut versucht werden.

Das Löschen eines Archivkontos in **Mail-Konten** entfernt die Remote-Anmeldedaten nicht stillschweigend. Die Bindung bleibt unter **Verbleibende App-Passwörter** auf der Instanzseite aufgeführt, damit sie explizit entzogen werden kann.

### Bearbeiten einer Instanz {#editing-an-instance}

Öffnen Sie **Mailcow-Instanzen** und klicken Sie auf **Bearbeiten**. Das Feld für den API-Key ist immer leer — der gespeicherte Key wird niemals angezeigt. Lassen Sie es leer, um den aktuellen Key beizubehalten, oder geben Sie einen neuen ein, um ihn zu ersetzen. Geänderte Verbindungsdetails werden vor dem Speichern gegen Mailcow geprüft.

### Löschen einer Instanz {#deleting-an-instance}

Öffnen Sie **Mailcow-Instanzen** und klicken Sie auf **Löschen**.

Das Löschen einer Instanz entfernt deren verschlüsselten API-Key und deren Postfach-Bindungen. **Archivkonten und deren archivierte E-Mails bleiben erhalten**, aber die Konten werden deaktiviert.

Das Löschen wird **verweigert, solange ein verwaltetes App-Passwort in Mailcow noch aktiv ist**: Das Entfernen der Instanz würde den API-Key löschen, der zum Entziehen des Passworts erforderlich ist. Entziehen Sie zuerst alle verwalteten App-Passwörter auf der Postfachseite.

## Audit-Protokoll {#audit-logging}

Jede administrative Operation wird mit dem ausführenden Benutzer in das Zugriffsprotokoll geschrieben:

- Verbinden einer Instanz,
- Aktualisieren einer Instanz,
- Löschen einer Instanz,
- Aktivieren von Postfächern (mit der Anzahl der bereitgestellten und der Anzahl der fehlgeschlagenen),
- Entziehen eines App-Passworts.

## Postfach-Limit {#mailbox-limit}

Ein einzelner Aktivierungsdurchlauf kann maximal `Mailcow__MaxSelectedMailboxes` Postfächer bereitstellen
(Standard `1000`). Dies schützt vor versehentlicher Massenbereitstellung und übermäßiger Mailcow API- und IMAP-Last. Führen Sie den Vorgang bei größeren Instanzen mehrmals aus.

Jedes Postfach erfordert mehrere API-Rundreisen plus eine IMAP-Verifizierung, und diese werden nacheinander verarbeitet. Die Bereitstellung vieler hundert Postfächer in einer Anfrage kann die Timeouts des Reverse-Proxys oder des Browsers überschreiten; teilen Sie die Auswahl in Chargen auf.

## Sicherheitshinweise {#security-notes}

- Der API-Key verbleibt serverseitig. Er wird als `X-API-Key`-Header gesendet, aus den Protokollen entfernt und niemals an den Browser zurückgegeben.
- Die App-Passwort-Antworten von Mailcow können Klartext-Passwörter in ihren `log`-Feldern zurückgeben. Mail Archiver protokolliert absichtlich keine Antwortkörper und gibt API-Fehler nicht an den Browser weiter; Fehler werden als lokalisierte Nachrichten gemeldet.
- Eine HTTP 200-Antwort wird nicht als Erfolg behandelt: Mutationen erfordern Mailcows pro Ergebnis `type: success`.
- Die IMAP-Berechtigung ist nicht von Natur aus schreibgeschützt. Bereitgestellte Konten haben keine serverseitige Löschrichtlinie konfiguriert, und Administratoren behalten die Kontrolle über die Kontoeinstellungen. Diese Funktion archiviert Nachrichten, nicht die Mailcow-Konfiguration oder Sicherungen.

### Hostnamen und private Netzwerke {#hostnames-and-private-networks}

Die Mailcow URL und der IMAP-Host werden von einem Administrator eingegeben und können auf privaten Adressraum verweisen. Dies ist beabsichtigt und wird unterstützt: Mail Archiver wird häufig neben dem Mailserver bereitgestellt, sodass `mail.example.lan`, `192.168.10.20` oder `10.0.0.5`
normale Werte sind. **Keine private, Loopback- oder Link-Local-Adresse wird blockiert.**

Die Vertrauensgrenze ist die Administratorrolle, nicht die Netzwerkadresse. `Mail Accounts`
steht Selbstverwaltern zur Verfügung, die möglicherweise bereits einen beliebigen IMAP-Server für ihre eigenen Konten konfigurieren; der Mailcow-Abschnitt ist auf Administratoren beschränkt, und ein Administrator hat bereits uneingeschränkten Zugriff auf jedes Archivkonto in der Installation. Die Beschränkung des IMAP-Hosts würde daher keine Privilegierungsgrenze schließen, während sie die beabsichtigte On-Premise-Bereitstellung unterbrechen würde.

Zwei Einschränkungen gelten weiterhin:

- Die Mailcow URL muss eine reine HTTPS-Origin sein. Pfade, Query-Strings, Benutzerinformationen und Redirects werden abgelehnt, sodass der API-Key nicht an eine andere Origin weitergeleitet werden kann.
- Der IMAP-Host muss ein Hostname ohne Schema oder Port sein, und die Bereitstellung verbindet sich immer mit TLS (`SslOnConnect`).

Wenn Sie einschränken müssen, welche Hosts Mail Archiver erreichen darf, erzwingen Sie dies auf der Netzwerkschicht — durch Egress-Firewall-Regeln oder eine DNS-Richtlinie für den Container — anstatt sich auf die Eingabevalidierung in der Anwendung zu verlassen.

## Fehlerbehebung {#troubleshooting}

| Symptom | Ursache und Abhilfe |
| --- | --- |
| *Encryption required* beim Verbinden oder Aktivieren | `Security__CredentialEncryptionKey` ist nicht gesetzt oder ungültig. Konfigurieren Sie einen Base64-kodierten 32-Byte-Schlüssel und starten Sie neu. |
| *HTTPS origin required* | Die URL enthält einen Pfad, Query-String oder eingebettete Anmeldedaten. Geben Sie die reine Origin ein, z. B. `https://mail.example.com`. |
| *IMAP host invalid* | Der IMAP-Host muss ein Hostname ohne Schema oder Port sein. |
| *Instance already connected* | Eine andere Instanz mit derselben URL existiert. Bearbeiten Sie stattdessen diese Instanz. |
| *API request failed (401/403)* | Dem API-Key fehlen die erforderlichen Berechtigungen oder die IP-Beschränkung enthält den Mail Archiver Host nicht. |
| *API request rejected* | Mailcow hat eine `error`/`danger`/`warning`-Zeile zurückgegeben. Überprüfen Sie die Mailcow-Protokolle; Mail Archiver leitet Antwortkörper nicht weiter. |
| *IMAP verification failed* | Falscher IMAP-Host oder -Port, ein nicht vertrauenswürdiges Zertifikat oder die App-Passwort-Funktion ist in Mailcow deaktiviert. |
| *IMAP account exists* | Ein unabhängiges IMAP-Konto archiviert bereits diese Adresse. Verwenden Sie dieses Konto, anstatt ein zweites Archiv zu erstellen. |
| *Revoke previous password* | Das Archivkonto für dieses Postfach wurde gelöscht. Entziehen Sie zuerst das verbleibende App-Passwort und aktivieren Sie es dann erneut. |
| Postfach bleibt **Ausstehend** | Die Mailcow API hat ein Timeout verursacht. Laden Sie die Seite neu und aktivieren Sie das Postfach erneut; es wird kein doppeltes App-Passwort erstellt. |
| Löschen wird verweigert | Verwaltete App-Passwörter sind noch aktiv. Entziehen Sie diese zuerst auf der Postfachseite. |

## Verwendete API-Endpunkte {#api-endpoints-used}

- `GET /api/v1/get/mailbox/all`
- `GET /api/v1/get/app-passwd/all/{mailbox}`
- `POST /api/v1/add/app-passwd`
- `POST /api/v1/delete/app-passwd`

## Datenbankänderungen {#database-changes}

Die Migration fügt nur die Tabellen `MailcowInstances` und `MailcowMailboxes` zusammen mit ihren Indizes und Fremdschlüsseln hinzu. Bestehende Postfachtabellen und archivierte Daten bleiben erhalten. Das Entfernen eines Archivkontos setzt den `MailAccountId` der Bindung auf `NULL`, sodass die Remote-Anmeldedaten danach noch entzogen werden können.
