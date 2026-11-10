# 📧 Original Message Storage {#original-message-storage}

[← Powrót do spisu treści dokumentacji](index.md)

## 📋 Przegląd {#overview}

Domyślnie Archiwum przechowuje **przetworzone części** wiadomości e-mail: nagłówki, tekstowe i HTML treści, załączniki. Wyeksportowany `.eml` jest odbudowywany na podstawie tych pól.

To wystarczy, aby przeczytać e-mail, ale nie jest to wiadomość **w takiej, jaka została odebrana**. Kodowania transferu, granice MIME i zawijanie nagłówków różnią się od oryginału, więc odbudowana wiadomość nie przechodzi już weryfikacji podpisu DKIM, a skanery spamu oceniają ją inaczej.

Gdy przechowywanie oryginalnej wiadomości jest włączone, Archiwum przechowuje pełną wiadomość w takiej formie, w jakiej dotarła, obok przetworzonych pól. Przechowywany oryginał to to, co Archiwum może udowodnić jako dostarczone.

| | Tylko przetworzone pola | Z przechowywanym oryginałem |
| --- | --- | --- |
| Nagłówki, treść, załączniki | ✅ | ✅ |
| Wiadomość identyczna co do bajtu | ❌ | ✅ |
| Weryfikacja podpisu DKIM | ❌ | ✅ |
| Zastosowania kryminalistyczne (sąd, audyt) | ograniczone | ✅ |

## 🎯 Gdzie serwowana jest oryginał {#where-the-original-is-served}

Po zapisaniu, oryginał jest zwracany wszędzie tam, gdzie oczekuje się kompletnej wiadomości:

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
| `X-Raw-Message-Source: serialized` | Przeanalizowana wiadomość zapisana ponownie. Identyczna, z wyjątkiem zakończeń linii będących CRLF (import MBox, pobieranie podczas odzyskiwania IMAP). |
| `X-Raw-Message-Sha256` | SHA-256 zapisanego oryginału. |
| `X-Raw-Message-Reconstructed: true` | Brak zapisanego oryginału; wiadomość została odbudowana. Brak nagłówka sumy kontrolnej. |
| `X-Raw-Message-Storage` | Backend, z którego odczytano oryginał (`postgres` lub `filesystem`). |

Nazwa pliku również to wskazuje: `email-{id}-original-...eml` dla zapisanego oryginału, `email-{id}-...eml` dla rekonstrukcji.

W przypadku, gdy zapisany oryginał **nie przejdzie weryfikacji sumy kontrolnej**, eksport i przywracanie zostają odrzucone z komunikatem o błędzie. Zwrócenie rekonstrukcji zamiast oryginału ukryłoby fakt, że zarchiwizowana kopia została zmieniona, a tego Archiwum nigdy nie może robić.

Przywracanie odczytuje zapisany oryginał i dołącza go poprzez klienta dostawcy, dzięki czemu Skrzynka pocztowa otrzymuje oryginalne nagłówki i strukturę MIME. Dokładny strumień bajtów przesyłany przez sieć nie jest gwarantowany, ponieważ klient ponownie serializuje wiadomość; sama zarchiwizowana kopia pozostaje niezmieniona.

## ⚙️ Ustawienia {#settings}

Wszystkie ustawienia znajdują się w sekcji `RawMessages` lub jako zmienne środowiskowe z prefiksem `RawMessages__`.

