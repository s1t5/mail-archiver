# 🌍 Sprache und Lokalisierung {#language-and-localization}

[← Zurück zum Dokumentationsindex](index.md)

## 📋 Übersicht {#overview}

Die Mail Archiver-Benutzeroberfläche ist in **14 Sprachen** verfügbar. Die Sprache ist eine
**browserbezogene Einstellung**, die in einem Cookie gespeichert wird – sie ist nicht an ein Benutzerkonto gebunden
und hat niemals Auswirkungen auf die archivierten E-Mails selbst: Nachrichten werden immer genau so gespeichert,
wie sie empfangen wurden.

## 🌐 Unterstützte Sprachen {#supported-languages}

| Sprache | Code |
|----------|------|
| English (default) | `en` |
| English (GB) | `en-GB` |
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

**English ist die Standard- und Fallback-Sprache.** Alles, was eine Übersetzung nicht abdeckt, wird auf Englisch angezeigt, sodass eine teilweise übersetzte Sprache Sie nie mit einem leeren Label zurücklässt.

## 🔄 Sprache wechseln {#switching-the-language}

1. Klicken Sie oben rechts auf Ihren **Benutzernamen**, um das Benutzermenü zu öffnen.
2. Wählen Sie unter **Language** eine Sprache aus der Liste aus — die Seite wird sofort in dieser Sprache neu geladen.

Die Auswahl wird in einem Cookie gespeichert, das **ein Jahr** lang gültig ist und nur für diesen Browser gilt. Andere Benutzer sowie derselbe Benutzer in einem anderen Browser oder auf einem anderen Gerät behalten ihre eigenen Einstellungen bei. Da das Cookie länger als die Sitzung gültig ist, bleibt die Sprache auch nach der Abmeldung und erneuten Anmeldung ausgewählt.

## 🧩 Was die Spracheinstellung beeinflusst — und was nicht {#what-the-language-setting-does-and-does-not-affect}

- **Übersetzt:** sämtlicher Interface-Text — Menüs, Schaltflächen, Formularbeschriftungen, Validierungs- und Fehlermeldungen, Job-Statusseiten und die Aktionsbeschreibungen im Zugriffsprotokoll.
- **Nicht übersetzt:** archivierte E-Mails. Inhalte, Betreffzeilen und Anhänge werden Byte für Byte so gespeichert und angezeigt, wie sie empfangen wurden.
- **Suche:** Die Abfragesyntax ist in jeder Sprache identisch; siehe [Mail search](Search.md). Das Ändern der Sprache ändert nichts daran, wie E-Mails gespeichert oder indiziert werden.
- **Zeitstempel:** Daten werden in der lokalen Zeitzone des Browsers angezeigt und sind für die ausgewählte Kultur formatiert. Um die Zeitzone für die gesamte Instanz festzulegen, setzen Sie `TimeZone__DisplayTimeZoneId` (IANA-Bezeichner, z. B. `Europe/Berlin`) — siehe [Setup and Parameters](Setup.md#-timezone-settings).

## 🛠️ Für Mitwirkende: Übersetzungen hinzufügen oder ändern {#for-contributors-adding-or-changing-translations}

Die übersetzten Strings befinden sich in `Resources/`:

| Datei | Zweck |
|------|---------|
| `SharedResource.resx` | Neutrale Ressourcendatei — **Englisch**, 1148 Keys |
| `SharedResource.<culture>.resx` | Eine Datei pro Sprache, z. B. `SharedResource.de.resx` |

Regeln, die sich aus der Art und Weise ergeben, wie .NET Ressourcen hier auflöst:

1. **Fügen Sie zuerst neue Keys der neutralen Datei hinzu**, dann den Sprachdateien. Ein Key, der in einer Übersetzung fehlt, fällt auf den englischen Text zurück — es geht nichts kaputt, die Seite bleibt lediglich teilweise auf Englisch.
2. **Ein Key, der nur in einer Übersetzungsdatei existiert, ist überflüssiger Ballast.** Er wird niemals nachgeschlagen, da die Suche in der neutralen Datei beginnt. (Zum Zeitpunkt der Erstellung existieren `DeleteAccountAllEmailsButton` und `Sign Out` nur in einigen Übersetzungen, und die polnische Datei enthält zudem überbleibende Designer-Keys: `Bitmap1`, `Icon1`, `Name1`.)
3. **`en-GB` ist designbedingt eine partielle Überschreibung.** Sie definiert nur 245 der 1148 Keys und erbt den Rest aus der neutralen englischen Datei — weshalb sie sich wie Englisch liest, außer dort, wo sich britische Formulierungen oder Datumsformate unterscheiden.
   Chinesisch (Vereinfacht), Deutsch, Spanisch, Französisch, Ungarisch, Italienisch, Japanisch, Niederländisch, Polnisch, Portugiesisch (Brasilien), Russisch und Slowenisch sind derzeit vollständig.
4. **Das Hinzufügen einer kompletten Sprache** erfordert drei Dinge: eine neue `SharedResource.<culture>.resx`, einen Eintrag in der Kulturliste in `Program.cs` (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`) und eine `<option>` im Sprach-Dropdown in `Views/Shared/_UserInfo.cshtml`. Ohne die letzten beiden wird die Datei niemals verwendet.

Siehe [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md)
für den Mitwirkungsprozess, Codierungsstandards und wie man einen Pull Request einreicht.

## 🔗 Verwandte Dokumentation {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — query syntax
- [User Management](UserManagement.md) — roles and permissions
