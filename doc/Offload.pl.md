# 📤 Date-Windowed Offload {#date-windowed-offload}

[← Powrót do indeksu dokumentacji](index.md)

## 📋 Przegląd {#overview}

Offload dołącza zarchiwizowaną pocztę nowszą niż określona data graniczna do innej Skrzynki pocztowej, pozostawiając wszystko starsze w Archiwum. Narzędzie to służy do migracji, w których nowa Skrzynka pocztowa powinna zawierać tylko niedawną pocztę, podczas gdy pełna historia pozostaje dostępna poprzez Mail Archiver.

Różni się od [Copy All Emails to Another Mailbox](MailboxMigration.md) na pięć sposobów:

| | Copy All Emails | Offload |
|---|---|---|
| Filtr daty | brak, wszystko lub nic | data graniczna, z opcjonalną górną granicą |
| Powtarzalność | nie, ponownie dołącza wszystko | tak, pomija to, co już znajduje się w celu |
| Wykluczenia Folderów | nie | tak, konfigurowalne |
| Zmiana nazw Folderów | nie | tak, konfigurowalne |
| Podgląd | nie | raport typu dry run dla każdego Folderu bez zapisu |

Offload zawsze tylko dołącza dane do celu. Nigdy nie usuwa danych z Archiwum i nigdy nie dotyka źródłowej Skrzynki pocztowej.

## 🔁 Powtarzanie uruchomienia jest bezpieczne {#repeating-a-run-is-safe}

Przed pierwszym dołączeniem, cała docelowa Skrzynka pocztowa jest indeksowana, a każda wiadomość jest sprawdzana pod jej kątem. Powtórzone uruchomienie nie dołącza zatem niczego, co już dotarło, lecz raportuje to jako już obecne. Ma to trzy praktyczne konsekwencje:

- Przerwanego uruchomienia można dokończyć, po prostu uruchamiając je ponownie.
- Zakończone uruchomienie można powtórzyć jako etap weryfikacji. Poprawny wynik to brak dołączonych elementów i informacja, że wszystko jest już obecne.
- Wiadomości usunięte w celu pojawią się przy następnym uruchomieniu, ponieważ nie ma ich już w indeksie.

Dwa kryteria decydują o tym, czy wiadomość jest już obecna, w następującej kolejności:

1. **Message-ID**, po normalizacji. To wykonuje niemal całą pracę.
2. **Fingerprint** (odcisk palca) nadawcy, odbiorców, tematu i czasu wysłania w ramach dwóch sekund. Obejmuje to wiadomości, których zapisany Message-ID jest nieobecny lub błędny. Takich wiadomości nie można w ogóle przywrócić z ich oryginalnym Message-ID, więc bez tego drugiego kryterium byłyby duplikowane przy każdym uruchomieniu.

Zadanie raportuje oddzielnie, ile dopasowań pochodziło z fingerprintu, ponieważ jest to kryterium, które mogłoby po cichu zawieść.

## ⚙️ Konfiguracja {#configuration}

Nowa sekcja `Offload` z wartościami domyślnymi, które odtwarzają poprzednie zachowanie:

```json
"Offload": {
  "MaxConcurrentJobs": 1,
  "PrefetchMaxMessages": 500000,
  "ExcludedSourceFolders": [],
  "FolderRenameMap": {},
  "MarkAsSeen": true
}
```

| Klucz | Wartość domyślna | Cel |
|---|---|---|
| `MaxConcurrentJobs` | `1` | Ile zadań Przywracanie lub offload może działać jednocześnie. Wartość ta utrzymuje ściśle szeregowe zachowanie, które zawsze posiadała kolejka zadań. Niezależnie od tej wartości, uruchamiane jest maksymalnie jedno zadanie na docelową Skrzynkę pocztową. |
| `PrefetchMaxMessages` | `500000` | Górna granica liczby wiadomości indeksowanych z docelowej Skrzynki pocztowej. Powyżej tej wartości sprawdzanie duplikatów zawęża się do pojedynczego Folderu i loguje tę czynność. |
| `ExcludedSourceFolders` | empty | Foldery źródłowe, które nigdy nie są poddawane offload. Sprawdzane przed zmianą nazwy, obejmuje podfoldery. |
| `FolderRenameMap` | empty | Nadpisuje początkowe segmenty ścieżki Folderu źródłowego. |
| `MarkAsSeen` | `true` | Czy dołączona poczta ma być oznaczona jako przeczytana. |

