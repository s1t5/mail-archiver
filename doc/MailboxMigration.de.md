# 📥 Mailbox Migration Guide {#mailbox-migration-guide}

[← Back to Documentation Index](index.md)

Dieser Leitfaden erklärt, wie Sie ein Postfach mit der Mail Archiver Anwendung von einem E-Mail-Anbieter zu einem anderen migrieren.

## 📋 Übersicht {#overview}

Die Mail Archiver Anwendung bietet Funktionalität zum Migrieren von E-Mails von einem Quell-Postfach zu einem Ziel-Postfach. Dies ist besonders nützlich beim Wechsel des E-Mail-Anbieters, während der E-Mail-Verlauf erhalten bleibt. Die Ordnerstruktur des Quell-Postfachs wird während der Migration ebenfalls beibehalten.

> **Nur aktuelle E-Mails migrieren?** Der unten beschriebene Ablauf kopiert das *gesamte* archivierte Postfach, und ein zweimaliges Ausführen fügt alles ein zweites Mal an. Wenn das neue Postfach nur E-Mails enthalten soll, die neuer als ein bestimmter Zeitpunkt sind, während der vollständige Verlauf im Archiv bleibt, verwenden Sie stattdessen den [date-windowed offload](Offload.md). Er akzeptiert ein Datumsfenster, kann Ordner ausschließen und umbenennen, bietet einen Testlauf (dry run) und ist sicher wiederholbar, da er alles überspringt, was das Ziel bereits enthält. Er ist zudem über die Befehlszeile steuerbar, was die Skriptierung einer Flotte von Postfächern ermöglicht.

## 🚀 Migrationsprozess {#migration-process}

Befolgen Sie diese Schritte, um Ihr Postfach zu migrieren:

1. **Das Quellkonto hinzufügen**
   - Navigieren Sie zum Abschnitt „Email Accounts“
   - Klicken Sie auf „Add Account“
   - Geben Sie Ihre Details und Anmeldedaten für das Quellkonto ein
   - Speichern Sie das Konto

2. **Das Quellkonto synchronisieren**
   - Erlauben Sie der Anwendung, alle E-Mails vom Quellkonto zu synchronisieren
   - Warten Sie, bis der Synchronisierungsprozess abgeschlossen ist

3. **Das Zielkonto hinzufügen**
   - Klicken Sie im Abschnitt „Email Accounts“ auf „Add Account“
   - Geben Sie Ihre Details und Anmeldedaten für das Zielkonto ein
   - Speichern Sie das Konto

4. **Das Zielkonto synchronisieren**
   - Wenn das Zielkonto neu ist, synchronisieren Sie es, um sicherzustellen, dass es ordnungsgemäß eingerichtet ist
   - Warten Sie, bis der Synchronisierungsprozess abgeschlossen ist

5. **Die Migration starten**
   - Identifizieren Sie im Abschnitt „Email Accounts“ das Quellkonto
   - Gehen Sie zu den Kontodetails
   - Wählen Sie die Option „Copy All Emails to Another Mailbox“
   - Wählen Sie das Zielkonto und einen Zielordner aus
   - Aktivieren Sie die Option **„Preserve Folder Structure“** – der ausgewählte Zielordner dient dann als Stammverzeichnis, unter dem die gesamte Quellordnerstruktur rekursiv neu erstellt wird
   - Wenn diese Option nicht aktiviert ist, werden alle E-Mails direkt in den ausgewählten Zielordner verschoben (flach, ohne Unterordner)
   - Starten Sie den Migrationsprozess

6. **Die Migration überwachen**
   - Wenn eine große Anzahl von E-Mails vorhanden ist, wird die Migration als Hintergrundaufgabe ausgeführt
   - Der Status und der Fortschritt der Migration können im Tab „Jobs“ eingesehen werden
   - Warten Sie, bis die Migration erfolgreich abgeschlossen ist

7. **Das Quellkonto deaktivieren**
   - Gehen Sie nach der erfolgreichen Übertragung zurück zum Quellkonto
   - Setzen Sie das Quellkonto in den Kontoeinstellungen auf „Disabled“
   - Dies stellt sicher, dass es in Zukunft nicht mehr im Archiv gespeichert wird

## ⚠️ Wichtige Hinweise {#important-notes}

- Dieser Ablauf ist nicht wiederholbar: Ein erneutes Ausführen fügt jede Nachricht ein zweites Mal hinzu. Der [date-windowed offload](Offload.md) ist die wiederholbare Alternative.
- Stellen Sie sicher, dass das Quellkonto über ausreichende Berechtigungen zum Lesen aller E-Mails verfügt
- Stellen Sie sicher, dass das Zielkonto über ausreichend Speicherplatz für die migrierten E-Mails verfügt
- Der Migrationsprozess kann je nach Anzahl der E-Mails und der Verbindungsgeschwindigkeit einige Zeit in Anspruch nehmen
- Es wird empfohlen, die Migration während der Nebenzeiten durchzuführen, um die Auswirkungen auf den E-Mail-Dienst zu minimieren
- Überprüfen Sie regelmäßig den Migrationsstatus im Tab "Jobs", um sicherzustellen, dass der Prozess fortschreitet
- Überprüfen Sie nach der Migration, ob alle E-Mails erfolgreich an das Zielkonto übertragen wurden

## ✅ Best Practices {#best-practices}

- Testen Sie den Migrationsprozess immer mit einer kleinen Teilmenge von E-Mails, bevor Sie das gesamte Postfach migrieren
- Erstellen Sie eine Sicherung wichtiger E-Mails, bevor Sie mit der Migration beginnen
- Planen Sie die Migration für einen Zeitraum, in dem die E-Mail-Nutzung minimal ist
- Überwachen Sie den Migrationsfortschritt genau und seien Sie darauf vorbereitet, etwaige Probleme zu beheben, die auftreten können
- Überprüfen Sie nach Abschluss der Migration, ob alle E-Mails korrekt übertragen und zugänglich sind
