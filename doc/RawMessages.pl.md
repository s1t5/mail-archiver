# 📧 Original Message Storage {#original-message-storage}

[← Powrót do spisu treści dokumentacji](index.md)

## 📋 Przegląd {#overview}

Domyślnie Archiwum przechowuje **przetworzone części** wiadomości e-mail: nagłówki, tekstowe i HTML treści oraz załączniki. Eksportowany `.eml` jest odbudowywany na podstawie tych pól.

To wystarczy, aby przeczytać e-mail, ale nie jest to wiadomość **w takiej, jaka została odebrana**. Kodowania transferu, granice MIME i zawijanie nagłówków różnią się od oryginału, więc odbudowana wiadomość nie przechodzi już weryfikacji podpisu DKIM, a skanery spamu oceniają ją inaczej.

Gdy przechowywanie oryginalnej wiadomości jest włączone, Archiwum przechowuje kompletną wiadomość w takiej formie, w jakiej dotarła, obok przetworzonych pól. Przechowywany oryginał to coś, co Archiwum może udowodnić jako dostarczone.

| | Tylko przetworzone pola | Z przechowywanym oryginałem |
| --- | --- | --- |
| Nagłówki, treść, załączniki | ✅ | ✅ |
| Wiadomość identyczna bajt po bajcie | ❌ | ✅ |
| Weryfikacja podpisu DKIM | ❌ | ✅ |
| Zastosowania kryminalistyczne (sąd, audyt) | ograniczone | ✅ |

## 🎯 Gdzie serwowana jest oryginał {#where-the-original-is-served}

Po zapisaniu, oryginał jest zwracany wszędzie tam, gdzie oczekiwana jest kompletna wiadomość:

| Miejsce | Co się dzieje |
| --- | --- |
| Przycisk **Export EML** na stronie szczegółów e-mail | Serwuje zapisany oryginał, jeśli istnieje; w przeciwnym razie przechodzi do rekonstrukcji dla wiadomości zarchiwizowanych bez oryginału. |
| **Restore to a mailbox** | Dołącza zapisany oryginał, jeśli istnieje; w przeciwnym razie przechodzi do rekonstrukcji dla wiadomości zarchiwizowanych bez oryginału. |
| `GET /api/v1/emails/{id}/raw` | Pobieranie przez REST API, patrz [REST API guide](API.md#download-original-message). |
| `get_email_raw` | Narzędzie MCP, patrz [MCP guide](MCP.md#get_email_raw). |

Odpowiedź informuje, jaką formę otrzymałeś, dzięki czemu wywołujący może odróżnić prawdziwy oryginał od rekonstrukcji bez sprawdzania bajtów:

| Sygnał | Znaczenie |
| --- | --- |
| `X-Raw-Message-Source: imap` | Dokładna kopia z serwera IMAP (`BODY[]` w dostarczonej formie). |
| `X-Raw-Message-Source: graph` | Dokładna kopia, zawartość MIME zwrócona przez Exchange. |
| `X-Raw-Message-Source: eml-file` | Dokładna kopia, zawartość zaimportowanego pliku `.eml`. |
| `X-Raw-Message-Source: serialized` | Przetworzona wiadomość zapisana ponownie. Identyczna z wyjątkiem zakończeń linii CRLF (import MBox, pobieranie podczas odzyskiwania IMAP). |
| `X-Raw-Message-Sha256` | SHA-256 zapisanego oryginału. |
| `X-Raw-Message-Reconstructed: true` | Brak zapisanego oryginału; wiadomość została odbudowana. Brak nagłówka sumy kontrolnej. |
| `X-Raw-Message-Storage` | Backend, z którego odczytano oryginał (`postgres` lub `filesystem`). |

Nazwa pliku również to wskazuje: `email-{id}-original-...eml` dla zapisanego oryginału, `email-{id}-...eml` dla rekonstrukcji.

W przypadku, gdy zapisany oryginał **nie przejdzie weryfikacji sumy kontrolnej**, eksport i przywracanie zostają odrzucone z komunikatem o błędzie. Zwrócenie rekonstrukcji zamiast oryginału ukryłoby fakt, że archiwalna kopia została zmieniona, a tego Archiwum nigdy nie może robić.

Przywracanie odczytuje zapisany oryginał i dołącza go poprzez klienta dostawcy, dzięki czemu Skrzynka pocztowa otrzymuje oryginalne nagłówki i strukturę MIME. Dokładny strumień bajtów przesyłany przez sieć nie jest gwarantowany, ponieważ klient ponownie serializuje wiadomość; sama archiwalna kopia pozostaje niezmieniona.

## ⚙️ Ustawienia {#settings}

Wszystkie ustawienia znajdują się w sekcji `RawMessages` lub jako zmienne środowiskowe z prefiksem `RawMessages__`.

| Ustawienie | Domyślne | Opis |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Przechowuj oryginalną wiadomość każdej nowo zarchiwizowanej wiadomości e-mail. Wiadomości e-mail zarchiwizowane przed włączeniem tej opcji nie posiadają oryginału. |
| `RawMessages:MaxMessageSizeMb` | `50` | Wiadomości większe niż ta wartość są archiwizowane bez oryginału. `0` = brak limitu. |
| `RawMessages:IncludeGraph` | `true` | Przechowuj również oryginał dla kont Microsoft 365 (Graph). Kosztuje to jedno dodatkowe żądanie Graph na wiadomość, co obciąża limit ograniczania przepustowości (throttling). |
| `RawMessages:Storage` | `postgres` | Miejsce przechowywania bajtów wiadomości: `postgres` (w bazie danych) lub `filesystem` (plik gzip obok Archiwum). Zobacz [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Katalog, do którego zapisywane są pliki wiadomości, względem głównego katalogu zawartości aplikacji. Skieruj to na zamontowany wolumen w Docker. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Liczba dwuznakowych poziomów katalogów, na które rozłożony jest identyfikator wiadomości e-mail. `2` daje 10 000 katalogów, `0` jest płaska. Utrzymuje niską liczbę plików na katalog w dużych Archiwum. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Ponownie obliczaj SHA-256 pliku przy każdym pobieraniu i porównuj go z sumą kontrolną zapisaną w czasie archiwizacji. Niezgodność powoduje błąd pobierania z kodem HTTP 500 zamiast serwowania zmienionych bajtów. |

### Rozmiar {#sizing}

Przechowywanie rośnie mniej więcej o rozmiar każdej wiadomości wraz z jej załącznikami, po kompresji gzip. Załączniki **nie** podlegają deduplikacji wewnątrz przechowywanego oryginału, więc wiadomość e-mail z dużym załącznikiem kosztuje dwa razy tyle: raz w zdeduplikowanym magazynie załączników i raz wewnątrz oryginału. Planuj rozmiar bazy danych na poziomie około **dwukrotnie** większym niż w przypadku Archiwum bez oryginałów.

Włączenie tego ustawienia nie uzupełnia istniejących wiadomości. Tylko nowo archiwizowane wiadomości e-mail otrzymują oryginał.

## 💾 Backend przechowywania {#storage-backend}

Ustawienie `Storage` decyduje o tym, gdzie znajdują się bajty. **Wiersz metadanych zawsze pozostaje w
bazie danych**, niezależnie od skonfigurowanego backendu, i przechowuje SHA-256, rozmiar, źródło przechwycenia oraz (dla plików) ścieżkę względną. Ten wiersz pozwala Archiwum udowodnić, co powinna zawierać
przechowywana wiadomość.

### postgres (default) {#postgres-default}

Skompresowana wiadomość znajduje się w kolumnie `Content` `mail_archiver."RawMessages"`.
Jest to zachowanie, które Archiwum miało zawsze i które nie wymaga dodatkowego wolumenu.

Wybierz tę opcję, gdy baza danych jest już Twoją jednostką do kopii zapasowej i chcesz mieć jedną rzecz do
wykonania kopii zapasowej i przywracania.

### filesystem {#filesystem}

Wiadomość jest zapisywana w `{BasePath}/{xx}/{yy}/{id}.eml.gz`, gdzie `xx`/`yy` są pochodne od
id wiadomości e-mail. Wiersz w bazie danych przechowuje tylko metadane.

Wybierz tę opcję, gdy Archiwum jest na tyle duże, że oryginały zdominowałyby rozmiar bazy danych, lub gdy chcesz umieścić pliki wiadomości na tańszym lub oddzielnym nośniku.

!!! danger "Zamontuj wolumen w Docker"
    Domyślny `BasePath` (`RawMessages`) rozwiązuje się do `/app/RawMessages` **wewnątrz
    kontenera**. Bez bind mountu ten katalog należy do warstwy zapisywalnej kontenera, więc każdy ponownie utworzony kontener – w tym `docker compose down && docker compose
    up -d`, aktualizacja obrazu lub restart hosta z warstwą efemeryczną – zabiera ze sobą każdy przechowywany
    oryginał, podczas gdy wiersze w bazie danych przetrwają.

    Zawsze montuj katalog na hoście podczas używania backendu filesystem:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Ścieżka hosta (tutaj `./raw-messages`) to to, co tworzysz jako kopia zapasowa. Ścieżka kontenera musi
    zgadzać się z `RawMessages:FileSystem:BasePath` lub należy ustawić `RawMessages:FileSystem:BasePath` na
    punkt montowania, który wybrałeś. Jeśli przeniesiesz montowanie, spraw, aby `BasePath` nadal na nie wskazywało:
    przechowywane wartości `StoragePath` są względne, więc nadal będą poprawnie rozwiązywane.

### Przełączanie backendów {#switching-backends}

Odczyty podążają za miejscem przechowywania zapisanym w każdym wierszu, a nie za skonfigurowanym ustawieniem. Po
zmianie `Storage`, wcześniej zarchiwizowane wiadomości pozostają dostępne do pobrania z miejsca, w którym się znajdują.
Ustawienie wpływa tylko na **nowo** archiwizowane poczta.

Aby przenieść to, co jest już zapisane, uruchom migrację przechowywania. Pełną dokumentację znajdziesz w
[CLI commands](CLI.md#moving-stored-original-messages):

```bash
# database -> filesystem
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from postgres --to filesystem

# filesystem -> database
docker compose exec mailarchiver dotnet MailArchiver.dll \
  --migrate-raw-storage --from filesystem --to postgres
```

Co gwarantuje migracja:

- Każda wiadomość jest weryfikowana pod kątem zapisanego sumy kontrolnej przed skopiowaniem. Wiadomość,
  która zakończy się błędem, zostaje tam, gdzie była, i zostaje zgłoszona: uszkodzony oryginał nigdy nie jest propagowany, a
  jego jedyna pozostała kopia nigdy nie zostaje usunięta.
- Plik jest usuwany dopiero po zatwierdzeniu transakcji bazy danych, która go przeniosła, więc
  przerwane działanie nie może spowodować utraty jedynej kopii.
- Proces można wznowić. Praca jest wybierana przez kolumnę `StorageType`, więc ponowne uruchomienie
  polecenia kontynuuje pracę z tym, co wciąż jest do zrobienia.
- Kod wyjścia to `1`, jeśli jakakolwiek wiadomość zakończyła się błędem, `0` w przeciwnym razie.

Migracja nie jest wymagana do działania Archiwum. Jest ona potrzebna tylko do przeniesienia istniejących
bajtów do innego backendu.

## 🛟 Kopia zapasowa i przywracanie {#backup-and-restore}

=== "postgres backend"
    Nic dodatkowego. Oryginały są częścią bazy danych i są objęte standardowym
    zrzutem, patrz [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Wykonaj kopię zapasową katalogu `BasePath` **wraz z** zrzutem bazy danych.

    Przywracanie bazy danych bez plików pozostawia Archiwum z wierszami, których zawartość
    nie może zostać udostępniona. Pobieranie zwraca wtedy błąd HTTP 404 zamiast zwracać
    nieprawidłowe dane, a same wiadomości e-mail pozostają czytelne dzięki ich sparsowanym polom.

## 🗑️ Usuwanie wiadomości e-mail {#deleting-an-email}

Usunięcie wiadomości e-mail usuwa również jej oryginał: reguły Przechowywanie, ręczne usuwanie pojedyncze i masowe oraz usunięcie całego Konta pocztowego usuwają plik jako część tej samej operacji. W przypadku backendu `postgres` nie trzeba robić nic dodatkowego, ponieważ bajty znajdują się wewnątrz wiersza, który jest już usuwany w procesie kaskadowym.

Plik jest usuwany **po** zatwierdzeniu wiersza wiadomości e-mail, nigdy przed. Te dwa kroki nie mogą stanowić jednej transakcji, ponieważ plik nie jest częścią transakcji bazy danych, więc jeden z nich musi nastąpić jako drugi. Ta kolejność jest bezpieczna: usunięcie, które zakończy się niepowodzeniem lub zostanie wycofane, na przykład na zablokowanym rekordzie, pozostawia oryginał nienaruszonym, a awaria między tymi dwoma krokami pozostawia plik bez wiersza, który zostanie odzyskany podczas poniższego czyszczenia. Inna kolejność zniszczyłaby oryginał wiadomości e-mail, która wciąż istnieje.

**Osierocone pliki są zbierane.** Wszystko, co prześlizgnęło się przez proces, w tym pliki pozostawione przez usunięcia sprzed wprowadzenia tego czyszczenia, jest usuwane podczas okresowego skanowania `BasePath`, które porównuje każdy plik z wierszami i usuwa te, do których nie odnosi się żadna wiadomość e-mail:

| Setting | Default | Meaning |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Włącz lub wyłącz skanowanie |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Jak często jest uruchamiane |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Pliki młodsze niż ta wartość nigdy nie są dotykane |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Opóźnienie przed pierwszym uruchomieniem, aby najpierw zakończyła się oczekująca migracja pamięci masowej |

Okres karencji sprawia, że skanowanie jest bezpieczne: import zapisuje plik przed zatwierdzeniem wiersza, który na niego wskazuje, a plik zapisany chwilę temu nie może zostać pomylony z osieroconym. Półpisane pliki `.tmp` z przerwanego zapisu są zbierane według tej samej reguły. W przypadku backendu `postgres` skanowanie nie wykonuje żadnych działań i nawet nie przeszukuje katalogu.

## 🔍 Weryfikacja Archiwum {#verifying-an-archive}

Suma kontrolna zarejestrowana w czasie tworzenia Archiwum umożliwia przeprowadzenie weryfikacji.

- **Podczas pobierania** (`VerifyChecksumOnRead=true`, domyślnie) plik jest haszowany i
  porównywany. Niezgodność powoduje odrzucenie pobierania z błędem HTTP 500.
- **Podczas migracji składowania** każda wiadomość jest weryfikowana przed skopiowaniem, w obu
  kierunkach.

Ponieważ wiersz metadanych znajduje się w bazie danych, a bajty mogą znajdować się na dysku, porównanie sum kontrolnych jest jedynym sposobem na zauważenie, że plik został zmieniony poza aplikacją.

## ⚠️ Ważne informacje {#things-to-know}

- **Włączenie funkcji nie powoduje uzupełnienia danych historycznych.** Tylko wiadomości zarchiwizowane po zmianie ustawień otrzymają oryginał.
- **Brakujący oryginał jest logowany jako błąd.** Gdy przechwycenie zostanie odrzucone (wiadomość powyżej `MaxMessageSizeMb`, brak użytecznego przechwycenia) lub gdy zapis do skonfigurowanego backendu zakończy się niepowodzeniem, dziennik zawiera błąd z identyfikatorem e-mail oraz powodem. E-mail pozostaje w Archiwum bez swojego oryginału; nie ma automatycznego uzupełniania danych ani ponawiania prób.
- **Strona szczegółów oznacza brakujący oryginał.** Przy `RawMessages:Enabled` strona szczegółów wyświetla powiadomienie nad nagłówkami. System pyta backend pamięci masowej, więc plik w systemie plików, który zniknął, również zostanie oznaczony. Lista e-maili nie posiada odznak w poszczególnych wierszach; zajęłoby to zbyt dużo miejsca w kolumnie statusu.
- **Rekonstrukcja nie jest wiadomością.** W przypadku e-maili bez zapisanego oryginału każda ścieżka pobierania zwraca odbudowaną wiadomość i informuje o tym za pomocą `X-Raw-Message-Reconstructed: true`.
- **System plików oznacza dwie jednostki kopii zapasowej.** Bazę danych i katalog wiadomości należy przywracać razem, w przeciwnym razie wiersze przetrwają bez swojej zawartości.
- **`VerifyChecksumOnRead` obciąża procesor.** Każde pobranie dużej wiadomości wiąże się z dekompresją i haszowaniem. Nie wyłączaj tej funkcji, chyba że zmierzysz problem; jest to jedyna ochrona przed cichą modyfikacją danych na dysku.
- **Zapisany oryginał nie podlega deduplikacji.** Załącznik, który pojawia się w wielu e-mailach, jest przechowywany raz w magazynie załączników, ale raz dla każdego e-maila wewnątrz oryginału.
- **Usunięcie usuwa również oryginał.** E-mail usunięty przez Przechowywanie lub ręcznie zabiera ze sobą swój zapisany oryginał, plik i wiersz. Nic nie jest zachowywane, jeśli Archiwum nie wymienia już danej pozycji.

## 🔗 Zobacz także {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - pełny proces migracji przechowywania
- [Setup and Parameters](Setup.md) - wszystkie parametry konfiguracji
- [REST API](API.md#download-original-message) - punkt końcowy `/raw`
- [MCP Server](MCP.md#get_email_raw) - narzędzie `get_email_raw`
- [Backup and Restore](BackupRestore.md) - co należy objąć kopią zapasową
- [Attachment Deduplication](AttachmentDeduplication.md) - sposób przechowywania załączników