Oba ustawienia Folderów są celowo puste. Nadpisywanie lub pomijanie Folderów bez zapytania zaskoczyłoby każdego, kto już korzysta z ścieżki Przywracanie, dlatego uruchomienie bez konfiguracji migruje wszystko, włączając Foldery ze spamem, i tworzy drugi zestaw specjalnych Folderów obok własnych Folderów celu.

### Wykluczenia i zmiana nazw Folderów {#folder-exclusions-and-renaming}

Dla migracji z Exchange do Dovecot dwa ustawienia zazwyczaj wyglądają następująco:

```json
"ExcludedSourceFolders": ["Junk E-Mail", "Deleted Items"],
"FolderRenameMap": {
  "Sent Items": "Sent",
  "Deleted Items": "Trash",
  "Junk E-Mail": "Junk"
}
```

Bez mapy nazw każda zmigrowana Skrzynka pocztowa kończy z zarówno `Sent`, jak i `Sent Items`, ponieważ Dovecot wstępnie tworzy własne foldery specjalnego użytku.

Warto wiedzieć o czterech rzeczach dotyczących sposobu ich stosowania, ponieważ każda z nich może prowadzić do zaskoczenia:

1. **Wykluczenia są dopasowywane jako pierwsze**, względem nazwy źródłowej. Gdyby zmiana nazwy nastąpiła najpierw, wykluczenie dla `Deleted Items` nigdy by nie zadziałało, gdyby nazwa zmieniła się już na `Trash`.
2. **Zmiana nazwy nadpisuje najdłuższy pasujący prefiks ścieżki**, na granicach segmentów. Zmiana `Sent Items` na `Sent` powoduje również zmianę `Sent Items/2019` na `Sent/2019` i pozostawia `Sent Items Archive` bez zmian.
3. **Dopasowanie nie uwzględnia wielkości liter**, a zarówno `/`, jak i `\` liczą się jako separatory.
4. **Dwa Foldery źródłowe mogą zostać połączone w jeden docelowy.** Sytuacja, w której `Sent Items` i `Sent` stają się `Sent`, jest poprawna i nie powoduje dwukrotnego otwarcia docelowego Folderu.

Zmiana mapy nazw między dwoma uruchomieniami nie powoduje duplikacji, ponieważ sprawdzanie duplikatów obejmuje całą docelową Skrzynkę pocztową, a nie jeden rozwiązany Folder.

## 🖥️ Uruchamianie z interfejsu {#running-it-from-the-interface}

Na stronie szczegółów Konta, **Offload to Another Mailbox** otwiera formularz z docelową Skrzynką pocztową, docelowym Folderem głównym, opcją zachowania struktury Folderów, oknem daty, przełącznikiem dry run oraz opcją oznaczania dołączonej poczty jako przeczytanej. Skonfigurowane wykluczenia i mapa nazw są wyświetlane w trybie tylko do odczytu, więc uruchomienie nigdy nie jest zaskoczeniem.

Zadanie trafia do kolejki, a jego postęp i raport na każdy Folder pojawiają się na stronie statusu zadania oraz w sekcji **Jobs**. Raport na każdy Folder podaje nazwy Folderów źródłowych, więc jest on wyświetlany tylko użytkownikowi, który uruchomił zadanie, oraz administratorom.

Docelowa Skrzynka pocztowa może być wyłączonym Kontem IMAP, np. w celu przygotowania celu migracji przed jego uruchomieniem; uruchomienie zakończy się błędem przy próbie połączenia, jeśli cel jest nieosiągalny. Konta Microsoft 365 nie mogą być celami offload; ścieżka Przywracanie przez Graph pozostaje bez zmian.

### Kto może je uruchamiać {#who-may-run-it}

Administratorzy mogą wykonywać offload między dowolnymi dwoma Kontami. Samodzielny menedżer (self-manager) może wykonywać offload **tylko między Kontami przypisanymi do niego**, na obu końcach: źródłowym, ponieważ strona Konta jest już w ten sposób ograniczona, oraz docelowym, ponieważ lista celów jest ograniczona do tego samego zestawu, a żądanie jest ponownie sprawdzane pod tym kątem po uruchomieniu zadania. Samodzielny menedżer z jednym przypisanym Kontem nie ma zatem uprawnionego celu i otrzymuje taką informację.

Zakres (scoping) pochodzi z `IAccountAccessResolver`, tego samego resolvera, którego używają REST API i serwer MCP, więc istnieje jedna definicja tego, kto może używać której Skrzynki pocztowej. Sama decyzja znajduje się w `OffloadTargetEligibility`, którą wywołują zarówno lista celów, jak i sprawdzanie żądania, dzięki czemu to, co oferuje formularz i co akceptuje serwer, nie może się rozjechać.

## ⌨️ Uruchamianie z linii komend {#running-it-from-the-command-line}

```bash
docker compose exec mailarchive-app dotnet MailArchiver.dll \
  --offload --source-account-id 3 --target-account-id 7 \
  --since 2025-08-01 \
  [--until 2026-08-01] [--target-folder INBOX] \
  [--preserve-folders] [--dry-run] [--no-mark-seen]
