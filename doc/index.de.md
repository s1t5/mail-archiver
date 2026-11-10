# 📧 Mail Archiver Dokumentation {#mail-archiver-documentation}

<div class="docs-lead" markdown>
**Selbst gehostetes E-Mail-Archivierung — Archivieren, Suchen und Exportieren von E-Mails von jedem Anbieter.**
Einmal einrichten, jede Nachricht behalten und sie Jahre später wiederfinden.
</div>

<div class="grid cards" markdown>

-   :material-rocket-launch:{ .lg .middle } __Erste Schritte__

    ---

    Installieren Sie Mail Archiver mit Docker Compose und verbinden Sie Ihr erstes Postfach.

    [:octicons-arrow-right-24: Setup und Parameter](Setup.md)

-   :material-magnify:{ .lg .middle } __Alles finden__

    ---

    Volltextsuche über jedes Konto hinweg, mit Filtern, Sortierung und Massenaktionen.

    [:octicons-arrow-right-24: E-Mail-Suche](Search.md)

-   :material-api:{ .lg .middle } __Automatisieren__

    ---

    Lesen Sie Ihr Archiv über Skripte und KI-Agenten aus — schreibgeschützte, eingeschränkte API-Schlüssel.

    [:octicons-arrow-right-24: REST API](API.md) · [:octicons-arrow-right-24: MCP server](MCP.md)

-   :material-shield-lock:{ .lg .middle } __Sichern__

    ---

    Zwei-Faktor-Authentifizierung, SSO, Zugriffsprotokollierung und Aufbewahrungsrichtlinien.

    [:octicons-arrow-right-24: Zwei-Faktor-Auth](TwoFactor.md) · [:octicons-arrow-right-24: OIDC / SSO](OIDC_Implementation.md)

</div>

## 🛠️ Installation & Wartung {#installation-maintenance}

<div class="grid cards" markdown>

-   :material-cog:{ .lg .middle } __Setup und Parameter__

    ---

    Jede Umgebungsvariable, Kestrel HTTPS und Secrets-Management.

    [:octicons-arrow-right-24: Lesen](Setup.md)

-   :material-server-network:{ .lg .middle } __Reverse Proxy__

    ---

    Platzieren Sie Mail Archiver hinter nginx oder Caddy mit TLS-Terminierung.

    [:octicons-arrow-right-24: Lesen](ReverseProxy.md)

-   :material-database-arrow-up:{ .lg .middle } __PostgreSQL Upgrade__

    ---

    Wechseln Sie zu einer neuen PostgreSQL-Hauptversion, ohne Daten zu verlieren.

    [:octicons-arrow-right-24: Lesen](PostgreSQLUpgrade.md)

-   :material-database-cog:{ .lg .middle } __Datenbank-Wartung__

    ---

    VACUUM, ANALYZE und die Aufrechterhaltung der Reaktionsfähigkeit des Archivs über die Zeit.

    [:octicons-arrow-right-24: Lesen](DatabaseMaintenance.md)

-   :material-backup-restore:{ .lg .middle } __Sicherung und Wiederherstellung__

    ---

    Sichern Sie die Datenbank und führen Sie eine Wiederherstellung durch — einschließlich Proxmox-Setups.

    [:octicons-arrow-right-24: Lesen](BackupRestore.md)

-   :material-flask:{ .lg .middle } __Lokale Testumgebung__

    ---

    Führen Sie lokal eine temporäre Instanz aus, bevor Sie die Produktion berühren.

    [:octicons-arrow-right-24: Lesen](LocalTestEnvironment.md)

</div>

## 📥 E-Mails importieren {#getting-mail-in}

<div class="grid cards" markdown>

-   :material-account-plus:{ .lg .middle } __Konto-Import__

    ---

    Fügen Sie viele Konten gleichzeitig aus einer CSV-Datei hinzu.

    [:octicons-arrow-right-24: Lesen](Account-Import.md)

-   :material-email-sync:{ .lg .middle } __Synchronisierung__

    ---

    Schnelle vs. vollständige Synchronisierung, Checkpoints und was passiert, wenn eine Synchronisierung fehlschlägt.

    [:octicons-arrow-right-24: Lesen](Synchronization.md)

