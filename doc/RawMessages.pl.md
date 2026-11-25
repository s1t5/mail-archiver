# 📧 Przechowywanie oryginalnej wiadomości {#original-message-storage}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Domyślnie Archiwum przechowuje **przetworzone części** wiadomości e-mail: nagłówki, tekst i treści HTML oraz załączniki. Eksportowany `.eml` jest odbudowywany z tych pól.

To wystarczy, aby przeczytać e-mail, ale nie jest to wiadomość **w takiej formie, w jakiej została odebrana**. Kodowania transferu, granice MIME i składanie nagłówków różnią się od oryginału, więc odbudowana wiadomość nie weryfikuje już podpisu DKIM, a skanery spamu oceniają ją inaczej.

Po włączeniu przechowywania oryginalnej wiadomości, Archiwum przechowuje kompletną wiadomość w takiej formie, w jakiej dotarła, obok przetworzonych pól. Przechowywany oryginał jest tym, co Archiwum może udowodnić jako dostarczone.

| | Tylko przetworzone pola | Z przechowywanym oryginałem |
| --- | --- | --- |
| Nagłówki, treść, załączniki | ✅ | ✅ |
| Wiadomość identyczna co do bajtów | ❌ | ✅ |
| Weryfikacja podpisu DKIM | ❌ | ✅ |
| Zastosowanie śledcze (sąd, audyt) | ograniczone | ✅ |

## 🎯 Gdzie serwowana jest oryginał {#where-the-original-is-served}

Po zapisaniu, oryginał jest zwracany wszędzie tam, gdzie oczekiwana jest kompletna wiadomość:

| Miejsce | Co się dzieje |
| --- | --- |
| Przycisk **Export EML** na stronie szczegółów e-mail | Serwuje zapisany oryginał, jeśli istnieje, w przeciwnym razie przechodzi do rekonstrukcji dla e-maili zarchiwizowanych bez oryginału. |
| **Przywracanie do Skrzynki pocztowej** | Dołącza zapisany oryginał, jeśli istnieje, w przeciwnym razie przechodzi do rekonstrukcji dla e-maili zarchiwizowanych bez oryginału. |
| `GET /api/v1/emails/{id}/raw` | Pobieranie przez REST API, patrz [przewodnik REST API](API.md#download-original-message). |
| `get_email_raw` | Narzędzie MCP, patrz [przewodnik MCP](MCP.md#get_email_raw). |

Odpowiedź informuje, jaką formę otrzymałeś, dzięki czemu wywołujący może odróżnić autentyczny oryginał od rekonstrukcji bez sprawdzania bajtów:

| Sygnał | Znaczenie |
| --- | --- |
| `X-Raw-Message-Source: imap` | Dokładna kopia z serwera IMAP (`BODY[]` w dostarczonej formie). |
| `X-Raw-Message-Source: graph` | Dokładna kopia, zawartość MIME zwrócona przez Exchange. |
| `X-Raw-Message-Source: eml-file` | Dokładna kopia, zawartość zaimportowanego pliku `.eml`. |
| `X-Raw-Message-Source: serialized` | Przetworzona wiadomość zapisana ponownie. Identyczna, z wyjątkiem zakończeń linii, które są CRLF (import MBox, pobieranie z odzyskiwania IMAP). |
| `X-Raw-Message-Sha256` | SHA-256 zapisanego oryginału. |
| `X-Raw-Message-Reconstructed: true` | Brak zapisanego oryginału; wiadomość została odbudowana. Brak nagłówka sumy kontrolnej. |
| `X-Raw-Message-Storage` | Backend, z którego odczytano oryginał (`postgres` lub `filesystem`). |

Nazwa pliku również to wskazuje: `email-{id}-original-...eml` dla zapisanego oryginału, `email-{id}-...eml` dla rekonstrukcji.

Gdy zapisany oryginał **nie przechodzi weryfikacji sumy kontrolnej**, eksport i przywracanie są odrzucane z komunikatem błędu. Zwrócenie rekonstrukcji zamiast oryginału ukryłoby fakt, że zarchiwizowana kopia została zmieniona, a tego Archiwum nigdy nie może robić.

Przywracanie odczytuje zapisany oryginał i dołącza go za pośrednictwem klienta dostawcy, dzięki czemu Skrzynka pocztowa otrzymuje oryginalne nagłówki i strukturę MIME. W przypadku IMAP klient ponownie serializuje wiadomość, więc dokładny strumień bajtów nie jest gwarantowany w transmisji. W Microsoft 365 zarchiwizowane bajty przesyłane są bez zmian, ponieważ Graph akceptuje wiadomość w formie MIME. Sama zarchiwizowana kopia nigdy nie jest modyfikowana.

## ⚙️ Ustawienia {#settings}

Wszystkie ustawienia znajdują się w sekcji `RawMessages` lub jako zmienne środowiskowe z prefiksem `RawMessages__`.

| Ustawienie | Domyślne | Opis |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Przechowuj oryginalną wiadomość każdego nowo zarchiwizowanego e-maila. E-maile zarchiwizowane przed włączeniem tej opcji nie posiadają oryginału. |
| `RawMessages:MaxMessageSizeMb` | `50` | Wiadomości większe niż ta są archiwizowane bez ich oryginału. `0` = brak limitu. |
| `RawMessages:IncludeGraph` | `true` | Przechowuj również oryginał dla kont Microsoft 365 (Graph). Kosztuje to jedną dodatkową prośbę Graph na wiadomość, co obciąża budżet ograniczania liczby żądań (throttling). |
| `RawMessages:Storage` | `postgres` | Miejsce przechowywania bajtów wiadomości: `postgres` (w bazie danych) lub `filesystem` (plik gzip obok Archiwum). Zobacz [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Katalog, do którego zapisywane są pliki wiadomości, względem głównego katalogu zawartości aplikacji. Wskaż tutaj zamontowany wolumen w Dockerze. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Liczba dwuznakowych poziomów katalogów, na które rozproszony jest identyfikator e-maila. `2` daje 10 000 katalogów, `0` jest płaski. Pozwala to utrzymać niską liczbę plików w katalogu w przypadku dużych Archiwów. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Ponownie obliczaj SHA-256 pliku przy każdym pobieraniu i porównuj go z sumą kontrolną zapisaną w czasie archiwizacji. Niezgodność powoduje błąd pobierania HTTP 500 zamiast serwowania zmienionych bajtów. |

### Rozmiarowanie {#sizing}

Rozmiar przechowywania rośnie mniej więcej o rozmiar każdej wiadomości wraz z jej Załącznikami, w formacie skompresowanym gzip. Załączniki **nie** podlegają procesowi Deduplikacja wewnątrz przechowywanego oryginału, więc e-mail z dużym Załącznikiem kosztuje dwa razy tyle: raz w zdeduplikowanym magazynie Załączników, a drugi raz wewnątrz oryginału. Należy zaplanować rozmiar bazy danych wynoszący w przybliżeniu **dwukrotność** rozmiaru Archiwum bez oryginałów.

Włączenie ustawienia nie uzupełnia istniejących wiadomości. Tylko nowo archiwizowane e-maile otrzymują oryginał.

📧 Original Message Storage

## 💾 Storage backend {#storage-backend}

Ustawienie `Storage` decyduje o tym, gdzie znajdują się bajty. **Wiersz metadanych zawsze pozostaje w
bazie danych**, niezależnie od skonfigurowanego backendu, i przechowuje SHA-256, rozmiar, źródło przechwycenia oraz (dla plików) ścieżkę względną. To właśnie ten wiersz pozwala Archiwum udowodnić, co powinna
zawierać przechowywana wiadomość.

### postgres (default) {#postgres-default}

Skompresowana wiadomość znajduje się w kolumnie `Content` `mail_archiver."RawMessages"`.
Jest to zachowanie, które Archiwum miało zawsze i które nie wymaga dodatkowej wolumenu.

Wybierz to rozwiązanie, gdy baza danych jest już Twoją jednostką do Kopia zapasowa i chcesz mieć jedną rzecz do
wykonania operacji Kopia zapasowa i Przywracanie.

### filesystem {#filesystem}

Wiadomość jest zapisywana w `{BasePath}/{xx}/{yy}/{id}.eml.gz`, gdzie `xx`/`yy` są pochodne od identyfikatora e-mail. Wiersz w bazie danych przechowuje tylko metadane.

Wybierz to rozwiązanie, gdy Archiwum jest na tyle duże, że oryginały zdominowałyby rozmiar bazy danych, lub gdy chcesz umieścić pliki wiadomości na tańszym lub oddzielnym magazynie.

!!! danger "Mount a volume in Docker"
    Domyślny `BasePath` (`RawMessages`) rozwiązuje się do `/app/RawMessages` **wewnątrz
    kontenera**. Bez bind mountu ten katalog należy do warstwy zapisywalnej kontenera, więc każdy odtworzony kontener - w tym `docker compose down && docker compose
    up -d`, aktualizacja obrazu lub restart hosta z warstwą efemeryczną - zabiera ze sobą każdy przechowywany
    oryginał, podczas gdy wiersze w bazie danych przetrwają.

    Zawsze montuj katalog na hoście podczas używania backendu filesystem:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Ścieżka hosta (tutaj `./raw-messages`) to jest to, co robisz Kopia zapasowa. Ścieżka kontenera musi
    zgadzać się z `RawMessages:FileSystem:BasePath`, lub ustaw `RawMessages:FileSystem:BasePath` na
    punkt montowania, który wybrałeś. Jeśli przeniesiesz montowanie, zachowaj `BasePath` wskazujący na nie:
    przechowywane wartości `StoragePath` są względne, więc nadal będą rozwiązywane.

### Switching backends {#switching-backends}

Odczyty podążają za magazynem zapisanym w każdym wierszu, a nie za skonfigurowanym ustawieniem. Po
zmianie `Storage`, wcześniej zarchiwizowane wiadomości pozostają dostępne do pobrania z dowolnego miejsca, w którym się znajdują.
Ustawienie wpływa tylko na **nowo** archiwizowane poczta.

Aby przenieść to, co jest już przechowywane, uruchom migrację magazynu. Pełną referencję znajdziesz w
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

- Każda wiadomość jest weryfikowana pod kątem jej zapisanego sumy kontrolnej przed jej skopiowaniem. Wiadomość,
  która zakończy się błędem, zostaje tam, gdzie była, i jest raportowana: uszkodzony oryginał nigdy nie jest propagowany i
  jego jedyna pozostała kopia nigdy nie jest usuwana.
- Plik jest usuwany dopiero po zatwierdzeniu transakcji bazy danych, która go przeniosła, dzięki czemu przerwane
  uruchomienie nie może spowodować utraty jedynej kopii.
- Proces można wznowić. Praca jest wybierana przez kolumnę `StorageType`, więc ponowne uruchomienie
  polecenia kontynuuje pracę z tym, co jest jeszcze zaległe.
- Kod wyjścia to `1`, jeśli jakakolwiek wiadomość zakończyła się błędem, `0` w przeciwnym razie.

Migracja nie jest wymagana, aby Archiwum działało. Jest ona potrzebna tylko do przeniesienia istniejących
bajtów do innego backendu.

## 🛟 Kopia zapasowa i przywracanie {#backup-and-restore}

=== "postgres backend"
    Nic dodatkowego. Oryginały są częścią bazy danych i są objęte normalną operacją
    dump, patrz [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Wykonaj kopię zapasową katalogu `BasePath` **razem z** dumpem bazy danych.

    Przywracanie bazy danych bez plików pozostawia Archiwum z wierszami, których zawartość
    nie może zostać udostępniona. Pobieranie zwraca wtedy błąd HTTP 404 zamiast zwracać coś
    błędnego, a same e-maile pozostają czytelne z ich przetworzonych pól.

## 🗑️ Usuwanie wiadomości e-mail {#deleting-an-email}

Usuwanie wiadomości e-mail usuwa wraz z nią jej oryginał: reguły Przechowywania, ręczne usuwanie pojedyncze i masowe oraz usuwanie całego konta pocztowego usuwają plik jako część tej samej operacji. W przypadku backendu `postgres` nie trzeba robić nic dodatkowego, ponieważ bajty przemieszczają się wewnątrz wiersza, który kaskadowo jest już usuwany.

Plik jest usuwany **po** zatwierdzeniu wiersza wiadomości e-mail, nigdy przed. Te dwa kroki nie mogą być jedną transakcją, ponieważ plik nie jest częścią transakcji bazy danych, więc jeden z nich musi nastąpić jako drugi. Ta kolejność jest bezpieczna: usunięcie, które się nie powiedzie lub zostanie wycofane, na przykład na zablokowanym rekordzie, pozostawia oryginał nienaruszonym, a awaria między tymi dwoma krokami pozostawia plik bez wiersza, który poniższy proces czyszczenia odzyskuje. Inna kolejność zniszczyłaby oryginał wiadomości e-mail, która wciąż istnieje.

**Osierocone pliki są zbierane.** Wszystko, co prześlizgnęło się przez system, w tym pliki pozostawione przez usunięcia sprzed istnienia tego czyszczenia, jest usuwane przez okresowe czyszczenie `BasePath`, które porównuje każdy plik z wierszami i usuwa te, do których żadna wiadomość e-mail nie odnosi się:

| Ustawienie | Domyślne | Znaczenie |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Włączenie lub wyłączenie czyszczenia |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Jak często jest uruchamiane |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Pliki młodsze od tej wartości nigdy nie są dotykane |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Opóźnienie przed pierwszym uruchomieniem, aby najpierw zakończyła się oczekująca migracja pamięci masowej |

Okres karencji sprawia, że czyszczenie jest bezpieczne: import zapisuje plik przed zatwierdzeniem wiersza, który na niego wskazuje, a plik zapisany przed chwilą nie może zostać pomylony z osieroconym. Półpisane pliki `.tmp` z przerwanego zapisu są zbierane według tej samej reguły. W przypadku backendu `postgres` czyszczenie nic nie robi i nawet nie przeszukuje katalogu.

## 🔍 Weryfikacja Archiwum {#verifying-an-archive}

Suma kontrolna zarejestrowana w czasie tworzenia Archiwum jest tym, co umożliwia weryfikację.

- **Podczas pobierania** (`VerifyChecksumOnRead=true`, domyślne) plik jest hashowany i
  porównywany. Niezgodność powoduje odrzucenie pobierania z błędem HTTP 500.
- **Podczas migracji składowania** każda wiadomość jest weryfikowana przed jej skopiowaniem, w obu
  kierunkach.

Ponieważ wiersz metadanych znajduje się w bazie danych, a bajty mogą znajdować się na dysku, porównanie sum kontrolnych jest jedynym sposobem na zauważenie, że plik został zmieniony poza aplikacją.

## ⚠️ Rzeczy, które należy wiedzieć {#things-to-know}

- **Włączenie nie powoduje uzupełniania danych wstecz.** Tylko poczta zarchiwizowana po przełączeniu otrzymuje oryginał.
- **Brakujący oryginał jest logowany jako błąd.** Gdy przechwycenie zostanie odrzucone (wiadomość powyżej `MaxMessageSizeMb`, brak używalnego przechwycenia) lub gdy zapis do skonfigurowanego backendu zakończy się niepowodzeniem, dziennik zawiera błąd z identyfikatorem wiadomości e-mail oraz powodem. Wiadomość e-mail pozostaje w Archiwum bez swojego oryginału; nie ma automatycznego uzupełniania wstecz ani ponawiania prób.
- **Strona szczegółów oznacza brakujący oryginał.** Przy `RawMessages:Enabled` strona szczegółów wyświetla powiadomienie nad nagłówkami. Pyta ona backend pamięci masowej, więc plik systemu plików, który zniknął, również zostanie oznaczony. Lista wiadomości e-mail nie posiada odznaki dla każdego wiersza; zajęłoby to zbyt dużo miejsca w kolumnie statusu.
- **Rekonstrukcja nie jest wiadomością.** Dla wiadomości e-mail bez przechowywanego oryginału każda ścieżka pobierania zwraca odbudowaną wiadomość i informuje o tym za pomocą `X-Raw-Message-Reconstructed: true`.
- **System plików oznacza dwie jednostki kopii zapasowej.** Baza danych i katalog wiadomości muszą zostać przywrócone razem, w przeciwnym razie wiersze przetrwają bez swojej zawartości.
- **`VerifyChecksumOnRead` obciąża procesor CPU.** Każde pobranie dużej wiadomości wiąże się z dekompresją i haszowaniem. Nie wyłączaj tej funkcji, chyba że zmierzysz problem; jest to jedyna ochrona przed cichą zmianą danych na dysku.
- **Przechowywany oryginał nie podlega procesowi deduplikacji.** Załącznik, który pojawia się w wielu wiadomościach e-mail, jest przechowywany raz w magazynie załączników, ale raz dla każdej wiadomości e-mail wewnątrz oryginału.
- **Usunięcie usuwa również oryginał.** Wiadomość e-mail usunięta przez Przechowywanie lub ręcznie zabiera ze sobą swój przechowywany oryginał, plik i wiersz. Nic nie jest zachowywane, jeśli Archiwum nie wymienia już danej pozycji.

## 🔗 Zobacz także {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - pełny proces migracji Archiwum
- [Setup and Parameters](Setup.md) - wszystkie parametry konfiguracji
- [REST API](API.md#download-original-message) - punkt końcowy `/raw`
- [MCP Server](MCP.md#get_email_raw) - narzędzie `get_email_raw`
- [Backup and Restore](BackupRestore.md) - co objąć Kopia zapasowa i Przywracanie
- [Attachment Deduplication](AttachmentDeduplication.md) - jak są przechowywane Załącznik i Deduplikacja
