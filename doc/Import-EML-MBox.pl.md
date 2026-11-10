# 📥 Importowanie poczty w interfejsie Web UI (EML / MBox) {#importing-mail-in-the-web-ui-eml-mbox}

[← Powrót do spisu treści dokumentacji](index.md)

## 📋 Przegląd {#overview}

Oprócz [interfejsu wiersza poleceń](CLI.md) oraz
[importu kont za pomocą CSV](Account-Import.md), Mail Archiver może importować istniejące
**Archiwum** poczty **poprzez interfejs webowy**. Dostępne są dwa formularze przesyłania:

| Formularz | Akceptowany plik | Wynik |
|------|---------------|--------|
| **Import MBox** | `.mbox`, `.mbx` | Każda wiadomość w pliku jest importowana do **jednego** wybranego przez Ciebie folderu |
| **Import EML** | `.zip` zawierający pliki `.eml` | **Struktura folderów wewnątrz ZIP** decyduje o tym, gdzie trafi każda wiadomość |

Oba formularze archiwizują wiadomości w istniejącym koncie Mail Archiver, oba działają jako
**zadania w tle** i oba **automatycznie pomijają duplikaty** — dwukrotny import
tego samego pliku nie tworzy drugiej kopii.

> 💡 **Wskazówka**: Jeśli Twoje Archiwum znajduje się już na serwerze jako plik, lepiej użyć
> [CLI](CLI.md). Pozwala to uniknąć przesyłania przez przeglądarkę i jest
> lepszym wyborem dla wielogigabajtowych archiwów.

## 🧭 Dostęp {#access}

1. Otwórz **Mail Accounts** w menu głównym.
2. Użyj rozwijanego menu **Import** nad listą kont i wybierz
   **Import MBox** lub **Import EML**.

