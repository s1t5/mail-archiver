# 📥 Mailbox Migration Guide {#mailbox-migration-guide}

[← Zurück zum Dokumentationsindex](index.md)

Dieser Leitfaden erklärt, wie Sie ein Postfach mit der Mail Archiver Anwendung von einem E-Mail-Anbieter zu einem anderen migrieren.

## 📋 Übersicht {#overview}

Die Mail Archiver Anwendung bietet die Funktionalität, E-Mails von einem Quell-Postfach zu einem Ziel-Postfach zu migrieren. Dies ist besonders nützlich, wenn Sie den E-Mail-Anbieter wechseln und dabei Ihre E-Mail-Historie bewahren möchten. Die Ordnerstruktur des Quell-Postfachs wird während der Migration ebenfalls beibehalten.

> **Nur aktuelle E-Mails migrieren?** Der unten beschriebene Ablauf kopiert das *gesamte* archivierte Postfach, und ein zweites Ausführen hängt alles ein zweites Mal an. Wenn das neue Postfach nur E-Mails enthalten soll, die neuer als ein bestimmter Zeitpunkt sind, während die vollständige Historie im Archiv bleibt, verwenden Sie stattdessen den [date-windowed offload](Offload.md). Dieser nimmt ein Zeitfenster entgegen, kann Ordner ausschließen und umbenennen, bietet einen Testlauf (Dry Run) und ist sicher wiederholbar, da er alles überspringt, was das Ziel bereits enthält. Er ist zudem über die Befehlszeile steuerbar, was die Automatisierung einer Flotte von Postfächern ermöglicht.

## 🚀 Migrationsprozess {#migration-process}

Folgen Sie diesen Schritten, um Ihr Postfach zu migrieren:

1. **Das Quell-Konto hinzufügen**
   - Navigieren Sie zum Abschnitt "Email Accounts"
   - Klicken Sie auf "Add Account"
   - Geben Sie die Details und Anmeldedaten Ihres Quell-Kontos ein
   - Speichern Sie das Konto

2. **Das Quell-Konto synchronisieren**
   - Erlauben Sie der Anwendung, alle E-Mails aus dem Quell-Konto zu synchronisieren
   - Warten Sie, bis der Synchronisierungsprozess abgeschlossen ist

3. **Das Ziel-Konto hinzufügen**
   - Klicken Sie im Abschnitt "Email Accounts" auf "Add Account"
   - Geben Sie die Details und Anmeldedaten Ihres Ziel-Kontos ein
   - Speichern Sie das Konto

4. **Das Ziel-Konto synchronisieren**
   - Wenn das Ziel-Konto neu ist, synchronisieren Sie es, um sicherzustellen, dass es korrekt eingerichtet ist
   - Warten Sie, bis der Synchronisierungsprozess abgeschlossen ist

5. **Die Migration einleiten**
   - Identifizieren Sie im Abschnitt "Email Accounts" das Quell-Konto
   - Gehen Sie zu den Kontodetails
   - Wählen Sie die Option "Copy All Emails to Another Mailbox"
   - Wählen Sie das Ziel-Konto und einen Ziel-Ordner aus
   - Aktivieren Sie die Option **"Preserve Folder Structure"** – der ausgewählte Ziel-Ordner dient dann als Stammverzeichnis, unter dem die gesamte Quell-Ordnerstruktur rekursiv neu erstellt wird
   - Wenn diese Option nicht aktiviert ist, werden alle E-Mails direkt in den ausgewählten Ziel-Ordner abgelegt (flach, ohne Unterordner)
   - Starten Sie den Migrationsprozess

6. **Die Migration überwachen**
   - Wenn eine große Anzahl von E-Mails vorhanden ist, wird die Migration als Hintergrundaufgabe ausgeführt
   - Status und Fortschritt der Migration können im Tab "Jobs" eingesehen werden
   - Warten Sie, bis die Migration erfolgreich abgeschlossen ist

7. **Das Quell-Konto deaktivieren**
   - Gehen Sie nach der erfolgreichen Übertragung zurück zum Quell-Konto
   - Setzen Sie das Quell-Konto in den Kontoeinstellungen auf "Disabled"
   - Dies stellt sicher, dass es in Zukunft nicht mehr archiviert wird

## ⚠️ Wichtige Hinweise {#important-notes}

- Dieser Ablauf ist nicht wiederholbar: Ein erneutes Ausführen hängt jede Nachricht ein zweites Mal an. Der [date-windowed offload](Offload.md) ist die wiederholbare Alternative.
- Stellen Sie sicher, dass das Quell-Konto über ausreichende Berechtigungen verfügt, um alle E-Mails zu lesen
- Vergewissern Sie sich, dass das Ziel-Konto über genügend Speicherplatz für die migrierten E-Mails verfügt
- Der Migrationsprozess kann je nach Anzahl der E-Mails und der Verbindungsgeschwindigkeit einige Zeit in Anspruch nehmen
- Es wird empfohlen, die Migration während der Nebenzeiten durchzuführen, um die Auswirkungen auf den E-Mail-Dienst zu minimieren
- Überprüfen Sie regelmäßig den Migrationsstatus im Tab "Jobs", um sicherzustellen, dass der Prozess voranschreitet
- Verifizieren Sie nach der Migration, ob alle E-Mails erfolgreich an das Ziel-Konto übertragen wurden

## ✅ Best Practices {#best-practices}

- Testen Sie den Migrationsprozess immer mit einer kleinen Teilmenge von E-Mails, bevor Sie das gesamte Postfach migrieren
- Erstellen Sie eine Sicherung wichtiger E-Mails, bevor Sie mit der Migration beginnen
- Planen Sie die Migration zu einer Zeit, zu der die E-Mail-Nutzung minimal ist
- Überwachen Sie den Migrationsfortschritt genau und seien Sie bereit, auf eventuelle Probleme zu reagieren
- Verifizieren Sie nach Abschluss der Migration, dass alle E-Mails korrekt übertragen und zugänglich sind
