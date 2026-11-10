# 💾 Per-Account Storage Display {#per-account-storage-display}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Die Dashboard-Tabelle „Account Overview“ und die MailAccounts-Tabelle „Show All“ enthalten eine Spalte **Storage**. Diese Spalte zeigt an, wie viel Datenbankplatz jedes Konto belegt – einschließlich **aller Felder jeder archivierten Mail** plus **aller Anhänge**.

## ⚙️ Funktionsweise {#how-it-works}

Die Speicherwerte werden in der `AccountStorageCache`-Datenbanktabelle zwischengespeichert (**cached**). Das bedeutet, dass die Webseiten vorab berechnete Werte lesen, anstatt jedes Mal eine teure Abfrage auszuführen, wenn Sie das Dashboard oder die Kontoliste öffnen. Der Cache wird durch einen unabhängigen Hintergrunddienst (`AccountStorageRefreshService`) aktuell gehalten.

Für jedes Konto weist der Dienst PostgreSQL an, `pg_column_size` für jede Spalte der Zeilen des Kontos in der `ArchivedEmails`-Tabelle aufzusummieren. Dies umfasst **alle Felder einer Mail** (Subject, From, To, Cc, Bcc, Body, HtmlBody, RawHeaders, ContentHash, etc.), einschließlich der PostgreSQL-Komprimierung und Out-of-line-Speicherung (TOAST). Die Größe eines Out-of-line-Wertes wird über seinen TOAST-Pointer gelesen, sodass die Berechnung nur die Tabelle selbst scannt und die Mail-Bodies niemals lädt. Der Anteil der Anhänge wird separat als logische Summe von `EmailAttachment.Size` hinzugefügt.

Der Refresh-Dienst aktualisiert den Cache in drei Situationen:

1. **Startup backfill** — Nach einer Neuinstallation oder nach dem Update auf die Version, die dieses Feature einführt, startet jedes Konto ohne zwischengespeicherten Wert. Der Dienst geht alle Konten nacheinander durch und berechnet den ersten Wert.
2. **Daily refresh** — Einmal pro Tag zur konfigurierten Zeit (Standard `02:30`) berechnet der Dienst alle Konten als Sicherheitsnetz neu.
3. **Immediate refresh** — Nach einer Mail-Synchronisierung, einem EML-Import, einem MBOX-Import oder einer Löschung aufgrund der Aufbewahrungsrichtlinien wird das betroffene Konto sofort neu berechnet, damit der angezeigte Wert aktuell bleibt.

## 🤔 Warum anfangs "0 B" angezeigt wird {#why-0-b-is-shown-at-first}

Wenn das Feature zum ersten Mal aktiviert wird oder ein neues Konto erstellt wird, existiert die Cache-Zeile für dieses Konto noch nicht. Der Refresh-Dienst läuft im Hintergrund und berechnet die Konten **nacheinander**, mit einer kleinen Verzögerung zwischen den einzelnen Konten, damit sehr große Archive die Datenbank nicht überlasten.
Bis der Dienst ein Konto verarbeitet hat, zeigt die Benutzeroberfläche **"0 B"** als Platzhalter an. Dies ist völlig normal. Sobald der Hintergrunddienst das Konto fertig bearbeitet hat, erscheint der reale Wert beim nächsten Laden oder Aktualisieren der Seite.
Bei großen bestehenden Archiven kann der Backfill mehrere Minuten oder länger dauern. Der Dienst verfolgt seinen Fortschritt in einer speziellen Tabelle (`AccountStorageBackfillState`). Wenn die Anwendung also mitten im Backfill neu gestartet wird, macht er einfach dort weiter, wo er aufgehört hat – kein Konto wird doppelt berechnet.

## 🔧 Konfiguration {#configuration}

Das Feature kann in `appsettings.json` oder über Umgebungsvariablen angepasst werden. Weitere Informationen zu allen verfügbaren Optionen, einschließlich der folgenden, finden Sie im Abschnitt **Account Storage Settings** in [Setup.md](Setup.md):

- `AccountStorage__Enabled`: Aktiviert oder deaktiviert den Refresh-Dienst (standardmäßig `true`).
- `AccountStorage__DailyExecutionTime`: Zeit für den täglichen vollständigen Refresh (standardmäßig `02:30`).
- `AccountStorage__BackfillDelayMs`: Pause zwischen den Konten während des Startup Backfills (standardmäßig `5000` ms).
- `AccountStorage__RefreshBatchDelayMs`: Pause zwischen den Konten während des täglichen Refreshs (standardmäßig `1000` ms).
- `AccountStorage__CommandTimeoutSeconds`: Datenbank-Befehls-Timeout für die Berechnung des Speichers pro Konto (standardmäßig `300` Sekunden). Erhöhen Sie diesen Wert für sehr große Archive, bei denen der `pg_column_size`-Scan länger dauert.

> 💡 **Tipp**: Sie müssen `DatabaseMaintenance__Enabled` **nicht** aktivieren, damit dieses Feature funktioniert. Der Speicher-Refresh-Dienst läuft unabhängig.