-   :material-console:{ .lg .middle } __CLI-Befehle__

    ---

    Importieren Sie bestehende Archive über die Befehlszeile und verschieben Sie gespeicherte Originalnachrichten zwischen der Datenbank und dem Dateisystem.

    [:octicons-arrow-right-24: Lesen](CLI.md)

-   :material-upload:{ .lg .middle } __Web-Import (EML / MBox)__

    ---

    Laden Sie eine MBox-Datei oder ein ZIP mit EML-Dateien über den Browser hoch.

    [:octicons-arrow-right-24: Lesen](Import-EML-MBox.md)

-   :material-timer-sand:{ .lg .middle } __Rate-Limit-Handhabung__

    ---

    Bandbreitenverfolgung und wie pausierte Synchronisierungen von selbst fortgesetzt werden.

    [:octicons-arrow-right-24: Lesen](RateLimitHandling.md)

</div>

## 🔍 Arbeiten mit dem Archiv {#working-with-the-archive}

<div class="grid cards" markdown>

-   :material-magnify:{ .lg .middle } __E-Mail-Suche__

    ---

    Abfragesyntax, Filter, Sortierung, Paging und Massenaktionen.

    [:octicons-arrow-right-24: Lesen](Search.md)

-   :material-progress-clock:{ .lg .middle } __Hintergrund-Jobs__

    ---

    Überwachen Sie Synchronisierungen, Importe, Wiederherstellungen und Exporte — und brechen Sie diese sicher ab.

    [:octicons-arrow-right-24: Lesen](BackgroundJobs.md)

-   :material-swap-horizontal:{ .lg .middle } __Postfach-Migration__

    ---

    Kopieren Sie E-Mails zwischen Postfächern unter Beibehaltung der Ordnerstruktur.

    [:octicons-arrow-right-24: Lesen](MailboxMigration.md)

-   :material-download:{ .lg .middle } __Konto-Export (EML / MBox)__

    ---

    Schreiben Sie ein ganzes Konto als portables EML- oder MBox-Archiv zurück.

    [:octicons-arrow-right-24: Lesen](Export-Account.md)

-   :material-email-lock:{ .lg .middle } __Speicherung der Originalnachricht__

    ---

    Behalten Sie die Nachricht im empfangenen Zustand, damit DKIM weiterhin verifiziert und Exporte bytegenau sind.

    [:octicons-arrow-right-24: Lesen](RawMessages.md)

-   :material-content-duplicate:{ .lg .middle } __Anhang-Deduplizierung__

    ---

    Wie identische Anhänge nur einmal statt mehrfach gespeichert werden.

    [:octicons-arrow-right-24: Lesen](AttachmentDeduplication.md)

-   :material-calendar-arrow-right:{ .lg .middle } __Zeitfenster-Auslagerung__

    ---

    Verschieben Sie ältere E-Mails in ein anderes Konto, Fenster für Fenster.

    [:octicons-arrow-right-24: Lesen](Offload.md)

-   :material-harddisk:{ .lg .middle } __Speicher pro Konto__

    ---

    Sehen Sie, wie viel Platz jedes Postfach einnimmt.

    [:octicons-arrow-right-24: Lesen](AccountStorage.md)

</div>

## 🔐 Administration & Sicherheit {#administration-security}

<div class="grid cards" markdown>

-   :material-account-group:{ .lg .middle } __Benutzerverwaltung__

    ---

    Erstellen Sie Benutzer, vergeben Sie Admin-Rechte, weisen Sie Postfach-Berechtigungen zu und ändern Sie Ihr eigenes Passwort.

    [:octicons-arrow-right-24: Lesen](UserManagement.md)

-   :material-two-factor-authentication:{ .lg .middle } __Zwei-Faktor-Authentifizierung__

    ---

    TOTP-Setup, Backup-Codes und was zu tun ist, wenn ein Gerät verloren geht.

    [:octicons-arrow-right-24: Lesen](TwoFactor.md)

-   :material-key-chain:{ .lg .middle } __OIDC / SSO__

    ---

    Anmelden über Entra ID, Authelia oder einen anderen Identity Provider.

    [:octicons-arrow-right-24: Lesen](OIDC_Implementation.md)

-   :material-text-box-search:{ .lg .middle } __Zugriffsprotokollierung__

    ---

    Wer hat was geöffnet, gesucht, exportiert oder gelöscht.

    [:octicons-arrow-right-24: Lesen](Logs.md)

