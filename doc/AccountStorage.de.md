# 💾 Anzeige des Speichers pro Konto {#per-account-storage-display}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Die Dashboard-Tabelle „Account Overview“ und die MailAccounts-Tabelle „Show All“ enthalten eine Spalte **Storage**. Diese Spalte zeigt an, wie viel Datenbankplatz jedes Konto belegt – einschließlich **aller Felder jeder archivierten Mail** plus **aller Anhänge**.

## ⚙️ Funktionsweise {#how-it-works}

Speicherwerte werden in der `AccountStorageCache`-Datenbanktabelle **zwischengespeichert**. Das bedeutet, dass die Webseiten vorab berechnete Werte lesen, anstatt jedes Mal eine teure Abfrage auszuführen, wenn Sie das Dashboard oder die Kontoliste öffnen. Der Cache wird durch einen unabhängigen Hintergrunddienst (`AccountStorageRefreshService`) aktuell gehalten.

Für jedes Konto weist der Dienst PostgreSQL an, `pg_column_size` für jede Spalte der Zeilen des Kontos in der `ArchivedEmails`-Tabelle aufzusummieren. Dies deckt **alle Felder einer E-Mail** ab (Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash, etc.), einschließlich der PostgreSQL-Komprimierung und Out-of-line-Speicherung (TOAST). Die Größe eines Out-of-line-Wertes wird aus seinem TOAST-Pointer gelesen, sodass die Berechnung nur die Tabelle selbst scannt und die E-Mail-Bodies niemals lädt. Der Anteil der Anhänge wird separat als logische Summe von `EmailAttachment.Size` hinzugefügt.

Der Refresh-Dienst aktualisiert den Cache in drei Situationen:

1. **Startup backfill** — Nach einer Neuinstallation oder nach dem Update auf die Version, die dieses Feature einführt, startet jedes Konto ohne zwischengespeicherten Wert. Der Dienst geht alle Konten nacheinander durch und berechnet den ersten Wert.
2. **Daily refresh** — Einmal pro Tag zur konfigurierten Zeit (Standard `02:30`) berechnet der Dienst alle Konten als Sicherheitsnetz neu.
3. **Immediate refresh** — Nach einer E-Mail-Synchronisierung, einem EML-Import, einem MBOX-Import oder einer Löschung aufgrund der Aufbewahrung wird das betroffene Konto sofort neu berechnet, damit der angezeigte Wert aktuell bleibt.

## 🤔 Warum anfangs „0 B“ angezeigt wird {#why-0-b-is-shown-at-first}

Wenn die Funktion zum ersten Mal aktiviert wird oder ein neues Konto erstellt wird, existiert die Cache-Zeile für dieses Konto noch nicht. Der Refresh-Service läuft im Hintergrund und berechnet die Konten **nacheinander**, mit einer kleinen Verzögerung zwischen jedem Konto, damit sehr große Archive die Datenbank nicht überlasten.
Bis der Service ein Konto verarbeitet hat, zeigt die Benutzeroberfläche **„0 B“** als Platzhalter an. Dies ist völlig normal. Sobald der Hintergrunddienst dieses Konto abgeschlossen hat, erscheint der tatsächliche Wert beim nächsten Laden oder Aktualisieren der Seite.
Bei großen bestehenden Archiven kann das Backfill mehrere Minuten oder länger dauern. Der Service verfolgt seinen Fortschritt in einer speziellen Tabelle (`AccountStorageBackfillState`). Wenn die Anwendung also mitten im Backfill neu gestartet wird, macht sie einfach dort weiter, wo sie aufgehört hat – kein Konto wird doppelt berechnet.

## 🔧 Konfiguration {#configuration}

Die Funktion kann in `appsettings.json` oder über Umgebungsvariablen angepasst werden. Weitere Informationen zu allen verfügbaren Optionen, einschließlich der folgenden, finden Sie im Abschnitt **Account Storage Settings** in [Setup.md](Setup.md):

- `AccountStorage__Enabled`: Den Refresh-Service ein- oder ausschalten (standardmäßig `true`).
- `AccountStorage__DailyExecutionTime`: Zeitpunkt für den täglichen vollständigen Refresh (standardmäßig `02:30`).
- `AccountStorage__BackfillDelayMs`: Pause zwischen Konten während des Backfills beim Start (standardmäßig `5000` ms).
- `AccountStorage__RefreshBatchDelayMs`: Pause zwischen Konten während des täglichen Refreshs (standardmäßig `1000` ms).
- `AccountStorage__CommandTimeoutSeconds`: Datenbank-Befehls-Timeout für die Berechnung des Speichers pro Konto (standardmäßig `300` Sekunden). Erhöhen Sie diesen Wert für sehr große Archive, bei denen der `pg_column_size`-Scan länger dauert.

> 💡 **Tipp**: Sie müssen `DatabaseMaintenance__Enabled` **nicht** aktivieren, damit diese Funktion funktioniert. Der Speicher-Refresh-Service läuft unabhängig.
