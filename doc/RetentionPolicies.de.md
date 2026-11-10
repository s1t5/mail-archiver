# 🗑️ Aufbewahrungsrichtlinien {#retention-policies}

[← Zurück zum Dokumentationsindex](index.md)

## Übersicht {#overview}

Mail Archiver bietet eine umfassende Verwaltung von Aufbewahrungsrichtlinien, um Ihnen zu helfen, die Speichernutzung zu kontrollieren und gleichzeitig die Compliance-Anforderungen einzuhalten. Das System unterstützt zwei Arten von Aufbewahrungsrichtlinien:

1. **Server-Aufbewahrungsrichtlinie**: Steuert das automatische Löschen von E-Mails vom Mailserver
2. **Lokale Archiv-Aufbewahrungsrichtlinie**: Steuert das automatische Löschen von E-Mails aus dem lokalen Archiv

Dieser duale Ansatz gibt Ihnen eine feingliedrige Kontrolle über die Speicherverwaltung, während gleichzeitig sichergestellt wird, dass die Datenkonsistenz und die Compliance-Anforderungen erfüllt werden.

## 🗂️ Wo eine Nachricht gespeichert ist {#where-a-message-lives}

Die beiden Richtlinien wirken auf zwei verschiedene Kopien derselben Nachricht, weshalb sie unabhängig voneinander konfiguriert werden:

```mermaid
stateDiagram-v2
    direction LR
    [*] --> OnServer : mail arrives
    OnServer --> OnServerAndArchived : a sync archives it
    OnServerAndArchived --> ArchivedOnly : DeleteAfterDays elapses<br/>IMAP EXPUNGE on the server
    ArchivedOnly --> Removed : LocalRetentionDays elapses<br/>row and attachments deleted
    OnServerAndArchived --> Removed : LocalRetentionDays elapses
    Removed --> [*]

    note right of OnServerAndArchived
        Local retention must be greater than or
        equal to server retention, so the archived
        copy is never removed before the server copy.
    end note
```

Die Server-Aufbewahrung bereinigt das Postfach; die lokale Aufbewahrung bereinigt das Archiv. Das Löschen vom Server berührt die archivierte Kopie **nicht** – genau das ist der Zweck eines Archivs. Die Validierungsregel (die lokale Aufbewahrung muss mindestens der Server-Aufbewahrung entsprechen) existiert, damit das Archiv immer länger existiert als das Postfach, aus dem es erstellt wurde.

## Hauptmerkmale {#key-features}

- **Automatische Bereinigung**: Automatische Löschung alter E-Mails während der Synchronisierungszyklen
- **Detaillierte Protokollierung**: Umfassendes Audit-Protokoll aller Löschvorgänge
- **Konfiguration pro Konto**: Konfigurieren Sie Aufbewahrungsrichtlinien individuell für jedes E-Mail-Konto

## Server-Aufbewahrungsrichtlinie {#server-retention-policy}

### Zweck {#purpose}
Die Server-Aufbewahrungsrichtlinie löscht E-Mails automatisch nach einer festgelegten Anzahl von Tagen vom Mailserver.

### Konfiguration {#configuration}
- **Feld**: `Delete After Days`
- **Position**: Email Accounts → Edit → Email Deletion section
- **Werte**: Anzahl der Tage (leer = deaktiviert)

### Funktionsweise {#how-it-works}
1. Während der Synchronisierung identifiziert das System E-Mails, die älter als der konfigurierte Aufbewahrungszeitraum sind
2. Identifizierte E-Mails werden für die Löschung auf dem Mailserver markiert
3. Die Löschung erfolgt über den IMAP EXPUNGE-Befehl

### Anforderungen {#requirements}
> 🚨 **Wichtiger Hinweis zu Aufbewahrungsrichtlinien**
> - Erfordert IMAP Expunge-Unterstützung vom Mailserver, um E-Mails dauerhaft zu löschen
> - Bei Gmail-Konten muss Auto-Expunge in den Gmail-Einstellungen unter dem Tab „Weiterleitung und POP/IMAP“ deaktiviert sein! (Weitere Details finden Sie unter [Gmail Best Practices](GmailBestPractices.md))

## Lokale Archiv-Aufbewahrungsrichtlinie {#local-archive-retention-policy}

### Zweck {#purpose}
Die lokale Archiv-Aufbewahrungsrichtlinie löscht E-Mails automatisch aus dem lokalen Archiv nach einer festgelegten Anzahl von Tagen.

### Konfiguration {#configuration}
- **Feld**: `Local Retention Days`
- **Position**: Email Accounts → Edit → Email Deletion section
- **Werte**: Anzahl der Tage (leer = deaktiviert, muss ≥ Server Retention sein)

### Funktionsweise {#how-it-works}
1. Während der Synchronisierung identifiziert das System archivierte E-Mails, die älter als der konfigurierte Aufbewahrungszeitraum sind
2. Identifizierte E-Mails werden aus der lokalen Datenbank gelöscht
3. Zugehörige Anhänge werden ebenfalls entfernt
4. Ein zusammenfassender Log-Eintrag wird in den Access Logs erstellt

### Validierungsregeln {#validation-rules}
1. **Server Retention erforderlich**: Die lokale Aufbewahrung kann nur konfiguriert werden, wenn auch die Server-Aufbewahrung festgelegt ist
2. **Aufbewahrungshierarchie**: Die lokalen Aufbewahrungstage müssen größer als oder gleich den Server-Aufbewahrungstagen sein
3. **Sicherheit geht vor**: Dies verhindert, dass E-Mails aus dem lokalen Archiv gelöscht werden, bevor sie vom Mailserver gelöscht werden

## Konfigurationsschritte {#configuration-steps}

### 1. E-Mail-Konto bearbeiten {#1-edit-email-account}

1. Navigieren Sie zu **Email Accounts** → **Edit Account**
2. Scrollen Sie zum Abschnitt **Email Deletion**
3. Legen Sie **Delete After Days** fest (Server-Aufbewahrung)
4. Legen Sie **Local Retention Days** fest (lokale Archiv-Aufbewahrung)
5. Speichern Sie die Konfiguration

## Überwachung und Protokollierung {#monitoring-and-logging}

### Zugriffsprotokolle {#access-logs}

Alle Löschvorgänge von Aufbewahrungsrichtlinien bezüglich des lokalen Archivs werden mit detaillierten Informationen in den Zugriffsprotokollen protokolliert:

## Best Practices {#best-practices}

### Compliance-Überlegungen {#compliance-considerations}

1. **Rechtliche Anforderungen**: 
   - Recherchieren Sie die Anforderungen an die Aufbewahrung für Ihre Branche
   - Dokumentieren Sie Aufbewahrungsrichtlinien
   - Regelmäßige Compliance-Audits

2. **Audit-Trails**: 
   - Führen Sie detaillierte Zugriffsprotokolle
   - Regelmäßige Protokollanalyse
   - Exportieren Sie Protokolle für externe Audits

3. **Massenlöschung:** Die filterbasierte Massenlöschung überspringt gesperrte E-Mails und meldet sie als übersprungen im Job-Status. Das Sperren einer Nachricht ist daher ein effektiver Schutz gegen versehentliche Massenlöschung.

4. **Dokumentation**: 
   - Führen Sie eine Dokumentation der Aufbewahrungsrichtlinien
   - Regelmäßige Überprüfung der Richtlinien
   - Genehmigungen durch Stakeholder