-   :material-file-export:{ .lg .middle } __Audit-Daten-Export__

    ---

    Exportieren Sie den Audit-Pfad für Compliance-Prüfungen.

    [:octicons-arrow-right-24: Lesen](AuditExport.md)

-   :material-lifebuoy:{ .lg .middle } __Notfall-Kontowiederherstellung__

    ---

    Erlangen Sie wieder Zugriff, wenn ein Konto gesperrt wurde.

    [:octicons-arrow-right-24: Lesen](EmergencyAccountRecovery.md)

-   :material-delete-sweep:{ .lg .middle } __Aufbewahrungsrichtlinien__

    ---

    Automatisches Löschen vom E-Mail-Server oder dem lokalen Archiv.

    [:octicons-arrow-right-24: Lesen](RetentionPolicies.md)

</div>

## ☁️ Anbieter-Leitfäden {#provider-guides}

<div class="grid cards" markdown>

-   :material-microsoft:{ .lg .middle } __Azure App-Registrierung (M365)__

    ---

    Registrieren Sie eine App und konfigurieren Sie die Aufbewahrung für Microsoft 365.

    [:octicons-arrow-right-24: Lesen](AZURE_APP_REGISTRATION_M365.md)

-   :material-domain:{ .lg .middle } __M365 Tenant Postfach-Import__

    ---

    Massimport aller Postfächer eines Tenants über ein einziges Formular.

    [:octicons-arrow-right-24: Lesen](M365TenantImport.md)

-   :material-email-multiple:{ .lg .middle } __Mailcow-Instanzen__

    ---

    Wählen Sie Postfächer zentral aus und stellen Sie dedizierte IMAP-App-Passwörter bereit.

    [:octicons-arrow-right-24: Lesen](Mailcow.md)

-   :material-account-key:{ .lg .middle } __Persönliche Microsoft-Konten__

    ---

    Outlook.com, Hotmail und M365 Family über den Device Code Flow.

    [:octicons-arrow-right-24: Lesen](MSA_Outlook_Setup.md)

-   :material-google:{ .lg .middle } __Gmail Best Practices__

    ---

    App-Passwörter, Labels, Expunge und Gmails Rate-Limits.

    [:octicons-arrow-right-24: Lesen](GmailBestPractices.md)

-   :material-email-off:{ .lg .middle } __Yahoo Mail Einschränkungen__

    ---

    Was Yahoo's IMAP- und Export-Endpunkte nicht leisten können.

    [:octicons-arrow-right-24: Lesen](YahooMailExportLimitations.md)

</div>

## 🔌 Programmatischer Zugriff {#programmatic-access}

<div class="grid cards" markdown>

-   :material-api:{ .lg .middle } __REST API__

    ---

    Schreibgeschützter Zugriff auf das Archiv mit eingeschränkten API-Schlüsseln pro Benutzer.

    [:octicons-arrow-right-24: Lesen](API.md)

-   :material-robot:{ .lg .middle } __MCP server__

    ---

    Stellen Sie das Archiv KI-Agenten als auffindbare Tools zur Verfügung.

    [:octicons-arrow-right-24: Lesen](MCP.md)

</div>

## 🧰 Betrieb & Entwicklung {#operations-development}

<div class="grid cards" markdown>

-   :material-translate:{ .lg .middle } __Sprache und Lokalisierung__

    ---

    Wechseln Sie die Interface-Sprache und erfahren Sie, wie Sie eine Übersetzung hinzufügen.

    [:octicons-arrow-right-24: Lesen](Localization.md)

-   :material-docker:{ .lg .middle } __Docker Compose Logs__

    ---

    Lesen Sie Container-Logs und ändern Sie das Log-Level.

    [:octicons-arrow-right-24: Lesen](DockerComposeLogs.md)

-   :material-tag:{ .lg .middle } __Entwicklungsversionen__

    ---

    Wechseln Sie zu einem Dev-Tag, um kommende Änderungen zu testen.

    [:octicons-arrow-right-24: Lesen](DevTag.md)

</div>

---

<div class="docs-footer-note" markdown>
:material-pencil: **Diese Dokumentation wird kontinuierlich erweitert.**
Wenn Sie einen Fehler finden oder einen Vorschlag haben, bitte
[ein Issue eröffnen](https://github.com/s1t5/mail-archiver/issues) oder einen Pull Request einreichen.
</div>
