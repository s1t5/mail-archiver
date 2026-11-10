# 📤 Eksport danych audytowych {#audit-data-export}

[← Powrót do spisu treści dokumentacji](index.md)

## 📋 Przegląd {#overview}

Eksport danych audytowych tworzy tabelaryczne pakiety masowych danych (ZIP z `INDEX.XML` + tabele CSV + indeks DTD) z istniejącego Archiwum. Pakiety te są zgodne z ustaloną strukturą indeksu dla przekazywania nośników danych i mogą być bezpośrednio importowane przez powszechne zewnętrzne narzędzia audytowe i analityczne obsługujące ten format indeksu.

Funkcja jest dostępna jako **dedykowana strona dostępna z poziomu strony Logs** i jest ograniczona do użytkowników z uprawnieniami administratora. Każde uruchomienie eksportu jest rejestrowane w dzienniku dostępu, dzięki czemu historia eksportu jest bezpieczna pod kątem rewizji.

## 🧭 Dostęp {#access}

1. Zaloguj się jako użytkownik administrator
2. Otwórz stronę **Logs**
3. Kliknij przycisk **Audit Data Export** w prawym górnym rogu

Użytkownicy bez uprawnień administratora nie widzą przycisku i nie mogą wywoływać żadnych punktów końcowych eksportu (serwer je odrzuca).

## 📦 Struktura pakietu eksportu {#export-package-structure}

Wygenerowany plik ZIP nazywa się `audit-export-<timestamp>.zip` i zawiera:

| Plik | Zawartość |
|---|---|
| `INDEX.XML` | Plik indeksu opisujący dostawcę danych, nośnik oraz wszystkie definicje tabel/kolumn |
| `index.dtd` | Plik DTD, do którego odwołuje się plik indeksu; wymagany przez narzędzia importujące do walidacji |
| `emails.csv` | Jeden wiersz na każdą zarchiwizowaną wiadomość e-mail w wybranym okresie/Skrzynce pocztowej |
| `attachments.csv` | Opcjonalnie; jeden wiersz na każdy Załącznik z eksportowanych wiadomości e-mail |

### Tabela `emails.csv` {#table-emailscsv}

Kolumny (bez wiersza nagłówka; nazwy kolumn pochodzą z `INDEX.XML`, zgodnie z tym formatem):

| Kolumna | Opis |
|---|---|
| Id | Wewnętrzny identyfikator e-mail (numeryczny) |
| MessageId | Nagłówek Email Message-ID |
| SentDate | Data wysłania, ISO 8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`) |
| ReceivedDate | Data otrzymania, ISO 8601 UTC |
| IsOutgoing | `1` = wychodząca, `0` = przychodząca |
| From | Nadawca |
| To | Odbiorcy |
| Cc | Odbiorcy CC |
| Bcc | Odbiorcy BCC |
| Subject | Temat |
| FolderName | Folder, w którym zarchiwizowano wiadomość e-mail |
| HasAttachments | `1` = z załącznikami, `0` = bez |
| AttachmentCount | Liczba załączników (numeryczna) |
| AccountEmail | Adres e-mail archiwizującej Skrzynki pocztowej |

### Tabela `attachments.csv` {#table-attachmentscsv}

| Kolumna | Opis |
|---|---|
| EmailId | Wewnętrzny identyfikator wiadomości e-mail, do której należy załącznik (klucz połączenia do `emails.csv`) |
| FileName | Nazwa pliku załącznika |
| ContentType | Typ MIME |
| Size | Rozmiar w bajtach (numeryczny) |
| Sha256 | Skrót SHA-256 zawartości (z magazynu załączników adresowanego treścią) |

### Konwencje CSV {#csv-conventions}

- Kodowanie: UTF-8 **bez** BOM (deklarowane jako `UTF8` w `INDEX.XML`)
- Separator kolumn: `;`
- Enkapsulator tekstu: `"` (podwojony w celu ucieczki znaków)
- Separator rekordów: `\n` (wyraźnie zadeklarowany w `INDEX.XML`)
- Daty: ISO 8601, czasy konwertowane z skonfigurowanej strefy czasowej wyświetlania na UTC
- Brak wiersza nagłówka; brak zlokalizowanych liczb (kolumny numeryczne zawierają zwykłe liczby całkowite)

## 🖥️ Korzystanie ze strony eksportu {#using-the-export-page}

### Pola formularza {#form-fields}

1. **From / To date** - Okres do eksportu (wymagane, maksymalny zakres jest konfigurowalny, domyślnie 10 lat)
2. **Mailbox** - Ogranicz eksport do pojedynczej Skrzynki pocztowej lub wyeksportuj wszystkie Skrzynki pocztowe
3. **Attachment metadata** - Po zaznaczeniu, `attachments.csv` zostanie dodane do pakietu
4. **Data supplier name / location / comment** - Zapisywane w bloku `DataSupplier` `INDEX.XML`; wypełniane automatycznie z sekcji konfiguracji `AuditExport` i edytowalne dla każdego eksportu

