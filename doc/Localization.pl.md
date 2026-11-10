# 🌍 Język i lokalizacja {#language-and-localization}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Interfejs Mail Archiver jest dostępny w **14 językach**. Język jest
**preferencją na poziomie przeglądarki** przechowywaną w pliku cookie — nie jest on powiązany z kontem użytkownika
i nigdy nie wpływa na samą zarchiwizowaną pocztę: wiadomości są zawsze przechowywane dokładnie
tak, jak zostały odebrane.

## 🌐 Obsługiwane języki {#supported-languages}

| Język | Kod |
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

**Angielski jest językiem domyślnym i zapasowym.** Wszystko, czego nie obejmuje tłumaczenie,
jest wyświetlane w języku angielskim, dzięki czemu częściowo przetłumaczony język nigdy nie pozostawia użytkownika z pustą etykietą.

## 🔄 Zmiana języka {#switching-the-language}

1. Kliknij swoją **nazwę użytkownika** w prawym górnym rogu, aby otworzyć menu użytkownika.
2. W sekcji **Language** wybierz język z listy — strona przeładuje się
   natychmiast w wybranym języku.

Wybór jest zapisywany w pliku cookie, który jest ważny przez **jeden rok** i dotyczy
tylko tej przeglądarki. Inni użytkownicy, a także ten sam użytkownik w innej przeglądarce lub na
innym urządzeniu, zachowują własne ustawienia. Ponieważ plik cookie przetrwa sesję,
język pozostanie wybrany po wylogowaniu i ponownym zalogowaniu.

## 🧩 Na co wpływa — a na co nie wpływa — ustawienie języka {#what-the-language-setting-does-and-does-not-affect}

- **Tłumaczone:** cały tekst interfejsu — menu, przyciski, etykiety formularzy, komunikaty walidacji i
  błędów, strony statusu zadań oraz opisy działań w dzienniku audytu.
- **Nietłumaczone:** zarchiwizowana poczta. Treści, tematy i załączniki są przechowywane
  i wyświetlane bajt po bajcie w takiej formie, w jakiej zostały odebrane.
- **Wyszukiwanie:** składnia zapytań jest taka sama w każdym języku; zobacz
  [Mail search](Search.md). Zmiana języka nie zmienia sposobu przechowywania ani indeksowania poczty.
- **Znaczniki czasu:** daty są wyświetlane w lokalnej strefie czasowej przeglądarki i
  formatowane zgodnie z wybraną kulturą. Aby przypisać strefę czasową dla całej instancji, ustaw `TimeZone__DisplayTimeZoneId` (identyfikator IANA, np.
  `Europe/Berlin`) — zobacz [Setup and Parameters](Setup.md#-timezone-settings).

## 🛠️ Dla współtwórców: dodawanie lub zmiana tłumaczeń {#for-contributors-adding-or-changing-translations}

Przetłumaczone ciągi znaków znajdują się w `Resources/`:

| Plik | Cel |
|------|---------|
| `SharedResource.resx` | Neutralny plik zasobów — **English**, 1148 kluczy |
| `SharedResource.<culture>.resx` | Jeden plik na język, np. `SharedResource.de.resx` |

Zasady wynikające ze sposobu, w jaki .NET rozwiązuje zasoby w tym miejscu:

1. **Najpierw dodaj nowe klucze do pliku neutralnego**, a następnie do plików językowych. Klucz,
   którego brakuje w tłumaczeniu, spowoduje użycie tekstu angielskiego — nic się nie
   psuje, strona po prostu pozostaje częściowo w języku angielskim.
2. **Klucz, który istnieje tylko w pliku tłumaczenia, jest zbędnym obciążeniem.** Nigdy nie
   zostanie odnaleziony, ponieważ wyszukiwanie zaczyna się od pliku neutralnego. (W momencie pisania,
   `DeleteAccountAllEmailsButton` i `Sign Out` istnieją tylko w niektórych tłumaczeniach,
   a polski plik zawiera również pozostałe klucze projektowe: `Bitmap1`, `Icon1`,
   `Name1`.)
3. **`en-GB` jest z założenia częściowym nadpisaniem.** Definiuje on tylko 245 z 1148
   kluczy i dziedziczy resztę z neutralnego angielskiego pliku — dlatego
   wygląda jak angielski, z wyjątkiem miejsc, gdzie różnią się brytyjskie sformułowania lub formaty dat.
   Chiński (uproszczony), niemiecki, hiszpański, francuski, węgierski, włoski,
   japoński, niderlandzki, polski, portugalski (Brazylia), rosyjski i słoweński są
   obecnie kompletne.
4. **Dodanie całego języka** wymaga trzech rzeczy: nowego
   `SharedResource.<culture>.resx`, wpisu na liście kultur w `Program.cs`
   (`UseRequestLocalization` → `AddSupportedCultures` / `AddSupportedUICultures`),
   oraz `<option>` w rozwijanej liście języków w
   `Views/Shared/_UserInfo.cshtml`. Bez dwóch ostatnich plik nigdy nie zostanie użyty.

Szczegóły dotyczące procesu współtworzenia, standardów kodowania i sposobu przesyłania pull requestów znajdziesz w [CONTRIBUTING.md](https://github.com/s1t5/mail-archiver/blob/main/CONTRIBUTING.md).

## 🔗 Powiązana dokumentacja {#related-documentation}

- [Setup and Parameters](Setup.md) — `TimeZone__DisplayTimeZoneId`
- [Mail search](Search.md) — składnia zapytań
- [User Management](UserManagement.md) — role i uprawnienia