| Ustawienie | Domyślne | Opis |
| --- | --- | --- |
| `RawMessages:Enabled` | `false` | Przechowuj oryginalną wiadomość każdej nowo zarchiwizowanej wiadomości e-mail. Wiadomości zarchiwizowane przed włączeniem tej opcji nie posiadają oryginału. |
| `RawMessages:MaxMessageSizeMb` | `50` | Wiadomości większe niż ta wartość są archiwizowane bez oryginału. `0` = brak limitu. |
| `RawMessages:IncludeGraph` | `true` | Przechowuj również oryginał dla kont Microsoft 365 (Graph). Kosztuje to jedno dodatkowe żądanie Graph na wiadomość, co obciąża limit przepustowości (throttling). |
| `RawMessages:Storage` | `postgres` | Miejsce przechowywania bajtów wiadomości: `postgres` (w bazie danych) lub `filesystem` (plik gzip obok Archiwum). Zobacz [Storage backend](#storage-backend). |
| `RawMessages:FileSystem:BasePath` | `RawMessages` | Katalog, do którego zapisywane są pliki wiadomości, względem głównego katalogu aplikacji. Skieruj to na zamontowany wolumen w Dockerze. |
| `RawMessages:FileSystem:ShardDepth` | `2` | Liczba dwuznakowych poziomów katalogów, na które rozłożony jest identyfikator e-mail. `2` daje 10 000 katalogów, `0` jest płaski. Utrzymuje niską liczbę plików na katalog w dużych Archiwach. |
| `RawMessages:FileSystem:VerifyChecksumOnRead` | `true` | Ponownie obliczaj SHA-256 pliku przy każdym pobieraniu i porównuj go z sumą kontrolną zapisaną w momencie archiwizacji. Niezgodność powoduje błąd pobierania z kodem HTTP 500 zamiast serwowania zmienionych bajtów. |

### Skalowanie {#sizing}

Przechowywanie rośnie mniej więcej o rozmiar każdej wiadomości wraz z jej załącznikami, po kompresji gzip. Załączniki **nie** podlegają deduplikacji wewnątrz przechowywanego oryginału, więc e-mail z dużym załącznikiem kosztuje dwa razy tyle (raz w zdeduplikowanym magazynie załączników i raz wewnątrz oryginału). Należy planować rozmiar bazy danych na poziomie około **dwukrotnie** większym niż w przypadku Archiwum bez oryginałów.

Włączenie tej opcji nie uzupełnia istniejących wiadomości. Tylko nowo archiwizowane wiadomości e-mail otrzymują oryginał.

## 💾 Backend przechowywania {#storage-backend}

Ustawienie `Storage` decyduje o tym, gdzie znajdują się bajty. **Wiersz metadanych zawsze pozostaje w
bazie danych**, niezależnie od skonfigurowanego backendu, i przechowuje SHA-256, rozmiar, źródło
przechwycenia oraz (dla plików) ścieżkę względną. To właśnie ten wiersz pozwala Archiwum udowodnić,
co powinna zawierać przechowywana wiadomość.

### postgres (domyślny) {#postgres-default}

Skompresowana wiadomość znajduje się w kolumnie `Content` tabeli `mail_archiver."RawMessages"`.
Jest to zachowanie, które Archiwum miało zawsze i które nie wymaga dodatkowej wolumenów.

Wybierz tę opcję, gdy baza danych jest już Twoją jednostką do Kopia zapasowa i chcesz mieć
jedną rzecz do Kopia zapasowa i Przywracanie.

### filesystem {#filesystem}

Wiadomość jest zapisywana w `{BasePath}/{xx}/{yy}/{id}.eml.gz`, gdzie `xx`/`yy` są pochodne od identyfikatora e-mail.
Wiersz w bazie danych przechowuje tylko metadane.

Wybierz tę opcję, gdy Archiwum jest na tyle duże, że oryginały zdominowałyby rozmiar bazy danych,
lub gdy chcesz umieścić pliki wiadomości na tańszym lub oddzielnym nośniku.

!!! danger "Zamontuj wolumen w Docker"
    Domyślny `BasePath` (`RawMessages`) rozwiązuje się do `/app/RawMessages` **wewnątrz
    kontenera**. Bez bind mount tego katalogu należy do warstwy zapisywalnej kontenera, więc każdy
    ponownie utworzony kontener – w tym po `docker compose down && docker compose
    up -d`, aktualizacji obrazu lub reboocie hosta z warstwą efemeryczną – zabiera ze sobą każdy
    przechowywany oryginał, podczas gdy wiersze w bazie danych przetrwają.

    Zawsze montuj katalog do hosta podczas używania backendu filesystem:

    ```yaml
    services:
      mailarchive-app:
        volumes:
          - ./raw-messages:/app/RawMessages
    ```

    Ścieżka hosta (tutaj `./raw-messages`) to to, co robisz Kopia zapasowa. Ścieżka kontenera musi
    zgadzać się z `RawMessages:FileSystem:BasePath` lub ustaw `RawMessages:FileSystem:BasePath` na
    punkt montowania, który wybrałeś. Jeśli zmienisz miejsce montowania, spraw, aby `BasePath` nadal na nie wskazywało:
    przechowywane wartości `StoragePath` są względne, więc nadal będą poprawnie rozwiązywane.

### Zmiana backendu {#switching-backends}

Odczyty podążają za przechowywaniem zapisanym w każdym wierszu, a nie za skonfigurowanym ustawieniem.
Po zmianie `Storage`, wcześniej zarchiwizowane wiadomości pozostają dostępne do pobrania z miejsca,
w którym się znajdują. Ustawienie wpływa tylko na **nowo** archiwizowane pocztę.

Aby przenieść to, co jest już przechowywane, uruchom migrację przechowywania. Pełną dokumentację
znajdziesz w [poleceniach CLI](CLI.md#moving-stored-original-messages):

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
  która nie przejdzie weryfikacji, zostaje w miejscu, w którym była, i zostaje zgłoszona: uszkodzony
  oryginał nigdy nie jest propagowany, a jego jedyna pozostała kopia nigdy nie jest usuwana.
- Plik jest usuwany dopiero po zatwierdzeniu transakcji bazy danych, która go przeniosła, więc
  przerwane działanie nie może spowodować utraty jedynej kopii.
- Proces można wznowić. Praca jest wybierana przez kolumnę `StorageType`, więc ponowne uruchomienie
  polecenia kontynuuje pracę z tym, co jeszcze nie zostało przetworzone.
- Kod wyjścia to `1`, jeśli jakakolwiek wiadomość zakończyła się błędem, w przeciwnym razie `0`.

Migracja nie jest wymagana do działania Archiwum. Jest ona potrzebna tylko do przeniesienia istniejących
bajtów do innego backendu.

## 🛟 Kopia zapasowa i przywracanie {#backup-and-restore}

=== "postgres backend"
    Nic dodatkowego. Oryginały są częścią bazy danych i są objęte standardowym
    zrzutem, patrz [Backup and Restore](BackupRestore.md).

=== "filesystem backend"
    Wykonaj kopię zapasową katalogu `BasePath` **wraz z** zrzutem bazy danych.

    Przywracanie bazy danych bez plików pozostawia Archiwum z wierszami, których zawartość
    nie może zostać dostarczona. Pobieranie zwraca wtedy błąd HTTP 404 zamiast zwracać coś
    błędnego, a same wiadomości e-mail pozostają czytelne z ich sparsowanych pól.

## 🗑️ Usuwanie wiadomości e-mail {#deleting-an-email}

Usunięcie wiadomości e-mail usuwa również jej oryginał: reguły Przechowywanie, ręczne usuwanie pojedyncze i masowe oraz usunięcie całego Konta pocztowego usuwają plik jako część tej samej operacji. W przypadku backendu `postgres` nie trzeba robić nic dodatkowego, ponieważ bajty znajdują się wewnątrz wiersza, który jest już usuwany w ramach kaskady.

Plik jest usuwany **po** zatwierdzeniu wiersza wiadomości e-mail, nigdy przed. Te dwa kroki nie mogą stanowić jednej transakcji, ponieważ plik nie jest częścią transakcji bazy danych, więc jeden z nich musi nastąpić jako drugi. Ta kolejność jest bezpieczna: usunięcie, które zakończy się niepowodzeniem lub zostanie wycofane, na przykład na zablokowanym rekordzie, pozostawia oryginał nienaruszonym, a awaria między tymi dwoma krokami pozostawia plik bez wiersza, który zostanie odzyskany przez poniższe czyszczenie. Inna kolejność zniszczyłaby oryginał wiadomości e-mail, która wciąż istnieje.

**Osierocone pliki są zbierane.** Wszystko, co prześlizgnęło się przez proces, w tym pliki pozostawione przez usunięcia sprzed wprowadzenia tego czyszczenia, jest usuwane przez okresowe skanowanie `BasePath`, które porównuje każdy plik z wierszami i usuwa te, do których nie odnosi się żadna wiadomość e-mail:

| Setting | Default | Meaning |
| --- | --- | --- |
| `RawMessages__FileSystem__OrphanCleanupEnabled` | `true` | Włącz lub wyłącz skanowanie |
| `RawMessages__FileSystem__OrphanCleanupIntervalHours` | `24` | Jak często jest uruchamiane |
| `RawMessages__FileSystem__OrphanCleanupGraceHours` | `24` | Pliki młodsze od tej wartości nigdy nie są dotykane |
| `RawMessages__FileSystem__OrphanCleanupStartupDelaySeconds` | `300` | Opóźnienie przed pierwszym uruchomieniem, aby najpierw zakończyła się oczekująca migracja storage |

Okres karencji sprawia, że skanowanie jest bezpieczne: import zapisuje plik przed zatwierdzeniem wiersza, który na niego wskazuje, a pliku zapisanego przed chwilą nie można pomylić z osieroconym. Pliki `.tmp` zapisane w połowie w wyniku przerwanego zapisu są zbierane na podstawie tej samej reguły. W przypadku backendu `postgres` skanowanie nie wykonuje żadnych działań i nawet nie przeszukuje katalogu.

## 🔍 Weryfikacja Archiwum {#verifying-an-archive}

Suma kontrolna zarejestrowana w czasie tworzenia Archiwum umożliwia przeprowadzenie weryfikacji.

- **Podczas pobierania** (`VerifyChecksumOnRead=true`, domyślnie) plik jest hashowany i
  porównywany. Niezgodność powoduje odrzucenie pobierania z błędem HTTP 500.
- **Podczas migracji składowania** każda wiadomość jest weryfikowana przed skopiowaniem, w obu
  kierunkach.

Ponieważ wiersz metadanych znajduje się w bazie danych, a bajty mogą znajdować się na dysku, porównanie sum kontrolnych jest jedynym sposobem na zauważenie, że plik został zmieniony poza aplikacją.

## ⚠️ Ważne informacje {#things-to-know}

- **Włączenie funkcji nie wypełnia danych wstecz.** Tylko wiadomości zarchiwizowane po przełączeniu otrzymują oryginał.
- **Rekonstrukcja to nie jest wiadomość.** W przypadku e-maili bez przechowywanego oryginału, każda ścieżka pobierania zwraca odbudowaną wiadomość i informuje o tym poprzez `X-Raw-Message-Reconstructed: true`.
- **System plików oznacza dwie jednostki kopii zapasowej.** Baza danych i katalog wiadomości muszą być przywracane razem, w przeciwnym razie wiersze przetrwają bez swojej zawartości.
- **`VerifyChecksumOnRead` obciąża procesor.** Każde pobranie dużej wiadomości wiąże się z dekompresją i haszowaniem. Pozostaw tę funkcję włączoną, chyba że zmierzysz problem; jest to jedyna ochrona przed cichą zmianą danych na dysku.
- **Przechowywany oryginał nie podlega procesowi deduplikacji.** Załącznik, który pojawia się w wielu e-mailach, jest przechowywany raz w magazynie załączników, ale raz na każdy e-mail wewnątrz oryginału.
- **Usunięcie usuwa również oryginał.** E-mail usunięty przez funkcję Przechowywanie lub ręcznie zabiera ze sobą przechowywany oryginał, zarówno plik, jak i wiersz. Nic nie zostaje zachowane, jeśli Archiwum nie wymienia już danej pozycji.

## 🔗 Zobacz także {#see-also}

- [CLI Commands](CLI.md#moving-stored-original-messages) - pełny proces migracji Archiwum
- [Setup and Parameters](Setup.md) - wszystkie parametry konfiguracji
- [REST API](API.md#download-original-message) - punkt końcowy `/raw`
- [MCP Server](MCP.md#get_email_raw) - narzędzie `get_email_raw`
- [Backup and Restore](BackupRestore.md) - co objąć procesem Kopia zapasowa i Przywracanie
- [Attachment Deduplication](AttachmentDeduplication.md) - jak przechowywane są Załączniki i Deduplikacja