### Cykl życia zadania {#job-lifecycle}

1. Kliknięcie **Start Export** waliduje okres i Skrzynkę pocztową, tworzy zadanie i powraca do strony
2. Zadanie działa w tle (kolejkowe eksporty są przetwarzane jeden po drugim); tabela zadań pokazuje postęp na żywo (przetworzone/całkowita liczba e-maili wraz z paskiem postępu) poprzez automatyczne odpytywanie
3. Po zakończeniu eksportu w wierszu zadania pojawia się przycisk **Download**
4. Trwające lub oczekujące zadania można **anulować**

### Historia eksportu {#export-history}

Tabela historii pod formularzem zawiera ostatnie eksporty wraz z sygnaturą czasową, użytkownikiem, okresem, Skrzynką pocztową, liczbą e-maili, statusem i rozmiarem pliku. Historia jest **zachowana w bazie danych** (tabela `AuditExportJobs`), więc przetrwa restarty aplikacji i jest bezpieczna pod kątem rewizji.

Pobrane pliki są przechowywane przez konfigurowalny okres Przechowywania (domyślnie 30 dni, patrz `AuditExport__RetentionDays`); po tym czasie pliki ZIP są usuwane podczas codziennego czyszczenia, podczas gdy wiersze historii pozostają.

> 📝 **Uwaga**: Pliki eksportu są przechowywane wewnątrz kontenera aplikacji. Jeśli chcesz zachować je na dłużej, pobierz je i przechowuj w swoim repozytorium dowodów audytowych.

## 🔐 Audytowanie {#auditing}

Każdy eksport generuje dwa wpisy w dzienniku dostępu nowego typu **Audit Data Export**:

1. **Wpis rozpoczęcia** - gdy eksport trafia do kolejki (z ID zadania, okresem, Skrzynką pocztową, opcjami w formacie JSON)
2. **Wpis wyniku** - gdy eksport się kończy, kończy się błędem lub zostaje anulowany (z ID zadania, okresem, Skrzynką pocztową, liczbą e-maili, wynikiem i rozmiarem pakietu w formacie JSON)

Wpisy pojawiają się na stronie Logs tak jak wszystkie inne wpisy w dzienniku dostępu i mogą być filtrowane według nowego typu. Rozwijana lista filtrów typu automatycznie je wykrywa.

## ⚙️ Konfiguracja {#configuration}

Sekcja `AuditExport` w `appsettings.json` (zmienne środowiskowe używają składni z podwójnym podkreśleniem, np. `AuditExport__RetentionDays`):

| Ustawienie | Opis | Domyślne |
|---|---|---|
| `DataSupplierName` | Domyślna wartość pola formularza nazwy dostawcy danych | puste |
| `DataSupplierLocation` | Domyślna wartość pola formularza lokalizacji dostawcy danych | puste |
| `Comment` | Domyślna wartość pola formularza komentarza | puste |
| `OutputDirectory` | Katalog dla generowanych plików ZIP (ścieżki względne są rozwiązywane względem głównego katalogu zawartości aplikacji) | `exports/audit` |
| `RetentionDays` | Liczba dni do usunięcia ukończonych plików eksportu | `30` |
| `MaxRangeYears` | Maksymalny dozwolony zakres między datą rozpoczęcia a końcem okresu | `10` |

## 🗄️ Baza danych {#database}

Historia eksportu jest przechowywana w tabeli `mail_archiver."AuditExportJobs"` (utworzonej przez migrację `MigrateV2609_1`, stosowaną automatycznie podczas uruchamiania). Ta funkcja nie modyfikuje żadnych istniejących tabel.

## 🔍 Rozwiązywanie problemów {#troubleshooting}

| Problem | Przyczyna / Rozwiązanie |
|---|---|
| Brak przycisku Download po restarcie | Pliki eksportu znajdują się w kontenerze; jeśli kontener został utworzony ponownie lub minął okres Przechowywania, plik ZIP zniknął, podczas gdy wiersz historii pozostał. Ponownie uruchom eksport. |
| "The period must not exceed X years" | Zmniejsz okres lub zwiększ `AuditExport__MaxRangeYears`. |
| Zadanie pozostaje w stanie *Running* przez długi czas | Duże Archiwa są przesyłane w paczkach po 1000 e-maili; postęp jest aktualizowany na bieżąco. Bardzo długie okresy mogą zająć trochę czasu — sprawdź licznik przetworzone/całkowita. |
| Narzędzie importujące zgłasza problemy z kodowaniem | Pakiet to UTF-8 bez BOM z zadeklarowaną flagą `UTF8` i wyraźnym separatorem rekordów; upewnij się, że narzędzie odczytuje `INDEX.XML` obok plików danych (rozpakuj cały plik ZIP, nie otwieraj plików pojedynczo wewnątrz ZIP). |

## 🔗 Powiązana dokumentacja {#related-documentation}

- [Access Logging](Logs.md)
- [Installation, Setup and Parameters](Setup.md)