Formularze są dostępne dla **administratorów** (wszystkie konta) oraz **zarządców własnych**
(konta przypisane do nich); zobacz
[User Management](UserManagement.md#-user-account-permissions). Tylko **włączone**
konta są oferowane jako cele importu — wyłączonego konta nie można wybrać.

## 📨 Import MBox {#import-mbox}

MBox to format używany przez większość klientów pocztowych do eksportu folderów oraz przez
kopie zapasowe typu `mbox`.

| Pole | Znaczenie |
|-------|---------|
| **MBox File** | Plik `.mbox` / `.mbx` do przesłania |
| **Target Account** | Konto Mail Archiver, które przyjmuje wiadomości |
| **Target Folder** | Folder, do którego importowane są wiadomości. Wybierz jeden z istniejących folderów konta lub wpisz nową nazwę — folder, który jeszcze nie istnieje, zostanie **utworzony automatycznie** podczas importu. Domyślnie: `INBOX`. |

Wszystkie wiadomości w pliku są importowane do tego jednego folderu docelowego. Sam plik MBox
nie zawiera informacji o folderach, więc struktura oryginalnej
skrzynki pocztowej nie zostaje odtworzona — jeśli tego potrzebujesz, skorzystaj z metody EML/ZIP.

## 🗂️ Import EML {#import-eml}

Import EML wymaga **archiwum ZIP**, którego wpisy są plikami `.eml`.

- Pliki `.eml` w **korzeniu** ZIP są importowane do `INBOX`.
- Pliki `.eml` w **podfolderze** są importowane do folderu o **tej samej
  nazwie**, co dany podfolder.

Zatem ZIP ułożony w następujący sposób:

```
mailbox.zip
├── 2024-invoice.eml            → INBOX
├── Archive/
│   └── old-message.eml         → Archive
└── Projects/
    └── 2026/
        └── kickoff.eml         → Projects/2026
```

przywraca drzewo folderów wraz z wiadomościami. Pojedyncze pliki `.eml` mogą zostać
przesłane, jeśli najpierw zostaną spakowane do ZIP (`zip emails.zip *.eml`).

## ⚙️ Wspólne zachowanie {#shared-behaviour}

Oba formularze zachowują się w ten sam sposób po naciśnięciu **Start Import**:

- **Limit rozmiaru pliku** — konfigurowany za pomocą `Upload__MaxFileSizeGB` (domyślnie **10 GB**).
  Formularz pokazuje aktualny limit obok selektora plików.
- **Przetwarzanie w tle** — przesyłanie jest kolejkowane jako zadanie; nie musisz
  trzymać otwartej przeglądarki.
- **Obsługa duplikatów** — wiadomości, które znajdują się już w Archiwum, są pomijane
  i liczone oddzielnie. Duplikaty są dopasowywane na podstawie nagłówka `Message-ID`.
- **Nieprawidłowe dane wejściowe** — nieczytelne pliki są pomijane pojedynczo i raportowane na
  stronie statusu zamiast przerywania całego importu.
- **Postęp** — zostaniesz przeniesiony bezpośrednio na stronę statusu zadania, która pokazuje
  ID zadania, nazwę pliku, rozmiar pliku, status, liczbę przetworzonych/całkowitych wiadomości oraz bieżące liczniki. To samo zadanie pojawia się również na stronie [Jobs](BackgroundJobs.md).
- **Anulowanie** — trwający import można anulować ze strony jego statusu.
  Wiadomości zaimportowane do tego momentu pozostają w Archiwum.
- **Ścieżka audytu** — import jest zapisywany w [access log](Logs.md), a
  wartość miejsca przechowywanego na koncie jest odświeżana po zakończeniu (zobacz
  [Per-Account Storage](AccountStorage.md)).

### Statusy zadań {#job-statuses}

`Queued` → `Running` → `Completed` / `Failed` / `Cancelled`

Zakończone zadanie raportuje, ile wiadomości zostało zaimportowanych, ile zakończyło się niepowodzeniem, a ile
zostało pominiętych jako duplikaty — na przykład:
*"1420 zaimportowano, 3 nie powiodło się, 87 duplikatów"*. W przypadku udanego importu przesłany
plik jest usuwany z serwera; nieudane wpisy są przechowywane przez czas życia
zadania, aby można było sprawdzić, co zostało odrzucone.

## ⚠️ Na co uważać {#things-to-watch-out-for}

- **Błędny typ pliku** — przesyłanie zostanie odrzucone, chyba że rozszerzenie to jedno z
  `.mbox`, `.eml` lub `.zip`. Selektor w formularzu EML akceptuje tylko `.zip`, a selektor
  w formularzu MBox tylko `.mbox` i `.mbx`.
- **"0 zaimportowano, N duplikatów"** — nie jest to błąd. Wiadomości znajdują się już
  w Archiwum, najprawdopodobniej z wcześniejszego importu lub synchronizacji tego samego źródła.
- **Bardzo duże archiwa** — plik MBox jest czytany wiadomo po wiadomości, więc
  wielogigabajtowy plik zajmuje trochę czasu nawet w tle. Monitoruj postęp na
  stronie statusu zamiast ponowić przesyłanie.
- **Wolne miejsce na dysku** — przesłany plik jest tymczasowo przechowywany na serwerze i
  wymaga miejsca obok bazy danych Archiwum.
- **Nazwy folderów ze znakami specjalnymi** — nazwy folderów są pobierane ze ścieżek ZIP
  lub z Twojego wpisu; sprawdź listę folderów konta po imporcie, jeśli
  użyłeś nietypowych znaków.

## 🔗 Powiązana dokumentacja {#related-documentation}

- [CLI commands](CLI.md) — importowanie z zamontowanego katalogu
- [Account Import (CSV)](Account-Import.md) — dodawanie wielu kont jednocześnie
- [Background Jobs](BackgroundJobs.md) — monitorowanie i anulowanie zadań importu
- [Exporting a Mail Account](Export-Account.md) — proces odwrotny
- [Setup and Parameters](Setup.md) — ustawienia `Upload__*`
