# Import konta {#account-import}

[← Powrót do Indeksu Dokumentacji](index.md)

## Przegląd {#overview}

Mail Archiver obsługuje import wielu kont e-mail jednocześnie, dzięki czemu proces wdrażania dużej liczby skrzynek pocztowych nie wymaga tworzenia każdego konta z osobna.

Istnieją dwie ścieżki masowego importu, w zależności od dostawcy poczty:

| Dostawca | Metoda masowego importu | Dokumentacja |
|---|---|---|
| **Microsoft 365 (tenant)** | Wykrywanie tenantu przez Microsoft Graph — jedna rejestracja aplikacji uzyskuje dostęp do każdej skrzynki pocztowej w tenantcie | [Microsoft 365 Tenant Mailbox Import](M365TenantImport.md) |
| **IMAP** | Masowy import CSV — przesłanie pliku CSV z jednym wierszem na każdą skrzynkę pocztową | Ta strona (patrz poniżej) |

> **Uwaga:** Konta osobiste Microsoft (Outlook.com / M365 Family, dostawca **MSA**) używają interaktywnego przepływu autoryzacji kodu urządzenia i nie mogą być importowane masowo. Muszą być tworzone i autoryzowane pojedynczo. Patrz [MSA Outlook Setup](MSA_Outlook_Setup.md).

---

## Masowy import IMAP CSV {#imap-csv-bulk-import}

### Co robi ta funkcja {#what-it-does}

Masowy import CSV pozwala administratorowi na utworzenie setek kont poczty IMAP w ramach jednej operacji poprzez przesłanie pliku CSV. Każdy wiersz w pliku odpowiada jednej skrzynce pocztowej. Wspólne ustawienia serwera (host IMAP, port, SSL) mogą być określone raz w formularzu przesyłania i mają zastosowanie do każdego wiersza, z opcjonalnymi nadpisaniami dla poszczególnych wierszy.

Import jest dostępny **tylko dla administratora**. Jest on dostępny na stronie **Mail Accounts** poprzez menu rozwijane **Import** → **CSV Bulk Import**.

### Format pliku CSV {#csv-file-format}

Plik musi być zakodowany w formacie UTF-8, być plikiem CSV z **wierszem nagłówka** i **przecinkiem (`,`) jako separatorem**. Pola zawierające przecinki lub cudzysłowy muszą być ujęte w podwójny cudzysłów (`"..."`).

#### Kolumny {#columns}

| Kolumna | Wymagana | Opis |
|---|---|---|
| `email` | Tak | Adres skrzynki pocztowej. Używany również jako domyślna nazwa użytkownika logowania, gdy nie podano `username`. |
| `password` | Tak | Hasło logowania IMAP. Przechowywane w podanej formie — białe znaki na początku i na końcu są zachowywane. |
| `name` | Nie | Nazwa wyświetlana dla konta. Jeśli pominięto, domyślnie używa `{prefix} - <{email}>`. |
| `username` | Nie | Nazwa użytkownika logowania, jeśli różni się od adresu e-mail. |
| `imap_server` | Nie | Host IMAP dla danego wiersza. Nadpisuje wspólny serwer z formularza przesyłania. |
| `imap_port` | Nie | Port IMAP dla danego wiersza (1–65535). Nadpisuje wspólny port. |
| `use_ssl` | Nie | Przełącznik SSL dla danego wiersza (`true` lub `false`). Nadpisuje wspólne ustawienie. |

#### Przykład {#example}

```csv
email,password,name,username,imap_server,imap_port,use_ssl
alice@firma.de,secret123,Alice Müller,,,,,
bob@firma.de,p@ssw0rd,,bob@firma.de,,,993,
charlie@extern.de,pass3,,charlie,mail.extern.de,143,false
```

- Wiersz 1 używa wspólnego serwera/portu/SSL z formularza przesyłania.
- Wiersz 2 ustawia niestandardową nazwę użytkownika, ale w pozostałym zakresie używa wspólnych ustawień.
- Wiersz 3 nadpisuje serwer, port i SSL.

Przykładowy plik do pobrania jest dostępny na stronie importu poprzez przycisk **Download example CSV**.