```

| Argument | Znaczenie |
|---|---|
| `--source-account-id` | Konto, z którego należy czytać zarchiwizowaną pocztę. Wymagane. |
| `--target-account-id` | Konto IMAP, do którego należy dołączać. Wymagane i musi różnić się od źródła. |
| `--since` | Włączna dolna granica daty wysłania, `YYYY-MM-DD`. Wymagane. |
| `--until` | Opcjonalna włączna górna granica. |
| `--target-folder` | Folder główny w docelowej Skrzynce pocztowej. Domyślnie `INBOX`. |
| `--preserve-folders` | Odtwórz strukturę Folderów źródłowych pod Folderem głównym. |
| `--dry-run` | Raportuj co by się stało, nie dołączaj niczego. |
| `--no-mark-seen` | Dołączaj bez flagi Seen. |

Kody wyjścia:

| Kod | Znaczenie |
|---|---|
| `0` | Wszystko zostało dołączone lub jest już obecne. |
| `1` | Przynajmniej jedna wiadomość zakończyła się niepowodzeniem lub samo uruchomienie zakończyło się niepowodzeniem. |
| `2` | Wywołanie było błędne: złe lub brakujące argumenty, nieznane Konto, cel nie jest IMAP, źródło jest równe celowi. |

Jedno wywołanie obsługuje jedną Skrzynkę pocztową, co pozwala na skryptowanie floty Skrzynek pocztowych z pliku CSV zawierającego źródło, cel i datę graniczną.

> **Uruchamianie kilku jednocześnie.** Zasada jednego zadania na cel istnieje tylko wewnątrz pojedynczego procesu, więc dwa jednoczesne wywołania z linii komend skierowane do *tej samej* docelowej Skrzynki pocztowej mogą każde pobrać swój indeks duplikatów przed rozpoczęciem zapisu przez drugie, a następnie oba mogą dołączyć tę samą pocztę. Rozdziel pracę tak, aby każde jednoczesne wywołanie miało inny cel, co jest normalne, gdy każdy wiersz w CSV to inna Skrzynka pocztowa.

## 🗓️ Jak interpretowana jest data graniczna {#how-the-cutoff-is-interpreted}

- Filtr dotyczy **daty wysłania**, a nie momentu, w którym wiadomość została zarchiwizowana. Znacznik czasu zarchiwizowania to czas uruchomienia procesu archiwizacji i nie ma on znaczenia dla daty granicznej.
- Relatywne okno, takie jak "ostatnie dwanaście miesięcy", jest **rozwiązywane na stałą datę w momencie tworzenia zadania**. Zadanie może trwać długo i być powtarzane później, a musi ono wybierać tę samą pocztę za każdym razem; przechowywane relatywne okno ulegałoby przesunięciu.
- Daty graniczne są interpretowane w **skonfigurowanej strefie czasowej wyświetlania**, tej samej, którą pokazuje ekran wyszukiwania, a dolna granica jest włączna od 00:00 tego dnia. Górna granica obejmuje cały dany dzień. Relatywne okna ("ostatnie N miesięcy") są również rozwiązywane w tej strefie czasowej wyświetlania, zgodnie ze sposobem przechowywania `SentDate`.

### Znane ograniczenie {#known-limitation}

- Gdy indeks duplikatów osiągnie `Offload:PrefetchMaxMessages`, zakres zostaje zmniejszony i zalogowany, zamiast ponownej próby dla pojedynczego Folderu. Fallback `restrictToFolder` oferowany przez budowniczy indeksu nie jest obecnie używany przez ścieżkę offload; zwiększenie limitu jest zalecanym rozwiązaniem.

## 📋 Migracja krok po kroku {#a-migration-end-to-end}

1. Utwórz docelowe Skrzynki pocztowe. Aby utworzyć wiele naraz, zobacz masowy import CSV w [Account Import](Account-Import.md).
2. Skonfiguruj `Offload:ExcludedSourceFolders` i `Offload:FolderRenameMap`. Oba są domyślnie puste, więc pominięcie tego etapu spowoduje migrację Folderów ze spamem i utworzenie duplikowanych specjalnych Folderów.
3. Potwierdź, że każda źródłowa Skrzynka pocztowa jest w pełni zarchiwizowana przed jej offloadem.
4. **Wykonaj dry run wszystkiego.** Sprawdź liczby i rozwiązane nazwy docelowych Folderów przed zapisem czegokolwiek.
5. Rzeczywiste uruchomienie. Zacznij od `MaxConcurrentJobs` ustawionego na 2 i zwiększaj je dopiero po zaobserwowaniu, jak radzi sobie docelowy serwer; Dovecot ogranicza jednoczesne połączenia na użytkownika i na IP.
6. Powtórz uruchomienie, aby zweryfikować. Spodziewaj się braku dołączonych elementów i informacji, że wszystko jest już obecne.
7. Wyłącz źródłowe Konta.

Przepustowość jest ograniczona przez `BatchOperation:PauseBetweenEmailsMs`, co domyślnie wynosi 50 ms, co pozwala na maksymalnie dwadzieścia operacji dołączenia na sekundę, zanim zacznie być liczony czas odpowiedzi serwera.

## 🔍 Co oznaczają liczniki {#what-the-counters-mean}

| Licznik | Znaczenie |
|---|---|
| Appended | Zapisano w celu. W trybie dry run: to, co zostałoby zapisane. |
| Already present | Cel już to posiadał, na podstawie dowolnego z kryteriów. |
| of those by fingerprint | Ile zostało rozpoznanych przez drugie kryterium zamiast przez Message-ID. |
| Excluded folder | Pominięto, ponieważ Folder źródłowy jest wykluczony. |
| Failed | Nie udało się dołączyć. |

Ostrzeżenie jest zgłaszane, jeśli indeks duplikatów nie objął całej docelowej Skrzynki pocztowej, co dzieje się powyżej `PrefetchMaxMessages` lub gdy nie można było odczytać Folderu. W takim stanie powtórzone uruchomienie może dołączyć pocztę, która jest już obecna w częściach, które nie zostały zaindeksowane.

## ⚠️ Uwagi i limity {#notes-and-limits}

- Przywrócona poczta niesie teraz swoją **oryginalną datę dostarczenia** jako wewnętrzną datę IMAP, pobraną z łańcucha `Received` wiadomości, z opcją powrotu do daty wysłania. Wcześniej niosła czas uruchomienia archiwizacji, co psuło sortowanie po przybyciu po stronie serwera i wszelkie reguły oparte na wieku w celu.
- Ponieważ powtórzone uruchomienie pomija to, co jest już obecne, błędnej wewnętrznej daty nie można naprawić poprzez ponowne uruchomienie: wiadomości zostaną rozpoznane i pominięte. Upewnij się, że jest ona poprawna przed uruchomieniem produkcyjnym lub usuń dotknięte wiadomości w celu i uruchom ponownie.
- Microsoft 365 nie jest wspierany jako cel offload.
- Wykrywanie duplikatów może rozpoznać tylko to, co widzi w docelowej Skrzynce pocztowej. Poczta, która dotarła do celu inną trasą, jest wyłapywana tylko w takim stopniu, w jakim dwa kryteria przypadkowo jej odpowiadają.
