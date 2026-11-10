# 🌍 Sprache und Lokalisierung {#language-and-localization}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Die Mail Archiver Benutzeroberfläche ist in **14 Sprachen** verfügbar. Die Sprache ist eine
**browserbezogene Einstellung**, die in einem Cookie gespeichert wird — sie ist nicht an ein Benutzerkonto gebunden
und hat keinen Einfluss auf die archivierten E-Mails selbst: Nachrichten werden immer genau so gespeichert,
wie sie empfangen wurden.

## 🌐 Unterstützte Sprachen {#supported-languages}

| Sprache | Code |
|----------|------|
| Englisch (Standard) | `en` |
| Englisch (GB) | `en-GB` |
| Slovenščina | `sl` |
| Deutsch | `de` |
| Italiano | `it` |
| Français | `fr` |
| Español | `es` |
| Nederlands | `nl` |
| Русский | `ru` |
| Magyar | `hu` |
| Polski | `pl` |
| Português (Brasil) | `pt-BR` |
| 日本語 | `ja` |
| 简体中文 | `zh` |

**Englisch ist die Standardsprache und der Fallback.** Alles, was eine Übersetzung nicht abdeckt,
wird auf Englisch angezeigt, sodass eine teilweise übersetzte Sprache Sie nie mit einem
leeren Label zurücklässt.

## 🔄 Wechseln der Sprache {#switching-the-language}

1. Klicken Sie oben rechts auf Ihren **Benutzernamen**, um das Benutzermenü zu öffnen.
2. Wählen Sie unter **Language** eine Sprache aus der Liste — die Seite wird
   sofort in dieser Sprache neu geladen.

Die Auswahl wird in einem Cookie gespeichert, das für **ein Jahr** gültig ist und nur für
diesen Browser gilt. Andere Benutzer und derselbe Benutzer in einem anderen Browser oder auf
einem anderen Gerät behalten ihre eigenen Einstellungen. Da das Cookie die Sitzung überdauert,
ist die Sprache auch nach dem Abmelden und erneuten Anmelden noch ausgewählt.

## 🧩 Was die Spracheinstellung beeinflusst — und was nicht {#what-the-language-setting-does-and-does-not-affect}

- **Übersetzt:** der gesamte Text der Benutzeroberfläche — Menüs, Schaltflächen, Formularbeschriftungen, Validierungs- und
  Fehlermeldungen, Job-Statusseiten und die Aktionsbeschreibungen im Audit-Protokoll.
- **Nicht übersetzt:** archivierte E-Mails. Inhalte, Betreffzeilen und Anhänge werden
  Byte für Byte so gespeichert und angezeigt, wie sie empfangen wurden.
- **Suche:** Die Abfragesyntax ist in jeder Sprache gleich; siehe
  [Mail search](Search.md). Das Ändern der Sprache ändert nicht, wie E-Mails
  gespeichert oder indiziert werden.
- **Zeitstempel:** Daten werden in der lokalen Zeitzone des Browsers angezeigt und
  für die ausgewählte Kultur formatiert. Um die Anzeige der Zeitzone instanzweit festzulegen,
  setzen Sie `TimeZone__DisplayTimeZoneId` (IANA-Bezeichner, z. B.
  `Europe/Berlin`) — siehe [Setup and Parameters](Setup.md#-timezone-settings).

## 🛠️ Für Mitwirkende: Übersetzungen hinzufügen oder ändern {#for-contributors-adding-or-changing-translations}

Die übersetzten Zeichenfolgen befinden sich in `Resources/`:

| Datei | Zweck |
|------|---------|
| `SharedResource.resx` | Neutrale Ressourcendatei — **Englisch**, 1148 Schlüssel |
| `SharedResource.<culture>.resx` | Eine Datei pro Sprache, z. B. `SharedResource.de.resx` |

Regeln, die sich aus der Art und Weise ergeben, wie .NET Ressourcen hier auflöst:

1. **Fügen Sie zuerst neue Schlüssel zur neutralen Datei hinzu**, dann zu den Sprachdateien. Ein Schlüssel,
   der in einer Übersetzung fehlt, fällt auf den englischen Text zurück — es geht nichts
   kaputt, die Seite bleibt lediglich teilweise auf Englisch.
2. **Ein Schlüssel, der nur in einer Übersetzungsdatei existiert, ist überflüssig.** Er wird nie
   aufgesucht, da die Suche in der neutralen Datei beginnt. (Zum Zeitpunkt der Erstellung
   existieren `DeleteAccountAllEmailsButton` und `Sign Out` nur in einigen Übersetzungen,
   und die polnische Datei enthält zudem veraltete Designer-Schlüssel: `Bitmap1`, `Icon1`,
   `Name1`.)
3. **`en-GB` ist konstruktionsbedingt eine Teilübersetzung.** Sie definiert nur 245 der 1148
   Schlüssel und erbt den Rest aus der neutralen englischen Datei — weshalb sie
   wie Englisch gelesen werden kann, außer dort, wo sich britische Formulierungen oder Datumsformate unterscheiden.
   Chinesisch (Vereinfacht), Deutsch, Spanisch, Französisch, Ungarisch, Italienisch,
   Japanisch, Niederländisch, Polnisch, Portugiesisch (Brasilien), Russisch und Slowenisch sind
   derzeit vollständig.
4. **Das Hinzufügen einer kompletten Sprache** erfordert drei Dinge: eine neue
   `SharedResource.<culture>.resx`, einen Eintrag in der Kulturliste in `Program.cs`
   (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`),
   und ein `<option>` im Sprach-Dropdown in
   `Views/Shared/_UserInfo.cshtml`. Ohne die letzten beiden wird die Datei nie verwendet.

Siehe [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md)
für den Mitwirkungsprozess, Codierungsstandards und wie Sie einen Pull Request einreichen.

## 🔗 Verwandte Dokumentation {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — Abfragesyntax
- [User Management](UserManagement.md) — Rollen und Berechtigungen