### Formularz przesyłania {#upload-form}

| Pole | Cel |
|---|---|
| **CSV file** | Plik do przesłania. |
| **IMAP server** | Wspólny host dla wszystkich wierszy bez nadpisania `imap_server`. |
| **IMAP port** | Wspólny port (domyślnie 993). |
| **Use SSL** | Wspólny przełącznik SSL (domyślnie włączony). |
| **Name prefix** | Prefiks dla automatycznie generowanych nazw kont. Domyślnie: `IMAP`. |
| **Skip existing mailboxes** | Gdy jest włączone, wiersze, których adres e-mail już istnieje jako konto IMAP, są pomijane (liczone jako "pominięte", a nie błędy). Gdy jest wyłączone, są traktowane jako błędy. |
| **Account enabled** | Czy utworzone konta powinny być włączone do synchronizacji w tle. |
| **Delete After Days / Local Retention Days** | Opcjonalne ustawienia przechowywania stosowane do każdego utworzonego konta. Patrz [Retention Policies](RetentionPolicies.md). |

### Przetwarzanie {#processing}

1. Plik CSV jest analizowany, a każdy wiersz jest walidowany (format e-mail, obecność hasła, zakres portu, wartość SSL).
2. Duplikaty adresów e-mail w pliku są usuwane (liczy się pierwsze wystąpienie).
3. Wykrywane są istniejące konta IMAP z tym samym adresem e-mail; w zależności od przełącznika **Skip existing** są one pomijane lub zgłaszane jako błędy.
4. Wszystkie nowe konta są wstawiane w ramach jednej partii do bazy danych.
5. Generowany jest wpis w dzienniku dostępu podsumowujący przebieg operacji.

Podczas importu nie jest wykonywany test połączenia IMAP — konta są zapisywane natychmiast. Problemy z uwierzytelnianiem ujawnią się dopiero podczas pierwszej synchronizacji w tle. Dzięki temu import jest szybki nawet dla setek skrzynek pocztowych.

### Strona wyników {#result-page}

Po zakończeniu importu strona wyników wyświetla trzy tabele:

- **Created** — adres e-mail i nazwa każdego utworzonego konta.
- **Skipped** — adresy e-mail, które były duplikatami (w pliku lub już istniejącymi), gdy opcja **Skip existing** była włączona.
- **Failed** — wiersze, których nie udało się przetworzyć, wraz z numerem linii i powodem (brak e-maila, brak hasła, nieprawidłowy port, brak serwera IMAP itp.).

Baner podsumowujący wyświetla liczby: *{created} utworzonych, {skipped} pominiętych, {failed} nieudanych*.

### Limity {#limits}

Poniższe limity można skonfigurować za pomocą `appsettings.json` / zmiennych środowiskowych (patrz [Setup Guide](Setup.md) → *CSV Import Settings*):

| Ustawienie | Domyślne | Opis |
|---|---|---|
| `CsvImport__MaxRows` | 5000 | Maksymalna liczba wierszy przetwarzanych podczas jednego przesłania. |
| `CsvImport__MaxFileSizeBytes` | 10000000 (10 MB) | Maksymalny rozmiar przesyłanego pliku. |

### Uwaga dotycząca bezpieczeństwa {#security-note}

Hasła IMAP są przechowywane w bazie danych w taki sam sposób, jak w przypadku kont IMAP tworzonych indywidualnie. Należy pamiętać, że plik CSV zawiera wszystkie hasła skrzynek pocztowych w tekście jawnym — należy go usunąć z komputera klienta po pomyślnym imporcie.

---

## Import Microsoft 365 Tenant {#microsoft-365-tenant-import}

Dla Microsoft 365 (Exchange Online) masowy import nie wymaga pliku CSV. Pojedyncza rejestracja aplikacji Azure AD z odpowiednimi uprawnieniami Graph może uzyskać dostęp do każdej skrzynki pocztowej w tenantcie, dzięki czemu Mail Archiver może automatycznie wykrywać skrzynki pocztowe.

Zapoznaj się z dedykowanym przewodnikiem: **[Microsoft 365 Tenant Mailbox Import](M365TenantImport.md)**.
