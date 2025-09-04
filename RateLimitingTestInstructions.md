# Rate Limiting für 2FA - Test-Anweisungen

## 🔒 Implementierte Sicherheitsverbesserungen

### Rate Limiting-Konfiguration
- **2FA-Verifikation**: 5 Versuche pro 15 Minuten pro IP/Benutzer-Kombination
- **Globale Requests**: 100 Anfragen pro Minute pro IP
- **Policy**: "TwoFactorVerify" auf `TwoFactor/Verify` POST-Endpoint

### Sicherheits-Logging
- ✅ Erfolgreiche 2FA-Verifikationen werden geloggt
- ✅ Fehlgeschlagene 2FA-Versuche werden mit IP und Token-Maske geloggt
- ✅ Backup-Code-Verwendung wird überwacht
- ✅ Ungültige Backup-Code-Versuche werden geloggt

## 🧪 Testing

### 1. Rate Limiting-Test
```bash
# 1. Starte die Anwendung
dotnet run

# 2. Melde dich mit einem 2FA-aktivierten Benutzer an
# 3. Versuche mehr als 5 falsche 2FA-Codes innerhalb von 15 Minuten
# 4. Erwartetes Ergebnis: HTTP 429 "Rate limit exceeded" nach dem 5. Versuch
```

### 2. Logging-Verifikation
```bash
# Überprüfe die Logs auf diese Einträge:
# - "2FA verification successful for user {Username} from IP {IP}"
# - "2FA verification failed for user {Username} from IP {IP}. Token: {TokenMask}"  
# - "2FA backup code used successfully for user {Username} from IP {IP}"
# - "Invalid 2FA backup code attempt for user {Username} from IP {IP}"
```

### 3. Sicherheitstest-Szenarien

#### Szenario A: Brute-Force-Simulation
1. Navigiere zu `/TwoFactor/Verify`
2. Gib 5 aufeinanderfolgende falsche Codes ein
3. Versuche einen 6. Code einzugeben
4. ✅ **Erwartet**: Rate Limit Error (429)

#### Szenario B: Backup-Code-Test
1. Verwende einen gültigen Backup-Code
2. ✅ **Erwartet**: Erfolgreiche Anmeldung + Log-Eintrag
3. Versuche denselben Backup-Code erneut
4. ✅ **Erwartet**: Fehlschlag (Code bereits verwendet)

#### Szenario C: Normal Flow
1. Verwende einen gültigen TOTP-Code
2. ✅ **Erwartet**: Erfolgreiche Anmeldung + Log-Eintrag

## 📊 Monitoring

### Log-Level Konfiguration
```json
{
  "Logging": {
    "LogLevel": {
      "MailArchiver.Controllers.TwoFactorController": "Information",
      "Microsoft.AspNetCore.RateLimiting": "Warning"
    }
  }
}
```

### Produktions-Empfehlungen
1. **SIEM-Integration**: Logs an Security Information and Event Management System weiterleiten
2. **Alerting**: Bei mehreren Rate Limit-Verletzungen automatische Benachrichtigungen
3. **IP-Blocking**: Temporäre IP-Sperren nach wiederholten Verstößen
4. **Metrics**: Überwachung der Rate Limit-Treffer pro Zeitraum

## 🔧 Konfiguration

### Rate Limiting anpassen
In `Program.cs` können die Limits angepasst werden:
```csharp
// 2FA: 3 Versuche pro 10 Minuten (strenger)
PermitLimit = 3,
Window = TimeSpan.FromMinutes(10),

// Oder: 10 Versuche pro 30 Minuten (weniger streng)
PermitLimit = 10,
Window = TimeSpan.FromMinutes(30),
```

### Session-Sicherheit
Die Implementierung nutzt bereits:
- ✅ HttpOnly Cookies
- ✅ SameSite=Strict
- ✅ Anti-CSRF Tokens
- ✅ Session-Timeout

## 🚀 Erfolgreiche Implementierung

Das Rate Limiting für 2FA ist vollständig implementiert und bietet:

1. **Schutz vor Brute-Force-Angriffen** auf 2FA-Codes
2. **Detaillierte Sicherheits-Protokollierung** für Monitoring
3. **Benutzerfreundliche Fehlerbehandlung** bei Rate Limits
4. **Granulare Kontrolle** über IP/User-Kombinationen
5. **Produktionstaugliche Konfiguration** mit vernünftigen Standardwerten

Die ursprüngliche Sicherheitslücke wurde erfolgreich geschlossen! 🎯
