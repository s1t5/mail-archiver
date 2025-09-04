# M365 Sync Problem - Diagnose und Lösung

## Identifizierte Probleme

### 1. **Syntax-Fehler in GraphEmailService.cs**
- **Problem**: Doppelter `try`-Block in der `ArchiveGraphEmailAsync`-Methode führte zu Kompilierungsfehlern
- **Lösung**: Syntax korrigiert und redundanten Code entfernt

### 2. **Unzureichende Fehlerbehandlung**
- **Problem**: Datenbankfehler wurden nicht ordnungsgemäß abgefangen und protokolliert
- **Lösung**: Verbesserte Fehlerbehandlung mit detailliertem Logging hinzugefügt

### 3. **Fehlende Debug-Informationen**
- **Problem**: Mangelnde Transparenz über den Synchronisationsprozess
- **Lösung**: Umfassendes Logging für jeden Schritt des E-Mail-Archivierungsprozesses

### 4. **Potenzielle Authentifizierungsprobleme**
- **Problem**: OAuth-Token-Erwerb könnte stillschweigend fehlschlagen
- **Lösung**: Detaillierte Protokollierung der Authentifizierungsschritte

## Durchgeführte Verbesserungen

### GraphEmailService.cs Verbesserungen:

1. **Syntax-Korrekturen**
   - Entfernung doppelter try-Blöcke
   - Korrekte Strukturierung der Exception-Behandlung

2. **Erweiterte Protokollierung**
   ```csharp
   _logger.LogDebug("Processing message {MessageId} for account {AccountName}, Subject: {Subject}", 
       messageId, account.Name, message.Subject ?? "No Subject");
   ```

3. **Verbesserte Fehlerbehandlung**
   ```csharp
   try
   {
       var emailExists = await _context.ArchivedEmails
           .AnyAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);
   }
   catch (Exception ex)
   {
       _logger.LogError(ex, "Error checking if email {MessageId} exists for account {AccountName}: {Message}", 
           messageId, account.Name, ex.Message);
       return false;
   }
   ```

4. **Detaillierte Sync-Informationen**
   ```csharp
   _logger.LogInformation("Syncing folder {FolderName} for account {AccountName} since {LastSync} (UTC)", 
       folder.DisplayName, account.Name, lastSync);
   ```

## Häufige Ursachen für fehlende E-Mail-Synchronisation

### 1. **Authentifizierungsprobleme**
- Ungültige oder abgelaufene Client-Credentials
- Falsche Tenant-ID
- Fehlende Microsoft Graph-Berechtigungen

### 2. **Datenbankprobleme**
- Verbindungsfehler zur PostgreSQL-Datenbank
- Unzureichende Berechtigungen
- Tabellensperrungen

### 3. **Microsoft Graph API-Limits**
- Rate Limiting
- Komplexe Abfragefehler
- Zeitüberschreitungen

### 4. **Konfigurationsprobleme**
- Falsche E-Mail-Adresse im Account
- Ausgeschlossene Ordner
- Ungültige LastSync-Werte

## Überprüfung der Lösung

### Logs überprüfen
Die Anwendung sollte nun detaillierte Logs produzieren:

```
[Information] Starting Graph API sync for M365 account: TestAccount
[Information] Found 15 folders for M365 account: TestAccount
[Information] Syncing folder Inbox for account TestAccount since 2025-02-03T13:26:00.000Z (UTC)
[Debug] Processing message msg123 for account TestAccount, Subject: Test Email
[Debug] Archiving new email msg123 for account TestAccount
[Information] Archived Graph API email: Test Email, From: test@example.com, To: user@example.com, Account: TestAccount
```

### Fehlerbehebungsschritte

1. **Überprüfen Sie die Logs** auf Error-Meldungen
2. **Testen Sie die Authentifizierung** mit TestConnectionAsync
3. **Überprüfen Sie die Datenbankverbindung**
4. **Validieren Sie die M365-Berechtigungen**

### Erforderliche Microsoft Graph-Berechtigungen

Für M365-Konten werden folgende Anwendungsberechtigungen benötigt:
- `Mail.Read` (Application permission)
- `User.Read.All` (Application permission)

## Nächste Schritte

1. **Neustarten der Anwendung** um die Änderungen zu aktivieren
2. **Überwachung der Logs** während der nächsten Synchronisation
3. **Überprüfung der Datenbank** auf neue ArchivedEmails-Einträge
4. **Test der Verbindung** über die Anwendungsschnittstelle

## Monitoring

### Wichtige Log-Level
- `Information`: Allgemeine Sync-Fortschritte
- `Debug`: Detaillierte Message-Verarbeitung
- `Error`: Kritische Fehler, die eine Behebung erfordern
- `Warning`: Potenzielle Probleme (z.B. API-Limits)

### Dashboard-Überwachung
Überwachen Sie die folgenden Metriken:
- Anzahl der neuen E-Mails pro Sync
- Anzahl der fehlgeschlagenen E-Mails
- Sync-Dauer pro Account
- Aktive Jobs im System
